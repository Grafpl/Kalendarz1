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

        public SaldoOdbiorcyWindow(int kontrahentId, string kontrahentNazwa, string userId)
        {
            InitializeComponent();

            _viewModel = new SaldoOdbiorcyViewModel(kontrahentId, kontrahentNazwa, userId);
            DataContext = _viewModel;

            // Inicjalizuj wykresy po załadowaniu okna
            Loaded += SaldoOdbiorcyWindow_Loaded;

            // Subskrybuj zmiany w ViewModel, aby aktualizować wykresy
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

        private Chart UtworzWykres(Color kolorLinii, string nazwaOsi)
        {
            var chart = new Chart
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                BackColor = Color.White,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High
            };

            var chartArea = new ChartArea("MainArea")
            {
                BackColor = Color.White
            };

            // Oś X - kompaktowa
            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 7F);
            chartArea.AxisX.LabelStyle.ForeColor = Color.FromArgb(100, 100, 100);
            chartArea.AxisX.MajorGrid.LineColor = Color.FromArgb(240, 240, 240);
            chartArea.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisX.LineColor = Color.FromArgb(200, 200, 200);
            chartArea.AxisX.LabelStyle.Angle = -45;
            chartArea.AxisX.Interval = 1;

            // Oś Y - kompaktowa
            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 7F);
            chartArea.AxisY.LabelStyle.ForeColor = Color.FromArgb(100, 100, 100);
            chartArea.AxisY.MajorGrid.LineColor = Color.FromArgb(240, 240, 240);
            chartArea.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisY.LineColor = Color.FromArgb(200, 200, 200);
            chartArea.AxisY.IsStartedFromZero = false;

            // Marginesy - maksymalizacja przestrzeni wykresu
            chartArea.Position = new ElementPosition(0, 0, 100, 100);
            chartArea.InnerPlotPosition = new ElementPosition(8, 2, 90, 85);

            chart.ChartAreas.Add(chartArea);

            return chart;
        }

        private void InicjalizujWykresy()
        {
            // Wykres E2
            if (chartHostE2 != null)
            {
                _chartE2 = UtworzWykres(Color.FromArgb(52, 152, 219), "E2");
                chartHostE2.Child = _chartE2;
            }

            // Wykres H1
            if (chartHostH1 != null)
            {
                _chartH1 = UtworzWykres(Color.FromArgb(230, 126, 34), "H1");
                chartHostH1.Child = _chartH1;
            }
        }

        private void AktualizujWykresy()
        {
            if (_viewModel.SaldaTygodniowe == null) return;

            var dane = _viewModel.SaldaTygodniowe.ToList();
            if (!dane.Any()) return;

            // Aktualizuj wykres E2
            if (_chartE2 != null)
            {
                AktualizujPojedynczyWykres(_chartE2, dane, d => d.SaldoE2, Color.FromArgb(52, 152, 219), "E2");
            }

            // Aktualizuj wykres H1
            if (_chartH1 != null)
            {
                AktualizujPojedynczyWykres(_chartH1, dane, d => d.SaldoH1, Color.FromArgb(230, 126, 34), "H1");
            }
        }

        private void AktualizujPojedynczyWykres(Chart chart, System.Collections.Generic.List<Models.SaldoTygodniowe> dane,
            Func<Models.SaldoTygodniowe, int> selector, Color kolor, string nazwa)
        {
            chart.Series.Clear();

            var series = new Series(nazwa)
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 2,
                Color = kolor,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 6,
                MarkerColor = kolor,
                MarkerBorderColor = Color.White,
                MarkerBorderWidth = 1,
                ToolTip = "#VALX\nSaldo: #VALY"
            };

            foreach (var punkt in dane)
            {
                var etykieta = $"T{punkt.NumerTygodnia}";
                series.Points.AddXY(etykieta, selector(punkt));
            }

            // Etykieta na ostatnim punkcie
            if (series.Points.Count > 0)
            {
                var lastPoint = series.Points[series.Points.Count - 1];
                lastPoint.IsValueShownAsLabel = true;
                lastPoint.LabelBackColor = Color.White;
                lastPoint.LabelBorderColor = kolor;
                lastPoint.LabelBorderWidth = 1;
                lastPoint.Font = new Font("Segoe UI", 7F, System.Drawing.FontStyle.Bold);
                lastPoint.LabelForeColor = kolor;
            }

            chart.Series.Add(series);

            // Dostosuj skalę osi Y
            var values = dane.Select(selector).ToList();
            if (values.Any())
            {
                var min = values.Min();
                var max = values.Max();
                var range = max - min;
                var margin = Math.Max(range * 0.2, 5);

                chart.ChartAreas[0].AxisY.Minimum = Math.Floor(min - margin);
                chart.ChartAreas[0].AxisY.Maximum = Math.Ceiling(max + margin);
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
