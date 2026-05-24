// ════════════════════════════════════════════════════════════════════════════
// KontraktyEditorWindow.xaml.cs — Faza 1 (formularz CRUD)
// Część 4 audytu — domyka MVP modułu Kontrakty
// Target: Kontrakty/Windows/KontraktyEditorWindow.xaml.cs
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Kontrakty.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Windows
{
    public partial class KontraktyEditorWindow : Window
    {
        private readonly KontraktyService _svc = new();
        private readonly DostawcyLookupService _lookup = new();
        private readonly WordTemplateService _word = new();
        private KontraktDto _model;
        private readonly bool _isNew;

        /// <summary>ctor dla NOWEGO kontraktu (model = null) lub EDYCJI (model != null).</summary>
        public KontraktyEditorWindow(KontraktDto? model = null)
        {
            InitializeComponent();
            _isNew = model == null;
            _model = model ?? new KontraktDto
            {
                Rok = (short)DateTime.Today.Year,
                DataObowiazujeOd = DateTime.Today,
                Status = "DRAFT"
            };

            txtTytul.Text = _isNew ? "Nowy kontrakt" : $"Edycja kontraktu {_model.NumerKontraktu}";
            txtNumer.Text = _isNew ? "numer nadany przy zapisie" : _model.NumerKontraktu;

            Loaded += async (_, _) =>
            {
                await ZaladujDostawcowAsync();
                if (!_isNew) WypelnijZModelu();
                else { dpOd.SelectedDate = DateTime.Today; dpPodpisania.SelectedDate = DateTime.Today; }
            };
        }

        // ────────────────────────────────────────────────────────────────────

        private async Task ZaladujDostawcowAsync()
        {
            try
            {
                var lista = await _lookup.GetAllAsync();
                cmbDostawca.ItemsSource = lista;
                cmbDostawca.DisplayMemberPath = "Nazwa";
                if (!_isNew)
                {
                    var match = lista.FirstOrDefault(d => d.Id == _model.DostawcaId);
                    if (match != null) cmbDostawca.SelectedItem = match;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania hodowców:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CmbDostawca_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbDostawca.SelectedItem is not DostawcaLookup d) return;
            // Auto-uzupełnij snapshot pól z danych hodowcy (Asia może nadpisać ręcznie)
            var pelne = await _lookup.GetByIdAsync(d.Id) ?? d;
            if (string.IsNullOrEmpty(txtNip.Text)) txtNip.Text = pelne.Nip ?? "";
            if (string.IsNullOrEmpty(txtNrGosp.Text)) txtNrGosp.Text = pelne.NrGospodarstwa ?? "";
            if (string.IsNullOrEmpty(txtAdres.Text)) txtAdres.Text = pelne.Adres ?? "";
        }

        private void CmbTyp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var typ = (cmbTyp.SelectedItem as ComboBoxItem)?.Tag as string;
            // Auto-ustaw daty zależnie od typu
            if (dpOd.SelectedDate == null) return;
            var od = dpOd.SelectedDate.Value;
            switch (typ)
            {
                case "ARIMR_3LAT": dpDo.SelectedDate = od.AddYears(3); chkArimr.IsChecked = true; break;
                case "ROCZNY":     dpDo.SelectedDate = od.AddYears(1); chkArimr.IsChecked = false; break;
                case "WIECZNY":    dpDo.SelectedDate = null;            chkArimr.IsChecked = false; break;
                case "SPOT":       dpDo.SelectedDate = od;              chkArimr.IsChecked = false; break;
            }
        }

        private void WypelnijZModelu()
        {
            txtNip.Text = _model.NipSnapshot ?? "";
            txtNrGosp.Text = _model.NrGospodarstwaSnapshot ?? "";
            txtAdres.Text = _model.AdresSnapshot ?? "";
            SetCombo(cmbTyp, _model.TypKontraktu);
            SetCombo(cmbTypCeny, _model.TypCeny);
            SetCombo(cmbWaga, _model.RozliczanaWaga);
            SetCombo(cmbPodmiot, _model.PartiaPiorkowscy ?? "PIORKOWSCY");
            chkArimr.IsChecked = _model.LiczySieDoArimr;
            dpOd.SelectedDate = _model.DataObowiazujeOd;
            dpDo.SelectedDate = _model.DataObowiazujeDo;
            dpPodpisania.SelectedDate = _model.DataPodpisania;
            txtWypowiedzenie.Text = _model.OkresWypowiedzeniaDni.ToString();
            txtUbytku.Text = _model.ProcentUbytku.ToString("F2", CultureInfo.InvariantCulture);
            txtCena.Text = _model.Cena?.ToString("F2", CultureInfo.InvariantCulture) ?? "";
            txtTermin.Text = _model.TerminPlatnosciDni.ToString();
        }

        // ────────────────────────────────────────────────────────────────────

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (!Waliduj(out var err)) { txtWalidacja.Text = err; return; }
            ZbierzDoModelu();

            try
            {
                if (_isNew)
                {
                    var id = await _svc.CreateAsync(_model, App.UserID ?? "?");
                    _model.Id = id;
                    MessageBox.Show($"Utworzono kontrakt {_model.NumerKontraktu}.", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    await _svc.UpdateAsync(_model, App.UserID ?? "?");
                    MessageBox.Show("Zapisano zmiany.", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnGenerujWord_Click(object sender, RoutedEventArgs e)
        {
            if (_isNew)
            {
                MessageBox.Show("Najpierw zapisz kontrakt (Zapisz), potem generuj Word.",
                    "Uwaga", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ZbierzDoModelu();

            var templatePath = _model.TypKontraktu switch
            {
                "ARIMR_3LAT" => @"\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_ARIMR_3LAT.docx",
                "WIECZNY"    => @"\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Wieczna.docx",
                "ROCZNY"     => @"\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Roczna.docx",
                _            => @"\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Spot.docx"
            };
            var nazwiskoSan = (_model.NazwaHodowcySnapshot ?? "Hodowca")
                .Replace(' ', '_').Replace('/', '_');
            var outputPath = $@"\\192.168.0.170\Install\UmowyZakupu\{_model.Rok}\Umowa_{nazwiskoSan}_{_model.NumerKontraktu.Replace('/', '_')}.docx";

            try
            {
                var podmiotNazwa = _model.PartiaPiorkowscy == "PIORKOWSCY_SPZOO" ? "Piórkowscy sp. z o.o." : "Piórkowscy";
                var values = _word.BuildValuesFromKontrakt(_model, podmiotNazwa);
                _word.GenerateContract(templatePath, outputPath, values);

                _model.SciezkaWord = outputPath;
                if (_model.Status == "DRAFT") _model.Status = "PRINTED";
                await _svc.UpdateAsync(_model, App.UserID ?? "?");

                Process.Start("explorer", $"\"{outputPath}\"");
                MessageBox.Show($"Wygenerowano:\n{outputPath}\n\nSkoryguj w Wordzie → Ctrl+S.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd generacji Word:\n{ex.Message}\n\nSprawdź czy szablon istnieje w _SZABLON\\.",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        // ────────────────────────────────────────────────────────────────────
        // WALIDACJA + zbieranie do modelu

        private bool Waliduj(out string err)
        {
            err = "";
            if (cmbDostawca.SelectedItem is not DostawcaLookup)
            { err = "⚠ Wybierz hodowcę z listy."; return false; }
            if (dpOd.SelectedDate == null)
            { err = "⚠ Podaj datę 'od'."; return false; }
            if (!decimal.TryParse(txtUbytku.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var ub) || ub < 0 || ub > 20)
            { err = "⚠ % ubytku musi być liczbą 0–20."; return false; }
            if (ub < 2 || ub > 4)
            { err = $"ℹ Uwaga: % ubytku {ub} jest nietypowy (typowo 2.5–3.5). Sprawdź — ale możesz zapisać."; /* nie blokuj */ }
            if (!string.IsNullOrWhiteSpace(txtCena.Text) &&
                (!decimal.TryParse(txtCena.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var c) || c <= 0 || c > 50))
            { err = "⚠ Cena musi być liczbą 0–50 zł/kg (lub puste = cennik dnia)."; return false; }
            if (!int.TryParse(txtTermin.Text, out var t) || t <= 0 || t > 90)
            { err = "⚠ Termin płatności 1–90 dni."; return false; }
            if (dpDo.SelectedDate.HasValue && dpDo.SelectedDate < dpOd.SelectedDate)
            { err = "⚠ Data 'do' nie może być wcześniejsza niż 'od'."; return false; }
            return true;
        }

        private void ZbierzDoModelu()
        {
            var d = cmbDostawca.SelectedItem as DostawcaLookup;
            _model.DostawcaId = d?.Id ?? _model.DostawcaId;
            _model.NazwaHodowcySnapshot = d?.Nazwa ?? _model.NazwaHodowcySnapshot;
            _model.NipSnapshot = NullIfEmpty(txtNip.Text);
            _model.NrGospodarstwaSnapshot = NullIfEmpty(txtNrGosp.Text);
            _model.AdresSnapshot = NullIfEmpty(txtAdres.Text);
            _model.TypKontraktu = TagOf(cmbTyp) ?? "ARIMR_3LAT";
            _model.TypCeny = TagOf(cmbTypCeny) ?? "wolnorynkowa";
            _model.RozliczanaWaga = TagOf(cmbWaga) ?? "NETTO_HODOWCY";
            _model.PartiaPiorkowscy = TagOf(cmbPodmiot) ?? "PIORKOWSCY";
            _model.LiczySieDoArimr = chkArimr.IsChecked == true;
            _model.DataObowiazujeOd = dpOd.SelectedDate ?? DateTime.Today;
            _model.DataObowiazujeDo = dpDo.SelectedDate;
            _model.DataPodpisania = dpPodpisania.SelectedDate;
            _model.Rok = (short)_model.DataObowiazujeOd.Year;
            _model.OkresWypowiedzeniaDni = int.TryParse(txtWypowiedzenie.Text, out var w) ? w : 90;
            _model.ProcentUbytku = decimal.Parse(txtUbytku.Text.Replace(',', '.'), CultureInfo.InvariantCulture);
            _model.Cena = string.IsNullOrWhiteSpace(txtCena.Text)
                ? null
                : decimal.Parse(txtCena.Text.Replace(',', '.'), CultureInfo.InvariantCulture);
            _model.TerminPlatnosciDni = int.Parse(txtTermin.Text);
        }

        // helpers
        private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        private static string? TagOf(ComboBox cb) => (cb.SelectedItem as ComboBoxItem)?.Tag as string;
        private static void SetCombo(ComboBox cb, string tag)
        {
            foreach (var it in cb.Items.OfType<ComboBoxItem>())
                if ((it.Tag as string) == tag) { cb.SelectedItem = it; return; }
        }
    }
}
