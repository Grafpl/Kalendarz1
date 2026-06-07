using Kalendarz1.Pasza.Models;
using Kalendarz1.Pasza.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Kalendarz1.Pasza
{
    public partial class KreatorPaszyWindow : Window
    {
        private readonly PaszaService _svc = new();
        private bool _zainit = false;
        private readonly DispatcherTimer _autoTimer = new() { Interval = TimeSpan.FromSeconds(15) };

        // ══════ STAN KREATORA (TAB 1) ══════
        private readonly ObservableCollection<KontrahentSymfonia> _paszarnie = new();
        private readonly ObservableCollection<TowarPasza> _towary = new();
        private readonly ObservableCollection<KontrahentSymfonia> _hodowcyWidoczni = new();
        private List<KontrahentSymfonia> _hodowcyWszyscy = new();   // pełna lista do lokalnego filtra
        public ObservableCollection<KreatorTowarPozycja> Pozycje { get; } = new();

        private KontrahentSymfonia? _paszarniaWybrana;
        private KontrahentSymfonia? _hodowcaWybrany;

        public KreatorPaszyWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            dpData.SelectedDate = DateTime.Today;
            icPaszarnie.ItemsSource = _paszarnie;
            icTowary.ItemsSource = _towary;
            icHodowcy.ItemsSource = _hodowcyWidoczni;
            icPozycje.ItemsSource = Pozycje;
            Pozycje.CollectionChanged += (_, __) => AktualizujSumy();
            _autoTimer.Tick += async (_, __) => await OdswiezKolejkeAsync();
            Loaded += async (_, __) => await ZainicjujAsync();
        }

        // ════════════════ ZAMYKANIE — confirm gdy są pozycje ════════════════
        protected override void OnClosing(CancelEventArgs e)
        {
            if (Pozycje.Count > 0)
            {
                var r = MessageBox.Show(
                    $"Masz {Pozycje.Count} pozycji niewysłanych do kolejki Symfonii.\n\nZamknąć bez wysłania? (utracisz wprowadzone dane)",
                    "Niewysłane pozycje",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }
            _autoTimer.Stop();
            base.OnClosing(e);
        }

        // ════════════════ LINK „Idź do Słowników" — z empty states ════════════════
        private void GoToSlowniki_Click(object sender, RoutedEventArgs e)
        {
            tabSlowniki.IsSelected = true;
        }

        // ════════════════ SKRÓTY ════════════════
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (btnWyslij.IsEnabled) BtnWyslij_Click(btnWyslij, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && tabKreator.IsSelected)
            {
                CzyscFormularz();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F5)
            {
                _ = OdswiezKolejkeAsync();
                _ = OdswiezCennikAsync();
                e.Handled = true;
            }
        }

        // ════════════════ INICJALIZACJA ════════════════
        private async Task ZainicjujAsync()
        {
            ShowLoading("Ładowanie słowników…");
            try
            {
                var paszarnie = await _svc.GetPaszarnieDoCBAsync();
                var towary = await _svc.GetTowaryDoCBAsync();
                _hodowcyWszyscy = await _svc.GetHodowcyKupujacychAsync(12);

                _paszarnie.Clear();
                foreach (var p in paszarnie) _paszarnie.Add(p);
                _towary.Clear();
                foreach (var t in towary) _towary.Add(t);
                FiltrjuHodowcow();

                lblNoPaszarni.Visibility = _paszarnie.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                lblNoTowarow.Visibility = _towary.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                lblNoHodowcow.Visibility = _hodowcyWszyscy.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                txtStatus.Text = (_paszarnie.Count == 0 || _towary.Count == 0)
                    ? "⚠ Słowniki paszarni/towarów puste — uzupełnij w zakładce ⚙ Słowniki."
                    : $"Załadowano: {_paszarnie.Count} paszarni · {_towary.Count} towarów · {_hodowcyWszyscy.Count} hodowców (brali w 12mc)";

                _zainit = true;
                AktualizujSumy();
                await OdswiezKolejkeAsync();
                await OdswiezCennikAsync();
                await OdswiezSlownikiAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd inicjalizacji: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        // ════════════════ PASZARNIA — radio chip ════════════════
        private async void PaszarniaChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not KontrahentSymfonia k) return;
            // Odznacz wszystkie, zaznacz wybraną
            foreach (var p in _paszarnie) p.IsSelected = p == k;
            _paszarniaWybrana = k;
            await OdswiezDedupAsync();
            AktualizujSumy();
        }

        // ════════════════ TOWAR — toggle chip + add/remove pozycji ════════════════
        private async void TowarChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not TowarPasza t) return;
            if (t.IsSelected)
            {
                // Odznacz + usuń pozycję
                t.IsSelected = false;
                var poz = Pozycje.FirstOrDefault(p => p.TowarKod == t.Kod);
                if (poz != null) Pozycje.Remove(poz);
            }
            else
            {
                // Zaznacz + dodaj pozycję
                t.IsSelected = true;
                var poz = new KreatorTowarPozycja
                {
                    TowarKod = t.Kod, TowarNazwa = t.Nazwa, Jm = t.Jm
                };
                poz.PropertyChanged += (_, e2) =>
                {
                    if (e2.PropertyName == nameof(KreatorTowarPozycja.Ilosc)
                     || e2.PropertyName == nameof(KreatorTowarPozycja.CenaZakNetto)
                     || e2.PropertyName == nameof(KreatorTowarPozycja.MarzaKwota))
                        AktualizujSumy();
                };
                Pozycje.Add(poz);
                // Autofill marża z cennika dla aktualnego hodowcy
                if (_hodowcaWybrany != null) await AutofillPozycjeMarzaAsync(poz);
                // Focus auto-jump na pole „Ilość" nowo dodanej karty — szybkie wpisywanie
                Dispatcher.BeginInvoke(new Action(() => FocusFirstInputOf(poz)), DispatcherPriority.Background);
            }
            lblPozycjeCount.Text = Pozycje.Count > 0 ? $"({Pozycje.Count} {Odmien(Pozycje.Count, "pozycja", "pozycje", "pozycji")})" : "";
            AktualizujSumy();
        }

        /// <summary>Znajduje w icPozycje kartę pozycji `p`, w niej pierwszy TextBox (Ilość), ustawia focus + SelectAll.</summary>
        private void FocusFirstInputOf(KreatorTowarPozycja p)
        {
            try
            {
                icPozycje.UpdateLayout();
                var container = icPozycje.ItemContainerGenerator.ContainerFromItem(p) as ContentPresenter;
                if (container == null) return;
                container.ApplyTemplate();
                var tb = FindVisualChild<TextBox>(container);
                if (tb != null)
                {
                    tb.Focus();
                    tb.SelectAll();
                }
            }
            catch
            {
                // Brak focusu to drobnostka — nie wywalamy UI
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void UsunPozycje_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not KreatorTowarPozycja p) return;
            Pozycje.Remove(p);
            // Odznacz towar
            var t = _towary.FirstOrDefault(x => x.Kod == p.TowarKod);
            if (t != null) t.IsSelected = false;
            lblPozycjeCount.Text = Pozycje.Count > 0 ? $"({Pozycje.Count} {Odmien(Pozycje.Count, "pozycja", "pozycje", "pozycji")})" : "";
            AktualizujSumy();
        }

        // Bez ciała — dane już zaktualizowane przez binding IloscStr/CenaZakStr/MarzaStr; AktualizujSumy w setterze decimala (PropertyChanged).
        private void PozycjaInput_Changed(object sender, TextChangedEventArgs e) { /* propagate przez model */ }

        // ════════════════ HODOWCA — radio chip + filter ════════════════
        private async void HodowcaChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not KontrahentSymfonia k) return;
            foreach (var h in _hodowcyWszyscy) h.IsSelected = h == k;
            _hodowcaWybrany = k;
            // Refresh marżę dla pozycji które jeszcze są "z cennika" (lub nigdy nie edytowane)
            foreach (var poz in Pozycje)
                if (poz.MarzaZCennika || poz.MarzaKwota == 0)
                    await AutofillPozycjeMarzaAsync(poz);
            AktualizujSumy();
        }

        private void SzukajHodowcy_TextChanged(object sender, TextChangedEventArgs e) => FiltrjuHodowcow();

        private void FiltrjuHodowcow()
        {
            string f = (txtSzukajHodowcy?.Text ?? "").Trim().ToLowerInvariant();
            List<KontrahentSymfonia> lista = string.IsNullOrEmpty(f)
                ? _hodowcyWszyscy.ToList()
                : _hodowcyWszyscy.Where(h =>
                    h.Name.ToLowerInvariant().Contains(f) ||
                    h.Shortcut.ToLowerInvariant().Contains(f) ||
                    (h.NIP ?? "").Replace("-", "").Replace(" ", "").Contains(f)
                ).ToList();

            // Jeśli zaznaczony hodowca wypadł z wyników filtra — pokaż go na samej górze,
            // żeby wizualnie nie zniknął (selekcja bez znikania pomaga przy zmianie zdania).
            if (_hodowcaWybrany != null && !lista.Contains(_hodowcaWybrany))
                lista.Insert(0, _hodowcaWybrany);

            _hodowcyWidoczni.Clear();
            foreach (var h in lista) _hodowcyWidoczni.Add(h);
        }

        // ════════════════ META INPUTÓW ════════════════
        private async void Meta_Changed(object sender, EventArgs e)
        {
            if (!_zainit) return;
            AktualizujTerminCalc();
            AktualizujSumy();
            if (sender == dpData) await OdswiezDedupAsync();
        }

        private void AktualizujTerminCalc()
        {
            var data = dpData.SelectedDate ?? DateTime.Today;
            if (int.TryParse(txtTermin.Text, out int dni) && dni > 0)
                lblTerminCalc.Text = $"Termin: {data:dd.MM.yyyy} + {dni} dni → {data.AddDays(dni):dd.MM.yyyy}";
            else
                lblTerminCalc.Text = $"Termin: {data:dd.MM.yyyy}";
        }

        // ════════════════ DEDUP ════════════════
        private async Task OdswiezDedupAsync()
        {
            banDedup.Visibility = Visibility.Collapsed;
            if (_paszarniaWybrana == null) return;
            var data = dpData.SelectedDate ?? DateTime.Today;
            var d = await _svc.SprawdzDuplikatFvAsync(_paszarniaWybrana.Shortcut, data);
            if (d.MaDuplikat)
            {
                lblDedup.Text = $"Uwaga: w Symfonii istnieje już FVZ od '{_paszarniaWybrana.Name}' z dnia {data:dd.MM.yyyy}: {d.NrIstniejacejFv}. Możliwy duplikat — sprawdź zanim wyślesz.";
                banDedup.Visibility = Visibility.Visible;
            }
        }

        // ════════════════ AUTOFILL MARŻY Z CENNIKA ════════════════
        private async Task AutofillPozycjeMarzaAsync(KreatorTowarPozycja poz)
        {
            if (_hodowcaWybrany == null) return;
            var data = dpData.SelectedDate ?? DateTime.Today;
            decimal? m = await _svc.GetMarzaZCennikaAsync(_hodowcaWybrany.Shortcut, poz.TowarKod, data);
            if (m.HasValue) poz.UstawMarzeZCennika(m.Value);
        }

        // ════════════════ SUMY + WALIDACJA ════════════════
        private void AktualizujSumy()
        {
            decimal sumaKg = Pozycje.Sum(p => p.Ilosc);
            decimal sumaWartZak = Pozycje.Sum(p => p.WartoscZakNetto);
            decimal vatFactor = 1 + ParseDec(txtVat.Text) / 100m;
            decimal sumaWartSprzBrutto = Pozycje.Sum(p => p.WartoscSprzNetto * vatFactor);
            decimal sumaMarza = Pozycje.Sum(p => p.MarzaLaczna);

            lblSumaKg.Text = $"{sumaKg:N3} t";
            lblSumaWartZakup.Text = $"{sumaWartZak:N2} zł";
            lblSumaWartSprzedaz.Text = $"{sumaWartSprzBrutto:N2} zł";
            lblKreSumaMarza.Text = $"{sumaMarza:N2} zł";

            AktualizujTerminCalc();

            string? brak = SprawdzGotowosc();
            btnWyslij.IsEnabled = brak == null;
            lblWalidacja.Text = brak ?? "";
            btnWyslij.Content = brak == null
                ? $"🚀  Wyślij {Pozycje.Count} {Odmien(Pozycje.Count, "pozycję", "pozycje", "pozycji")} do kolejki  (Ctrl+Enter)"
                : "🚀  Wyślij N pozycji do kolejki  (Ctrl+Enter)";
        }

        private string? SprawdzGotowosc()
        {
            if (_paszarniaWybrana == null) return "✗ Wybierz paszarnię";
            if (_hodowcaWybrany == null)  return "✗ Wybierz hodowcę";
            if (Pozycje.Count == 0)       return "✗ Zaznacz przynajmniej jeden towar";
            foreach (var p in Pozycje)
            {
                if (p.Ilosc <= 0) return $"✗ Pozycja '{p.TowarNazwa}': brak ilości";
                if (p.CenaZakNetto <= 0) return $"✗ Pozycja '{p.TowarNazwa}': brak ceny zakupu";
            }
            if (ParseDec(txtVat.Text) < 0) return "✗ VAT nie może być ujemny";
            return null;
        }

        // ════════════════ WYŚLIJ ════════════════
        private async void BtnWyslij_Click(object sender, RoutedEventArgs e)
        {
            if (_paszarniaWybrana == null || _hodowcaWybrany == null || Pozycje.Count == 0) return;
            decimal vat = ParseDec(txtVat.Text);
            if (!int.TryParse(txtTermin.Text, out int dni) || dni <= 0) dni = 45;
            var data = dpData.SelectedDate ?? DateTime.Today;
            string nr = txtNumerObcy.Text?.Trim() ?? "";

            ShowLoading($"Wysyłam {Pozycje.Count} pozycji do kolejki…");
            try
            {
                string user = App.UserID ?? Environment.UserName;
                int ok = 0;
                foreach (var p in Pozycje)
                {
                    var item = new KolejkaItem
                    {
                        PaszarniaKhKod = _paszarniaWybrana.Shortcut,
                        PaszarniaNazwa = _paszarniaWybrana.Name,
                        HodowcaKhKod = _hodowcaWybrany.Shortcut,
                        HodowcaNazwa = _hodowcaWybrany.Name,
                        TowarKod = p.TowarKod,
                        TowarNazwa = p.TowarNazwa,
                        TowarJm = p.Jm,
                        Ilosc = p.Ilosc,
                        CenaZakNetto = p.CenaZakNetto,
                        MarzaKwota = p.MarzaKwota,
                        VatProc = vat,
                        NumerObcy = nr,
                        DataWystawienia = data,
                        TerminDni = dni
                    };
                    await _svc.DodajDoKolejkiAsync(item, user);
                    ok++;
                }
                txtStatus.Text = $"✓ Dodano {ok} {Odmien(ok, "pozycję", "pozycje", "pozycji")} do kolejki Symfonii. Status: NOWY.";
                await OdswiezKolejkeAsync();
                CzyscFormularz();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        private void CzyscFormularz()
        {
            foreach (var p in _paszarnie) p.IsSelected = false;
            foreach (var t in _towary) t.IsSelected = false;
            foreach (var h in _hodowcyWszyscy) h.IsSelected = false;
            _paszarniaWybrana = null;
            _hodowcaWybrany = null;
            Pozycje.Clear();
            txtNumerObcy.Text = "";
            txtSzukajHodowcy.Text = "";
            dpData.SelectedDate = DateTime.Today;
            txtTermin.Text = "45";
            txtVat.Text = "8";
            banDedup.Visibility = Visibility.Collapsed;
            lblPozycjeCount.Text = "";
            AktualizujSumy();
        }

        // ════════════════ KOLEJKA (TAB 2) ════════════════

        private async Task OdswiezKolejkeAsync()
        {
            string? filter = (cbStatusFilter?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (string.IsNullOrEmpty(filter)) filter = null;
            try
            {
                var lista = await _svc.GetKolejkaAsync(filter);
                dgKolejka.ItemsSource = lista;
                lblKolejkaCount.Text = $"{lista.Count} pozycji";
                AktualizujSumyKolejki(lista);
                if (lblOstatnioOdswiezone != null)
                    lblOstatnioOdswiezone.Text = $"ostatnio: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Błąd ładowania kolejki: " + ex.Message;
            }
        }

        private void AktualizujSumyKolejki(List<KolejkaItem> lista)
        {
            lblCntNowy.Text = lista.Count(x => x.Status == "NOWY").ToString();
            lblCntImp.Text  = lista.Count(x => x.Status == "IMPORTOWANE").ToString();
            lblCntBlad.Text = lista.Count(x => x.Status == "BLAD").ToString();
            lblCntAnul.Text = lista.Count(x => x.Status == "ANULOWANE").ToString();
            var aktywne = lista.Where(x => x.Status == "NOWY" || x.Status == "IMPORTOWANE").ToList();
            lblSumaBrutto.Text = $"{aktywne.Sum(x => x.WartoscSprzBrutto):N2} zł";
            lblSumaMarza.Text  = $"{aktywne.Sum(x => x.MarzaLaczna):N2} zł";
        }

        private void ChkAutoRefresh_Changed(object sender, RoutedEventArgs e)
        {
            if (chkAutoRefresh.IsChecked == true)
            {
                _autoTimer.Start();
                txtStatus.Text = "⚡ Auto-odświeżanie Kolejki włączone (co 15 s)";
            }
            else
            {
                _autoTimer.Stop();
                txtStatus.Text = "Auto-odświeżanie Kolejki wyłączone";
            }
        }

        private async void BtnOdswiezKolejka_Click(object sender, RoutedEventArgs e) => await OdswiezKolejkeAsync();
        private async void CbStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_zainit) return;
            await OdswiezKolejkeAsync();
        }

        private async void BtnAnulujKolejka_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not KolejkaItem k) return;
            if (k.Status != "NOWY")
            {
                MessageBox.Show("Można anulować tylko pozycje ze statusem NOWY.", "Anulowanie", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show($"Anulować pozycję #{k.Id}?", "Potwierdź", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await _svc.AnulujKolejkaAsync(k.Id, App.UserID ?? Environment.UserName);
            await OdswiezKolejkeAsync();
        }

        private async void BtnPonowKolejka_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not KolejkaItem k) return;
            if (k.Status != "BLAD")
            {
                MessageBox.Show("Można ponowić tylko pozycje ze statusem BŁĄD.", "Ponowienie", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            await _svc.WyslijPonownieAsync(k.Id);
            await OdswiezKolejkeAsync();
        }

        // ════════════════ CENNIK (TAB 3) ════════════════

        private async Task OdswiezCennikAsync()
        {
            try
            {
                bool tylkoAkt = chkCennikTylkoAktywne?.IsChecked == true;
                var lista = await _svc.GetCennikAsync(tylkoAkt);
                dgCennik.ItemsSource = lista;
                lblCennikCount.Text = $"{lista.Count} wpisów";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Błąd ładowania cennika: " + ex.Message;
            }
        }

        private async void BtnOdswiezCennik_Click(object sender, RoutedEventArgs e) => await OdswiezCennikAsync();
        private async void CennikFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (!_zainit) return;
            await OdswiezCennikAsync();
        }

        private async void BtnDodajCennik_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CennikEditDialog(_svc, new CennikItem { DataOd = DateTime.Today, Aktywny = true }) { Owner = this };
            if (dlg.ShowDialog() == true) await OdswiezCennikAsync();
        }

        private async void DgCennik_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgCennik.SelectedItem is not CennikItem c) return;
            var dlg = new CennikEditDialog(_svc, c) { Owner = this };
            if (dlg.ShowDialog() == true) await OdswiezCennikAsync();
        }

        private async void BtnEdytujCennik_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not CennikItem c) return;
            var dlg = new CennikEditDialog(_svc, c) { Owner = this };
            if (dlg.ShowDialog() == true) await OdswiezCennikAsync();
        }

        private async void BtnUsunCennik_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not CennikItem c) return;
            if (MessageBox.Show($"Usunąć wpis cennika: {c.HodowcaNazwa} × {c.TowarNazwa} ({c.MarzaKwota:N2} zł)?",
                "Potwierdź", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await _svc.UsunCennikAsync(c.Id);
            await OdswiezCennikAsync();
        }

        // ════════════════ SŁOWNIKI (TAB 4) ════════════════

        private async Task OdswiezSlownikiAsync()
        {
            try
            {
                var paszarnie = await _svc.GetPaszarnieSlownikAsync(tylkoAktywne: false);
                var towary = await _svc.GetTowarySlownikAsync(tylkoAktywne: false);
                dgPaszarnie.ItemsSource = paszarnie;
                dgTowary.ItemsSource = towary;
                lblPaszarnieCount.Text = $"({paszarnie.Count})";
                lblTowaryCount.Text = $"({towary.Count})";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Błąd ładowania słowników: " + ex.Message;
            }
        }

        private async void BtnDodajPaszarnie_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new DodajPaszarnieDialog(_svc) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                txtStatus.Text = $"✓ Dodano {dlg.IloscDodanych} paszarni do słownika.";
                await OdswiezSlownikiAsync();
                // Odśwież chipy w Kreatorze
                _paszarnie.Clear();
                foreach (var p in await _svc.GetPaszarnieDoCBAsync()) _paszarnie.Add(p);
                lblNoPaszarni.Visibility = _paszarnie.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private async void BtnUsunPaszarnie_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not PaszarniaSlownik p) return;
            if (MessageBox.Show($"Usunąć '{p.Nazwa}' ze słownika paszarni?",
                "Potwierdź", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await _svc.UsunPaszarnieZeSlownikaAsync(p.Id);
            await OdswiezSlownikiAsync();
            _paszarnie.Clear();
            foreach (var x in await _svc.GetPaszarnieDoCBAsync()) _paszarnie.Add(x);
            lblNoPaszarni.Visibility = _paszarnie.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            txtStatus.Text = $"🗑 Usunięto '{p.Nazwa}' ze słownika.";
        }

        private async void BtnDodajTowar_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new DodajTowarDialog(_svc) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                txtStatus.Text = $"✓ Dodano {dlg.IloscDodanych} towarów do słownika.";
                await OdswiezSlownikiAsync();
                _towary.Clear();
                foreach (var t in await _svc.GetTowaryDoCBAsync()) _towary.Add(t);
                lblNoTowarow.Visibility = _towary.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                // Odśwież listę hodowców (nowy towar może otworzyć nową grupę)
                _hodowcyWszyscy = await _svc.GetHodowcyKupujacychAsync(12);
                FiltrjuHodowcow();
                lblNoHodowcow.Visibility = _hodowcyWszyscy.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private async void BtnUsunTowar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not TowarSlownik t) return;
            if (MessageBox.Show($"Usunąć '{t.TowarNazwa}' ze słownika towarów?",
                "Potwierdź", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await _svc.UsunTowarZeSlownikaAsync(t.Id);
            await OdswiezSlownikiAsync();
            _towary.Clear();
            foreach (var x in await _svc.GetTowaryDoCBAsync()) _towary.Add(x);
            lblNoTowarow.Visibility = _towary.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            txtStatus.Text = $"🗑 Usunięto '{t.TowarNazwa}' ze słownika.";
        }

        // ════════════════ HELPERY ════════════════
        private static decimal ParseDec(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0m;
            s = s!.Replace(',', '.').Trim();
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }

        private static string Odmien(int n, string mian, string mn2, string mn5)
        {
            int last = n % 10;
            int last2 = n % 100;
            if (n == 1) return mian;
            if (last >= 2 && last <= 4 && (last2 < 12 || last2 > 14)) return mn2;
            return mn5;
        }

        private void ShowLoading(string msg)
        {
            loadingText.Text = msg;
            loadingOverlay.Visibility = Visibility.Visible;
        }
        private void HideLoading() => loadingOverlay.Visibility = Visibility.Collapsed;
    }
}
