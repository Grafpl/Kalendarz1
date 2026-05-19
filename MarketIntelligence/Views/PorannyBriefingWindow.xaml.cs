using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Kalendarz1.MarketIntelligence.Models;
using Kalendarz1.MarketIntelligence.ViewModels;

namespace Kalendarz1.MarketIntelligence.Views
{
    public partial class PorannyBriefingWindow : Window
    {
        private PorannyBriefingViewModel _viewModel;

        // Debounce dla SearchTextBox — bez tego każdy znak triggował filter na 100+ artykułach (lag)
        private readonly DispatcherTimer _searchDebounceTimer;
        private string _pendingSearchText;

        // Auto-cron sprawdzający co minutę czy 06:00 i czy ostatni fetch >18h temu → auto-trigger.
        // State per-day w %LOCALAPPDATA%\Kalendarz1\MarketIntelligence\autofetch-state.json
        // żeby przeżywał restart apki i nie triggerował dwa razy w tym samym dniu.
        private DispatcherTimer _autoFetchTimer;
        private static readonly string AutoFetchStatePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kalendarz1", "MarketIntelligence", "autofetch-state.json");

        public PorannyBriefingWindow()
        {
            InitializeComponent();
            _viewModel = DataContext as PorannyBriefingViewModel;
            Loaded += PorannyBriefingWindow_Loaded;
            Closed += PorannyBriefingWindow_Closed;

            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        }

        private void PorannyBriefingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BuildSummaryText();
            DrawPriceChart();
            StartAutoFetchTimer();
        }

        private void PorannyBriefingWindow_Closed(object sender, EventArgs e)
        {
            _autoFetchTimer?.Stop();
        }

