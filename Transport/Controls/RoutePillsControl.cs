using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kalendarz1.Transport.Theme;

namespace Kalendarz1.Transport.Controls
{
    /// <summary>
    /// FlowLayoutPanel z kolorowymi pills trasy:
    /// [START] → [Klient1 HH:mm] → [Klient2 HH:mm] → [POWRÓT]
    /// </summary>
    public class RoutePillsControl : Control
    {
        private List<RouteStop> _stops = new List<RouteStop>();

        public class RouteStop
        {
            public string Name { get; set; } = "";
            public string? Time { get; set; }
            public bool IsStart { get; set; }
            public bool IsReturn { get; set; }
        }

        public RoutePillsControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Height = 44;
            BackColor = ZpspColors.PanelDarkAlt;
        }

        public void SetRoute(IEnumerable<RouteStop> stops)
        {
            _stops = new List<RouteStop>(stops);
            Invalidate();
        }

        public void SetRoute(params string[] clientNames)
        {
            _stops = new List<RouteStop>();
            _stops.Add(new RouteStop { Name = "START", IsStart = true });
            foreach (var name in clientNames)
                _stops.Add(new RouteStop { Name = name });
            _stops.Add(new RouteStop { Name = "POWRÓT", IsReturn = true });
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bounds = ClientRectangle;
            int cornerRadius = 6;

            // Tło z zaokrąglonymi rogami + border
            using (var path = RoundedRect(bounds, cornerRadius))
            using (var bgBrush = new SolidBrush(ZpspColors.PanelDarkAlt))
            {
                g.FillPath(bgBrush, path);
            }
            using (var path = RoundedRect(new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1), cornerRadius))
            using (var borderPen = new Pen(ZpspColors.PanelDarkBorder, 1))
            {
                g.DrawPath(borderPen, path);
            }

            if (_stops.Count == 0) return;

            float x = 8;
            float y = (bounds.Height - 24) / 2f;
            int pillRadius = 4;

            for (int i = 0; i < _stops.Count; i++)
            {
                var stop = _stops[i];

                // Strzałka przed pill (poza pierwszym)
                if (i > 0)
                {
                    using var arrowBrush = new SolidBrush(ZpspColors.TextMuted);
                    g.DrawString("\u2192", ZpspFonts.Text9, arrowBrush, x, y + 3);
                    x += 18;
                }

                // Tekst pill
                string pillText;
                if (stop.IsStart) pillText = "\U0001f3ed START";
                else if (stop.IsReturn) pillText = "\U0001f3e0 POWRÓT";
                else pillText = stop.Time != null ? $"{stop.Name} ({stop.Time})" : stop.Name;

                var textSize = g.MeasureString(pillText, ZpspFonts.RoutePill);
                float pillWidth = textSize.Width + 16;
                float pillHeight = 22;
                var pillRect = new RectangleF(x, y, pillWidth, pillHeight);

                // Kolory pill
                Color bgColor, fgColor, borderColor;
                if (stop.IsStart)
                {
                    bgColor = ZpspColors.GreenDark;
                    fgColor = Color.White;
                    borderColor = ZpspColors.GreenDark;
                }
                else if (stop.IsReturn)
                {
                    bgColor = ZpspColors.Red;
                    fgColor = Color.White;
                    borderColor = ZpspColors.Red;
                }
                else
                {
                    bgColor = ZpspColors.PurpleBg;
                    fgColor = ZpspColors.Purple;
                    borderColor = ZpspColors.PurpleBg2;
                }

                using (var pillPath = RoundedRect(Rectangle.Round(pillRect), pillRadius))
                {
                    using (var bgBrush = new SolidBrush(bgColor))
                        g.FillPath(bgBrush, pillPath);
                    using (var borderPen = new Pen(borderColor, 1))
                        g.DrawPath(borderPen, pillPath);
                }

                using (var fgBrush = new SolidBrush(fgColor))
                    g.DrawString(pillText, ZpspFonts.RoutePill, fgBrush, x + 8, y + 3);

                x += pillWidth + 4;
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d > bounds.Width) d = bounds.Width;
            if (d > bounds.Height) d = bounds.Height;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
