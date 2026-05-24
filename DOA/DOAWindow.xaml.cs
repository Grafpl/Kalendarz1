using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace Kalendarz1.DOA
{
    /// <summary>
    /// DOA Dashboard (#1) — ANALIZA padłych z FarmerCalc (Specyfikacja Drobiu).
    /// Read-only: ranking hodowców, dostawy, KPI. Wpis padłych jest w WidokSpecyfikacje.
    /// </summary>
    public partial class DOAWindow : Window
    {
        private readonly DOAService _service = new();
        private System.Collections.Generic.List<DOARekord> _rekordy = new();
        private System.Collections.Generic.List<DOAHodowca> _ranking = new();

        public DOAWindow()
        {
            InitializeComponent();
            dpOd.SelectedDate = DateTime.Today.AddDays(-30);
            dpDo.SelectedDate = DateTime.Today;
            Loaded += async (_, _) => await OdswiezAsync();
        }

        private async Task OdswiezAsync()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                DateTime od = dpOd.SelectedDate ?? DateTime.Today.AddDays(-30);
                DateTime doD = dpDo.SelectedDate ?? DateTime.Today;

                _ranking = await _service.GetRankingHodowcowAsync(od, doD);
                _rekordy = await _service.GetRekordyAsync(od, doD);

                dgRanking.ItemsSource = _ranking;
                dgRekordy.ItemsSource = _rekordy;

                long sumaPadle = _ranking.Sum(x => x.SumaPadlych);
                long sumaDek = _ranking.Sum(x => x.SumaSztukDek);
                decimal srednie = sumaDek > 0 ? (decimal)sumaPadle / sumaDek * 100m : 0m;

                kpiSrednie.Text = $"{srednie:N2}%";
                kpiPadle.Text = $"{sumaPadle:N0} / {sumaDek:N0}";
                kpiAlerty.Text = _ranking.Count(x => x.SredniProcDOA > 0.50m).ToString();
                kpiDostaw.Text = _rekordy.Count.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania danych DOA:\n" + ex.Message
                    + "\n\nDane pochodzą z FarmerCalc (Specyfikacja Drobiu).",
                    "DOA", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { Mouse.OverrideCursor = null; }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e) => await OdswiezAsync();

        private void BtnCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_ranking.Count == 0) { return; }
            var dlg = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"doa_ranking_{DateTime.Today:yyyyMMdd}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("Pozycja;Hodowca;Dostaw;Padle;SztukiDek;DOA_proc;Status");
            foreach (var h in _ranking)
            {
                sb.AppendLine(string.Join(";",
                    h.Pozycja,
                    Csv(h.Hodowca),
                    h.LiczbaPartii,
                    h.SumaPadlych,
                    h.SumaSztukDek,
                    h.SredniProcDOA.ToString("F2", CultureInfo.InvariantCulture),
                    Csv(h.Status)));
            }
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("Zapisano: " + dlg.FileName, "Eksport",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static string Csv(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    }
}
