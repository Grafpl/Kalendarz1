// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/EdytorKursuWpfWindow.xaml.cs
// ════════════════════════════════════════════════════════════════════════════
// Edytor kursu — sandbox WPF (tworzenie + modyfikacja). NIE dotyka WinForms.
// Reuse: TransportRepozytorium (przez TransportWpfService). Zapis gwarantuje
// spójność TransportStatus ↔ Ladunek (SyncStatusyKursuAsync + auto-healing).
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kalendarz1.Transport;
using Kalendarz1.Transport.WPF.Dialogs;
using Kalendarz1.Transport.WPF.Models;
using Kalendarz1.Transport.WPF.Services;

namespace Kalendarz1.Transport.WPF
{
    public partial class EdytorKursuWpfWindow : Window
    {
        private readonly TransportWpfService _svc;
        private readonly string _user;
        private long? _kursId;            // null = nowy
        private Kurs? _kurs;

        private readonly ObservableCollection<LadunekWierszWpf> _ladunki = new();
        private readonly ObservableCollection<WolneZamowienieWpf> _wolne = new();
        private List<WolneZamowienieWpf> _wolneAll = new();
        private readonly HashSet<long> _ladunkiDoUsuniecia = new();

        private List<Kierowca> _kierowcy = new();
        private List<Pojazd> _pojazdy = new();
        private bool _ladowanie;

        public EdytorKursuWpfWindow(TransportWpfService svc, string user, DateTime data, long? kursId = null)
        {
            InitializeComponent();
            _svc = svc;
            _user = user ?? "system";
            _kursId = kursId;

            LadunkiGrid.ItemsSource = _ladunki;
            WolneGrid.ItemsSource = _wolne;
            DataKursu.SelectedDate = data.Date;

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
                    }
                    await LoadLadunkiAsync();
                }
                else
                {
                    TytulText.Text = "🚚 Nowy kurs";
                }

                await OdswiezWolneAsync();
                PrzeliczPakowanie();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { _ladowanie = false; }
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

            // rozwiąż nazwy dla ZAM_*
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

                // odfiltruj te już w kursie (po ZamowienieId)
                var wKursie = _ladunki.Where(l => l.ZamowienieId.HasValue)
                                      .Select(l => l.ZamowienieId!.Value).ToHashSet();
                _wolneAll = _wolneAll.Where(z => !wKursie.Contains(z.ZamowienieId)).ToList();

                FiltrujWolne();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd wolnych zamówień: {ex.Message}";
            }
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
            PaletyText.Text = $"{paletyNominal} / {kapacita} palet  ·  {sumaE2} poj.";

            // szerokość paska względem kontenera
            double maxW = ((FrameworkElement)PaskoFill.Parent).ActualWidth;
            if (maxW <= 0) maxW = 600;
            PaskoFill.Width = Math.Min(1.0, proc / 100.0) * maxW;

            PaskoFill.Background = new SolidColorBrush(
                proc > 100 ? Color.FromRgb(0xC6, 0x28, 0x28) :   // czerwony — przeładowany
                proc >= 75 ? Color.FromRgb(0xF5, 0x7C, 0x00) :   // pomarańcz
                             Color.FromRgb(0x43, 0xA0, 0x47));    // zielony
        }

        private void CmbPojazd_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ladowanie) PrzeliczPakowanie();
        }

        // ════════════════════════════════════════════════════════════════════
        // ŁADUNKI — dodaj / usuń / kolejność
        // ════════════════════════════════════════════════════════════════════
        private void BtnDodajWolne_Click(object sender, RoutedEventArgs e) => DodajZaznaczoneWolne();
        private void WolneGrid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => DodajZaznaczoneWolne();

        private void DodajZaznaczoneWolne()
        {
            if (WolneGrid.SelectedItem is not WolneZamowienieWpf z) return;

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

            // wróć do puli wolnych jeśli to było zamówienie
            if (lad.ZamowienieId.HasValue &&
                !_wolneAll.Any(z => z.ZamowienieId == lad.ZamowienieId.Value))
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

        private void Renumeruj()
        {
            for (int i = 0; i < _ladunki.Count; i++) _ladunki[i].Kolejnosc = i + 1;
        }

        // ════════════════════════════════════════════════════════════════════
        // Filtry / odświeżanie wolnych
        // ════════════════════════════════════════════════════════════════════
        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ladowanie) FiltrujWolne();
        }
        private async void DataTyp_Changed(object sender, RoutedEventArgs e)
        {
            if (!_ladowanie) await OdswiezWolneAsync();
        }
        private async void BtnOdswiezWolne_Click(object sender, RoutedEventArgs e) => await OdswiezWolneAsync();

        private void BtnAutoTrasa_Click(object sender, RoutedEventArgs e)
        {
            var nazwy = _ladunki.Select(l => l.NazwaDisplay).Where(n => n != "—").Distinct().Take(3).ToList();
            if (nazwy.Count == 0) { StatusText.Text = "Brak ładunków do złożenia trasy."; return; }
            TxtTrasa.Text = string.Join(" → ", nazwy) + (_ladunki.Count > 3 ? " → …" : "");
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
                    MessageBox.Show($"Błąd dodawania kierowcy:\n{ex.Message}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show($"Błąd dodawania pojazdu:\n{ex.Message}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Błąd zapisu:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Błąd zapisu.";
                BtnZapisz.IsEnabled = true;
            }
        }

        private async Task<long> ZapiszAsync()
        {
            var data = (DataKursu.SelectedDate ?? DateTime.Today).Date;
            TimeSpan? wyj = ParseGodz(TxtWyjazd.Text);
            TimeSpan? pow = ParseGodz(TxtPowrot.Text);

            var kurs = new Kurs
            {
                KursID = _kursId ?? 0,
                DataKursu = data,
                KierowcaID = (CmbKierowca.SelectedItem as Kierowca)?.KierowcaID,
                PojazdID = (CmbPojazd.SelectedItem as Pojazd)?.PojazdID,
                Trasa = string.IsNullOrWhiteSpace(TxtTrasa.Text) ? null : TxtTrasa.Text.Trim(),
                GodzWyjazdu = wyj,
                GodzPowrotu = pow,
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

            // usunięte ładunki
            foreach (var id in _ladunkiDoUsuniecia)
                await _svc.Repo.UsunLadunekAsync(id);
            _ladunkiDoUsuniecia.Clear();

            // upsert w kolejności kolekcji (Kolejnosc = i+1)
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
                await _svc.Repo.AktualizujLadunekAsync(l);   // gwarantuje poprawną Kolejnosc
            }

            // spójne statusy + auto-healing sierot
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
