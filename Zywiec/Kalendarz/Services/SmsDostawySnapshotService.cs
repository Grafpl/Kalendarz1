using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kalendarz1.Zywiec.Kalendarz.Services
{
    // ============================================================================
    // SmsDostawySnapshotService — historia SMS-ów + porównywanie z aktualnym stanem
    //
    // Zapisuje snapshot kluczowych pól (data, auta, sztuki, waga) w momencie
    // utworzenia SMS-a "o szczegółach dostawy". Później dla każdej dostawy
    // sprawdza czy aktualne dane różnią się od snapshotu — wtedy widać że
    // trzeba wysłać SMS aktualizujący.
    //
    // Tabela: dbo.SmsDostawySnapshot (LibraNet) — patrz CreateSmsDostawySnapshot.sql
    // ============================================================================

    public sealed class SmsSnapshotInfo
    {
        public int Id { get; init; }
        public int LP { get; init; }
        public DateTime DataOdbioru { get; init; }
        public int Auta { get; init; }
        public int SztukiDek { get; init; }
        public decimal WagaDek { get; init; }
        public string Wariant { get; init; } = "pierwszy";
        public string UserID { get; init; } = "";
        public DateTime CreatedAt { get; init; }
    }

    public static class SmsDostawySnapshotService
    {
        // Ostatni błąd — read przez code-behind żeby pokazać użytkownikowi co poszło nie tak
        public static string? LastError { get; private set; }

        // Czy tabela już utworzona w tej sesji (cache — robimy CREATE IF NOT EXISTS tylko raz)
        private static bool _tableReady = false;

        // Idempotent CREATE TABLE — wywoływane raz przy pierwszym użyciu w sesji.
        // Dzięki temu funkcja działa od razu po wdrożeniu kodu, bez konieczności ręcznego uruchamiania skryptu SQL.
        private static async Task EnsureTableAsync(SqlConnection conn)
        {
            if (_tableReady) return;
            const string ddl = @"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SmsDostawySnapshot' AND schema_id = SCHEMA_ID('dbo'))
                BEGIN
                    CREATE TABLE dbo.SmsDostawySnapshot
                    (
                        Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SmsDostawySnapshot PRIMARY KEY,
                        LP           INT              NOT NULL,
                        Dostawca     NVARCHAR(255)    NULL,
                        DataOdbioru  DATE             NOT NULL,
                        Auta         INT              NOT NULL,
                        SztukiDek    INT              NOT NULL,
                        WagaDek      DECIMAL(10,2)    NOT NULL,
                        Wariant      NVARCHAR(20)     NOT NULL CONSTRAINT DF_SmsDostawySnapshot_Wariant DEFAULT ('pierwszy'),
                        SmsText      NVARCHAR(MAX)    NULL,
                        UserID       NVARCHAR(50)     NOT NULL,
                        CreatedAt    DATETIME         NOT NULL CONSTRAINT DF_SmsDostawySnapshot_CreatedAt DEFAULT (GETDATE())
                    );
                    CREATE INDEX IX_SmsDostawySnapshot_LP_CreatedAt
                        ON dbo.SmsDostawySnapshot (LP, CreatedAt DESC);
                END";
            using var cmd = new SqlCommand(ddl, conn);
            await cmd.ExecuteNonQueryAsync();
            _tableReady = true;
        }

        // Zapisuje nowy snapshot SMS-a. Zwraca Id wstawionego wiersza (lub 0 przy błędzie).
        public static async Task<int> ZapiszSnapshotAsync(
            string connectionString,
            int lp,
            string? dostawca,
            DateTime dataOdbioru,
            int auta,
            int sztukiDek,
            decimal wagaDek,
            string smsText,
            string userId,
            string wariant = "pierwszy")
        {
            LastError = null;
            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();
                await EnsureTableAsync(conn);

                using var cmd = new SqlCommand(@"
                    INSERT INTO dbo.SmsDostawySnapshot
                        (LP, Dostawca, DataOdbioru, Auta, SztukiDek, WagaDek, Wariant, SmsText, UserID)
                    OUTPUT INSERTED.Id
                    VALUES
                        (@lp, @d, @data, @auta, @szt, @waga, @w, @text, @u)", conn);

                cmd.Parameters.AddWithValue("@lp", lp);
                cmd.Parameters.AddWithValue("@d", (object?)dostawca ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@data", dataOdbioru.Date);
                cmd.Parameters.AddWithValue("@auta", auta);
                cmd.Parameters.AddWithValue("@szt", sztukiDek);
                cmd.Parameters.AddWithValue("@waga", wagaDek);
                cmd.Parameters.AddWithValue("@w", wariant);
                cmd.Parameters.AddWithValue("@text", (object?)smsText ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@u", userId ?? "");

                var result = await cmd.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return 0;
            }
        }

        // Hurtowe pobranie NAJNOWSZYCH snapshotów dla wszystkich LP (jeden per LP).
        // Używane po LoadDostawyAsync — robi jeden round-trip zamiast N.
        public static async Task<Dictionary<int, SmsSnapshotInfo>> PobierzWszystkieNajnowszeAsync(string connectionString)
        {
            LastError = null;
            var wynik = new Dictionary<int, SmsSnapshotInfo>();
            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();
                await EnsureTableAsync(conn);
                // ROW_NUMBER w CTE bierze najnowszy snapshot per LP
                using var cmd = new SqlCommand(@"
                    WITH najnowsze AS (
                        SELECT Id, LP, DataOdbioru, Auta, SztukiDek, WagaDek, Wariant, UserID, CreatedAt,
                               ROW_NUMBER() OVER (PARTITION BY LP ORDER BY CreatedAt DESC) AS rn
                        FROM dbo.SmsDostawySnapshot
                    )
                    SELECT Id, LP, DataOdbioru, Auta, SztukiDek, WagaDek, Wariant, UserID, CreatedAt
                    FROM najnowsze
                    WHERE rn = 1", conn);

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var snap = new SmsSnapshotInfo
                    {
                        Id = r.GetInt32(0),
                        LP = r.GetInt32(1),
                        DataOdbioru = r.GetDateTime(2),
                        Auta = r.GetInt32(3),
                        SztukiDek = r.GetInt32(4),
                        WagaDek = r.GetDecimal(5),
                        Wariant = r.IsDBNull(6) ? "pierwszy" : r.GetString(6),
                        UserID = r.IsDBNull(7) ? "" : r.GetString(7),
                        CreatedAt = r.GetDateTime(8)
                    };
                    wynik[snap.LP] = snap;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            return wynik;
        }

        // Czy aktualne pola dostawy różnią się od snapshotu w POLACH KLUCZOWYCH:
        // data i auta (te najbardziej krytyczne dla hodowcy — kiedy i ile aut przyjedzie).
        // Pozostałe (sztuki/waga) mogą się drobnie zmieniać bez aktualizacji SMS-a.
        public static bool WymagaAktualizacji(SmsSnapshotInfo snap, DateTime aktualnaData, int aktualneAuta)
        {
            if (snap.DataOdbioru.Date != aktualnaData.Date) return true;
            if (snap.Auta != aktualneAuta) return true;
            return false;
        }

        // Zwraca listę różnic w czytelnej formie (do treści SMS-a aktualizującego).
        public static string OpiszZmiany(SmsSnapshotInfo snap, DateTime aktualnaData, int aktualneAuta, int aktualneSzt, decimal aktualnaWaga)
        {
            var lista = new List<string>();
            if (snap.DataOdbioru.Date != aktualnaData.Date)
                lista.Add($"data: {snap.DataOdbioru:dd.MM.yyyy} → {aktualnaData:dd.MM.yyyy}");
            if (snap.Auta != aktualneAuta)
                lista.Add($"auta: {snap.Auta} → {aktualneAuta}");
            if (snap.SztukiDek != aktualneSzt)
                lista.Add($"sztuki: {snap.SztukiDek:#,0} → {aktualneSzt:#,0}");
            if (Math.Abs(snap.WagaDek - aktualnaWaga) > 0.01m)
                lista.Add($"waga: {snap.WagaDek:0.00} → {aktualnaWaga:0.00}");
            return string.Join(", ", lista);
        }
    }
}
