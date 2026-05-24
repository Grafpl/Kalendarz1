using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Kalendarz1.Sprawozdania.Models;

namespace Kalendarz1.Sprawozdania.Services
{
    // ════════════════════════════════════════════════════════════════════
    // P-02 — Walidacja logiczna przed generacją XML
    //
    // GUS NIE udostępnia XSD publicznie (waliduje na serwerze Portalu).
    // Robimy własne sanity-checki które wyłapują ~90% typowych błędów:
    //   • Brak REGON-u / osoby odpowiedzialnej / PKD
    //   • Pusty zbiór pozycji
    //   • PKWiU w złym formacie (powinno być np. 10.12.10-50)
    //   • Liczby ujemne (P-02 oczekuje >= 0)
    //   • Sumy YTD < sumy MC (matematycznie niepoprawne)
    //   • Brak wszystkich 4 standardowych pozycji (10.12.10-10, ..., 10.12.20-53)
    //   • Sprzedaż > produkcja (ostrzeżenie — może być prawdą jeśli zapasy)
    //   • Wartości w tonach > 100 000 (prawdopodobnie ktoś wpisał kg zamiast ton)
    //
    // Severity:
    //   Error    → blokuje eksport
    //   Warning  → ostrzega, pozwala kontynuować
    //   Info     → tylko informacja
    // ════════════════════════════════════════════════════════════════════
    public class P02Validator
    {
        public enum Severity { Error, Warning, Info }

        public record ValidationIssue(Severity Severity, string Field, string Message);

        // Standardowy PKWiU drobiu — regex: 2 cyfry . 2 cyfry . 2 cyfry - 2 cyfry
        // Akceptuje też krótszy: 10.12.10 (bez sufiksu) ALE z ostrzeżeniem
        private static readonly Regex PkwiuRegex = new(@"^\d{2}\.\d{2}\.\d{2}-\d{2}$");
        private static readonly Regex PkwiuShortRegex = new(@"^\d{2}\.\d{2}\.\d{2}$");
        private static readonly Regex RegonRegex = new(@"^\d{9}(\d{5})?$");  // 9 lub 14 cyfr

        public List<ValidationIssue> Validate(P02ReportData data, GusSettings cfg)
        {
            var issues = new List<ValidationIssue>();

            // ═══════ Konfiguracja jednostki ═══════
            if (string.IsNullOrWhiteSpace(cfg.Regon))
                issues.Add(new(Severity.Error, "REGON", "Brak numeru REGON w konfiguracji"));
            else if (!RegonRegex.IsMatch(cfg.Regon.Replace(" ", "")))
                issues.Add(new(Severity.Warning, "REGON",
                    $"REGON '{cfg.Regon}' ma niestandardowy format (oczekuje 9 lub 14 cyfr)"));

            if (string.IsNullOrWhiteSpace(cfg.OsobaNazwisko))
                issues.Add(new(Severity.Error, "Osoba", "Brak nazwiska osoby odpowiedzialnej"));

            if (string.IsNullOrWhiteSpace(cfg.OsobaImie))
                issues.Add(new(Severity.Warning, "Osoba", "Brak imienia osoby odpowiedzialnej"));

            if (string.IsNullOrWhiteSpace(cfg.EmailJednostki))
                issues.Add(new(Severity.Warning, "Email", "Brak email-a jednostki"));
            else if (!cfg.EmailJednostki.Contains("@"))
                issues.Add(new(Severity.Warning, "Email", $"Email '{cfg.EmailJednostki}' wygląda na niepoprawny"));

            if (string.IsNullOrWhiteSpace(cfg.Pkd))
                issues.Add(new(Severity.Warning, "PKD", "Brak kodu PKD (domyślnie powinno być '1012' dla drobiu)"));

            // ═══════ Okres ═══════
            if (data.Rok < 2020 || data.Rok > DateTime.Today.Year + 1)
                issues.Add(new(Severity.Warning, "Rok",
                    $"Rok {data.Rok} wygląda nietypowo (oczekuje 2020 — {DateTime.Today.Year + 1})"));

            if (data.Miesiac < 1 || data.Miesiac > 12)
                issues.Add(new(Severity.Error, "Miesiąc",
                    $"Miesiąc {data.Miesiac} poza zakresem 1-12"));

            // ═══════ Pozycje ═══════
            if (data.Pozycje == null || data.Pozycje.Count == 0)
            {
                issues.Add(new(Severity.Error, "Pozycje", "Brak żadnych pozycji w sprawozdaniu"));
                return issues;
            }

            // Standardowe 4 pozycje dla drobiu
            var oczekiwane = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "10.12.10-10", "10.12.10-50", "10.12.20-13", "10.12.20-53" };
            var obecne = new HashSet<string>(data.Pozycje.Select(p => p.Pkwiu ?? ""), StringComparer.OrdinalIgnoreCase);
            foreach (var p in oczekiwane.Except(obecne, StringComparer.OrdinalIgnoreCase))
                issues.Add(new(Severity.Info, "Pozycje", $"Brak standardowej pozycji {p} (drobiarstwo zwykle ma wszystkie 4)"));

