using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kalendarz1.AnalitykaPelna.Models;
using Kalendarz1.AnalitykaPelna.Services;

namespace Kalendarz1.AnalitykaPelna.Views
{
    /// <summary>
    /// Łańcuch Graficzny v2 — clean numbers-first dashboard (Linear/Stripe inspired).
    /// Sekcje: Header → 5 KPI Hero → Flow diagram → Breakdown → Alerty → Footer.
    /// </summary>
    public partial class WidokLancuchGraficzny : UserControl
    {
        private readonly WydajnoscService _service = new();
        private FlowChainGraficznyData _data = new();
        private FlowChainGraficznyData? _dataPoprzedni;
        private FiltryAnaliz? _ostatnieFiltry;
        public bool UkryjPrzyciskFullscreen { get; set; }

        public WidokLancuchGraficzny()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                if (UkryjPrzyciskFullscreen && btnFullscreen != null)
                    btnFullscreen.Visibility = Visibility.Collapsed;
                Focus();
            };
        }

        public async Task ZastosujFiltryAsync(FiltryAnaliz f)
        {
            _ostatnieFiltry = f;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                _data = await _service.LoadFlowChainGraficznyAsync(f);
                if (btnPorownaj.IsChecked == true)
                {
                    int dni = (f.DataDo.Date - f.DataOd.Date).Days + 1;
                    var fPoprz = new FiltryAnaliz
                    {
                        DataOd = f.DataOd.AddDays(-dni),
                        DataDo = f.DataOd.AddDays(-1),
                        TowarIdHandel = f.TowarIdHandel,
                        TowarIdLibra = f.TowarIdLibra,
                        OdbiorcyIds = f.OdbiorcyIds,
                        Handlowcy = f.Handlowcy
                    };
                    _dataPoprzedni = await _service.LoadFlowChainGraficznyAsync(fPoprz);
                }
                else _dataPoprzedni = null;
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

        private void Odswiez()
        {
            var s = _data.Summary;

            // Empty state
            bool brak = s.Zywiec.Kg <= 0 && s.Uboj.Kg <= 0 && s.Produkcja.Kg <= 0;
            bdEmptyState.Visibility = brak ? Visibility.Visible : Visibility.Collapsed;
            if (brak && _ostatnieFiltry != null)
            {
                txtEmptyHint.Text = $"Brak dokumentów w {_ostatnieFiltry.DataOd:dd.MM.yyyy} – {_ostatnieFiltry.DataDo:dd.MM.yyyy}.\n" +
                                    "Rozszerz zakres dat lub sprawdź filtry.";
            }

            // Header zakres
            if (_ostatnieFiltry != null)
            {
                int dni = (_ostatnieFiltry.DataDo.Date - _ostatnieFiltry.DataOd.Date).Days + 1;
                txtZakres.Text = $"{_ostatnieFiltry.DataOd:dd.MM.yyyy} – {_ostatnieFiltry.DataDo:dd.MM.yyyy} · {dni} dni · {s.LiczbaDokumentowCalkowita:N0} dokumentów";
            }

            // ─── HERO KPI ──────────────────────────────────────────────
            UstawKpiKg(kpiZywiec, kpiZywiecJedn, s.Zywiec.Kg);
            kpiZywiecMeta.Text = s.Zywiec.LiczbaDok > 0 ? $"{s.Zywiec.LiczbaDok} dok." : "—";
            UstawDelte(kpiZywiecDelta, s.Zywiec.Kg, _dataPoprzedni?.Summary.Zywiec.Kg);

            kpiWydUboj.Text = s.Zywiec.Kg > 0 ? $"{s.WydajnoscUbojuProc:F1}" : "—";
            UstawStatus(bdKpiWydUbojStatus, kpiWydUbojStatus, s.WydajnoscUbojuStatus, s.WydajnoscUbojuKolor);

            kpiWydKroj.Text = s.RozchodKrojenia.Kg > 0 ? $"{s.WydajnoscKrojeniaProc:F1}" : "—";
            UstawStatus(bdKpiWydKrojStatus, kpiWydKrojStatus, s.WydajnoscKrojeniaStatus, s.WydajnoscKrojeniaKolor);

            UstawKpiKg(kpiKlienci, kpiKlienciJedn, s.Klienci.Kg);
            kpiKlienciMeta.Text = s.Klienci.LiczbaDok > 0 ? $"{s.Klienci.LiczbaDok} dok." : "—";
            UstawDelte(kpiKlienciDelta, s.Klienci.Kg, _dataPoprzedni?.Summary.Klienci.Kg);

            UstawKpiKg(kpiZostalo, kpiZostaloJedn, s.ZostaloProdKg);
            kpiZostaloMeta.Text = s.Produkcja.Kg > 0 ? $"{s.ZostaloProdProc:F1}% z prod." : "";
            UstawStatus(bdKpiZostaloStatus, kpiZostaloStatus, s.ZostaloStatus, s.ZostaloKolor);

            // ─── FLOW DIAGRAM ──────────────────────────────────────────
            flowZywiecKg.Text = FormatTony(s.Zywiec.Kg);
            flowZywiecDok.Text = s.Zywiec.LiczbaDok > 0 ? $"{s.Zywiec.LiczbaDok} dok." : "";
            // Strata uboju — czerwony pasek pod ŻYWIEC
            if (s.Zywiec.Kg > 0 && s.StratyUbojuKg > 0)
            {
                flowStrataUboju.Text = $"↓ strata {FormatTony(s.StratyUbojuKg)} t  ({s.StratyUbojuProc:F1}%)";
                bdStrataUboju.Visibility = Visibility.Visible;
            }
            else
            {
                bdStrataUboju.Visibility = Visibility.Collapsed;
            }
            flowUbojKg.Text = FormatTony(s.Uboj.Kg);
            flowUbojDok.Text = s.Uboj.LiczbaDok > 0 ? $"{s.Uboj.LiczbaDok} dok." : "";
            flowProdKg.Text = FormatTony(s.Produkcja.Kg);
            flowProdDok.Text = s.Produkcja.LiczbaDok > 0 ? $"{s.Produkcja.LiczbaDok} dok." : "";
            flowKlienciKg.Text = FormatTony(s.Klienci.Kg);
            flowKlienciDok.Text = s.Klienci.LiczbaDok > 0 ? $"{s.Klienci.LiczbaDok} dok." : "";

            flowArrowZUProc.Text = s.Zywiec.Kg > 0 ? $"{s.WydajnoscUbojuProc:F1}%" : "—";
            flowArrowUPProc.Text = s.RozchodKrojenia.Kg > 0 ? $"{s.WydajnoscKrojeniaProc:F1}%" : "—";
            flowArrowPKProc.Text = s.Dystrybucja.Kg > 0 ? $"{s.ProcSprzedanoProc:F1}%" : "—";

            // Proporcjonalne wysokości barów (max 90px)
            decimal maxKg = Math.Max(s.Zywiec.Kg, Math.Max(s.Uboj.Kg, Math.Max(s.Produkcja.Kg, s.Klienci.Kg)));
            if (maxKg > 0)
            {
                bdFlowZywiec.Height = ProporcjaKg(s.Zywiec.Kg, maxKg);
                bdFlowUboj.Height = ProporcjaKg(s.Uboj.Kg, maxKg);
                bdFlowProd.Height = ProporcjaKg(s.Produkcja.Kg, maxKg);
                bdFlowKlienci.Height = ProporcjaKg(s.Klienci.Kg, maxKg);
            }

            // Podział uboju na ścieżki
            txtPodzialUboju.Text = s.Uboj.Kg > 0
                ? $"{FormatTony(s.RozchodKrojenia.Kg)} t ({s.UbojDoKrojeniaProc:F1}%) → krojenie    ·    " +
                  $"{FormatTony(s.UbojBezKrojeniaKg)} t ({s.UbojBezKrojeniaProc:F1}%) → sprzedaż bezpośrednia / mroźnia jako całe tuszki"
                : "Brak danych uboju.";

            // ─── BREAKDOWN — gdzie trafił PROD ─────────────────────────
            BudujBreakdown(s);

            // ─── ALERTY ─────────────────────────────────────────────────
            BudujAlerty(s);

            // ─── Footer ─────────────────────────────────────────────────
            txtFooter.Text = $"Odświeżono o {DateTime.Now:HH:mm:ss}  ·  F5 odśwież  ·  F11 pełny ekran  ·  S snapshot  ·  C porównaj";
        }

        // ════════════════════════════════════════════════════════════════
        // HERO KPI helpers
        // ════════════════════════════════════════════════════════════════
        private static void UstawKpiKg(TextBlock tbValue, TextBlock tbJedn, decimal kg)
        {
            if (kg == 0)
            {
                tbValue.Text = "—";
                tbValue.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));  // muted
                tbJedn.Text = "";
                return;
            }
            tbValue.Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));  // primary
            // Skala: <1t = kg, 1-99t = ton z 1 dec, >=100t = ton bez dec
            if (Math.Abs(kg) < 1000m)
            {
                tbValue.Text = $"{kg:N0}";
                tbJedn.Text = "kg";
            }
            else
            {
                decimal tony = kg / 1000m;
                tbValue.Text = tony >= 100 ? $"{tony:N0}" : $"{tony:N1}";
                tbJedn.Text = "t";
            }
        }

        private static void UstawStatus(Border pill, TextBlock txt, string statusText, string colorHex)
        {
            txt.Text = statusText ?? "—";
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(colorHex)!;
                pill.Background = new SolidColorBrush(c);
            }
            catch { }
        }

        private static void UstawDelte(TextBlock tb, decimal current, decimal? previous)
        {
            if (previous is null || previous <= 0)
            {
                tb.Visibility = Visibility.Collapsed;
                return;
            }
            decimal delta = (current - previous.Value) / previous.Value * 100m;
            string arrow = delta >= 0 ? "▲" : "▼";
            tb.Text = $"{arrow} {(delta >= 0 ? "+" : "")}{delta:F1}%";
            tb.Foreground = delta >= 0
                ? new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81))
                : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            tb.Visibility = Visibility.Visible;
        }

        // ════════════════════════════════════════════════════════════════
        // FLOW helpers
        // ════════════════════════════════════════════════════════════════
        private static string FormatTony(decimal kg)
        {
            if (kg == 0) return "—";
            if (Math.Abs(kg) < 1000m) return $"{kg:N0}";
            decimal tony = kg / 1000m;
            return tony >= 100 ? $"{tony:N0}t" : $"{tony:N1}t";
        }

        private static double ProporcjaKg(decimal kg, decimal max)
            => max <= 0 ? 60 : 30 + (double)(kg / max) * 60;

        // ════════════════════════════════════════════════════════════════
        // BREAKDOWN — 7 wierszy z pasem (Linear-style)
        // ════════════════════════════════════════════════════════════════
        private void BudujBreakdown(FlowChainSummary s)
        {
            panelBreakdown.Children.Clear();
            txtBreakdownSuma.Text = s.Produkcja.Kg > 0
                ? $"Z {FormatTony(s.Produkcja.Kg)} t wyprodukowanego — pasy proporcjonalne do udziału"
                : "Brak produkcji w okresie";

            if (s.Produkcja.Kg <= 0) return;

            var elementy = new List<(string Tag, string Ikona, string Nazwa, decimal Kg, decimal Proc, string Kolor)>
            {
                ("DYSTRYBUCJA", "📦", "Dystrybucja",  s.Dystrybucja.Kg, s.ProcDoDystProc, "#2563EB"),
                ("KLIENCI", "🚚", "→ Klienci (z DYST)", s.Klienci.Kg, s.Dystrybucja.Kg > 0 ? s.Klienci.Kg / s.Produkcja.Kg * 100m : 0, "#10B981"),
                ("MROZNIA", "❄", "Mroźnia",         s.Mroznia.Kg,     s.ProcDoMrozniProc, "#0EA5E9"),
                ("MASARNIA", "🥓", "Masarnia",       s.Masarnia.Kg,    s.ProcDoMasarniProc, "#9A3412"),
                ("KARMA", "🌾", "Karma",             s.Karma.Kg,       s.ProcDoKarmyProc, "#CA8A04"),
                ("ODPADY", "🗑", "Odpady",           s.Odpady.Kg,      s.ProcDoOdpadowProc, "#94A3B8"),
                ("", "📍", "Zostało w PROD",         Math.Max(0m, s.ZostaloProdKg), s.Produkcja.Kg > 0 ? Math.Max(0m, s.ZostaloProdKg) / s.Produkcja.Kg * 100m : 0, "#475569")
            };

            decimal maxProc = 100m;  // skala paska
            foreach (var el in elementy)
            {
                if (el.Kg <= 0) continue;
                panelBreakdown.Children.Add(BudujBreakdownRow(el.Tag, el.Ikona, el.Nazwa, el.Kg, el.Proc, el.Kolor, maxProc));
            }
        }

        private UIElement BudujBreakdownRow(string tag, string ikona, string nazwa, decimal kg, decimal proc, string kolorHex, decimal maxProc)
        {
            var row = new Border
            {
                Style = (Style)FindResource("BreakdownRowStyle"),
                Cursor = string.IsNullOrEmpty(tag) ? Cursors.Arrow : Cursors.Hand,
                Tag = tag
            };
            if (!string.IsNullOrEmpty(tag))
            {
                row.MouseLeftButtonUp += Kpi_Click;
                row.ToolTip = $"Kliknij — szczegóły {nazwa.ToLower()}";
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            grid.Children.Add(new TextBlock { Text = ikona, FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(grid.Children[0], 0);

            var nazwaTb = new TextBlock { Text = nazwa, FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            grid.Children.Add(nazwaTb);
            Grid.SetColumn(nazwaTb, 1);

            // Pasek proporcjonalny (rounded, elegant)
            var barWrap = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                CornerRadius = new CornerRadius(5),
                Height = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 16, 0)
            };
            try
            {
                var kolor = (Color)ColorConverter.ConvertFromString(kolorHex)!;
                double szerokoscProc = Math.Min(100, (double)(proc / maxProc * 100m));
                var pasek = new Grid();
                pasek.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(szerokoscProc, GridUnitType.Star) });
                pasek.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - szerokoscProc, GridUnitType.Star) });
                var fill = new Border { Background = new SolidColorBrush(kolor), CornerRadius = new CornerRadius(5) };
                Grid.SetColumn(fill, 0);
                pasek.Children.Add(fill);
                barWrap.Child = pasek;
            }
            catch { }
            grid.Children.Add(barWrap);
            Grid.SetColumn(barWrap, 2);

            var kgTb = new TextBlock
            {
                Text = $"{FormatTony(kg)} t",
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI"),
                TextAlignment = TextAlignment.Right,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)),
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(kgTb);
            Grid.SetColumn(kgTb, 3);

            var procTb = new TextBlock
            {
                Text = $"{proc:F1}%",
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI"),
                TextAlignment = TextAlignment.Right,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(procTb);
            Grid.SetColumn(procTb, 4);

            row.Child = grid;
            return row;
        }

        // ════════════════════════════════════════════════════════════════
        // ALERTY — lista konkretnych problemów
        // ════════════════════════════════════════════════════════════════
        private void BudujAlerty(FlowChainSummary s)
        {
            var alerty = new List<AlertItem>();

            // Severity: 1=critical (🚨), 2=warning (⚠), 3=info (📦/🧮)
            if (s.Produkcja.Kg > 0 && s.ProcDoOdpadowProc > 8m)
                alerty.Add(new AlertItem(1, "🚨", "Wysokie odpady",
                    $"{s.ProcDoOdpadowProc:F1}% z PROD trafiło do ODPADÓW ({s.Odpady.Kg:N0} kg). Norma 3-5%. Sprawdź klasę żywca i jakość krojenia."));

            if (s.ZostaloProdKg < 0)
                alerty.Add(new AlertItem(2, "⚠", "Saldo PROD ujemne",
                    $"Rozchodowano {Math.Abs(s.ZostaloProdKg):N0} kg więcej niż wyprodukowano — możliwy zapas z poprzedniego okresu lub błąd dokumentów."));

            if (s.RozchodKrojenia.Kg > 0 && s.WydajnoscKrojeniaProc < 55m)
                alerty.Add(new AlertItem(2, "⚠", "Niska wydajność krojenia",
                    $"Z {s.RozchodKrojenia.Kg:N0} kg sRWP wyszło tylko {s.Produkcja.Kg:N0} kg ({s.WydajnoscKrojeniaProc:F1}%). Norma ≥55%. Strata {s.StrataKrojeniaKg:N0} kg."));

            if (s.Zywiec.Kg > 0 && s.WydajnoscUbojuProc < 80m)
                alerty.Add(new AlertItem(2, "⚠", "Niska wydajność uboju",
                    $"Z {s.Zywiec.Kg:N0} kg żywca powstało {s.Uboj.Kg:N0} kg ({s.WydajnoscUbojuProc:F1}%). Norma ≥80%."));

            if (s.Zywiec.Kg > 0 && !s.BilansMasyOk)
                alerty.Add(new AlertItem(2, "🧮", "Bilans masy poza normą",
                    $"Strata uboju {s.StratyUbojuProc:F1}% — typowa 10-22%. {(s.StratyUbojuProc < 10m ? "Możliwy brak udokumentowanych odpadów ubojowych." : "Sprawdź czy nie ma wycieków lub błędnych pomiarów.")}"));

            if (s.Produkcja.Kg > 0 && s.ZostaloProdProc > 25m)
                alerty.Add(new AlertItem(3, "📦", "Duża stagnacja w PROD",
                    $"{s.ZostaloProdProc:F1}% wyprodukowanego ({s.ZostaloProdKg:N0} kg) wciąż w magazynie. Sprawdź czy nie zalega stary towar."));

            if (s.Dystrybucja.Kg > 0 && s.ProcSprzedanoProc < 70m)
                alerty.Add(new AlertItem(2, "⚠", "Słaba rotacja DYST → KLIENCI",
                    $"Tylko {s.ProcSprzedanoProc:F1}% z DYST trafiło do klientów ({s.Klienci.Kg:N0} kg z {s.Dystrybucja.Kg:N0} kg)."));

            // Sort: krytyczne najpierw
            alerty.Sort((a, b) => a.Severity.CompareTo(b.Severity));
            icAlerty.ItemsSource = alerty;

            if (alerty.Count == 0)
            {
                txtAlertyIkona.Text = "✓";
                txtAlertyIkona.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                txtAlertyTytul.Text = "WSZYSTKO W NORMIE";
                txtAlertyTytul.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                txtAlertyOpis.Text = "Wszystkie wskaźniki w granicach norm operacyjnych. Dobra robota!";
                bdAlerty.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                bdAlertyCount.Visibility = Visibility.Collapsed;
            }
            else
            {
                int crit = alerty.FindAll(a => a.Severity == 1).Count;
                txtAlertyIkona.Text = crit > 0 ? "🚨" : "⚠";
                txtAlertyIkona.Foreground = new SolidColorBrush(crit > 0
                    ? Color.FromRgb(0xEF, 0x44, 0x44) : Color.FromRgb(0xF5, 0x9E, 0x0B));
                txtAlertyTytul.Text = crit > 0 ? "WYMAGA PILNEJ UWAGI" : "WYMAGA UWAGI";
                txtAlertyTytul.Foreground = new SolidColorBrush(crit > 0
                    ? Color.FromRgb(0xEF, 0x44, 0x44) : Color.FromRgb(0xF5, 0x9E, 0x0B));
                txtAlertyOpis.Text = $"Wskaźniki poza normami — sprawdź źródło problemu. Posortowane od najpilniejszych.";
                bdAlerty.BorderBrush = new SolidColorBrush(crit > 0
                    ? Color.FromRgb(0xEF, 0x44, 0x44) : Color.FromRgb(0xF5, 0x9E, 0x0B));
                txtAlertyCount.Text = alerty.Count.ToString();
                bdAlertyCount.Background = new SolidColorBrush(crit > 0
                    ? Color.FromRgb(0xEF, 0x44, 0x44) : Color.FromRgb(0xF5, 0x9E, 0x0B));
                bdAlertyCount.Visibility = Visibility.Visible;
            }
            bdAlerty.Visibility = Visibility.Visible;
        }

        public class AlertItem
        {
            public int Severity { get; }
            public string Ikona { get; }
            public string Tytul { get; }
            public string Opis { get; }
            public AlertItem(int severity, string ikona, string tytul, string opis)
            {
                Severity = severity; Ikona = ikona; Tytul = tytul; Opis = opis;
            }
        }

        // ════════════════════════════════════════════════════════════════
        // HANDLERY
        // ════════════════════════════════════════════════════════════════
        private async void Kpi_Click(object sender, MouseButtonEventArgs e)
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

        private async void BtnPorownaj_Click(object sender, RoutedEventArgs e)
        {
            if (_ostatnieFiltry == null) return;
            await ZastosujFiltryAsync(_ostatnieFiltry);
        }

        private void BtnSnapshot_Click(object sender, RoutedEventArgs e) => ZrobSnapshot();

        private void ZrobSnapshot()
        {
            try
            {
                int w = (int)Math.Ceiling(ActualWidth);
                int h = (int)Math.Ceiling(ActualHeight);
                if (w <= 0 || h <= 0) return;
                var dpi = VisualTreeHelper.GetDpi(this);
                var rtb = new RenderTargetBitmap(
                    (int)(w * dpi.DpiScaleX), (int)(h * dpi.DpiScaleY),
                    96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
                rtb.Render(this);
                Clipboard.SetImage(rtb);
                MessageBox.Show("Snapshot skopiowany do schowka — Ctrl+V w mailu/prezentacji.",
                                "Snapshot", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnFullscreen_Click(object sender, RoutedEventArgs e)
        {
            var win = new Windows.LancuchGraficznyFullscreenWindow();
            win.Owner = Window.GetWindow(this);
            win.Show();
            if (_ostatnieFiltry != null)
                _ = win.ZastosujFiltryAsync(_ostatnieFiltry);
        }

        private async void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F5:
                    if (_ostatnieFiltry != null) await ZastosujFiltryAsync(_ostatnieFiltry);
                    e.Handled = true; break;
                case Key.F11:
                    BtnFullscreen_Click(this, new RoutedEventArgs());
                    e.Handled = true; break;
                case Key.S:
                    if (Keyboard.Modifiers == ModifierKeys.None) { ZrobSnapshot(); e.Handled = true; }
                    break;
                case Key.C:
                    if (Keyboard.Modifiers == ModifierKeys.None)
                    {
                        btnPorownaj.IsChecked = !(btnPorownaj.IsChecked ?? false);
                        if (_ostatnieFiltry != null) await ZastosujFiltryAsync(_ostatnieFiltry);
                        e.Handled = true;
                    }
                    break;
            }
        }
    }
}
