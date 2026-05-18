using System.Collections.Generic;

namespace Kalendarz1.AnalitykaPelna.Models
{
    /// <summary>
    /// Z czego liczymy procent udziału w bilansie materiałowym.
    /// </summary>
    public enum BazaUdzialu
    {
        Zywiec,        // Suma kurczaka żywego (PZ, katalog Zywy)
        TuszkaAB,      // Suma Tuszka A + Tuszka B (sPWU)
        TuszkaA,       // Sama Tuszka A
        TuszkaB        // Sama Tuszka B (do liczenia % elementów z krojenia)
    }

    /// <summary>
    /// Pojedyncza pozycja w tabeli bilansu: towar/kod, kategoria, seria, kg, % bazy.
    /// </summary>
    public class BilansMaterialowyWiersz
    {
        public string Etap { get; set; } = "";          // ŻYWIEC / UBÓJ / KROJENIE
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string Kategoria { get; set; } = "";     // Mięso / Mrożony / Żywy / Odpady / Inne
        public string Seria { get; set; } = "";         // PZ / sPWU / PWU / PWP / RWU / RWP
        public decimal Kg { get; set; }
        public decimal ProcentBazy { get; set; }        // % z bazy (Zywiec / A+B / B itd.)

        // ─── NOWE: Magazyn (z HM.MZ.magazyn) ─────────────────────────────────
        public int? MagazynID { get; set; }
        public string MagazynNazwa { get; set; } = "";

        // Dla MM- (przesunięcia międzymagazynowe): MagazynID = źródło (rozchód, ujemny ilosc),
        // MagazynDocelowyID = cel (przychód, dodatni ilosc) z tego samego dokumentu.
        public int? MagazynDocelowyID { get; set; }
        public string MagazynDocelowyNazwa { get; set; } = "";

        public string KierunekMM => MagazynID.HasValue && MagazynDocelowyID.HasValue
            ? $"{MagazynID} → {MagazynDocelowyID}"
            : "";

        // ─── NOWE: Numery dokumentów Sage (HM.MG.kod) ────────────────────────
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
    }

    /// <summary>
    /// Pełen bilans materiałowy w okresie: agregaty + lista szczegółowa.
    /// </summary>
    public class BilansMaterialowy
    {
        public decimal ZywiecPzKg { get; set; }         // sPZ, katalog 65882 — żywiec przyjęty od hodowcy
        public decimal ZywiecRwuKg { get; set; }        // sRWU, katalog 65882 — żywiec wzięty do uboju
        public decimal TuszkaAKg { get; set; }          // sPWU, kod='Kurczak A', magazyn 65554
        public decimal TuszkaBKg { get; set; }          // sPWU, kod='Kurczak B', magazyn 65554
        public decimal PodrobyKg { get; set; }          // sPWU, kody Wątroba/Żołądki/Serce/inne
        public decimal OdpadyKg { get; set; }           // sPWU, katalog 67094 (Odpady, jeśli są)
        public decimal MmTuszkiPodrobyKg { get; set; }  // MM-, kody Kurczak A/B + Wątroba/Żołądki/Serce (przed krojeniem)
        public decimal MmElementyKg { get; set; }       // MM-, pozostałe kody (po krojeniu — Filet, Skrzydło, Korpus...)
        public decimal MmMinusKg => MmTuszkiPodrobyKg + MmElementyKg;
        public decimal WejscieKrojeniaKg { get; set; }  // sRWP/RWP, magazyn 65554, katalog mięsa — co poszło do krojenia
        public decimal ElementyKg { get; set; }         // sPWP/PWP, katalog 67095/67104 — wyjście krojenia (bez tuszek/podrobów)

        // Bazowa wartość żywca dla % udziału — preferujemy sRWU (faktycznie poszło do uboju)
        public decimal ZywiecKg => ZywiecRwuKg > 0 ? ZywiecRwuKg : ZywiecPzKg;

        public decimal SumaTuszkiAB => TuszkaAKg + TuszkaBKg;
        public decimal SumaWyjsciaUboju => TuszkaAKg + TuszkaBKg + PodrobyKg + OdpadyKg;

        // Wydajność uboju = (Tuszki + Podroby + Odpady) / Żywiec do uboju (sRWU)
        public decimal WydajnoscUbojuProc =>
            ZywiecKg <= 0 ? 0 : SumaWyjsciaUboju / ZywiecKg * 100m;

        // Wydajność krojenia = Elementy / Wejście do krojenia (sRWP)
        // (a nie Elementy/TuszkaB, bo do krojenia wchodzi też czasem A i inne towary)
        public decimal WydajnoscKrojeniaProc =>
            WejscieKrojeniaKg <= 0 ? 0 : ElementyKg / WejscieKrojeniaKg * 100m;

        // Strata uboju = żywiec − wyjście (pióra, krew, kości głębokie)
        public decimal StrataUbojuKg => ZywiecKg - SumaWyjsciaUboju;
        public decimal StrataUbojuProc =>
            ZywiecKg <= 0 ? 0 : StrataUbojuKg / ZywiecKg * 100m;

        public List<BilansMaterialowyWiersz> Pozycje { get; set; } = new();
    }
}
