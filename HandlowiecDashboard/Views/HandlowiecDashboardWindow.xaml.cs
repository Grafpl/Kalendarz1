using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;
using Kalendarz1.HandlowiecDashboard.Models;
using Kalendarz1.HandlowiecDashboard.Services;

namespace Kalendarz1.HandlowiecDashboard.Views
{
    /// <summary>
    /// Dashboard Handlowca - analiza sprzedaży z porównaniem miesiąc do miesiąca
    /// </summary>
    public partial class HandlowiecDashboardWindow : Window
    {
        private readonly HandlowiecDashboardService _service;
        private string _wybranyHandlowiec;
        private bool _isInitializing = true;

        public HandlowiecDashboardWindow()
        {
            InitializeComponent();
            _service = new HandlowiecDashboardService();

            Loaded += async (s, e) => await InitializeAsync();
        }

        /// <summary>
        /// Inicjalizacja dashboardu
        /// </summary>
        private async Task InitializeAsync()
        {
            ShowLoading(true);

            try
            {
                // Pobierz listę handlowców
                var handlowcy = await _service.PobierzHandlowcowAsync();
                cmbHandlowiec.ItemsSource = handlowcy;

                // Ustaw domyślnie aktualnego użytkownika lub "Wszyscy"
                var currentUser = App.UserFullName;
                if (!string.IsNullOrEmpty(currentUser) && handlowcy.Contains(currentUser))
                {
                    cmbHandlowiec.SelectedItem = currentUser;
                    _wybranyHandlowiec = currentUser;
                }
                else
                {
                    cmbHandlowiec.SelectedIndex = 0;
                    _wybranyHandlowiec = "— Wszyscy —";
                }

                _isInitializing = false;

                // Załaduj dane
                await LoadDashboardDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        /// <summary>
        /// Ładuje wszystkie dane dashboardu
        /// </summary>
        private async Task LoadDashboardDataAsync()
        {
            ShowLoading(true);

            try
            {
                // Równoległe pobieranie danych
                var taskKPI = _service.PobierzKPIAsync(_wybranyHandlowiec);
                var taskMiesieczne = _service.PobierzDaneMiesieczneAsync(_wybranyHandlowiec, 12);
                var taskTopOdbiorcy = _service.PobierzTopOdbiorcowAsync(_wybranyHandlowiec, 10, 3);
                var taskPorownanie = _service.PobierzPorownanieOkresowAsync(_wybranyHandlowiec);
                var taskKategorie = _service.PobierzKategorieProduktowAsync(_wybranyHandlowiec, 1);
                var taskOstatnie = _service.PobierzOstatnieZamowieniaAsync(_wybranyHandlowiec, 15);

                await Task.WhenAll(taskKPI, taskMiesieczne, taskTopOdbiorcy, taskPorownanie, taskKategorie, taskOstatnie);

                // Aktualizuj KPI
                UpdateKPI(taskKPI.Result);

                // Aktualizuj wykres trendu
                UpdateTrendChart(taskMiesieczne.Result);

                // Aktualizuj top odbiorców
                dgTopOdbiorcy.ItemsSource = taskTopOdbiorcy.Result;

                // Aktualizuj porównanie miesięcy
                dgPorownanie.ItemsSource = taskPorownanie.Result;

                // Aktualizuj wykres kategorii
                UpdateKategorieChart(taskKategorie.Result);

                // Aktualizuj ostatnie zamówienia
                dgOstatnie.ItemsSource = taskOstatnie.Result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        /// <summary>
        /// Aktualizuje karty KPI
        /// </summary>
        private void UpdateKPI(HandlowiecKPI kpi)
        {
            // Zamówienia
            txtZamowienia.Text = kpi.LiczbaZamowienTekst;
            txtZamowieniaZmiana.Text = kpi.ZmianaZamowienTekst;
            txtZamowieniaZmiana.Foreground = kpi.ZmianaZamowienPozytywna
                ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
                : new SolidColorBrush(Color.FromRgb(231, 76, 60));

            // Kg
            txtKg.Text = kpi.SumaKgTekst;
            txtKgZmiana.Text = kpi.ZmianaKgTekst;
            txtKgZmiana.Foreground = kpi.ZmianaKgPozytywna
                ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
                : new SolidColorBrush(Color.FromRgb(231, 76, 60));

            // Wartość
            txtWartosc.Text = kpi.SumaWartoscTekst;
            txtWartoscZmiana.Text = kpi.ZmianaWartoscTekst;
            txtWartoscZmiana.Foreground = kpi.ZmianaWartoscPozytywna
                ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
                : new SolidColorBrush(Color.FromRgb(231, 76, 60));

            // Odbiorcy
            txtOdbiorcy.Text = kpi.LiczbaOdbiorcowTekst;
            txtOdbiorcyZmiana.Text = kpi.ZmianaOdbiorcowTekst;
            txtOdbiorcyZmiana.Foreground = kpi.ZmianaOdbiorcowPozytywna
                ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
                : new SolidColorBrush(Color.FromRgb(231, 76, 60));

            // Średnia
            txtSrednia.Text = kpi.SredniWartoscTekst;
        }

        /// <summary>
        /// Aktualizuje wykres trendu
        /// </summary>
        private void UpdateTrendChart(List<DaneMiesieczne> dane)
        {
            if (dane == null || dane.Count == 0)
            {
                chartTrend.Series.Clear();
                return;
            }

            // Etykiety osi X
            var etykiety = dane.Select(d => d.MiesiacKrotki).ToArray();
            axisXTrend.Labels = etykiety;

            // Wartości dla serii
            var wartosci = new ChartValues<double>(dane.Select(d => (double)(d.SumaWartosc / 1000))); // w tysiącach
            var kg = new ChartValues<double>(dane.Select(d => (double)(d.SumaKg / 1000))); // w tysiącach

            chartTrend.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Wartość (tys. zł)",
                    Values = wartosci,
                    Stroke = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                    StrokeThickness = 3,
                    Fill = Brushes.Transparent,
                    PointGeometrySize = 8,
                    LineSmoothness = 0.3
                },
                new ColumnSeries
                {
                    Title = "Kg (tys.)",
                    Values = kg,
                    Fill = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                    MaxColumnWidth = 25
                }
            };
        }

        /// <summary>
        /// Aktualizuje wykres kategorii produktów
        /// </summary>
        private void UpdateKategorieChart(List<KategoriaProduktow> kategorie)
        {
            if (kategorie == null || kategorie.Count == 0)
            {
                chartKategorie.Series.Clear();
                return;
            }

            var series = new SeriesCollection();
            var kolory = new[]
            {
                Color.FromRgb(39, 174, 96),   // Zielony - Świeże
                Color.FromRgb(52, 152, 219),  // Niebieski - Mrożonki
                Color.FromRgb(241, 196, 15),  // Żółty
                Color.FromRgb(231, 76, 60)    // Czerwony
            };

            int i = 0;
            foreach (var kat in kategorie)
            {
                series.Add(new PieSeries
                {
                    Title = kat.Nazwa,
                    Values = new ChartValues<double> { (double)kat.SumaKg },
                    Fill = new SolidColorBrush(kolory[i % kolory.Length]),
                    DataLabels = true,
                    LabelPoint = point => $"{point.Y:N0} kg"
                });
                i++;
            }

            chartKategorie.Series = series;
        }

        /// <summary>
        /// Zmiana wybranego handlowca
        /// </summary>
        private async void CmbHandlowiec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            _wybranyHandlowiec = cmbHandlowiec.SelectedItem?.ToString() ?? "— Wszyscy —";
            await LoadDashboardDataAsync();
        }

        /// <summary>
        /// Kliknięcie przycisku odświeżania
        /// </summary>
        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadDashboardDataAsync();
        }

        /// <summary>
        /// Pokazuje/ukrywa overlay ładowania
        /// </summary>
        private void ShowLoading(bool show)
        {
            loadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
