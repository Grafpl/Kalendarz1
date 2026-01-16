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
        private Timer fadeTimer;
        private Timer closeTimer;
        private float opacity = 0;
        private bool fadingIn = true;
        private string userName;
        private string odbiorcaId;
        private int avatarSize = 120;

        public WelcomeScreen(string odbiorcaId, string userName)
        {
            this.odbiorcaId = odbiorcaId;
            this.userName = userName;

            InitializeForm();
            SetupTimers();
        }

        private void InitializeForm()
        {
            // Konfiguracja formularza
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(400, 300);
            this.BackColor = Color.FromArgb(45, 57, 69);
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Opacity = 0;
            this.DoubleBuffered = true;

            // Zaokrąglone rogi
            this.Region = CreateRoundedRegion(this.Width, this.Height, 20);

            // Custom painting
            this.Paint += WelcomeScreen_Paint;
        }

        private void SetupTimers()
        {
            // Timer fade in/out
            fadeTimer = new Timer { Interval = 20 };
            fadeTimer.Tick += FadeTimer_Tick;

            // Timer do automatycznego zamknięcia (2 sekundy)
            closeTimer = new Timer { Interval = 2000 };
            closeTimer.Tick += (s, e) => {
                closeTimer.Stop();
                fadingIn = false;
                fadeTimer.Start();
            };

            this.Shown += (s, e) => {
                fadeTimer.Start();
            };
        }

        private void FadeTimer_Tick(object sender, EventArgs e)
        {
            if (fadingIn)
            {
                opacity += 0.08f;
                if (opacity >= 1)
                {
                    opacity = 1;
                    fadeTimer.Stop();
                    closeTimer.Start();
                }
            }
            else
            {
                opacity -= 0.06f;
                if (opacity <= 0)
                {
                    opacity = 0;
                    fadeTimer.Stop();
                    this.Close();
                }
            }
            this.Opacity = opacity;
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

            // Subtelna ramka
            using (var borderPen = new Pen(Color.FromArgb(80, 255, 255, 255), 2))
            {
                g.DrawRectangle(borderPen, 1, 1, this.Width - 3, this.Height - 3);
            }

            // Avatar
            int avatarY = 40;
            DrawAvatar(g, centerX - avatarSize / 2, avatarY);

            // Tekst "Witaj"
            using (var font = new Font("Segoe UI", 14, FontStyle.Regular))
            using (var brush = new SolidBrush(Color.FromArgb(180, 200, 220)))
            {
                string welcomeText = "Witaj";
                var size = g.MeasureString(welcomeText, font);
                g.DrawString(welcomeText, font, brush, centerX - size.Width / 2, avatarY + avatarSize + 15);
            }

            // Pełne imię użytkownika
            using (var font = new Font("Segoe UI", 20, FontStyle.Bold))
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

            // Animowana linia pod imieniem
            int lineWidth = 100;
            int lineY = avatarY + avatarSize + 85;
            using (var lineBrush = new LinearGradientBrush(
                new Point(centerX - lineWidth / 2, 0),
                new Point(centerX + lineWidth / 2, 0),
                Color.FromArgb(0, 76, 175, 80),
                Color.FromArgb(255, 76, 175, 80)))
            {
                // Symetryczny gradient
                ColorBlend blend = new ColorBlend(3);
                blend.Colors = new Color[] {
                    Color.FromArgb(0, 76, 175, 80),
                    Color.FromArgb(255, 76, 175, 80),
                    Color.FromArgb(0, 76, 175, 80)
                };
                blend.Positions = new float[] { 0f, 0.5f, 1f };
                lineBrush.InterpolationColors = blend;

                g.FillRectangle(lineBrush, centerX - lineWidth / 2, lineY, lineWidth, 3);
            }
        }

        private void DrawAvatar(Graphics g, int x, int y)
        {
            // Cień pod avatarem
            using (var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
            {
                g.FillEllipse(shadowBrush, x + 4, y + 4, avatarSize, avatarSize);
            }

            // Ramka avatara (jasna obwódka)
            using (var borderPen = new Pen(Color.FromArgb(100, 255, 255, 255), 3))
            {
                g.DrawEllipse(borderPen, x - 2, y - 2, avatarSize + 3, avatarSize + 3);
            }

            // Avatar
            if (UserAvatarManager.HasAvatar(odbiorcaId))
            {
                try
                {
                    using (var avatar = UserAvatarManager.GetAvatarRounded(odbiorcaId, avatarSize))
                    {
                        if (avatar != null)
                        {
                            g.DrawImage(avatar, x, y, avatarSize, avatarSize);
                            return;
                        }
                    }
                }
                catch { }
            }

            // Domyślny avatar z inicjałami
            using (var defaultAvatar = UserAvatarManager.GenerateDefaultAvatar(userName, odbiorcaId, avatarSize))
            {
                g.DrawImage(defaultAvatar, x, y, avatarSize, avatarSize);
            }
        }

        private string GetFirstName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "Użytkowniku";

            var parts = fullName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : fullName;
        }

        private Region CreateRoundedRegion(int width, int height, int radius)
        {
            using (var path = new GraphicsPath())
            {
                path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
                path.AddArc(width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
                path.AddArc(width - radius * 2, height - radius * 2, radius * 2, radius * 2, 0, 90);
                path.AddArc(0, height - radius * 2, radius * 2, radius * 2, 90, 90);
                path.CloseFigure();
                return new Region(path);
            }
        }

        /// <summary>
        /// Wyświetla ekran powitalny dla użytkownika
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
            var screen = new WelcomeScreen(odbiorcaId, userName);
            screen.ShowDialog();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED - double buffering
                return cp;
            }
        }
    }
}
