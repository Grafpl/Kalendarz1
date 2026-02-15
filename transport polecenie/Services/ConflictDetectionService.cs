// ═══════════════════════════════════════════════════════════════════════
// ConflictDetectionService.cs — Silnik wykrywania konfliktów
// ═══════════════════════════════════════════════════════════════════════
// Wywoływany po każdej zmianie w kursie (dodanie/usunięcie ładunku,
// zmiana kierowcy, zmiana pojazdu, zmiana godzin).
// Zwraca listę CourseConflict do wyświetlenia w UI.
//
// UŻYCIE:
//   var service = new ConflictDetectionService();
//   List<CourseConflict> conflicts = service.DetectAll(course, allOrders, allCourses);
//   conflictPanel.SetConflicts(conflicts);
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using ZpspTransport.Models;

namespace ZpspTransport.Services
{
    public class ConflictDetectionService
    {
        // ─── KONFIGURACJA PROGÓW ───
        // Zmień te wartości jeśli chcesz inne progi ostrzeżeń
        
        /// <summary>Procent wypełnienia od którego jest ostrzeżenie (np. 80%)</summary>
        public decimal CapacityWarningThreshold { get; set; } = 80m;
        
        /// <summary>Maksymalny czas pracy kierowcy w godzinach</summary>
        public decimal MaxDriverHours { get; set; } = 12m;
        
        /// <summary>Maksymalna masa całkowita pojazdu (DMC) w kg</summary>
        public decimal DefaultDMC { get; set; } = 18000m;
        
        /// <summary>Waga tary pojazdu (pusty pojazd) w kg</summary>
        public decimal VehicleTareWeight { get; set; } = 7500m;
        
        /// <summary>Słowa kluczowe wskazujące na adres zagraniczny</summary>
        private readonly string[] _foreignKeywords = {
            "Romania", "Rumunia", "MUN.", "STR.", "Deutschland", "Germany",
            "Czech", "Czechy", "Slovakia", "Słowacja", "Hungary", "Węgry",
            "Ukraine", "Ukraina", "Lithuania", "Litwa"
        };

        // ═══════════════════════════════════════════════
        // GŁÓWNA METODA — WYKRYJ WSZYSTKIE KONFLIKTY
        // ═══════════════════════════════════════════════
        /// <summary>
        /// Wykrywa wszystkie konflikty dla danego kursu.
        /// Wywołuj po KAŻDEJ zmianie w kursie.
        /// </summary>
        /// <param name="course">Aktualny kurs do sprawdzenia</param>
        /// <param name="allOrders">Wszystkie zamówienia (do sprawdzenia duplikatów)</param>
        /// <param name="allCourses">Wszystkie inne kursy (do sprawdzenia kolizji)</param>
        /// <returns>Lista wykrytych konfliktów posortowana: Error → Warning → Info</returns>
        public List<CourseConflict> DetectAll(
            TransportCourse course,
            List<Order> allOrders,
            List<TransportCourse> allCourses)
        {
            var conflicts = new List<CourseConflict>();

            // Kolejność sprawdzania: od najważniejszych do informacyjnych
            CheckNoDriver(course, conflicts);
            CheckNoVehicle(course, conflicts);
            CheckCapacityOverload(course, conflicts);
            CheckWeightOverload(course, conflicts);
            CheckDriverWorkingHours(course, conflicts);
            CheckDriverDoubleBooking(course, allCourses, conflicts);
            CheckVehicleDoubleBooking(course, allCourses, conflicts);
            CheckDuplicateClient(course, allCourses, conflicts);
            CheckForeignAddress(course, conflicts);
            CheckDeliveryTimeConflicts(course, conflicts);
            CheckEmptyCourse(course, conflicts);
            CheckSingleStopOptimization(course, conflicts);
            CheckNearbyAddresses(course, allOrders, conflicts);
            CheckHandlowiecNotification(course, conflicts);

            // Sortuj: Error na górze, potem Warning, potem Info
            return conflicts
                .OrderBy(c => c.Level)
                .ThenBy(c => c.Code)
                .ToList();
        }

        // ═══════════════════════════════════════════════
        // POSZCZEGÓLNE SPRAWDZENIA
        // ═══════════════════════════════════════════════

        /// <summary>
        /// 1. BRAK KIEROWCY — Error
        /// Kurs nie ma przypisanego kierowcy.
        /// </summary>
        private void CheckNoDriver(TransportCourse course, List<CourseConflict> conflicts)
        {
            if (course.Kierowca == null)
            {
                conflicts.Add(new CourseConflict
                {
                    Level = ConflictLevel.Error,
                    Code = "NO_DRIVER",
                    Message = "Brak przypisanego kierowcy do kursu",
                    Details = "Wybierz kierowcę z listy rozwijanej"
                });
            }
        }

