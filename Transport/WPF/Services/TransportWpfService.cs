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

        // Mapowanie handlowiec (nazwa) → userId (login) z LibraNet.UserHandlowcy — do avatara ze zdjęciem.
        private Dictionary<string, string>? _handlowiecMap;

        public TransportWpfService()
        {
            Repo = new TransportRepozytorium(ConnTransport, ConnLibra);
        }

        // ════════════════════════════════════════════════════════════════════
        // WOLNE ZAMÓWIENIA (LibraNet) + nazwy (HANDEL)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wolne zamówienia — DOKŁADNIE jak stary panel (LoadWolneZamowieniaAsync):
        /// jeden dzień (po dacie uboju domyślnie), TransportStatus NOT IN ('Przypisany','Wlasny'),
        /// TransportKursID IS NULL, bez 'Anulowane'. poUboju=true ⇒ filtr po DataUboju,
        /// inaczej po DataPrzyjazdu (odbiór). Grupowanie po dniu odbioru robi UI.
        /// </summary>
        public async Task<List<WolneZamowienieWpf>> LoadWolneZamowieniaAsync(DateTime data, bool poUboju)
        {
            var wynik = new List<WolneZamowienieWpf>();

            await using (var cn = new SqlConnection(ConnLibra))
            {
                await cn.OpenAsync();

                string kol = poUboju ? "zm.DataUboju" : "zm.DataPrzyjazdu";
                var sql = $@"
                    SELECT
                        zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.Status,
                        ISNULL(zm.LiczbaPalet, 0)      AS LiczbaPalet,
                        ISNULL(zm.LiczbaPojemnikow, 0) AS LiczbaPojemnikow,
                        ISNULL(zm.TrybE2, 0)           AS TrybE2,
                        ISNULL(zm.TransportStatus, 'Oczekuje') AS TransportStatus,
                        zm.DataUboju
                    FROM dbo.ZamowieniaMieso zm
                    WHERE CAST({kol} AS DATE) = @Data
                      AND ISNULL(zm.Status, 'Nowe') NOT IN ('Anulowane')
                      AND ISNULL(zm.TransportStatus, 'Oczekuje') NOT IN ('Przypisany', 'Wlasny')
                      AND zm.TransportKursID IS NULL
                    ORDER BY zm.DataPrzyjazdu";

                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", data.Date);
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
            await EnsureHandlowiecMapAsync();
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
                z.HandlowiecId = HandlowiecUserId(z.Handlowiec);
            }
        }

        /// <summary>Ładuje raz mapowanie handlowiec→userId z LibraNet.UserHandlowcy (do avatarów handlowców).</summary>
        public async Task EnsureHandlowiecMapAsync()
        {
            if (_handlowiecMap != null) return;
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                using var cmd = new SqlCommand("SELECT HandlowiecName, UserID FROM UserHandlowcy", cn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    if (!r.IsDBNull(0) && !r.IsDBNull(1))
                        map[r.GetString(0)] = r.GetString(1);
            }
            catch { /* brak mapowania → inicjały */ }
            _handlowiecMap = map;
        }

        /// <summary>userId handlowca po nazwie (null gdy brak mapowania → avatar pokaże inicjały).</summary>
        public string? HandlowiecUserId(string? handlowiecName)
        {
            if (string.IsNullOrWhiteSpace(handlowiecName) || _handlowiecMap == null) return null;
            return _handlowiecMap.TryGetValue(handlowiecName, out var uid) ? uid : null;
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
                    SELECT zm.Id, zm.KlientId, zm.DataPrzyjazdu, ISNULL(zm.LiczbaPojemnikow, 0),
                           (SELECT ISNULL(SUM(t.Ilosc), 0) FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = zm.Id) AS Kg
                    FROM dbo.ZamowieniaMieso zm
                    WHERE zm.Id IN ({string.Join(",", ids)})";
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
                        Pojemniki = r.GetInt32(3),
                        IloscKg = r.IsDBNull(4) ? 0 : r.GetDecimal(4)
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
        // SYSTEM AKCEPTACJI ZMIAN — proxy do TransportZmianyService (static).
        // Reuse całej detekcji/zapisu starego systemu, tylko nowe UI w WPF.
        // Cache pendingów 30 s, żeby przełączanie Lista↔Timeline nie powielało query.
        // Mapa ZamId → zbiór typów zmian: licznik per (zam × typ), nie sam zam —
        // dlatego badge 🔔 zgadza się z liczbą widocznych (scalonych) kart.
        // Filtrujemy "ZmianaStatusu" (wewnętrzny typ, nie edycja handlowca).
        // ════════════════════════════════════════════════════════════════════
        private static Dictionary<int, HashSet<string>>? _pendingMap;
        private static DateTime _pendingCacheUtc = DateTime.MinValue;
        private static readonly TimeSpan PendingTtl = TimeSpan.FromSeconds(30);

        /// <summary>Mapa ZamowienieId → zbiór typów oczekujących zmian (z filtrem ZmianaStatusu, cache 30 s).</summary>
        public async Task<Dictionary<int, HashSet<string>>> PobierzOczekujaceMapaAsync()
        {
            if (_pendingMap != null && DateTime.UtcNow - _pendingCacheUtc < PendingTtl)
                return _pendingMap.ToDictionary(p => p.Key, p => new HashSet<string>(p.Value));
            try
            {
                var lista = await TransportZmianyService.GetPendingAsync();
                var mapa = lista
                    .Where(z => z.TypZmiany != "ZmianaStatusu")
                    .GroupBy(z => z.ZamowienieId)
                    .ToDictionary(g => g.Key, g => g.Select(z => z.TypZmiany).ToHashSet());
                _pendingMap = mapa;
                _pendingCacheUtc = DateTime.UtcNow;
                return mapa.ToDictionary(p => p.Key, p => new HashSet<string>(p.Value));
            }
            catch { return _pendingMap != null ? _pendingMap.ToDictionary(p => p.Key, p => new HashSet<string>(p.Value)) : new(); }
        }

        public void InwalidujCacheZmian()
        {
            _pendingMap = null;
            _pendingCacheUtc = DateTime.MinValue;
        }

        /// <summary>Pełna lista oczekujących zmian dla konkretnego kursu (przez Ladunek.KodKlienta='ZAM_xxx').</summary>
        public async Task<List<TransportZmiana>> PobierzZmianyDlaKursuAsync(long kursId)
        {
            try { return await TransportZmianyService.GetPendingForKursAsync(kursId); }
            catch { return new List<TransportZmiana>(); }
        }

        /// <summary>
        /// Akceptuje GRUPĘ zmian (np. 7 kolejnych edycji tego samego pola).
        /// AcceptAsync per każde Id, potem (jeśli ZmianaPojemnikow) synchronizuje
        /// Ladunek.PojemnikiE2 raz na koniec z aktualną wartością LibraNet — żeby
        /// akceptacja faktycznie propagowała, nie tylko „odhaczała".
        /// </summary>
        public async Task AkceptujGrupeIPrzeliczAsync(IList<int> ids, long? kursId, int? zamowienieId, string? typ, string user, string? komentarz = null)
        {
            foreach (var id in ids)
                await TransportZmianyService.AcceptAsync(id, user, komentarz);
            InwalidujCacheZmian();

            if (kursId.HasValue && zamowienieId.HasValue && typ == "ZmianaPojemnikow")
            {
                int? aktualna = null;
                try
                {
                    await using var cnL = new SqlConnection(ConnLibra);
                    await cnL.OpenAsync();
                    using var cmdL = new SqlCommand("SELECT LiczbaPojemnikow FROM dbo.ZamowieniaMieso WHERE Id=@z", cnL);
                    cmdL.Parameters.AddWithValue("@z", zamowienieId.Value);
                    var v = await cmdL.ExecuteScalarAsync();
                    if (v != null && v != DBNull.Value) aktualna = Convert.ToInt32(v);
                }
                catch { }
                if (aktualna.HasValue)
                {
                    try
                    {
                        await using var cn = new SqlConnection(ConnTransport);
                        await cn.OpenAsync();
                        using var cmd = new SqlCommand(
                            "UPDATE dbo.Ladunek SET PojemnikiE2=@p WHERE KursID=@k AND KodKlienta=@kk", cn);
                        cmd.Parameters.AddWithValue("@p", aktualna.Value);
                        cmd.Parameters.AddWithValue("@k", kursId.Value);
                        cmd.Parameters.AddWithValue("@kk", $"ZAM_{zamowienieId.Value}");
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch { }
                }
            }
        }

        public async Task OdrzucGrupeAsync(IList<int> ids, string user, string? komentarz = null)
        {
            foreach (var id in ids)
                await TransportZmianyService.RejectAsync(id, user, komentarz);
            InwalidujCacheZmian();
        }

        /// <summary>Hurtowo: akceptuje wszystkie zmiany dla kursu + synchronizuje Ladunek.PojemnikiE2.</summary>
        public async Task AkceptujWszystkieDlaKursuAsync(long kursId, string user)
        {
            await TransportZmianyService.AcceptChangesForKursAsync(kursId, user);
            InwalidujCacheZmian();
        }

        // Nazwy użytkowników (LibraNet.operators) — do podpisu „Utworzył" + avatar.
        private readonly Dictionary<string, string> _userCache = new();
        public async Task<Dictionary<string, string>> PobierzNazwyUzytkownikowAsync(IEnumerable<string> userIds)
        {
            var ids = userIds.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
            var result = new Dictionary<string, string>();
            var missing = new List<string>();
            foreach (var id in ids)
            {
                if (_userCache.TryGetValue(id, out var n)) result[id] = n;
                else missing.Add(id);
            }
            if (missing.Count == 0) return result;

            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                var pars = missing.Select((id, i) => $"@u{i}").ToList();
                var sql = $"SELECT ID, ISNULL(Name, ID) FROM operators WHERE ID IN ({string.Join(",", pars)})";
                using var cmd = new SqlCommand(sql, cn);
                for (int i = 0; i < missing.Count; i++) cmd.Parameters.AddWithValue($"@u{i}", missing[i]);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var id = r.GetString(0); var name = r.GetString(1);
                    _userCache[id] = name; result[id] = name;
                }
            }
            catch { /* fallback niżej */ }
            foreach (var id in missing)
                if (!result.ContainsKey(id)) { _userCache[id] = id; result[id] = id; }
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

        // ════════════════════════════════════════════════════════════════════
        // TIMELINE (Faza T)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Kierowcy aktywni + ich kursy dnia, pogrupowane, z detekcją konfliktów. Pseudo-wiersz „brak kierowcy" na górze.</summary>
        public async Task<List<KierowcaWierszTimeline>> LoadKierowcyZKursamiAsync(DateTime data, bool ukryjWolnych = false)
        {
            var kierowcy = await Repo.PobierzKierowcowAsync(true);
            var kursy = await Repo.PobierzKursyPoDacieAsync(data);
            var ladunki = await Repo.PobierzLadunkiDlaKursowAsync(kursy.Select(k => k.KursID));
            var pendingMap = await PobierzOczekujaceMapaAsync();   // ZamId → typy (filtr ZmianaStatusu, cache 30 s)

            var bary = new List<KursBar>();
            foreach (var k in kursy)
            {
                var wyj = k.GodzWyjazdu ?? new TimeSpan(6, 0, 0);
                var pow = k.GodzPowrotu ?? wyj.Add(TimeSpan.FromHours(8));
                if (pow <= wyj) pow = wyj.Add(TimeSpan.FromHours(1));
                bary.Add(new KursBar
                {
                    KursID = k.KursID,
                    Trasa = k.Trasa ?? "—",
                    Wyjazd = wyj,
                    Powrot = pow,
                    KierowcaID = k.KierowcaID,
                    PojazdID = k.PojazdID,
                    KierowcaNazwa = k.KierowcaNazwa ?? "",
                    PojazdRej = k.PojazdRejestracja ?? "",
                    LiczbaLadunkow = ladunki.TryGetValue(k.KursID, out var l) ? l.Count : 0,
                    Proc = k.ProcNominal,
                    Pal = k.PaletyNominal,
                    Poj = k.SumaE2,
                    UtworzylName = k.Utworzyl ?? "",
                    UtworzylData = k.UtworzonoUTC.ToLocalTime().ToString("dd.MM HH:mm"),
                    ZmienilName = k.Zmienil ?? "",
                    ZmienilData = k.ZmienionoUTC.HasValue ? k.ZmienionoUTC.Value.ToLocalTime().ToString("dd.MM HH:mm") : "",
                    BrakGodzin = !k.GodzWyjazdu.HasValue,
                    LiczbaZmianOczekujacych = ladunki.TryGetValue(k.KursID, out var lk) ? lk.Sum(x =>
                        x.KodKlienta != null && x.KodKlienta.StartsWith("ZAM_")
                        && int.TryParse(x.KodKlienta.Substring(4), out var zid)
                        && pendingMap.TryGetValue(zid, out var typy) ? typy.Count : 0) : 0
                });
            }

            var wiersze = new Dictionary<int, KierowcaWierszTimeline>();
            foreach (var ki in kierowcy)
                wiersze[ki.KierowcaID] = new KierowcaWierszTimeline { KierowcaID = ki.KierowcaID, Imie = ki.Imie, Nazwisko = ki.Nazwisko };

            var pseudo = new KierowcaWierszTimeline { KierowcaID = 0, BrakKierowcy = true };

            foreach (var b in bary)
            {
                if (!b.KierowcaID.HasValue || b.KierowcaID.Value == 0)
                {
                    pseudo.Kursy.Add(b);
                }
                else
                {
                    if (!wiersze.TryGetValue(b.KierowcaID.Value, out var w))
                    {
                        var parts = (b.KierowcaNazwa ?? "").Split(new[] { ' ' }, 2);
                        w = new KierowcaWierszTimeline
                        {
                            KierowcaID = b.KierowcaID.Value,
                            Imie = parts.Length > 0 ? parts[0] : "",
                            Nazwisko = parts.Length > 1 ? parts[1] : ""
                        };
                        wiersze[b.KierowcaID.Value] = w;
                    }
                    w.Kursy.Add(b);
                }
            }

            foreach (var w in wiersze.Values)
            {
                var p = w.Kursy.FirstOrDefault(x => !string.IsNullOrEmpty(x.PojazdRej));
                if (p != null) w.PojazdRej = p.PojazdRej;
                WykryjKonflikty(w);
            }
            WykryjKonflikty(pseudo);

            IEnumerable<KierowcaWierszTimeline> lista = wiersze.Values;
            if (ukryjWolnych) lista = lista.Where(w => w.Kursy.Count > 0);

            var posort = lista
                .OrderByDescending(w => w.Kursy.Count > 0)
                .ThenBy(w => w.Kursy.Count > 0 ? w.Kursy.Min(k => k.Wyjazd) : TimeSpan.MaxValue)
                .ThenBy(w => w.Nazwisko)
                .ToList();

            var wynik = new List<KierowcaWierszTimeline>();
            if (pseudo.Kursy.Count > 0) wynik.Add(pseudo);
            wynik.AddRange(posort);
            return wynik;
        }

        private static void WykryjKonflikty(KierowcaWierszTimeline w)
        {
            var ks = w.Kursy;
            for (int i = 0; i < ks.Count; i++)
                for (int j = i + 1; j < ks.Count; j++)
                    if (ks[i].Wyjazd < ks[j].Powrot && ks[j].Wyjazd < ks[i].Powrot)
                    {
                        ks[i].Konflikt = true;
                        ks[j].Konflikt = true;
                    }
        }

        /// <summary>Przeniesienie kursu (zmiana kierowcy i/lub godzin) — COALESCE, tylko podane pola.</summary>
        public async Task<bool> ZapiszKursPrzeniesionyAsync(long kursId, int? nowyKierowcaId, TimeSpan? wyj, TimeSpan? pow, string user)
        {
            try
            {
                await using var cn = new SqlConnection(ConnTransport);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(
                    @"UPDATE dbo.Kurs SET
                        KierowcaID = COALESCE(@Kier, KierowcaID),
                        GodzWyjazdu = COALESCE(@Wyj, GodzWyjazdu),
                        GodzPowrotu = COALESCE(@Pow, GodzPowrotu),
                        ZmienionoUTC = SYSUTCDATETIME(), Zmienil = @User
                      WHERE KursID = @Id", cn) { CommandTimeout = 15 };
                cmd.Parameters.AddWithValue("@Id", kursId);
                cmd.Parameters.AddWithValue("@Kier", (object?)nowyKierowcaId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Wyj", (object?)wyj ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Pow", (object?)pow ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@User", user ?? "system");
                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransportWPF] ZapiszKursPrzeniesiony: {ex.Message}");
                return false;
            }
        }

        /// <summary>Centralne przypisanie wolnych zamówień do kursu (ładunki + spójny status). Reuse z timeline i listy.</summary>
        public async Task DodajWolneDoKursuAsync(long kursId, IEnumerable<WolneZamowienieWpf> zamowienia, string user)
        {
            foreach (var z in zamowienia)
                await Repo.DodajLadunekAsync(new Ladunek
                {
                    KursID = kursId,
                    KodKlienta = z.KodKlienta,
                    PojemnikiE2 = z.Pojemniki,
                    TrybE2 = z.TrybE2
                });

            var lad = await Repo.PobierzLadunkiAsync(kursId);
            var zamIds = lad.Where(l => l.KodKlienta != null && l.KodKlienta.StartsWith("ZAM_")
                                        && int.TryParse(l.KodKlienta.Substring(4), out _))
                            .Select(l => int.Parse(l.KodKlienta!.Substring(4)))
                            .ToHashSet();
            await SyncStatusyKursuAsync(kursId, zamIds, user);
        }

        /// <summary>Tworzy nowy kurs (preselekcja kierowcy + sugerowana godzina) i dokłada zamówienia. Zwraca KursID.</summary>
        public async Task<long> UtworzKursIDodajAsync(DateTime data, int? kierowcaId, TimeSpan wyjazd,
            IEnumerable<WolneZamowienieWpf> zamowienia, string user)
        {
            var kurs = new Kurs
            {
                DataKursu = data.Date,
                KierowcaID = kierowcaId,
                GodzWyjazdu = wyjazd,
                GodzPowrotu = wyjazd.Add(TimeSpan.FromHours(8)),
                Status = "Planowany",
                PlanE2NaPalete = 36
            };
            var kursId = await Repo.DodajKursAsync(kurs, user);
            await DodajWolneDoKursuAsync(kursId, zamowienia, user);
            return kursId;
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
