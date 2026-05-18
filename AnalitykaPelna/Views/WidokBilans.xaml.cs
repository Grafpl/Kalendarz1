using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Kalendarz1.AnalitykaPelna.Models;
using Kalendarz1.AnalitykaPelna.Services;
using LiveCharts;
using LiveCharts.Wpf;

namespace Kalendarz1.AnalitykaPelna.Views
{
    public partial class WidokBilans : UserControl
    {
        private readonly BilansService _service = new();
        private List<BilansDzien> _ostatnieDni = new();
        private List<BilansSurowyRekord> _ostatnieSurowe = new();

        public WidokBilans()
        {
            InitializeComponent();
        }

        public async Task ZastosujFiltryAsync(FiltryAnaliz f)
        {
            try
            {
                _ostatnieSurowe = await _service.LoadAnalitykaAsync(f);
                var prognoza = await _service.LoadPrognozaSprzedazyAsync(f);
                _ostatnieDni = _service.BudujBilans(_ostatnieSurowe, f, prognoza);

                dgBilans.ItemsSource = _ostatnieDni;

                // ═══ KPI ═══
                decimal sumaProdukcji = _ostatnieDni.Sum(b => b.Produkcja);
                decimal sumaSprzedazy = _ostatnieDni.Sum(b => b.Sprzedaz);
                decimal rotacja = sumaProdukcji == 0 ? 0 : sumaSprzedazy / sumaProdukcji * 100m;
                int liczbaAnomalii = _ostatnieDni.Count(b => b.Anomalia);
                var dniZPrognoza = _ostatnieDni.Where(b => b.Prognoza > 0).ToList();
                decimal mape = dniZPrognoza.Count == 0 ? 0 : dniZPrognoza.Average(b => b.MapeProc);

                kpiProdukcja.Wartosc = sumaProdukcji.ToString("N0");
                kpiSprzedaz.Wartosc = sumaSprzedazy.ToString("N0");
                kpiRotacja.Wartosc = rotacja.ToString("N1") + "%";
                kpiAnomalie.Wartosc = liczbaAnomalii.ToString();
                kpiAnomalie.Jednostka = $"z {_ostatnieDni.Count} dni (2σ)";
                kpiMape.Wartosc = dniZPrognoza.Count == 0 ? "—" : mape.ToString("N1") + "%";

                BudujWykres(_ostatnieDni);

                int topN = AnalitykaConfig.TopNRanking;
                dgRankingOdbiorcy.ItemsSource = _service.BudujRankingOdbiorcow(_ostatnieSurowe, topN);
                dgRankingTowary.ItemsSource = _service.BudujRankingTowarow(_ostatnieSurowe, topN);
                dgRankingHandlowcy.ItemsSource = _service.BudujRankingHandlowcow(_ostatnieSurowe, topN);

                int liczbaSprzedazy = _ostatnieSurowe.Count(s => s.TypOperacji == "SPRZEDAZ");
                txtInfo.Text = _ostatnieDni.Count == 0
                    ? "Brak danych w wybranym zakresie"
                    : $"{f.DataOd:dd.MM.yyyy} – {f.DataDo:dd.MM.yyyy}  •  " +
                      $"{_ostatnieDni.Count} dni  •  {liczbaSprzedazy} zapisów sprzedaży";
            }
            catch
            {
                _ostatnieDni.Clear();
                _ostatnieSurowe.Clear();
                dgBilans.ItemsSource = null;
                wykresBilans.Series = new SeriesCollection();
                throw;
            }
        }

        private void BudujWykres(List<BilansDzien> bilans)
        {
            if (bilans == null || bilans.Count == 0)
            {
                wykresBilans.Series = new SeriesCollection();
                osX.Labels = new List<string>();
                return;
            }

            var produkcja = new ChartValues<double>(bilans.Select(b => SafeDouble(b.Produkcja)));
            var sprzedaz = new ChartValues<double>(bilans.Select(b => SafeDouble(b.Sprzedaz)));
            var prognoza = new ChartValues<double>(bilans.Select(b => SafeDouble(b.Prognoza)));

            wykresBilans.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Produkcja",
                    Values = produkcja,
                    Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x08, 0x91, 0xB2)),
                    DataLabels = false
                },
                new ColumnSeries
                {
                    Title = "Sprzedaż",
                    Values = sprzedaz,
                    Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x05, 0x96, 0x69)),
                    DataLabels = false
                },
                new LineSeries
                {
                    Title = "Prognoza",
                    Values = prognoza,
                    Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDC, 0x26, 0x26)),
                    StrokeThickness = 2,
                    Fill = System.Windows.Media.Brushes.Transparent,
                    PointGeometrySize = 6,
                    LineSmoothness = 0.5
                }
            };

            osX.Labels = bilans.Select(b => b.Data.ToString("dd.MM")).ToList();
        }

        private static double SafeDouble(decimal d)
        {
            double v = (double)d;
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            return v;
        }

        public void EksportujCsv()
        {
            // Eksportujemy bilans dzienny (najczęściej potrzebny widok)
            CsvExporter.Eksportuj(_ostatnieDni, "Bilans_dzienny",
                new[] { nameof(BilansDzien.Data), nameof(BilansDzien.DzienTygodnia),
                    nameof(BilansDzien.Produkcja), nameof(BilansDzien.Sprzedaz),
                    nameof(BilansDzien.Prognoza), nameof(BilansDzien.Wartosc),
                    nameof(BilansDzien.RoznicaProdSprz), nameof(BilansDzien.RotacjaProc),
                    nameof(BilansDzien.MapeProc), nameof(BilansDzien.Anomalia) },
                Window.GetWindow(this));
        }
    }

    public class BoolToAnomaliaConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? "⚡" : "";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
