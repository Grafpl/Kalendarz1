using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kalendarz1.Kontrakty.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Views
{
    /// <summary>
    /// Dashboard ARiMR: donut % surowca pod 3-letnim kontraktem (cel 50%), inwentarz,
    /// lista wygasających w 90 dniach + hodowcy bez kontraktu (high-value).
    /// </summary>
    public partial class KontraktyDashboardWindow : Window
    {
        private readonly KontraktyService _service = new();
        private ArimrCompliance _c = new();
        private List<HodowcaBezKontraktu> _bezKontraktu = new();
        private List<ComplianceTrendPunkt> _trend = new();

        public KontraktyDashboardWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await ZaladujAsync();
        }

        private async System.Threading.Tasks.Task ZaladujAsync()
        {
            txtPodtytul.Text = $"stan na {DateTime.Now:dd.MM.yyyy HH:mm} • ostatnie 12 miesięcy";

            var c = await _service.GetComplianceAsync();
            _c = c;
            var inw = await _service.GetInwentarzAsync();
            var wygasajace = await _service.GetWygasajaceAsync(90);
            var bezKontraktu = await _service.GetHodowcyBezKontraktuAsync(15);
            _bezKontraktu = bezKontraktu;

            // ── Compliance (donut) ──
            txtProcent.Text = c.Status == "BRAK_DANYCH" ? "—" : $"{c.ProcentArimr:0.0}%";
            (string tekst, string hex) = c.Status switch
            {
                "OK" => ($"✅ powyżej progu (margines +{c.MarginesPp:0.0} pp)", "#16A34A"),
                "WARN" => ($"⚠ blisko progu ({c.MarginesPp:0.0} pp)", "#F59E0B"),
                "CRIT" => ($"🔴 poniżej 50% ({c.MarginesPp:0.0} pp) — działaj", "#DC2626"),
                _ => ("brak danych (schemat niewdrożony lub brak dostaw)", "#64748B")
            };
            txtProcentStatus.Text = tekst;
            txtProcentStatus.Foreground = Kolor(hex);
            RysujGauge(c.Status == "BRAK_DANYCH" ? 0 : (double)c.ProcentArimr, c.Status != "BRAK_DANYCH");

            txtSurowiecCalosc.Text = Tony(c.SurowiecCaloscKg);
            txtSurowiecArimr.Text = Tony(c.SurowiecArimrKg);
            txtHodowcy.Text = $"{c.HodowcowArimr} / {c.HodowcowOgolem}";

            // ── Inwentarz (karty z ikoną + akcentem) ──
            gridInwentarz.Children.Clear();
            gridInwentarz.Children.Add(Karta("Aktywne", inw.Aktywne, "#16A34A", "🟢"));
            gridInwentarz.Children.Add(Karta("Wygasają ≤90 dni", inw.Wygasajace90, "#C2410C", "⏰"));
            gridInwentarz.Children.Add(Karta("Wygasłe", inw.Wygasle, "#DC2626", "🔴"));
            gridInwentarz.Children.Add(Karta("Robocze", inw.Robocze, "#64748B", "📝"));

            // ── Listy + badge'y ──
            dgWygasajace.ItemsSource = wygasajace;
            dgBezKontraktu.ItemsSource = bezKontraktu;
            txtBadgeWygasajace.Text = wygasajace.Count.ToString();
            badgeWygasajace.Visibility = wygasajace.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            txtBadgeBezK.Text = bezKontraktu.Count.ToString();
            badgeBezK.Visibility = bezKontraktu.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // ── Symulator „dokontraktuj TOP X" ──
            UstawSymulator();

            // ── Trend (zapisz dzisiejszy punkt, potem odczytaj historię) ──
            await _service.ZapiszComplianceSnapshotAsync();
            _trend = await _service.GetComplianceTrendAsync(180);
            txtTrendPusto.Visibility = _trend.Count >= 2 ? Visibility.Collapsed : Visibility.Visible;
            UstawTrendKpi();
            RysujTrend();
        }

        // ── KPI trendu: teraz · Δ7 · Δ30 ─────────────────────────────────────
        private void UstawTrendKpi()
        {
            if (_trend.Count == 0) { boxTrendKpi.Visibility = Visibility.Collapsed; return; }
            boxTrendKpi.Visibility = Visibility.Visible;
            var ostatni = _trend[^1];
            txtTrendTeraz.Text = $"{ostatni.Procent:0.0}%";
            UstawDelta(txtTrend7,  Delta(ostatni.Procent, 7));
            UstawDelta(txtTrend30, Delta(ostatni.Procent, 30));
        }

        private decimal? Delta(decimal teraz, int dni)
        {
            var cel = DateTime.Today.AddDays(-dni);
            // znajdź punkt najbliższy dniom temu (≤ cel)
            ComplianceTrendPunkt? p = null;
            foreach (var t in _trend) if (t.Data <= cel) p = t; else break;
            return p == null ? null : teraz - p.Procent;
        }

        private void UstawDelta(TextBlock tb, decimal? d)
        {
            if (d is null) { tb.Text = "—"; tb.Foreground = Kolor("#94A3B8"); return; }
            tb.Text = (d >= 0 ? "+" : "") + $"{d:0.0} pp";
            tb.Foreground = d > 0 ? Kolor("#16A34A") : d < 0 ? Kolor("#DC2626") : Kolor("#475569");
        }

        // ── Dwuklik wierszy prawej kolumny ───────────────────────────────────
        private async void DgWygasajace_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgWygasajace.SelectedItem is KontraktListItem k)
            {
                new KontraktyKartaWindow(k.Id) { Owner = this }.ShowDialog();
                await ZaladujAsync();
            }
        }

        private async void DgBezKontraktu_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgBezKontraktu.SelectedItem is HodowcaBezKontraktu h)
            {
                new KontraktKreatorWindow(h.DostawcaId) { Owner = this }.ShowDialog();
                await ZaladujAsync();
            }
        }

        // ── Symulator dokontraktowania (4.2) ─────────────────────────────────
        private void UstawSymulator()
        {
            int n = _bezKontraktu.Count;
            sldSym.Maximum = n;
            sldSym.Value = 0;
            if (n == 0 || _c.SurowiecCaloscKg <= 0)
            {
                txtSymOpis.Text = "Brak hodowców bez kontraktu lub brak danych o dostawach — nic do symulacji.";
                sldSym.IsEnabled = false;
                boxSymWynik.Visibility = Visibility.Collapsed;
                return;
            }
            txtSymOpis.Text = $"Przesuń, by zasymulować podpisanie umów z TOP hodowcami bez kontraktu (wg wolumenu). Dostępnych: {n}.";
            boxSymWynik.Visibility = Visibility.Visible;
            Sym_Changed(sldSym, null!);
        }

        private void Sym_Changed(object sender, RoutedPropertyChangedEventArgs<double>? e)
        {
            if (_c.SurowiecCaloscKg <= 0) return;
            int x = (int)Math.Round(sldSym.Value);
            decimal dodatkowe = _bezKontraktu.Take(x).Sum(h => h.WagaKg12m);
            decimal nowyArimr = _c.SurowiecArimrKg + dodatkowe;
            decimal nowyProc = nowyArimr / _c.SurowiecCaloscKg * 100m;
            if (nowyProc > 100m) nowyProc = 100m;

            string strzalka = x == 0 ? "" : $"  (+{nowyProc - _c.ProcentArimr:0.0} pp)";
            string osiagniesz = nowyProc >= 50m ? "✅ osiągasz próg 50%" : $"⚠ wciąż poniżej progu (brakuje {50m - nowyProc:0.0} pp)";
            txtSymWynik.Text = x == 0
                ? $"Stan obecny: {_c.ProcentArimr:0.0}%"
                : $"Kontraktując TOP {x} (+{dodatkowe / 1000m:N0} t) → {nowyProc:0.0}%{strzalka}\n{osiagniesz}";

            boxSymWynik.Background = nowyProc >= 50m ? Kolor("#DCFCE7") : Kolor("#FEF3C7");
            txtSymWynik.Foreground = nowyProc >= 50m ? Kolor("#166534") : Kolor("#92400E");
        }

        // ── Trend zgodności (8.1) ────────────────────────────────────────────
        private void TrendCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RysujTrend();

        private void RysujTrend()
        {
            trendCanvas.Children.Clear();
            double w = trendCanvas.ActualWidth, h = trendCanvas.ActualHeight;
            if (w < 10 || h < 10 || _trend.Count < 2) return;

            double maxP = Math.Max(60, (double)_trend.Max(t => t.Procent) + 5);
            const double minP = 0;
            double X(int i) => _trend.Count == 1 ? w / 2 : i * (w - 8) / (_trend.Count - 1) + 4;
            double Y(decimal p) => h - ((double)p - minP) / (maxP - minP) * (h - 8) - 4;

            // linia celu 50%
            double y50 = Y(50m);
            trendCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = 0, Y1 = y50, X2 = w, Y2 = y50,
                Stroke = Kolor("#DC2626"), StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 }, Opacity = 0.7
            });
            trendCanvas.Children.Add(new TextBlock
            {
                Text = "50%", FontSize = 9, Foreground = Kolor("#DC2626"),
                RenderTransform = new TranslateTransform(2, Math.Max(0, y50 - 13))
            });

            // linia trendu
            var poly = new System.Windows.Shapes.Polyline
            {
                Stroke = Kolor("#2563EB"), StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };
            for (int i = 0; i < _trend.Count; i++)
                poly.Points.Add(new Point(X(i), Y(_trend[i].Procent)));
            trendCanvas.Children.Add(poly);

            // ostatni punkt + wartość
            int last = _trend.Count - 1;
            var dot = new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = Kolor("#2563EB") };
            Canvas.SetLeft(dot, X(last) - 4); Canvas.SetTop(dot, Y(_trend[last].Procent) - 4);
            trendCanvas.Children.Add(dot);
        }

        // Geometria gauge: półkole od lewej (0%) przez górę (50%) do prawej (100%).
        private const double GcX = 118, GcY = 130, GcR = 96, GcGrub = 16;

        /// <summary>Gauge-półkole ARiMR: 3 strefy (czerw/żółta/zielona), igła na wartości, znacznik celu 50%.</summary>
        private void RysujGauge(double pct, bool maDane)
        {
            gaugeCanvas.Children.Clear();

            // strefy (frakcje 0..1): <0.40 czerwona, 0.40–0.50 żółta, ≥0.50 zielona
            gaugeCanvas.Children.Add(LukStrefy(0.00, 0.40, "#FCA5A5"));
            gaugeCanvas.Children.Add(LukStrefy(0.40, 0.50, "#FCD34D"));
            gaugeCanvas.Children.Add(LukStrefy(0.50, 1.00, "#86EFAC"));

            // znacznik celu 50% (kreska na szczycie)
            var (tx1, ty1) = PunktNaLuku(0.50, GcR + GcGrub / 2 + 3);
            var (tx2, ty2) = PunktNaLuku(0.50, GcR - GcGrub / 2 - 3);
            gaugeCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = tx1, Y1 = ty1, X2 = tx2, Y2 = ty2,
                Stroke = Kolor("#0F172A"), StrokeThickness = 3
            });

            if (!maDane) return;

            // igła na wartości (clamp 0..100)
            double f = Math.Max(0, Math.Min(100, pct)) / 100.0;
            var (nx, ny) = PunktNaLuku(f, GcR - 6);
            gaugeCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = GcX, Y1 = GcY, X2 = nx, Y2 = ny,
                Stroke = Kolor("#0F172A"), StrokeThickness = 4,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            });
            // piasta igły
            var hub = new System.Windows.Shapes.Ellipse
            {
                Width = 16, Height = 16, Fill = Kolor("#0F172A")
            };
            Canvas.SetLeft(hub, GcX - 8); Canvas.SetTop(hub, GcY - 8);
            gaugeCanvas.Children.Add(hub);
        }

        /// <summary>Łuk strefy między frakcjami f1..f2 (0=lewa, 0.5=góra, 1=prawa).</summary>
        private static System.Windows.Shapes.Path LukStrefy(double f1, double f2, string hex)
        {
            var (sx, sy) = PunktNaLuku(f1, GcR);
            var (ex, ey) = PunktNaLuku(f2, GcR);
            var fig = new PathFigure { StartPoint = new Point(sx, sy), IsClosed = false };
            fig.Segments.Add(new ArcSegment(new Point(ex, ey), new Size(GcR, GcR), 0,
                (f2 - f1) > 0.5, SweepDirection.Clockwise, true));
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            geo.Freeze();
            return new System.Windows.Shapes.Path
            {
                Data = geo,
                Stroke = (Brush)new BrushConverter().ConvertFromString(hex)!,
                StrokeThickness = GcGrub,
                StrokeStartLineCap = PenLineCap.Flat,
                StrokeEndLineCap = PenLineCap.Flat
            };
        }

        /// <summary>Punkt na półkolu dla frakcji f (0=lewa, 0.5=szczyt, 1=prawa) i promienia r.</summary>
        private static (double x, double y) PunktNaLuku(double f, double r)
        {
            double kat = Math.PI * (1.0 - Math.Max(0, Math.Min(1, f))); // π (lewa) → 0 (prawa)
            return (GcX + r * Math.Cos(kat), GcY - r * Math.Sin(kat));
        }

        private static Border Karta(string etykieta, int wartosc, string hex, string ikona)
        {
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(new TextBlock
            {
                Text = ikona,
                FontSize = 15,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            head.Children.Add(new TextBlock
            {
                Text = wartosc.ToString(),
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Kolor(hex),
                VerticalAlignment = VerticalAlignment.Center
            });

            var sp = new StackPanel();
            sp.Children.Add(head);
            sp.Children.Add(new TextBlock
            {
                Text = etykieta,
                FontSize = 11.5,
                Foreground = Kolor("#64748B"),
                Margin = new Thickness(0, 2, 0, 0)
            });

            return new Border
            {
                Background = Kolor("#F8FAFC"),
                BorderBrush = Kolor(hex),
                BorderThickness = new Thickness(4, 0, 0, 0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 11, 14, 11),
                Margin = new Thickness(0, 0, 8, 8),
                Child = sp
            };
        }

        private static string Tony(decimal kg) => kg <= 0 ? "—" : $"{kg / 1000m:N0} t";

        private static Brush Kolor(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}
