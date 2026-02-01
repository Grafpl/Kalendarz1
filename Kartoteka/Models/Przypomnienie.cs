using System;

namespace Kalendarz1.Kartoteka.Models
{
    public class Przypomnienie
    {
        public int Id { get; set; }

        public int? KlientId { get; set; }
        public int? FakturaId { get; set; }
        public int? KontaktId { get; set; }

        public string Typ { get; set; }
        public string Tytul { get; set; }
        public string Opis { get; set; }
        public int Priorytet { get; set; } = 3;  // 1=Krytyczny, 2=Wysoki, 3=Normalny

        public DateTime DataPrzypomnienia { get; set; }
        public DateTime? DataWygasniecia { get; set; }

        public string Status { get; set; } = "Aktywne";

        public string PrzypisaneDo { get; set; }

        public bool CzyPowtarzalne { get; set; }
        public int? InterwalDni { get; set; }

        public DateTime DataUtworzenia { get; set; }
        public string UtworzonyPrzez { get; set; }
        public DateTime? DataModyfikacji { get; set; }

        // Pomocnicze
        public string NazwaKlienta { get; set; }

        public string PriorytetIkona => Priorytet switch
        {
            1 => "🔴",
            2 => "🟠",
            3 => "🔵",
            _ => "⚪"
        };

        public string PriorytetNazwa => Priorytet switch
        {
            1 => "Krytyczny",
            2 => "Wysoki",
            3 => "Normalny",
            _ => "Niski"
        };

        public string TypIkona => Typ switch
        {
            "KONTAKT_CYKLICZNY" => "📞",
            "PLATNOSC_TERMIN" => "💳",
            "PLATNOSC_PRZETERMINOWANA" => "⚠️",
            "NIEAKTYWNY_KLIENT" => "😴",
            "LIMIT_ALERT" => "📊",
            "URODZINY" => "🎂",
            "ROCZNICA" => "🎉",
            "CUSTOM" => "📌",
            _ => "🔔"
        };

        public bool JestPrzeterminowane => DataPrzypomnienia < DateTime.Now && Status == "Aktywne";
    }
}
