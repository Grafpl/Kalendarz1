using System;
using System.Collections.Generic;
using System.Linq;
using Kalendarz1.Transport.Controls;

namespace Kalendarz1.Transport.Services
{
    /// <summary>
    /// Silnik wykrywania 14 typów konfliktów w kursie transportowym.
    /// Wywołuj DetectAll() po każdej zmianie kursu.
    /// </summary>
    public class ConflictDetectionService
    {
        // ═══════════════════════════════════════════
        //  KODY KONFLIKTÓW
        // ═══════════════════════════════════════════
        public const string NO_DRIVER = "NO_DRIVER";
        public const string NO_VEHICLE = "NO_VEHICLE";
        public const string CAPACITY_OVERLOAD = "CAPACITY_OVERLOAD";
        public const string WEIGHT_OVERLOAD = "WEIGHT_OVERLOAD";
        public const string DRIVER_DOUBLE_BOOKING = "DRIVER_DOUBLE_BOOKING";
        public const string VEHICLE_DOUBLE_BOOKING = "VEHICLE_DOUBLE_BOOKING";
        public const string CAPACITY_HIGH = "CAPACITY_HIGH";
        public const string WEIGHT_HIGH = "WEIGHT_HIGH";
        public const string DRIVER_HOURS = "DRIVER_HOURS";
        public const string DUPLICATE_CLIENT = "DUPLICATE_CLIENT";
        public const string FOREIGN_ADDRESS = "FOREIGN_ADDRESS";
        public const string RETURN_LATE = "RETURN_LATE";
        public const string NEARBY_ORDER = "NEARBY_ORDER";
        public const string MULTI_HANDLOWIEC = "MULTI_HANDLOWIEC";

        /// <summary>Słowa kluczowe wskazujące na adres zagraniczny (potrzebny CMR).</summary>
        private static readonly string[] ForeignKeywords = {
            "rumunia", "romania", "mun.", "str.", "deutschland", "germany",
            "czech", "slovakia", "slovensko", "česko", "ukraine", "ukraina",
            "litwa", "lithuania", "łotwa", "latvia", "hungary", "węgry",
            "belarus", "białoruś", "france", "francja", "italia", "włochy"
        };

        /// <summary>
        /// Dane kursu potrzebne do detekcji konfliktów.
        /// </summary>
        public class CourseData
        {
            public long? KursId { get; set; }
            public DateTime DataKursu { get; set; }
            public int? KierowcaId { get; set; }
            public int? PojazdId { get; set; }
            public int PaletyPojazdu { get; set; }
            public TimeSpan? GodzinaWyjazdu { get; set; }
            public TimeSpan? GodzinaPowrotu { get; set; }
            public decimal SumaPalet { get; set; }
            public int SumaPojemnikow { get; set; }
            public int SumaWagaKg { get; set; }
            public int DmcKg { get; set; } = 18000;

            public List<LoadItem> Ladunki { get; set; } = new();
        }

        public class LoadItem
        {
            public string? KodKlienta { get; set; }
            public string? NazwaKlienta { get; set; }
            public string? Adres { get; set; }
            public string? Handlowiec { get; set; }
            public decimal Palety { get; set; }
            public TimeSpan? PlannedArrival { get; set; }
        }

        /// <summary>
        /// Informacja o innym kursie tego samego dnia (do sprawdzania podwójnych rezerwacji).
        /// </summary>
        public class OtherCourse
        {
            public long KursId { get; set; }
            public int? KierowcaId { get; set; }
            public int? PojazdId { get; set; }
            public TimeSpan? GodzinaWyjazdu { get; set; }
            public TimeSpan? GodzinaPowrotu { get; set; }
            public List<string> KodyKlientow { get; set; } = new();
        }

        /// <summary>
        /// Wolne zamówienie z tego samego regionu (do NEARBY_ORDER).
        /// </summary>
        public class FreeOrder
        {
            public int ZamowienieId { get; set; }
            public string? KodPocztowy { get; set; }
            public string? NazwaKlienta { get; set; }
        }

