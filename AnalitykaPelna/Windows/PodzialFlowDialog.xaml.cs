using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kalendarz1.AnalitykaPelna.Models;

namespace Kalendarz1.AnalitykaPelna.Windows
{
    /// <summary>
    /// Lekki dialog pokazujący rozdział przepływu w 2 trybach:
    /// • CaleTuszki: co poszło z UBÓJ bezpośrednio (omijając krojenie)
    /// • Pakowanie: co spakowano i rozdysponowano sMM- do 5 magazynów
    /// </summary>
    public partial class PodzialFlowDialog : Window
    {
        public enum Tryb { CaleTuszki, Pakowanie }

        public PodzialFlowDialog(FlowChainSummary s, Tryb tryb)
        {
            InitializeComponent();
            if (tryb == Tryb.CaleTuszki) WypelnijCaleTuszki(s);
            else WypelnijPakowanie(s);
        }

        private void WypelnijCaleTuszki(FlowChainSummary s)
        {
            Title = "Całe tuszki sPWU";
            txtTytul.Text = "Całe tuszki (sPWU) — co poszło bezpośrednio";
            txtPodtytul.Text = "Część uboju która ominęła krojenie i poszła wprost do pakowania jako gotowy towar";

            // KPI 1: Ubój total (sPWU)
            kpi1Label.Text = "WYJŚCIE UBOJU (sPWU)";
            kpi1Value.Text = FormatT(s.Uboj.Kg);
            kpi1Unit.Text = "t";
            kpi1Sub.Text = "100% — wszystko z linii uboju";

            // KPI 2: Na krojenie (sRWP)
            kpi2Label.Text = "POSZŁO NA KROJENIE (sRWP)";
            kpi2Value.Text = FormatT(s.RozchodKrojenia.Kg);
            kpi2Unit.Text = "t";
            kpi2Sub.Text = s.Uboj.Kg > 0 ? $"{s.UbojDoKrojeniaProc:F1}% — wsad do hali krojenia" : "—";

            // KPI 3: Bezpośrednio (różnica) — całe tuszki
            kpi3Label.Text = "CAŁE TUSZKI (bezpośrednio)";
            kpi3Value.Text = FormatT(s.UbojBezKrojeniaKg);
            kpi3Unit.Text = "t";
            kpi3Sub.Text = s.Uboj.Kg > 0 ? $"{s.UbojBezKrojeniaProc:F1}% — omijają krojenie, idą do pakowania" : "—";
            // Wyróżnij KPI 3 — to jest "to co użytkownik chce zobaczyć"
            kpi3Wrap.BorderBrush = new SolidColorBrush(Color.FromRgb(0x0E, 0xA5, 0xE9));
            kpi3Wrap.BorderThickness = new Thickness(2);

            txtOpis.Text =
                $"Z {FormatT(s.Uboj.Kg)} t towaru wyjścia uboju (sPWU), do hali krojenia trafiło {FormatT(s.RozchodKrojenia.Kg)} t " +
                $"({s.UbojDoKrojeniaProc:F1}%) jako rozchód wewnętrzny (sRWP).\n\n" +
                $"Pozostałe {FormatT(s.UbojBezKrojeniaKg)} t ({s.UbojBezKrojeniaProc:F1}%) to całe tuszki — sprzedawane bez krojenia. " +
                $"Pakowane są jako gotowy towar (kartony / pojemniki E2) i przesuwane przez sMM− bezpośrednio do magazynów docelowych " +
                $"(głównie DYSTRYBUCJA, czasem MROŹNIA).";

            bdBreakdown.Visibility = Visibility.Collapsed;
            txtFooterInfo.Text = "Klucz: sPWU = sRWP + całe tuszki";
        }

