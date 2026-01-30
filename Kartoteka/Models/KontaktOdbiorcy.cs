using System;

namespace Kalendarz1.Kartoteka.Models
{
    public class KontaktOdbiorcy
    {
        public int Id { get; set; }
        public int IdSymfonia { get; set; }
        public string TypKontaktu { get; set; }
        public string Imie { get; set; }
        public string Nazwisko { get; set; }
        public string Telefon { get; set; }
        public string Email { get; set; }
        public string Stanowisko { get; set; }
        public string Notatka { get; set; }
        public DateTime DataUtworzenia { get; set; }
        public DateTime? DataModyfikacji { get; set; }

        public string PelneNazwisko => $"{Imie} {Nazwisko}".Trim();

        public string IkonaTypu => TypKontaktu switch
        {
            "Główny" => "\U0001F464",
            "Księgowość" => "\U0001F4CA",
            "Opakowania" => "\U0001F4E6",
            "Właściciel" => "\U0001F454",
            "Magazyn" => "\U0001F3ED",
            _ => "\U0001F4CB"
        };
    }
}
