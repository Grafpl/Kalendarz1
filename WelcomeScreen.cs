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
        private int avatarSize = 120;
        private Image cachedAvatar = null;

        public WelcomeScreen(string odbiorcaId, string userName)
        {
            this.odbiorcaId = odbiorcaId;
            this.userName = userName;

            InitializeForm();
            CacheAvatar();
            SetupTimers();
        }

        private void InitializeForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(400, 280);
            this.BackColor = Color.FromArgb(45, 57, 69);
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Opacity = 0;
            this.DoubleBuffered = true;

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

            int centerX = this.Width / 2;

            // Gradient tła
            using (var bgBrush = new LinearGradientBrush(
                new Point(0, 0), new Point(0, this.Height),
                Color.FromArgb(55, 67, 79), Color.FromArgb(35, 47, 59)))
            {
                g.FillRectangle(bgBrush, this.ClientRectangle);
            }

            // Ramka
            using (var borderPen = new Pen(Color.FromArgb(80, 255, 255, 255), 2))
            {
                g.DrawRectangle(borderPen, 1, 1, this.Width - 3, this.Height - 3);
            }

            // Avatar
            int avatarY = 30;
            int avatarX = centerX - avatarSize / 2;

            // Cień pod avatarem
            using (var shadowBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
            {
                g.FillEllipse(shadowBrush, avatarX + 4, avatarY + 4, avatarSize, avatarSize);
            }

            // Ramka avatara
            using (var borderPen = new Pen(Color.FromArgb(120, 255, 255, 255), 3))
            {
                g.DrawEllipse(borderPen, avatarX - 2, avatarY - 2, avatarSize + 3, avatarSize + 3);
            }

            // Avatar
            if (cachedAvatar != null)
            {
                g.DrawImage(cachedAvatar, avatarX, avatarY, avatarSize, avatarSize);
            }

            // Tekst "Witaj"
            using (var font = new Font("Segoe UI", 14, FontStyle.Regular))
            using (var brush = new SolidBrush(Color.FromArgb(180, 200, 220)))
            {
                string welcomeText = "Witaj";
                var size = g.MeasureString(welcomeText, font);
                g.DrawString(welcomeText, font, brush, centerX - size.Width / 2, avatarY + avatarSize + 15);
            }

            // Pełne imię użytkownika
            using (var font = new Font("Segoe UI", 18, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            {
                string displayName = userName ?? "Użytkowniku";
                var size = g.MeasureString(displayName, font);

                // Cień tekstu
                using (var shadowBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                {
                    g.DrawString(displayName, font, shadowBrush, centerX - size.Width / 2 + 2, avatarY + avatarSize + 42);
                }
                g.DrawString(displayName, font, brush, centerX - size.Width / 2, avatarY + avatarSize + 40);
            }

            // Zielona linia akcentowa
            int lineWidth = 80;
            int lineY = avatarY + avatarSize + 80;
            using (var lineBrush = new SolidBrush(Color.FromArgb(76, 175, 80)))
            {
                g.FillRectangle(lineBrush, centerX - lineWidth / 2, lineY, lineWidth, 3);
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
