// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/EdytorKursuWpfWindow.xaml.cs
// ════════════════════════════════════════════════════════════════════════════
// Edytor kursu — sandbox WPF (tworzenie + modyfikacja). Pełna funkcjonalność jak
// w oryginale WinForms + lepszy UX: drag&drop (wolne→ładunki, reorder), menu
// kontekstowe, edycja uwag, sortowanie, info bar (utworzył/zmienił), pasek
// pakowania. Reuse: TransportRepozytorium (przez TransportWpfService). Zapis
// gwarantuje spójność TransportStatus ↔ Ladunek (SyncStatusyKursuAsync + auto-healing).
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Transport;
using Kalendarz1.Transport.Services;
using Kalendarz1.Transport.WPF.Dialogs;
using Kalendarz1.Transport.WPF.Models;
using Kalendarz1.Transport.WPF.Services;
using Kalendarz1.MapaFloty;

namespace Kalendarz1.Transport.WPF
{
    public partial class EdytorKursuWpfWindow : Window
    {
        private readonly TransportWpfService _svc;
        private readonly string _user;
        private long? _kursId;            // null = nowy
        private Kurs? _kurs;
        private readonly List<WolneZamowienieWpf>? _preselect;   // przy "nowy kurs z odbiorcą"

        private readonly ObservableCollection<LadunekWierszWpf> _ladunki = new();
        private readonly ObservableCollection<WolneZamowienieWpf> _wolne = new();
        private List<WolneZamowienieWpf> _wolneAll = new();
        private readonly HashSet<long> _ladunkiDoUsuniecia = new();
        private readonly ObservableCollection<ZmianaCard> _zmiany = new();

        private List<Kierowca> _kierowcy = new();
        private List<Pojazd> _pojazdy = new();
        private bool _ladowanie;

        // drag&drop
        private Point _dragStart;
        private const string FmtWolne = "ZPSP_wolne";
        private const string FmtLadunek = "ZPSP_ladunek";
        private DataGridRow? _hoverRow;
        private DragGhostAdorner? _ghost;
        private System.Windows.Documents.AdornerLayer? _ghostLayer;

        public EdytorKursuWpfWindow(TransportWpfService svc, string user, DateTime data, long? kursId = null,
            IEnumerable<WolneZamowienieWpf>? preselect = null)
        {
            InitializeComponent();
            _svc = svc;
            _user = user ?? "system";
            _kursId = kursId;
            _preselect = preselect?.ToList();

            LadunkiGrid.ItemsSource = _ladunki;
            WolneGrid.ItemsSource = _wolne;
            ListaZmian.ItemsSource = _zmiany;
            // grupowanie kart po kliencie — nagłówek sekcji, klient nie powtarza się w wierszach
            var widokZmian = System.Windows.Data.CollectionViewSource.GetDefaultView(_zmiany);
            widokZmian.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(ZmianaCard.KlientNazwa)));
            WpfDragHelper.GrupujKolekcje(_wolne, nameof(WolneZamowienieWpf.DzienOdbioru),
                nameof(WolneZamowienieWpf.DzienOdbioru), nameof(WolneZamowienieWpf.DataPrzyjazdu));
            DataKursu.SelectedDate = data.Date;

            // drag&drop
            WolneGrid.PreviewMouseLeftButtonDown += (_, e) => _dragStart = e.GetPosition(null);
            WolneGrid.PreviewMouseMove += WolneGrid_PreviewMouseMove;
            LadunkiGrid.PreviewMouseLeftButtonDown += (_, e) => _dragStart = e.GetPosition(null);
            LadunkiGrid.PreviewMouseMove += LadunkiGrid_PreviewMouseMove;
            LadunkiGrid.DragOver += LadunkiGrid_DragOver;
            LadunkiGrid.Drop += LadunkiGrid_Drop;
            LadunkiGrid.DragLeave += (_, _) => ResetHover();

