using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Imienna lista odbiorców powiadomień — wspólna dla obu typów:
    ///   • ZAMOWIENIA_MIESA — zmiany zamówień mięsa po godzinie
    ///   • DOSTAWY_ZYWCA    — zmiany dostaw żywca w kalendarzu
    /// Tabela LibraNet.dbo.PowiadomieniaOdbiorcy. Administrator wskazuje konkretne osoby,
    /// którym mają się pokazywać powiadomienia danej kategorii.
    /// Pusta lista dla kategorii = powiadomienie dla wszystkich (zachowanie zgodne ze starym).
    /// </summary>
    public static class PowiadomieniaOdbiorcyService
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public const string KatZamowieniaMiesa = "ZAMOWIENIA_MIESA";
        public const string KatDostawyZywca = "DOSTAWY_ZYWCA";

        public class Odbiorca
        {
            public string UserID { get; set; } = "";
            public string UserName { get; set; } = "";
        }

        private static readonly System.Threading.SemaphoreSlim _initLock = new(1, 1);
        private static volatile bool _initialized;

        // Cache zbiorów odbiorców per kategoria (TTL 60 s) — IsOdbiorcaAsync wołane przy każdym pollingu/sprawdzeniu.
        private static readonly Dictionary<string, (HashSet<string> Users, DateTime Expiry)> _cache =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _cacheLock = new();
        private static readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(60);

        private static void InvalidateCache(string kategoria)
        {
            lock (_cacheLock) { _cache.Remove(kategoria); }
        }

        private static bool TryGetCached(string kategoria, out HashSet<string> users)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(kategoria, out var e) && DateTime.Now < e.Expiry)
                {
                    users = e.Users;
                    return true;
                }
            }
            users = null!;
            return false;
        }

        /// <summary>Zbiór UserID odbiorców kategorii — z 60 s cache (jedno zapytanie na odświeżenie).</summary>
        private static async Task<HashSet<string>> GetSetAsync(string kategoria)
        {
            if (TryGetCached(kategoria, out var cached)) return cached;
            await EnsureSchemaAsync();
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT UserID FROM dbo.PowiadomieniaOdbiorcy WHERE Kategoria = @kat", cn) { CommandTimeout = 5 };
            cmd.Parameters.AddWithValue("@kat", kategoria);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                if (!rd.IsDBNull(0)) set.Add(rd.GetString(0));
            lock (_cacheLock) { _cache[kategoria] = (set, DateTime.Now.Add(_cacheTtl)); }
            return set;
        }

        public static async Task EnsureSchemaAsync()
        {
            if (_initialized) return;
            await _initLock.WaitAsync();
            try
            {
                if (_initialized) return;
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                const string ddl = @"
                    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='PowiadomieniaOdbiorcy' AND type='U')
                    BEGIN
                        CREATE TABLE dbo.PowiadomieniaOdbiorcy (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Kategoria NVARCHAR(20) NOT NULL,
                            UserID NVARCHAR(50) NOT NULL,
                            UserName NVARCHAR(200) NULL,
                            DodanoPrzez NVARCHAR(50) NULL,
                            DodanoData DATETIME NOT NULL DEFAULT GETDATE()
                        );
                        CREATE UNIQUE INDEX UX_PowOdb_KatUser ON dbo.PowiadomieniaOdbiorcy(Kategoria, UserID);
                    END;";
                await using var cmd = new SqlCommand(ddl, cn);
                await cmd.ExecuteNonQueryAsync();
                _initialized = true;
            }
            finally { _initLock.Release(); }
        }

        /// <summary>Lista odbiorców danej kategorii.</summary>
        public static async Task<List<Odbiorca>> GetOdbiorcyAsync(string kategoria)
        {
            await EnsureSchemaAsync();
            var list = new List<Odbiorca>();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            const string sql = @"SELECT UserID, ISNULL(UserName,'') FROM dbo.PowiadomieniaOdbiorcy
                                 WHERE Kategoria = @kat ORDER BY UserName";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@kat", kategoria);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                list.Add(new Odbiorca { UserID = rd.GetString(0), UserName = rd.GetString(1) });
            return list;
        }

        /// <summary>Zastępuje całą listę odbiorców kategorii (delete + insert w transakcji).</summary>
        public static async Task SetOdbiorcyAsync(string kategoria, IEnumerable<Odbiorca> odbiorcy, string dodanoPrzez)
        {
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            await using var tx = cn.BeginTransaction();
            try
            {
                await using (var del = new SqlCommand("DELETE FROM dbo.PowiadomieniaOdbiorcy WHERE Kategoria = @kat", cn, tx))
                {
                    del.Parameters.AddWithValue("@kat", kategoria);
                    await del.ExecuteNonQueryAsync();
                }
                foreach (var o in odbiorcy)
                {
                    if (string.IsNullOrWhiteSpace(o.UserID)) continue;
                    await using var ins = new SqlCommand(
                        @"INSERT INTO dbo.PowiadomieniaOdbiorcy (Kategoria, UserID, UserName, DodanoPrzez)
                          VALUES (@kat, @uid, @un, @dp)", cn, tx);
                    ins.Parameters.AddWithValue("@kat", kategoria);
                    ins.Parameters.AddWithValue("@uid", o.UserID);
                    ins.Parameters.AddWithValue("@un", (object?)o.UserName ?? "");
                    ins.Parameters.AddWithValue("@dp", (object?)dodanoPrzez ?? "");
                    await ins.ExecuteNonQueryAsync();
                }
                tx.Commit();
                InvalidateCache(kategoria);
            }
            catch { tx.Rollback(); throw; }
        }

        /// <summary>
        /// Czy user jest odbiorcą danej kategorii. Pusta lista kategorii = TAK (powiadomienie dla wszystkich).
        /// </summary>
        public static async Task<bool> IsOdbiorcaAsync(string kategoria, string userId)
        {
            try
            {
                var set = await GetSetAsync(kategoria);
                if (set.Count == 0) return true;            // brak konfiguracji = wszyscy
                return set.Contains(userId ?? "");
            }
            catch { return true; }
        }

        /// <summary>Czy user jest JAWNIE na liście kategorii (bez reguły 'pusta=wszyscy') — do edytora per-user.</summary>
        public static async Task<bool> IsUserExplicitlyListedAsync(string kategoria, string userId)
        {
            try
            {
                var set = await GetSetAsync(kategoria);
                return set.Contains(userId ?? "");
            }
            catch { return false; }
        }

        /// <summary>Dodaje pojedynczego użytkownika do kategorii (idempotentnie).</summary>
        public static async Task AddOdbiorcaAsync(string kategoria, string userId, string userName, string dodanoPrzez)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM dbo.PowiadomieniaOdbiorcy WHERE Kategoria=@kat AND UserID=@uid)
                    INSERT INTO dbo.PowiadomieniaOdbiorcy (Kategoria, UserID, UserName, DodanoPrzez)
                    VALUES (@kat, @uid, @un, @dp);";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@kat", kategoria);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@un", (object?)userName ?? "");
            cmd.Parameters.AddWithValue("@dp", (object?)dodanoPrzez ?? "");
            await cmd.ExecuteNonQueryAsync();
            InvalidateCache(kategoria);
        }

        /// <summary>Usuwa pojedynczego użytkownika z kategorii.</summary>
        public static async Task RemoveOdbiorcaAsync(string kategoria, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(
                "DELETE FROM dbo.PowiadomieniaOdbiorcy WHERE Kategoria=@kat AND UserID=@uid", cn);
            cmd.Parameters.AddWithValue("@kat", kategoria);
            cmd.Parameters.AddWithValue("@uid", userId);
            await cmd.ExecuteNonQueryAsync();
            InvalidateCache(kategoria);
        }

        /// <summary>Lista wszystkich operatorów (do wyboru w UI) — z LibraNet.operators.</summary>
        public static async Task<List<Odbiorca>> GetWszyscyOperatorzyAsync()
        {
            var list = new List<Odbiorca>();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            const string sql = @"SELECT CAST(ID AS NVARCHAR(50)) AS UserID, ISNULL(Name,'') AS UserName
                                 FROM dbo.operators
                                 WHERE ISNULL(Name,'') <> ''
                                 ORDER BY Name";
            await using var cmd = new SqlCommand(sql, cn);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                list.Add(new Odbiorca { UserID = rd.GetString(0), UserName = rd.GetString(1) });
            return list;
        }
    }
}
