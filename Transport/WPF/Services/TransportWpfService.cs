// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Services/TransportWpfService.cs
// ════════════════════════════════════════════════════════════════════════════
// Warstwa danych dla sandbox WPF transportu. Opakowuje TransportRepozytorium
// (TransportPL) i dokłada zapytania cross-DB do LibraNet (wolne zamówienia) +
// HANDEL (nazwy klientów). Łączenie po stronie .NET (brak cross-DB JOIN do .112).
//
// Synchronizacja statusów (SyncStatusyKursuAsync) powiela poprawiony algorytm z
// transport-editor.cs (commity 6a326bf + e59126c): status 'Przypisany' tylko dla
// zamówień faktycznie w kursie + auto-healing sierot (TransportKursId bez ładunku).
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Transport;
using Kalendarz1.Transport.Repozytorium;
using Kalendarz1.Transport.WPF.Models;
using Kalendarz1.Services;

namespace Kalendarz1.Transport.WPF.Services
{
    public class TransportWpfService
    {
        // Hardcoded conn-stringi — spójne z transport-editor.cs (legacy pattern modułu).
        public const string ConnTransport =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public TransportRepozytorium Repo { get; }

        // Cache nazw klientów (HANDEL = najdroższy, cross-DB). Pytamy raz na sesję per KlientId.
        private readonly Dictionary<int, (string Nazwa, string Handlowiec, string Adres)> _nazwaCache = new();

        public TransportWpfService()
        {
            Repo = new TransportRepozytorium(ConnTransport, ConnLibra);
        }

        // ════════════════════════════════════════════════════════════════════
        // WOLNE ZAMÓWIENIA (LibraNet) + nazwy (HANDEL)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wolne zamówienia na dany dzień: TransportStatus 'Oczekuje'/NULL i bez
        /// przypisanego kursu (TransportKursId IS NULL). Okno dat: [data, data+2].
        /// poUboju=true ⇒ filtr po DataUboju, inaczej po DataZamowienia (jak WinForms).
        /// </summary>
        public async Task<List<WolneZamowienieWpf>> LoadWolneZamowieniaAsync(DateTime data, bool poUboju)
        {
            var wynik = new List<WolneZamowienieWpf>();

            await using (var cn = new SqlConnection(ConnLibra))
            {
                await cn.OpenAsync();

                string kol = poUboju ? "zm.DataUboju" : "zm.DataZamowienia";
                var sql = $@"
                    SELECT
                        zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.Status,
                        ISNULL(zm.LiczbaPalet, 0)      AS LiczbaPalet,
                        ISNULL(zm.LiczbaPojemnikow, 0) AS LiczbaPojemnikow,
                        ISNULL(zm.TrybE2, 0)           AS TrybE2,
                        ISNULL(zm.TransportStatus, 'Oczekuje') AS TransportStatus,
                        zm.DataUboju
                    FROM dbo.ZamowieniaMieso zm
                    WHERE {kol} >= @DataOd AND {kol} <= @DataDo
                      AND ISNULL(zm.Status, 'Nowe') NOT IN ('Anulowane')
                      AND ISNULL(zm.TransportStatus, 'Oczekuje') = 'Oczekuje'
                      AND zm.TransportKursId IS NULL
                    ORDER BY {kol}, zm.DataPrzyjazdu";

                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DataOd", data.Date);
                cmd.Parameters.AddWithValue("@DataDo", data.Date.AddDays(2));
                cmd.CommandTimeout = 60;

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    wynik.Add(new WolneZamowienieWpf
                    {
                        ZamowienieId = r.GetInt32(0),
                        KlientId = r.GetInt32(1),
                        DataPrzyjazdu = r.GetDateTime(2),
                        Palety = r.GetDecimal(4),
                        Pojemniki = r.GetInt32(5),
                        TrybE2 = r.GetBoolean(6),
                        TransportStatus = r.GetString(7),
                        DataUboju = r.IsDBNull(8) ? null : r.GetDateTime(8)
                    });
                }
            }

            await UzupelnijNazwyKlientowAsync(wynik);
            return wynik;
        }

