using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.HandlowiecDashboard.Models;

namespace Kalendarz1.HandlowiecDashboard.Services
{
    /// <summary>
    /// Serwis danych dla Dashboard Handlowca
    /// </summary>
    public class HandlowiecDashboardService
    {
        private readonly string _connectionStringHandel;
        private readonly string _connectionStringLibraNet;
        private static readonly CultureInfo _pl = new CultureInfo("pl-PL");

        private static readonly string[] _miesiace = {
            "", "Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec",
            "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień"
        };

        public HandlowiecDashboardService()
        {
            _connectionStringHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
            _connectionStringLibraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        }

        /// <summary>
        /// Pobiera KPI dla handlowca (bieżący miesiąc vs poprzedni)
        /// </summary>
        public async Task<HandlowiecKPI> PobierzKPIAsync(string handlowiec = null)
        {
            var kpi = new HandlowiecKPI();
            var dzis = DateTime.Today;
            var pierwszyDzienMiesiaca = new DateTime(dzis.Year, dzis.Month, 1);
            var pierwszyDzienPoprzedniego = pierwszyDzienMiesiaca.AddMonths(-1);

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                // Bieżący miesiąc
                var sqlBiezacy = @"
                    SELECT
                        COUNT(DISTINCT z.ID) as LiczbaZamowien,
                        ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
                        ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as SumaWartosc,
                        COUNT(DISTINCT z.OdbiorcaId) as LiczbaOdbiorcow
                    FROM ZamowieniaMieso z
                    LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                    WHERE z.DataOdbioru >= @DataOd AND z.DataOdbioru < @DataDo
                        AND (z.Anulowane IS NULL OR z.Anulowane = 0)";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sqlBiezacy += " AND z.Handlowiec = @Handlowiec";

                await using (var cmd = new SqlCommand(sqlBiezacy, cn))
                {
                    cmd.Parameters.AddWithValue("@DataOd", pierwszyDzienMiesiaca);
                    cmd.Parameters.AddWithValue("@DataDo", pierwszyDzienMiesiaca.AddMonths(1));
                    if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                        cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                    await using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        kpi.LiczbaZamowienMiesiac = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        kpi.SumaKgMiesiac = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                        kpi.SumaWartoscMiesiac = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                        kpi.LiczbaOdbiorcowMiesiac = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        kpi.SredniWartoscZamowienia = kpi.LiczbaZamowienMiesiac > 0
                            ? kpi.SumaWartoscMiesiac / kpi.LiczbaZamowienMiesiac
                            : 0;
                    }
                }

                // Poprzedni miesiąc
                var sqlPoprzedni = sqlBiezacy;
                await using (var cmd = new SqlCommand(sqlPoprzedni, cn))
                {
                    cmd.Parameters.AddWithValue("@DataOd", pierwszyDzienPoprzedniego);
                    cmd.Parameters.AddWithValue("@DataDo", pierwszyDzienMiesiaca);
                    if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                        cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                    await using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        kpi.LiczbaZamowienPoprzedni = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        kpi.SumaKgPoprzedni = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                        kpi.SumaWartoscPoprzedni = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                        kpi.LiczbaOdbiorcowPoprzedni = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania KPI: {ex.Message}");
            }

            return kpi;
        }

        /// <summary>
        /// Pobiera dane miesięczne dla wykresów (ostatnie 12 miesięcy)
        /// </summary>
        public async Task<List<DaneMiesieczne>> PobierzDaneMiesieczneAsync(string handlowiec = null, int liczbaMiesiecy = 12)
        {
            var dane = new List<DaneMiesieczne>();
            var dzis = DateTime.Today;
            var dataStart = new DateTime(dzis.Year, dzis.Month, 1).AddMonths(-liczbaMiesiecy + 1);

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        YEAR(z.DataOdbioru) as Rok,
                        MONTH(z.DataOdbioru) as Miesiac,
                        COUNT(DISTINCT z.ID) as LiczbaZamowien,
                        ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
                        ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as SumaWartosc,
                        COUNT(DISTINCT z.OdbiorcaId) as LiczbaOdbiorcow
                    FROM ZamowieniaMieso z
                    LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                    WHERE z.DataOdbioru >= @DataStart
                        AND (z.Anulowane IS NULL OR z.Anulowane = 0)";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND z.Handlowiec = @Handlowiec";

                sql += @"
                    GROUP BY YEAR(z.DataOdbioru), MONTH(z.DataOdbioru)
                    ORDER BY Rok, Miesiac";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DataStart", dataStart);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var rok = reader.GetInt32(0);
                    var miesiac = reader.GetInt32(1);
                    dane.Add(new DaneMiesieczne
                    {
                        Rok = rok,
                        Miesiac = miesiac,
                        MiesiacNazwa = _miesiace[miesiac],
                        LiczbaZamowien = reader.GetInt32(2),
                        SumaKg = reader.GetDecimal(3),
                        SumaWartosc = reader.GetDecimal(4),
                        LiczbaOdbiorcow = reader.GetInt32(5),
                        SredniaCena = reader.GetDecimal(3) > 0 ? reader.GetDecimal(4) / reader.GetDecimal(3) : 0
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania danych miesięcznych: {ex.Message}");
            }

            return dane;
        }

