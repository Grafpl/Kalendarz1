using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Kalendarz1.Customer360.Services
{
    /// <summary>
    /// Cache scoringu klienta: pamięć (sesja) → tabela LibraNet (TTL) → przelicz.
    /// Scoring zależy od KPI, więc samo LICZENIE dostarcza caller przez callback `oblicz`
    /// (ma dostęp do już-pobranego KlientKpi). Tu tylko cache + zapis.
    /// Działa też BEZ tabeli Customer360_ScoreCache (degraduje: tylko cache w pamięci).
    /// </summary>
    public static class Customer360ScoringService
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public const int ScoreCacheTtlDays = 7;

        private static readonly ConcurrentDictionary<int, (Customer360Score score, DateTime expiry)> _mem = new();

        /// <summary>Pobiera scoring z cache (pamięć→DB) lub liczy przez callback i zapisuje.</summary>
        public static async Task<Customer360Score> PobierzLubObliczAsync(int klientId, bool forceRefresh, Func<Task<Customer360Score>> oblicz)
        {
            if (!forceRefresh)
            {
                // 1) pamięć
                if (_mem.TryGetValue(klientId, out var wpis) && wpis.expiry > DateTime.Now)
                    return wpis.score;
                // 2) DB
                var zBazy = await CzytajZBazyAsync(klientId);
                if (zBazy != null)
                {
                    _mem[klientId] = (zBazy, DateTime.Now.AddHours(1)); // pamięć krócej niż DB
                    return zBazy;
                }
            }

            // 3) przelicz + zapisz
            var score = await oblicz();
            _mem[klientId] = (score, DateTime.Now.AddHours(1));
            await ZapiszDoBazyAsync(klientId, score);
            return score;
        }

        public static void InvalidateScore(int klientId) => _mem.TryRemove(klientId, out _);
        public static void InvalidateAll() => _mem.Clear();

        /// <summary>Wygaś CAŁY cache (pamięć + DB) — po zmianie parametrów scoringu wszystkie wyniki są nieaktualne.</summary>
        public static async Task WygasCalyCacheAsync()
        {
            _mem.Clear();
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    "UPDATE dbo.Customer360_ScoreCache SET ValidUntil = DATEADD(DAY,-1,GETDATE())", cn) { CommandTimeout = 8 };
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 score cache wygas] " + ex.Message); }
        }

        private static async Task<Customer360Score?> CzytajZBazyAsync(int klientId)
        {
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    "SELECT ScoreJson FROM dbo.Customer360_ScoreCache WHERE KlientId=@id AND ValidUntil > GETDATE()", cn) { CommandTimeout = 5 };
                cmd.Parameters.AddWithValue("@id", klientId);
                var r = await cmd.ExecuteScalarAsync();
                if (r != null && r != DBNull.Value)
                    return JsonConvert.DeserializeObject<Customer360Score>(r.ToString()!);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 score cache read] " + ex.Message); }
            return null;
        }

        private static async Task ZapiszDoBazyAsync(int klientId, Customer360Score score)
        {
            try
            {
                string json = JsonConvert.SerializeObject(score);
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                const string sql = @"
                    IF EXISTS (SELECT 1 FROM dbo.Customer360_ScoreCache WHERE KlientId=@id)
                        UPDATE dbo.Customer360_ScoreCache
                           SET ScoreJson=@j, ScoreLetter=@l, ValidUntil=DATEADD(DAY,@ttl,GETDATE()), CreatedAt=GETDATE()
                         WHERE KlientId=@id;
                    ELSE
                        INSERT INTO dbo.Customer360_ScoreCache (KlientId, ScoreJson, ScoreLetter, ValidUntil, CreatedAt)
                        VALUES (@id, @j, @l, DATEADD(DAY,@ttl,GETDATE()), GETDATE());";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 5 };
                cmd.Parameters.AddWithValue("@id", klientId);
                cmd.Parameters.AddWithValue("@j", json);
                cmd.Parameters.AddWithValue("@l", string.IsNullOrEmpty(score.Litera) ? "?" : score.Litera.Substring(0, 1));
                cmd.Parameters.AddWithValue("@ttl", ScoreCacheTtlDays);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 score cache write] " + ex.Message); }
        }
    }
}
