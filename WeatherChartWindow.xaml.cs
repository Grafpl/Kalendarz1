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
    public partial class WeatherChartWindow : Window, INotifyPropertyChanged
    {
        private ChartValues<int> _tempValues;
        public ChartValues<int> TempValues
        {
            get => _tempValues;
            set { _tempValues = value; OnPropertyChanged(); }
        }

        private List<string> _timeLabels;
        public List<string> TimeLabels
        {
            get => _timeLabels;
            set { _timeLabels = value; OnPropertyChanged(); }
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

        public WeatherChartWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            DataContext = this;

            YFormatter = value => $"{value:0}°C";
            LabelFormatter = point => $"{point.Y:0}°C";
            TempValues = new ChartValues<int>();
            TimeLabels = new List<string>();

            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pobierz aktualną pogodę
                var weather = await WeatherManager.GetWeatherAsync();

                if (weather != null)
                {
                    txtAktualnaTemp.Text = $"{weather.Temperature}";
                    txtAktualnyOpis.Text = weather.Description;
                }

                // Pobierz prognozę godzinową (4 dni co 5 godzin)
                var hourlyForecast = await WeatherManager.GetHourlyForecastAsync();

                if (hourlyForecast != null && hourlyForecast.Count > 0)
                {
                    // Wypełnij wykres
                    TempValues = new ChartValues<int>(hourlyForecast.Select(f => f.Temperature));

                    // Etykiety - data i godzina
                    TimeLabels = hourlyForecast.Select(f =>
                    {
                        var dayName = PolishCulture.DateTimeFormat.GetAbbreviatedDayName(f.DateTime.DayOfWeek);
                        dayName = char.ToUpper(dayName[0]) + dayName.Substring(1);
                        return $"{dayName}\n{f.DateTime:dd.MM}\n{f.DateTime:HH:00}";
                    }).ToList();

                    // Statystyki
                    var minTemp = hourlyForecast.Min(f => f.Temperature);
                    var maxTemp = hourlyForecast.Max(f => f.Temperature);
                    var avgTemp = hourlyForecast.Average(f => f.Temperature);

                    var minEntry = hourlyForecast.First(f => f.Temperature == minTemp);
                    var maxEntry = hourlyForecast.First(f => f.Temperature == maxTemp);

                    txtMinTemp.Text = $"{minTemp}";
                    txtMinDzien.Text = $"{minEntry.DateTime:ddd dd.MM HH:00}";

                    txtMaxTemp.Text = $"{maxTemp}";
                    txtMaxDzien.Text = $"{maxEntry.DateTime:ddd dd.MM HH:00}";

                    txtSredniaTemp.Text = $"{avgTemp:0}";
                    txtAmplituda.Text = $"{maxTemp - minTemp}";

                    // Zakres osi Y (z marginesem)
                    var margin = Math.Max(3, (maxTemp - minTemp) * 0.2);
                    YAxisMin = minTemp - margin;
                    YAxisMax = maxTemp + margin;

                    // Szczegółowa prognoza w stopce (wybrane godziny - co 2-3 wpisy)
                    var step = Math.Max(1, hourlyForecast.Count / 8);
                    var forecastItems = new List<ForecastItem>();
                    for (int i = 0; i < hourlyForecast.Count; i += step)
                    {
                        var f = hourlyForecast[i];
                        var dayName = PolishCulture.DateTimeFormat.GetAbbreviatedDayName(f.DateTime.DayOfWeek);
                        dayName = char.ToUpper(dayName[0]) + dayName.Substring(1);
                        forecastItems.Add(new ForecastItem
                        {
                            DayName = $"{dayName} {f.DateTime:dd.MM}",
                            Time = f.DateTime.ToString("HH:00"),
                            Icon = f.Icon,
                            Temperature = $"{f.Temperature}°C",
                            Description = f.Description
                        });
                    }

                    forecastPanel.ItemsSource = forecastItems.Take(8).ToList();
                }
                else
                {
                    txtAktualnaTemp.Text = "--";
                    txtAktualnyOpis.Text = "Brak danych pogodowych";
                }
            }
            catch (Exception ex)
            {
                txtAktualnaTemp.Text = "--";
                txtAktualnyOpis.Text = "Błąd podczas ładowania";
                MessageBox.Show($"Błąd podczas ładowania danych pogodowych: {ex.Message}", "Błąd",
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

        // Klasa pomocnicza dla ItemsControl
        public class ForecastItem
        {
            public string DayName { get; set; }
            public string Time { get; set; }
            public string Icon { get; set; }
            public string Temperature { get; set; }
            public string Description { get; set; }
        }
    }
}
