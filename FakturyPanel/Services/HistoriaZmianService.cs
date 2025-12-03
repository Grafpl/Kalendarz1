using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.FakturyPanel.Services
{
    /// <summary>
    /// Statyczny serwis do logowania historii zmian zamówień
    /// Może być używany z dowolnego miejsca w aplikacji
    /// </summary>
    public static class HistoriaZmianService
    {
        private static readonly string _connectionStringLibraNet =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private static bool _tableInitialized = false;

        /// <summary>
        /// Loguje utworzenie nowego zamówienia
        /// </summary>
        public static async Task LogujUtworzenie(int zamowienieId, string uzytkownik, string uzytkownikNazwa = null, string opisDodatkowy = null)
        {
            await LogujZmianeAsync(zamowienieId, "UTWORZENIE", uzytkownik, uzytkownikNazwa,
                opisZmiany: opisDodatkowy ?? $"Utworzono nowe zamówienie #{zamowienieId}");
        }

        /// <summary>
        /// Loguje edycję zamówienia
        /// </summary>
        public static async Task LogujEdycje(int zamowienieId, string uzytkownik, string uzytkownikNazwa = null,
            string poleZmienione = null, string wartoscPoprzednia = null, string wartoscNowa = null, string opisDodatkowy = null)
        {
            string opis = opisDodatkowy;
            if (string.IsNullOrEmpty(opis) && !string.IsNullOrEmpty(poleZmienione))
            {
                opis = $"Zmieniono {poleZmienione}";
                if (!string.IsNullOrEmpty(wartoscPoprzednia) || !string.IsNullOrEmpty(wartoscNowa))
                    opis += $": '{wartoscPoprzednia ?? "(puste)"}' → '{wartoscNowa ?? "(puste)"}'";
            }

            await LogujZmianeAsync(zamowienieId, "EDYCJA", uzytkownik, uzytkownikNazwa,
                poleZmienione, wartoscPoprzednia, wartoscNowa, opis);
        }

        /// <summary>
        /// Loguje anulowanie zamówienia
        /// </summary>
        public static async Task LogujAnulowanie(int zamowienieId, string uzytkownik, string uzytkownikNazwa = null, string powod = null)
        {
            await LogujZmianeAsync(zamowienieId, "ANULOWANIE", uzytkownik, uzytkownikNazwa,
                opisZmiany: powod ?? $"Anulowano zamówienie #{zamowienieId}");
        }

        /// <summary>
        /// Loguje przywrócenie anulowanego zamówienia
        /// </summary>
        public static async Task LogujPrzywrocenie(int zamowienieId, string uzytkownik, string uzytkownikNazwa = null)
        {
            await LogujZmianeAsync(zamowienieId, "PRZYWROCENIE", uzytkownik, uzytkownikNazwa,
                opisZmiany: $"Przywrócono zamówienie #{zamowienieId}");
        }

        /// <summary>
        /// Loguje usunięcie zamówienia
        /// </summary>
        public static async Task LogujUsuniecie(int zamowienieId, string uzytkownik, string uzytkownikNazwa = null)
        {
            await LogujZmianeAsync(zamowienieId, "USUNIECIE", uzytkownik, uzytkownikNazwa,
                opisZmiany: $"Trwale usunięto zamówienie #{zamowienieId}");
        }

        /// <summary>
        /// Loguje zmianę notatki
        /// </summary>
        public static async Task LogujZmianeNotatki(int zamowienieId, string uzytkownik,
            string staraNotatka, string nowaNotatka, string uzytkownikNazwa = null)
        {
            await LogujEdycje(zamowienieId, uzytkownik, uzytkownikNazwa,
                "Notatka",
                TruncateString(staraNotatka, 500),
                TruncateString(nowaNotatka, 500),
                "Zmieniono notatkę zamówienia");
        }

        /// <summary>
        /// Loguje zmianę daty
        /// </summary>
        public static async Task LogujZmianeData(int zamowienieId, string uzytkownik,
            string typDaty, DateTime? stara, DateTime? nowa, string uzytkownikNazwa = null)
        {
            await LogujEdycje(zamowienieId, uzytkownik, uzytkownikNazwa,
                typDaty,
                stara?.ToString("yyyy-MM-dd"),
                nowa?.ToString("yyyy-MM-dd"),
                $"Zmieniono {typDaty.ToLower()}");
        }

        /// <summary>
        /// Główna metoda logowania zmiany
        /// </summary>
        private static async Task LogujZmianeAsync(
            int zamowienieId,
            string typZmiany,
            string uzytkownik,
            string uzytkownikNazwa = null,
            string poleZmienione = null,
            string wartoscPoprzednia = null,
            string wartoscNowa = null,
            string opisZmiany = null)
        {
            try
            {
                await using var cn = new SqlConnection(_connectionStringLibraNet);
                await cn.OpenAsync();

                // Upewnij się, że tabela istnieje
                if (!_tableInitialized)
                {
                    await EnsureTableExistsAsync(cn);
                    _tableInitialized = true;
                }

                var sql = @"
                    INSERT INTO HistoriaZmianZamowien
                        (ZamowienieId, TypZmiany, PoleZmienione, WartoscPoprzednia,
                         WartoscNowa, Uzytkownik, UzytkownikNazwa, OpisZmiany, NazwaKomputera)
                    VALUES
                        (@ZamowienieId, @TypZmiany, @PoleZmienione, @WartoscPoprzednia,
                         @WartoscNowa, @Uzytkownik, @UzytkownikNazwa, @OpisZmiany, @NazwaKomputera)";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@ZamowienieId", zamowienieId);
                cmd.Parameters.AddWithValue("@TypZmiany", typZmiany);
                cmd.Parameters.AddWithValue("@PoleZmienione", (object)poleZmienione ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@WartoscPoprzednia", (object)wartoscPoprzednia ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@WartoscNowa", (object)wartoscNowa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Uzytkownik", uzytkownik ?? "SYSTEM");
                cmd.Parameters.AddWithValue("@UzytkownikNazwa", (object)uzytkownikNazwa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OpisZmiany", (object)opisZmiany ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NazwaKomputera", Environment.MachineName);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // Logowanie nie powinno przerywać głównej operacji
                System.Diagnostics.Debug.WriteLine($"[HistoriaZmian] Błąd logowania: {ex.Message}");
            }
        }

        /// <summary>
        /// Tworzy tabelę historii jeśli nie istnieje
        /// </summary>
        private static async Task EnsureTableExistsAsync(SqlConnection cn)
        {
            var sql = @"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HistoriaZmianZamowien]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [dbo].[HistoriaZmianZamowien](
                        [Id] INT IDENTITY(1,1) PRIMARY KEY,
                        [ZamowienieId] INT NOT NULL,
                        [TypZmiany] NVARCHAR(50) NOT NULL,
                        [PoleZmienione] NVARCHAR(100) NULL,
                        [WartoscPoprzednia] NVARCHAR(MAX) NULL,
                        [WartoscNowa] NVARCHAR(MAX) NULL,
                        [Uzytkownik] NVARCHAR(50) NOT NULL,
                        [UzytkownikNazwa] NVARCHAR(200) NULL,
                        [DataZmiany] DATETIME NOT NULL DEFAULT GETDATE(),
                        [OpisZmiany] NVARCHAR(MAX) NULL,
                        [DodatkoweInfo] NVARCHAR(MAX) NULL,
                        [AdresIP] NVARCHAR(50) NULL,
                        [NazwaKomputera] NVARCHAR(100) NULL
                    );

                    CREATE NONCLUSTERED INDEX [IX_HistoriaZmianZamowien_ZamowienieId]
                    ON [dbo].[HistoriaZmianZamowien] ([ZamowienieId])
                    INCLUDE ([TypZmiany], [DataZmiany], [Uzytkownik]);

                    CREATE NONCLUSTERED INDEX [IX_HistoriaZmianZamowien_DataZmiany]
                    ON [dbo].[HistoriaZmianZamowien] ([DataZmiany] DESC)
                    INCLUDE ([ZamowienieId], [TypZmiany], [Uzytkownik]);
                END";

            await using var cmd = new SqlCommand(sql, cn);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Przycina tekst do określonej długości
        /// </summary>
        private static string TruncateString(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// Loguje wiele zmian jednocześnie (dla kompleksowych edycji)
        /// </summary>
        public static async Task LogujWieleZmian(int zamowienieId, string uzytkownik,
            string uzytkownikNazwa, params (string pole, string stara, string nowa)[] zmiany)
        {
            foreach (var (pole, stara, nowa) in zmiany)
            {
                if (stara != nowa)
                {
                    await LogujEdycje(zamowienieId, uzytkownik, uzytkownikNazwa, pole, stara, nowa);
                }
            }
        }
    }
}