        private void WypelnijPakowanie(FlowChainSummary s)
        {
            Title = "Pakowanie — co spakowano i gdzie poszło";
            txtTytul.Text = "Pakowanie — bilans końcowy";
            txtPodtytul.Text = "Suma elementów z krojenia + całych tuszek z uboju → rozdysponowanie sMM− do 5 magazynów";

            decimal sumPak = s.Dystrybucja.Kg + s.Mroznia.Kg + s.Masarnia.Kg + s.Karma.Kg + s.Odpady.Kg;

            // KPI 1: z krojenia (sPWP)
            kpi1Label.Text = "Z KROJENIA (sPWP)";
            kpi1Value.Text = FormatT(s.Produkcja.Kg);
            kpi1Unit.Text = "t";
            kpi1Sub.Text = "elementy: filet, korpus, skrzydło...";

            // KPI 2: całe tuszki (sPWU bezp.)
            kpi2Label.Text = "CAŁE TUSZKI (sPWU)";
            kpi2Value.Text = FormatT(s.UbojBezKrojeniaKg);
            kpi2Unit.Text = "t";
            kpi2Sub.Text = "ominęły krojenie (gotowy towar)";

            // KPI 3: RAZEM spakowano
            kpi3Label.Text = "RAZEM SPAKOWANO (sMM−)";
            kpi3Value.Text = FormatT(sumPak);
            kpi3Unit.Text = "t";
            kpi3Sub.Text = "rozesłane do 5 magazynów";
            kpi3Wrap.BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            kpi3Wrap.BorderThickness = new Thickness(2);

            txtOpis.Text =
                $"Do pakowania trafiły 2 strumienie: {FormatT(s.Produkcja.Kg)} t elementów po krojeniu (sPWP) " +
                $"oraz {FormatT(s.UbojBezKrojeniaKg)} t całych tuszek (sPWU bezpośrednio).\n\n" +
                $"Towar pakowany jest do pojemników E2, kartonów i folii, następnie dokumenty sMM− przesuwają go do magazynów docelowych. " +
                $"Z DYSTRYBUCJI {FormatT(s.Klienci.Kg)} t ({s.ProcSprzedanoProc:F1}%) trafiło dalej do klientów (sWZ).";

            // Breakdown 5 magazynów
            bdBreakdown.Visibility = Visibility.Visible;
            txtBreakdownTitle.Text = "↓ DOKĄD — ROZDYSPONOWANIE sMM−";
            panelBreakdown.Children.Clear();
            DodajBreakdownWiersz("📦", "DYSTRYBUCJA", "→ 65556 (M.DYST)", s.Dystrybucja.Kg, sumPak, "#2563EB");
            DodajBreakdownWiersz("❄", "MROŹNIA", "→ 65552 (M.MROŹ)", s.Mroznia.Kg, sumPak, "#0EA5E9");
            DodajBreakdownWiersz("🥓", "MASARNIA", "→ 65562 (M.MASAR)", s.Masarnia.Kg, sumPak, "#9A3412");
            DodajBreakdownWiersz("🌾", "KARMA", "→ 65547 (M.KARMA)", s.Karma.Kg, sumPak, "#CA8A04");
            DodajBreakdownWiersz("🗑", "ODPADY", "→ 65551 (M.ODPA)", s.Odpady.Kg, sumPak, "#475569");

            txtFooterInfo.Text = $"sPWP + sPWU bezp. = {FormatT(s.Produkcja.Kg + s.UbojBezKrojeniaKg)} t  ·  sMM− razem: {FormatT(sumPak)} t";
        }

        private void DodajBreakdownWiersz(string ikona, string nazwa, string magazyn, decimal kg, decimal total, string colorHex)
        {
            var row = new Border
            {
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 3, 0, 3),
                Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)),
                CornerRadius = new CornerRadius(5)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            // Ikona
            var ikonaTb = new TextBlock { Text = ikona, FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
            grid.Children.Add(ikonaTb);
            Grid.SetColumn(ikonaTb, 0);

            // Nazwa + magazyn
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = nazwa, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)) });
            sp.Children.Add(new TextBlock { Text = magazyn, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)) });
            grid.Children.Add(sp);
            Grid.SetColumn(sp, 1);

            // Pasek proporcjonalny
            decimal proc = total > 0 ? kg / total * 100m : 0;
            var barWrap = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                CornerRadius = new CornerRadius(4),
                Height = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 12, 0)
            };
            try
            {
                var kolor = (Color)ColorConverter.ConvertFromString(colorHex)!;
                var pasek = new Grid();
                pasek.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength((double)proc, GridUnitType.Star) });
                pasek.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.01, 100 - (double)proc), GridUnitType.Star) });
                var fill = new Border { Background = new SolidColorBrush(kolor), CornerRadius = new CornerRadius(4) };
                Grid.SetColumn(fill, 0);
                pasek.Children.Add(fill);
                barWrap.Child = pasek;
            }
            catch { }
            grid.Children.Add(barWrap);
            Grid.SetColumn(barWrap, 2);

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
            Grid.SetColumn(kgTb, 3);

            // %
            var procTb = new TextBlock
            {
                Text = $"{proc:F1}%",
                FontSize = 11,
                FontFamily = new FontFamily("Segoe UI"),
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))
            };
            grid.Children.Add(procTb);
            Grid.SetColumn(procTb, 4);

            row.Child = grid;
            panelBreakdown.Children.Add(row);
        }

        private static string FormatT(decimal kg)
        {
            if (kg == 0) return "—";
            if (Math.Abs(kg) < 1000m) return $"{kg:N0}";
            decimal tony = kg / 1000m;
            return tony >= 100 ? $"{tony:N0}" : $"{tony:N1}";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
