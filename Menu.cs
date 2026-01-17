using Kalendarz1.AnalizaPrzychoduProdukcji;
using Kalendarz1.HandlowiecDashboard.Views;
using Kalendarz1.OfertaCenowa;
using Kalendarz1.Opakowania.Views;
using Kalendarz1.Reklamacje;
using Kalendarz1.KontrolaGodzin;
using Kalendarz1.Zywiec.RaportyStatystyki;
using Kalendarz1.Spotkania.Views;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Kalendarz1
{
    public partial class MENU : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string connectionHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private Dictionary<string, bool> userPermissions = new Dictionary<string, bool>();
        private bool isAdmin = false;
        private Panel sidePanel;
        private TableLayoutPanel mainLayout;

        public MENU()
        {
            InitializeComponent();
            InitializeCustomComponents();
            LoadUserPermissions();
            SetupMenuItems();
            ApplyModernStyle();
        }

        private void InitializeCustomComponents()
        {
            this.WindowState = FormWindowState.Maximized;
            this.Text = "ZPSP - Menu Główne";

            // Panel boczny z informacjami o użytkowniku - zawsze widoczny
            sidePanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 170,
                BackColor = Color.FromArgb(30, 40, 50),
                Visible = true
            };

            // Górna sekcja z użytkownikiem
            var userSection = CreateUserSidePanel();
            sidePanel.Controls.Add(userSection);

            mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(236, 239, 241),
                Padding = new Padding(10),
                ColumnCount = 2,
                RowCount = 1,
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            this.Controls.Add(mainLayout);
            this.Controls.Add(sidePanel);
        }

        /// <summary>
        /// Tworzy panel boczny z informacjami o użytkowniku
        /// </summary>
        private Panel CreateUserSidePanel()
        {
            string odbiorcaId = App.UserID ?? "";
            string userName = App.UserFullName ?? App.UserID ?? "Użytkownik";
            bool isUserAdmin = (App.UserID == "11111");
            int avatarSize = 90;
            int panelWidth = 170;
            int logoHeight = 60;

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };

            // Sekcja logo na samej górze (nad zieloną kreską)
            var logoSection = new Panel
            {
                Dock = DockStyle.Top,
                Height = logoHeight + 10,
                BackColor = Color.FromArgb(30, 40, 50)
            };

            var logoPanel = new Panel
            {
                Size = new Size(panelWidth - 20, logoHeight),
                Location = new Point(10, 5),
                BackColor = Color.Transparent,
                Cursor = isUserAdmin ? Cursors.Hand : Cursors.Default
            };

            logoPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                try
                {
                    if (CompanyLogoManager.HasLogo())
                    {
                        using (var logo = CompanyLogoManager.GetLogoScaled(panelWidth - 20, logoHeight))
                        {
                            if (logo != null)
                            {
                                int x = (logoPanel.Width - logo.Width) / 2;
                                int y = (logoPanel.Height - logo.Height) / 2;
                                e.Graphics.DrawImage(logo, x, y, logo.Width, logo.Height);
                                return;
                            }
                        }
                    }
                }
                catch { }

                // Domyślne logo
                using (var defaultLogo = CompanyLogoManager.GenerateDefaultLogo(panelWidth - 40, logoHeight - 10))
                {
                    int x = (logoPanel.Width - defaultLogo.Width) / 2;
                    int y = (logoPanel.Height - defaultLogo.Height) / 2;
                    e.Graphics.DrawImage(defaultLogo, x, y, defaultLogo.Width, defaultLogo.Height);
                }
            };

            // Menu kontekstowe dla admina (prawy przycisk myszy) - sprawdzamy App.UserID bezpośrednio
            if (isUserAdmin)
            {
                var logoContextMenu = new ContextMenuStrip();

                var importLogoItem = new ToolStripMenuItem("Importuj logo firmy", null, (s, e) =>
                {
                    using (var openFileDialog = new OpenFileDialog())
                    {
                        openFileDialog.Title = "Wybierz logo firmy";
                        openFileDialog.Filter = "Pliki graficzne|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Wszystkie pliki|*.*";

                        if (openFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            if (CompanyLogoManager.SaveLogo(openFileDialog.FileName))
                            {
                                logoPanel.Invalidate();
                                MessageBox.Show("Logo firmy zostało zaktualizowane!", "Sukces",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show("Nie udało się zapisać logo.", "Błąd",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                });

                var deleteLogoItem = new ToolStripMenuItem("Usuń logo", null, (s, e) =>
                {
                    if (MessageBox.Show("Czy na pewno chcesz usunąć logo firmy?", "Potwierdzenie",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        CompanyLogoManager.DeleteLogo();
                        logoPanel.Invalidate();
                    }
                });

                logoContextMenu.Items.Add(importLogoItem);
                logoContextMenu.Items.Add(new ToolStripSeparator());
                logoContextMenu.Items.Add(deleteLogoItem);

                logoPanel.ContextMenuStrip = logoContextMenu;
            }

            logoSection.Controls.Add(logoPanel);
            // logoSection będzie dodany później (po headerPanel) aby być na górze

            // Sekcja z avatarem i nazwą (pod zieloną kreską)
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 215,
                BackColor = Color.FromArgb(25, 35, 45)
            };

            // Gradient w tle headera
            headerPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new LinearGradientBrush(
                    new Point(0, 0), new Point(0, headerPanel.Height),
                    Color.FromArgb(40, 55, 70), Color.FromArgb(25, 35, 45)))
                {
                    e.Graphics.FillRectangle(brush, 0, 0, headerPanel.Width, headerPanel.Height);
                }

                // Zielony pasek akcentowy na górze
                using (var brush = new SolidBrush(Color.FromArgb(76, 175, 80)))
                {
                    e.Graphics.FillRectangle(brush, 0, 0, headerPanel.Width, 4);
                }
            };

            // Avatar wycentrowany
            var avatarPanel = new Panel
            {
                Size = new Size(avatarSize, avatarSize),
                Location = new Point((panelWidth - avatarSize) / 2, 20),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            avatarPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // Biała obwódka wokół avatara
                using (var pen = new Pen(Color.FromArgb(100, 255, 255, 255), 3))
                {
                    e.Graphics.DrawEllipse(pen, 1, 1, avatarSize - 3, avatarSize - 3);
                }

                try
                {
                    if (UserAvatarManager.HasAvatar(odbiorcaId))
                    {
                        using (var avatar = UserAvatarManager.GetAvatarRounded(odbiorcaId, avatarSize - 6))
                        {
                            if (avatar != null)
                            {
                                e.Graphics.DrawImage(avatar, 3, 3, avatarSize - 6, avatarSize - 6);
                                return;
                            }
                        }
                    }
                }
                catch { }

                using (var defaultAvatar = UserAvatarManager.GenerateDefaultAvatar(userName, odbiorcaId, avatarSize - 6))
                {
                    e.Graphics.DrawImage(defaultAvatar, 3, 3, avatarSize - 6, avatarSize - 6);
                }
            };

            // Kliknięcie na avatar - powiększenie
            avatarPanel.Click += (s, e) => ShowEnlargedAvatar(odbiorcaId, userName);
            headerPanel.Controls.Add(avatarPanel);

            // Nazwa użytkownika - wycentrowana
            var nameLabel = new Label
            {
                Text = userName,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(panelWidth - 10, 25),
                Location = new Point(5, 120),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            headerPanel.Controls.Add(nameLabel);

            // Status online - wycentrowany
            var statusLabel = new Label
            {
                Text = "● Online",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(76, 175, 80),
                AutoSize = false,
                Size = new Size(panelWidth - 10, 20),
                Location = new Point(5, 148),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            headerPanel.Controls.Add(statusLabel);

            // Imieniny - wycentrowane
            var nameDaysLabel = new Label
            {
                Text = NameDaysManager.GetTodayNameDaysWithHeader(),
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(180, 180, 180),
                AutoSize = false,
                Size = new Size(panelWidth - 10, 35),
                Location = new Point(5, 170),
                TextAlign = ContentAlignment.TopCenter,
                BackColor = Color.Transparent
            };
            headerPanel.Controls.Add(nameDaysLabel);

            // Separator między headerPanel a resztą
            var separator = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(50, 60, 70)
            };

            // Dolna sekcja - wyloguj, admin i wersja
            var footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 140,
                BackColor = Color.FromArgb(20, 28, 36)
            };

            // Przycisk Wyloguj
            var logoutButton = new Button
            {
                Text = "Wyloguj",
                Font = new Font("Segoe UI", 9),
                Size = new Size(panelWidth - 20, 38),
                Location = new Point(10, 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 65, 75),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            logoutButton.FlatAppearance.BorderSize = 0;
            logoutButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(180, 60, 60);
            logoutButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(200, 50, 50);
            logoutButton.Click += LogoutButton_Click;
            footerPanel.Controls.Add(logoutButton);

            // Przycisk Panel Admin (pod wyloguj, widoczny tylko dla adminów)
            var adminButton = new Button
            {
                Text = "Panel Admin",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Size = new Size(panelWidth - 20, 38),
                Location = new Point(10, 55),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(229, 57, 53),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Visible = false,
                Tag = "adminButton"
            };
            adminButton.FlatAppearance.BorderSize = 0;
            adminButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(244, 81, 77);
            adminButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(198, 40, 40);
            adminButton.Click += AdminPanelButton_Click;
            footerPanel.Controls.Add(adminButton);

            var versionLabel = new Label
            {
                Text = "ZPSP v2.0",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(100, 110, 120),
                AutoSize = false,
                Size = new Size(panelWidth - 20, 20),
                Location = new Point(10, 105),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            footerPanel.Controls.Add(versionLabel);

            panel.Controls.Add(footerPanel);

            // Panel z informacjami - używamy Dock.Top z dużą wysokością
            var infoPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 580,
                BackColor = Color.FromArgb(25, 35, 45)
            };

            var culture = new System.Globalization.CultureInfo("pl-PL");
            var now = DateTime.Now;
            string dayOfWeek = culture.DateTimeFormat.GetDayName(now.DayOfWeek);
            dayOfWeek = char.ToUpper(dayOfWeek[0]) + dayOfWeek.Substring(1);
            int weekNumber = culture.Calendar.GetWeekOfYear(now, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            var weather = WeatherManager.GetWeather();
            var lastLogin = LoginHistoryManager.GetLastLogin(App.UserID);
            var quote = QuotesManager.GetRandomQuote();
            var meetings = MeetingsManager.GetMeetingsSummary(App.UserID);
            var tasks = TasksManager.GetTodayTasksSummary(App.UserID);
            var currency = CurrencyManager.GetCurrency();

            int y = 5; // Początkowa pozycja Y
            int contentWidth = panelWidth - 20;

            // ========== GODZINA ==========
            var timeLabel = new Label
            {
                Text = now.ToString("HH:mm"),
                Font = new Font("Segoe UI Light", 26),
                ForeColor = Color.White,
                Size = new Size(contentWidth, 38),
                Location = new Point(10, y),
                TextAlign = ContentAlignment.MiddleCenter
            };
            infoPanel.Controls.Add(timeLabel);
            y += 40;

            // ========== DZIEŃ I DATA ==========
            var dayLabel = new Label
            {
                Text = $"{dayOfWeek}, {now.ToString("d MMM", culture)}",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 175, 80),
                Size = new Size(contentWidth, 18),
                Location = new Point(10, y),
                TextAlign = ContentAlignment.MiddleCenter
            };
            infoPanel.Controls.Add(dayLabel);
            y += 20;

            // ========== TYDZIEŃ ==========
            var weekLabel = new Label
            {
                Text = $"Tydzień {weekNumber}",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(120, 130, 140),
                Size = new Size(contentWidth, 16),
                Location = new Point(10, y),
                TextAlign = ContentAlignment.MiddleCenter
            };
            infoPanel.Controls.Add(weekLabel);
            y += 22;

            // ========== SEPARATOR ==========
            var sep1 = new Panel
            {
                Size = new Size(contentWidth - 20, 1),
                Location = new Point(20, y),
                BackColor = Color.FromArgb(50, 60, 70)
            };
            infoPanel.Controls.Add(sep1);
            y += 8;

            // ========== POGODA ==========
            var weatherMainLabel = new Label
            {
                Text = $"{weather.Icon} {weather.Temperature}°C",
                Font = new Font("Segoe UI", 14),
                ForeColor = Color.FromArgb(255, 220, 100), // Jasny żółty
                Size = new Size(contentWidth, 28),
                Location = new Point(10, y),
                TextAlign = ContentAlignment.MiddleCenter
            };
            infoPanel.Controls.Add(weatherMainLabel);
            y += 26;

            var weatherDescLabel = new Label
            {
                Text = weather.Description,
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(140, 150, 160),
                Size = new Size(contentWidth, 16),
                Location = new Point(10, y),
                TextAlign = ContentAlignment.MiddleCenter
            };
            infoPanel.Controls.Add(weatherDescLabel);
            y += 22;

            // ========== SEPARATOR ==========
            var sep2 = new Panel
            {
                Size = new Size(contentWidth - 20, 1),
                Location = new Point(20, y),
                BackColor = Color.FromArgb(50, 60, 70)
            };
            infoPanel.Controls.Add(sep2);
            y += 6;

            // ========== PROGNOZA ==========
            int forecastDays = Math.Min(5, weather.Forecast.Count);
            if (forecastDays > 0)
            {
                int dayWidth = contentWidth / forecastDays;
                for (int i = 0; i < forecastDays; i++)
                {
                    var day = weather.Forecast[i];

                    var dayNameLbl = new Label
                    {
                        Text = day.DayName,
                        Font = new Font("Segoe UI", 7),
                        ForeColor = Color.FromArgb(120, 130, 140),
                        Size = new Size(dayWidth, 14),
                        Location = new Point(10 + i * dayWidth, y),
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    infoPanel.Controls.Add(dayNameLbl);

                    var dayIconLbl = new Label
                    {
                        Text = day.Icon,
                        Font = new Font("Segoe UI", 11),
                        ForeColor = Color.FromArgb(255, 220, 100), // Jasny żółty dla ikon
                        Size = new Size(dayWidth, 18),
                        Location = new Point(10 + i * dayWidth, y + 14),
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    infoPanel.Controls.Add(dayIconLbl);

                    var dayTempLbl = new Label
                    {
                        Text = $"{day.TempMax}°",
                        Font = new Font("Segoe UI", 7),
                        ForeColor = Color.FromArgb(170, 180, 190),
                        Size = new Size(dayWidth, 14),
                        Location = new Point(10 + i * dayWidth, y + 32),
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    infoPanel.Controls.Add(dayTempLbl);
                }
                y += 50;
            }

            // ========== SEPARATOR ==========
            var sep3 = new Panel
            {
                Size = new Size(contentWidth - 20, 1),
                Location = new Point(20, y),
                BackColor = Color.FromArgb(50, 60, 70)
            };
            infoPanel.Controls.Add(sep3);
            y += 6;

            // ========== SPOTKANIA ==========
            var meetingsHeaderText = meetings.TodayCount > 0
                ? $"Spotkania ({meetings.TodayCount} dziś)"
                : "Spotkania";
            var meetingsHeader = new Label
            {
                Text = meetingsHeaderText,
                Font = new Font("Segoe UI", 7, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 180, 255),
                Size = new Size(contentWidth, 14),
                Location = new Point(10, y),
                TextAlign = ContentAlignment.MiddleCenter
            };
            infoPanel.Controls.Add(meetingsHeader);
            y += 16;

            if (meetings.NextMeeting != null)
            {
                var nextMtg = meetings.NextMeeting;
                var meetingColor = nextMtg.IsNow ? Color.FromArgb(76, 175, 80) :
                                   nextMtg.IsSoon ? Color.FromArgb(255, 193, 7) :
                                   Color.FromArgb(150, 160, 170);

                var meetingTimeLabel = new Label
                {
                    Text = nextMtg.GetTimeUntilText(),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = meetingColor,
                    Size = new Size(contentWidth, 16),
                    Location = new Point(10, y),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                infoPanel.Controls.Add(meetingTimeLabel);
                y += 16;

                var meetingTitle = nextMtg.Tytul ?? "";
                if (meetingTitle.Length > 22) meetingTitle = meetingTitle.Substring(0, 20) + "..";
                var meetingTitleLabel = new Label
                {
                    Text = meetingTitle,
                    Font = new Font("Segoe UI", 7),
                    ForeColor = Color.FromArgb(180, 190, 200),
                    Size = new Size(contentWidth, 14),
                    Location = new Point(10, y),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                infoPanel.Controls.Add(meetingTitleLabel);
                y += 14;
            }
            else
            {
                var noMeetingsLabel = new Label
                {
                    Text = "Brak nadchodzacych",
                    Font = new Font("Segoe UI", 7),
                    ForeColor = Color.FromArgb(100, 110, 120),
                    Size = new Size(contentWidth, 14),
                    Location = new Point(10, y),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                infoPanel.Controls.Add(noMeetingsLabel);
                y += 14;
            }
            y += 4;

            var sepMeetings = new Panel
            {
                Size = new Size(contentWidth - 20, 1),
                Location = new Point(20, y),
                BackColor = Color.FromArgb(50, 60, 70)
            };
            infoPanel.Controls.Add(sepMeetings);
            y += 6;

            // ========== ZADANIA ==========
            var tasksHeaderText = tasks.Total > 0
                ? $"Zadania: {tasks.Done}/{tasks.Total}"
                : "Zadania";
            var tasksColor = tasks.Zalegle > 0 ? Color.FromArgb(244, 67, 54) :
                             tasks.Pilne > 0 ? Color.FromArgb(255, 193, 7) :
                             tasks.Total > 0 ? Color.FromArgb(76, 175, 80) :
                             Color.FromArgb(100, 180, 255);

            var tasksHeader = new Label
            {
                Text = tasksHeaderText,
                Font = new Font("Segoe UI", 7, FontStyle.Bold),
                ForeColor = tasksColor,
                Size = new Size(contentWidth, 14),
                Location = new Point(10, y),
                TextAlign = ContentAlignment.MiddleCenter
            };
            infoPanel.Controls.Add(tasksHeader);
            y += 16;

            if (tasks.Total > 0)
            {
                if (tasks.Zalegle > 0)
                {
                    var zalegleLabel = new Label
                    {
                        Text = $"! Zalegle: {tasks.Zalegle}",
                        Font = new Font("Segoe UI", 7),
                        ForeColor = Color.FromArgb(244, 67, 54),
                        Size = new Size(contentWidth, 14),
                        Location = new Point(10, y),
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    infoPanel.Controls.Add(zalegleLabel);
                    y += 14;
                }

                if (tasks.Pilne > 0)
                {
                    var pilneLabel = new Label
                    {
                        Text = $"Pilne: {tasks.Pilne}",
                        Font = new Font("Segoe UI", 7),
                        ForeColor = Color.FromArgb(255, 193, 7),
                        Size = new Size(contentWidth, 14),
                        Location = new Point(10, y),
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    infoPanel.Controls.Add(pilneLabel);
                    y += 14;
                }
            }
            else
            {
                var noTasksLabel = new Label
                {
                    Text = "Brak zadan na dzis",
                    Font = new Font("Segoe UI", 7),
                    ForeColor = Color.FromArgb(100, 110, 120),
                    Size = new Size(contentWidth, 14),
                    Location = new Point(10, y),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                infoPanel.Controls.Add(noTasksLabel);
                y += 14;
            }
            y += 4;

            var sepTasks = new Panel
            {
                Size = new Size(contentWidth - 20, 1),
                Location = new Point(20, y),
                BackColor = Color.FromArgb(50, 60, 70)
            };
            infoPanel.Controls.Add(sepTasks);
            y += 6;

            // ========== KURS WALUT ==========
            var currencyHeaderLabel = new Label
            {
                Text = "Kurs EUR (NBP)",
                Font = new Font("Segoe UI", 7, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 180, 255),
                Size = new Size(contentWidth, 14),
                Location = new Point(10, y),
                TextAlign = ContentAlignment.MiddleCenter
            };
            infoPanel.Controls.Add(currencyHeaderLabel);
            y += 16;

            if (currency.IsValid)
            {
                var eurChangeColor = currency.EurChange.StartsWith("+") ? Color.FromArgb(76, 175, 80) : Color.FromArgb(244, 67, 54);

                var currencyLabel = new Label
                {
                    Text = $"{currency.EurRate:F4} PLN",
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = Color.White,
                    Size = new Size(contentWidth, 18),
                    Location = new Point(10, y),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                infoPanel.Controls.Add(currencyLabel);
                y += 18;

                var currencyChangeLabel = new Label
                {
                    Text = $"({currency.EurChange})",
                    Font = new Font("Segoe UI", 7),
                    ForeColor = eurChangeColor,
                    Size = new Size(contentWidth, 14),
                    Location = new Point(10, y),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                infoPanel.Controls.Add(currencyChangeLabel);
                y += 14;
            }
            else
            {
                var currencyLoadingLabel = new Label
                {
                    Text = "Ladowanie...",
                    Font = new Font("Segoe UI", 7),
                    ForeColor = Color.FromArgb(100, 110, 120),
                    Size = new Size(contentWidth, 14),
                    Location = new Point(10, y),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                infoPanel.Controls.Add(currencyLoadingLabel);
                y += 14;
            }
            y += 4;

            var sepCurrency = new Panel
            {
                Size = new Size(contentWidth - 20, 1),
                Location = new Point(20, y),
                BackColor = Color.FromArgb(50, 60, 70)
            };
            infoPanel.Controls.Add(sepCurrency);
            y += 6;

            // ========== OSTATNIE LOGOWANIE ==========
            string lastLoginText = lastLogin != null
                ? $"Ostatnie logowanie: {lastLogin.LoginTime:dd.MM HH:mm}"
                : "Pierwsze logowanie";

            var lastLoginLabel = new Label
            {
                Text = lastLoginText,
                Font = new Font("Segoe UI", 7),
                ForeColor = Color.FromArgb(110, 120, 130),
                Size = new Size(contentWidth, 16),
                Location = new Point(10, y),
                TextAlign = ContentAlignment.MiddleCenter
            };
            infoPanel.Controls.Add(lastLoginLabel);
            y += 22;

            // ========== SEPARATOR ==========
            var sep4 = new Panel
            {
                Size = new Size(contentWidth - 20, 1),
                Location = new Point(20, y),
                BackColor = Color.FromArgb(50, 60, 70)
            };
            infoPanel.Controls.Add(sep4);
            y += 8;

            // ========== NOTATNIK ==========
            var notepadButton = new Button
            {
                Text = "📝 Notatnik",
                Font = new Font("Segoe UI", 8),
                Size = new Size(contentWidth - 20, 28),
                Location = new Point(20, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 55, 65),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            notepadButton.FlatAppearance.BorderColor = Color.FromArgb(60, 70, 80);
            notepadButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 65, 75);
            notepadButton.Click += (s, e) => ShowNotepadDialog();
            infoPanel.Controls.Add(notepadButton);
            y += 34;

            // ========== SEPARATOR ==========
            var sep5 = new Panel
            {
                Size = new Size(contentWidth - 20, 1),
                Location = new Point(20, y),
                BackColor = Color.FromArgb(50, 60, 70)
            };
            infoPanel.Controls.Add(sep5);
            y += 8;

            // ========== CYTAT ==========
            var quoteText = "\"" + quote.Text + "\"";
            if (!string.IsNullOrEmpty(quote.Author))
            {
                quoteText += "\n- " + quote.Author;
            }
            var quoteLabel = new Label
            {
                Text = quoteText,
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                ForeColor = Color.FromArgb(120, 130, 140),
                Size = new Size(contentWidth, 80),
                Location = new Point(10, y),
                TextAlign = ContentAlignment.TopCenter
            };
            infoPanel.Controls.Add(quoteLabel);

            // Dodajemy panele w odwrotnej kolejności (WinForms Dock.Top - ostatni dodany jest na górze)
            // Kolejność od dołu do góry: infoPanel, separator, headerPanel, logoSection
            panel.Controls.Add(infoPanel);
            panel.Controls.Add(separator);
            panel.Controls.Add(headerPanel);
            panel.Controls.Add(logoSection);

            // Timer do aktualizacji czasu
            var clockTimer = new Timer { Interval = 1000 };
            clockTimer.Tick += (s, e) =>
            {
                var currentTime = DateTime.Now;
                timeLabel.Text = currentTime.ToString("HH:mm");

                if (currentTime.Second == 0 && currentTime.Minute == 0)
                {
                    string newDayOfWeek = culture.DateTimeFormat.GetDayName(currentTime.DayOfWeek);
                    newDayOfWeek = char.ToUpper(newDayOfWeek[0]) + newDayOfWeek.Substring(1);
                    dayLabel.Text = $"{newDayOfWeek}, {currentTime.ToString("d MMM", culture)}";
                    int newWeekNumber = culture.Calendar.GetWeekOfYear(currentTime, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                    weekLabel.Text = $"Tydzień {newWeekNumber}";
                }
            };
            clockTimer.Start();

            // Timer do aktualizacji pogody (co 30 minut)
            var weatherTimer = new Timer { Interval = 30 * 60 * 1000 };
            weatherTimer.Tick += async (s, e) =>
            {
                var newWeather = await WeatherManager.GetWeatherAsync();
                weatherMainLabel.Text = $"{newWeather.Icon} {newWeather.Temperature}°C";
                weatherDescLabel.Text = newWeather.Description;
            };
            weatherTimer.Start();

            return panel;
        }

        private void LoadUserPermissions()
        {
            string userId = App.UserID;
            isAdmin = (userId == "11111");

            LoadAllPermissions(false);

            if (isAdmin)
            {
                // Pokaż przycisk Panel Administracyjny dla admina
                ShowAdminButton();
                LoadAllPermissions(true);
            }
            else
            {
                LoadUserAccessFromDatabase(userId);
            }
        }

        private void LoadUserAccessFromDatabase(string userId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT Access FROM operators WHERE ID = @userId";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        var result = cmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value && !string.IsNullOrEmpty(result.ToString()))
                        {
                            ParseAccessString(result.ToString());
                        }
                        else
                        {
                            MessageBox.Show("Użytkownik nie ma zdefiniowanych uprawnień. Dostęp został zablokowany.", "Brak uprawnień", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania uprawnień: {ex.Message}\n\nDostęp został zablokowany z powodu błędu.", "Błąd krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LoadAllPermissions(false);
            }
        }

        private void ParseAccessString(string accessString)
        {
            var accessMap = new Dictionary<int, string>
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
                [42] = "CentrumSpotkan"  // <-- CENTRUM SPOTKAŃ
            };

            for (int i = 0; i < accessString.Length && i < accessMap.Count; i++)
            {
                if (accessMap.ContainsKey(i) && accessString[i] == '1')
                {
                    userPermissions[accessMap[i]] = true;
                }
            }
        }

        /// <summary>
        /// Pokazuje przycisk Panel Administracyjny w panelu bocznym
        /// </summary>
        private void ShowAdminButton()
        {
            var adminButton = FindControlByTag(sidePanel, "adminButton");
            if (adminButton != null)
            {
                adminButton.Visible = true;
            }
        }

        /// <summary>
        /// Rekurencyjnie szuka kontrolki po Tag
        /// </summary>
        private Control FindControlByTag(Control parent, string tag)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Tag != null && control.Tag.ToString() == tag)
                {
                    return control;
                }

                var found = FindControlByTag(control, tag);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private void LoadAllPermissions(bool grantAll)
        {
            var allModules = GetAllModules();
            if (userPermissions.Count == 0)
            {
                foreach (var module in allModules)
                {
                    userPermissions.Add(module, grantAll);
                }
            }
            else
            {
                foreach (var module in allModules)
                {
                    userPermissions[module] = grantAll;
                }
            }
        }

        private List<string> GetAllModules()
        {
            // Pełna lista wszystkich modułów dostępnych w menu
            return new List<string>
            {
                // ZAOPATRZENIE I ZAKUPY
                "DaneHodowcy", "WstawieniaHodowcy", "TerminyDostawyZywca", "PlachtyAviloga",
                "PanelPortiera", "PanelLekarza", "Specyfikacje", "DokumentyZakupu",
                "PlatnosciHodowcy", "ZakupPaszyPisklak", "RaportyHodowcow",

                // PRODUKCJA I MAGAZYN
                "ProdukcjaPodglad", "KalkulacjaKrojenia", "PrzychodMrozni", "LiczenieMagazynu",
                "PanelMagazyniera", "AnalizaPrzychodu", "AnalizaWydajnosci",

                // SPRZEDAŻ I CRM
                "CRM", "KartotekaOdbiorcow", "ZamowieniaOdbiorcow", "DashboardHandlowca",
                "DokumentySprzedazy", "PanelFaktur", "OfertaCenowa", "ListaOfert",
                "DashboardOfert", "DashboardWyczerpalnosci", "PanelReklamacji",

                // PLANOWANIE I ANALIZY
                "PrognozyUboju", "PlanTygodniowy", "AnalizaTygodniowa",

                // OPAKOWANIA I TRANSPORT
                "PodsumowanieSaldOpak", "SaldaOdbiorcowOpak", "UstalanieTranportu",

                // FINANSE I ZARZĄDZANIE
                "DaneFinansowe", "CentrumSpotkan", "NotatkiZeSpotkan",

                // KADRY I HR
                "KontrolaGodzin",

                // ADMINISTRACJA SYSTEMU
                "ZmianyUHodowcow", "AdminPermissions",

                // Nieużywane ale w systemie uprawnień
                "RezerwacjaKlas", "ReklamacjeJakosc"
            };
        }

        private void SetupMenuItems()
        {
            mainLayout.Controls.Clear();

            // ══════════════════════════════════════════════════════════════════════════════
            // KOLORY DZIAŁÓW - GRADIENT OD JAŚNIEJSZEGO DO CIEMNIEJSZEGO
            // ══════════════════════════════════════════════════════════════════════════════
            // ZAKUP/ZAOPATRZENIE - Odcienie zielonego (od jasnego do ciemnego)
            // SPRZEDAŻ/CRM - Odcienie niebieskiego (od jasnego do ciemnego)
            // PRODUKCJA/MAGAZYN - Odcienie pomarańczowego (od jasnego do ciemnego)
            // OPAKOWANIA/TRANSPORT - Odcienie turkusowego (od jasnego do ciemnego)
            // FINANSE/ZARZĄDZANIE - Odcienie szaroniebieskiego (od jasnego do ciemnego)
            // ADMINISTRACJA - Odcienie czerwonego (od jasnego do ciemnego)
            // KADRY/HR - Odcienie fioletowo-różowe (NOWE)
            // ══════════════════════════════════════════════════════════════════════════════

            var leftColumnCategories = new Dictionary<string, List<MenuItemConfig>>
            {
                // ═══════════════════════════════════════════════════════════════════════════
                // DZIAŁ ZAKUPÓW - KOLOR ZIELONY (gradient od jasnego #A5D6A7 do ciemnego #1B5E20)
                // ═══════════════════════════════════════════════════════════════════════════
                ["ZAOPATRZENIE I ZAKUPY"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("DaneHodowcy", "Baza Hodowców",
                        "Kompletna kartoteka wszystkich dostawców żywca kurczaków z danymi kontaktowymi i historią współpracy",
                        Color.FromArgb(165, 214, 167), // Jasny zielony #A5D6A7
                        () => new WidokKontrahenci(), "🧑‍🌾", "Hodowcy"),

                    new MenuItemConfig("WstawieniaHodowcy", "Cykle Wstawień",
                        "Rejestracja i monitorowanie cykli hodowlanych piskląt u hodowców wraz z terminami odbioru",
                        Color.FromArgb(129, 199, 132), // #81C784
                        () => new WidokWstawienia(), "🐣", "Wstawienia"),

                    new MenuItemConfig("TerminyDostawyZywca", "Kalendarz Dostaw Żywca",
                        "Interaktywny kalendarz planowania terminów dostaw żywca od hodowców do ubojni",
                        Color.FromArgb(102, 187, 106), // #66BB6A
                        () => new WidokKalendarza { UserID = App.UserID, WindowState = FormWindowState.Maximized }, "📅", "Dostawy Żywca"),

                    new MenuItemConfig("PlachtyAviloga", "Matryca Transportu",
                        "Zaawansowane planowanie tras transportu żywca z optymalizacją załadunku i wysyłką SMS",
                        Color.FromArgb(76, 175, 80), // #4CAF50
                        () => new WidokMatrycaWPF(), "🚛", "Matryca"),

                    new MenuItemConfig("PanelPortiera", "Panel Portiera",
                        "Dotykowy panel do rejestracji wag brutto i tary dostaw żywca przy wjeździe",
                        Color.FromArgb(85, 139, 47), // Zielony #558B2F (gradient zaopatrzenia)
                        () => new PanelPortiera(), "⚖️", "Portier"),

                    new MenuItemConfig("PanelLekarza", "Panel Lekarza",
                        "Ocena dobrostanu drobiu - padłe, konfiskaty CH/NW/ZM dla lekarza weterynarii",
                        Color.FromArgb(51, 105, 30), // Ciemniejszy zielony #33691E (gradient zaopatrzenia)
                        () => new PanelLekarza(), "🩺", "Lekarz Wet."),

                    new MenuItemConfig("Specyfikacje", "Specyfikacja Surowca",
                        "Definiowanie parametrów jakościowych surowca od poszczególnych dostawców żywca",
                        Color.FromArgb(67, 160, 71), // #43A047
                        () => new WidokSpecyfikacje(), "📋", "Specyfikacje"),

                    new MenuItemConfig("DokumentyZakupu", "Dokumenty i Umowy",
                        "Archiwum umów handlowych, certyfikatów i dokumentów związanych z zakupem żywca",
                        Color.FromArgb(56, 142, 60), // #388E3C
                        () => new SprawdzalkaUmow { UserID = App.UserID }, "📑", "Umowy"),

                    new MenuItemConfig("PlatnosciHodowcy", "Rozliczenia z Hodowcami",
                        "Monitorowanie należności i płatności dla dostawców żywca wraz z historią transakcji",
                        Color.FromArgb(46, 125, 50), // #2E7D32
                        () => new Platnosci(), "💵", "Płatności"),

                    new MenuItemConfig("ZakupPaszyPisklak", "Zakup Paszy i Piskląt",
                        "Ewidencja zakupów pasz i piskląt dla hodowców kontraktowych",
                        Color.FromArgb(27, 94, 32), // Ciemny zielony #1B5E20
                        null, "🌾", "Pasza"),

                    new MenuItemConfig("RaportyHodowcow", "Statystyki Hodowców",
                        "Raporty i analizy współpracy z hodowcami - wydajność, jakość, terminowość dostaw",
                        Color.FromArgb(27, 94, 32), // #1B5E20
                        () => new RaportyStatystykiWindow(), "📊", "Raporty Hodowców")
                },

                // ═══════════════════════════════════════════════════════════════════════════
                // DZIAŁ PRODUKCJI - KOLOR POMARAŃCZOWY (gradient od jasnego #FFCC80 do ciemnego #E65100)
                // ═══════════════════════════════════════════════════════════════════════════
                ["PRODUKCJA I MAGAZYN"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("ProdukcjaPodglad", "Panel Produkcji",
                        "Bieżący monitoring procesu uboju i krojenia z podglądem wydajności linii",
                        Color.FromArgb(255, 204, 128), // Jasny pomarańczowy #FFCC80
                        () => {
                            var window = new Kalendarz1.ProdukcjaPanel();
                            window.UserID = App.UserID;
                            return window;
                        }, "🏭", "Produkcja"),

                    new MenuItemConfig("KalkulacjaKrojenia", "Kalkulacja Rozbioru",
                        "Planowanie procesu krojenia tuszek z kalkulacją wydajności poszczególnych elementów",
                        Color.FromArgb(255, 183, 77), // #FFB74D
                        () => new PokazKrojenieMrozenie { WindowState = FormWindowState.Maximized }, "✂️", "Krojenie"),

                    new MenuItemConfig("PrzychodMrozni", "Magazyn Mroźni",
                        "Zarządzanie stanami magazynowymi produktów mrożonych z kontrolą partii i dat",
                        Color.FromArgb(255, 152, 0), // #FF9800
                        () => new Mroznia(), "❄️", "Mroźnia"),

                    new MenuItemConfig("LiczenieMagazynu", "Inwentaryzacja Magazynu",
                        "Codzienna rejestracja stanów magazynowych produktów gotowych i surowców",
                        Color.FromArgb(251, 140, 0), // #FB8C00
                        () => {
                            return new Kalendarz1.MagazynLiczenie.Formularze.LiczenieStanuWindow(
                                connectionString,
                                connectionHandel,
                                App.UserID
                            );
                        }, "📦", "Inwentaryzacja"),

                    new MenuItemConfig("PanelMagazyniera", "Panel Magazyniera",
                        "Kompleksowe narzędzie do zarządzania wydaniami towarów i dokumentacją magazynową",
                        Color.FromArgb(245, 124, 0), // #F57C00
                        () => {
                            var panel = new Kalendarz1.MagazynPanel();
                            panel.UserID = App.UserID;
                            return panel;
                        }, "🗃️", "Magazyn"),

                    new MenuItemConfig("AnalizaPrzychodu", "Analiza Przychodu",
                        "Kompleksowa analiza tempa produkcji, wydajności operatorów i przychodu towarów na godzinę",
                        Color.FromArgb(239, 108, 0), // #EF6C00
                        () => new AnalizaPrzychoduWindow(), "⏱️", "Przychody"),

                    new MenuItemConfig("AnalizaWydajnosci", "Analiza Wydajności",
                        "Porównanie masy żywca do masy tuszek - analiza strat i efektywności uboju",
                        Color.FromArgb(230, 81, 0), // Ciemny pomarańczowy #E65100
                        () => new AnalizaWydajnosciKrojenia(connectionHandel), "📈", "Wydajność")
                },

                // ═══════════════════════════════════════════════════════════════════════════
                // DZIAŁ ADMINISTRACJI - KOLOR CZERWONY (gradient od jasnego #EF9A9A do ciemnego #B71C1C)
                // ═══════════════════════════════════════════════════════════════════════════
                ["ADMINISTRACJA SYSTEMU"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("ZmianyUHodowcow", "Wnioski o Zmiany",
                        "Przeglądanie i zatwierdzanie wniosków o zmiany danych hodowców zgłoszonych przez użytkowników",
                        Color.FromArgb(239, 154, 154), // Jasny czerwony #EF9A9A
                        () => new AdminChangeRequestsForm(connectionString, App.UserID), "📝", "Zmiany Danych"),

                    new MenuItemConfig("AdminPermissions", "Zarządzanie Uprawnieniami",
                        "Panel administratora do nadawania i odbierania uprawnień dostępu użytkownikom systemu",
                        Color.FromArgb(183, 28, 28), // Ciemny czerwony #B71C1C
                        () => new AdminPermissionsForm(), "🔐", "Uprawnienia")
                }
            };

            var rightColumnCategories = new Dictionary<string, List<MenuItemConfig>>
            {
                // ═══════════════════════════════════════════════════════════════════════════
                // DZIAŁ SPRZEDAŻY - KOLOR NIEBIESKI (gradient od jasnego #90CAF9 do ciemnego #0D47A1)
                // ═══════════════════════════════════════════════════════════════════════════
                ["SPRZEDAŻ I CRM"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("CRM", "Relacje z Klientami",
                        "Zarządzanie relacjami z odbiorcami - kontakty, notatki, historia współpracy",
                        Color.FromArgb(144, 202, 249), // Jasny niebieski #90CAF9
                        () => new CRM.CRMWindow { UserID = App.UserID }, "🤝", "CRM"),

                    new MenuItemConfig("KartotekaOdbiorcow", "Kartoteka Odbiorców",
                        "Pełna baza danych klientów z danymi kontaktowymi, warunkami handlowymi i historią zamówień",
                        Color.FromArgb(100, 181, 246), // #64B5F6
                        () => {
                            var window = new Kalendarz1.KartotekaOdbiorcowWindow();
                            window.UserID = App.UserID;
                            return window;
                        }, "👤", "Klienci"),

                    new MenuItemConfig("ZamowieniaOdbiorcow", "Zamówienia Klientów",
                        "Przyjmowanie i realizacja zamówień na produkty mięsne od odbiorców hurtowych",
                        Color.FromArgb(66, 165, 245), // #42A5F5
                        () => {
                            var window = new Kalendarz1.WPF.MainWindow();
                            window.UserID = App.UserID;
                            return window;
                        }, "🛒", "Zamówienia"),

                    new MenuItemConfig("DashboardHandlowca", "Dashboard Handlowca",
                        "Kompleksowa analiza sprzedaży - wykresy, trendy, porównanie miesięczne, top odbiorcy",
                        Color.FromArgb(41, 121, 255), // Niebieski #2979FF (gradient sprzedaży)
                        () => new HandlowiecDashboardWindow(), "📊", "Dashboard"),

                    new MenuItemConfig("DokumentySprzedazy", "Faktury Sprzedaży",
                        "Przeglądanie i drukowanie faktur sprzedaży wraz z dokumentami WZ",
                        Color.FromArgb(33, 150, 243), // #2196F3
                        () => new WidokFakturSprzedazy { UserID = App.UserID }, "🧾", "Faktury"),

                    new MenuItemConfig("PanelFaktur", "Panel Faktur",
                        "Panel dla fakturzystki - przepisywanie zamówień do Symfonii Handel i tworzenie faktur",
                        Color.FromArgb(30, 136, 229), // #1E88E5
                        () => {
                            var window = new Kalendarz1.WPF.PanelFakturWindow();
                            window.UserID = App.UserID;
                            return window;
                        }, "📋", "Fakturowanie"),

                    new MenuItemConfig("OfertaCenowa", "Kreator Ofert",
                        "Tworzenie profesjonalnych ofert cenowych dla klientów z aktualnym cennikiem produktów",
                        Color.FromArgb(30, 136, 229), // #1E88E5
                        () => {
                            var window = new OfertaHandlowaWindow();
                            window.UserID = App.UserID;
                            return window;
                        }, "💰", "Oferty"),

                    new MenuItemConfig("ListaOfert", "Archiwum Ofert",
                        "Historia wszystkich wysłanych ofert handlowych z możliwością kopiowania i edycji",
                        Color.FromArgb(25, 118, 210), // #1976D2
                        () => {
                            var window = new OfertyListaWindow();
                            window.UserID = App.UserID;
                            return window;
                        }, "📂", "Archiwum Ofert"),

                    new MenuItemConfig("DashboardOfert", "Analiza Ofert",
                        "Statystyki skuteczności ofert - konwersja, wartości, porównania okresów",
                        Color.FromArgb(21, 101, 192), // #1565C0
                        () => {
                            return new OfertyDashboardWindow();
                        }, "📊", "Analiza Ofert"),

                    new MenuItemConfig("DashboardWyczerpalnosci", "Klasy Wagowe",
                        "Rozdzielanie dostępnych klas wagowych tuszek pomiędzy zamówienia klientów",
                        Color.FromArgb(13, 71, 161), // Ciemny niebieski #0D47A1
                        () => {
                            var window = new DashboardKlasWagowychWindow();
                            window.UserID = App.UserID;
                            return window;
                        }, "⚖️", "Klasy"),

                    new MenuItemConfig("PanelReklamacji", "Reklamacje Klientów",
                        "Rejestracja i obsługa reklamacji jakościowych zgłaszanych przez odbiorców",
                        Color.FromArgb(21, 101, 192), // #1565C0
                        () => new FormPanelReklamacjiWindow(connectionString, App.UserID), "⚠️", "Reklamacje")
                },

                // ═══════════════════════════════════════════════════════════════════════════
                // DZIAŁ PLANOWANIA - KOLOR FIOLETOWY (gradient od jasnego #CE93D8 do ciemnego #4A148C)
                // ═══════════════════════════════════════════════════════════════════════════
                ["PLANOWANIE I ANALIZY"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("PrognozyUboju", "Prognoza Uboju",
                        "Analiza średnich tygodniowych zakupów żywca z prognozą zapotrzebowania",
                        Color.FromArgb(206, 147, 216), // Jasny fioletowy #CE93D8
                        () => new PrognozyUboju.PrognozyUbojuWindow(), "🔮", "Prognozy"),

                    new MenuItemConfig("PlanTygodniowy", "Plan Tygodniowy",
                        "Harmonogram uboju i krojenia na nadchodzący tydzień z podziałem na dni",
                        Color.FromArgb(171, 71, 188), // #AB47BC
                        () => new Kalendarz1.TygodniowyPlan(), "🗓️", "Plan"),

                    new MenuItemConfig("AnalizaTygodniowa", "Dashboard Analityczny",
                        "Kompleksowa analiza bilansu produkcji i sprzedaży z wykresami i wskaźnikami",
                        Color.FromArgb(74, 20, 140), // Ciemny fioletowy #4A148C
                        () => new Kalendarz1.AnalizaTygodniowa.AnalizaTygodniowaWindow(), "📉", "Analizy")
                },

                // ═══════════════════════════════════════════════════════════════════════════
                // DZIAŁ OPAKOWAŃ - KOLOR TURKUSOWY (gradient od jasnego #80DEEA do ciemnego #006064)
                // ═══════════════════════════════════════════════════════════════════════════
                ["OPAKOWANIA I TRANSPORT"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("PodsumowanieSaldOpak", "Zestawienie Opakowań",
                        "Zbiorcze zestawienie sald opakowań zwrotnych wg typu z podsumowaniem wartości",
                        Color.FromArgb(128, 222, 234), // Jasny turkusowy #80DEEA
                        () => new ZestawienieOpakowanWindow(), "📦", "Opakowania"),

                    new MenuItemConfig("SaldaOdbiorcowOpak", "Salda Opakowań Klientów",
                        "Szczegółowe salda opakowań zwrotnych dla każdego kontrahenta z historią obrotów",
                        Color.FromArgb(0, 172, 193), // #00ACC1
                        () => new SaldaWszystkichOpakowanWindow(), "🏷️", "Salda Opak."),

                    new MenuItemConfig("UstalanieTranportu", "Planowanie Transportu",
                        "Organizacja tras dostaw do klientów z przydziałem pojazdów i kierowców",
                        Color.FromArgb(0, 96, 100), // Ciemny turkusowy #006064
                        () => {
                            var connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                            var repo = new Transport.Repozytorium.TransportRepozytorium(connTransport, connectionString);
                            return new Transport.Formularze.TransportMainFormImproved(repo, App.UserID);
                        }, "🚚", "Transport")
                },

                // ═══════════════════════════════════════════════════════════════════════════
                // DZIAŁ FINANSÓW - KOLOR SZARONIEBIESKI (gradient od jasnego #B0BEC5 do ciemnego #263238)
                // ═══════════════════════════════════════════════════════════════════════════
                ["FINANSE I ZARZĄDZANIE"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("DaneFinansowe", "Wyniki Finansowe",
                        "Zestawienie wyników finansowych firmy - przychody, koszty, marże i rentowność",
                        Color.FromArgb(176, 190, 197), // Jasny szaroniebieski #B0BEC5
                        () => new WidokSprzeZakup(), "💼", "Finanse"),

                    new MenuItemConfig("CentrumSpotkan", "Centrum Spotkań",
                        "Kompleksowe zarządzanie spotkaniami, powiadomienia, integracja Fireflies.ai, notatki ze spotkań",
                        Color.FromArgb(25, 118, 210), // Niebieski #1976D2
                        () => new Kalendarz1.Spotkania.Views.SpotkaniaGlowneWindow(App.UserID), "📅", "Spotkania"),

                    new MenuItemConfig("NotatkiZeSpotkan", "Notatki Służbowe",
                        "Rejestr notatek ze spotkań biznesowych, ustaleń i zadań do wykonania",
                        Color.FromArgb(38, 50, 56), // Ciemny szaroniebieski #263238
                        () => new Kalendarz1.NotatkiZeSpotkan.NotatkirGlownyWindow(App.UserID), "📝", "Notatki")
                },

                // ═══════════════════════════════════════════════════════════════════════════
                // NOWA KATEGORIA: KADRY I HR - KOLOR INDYGO/FIOLETOWY
                // ═══════════════════════════════════════════════════════════════════════════
                ["KADRY I HR"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("KontrolaGodzin", "Kontrola Czasu Pracy",
                        "System UNICARD - rejestracja wejść/wyjść, godziny pracy, obecności i raporty agencji",
                        Color.FromArgb(126, 87, 194), // Indygo #7E57C2
                        () => new KontrolaGodzinWindow(), "⏱️", "Czas Pracy")
                }
            };

            var leftPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true, WrapContents = false };
            PopulateColumn(leftPanel, leftColumnCategories);
            mainLayout.Controls.Add(leftPanel, 0, 0);

            var rightPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true, WrapContents = false };
            PopulateColumn(rightPanel, rightColumnCategories);
            mainLayout.Controls.Add(rightPanel, 1, 0);
        }

        private void PopulateColumn(FlowLayoutPanel columnPanel, Dictionary<string, List<MenuItemConfig>> categories)
        {
            foreach (var category in categories)
            {
                var permittedItems = category.Value.Where(item =>
                    (userPermissions.ContainsKey(item.ModuleName) && userPermissions[item.ModuleName])
                ).ToList();

                if (permittedItems.Any() || isAdmin)
                {
                    var categoryLabel = new Label
                    {
                        Text = "▎" + category.Key,
                        Font = new Font("Segoe UI", 14, FontStyle.Bold),
                        ForeColor = Color.FromArgb(45, 57, 69),
                        AutoSize = false,
                        Width = columnPanel.Width - 40,
                        Height = 40,
                        TextAlign = ContentAlignment.MiddleLeft,
                        Margin = new Padding(10, 20, 10, 5)
                    };
                    columnPanel.Controls.Add(categoryLabel);

                    var buttonsPanel = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        AutoSize = true,
                        Padding = new Padding(5, 0, 5, 10)
                    };

                    var itemsToDisplay = isAdmin ? category.Value : permittedItems;
                    foreach (var item in itemsToDisplay)
                    {
                        var buttonPanel = CreateMenuButton(item);
                        buttonsPanel.Controls.Add(buttonPanel);
                    }
                    columnPanel.Controls.Add(buttonsPanel);
                }
            }
        }

        private Panel CreateMenuButton(MenuItemConfig config)
        {
            // Użyj AnimatedTile zamiast zwykłego Panel - animacje hover, ripple, bounce
            var tile = new AnimatedTile(config.Color) { Tag = config };

            // Ikona emoji z animacją bounce
            var iconLabel = new Label
            {
                Text = config.IconText,
                Font = new Font("Segoe UI Emoji", 24),
                Size = new Size(50, 50),
                Location = new Point(15, 15),
                ForeColor = config.Color,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            // Tytuł kafelka
            var titleLabel = new Label
            {
                Text = config.DisplayName,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(55, 71, 79),
                Location = new Point(15, 65),
                AutoSize = true,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            // Opis kafelka
            var descriptionLabel = new Label
            {
                Text = config.Description,
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Gray,
                Location = new Point(15, 85),
                Size = new Size(150, 30),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            tile.Controls.Add(titleLabel);
            tile.Controls.Add(descriptionLabel);
            tile.Controls.Add(iconLabel);

            // Podłącz ikonę do efektu bounce
            tile.SetIconLabel(iconLabel);

            // Podłącz kliknięcia do wszystkich elementów potomnych
            Action<Control> attachClickEvent = null;
            attachClickEvent = (control) =>
            {
                control.Click += Panel_Click;
                foreach (Control child in control.Controls)
                {
                    attachClickEvent(child);
                }
            };
            attachClickEvent(tile);

            return tile;
        }

        private void Panel_Click(object sender, EventArgs e)
        {
            Control control = sender as Control;

            // Szukaj panelu z MenuItemConfig w Tag rekurencyjnie w górę hierarchii
            MenuItemConfig config = null;
            Control current = control;
            while (current != null)
            {
                if (current.Tag is MenuItemConfig foundConfig)
                {
                    config = foundConfig;
                    break;
                }
                current = current.Parent;
            }

            if (config != null)
            {
                try
                {
                    if (config.FormFactory != null)
                    {
                        var formularz = config.FormFactory();

                        if (formularz is System.Windows.Window wpfWindow)
                        {
                            // Ustaw krótki tytuł i ikonę dla okna WPF
                            wpfWindow.Title = config.ShortTitle;
                            var wpfIcon = CreateWpfEmojiIcon(config.IconText, config.Color);
                            if (wpfIcon != null)
                            {
                                wpfWindow.Icon = wpfIcon;
                            }
                            wpfWindow.ShowDialog();
                        }
                        else if (formularz is System.Windows.Forms.Form winForm)
                        {
                            // Ustaw krótki tytuł i ikonę dla okna WinForms
                            winForm.Text = config.ShortTitle;
                            var winIcon = CreateEmojiIcon(config.IconText, config.Color);
                            if (winIcon != null)
                            {
                                winForm.Icon = winIcon;
                            }
                            winForm.Show();
                        }
                        else if (formularz != null)
                        {
                            MessageBox.Show($"Nieobsługiwany typ okna: {formularz.GetType().Name}",
                                "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Funkcja '{config.DisplayName}' jest w trakcie rozwoju.",
                            "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas otwierania modułu: {ex.Message}\n\nSzczegóły: {ex.StackTrace}",
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Pokazuje powiększony avatar na chwilę
        /// </summary>
        private void ShowEnlargedAvatar(string odbiorcaId, string userName)
        {
            int enlargedSize = 250;

            var popup = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterScreen,
                Size = new Size(enlargedSize + 40, enlargedSize + 60),
                BackColor = Color.FromArgb(30, 40, 50),
                ShowInTaskbar = false,
                TopMost = true
            };

            // Zaokrąglone rogi
            popup.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(76, 175, 80), 3))
                {
                    e.Graphics.DrawRectangle(pen, 1, 1, popup.Width - 3, popup.Height - 3);
                }
            };

            // Panel na avatar
            var avatarPanel = new Panel
            {
                Size = new Size(enlargedSize, enlargedSize),
                Location = new Point(20, 15),
                BackColor = Color.Transparent
            };

            avatarPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // Biała obwódka
                using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 4))
                {
                    e.Graphics.DrawEllipse(pen, 2, 2, enlargedSize - 5, enlargedSize - 5);
                }

                try
                {
                    if (UserAvatarManager.HasAvatar(odbiorcaId))
                    {
                        using (var avatar = UserAvatarManager.GetAvatarRounded(odbiorcaId, enlargedSize - 10))
                        {
                            if (avatar != null)
                            {
                                e.Graphics.DrawImage(avatar, 5, 5, enlargedSize - 10, enlargedSize - 10);
                                return;
                            }
                        }
                    }
                }
                catch { }

                using (var defaultAvatar = UserAvatarManager.GenerateDefaultAvatar(userName, odbiorcaId, enlargedSize - 10))
                {
                    e.Graphics.DrawImage(defaultAvatar, 5, 5, enlargedSize - 10, enlargedSize - 10);
                }
            };
            popup.Controls.Add(avatarPanel);

            // Nazwa użytkownika pod avatarem
            var nameLabel = new Label
            {
                Text = userName,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(enlargedSize + 20, 25),
                Location = new Point(10, enlargedSize + 20),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            popup.Controls.Add(nameLabel);

            // Zamknij po kliknięciu
            popup.Click += (s, e) => popup.Close();
            avatarPanel.Click += (s, e) => popup.Close();
            nameLabel.Click += (s, e) => popup.Close();

            // Auto-zamknij po 3 sekundach
            var closeTimer = new Timer { Interval = 3000 };
            closeTimer.Tick += (s, e) =>
            {
                closeTimer.Stop();
                closeTimer.Dispose();
                if (!popup.IsDisposed)
                {
                    popup.Close();
                }
            };
            closeTimer.Start();

            // Animacja fade-in
            popup.Opacity = 0;
            var fadeTimer = new Timer { Interval = 15 };
            fadeTimer.Tick += (s, e) =>
            {
                if (popup.Opacity < 1)
                {
                    popup.Opacity += 0.1;
                }
                else
                {
                    fadeTimer.Stop();
                    fadeTimer.Dispose();
                }
            };
            fadeTimer.Start();

            popup.Show();
        }

        private void ApplyModernStyle()
        {
            this.BackColor = Color.FromArgb(236, 239, 241);
            this.Font = new Font("Segoe UI", 10);
        }

        private void AdminPanelButton_Click(object sender, EventArgs e)
        {
            var button = sender as Button;
            var contextMenu = new ContextMenuStrip();
            contextMenu.Font = new Font("Segoe UI", 10);
            contextMenu.BackColor = Color.White;

            // 1. Użytkownicy (uprawnienia)
            var usersItem = new ToolStripMenuItem("Użytkownicy (Uprawnienia)");
            usersItem.Image = CreateMenuItemImage("🔐");
            usersItem.Click += (s, args) =>
            {
                var adminForm = new AdminPermissionsForm();
                var adminIcon = CreateEmojiIcon("🔐", Color.FromArgb(183, 28, 28));
                if (adminIcon != null)
                {
                    adminForm.Icon = adminIcon;
                }
                adminForm.ShowDialog();
                LoadUserPermissions();
                SetupMenuItems();
            };
            contextMenu.Items.Add(usersItem);

            // 2. Cytaty
            var quotesItem = new ToolStripMenuItem("Cytaty motywacyjne");
            quotesItem.Image = CreateMenuItemImage("💬");
            quotesItem.Click += (s, args) =>
            {
                ShowQuotesManagementDialog();
            };
            contextMenu.Items.Add(quotesItem);

            // Pokaż menu pod przyciskiem
            if (button != null)
            {
                contextMenu.Show(button, new Point(0, button.Height));
            }
            else
            {
                contextMenu.Show(Cursor.Position);
            }
        }

        private System.Drawing.Image CreateMenuItemImage(string emoji)
        {
            try
            {
                var bmp = new Bitmap(20, 20);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    using (var font = new Font("Segoe UI Emoji", 12))
                    {
                        g.DrawString(emoji, font, Brushes.Black, -2, -2);
                    }
                }
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private void ShowNotepadDialog()
        {
            var form = new Form
            {
                Text = "Mój notatnik",
                Size = new Size(450, 400),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 40, 50)
            };

            var headerLabel = new Label
            {
                Text = "📝 Osobisty notatnik",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 15),
                AutoSize = true
            };
            form.Controls.Add(headerLabel);

            var infoLabel = new Label
            {
                Text = "Twoje notatki są zapisywane automatycznie",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(150, 160, 170),
                Location = new Point(20, 40),
                AutoSize = true
            };
            form.Controls.Add(infoLabel);

            var textBox = new TextBox
            {
                Location = new Point(20, 65),
                Size = new Size(395, 240),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(40, 50, 60),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = NotesManager.GetNotes(App.UserID)
            };
            form.Controls.Add(textBox);

            var saveButton = new Button
            {
                Text = "Zapisz",
                Location = new Point(230, 315),
                Size = new Size(90, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += (s, args) =>
            {
                if (NotesManager.SaveNotes(App.UserID, textBox.Text))
                {
                    MessageBox.Show("Notatki zostały zapisane!", "Sukces",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            form.Controls.Add(saveButton);

            var closeButton = new Button
            {
                Text = "Zamknij",
                Location = new Point(330, 315),
                Size = new Size(90, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 65, 75),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            closeButton.FlatAppearance.BorderSize = 0;
            form.Controls.Add(closeButton);

            // Auto-zapis przy zamknięciu
            form.FormClosing += (s, args) =>
            {
                NotesManager.SaveNotes(App.UserID, textBox.Text);
            };

            form.CancelButton = closeButton;
            form.ShowDialog();
        }

        private void ShowQuotesManagementDialog()
        {
            var form = new Form
            {
                Text = "Zarządzanie cytatami",
                Size = new Size(500, 400),
                StartPosition = FormStartPosition.CenterParent,
                Font = new Font("Segoe UI", 10),
                BackColor = Color.White,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var infoLabel = new Label
            {
                Text = $"Liczba cytatów: {QuotesManager.GetQuotesCount()}",
                Location = new Point(20, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            form.Controls.Add(infoLabel);

            // Przycisk Import
            var importButton = new Button
            {
                Text = "Importuj z pliku JSON",
                Location = new Point(20, 60),
                Size = new Size(200, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(92, 138, 58),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            importButton.FlatAppearance.BorderSize = 0;
            importButton.Click += (s, args) =>
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = "Wybierz plik z cytatami";
                    ofd.Filter = "Pliki JSON|*.json|Wszystkie pliki|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        var (success, count, error) = QuotesManager.ImportFromFile(ofd.FileName);
                        if (success)
                        {
                            infoLabel.Text = $"Liczba cytatów: {QuotesManager.GetQuotesCount()}";
                            MessageBox.Show($"Zaimportowano {count} nowych cytatów!", "Sukces",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show($"Błąd importu:\n{error}", "Błąd",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            };
            form.Controls.Add(importButton);

            // Przycisk Eksport
            var exportButton = new Button
            {
                Text = "Eksportuj do pliku",
                Location = new Point(240, 60),
                Size = new Size(200, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 73, 94),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            exportButton.FlatAppearance.BorderSize = 0;
            exportButton.Click += (s, args) =>
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Title = "Zapisz cytaty";
                    sfd.Filter = "Pliki JSON|*.json";
                    sfd.FileName = "cytaty.json";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        if (QuotesManager.ExportToFile(sfd.FileName))
                        {
                            MessageBox.Show("Cytaty zostały wyeksportowane!", "Sukces",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            };
            form.Controls.Add(exportButton);

            // Przycisk Dodaj cytat
            var addButton = new Button
            {
                Text = "Dodaj nowy cytat",
                Location = new Point(20, 120),
                Size = new Size(200, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            addButton.FlatAppearance.BorderSize = 0;
            addButton.Click += (s, args) =>
            {
                // Dialog do dodania cytatu z opcjonalnym autorem
                var inputForm = new Form
                {
                    Text = "Dodaj cytat",
                    Size = new Size(450, 250),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                var label = new Label
                {
                    Text = "Wpisz treść cytatu:",
                    Location = new Point(20, 20),
                    AutoSize = true
                };
                inputForm.Controls.Add(label);

                var textBox = new TextBox
                {
                    Location = new Point(20, 45),
                    Size = new Size(390, 60),
                    Multiline = true
                };
                inputForm.Controls.Add(textBox);

                var authorLabel = new Label
                {
                    Text = "Autor (opcjonalnie):",
                    Location = new Point(20, 115),
                    AutoSize = true
                };
                inputForm.Controls.Add(authorLabel);

                var authorTextBox = new TextBox
                {
                    Location = new Point(20, 140),
                    Size = new Size(390, 25)
                };
                inputForm.Controls.Add(authorTextBox);

                var okButton = new Button
                {
                    Text = "Dodaj",
                    Location = new Point(230, 175),
                    Size = new Size(80, 30),
                    DialogResult = DialogResult.OK
                };
                inputForm.Controls.Add(okButton);

                var cancelButton = new Button
                {
                    Text = "Anuluj",
                    Location = new Point(320, 175),
                    Size = new Size(80, 30),
                    DialogResult = DialogResult.Cancel
                };
                inputForm.Controls.Add(cancelButton);

                inputForm.AcceptButton = okButton;
                inputForm.CancelButton = cancelButton;

                if (inputForm.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(textBox.Text))
                {
                    var author = string.IsNullOrWhiteSpace(authorTextBox.Text) ? null : authorTextBox.Text.Trim();
                    if (QuotesManager.AddQuote(textBox.Text.Trim(), author))
                    {
                        infoLabel.Text = $"Liczba cytatów: {QuotesManager.GetQuotesCount()}";
                        MessageBox.Show("Cytat został dodany!", "Sukces",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };
            form.Controls.Add(addButton);

            // Przycisk Reset
            var resetButton = new Button
            {
                Text = "Przywróć domyślne",
                Location = new Point(240, 120),
                Size = new Size(200, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(231, 76, 60),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            resetButton.FlatAppearance.BorderSize = 0;
            resetButton.Click += (s, args) =>
            {
                if (MessageBox.Show("Czy na pewno chcesz przywrócić domyślne cytaty?\nWszystkie własne cytaty zostaną usunięte.",
                    "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    QuotesManager.ResetToDefaults();
                    infoLabel.Text = $"Liczba cytatów: {QuotesManager.GetQuotesCount()}";
                    MessageBox.Show("Przywrócono domyślne cytaty.", "Sukces",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            form.Controls.Add(resetButton);

            // Przycisk otwórz folder
            var folderButton = new Button
            {
                Text = "Otwórz folder z cytatami",
                Location = new Point(20, 180),
                Size = new Size(420, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(149, 165, 166),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            folderButton.FlatAppearance.BorderSize = 0;
            folderButton.Click += (s, args) =>
            {
                string path = QuotesManager.GetQuotesFilePath();
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", dir);
            };
            form.Controls.Add(folderButton);

            // Info o formacie
            var formatLabel = new Label
            {
                Text = "Format pliku JSON do importu:\n[{\"Text\": \"Treść cytatu\", \"Author\": \"Autor\"}, ...]",
                Location = new Point(20, 240),
                Size = new Size(440, 60),
                ForeColor = Color.Gray
            };
            form.Controls.Add(formatLabel);

            // Przycisk Zamknij
            var closeButton = new Button
            {
                Text = "Zamknij",
                Location = new Point(340, 310),
                Size = new Size(100, 35),
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            form.Controls.Add(closeButton);

            form.ShowDialog();
        }

        private void LogoutButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Czy na pewno chcesz się wylogować?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Application.Restart();
            }
        }

        private void MENU_Load(object sender, EventArgs e) { }

        #region Tworzenie ikon z emoji

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        /// <summary>
        /// Tworzy ikonę Windows Forms z emoji i kolorowym tłem (48x48 dla paska zadań)
        /// </summary>
        private Icon CreateEmojiIcon(string emoji, Color accentColor)
        {
            try
            {
                // Rozmiar 48x48 dla lepszej widoczności w pasku zadań Windows
                int size = 48;
                using (Bitmap bmp = new Bitmap(size, size))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                    // Rysuj kolorowe okrągłe tło
                    using (SolidBrush bgBrush = new SolidBrush(accentColor))
                    {
                        g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);
                    }

                    // Renderuj emoji na środku
                    if (!string.IsNullOrEmpty(emoji))
                    {
                        using (Font emojiFont = new Font("Segoe UI Emoji", 28, FontStyle.Regular, GraphicsUnit.Pixel))
                        {
                            var textSize = g.MeasureString(emoji, emojiFont);
                            float x = (size - textSize.Width) / 2;
                            float y = (size - textSize.Height) / 2;
                            g.DrawString(emoji, emojiFont, Brushes.White, x, y);
                        }
                    }

                    // Utwórz ikonę i sklonuj ją, aby przetrwała po dispose bitmapy
                    IntPtr hIcon = bmp.GetHicon();
                    using (Icon tempIcon = Icon.FromHandle(hIcon))
                    {
                        Icon clonedIcon = (Icon)tempIcon.Clone();
                        DestroyIcon(hIcon);
                        return clonedIcon;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tworzy ikonę WPF (BitmapSource) z emoji i kolorowym tłem (48x48 dla paska zadań)
        /// </summary>
        private BitmapSource CreateWpfEmojiIcon(string emoji, Color accentColor)
        {
            try
            {
                // Rozmiar 48x48 dla lepszej widoczności w pasku zadań Windows
                int size = 48;
                using (Bitmap bmp = new Bitmap(size, size))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                    // Rysuj kolorowe okrągłe tło
                    using (SolidBrush bgBrush = new SolidBrush(accentColor))
                    {
                        g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);
                    }

                    // Renderuj emoji na środku
                    if (!string.IsNullOrEmpty(emoji))
                    {
                        using (Font emojiFont = new Font("Segoe UI Emoji", 28, FontStyle.Regular, GraphicsUnit.Pixel))
                        {
                            var textSize = g.MeasureString(emoji, emojiFont);
                            float x = (size - textSize.Width) / 2;
                            float y = (size - textSize.Height) / 2;
                            g.DrawString(emoji, emojiFont, Brushes.White, x, y);
                        }
                    }

                    IntPtr hBitmap = bmp.GetHbitmap();
                    try
                    {
                        return Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap,
                            IntPtr.Zero,
                            System.Windows.Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                    }
                    finally
                    {
                        DeleteObject(hBitmap);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }

    public class MenuItemConfig
    {
        public string ModuleName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public Color Color { get; set; }
        public Func<object> FormFactory { get; set; }
        public string IconText { get; set; }
        public string ShortTitle { get; set; }

        public MenuItemConfig(string moduleName, string displayName, string description,
            Color color, Func<object> formFactory, string iconText = null, string shortTitle = null)
        {
            ModuleName = moduleName;
            DisplayName = displayName;
            Description = description;
            Color = color;
            FormFactory = formFactory;
            IconText = iconText;
            ShortTitle = shortTitle ?? displayName; // Domyślnie używa DisplayName jeśli ShortTitle nie podany
        }
    }
}
