using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Kalendarz1.AnalitykaPelna.Models;
using Kalendarz1.AnalitykaPelna.Services;

namespace Kalendarz1.AnalitykaPelna.Views
{
    /// <summary>
    /// Koncepcja #1: Sankey flow diagram — grubość strumienia = kg.
    /// 5 kolumn: ŻYWIEC → UBÓJ → PRODUKCJA → MAGAZYNY → KLIENCI.
    /// Strata uboju i krojenia jako osobne "branche" odpadające w bok.
    /// </summary>
    public partial class WidokSankey : UserControl
    {
        private readonly WydajnoscService _service = new();
        private FlowChainSummary _summary = new();
        private FiltryAnaliz? _ostatnieFiltry;

        // Layout constants
        private const double Col1X = 80;
        private const double Col2X = 380;
        private const double Col3X = 680;
        private const double Col4X = 980;
        private const double Col5X = 1380;
        private const double NodeWidth = 22;
        private const double CanvasH = 640;  // useful area height
        private const double TopMargin = 60;
        private const double NodeGap = 8;     // gap between stacked nodes
        private const double MinNodeH = 4;

        public WidokSankey()
        {
            InitializeComponent();
        }

        public async Task ZastosujFiltryAsync(FiltryAnaliz f)
        {
            _ostatnieFiltry = f;
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

        private void Odswiez()
        {
            var s = _summary;

            // Header z total summary
            if (_ostatnieFiltry != null)
            {
                int dni = (_ostatnieFiltry.DataDo.Date - _ostatnieFiltry.DataOd.Date).Days + 1;
                string totalT = FormatT(s.Zywiec.Kg);
                txtZakres.Text = $"{_ostatnieFiltry.DataOd:dd.MM.yyyy} – {_ostatnieFiltry.DataDo:dd.MM.yyyy}  ·  {dni} dni  ·  Σ {totalT} t żywca w łańcuchu  ·  {s.LiczbaDokumentowCalkowita:N0} dok.";
            }
            txtFooter.Text = $"Skala: 1 piksel = ~{SkalaPxNaKg(s):F1} kg  ·  Odświeżono {DateTime.Now:HH:mm:ss}";

            // Tooltipy z kg + % — dla wszystkich flows
            if (s.Zywiec.Kg > 0)
            {
                flowZywiecUboj.ToolTip = $"Żywiec → Ubój:  {FormatT(s.Uboj.Kg)} t  ({s.WydajnoscUbojuProc:F1}% wyd.)";
                flowZywiecStrata.ToolTip = $"Strata uboju (pióra/krew/woda):  {FormatT(s.StratyUbojuKg)} t  ({s.StratyUbojuProc:F1}%)";
                flowUbojKrojenie.ToolTip = $"Ubój → Krojenie (sRWP):  {FormatT(s.RozchodKrojenia.Kg)} t  ({s.UbojDoKrojeniaProc:F1}% uboju)";
                flowUbojBezposrednio.ToolTip = $"Ubój → Sortownia (całe tuszki):  {FormatT(s.UbojBezKrojeniaKg)} t  ({s.UbojBezKrojeniaProc:F1}% uboju)";
                flowKrojenieStrata.ToolTip = $"Strata krojenia (kości/ścinki):  {FormatT(s.StrataKrojeniaKg)} t  ({s.StrataKrojeniaProc:F1}%)";
                flowProdDyst.ToolTip = $"PROD → Dystrybucja:  {FormatT(s.Dystrybucja.Kg)} t  ({s.ProcDoDystProc:F1}% PROD)";
                flowProdMroz.ToolTip = $"PROD → Mroźnia:  {FormatT(s.Mroznia.Kg)} t  ({s.ProcDoMrozniProc:F1}%)";
                flowProdMasar.ToolTip = $"PROD → Masarnia:  {FormatT(s.Masarnia.Kg)} t  ({s.ProcDoMasarniProc:F1}%)";
                flowProdKarma.ToolTip = $"PROD → Karma:  {FormatT(s.Karma.Kg)} t  ({s.ProcDoKarmyProc:F1}%)";
                flowProdOdpady.ToolTip = $"PROD → Odpady:  {FormatT(s.Odpady.Kg)} t  ({s.ProcDoOdpadowProc:F1}%)";
                flowProdZostalo.ToolTip = $"Pozostało w PROD:  {FormatT(Math.Max(0m, s.ZostaloProdKg))} t  ({s.ZostaloProdProc:F1}%)";
                flowDystKlienci.ToolTip = $"DYST → Klienci:  {FormatT(s.Klienci.Kg)} t  ({s.ProcSprzedanoProc:F1}% sprzedane)";
                flowBezpDyst.ToolTip = $"Całe tuszki → DYST:  {FormatT(s.UbojBezKrojeniaKg)} t";
            }

            if (s.Zywiec.Kg <= 0)
            {
                CzyscWszystko();
                return;
            }

            // ════════════════════════════════════════════════════════════════
            // SKALOWANIE — największa wartość (Żywiec) = MaxH px wysokości
            // ════════════════════════════════════════════════════════════════
            const double MaxH = 500;
            double skala = MaxH / (double)s.Zywiec.Kg;  // kg → px

            // ════════════════════════════════════════════════════════════════
            // KOL 1: ŻYWIEC (1 nod)
            // ════════════════════════════════════════════════════════════════
            double zywiecH = (double)s.Zywiec.Kg * skala;
            double zywiecTop = TopMargin + (MaxH - zywiecH) / 2;
            UstawNode(nodeZywiec, lblZywiec, Col1X, zywiecTop, zywiecH,
                $"🐔 ŻYWIEC\n{FormatT(s.Zywiec.Kg)} t", leftLabel: true);

            // ════════════════════════════════════════════════════════════════
            // KOL 2: UBÓJ + STRATA UBOJU
            // ════════════════════════════════════════════════════════════════
            double ubojH = (double)s.Uboj.Kg * skala;
            double strataH = Math.Max(MinNodeH, (double)s.StratyUbojuKg * skala);

            // STRATA na górze, UBÓJ pod nią — razem zajmują tyle co ŻYWIEC
            double col2Top = zywiecTop;
            UstawNode(nodeStrata, lblStrata, Col2X, col2Top, strataH,
                $"↓ strata {FormatT(s.StratyUbojuKg)} t ({s.StratyUbojuProc:F1}%)", small: true);
            double ubojTop = col2Top + strataH + NodeGap;
            UstawNode(nodeUboj, lblUboj, Col2X, ubojTop, ubojH,
                $"⚙ UBÓJ\n{FormatT(s.Uboj.Kg)} t · {s.WydajnoscUbojuProc:F1}%");

            // Flow ŻYWIEC → UBÓJ (główny)
            RysujSankey(flowZywiecUboj, Col1X + NodeWidth, zywiecTop, zywiecH - strataH,
                                          Col2X, ubojTop, ubojH);
            // Flow ŻYWIEC → STRATA (mały, w górę)
            RysujSankey(flowZywiecStrata, Col1X + NodeWidth, zywiecTop + (zywiecH - strataH), strataH,
                                            Col2X, col2Top, strataH);

            // ════════════════════════════════════════════════════════════════
            // KOL 3: PROD + UBOJ-BEZP + STRATA-KROJ
            // Z UBOJU rozdział: część (sRWP) idzie na krojenie → PROD, część bezpośrednio
            // ════════════════════════════════════════════════════════════════
            double rwpH = (double)s.RozchodKrojenia.Kg * skala;
            double prodH = (double)s.Produkcja.Kg * skala;
            double strataKrojH = Math.Max(MinNodeH, (double)s.StrataKrojeniaKg * skala);
            double bezpH = (double)s.UbojBezKrojeniaKg * skala;

            // Layout col 3 od góry: STRATA-KROJ, PROD, BEZP
            double col3Top = ubojTop;
            UstawNode(nodeStrataKroj, lblStrataKroj, Col3X, col3Top, strataKrojH,
                $"↓ strata krojenia {FormatT(s.StrataKrojeniaKg)} t", small: true);
            double prodTop = col3Top + strataKrojH + NodeGap;
            UstawNode(nodeProd, lblProd, Col3X, prodTop, prodH,
                $"🔪 PROD\n{FormatT(s.Produkcja.Kg)} t · {s.WydajnoscKrojeniaProc:F1}%");
            double bezpTop = prodTop + prodH + NodeGap;
            UstawNode(nodeUbojBezp, lblUbojBezp, Col3X, bezpTop, bezpH,
                $"📦 całe tuszki {FormatT(s.UbojBezKrojeniaKg)} t");

            // Flow UBÓJ → strata krojenia (małe, do góry)
            RysujSankey(flowKrojenieStrata, Col2X + NodeWidth, ubojTop, strataKrojH,
                                              Col3X, col3Top, strataKrojH);
            // Flow UBÓJ → PROD (główny)
            RysujSankey(flowUbojKrojenie, Col2X + NodeWidth, ubojTop + strataKrojH, prodH,
                                            Col3X, prodTop, prodH);
            // Flow UBÓJ → BEZP
            RysujSankey(flowUbojBezposrednio, Col2X + NodeWidth, ubojTop + strataKrojH + prodH, bezpH,
                                                Col3X, bezpTop, bezpH);

            // ════════════════════════════════════════════════════════════════
            // KOL 4: 7 wyjść — DYST, MROŹ, MASAR, KARMA, ODPADY, ZOSTAŁO
            // ════════════════════════════════════════════════════════════════
            decimal zostaloKg = Math.Max(0m, s.ZostaloProdKg);
            double dystH = (double)(s.Dystrybucja.Kg + s.UbojBezKrojeniaKg) * skala;  // DYST przyjmuje też bezpośr.
            double mrozH = Math.Max(MinNodeH, (double)s.Mroznia.Kg * skala);
            double masarH = Math.Max(MinNodeH, (double)s.Masarnia.Kg * skala);
            double karmaH = Math.Max(MinNodeH, (double)s.Karma.Kg * skala);
            double odpadyH = Math.Max(MinNodeH, (double)s.Odpady.Kg * skala);
            double zostaloH = Math.Max(MinNodeH, (double)zostaloKg * skala);

            // Layout col 4 — DYST na górze, potem MROŹ/MASAR/KARMA/ODPADY/ZOSTAŁO
            double col4Top = col3Top;
            UstawNode(nodeDyst, lblDyst, Col4X, col4Top, dystH,
                $"📦 DYST\n{FormatT(s.Dystrybucja.Kg + s.UbojBezKrojeniaKg)} t");
            double mrozTop = col4Top + dystH + NodeGap;
            UstawNode(nodeMroz, lblMroz, Col4X, mrozTop, mrozH,
                $"❄ MROŹ {FormatT(s.Mroznia.Kg)} t ({s.ProcDoMrozniProc:F1}%)");
            double masarTop = mrozTop + mrozH + NodeGap;
            UstawNode(nodeMasar, lblMasar, Col4X, masarTop, masarH,
                $"🥓 MASAR {FormatT(s.Masarnia.Kg)} t ({s.ProcDoMasarniProc:F1}%)");
            double karmaTop = masarTop + masarH + NodeGap;
            UstawNode(nodeKarma, lblKarma, Col4X, karmaTop, karmaH,
                $"🌾 KARMA {FormatT(s.Karma.Kg)} t ({s.ProcDoKarmyProc:F1}%)");
            double odpadyTop = karmaTop + karmaH + NodeGap;
            UstawNode(nodeOdpady, lblOdpady, Col4X, odpadyTop, odpadyH,
                $"🗑 ODPADY {FormatT(s.Odpady.Kg)} t ({s.ProcDoOdpadowProc:F1}%)");
            double zostaloTop = odpadyTop + odpadyH + NodeGap;
            UstawNode(nodeZostalo, lblZostalo, Col4X, zostaloTop, zostaloH,
                $"📍 zostało {FormatT((decimal)zostaloKg)} t");

            // Flows from PROD do MROŹ/MASAR/KARMA/ODPADY/ZOSTAŁO
            // Z PROD odprowadzamy proporcjonalne fragmenty (max prodH)
            double prodOut = (double)s.Produkcja.Kg * skala;  // == prodH
            decimal sumProdDestKg = s.Dystrybucja.Kg + s.Mroznia.Kg + s.Masarnia.Kg + s.Karma.Kg + s.Odpady.Kg + zostaloKg;
            double sumProdDest = (double)sumProdDestKg * skala;
            double prodScale = sumProdDest > 0 ? prodOut / sumProdDest : 1.0;

            double dystFromProd = (double)s.Dystrybucja.Kg * skala * prodScale;
            double mrozFromProd = mrozH * prodScale;
            double masarFromProd = masarH * prodScale;
            double karmaFromProd = karmaH * prodScale;
            double odpadyFromProd = odpadyH * prodScale;
            double zostaloFromProd = zostaloH * prodScale;

            double srcY = prodTop;
            RysujSankey(flowProdDyst, Col3X + NodeWidth, srcY, dystFromProd,
                                       Col4X, col4Top, dystFromProd);
            srcY += dystFromProd;
            RysujSankey(flowProdMroz, Col3X + NodeWidth, srcY, mrozFromProd,
                                       Col4X, mrozTop, mrozH);
            srcY += mrozFromProd;
            RysujSankey(flowProdMasar, Col3X + NodeWidth, srcY, masarFromProd,
                                        Col4X, masarTop, masarH);
            srcY += masarFromProd;
            RysujSankey(flowProdKarma, Col3X + NodeWidth, srcY, karmaFromProd,
                                        Col4X, karmaTop, karmaH);
            srcY += karmaFromProd;
            RysujSankey(flowProdOdpady, Col3X + NodeWidth, srcY, odpadyFromProd,
                                         Col4X, odpadyTop, odpadyH);
            srcY += odpadyFromProd;
            RysujSankey(flowProdZostalo, Col3X + NodeWidth, srcY, zostaloFromProd,
                                          Col4X, zostaloTop, zostaloH);

            // BEZP → DYST (dodatkowy nurt do DYST — osobny Path żeby nie nadpisywać UBÓJ→BEZP)
            RysujSankey(flowBezpDyst, Col3X + NodeWidth, bezpTop, bezpH,
                                       Col4X, col4Top + dystFromProd, bezpH);

            // ════════════════════════════════════════════════════════════════
            // KOL 5: KLIENCI (otrzymuje z DYST)
            // ════════════════════════════════════════════════════════════════
            double klienciH = Math.Max(MinNodeH, (double)s.Klienci.Kg * skala);
            double klienciTop = col4Top + (dystH - klienciH) / 2;  // wycentruj wobec DYST
            UstawNode(nodeKlienci, lblKlienci, Col5X, klienciTop, klienciH,
                $"🚚 KLIENCI\n{FormatT(s.Klienci.Kg)} t · {s.ProcSprzedanoProc:F1}%", leftLabel: false);

            RysujSankey(flowDystKlienci, Col4X + NodeWidth, klienciTop, klienciH,
                                          Col5X, klienciTop, klienciH);
        }

        // ════════════════════════════════════════════════════════════════
        // Helpers — pozycjonowanie nodów i rysowanie flows
        // ════════════════════════════════════════════════════════════════
        private static void UstawNode(Border node, TextBlock label, double x, double y, double h,
                                       string text, bool small = false, bool leftLabel = false)
        {
            // Defensywnie wobec NaN/Infinity z arytmetyki (skala * 0, division by zero itp.)
            if (double.IsNaN(h) || double.IsInfinity(h) || h < MinNodeH) h = MinNodeH;
            if (double.IsNaN(x) || double.IsInfinity(x)) x = 0;
            if (double.IsNaN(y) || double.IsInfinity(y)) y = 0;
            Canvas.SetLeft(node, x);
            Canvas.SetTop(node, y);
            node.Height = h;

            label.Text = text;
            label.TextWrapping = TextWrapping.Wrap;
            label.MaxWidth = 200;
            // Label po prawej stronie node — jeśli node jest na ostatniej kolumnie, label po lewej
            Canvas.SetLeft(label, leftLabel ? x - 210 : x + NodeWidth + 8);
            Canvas.SetTop(label, y + (h / 2) - (small ? 8 : 18));
            label.TextAlignment = leftLabel ? TextAlignment.Right : TextAlignment.Left;
        }

        /// <summary>Rysuje Sankey "tube" — wypełniona forma między dwoma punktami z Bezier curve.</summary>
        private static void RysujSankey(System.Windows.Shapes.Path path, double x1, double y1, double h1,
                                                                          double x2, double y2, double h2)
        {
            // Defensywnie: skip jeśli NaN/Infinity/za mały
            if (double.IsNaN(h1) || double.IsNaN(h2) || double.IsInfinity(h1) || double.IsInfinity(h2)
                || double.IsNaN(x1) || double.IsNaN(x2) || double.IsNaN(y1) || double.IsNaN(y2)
                || h1 < 0.5 || h2 < 0.5) { path.Data = null; return; }

            double cx = (x2 - x1) * 0.5;
            var geom = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(x1, y1), IsClosed = true, IsFilled = true };

            // Top edge: bezier from (x1, y1) to (x2, y2)
            fig.Segments.Add(new BezierSegment(
                new Point(x1 + cx, y1),
                new Point(x2 - cx, y2),
                new Point(x2, y2),
                isStroked: false));
            // Right edge: line to (x2, y2+h2)
            fig.Segments.Add(new LineSegment(new Point(x2, y2 + h2), isStroked: false));
            // Bottom edge: bezier from (x2, y2+h2) back to (x1, y1+h1)
            fig.Segments.Add(new BezierSegment(
                new Point(x2 - cx, y2 + h2),
                new Point(x1 + cx, y1 + h1),
                new Point(x1, y1 + h1),
                isStroked: false));
            // Left edge: line closes back to start

            geom.Figures.Add(fig);
            path.Data = geom;
        }

