using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.AnalitykaPelna.Models;
using Kalendarz1.AnalitykaPelna.Services;
using Microsoft.Win32;

namespace Kalendarz1.AnalitykaPelna.Views
{
    /// <summary>
    /// Wodospad uzysku (#5) — wizualizacja gdzie znika mięso od żywca do gotowych elementów.
    /// Reuse WydajnoscService.LoadFlowChainAsync (FlowChainSummary). Ceny opcjonalne (PLN).
    /// </summary>
    public partial class WidokWodospad : UserControl
    {
        private readonly WydajnoscService _service = new();
        private FlowChainSummary _summary = new();
        private WaterfallData _data = new();
        private FiltryAnaliz? _ostatnieFiltry;

        public WidokWodospad()
        {
            InitializeComponent();
        }

        public async Task ZastosujFiltryAsync(FiltryAnaliz f)
        {
            _ostatnieFiltry = f;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                _summary = await _service.LoadFlowChainAsync(f);
                Przelicz();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania wodospadu: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void Przelicz()
        {
            decimal? cenaZywca = ParseCena(txtCenaZywca.Text);
            decimal? cenaGotowego = ParseCena(txtCenaGotowego.Text);

            DateTime od = _ostatnieFiltry?.DataOd ?? DateTime.Today;
            DateTime doD = _ostatnieFiltry?.DataDo ?? DateTime.Today;

            _data = WaterfallData.ZFlowChain(_summary, od, doD, cenaZywca, cenaGotowego);
            Odswiez();
        }

        private void Odswiez()
        {
            // KPI
            kpiWejscie.Text = _data.WejscieFormatted;
            kpiWyjscie.Text = _data.WyjscieFormatted;
            kpiYield.Text = _data.YieldFormatted;
            kpiYieldCard.Background = BrushFromHex(YieldTloHex(_data.YieldProc));

            kpiMarza.Text = _data.MarzaFormatted;
            kpiKoszt.Text = _data.EfektywnyKosztGotowegoZaKg.HasValue
                ? $"Efekt. koszt mięsa: {_data.EfektywnyKosztFormatted}"
                : "";

            // Listy
            lstWodospad.ItemsSource = _data.Etapy;
            lstRozejscie.ItemsSource = WaterfallData.RozejscieZFlowChain(_summary);

            // Stopka
            txtStopka.Text = _data.WagaWejsciaKg <= 0
                ? "Brak danych żywca w wybranym okresie. Zmień zakres dat / katalogi."
                : $"Okres: {_data.DataOd:dd.MM.yyyy}–{_data.DataDo:dd.MM.yyyy}  •  "
                  + $"Yield: {_data.YieldFormatted}  •  "
                  + (_data.MarzaPLN.HasValue
                        ? $"Marża: {_data.MarzaFormatted}"
                        : "Podaj ceny aby zobaczyć marżę i efektywny koszt.");
        }

        private void Ceny_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Przelicz tylko jeśli mamy już załadowane dane
            if (_summary.Zywiec.Kg > 0 || _summary.Uboj.Kg > 0)
                Przelicz();
        }

        private void BtnEksport_Click(object sender, RoutedEventArgs e) => EksportujCsv();

        public void EksportujCsv()
        {
            if (_data.Etapy.Count == 0)
            {
                MessageBox.Show("Brak danych do eksportu.", "Eksport",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"wodospad_{_data.DataOd:yyyyMMdd}_{_data.DataDo:yyyyMMdd}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("Etap;Typ;Waga_kg;Proc_bazy;Proc_straty;Kierunek;Status");
            foreach (var s in _data.Etapy)
            {
                sb.AppendLine(string.Join(";",
                    Csv(s.Nazwa),
                    s.Typ.ToString(),
                    s.WartoscKg.ToString("F0", CultureInfo.InvariantCulture),
                    s.ProcentBazy.ToString("F1", CultureInfo.InvariantCulture),
                    s.ProcentStraty.ToString("F1", CultureInfo.InvariantCulture),
                    Csv(s.Kierunek),
                    Csv(s.Status)));
            }
            sb.AppendLine();
            sb.AppendLine($"Wejscie_kg;{_data.WagaWejsciaKg:F0}");
            sb.AppendLine($"Wyjscie_kg;{_data.WagaWyjsciaKg:F0}");
            sb.AppendLine($"Yield_proc;{_data.YieldProc:F1}");
            if (_data.MarzaPLN.HasValue)
            {
                sb.AppendLine($"Marza_PLN;{_data.MarzaPLN.Value:F0}");
                sb.AppendLine($"Efektywny_koszt_zl_kg;{_data.EfektywnyKosztGotowegoZaKg!.Value:F2}");
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("Zapisano: " + dlg.FileName, "Eksport",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ─── Helpers ───────────────────────────────────────────────────────

        private static decimal? ParseCena(string? tekst)
        {
            if (string.IsNullOrWhiteSpace(tekst)) return null;
            tekst = tekst.Replace(',', '.').Trim();
            return decimal.TryParse(tekst, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v > 0
                ? v : null;
        }

        private static string Csv(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

        private static string YieldTloHex(decimal yield) =>
            yield >= 58m ? "#D1FAE5" :
            yield >= 50m ? "#FEF3C7" : "#FEE2E2";

        private static SolidColorBrush BrushFromHex(string hex)
        {
            try { return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; }
            catch { return new SolidColorBrush(Colors.LightGray); }
        }
    }
}
