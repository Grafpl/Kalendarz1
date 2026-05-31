using Kalendarz1.Customer360.Services;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;

namespace Kalendarz1.Customer360
{
    public partial class Customer360ScoringConfigWindow : Window
    {
        private static readonly CultureInfo Pl = new("pl-PL");

        public Customer360ScoringConfigWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            Loaded += async (s, e) => Wypelnij(await Customer360ScoringConfigStore.WczytajAsync(force: true));
        }

        private void Wypelnij(Customer360ScoringConfig c)
        {
            TbWagaObrot.Text = c.WagaObrot.ToString();
            TbWagaCzest.Text = c.WagaCzestotliwosc.ToString();
            TbWagaTermin.Text = c.WagaTerminowosc.ToString();
            TbWagaDlugosc.Text = c.WagaDlugosc.ToString();
            TbObrotMax.Text = c.ObrotNaMaxPkt.ToString("0", Pl);
            TbCzestBaza.Text = c.CzestBazaDni.ToString();
            TbCzestSpadek.Text = c.CzestSpadekNaDzien.ToString("0.0", Pl);
            TbTerminBrak.Text = c.TerminowoscBrakDanychPkt.ToString();
            TbDlugoscLata.Text = c.DlugoscLataNaMax.ToString("0.0", Pl);
            TbDlugoscMin.Text = c.DlugoscMinPkt.ToString();
            TbProgA.Text = c.ProgA.ToString();
            TbProgB.Text = c.ProgB.ToString();
            TbProgC.Text = c.ProgC.ToString();
            TbProgD.Text = c.ProgD.ToString();
            PrzeliczSumeWag();
        }

        private void Waga_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e) => PrzeliczSumeWag();

        private void PrzeliczSumeWag()
        {
            if (LblSumaWag == null) return;
            int suma = Int(TbWagaObrot) + Int(TbWagaCzest) + Int(TbWagaTermin) + Int(TbWagaDlugosc);
            LblSumaWag.Text = $"{suma}%";
            LblSumaWag.Foreground = suma == 100
                ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#16A34A")!
                : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#DC2626")!;
        }

        private static int Int(System.Windows.Controls.TextBox tb) => int.TryParse(tb.Text?.Trim(), out int v) ? v : 0;
        private static double Dbl(System.Windows.Controls.TextBox tb) => double.TryParse(tb.Text?.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0;
        private static decimal Dec(System.Windows.Controls.TextBox tb)
        {
            string s = (tb.Text ?? "").Replace(" ", "").Replace(",", ".");
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v) ? v : 0m;
        }

        private void BtnDomyslne_Click(object sender, RoutedEventArgs e) => Wypelnij(new Customer360ScoringConfig());
        private void BtnAnuluj_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            var c = new Customer360ScoringConfig
            {
                WagaObrot = Int(TbWagaObrot),
                WagaCzestotliwosc = Int(TbWagaCzest),
                WagaTerminowosc = Int(TbWagaTermin),
                WagaDlugosc = Int(TbWagaDlugosc),
                ObrotNaMaxPkt = Dec(TbObrotMax),
                CzestBazaDni = Int(TbCzestBaza),
                CzestSpadekNaDzien = Dbl(TbCzestSpadek),
                TerminowoscBrakDanychPkt = Int(TbTerminBrak),
                DlugoscLataNaMax = Dbl(TbDlugoscLata),
                DlugoscMinPkt = Int(TbDlugoscMin),
                ProgA = Int(TbProgA),
                ProgB = Int(TbProgB),
                ProgC = Int(TbProgC),
                ProgD = Int(TbProgD)
            };

            // Walidacja
            if (c.SumaWag != 100) { LblStatus.Text = "Suma wag musi wynosić 100%."; return; }
            if (c.ObrotNaMaxPkt <= 0) { LblStatus.Text = "Kwota obrotu na 100 pkt musi być > 0."; return; }
            if (!(c.ProgA > c.ProgB && c.ProgB > c.ProgC && c.ProgC > c.ProgD && c.ProgD > 0)) { LblStatus.Text = "Progi liter muszą maleć: A > B > C > D > 0."; return; }
            // Total scoringu jest w [0,100]. ProgA > 100 = nikt nigdy nie dostanie A. Zapobiega cichej katastrofie "wszyscy F".
            if (c.ProgA > 100) { LblStatus.Text = "Próg A nie może być > 100 (Total scoringu mieści się w 0–100)."; return; }

            BtnZapisz.IsEnabled = false;
            LblStatus.Foreground = System.Windows.Media.Brushes.Gray;
            LblStatus.Text = "Zapisuję…";
            bool ok = await Customer360ScoringConfigStore.ZapiszAsync(c, App.UserID ?? "?");
            BtnZapisz.IsEnabled = true;
            if (ok) { DialogResult = true; Close(); }
            else { LblStatus.Foreground = System.Windows.Media.Brushes.Red; LblStatus.Text = "❌ Błąd zapisu do bazy."; }
        }
    }
}
