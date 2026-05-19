using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Kalendarz1.MarketIntelligence.Services;
using Kalendarz1.MarketIntelligence.Services.AI;
using Kalendarz1.MarketIntelligence.Services.DataSources;

namespace Kalendarz1.MarketIntelligence.Views
{
    public partial class BriefingDiagnosticsWindow : Window
    {
        private readonly UserQueriesService _userQueries = new();
        private readonly ObservableCollection<UserQuery> _userQueriesUi = new();
        private readonly ObservableCollection<FetchLogRow> _fetchHistory = new();
        private readonly ObservableCollection<LogEntry> _liveLog = new();
        private CancellationTokenSource _cts;

        public BriefingDiagnosticsWindow()
        {
            InitializeComponent();

            // Bindowanie kolekcji
            gridUserQueries.ItemsSource = _userQueriesUi;
            gridFetchHistory.ItemsSource = _fetchHistory;
            lstLog.ItemsSource = _liveLog;
            gridSources.ItemsSource = NewsSourceConfig.GetAllSources();

            // Załaduj historię logów (BriefingLogHub działa od momentu pierwszego użycia briefingu)
            foreach (var e in BriefingLogHub.GetRecent())
                _liveLog.Add(e);

            // Subskrypcja live (event może być wywoływany z dowolnego wątku)
            BriefingLogHub.OnMessage += OnLogMessage;
            Closed += (s, e) =>
            {
                BriefingLogHub.OnMessage -= OnLogMessage;
                _cts?.Cancel();
            };

            Loaded += async (s, e) =>
            {
                await RefreshStatusAsync();
                await ReloadUserQueriesAsync();
                await LoadFetchHistoryAsync();
            };
        }

        private void OnLogMessage(LogEntry entry)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (chkOnlyErrors.IsChecked == true && entry.Level != LogLevel.Error && entry.Level != LogLevel.Warning)
                    return;

                _liveLog.Add(entry);
                while (_liveLog.Count > 500) _liveLog.RemoveAt(0);