            Loaded += async (_, _) => await LoadAsync();
        }

        // ════════════════════════════════════════════════════════════════════
        // LOAD
        // ════════════════════════════════════════════════════════════════════
        private async Task LoadAsync()
        {
            _ladowanie = true;
            try
            {
                _kierowcy = await _svc.Repo.PobierzKierowcowAsync(true);
                _pojazdy = await _svc.Repo.PobierzPojazdyAsync(true);
                CmbKierowca.ItemsSource = _kierowcy;
                CmbPojazd.ItemsSource = _pojazdy;

                if (_kursId.HasValue)
                {
                    TytulText.Text = $"📝 Edycja kursu #{_kursId.Value}";
                    _kurs = await _svc.Repo.PobierzKursAsync(_kursId.Value);
                    if (_kurs != null)
                    {
                        DataKursu.SelectedDate = _kurs.DataKursu.Date;
                        if (_kurs.KierowcaID.HasValue)
                            CmbKierowca.SelectedItem = _kierowcy.FirstOrDefault(k => k.KierowcaID == _kurs.KierowcaID.Value);
                        if (_kurs.PojazdID.HasValue)
                            CmbPojazd.SelectedItem = _pojazdy.FirstOrDefault(p => p.PojazdID == _kurs.PojazdID.Value);
                        TxtWyjazd.Text = _kurs.GodzWyjazdu?.ToString(@"hh\:mm") ?? "";
                        TxtPowrot.Text = _kurs.GodzPowrotu?.ToString(@"hh\:mm") ?? "";
                        TxtTrasa.Text = _kurs.Trasa ?? "";
                        UpdateInfoBar();
                    }
                    await LoadLadunkiAsync();
                }
                else
                {
                    TytulText.Text = "🚚 Nowy kurs";
                    InfoText.Text = "";
                }

                await OdswiezWolneAsync();
                // preselect z głównego okna (np. dwuklik na wolne → nowy kurs z tym odbiorcą)
                if (_preselect != null && !_kursId.HasValue)
                {
                    foreach (var z in _preselect) DodajWolne(z);
                }
                PrzeliczPakowanie();
                await OdswiezZmianyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { _ladowanie = false; }
        }

        // ════════════════════════════════════════════════════════════════════
        // ZMIANY ZAMÓWIEŃ (TransportZmiany) — pasek alertu + delta-cards
        // ════════════════════════════════════════════════════════════════════
        private async Task OdswiezZmianyAsync()
        {
            _zmiany.Clear();
            if (!_kursId.HasValue) { AlertZmianyBar.Visibility = Visibility.Collapsed; PanelZmiany.Visibility = Visibility.Collapsed; return; }
            try
            {
                var raw = await _svc.PobierzZmianyDlaKursuAsync(_kursId.Value);
                foreach (var c in ZmianaCard.ScalListe(raw)) _zmiany.Add(c);

                if (_zmiany.Count == 0)
                {
                    AlertZmianyBar.Visibility = Visibility.Collapsed;
                    PanelZmiany.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AlertText.Text = $"{_zmiany.Count} {(_zmiany.Count == 1 ? "zmiana zamówień czeka" : "zmian zamówień czeka")} na akceptację";
                    BtnAkceptujWszystkieText.Text = $"Akceptuj wszystkie ({_zmiany.Count})";
                    AlertZmianyBar.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd ładowania zmian: {ex.Message}";
            }
        }

        private void BtnPokazZmiany_Click(object sender, RoutedEventArgs e)
        {
            bool widoczny = PanelZmiany.Visibility == Visibility.Visible;
            PanelZmiany.Visibility = widoczny ? Visibility.Collapsed : Visibility.Visible;
            BtnPokazZmiany.Content = widoczny ? "Pokaż ▼" : "Ukryj ▲";
        }

        private async void BtnAkceptujJedno_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not ZmianaCard card) return;
            b.IsEnabled = false;
            try
            {
                await _svc.AkceptujGrupeIPrzeliczAsync(card.Ids, _kursId, card.ZamowienieId, card.Source.TypZmiany, _user);
                _zmiany.Remove(card);

                if (card.Source.TypZmiany == "ZmianaPojemnikow")
                {
                    await LoadLadunkiAsync();
                    PrzeliczPakowanie();
                }
                AktualizujLicznikZmian();
                StatusText.Text = card.IloscScalonych > 1
                    ? $"✓ Zaakceptowano {card.IloscScalonych} kolejnych zmian: {card.KlientNazwa} · {card.TypLabel}"
                    : $"✓ Zaakceptowano: {card.KlientNazwa} · {card.TypLabel}";
            }
            catch (Exception ex) { MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error); b.IsEnabled = true; }
        }

        private async void BtnOdrzucJedno_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not ZmianaCard card) return;
            var iloscTxt = card.IloscScalonych > 1 ? $" ({card.IloscScalonych} kolejnych edycji)" : "";
            var kontekst = $"{card.TypLabel} · {card.KlientNazwa}{iloscTxt}   {card.Stare} → {card.Nowa}";
            var dlg = new OdrzucPowodDialog(kontekst) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            b.IsEnabled = false;
            try
            {
                await _svc.OdrzucGrupeAsync(card.Ids, _user, dlg.Powod);
                _zmiany.Remove(card);
                AktualizujLicznikZmian();
                StatusText.Text = $"✗ Odrzucono: {card.KlientNazwa} · {card.TypLabel}";
            }
            catch (Exception ex) { MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error); b.IsEnabled = true; }
        }

        private async void BtnAkceptujWszystkie_Click(object sender, RoutedEventArgs e)
        {
            if (!_kursId.HasValue || _zmiany.Count == 0) return;
            if (MessageBox.Show($"Zaakceptować wszystkie {_zmiany.Count} zmian dla tego kursu?\nPojemniki ładunków zostaną zsynchronizowane z aktualnymi wartościami zamówień.",
                "Potwierdź akceptację", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            BtnAkceptujWszystkie.IsEnabled = false;
            try
            {
                await _svc.AkceptujWszystkieDlaKursuAsync(_kursId.Value, _user);
                _zmiany.Clear();
                AlertZmianyBar.Visibility = Visibility.Collapsed;
                PanelZmiany.Visibility = Visibility.Collapsed;
                await LoadLadunkiAsync();      // sync PojemnikiE2 mógł zmienić wiersze
                PrzeliczPakowanie();
                StatusText.Text = "Zaakceptowano wszystkie zmiany dla kursu.";
            }
            catch (Exception ex) { MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { BtnAkceptujWszystkie.IsEnabled = true; }
        }

        private void AktualizujLicznikZmian()
        {
            if (_zmiany.Count == 0)
            {
                AlertZmianyBar.Visibility = Visibility.Collapsed;
                PanelZmiany.Visibility = Visibility.Collapsed;
            }
            else
            {
                AlertText.Text = $"{_zmiany.Count} {(_zmiany.Count == 1 ? "zmiana zamówień czeka" : "zmian zamówień czeka")} na akceptację";
                BtnAkceptujWszystkieText.Text = $"Akceptuj wszystkie ({_zmiany.Count})";
            }
        }

        private void UpdateInfoBar()
        {
            if (_kurs == null) { InfoText.Text = ""; return; }
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(_kurs.Utworzyl))
                parts.Add($"Utworzył: {_kurs.Utworzyl} · {_kurs.UtworzonoUTC.ToLocalTime():dd.MM HH:mm}");
            if (!string.IsNullOrEmpty(_kurs.Zmienil) && _kurs.ZmienionoUTC.HasValue)
                parts.Add($"Zmienił: {_kurs.Zmienil} · {_kurs.ZmienionoUTC.Value.ToLocalTime():dd.MM HH:mm}");
            InfoText.Text = string.Join("      ", parts);
        }

        private async Task LoadLadunkiAsync()
        {
            _ladunki.Clear();
            var dbLad = await _svc.Repo.PobierzLadunkiAsync(_kursId!.Value);

            var rows = dbLad.Select(l => new LadunekWierszWpf
            {
                LadunekID = l.LadunekID,
                KursID = l.KursID,
                Kolejnosc = l.Kolejnosc,
                KodKlienta = l.KodKlienta,
                PojemnikiE2 = l.PojemnikiE2,
                Uwagi = l.Uwagi,
                TrybE2 = l.TrybE2,
                PlanE2NaPaleteOverride = l.PlanE2NaPaleteOverride
            }).ToList();

            var zamIds = rows.Where(r => r.ZamowienieId.HasValue).Select(r => r.ZamowienieId!.Value).ToList();
            if (zamIds.Count > 0)
            {
                var nazwy = await _svc.ResolveNazwyAsync(zamIds);
                foreach (var r in rows)
                {
                    if (r.ZamowienieId.HasValue && nazwy.TryGetValue(r.ZamowienieId.Value, out var info))
                    {
                        r.NazwaKlienta = info.Nazwa;
                        r.Awizacja = info.Awizacja;
                        r.Handlowiec = info.Handlowiec;
                    }
                    else r.NazwaKlienta = r.KodKlienta ?? "—";
                }
            }
            else foreach (var r in rows) r.NazwaKlienta = r.KodKlienta ?? "—";

            foreach (var r in rows.OrderBy(r => r.Kolejnosc)) _ladunki.Add(r);
        }

        private async Task OdswiezWolneAsync()
        {
            try
            {
                var data = DataKursu.SelectedDate ?? DateTime.Today;
                bool poUboju = RbUboj.IsChecked == true;
                _wolneAll = await _svc.LoadWolneZamowieniaAsync(data, poUboju);

                var wKursie = _ladunki.Where(l => l.ZamowienieId.HasValue)
                                      .Select(l => l.ZamowienieId!.Value).ToHashSet();
                _wolneAll = _wolneAll.Where(z => !wKursie.Contains(z.ZamowienieId)).ToList();
                await OdswiezPodpowiedziParAsync();
                FiltrujWolne();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd wolnych zamówień: {ex.Message}";
            }
        }

        /// <summary>Wyznacza zbiór KlientId, którzy w ostatnich 90 dniach jeździli razem z klientami aktualnego kursu.
        /// Każdemu wolnemu zamówieniu należącemu do takiego klienta ustawia CzestaPara=true.</summary>
        private async Task OdswiezPodpowiedziParAsync()
        {
            try
            {
                var zamIds = _ladunki.Where(l => l.ZamowienieId.HasValue)
                                     .Select(l => l.ZamowienieId!.Value).Distinct().ToList();
                if (zamIds.Count == 0)
                {
                    foreach (var w in _wolneAll) w.CzestaPara = false;
                    return;
                }
                var nazwy = await _svc.ResolveNazwyAsync(zamIds);
                var aktualne = nazwy.Values.Select(z => z.KlientId).Where(i => i > 0).Distinct().ToList();
                if (aktualne.Count == 0) { foreach (var w in _wolneAll) w.CzestaPara = false; return; }
                var partnerzy = await _svc.PobierzKlientowParaAsync(aktualne);
                foreach (var w in _wolneAll) w.CzestaPara = partnerzy.Contains(w.KlientId);
            }
            catch { /* podpowiedzi to nice-to-have, nie blokuj UI */ }
        }

        private void FiltrujWolne()
        {
            var q = TxtSzukaj.Text?.Trim().ToLowerInvariant() ?? "";
            _wolne.Clear();
            IEnumerable<WolneZamowienieWpf> src = _wolneAll;
            if (!string.IsNullOrEmpty(q))
                src = src.Where(z => (z.KlientNazwa ?? "").ToLowerInvariant().Contains(q)
                                  || (z.Handlowiec ?? "").ToLowerInvariant().Contains(q));
            foreach (var z in src.OrderBy(z => z.DataPrzyjazdu)) _wolne.Add(z);
            WolneCountText.Text = _wolne.Count.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        // PAKOWANIE
        // ════════════════════════════════════════════════════════════════════
        private void PrzeliczPakowanie()
        {
            int sumaE2 = _ladunki.Sum(l => l.PojemnikiE2);
            const int planE2 = 36;
            int paletyNominal = sumaE2 == 0 ? 0 : (int)Math.Ceiling(sumaE2 / (double)planE2);
            int kapacita = (CmbPojazd.SelectedItem as Pojazd)?.PaletyH1 ?? 33;
            double proc = kapacita > 0 ? 100.0 * paletyNominal / kapacita : 0;

            PaskoText.Text = $"{proc:F0}%";
            PaletyText.Text = $"{paletyNominal} / {kapacita} palet  ·  {sumaE2} poj.  ·  {_ladunki.Count} ład.";

            double maxW = ((FrameworkElement)PaskoFill.Parent).ActualWidth;
            if (maxW <= 0) maxW = 600;
            PaskoFill.Width = Math.Min(1.0, proc / 100.0) * maxW;
            PaskoFill.Background = new SolidColorBrush(
                proc > 100 ? Color.FromRgb(0xC6, 0x28, 0x28) :
                proc >= 75 ? Color.FromRgb(0xF5, 0x7C, 0x00) :
                             Color.FromRgb(0x43, 0xA0, 0x47));
        }

        private void CmbPojazd_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ladowanie) PrzeliczPakowanie();
        }

        private async void DataKursu_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_ladowanie) await OdswiezWolneAsync();
        }

        // ════════════════════════════════════════════════════════════════════
        // DODAWANIE / USUWANIE / KOLEJNOŚĆ
        // ════════════════════════════════════════════════════════════════════
        private void BtnDodajWolne_Click(object sender, RoutedEventArgs e)
        {
            foreach (var z in WolneGrid.SelectedItems.Cast<WolneZamowienieWpf>().ToList())
                DodajWolne(z);
        }
        private void WolneGrid_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (WolneGrid.SelectedItem is WolneZamowienieWpf z) DodajWolne(z);
        }

        private void DodajWolne(WolneZamowienieWpf z)
        {
            if (_ladunki.Any(l => l.ZamowienieId == z.ZamowienieId)) return;
            _ladunki.Add(new LadunekWierszWpf
            {
                LadunekID = 0,
                KodKlienta = z.KodKlienta,
                PojemnikiE2 = z.Pojemniki,
                TrybE2 = z.TrybE2,
                NazwaKlienta = z.KlientNazwa,
                Awizacja = z.DataPrzyjazdu,
                Handlowiec = z.Handlowiec,
                Kolejnosc = _ladunki.Count + 1
            });
            _wolneAll.RemoveAll(x => x.ZamowienieId == z.ZamowienieId);
            FiltrujWolne();
            PrzeliczPakowanie();
        }

        private void BtnUsunLadunek_Click(object sender, RoutedEventArgs e)
        {
            if (LadunkiGrid.SelectedItem is not LadunekWierszWpf lad) return;
            if (lad.LadunekID > 0) _ladunkiDoUsuniecia.Add(lad.LadunekID);
            _ladunki.Remove(lad);
            Renumeruj();

            if (lad.ZamowienieId.HasValue && !_wolneAll.Any(z => z.ZamowienieId == lad.ZamowienieId.Value))
            {
                _wolneAll.Add(new WolneZamowienieWpf
                {
                    ZamowienieId = lad.ZamowienieId.Value,
                    KlientNazwa = lad.NazwaKlienta,
                    Handlowiec = lad.Handlowiec,
                    Pojemniki = lad.PojemnikiE2,
                    TrybE2 = lad.TrybE2,
                    DataPrzyjazdu = lad.Awizacja ?? (DataKursu.SelectedDate ?? DateTime.Today)
                });
                FiltrujWolne();
            }
            PrzeliczPakowanie();
        }

        private void BtnGora_Click(object sender, RoutedEventArgs e) => Przesun(-1);
        private void BtnDol_Click(object sender, RoutedEventArgs e) => Przesun(+1);

        private void Przesun(int delta)
        {
            if (LadunkiGrid.SelectedItem is not LadunekWierszWpf lad) return;
            int idx = _ladunki.IndexOf(lad);
            int nowy = idx + delta;
            if (nowy < 0 || nowy >= _ladunki.Count) return;
            _ladunki.Move(idx, nowy);
            Renumeruj();
            LadunkiGrid.SelectedItem = lad;
        }

        private void BtnSortuj_Click(object sender, RoutedEventArgs e)
        {
            var posort = _ladunki.OrderBy(l => l.Awizacja ?? DateTime.MaxValue).ThenBy(l => l.NazwaDisplay).ToList();
            _ladunki.Clear();
            foreach (var l in posort) _ladunki.Add(l);
            Renumeruj();
        }

        private void Renumeruj()
        {
            for (int i = 0; i < _ladunki.Count; i++) _ladunki[i].Kolejnosc = i + 1;
        }

        private void LadunkiGrid_DoubleClick(object sender, MouseButtonEventArgs e) => EdytujUwagi();
        private void MiEdytujUwagi_Click(object sender, RoutedEventArgs e) => EdytujUwagi();

        private void EdytujUwagi()
        {
            if (LadunkiGrid.SelectedItem is not LadunekWierszWpf lad) return;
            var dlg = new TekstPromptDialog("Uwagi do ładunku", $"Uwagi — {lad.NazwaDisplay}:", lad.Uwagi ?? "") { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                lad.Uwagi = string.IsNullOrWhiteSpace(dlg.Wartosc) ? null : dlg.Wartosc.Trim();
                LadunkiGrid.Items.Refresh();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // DRAG & DROP
        // ════════════════════════════════════════════════════════════════════
        private void WolneGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (!WpfDragHelper.ExceededThreshold(_dragStart, e.GetPosition(null))) return;
            var items = WolneGrid.SelectedItems.Cast<WolneZamowienieWpf>().ToList();
            if (items.Count == 0) return;

            StartGhost(items.Count == 1
                ? $"📦 {items[0].KlientNazwa} — przeciągnij do kursu"
                : $"📦 {items.Count} zam. — przeciągnij do kursu");
            if (DropHintLad != null) DropHintLad.Visibility = Visibility.Visible;
            try { DragDrop.DoDragDrop(WolneGrid, new DataObject(FmtWolne, items), DragDropEffects.Copy); }
            catch { /* drag anulowany */ }
            finally
            {
                EndGhost();
                if (DropHintLad != null) DropHintLad.Visibility = Visibility.Collapsed;
                ResetHover();
            }
        }

        private void StartGhost(string text)
        {
            try
            {
                if (Content is not UIElement host) return;
                _ghostLayer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(host);
                if (_ghostLayer == null) return;
                _ghost = new DragGhostAdorner(host, text);
                _ghostLayer.Add(_ghost);
            }
            catch { }
        }

        private void EndGhost()
        {
            try { if (_ghostLayer != null && _ghost != null) _ghostLayer.Remove(_ghost); } catch { }
            _ghost = null; _ghostLayer = null;
        }

        private void LadunkiGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (!WpfDragHelper.ExceededThreshold(_dragStart, e.GetPosition(null))) return;
            if (LadunkiGrid.SelectedItem is not LadunekWierszWpf lad) return;
            try { DragDrop.DoDragDrop(LadunkiGrid, new DataObject(FmtLadunek, lad), DragDropEffects.Move); }
            catch { }
        }

        private void LadunkiGrid_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(FmtWolne)) e.Effects = DragDropEffects.Copy;
            else if (e.Data.GetDataPresent(FmtLadunek)) e.Effects = DragDropEffects.Move;
            else e.Effects = DragDropEffects.None;
            Highlight(WpfDragHelper.GetRowAtPoint(LadunkiGrid, e.GetPosition(LadunkiGrid)));
            if (_ghost != null && Content is IInputElement root) _ghost.SetPosition(e.GetPosition(root));
            e.Handled = true;
        }

        private void Highlight(DataGridRow? row)
        {
            if (ReferenceEquals(row, _hoverRow)) return;
            ResetHover();
            if (row != null)
            {
                row.Background = new SolidColorBrush(Color.FromRgb(0xC8, 0xE6, 0xC9)); // zielony cel
                _hoverRow = row;
            }
        }
        private void ResetHover()
        {
            if (_hoverRow != null) { _hoverRow.ClearValue(Control.BackgroundProperty); _hoverRow = null; }
        }

        private void LadunkiGrid_Drop(object sender, DragEventArgs e)
        {
            ResetHover();
            var target = WpfDragHelper.GetItemAtPoint(LadunkiGrid, e.GetPosition(LadunkiGrid)) as LadunekWierszWpf;

            if (e.Data.GetDataPresent(FmtWolne))
            {
                if (e.Data.GetData(FmtWolne) is List<WolneZamowienieWpf> items)
                    foreach (var z in items) DodajWolne(z);
            }
            else if (e.Data.GetDataPresent(FmtLadunek))
            {
                if (e.Data.GetData(FmtLadunek) is LadunekWierszWpf src)
                {
                    int from = _ladunki.IndexOf(src);
                    int to = target != null ? _ladunki.IndexOf(target) : _ladunki.Count - 1;
                    if (from >= 0 && to >= 0 && from != to)
                    {
                        _ladunki.Move(from, to);
                        Renumeruj();
                        LadunkiGrid.SelectedItem = src;
                    }
                }
            }
            e.Handled = true;
        }

        // ════════════════════════════════════════════════════════════════════
        // Filtry / odświeżanie / trasa
        // ════════════════════════════════════════════════════════════════════
        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e) { if (!_ladowanie) FiltrujWolne(); }
        private async void DataTyp_Changed(object sender, RoutedEventArgs e) { if (!_ladowanie) await OdswiezWolneAsync(); }
        private async void BtnOdswiezWolne_Click(object sender, RoutedEventArgs e) => await OdswiezWolneAsync();

        private async void MiWlasnyOdbior_Click(object sender, RoutedEventArgs e)
        {
            var wybrane = WolneGrid.SelectedItems.Cast<WolneZamowienieWpf>().ToList();
            if (wybrane.Count == 0) return;
            if (MessageBox.Show($"Oznaczyć {wybrane.Count} zam. jako ODBIÓR WŁASNY?\nZnikną z puli transportu.",
                "Odbiór własny", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                foreach (var z in wybrane) await _svc.WlasnyOdbiorAsync(z.ZamowienieId, _user);
                await OdswiezWolneAsync();
                StatusText.Text = $"Oznaczono {wybrane.Count} jako odbiór własny.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAutoTrasa_Click(object sender, RoutedEventArgs e)
        {
            var nazwy = _ladunki.Select(l => l.NazwaDisplay).Where(n => n != "—").Distinct().Take(3).ToList();
            if (nazwy.Count == 0) { StatusText.Text = "Brak ładunków do złożenia trasy."; return; }
            TxtTrasa.Text = string.Join(" → ", nazwy) + (_ladunki.Count > 3 ? " → …" : "");
        }

        // ════════════════════════════════════════════════════════════════════
        // SZACOWANIE GODZINY POWROTU
        // Model geometryczny (EtaService): baza → +załadunek → Σ(jazda Haversine×1.3 /
        // 60 km/h + rozładunek 30 min) → powrót do bazy. Współrzędne przystanków z
        // WebfleetOrderService.PobierzAdresySzybkoAsync (cache KlientAdres, bez Nominatim;
        // brak GPS → płaskie 30 min/przystanek). Planista może nadpisać ręcznie.
        // ════════════════════════════════════════════════════════════════════
        private async void BtnSzacujPowrot_Click(object sender, RoutedEventArgs e)
        {
            if (_ladunki.Count == 0)
            {
                SzacunekHint.Text = "";
                StatusText.Text = "Brak przystanków — dodaj ładunki, aby oszacować powrót.";
                return;
            }

            var wyjazd = ParseGodz(TxtWyjazd.Text) ?? new TimeSpan(6, 0, 0);
            BtnSzacujPowrot.IsEnabled = false;
            var poprzedniHint = SzacunekHint.Text;
            SzacunekHint.Text = "Liczę trasę…";
            try
            {
                // współrzędne przystanków (szybko, bez Nominatim; ZAM_{id} obsłużone)
                var kody = _ladunki.Select(l => l.KodKlienta ?? "").Where(k => k.Length > 0).Distinct().ToList();
                Dictionary<string, WebfleetOrderService.KlientAdresInfo> adresy;
                try { adresy = await new WebfleetOrderService().PobierzAdresySzybkoAsync(kody); }
                catch { adresy = new Dictionary<string, WebfleetOrderService.KlientAdresInfo>(); } // brak bazy adresów → szacunek płaski

                var stops = _ladunki.OrderBy(l => l.Kolejnosc).Select((l, i) =>
                {
                    adresy.TryGetValue(l.KodKlienta ?? "", out var a);
                    return new EtaService.StopInput
                    {
                        Kolejnosc = l.Kolejnosc > 0 ? l.Kolejnosc : i + 1,
                        NazwaKlienta = l.NazwaDisplay,
                        Latitude = a?.Lat ?? 0,
                        Longitude = a?.Lon ?? 0
                    };
                }).ToList();

                var wynik = new EtaService().Calculate(wyjazd, stops);
                var powrot = ZaokraglijDo5(wynik.EstimatedReturnTime);
                TxtPowrot.Text = powrot.ToString(@"hh\:mm");

                int bezGps = stops.Count(s => s.Latitude == 0 || s.Longitude == 0);
                double km = wynik.TotalDistanceKm + wynik.ReturnDistanceKm;
                var dur = wynik.TotalDuration;
                SzacunekHint.Text =
                    $"🔮 Szac. powrót ~{powrot:hh\\:mm}  ·  {stops.Count} przyst.  ·  ~{km:F0} km  ·  czas ~{(int)dur.TotalHours}h {dur.Minutes:00}min"
                    + (bezGps > 0 ? $"  ·  ⚠ {bezGps} bez GPS (przyjęto 30 min/szt.)" : "");
                StatusText.Text = "Oszacowano powrót — w razie potrzeby nadpisz ręcznie.";
            }
            catch (Exception ex)
            {
                SzacunekHint.Text = poprzedniHint;
                StatusText.Text = $"Błąd szacowania: {ex.Message}";
            }
            finally { BtnSzacujPowrot.IsEnabled = true; }
        }

        private static TimeSpan ZaokraglijDo5(TimeSpan t)
        {
            int m = (int)Math.Round(t.TotalMinutes / 5.0) * 5;
            m = Math.Max(0, Math.Min(24 * 60 - 5, m));
            return TimeSpan.FromMinutes(m);
        }

        // ════════════════════════════════════════════════════════════════════
        // Nowy kierowca / pojazd
        // ════════════════════════════════════════════════════════════════════
        private async void BtnNowyKierowca_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new NowyKierowcaWpfDialog { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Wynik != null)
            {
                try
                {
                    var id = await _svc.Repo.DodajKierowceAsync(dlg.Wynik);
                    _kierowcy = await _svc.Repo.PobierzKierowcowAsync(true);
                    CmbKierowca.ItemsSource = _kierowcy;
                    CmbKierowca.SelectedItem = _kierowcy.FirstOrDefault(k => k.KierowcaID == id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd dodawania kierowcy:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnNowyPojazd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new NowyPojazdWpfDialog { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Wynik != null)
            {
                try
                {
                    var id = await _svc.Repo.DodajPojazdAsync(dlg.Wynik);
                    _pojazdy = await _svc.Repo.PobierzPojazdyAsync(true);
                    CmbPojazd.ItemsSource = _pojazdy;
                    CmbPojazd.SelectedItem = _pojazdy.FirstOrDefault(p => p.PojazdID == id);
                    PrzeliczPakowanie();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd dodawania pojazdu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAPIS
        // ════════════════════════════════════════════════════════════════════
        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            BtnZapisz.IsEnabled = false;
            StatusText.Text = "Zapisywanie...";
            try
            {
                var kursId = await ZapiszAsync();
                StatusText.Text = $"Zapisano kurs #{kursId}.";
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Błąd zapisu.";
                BtnZapisz.IsEnabled = true;
            }
        }

        private async Task<long> ZapiszAsync()
        {
            var data = (DataKursu.SelectedDate ?? DateTime.Today).Date;
            var kurs = new Kurs
            {
                KursID = _kursId ?? 0,
                DataKursu = data,
                KierowcaID = (CmbKierowca.SelectedItem as Kierowca)?.KierowcaID,
                PojazdID = (CmbPojazd.SelectedItem as Pojazd)?.PojazdID,
                Trasa = string.IsNullOrWhiteSpace(TxtTrasa.Text) ? null : TxtTrasa.Text.Trim(),
                GodzWyjazdu = ParseGodz(TxtWyjazd.Text),
                GodzPowrotu = ParseGodz(TxtPowrot.Text),
                Status = _kurs?.Status ?? "Planowany",
                PlanE2NaPalete = 36
            };

            long kursId;
            if (_kursId.HasValue)
            {
                kursId = _kursId.Value;
                kurs.KursID = kursId;
                await _svc.Repo.AktualizujNaglowekKursuAsync(kurs, _user);
            }
            else
            {
                kursId = await _svc.Repo.DodajKursAsync(kurs, _user);
                _kursId = kursId;
            }

            foreach (var id in _ladunkiDoUsuniecia)
                await _svc.Repo.UsunLadunekAsync(id);
            _ladunkiDoUsuniecia.Clear();

            for (int i = 0; i < _ladunki.Count; i++)
            {
                var w = _ladunki[i];
                w.Kolejnosc = i + 1;
                var l = new Ladunek
                {
                    LadunekID = w.LadunekID,
                    KursID = kursId,
                    Kolejnosc = i + 1,
                    KodKlienta = w.KodKlienta,
                    PojemnikiE2 = w.PojemnikiE2,
                    Uwagi = w.Uwagi,
                    TrybE2 = w.TrybE2,
                    PlanE2NaPaleteOverride = w.PlanE2NaPaleteOverride
                };
                if (w.LadunekID == 0)
                    w.LadunekID = await _svc.Repo.DodajLadunekAsync(l);
                l.LadunekID = w.LadunekID;
                await _svc.Repo.AktualizujLadunekAsync(l);
            }

            var zamIdyWKursie = _ladunki.Where(x => x.ZamowienieId.HasValue)
                                        .Select(x => x.ZamowienieId!.Value)
                                        .ToHashSet();
            await _svc.SyncStatusyKursuAsync(kursId, zamIdyWKursie, _user);

            return kursId;
        }

        private static TimeSpan? ParseGodz(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return TimeSpan.TryParse(s.Trim(), out var ts) ? ts : (TimeSpan?)null;
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
