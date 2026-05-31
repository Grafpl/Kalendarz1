using System.Windows.Media;

namespace Kalendarz1.DashboardPrzychodu.Theme
{
    /// <summary>
    /// Centralna paleta Frozen brushy Warm Industrial używanych w code-behind.
    /// Wszystkie statyczne — nie alokujemy nowych przy każdym refresh (30s timer).
    /// </summary>
    internal static class DashboardBrushes
    {
        // ===== TŁA =====
        public static readonly SolidColorBrush BgMain     = Make(28, 25, 23);    // #1c1917
        public static readonly SolidColorBrush BgPanel    = Make(41, 37, 36);    // #292524
        public static readonly SolidColorBrush BgDark     = Make(68, 64, 60);    // #44403c
        public static readonly SolidColorBrush BgDarker   = Make(87, 83, 78);    // #57534e
        public static readonly SolidColorBrush BgSection  = Make(55, 50, 48);    // #373230 (dialog sections)

        // ===== TEKST =====
        public static readonly SolidColorBrush TextPrimary   = Make(231, 229, 228); // #e7e5e4
        public static readonly SolidColorBrush TextSecondary = Make(168, 162, 158); // #a8a29e
        public static readonly SolidColorBrush TextMuted     = Make(120, 113, 108); // #78716c

        // ===== AKCENTY =====
        public static readonly SolidColorBrush Amber       = Make(245, 158, 11);  // #f59e0b
        public static readonly SolidColorBrush AmberLight  = Make(251, 191, 36);  // #fbbf24
        public static readonly SolidColorBrush Green       = Make(34, 197, 94);   // #22c55e
        public static readonly SolidColorBrush Red         = Make(239, 68, 68);   // #ef4444
        public static readonly SolidColorBrush RedDark     = Make(127, 29, 29);   // #7f1d1d
        public static readonly SolidColorBrush RedSoft     = Make(252, 165, 165); // #fca5a5 (animacja pulsująca)
        public static readonly SolidColorBrush Blue        = Make(96, 165, 250);  // #60a5fa
        public static readonly SolidColorBrush Purple      = Make(192, 132, 252); // #c084fc

        // ===== DODATKOWE =====
        public static readonly SolidColorBrush BorderDefault   = Make(68, 64, 60);   // alias BgDark
        public static readonly SolidColorBrush PaceAhead       = Make(34, 197, 94);  // alias Green (wyprzedzasz)
        public static readonly SolidColorBrush PaceBehind      = Make(239, 68, 68);  // alias Red (opóźnienie)
        public static readonly SolidColorBrush PaceOnTrack     = Make(168, 162, 158);// alias TextSecondary

        // ===== PALETA HODOWCÓW (16 kolorów, deterministycznie wybierane przez hash nazwy) =====
        public static readonly SolidColorBrush[] HodowcaPalette = new[]
        {
            Make(239, 68, 68),    // CZERWONY
            Make(250, 204, 21),   // ŻÓŁTY
            Make(34, 197, 94),    // ZIELONY
            Make(59, 130, 246),   // NIEBIESKI
            Make(249, 115, 22),   // POMARAŃCZOWY
            Make(168, 85, 247),   // FIOLETOWY
            Make(6, 182, 212),    // CYAN
            Make(236, 72, 153),   // RÓŻOWY
            Make(132, 204, 22),   // LIMONKOWY
            Make(244, 63, 94),    // MALINOWY
            Make(20, 184, 166),   // MORSKI
            Make(245, 158, 11),   // BURSZTYNOWY
            Make(99, 102, 241),   // INDYGO
            Make(16, 185, 129),   // SZMARAGD
            Make(217, 70, 239),   // MAGENTA
            Make(251, 191, 36),   // ZŁOTY
        };

        private static SolidColorBrush Make(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Deterministyczny FNV-1a hash dla string (niezależny od randomizacji string.GetHashCode w .NET Core).
        /// Gwarantuje że "Jan Kowalski" zawsze trafi w ten sam kolor w palecie, między procesami i sesjami.
        /// </summary>
        public static int DeterministicHash(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            const int fnvOffsetBasis = unchecked((int)2166136261);
            const int fnvPrime = 16777619;
            int hash = fnvOffsetBasis;
            foreach (var c in s)
            {
                hash ^= char.ToUpperInvariant(c);
                hash = unchecked(hash * fnvPrime);
            }
            return hash;
        }

        /// <summary>
        /// Zwraca deterministyczny brush hodowcy z palety na bazie hashu nazwy.
        /// </summary>
        public static SolidColorBrush BrushForHodowca(string name)
        {
            int idx = (DeterministicHash(name) & 0x7FFFFFFF) % HodowcaPalette.Length;
            return HodowcaPalette[idx];
        }
    }
}
