using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.MarketIntelligence.Services.AI;
using Kalendarz1.MarketIntelligence.Services.DataSources;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Generuje pełny raport diagnostyczny modułu Poranny Briefing.
    /// Wszystko w jednym pliku markdown — można skopiować/wysłać.
    /// Sekrety są maskowane (klucze API → pierwsze 8 znaków, hasła w conn string → ***).
    /// </summary>
    public static class DiagnosticsReportGenerator
    {
        public static async Task<string> GenerateAsync()
        {
            var sb = new StringBuilder();
            var generatedAt = DateTime.Now;

            sb.AppendLine($"# 🛠 Diagnostyka Porannego Briefingu");
            sb.AppendLine();
            sb.AppendLine($"**Wygenerowano:** {generatedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**Maszyna:** {Environment.MachineName} | **Użytkownik:** {Environment.UserName}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            AppendEnvironmentInfo(sb);
            AppendApiStatus(sb);
            AppendConfigSection(sb);
            await AppendDatabaseStatusAsync(sb);
            AppendRssSources(sb);
            AppendScrapingSources(sb);
            await AppendUserQueriesAsync(sb);
            await AppendFetchHistoryAsync(sb);
            await AppendRecentArticlesAsync(sb);
            AppendLiveLog(sb);

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"*Raport wygenerowany przez DiagnosticsReportGenerator @ {generatedAt:yyyy-MM-dd HH:mm:ss}*");

            return sb.ToString();
        }

        #region Sections

        private static void AppendEnvironmentInfo(StringBuilder sb)
        {
            sb.AppendLine("## 🖥 Środowisko");
            sb.AppendLine();
            sb.AppendLine($"- **OS:** {Environment.OSVersion}");
            sb.AppendLine($"- **.NET:** {Environment.Version}");
            sb.AppendLine($"- **Process:** PID {Environment.ProcessId} ({Process.GetCurrentProcess().ProcessName})");
            sb.AppendLine($"- **Working Set:** {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024:N0} MB");
            sb.AppendLine($"- **CWD:** {Environment.CurrentDirectory}");
            sb.AppendLine();
        }

        private static void AppendApiStatus(StringBuilder sb)
        {
            sb.AppendLine("## 🔑 Status API");
            sb.AppendLine();

            // Anthropic
            using (var claude = new ClaudeAnalysisService())
            {
                var envKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
                var secretsKey = SecretsLoader.Get("ANTHROPIC_API_KEY");
                sb.AppendLine($"### Anthropic (Claude)");
                sb.AppendLine($"- **Skonfigurowane:** {(claude.IsConfigured ? "✅ TAK" : "❌ NIE")}");
                sb.AppendLine($"- **Źródło klucza:** {(envKey != null ? "env ANTHROPIC_API_KEY" : secretsKey != null ? "secrets.json" : "brak")}");
                sb.AppendLine($"- **env ANTHROPIC_API_KEY:** {MaskKey(envKey)}");
                sb.AppendLine($"- **secrets.json ANTHROPIC_API_KEY:** {MaskKey(secretsKey)}");
                sb.AppendLine($"- **Modele używane:**");
                sb.AppendLine($"  - Filter/Translation: `claude-haiku-4-5-20251001`");
                sb.AppendLine($"  - Daily Summary: `claude-sonnet-4-6`");
                sb.AppendLine($"  - Article Analysis: `claude-sonnet-4-6` (parallel x3, max_tokens 8000)");
                sb.AppendLine();
            }

            // Perplexity
            using (var px = new PerplexitySearchService())
            {
                var envKey = Environment.GetEnvironmentVariable("PERPLEXITY_API_KEY");
                var secretsKey = SecretsLoader.Get("PERPLEXITY_API_KEY");
                var limit = Environment.GetEnvironmentVariable("PERPLEXITY_DAILY_LIMIT")
                            ?? SecretsLoader.Get("PERPLEXITY_DAILY_LIMIT")
                            ?? "30 (default — tryb oszczędny)";
                sb.AppendLine($"### Perplexity (Sonar)");
                sb.AppendLine($"- **Skonfigurowane:** {(px.IsConfigured ? "✅ TAK" : "❌ NIE")}");
                sb.AppendLine($"- **Źródło klucza:** {(envKey != null ? "env PERPLEXITY_API_KEY" : secretsKey != null ? "secrets.json" : "brak")}");
                sb.AppendLine($"- **env PERPLEXITY_API_KEY:** {MaskKey(envKey)}");
                sb.AppendLine($"- **secrets.json PERPLEXITY_API_KEY:** {MaskKey(secretsKey)}");
                if (px.IsConfigured)
                {
                    var (used, lim) = px.DailyBudget;
                    sb.AppendLine($"- **Dzienny budżet:** {used}/{lim} zapytań ({(lim > 0 ? (used * 100 / lim) : 0)}%)");
                }
                sb.AppendLine($"- **PERPLEXITY_DAILY_LIMIT:** {limit}");
                sb.AppendLine();
            }
        }

        private static void AppendConfigSection(StringBuilder sb)
        {
            sb.AppendLine("## 🔧 Konfiguracja");
            sb.AppendLine();

            sb.AppendLine("### Plik secrets.json (poza repo)");
            sb.AppendLine($"- **Ścieżka:** `{SecretsLoader.SecretsFilePath}`");
            if (SecretsLoader.Exists)
            {
                try
                {
                    var fi = new System.IO.FileInfo(SecretsLoader.SecretsFilePath);
                    sb.AppendLine($"- **Status:** ✅ istnieje ({fi.Length} bajtów, modyfikowany {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss})");
                    var keys = SecretsLoader.GetKeyNames();
                    var realKeys = keys.Where(k => !k.StartsWith("_")).ToList();
                    sb.AppendLine($"- **Klucze:** {string.Join(", ", realKeys.Select(k => $"`{k}`"))}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"- **Status:** ⚠️ błąd odczytu: {ex.Message}");
                }
            }
            else
            {
                sb.AppendLine($"- **Status:** ❌ NIE ISTNIEJE — utwórz plik aby zapisać klucze trwale");
            }
            sb.AppendLine();

            sb.AppendLine("### Connection strings (hasła zamaskowane)");
            sb.AppendLine($"- **LibraNet:** `{MaskConnString(MarketIntelligenceConfig.LibraNetConnectionString)}`");
            sb.AppendLine($"- **HANDEL (Sage):** `{MaskConnString(MarketIntelligenceConfig.HandelConnectionString)}`");
            sb.AppendLine();
            sb.AppendLine("### Zmienne środowiskowe");
            string[] envVars =
            {
                "LIBRANET_CONNECTION_STRING", "HANDEL_CONNECTION_STRING",
                "PERPLEXITY_DAILY_LIMIT"
            };
            foreach (var v in envVars)
            {
                var val = Environment.GetEnvironmentVariable(v);
                if (!string.IsNullOrEmpty(val))
                {
                    var display = v.Contains("CONNECTION") ? MaskConnString(val) : val;
                    sb.AppendLine($"- `{v}` = `{display}`");
                }
                else
                {
                    sb.AppendLine($"- `{v}` = *(nie ustawione, użyje default lub secrets.json)*");
                }
            }
            sb.AppendLine();
        }

        private static async Task AppendDatabaseStatusAsync(StringBuilder sb)
        {
            sb.AppendLine("## 🗄 Baza danych LibraNet");
            sb.AppendLine();
            try
            {
                using var conn = new SqlConnection(MarketIntelligenceConfig.LibraNetConnectionString);
                await conn.OpenAsync();
                sb.AppendLine($"- **Status:** ✅ POŁĄCZONO");
                sb.AppendLine($"- **Server:** `{conn.DataSource}`");
                sb.AppendLine($"- **Database:** `{conn.Database}`");
                sb.AppendLine($"- **Server version:** `{conn.ServerVersion}`");
                sb.AppendLine();
                sb.AppendLine("### Liczność tabel intel_*");
                sb.AppendLine();
                sb.AppendLine("| Tabela | Rekordów | Najstarszy / Najnowszy |");
                sb.AppendLine("|--------|----------|------------------------|");

                await AppendTableCount(sb, conn, "intel_Articles", "FetchedAt");
                await AppendTableCount(sb, conn, "intel_HpaiAlerts", "ReportDate");
                await AppendTableCount(sb, conn, "intel_FetchLog", "FetchTime");
                await AppendTableCount(sb, conn, "intel_Prices", "PriceDate");
                await AppendTableCount(sb, conn, "intel_Sources", null);
                await AppendTableCount(sb, conn, "intel_DailySummary", "SummaryDate");
                await AppendTableCount(sb, conn, "intel_UserQueries", "CreatedAt");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"- **Status:** ❌ BŁĄD");
                sb.AppendLine($"- **Komunikat:** `{ex.Message}`");
                sb.AppendLine();
            }
        }

        private static async Task AppendTableCount(StringBuilder sb, SqlConnection conn, string tableName, string dateColumn)
        {
            try
            {
                using var cmd = new SqlCommand(
                    $"SELECT COUNT(*) FROM {tableName} WITH (NOLOCK)" +
                    (dateColumn != null ? $"; SELECT MIN({dateColumn}), MAX({dateColumn}) FROM {tableName} WITH (NOLOCK)" : ""),
                    conn);
                cmd.CommandTimeout = 10;
                using var reader = await cmd.ExecuteReaderAsync();
                int count = 0;
                if (await reader.ReadAsync()) count = reader.GetInt32(0);
                string range = "—";
                if (dateColumn != null && await reader.NextResultAsync() && await reader.ReadAsync() && !reader.IsDBNull(0))
                {
                    var min = reader.GetDateTime(0);
                    var max = reader.GetDateTime(1);
                    range = $"{min:yyyy-MM-dd} → {max:yyyy-MM-dd}";
                }
                sb.AppendLine($"| `{tableName}` | {count:N0} | {range} |");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"| `{tableName}` | ❌ błąd | `{ex.Message.Substring(0, Math.Min(60, ex.Message.Length))}` |");
            }
        }

        private static void AppendRssSources(StringBuilder sb)
        {
            sb.AppendLine("## 📡 Źródła RSS");
            sb.AppendLine();
            var rss = NewsSourceConfig.GetAllRssSources();
            sb.AppendLine($"**Łącznie:** {rss.Count} źródeł");
            sb.AppendLine();
            sb.AppendLine("| Id | Nazwa | Kategoria | Język | Pri | URL |");
            sb.AppendLine("|----|-------|-----------|-------|-----|-----|");
            foreach (var s in rss.OrderBy(s => s.Priority).ThenBy(s => s.Name))
            {
                sb.AppendLine($"| `{s.Id}` | {s.Name} | {s.Category} | {s.Language} | {s.Priority} | `{s.Url}` |");
            }
            sb.AppendLine();
        }

        private static void AppendScrapingSources(StringBuilder sb)
        {
            sb.AppendLine("## 🌐 Źródła scrapingu (HTML)");
            sb.AppendLine();
            var scrap = NewsSourceConfig.GetAllScrapingSources();
            sb.AppendLine($"**Łącznie:** {scrap.Count} źródeł");
            sb.AppendLine();
            if (scrap.Count == 0)
            {
                sb.AppendLine("*Brak skonfigurowanych źródeł scrapingu.*");
                sb.AppendLine();
                return;
            }
            sb.AppendLine("| Id | Nazwa | Kategoria | URL |");
            sb.AppendLine("|----|-------|-----------|-----|");
            foreach (var s in scrap.OrderBy(s => s.Priority).ThenBy(s => s.Name))
            {
                sb.AppendLine($"| `{s.Id}` | {s.Name} | {s.Category} | `{s.Url}` |");
            }
            sb.AppendLine();
        }

        private static async Task AppendUserQueriesAsync(StringBuilder sb)
        {
            sb.AppendLine("## 🔍 Zapytania użytkownika (intel_UserQueries)");
            sb.AppendLine();
            try
            {
                var svc = new UserQueriesService();
                var queries = await svc.GetAllAsync();
                if (queries.Count == 0)
                {
                    sb.AppendLine("*Brak własnych zapytań — używane tylko hardcoded (80+ w PerplexitySearchService).*");
                    sb.AppendLine();
                    return;
                }
                sb.AppendLine($"**Łącznie:** {queries.Count} ({queries.Count(q => q.Enabled)} włączonych)");
                sb.AppendLine();
                sb.AppendLine("| Id | Wł. | Pri | Świeżość | Kategoria | Tekst | Użyte | Ostatnio |");
                sb.AppendLine("|----|-----|-----|----------|-----------|-------|-------|----------|");
                foreach (var q in queries.OrderBy(q => q.Priority))
                {
                    var enabled = q.Enabled ? "✅" : "—";
                    var text = q.QueryText.Length > 80 ? q.QueryText.Substring(0, 77) + "..." : q.QueryText;
                    sb.AppendLine($"| {q.Id} | {enabled} | {q.Priority} | {q.RecencyFilter} | {q.Category} | {text} | {q.TimesUsed} | {q.LastUsedDisplay} |");
                }
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Błąd ładowania: `{ex.Message}`");
                sb.AppendLine();
            }
        }

        private static async Task AppendFetchHistoryAsync(StringBuilder sb)
        {
            sb.AppendLine("## 📊 Historia pobrań (ostatnie 20)");
            sb.AppendLine();
            try
            {
                using var conn = new SqlConnection(MarketIntelligenceConfig.LibraNetConnectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
SELECT TOP 20 FetchTime, FetchType, RssArticles, ScrapedArticles, TotalArticles,
              RelevantArticles, AnalyzedArticles, HpaiAlerts, DurationMs, Success, ErrorMessage
FROM intel_FetchLog ORDER BY FetchTime DESC", conn);
                cmd.CommandTimeout = 15;
                using var reader = await cmd.ExecuteReaderAsync();
                sb.AppendLine("| Kiedy | Typ | RSS | Scrap | Total | Relev. | Analiz. | HPAI | Czas | Sukces | Błąd |");
                sb.AppendLine("|-------|-----|-----|-------|-------|--------|---------|------|------|--------|------|");
                int rows = 0;
                while (await reader.ReadAsync())
                {
                    var ft = reader.GetDateTime(0);
                    var type = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var dur = reader.IsDBNull(8) ? 0 : reader.GetInt32(8);
                    var success = reader.GetBoolean(9);
                    var err = reader.IsDBNull(10) ? "" : reader.GetString(10);
                    if (err.Length > 60) err = err.Substring(0, 57) + "...";
                    sb.AppendLine($"| {ft:yyyy-MM-dd HH:mm:ss} | {type} | {reader.GetInt32(2)} | {reader.GetInt32(3)} | {reader.GetInt32(4)} | {reader.GetInt32(5)} | {reader.GetInt32(6)} | {reader.GetInt32(7)} | {dur / 1000.0:F1}s | {(success ? "✅" : "❌")} | {err} |");
                    rows++;
                }
                if (rows == 0) sb.AppendLine("| *(brak danych)* | | | | | | | | | | |");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Błąd: `{ex.Message}`");
                sb.AppendLine();
            }
        }

        private static async Task AppendRecentArticlesAsync(StringBuilder sb)
        {
            sb.AppendLine("## 📰 Ostatnie 15 artykułów (intel_Articles)");
            sb.AppendLine();
            try
            {
                using var conn = new SqlConnection(MarketIntelligenceConfig.LibraNetConnectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
SELECT TOP 15 FetchedAt, SourceName, Title, Category, Severity, RelevanceScore, IsRelevant, AiModel
FROM intel_Articles ORDER BY FetchedAt DESC", conn);
                cmd.CommandTimeout = 15;
                using var reader = await cmd.ExecuteReaderAsync();
                sb.AppendLine("| Pobrane | Źródło | Tytuł | Kategoria | Severity | Rel. score | Relev. | Model AI |");
                sb.AppendLine("|---------|--------|-------|-----------|----------|------------|--------|----------|");
                int rows = 0;
                while (await reader.ReadAsync())
                {
                    var title = reader.GetString(2);
                    if (title.Length > 80) title = title.Substring(0, 77) + "...";
                    var model = reader.IsDBNull(7) ? "—" : reader.GetString(7);
                    sb.AppendLine($"| {reader.GetDateTime(0):MM-dd HH:mm} | {reader.GetString(1)} | {title} | {(reader.IsDBNull(3) ? "" : reader.GetString(3))} | {(reader.IsDBNull(4) ? "" : reader.GetString(4))} | {reader.GetInt32(5)} | {(reader.GetBoolean(6) ? "✅" : "—")} | {model} |");
                    rows++;
                }
                if (rows == 0) sb.AppendLine("| *(brak artykułów)* | | | | | | | |");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Błąd: `{ex.Message}`");
                sb.AppendLine();
            }
        }

        private static void AppendLiveLog(StringBuilder sb)
        {
            var entries = BriefingLogHub.GetRecent();
            sb.AppendLine($"## 🪵 Live log ({entries.Count} wpisów w buforze)");
            sb.AppendLine();
            if (entries.Count == 0)
            {
                sb.AppendLine("*Bufor pusty — moduł jeszcze nic nie zalogował od momentu startu apki.*");
                sb.AppendLine();
                return;
            }

            // Najpierw same błędy/warningi (szybkie skanowanie)
            var problems = entries.Where(e => e.Level != LogLevel.Info).ToList();
            if (problems.Any())
            {
                sb.AppendLine($"### ⚠️ Błędy i warningi ({problems.Count})");
                sb.AppendLine();
                sb.AppendLine("```");
                foreach (var e in problems)
                {
                    sb.AppendLine($"[{e.TimestampDisplay}] [{e.Source}] [{e.Level}] {e.Message}");
                }
                sb.AppendLine("```");
                sb.AppendLine();
            }

            sb.AppendLine("### 📜 Pełny log");
            sb.AppendLine();
            sb.AppendLine("```");
            foreach (var e in entries)
            {
                sb.AppendLine($"[{e.TimestampDisplay}] [{e.Source}] {e.Message}");
            }
            sb.AppendLine("```");
            sb.AppendLine();
        }

        #endregion

        #region Masking helpers

        private static string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "*(nie ustawione)*";
            if (key.Length <= 12) return "***";
            return key.Substring(0, 8) + "..." + key.Substring(key.Length - 4);
        }

        private static string MaskConnString(string cs)
        {
            if (string.IsNullOrEmpty(cs)) return "*(empty)*";
            // Replace Password=xxx (case insensitive) z Password=***
            return System.Text.RegularExpressions.Regex.Replace(
                cs,
                @"(Password|Pwd)\s*=\s*[^;]+",
                "$1=***",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        #endregion
    }
}
