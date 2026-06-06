// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Services/OstrzezeniaService.cs
//
// Analiza kursu przed wyjazdem — wykrywanie typowych problemów planowania.
// 10 reguł, kategorie: Krytyczny / Ostrzezenie / Info.
//
// Reguły:
//   1. Brak kierowcy / pojazdu                                  → Krytyczny
//   2. Powrót przed wyjazdem (cofnięcie czasu)                  → Krytyczny
//   3. Czas pracy > 13h (przepisy)                              → Krytyczny
//   4. Czas pracy 11-13h (blisko limitu)                        → Ostrzezenie
//   5. Powrót po 22:00 (zmęczenie)                              → Ostrzezenie
//   6. Łączna jazda > 4.5h bez pauzy (przepisy 561/2006)        → Ostrzezenie
//   7. Pojemność > 100% kapacity pojazdu                        → Ostrzezenie
//   8. Spóźnienie na awizację > 30 min                          → Ostrzezenie/Krytyczny
//   9. Klienci bez GPS — niepełne szacowanie                    → Info
//  10. Trasa > 500 km                                           → Info
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using Kalendarz1.Transport.Services;
using Kalendarz1.Transport.WPF.Models;

namespace Kalendarz1.Transport.WPF.Services
{
    public class OstrzezeniaService
    {
        private const int E2_NA_PALETE = 36;
        private static readonly TimeSpan MaxCzasPracy = TimeSpan.FromHours(13);
        private static readonly TimeSpan PrzedostrzezenieDniaPracy = TimeSpan.FromHours(11);
        private static readonly TimeSpan LimitJazdyBezPauzy = TimeSpan.FromHours(4.5);
        private static readonly TimeSpan TolerancjaAwizacji = TimeSpan.FromMinutes(30);
        private const double DlugaTrasaKm = 500;
        private const int LateHour = 22;

