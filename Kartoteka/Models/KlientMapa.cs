using System;
using System.Linq;

namespace Kalendarz1.Kartoteka.Models
{
    public class KlientMapa
    {
        public int Id { get; set; }
        public string NazwaFirmy { get; set; }
        public string Skrot { get; set; }
        public string Miasto { get; set; }
        public string Ulica { get; set; }
        public string KodPocztowy { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Kategoria { get; set; }
        public decimal ObrotyMiesieczne { get; set; }
        public bool MaAlert { get; set; }
        public string Handlowiec { get; set; }
        public string NIP { get; set; }
        public bool IsActive { get; set; }

        public string PelnyAdres => string.Join(", ",
            new[] { Ulica, KodPocztowy, Miasto }.Where(s => !string.IsNullOrWhiteSpace(s)));

        public string MarkerKolor => Kategoria switch
        {
            "A" => "#10B981",
            "B" => "#3B82F6",
            "C" => "#F59E0B",
            "D" => "#6B7280",
            _ => "#EF4444"
        };

        public bool MaWspolrzedne => Latitude.HasValue && Longitude.HasValue;
    }
}
