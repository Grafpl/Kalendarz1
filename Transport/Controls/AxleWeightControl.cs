using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kalendarz1.Transport.Theme;

namespace Kalendarz1.Transport.Controls
{
    /// <summary>
    /// Wizualizacja wagi na 3 osiach pojazdu — słupki z gradientem.
    /// Oś 1 (przód), Oś 2 (środek), Oś 3 (tył).
    /// Pod spodem: DMC podsumowanie.
    /// </summary>
    public class AxleWeightControl : Control
    {
        private int _axle1Kg, _axle2Kg, _axle3Kg;
        private int _maxPerAxle = 3000;
        private int _dmcKg;
        private int _dmcMaxKg = 18000;

        public AxleWeightControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Height = 160;
            BackColor = ZpspColors.PanelDarkAlt;
        }

        /// <summary>Ustaw wagę na osiach (równomierny rozkład jeśli autoDistribute=true).</summary>
        public void SetWeight(int totalKg, int dmcMaxKg = 18000, int maxPerAxle = 3000, bool autoDistribute = true)
        {
            _dmcKg = totalKg;
            _dmcMaxKg = dmcMaxKg;
            _maxPerAxle = maxPerAxle;

            if (autoDistribute && totalKg > 0)
            {
                // Rozkład: przód 30%, środek 40%, tył 30%
                _axle1Kg = (int)(totalKg * 0.30);
                _axle2Kg = (int)(totalKg * 0.40);
                _axle3Kg = totalKg - _axle1Kg - _axle2Kg;
            }
            else
            {
                _axle1Kg = _axle2Kg = _axle3Kg = 0;
            }

            Invalidate();
        }

        public void SetAxles(int axle1, int axle2, int axle3, int maxPerAxle = 3000, int dmcMaxKg = 18000)
        {
            _axle1Kg = axle1;
            _axle2Kg = axle2;
            _axle3Kg = axle3;
            _maxPerAxle = maxPerAxle;
            _dmcMaxKg = dmcMaxKg;
            _dmcKg = axle1 + axle2 + axle3;
            Invalidate();
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

            // Nagłówek
            using (var hBrush = new SolidBrush(ZpspColors.TextMuted))
                g.DrawString("\u2696\ufe0f WAGA NA OSIACH", ZpspFonts.Label8Bold, hBrush, 10, 8);

            // 3 słupki
            int barAreaTop = 30;
            int barAreaHeight = 80;
            int barWidth = 50;
            int gap = 30;
            int totalBarsWidth = barWidth * 3 + gap * 2;
            int startX = (bounds.Width - totalBarsWidth) / 2;

            int[] values = { _axle1Kg, _axle2Kg, _axle3Kg };
            string[] labels = { "Oś 1\n(przód)", "Oś 2\n(środek)", "Oś 3\n(tył)" };

            for (int i = 0; i < 3; i++)
            {
                int x = startX + i * (barWidth + gap);
                float fillPercent = _maxPerAxle > 0 ? Math.Min((float)values[i] / _maxPerAxle, 1f) : 0f;
                int fillHeight = (int)(barAreaHeight * fillPercent);

                Color barColor = values[i] > _maxPerAxle ? ZpspColors.Red :
                                 values[i] > _maxPerAxle * 0.7 ? ZpspColors.Orange :
                                 ZpspColors.Green;

                // Tło słupka
                var barRect = new Rectangle(x, barAreaTop, barWidth, barAreaHeight);
                using (var bgPath = RoundedRect(barRect, 4))
                using (var bgBrush = new SolidBrush(ZpspColors.PanelDarkBorder))
                    g.FillPath(bgBrush, bgPath);

                // Wypełnienie (od dołu)
                if (fillHeight > 0)
                {
                    var fillRect = new Rectangle(x, barAreaTop + barAreaHeight - fillHeight, barWidth, fillHeight);
                    using var fillPath = RoundedRect(fillRect, 4);

                    // Gradient: 44% alpha na górze → pełny na dole
                    using var gradient = new LinearGradientBrush(
                        fillRect,
                        ZpspColors.WithAlpha(barColor, 112),
                        barColor,
                        LinearGradientMode.Vertical);
                    g.FillPath(gradient, fillPath);
                }

                // Wartość nad słupkiem
                string valText = $"{values[i]:N0}";
                var valSize = g.MeasureString(valText, ZpspFonts.Label9Bold);
                using (var valBrush = new SolidBrush(ZpspColors.TextLight))
                    g.DrawString(valText, ZpspFonts.Label9Bold, valBrush, x + (barWidth - valSize.Width) / 2, barAreaTop - 16);

                // Label pod słupkiem
                using (var lblBrush = new SolidBrush(ZpspColors.TextMuted))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center };
                    g.DrawString(labels[i], ZpspFonts.Text7, lblBrush, new RectangleF(x, barAreaTop + barAreaHeight + 4, barWidth, 24), sf);
                }
            }

            // DMC podsumowanie
            int dmcY = barAreaTop + barAreaHeight + 30;
            bool ok = _dmcKg <= _dmcMaxKg;
            string dmcText = $"DMC: {_dmcKg:N0} / {_dmcMaxKg:N0} kg {(ok ? "\u2713 OK" : "\u26a0 PRZEKROCZONO")}";

            var dmcRect = new Rectangle(10, dmcY, bounds.Width - 20, 22);
            using (var dmcPath = RoundedRect(dmcRect, 4))
            {
                Color dmcBg = ok ? ZpspColors.GreenBg : ZpspColors.RedBg;
                Color dmcFg = ok ? ZpspColors.GreenDark : ZpspColors.Red;
                using (var bgBrush = new SolidBrush(dmcBg))
                    g.FillPath(bgBrush, dmcPath);
                using (var fgBrush = new SolidBrush(dmcFg))
                    g.DrawString(dmcText, ZpspFonts.Label9Bold, fgBrush, 16, dmcY + 3);
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
