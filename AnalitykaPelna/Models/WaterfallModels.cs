using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.AnalitykaPelna.Models
{
    /// <summary>
    /// Typ etapu w wodospadzie uzysku.
    /// </summary>
    public enum WaterfallStepTyp
    {
        /// <summary>Stan masy w danym punkcie łańcucha (np. żywiec, tuszka, elementy).</summary>
        Poziom,
        /// <summary>Ubytek masy (np. pióra/krew, kości, odpady).</summary>
        Strata
    }

    /// <summary>
    /// Pojedynczy etap wodospadu uzysku (żywiec → premium).
    /// </summary>
    public class WaterfallStep
    {
        public string Nazwa { get; set; } = "";
        public string Ikona { get; set; } = "";
        public WaterfallStepTyp Typ { get; set; }

        /// <summary>Dla Poziom: stan masy. Dla Strata: ile kg ubyło na tym etapie.</summary>
        public decimal WartoscKg { get; set; }

        /// <summary>% względem żywca (bazy).</summary>
        public decimal ProcentBazy { get; set; }

        /// <summary>Dla Strata: % straty na tym konkretnym etapie (względem wejścia etapu).</summary>
        public decimal ProcentStraty { get; set; }

        public string Kolor { get; set; } = "#94A3B8";

        /// <summary>OK / WARN / ALERT — kolorowanie statusu.</summary>
        public string Status { get; set; } = "OK";

        /// <summary>Gdzie trafia strata (KARMA / ODPADY / UTYL / NIEUŻYTECZNE).</summary>
        public string Kierunek { get; set; } = "";

        public string Opis { get; set; } = "";

        // ─── Formatowanie do UI ───────────────────────────────────────────
        public string KgFormatted => WartoscKg <= 0 ? "—" : $"{WartoscKg:N0} kg";
        public string ProcBazyFormatted => $"{ProcentBazy:N1}%";
        public string ProcStratyFormatted => ProcentStraty > 0 ? $"−{ProcentStraty:N1}%" : "";
        public bool JestStrata => Typ == WaterfallStepTyp.Strata;
        public bool JestPoziom => Typ == WaterfallStepTyp.Poziom;

        /// <summary>Szerokość paska (px) proporcjonalna do % bazy — ustawiana przez widok.</summary>
        public double SzerokoscPx { get; set; }

        public string StatusIkona => Status switch
        {
            "OK" => "✓",
            "WARN" => "⚠",
            "ALERT" => "🚨",
            _ => ""
        };

        public string StatusKolor => Status switch
        {
            "OK" => "#10B981",
            "WARN" => "#F59E0B",
            "ALERT" => "#DC2626",
            _ => "#94A3B8"
        };
    }

    /// <summary>
    /// Pełny wodospad uzysku — od żywca (100%) do gotowych elementów (yield %).
    /// Budowany z FlowChainSummary (reuse danych z WydajnoscService.LoadFlowChainAsync).
    /// Opcjonalnie liczy wartość PLN jeśli podano ceny.
    /// </summary>
    public class WaterfallData
    {
        public DateTime DataOd { get; set; }
        public DateTime DataDo { get; set; }

        public List<WaterfallStep> Etapy { get; set; } = new();

        public decimal WagaWejsciaKg { get; set; }   // żywiec
        public decimal WagaWyjsciaKg { get; set; }    // finalne elementy (lub tuszka jeśli brak krojenia)

        public decimal YieldProc => WagaWejsciaKg > 0 ? WagaWyjsciaKg / WagaWejsciaKg * 100m : 0;

        // ─── Wartość PLN (opcjonalna) ──────────────────────────────────────
        public decimal? CenaZywcaZaKg { get; set; }
        public decimal? CenaGotowegoZaKg { get; set; }

        public decimal? WartoscWejsciaPLN =>
            CenaZywcaZaKg.HasValue ? WagaWejsciaKg * CenaZywcaZaKg.Value : null;

        public decimal? WartoscWyjsciaPLN =>
            CenaGotowegoZaKg.HasValue ? WagaWyjsciaKg * CenaGotowegoZaKg.Value : null;

        public decimal? MarzaPLN =>
            (WartoscWyjsciaPLN.HasValue && WartoscWejsciaPLN.HasValue)
                ? WartoscWyjsciaPLN.Value - WartoscWejsciaPLN.Value : null;

        public decimal? MarzaProc =>
            (MarzaPLN.HasValue && WartoscWejsciaPLN.HasValue && WartoscWejsciaPLN.Value > 0)
                ? MarzaPLN.Value / WartoscWejsciaPLN.Value * 100m : null;

        /// <summary>Efektywny koszt 1 kg gotowego mięsa (cena żywca / yield).</summary>
        public decimal? EfektywnyKosztGotowegoZaKg =>
            (CenaZywcaZaKg.HasValue && YieldProc > 0)
                ? CenaZywcaZaKg.Value / (YieldProc / 100m) : null;

        // ─── Formatowanie ──────────────────────────────────────────────────
        public string YieldFormatted => $"{YieldProc:N1}%";
        public string WejscieFormatted => $"{WagaWejsciaKg:N0} kg";
        public string WyjscieFormatted => $"{WagaWyjsciaKg:N0} kg";

        public string YieldKolor =>
            WagaWejsciaKg <= 0 ? "#94A3B8" :
            YieldProc >= 58m ? "#10B981" :
            YieldProc >= 50m ? "#F59E0B" : "#DC2626";

        public string MarzaFormatted =>
            MarzaPLN.HasValue ? $"{MarzaPLN.Value:N0} zł ({MarzaProc:N0}%)" : "podaj ceny →";

        public string EfektywnyKosztFormatted =>
            EfektywnyKosztGotowegoZaKg.HasValue
                ? $"{EfektywnyKosztGotowegoZaKg.Value:N2} zł/kg" : "—";

        // ─── Builder z FlowChainSummary ────────────────────────────────────

        /// <summary>
        /// Buduje wodospad z FlowChainSummary. Ceny opcjonalne (jeśli null — tylko kg/%).
        /// </summary>
        public static WaterfallData ZFlowChain(
            FlowChainSummary s,
            DateTime od, DateTime doDate,
            decimal? cenaZywca = null,
            decimal? cenaGotowego = null)
        {
            var d = new WaterfallData
            {
                DataOd = od,
                DataDo = doDate,
                WagaWejsciaKg = s.Zywiec.Kg,
                CenaZywcaZaKg = cenaZywca,
                CenaGotowegoZaKg = cenaGotowego
            };

            decimal baza = s.Zywiec.Kg;
            decimal P(decimal kg) => baza > 0 ? kg / baza * 100m : 0;

            // 1. ŻYWIEC — poziom startowy (100%)
            d.Etapy.Add(new WaterfallStep
            {
                Nazwa = "Żywiec przyjęty",
                Ikona = "🐔",
                Typ = WaterfallStepTyp.Poziom,
                WartoscKg = s.Zywiec.Kg,
                ProcentBazy = 100m,
                Kolor = "#F59E0B",
                Opis = $"{s.Zywiec.DokFormatted}"
            });

            // 2. STRATA UBOJU (pióra/krew/woda/jelita)
            if (s.Uboj.Kg > 0)
            {
                d.Etapy.Add(new WaterfallStep
                {
                    Nazwa = "Strata uboju (pióra/krew/woda)",
                    Ikona = "💧",
                    Typ = WaterfallStepTyp.Strata,
                    WartoscKg = s.StratyUbojuKg,
                    ProcentBazy = P(s.StratyUbojuKg),
                    ProcentStraty = s.StratyUbojuProc,
                    Kolor = "#94A3B8",
                    Status = s.BilansMasyOk ? "OK" : "WARN",
                    Kierunek = "NIEUŻYTECZNE",
                    Opis = s.BilansMasyStatus
                });

                // 3. TUSZKA PO UBOJU — poziom
                d.Etapy.Add(new WaterfallStep
                {
                    Nazwa = "Tuszka po uboju",
                    Ikona = "⚙",
                    Typ = WaterfallStepTyp.Poziom,
                    WartoscKg = s.Uboj.Kg,
                    ProcentBazy = P(s.Uboj.Kg),
                    Kolor = "#DC2626",
                    Status = s.WydajnoscUbojuProc >= 80m ? "OK" : s.WydajnoscUbojuProc >= 70m ? "WARN" : "ALERT",
                    Opis = $"Wydajność uboju: {s.WydajnoscUbojuProc:N1}%"
                });
            }

            // 4. STRATA KROJENIA (kości/ścinki) — tylko jeśli było krojenie (sRWP > 0)
            if (s.RozchodKrojenia.Kg > 0 && s.Produkcja.Kg > 0)
            {
                d.Etapy.Add(new WaterfallStep
                {
                    Nazwa = "Strata krojenia (kości/ścinki)",
                    Ikona = "🦴",
                    Typ = WaterfallStepTyp.Strata,
                    WartoscKg = s.StrataKrojeniaKg,
                    ProcentBazy = P(s.StrataKrojeniaKg),
                    ProcentStraty = s.StrataKrojeniaProc,
                    Kolor = "#CA8A04",
                    Status = "OK",
                    Kierunek = "KARMA",
                    Opis = $"Wydajność krojenia: {s.WydajnoscKrojeniaProc:N1}%"
                });

                // 5. ELEMENTY (po krojeniu) — poziom
                d.Etapy.Add(new WaterfallStep
                {
                    Nazwa = "Elementy (po krojeniu)",
                    Ikona = "🔪",
                    Typ = WaterfallStepTyp.Poziom,
                    WartoscKg = s.Produkcja.Kg,
                    ProcentBazy = P(s.Produkcja.Kg),
                    Kolor = "#7C3AED",
                    Status = s.WydajnoscKrojeniaProc >= 55m ? "OK" : s.WydajnoscKrojeniaProc >= 45m ? "WARN" : "ALERT",
                    Opis = $"sPWP: {s.Produkcja.KgFormatted}"
                });
            }

            // 6. ODPADY (jeśli rozchodowane do magazynu odpadów)
            if (s.Odpady.Kg > 0)
            {
                d.Etapy.Add(new WaterfallStep
                {
                    Nazwa = "Odpady poprodukcyjne",
                    Ikona = "🗑",
                    Typ = WaterfallStepTyp.Strata,
                    WartoscKg = s.Odpady.Kg,
                    ProcentBazy = P(s.Odpady.Kg),
                    ProcentStraty = s.ProcDoOdpadowProc,
                    Kolor = "#94A3B8",
                    Status = s.ProcDoOdpadowProc <= 5m ? "OK" : s.ProcDoOdpadowProc <= 8m ? "WARN" : "ALERT",
                    Kierunek = "UTYL",
                    Opis = s.OdpadyStatus
                });
            }

            // Finalne wyjście = elementy (jeśli było krojenie) lub tuszka
            d.WagaWyjsciaKg = s.Produkcja.Kg > 0 ? s.Produkcja.Kg : s.Uboj.Kg;

            // Oblicz szerokości pasków (max 100% bazy → 600px)
            const double maxPx = 600.0;
            foreach (var step in d.Etapy)
            {
                double proc = (double)step.ProcentBazy;
                step.SzerokoscPx = Math.Max(2.0, proc / 100.0 * maxPx);
            }

            return d;
        }

        /// <summary>
        /// Gdzie poszły wyprodukowane elementy — rozejście do 5 magazynów.
        /// </summary>
        public static List<WaterfallStep> RozejscieZFlowChain(FlowChainSummary s)
        {
            var lista = new List<WaterfallStep>();
            decimal baza = s.Produkcja.Kg > 0 ? s.Produkcja.Kg : s.Uboj.Kg;
            decimal P(decimal kg) => baza > 0 ? kg / baza * 100m : 0;

            void Dodaj(FlowChainNode node, string kierunek, string status)
            {
                if (node.Kg <= 0) return;
                lista.Add(new WaterfallStep
                {
                    Nazwa = node.Etap,
                    Ikona = node.Ikona,
                    Typ = WaterfallStepTyp.Poziom,
                    WartoscKg = node.Kg,
                    ProcentBazy = P(node.Kg),
                    Kolor = node.Kolor,
                    Status = status,
                    Kierunek = kierunek
                });
            }

            Dodaj(s.Dystrybucja, "SPRZEDAŻ", "OK");
            Dodaj(s.Mroznia, "MROŻENIE", "OK");
            Dodaj(s.Masarnia, "PRZEROBKA", "OK");
            Dodaj(s.Karma, "KARMA", "OK");
            Dodaj(s.Odpady, "UTYLIZACJA", s.ProcDoOdpadowProc <= 5m ? "OK" : "WARN");

            const double maxPx = 400.0;
            decimal maxKg = lista.Count > 0 ? lista.Max(x => x.WartoscKg) : 1;
            foreach (var step in lista)
                step.SzerokoscPx = Math.Max(2.0, (double)(step.WartoscKg / (maxKg > 0 ? maxKg : 1)) * maxPx);

            return lista.OrderByDescending(x => x.WartoscKg).ToList();
        }
    }
}
