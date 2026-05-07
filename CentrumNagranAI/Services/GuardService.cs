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
        public bool NotifySms { get; set; } = false;
        // B1: ile kolejnych klatek musi potwierdzić zanim alert się wyzwoli (1=natychmiast).
        public int RequiredConfirmations { get; set; } = 2;
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

        // CLIP prefilter: minimalny cosine similarity między embedingiem promptu reguły
        // a embedingiem klatki, żeby reguła była w ogóle warta wysłania do VLM.
        // Wysoka wartość (np. 0.5) = oszczędniej, ale ryzyko false-negative.
        // Niska (np. 0.3) = bezpieczniej, ale więcej VLM calls.
        public const float PrefilterMinSim = 0.35f;

        // Cache: ruleId → embedding promptu (ten sam prompt, jeden embed na regułę).
        // Reset gdy reguła się zmieni (Upsert czyści wpis).
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<long, float[]> _ruleEmbeddingCache = new();

        public static List<GuardRule> GetAllRules()
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT id, name, prompt, threshold, cooldown_min, enabled, camera_filter, last_alert,
                                       COALESCE(notify_sms,0), COALESCE(required_confirmations,2)
                                FROM guard_rule ORDER BY id";
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
                    LastAlert = rdr.IsDBNull(7) ? null : DateTime.Parse(rdr.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    NotifySms = rdr.GetInt32(8) == 1,
                    RequiredConfirmations = rdr.GetInt32(9)
                });
            }
            return result;
        }

        public static long UpsertRule(GuardRule r)
        {
            // Wyczyść cache embeddingu - prompt mógł się zmienić.
            if (r.Id > 0) _ruleEmbeddingCache.TryRemove(r.Id, out _);
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            if (r.Id <= 0)
            {
                cmd.CommandText = @"
                    INSERT INTO guard_rule (name, prompt, threshold, cooldown_min, enabled, camera_filter, notify_sms, required_confirmations, created)
                    VALUES ($n, $p, $t, $c, $e, $cf, $sms, $rc, datetime('now'));
                    SELECT last_insert_rowid();";
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE guard_rule SET name=$n, prompt=$p, threshold=$t,
                        cooldown_min=$c, enabled=$e, camera_filter=$cf, notify_sms=$sms,
                        required_confirmations=$rc
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
            cmd.Parameters.AddWithValue("$sms", r.NotifySms ? 1 : 0);
            cmd.Parameters.AddWithValue("$rc", Math.Max(1, r.RequiredConfirmations));
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
        /// Sprawdź klatkę przeciwko wszystkim aktywnym regułom.
        /// CLIP prefilter: jeśli klatka ma embedding, każda reguła ma swój embed
        /// promptu, sprawdzamy cosine — VLM odpalamy TYLKO gdy similarity >= PrefilterMinSim.
        /// To redukuje VLM calls o ~90% przy 24/7 monitoring.
        /// </summary>
        public static async Task CheckFrameAgainstRulesAsync(long frameId, string cameraId, string filePath, DateTime tsUtc, CancellationToken ct = default)
        {
            var rules = GetAllRules().Where(r => r.Enabled).ToList();
            if (rules.Count == 0) return;

            // Pobierz embedding klatki (jeśli backfill już go zrobił).
            float[]? frameEmb = FrameIndex.GetEmbedding(frameId);
            bool prefilterOn = frameEmb != null && EmbeddingService.IsConfigured;

            foreach (var rule in rules)
            {
                if (ct.IsCancellationRequested) break;
                if (rule.CameraFilter != null && !rule.CameraFilter.Split(',').Contains(cameraId)) continue;

                if (rule.LastAlert.HasValue &&
                    (DateTime.UtcNow - rule.LastAlert.Value).TotalMinutes < rule.CooldownMin) continue;

                // CLIP prefilter: cosine między embed reguły a embed klatki.
                // Skip VLM jeśli za niska similarity.
                if (prefilterOn)
                {
                    try
                    {
                        var ruleEmb = await GetOrCreateRuleEmbeddingAsync(rule, ct);
                        if (ruleEmb != null)
                        {
                            float sim = EmbeddingService.Cosine(frameEmb!, ruleEmb);
                            if (sim < PrefilterMinSim)
                            {
                                // Skip - klatka nie pasuje semantycznie do reguły, oszczędzamy VLM call.
                                continue;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Guard prefilter fail rule={rule.Id}] {ex.Message}");
                        // Fallback: leci do VLM jakby prefilter nie działał.
                    }
                }

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
                        // B1: Multi-frame confirmation. Trzymamy "pending" do potwierdzenia.
                        // Wymagane RequiredConfirmations kolejnych klatek tej kamery z match score.
                        int needed = Math.Max(1, rule.RequiredConfirmations);
                        int confirmedCount = needed == 1 ? 1 : RecordPendingAndCount(rule.Id, cameraId, frameId, tsUtc, score, reason);

                        if (confirmedCount >= needed)
                        {
                            long alertId = InsertAlert(rule.Id, frameId, tsUtc, score, reason);
                            UpdateLastAlert(rule.Id, DateTime.UtcNow);
                            ClearPending(rule.Id, cameraId);
                            Log($"ALERT! reguła='{rule.Name}' score={score} kamera={cameraId} frame={frameId} (potwierdzenia: {confirmedCount}/{needed})");

                            if (rule.NotifySms)
                            {
                                try { await NotifyService.SendAlertSmsAsync(rule, cameraId, score, reason, ct); }
                                catch (Exception ex) { Log($"SMS fail rule={rule.Id}: {ex.Message}"); }
                            }
                        }
                        else
                        {
                            Log($"PENDING reguła='{rule.Name}' score={score} kamera={cameraId} ({confirmedCount}/{needed} potwierdzeń)");
                        }
                    }
                    else
                    {
                        // Klatka NIE pasuje - zerujemy pending dla tej (rule, camera).
                        ClearPending(rule.Id, cameraId);
                    }
                }
                catch (Exception ex)
                {
                    Log($"reguła '{rule.Name}' fail: {ex.Message.Split('\n')[0]}");
                }
            }
        }

        private static async Task<float[]?> GetOrCreateRuleEmbeddingAsync(GuardRule rule, CancellationToken ct)
        {
            if (_ruleEmbeddingCache.TryGetValue(rule.Id, out var cached)) return cached;

            // Embeding tekstu reguły. Robimy raz per regułę (jak prompt się zmieni → cache czyszczony przez UpsertRule).
            var emb = await EmbeddingService.EmbedAsync(rule.Prompt, ct);
            _ruleEmbeddingCache[rule.Id] = emb;
            return emb;
        }

        /// <summary>
        /// B1: zapisuje pending alert (lub aktualizuje licznik). Zwraca aktualną liczbę potwierdzeń.
        /// Stary pending wygasa po 5 minutach (bez kolejnych match'ów) - zerowany.
        /// </summary>
        private static int RecordPendingAndCount(long ruleId, string cameraId, long frameId, DateTime tsUtc, int score, string reason)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();

            int currentCount = 0;
            DateTime? lastTs = null;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT confirmations, last_ts FROM pending_alert WHERE rule_id=$r AND camera_id=$c";
                cmd.Parameters.AddWithValue("$r", ruleId);
                cmd.Parameters.AddWithValue("$c", cameraId);
                using var rdr = cmd.ExecuteReader();
                if (rdr.Read())
                {
                    currentCount = rdr.GetInt32(0);
                    lastTs = DateTime.Parse(rdr.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind);
                }
            }

            // Ekspirujemy pending jeśli > 5 min od poprzedniej match'owanej klatki
            bool expired = lastTs.HasValue && (tsUtc - lastTs.Value).TotalMinutes > 5;
            int newCount = expired ? 1 : currentCount + 1;

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO pending_alert (rule_id, camera_id, first_frame_id, first_ts, last_frame_id, last_ts, confirmations, best_score, best_reason)
                    VALUES ($r, $c, $f, $t, $f, $t, $cnt, $s, $reason)
                    ON CONFLICT(rule_id, camera_id) DO UPDATE SET
                        last_frame_id = excluded.last_frame_id,
                        last_ts = excluded.last_ts,
                        confirmations = $cnt,
                        best_score = MAX(best_score, excluded.best_score),
                        best_reason = CASE WHEN excluded.best_score > best_score THEN excluded.best_reason ELSE best_reason END;";
                cmd.Parameters.AddWithValue("$r", ruleId);
                cmd.Parameters.AddWithValue("$c", cameraId);
                cmd.Parameters.AddWithValue("$f", frameId);
                cmd.Parameters.AddWithValue("$t", tsUtc.ToString("o"));
                cmd.Parameters.AddWithValue("$cnt", newCount);
                cmd.Parameters.AddWithValue("$s", score);
                cmd.Parameters.AddWithValue("$reason", reason);
                cmd.ExecuteNonQuery();
            }
            return newCount;
        }

        private static void ClearPending(long ruleId, string cameraId)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pending_alert WHERE rule_id=$r AND camera_id=$c";
            cmd.Parameters.AddWithValue("$r", ruleId);
            cmd.Parameters.AddWithValue("$c", cameraId);
            cmd.ExecuteNonQuery();
        }

        private static long InsertAlert(long ruleId, long frameId, DateTime ts, int score, string reason)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO guard_alert (rule_id, frame_id, ts, score, reason)
                VALUES ($r, $f, $t, $s, $reason);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$r", ruleId);
            cmd.Parameters.AddWithValue("$f", frameId);
            cmd.Parameters.AddWithValue("$t", ts.ToString("o"));
            cmd.Parameters.AddWithValue("$s", score);
            cmd.Parameters.AddWithValue("$reason", reason);
            return (long)cmd.ExecuteScalar()!;
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

        /// <summary>
        /// B3: Sprawdź jak reguła zachowałaby się na ostatnich N klatkach (bez efektów ubocznych —
        /// nie zapisuje alertów, nie wysyła SMS). Zwraca statystyki + listę top match'ów do podglądu.
        /// </summary>
        public class TestResult
        {
            public int FramesChecked { get; set; }
            public int Matches { get; set; }      // ile klatek przekroczyło threshold
            public int VlmCalls { get; set; }     // ile poszło do VLM (po prefiltrze CLIP)
            public double TotalCostUsd { get; set; }
            public List<(long FrameId, string Camera, DateTime Ts, int Score, string Reason, string FilePath)> TopHits { get; set; } = new();
        }

        public static async Task<TestResult> TestRuleAsync(GuardRule rule, int sampleSize = 100, CancellationToken ct = default)
        {
            CnaConfig.ZaladujJesliTrzeba();
            FrameIndex.Init();

            var cams = rule.CameraFilter?.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var frames = FrameIndex.GetFrames(cameraIds: cams, limit: sampleSize);
            var result = new TestResult { FramesChecked = frames.Count };
            if (frames.Count == 0) return result;

            // Embedding promptu (raz)
            float[]? ruleEmb = null;
            if (EmbeddingService.IsConfigured)
            {
                try { ruleEmb = await EmbeddingService.EmbedAsync(rule.Prompt, ct); } catch { }
            }

            string vlmPrompt =
                $"Pytanie: \"{rule.Prompt}\"\n\n" +
                "Oceń 0-100 jak BARDZO BEZPOŚREDNIO ta klatka odpowiada pytaniu. " +
                "Zwróć WYŁĄCZNIE JSON: {\"score\": <0-100>, \"reason\": \"<jedno zdanie>\"}";

            var hits = new List<(long, string, DateTime, int, string, string)>();
            foreach (var f in frames)
            {
                if (ct.IsCancellationRequested) break;

                // Prefilter CLIP
                if (ruleEmb != null)
                {
                    var frameEmb = FrameIndex.GetEmbedding(f.Id);
                    if (frameEmb != null)
                    {
                        float sim = EmbeddingService.Cosine(frameEmb, ruleEmb);
                        if (sim < PrefilterMinSim) continue; // skip - nie pasuje semantycznie
                    }
                }

                try
                {
                    var vlm = await VlmClient.AnalyzeImageAsync(f.FilePath, vlmPrompt, model: VlmClient.ModelHaiku, maxTokens: 150, ct: ct);
                    result.VlmCalls++;
                    result.TotalCostUsd += vlm.CostUsd;
                    var (score, reason) = ParseJson(vlm.Text);
                    if (score >= rule.Threshold)
                    {
                        result.Matches++;
                        hits.Add((f.Id, f.CameraId, f.TsUtc, score, reason, f.FilePath));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Guard test] frame {f.Id} fail: {ex.Message}");
                }
            }

            result.TopHits = hits.OrderByDescending(h => h.Item4).Take(10).ToList();
            return result;
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