        /// <summary>
        /// Wykryj wszystkie konflikty w kursie.
        /// </summary>
        public List<CourseConflict> DetectAll(
            CourseData course,
            IEnumerable<OtherCourse>? otherCourses = null,
            IEnumerable<FreeOrder>? freeOrders = null)
        {
            var conflicts = new List<CourseConflict>();
            var others = otherCourses?.ToList() ?? new List<OtherCourse>();
            var free = freeOrders?.ToList() ?? new List<FreeOrder>();

            // ── BŁĘDY (Error) ──────────────────────
            // 1. NO_DRIVER
            if (!course.KierowcaId.HasValue)
                conflicts.Add(new CourseConflict
                {
                    Code = NO_DRIVER,
                    Level = ConflictLevel.Error,
                    Message = "Brak kierowcy — przypisz kierowcę do kursu"
                });

            // 2. NO_VEHICLE
            if (!course.PojazdId.HasValue)
                conflicts.Add(new CourseConflict
                {
                    Code = NO_VEHICLE,
                    Level = ConflictLevel.Error,
                    Message = "Brak pojazdu — przypisz pojazd do kursu"
                });

            // 3. CAPACITY_OVERLOAD
            if (course.PaletyPojazdu > 0 && course.SumaPalet > course.PaletyPojazdu)
            {
                decimal proc = course.SumaPalet / course.PaletyPojazdu * 100m;
                conflicts.Add(new CourseConflict
                {
                    Code = CAPACITY_OVERLOAD,
                    Level = ConflictLevel.Error,
                    Message = $"Przeładowanie palet: {course.SumaPalet:N1}/{course.PaletyPojazdu} ({proc:F0}%)",
                    Detail = "Naczepa jest przeładowana powyżej 100% pojemności."
                });
            }

            // 4. WEIGHT_OVERLOAD
            if (course.DmcKg > 0 && course.SumaWagaKg > course.DmcKg)
            {
                conflicts.Add(new CourseConflict
                {
                    Code = WEIGHT_OVERLOAD,
                    Level = ConflictLevel.Error,
                    Message = $"Przekroczenie DMC: {course.SumaWagaKg:N0}/{course.DmcKg:N0} kg",
                    Detail = "Waga towaru przekracza dopuszczalną masę całkowitą pojazdu."
                });
            }

            // 5. DRIVER_DOUBLE_BOOKING
            if (course.KierowcaId.HasValue)
            {
                var driverConflict = others.FirstOrDefault(o =>
                    o.KursId != course.KursId &&
                    o.KierowcaId == course.KierowcaId &&
                    TimesOverlap(course.GodzinaWyjazdu, course.GodzinaPowrotu, o.GodzinaWyjazdu, o.GodzinaPowrotu));

                if (driverConflict != null)
                {
                    conflicts.Add(new CourseConflict
                    {
                        Code = DRIVER_DOUBLE_BOOKING,
                        Level = ConflictLevel.Error,
                        Message = $"Kierowca przypisany do innego kursu #{driverConflict.KursId} w tym samym czasie",
                        Detail = $"Kurs #{driverConflict.KursId}: {driverConflict.GodzinaWyjazdu:hh\\:mm}-{driverConflict.GodzinaPowrotu:hh\\:mm}"
                    });
                }
            }

            // 6. VEHICLE_DOUBLE_BOOKING
            if (course.PojazdId.HasValue)
            {
                var vehicleConflict = others.FirstOrDefault(o =>
                    o.KursId != course.KursId &&
                    o.PojazdId == course.PojazdId &&
                    TimesOverlap(course.GodzinaWyjazdu, course.GodzinaPowrotu, o.GodzinaWyjazdu, o.GodzinaPowrotu));

                if (vehicleConflict != null)
                {
                    conflicts.Add(new CourseConflict
                    {
                        Code = VEHICLE_DOUBLE_BOOKING,
                        Level = ConflictLevel.Error,
                        Message = $"Pojazd przypisany do innego kursu #{vehicleConflict.KursId} w tym samym czasie"
                    });
                }
            }

            // ── OSTRZEŻENIA (Warning) ──────────────
            // 7. CAPACITY_HIGH
            if (course.PaletyPojazdu > 0 && course.SumaPalet <= course.PaletyPojazdu)
            {
                decimal proc = course.SumaPalet / course.PaletyPojazdu * 100m;
                if (proc > 80)
                {
                    conflicts.Add(new CourseConflict
                    {
                        Code = CAPACITY_HIGH,
                        Level = ConflictLevel.Warning,
                        Message = $"Naczepa zapełniona w {proc:F0}% — bliskie limitu"
                    });
                }
            }

            // 8. WEIGHT_HIGH
            if (course.DmcKg > 0 && course.SumaWagaKg > 0 && course.SumaWagaKg <= course.DmcKg)
            {
                decimal procW = (decimal)course.SumaWagaKg / course.DmcKg * 100m;
                if (procW > 80)
                {
                    conflicts.Add(new CourseConflict
                    {
                        Code = WEIGHT_HIGH,
                        Level = ConflictLevel.Warning,
                        Message = $"Waga {course.SumaWagaKg:N0} kg to {procW:F0}% DMC"
                    });
                }
            }

            // 9. DRIVER_HOURS
            if (course.GodzinaWyjazdu.HasValue && course.GodzinaPowrotu.HasValue)
            {
                var hours = (course.GodzinaPowrotu.Value - course.GodzinaWyjazdu.Value).TotalHours;
                if (hours > 12)
                {
                    conflicts.Add(new CourseConflict
                    {
                        Code = DRIVER_HOURS,
                        Level = ConflictLevel.Warning,
                        Message = $"Czas pracy kierowcy: {hours:F1}h (>12h)",
                        Detail = "Czas od godziny wyjazdu do powrotu przekracza 12 godzin."
                    });
                }
            }

            // 10. DUPLICATE_CLIENT
            foreach (var ladunek in course.Ladunki)
            {
                if (string.IsNullOrEmpty(ladunek.KodKlienta)) continue;
                var duplicate = others.FirstOrDefault(o =>
                    o.KursId != course.KursId &&
                    o.KodyKlientow.Contains(ladunek.KodKlienta));

                if (duplicate != null)
                {
                    conflicts.Add(new CourseConflict
                    {
                        Code = DUPLICATE_CLIENT,
                        Level = ConflictLevel.Warning,
                        Message = $"Klient {ladunek.NazwaKlienta ?? ladunek.KodKlienta} jest też w kursie #{duplicate.KursId}"
                    });
                    break; // Jeden alert wystarczy
                }
            }

            // 11. FOREIGN_ADDRESS
            foreach (var ladunek in course.Ladunki)
            {
                string adresLower = (ladunek.Adres ?? "").ToLowerInvariant() +
                                    " " + (ladunek.NazwaKlienta ?? "").ToLowerInvariant();
                if (ForeignKeywords.Any(kw => adresLower.Contains(kw)))
                {
                    conflicts.Add(new CourseConflict
                    {
                        Code = FOREIGN_ADDRESS,
                        Level = ConflictLevel.Warning,
                        Message = $"Adres zagraniczny: {ladunek.NazwaKlienta} — wymagany CMR",
                        Detail = ladunek.Adres
                    });
                }
            }

            // 12. RETURN_LATE
            if (course.GodzinaPowrotu.HasValue && course.Ladunki.Count > 0)
            {
                var lastArrival = course.Ladunki
                    .Where(l => l.PlannedArrival.HasValue)
                    .Select(l => l.PlannedArrival!.Value)
                    .DefaultIfEmpty(TimeSpan.Zero)
                    .Max();

                if (lastArrival > TimeSpan.Zero && lastArrival > course.GodzinaPowrotu.Value)
                {
                    conflicts.Add(new CourseConflict
                    {
                        Code = RETURN_LATE,
                        Level = ConflictLevel.Warning,
                        Message = $"Ostatni przystanek ({lastArrival:hh\\:mm}) po godzinie powrotu ({course.GodzinaPowrotu.Value:hh\\:mm})"
                    });
                }
            }

            // ── INFO ───────────────────────────────
            // 13. NEARBY_ORDER
            if (free.Count > 0 && course.Ladunki.Count > 0)
            {
                var coursePostalPrefixes = course.Ladunki
                    .Where(l => !string.IsNullOrEmpty(l.Adres) && l.Adres.Length >= 2)
                    .Select(l => ExtractPostalPrefix(l.Adres))
                    .Where(p => p != null)
                    .Distinct()
                    .ToHashSet();

                var nearby = free.Where(f =>
                {
                    var prefix = f.KodPocztowy?.Length >= 2 ? f.KodPocztowy.Substring(0, 2) : null;
                    return prefix != null && coursePostalPrefixes.Contains(prefix);
                }).ToList();

                if (nearby.Count > 0)
                {
                    var names = string.Join(", ", nearby.Take(3).Select(n => n.NazwaKlienta ?? $"#{n.ZamowienieId}"));
                    conflicts.Add(new CourseConflict
                    {
                        Code = NEARBY_ORDER,
                        Level = ConflictLevel.Info,
                        Message = $"Zamówienia z tego regionu: {names}{(nearby.Count > 3 ? $" (+{nearby.Count - 3})" : "")}"
                    });
                }
            }

            // 14. MULTI_HANDLOWIEC
            var handlowcy = course.Ladunki
                .Where(l => !string.IsNullOrEmpty(l.Handlowiec))
                .Select(l => l.Handlowiec!)
                .Distinct()
                .ToList();

            if (handlowcy.Count > 1)
            {
                conflicts.Add(new CourseConflict
                {
                    Code = MULTI_HANDLOWIEC,
                    Level = ConflictLevel.Info,
                    Message = $"Zamówienia od {handlowcy.Count} handlowców: {string.Join(", ", handlowcy)}"
                });
            }

            // Sortuj: Error → Warning → Info
            conflicts.Sort((a, b) => a.Level.CompareTo(b.Level));
            return conflicts;
        }

        private static bool TimesOverlap(TimeSpan? start1, TimeSpan? end1, TimeSpan? start2, TimeSpan? end2)
        {
            if (!start1.HasValue || !end1.HasValue || !start2.HasValue || !end2.HasValue)
                return true; // Jeśli brak godzin, zakładamy potencjalny konflikt

            return start1.Value < end2.Value && start2.Value < end1.Value;
        }

        private static string? ExtractPostalPrefix(string? adres)
        {
            if (string.IsNullOrEmpty(adres)) return null;

            // Szukaj wzorca XX-XXX (polski kod pocztowy)
            for (int i = 0; i <= adres.Length - 6; i++)
            {
                if (char.IsDigit(adres[i]) && char.IsDigit(adres[i + 1]) && adres[i + 2] == '-' &&
                    char.IsDigit(adres[i + 3]) && char.IsDigit(adres[i + 4]) && char.IsDigit(adres[i + 5]))
                {
                    return adres.Substring(i, 2);
                }
            }

            // Fallback: pierwsze 2 znaki
            return adres.Length >= 2 ? adres.Substring(0, 2) : null;
        }
    }
}