                if (chkAutoScroll.IsChecked == true && _liveLog.Count > 0)
                {
                    lstLog.ScrollIntoView(_liveLog[_liveLog.Count - 1]);
                }
            }));
        }

        #region Status tab

        private async Task RefreshStatusAsync()
        {
            // 1. Anthropic — sprawdź czy klucz jest skonfigurowany
            using (var claude = new ClaudeAnalysisService())
            {
                if (claude.IsConfigured)
                {
                    SetPill(bdrAnthropicPill, txtAnthropicStatus, "OK", isOk: true);
                    txtAnthropicDetail.Text = "Klucz skonfigurowany — modele: Haiku 4.5 + Sonnet 4.6 + Opus 4.7";
                }
                else
                {
                    SetPill(bdrAnthropicPill, txtAnthropicStatus, "BRAK KLUCZA", isOk: false);
                    txtAnthropicDetail.Text = "Ustaw ANTHROPIC_API_KEY w zmiennej środowiskowej lub %LOCALAPPDATA%\\Antropic";
                }
            }

            // 2. Perplexity — klucz + budżet
            using (var px = new PerplexitySearchService())
            {
                if (px.IsConfigured)
                {
                    var (used, limit) = px.DailyBudget;
                    var pct = limit > 0 ? (int)((double)used / limit * 100) : 0;
                    if (pct >= 100)
                        SetPill(bdrPerplexityPill, txtPerplexityStatus, "LIMIT WYCZERPANY", isOk: false);
                    else if (pct >= 80)
                        SetPill(bdrPerplexityPill, txtPerplexityStatus, "80% LIMITU", isWarn: true);
                    else
                        SetPill(bdrPerplexityPill, txtPerplexityStatus, "OK", isOk: true);
                    txtPerplexityBudget.Text = $"Dzienny budżet: {used}/{limit} zapytań ({pct}%)";
                }
                else
                {
                    SetPill(bdrPerplexityPill, txtPerplexityStatus, "BRAK KLUCZA", isOk: false);
                    txtPerplexityBudget.Text = "Ustaw PERPLEXITY_API_KEY — bez tego internet search nie działa";
                }
            }

            // 3. Baza LibraNet — test connection
            try
            {
                using var conn = new SqlConnection(MarketIntelligenceConfig.LibraNetConnectionString);
                await conn.OpenAsync();
                SetPill(bdrDbPill, txtDbStatus, "POŁĄCZONO", isOk: true);
                txtDbDetail.Text = $"Server: {conn.DataSource}, DB: {conn.Database}";
            }
            catch (Exception ex)
            {
                SetPill(bdrDbPill, txtDbStatus, "BŁĄD", isOk: false);
                txtDbDetail.Text = ex.Message;
            }

            // 4. Ostatnie pobranie z intel_FetchLog
            try
            {
                using var conn = new SqlConnection(MarketIntelligenceConfig.LibraNetConnectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
SELECT TOP 1 FetchTime, RssArticles, ScrapedArticles, RelevantArticles, AnalyzedArticles,
              DurationMs, Success, ErrorMessage
FROM intel_FetchLog ORDER BY FetchTime DESC", conn);
                cmd.CommandTimeout = 10;
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var ft = reader.GetDateTime(0);
                    txtLastFetchTime.Text = ft.ToString("yyyy-MM-dd HH:mm:ss");
                    txtLastRss.Text = reader.GetInt32(1).ToString();
                    txtLastScraped.Text = reader.GetInt32(2).ToString();
                    txtLastRelevant.Text = reader.GetInt32(3).ToString();
                    txtLastAnalyzed.Text = reader.GetInt32(4).ToString();
                    var durMs = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                    txtLastDuration.Text = (durMs / 1000.0).ToString("F1") + "s";
                    var success = reader.GetBoolean(6);
                    txtLastStatus.Text = success ? "✅ OK" : "❌ " + (reader.IsDBNull(7) ? "błąd" : reader.GetString(7));
                    txtLastStatus.Foreground = success
                        ? System.Windows.Media.Brushes.LightGreen
                        : System.Windows.Media.Brushes.IndianRed;
                }
                else
                {
                    txtLastFetchTime.Text = "Brak danych";
                }
            }
            catch (Exception ex)
            {
                txtLastFetchTime.Text = "Błąd: " + ex.Message;
            }
        }

        private static void SetPill(System.Windows.Controls.Border border, TextBlock tb, string text, bool isOk = false, bool isWarn = false)
        {
            tb.Text = text;
            if (isOk)
                border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x35, 0x20));
            else if (isWarn)
                border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3D, 0x33, 0x1E));
            else
                border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3D, 0x1E, 0x1E));
        }

        private async void btnRefreshStatus_Click(object sender, RoutedEventArgs e)
        {
            await RefreshStatusAsync();
            txtActionResult.Text = $"[{DateTime.Now:HH:mm:ss}] Status odświeżony";
        }

        private async void btnTestPerplexity_Click(object sender, RoutedEventArgs e)
        {
            txtActionResult.Text = "🧪 Testuję Perplexity (1 zapytanie)...";
            try
            {
                using var px = new PerplexitySearchService();
                if (!px.IsConfigured)
                {
                    txtActionResult.Text = "❌ Klucz PERPLEXITY_API_KEY nie skonfigurowany";
                    return;
                }
                var result = await px.SearchAsync("Najnowsze wiadomości z polskiej branży drobiarskiej dzisiaj",
                    new PerplexitySearchOptions { RecencyFilter = "day", MaxTokens = 1500 });
                txtActionResult.Text = result.Success
                    ? $"✅ Test OK — {result.Articles?.Count ?? 0} artykułów, AI summary {(string.IsNullOrEmpty(result.AiAnswer) ? "(brak)" : $"({result.AiAnswer.Length} znaków)")}"
                    : $"❌ Test fail: {result.Error}";
            }
            catch (Exception ex)
            {
                txtActionResult.Text = $"❌ Wyjątek: {ex.Message}";
            }
        }

        private async void btnTestClaude_Click(object sender, RoutedEventArgs e)
        {
            txtActionResult.Text = "🧪 Testuję Claude (1 ping)...";
            try
            {
                using var claude = new ClaudeAnalysisService();
                if (!claude.IsConfigured)
                {
                    txtActionResult.Text = "❌ Klucz ANTHROPIC_API_KEY nie skonfigurowany";
                    return;
                }
                // Najprostszy test: filtr 1 pseudo-artykułu
                var fakeArticles = new List<RawArticle>
                {
                    new RawArticle { Title = "Test - HPAI w Polsce, ognisko w łódzkim", Summary = "Test artykuł", UrlHash = Guid.NewGuid().ToString() }
                };
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var filtered = await claude.QuickFilterArticlesAsync(fakeArticles);
                sw.Stop();
                txtActionResult.Text = $"✅ Claude odpowiedział w {sw.ElapsedMilliseconds}ms — sklasyfikował {filtered.Count} artykuł(ów)";
            }
            catch (Exception ex)
            {
                txtActionResult.Text = $"❌ Wyjątek: {ex.Message}";
            }
        }

        private System.Windows.Threading.DispatcherTimer _elapsedTimer;
        private DateTime _fetchStartTime;
        private DateTime _lastProgressUpdate;
        private bool _watchdogWarned;
        private const int WatchdogStaleSeconds = 90; // próg ostrzeżenia

        private async void btnForceFetch_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                txtActionResult.Text = "⏳ Pobieranie już w toku — kliknij Anuluj jeśli chcesz przerwać";
                return;
            }

            _cts = new CancellationTokenSource();
            _fetchStartTime = DateTime.Now;
            _lastProgressUpdate = DateTime.Now;
            _watchdogWarned = false;
            txtActionResult.Text = "";

            // Pokaż progress bar
            bdrProgress.Visibility = Visibility.Visible;
            pbFetch.Value = 0;
            txtProgressStage.Text = "Inicjalizacja";
            txtProgressMessage.Text = "Startuję pobieranie...";
            txtProgressPercent.Text = "0%";
            txtProgressDetail.Text = "";

            // Włącz Anuluj, wyłącz Force
            btnForceFetch.IsEnabled = false;
            btnCancelFetch.IsEnabled = true;

            // Timer odświeża elapsed co 200ms + WATCHDOG sprawdza czy progress nie utknął
            _elapsedTimer?.Stop();
            _elapsedTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _elapsedTimer.Tick += (s, _) =>
            {
                var elapsed = DateTime.Now - _fetchStartTime;
                txtProgressElapsed.Text = elapsed.TotalSeconds < 60
                    ? $"{elapsed.TotalSeconds:F1}s"
                    : $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";

                // Watchdog: jeśli ostatni update progress >90s temu, ostrzeż użytkownika 1x
                var sinceProgress = (DateTime.Now - _lastProgressUpdate).TotalSeconds;
                if (sinceProgress > 5)
                {
                    txtProgressDetail.Text = $"⚠ Brak postępu od {sinceProgress:F0}s (ostatni etap: {txtProgressStage.Text})";
                }
                if (!_watchdogWarned && sinceProgress > WatchdogStaleSeconds && _cts != null && !_cts.IsCancellationRequested)
                {
                    _watchdogWarned = true;
                    _elapsedTimer.Stop(); // zatrzymaj zanim user zdecyduje
                    var stage = txtProgressStage.Text;
                    var lastMsg = txtProgressMessage.Text;
                    var result = MessageBox.Show(
                        this,
                        $"Pobieranie nie odpowiada od {sinceProgress:F0} sekund.\n\n" +
                        $"Etap: {stage}\n" +
                        $"Ostatnia wiadomość: {lastMsg}\n\n" +
                        $"Anulować pobranie?",
                        "⚠ Watchdog — możliwy zawiech",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (result == MessageBoxResult.Yes)
                    {
                        _cts?.Cancel();
                        txtProgressDetail.Text = "Anulowanie przez watchdog...";
                    }
                    else
                    {
                        _watchdogWarned = false; // pozwól zaalarmować ponownie za 90s
                        _lastProgressUpdate = DateTime.Now; // reset
                    }
                    _elapsedTimer.Start();
                }
            };
            _elapsedTimer.Start();

            try
            {
                var loader = BriefingDataLoaderService.Instance;
                var progress = new Progress<FetchProgress>(p =>
                {
                    _lastProgressUpdate = DateTime.Now; // reset watchdog
                    pbFetch.Value = p.Percent;
                    txtProgressStage.Text = p.Stage ?? "—";
                    txtProgressMessage.Text = p.Message ?? "";
                    txtProgressPercent.Text = $"{p.Percent}%";
                    txtProgressDetail.Text = p.Message ?? "";
                    txtActionResult.Text = $"⚡ [{p.Percent}%] {p.Stage} — {p.Message}";
                });
                var data = await loader.FetchNewDataAsync(fullFetch: true, ct: _cts.Token, progress: progress);

                _elapsedTimer.Stop();
                if (data.Success)
                {
                    pbFetch.Value = 100;
                    txtProgressStage.Text = "✅ Zakończone";
                    txtProgressMessage.Text = $"{data.Articles?.Count ?? 0} artykułów, {data.HpaiAlerts?.Count ?? 0} HPAI";
                    txtProgressPercent.Text = "100%";
                    txtActionResult.Text = $"✅ Pobranie zakończone w {(DateTime.Now - _fetchStartTime).TotalSeconds:F1}s: {data.Articles?.Count ?? 0} artykułów, {data.HpaiAlerts?.Count ?? 0} HPAI alertów";
                    await LoadFetchHistoryAsync();
                    await RefreshStatusAsync();
                }
                else
                {
                    txtProgressStage.Text = "❌ Błąd";
                    txtProgressMessage.Text = data.Error ?? "Nieznany błąd";
                    txtActionResult.Text = $"❌ Pobranie nieudane: {data.Error}";
                }
            }
            catch (OperationCanceledException)
            {
                _elapsedTimer.Stop();
                txtProgressStage.Text = "⏹ Anulowane";
                txtActionResult.Text = "⏹ Pobranie anulowane";
            }
            catch (Exception ex)
            {
                _elapsedTimer.Stop();
                txtProgressStage.Text = "❌ Wyjątek";
                txtProgressMessage.Text = ex.Message;
                txtActionResult.Text = $"❌ Wyjątek: {ex.Message}";
            }
            finally
            {
                btnForceFetch.IsEnabled = true;
                btnCancelFetch.IsEnabled = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void btnCancelFetch_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                btnCancelFetch.IsEnabled = false;
                txtProgressMessage.Text = "Anulowanie... (czekam aż bieżąca operacja zwróci)";
            }
        }

        #endregion

        #region Eksport raportu

        private async void btnExportFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtActionResult.Text = "⏳ Generuję pełny raport...";
                var report = await DiagnosticsReportGenerator.GenerateAsync();

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Zapisz raport diagnostyczny",
                    Filter = "Markdown (*.md)|*.md|Text (*.txt)|*.txt|All files (*.*)|*.*",
                    FileName = $"briefing-diagnostics-{DateTime.Now:yyyy-MM-dd-HHmm}.md"
                };
                if (dlg.ShowDialog() == true)
                {
                    await System.IO.File.WriteAllTextAsync(dlg.FileName, report, System.Text.Encoding.UTF8);
                    var fi = new System.IO.FileInfo(dlg.FileName);
                    txtActionResult.Text = $"✅ Zapisano: {dlg.FileName} ({fi.Length / 1024.0:F1} KB)";

                    if (MessageBox.Show("Otworzyć folder z raportem?", "Eksport gotowy",
                            MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{dlg.FileName}\"",
                            UseShellExecute = true
                        });
                    }
                }
                else
                {
                    txtActionResult.Text = "Eksport anulowany.";
                }
            }
            catch (Exception ex)
            {
                txtActionResult.Text = $"❌ Błąd eksportu: {ex.Message}";
            }
        }

        private async void btnExportClipboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtActionResult.Text = "⏳ Generuję raport do schowka...";
                var report = await DiagnosticsReportGenerator.GenerateAsync();
                Clipboard.SetText(report);
                var sizeKb = System.Text.Encoding.UTF8.GetByteCount(report) / 1024.0;
                txtActionResult.Text = $"✅ Skopiowano do schowka ({report.Length:N0} znaków, {sizeKb:F1} KB). Wklej w wiadomość do Claude (Ctrl+V).";
            }
            catch (Exception ex)
            {
                txtActionResult.Text = $"❌ Błąd: {ex.Message}";
            }
        }

        private async void btnExportPreview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtActionResult.Text = "⏳ Generuję podgląd...";
                var report = await DiagnosticsReportGenerator.GenerateAsync();
                var preview = new Window
                {
                    Title = "Podgląd raportu diagnostycznego",
                    Width = 1100,
                    Height = 800,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = System.Windows.Media.Brushes.Black,
                    Content = new ScrollViewer
                    {
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Content = new TextBox
                        {
                            Text = report,
                            IsReadOnly = true,
                            AcceptsReturn = true,
                            TextWrapping = TextWrapping.NoWrap,
                            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                            FontSize = 11,
                            Background = System.Windows.Media.Brushes.Black,
                            Foreground = System.Windows.Media.Brushes.LightGray,
                            BorderThickness = new Thickness(0),
                            Padding = new Thickness(12)
                        }
                    }
                };
                preview.ShowDialog();
                txtActionResult.Text = $"Podgląd zamknięty ({report.Length:N0} znaków).";
            }
            catch (Exception ex)
            {
                txtActionResult.Text = $"❌ Błąd podglądu: {ex.Message}";
            }
        }

        #endregion

        #region Fetch history tab

        private async Task LoadFetchHistoryAsync()
        {
            _fetchHistory.Clear();
            try
            {
                using var conn = new SqlConnection(MarketIntelligenceConfig.LibraNetConnectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
SELECT TOP 50 FetchTime, FetchType, RssArticles, ScrapedArticles, TotalArticles,
              RelevantArticles, AnalyzedArticles, HpaiAlerts, DurationMs, Success, ErrorMessage
FROM intel_FetchLog ORDER BY FetchTime DESC", conn);
                cmd.CommandTimeout = 15;
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    _fetchHistory.Add(new FetchLogRow
                    {
                        FetchTime = reader.GetDateTime(0),
                        FetchType = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        RssArticles = reader.GetInt32(2),
                        ScrapedArticles = reader.GetInt32(3),
                        TotalArticles = reader.GetInt32(4),
                        RelevantArticles = reader.GetInt32(5),
                        AnalyzedArticles = reader.GetInt32(6),
                        HpaiAlerts = reader.GetInt32(7),
                        DurationSec = reader.IsDBNull(8) ? "—" : (reader.GetInt32(8) / 1000.0).ToString("F1"),
                        Success = reader.GetBoolean(9),
                        ErrorMessage = reader.IsDBNull(10) ? "" : reader.GetString(10)
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania historii: " + ex.Message, "Diagnostyka", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region Live log tab

        private void btnClearLog_Click(object sender, RoutedEventArgs e)
        {
            _liveLog.Clear();
            BriefingLogHub.Clear();
        }

        #endregion

        #region User queries tab

        private async Task ReloadUserQueriesAsync()
        {
            _userQueriesUi.Clear();
            var list = await _userQueries.GetAllAsync();
            foreach (var q in list) _userQueriesUi.Add(q);
        }

        private void btnAddQuery_Click(object sender, RoutedEventArgs e)
        {
            _userQueriesUi.Add(new UserQuery
            {
                Id = 0,
                QueryText = "Nowe zapytanie - wpisz tekst",
                Category = "Custom",
                Priority = 5,
                Enabled = true,
                RecencyFilter = "week",
                CreatedAt = DateTime.Now
            });
        }

        private async void btnSaveQueries_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int inserted = 0, updated = 0;
                foreach (var q in _userQueriesUi.ToList())
                {
                    if (string.IsNullOrWhiteSpace(q.QueryText)) continue;
                    if (q.Id == 0)
                    {
                        q.Id = await _userQueries.InsertAsync(q);
                        inserted++;
                    }
                    else
                    {
                        await _userQueries.UpdateAsync(q);
                        updated++;
                    }
                }
                MessageBox.Show($"Zapisano: {inserted} nowych, {updated} zaktualizowanych.", "Zapytania",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                await ReloadUserQueriesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd: " + ex.Message, "Zapis", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnDeleteQuery_Click(object sender, RoutedEventArgs e)
        {
            if (gridUserQueries.SelectedItem is UserQuery sel)
            {
                if (MessageBox.Show($"Usunąć zapytanie #{sel.Id}?\n\n{sel.QueryText}", "Potwierdzenie",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

                if (sel.Id > 0) await _userQueries.DeleteAsync(sel.Id);
                _userQueriesUi.Remove(sel);
            }
        }

        private async void btnReloadQueries_Click(object sender, RoutedEventArgs e)
        {
            await ReloadUserQueriesAsync();
        }

        #endregion

        /// <summary>Wiersz historii pobrań dla DataGrid.</summary>
        public class FetchLogRow
        {
            public DateTime FetchTime { get; set; }
            public string FetchType { get; set; }
            public int RssArticles { get; set; }
            public int ScrapedArticles { get; set; }
            public int TotalArticles { get; set; }
            public int RelevantArticles { get; set; }
            public int AnalyzedArticles { get; set; }
            public int HpaiAlerts { get; set; }
            public string DurationSec { get; set; }
            public bool Success { get; set; }
            public string SuccessDisplay => Success ? "✅" : "❌";
            public string ErrorMessage { get; set; }
        }
    }
}
