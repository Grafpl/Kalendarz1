using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    // ════════════════════════════════════════════════════════════════════════════════
    // KLASA ANIMACJI - Płynne przejścia i efekty
    // ════════════════════════════════════════════════════════════════════════════════
    public static class UIAnimator
    {
        public static void AnimateColor(Control control, Color fromColor, Color toColor, int duration = 150, Action onComplete = null)
        {
            int steps = duration / 15;
            int currentStep = 0;

            var timer = new Timer { Interval = 15 };
            timer.Tick += (s, e) => {
                currentStep++;
                float progress = (float)currentStep / steps;
                progress = EaseOutQuad(progress);

                int r = (int)(fromColor.R + (toColor.R - fromColor.R) * progress);
                int g = (int)(fromColor.G + (toColor.G - fromColor.G) * progress);
                int b = (int)(fromColor.B + (toColor.B - fromColor.B) * progress);

                control.BackColor = Color.FromArgb(
                    Math.Max(0, Math.Min(255, r)),
                    Math.Max(0, Math.Min(255, g)),
                    Math.Max(0, Math.Min(255, b)));

                if (currentStep >= steps)
                {
                    timer.Stop();
                    timer.Dispose();
                    control.BackColor = toColor;
                    onComplete?.Invoke();
                }
            };
            timer.Start();
        }

        public static void AnimateSize(Control control, Size toSize, int duration = 200)
        {
            Size fromSize = control.Size;
            int steps = duration / 15;
            int currentStep = 0;

            var timer = new Timer { Interval = 15 };
            timer.Tick += (s, e) => {
                currentStep++;
                float progress = EaseOutQuad((float)currentStep / steps);

                int w = (int)(fromSize.Width + (toSize.Width - fromSize.Width) * progress);
                int h = (int)(fromSize.Height + (toSize.Height - fromSize.Height) * progress);

                control.Size = new Size(w, h);

                if (currentStep >= steps)
                {
                    timer.Stop();
                    timer.Dispose();
                    control.Size = toSize;
                }
            };
            timer.Start();
        }

        private static float EaseOutQuad(float t) => t * (2 - t);
        private static float EaseInOutQuad(float t) => t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;
    }

    // ════════════════════════════════════════════════════════════════════════════════
    // TOAST NOTIFICATION - Eleganckie powiadomienia
    // ════════════════════════════════════════════════════════════════════════════════
    public class ToastNotification : Form
    {
        private Timer fadeTimer;
        private Timer displayTimer;
        private float opacity = 0;
        private bool fadingIn = true;

        public enum ToastType { Success, Error, Warning, Info }

        public ToastNotification(string message, ToastType type, int duration = 3000)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(320, 60);
            this.Opacity = 0;

            Color bgColor = type switch
            {
                ToastType.Success => Color.FromArgb(39, 174, 96),
                ToastType.Error => Color.FromArgb(192, 57, 43),
                ToastType.Warning => Color.FromArgb(243, 156, 18),
                ToastType.Info => Color.FromArgb(41, 128, 185),
                _ => Color.FromArgb(52, 73, 94)
            };

            string icon = type switch
            {
                ToastType.Success => "✓",
                ToastType.Error => "✕",
                ToastType.Warning => "⚠",
                ToastType.Info => "ℹ",
                _ => "•"
            };

            this.BackColor = bgColor;
            this.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = CreateRoundedRect(0, 0, Width - 1, Height - 1, 8))
                using (var brush = new SolidBrush(bgColor))
                {
                    e.Graphics.FillPath(brush, path);
                }
                // Cień
                using (var shadowBrush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
                {
                    e.Graphics.FillRectangle(shadowBrush, 4, Height - 4, Width - 8, 4);
                }
            };

            // Ikona
            var iconLabel = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(15, 12),
                Size = new Size(40, 40),
                BackColor = Color.Transparent
            };
            this.Controls.Add(iconLabel);

            // Tekst
            var textLabel = new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                Location = new Point(55, 18),
                Size = new Size(250, 30),
                BackColor = Color.Transparent
            };
            this.Controls.Add(textLabel);

            // Pozycja - prawy dolny róg
            var screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(screen.Right - this.Width - 20, screen.Bottom - this.Height - 20);

            // Animacja fade in
            fadeTimer = new Timer { Interval = 20 };
            fadeTimer.Tick += (s, e) => {
                if (fadingIn)
                {
                    opacity += 0.1f;
                    if (opacity >= 1) { opacity = 1; fadingIn = false; fadeTimer.Stop(); }
                }
                else
                {
                    opacity -= 0.05f;
                    if (opacity <= 0) { fadeTimer.Stop(); this.Close(); }
                }
                this.Opacity = opacity;
            };

            // Timer do zamknięcia
            displayTimer = new Timer { Interval = duration };
            displayTimer.Tick += (s, e) => {
                displayTimer.Stop();
                fadingIn = false;
                fadeTimer.Start();
            };

            this.Shown += (s, e) => {
                fadeTimer.Start();
                displayTimer.Start();
            };
        }

        private GraphicsPath CreateRoundedRect(int x, int y, int w, int h, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void Show(Control parent, string message, ToastType type = ToastType.Info)
        {
            var toast = new ToastNotification(message, type);
            toast.Show();
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════
    // PASEK POSTĘPU UPRAWNIEŃ
    // ════════════════════════════════════════════════════════════════════════════════
    public class PermissionProgressBar : Panel
    {
        private int totalCount = 0;
        private int enabledCount = 0;
        private Color barColor = Color.FromArgb(76, 175, 80);

        public PermissionProgressBar()
        {
            this.Height = 28;
            this.BackColor = Color.FromArgb(240, 242, 245);
            this.DoubleBuffered = true;
        }

        public void UpdateProgress(int enabled, int total)
        {
            enabledCount = enabled;
            totalCount = total;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int barHeight = 6;
            int barY = (Height - barHeight) / 2 + 4;
            int barWidth = Width - 120;

            // Tło paska
            using (var bgBrush = new SolidBrush(Color.FromArgb(220, 225, 230)))
            {
                e.Graphics.FillRectangle(bgBrush, 10, barY, barWidth, barHeight);
            }

            // Wypełnienie paska
            if (totalCount > 0)
            {
                float percent = (float)enabledCount / totalCount;
                int fillWidth = (int)(barWidth * percent);

                // Zabezpieczenie przed OutOfMemoryException gdy fillWidth <= 0
                if (fillWidth > 1)
                {
                    using (var brush = new LinearGradientBrush(
                        new Point(10, 0), new Point(10 + fillWidth, 0),
                        Color.FromArgb(76, 175, 80), Color.FromArgb(129, 199, 132)))
                    {
                        e.Graphics.FillRectangle(brush, 10, barY, fillWidth, barHeight);
                    }
                }
                else if (fillWidth == 1)
                {
                    // Dla bardzo małych wartości użyj jednolitego koloru
                    using (var brush = new SolidBrush(Color.FromArgb(76, 175, 80)))
                    {
                        e.Graphics.FillRectangle(brush, 10, barY, fillWidth, barHeight);
                    }
                }
            }

            // Tekst
            string text = totalCount > 0 ? $"{enabledCount}/{totalCount} modułów ({(enabledCount * 100 / totalCount)}%)" : "Wybierz użytkownika";
            using (var font = new Font("Segoe UI", 9))
            using (var brush = new SolidBrush(Color.FromArgb(100, 110, 120)))
            {
                e.Graphics.DrawString(text, font, brush, barWidth + 20, 6);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════
    // ANIMOWANY PRZYCISK - Profesjonalny wygląd z gradientem i cieniem
    // ════════════════════════════════════════════════════════════════════════════════
    public class AnimatedButton : Panel
    {
        private Color baseColor;
        private Color hoverColor;
        private Color pressColor;
        private bool isHovered = false;
        private bool isPressed = false;
        private float rippleSize = 0;
        private Point rippleCenter;
        private Timer rippleTimer;
        private string text;
        private Font font;

        public event EventHandler Click;
        public new string Text { get => text; set { text = value; Invalidate(); } }

        public AnimatedButton(string text, Color color)
        {
            this.text = text;
            this.baseColor = color;
            this.hoverColor = ControlPaint.Light(color, 0.2f);
            this.pressColor = ControlPaint.Dark(color, 0.15f);
            this.font = new Font("Segoe UI", 9, FontStyle.Bold);

            this.Size = new Size(text.Length * 8 + 28, 34);
            this.BackColor = baseColor;
            this.Cursor = Cursors.Hand;
            this.DoubleBuffered = true;

            this.MouseEnter += (s, e) => {
                isHovered = true;
                Invalidate();
            };

            this.MouseLeave += (s, e) => {
                isHovered = false;
                isPressed = false;
                Invalidate();
            };

            this.MouseDown += (s, e) => {
                isPressed = true;
                rippleCenter = e.Location;
                StartRipple();
                Invalidate();
            };

            this.MouseUp += (s, e) => {
                isPressed = false;
                Invalidate();
                Click?.Invoke(this, EventArgs.Empty);
            };
        }

        private void StartRipple()
        {
            rippleSize = 0;
            rippleTimer?.Stop();
            rippleTimer = new Timer { Interval = 15 };
            rippleTimer.Tick += (s, e) => {
                rippleSize += 10;
                if (rippleSize > Math.Max(Width, Height) * 2)
                {
                    rippleTimer.Stop();
                    rippleSize = 0;
                }
                Invalidate();
            };
            rippleTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Color currentColor = isPressed ? pressColor : (isHovered ? hoverColor : baseColor);
            int radius = 6;

            // Cień pod przyciskiem
            if (!isPressed)
            {
                using (var shadowPath = GetRoundedRectPath(new Rectangle(2, 3, Width - 4, Height - 4), radius))
                using (var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                {
                    e.Graphics.FillPath(shadowBrush, shadowPath);
                }
            }

            // Tło z gradientem
            using (var path = GetRoundedRectPath(new Rectangle(0, isPressed ? 1 : 0, Width - 1, Height - 2), radius))
            {
                Color topColor = ControlPaint.Light(currentColor, 0.15f);
                Color bottomColor = ControlPaint.Dark(currentColor, 0.05f);

                using (var gradientBrush = new LinearGradientBrush(
                    new Point(0, 0), new Point(0, Height),
                    topColor, bottomColor))
                {
                    e.Graphics.FillPath(gradientBrush, path);
                }

                // Subtelna ramka
                using (var borderPen = new Pen(Color.FromArgb(60, 0, 0, 0), 1))
                {
                    e.Graphics.DrawPath(borderPen, path);
                }

                // Highlight na górze (efekt szklany)
                if (!isPressed)
                {
                    using (var highlightBrush = new LinearGradientBrush(
                        new Point(0, 0), new Point(0, Height / 3),
                        Color.FromArgb(50, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)))
                    {
                        e.Graphics.FillPath(highlightBrush, path);
                    }
                }
            }

            // Efekt ripple
            if (rippleSize > 0)
            {
                using (var brush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                {
                    e.Graphics.FillEllipse(brush,
                        rippleCenter.X - rippleSize / 2,
                        rippleCenter.Y - rippleSize / 2,
                        rippleSize, rippleSize);
                }
            }

            // Tekst z cieniem
            int yOffset = isPressed ? 1 : 0;
            using (var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
            using (var textBrush = new SolidBrush(Color.White))
            {
                var size = e.Graphics.MeasureString(text, font);
                float x = (Width - size.Width) / 2;
                float y = (Height - size.Height) / 2 + yOffset;

                // Cień tekstu
                e.Graphics.DrawString(text, font, shadowBrush, x + 1, y + 1);
                // Tekst
                e.Graphics.DrawString(text, font, textBrush, x, y);
            }
        }

        private GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override CreateParams CreateParams
        {
            get {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED - double buffering
                return cp;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════
    // GŁÓWNY FORMULARZ
    // ════════════════════════════════════════════════════════════════════════════════
    public partial class AdminPermissionsForm : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string handelConnectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private Panel topToolbar;
        private Panel leftPanel;
        private Panel rightPanel;
        private Panel permissionsPanel;
        private TextBox searchBox;
        private Label usersCountLabel;
        private Label selectedUserLabel;
        private string selectedUserId;
        private FlowLayoutPanel permissionsFlowPanel;
        private FlowLayoutPanel usersCardsPanel;
        private Panel selectedUserCard;
        private List<UserInfo> allUsers = new List<UserInfo>();
        private Dictionary<string, List<CheckBox>> categoryCheckboxes = new Dictionary<string, List<CheckBox>>();
        private Dictionary<string, CheckBox> categoryHeaders = new Dictionary<string, CheckBox>();
        private PermissionProgressBar progressBar;
        private string copiedPermissions = null; // Skopiowane uprawnienia
        private FlowLayoutPanel handlowcyAssignPanel; // Panel przypisanych handlowcow

        // Klasa do przechowywania danych użytkownika
        private class UserInfo
        {
            public string ID { get; set; }
            public string Name { get; set; }
            public Panel Card { get; set; }
        }

        // Kolory działów - zsynchronizowane z Menu.cs
        private static class DepartmentColors
        {
            public static readonly Color Zakupy = Color.FromArgb(46, 125, 50);      // Zielony
            public static readonly Color Produkcja = Color.FromArgb(230, 81, 0);    // Pomarańczowy
            public static readonly Color Sprzedaz = Color.FromArgb(25, 118, 210);   // Niebieski
            public static readonly Color Planowanie = Color.FromArgb(74, 20, 140);  // Fioletowy
            public static readonly Color Opakowania = Color.FromArgb(0, 96, 100);   // Turkusowy
            public static readonly Color Finanse = Color.FromArgb(69, 90, 100);     // Szaroniebieski
            public static readonly Color Administracja = Color.FromArgb(183, 28, 28); // Czerwony
        }

        private static class Colors
        {
            public static readonly Color Primary = Color.FromArgb(45, 57, 69);
            public static readonly Color PrimaryLight = Color.FromArgb(236, 239, 241);
            public static readonly Color TextDark = Color.FromArgb(44, 62, 80);
            public static readonly Color TextGray = Color.FromArgb(127, 140, 141);
            public static readonly Color Border = Color.FromArgb(189, 195, 199);
            public static readonly Color Background = Color.FromArgb(245, 247, 249);
            public static readonly Color Success = Color.FromArgb(39, 174, 96);
            public static readonly Color Danger = Color.FromArgb(231, 76, 60);
            public static readonly Color Warning = Color.FromArgb(243, 156, 18);
            public static readonly Color RowAlt = Color.FromArgb(250, 251, 252);
            public static readonly Color RowHover = Color.FromArgb(232, 245, 233);
        }

        public AdminPermissionsForm()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            InitializeCustomComponents();
            UserHandlowcyManager.CreateTableIfNotExists();
            LoadUsers();
        }

        private void InitializeComponent()
        {
            this.Text = "Panel Administracyjny - Zarządzanie Uprawnieniami";
            this.Size = new Size(1700, 950);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Colors.Background;
            this.MinimumSize = new Size(1400, 800);
        }

        private void InitializeCustomComponents()
        {
            // Włącz double buffering dla płynnych animacji
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            // ═══════════════════════════════════════════════════════════════════════════
            // TOP TOOLBAR - z gradientem i cieniem
            // ═══════════════════════════════════════════════════════════════════════════
            topToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Colors.Primary
            };
            topToolbar.Paint += (s, e) => {
                // Gradient
                using (var brush = new LinearGradientBrush(
                    new Point(0, 0), new Point(0, topToolbar.Height),
                    Color.FromArgb(55, 67, 79), Color.FromArgb(35, 47, 59)))
                {
                    e.Graphics.FillRectangle(brush, topToolbar.ClientRectangle);
                }
                // Cień na dole
                using (var pen = new Pen(Color.FromArgb(60, 0, 0, 0), 2))
                {
                    e.Graphics.DrawLine(pen, 0, topToolbar.Height - 1, topToolbar.Width, topToolbar.Height - 1);
                }
            };

            // Wyszukiwanie użytkowników - stylowe
            var searchContainer = new Panel
            {
                Location = new Point(10, 10),
                Size = new Size(200, 32),
                BackColor = Color.FromArgb(60, 72, 84)
            };
            searchContainer.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = CreateRoundedRectangle(0, 0, searchContainer.Width - 1, searchContainer.Height - 1, 6))
                using (var brush = new SolidBrush(Color.FromArgb(60, 72, 84)))
                {
                    e.Graphics.FillPath(brush, path);
                }
            };
            topToolbar.Controls.Add(searchContainer);

            searchBox = new TextBox
            {
                Location = new Point(8, 6),
                Size = new Size(184, 20),
                Font = new Font("Segoe UI", 10),
                PlaceholderText = "🔍 Szukaj...",
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(60, 72, 84),
                ForeColor = Color.White
            };
            searchBox.TextChanged += SearchBox_TextChanged;
            searchContainer.Controls.Add(searchBox);

            int btnX = 220;

            // Przyciski z animacjami
            var saveBtn = CreateAnimatedButton("💾 Zapisz", Colors.Success, ref btnX);
            saveBtn.Click += SaveButton_Click;
            topToolbar.Controls.Add(saveBtn);

            var selectAllBtn = CreateAnimatedButton("✓ Wszystko", Color.FromArgb(76, 175, 80), ref btnX);
            selectAllBtn.Click += (s, e) => SetAllPermissions(true);
            topToolbar.Controls.Add(selectAllBtn);

            var selectNoneBtn = CreateAnimatedButton("✗ Nic", Color.FromArgb(192, 57, 43), ref btnX);
            selectNoneBtn.Click += (s, e) => SetAllPermissions(false);
            topToolbar.Controls.Add(selectNoneBtn);

            btnX += 15;

            var addUserBtn = CreateAnimatedButton("➕ Nowy", Color.FromArgb(41, 128, 185), ref btnX);
            addUserBtn.Click += AddUserButton_Click;
            topToolbar.Controls.Add(addUserBtn);

            var deleteUserBtn = CreateAnimatedButton("🗑 Usuń", Color.FromArgb(192, 57, 43), ref btnX);
            deleteUserBtn.Click += DeleteUserButton_Click;
            topToolbar.Controls.Add(deleteUserBtn);

            btnX += 15;

            var handlowcyBtn = CreateAnimatedButton("👔 Handlowcy", Color.FromArgb(142, 68, 173), ref btnX);
            handlowcyBtn.Click += ManageHandlowcyButton_Click;
            topToolbar.Controls.Add(handlowcyBtn);

            var contactBtn = CreateAnimatedButton("📞 Kontakt", Color.FromArgb(22, 160, 133), ref btnX);
            contactBtn.Click += EditContactButton_Click;
            topToolbar.Controls.Add(contactBtn);

            var avatarBtn = CreateAnimatedButton("🖼 Avatar", Color.FromArgb(233, 30, 99), ref btnX);
            avatarBtn.Click += ImportAvatarButton_Click;
            topToolbar.Controls.Add(avatarBtn);

            btnX += 15;

            // Kopiowanie uprawnień
            var copyBtn = CreateAnimatedButton("📋 Kopiuj", Color.FromArgb(52, 73, 94), ref btnX);
            copyBtn.Click += CopyPermissions_Click;
            topToolbar.Controls.Add(copyBtn);

            var pasteBtn = CreateAnimatedButton("📥 Wklej", Color.FromArgb(52, 73, 94), ref btnX);
            pasteBtn.Click += PastePermissions_Click;
            topToolbar.Controls.Add(pasteBtn);

            // Szablony
            var presetsBtn = CreateAnimatedButton("⚡ Szablony", Color.FromArgb(155, 89, 182), ref btnX);
            presetsBtn.Click += ShowPresets_Click;
            topToolbar.Controls.Add(presetsBtn);

            btnX += 15;

            // Przypisywanie klientów do handlowców
            var przydzielKlientowBtn = CreateAnimatedButton("📊 Przypisz klientów", Color.FromArgb(0, 150, 136), ref btnX);
            przydzielKlientowBtn.Click += PrzydzielKlientowButton_Click;
            topToolbar.Controls.Add(przydzielKlientowBtn);

            // Wybrany użytkownik - elegancki badge
            selectedUserLabel = new Label
            {
                Text = "👤 Wybierz użytkownika",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 190, 200),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            selectedUserLabel.Location = new Point(this.Width - selectedUserLabel.PreferredWidth - 25, 16);
            topToolbar.Controls.Add(selectedUserLabel);

            // ═══════════════════════════════════════════════════════════════════════════
            // LEFT PANEL - Lista użytkowników (czysta, bez nagłówków)
            // ═══════════════════════════════════════════════════════════════════════════
            leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 240,
                BackColor = Color.White,
                Padding = new Padding(0)
            };
            leftPanel.Paint += (s, e) => {
                using (var pen = new Pen(Colors.Border))
                    e.Graphics.DrawLine(pen, leftPanel.Width - 1, 0, leftPanel.Width - 1, leftPanel.Height);
            };

            // Panel z kartami użytkowników - bezpośrednio
            usersCardsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(4, 4, 4, 4)
            };
            leftPanel.Controls.Add(usersCardsPanel);

            // Pasek statusu na dole - minimalny
            var statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                BackColor = Color.FromArgb(240, 242, 245)
            };

            usersCountLabel = new Label
            {
                Text = "...",
                Font = new Font("Segoe UI", 8),
                ForeColor = Colors.TextGray,
                Location = new Point(8, 4),
                AutoSize = true
            };
            statusPanel.Controls.Add(usersCountLabel);
            leftPanel.Controls.Add(statusPanel);

            // ═══════════════════════════════════════════════════════════════════════════
            // RIGHT PANEL - Uprawnienia modułów
            // ═══════════════════════════════════════════════════════════════════════════
            rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Colors.Background,
                Padding = new Padding(0)
            };

            // Panel z uprawnieniami - scrollowalny
            permissionsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Colors.Background,
                Padding = new Padding(8)
            };

            permissionsFlowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Colors.Background,
                Padding = new Padding(0)
            };

            permissionsPanel.Controls.Add(permissionsFlowPanel);
            // Pasek postępu uprawnień
            progressBar = new PermissionProgressBar
            {
                Dock = DockStyle.Bottom
            };
            rightPanel.Controls.Add(progressBar);

            rightPanel.Controls.Add(permissionsPanel);

            // Dodaj kontrolki do formularza
            this.Controls.Add(rightPanel);
            this.Controls.Add(leftPanel);
            this.Controls.Add(topToolbar);

            // Obsługa resize dla pozycji labela
            this.Resize += (s, e) => {
                selectedUserLabel.Location = new Point(this.Width - selectedUserLabel.PreferredWidth - 30, 12);
            };
        }

        private AnimatedButton CreateAnimatedButton(string text, Color color, ref int x)
        {
            var btn = new AnimatedButton(text, color)
            {
                Location = new Point(x, 10)
            };
            x += btn.Width + 6;
            return btn;
        }

        private GraphicsPath CreateRoundedRectangle(int x, int y, int width, int height, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(x, y, radius * 2, radius * 2, 180, 90);
            path.AddArc(x + width - radius * 2, y, radius * 2, radius * 2, 270, 90);
            path.AddArc(x + width - radius * 2, y + height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(x, y + height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }


        private void BuildPermissionsUI()
        {
            permissionsFlowPanel.Controls.Clear();
            categoryCheckboxes.Clear();
            categoryHeaders.Clear();

            if (string.IsNullOrEmpty(selectedUserId)) return;

            var modules = GetModulesList();
            var accessMap = GetAccessMap();
            string accessString = "";

            // Pobierz aktualny access string
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT Access FROM operators WHERE ID = @userId";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", selectedUserId);
                        var result = cmd.ExecuteScalar();
                        if (result != null) accessString = result.ToString();
                    }
                }
            }
            catch { }

            // Szerokość dla czterech kolumn
            int totalWidth = permissionsPanel.ClientSize.Width - 30;
            int columnWidth = (totalWidth - 30) / 4; // 30px gap między kolumnami
            if (columnWidth < 220) columnWidth = 220;

            // Grupuj moduły według kategorii
            var groupedModules = modules.GroupBy(m => m.Category).OrderBy(g => GetCategoryOrder(g.Key));

            foreach (var group in groupedModules)
            {
                string category = group.Key;
                Color categoryColor = GetCategoryColor(category);

                // ═══════════════════════════════════════════════════════════════════════
                // NAGŁÓWEK KATEGORII - kompaktowy
                // ═══════════════════════════════════════════════════════════════════════
                var categoryPanel = new Panel
                {
                    Width = totalWidth,
                    Height = 28,
                    BackColor = categoryColor,
                    Margin = new Padding(0, 6, 0, 2)
                };

                var categoryCheckbox = new CheckBox
                {
                    Text = $"  {category}",
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(6, 4),
                    AutoSize = true,
                    Cursor = Cursors.Hand,
                    BackColor = Color.Transparent
                };
                categoryCheckbox.CheckedChanged += (s, e) => CategoryHeader_CheckedChanged(category, categoryCheckbox.Checked);
                categoryPanel.Controls.Add(categoryCheckbox);
                categoryHeaders[category] = categoryCheckbox;

                // Licznik uprawnień w kategorii
                var countLabel = new Label
                {
                    Text = $"{group.Count()}",
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = Color.FromArgb(255, 255, 255, 180),
                    AutoSize = true,
                    BackColor = Color.Transparent
                };
                countLabel.Location = new Point(totalWidth - countLabel.PreferredWidth - 12, 5);
                categoryPanel.Controls.Add(countLabel);

                categoryPanel.Click += (s, e) => categoryCheckbox.Checked = !categoryCheckbox.Checked;
                permissionsFlowPanel.Controls.Add(categoryPanel);

                // ═══════════════════════════════════════════════════════════════════════
                // KONTENER NA MODUŁY W CZTERECH KOLUMNACH
                // ═══════════════════════════════════════════════════════════════════════
                var modulesContainer = new FlowLayoutPanel
                {
                    Width = totalWidth,
                    AutoSize = true,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = true,
                    BackColor = Color.White,
                    Padding = new Padding(0),
                    Margin = new Padding(0, 0, 0, 4)
                };

                categoryCheckboxes[category] = new List<CheckBox>();
                int moduleIndex = 0;
                var modulesList = group.ToList();

                foreach (var module in modulesList)
                {
                    bool hasAccess = false;
                    var position = accessMap.FirstOrDefault(x => x.Value == module.Key).Key;
                    if (position >= 0 && position < accessString.Length)
                        hasAccess = accessString[position] == '1';

                    // Panel pojedynczego modułu - kompaktowy
                    var modulePanel = new Panel
                    {
                        Width = columnWidth - 6,
                        Height = 32,
                        BackColor = moduleIndex % 2 == 0 ? Color.White : Color.FromArgb(250, 251, 252),
                        Margin = new Padding(2, 1, 2, 1),
                        Cursor = Cursors.Hand
                    };

                    // Pasek koloru po lewej
                    var colorBar = new Panel
                    {
                        Width = 3,
                        Height = 32,
                        BackColor = categoryColor,
                        Location = new Point(0, 0)
                    };
                    modulePanel.Controls.Add(colorBar);

                    // Ikona emoji - mniejsza
                    var iconLabel = new Label
                    {
                        Text = module.Icon,
                        Font = new Font("Segoe UI Emoji", 12),
                        ForeColor = categoryColor,
                        Location = new Point(6, 5),
                        Size = new Size(24, 24),
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    modulePanel.Controls.Add(iconLabel);

                    // Nazwa modułu - tylko nazwa, bez opisu dla kompaktowości
                    var nameLabel = new Label
                    {
                        Text = module.DisplayName,
                        Font = new Font("Segoe UI", 9),
                        ForeColor = Colors.TextDark,
                        Location = new Point(32, 7),
                        Size = new Size(columnWidth - 80, 18),
                        AutoEllipsis = true
                    };
                    modulePanel.Controls.Add(nameLabel);

                    // Checkbox dostępu - przesunięty
                    var accessCheckbox = new CheckBox
                    {
                        Checked = hasAccess,
                        Location = new Point(columnWidth - 32, 6),
                        Size = new Size(20, 20),
                        Cursor = Cursors.Hand,
                        Tag = module.Key
                    };
                    accessCheckbox.CheckedChanged += (s, e) => { UpdateCategoryHeaderState(category); UpdateProgressBar(); };
                    modulePanel.Controls.Add(accessCheckbox);
                    categoryCheckboxes[category].Add(accessCheckbox);

                    // Hover effect
                    Color normalColor = moduleIndex % 2 == 0 ? Color.White : Color.FromArgb(250, 251, 252);
                    modulePanel.MouseEnter += (s, e) => modulePanel.BackColor = Color.FromArgb(232, 245, 233);
                    modulePanel.MouseLeave += (s, e) => modulePanel.BackColor = normalColor;

                    // Kliknięcie przełącza checkbox
                    Action toggleCheckbox = () => accessCheckbox.Checked = !accessCheckbox.Checked;
                    modulePanel.Click += (s, e) => toggleCheckbox();
                    nameLabel.Click += (s, e) => toggleCheckbox();
                    iconLabel.Click += (s, e) => toggleCheckbox();
                    colorBar.Click += (s, e) => toggleCheckbox();

                    modulesContainer.Controls.Add(modulePanel);
                    moduleIndex++;
                }

                permissionsFlowPanel.Controls.Add(modulesContainer);
                UpdateCategoryHeaderState(category);
            }

            // ═══════════════════════════════════════════════════════════════════════
            // SEKCJA: POWIĄZANIE Z HANDLOWCEM SYMFONIA
            // ═══════════════════════════════════════════════════════════════════════
            var handlowcyHeaderPanel = new Panel
            {
                Width = totalWidth,
                Height = 28,
                BackColor = Color.FromArgb(142, 68, 173),
                Margin = new Padding(0, 12, 0, 2)
            };
            var handlowcyHeaderLabel = new Label
            {
                Text = "  👔 Powiązanie z handlowcem Symfonia",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(6, 4),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            handlowcyHeaderPanel.Controls.Add(handlowcyHeaderLabel);
            permissionsFlowPanel.Controls.Add(handlowcyHeaderPanel);

            // Kontener na przypisanych handlowcow + combobox
            var handlowcyContainer = new Panel
            {
                Width = totalWidth,
                AutoSize = true,
                MinimumSize = new System.Drawing.Size(totalWidth, 60),
                BackColor = Color.White,
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 0, 4)
            };

            // Panel z przypisanymi handlowcami (flow)
            handlowcyAssignPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 6),
                MinimumSize = new System.Drawing.Size(totalWidth - 20, 10)
            };
            handlowcyContainer.Controls.Add(handlowcyAssignPanel);

            // Wiersz dodawania: ComboBox + przycisk
            var addRowPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 4, 0, 0)
            };

            var cmbHandlowcy = new ComboBox
            {
                Width = 280,
                Location = new Point(0, 4),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9),
                FlatStyle = FlatStyle.Flat
            };
            addRowPanel.Controls.Add(cmbHandlowcy);

            var btnDodajHandlowca = new Button
            {
                Text = "➕ Przypisz",
                Location = new Point(290, 2),
                Width = 100,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDodajHandlowca.FlatAppearance.BorderSize = 0;
            addRowPanel.Controls.Add(btnDodajHandlowca);

            handlowcyContainer.Controls.Add(addRowPanel);
            // Kolejnosc: addRowPanel na dole, handlowcyAssignPanel na gorze (Dock.Top)
            handlowcyContainer.Controls.SetChildIndex(handlowcyAssignPanel, 0);
            handlowcyContainer.Controls.SetChildIndex(addRowPanel, 1);

            permissionsFlowPanel.Controls.Add(handlowcyContainer);

            // Wypelnij combo dostepnymi handlowcami
            try
            {
                var allHandlowcy = UserHandlowcyManager.GetAvailableHandlowcy();
                var assignedHandlowcy = UserHandlowcyManager.GetUserHandlowcy(selectedUserId);

                cmbHandlowcy.Items.Clear();
                foreach (var h in allHandlowcy.Where(a => !assignedHandlowcy.Contains(a)))
                    cmbHandlowcy.Items.Add(h);
                if (cmbHandlowcy.Items.Count > 0)
                    cmbHandlowcy.SelectedIndex = 0;

                // Wyswietl przypisanych
                OdswiezHandlowcyChips(assignedHandlowcy, cmbHandlowcy);
            }
            catch { }

            // Przycisk dodaj
            btnDodajHandlowca.Click += (s, ev) =>
            {
                if (cmbHandlowcy.SelectedItem == null) return;
                var handlowiec = cmbHandlowcy.SelectedItem.ToString();
                if (UserHandlowcyManager.AddHandlowiecToUser(selectedUserId, handlowiec, Environment.UserName))
                {
                    cmbHandlowcy.Items.Remove(handlowiec);
                    if (cmbHandlowcy.Items.Count > 0) cmbHandlowcy.SelectedIndex = 0;
                    var assigned = UserHandlowcyManager.GetUserHandlowcy(selectedUserId);
                    OdswiezHandlowcyChips(assigned, cmbHandlowcy);
                }
            };

            // Aktualizuj pasek postępu
            UpdateProgressBar();
        }

        private void OdswiezHandlowcyChips(List<string> assigned, ComboBox cmbHandlowcy)
        {
            handlowcyAssignPanel.Controls.Clear();

            if (assigned.Count == 0)
            {
                var emptyLabel = new Label
                {
                    Text = "Brak przypisanych handlowców",
                    Font = new Font("Segoe UI", 9, FontStyle.Italic),
                    ForeColor = Color.Gray,
                    AutoSize = true,
                    Margin = new Padding(2)
                };
                handlowcyAssignPanel.Controls.Add(emptyLabel);
                return;
            }

            foreach (var h in assigned)
            {
                var chip = new Panel
                {
                    Height = 28,
                    AutoSize = false,
                    BackColor = Color.FromArgb(243, 229, 245),
                    Margin = new Padding(2),
                    Cursor = Cursors.Default
                };

                var chipLabel = new Label
                {
                    Text = $"👔 {h}",
                    Font = new Font("Segoe UI", 9),
                    ForeColor = Color.FromArgb(74, 20, 140),
                    Location = new Point(4, 4),
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Default
                };
                chip.Controls.Add(chipLabel);

                var btnRemove = new Label
                {
                    Text = "✕",
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = Color.FromArgb(192, 57, 43),
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0),
                    Tag = h
                };
                btnRemove.Location = new Point(chipLabel.PreferredWidth + 10, 4);
                chip.Width = chipLabel.PreferredWidth + btnRemove.PreferredWidth + 18;

                btnRemove.Click += (s, ev) =>
                {
                    var handlowiec = btnRemove.Tag.ToString();
                    if (UserHandlowcyManager.RemoveHandlowiecFromUser(selectedUserId, handlowiec))
                    {
                        cmbHandlowcy.Items.Add(handlowiec);
                        var refreshed = UserHandlowcyManager.GetUserHandlowcy(selectedUserId);
                        OdswiezHandlowcyChips(refreshed, cmbHandlowcy);
                    }
                };
                btnRemove.MouseEnter += (s, ev) => btnRemove.ForeColor = Color.Red;
                btnRemove.MouseLeave += (s, ev) => btnRemove.ForeColor = Color.FromArgb(192, 57, 43);

                chip.Controls.Add(btnRemove);
                handlowcyAssignPanel.Controls.Add(chip);
            }
        }

        private void CategoryHeader_CheckedChanged(string category, bool isChecked)
        {
            if (!categoryCheckboxes.ContainsKey(category)) return;

            foreach (var checkbox in categoryCheckboxes[category])
            {
                checkbox.Checked = isChecked;
            }
        }

        private void UpdateCategoryHeaderState(string category)
        {
            if (!categoryCheckboxes.ContainsKey(category) || !categoryHeaders.ContainsKey(category)) return;

            var checkboxes = categoryCheckboxes[category];
            var header = categoryHeaders[category];

            int checkedCount = checkboxes.Count(c => c.Checked);

            // Tymczasowo odłącz event, żeby nie wywoływać CategoryHeader_CheckedChanged
            header.CheckedChanged -= (s, e) => CategoryHeader_CheckedChanged(category, header.Checked);
            header.Checked = checkedCount == checkboxes.Count && checkboxes.Count > 0;
            header.CheckedChanged += (s, e) => CategoryHeader_CheckedChanged(category, header.Checked);
        }

        private Color GetCategoryColor(string category)
        {
            switch (category)
            {
                case "Zaopatrzenie i Zakupy": return DepartmentColors.Zakupy;
                case "Produkcja i Magazyn": return DepartmentColors.Produkcja;
                case "Sprzedaż i CRM": return DepartmentColors.Sprzedaz;
                case "Planowanie i Analizy": return DepartmentColors.Planowanie;
                case "Opakowania i Transport": return DepartmentColors.Opakowania;
                case "Finanse i Zarządzanie": return DepartmentColors.Finanse;
                case "Administracja Systemu": return DepartmentColors.Administracja;
                default: return Colors.TextGray;
            }
        }

        private int GetCategoryOrder(string category)
        {
            switch (category)
            {
                case "Zaopatrzenie i Zakupy": return 1;
                case "Produkcja i Magazyn": return 2;
                case "Sprzedaż i CRM": return 3;
                case "Planowanie i Analizy": return 4;
                case "Opakowania i Transport": return 5;
                case "Finanse i Zarządzanie": return 6;
                case "Administracja Systemu": return 7;
                default: return 99;
            }
        }

        private void LoadUsers()
        {
            try
            {
                allUsers.Clear();
                usersCardsPanel.Controls.Clear();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT ID, Name FROM operators ORDER BY Name";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string id = reader["ID"]?.ToString() ?? "";
                            string name = reader["Name"]?.ToString() ?? "";

                            var userInfo = new UserInfo { ID = id, Name = name };
                            var card = CreateUserCard(userInfo);
                            userInfo.Card = card;
                            allUsers.Add(userInfo);
                            usersCardsPanel.Controls.Add(card);
                        }
                    }
                }

                usersCountLabel.Text = $"Użytkowników: {allUsers.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania użytkowników:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Panel CreateUserCard(UserInfo user)
        {
            int cardWidth = usersCardsPanel.ClientSize.Width - 15;
            if (cardWidth < 210) cardWidth = 210;

            var card = new Panel
            {
                Width = cardWidth,
                Height = 40,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 2),
                Cursor = Cursors.Hand,
                Tag = user
            };

            // Avatar - zaimportowany lub domyślny z inicjałami
            string initials = GetInitials(user.Name);
            Color avatarColor = GetAvatarColor(user.ID);
            string currentUserId = user.ID; // Lokalna kopia dla lambda

            var avatarPanel = new Panel
            {
                Size = new Size(32, 32),
                Location = new Point(4, 4),
                BackColor = Color.Transparent,
                Tag = "avatar" // Tag dla RefreshUserCard
            };
            avatarPanel.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // Sprawdź czy jest zaimportowany avatar
                if (UserAvatarManager.HasAvatar(currentUserId))
                {
                    try
                    {
                        using (var avatar = UserAvatarManager.GetAvatarRounded(currentUserId, 32))
                        {
                            if (avatar != null)
                            {
                                e.Graphics.DrawImage(avatar, 0, 0, 32, 32);
                                return;
                            }
                        }
                    }
                    catch { }
                }

                // Domyślny avatar z inicjałami
                using (SolidBrush brush = new SolidBrush(avatarColor))
                    e.Graphics.FillEllipse(brush, 0, 0, 31, 31);
                using (Font font = new Font("Segoe UI", 10, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                {
                    var size = e.Graphics.MeasureString(initials, font);
                    e.Graphics.DrawString(initials, font, textBrush,
                        (32 - size.Width) / 2, (32 - size.Height) / 2);
                }
            };
            card.Controls.Add(avatarPanel);

            // Nazwa użytkownika
            var nameLabel = new Label
            {
                Text = user.Name,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Colors.TextDark,
                Location = new Point(40, 4),
                Size = new Size(cardWidth - 50, 18),
                AutoEllipsis = true
            };
            card.Controls.Add(nameLabel);

            // ID użytkownika - mniejszy
            var idLabel = new Label
            {
                Text = $"ID: {user.ID}",
                Font = new Font("Segoe UI", 7),
                ForeColor = Colors.TextGray,
                Location = new Point(40, 21),
                AutoSize = true
            };
            card.Controls.Add(idLabel);

            // Hover z animacją
            Color normalColor = Color.White;
            Color hoverColor = Color.FromArgb(240, 248, 255);
            Color selectedColor = Color.FromArgb(200, 230, 201);

            Action<bool> setHover = (hover) => {
                if (card != selectedUserCard)
                {
                    Color targetColor = hover ? hoverColor : normalColor;
                    UIAnimator.AnimateColor(card, card.BackColor, targetColor, 80);
                }
            };

            card.MouseEnter += (s, e) => setHover(true);
            card.MouseLeave += (s, e) => setHover(false);
            avatarPanel.MouseEnter += (s, e) => setHover(true);
            avatarPanel.MouseLeave += (s, e) => setHover(false);
            nameLabel.MouseEnter += (s, e) => setHover(true);
            nameLabel.MouseLeave += (s, e) => setHover(false);
            idLabel.MouseEnter += (s, e) => setHover(true);
            idLabel.MouseLeave += (s, e) => setHover(false);

            Action selectCard = () => SelectUserCard(card, user);
            card.Click += (s, e) => selectCard();
            avatarPanel.Click += (s, e) => selectCard();
            nameLabel.Click += (s, e) => selectCard();
            idLabel.Click += (s, e) => selectCard();

            return card;
        }

        private void SelectUserCard(Panel card, UserInfo user)
        {
            // Odznacz poprzednią kartę z animacją
            if (selectedUserCard != null)
            {
                UIAnimator.AnimateColor(selectedUserCard, selectedUserCard.BackColor, Color.White, 120);
            }

            // Zaznacz nową kartę z animacją
            selectedUserCard = card;
            UIAnimator.AnimateColor(card, card.BackColor, Color.FromArgb(200, 230, 201), 150);

            selectedUserId = user.ID;
            selectedUserLabel.Text = $"👤 {user.Name} (ID: {user.ID})";
            selectedUserLabel.ForeColor = Color.White;
            // Aktualizuj pozycję labela
            selectedUserLabel.Location = new Point(this.Width - selectedUserLabel.PreferredWidth - 30, 16);
            BuildPermissionsUI();
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private Color GetAvatarColor(string id)
        {
            // Generuj kolor na podstawie ID
            int hash = id.GetHashCode();
            Color[] colors = {
                Color.FromArgb(46, 125, 50),   // Zielony
                Color.FromArgb(25, 118, 210),  // Niebieski
                Color.FromArgb(156, 39, 176),  // Fioletowy
                Color.FromArgb(230, 81, 0),    // Pomarańczowy
                Color.FromArgb(0, 137, 123),   // Teal
                Color.FromArgb(194, 24, 91),   // Różowy
                Color.FromArgb(69, 90, 100),   // Szary
                Color.FromArgb(121, 85, 72)    // Brązowy
            };
            return colors[Math.Abs(hash) % colors.Length];
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            string filter = searchBox.Text.Trim().ToLower();

            foreach (var user in allUsers)
            {
                bool visible = string.IsNullOrEmpty(filter) ||
                               user.ID.ToLower().Contains(filter) ||
                               user.Name.ToLower().Contains(filter);
                user.Card.Visible = visible;
            }
        }

        private void SetAllPermissions(bool value)
        {
            foreach (var categoryList in categoryCheckboxes.Values)
            {
                foreach (var checkbox in categoryList)
                {
                    checkbox.Checked = value;
                }
            }
        }

        private void InvertPermissions_Click(object sender, EventArgs e)
        {
            foreach (var categoryList in categoryCheckboxes.Values)
            {
                foreach (var checkbox in categoryList)
                {
                    checkbox.Checked = !checkbox.Checked;
                }
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(selectedUserId))
            {
                ToastNotification.Show(this, "Wybierz użytkownika", ToastNotification.ToastType.Warning);
                return;
            }

            try
            {
                char[] accessArray = new char[50];
                for (int i = 0; i < 50; i++) accessArray[i] = '0';

                var accessMap = GetAccessMap();

                foreach (var categoryList in categoryCheckboxes.Values)
                {
                    foreach (var checkbox in categoryList)
                    {
                        string moduleKey = checkbox.Tag?.ToString();
                        if (!string.IsNullOrEmpty(moduleKey) && checkbox.Checked)
                        {
                            var position = accessMap.FirstOrDefault(x => x.Value == moduleKey).Key;
                            if (position >= 0 && position < 50)
                            {
                                accessArray[position] = '1';
                            }
                        }
                    }
                }

                string newAccessString = new string(accessArray);

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "UPDATE operators SET Access = @access WHERE ID = @userId";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@access", newAccessString);
                        cmd.Parameters.AddWithValue("@userId", selectedUserId);
                        cmd.ExecuteNonQuery();
                    }
                }

                ToastNotification.Show(this, "Uprawnienia zapisane!", ToastNotification.ToastType.Success);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania uprawnień:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // KOPIOWANIE UPRAWNIEŃ
        // ════════════════════════════════════════════════════════════════════════════════
        private void CopyPermissions_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(selectedUserId))
            {
                ToastNotification.Show(this, "Wybierz użytkownika", ToastNotification.ToastType.Warning);
                return;
            }

            // Pobierz aktualne uprawnienia
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT Access FROM operators WHERE ID = @userId";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", selectedUserId);
                        var result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            copiedPermissions = result.ToString();
                            var userName = allUsers.FirstOrDefault(u => u.ID == selectedUserId)?.Name ?? "użytkownika";
                            ToastNotification.Show(this, $"Skopiowano uprawnienia {userName}", ToastNotification.ToastType.Info);
                        }
                    }
                }
            }
            catch { }
        }

        private void PastePermissions_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(selectedUserId))
            {
                ToastNotification.Show(this, "Wybierz użytkownika", ToastNotification.ToastType.Warning);
                return;
            }

            if (string.IsNullOrEmpty(copiedPermissions))
            {
                ToastNotification.Show(this, "Najpierw skopiuj uprawnienia", ToastNotification.ToastType.Warning);
                return;
            }

            // Zastosuj skopiowane uprawnienia
            var accessMap = GetAccessMap();
            foreach (var categoryList in categoryCheckboxes.Values)
            {
                foreach (var checkbox in categoryList)
                {
                    string moduleKey = checkbox.Tag?.ToString();
                    if (!string.IsNullOrEmpty(moduleKey))
                    {
                        var position = accessMap.FirstOrDefault(x => x.Value == moduleKey).Key;
                        if (position >= 0 && position < copiedPermissions.Length)
                        {
                            checkbox.Checked = copiedPermissions[position] == '1';
                        }
                    }
                }
            }

            UpdateProgressBar();
            ToastNotification.Show(this, "Uprawnienia wklejone!", ToastNotification.ToastType.Success);
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // SZABLONY UPRAWNIEŃ
        // ════════════════════════════════════════════════════════════════════════════════
        private void ShowPresets_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(selectedUserId))
            {
                ToastNotification.Show(this, "Wybierz użytkownika", ToastNotification.ToastType.Warning);
                return;
            }

            var menu = new ContextMenuStrip();
            menu.BackColor = Color.White;
            menu.Font = new Font("Segoe UI", 10);

            var adminItem = new ToolStripMenuItem("👑 Administrator (wszystko)");
            adminItem.Click += (s, ev) => ApplyPreset("admin");
            menu.Items.Add(adminItem);

            var managerItem = new ToolStripMenuItem("👔 Kierownik (bez administracji)");
            managerItem.Click += (s, ev) => ApplyPreset("manager");
            menu.Items.Add(managerItem);

            var salesItem = new ToolStripMenuItem("💼 Handlowiec (sprzedaż + CRM)");
            salesItem.Click += (s, ev) => ApplyPreset("sales");
            menu.Items.Add(salesItem);

            var warehouseItem = new ToolStripMenuItem("📦 Magazynier (produkcja + magazyn)");
            warehouseItem.Click += (s, ev) => ApplyPreset("warehouse");
            menu.Items.Add(warehouseItem);

            var viewerItem = new ToolStripMenuItem("👁 Podgląd (tylko odczyt analiz)");
            viewerItem.Click += (s, ev) => ApplyPreset("viewer");
            menu.Items.Add(viewerItem);

            var directorItem = new ToolStripMenuItem("👔 Dyrektor (pełny podgląd bez admin)");
            directorItem.Click += (s, ev) => ApplyPreset("director");
            menu.Items.Add(directorItem);

            menu.Items.Add(new ToolStripSeparator());

            var clearItem = new ToolStripMenuItem("🚫 Wyczyść wszystko");
            clearItem.Click += (s, ev) => ApplyPreset("none");
            menu.Items.Add(clearItem);

            var btn = sender as Control;
            menu.Show(btn, new Point(0, btn.Height));
        }

        private void ApplyPreset(string preset)
        {
            var modules = GetModulesList();

            foreach (var categoryList in categoryCheckboxes.Values)
            {
                foreach (var checkbox in categoryList)
                {
                    string moduleKey = checkbox.Tag?.ToString();
                    if (string.IsNullOrEmpty(moduleKey)) continue;

                    var module = modules.FirstOrDefault(m => m.Key == moduleKey);
                    if (module == null) continue;

                    checkbox.Checked = preset switch
                    {
                        "admin" => true,
                        "manager" => module.Category != "Administracja Systemu",
                        "director" => module.Category != "Administracja Systemu",
                        "sales" => module.Category == "Sprzedaż i CRM" || module.Category == "Planowanie i Analizy",
                        "warehouse" => module.Category == "Produkcja i Magazyn" || module.Category == "Opakowania i Transport",
                        "viewer" => module.Category == "Planowanie i Analizy" || module.Category == "Finanse i Zarządzanie",
                        "none" => false,
                        _ => checkbox.Checked
                    };
                }
            }

            UpdateProgressBar();
            string presetName = preset switch
            {
                "admin" => "Administrator",
                "manager" => "Kierownik",
                "director" => "Dyrektor",
                "sales" => "Handlowiec",
                "warehouse" => "Magazynier",
                "viewer" => "Podgląd",
                "none" => "Wyczyszczono",
                _ => preset
            };
            ToastNotification.Show(this, $"Szablon: {presetName}", ToastNotification.ToastType.Info);
        }

        private void UpdateProgressBar()
        {
            int total = 0;
            int enabled = 0;

            foreach (var categoryList in categoryCheckboxes.Values)
            {
                foreach (var checkbox in categoryList)
                {
                    total++;
                    if (checkbox.Checked) enabled++;
                }
            }

            progressBar.UpdateProgress(enabled, total);
        }

        private void AddUserButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new AddUserDialog(connectionString))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    LoadUsers();
                }
            }
        }

        private void DeleteUserButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(selectedUserId))
            {
                MessageBox.Show("Wybierz użytkownika do usunięcia.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedUser = allUsers.FirstOrDefault(u => u.ID == selectedUserId);
            string userName = selectedUser?.Name ?? "Nieznany";

            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć użytkownika:\n\nID: {selectedUserId}\nNazwa: {userName}\n\nTa operacja jest nieodwracalna!",
                "Potwierdzenie usunięcia",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string query = "DELETE FROM operators WHERE ID = @userId";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@userId", selectedUserId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    MessageBox.Show("✓ Użytkownik został usunięty.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    selectedUserCard = null;
                    LoadUsers();
                    permissionsFlowPanel.Controls.Clear();
                    selectedUserId = null;
                    selectedUserLabel.Text = "Wybierz użytkownika z listy";
                    selectedUserLabel.ForeColor = Colors.TextGray;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas usuwania użytkownika:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ManageHandlowcyButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(selectedUserId))
            {
                MessageBox.Show("Wybierz użytkownika, któremu chcesz przypisać handlowców.",
                    "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedUser = allUsers.FirstOrDefault(u => u.ID == selectedUserId);
            string userName = selectedUser?.Name ?? "Nieznany";

            var dialog = new UserHandlowcyDialog(connectionString, handelConnectionString, selectedUserId, userName);
            dialog.HandlowcyZapisani += (s, ev) => LoadUsers();
            dialog.Show();
        }

        private void EditContactButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(selectedUserId))
            {
                MessageBox.Show("Wybierz użytkownika, któremu chcesz edytować dane kontaktowe.",
                    "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedUser = allUsers.FirstOrDefault(u => u.ID == selectedUserId);
            string userName = selectedUser?.Name ?? "Nieznany";

            var dialog = new EditOperatorContactDialog(connectionString, selectedUserId, userName);
            dialog.ShowDialog();
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // PRZYPISYWANIE KLIENTÓW DO HANDLOWCÓW
        // ════════════════════════════════════════════════════════════════════════════════
        private void PrzydzielKlientowButton_Click(object sender, EventArgs e)
        {
            var dialog = new PrzydzielKlientowDialog(connectionString);
            dialog.Show();
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // IMPORT AVATARA UŻYTKOWNIKA
        // ════════════════════════════════════════════════════════════════════════════════
        private void ImportAvatarButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(selectedUserId))
            {
                ToastNotification.Show(this, "Wybierz użytkownika", ToastNotification.ToastType.Warning);
                return;
            }

            var selectedUser = allUsers.FirstOrDefault(u => u.ID == selectedUserId);
            string userName = selectedUser?.Name ?? "Nieznany";

            // Menu kontekstowe z opcjami
            var menu = new ContextMenuStrip();
            menu.BackColor = Color.White;
            menu.Font = new Font("Segoe UI", 10);

            var importItem = new ToolStripMenuItem("📷 Importuj zdjęcie...");
            importItem.Click += (s, ev) => ImportAvatarFromFile(selectedUserId, userName);
            menu.Items.Add(importItem);

            if (UserAvatarManager.HasAvatar(selectedUserId))
            {
                var removeItem = new ToolStripMenuItem("🗑 Usuń avatar");
                removeItem.Click += (s, ev) => {
                    if (UserAvatarManager.DeleteAvatar(selectedUserId))
                    {
                        ToastNotification.Show(this, "Avatar usunięty", ToastNotification.ToastType.Info);
                        RefreshUserCard(selectedUserId);
                    }
                };
                menu.Items.Add(removeItem);
            }

            menu.Items.Add(new ToolStripSeparator());

            var infoItem = new ToolStripMenuItem("ℹ Informacje o avatarach") { Enabled = false };
            menu.Items.Add(infoItem);

            var btn = sender as Control;
            menu.Show(btn, new Point(0, btn.Height));
        }

        private void ImportAvatarFromFile(string userId, string userName)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = $"Wybierz avatar dla {userName}";
                openFileDialog.Filter = "Pliki graficzne|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Wszystkie pliki|*.*";
                openFileDialog.FilterIndex = 1;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (UserAvatarManager.SaveAvatar(userId, openFileDialog.FileName))
                        {
                            ToastNotification.Show(this, $"Avatar zapisany dla {userName}", ToastNotification.ToastType.Success);
                            RefreshUserCard(userId);
                        }
                        else
                        {
                            ToastNotification.Show(this, "Błąd podczas zapisywania avatara", ToastNotification.ToastType.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd podczas importowania avatara:\n{ex.Message}",
                            "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void RefreshUserCard(string userId)
        {
            var user = allUsers.FirstOrDefault(u => u.ID == userId);
            if (user?.Card != null)
            {
                // Odśwież panel avatara
                foreach (Control c in user.Card.Controls)
                {
                    if (c.Tag?.ToString() == "avatar")
                    {
                        c.Invalidate();
                        break;
                    }
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // ZSYNCHRONIZOWANA LISTA MODUŁÓW - MUSI ODPOWIADAĆ Menu.cs
        // ════════════════════════════════════════════════════════════════════════════════
        private List<ModuleInfo> GetModulesList()
        {
            return new List<ModuleInfo>
            {
                // ═══════════════════════════════════════════════════════════════════════
                // ZAOPATRZENIE I ZAKUPY - Zielony
                // ═══════════════════════════════════════════════════════════════════════
                new ModuleInfo("DaneHodowcy", "Baza Hodowców", "Kompletna kartoteka dostawców żywca", "Zaopatrzenie i Zakupy", "🧑‍🌾"),
                new ModuleInfo("WstawieniaHodowcy", "Cykle Wstawień", "Rejestracja cykli hodowlanych piskląt", "Zaopatrzenie i Zakupy", "🐣"),
                new ModuleInfo("TerminyDostawyZywca", "Kalendarz Dostaw Żywca", "Planowanie terminów dostaw żywca", "Zaopatrzenie i Zakupy", "📅"),
                new ModuleInfo("PlachtyAviloga", "Matryca Transportu", "Planowanie tras transportu żywca z SMS", "Zaopatrzenie i Zakupy", "🚛"),
                new ModuleInfo("PanelPortiera", "Panel Portiera", "Przyjęcie i ważenie żywca na bramie", "Zaopatrzenie i Zakupy", "🚧"),
                new ModuleInfo("PanelLekarza", "Panel Lekarza", "Badanie weterynaryjne zwierząt", "Zaopatrzenie i Zakupy", "⚕️"),
                new ModuleInfo("Specyfikacje", "Specyfikacja Surowca", "Parametry jakościowe surowca", "Zaopatrzenie i Zakupy", "📋"),
                new ModuleInfo("DokumentyZakupu", "Dokumenty i Umowy", "Archiwum umów i certyfikatów", "Zaopatrzenie i Zakupy", "📑"),
                new ModuleInfo("PlatnosciHodowcy", "Rozliczenia z Hodowcami", "Płatności dla dostawców żywca", "Zaopatrzenie i Zakupy", "💵"),
                new ModuleInfo("ZakupPaszyPisklak", "Zakup Paszy i Piskląt", "Ewidencja zakupów pasz i piskląt", "Zaopatrzenie i Zakupy", "🌾"),
                new ModuleInfo("RaportyHodowcow", "Statystyki Hodowców", "Raporty współpracy z hodowcami", "Zaopatrzenie i Zakupy", "📊"),

                // ═══════════════════════════════════════════════════════════════════════
                // PRODUKCJA I MAGAZYN - Pomarańczowy
                // ═══════════════════════════════════════════════════════════════════════
                new ModuleInfo("ProdukcjaPodglad", "Panel Produkcji", "Monitoring procesu uboju i krojenia", "Produkcja i Magazyn", "🏭"),
                new ModuleInfo("KalkulacjaKrojenia", "Kalkulacja Rozbioru", "Planowanie krojenia tuszek", "Produkcja i Magazyn", "✂️"),
                new ModuleInfo("PrzychodMrozni", "Magazyn Mroźni", "Stany magazynowe produktów mrożonych", "Produkcja i Magazyn", "❄️"),
                new ModuleInfo("LiczenieMagazynu", "Inwentaryzacja Magazynu", "Rejestracja stanów magazynowych", "Produkcja i Magazyn", "📦"),
                new ModuleInfo("PanelMagazyniera", "Panel Magazyniera", "Zarządzanie wydaniami towarów", "Produkcja i Magazyn", "🗃️"),
                new ModuleInfo("AnalizaPrzychodu", "Analiza Przychodu", "Analiza tempa produkcji i przychodu towarów", "Produkcja i Magazyn", "⏱️"),
                new ModuleInfo("AnalizaWydajnosci", "Analiza Wydajności", "Porównanie masy żywca do tuszek", "Produkcja i Magazyn", "📈"),

                // ═══════════════════════════════════════════════════════════════════════
                // SPRZEDAŻ I CRM - Niebieski
                // ═══════════════════════════════════════════════════════════════════════
                new ModuleInfo("CRM", "Relacje z Klientami", "Zarządzanie relacjami z odbiorcami", "Sprzedaż i CRM", "🤝"),
                new ModuleInfo("KartotekaOdbiorcow", "Kartoteka Odbiorców", "Pełna baza danych klientów", "Sprzedaż i CRM", "👤"),
                new ModuleInfo("ZamowieniaOdbiorcow", "Zamówienia Klientów", "Przyjmowanie zamówień", "Sprzedaż i CRM", "🛒"),
                new ModuleInfo("DashboardHandlowca", "Dashboard Handlowca", "Kompleksowa analiza sprzedaży handlowca", "Sprzedaż i CRM", "📊"),
                new ModuleInfo("DokumentySprzedazy", "Faktury Sprzedaży", "Przeglądanie faktur i WZ", "Sprzedaż i CRM", "🧾"),
                new ModuleInfo("PanelFaktur", "Panel Faktur", "Tworzenie faktur w Symfonii", "Sprzedaż i CRM", "📋"),
                new ModuleInfo("OfertaCenowa", "Kreator Ofert", "Tworzenie ofert cenowych", "Sprzedaż i CRM", "💰"),
                new ModuleInfo("ListaOfert", "Archiwum Ofert", "Historia ofert handlowych", "Sprzedaż i CRM", "📂"),
                new ModuleInfo("DashboardOfert", "Analiza Ofert", "Statystyki skuteczności ofert", "Sprzedaż i CRM", "📊"),
                new ModuleInfo("DashboardWyczerpalnosci", "Klasy Wagowe", "Rozdzielanie klas wagowych", "Sprzedaż i CRM", "⚖️"),
                new ModuleInfo("PanelReklamacji", "Reklamacje Klientów", "Obsługa reklamacji odbiorców", "Sprzedaż i CRM", "⚠️"),
                new ModuleInfo("PanelPaniJola", "Panel Pani Jola", "Uproszczony widok zamówień - duże kafelki", "Sprzedaż i CRM", "📞"),

                // ═══════════════════════════════════════════════════════════════════════
                // PLANOWANIE I ANALIZY - Fioletowy
                // ═══════════════════════════════════════════════════════════════════════
                new ModuleInfo("PrognozyUboju", "Prognoza Uboju", "Analiza średnich zakupów żywca", "Planowanie i Analizy", "🔮"),
                new ModuleInfo("PlanTygodniowy", "Plan Tygodniowy", "Harmonogram uboju i krojenia", "Planowanie i Analizy", "🗓️"),
                new ModuleInfo("AnalizaTygodniowa", "Dashboard Analityczny", "Analiza produkcji i sprzedaży", "Planowanie i Analizy", "📉"),
                new ModuleInfo("DashboardPrzychodu", "Przychód Żywca LIVE", "Dashboard przyjęć żywca w czasie rzeczywistym", "Planowanie i Analizy", "🐔"),

                // ═══════════════════════════════════════════════════════════════════════
                // OPAKOWANIA I TRANSPORT - Turkusowy
                // ═══════════════════════════════════════════════════════════════════════
                new ModuleInfo("PodsumowanieSaldOpak", "Zestawienie Opakowań", "Salda opakowań wg typu", "Opakowania i Transport", "📦"),
                new ModuleInfo("SaldaOdbiorcowOpak", "Salda Opakowań Klientów", "Salda dla kontrahentów", "Opakowania i Transport", "🏷️"),
                new ModuleInfo("UstalanieTranportu", "Planowanie Transportu", "Organizacja tras dostaw", "Opakowania i Transport", "🚚"),

                // ═══════════════════════════════════════════════════════════════════════
                // FINANSE I ZARZĄDZANIE - Szaroniebieski
                // ═══════════════════════════════════════════════════════════════════════
                new ModuleInfo("PanelDyrektora", "Panel Dyrektora", "Panel zarządczy - KPI wszystkich działów", "Finanse i Zarządzanie", "👔"),
                new ModuleInfo("DaneFinansowe", "Wyniki Finansowe", "Przychody, koszty, marże", "Finanse i Zarządzanie", "💼"),
                new ModuleInfo("CentrumSpotkan", "Centrum Spotkań", "Rejestr spotkań i wizyt", "Finanse i Zarządzanie", "📆"),
                new ModuleInfo("NotatkiZeSpotkan", "Notatki Służbowe", "Notatki ze spotkań biznesowych", "Finanse i Zarządzanie", "📝"),
                new ModuleInfo("KontrolaGodzin", "Kontrola Czasu Pracy", "Monitoring czasu pracy pracowników", "Finanse i Zarządzanie", "⏰"),
                new ModuleInfo("KomunikatorFirmowy", "Komunikator Firmowy", "Wewnętrzny czat między pracownikami", "Finanse i Zarządzanie", "💬"),

                // ═══════════════════════════════════════════════════════════════════════
                // ADMINISTRACJA SYSTEMU - Czerwony
                // ═══════════════════════════════════════════════════════════════════════
                new ModuleInfo("ZmianyUHodowcow", "Wnioski o Zmiany", "Zatwierdzanie zmian danych hodowców", "Administracja Systemu", "📝"),
                new ModuleInfo("AdminPermissions", "Zarządzanie Uprawnieniami", "Nadawanie uprawnień użytkownikom", "Administracja Systemu", "🔐")
            };
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // ZSYNCHRONIZOWANA MAPA DOSTĘPU - Musi odpowiadać Menu.cs ParseAccessString
        // ════════════════════════════════════════════════════════════════════════════════
        private Dictionary<int, string> GetAccessMap()
        {
            return new Dictionary<int, string>
            {
                [0] = "DaneHodowcy",
                [1] = "ZakupPaszyPisklak",
                [2] = "WstawieniaHodowcy",
                [3] = "TerminyDostawyZywca",
                [4] = "PlachtyAviloga",
                [5] = "DokumentyZakupu",
                [6] = "Specyfikacje",
                [7] = "PlatnosciHodowcy",
                [8] = "CRM",
                [9] = "ZamowieniaOdbiorcow",
                [10] = "KalkulacjaKrojenia",
                [11] = "PrzychodMrozni",
                [12] = "DokumentySprzedazy",
                [13] = "PodsumowanieSaldOpak",
                [14] = "SaldaOdbiorcowOpak",
                [15] = "DaneFinansowe",
                [16] = "UstalanieTranportu",
                [17] = "ZmianyUHodowcow",
                [18] = "ProdukcjaPodglad",
                [19] = "OfertaCenowa",
                [20] = "PrognozyUboju",
                [21] = "AnalizaTygodniowa",
                [22] = "NotatkiZeSpotkan",
                [23] = "PlanTygodniowy",
                [24] = "LiczenieMagazynu",
                [25] = "PanelMagazyniera",
                [26] = "KartotekaOdbiorcow",
                [27] = "AnalizaWydajnosci",
                [28] = "RezerwacjaKlas",
                [29] = "DashboardWyczerpalnosci",
                [30] = "ListaOfert",
                [31] = "DashboardOfert",
                [32] = "PanelReklamacji",
                [33] = "ReklamacjeJakosc",
                [34] = "RaportyHodowcow",
                [35] = "AdminPermissions",
                [36] = "AnalizaPrzychodu",
                [37] = "DashboardHandlowca",
                [38] = "PanelFaktur",
                [39] = "PanelPortiera",
                [40] = "PanelLekarza",
                [41] = "KontrolaGodzin",
                [42] = "CentrumSpotkan",
                [43] = "PanelPaniJola",
                [44] = "KomunikatorFirmowy",
                [46] = "DashboardPrzychodu",
                [47] = "MapaKlientow",
                [48] = "WnioskiUrlopowe",
                [49] = "PanelDyrektora"
            };
        }

        private class ModuleInfo
        {
            public string Key { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public string Category { get; set; }
            public string Icon { get; set; }

            public ModuleInfo(string key, string displayName, string description, string category, string icon)
            {
                Key = key;
                DisplayName = displayName;
                Description = description;
                Category = category;
                Icon = icon;
            }
        }
    }

    public class AddUserDialog : Form
    {
        private TextBox idTextBox, nameTextBox;
        private Button okButton, cancelButton;
        private string connectionString;

        public AddUserDialog(string connString)
        {
            connectionString = connString;
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "Dodaj nowego użytkownika";
            this.Size = new Size(450, 250);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(245, 247, 249);

            var titleLabel = new Label
            {
                Text = "➕ Nowy użytkownik",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location = new Point(30, 20),
                AutoSize = true
            };
            this.Controls.Add(titleLabel);

            var idLabel = new Label { Text = "ID użytkownika:", Location = new Point(30, 70), AutoSize = true, Font = new Font("Segoe UI", 10) };
            this.Controls.Add(idLabel);

            idTextBox = new TextBox { Location = new Point(170, 67), Size = new Size(230, 28), Font = new Font("Segoe UI", 11) };
            this.Controls.Add(idTextBox);

            var nameLabel = new Label { Text = "Nazwa:", Location = new Point(30, 115), AutoSize = true, Font = new Font("Segoe UI", 10) };
            this.Controls.Add(nameLabel);

            nameTextBox = new TextBox { Location = new Point(170, 112), Size = new Size(230, 28), Font = new Font("Segoe UI", 11) };
            this.Controls.Add(nameTextBox);

            okButton = new Button
            {
                Text = "Dodaj",
                Location = new Point(130, 170),
                Size = new Size(110, 40),
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                DialogResult = DialogResult.OK
            };
            okButton.FlatAppearance.BorderSize = 0;
            okButton.Click += OkButton_Click;
            this.Controls.Add(okButton);

            cancelButton = new Button
            {
                Text = "Anuluj",
                Location = new Point(250, 170),
                Size = new Size(110, 40),
                BackColor = Color.FromArgb(127, 140, 141),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                DialogResult = DialogResult.Cancel
            };
            cancelButton.FlatAppearance.BorderSize = 0;
            this.Controls.Add(cancelButton);
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(idTextBox.Text) || string.IsNullOrWhiteSpace(nameTextBox.Text))
            {
                MessageBox.Show("Wypełnij wszystkie pola.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"INSERT INTO operators (ID, Name, Access) VALUES (@id, @name, @access)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", idTextBox.Text);
                        cmd.Parameters.AddWithValue("@name", nameTextBox.Text);
                        cmd.Parameters.AddWithValue("@access", new string('0', 50));
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("✓ Użytkownik został dodany.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dodawania użytkownika:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.None;
            }
        }
    }
}
