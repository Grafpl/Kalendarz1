using System.Drawing;

namespace Kalendarz1.Transport.Theme
{
    /// <summary>
    /// Centralne definicje fontów dla modułu transportu.
    /// Wszystkie fonty Segoe UI, zgodne ze specyfikacją.
    /// </summary>
    public static class ZpspFonts
    {
        // ═══════════════════════════════════════════
        //  NAGŁÓWKI
        // ═══════════════════════════════════════════
        public static readonly Font Header16Bold   = new Font("Segoe UI", 16F, FontStyle.Bold);
        public static readonly Font Header14Bold   = new Font("Segoe UI", 14F, FontStyle.Bold);
        public static readonly Font Header13Bold   = new Font("Segoe UI", 13F, FontStyle.Bold);
        public static readonly Font Header12Bold   = new Font("Segoe UI", 12F, FontStyle.Bold);
        public static readonly Font Header11Bold   = new Font("Segoe UI", 11F, FontStyle.Bold);

        // ═══════════════════════════════════════════
        //  ETYKIETY (LABELS)
        // ═══════════════════════════════════════════
        public static readonly Font Label8Bold     = new Font("Segoe UI", 8F, FontStyle.Bold);
        public static readonly Font Label85Bold    = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        public static readonly Font Label9Bold     = new Font("Segoe UI", 9F, FontStyle.Bold);
        public static readonly Font Label10Bold    = new Font("Segoe UI", 10F, FontStyle.Bold);

        // ═══════════════════════════════════════════
        //  TEKST ZWYKŁY
        // ═══════════════════════════════════════════
        public static readonly Font Text10         = new Font("Segoe UI", 10F);
        public static readonly Font Text9          = new Font("Segoe UI", 9F);
        public static readonly Font Text8          = new Font("Segoe UI", 8F);
        public static readonly Font Text7          = new Font("Segoe UI", 7F);
        public static readonly Font Text7Bold      = new Font("Segoe UI", 7F, FontStyle.Bold);

        // ═══════════════════════════════════════════
        //  SPECJALNE
        // ═══════════════════════════════════════════

        /// <summary>ComboBox kierowcy — duży, Bold, biały na zielonym.</summary>
        public static readonly Font ComboDriver    = new Font("Segoe UI", 11F, FontStyle.Bold);

        /// <summary>Godzina w pill — duży Bold.</summary>
        public static readonly Font TimePill       = new Font("Segoe UI", 13F, FontStyle.Bold);

        /// <summary>Procent wypełnienia.</summary>
        public static readonly Font CapacityPercent = new Font("Segoe UI", 16F, FontStyle.Bold);

        /// <summary>Pill trasy.</summary>
        public static readonly Font RoutePill      = new Font("Segoe UI", 9F, FontStyle.Bold);

        /// <summary>Nagłówek DGV.</summary>
        public static readonly Font DgvHeader      = new Font("Segoe UI", 8.5F, FontStyle.Bold);

        /// <summary>Komórka DGV — klient (Bold).</summary>
        public static readonly Font DgvClientBold  = new Font("Segoe UI", 10F, FontStyle.Bold);

        /// <summary>Komórka DGV — palety (Bold).</summary>
        public static readonly Font DgvPaletyBold  = new Font("Segoe UI", 11F, FontStyle.Bold);

        /// <summary>Komórka DGV — Lp (Bold, duży).</summary>
        public static readonly Font DgvLpBold      = new Font("Segoe UI", 14F, FontStyle.Bold);
    }
}