            // Walidacja każdej pozycji
            for (int i = 0; i < data.Pozycje.Count; i++)
            {
                var poz = data.Pozycje[i];
                string lbl = $"Lp.{i + 1} ({poz.Pkwiu})";

                if (string.IsNullOrWhiteSpace(poz.Pkwiu))
                {
                    issues.Add(new(Severity.Error, lbl, "Pusty PKWiU"));
                    continue;
                }

                if (!PkwiuRegex.IsMatch(poz.Pkwiu))
                {
                    if (PkwiuShortRegex.IsMatch(poz.Pkwiu))
                        issues.Add(new(Severity.Warning, lbl,
                            $"PKWiU '{poz.Pkwiu}' to wersja skrócona (5 cyfr) — GUS może wymagać 7-cyfrowej (np. {poz.Pkwiu}-10)"));
                    else
                        issues.Add(new(Severity.Error, lbl,
                            $"PKWiU '{poz.Pkwiu}' ma nieprawidłowy format (oczekuje '10.12.10-50')"));
                }

                if (poz.JednostkaKod != "00130" && poz.JednostkaKod != "00135" && poz.JednostkaKod != "00150")
                    issues.Add(new(Severity.Warning, lbl,
                        $"Jednostka '{poz.JednostkaKod}' jest niestandardowa (drób = 00130 = tona)"));

                // Liczby ujemne
                if (poz.ProdukcjaWMiesiacuTony < 0)
                    issues.Add(new(Severity.Error, lbl, "Produkcja w miesiącu < 0"));
                if (poz.SprzedazWMiesiacuTony < 0)
                    issues.Add(new(Severity.Error, lbl, "Sprzedaż w miesiącu < 0"));
                if (poz.ZapasyWyrobowTony < 0)
                    issues.Add(new(Severity.Error, lbl, "Zapasy wyrobów < 0"));
                if (poz.ZapasyTowarowTony < 0)
                    issues.Add(new(Severity.Error, lbl, "Zapasy towarów < 0"));

                // YTD < MC = błąd matematyczny
                if (poz.ProdukcjaOdPoczatkuRokuTony < poz.ProdukcjaWMiesiacuTony)
                    issues.Add(new(Severity.Error, lbl,
                        $"Produkcja YTD ({poz.ProdukcjaOdPoczatkuRokuTony:N0}) < produkcji MC ({poz.ProdukcjaWMiesiacuTony:N0}) — YTD musi obejmować bieżący miesiąc"));

                if (poz.SprzedazOdPoczatkuRokuTony < poz.SprzedazWMiesiacuTony)
                    issues.Add(new(Severity.Error, lbl,
                        $"Sprzedaż YTD ({poz.SprzedazOdPoczatkuRokuTony:N0}) < sprzedaży MC ({poz.SprzedazWMiesiacuTony:N0})"));

                // Sprzedaż > produkcja (możliwe ale rzadkie — ostrzeżenie)
                if (poz.SprzedazWMiesiacuTony > poz.ProdukcjaWMiesiacuTony * 1.5m && poz.ProdukcjaWMiesiacuTony > 0)
                    issues.Add(new(Severity.Info, lbl,
                        "Sprzedaż MC > 150% produkcji MC — sprawdź czy nie sprzedajesz z zapasów"));

                // Wartości > 100 000 ton = prawdopodobnie kg zamiast ton
                if (poz.ProdukcjaWMiesiacuTony > 100_000m || poz.SprzedazWMiesiacuTony > 100_000m)
                    issues.Add(new(Severity.Warning, lbl,
                        "Wartość > 100 000 t — prawdopodobnie wpisałeś kg zamiast ton (1 t = 1000 kg)"));
            }

            // ═══════ Globalne ═══════
            decimal sumaProdMc = data.Pozycje.Sum(p => p.ProdukcjaWMiesiacuTony);
            decimal sumaSprzMc = data.Pozycje.Sum(p => p.SprzedazWMiesiacuTony);

            if (sumaProdMc == 0 && sumaSprzMc == 0)
                issues.Add(new(Severity.Warning, "Sumy",
                    "Wszystkie pozycje produkcji i sprzedaży = 0 — czy na pewno był miesiąc bez działalności?"));

            return issues;
        }

        // Pomocnicze podsumowanie do wyświetlenia w UI
        public static string Podsumowanie(List<ValidationIssue> issues)
        {
            int errors = issues.Count(i => i.Severity == Severity.Error);
            int warnings = issues.Count(i => i.Severity == Severity.Warning);
            int infos = issues.Count(i => i.Severity == Severity.Info);
            return $"{errors} błędów  ·  {warnings} ostrzeżeń  ·  {infos} info";
        }

        public static bool MaBledy(List<ValidationIssue> issues) =>
            issues.Any(i => i.Severity == Severity.Error);
    }
}
