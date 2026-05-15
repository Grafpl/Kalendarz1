using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace Kalendarz1.Transport.Services
{
    /// <summary>
    /// Ładuje wolne zamówienia (bez przypisanego kursu) z mapowaniem GPS.
    /// Wyciągnięte z TransportMapaWindow w Fazie 4-B — żeby MapaFlotyView mógł korzystać z tej samej logiki.
    ///
    /// Pipeline (3 etapy):
    /// 1. SQL LibraNet.ZamowieniaMieso WHERE TransportStatus != 'Przypisany' AND TransportKursID IS NULL
    ///    + JOIN Handel.STContractors dla nazwy/adresu/handlowca per klient
    /// 2. Lookup w LibraNet.KodyPocztowe (cache) — szybkie, bez API call
    /// 3. Fallback: Google Geocoding REST dla brakujących, wynik zapisany do KodyPocztowe (na przyszłość)
    /// </summary>
    public class WolneZamowieniaMapaService
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private readonly Action<string>? _log;
        private static readonly HttpClient _http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        { Timeout = TimeSpan.FromSeconds(15) };

        public WolneZamowieniaMapaService(string connLibra, string connHandel, Action<string>? log = null)
        {
            _connLibra = connLibra;
            _connHandel = connHandel;
            _log = log;
        }

        private void Log(string m) => _log?.Invoke(m);

        /// <summary>
        /// Pełen pipeline: SQL → KodyPocztowe → (opcjonalnie) Google geocoding.
        /// </summary>
        /// <param name="data">Data kursu — pobiera zamówienia z DataUboju ∈ [data, data+2 dni].</param>
        /// <param name="useGoogleFallback">Jeśli false: pomiń Google geocoding (KodyPocztowe only).</param>
        public async Task<List<ZamMapItem>> LoadMarkersAsync(DateTime data, bool useGoogleFallback = true)
        {
            var zamowienia = await PobierzZamowieniaAsync(data);
            if (zamowienia.Count == 0) return zamowienia;

            await PrzypiszWspolrzedneAsync(zamowienia);

            if (useGoogleFallback && zamowienia.Any(z => !z.Lat.HasValue))
                await GeokodujBrakujaceAsync(zamowienia);

            return zamowienia;
        }

        // ════════════════════════════════════════════════════════════════════
        // KROK 1: SQL ZamowieniaMieso + Handel join
        // ════════════════════════════════════════════════════════════════════

        private async Task<List<ZamMapItem>> PobierzZamowieniaAsync(DateTime data)
        {
            var lista = new List<ZamMapItem>();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            var dataOd = data.Date;
            var dataDo = data.Date.AddDays(2);
            Log($"SQL: DataUboju ∈ [{dataOd:yyyy-MM-dd}..{dataDo:yyyy-MM-dd}], TransportStatus ∉ Przypisany/Wlasny, KursID IS NULL");

            const string sql = @"
                SELECT
                    zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.DataUboju,
                    ISNULL(zm.LiczbaPalet, 0) AS Palety,
                    ISNULL(zm.LiczbaPojemnikow, 0) AS Pojemniki
                FROM dbo.ZamowieniaMieso zm
                WHERE CAST(zm.DataUboju AS DATE) >= @DataOd
                  AND CAST(zm.DataUboju AS DATE) <= @DataDo
                  AND ISNULL(zm.Status, 'Nowe') NOT IN ('Anulowane')
                  AND ISNULL(zm.TransportStatus, 'Oczekuje') NOT IN ('Przypisany', 'Wlasny')
                  AND zm.TransportKursID IS NULL
                ORDER BY zm.DataPrzyjazdu";

            await using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@DataOd", dataOd);
                cmd.Parameters.AddWithValue("@DataDo", dataDo);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    lista.Add(new ZamMapItem
                    {
                        Id = rdr.GetInt32(0),
                        KlientId = rdr.GetInt32(1),
                        DataPrzyjazdu = rdr.IsDBNull(2) ? data : rdr.GetDateTime(2),
                        DataUboju = rdr.IsDBNull(3) ? data : rdr.GetDateTime(3),
                        Palety = rdr.IsDBNull(4) ? 0 : rdr.GetDecimal(4),
                        Pojemniki = rdr.IsDBNull(5) ? 0 : rdr.GetInt32(5)
                    });
                }
            }
            Log($"SQL zwrócił {lista.Count} zamówień");
            if (lista.Count == 0) return lista;

            // Dane klientów z Handel
            try
            {
                var klientIds = string.Join(",", lista.Select(z => z.KlientId).Distinct());
                await using var cnH = new SqlConnection(_connHandel);
                await cnH.OpenAsync();

                var sqlK = $@"
                    SELECT
                        c.Id,
                        ISNULL(c.Shortcut, 'KH ' + CAST(c.Id AS VARCHAR(10))) AS Nazwa,
                        ISNULL(poa.Street, '') AS Ulica,
                        ISNULL(poa.Postcode, '') AS Kod,
                        ISNULL(poa.City, '') AS Miasto,
                        ISNULL(wym.CDim_Handlowiec_Val, '') AS Handlowiec
                    FROM SSCommon.STContractors c
                    LEFT JOIN SSCommon.STPostOfficeAddresses poa
                        ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'
                    LEFT JOIN SSCommon.ContractorClassification wym ON c.Id = wym.ElementId
                    WHERE c.Id IN ({klientIds})";

                await using var cmdK = new SqlCommand(sqlK, cnH);
                await using var rdrK = await cmdK.ExecuteReaderAsync();
                var dict = new Dictionary<int, (string Nazwa, string Ulica, string Kod, string Miasto, string Handlowiec)>();
                while (await rdrK.ReadAsync())
                {
                    dict[rdrK.GetInt32(0)] = (
                        rdrK.GetString(1),
                        rdrK.GetString(2).Trim(),
                        rdrK.GetString(3).Trim(),
                        rdrK.GetString(4).Trim(),
                        rdrK.GetString(5).Trim()
                    );
                }
                Log($"Handel: dane dla {dict.Count} klientów");

                foreach (var z in lista)
                {
                    if (dict.TryGetValue(z.KlientId, out var k))
                    {
                        z.Klient = k.Nazwa;
                        z.Adres = string.Join(", ", new[] { k.Ulica, k.Kod, k.Miasto }.Where(s => !string.IsNullOrWhiteSpace(s)));
                        z.Kod = k.Kod;
                        z.Miasto = k.Miasto;
                        z.Handlowiec = k.Handlowiec;
                    }
                    else
                    {
                        z.Klient = $"KH #{z.KlientId}";
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Handel BŁĄD: {ex.Message}");
            }

            return lista;
        }

        // ════════════════════════════════════════════════════════════════════
        // KROK 2: KodyPocztowe (LibraNet cache)
        // ════════════════════════════════════════════════════════════════════

        private async Task PrzypiszWspolrzedneAsync(List<ZamMapItem> zamowienia)
        {
            var kody = zamowienia
                .Where(z => !string.IsNullOrWhiteSpace(z.Kod))
                .Select(z => z.Kod.Trim().Replace("-", ""))
                .Distinct()
                .ToList();
            if (kody.Count == 0) return;

            var cache = new Dictionary<string, (double lat, double lng)>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var tableExists = (int)await new SqlCommand(
                    "SELECT COUNT(*) FROM sys.tables WHERE name = 'KodyPocztowe'", cn).ExecuteScalarAsync() > 0;
                if (!tableExists) return;

                var kodParams = string.Join(",", kody.Select((k, i) => $"@k{i}"));
                var sql = $@"SELECT Kod, Latitude, Longitude
                             FROM KodyPocztowe
                             WHERE REPLACE(Kod, '-', '') IN ({kodParams})
                               AND Latitude IS NOT NULL AND Longitude IS NOT NULL";
                await using var cmd = new SqlCommand(sql, cn);
                for (int i = 0; i < kody.Count; i++)
                    cmd.Parameters.AddWithValue($"@k{i}", kody[i]);

                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var kod = rdr.GetString(0).Replace("-", "");
                    var lat = rdr.GetDouble(1);
                    var lng = rdr.GetDouble(2);
                    if (Math.Abs(lat) > 0.001 && Math.Abs(lng) > 0.001)
                        cache[kod] = (lat, lng);
                }
                Log($"KodyPocztowe: {cache.Count}/{kody.Count} kodów ma współrzędne");
            }
            catch (Exception ex)
            {
                Log($"KodyPocztowe BŁĄD: {ex.Message}");
            }

            int assigned = 0;
            foreach (var z in zamowienia)
            {
                if (string.IsNullOrWhiteSpace(z.Kod)) continue;
                var kodNorm = z.Kod.Trim().Replace("-", "");
                if (cache.TryGetValue(kodNorm, out var c))
                {
                    z.Lat = c.lat;
                    z.Lng = c.lng;
                    assigned++;
                }
            }
            Log($"Przypisano współrzędne z KodyPocztowe do {assigned} zamówień");
        }

        // ════════════════════════════════════════════════════════════════════
        // KROK 3: Google Geocoding fallback (i zapis do KodyPocztowe na przyszłość)
        // ════════════════════════════════════════════════════════════════════

        private async Task GeokodujBrakujaceAsync(List<ZamMapItem> zamowienia)
        {
            var brakujace = zamowienia
                .Where(z => !z.Lat.HasValue && !string.IsNullOrWhiteSpace(z.Kod) && z.Kod.Trim().Length >= 5)
                .Select(z => z.Kod.Trim())
                .Distinct()
                .ToList();
            if (brakujace.Count == 0) return;

            var cache = new Dictionary<string, (double lat, double lng)>();
            int ok = 0, fail = 0;

            foreach (var kod in brakujace)
            {
                var coords = await GeokodujGoogle(kod + ", Poland");
                if (coords.HasValue)
                {
                    cache[kod] = coords.Value;
                    ok++;
                }
                else fail++;
                await Task.Delay(30);
            }
            Log($"Google geokoding: {ok} OK, {fail} FAIL z {brakujace.Count}");

            if (cache.Count > 0)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    await new SqlCommand(
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KodyPocztowe') " +
                        "CREATE TABLE KodyPocztowe (Kod NVARCHAR(10) PRIMARY KEY, miej NVARCHAR(100), Latitude FLOAT, Longitude FLOAT)",
                        cn).ExecuteNonQueryAsync();

                    foreach (var (kod, (lat, lng)) in cache)
                    {
                        const string upsert = @"IF NOT EXISTS (SELECT 1 FROM KodyPocztowe WHERE REPLACE(Kod,'-','') = @kodNorm)
                                                INSERT INTO KodyPocztowe (Kod, Latitude, Longitude) VALUES (@kod, @lat, @lng)
                                               ELSE
                                                UPDATE KodyPocztowe SET Latitude=@lat, Longitude=@lng WHERE REPLACE(Kod,'-','')=@kodNorm AND Latitude IS NULL";
                        await using var cmd = new SqlCommand(upsert, cn);
                        cmd.Parameters.AddWithValue("@kod", kod);
                        cmd.Parameters.AddWithValue("@kodNorm", kod.Replace("-", ""));
                        cmd.Parameters.AddWithValue("@lat", lat);
                        cmd.Parameters.AddWithValue("@lng", lng);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                catch (Exception ex)
                {
                    Log($"Zapis KodyPocztowe BŁĄD: {ex.Message}");
                }
            }

            int assignedFromGeo = 0;
            foreach (var z in zamowienia)
            {
                if (!z.Lat.HasValue && !string.IsNullOrWhiteSpace(z.Kod) &&
                    cache.TryGetValue(z.Kod.Trim(), out var c))
                {
                    z.Lat = c.lat;
                    z.Lng = c.lng;
                    assignedFromGeo++;
                }
            }
        }

        private async Task<(double lat, double lng)?> GeokodujGoogle(string addr)
        {
            try
            {
                var apiKey = Kalendarz1.Maps.GoogleMapsConfig.ApiKey;
                if (string.IsNullOrEmpty(apiKey)) return null;
                var q = HttpUtility.UrlEncode(addr);
                var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={q}&key={apiKey}";
                using var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.GetProperty("status").GetString() != "OK") return null;
                var loc = doc.RootElement.GetProperty("results")[0].GetProperty("geometry").GetProperty("location");
                return (loc.GetProperty("lat").GetDouble(), loc.GetProperty("lng").GetDouble());
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>DTO marker zamówienia. Public bo używane przez konsumentów (MapaFlotyView, TransportMapaWindow).</summary>
    public class ZamMapItem
    {
        public int Id { get; set; }
        public int KlientId { get; set; }
        public string Klient { get; set; } = "";
        public string Adres { get; set; } = "";
        public string Miasto { get; set; } = "";
        public string Kod { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public decimal Palety { get; set; }
        public int Pojemniki { get; set; }
        public DateTime DataPrzyjazdu { get; set; }
        public DateTime DataUboju { get; set; }
        public double? Lat { get; set; }
        public double? Lng { get; set; }
    }
}
