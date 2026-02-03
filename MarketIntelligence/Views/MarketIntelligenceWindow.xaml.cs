using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Kalendarz1.MarketIntelligence.Models;
using Kalendarz1.MarketIntelligence.Services;
using LiveCharts;
using LiveCharts.Wpf;

namespace Kalendarz1.MarketIntelligence.Views
{
    public partial class MarketIntelligenceWindow : Window
    {
        private readonly MarketIntelligenceService _service;
        private readonly DispatcherTimer _refreshTimer;
        private string _currentCategory = "Wszystkie";
        private string _currentSeverity = "Wszystkie";
        private string _searchText = "";
        private bool _autoRefreshEnabled = true;

        public MarketIntelligenceWindow()
        {
            _service = new MarketIntelligenceService();
            InitializeComponent();

            // Setup auto-refresh timer (every 5 minutes)
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(5)
            };
            _refreshTimer.Tick += async (s, args) =>
            {
                if (_autoRefreshEnabled)
                {
                    await RefreshDataSilentAsync();
                }
            };

            Loaded += MarketIntelligenceWindow_Loaded;
            Closed += (s, args) => _refreshTimer.Stop();
        }

        private async void MarketIntelligenceWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
            _refreshTimer.Start();
        }

        private async Task RefreshDataSilentAsync()
        {
            try
            {
                // Refresh indicators
                var indicators = await _service.GetDashboardIndicatorsAsync();
                icIndicators.ItemsSource = indicators;

                // Refresh alerts
                var allArticles = await _service.GetArticlesAsync();
                var alerts = allArticles
                    .Where(a => a.Severity == "critical" || a.Severity == "warning")
                    .OrderByDescending(a => a.Severity == "critical")
                    .ThenByDescending(a => a.PublishDate)
                    .Take(5)
                    .ToList();

                if (alerts.Any())
                {
                    icAlerts.ItemsSource = alerts;
                    pnlAlerts.Visibility = Visibility.Visible;
                }

                // Update timestamp
                txtLastUpdate.Text = $"Ostatnia aktualizacja: {DateTime.Now:HH:mm:ss} (auto)";
            }
            catch
            {
                // Silent fail for auto-refresh
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                pnlLoading.Visibility = Visibility.Visible;

                // Initialize tables
                txtLoadingStatus.Text = "Tworzenie tabel w bazie danych...";
                await _service.EnsureTablesExistAsync();

                // Seed data if empty
                txtLoadingStatus.Text = "Ładowanie danych początkowych...";
                await _service.SeedDataIfEmptyAsync();

                // Load indicators
                txtLoadingStatus.Text = "Pobieranie wskaźników...";
                var indicators = await _service.GetDashboardIndicatorsAsync();
                icIndicators.ItemsSource = indicators;

                // Load alerts (critical + warning)
                txtLoadingStatus.Text = "Sprawdzanie alertów...";
                var allArticles = await _service.GetArticlesAsync();
                var alerts = allArticles
                    .Where(a => a.Severity == "critical" || a.Severity == "warning")
                    .OrderByDescending(a => a.Severity == "critical")
                    .ThenByDescending(a => a.PublishDate)
                    .Take(5)
                    .ToList();

                if (alerts.Any())
                {
                    icAlerts.ItemsSource = alerts;
                    pnlAlerts.Visibility = Visibility.Visible;
                }

                // Load articles
                txtLoadingStatus.Text = "Pobieranie artykułów...";
                await RefreshArticlesAsync();

                // Load side panels
                txtLoadingStatus.Text = "Ładowanie danych bocznych...";
                await LoadSidePanelsAsync();

                // Update timestamp
                txtLastUpdate.Text = $"Ostatnia aktualizacja: {DateTime.Now:HH:mm:ss}";

                pnlLoading.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                txtLoadingStatus.Text = $"Błąd: {ex.Message}";
                MessageBox.Show($"Błąd podczas ładowania danych:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RefreshArticlesAsync()
        {
            if (icArticles == null) return; // Guard against calls during initialization

            var articles = await _service.GetArticlesAsync(
                _currentCategory == "Wszystkie" ? null : _currentCategory,
                _currentSeverity == "Wszystkie" ? null : _currentSeverity,
                string.IsNullOrWhiteSpace(_searchText) ? null : _searchText
            );
            icArticles.ItemsSource = articles;
        }

        private async Task LoadSidePanelsAsync()
        {
            // === WYKRES: Historia cen skupu ===
            var priceHistory = await _service.GetPriceHistoryAsync("WolnyRynek", 60);
            if (priceHistory.Any())
            {
                var values = new ChartValues<double>(priceHistory.Select(p => (double)p.Value));
                var labels = priceHistory.Select(p => p.Date.ToString("dd.MM")).ToArray();

                chartPriceHistory.Series = new SeriesCollection
                {
                    new LineSeries
                    {
                        Title = "Wolny rynek",
                        Values = values,
                        Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1")),
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A6366F1")),
                        StrokeThickness = 2,
                        PointGeometry = DefaultGeometries.Circle,
                        PointGeometrySize = 6
                    }
                };
                axisXPriceHistory.Labels = labels;
            }

            // === WYKRES: Ceny elementów ===
            var prices = await _service.GetLatestPricesAsync();
            var elementTypes = new[] { "Filet", "Podudzie", "Udko", "Skrzydlo", "TuszkaHurt", "Cwiartka", "Korpus" };
            var elementLabels = new[] { "Filet", "Podudzie", "Udko", "Skrzydło", "Tuszka", "Ćwiartka", "Korpus" };
            var elementValues = new ChartValues<double>();

            foreach (var type in elementTypes)
            {
                var price = prices.FirstOrDefault(p => p.PriceType == type);
                elementValues.Add(price != null ? (double)price.Value : 0);
            }

            chartElements.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "zł/kg",
                    Values = elementValues,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E")),
                    MaxColumnWidth = 30
                }
            };
            axisXElements.Labels = elementLabels;

            // === Ceny pasz (lista) ===
            var feedPrices = await _service.GetLatestFeedPricesAsync();
            pnlFeedPrices.Children.Clear();

            foreach (var feed in feedPrices)
            {
                var row = new Border
                {
                    Background = FindResource("BgTertiary") as Brush,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(0, 0, 0, 4)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var nameText = new TextBlock
                {
                    Text = feed.Commodity,
                    Foreground = FindResource("TextSecondary") as Brush,
                    FontSize = 12
                };
                Grid.SetColumn(nameText, 0);

                var valueText = new TextBlock
                {
                    Text = $"{feed.Value:N2} {feed.Unit}",
                    Foreground = FindResource("TextPrimary") as Brush,
                    FontWeight = FontWeights.SemiBold
                };
                Grid.SetColumn(valueText, 1);

                grid.Children.Add(nameText);
                grid.Children.Add(valueText);
                row.Child = grid;
                pnlFeedPrices.Children.Add(row);
            }

            // === Konkurencja ===
            var competitors = await _service.GetCompetitorsAsync();
            icCompetitors.ItemsSource = competitors;

            // === HPAI ===
            var hpai = await _service.GetHpaiOutbreaksAsync("PL");
            var totalOutbreaks = await _service.GetTotalHpaiOutbreaks2026Async();
            txtHpaiSummary.Text = $"Ogniska HPAI 2026: {totalOutbreaks} w Polsce";
            icHpai.ItemsSource = hpai.Take(10);
            DrawHpaiMarkers(hpai);

            // === WYKRES: EU Benchmark ===
            var benchmark = await _service.GetEuBenchmarkAsync();
            icBenchmark.ItemsSource = benchmark;

            if (benchmark.Any())
            {
                var benchmarkValues = new ChartValues<double>(benchmark.Select(b => (double)b.PricePer100kg));
                var benchmarkLabels = benchmark.Select(b => b.Country).ToArray();

                // Kolorowanie słupków - Polska na zielono, reszta na szaro/niebiesko
                var benchmarkColors = benchmark.Select(b =>
                    b.Country == "PL"
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"))
                        : b.Country == "UA" || b.Country == "BR"
                            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"))
                            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"))
                ).ToList();

                chartEuBenchmark.Series = new SeriesCollection
                {
                    new ColumnSeries
                    {
                        Title = "EUR/100kg",
                        Values = benchmarkValues,
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1")),
                        MaxColumnWidth = 25
                    }
                };
                axisXBenchmark.Labels = benchmarkLabels;
            }
        }

        private void DrawHpaiMarkers(IEnumerable<IntelHpaiOutbreak> outbreaks)
        {
            // Region coordinates on the canvas (approximate positions for Polish voivodeships)
            var regionCoords = new Dictionary<string, (double X, double Y)>(StringComparer.OrdinalIgnoreCase)
            {
                { "zachodniopomorskie", (55, 35) },
                { "pomorskie", (95, 25) },
                { "warmińsko-mazurskie", (145, 35) },
                { "podlaskie", (170, 60) },
                { "lubuskie", (40, 70) },
                { "wielkopolskie", (75, 80) },
                { "kujawsko-pomorskie", (100, 55) },
                { "mazowieckie", (135, 80) },
                { "łódzkie", (105, 100) },
                { "dolnośląskie", (55, 110) },
                { "opolskie", (75, 125) },
                { "śląskie", (90, 135) },
                { "świętokrzyskie", (120, 120) },
                { "lubelskie", (160, 110) },
                { "małopolskie", (110, 145) },
                { "podkarpackie", (150, 140) }
            };

            // Remove existing HPAI markers (keep elements with Tag != "hpai")
            var toRemove = canvasPolandMap.Children.OfType<System.Windows.Shapes.Ellipse>()
                .Where(e => e.Tag?.ToString() == "hpai")
                .ToList();
            foreach (var el in toRemove)
                canvasPolandMap.Children.Remove(el);

            // Group outbreaks by region and draw markers
            var grouped = outbreaks.GroupBy(o => o.Region.ToLowerInvariant()).ToList();

            foreach (var group in grouped)
            {
                if (regionCoords.TryGetValue(group.Key, out var coords))
                {
                    var totalOutbreaks = group.Sum(o => o.OutbreakCount);
                    var size = Math.Min(6 + totalOutbreaks * 2, 20); // Size based on outbreak count

                    var marker = new System.Windows.Shapes.Ellipse
                    {
                        Width = size,
                        Height = size,
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")),
                        Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCA5A5")),
                        StrokeThickness = 1,
                        Tag = "hpai",
                        ToolTip = $"🦠 {group.First().Region}: {totalOutbreaks} ognisk"
                    };

                    Canvas.SetLeft(marker, coords.X - size / 2);
                    Canvas.SetTop(marker, coords.Y - size / 2);
                    canvasPolandMap.Children.Add(marker);
                }
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd odświeżania danych:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbCategory?.SelectedItem is ComboBoxItem item)
            {
                _currentCategory = item.Content?.ToString() ?? "Wszystkie";
                try
                {
                    await RefreshArticlesAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Błąd filtrowania: {ex.Message}");
                }
            }
        }

        private async void CmbSeverity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSeverity?.SelectedItem is ComboBoxItem item)
            {
                _currentSeverity = item.Content?.ToString() ?? "Wszystkie";
                try
                {
                    await RefreshArticlesAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Błąd filtrowania: {ex.Message}");
                }
            }
        }

        private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = txtSearch?.Text ?? "";
            try
            {
                await RefreshArticlesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wyszukiwania: {ex.Message}");
            }
        }
    }

    // Converter for positive values
    public class PositiveConverter : IValueConverter
    {
        public static readonly PositiveConverter Instance = new PositiveConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d)
                return d > 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