        /// <summary>
        /// Pobiera top odbiorców handlowca
        /// </summary>
        public async Task<List<TopOdbiorca>> PobierzTopOdbiorcowAsync(string handlowiec = null, int limit = 10, int miesiecy = 3)
        {
            var odbiorcy = new List<TopOdbiorca>();
            var dataStart = DateTime.Today.AddMonths(-miesiecy);
            decimal sumaCalkowita = 0;

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT TOP (@Limit)
                        z.OdbiorcaId,
                        k.Nazwa,
                        k.Miasto,
                        COUNT(DISTINCT z.ID) as LiczbaZamowien,
                        ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
                        ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as SumaWartosc
                    FROM ZamowieniaMieso z
                    LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                    LEFT JOIN Kontrahenci k ON z.OdbiorcaId = k.id
                    WHERE z.DataOdbioru >= @DataStart
                        AND (z.Anulowane IS NULL OR z.Anulowane = 0)";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND z.Handlowiec = @Handlowiec";

                sql += @"
                    GROUP BY z.OdbiorcaId, k.Nazwa, k.Miasto
                    ORDER BY SumaWartosc DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Limit", limit);
                cmd.Parameters.AddWithValue("@DataStart", dataStart);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                await using var reader = await cmd.ExecuteReaderAsync();
                int pozycja = 1;
                while (await reader.ReadAsync())
                {
                    var odbiorca = new TopOdbiorca
                    {
                        Pozycja = pozycja++,
                        OdbiorcaId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        Nazwa = reader.IsDBNull(1) ? "Nieznany" : reader.GetString(1),
                        Miasto = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        LiczbaZamowien = reader.GetInt32(3),
                        SumaKg = reader.GetDecimal(4),
                        SumaWartosc = reader.GetDecimal(5)
                    };
                    sumaCalkowita += odbiorca.SumaWartosc;
                    odbiorcy.Add(odbiorca);
                }

                // Oblicz udział procentowy
                foreach (var o in odbiorcy)
                {
                    o.UdzialProcent = sumaCalkowita > 0 ? (o.SumaWartosc / sumaCalkowita) * 100 : 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania top odbiorców: {ex.Message}");
            }

            return odbiorcy;
        }

        /// <summary>
        /// Pobiera statystyki kategorii produktów
        /// </summary>
        public async Task<List<KategoriaProduktow>> PobierzKategorieProduktowAsync(string handlowiec = null, int miesiecy = 1)
        {
            var kategorie = new List<KategoriaProduktow>();
            var dataStart = DateTime.Today.AddMonths(-miesiecy);
            decimal sumaCalkowita = 0;

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        CASE
                            WHEN zp.Katalog = '67153' THEN 'Mrożonki'
                            ELSE 'Świeże'
                        END as Kategoria,
                        zp.Katalog,
                        COUNT(DISTINCT z.ID) as LiczbaZamowien,
                        ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
                        ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as SumaWartosc
                    FROM ZamowieniaMieso z
                    INNER JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                    WHERE z.DataOdbioru >= @DataStart
                        AND (z.Anulowane IS NULL OR z.Anulowane = 0)
                        AND zp.Ilosc > 0";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND z.Handlowiec = @Handlowiec";

                sql += @"
                    GROUP BY
                        CASE WHEN zp.Katalog = '67153' THEN 'Mrożonki' ELSE 'Świeże' END,
                        zp.Katalog
                    ORDER BY SumaWartosc DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DataStart", dataStart);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var kat = new KategoriaProduktow
                    {
                        Nazwa = reader.GetString(0),
                        Kod = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        LiczbaZamowien = reader.GetInt32(2),
                        SumaKg = reader.GetDecimal(3),
                        SumaWartosc = reader.GetDecimal(4)
                    };
                    sumaCalkowita += kat.SumaKg;
                    kategorie.Add(kat);
                }

                foreach (var k in kategorie)
                {
                    k.UdzialProcent = sumaCalkowita > 0 ? (k.SumaKg / sumaCalkowita) * 100 : 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania kategorii: {ex.Message}");
            }

