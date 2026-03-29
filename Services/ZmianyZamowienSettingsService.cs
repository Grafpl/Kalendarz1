using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Services
{
    public class ZmianyZamowienSettings
    {
        public TimeSpan GodzinaOdKtorejPowiadamiac { get; set; } = new TimeSpan(11, 0, 0);
        public TimeSpan? GodzinaBlokadyEdycji { get; set; }
        public bool CzyBlokowacEdycjePoGodzinie { get; set; }
        /// <summary>
        /// CSV nazw modułów z accessMap (np. "PanelMagazyniera,ProdukcjaPodglad,UstalanieTranportu").
        /// Powiadomienie pojawi się użytkownikom, którzy mają dostęp do co najmniej jednego z tych kafelków.
        /// Puste = powiadomienie dla wszystkich.
        /// </summary>
        public string KafelkiDocelowe { get; set; } = "";
        public string RodzajPowiadomienia { get; set; } = "MessageBox";
        public decimal MinimalnaZmianaKgDoPowiadomienia { get; set; }
        public bool CzyWymagacKomentarzaPrzyZmianie { get; set; }
        public bool CzyLogowacZmianyDoHistorii { get; set; } = true;
        public string DniTygodniaAktywne { get; set; } = "1,2,3,4,5";
        public DateTime? ModifiedAt { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class WylaczonyUzytkownik
    {
        public int Id { get; set; }
        public string UserID { get; set; } = "";
        public string UserName { get; set; } = "";
        public bool CzyZwolnionyZPowiadomien { get; set; } = true;
        public TimeSpan? IndywidualnaGodzina { get; set; }
        public string? Powod { get; set; }
        public string? DodanoPrzez { get; set; }
        public DateTime DodanoData { get; set; }
    }

    public static class ZmianyZamowienSettingsService
    {
        private static readonly string _connectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private static bool _tableInitialized = false;
        private static ZmianyZamowienSettings? _cachedSettings;
        private static DateTime _cacheExpiry = DateTime.MinValue;
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

        // ═══════════════════════════════════════════════════════════════
        // accessMap — kopia z Menu.cs (pozycja bitu → nazwa modułu)
        // ═══════════════════════════════════════════════════════════════
        private static readonly Dictionary<int, string> _accessMap = new()
        {
            [0] = "DaneHodowcy", [1] = "ZakupPaszyPisklak", [2] = "ZamowieniaPisklak",
            [3] = "WstawieniaKurczaka", [4] = "SpecyfikacjeWysylek", [5] = "SprzedazZamowienia",
            [6] = "SprzedazPlan", [7] = "AvilogPlan", [8] = "HistoriaKierunkow",
            [9] = "ZmianyUHodowcow", [10] = "Cenniki", [11] = "CRM",
            [12] = "TerminyDostawyZywca", [13] = "PrognozyUboju", [14] = "KalendarzZywiec",
            [15] = "ProdukcjaPodglad", [16] = "UstalanieTranportu", [17] = "SaldaOdbiorcowOpak",
            [18] = "PodsumowanieSaldOpak", [19] = "MagazynPodglad", [20] = "KalkulacjaKrojenia",
            [21] = "ZamowieniaZakupu", [22] = "NotatkiZeSpotkan", [23] = "PlanTygodniowy",
            [24] = "LiczenieMagazynu", [25] = "PanelMagazyniera", [26] = "KartotekaOdbiorcow",
            [27] = "AnalizaWydajnosci", [28] = "RezerwacjaKlas", [29] = "DashboardWyczerpalnosci",
            [30] = "ListaOfert", [31] = "DashboardOfert", [32] = "PanelReklamacji",
            [33] = "ReklamacjeJakosc", [34] = "RaportyHodowcow", [35] = "AdminPermissions",
            [36] = "AnalizaPrzychodu", [37] = "DashboardHandlowca", [38] = "PanelFaktur",
            [39] = "PanelPortiera", [40] = "PanelLekarza", [41] = "KontrolaGodzin",
            [42] = "CentrumSpotkan", [43] = "PanelPaniJola", [44] = "KomunikatorFirmowy",
            [45] = "RozliczeniaAvilog", [46] = "DashboardPrzychodu", [47] = "MapaKlientow",
            [48] = "WnioskiUrlopowe", [49] = "DashboardZamowien", [50] = "QuizDrobiarstwo",
            [51] = "PulpitZarzadu", [52] = "CallReminders", [53] = "PorannyBriefing",
            [54] = "ProductImages", [55] = "PozyskiwanieHodowcow", [56] = "KartotekaTowarow",
            [57] = "Flota", [58] = "ListaPartii", [59] = "TransportZmiany",
            [60] = "OpakowaniaWinForm", [61] = "UstawieniaZmianZamowien"
        };

        /// <summary>
        /// Zwraca pełną accessMap do użytku w UI (okno admina — lista kafelków).
        /// </summary>
        public static Dictionary<int, string> GetAccessMap() => _accessMap;

        // ═══════════════════════════════════════════════════════════════
        // CACHE
        // ═══════════════════════════════════════════════════════════════
        public static ZmianyZamowienSettings GetSettingsCached()
        {
            if (_cachedSettings != null && DateTime.Now < _cacheExpiry)
                return _cachedSettings;

            try
            {
                using var cn = new SqlConnection(_connectionString);
                cn.Open();

                if (!_tableInitialized)
                {
                    EnsureTablesSync(cn);
                    _tableInitialized = true;
                }

                using var cmd = new SqlCommand("SELECT TOP 1 * FROM UstawieniaZmianZamowien", cn);
                using var rdr = cmd.ExecuteReader();

                if (rdr.Read())
                {
                    _cachedSettings = new ZmianyZamowienSettings
                    {
                        GodzinaOdKtorejPowiadamiac = (TimeSpan)rdr["GodzinaOdKtorejPowiadamiac"],
                        GodzinaBlokadyEdycji = rdr["GodzinaBlokadyEdycji"] == DBNull.Value ? null : (TimeSpan)rdr["GodzinaBlokadyEdycji"],
                        CzyBlokowacEdycjePoGodzinie = (bool)rdr["CzyBlokowacEdycjePoGodzinie"],
                        KafelkiDocelowe = rdr["KafelkiDocelowe"]?.ToString() ?? "",
                        RodzajPowiadomienia = rdr["RodzajPowiadomienia"]?.ToString() ?? "MessageBox",
                        MinimalnaZmianaKgDoPowiadomienia = rdr["MinimalnaZmianaKgDoPowiadomienia"] == DBNull.Value ? 0 : (decimal)rdr["MinimalnaZmianaKgDoPowiadomienia"],
                        CzyWymagacKomentarzaPrzyZmianie = (bool)rdr["CzyWymagacKomentarzaPrzyZmianie"],
                        CzyLogowacZmianyDoHistorii = (bool)rdr["CzyLogowacZmianyDoHistorii"],
                        DniTygodniaAktywne = rdr["DniTygodniaAktywne"]?.ToString() ?? "1,2,3,4,5",
                        ModifiedAt = rdr["ModifiedAt"] == DBNull.Value ? null : (DateTime)rdr["ModifiedAt"],
                        ModifiedBy = rdr["ModifiedBy"]?.ToString()
                    };
                }
                else
                {
                    _cachedSettings = new ZmianyZamowienSettings();
                }

                _cacheExpiry = DateTime.Now.Add(_cacheDuration);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZmianySettings] Błąd odczytu: {ex.Message}");
                if (_cachedSettings == null)
                    _cachedSettings = new ZmianyZamowienSettings();
            }

            return _cachedSettings;
        }

        public static void InvalidateCache()
        {
            _cacheExpiry = DateTime.MinValue;
            _cachedSettings = null;
        }

        // ═══════════════════════════════════════════════════════════════
        // SAVE
        // ═══════════════════════════════════════════════════════════════
        public static async Task SaveSettingsAsync(ZmianyZamowienSettings settings, string modifiedBy)
        {
            await using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync();

            if (!_tableInitialized)
            {
                EnsureTablesSync(cn);
                _tableInitialized = true;
            }

            var sql = @"
                IF EXISTS (SELECT 1 FROM UstawieniaZmianZamowien)
                    UPDATE UstawieniaZmianZamowien SET
                        GodzinaOdKtorejPowiadamiac = @Godzina,
                        GodzinaBlokadyEdycji = @GodzinaBlokady,
                        CzyBlokowacEdycjePoGodzinie = @Blokada,
                        KafelkiDocelowe = @Kafelki,
                        RodzajPowiadomienia = @Rodzaj,
                        MinimalnaZmianaKgDoPowiadomienia = @MinKg,
                        CzyWymagacKomentarzaPrzyZmianie = @Komentarz,
                        CzyLogowacZmianyDoHistorii = @Logowanie,
                        DniTygodniaAktywne = @Dni,
                        ModifiedAt = GETDATE(),
                        ModifiedBy = @ModifiedBy
                ELSE
                    INSERT INTO UstawieniaZmianZamowien
                        (GodzinaOdKtorejPowiadamiac, GodzinaBlokadyEdycji, CzyBlokowacEdycjePoGodzinie,
                         KafelkiDocelowe, RodzajPowiadomienia, MinimalnaZmianaKgDoPowiadomienia,
                         CzyWymagacKomentarzaPrzyZmianie, CzyLogowacZmianyDoHistorii,
                         DniTygodniaAktywne, ModifiedBy)
                    VALUES
                        (@Godzina, @GodzinaBlokady, @Blokada,
                         @Kafelki, @Rodzaj, @MinKg,
                         @Komentarz, @Logowanie,
                         @Dni, @ModifiedBy)";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Godzina", settings.GodzinaOdKtorejPowiadamiac);
            cmd.Parameters.AddWithValue("@GodzinaBlokady", (object?)settings.GodzinaBlokadyEdycji ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Blokada", settings.CzyBlokowacEdycjePoGodzinie);
            cmd.Parameters.AddWithValue("@Kafelki", settings.KafelkiDocelowe ?? "");
            cmd.Parameters.AddWithValue("@Rodzaj", settings.RodzajPowiadomienia ?? "MessageBox");
            cmd.Parameters.AddWithValue("@MinKg", settings.MinimalnaZmianaKgDoPowiadomienia);
            cmd.Parameters.AddWithValue("@Komentarz", settings.CzyWymagacKomentarzaPrzyZmianie);
            cmd.Parameters.AddWithValue("@Logowanie", settings.CzyLogowacZmianyDoHistorii);
            cmd.Parameters.AddWithValue("@Dni", settings.DniTygodniaAktywne ?? "1,2,3,4,5");
            cmd.Parameters.AddWithValue("@ModifiedBy", modifiedBy ?? "SYSTEM");

            await cmd.ExecuteNonQueryAsync();
            InvalidateCache();
        }

        // ═══════════════════════════════════════════════════════════════
        // EXEMPTIONS
        // ═══════════════════════════════════════════════════════════════
        public static async Task<List<WylaczonyUzytkownik>> GetExemptionsAsync()
        {
            var list = new List<WylaczonyUzytkownik>();

            await using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync();

            if (!_tableInitialized)
            {
                EnsureTablesSync(cn);
                _tableInitialized = true;
            }

            await using var cmd = new SqlCommand("SELECT * FROM UstawieniaZmianZamowien_Wylaczenia ORDER BY UserName", cn);
            await using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                list.Add(new WylaczonyUzytkownik
                {
                    Id = (int)rdr["Id"],
                    UserID = rdr["UserID"]?.ToString() ?? "",
                    UserName = rdr["UserName"]?.ToString() ?? "",
                    CzyZwolnionyZPowiadomien = (bool)rdr["CzyZwolnionyZPowiadomien"],
                    IndywidualnaGodzina = rdr["IndywidualnaGodzina"] == DBNull.Value ? null : (TimeSpan)rdr["IndywidualnaGodzina"],
                    Powod = rdr["Powod"]?.ToString(),
                    DodanoPrzez = rdr["DodanoPrzez"]?.ToString(),
                    DodanoData = (DateTime)rdr["DodanoData"]
                });
            }

            return list;
        }

        public static async Task AddExemptionAsync(string userId, string userName, bool zwolnionyZPowiadomien, TimeSpan? indywidualnaGodzina, string? powod, string dodanoPrzez)
        {
            await using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync();

            if (!_tableInitialized)
            {
                EnsureTablesSync(cn);
                _tableInitialized = true;
            }

            var sql = @"
                IF NOT EXISTS (SELECT 1 FROM UstawieniaZmianZamowien_Wylaczenia WHERE UserID = @UserID)
                    INSERT INTO UstawieniaZmianZamowien_Wylaczenia
                        (UserID, UserName, CzyZwolnionyZPowiadomien, IndywidualnaGodzina, Powod, DodanoPrzez)
                    VALUES
                        (@UserID, @UserName, @Zwolniony, @Godzina, @Powod, @DodanoPrzez)
                ELSE
                    UPDATE UstawieniaZmianZamowien_Wylaczenia SET
                        UserName = @UserName, CzyZwolnionyZPowiadomien = @Zwolniony,
                        IndywidualnaGodzina = @Godzina, Powod = @Powod, DodanoPrzez = @DodanoPrzez
                    WHERE UserID = @UserID";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@UserID", userId);
            cmd.Parameters.AddWithValue("@UserName", userName);
            cmd.Parameters.AddWithValue("@Zwolniony", zwolnionyZPowiadomien);
            cmd.Parameters.AddWithValue("@Godzina", (object?)indywidualnaGodzina ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Powod", (object?)powod ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DodanoPrzez", dodanoPrzez ?? "SYSTEM");

            await cmd.ExecuteNonQueryAsync();
        }

        public static async Task RemoveExemptionAsync(int id)
        {
            await using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync();

            await using var cmd = new SqlCommand("DELETE FROM UstawieniaZmianZamowien_Wylaczenia WHERE Id = @Id", cn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        // LOGIKA DECYZYJNA
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Czy pokazać powiadomienie temu userowi?
        /// Sprawdza: dzień tygodnia → wyłączenie → godzinę → uprawnienia do wybranych kafelków.
        /// </summary>
        public static bool ShouldShowNotification(string userId)
        {
            var settings = GetSettingsCached();

            // 1. Dzień tygodnia
            var activeDays = ParseActiveDays(settings.DniTygodniaAktywne);
            int today = (int)DateTime.Now.DayOfWeek;
            if (!activeDays.Contains(today))
                return false;

            // 2. Wyłączenie indywidualne
            if (IsUserExemptFromNotifications(userId, out var indywidualnaGodzina))
                return false;

            // 3. Godzina
            var godzinaDoSprawdzenia = indywidualnaGodzina ?? settings.GodzinaOdKtorejPowiadamiac;
            if (DateTime.Now.TimeOfDay < godzinaDoSprawdzenia)
                return false;

            // 4. Kafelki docelowe — sprawdź czy user ma dostęp do przynajmniej jednego
            if (!string.IsNullOrWhiteSpace(settings.KafelkiDocelowe))
            {
                if (!UserHasAnyTileAccess(userId, settings.KafelkiDocelowe))
                    return false;
            }
            // Puste KafelkiDocelowe = powiadom wszystkich (bez filtra)

            return true;
        }

        /// <summary>
        /// Czy zablokować edycję temu userowi?
        /// </summary>
        public static bool ShouldBlockEdit(string userId)
        {
            var settings = GetSettingsCached();

            if (!settings.CzyBlokowacEdycjePoGodzinie || settings.GodzinaBlokadyEdycji == null)
                return false;

            var activeDays = ParseActiveDays(settings.DniTygodniaAktywne);
            int today = (int)DateTime.Now.DayOfWeek;
            if (!activeDays.Contains(today))
                return false;

            if (IsUserExemptFromNotifications(userId, out _))
                return false;

            return DateTime.Now.TimeOfDay >= settings.GodzinaBlokadyEdycji.Value;
        }

        /// <summary>
        /// Sprawdza czy user ma w swoim stringu Access bit=1 na pozycji odpowiadającej
        /// co najmniej jednemu z wybranych kafelków.
        /// </summary>
        private static bool UserHasAnyTileAccess(string userId, string kafelkiCsv)
        {
            try
            {
                // Pobierz Access string usera z tabeli operators
                string? accessString = null;
                using (var cn = new SqlConnection(_connectionString))
                {
                    cn.Open();
                    using var cmd = new SqlCommand("SELECT Access FROM operators WHERE ID = @userId", cn);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    var result = cmd.ExecuteScalar();
                    accessString = result?.ToString();
                }

                if (string.IsNullOrEmpty(accessString))
                    return false;

                // Parsuj wybrane kafelki → znajdź ich pozycje bitowe
                var wybraneModuly = kafelkiCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var pozycjeBitowe = new HashSet<int>();
                foreach (var kv in _accessMap)
                {
                    if (wybraneModuly.Contains(kv.Value, StringComparer.OrdinalIgnoreCase))
                        pozycjeBitowe.Add(kv.Key);
                }

                // Sprawdź czy user ma bit=1 na którejkolwiek z tych pozycji
                foreach (int pos in pozycjeBitowe)
                {
                    if (pos < accessString.Length && accessString[pos] == '1')
                        return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZmianySettings] Tile access check error: {ex.Message}");
            }

            return false;
        }

        private static bool IsUserExemptFromNotifications(string userId, out TimeSpan? indywidualnaGodzina)
        {
            indywidualnaGodzina = null;

            try
            {
                using var cn = new SqlConnection(_connectionString);
                cn.Open();

                using var cmd = new SqlCommand(
                    "SELECT CzyZwolnionyZPowiadomien, IndywidualnaGodzina FROM UstawieniaZmianZamowien_Wylaczenia WHERE UserID = @UserID", cn);
                cmd.Parameters.AddWithValue("@UserID", userId);

                using var rdr = cmd.ExecuteReader();
                if (rdr.Read())
                {
                    indywidualnaGodzina = rdr["IndywidualnaGodzina"] == DBNull.Value ? null : (TimeSpan)rdr["IndywidualnaGodzina"];
                    return (bool)rdr["CzyZwolnionyZPowiadomien"];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZmianySettings] Exemption check error: {ex.Message}");
            }

            return false;
        }

        private static HashSet<int> ParseActiveDays(string dniStr)
        {
            var result = new HashSet<int>();
            if (string.IsNullOrEmpty(dniStr)) return result;

            foreach (var part in dniStr.Split(','))
            {
                if (int.TryParse(part.Trim(), out int day))
                    result.Add(day);
            }
            return result;
        }

        public static async Task<List<(string Id, string Name)>> GetOperatorsAsync()
        {
            var list = new List<(string, string)>();

            await using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync();

            await using var cmd = new SqlCommand("SELECT DISTINCT Uzytkownik, UzytkownikNazwa FROM HistoriaZmianZamowien WHERE Uzytkownik IS NOT NULL ORDER BY UzytkownikNazwa", cn);
            await using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                var id = rdr["Uzytkownik"]?.ToString() ?? "";
                var name = rdr["UzytkownikNazwa"]?.ToString() ?? id;
                if (!string.IsNullOrEmpty(id))
                    list.Add((id, name));
            }

            return list;
        }

        // ═══════════════════════════════════════════════════════════════
        // AUTO-CREATE TABLES
        // ═══════════════════════════════════════════════════════════════
        private static void EnsureTablesSync(SqlConnection cn)
        {
            var sql = @"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UstawieniaZmianZamowien]') AND type = N'U')
                BEGIN
                    CREATE TABLE [dbo].[UstawieniaZmianZamowien](
                        [GodzinaOdKtorejPowiadamiac] TIME NOT NULL DEFAULT '11:00',
                        [GodzinaBlokadyEdycji] TIME NULL,
                        [CzyBlokowacEdycjePoGodzinie] BIT NOT NULL DEFAULT 0,
                        [KafelkiDocelowe] NVARCHAR(MAX) NOT NULL DEFAULT '',
                        [RodzajPowiadomienia] VARCHAR(20) NOT NULL DEFAULT 'MessageBox',
                        [MinimalnaZmianaKgDoPowiadomienia] DECIMAL(10,2) NOT NULL DEFAULT 0,
                        [CzyWymagacKomentarzaPrzyZmianie] BIT NOT NULL DEFAULT 0,
                        [CzyLogowacZmianyDoHistorii] BIT NOT NULL DEFAULT 1,
                        [DniTygodniaAktywne] VARCHAR(20) NOT NULL DEFAULT '1,2,3,4,5',
                        [ModifiedAt] DATETIME NOT NULL DEFAULT GETDATE(),
                        [ModifiedBy] NVARCHAR(100) NULL
                    );

                    INSERT INTO [dbo].[UstawieniaZmianZamowien] DEFAULT VALUES;
                END
                ELSE
                BEGIN
                    -- Migracja: dodaj nową kolumnę jeśli nie istnieje
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UstawieniaZmianZamowien') AND name = 'KafelkiDocelowe')
                        ALTER TABLE [dbo].[UstawieniaZmianZamowien] ADD [KafelkiDocelowe] NVARCHAR(MAX) NOT NULL DEFAULT '';
                END

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UstawieniaZmianZamowien_Wylaczenia]') AND type = N'U')
                BEGIN
                    CREATE TABLE [dbo].[UstawieniaZmianZamowien_Wylaczenia](
                        [Id] INT IDENTITY(1,1) PRIMARY KEY,
                        [UserID] NVARCHAR(50) NOT NULL,
                        [UserName] NVARCHAR(200) NOT NULL,
                        [CzyZwolnionyZPowiadomien] BIT NOT NULL DEFAULT 1,
                        [IndywidualnaGodzina] TIME NULL,
                        [Powod] NVARCHAR(500) NULL,
                        [DodanoPrzez] NVARCHAR(100) NOT NULL DEFAULT 'SYSTEM',
                        [DodanoData] DATETIME NOT NULL DEFAULT GETDATE()
                    );

                    CREATE UNIQUE INDEX [IX_Wylaczenia_UserID]
                    ON [dbo].[UstawieniaZmianZamowien_Wylaczenia] ([UserID]);
                END";

            using var cmd = new SqlCommand(sql, cn);
            cmd.ExecuteNonQuery();
        }
    }
}
