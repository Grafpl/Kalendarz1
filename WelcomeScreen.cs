using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kalendarz1
{
    /// <summary>
    /// Ekran powitalny wyświetlany po zalogowaniu z avatarem i imieniem użytkownika
    /// </summary>
    public class WelcomeScreen : Form
    {
        private Timer animationTimer;
        private Timer closeTimer;
        private int animationPhase = 0; // 0=fade in, 1=display, 2=fade out
        private float currentOpacity = 0;
        private string userName;
        private string odbiorcaId;
        private int avatarSize = 80;
        private Image cachedAvatar = null;

        public WelcomeScreen(string odbiorcaId, string userName)
        {
            this.odbiorcaId = odbiorcaId;
            this.userName = userName;

            InitializeForm();
            WindowIconHelper.SetIcon(this);
            CacheAvatar();
            SetupTimers();
        }

        private void InitializeForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(350, 120);
            this.BackColor = Color.FromArgb(45, 57, 69);
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Opacity = 0;
            this.DoubleBuffered = true;

            // Pozycja na dole ekranu, wycentrowane
            var screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(
                (screen.Width - this.Width) / 2,
                screen.Bottom - this.Height - 20
            );

            this.Paint += WelcomeScreen_Paint;
        }

        private void CacheAvatar()
        {
            try
            {
                if (UserAvatarManager.HasAvatar(odbiorcaId))
                {
                    cachedAvatar = UserAvatarManager.GetAvatarRounded(odbiorcaId, avatarSize);
                }
                else
                {
                    cachedAvatar = UserAvatarManager.GenerateDefaultAvatar(userName, odbiorcaId, avatarSize);
                }
            }
            catch
            {
                cachedAvatar = null;
            }
        }

        private void SetupTimers()
        {
            // Główny timer animacji
            animationTimer = new Timer { Interval = 30 };
            animationTimer.Tick += AnimationTimer_Tick;

            // Timer do przejścia do fade out
            closeTimer = new Timer { Interval = 2000 };
            closeTimer.Tick += (s, e) =>
            {
                closeTimer.Stop();
                animationPhase = 2; // Rozpocznij fade out
            };

            this.Shown += (s, e) =>
            {
                animationPhase = 0;
                animationTimer.Start();
            };
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            switch (animationPhase)
            {
                case 0: // Fade in
                    currentOpacity += 0.1f;
                    if (currentOpacity >= 1)
                    {
                        currentOpacity = 1;
                        animationPhase = 1;
                        closeTimer.Start();
                    }
                    break;

                case 2: // Fade out
                    currentOpacity -= 0.08f;
                    if (currentOpacity <= 0)
                    {
                        currentOpacity = 0;
                        animationTimer.Stop();
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                        return;
                    }
                    break;
            }

            this.Opacity = currentOpacity;
            this.Invalidate();
        }

        private void WelcomeScreen_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // Gradient tła
            using (var bgBrush = new LinearGradientBrush(
                new Point(0, 0), new Point(this.Width, 0),
                Color.FromArgb(55, 67, 79), Color.FromArgb(45, 57, 69)))
            {
                g.FillRectangle(bgBrush, this.ClientRectangle);
            }

            // Ramka z zaokrąglonymi rogami (efekt)
            using (var borderPen = new Pen(Color.FromArgb(100, 255, 255, 255), 2))
            {
                g.DrawRectangle(borderPen, 1, 1, this.Width - 3, this.Height - 3);
            }

            // Zielony pasek akcentowy z lewej
            using (var accentBrush = new SolidBrush(Color.FromArgb(76, 175, 80)))
            {
                g.FillRectangle(accentBrush, 0, 0, 5, this.Height);
            }

            // Avatar - z lewej strony
            int avatarX = 20;
            int avatarY = (this.Height - avatarSize) / 2;

            // Cień pod avatarem
            using (var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
            {
                g.FillEllipse(shadowBrush, avatarX + 3, avatarY + 3, avatarSize, avatarSize);
            }

            // Ramka avatara
            using (var borderPen = new Pen(Color.FromArgb(120, 255, 255, 255), 2))
            {
                g.DrawEllipse(borderPen, avatarX - 1, avatarY - 1, avatarSize + 1, avatarSize + 1);
            }

            // Avatar
            if (cachedAvatar != null)
            {
                g.DrawImage(cachedAvatar, avatarX, avatarY, avatarSize, avatarSize);
            }

            // Tekst - z prawej strony avatara
            int textX = avatarX + avatarSize + 20;
            int textY = avatarY + 5;

            // "Witaj"
            using (var font = new Font("Segoe UI", 12, FontStyle.Regular))
            using (var brush = new SolidBrush(Color.FromArgb(180, 200, 220)))
            {
                g.DrawString("Witaj", font, brush, textX, textY);
            }

            // Pełne imię użytkownika
            using (var font = new Font("Segoe UI", 16, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            {
                string displayName = userName ?? "Użytkowniku";

                // Cień tekstu
                using (var shadowBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
                {
                    g.DrawString(displayName, font, shadowBrush, textX + 1, textY + 26);
                }
                g.DrawString(displayName, font, brush, textX, textY + 25);
            }

            // Podpis "Zalogowano pomyślnie"
            using (var font = new Font("Segoe UI", 9, FontStyle.Italic))
            using (var brush = new SolidBrush(Color.FromArgb(120, 150, 120)))
            {
                g.DrawString("Zalogowano pomyślnie ✓", font, brush, textX, textY + 55);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Zwolnij zasoby
            cachedAvatar?.Dispose();
            animationTimer?.Stop();
            animationTimer?.Dispose();
            closeTimer?.Stop();
            closeTimer?.Dispose();
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Wyświetla ekran powitalny dla użytkownika (nieblokujące)
        /// </summary>
        public static void Show(string odbiorcaId, string userName)
        {
            var screen = new WelcomeScreen(odbiorcaId, userName);
            screen.Show();
        }

        /// <summary>
        /// Wyświetla ekran powitalny i czeka na zamknięcie (blokujące)
        /// </summary>
        public static void ShowAndWait(string odbiorcaId, string userName)
        {
            using (var screen = new WelcomeScreen(odbiorcaId, userName))
            {
                screen.ShowDialog();
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }
    }
}
