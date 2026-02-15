// ═══════════════════════════════════════════════════════════════════════
// RoutePillsControl.cs — Wizualizacja trasy jako kolorowe "pills"
// ═══════════════════════════════════════════════════════════════════════
// Zastępuje zwykły TextBox trasy. Wyświetla:
//   [🏭 START] → [LOCIV IMPEX (RO)] → [PODOLSKI] → [🏠 POWRÓT]
// Każdy pill jest zaokrągloną etykietą z kolorem tła.
//
// UŻYCIE:
//   var pills = new RoutePillsControl();
//   pills.SetRoute(new[] { "LOCIV IMPEX (RO)", "PODOLSKI" });
//   parentPanel.Controls.Add(pills);
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZpspTransport.Theme;

namespace ZpspTransport.Controls
{
    public class RoutePillsControl : UserControl
    {
        private string[] _stops = Array.Empty<string>();
        private readonly FlowLayoutPanel _flow;

        public RoutePillsControl()
        {
            DoubleBuffered = true;
            BackColor = ZpspColors.PanelDarkAlt;
            Height = 34;
            Padding = new Padding(6, 4, 6, 4);

            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
            };
            Controls.Add(_flow);
        }

        /// <summary>
        /// Ustawia trasę. Automatycznie dodaje START na początku i POWRÓT na końcu.
        /// </summary>
        /// <param name="stopNames">Nazwy przystanków (bez START/POWRÓT)</param>
        public void SetRoute(string[] stopNames)
        {
            _stops = stopNames ?? Array.Empty<string>();
            RebuildPills();
        }

        private void RebuildPills()
        {
            _flow.SuspendLayout();
            _flow.Controls.Clear();

            // START pill
            AddPill("🏭 START", ZpspColors.GreenDark, Color.White, filled: true);

            foreach (var stop in _stops)
            {
                AddArrow();
                AddPill(stop, ZpspColors.Purple, ZpspColors.Purple, filled: false);
            }

            // POWRÓT pill
            if (_stops.Length > 0) AddArrow();
            AddPill("🏠 POWRÓT", ZpspColors.Red, Color.White, filled: true);

            _flow.ResumeLayout();
        }

        private void AddPill(string text, Color bgOrBorder, Color textColor, bool filled)
        {
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = textColor,
                AutoSize = true,
                Padding = new Padding(6, 2, 6, 2),
                Margin = new Padding(2),
            };

            if (filled)
            {
                lbl.BackColor = bgOrBorder;
            }
            else
            {
                lbl.BackColor = ZpspColors.PurpleBg;
                lbl.ForeColor = ZpspColors.Purple;
            }

            // Zaokrąglone rogi
            lbl.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, lbl.Width - 1, lbl.Height - 1);
                using var path = RoundedRect(rect, 4);

                if (filled)
                {
                    using var brush = new SolidBrush(bgOrBorder);
                    e.Graphics.FillPath(brush, path);
                }
                else
                {
                    using var brush = new SolidBrush(ZpspColors.PurpleBg);
                    e.Graphics.FillPath(brush, path);
                    using var pen = new Pen(ZpspColors.PurpleBg2, 1);
                    e.Graphics.DrawPath(pen, path);
                }

                TextRenderer.DrawText(e.Graphics, text, lbl.Font,
                    new Rectangle(0, 0, lbl.Width, lbl.Height),
                    lbl.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };

            _flow.Controls.Add(lbl);
        }

        private void AddArrow()
        {
            var arrow = new Label
            {
                Text = "→",
                Font = new Font("Segoe UI", 10f),
                ForeColor = ZpspColors.TextMuted,
                AutoSize = true,
                Margin = new Padding(0, 3, 0, 0),
                BackColor = Color.Transparent,
            };
            _flow.Controls.Add(arrow);
        }

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