        public List<OstrzezenieKursu> Analizuj(
            Kierowca? kierowca,
            Pojazd? pojazd,
            TimeSpan? wyjazd,
            TimeSpan? powrot,
            IEnumerable<LadunekZAwizacja> ladunki,
            EtaService.RouteEtaResult? szacowanie)
        {
            var lista = new List<OstrzezenieKursu>();
            var ladList = ladunki?.OrderBy(l => l.Kolejnosc).ToList() ?? new List<LadunekZAwizacja>();

            // 1. Brak kierowcy / pojazdu
            if (kierowca == null)
                lista.Add(Krytyczny("👤", "Brak kierowcy",
                    "Kurs nie ma przypisanego kierowcy. Wybierz z listy przed zapisem."));
            if (pojazd == null)
                lista.Add(Krytyczny("🚛", "Brak pojazdu",
                    "Kurs nie ma przypisanego pojazdu. Wybierz z listy przed zapisem."));

            // 2. Cofnięcie czasu (powrót przed wyjazdem)
            if (wyjazd.HasValue && powrot.HasValue && powrot < wyjazd)
            {
                lista.Add(Krytyczny("❌", "Powrót przed wyjazdem",
                    $"Wyjazd {wyjazd:hh\\:mm}, powrót {powrot:hh\\:mm}. Sprawdź godziny."));
            }

            // 7. Pojemność pojazdu
            if (pojazd != null && ladList.Count > 0)
            {
                int sumaE2 = ladList.Sum(l => l.PojemnikiE2);
                int paletyNominalne = (int)Math.Ceiling(sumaE2 / (double)E2_NA_PALETE);
                int kapacita = pojazd.PaletyH1 > 0 ? pojazd.PaletyH1 : 33;
                if (paletyNominalne > kapacita)
                {
                    int procent = paletyNominalne * 100 / kapacita;
                    lista.Add(Ostrzezenie("⚠", $"Przekroczona pojemność ({procent}%)",
                        $"Załadunek {paletyNominalne} palet vs {kapacita} miejsc. " +
                        $"Rozdziel na 2 kursy lub zmień pojazd."));
                }
            }

            // 9. Klienci bez GPS (przed szacowaniem - z ladunki)
            int klientowBezGps = ladList.Count(l => !l.MaGps);
            if (klientowBezGps > 0)
            {
                lista.Add(Info("📍", $"{klientowBezGps} klientów bez GPS",
                    "Szacowanie używa 30 min zamiast realnego dystansu. " +
                    "Geokoduj w Mapie Klientów (Kartoteka)."));
            }

            // === Reguły wymagające szacowania ===
            if (szacowanie == null || szacowanie.Stops.Count == 0)
                return lista;

            var czasPracy = szacowanie.TotalDuration;

            // 3+4. Czas pracy
            if (czasPracy > MaxCzasPracy)
            {
                int h = (int)czasPracy.TotalHours;
                lista.Add(Krytyczny("⏳", $"Czas pracy {h}h {czasPracy.Minutes:00}min — przekroczenie",
                    $"Maksymalny dzień pracy kierowcy = 13h (rozp. 561/2006). " +
                    $"Rozważ start wcześniej, drugiego kierowcę lub podział kursu."));
            }
            else if (czasPracy > PrzedostrzezenieDniaPracy)
            {
                int h = (int)czasPracy.TotalHours;
                lista.Add(Ostrzezenie("⏳", $"Długi dzień pracy ({h}h {czasPracy.Minutes:00}min)",
                    "Bliski limit 13h. Sprawdź czy kierowca miał wczoraj odpoczynek 11h."));
            }

            // 5. Powrót po 22:00
            if (wyjazd.HasValue)
            {
                var powrotEst = wyjazd.Value + czasPracy;
                if (powrotEst.TotalHours >= LateHour && powrotEst.TotalHours < 24 + LateHour)
                {
                    lista.Add(Ostrzezenie("🌙", $"Powrót po {LateHour}:00 (oszacowano {powrotEst:hh\\:mm})",
                        "Późny powrót = zmęczenie. Sprawdź czy nie da się ruszyć wcześniej."));
                }
            }

            // 6. Pauza 45 min — łączna jazda
            double minutJazdy = szacowanie.Stops.Sum(s => s.DriveTime.TotalMinutes)
                              + (szacowanie.ReturnDistanceKm / EtaService.AvgSpeedKmh * 60.0);
            var totalJazda = TimeSpan.FromMinutes(minutJazdy);
            if (totalJazda > LimitJazdyBezPauzy)
            {
                int h = (int)totalJazda.TotalHours;
                lista.Add(Ostrzezenie("☕", $"Łączna jazda {h}h {totalJazda.Minutes:00}min — wymagana pauza",
                    "Po 4h 30min jazdy kierowca musi mieć przerwę 45 min (rozp. 561/2006). " +
                    "Doliczona do powrotu? Sprawdź."));
            }

            // 10. Długa trasa
            double km = szacowanie.TotalDistanceKm + szacowanie.ReturnDistanceKm;
            if (km > DlugaTrasaKm)
            {
                lista.Add(Info("🌍", $"Łączna trasa {km:F0} km",
                    "Daleka wyprawa. Sprawdź czy kierowca może wrócić w jeden dzień " +
                    "albo zaplanuj odpoczynek nocą po drodze."));
            }

            // 8. Spóźnienia na awizacje
            for (int i = 0; i < szacowanie.Stops.Count && i < ladList.Count; i++)
            {
                var stop = szacowanie.Stops[i];
                var lad = ladList[i];
                if (!lad.Awizacja.HasValue) continue;

                var awiz = lad.Awizacja.Value.TimeOfDay;
                if (stop.Eta <= awiz + TolerancjaAwizacji) continue;   // na czas

                var diff = stop.Eta - awiz;
                int diffMin = (int)Math.Ceiling(diff.TotalMinutes);
                string nazwa = string.IsNullOrEmpty(lad.NazwaDisplay) ? $"Klient #{i + 1}" : lad.NazwaDisplay;

                if (diffMin > 60)
                {
                    lista.Add(Krytyczny("🕐", $"{nazwa}: spóźnienie {diffMin} min",
                        $"Awizacja {awiz:hh\\:mm}, planowany przyjazd ~{stop.Eta:hh\\:mm}. " +
                        "Zadzwoń do klienta lub zmień kolejność."));
                }
                else
                {
                    lista.Add(Ostrzezenie("🕐", $"{nazwa}: spóźnienie {diffMin} min",
                        $"Awizacja {awiz:hh\\:mm}, planowany przyjazd ~{stop.Eta:hh\\:mm}."));
                }
            }

            // Sortowanie wynikowe: najpierw Krytyczne, potem Ostrzeżenia, potem Info
            return lista
                .OrderBy(o => o.Waga == WagaOstrzezenia.Krytyczny ? 0 : o.Waga == WagaOstrzezenia.Ostrzezenie ? 1 : 2)
                .ToList();
        }

        // ── helpers ─────────────────────────────────────────────────────────
        private static OstrzezenieKursu Krytyczny(string ikona, string tytul, string opis) =>
            new() { Waga = WagaOstrzezenia.Krytyczny, Ikona = ikona, Tytul = tytul, Opis = opis };
        private static OstrzezenieKursu Ostrzezenie(string ikona, string tytul, string opis) =>
            new() { Waga = WagaOstrzezenia.Ostrzezenie, Ikona = ikona, Tytul = tytul, Opis = opis };
        private static OstrzezenieKursu Info(string ikona, string tytul, string opis) =>
            new() { Waga = WagaOstrzezenia.Info, Ikona = ikona, Tytul = tytul, Opis = opis };

        /// <summary>
        /// Lekki DTO do przekazania ładunków z edytora — odseparowany od UI modelu LadunekWierszWpf,
        /// żeby service nie zależał od WPF.
        /// </summary>
        public class LadunekZAwizacja
        {
            public string NazwaDisplay { get; set; } = "";
            public int Kolejnosc { get; set; }
            public DateTime? Awizacja { get; set; }
            public int PojemnikiE2 { get; set; }
            public bool MaGps { get; set; }
        }
    }
}
