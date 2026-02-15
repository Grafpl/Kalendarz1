using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kalendarz1.Transport.Theme;

namespace Kalendarz1.Transport.Controls
{
    /// <summary>
    /// Custom pasek ładowności naczepy z hatching >100%, zaokrąglone rogi,
    /// strefy kolorów: zielony (0-50%), pomarańczowy jasny (50-80%),
    /// pomarańczowy (80-100%), czerwony hatching (>100%).
    /// </summary>
    public class CapacityBarControl : Control
    {
        private decimal _sumaPalet;
        private int _maxPalet = 33;
        private int _sumaPojemnikow;
        private int _sumaWagaKg;
        private decimal _procent;

        public CapacityBarControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Height = 90;
            BackColor = ZpspColors.PanelDarkAlt;
        }

        public void SetCapacity(decimal sumaPalet, int maxPalet, int sumaPojemnikow = 0, int sumaWagaKg = 0)
        {
            _sumaPalet = sumaPalet;
            _maxPalet = Math.Max(1, maxPalet);
            _sumaPojemnikow = sumaPojemnikow;
            _sumaWagaKg = sumaWagaKg;
            _procent = _sumaPalet / _maxPalet * 100m;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bounds = ClientRectangle;
            int pad = 10;
            int cornerRadius = 6;

            // Tło z zaokrąglonymi rogami
            using (var path = RoundedRect(bounds, cornerRadius))
            using (var bgBrush = new SolidBrush(ZpspColors.PanelDarkAlt))
            {
                g.FillPath(bgBrush, path);
            }

            // Obramowanie
            using (var path = RoundedRect(new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1), cornerRadius))
            using (var borderPen = new Pen(ZpspColors.PanelDarkBorder, 1))
            {
                g.DrawPath(borderPen, path);
            }

            // Wiersz nagłówka
            int y = pad;
            string labelText = "ŁADOWNOŚĆ NACZEPY";
            using (var labelBrush = new SolidBrush(ZpspColors.TextMuted))
                g.DrawString(labelText, ZpspFonts.Label8Bold, labelBrush, pad, y);

            // Procent po prawej
            string procentText = $"{_procent:F0}%";
            Color procentColor = _procent > 100 ? ZpspColors.Red : (_procent > 80 ? ZpspColors.Orange : ZpspColors.Green);
            using (var procentBrush = new SolidBrush(procentColor))
            {
                var procentSize = g.MeasureString(procentText, ZpspFonts.CapacityPercent);
                float procentX = bounds.Width - pad - procentSize.Width;

                // Pill "PRZEŁADOWANE" jeśli >100%
                if (_procent > 100)
                {
                    string pillText = "PRZEŁADOWANE";
                    var pillSize = g.MeasureString(pillText, ZpspFonts.Label8Bold);
                    float pillX = procentX - pillSize.Width - 16;
                    float pillY = y + 2;
                    var pillRect = new RectangleF(pillX, pillY, pillSize.Width + 10, pillSize.Height + 4);

                    using (var pillPath = RoundedRect(Rectangle.Round(pillRect), 3))
                    using (var pillBg = new SolidBrush(ZpspColors.RedBg))
                    using (var pillFg = new SolidBrush(ZpspColors.Red))
                    {
                        g.FillPath(pillBg, pillPath);
                        g.DrawString(pillText, ZpspFonts.Label8Bold, pillFg, pillX + 5, pillY + 2);
                    }
                }

                g.DrawString(procentText, ZpspFonts.CapacityPercent, procentBrush, procentX, y - 4);
            }

            // Pasek postępu
            y += 28;
            int barHeight = 12;
            int barX = pad;
            int barWidth = bounds.Width - pad * 2;
            var barRect = new Rectangle(barX, y, barWidth, barHeight);
            int barRadius = barHeight / 2;

            // Tło paska
            using (var bgPath = RoundedRect(barRect, barRadius))
            using (var bgBrush = new SolidBrush(ZpspColors.ProgressBg))
            {
                g.FillPath(bgBrush, bgPath);
            }

            // Wypełnienie
            float fillPercent = Math.Min((float)_procent, 100f) / 100f;
            int fillWidth = (int)(barWidth * fillPercent);
            if (_procent > 100) fillWidth = barWidth;

            if (fillWidth > 0)
            {
                var fillRect = new Rectangle(barX, y, Math.Max(fillWidth, barRadius * 2), barHeight);
                using var fillPath = RoundedRect(fillRect, barRadius);

                if (_procent > 100)
                {
                    // Czerwony hatching
                    using var hatchBrush = new HatchBrush(HatchStyle.ForwardDiagonal, ZpspColors.Red, ZpspColors.RedDark);
                    g.FillPath(hatchBrush, fillPath);
                }
                else
                {
                    Color barColor;
                    if (_procent <= 50) barColor = ZpspColors.Green;
                    else if (_procent <= 80) barColor = ZpspColors.OrangeLight;
                    else barColor = ZpspColors.Orange;

                    using var fillBrush = new SolidBrush(barColor);
                    g.FillPath(fillBrush, fillPath);
                }
            }

            // Info pod paskiem
            y += barHeight + 6;
            string infoText = $"{_sumaPalet:N1} palet / {_maxPalet} max";
            if (_sumaPojemnikow > 0)
                infoText += $" \u2022 {_sumaPojemnikow:N0} pojemników";
            if (_sumaWagaKg > 0)
                infoText += $" \u2022 {_sumaWagaKg:N0} kg";

            using (var infoBrush = new SolidBrush(ZpspColors.TextMuted))
                g.DrawString(infoText, ZpspFonts.Text9, infoBrush, pad, y);
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
