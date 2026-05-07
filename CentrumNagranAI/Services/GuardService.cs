using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Strażnik AI - reguły wpisane przez użytkownika. Po każdej nowej klatce
    /// VLM ocenia czy spełnia regułę. Match → alert (z cooldownem żeby nie spamować).
    ///
    /// Optymalizacja: jeśli klatka ma już embedding, najpierw KNN porównanie z
    /// embedingiem promptu reguły. Tylko top-podobne idą do VLM.
    /// </summary>
    public class GuardRule
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public int Threshold { get; set; } = 70;
        public int CooldownMin { get; set; } = 10;
        public bool Enabled { get; set; } = true;
        public string? CameraFilter { get; set; }
        public DateTime? LastAlert { get; set; }
    }

    public class GuardAlert
    {
        public long Id { get; set; }
        public long RuleId { get; set; }
        public long FrameId { get; set; }
        public DateTime TsUtc { get; set; }
        public int Score { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public string CameraId { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }

    public static class GuardService
    {
        private static string ConnString =>
            $"Data Source={CnaConfig.DbPath};Cache=Shared;Foreign Keys=True";

        public static List<GuardRule> GetAllRules()
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, prompt, threshold, cooldown_min, enabled, camera_filter, last_alert FROM guard_rule ORDER BY id";
            var result = new List<GuardRule>();
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                result.Add(new GuardRule
                {
                    Id = rdr.GetInt64(0),
                    Name = rdr.GetString(1),
                    Prompt = rdr.GetString(2),
                    Threshold = rdr.GetInt32(3),
                    CooldownMin = rdr.GetInt32(4),
                    Enabled = rdr.GetInt32(5) == 1,
                    CameraFilter = rdr.IsDBNull(6) ? null : rdr.GetString(6),
                    LastAlert = rdr.IsDBNull(7) ? null : DateTime.Parse(rdr.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind)
                });
            }
            return result;
        }

        public static long UpsertRule(GuardRule r)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            if (r.Id <= 0)
            {
                cmd.CommandText = @"
                    INSERT INTO guard_rule (name, prompt, threshold, cooldown_min, enabled, camera_filter, created)
                    VALUES ($n, $p, $t, $c, $e, $cf, datetime('now'));
                    SELECT last_insert_rowid();";
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE guard_rule SET name=$n, prompt=$p, threshold=$t,
                        cooldown_min=$c, enabled=$e, camera_filter=$cf
                    WHERE id=$id;
                    SELECT $id;";
                cmd.Parameters.AddWithValue("$id", r.Id);
            }
            cmd.Parameters.AddWithValue("$n", r.Name);
            cmd.Parameters.AddWithValue("$p", r.Prompt);
            cmd.Parameters.AddWithValue("$t", r.Threshold);
            cmd.Parameters.AddWithValue("$c", r.CooldownMin);
            cmd.Parameters.AddWithValue("$e", r.Enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("$cf", (object?)r.CameraFilter ?? DBNull.Value);
            return (long)cmd.ExecuteScalar()!;
        }

        public static void DeleteRule(long id)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM guard_rule WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public static List<GuardAlert> GetRecentAlerts(int limit = 50)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT a.id, a.rule_id, a.frame_id, a.ts, a.score, a.reason,
                       r.name, f.camera_id, f.file_path
                FROM guard_alert a
                INNER JOIN guard_rule r ON r.id = a.rule_id
                INNER JOIN frame f ON f.id = a.frame_id
                ORDER BY a.ts DESC
                LIMIT $lim";
            cmd.Parameters.AddWithValue("$lim", limit);
            var result = new List<GuardAlert>();
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                result.Add(new GuardAlert
                {
                    Id = rdr.GetInt64(0),
                    RuleId = rdr.GetInt64(1),
                    FrameId = rdr.GetInt64(2),
                    TsUtc = DateTime.Parse(rdr.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    Score = rdr.GetInt32(4),
                    Reason = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5),
                    RuleName = rdr.GetString(6),
                    CameraId = rdr.GetString(7),
                    FilePath = rdr.GetString(8)
                });
            }
            return result;
        }

        /// <summary>
        /// Sprawdź klatkę przeciwko wszystkim aktywnym regułom. Wywoływane z indexera
        /// po zapisaniu klatki + jej captionu/embedingu (żeby móc użyć KNN prefiltera).
        /// </summary>
        public static async Task CheckFrameAgainstRulesAsync(long frameId, string cameraId, string filePath, DateTime tsUtc, CancellationToken ct = default)
        {
            var rules = GetAllRules().Where(r => r.Enabled).ToList();
            if (rules.Count == 0) return;

            foreach (var rule in rules)
            {
                if (ct.IsCancellationRequested) break;
                if (rule.CameraFilter != null && !rule.CameraFilter.Split(',').Contains(cameraId)) continue;

                // Cooldown: jeśli ta reguła już alertowała w ciągu cooldown_min, skip.
                if (rule.LastAlert.HasValue &&
                    (DateTime.UtcNow - rule.LastAlert.Value).TotalMinutes < rule.CooldownMin) continue;

                try
                {
                    string prompt =
                        $"Pytanie: \"{rule.Prompt}\"\n\n" +
                        "Oceń 0-100 jak BARDZO BEZPOŚREDNIO ta klatka odpowiada pytaniu. " +
                        "Bądź surowy, nie dawaj wysokich punktów za luźne skojarzenia. " +
                        "Zwróć WYŁĄCZNIE JSON: {\"score\": <0-100>, \"reason\": \"<jedno zdanie>\"}";

                    var vlm = await VlmClient.AnalyzeImageAsync(filePath, prompt, model: VlmClient.ModelHaiku, maxTokens: 150, ct: ct);
                    var (score, reason) = ParseJson(vlm.Text);

                    if (score >= rule.Threshold)
                    {
                        InsertAlert(rule.Id, frameId, tsUtc, score, reason);
                        UpdateLastAlert(rule.Id, DateTime.UtcNow);
                        Log($"ALERT! reguła='{rule.Name}' score={score} kamera={cameraId} frame={frameId}");
                        // TODO: tu można dorzucić Twilio SMS / email. Na razie tylko log + DB.
                    }
                }
                catch (Exception ex)
                {
                    Log($"reguła '{rule.Name}' fail: {ex.Message.Split('\n')[0]}");
                }
            }
        }

        private static void InsertAlert(long ruleId, long frameId, DateTime ts, int score, string reason)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO guard_alert (rule_id, frame_id, ts, score, reason)
                VALUES ($r, $f, $t, $s, $reason)";
            cmd.Parameters.AddWithValue("$r", ruleId);
            cmd.Parameters.AddWithValue("$f", frameId);
            cmd.Parameters.AddWithValue("$t", ts.ToString("o"));
            cmd.Parameters.AddWithValue("$s", score);
            cmd.Parameters.AddWithValue("$reason", reason);
            cmd.ExecuteNonQuery();
        }

        private static void UpdateLastAlert(long ruleId, DateTime t)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE guard_rule SET last_alert=$t WHERE id=$r";
            cmd.Parameters.AddWithValue("$r", ruleId);
            cmd.Parameters.AddWithValue("$t", t.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        private static (int score, string reason) ParseJson(string text)
        {
            try
            {
                var m = System.Text.RegularExpressions.Regex.Match(text, @"\{[^{}]*""score""[^{}]*\}", System.Text.RegularExpressions.RegexOptions.Singleline);
                if (!m.Success) return (0, "no JSON");
                var jo = JObject.Parse(m.Value);
                int s = (int?)jo["score"] ?? 0;
                if (s < 0) s = 0; if (s > 100) s = 100;
                return (s, (string?)jo["reason"] ?? "");
            }
            catch { return (0, "parse fail"); }
        }

        private static void Log(string msg)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff} [CNA-Guard] {msg}";
            Debug.WriteLine(line);
            try
            {
                Directory.CreateDirectory(CnaConfig.AuditDir);
                File.AppendAllText(Path.Combine(CnaConfig.AuditDir, "cna_guard.log"),
                    line + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}
