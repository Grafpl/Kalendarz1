using System;

namespace Kalendarz1.Kartoteka.Models
{
    public class NotatkaPozycja
    {
        public int Id { get; set; }
        public int IdSymfonia { get; set; }
        public string Tresc { get; set; }
        public string Autor { get; set; }
        public DateTime DataUtworzenia { get; set; }
    }
}
