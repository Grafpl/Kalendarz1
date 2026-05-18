using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.AnalitykaPelna.Models;
using Kalendarz1.AnalitykaPelna.Services;

namespace Kalendarz1.AnalitykaPelna.Views
{
    /// <summary>
    /// Fabryka v3 — infographic timeline horizontal (6 kroków + szczegóły + magazyny).
    /// Inspiracja: Apple keynote process, Stripe checkout flow, modern infographic posters.
    /// </summary>
    public partial class WidokFabryka : UserControl
    {
        private readonly WydajnoscService _service = new();
        private FlowChainSummary _summary = new();
        private FiltryAnaliz? _ostatnieFiltry;

        public WidokFabryka()
        {
            InitializeComponent();
        }

        public async Task ZastosujFiltryAsync(FiltryAnaliz f)
        {
            _ostatnieFiltry = f;
            // Nałóż lokalne wybrane katalogi na filtr przekazany z paska
            f.KatalogiTowarow = ZebraneKatalogi();
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                _summary = await _service.LoadFlowChainAsync(f);
                Odswiez();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Filtr katalogów towarów — checkboxy w headerze
        // ════════════════════════════════════════════════════════════════
        private System.Collections.Generic.List<int> ZebraneKatalogi()
        {
            var lista = new System.Collections.Generic.List<int>();
            void Dodaj(System.Windows.Controls.CheckBox cb)
            {
                if (cb?.IsChecked == true && cb.Tag is string tag && int.TryParse(tag, out int k))
                    lista.Add(k);
            }
            Dodaj(chkKatZywiec);
            Dodaj(chkKatMieso);
            Dodaj(chkKatMiesoInne);
            Dodaj(chkKatMrozone);
            Dodaj(chkKatOdpady);
            return lista;
        }

        private async void KatalogChanged(object sender, RoutedEventArgs e)
        {
            if (_ostatnieFiltry == null) return;
            await ZastosujFiltryAsync(_ostatnieFiltry);
        }

        private async void PresetProdukcja_Click(object sender, RoutedEventArgs e)
        {
            chkKatZywiec.IsChecked = true;
            chkKatMieso.IsChecked = true;
            chkKatMiesoInne.IsChecked = true;
            chkKatMrozone.IsChecked = true;
            chkKatOdpady.IsChecked = true;
            if (_ostatnieFiltry != null)
                await ZastosujFiltryAsync(_ostatnieFiltry);
        }

        private async void PresetWszystkie_Click(object sender, RoutedEventArgs e)
        {
            // Odznacz wszystko = brak filtra = wszystkie katalogi liczone
            chkKatZywiec.IsChecked = false;
            chkKatMieso.IsChecked = false;
            chkKatMiesoInne.IsChecked = false;
            chkKatMrozone.IsChecked = false;
            chkKatOdpady.IsChecked = false;
            if (_ostatnieFiltry != null)
                await ZastosujFiltryAsync(_ostatnieFiltry);
        }

        private void Odswiez()
        {
            var s = _summary;

            // Header
            if (_ostatnieFiltry != null)
            {
                int dni = (_ostatnieFiltry.DataDo.Date - _ostatnieFiltry.DataOd.Date).Days + 1;
                txtZakres.Text = $"{_ostatnieFiltry.DataOd:dd.MM.yyyy} – {_ostatnieFiltry.DataDo:dd.MM.yyyy}  ·  {dni} dni  ·  Σ {s.LiczbaDokumentowCalkowita:N0} dokumentów";
            }

            // ═══════════════ ① BILANS GŁÓWNY (defensywnie) ═══════════════
            try { WypelnijBilansGlowny(s); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Bilans główny: " + ex); }

            // ═══════════════ ② ANALIZA STRAT (defensywnie) ═══════════════
            try { WypelnijAnalizeStrat(s); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Analiza strat: " + ex); }

            // ① ŻYWIEC
            (kpi1Value.Text, kpi1Unit.Text) = FormatKgT(s.Zywiec.Kg);
            kpi1Sub.Text = s.Zywiec.LiczbaDok > 0 ? $"{s.Zywiec.LiczbaDok} dokumentów przyjęcia" : "brak danych";

            // ② UBÓJ
            (kpi2Value.Text, kpi2Unit.Text) = FormatKgT(s.Uboj.Kg);
            kpi2WydVal.Text = s.Zywiec.Kg > 0 ? $"{s.WydajnoscUbojuProc:F1}" : "—";
            kpi2Strata.Text = s.Zywiec.Kg > 0
                ? $"↓ strata uboju: {FormatT(s.StratyUbojuKg)} t ({s.StratyUbojuProc:F1}%)\n   pióra, krew, woda, jelita"
                : "";

            // ③ KROJENIE
            (kpi3Value.Text, kpi3Unit.Text) = FormatKgT(s.Produkcja.Kg);
            kpi3WydVal.Text = s.RozchodKrojenia.Kg > 0 ? $"{s.WydajnoscKrojeniaProc:F1}" : "—";
            kpi3Strata.Text = s.RozchodKrojenia.Kg > 0
                ? $"wsad: {FormatT(s.RozchodKrojenia.Kg)} t (sRWP)\n↓ strata: {FormatT(s.StrataKrojeniaKg)} t — kości, ścinki"
                : "";

            // Badge nad ikoną KROJENIE: sRWP (wsad)
            badgeKrojRwp.Text = s.RozchodKrojenia.Kg > 0 ? $"{FormatT(s.RozchodKrojenia.Kg)} t" : "—";
            // Badge pod ikoną KROJENIE: sPWP (elementy)
            badgeKrojPwp.Text = s.Produkcja.Kg > 0 ? $"{FormatT(s.Produkcja.Kg)} t" : "—";

            // Łuk UBÓJ → PAKOWANIE (całe tuszki, omijają krojenie)
            lblArcKg.Text = s.UbojBezKrojeniaKg > 0 ? $"{FormatT(s.UbojBezKrojeniaKg)} t" : "—";
            UstawLukUbojPakowanie();

            // ④ PAKOWANIE (suma do magazynów)
            decimal sumPaczek = s.Dystrybucja.Kg + s.Mroznia.Kg + s.Masarnia.Kg + s.Karma.Kg + s.Odpady.Kg;
            (kpi4Value.Text, kpi4Unit.Text) = FormatKgT(sumPaczek);
            kpi4Sub.Text = sumPaczek > 0
                ? $"elementów + całych tuszek\nspakowane do pojemników E2 / kartonów"
                : "brak danych";

            // ⑤ MAGAZYNY — pokazuje sumę bez DYST (silosy wewnętrzne) + osobno DYST?
            // Tu pokażę DYST jako główny "magazyn" docelowy
            (kpi5Value.Text, kpi5Unit.Text) = FormatKgT(s.Dystrybucja.Kg);
            kpi5Sub.Text = s.Dystrybucja.Kg > 0
                ? $"w magazynie głównym DYST\n+ 4 inne magazyny (mróź/masar/karma/odp)"
                : "—";

            // ⑥ KLIENCI
            (kpi6Value.Text, kpi6Unit.Text) = FormatKgT(s.Klienci.Kg);
            kpi6Sub.Text = s.Klienci.LiczbaDok > 0
                ? $"{s.Klienci.LiczbaDok} dokumentów sprzedaży"
                : "brak wysyłki";

            // ═══════════════ MAGAZYNY DOCELOWE ═══════════════
            UstawSilo(silMrozKg, silMrozProc, silMrozDok, s.Mroznia.Kg, s.Mroznia.LiczbaDok, s.ProcDoMrozniProc);
            UstawSilo(silMasarKg, silMasarProc, silMasarDok, s.Masarnia.Kg, s.Masarnia.LiczbaDok, s.ProcDoMasarniProc);
            UstawSilo(silKarmaKg, silKarmaProc, silKarmaDok, s.Karma.Kg, s.Karma.LiczbaDok, s.ProcDoKarmyProc);
            UstawSilo(silOdpadyKg, silOdpadyProc, silOdpadyDok, s.Odpady.Kg, s.Odpady.LiczbaDok, s.ProcDoOdpadowProc);

            // ODPADY — status indicator (alarm gdy >5%)
            silOdpadyDot.Fill = StatusColor(s.ProcDoOdpadowProc > 8 ? "ALARM" : s.ProcDoOdpadowProc > 5 ? "WARN" : "OK");

            // DYSTRYBUCJA
            silDystKg.Text = FormatT(s.Dystrybucja.Kg);
            silDystProc.Text = s.Produkcja.Kg > 0 ? $"{s.ProcDoDystProc:F1}% z PROD" : "—%";
            silDystDoKli.Text = s.Dystrybucja.Kg > 0
                ? $"{FormatT(s.Klienci.Kg)} t ({s.ProcSprzedanoProc:F1}%)"
                : "—";

            // Footer
            txtFooter.Text = $"6-stopniowy proces produkcji  ·  Odświeżono {DateTime.Now:HH:mm:ss}";
        }

        private static void UstawSilo(TextBlock tbKg, TextBlock tbProc, TextBlock tbDok,
                                       decimal kg, int liczbaDok, decimal proc)
        {
            tbKg.Text = FormatT(kg);
            tbProc.Text = proc > 0 ? $"{proc:F1}%" : "—%";
            tbDok.Text = liczbaDok > 0 ? $"{liczbaDok} dok." : "";
        }

        private static (string val, string unit) FormatKgT(decimal kg)
        {
            if (kg == 0) return ("—", "");
            if (Math.Abs(kg) < 1000m) return ($"{kg:N0}", "kg");
            decimal tony = kg / 1000m;
            return tony >= 100 ? ($"{tony:N0}", "t") : ($"{tony:N1}", "t");
        }

        private static string FormatT(decimal kg)
        {
            if (kg == 0) return "—";
            if (Math.Abs(kg) < 1000m) return $"{kg:N0}";
            decimal tony = kg / 1000m;
            return tony >= 100 ? $"{tony:N0}" : $"{tony:N1}";
        }

        private static SolidColorBrush StatusColor(string s) => s switch
        {
            "OK" => new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
            "WARN" => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
            "ALARM" => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
            _ => new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))
        };

        // ════════════════════════════════════════════════════════════════
        // BILANS GŁÓWNY — co się stało z żywcem (4 segmenty + niezgodność)
        // ════════════════════════════════════════════════════════════════
        private void WypelnijBilansGlowny(FlowChainSummary s)
        {
            decimal wejscie = s.Zywiec.Kg;
            if (wejscie <= 0)
            {
                txtBilansHero.Text = "Brak danych żywca w okresie";
                txtBilansStatus.Text = "—";
                bdBilansStatus.Background = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                return;
            }

            decimal sprzedaz = s.Klienci.Kg;
            decimal wMagazynach = Math.Max(0m, s.Dystrybucja.Kg - s.Klienci.Kg) + s.Mroznia.Kg + s.Masarnia.Kg + Math.Max(0m, s.ZostaloProdKg);
            decimal straty = s.StratyUbojuKg + s.StrataKrojeniaKg;
            decimal odpadyKarma = s.Odpady.Kg + s.Karma.Kg;
            decimal rozliczone = sprzedaz + wMagazynach + straty + odpadyKarma;
            decimal niezgodnosc = wejscie - rozliczone;
            decimal niezgodAbs = Math.Abs(niezgodnosc);

            // Hero text
            txtBilansHero.Text = $"Z {FormatT(wejscie)} t żywca:  {FormatT(sprzedaz)} t sprzedano  ·  {FormatT(wMagazynach)} t w magazynach  ·  {FormatT(straty + odpadyKarma)} t straty/odpady";

            // Status badge (zgodność bilansu)
            decimal niezgodProc = wejscie > 0 ? niezgodAbs / wejscie * 100m : 0;
            if (niezgodProc < 3m)
            {
                txtBilansStatus.Text = "✓ ZAMYKA SIĘ";
                bdBilansStatus.Background = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            }
            else if (niezgodProc < 10m)
            {
                txtBilansStatus.Text = $"⚠ ROZBIEŻNOŚĆ {niezgodProc:F1}%";
                bdBilansStatus.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            }
            else
            {
                txtBilansStatus.Text = $"🚨 BILANS NIE GRA: {niezgodProc:F1}%";
                bdBilansStatus.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            }

            // Paskowy stacked bar — segmenty proporcjonalne (z fallbackiem na zero by uniknąć NaN w MeasureOverride)
            decimal total = sprzedaz + wMagazynach + straty + odpadyKarma + niezgodAbs;
            if (total <= 0m)
            {
                // wszystko zero → pojedyncza fałszywa kolumna żeby grid się nie wywalił
                colBarSprzedaz.Width = new GridLength(1, GridUnitType.Star);
                colBarWMag.Width = new GridLength(0, GridUnitType.Star);
                colBarStraty.Width = new GridLength(0, GridUnitType.Star);
                colBarOdpady.Width = new GridLength(0, GridUnitType.Star);
                colBarNiezgod.Width = new GridLength(0, GridUnitType.Star);
            }
            else
            {
                colBarSprzedaz.Width = new GridLength(Math.Max(0.0001, (double)sprzedaz), GridUnitType.Star);
                colBarWMag.Width = new GridLength(Math.Max(0.0001, (double)wMagazynach), GridUnitType.Star);
                colBarStraty.Width = new GridLength(Math.Max(0.0001, (double)straty), GridUnitType.Star);
                colBarOdpady.Width = new GridLength(Math.Max(0.0001, (double)odpadyKarma), GridUnitType.Star);
                colBarNiezgod.Width = new GridLength(Math.Max(0.0001, (double)niezgodAbs), GridUnitType.Star);
            }

            // Etykiety segmentów (tylko gdy >5% dla czytelności)
            decimal sprzedazPct = wejscie > 0 ? sprzedaz / wejscie * 100m : 0;
            decimal wMagPct = wejscie > 0 ? wMagazynach / wejscie * 100m : 0;
            decimal stratyPct = wejscie > 0 ? straty / wejscie * 100m : 0;
            decimal odpKarmaPct = wejscie > 0 ? odpadyKarma / wejscie * 100m : 0;
            decimal niezgodPct = wejscie > 0 ? niezgodAbs / wejscie * 100m : 0;
            txtBarSprzedaz.Text = sprzedazPct >= 5 ? $"{sprzedazPct:F0}%" : "";
            txtBarWMag.Text = wMagPct >= 5 ? $"{wMagPct:F0}%" : "";
            txtBarStraty.Text = stratyPct >= 5 ? $"{stratyPct:F0}%" : "";
            txtBarOdpady.Text = odpKarmaPct >= 5 ? $"{odpKarmaPct:F0}%" : "";
            txtBarNiezgod.Text = niezgodPct >= 3 ? $"{niezgodPct:F0}%" : "";

            // Legenda
            txtLegSprzedaz.Text = $"{FormatT(sprzedaz)} t  ({sprzedazPct:F1}%)";
            txtLegWMag.Text = $"{FormatT(wMagazynach)} t  ({wMagPct:F1}%)";
            txtLegStraty.Text = $"{FormatT(straty)} t  ({stratyPct:F1}%)";
            txtLegOdpady.Text = $"{FormatT(odpadyKarma)} t  ({odpKarmaPct:F1}%)";
            txtLegNiezgod.Text = niezgodAbs > 0
                ? $"{(niezgodnosc >= 0 ? "+" : "−")}{FormatT(niezgodAbs)} t  ({niezgodPct:F1}%)"
                : "0 t";
        }

        // ════════════════════════════════════════════════════════════════
        // ANALIZA STRAT — punkty gdzie towar może zniknąć
        // ════════════════════════════════════════════════════════════════
        private void WypelnijAnalizeStrat(FlowChainSummary s)
        {
            panelAnaliza.Children.Clear();
            int alarmCount = 0, warnCount = 0;

            decimal wejscie = s.Zywiec.Kg;
            if (wejscie <= 0)
            {
                txtAnalizaIlosc.Text = "brak danych";
                bdInsight.Visibility = Visibility.Collapsed;
                return;
            }

            // 1. STRATA UBOJU (pióra/krew/woda) — norma 12-18%
            decimal stratUbojProc = s.StratyUbojuProc;
            string stratUbojStatus = (stratUbojProc >= 12 && stratUbojProc <= 18) ? "OK"
                                    : (stratUbojProc >= 10 && stratUbojProc <= 22) ? "WARN" : "ALARM";
            if (stratUbojStatus == "ALARM") alarmCount++; else if (stratUbojStatus == "WARN") warnCount++;
            panelAnaliza.Children.Add(BudujWierszStraty(
                "🐔→⚙", "Strata uboju", "pióra, krew, woda, jelita",
                s.StratyUbojuKg, stratUbojProc, "12-18%", 12m, 18m, stratUbojStatus));

            // 2. STRATA KROJENIA (kości/ścinki) — norma 36-42%
            if (s.RozchodKrojenia.Kg > 0)
            {
                decimal stratKrojProc = s.StrataKrojeniaProc;
                string stratKrojStatus = (stratKrojProc >= 36 && stratKrojProc <= 42) ? "OK"
                                        : (stratKrojProc >= 30 && stratKrojProc <= 48) ? "WARN" : "ALARM";
                if (stratKrojStatus == "ALARM") alarmCount++; else if (stratKrojStatus == "WARN") warnCount++;
                panelAnaliza.Children.Add(BudujWierszStraty(
                    "🔪→📦", "Strata krojenia", "kości, ścinki, ubytki",
                    s.StrataKrojeniaKg, stratKrojProc, "36-42%", 36m, 42m, stratKrojStatus));
            }

            // 3. ODPADY — norma 3-5%
            decimal odpProc = s.ProcDoOdpadowProc;
            string odpStatus = (odpProc <= 5) ? "OK" : (odpProc <= 8) ? "WARN" : "ALARM";
            if (odpStatus == "ALARM") alarmCount++; else if (odpStatus == "WARN") warnCount++;
            panelAnaliza.Children.Add(BudujWierszStraty(
                "📦→🗑", "Odpady", "utylizacja zewnętrzna",
                s.Odpady.Kg, odpProc, "≤5%", 0m, 5m, odpStatus));

            // 4. ZALEGA W MAGAZYNACH — norma <15%
            decimal wMag = Math.Max(0m, s.Dystrybucja.Kg - s.Klienci.Kg) + s.Mroznia.Kg + s.Masarnia.Kg + Math.Max(0m, s.ZostaloProdKg);
            decimal wMagProc = wejscie > 0 ? wMag / wejscie * 100m : 0;
            string wMagStatus = (wMagProc <= 15) ? "OK" : (wMagProc <= 25) ? "WARN" : "ALARM";
            if (wMagStatus == "ALARM") alarmCount++; else if (wMagStatus == "WARN") warnCount++;
            panelAnaliza.Children.Add(BudujWierszStraty(
                "🏬", "Zalega w magazynach", "DYST/MROŹ/MASAR/zostało w PROD",
                wMag, wMagProc, "≤15%", 0m, 15m, wMagStatus));

            // 5. KARMA — informacyjnie (brak ścisłej normy ale >8% sygnał)
            decimal karmaProc = s.ProcDoKarmyProc;
            string karmaStatus = (karmaProc <= 5) ? "OK" : (karmaProc <= 10) ? "WARN" : "ALARM";
            if (karmaStatus == "ALARM") alarmCount++; else if (karmaStatus == "WARN") warnCount++;
            panelAnaliza.Children.Add(BudujWierszStraty(
                "🌾", "Karma", "dla zwierząt (klasa B / MDM)",
                s.Karma.Kg, karmaProc, "≤5%", 0m, 5m, karmaStatus));

            // 6. NIEWYJAŚNIONE — różnica bilansu
            decimal sprzedaz = s.Klienci.Kg;
            decimal straty = s.StratyUbojuKg + s.StrataKrojeniaKg;
            decimal odpKarma = s.Odpady.Kg + s.Karma.Kg;
            decimal rozliczone = sprzedaz + wMag + straty + odpKarma;
            decimal niezgodAbs = Math.Abs(wejscie - rozliczone);
            decimal niezgodProc = wejscie > 0 ? niezgodAbs / wejscie * 100m : 0;
            string niezgodStatus = (niezgodProc <= 3) ? "OK" : (niezgodProc <= 10) ? "WARN" : "ALARM";
            if (niezgodStatus == "ALARM") alarmCount++; else if (niezgodStatus == "WARN") warnCount++;
            panelAnaliza.Children.Add(BudujWierszStraty(
                "❓", "Niewyjaśnione", "różnica vs masa wejściowa",
                niezgodAbs, niezgodProc, "≤3%", 0m, 3m, niezgodStatus));

            txtAnalizaIlosc.Text = $"{alarmCount + warnCount} alarmów  ·  punktów strat: {panelAnaliza.Children.Count}";

            // Insight tip
            string insight = "";
            if (niezgodStatus == "ALARM")
                insight = $"Bilans masy nie zamyka się ({niezgodProc:F1}% rozbieżności). Możliwe: towar z innego okresu, brak dokumentów lub błąd ewidencji. Sprawdź dokumenty WZ/MM-.";
            else if (wMagStatus == "ALARM")
                insight = $"Dużo towaru zalega w magazynach ({wMagProc:F1}%, norma ≤15%). Sprawdź czy nie zalega stary towar lub przepływ jest wolny.";
            else if (odpStatus == "ALARM")
                insight = $"Wysokie odpady ({odpProc:F1}%, norma ≤5%). Sprawdź klasę żywca i jakość krojenia. Możliwa kradzież/wyciek towaru przez fałszywe dokumenty odpadów.";
            else if (alarmCount > 0 || warnCount > 0)
                insight = $"Wykryto {alarmCount + warnCount} odchyleń od normy. Sprawdź dokumenty etapów z statusem ⚠ lub 🚨.";

            if (!string.IsNullOrEmpty(insight))
            {
                txtInsight.Text = insight;
                bdInsight.Visibility = Visibility.Visible;
            }
            else bdInsight.Visibility = Visibility.Collapsed;
        }

        private UIElement BudujWierszStraty(string ikona, string nazwa, string opis,
                                            decimal kg, decimal proc, string norma,
                                            decimal normaMin, decimal normaMax, string status)
        {
            var row = new Border
            {
                Padding = new Thickness(0, 8, 0, 8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Double.NaN, GridUnitType.Auto) });

            // Etap (ikona + nazwa + opis)
            var spEtap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            spEtap.Children.Add(new TextBlock
            {
                Text = ikona, FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0)
            });
            var spNazwa = new StackPanel();
            spNazwa.Children.Add(new TextBlock { Text = nazwa, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)) });
            spNazwa.Children.Add(new TextBlock { Text = opis, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)) });
            spEtap.Children.Add(spNazwa);
            grid.Children.Add(spEtap);
            Grid.SetColumn(spEtap, 0);

            // kg
            var kgTb = new TextBlock
            {
                Text = $"{FormatT(kg)} t",
                FontSize = 13, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI"),
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A))
            };
            grid.Children.Add(kgTb);
            Grid.SetColumn(kgTb, 1);

            // %
            var procTb = new TextBlock
            {
                Text = $"{proc:F1}%",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI"),
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))
            };
            grid.Children.Add(procTb);
            Grid.SetColumn(procTb, 2);

            // norma
            var normaTb = new TextBlock
            {
                Text = norma,
                FontSize = 11,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                Margin = new Thickness(0, 0, 8, 0)
            };
            grid.Children.Add(normaTb);
            Grid.SetColumn(normaTb, 3);

            // VS NORMY - pasek wizualny
            var barGrid = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 12, 0) };
            barGrid.ColumnDefinitions.Add(new ColumnDefinition());
            // Tło paska (norma zakres)
            var bgBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                CornerRadius = new CornerRadius(4),
                Height = 8
            };
            barGrid.Children.Add(bgBar);
            // Skala dynamiczna: max(50, proc*1.3) — zabezpiecz przed 0/NaN
            double procD = Math.Max(0, (double)proc);
            double maxScale = Math.Max(50, procD * 1.3);
            double procPct = Math.Min(95, procD / maxScale * 100);
            if (double.IsNaN(procPct) || double.IsInfinity(procPct)) procPct = 0;
            // Aktualna wartość — pasek wypełnienia
            var actBar = new Grid { Height = 8 };
            actBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.0001, procPct), GridUnitType.Star) });
            actBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.0001, 100 - procPct), GridUnitType.Star) });
            var fillColor = status == "ALARM" ? Color.FromRgb(0xEF, 0x44, 0x44)
                          : status == "WARN" ? Color.FromRgb(0xF5, 0x9E, 0x0B)
                          : Color.FromRgb(0x10, 0xB9, 0x81);
            var fill = new Border { Background = new SolidColorBrush(fillColor), CornerRadius = new CornerRadius(4) };
            Grid.SetColumn(fill, 0);
            actBar.Children.Add(fill);
            barGrid.Children.Add(actBar);
            grid.Children.Add(barGrid);
            Grid.SetColumn(barGrid, 4);

            // STATUS pill
            var statusBg = status == "ALARM" ? Color.FromRgb(0xEF, 0x44, 0x44)
                         : status == "WARN" ? Color.FromRgb(0xF5, 0x9E, 0x0B)
                         : Color.FromRgb(0x10, 0xB9, 0x81);
            var statusPill = new Border
            {
                Background = new SolidColorBrush(statusBg),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(14, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusPill.Child = new TextBlock
            {
                Text = status == "OK" ? "✓ OK" : status == "WARN" ? "⚠ uwaga" : "🚨 alarm",
                FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF))
            };
            grid.Children.Add(statusPill);
            Grid.SetColumn(statusPill, 5);

            row.Child = grid;
            return row;
        }

        // ════════════════════════════════════════════════════════════════
        // Łuk UBÓJ → PAKOWANIE (omija krojenie — całe tuszki bezpośrednio)
        // ════════════════════════════════════════════════════════════════
        private void GridTimeline_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UstawLukUbojPakowanie();
        }

        private void UstawLukUbojPakowanie()
        {
            if (gridTimeline == null || pathArc == null || lblArc == null) return;
            double w = gridTimeline.ActualWidth;
            if (w <= 0) return;

            // 6 kolumn — środek każdej
            double colW = w / 6.0;
            double col2Cx = 1.5 * colW;  // UBÓJ (kolumna index 1)
            double col4Cx = 3.5 * colW;  // PAKOWANIE (kolumna index 3)

            // Pozycja Y — Canvas ma Margin top=-50, więc Y=0 to 50px nad linią kółek
            // Linia łącząca kółek jest na y=38 od top of grid → w Canvas (z marginem -50) to y=88
            // Łuk wychodzi z górnej krawędzi kółka UBÓJ (~ y=88 - 38 = 50 w Canvas)
            // i idzie do górnej krawędzi kółka PAKOWANIE (y=50)
            // Najwyższy punkt łuku: y=10
            double yStart = 50;
            double yPeak = 6;

            var geom = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(col2Cx, yStart) };
            fig.Segments.Add(new BezierSegment(
                new Point(col2Cx + colW * 0.4, yPeak),
                new Point(col4Cx - colW * 0.4, yPeak),
                new Point(col4Cx, yStart),
                isStroked: true));
            geom.Figures.Add(fig);
            pathArc.Data = geom;

            // Etykieta w środku łuku (najwyższy punkt) — defensywnie wobec NaN z DesiredSize
            double midX = (col2Cx + col4Cx) / 2;
            try { lblArc.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); } catch { }
            double lblW = lblArc.DesiredSize.Width;
            double lblH = lblArc.DesiredSize.Height;
            if (double.IsNaN(lblW) || double.IsInfinity(lblW) || lblW <= 0) lblW = 200;
            if (double.IsNaN(lblH) || double.IsInfinity(lblH) || lblH <= 0) lblH = 26;
            Canvas.SetLeft(lblArc, midX - lblW / 2);
            Canvas.SetTop(lblArc, yPeak - lblH / 2);
        }

        // ════════════════════════════════════════════════════════════════
        // Custom dialogi: Całe tuszki sPWU + Pakowanie sMM−
        // ════════════════════════════════════════════════════════════════
        private void CaleTuszki_Click(object sender, MouseButtonEventArgs e)
        {
            var dlg = new Windows.PodzialFlowDialog(_summary, Windows.PodzialFlowDialog.Tryb.CaleTuszki)
            {
                Owner = Window.GetWindow(this)
            };
            dlg.ShowDialog();
            e.Handled = true;
        }

        private void Pakowanie_Click(object sender, MouseButtonEventArgs e)
        {
            var dlg = new Windows.PodzialFlowDialog(_summary, Windows.PodzialFlowDialog.Tryb.Pakowanie)
            {
                Owner = Window.GetWindow(this)
            };
            dlg.ShowDialog();
            e.Handled = true;
        }

        // ════════════════════════════════════════════════════════════════
        // Click — kroki + karty + silosy otwierają FlowChainEtapDialog
        // ════════════════════════════════════════════════════════════════
        private async void Step_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement el || el.Tag is not string etap || string.IsNullOrEmpty(etap) || _ostatnieFiltry == null) return;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var detail = await _service.LoadFlowChainEtapDetailAsync(etap, _ostatnieFiltry);
                Mouse.OverrideCursor = null;
                var dlg = new Windows.FlowChainEtapDialog(detail) { Owner = Window.GetWindow(this) };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show("Błąd: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
