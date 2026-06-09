using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kalendarz1.Zywiec.Kalendarz.Services
{
    // Service do tabeli dbo.CallLog — historia wykonanych połączeń.
    // Tabela tworzona automatycznie (CREATE IF NOT EXISTS) przy pierwszym użyciu.
    public static class CallLogService
    {
        private static bool _tableReady = false;

        public sealed class CallLogWpis
        {
            public int Id { get; init; }
            public string Dostawca { get; init; } = "";
            public string Numer { get; init; } = "";
            public string UserID { get; init; } = "";
            public string Zrodlo { get; init; } = "";
            public bool Sukces { get; init; }
            public DateTime CreatedAt { get; init; }
        }

        private static async Task EnsureTableAsync(SqlConnection conn)
        {
            if (_tableReady) return;
            const string ddl = @"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CallLog' AND schema_id = SCHEMA_ID('dbo'))
                BEGIN
                    CREATE TABLE dbo.CallLog
                    (
                        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallLog PRIMARY KEY,
                        Dostawca    NVARCHAR(255)    NULL,
                        Numer       NVARCHAR(32)     NOT NULL,
                        UserID      NVARCHAR(50)     NOT NULL,
                        Zrodlo      NVARCHAR(50)     NOT NULL,
                        Sukces      BIT              NOT NULL,
                        StatusKod   INT              NULL,
                        Komunikat   NVARCHAR(500)    NULL,
                        CreatedAt   DATETIME         NOT NULL CONSTRAINT DF_CallLog_CreatedAt DEFAULT (GETDATE())
                    );
                    CREATE INDEX IX_CallLog_Dostawca_CreatedAt ON dbo.CallLog (Dostawca, CreatedAt DESC);
                    CREATE INDEX IX_CallLog_UserID_CreatedAt   ON dbo.CallLog (UserID, CreatedAt DESC);
                    CREATE INDEX IX_CallLog_CreatedAt          ON dbo.CallLog (CreatedAt DESC);
                END";
            using var cmd = new SqlCommand(ddl, conn);
            await cmd.ExecuteNonQueryAsync();
            _tableReady = true;
        }

        public static async Task<int> ZapiszAsync(
            string connectionString,
            string dostawca,
            string numer,
            string userId,
            string zrodlo,
            bool sukces,
            int? statusKod,
            string? komunikat)
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();
                await EnsureTableAsync(conn);
                using var cmd = new SqlCommand(@"
                    INSERT INTO dbo.CallLog (Dostawca, Numer, UserID, Zrodlo, Sukces, StatusKod, Komunikat)
                    OUTPUT INSERTED.Id
                    VALUES (@d, @n, @u, @z, @s, @k, @m)", conn);
                cmd.Parameters.AddWithValue("@d", (object?)dostawca ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@n", numer ?? "");
                cmd.Parameters.AddWithValue("@u", userId ?? "");
                cmd.Parameters.AddWithValue("@z", zrodlo ?? "");
                cmd.Parameters.AddWithValue("@s", sukces);
                cmd.Parameters.AddWithValue("@k", (object?)statusKod ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@m", (object?)komunikat ?? DBNull.Value);
                var r = await cmd.ExecuteScalarAsync();
                return r != null && r != DBNull.Value ? Convert.ToInt32(r) : 0;
            }
            catch { return 0; }
        }

        // Liczba akcji (SMS + Call) dziś dla danego usera — do counter w status bar
        public static async Task<(int sms, int call)> LiczbaAkcjiDzisAsync(string connectionString, string? userId)
        {
            int sms = 0, call = 0;
            if (string.IsNullOrWhiteSpace(userId)) return (0, 0);
            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                // SMS z ContactHistory (Cykle Wstawień) + SmsDostawySnapshot (Kalendarz)
                using (var cmd = new SqlCommand(@"
                    SELECT
                        (SELECT COUNT(*) FROM dbo.ContactHistory
                         WHERE UserID = @u AND CAST(CreatedAt AS date) = CAST(GETDATE() AS date)
                           AND (Reason LIKE 'Auto SMS%' OR Reason LIKE '%SMS%' OR Reason LIKE 'Potwierdzenie%')) +
                        (SELECT COUNT(*) FROM dbo.SmsDostawySnapshot
                         WHERE UserID = @u AND CAST(CreatedAt AS date) = CAST(GETDATE() AS date))
                        AS SmsCount,
                        (SELECT COUNT(*) FROM dbo.CallLog
                         WHERE UserID = @u AND CAST(CreatedAt AS date) = CAST(GETDATE() AS date)
                           AND Sukces = 1) AS CallCount", conn) { CommandTimeout = 5 })
                {
                    cmd.Parameters.AddWithValue("@u", userId);
                    using var r = await cmd.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                    {
                        sms = r.IsDBNull(0) ? 0 : r.GetInt32(0);
                        call = r.IsDBNull(1) ? 0 : r.GetInt32(1);
                    }
                }
            }
            catch { }
            return (sms, call);
        }

        // Wykrywa do których hodowców jest już przypisany ten numer (z dbo.Dostawcy).
        // Używane gdy user dodaje numer — alert "ten numer jest już u X innych hodowców".
        public static async Task<List<string>> ZnajdzInnychHodowcowZNumeremAsync(
            string connectionString, string numer, string aktualnyDostawca)
        {
            var wynik = new List<string>();
            if (string.IsNullOrWhiteSpace(numer)) return wynik;
            try
            {
                // Normalizujemy do porównania — usuń wszystko poza cyframi
                string norm = new string(numer.Where(char.IsDigit).ToArray());
                if (norm.Length < 9) return wynik;
                // Bierzemy 9 ostatnich cyfr — pomijamy prefiks kraju przy porównaniu
                string suffix = norm.Substring(norm.Length - 9);

                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT ShortName
                    FROM dbo.Dostawcy
                    WHERE (Phone1 LIKE '%' + @suf
                        OR Phone2 LIKE '%' + @suf
                        OR Phone3 LIKE '%' + @suf)
                      AND ShortName <> @nieten
                      AND Name <> @nieten", conn) { CommandTimeout = 5 };
                cmd.Parameters.AddWithValue("@suf", suffix);
                cmd.Parameters.AddWithValue("@nieten", aktualnyDostawca ?? "");
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    if (!r.IsDBNull(0)) wynik.Add(r.GetString(0));
                }
            }
            catch { }
            return wynik;
        }
    }
}
