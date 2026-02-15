// ═══════════════════════════════════════════════════════════════════════
// ConflictPanelControl.cs — Panel wyświetlania konfliktów
// ═══════════════════════════════════════════════════════════════════════
// Wyświetla listę wykrytych konfliktów w formie kolorowych alertów.
// Każdy alert ma ikonę, kolor tła (zależny od poziomu), treść i szczegóły.
// Kliknięcie na alert rozwija/zwija szczegóły.
//
// UŻYCIE:
//   var panel = new ConflictPanelControl();
//   panel.SetConflicts(conflictList);
//   parentPanel.Controls.Add(panel);
//
// Wywołuj SetConflicts() po każdej zmianie w kursie.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZpspTransport.Models;
using ZpspTransport.Theme;

namespace ZpspTransport.Controls
{
    public class ConflictPanelControl : UserControl
    {
        private List<CourseConflict> _conflicts = new();
        private readonly FlowLayoutPanel _flowPanel;
        private readonly Label _headerLabel;
        private readonly Label _countLabel;

        public ConflictPanelControl()
        {
            DoubleBuffered = true;
            BackColor = ZpspColors.PanelDark;
            Padding = new Padding(0);

            // ─── HEADER ───
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = ZpspColors.PanelDarkAlt,
                Padding = new Padding(10, 0, 10, 0),
            };

            _headerLabel = new Label
            {
                Text = "⚠️ WYKRYTE PROBLEMY",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = ZpspColors.TextWhite,
                AutoSize = true,
                Location = new Point(10, 7),
            };

            _countLabel = new Label
            {
                Text = "0",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ZpspColors.TextWhite,
                BackColor = ZpspColors.Red,
                AutoSize = false,
                Size = new Size(24, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(180, 7),
            };
            // Zaokrąglone tło count badge
            _countLabel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, _countLabel.Width, _countLabel.Height), 9);
                using var brush = new SolidBrush(_countLabel.BackColor);
                e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(e.Graphics, _countLabel.Text, _countLabel.Font,
                    new Rectangle(0, 0, _countLabel.Width, _countLabel.Height),
                    _countLabel.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };

            headerPanel.Controls.Add(_headerLabel);
            headerPanel.Controls.Add(_countLabel);

