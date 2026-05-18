using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kalendarz1.AnalitykaPelna.Models
{
    public class WydajnoscDzien
    {
        public DateTime Data { get; set; }
        public DateTime DataOd { get; set; }
        public DateTime DataDo { get; set; }
        public string EtykietaOkresu { get; set; } = "";   // "05.05.2026 (Pn)" / "Tydz. 18/2026" / "Maj 2026" / "Q2 2026"
        public string DzienTygodnia => Data.ToString("ddd", new CultureInfo("pl-PL"));

        // ─── Wejście uboju ───
        public decimal ZywiecKg { get; set; }       // sPZ kat. 65882 (kurczak żywy przyjęty)
        public decimal ZywiecRwuKg { get; set; }    // sRWU kat. 65882 (rozchodowany do uboju)

        // ─── Wyjście uboju (sPWU magazyn 65554) ───
        public decimal TuszkaAKg { get; set; }
        public decimal TuszkaBKg { get; set; }
        public decimal WatrobaKg { get; set; }
        public decimal ZoladkiKg { get; set; }
        public decimal SerceKg { get; set; }
        public decimal PodrobyKg => WatrobaKg + ZoladkiKg + SerceKg;
        public decimal SumaTuszekAB => TuszkaAKg + TuszkaBKg;
        public decimal SumaWyjscia => SumaTuszekAB + PodrobyKg;

        // ─── Wydajności ───
        public decimal WydajnoscZPodrobamiProc
            => ZywiecKg <= 0 ? 0 : SumaWyjscia / ZywiecKg * 100m;
        public decimal WydajnoscBezPodrobowProc
            => ZywiecKg <= 0 ? 0 : SumaTuszekAB / ZywiecKg * 100m;
        public decimal WydajnoscPodrobowProc
            => ZywiecKg <= 0 ? 0 : PodrobyKg / ZywiecKg * 100m;

        // ─── Stary kontrakt (zachowany dla kompatybilności wykresu w widoku Bilans) ───
        public decimal ElementyKg { get; set; }
        public decimal SumaWyjscie => SumaTuszekAB + PodrobyKg;
        public decimal RoznicaKg => SumaWyjscie - ZywiecKg;
        public decimal WydajnoscProcent => WydajnoscZPodrobamiProc;

        public bool CzyAlert { get; set; }
        public string Status => CzyAlert ? "⚠ Problem" : "✓ OK";
        public string Uwagi { get; set; } = "";
    }

    public class WydajnoscHodowca
    {
        public int Pozycja { get; set; }
        public string CustomerID { get; set; } = "";
        public string Hodowca { get; set; } = "";
        public int LiczbaPartii { get; set; }
        public decimal SumaTuszkaBKg { get; set; }
        public decimal SumaWyjscieKg { get; set; }
        public decimal SredniaWydajnoscProc
            => SumaTuszkaBKg <= 0 ? 0 : SumaWyjscieKg / SumaTuszkaBKg * 100m;
        public decimal ProcentLidera { get; set; }     // 0–100, dla progress bar
        public decimal ProcentUdzialu { get; set; }    // % od sumy wszystkich hodowców
    }

    public class WydajnoscKlasa
    {
        public int Klasa { get; set; }   // QntInCont 4..12
        public int LiczbaWazen { get; set; }
        public decimal SumaActWeightKg { get; set; }
        public decimal MinWagaSzt { get; set; }
        public decimal MaxWagaSzt { get; set; }
        public decimal SredniaWagaSzt
            => LiczbaWazen == 0 ? 0 : SumaActWeightKg / LiczbaWazen;
        public decimal ProcentUdzialu { get; set; }

        // Wiersz podsumowania (Σ) — flag + display name dla kolumny "Klasa"
        public bool JestPodsumowaniem { get; set; }
        public string NazwaPodsumowania { get; set; } = "";
        /// <summary>"Duzy" / "Maly" / "Razem" — używane do kolorowania wiersza Σ.</summary>
        public string TypPodsumowania { get; set; } = "";

        /// <summary>Wartość wyświetlana w kolumnie "Klasa": numer klasy lub nazwa podsumowania.</summary>
        public string KlasaDisplay => JestPodsumowaniem ? NazwaPodsumowania : Klasa.ToString();

        /// <summary>Grupa kurczaka: "🍗 Duży (4-7)" / "🐥 Mały (8-12)".</summary>
        public string Grupa => Klasa switch
        {
            >= 4 and <= 7 => "🍗 Duży (4-7)",
            >= 8 and <= 12 => "🐥 Mały (8-12)",
            _ => "❓ Inne"
        };

        public int KolejnoscGrupy => Klasa switch
        {
            >= 4 and <= 7 => 1,
            >= 8 and <= 12 => 2,
            _ => 3
        };
    }

    public class WydajnoscSzczegolElement
    {
        public DateTime Data { get; set; }
        public string KodTowaru { get; set; } = "";
        public string NazwaTowaru { get; set; } = "";
        public string Kategoria { get; set; } = "";   // Mięso/Mrozony/Zywy/Odpady/Inne
        public string Seria { get; set; } = "";       // PWP/RWP/sPWU
        public decimal Przychod { get; set; }
        public decimal Krojenie { get; set; }

        // ─── NOWE: Magazyn (HM.MZ.magazyn) ──────────────────────────────────
        public int? MagazynID { get; set; }
        public string MagazynNazwa { get; set; } = "";

        // ─── NOWE: Numery dokumentów Sage (HM.MG.kod) ───────────────────────
        public int LiczbaDokumentow { get; set; }
        public string NumeryDokumentow { get; set; } = "";

        /// <summary>Skrócona wersja do tabeli (max 50 znaków + "..." jeśli więcej).</summary>
        public string NumeryDokumentowSkrocone =>
            string.IsNullOrEmpty(NumeryDokumentow) ? "" :
            NumeryDokumentow.Length <= 50 ? NumeryDokumentow :
            NumeryDokumentow.Substring(0, 47) + "...";

        /// <summary>Etykieta licznika np. "12 dok." dla kolumny.</summary>
        public string LicznikDokumentow =>
            LiczbaDokumentow == 0 ? "" :
            LiczbaDokumentow == 1 ? "1 dok." :
            $"{LiczbaDokumentow} dok.";

        /// <summary>% wydajności krojenia = Krojenie / Przychód × 100. Kluczowa metryka.</summary>
        public decimal WydajnoscProc => Przychod > 0 ? Krojenie / Przychod * 100m : 0m;

        /// <summary>Bilans = Przychód − Krojenie (różnica do oszacowania marży lub straty).</summary>
        public decimal Bilans => Przychod - Krojenie;
    }
}
