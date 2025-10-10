using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class MENU : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private Dictionary<string, bool> userPermissions = new Dictionary<string, bool>();
        private bool isAdmin = false;
        private FlowLayoutPanel menuFlowPanel;
        private Panel headerPanel;
        private Panel sidePanel;
        private Label welcomeLabel;
        private Button adminPanelButton;

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
            // Główny layout
            this.WindowState = FormWindowState.Maximized;
            this.Text = "System Zarządzania - Menu Główne";

            // Panel górny
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100, // Zmniejszone z 120
                BackColor = Color.FromArgb(41, 53, 65)
            };

            // Logo - próba załadowania z zasobów
            try
            {
                PictureBox logo = new PictureBox
                {
                    Image = Properties.Resources.pm, // Jeśli masz zasób
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size(250, 70), // Zmniejszone
                    Location = new Point(20, 15)
                };
                headerPanel.Controls.Add(logo);
            }
            catch
            {
                // Jeśli brak zasobu, użyj tekstu
                Label logoLabel = new Label
                {
                    Text = "🏢 SYSTEM ZARZĄDZANIA",
                    Font = new Font("Segoe UI", 20, FontStyle.Bold),
                    ForeColor = Color.White,
                    Size = new Size(300, 40),
                    Location = new Point(20, 30)
                };
                headerPanel.Controls.Add(logoLabel);
            }

            // Label powitalny
            welcomeLabel = new Label
            {
                Text = $"👤 Zalogowany jako: {App.UserID}",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(this.Width - 300, 40)
            };
            headerPanel.Controls.Add(welcomeLabel);

            // Panel boczny dla admina
            sidePanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 200, // Zmniejszone z 250
                BackColor = Color.FromArgb(31, 43, 55),
                Visible = false
            };

            // Przycisk panelu administracyjnego
            adminPanelButton = new Button
            {
                Text = "⚙ Panel Admin",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(229, 57, 53),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(180, 40), // Zmniejszone
                Location = new Point(10, 15),
                Cursor = Cursors.Hand
            };
            adminPanelButton.FlatAppearance.BorderSize = 0;
            adminPanelButton.Click += AdminPanelButton_Click;
            sidePanel.Controls.Add(adminPanelButton);

            // Przycisk wylogowania
            Button logoutButton = new Button
            {
                Text = "🚪 Wyloguj",
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(76, 88, 100),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(180, 35), // Zmniejszone
                Location = new Point(10, 60),
                Cursor = Cursors.Hand
            };
            logoutButton.FlatAppearance.BorderSize = 0;
            logoutButton.Click += LogoutButton_Click;
            sidePanel.Controls.Add(logoutButton);

            // Panel główny z menu
            menuFlowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(15), // Zmniejszone z 30
                BackColor = Color.FromArgb(236, 239, 241)
            };

            this.Controls.Add(menuFlowPanel);
            this.Controls.Add(sidePanel);
            this.Controls.Add(headerPanel);
        }

        private void LoadUserPermissions()
        {
            string userId = App.UserID;
            isAdmin = (userId == "11111");

            if (isAdmin)
            {
                sidePanel.Visible = true;
                // Admin ma dostęp do wszystkiego
                LoadAllPermissions(true);
            }
            else
            {
                // Załaduj uprawnienia z kolumny Access w bazie
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

                        if (result != null && result.ToString().Length > 0)
                        {
                            string accessString = result.ToString();
                            ParseAccessString(accessString);
                        }
                        else
                        {
                            // Brak uprawnień - wszystko false
                            LoadAllPermissions(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania uprawnień: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                LoadAllPermissions(false);
            }
        }

        private void ParseAccessString(string accessString)
        {
            // Mapowanie pozycji w stringu Access na moduły
            // Na podstawie starego systemu
            var accessMap = new Dictionary<int, string>
            {
                [0] = "DaneHodowcy",           // Pozycja 0
                [1] = "ZakupPaszyPisklak",     // Pozycja 1
                [2] = "WstawieniaHodowcy",     // Pozycja 2
                [3] = "TerminyDostawyZywca",   // Pozycja 3
                [4] = "PlachtyAviloga",        // Pozycja 4
                [5] = "DokumentyZakupu",       // Pozycja 5
                [6] = "Specyfikacje",          // Pozycja 6
                [7] = "PlatnosciHodowcy",      // Pozycja 7
                [8] = "CRM",                   // Pozycja 8
                [9] = "ZamowieniaOdbiorcow",   // Pozycja 9
                [10] = "KalkulacjaKrojenia",   // Pozycja 10
                [11] = "PrzychodMrozni",       // Pozycja 11
                [12] = "DokumentySprzedazy",   // Pozycja 12
                [13] = "PodsumowanieSaldOpak", // Pozycja 13
                [14] = "SaldaOdbiorcowOpak",   // Pozycja 14
                [15] = "DaneFinansowe",        // Pozycja 15
                [16] = "UstalanieTranportu",   // Pozycja 16
                [17] = "ZmianyUHodowcow",      // Pozycja 17
                [18] = "ProdukcjaPodglad",     // Pozycja 18
                [19] = "OfertaCenowa"          // Pozycja 19
            };

            // Najpierw ustaw wszystkie na false
            LoadAllPermissions(false);

            // Parsuj string i ustaw uprawnienia
            for (int i = 0; i < accessString.Length && i < 20; i++)
            {
                if (accessMap.ContainsKey(i))
                {
                    userPermissions[accessMap[i]] = (accessString[i] == '1');
                }
            }
        }

        // USUŃ metodę AssignDefaultPermissions - nie będzie już potrzebna

        private void LoadUserSpecificPermissions(string userId)
        {
            // Zastąp całą metodę tą prostszą wersją
            LoadUserAccessFromDatabase(userId);
        }
        private void LoadAllPermissions(bool grantAll)
        {
            var allModules = GetAllModules();
            foreach (var module in allModules)
            {
                userPermissions[module] = grantAll;
            }
        }

        private List<string> GetAllModules()
        {
            return new List<string>
            {
                "DaneHodowcy", "ZakupPaszyPisklak", "WstawieniaHodowcy", "TerminyDostawyZywca",
                "PlachtyAviloga", "DokumentyZakupu", "Specyfikacje", "PlatnosciHodowcy",
                "CRM", "ZamowieniaOdbiorcow", "KalkulacjaKrojenia", "PrzychodMrozni",
                "DokumentySprzedazy", "PodsumowanieSaldOpak", "SaldaOdbiorcowOpak", "DaneFinansowe",
                "UstalanieTranportu", "ZmianyUHodowcow", "ProdukcjaPodglad", "OfertaCenowa"
            };
        }

        
      
        private void SetupMenuItems()
        {
            menuFlowPanel.Controls.Clear();

            // Główny panel z tabelą przycisków
            TableLayoutPanel mainTable = new TableLayoutPanel
            {
                ColumnCount = 6, // 6 kolumn dla kompaktowego układu
                AutoSize = true,
                Padding = new Padding(10),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };

            // Ustawienie szerokości kolumn
            for (int i = 0; i < 6; i++)
            {
                mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            }

            // Mapa obrazków dla modułów
            var moduleImages = GetModuleImages();

            // Kategorie menu
            var categories = new Dictionary<string, List<MenuItemConfig>>
            {
                ["📦 ZAKUP"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("DaneHodowcy", "Dane Hodowcy", Color.FromArgb(46, 125, 50), () => new WidokKontrahenci(), "📋"),
                    new MenuItemConfig("ZakupPaszyPisklak", "Zakup paszy", Color.FromArgb(67, 160, 71), null, "🌾"),
                    new MenuItemConfig("WstawieniaHodowcy", "Wstawienia", Color.FromArgb(76, 175, 80), () => new WidokWstawienia(), "🐣"),
                    new MenuItemConfig("TerminyDostawyZywca", "Terminy dostaw", Color.FromArgb(102, 187, 106),
                        () => new WidokKalendarza { UserID = App.UserID, WindowState = FormWindowState.Maximized }, "📅"),
                    new MenuItemConfig("DokumentyZakupu", "Dokumenty", Color.FromArgb(129, 199, 132),
                        () => new SprawdzalkaUmow { UserID = App.UserID }, "📄"),
                    new MenuItemConfig("PlatnosciHodowcy", "Płatności", Color.FromArgb(156, 204, 101), () => new Platnosci(), "💰"),
                    new MenuItemConfig("ZmianyUHodowcow", "Zmiany", Color.FromArgb(139, 195, 74),
                        () => new AdminChangeRequestsForm(connectionString, App.UserID), "✏️")
                },
                ["💼 SPRZEDAŻ"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("CRM", "CRM", Color.FromArgb(33, 150, 243), () => new CRM { UserID = App.UserID }, "👥"),
                    new MenuItemConfig("ZamowieniaOdbiorcow", "Zamówienia", Color.FromArgb(30, 136, 229),
                        () => new WidokZamowieniaPodsumowanie { UserID = App.UserID }, "📦"),
                    new MenuItemConfig("KalkulacjaKrojenia", "Krojenie", Color.FromArgb(25, 118, 210),
                        () => new PokazKrojenieMrozenie { WindowState = FormWindowState.Maximized }, "✂️"),
                    new MenuItemConfig("DokumentySprzedazy", "Faktury", Color.FromArgb(21, 101, 192),
                        () => new WidokFakturSprzedazy { UserID = App.UserID }, "🧾"),
                    new MenuItemConfig("OfertaCenowa", "Oferty", Color.FromArgb(13, 71, 161), () => new WidokOfertyHandlowej(), "💵")
                },
                ["🏭 MAGAZYN"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("PrzychodMrozni", "Mroźnia", Color.FromArgb(0, 172, 193), () => new Mroznia(), "❄️"),
                    new MenuItemConfig("PodsumowanieSaldOpak", "Salda zbiorcze", Color.FromArgb(0, 151, 167),
                        () => new WidokPojemnikiZestawienie(), "📊"),
                    new MenuItemConfig("SaldaOdbiorcowOpak", "Salda odbiorcy", Color.FromArgb(0, 131, 143), () => new WidokPojemniki(), "📈"),
                    new MenuItemConfig("Specyfikacje", "Specyfikacje", Color.FromArgb(0, 96, 100), () => new WidokSpecyfikacje(), "📝")
                },
                ["⚙️ POZOSTAŁE"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("PlachtyAviloga", "Aviloga", Color.FromArgb(255, 152, 0), () => new WidokMatryca(), "🎯"),
                    new MenuItemConfig("DaneFinansowe", "Finanse", Color.FromArgb(251, 140, 0), () => new WidokSprzeZakup(), "💼"),
                    new MenuItemConfig("UstalanieTranportu", "Transport", Color.FromArgb(245, 124, 0),
                        () => {
                            var connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                            var repo = new Transport.Repozytorium.TransportRepozytorium(connTransport, connectionString);
                            return new Transport.Formularze.TransportMainFormImproved(repo, App.UserID);
                        }, "🚚"),
                    new MenuItemConfig("ProdukcjaPodglad", "Produkcja", Color.FromArgb(230, 81, 0),
                        () => new WidokPanelProdukcjaNowy { UserID = App.UserID }, "🏭")
                }
            };

            int currentRow = 0;
            int currentCol = 0;

            foreach (var category in categories)
            {
                var hasAnyPermission = category.Value.Any(item =>
                    userPermissions.ContainsKey(item.ModuleName) && userPermissions[item.ModuleName]);

                if (hasAnyPermission || isAdmin)
                {
                    // Nagłówek kategorii
                    var categoryLabel = new Label
                    {
                        Text = category.Key,
                        Font = new Font("Segoe UI", 12, FontStyle.Bold),
                        ForeColor = Color.FromArgb(55, 71, 79),
                        AutoSize = false,
                        Size = new Size(1080, 30),
                        TextAlign = ContentAlignment.MiddleLeft,
                        BackColor = Color.FromArgb(220, 220, 220),
                        Padding = new Padding(5)
                    };

                    mainTable.Controls.Add(categoryLabel, 0, currentRow);
                    mainTable.SetColumnSpan(categoryLabel, 6);
                    currentRow++;
                    currentCol = 0;

                    foreach (var item in category.Value)
                    {
                        if (userPermissions.ContainsKey(item.ModuleName) && userPermissions[item.ModuleName] || isAdmin)
                        {
                            var button = CreateCompactMenuButton(item, moduleImages);
                            mainTable.Controls.Add(button, currentCol, currentRow);

                            currentCol++;
                            if (currentCol >= 6)
                            {
                                currentCol = 0;
                                currentRow++;
                            }
                        }
                    }

                    // Przejście do nowego wiersza po kategorii
                    if (currentCol != 0)
                    {
                        currentRow++;
                        currentCol = 0;
                    }
                    currentRow++; // Dodatkowy odstęp między kategoriami
                }
            }

            menuFlowPanel.Controls.Add(mainTable);
        }

        private Dictionary<string, Image> GetModuleImages()
        {
            var images = new Dictionary<string, Image>();

            // Spróbuj załadować obrazki z zasobów
            // Jeśli masz obrazki w Resources, odkomentuj odpowiednie linie
            try
            {
                // images["DaneHodowcy"] = Properties.Resources.kontrahenci;
                // images["WstawieniaHodowcy"] = Properties.Resources.wstawienia;
                // images["TerminyDostawyZywca"] = Properties.Resources.kalendarz;
                // images["PlatnosciHodowcy"] = Properties.Resources.platnosci;
                // images["CRM"] = Properties.Resources.crm;
                // images["ZamowieniaOdbiorcow"] = Properties.Resources.odbiorcy;
                // images["KalkulacjaKrojenia"] = Properties.Resources.krojenie;
                // images["DokumentySprzedazy"] = Properties.Resources.faktury;
                // images["PrzychodMrozni"] = Properties.Resources.mroznia;
                // images["PodsumowanieSaldOpak"] = Properties.Resources.pojemniki;
                // images["DaneFinansowe"] = Properties.Resources.finanse;
                // images["UstalanieTranportu"] = Properties.Resources.transport;
                // images["ProdukcjaPodglad"] = Properties.Resources.produkcja;
            }
            catch { }

            return images;
        }

        private Button CreateCompactMenuButton(MenuItemConfig config, Dictionary<string, Image> moduleImages)
        {
            var button = new Button
            {
                Size = new Size(170, 100), // Kompaktowy rozmiar
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = config.Color,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(5),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.BottomCenter,
                ImageAlign = ContentAlignment.TopCenter,
                Padding = new Padding(0, 10, 0, 5)
            };

            // Dodaj emoji lub obrazek
            if (moduleImages.ContainsKey(config.ModuleName) && moduleImages[config.ModuleName] != null)
            {
                button.Image = ResizeImage(moduleImages[config.ModuleName], 32, 32);
                button.Text = config.DisplayName;
            }
            else if (!string.IsNullOrEmpty(config.IconText))
            {
                // Użyj emoji jako tekstu
                button.Text = $"{config.IconText}\n{config.DisplayName}";
                button.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            }
            else
            {
                button.Text = config.DisplayName;
            }

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(config.Color, 0.1f);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(config.Color, 0.2f);

            button.Click += (sender, e) =>
            {
                try
                {
                    if (config.FormFactory != null)
                    {
                        var form = config.FormFactory();
                        form?.Show();
                    }
                    else
                    {
                        MessageBox.Show($"Funkcja '{config.DisplayName}' jest w trakcie rozwoju.",
                            "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas otwierania modułu: {ex.Message}",
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            return button;
        }

        private Image ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                using (var wrapMode = new System.Drawing.Imaging.ImageAttributes())
                {
                    wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        private void ApplyModernStyle()
        {
            this.BackColor = Color.FromArgb(236, 239, 241);
            this.Font = new Font("Segoe UI", 10, FontStyle.Regular);
        }

        private void AdminPanelButton_Click(object sender, EventArgs e)
        {
            var adminForm = new AdminPermissionsForm();
            adminForm.ShowDialog();
            LoadUserPermissions();
            SetupMenuItems();
        }

        private void LogoutButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Czy na pewno chcesz się wylogować?", "Potwierdzenie",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void MENU_Load(object sender, EventArgs e)
        {
            welcomeLabel.Location = new Point(this.Width - 350, 40);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (welcomeLabel != null)
            {
                welcomeLabel.Location = new Point(this.Width - 350, 40);
            }
        }
    }

    public class MenuItemConfig
    {
        public string ModuleName { get; set; }
        public string DisplayName { get; set; }
        public Color Color { get; set; }
        public Func<Form> FormFactory { get; set; }
        public string IconText { get; set; }

        public MenuItemConfig(string moduleName, string displayName, Color color, Func<Form> formFactory, string iconText = null)
        {
            ModuleName = moduleName;
            DisplayName = displayName;
            Color = color;
            FormFactory = formFactory;
            IconText = iconText;
        }
    }
}