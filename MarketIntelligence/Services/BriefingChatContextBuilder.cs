using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Buduje system prompt dla chatu w One-pagerze.
    /// Zawiera: profil firmy + ostatnie 30 dni intel_Articles + ostatnie 7 dni intel_DailySummary
    /// + listę intel_UserQueries (co śledzi user) + preferencje (intel_ArticleFeedback agregat).
    /// Cache'owany przez Anthropic ephemeral cache → 90% rabat na powtarzane pytania.
    /// </summary>
    public class BriefingChatContextBuilder
    {
        private readonly string _connectionString;

        public BriefingChatContextBuilder(string connectionString = null)
        {
            _connectionString = connectionString ?? MarketIntelligenceConfig.LibraNetConnectionString;
        }

        public async Task<string> BuildSystemPromptAsync(string userId)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Jesteś osobistym asystentem AI Sergiusza Piórkowskiego — właściciela Ubojni Drobiu Piórkowscy.");
            sb.AppendLine();
            sb.AppendLine("## PROFIL FIRMY");
            sb.AppendLine("- **Nazwa:** Ubojnia Drobiu Piórkowscy sp. z o.o. (ZPSP)");
            sb.AppendLine("- **Obrót roczny:** ~258 mln zł");
            sb.AppendLine("- **Produkcja:** 200 ton/dzień");
            sb.AppendLine("- **Lokalizacja:** Brzeziny, łódzkie");
            sb.AppendLine("- **Stan obecny:** kryzys — sprzedaż -40% YoY (sytuacja przy okazji rozmów)");
            sb.AppendLine("- **Klienci kluczowi:** Biedronka DC (~380 palet/mies), Auchan, Carrefour, Selgros, Makro");
            sb.AppendLine("- **Zagrożenia konkurencyjne:** Cedrob (~40% rynku, możliwe przejęcie przez ADQ), Drosed, import z Brazylii (BRF/JBS), MHP Ukraina");
            sb.AppendLine("- **System:** Sage Symfonia HANDEL + LibraNet (wagi) + UNICARD RCP + własny Kalendarz1/ZPSP");
            sb.AppendLine();

            sb.AppendLine("## TWOJA ROLA");
            sb.AppendLine("- Odpowiadaj **konkretnie, krótko, biznesowo** — Sergiusz jest praktyczny, nie lubi lania wody");
            sb.AppendLine("- Mów PO POLSKU, używaj liczb gdy są dostępne, daj zawsze konkretne rekomendacje");
            sb.AppendLine("- Możesz odwoływać się do KONKRETNYCH artykułów z bazy (są poniżej)");
            sb.AppendLine("- Możesz analizować pytania pod kątem CEO/Sales/Buyer perspective");
            sb.AppendLine("- Jeśli pytanie wykracza poza dostępne dane — powiedz to wprost zamiast halucynować");
            sb.AppendLine();

            // Daily summary z ostatnich 7 dni
            await AppendSummariesAsync(sb);

            // Top articles z ostatnich 30 dni (critical + warning)
            await AppendTopArticlesAsync(sb);

            // User queries (co user śledzi)
            await AppendUserQueriesAsync(sb);

            // Feedback preferences (co user lubi/nie lubi)
            await AppendFeedbackPrefsAsync(sb, userId);

            sb.AppendLine();
            sb.AppendLine("## ZASADY ODPOWIEDZI");
            sb.AppendLine("1. Pytanie ogólne (np. \"Co o tym sądzisz?\") → 2-3 zdania syntetyczne + 1 konkretna rekomendacja");
            sb.AppendLine("2. Pytanie analityczne → max 200-300 słów z liczbami i odniesieniami do bazy");
            sb.AppendLine("3. Pytanie strategiczne → odpowiedź 3-warstwowa: Diagnoza / Implikacja / Action");
            sb.AppendLine("4. Pytanie operacyjne → bullet points, max 5 punktów");
            sb.AppendLine();
            sb.AppendLine("Aktualna data: " + DateTime.Today.ToString("yyyy-MM-dd, dddd"));

            return sb.ToString();
        }

        private async Task AppendSummariesAsync(StringBuilder sb)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
SELECT TOP 7 SummaryDate, Headline, CeoSummary, MarketMood, MarketMoodReason,
       CriticalCount, WarningCount, PositiveCount
