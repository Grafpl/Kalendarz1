using System;
using System.Collections.Generic;

namespace Kalendarz1.Models.IRZplus
{
    /// <summary>
    /// Typ dokumentu IRZplus
    /// </summary>
    public enum TypDokumentuIRZ
    {
        /// <summary>ZURD - Zgłoszenie Uboju drobiu w Rzeźni (składa rzeźnia)</summary>
        ZgloszenieUbojuDrobiu,

        /// <summary>ZSSD - Zgłoszenie Zmiany Stanu Stada Drobiu (składa hodowca)</summary>
        ZgloszenieZmianyStanuStada
    }

    /// <summary>
    /// Typ zdarzenia dla ZURD
    /// </summary>
    public static class TypZdarzeniaZURD
    {
        public const string UbojRzezniczy = "UR";      // Ubój rzeźniczy
        public const string UbojRytualny = "URR";      // Ubój rytualny
        public const string Padniecie = "P";          // Padnięcie w rzeźni
    }

    /// <summary>
    /// Typ zdarzenia dla ZSSD
    /// </summary>
    public static class TypZdarzeniaZSSD
    {
        public const string PrzychodZakup = "PZ";           // Przychód - zakup
        public const string PrzychodUrodzenie = "PU";       // Przychód - wylęg/urodzenie
        public const string RozchodSprzedaz = "RS";         // Rozchód - sprzedaż
        public const string RozchodUboj = "RU";             // Rozchód - przekazanie do uboju
        public const string Padniecie = "P";                // Padnięcie
        public const string Strata = "S";                   // Strata (kradzież, ucieczka)
    }

    /// <summary>
    /// Gatunek drobiu - kody IRZplus
    /// </summary>
    public static class GatunekDrobiu
    {
        public const string Kury = "KURY";
        public const string Kurczeta = "KURCZETA";      // Kurczęta brojlery
        public const string Indyki = "INDYKI";
        public const string Kaczki = "KACZKI";
        public const string Gesi = "GESI";
        public const string Przepiorki = "PRZEPIORKI";
        public const string Perliczki = "PERLICZKI";
    }

    /// <summary>
    /// Pozycja do eksportu - uniwersalna dla obu typów dokumentów
    /// </summary>
    public class PozycjaEksportuIRZ
    {
        public int Lp { get; set; }
        public string NumerPartiiDrobiu { get; set; }       // Numer identyfikacyjny partii drobiu hodowcy
        public string TypZdarzenia { get; set; }            // Kod typu zdarzenia (UR, P, RU, itp.)
        public int LiczbaSztuk { get; set; }
        public DateTime DataZdarzenia { get; set; }
        public string PrzyjeteZDzialalnosci { get; set; }   // Numer działalności źródłowej
        public bool UbojRytualny { get; set; }
        public decimal? WagaKg { get; set; }                // Opcjonalna waga
        public int? LiczbaPadlych { get; set; }             // Opcjonalna liczba padłych
        public string Uwagi { get; set; }
    }

    /// <summary>
    /// Dane do eksportu ZURD (Zgłoszenie Uboju w Rzeźni)
    /// </summary>
    public class EksportZURD
    {
        public string NumerProducenta { get; set; } = "039806095";
        public string NumerRzezni { get; set; } = "039806095-001";
        public string NumerPartiiUboju { get; set; }
        public string GatunekKod { get; set; } = "KURY";
        public DateTime DataUboju { get; set; }
        public List<PozycjaEksportuIRZ> Pozycje { get; set; } = new List<PozycjaEksportuIRZ>();

        /// <summary>
        /// Generuje numer partii uboju w formacie RRDDD### (rok + dzień roku + numer)
        /// </summary>
        public static string GenerujNumerPartiiUboju(DateTime data, int numer = 1)
        {
            return data.ToString("yy") + data.DayOfYear.ToString("000") + numer.ToString("000");
        }
    }

    /// <summary>
    /// Dane do eksportu ZSSD (Zgłoszenie Zmiany Stanu Stada)
    /// </summary>
    public class EksportZSSD
    {
        public string NumerSiedliskaHodowcy { get; set; }
        public string GatunekKod { get; set; } = "KURY";
        public DateTime DataZdarzenia { get; set; }
        public List<PozycjaEksportuIRZ> Pozycje { get; set; } = new List<PozycjaEksportuIRZ>();
    }
}
