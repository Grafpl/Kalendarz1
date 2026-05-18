using System.Windows.Media;

namespace Kalendarz1.Kartoteka.Resources
{
    // Cache statycznych, zamrożonych (Freeze) brushów używanych w KartotekaOdbiorcowWindow.
    // Eliminuje ~115 alokacji SolidColorBrush per generowanie listy kart (przy 500 klientach
    // to ~57500 niepotrzebnych alokacji). Frozen brushe są też shareable w WPF → szybsze renderowanie.
    //
    // Nazewnictwo: Tailwind CSS palette (skala -50…-900) — odpowiada palecie używanej w XAML zaplecza.
    internal static class KartotekaBrushes
    {
        private static SolidColorBrush B(byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
        }

        // ── Gray (slate / cool)
        public static readonly SolidColorBrush Gray50  = B(249, 250, 251);
        public static readonly SolidColorBrush Gray100 = B(243, 244, 246);
        public static readonly SolidColorBrush Gray200 = B(229, 231, 235);
        public static readonly SolidColorBrush Gray300 = B(209, 213, 219);
        public static readonly SolidColorBrush Gray400 = B(156, 163, 175);
        public static readonly SolidColorBrush Gray500 = B(107, 114, 128);
        public static readonly SolidColorBrush Gray700 = B(55, 65, 81);

        // ── Red
        public static readonly SolidColorBrush Red50  = B(254, 242, 242);
        public static readonly SolidColorBrush Red100 = B(254, 226, 226);
        public static readonly SolidColorBrush Red200 = B(254, 202, 202);
        public static readonly SolidColorBrush Red600 = B(220, 38, 38);
        public static readonly SolidColorBrush Red900 = B(153, 27, 27);

        // ── Green
        public static readonly SolidColorBrush Green50  = B(240, 253, 244);
        public static readonly SolidColorBrush Green100 = B(220, 252, 231);
        public static readonly SolidColorBrush Green200 = B(187, 247, 208);
        public static readonly SolidColorBrush Green300 = B(134, 239, 172);
        public static readonly SolidColorBrush Green500 = B(34, 197, 94);
        public static readonly SolidColorBrush Green600 = B(22, 163, 74);
        public static readonly SolidColorBrush Green800 = B(22, 101, 52);

        // ── Blue
        public static readonly SolidColorBrush Blue50  = B(239, 246, 255);
        public static readonly SolidColorBrush Blue100 = B(219, 234, 254);
        public static readonly SolidColorBrush Blue200 = B(191, 219, 254);
        public static readonly SolidColorBrush Blue500 = B(59, 130, 246);
        public static readonly SolidColorBrush Blue600 = B(37, 99, 235);
        public static readonly SolidColorBrush Blue800 = B(30, 64, 175);

        // ── Yellow / Amber
        public static readonly SolidColorBrush Yellow100 = B(254, 249, 195);
        public static readonly SolidColorBrush Yellow500 = B(234, 179, 8);
        public static readonly SolidColorBrush Amber500 = B(245, 158, 11);
        public static readonly SolidColorBrush Amber700 = B(180, 83, 9);

        // ── Orange / Violet / Purple
        public static readonly SolidColorBrush Orange600 = B(234, 88, 12);
        public static readonly SolidColorBrush Violet600 = B(124, 58, 237);
        public static readonly SolidColorBrush Purple500 = B(168, 85, 247);
    }
}
