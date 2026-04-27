using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    // Repozytorium dla tabeli HarmonogramDostaw + powiązane (operators, audit log).
    // Cel: jedno miejsce na connection string i SQL-e używane przez SprawdzalkaUmow / UmowyForm.
    public class HarmonogramDostawRepository
    {
        private readonly string _connectionString;

        public HarmonogramDostawRepository(string connectionString = null)
        {
            _connectionString = connectionString ??
                "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        }

        // === LOAD: główne SELECT z joinami operators (×3) ===
        public DataTable LoadDostawy(bool pokazArchiwalne, int domyslnyZakresMiesiecy)
        {
            string dolnaGranica = pokazArchiwalne
                ? "'2021-01-01'"
                : "DATEADD(MONTH, -" + domyslnyZakresMiesiecy + ", GETDATE())";

            string query = $@"
                SELECT
                    h.[LP] AS ID, h.[DataOdbioru], h.[Dostawca],
                    CAST(ISNULL(h.[Utworzone],0) AS bit) AS Utworzone,
                    CAST(ISNULL(h.[Wysłane],0) AS bit) AS Wysłane,
                    CAST(ISNULL(h.[Otrzymane],0) AS bit) AS Otrzymane,
                    CAST(ISNULL(h.[Posrednik],0) AS bit) AS Posrednik,
                    h.[Auta], h.[SztukiDek], h.[WagaDek], h.[SztSzuflada],
                    ISNULL(u1.Name, h.KtoUtw) AS KtoUtw, h.[KiedyUtw],
                    CAST(h.KtoUtw AS VARCHAR(50)) AS KtoUtwID,
                    ISNULL(u2.Name, h.KtoWysl) AS KtoWysl, h.[KiedyWysl],
                    CAST(h.KtoWysl AS VARCHAR(50)) AS KtoWyslID,
                    ISNULL(u3.Name, h.KtoOtrzym) AS KtoOtrzym, h.[KiedyOtrzm],
                    CAST(h.KtoOtrzym AS VARCHAR(50)) AS KtoOtrzymID
                FROM [LibraNet].[dbo].[HarmonogramDostaw] h
                LEFT JOIN [LibraNet].[dbo].[operators] u1 ON TRY_CAST(h.KtoUtw AS INT) = u1.ID
                LEFT JOIN [LibraNet].[dbo].[operators] u2 ON TRY_CAST(h.KtoWysl AS INT) = u2.ID
                LEFT JOIN [LibraNet].[dbo].[operators] u3 ON TRY_CAST(h.KtoOtrzym AS INT) = u3.ID
                WHERE h.Bufor = 'Potwierdzony'
                  AND h.DataOdbioru BETWEEN {dolnaGranica} AND DATEADD(DAY, 2, GETDATE())
                ORDER BY h.DataOdbioru DESC;";

            using var conn = new SqlConnection(_connectionString);
            using var adapter = new SqlDataAdapter(query, conn);
            var table = new DataTable();
            adapter.Fill(table);
            return table;
        }

        // Async wariant - okno otwiera się natychmiast, dane dociągają w tle.
        public async Task<DataTable> LoadDostawyAsync(bool pokazArchiwalne, int domyslnyZakresMiesiecy)
        {
            return await Task.Run(() => LoadDostawy(pokazArchiwalne, domyslnyZakresMiesiecy));
        }

        // === UPDATE: flaga + Kto/Kiedy ===
        // columnName: "Utworzone" | "Wysłane" | "Otrzymane" | "Posrednik"
        // Pośrednik nie ma KtoCol/KiedyCol - osobna ścieżka.
        public void UpdateFlag(int lp, string columnName, bool value, int? userId, out bool? oldValue)
        {
            oldValue = null;
            string[] allowed = { "Utworzone", "Wysłane", "Otrzymane", "Posrednik" };
            if (Array.IndexOf(allowed, columnName) < 0)
                throw new InvalidOperationException("Nieobsługiwana kolumna: " + columnName);

            string ktoCol = null, kiedyCol = null;
            switch (columnName)
            {
                case "Utworzone": ktoCol = "KtoUtw"; kiedyCol = "KiedyUtw"; break;
                case "Wysłane": ktoCol = "KtoWysl"; kiedyCol = "KiedyWysl"; break;
                case "Otrzymane": ktoCol = "KtoOtrzym"; kiedyCol = "KiedyOtrzm"; break;
            }

            if (value && ktoCol != null && !userId.HasValue)
                throw new InvalidOperationException("UserID musi być podany dla " + columnName);

            string sql = ktoCol == null
                ? $@"DECLARE @old bit;
                     SELECT @old = [{columnName}] FROM dbo.HarmonogramDostaw WHERE [LP] = @id;
                     UPDATE dbo.HarmonogramDostaw SET [{columnName}] = @val WHERE [LP] = @id;
                     SELECT @old;"
                : $@"DECLARE @old bit;
                     SELECT @old = [{columnName}] FROM dbo.HarmonogramDostaw WHERE [LP] = @id;
                     UPDATE dbo.HarmonogramDostaw
                       SET [{columnName}] = @val,
                           [{ktoCol}] = CASE WHEN @val = 1 THEN @kto ELSE NULL END,
                           [{kiedyCol}] = CASE WHEN @val = 1 THEN GETDATE() ELSE NULL END
                       WHERE [LP] = @id;
                     SELECT @old;";

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@val", value);
            cmd.Parameters.AddWithValue("@id", lp);
            if (ktoCol != null && userId.HasValue)
                cmd.Parameters.AddWithValue("@kto", userId.Value);

            conn.Open();
            var result = cmd.ExecuteScalar();
            if (result != null && result != DBNull.Value)
                oldValue = Convert.ToBoolean(result);
        }

        // === OPERATORS ===
        // Pojedyncze imię+nazwisko (do uzupełnienia komórki bez reload).
        public string GetOperatorName(int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT TOP 1 Name FROM dbo.operators WHERE ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", userId);
            conn.Open();
            var v = cmd.ExecuteScalar();
            return v == null || v == DBNull.Value ? userId.ToString() : v.ToString();
        }

        // === AUDIT LOG ===
        public void InsertAuditLog(int lp, string columnName, bool? oldValue, bool newValue, int? userId)
        {
            string sql = @"
                IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HarmonogramDostaw_AuditLog' AND schema_id = SCHEMA_ID('dbo'))
                INSERT INTO dbo.HarmonogramDostaw_AuditLog (LP, ColumnName, OldValue, NewValue, UserID, ChangedAt)
                VALUES (@lp, @col, @old, @new, @user, GETDATE());";

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lp", lp);
            cmd.Parameters.AddWithValue("@col", columnName);
            cmd.Parameters.AddWithValue("@old", (object)oldValue ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@new", newValue);
            cmd.Parameters.AddWithValue("@user", (object)userId ?? DBNull.Value);

            conn.Open();
            // Try-catch: jeśli tabela audit nie istnieje, nie blokujemy update flagi
            try { cmd.ExecuteNonQuery(); } catch { }
        }

        public DataTable GetAuditHistory(int lp)
        {
            string sql = @"
                IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HarmonogramDostaw_AuditLog' AND schema_id = SCHEMA_ID('dbo'))
                SELECT a.ChangedAt, a.ColumnName, a.OldValue, a.NewValue,
                       ISNULL(o.Name, CAST(a.UserID AS VARCHAR(20))) AS UserName
                FROM dbo.HarmonogramDostaw_AuditLog a
                LEFT JOIN dbo.operators o ON a.UserID = o.ID
                WHERE a.LP = @lp
                ORDER BY a.ChangedAt DESC;";

            using var conn = new SqlConnection(_connectionString);
            using var adapter = new SqlDataAdapter(sql, conn);
            adapter.SelectCommand.Parameters.AddWithValue("@lp", lp);
            var table = new DataTable();
            try { adapter.Fill(table); } catch { /* tabela nie istnieje */ }
            return table;
        }
    }
}
