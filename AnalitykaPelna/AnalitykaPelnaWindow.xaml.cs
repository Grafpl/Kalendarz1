using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Kalendarz1.AnalitykaPelna.Controls;
using Kalendarz1.AnalitykaPelna.Models;
using Kalendarz1.AnalitykaPelna.Services;

namespace Kalendarz1.AnalitykaPelna
{
    public partial class AnalitykaPelnaWindow : Window
    {
        private DispatcherTimer? _liveTimer;
        private bool _liveAktywne;
        private FiltryAnaliz _ostatnieFiltry = new();
        private CancellationTokenSource? _cts;
        private bool _ladowanie;

        public AnalitykaPelnaWindow()
        {
            AnalitykaConfig.ZaladujJesliTrzeba();
            AnalitykaSettings.Zaladuj();
            InitializeComponent();

            WindowIconHelper.SetIcon(this);
            KeyDown += OknoKeyDown;
            txtCzasOdswiezenia.Text = $"Otwarte: {DateTime.Now:HH:mm:ss}";

            filtryPasek.FiltryZastosowane += FiltryPasek_FiltryZastosowane;
            filtryPasek.LiveKlik += (s, ev) => BtnLive_Click(this, new RoutedEventArgs());
            filtryPasek.EksportKlik += (s, ev) => BtnEksport_Click(this, new RoutedEventArgs());
            filtryPasek.ZamknijKlik += (s, ev) => BtnZamknij_Click(this, new RoutedEventArgs());
            Loaded += AnalitykaPelnaWindow_Loaded;
            Closed += AnalitykaPelnaWindow_Closed;
        }

        private async void AnalitykaPelnaWindow_Loaded(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = "Ładowanie list filtrów…";
            try
            {
                await filtryPasek.ZaladujKomboboxyAsync();

                // Przywróć ostatnią zakładkę
                if (AnalitykaSettings.OstatniaZakladka >= 0 && AnalitykaSettings.OstatniaZakladka < tabGlowny.Items.Count)
                    tabGlowny.SelectedIndex = AnalitykaSettings.OstatniaZakladka;

                // Przywróć daty (jeśli są zapisane i sensowne)
                filtryPasek.PrzywrocOstatnieDaty();

                UstawTrybZakladki();
                txtStatus.Text = "Gotowe — wybierz zakres i kliknij Zastosuj";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Błąd inicjalizacji: " + ex.Message;
            }
        }

        private void AnalitykaPelnaWindow_Closed(object? sender, EventArgs e)
        {
            _cts?.Cancel();
            _liveTimer?.Stop();

            AnalitykaSettings.OstatniaZakladka = tabGlowny.SelectedIndex;
            AnalitykaSettings.OstatniaDataOd = _ostatnieFiltry.DataOd;
            AnalitykaSettings.OstatniaDataDo = _ostatnieFiltry.DataDo;
            AnalitykaSettings.OstatniLiczbaTygodniPrognozy = _ostatnieFiltry.LiczbaTygodniPrognozy;
            AnalitykaSettings.LiveAktywneNaStarcie = _liveAktywne;
            AnalitykaSettings.Zapisz();
        }

