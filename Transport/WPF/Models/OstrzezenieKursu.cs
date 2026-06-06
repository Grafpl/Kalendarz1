// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Models/OstrzezenieKursu.cs
// Model pojedynczego ostrzeżenia + waga (kolor + ikona).
// ════════════════════════════════════════════════════════════════════════════

namespace Kalendarz1.Transport.WPF.Models
{
    public enum WagaOstrzezenia
    {
        Info,        // niebieski — nie blokuje, dobre do wiedzy
        Ostrzezenie, // amber — uwaga, sprawdź
        Krytyczny    // czerwony — coś jest nie tak, popraw
    }

    public class OstrzezenieKursu
    {
        public WagaOstrzezenia Waga { get; set; }
        public string Ikona { get; set; } = "";
        public string Tytul { get; set; } = "";
        public string Opis { get; set; } = "";
    }
}
