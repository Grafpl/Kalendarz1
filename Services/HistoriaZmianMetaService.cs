using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Meta-dane historii zmian: tagi per wpis, obserwacje per zamówienie, read/unread per użytkownik.
    /// 3 tabele dynamicznie tworzone w LibraNet (CREATE IF NOT EXISTS).
    /// </summary>
    public static class HistoriaZmianMetaService
    {
        private static readonly string _conn =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private static readonly System.Threading.SemaphoreSlim _initLock = new(1, 1);
        private static volatile bool _initialized;

        public const string TagOk = "OK";
        public const string TagDoWyjasnienia = "DoWyjasnienia";
        public const string TagKomentarz = "Komentarz";

        public static async Task EnsureTablesAsync()
        {
            if (_initialized) return;
            await _initLock.WaitAsync();
            try
            {
                if (_initialized) return;
                await using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                const string ddl = @"
                    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='HistoriaZmianTagi' AND type='U')
                    BEGIN
                        CREATE TABLE dbo.HistoriaZmianTagi (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            HistoriaId INT NOT NULL,
                            Tag NVARCHAR(50) NOT NULL,
                            Komentarz NVARCHAR(500) NULL,
                            UserId NVARCHAR(50) NOT NULL,
                            DataDodania DATETIME NOT NULL DEFAULT GETDATE()
                        );
                        CREATE INDEX IX_HZTagi_HistoriaId ON dbo.HistoriaZmianTagi(HistoriaId);
                    END;
                    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='HistoriaZmianObserwacje' AND type='U')
                    BEGIN
                        CREATE TABLE dbo.HistoriaZmianObserwacje (
                            UserId NVARCHAR(50) NOT NULL,
                            ZamowienieId INT NOT NULL,
                            DataDodania DATETIME NOT NULL DEFAULT GETDATE(),
                            CONSTRAINT PK_HZObserwacje PRIMARY KEY (UserId, ZamowienieId)
                        );
                    END;
                    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='HistoriaZmianOdczyty' AND type='U')
                    BEGIN
                        CREATE TABLE dbo.HistoriaZmianOdczyty (
                            UserId NVARCHAR(50) NOT NULL PRIMARY KEY,
                            OstatnioOdczytane DATETIME NOT NULL DEFAULT GETDATE()
                        );
                    END;";
                await using var cmd = new SqlCommand(ddl, cn);
                await cmd.ExecuteNonQueryAsync();
                _initialized = true;
            }
            finally { _initLock.Release(); }
        }

        // ── Tagi ────────────────────────────────────────────────────────────
        public class TagInfo
        {
            public int Id { get; set; }
            public string Tag { get; set; } = "";
            public string Komentarz { get; set; } = "";
            public string UserId { get; set; } = "";
            public DateTime DataDodania { get; set; }
        }

        public static async Task<Dictionary<int, List<TagInfo>>> LoadAllTagsAsync()
        {
            await EnsureTablesAsync();
            var result = new Dictionary<int, List<TagInfo>>();
            await using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, HistoriaId, Tag, ISNULL(Komentarz, ''), UserId, DataDodania FROM dbo.HistoriaZmianTagi ORDER BY DataDodania DESC", cn);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                int historiaId = rd.GetInt32(1);
                if (!result.TryGetValue(historiaId, out var list))
                {
                    list = new List<TagInfo>();
                    result[historiaId] = list;
                }
                list.Add(new TagInfo
                {
                    Id = rd.GetInt32(0),
                    Tag = rd.GetString(2),
                    Komentarz = rd.GetString(3),
                    UserId = rd.GetString(4),
                    DataDodania = rd.GetDateTime(5)
                });
            }
            return result;
        }

        public static async Task<int> AddTagAsync(int historiaId, string tag, string komentarz, string userId)
        {
            await EnsureTablesAsync();
            await using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                INSERT INTO dbo.HistoriaZmianTagi (HistoriaId, Tag, Komentarz, UserId)
                VALUES (@hid, @t, @k, @u);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", cn);
            cmd.Parameters.AddWithValue("@hid", historiaId);
            cmd.Parameters.AddWithValue("@t", tag ?? "");
            cmd.Parameters.AddWithValue("@k", (object?)komentarz ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@u", userId ?? "");
            var id = await cmd.ExecuteScalarAsync();
            return id == null ? 0 : Convert.ToInt32(id);
        }

        public static async Task RemoveTagAsync(int tagId)
        {
            await EnsureTablesAsync();
            await using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand("DELETE FROM dbo.HistoriaZmianTagi WHERE Id = @id", cn);
            cmd.Parameters.AddWithValue("@id", tagId);
            await cmd.ExecuteNonQueryAsync();
        }

        // ── Bookmarks / obserwowane zamówienia ──────────────────────────────
        public static async Task<HashSet<int>> LoadBookmarksAsync(string userId)
        {
            await EnsureTablesAsync();
            var set = new HashSet<int>();
            await using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT ZamowienieId FROM dbo.HistoriaZmianObserwacje WHERE UserId = @u", cn);
            cmd.Parameters.AddWithValue("@u", userId ?? "");
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync()) set.Add(rd.GetInt32(0));
            return set;
        }

        public static async Task ToggleBookmarkAsync(int zamowienieId, string userId, bool addRemove)
        {
            await EnsureTablesAsync();
            await using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            string sql = addRemove
                ? "IF NOT EXISTS (SELECT 1 FROM dbo.HistoriaZmianObserwacje WHERE UserId=@u AND ZamowienieId=@z) INSERT INTO dbo.HistoriaZmianObserwacje (UserId, ZamowienieId) VALUES (@u, @z)"
                : "DELETE FROM dbo.HistoriaZmianObserwacje WHERE UserId=@u AND ZamowienieId=@z";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@u", userId ?? "");
            cmd.Parameters.AddWithValue("@z", zamowienieId);
            await cmd.ExecuteNonQueryAsync();
        }

        // ── Read/Unread ─────────────────────────────────────────────────────
        public static async Task<DateTime?> GetLastReadAsync(string userId)
        {
            await EnsureTablesAsync();
            await using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT OstatnioOdczytane FROM dbo.HistoriaZmianOdczyty WHERE UserId = @u", cn);
            cmd.Parameters.AddWithValue("@u", userId ?? "");
            var r = await cmd.ExecuteScalarAsync();
            return r == null || r == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r);
        }

        public static async Task MarkReadAsync(string userId)
        {
            await EnsureTablesAsync();
            await using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                IF EXISTS (SELECT 1 FROM dbo.HistoriaZmianOdczyty WHERE UserId=@u)
                    UPDATE dbo.HistoriaZmianOdczyty SET OstatnioOdczytane = GETDATE() WHERE UserId=@u
                ELSE
                    INSERT INTO dbo.HistoriaZmianOdczyty (UserId) VALUES (@u)", cn);
            cmd.Parameters.AddWithValue("@u", userId ?? "");
            await cmd.ExecuteNonQueryAsync();
        }

        public static async Task<int> GetUnreadCountAsync(string userId, DateTime? sinceDataUboju = null)
        {
            await EnsureTablesAsync();
            var lastRead = await GetLastReadAsync(userId) ?? DateTime.MinValue;

            await using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            string sql = @"
                SELECT COUNT(*)
                FROM dbo.HistoriaZmianZamowien h
                LEFT JOIN dbo.ZamowieniaMieso z ON z.Id = h.ZamowienieId
                WHERE h.DataZmiany > @last";
            if (sinceDataUboju.HasValue)
                sql += " AND z.DataUboju IS NOT NULL AND CAST(z.DataUboju AS DATE) >= @od";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@last", lastRead);
            if (sinceDataUboju.HasValue) cmd.Parameters.AddWithValue("@od", sinceDataUboju.Value.Date);
            var r = await cmd.ExecuteScalarAsync();
            return r == null ? 0 : Convert.ToInt32(r);
        }
    }
}
