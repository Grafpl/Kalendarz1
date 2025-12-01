using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms.DataVisualization.Charting;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    /// <summary>
    /// Okno szczegółów salda dla konkretnego odbiorcy
    /// </summary>
    public partial class SaldoOdbiorcyWindow : Window
    {
        private readonly SaldoOdbiorcyViewModel _viewModel;
        private Chart _chartE2;
        private Chart _chartH1;

        // Kolory w stylu Power BI
        private static readonly Color KolorTla = Color.FromArgb(45, 45, 48);
        private static readonly Color KolorE2 = Color.FromArgb(220, 53, 69); // Czerwony dla E2
        private static readonly Color KolorH1 = Color.FromArgb(150, 150, 150); // Szary dla H1
        private static readonly Color KolorSiatki = Color.FromArgb(80, 80, 80);
        private static readonly Color KolorTekstu = Color.FromArgb(200, 200, 200);
        private static readonly Color KolorZera = Color.White; // Biała linia dla Y=0

        private static readonly string[] NazwyMiesiecy = { "", "sty", "lut", "mar", "kwi", "maj", "cze",
                                                           "lip", "sie", "wrz", "paź", "lis", "gru" };

        public SaldoOdbiorcyWindow(int kontrahentId, string kontrahentNazwa, string userId)
        {
            InitializeComponent();

            _viewModel = new SaldoOdbiorcyViewModel(kontrahentId, kontrahentNazwa, userId);
            DataContext = _viewModel;

            Loaded += SaldoOdbiorcyWindow_Loaded;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void SaldoOdbiorcyWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InicjalizujWykresy();
            AktualizujWykresy();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SaldoOdbiorcyViewModel.SaldaTygodniowe))
            {
                AktualizujWykresy();
            }
        }

        private Chart UtworzWykres()
        {
            var chart = new Chart
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                BackColor = KolorTla,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High
            };

            var chartArea = new ChartArea("MainArea")
            {
                BackColor = KolorTla
            };

            // Oś X
            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 9F);
            chartArea.AxisX.LabelStyle.ForeColor = KolorTekstu;
            chartArea.AxisX.MajorGrid.LineColor = KolorSiatki;
            chartArea.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisX.LineColor = KolorSiatki;
            chartArea.AxisX.MajorTickMark.LineColor = KolorSiatki;
            chartArea.AxisX.IntervalType = DateTimeIntervalType.Number;
            chartArea.AxisX.Interval = 1;

            // Oś Y
            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 9F);
            chartArea.AxisY.LabelStyle.ForeColor = KolorTekstu;
            chartArea.AxisY.MajorGrid.LineColor = KolorSiatki;
            chartArea.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            chartArea.AxisY.LineColor = KolorSiatki;
            chartArea.AxisY.MajorTickMark.LineColor = KolorSiatki;
            chartArea.AxisY.LabelStyle.Format = "N0";

            // Marginesy
            chartArea.Position = new ElementPosition(0, 0, 100, 100);
            chartArea.InnerPlotPosition = new ElementPosition(8, 5, 90, 80);

            chart.ChartAreas.Add(chartArea);

            return chart;
        }

        private void InicjalizujWykresy()
        {
            if (chartHostE2 != null)
            {
                _chartE2 = UtworzWykres();
                chartHostE2.Child = _chartE2;
            }

            if (chartHostH1 != null)
            {
                _chartH1 = UtworzWykres();
                chartHostH1.Child = _chartH1;
            }
        }

        private void AktualizujWykresy()
        {
            if (_viewModel.SaldaTygodniowe == null) return;

            var dane = _viewModel.SaldaTygodniowe.ToList();
            if (!dane.Any()) return;

            if (_chartE2 != null)
            {
                RysujWykres(_chartE2, dane, d => d.SaldoE2, KolorE2);
            }

            if (_chartH1 != null)
            {
                RysujWykres(_chartH1, dane, d => d.SaldoH1, KolorH1);
            }
        }

        private void RysujWykres(Chart chart, System.Collections.Generic.List<Models.SaldoTygodniowe> dane,
            Func<Models.SaldoTygodniowe, int> selector, Color kolorLinii)
        {
            chart.Series.Clear();
            chart.Legends.Clear();
            chart.Titles.Clear();

            var chartArea = chart.ChartAreas[0];
            chartArea.AxisX.CustomLabels.Clear();
            chartArea.AxisY.StripLines.Clear();

            // Biała linia na poziomie Y=0
            var stripLine = new StripLine
            {
                IntervalOffset = 0,
                StripWidth = 0,
                BorderColor = KolorZera,
                BorderWidth = 2,
                BorderDashStyle = ChartDashStyle.Solid
            };
            chartArea.AxisY.StripLines.Add(stripLine);

            // Seria danych
            var series = new Series("Saldo")
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 3,
                Color = kolorLinii,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 10,
                MarkerColor = kolorLinii,
                MarkerBorderColor = Color.White,
                MarkerBorderWidth = 2,
                IsValueShownAsLabel = true,
                LabelForeColor = KolorTekstu,
                Font = new Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
            };

            // Dodaj punkty używając indeksów numerycznych
            int? poprzedniMiesiac = null;
            for (int i = 0; i < dane.Count; i++)
            {
                var punkt = dane[i];
                int wartosc = selector(punkt);
                int miesiac = punkt.DataNiedziela.Month;

                // Dodaj punkt z indeksem numerycznym jako X
                var dataPoint = new DataPoint(i, wartosc);
                dataPoint.Label = wartosc.ToString("N0");
                series.Points.Add(dataPoint);

                // Dodaj etykietę osi X
                string labelText;
                if (poprzedniMiesiac == null || poprzedniMiesiac != miesiac)
                {
                    // Pokaż numer tygodnia i nazwę miesiąca
                    labelText = $"{punkt.NumerTygodnia}\n{NazwyMiesiecy[miesiac]}";
                    poprzedniMiesiac = miesiac;
                }
                else
                {
                    // Tylko numer tygodnia
                    labelText = punkt.NumerTygodnia.ToString();
                }

                var customLabel = new CustomLabel(i - 0.5, i + 0.5, labelText, 0, LabelMarkStyle.None);
                chartArea.AxisX.CustomLabels.Add(customLabel);
            }

            chart.Series.Add(series);

            // Ustaw zakres osi X
            chartArea.AxisX.Minimum = -0.5;
            chartArea.AxisX.Maximum = dane.Count - 0.5;

            // Ustaw zakres osi Y
            var values = dane.Select(selector).ToList();
            if (values.Any())
            {
                int min = values.Min();
                int max = values.Max();
                int range = max - min;

                double yMin = min >= 0 ? 0 : min - range * 0.1;
                double yMax = max + range * 0.15;

                // Zaokrąglij do ładnych wartości
                if (range > 0)
                {
                    int magnitude = (int)Math.Pow(10, Math.Floor(Math.Log10(range)));
                    int step = (int)Math.Ceiling((double)range / 5 / magnitude) * magnitude;
                    if (step == 0) step = 1;

                    yMin = Math.Floor(yMin / step) * step;
                    yMax = Math.Ceiling(yMax / step) * step;

                    chartArea.AxisY.Minimum = yMin;
                    chartArea.AxisY.Maximum = yMax;
                    chartArea.AxisY.Interval = step;
                }
            }

            chart.Invalidate();
        }

        #region Obsługa okna

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnPowrot_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        #endregion
    }
}
