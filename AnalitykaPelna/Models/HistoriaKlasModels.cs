using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.AnalitykaPelna.Models
{
    /// <summary>
    /// Pojedynczy punkt z bazy: jeden dzień + jedna klasa drobiowa = liczba ważeń + suma kg.
    /// Surowy zapis przed agregacją po okresach (tydzień/miesiąc/kwartał/rok).
    /// </summary>
    public class HistoriaKlasPunkt
    {
        public DateTime Data { get; set; }
        public int Klasa { get; set; }            // 4..12
        public int LiczbaWazen { get; set; }
        public decimal SumaKg { get; set; }
    }

    /// <summary>
    /// Zagregowany rekord — jeden okres (np. "Tydzień 18/2026") × wszystkie klasy.
    /// Słownik per-klasa pozwala na sumowanie do grup (Duży 4-7, Mały 8-12, Razem 4-12).
    /// </summary>
    public class HistoriaKlasOkres
    {
        public string Klucz { get; set; } = "";
        public string Etykieta { get; set; } = "";
        public string EtykietaKrotka { get; set; } = "";   // dla osi X wykresu
        public DateTime DataOd { get; set; }
        public DateTime DataDo { get; set; }
        public Dictionary<int, decimal> KgPerKlasa { get; set; } = new();
        public Dictionary<int, int> WazeniaPerKlasa { get; set; } = new();

        public decimal SumaKg => KgPerKlasa.Values.Sum();
        public int SumaWazen => WazeniaPerKlasa.Values.Sum();

        public decimal KgGrupa(IEnumerable<int> klasy)
            => klasy.Sum(k => KgPerKlasa.TryGetValue(k, out var v) ? v : 0m);

        public int WazeniaGrupa(IEnumerable<int> klasy)
            => klasy.Sum(k => WazeniaPerKlasa.TryGetValue(k, out var v) ? v : 0);
    }

    /// <summary>Co prezentuje wykres: kg (suma wagi) lub szt. (liczba ważeń).</summary>
    public enum HistoriaMetryka { Kg, Wazenia }

    /// <summary>Tryb prezentacji: Per-klasa (4..12 osobne serie) lub Per-grupa (Duży/Mały/Razem).</summary>
    public enum HistoriaTryb { PerKlasa, PerGrupa }
}
