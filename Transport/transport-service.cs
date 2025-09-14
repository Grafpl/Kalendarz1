// Plik: /Pakowanie/PakowanieSerwis.cs
// Implementacja serwisu pakowania

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.Transport.Pakowanie
{
    /// <summary>
    /// Serwis implementujący logikę pakowania ładunków
    /// </summary>
    public class PakowanieSerwis : IPakowanieSerwis
    {
        /// <summary>
        /// Oblicza wypełnienie kursu dla podanych ładunków
        /// </summary>
        public WynikPakowania ObliczKurs(IList<PozycjaLike> ladunki, int paletyPojazdu, int planE2NaPalete = 36)
        {
            if (ladunki == null || !ladunki.Any())
            {
                return new WynikPakowania
                {
                    SumaE2 = 0,
                    PaletyNominal = 0,
                    PaletyMax = 0,
                    ProcNominal = 0,
                    ProcMax = 0,
                    NadwyzkaNominal = 0,
                    NadwyzkaMax = 0
                };
            }

            // Walidacja parametrów
            if (planE2NaPalete != 36 && planE2NaPalete != 40)
                planE2NaPalete = 36;

            if (paletyPojazdu <= 0)
                paletyPojazdu = 33; // Domyślna wartość

            // Obliczenia podstawowe
            int sumaE2 = ladunki.Sum(l => l.PojemnikiE2);
            int paletyNominal = (int)Math.Ceiling(sumaE2 / 36.0);
            int paletyMax = (int)Math.Ceiling(sumaE2 / 40.0);

            // Procenty wypełnienia
            decimal procNominal = paletyPojazdu > 0
                ? Math.Min(100, (paletyNominal * 100.0m) / paletyPojazdu)
                : 0;

            decimal procMax = paletyPojazdu > 0
                ? Math.Min(100, (paletyMax * 100.0m) / paletyPojazdu)
                : 0;

            // Obliczenie nadwyżki
            int nadwyzkaNominal = Math.Max(0, paletyNominal - paletyPojazdu);
            int nadwyzkaMax = Math.Max(0, paletyMax - paletyPojazdu);

            return new WynikPakowania
            {
                SumaE2 = sumaE2,
                PaletyNominal = paletyNominal,
                PaletyMax = paletyMax,
                ProcNominal = procNominal,
                ProcMax = procMax,
                NadwyzkaNominal = nadwyzkaNominal,
                NadwyzkaMax = nadwyzkaMax
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