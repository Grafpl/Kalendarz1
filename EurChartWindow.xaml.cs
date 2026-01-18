using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using LiveCharts;
using LiveCharts.Wpf;

namespace Kalendarz1
{
    public partial class EurChartWindow : Window, INotifyPropertyChanged
    {
        private ChartValues<decimal> _eurValues;
        public ChartValues<decimal> EurValues
        {
            get => _eurValues;
            set { _eurValues = value; OnPropertyChanged(); }
        }

        private List<string> _dateLabels;
        public List<string> DateLabels
        {
            get => _dateLabels;
            set { _dateLabels = value; OnPropertyChanged(); }
        }

        private double _yAxisMin = double.NaN;
        public double YAxisMin
        {
            get => _yAxisMin;
            set { _yAxisMin = value; OnPropertyChanged(); }
        }

        private double _yAxisMax = double.NaN;
        public double YAxisMax
        {
            get => _yAxisMax;
            set { _yAxisMax = value; OnPropertyChanged(); }
        }

        public Func<double, string> YFormatter { get; set; }
        public Func<ChartPoint, string> LabelFormatter { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static readonly CultureInfo PolishCulture = new CultureInfo("pl-PL");

        public EurChartWindow()
        {
            InitializeComponent();
            DataContext = this;

            YFormatter = value => value.ToString("F4") + " PLN";
            LabelFormatter = point => point.Y.ToString("F4");
            EurValues = new ChartValues<decimal>();
            DateLabels = new List<string>();

            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var history = await CurrencyManager.GetEurHistoryAsync();

                if (history.Count > 0)
                {
                    // Wypelnij wykres
                    EurValues = new ChartValues<decimal>(history.Select(h => h.Rate));

                    // Pelne daty z dniem tygodnia
                    DateLabels = history.Select(h =>
                        h.Date.ToString("ddd dd.MM", PolishCulture)).ToList();

                    // Oblicz statystyki
                    var minItem = history.OrderBy(h => h.Rate).First();
                    var maxItem = history.OrderByDescending(h => h.Rate).First();
                    var firstItem = history.First();
                    var lastItem = history.Last();
                    var avgRate = history.Average(h => h.Rate);

                    // Ustaw zakres osi Y (z marginesem)
                    var minRate = (double)history.Min(h => h.Rate);
                    var maxRate = (double)history.Max(h => h.Rate);
                    // Minimalny margines 0.01 PLN gdy wszystkie kursy są identyczne
                    var margin = Math.Max(0.01, (maxRate - minRate) * 0.1);
                    YAxisMin = minRate - margin;
                    YAxisMax = maxRate + margin;

                    // Aktualizuj UI - aktualny kurs
                    txtAktualnyKurs.Text = $"{lastItem.Rate:F4} PLN";
                    txtAktualnaData.Text = lastItem.Date.ToString("dddd, dd MMMM yyyy", PolishCulture);

                    // Minimum
                    txtMinKurs.Text = $"{minItem.Rate:F4} PLN";
                    txtMinData.Text = minItem.Date.ToString("dddd, dd.MM.yyyy", PolishCulture);

                    // Maximum
                    txtMaxKurs.Text = $"{maxItem.Rate:F4} PLN";
                    txtMaxData.Text = maxItem.Date.ToString("dddd, dd.MM.yyyy", PolishCulture);

                    // Srednia
                    txtSredniaKurs.Text = $"{avgRate:F4} PLN";

                    // Zmiana w okresie
                    var zmiana = lastItem.Rate - firstItem.Rate;
                    var zmianaProc = (zmiana / firstItem.Rate) * 100;
                    var zmianaSign = zmiana >= 0 ? "+" : "";
                    txtZmiana.Text = $"{zmianaSign}{zmiana:F4} PLN";
                    txtZmiana.Foreground = zmiana >= 0
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96))
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
                    txtZmianaProc.Text = $"{zmianaSign}{zmianaProc:F2}% od {firstItem.Date:dd.MM.yyyy}";

                    // Liczba dni
                    txtLiczbaDni.Text = $"Liczba dni roboczych: {history.Count}";
                    txtStatus.Text = $"Dane z okresu {firstItem.Date:dd.MM.yyyy} - {lastItem.Date:dd.MM.yyyy}";
                }
                else
                {
                    txtAktualnyKurs.Text = "Brak danych";
                    txtStatus.Text = "Nie udalo sie pobrac danych";
                }
            }
            catch (Exception ex)
            {
                txtAktualnyKurs.Text = "Blad";
                txtStatus.Text = "Blad podczas ladowania";
                MessageBox.Show($"Blad podczas ladowania danych: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