            // ─── SCROLL AREA ───
            _flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(6, 4, 6, 4),
                BackColor = ZpspColors.PanelDark,
            };

            Controls.Add(_flowPanel);
            Controls.Add(headerPanel);
        }

        /// <summary>
        /// Ustawia listę konfliktów do wyświetlenia.
        /// Wywołuj po każdej zmianie w kursie.
        /// </summary>
        public void SetConflicts(List<CourseConflict> conflicts)
        {
            _conflicts = conflicts ?? new();
            RebuildUI();
        }

        private void RebuildUI()
        {
            _flowPanel.SuspendLayout();
            _flowPanel.Controls.Clear();

            // Update counter
            int errors = _conflicts.Count(c => c.Level == ConflictLevel.Error);
            int warnings = _conflicts.Count(c => c.Level == ConflictLevel.Warning);
            int infos = _conflicts.Count(c => c.Level == ConflictLevel.Info);

            _countLabel.Text = _conflicts.Count.ToString();
            _countLabel.BackColor = errors > 0 ? ZpspColors.Red
                : warnings > 0 ? ZpspColors.Orange
                : ZpspColors.Blue;
            _countLabel.Visible = _conflicts.Count > 0;

            if (_conflicts.Count == 0)
            {
                // Brak konfliktów — pokaż zielony komunikat
                var okPanel = CreateAlertPanel(new CourseConflict
                {
                    Level = ConflictLevel.Info,
                    Code = "ALL_OK",
                    Message = "✅ Brak wykrytych problemów — kurs wygląda OK",
                });
                okPanel.BackColor = ZpspColors.GreenBg;
                _flowPanel.Controls.Add(okPanel);
            }
            else
            {
                // Pokaż summary (np. "2 błędy, 3 ostrzeżenia, 1 info")
                var parts = new List<string>();
                if (errors > 0) parts.Add($"🔴 {errors} {Plural(errors, "błąd", "błędy", "błędów")}");
                if (warnings > 0) parts.Add($"🟡 {warnings} {Plural(warnings, "ostrzeżenie", "ostrzeżenia", "ostrzeżeń")}");
                if (infos > 0) parts.Add($"🔵 {infos} {Plural(infos, "info", "info", "info")}");

                var summaryLabel = new Label
                {
                    Text = string.Join("   ", parts),
                    Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                    ForeColor = ZpspColors.TextLight,
                    AutoSize = false,
                    Height = 20,
                    Width = _flowPanel.ClientSize.Width - 16,
                    Padding = new Padding(4, 2, 0, 2),
                };
                _flowPanel.Controls.Add(summaryLabel);

                // Dodaj każdy alert
                foreach (var conflict in _conflicts)
                {
                    var alertPanel = CreateAlertPanel(conflict);
                    _flowPanel.Controls.Add(alertPanel);
                }
            }

            _flowPanel.ResumeLayout();
        }

        /// <summary>
        /// Tworzy pojedynczy panel alertu z ikoną, treścią i rozwijalnym szczegółem.
        /// </summary>
        private Panel CreateAlertPanel(CourseConflict conflict)
        {
            // Kolory zależne od poziomu
            Color bgColor = conflict.Level switch
            {
                ConflictLevel.Error => ZpspColors.RedBg,
                ConflictLevel.Warning => ZpspColors.OrangeBg,
                _ => ZpspColors.BlueBg
            };
            Color borderColor = conflict.Level switch
            {
                ConflictLevel.Error => Color.FromArgb(255, 205, 210),   // #FFCDD2
                ConflictLevel.Warning => Color.FromArgb(255, 224, 178), // #FFE0B2
                _ => Color.FromArgb(187, 222, 251)                      // #BBDEFB
            };
            Color textColor = conflict.Level switch
            {
                ConflictLevel.Error => ZpspColors.Red,
                ConflictLevel.Warning => Color.FromArgb(230, 126, 34),
                _ => ZpspColors.Blue
            };

            var panel = new Panel
            {
                Width = _flowPanel.ClientSize.Width - 16,
                Height = 36,
                BackColor = bgColor,
                Margin = new Padding(0, 2, 0, 2),
                Cursor = Cursors.Hand,
                Tag = false, // collapsed state
            };

            // Ikona
            var iconLabel = new Label
            {
                Text = conflict.Icon,
                Font = new Font("Segoe UI", 11f),
                Location = new Point(8, 8),
                AutoSize = true,
            };

            // Treść
            var msgLabel = new Label
            {
                Text = conflict.Message,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                ForeColor = ZpspColors.TextDark,
                Location = new Point(32, 9),
                AutoSize = true,
                MaximumSize = new Size(panel.Width - 50, 0),
            };

            // Szczegóły (ukryte domyślnie)
            Label? detailLabel = null;
            if (!string.IsNullOrEmpty(conflict.Details))
            {
                detailLabel = new Label
                {
                    Text = conflict.Details,
                    Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                    ForeColor = ZpspColors.TextMedium,
                    Location = new Point(32, 32),
                    AutoSize = true,
                    MaximumSize = new Size(panel.Width - 50, 0),
                    Visible = false,
                };
            }

            // Toggle expand/collapse on click
            var detailRef = detailLabel; // capture for closure
            EventHandler toggleHandler = (s, e) =>
            {
                if (detailRef == null) return;
                bool expanded = (bool)(panel.Tag ?? false);
                expanded = !expanded;
                panel.Tag = expanded;
                detailRef.Visible = expanded;
                panel.Height = expanded ? 36 + detailRef.Height + 8 : 36;
            };

            panel.Click += toggleHandler;
            iconLabel.Click += toggleHandler;
            msgLabel.Click += toggleHandler;

            // Border left
            panel.Paint += (s, e) =>
            {
                using var pen = new Pen(textColor, 3);
                e.Graphics.DrawLine(pen, 0, 0, 0, panel.Height);
                // Border
                using var borderPen = new Pen(borderColor, 1);
                e.Graphics.DrawRectangle(borderPen, 0, 0, panel.Width - 1, panel.Height - 1);
            };

            panel.Controls.Add(iconLabel);
            panel.Controls.Add(msgLabel);
            if (detailLabel != null) panel.Controls.Add(detailLabel);

            return panel;
        }

        private static string Plural(int n, string one, string few, string many)
        {
            if (n == 1) return one;
            if (n >= 2 && n <= 4) return few;
            return many;
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