        private void CzyscWszystko()
        {
            foreach (var p in new[] { flowZywiecUboj, flowZywiecStrata, flowUbojKrojenie, flowUbojBezposrednio,
                                       flowKrojenieStrata, flowProdDyst, flowProdMroz, flowProdMasar,
                                       flowProdKarma, flowProdOdpady, flowProdZostalo, flowDystKlienci, flowBezpDyst })
                p.Data = null;

            foreach (var n in new[] { nodeZywiec, nodeUboj, nodeStrata, nodeProd, nodeUbojBezp, nodeStrataKroj,
                                       nodeDyst, nodeMroz, nodeMasar, nodeKarma, nodeOdpady, nodeZostalo, nodeKlienci })
                n.Height = 0;

            foreach (var l in new[] { lblZywiec, lblUboj, lblStrata, lblProd, lblUbojBezp, lblStrataKroj,
                                       lblDyst, lblMroz, lblMasar, lblKarma, lblOdpady, lblZostalo, lblKlienci })
                l.Text = "";
        }

        private static string FormatT(decimal kg)
        {
            if (kg == 0) return "0";
            if (Math.Abs(kg) < 1000m) return $"{kg:N0} kg";
            decimal tony = kg / 1000m;
            return tony >= 100 ? $"{tony:N0}" : $"{tony:N1}";
        }

        private double SkalaPxNaKg(FlowChainSummary s) =>
            s.Zywiec.Kg > 0 ? (double)s.Zywiec.Kg / 500.0 : 1.0;

        // ════════════════════════════════════════════════════════════════
        // Click handlers
        // ════════════════════════════════════════════════════════════════
        private async void Node_Click(object sender, MouseButtonEventArgs e) => await OtworzSzczegoly(sender);
        private async void Flow_Click(object sender, MouseButtonEventArgs e) => await OtworzSzczegoly(sender);

        private async Task OtworzSzczegoly(object sender)
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
