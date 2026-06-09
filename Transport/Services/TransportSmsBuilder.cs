using Kalendarz1.Transport;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kalendarz1.Transport.Services
{
    // Generuje treść SMS-a do kierowcy o jego kursie na dany dzień.
    // Format:
    //   Dzień dobry. Dziś (poniedziałek 09.06) wyjazd z bazy 15:30.
    //   Trasa:
    //   1. KLIENT A
    //   2. KLIENT B
    //   3. KLIENT C
    //   Auto: WG12345 (Renault Master). Razem: 1450 kg / 320 poj / 9 palet.
    //   Pozdrawiamy, Piórkowscy.
    public static class TransportSmsBuilder
    {
        // Ile minut wcześniej kierowca ma być w bazie (przed godzWyjazdu kursu)
        private const int MinutPrzedWyjazdem = 30;

        public sealed class KursPodsumowanie
        {
            public Kurs Kurs { get; init; } = null!;
            public List<Ladunek> Ladunki { get; init; } = new();
            // Ile sztuk/kg z LibraNet (opcjonalnie — pobierane zewnętrznie, bo wymaga drugiej bazy)
            public decimal? SumaKg { get; init; }
        }

        // Pełen tekst SMS dla pojedynczego kursu.
        public static string ZbudujTresc(KursPodsumowanie p)
        {
            if (p.Kurs == null) return "";
            var sb = new StringBuilder();

            var dni = new[] { "niedziela", "poniedziałek", "wtorek", "środa", "czwartek", "piątek", "sobota" };
            string dzienTyg = dni[(int)p.Kurs.DataKursu.DayOfWeek];
            string dataKursu = p.Kurs.DataKursu.ToString("dd.MM");

            // Godzina wyjazdu z bazy = godz wyjazdu - 30 min
            string godzBaza = "";
            if (p.Kurs.GodzWyjazdu.HasValue)
            {
                var wyjazdZBazy = p.Kurs.GodzWyjazdu.Value.Subtract(TimeSpan.FromMinutes(MinutPrzedWyjazdem));
                godzBaza = wyjazdZBazy.ToString(@"hh\:mm");
            }

            sb.AppendLine($"Dzień dobry. Kurs {dzienTyg} {dataKursu}" +
                          (string.IsNullOrEmpty(godzBaza) ? "." : $", wyjazd z bazy {godzBaza}."));

            // Lista klientów po kolei
            if (p.Ladunki != null && p.Ladunki.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Trasa:");
                var posortowane = p.Ladunki.OrderBy(l => l.Kolejnosc).ToList();
                int n = 1;
                foreach (var l in posortowane)
                {
                    string klient = string.IsNullOrWhiteSpace(l.KodKlienta) ? "(bez kodu)" : l.KodKlienta;
                    sb.AppendLine($"{n}. {klient}");
                    n++;
                }
            }

            // Suma kg / pojemniki / palety + auto
            int sumaPoj = p.Ladunki?.Sum(l => l.PojemnikiE2) ?? 0;
            int sumaPalet = p.Ladunki?.Sum(l => l.PaletyH1 ?? 0) ?? 0;
            string auto = !string.IsNullOrWhiteSpace(p.Kurs.PojazdRejestracja)
                ? p.Kurs.PojazdRejestracja
                : "(auto nieprzypisane)";

            sb.AppendLine();
            string kgInfo = p.SumaKg.HasValue && p.SumaKg.Value > 0
                ? $"{p.SumaKg.Value.ToString("#,##0", new CultureInfo("pl-PL"))} kg / "
                : "";
            sb.Append($"Auto: {auto}. Razem: {kgInfo}{sumaPoj} poj / {sumaPalet} palet.");

            sb.AppendLine();
            sb.AppendLine();
            sb.Append("Pozdrawiamy, Ubojnia Drobiu \"Piórkowscy\".");

            return sb.ToString();
        }

        // Złóż kompletne podsumowanie kursu — Kurs + Ładunki + ewentualnie sumaKg.
        // Wywołujący przekazuje już pobrane Ładunki (z TransportRepozytorium.PobierzLadunkiAsync).
        public static KursPodsumowanie PrzygotujPodsumowanie(Kurs kurs, List<Ladunek> ladunki, decimal? sumaKg = null)
            => new KursPodsumowanie { Kurs = kurs, Ladunki = ladunki ?? new(), SumaKg = sumaKg };
    }
}
