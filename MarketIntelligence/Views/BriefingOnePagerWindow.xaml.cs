using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.MarketIntelligence.Views
{
    /// <summary>
    /// One-pager dla CEO — esencja dnia: TOP 3 krytyczne + action items + 3 perspektywy.
    /// Dane pobierane z intel_DailySummary (najnowszy rekord) + intel_Articles (top critical/warning per day).
    /// </summary>
    public partial class BriefingOnePagerWindow : Window
    {
        private string _markdownContent = "";

        public BriefingOnePagerWindow()
        {
            InitializeComponent();
            Loaded += async (s, e) =>
            {
                await LoadAsync();
                lstChat.ItemsSource = _chatHistory;
                AddSystemHello();
            };
        }

        private void AddSystemHello()
        {
            _chatHistory.Add(new ChatMessage
            {
                Author = "🤖 Asystent AI",
                AuthorColor = new SolidColorBrush(Color.FromRgb(201, 169, 110)),
                BgColor = new SolidColorBrush(Color.FromRgb(31, 26, 20)),
                Content = "Cześć Sergiusz. Wiem co się działo w drobiarstwie ostatnie 30 dni + znam profil firmy. Pytaj swobodnie albo użyj szybkich pytań powyżej."
            });
        }

        private async Task LoadAsync()
        {
            try
            {
                using var conn = new SqlConnection(Kalendarz1.MarketIntelligence.MarketIntelligenceConfig.LibraNetConnectionString);
                await conn.OpenAsync();

                // 1. Najnowszy daily summary
                var summary = await LoadLatestSummaryAsync(conn);
                if (summary == null)
                {
                    txtHeadline.Text = "Brak danych — uruchom pełne pobranie";
                    txtDate.Text = "intel_DailySummary jest puste";
                    return;
                }

                // 2. Top 3 critical articles per data summary
                var critical = await LoadTopCriticalArticlesAsync(conn, summary.SummaryDate);

                // 3. Render header
                txtHeadline.Text = string.IsNullOrEmpty(summary.Headline) ? "Brief dnia" : summary.Headline;
                txtDate.Text = summary.SummaryDate.ToString("dddd, dd MMMM yyyy", new System.Globalization.CultureInfo("pl-PL"));
                txtArticlesAnalyzed.Text = $"{summary.ArticlesAnalyzed} artykułów";
                txtSeverityCounts.Text = $"🔴 {summary.CriticalCount}  🟡 {summary.WarningCount}  🟢 {summary.PositiveCount}";

                // Mood badge
                switch (summary.MarketMood?.ToLower())
                {
                    case "positive": moodBadge.Background = new SolidColorBrush(Color.FromRgb(46, 125, 50)); txtMood.Text = "📈 Pozytywny"; break;
                    case "negative": moodBadge.Background = new SolidColorBrush(Color.FromRgb(198, 40, 40)); txtMood.Text = "📉 Negatywny"; break;
                    default: moodBadge.Background = new SolidColorBrush(Color.FromRgb(63, 56, 48)); txtMood.Text = "⚖ Neutralny"; break;
                }

                // 4. Top critical
                if (critical.Any())
                {
                    lstCritical.ItemsSource = critical;
                    txtNoCritical.Visibility = Visibility.Collapsed;
                }
                else
                {
                    txtNoCritical.Visibility = Visibility.Visible;
                }

                // 5. Action items
                var actions = ParseActionItems(summary.ActionItemsJson);
                if (actions.Any())
                {
                    lstActions.ItemsSource = actions.Take(5).ToList();
                    txtNoActions.Visibility = Visibility.Collapsed;
                }
                else
                {
                    txtNoActions.Visibility = Visibility.Visible;
                }

                // 6. 3 perspektywy
                txtCeo.Text = string.IsNullOrEmpty(summary.CeoSummary) ? "—" : summary.CeoSummary;
                txtSales.Text = string.IsNullOrEmpty(summary.SalesSummary) ? "—" : summary.SalesSummary;
                txtBuyer.Text = string.IsNullOrEmpty(summary.BuyerSummary) ? "—" : summary.BuyerSummary;

                // 7. Weekly outlook
                txtWeekly.Text = string.IsNullOrEmpty(summary.WeeklyOutlook) ? "—" : summary.WeeklyOutlook;

                // 8. Footer
                txtFooter.Text = $"Wygenerowane: {summary.GeneratedAt:yyyy-MM-dd HH:mm} · Model: {summary.AiModel} · Source: intel_DailySummary";

                // 9. Build markdown for export
                _markdownContent = BuildMarkdown(summary, critical, actions);
            }
            catch (Exception ex)
            {
                txtHeadline.Text = "Błąd ładowania danych";
                txtDate.Text = ex.Message;
            }
        }

        private async Task<SummaryRow> LoadLatestSummaryAsync(SqlConnection conn)
        {
            using var cmd = new SqlCommand(@"
SELECT TOP 1 SummaryDate, Headline, CeoSummary, SalesSummary, BuyerSummary,
       MarketMood, MarketMoodReason, WeeklyOutlook, TopAlerts, ActionItems,
       ArticlesAnalyzed, CriticalCount, WarningCount, PositiveCount,
       GeneratedAt, AiModel
FROM intel_DailySummary
ORDER BY SummaryDate DESC, GeneratedAt DESC", conn) { CommandTimeout = 10 };

            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            return new SummaryRow
            {
                SummaryDate = r.GetDateTime(0),
                Headline = r.IsDBNull(1) ? null : r.GetString(1),
                CeoSummary = r.IsDBNull(2) ? null : r.GetString(2),
                SalesSummary = r.IsDBNull(3) ? null : r.GetString(3),
                BuyerSummary = r.IsDBNull(4) ? null : r.GetString(4),
                MarketMood = r.IsDBNull(5) ? null : r.GetString(5),
                MarketMoodReason = r.IsDBNull(6) ? null : r.GetString(6),
                WeeklyOutlook = r.IsDBNull(7) ? null : r.GetString(7),
                TopAlertsJson = r.IsDBNull(8) ? null : r.GetString(8),
                ActionItemsJson = r.IsDBNull(9) ? null : r.GetString(9),
                ArticlesAnalyzed = r.IsDBNull(10) ? 0 : r.GetInt32(10),
                CriticalCount = r.IsDBNull(11) ? 0 : r.GetInt32(11),
                WarningCount = r.IsDBNull(12) ? 0 : r.GetInt32(12),
                PositiveCount = r.IsDBNull(13) ? 0 : r.GetInt32(13),
                GeneratedAt = r.IsDBNull(14) ? DateTime.MinValue : r.GetDateTime(14),
                AiModel = r.IsDBNull(15) ? null : r.GetString(15)
            };
        }

        private async Task<List<CriticalArticle>> LoadTopCriticalArticlesAsync(SqlConnection conn, DateTime summaryDate)
        {
            var list = new List<CriticalArticle>();
            try
            {
                using var cmd = new SqlCommand(@"
SELECT TOP 3
    Id, Title, SourceName, Category, Severity,
    ISNULL(CeoAnalysis, ISNULL(Summary, '')) AS WhatItMeans
FROM intel_Articles
WHERE CAST(FetchedAt AS DATE) >= DATEADD(day, -1, @SummaryDate)
  AND Severity IN ('critical', 'warning')
ORDER BY
    CASE Severity WHEN 'critical' THEN 1 WHEN 'warning' THEN 2 ELSE 3 END,
    RelevanceScore DESC", conn) { CommandTimeout = 10 };
                cmd.Parameters.AddWithValue("@SummaryDate", summaryDate);

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var what = r.IsDBNull(5) ? "" : r.GetString(5);
                    if (what.Length > 280) what = what.Substring(0, 280) + "...";
                    list.Add(new CriticalArticle
                    {
                        Id = r.GetInt32(0),
                        Title = r.GetString(1),
                        SourceName = r.IsDBNull(2) ? "?" : r.GetString(2),
                        Category = r.IsDBNull(3) ? "Info" : r.GetString(3),
                        Severity = r.IsDBNull(4) ? "info" : r.GetString(4),
                        WhatItMeans = what
                    });
                }
            }
            catch { }
            return list;
        }

        private List<ActionRow> ParseActionItems(string json)
        {
            var result = new List<ActionRow>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var priority = el.TryGetProperty("priority", out var p) ? p.GetString() ?? "medium" : "medium";
                    var action = el.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
                    var owner = el.TryGetProperty("owner", out var o) ? o.GetString() ?? "?" : "?";

                    var color = priority?.ToLower() switch
                    {
                        "high" => new SolidColorBrush(Color.FromRgb(232, 93, 93)),
                        "medium" => new SolidColorBrush(Color.FromRgb(201, 169, 110)),
                        _ => new SolidColorBrush(Color.FromRgb(63, 56, 48))
                    };

                    result.Add(new ActionRow
                    {
                        Priority = priority?.ToUpper() ?? "MED",
                        PriorityColor = color,
                        Owner = owner,
                        Action = action
                    });
                }
            }
            catch { }
            return result;
        }

        private string BuildMarkdown(SummaryRow s, List<CriticalArticle> crit, List<ActionRow> actions)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# 📋 Briefing dnia — {s.SummaryDate:yyyy-MM-dd}");
            sb.AppendLine();
            sb.AppendLine($"**{s.Headline}**");
            sb.AppendLine();
            sb.AppendLine($"- **Artykułów przeanalizowanych:** {s.ArticlesAnalyzed}");
            sb.AppendLine($"- **Severity:** 🔴 {s.CriticalCount} critical, 🟡 {s.WarningCount} warning, 🟢 {s.PositiveCount} positive");
            sb.AppendLine($"- **Nastrój rynku:** {s.MarketMood} — {s.MarketMoodReason}");
            sb.AppendLine();

            sb.AppendLine("## 🔴 TOP 3 KRYTYCZNE DZIŚ");
            sb.AppendLine();
            if (crit.Any())
            {
                int i = 1;
                foreach (var c in crit)
                {
                    sb.AppendLine($"### {i++}. [{c.Severity.ToUpper()}] {c.Title}");
                    sb.AppendLine($"_Kategoria: {c.Category} · Źródło: {c.SourceName}_");
                    sb.AppendLine();
                    sb.AppendLine(c.WhatItMeans);
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("*Brak krytycznych alertów dziś.*");
                sb.AppendLine();
            }

            sb.AppendLine("## 📋 CO ROBIĆ DZIŚ");
            sb.AppendLine();
            if (actions.Any())
            {
                foreach (var a in actions.Take(5))
                    sb.AppendLine($"- **[{a.Priority}] {a.Owner}:** {a.Action}");
            }
            else
            {
                sb.AppendLine("*Brak action items.*");
            }
            sb.AppendLine();

            sb.AppendLine("## 🎯 Streszczenia rolowe");
            sb.AppendLine();
            sb.AppendLine($"**👔 CEO:** {s.CeoSummary}");
            sb.AppendLine();
            sb.AppendLine($"**💼 Sales:** {s.SalesSummary}");
            sb.AppendLine();
            sb.AppendLine($"**🛒 Buyer:** {s.BuyerSummary}");
            sb.AppendLine();

            sb.AppendLine("## 📅 Prognoza tygodniowa");
            sb.AppendLine();
            sb.AppendLine(s.WeeklyOutlook);
            sb.AppendLine();

            sb.AppendLine("---");
            sb.AppendLine($"*Wygenerowane: {s.GeneratedAt:yyyy-MM-dd HH:mm} · Model: {s.AiModel}*");
            return sb.ToString();
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(_markdownContent); MessageBox.Show("Skopiowano markdown do schowka."); }
            catch (Exception ex) { MessageBox.Show($"Błąd: {ex.Message}"); }
        }

        private void BtnExportMd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Briefing Piorkowscy");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"BriefingDnia_{DateTime.Today:yyyy-MM-dd}.md");
                File.WriteAllText(path, _markdownContent, Encoding.UTF8);
                MessageBox.Show($"Zapisano:\n{path}", "Eksport MD", MessageBoxButton.OK, MessageBoxImage.Information);
                try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
            }
            catch (Exception ex) { MessageBox.Show($"Błąd zapisu: {ex.Message}"); }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private readonly Services.FeedbackService _feedbackService = new();
        private readonly System.Collections.ObjectModel.ObservableCollection<ChatMessage> _chatHistory = new();
        private Services.AI.ClaudeAnalysisService _claude;
        private string _chatSystemPrompt;

        private async void BtnFeedbackUp_Click(object sender, RoutedEventArgs e) => await RecordFeedbackAsync(sender, 1);
        private async void BtnFeedbackDown_Click(object sender, RoutedEventArgs e) => await RecordFeedbackAsync(sender, -1);

        private async Task RecordFeedbackAsync(object sender, int vote)
        {
            try
            {
                if (sender is not System.Windows.Controls.Button btn || btn.Tag == null) return;
                if (!int.TryParse(btn.Tag.ToString(), out var articleId) || articleId == 0) return;

                var article = lstCritical.ItemsSource is IEnumerable<CriticalArticle> items
                    ? items.FirstOrDefault(a => a.Id == articleId)
                    : null;

                await _feedbackService.RecordAsync(
                    articleId,
                    App.UserID ?? Environment.UserName,
                    vote,
                    category: article?.Category,
                    sourceName: article?.SourceName);

                // Visual feedback — change button color to confirm
                btn.Background = new SolidColorBrush(vote > 0
                    ? Color.FromRgb(46, 125, 50)   // green
                    : Color.FromRgb(120, 60, 60)); // red-brown
                btn.Foreground = new SolidColorBrush(Colors.White);
                btn.ToolTip = vote > 0
                    ? "✅ Zapisano — AI pokaże więcej takich"
                    : "✅ Zapisano — AI ograniczy podobne";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Feedback error: {ex.Message}");
            }
        }

        // POCO models
        private class SummaryRow
        {
            public DateTime SummaryDate { get; set; }
            public string Headline { get; set; }
            public string CeoSummary { get; set; }
            public string SalesSummary { get; set; }
            public string BuyerSummary { get; set; }
            public string MarketMood { get; set; }
            public string MarketMoodReason { get; set; }
            public string WeeklyOutlook { get; set; }
            public string TopAlertsJson { get; set; }
            public string ActionItemsJson { get; set; }
            public int ArticlesAnalyzed { get; set; }
            public int CriticalCount { get; set; }
            public int WarningCount { get; set; }
            public int PositiveCount { get; set; }
            public DateTime GeneratedAt { get; set; }
            public string AiModel { get; set; }
        }

        public class CriticalArticle
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string SourceName { get; set; }
            public string Category { get; set; }
            public string Severity { get; set; }
            public string WhatItMeans { get; set; }
        }

        private async void BtnQuickPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string prompt)
            {
                txtChatInput.Text = prompt;
                await SendChatAsync();
            }
        }

        private async void TxtChatInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter
                && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == 0)
            {
                e.Handled = true;
                await SendChatAsync();
            }
        }

        private async void BtnChatSend_Click(object sender, RoutedEventArgs e) => await SendChatAsync();

        private async Task SendChatAsync()
        {
            var q = (txtChatInput.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(q)) return;

            txtChatInput.Text = "";
            btnChatSend.IsEnabled = false;
            txtChatStatus.Text = "⏳ AI myśli (Sonnet 4.6, ~5-15s)…";

            // Append user message
            _chatHistory.Add(new ChatMessage
            {
                Author = "👤 " + (App.UserFullName ?? "Sergiusz"),
                AuthorColor = new SolidColorBrush(Color.FromRgb(245, 237, 224)),
                BgColor = new SolidColorBrush(Color.FromRgb(42, 37, 32)),
                Content = q
            });
            ScrollChatToEnd();

            try
            {
                if (_claude == null) _claude = new Services.AI.ClaudeAnalysisService();
                if (_chatSystemPrompt == null)
                {
                    var builder = new Services.BriefingChatContextBuilder();
                    _chatSystemPrompt = await builder.BuildSystemPromptAsync(App.UserID ?? Environment.UserName);
                }

                var answer = await _claude.ChatAsync(_chatSystemPrompt, q);

                _chatHistory.Add(new ChatMessage
                {
                    Author = "🤖 Asystent AI",
                    AuthorColor = new SolidColorBrush(Color.FromRgb(201, 169, 110)),
                    BgColor = new SolidColorBrush(Color.FromRgb(31, 26, 20)),
                    Content = answer ?? "(brak odpowiedzi)"
                });
                ScrollChatToEnd();
                txtChatStatus.Text = $"✅ Odpowiedź wygenerowana · {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                _chatHistory.Add(new ChatMessage
                {
                    Author = "⚠ Błąd",
                    AuthorColor = new SolidColorBrush(Color.FromRgb(232, 93, 93)),
                    BgColor = new SolidColorBrush(Color.FromRgb(40, 24, 24)),
                    Content = $"Nie udało się uzyskać odpowiedzi: {ex.Message}"
                });
                txtChatStatus.Text = "❌ Błąd — sprawdź klucz Claude API.";
            }
            finally
            {
                btnChatSend.IsEnabled = true;
            }
        }

        private void ScrollChatToEnd()
        {
            Dispatcher.InvokeAsync(() => chatScroll?.ScrollToEnd(),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        public class ChatMessage
        {
            public string Author { get; set; }
            public string Content { get; set; }
            public SolidColorBrush AuthorColor { get; set; }
            public SolidColorBrush BgColor { get; set; }
        }

        public class ActionRow
        {
            public string Priority { get; set; }
            public SolidColorBrush PriorityColor { get; set; }
            public string Owner { get; set; }
            public string Action { get; set; }
        }
    }
}