        /// <summary>
        /// 2. BRAK POJAZDU — Error
        /// Kurs nie ma przypisanego pojazdu.
        /// </summary>
        private void CheckNoVehicle(TransportCourse course, List<CourseConflict> conflicts)
        {
            if (course.Pojazd == null)
            {
                conflicts.Add(new CourseConflict
                {
                    Level = ConflictLevel.Error,
                    Code = "NO_VEHICLE",
                    Message = "Brak przypisanego pojazdu do kursu",
                    Details = "Wybierz pojazd z listy rozwijanej"
                });
            }
        }

        /// <summary>
        /// 3. PRZEŁADOWANIE NACZEPY (PALETY) — Error gdy >100%, Warning gdy >80%
        /// Najczęstszy problem — suma palet przekracza pojemność pojazdu.
        /// </summary>
        private void CheckCapacityOverload(TransportCourse course, List<CourseConflict> conflicts)
        {
            if (course.Pojazd == null || course.Stops.Count == 0) return;

            decimal pct = course.WypelnienieProcent;
            decimal sumPalet = course.SumaPalet;
            decimal maxPalet = course.Pojazd.MaxPalet;
            decimal brakuje = sumPalet - maxPalet;

            if (sumPalet > maxPalet)
            {
                conflicts.Add(new CourseConflict
                {
                    Level = ConflictLevel.Error,
                    Code = "CAPACITY_OVERLOAD",
                    Message = $"Przeładowanie naczepy: {sumPalet:F1} palet / {maxPalet} max ({pct:F0}%)",
                    Details = $"Nadmiar: {brakuje:F1} palet. Przenieś ładunki do innego kursu lub zmień pojazd na większy."
                });
            }
            else if (pct >= CapacityWarningThreshold)
            {
                conflicts.Add(new CourseConflict
                {
                    Level = ConflictLevel.Warning,
                    Code = "CAPACITY_HIGH",
                    Message = $"Naczepa prawie pełna: {sumPalet:F1} / {maxPalet} palet ({pct:F0}%)",
                    Details = $"Zostało miejsce na {(maxPalet - sumPalet):F1} palet"
                });
            }
        }

        /// <summary>
        /// 4. PRZEKROCZENIE DMC (WAGA) — Error gdy >DMC, Warning gdy >80% DMC
        /// Sprawdza czy waga towaru + tara pojazdu nie przekracza DMC.
        /// </summary>
        private void CheckWeightOverload(TransportCourse course, List<CourseConflict> conflicts)
        {
            if (course.Pojazd == null || course.Stops.Count == 0) return;

            decimal dmc = course.Pojazd.DMC_Kg > 0 ? course.Pojazd.DMC_Kg : DefaultDMC;
            decimal maxPayload = dmc - VehicleTareWeight;
            decimal totalWeight = course.SumaWagaKg;
            decimal pct = maxPayload > 0 ? (totalWeight / maxPayload) * 100m : 0;

            if (totalWeight > maxPayload)
            {
                conflicts.Add(new CourseConflict
                {
                    Level = ConflictLevel.Error,
                    Code = "WEIGHT_OVERLOAD",
                    Message = $"Przekroczenie DMC: {totalWeight:F0} kg towaru / {maxPayload:F0} kg max ładowności",
                    Details = $"DMC pojazdu: {dmc:F0} kg, tara: {VehicleTareWeight:F0} kg. Nadmiar: {(totalWeight - maxPayload):F0} kg"
                });
            }
            else if (pct >= 80)
            {
                conflicts.Add(new CourseConflict
                {
                    Level = ConflictLevel.Warning,
                    Code = "WEIGHT_HIGH",
                    Message = $"Blisko limitu wagi: {totalWeight:F0} / {maxPayload:F0} kg ({pct:F0}%)",
                    Details = $"Zostało {(maxPayload - totalWeight):F0} kg do limitu"
                });
            }
        }

