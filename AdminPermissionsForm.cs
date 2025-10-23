using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class AdminPermissionsForm : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string handelConnectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private DataGridView usersGrid;
        private DataGridView permissionsGrid;
        private ComboBox userComboBox;
        private ComboBox categoryFilterCombo;
        private TextBox searchBox;
        private Button saveButton, refreshButton, addUserButton, deleteUserButton;
        private Button manageHandlowcyButton;
        private Panel topPanel, leftPanel, rightPanel, bottomPanel;
        private Label titleLabel, usersCountLabel;
        private string selectedUserId;
        private FlowLayoutPanel categoryCheckboxesPanel;
        private ProgressBar loadingBar;

        private static class Colors
        {
            public static readonly Color Primary = ColorTranslator.FromHtml("#5C8A3A");
            public static readonly Color PrimaryDark = ColorTranslator.FromHtml("#4B732F");
            public static readonly Color PrimaryLight = ColorTranslator.FromHtml("#E0F0D6");
            public static readonly Color TextDark = ColorTranslator.FromHtml("#2C3E50");
            public static readonly Color TextGray = ColorTranslator.FromHtml("#7F8C8D");
            public static readonly Color Border = ColorTranslator.FromHtml("#BDC3C7");
            public static readonly Color Background = ColorTranslator.FromHtml("#ECF0F1");
            public static readonly Color Success = ColorTranslator.FromHtml("#27AE60");
            public static readonly Color Danger = ColorTranslator.FromHtml("#E74C3C");
            public static readonly Color Warning = ColorTranslator.FromHtml("#F39C12");
            public static readonly Color Info = ColorTranslator.FromHtml("#3498DB");
        }

        public AdminPermissionsForm()
        {
            InitializeComponent();
            InitializeCustomComponents();
            UserHandlowcyManager.CreateTableIfNotExists();
            LoadUsers();
        }

        private void InitializeComponent()
        {
            this.Text = "Panel Administracyjny - Zarządzanie Uprawnieniami";
            this.Size = new Size(1600, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Colors.Background;
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
        }

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private void InitializeCustomComponents()
        {
            // TOP PANEL
            topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = Colors.Primary
            };

            titleLabel = new Label
            {
                Text = "⚙️ PANEL ADMINISTRACYJNY",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(35, 20)
            };
            topPanel.Controls.Add(titleLabel);

            var subtitleLabel = new Label
            {
                Text = "Zarządzanie użytkownikami, uprawnieniami i przypisanymi handlowcami",
                Font = new Font("Segoe UI", 11),
                ForeColor = Colors.PrimaryLight,
                AutoSize = true,
                Location = new Point(38, 55)
            };
            topPanel.Controls.Add(subtitleLabel);

            var closeButton = new Button
            {
                Text = "✕",
                Size = new Size(40, 40),
                Location = new Point(this.Width - 60, 25),
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 16),
                Cursor = Cursors.Hand
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Colors.Danger;
            closeButton.Click += (s, e) => this.Close();
            topPanel.Controls.Add(closeButton);

            // LEFT PANEL
            leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 450,
                BackColor = Color.White,
                Padding = new Padding(25, 20, 25, 20)
            };

            var usersLabel = new Label
            {
                Text = "👥 UŻYTKOWNICY SYSTEMU",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Colors.TextDark,
                Location = new Point(25, 20),
                AutoSize = true
            };
            leftPanel.Controls.Add(usersLabel);

            usersCountLabel = new Label
            {
                Text = "Ładowanie...",
                Font = new Font("Segoe UI", 9),
                ForeColor = Colors.TextGray,
                Location = new Point(28, 48),
                AutoSize = true
            };
            leftPanel.Controls.Add(usersCountLabel);

            searchBox = new TextBox
            {
                Location = new Point(25, 75),
                Size = new Size(400, 35),
                Font = new Font("Segoe UI", 12),
                PlaceholderText = "🔍 Szukaj użytkownika (ID lub nazwa)...",
                BorderStyle = BorderStyle.FixedSingle
            };
            searchBox.TextChanged += SearchBox_TextChanged;
            leftPanel.Controls.Add(searchBox);

            usersGrid = CreateStyledGrid(new Point(25, 120), new Size(400, 400));
            usersGrid.SelectionChanged += UsersGrid_SelectionChanged;
            usersGrid.DataBindingComplete += UsersGrid_DataBindingComplete;
            leftPanel.Controls.Add(usersGrid);

            var buttonY = 530;
            addUserButton = CreateStyledButton("➕ Nowy użytkownik", Colors.Success,
                new Point(25, buttonY), new Size(195, 48));
            addUserButton.Click += AddUserButton_Click;
            leftPanel.Controls.Add(addUserButton);

            deleteUserButton = CreateStyledButton("🗑️ Usuń użytkownika", Colors.Danger,
                new Point(230, buttonY), new Size(195, 48));
            deleteUserButton.Click += DeleteUserButton_Click;
            leftPanel.Controls.Add(deleteUserButton);

            manageHandlowcyButton = CreateStyledButton("👔 Zarządzaj handlowcami", ColorTranslator.FromHtml("#9B59B6"),
                new Point(25, 585), new Size(400, 48));
            manageHandlowcyButton.Click += ManageHandlowcyButton_Click;
            leftPanel.Controls.Add(manageHandlowcyButton);

            loadingBar = new ProgressBar
            {
                Location = new Point(25, 638),
                Size = new Size(400, 6),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Visible = false
            };
            leftPanel.Controls.Add(loadingBar);

            // RIGHT PANEL
            rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Colors.Background,
                Padding = new Padding(25, 20, 25, 20)
            };

            var permissionsLabel = new Label
            {
                Text = "🔐 UPRAWNIENIA UŻYTKOWNIKA",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Colors.TextDark,
                Location = new Point(25, 20),
                AutoSize = true
            };
            rightPanel.Controls.Add(permissionsLabel);

            userComboBox = new ComboBox
            {
                Location = new Point(25, 55),
                Size = new Size(350, 35),
                Font = new Font("Segoe UI", 11),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false,
                FlatStyle = FlatStyle.Flat
            };
            rightPanel.Controls.Add(userComboBox);

            // Filtr kategorii
            var filterLabel = new Label
            {
                Text = "Filtruj:",
                Font = new Font("Segoe UI", 10),
                ForeColor = Colors.TextDark,
                Location = new Point(390, 58),
                AutoSize = true
            };
            rightPanel.Controls.Add(filterLabel);

            categoryFilterCombo = new ComboBox
            {
                Location = new Point(450, 55),
                Size = new Size(200, 35),
                Font = new Font("Segoe UI", 11),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            categoryFilterCombo.Items.AddRange(new object[] { "Wszystkie kategorie", "Zaopatrzenie i Zakupy", "Produkcja i Magazyn", "Sprzedaż i CRM", "Opakowania i Transport", "Finanse i Zarządzanie" });
            categoryFilterCombo.SelectedIndex = 0;
            categoryFilterCombo.SelectedIndexChanged += CategoryFilter_Changed;
            rightPanel.Controls.Add(categoryFilterCombo);

            // Category checkboxes
            categoryCheckboxesPanel = new FlowLayoutPanel
            {
                Location = new Point(665, 55),
                Size = new Size(450, 80),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            var categoryLabel = new Label
            {
                Text = "⚡ Szybkie zaznaczanie:",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Colors.TextDark,
                AutoSize = true,
                Margin = new Padding(5, 10, 15, 5)
            };
            categoryCheckboxesPanel.Controls.Add(categoryLabel);

            CreateCategoryCheckbox("Zaopatrzenie i Zakupy", Colors.Success);
            CreateCategoryCheckbox("Produkcja i Magazyn", Colors.Warning);
            CreateCategoryCheckbox("Sprzedaż i CRM", Colors.Info);
            CreateCategoryCheckbox("Opakowania i Transport", ColorTranslator.FromHtml("#00BCD4"));
            CreateCategoryCheckbox("Finanse i Zarządzanie", Colors.TextGray);

            rightPanel.Controls.Add(categoryCheckboxesPanel);

            permissionsGrid = CreateStyledGrid(new Point(25, 145), new Size(1090, 430));
            permissionsGrid.CellContentClick += PermissionsGrid_CellContentClick;
            permissionsGrid.CurrentCellDirtyStateChanged += PermissionsGrid_CurrentCellDirtyStateChanged;
            rightPanel.Controls.Add(permissionsGrid);

            // Bottom buttons
            bottomPanel = new Panel
            {
                Location = new Point(25, 585),
                Size = new Size(1090, 60),
                BackColor = Color.Transparent
            };

            saveButton = CreateStyledButton("💾 Zapisz zmiany", Colors.Primary,
                new Point(0, 0), new Size(200, 55));
            saveButton.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            saveButton.Click += SaveButton_Click;
            bottomPanel.Controls.Add(saveButton);

            refreshButton = CreateStyledButton("🔄 Odśwież", Colors.TextGray,
                new Point(210, 0), new Size(170, 55));
            refreshButton.Click += RefreshButton_Click;
            bottomPanel.Controls.Add(refreshButton);

            var selectAllButton = CreateStyledButton("✓ Zaznacz wszystkie", Colors.Success,
                new Point(390, 0), new Size(200, 55));
            selectAllButton.Click += (s, e) => SetAllPermissions(true);
            bottomPanel.Controls.Add(selectAllButton);

            var deselectAllButton = CreateStyledButton("✗ Odznacz wszystkie", Colors.Danger,
                new Point(600, 0), new Size(200, 55));
            deselectAllButton.Click += (s, e) => SetAllPermissions(false);
            bottomPanel.Controls.Add(deselectAllButton);

            rightPanel.Controls.Add(bottomPanel);

            this.Controls.Add(rightPanel);
            this.Controls.Add(leftPanel);
            this.Controls.Add(topPanel);
        }

        private DataGridView CreateStyledGrid(Point location, Size size)
        {
            var grid = new DataGridView
            {
                Location = location,
                Size = size,
                Font = new Font("Segoe UI", 10),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = Colors.Primary;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(5);
            grid.ColumnHeadersHeight = 40;
            grid.DefaultCellStyle.SelectionBackColor = Colors.PrimaryLight;
            grid.DefaultCellStyle.SelectionForeColor = Colors.TextDark;
            grid.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#F8F9FA");
            grid.RowTemplate.Height = 35;

            return grid;
        }

        private Button CreateStyledButton(string text, Color color, Point location, Size size)
        {
            var button = new Button
            {
                Text = text,
                Size = size,
                Location = location,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = color,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(color, 0.15f);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(color, 0.25f);

            return button;
        }

        private void CreateCategoryCheckbox(string categoryName, Color color)
        {
            var checkbox = new CheckBox
            {
                Text = categoryName.Replace(" i ", " "),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = color,
                AutoSize = true,
                Margin = new Padding(8, 8, 8, 8),
                Cursor = Cursors.Hand
            };
            checkbox.CheckedChanged += (s, e) => CategoryCheckbox_CheckedChanged(categoryName, checkbox.Checked);
            categoryCheckboxesPanel.Controls.Add(checkbox);
        }

        private void CategoryCheckbox_CheckedChanged(string categoryName, bool isChecked)
        {
            if (permissionsGrid.DataSource == null) return;

            var modulesInCategory = GetModulesByCategory(categoryName);
            foreach (DataGridViewRow row in permissionsGrid.Rows)
            {
                string moduleName = row.Cells["Moduł"].Value?.ToString();
                if (modulesInCategory.Contains(moduleName))
                {
                    row.Cells["Dostęp"].Value = isChecked;
                }
            }
        }

        private void CategoryFilter_Changed(object sender, EventArgs e)
        {
            if (permissionsGrid.DataSource is DataTable dt)
            {
                string selectedCategory = categoryFilterCombo.SelectedItem?.ToString();
                if (selectedCategory == "Wszystkie kategorie")
                {
                    dt.DefaultView.RowFilter = "";
                }
                else
                {
                    var modulesInCategory = GetModulesByCategory(selectedCategory);
                    string filter = string.Join(" OR ", modulesInCategory.Select(m => $"Moduł = '{m}'"));
                    dt.DefaultView.RowFilter = filter;
                }
            }
        }

        private List<string> GetModulesByCategory(string categoryName)
        {
            var categories = new Dictionary<string, List<string>>
            {
                ["Zaopatrzenie i Zakupy"] = new List<string> {
                    "DaneHodowcy", "ZakupPaszyPisklak", "WstawieniaHodowcy",
                    "TerminyDostawyZywca", "DokumentyZakupu", "PlatnosciHodowcy",
                    "ZmianyUHodowcow", "Specyfikacje", "PlachtyAviloga"
                },
                ["Produkcja i Magazyn"] = new List<string> {
                    "KalkulacjaKrojenia", "ProdukcjaPodglad", "PrzychodMrozni", "LiczenieMagazynu", "PanelMagazyniera"
                },
                ["Sprzedaż i CRM"] = new List<string> {
                    "CRM", "ZamowieniaOdbiorcow", "DokumentySprzedazy",
                    "PrognozyUboju", "PlanTygodniowy", "AnalizaTygodniowa", "OfertaCenowa"
                },
                ["Opakowania i Transport"] = new List<string> {
                    "PodsumowanieSaldOpak", "SaldaOdbiorcowOpak", "UstalanieTranportu"
                },
                ["Finanse i Zarządzanie"] = new List<string> {
                    "DaneFinansowe", "NotatkiZeSpotkan"
                }
            };
            return categories.ContainsKey(categoryName) ? categories[categoryName] : new List<string>();
        }

        private void LoadUsers()
        {
            loadingBar.Visible = true;
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            o.ID, 
                            o.Name,
                            STUFF((
                                SELECT ', ' + uh.HandlowiecName
                                FROM UserHandlowcy uh
                                WHERE uh.UserID = o.ID
                                ORDER BY uh.HandlowiecName
                                FOR XML PATH('')
                            ), 1, 2, '') AS Handlowcy
                        FROM operators o
                        ORDER BY o.ID";

                    SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    usersGrid.DataSource = dt;
                    usersCountLabel.Text = $"Liczba użytkowników: {dt.Rows.Count}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania użytkowników:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                loadingBar.Visible = false;
            }
        }

        private void UsersGrid_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            // Ustaw szerokości kolumn po zakończeniu bindowania
            if (usersGrid.Columns.Count > 0)
            {
                if (usersGrid.Columns.Contains("ID"))
                {
                    usersGrid.Columns["ID"].HeaderText = "ID Użytkownika";
                    usersGrid.Columns["ID"].Width = 100;
                }

                if (usersGrid.Columns.Contains("Name"))
                {
                    usersGrid.Columns["Name"].HeaderText = "Nazwa";
                    usersGrid.Columns["Name"].Width = 150;
                }

                if (usersGrid.Columns.Contains("Handlowcy"))
                {
                    usersGrid.Columns["Handlowcy"].HeaderText = "Przypisani handlowcy";
                    usersGrid.Columns["Handlowcy"].Width = 150;
                }
            }

            // Wybierz pierwszy wiersz jeśli istnieje
            if (usersGrid.Rows.Count > 0)
            {
                usersGrid.Rows[0].Selected = true;
            }
        }

        private void LoadPermissions(string userId)
        {
            try
            {
                var modules = GetModulesList();
                var permissions = new DataTable();
                permissions.Columns.Add("Ikona", typeof(string));
                permissions.Columns.Add("Kategoria", typeof(string));
                permissions.Columns.Add("Moduł", typeof(string));
                permissions.Columns.Add("Opis", typeof(string));
                permissions.Columns.Add("Dostęp", typeof(bool));

                var accessMap = GetAccessMap();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT Access FROM operators WHERE ID = @userId";
                    string accessString = "";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        var result = cmd.ExecuteScalar();
                        if (result != null) accessString = result.ToString();
                    }

                    foreach (var module in modules)
                    {
                        bool hasAccess = false;
                        var position = accessMap.FirstOrDefault(x => x.Value == module.Key).Key;
                        if (position >= 0 && position < accessString.Length)
                            hasAccess = accessString[position] == '1';

                        permissions.Rows.Add(module.Icon, module.Category, module.DisplayName, module.Description, hasAccess);
                    }
                }

                permissionsGrid.DataSource = permissions;
                if (permissionsGrid.Columns.Count > 0)
                {
                    // Kolumna Ikona
                    permissionsGrid.Columns["Ikona"].Width = 50;
                    permissionsGrid.Columns["Ikona"].ReadOnly = true;
                    permissionsGrid.Columns["Ikona"].DefaultCellStyle.Font = new Font("Segoe UI Emoji", 14);
                    permissionsGrid.Columns["Ikona"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                    permissionsGrid.Columns["Kategoria"].Width = 180;
                    permissionsGrid.Columns["Kategoria"].ReadOnly = true;

                    permissionsGrid.Columns["Moduł"].Width = 200;
                    permissionsGrid.Columns["Moduł"].ReadOnly = true;

                    permissionsGrid.Columns["Opis"].Width = 450;
                    permissionsGrid.Columns["Opis"].ReadOnly = true;

                    permissionsGrid.Columns["Dostęp"].Width = 100;
                    permissionsGrid.Columns["Dostęp"].ReadOnly = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania uprawnień:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private List<ModuleInfo> GetModulesList()
        {
            return new List<ModuleInfo>
            {
                // Zaopatrzenie i Zakupy
                new ModuleInfo("DaneHodowcy", "Dane Hodowcy", "Zarządzanie danymi hodowców", "Zaopatrzenie i Zakupy", "📋"),
                new ModuleInfo("ZakupPaszyPisklak", "Zakup Paszy", "Zakup paszy i piskląt", "Zaopatrzenie i Zakupy", "🌾"),
                new ModuleInfo("WstawieniaHodowcy", "Wstawienia", "Rejestracja wstawień u hodowców", "Zaopatrzenie i Zakupy", "🐣"),
                new ModuleInfo("TerminyDostawyZywca", "Kalendarz Dostaw", "Planuj terminy dostaw żywca", "Zaopatrzenie i Zakupy", "📅"),
                new ModuleInfo("PlachtyAviloga", "Transport Avilog", "Zarządzaj transportem surowca", "Zaopatrzenie i Zakupy", "🎯"),
                new ModuleInfo("DokumentyZakupu", "Dokumenty Zakupu", "Dokumenty zakupowe i umowy", "Zaopatrzenie i Zakupy", "📄"),
                new ModuleInfo("Specyfikacje", "Specyfikacja Surowca", "Tworzenie i zarządzanie specyfikacjami", "Zaopatrzenie i Zakupy", "📝"),
                new ModuleInfo("PlatnosciHodowcy", "Płatności Hodowców", "Płatności dla hodowców", "Zaopatrzenie i Zakupy", "💰"),
                new ModuleInfo("ZmianyUHodowcow", "Wnioski o Zmianę", "Zgłoszenia zmian u hodowców", "Zaopatrzenie i Zakupy", "✏️"),
                
                // Produkcja i Magazyn
                new ModuleInfo("KalkulacjaKrojenia", "Kalkulacja Krojenia", "Planuj proces krojenia", "Produkcja i Magazyn", "✂️"),
                new ModuleInfo("ProdukcjaPodglad", "Podgląd Produkcji", "Monitoruj bieżącą produkcję", "Produkcja i Magazyn", "🏭"),
                new ModuleInfo("PrzychodMrozni", "Mroźnia", "Zarządzaj stanami magazynowymi", "Produkcja i Magazyn", "❄️"),
                new ModuleInfo("LiczenieMagazynu", "Liczenie Magazynu", "Rejestruj poranne stany magazynowe", "Produkcja i Magazyn", "📦"),
                new ModuleInfo("PanelMagazyniera", "Panel Magazyniera", "Kompleksowy panel do zarządzania wydaniami", "Produkcja i Magazyn", "📱"),
                
                // Sprzedaż i CRM
                new ModuleInfo("CRM", "CRM", "Zarządzaj relacjami z klientami", "Sprzedaż i CRM", "👥"),
                new ModuleInfo("ZamowieniaOdbiorcow", "Zamówienia Mięsa", "Zarządzanie zamówieniami", "Sprzedaż i CRM", "📦"),
                new ModuleInfo("DokumentySprzedazy", "Faktury Sprzedaży", "Generuj i przeglądaj faktury", "Sprzedaż i CRM", "🧾"),
                new ModuleInfo("PrognozyUboju", "Prognoza Uboju", "Analizuj średnie tygodniowe zakupów", "Sprzedaż i CRM", "📈"),
                new ModuleInfo("PlanTygodniowy", "Plan Produkcji", "Tygodniowy plan uboju i krojenia", "Sprzedaż i CRM", "📊"),
                new ModuleInfo("AnalizaTygodniowa", "Dashboard Analityczny", "Analizuj bilans produkcji i sprzedaży", "Sprzedaż i CRM", "📊"),
                new ModuleInfo("OfertaCenowa", "Oferty Handlowe", "Twórz i zarządzaj ofertami", "Sprzedaż i CRM", "💵"),
                
                // Opakowania i Transport
                new ModuleInfo("PodsumowanieSaldOpak", "Salda Zbiorcze", "Analizuj zbiorcze salda opakowań", "Opakowania i Transport", "📊"),
                new ModuleInfo("SaldaOdbiorcowOpak", "Salda Odbiorcy", "Sprawdzaj salda dla odbiorców", "Opakowania i Transport", "📈"),
                new ModuleInfo("UstalanieTranportu", "Transport", "Organizuj i planuj transport", "Opakowania i Transport", "🚚"),
                
                // Finanse i Zarządzanie
                new ModuleInfo("DaneFinansowe", "Wynik Finansowy", "Analizuj dane finansowe firmy", "Finanse i Zarządzanie", "💼"),
                new ModuleInfo("NotatkiZeSpotkan", "Notatki ze Spotkań", "Twórz i przeglądaj notatki", "Finanse i Zarządzanie", "📝")
            };
        }

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
                [25] = "PanelMagazyniera" // ✅ NOWE UPRAWNIENIE
            };
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(selectedUserId))
            {
                MessageBox.Show("Wybierz użytkownika przed zapisaniem.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                char[] accessArray = new char[50];
                for (int i = 0; i < 50; i++) accessArray[i] = '0';

                var accessMap = GetAccessMap();
                var modulesList = GetModulesList();

                foreach (DataGridViewRow row in permissionsGrid.Rows)
                {
                    string displayName = row.Cells["Moduł"].Value?.ToString();
                    bool hasAccess = Convert.ToBoolean(row.Cells["Dostęp"].Value);

                    // Znajdź klucz modułu na podstawie DisplayName
                    var module = modulesList.FirstOrDefault(m => m.DisplayName == displayName);
                    if (module != null)
                    {
                        var position = accessMap.FirstOrDefault(x => x.Value == module.Key).Key;
                        if (position >= 0 && hasAccess)
                        {
                            accessArray[position] = '1';
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

                MessageBox.Show("✓ Uprawnienia zostały zapisane.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania uprawnień:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            if (usersGrid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Wybierz użytkownika do usunięcia.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string userId = usersGrid.SelectedRows[0].Cells["ID"].Value.ToString();
            string userName = usersGrid.SelectedRows[0].Cells["Name"].Value?.ToString() ?? "Nieznany";

            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć użytkownika:\n\nID: {userId}\nNazwa: {userName}\n\nTa operacja jest nieodwracalna!",
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
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    MessageBox.Show("✓ Użytkownik został usunięty.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadUsers();
                    permissionsGrid.DataSource = null;
                    selectedUserId = null;
                    userComboBox.Text = "";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas usuwania użytkownika:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            LoadUsers();
            if (!string.IsNullOrEmpty(selectedUserId))
                LoadPermissions(selectedUserId);
        }

        private void UsersGrid_SelectionChanged(object sender, EventArgs e)
        {
            if (usersGrid.SelectedRows.Count > 0)
            {
                selectedUserId = usersGrid.SelectedRows[0].Cells["ID"].Value?.ToString();
                if (!string.IsNullOrEmpty(selectedUserId))
                {
                    string userName = usersGrid.SelectedRows[0].Cells["Name"].Value?.ToString() ?? "Nieznany";
                    userComboBox.Text = $"{selectedUserId} - {userName}";
                    LoadPermissions(selectedUserId);
                }
            }
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            if (usersGrid.DataSource is DataTable dt)
            {
                string filter = searchBox.Text.Trim();
                dt.DefaultView.RowFilter = string.IsNullOrEmpty(filter) ? "" : $"ID LIKE '%{filter}%' OR Name LIKE '%{filter}%'";
            }
        }

        private void SetAllPermissions(bool value)
        {
            foreach (DataGridViewRow row in permissionsGrid.Rows)
                row.Cells["Dostęp"].Value = value;
        }

        private void PermissionsGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0)
            {
                var column = permissionsGrid.Columns[e.ColumnIndex];
                if (column.Name == "Dostęp" && column is DataGridViewCheckBoxColumn)
                {
                    permissionsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            }
        }

        private void PermissionsGrid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (permissionsGrid.IsCurrentCellDirty)
            {
                permissionsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void ManageHandlowcyButton_Click(object sender, EventArgs e)
        {
            if (usersGrid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Wybierz użytkownika, któremu chcesz przypisać handlowców.",
                    "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string userId = usersGrid.SelectedRows[0].Cells["ID"].Value.ToString();
            string userName = usersGrid.SelectedRows[0].Cells["Name"].Value?.ToString() ?? "Nieznany";

            using (var dialog = new UserHandlowcyDialog(connectionString, handelConnectionString, userId, userName))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    LoadUsers();
                }
            }
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
            this.BackColor = ColorTranslator.FromHtml("#ECF0F1");

            var titleLabel = new Label { Text = "➕ Nowy użytkownik", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#2C3E50"), Location = new Point(30, 20), AutoSize = true };
            this.Controls.Add(titleLabel);

            var idLabel = new Label { Text = "ID użytkownika:", Location = new Point(30, 70), AutoSize = true, Font = new Font("Segoe UI", 10) };
            this.Controls.Add(idLabel);

            idTextBox = new TextBox { Location = new Point(170, 67), Size = new Size(230, 28), Font = new Font("Segoe UI", 11) };
            this.Controls.Add(idTextBox);

            var nameLabel = new Label { Text = "Nazwa:", Location = new Point(30, 115), AutoSize = true, Font = new Font("Segoe UI", 10) };
            this.Controls.Add(nameLabel);

            nameTextBox = new TextBox { Location = new Point(170, 112), Size = new Size(230, 28), Font = new Font("Segoe UI", 11) };
            this.Controls.Add(nameTextBox);

            okButton = new Button { Text = "Dodaj", Location = new Point(130, 170), Size = new Size(110, 40), BackColor = ColorTranslator.FromHtml("#27AE60"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), DialogResult = DialogResult.OK };
            okButton.FlatAppearance.BorderSize = 0;
            okButton.Click += OkButton_Click;
            this.Controls.Add(okButton);

            cancelButton = new Button { Text = "Anuluj", Location = new Point(250, 170), Size = new Size(110, 40), BackColor = ColorTranslator.FromHtml("#7F8C8D"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), DialogResult = DialogResult.Cancel };
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