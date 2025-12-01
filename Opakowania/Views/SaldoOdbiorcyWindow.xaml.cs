using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
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
        private static readonly Color KolorLiniiE2 = Color.FromArgb(237, 125, 49); // Pomarańczowy
        private static readonly Color KolorLiniiH1 = Color.FromArgb(237, 125, 49); // Pomarańczowy
        private static readonly Color KolorSiatki = Color.FromArgb(80, 80, 80);
        private static readonly Color KolorTekstu = Color.FromArgb(200, 200, 200);

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

        private Chart UtworzWykresPowerBI(string tytul)
        {
            var chart = new Chart
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                BackColor = KolorTla,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High
            };

            // Tytuł wykresu
            var title = new Title
            {
                Text = tytul,
                Font = new Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular),
                ForeColor = KolorTekstu,
                Docking = Docking.Top,
                Alignment = ContentAlignment.MiddleLeft
            };
            chart.Titles.Add(title);

            var chartArea = new ChartArea("MainArea")
            {
                BackColor = KolorTla
            };

            // Oś X - tygodnie z miesiącami
            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 8F);
            chartArea.AxisX.LabelStyle.ForeColor = KolorTekstu;
            chartArea.AxisX.MajorGrid.LineColor = KolorSiatki;
            chartArea.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisX.LineColor = KolorSiatki;
            chartArea.AxisX.LabelStyle.Angle = 0;
            chartArea.AxisX.Interval = 1;
            chartArea.AxisX.MajorTickMark.LineColor = KolorSiatki;

            // Oś Y - wartości
            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 8F);
            chartArea.AxisY.LabelStyle.ForeColor = KolorTekstu;
            chartArea.AxisY.MajorGrid.LineColor = KolorSiatki;
            chartArea.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            chartArea.AxisY.LineColor = KolorSiatki;
            chartArea.AxisY.IsStartedFromZero = true;
            chartArea.AxisY.MajorTickMark.LineColor = KolorSiatki;
            chartArea.AxisY.LabelStyle.Format = "#,##0";

            // Marginesy
            chartArea.Position = new ElementPosition(0, 8, 100, 92);
            chartArea.InnerPlotPosition = new ElementPosition(8, 5, 90, 75);

            chart.ChartAreas.Add(chartArea);

            return chart;
        }

        private void InicjalizujWykresy()
        {
            // Wykres E2
            if (chartHostE2 != null)
            {
                _chartE2 = UtworzWykresPowerBI("Ilość pojemników E2 które \"wiszą\" nam odbiorcy w tygodniach");
                chartHostE2.Child = _chartE2;
            }

            // Wykres H1
            if (chartHostH1 != null)
            {
                _chartH1 = UtworzWykresPowerBI("Ilość palet H1 które \"wiszą\" nam odbiorcy w tygodniach");
                chartHostH1.Child = _chartH1;
            }
        }

        private void AktualizujWykresy()
        {
            if (_viewModel.SaldaTygodniowe == null) return;

            var dane = _viewModel.SaldaTygodniowe.ToList();
            if (!dane.Any()) return;

            string nazwaOdbiorcy = _viewModel.KontrahentNazwa;

            // Aktualizuj wykres E2
            if (_chartE2 != null)
            {
                AktualizujWykresPowerBI(_chartE2, dane, d => d.SaldoE2, KolorLiniiE2, nazwaOdbiorcy);
            }

            // Aktualizuj wykres H1
            if (_chartH1 != null)
            {
                AktualizujWykresPowerBI(_chartH1, dane, d => d.SaldoH1, KolorLiniiH1, nazwaOdbiorcy);
            }
        }

        private string GetNazwaMiesiaca(int miesiac)
        {
            string[] miesiace = { "", "styczeń", "luty", "marzec", "kwiecień", "maj", "czerwiec",
                                  "lipiec", "sierpień", "wrzesień", "październik", "listopad", "grudzień" };
            return miesiac >= 1 && miesiac <= 12 ? miesiace[miesiac] : "";
        }

        private void AktualizujWykresPowerBI(Chart chart, System.Collections.Generic.List<Models.SaldoTygodniowe> dane,
            Func<Models.SaldoTygodniowe, int> selector, Color kolor, string nazwaOdbiorcy)
        {
            chart.Series.Clear();

            // Seria główna - linia
            var series = new Series(nazwaOdbiorcy)
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 3,
                Color = kolor,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 8,
                MarkerColor = kolor,
                MarkerBorderColor = kolor,
                MarkerBorderWidth = 2,
                IsValueShownAsLabel = true,
                LabelForeColor = KolorTekstu,
                Font = new Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold)
            };

            // Dodaj punkty z etykietami
            int? poprzedniMiesiac = null;
            foreach (var punkt in dane)
            {
                int miesiac = punkt.DataNiedziela.Month;
                int rok = punkt.DataNiedziela.Year;

                // Etykieta: numer tygodnia + nazwa miesiąca (jeśli zmienił się miesiąc)
                string etykietaMiesiaca = "";
                if (poprzedniMiesiac == null || poprzedniMiesiac != miesiac)
                {
                    etykietaMiesiaca = $"\n{GetNazwaMiesiaca(miesiac)}";
                    poprzedniMiesiac = miesiac;
                }

                var etykieta = $"{punkt.NumerTygodnia}{etykietaMiesiaca}";
                int wartosc = selector(punkt);

                var dataPoint = series.Points.AddXY(etykieta, wartosc);

                // Etykieta wartości nad punktem
                series.Points[series.Points.Count - 1].Label = wartosc.ToString("#,##0");
                series.Points[series.Points.Count - 1].LabelAngle = 0;
            }

            chart.Series.Add(series);

            // Legenda
            chart.Legends.Clear();
            var legend = new Legend
            {
                Docking = Docking.Top,
                Alignment = StringAlignment.Far,
                BackColor = Color.Transparent,
                ForeColor = kolor,
                Font = new Font("Segoe UI", 9F)
            };
            legend.CustomItems.Add(new LegendItem
            {
                Name = nazwaOdbiorcy,
                Color = kolor,
                MarkerStyle = MarkerStyle.None,
                BorderWidth = 3,
                BorderColor = kolor
            });
            chart.Legends.Add(legend);

            // Dodaj rok na dole
            if (dane.Any())
            {
                int rok = dane.First().DataNiedziela.Year;
                var titleRok = new Title
                {
                    Text = rok.ToString(),
                    Font = new Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold),
                    ForeColor = KolorTekstu,
                    Docking = Docking.Bottom,
                    Alignment = ContentAlignment.MiddleCenter
                };
                // Usuń stary tytuł roku jeśli istnieje
                for (int i = chart.Titles.Count - 1; i >= 0; i--)
                {
                    if (chart.Titles[i].Docking == Docking.Bottom)
                        chart.Titles.RemoveAt(i);
                }
                chart.Titles.Add(titleRok);
            }

            // Dostosuj skalę osi Y
            var values = dane.Select(selector).ToList();
            if (values.Any())
            {
                var min = values.Min();
                var max = values.Max();
                var range = max - min;

                // Zawsze zacznij od 0 jeśli wartości są dodatnie
                double yMin = min >= 0 ? 0 : Math.Floor(min - range * 0.1);
                double yMax = Math.Ceiling(max + range * 0.15);

                chart.ChartAreas[0].AxisY.Minimum = yMin;
                chart.ChartAreas[0].AxisY.Maximum = yMax;

                // Ustaw interwał dla ładnych wartości na osi Y
                double interval = Math.Ceiling(range / 5.0);
                if (interval > 0)
                {
                    // Zaokrąglij do ładnej wartości (10, 50, 100, 500, 1000, etc.)
                    int magnitude = (int)Math.Pow(10, Math.Floor(Math.Log10(interval)));
                    interval = Math.Ceiling(interval / magnitude) * magnitude;
                    chart.ChartAreas[0].AxisY.Interval = interval;
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
