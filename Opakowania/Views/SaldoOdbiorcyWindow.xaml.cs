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

        public SaldoOdbiorcyWindow(int kontrahentId, string kontrahentNazwa, string userId)
        {
            InitializeComponent();

            _viewModel = new SaldoOdbiorcyViewModel(kontrahentId, kontrahentNazwa, userId);
            DataContext = _viewModel;

            // Inicjalizuj wykres po załadowaniu okna
            Loaded += SaldoOdbiorcyWindow_Loaded;

            // Subskrybuj zmiany w ViewModel, aby aktualizować wykres
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void SaldoOdbiorcyWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InicjalizujWykres();
            AktualizujWykres();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SaldoOdbiorcyViewModel.SaldaTygodniowe))
            {
                AktualizujWykres();
            }
        }

        private void InicjalizujWykres()
        {
            if (saldaChart == null) return;

            saldaChart.BackColor = Color.White;
            saldaChart.BorderlineColor = Color.FromArgb(220, 220, 220);
            saldaChart.BorderlineDashStyle = ChartDashStyle.Solid;
            saldaChart.BorderlineWidth = 1;
            saldaChart.AntiAliasing = AntiAliasingStyles.All;
            saldaChart.TextAntiAliasingQuality = TextAntiAliasingQuality.High;

            // Obszar wykresu
            var chartArea = new ChartArea("MainArea")
            {
                BackColor = Color.White,
                BackSecondaryColor = Color.FromArgb(250, 250, 250),
                BackGradientStyle = GradientStyle.TopBottom
            };

            // Oś X
            chartArea.AxisX.Title = "Tydzień (niedziela)";
            chartArea.AxisX.TitleFont = new Font("Segoe UI Semibold", 9F);
            chartArea.AxisX.TitleForeColor = Color.FromArgb(60, 60, 60);
            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 8F);
            chartArea.AxisX.LabelStyle.ForeColor = Color.FromArgb(80, 80, 80);
            chartArea.AxisX.MajorGrid.LineColor = Color.FromArgb(230, 230, 230);
            chartArea.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            chartArea.AxisX.LineColor = Color.FromArgb(180, 180, 180);
            chartArea.AxisX.LabelStyle.Angle = -45;
            chartArea.AxisX.Interval = 1;

            // Oś Y
            chartArea.AxisY.Title = "Saldo";
            chartArea.AxisY.TitleFont = new Font("Segoe UI Semibold", 9F);
            chartArea.AxisY.TitleForeColor = Color.FromArgb(60, 60, 60);
            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 8F);
            chartArea.AxisY.LabelStyle.ForeColor = Color.FromArgb(80, 80, 80);
            chartArea.AxisY.MajorGrid.LineColor = Color.FromArgb(230, 230, 230);
            chartArea.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            chartArea.AxisY.LineColor = Color.FromArgb(180, 180, 180);
            chartArea.AxisY.IsStartedFromZero = true;

            // Marginesy
            chartArea.Position = new ElementPosition(3, 3, 94, 94);
            chartArea.InnerPlotPosition = new ElementPosition(10, 5, 88, 80);

            saldaChart.ChartAreas.Clear();
            saldaChart.ChartAreas.Add(chartArea);

            // Legenda
            var legend = new Legend
            {
                Name = "MainLegend",
                Docking = Docking.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(60, 60, 60),
                Alignment = StringAlignment.Center
            };
            saldaChart.Legends.Clear();
            saldaChart.Legends.Add(legend);
        }

        private void AktualizujWykres()
        {
            if (saldaChart == null || _viewModel.SaldaTygodniowe == null) return;

            saldaChart.Series.Clear();

            var dane = _viewModel.SaldaTygodniowe.ToList();
            if (!dane.Any()) return;

            // Seria E2
            var seriesE2 = new Series("E2")
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 3,
                Color = Color.FromArgb(52, 152, 219), // Niebieski
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 8,
                MarkerColor = Color.FromArgb(52, 152, 219),
                MarkerBorderColor = Color.White,
                MarkerBorderWidth = 2,
                ToolTip = "#SERIESNAME\n#VALX\nSaldo: #VALY",
                IsVisibleInLegend = true
            };

            // Seria H1
            var seriesH1 = new Series("H1")
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 3,
                Color = Color.FromArgb(230, 126, 34), // Pomarańczowy
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 8,
                MarkerColor = Color.FromArgb(230, 126, 34),
                MarkerBorderColor = Color.White,
                MarkerBorderWidth = 2,
                ToolTip = "#SERIESNAME\n#VALX\nSaldo: #VALY",
                IsVisibleInLegend = true
            };

            foreach (var punkt in dane)
            {
                var etykieta = $"Tydz.{punkt.NumerTygodnia}\n{punkt.DataText}";
                seriesE2.Points.AddXY(etykieta, punkt.SaldoE2);
                seriesH1.Points.AddXY(etykieta, punkt.SaldoH1);
            }

            // Dodaj etykiety na końcu linii
            if (seriesE2.Points.Count > 0)
            {
                var lastPointE2 = seriesE2.Points[seriesE2.Points.Count - 1];
                lastPointE2.IsValueShownAsLabel = true;
                lastPointE2.LabelBackColor = Color.White;
                lastPointE2.LabelBorderColor = seriesE2.Color;
                lastPointE2.LabelBorderWidth = 1;
                lastPointE2.Font = new Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
                lastPointE2.LabelForeColor = seriesE2.Color;
            }

            if (seriesH1.Points.Count > 0)
            {
                var lastPointH1 = seriesH1.Points[seriesH1.Points.Count - 1];
                lastPointH1.IsValueShownAsLabel = true;
                lastPointH1.LabelBackColor = Color.White;
                lastPointH1.LabelBorderColor = seriesH1.Color;
                lastPointH1.LabelBorderWidth = 1;
                lastPointH1.Font = new Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
                lastPointH1.LabelForeColor = seriesH1.Color;
            }

            saldaChart.Series.Add(seriesE2);
            saldaChart.Series.Add(seriesH1);

            // Dostosuj skalę osi Y
            var allValues = dane.SelectMany(d => new[] { d.SaldoE2, d.SaldoH1 }).ToList();
            if (allValues.Any())
            {
                var min = allValues.Min();
                var max = allValues.Max();
                var range = max - min;
                var margin = Math.Max(range * 0.15, 10);

                saldaChart.ChartAreas[0].AxisY.Minimum = Math.Floor(min - margin);
                saldaChart.ChartAreas[0].AxisY.Maximum = Math.Ceiling(max + margin);
            }

            saldaChart.Invalidate();
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
