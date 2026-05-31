using System;
using System.Text;
using System.Windows;
using Kalendarz1.DashboardPrzychodu.Models;

namespace Kalendarz1.DashboardPrzychodu.Windows
{
    /// <summary>
    /// Okno szczegolow pojedynczej dostawy z dashboardu LIVE.
    /// Wczesniej zbudowane reka w ShowDeliveryDetails (~600 linii UIElement-builder)
    /// teraz czyste XAML + bindingi do DostawaItem (#12 fix).
    /// </summary>
    public partial class DeliveryDetailsWindow : Window
    {
        private readonly DostawaItem _dostawa;

        public DeliveryDetailsWindow(DostawaItem dostawa)
        {
            InitializeComponent();
            _dostawa = dostawa ?? throw new ArgumentNullException(nameof(dostawa));
            DataContext = _dostawa;
            Title = $"Szczegoly dostawy #{_dostawa.NrKursu} - {_dostawa.Hodowca}";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var d = _dostawa;
                var sb = new StringBuilder();
                sb.AppendLine($"DOSTAWA #{d.NrKursu} - {d.Hodowca}");
                sb.AppendLine($"Data: {d.Data:dd.MM.yyyy}  |  Status: {d.StatusText}");
                sb.AppendLine(new string('-', 50));
                sb.AppendLine($"LP dostawy: {d.LpDostawy?.ToString() ?? "-"}");
                sb.AppendLine($"Plan lacznie: {d.PlanSztukiLacznie:N0} szt / {d.PlanKgLacznie:N0} kg / {d.AutaPlanowane} aut");
                sb.AppendLine($"Waga deklar.: {d.WagaDeklHarmonogram?.ToString("N3") ?? "-"} kg/szt");
                sb.AppendLine($"Plan na auto: {d.SztukiPlanNaAuto:N0} szt / {d.KgPlanNaAuto:N0} kg");
                sb.AppendLine(new string('-', 50));
                sb.AppendLine($"Brutto: {d.Brutto:N0} kg  |  Tara: {d.Tara:N0} kg  |  Netto: {d.KgRzeczywiste:N0} kg");
                sb.AppendLine($"Sztuki: {d.SztukiRzeczywiste:N0}  |  SztExcel: {d.SztukiExcel:N0}");
                sb.AppendLine($"Padle: {d.Padle}  |  Konfiskaty: {d.Konfiskaty}");
                sb.AppendLine($"Sr. waga: {d.SredniaWagaRzeczywistaCalc?.ToString("N3") ?? "-"} kg  (dekl: {d.WagaDeklHarmonogram?.ToString("N3") ?? "-"})");
                sb.AppendLine(new string('-', 50));
                sb.AppendLine($"Odchylenie vs plan-auto:   {d.OdchylenieVsPlanAutoDisplay}");
                sb.AppendLine($"Odchylenie vs deklaracja:  {d.OdchylenieVsDeklHodowcaDisplay}");
                sb.AppendLine($"Ocena: {d.Poziom}");
                sb.AppendLine($"Postep: {d.PostepDisplay}  |  Trend: {d.TrendHodowcy}");
                sb.AppendLine($"Tuszki plan: {d.TuszkiPlanKg:N0} kg  |  rzecz: {d.TuszkiRzeczywisteKg?.ToString("N0") ?? "-"} kg");
                sb.AppendLine($"Rozmiar: {d.RozmiarDisplay}  |  Szt/poj: {d.SztukWPojemniku?.ToString("N2") ?? "-"}");
                sb.AppendLine($"Przyjazd: {d.PrzyjazdDisplay}  |  Wazenie: {d.GodzinaWazeniaDisplay}  |  Wazyl: {d.KtoWazyl ?? "-"}");

                Clipboard.SetText(sb.ToString());
                BtnCopy.Content = "Skopiowano!";
            }
            catch { }
        }
    }
}
