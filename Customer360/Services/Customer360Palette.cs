using SkiaSharp;

namespace Kalendarz1.Customer360.Services
{
    /// <summary>Wspólna paleta kolorów wykresów Customer360 (LiveCharts2 / SkiaSharp).</summary>
    public static class Customer360Palette
    {
        // 8 kolorów — spójne dla obrotu i asortymentu
        public static readonly string[] Hex =
        {
            "#2563EB", // niebieski
            "#16A34A", // zielony
            "#F59E0B", // pomarańczowy
            "#7C3AED", // fiolet
            "#EC4899", // róż
            "#0891B2", // cyjan
            "#DC2626", // czerwony
            "#64748B"  // szary
        };

        public static SKColor Color(int i) => SKColor.Parse(Hex[((i % Hex.Length) + Hex.Length) % Hex.Length]);

        public static readonly SKColor Niebieski = SKColor.Parse("#2563EB");
        public static readonly SKColor Zielony = SKColor.Parse("#16A34A");
        public static readonly SKColor SzaryTekst = SKColor.Parse("#64748B");
        public static readonly SKColor SiatkaSlaba = SKColor.Parse("#EEF2F7");
    }
}
