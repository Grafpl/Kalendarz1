using System.Linq;
using System.Windows;
using Kalendarz1.DashboardPrzychodu.Models;

namespace Kalendarz1.DashboardPrzychodu.Views
{
    /// <summary>
    /// #11 - Tryb "Stare" vs "Nowe" planu (per-auto plan kg).
    /// "Nowe" liczy plan na auto z SztukiExcel × WagaDek z harmonogramu,
    /// a ostatnie auto dostaje reszte (zeby suma sie zgadzala).
    /// "Stare" przywraca plan proporcjonalny z PrzychodService.
    /// </summary>
    public partial class DashboardPrzychoduWindow
    {
        /// <summary>
        /// Obsluga zmiany trybu planu (Stare/Nowe).
        /// </summary>
        private void RbPlanMode_Checked(object sender, RoutedEventArgs e)
        {
            if (rbPlanNowe == null || rbPlanStare == null) return; // jeszcze nie zainicjalizowane
            // IsChecked="True" w XAML pali ten event podczas InitializeComponent, ZANIM konstruktor
            // zdazyl utworzyc kolekcje i podsumowanie. Pomijamy - LoadDataAsync i tak wywola refresh.
            if (_dostawy == null || _podsumowanie == null) return;

            _useNowyPlan = rbPlanNowe.IsChecked == true;

            if (_useNowyPlan)
                RecalculateNowyPlan();
            else
                ClearNowyPlan();

            UpdateSummaryUI();
            UpdateTableSummary();

            // Persystencja (#9)
            SaveSettings();
        }

        /// <summary>
        /// Tryb "Nowe": per-auto plan = SztukiExcel × WagaDek(harmonogram),
        /// ostatnie auto w grupie = reszta z harmonogramu (tylko gdy NIE ma overflow).
        ///
        /// PRZYPADEK OVERFLOW (np. Łapiak Monika - 3 auta wjechały, plan na 2):
        /// Reguła "ostatnie = reszta" daje 0 kg planu dla trzeciego auta, bo poprzednie
        /// dwie wyczerpały plan. Dlatego: GDY items.Count > AutaPlanowane,
        /// wszystkie auta dostają zwykłe SztukiExcel × WagaDek bez "reszty".
        /// </summary>
        private void RecalculateNowyPlan()
        {
            var groups = _dostawy
                .Where(d => d.LpDostawy.HasValue)
                .GroupBy(d => d.LpDostawy.Value);

            foreach (var group in groups)
            {
                var items = group.OrderBy(d => d.NrKursu).ToList();
                decimal planKgLacznie = items.First().PlanKgLacznie;
                decimal wagaDekl = items.First().WagaDeklHarmonogram ?? 0;
                int autaPlan = items.First().AutaPlanowane;

                // OVERFLOW: wjechało więcej aut niż w harmonogramie - wszystkie dostają
                // własny SztukiExcel × WagaDek (bez reguły "ostatnie = reszta")
                bool overflow = items.Count > autaPlan;

                decimal sumaAssigned = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    if (!overflow && i == items.Count - 1)
                    {
                        // Standard: ostatnie auto = reszta z harmonogramu (zgadza się suma)
                        items[i].NowyPlanKg = planKgLacznie - sumaAssigned;
                    }
                    else
                    {
                        decimal planNaAuto = items[i].SztukiExcel * wagaDekl;
                        items[i].NowyPlanKg = planNaAuto;
                        sumaAssigned += planNaAuto;
                    }
                }
            }

            // Rekordy bez LpDostawy: SztukiExcel × WagaDek z FarmerCalc,
            // z fallbackiem do item.KgPlan (które ma już NettoFarmWeight z SQL gdy istnieje)
            foreach (var item in _dostawy.Where(d => !d.LpDostawy.HasValue))
            {
                decimal wagaDekl = item.WagaDeklHarmonogram ?? item.SredniaWagaPlan ?? 0;
                decimal obliczony = item.SztukiExcel * wagaDekl;
                // Jeśli SztExcel=0 lub waga=NULL → użyj planu z SQL (NettoFarmWeight per auto)
                item.NowyPlanKg = obliczony > 0 ? obliczony : item.KgPlan;
            }

            RecalculateNowySummary();
        }

        /// <summary>
        /// Tryb "Stare": kasuje override planu, przywraca oryginalne wartosci podsumowania.
        /// </summary>
        private void ClearNowyPlan()
        {
            foreach (var item in _dostawy)
                item.NowyPlanKg = null;

            _podsumowanie.KgPlanDoZwazonych = _origKgPlanDoZwazonych;
            _podsumowanie.OdchylenieKgSuma = _origOdchylenieKgSuma;
        }

        /// <summary>
        /// Przelicza odchylenie sumaryczne w trybie "Nowe".
        /// </summary>
        private void RecalculateNowySummary()
        {
            decimal kgPlanDoZwazonych = _dostawy
                .Where(d => d.Status == StatusDostawy.Zwazony && d.NowyPlanKg.HasValue)
                .Sum(d => d.NowyPlanKg.Value);

            _podsumowanie.OdchylenieKgSuma = _podsumowanie.KgZwazoneSuma - kgPlanDoZwazonych;
            _podsumowanie.KgPlanDoZwazonych = kgPlanDoZwazonych;
        }
    }
}