        /// <summary>
        /// 5. CZAS PRACY KIEROWCY — Warning gdy kurs dłuższy niż MaxDriverHours
        /// Sprawdza różnicę godzina powrotu - godzina wyjazdu.
        /// </summary>
        private void CheckDriverWorkingHours(TransportCourse course, List<CourseConflict> conflicts)
        {
            if (course.Kierowca == null) return;

            double hours = (course.GodzinaPowrotu - course.GodzinaWyjazdu).TotalHours;
            if (hours < 0) hours += 24; // kurs przez północ

            if (hours > (double)MaxDriverHours)
            {
                conflicts.Add(new CourseConflict
                {
                    Level = ConflictLevel.Warning,
                    Code = "DRIVER_HOURS",
                    Message = $"Czas pracy kierowcy: {hours:F1}h (max dozwolone: {MaxDriverHours}h)",
                    Details = $"Wyjazd {course.GodzinaWyjazdu:hh\\:mm} → powrót {course.GodzinaPowrotu:hh\\:mm}. " +
                              $"Sprawdź regulacje czasu pracy kierowców."
                });
            }
        }

        /// <summary>
        /// 6. KIEROWCA PODWÓJNIE ZAREZERWOWANY — Error
        /// Ten sam kierowca jest przypisany do innego kursu w tym samym dniu
        /// z nachodzącymi się godzinami.
        /// </summary>
        private void CheckDriverDoubleBooking(
            TransportCourse course, List<TransportCourse> allCourses, List<CourseConflict> conflicts)
        {
            if (course.Kierowca == null) return;

            var overlapping = allCourses
                .Where(c => c.Id != course.Id
                    && c.Kierowca?.Id == course.Kierowca.Id
                    && c.DataWyjazdu.Date == course.DataWyjazdu.Date
                    && TimesOverlap(course.GodzinaWyjazdu, course.GodzinaPowrotu,
                                    c.GodzinaWyjazdu, c.GodzinaPowrotu))
                .ToList();

            foreach (var other in overlapping)
            {
                conflicts.Add(new CourseConflict
                {
                    Level = ConflictLevel.Error,
                    Code = "DRIVER_DOUBLE_BOOKING",
                    Message = $"Kierowca {course.Kierowca.PelneImie} jest już przypisany do kursu #{other.Id} " +
                              $"({other.GodzinaWyjazdu:hh\\:mm}–{other.GodzinaPowrotu:hh\\:mm})",
                    Details = $"Trasa kursu #{other.Id}: {other.TrasaOpis}. " +
                              $"Zmień kierowcę lub dostosuj godziny."
                });
            }
        }

        /// <summary>
        /// 7. POJAZD PODWÓJNIE ZAREZERWOWANY — Error
        /// Ten sam pojazd jest przypisany do innego kursu z nachodzącym czasem.
        /// </summary>
        private void CheckVehicleDoubleBooking(
            TransportCourse course, List<TransportCourse> allCourses, List<CourseConflict> conflicts)
        {
            if (course.Pojazd == null) return;

            var overlapping = allCourses
                .Where(c => c.Id != course.Id
                    && c.Pojazd?.Id == course.Pojazd.Id
                    && c.DataWyjazdu.Date == course.DataWyjazdu.Date
                    && TimesOverlap(course.GodzinaWyjazdu, course.GodzinaPowrotu,
                                    c.GodzinaWyjazdu, c.GodzinaPowrotu))
                .ToList();

            foreach (var other in overlapping)
            {
                conflicts.Add(new CourseConflict
                {
                    Level = ConflictLevel.Error,
                    Code = "VEHICLE_DOUBLE_BOOKING",
                    Message = $"Pojazd {course.Pojazd.Rejestracja} jest już używany w kursie #{other.Id} " +
                              $"({other.GodzinaWyjazdu:hh\\:mm}–{other.GodzinaPowrotu:hh\\:mm})",
                    Details = $"Kierowca kursu #{other.Id}: {other.Kierowca?.PelneImie ?? "brak"}. " +
                              $"Zmień pojazd lub dostosuj godziny."
                });
            }
        }

        /// <summary>
        /// 8. TEN SAM KLIENT W DWÓCH KURSACH — Warning
        /// Ten sam klient ma ładunek w bieżącym kursie i w innym kursie tego dnia.
        /// Może być zamierzone (duże zamówienie podzielone) ale warto ostrzec.
        /// </summary>
        private void CheckDuplicateClient(
            TransportCourse course, List<TransportCourse> allCourses, List<CourseConflict> conflicts)
        {
            var myClients = course.Stops.Select(s => s.NazwaKlienta.ToLower().Trim()).ToHashSet();

            foreach (var other in allCourses.Where(c => c.Id != course.Id))
            {
                var duplicates = other.Stops
                    .Where(s => myClients.Contains(s.NazwaKlienta.ToLower().Trim()))
                    .Select(s => s.NazwaKlienta)
                    .Distinct()
                    .ToList();

                foreach (var clientName in duplicates)
                {
                    conflicts.Add(new CourseConflict
                    {
                        Level = ConflictLevel.Warning,
                        Code = "DUPLICATE_CLIENT",
                        Message = $"Klient \"{clientName}\" jest też w kursie #{other.Id} ({other.Kierowca?.PelneImie ?? "brak kierowcy"})",
                        Details = "Jeśli to zamierzone (split zamówienia) — OK. " +
                                  "Jeśli pomyłka — usuń z jednego z kursów."
                    });
                }
            }
        }

