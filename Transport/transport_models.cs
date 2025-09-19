// =====================================================================
// PLIK: Transport/TransportModels.cs - KOMPLETNY PLIK BEZ DUPLIKATÓW
// =====================================================================

using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.Transport
{
    public class Kierowca
    {
        public int KierowcaID { get; set; }

        [Required]
        [MaxLength(50)]
        public string Imie { get; set; } = "";

        [Required]
        [MaxLength(50)]
        public string Nazwisko { get; set; } = "";

        [MaxLength(20)]
        public string? Telefon { get; set; }

        public bool Aktywny { get; set; } = true;

        public DateTime UtworzonoUTC { get; set; }
        public DateTime? ZmienionoUTC { get; set; }

        public string PelneImie => $"{Imie} {Nazwisko}";
    }

    public class Pojazd
    {
        public int PojazdID { get; set; }

        [Required]
        [MaxLength(20)]
        public string Rejestracja { get; set; } = "";

        [MaxLength(50)]
        public string? Marka { get; set; }

        [MaxLength(50)]
        public string? Model { get; set; }

        [Range(1, 100)]
        public int PaletyH1 { get; set; } = 33;

        public bool Aktywny { get; set; } = true;

        public DateTime UtworzonoUTC { get; set; }
        public DateTime? ZmienionoUTC { get; set; }

        public string PelnyNazwa => string.IsNullOrEmpty(Marka)
            ? Rejestracja
            : $"{Marka} {Model} ({Rejestracja})";
    }

    public class Kurs
    {
        public long KursID { get; set; }

        [Required]
        public DateTime DataKursu { get; set; }

        [Required]
        public int KierowcaID { get; set; }

        [Required]
        public int PojazdID { get; set; }

        [MaxLength(500)]
        public string? Trasa { get; set; }

        public TimeSpan? GodzWyjazdu { get; set; }
        public TimeSpan? GodzPowrotu { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Planowany";

        [Range(1, 255)]
        public byte PlanE2NaPalete { get; set; } = 36;

        public DateTime UtworzonoUTC { get; set; }
        public string? Utworzyl { get; set; }
        public DateTime? ZmienionoUTC { get; set; }
        public string? Zmienil { get; set; }

        // Właściwości nawigacyjne
        public string? KierowcaNazwa { get; set; }
        public string? PojazdRejestracja { get; set; }
        public int PaletyPojazdu { get; set; }
        public int SumaE2 { get; set; }
        public int PaletyNominal { get; set; }
        public int PaletyMax { get; set; }
        public decimal ProcNominal { get; set; }
        public decimal ProcMax { get; set; }
    }

    public class Ladunek
    {
        public long LadunekID { get; set; }

        [Required]
        public long KursID { get; set; }

        [Range(1, 999)]
        public int Kolejnosc { get; set; }

        [MaxLength(100)]
        public string? KodKlienta { get; set; }

        [Range(0, 9999)]
        public int PojemnikiE2 { get; set; }

        [Range(0, 999)]
        public int? PaletyH1 { get; set; }

        [Range(1, 255)]
        public byte? PlanE2NaPaleteOverride { get; set; }

        [MaxLength(1000)]
        public string? Uwagi { get; set; }

        public DateTime UtworzonoUTC { get; set; }

        public Kalendarz1.Transport.Pakowanie.PozycjaLike ToPozycjaLike()
        {
            return new Kalendarz1.Transport.Pakowanie.PozycjaLike
            {
                Kolejnosc = this.Kolejnosc,
                KodKlienta = this.KodKlienta,
                PojemnikiE2 = this.PojemnikiE2,
                PaletyH1 = this.PaletyH1,
                PlanE2NaPaleteOverride = this.PlanE2NaPaleteOverride,
                Uwagi = this.Uwagi
            };
        }
    }

    public class ZamowienieTransport
    {
        public int ZamowienieID { get; set; }
        public int KlientID { get; set; }
        public string KlientNazwa { get; set; } = "";
        public DateTime DataZamowienia { get; set; }
        public decimal IloscKg { get; set; }
        public string Status { get; set; } = "";
        public string? Handlowiec { get; set; }
    }
}

namespace Kalendarz1.Transport.Pakowanie
{
    public class PozycjaLike
    {
        public int Kolejnosc { get; set; }
        public string? KodKlienta { get; set; }
        public int PojemnikiE2 { get; set; }
        public int? PaletyH1 { get; set; }
        public byte? PlanE2NaPaleteOverride { get; set; }
        public string? Uwagi { get; set; }

        /// <summary>
        /// Tworzy kopię obiektu PozycjaLike
        /// </summary>
        public PozycjaLike Clone()
        {
            return new PozycjaLike
            {
                Kolejnosc = this.Kolejnosc,
                KodKlienta = this.KodKlienta,
                PojemnikiE2 = this.PojemnikiE2,
                PaletyH1 = this.PaletyH1,
                PlanE2NaPaleteOverride = this.PlanE2NaPaleteOverride,
                Uwagi = this.Uwagi
            };
        }
    }

    public class WynikPakowania
    {
        public int PaletyPojazdu { get; set; }
        public int SumaE2 { get; set; }
        public int PaletyNominal { get; set; }
        public int PaletyMax { get; set; }
        public decimal ProcNominal { get; set; }
        public decimal ProcMax { get; set; }
        public int NadwyzkaNominal { get; set; }
        public int NadwyzkaMax { get; set; }
        public List<PozycjaLike>? Nadwyzka { get; set; }
        public bool CzyPrzeladowny => ProcNominal > 100;
    }

    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za obliczenia pakowania ładunków
    /// </summary>
    public interface IPakowanieSerwis
    {
        WynikPakowania ObliczKurs(IList<PozycjaLike> pozycje, int paletyPojazdu, int planE2NaPalete);
        (IList<PozycjaLike> zapakowane, IList<PozycjaLike> nadwyzka) PakujZachlannie(
            IList<PozycjaLike> ladunki, int paletyPojazdu, int planE2NaPalete = 36);
        bool CzyZmiesciSie(PozycjaLike ladunek, int pozostalePalety, int planE2NaPalete = 36);
        IList<PozycjaLike> OptymalizujKolejnosc(IList<PozycjaLike> ladunki, int paletyPojazdu, int planE2NaPalete = 36);
    }

    /// <summary>
    /// Serwis implementujący logikę pakowania ładunków
    /// </summary>
    public class PakowanieSerwis : IPakowanieSerwis
    {
        /// <summary>
        /// Oblicza wypełnienie kursu dla podanych ładunków
        /// </summary>
        public WynikPakowania ObliczKurs(IList<PozycjaLike> pozycje, int paletyPojazdu, int planE2NaPalete)
        {
            if (pozycje == null || !pozycje.Any())
            {
                return new WynikPakowania
                {
                    PaletyPojazdu = paletyPojazdu,
                    SumaE2 = 0,
                    PaletyNominal = 0,
                    PaletyMax = 0,
                    ProcNominal = 0,
                    ProcMax = 0,
                    NadwyzkaNominal = 0,
                    NadwyzkaMax = 0,
                    Nadwyzka = new List<PozycjaLike>()
                };
            }

            // Walidacja parametrów
            if (planE2NaPalete != 36 && planE2NaPalete != 40)
                planE2NaPalete = 36;

            if (paletyPojazdu <= 0)
                paletyPojazdu = 33; // Domyślna wartość

            // Obliczenia podstawowe
            var sumaE2 = pozycje.Sum(p => p.PojemnikiE2);
            var paletyNominal = (int)Math.Ceiling((double)sumaE2 / planE2NaPalete);
            var maxE2NaPalete = planE2NaPalete + (planE2NaPalete * 0.15);
            var paletyMax = (int)Math.Ceiling((double)sumaE2 / maxE2NaPalete);

            var procNominal = paletyPojazdu > 0 ? (decimal)paletyNominal / paletyPojazdu * 100 : 0;
            var procMax = paletyPojazdu > 0 ? (decimal)paletyMax / paletyPojazdu * 100 : 0;

            // Obliczenie nadwyżki
            var nadwyzkaNominal = Math.Max(0, paletyNominal - paletyPojazdu);
            var nadwyzkaMax = Math.Max(0, paletyMax - paletyPojazdu);

            var nadwyzka = new List<PozycjaLike>();

            if (paletyNominal > paletyPojazdu)
            {
                var dostepneE2 = paletyPojazdu * planE2NaPalete;
                var aktualneE2 = 0;

                foreach (var pozycja in pozycje.OrderBy(p => p.Kolejnosc))
                {
                    if (aktualneE2 + pozycja.PojemnikiE2 <= dostepneE2)
                    {
                        aktualneE2 += pozycja.PojemnikiE2;
                    }
                    else
                    {
                        nadwyzka.Add(pozycja);
                    }
                }
            }

            return new WynikPakowania
            {
                PaletyPojazdu = paletyPojazdu,
                SumaE2 = sumaE2,
                PaletyNominal = paletyNominal,
                PaletyMax = paletyMax,
                ProcNominal = procNominal,
                ProcMax = procMax,
                NadwyzkaNominal = nadwyzkaNominal,
                NadwyzkaMax = nadwyzkaMax,
                Nadwyzka = nadwyzka
            };
        }

        /// <summary>
        /// Pakuje ładunki zachłannie (greedy algorithm)
        /// </summary>
        public (IList<PozycjaLike> zapakowane, IList<PozycjaLike> nadwyzka) PakujZachlannie(
            IList<PozycjaLike> ladunki,
            int paletyPojazdu,
            int planE2NaPalete = 36)
        {
            var zapakowane = new List<PozycjaLike>();
            var nadwyzka = new List<PozycjaLike>();

            if (ladunki == null || !ladunki.Any() || paletyPojazdu <= 0)
            {
                return (zapakowane, nadwyzka);
            }

            // Walidacja planu
            if (planE2NaPalete != 36 && planE2NaPalete != 40)
                planE2NaPalete = 36;

            // Sortuj według kolejności
            var posortowane = ladunki.OrderBy(l => l.Kolejnosc).ToList();

            int wykorzystanePalety = 0;

            foreach (var ladunek in posortowane)
            {
                // Oblicz ile palet potrzebuje ten ładunek
                int potrzebnePalety = (int)Math.Ceiling((double)ladunek.PojemnikiE2 / planE2NaPalete);

                // Sprawdź czy się zmieści
                if (wykorzystanePalety + potrzebnePalety <= paletyPojazdu)
                {
                    zapakowane.Add(ladunek.Clone());
                    wykorzystanePalety += potrzebnePalety;
                }
                else
                {
                    // Sprawdź czy można częściowo zapakować
                    int pozostalePalety = paletyPojazdu - wykorzystanePalety;

                    if (pozostalePalety > 0)
                    {
                        // Można zapakować część
                        int mozliweE2 = pozostalePalety * planE2NaPalete;

                        if (mozliweE2 > 0)
                        {
                            // Dodaj częściowy ładunek do zapakowanych
                            var czesciowy = ladunek.Clone();
                            czesciowy.PojemnikiE2 = mozliweE2;
                            czesciowy.Uwagi = $"[CZĘŚCIOWY] {czesciowy.Uwagi}";
                            zapakowane.Add(czesciowy);

                            // Reszta do nadwyżki
                            var reszta = ladunek.Clone();
                            reszta.PojemnikiE2 = ladunek.PojemnikiE2 - mozliweE2;
                            reszta.Uwagi = $"[NADWYŻKA] {reszta.Uwagi}";
                            nadwyzka.Add(reszta);

                            wykorzystanePalety = paletyPojazdu; // Pełne wykorzystanie
                        }
                        else
                        {
                            // Cały ładunek do nadwyżki
                            nadwyzka.Add(ladunek.Clone());
                        }
                    }
                    else
                    {
                        // Brak miejsca - cały do nadwyżki
                        nadwyzka.Add(ladunek.Clone());
                    }
                }
            }

            // Renumeruj kolejność w nadwyżce
            for (int i = 0; i < nadwyzka.Count; i++)
            {
                nadwyzka[i].Kolejnosc = i + 1;
            }

            return (zapakowane, nadwyzka);
        }

        /// <summary>
        /// Sprawdza czy ładunek zmieści się w pozostałej przestrzeni
        /// </summary>
        public bool CzyZmiesciSie(PozycjaLike ladunek, int pozostalePalety, int planE2NaPalete = 36)
        {
            if (ladunek == null || pozostalePalety <= 0)
                return false;

            int potrzebnePalety = (int)Math.Ceiling((double)ladunek.PojemnikiE2 / planE2NaPalete);
            return potrzebnePalety <= pozostalePalety;
        }

        /// <summary>
        /// Optymalizuje kolejność ładunków dla lepszego upakowania
        /// </summary>
        public IList<PozycjaLike> OptymalizujKolejnosc(IList<PozycjaLike> ladunki, int paletyPojazdu, int planE2NaPalete = 36)
        {
            if (ladunki == null || !ladunki.Any())
                return new List<PozycjaLike>();

            // Strategia: najpierw duże ładunki, potem dopasowywanie małych
            // To może dać lepsze wypełnienie niż czysto sekwencyjna kolejność

            var zoptymalizowane = new List<PozycjaLike>();
            var pozostale = ladunki.OrderBy(l => l.Kolejnosc).ToList();

            // Najpierw sortuj malejąco po rozmiarze
            var posortowane = pozostale.OrderByDescending(l => l.PojemnikiE2).ToList();

            int wykorzystanePalety = 0;
            var doUzycia = new List<PozycjaLike>();
            var pominiete = new List<PozycjaLike>();

            // Pierwszy przebieg - duże elementy
            foreach (var ladunek in posortowane)
            {
                int potrzebnePalety = (int)Math.Ceiling((double)ladunek.PojemnikiE2 / planE2NaPalete);

                if (wykorzystanePalety + potrzebnePalety <= paletyPojazdu)
                {
                    doUzycia.Add(ladunek);
                    wykorzystanePalety += potrzebnePalety;
                }
                else
                {
                    pominiete.Add(ladunek);
                }
            }

            // Drugi przebieg - próba dopasowania pominiętych
            foreach (var ladunek in pominiete.OrderBy(l => l.PojemnikiE2))
            {
                int potrzebnePalety = (int)Math.Ceiling((double)ladunek.PojemnikiE2 / planE2NaPalete);

                if (wykorzystanePalety + potrzebnePalety <= paletyPojazdu)
                {
                    doUzycia.Add(ladunek);
                    wykorzystanePalety += potrzebnePalety;
                }
            }

            // Przywróć oryginalną kolejność dla znalezionych
            zoptymalizowane = doUzycia.OrderBy(l => l.Kolejnosc).ToList();

            // Dodaj pozostałe które się nie zmieściły
            var nieZmieszczone = ladunki.Where(l => !doUzycia.Contains(l)).OrderBy(l => l.Kolejnosc);
            zoptymalizowane.AddRange(nieZmieszczone);

            // Renumeruj
            for (int i = 0; i < zoptymalizowane.Count; i++)
            {
                zoptymalizowane[i].Kolejnosc = i + 1;
            }

            return zoptymalizowane;
        }
    }
}