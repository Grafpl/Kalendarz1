using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.AnalitykaPelna.Models;
using Kalendarz1.AnalitykaPelna.Services;
using LiveCharts;
using LiveCharts.Wpf;

namespace Kalendarz1.AnalitykaPelna.Views
{
    public partial class WidokWydajnosc : UserControl
    {
        private readonly WydajnoscService _service = new();
        private List<WydajnoscDzien> _dniSurowe = new();   // raw per-day z DB
        private List<WydajnoscDzien> _ostatnieDni = new(); // po agregacji wg wybranego okresu
        private List<WydajnoscHodowca> _ostatniHodowcy = new();
        private List<WydajnoscKlasa> _ostatnieKlasy = new();
        private List<WydajnoscSzczegolElement> _ostatnieElementy = new();
        private BilansMaterialowy _ostatniBilansMat = new();
        private List<StanMagazynu> _ostatnieStanyMagazynow = new();
        private List<PrzeplywMagazynow> _ostatniePrzeplywy = new();
        private List<TowarProdukcyjny> _ostatnieTowaryProdukcji = new();
        private FlowChainSummary _ostatniFlowChain = new();
        private List<FlowChainTowar> _ostatnieUbojTopTowary = new();
        private List<FlowChainTowar> _ostatnieProdTopTowary = new();
        private List<UzyskiPerHodowca> _ostatnieUzyski = new();
        private FiltryAnaliz _ostatnieFiltry = new();

        public WidokWydajnosc()
        {
            InitializeComponent();
        }

