using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kalendarz1
{
    // ════════════════════════════════════════════════════════════════════════════════
    // KAFELEK MENU — wersja bez animacji.
    // Zostawione tylko proste podświetlenie obramowania na hover, dla wyraźnej
    // informacji wizualnej co jest aktualnie pod kursorem.
    // Propagacja zdarzeń myszy z dzieci (Label, ikona) — bez tego ramka znikała,
    // gdy kursor przelatywał z tła kafelka na tekst tytułu/opisu.
    // ════════════════════════════════════════════════════════════════════════════════
    public class AnimatedTile : Panel
    {
        private static readonly Color BaseColor = Color.White;
        private static readonly Color PressedColor = Color.FromArgb(240, 244, 248);
        private static readonly Color BorderIdle = Color.FromArgb(220, 220, 220);

        private readonly Color accentColor;
        private bool isHovered;
        private bool isPressed;

        public AnimatedTile(Color accent)
        {
            this.accentColor = accent;
            this.Size = new Size(180, 120);
            this.BackColor = BaseColor;
            this.Cursor = Cursors.Hand;
            this.DoubleBuffered = true;
            this.Margin = new Padding(10);
        }

        // No-op — wcześniej kafelek "bujał" ikoną przy hoverze. Zostawione dla zgodności
        // z Menu.cs, który wciąż wywołuje tile.SetIconLabel(...).
        public void SetIconLabel(Label label) { }

        // ──────────────────────────────────────────────────────────────────────
        // Hover/press handling z propagacją z dzieci.
        // WinForms nie propaguje MouseEnter/Leave w górę hierarchii — gdy kursor
        // przesuwa się z parent na child, parent dostaje MouseLeave (a my chcemy
        // aby ramka pozostała). Rozwiązanie: hookujemy każdą kontrolkę-dziecko
        // i sprawdzamy fizyczną pozycję kursora przy "leave".
        // ──────────────────────────────────────────────────────────────────────

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            SetHovered(true);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            UpdateHoverFromCursor();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            SetPressed(true);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            SetPressed(false);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            AttachHoverPropagation(e.Control);
        }

        private void AttachHoverPropagation(Control c)
        {
            c.MouseEnter += (s, ev) => SetHovered(true);
            c.MouseLeave += (s, ev) => UpdateHoverFromCursor();
            c.MouseDown += (s, ev) => SetPressed(true);
            c.MouseUp += (s, ev) => SetPressed(false);

            // Rekurencyjnie dla istniejących dzieci dziecka (np. badge na panelu kafelka)
            foreach (Control child in c.Controls)
                AttachHoverPropagation(child);

            // I dla dzieci dodanych później
            c.ControlAdded += (s, ev) => AttachHoverPropagation(ev.Control);
        }

        // Sprawdza gdzie naprawdę jest kursor i ustawia hover odpowiednio.
        // Wywoływane przy MouseLeave parent/child — kursor mógł:
        //   (a) wciąż być nad kafelkiem, tylko nad innym dzieckiem → zostaw hover
        //   (b) wyjść poza kafelek całkowicie → wyłącz hover
        private void UpdateHoverFromCursor()
        {
            if (IsDisposed || !IsHandleCreated) return;
            var p = PointToClient(Cursor.Position);
            bool inside = ClientRectangle.Contains(p);
            SetHovered(inside);
            if (!inside) SetPressed(false);
        }

        private void SetHovered(bool value)
        {
            if (isHovered == value) return;
            isHovered = value;
            Invalidate();
        }

        private void SetPressed(bool value)
        {
            if (isPressed == value) return;
            isPressed = value;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Tło — lekki tint przy wciśnięciu, dla feedbacku że klik został zarejestrowany.
            using (var bg = new SolidBrush(isPressed ? PressedColor : BaseColor))
            {
                g.FillRectangle(bg, 0, 0, Width, Height);
            }

            // Dolny pasek koloru (akcent kategorii) — zawsze widoczny.
            using (var accentBrush = new SolidBrush(accentColor))
            {
                g.FillRectangle(accentBrush, 0, Height - 5, Width, 5);
            }

            // Obramowanie:
            //  - idle: 1px szare
            //  - hover: 3px w kolorze akcentu — wyraźne podświetlenie kafelka pod kursorem
            int thickness = isHovered ? 3 : 1;
            Color borderColor = isHovered ? accentColor : BorderIdle;
            using (var pen = new Pen(borderColor, thickness))
            {
                float inset = thickness / 2f;
                g.DrawRectangle(pen, inset, inset, Width - thickness, Height - thickness);
            }
        }
    }
}
