using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;
using Kalendarz1.Admin.Models;

namespace Kalendarz1.Admin.Services
{
    // Warstwa biznesowa panelu uprawnień. Wyciągnięta z AdminPermissionsForm.cs (WinForms)
    // żeby nowe WPF okno (AdminPermissionsWindow) i stara forma mogły dzielić logikę.
    public class AdminPermissionsService
    {
        private readonly string _libraNetConn = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // ─────────────────────────────────────────────────────────────────
        // USERS
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<AdminUserInfo>> LoadUsersAsync()
        {
            var users = new List<AdminUserInfo>();
            try
            {
                await using var conn = new SqlConnection(_libraNetConn);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT ID, Name FROM operators ORDER BY Name", conn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    users.Add(new AdminUserInfo
                    {
                        ID = rd["ID"]?.ToString() ?? "",
                        Name = rd["Name"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AdminService.LoadUsers] {ex.Message}");
            }
            return users;
        }

        // Jeden SELECT zwracający last successful login per user — pozwala uniknąć N+1.
        public async Task<Dictionary<string, DateTime>> LoadLastLoginsAsync()
        {
            var result = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            try
            {
                const string sql = @"
                    SELECT UserId, MAX(AttemptedAt) AS LastLogin
                    FROM dbo.LoginAttempts
                    WHERE Success = 1 AND UserId IS NOT NULL
                    GROUP BY UserId";

                await using var conn = new SqlConnection(_libraNetConn);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var uid = rd.IsDBNull(0) ? "" : rd.GetString(0);
                    var dt = rd.IsDBNull(1) ? (DateTime?)null : rd.GetDateTime(1);
                    if (!string.IsNullOrEmpty(uid) && dt.HasValue) result[uid] = dt.Value;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AdminService.LoadLastLogins] {ex.Message}");
            }
            return result;
        }

        // Wczytuje avatar użytkownika z pliku sieciowego/lokalnego.
        // Bardzo szybkie: BitmapImage z DecodePixelWidth=64 (mniejszy obraz w pamięci niż 1080p),
        // CacheOption=OnLoad (synchroniczne wczytanie, zwalnia handle pliku natychmiast).
        // Wywoływane z Task.Run żeby nie blokować wątku UI.
        public static BitmapImage? LoadAvatarFast(string userId)
        {
            try
            {
                var path = UserAvatarManager.GetAvatarFilePathOrNull(userId);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

                // Wczytaj do MemoryStream żeby nie trzymać handle do pliku sieciowego.
                byte[] bytes = File.ReadAllBytes(path);
                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.DecodePixelWidth = 64; // dopasowane do 32-64px wyświetlania
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze(); // niezbędne — pozwoli używać między wątkami
                return bmp;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AdminService.LoadAvatar] {userId}: {ex.Message}");
                return null;
            }
        }

