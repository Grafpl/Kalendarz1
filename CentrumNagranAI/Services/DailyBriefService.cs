using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Codzienny AI brief - syntetyczne podsumowanie dnia w zakładzie.
    /// Bierze losowe N klatek z dnia, każdą krótko opisuje (już ma caption!),
    /// agreguje + Sonnet generuje synthesis "co działo się dziś".
    ///
    /// Koszt: bardzo niski bo używamy gotowych captionów + tylko 1 call do Sonnet
    /// na agregację (~$0.05 za brief). Wywołanie manualne lub timer codziennie 17:00.
    /// </summary>
    public static class DailyBriefService
    {
        private static string ConnString =>
            $"Data Source={CnaConfig.DbPath};Cache=Shared;Foreign Keys=True";

        public class Brief
        {
            public long Id { get; set; }
            public string Day { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public List<long> SampleFrameIds { get; set; } = new();
            public double CostUsd { get; set; }
            public DateTime Created { get; set; }
        }

        public static async Task<Brief> GenerateAsync(DateTime? dayLocal = null, int sampleSize = 80, CancellationToken ct = default)
        {
            CnaConfig.ZaladujJesliTrzeba();
            FrameIndex.Init();

            var day = (dayLocal ?? DateTime.Now).Date;
            var dayKey = day.ToString("yyyy-MM-dd");
            var fromUtc = day.ToUniversalTime();
            var toUtc = day.AddDays(1).ToUniversalTime();

            // Pobierz captions z bazy (już mamy z backfill).
            var captions = new List<(long Id, string Camera, DateTime Ts, string Caption)>();
            using (var conn = new SqliteConnection(ConnString))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT f.id, f.camera_id, f.ts, fc.caption
                    FROM frame f
                    INNER JOIN frame_caption fc ON fc.frame_id = f.id
                    WHERE f.ts >= $from AND f.ts < $to
                    ORDER BY f.ts";
                cmd.Parameters.AddWithValue("$from", fromUtc.ToString("o"));
                cmd.Parameters.AddWithValue("$to", toUtc.ToString("o"));
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    captions.Add((
                        rdr.GetInt64(0),
                        rdr.GetString(1),
                        DateTime.Parse(rdr.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                        rdr.GetString(3)
                    ));
                }
            }

            if (captions.Count == 0)
            {
                return new Brief { Day = dayKey, Summary = "Brak klatek z tego dnia (lub captions jeszcze niegotowe)." };
            }

            // Sample N klatek (równomiernie po godzinach).
            var sampled = SampleEvenly(captions, sampleSize);

            // Zbuduj kontekst dla Sonnet
            var sb = new StringBuilder();
            sb.AppendLine("LISTA OPISÓW KLATEK Z KAMER (próbka dnia):");
            foreach (var c in sampled)
            {
                sb.AppendLine($"[{c.Ts.ToLocalTime():HH:mm} {CnaConfig.DisplayName(c.Camera)}] {c.Caption}");
            }
            sb.AppendLine();
            sb.AppendLine("===== ZADANIE =====");
            sb.AppendLine($"Napisz krótkie podsumowanie dnia {dayKey} w zakładzie drobiarskim Piórkowscy " +
                          "na podstawie powyższych opisów klatek. 5-8 zdań po polsku, konkretnie:");
            sb.AppendLine("- Jakie obszary były aktywne (pakowalnia, hala uboju, rampa, magazyn)?");
            sb.AppendLine("- Czy zauważasz wzorce godzinowe?");
            sb.AppendLine("- Czy są anomalie/odstępstwa?");
            sb.AppendLine("- Czy widać ludzi, pojazdy, ich ruch?");
            sb.AppendLine();
            sb.AppendLine("Bez disclaimerów typu 'na podstawie opisów'. Pisz jak raport operacyjny.");

            // Wysłanie - używamy Sonnet bo to syntetyzacja (lepszy model niż Haiku do tekstu).
            // Tu dziwna sprawa - VlmClient.AnalyzeImageAsync wymaga obrazu. Robimy manualne wywołanie.
            var result = await TextOnlyClaudeAsync(sb.ToString(), VlmClient.ModelSonnet, ct);

            // Zapisz brief
            using (var conn = new SqliteConnection(ConnString))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO daily_brief (day, summary, sample_frame_ids, cost_usd, created)
                    VALUES ($d, $s, $ids, $cost, datetime('now'))
                    ON CONFLICT(day) DO UPDATE SET
                        summary = excluded.summary,
                        sample_frame_ids = excluded.sample_frame_ids,
                        cost_usd = excluded.cost_usd,
                        created = excluded.created;";
                cmd.Parameters.AddWithValue("$d", dayKey);
                cmd.Parameters.AddWithValue("$s", result.Text);
                cmd.Parameters.AddWithValue("$ids", string.Join(",", sampled.Select(s => s.Id)));
                cmd.Parameters.AddWithValue("$cost", result.CostUsd);
                cmd.ExecuteNonQuery();
            }

            Log($"Brief {dayKey} wygenerowany: {captions.Count} klatek, próbka {sampled.Count}, koszt ${result.CostUsd:F4}");

            return new Brief
            {
                Day = dayKey,
                Summary = result.Text,
                SampleFrameIds = sampled.Select(s => s.Id).ToList(),
                CostUsd = result.CostUsd,
                Created = DateTime.UtcNow
            };
        }

        public static List<Brief> GetRecent(int limit = 30)
        {
            using var conn = new SqliteConnection(ConnString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, day, summary, sample_frame_ids, cost_usd, created FROM daily_brief ORDER BY day DESC LIMIT $lim";
            cmd.Parameters.AddWithValue("$lim", limit);
            var result = new List<Brief>();
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                result.Add(new Brief
                {
                    Id = rdr.GetInt64(0),
                    Day = rdr.GetString(1),
                    Summary = rdr.GetString(2),
                    SampleFrameIds = rdr.IsDBNull(3) ? new List<long>() :
                        rdr.GetString(3).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(long.Parse).ToList(),
                    CostUsd = rdr.IsDBNull(4) ? 0 : rdr.GetDouble(4),
                    Created = DateTime.Parse(rdr.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind)
                });
            }
            return result;
        }

        // ───── Helpers ─────

        private static List<(long Id, string Camera, DateTime Ts, string Caption)> SampleEvenly(
            List<(long Id, string Camera, DateTime Ts, string Caption)> all, int n)
        {
            if (all.Count <= n) return all;
            var result = new List<(long, string, DateTime, string)>(n);
            double step = (double)all.Count / n;
            for (int i = 0; i < n; i++)
            {
                int idx = (int)(i * step);
                if (idx >= all.Count) idx = all.Count - 1;
                result.Add(all[idx]);
            }
            return result;
        }

        private static async Task<VlmClient.VlmResult> TextOnlyClaudeAsync(string text, string model, CancellationToken ct)
        {
            // Trick: zostawiamy VlmClient pełną logikę nagłówków/auth, ale wysyłamy
            // bez obrazu. Reuse - pisze się analogiczny request body, ale tu kopia bo VlmClient
            // ma tylko ImageAnalyze.
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            http.DefaultRequestHeaders.Add("x-api-key", CnaConfig.AnthropicApiKey);
            http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var body = new
            {
                model = model,
                max_tokens = 800,
                messages = new[] { new { role = "user", content = text } }
            };
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
            using var content = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");
            var sw = Stopwatch.StartNew();
            using var resp = await http.PostAsync("https://api.anthropic.com/v1/messages", content, ct);
            string respText = await resp.Content.ReadAsStringAsync(ct);
            sw.Stop();
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Claude HTTP {(int)resp.StatusCode}: {respText.Substring(0, Math.Min(300, respText.Length))}");

            var jo = Newtonsoft.Json.Linq.JObject.Parse(respText);
            string outText = (string?)jo["content"]?[0]?["text"] ?? string.Empty;
            int inTok = (int?)jo["usage"]?["input_tokens"] ?? 0;
            int outTok = (int?)jo["usage"]?["output_tokens"] ?? 0;
            double inP = model.Contains("haiku") ? 1.0 : 3.0;
            double outP = model.Contains("haiku") ? 5.0 : 15.0;
            return new VlmClient.VlmResult
            {
                Text = outText,
                InputTokens = inTok,
                OutputTokens = outTok,
                DurationMs = sw.ElapsedMilliseconds,
                CostUsd = (inTok * inP + outTok * outP) / 1_000_000.0
            };
        }

        private static void Log(string msg)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff} [CNA-Brief] {msg}";
            Debug.WriteLine(line);
            try
            {
                Directory.CreateDirectory(CnaConfig.AuditDir);
                File.AppendAllText(Path.Combine(CnaConfig.AuditDir, "cna_brief.log"),
                    line + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}