        /// <summary>
        /// Timer sprawdzający co 60s czy jest 06:00-06:15 i czy dziś jeszcze nie było auto-fetcha.
        /// Per-day flag w pliku JSON żeby restart apki nie wywołał ponownie.
        /// </summary>
        private void StartAutoFetchTimer()
        {
            _autoFetchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _autoFetchTimer.Tick += async (s, e) =>
            {
                try
                {
                    var now = DateTime.Now;
                    // Window: 06:00–06:15
                    if (now.Hour != 6 || now.Minute > 15) return;

                    var todayKey = now.ToString("yyyy-MM-dd");
                    if (LastAutoFetchDay() == todayKey) return; // już dziś było

                    Debug.WriteLine($"[AutoFetch] ⏰ 06:0X — triggering daily fetch ({todayKey})");
                    MarkAutoFetchDone(todayKey);

                    if (_viewModel?.RefreshFromInternetCommand?.CanExecute(null) == true)
                    {
                        _viewModel.RefreshFromInternetCommand.Execute(null);
                        // Po fetchu — auto-eksport one-pager do MD (delayed 8min żeby fetch zdążył)
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

        /// <summary>
        /// Auto-eksport One-pager do MD pliku po auto-fetchu o 6:00.
        /// Otwiera niewidocznie BriefingOnePagerWindow w tle, czeka aż załaduje dane,
        /// klika "Zapisz MD" przez ukrytą metodę, zamyka. Plik trafia do Documents/Briefing Piorkowscy/.
        /// </summary>
        private void AutoExportOnePagerToMd()
        {
            try
            {
                var win = new BriefingOnePagerWindow();
                // Trick: pokaż window minimized + invisible, daj mu się zainicjalizować, zapisz, zamknij
                win.WindowState = WindowState.Minimized;
                win.ShowInTaskbar = false;
                win.Opacity = 0;
                win.Loaded += async (s, e) =>
                {
                    await System.Threading.Tasks.Task.Delay(2000); // wait for DB load
                    try
                    {
                        // wywołaj wewnętrzny export
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

        #region Summary Builder

        private void BuildSummaryText()
        {
            if (_viewModel == null || txtSummary == null) return;

            txtSummary.Inlines.Clear();

            foreach (var segment in _viewModel.SummarySegments)
            {
                var run = new Run(segment.Text);

                if (segment.Color != "default")
                {
                    try
                    {
                        run.Foreground = new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString(segment.Color));
                    }
                    catch
                    {
                        run.Foreground = new SolidColorBrush(Color.FromRgb(213, 204, 192));
                    }
                }
                else
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(213, 204, 192));
                }

                if (segment.IsBold)
                {
                    run.FontWeight = FontWeights.SemiBold;
                }

                txtSummary.Inlines.Add(run);
            }
        }

        #endregion

        #region Chart Drawing

        private void DrawPriceChart()
        {
            if (canvasChart == null || _viewModel == null) return;

            canvasChart.Children.Clear();

            var skupIndicator = _viewModel.Indicators.Count > 0 ? _viewModel.Indicators[0] : null;
            if (skupIndicator?.SparkData == null || skupIndicator.SparkData.Length < 2) return;

            var data = skupIndicator.SparkData;
            double width = 320;
            double height = 80;
            double padding = 10;

            double minVal = double.MaxValue;
            double maxVal = double.MinValue;
            foreach (var val in data)
            {
                if (val < minVal) minVal = val;
                if (val > maxVal) maxVal = val;
            }
            double range = maxVal - minVal;
            if (range < 0.01) range = 0.1;

            minVal -= range * 0.1;
            maxVal += range * 0.1;
            range = maxVal - minVal;

            var points = new PointCollection();
            double stepX = (width - padding * 2) / (data.Length - 1);

            for (int i = 0; i < data.Length; i++)
            {
                double x = padding + i * stepX;
                double y = height - padding - ((data[i] - minVal) / range) * (height - padding * 2);
                points.Add(new Point(x, y));
            }

            var fillPoints = new PointCollection(points);
            fillPoints.Add(new Point(padding + (data.Length - 1) * stepX, height - padding));
            fillPoints.Add(new Point(padding, height - padding));

            var fillPolygon = new Polygon
            {
                Points = fillPoints,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(60, 201, 169, 110), 0),
                        new GradientStop(Color.FromArgb(0, 201, 169, 110), 1)
                    }
                }
            };
            canvasChart.Children.Add(fillPolygon);

            var polyline = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(Color.FromRgb(201, 169, 110)),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };
            canvasChart.Children.Add(polyline);

            if (points.Count > 0)
            {
                var lastPoint = points[points.Count - 1];
                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(Color.FromRgb(201, 169, 110)),
                    Stroke = new SolidColorBrush(Color.FromRgb(21, 18, 13)),
                    StrokeThickness = 2
                };
                Canvas.SetLeft(dot, lastPoint.X - 4);
                Canvas.SetTop(dot, lastPoint.Y - 4);
                canvasChart.Children.Add(dot);
            }

            var priceLabel = new TextBlock
            {
                Text = $"{data[data.Length - 1]:N2} zl",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(245, 237, 224))
            };
            Canvas.SetRight(priceLabel, 10);
            Canvas.SetTop(priceLabel, 10);
            canvasChart.Children.Add(priceLabel);
        }

        #endregion

        #region Event Handlers

        private void RoleButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string role)
            {
                _viewModel?.ChangeRoleCommand.Execute(role);
                // Force refresh of articles to update AI analysis
                icArticles?.Items.Refresh();
            }
        }

        private void TasksButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.ToggleTasksPanelCommand.Execute(null);
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn && btn.Tag is string category)
            {
                var parent = btn.Parent as Panel;
                if (parent != null)
                {
                    foreach (var child in parent.Children)
                    {
                        if (child is ToggleButton otherBtn && otherBtn != btn)
                        {
                            otherBtn.IsChecked = false;
                        }
                    }
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
            {
                _viewModel.SearchText = _pendingSearchText;
            }
        }

        private void Article_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is BriefingArticle article)
            {
                _viewModel?.ToggleArticleCommand.Execute(article);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening link: {ex.Message}");
            }
            e.Handled = true;
        }

        private void btnDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new BriefingDiagnosticsWindow { Owner = this };
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd otwierania diagnostyki: " + ex.Message, "Diagnostyka",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnOnePager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new BriefingOnePagerWindow { Owner = this };
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd otwierania One-pager: " + ex.Message, "One-pager",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenUrl_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string url && !string.IsNullOrEmpty(url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error opening URL: {ex.Message}");
                }
            }
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
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Multi-value converter for role-based AI analysis.
    /// Values[0] = BriefingArticle, Values[1] = UserRole
    /// Parameter = "Analysis" or "Actions" or "Label"
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
