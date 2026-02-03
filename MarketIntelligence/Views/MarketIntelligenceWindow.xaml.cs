using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Kalendarz1.MarketIntelligence.Models;
using Kalendarz1.MarketIntelligence.Services;

namespace Kalendarz1.MarketIntelligence.Views
{
    public partial class MarketIntelligenceWindow : Window
    {
        private readonly MarketIntelligenceService _service;
        private string _currentCategory = "Wszystkie";
        private string _currentSeverity = "Wszystkie";
        private string _searchText = "";

        public MarketIntelligenceWindow()
        {
            InitializeComponent();
            _service = new MarketIntelligenceService();
            Loaded += MarketIntelligenceWindow_Loaded;
        }

        private async void MarketIntelligenceWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
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
            var articles = await _service.GetArticlesAsync(
                _currentCategory == "Wszystkie" ? null : _currentCategory,
                _currentSeverity == "Wszystkie" ? null : _currentSeverity,
                string.IsNullOrWhiteSpace(_searchText) ? null : _searchText
            );
            icArticles.ItemsSource = articles;
        }

        private async Task LoadSidePanelsAsync()
        {
            // Load prices
            var prices = await _service.GetLatestPricesAsync();
            pnlPrices.Children.Clear();

            var priceGroups = new[]
            {
                ("Skup żywca", new[] { "WolnyRynek", "Kontraktacja" }),
                ("Tuszka i elementy", new[] { "TuszkaHurt", "Filet", "Udko", "Skrzydlo", "Podudzie", "Cwiartka", "Korpus" })
            };

            foreach (var (groupName, types) in priceGroups)
            {
                var header = new TextBlock
                {
                    Text = groupName,
                    FontWeight = FontWeights.Bold,
                    Foreground = FindResource("TextPrimary") as System.Windows.Media.Brush,
                    Margin = new Thickness(0, 12, 0, 8)
                };
                pnlPrices.Children.Add(header);

                foreach (var type in types)
                {
                    var price = prices.FirstOrDefault(p => p.PriceType == type);
                    if (price != null)
                    {
                        var row = new Border
                        {
                            Background = FindResource("BgTertiary") as System.Windows.Media.Brush,
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(8, 6, 8, 6),
                            Margin = new Thickness(0, 0, 0, 4)
                        };

                        var grid = new Grid();
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        var nameText = new TextBlock
                        {
                            Text = price.PriceTypeDisplay,
                            Foreground = FindResource("TextSecondary") as System.Windows.Media.Brush,
                            FontSize = 12
                        };
                        Grid.SetColumn(nameText, 0);

                        var valueText = new TextBlock
                        {
                            Text = price.FormattedValue,
                            Foreground = FindResource("TextPrimary") as System.Windows.Media.Brush,
                            FontWeight = FontWeights.SemiBold
                        };
                        Grid.SetColumn(valueText, 1);

                        grid.Children.Add(nameText);
                        grid.Children.Add(valueText);
                        row.Child = grid;
                        pnlPrices.Children.Add(row);
                    }
                }
            }

            // Load feed prices
            var feedPrices = await _service.GetLatestFeedPricesAsync();
            var feedHeader = new TextBlock
            {
                Text = "Pasze (MATIF)",
                FontWeight = FontWeights.Bold,
                Foreground = FindResource("TextPrimary") as System.Windows.Media.Brush,
                Margin = new Thickness(0, 16, 0, 8)
            };
            pnlPrices.Children.Add(feedHeader);

            foreach (var feed in feedPrices)
            {
                var row = new Border
                {
                    Background = FindResource("BgTertiary") as System.Windows.Media.Brush,
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
                    Foreground = FindResource("TextSecondary") as System.Windows.Media.Brush,
                    FontSize = 12
                };
                Grid.SetColumn(nameText, 0);

                var valueText = new TextBlock
                {
                    Text = $"{feed.Value:N2} {feed.Unit}",
                    Foreground = FindResource("TextPrimary") as System.Windows.Media.Brush,
                    FontWeight = FontWeights.SemiBold
                };
                Grid.SetColumn(valueText, 1);

                grid.Children.Add(nameText);
                grid.Children.Add(valueText);
                row.Child = grid;
                pnlPrices.Children.Add(row);
            }

            // Load competitors
            var competitors = await _service.GetCompetitorsAsync();
            icCompetitors.ItemsSource = competitors;

            // Load HPAI
            var hpai = await _service.GetHpaiOutbreaksAsync("PL");
            var totalOutbreaks = await _service.GetTotalHpaiOutbreaks2026Async();
            txtHpaiSummary.Text = $"Ogniska HPAI 2026: {totalOutbreaks} w Polsce";
            icHpai.ItemsSource = hpai.Take(10);

            // Load EU Benchmark
            var benchmark = await _service.GetEuBenchmarkAsync();
            icBenchmark.ItemsSource = benchmark;
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadDataAsync();
        }

        private void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbCategory.SelectedItem is ComboBoxItem item)
            {
                _currentCategory = item.Content?.ToString() ?? "Wszystkie";
                _ = RefreshArticlesAsync();
            }
        }

        private void CmbSeverity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSeverity.SelectedItem is ComboBoxItem item)
            {
                _currentSeverity = item.Content?.ToString() ?? "Wszystkie";
                _ = RefreshArticlesAsync();
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = txtSearch.Text;
            _ = RefreshArticlesAsync();
        }

        private void Article_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is IntelArticle article)
            {
                var detail = new ArticleDetailWindow(article);
                detail.Owner = this;
                detail.ShowDialog();
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
