using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.AnalitykaPelna.Models;
using Kalendarz1.AnalitykaPelna.Services;
using Kalendarz1.AnalitykaPelna.Windows;

namespace Kalendarz1.AnalitykaPelna.Views
{
    public partial class WidokRealizacja : UserControl
    {
        private readonly RealizacjaService _service = new();
        private List<WazenieRekord> _wazenia = new();

        public WidokRealizacja()
        {
            InitializeComponent();
            UtworzKolumnyHeatmapy();
        }

        public async Task ZastosujFiltryAsync(FiltryAnaliz f)
        {
            try
            {
                _wazenia = await _service.LoadWazeniaAsync(f);

                decimal sumaKg = _wazenia.Where(w => w.ActWeight > 0).Sum(w => w.ActWeight);
                int liczbaWazen = _wazenia.Count(w => w.ActWeight > 0);
                int liczbaAnulacji = _wazenia.Count(w => w.ActWeight < 0);

                double godziny = 1;
                if (_wazenia.Count > 1)
                {
                    var min = _wazenia.Min(w => w.Godzina);
                    var max = _wazenia.Max(w => w.Godzina);
                    godziny = Math.Max(1, (max - min).TotalHours);
                }
                decimal tempo = godziny <= 0 ? 0 : (decimal)((double)sumaKg / godziny);

                int liczbaOperatorow = _wazenia
                    .Where(w => !string.IsNullOrEmpty(w.OperatorID))
                    .Select(w => w.OperatorID)
                    .Distinct()
                    .Count();

                kpiSumaKg.Wartosc = sumaKg.ToString("N0");
                kpiLiczbaWazen.Wartosc = liczbaWazen.ToString("N0");
                kpiAnulacje.Wartosc = liczbaAnulacji.ToString("N0");
                kpiTempo.Wartosc = tempo.ToString("N0");
                kpiOperatorzy.Wartosc = liczbaOperatorow.ToString();

                dgOperatorzy.ItemsSource = _service.BudujRankingOperatorow(_wazenia, AnalitykaConfig.TopNRanking);
                dgZmiany.ItemsSource = _service.BudujStatystykiZmian(_wazenia);
                dgPartie.ItemsSource = _service.BudujRankingPartii(_wazenia, AnalitykaConfig.TopNRanking * 5);

                var heatmapa = _service.BudujHeatmapeGodzinowa(_wazenia, AnalitykaConfig.HeatmapaDniWstecz);
                dgHeatmapa.ItemsSource = heatmapa.Select(h => new HeatmapaWiersz(h)).ToList();

                var ostatnie = _wazenia.OrderByDescending(w => w.Godzina).Take(1000).ToList();
                dgWazenia.ItemsSource = ostatnie;

                txtInfo.Text = _wazenia.Count == 0
                    ? "Brak ważeń w wybranym zakresie"
                    : $"{f.DataOd:dd.MM.yyyy} – {f.DataDo:dd.MM.yyyy}  •  " +
                      $"{liczbaWazen:N0} ważeń, {liczbaAnulacji:N0} anulacji, {liczbaOperatorow} operatorów" +
                      (ostatnie.Count == 1000 ? "  •  pokazano 1000 najnowszych" : "");
            }
            catch
            {
                _wazenia.Clear();
                dgOperatorzy.ItemsSource = null;
                dgZmiany.ItemsSource = null;
                dgPartie.ItemsSource = null;
                dgHeatmapa.ItemsSource = null;
                dgWazenia.ItemsSource = null;
                throw;
            }
        }

        private void UtworzKolumnyHeatmapy()
        {
            // Pierwsza kolumna: Data
            dgHeatmapa.Columns.Add(new DataGridTextColumn
            {
                Header = "Data",
                Binding = new System.Windows.Data.Binding(nameof(HeatmapaWiersz.DataKrotka)),
                Width = new DataGridLength(60)
            });

            // 24 kolumny godzinowe — z kolorowaniem (gradient zielony)
            var kolorConverter = new HeatmapaKolorConverter();
            for (int g = 0; g < 24; g++)
            {
                int godz = g;
                var col = new DataGridTextColumn
                {
                    Header = godz.ToString("00"),
                    Binding = new System.Windows.Data.Binding($"Godziny[{godz}]")
                    {
                        StringFormat = "N0"
                    },
                    Width = new DataGridLength(50)
                };

                // Kolorowanie tła komórek wg wartości (gradient zielony)
                var cellStyle = new System.Windows.Style(typeof(DataGridCell));
                var bg = new System.Windows.Setter(System.Windows.Controls.DataGridCell.BackgroundProperty,
                    new System.Windows.Data.Binding($"Godziny[{godz}]")
                    {
                        Converter = kolorConverter,
                        ConverterParameter = "5000"
                    });
                cellStyle.Setters.Add(bg);
                cellStyle.Setters.Add(new System.Windows.Setter(System.Windows.Controls.DataGridCell.FontWeightProperty,
                    System.Windows.FontWeights.SemiBold));
                col.CellStyle = cellStyle;

                dgHeatmapa.Columns.Add(col);
            }
        }

        private void DgPartie_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgPartie.SelectedItem is not RankingPartii p) return;

            // Pełen drilldown z modalem
            var wazeniaPartii = _wazenia
                .Where(w => w.Partia == p.Partia)
                .OrderBy(w => w.Godzina)
                .ToList();

            var dialog = new WazeniaPartiiDialog(p.Partia, p.Hodowca, wazeniaPartii)
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();
        }

        public void EksportujCsv()
        {
            // Eksport: surowe ważenia (najużyteczniejsze)
            CsvExporter.Eksportuj(_wazenia, "Realizacja_wazenia",
                new[] { nameof(WazenieRekord.Data), nameof(WazenieRekord.Godzina),
                    nameof(WazenieRekord.ArticleID), nameof(WazenieRekord.NazwaTowaru),
                    nameof(WazenieRekord.Weight), nameof(WazenieRekord.ActWeight),
                    nameof(WazenieRekord.Roznica), nameof(WazenieRekord.OperatorID),
                    nameof(WazenieRekord.Wagowy), nameof(WazenieRekord.Partia),
                    nameof(WazenieRekord.Hodowca), nameof(WazenieRekord.Klasa),
                    nameof(WazenieRekord.TermID), nameof(WazenieRekord.Tara) },
                Window.GetWindow(this));
        }
    }

    /// <summary>
    /// Wiersz dla DataGrid heatmapy: data + 24 wartości godzinowe (indekser).
    /// </summary>
    public class HeatmapaWiersz
    {
        public DateTime Data { get; }
        public string DataKrotka => Data.ToString("dd.MM");
        public IndekserGodzin Godziny { get; }

        public HeatmapaWiersz(HeatmapaGodzinowa h)
        {
            Data = h.Data;
            Godziny = new IndekserGodzin(h.KgPerGodzina);
        }
    }

    public class IndekserGodzin
    {
        private readonly Dictionary<int, decimal> _data;
        public IndekserGodzin(Dictionary<int, decimal> data) { _data = data; }
        public decimal this[int godzina]
            => _data.TryGetValue(godzina, out var v) ? v : 0m;
    }
}
