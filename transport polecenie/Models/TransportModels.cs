// ═══════════════════════════════════════════════════════════════════════
// TransportModels.cs — Modele danych
// ═══════════════════════════════════════════════════════════════════════
// Klasy reprezentujące zamówienie, ładunek w kursie, kurs, pojazd,
// kierowcę, oraz konflikty wykrywane automatycznie.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace ZpspTransport.Models
{
    // ─── PRIORYTET ZAMÓWIENIA ───
    public enum OrderPriority
    {
        Low,        // Niski — szara kropka
        Normal,     // Normalny — zielona kropka
        High,       // Pilne — czerwona kropka z glow
        Express     // Ekspres — fioletowa kropka z glow
    }

    // ─── STATUS ŁADUNKU ───
    public enum StopStatus
    {
        Pending,    // Oczekuje na załadunek
        Loaded,     // Załadowany na pojazd
        InTransit,  // W trasie
        Delivered,  // Dostarczony
        Problem     // Problem (reklamacja, opóźnienie)
    }

    // ─── POZIOM KONFLIKTU ───
    public enum ConflictLevel
    {
        Info,       // Informacja — niebieskie tło
        Warning,    // Ostrzeżenie — pomarańczowe tło
        Error       // Błąd krytyczny — czerwone tło
    }

    // ═══════════════════════════════════════════════
    // ZAMÓWIENIE (prawy panel — lista wolnych zamówień)
    // ═══════════════════════════════════════════════
    /// <summary>
    /// Zamówienie od handlowca czekające na przypisanie do kursu.
    /// Wyświetlane w prawym (jasnym) panelu, pogrupowane po dacie odbioru.
    /// </summary>
    public class Order
    {
        public int Id { get; set; }
        
        /// <summary>Data uboju — kiedy towar był produkowany</summary>
        public DateTime DataUboju { get; set; }
        
        /// <summary>Data odbioru — kiedy klient chce dostać towar</summary>
        public DateTime DataOdbioru { get; set; }
        
        /// <summary>Godzina odbioru — okno dostawy klienta</summary>
        public TimeSpan GodzinaOdbioru { get; set; }
        
        /// <summary>Liczba palet — kluczowa dla pojemności naczepy</summary>
        public decimal Palety { get; set; }
        
        /// <summary>Liczba pojemników</summary>
        public int Pojemniki { get; set; }
        
        /// <summary>Waga w kg</summary>
        public decimal WagaKg { get; set; }
        
        /// <summary>Nazwa klienta (skrócona do wyświetlenia)</summary>
        public string NazwaKlienta { get; set; } = "";
        
        /// <summary>Pełna nazwa klienta</summary>
        public string PelnaNazwa { get; set; } = "";
        
        /// <summary>Adres dostawy</summary>
        public string Adres { get; set; } = "";
        
        /// <summary>Kod pocztowy</summary>
        public string KodPocztowy { get; set; } = "";
        
        /// <summary>Priorytet zamówienia</summary>
        public OrderPriority Priority { get; set; } = OrderPriority.Normal;
        
        /// <summary>Handlowiec który złożył zamówienie</summary>
        public string Handlowiec { get; set; } = "";
        
        /// <summary>Uwagi do zamówienia</summary>
        public string Uwagi { get; set; } = "";
        
        /// <summary>Czy zamówienie jest zaznaczone w UI (checkbox/klik)</summary>
        public bool IsSelected { get; set; }
        
        /// <summary>Czy zamówienie jest już przypisane do jakiegoś kursu</summary>
        public bool IsAssigned { get; set; }
        
        /// <summary>Id kursu do którego jest przypisane (0 = nieprzypisane)</summary>
        public int AssignedCourseId { get; set; }

        /// <summary>Formatuje godzinę odbioru jako string "HH:mm"</summary>
        public string GodzinaFormatted => GodzinaOdbioru.ToString(@"hh\:mm");

        /// <summary>Formatuje datę uboju jako "dd.MM ddd"</summary>
        public string DataUbojuFormatted => $"{DataUboju:dd.MM} {DayAbbr(DataUboju)}";

        /// <summary>Formatuje datę odbioru jako "dd.MM ddd"</summary>
        public string DataOdbioruFormatted => $"{DataOdbioru:dd.MM} {DayAbbr(DataOdbioru)}";

        private static string DayAbbr(DateTime d) => d.DayOfWeek switch
        {
            DayOfWeek.Monday => "pon.",
            DayOfWeek.Tuesday => "wt.",
            DayOfWeek.Wednesday => "śr.",
            DayOfWeek.Thursday => "czw.",
            DayOfWeek.Friday => "pt.",
            DayOfWeek.Saturday => "sob.",
            DayOfWeek.Sunday => "ndz.",
            _ => ""
        };
    }

    // ═══════════════════════════════════════════════
    // ŁADUNEK W KURSIE (lewy dolny panel)
    // ═══════════════════════════════════════════════
    /// <summary>
    /// Ładunek przypisany do kursu — jeden wiersz w tabeli ładunków.
    /// Pochodzi z zamówienia (Order), ale ma dodatkowe pola: kolejność, status.
    /// </summary>
    public class CourseStop
    {
        /// <summary>Numer kolejności w kursie (1, 2, 3...)</summary>
        public int Lp { get; set; }
        
        /// <summary>Referencja do oryginalnego zamówienia</summary>
        public int OrderId { get; set; }
        
        /// <summary>Nazwa klienta</summary>
        public string NazwaKlienta { get; set; } = "";
        
        /// <summary>Data uboju towaru</summary>
        public DateTime DataUboju { get; set; }
        
        /// <summary>Palety</summary>
        public decimal Palety { get; set; }
        
        /// <summary>Pojemniki</summary>
        public int Pojemniki { get; set; }
        
        /// <summary>Waga kg</summary>
        public decimal WagaKg { get; set; }
        
        /// <summary>Adres dostawy</summary>
        public string Adres { get; set; } = "";
        
        /// <summary>Uwagi (np. "LOCIV IMPEX DIA SRL Rumunia (08:00)")</summary>
        public string Uwagi { get; set; } = "";
        
        /// <summary>Status ładunku</summary>
        public StopStatus Status { get; set; } = StopStatus.Pending;
        
        /// <summary>Godzina planowanego przyjazdu</summary>
        public TimeSpan? PlannedArrival { get; set; }
    }

    // ═══════════════════════════════════════════════
    // KIEROWCA
    // ═══════════════════════════════════════════════
    public class Driver
    {
        public int Id { get; set; }
        public string Imie { get; set; } = "";
        public string Nazwisko { get; set; } = "";
        public string PelneImie => $"{Imie} {Nazwisko}";
        
        /// <summary>Inicjały do avatara (np. "RC")</summary>
        public string Inicjaly => $"{(Imie.Length > 0 ? Imie[0] : '?')}{(Nazwisko.Length > 0 ? Nazwisko[0] : '?')}";
        
        /// <summary>Numer telefonu</summary>
        public string Telefon { get; set; } = "";
        
        /// <summary>Czy aktywny (może jeździć)</summary>
        public bool IsActive { get; set; } = true;
    }

    // ═══════════════════════════════════════════════
    // POJAZD
    // ═══════════════════════════════════════════════
    public class Vehicle
    {
        public int Id { get; set; }
        
        /// <summary>Numer rejestracyjny</summary>
        public string Rejestracja { get; set; } = "";
        
        /// <summary>Maksymalna liczba palet</summary>
        public decimal MaxPalet { get; set; }
        
        /// <summary>Maksymalna liczba pojemników</summary>
        public int MaxPojemnikow { get; set; }
        
        /// <summary>Dopuszczalna masa całkowita (DMC) w kg</summary>
        public decimal DMC_Kg { get; set; }
        
        /// <summary>Opis pojazdu do wyświetlenia w combobox</summary>
        public string DisplayName => $"{Rejestracja} – {MaxPalet} palet";
        
        /// <summary>Czy pojazd jest dostępny (nie w naprawie)</summary>
        public bool IsAvailable { get; set; } = true;
    }

    // ═══════════════════════════════════════════════
    // KURS TRANSPORTOWY (cały lewy panel)
    // ═══════════════════════════════════════════════
    /// <summary>
    /// Kurs = jeden wyjazd kierowcy pojazdem na trasę.
    /// Zawiera kierowcę, pojazd, datę, godziny, listę ładunków.
    /// </summary>
    public class TransportCourse
    {
        public int Id { get; set; }
        
        /// <summary>Przypisany kierowca</summary>
        public Driver? Kierowca { get; set; }
        
        /// <summary>Przypisany pojazd</summary>
        public Vehicle? Pojazd { get; set; }
        
        /// <summary>Data wyjazdu</summary>
        public DateTime DataWyjazdu { get; set; }
        
        /// <summary>Godzina wyjazdu</summary>
        public TimeSpan GodzinaWyjazdu { get; set; }
        
        /// <summary>Godzina planowanego powrotu</summary>
        public TimeSpan GodzinaPowrotu { get; set; }
        
        /// <summary>Lista ładunków (przystanków) w kursie</summary>
        public List<CourseStop> Stops { get; set; } = new();
        
        /// <summary>Kto utworzył kurs</summary>
        public string CreatedBy { get; set; } = "";
        
        /// <summary>Kiedy utworzono</summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>Handlowcy powiązani z zamówieniami w kursie</summary>
        public List<string> Handlowcy { get; set; } = new();

        // ─── OBLICZENIA ───
        
        /// <summary>Suma palet w kursie</summary>
        public decimal SumaPalet => Stops.Sum(s => s.Palety);
        
        /// <summary>Suma pojemników w kursie</summary>
        public int SumaPojemnikow => Stops.Sum(s => s.Pojemniki);
        
        /// <summary>Suma wagi w kursie</summary>
        public decimal SumaWagaKg => Stops.Sum(s => s.WagaKg);
        
        /// <summary>Procent wypełnienia naczepy (palety)</summary>
        public decimal WypelnienieProcent => Pojazd != null && Pojazd.MaxPalet > 0
            ? (SumaPalet / Pojazd.MaxPalet) * 100m
            : 0m;
        
        /// <summary>Czy naczepa jest przeładowana</summary>
        public bool IsPrzeladowane => Pojazd != null && SumaPalet > Pojazd.MaxPalet;
        
        /// <summary>Automatyczny opis trasy: "LOCIV IMPEX → PODOLSKI"</summary>
        public string TrasaOpis => Stops.Count == 0
            ? "(brak ładunków)"
            : string.Join(" → ", Stops.OrderBy(s => s.Lp).Select(s => s.NazwaKlienta));
    }

    // ═══════════════════════════════════════════════
    // KONFLIKT (panel wykrywania problemów)
    // ═══════════════════════════════════════════════
    /// <summary>
    /// Wykryty konflikt/problem w kursie.
    /// Wyświetlany w panelu alertów pod capacity barem.
    /// </summary>
    public class CourseConflict
    {
        /// <summary>Poziom: Info, Warning, Error</summary>
        public ConflictLevel Level { get; set; }
        
        /// <summary>Kod konfliktu (do programistycznego obsłużenia)</summary>
        public string Code { get; set; } = "";
        
        /// <summary>Opis konfliktu czytelny dla logistyka</summary>
        public string Message { get; set; } = "";
        
        /// <summary>Szczegóły (opcjonalnie — np. wartości liczbowe)</summary>
        public string? Details { get; set; }
        
        /// <summary>Ikona emoji dla UI</summary>
        public string Icon => Level switch
        {
            ConflictLevel.Error => "🔴",
            ConflictLevel.Warning => "🟡",
            ConflictLevel.Info => "🔵",
            _ => "⚪"
        };
    }
}
