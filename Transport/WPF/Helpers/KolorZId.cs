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
        private static readonly Color[] Paleta =
        {
            Color.FromRgb(0x2E, 0x7D, 0x32),  // zielony
            Color.FromRgb(0x19, 0x76, 0xD2),  // niebieski
            Color.FromRgb(0x9C, 0x27, 0xB0),  // fioletowy
            Color.FromRgb(0xE6, 0x51, 0x00),  // pomarańcz
            Color.FromRgb(0x00, 0x89, 0x7B),  // turkus
            Color.FromRgb(0xC2, 0x18, 0x5B),  // magenta
            Color.FromRgb(0x45, 0x5A, 0x64),  // szary
            Color.FromRgb(0x79, 0x55, 0x48),  // brąz
            Color.FromRgb(0x68, 0x9F, 0x38),  // limonkowy
            Color.FromRgb(0x00, 0x83, 0x8F),  // morski
            Color.FromRgb(0xAD, 0x14, 0x57),  // wino
            Color.FromRgb(0x51, 0x2D, 0xA8),  // indygo
            Color.FromRgb(0xFB, 0xC0, 0x2D),  // żółty
            Color.FromRgb(0xD3, 0x2F, 0x2F),  // czerwony
            Color.FromRgb(0x53, 0x6D, 0xFE),  // niebieski jasny
            Color.FromRgb(0x37, 0x47, 0x4F),  // grafit
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
