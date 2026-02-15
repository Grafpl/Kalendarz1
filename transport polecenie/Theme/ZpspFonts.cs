// ═══════════════════════════════════════════════════════════════════════
// ZpspFonts.cs — Definicje fontów ZPSP
// ═══════════════════════════════════════════════════════════════════════

using System.Drawing;

namespace ZpspTransport.Theme
{
    /// <summary>
    /// Centralne fonty aplikacji. Segoe UI = domyślny font Windows 10/11.
    /// Jeśli chcesz inny font (np. Inter, JetBrains Mono), zmień tutaj.
    /// </summary>
    public static class ZpspFonts
    {
        private const string FAMILY = "Segoe UI";

        /// <summary>Nagłówek sekcji: "ŁADUNKI W KURSIE", "ZAMÓWIENIA"</summary>
        public static readonly Font SectionTitle = new(FAMILY, 12f, FontStyle.Bold);
        
        /// <summary>Label nad polem: "KIEROWCA", "POJAZD", "DATA"</summary>
        public static readonly Font FieldLabel = new(FAMILY, 8f, FontStyle.Bold);
        
        /// <summary>Wartość w combobox / polu tekstowym</summary>
        public static readonly Font FieldValue = new(FAMILY, 11f, FontStyle.Bold);
        
        /// <summary>Nagłówek tabeli: "Lp.", "Klient", "Palety"</summary>
        public static readonly Font TableHeader = new(FAMILY, 8.5f, FontStyle.Bold);
        
        /// <summary>Komórka tabeli — tekst normalny</summary>
        public static readonly Font TableCell = new(FAMILY, 10f, FontStyle.Regular);
        
        /// <summary>Komórka tabeli — tekst pogrubiony (klient, palety)</summary>
        public static readonly Font TableCellBold = new(FAMILY, 10f, FontStyle.Bold);
        
        /// <summary>Komórka tabeli — wartość liczbowa duża (np. palety w wierszu)</summary>
        public static readonly Font TableCellNumber = new(FAMILY, 11f, FontStyle.Bold);
        
        /// <summary>Numer Lp. w tabeli ładunków</summary>
        public static readonly Font StopNumber = new(FAMILY, 14f, FontStyle.Bold);
        
        /// <summary>Pill (godzina, status, badge)</summary>
        public static readonly Font Pill = new(FAMILY, 9f, FontStyle.Bold);
        
        /// <summary>Capacity bar — wartość procentowa</summary>
        public static readonly Font CapacityPercent = new(FAMILY, 16f, FontStyle.Bold);
        
        /// <summary>Capacity bar — opis "21.4 / 4 max"</summary>
        public static readonly Font CapacityLabel = new(FAMILY, 9f, FontStyle.Regular);
        
        /// <summary>Przycisk duży: "✓ ZAPISZ KURS"</summary>
        public static readonly Font ButtonLarge = new(FAMILY, 13f, FontStyle.Bold);
        
        /// <summary>Przycisk mały: "ANULUJ", "Sortuj"</summary>
        public static readonly Font ButtonSmall = new(FAMILY, 10f, FontStyle.Bold);
        
        /// <summary>Tekst w wierszu podsumowania</summary>
        public static readonly Font Summary = new(FAMILY, 9.5f, FontStyle.Regular);
        
        /// <summary>Timestamp, kto utworzył</summary>
        public static readonly Font Timestamp = new(FAMILY, 8.5f, FontStyle.Regular);
        
        /// <summary>Tekst grupy dat: "► 16.02 poniedziałek"</summary>
        public static readonly Font GroupHeader = new(FAMILY, 10f, FontStyle.Bold);
        
        /// <summary>Alert w panelu konfliktów</summary>
        public static readonly Font AlertText = new(FAMILY, 10f, FontStyle.Regular);
        
        /// <summary>Alert bold</summary>
        public static readonly Font AlertBold = new(FAMILY, 10f, FontStyle.Bold);
    }
}
