using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.Models.IRZplus
{
    /// <summary>
    /// Gatunek drobiu - kody zgodne z IRZplus
    /// </summary>
    public static class GatunekDrobiu
    {
        public const string Kury = "KURY";
        public const string Kurczeta = "KURCZETA";
        public const string Indyki = "INDYKI";
        public const string Kaczki = "KACZKI";
        public const string Gesi = "GESI";
        public const string Przepiorki = "PRZEPIORKI";
        public const string Perliczki = "PERLICZKI";
    }

    /// <summary>
    /// Typ zdarzenia dla ZURD (Zgloszenie Uboju Drobiu w Rzezni)
    /// Slownik: SIA-SL02126 (TYPY_ZDARZEN_DROBIU)
    /// </summary>
    public static class TypZdarzeniaZURD
    {
        public const string UbojRzezniczy = "ZURDUR";  // Przybycie do rzezni i uboj drobiu
        public const string UbojRytualny = "ZURDUW";   // Uboj po wwozie
        public const string Padniecie = "P";
    }

    /// <summary>
    /// Typ zdarzenia dla ZSSD (Zgloszenie Zmiany Stanu Stada)
    /// </summary>
    public static class TypZdarzeniaZSSD
    {
        public const string PrzychodZakup = "PZ";
        public const string PrzychodWyleg = "PU";
        public const string RozchodSprzedaz = "RS";
        public const string RozchodUboj = "RU";
        public const string Padniecie = "P";
        public const string Strata = "S";
    }

    /// <summary>
    /// POZYCJA w zgloszeniu - reprezentuje JEDEN TRANSPORT/AUT
    /// Kazdy aut z hodowcy to osobna pozycja w tym samym zgloszeniu!
    /// </summary>
    public class PozycjaZgloszeniaIRZ
    {
        /// <summary>Numer porzadkowy pozycji (1, 2, 3...)</summary>
        public int Lp { get; set; }

        /// <summary>Numer identyfikacyjny partii drobiu / numer siedliska hodowcy (np. 038481631-001)</summary>
        public string NumerPartiiDrobiu { get; set; }

        /// <summary>Typ zdarzenia: ZURDUR, ZURDUW, P (slownik SIA-SL02126)</summary>
        public string TypZdarzenia { get; set; } = TypZdarzeniaZURD.UbojRzezniczy;

        /// <summary>Liczba sztuk drobiu w tym transporcie</summary>
        public int LiczbaSztuk { get; set; }

        /// <summary>Data zdarzenia (uboju)</summary>
        public DateTime DataZdarzenia { get; set; }

        /// <summary>Masa drobiu poddanego ubojowi w kg</summary>
        public decimal MasaKg { get; set; }

        /// <summary>Kod kraju wwozu (puste dla polskich hodowcow)</summary>
        public string KrajWwozu { get; set; }

        /// <summary>Data kupna/wwozu (puste dla polskich hodowcow)</summary>
        public DateTime? DataKupnaWwozu { get; set; }

        /// <summary>Numer dzialalnosci z ktorej przyjeto drob (np. 038481631-001-001)</summary>
        public string PrzyjeteZDzialalnosci { get; set; }

        /// <summary>Czy uboj rytualny</summary>
        public bool UbojRytualny { get; set; } = false;

        /// <summary>Liczba padlych (opcjonalne - do statystyk)</summary>
        public int? LiczbaPadlych { get; set; }

        /// <summary>Numer partii wewnetrzny (z systemu ZPSP)</summary>
        public string NumerPartiiWewnetrzny { get; set; }

        /// <summary>Uwagi dodatkowe</summary>
        public string Uwagi { get; set; }
    }

    /// <summary>
    /// ZGLOSZENIE ZURD - Zgloszenie Uboju Drobiu w Rzezni
    /// JEDNO zgloszenie zawiera WIELE pozycji (transportow/autow)
    /// </summary>
    public class ZgloszenieZURD
    {
        /// <summary>Gatunek drobiu (KURY, KURCZETA, INDYKI...)</summary>
        public string Gatunek { get; set; } = GatunekDrobiu.Kury;

        /// <summary>Numer weterynaryjny rzezni z numerem zakladu (np. 039806095-001)</summary>
        public string NumerRzezni { get; set; } = "039806095-001";

        /// <summary>Numer producenta (numer weterynaryjny bez sufixu, np. 039806095)</summary>
        public string NumerProducenta { get; set; } = "039806095";

        /// <summary>Numer partii uboju - unikalny identyfikator zgloszenia (np. 26014001)</summary>
        public string NumerPartiiUboju { get; set; }

        /// <summary>Data uboju</summary>
        public DateTime DataUboju { get; set; }

        /// <summary>Lista pozycji - kazdy transport/aut to osobna pozycja</summary>
        public List<PozycjaZgloszeniaIRZ> Pozycje { get; set; } = new List<PozycjaZgloszeniaIRZ>();

        /// <summary>
        /// Generuje numer partii uboju w formacie RRDDDNNN
        /// RR = rok (2 cyfry), DDD = dzien roku (001-366), NNN = numer kolejny
        /// </summary>
        public static string GenerujNumerPartiiUboju(DateTime data, int numerKolejny = 1)
        {
            return $"{data:yy}{data.DayOfYear:000}{numerKolejny:000}";
        }

        // Wlasciwosci obliczane
        public int SumaLiczbaSztuk => Pozycje?.Sum(p => p.LiczbaSztuk) ?? 0;
        public decimal SumaMasaKg => Pozycje?.Sum(p => p.MasaKg) ?? 0;
        public int LiczbaPozycji => Pozycje?.Count ?? 0;
        public int LiczbaHodowcow => Pozycje?.Select(p => p.NumerPartiiDrobiu?.Split('-').FirstOrDefault()).Distinct().Count() ?? 0;
    }

    /// <summary>
    /// ZGLOSZENIE ZSSD - Zgloszenie Zmiany Stanu Stada Drobiu (dla hodowcy)
    /// </summary>
    public class ZgloszenieZSSD
    {
        /// <summary>Numer siedliska hodowcy</summary>
        public string NumerSiedliska { get; set; }

        /// <summary>Gatunek drobiu</summary>
        public string Gatunek { get; set; } = GatunekDrobiu.Kury;

        /// <summary>Data zdarzenia</summary>
        public DateTime DataZdarzenia { get; set; }

        /// <summary>Lista pozycji</summary>
        public List<PozycjaZgloszeniaIRZ> Pozycje { get; set; } = new List<PozycjaZgloszeniaIRZ>();
    }

    // Zachowaj stare klasy dla kompatybilnosci wstecznej
    [Obsolete("Uzyj PozycjaZgloszeniaIRZ")]
    public class PozycjaEksportuIRZ : PozycjaZgloszeniaIRZ
    {
        // Alias dla starej nazwy WagaKg
        public decimal? WagaKg
        {
            get => MasaKg;
            set => MasaKg = value ?? 0;
        }
    }

    [Obsolete("Uzyj ZgloszenieZURD")]
    public class EksportZURD : ZgloszenieZURD
    {
        public string GatunekKod
        {
            get => Gatunek;
            set => Gatunek = value;
        }
    }

    [Obsolete("Uzyj ZgloszenieZSSD")]
    public class EksportZSSD : ZgloszenieZSSD
    {
        public string NumerSiedliskaHodowcy
        {
            get => NumerSiedliska;
            set => NumerSiedliska = value;
        }

        public string GatunekKod
        {
            get => Gatunek;
            set => Gatunek = value;
        }
    }
}
