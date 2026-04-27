using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace Kalendarz1
{
    // Stałe stylowe (kolory, fonty) i cache obiektów GDI+ używanych w SprawdzalkaUmow.
    // Cache: jeden Font/Brush per stała - alokowane raz w static ctor, używane przez całe życie aplikacji.
    // GDI+ obiekty są thread-safe do odczytu, więc OK dla DataGridView paint events.
    internal static class SprawdzalkaUmowStyles
    {
        // === KOLORY ===
        public static readonly Color Primary = Color.FromArgb(92, 138, 58);
        public static readonly Color PrimaryHover = Color.FromArgb(75, 115, 47);
        public static readonly Color RowComplete = Color.FromArgb(46, 204, 113);
        public static readonly Color RowAlternate = Color.FromArgb(245, 247, 249);
        public static readonly Color TextDark = Color.FromArgb(44, 62, 80);
        public static readonly Color TextMuted = Color.FromArgb(80, 80, 80);
        public static readonly Color TextSubtle = Color.FromArgb(127, 140, 141);
        public static readonly Color BorderLine = Color.FromArgb(220, 224, 228);

        // Header daty (slate gradient)
        public static readonly Color HeaderBgStart = Color.FromArgb(55, 71, 79);
        public static readonly Color HeaderBgEnd = Color.FromArgb(84, 110, 122);
        public static readonly Color HeaderAccent = Color.FromArgb(255, 152, 0);   // bursztyn

        // Pośrednik / zamrożone checkboxy
        public static readonly Color FrozenCheckBg = Color.FromArgb(200, 230, 201);
        public static readonly Color FrozenCheckFg = Color.FromArgb(189, 195, 199);

        // Highlight dla dzisiejszej daty (banner + wiersze danych)
        public static readonly Color TodayBgStart = Color.FromArgb(255, 235, 59);   // żółty material 500
        public static readonly Color TodayBgEnd = Color.FromArgb(255, 193, 7);     // amber 500
        public static readonly Color TodayRowBg = Color.FromArgb(255, 249, 196);   // żółty material 100 (jasny)
        public static readonly Color TodayBorder = Color.FromArgb(229, 57, 53);    // red 600

        // Quick-filter chips
        public static readonly Color ChipInactiveBg = Color.FromArgb(236, 240, 241);
        public static readonly Color ChipInactiveFg = Color.FromArgb(80, 80, 80);
        public static readonly Color ChipActiveBg = Primary;
        public static readonly Color ChipActiveFg = Color.White;

        // === FONTY (cache - dispose w DisposeAll) ===
        public static readonly Font HeaderBannerFont = new Font("Segoe UI", 11.5F, FontStyle.Bold);
        public static readonly Font CellBoldFont = new Font("Segoe UI", 9F, FontStyle.Bold);
        public static readonly Font CellRegularFont = new Font("Segoe UI", 9F, FontStyle.Regular);
        public static readonly Font ToolbarBoldFont = new Font("Segoe UI", 9F, FontStyle.Bold);
        public static readonly Font ButtonFont = new Font("Segoe UI", 11F, FontStyle.Bold);
        public static readonly Font SearchFont = new Font("Segoe UI", 11F, FontStyle.Regular);
        public static readonly Font ChipFont = new Font("Segoe UI", 9F, FontStyle.Bold);
        public static readonly Font StatsFont = new Font("Segoe UI", 9F, FontStyle.Regular);

        // === BRUSHE / PENS (cache) ===
        public static readonly SolidBrush HeaderAccentBrush = new SolidBrush(HeaderAccent);
        public static readonly SolidBrush HeaderTextBrush = new SolidBrush(Color.White);
        public static readonly SolidBrush TodayHeaderTextBrush = new SolidBrush(Color.FromArgb(33, 33, 33));
        public static readonly Pen BorderPen = new Pen(BorderLine, 1);
        public static readonly Pen TodayBorderPen = new Pen(TodayBorder, 2);

        // StringFormat dla banera daty (lewy align, vertical center)
        public static readonly StringFormat HeaderBannerFormat = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };

        // === KULTURA (data + dzień tygodnia) ===
        public static readonly CultureInfo PlCulture = new CultureInfo("pl-PL");

        // === ROZMIARY ===
        public const int HeaderRowHeight = 32;
        public const int DataRowHeight = 36;
        public const int AvatarSize = 24;

        // Gradient brush dla nagłówka daty - tworzony per RowBounds (zależy od size),
        // więc nie da się go zcachować jako static. Helper w jednym miejscu.
        public static LinearGradientBrush CreateHeaderBgBrush(Rectangle rowBounds)
            => new LinearGradientBrush(rowBounds, HeaderBgStart, HeaderBgEnd, LinearGradientMode.Horizontal);

        // Gradient żółty dla bannera "DZIŚ"
        public static LinearGradientBrush CreateTodayHeaderBgBrush(Rectangle rowBounds)
            => new LinearGradientBrush(rowBounds, TodayBgStart, TodayBgEnd, LinearGradientMode.Horizontal);

        // Wywoływane raz przy zamknięciu aplikacji (lub nigdy - process exit zwolni resources).
        // Zachowane dla porządku gdyby ktoś chciał re-create stylów.
        public static void DisposeAll()
        {
            HeaderBannerFont.Dispose();
            CellBoldFont.Dispose();
            CellRegularFont.Dispose();
            ToolbarBoldFont.Dispose();
            ButtonFont.Dispose();
            SearchFont.Dispose();
            ChipFont.Dispose();
            StatsFont.Dispose();
            HeaderAccentBrush.Dispose();
            HeaderTextBrush.Dispose();
            TodayHeaderTextBrush.Dispose();
            BorderPen.Dispose();
            TodayBorderPen.Dispose();
            HeaderBannerFormat.Dispose();
        }
    }
}