        /// <summary>
        /// 9. ADRES ZAGRANICZNY — Warning
        /// Sprawdza czy adres wskazuje na zagranicę → potrzebne CMR, dokumenty celne.
        /// </summary>
        private void CheckForeignAddress(TransportCourse course, List<CourseConflict> conflicts)
        {
            foreach (var stop in course.Stops)
            {
                string combined = $"{stop.Adres} {stop.Uwagi}".ToLower();
                bool isForeign = _foreignKeywords.Any(k => combined.Contains(k.ToLower()));

                if (isForeign)
                {
                    conflicts.Add(new CourseConflict
                    {
                        Level = ConflictLevel.Warning,
                        Code = "FOREIGN_ADDRESS",
                        Message = $"Adres zagraniczny: {stop.NazwaKlienta} — sprawdź dokumenty CMR i celne",
                        Details = $"Adres: {stop.Adres}. Upewnij się że kierowca ma: " +
                                  $"CMR, fakturę eksportową, świadectwo zdrowia, licencję transportową UE."
                    });
                }
            }
        }

        /// <summary>
        /// 10. KONFLIKT GODZIN DOSTAWY — Warning
        /// Sprawdza czy godziny odbioru klientów są w logicznej kolejności
        /// i czy kierowca zdąży z jednego na drugi.
        /// </summary>
        private void CheckDeliveryTimeConflicts(TransportCourse course, List<CourseConflict> conflicts)
        {
            var stopsWithTime = course.Stops
                .Where(s => s.PlannedArrival.HasValue)
                .OrderBy(s => s.Lp)
                .ToList();

            for (int i = 1; i < stopsWithTime.Count; i++)
            {
                var prev = stopsWithTime[i - 1];
                var curr = stopsWithTime[i];

                // Jeśli następny przystanek ma wcześniejszą godzinę niż poprzedni
                if (curr.PlannedArrival!.Value < prev.PlannedArrival!.Value)
                {
                    conflicts.Add(new CourseConflict
                    {
                        Level = ConflictLevel.Warning,
                        Code = "TIME_ORDER",
                        Message = $"Odwrócona kolejność czasowa: {curr.NazwaKlienta} ({curr.PlannedArrival:hh\\:mm}) " +
                                  $"jest po {prev.NazwaKlienta} ({prev.PlannedArrival:hh\\:mm}) ale ma wcześniejszą godzinę",
                        Details = "Rozważ zmianę kolejności przystanków."
                    });
                }

                // Sprawdź czy jest minimum 30 min między przystankami
                double minutesBetween = (curr.PlannedArrival!.Value - prev.PlannedArrival!.Value).TotalMinutes;
                if (minutesBetween > 0 && minutesBetween < 30)
                {
                    conflicts.Add(new CourseConflict
                    {
                        Level = ConflictLevel.Info,
                        Code = "TIME_TIGHT",
                        Message = $"Mało czasu między {prev.NazwaKlienta} a {curr.NazwaKlienta}: " +
                                  $"{minutesBetween:F0} min (min. 30 min na rozładunek)",
                        Details = "Sprawdź czy kierowca zdąży dojechać i rozładować."
                    });
                }
            }

            // Sprawdź czy ostatni przystanek mieści się przed godziną powrotu
            if (stopsWithTime.Count > 0)
            {
                var lastStop = stopsWithTime.Last();
                if (lastStop.PlannedArrival.HasValue && lastStop.PlannedArrival.Value > course.GodzinaPowrotu)
                {
                    conflicts.Add(new CourseConflict
                    {
                        Level = ConflictLevel.Warning,
                        Code = "RETURN_LATE",
                        Message = $"Ostatni przystanek ({lastStop.NazwaKlienta}) o {lastStop.PlannedArrival:hh\\:mm} " +
                                  $"— kierowca wróci po planowanej godzinie powrotu ({course.GodzinaPowrotu:hh\\:mm})",
                        Details = "Zmień godzinę powrotu lub przenieś ładunek do innego kursu."
                    });
                }
            }
        }

