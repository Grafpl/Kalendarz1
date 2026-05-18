using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.AnalitykaPelna.Models;

namespace Kalendarz1.AnalitykaPelna.Windows
{
    public partial class SzczegolyWydajnosciDialog : Window
    {
        public SzczegolyWydajnosciDialog(WydajnoscDzien d)
        {
            InitializeComponent();
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

            txtTytul.Text = $"📊 Wydajność dnia {d.Data:dd.MM.yyyy} ({d.DzienTygodnia})";
            txtPodtytul.Text = "Pełen rozkład wyliczenia: żywiec → tuszki + podroby";

            txtZywiecPz.Text = $"{d.ZywiecKg:N0} kg";
            txtZywiecRwu.Text = $"{d.ZywiecRwuKg:N0} kg";

            txtTuszkaA.Text = $"{d.TuszkaAKg:N0} kg";
            txtTuszkaB.Text = $"{d.TuszkaBKg:N0} kg";
            txtWatroba.Text = $"{d.WatrobaKg:N0} kg";
            txtZoladki.Text = $"{d.ZoladkiKg:N0} kg";
            txtSerce.Text = $"{d.SerceKg:N0} kg";

            txtTuszkaAProc.Text = Proc(d.TuszkaAKg, d.ZywiecKg);
            txtTuszkaBProc.Text = Proc(d.TuszkaBKg, d.ZywiecKg);
            txtWatrobaProc.Text = Proc(d.WatrobaKg, d.ZywiecKg);
            txtZoladkiProc.Text = Proc(d.ZoladkiKg, d.ZywiecKg);
            txtSerceProc.Text = Proc(d.SerceKg, d.ZywiecKg);

            txtSumaWyj.Text = $"{d.SumaWyjscia:N0} kg";

            // Wzór z podrobami
            string sumaABP = $"({d.TuszkaAKg:N0} + {d.TuszkaBKg:N0} + {d.PodrobyKg:N0})";
            txtWzorZPodrobami.Text = $"= {sumaABP} kg / {d.ZywiecKg:N0} kg × 100";
            txtWynikZPodrobami.Text = d.ZywiecKg <= 0 ? "—" : $"= {d.WydajnoscZPodrobamiProc:F2} %";

            // Wzór bez podrobów
            string sumaAB = $"({d.TuszkaAKg:N0} + {d.TuszkaBKg:N0})";
            txtWzorBezPodrobow.Text = $"= {sumaAB} kg / {d.ZywiecKg:N0} kg × 100";
            txtWynikBezPodrobow.Text = d.ZywiecKg <= 0 ? "—" : $"= {d.WydajnoscBezPodrobowProc:F2} %";

            // Wzór % podrobów
            string podrobyRozb = $"(wątr.{d.WatrobaKg:N0} + żoł.{d.ZoladkiKg:N0} + serce {d.SerceKg:N0})";
            txtWzorPodrobow.Text = $"= {podrobyRozb} = {d.PodrobyKg:N0} kg / {d.ZywiecKg:N0} kg × 100";
            txtWynikPodrobow.Text = d.ZywiecKg <= 0 ? "—" : $"= {d.WydajnoscPodrobowProc:F2} %";

            // Strata
            decimal strataKg = d.ZywiecKg - d.SumaWyjscia;
            decimal strataProc = d.ZywiecKg <= 0 ? 0 : strataKg / d.ZywiecKg * 100m;
            txtWzorStraty.Text = $"= {d.ZywiecKg:N0} kg − {d.SumaWyjscia:N0} kg";
            txtWynikStraty.Text = d.ZywiecKg <= 0 ? "—"
                : $"= {strataKg:N0} kg ({strataProc:F2} %)";
        }

        private static string Proc(decimal kg, decimal zywiec)
            => zywiec <= 0 ? "—" : (kg / zywiec * 100m).ToString("F2", CultureInfo.InvariantCulture) + "%";

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();
    }
}