            return kategorie;
        }

        /// <summary>
        /// Pobiera ostatnie zamówienia
        /// </summary>
        public async Task<List<OstatnieZamowienie>> PobierzOstatnieZamowieniaAsync(string handlowiec = null, int limit = 20)
        {
            var zamowienia = new List<OstatnieZamowienie>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT TOP (@Limit)
                        z.ID,
                        z.DataOdbioru,
                        k.Nazwa as Odbiorca,
                        ISNULL(SUM(zp.Ilosc), 0) as Kg,
                        ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as Wartosc,
                        CASE WHEN z.Anulowane = 1 THEN 'Anulowane' ELSE 'Aktywne' END as Status,
                        ISNULL(z.TransportStatus, 'Firma') as TransportStatus
                    FROM ZamowieniaMieso z
                    LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                    LEFT JOIN Kontrahenci k ON z.OdbiorcaId = k.id
                    WHERE 1=1";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND z.Handlowiec = @Handlowiec";

                sql += @"
                    GROUP BY z.ID, z.DataOdbioru, k.Nazwa, z.Anulowane, z.TransportStatus
                    ORDER BY z.DataOdbioru DESC, z.ID DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Limit", limit);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    zamowienia.Add(new OstatnieZamowienie
                    {
                        Id = reader.GetInt32(0),
                        DataOdbioru = reader.GetDateTime(1),
                        Odbiorca = reader.IsDBNull(2) ? "Nieznany" : reader.GetString(2),
                        Kg = reader.GetDecimal(3),
                        Wartosc = reader.GetDecimal(4),
                        Status = reader.GetString(5),
                        TransportStatus = reader.GetString(6)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania zamówień: {ex.Message}");
            }

            return zamowienia;
        }

        /// <summary>
        /// Pobiera listę handlowców
        /// </summary>
        public async Task<List<string>> PobierzHandlowcowAsync()
        {
            var handlowcy = new List<string> { "— Wszyscy —" };

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT DISTINCT Handlowiec
                    FROM ZamowieniaMieso
                    WHERE Handlowiec IS NOT NULL AND Handlowiec != ''
                    ORDER BY Handlowiec";

                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                        handlowcy.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania handlowców: {ex.Message}");
            }

            return handlowcy;
        }

        /// <summary>
        /// Pobiera porównanie okresów (ostatnie 6 miesięcy)
        /// </summary>
        public async Task<List<PorownanieOkresu>> PobierzPorownanieOkresowAsync(string handlowiec = null)
        {
            var porownanie = new List<PorownanieOkresu>();
            var dzis = DateTime.Today;
            var biezacyMiesiac = new DateTime(dzis.Year, dzis.Month, 1);

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                for (int i = 0; i < 6; i++)
                {
                    var dataStart = biezacyMiesiac.AddMonths(-i);
                    var dataKoniec = dataStart.AddMonths(1);

                    var sql = @"
                        SELECT
                            COUNT(DISTINCT z.ID) as LiczbaZamowien,
                            ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
                            ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as SumaWartosc,
                            COUNT(DISTINCT z.OdbiorcaId) as LiczbaOdbiorcow
                        FROM ZamowieniaMieso z
                        LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                        WHERE z.DataOdbioru >= @DataStart AND z.DataOdbioru < @DataKoniec
                            AND (z.Anulowane IS NULL OR z.Anulowane = 0)";

                    if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                        sql += " AND z.Handlowiec = @Handlowiec";

                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@DataStart", dataStart);
                    cmd.Parameters.AddWithValue("@DataKoniec", dataKoniec);
                    if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                        cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                    await using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        porownanie.Add(new PorownanieOkresu
                        {
                            OkresNazwa = $"{_miesiace[dataStart.Month]} {dataStart.Year}",
                            LiczbaZamowien = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                            SumaKg = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1),
                            SumaWartosc = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                            LiczbaOdbiorcow = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                            JestBiezacy = i == 0
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania porównania: {ex.Message}");
            }

            return porownanie;
        }

        /// <summary>
        /// Pobiera dane dzienne (sprzedaż dzień po dniu - ostatnie 14 dni)
        /// </summary>
        public async Task<List<DaneDzienne>> PobierzDaneDzienneAsync(string handlowiec = null, int liczbaDni = 14)
        {
            var dane = new List<DaneDzienne>();
            var dzis = DateTime.Today;
            var dataStart = dzis.AddDays(-liczbaDni + 1);

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        CAST(z.DataOdbioru AS DATE) as Dzien,
                        COUNT(DISTINCT z.ID) as LiczbaZamowien,
                        ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
                        ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as SumaWartosc,
                        COUNT(DISTINCT z.OdbiorcaId) as LiczbaOdbiorcow
                    FROM ZamowieniaMieso z
                    LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                    WHERE z.DataOdbioru >= @DataStart AND z.DataOdbioru <= @DataKoniec
                        AND (z.Anulowane IS NULL OR z.Anulowane = 0)";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND z.Handlowiec = @Handlowiec";

                sql += @"
                    GROUP BY CAST(z.DataOdbioru AS DATE)
                    ORDER BY Dzien";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DataStart", dataStart);
                cmd.Parameters.AddWithValue("@DataKoniec", dzis);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                // Inicjalizuj wszystkie dni (nawet te bez danych)
                var dniDict = new Dictionary<DateTime, DaneDzienne>();
                for (var d = dataStart; d <= dzis; d = d.AddDays(1))
                {
                    dniDict[d] = new DaneDzienne
                    {
                        Data = d,
                        LiczbaZamowien = 0,
                        SumaKg = 0,
                        SumaWartosc = 0,
                        LiczbaOdbiorcow = 0
                    };
                }

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var dzien = reader.GetDateTime(0);
                    if (dniDict.ContainsKey(dzien))
                    {
                        dniDict[dzien].LiczbaZamowien = reader.GetInt32(1);
                        dniDict[dzien].SumaKg = reader.GetDecimal(2);
                        dniDict[dzien].SumaWartosc = reader.GetDecimal(3);
                        dniDict[dzien].LiczbaOdbiorcow = reader.GetInt32(4);
                    }
                }

                dane = dniDict.Values.OrderBy(d => d.Data).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania danych dziennych: {ex.Message}");
            }

            return dane;
        }

        /// <summary>
        /// Pobiera sprzedaż według województw (top 10)
        /// </summary>
        public async Task<List<SprzedazRegionalna>> PobierzSprzedazRegionalnąAsync(string handlowiec = null, int miesiecy = 3)
        {
            var regiony = new List<SprzedazRegionalna>();
            var dataStart = DateTime.Today.AddMonths(-miesiecy);
            decimal sumaCalkowita = 0;

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT TOP 10
                        ISNULL(k.Wojewodztwo, 'Nieznane') as Wojewodztwo,
                        COUNT(DISTINCT z.ID) as LiczbaZamowien,
                        ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
                        ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as SumaWartosc,
                        COUNT(DISTINCT z.OdbiorcaId) as LiczbaOdbiorcow
                    FROM ZamowieniaMieso z
                    LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                    LEFT JOIN Kontrahenci k ON z.OdbiorcaId = k.id
                    WHERE z.DataOdbioru >= @DataStart
                        AND (z.Anulowane IS NULL OR z.Anulowane = 0)";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND z.Handlowiec = @Handlowiec";

                sql += @"
                    GROUP BY ISNULL(k.Wojewodztwo, 'Nieznane')
                    ORDER BY SumaWartosc DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DataStart", dataStart);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                await using var reader = await cmd.ExecuteReaderAsync();
                int pozycja = 1;
                while (await reader.ReadAsync())
                {
                    var region = new SprzedazRegionalna
                    {
                        Pozycja = pozycja++,
                        Wojewodztwo = reader.GetString(0),
                        LiczbaZamowien = reader.GetInt32(1),
                        SumaKg = reader.GetDecimal(2),
                        SumaWartosc = reader.GetDecimal(3),
                        LiczbaOdbiorcow = reader.GetInt32(4)
                    };
                    sumaCalkowita += region.SumaWartosc;
                    regiony.Add(region);
                }

                foreach (var r in regiony)
                {
                    r.UdzialProcent = sumaCalkowita > 0 ? (r.SumaWartosc / sumaCalkowita) * 100 : 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania sprzedaży regionalnej: {ex.Message}");
            }

            return regiony;
        }

        /// <summary>
        /// Pobiera podsumowanie 30-dniowe (jak na dashboardzie Amazon)
        /// </summary>
        public async Task<Podsumowanie30Dni> PobierzPodsumowanie30DniAsync(string handlowiec = null)
        {
            var podsumowanie = new Podsumowanie30Dni();
            var dzis = DateTime.Today;
            var dataStart = dzis.AddDays(-30);

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as SumaSprzedazy,
                        COUNT(DISTINCT z.ID) as LiczbaZamowien,
                        COUNT(DISTINCT CASE WHEN z.Anulowane = 1 THEN z.ID END) as ZwrotyAnulowane,
                        ISNULL(SUM(zp.Ilosc), 0) as SumaKg
                    FROM ZamowieniaMieso z
                    LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                    WHERE z.DataOdbioru >= @DataStart AND z.DataOdbioru <= @DataKoniec";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND z.Handlowiec = @Handlowiec";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DataStart", dataStart);
                cmd.Parameters.AddWithValue("@DataKoniec", dzis);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    podsumowanie.SumaSprzedazy = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
                    podsumowanie.LiczbaZamowien = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    podsumowanie.ZwrotyAnulowane = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    var sumaKg = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);

                    podsumowanie.SredniaWartoscZamowienia = podsumowanie.LiczbaZamowien > 0
                        ? podsumowanie.SumaSprzedazy / podsumowanie.LiczbaZamowien
                        : 0;

                    podsumowanie.SredniaCenaKg = sumaKg > 0
                        ? podsumowanie.SumaSprzedazy / sumaKg
                        : 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania podsumowania 30 dni: {ex.Message}");
            }

            return podsumowanie;
        }

        /// <summary>
        /// Pobiera statystyki typów dostawy (Firma/Własny)
        /// </summary>
        public async Task<List<StatystykiDostawy>> PobierzStatystykiDostawyAsync(string handlowiec = null, int miesiecy = 1)
        {
            var statystyki = new List<StatystykiDostawy>();
            var dataStart = DateTime.Today.AddMonths(-miesiecy);
            int suma = 0;

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        ISNULL(z.TransportStatus, 'Firma') as TypDostawy,
                        COUNT(*) as Liczba
                    FROM ZamowieniaMieso z
                    WHERE z.DataOdbioru >= @DataStart
                        AND (z.Anulowane IS NULL OR z.Anulowane = 0)";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND z.Handlowiec = @Handlowiec";

                sql += " GROUP BY ISNULL(z.TransportStatus, 'Firma')";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DataStart", dataStart);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                var kolory = new Dictionary<string, string>
                {
                    { "Firma", "#3498DB" },
                    { "Wlasny", "#E67E22" },
                    { "Odbiór własny", "#27AE60" },
                    { "Kurier", "#9B59B6" }
                };

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var typ = reader.GetString(0);
                    var liczba = reader.GetInt32(1);
                    suma += liczba;

                    statystyki.Add(new StatystykiDostawy
                    {
                        TypDostawy = typ == "Wlasny" ? "Odbiór własny" : typ == "Firma" ? "Dostawa firmowa" : typ,
                        Liczba = liczba,
                        Kolor = kolory.ContainsKey(typ) ? kolory[typ] : "#95A5A6"
                    });
                }

                foreach (var s in statystyki)
                {
                    s.Procent = suma > 0 ? (decimal)s.Liczba / suma * 100 : 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania statystyk dostawy: {ex.Message}");
            }

            return statystyki;
        }

        /// <summary>
        /// Pobiera średnią wartość zamówień dziennie z porównaniem tygodniowym
        /// </summary>
        public async Task<List<SredniaZamowieniaDziennie>> PobierzSredniaZamowieniaDziennieAsync(string handlowiec = null)
        {
            var dane = new List<SredniaZamowieniaDziennie>();
            var dzis = DateTime.Today;

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                // Pobierz dane dla ostatnich 7 dni i poprzednich 7 dni do porównania
                for (int i = 6; i >= 0; i--)
                {
                    var dzien = dzis.AddDays(-i);
                    var dzienPoprzedni = dzien.AddDays(-7);

                    var sqlTenTydzien = @"
                        SELECT
                            ISNULL(AVG(wartosc_zam), 0) as SredniaTenTydzien
                        FROM (
                            SELECT
                                z.ID,
                                ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as wartosc_zam
                            FROM ZamowieniaMieso z
                            LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                            WHERE CAST(z.DataOdbioru AS DATE) = @Dzien
                                AND (z.Anulowane IS NULL OR z.Anulowane = 0)";

                    if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                        sqlTenTydzien += " AND z.Handlowiec = @Handlowiec";

                    sqlTenTydzien += " GROUP BY z.ID) sub";

                    decimal sredniaTen = 0;
                    await using (var cmd = new SqlCommand(sqlTenTydzien, cn))
                    {
                        cmd.Parameters.AddWithValue("@Dzien", dzien);
                        if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                            cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                            sredniaTen = Convert.ToDecimal(result);
                    }

                    // Poprzedni tydzień
                    decimal sredniaPoprzedni = 0;
                    var sqlPoprzedni = sqlTenTydzien.Replace("@Dzien", "@DzienPoprzedni");
                    await using (var cmd = new SqlCommand(sqlPoprzedni, cn))
                    {
                        cmd.Parameters.AddWithValue("@DzienPoprzedni", dzienPoprzedni);
                        if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                            cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                            sredniaPoprzedni = Convert.ToDecimal(result);
                    }

                    dane.Add(new SredniaZamowieniaDziennie
                    {
                        Data = dzien,
                        SredniaTenTydzien = sredniaTen,
                        SredniaPoprzedniTydzien = sredniaPoprzedni,
                        CelTygodniowy = 200 // Przykładowy cel - można skonfigurować
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania średniej dziennej: {ex.Message}");
            }

            return dane;
        }

        /// <summary>
        /// Pobiera statystyki CRM dla handlowca (wymaga powiązania z operatorem)
        /// </summary>
        public async Task<CRMStatystyki> PobierzCRMStatystykiAsync(string operatorId = null)
        {
            var crm = new CRMStatystyki();
            var dzis = DateTime.Today;
            var poczatekTygodnia = dzis.AddDays(-(int)dzis.DayOfWeek + 1);
            var poczatekMiesiaca = new DateTime(dzis.Year, dzis.Month, 1);

            try
            {
                await using var cn = new SqlConnection(_connectionStringLibraNet);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        ISNULL(SUM(CASE
                            WHEN o.DataNastepnegoKontaktu IS NOT NULL
                                AND CAST(o.DataNastepnegoKontaktu AS DATE) = CAST(GETDATE() AS DATE)
                            THEN 1 ELSE 0 END), 0) as KontaktyDzisiaj,
                        ISNULL(SUM(CASE
                            WHEN o.DataNastepnegoKontaktu IS NOT NULL
                                AND CAST(o.DataNastepnegoKontaktu AS DATE) < CAST(GETDATE() AS DATE)
                            THEN 1 ELSE 0 END), 0) as KontaktyZalegle,
                        ISNULL(SUM(CASE WHEN o.Status = 'Próba kontaktu' THEN 1 ELSE 0 END), 0) as ProbyKontaktu,
                        ISNULL(SUM(CASE WHEN o.Status = 'Nawiązano kontakt' THEN 1 ELSE 0 END), 0) as NawiazaneKontakty,
                        ISNULL(SUM(CASE WHEN o.Status = 'Zgoda na dalszy kontakt' THEN 1 ELSE 0 END), 0) as ZgodyNaKontakt,
                        ISNULL(SUM(CASE WHEN o.Status = 'Do wysłania oferta' THEN 1 ELSE 0 END), 0) as DoWyslaniOferty,
                        COUNT(*) as RazemAktywnych
                    FROM OdbiorcyCRM o
                    LEFT JOIN WlascicieleOdbiorcow w ON o.ID = w.IDOdbiorcy
                    WHERE ISNULL(o.Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)', 'Nie zainteresowany')";

                if (!string.IsNullOrEmpty(operatorId))
                    sql += " AND (w.OperatorID = @OperatorID OR w.OperatorID IS NULL)";

                await using var cmd = new SqlCommand(sql, cn);
                if (!string.IsNullOrEmpty(operatorId))
                    cmd.Parameters.AddWithValue("@OperatorID", operatorId);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    crm.KontaktyDzisiaj = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    crm.KontaktyZalegle = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    crm.ProbyKontaktu = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    crm.NawiazaneKontakty = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    crm.ZgodyNaKontakt = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                    crm.DoWyslaniOferty = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                    crm.RazemAktywnych = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
                }

                // Pobierz priorytetowe branże
                await using (var cmdPrior = new SqlCommand(@"
                    SELECT COUNT(*) FROM OdbiorcyCRM o
                    INNER JOIN PriorytetoweBranzeCRM pb ON o.PKD_Opis = pb.PKD_Opis
                    LEFT JOIN WlascicieleOdbiorcow w ON o.ID = w.IDOdbiorcy
                    WHERE ISNULL(o.Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)', 'Nie zainteresowany')" +
                    (!string.IsNullOrEmpty(operatorId) ? " AND (w.OperatorID = @OperatorID OR w.OperatorID IS NULL)" : ""), cn))
                {
                    if (!string.IsNullOrEmpty(operatorId))
                        cmdPrior.Parameters.AddWithValue("@OperatorID", operatorId);

                    var result = await cmdPrior.ExecuteScalarAsync();
                    crm.PriorytetoweBranze = result != null ? Convert.ToInt32(result) : 0;
                }

                // Notatki i zmiany statusu
                await using (var cmdNotatki = new SqlCommand(@"
                    SELECT
                        ISNULL(SUM(CASE WHEN CAST(DataUtworzenia AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END), 0),
                        ISNULL(SUM(CASE WHEN DataUtworzenia >= @PoczatekTygodnia THEN 1 ELSE 0 END), 0)
                    FROM NotatkiCRM" +
                    (!string.IsNullOrEmpty(operatorId) ? " WHERE KtoDodal = @OperatorID" : ""), cn))
                {
                    cmdNotatki.Parameters.AddWithValue("@PoczatekTygodnia", poczatekTygodnia);
                    if (!string.IsNullOrEmpty(operatorId))
                        cmdNotatki.Parameters.AddWithValue("@OperatorID", operatorId);

                    await using var readerN = await cmdNotatki.ExecuteReaderAsync();
                    if (await readerN.ReadAsync())
                    {
                        crm.NotatekDzisiaj = readerN.IsDBNull(0) ? 0 : readerN.GetInt32(0);
                        crm.NotatekTenTydzien = readerN.IsDBNull(1) ? 0 : readerN.GetInt32(1);
                    }
                }

                await using (var cmdZmiany = new SqlCommand(@"
                    SELECT
                        ISNULL(SUM(CASE WHEN CAST(DataZmiany AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END), 0),
                        ISNULL(SUM(CASE WHEN DataZmiany >= @PoczatekMiesiaca THEN 1 ELSE 0 END), 0)
                    FROM HistoriaZmianCRM
                    WHERE TypZmiany = 'Zmiana statusu'" +
                    (!string.IsNullOrEmpty(operatorId) ? " AND KtoWykonal = @OperatorID" : ""), cn))
                {
                    cmdZmiany.Parameters.AddWithValue("@PoczatekMiesiaca", poczatekMiesiaca);
                    if (!string.IsNullOrEmpty(operatorId))
                        cmdZmiany.Parameters.AddWithValue("@OperatorID", operatorId);

                    await using var readerZ = await cmdZmiany.ExecuteReaderAsync();
                    if (await readerZ.ReadAsync())
                    {
                        crm.ZmianStatusuDzisiaj = readerZ.IsDBNull(0) ? 0 : readerZ.GetInt32(0);
                        crm.ZmianStatusuTenMiesiac = readerZ.IsDBNull(1) ? 0 : readerZ.GetInt32(1);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania CRM: {ex.Message}");
            }

            return crm;
        }

        /// <summary>
        /// Pobiera analizę cen produktu dla handlowca (z Faktur Sprzedaży)
        /// Używa bazy HANDEL i tabel HM.DK, HM.DP, SSCommon.ContractorClassification
        /// </summary>
        public async Task<List<AnalizaCenHandlowca>> PobierzAnalizeCenAsync(string handlowiec = null, int dni = 30)
        {
            var wyniki = new List<AnalizaCenHandlowca>();
            var dzis = DateTime.Today;
            var wczoraj = dzis.AddDays(-1);
            // Pomijamy weekendy dla wczorajszego dnia
            while (wczoraj.DayOfWeek == DayOfWeek.Saturday || wczoraj.DayOfWeek == DayOfWeek.Sunday)
                wczoraj = wczoraj.AddDays(-1);
            var dataOd = dzis.AddDays(-dni);

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
WITH CenyHandlowcow AS (
    SELECT
        ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
        TW.kod AS Produkt,
        DP.cena AS Cena,
        DK.data AS Data
    FROM [HANDEL].[HM].[DK] DK
    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
    INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
    WHERE DK.data >= @DataOd AND DK.data <= @DataDo
      AND TW.katalog IN ('67095', '67153')
      AND ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') NOT IN ('Ogólne')
),
CenyWczoraj AS (
    SELECT Handlowiec, AVG(Cena) AS CenaWczoraj
    FROM CenyHandlowcow WHERE CONVERT(date, Data) = @Wczoraj
    GROUP BY Handlowiec
),
CenyDzisiaj AS (
    SELECT Handlowiec, AVG(Cena) AS CenaDzisiaj
    FROM CenyHandlowcow WHERE CONVERT(date, Data) = @Dzisiaj
    GROUP BY Handlowiec
)
SELECT
    CH.Handlowiec,
    CAST(AVG(CH.Cena) AS DECIMAL(18,2)) AS SredniaCena,
    CAST(CW.CenaWczoraj AS DECIMAL(18,2)) AS CenaWczoraj,
    CAST(CD.CenaDzisiaj AS DECIMAL(18,2)) AS CenaDzisiaj,
    CAST(MIN(CH.Cena) AS DECIMAL(18,2)) AS MinCena,
    CAST(MAX(CH.Cena) AS DECIMAL(18,2)) AS MaxCena,
    COUNT(*) AS LiczbaTransakcji
FROM CenyHandlowcow CH
LEFT JOIN CenyWczoraj CW ON CH.Handlowiec = CW.Handlowiec
LEFT JOIN CenyDzisiaj CD ON CH.Handlowiec = CD.Handlowiec
WHERE 1=1";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND CH.Handlowiec = @Handlowiec";

                sql += @"
GROUP BY CH.Handlowiec, CW.CenaWczoraj, CD.CenaDzisiaj
ORDER BY SredniaCena DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DataOd", dataOd);
                cmd.Parameters.AddWithValue("@DataDo", dzis);
                cmd.Parameters.AddWithValue("@Wczoraj", wczoraj);
                cmd.Parameters.AddWithValue("@Dzisiaj", dzis);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var analiza = new AnalizaCenHandlowca
                    {
                        Handlowiec = reader.GetString(0),
                        SredniaCena = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1),
                        CenaWczoraj = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                        CenaDzisiaj = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                        MinCena = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                        MaxCena = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                        LiczbaTransakcji = reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
                    };

                    // Oblicz zmianę
                    if (analiza.CenaWczoraj > 0 && analiza.CenaDzisiaj > 0)
                    {
                        analiza.ZmianaZl = analiza.CenaDzisiaj - analiza.CenaWczoraj;
                        analiza.ZmianaProcent = (analiza.ZmianaZl / analiza.CenaWczoraj) * 100;
                    }

                    wyniki.Add(analiza);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania analizy cen: {ex.Message}");
            }

            return wyniki;
        }

        /// <summary>
        /// Pobiera udział handlowców w sprzedaży (z Faktur)
        /// </summary>
        public async Task<List<UdzialHandlowcaWSprzedazy>> PobierzUdzialHandlowcowAsync(int rok, int miesiac)
        {
            var wyniki = new List<UdzialHandlowcaWSprzedazy>();
            decimal sumaCalkowita = 0;

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
SELECT
    ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
    CAST(SUM(DP.ilosc) AS DECIMAL(18,2)) AS SumaKg,
    CAST(SUM(DP.ilosc * DP.cena) AS DECIMAL(18,2)) AS SumaWartosc,
    COUNT(DISTINCT DK.id) AS LiczbaFaktur,
    COUNT(DISTINCT DK.khid) AS LiczbaOdbiorcow
FROM [HANDEL].[HM].[DK] DK
INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
  AND TW.katalog IN ('67095', '67153')
  AND ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') NOT IN ('Ogólne')
GROUP BY ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany')
ORDER BY SumaWartosc DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);

                await using var reader = await cmd.ExecuteReaderAsync();
                int pozycja = 1;
                while (await reader.ReadAsync())
                {
                    var udzial = new UdzialHandlowcaWSprzedazy
                    {
                        Pozycja = pozycja++,
                        Handlowiec = reader.GetString(0),
                        SumaKg = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1),
                        SumaWartosc = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                        LiczbaFaktur = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                        LiczbaOdbiorcow = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                    };
                    sumaCalkowita += udzial.SumaWartosc;
                    wyniki.Add(udzial);
                }

                foreach (var u in wyniki)
                {
                    u.UdzialProcent = sumaCalkowita > 0 ? (u.SumaWartosc / sumaCalkowita) * 100 : 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania udziału handlowców: {ex.Message}");
            }

            return wyniki;
        }

        /// <summary>
        /// Pobiera zamówienia na dziś i jutro (z Zamówień Klientów)
        /// </summary>
        public async Task<ZamowieniaNaDzien> PobierzZamowieniaNaDzienAsync(string handlowiec = null)
        {
            var wynik = new ZamowieniaNaDzien();
            var dzis = DateTime.Today;
            var jutro = dzis.AddDays(1);

            try
            {
                await using var cn = new SqlConnection(_connectionStringLibraNet);
                await cn.OpenAsync();

                var sql = @"
SELECT
    SUM(CASE WHEN z.DataOdbioru = @Dzis THEN 1 ELSE 0 END) as ZamDzis,
    SUM(CASE WHEN z.DataOdbioru = @Dzis THEN ISNULL(zp.Ilosc, 0) ELSE 0 END) as KgDzis,
    SUM(CASE WHEN z.DataOdbioru = @Dzis THEN ISNULL(zp.Ilosc * zp.Cena, 0) ELSE 0 END) as WartoscDzis,
    SUM(CASE WHEN z.DataOdbioru = @Jutro THEN 1 ELSE 0 END) as ZamJutro,
    SUM(CASE WHEN z.DataOdbioru = @Jutro THEN ISNULL(zp.Ilosc, 0) ELSE 0 END) as KgJutro,
    SUM(CASE WHEN z.DataOdbioru = @Jutro THEN ISNULL(zp.Ilosc * zp.Cena, 0) ELSE 0 END) as WartoscJutro
FROM ZamowieniaMieso z
LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
WHERE z.DataOdbioru IN (@Dzis, @Jutro)
  AND (z.Anulowane IS NULL OR z.Anulowane = 0)";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND z.Handlowiec = @Handlowiec";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Dzis", dzis);
                cmd.Parameters.AddWithValue("@Jutro", jutro);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    wynik.LiczbaZamowienDzis = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    wynik.SumaKgDzis = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                    wynik.SumaWartoscDzis = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                    wynik.LiczbaZamowienJutro = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    wynik.SumaKgJutro = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4);
                    wynik.SumaWartoscJutro = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania zamówień na dzień: {ex.Message}");
            }

            return wynik;
        }

        /// <summary>
        /// Pobiera top produkty handlowca (z Faktur)
        /// </summary>
        public async Task<List<TopProduktHandlowca>> PobierzTopProduktyAsync(string handlowiec = null, int top = 5, int dni = 30)
        {
            var wyniki = new List<TopProduktHandlowca>();
            var dataOd = DateTime.Today.AddDays(-dni);

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = $@"
SELECT TOP {top}
    TW.kod AS NazwaProduktu,
    CAST(SUM(DP.ilosc) AS DECIMAL(18,2)) AS SumaKg,
    CAST(SUM(DP.ilosc * DP.cena) AS DECIMAL(18,2)) AS SumaWartosc,
    CAST(AVG(DP.cena) AS DECIMAL(18,2)) AS SredniaCena,
    COUNT(*) AS LiczbaTransakcji
FROM [HANDEL].[HM].[DK] DK
INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
WHERE DK.data >= @DataOd
  AND TW.katalog IN ('67095', '67153')";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND WYM.CDim_Handlowiec_Val = @Handlowiec";

                sql += @"
GROUP BY TW.kod
ORDER BY SumaWartosc DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DataOd", dataOd);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                await using var reader = await cmd.ExecuteReaderAsync();
                int pozycja = 1;
                while (await reader.ReadAsync())
                {
                    wyniki.Add(new TopProduktHandlowca
                    {
                        Pozycja = pozycja++,
                        NazwaProduktu = reader.GetString(0),
                        SumaKg = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1),
                        SumaWartosc = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                        SredniaCena = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                        LiczbaTransakcji = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania top produktów: {ex.Message}");
            }

            return wyniki;
        }
    }
}
