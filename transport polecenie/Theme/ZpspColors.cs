// ═══════════════════════════════════════════════════════════════════════
// ZpspColors.cs — Paleta kolorów ZPSP
// ═══════════════════════════════════════════════════════════════════════
// Wszystkie kolory wyciągnięte z oryginalnego screenshota aplikacji.
// Użycie: ZpspColors.Green, ZpspColors.PanelDark, itp.
// W WinForms ustaw: control.BackColor = ZpspColors.PanelDark;
// ═══════════════════════════════════════════════════════════════════════

using System.Drawing;

namespace ZpspTransport.Theme
{
    /// <summary>
    /// Centralna definicja kolorów ZPSP.
    /// Zmiana koloru tutaj zmienia go w całej aplikacji.
    /// </summary>
    public static class ZpspColors
    {
        // ─────────────────────────────────────────────
        // CIEMNY PANEL (lewy — kurs, ładunki)
        // ─────────────────────────────────────────────
        
        /// <summary>Główne tło ciemnego panelu — ciemny granat/charcoal</summary>
        public static readonly Color PanelDark = Color.FromArgb(43, 45, 66);        // #2B2D42
        
        /// <summary>Tło inputów, comboboxów na ciemnym panelu</summary>
        public static readonly Color PanelDarkAlt = Color.FromArgb(50, 52, 80);      // #323450
        
        /// <summary>Obramowania na ciemnym panelu</summary>
        public static readonly Color PanelDarkBorder = Color.FromArgb(61, 63, 92);   // #3D3F5C
        
        /// <summary>Hover na ciemnym panelu</summary>
        public static readonly Color PanelDarkHover = Color.FromArgb(58, 60, 88);    // #3A3C58

        // ─────────────────────────────────────────────
        // JASNY PANEL (prawy — zamówienia)
        // ─────────────────────────────────────────────
        
        /// <summary>Białe tło jasnego panelu</summary>
        public static readonly Color PanelLight = Color.White;                        // #FFFFFF
        
        /// <summary>Zebra striping — co drugi wiersz</summary>
        public static readonly Color PanelLightAlt = Color.FromArgb(248, 249, 252);  // #F8F9FC
        
        /// <summary>Obramowania na jasnym panelu</summary>
        public static readonly Color PanelLightBorder = Color.FromArgb(226, 229, 239); // #E2E5EF
        
        /// <summary>Hover na jasnym panelu</summary>
        public static readonly Color PanelLightHover = Color.FromArgb(240, 242, 250); // #F0F2FA

        // ─────────────────────────────────────────────
        // ZIELONE AKCENTY (primary — przyciski, nagłówki)
        // ─────────────────────────────────────────────
        
        /// <summary>Główny zielony — combobox kierowcy, przycisk Zapisz, nagłówek zamówień</summary>
        public static readonly Color Green = Color.FromArgb(67, 160, 71);            // #43A047
        
        /// <summary>Jasny zielony — hover, godzina startu</summary>
        public static readonly Color GreenLight = Color.FromArgb(102, 187, 106);     // #66BB6A
        
        /// <summary>Ciemny zielony — gradient przycisku Zapisz</summary>
        public static readonly Color GreenDark = Color.FromArgb(46, 125, 50);        // #2E7D32
        
        /// <summary>Zielone tło — sukces, grupowanie dat</summary>
        public static readonly Color GreenBg = Color.FromArgb(232, 245, 233);        // #E8F5E9
        
        /// <summary>Zielone tło intensywne</summary>
        public static readonly Color GreenBg2 = Color.FromArgb(200, 230, 201);       // #C8E6C9

        // ─────────────────────────────────────────────
        // FIOLETOWE AKCENTY (selekcja, godziny, sortowanie)
        // ─────────────────────────────────────────────
        
        /// <summary>Fiolet — zaznaczony wiersz border, przycisk Sortuj, godzina końca</summary>
        public static readonly Color Purple = Color.FromArgb(123, 31, 162);          // #7B1FA2
        
        /// <summary>Jasny fiolet</summary>
        public static readonly Color PurpleLight = Color.FromArgb(156, 77, 204);     // #9C4DCC
        
        /// <summary>Tło zaznaczonego wiersza na jasnym panelu</summary>
        public static readonly Color PurpleRow = Color.FromArgb(232, 213, 245);      // #E8D5F5
        
        /// <summary>Tło pill godziny</summary>
        public static readonly Color PurpleBg = Color.FromArgb(243, 229, 245);       // #F3E5F5
        
