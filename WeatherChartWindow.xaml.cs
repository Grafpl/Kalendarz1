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
        private ChartValues<int> _tempMaxValues;
        public ChartValues<int> TempMaxValues
        {
            get => _tempMaxValues;
            set { _tempMaxValues = value; OnPropertyChanged(); }
        }

        private ChartValues<int> _tempMinValues;
        public ChartValues<int> TempMinValues
        {
            get => _tempMinValues;
            set { _tempMinValues = value; OnPropertyChanged(); }
        }

        private List<string> _dayLabels;
        public List<string> DayLabels
        {
            get => _dayLabels;
            set { _dayLabels = value; OnPropertyChanged(); }
        }

        private double _yAxisMin;
        public double YAxisMin
        {
            get => _yAxisMin;
            set { _yAxisMin = value; OnPropertyChanged(); }
        }

        private double _yAxisMax;
        public double YAxisMax
        {
            get => _yAxisMax;
            set { _yAxisMax = value; OnPropertyChanged(); }
        }

        public Func<double, string> YFormatter { get; set; }
        public Func<ChartPoint, string> LabelFormatterMax { get; set; }
        public Func<ChartPoint, string> LabelFormatterMin { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static readonly CultureInfo PolishCulture = new CultureInfo("pl-PL");

        public WeatherChartWindow()
        {
            InitializeComponent();
            DataContext = this;

            YFormatter = value => $"{value:0}C";
            LabelFormatterMax = point => $"{point.Y:0}C";
            LabelFormatterMin = point => $"{point.Y:0}C";
            TempMaxValues = new ChartValues<int>();
            TempMinValues = new ChartValues<int>();
            DayLabels = new List<string>();

            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var weather = await WeatherManager.GetWeatherAsync();

                if (weather != null && weather.Forecast.Count > 0)
                {
                    // Aktualna pogoda
                    txtAktualnaTemp.Text = $"{weather.Temperature}";
                    txtAktualnyOpis.Text = weather.Description;

                    // Wypelnij wykres
                    TempMaxValues = new ChartValues<int>(weather.Forecast.Select(f => f.TempMax));
                    TempMinValues = new ChartValues<int>(weather.Forecast.Select(f => f.TempMin));

                    // Etykiety dni - pelna data
                    var today = DateTime.Today;
                    DayLabels = weather.Forecast.Select((f, index) =>
                    {
                        var date = today.AddDays(index);
                        var dayName = PolishCulture.DateTimeFormat.GetDayName(date.DayOfWeek);
                        dayName = char.ToUpper(dayName[0]) + dayName.Substring(1);
                        return $"{dayName}\n{date:dd.MM}";
                    }).ToList();

                    // Statystyki
                    var minTemp = weather.Forecast.Min(f => f.TempMin);
                    var maxTemp = weather.Forecast.Max(f => f.TempMax);
                    var avgTemp = weather.Forecast.Average(f => (f.TempMin + f.TempMax) / 2.0);

                    var minDay = weather.Forecast.First(f => f.TempMin == minTemp);
                    var maxDay = weather.Forecast.First(f => f.TempMax == maxTemp);

                    txtMinTemp.Text = $"{minTemp}";
                    txtMinDzien.Text = minDay.DayName;

                    txtMaxTemp.Text = $"{maxTemp}";
                    txtMaxDzien.Text = maxDay.DayName;

                    txtSredniaTemp.Text = $"{avgTemp:0}";
                    txtAmplituda.Text = $"{maxTemp - minTemp}";

                    // Ustaw zakres osi Y (z marginesem)
                    var margin = Math.Max(3, (maxTemp - minTemp) * 0.2);
                    YAxisMin = minTemp - margin;
                    YAxisMax = maxTemp + margin;

                    // Szczegolowa prognoza w stopce
                    var forecastItems = weather.Forecast.Select((f, index) =>
                    {
                        var date = today.AddDays(index);
                        var dayName = PolishCulture.DateTimeFormat.GetDayName(date.DayOfWeek);
                        dayName = char.ToUpper(dayName[0]) + dayName.Substring(1);
                        return new ForecastItem
                        {
                            DayName = $"{dayName} {date:dd.MM}",
                            Icon = f.Icon,
                            TempRange = $"{f.TempMin}C / {f.TempMax}C",
                            Description = f.Description
                        };
                    }).ToList();

                    forecastPanel.ItemsSource = forecastItems;
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
                txtAktualnyOpis.Text = "Blad podczas ladowania";
                MessageBox.Show($"Blad podczas ladowania danych pogodowych: {ex.Message}", "Blad",
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
            public string Icon { get; set; }
            public string TempRange { get; set; }
            public string Description { get; set; }
        }
    }
}
