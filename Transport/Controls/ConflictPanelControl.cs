using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Kalendarz1.Transport.Theme;

namespace Kalendarz1.Transport.Controls
{
    public enum ConflictLevel { Error, Warning, Info }

    public class CourseConflict
    {
        public string Code { get; set; } = "";
        public ConflictLevel Level { get; set; }
        public string Message { get; set; } = "";
        public string? Detail { get; set; }
    }

    /// <summary>
    /// Kompaktowy + rozwijalny panel konfliktów.
    /// Zwinięty: ~40px z pill podsumowaniem. Rozwinięty: lista alertów.
    /// </summary>
    public class ConflictPanelControl : Control
    {
        private List<CourseConflict> _conflicts = new List<CourseConflict>();
        private bool _expanded;
        private const int CollapsedHeight = 42;
        private const int ItemHeight = 28;
        private const int MaxExpandedItems = 6;

        public ConflictPanelControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Height = CollapsedHeight;
            BackColor = ZpspColors.PanelDarkAlt;
            Cursor = Cursors.Hand;
        }

        public bool HasErrors => _conflicts.Any(c => c.Level == ConflictLevel.Error);
        public int ErrorCount => _conflicts.Count(c => c.Level == ConflictLevel.Error);
        public int WarningCount => _conflicts.Count(c => c.Level == ConflictLevel.Warning);
        public int InfoCount => _conflicts.Count(c => c.Level == ConflictLevel.Info);

        public void SetConflicts(IEnumerable<CourseConflict> conflicts)
        {
            _conflicts = new List<CourseConflict>(conflicts);
            if (_conflicts.Count == 0) _expanded = false;
            UpdateHeight();
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_conflicts.Count == 0) return;
            _expanded = !_expanded;
            UpdateHeight();
            Invalidate();
        }

        private void UpdateHeight()
        {
            if (_expanded && _conflicts.Count > 0)
                Height = CollapsedHeight + Math.Min(_conflicts.Count, MaxExpandedItems) * ItemHeight + 6;
            else
                Height = CollapsedHeight;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bounds = ClientRectangle;
            int radius = 6;

            // Tło + border
            using (var path = RoundedRect(bounds, radius))
            using (var bg = new SolidBrush(ZpspColors.PanelDarkAlt))
                g.FillPath(bg, path);
            using (var path = RoundedRect(new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1), radius))
            using (var pen = new Pen(ZpspColors.PanelDarkBorder, 1))
                g.DrawPath(pen, path);

            int x = 10;
            int y = 10;

            // Nagłówek
            if (_conflicts.Count == 0)
            {
                using var okBrush = new SolidBrush(ZpspColors.Green);
                g.DrawString("\u2714 Brak konfliktów", ZpspFonts.Label10Bold, okBrush, x, y);
                return;
            }

            using (var headerBrush = new SolidBrush(ZpspColors.TextWhite))
                g.DrawString("\u26a0 KONFLIKTY", ZpspFonts.Label10Bold, headerBrush, x, y);
            x += 120;

            // Pills
            int errors = ErrorCount;
            int warnings = WarningCount;
            int infos = InfoCount;

            if (errors > 0) x = DrawPill(g, x, y, $"\U0001f534 {errors} bł.", ZpspColors.Red, Color.White);
            if (warnings > 0) x = DrawPill(g, x, y, $"\U0001f7e1 {warnings} ostrz.", ZpspColors.Orange, Color.White);
            if (infos > 0) x = DrawPill(g, x, y, $"\U0001f535 {infos} info", ZpspColors.Blue, Color.White);

            // Przycisk Rozwiń/Zwiń
            string toggleText = _expanded ? "Zwiń \u25b2" : "Rozwiń \u25bc";
            var toggleSize = g.MeasureString(toggleText, ZpspFonts.Text9);
            using (var toggleBrush = new SolidBrush(ZpspColors.TextMuted))
                g.DrawString(toggleText, ZpspFonts.Text9, toggleBrush, bounds.Width - toggleSize.Width - 12, y + 2);

            // Lista alertów (jeśli rozwinięty)
            if (_expanded)
            {
                int itemY = CollapsedHeight;
                int count = 0;
                foreach (var conflict in _conflicts)
                {
                    if (count >= MaxExpandedItems) break;

                    Color borderColor, bgColor;
                    switch (conflict.Level)
                    {
                        case ConflictLevel.Error:   borderColor = ZpspColors.Red;    bgColor = ZpspColors.RedBg; break;
                        case ConflictLevel.Warning: borderColor = ZpspColors.Orange;  bgColor = ZpspColors.OrangeBg; break;
                        default:                    borderColor = ZpspColors.Blue;    bgColor = ZpspColors.BlueBg; break;
                    }

                    var itemRect = new Rectangle(8, itemY, bounds.Width - 16, ItemHeight - 2);

                    // Tło alertu
                    using (var itemBg = new SolidBrush(bgColor))
                        g.FillRectangle(itemBg, itemRect);

                    // Border-left 3px
                    using (var borderBrush = new SolidBrush(borderColor))
                        g.FillRectangle(borderBrush, itemRect.X, itemRect.Y, 3, itemRect.Height);

                    // Tekst alertu
                    using (var textBrush = new SolidBrush(ZpspColors.TextDark))
                        g.DrawString(conflict.Message, ZpspFonts.Text9, textBrush, itemRect.X + 10, itemRect.Y + 5);

                    itemY += ItemHeight;
                    count++;
                }
            }
        }

        private int DrawPill(Graphics g, int x, int y, string text, Color bgColor, Color fgColor)
        {
            var size = g.MeasureString(text, ZpspFonts.Text8);
            var pillRect = new Rectangle(x, y, (int)size.Width + 12, 20);

            using (var path = RoundedRect(pillRect, 3))
            using (var bg = new SolidBrush(bgColor))
                g.FillPath(bg, path);
            using (var fg = new SolidBrush(fgColor))
                g.DrawString(text, ZpspFonts.Text8, fg, x + 6, y + 3);

            return x + pillRect.Width + 6;
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
