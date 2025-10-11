using Kalendarz1.OfertaCenowa;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;


namespace Kalendarz1
{
    public partial class MENU : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private Dictionary<string, bool> userPermissions = new Dictionary<string, bool>();
        private bool isAdmin = false;
        private Panel headerPanel;
        private Panel sidePanel;
        private Label welcomeLabel;

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
            this.Text = "System Zarządzania - Piórkowscy";

            headerPanel = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = Color.FromArgb(45, 57, 69) };

            try
            {
                Label logoLabel = new Label { Text = "PIÓRKOWSCY", Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(20, 25) };
                headerPanel.Controls.Add(logoLabel);
            }
            catch { }

            welcomeLabel = new Label { Text = $"👤 Zalogowany jako: {App.UserID}", Font = new Font("Segoe UI", 12), ForeColor = Color.White, AutoSize = true };
            headerPanel.Controls.Add(welcomeLabel);

            sidePanel = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = Color.FromArgb(35, 45, 55), Visible = false };

            var adminPanelButton = new Button { Text = "⚙ Panel Administracyjny", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.FromArgb(229, 57, 53), FlatStyle = FlatStyle.Flat, Size = new Size(190, 45), Location = new Point(15, 20), Cursor = Cursors.Hand };
            adminPanelButton.FlatAppearance.BorderSize = 0;
            adminPanelButton.Click += AdminPanelButton_Click;
            sidePanel.Controls.Add(adminPanelButton);

            var logoutButton = new Button { Text = "🚪 Wyloguj", Font = new Font("Segoe UI", 10), ForeColor = Color.White, BackColor = Color.FromArgb(76, 88, 100), FlatStyle = FlatStyle.Flat, Size = new Size(190, 40), Location = new Point(15, 75), Cursor = Cursors.Hand };
            logoutButton.FlatAppearance.BorderSize = 0;
            logoutButton.Click += LogoutButton_Click;
            sidePanel.Controls.Add(logoutButton);

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
            this.Controls.Add(headerPanel);
        }

        private void LoadUserPermissions()
        {
            string userId = App.UserID;
            isAdmin = (userId == "11111");

            // First, reset all permissions to false. This is a failsafe.
            LoadAllPermissions(false);

            if (isAdmin)
            {
                sidePanel.Visible = true;
                // Grant all permissions only if the user is an admin
                LoadAllPermissions(true);
            }
            else
            {
                // Otherwise, load specific permissions from the database
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
                            // If user has no permissions string, LoadAllPermissions(false) has already been called.
                            MessageBox.Show("Użytkownik nie ma zdefiniowanych uprawnień. Dostęp został zablokowany.", "Brak uprawnień", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania uprawnień: {ex.Message}\n\nDostęp został zablokowany z powodu błędu.", "Błąd krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Ensure no permissions are granted on error
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
                [19] = "OfertaCenowa"
            };

            // The initial reset is now in LoadUserPermissions, so we just apply the grants here
            for (int i = 0; i < accessString.Length && i < accessMap.Count; i++)
            {
                if (accessMap.ContainsKey(i) && accessString[i] == '1')
                {
                    userPermissions[accessMap[i]] = true;
                }
            }
        }

        private void LoadAllPermissions(bool grantAll)
        {
            var allModules = GetAllModules();
            if (userPermissions.Count == 0)
            {
                // Initialize dictionary if it's empty
                foreach (var module in allModules)
                {
                    userPermissions.Add(module, grantAll);
                }
            }
            else
            {
                // Otherwise, just update values
                foreach (var module in allModules)
                {
                    userPermissions[module] = grantAll;
                }
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
            mainLayout.Controls.Clear();

            var leftColumnCategories = new Dictionary<string, List<MenuItemConfig>>
            {
                ["ZAOPATRZENIE I ZAKUPY"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("DaneHodowcy", "Dane Hodowcy", "Zarządzaj bazą hodowców", Color.FromArgb(46, 125, 50), () => new WidokKontrahenci(), "📋"),
                    new MenuItemConfig("ZakupPaszyPisklak", "Zakup Paszy", "Rejestruj zakupy paszy i piskląt", Color.FromArgb(67, 160, 71), null, "🌾"),
                    new MenuItemConfig("WstawieniaHodowcy", "Wstawienia", "Zarządzaj cyklami wstawień", Color.FromArgb(76, 175, 80), () => new WidokWstawienia(), "🐣"),
                    new MenuItemConfig("TerminyDostawyZywca", "Kalendarz Dostaw", "Planuj terminy dostaw żywca", Color.FromArgb(102, 187, 106), () => new WidokKalendarza { UserID = App.UserID, WindowState = FormWindowState.Maximized }, "📅"),
                    new MenuItemConfig("DokumentyZakupu", "Dokumenty Zakupu", "Archiwizuj dokumenty i umowy", Color.FromArgb(129, 199, 132), () => new SprawdzalkaUmow { UserID = App.UserID }, "📄"),
                    new MenuItemConfig("PlatnosciHodowcy", "Płatności", "Monitoruj płatności dla hodowców", Color.FromArgb(156, 204, 101), () => new Platnosci(), "💰"),
                    new MenuItemConfig("ZmianyUHodowcow", "Wnioski o Zmianę", "Zatwierdzaj zmiany w danych", Color.FromArgb(139, 195, 74), () => new AdminChangeRequestsForm(connectionString, App.UserID), "✏️"),
                    new MenuItemConfig("Specyfikacje", "Specyfikacja Surowca", "Definiuj specyfikacje produktów", Color.FromArgb(120, 144, 156), () => new WidokSpecyfikacje(), "📝"),
                    new MenuItemConfig("PlachtyAviloga", "Transport Avilog", "Zarządzaj transportem surowca", Color.FromArgb(120, 144, 156), () => new WidokMatryca(), "🎯")
                },
                ["PRODUKCJA I MAGAZYN"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("KalkulacjaKrojenia", "Kalkulacja Krojenia", "Planuj proces krojenia", Color.FromArgb(230, 81, 0), () => new PokazKrojenieMrozenie { WindowState = FormWindowState.Maximized }, "✂️"),
                    new MenuItemConfig("ProdukcjaPodglad", "Podgląd Produkcji", "Monitoruj bieżącą produkcję", Color.FromArgb(245, 124, 0), () => new WidokPanelProdukcjaNowy { UserID = App.UserID }, "🏭"),
                    new MenuItemConfig("PrzychodMrozni", "Mroźnia", "Zarządzaj stanami magazynowymi", Color.FromArgb(0, 172, 193), () => new Mroznia(), "❄️"),
                }
            };

            var rightColumnCategories = new Dictionary<string, List<MenuItemConfig>>
            {
                ["SPRZEDAŻ I CRM"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("CRM", "CRM", "Zarządzaj relacjami z klientami", Color.FromArgb(33, 150, 243), () => new CRM { UserID = App.UserID }, "👥"),
                    new MenuItemConfig("ZamowieniaOdbiorcow", "Zamówienia Mięsa", "Przeglądaj i zarządzaj zamówieniami", Color.FromArgb(30, 136, 229), () => new WidokZamowieniaPodsumowanie { UserID = App.UserID }, "📦"),
                    new MenuItemConfig("DokumentySprzedazy", "Faktury Sprzedaży", "Generuj i przeglądaj faktury", Color.FromArgb(21, 101, 192), () => new WidokFakturSprzedazy { UserID = App.UserID }, "🧾"),
                    new MenuItemConfig("OfertaCenowa", "Oferty Handlowe", "Twórz i zarządzaj ofertami", Color.FromArgb(13, 71, 161), () => new OfertaHandlowaWindow(), "💵")
                },
                ["OPAKOWANIA I TRANSPORT"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("PodsumowanieSaldOpak", "Salda Zbiorcze", "Analizuj zbiorcze salda opakowań", Color.FromArgb(0, 151, 167), () => new WidokPojemnikiZestawienie(), "📊"),
                    new MenuItemConfig("SaldaOdbiorcowOpak", "Salda Odbiorcy", "Sprawdzaj salda dla odbiorców", Color.FromArgb(0, 131, 143), () => new WidokPojemniki(), "📈"),
                    new MenuItemConfig("UstalanieTranportu", "Transport", "Organizuj i planuj transport", Color.FromArgb(255, 111, 0), () => { var connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True"; var repo = new Transport.Repozytorium.TransportRepozytorium(connTransport, connectionString); return new Transport.Formularze.TransportMainFormImproved(repo, App.UserID); }, "🚚")
                },
                ["FINANSE I ZARZĄDZANIE"] = new List<MenuItemConfig>
                {
                    new MenuItemConfig("DaneFinansowe", "Wynik Finansowy", "Analizuj dane finansowe firmy", Color.FromArgb(96, 125, 139), () => new WidokSprzeZakup(), "💼")
                }
            };

            var leftPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true, WrapContents = false };
            PopulateColumn(leftPanel, leftColumnCategories);
            mainLayout.Controls.Add(leftPanel, 0, 0);

            var rightPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true, WrapContents = false };
            PopulateColumn(rightPanel, rightColumnCategories);
            mainLayout.Controls.Add(rightPanel, 1, 0);
        }

        // --- KLUCZOWA POPRAWKA LOGIKI ---
        private void PopulateColumn(FlowLayoutPanel columnPanel, Dictionary<string, List<MenuItemConfig>> categories)
        {
            foreach (var category in categories)
            {
                // 1. Znajdź wszystkie moduły w tej kategorii, do których użytkownik FAKTYCZNIE ma dostęp.
                var permittedItems = category.Value.Where(item =>
                    (userPermissions.ContainsKey(item.ModuleName) && userPermissions[item.ModuleName])
                ).ToList();

                // 2. Jeśli istnieje chociaż jeden taki moduł (LUB jeśli użytkownik jest adminem), wyświetl całą kategorię.
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

                    // 3. Wyświetl przyciski TYLKO dla dozwolonych modułów (lub wszystkich, jeśli to admin).
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
            var panel = new Panel { Size = new Size(180, 120), BackColor = Color.White, Margin = new Padding(10), Cursor = Cursors.Hand, Tag = config };
            var bottomBorder = new Panel { Height = 5, Dock = DockStyle.Bottom, BackColor = config.Color };
            var iconLabel = new Label { Text = config.IconText, Font = new Font("Segoe UI Emoji", 24), Size = new Size(50, 50), Location = new Point(15, 15), ForeColor = config.Color };
            var titleLabel = new Label { Text = config.DisplayName, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(55, 71, 79), Location = new Point(15, 65), AutoSize = true };
            var descriptionLabel = new Label { Text = config.Description, Font = new Font("Segoe UI", 8), ForeColor = Color.Gray, Location = new Point(15, 85), Size = new Size(150, 30) };
            panel.Controls.Add(titleLabel);
            panel.Controls.Add(descriptionLabel);
            panel.Controls.Add(iconLabel);
            panel.Controls.Add(bottomBorder);
            panel.Paint += (sender, e) => {
                ControlPaint.DrawBorder(e.Graphics, panel.ClientRectangle,
                    Color.FromArgb(220, 220, 220), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(220, 220, 220), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(220, 220, 220), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(220, 220, 220), 1, ButtonBorderStyle.Solid);
            };

            Action<Control> attachClickEvent = null;
            attachClickEvent = (control) =>
            {
                control.Click += Panel_Click;
                foreach (Control child in control.Controls)
                {
                    attachClickEvent(child);
                }
            };
            panel.MouseEnter += (s, e) => panel.BackColor = Color.FromArgb(248, 249, 250);
            panel.MouseLeave += (s, e) => panel.BackColor = Color.White;
            attachClickEvent(panel);
            return panel;
        }

        private void Panel_Click(object sender, EventArgs e)
        {
            Control control = sender as Control;
            Panel panel = control as Panel ?? control.Parent as Panel;
            if (panel?.Tag is MenuItemConfig config)
            {
                try
                {
                    if (config.FormFactory != null)
                    {
                        var formularz = config.FormFactory();

                        // Obsługa okna WPF
                        if (formularz is System.Windows.Window wpfWindow)
                        {
                            wpfWindow.ShowDialog(); // lub .Show()
                        }
                        // Obsługa formularza WinForms
                        else if (formularz is System.Windows.Forms.Form winForm)
                        {
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
        private void ApplyModernStyle()
        {
            this.BackColor = Color.FromArgb(236, 239, 241);
            this.Font = new Font("Segoe UI", 10);
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
            if (MessageBox.Show("Czy na pewno chcesz się wylogować?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Application.Restart();
            }
        }

        private void MENU_Load(object sender, EventArgs e) => HandleResize();

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            HandleResize();
        }

        private void HandleResize()
        {
            if (welcomeLabel != null)
            {
                welcomeLabel.Location = new Point(this.Width - welcomeLabel.Width - 40, (headerPanel.Height - welcomeLabel.Height) / 2);
            }
        }
    }

    public class MenuItemConfig
    {
        public string ModuleName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public Color Color { get; set; }
        public Func<object> FormFactory { get; set; } // ← ZMIANA: Form → object
        public string IconText { get; set; }

        public MenuItemConfig(string moduleName, string displayName, string description,
            Color color, Func<object> formFactory, string iconText = null) // ← ZMIANA
        {
            ModuleName = moduleName;
            DisplayName = displayName;
            Description = description;
            Color = color;
            FormFactory = formFactory;
            IconText = iconText;
        }
    }
}