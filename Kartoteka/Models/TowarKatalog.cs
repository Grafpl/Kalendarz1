namespace Kalendarz1.Kartoteka.Models
{
    public class TowarKatalog
    {
        public int Id { get; set; }
        public string Kod { get; set; }
        public string Nazwa { get; set; }
        public string Katalog { get; set; }

        public override string ToString() => $"{Kod} - {Nazwa} ({Katalog})";
    }
}
