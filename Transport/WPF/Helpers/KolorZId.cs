// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Helpers/KolorZId.cs — deterministyczny kolor z ID.
// 16-kolorowa paleta. Kierowca i pojazd dostają NIEZALEŻNE kolory (oba z hashu
// własnego ID) — gdy pojazd trafia do innego kierowcy, kierowca zachowuje
// swój kolor, pojazd zachowuje swój. AvatarControl ma własną 8-kolorową
// paletę dla awatarów; tu używamy szerszej, żeby flota była lepiej rozróżnialna.
// ════════════════════════════════════════════════════════════════════════════

using System.Windows.Media;

namespace Kalendarz1.Transport.WPF.Helpers
{
    public static class KolorZId
    {
        // Paleta dobrana tak, żeby każde sąsiednie hue było wyraźnie różne.
        // Usunięto bliskie pary (jasny/ciemny niebieski, dwa turkusy, dwa zielone),
        // żeby kropka kierowcy obok pojazdu była łatwa do rozróżnienia.
        private static readonly Color[] Paleta =
        {
            Color.FromRgb(0xD3, 0x2F, 0x2F),  // 0°   czerwony
            Color.FromRgb(0xE6, 0x51, 0x00),  // 20°  pomarańcz
            Color.FromRgb(0xF5, 0x7C, 0x00),  // 30°  bursztyn
            Color.FromRgb(0xFB, 0xC0, 0x2D),  // 50°  żółty
            Color.FromRgb(0x82, 0x77, 0x17),  // 60°  oliwka ciemna
            Color.FromRgb(0x68, 0x9F, 0x38),  // 90°  limonka
            Color.FromRgb(0x2E, 0x7D, 0x32),  // 120° zielony las
            Color.FromRgb(0x00, 0x89, 0x7B),  // 170° turkus
            Color.FromRgb(0x19, 0x76, 0xD2),  // 210° niebieski
            Color.FromRgb(0x40, 0x3F, 0x97),  // 235° indygo (mocny, ≠ niebieski)
            Color.FromRgb(0x9C, 0x27, 0xB0),  // 290° fiolet
            Color.FromRgb(0xC2, 0x18, 0x5B),  // 330° magenta
            Color.FromRgb(0xAD, 0x14, 0x57),  // 345° wino
            Color.FromRgb(0x79, 0x55, 0x48),  // brąz
            Color.FromRgb(0x45, 0x5A, 0x64),  // szaroniebieski
            Color.FromRgb(0x5D, 0x40, 0x37),  // kakao
        };

        public static Color DlaInt(int? id)
        {
            if (!id.HasValue) return Color.FromRgb(0xBD, 0xBD, 0xBD); // jasny szary = brak
            // mieszanka prostym hashem (Knuth multiplicative) — żeby kolejne ID nie wpadały w sąsiednie indeksy
            uint h = (uint)id.Value * 2654435761u;
            return Paleta[(int)(h % (uint)Paleta.Length)];
        }

        public static SolidColorBrush BrushDlaInt(int? id) => new(DlaInt(id));

        public static SolidColorBrush BrushDlaIntMiekki(int? id)
        {
            var c = DlaInt(id);
            return new SolidColorBrush(Color.FromArgb(0x28, c.R, c.G, c.B));
        }
    }
}
