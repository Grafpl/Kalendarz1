using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using LiveCharts;

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

        public Func<double, string> YFormatter { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public EurChartWindow()
        {
            InitializeComponent();
            DataContext = this;

            YFormatter = value => value.ToString("F4") + " PLN";
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
                    DateLabels = history.Select(h => h.Date.ToString("dd.MM")).ToList();

                    // Oblicz statystyki
                    var minItem = history.OrderBy(h => h.Rate).First();
                    var maxItem = history.OrderByDescending(h => h.Rate).First();
                    var lastItem = history.Last();
                    var avgRate = history.Average(h => h.Rate);

                    // Aktualizuj UI
                    txtAktualnyKurs.Text = $"{lastItem.Rate:F4}";
                    txtAktualnaData.Text = lastItem.Date.ToString("dd.MM.yyyy");

                    txtMinKurs.Text = $"{minItem.Rate:F4}";
                    txtMinData.Text = minItem.Date.ToString("dd.MM.yyyy");

                    txtMaxKurs.Text = $"{maxItem.Rate:F4}";
                    txtMaxData.Text = maxItem.Date.ToString("dd.MM.yyyy");

                    txtSredniaKurs.Text = $"{avgRate:F4}";
                }
                else
                {
                    txtAktualnyKurs.Text = "Brak danych";
                }
            }
            catch (Exception ex)
            {
                txtAktualnyKurs.Text = "Blad";
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
