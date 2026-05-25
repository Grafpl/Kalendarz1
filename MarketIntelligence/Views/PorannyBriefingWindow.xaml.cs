using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Kalendarz1.MarketIntelligence.Models;
using Kalendarz1.MarketIntelligence.ViewModels;

namespace Kalendarz1.MarketIntelligence.Views
{
    /// <summary>
    /// Centrum Wiadomości AI (dawniej Poranny Briefing) — CHAT-FIRST redesign (opcja A).
    /// Lewo: lista artykułów (intel_Articles, realne). Prawo: stały chat AI.
    /// Usunięto 10 mock-tabów + role switcher + tasks panel + summary/indicators bar z hardcoded danymi.
    /// </summary>
    public partial class PorannyBriefingWindow : Window
    {
        private PorannyBriefingViewModel _viewModel;

        // Debounce dla SearchTextBox
        private readonly DispatcherTimer _searchDebounceTimer;
        private string _pendingSearchText;

        // Auto-cron 06:00 daily fetch (state per-day w %LOCALAPPDATA%)
        private DispatcherTimer _autoFetchTimer;
        private static readonly string AutoFetchStatePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kalendarz1", "MarketIntelligence", "autofetch-state.json");

        // ── Chat ──
        private readonly ObservableCollection<ChatMessage> _chatHistory = new();
        private Services.AI.ClaudeAnalysisService _claude;
        private string _chatSystemPrompt;

        public PorannyBriefingWindow()
        {
            InitializeComponent();
            _viewModel = DataContext as PorannyBriefingViewModel;
            Loaded += PorannyBriefingWindow_Loaded;
            Closed += PorannyBriefingWindow_Closed;

            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        }

        private void PorannyBriefingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Chat: pokaż powitanie
            lstChat.ItemsSource = _chatHistory;
            AddSystemHello();

            // Auto-load artykułów z bazy (realne dane) zaraz po otwarciu
            if (_viewModel?.LoadFromDatabaseCommand?.CanExecute(null) == true)
                _viewModel.LoadFromDatabaseCommand.Execute(null);

            StartAutoFetchTimer();
        }

        private void PorannyBriefingWindow_Closed(object sender, EventArgs e)
        {
            _autoFetchTimer?.Stop();
        }

        #region Auto-fetch 06:00