        private async Task UzupelnijNazwyKlientowAsync(List<WolneZamowienieWpf> zamowienia)
        {
            if (zamowienia.Count == 0) return;
            var nazwy = await PobierzNazwyKlientowAsync(zamowienia.Select(z => z.KlientId));
            foreach (var z in zamowienia)
            {
                if (nazwy.TryGetValue(z.KlientId, out var info))
                {
                    z.KlientNazwa = info.Nazwa;
                    z.Handlowiec = info.Handlowiec;
                    z.Adres = info.Adres;
                }
                else
                {
                    z.KlientNazwa = $"Klient {z.KlientId}";
                }
            }
        }

        /// <summary>HANDEL: KlientId → (nazwa skrócona, handlowiec, adres). Łączone w .NET.</summary>
        private async Task<Dictionary<int, (string Nazwa, string Handlowiec, string Adres)>>
            PobierzNazwyKlientowAsync(IEnumerable<int> klientIds)
        {
            var ids = klientIds.Distinct().Where(i => i > 0).ToList();
            var dict = new Dictionary<int, (string, string, string)>();
            if (ids.Count == 0) return dict;

            // z cache + lista brakujących do dociągnięcia z HANDEL
            var missing = new List<int>();
            foreach (var id in ids)
            {
                if (_nazwaCache.TryGetValue(id, out var c)) dict[id] = c;
                else missing.Add(id);
            }
            if (missing.Count == 0) return dict;

            await using var cn = new SqlConnection(ConnHandel);
            await cn.OpenAsync();

            var sql = $@"
                SELECT
                    c.Id,
                    ISNULL(c.Shortcut, 'KH ' + CAST(c.Id AS VARCHAR(10))) AS Nazwa,
                    ISNULL(wym.CDim_Handlowiec_Val, '') AS Handlowiec,
                    ISNULL(poa.Postcode, '') + ' ' + ISNULL(poa.Street, '') AS Adres
                FROM SSCommon.STContractors c
                LEFT JOIN SSCommon.ContractorClassification wym ON c.Id = wym.ElementId
                LEFT JOIN SSCommon.STPostOfficeAddresses poa ON poa.ContactGuid = c.ContactGuid
                    AND poa.AddressName = N'adres domyślny'
                WHERE c.Id IN ({string.Join(",", missing)})";

            using var cmd = new SqlCommand(sql, cn);
            cmd.CommandTimeout = 60;
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var t = (r.GetString(1), r.GetString(2), r.GetString(3).Trim());
                int id = r.GetInt32(0);
                _nazwaCache[id] = t;
                dict[id] = t;
            }
            return dict;
        }

        /// <summary>
        /// Rozwiązuje dane klientów dla istniejących ładunków ZAM_* (edycja kursu):
        /// LibraNet (zamId → KlientId/awizacja/poj.) + HANDEL (KlientId → nazwa).
        /// </summary>
        public async Task<Dictionary<int, ZamowienieNazwaInfo>> ResolveNazwyAsync(IEnumerable<int> zamIds)
        {
            var result = new Dictionary<int, ZamowienieNazwaInfo>();
            var ids = zamIds.Distinct().ToList();
            if (ids.Count == 0) return result;

            await using (var cn = new SqlConnection(ConnLibra))
            {
                await cn.OpenAsync();
                var sql = $@"
                    SELECT Id, KlientId, DataPrzyjazdu, ISNULL(LiczbaPojemnikow, 0)
                    FROM dbo.ZamowieniaMieso
                    WHERE Id IN ({string.Join(",", ids)})";
                using var cmd = new SqlCommand(sql, cn);
                cmd.CommandTimeout = 60;
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var zid = r.GetInt32(0);
                    result[zid] = new ZamowienieNazwaInfo
                    {
                        ZamowienieId = zid,
                        KlientId = r.GetInt32(1),
                        Awizacja = r.IsDBNull(2) ? null : r.GetDateTime(2),
                        Pojemniki = r.GetInt32(3)
                    };
                }
            }

            var nazwy = await PobierzNazwyKlientowAsync(result.Values.Select(v => v.KlientId));
            foreach (var info in result.Values)
            {
                if (nazwy.TryGetValue(info.KlientId, out var n))
                {
                    info.Nazwa = n.Nazwa;
                    info.Handlowiec = n.Handlowiec;
                    info.Adres = n.Adres;
                }
                else info.Nazwa = $"Klient {info.KlientId}";
            }
            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        // SYNCHRONIZACJA STATUSÓW (spójna z naprawą sierot)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Po zapisie kursu: ustaw 'Przypisany' + TransportKursId dla zamówień faktycznie
        /// w kursie (zamIdyWKursie), oraz AUTO-HEALING: zwolnij każde zamówienie, które
        /// w LibraNet wskazuje na ten kurs (TransportKursId = kursId), a NIE jest w zbiorze
        /// (= sierota bez ładunku). Gwarantuje spójność TransportStatus ↔ Ladunek.
        /// </summary>
        public async Task SyncStatusyKursuAsync(long kursId, ISet<int> zamIdyWKursie, string uzytkownik)
        {
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();

            // 1) Ustaw 'Przypisany' dla zamówień w kursie
            foreach (var zamId in zamIdyWKursie)
            {
                using var cmd = new SqlCommand(
                    @"UPDATE dbo.ZamowieniaMieso
                      SET TransportStatus = 'Przypisany', TransportKursId = @KursId
                      WHERE Id = @ZamId
                        AND (ISNULL(TransportStatus,'') <> 'Przypisany'
                             OR TransportKursId IS NULL OR TransportKursId <> @KursId)", cn);
                cmd.Parameters.AddWithValue("@KursId", kursId);
                cmd.Parameters.AddWithValue("@ZamId", zamId);
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows > 0)
                    await LogSafe(zamId, uzytkownik, "Transport WPF - przypisanie do kursu",
                        "Oczekuje na przypisanie", $"Kurs #{kursId}");
            }

            // 2) Auto-healing: zwolnij sieroty wskazujące na ten kurs bez ładunku
            var sieroty = new List<int>();
            using (var find = new SqlCommand(
                @"SELECT Id FROM dbo.ZamowieniaMieso WHERE TransportKursId = @KursId", cn))
            {
                find.Parameters.AddWithValue("@KursId", kursId);
                using var r = await find.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    int id = r.GetInt32(0);
                    if (!zamIdyWKursie.Contains(id)) sieroty.Add(id);
                }
            }
            foreach (var zamId in sieroty)
            {
                using var cmd = new SqlCommand(
                    @"UPDATE dbo.ZamowieniaMieso
                      SET TransportStatus = 'Oczekuje', TransportKursId = NULL
                      WHERE Id = @ZamId", cn);
                cmd.Parameters.AddWithValue("@ZamId", zamId);
                await cmd.ExecuteNonQueryAsync();
                await LogSafe(zamId, uzytkownik, "Transport WPF - auto-zwolnienie sieroty",
                    $"Kurs #{kursId}", "Oczekuje na przypisanie");
            }
        }

        /// <summary>
        /// Odbiór własny klienta: zamówienie znika z puli transportowej
        /// (TransportStatus='Wlasny', TransportKursId=NULL). Jak ZmienNaWlasnyOdbior w WinForms.
        /// </summary>
        public async Task WlasnyOdbiorAsync(int zamId, string uzytkownik)
        {
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(
                @"UPDATE dbo.ZamowieniaMieso SET TransportStatus = 'Wlasny', TransportKursId = NULL
                  WHERE Id = @Id", cn);
            cmd.Parameters.AddWithValue("@Id", zamId);
            await cmd.ExecuteNonQueryAsync();
            await LogSafe(zamId, uzytkownik, "Transport WPF - odbiór własny", "Oczekuje", "Własny");
        }

        private static async Task LogSafe(int zamId, string user, string operacja, string przed, string po)
        {
            try
            {
                await HistoriaZmianService.LogujEdycje(zamId, user ?? "system", App.UserFullName,
                    operacja, przed, po, "Zmiana z nowego edytora WPF");
            }
            catch { /* log nie może blokować zapisu */ }
        }
    }
}
