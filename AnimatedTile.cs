using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kalendarz1
{
    // ════════════════════════════════════════════════════════════════════════════════
    // ANIMOWANY KAFELEK MENU - Z efektami hover, ripple, bounce
    // ════════════════════════════════════════════════════════════════════════════════
    public class AnimatedTile : Panel
    {
        private Color baseColor = Color.White;
        private Color hoverColor = Color.FromArgb(248, 250, 252);
        private Color accentColor;
        private float currentScale = 1.0f;
        private float targetScale = 1.0f;
        private int shadowOffset = 2;
        private int targetShadowOffset = 2;
        private float rippleSize = 0;
        private Point rippleCenter;
        private Timer animationTimer;
        private Timer rippleTimer;
        private Timer iconBounceTimer;
        private Label iconLabel;
        private int iconBaseY;
        private float bouncePhase = 0;
        private bool isHovered = false;

        public AnimatedTile(Color accent)
        {
            this.accentColor = accent;
            this.Size = new Size(180, 120);
            this.BackColor = baseColor;
            this.Cursor = Cursors.Hand;
            this.DoubleBuffered = true;
            this.Margin = new Padding(10);

            // Timer animacji hover
            animationTimer = new Timer { Interval = 16 }; // ~60fps
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();

            // Timer ripple
            rippleTimer = new Timer { Interval = 16 };
            rippleTimer.Tick += RippleTimer_Tick;

            // Timer bounce ikony
            iconBounceTimer = new Timer { Interval = 25 };
            iconBounceTimer.Tick += IconBounceTimer_Tick;

            this.MouseEnter += (s, e) => {
                isHovered = true;
                targetScale = 1.03f;
                targetShadowOffset = 8;
                iconBounceTimer.Start();
            };

            this.MouseLeave += (s, e) => {
                isHovered = false;
                targetScale = 1.0f;
                targetShadowOffset = 2;
                iconBounceTimer.Stop();
                bouncePhase = 0;
                if (iconLabel != null) iconLabel.Top = iconBaseY;
            };

            this.MouseDown += (s, e) => {
                rippleCenter = e.Location;
                rippleSize = 0;
                rippleTimer.Start();
            };
        }

        public void SetIconLabel(Label label)
        {
            iconLabel = label;
            iconBaseY = label.Top;
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            bool needsRedraw = false;

            // Animacja skali
            if (Math.Abs(currentScale - targetScale) > 0.001f)
            {
                currentScale += (targetScale - currentScale) * 0.2f;
                needsRedraw = true;
            }

            // Animacja cienia
            if (Math.Abs(shadowOffset - targetShadowOffset) > 0.5f)
            {
                shadowOffset += (int)((targetShadowOffset - shadowOffset) * 0.3f);
                if (shadowOffset == 0 && targetShadowOffset > 0) shadowOffset = 1;
                needsRedraw = true;
            }

            // Animacja koloru
            Color targetColor = isHovered ? hoverColor : baseColor;
            if (BackColor != targetColor)
            {
                int r = BackColor.R + (int)((targetColor.R - BackColor.R) * 0.2f);
                int g = BackColor.G + (int)((targetColor.G - BackColor.G) * 0.2f);
                int b = BackColor.B + (int)((targetColor.B - BackColor.B) * 0.2f);
                BackColor = Color.FromArgb(r, g, b);
                needsRedraw = true;
            }

            if (needsRedraw) Invalidate();
        }

        private void RippleTimer_Tick(object sender, EventArgs e)
        {
            rippleSize += 15;
            if (rippleSize > Math.Max(Width, Height) * 2)
            {
                rippleTimer.Stop();
                rippleSize = 0;
            }
            Invalidate();
        }

        private void IconBounceTimer_Tick(object sender, EventArgs e)
        {
            if (iconLabel == null) return;
            bouncePhase += 0.3f;
            int bounceY = (int)(Math.Sin(bouncePhase) * 3);
            iconLabel.Top = iconBaseY + bounceY;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Oblicz przeskalowany prostokąt
            int scaledWidth = (int)(Width * currentScale);
            int scaledHeight = (int)(Height * currentScale);
            int offsetX = (Width - scaledWidth) / 2;
            int offsetY = (Height - scaledHeight) / 2;

            // Rysuj cień
            if (shadowOffset > 0)
            {
                using (var shadowBrush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
                {
                    e.Graphics.FillRectangle(shadowBrush,
                        offsetX + shadowOffset, offsetY + shadowOffset,
                        scaledWidth, scaledHeight);
                }
            }

            // Rysuj tło kafelka
            using (var bgBrush = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(bgBrush, offsetX, offsetY, scaledWidth, scaledHeight);
            }

            // Rysuj ramkę
            using (var borderPen = new Pen(Color.FromArgb(220, 220, 220), 1))
            {
                e.Graphics.DrawRectangle(borderPen, offsetX, offsetY, scaledWidth - 1, scaledHeight - 1);
            }

            // Rysuj dolny pasek koloru
            using (var accentBrush = new SolidBrush(accentColor))
            {
                e.Graphics.FillRectangle(accentBrush, offsetX, offsetY + scaledHeight - 5, scaledWidth, 5);
            }

            // Efekt ripple
            if (rippleSize > 0)
            {
                int alpha = Math.Max(0, 50 - (int)(rippleSize / 5));
                using (var rippleBrush = new SolidBrush(Color.FromArgb(alpha, accentColor)))
                {
                    e.Graphics.FillEllipse(rippleBrush,
                        rippleCenter.X - rippleSize / 2,
                        rippleCenter.Y - rippleSize / 2,
                        rippleSize, rippleSize);
                }
            }

            // Poświata przy hover
            if (isHovered && currentScale > 1.01f)
            {
                using (var glowPen = new Pen(Color.FromArgb(40, accentColor), 2))
                {
                    e.Graphics.DrawRectangle(glowPen, offsetX - 1, offsetY - 1, scaledWidth + 1, scaledHeight + 1);
                }
            }
        }

        protected override CreateParams CreateParams
        {
            get {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }
    }
}
