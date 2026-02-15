using System.Drawing;

namespace Kalendarz1.Transport.Theme
{
    /// <summary>
    /// Centralna paleta kolorów dla modułu transportu.
    /// Wszystkie kolory zgodne ze specyfikacją ZPSP Design System.
    /// </summary>
    public static class ZpspColors
    {
        // ═══════════════════════════════════════════
        //  CIEMNY PANEL (lewy)
        // ═══════════════════════════════════════════
        public static readonly Color PanelDark       = Color.FromArgb(43, 45, 66);       // #2B2D42
        public static readonly Color PanelDarkAlt    = Color.FromArgb(50, 52, 80);       // #323450
        public static readonly Color PanelDarkBorder = Color.FromArgb(61, 63, 92);       // #3D3F5C
        public static readonly Color PanelDarkHover  = Color.FromArgb(58, 60, 88);       // #3A3C58

        // ═══════════════════════════════════════════
        //  JASNY PANEL (prawy)
        // ═══════════════════════════════════════════
        public static readonly Color PanelLight       = Color.White;                      // #FFFFFF
        public static readonly Color PanelLightAlt    = Color.FromArgb(248, 249, 252);   // #F8F9FC
        public static readonly Color PanelLightBorder = Color.FromArgb(226, 229, 239);   // #E2E5EF
        public static readonly Color PanelLightHover  = Color.FromArgb(240, 242, 250);   // #F0F2FA

        // ═══════════════════════════════════════════
        //  ZIELONE (primary)
        // ═══════════════════════════════════════════
        public static readonly Color Green     = Color.FromArgb(67, 160, 71);            // #43A047
        public static readonly Color GreenDark = Color.FromArgb(46, 125, 50);            // #2E7D32
        public static readonly Color GreenBg   = Color.FromArgb(232, 245, 233);          // #E8F5E9
        public static readonly Color GreenBg2  = Color.FromArgb(200, 230, 201);          // #C8E6C9

        // ═══════════════════════════════════════════
        //  FIOLETOWE (selekcja, godzina konca)
        // ═══════════════════════════════════════════
        public static readonly Color Purple    = Color.FromArgb(123, 31, 162);           // #7B1FA2
        public static readonly Color PurpleRow = Color.FromArgb(232, 213, 245);          // #E8D5F5
        public static readonly Color PurpleBg  = Color.FromArgb(243, 229, 245);          // #F3E5F5
        public static readonly Color PurpleBg2 = Color.FromArgb(225, 190, 231);          // #E1BEE7

        // ═══════════════════════════════════════════
        //  POZOSTALE AKCENTY
        // ═══════════════════════════════════════════
        public static readonly Color Orange   = Color.FromArgb(245, 124, 0);             // #F57C00
        public static readonly Color OrangeBg = Color.FromArgb(255, 243, 224);           // #FFF3E0
        public static readonly Color Red      = Color.FromArgb(229, 57, 53);             // #E53935
        public static readonly Color RedDark  = Color.FromArgb(198, 40, 40);             // #C62828
        public static readonly Color RedBg    = Color.FromArgb(255, 235, 238);           // #FFEBEE
        public static readonly Color Blue     = Color.FromArgb(30, 136, 229);            // #1E88E5
        public static readonly Color BlueBg   = Color.FromArgb(227, 242, 253);           // #E3F2FD
        public static readonly Color Cyan     = Color.FromArgb(0, 172, 193);             // #00ACC1

        // ═══════════════════════════════════════════
        //  TEKST NA CIEMNYM TLE
        // ═══════════════════════════════════════════
        public static readonly Color TextWhite = Color.White;
        public static readonly Color TextLight = Color.FromArgb(200, 202, 216);          // #C8CAD8
        public static readonly Color TextMuted = Color.FromArgb(142, 144, 166);          // #8E90A6

        // ═══════════════════════════════════════════
        //  TEKST NA JASNYM TLE
        // ═══════════════════════════════════════════
        public static readonly Color TextDark   = Color.FromArgb(26, 28, 46);            // #1A1C2E
        public static readonly Color TextMedium = Color.FromArgb(85, 87, 112);           // #555770
        public static readonly Color TextGray   = Color.FromArgb(142, 144, 166);         // #8E90A6
        public static readonly Color TextFaint  = Color.FromArgb(176, 179, 197);         // #B0B3C5

        // ═══════════════════════════════════════════
        //  POMOCNICZE
        // ═══════════════════════════════════════════

        /// <summary>Kolor z alpha (przezroczystość).</summary>
        public static Color WithAlpha(Color color, int alpha) =>
            Color.FromArgb(alpha, color.R, color.G, color.B);

        /// <summary>Purple z 33% alpha na selekcję DGV.</summary>
        public static readonly Color PurpleSelection = Color.FromArgb(84, 123, 31, 162);

        /// <summary>Green z 44% alpha na boxShadow przycisku Zapisz.</summary>
        public static readonly Color GreenShadow = Color.FromArgb(112, 67, 160, 71);

        /// <summary>Kolor tła paska postępu (szary).</summary>
        public static readonly Color ProgressBg = Color.FromArgb(224, 224, 224);         // #E0E0E0

        /// <summary>Kolor dla ostrzeżeń (OrangeLight) w zakresie 50-80%.</summary>
        public static readonly Color OrangeLight = Color.FromArgb(255, 167, 38);         // #FFA726
    }
}