        public async Task<string> GetAccessStringAsync(string userId)
        {
            try
            {
                await using var conn = new SqlConnection(_libraNetConn);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT Access FROM operators WHERE ID = @userId", conn);
                cmd.Parameters.AddWithValue("@userId", userId);
                var result = await cmd.ExecuteScalarAsync();
                return result?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AdminService.GetAccess] {ex.Message}");
                return "";
            }
        }

        public async Task<bool> SaveAccessAsync(string userId, IEnumerable<AdminModuleInfo> modules, string changedBy = "", string source = "manual")
        {
            try
            {
                var map = GetAccessMap();
                int len = map.Count > 0 ? map.Keys.Max() + 1 : 65;
                char[] arr = new char[len];
                for (int i = 0; i < len; i++) arr[i] = '0';

                foreach (var module in modules)
                {
                    if (!module.HasAccess) continue;
                    var pos = map.FirstOrDefault(kv => kv.Value == module.Key).Key;
                    if (pos >= 0 && pos < len) arr[pos] = '1';
                }
                string newAccess = new string(arr);

                // Audit: pobierz stary access ZANIM zaktualizujemy — żeby zalogować zmianę.
                string oldAccess = await GetAccessStringAsync(userId);

                await using var conn = new SqlConnection(_libraNetConn);
                await conn.OpenAsync();

                // SET Access + SessionInvalidatedAt (re-login forced) w jednym query.
                await using var cmd = new SqlCommand(
                    "UPDATE operators SET Access = @access, SessionInvalidatedAt = GETDATE() WHERE ID = @userId",
                    conn);
                cmd.Parameters.AddWithValue("@access", newAccess);
                cmd.Parameters.AddWithValue("@userId", userId);
                await cmd.ExecuteNonQueryAsync();

                // Zapis audit log
                if (oldAccess != newAccess)
                {
                    await LogAuditAsync(userId, oldAccess, newAccess, changedBy, source);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AdminService.SaveAccess] {ex.Message}");
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // AUDIT LOG zmian uprawnień (BRC 3.3, IFS 2.2)
        // ─────────────────────────────────────────────────────────────────

        public async Task EnsureAuditTableAsync()
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.PermissionAudit') AND type = 'U')
                BEGIN
                    CREATE TABLE dbo.PermissionAudit (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        UserId VARCHAR(20) NOT NULL,
                        OldAccess VARCHAR(100),
                        NewAccess VARCHAR(100),
                        DiffAdded NVARCHAR(MAX),     -- CSV: pozycje dodane
                        DiffRemoved NVARCHAR(MAX),   -- CSV: pozycje odebrane
                        ChangedBy VARCHAR(50),
                        ChangedAt DATETIME DEFAULT GETDATE(),
                        Source NVARCHAR(50)          -- 'manual' / 'template:<name>' / 'clone' / 'bulk'
                    );
                    CREATE NONCLUSTERED INDEX IX_PermissionAudit_User_Date
                        ON dbo.PermissionAudit (UserId, ChangedAt DESC);
                    CREATE NONCLUSTERED INDEX IX_PermissionAudit_Date
                        ON dbo.PermissionAudit (ChangedAt DESC);
                END;

                -- Kolumna SessionInvalidatedAt w operators (do force re-login)
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'SessionInvalidatedAt' AND Object_ID = OBJECT_ID('dbo.operators'))
                BEGIN
                    ALTER TABLE dbo.operators ADD SessionInvalidatedAt DATETIME NULL;
                END";
            try
            {
                await using var conn = new SqlConnection(_libraNetConn);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AdminService.EnsureAudit] {ex.Message}");
            }
        }

        private async Task LogAuditAsync(string userId, string oldAccess, string newAccess, string changedBy, string source)
        {
            try
            {
                // Wylicz diff — które pozycje dodano/odebrano
                int max = Math.Max(oldAccess.Length, newAccess.Length);
                var addedPositions = new List<int>();
                var removedPositions = new List<int>();
                for (int i = 0; i < max; i++)
                {
                    char oldC = i < oldAccess.Length ? oldAccess[i] : '0';
                    char newC = i < newAccess.Length ? newAccess[i] : '0';
                    if (oldC == '0' && newC == '1') addedPositions.Add(i);
                    else if (oldC == '1' && newC == '0') removedPositions.Add(i);
                }

                var map = GetAccessMap();
                string DescribePositions(IEnumerable<int> positions)
                    => string.Join(",", positions.Select(p => map.TryGetValue(p, out var name) ? name : $"#{p}"));

                const string sql = @"
                    INSERT INTO dbo.PermissionAudit (UserId, OldAccess, NewAccess, DiffAdded, DiffRemoved, ChangedBy, Source)
                    VALUES (@uid, @oldA, @newA, @added, @removed, @by, @src)";
                await using var conn = new SqlConnection(_libraNetConn);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@uid", userId ?? "");
                cmd.Parameters.AddWithValue("@oldA", oldAccess ?? "");
                cmd.Parameters.AddWithValue("@newA", newAccess ?? "");
                cmd.Parameters.AddWithValue("@added", DescribePositions(addedPositions));
                cmd.Parameters.AddWithValue("@removed", DescribePositions(removedPositions));
                cmd.Parameters.AddWithValue("@by", changedBy ?? "");
                cmd.Parameters.AddWithValue("@src", source ?? "manual");
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AdminService.LogAudit] {ex.Message}");
            }
        }

        // Klonuje accessString z jednego usera do drugiego. Zapisuje też audit (Source = "clone:<sourceId>").
        public async Task<bool> CloneAccessAsync(string targetUserId, string sourceUserId, string changedBy)
        {
            try
            {
                string sourceAccess = await GetAccessStringAsync(sourceUserId);
                if (string.IsNullOrEmpty(sourceAccess)) return false;

                string oldAccess = await GetAccessStringAsync(targetUserId);

                await using var conn = new SqlConnection(_libraNetConn);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(
                    "UPDATE operators SET Access = @access, SessionInvalidatedAt = GETDATE() WHERE ID = @userId",
                    conn);
                cmd.Parameters.AddWithValue("@access", sourceAccess);
                cmd.Parameters.AddWithValue("@userId", targetUserId);
                int affected = await cmd.ExecuteNonQueryAsync();

                if (affected > 0 && oldAccess != sourceAccess)
                {
                    await LogAuditAsync(targetUserId, oldAccess, sourceAccess, changedBy, $"clone:{sourceUserId}");
                }
                return affected > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AdminService.CloneAccess] {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(string userId)
        {
            try
            {
                await using var conn = new SqlConnection(_libraNetConn);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand("DELETE FROM operators WHERE ID = @userId", conn);
                cmd.Parameters.AddWithValue("@userId", userId);
                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AdminService.DeleteUser] {ex.Message}");
                return false;
            }
        }

        public async Task EnsureAccessColumnSizeAsync()
        {
            try
            {
                await using var conn = new SqlConnection(_libraNetConn);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand("ALTER TABLE operators ALTER COLUMN Access VARCHAR(100)", conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AdminService.EnsureColSize] {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // MODELE BIZNESOWE (zsynchronizowane z AdminPermissionsForm.cs i Menu.cs)
        // ─────────────────────────────────────────────────────────────────

        public List<AdminModuleInfo> GetModulesList()
        {
            return new List<AdminModuleInfo>
            {
                // ─── Zaopatrzenie i Zakupy ───
                new() { Key = "DaneHodowcy",          DisplayName = "Baza Hodowców",         Description = "Kompletna kartoteka dostawców żywca",        Category = "Zaopatrzenie i Zakupy", Icon = "🧑‍🌾" },
                new() { Key = "WstawieniaHodowcy",    DisplayName = "Cykle Wstawień",        Description = "Rejestracja cykli hodowlanych piskląt",      Category = "Zaopatrzenie i Zakupy", Icon = "🐣" },
                new() { Key = "TerminyDostawyZywca",  DisplayName = "Kalendarz Dostaw Żywca",Description = "Planowanie terminów dostaw żywca",           Category = "Zaopatrzenie i Zakupy", Icon = "📅" },
                new() { Key = "PlachtyAviloga",       DisplayName = "Matryca Transportu",    Description = "Planowanie tras transportu żywca z SMS",     Category = "Zaopatrzenie i Zakupy", Icon = "🚛" },
                new() { Key = "PanelPortiera",        DisplayName = "Panel Portiera",        Description = "Przyjęcie i ważenie żywca na bramie",        Category = "Zaopatrzenie i Zakupy", Icon = "🚧" },
                new() { Key = "PanelLekarza",         DisplayName = "Panel Lekarza",         Description = "Badanie weterynaryjne zwierząt",             Category = "Zaopatrzenie i Zakupy", Icon = "⚕️" },
                new() { Key = "Specyfikacje",         DisplayName = "Specyfikacja Surowca",  Description = "Parametry jakościowe surowca",               Category = "Zaopatrzenie i Zakupy", Icon = "📋" },
                new() { Key = "DokumentyZakupu",      DisplayName = "Dokumenty i Umowy",     Description = "Archiwum umów i certyfikatów",               Category = "Zaopatrzenie i Zakupy", Icon = "📑" },
                new() { Key = "PlatnosciHodowcy",     DisplayName = "Rozliczenia z Hodowcami",Description = "Płatności dla dostawców żywca",             Category = "Zaopatrzenie i Zakupy", Icon = "💵" },
                new() { Key = "ZakupPaszyPisklak",    DisplayName = "Zakup Paszy i Piskląt", Description = "Ewidencja zakupów pasz i piskląt",           Category = "Zaopatrzenie i Zakupy", Icon = "🌾" },
                new() { Key = "RozliczeniaAvilog",    DisplayName = "Rozliczenia Avilog",    Description = "Rozliczenia transportu Avilog",              Category = "Zaopatrzenie i Zakupy", Icon = "🧮" },
                new() { Key = "RaportyHodowcow",      DisplayName = "Statystyki Hodowców",   Description = "Raporty współpracy z hodowcami",             Category = "Zaopatrzenie i Zakupy", Icon = "📊" },
                new() { Key = "PozyskiwanieHodowcow", DisplayName = "Pozyskiwanie Hodowców", Description = "CRM do pozyskiwania nowych hodowców drobiu", Category = "Zaopatrzenie i Zakupy", Icon = "🐔" },

                // ─── Produkcja i Magazyn ───
                new() { Key = "ProdukcjaPodglad",     DisplayName = "Panel Produkcji",       Description = "Monitoring procesu uboju i krojenia",        Category = "Produkcja i Magazyn", Icon = "🏭" },
                new() { Key = "KalkulacjaKrojenia",   DisplayName = "Kalkulacja Rozbioru",   Description = "Planowanie krojenia tuszek",                 Category = "Produkcja i Magazyn", Icon = "✂️" },
                new() { Key = "PrzychodMrozni",       DisplayName = "Magazyn Mroźni",        Description = "Stany magazynowe produktów mrożonych",       Category = "Produkcja i Magazyn", Icon = "❄️" },
                new() { Key = "LiczenieMagazynu",     DisplayName = "Inwentaryzacja Magazynu",Description = "Rejestracja stanów magazynowych",           Category = "Produkcja i Magazyn", Icon = "📦" },
                new() { Key = "PanelMagazyniera",     DisplayName = "Panel Magazyniera",     Description = "Zarządzanie wydaniami towarów",              Category = "Produkcja i Magazyn", Icon = "🗃️" },
                new() { Key = "KartotekaTowarow",     DisplayName = "Kartoteka Towarów",     Description = "Przeglądanie i edycja artykułów",            Category = "Produkcja i Magazyn", Icon = "🗂️" },
                new() { Key = "ListaPartii",          DisplayName = "Lista Partii Ubojowych",Description = "Zarządzanie partiami produkcyjnymi",         Category = "Produkcja i Magazyn", Icon = "📋" },

                // ─── Sprzedaż i CRM ───
                new() { Key = "CRM",                  DisplayName = "Relacje z Klientami",   Description = "Zarządzanie relacjami z odbiorcami",         Category = "Sprzedaż i CRM", Icon = "🤝" },
                new() { Key = "KartotekaOdbiorcow",   DisplayName = "Kartoteka Odbiorców",   Description = "Pełna baza danych klientów",                 Category = "Sprzedaż i CRM", Icon = "👤" },
                new() { Key = "ZamowieniaOdbiorcow",  DisplayName = "Zamówienia Klientów",   Description = "Przyjmowanie zamówień",                      Category = "Sprzedaż i CRM", Icon = "🛒" },
                new() { Key = "DashboardHandlowca",   DisplayName = "Dashboard Handlowca",   Description = "Kompleksowa analiza sprzedaży handlowca",    Category = "Sprzedaż i CRM", Icon = "📊" },
                new() { Key = "DokumentySprzedazy",   DisplayName = "Faktury Sprzedaży",     Description = "Przeglądanie faktur i WZ",                   Category = "Sprzedaż i CRM", Icon = "🧾" },
                new() { Key = "PanelFaktur",          DisplayName = "Panel Faktur",          Description = "Tworzenie faktur w Symfonii",                Category = "Sprzedaż i CRM", Icon = "📋" },
                new() { Key = "OfertaCenowa",         DisplayName = "Kreator Ofert",         Description = "Tworzenie ofert cenowych",                   Category = "Sprzedaż i CRM", Icon = "💰" },
                new() { Key = "ListaOfert",           DisplayName = "Archiwum Ofert",        Description = "Historia ofert handlowych",                  Category = "Sprzedaż i CRM", Icon = "📂" },
                new() { Key = "DashboardOfert",       DisplayName = "Analiza Ofert",         Description = "Statystyki skuteczności ofert",              Category = "Sprzedaż i CRM", Icon = "📊" },
                new() { Key = "DashboardWyczerpalnosci",DisplayName = "Klasy Wagowe",        Description = "Rozdzielanie klas wagowych",                 Category = "Sprzedaż i CRM", Icon = "⚖️" },
                new() { Key = "PanelReklamacji",      DisplayName = "Reklamacje Klientów",   Description = "Obsługa reklamacji odbiorców",               Category = "Sprzedaż i CRM", Icon = "⚠️" },
                new() { Key = "ReklamacjeJakosc",     DisplayName = "Reklamacje Jakość",     Description = "Reklamacje wewnętrzne jakości",              Category = "Sprzedaż i CRM", Icon = "🔍" },
                new() { Key = "StatystykiReklamacji", DisplayName = "Analiza Reklamacji",    Description = "Dashboard analityczny korekt i reklamacji",  Category = "Sprzedaż i CRM", Icon = "📊" },
                new() { Key = "PanelPaniJola",        DisplayName = "Panel Pani Jola",       Description = "Uproszczony widok zamówień",                 Category = "Sprzedaż i CRM", Icon = "📞" },
                new() { Key = "MapaKlientow",         DisplayName = "Mapa Klientów",         Description = "Wizualizacja lokalizacji klientów",          Category = "Sprzedaż i CRM", Icon = "🗺️" },

                // ─── Planowanie i Analizy ───
                new() { Key = "AnalitykaPelna",       DisplayName = "Analityka Pełna",       Description = "Plan • Realizacja • Bilans • Wydajność",     Category = "Planowanie i Analizy", Icon = "📊" },
                new() { Key = "PlanTygodniowy",       DisplayName = "Plan Tygodniowy",       Description = "Harmonogram uboju i krojenia",               Category = "Planowanie i Analizy", Icon = "🗓️" },
                new() { Key = "DashboardPrzychodu",   DisplayName = "Przychód Żywca LIVE",   Description = "Dashboard przyjęć żywca w czasie rzeczywistym",Category = "Planowanie i Analizy", Icon = "🐔" },
                new() { Key = "DashboardZamowien",    DisplayName = "Dashboard Zamówień",    Description = "Bilans zamówień i wydań",                    Category = "Planowanie i Analizy", Icon = "📊" },
                new() { Key = "QuizDrobiarstwo",      DisplayName = "Quiz Drobiarstwo",      Description = "Quiz szkoleniowy z wiedzy o drobiarstwie",   Category = "Planowanie i Analizy", Icon = "🎓" },

                // ─── Opakowania i Transport ───
                new() { Key = "OpakowaniaWinForm",    DisplayName = "Opakowania Zwrotne",    Description = "Salda opakowań zwrotnych kontrahentów",      Category = "Opakowania i Transport", Icon = "📦" },
                new() { Key = "UstalanieTranportu",   DisplayName = "Planowanie Transportu", Description = "Organizacja tras dostaw",                    Category = "Opakowania i Transport", Icon = "🚚" },
                new() { Key = "TransportZmiany",      DisplayName = "Zmiany Transportu",     Description = "Zatwierdzanie zmian w transporcie",          Category = "Opakowania i Transport", Icon = "📋" },
                new() { Key = "Flota",                DisplayName = "Flota Pojazdów",        Description = "Zarządzanie kierowcami i pojazdami",         Category = "Opakowania i Transport", Icon = "🚛" },
                new() { Key = "MapaFloty",            DisplayName = "Mapa Floty",            Description = "Mapa live GPS pojazdów z Webfleet",          Category = "Opakowania i Transport", Icon = "🗺️" },
                new() { Key = "OsCzasuFloty",         DisplayName = "Oś Czasu Floty",        Description = "Oś czasu 24h pojazdów",                      Category = "Opakowania i Transport", Icon = "📊" },
                new() { Key = "RaportFloty",          DisplayName = "Raport Floty",          Description = "Raport efektywności pojazdów",               Category = "Opakowania i Transport", Icon = "📈" },

                // ─── Finanse i Zarządzanie ───
                new() { Key = "PulpitZarzadu",        DisplayName = "Pulpit Zarządu",        Description = "Dashboardy i wskaźniki dla zarządu",         Category = "Finanse i Zarządzanie", Icon = "🏛️" },
                new() { Key = "DaneFinansowe",        DisplayName = "Wyniki Finansowe",      Description = "Przychody, koszty, marże",                   Category = "Finanse i Zarządzanie", Icon = "💼" },
                new() { Key = "CentrumSpotkan",       DisplayName = "Centrum Spotkań",       Description = "Rejestr spotkań i wizyt",                    Category = "Finanse i Zarządzanie", Icon = "📆" },
                new() { Key = "NotatkiZeSpotkan",     DisplayName = "Notatki Służbowe",      Description = "Notatki ze spotkań biznesowych",             Category = "Finanse i Zarządzanie", Icon = "📝" },
                new() { Key = "KomunikatorFirmowy",   DisplayName = "Komunikator Firmowy",   Description = "Wewnętrzny czat między pracownikami",        Category = "Finanse i Zarządzanie", Icon = "💬" },
                new() { Key = "PorannyBriefing",      DisplayName = "Poranny Briefing",      Description = "Newsy, analizy AI, konkurencja, ceny",       Category = "Finanse i Zarządzanie", Icon = "📰" },

                // ─── Kadry i HR ───
                new() { Key = "KontrolaGodzin",       DisplayName = "Kontrola Czasu Pracy",  Description = "Monitoring czasu pracy pracowników",         Category = "Kadry i HR", Icon = "⏰" },
                new() { Key = "WnioskiUrlopowe",      DisplayName = "Wnioski Urlopowe",      Description = "Kalendarz urlopów i wnioski pracowników",    Category = "Kadry i HR", Icon = "🏖️" },

                // ─── Administracja Systemu ───
                new() { Key = "ZmianyUHodowcow",      DisplayName = "Wnioski o Zmiany",      Description = "Zatwierdzanie zmian danych hodowców",        Category = "Administracja Systemu", Icon = "📝" },
                new() { Key = "AdminPermissions",     DisplayName = "Zarządzanie Uprawnieniami",Description = "Nadawanie uprawnień użytkownikom",          Category = "Administracja Systemu", Icon = "🔐" },
                new() { Key = "CallReminders",        DisplayName = "Przypomnienia Telefonów",Description = "Konfiguracja przypomnień o telefonach CRM", Category = "Administracja Systemu", Icon = "⏰" },
                new() { Key = "ProductImages",        DisplayName = "Zdjęcia Produktów",     Description = "Zarządzanie zdjęciami produktów",            Category = "Administracja Systemu", Icon = "📷" },
                new() { Key = "UstawieniaZmianZamowien",DisplayName = "Ustawienia Zmian Zamówień",Description = "Konfiguracja zmian w zamówieniach",     Category = "Administracja Systemu", Icon = "⚙️" },
                new() { Key = "CentrumNagranAI",      DisplayName = "Centrum nagrań AI",     Description = "Wyszukiwanie zdarzeń w CCTV przez Claude AI",Category = "Administracja Systemu", Icon = "🎥" },
            };
        }

        // Zsynchronizowana mapa dostępu — musi odpowiadać Menu.cs ParseAccessString
        // i AdminPermissionsForm.GetAccessMap()
        public Dictionary<int, string> GetAccessMap()
        {
            return new Dictionary<int, string>
            {
                [0] = "DaneHodowcy",            [1] = "ZakupPaszyPisklak",      [2] = "WstawieniaHodowcy",      [3] = "TerminyDostawyZywca",
                [4] = "PlachtyAviloga",         [5] = "DokumentyZakupu",        [6] = "Specyfikacje",           [7] = "PlatnosciHodowcy",
                [8] = "CRM",                    [9] = "ZamowieniaOdbiorcow",    [10] = "KalkulacjaKrojenia",    [11] = "PrzychodMrozni",
                [12] = "DokumentySprzedazy",    [13] = "PodsumowanieSaldOpak",  [14] = "SaldaOdbiorcowOpak",    [15] = "DaneFinansowe",
                [16] = "UstalanieTranportu",    [17] = "ZmianyUHodowcow",       [18] = "ProdukcjaPodglad",      [19] = "OfertaCenowa",
                [20] = "PrognozyUboju",         [21] = "AnalizaTygodniowa",     [22] = "NotatkiZeSpotkan",      [23] = "PlanTygodniowy",
                [24] = "LiczenieMagazynu",      [25] = "PanelMagazyniera",      [26] = "KartotekaOdbiorcow",    [27] = "AnalizaWydajnosci",
                [28] = "RezerwacjaKlas",        [29] = "DashboardWyczerpalnosci",[30] = "ListaOfert",           [31] = "DashboardOfert",
                [32] = "PanelReklamacji",       [33] = "ReklamacjeJakosc",      [34] = "RaportyHodowcow",       [35] = "AdminPermissions",
                [36] = "AnalizaPrzychodu",      [37] = "DashboardHandlowca",    [38] = "PanelFaktur",           [39] = "PanelPortiera",
                [40] = "PanelLekarza",          [41] = "KontrolaGodzin",        [42] = "CentrumSpotkan",        [43] = "PanelPaniJola",
                [44] = "KomunikatorFirmowy",    [45] = "RozliczeniaAvilog",     [46] = "DashboardPrzychodu",    [47] = "MapaKlientow",
                [48] = "WnioskiUrlopowe",       [49] = "DashboardZamowien",     [50] = "QuizDrobiarstwo",       [51] = "PulpitZarzadu",
                [52] = "CallReminders",         [53] = "PorannyBriefing",       [54] = "ProductImages",         [55] = "PozyskiwanieHodowcow",
                [56] = "KartotekaTowarow",      [57] = "Flota",                 [58] = "ListaPartii",           [59] = "TransportZmiany",
                [60] = "OpakowaniaWinForm",     [61] = "UstawieniaZmianZamowien",[62] = "MapaFloty",            [63] = "OsCzasuFloty",
                [64] = "RaportFloty",           [65] = "StatystykiReklamacji",  [66] = "AnalitykaPelna",        [67] = "CentrumNagranAI",
            };
        }

        public void ApplyAccessToModules(string accessString, List<AdminModuleInfo> modules)
        {
            var map = GetAccessMap();
            foreach (var module in modules)
            {
                var pos = map.FirstOrDefault(kv => kv.Value == module.Key).Key;
                module.HasAccess = pos >= 0 && pos < accessString.Length && accessString[pos] == '1';
            }
        }

        public int GetCategoryOrder(string category) => category switch
        {
            "Zaopatrzenie i Zakupy"   => 1,
            "Produkcja i Magazyn"     => 2,
            "Sprzedaż i CRM"          => 3,
            "Planowanie i Analizy"    => 4,
            "Opakowania i Transport"  => 5,
            "Finanse i Zarządzanie"   => 6,
            "Kadry i HR"              => 7,
            "Administracja Systemu"   => 8,
            _ => 99
        };

        // Kolory działów (RGB) — zsynchronizowane z Menu.cs i WinForms AdminPermissionsForm
        public (byte R, byte G, byte B) GetCategoryColor(string category) => category switch
        {
            "Zaopatrzenie i Zakupy"   => (46, 125, 50),     // Zielony
            "Produkcja i Magazyn"     => (230, 81, 0),      // Pomarańczowy
            "Sprzedaż i CRM"          => (25, 118, 210),    // Niebieski
            "Planowanie i Analizy"    => (74, 20, 140),     // Fioletowy
            "Opakowania i Transport"  => (0, 96, 100),      // Turkusowy
            "Finanse i Zarządzanie"   => (69, 90, 100),     // Szaroniebieski
            "Kadry i HR"              => (126, 87, 194),    // Indygo
            "Administracja Systemu"   => (183, 28, 28),     // Czerwony
            _ => (96, 96, 96)
        };

        // Presety uprawnień (admin / kierownik / handlowiec / magazynier / podgląd / brak)
        public void ApplyPreset(List<AdminModuleInfo> modules, string preset)
        {
            foreach (var m in modules)
            {
                m.HasAccess = preset switch
                {
                    "admin"     => true,
                    "manager"   => m.Category != "Administracja Systemu",
                    "sales"     => m.Category == "Sprzedaż i CRM" || m.Category == "Planowanie i Analizy",
                    "warehouse" => m.Category == "Produkcja i Magazyn" || m.Category == "Opakowania i Transport",
                    "viewer"    => m.Category == "Planowanie i Analizy" || m.Category == "Finanse i Zarządzanie",
                    "none"      => false,
                    _           => m.HasAccess
                };
            }
        }

        public static string GetPresetDisplayName(string preset) => preset switch
        {
            "admin"     => "Administrator (wszystko)",
            "manager"   => "Kierownik (bez administracji)",
            "sales"     => "Handlowiec (sprzedaż + CRM)",
            "warehouse" => "Magazynier (produkcja + magazyn)",
            "viewer"    => "Podgląd (tylko odczyt analiz)",
            "none"      => "Wyczyść wszystko",
            _           => preset
        };
    }
}
