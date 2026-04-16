using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Kalendarz1.Reklamacje.Analityka
{
    public partial class StatystykiReklamacjiWindow : Window
    {
        private const string Conn = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private List<Rek> _dane = new();
        private bool _loaded;

        // Filtr daty — ustawiane przez guziki
        private int? _filtrRok;
        private int? _filtrMiesiac;
        private int _filtrZakres = 180;
        private string _filtrGrupowanie = "month";

        // Cache kluczy do drilldown
        private string[] _trendKeys;
        private List<Rek> _lastFiltered;
        private string[] _prodKeys;
        private string[] _typKeys;

        public StatystykiReklamacjiWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var sw = Stopwatch.StartNew();
            Laduj();
            sw.Stop();
            WypelnijKontrahentow();
            BudujGuziki();
            _loaded = true;
            txtPodtytul.Text = $"{_dane.Count:N0} pozycji korekt miesa  •  {sw.ElapsedMilliseconds} ms  •  Wybierz kontrahenta";
            Odswiez();
        }

        // ════════════════════════════════════════
        // GUZIKI FILTROW
        // ════════════════════════════════════════

        private void BudujGuziki()
        {
            // LATA — unikalne z danych
            var lata = _dane.Select(d => d.Data.Year).Distinct().OrderByDescending(y => y).ToList();
            DodajGuzik(panelLata, "Wszystkie", () => { _filtrRok = null; _filtrMiesiac = null; OdswiezGuzikiIFiltr(); }, true);
            foreach (var rok in lata)
            {
                int r = rok;
                DodajGuzik(panelLata, r.ToString(), () => { _filtrRok = r; _filtrMiesiac = null; _filtrZakres = 9999; OdswiezGuzikiIFiltr(); });
            }

            // MIESIACE
            string[] nazwyMies = { "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paz", "Lis", "Gru" };
            DodajGuzik(panelMiesiace, "Caly rok", () => { _filtrMiesiac = null; OdswiezGuzikiIFiltr(); }, true);
            for (int m = 1; m <= 12; m++)
            {
                int mc = m;
                DodajGuzik(panelMiesiace, nazwyMies[m - 1], () => { _filtrMiesiac = mc; _filtrZakres = 9999; OdswiezGuzikiIFiltr(); });
            }

            // SZYBKIE ZAKRESY
            var zakresy = new[] { ("30 dni", 30), ("90 dni", 90), ("Pol roku", 180), ("Rok", 365), ("2 lata", 730), ("Wszystko", 9999) };
            foreach (var (label, dni) in zakresy)
            {
                int d = dni;
                bool aktywny = d == 180;
                DodajGuzik(panelZakresy, label, () => { _filtrZakres = d; _filtrRok = null; _filtrMiesiac = null; OdswiezGuzikiIFiltr(); }, aktywny);
            }

            // GRUPOWANIE
            var grupy = new[] { ("Tydz.", "week"), ("Mies.", "month"), ("Kw.", "quarter"), ("Rok", "year") };
            foreach (var (label, tag) in grupy)
            {
                string g = tag;
                bool aktywny = g == "month";
                DodajGuzik(panelGrupowanie, label, () => { _filtrGrupowanie = g; OdswiezGuzikiIFiltr(); }, aktywny);
            }
        }

        private void DodajGuzik(WrapPanel panel, string text, Action onClick, bool aktywny = false)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0),
                Foreground = aktywny
                    ? Brushes.White
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C7380")),
                Background = aktywny
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2230")),
                Tag = onClick
            };
            // Zaokraglone rogi
            var template = new ControlTemplate(typeof(Button));
            var bdFactory = new FrameworkElementFactory(typeof(Border));
            bdFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            bdFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            bdFactory.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            cpFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cpFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bdFactory.AppendChild(cpFactory);
            template.VisualTree = bdFactory;
            btn.Template = template;

            var origBg = btn.Background;
            btn.MouseEnter += (s, e) =>
            {
                if (btn.Background is SolidColorBrush sb && sb.Color != ((SolidColorBrush)new BrushConverter().ConvertFromString("#3B82F6")).Color)
                    btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3040"));
            };
            btn.MouseLeave += (s, e) =>
            {
                // Odtworz kolor po OdswiezPanelGuzikow — nie nadpisuj aktywnego
                if (btn.Foreground is SolidColorBrush fg && fg.Color != Colors.White)
                    btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2230"));
            };

            btn.Click += (s, e) =>
            {
                if (btn.Tag is Action action) action();
            };

            panel.Children.Add(btn);
        }

        private void OdswiezGuzikiIFiltr()
        {
            // Odswiezenie wygladu guzikow
            OdswiezPanelGuzikow(panelLata, b =>
            {
                string txt = (b.Content as string) ?? "";
                if (txt == "Wszystkie") return !_filtrRok.HasValue;
                return int.TryParse(txt, out int r) && _filtrRok == r;
            });

            string[] nazwyMies = { "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paz", "Lis", "Gru" };
            OdswiezPanelGuzikow(panelMiesiace, b =>
            {
                string txt = (b.Content as string) ?? "";
                if (txt == "Caly rok") return !_filtrMiesiac.HasValue;
                int idx = Array.IndexOf(nazwyMies, txt);
                return idx >= 0 && _filtrMiesiac == idx + 1;
            });

            var zakresy = new[] { 30, 90, 180, 365, 730, 9999 };
            int zi = 0;
            OdswiezPanelGuzikow(panelZakresy, b =>
            {
                if (zi >= zakresy.Length) return false;
                bool res = !_filtrRok.HasValue && zakresy[zi] == _filtrZakres;
                zi++;
                return res;
            });

            var grupy = new[] { "week", "month", "quarter", "year" };
            int gi = 0;
            OdswiezPanelGuzikow(panelGrupowanie, b =>
            {
                if (gi >= grupy.Length) return false;
                bool res = grupy[gi] == _filtrGrupowanie;
                gi++;
                return res;
            });

            Odswiez();
        }

        private void OdswiezPanelGuzikow(WrapPanel panel, Func<Button, bool> czyAktywny)
        {
            var aktywnyBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
            var nieaktywnyBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2230"));
            var aktywnyFg = Brushes.White;
            var nieaktywnyFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C7380"));

            foreach (var child in panel.Children)
            {
                if (child is Button btn)
                {
                    bool akt = czyAktywny(btn);
                    btn.Background = akt ? aktywnyBg : nieaktywnyBg;
                    btn.Foreground = akt ? aktywnyFg : nieaktywnyFg;
                }
            }
        }

        // ════════════════════════════════════════
        // DANE — strata = oryginalna ilosc - nowa ilosc po korekcie
        // Korekta ma 2 pozycje per towar: ujemna (anulowanie) + dodatnia (nowa ilosc)
        // Bierzemy tylko DODATNIE (nowa ilosc) i porownujemy z oryginalna
        // ════════════════════════════════════════

        private void Laduj()
        {
            _dane.Clear();
            try
            {
                using var conn = new SqlConnection(Conn);
                conn.Open();
                // Bierzemy pozycje DODATNIE korekty (nowa ilosc po korekcie)
                // + pozycje oryginalne z faktury bazowej (idpozkoryg -> DP.id)
                // Strata = oryginalna - nowa
                using var cmd = new SqlCommand(@"
                    SELECT
                        DK.id, DK.kod, DK.seria, DK.data, DK.khid,
                        C.shortcut AS Kontrahent,
                        ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec,
                        DK.opis,
                        DPK.kod AS TowarKod,
                        ISNULL(TW.nazwa, DPK.kod) AS TowarNazwa,
                        ABS(ISNULL(DPF.ilosc, 0)) AS KgOryginal,
                        ABS(ISNULL(DPK.ilosc, 0)) AS KgPoKorekcie,
                        ABS(ISNULL(DPF.cena, 0)) AS Cena,
                        ABS(ISNULL(DPF.ilosc, 0)) - ABS(ISNULL(DPK.ilosc, 0)) AS StrataKg,
                        (ABS(ISNULL(DPF.ilosc, 0)) - ABS(ISNULL(DPK.ilosc, 0))) * ABS(ISNULL(DPF.cena, 0)) AS StrataZl,
                        ISNULL(FB.kod, '') AS FaktBaz
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DPK ON DPK.super = DK.id AND DPK.ilosc >= 0
                    INNER JOIN [HANDEL].[HM].[DP] DPF ON DPK.idpozkoryg = DPF.id
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                    LEFT JOIN [HANDEL].[HM].[TW] TW ON DPK.idtw = TW.ID
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    LEFT JOIN [HANDEL].[HM].[DK] FB ON DK.iddokkoryg = FB.id
                    WHERE DK.seria IN ('sFKS', 'sFKSB', 'sFWK')
                      AND DK.anulowany = 0
                      AND TW.katalog IN (67095, 67153)
                      AND C.shortcut NOT LIKE 'SD/%'
                      AND DPK.idpozkoryg IS NOT NULL AND DPK.idpozkoryg > 0
                      AND (ABS(ISNULL(DPF.ilosc, 0)) - ABS(ISNULL(DPK.ilosc, 0))) > 0.01", conn);
                cmd.CommandTimeout = 90;
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    string seria = r.IsDBNull(2) ? "" : r.GetString(2);
                    _dane.Add(new Rek
                    {
                        IdDK = r.GetInt32(0),
                        Kod = r.GetString(1),
                        Seria = seria,
                        Typ = seria switch { "sFKS" => "FKS", "sFKSB" => "FKSB", "sFWK" => "FWK", _ => seria },
                        Data = r.GetDateTime(3),
                        KhId = r.GetInt32(4),
                        Kh = r.GetString(5),
                        Handl = r.IsDBNull(6) ? "-" : r.GetString(6),
                        Opis = r.IsDBNull(7) ? "" : r.GetString(7),
                        TwKod = r.IsDBNull(8) ? "" : r.GetString(8),
                        Tw = r.IsDBNull(9) ? "" : r.GetString(9),
                        KgOryg = r.IsDBNull(10) ? 0 : Convert.ToDecimal(r.GetValue(10)),
                        KgPo = r.IsDBNull(11) ? 0 : Convert.ToDecimal(r.GetValue(11)),
                        Cena = r.IsDBNull(12) ? 0 : Convert.ToDecimal(r.GetValue(12)),
                        StrataKg = r.IsDBNull(13) ? 0 : Convert.ToDecimal(r.GetValue(13)),
                        StrataZl = r.IsDBNull(14) ? 0 : Convert.ToDecimal(r.GetValue(14)),
                        FaktBaz = r.IsDBNull(15) ? "" : r.GetString(15)
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WypelnijKontrahentow()
        {
            var lista = new List<KhC> { new() { KhId = 0, Nazwa = "-- Wybierz kontrahenta --" } };
            lista.AddRange(_dane.GroupBy(d => d.KhId)
                .Select(g => new KhC
                {
                    KhId = g.Key,
                    Nazwa = g.First().Kh,
                    N = g.Select(x => x.IdDK).Distinct().Count(),
                    W = g.Sum(x => x.StrataZl)
                })
                .OrderByDescending(k => k.W));
            cmbKontrahent.ItemsSource = lista;
            cmbKontrahent.DisplayMemberPath = "Opis";
            cmbKontrahent.SelectedIndex = 0;
        }

        private void Filtr_Changed(object s, SelectionChangedEventArgs e) { if (_loaded) Odswiez(); }

        private List<Rek> Filtruj()
        {
            int kh = cmbKontrahent.SelectedItem is KhC kc ? kc.KhId : 0;

            return _dane.Where(d =>
            {
                // Filtr kontrahenta
                if (kh != 0 && d.KhId != kh) return false;

                // Filtr rok + miesiac (priorytet nad zakresem)
                if (_filtrRok.HasValue)
                {
                    if (d.Data.Year != _filtrRok.Value) return false;
                    if (_filtrMiesiac.HasValue && d.Data.Month != _filtrMiesiac.Value) return false;
                    return true;
                }

                // Filtr zakresu dni
                if (_filtrZakres < 9999)
                {
                    DateTime od = DateTime.Today.AddDays(-_filtrZakres);
                    if (d.Data < od) return false;
                }

                return true;
            }).ToList();
        }

        private string Grup() => _filtrGrupowanie;

        private static string Fzl(decimal v) => v.ToString("N0", CultureInfo.GetCultureInfo("pl-PL")) + " zl";
        private static string Fkg(decimal v) => v.ToString("N0", CultureInfo.GetCultureInfo("pl-PL")) + " kg";

        private void Odswiez()
        {
            var sw = Stopwatch.StartNew();
            var d = Filtruj();
            _lastFiltered = d;
            KPI(d);
            Trend(d);
            TypKorekty(d);
            TopProd(d);
            Detale(d);
            sw.Stop();

            // Dynamiczny podtytul
            int ile = d.Select(x => x.IdDK).Distinct().Count();
            string kh = cmbKontrahent.SelectedItem is KhC kc && kc.KhId > 0 ? kc.Nazwa : "wszyscy kontrahenci";
            string okres = _filtrRok.HasValue
                ? (_filtrMiesiac.HasValue ? $"{_filtrRok}-{_filtrMiesiac:00}" : $"rok {_filtrRok}")
                : (_filtrZakres < 9999 ? $"ostatnie {_filtrZakres} dni" : "caly okres");
            txtPodtytul.Text = $"{kh}  •  {okres}  •  {ile} korekt  •  {d.Count} pozycji  •  {sw.ElapsedMilliseconds} ms";
        }

        // ════════════════════════════════════════
        // KPI
        // ════════════════════════════════════════

        private void KPI(List<Rek> d)
        {
            int ile = d.Select(x => x.IdDK).Distinct().Count();
            decimal zl = d.Sum(x => x.StrataZl);
            decimal kg = d.Sum(x => x.StrataKg);
            decimal sr = ile > 0 ? zl / ile : 0;
            int prod = d.Select(x => x.TwKod).Distinct().Count();
            string handl = d.Select(x => x.Handl).Where(h => h != "-" && h != "").GroupBy(h => h)
                .OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "-";

            txtKpiIle.Text = ile.ToString("N0");
            txtKpiZl.Text = Fzl(zl);
            txtKpiKg.Text = Fkg(kg);
            txtKpiSr.Text = Fzl(sr);
            txtKpiProd.Text = prod.ToString("N0");
            txtKpiHandl.Text = handl;

            int dni = _filtrRok.HasValue ? 365 : _filtrZakres;
            if (dni > 0 && dni < 9999)
            {
                decimal m = dni / 30.0m;
                txtKpiZlSub.Text = $"~{(zl / m):N0} zl / mies.";
                txtKpiKgSub.Text = $"~{(kg / m):N0} kg / mies.";
                txtKpiIleSub.Text = $"~{(ile / (dni / 7.0m)):N1} / tydz.";
            }
            else { txtKpiZlSub.Text = ""; txtKpiKgSub.Text = ""; txtKpiIleSub.Text = ""; }
        }

        // ════════════════════════════════════════
        // TREND
        // ════════════════════════════════════════

        private void Trend(List<Rek> d)
        {
            string g = Grup();

            // Zbierz dane z rekordow
            var gr = new SortedDictionary<string, (decimal zl, decimal kg)>();
            foreach (var r in d)
            {
                string k = GrupKlucz(r.Data, g);
                if (!gr.ContainsKey(k)) gr[k] = (0, 0);
                var v = gr[k];
                gr[k] = (v.zl + r.StrataZl, v.kg + r.StrataKg);
            }

            // Wypelnij luki zerami — pelna ciaglosc osi czasu
            if (d.Count > 0)
            {
                DateTime min = d.Min(x => x.Data);
                DateTime max = d.Max(x => x.Data);
                var wszystkieKlucze = GenerujWszystkieKlucze(min, max, g);
                foreach (var k in wszystkieKlucze)
                    if (!gr.ContainsKey(k)) gr[k] = (0, 0);
            }

            chartTrend.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Strata zl",
                    Values = new ChartValues<double>(gr.Values.Select(v => (double)v.zl)),
                    Fill = new SolidColorBrush(Color.FromArgb(200, 239, 68, 68)),
                    MaxColumnWidth = 30, ColumnPadding = 3,
                    ScalesYAt = 0,
                    DataLabels = true,
                    LabelPoint = p => p.Y.ToString("N0") + " zl",
                    Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                    FontSize = 8
                },
                new LineSeries
                {
                    Title = "Strata kg",
                    Values = new ChartValues<double>(gr.Values.Select(v => (double)v.kg)),
                    Stroke = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                    Fill = new SolidColorBrush(Color.FromArgb(15, 245, 158, 11)),
                    PointGeometrySize = 5, StrokeThickness = 2, LineSmoothness = 0.2,
                    ScalesYAt = 1,
                    DataLabels = true,
                    LabelPoint = p => p.Y.ToString("N0") + " kg",
                    Foreground = new SolidColorBrush(Color.FromArgb(160, 245, 158, 11)),
                    FontSize = 8
                }
            };
            _trendKeys = gr.Keys.ToArray();
            axTX.Labels = _trendKeys;
            axTY.LabelFormatter = v => v.ToString("N0") + " zl";
            axTY2.LabelFormatter = v => v.ToString("N0") + " kg";
            txtTrendInfo.Text = $"{gr.Count} okresow";
        }

        private static string GrupKlucz(DateTime data, string g) => g switch
        {
            "week" => ISOWeek.GetYear(data) + "-W" + ISOWeek.GetWeekOfYear(data).ToString("00"),
            "month" => data.ToString("yyyy-MM"),
            "quarter" => $"{data.Year}-Q{(data.Month - 1) / 3 + 1}",
            "year" => data.Year.ToString(),
            _ => data.ToString("yyyy-MM")
        };

        private static List<string> GenerujWszystkieKlucze(DateTime od, DateTime doo, string g)
        {
            var klucze = new List<string>();
            switch (g)
            {
                case "week":
                    var w = od.AddDays(-(int)od.DayOfWeek + 1);
                    while (w <= doo) { klucze.Add(GrupKlucz(w, g)); w = w.AddDays(7); }
                    break;
                case "month":
                    var m = new DateTime(od.Year, od.Month, 1);
                    while (m <= doo) { klucze.Add(m.ToString("yyyy-MM")); m = m.AddMonths(1); }
                    break;
                case "quarter":
                    var q = new DateTime(od.Year, ((od.Month - 1) / 3) * 3 + 1, 1);
                    while (q <= doo) { klucze.Add($"{q.Year}-Q{(q.Month - 1) / 3 + 1}"); q = q.AddMonths(3); }
                    break;
                case "year":
                    for (int y = od.Year; y <= doo.Year; y++) klucze.Add(y.ToString());
                    break;
            }
            return klucze;
        }

        // ════════════════════════════════════════
        // TYP KOREKTY
        // ════════════════════════════════════════

        private void TypKorekty(List<Rek> d)
        {
            var gr = d.GroupBy(x => x.Typ)
                .Select(g => new
                {
                    Typ = g.Key,
                    Zl = g.Sum(x => x.StrataZl),
                    Kg = g.Sum(x => x.StrataKg),
                    N = g.Select(x => x.IdDK).Distinct().Count()
                })
                .OrderByDescending(x => x.Zl).ToList();

            string TypOpis(string t, int n, decimal zl, decimal kg) => t switch
            {
                "FKS" => $"FKS Korekta ({n} szt, {Fkg(kg)})",
                "FKSB" => $"FKSB Brak wagowy ({n} szt, {Fkg(kg)})",
                "FWK" => $"FWK Korekta wl. ({n} szt, {Fkg(kg)})",
                _ => $"{t} ({n} szt)"
            };

            chartTyp.Series = new SeriesCollection
            {
                new RowSeries
                {
                    Title = "Strata",
                    Values = new ChartValues<double>(gr.Select(x => (double)x.Zl)),
                    Fill = new LinearGradientBrush(
                        Color.FromRgb(99, 102, 241), Color.FromRgb(59, 130, 246), 0),
                    DataLabels = true,
                    LabelPoint = p => p.Y.ToString("N0") + " zl",
                    Foreground = Brushes.White,
                    FontSize = 10
                }
            };
            _typKeys = gr.Select(x => x.Typ).ToArray();
            axTypY.Labels = gr.Select(x => TypOpis(x.Typ, x.N, x.Zl, x.Kg)).ToArray();
        }

        // ════════════════════════════════════════
        // TOP PRODUKTY
        // ════════════════════════════════════════

        private void TopProd(List<Rek> d)
        {
            var top = d.GroupBy(x => x.Tw)
                .Select(g => new { N = g.Key, Kg = g.Sum(x => x.StrataKg), Zl = g.Sum(x => x.StrataZl) })
                .OrderByDescending(x => x.Kg).Take(10).ToList();

            chartProd.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Strata kg",
                    Values = new ChartValues<double>(top.Select(t => (double)t.Kg)),
                    Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                    MaxColumnWidth = 22,
                    DataLabels = true,
                    LabelPoint = p => p.Y.ToString("N0") + " kg",
                    Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    FontSize = 8
                },
                new ColumnSeries
                {
                    Title = "Strata zl",
                    Values = new ChartValues<double>(top.Select(t => (double)t.Zl)),
                    Fill = new SolidColorBrush(Color.FromArgb(160, 239, 68, 68)),
                    MaxColumnWidth = 22,
                    DataLabels = true,
                    LabelPoint = p => p.Y.ToString("N0") + " zl",
                    Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 107, 107)),
                    FontSize = 8
                }
            };
            _prodKeys = top.Select(t => t.N).ToArray();
            axPX.Labels = top.Select(t => t.N.Length > 20 ? t.N.Substring(0, 20) : t.N).ToArray();
        }

        // ════════════════════════════════════════
        // SZCZEGOLY
        // ════════════════════════════════════════

        private void Detale(List<Rek> d)
        {
            var rows = d.OrderByDescending(x => x.Data)
                .Select(x => new DetaleRow
                {
                    DataSort = x.Data,
                    DataStr = x.Data.ToString("dd.MM.yy"),
                    Kod = x.Kod,
                    Typ = x.Typ,
                    Tw = x.Tw,
                    KgOryg = x.KgOryg,
                    KgPo = x.KgPo,
                    StrataKg = x.StrataKg,
                    Cena = x.Cena,
                    StrataZl = x.StrataZl,
                    Opis = x.Opis,
                    FaktBaz = x.FaktBaz
                }).ToList();

            dgDetale.ItemsSource = rows;
            txtDetaleInfo.Text = $"{rows.Count} pozycji";
        }

        // ════════════════════════════════════════
        // DRILLDOWN — klikniecie na wykres
        // ════════════════════════════════════════

        private void ChartTrend_DataClick(object sender, ChartPoint p)
        {
            if (_trendKeys == null || _lastFiltered == null) return;
            int idx = (int)p.X;
            if (idx < 0 || idx >= _trendKeys.Length) return;
            string klucz = _trendKeys[idx];
            string g = Grup();

            var docs = _lastFiltered.Where(r => GrupKlucz(r.Data, g) == klucz).ToList();

            PokazDrilldown($"Dokumenty z okresu: {klucz}", docs);
        }

        private void ChartProd_DataClick(object sender, ChartPoint p)
        {
            if (_prodKeys == null || _lastFiltered == null) return;
            int idx = (int)p.X;
            if (idx < 0 || idx >= _prodKeys.Length) return;
            string produkt = _prodKeys[idx];
            var docs = _lastFiltered.Where(r => r.Tw == produkt).ToList();
            PokazDrilldown($"Korekty produktu: {produkt}", docs);
        }

        private void ChartTyp_DataClick(object sender, ChartPoint p)
        {
            if (_typKeys == null || _lastFiltered == null) return;
            int idx = (int)p.Y;
            if (idx < 0 || idx >= _typKeys.Length) return;
            string typ = _typKeys[idx];
            var docs = _lastFiltered.Where(r => r.Typ == typ).ToList();
            PokazDrilldown($"Korekty typu: {typ}", docs);
        }

        private void PokazDrilldown(string tytul, List<Rek> docs)
        {
            if (docs.Count == 0) { MessageBox.Show("Brak danych.", "Info", MessageBoxButton.OK); return; }

            var win = new Window
            {
                Title = tytul,
                Width = 1200, Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F1117")),
                FontFamily = new FontFamily("Segoe UI")
            };
            WindowIconHelper.SetIcon(win);

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Header
            root.Children.Add(new TextBlock
            {
                Text = tytul, FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // KPI
            var kpiPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var kpiIle = new TextBlock(); var kpiPoz = new TextBlock(); var kpiKg = new TextBlock(); var kpiZl = new TextBlock();
            void AddKpi(string label, TextBlock tb, string color)
            {
                var b = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1E26")), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#565D6B")), FontWeight = FontWeights.Bold });
                tb.FontSize = 20; tb.FontWeight = FontWeights.Bold; tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); tb.Margin = new Thickness(0, 2, 0, 0);
                sp.Children.Add(tb); b.Child = sp; kpiPanel.Children.Add(b);
            }
            AddKpi("DOKUMENTOW", kpiIle, "#60A5FA");
            AddKpi("POZYCJI", kpiPoz, "#A78BFA");
            AddKpi("STRATA KG", kpiKg, "#FFD166");
            AddKpi("STRATA ZL", kpiZl, "#FF6B6B");
            Grid.SetRow(kpiPanel, 1);
            root.Children.Add(kpiPanel);

            // Guziki filtra typu
            var filterPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            filterPanel.Children.Add(new TextBlock { Text = "FILTRUJ TYP:", FontSize = 9, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5060")), FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });

            // Podsumowanie per typ — wiersz zsumowany
            var sumPanel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(filterPanel, 2);
            root.Children.Add(filterPanel);
            Grid.SetRow(sumPanel, 3);
            root.Children.Add(sumPanel);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // row 4 = dg

            // DataGrid
            var dg = new DataGrid
            {
                AutoGenerateColumns = false, IsReadOnly = true,
                Background = Brushes.Transparent, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C5CAD3")),
                BorderThickness = new Thickness(0), RowHeaderWidth = 0,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2230")),
                RowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1E26")),
                AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F232C")),
                HeadersVisibility = DataGridHeadersVisibility.Column,
                RowHeight = 28, FontSize = 11, ColumnHeaderHeight = 30, CanUserAddRows = false
            };
            var hs = new Style(typeof(DataGridColumnHeader));
            hs.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2230"))));
            hs.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#565D6B"))));
            hs.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
            hs.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, 9.0));
            hs.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(6, 4, 6, 4)));
            hs.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0)));
            dg.ColumnHeaderStyle = hs;
            Grid.SetRow(dg, 4);
            root.Children.Add(dg);

            // Funkcja odswiezania
            string aktywnyTyp = null; // null = wszystkie
            Action odswiez = () =>
            {
                var filtered = aktywnyTyp == null ? docs : docs.Where(x => x.Typ == aktywnyTyp).ToList();

                int ile = filtered.Select(x => x.IdDK).Distinct().Count();
                decimal zl = filtered.Sum(x => x.StrataZl);
                decimal kg = filtered.Sum(x => x.StrataKg);
                kpiIle.Text = ile.ToString("N0");
                kpiPoz.Text = filtered.Count.ToString("N0");
                kpiKg.Text = Fkg(kg);
                kpiZl.Text = Fzl(zl);

                // Podsumowanie per typ — kolorowe kafelki
                sumPanel.Children.Clear();
                var poTypie = filtered.GroupBy(x => x.Typ).OrderByDescending(g => g.Sum(x => x.StrataZl)).ToList();
                string[] kolory = { "#6366F1", "#3B82F6", "#10B981", "#F59E0B", "#EF4444" };
                int ci = 0;
                foreach (var gt in poTypie)
                {
                    string kolor = kolory[ci % kolory.Length]; ci++;
                    int dok = gt.Select(x => x.IdDK).Distinct().Count();
                    decimal tKg = gt.Sum(x => x.StrataKg);
                    decimal tZl = gt.Sum(x => x.StrataZl);
                    string typLabel = gt.Key switch { "FKS" => "FKS Korekta", "FKSB" => "FKSB Brak wagowy", "FWK" => "FWK Wlasna", _ => gt.Key };

                    var card = new Border
                    {
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1E26")),
                        CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 8, 14, 8),
                        Margin = new Thickness(0, 0, 8, 0),
                        BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolor)),
                        BorderThickness = new Thickness(2, 0, 0, 0)
                    };
                    var sp = new StackPanel { Orientation = Orientation.Horizontal };
                    sp.Children.Add(new TextBlock
                    {
                        Text = typLabel,
                        FontSize = 11, FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolor)),
                        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0)
                    });
                    sp.Children.Add(new TextBlock { Text = $"{dok} dok.", FontSize = 10, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B929A")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
                    sp.Children.Add(new TextBlock { Text = Fkg(tKg), FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD166")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
                    sp.Children.Add(new TextBlock { Text = Fzl(tZl), FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6B6B")), VerticalAlignment = VerticalAlignment.Center });
                    card.Child = sp;
                    sumPanel.Children.Add(card);
                }

                // Tabela szczegolow — SortMemberPath na polach numerycznych dla poprawnego sortowania
                dg.Columns.Clear();
                dg.Columns.Add(new DataGridTextColumn { Header = "DATA", Binding = new System.Windows.Data.Binding("DataStr"), Width = new DataGridLength(65), SortMemberPath = "Data" });
                dg.Columns.Add(new DataGridTextColumn { Header = "NR KOREKTY", Binding = new System.Windows.Data.Binding("Kod"), Width = new DataGridLength(110) });
                dg.Columns.Add(new DataGridTextColumn { Header = "TYP", Binding = new System.Windows.Data.Binding("Typ"), Width = new DataGridLength(48) });
                dg.Columns.Add(new DataGridTextColumn { Header = "KONTRAHENT", Binding = new System.Windows.Data.Binding("Kh"), Width = new DataGridLength(140) });
                dg.Columns.Add(new DataGridTextColumn { Header = "TOWAR", Binding = new System.Windows.Data.Binding("Tw"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                dg.Columns.Add(new DataGridTextColumn { Header = "FAKTURA", Binding = new System.Windows.Data.Binding("KgOrygStr"), Width = new DataGridLength(65), SortMemberPath = "KgOryg" });
                dg.Columns.Add(new DataGridTextColumn { Header = "PO KOR.", Binding = new System.Windows.Data.Binding("KgPoStr"), Width = new DataGridLength(65), SortMemberPath = "KgPo" });
                dg.Columns.Add(new DataGridTextColumn { Header = "STRATA KG", Binding = new System.Windows.Data.Binding("StrataKgStr"), Width = new DataGridLength(75), SortMemberPath = "StrataKg" });
                dg.Columns.Add(new DataGridTextColumn { Header = "CENA", Binding = new System.Windows.Data.Binding("CenaStr"), Width = new DataGridLength(60), SortMemberPath = "Cena" });
                dg.Columns.Add(new DataGridTextColumn { Header = "STRATA ZL", Binding = new System.Windows.Data.Binding("StrataZlStr"), Width = new DataGridLength(80), SortMemberPath = "StrataZl" });

                dg.ItemsSource = filtered.OrderByDescending(x => x.Data).Select(x => new DrillRow
                {
                    Data = x.Data,
                    DataStr = x.Data.ToString("dd.MM.yy"),
                    Kod = x.Kod, Typ = x.Typ, Kh = x.Kh, Tw = x.Tw,
                    KgOryg = x.KgOryg, KgPo = x.KgPo,
                    StrataKg = x.StrataKg, Cena = x.Cena, StrataZl = x.StrataZl
                }).ToList();
            };

            // Guziki typow
            var typy = docs.Select(x => x.Typ).Distinct().OrderBy(x => x).ToList();
            var btnAll = new List<Button>();

            Action<string> ustawTyp = (typ) =>
            {
                aktywnyTyp = typ;
                var aktBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                var nieBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2230"));
                foreach (var b in btnAll)
                {
                    bool akt = ((string)b.Tag == typ) || (typ == null && (string)b.Tag == "__ALL__");
                    b.Background = akt ? aktBg : nieBg;
                    b.Foreground = akt ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C7380"));
                }
                odswiez();
            };

            Action<Button> styleBtn = (b) =>
            {
                b.FontSize = 10; b.FontWeight = FontWeights.SemiBold;
                b.Padding = new Thickness(12, 5, 12, 5); b.Margin = new Thickness(0, 0, 4, 0);
                b.BorderThickness = new Thickness(0); b.Cursor = System.Windows.Input.Cursors.Hand;
                b.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2230"));
                b.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C7380"));
                var tmpl = new ControlTemplate(typeof(Button));
                var bd = new FrameworkElementFactory(typeof(Border));
                bd.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
                bd.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                bd.AppendChild(cp);
                tmpl.VisualTree = bd;
                b.Template = tmpl;
            };

            var bWszystkie = new Button { Content = "Wszystkie", Tag = "__ALL__" };
            styleBtn(bWszystkie);
            bWszystkie.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
            bWszystkie.Foreground = Brushes.White;
            bWszystkie.Click += (s, e) => ustawTyp(null);
            filterPanel.Children.Add(bWszystkie);
            btnAll.Add(bWszystkie);

            foreach (var typ in typy)
            {
                string t = typ;
                string label = t switch { "FKS" => "FKS Korekta", "FKSB" => "FKSB Brak wag.", "FWK" => "FWK Wlasna", _ => t };
                int cnt = docs.Where(x => x.Typ == t).Select(x => x.IdDK).Distinct().Count();
                var b = new Button { Content = $"{label} ({cnt})", Tag = t };
                styleBtn(b);
                b.Click += (s, e) => ustawTyp(t);
                filterPanel.Children.Add(b);
                btnAll.Add(b);
            }

            odswiez();
            win.Content = root;
            win.ShowDialog();
        }

        // ════════════════════════════════════════
        // MODELE
        // ════════════════════════════════════════

        private class Rek
        {
            public int IdDK; public string Kod, Seria, Typ; public DateTime Data;
            public int KhId; public string Kh, Handl, Opis, TwKod, Tw, FaktBaz;
            public decimal KgOryg, KgPo, Cena, StrataKg, StrataZl;
        }

        private class KhC
        {
            public int KhId { get; set; }
            public string Nazwa { get; set; }
            public int N { get; set; }
            public decimal W { get; set; }
            public string Opis => KhId == 0 ? Nazwa : $"{Nazwa}  ({N} korekt, {W:N0} zl straty)";
            public override string ToString() => Opis;
        }

        private class DetaleRow
        {
            public DateTime DataSort { get; set; }
            public string DataStr { get; set; }
            public string Kod { get; set; }
            public string Typ { get; set; }
            public string Tw { get; set; }
            public decimal KgOryg { get; set; }
            public decimal KgPo { get; set; }
            public decimal StrataKg { get; set; }
            public decimal Cena { get; set; }
            public decimal StrataZl { get; set; }
            public string Opis { get; set; }
            public string FaktBaz { get; set; }
            public string KgOrygStr => KgOryg.ToString("N0") + " kg";
            public string KgPoStr => KgPo.ToString("N0") + " kg";
            public string StrataKgStr => StrataKg.ToString("N0") + " kg";
            public string CenaStr => Cena.ToString("N2") + " zl";
            public string StrataZlStr => StrataZl.ToString("N0") + " zl";
        }

        private class DrillRow
        {
            public DateTime Data { get; set; }
            public string DataStr { get; set; }
            public string Kod { get; set; }
            public string Typ { get; set; }
            public string Kh { get; set; }
            public string Tw { get; set; }
            public decimal KgOryg { get; set; }
            public decimal KgPo { get; set; }
            public decimal StrataKg { get; set; }
            public decimal Cena { get; set; }
            public decimal StrataZl { get; set; }
            public string KgOrygStr => KgOryg.ToString("N0") + " kg";
            public string KgPoStr => KgPo.ToString("N0") + " kg";
            public string StrataKgStr => StrataKg.ToString("N0") + " kg";
            public string CenaStr => Cena.ToString("N2") + " zl";
            public string StrataZlStr => StrataZl.ToString("N0") + " zl";
        }
    }
}
