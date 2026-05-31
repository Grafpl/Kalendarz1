using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// Faza C: chat sesyjny z pamięcią między dniami.
    /// 3-warstwowy system prompt cache'owany ephemeral (Warstwy A i B są STAŁE w ramach dnia
    /// → cache hituje przy każdej wiadomości w sesji). Warstwa C = session summaries + open questions
    /// z ostatnich sesji + bieżąca rozmowa.
    /// </summary>
    public class SmartChatService
    {
        private readonly string _connectionString;
        private readonly ClaudeAnalysisService _claude;

        public SmartChatService(string connectionString = null, ClaudeAnalysisService claude = null)
        {
            _connectionString = connectionString ?? MarketIntelligenceConfig.LibraNetConnectionString;
            _claude = claude ?? new ClaudeAnalysisService();
        }

        // ══════════════════════════ PUBLIC API ══════════════════════════

        /// <summary>Tworzy nową sesję i zwraca jej Id.</summary>
        public async Task<int> StartSessionAsync(CancellationToken ct = default)
        {
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(
                "INSERT INTO intel_ChatSessions (StartedAt) OUTPUT INSERTED.Id VALUES (SYSUTCDATETIME())", cn);
            var id = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
            Debug.WriteLine($"[SmartChat] ▶ nowa sesja #{id}");
            return id;
        }

        /// <summary>
        /// Wysyła wiadomość: ładuje historię, buduje 3 warstwy, woła Claude, zapisuje user+assistant.
        /// Zwraca tekst odpowiedzi (lub komunikat błędu w nawiasach kwadratowych).
        /// </summary>
        public async Task<string> SendMessageAsync(int sessionId, string userMessage, CancellationToken ct = default)
        {
            if (sessionId <= 0 || string.IsNullOrWhiteSpace(userMessage)) return "[Puste pytanie lub brak sesji]";

            try
            {
                var history = await LoadHistoryAsync(sessionId, ct);
                history.Add(("user", userMessage));

                var layerA = await BuildLayerAAsync(ct);
                var layerB = await BuildLayerBAsync(ct);
                var layerC = await BuildLayerCAsync(sessionId, ct);

                var result = await _claude.ChatLayeredAsync(layerA, layerB, layerC, history, ct);

                // Zapisz user + assistant w jednej transakcji koncepcyjnej (dwa INSERT-y).
                await SaveMessageAsync(sessionId, "user", userMessage, null, null, null, null, ct);
                await SaveMessageAsync(sessionId, "assistant", result.Text,
                    result.InputTokens, result.OutputTokens, result.CacheReadTokens, null, ct);
                await IncrementMessageCountAsync(sessionId, 2, ct);

                return result.Text;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SmartChat] SendMessage error: {ex.Message}");
                return $"[Błąd: {ex.Message}]";
            }
        }

        /// <summary>
        /// Zamyka sesję: Haiku generuje summary + key topics + open questions, zapisuje do intel_ChatSessions.
        /// Sesje z 0 wiadomości są usuwane (sprzątanie pustych sesji utworzonych przez StartSession bez wiadomości).
        /// </summary>
        public async Task EndSessionAsync(int sessionId, CancellationToken ct = default)
        {
            if (sessionId <= 0) return;
            try
            {
                var messages = await LoadHistoryAsync(sessionId, ct);
                if (messages.Count == 0)
                {
                    // pusta sesja → usuń (cascade usunie ewentualne wiadomości)
                    await DeleteSessionAsync(sessionId, ct);
                    Debug.WriteLine($"[SmartChat] ⏹ sesja #{sessionId} pusta — usunięta");
                    return;
                }

                var (summary, keyTopics, openQuestions) = await GenerateSessionSummaryAsync(messages, ct);
                using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync(ct);
                using var cmd = new SqlCommand(@"
UPDATE intel_ChatSessions
SET EndedAt = SYSUTCDATETIME(),
    Summary = @sum,
    KeyTopics = @keys,
    OpenQuestions = @open
WHERE Id = @s", cn) { CommandTimeout = 15 };
                cmd.Parameters.AddWithValue("@sum", (object)summary ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@keys", (object)keyTopics ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@open", (object)openQuestions ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@s", sessionId);
                await cmd.ExecuteNonQueryAsync(ct);
                Debug.WriteLine($"[SmartChat] ⏹ sesja #{sessionId} zamknięta z summary ({messages.Count} wiad.)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SmartChat] EndSession error: {ex.Message}");
            }
        }

        // ══════════════════════════ LAYER BUILDERS ══════════════════════════

        /// <summary>
        /// Warstwa A — profil firmy + tracked entities + klienci HANDEL.
        /// STAŁA w ramach dnia → cache hit między wiadomościami i sesjami.
        /// </summary>
        private async Task<string> BuildLayerAAsync(CancellationToken ct)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Jesteś osobistym asystentem AI Sergiusza Piórkowskiego — właściciela Ubojni Drobiu Piórkowscy.");
            sb.AppendLine();
            sb.AppendLine("## PROFIL FIRMY");
            sb.AppendLine("- Nazwa: Ubojnia Drobiu Piórkowscy sp. z o.o. (wewn. ZPSP)");
            sb.AppendLine("- Obrót: ~258 mln zł, produkcja: 200 t/dzień");
            sb.AppendLine("- Lokalizacja: Brzeziny, łódzkie");
            sb.AppendLine("- Klienci kluczowi: Biedronka, Auchan, Carrefour, Makro, Selgros, Dino, Lidl, Kaufland");
            sb.AppendLine("- Zagrożenia: Cedrob (~30-40% rynku, możliwe przejęcie przez ADQ), import Brazylia (BRF/JBS), MHP Ukraina, HPAI łódzkie");
            sb.AppendLine("- Stan: kryzys, sprzedaż -40% YoY");
            sb.AppendLine();

            // Tracked entities (z intel_Entities)
            sb.AppendLine("## ŚLEDZONE PODMIOTY (encje w bazie)");
            try
            {
                using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync(ct);
                using var cmd = new SqlCommand(@"
SELECT Name, EntityType, ISNULL(Notes,'')
FROM intel_Entities WHERE IsTracked=1
ORDER BY EntityType, Name", cn) { CommandTimeout = 10 };
                using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var notes = r.GetString(2);
                    sb.AppendLine($"- [{r.GetString(1)}] {r.GetString(0)}{(notes.Length > 0 ? " — " + notes : "")}");
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[SmartChat/A] entities: {ex.Message}"); }

            sb.AppendLine();
            sb.AppendLine("## ZASADY ODPOWIEDZI");
            sb.AppendLine("- Po polsku, konkretnie, biznesowo. Bez lania wody.");
            sb.AppendLine("- Używaj liczb z bazy (Warstwa B). Nie zgaduj.");
            sb.AppendLine("- Jeśli czegoś brakuje w bazie — powiedz to wprost.");
            sb.AppendLine("- Pamiętaj o poprzednich sesjach (Warstwa C).");

            return sb.ToString();
        }

        /// <summary>
        /// Warstwa B — top 15 aktywnych wątków + top trend datapoints (ostatnie 7 dni).
        /// STAŁA w ramach dnia → cache hit.
        /// </summary>
        private async Task<string> BuildLayerBAsync(CancellationToken ct)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## TOP AKTYWNE WĄTKI (z intel_Stories)");
            try
            {
                using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync(ct);
                using var cmd = new SqlCommand(@"
SELECT TOP 15 Title, StoryType, Severity, PoultryRelevance, ArticleCount,
       ISNULL(LastDigest,''), ISNULL(BusinessImpact,'')
FROM intel_Stories
WHERE Status IN ('developing','stable')
ORDER BY (Severity * PoultryRelevance) DESC, LastUpdatedAt DESC", cn) { CommandTimeout = 15 };
                using var r = await cmd.ExecuteReaderAsync(ct);
                int i = 1;
                while (await r.ReadAsync(ct))
                {
                    var digest = r.GetString(5);
                    var impact = r.GetString(6);
                    if (digest.Length > 400) digest = digest.Substring(0, 400) + "...";
                    sb.AppendLine($"### {i++}. [{r.GetString(1)} · sev={r.GetInt32(2)}/5 · rel={r.GetInt32(3)}/5 · {r.GetInt32(4)} art.] {r.GetString(0)}");
                    if (digest.Length > 0) sb.AppendLine(digest);
                    if (impact.Length > 0) sb.AppendLine($"💼 IMPACT: {impact}");
                    sb.AppendLine();
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[SmartChat/B] stories: {ex.Message}"); }

            // Trendy — ostatni punkt każdej metryki + delta 7 dni
            sb.AppendLine("## TRENDY (intel_TrendDataPoints, ostatnie 7 dni)");
            try
            {
                using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync(ct);
                using var cmd = new SqlCommand(@"
SELECT MetricKey,
       (SELECT TOP 1 Value FROM intel_TrendDataPoints WHERE MetricKey=t.MetricKey ORDER BY SnapshotDate DESC) AS Latest,
       (SELECT TOP 1 Value FROM intel_TrendDataPoints WHERE MetricKey=t.MetricKey AND SnapshotDate <= DATEADD(day,-7,CAST(GETDATE() AS DATE)) ORDER BY SnapshotDate DESC) AS Weekly,
       MAX(SnapshotDate) AS LastDate
FROM intel_TrendDataPoints t
WHERE SnapshotDate >= DATEADD(day,-30,CAST(GETDATE() AS DATE))
GROUP BY MetricKey
ORDER BY MetricKey", cn) { CommandTimeout = 15 };
                using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var key = r.GetString(0);
                    var latest = r.GetDecimal(1);
                    var weekly = r.IsDBNull(2) ? (decimal?)null : r.GetDecimal(2);
                    var deltaStr = "";
                    if (weekly.HasValue && weekly.Value != 0)
                    {
                        var delta = (latest - weekly.Value) / weekly.Value * 100m;
                        deltaStr = $" ({(delta >= 0 ? "+" : "")}{delta:0.#}% / 7d)";
                    }
                    sb.AppendLine($"- {key} = {latest:0.##}{deltaStr} (na {r.GetDateTime(3):yyyy-MM-dd})");
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[SmartChat/B] trends: {ex.Message}"); }

            return sb.ToString();
        }

        /// <summary>
        /// Warstwa C — pamięć między sesjami: ostatnie 3 summaries + open questions.
        /// NIE cache'owana (zmienia się po każdej zakończonej sesji).
        /// </summary>
        private async Task<string> BuildLayerCAsync(int currentSessionId, CancellationToken ct)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## PAMIĘĆ Z POPRZEDNICH SESJI (ostatnie 3, zamknięte)");
            try
            {
                using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync(ct);
                using var cmd = new SqlCommand(@"
SELECT TOP 3 StartedAt, ISNULL(Summary,''), ISNULL(KeyTopics,''), ISNULL(OpenQuestions,'')
FROM intel_ChatSessions
WHERE EndedAt IS NOT NULL AND Id <> @cur
ORDER BY EndedAt DESC", cn) { CommandTimeout = 10 };
                cmd.Parameters.AddWithValue("@cur", currentSessionId);
                using var r = await cmd.ExecuteReaderAsync(ct);
                bool any = false;
                while (await r.ReadAsync(ct))
                {
                    any = true;
                    var date = r.GetDateTime(0);
                    var summary = r.GetString(1);
                    var topics = r.GetString(2);
                    var openQ = r.GetString(3);
                    sb.AppendLine($"### Sesja {date:yyyy-MM-dd HH:mm}");
                    if (summary.Length > 0) sb.AppendLine(summary);
                    if (topics.Length > 0) sb.AppendLine($"Tematy: {topics}");
                    if (openQ.Length > 0) sb.AppendLine($"⚠ Otwarte: {openQ}");
                    sb.AppendLine();
                }
                if (!any) sb.AppendLine("(brak — to pierwsza rozmowa)");
            }
            catch (Exception ex) { Debug.WriteLine($"[SmartChat/C] prev sessions: {ex.Message}"); }

            sb.AppendLine();
            sb.AppendLine($"Aktualna data: {DateTime.Today:yyyy-MM-dd, dddd}");
            return sb.ToString();
        }

        // ══════════════════════════ SESSION SUMMARY (Haiku) ══════════════════════════

        private async Task<(string Summary, string KeyTopics, string OpenQuestions)> GenerateSessionSummaryAsync(
            List<(string Role, string Content)> messages, CancellationToken ct)
        {
            var transcript = new StringBuilder();
            foreach (var (role, content) in messages)
            {
                var c = content.Length > 600 ? content.Substring(0, 600) + "..." : content;
                transcript.AppendLine($"[{role}] {c}");
            }

            var systemPrompt =
@"Streszczasz sesję chatu CEO ubojni drobiu. Odpowiedz TYLKO JSON-em:
{
  ""summary"": ""5-7 zdaniowy abstrakt PL — o czym była rozmowa, jakie wnioski"",
  ""keyTopics"": [""temat1"", ""temat2"", ...],  // max 5
  ""openQuestions"": ""niedokończone wątki — co warto dopytać następnym razem (1-2 zdania, lub pusty string)""
}";

            var userPrompt = "TRANSKRYPT:\n" + transcript.ToString();
            var raw = await _claude.CompleteAsync(systemPrompt, userPrompt, useHaiku: true, maxTokens: 800, ct: ct);
            if (string.IsNullOrWhiteSpace(raw)) return ("", "", "");

            try
            {
                var start = raw.IndexOf('{');
                var end = raw.LastIndexOf('}');
                if (start < 0 || end <= start) return ("", "", "");
                using var doc = JsonDocument.Parse(raw.Substring(start, end - start + 1));
                var root = doc.RootElement;
                var summary = root.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : "";
                string keyTopics = "";
                if (root.TryGetProperty("keyTopics", out var k) && k.ValueKind == JsonValueKind.Array)
                    keyTopics = JsonSerializer.Serialize(k);
                var openQ = root.TryGetProperty("openQuestions", out var o) && o.ValueKind == JsonValueKind.String ? o.GetString() : "";
                return (summary ?? "", keyTopics, openQ ?? "");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SmartChat] summary JSON parse: {ex.Message}");
                return ("", "", "");
            }
        }

        // ══════════════════════════ DB HELPERS ══════════════════════════

        private async Task<List<(string Role, string Content)>> LoadHistoryAsync(int sessionId, CancellationToken ct)
        {
            var list = new List<(string, string)>();
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(
                "SELECT Role, Content FROM intel_ChatMessages WHERE SessionId=@s ORDER BY SentAt ASC", cn) { CommandTimeout = 15 };
            cmd.Parameters.AddWithValue("@s", sessionId);
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add((r.GetString(0), r.GetString(1)));
            return list;
        }

        private async Task SaveMessageAsync(int sessionId, string role, string content,
            int? inTok, int? outTok, int? cacheRead, string refIds, CancellationToken ct)
        {
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(@"
INSERT INTO intel_ChatMessages (SessionId, Role, Content, InputTokens, OutputTokens, CacheReadTokens, ReferencedStoryIds)
VALUES (@s, @r, @c, @in, @out, @cr, @ref)", cn) { CommandTimeout = 15 };
            cmd.Parameters.AddWithValue("@s", sessionId);
            cmd.Parameters.AddWithValue("@r", role);
            cmd.Parameters.AddWithValue("@c", content ?? "");
            cmd.Parameters.AddWithValue("@in", (object)inTok ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@out", (object)outTok ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cr", (object)cacheRead ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ref", (object)refIds ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task IncrementMessageCountAsync(int sessionId, int delta, CancellationToken ct)
        {
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(
                "UPDATE intel_ChatSessions SET MessageCount = MessageCount + @d WHERE Id=@s", cn) { CommandTimeout = 10 };
            cmd.Parameters.AddWithValue("@d", delta);
            cmd.Parameters.AddWithValue("@s", sessionId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task DeleteSessionAsync(int sessionId, CancellationToken ct)
        {
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand("DELETE FROM intel_ChatSessions WHERE Id=@s", cn) { CommandTimeout = 10 };
            cmd.Parameters.AddWithValue("@s", sessionId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
