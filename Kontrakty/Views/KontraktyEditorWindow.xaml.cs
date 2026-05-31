using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Kalendarz1.Kontrakty.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Views
{
    public enum EditorMode { Nowy, Edycja, Przedluzenie }

    /// <summary>
    /// Edytor kontraktu: tworzenie nowego (nagłówek + wersja 1), edycja warunków wersji
    /// (tylko szkic / w negocjacji) lub przedłużenie (nowa wersja z preselekcją z bieżącej).
    /// </summary>
    public partial class KontraktyEditorWindow : Window
    {
        private static readonly CultureInfo Pl = new("pl-PL");
        private readonly KontraktyService _svc = new();
        private readonly EditorMode _mode;
        private readonly int _kontraktId;
        private readonly int _wersjaId;
        private readonly DispatcherTimer _debounce;

        private HodowcaPicker? _wybranyHodowca;
        private KontraktDetail? _header;
        private bool _ready;

        /// <summary>True, gdy zapisano (caller odświeża listę).</summary>
        public bool Zapisano { get; private set; }

        public KontraktyEditorWindow(EditorMode mode = EditorMode.Nowy, int kontraktId = 0, int wersjaId = 0)
        {
            InitializeComponent();
            _mode = mode; _kontraktId = kontraktId; _wersjaId = wersjaId;
            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _debounce.Tick += async (_, _) => { _debounce.Stop(); await SzukajHodAsync(); };
            dpOd.SelectedDate = DateTime.Today;
            dpPodpis.SelectedDate = DateTime.Today;
            Loaded += async (_, _) => await InitAsync();
        }

        private async System.Threading.Tasks.Task InitAsync()
        {
            if (_mode == EditorMode.Nowy)
            {
                txtTytul.Text = "➕ Nowy kontrakt";
                txtPodtytul.Text = "Wybierz hodowcę i ustaw warunki — numer nadany przy zapisie";
                await SzukajHodAsync();
                AktualizujDateDo();
            }
            else
            {
                // tryby na istniejącym nagłówku
                _header = await _svc.GetDetailAsync(_kontraktId);
                var wersje = await _svc.GetWersjeAsync(_kontraktId);
                panelPicker.Visibility = Visibility.Collapsed;
                btnZmienHod.Visibility = Visibility.Collapsed;
                cbTyp.IsEnabled = false; cbPodmiot.IsEnabled = false; chkArimr.IsEnabled = false;
                cardPowod.Visibility = Visibility.Visible;

                if (_header != null)
                {
                    SelectByTag(cbTyp, _header.TypKontraktu);
                    SelectByTag(cbPodmiot, _header.Podmiot);
                    chkArimr.IsChecked = _header.LiczySieDoArimr;
                    PokazHodowca(_header.NazwaHodowcySnapshot ?? "(hodowca)",
                        $"NIP {_header.NipSnapshot}  ·  gosp. {_header.NrGospodarstwaSnapshot}  ·  {_header.AdresSnapshot}");
                }

                if (_mode == EditorMode.Edycja)
                {
                    txtTytul.Text = $"✏ Edycja warunków — {_header?.NumerKontraktu}";
                    txtPodtytul.Text = "Zmiana warunków bieżącej wersji";
                    var w = wersje.FirstOrDefault(x => x.Id == _wersjaId);
                    if (w != null) WypelnijZWersji(w);
                }
                else // Przedluzenie
                {
                    txtTytul.Text = $"🔄 Przedłużenie — {_header?.NumerKontraktu}";
                    txtPodtytul.Text = "Nowa wersja z preselekcją warunków z bieżącej umowy";
                    var biezaca = wersje.FirstOrDefault(x => x.IsAktualna) ?? wersje.FirstOrDefault();
                    if (biezaca != null)
                    {
                        WypelnijZWersji(biezaca);
                        // nowy okres: od dnia po zakończeniu bieżącej (lub dziś)
                        var od = biezaca.ObowiazujeDo?.AddDays(1) ?? DateTime.Today;
                        dpOd.SelectedDate = od;
                        dpDo.SelectedDate = biezaca.ObowiazujeDo.HasValue ? od.AddYears(LataDlaTypu()) : (DateTime?)null;
                        dpPodpis.SelectedDate = DateTime.Today;
                    }
                    txtPowod.Text = "przedłużenie";
                }
            }
            _ready = true;
        }

        // ── Picker hodowców ──────────────────────────────────────────────────
        private void SzukajHod_Changed(object sender, TextChangedEventArgs e)
        {
            if (!_ready && _mode != EditorMode.Nowy) return;
            _debounce.Stop(); _debounce.Start();
        }

        private async System.Threading.Tasks.Task SzukajHodAsync()
        {
            var dane = await _svc.GetHodowcyAsync(txtSzukajHod.Text);
            lstHodowcy.ItemsSource = dane;
            txtHodCount.Text = dane.Count == 0
                ? (string.IsNullOrWhiteSpace(txtSzukajHod.Text) ? "Zacznij pisać, by wyszukać hodowcę…" : "Brak wyników dla tej frazy")
                : $"{dane.Count} {(dane.Count >= 60 ? "wyników (pokazuję pierwsze 60 — uściślij frazę)" : "wyników")}";
        }

        private void LstHodowcy_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (lstHodowcy.SelectedItem is not HodowcaPicker h) return;
            _wybranyHodowca = h;
            PokazHodowca(h.Nazwa, $"NIP {h.Nip}  ·  gosp. {h.NrGospodarstwa}  ·  {h.Adres}");
            panelPicker.Visibility = Visibility.Collapsed; // zwiń wyszukiwarkę po wyborze
        }

        private void BtnZmienHod_Click(object sender, RoutedEventArgs e)
        {
            panelPicker.Visibility = Visibility.Visible;
            txtSzukajHod.Focus();
        }

        private void PokazHodowca(string nazwa, string meta)
        {
            txtHodNazwa.Text = nazwa;
            txtHodMeta.Text = meta;
            boxHodowca.Visibility = Visibility.Visible;
        }

        private void CbTyp_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready || _mode != EditorMode.Nowy) return;
            AktualizujDateDo();
            chkArimr.IsChecked = TagOf(cbTyp) == "ARIMR_3LAT";
        }

        private int LataDlaTypu() => TagOf(cbTyp) switch
        {
            "ARIMR_3LAT" => 3,
            "WIECZNY" => 0,
            "SPOT" => 0,
            _ => 1
        };

        private void AktualizujDateDo()
        {
            string typ = TagOf(cbTyp);
            var od = dpOd.SelectedDate ?? DateTime.Today;
            if (typ is "WIECZNY")
            {
                chkBezterm.IsChecked = true; ChkBezterm_Click(this, new RoutedEventArgs());
            }
            else if (typ is "SPOT")
            {
                chkBezterm.IsChecked = false; dpDo.IsEnabled = true;
                dpDo.SelectedDate = od.AddDays(7);
            }
            else
            {
                chkBezterm.IsChecked = false; dpDo.IsEnabled = true;
                dpDo.SelectedDate = od.AddYears(LataDlaTypu()).AddDays(-1);
            }
        }

        private void ChkBezterm_Click(object sender, RoutedEventArgs e)
        {
            bool bez = chkBezterm.IsChecked == true;
            dpDo.IsEnabled = !bez;
            if (bez) dpDo.SelectedDate = null;
        }

        // ── Wypełnianie z wersji ─────────────────────────────────────────────
        private void WypelnijZWersji(KontraktWersja w)
        {
            dpOd.SelectedDate = w.ObowiazujeOd;
            dpDo.SelectedDate = w.ObowiazujeDo;
            dpPodpis.SelectedDate = w.DataPodpisania;
            chkBezterm.IsChecked = w.ObowiazujeDo is null;
            dpDo.IsEnabled = w.ObowiazujeDo is not null;
            txtWypowiedzenie.Text = w.OkresWypowiedzeniaDni.ToString();
            SelectByTag(cbTypCeny, w.TypCeny);
            SelectByTag(cbWaga, w.RozliczanaWaga);
            txtCena.Text = Dec(w.Cena);
            txtDodatek.Text = Dec(w.DodatekZl);
            txtUbytek.Text = Dec(w.ProcentUbytku);
            txtTermin.Text = w.TerminPlatnosciDni.ToString();
            txtMinSzt.Text = w.MinimalnaIloscSzt?.ToString() ?? "";
            chkEkskl.IsChecked = w.Ekskluzywnosc;
            txtKlauzule.Text = w.KlauzuleSzczegolne ?? "";
            // rozszerzone warunki
            SelectByTag(cbIndeksacja, w.Indeksacja ?? "STALA");
            SelectByTag(cbCzestotliwosc, w.CzestotliwoscDostaw ?? "CYKL");
            SelectByTag(cbTransport, w.TransportCzyj ?? "NASZ");
            txtCenaMin.Text = Dec(w.CenaMin);
            txtCenaMax.Text = Dec(w.CenaMax);
            txtMaxSzt.Text = w.MaxIloscSzt?.ToString() ?? "";
            txtKaraUmowna.Text = Dec(w.KaraUmownaZl);
            chkPasza.IsChecked = w.PaszaOdNas;
            chkPisklaki.IsChecked = w.PisklakiOdNas;
            chkAutoOdn.IsChecked = w.AutoOdnowienie;
            chkPierwokup.IsChecked = w.PrawoPierwokupu;
            txtOsoba.Text = w.OsobaKontaktowa ?? "";
            txtTelefon.Text = w.TelefonKontaktowy ?? "";
        }

        // ── Zapis ────────────────────────────────────────────────────────────
        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (!Waliduj(out var w)) return;
            string user = Kalendarz1.App.UserID ?? "";
            try
            {
                btnZapisz.IsEnabled = false;
                if (_mode == EditorMode.Nowy)
                {
                    var h = new KontraktDetail
                    {
                        DostawcaId = _wybranyHodowca!.DostawcaId,
                        TypKontraktu = TagOf(cbTyp),
                        LiczySieDoArimr = chkArimr.IsChecked == true,
                        Podmiot = TagOf(cbPodmiot),
                        NazwaHodowcySnapshot = _wybranyHodowca.Nazwa,
                        NipSnapshot = _wybranyHodowca.Nip,
                        NrGospodarstwaSnapshot = _wybranyHodowca.NrGospodarstwa,
                        AdresSnapshot = _wybranyHodowca.Adres
                    };
                    var (_, numer) = await _svc.CreateKontraktAsync(h, w, user);
                    Zapisano = true;
                    MessageBox.Show($"Utworzono kontrakt {numer}.", "Kontrakty",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (_mode == EditorMode.Edycja)
                {
                    w.Id = _wersjaId;
                    await _svc.UpdateWersjaAsync(w);
                    Zapisano = true;
                }
                else // Przedluzenie
                {
                    await _svc.CreateRenewalVersionAsync(_kontraktId, w, user);
                    Zapisano = true;
                    MessageBox.Show("Utworzono nową wersję (status: w negocjacji). " +
                        "Po podpisaniu aktywuj ją w karcie kontraktu.", "Kontrakty",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                btnZapisz.IsEnabled = true;
                MessageBox.Show("Błąd zapisu: " + ex.Message, "Kontrakty",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool Waliduj(out KontraktWersja w)
        {
            w = new KontraktWersja();
            var bledy = new List<string>();

            if (_mode == EditorMode.Nowy && _wybranyHodowca == null)
                bledy.Add("wybierz hodowcę");
            if (dpOd.SelectedDate is null)
                bledy.Add("ustaw datę „od”");
            bool bez = chkBezterm.IsChecked == true;
            if (!bez && dpDo.SelectedDate is null)
                bledy.Add("ustaw datę „do” (lub zaznacz bezterminowy)");
            if (!bez && dpOd.SelectedDate is { } od && dpDo.SelectedDate is { } doo && doo < od)
                bledy.Add("data „do” nie może być wcześniejsza niż „od”");

            if (bledy.Count > 0)
            {
                txtWalidacja.Text = "⚠ " + string.Join(", ", bledy);
                return false;
            }
            txtWalidacja.Text = "";

            w.ObowiazujeOd = dpOd.SelectedDate!.Value;
            w.ObowiazujeDo = bez ? null : dpDo.SelectedDate;
            w.DataPodpisania = dpPodpis.SelectedDate;
            w.OkresWypowiedzeniaDni = ParseInt(txtWypowiedzenie.Text) ?? 90;
            w.TypCeny = TagOf(cbTypCeny);
            w.RozliczanaWaga = TagOf(cbWaga);
            w.Cena = ParseDec(txtCena.Text);
            w.DodatekZl = ParseDec(txtDodatek.Text);
            w.ProcentUbytku = ParseDec(txtUbytek.Text);
            w.TerminPlatnosciDni = ParseInt(txtTermin.Text) ?? 21;
            w.MinimalnaIloscSzt = ParseInt(txtMinSzt.Text);
            w.Ekskluzywnosc = chkEkskl.IsChecked == true;
            w.KlauzuleSzczegolne = string.IsNullOrWhiteSpace(txtKlauzule.Text) ? null : txtKlauzule.Text.Trim();
            w.PowodZmiany = string.IsNullOrWhiteSpace(txtPowod.Text) ? null : txtPowod.Text.Trim();
            w.Status = _mode == EditorMode.Przedluzenie ? "NEGOCJACJE" : "DRAFT";

            // rozszerzone warunki
            w.CenaMin = ParseDec(txtCenaMin.Text);
            w.CenaMax = ParseDec(txtCenaMax.Text);
            w.Indeksacja = PustyNaNull(TagOf(cbIndeksacja));
            w.CzestotliwoscDostaw = PustyNaNull(TagOf(cbCzestotliwosc));
            w.MaxIloscSzt = ParseInt(txtMaxSzt.Text);
            w.TransportCzyj = PustyNaNull(TagOf(cbTransport));
            w.PaszaOdNas = chkPasza.IsChecked == true;
            w.PisklakiOdNas = chkPisklaki.IsChecked == true;
            w.KaraUmownaZl = ParseDec(txtKaraUmowna.Text);
            w.AutoOdnowienie = chkAutoOdn.IsChecked == true;
            w.PrawoPierwokupu = chkPierwokup.IsChecked == true;
            w.OsobaKontaktowa = string.IsNullOrWhiteSpace(txtOsoba.Text) ? null : txtOsoba.Text.Trim();
            w.TelefonKontaktowy = string.IsNullOrWhiteSpace(txtTelefon.Text) ? null : txtTelefon.Text.Trim();
            return true;
        }

        // ── Generowanie Word ─────────────────────────────────────────────────
        private async void BtnGenerujWord_Click(object sender, RoutedEventArgs e)
        {
            if (!Waliduj(out var w)) return;

            string typ = TagOf(cbTyp);
            string? szablon = await _svc.GetTemplatePathAsync(typ);
            if (string.IsNullOrWhiteSpace(szablon))
            {
                MessageBox.Show("Brak szablonu Word dla typu „" + KontraktStatus.TypLabel(typ) +
                    "”. Uzupełnij dbo.KontraktyTemplates i utwórz plik .docx w folderze _SZABLON.",
                    "Generator Word", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!File.Exists(szablon))
            {
                MessageBox.Show("Szablon nie istnieje pod ścieżką:\n" + szablon +
                    "\n\nUtwórz plik .docx z bookmarkami (bm_NumerKontraktu, bm_NazwaHodowcy, ...) " +
                    "wg instrukcji w BAZA_WIEDZY/AUDYT_ZAKUPY_2026_05_23/Szablony_Word.",
                    "Generator Word", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var h = _header ?? new KontraktDetail
            {
                NumerKontraktu = "(szkic — numer po zapisie)",
                TypKontraktu = typ,
                Podmiot = TagOf(cbPodmiot),
                NazwaHodowcySnapshot = _wybranyHodowca?.Nazwa,
                NipSnapshot = _wybranyHodowca?.Nip,
                NrGospodarstwaSnapshot = _wybranyHodowca?.NrGospodarstwa,
                AdresSnapshot = _wybranyHodowca?.Adres
            };
            var values = WordTemplateService.BuildValues(h, w);

            try
            {
                string folder = Path.Combine(Path.GetDirectoryName(szablon) ?? Path.GetTempPath(), "..",
                    w.ObowiazujeOd.Year.ToString());
                string nazwa = $"Umowa_{Bezpieczne(h.NazwaHodowcySnapshot ?? "hodowca")}_{Bezpieczne(h.NumerKontraktu)}.docx";
                string output = Path.Combine(Path.GetFullPath(folder), nazwa);

                var word = new WordTemplateService();
                word.Generuj(szablon, output, values);

                if (_mode == EditorMode.Edycja && _wersjaId > 0)
                    await _svc.SetSciezkiAsync(_wersjaId, output, null);

                Process.Start(new ProcessStartInfo(output) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się wygenerować Word: " + ex.Message, "Generator Word",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static string TagOf(ComboBox cb)
            => (cb.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

        private static string? PustyNaNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

        private static void SelectByTag(ComboBox cb, string tag)
        {
            foreach (var it in cb.Items.OfType<ComboBoxItem>())
                if ((it.Tag?.ToString() ?? "") == tag) { cb.SelectedItem = it; return; }
        }

        private static string Dec(decimal? v) => v?.ToString("0.##", Pl) ?? "";

        private static decimal? ParseDec(string? s)
        {
            s = (s ?? "").Trim().Replace(',', '.');
            if (s.Length == 0) return null;
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        private static int? ParseInt(string? s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return null;
            return int.TryParse(s, out var i) ? i : null;
        }

        private static string Bezpieczne(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace('/', '-').Trim();
        }
    }
}