        private void StartAutoFetchTimer()
        {
            _autoFetchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _autoFetchTimer.Tick += async (s, e) =>
            {
                try
                {
                    var now = DateTime.Now;
                    if (now.Hour != 6 || now.Minute > 15) return;

                    var todayKey = now.ToString("yyyy-MM-dd");
                    if (LastAutoFetchDay() == todayKey) return;

                    Debug.WriteLine($"[AutoFetch] ⏰ 06:0X — triggering daily fetch ({todayKey})");
                    MarkAutoFetchDone(todayKey);

                    if (_viewModel?.RefreshFromInternetCommand?.CanExecute(null) == true)
                    {
                        _viewModel.RefreshFromInternetCommand.Execute(null);
                        var exportTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(8) };
                        exportTimer.Tick += (_, __) =>
                        {
                            exportTimer.Stop();
                            try { AutoExportOnePagerToMd(); } catch (Exception ex) { Debug.WriteLine($"[AutoFetch] MD export error: {ex.Message}"); }
                        };
                        exportTimer.Start();
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"[AutoFetch] tick error: {ex.Message}"); }
            };
            _autoFetchTimer.Start();
            Debug.WriteLine("[AutoFetch] Timer started — checking every 60s for 06:00 window");
        }

        private static string LastAutoFetchDay()
        {
            try
            {
                if (!System.IO.File.Exists(AutoFetchStatePath)) return null;
                var json = System.IO.File.ReadAllText(AutoFetchStatePath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("lastDay", out var d) ? d.GetString() : null;
            }
            catch { return null; }
        }

        private static void MarkAutoFetchDone(string day)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(AutoFetchStatePath);
                if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(AutoFetchStatePath, $"{{\"lastDay\":\"{day}\",\"triggeredAt\":\"{DateTime.Now:O}\"}}");
            }
            catch { }
        }

        /// <summary>Auto-eksport One-pager do MD po auto-fetchu o 6:00 (niewidoczne okno w tle).</summary>
        private void AutoExportOnePagerToMd()
        {
            try
            {
                var win = new BriefingOnePagerWindow();
                win.WindowState = WindowState.Minimized;
                win.ShowInTaskbar = false;
                win.Opacity = 0;
                win.Loaded += async (s, e) =>
                {
                    await Task.Delay(2000);
                    try
                    {
                        win.GetType().GetMethod("BtnExportMd_Click", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                            ?.Invoke(win, new object[] { null, null });
                    }
                    catch (Exception ex) { Debug.WriteLine($"[AutoFetch] auto-export invoke error: {ex.Message}"); }
                    win.Close();
                };
                win.Show();
                Debug.WriteLine("[AutoFetch] ✓ One-pager auto-export triggered");
            }
            catch (Exception ex) { Debug.WriteLine($"[AutoFetch] auto-export setup error: {ex.Message}"); }
        }

        #endregion

        #region Articles: filters / search / expand

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn && btn.Tag is string category)
            {
                if (btn.Parent is Panel parent)
                {
                    foreach (var child in parent.Children)
                        if (child is ToggleButton otherBtn && otherBtn != btn)
                            otherBtn.IsChecked = false;
                }
                btn.IsChecked = true;
                _viewModel?.FilterByCategoryCommand.Execute(category);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb && _viewModel != null)
            {
                _pendingSearchText = tb.Text;
                _searchDebounceTimer.Stop();
                _searchDebounceTimer.Start();
            }
        }

        private void SearchDebounceTimer_Tick(object sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();
            if (_viewModel != null)
                _viewModel.SearchText = _pendingSearchText;
        }

        private void Article_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is BriefingArticle article)
                _viewModel?.ToggleArticleCommand.Execute(article);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
            }
            catch (Exception ex) { Debug.WriteLine($"Error opening link: {ex.Message}"); }
            e.Handled = true;
        }

        private void OpenUrl_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string url && !string.IsNullOrEmpty(url))
            {
                try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
                catch (Exception ex) { Debug.WriteLine($"Error opening URL: {ex.Message}"); }
            }
        }

        #endregion

        #region Toolbar buttons

        private void btnOnePager_Click(object sender, RoutedEventArgs e)
            => OpenChild(() => new BriefingOnePagerWindow(), "One-pager");

        private void btnSources_Click(object sender, RoutedEventArgs e)
            => OpenChild(() => new SourcesManagementWindow(), "Moje źródła");

        private void btnQueries_Click(object sender, RoutedEventArgs e)
            => OpenChild(() => new UserQueriesManagementWindow(), "Moje tematy");

        private void btnDiagnostics_Click(object sender, RoutedEventArgs e)
            => OpenChild(() => new BriefingDiagnosticsWindow(), "Diagnostyka");

        private void btnHelp_Click(object sender, RoutedEventArgs e)
        {
            try { new BriefingHelpWindow { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show("Błąd otwierania pomocy: " + ex.Message); }
        }

        private void OpenChild(Func<Window> factory, string name)
        {
            try
            {
                var win = factory();
                win.Owner = this;
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania '{name}': {ex.Message}", name,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Chat AI

        private void AddSystemHello()
        {
            _chatHistory.Add(new ChatMessage
            {
                Author = "🤖 Asystent AI",
                AuthorColor = new SolidColorBrush(Color.FromRgb(201, 169, 110)),
                BgColor = new SolidColorBrush(Color.FromRgb(31, 26, 20)),
                Content = "Cześć Sergiusz. Znam profil firmy + ostatnie 30 dni z drobiarstwa. Pytaj swobodnie albo użyj szybkich pytań powyżej."
            });
        }

        private async void BtnQuickPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string prompt)
            {
                txtChatInput.Text = prompt;
                await SendChatAsync();
            }
        }

        private async void TxtChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
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
                _claude ??= new Services.AI.ClaudeAnalysisService();
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
                txtChatStatus.Text = $"✅ Odpowiedź · {DateTime.Now:HH:mm:ss}";
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
            Dispatcher.InvokeAsync(() => chatScroll?.ScrollToEnd(), DispatcherPriority.Background);
        }

        public class ChatMessage
        {
            public string Author { get; set; }
            public string Content { get; set; }
            public SolidColorBrush AuthorColor { get; set; }
            public SolidColorBrush BgColor { get; set; }
        }

        #endregion
    }

    #region Value Converters

    public class SeverityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SeverityLevel severity)
            {
                return severity switch
                {
                    SeverityLevel.Critical => new SolidColorBrush(Color.FromRgb(192, 80, 80)),
                    SeverityLevel.Warning => new SolidColorBrush(Color.FromRgb(212, 160, 53)),
                    SeverityLevel.Positive => new SolidColorBrush(Color.FromRgb(109, 175, 109)),
                    SeverityLevel.Info => new SolidColorBrush(Color.FromRgb(90, 143, 192)),
                    _ => new SolidColorBrush(Color.FromRgb(90, 143, 192))
                };
            }
            return new SolidColorBrush(Color.FromRgb(90, 143, 192));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class SeverityToBgConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SeverityLevel severity)
            {
                return severity switch
                {
                    SeverityLevel.Critical => new SolidColorBrush(Color.FromRgb(28, 18, 16)),
                    SeverityLevel.Warning => new SolidColorBrush(Color.FromRgb(28, 24, 8)),
                    SeverityLevel.Positive => new SolidColorBrush(Color.FromRgb(16, 28, 16)),
                    SeverityLevel.Info => new SolidColorBrush(Color.FromRgb(16, 21, 32)),
                    _ => new SolidColorBrush(Color.FromRgb(16, 21, 32))
                };
            }
            return new SolidColorBrush(Color.FromRgb(16, 21, 32));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class DirectionToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PriceDirection direction)
            {
                return direction switch
                {
                    PriceDirection.Up => new SolidColorBrush(Color.FromRgb(109, 175, 109)),
                    PriceDirection.Down => new SolidColorBrush(Color.FromRgb(192, 80, 80)),
                    PriceDirection.Stable => new SolidColorBrush(Color.FromRgb(122, 111, 99)),
                    _ => new SolidColorBrush(Color.FromRgb(122, 111, 99))
                };
            }
            return new SolidColorBrush(Color.FromRgb(122, 111, 99));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // bool: false → Visible (np. IsExpanded). int: 0 → Visible (np. empty state TotalArticles).
            if (value is bool b) return b ? Visibility.Collapsed : Visibility.Visible;
            if (value is int i) return i == 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Multi-value converter dla per-article AI analysis.
    /// Values[0] = BriefingArticle, Values[1] = UserRole. Parameter = "Analysis"/"Actions"/"Label".
    /// </summary>
    public class RoleBasedAnalysisConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return string.Empty;

            var article = values[0] as BriefingArticle;
            var role = values[1] is UserRole r ? r : UserRole.CEO;
            var type = parameter as string ?? "Analysis";

            if (article == null) return string.Empty;

            return type switch
            {
                "Analysis" => role switch
                {
                    UserRole.CEO => article.AiAnalysisCeo ?? "",
                    UserRole.Sales => article.AiAnalysisSales ?? "",
                    UserRole.Buyer => article.AiAnalysisBuyer ?? "",
                    _ => article.AiAnalysisCeo ?? ""
                },
                "Actions" => role switch
                {
                    UserRole.CEO => article.RecommendedActionsCeo ?? "",
                    UserRole.Sales => article.RecommendedActionsSales ?? "",
                    UserRole.Buyer => article.RecommendedActionsBuyer ?? "",
                    _ => article.RecommendedActionsCeo ?? ""
                },
                "Label" => role switch
                {
                    UserRole.CEO => " — CEO / Strategia",
                    UserRole.Sales => " — Handlowiec",
                    UserRole.Buyer => " — Zakupowiec",
                    _ => " — CEO"
                },
                _ => ""
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    #endregion
}