        private void OknoKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.L: BtnLive_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                    case Key.E: BtnEksport_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                    case Key.D1: tabGlowny.SelectedIndex = 0; e.Handled = true; break;
                    case Key.D2: tabGlowny.SelectedIndex = 1; e.Handled = true; break;
                    case Key.D3: tabGlowny.SelectedIndex = 2; e.Handled = true; break;
                    case Key.D4: tabGlowny.SelectedIndex = 3; e.Handled = true; break;
                    case Key.D5: tabGlowny.SelectedIndex = 4; e.Handled = true; break;
                }
            }

            if (e.Key == Key.F5)
            {
                _ostatnieFiltry = filtryPasek.ZbierzFiltry();
                _ = PrzekazFiltryDoAktywnejZakladki(_ostatnieFiltry);
                e.Handled = true;
            }
        }

        private void TabGlowny_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != tabGlowny) return;
            UstawTrybZakladki();
        }

        private void UstawTrybZakladki()
        {
            filtryPasek.Tryb = tabGlowny.SelectedIndex switch
            {
                0 => TrybZakladki.Plan,
                1 => TrybZakladki.Realizacja,
                2 => TrybZakladki.Bilans,
                3 => TrybZakladki.Wydajnosc,
                4 => TrybZakladki.Wydajnosc,
                _ => TrybZakladki.Bilans
            };
        }

        private async void FiltryPasek_FiltryZastosowane(object? sender, FiltryAnaliz f)
        {
            _ostatnieFiltry = f;
            await PrzekazFiltryDoAktywnejZakladki(f);
        }

        private async Task PrzekazFiltryDoAktywnejZakladki(FiltryAnaliz f)
        {
            if (_ladowanie)
            {
                // Anuluj poprzedni query
                _cts?.Cancel();
            }

            // Walidacja
            if (f.DataOd > f.DataDo)
            {
                txtStatus.Text = "⚠ Data Od jest późniejsza niż Data Do — sprawdź zakres";
                return;
            }
            if ((f.DataDo - f.DataOd).TotalDays > 365)
            {
                if (MessageBox.Show("Zakres przekracza 365 dni — zapytania mogą być wolne. Kontynuować?",
                    "Analityka Pełna", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    txtStatus.Text = "Anulowano (zakres za duży)";
                    return;
                }
            }

            _cts = new CancellationTokenSource();
            _ladowanie = true;
            var stoper = Stopwatch.StartNew();
            loadingOverlay.Pokaz(LiczbaSlow(tabGlowny.SelectedIndex));
            ZaktualizujBadgeFiltrow(f);

            try
            {
                txtStatus.Text = "Ładowanie danych…";
                switch (tabGlowny.SelectedIndex)
                {
                    case 0: await widokPlan.ZastosujFiltryAsync(f); break;
                    case 1: await widokRealizacja.ZastosujFiltryAsync(f); break;
                    case 2: await widokBilans.ZastosujFiltryAsync(f); break;
                    case 3: await widokWydajnosc.ZastosujFiltryAsync(f); break;
                    case 4: await widokWodospad.ZastosujFiltryAsync(f); break;
                }
                stoper.Stop();
                txtStatus.Text = $"✓ {f.DataOd:dd.MM.yyyy} – {f.DataDo:dd.MM.yyyy} • załadowano w {stoper.Elapsed.TotalSeconds:F1}s";
                txtCzasOdswiezenia.Text = $"Ostatnio: {DateTime.Now:HH:mm:ss}";
            }
            catch (OperationCanceledException)
            {
                txtStatus.Text = "Anulowano";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Błąd: " + ex.Message;
                MessageBox.Show(ex.Message, "Analityka Pełna",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _ladowanie = false;
                loadingOverlay.Ukryj();
            }
        }

        private void ZaktualizujBadgeFiltrow(FiltryAnaliz f)
        {
            var aktywne = new System.Collections.Generic.List<string>();
            if (f.TowarIdHandel.HasValue) aktywne.Add("towar");
            if (!string.IsNullOrEmpty(f.Dostawca)) aktywne.Add("hodowca");
            if (!string.IsNullOrEmpty(f.OperatorID)) aktywne.Add("operator");
            if (f.KlasaKurczaka.HasValue) aktywne.Add($"klasa {f.KlasaKurczaka}");

            if (aktywne.Count == 0)
            {
                badgeFiltry.Visibility = Visibility.Collapsed;
            }
            else
            {
                badgeFiltry.Visibility = Visibility.Visible;
                txtBadgeFiltry.Text = $"🔧 {aktywne.Count} aktywne: {string.Join(", ", aktywne)}";
            }
        }

        private static string LiczbaSlow(int tab) => tab switch
        {
            0 => "Liczę prognozę 8-tygodniową…",
            1 => "Wczytuję ważenia In0E…",
            2 => "Liczę bilans produkcji vs sprzedaży…",
            3 => "Liczę wydajność krojenia…",
            4 => "Liczę wodospad uzysku…",
            _ => "Ładowanie danych…"
        };

        private void BtnLive_Click(object sender, RoutedEventArgs e)
        {
            _liveAktywne = !_liveAktywne;
            if (_liveAktywne)
            {
                _liveTimer ??= new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(AnalitykaConfig.LiveRefreshSekund)
                };
                _liveTimer.Tick -= LiveTimerTick;
                _liveTimer.Tick += LiveTimerTick;
                _liveTimer.Start();
                filtryPasek.UstawWygladLive(true);
                txtStatus.Text = $"🔴 LIVE — auto-odświeżanie co {AnalitykaConfig.LiveRefreshSekund}s";
            }
            else
            {
                _liveTimer?.Stop();
                filtryPasek.UstawWygladLive(false);
                txtStatus.Text = "LIVE wyłączone";
            }
        }

        private async void LiveTimerTick(object? sender, EventArgs e)
        {
            if (_ladowanie) return;  // poprzednie jeszcze trwa, pomiń
            await PrzekazFiltryDoAktywnejZakladki(_ostatnieFiltry);
        }

        private void BtnEksport_Click(object sender, RoutedEventArgs e)
        {
            // Każda zakładka ma swój własny eksport CSV w toolbarze.
            // Tu pokazujemy informację, że eksport jest dostępny per zakładka.
            switch (tabGlowny.SelectedIndex)
            {
                case 0: widokPlan.EksportujCsv(); break;
                case 1: widokRealizacja.EksportujCsv(); break;
                case 2: widokBilans.EksportujCsv(); break;
                case 3: widokWydajnosc.EksportujCsv(); break;
                case 4: widokWodospad.EksportujCsv(); break;
            }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();
    }
}
