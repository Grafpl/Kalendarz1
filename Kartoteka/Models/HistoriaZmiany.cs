using System;

namespace Kalendarz1.Kartoteka.Models
{
    public class HistoriaZmiany
    {
        public long Id { get; set; }

        public string TabelaNazwa { get; set; }
        public int RekordId { get; set; }
        public int? KlientId { get; set; }

        public string TypOperacji { get; set; }  // INSERT, UPDATE, DELETE
        public string PoleNazwa { get; set; }
        public string StaraWartosc { get; set; }
        public string NowaWartosc { get; set; }

        public string UzytkownikId { get; set; }
        public string UzytkownikNazwa { get; set; }
        public DateTime DataZmiany { get; set; }

        public string Komentarz { get; set; }
        public bool CzyCofniete { get; set; }
        public string CofnietePrzez { get; set; }
        public DateTime? DataCofniecia { get; set; }

        // Właściwości wyświetlania
        public string TypIkona => TypOperacji switch
        {
            "INSERT" => "➕",
            "UPDATE" => "✏️",
            "DELETE" => "🗑️",
            _ => "❓"
        };

        public string TypKolor => TypOperacji switch
        {
            "INSERT" => "#10B981",
            "UPDATE" => "#F59E0B",
            "DELETE" => "#EF4444",
            _ => "#6B7280"
        };

        public string StatusDisplay => CzyCofniete ? $"↩ Cofnięte przez {CofnietePrzez}" : "";
    }

    public class ZmianaPola
    {
        public string NazwaPola { get; set; }
        public string StaraWartosc { get; set; }
        public string NowaWartosc { get; set; }
    }
}
