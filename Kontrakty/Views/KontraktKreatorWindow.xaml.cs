using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Kalendarz1.Kontrakty.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Views
{
    /// <summary>
    /// Kreator kontraktu — JEDEN EKRAN: hodowca, producent, umowa+okres, warunki, cykle wstawień, skan.
    /// Walidacja zbiorcza w stopce. Cykle przez przyjazny dialog (data + sztuki → auto +33/+42 dni, opcjonalne „co N dni × M razy").
    /// </summary>
    public partial class KontraktKreatorWindow : Window
    {
        private static readonly CultureInfo Pl = new("pl-PL");
        private readonly KontraktyService _svc = new();
        private readonly WordTemplateService _word = new();
        private readonly DispatcherTimer _debounce;
        private readonly ObservableCollection<HarmonogramCykl> _cykle = new();

        private HodowcaPicker? _hod;
        private int? _poprzedniId;
        private string? _skanPath;
        private bool _ready;
        private readonly string? _prefillDostawcaId;
        private readonly bool _trybSeryjny;
        private WarunkiSugestia? _sugestia;
        private int _zapisanychSeryjnie;

        // Smart „Obowiązuje do" — auto z typu kontraktu, dopóki user nie zmieni ręcznie
        private bool _recznaDataDo;
        private bool _ustawiamDoAuto;

        // Auto-zapis draft do %TEMP%
        private DispatcherTimer? _autoSave;
        private static readonly string DraftPath =
            Path.Combine(Path.GetTempPath(), "Kalendarz1", "kreator_draft.json");

        public bool Zapisano { get; private set; }

        public KontraktKreatorWindow(string? prefillDostawcaId = null, bool trybSeryjny = false)
        {
            InitializeComponent();
            _prefillDostawcaId = prefillDostawcaId;
            _trybSeryjny = trybSeryjny;
            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _debounce.Tick += async (_, _) => { _debounce.Stop(); await SzukajHodAsync(); };
            icCykle.ItemsSource = _cykle;

            // Defaulty: od = dziś, do = dziś + 1 rok
            dpOd.SelectedDate = DateTime.Today;
            dpDo.SelectedDate = DateTime.Today.AddYears(1);
            dpPodpis.SelectedDate = DateTime.Today;

            Loaded += async (_, _) => await InitAsync();
        }

        private async System.Threading.Tasks.Task InitAsync()
        {
            if (_trybSeryjny)
            {
                Title = "Nowe kontrakty — tryb seryjny";
                txtTytulKrok.Text = "🔁 Tryb seryjny — po zapisie okno zostaje otwarte na kolejny kontrakt";
                txtStopka.Text = "Tryb seryjny aktywny — zapisuj jeden po drugim. Zamknij okno (✕), gdy skończysz.";
            }
            OdswiezCykle();
            await SzukajHodAsync();
            if (!string.IsNullOrWhiteSpace(_prefillDostawcaId))
            {
                var dane = await _svc.GetHodowcyAsync(_prefillDostawcaId);
                var match = dane.FirstOrDefault(h => h.DostawcaId == _prefillDostawcaId) ?? dane.FirstOrDefault();
                if (match != null) { lstHodowcy.ItemsSource = dane; lstHodowcy.SelectedItem = match; }
            }

            // #4 Smart „Obowiązuje do" — eventy na od/typ/do
            dpOd.SelectedDateChanged += DpOd_SelectedDateChanged;
            cbTyp.SelectionChanged += CbTyp_SelectionChanged;
            dpDo.SelectedDateChanged += DpDo_SelectedDateChanged;

            // #14 Auto-zapis draft co 2 min
            _autoSave = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
            _autoSave.Tick += (_, _) => ZapiszDraft();
            _autoSave.Start();
            Closed += (_, _) => _autoSave?.Stop();

            _ready = true;

            // #14 Sprawdź czy jest draft do przywrócenia (po _ready=true, by eventy działały)
            if (string.IsNullOrWhiteSpace(_prefillDostawcaId) && !_trybSeryjny)
                await SprawdzDraftAsync();

            // #1 Auto-focus na polu szukania (Asia od razu pisze)
            txtSzukaj.Focus();
            Keyboard.Focus(txtSzukaj);
        }

        // ── Picker hodowcy ───────────────────────────────────────────────────
        private void SzukajHod_Changed(object sender, TextChangedEventArgs e)
        { _debounce.Stop(); _debounce.Start(); }

        private async System.Threading.Tasks.Task SzukajHodAsync()
        {
            var dane = await _svc.GetHodowcyAsync(txtSzukaj.Text);
            lstHodowcy.ItemsSource = dane;
            txtCount.Text = dane.Count == 0
                ? (string.IsNullOrWhiteSpace(txtSzukaj.Text) ? "Wpisz frazę, by wyszukać hodowcę…" : "Brak wyników")
                : $"{dane.Count}{(dane.Count >= 60 ? " (pierwsze 60 — uściślij)" : "")} wyników";
        }

        private async void LstHodowcy_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (lstHodowcy.SelectedItem is not HodowcaPicker h) return;
            _hod = h;
            // prefill edytowalnych pól danymi z bazy (109)
            txtNazwa.Text = h.Nazwa;
            txtNip.Text = h.Nip;
            txtPesel.Text = h.Pesel;
            txtRegon.Text = h.Regon;
            txtDowod.Text = h.NrDowodu;
            txtTelefon.Text = h.Telefon;
            txtEmail.Text = h.Email;
            txtGosp.Text = h.NrGospodarstwa;
            txtAdres.Text = h.Adres;

            // banner wybranego
            txtWybranyInicjal.Text = h.Inicjal;
            txtWybranyNazwa.Text = h.Nazwa;
            txtWybranyMeta.Text = h.Meta;
            bannerWybrany.Visibility = Visibility.Visible;

            PoleWalid_Changed(this, null!);

            _poprzedniId = await _svc.GetOstatniKontraktIdHodowcyAsync(h.DostawcaId);
            btnKopiuj.Visibility = _poprzedniId.HasValue ? Visibility.Visible : Visibility.Collapsed;
            btnZIstniejacego.Visibility = _poprzedniId.HasValue ? Visibility.Visible : Visibility.Collapsed;

            // #3 Detektor aktywnego kontraktu — wczytaj listę aktywnych i pokaż banner
            await PokazAktywneKontraktyAsync(h.DostawcaId);

            // sugestie warunków (FarmerCalc)
            _sugestia = await _svc.GetSugestieWarunkowAsync(h.DostawcaId);
            bool maCo = _sugestia.MaDane && _sugestia.UbytekSredniProc != null;
            txtSugestia.Text = _sugestia.MaDane ? _sugestia.Opis : "";
            panelSugestia.Visibility = maCo ? Visibility.Visible : Visibility.Collapsed;
            btnZastosujSugestie.IsEnabled = maCo;

            // historia dostaw w bannerze — kluczowe info inline
            if (_sugestia.MaDane)
            {
                var parts = new List<string> { $"📊 {_sugestia.Dostaw} dostaw 12 mies." };
                if (_sugestia.WagaSrednia is { } waga && _sugestia.Dostaw > 0)
                {
                    // szacunek wolumenu: śr. waga × ilość dostaw (orientacyjnie)
                    // właściwie WagaSrednia to średnia waga ptaka, nie dostawy — bezpieczniej pokazać sztuki/dostawę
                    parts.Add($"~{waga:0.0} kg/ptak");
                }
                if (_sugestia.OstatniaDostawa is { } ost)
                    parts.Add($"ost. {ost:dd.MM.yyyy}");
                txtWybranyHistoria.Text = string.Join("  ·  ", parts);
                boxHistoria.Visibility = Visibility.Visible;
            }
            else
            {
                txtWybranyHistoria.Text = "📊 Brak historii dostaw";
                boxHistoria.Visibility = Visibility.Visible;
            }
        }

        private void BtnZastosujSugestie_Click(object sender, RoutedEventArgs e)
        {
            if (_sugestia?.UbytekSredniProc is { } u) txtUbytek.Text = u.ToString("0.0", Pl);
        }

        // #3 — banner "hodowca ma aktywny kontrakt"
        private async System.Threading.Tasks.Task PokazAktywneKontraktyAsync(string dostawcaId)
        {
            try
            {
                var akt = await _svc.GetAktywneKontraktyHodowcyAsync(dostawcaId);
                if (akt.Count == 0)
                {
                    boxAktywneKontrakty.Visibility = Visibility.Collapsed;
                    return;
                }
                var pierwszy = akt[0];
                txtAktywneTytul.Text = akt.Count == 1
                    ? $"⚠ Hodowca ma aktywny kontrakt {pierwszy.NumerKontraktu}"
                    : $"⚠ Hodowca ma {akt.Count} aktywne kontrakty (najbliżej wygasa {pierwszy.NumerKontraktu})";
                txtAktywneSzczegoly.Text =
                    $"{pierwszy.TypLabel} · {pierwszy.OkresLabel}. Tworzysz aneks/przedłużenie? Kliknij „Zobacz/skopiuj”.";
                boxAktywneKontrakty.Visibility = Visibility.Visible;
            }
            catch
            {
                boxAktywneKontrakty.Visibility = Visibility.Collapsed;
            }
        }

        // Otwarcie Sprawdzalki Umów z prefilowanym hodowcą — szybki podgląd historii dostaw
        private void BtnSprawdzalka_Click(object sender, RoutedEventArgs e)
        {
            if (_hod == null) return;
            try
            {
                var w = new Kalendarz1.WPF.SprawdzalkaUmowWindow(Kalendarz1.App.UserID ?? "") { Owner = this };
                // pole szukania w Sprawdzalce — pre-fill na nazwę hodowcy (internal x:Name field)
                w.txtSearch.Text = _hod.Nazwa;
                w.Show(); // non-modal, by user mógł flippować z kreatorem
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się otworzyć Sprawdzalki: " + ex.Message, "Sprawdzalka",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BtnKopiuj_Click(object sender, RoutedEventArgs e)
        {
            if (_poprzedniId is not int kid) return;
            await KopiujZKontraktuAsync(kid);
        }

        // #4 — wybierz z listy wszystkich kontraktów hodowcy
        private async void BtnZIstniejacego_Click(object sender, RoutedEventArgs e)
        {
            if (_hod == null) return;
            List<Models.KontraktListItem> lista;
            try { lista = await _svc.GetKontraktyHodowcyAsync(_hod.DostawcaId); }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się wczytać listy kontraktów: " + ex.Message, "Z istniejącego",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (lista.Count == 0)
            {
                MessageBox.Show("Ten hodowca nie ma jeszcze żadnych kontraktów do skopiowania.",
                    "Z istniejącego", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dlg = new WybierzKontraktDialog($"Hodowca: {_hod.Nazwa}", lista) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.WybranyId is int kid)
                await KopiujZKontraktuAsync(kid);
        }

        private async System.Threading.Tasks.Task KopiujZKontraktuAsync(int kontraktId)
        {
            var det = await _svc.GetDetailAsync(kontraktId);
            var wersje = await _svc.GetWersjeAsync(kontraktId);
            var w = wersje.FirstOrDefault(x => x.IsAktualna) ?? wersje.FirstOrDefault();
            if (det == null || w == null)
            {
                MessageBox.Show("Nie udało się wczytać szczegółów kontraktu.", "Z istniejącego",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectByTag(cbTyp, det.TypKontraktu);
            SelectByTag(cbPodmiot, det.Podmiot);
            txtEmail.Text = det.EmailRODO ?? "";

            SelectByTag(cbTypCeny, w.TypCeny);
            SelectByTag(cbWaga, w.RozliczanaWaga);
            txtDodatek.Text = Dec(w.DodatekZl);
            txtUbytek.Text = Dec(w.ProcentUbytku);
            txtTermin.Text = w.TerminPlatnosciDni.ToString();
            chkKonfiskatyHodowca.IsChecked = w.KonfiskatyHodowca;

            // nowy okres: od dnia po zakończeniu źródłowego (lub dziś) → +1 rok
            var od = w.ObowiazujeDo?.AddDays(1) ?? DateTime.Today;
            _ustawiamDoAuto = true;
            dpOd.SelectedDate = od;
            dpDo.SelectedDate = od.AddYears(1);
            _ustawiamDoAuto = false;
            _recznaDataDo = true;
            dpPodpis.SelectedDate = DateTime.Today;

            var stary = await _svc.GetHarmonogramAsync(w.Id);
            _cykle.Clear();
            var offset = od - (w.ObowiazujeOd);
            foreach (var c in stary.OrderBy(x => x.NrCyklu))
                _cykle.Add(new HarmonogramCykl
                {
                    NrCyklu = c.NrCyklu,
                    DataWstawienia = c.DataWstawienia?.Add(offset),
                    IloscWstawiona = c.IloscWstawiona,
                    DzienUbiorki = c.DzienUbiorki ?? 33,
                    DataUbojuKoncowego = c.DataUbojuKoncowego?.Add(offset),
                    Status = "PLANOWANY"
                });
            OdswiezCykle();
            txtStopka.Text = $"✔ Skopiowano warunki i {_cykle.Count} cykli z {det.NumerKontraktu}.";
        }

        // #1 — GUS / Biała Lista MF: pobierz nazwę + REGON + adres po NIP
        private async void BtnGus_Click(object sender, RoutedEventArgs e)
        {
            string nip = (txtNip.Text ?? "").Trim();
            if (!Walidatory.NipPoprawny(nip))
            {
                MessageBox.Show("NIP nieprawidłowy (suma kontrolna). Sprawdź cyfry.",
                    "GUS / Biała Lista MF", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            btnGus.IsEnabled = false;
            string staryLabel = txtGusLabel.Text;
            txtGusLabel.Text = "Pobieram…";
            try
            {
                var r = await GusApiService.PobierzPoNipAsync(nip);
                if (!r.Znaleziono)
                {
                    MessageBox.Show(r.Komunikat ?? "Nie znaleziono podmiotu w MF.",
                        "GUS / Biała Lista MF", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // nadpisuj tylko puste pola — szanujemy ręczne wpisy
                int zmian = 0;
                if (string.IsNullOrWhiteSpace(txtNazwa.Text) && !string.IsNullOrWhiteSpace(r.Nazwa)) { txtNazwa.Text = r.Nazwa; zmian++; }
                if (string.IsNullOrWhiteSpace(txtRegon.Text) && !string.IsNullOrWhiteSpace(r.Regon)) { txtRegon.Text = r.Regon; zmian++; }
                if (string.IsNullOrWhiteSpace(txtAdres.Text) && !string.IsNullOrWhiteSpace(r.Adres)) { txtAdres.Text = r.Adres; zmian++; }
                string statusInfo = string.IsNullOrEmpty(r.StatusVat) ? "" : $" · VAT: {r.StatusVat}";
                txtStopka.Text = zmian > 0
                    ? $"✔ Pobrano z MF: {r.Nazwa}{statusInfo} (uzupełniono {zmian} {Odmiana(zmian, "pole", "pola", "pól")})."
                    : $"ℹ MF zwrócił dane, ale wszystkie pola były już wypełnione.{statusInfo}";
            }
            finally
            {
                txtGusLabel.Text = staryLabel;
                btnGus.IsEnabled = Walidatory.NipPoprawny(txtNip.Text);
            }
        }

        // ── Cykle wstawień ──────────────────────────────────────────────────
        private void BtnDodajCykl_Click(object sender, RoutedEventArgs e)
        {
            int nr = _cykle.Count == 0 ? 1 : _cykle.Max(c => c.NrCyklu) + 1;
            var domyslna = _cykle.LastOrDefault()?.DataWstawienia ?? dpOd.SelectedDate ?? DateTime.Today;
            // przy nowych dodaniach proponuj kolejną datę po ostatnim cyklu
            if (_cykle.Count > 0) domyslna = domyslna.AddDays(67);
            var dlg = new KontraktWstawienieDialog(null, nr, domyslna) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                foreach (var c in dlg.Wynik) _cykle.Add(c);
                Renumeruj();
                OdswiezCykle();
            }
        }

        private void BtnEdytujCykl_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not HarmonogramCykl c) return;
            int idx = _cykle.IndexOf(c);
            if (idx < 0) return;
            var dlg = new KontraktWstawienieDialog(c, c.NrCyklu, c.DataWstawienia) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Wynik.Count > 0)
            {
                _cykle[idx] = dlg.Wynik[0]; OdswiezCykle();
            }
        }

        private void BtnUsunCyklJeden_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is HarmonogramCykl c) { _cykle.Remove(c); Renumeruj(); OdswiezCykle(); }
        }

        private void BtnWyczyscCykle_Click(object sender, RoutedEventArgs e)
        {
            if (_cykle.Count == 0) return;
            if (MessageBox.Show($"Usunąć wszystkie {_cykle.Count} cykli?", "Cykle wstawień",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            _cykle.Clear(); OdswiezCykle();
        }

        private void Renumeruj()
        { for (int i = 0; i < _cykle.Count; i++) _cykle[i].NrCyklu = i + 1; }

        private void OdswiezCykle()
        {
            icCykle.ItemsSource = null;
            icCykle.ItemsSource = _cykle;
            if (IsLoaded) boxCyklePusto.Visibility = _cykle.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            int sumaSzt = _cykle.Sum(c => c.IloscWstawiona ?? 0);
            txtKpiCykle.Text = sumaSzt > 0
                ? $"{_cykle.Count} {Odmiana(_cykle.Count, "wstawienie", "wstawienia", "wstawień")} · {sumaSzt:N0} szt. razem"
                : $"{_cykle.Count} {Odmiana(_cykle.Count, "wstawienie", "wstawienia", "wstawień")}";
        }

        private void ChkBezterm_Click(object sender, RoutedEventArgs e)
        {
            bool bez = chkBezterm.IsChecked == true;
            dpDo.IsEnabled = !bez;
            if (bez) dpDo.SelectedDate = null;
        }

        private void BtnWybierzSkan_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            { Title = "Wybierz skan (PDF)", Filter = "PDF (*.pdf)|*.pdf|Wszystkie (*.*)|*.*" };
            if (dlg.ShowDialog() == true) { _skanPath = dlg.FileName; txtSkanPlik.Text = Path.GetFileName(dlg.FileName); }
        }

        // ── Walidacja inline (NIP/PESEL/ARiMR) ──────────────────────────────
        private static readonly Brush BrushErr  = new SolidColorBrush(Color.FromRgb(0xDC,0x26,0x26));
        private static readonly Brush BrushOk   = new SolidColorBrush(Color.FromRgb(0x16,0xA3,0x4A));
        private static readonly Brush BrushWarn = new SolidColorBrush(Color.FromRgb(0xB4,0x53,0x09));

        private void PoleWalid_Changed(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            bool? nipOk = string.IsNullOrWhiteSpace(txtNip.Text) ? (bool?)null : Walidatory.NipPoprawny(txtNip.Text);
            Oznacz(txtNip, hintNip, nipOk,
                "✓ NIP poprawny", "⛔ błędna suma kontrolna NIP", blokujace: true);
            btnGus.IsEnabled = nipOk == true;
            Oznacz(txtPesel, hintPesel,
                string.IsNullOrWhiteSpace(txtPesel.Text) ? (bool?)null : Walidatory.PeselPoprawny(txtPesel.Text),
                "✓ PESEL poprawny", "⛔ błędna suma kontrolna PESEL", blokujace: true);
            Oznacz(txtGosp, hintGosp,
                string.IsNullOrWhiteSpace(txtGosp.Text) ? (bool?)null : Walidatory.ArimrPoprawny(txtGosp.Text),
                "✓ format ARiMR OK", "⚠ nie pasuje do PL+9 cyfr", blokujace: false);
        }

        private void Oznacz(TextBox tb, TextBlock hint, bool? ok, string okMsg, string errMsg, bool blokujace)
        {
            if (ok is null)
            {
                tb.ClearValue(BorderBrushProperty);
                hint.Visibility = Visibility.Collapsed;
                return;
            }
            if (ok == true)
            {
                tb.BorderBrush = BrushOk;
                hint.Text = okMsg; hint.Foreground = BrushOk; hint.Visibility = Visibility.Visible;
            }
            else
            {
                tb.BorderBrush = blokujace ? BrushErr : BrushWarn;
                hint.Text = errMsg; hint.Foreground = blokujace ? BrushErr : BrushWarn; hint.Visibility = Visibility.Visible;
            }
        }

        // ── Walidacja ZBIORCZA (jeden ekran, jeden komunikat) ────────────────
        private bool Waliduj()
        {
            var bledy = new List<string>();
            var ostrzezenia = new List<string>();

            if (_hod == null) bledy.Add("wybierz hodowcę z listy");
            if (string.IsNullOrWhiteSpace(txtNazwa.Text)) bledy.Add("podaj nazwę producenta");
            if (!string.IsNullOrWhiteSpace(txtNip.Text) && !Walidatory.NipPoprawny(txtNip.Text))
                bledy.Add("NIP — błędna suma kontrolna");
            if (!string.IsNullOrWhiteSpace(txtPesel.Text) && !Walidatory.PeselPoprawny(txtPesel.Text))
                bledy.Add("PESEL — błędna suma kontrolna");
            if (!string.IsNullOrWhiteSpace(txtGosp.Text) && !Walidatory.ArimrPoprawny(txtGosp.Text))
                ostrzezenia.Add("nr gospodarstwa nie pasuje do PL+9 cyfr");

            if (dpOd.SelectedDate is null) bledy.Add("ustaw datę „od”");
            bool bez = chkBezterm.IsChecked == true;
            if (!bez && dpDo.SelectedDate is null) bledy.Add("ustaw datę „do” (lub bezterminowy)");
            if (!bez && dpOd.SelectedDate is { } od && dpDo.SelectedDate is { } doo && doo < od)
                bledy.Add("data „do” < „od”");

            var harm = Walidatory.WalidujHarmonogram(_cykle);
            if (Walidatory.MaBlad(harm)) bledy.AddRange(harm.Where(x => x.StartsWith("BŁĄD")));
            foreach (var u in harm.Where(x => x.StartsWith("UWAGA"))) ostrzezenia.Add(u.Replace("UWAGA:", "").Trim());

            if (bledy.Count > 0)
            {
                txtWalidacja.Text = "⛔ " + string.Join("  ·  ", bledy) +
                    (ostrzezenia.Count > 0 ? "    ⚠ " + string.Join("  ·  ", ostrzezenia) : "");
                boxWalidacja.Visibility = Visibility.Visible;
                return false;
            }
            if (ostrzezenia.Count > 0)
            {
                txtWalidacja.Text = "⚠ " + string.Join("  ·  ", ostrzezenia);
                boxWalidacja.Visibility = Visibility.Visible;
            }
            else boxWalidacja.Visibility = Visibility.Collapsed;
            return true;
        }

        private static string Odmiana(int n, string f1, string f234, string f5)
        {
            if (n == 1) return f1;
            int last = n % 10, last2 = n % 100;
            if (last >= 2 && last <= 4 && (last2 < 12 || last2 > 14)) return f234;
            return f5;
        }

        // ── Budowa modelu z formularza (reuse: zapis + podgląd) ──────────────
        private (KontraktDetail h, KontraktWersja w, List<HarmonogramCykl> cykle) BudujModelZFormularza()
        {
            var h = new KontraktDetail
            {
                DostawcaId = _hod?.DostawcaId ?? "",
                TypKontraktu = TagOf(cbTyp),
                LiczySieDoArimr = false,                                  // ARiMR checkbox USUNIĘTY — zawsze false
                Podmiot = TagOf(cbPodmiot),
                NazwaHodowcySnapshot = Nn(txtNazwa.Text),
                NipSnapshot = Nn(txtNip.Text),
                NrGospodarstwaSnapshot = Nn(txtGosp.Text),
                AdresSnapshot = Nn(txtAdres.Text),
                EmailRODO = Nn(txtEmail.Text),
                PeselSnapshot = Nn(txtPesel.Text),
                RegonSnapshot = Nn(txtRegon.Text),
                NrDowoduSnapshot = Nn(txtDowod.Text),
                TelefonSnapshot = Nn(txtTelefon.Text)
            };
            var w = new KontraktWersja
            {
                Status = "DRAFT",
                ObowiazujeOd = dpOd.SelectedDate ?? DateTime.Today,
                ObowiazujeDo = chkBezterm.IsChecked == true ? null : dpDo.SelectedDate,
                DataPodpisania = dpPodpis.SelectedDate,
                OkresWypowiedzeniaDni = ParseInt(txtWypow.Text) ?? 90,
                TypCeny = TagOf(cbTypCeny),
                RozliczanaWaga = TagOf(cbWaga),
                Cena = null,                                       // Cena bazowa USUNIĘTA — zawsze null
                DodatekZl = ParseDec(txtDodatek.Text),
                ProcentUbytku = ParseDec(txtUbytek.Text),
                TerminPlatnosciDni = ParseInt(txtTermin.Text) ?? 21,
                BonusOpis = null,                                         // Bonus USUNIĘTY — zawsze null
                DostawcaPaszyNazwa = null,                                // Pasza USUNIĘTA — zawsze null
                DostawcaPisklatNazwa = null,                              // Pisklak USUNIĘTY — zawsze null
                KonfiskatyHodowca = chkKonfiskatyHodowca.IsChecked == true
            };
            var cykle = _cykle.ToList();
            for (int i = 0; i < cykle.Count; i++) cykle[i].NrCyklu = i + 1;
            return (h, w, cykle);
        }

        // ── Podgląd treści umowy (bez zapisu) ────────────────────────────────
        private async void BtnPodglad_Click(object sender, RoutedEventArgs e)
        {
            if (_hod == null) { txtWalidacja.Text = "⛔ Wybierz hodowcę przed podglądem."; boxWalidacja.Visibility = Visibility.Visible; return; }
            var (h, w, cykle) = BudujModelZFormularza();
            string? szablon = await _svc.GetTemplatePathAsync(h.TypKontraktu);
            if (string.IsNullOrWhiteSpace(szablon) || !File.Exists(szablon))
            {
                MessageBox.Show("Brak/niedostępny szablon Word dla typu „" + KontraktStatus.TypLabel(h.TypKontraktu) +
                    "”. Podgląd wymaga szablonu w _SZABLON\\.", "Podgląd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string temp = Path.Combine(Path.GetTempPath(), $"podglad_{Guid.NewGuid():N}.docx");
            try
            {
                var tokeny = WordTemplateService.BuildKontraktacjaTokens(h, w, "(numer nadany przy zapisie)");
                _word.GenerujKontraktacja(szablon!, temp, tokeny, cykle);
                new KontraktPodgladWindow(temp, $"Podgląd umowy — {h.NazwaHodowcySnapshot}") { Owner = this }.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się przygotować podglądu: " + ex.Message, "Podgląd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        }

        // ── Zapis ────────────────────────────────────────────────────────────
        private async void BtnZapisz_Click(object sender, RoutedEventArgs e) => await ZapiszAsync(false);
        private async void BtnZapiszWord_Click(object sender, RoutedEventArgs e) => await ZapiszAsync(true);
        private void BtnAnuluj_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private async System.Threading.Tasks.Task ZapiszAsync(bool generujWord)
        {
            if (!Waliduj()) return;

            string user = Kalendarz1.App.UserID ?? "";
            var (h, w, cykle) = BudujModelZFormularza();

            int kontraktId, wersjaId; string numer;
            try
            {
                (kontraktId, wersjaId, numer) = await _svc.CreateKontraktZHarmonogramAsync(h, w, cykle, user);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu kontraktu: " + ex.Message, "Kreator", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var komunikaty = new List<string> { $"Utworzono kontrakt {numer}." };

            if (!string.IsNullOrWhiteSpace(_skanPath))
            {
                try
                {
                    await ZalacznikiHelper.UploadAsync(_svc, kontraktId, wersjaId, numer, w.ObowiazujeOd.Year,
                        TagOf(cbTypZal), _skanPath!, user);
                    komunikaty.Add("Skan podpięty.");
                }
                catch (Exception ex) { komunikaty.Add("Skan NIE podpięty: " + ex.Message); }
            }

            if (generujWord)
            {
                try { await GenerujWordAsync(h, w, cykle, numer); komunikaty.Add("Word wygenerowany."); }
                catch (Exception ex) { komunikaty.Add("Word NIE wygenerowany: " + ex.Message); }
            }

            Zapisano = true;
            _zapisanychSeryjnie++;
            UsunDraft();    // #14 zapisany realnie → draft niepotrzebny

            if (_trybSeryjny)
            {
                txtStopka.Text = $"✔ Zapisano {numer}. Wprowadź następny kontrakt (w tej sesji: {_zapisanychSeryjnie}).";
                ResetujFormularz();
                return;
            }

            MessageBox.Show(string.Join("\n", komunikaty), "Kreator", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        /// <summary>Tryb seryjny — czyści formularz pod kolejny kontrakt.</summary>
        private void ResetujFormularz()
        {
            _hod = null; _poprzedniId = null; _skanPath = null; _sugestia = null;
            foreach (var tb in new[] { txtNazwa, txtNip, txtPesel, txtRegon, txtDowod, txtTelefon, txtEmail,
                                       txtGosp, txtAdres, txtDodatek, txtSkanPlik })
                tb.Clear();
            txtUbytek.Text = "3,0"; txtTermin.Text = "21"; txtWypow.Text = "90";
            cbTyp.SelectedIndex = 0; cbPodmiot.SelectedIndex = 0; cbTypCeny.SelectedIndex = 0; cbWaga.SelectedIndex = 0; cbTypZal.SelectedIndex = 0;
            chkBezterm.IsChecked = false; chkKonfiskatyHodowca.IsChecked = true;
            _cykle.Clear(); OdswiezCykle();
            _recznaDataDo = false;          // reset trybu auto „do"
            _ustawiamDoAuto = true;
            dpOd.SelectedDate = DateTime.Today;
            dpDo.SelectedDate = DateTime.Today.AddYears(1);
            _ustawiamDoAuto = false;
            dpPodpis.SelectedDate = DateTime.Today;
            hintNip.Visibility = hintPesel.Visibility = hintGosp.Visibility = Visibility.Collapsed;
            txtNip.ClearValue(BorderBrushProperty); txtPesel.ClearValue(BorderBrushProperty); txtGosp.ClearValue(BorderBrushProperty);
            panelSugestia.Visibility = Visibility.Collapsed;
            btnKopiuj.Visibility = Visibility.Collapsed;
            btnZIstniejacego.Visibility = Visibility.Collapsed;
            boxAktywneKontrakty.Visibility = Visibility.Collapsed;
            btnGus.IsEnabled = false;
            bannerWybrany.Visibility = Visibility.Collapsed; boxHistoria.Visibility = Visibility.Collapsed;
            boxWalidacja.Visibility = Visibility.Collapsed;
            lstHodowcy.SelectedItem = null; txtSzukaj.Clear();
            txtSzukaj.Focus();
        }

        private async System.Threading.Tasks.Task GenerujWordAsync(KontraktDetail h, KontraktWersja w, List<HarmonogramCykl> cykle, string numer)
        {
            string? szablon = await _svc.GetTemplatePathAsync(h.TypKontraktu);
            if (string.IsNullOrWhiteSpace(szablon) || !File.Exists(szablon))
                throw new FileNotFoundException("Brak/niedostępny szablon Word dla typu " + h.TypLabel +
                    ". Umieść .docx w _SZABLON\\ i uruchom AddBookmark.");

            string folder = Path.Combine(ZalacznikiHelper.Root, w.ObowiazujeOd.Year.ToString());
            string nazwa = $"Umowa_{ZalacznikiHelper.SanitizeNumer(numer)}_{Bezpieczne(h.NazwaHodowcySnapshot ?? "hodowca")}.docx";
            string output = Path.Combine(folder, nazwa);

            var tokeny = WordTemplateService.BuildKontraktacjaTokens(h, w, numer);
            _word.GenerujKontraktacja(szablon!, output, tokeny, cykle);
            try { Process.Start(new ProcessStartInfo(output) { UseShellExecute = true }); } catch { }
        }

        // ── #4 Smart „Obowiązuje do" ─────────────────────────────────────────
        private void DpOd_SelectedDateChanged(object? s, SelectionChangedEventArgs e)
        {
            if (!_ready || _ustawiamDoAuto || _recznaDataDo) return;
            AktualizujDoAuto();
        }

        private void CbTyp_SelectionChanged(object? s, SelectionChangedEventArgs e)
        {
            if (!_ready || _ustawiamDoAuto || _recznaDataDo) return;
            AktualizujDoAuto();
        }

        private void DpDo_SelectedDateChanged(object? s, SelectionChangedEventArgs e)
        {
            if (!_ready || _ustawiamDoAuto) return;
            _recznaDataDo = true;   // user zmienił sam → przestajemy auto-aktualizować
        }

        private void AktualizujDoAuto()
        {
            if (dpOd.SelectedDate is not { } od) return;
            if (chkBezterm.IsChecked == true) return;

            DateTime nowaDo = TagOf(cbTyp) switch
            {
                "ARIMR_3LAT" => od.AddYears(3),
                "SEZONOWY"   => od.AddMonths(6),
                "ROCZNY"     => od.AddYears(1),
                _            => od.AddYears(1),    // WIECZNY/SPOT — i tak rzadko z datą do, +1 rok jako placeholder
            };
            _ustawiamDoAuto = true;
            dpDo.SelectedDate = nowaDo;
            _ustawiamDoAuto = false;
        }

        // ── #12 Drag&drop PDF na karcie SKAN ─────────────────────────────────
        private void SkanBorder_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && files[0].EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    e.Effects = DragDropEffects.Copy;
            }
            e.Handled = true;
        }

        private void SkanBorder_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 0) return;
            string plik = files[0];
            if (!plik.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Tylko pliki PDF są akceptowane.", "Skan",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _skanPath = plik;
            txtSkanPlik.Text = Path.GetFileName(plik);
            e.Handled = true;
        }

        // ── #14 Auto-zapis draft i przywracanie ──────────────────────────────
        private class DraftDto
        {
            public string? HodowcaDostawcaId { get; set; }
            public string? Nazwa { get; set; }
            public string? Nip { get; set; }
            public string? Pesel { get; set; }
            public string? Regon { get; set; }
            public string? Dowod { get; set; }
            public string? Telefon { get; set; }
            public string? Email { get; set; }
            public string? Gosp { get; set; }
            public string? Adres { get; set; }
            public string? TypTag { get; set; }
            public string? PodmiotTag { get; set; }
            public DateTime? DataOd { get; set; }
            public DateTime? DataDo { get; set; }
            public DateTime? DataPodpis { get; set; }
            public bool Bezterm { get; set; }
            public string? Wypow { get; set; }
            public string? TypCenyTag { get; set; }
            public string? WagaTag { get; set; }
            public string? Ubytek { get; set; }
            public string? Termin { get; set; }
            public string? Dodatek { get; set; }
            public bool KonfHodowca { get; set; }
            public string? TypZalTag { get; set; }
            public string? SkanPath { get; set; }
            public List<DraftCykl> Cykle { get; set; } = new();
            public DateTime Zapisano { get; set; }
        }

        private class DraftCykl
        {
            public int NrCyklu { get; set; }
            public DateTime? DataWstawienia { get; set; }
            public int? IloscWstawiona { get; set; }
            public int? DzienUbiorki { get; set; }
            public DateTime? DataUbojuKoncowego { get; set; }
            public int? IloscUboju { get; set; }
        }

        private bool MaJakieŚDane()
            => _hod != null
            || !string.IsNullOrWhiteSpace(txtNazwa.Text)
            || !string.IsNullOrWhiteSpace(txtNip.Text)
            || _cykle.Count > 0;

        private void ZapiszDraft()
        {
            if (!_ready || !MaJakieŚDane()) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DraftPath)!);
                var d = new DraftDto
                {
                    HodowcaDostawcaId = _hod?.DostawcaId,
                    Nazwa = Nn(txtNazwa.Text), Nip = Nn(txtNip.Text), Pesel = Nn(txtPesel.Text),
                    Regon = Nn(txtRegon.Text), Dowod = Nn(txtDowod.Text), Telefon = Nn(txtTelefon.Text),
                    Email = Nn(txtEmail.Text), Gosp = Nn(txtGosp.Text), Adres = Nn(txtAdres.Text),
                    TypTag = TagOf(cbTyp), PodmiotTag = TagOf(cbPodmiot),
                    DataOd = dpOd.SelectedDate, DataDo = dpDo.SelectedDate, DataPodpis = dpPodpis.SelectedDate,
                    Bezterm = chkBezterm.IsChecked == true,
                    Wypow = txtWypow.Text,
                    TypCenyTag = TagOf(cbTypCeny), WagaTag = TagOf(cbWaga),
                    Ubytek = txtUbytek.Text, Termin = txtTermin.Text, Dodatek = Nn(txtDodatek.Text),
                    KonfHodowca = chkKonfiskatyHodowca.IsChecked == true,
                    TypZalTag = TagOf(cbTypZal), SkanPath = _skanPath,
                    Cykle = _cykle.Select(c => new DraftCykl
                    {
                        NrCyklu = c.NrCyklu,
                        DataWstawienia = c.DataWstawienia,
                        IloscWstawiona = c.IloscWstawiona,
                        DzienUbiorki = c.DzienUbiorki,
                        DataUbojuKoncowego = c.DataUbojuKoncowego,
                        IloscUboju = c.IloscUboju
                    }).ToList(),
                    Zapisano = DateTime.Now
                };
                File.WriteAllText(DraftPath, JsonSerializer.Serialize(d));
            }
            catch { /* draft to luksus — nie psuj user-flow */ }
        }

        private void UsunDraft()
        {
            try { if (File.Exists(DraftPath)) File.Delete(DraftPath); } catch { }
        }

        private async System.Threading.Tasks.Task SprawdzDraftAsync()
        {
            if (!File.Exists(DraftPath)) return;
            DraftDto? d;
            try
            {
                var json = await File.ReadAllTextAsync(DraftPath);
                d = JsonSerializer.Deserialize<DraftDto>(json);
            }
            catch { UsunDraft(); return; }
            if (d == null) return;

            // Stary draft (>24h) → wyrzuć cicho
            if ((DateTime.Now - d.Zapisano).TotalHours > 24) { UsunDraft(); return; }

            int min = Math.Max(1, (int)(DateTime.Now - d.Zapisano).TotalMinutes);
            string czas = min < 60 ? $"{min} min temu" : $"{min / 60} godz. temu";
            var r = MessageBox.Show(
                $"Znaleziono niezapisany kontrakt sprzed {czas}.\nPrzywrócić?",
                "Niezapisany kreator",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) { UsunDraft(); return; }

            await PrzywrocDraftAsync(d);
        }

        private async System.Threading.Tasks.Task PrzywrocDraftAsync(DraftDto d)
        {
            _ustawiamDoAuto = true;
            try
            {
                // Hodowca z bazy
                if (!string.IsNullOrEmpty(d.HodowcaDostawcaId))
                {
                    var dane = await _svc.GetHodowcyAsync(d.HodowcaDostawcaId);
                    var h = dane.FirstOrDefault(x => x.DostawcaId == d.HodowcaDostawcaId);
                    if (h != null)
                    {
                        lstHodowcy.ItemsSource = dane;
                        lstHodowcy.SelectedItem = h;   // wywoła LstHodowcy_Changed → wypełni z 109
                    }
                }

                // Pola producenta — nadpisujemy po prefillu (user mógł zmienić)
                txtNazwa.Text = d.Nazwa ?? ""; txtNip.Text = d.Nip ?? ""; txtPesel.Text = d.Pesel ?? "";
                txtRegon.Text = d.Regon ?? ""; txtDowod.Text = d.Dowod ?? ""; txtTelefon.Text = d.Telefon ?? "";
                txtEmail.Text = d.Email ?? ""; txtGosp.Text = d.Gosp ?? ""; txtAdres.Text = d.Adres ?? "";

                SelectByTag(cbTyp, d.TypTag ?? "ROCZNY");
                SelectByTag(cbPodmiot, d.PodmiotTag ?? "PIORKOWSCY_SC");

                dpOd.SelectedDate = d.DataOd;
                dpDo.SelectedDate = d.DataDo;
                dpPodpis.SelectedDate = d.DataPodpis;
                chkBezterm.IsChecked = d.Bezterm;
                if (!string.IsNullOrEmpty(d.Wypow)) txtWypow.Text = d.Wypow;

                SelectByTag(cbTypCeny, d.TypCenyTag ?? "wolnorynkowa");
                SelectByTag(cbWaga, d.WagaTag ?? "NETTO_HODOWCY");
                if (!string.IsNullOrEmpty(d.Ubytek)) txtUbytek.Text = d.Ubytek;
                if (!string.IsNullOrEmpty(d.Termin)) txtTermin.Text = d.Termin;
                txtDodatek.Text = d.Dodatek ?? "";
                chkKonfiskatyHodowca.IsChecked = d.KonfHodowca;

                SelectByTag(cbTypZal, d.TypZalTag ?? "SKAN_PODPISANY");
                _skanPath = d.SkanPath;
                if (!string.IsNullOrEmpty(_skanPath) && File.Exists(_skanPath))
                    txtSkanPlik.Text = Path.GetFileName(_skanPath);

                _cykle.Clear();
                foreach (var c in d.Cykle)
                    _cykle.Add(new HarmonogramCykl
                    {
                        NrCyklu = c.NrCyklu,
                        DataWstawienia = c.DataWstawienia,
                        IloscWstawiona = c.IloscWstawiona,
                        DzienUbiorki = c.DzienUbiorki ?? 33,
                        DataUbojuKoncowego = c.DataUbojuKoncowego,
                        IloscUboju = c.IloscUboju,
                        Status = "PLANOWANY"
                    });
                OdswiezCykle();

                _recznaDataDo = true; // przywrócone „do" traktujemy jak ustawione ręcznie (nie nadpisujemy)
            }
            finally
            {
                _ustawiamDoAuto = false;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static string TagOf(ComboBox cb) => (cb.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        private static void SelectByTag(ComboBox cb, string tag)
        { foreach (var it in cb.Items.OfType<ComboBoxItem>()) if ((it.Tag?.ToString() ?? "") == tag) { cb.SelectedItem = it; return; } }
        private static string? Nn(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        private static string Dec(decimal? v) => v?.ToString("0.##", Pl) ?? "";
        private static decimal? ParseDec(string? s)
        { s = (s ?? "").Trim().Replace(',', '.'); return s.Length == 0 ? null : (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null); }
        private static int? ParseInt(string? s) { s = (s ?? "").Trim(); return s.Length == 0 ? null : (int.TryParse(s, out var i) ? i : null); }
        private static string Bezpieczne(string s) { foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s.Replace('/', '-').Trim(); }
    }
}
