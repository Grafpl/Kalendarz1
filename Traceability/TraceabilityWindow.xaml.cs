using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1.Traceability
{
    /// <summary>
    /// Traceability (#3) — rejestracja palet, reverse trace (lot→hodowca), recall.
    /// Wymaga Traceability/SQL/CreateTraceability.sql. Drukarka etykiet — osobny krok.
    /// </summary>
    public partial class TraceabilityWindow : Window
    {
        private readonly TraceabilityService _service = new();
        private readonly ObservableCollection<PaletaSklad> _skladBiezacy = new();
        private List<PaletaSklad> _partieDoWyboru = new();

        public TraceabilityWindow()
        {
            InitializeComponent();
            dgSklad.ItemsSource = _skladBiezacy;
            Loaded += async (_, _) =>
            {
                await ZaladujPartieAsync();
                await ZaladujPartieReverseAsync();
                await OdswiezRecalleAsync();
                await GenerujLotAsync();
            };
        }

        // ─── TAB 0: Reverse trace na istniejących danych ───────────────────
        private async Task ZaladujPartieReverseAsync()
        {
            try { cbTracePartia.ItemsSource = await _service.GetPartieReverseAsync(); }
            catch { /* In0E zawsze jest, ale defensywnie */ }
        }

        private async void BtnTrace_Click(object sender, RoutedEventArgs e)
        {
            string partia = (cbTracePartia.SelectedItem as string)
                            ?? cbTracePartia.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(partia)) { txtTraceInfo.Text = "Podaj numer partii."; return; }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var res = await _service.ReverseTraceExistingAsync(partia);

                boxTraceHead.Visibility = Visibility.Visible;
                if (!res.Znaleziono)
                {
                    txtTraceHodowca.Text = "❌ " + (res.Blad ?? "Nie znaleziono");
                    txtTracePodsum.Text = "";
                    dgTracePrzyjecia.ItemsSource = null;
                    dgTracePrzeplywy.ItemsSource = null;
                    dgTraceWyjscia.ItemsSource = null;
                    return;
                }

                txtTraceHodowca.Text = $"🐔 Partia {res.Partia}  —  hodowca: {res.Hodowca ?? "(brak w PartiaDostawca)"}";
                txtTracePodsum.Text = res.PodsumowanieFormatted;
                dgTracePrzyjecia.ItemsSource = res.Przyjecia;
                dgTracePrzeplywy.ItemsSource = res.Przeplywy;
                dgTraceWyjscia.ItemsSource = res.Wyjscia;
                txtTraceInfo.Text = "";
            }
            catch (Exception ex)
            {
                txtTraceInfo.Text = "Błąd: " + ex.Message;
            }
            finally { Mouse.OverrideCursor = null; }
        }

        // ─── TAB 1: Rejestracja ────────────────────────────────────────────
        private async Task ZaladujPartieAsync()
        {
            try
            {
                _partieDoWyboru = await _service.GetPartieZHodowcamiAsync();
                cbPartia.ItemsSource = _partieDoWyboru;
            }
            catch (Exception ex) { KomunikatRej("Błąd ładowania partii: " + ex.Message, false); }
        }

        private async Task GenerujLotAsync()
        {
            try { txtLot.Text = await _service.GenerujLotNumberAsync(DateTime.Today); }
            catch { /* tabela może nie istnieć jeszcze */ }
        }

        private async void BtnGenerujLot_Click(object sender, RoutedEventArgs e) => await GenerujLotAsync();

        private void BtnDodajSklad_Click(object sender, RoutedEventArgs e)
        {
            var sel = cbPartia.SelectedItem as PaletaSklad;
            string partia = sel?.Partia ?? cbPartia.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(partia)) { KomunikatRej("Wybierz partię.", false); return; }
            if (_skladBiezacy.Any(x => x.Partia == partia)) { KomunikatRej("Partia już dodana.", false); return; }

            _skladBiezacy.Add(new PaletaSklad
            {
                Partia = partia,
                CustomerID = sel?.CustomerID,
                CustomerName = sel?.CustomerName
            });
            KomunikatRej("", true);
        }

        private async void BtnZapiszPalete_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLot.Text)) { KomunikatRej("Brak lot numeru.", false); return; }
            if (_skladBiezacy.Count == 0) { KomunikatRej("Dodaj co najmniej jedną partię do składu.", false); return; }

            var p = new PaletaWyrob
            {
                LotNumber = txtLot.Text.Trim(),
                DataProdukcji = DateTime.Today,
                KodTowaru = string.IsNullOrWhiteSpace(txtKodTowaru.Text) ? null : txtKodTowaru.Text.Trim(),
                NazwaTowaru = string.IsNullOrWhiteSpace(txtTowar.Text) ? null : txtTowar.Text.Trim(),
                WagaKg = ParseDec(txtWaga.Text),
                LiczbaSztuk = string.IsNullOrWhiteSpace(txtSztuk.Text) ? null : (int?)ParseInt(txtSztuk.Text),
                OperatorId = App.UserID
            };
            try
            {
                btnZapiszPalete.IsEnabled = false;
                await _service.RejestrujPaleteAsync(p, _skladBiezacy.ToList(), App.UserID);
                KomunikatRej($"✓ Zarejestrowano paletę {p.LotNumber} ({_skladBiezacy.Count} partii).", true);
                _skladBiezacy.Clear();
                txtTowar.Clear(); txtKodTowaru.Clear(); txtWaga.Clear(); txtSztuk.Clear();
                await GenerujLotAsync();
            }
            catch (Exception ex) { KomunikatRej("Błąd zapisu: " + ex.Message, false); }
            finally { btnZapiszPalete.IsEnabled = true; }
        }

        // ─── TAB 2: Reverse Trace ──────────────────────────────────────────
        private async void BtnReverse_Click(object sender, RoutedEventArgs e)
        {
            string lot = txtLotSzukaj.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(lot)) return;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var res = await _service.ReverseTraceAsync(lot);
                if (!res.Znaleziono)
                {
                    boxPaleta.Visibility = Visibility.Visible;
                    txtPaletaInfo.Text = "❌ " + (res.Blad ?? "Nie znaleziono");
                    txtPaletaMeta.Text = "";
                    dgRevSklad.ItemsSource = null;
                    dgRevWydania.ItemsSource = null;
                    return;
                }
                boxPaleta.Visibility = Visibility.Visible;
                txtPaletaInfo.Text = $"📦 {res.Paleta!.LotNumber} — {res.Paleta.NazwaTowaru ?? "(towar?)"}";
                txtPaletaMeta.Text = $"Produkcja: {res.Paleta.DataFormatted} • Waga: {res.Paleta.WagaFormatted} • "
                    + $"Status: {res.Paleta.Status} • Hodowców: {res.LiczbaHodowcow} • Wydań: {res.Wydania.Count}";
                dgRevSklad.ItemsSource = res.Sklad;
                dgRevWydania.ItemsSource = res.Wydania;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd: " + ex.Message + "\n\nCzy tabele istnieją? Uruchom CreateTraceability.sql.",
                    "Traceability", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { Mouse.OverrideCursor = null; }
        }

        // ─── TAB 3: Recall ─────────────────────────────────────────────────
        private string TypZakresu() => (cbTypZakresu.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PARTIA";
        private string Kategoria() => (cbKategoria.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "JAKOSC";

        private async void BtnSprawdzZakres_Click(object sender, RoutedEventArgs e)
        {
            string ident = txtZakresIdent.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(ident)) { txtZakresPodglad.Text = "Podaj identyfikator."; return; }
            try
            {
                var palety = await _service.ZnajdzPaletyDoRecallAsync(TypZakresu(), ident);
                txtZakresPodglad.Text = palety.Count == 0
                    ? "Brak palet w tym zakresie."
                    : $"Objętych palet: {palety.Count}, łączna waga: {palety.Sum(x => x.WagaKg):N0} kg.";
            }
            catch (Exception ex) { txtZakresPodglad.Text = "Błąd: " + ex.Message; }
        }

        private async void BtnInicjujRecall_Click(object sender, RoutedEventArgs e)
        {
            string ident = txtZakresIdent.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(ident)) { txtZakresPodglad.Text = "Podaj identyfikator."; return; }

            var potw = MessageBox.Show(
                $"Zainicjować recall?\n\nTyp: {TypZakresu()}\nIdent: {ident}\nKategoria: {Kategoria()}\n\n"
                + "Palety w zakresie zostaną oznaczone jako WYCOFANO.",
                "Potwierdź recall", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (potw != MessageBoxResult.Yes) return;

            try
            {
                btnInicjujRecall.IsEnabled = false;
                var res = await _service.InicjujRecallAsync(TypZakresu(), ident,
                    txtPowod.Text?.Trim() ?? "", Kategoria(), App.UserID);
                MessageBox.Show(
                    $"✓ Recall {res.RecallNumber} utworzony.\n\nObjętych palet: {res.LiczbaPalet}\n"
                    + $"Łączna waga: {res.WagaKg:N0} kg",
                    "Recall", MessageBoxButton.OK, MessageBoxImage.Information);
                txtZakresIdent.Clear(); txtPowod.Clear(); txtZakresPodglad.Text = "";
                await OdswiezRecalleAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd inicjacji recall: " + ex.Message, "Recall",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { btnInicjujRecall.IsEnabled = true; }
        }

        private async void BtnOdswiezRecall_Click(object sender, RoutedEventArgs e) => await OdswiezRecalleAsync();

        private async Task OdswiezRecalleAsync()
        {
            try { dgRecalle.ItemsSource = await _service.GetRecalleAsync(); }
            catch { /* tabela może nie istnieć */ }
        }

        // ─── Helpers ───────────────────────────────────────────────────────
        private void KomunikatRej(string tekst, bool ok)
        {
            txtKomunikatRej.Text = tekst;
            txtKomunikatRej.Foreground = ok ? BrushFromHex("#059669") : Brushes.DarkRed;
        }

        private static decimal ParseDec(string? s)
        {
            s = (s ?? "").Replace(',', '.').Trim();
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }

        private static int ParseInt(string? s)
            => int.TryParse((s ?? "").Trim(), out int v) ? v : 0;

        private static SolidColorBrush BrushFromHex(string hex)
        {
            try { return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; }
            catch { return new SolidColorBrush(Colors.Gray); }
        }
    }
}
