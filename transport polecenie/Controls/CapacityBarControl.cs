// ═══════════════════════════════════════════════════════════════════════
// CapacityBarControl.cs — Pasek ładowności naczepy (custom ProgressBar)
// ═══════════════════════════════════════════════════════════════════════
// Rysuje kolorowy pasek: zielony (0-50%), pomarańczowy (50-80%),
// żółty (80-100%), czerwony z hatching (>100%).
// Pokazuje wartość procentową po prawej stronie.
//
// UŻYCIE W FORMIE:
//   var bar = new CapacityBarControl();
//   bar.SetCapacity(21.4m, 4m);  // 21.4 palet / 4 max = 536%
//   panel.Controls.Add(bar);
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZpspTransport.Theme;

namespace ZpspTransport.Controls
{
    public class CapacityBarControl : UserControl
    {
        private decimal _used = 0;
        private decimal _max = 1;
        private decimal _percent => _max > 0 ? (_used / _max) * 100m : 0m;

        // ─── PUBLICZNE PROPERTIES ───

        /// <summary>Wysokość samego paska (nie całego kontrolki)</summary>
        public int BarHeight { get; set; } = 12;
        
        /// <summary>Czy pokazywać procent po prawej</summary>
        public bool ShowPercent { get; set; } = true;
        
        /// <summary>Czy pokazywać opis "21.4 / 4 max" pod paskiem</summary>
        public bool ShowValues { get; set; } = true;
        
        /// <summary>Czy pokazywać ostrzeżenie "⚠ PRZEŁADOWANE" po prawej</summary>
        public bool ShowOverloadWarning { get; set; } = true;

        public CapacityBarControl()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Height = 50;
            BackColor = Color.Transparent;
        }

        /// <summary>
        /// Ustaw wartości ładowności.
        /// </summary>
        /// <param name="used">Aktualne palety (np. 21.4)</param>
        /// <param name="max">Maksymalne palety pojazdu (np. 4)</param>
        public void SetCapacity(decimal used, decimal max)
        {
            _used = used;
            _max = max > 0 ? max : 1;
            Invalidate(); // Przerysuj
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int y = 2;
            int barWidth = ShowPercent ? Width - 70 : Width - 10;
            int barLeft = 5;

            // ─── WIERSZ 1: Pasek ───

            // Tło paska (szare)
            using (var bgBrush = new SolidBrush(ZpspColors.CapacityBg))
            {
                var bgRect = new Rectangle(barLeft, y, barWidth, BarHeight);
                int radius = BarHeight / 2;
                using var bgPath = RoundedRect(bgRect, radius);
                g.FillPath(bgBrush, bgPath);
            }

            // Wypełnienie paska
            decimal displayPct = Math.Min(_percent, 100m);
            int fillWidth = (int)(barWidth * displayPct / 100m);
            if (fillWidth > 0)
            {
                var fillRect = new Rectangle(barLeft, y, fillWidth, BarHeight);
                int radius = BarHeight / 2;

                if (_percent > 100)
                {
                    // >100% — czerwony z ukośnym hatching
                    using var hatchBrush = new HatchBrush(
                        HatchStyle.ForwardDiagonal,
                        ZpspColors.Red,
                        ZpspColors.RedDark);
                    using var fillPath = RoundedRect(fillRect, radius);
                    g.FillPath(hatchBrush, fillPath);
                }
                else
                {
                    // Normalny kolor zależny od procentu
                    Color barColor = _percent switch
                    {
                        > 80 => ZpspColors.Orange,
                        > 50 => ZpspColors.OrangeLight,
                        _ => ZpspColors.Green
                    };

                    using var fillBrush = new SolidBrush(barColor);
                    using var fillPath = RoundedRect(fillRect, radius);
                    g.FillPath(fillBrush, fillPath);
                }
            }

            // Procent po prawej stronie paska
            if (ShowPercent)
            {
                Color pctColor = _percent switch
                {
                    > 100 => ZpspColors.Red,
                    > 80 => ZpspColors.Orange,
                    _ => ZpspColors.Green
                };

                using var pctFont = new Font("Segoe UI", 14f, FontStyle.Bold);
                using var pctBrush = new SolidBrush(pctColor);
                string pctText = $"{_percent:F0}%";
                var pctSize = g.MeasureString(pctText, pctFont);
                g.DrawString(pctText, pctFont, pctBrush,
                    barLeft + barWidth + 8,
                    y + (BarHeight - pctSize.Height) / 2);
            }

            // ─── WIERSZ 2: Wartości pod paskiem ───
            if (ShowValues)
            {
                int valY = y + BarHeight + 4;

                using var valFont = new Font("Segoe UI", 9f, FontStyle.Regular);
                using var valBrush = new SolidBrush(ZpspColors.TextMuted);
                g.DrawString($"{_used:F1} palet / {_max} max  •  ", valFont, valBrush, barLeft, valY);

                // Ostrzeżenie
                if (ShowOverloadWarning && _percent > 100)
                {
                    var textSize = g.MeasureString($"{_used:F1} palet / {_max} max  •  ", valFont);
                    using var warnFont = new Font("Segoe UI", 9f, FontStyle.Bold);
                    using var warnBrush = new SolidBrush(ZpspColors.Red);
                    g.DrawString("⚠ PRZEŁADOWANE!", warnFont, warnBrush, barLeft + textSize.Width, valY);
                }
            }
        }

        /// <summary>Tworzy zaokrąglony prostokąt (GraphicsPath)</summary>
        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