FROM intel_DailySummary
ORDER BY SummaryDate DESC", conn) { CommandTimeout = 15 };
                using var r = await cmd.ExecuteReaderAsync();
                var rows = new List<string>();
                while (await r.ReadAsync())
                {
                    var date = r.GetDateTime(0).ToString("yyyy-MM-dd");
                    var headline = r.IsDBNull(1) ? "" : r.GetString(1);
                    var ceo = r.IsDBNull(2) ? "" : r.GetString(2);
                    var mood = r.IsDBNull(3) ? "?" : r.GetString(3);
                    var moodReason = r.IsDBNull(4) ? "" : r.GetString(4);
                    var c = r.GetInt32(5);
                    var w = r.GetInt32(6);
                    var p = r.GetInt32(7);
                    if (ceo.Length > 400) ceo = ceo.Substring(0, 400) + "...";
                    rows.Add($"### {date} — {mood.ToUpper()}\n**Nagłówek:** {headline}\n**CEO summary:** {ceo}\n**Severity:** 🔴{c} 🟡{w} 🟢{p}");
                }
                if (rows.Count > 0)
                {
                    sb.AppendLine("## OSTATNIE 7 DNI BRIEFINGÓW");
                    foreach (var row in rows) { sb.AppendLine(row); sb.AppendLine(); }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[ChatContext] Summaries error: {ex.Message}"); }
        }

        private async Task AppendTopArticlesAsync(StringBuilder sb)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
SELECT TOP 25 Id, Title, SourceName, Category, Severity,
       ISNULL(CeoAnalysis, ISNULL(Summary, '')) AS WhatItMeans,
       PublishDate, RelevanceScore
FROM intel_Articles
WHERE FetchedAt >= DATEADD(day, -30, GETDATE())
  AND Severity IN ('critical', 'warning', 'positive')
ORDER BY
    CASE Severity WHEN 'critical' THEN 1 WHEN 'warning' THEN 2 WHEN 'positive' THEN 3 ELSE 4 END,
    RelevanceScore DESC", conn) { CommandTimeout = 15 };
                using var r = await cmd.ExecuteReaderAsync();
                var rows = new List<string>();
                while (await r.ReadAsync())
                {
                    var id = r.GetInt32(0);
                    var title = r.GetString(1);
                    var source = r.IsDBNull(2) ? "?" : r.GetString(2);
                    var cat = r.IsDBNull(3) ? "?" : r.GetString(3);
                    var sev = r.IsDBNull(4) ? "?" : r.GetString(4);
                    var what = r.IsDBNull(5) ? "" : r.GetString(5);
                    var date = r.IsDBNull(6) ? DateTime.MinValue : r.GetDateTime(6);
                    if (what.Length > 250) what = what.Substring(0, 250) + "...";
                    rows.Add($"- [#{id} · {date:yyyy-MM-dd} · {sev.ToUpper()} · {cat} · {source}] **{title}** — {what}");
                }
                if (rows.Count > 0)
                {
                    sb.AppendLine("## TOP ARTYKUŁY 30 DNI (critical/warning/positive)");
                    foreach (var row in rows) sb.AppendLine(row);
                    sb.AppendLine();
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[ChatContext] Articles error: {ex.Message}"); }
        }

        private async Task AppendUserQueriesAsync(StringBuilder sb)
        {
            try
            {
                var svc = new UserQueriesService();
                var queries = await svc.GetAllAsync(onlyEnabled: true);
                if (queries.Count > 0)
                {
                    sb.AppendLine("## TEMATY KTÓRE SERGIUSZ ŚLEDZI");
                    foreach (var q in queries.Take(20))
                    {
                        var prio = q.Priority <= 2 ? "🔴" : q.Priority <= 4 ? "🟠" : "🟡";
                        var text = q.QueryText.Length > 150 ? q.QueryText.Substring(0, 150) + "..." : q.QueryText;
                        sb.AppendLine($"- {prio} [{q.Category}] {text}");
                    }
                    sb.AppendLine();
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[ChatContext] UserQueries error: {ex.Message}"); }
        }

        private async Task AppendFeedbackPrefsAsync(StringBuilder sb, string userId)
        {
            try
            {
                var svc = new FeedbackService();
                var prefs = await svc.GetUserPreferencesAsync(userId, sinceDaysAgo: 90);
                var liked = prefs.Where(p => p.NetVote > 0).Take(5).ToList();
                var disliked = prefs.Where(p => p.NetVote < 0).Take(5).ToList();
                if (liked.Count + disliked.Count == 0) return;

                sb.AppendLine("## PREFERENCJE (z feedbacku 👍/👎)");
                if (liked.Count > 0)
                {
                    sb.AppendLine("**Lubi:**");
                    foreach (var p in liked)
                        sb.AppendLine($"- {p.Category} z {p.SourceName} (+{p.NetVote})");
                }
                if (disliked.Count > 0)
                {
                    sb.AppendLine("**Nie lubi:**");
                    foreach (var p in disliked)
                        sb.AppendLine($"- {p.Category} z {p.SourceName} ({p.NetVote})");
                }
                sb.AppendLine();
            }
            catch (Exception ex) { Debug.WriteLine($"[ChatContext] FeedbackPrefs error: {ex.Message}"); }
        }
    }
}
