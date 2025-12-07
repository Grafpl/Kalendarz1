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
    /// Dashboard Handlowca - nowoczesny ciemny motyw z analiza sprzedazy i CRM
    /// </summary>
    public partial class HandlowiecDashboardWindow : Window
    {
        private readonly HandlowiecDashboardService _service;
        private string _wybranyHandlowiec;
        private bool _isInitializing = true;

        // Kolory dla wykresow
        private static readonly SolidColorBrush OrangeBrush = new SolidColorBrush(Color.FromRgb(244, 162, 97));
        private static readonly SolidColorBrush BlueBrush = new SolidColorBrush(Color.FromRgb(78, 168, 222));
        private static readonly SolidColorBrush GreenBrush = new SolidColorBrush(Color.FromRgb(39, 174, 96));
        private static readonly SolidColorBrush RedBrush = new SolidColorBrush(Color.FromRgb(231, 76, 60));
        private static readonly SolidColorBrush PurpleBrush = new SolidColorBrush(Color.FromRgb(155, 89, 182));
        private static readonly SolidColorBrush GrayBrush = new SolidColorBrush(Color.FromRgb(139, 148, 158));

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
                // Pobierz liste handlowcow
                var handlowcy = await _service.PobierzHandlowcowAsync();
                cmbHandlowiec.ItemsSource = handlowcy;

                // Ustaw domyslnie aktualnego uzytkownika lub "Wszyscy"
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

                // Zaladuj dane
                await LoadDashboardDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad inicjalizacji: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        /// <summary>
        /// Laduje wszystkie dane dashboardu
        /// </summary>
        private async Task LoadDashboardDataAsync()
        {
            ShowLoading(true);

            try
            {
                // Rownolegle pobieranie danych
                var taskDzienne = _service.PobierzDaneDzienneAsync(_wybranyHandlowiec, 14);
                var taskRegiony = _service.PobierzSprzedazRegionalnąAsync(_wybranyHandlowiec, 3);
                var task30Dni = _service.PobierzPodsumowanie30DniAsync(_wybranyHandlowiec);
                var taskMiesieczne = _service.PobierzDaneMiesieczneAsync(_wybranyHandlowiec, 12);
                var taskDostawa = _service.PobierzStatystykiDostawyAsync(_wybranyHandlowiec, 1);
                var taskTopOdbiorcy = _service.PobierzTopOdbiorcowAsync(_wybranyHandlowiec, 10, 3);
                var taskCRM = _service.PobierzCRMStatystykiAsync(App.UserID);
                var taskPorownanie = _service.PobierzPorownanieOkresowAsync(_wybranyHandlowiec);
                var taskSrednia = _service.PobierzSredniaZamowieniaDziennieAsync(_wybranyHandlowiec);
                // Nowe dane z Faktur i Zamowien
                var taskZamDzien = _service.PobierzZamowieniaNaDzienAsync(_wybranyHandlowiec);
                var taskTopProdukty = _service.PobierzTopProduktyAsync(_wybranyHandlowiec, 5, 30);

                await Task.WhenAll(taskDzienne, taskRegiony, task30Dni, taskMiesieczne,
                    taskDostawa, taskTopOdbiorcy, taskCRM, taskPorownanie, taskSrednia,
                    taskZamDzien, taskTopProdukty);

                // Aktualizuj sprzedaz dzienna
                UpdateDzienneChart(taskDzienne.Result);

                // Aktualizuj regiony
                dgRegiony.ItemsSource = taskRegiony.Result;

                // Aktualizuj 30-dniowe podsumowanie
                Update30DniSummary(task30Dni.Result);

                // Aktualizuj trend miesieczny
                UpdateTrendChart(taskMiesieczne.Result);

                // Aktualizuj typ dostawy
                UpdateDostawaChart(taskDostawa.Result);

                // Aktualizuj top odbiorcow
                dgTopOdbiorcy.ItemsSource = taskTopOdbiorcy.Result;

                // Aktualizuj CRM
                UpdateCRM(taskCRM.Result);

                // Aktualizuj porownanie miesiecy
                dgPorownanie.ItemsSource = taskPorownanie.Result;

                // Aktualizuj srednia dzienna
                UpdateSredniaDziennaChart(taskSrednia.Result);

                // Aktualizuj zamowienia dzis/jutro
                UpdateZamowieniaNaDzien(taskZamDzien.Result);

                // Aktualizuj top produkty
                dgTopProdukty.ItemsSource = taskTopProdukty.Result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        /// <summary>
        /// Aktualizuje wykres sprzedazy dziennej
        /// </summary>
        private void UpdateDzienneChart(List<DaneDzienne> dane)
        {
            if (dane == null || dane.Count == 0)
            {
                chartDzienne.Series?.Clear();
                return;
            }

            // Oblicz sume i zmiane
            var suma = dane.Sum(d => d.SumaWartosc);
            var ostatniTydzien = dane.Skip(Math.Max(0, dane.Count - 7)).Sum(d => d.SumaWartosc);
            var poprzedniTydzien = dane.Take(Math.Min(7, dane.Count)).Sum(d => d.SumaWartosc);
            var zmianaProcent = poprzedniTydzien > 0
                ? ((ostatniTydzien - poprzedniTydzien) / poprzedniTydzien) * 100
                : 0;

            txtSprzedazDziennaWartosc.Text = $"{suma / 1000:N1}k zl";
            txtSprzedazDziennaZmiana.Text = $"{(zmianaProcent >= 0 ? "+" : "")}{zmianaProcent:N1}% vs poprz.";
            txtSprzedazDziennaZmiana.Foreground = zmianaProcent >= 0 ? GreenBrush : RedBrush;

            // Etykiety osi X
            var etykiety = dane.Select(d => d.DataTekst).ToArray();
            axisXDzienne.Labels = etykiety;

            // Wartosci
            var wartosci = new ChartValues<double>(dane.Select(d => (double)(d.SumaWartosc / 1000)));

            chartDzienne.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Wartosc (tys. zl)",
                    Values = wartosci,
                    Fill = OrangeBrush,
                    MaxColumnWidth = 25
                }
            };
        }

        /// <summary>
        /// Aktualizuje podsumowanie 30-dniowe
        /// </summary>
        private void Update30DniSummary(Podsumowanie30Dni p)
        {
            txt30Sprzedaz.Text = p.SumaSprzedazyTekst;
            txt30Zamowienia.Text = p.ZamowieniaTekst;
            txt30Anulowane.Text = p.ZwrotyTekst;
            txt30Srednia.Text = p.SredniaTekst;
            txt30CenaKg.Text = p.SredniaCenaTekst;
        }

        /// <summary>
        /// Aktualizuje wykres trendu miesiecznego
        /// </summary>
        private void UpdateTrendChart(List<DaneMiesieczne> dane)
        {
            if (dane == null || dane.Count == 0)
            {
                chartTrend.Series?.Clear();
                return;
            }

            // Oblicz trend
            if (dane.Count >= 2)
            {
                var ostatni = dane.LastOrDefault();
                var przedostatni = dane.Count > 1 ? dane[dane.Count - 2] : null;

                if (ostatni != null)
                {
                    txtTrendWartosc.Text = $"{ostatni.SumaWartosc / 1000:N1}k";

                    if (przedostatni != null && przedostatni.SumaWartosc > 0)
                    {
                        var zmiana = ((ostatni.SumaWartosc - przedostatni.SumaWartosc) / przedostatni.SumaWartosc) * 100;
                        txtTrendZmiana.Text = $"{(zmiana >= 0 ? "+" : "")}{zmiana:N1}%";
                        txtTrendZmiana.Foreground = zmiana >= 0 ? GreenBrush : RedBrush;
                    }
                }
            }

            // Etykiety osi X
            var etykiety = dane.Select(d => d.MiesiacKrotki).ToArray();
            axisXTrend.Labels = etykiety;

            // Wartosci dla serii
            var wartosci = new ChartValues<double>(dane.Select(d => (double)(d.SumaWartosc / 1000)));
            var kg = new ChartValues<double>(dane.Select(d => (double)(d.SumaKg / 1000)));

            chartTrend.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Kg (tys.)",
                    Values = kg,
                    Fill = BlueBrush,
                    MaxColumnWidth = 20
                },
                new LineSeries
                {
                    Title = "Wartosc (tys. zl)",
                    Values = wartosci,
                    Stroke = OrangeBrush,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent,
                    PointGeometrySize = 6,
                    LineSmoothness = 0.3
                }
            };
        }

        /// <summary>
        /// Aktualizuje wykres typow dostawy
        /// </summary>
        private void UpdateDostawaChart(List<StatystykiDostawy> statystyki)
        {
            if (statystyki == null || statystyki.Count == 0)
            {
                chartDostawa.Series?.Clear();
                return;
            }

            var series = new SeriesCollection();
            var kolory = new[] { OrangeBrush, BlueBrush, GreenBrush, PurpleBrush, GrayBrush };

            int i = 0;
            foreach (var stat in statystyki)
            {
                series.Add(new PieSeries
                {
                    Title = stat.TypDostawy,
                    Values = new ChartValues<int> { stat.Liczba },
                    Fill = kolory[i % kolory.Length],
                    DataLabels = true,
                    LabelPoint = point => $"{stat.ProcentTekst}"
                });
                i++;
            }

            chartDostawa.Series = series;
        }

        /// <summary>
        /// Aktualizuje dane CRM
        /// </summary>
        private void UpdateCRM(CRMStatystyki crm)
        {
            txtCRMDzisiaj.Text = crm.KontaktyDzisiaj.ToString();
            txtCRMZalegle.Text = crm.KontaktyZalegle.ToString();
            txtCRMProby.Text = crm.ProbyKontaktu.ToString();
            txtCRMNawiazane.Text = crm.NawiazaneKontakty.ToString();
            txtCRMOferty.Text = crm.DoWyslaniOferty.ToString();

            // Podswietl zalegle na czerwono jesli sa
            if (crm.KontaktyZalegle > 0)
            {
                txtCRMZalegle.Foreground = RedBrush;
            }
        }

        /// <summary>
        /// Aktualizuje wykres sredniej wartosci zamowienia dziennie
        /// </summary>
        private void UpdateSredniaDziennaChart(List<SredniaZamowieniaDziennie> dane)
        {
            if (dane == null || dane.Count == 0)
            {
                chartSredniaDzienna.Series?.Clear();
                return;
            }

            // Etykiety osi X
            var etykiety = dane.Select(d => d.DzienTekst).ToArray();
            axisXSrednia.Labels = etykiety;

            // Wartosci
            var tenTydzien = new ChartValues<double>(dane.Select(d => (double)d.SredniaTenTydzien));
            var poprzedniTydzien = new ChartValues<double>(dane.Select(d => (double)d.SredniaPoprzedniTydzien));

            chartSredniaDzienna.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Poprzedni tydz.",
                    Values = poprzedniTydzien,
                    Fill = GrayBrush,
                    MaxColumnWidth = 15
                },
                new ColumnSeries
                {
                    Title = "Ten tydzien",
                    Values = tenTydzien,
                    Fill = OrangeBrush,
                    MaxColumnWidth = 15
                }
            };
        }

        /// <summary>
        /// Aktualizuje zamowienia na dzis/jutro
        /// </summary>
        private void UpdateZamowieniaNaDzien(ZamowieniaNaDzien zam)
        {
            txtZamDzisLiczba.Text = $"{zam.LiczbaZamowienDzis} zam.";
            txtZamDzisKg.Text = $"{zam.SumaKgDzis:N0} kg";
            txtZamJutroLiczba.Text = $"{zam.LiczbaZamowienJutro} zam.";
            txtZamJutroKg.Text = $"{zam.SumaKgJutro:N0} kg";
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
        /// Klikniecie przycisku odswiezania
        /// </summary>
        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadDashboardDataAsync();
        }

        /// <summary>
        /// Pokazuje/ukrywa overlay ladowania
        /// </summary>
        private void ShowLoading(bool show)
        {
            loadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
