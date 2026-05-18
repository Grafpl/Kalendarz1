using System;
using System.Collections.Generic;

namespace Kalendarz1.HalaLive.Models
{
    /// <summary>
    /// Kontener na wszystkie dane potrzebne do dashboarda HalaLive.
    /// Wszystko ładowane równolegle przez Task.WhenAll w HalaLiveService.
    /// </summary>
    public class HalaLiveData
    {
        public DateTime SnapshotAt { get; set; } = DateTime.Now;

        // Pracownicy
        public int PracownicyObecni { get; set; }
        public Dictionary<string, int> PracownicyPerGrupa { get; set; } = new();

        // Żywiec
        public decimal ZywiecKgDzis { get; set; }
        public decimal ZywiecPlanKg { get; set; } = 200_000m; // 200 ton domyślnie
        public int ZywiecLiczbaSztuk { get; set; }
        public int ZywiecLiczbaDostaw { get; set; }

        // Klasy A/B (z In0E.QntInCont)
        public List<KlasaWagowa> Klasy { get; set; } = new();
        public decimal KlasyDuzyProc => SumaKlasy(4, 7) / Math.Max(SumaWszystkichKlas, 1m) * 100m;
        public decimal KlasyMalyProc => SumaKlasy(8, 12) / Math.Max(SumaWszystkichKlas, 1m) * 100m;

        private decimal SumaWszystkichKlas =>
            Math.Max(SumaKlasy(1, 12), 1m);
        private decimal SumaKlasy(int min, int max)
        {
            decimal sum = 0m;
            foreach (var k in Klasy)
                if (k.Klasa >= min && k.Klasa <= max)
                    sum += k.SumaKg;
            return sum;
        }

        // Produkcja
        public int PaletyDzis { get; set; }
        public int WazeniaDzis { get; set; }

        // Wydania
        public decimal WydaniaKgDzis { get; set; }
        public int WydaniaLiczbaDokumentow { get; set; }

        // Hodowcy dziś (TOP 5)
        public List<HodowcaDzis> HodowcyTop { get; set; } = new();

        // Status (errors per source)
        public string? ErrorUnisystem { get; set; }
        public string? ErrorLibraNet { get; set; }
        public string? ErrorHandel { get; set; }
    }

    public class KlasaWagowa
    {
        public int Klasa { get; set; }          // 4..12
        public int Liczba { get; set; }
        public decimal SumaKg { get; set; }
        public string Etykieta => Klasa <= 7 ? "Duży" : "Mały";
    }

    public class HodowcaDzis
    {
        public string Id { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string Skrot { get; set; } = "";
        public int LiczbaPartii { get; set; }
        public decimal SumaKg { get; set; }
        public string DisplayName => !string.IsNullOrEmpty(Skrot) ? Skrot : Nazwa;
    }
}