        /// <summary>
        /// 11. PUSTY KURS — Info
        /// Kurs nie ma żadnych ładunków.
        /// </summary>
        private void CheckEmptyCourse(TransportCourse course, List<CourseConflict> conflicts)
        {
            if (course.Stops.Count == 0)
            {
                conflicts.Add(new CourseConflict
                {
                    Level = ConflictLevel.Info,
                    Code = "EMPTY_COURSE",
                    Message = "Kurs nie ma żadnych ładunków",
                    Details = "Dodaj zamówienia z prawego panelu (kliknij 2x lub przeciągnij)."
                });
            }
        }

        /// <summary>
        /// 12. JEDEN PRZYSTANEK — Info
        /// Kurs ma tylko 1 przystanek — może warto połączyć z innym kursem.
        /// </summary>
        private void CheckSingleStopOptimization(TransportCourse course, List<CourseConflict> conflicts)
        {
            if (course.Stops.Count == 1 && course.SumaPalet < (course.Pojazd?.MaxPalet ?? 999) * 0.5m)
            {
                conflicts.Add(new CourseConflict
                {
                    Level = ConflictLevel.Info,
                    Code = "SINGLE_STOP_LOW",
                    Message = $"Kurs z 1 przystankiem i niskim wypełnieniem ({course.WypelnienieProcent:F0}%)",
                    Details = "Rozważ połączenie z innym kursem jadącym w tym kierunku."
                });
            }
        }

        /// <summary>
        /// 13. BLISCY ADRESACI — Info
        /// Klienci z podobnymi kodami pocztowymi mogą być w jednym kursie.
        /// Szuka zamówień nieprzypisanych które mają zbliżony kod pocztowy do ładunków w kursie.
        /// </summary>
        private void CheckNearbyAddresses(
            TransportCourse course, List<Order> allOrders, List<CourseConflict> conflicts)
        {
            if (course.Stops.Count == 0) return;

            // Wyciągnij kody pocztowe z ładunków w kursie (pierwsze 2 cyfry = region)
            var courseRegions = course.Stops
                .Select(s => ExtractPostalPrefix(s.Adres))
                .Where(p => p != null)
                .ToHashSet();

            if (courseRegions.Count == 0) return;

            // Szukaj nieprzypisanych zamówień z tego samego regionu
            var nearbyUnassigned = allOrders
                .Where(o => !o.IsAssigned)
                .Where(o => {
                    var prefix = ExtractPostalPrefix(o.Adres);
                    return prefix != null && courseRegions.Contains(prefix);
                })
                .Take(3) // max 3 sugestie
                .ToList();

            foreach (var order in nearbyUnassigned)
            {
                conflicts.Add(new CourseConflict
                {
                    Level = ConflictLevel.Info,
                    Code = "NEARBY_ORDER",
                    Message = $"Zamówienie {order.NazwaKlienta} ({order.Palety:F1} pal) ma zbliżony adres — rozważ dodanie do tego kursu",
                    Details = $"Adres: {order.Adres}"
                });
            }
        }

        /// <summary>
        /// 14. POWIADOMIENIE HANDLOWCA — Info
        /// Jeśli w kursie są zamówienia od wielu handlowców, warto ich poinformować.
        /// </summary>
        private void CheckHandlowiecNotification(TransportCourse course, List<CourseConflict> conflicts)
        {
            if (course.Handlowcy.Count > 1)
            {
                conflicts.Add(new CourseConflict
                {
                    Level = ConflictLevel.Info,
                    Code = "MULTI_HANDLOWIEC",
                    Message = $"Kurs zawiera zamówienia od {course.Handlowcy.Count} handlowców: {string.Join(", ", course.Handlowcy)}",
                    Details = "Rozważ powiadomienie wszystkich handlowców o godzinach dostawy."
                });
            }
        }

        // ─── HELPERY ───

        /// <summary>Sprawdza czy dwa przedziały czasowe się nakładają</summary>
        private static bool TimesOverlap(TimeSpan start1, TimeSpan end1, TimeSpan start2, TimeSpan end2)
        {
            return start1 < end2 && start2 < end1;
        }

        /// <summary>Wyciąga prefiks kodu pocztowego (np. "05" z "05-555 Aleja Krakowska")</summary>
        private static string? ExtractPostalPrefix(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;

            // Szukaj wzorca XX-XXX (polski kod pocztowy)
            for (int i = 0; i <= address.Length - 6; i++)
            {
                if (char.IsDigit(address[i]) && char.IsDigit(address[i + 1])
                    && address[i + 2] == '-'
                    && char.IsDigit(address[i + 3]))
                {
                    return address.Substring(i, 2); // pierwsze 2 cyfry
                }
            }
            return null;
        }
    }
}
