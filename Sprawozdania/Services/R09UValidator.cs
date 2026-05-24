using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Kalendarz1.Sprawozdania.Models;

namespace Kalendarz1.Sprawozdania.Services
{
    // ════════════════════════════════════════════════════════════════════
    // R-09U — Walidacja logiczna przed eksportem XML
    // Wzorzec: P02Validator. Severity: Error blokuje, Warning ostrzega, Info informuje.
    // ════════════════════════════════════════════════════════════════════
    public class R09UValidator
    {
        public enum Severity { Error, Warning, Info }
        public record ValidationIssue(Severity Severity, string Field, string Message);

        private static readonly Regex RegonRegex = new(@"^\d{9}(\d{5})?$");

        public List<ValidationIssue> Validate(R09UReportData data, GusSettings cfg)
        {
            var issues = new List<ValidationIssue>();

            // ═══════ Konfiguracja jednostki ═══════
            if (string.IsNullOrWhiteSpace(cfg.Regon))
                issues.Add(new(Severity.Error, "REGON", "Brak numeru REGON w konfiguracji"));
            else if (!RegonRegex.IsMatch(cfg.Regon.Replace(" ", "")))
                issues.Add(new(Severity.Warning, "REGON",
                    $"REGON '{cfg.Regon}' ma niestandardowy format (9 lub 14 cyfr)"));

            if (string.IsNullOrWhiteSpace(cfg.OsobaNazwisko))
                issues.Add(new(Severity.Warning, "Osoba", "Brak nazwiska osoby odpowiedzialnej"));

            if (string.IsNullOrWhiteSpace(cfg.EmailJednostki) || !cfg.EmailJednostki.Contains("@"))
                issues.Add(new(Severity.Warning, "Email", "Email jednostki nieprawidłowy"));

            if (string.IsNullOrWhiteSpace(cfg.EmailOsoby) || !cfg.EmailOsoby.Contains("@"))
                issues.Add(new(Severity.Warning, "Email", "Email osoby nieprawidłowy"));

            // ═══════ Okres ═══════
            if (data.Miesiac < 1 || data.Miesiac > 12)
                issues.Add(new(Severity.Error, "Miesiąc", $"Miesiąc {data.Miesiac} poza zakresem 1-12"));

            // ═══════ Pozycje ═══════
            if (data.Dzial1 == null || data.Dzial1.Count == 0)
            {
                issues.Add(new(Severity.Error, "Dział 1", "Brak pozycji w Dziale 1 (skup własny)"));
                return issues;
            }

            int wypelnione = 0;
            for (int i = 0; i < data.Dzial1.Count; i++)
            {
                var poz = data.Dzial1[i];
                string lbl = $"D1.{poz.WierszLabel}";

                if (poz.JestPusta) continue;
                wypelnione++;

                // Liczby ujemne
                if (poz.LiczbaSztuk < 0) issues.Add(new(Severity.Error, lbl, "Sztuki < 0"));
                if (poz.WagaZywaKg < 0) issues.Add(new(Severity.Error, lbl, "Waga żywa < 0"));
                if (poz.WagaPoubojowaBruttoKg < 0) issues.Add(new(Severity.Error, lbl, "Waga po uboju brutto < 0"));
                if (poz.WagaHandlowaNettoKg < 0) issues.Add(new(Severity.Error, lbl, "Waga handlowa netto < 0"));
                if (poz.WartoscZl < 0) issues.Add(new(Severity.Error, lbl, "Wartość < 0"));

                // Spójność: po uboju powinno być MNIEJSZE niż waga żywa
                if (poz.WagaPoubojowaBruttoKg > poz.WagaZywaKg && poz.WagaZywaKg > 0)
                    issues.Add(new(Severity.Warning, lbl,
                        $"Waga po uboju ({poz.WagaPoubojowaBruttoKg:N0}) > wagi żywej ({poz.WagaZywaKg:N0}) — niefizyczne"));

                // Wydajność poubojowa typowo 65-80% dla drobiu
                if (poz.WagaZywaKg > 0)
                {
                    var wyd = poz.WydajnoscPoubojowa;
                    if (wyd < 50m)
                        issues.Add(new(Severity.Warning, lbl,
                            $"Wydajność poubojowa {wyd:N1}% < 50% — dla brojlerów typowo 70-80%"));
                    else if (wyd > 95m)
                        issues.Add(new(Severity.Warning, lbl,
                            $"Wydajność poubojowa {wyd:N1}% > 95% — sprawdź czy waga po uboju jest prawidłowa"));
                }

                // Średnia masa szt typowo 1.5-3.5 kg dla brojlerów
                if (poz.LiczbaSztuk > 0)
                {
                    var srMasa = poz.SredniaMasaSztKg;
                    if (poz.Wiersz == R09UWiersz.Brojlery_Kurze && (srMasa < 1m || srMasa > 5m))
                        issues.Add(new(Severity.Warning, lbl,
                            $"Średnia masa szt {srMasa:N2} kg poza typowym zakresem 1.5-3.5 dla brojlerów"));
                }

                // Sztuki > 10M = pewnie pomyłka (1 brojler ≈ 2 kg, więc 10M szt = 20M kg)
                if (poz.LiczbaSztuk > 10_000_000)
                    issues.Add(new(Severity.Warning, lbl,
                        $"Sztuki {poz.LiczbaSztuk:N0} > 10 mln — sprawdź jednostkę (może wpisałeś kg zamiast szt)"));

                // Wartość > 100 mln zł = pewnie pomyłka
                if (poz.WartoscZl > 100_000_000m)
                    issues.Add(new(Severity.Warning, lbl,
                        $"Wartość {poz.WartoscZl:N0} zł > 100 mln — sprawdź"));
            }

            if (wypelnione == 0)
                issues.Add(new(Severity.Warning, "Pozycje",
                    "Wszystkie pozycje są puste — czy na pewno miesiąc bez ubojów?"));

            return issues;
        }

        public static bool MaBledy(List<ValidationIssue> issues) =>
            issues.Any(i => i.Severity == Severity.Error);
    }
}