        public async Task ZastosujFiltryAsync(FiltryAnaliz f)
        {
            _ostatnieFiltry = f;
            try
            {
                _dniSurowe = await _service.LoadWydajnoscUbojuPerDzienAsync(f);
                _ostatnieDni = AgregujDniTrend(_dniSurowe, AktualnyOkresTrend());
                _ostatniHodowcy = await _service.LoadWydajnoscPerHodowcaAsync(f);
                _ostatnieKlasy = await _service.LoadWydajnoscPerKlasaAsync(f);
                _ostatnieElementy = await _service.LoadSzczegolyElementowAsync(f);
                _ostatniBilansMat = await _service.LoadBilansMaterialowyAsync(f);
                OdswiezBilansMaterialowy();
                _ostatnieStanyMagazynow = await _service.LoadStanMagazynowAsync(f);
                _ostatniePrzeplywy = await _service.LoadPrzeplywyMagazynowAsync(f);
                _ostatnieTowaryProdukcji = await _service.LoadTowaryProdukcjiAsync(f);
                _ostatniFlowChain = await _service.LoadFlowChainAsync(f);
                _ostatnieUbojTopTowary = await _service.LoadTopTowaryEtapuAsync("UBOJ", f, 6);
                _ostatnieProdTopTowary = await _service.LoadTopTowaryEtapuAsync("PRODUKCJA", f, 6);
                OdswiezStanMagazynow();
                OdswiezFlowChain();
                OdswiezTowaryProdukcji();
                await OdswiezUzyskiAsync();

                // Sub-tab "Łańcuch Graficzny" — własny zestaw danych (równoległe 10 query)
                if (widokLancuch != null)
                    _ = widokLancuch.ZastosujFiltryAsync(f);

                // Sub-tab "Fabryka" — używa summary z FlowChainGraficzny już załadowane przez WidokLancuch
                if (widokFabryka != null)
                    _ = widokFabryka.ZastosujFiltryAsync(f);

                // Sub-tab "Sankey" — proporcjonalne strumienie z FlowChainSummary
                if (widokSankey != null)
                    _ = widokSankey.ZastosujFiltryAsync(f);

                decimal sumaZywca = _ostatnieDni.Sum(d => d.ZywiecKg);
                decimal sumaWyjscie = _ostatnieDni.Sum(d => d.SumaWyjscia);
                decimal srWydajnosc = sumaZywca == 0 ? 0 : sumaWyjscie / sumaZywca * 100m;
                int alertow = _ostatnieDni.Count(d => d.CzyAlert);
                decimal sumaTuszkaB = _ostatnieDni.Sum(d => d.TuszkaBKg);
                var topHodowca = _ostatniHodowcy.OrderByDescending(h => h.SumaWyjscieKg).FirstOrDefault();

                kpiTuszkaB.Text = $"{sumaTuszkaB:N0} kg";
                kpiWyjscie.Text = $"{sumaWyjscie:N0} kg";
                kpiWydajnosc.Text = sumaTuszkaB > 0 ? srWydajnosc.ToString("N1") + "%" : "—";
                kpiWydajnosc.Foreground = WydajnoscKolor(srWydajnosc);
                kpiAlerty.Text = alertow.ToString();
                kpiAlerty.Foreground = alertow > 0
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDC, 0x26, 0x26))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x05, 0x96, 0x69));
                kpiTopHodowca.Text = topHodowca?.Hodowca ?? "—";
                if (topHodowca != null)
                    kpiTopHodowca.ToolTip = $"{topHodowca.SumaWyjscieKg:N0} kg / {topHodowca.LiczbaPartii} partii";

                BudujWykresTrendu(_ostatnieDni);
                BudujWykresKlas(_ostatnieKlasy);
                _trendView = System.Windows.Data.CollectionViewSource.GetDefaultView(_ostatnieDni);
                dgTrend.ItemsSource = _trendView;
                ZastosujTrendFiltr();

                _hodowcyView = System.Windows.Data.CollectionViewSource.GetDefaultView(_ostatniHodowcy);
                dgHodowcy.ItemsSource = _hodowcyView;
                ZastosujHodowcyFiltr();

                // Tabela klas — z wirtualnymi wierszami podsumowania (Σ) per grupa
                WypelnijTabeleKlas(_ostatnieKlasy);

                _elementyView = System.Windows.Data.CollectionViewSource.GetDefaultView(_ostatnieElementy);
                dgElementy.ItemsSource = _elementyView;
                ZastosujElementyFiltr();
                ZastosujElementyGrupowanie(chkElementyGrupowanie?.IsChecked == true);
                AktualizujElementyKpi();
                BudujFiltrySerii();
                BudujWykresElementy();

                // Mini-stats per zakładka — użyj agregowanej metryki
                AktualizujMiniStatsTrend();

                decimal sumaHodowcy = _ostatniHodowcy.Sum(h => h.SumaWyjscieKg);
                var topHo = _ostatniHodowcy.FirstOrDefault();
                txtHodowcyStats.Text = _ostatniHodowcy.Count == 0 ? "Brak hodowców"
                    : $"🐔 {_ostatniHodowcy.Count} hodowców • łącznie {sumaHodowcy:N0} kg • " +
                      $"top: {topHo?.Hodowca} ({topHo?.SumaWyjscieKg:N0} kg)";

                AktualizujKpiKlas();

                // KPI elementów aktualizowany w AktualizujElementyKpi() po pełnym renderze

                txtInfo.Text = _ostatnieDni.Count == 0
                    ? "Brak danych wydajności w wybranym zakresie"
                    : $"{f.DataOd:dd.MM.yyyy} – {f.DataDo:dd.MM.yyyy}  •  " +
                      $"{_ostatnieDni.Count} dni  •  {_ostatniHodowcy.Count} hodowców  •  " +
                      $"{_ostatnieElementy.Count} pozycji elementów  •  alertów: {alertow}";
            }
            catch
            {
                _ostatnieDni.Clear();
                _ostatniHodowcy.Clear();
                _ostatnieKlasy.Clear();
                _ostatnieElementy.Clear();
                dgTrend.ItemsSource = null;
                dgHodowcy.ItemsSource = null;
                dgKlasyDuzy.ItemsSource = null;
                dgKlasyMaly.ItemsSource = null;
                dgElementy.ItemsSource = null;
                wykresTrend.Series = new SeriesCollection();
                throw;
            }
        }

        private void BudujWykresTrendu(List<WydajnoscDzien> dni)
        {
            if (dni == null || dni.Count == 0)
            {
                wykresTrend.Series = new SeriesCollection();
                osXTrend.Labels = new List<string>();
                return;
            }

            // Tryb wyboru z radio buttonów: ZPodrobami / TylkoTuszki / TylkoPodroby
            bool zPodrobami = rbTrendZPodrobami?.IsChecked == true;
            bool tylkoTuszki = rbTrendTylkoTuszki?.IsChecked == true;
            bool tylkoPodroby = rbTrendTylkoPodroby?.IsChecked == true;

            // Default jeśli nic nie zaznaczone — pierwszy
            if (!zPodrobami && !tylkoTuszki && !tylkoPodroby) zPodrobami = true;

            var series = new SeriesCollection();
            double normaWyk;
            string normaLabel;

            if (zPodrobami)
            {
                series.Add(new LineSeries
                {
                    Title = "🟣 % wydajności z podrobami (A+B+podroby) / żywiec",
                    Values = new ChartValues<double>(dni.Select(d => SafeDouble(d.WydajnoscZPodrobamiProc))),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)),
                    StrokeThickness = 3,
                    Fill = new SolidColorBrush(Color.FromArgb(0x25, 0x7C, 0x3A, 0xED)),
                    PointGeometrySize = 14,
                    PointForeground = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)),
                    LineSmoothness = 0.3,
                    DataLabels = true,
                    LabelPoint = p => p.Y.ToString("F1") + "%",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED))
                });
                normaWyk = AnalitykaConfig.NormaWydajnosciProc;
                normaLabel = $"Norma {normaWyk:F1}%";
            }
            else if (tylkoTuszki)
            {
                series.Add(new LineSeries
                {
                    Title = "🔵 % wydajności tuszek (A+B) / żywiec",
                    Values = new ChartValues<double>(dni.Select(d => SafeDouble(d.WydajnoscBezPodrobowProc))),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x08, 0x91, 0xB2)),
                    StrokeThickness = 3,
                    Fill = new SolidColorBrush(Color.FromArgb(0x25, 0x08, 0x91, 0xB2)),
                    PointGeometrySize = 14,
                    PointForeground = new SolidColorBrush(Color.FromRgb(0x08, 0x91, 0xB2)),
                    LineSmoothness = 0.3,
                    DataLabels = true,
                    LabelPoint = p => p.Y.ToString("F1") + "%",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x08, 0x91, 0xB2))
                });
                normaWyk = AnalitykaConfig.NormaWydajnosciProc;
                normaLabel = $"Norma {normaWyk:F1}%";
            }
            else // tylkoPodroby
            {
                series.Add(new LineSeries
                {
                    Title = "🟠 % udziału podrobów / żywiec",
                    Values = new ChartValues<double>(dni.Select(d => SafeDouble(d.WydajnoscPodrobowProc))),
                    Stroke = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                    StrokeThickness = 3,
                    Fill = new SolidColorBrush(Color.FromArgb(0x25, 0xF5, 0x9E, 0x0B)),
                    PointGeometrySize = 14,
                    PointForeground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                    LineSmoothness = 0.3,
                    DataLabels = true,
                    LabelPoint = p => p.Y.ToString("F2") + "%",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xB4, 0x53, 0x09))
                });
                normaWyk = AnalitykaConfig.NormaPodrobowProc;
                normaLabel = $"Norma {normaWyk:F2}%";
            }

            // Linia normy
            series.Add(new LineSeries
            {
                Title = normaLabel,
                Values = new ChartValues<double>(dni.Select(_ => normaWyk)),
                Stroke = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
                StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 3 },
                StrokeThickness = 1.5,
                Fill = System.Windows.Media.Brushes.Transparent,
                PointGeometry = null
            });

            // Dopasuj oś Y do danych (z marginesem) + uwzględnij normę
            DopasujOsYTrend(series, normaWyk, tylkoPodroby);

            wykresTrend.Series = series;

            // Etykiety osi X — krótkie, dwuwierszowe, zawsze poziomo (LabelsRotation=0)
            var okres = AktualnyOkresTrend();
            osXTrend.Labels = dni.Select(d => SkrocEtykieteOsi(d, okres)).ToList();
            osXTrend.LabelsRotation = 0;
            osXTrend.Title = okres switch
            {
                OkresAgregacji.Tygodniowa => "Tydzień (poniedziałek)",
                OkresAgregacji.Miesieczna => "Miesiąc",
                OkresAgregacji.Kwartalna => "Kwartał",
                OkresAgregacji.Roczna => "Rok",
                OkresAgregacji.CalyOkres => "Okres",
                _ => "Dzień"
            };
        }

        /// <summary>
        /// Krótka etykieta na oś X — dwuwierszowa, czytelna w pionie nawet gdy mamy 12+ słupków.
        /// </summary>
        private static string SkrocEtykieteOsi(WydajnoscDzien d, OkresAgregacji okres)
        {
            var pl = new System.Globalization.CultureInfo("pl-PL");
            switch (okres)
            {
                case OkresAgregacji.Dzienna:
                    return $"{d.Data:dd.MM}\n{d.Data.ToString("ddd", pl)}";
                case OkresAgregacji.Tygodniowa:
                    {
                        int t = System.Globalization.ISOWeek.GetWeekOfYear(d.Data);
                        var pn = d.DataOd != default ? d.DataOd : d.Data;
                        return $"T{t:00}\n{pn:dd.MM}";
                    }
                case OkresAgregacji.Miesieczna:
                    return d.Data.ToString("MMM\nyyyy", pl);
                case OkresAgregacji.Kwartalna:
                    {
                        int q = (d.Data.Month - 1) / 3 + 1;
                        return $"Q{q}\n{d.Data:yyyy}";
                    }
                case OkresAgregacji.Roczna:
                    return d.Data.Year.ToString();
                default:
                    return string.IsNullOrEmpty(d.EtykietaOkresu) ? d.Data.ToString("dd.MM") : d.EtykietaOkresu;
            }
        }

        /// <summary>
        /// Dynamicznie dopasowuje oś Y do zakresu wartości — żeby linia nie była zgubiona w zbyt szerokim zakresie.
        /// </summary>
        private void DopasujOsYTrend(SeriesCollection series, double norma, bool tylkoPodroby)
        {
            if (osYTrend == null) return;

            // Bierze pierwszą serię (właściwe dane) — pomija linię normy
            var glownaSeria = series.OfType<LineSeries>().FirstOrDefault();
            if (glownaSeria?.Values is not ChartValues<double> values || values.Count == 0)
            {
                osYTrend.MinValue = double.NaN;
                osYTrend.MaxValue = double.NaN;
                return;
            }

            double min = values.Min();
            double max = values.Max();
            // Uwzględnij normę
            min = Math.Min(min, norma);
            max = Math.Max(max, norma);

            double zakres = max - min;
            double padding;
            if (tylkoPodroby)
            {
                // Małe wartości: padding 0.5 lub 25% zakresu
                padding = Math.Max(0.5, zakres * 0.25);
            }
            else
            {
                // Wydajność: padding ~3 punkty lub 25% zakresu
                padding = Math.Max(2.0, zakres * 0.25);
            }

            double minOs = Math.Max(0, min - padding);
            double maxOs = max + padding;

            osYTrend.MinValue = minOs;
            osYTrend.MaxValue = maxOs;
            osYTrend.Title = tylkoPodroby ? "% udziału podrobów" : "Wydajność %";
        }

        private void OpcjeTrend_Changed(object sender, RoutedEventArgs e)
        {
            if (_ostatnieDni == null || _ostatnieDni.Count == 0) return;
            BudujWykresTrendu(_ostatnieDni);
        }

        // ─── Agregacja okresu trend ───

        private OkresAgregacji AktualnyOkresTrend()
        {
            if (cbTrendAgregacja?.SelectedItem is System.Windows.Controls.ComboBoxItem item
                && item.Tag is string tag
                && Enum.TryParse<OkresAgregacji>(tag, out var okres))
                return okres;
            return OkresAgregacji.Dzienna;
        }

        private void TrendAgregacja_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_dniSurowe == null || _dniSurowe.Count == 0) return;
            if (!IsLoaded) return;
            _ostatnieDni = AgregujDniTrend(_dniSurowe, AktualnyOkresTrend());
            BudujWykresTrendu(_ostatnieDni);
            _trendView = System.Windows.Data.CollectionViewSource.GetDefaultView(_ostatnieDni);
            dgTrend.ItemsSource = _trendView;
            ZastosujTrendFiltr();
            AktualizujMiniStatsTrend();
        }

        private void AktualizujMiniStatsTrend()
        {
            if (_ostatnieDni.Count == 0)
            {
                txtTrendStats.Text = "Brak danych w wybranym zakresie";
                return;
            }
            decimal sumaZywca = _ostatnieDni.Sum(d => d.ZywiecKg);
            decimal sumaWyj = _ostatnieDni.Sum(d => d.SumaWyjscia);
            decimal srWydaj = sumaZywca <= 0 ? 0 : sumaWyj / sumaZywca * 100m;
            int alertow = _ostatnieDni.Count(d => d.CzyAlert);
            string okresNazwa = AktualnyOkresTrend() switch
            {
                OkresAgregacji.Dzienna => "dni",
                OkresAgregacji.Tygodniowa => "tygodni",
                OkresAgregacji.Miesieczna => "miesięcy",
                OkresAgregacji.Kwartalna => "kwartałów",
                OkresAgregacji.Roczna => "lat",
                _ => "okresów"
            };
            txtTrendStats.Text =
                $"📊 {_ostatnieDni.Count} {okresNazwa} • " +
                $"żywiec {sumaZywca:N0} kg → wyjście {sumaWyj:N0} kg • " +
                $"średnia wydajność {srWydaj:F1}% • alertów: {alertow}";
        }

        private List<WydajnoscDzien> AgregujDniTrend(List<WydajnoscDzien> dni, OkresAgregacji okres)
        {
            if (dni == null || dni.Count == 0) return new List<WydajnoscDzien>();

            // Per dzień — wystarczy wzbogacić etykietę
            if (okres == OkresAgregacji.Dzienna)
            {
                foreach (var d in dni)
                {
                    d.EtykietaOkresu = $"{d.Data:dd.MM.yyyy} ({d.DzienTygodnia})";
                    d.DataOd = d.Data;
                    d.DataDo = d.Data;
                }
                return dni.OrderBy(d => d.Data).ToList();
            }

            double norma = AnalitykaConfig.NormaWydajnosciProc;
            double tol = AnalitykaConfig.TolerancjaWydajnosciProc;

            var grupy = dni.GroupBy(d => OkresHelper.DlaDaty(d.Data, okres).Klucz);
            return grupy.Select(g =>
            {
                var pierwszy = g.First();
                var (klucz, etykieta, od, doData) = OkresHelper.DlaDaty(pierwszy.Data, okres);
                if (okres == OkresAgregacji.CalyOkres)
                {
                    od = g.Min(d => d.Data);
                    doData = g.Max(d => d.Data);
                    etykieta = $"Cały okres ({od:dd.MM.yyyy}–{doData:dd.MM.yyyy})";
                }
                var nowy = new WydajnoscDzien
                {
                    Data = od,
                    DataOd = od,
                    DataDo = doData,
                    EtykietaOkresu = etykieta,
                    ZywiecKg = g.Sum(d => d.ZywiecKg),
                    ZywiecRwuKg = g.Sum(d => d.ZywiecRwuKg),
                    TuszkaAKg = g.Sum(d => d.TuszkaAKg),
                    TuszkaBKg = g.Sum(d => d.TuszkaBKg),
                    WatrobaKg = g.Sum(d => d.WatrobaKg),
                    ZoladkiKg = g.Sum(d => d.ZoladkiKg),
                    SerceKg = g.Sum(d => d.SerceKg)
                };
                if (nowy.ZywiecKg > 0
                    && Math.Abs((double)nowy.WydajnoscZPodrobamiProc - norma) > tol)
                {
                    nowy.CzyAlert = true;
                    nowy.Uwagi = $"Wydajność {nowy.WydajnoscZPodrobamiProc:F1}% (norma {norma:F0}% ±{tol:F0}%)";
                }
                return nowy;
            }).OrderBy(x => x.DataOd).ToList();
        }

        private void DgTrend_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgTrend.SelectedItem is not WydajnoscDzien d) return;
            var dialog = new Kalendarz1.AnalitykaPelna.Windows.SzczegolyWydajnosciDialog(d)
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();
        }

        private static double SafeDouble(decimal d)
        {
            double v = (double)d;
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            return v;
        }

        // ═══════════════ Bilans materiałowy ═══════════════

        private bool _bmLadowanie;

        private BazaUdzialu AktualnaBaza()
        {
            // Komparator nie używa już radio buttonów — domyślnie liczymy
            // % w tabeli od żywca, a komparator daje precyzyjny wynik
            return BazaUdzialu.Zywiec;
        }

        private void OdswiezBilansMaterialowy()
        {
            try
            {
                _bmLadowanie = true;
                var baza = AktualnaBaza();
                WydajnoscService.PrzeliczProcenty(_ostatniBilansMat, baza);

                var bm = _ostatniBilansMat;

                // ─── DataGrid: ICollectionView z grupowaniem po etap ───
                _bilansPosortowane = bm.Pozycje
                    .OrderBy(p => EtapKolejnosc(p.Etap))
                    .ThenByDescending(p => p.Kg)
                    .ToList();
                _bilansView = System.Windows.Data.CollectionViewSource.GetDefaultView(_bilansPosortowane);
                ZastosujGrupowanie(chkBmGrupowanie?.IsChecked == true);
                dgBilansMaterialowy.ItemsSource = _bilansView;
                ZastosujQuickFiltr();

                _bmLadowanie = false;
            }
            catch (Exception ex)
            {
                if (txtInfo != null)
                    txtInfo.Text = "Bilans materiałowy: " + ex.Message;
            }
            finally
            {
                _bmLadowanie = false;
            }
        }

        // ─── Quick filter / grupowanie / context menu ───

        private List<BilansMaterialowyWiersz> _bilansPosortowane = new();
        private System.ComponentModel.ICollectionView? _bilansView;

        private void TxtBmFiltr_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (txtBmFiltrPlaceholder != null)
                txtBmFiltrPlaceholder.Visibility = string.IsNullOrEmpty(txtBmFiltr.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
            ZastosujQuickFiltr();
        }

        private void ZastosujQuickFiltr()
        {
            if (_bilansView == null) return;
            string txt = (txtBmFiltr?.Text ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(txt))
            {
                _bilansView.Filter = null;
                txtBmFiltrInfo.Text = $"{_bilansPosortowane.Count} pozycji";
            }
            else
            {
                _bilansView.Filter = obj =>
                {
                    if (obj is not BilansMaterialowyWiersz w) return true;
                    return (w.Nazwa ?? "").ToLowerInvariant().Contains(txt)
                        || (w.Kod ?? "").ToLowerInvariant().Contains(txt)
                        || (w.Etap ?? "").ToLowerInvariant().Contains(txt)
                        || (w.Seria ?? "").ToLowerInvariant().Contains(txt)
                        || (w.Kategoria ?? "").ToLowerInvariant().Contains(txt);
                };
                int dopasowane = _bilansPosortowane.Count(w =>
                    (w.Nazwa ?? "").ToLowerInvariant().Contains(txt)
                    || (w.Kod ?? "").ToLowerInvariant().Contains(txt)
                    || (w.Etap ?? "").ToLowerInvariant().Contains(txt)
                    || (w.Seria ?? "").ToLowerInvariant().Contains(txt)
                    || (w.Kategoria ?? "").ToLowerInvariant().Contains(txt));
                txtBmFiltrInfo.Text = $"{dopasowane} z {_bilansPosortowane.Count} pozycji";
            }
            AktualizujBilansFooter();
        }

        /// <summary>Aktualizuje stopkę Bilansu materiałowego — sumy widocznych po filtrach.</summary>
        private void AktualizujBilansFooter()
        {
            if (_bilansView == null || txtBmFooterCount == null) return;
            var widoczne = _bilansView.Cast<BilansMaterialowyWiersz>().ToList();
            decimal sumaKg = widoczne.Sum(w => w.Kg);
            int sumaDok = widoczne.Sum(w => w.LiczbaDokumentow);
            txtBmFooterCount.Text = $"{widoczne.Count:N0} pozycji" +
                (widoczne.Count != _bilansPosortowane.Count ? $" z {_bilansPosortowane.Count:N0}" : "");
            txtBmFooterKg.Text = $"{sumaKg:N0} kg";
            txtBmFooterDok.Text = $"{sumaDok:N0} dok.";
        }

        // ═══════════════════════════════════════════════════════════════════
        // STAN MAGAZYNÓW (sub-tab w Bilansie)
        // ═══════════════════════════════════════════════════════════════════

        private void OdswiezStanMagazynow()
        {
            if (icMagazynyKafelki == null) return;

            // Legacy hidden ItemsControl — pokażmy wszystkie stany (Visibility=Collapsed w XAML)
            icMagazynyKafelki.ItemsSource = _ostatnieStanyMagazynow;

            // ─── Nowa widoczna sekcja: Przepływy MM- (Sankey-style) ─────────
            decimal maxKgVisible = _ostatniePrzeplywy.Count > 0 ? _ostatniePrzeplywy.Max(p => p.Kg) : 0m;
            var przeplywyVisible = _ostatniePrzeplywy
                .Select(p => new
                {
                    p.MagazynZNazwa,
                    p.MagazynDoNazwa,
                    p.Kg,
                    p.LiczbaDok,
                    p.KgFormatted,
                    p.DokFormatted,
                    NumeryDok = "",
                    KgPasek = maxKgVisible <= 0 ? 0.0 : Math.Max(12.0, (double)(p.Kg / maxKgVisible) * 280.0)
                })
                .ToList();
            if (icPrzeplywyVisible != null)
                icPrzeplywyVisible.ItemsSource = przeplywyVisible;
            if (txtPrzeplywyEmptyVisible != null)
                txtPrzeplywyEmptyVisible.Visibility = przeplywyVisible.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;
            if (txtPrzeplywyInfoVisible != null)
                txtPrzeplywyInfoVisible.Text = przeplywyVisible.Count > 0
                    ? $"({przeplywyVisible.Count} kierunków, Σ {_ostatniePrzeplywy.Sum(p => p.Kg):N0} kg, {_ostatniePrzeplywy.Sum(p => p.LiczbaDok):N0} dok.)"
                    : "";

            // Przepływy MM- — z normalizacją szerokości paska (max 200px)
            decimal maxKg = _ostatniePrzeplywy.Count > 0 ? _ostatniePrzeplywy.Max(p => p.Kg) : 0m;
            var przeplywyZPaskiem = _ostatniePrzeplywy
                .Select(p => new
                {
                    p.MagazynZNazwa,
                    p.MagazynDoNazwa,
                    p.Kg,
                    p.LiczbaDok,
                    p.KgFormatted,
                    p.DokFormatted,
                    KgPasek = maxKg <= 0 ? 0.0 : Math.Max(8.0, (double)(p.Kg / maxKg) * 200.0)
                })
                .ToList();
            icPrzeplywy.ItemsSource = przeplywyZPaskiem;
            txtPrzeplywyEmpty.Visibility = przeplywyZPaskiem.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;
            txtPrzeplywyInfo.Text = przeplywyZPaskiem.Count > 0
                ? $"({przeplywyZPaskiem.Count} kierunków, suma {_ostatniePrzeplywy.Sum(p => p.Kg):N0} kg)"
                : "";

            // Tabela rozkład per magazyn × seria — flatten z RozkladSerii
            var rozklad = _ostatnieStanyMagazynow
                .SelectMany(s => s.RozkladSerii.Select(r => new
                {
                    Magazyn = s.MagazynNazwa,
                    Kierunek = r.Kierunek == "IN" ? "⬇ IN" : "⬆ OUT",
                    r.Seria,
                    r.OpisSerii,
                    r.Kg,
                    r.LiczbaDok
                }))
                .OrderBy(x => x.Magazyn)
                .ThenBy(x => x.Kierunek)
                .ThenByDescending(x => x.Kg)
                .ToList();
            dgStanRozklad.ItemsSource = rozklad;
        }

        // ═══════════════════════════════════════════════════════════════════
        // FLOW CHAIN (główna oś produkcji w sub-tab Stan magazynów)
        // ═══════════════════════════════════════════════════════════════════

        private void OdswiezFlowChain()
        {
            if (txtFlowZywiecKg == null) return;
            var fc = _ostatniFlowChain;

            void Set(System.Windows.Controls.TextBlock tbKg, System.Windows.Controls.TextBlock tbDok, FlowChainNode n)
            {
                tbKg.Text = n.KgFormatted;
                tbDok.Text = n.DokFormatted;
            }

            // ─── Główne kafelki ─────────────────────────────────────────────
            Set(txtFlowZywiecKg,  txtFlowZywiecDok,  fc.Zywiec);
            Set(txtFlowUbojKg,    txtFlowUbojDok,    fc.Uboj);
            Set(txtFlowProdKg,    txtFlowProdDok,    fc.Produkcja);
            Set(txtFlowDystKg,    txtFlowDystDok,    fc.Dystrybucja);
            Set(txtFlowKlienciKg, txtFlowKlienciDok, fc.Klienci);
            Set(txtFlowMrozKg,    txtFlowMrozDok,    fc.Mroznia);
            Set(txtFlowKarmaKg,   txtFlowKarmaDok,   fc.Karma);
            Set(txtFlowOdpadyKg,  txtFlowOdpadyDok,  fc.Odpady);

            // ═══════════════════════════════════════════════════════════════
            // STRZAŁKI w łańcuchu — nowy layout (2026-05-09)
            // ═══════════════════════════════════════════════════════════════

            // ─── ARROW 1: ŻYWIEC → UBÓJ ───────────────────────────────────
            // Top: wydajność %  |  Bottom: "ubyło X kg" (smaller font, italic)
            txtArrowZywiecUbojProc.Text = fc.Zywiec.Kg > 0 ? $"{fc.WydajnoscUbojuProc:F1}%" : "—";
            bdArrowZywiecUboj.Background = (System.Windows.Media.Brush)new HexToBrushConverter()
                .Convert(fc.WydajnoscUbojuKolor, typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
            txtArrowZywiecUbojOpis.Text = fc.Zywiec.Kg > 0
                ? $"ubyło {fc.StratyUbojuKg:N0} kg ({fc.StratyUbojuProc:F1}%)"
                : "";

            // ─── ARROW 2: UBÓJ → PROD ─────────────────────────────────────
            // Box z 4 metrykami: rozch (sRWP), przych (sPWP), Δ (różnica), Wyd. (%)
            decimal rozchKroj = fc.RozchodKrojenia.Kg;
            decimal przychKroj = fc.Produkcja.Kg;
            decimal roznicaKroj = rozchKroj - przychKroj;
            txtKrojRozchod.Text = rozchKroj > 0 ? $"{rozchKroj:N0} kg" : "—";
            txtKrojPrzychod.Text = przychKroj > 0 ? $"{przychKroj:N0} kg" : "—";
            txtKrojRoznica.Text = rozchKroj > 0
                ? $"{(roznicaKroj >= 0 ? "-" : "+")}{System.Math.Abs(roznicaKroj):N0} kg"
                : "—";
            txtArrowUbojProdProc.Text = rozchKroj > 0 ? $"{fc.WydajnoscKrojeniaProc:F1}%" : "—";

            // ─── ARROW 3: PROD → DYST ─────────────────────────────────────
            // Top: "📦 250,000 kg" (kg na DYST)  |  Bottom: "X.X% z PROD"
            txtArrowProdDystProc.Text = fc.Dystrybucja.Kg > 0 ? $"{fc.Dystrybucja.Kg:N0} kg" : "—";
            txtArrowProdDystOpis.Text = fc.Produkcja.Kg > 0
                ? $"{fc.ProcDoDystProc:F1}% z PROD"
                : "";

            // ─── ARROW 4: DYST → KLIENCI ──────────────────────────────────
            // Top: "🚚 99.2%"  |  Bottom: "sprzedano X kg"
            txtArrowDystKlienciProc.Text = fc.Dystrybucja.Kg > 0 ? $"{fc.ProcSprzedanoProc:F1}%" : "—";
            txtArrowDystKlienciOpis.Text = fc.Klienci.Kg > 0
                ? $"sprzedano {fc.Klienci.Kg:N0} kg"
                : "";

            // ─── BRANCHES pod DYST (% bazują na PROD jako baza, bo MM- z PROD) ─
            txtFlowMrozProc.Text   = fc.Produkcja.Kg > 0 ? $"{fc.ProcDoMrozniProc:F1}%" : "—";
            txtFlowMasarProc.Text  = fc.Produkcja.Kg > 0 ? $"{(fc.Masarnia.Kg / fc.Produkcja.Kg * 100m):F1}%" : "—";
            txtFlowKarmaProc.Text  = fc.Produkcja.Kg > 0 ? $"{fc.ProcDoKarmyProc:F1}%" : "—";
            txtFlowOdpadyProc.Text = fc.Produkcja.Kg > 0 ? $"{fc.ProcDoOdpadowProc:F1}%" : "—";

            // ─── MASARNIA (nowy branch) ───────────────────────────────────
            txtFlowMasarKg.Text = fc.Masarnia.KgFormatted;
            txtFlowMasarDok.Text = fc.Masarnia.DokFormatted;

            // ─── KPI Hero Strip ────────────────────────────────────────────
            txtKpiZywiecKg.Text   = fc.Zywiec.KgFormatted;
            txtKpiZywiecDok.Text  = fc.Zywiec.DokFormatted;
            txtKpiKlienciKg.Text  = fc.Klienci.KgFormatted;
            txtKpiKlienciDok.Text = fc.Klienci.DokFormatted;
            txtKpiDokCalk.Text    = fc.LiczbaDokumentowCalkowita.ToString("N0");

            txtKpiWydUboju.Text         = fc.Zywiec.Kg > 0 ? fc.WydajnoscUbojuProc.ToString("F1") : "—";
            txtKpiWydUbojuStatus.Text   = fc.WydajnoscUbojuStatus;
            bdKpiWydUbojuStatus.Background = (System.Windows.Media.Brush)new HexToBrushConverter()
                .Convert(fc.WydajnoscUbojuKolor, typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture);

            txtKpiWydKrojenia.Text         = fc.Uboj.Kg > 0 ? fc.WydajnoscKrojeniaProc.ToString("F1") : "—";
            txtKpiWydKrojeniaStatus.Text   = fc.WydajnoscKrojeniaStatus;
            bdKpiWydKrojeniaStatus.Background = (System.Windows.Media.Brush)new HexToBrushConverter()
                .Convert(fc.WydajnoscKrojeniaKolor, typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture);

            txtKpiStraty.Text   = fc.Zywiec.Kg > 0 ? fc.StratyUbojuProc.ToString("F1") : "—";
            txtKpiStratyKg.Text = fc.Zywiec.Kg > 0 ? $"{fc.StratyUbojuKg:N0} kg" : "";

            // ─── Mini-listy top towarów w kafelkach UBÓJ + PROD (2-kolumnowy układ) ──
            if (icUbojTopTowary != null && bdUbojTopTowary != null)
            {
                if (_ostatnieUbojTopTowary.Count > 0)
                {
                    icUbojTopTowary.ItemsSource = _ostatnieUbojTopTowary;
                    bdUbojTopTowary.Visibility = Visibility.Visible;
                }
                else
                {
                    icUbojTopTowary.ItemsSource = null;
                    bdUbojTopTowary.Visibility = Visibility.Collapsed;
                }
            }
            if (icProdTopTowary != null && bdProdTopTowary != null)
            {
                if (_ostatnieProdTopTowary.Count > 0)
                {
                    icProdTopTowary.ItemsSource = _ostatnieProdTopTowary;
                    bdProdTopTowary.Visibility = Visibility.Visible;
                }
                else
                {
                    icProdTopTowary.ItemsSource = null;
                    bdProdTopTowary.Visibility = Visibility.Collapsed;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // TOWARY WYPRODUKOWANE (lista kart w sub-tab Stan magazynów)
        // ═══════════════════════════════════════════════════════════════════

        private void OdswiezTowaryProdukcji()
        {
            if (icTowaryProdukcji == null) return;

            // Wypełnij ComboBox kategorii (raz, jeśli pusty)
            if (cbTowaryKategoria != null && cbTowaryKategoria.Items.Count == 0)
            {
                cbTowaryKategoria.Items.Clear();
                cbTowaryKategoria.Items.Add("Wszystkie kategorie");
                foreach (var kat in _ostatnieTowaryProdukcji
                    .Select(t => t.Kategoria).Distinct().OrderBy(k => k))
                {
                    cbTowaryKategoria.Items.Add(kat);
                }
                cbTowaryKategoria.SelectedIndex = 0;
            }

            // Filtry
            bool tylkoZSaldem = chkTowaryTylkoZSaldem?.IsChecked == true;
            string? wybranaKat = cbTowaryKategoria?.SelectedItem as string;
            bool filtrKategorii = !string.IsNullOrEmpty(wybranaKat) && wybranaKat != "Wszystkie kategorie";

            var towary = _ostatnieTowaryProdukcji
                .Where(t => !tylkoZSaldem || t.SaldoProcent >= 5m)
                .Where(t => !filtrKategorii || t.Kategoria == wybranaKat)
                .ToList();

            icTowaryProdukcji.ItemsSource = towary;

            // Empty state + info
            if (txtTowaryEmpty != null)
                txtTowaryEmpty.Visibility = towary.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;
            if (txtTowaryInfo != null)
            {
                decimal sumKg = towary.Sum(t => t.WyprodukowanoKg);
                txtTowaryInfo.Text = towary.Count == 0
                    ? ""
                    : $"({towary.Count} towarów, Σ {sumKg:N0} kg wyprodukowane)";
            }
        }

        private void ChkTowaryTylkoZSaldem_Toggle(object sender, RoutedEventArgs e)
        {
            OdswiezTowaryProdukcji();
        }

        // ═══════════════════════════════════════════════════════════════════
        // Fullscreen toggle dla sekcji łańcucha produkcji — ukrywa siostry sekcje
        // ═══════════════════════════════════════════════════════════════════

        private bool _chainFullscreen;

        private void BtnChainFullscreen_Click(object sender, RoutedEventArgs e)
        {
            _chainFullscreen = !_chainFullscreen;
            var hide = _chainFullscreen ? Visibility.Collapsed : Visibility.Visible;
            if (bdTowaryWyprodukowaneSekcja != null) bdTowaryWyprodukowaneSekcja.Visibility = hide;
            if (bdSankeySekcja != null) bdSankeySekcja.Visibility = hide;
            if (btnChainFullscreen != null)
            {
                btnChainFullscreen.Content = _chainFullscreen ? "🗗" : "⛶";
                btnChainFullscreen.ToolTip = _chainFullscreen
                    ? "Wyjdź z trybu pełnoekranowego (Esc)"
                    : "Powiększ łańcuch produkcji do pełnej szerokości karty (Esc — wyjdź)";
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Klikanie kafelka łańcucha → otwiera FlowChainEtapDialog z pełnymi szczegółami
        // ═══════════════════════════════════════════════════════════════════

        private async void FlowCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement el || el.Tag is not string etap) return;
            if (string.IsNullOrEmpty(etap)) return;

            try
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                var f = AktualnyFiltrSafe();
                if (f == null)
                {
                    Mouse.OverrideCursor = null;
                    System.Windows.MessageBox.Show("Najpierw wybierz zakres dat i kliknij Zastosuj.",
                        "Brak danych", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var detail = await _service.LoadFlowChainEtapDetailAsync(etap, f);
                Mouse.OverrideCursor = null;

                var dlg = new Windows.FlowChainEtapDialog(detail)
                {
                    Owner = Window.GetWindow(this)
                };
                dlg.ShowDialog();
            }
            catch (System.Exception ex)
            {
                Mouse.OverrideCursor = null;
                System.Windows.MessageBox.Show("Błąd ładowania szczegółów etapu: " + ex.Message,
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Zwraca aktualny filtr — używa _ostatnieFiltry (ustawiane przez ZastosujFiltryAsync).</summary>
        private FiltryAnaliz? AktualnyFiltrSafe()
        {
            // Wymagamy żeby co najmniej raz wczytano dane (DataDo > MinValue oznacza że ZastosujFiltryAsync działało)
            if (_ostatnieFiltry == null || _ostatnieFiltry.DataDo == default) return null;
            return _ostatnieFiltry;
        }

        private void CbTowaryKategoria_Changed(object sender, SelectionChangedEventArgs e)
        {
            OdswiezTowaryProdukcji();
        }

        private void BtnBmWyczyscFiltr_Click(object sender, RoutedEventArgs e)
        {
            if (txtBmFiltr != null) txtBmFiltr.Text = "";
            ZastosujQuickFiltr();
        }

        private void ChkBmGrupowanie_Toggle(object sender, RoutedEventArgs e)
        {
            if (_bilansView == null) return;
            ZastosujGrupowanie(chkBmGrupowanie?.IsChecked == true);
        }

        private void ZastosujGrupowanie(bool wlacz)
        {
            if (_bilansView == null) return;
            _bilansView.GroupDescriptions.Clear();
            if (wlacz)
                _bilansView.GroupDescriptions.Add(
                    new System.Windows.Data.PropertyGroupDescription(nameof(BilansMaterialowyWiersz.Etap)));
        }

        // ─── Context menu handlery ───

        private void MenuFiltruyTowar_Click(object sender, RoutedEventArgs e)
        {
            if (dgBilansMaterialowy.SelectedItem is BilansMaterialowyWiersz w)
            {
                txtBmFiltr.Text = w.Nazwa;
            }
        }

        private void MenuFiltruyEtap_Click(object sender, RoutedEventArgs e)
        {
            if (dgBilansMaterialowy.SelectedItem is BilansMaterialowyWiersz w)
            {
                txtBmFiltr.Text = w.Etap;
            }
        }

        private void MenuFiltruySeria_Click(object sender, RoutedEventArgs e)
        {
            if (dgBilansMaterialowy.SelectedItem is BilansMaterialowyWiersz w)
            {
                txtBmFiltr.Text = w.Seria;
            }
        }

        private void MenuSkopiujKg_Click(object sender, RoutedEventArgs e)
        {
            if (dgBilansMaterialowy.SelectedItem is BilansMaterialowyWiersz w)
            {
                try { System.Windows.Clipboard.SetText(w.Kg.ToString("N0")); }
                catch { /* clipboard może być zajęty */ }
            }
        }

        private void MenuSkopiujWiersz_Click(object sender, RoutedEventArgs e)
        {
            if (dgBilansMaterialowy.SelectedItem is BilansMaterialowyWiersz w)
            {
                try
                {
                    string csv = $"{w.Etap};{w.Kategoria};{w.Nazwa};{w.Kod};{w.Seria};" +
                                 $"{w.Kg.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)};" +
                                 $"{w.ProcentBazy.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";
                    System.Windows.Clipboard.SetText(csv);
                }
                catch { }
            }
        }

        // ═══ Tabela klas — 2 DataGridy + 3 panele Σ ═══

        private void WypelnijTabeleKlas(List<WydajnoscKlasa> klasy)
        {
            var duzy = klasy.Where(k => k.Klasa is >= 4 and <= 7).OrderBy(k => k.Klasa).ToList();
            var maly = klasy.Where(k => k.Klasa is >= 8 and <= 12).OrderBy(k => k.Klasa).ToList();

            dgKlasyDuzy.ItemsSource = duzy;
            dgKlasyMaly.ItemsSource = maly;

            // Σ Duży — agregaty
            int wD = duzy.Sum(k => k.LiczbaWazen);
            decimal kgD = duzy.Sum(k => k.SumaActWeightKg);
            decimal sD = wD <= 0 ? 0 : kgD / wD;
            decimal udD = duzy.Sum(k => k.ProcentUdzialu);
            txtSumDuzyWazen.Text = wD.ToString("N0");
            txtSumDuzyKg.Text = kgD.ToString("N0");
            txtSumDuzySrednia.Text = sD.ToString("N1");
            pbSumDuzy.Value = (double)udD;
            txtSumDuzyProc.Text = udD.ToString("N1") + "%";

            // Σ Mały
            int wM = maly.Sum(k => k.LiczbaWazen);
            decimal kgM = maly.Sum(k => k.SumaActWeightKg);
            decimal sM = wM <= 0 ? 0 : kgM / wM;
            decimal udM = maly.Sum(k => k.ProcentUdzialu);
            txtSumMalyWazen.Text = wM.ToString("N0");
            txtSumMalyKg.Text = kgM.ToString("N0");
            txtSumMalySrednia.Text = sM.ToString("N1");
            pbSumMaly.Value = (double)udM;
            txtSumMalyProc.Text = udM.ToString("N1") + "%";

            // Σ RAZEM
            int wR = wD + wM;
            decimal kgR = kgD + kgM;
            decimal sR = wR <= 0 ? 0 : kgR / wR;
            txtSumRazemWazen.Text = wR.ToString("N0");
            txtSumRazemKg.Text = kgR.ToString("N0");
            txtSumRazemSrednia.Text = sR.ToString("N1");
        }

        // ═══ Wiersze podsumowania w tabeli klas (legacy — nie używane przy nowym layout) ═══

        private static List<WydajnoscKlasa> DodajPodsumowania(List<WydajnoscKlasa> klasy)
        {
            var wynik = new List<WydajnoscKlasa>();

            var duzy = klasy.Where(k => k.Klasa is >= 4 and <= 7 && !k.JestPodsumowaniem).ToList();
            var maly = klasy.Where(k => k.Klasa is >= 8 and <= 12 && !k.JestPodsumowaniem).ToList();

            // Najpierw nagłówek Σ Duży, potem klasy 4-7
            if (duzy.Count > 0)
            {
                wynik.Add(StworzPodsumowanie("🍗 Σ DUŻY KURCZAK (klasy 4–7)", duzy, "Duzy"));
                wynik.AddRange(duzy);
            }

            // Potem nagłówek Σ Mały, potem klasy 8-12
            if (maly.Count > 0)
            {
                wynik.Add(StworzPodsumowanie("🐥 Σ MAŁY KURCZAK (klasy 8–12)", maly, "Maly"));
                wynik.AddRange(maly);
            }

            // Na samym końcu — RAZEM (suma wszystkich)
            var wszystkie = duzy.Concat(maly).ToList();
            if (wszystkie.Count > 0)
                wynik.Add(StworzPodsumowanie("📊 Σ RAZEM (4–12)", wszystkie, "Razem"));

            return wynik;
        }

        private static WydajnoscKlasa StworzPodsumowanie(string nazwa, List<WydajnoscKlasa> grupa, string typ)
        {
            int sumaWazen = grupa.Sum(k => k.LiczbaWazen);
            decimal sumaKg = grupa.Sum(k => k.SumaActWeightKg);
            return new WydajnoscKlasa
            {
                JestPodsumowaniem = true,
                NazwaPodsumowania = nazwa,
                TypPodsumowania = typ,
                Klasa = 0,
                LiczbaWazen = sumaWazen,
                SumaActWeightKg = sumaKg,
                MinWagaSzt = grupa.Count == 0 ? 0 : grupa.Min(k => k.MinWagaSzt),
                MaxWagaSzt = grupa.Count == 0 ? 0 : grupa.Max(k => k.MaxWagaSzt),
                ProcentUdzialu = grupa.Sum(k => k.ProcentUdzialu)
            };
        }

        // ═══ KPI Klas wagowych: Duży vs Mały ═══

        private void AktualizujKpiKlas()
        {
            if (_ostatnieKlasy.Count == 0)
            {
                txtKlasyKpiDuzy.Text = "0 kg";
                txtKlasyKpiDuzyDetal.Text = "";
                txtKlasyKpiDuzyProc.Text = "—";
                txtKlasyKpiMaly.Text = "0 kg";
                txtKlasyKpiMalyDetal.Text = "";
                txtKlasyKpiMalyProc.Text = "—";
                txtKlasyStats.Text = "Brak danych klas wagowych w wybranym zakresie";
                return;
            }

            var duzy = _ostatnieKlasy.Where(k => k.Klasa is >= 4 and <= 7).ToList();
            var maly = _ostatnieKlasy.Where(k => k.Klasa is >= 8 and <= 12).ToList();
            decimal sumaDuzy = duzy.Sum(k => k.SumaActWeightKg);
            decimal sumaMaly = maly.Sum(k => k.SumaActWeightKg);
            int wazenDuzy = duzy.Sum(k => k.LiczbaWazen);
            int wazenMaly = maly.Sum(k => k.LiczbaWazen);
            decimal calosc = sumaDuzy + sumaMaly;
            decimal procDuzy = calosc <= 0 ? 0 : sumaDuzy / calosc * 100m;
            decimal procMaly = calosc <= 0 ? 0 : sumaMaly / calosc * 100m;
            decimal srWagaDuzy = wazenDuzy <= 0 ? 0 : sumaDuzy / wazenDuzy;
            decimal srWagaMaly = wazenMaly <= 0 ? 0 : sumaMaly / wazenMaly;

            txtKlasyKpiDuzy.Text = sumaDuzy.ToString("N0") + " kg";
            txtKlasyKpiDuzyDetal.Text = $"{wazenDuzy:N0} szt. • śr. {srWagaDuzy:N3} kg/szt.";
            txtKlasyKpiDuzyProc.Text = procDuzy.ToString("N1") + "%";

            txtKlasyKpiMaly.Text = sumaMaly.ToString("N0") + " kg";
            txtKlasyKpiMalyDetal.Text = $"{wazenMaly:N0} szt. • śr. {srWagaMaly:N3} kg/szt.";
            txtKlasyKpiMalyProc.Text = procMaly.ToString("N1") + "%";

            int sumaWazen = wazenDuzy + wazenMaly;
            var topKlasa = _ostatnieKlasy.OrderByDescending(k => k.SumaActWeightKg).FirstOrDefault();
            txtKlasyStats.Text =
                $"⚖ {_ostatnieKlasy.Count} klas (po wykluczeniu klasy 1) • " +
                $"{sumaWazen:N0} ważeń • {calosc:N0} kg • " +
                $"dominująca: klasa {topKlasa?.Klasa} ({topKlasa?.ProcentUdzialu:F1}%)";
        }

        // ═══ Wykres klas wagowych — 2 serie (Duży niebieski / Mały pomarańczowy) ═══

        private void BudujWykresKlas(List<WydajnoscKlasa> klasy)
        {
            if (osYKlasy != null)
                osYKlasy.LabelFormatter = v => v.ToString("N0");

            if (klasy == null || klasy.Count == 0)
            {
                wykresKlasy.Series = new SeriesCollection();
                osXKlasy.Labels = new List<string>();
                return;
            }

            // Tylko klasy 4-12 do wykresu (Service już je takie zwraca, ale defensywnie).
            var klasyWykres = klasy.Where(k => k.Klasa is >= 4 and <= 12).ToList();
            if (klasyWykres.Count == 0)
            {
                wykresKlasy.Series = new SeriesCollection();
                osXKlasy.Labels = new List<string>();
                return;
            }

            // Każda klasa = 1 słupek. Zamiast 2 serii ze sobą obok, używamy
            // 1 serii ColumnSeries i kolorujemy słupki per ChartPoint przez
            // PointForeground/Configuration. LiveCharts.Wpf 0.9.7 nie pozwala
            // łatwo na per-bar fill, więc ROBIMY 2 serie i wartości
            // 0 (nie NaN!) dla klas spoza grupy. Słupek o wysokości 0 jest niewidoczny,
            // a etykieta jest pominięta przez LabelPoint.

            var serieDuzy = new ChartValues<double>(klasyWykres.Select(k =>
                k.Klasa is >= 4 and <= 7 ? SafeDouble(k.SumaActWeightKg) : 0.0));
            var serieMaly = new ChartValues<double>(klasyWykres.Select(k =>
                k.Klasa is >= 8 and <= 12 ? SafeDouble(k.SumaActWeightKg) : 0.0));

            // Suma kg wszystkich klas 4–12 (do liczenia % udziału per słupek)
            double sumaKgWszystkich = klasyWykres.Sum(k => SafeDouble(k.SumaActWeightKg));

            wykresKlasy.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "🍗 Duży kurczak (klasy 4-7)",
                    Values = serieDuzy,
                    Fill = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x8A)),
                    StrokeThickness = 1,
                    DataLabels = true,
                    LabelPoint = p =>
                    {
                        if (p.Y <= 0) return "";
                        double proc = sumaKgWszystkich > 0 ? p.Y / sumaKgWszystkich * 100.0 : 0;
                        return $"{p.Y:N0} kg\n({proc:N1}%)";
                    },
                    MaxColumnWidth = 60,
                    ColumnPadding = 4,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x8A))
                },
                new ColumnSeries
                {
                    Title = "🐥 Mały kurczak (klasy 8-12)",
                    Values = serieMaly,
                    Fill = new SolidColorBrush(Color.FromRgb(0xFB, 0x92, 0x3C)),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x9A, 0x34, 0x12)),
                    StrokeThickness = 1,
                    DataLabels = true,
                    LabelPoint = p =>
                    {
                        if (p.Y <= 0) return "";
                        double proc = sumaKgWszystkich > 0 ? p.Y / sumaKgWszystkich * 100.0 : 0;
                        return $"{p.Y:N0} kg\n({proc:N1}%)";
                    },
                    MaxColumnWidth = 60,
                    ColumnPadding = 4,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x34, 0x12))
                }
            };

            osXKlasy.Labels = klasyWykres.Select(k => "Klasa " + k.Klasa).ToList();
        }

        // ═══ Trend — filter "tylko alerty" ═══

        private System.ComponentModel.ICollectionView? _trendView;

        private void ChkTrendTylkoAlerty_Toggle(object sender, RoutedEventArgs e) => ZastosujTrendFiltr();

        private void ZastosujTrendFiltr()
        {
            if (_trendView == null) return;
            bool tylkoAlerty = chkTrendTylkoAlerty?.IsChecked == true;
            _trendView.Filter = tylkoAlerty
                ? obj => obj is WydajnoscDzien d && d.CzyAlert
                : (Predicate<object>?)null;
        }

        // ═══ Hodowcy — quick filter ═══

        private System.ComponentModel.ICollectionView? _hodowcyView;

        private void TxtHodowcyFiltr_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (txtHodowcyFiltrPlaceholder != null)
                txtHodowcyFiltrPlaceholder.Visibility = string.IsNullOrEmpty(txtHodowcyFiltr.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
            ZastosujHodowcyFiltr();
        }

        private void ZastosujHodowcyFiltr()
        {
            if (_hodowcyView == null) return;
            string txt = (txtHodowcyFiltr?.Text ?? "").Trim().ToLowerInvariant();
            _hodowcyView.Filter = string.IsNullOrEmpty(txt) ? null : obj =>
                obj is WydajnoscHodowca h
                    && ((h.Hodowca ?? "").ToLowerInvariant().Contains(txt)
                     || (h.CustomerID ?? "").ToLowerInvariant().Contains(txt));
        }

        private void BtnHodowcyWyczyscFiltr_Click(object sender, RoutedEventArgs e)
        {
            if (txtHodowcyFiltr != null) txtHodowcyFiltr.Text = "";
            ZastosujHodowcyFiltr();
        }

        private void MenuFiltruyHodowca_Click(object sender, RoutedEventArgs e)
        {
            if (dgHodowcy.SelectedItem is WydajnoscHodowca h)
                txtHodowcyFiltr.Text = h.Hodowca;
        }

        private void MenuSkopiujHodowcaNazwa_Click(object sender, RoutedEventArgs e)
        {
            if (dgHodowcy.SelectedItem is WydajnoscHodowca h)
                try { System.Windows.Clipboard.SetText(h.Hodowca); } catch { }
        }

        private void MenuSkopiujHodowcaKg_Click(object sender, RoutedEventArgs e)
        {
            if (dgHodowcy.SelectedItem is WydajnoscHodowca h)
                try { System.Windows.Clipboard.SetText(h.SumaWyjscieKg.ToString("N0")); } catch { }
        }

        // ═══ Elementy — quick filter + grupowanie ═══

        private System.ComponentModel.ICollectionView? _elementyView;

        // Debouncing filtra tekstowego — zamiast filtrować przy każdym znaku, czekamy 250ms.
        // Przy dużych zbiorach (10k+ pozycji) to drastycznie poprawia responsywność.
        private System.Windows.Threading.DispatcherTimer? _elementyFiltrTimer;

        private void TxtElementyFiltr_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (txtElementyFiltrPlaceholder != null)
                txtElementyFiltrPlaceholder.Visibility = string.IsNullOrEmpty(txtElementyFiltr.Text)
                    ? Visibility.Visible : Visibility.Collapsed;

            // Reset timera — filtruj dopiero 250ms po ostatnim znaku
            if (_elementyFiltrTimer == null)
            {
                _elementyFiltrTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(250)
                };
                _elementyFiltrTimer.Tick += (_, _) =>
                {
                    _elementyFiltrTimer!.Stop();
                    ZastosujElementyFiltr();
                };
            }
            _elementyFiltrTimer.Stop();
            _elementyFiltrTimer.Start();
        }

        // ─── Filtry interaktywne (kategorie + serie) — kombinują się z filtrem tekstowym ───
        private readonly HashSet<string> _wybraneKategoriaEl = new();
        private readonly HashSet<string> _wybraneSerieEl = new();

        private void ZastosujElementyFiltr()
        {
            if (_elementyView == null) return;
            string txt = (txtElementyFiltr?.Text ?? "").Trim().ToLowerInvariant();
            bool brakKategFilter = _wybraneKategoriaEl.Count == 0;
            bool brakSerieFilter = _wybraneSerieEl.Count == 0;
            bool tylkoZKrojeniem = chkElTylkoZKrojeniem?.IsChecked == true;

            _elementyView.Filter = obj =>
            {
                if (obj is not WydajnoscSzczegolElement el) return false;
                // Filtr tylko z krojeniem
                if (tylkoZKrojeniem && el.Krojenie <= 0) return false;
                // Filtr tekstowy
                if (!string.IsNullOrEmpty(txt))
                {
                    bool match = (el.NazwaTowaru ?? "").ToLowerInvariant().Contains(txt)
                              || (el.KodTowaru ?? "").ToLowerInvariant().Contains(txt)
                              || (el.Kategoria ?? "").ToLowerInvariant().Contains(txt)
                              || (el.Seria ?? "").ToLowerInvariant().Contains(txt);
                    if (!match) return false;
                }
                // Filtr kategorii
                if (!brakKategFilter && !_wybraneKategoriaEl.Contains(el.Kategoria ?? "")) return false;
                // Filtr serii
                if (!brakSerieFilter && !_wybraneSerieEl.Contains(el.Seria ?? "")) return false;
                return true;
            };

            // Po zmianie filtru — KPI, stopka i wykresy przeliczają się na widocznych pozycjach.
            // Aby uniknąć "podwójnego enumerowania" CollectionView (kosztowne dla 10k+ rekordów),
            // bufujemy widoczne raz i przekazujemy do wszystkich konsumentów.
            var widoczne = _elementyView == null
                ? _ostatnieElementy
                : _elementyView.Cast<WydajnoscSzczegolElement>().ToList();
            AktualizujElementyKpi(widoczne);
            AktualizujElementyFooter(widoczne);
            BudujWykresElementy(widoczne);
        }

        // ─── Toggle button kategorii (Eksplorator-style: klik=jedna, Ctrl+klik=multi) ───
        private void ElKategoriaBtn_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.Primitives.ToggleButton tb || tb.Tag is not string tag) return;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (ctrl)
            {
                if (_wybraneKategoriaEl.Contains(tag)) _wybraneKategoriaEl.Remove(tag);
                else _wybraneKategoriaEl.Add(tag);
            }
            else
            {
                bool wasOnly = _wybraneKategoriaEl.Count == 1 && _wybraneKategoriaEl.Contains(tag);
                _wybraneKategoriaEl.Clear();
                if (!wasOnly) _wybraneKategoriaEl.Add(tag);
            }
            AktualizujKategoriaToggle();
            ZastosujElementyFiltr();
            e.Handled = true;
        }

        private void AktualizujKategoriaToggle()
        {
            kategMieso.IsChecked = _wybraneKategoriaEl.Contains("Mięso");
            kategMrozony.IsChecked = _wybraneKategoriaEl.Contains("Mrozony");
            kategZywy.IsChecked = _wybraneKategoriaEl.Contains("Zywy");
            kategOdpady.IsChecked = _wybraneKategoriaEl.Contains("Odpady");
            kategInne.IsChecked = _wybraneKategoriaEl.Contains("Inne");
        }

        private void ElSeriaBtn_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.Primitives.ToggleButton tb || tb.Tag is not string tag) return;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (ctrl)
            {
                if (_wybraneSerieEl.Contains(tag)) _wybraneSerieEl.Remove(tag);
                else _wybraneSerieEl.Add(tag);
            }
            else
            {
                bool wasOnly = _wybraneSerieEl.Count == 1 && _wybraneSerieEl.Contains(tag);
                _wybraneSerieEl.Clear();
                if (!wasOnly) _wybraneSerieEl.Add(tag);
            }
            AktualizujSerieToggle();
            ZastosujElementyFiltr();
            e.Handled = true;
        }

        private void AktualizujSerieToggle()
        {
            foreach (var child in panelSerie.Children)
                if (child is System.Windows.Controls.Primitives.ToggleButton tb && tb.Tag is string tag)
                    tb.IsChecked = _wybraneSerieEl.Contains(tag);
        }

        private void BudujFiltrySerii()
        {
            panelSerie.Children.Clear();
            var serie = _ostatnieElementy.Select(e => e.Seria).Where(s => !string.IsNullOrEmpty(s))
                .Distinct().OrderBy(s => s).ToList();
            // TryFindResource — bezpieczniej niż FindResource (zwraca null zamiast rzucać/UnsetValue)
            var styleObj = TryFindResource("KategoriaToggleStyle");
            var style = styleObj as Style;  // jeśli zasób nie istnieje, style będzie null → użyjemy domyślnego wyglądu

            foreach (var seria in serie)
            {
                var tb = new System.Windows.Controls.Primitives.ToggleButton
                {
                    Content = "📋  " + seria,
                    Tag = seria
                };
                if (style != null)
                {
                    tb.Style = style;
                }
                else
                {
                    // Fallback styling jeśli zasób nie znaleziony — ToggleButton dostaje domyślny wygląd WPF
                    tb.Padding = new Thickness(10, 7, 10, 7);
                    tb.Margin = new Thickness(0, 0, 0, 4);
                    tb.Cursor = System.Windows.Input.Cursors.Hand;
                    tb.HorizontalContentAlignment = HorizontalAlignment.Left;
                    tb.FontWeight = FontWeights.SemiBold;
                }
                tb.PreviewMouseLeftButtonDown += ElSeriaBtn_PreviewMouseDown;
                panelSerie.Children.Add(tb);
            }
        }

        private void BtnElResetFiltry_Click(object sender, RoutedEventArgs e)
        {
            if (txtElementyFiltr != null) txtElementyFiltr.Text = "";
            _wybraneKategoriaEl.Clear();
            _wybraneSerieEl.Clear();
            AktualizujKategoriaToggle();
            AktualizujSerieToggle();
            ZastosujElementyFiltr();
        }

        private void AktualizujElementyKpi(List<WydajnoscSzczegolElement>? buffer = null)
        {
            // Liczymy tylko widoczne (po wszystkich filtrach). Przyjmujemy bufor jeśli już jest
            // przeliczony z innego miejsca, w przeciwnym razie enumerujemy CollectionView.
            var widoczne = buffer ?? (_elementyView == null
                ? _ostatnieElementy
                : _elementyView.Cast<WydajnoscSzczegolElement>().ToList());

            decimal sumaPrz = widoczne.Sum(e => e.Przychod);
            decimal sumaKr = widoczne.Sum(e => e.Krojenie);
            int liczbaTow = widoczne.Select(e => e.KodTowaru).Where(k => !string.IsNullOrEmpty(k)).Distinct().Count();
            int liczbaKat = widoczne.Select(e => e.Kategoria).Where(k => !string.IsNullOrEmpty(k)).Distinct().Count();

            txtElKpiPrzychod.Text = sumaPrz.ToString("N0");
            txtElKpiPrzychodSub.Text = $"kg • {widoczne.Count:N0} pozycji";
            txtElKpiKrojenie.Text = sumaKr.ToString("N0");
            txtElKpiKrojenieSub.Text = sumaPrz > 0 ? $"kg • {sumaKr / sumaPrz * 100m:F1}% przychodu" : "kg";
            txtElKpiKategorii.Text = liczbaKat.ToString();
            txtElKpiTowarow.Text = $"{liczbaTow} towarów";

            // % Wydajność — Krojenie/Przychód × 100 (kluczowa metryka)
            if (sumaPrz > 0)
            {
                decimal wydaj = sumaKr / sumaPrz * 100m;
                txtElKpiWydaj.Text = $"{wydaj:F1}%";
                // Kolor zależny od wartości: < 30% czerwony, 30-60% pomarańczowy, > 60% zielony
                txtElKpiWydaj.Foreground = WydajnoscKolor(wydaj);
                txtElKpiWydajSub.Text = $"= {sumaKr:N0} / {sumaPrz:N0}";
            }
            else
            {
                txtElKpiWydaj.Text = "—";
                txtElKpiWydajSub.Text = "brak przychodu";
            }

            // Top kategoria — najwięcej krojenia
            var topKat = widoczne
                .Where(e => !string.IsNullOrEmpty(e.Kategoria))
                .GroupBy(e => e.Kategoria)
                .Select(g => new { Kat = g.Key, Kg = g.Sum(e => e.Krojenie) })
                .OrderByDescending(x => x.Kg)
                .FirstOrDefault();
            if (topKat != null && topKat.Kg > 0)
            {
                txtElKpiTop.Text = topKat.Kat;
                txtElKpiTopProc.Text = sumaKr > 0 ? $"{topKat.Kg / sumaKr * 100m:F1}% krojenia • {topKat.Kg:N0} kg" : $"{topKat.Kg:N0} kg";
            }
            else
            {
                txtElKpiTop.Text = "—";
                txtElKpiTopProc.Text = "";
            }
        }

        private void AktualizujElementyFooter(List<WydajnoscSzczegolElement> widoczne)
        {
            decimal sumaPrz = widoczne.Sum(e => e.Przychod);
            decimal sumaKr = widoczne.Sum(e => e.Krojenie);
            txtElFooterCount.Text = $"{widoczne.Count:N0} pozycji" +
                (widoczne.Count != _ostatnieElementy.Count ? $" z {_ostatnieElementy.Count:N0}" : "");
            txtElFooterPrz.Text = $"{sumaPrz:N0} kg";
            txtElFooterKr.Text = $"Krojenie: {sumaKr:N0} kg";
            txtElFooterWyd.Text = sumaPrz > 0 ? $"Wyd. {sumaKr / sumaPrz * 100m:F1}%" : "Wyd. —";
        }

        private static readonly Dictionary<string, System.Windows.Media.Color> KOLORY_KAT = new()
        {
            { "Mięso", System.Windows.Media.Color.FromRgb(0x05, 0x96, 0x69) },
            { "Mrozony", System.Windows.Media.Color.FromRgb(0x08, 0x91, 0xB2) },
            { "Zywy", System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B) },
            { "Odpady", System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8) },
            { "Inne", System.Windows.Media.Color.FromRgb(0x7C, 0x3A, 0xED) }
        };

        private void BudujWykresElementy(List<WydajnoscSzczegolElement>? buffer = null)
        {
            var widoczne = buffer ?? (_elementyView == null
                ? _ostatnieElementy
                : _elementyView.Cast<WydajnoscSzczegolElement>().ToList());

            // Donut: rozkład kategorii (kg krojenia)
            var perKat = widoczne
                .Where(e => !string.IsNullOrEmpty(e.Kategoria) && e.Krojenie > 0)
                .GroupBy(e => e.Kategoria)
                .Select(g => new { Kat = g.Key, Kg = g.Sum(e => e.Krojenie) })
                .OrderByDescending(x => x.Kg)
                .ToList();

            var donut = new SeriesCollection();
            foreach (var p in perKat)
            {
                var color = KOLORY_KAT.TryGetValue(p.Kat, out var c)
                    ? c : System.Windows.Media.Color.FromRgb(0x64, 0x74, 0x8B);
                donut.Add(new PieSeries
                {
                    Title = p.Kat,
                    Values = new ChartValues<double> { (double)p.Kg },
                    Fill = new System.Windows.Media.SolidColorBrush(color),
                    StrokeThickness = 0,
                    DataLabels = true,
                    LabelPoint = chartPoint => $"{chartPoint.Y:N0} kg ({chartPoint.Participation:P1})",
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                });
            }
            wykresElKategorii.Series = donut;

            // Bar: top 10 towarów (krojenie kg)
            var top10 = widoczne
                .Where(e => !string.IsNullOrEmpty(e.NazwaTowaru) && e.Krojenie > 0)
                .GroupBy(e => e.NazwaTowaru)
                .Select(g => new { Tow = g.Key, Kg = g.Sum(e => e.Krojenie) })
                .OrderByDescending(x => x.Kg)
                .Take(10)
                .ToList();

            if (top10.Count == 0)
            {
                wykresElTop10.Series = new SeriesCollection();
                osYElTop10.Labels = new List<string>();
                return;
            }

            var values = new ChartValues<double>(top10.Select(x => (double)x.Kg).Reverse());
            wykresElTop10.Series = new SeriesCollection
            {
                new RowSeries
                {
                    Title = "Krojenie",
                    Values = values,
                    Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x7C, 0x3A, 0xED)),
                    DataLabels = true,
                    LabelPoint = p => p.X.ToString("N0"),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x4C, 0x1D, 0x95))
                }
            };
            // Skróć nazwy do 30 znaków
            osYElTop10.Labels = top10
                .Select(x => x.Tow.Length > 30 ? x.Tow.Substring(0, 28) + "…" : x.Tow)
                .Reverse().ToList();
            osXElTop10.LabelFormatter = v => v.ToString("N0");

            // ─── Trend efektywności w czasie (per dzień) ───
            var perDzien = widoczne
                .Where(e => e.Przychod > 0)
                .GroupBy(e => e.Data.Date)
                .Select(g => new
                {
                    Data = g.Key,
                    Prz = g.Sum(e => e.Przychod),
                    Kr = g.Sum(e => e.Krojenie)
                })
                .Where(x => x.Prz > 0)
                .OrderBy(x => x.Data)
                .ToList();

            if (perDzien.Count == 0)
            {
                wykresElTrend.Series = new SeriesCollection();
                osXElTrend.Labels = new List<string>();
                return;
            }

            // Format etykiety: dd.MM gdy <60 dni, MM.yy przy dłuższym okresie
            string fmt = (perDzien.Last().Data - perDzien.First().Data).TotalDays > 60 ? "MM.yy" : "dd.MM";
            var trendValues = new ChartValues<double>(
                perDzien.Select(x => (double)(x.Kr / x.Prz * 100m)));

            wykresElTrend.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Wydajność %",
                    Values = trendValues,
                    Stroke = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x05, 0x96, 0x69)),
                    StrokeThickness = 2.5,
                    Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(0x22, 0x05, 0x96, 0x69)),
                    PointGeometry = LiveCharts.Wpf.DefaultGeometries.Circle,
                    PointGeometrySize = 8,
                    LineSmoothness = 0.2,
                    DataLabels = false,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x05, 0x96, 0x69))
                }
            };
            osXElTrend.Labels = perDzien.Select(x => x.Data.ToString(fmt)).ToList();
            osYElTrend.LabelFormatter = v => v.ToString("F0") + "%";
        }

        private void BtnElementyWyczyscFiltr_Click(object sender, RoutedEventArgs e)
        {
            if (txtElementyFiltr != null) txtElementyFiltr.Text = "";
            ZastosujElementyFiltr();
        }

        private void ChkElementyGrupowanie_Toggle(object sender, RoutedEventArgs e)
            => ZastosujElementyGrupowanie(chkElementyGrupowanie?.IsChecked == true);

        private void ChkElTylkoKrojenie_Toggle(object sender, RoutedEventArgs e)
            => ZastosujElementyFiltr();

        private void ZastosujElementyGrupowanie(bool wlacz)
        {
            if (_elementyView == null) return;
            _elementyView.GroupDescriptions.Clear();
            if (wlacz)
                _elementyView.GroupDescriptions.Add(
                    new System.Windows.Data.PropertyGroupDescription(nameof(WydajnoscSzczegolElement.Kategoria)));
        }

        private void MenuFiltruyElementTowar_Click(object sender, RoutedEventArgs e)
        {
            if (dgElementy.SelectedItem is WydajnoscSzczegolElement el)
                txtElementyFiltr.Text = el.NazwaTowaru;
        }

        private void MenuFiltruyElementKategoria_Click(object sender, RoutedEventArgs e)
        {
            if (dgElementy.SelectedItem is WydajnoscSzczegolElement el)
                txtElementyFiltr.Text = el.Kategoria;
        }

        private void MenuSkopiujElement_Click(object sender, RoutedEventArgs e)
        {
            if (dgElementy.SelectedItem is WydajnoscSzczegolElement el)
            {
                try
                {
                    string csv = $"{el.Data:yyyy-MM-dd};{el.KodTowaru};{el.NazwaTowaru};{el.Kategoria};" +
                                 $"{el.Seria};{el.Przychod.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)};" +
                                 $"{el.Krojenie.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";
                    System.Windows.Clipboard.SetText(csv);
                }
                catch { }
            }
        }

        // ═══ Uzyski hodowców (cross-DB raport) ═══

        private System.ComponentModel.ICollectionView? _uzyskiView;

        private async void UzyskiAgregacja_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_ostatniBilansMat == null || dgUzyski == null) return;
            if (!IsLoaded) return;
            await OdswiezUzyskiAsync();
        }

        private OkresAgregacji AktualnyOkresUzyskow()
        {
            if (cbUzyskiAgregacja?.SelectedItem is System.Windows.Controls.ComboBoxItem item
                && item.Tag is string tag
                && Enum.TryParse<OkresAgregacji>(tag, out var okres))
                return okres;
            return OkresAgregacji.Tygodniowa;
        }

        private async Task OdswiezUzyskiAsync()
        {
            try
            {
                var okres = AktualnyOkresUzyskow();
                _ostatnieUzyski = await _service.LoadUzyskiPerHodowcaAsync(_ostatnieFiltry, okres);

                _uzyskiView = System.Windows.Data.CollectionViewSource.GetDefaultView(_ostatnieUzyski);
                ZastosujUzyskiGrupowanie(chkUzyskiGrupowanie?.IsChecked == true);
                dgUzyski.ItemsSource = _uzyskiView;
                ZastosujUzyskiFiltr();

                // Mini stats
                if (_ostatnieUzyski.Count == 0)
                {
                    txtUzyskiStats.Text = "Brak danych uzysku w wybranym zakresie";
                    return;
                }

                int liczbaHodowcow = _ostatnieUzyski.Select(u => u.KhId).Distinct().Count();
                int liczbaOkresow = _ostatnieUzyski.Select(u => u.KluczOkresu).Distinct().Count();
                decimal sumaZywca = _ostatnieUzyski.Sum(u => u.ZywiecKg);
                decimal sumaTuszek = _ostatnieUzyski.Sum(u => u.SumaTuszekAB);
                decimal sumaPodrobow = _ostatnieUzyski.Sum(u => u.SumaPodrobow);
                decimal sredniaWydajnosc = sumaZywca <= 0 ? 0 : (sumaTuszek + sumaPodrobow) / sumaZywca * 100m;
                var topHodowca = _ostatnieUzyski
                    .GroupBy(u => u.Hodowca)
                    .Select(g => new { Hod = g.Key, Suma = g.Sum(x => x.ZywiecKg) })
                    .OrderByDescending(x => x.Suma)
                    .FirstOrDefault();

                txtUzyskiStats.Text =
                    $"🐔 {liczbaHodowcow} hodowców  •  {liczbaOkresow} okresów  •  " +
                    $"żywiec {sumaZywca:N0} kg → tuszki {sumaTuszek:N0} kg + podroby {sumaPodrobow:N0} kg  •  " +
                    $"średnia wydajność {sredniaWydajnosc:F1}%  •  " +
                    $"top dostawca: {topHodowca?.Hod} ({topHodowca?.Suma:N0} kg)";
            }
            catch (Exception ex)
            {
                txtUzyskiStats.Text = "Błąd uzyskania: " + ex.Message;
            }
        }

        private void TxtUzyskiFiltr_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (txtUzyskiFiltrPlaceholder != null)
                txtUzyskiFiltrPlaceholder.Visibility = string.IsNullOrEmpty(txtUzyskiFiltr.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
            ZastosujUzyskiFiltr();
        }

        private void ZastosujUzyskiFiltr()
        {
            if (_uzyskiView == null) return;
            string txt = (txtUzyskiFiltr?.Text ?? "").Trim().ToLowerInvariant();
            _uzyskiView.Filter = string.IsNullOrEmpty(txt) ? null : obj =>
                obj is UzyskiPerHodowca u
                    && ((u.Hodowca ?? "").ToLowerInvariant().Contains(txt)
                     || u.KhId.ToString().Contains(txt));
        }

        private void BtnUzyskiWyczyscFiltr_Click(object sender, RoutedEventArgs e)
        {
            if (txtUzyskiFiltr != null) txtUzyskiFiltr.Text = "";
            ZastosujUzyskiFiltr();
        }

        private void ChkUzyskiGrupowanie_Toggle(object sender, RoutedEventArgs e)
            => ZastosujUzyskiGrupowanie(chkUzyskiGrupowanie?.IsChecked == true);

        private void ZastosujUzyskiGrupowanie(bool wlacz)
        {
            if (_uzyskiView == null) return;
            _uzyskiView.GroupDescriptions.Clear();
            if (wlacz)
                _uzyskiView.GroupDescriptions.Add(
                    new System.Windows.Data.PropertyGroupDescription(
                        nameof(UzyskiPerHodowca.EtykietaOkresu)));
        }

        private void BtnEksportUzyski_Click(object sender, RoutedEventArgs e)
            => CsvExporter.Eksportuj(_ostatnieUzyski, "Uzyski_hodowcy",
                new[] { nameof(UzyskiPerHodowca.EtykietaOkresu), nameof(UzyskiPerHodowca.Hodowca),
                        nameof(UzyskiPerHodowca.LiczbaDniDostaw),
                        nameof(UzyskiPerHodowca.ZywiecKg),
                        nameof(UzyskiPerHodowca.TuszkaAKg), nameof(UzyskiPerHodowca.TuszkaBKg),
                        nameof(UzyskiPerHodowca.WatrobaKg), nameof(UzyskiPerHodowca.ZoladkiKg),
                        nameof(UzyskiPerHodowca.SerceKg),
                        nameof(UzyskiPerHodowca.WydajnoscTuszekProc),
                        nameof(UzyskiPerHodowca.WydajnoscCalkowitaProc),
                        nameof(UzyskiPerHodowca.ProcWatroba),
                        nameof(UzyskiPerHodowca.ProcZoladki),
                        nameof(UzyskiPerHodowca.ProcSerce) },
                Window.GetWindow(this));

        private void MenuFiltruyUzyskHodowca_Click(object sender, RoutedEventArgs e)
        {
            if (dgUzyski.SelectedItem is UzyskiPerHodowca u)
                txtUzyskiFiltr.Text = u.Hodowca;
        }

        private void MenuSkopiujUzyskHodowcaNazwa_Click(object sender, RoutedEventArgs e)
        {
            if (dgUzyski.SelectedItem is UzyskiPerHodowca u)
                try { System.Windows.Clipboard.SetText(u.Hodowca); } catch { }
        }

        private void MenuSkopiujUzyskWiersz_Click(object sender, RoutedEventArgs e)
        {
            if (dgUzyski.SelectedItem is UzyskiPerHodowca u)
            {
                try
                {
                    string csv = $"{u.EtykietaOkresu};{u.Hodowca};" +
                                 $"{u.ZywiecKg.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)};" +
                                 $"{u.TuszkaAKg.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)};" +
                                 $"{u.TuszkaBKg.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)};" +
                                 $"{u.WatrobaKg.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)};" +
                                 $"{u.ZoladkiKg.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)};" +
                                 $"{u.SerceKg.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)};" +
                                 $"{u.WydajnoscCalkowitaProc.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";
                    System.Windows.Clipboard.SetText(csv);
                }
                catch { }
            }
        }

        // ═══ Eksporty per zakładka ═══

        private void BtnEksportTrend_Click(object sender, RoutedEventArgs e)
            => CsvExporter.Eksportuj(_ostatnieDni, "Wydajnosc_trend", owner: Window.GetWindow(this));

        private void BtnEksportHodowcy_Click(object sender, RoutedEventArgs e)
            => CsvExporter.Eksportuj(_ostatniHodowcy, "Wydajnosc_hodowcy", owner: Window.GetWindow(this));

        private void BtnEksportKlasy_Click(object sender, RoutedEventArgs e)
            => CsvExporter.Eksportuj(_ostatnieKlasy, "Wydajnosc_klasy", owner: Window.GetWindow(this));

        private void BtnEksportElementy_Click(object sender, RoutedEventArgs e)
            => CsvExporter.Eksportuj(_ostatnieElementy, "Wydajnosc_elementy", owner: Window.GetWindow(this));

        // ─── Double-click hodowcy → Karta Hodowcy 360° ─────────────────────────

        private void DgHodowcy_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.DataGrid dg) return;
            if (dg.SelectedItem is not WydajnoscHodowca h) return;
            if (string.IsNullOrEmpty(h.CustomerID)) return;
            OtworzKarteHodowcy(h.CustomerID);
        }

        private void OtworzKarteHodowcy(string customerID)
        {
            try
            {
                var dlg = new Kalendarz1.Hodowcy.KartaHodowcyWindow(customerID)
                {
                    Owner = Window.GetWindow(this)
                };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się otworzyć Karty Hodowcy:\n" + ex.Message,
                    "Karta Hodowcy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnHistoriaKlas_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Kalendarz1.AnalitykaPelna.Windows.HistoriaKlasWagowychWindow(_ostatnieFiltry)
                {
                    Owner = Window.GetWindow(this)
                };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się otworzyć historii klas:\n" + ex.Message,
                    "Historia klas wagowych", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgKlasy_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not DataGrid dg) return;
            if (dg.SelectedItem is not WydajnoscKlasa klasa) return;
            if (klasa.Klasa is < 4 or > 12) return;

            try
            {
                var dlg = new Kalendarz1.AnalitykaPelna.Windows.SzczegolyKlasyDialog(klasa, _ostatnieFiltry)
                {
                    Owner = Window.GetWindow(this)
                };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się otworzyć szczegółów klasy:\n" + ex.Message,
                    "Szczegóły klasy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Kolor wydajności (zielony 93-97%, czerwony poza tolerancją) ───

        private static System.Windows.Media.Brush WydajnoscKolor(decimal proc)
        {
            double p = (double)proc;
            double norma = AnalitykaConfig.NormaWydajnosciProc;
            double tol = AnalitykaConfig.TolerancjaWydajnosciProc;
            if (p == 0)
                return new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));   // szary — brak danych
            if (Math.Abs(p - norma) <= tol)
                return new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x05, 0x96, 0x69));   // zielony — w normie
            if (Math.Abs(p - norma) <= tol * 2)
                return new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B));   // pomarańczowy — uwaga
            return new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xDC, 0x26, 0x26));       // czerwony — poza tolerancją
        }

        private static int EtapKolejnosc(string etap) => etap switch
        {
            "ŻYWIEC PZ" => 1,
            "DO UBOJU" => 2,
            "UBÓJ" => 3,
            "MM- (przed krojeniem)" => 4,
            "DO KROJENIA" => 5,
            "KROJENIE" => 6,
            "MM- (po krojeniu)" => 7,
            _ => 99
        };

        private static string Skroc(decimal kg)
        {
            if (kg >= 1_000_000m) return (kg / 1_000_000m).ToString("F1") + "M";
            if (kg >= 1_000m) return (kg / 1_000m).ToString("F0") + "k";
            return kg.ToString("N0");
        }

        public void EksportujCsv()
        {
            // Domyślnie eksportujemy trend dzienny
            CsvExporter.Eksportuj(_ostatnieDni, "Wydajnosc_trend",
                new[] { nameof(WydajnoscDzien.Data), nameof(WydajnoscDzien.DzienTygodnia),
                    nameof(WydajnoscDzien.TuszkaBKg), nameof(WydajnoscDzien.ElementyKg),
                    nameof(WydajnoscDzien.PodrobyKg), nameof(WydajnoscDzien.SumaWyjscie),
                    nameof(WydajnoscDzien.WydajnoscProcent), nameof(WydajnoscDzien.CzyAlert),
                    nameof(WydajnoscDzien.Status), nameof(WydajnoscDzien.Uwagi) },
                Window.GetWindow(this));
        }
    }

    public class AlertBackgroundConverter : IValueConverter
    {
        private static readonly Brush AlertBrush =
            new SolidColorBrush(Color.FromRgb(0xFF, 0xE5, 0xE5));
        private static readonly Brush DefaultBrush = Brushes.Transparent;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? AlertBrush : DefaultBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class AlertIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? "⚠" : "";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