        /// <summary>Border pill godziny</summary>
        public static readonly Color PurpleBg2 = Color.FromArgb(225, 190, 231);      // #E1BEE7

        // ─────────────────────────────────────────────
        // POMARAŃCZOWE (palety, ostrzeżenia)
        // ─────────────────────────────────────────────
        
        /// <summary>Pomarańczowy — wartości palet, ostrzeżenia</summary>
        public static readonly Color Orange = Color.FromArgb(245, 124, 0);           // #F57C00
        
        /// <summary>Jasny pomarańczowy</summary>
        public static readonly Color OrangeLight = Color.FromArgb(255, 152, 0);      // #FF9800
        
        /// <summary>Tło pomarańczowe — ostrzeżenia, grupa dat wtorkowych</summary>
        public static readonly Color OrangeBg = Color.FromArgb(255, 243, 224);       // #FFF3E0

        // ─────────────────────────────────────────────
        // CZERWONE (alarmy, przeładowanie)
        // ─────────────────────────────────────────────
        
        /// <summary>Czerwony — przeładowanie, błędy, priorytet wysoki</summary>
        public static readonly Color Red = Color.FromArgb(229, 57, 53);              // #E53935
        
        /// <summary>Ciemny czerwony — hatching na capacity bar</summary>
        public static readonly Color RedDark = Color.FromArgb(198, 40, 40);          // #C62828
        
        /// <summary>Tło czerwone — alerty błędów</summary>
        public static readonly Color RedBg = Color.FromArgb(255, 235, 238);          // #FFEBEE

        // ─────────────────────────────────────────────
        // NIEBIESKI (info, strzałki kolejności)
        // ─────────────────────────────────────────────
        
        /// <summary>Niebieski — przyciski ▲▼, info, dodaj</summary>
        public static readonly Color Blue = Color.FromArgb(30, 136, 229);            // #1E88E5
        
        /// <summary>Tło niebieskie — alerty info</summary>
        public static readonly Color BlueBg = Color.FromArgb(227, 242, 253);         // #E3F2FD

        // ─────────────────────────────────────────────
        // CYAN (trasa, split)
        // ─────────────────────────────────────────────
        
        /// <summary>Cyan — trasa, split zamówień</summary>
        public static readonly Color Cyan = Color.FromArgb(0, 172, 193);             // #00ACC1
        
        /// <summary>Tło cyan</summary>
        public static readonly Color CyanBg = Color.FromArgb(224, 247, 250);         // #E0F7FA

        // ─────────────────────────────────────────────
        // TEKST NA CIEMNYM TLE
        // ─────────────────────────────────────────────
        
        /// <summary>Biały tekst — nagłówki, nazwy klientów na ciemnym tle</summary>
        public static readonly Color TextWhite = Color.White;
        
        /// <summary>Jasny tekst — wartości, tekst ogólny na ciemnym tle</summary>
        public static readonly Color TextLight = Color.FromArgb(200, 202, 216);      // #C8CAD8
        
        /// <summary>Przytłumiony tekst — labele, etykiety na ciemnym tle</summary>
        public static readonly Color TextMuted = Color.FromArgb(142, 144, 166);      // #8E90A6

        // ─────────────────────────────────────────────
        // TEKST NA JASNYM TLE
        // ─────────────────────────────────────────────
        
        /// <summary>Ciemny tekst — nazwy klientów na jasnym tle</summary>
        public static readonly Color TextDark = Color.FromArgb(26, 28, 46);          // #1A1C2E
        
        /// <summary>Średni tekst — tekst ogólny na jasnym tle</summary>
        public static readonly Color TextMedium = Color.FromArgb(85, 87, 112);       // #555770
        
        /// <summary>Szary tekst — adresy, daty na jasnym tle</summary>
        public static readonly Color TextGray = Color.FromArgb(142, 144, 166);       // #8E90A6
        
        /// <summary>Najbledszy tekst — timestampy, disabled</summary>
        public static readonly Color TextFaint = Color.FromArgb(176, 179, 197);      // #B0B3C5

        // ─────────────────────────────────────────────
        // CAPACITY BAR
        // ─────────────────────────────────────────────
        
        /// <summary>Tło paska ładowności (szare)</summary>
        public static readonly Color CapacityBg = Color.FromArgb(224, 224, 224);     // #E0E0E0
        
        /// <summary>Żółty na capacity bar (50-80%)</summary>
        public static readonly Color Yellow = Color.FromArgb(253, 216, 53);          // #FDD835
    }
}
