using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
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
        private Panel topToolbar;
        private Panel leftPanel;
        private Panel rightPanel;
        private Panel permissionsPanel;
        private TextBox searchBox;
        private Label usersCountLabel;
        private Label selectedUserLabel;
        private string selectedUserId;
        private PictureBox logoPictureBox;
        private FlowLayoutPanel permissionsFlowPanel;
        private Dictionary<string, List<CheckBox>> categoryCheckboxes = new Dictionary<string, List<CheckBox>>();
        private Dictionary<string, CheckBox> categoryHeaders = new Dictionary<string, CheckBox>();

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
            // ═══════════════════════════════════════════════════════════════════════════
            // TOP TOOLBAR - jak w screenie
            // ═══════════════════════════════════════════════════════════════════════════
            topToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(250, 250, 250),
                BorderStyle = BorderStyle.None
            };
            topToolbar.Paint += (s, e) => {
                e.Graphics.DrawLine(new Pen(Colors.Border), 0, topToolbar.Height - 1, topToolbar.Width, topToolbar.Height - 1);
            };

            int btnX = 15;
            var saveBtn = CreateToolbarButton("💾 Zapisz", Colors.Success, ref btnX);
            saveBtn.Click += SaveButton_Click;
            topToolbar.Controls.Add(saveBtn);

            var cancelBtn = CreateToolbarButton("❌ Anuluj", Colors.TextGray, ref btnX);
            cancelBtn.Click += (s, e) => this.Close();
            topToolbar.Controls.Add(cancelBtn);

            btnX += 20; // Separator

            var selectAllBtn = CreateToolbarButton("✓ Wszystko", Color.FromArgb(46, 125, 50), ref btnX);
            selectAllBtn.Click += (s, e) => SetAllPermissions(true);
            topToolbar.Controls.Add(selectAllBtn);

            var selectNoneBtn = CreateToolbarButton("✗ Nic", Colors.Danger, ref btnX);
            selectNoneBtn.Click += (s, e) => SetAllPermissions(false);
            topToolbar.Controls.Add(selectNoneBtn);

            var invertBtn = CreateToolbarButton("🔄 Odwróć", Colors.Warning, ref btnX);
            invertBtn.Click += InvertPermissions_Click;
            topToolbar.Controls.Add(invertBtn);

            btnX += 20;

            var addUserBtn = CreateToolbarButton("➕ Nowy użytkownik", Color.FromArgb(25, 118, 210), ref btnX);
            addUserBtn.Click += AddUserButton_Click;
            topToolbar.Controls.Add(addUserBtn);

            var deleteUserBtn = CreateToolbarButton("🗑️ Usuń", Colors.Danger, ref btnX);
            deleteUserBtn.Click += DeleteUserButton_Click;
            topToolbar.Controls.Add(deleteUserBtn);

            btnX += 20;

            var handlowcyBtn = CreateToolbarButton("👔 Handlowcy", Color.FromArgb(156, 39, 176), ref btnX);
            handlowcyBtn.Click += ManageHandlowcyButton_Click;
            topToolbar.Controls.Add(handlowcyBtn);

            var contactBtn = CreateToolbarButton("📞 Kontakt", Color.FromArgb(0, 172, 193), ref btnX);
            contactBtn.Click += EditContactButton_Click;
            topToolbar.Controls.Add(contactBtn);

            // Close button na prawo
            var closeBtn = new Button
            {
                Text = "✕",
                Size = new Size(40, 35),
                Location = new Point(this.Width - 60, 7),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.Transparent,
                ForeColor = Colors.TextDark,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 14),
                Cursor = Cursors.Hand
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.FlatAppearance.MouseOverBackColor = Colors.Danger;
            closeBtn.FlatAppearance.MouseOverBackColor = Colors.Danger;
            closeBtn.MouseEnter += (s, e) => closeBtn.ForeColor = Color.White;
            closeBtn.MouseLeave += (s, e) => closeBtn.ForeColor = Colors.TextDark;
            closeBtn.Click += (s, e) => this.Close();
            topToolbar.Controls.Add(closeBtn);

            // ═══════════════════════════════════════════════════════════════════════════
            // LEFT PANEL - Logo + Lista użytkowników
            // ═══════════════════════════════════════════════════════════════════════════
            leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 320,
                BackColor = Color.White,
                Padding = new Padding(0)
            };
            leftPanel.Paint += (s, e) => {
                e.Graphics.DrawLine(new Pen(Colors.Border), leftPanel.Width - 1, 0, leftPanel.Width - 1, leftPanel.Height);
            };

            // Logo - kompaktowy nagłówek
            var logoPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 55,
                BackColor = Colors.Primary
            };

            logoPictureBox = new PictureBox
            {
                Size = new Size(40, 40),
                Location = new Point(8, 8),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };

            // Próba załadowania logo
            try
            {
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logo.png");
                if (!File.Exists(logoPath))
                {
                    logoPath = Path.Combine(Directory.GetCurrentDirectory(), "Logo.png");
                }
                if (File.Exists(logoPath))
                {
                    logoPictureBox.Image = Image.FromFile(logoPath);
                }
            }
            catch { }

            logoPanel.Controls.Add(logoPictureBox);

            var titleLabel = new Label
            {
                Text = "PIÓRKOWSCY",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(55, 10),
                AutoSize = true
            };
            logoPanel.Controls.Add(titleLabel);

            var subtitleLabel = new Label
            {
                Text = "Panel Administracyjny",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(180, 180, 180),
                Location = new Point(57, 30),
                AutoSize = true
            };
            logoPanel.Controls.Add(subtitleLabel);

            leftPanel.Controls.Add(logoPanel);

            // Panel wyszukiwania - kompaktowy
            var searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.White,
                Padding = new Padding(8, 3, 8, 3)
            };

            var usersLabel = new Label
            {
                Text = "👥 UŻYTKOWNICY SYSTEMU",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Colors.TextDark,
                Location = new Point(8, 3),
                AutoSize = true
            };
            searchPanel.Controls.Add(usersLabel);

            searchBox = new TextBox
            {
                Location = new Point(8, 24),
                Size = new Size(295, 22),
                Font = new Font("Segoe UI", 9),
                PlaceholderText = "🔍 Szukaj użytkownika...",
                BorderStyle = BorderStyle.FixedSingle
            };
            searchBox.TextChanged += SearchBox_TextChanged;
            searchPanel.Controls.Add(searchBox);

            leftPanel.Controls.Add(searchPanel);

            // Grid użytkowników
            usersGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Colors.Border
            };

            usersGrid.ColumnHeadersDefaultCellStyle.BackColor = Colors.Primary;
            usersGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            usersGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            usersGrid.ColumnHeadersDefaultCellStyle.Padding = new Padding(5);
            usersGrid.ColumnHeadersHeight = 38;
            usersGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 230, 201);
            usersGrid.DefaultCellStyle.SelectionForeColor = Colors.TextDark;
            usersGrid.AlternatingRowsDefaultCellStyle.BackColor = Colors.RowAlt;
            usersGrid.RowTemplate.Height = 32;
            usersGrid.SelectionChanged += UsersGrid_SelectionChanged;
            usersGrid.DataBindingComplete += UsersGrid_DataBindingComplete;

            leftPanel.Controls.Add(usersGrid);

            // Pasek statusu na dole - kompaktowy
            var statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 25,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            usersCountLabel = new Label
            {
                Text = "Ładowanie...",
                Font = new Font("Segoe UI", 8),
                ForeColor = Colors.TextGray,
                Location = new Point(10, 5),
                AutoSize = true
            };
            statusPanel.Controls.Add(usersCountLabel);

            leftPanel.Controls.Add(statusPanel);

            // ═══════════════════════════════════════════════════════════════════════════
            // RIGHT PANEL - Uprawnienia w stylu TreeView z grupami
            // ═══════════════════════════════════════════════════════════════════════════
            rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Colors.Background,
                Padding = new Padding(0)
            };

            // Nagłówek z info o wybranym użytkowniku - jedna linia
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.White
            };
            headerPanel.Paint += (s, e) => {
                e.Graphics.DrawLine(new Pen(Colors.Border), 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            var permTitleLabel = new Label
            {
                Text = "🔐 UPRAWNIENIA MODUŁÓW",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Colors.TextDark,
                Location = new Point(10, 5),
                AutoSize = true
            };
            headerPanel.Controls.Add(permTitleLabel);

            selectedUserLabel = new Label
            {
                Text = "Wybierz użytkownika",
                Font = new Font("Segoe UI", 9),
                ForeColor = Colors.TextGray,
                Location = new Point(220, 6),
                AutoSize = true
            };
            headerPanel.Controls.Add(selectedUserLabel);

            rightPanel.Controls.Add(headerPanel);

            // Panel z uprawnieniami - scrollowalny
            permissionsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Colors.Background,
                Padding = new Padding(5)
            };

            permissionsFlowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Colors.Background,
                Padding = new Padding(5)
            };

            permissionsPanel.Controls.Add(permissionsFlowPanel);
            rightPanel.Controls.Add(permissionsPanel);

            // Dodaj kontrolki do formularza
            this.Controls.Add(rightPanel);
            this.Controls.Add(leftPanel);
            this.Controls.Add(topToolbar);
        }

        private Button CreateToolbarButton(string text, Color color, ref int x)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(text.Length * 9 + 30, 35),
                Location = new Point(x, 7),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = color,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(color, 0.1f);

            x += btn.Width + 8;
            return btn;
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

            // Stała szerokość paneli
            int panelWidth = 950;

            // Grupuj moduły według kategorii
            var groupedModules = modules.GroupBy(m => m.Category).OrderBy(g => GetCategoryOrder(g.Key));

            foreach (var group in groupedModules)
            {
                string category = group.Key;
                Color categoryColor = GetCategoryColor(category);

                // ═══════════════════════════════════════════════════════════════════════
                // NAGŁÓWEK KATEGORII - minimalny
                // ═══════════════════════════════════════════════════════════════════════
                var categoryPanel = new Panel
                {
                    Width = panelWidth,
                    Height = 24,
                    BackColor = categoryColor,
                    Margin = new Padding(0, 3, 0, 0),
                    Cursor = Cursors.Hand
                };

                var categoryCheckbox = new CheckBox
                {
                    Text = $" ▼ {category}",
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(5, 3),
                    AutoSize = true,
                    Cursor = Cursors.Hand,
                    BackColor = Color.Transparent
                };
                categoryCheckbox.CheckedChanged += (s, e) => CategoryHeader_CheckedChanged(category, categoryCheckbox.Checked);
                categoryPanel.Controls.Add(categoryCheckbox);
                categoryHeaders[category] = categoryCheckbox;

                // Licznik uprawnień w kategorii - na prawo
                var countLabel = new Label
                {
                    Text = $"({group.Count()} modułów)",
                    Font = new Font("Segoe UI", 7),
                    ForeColor = Color.FromArgb(220, 220, 220),
                    AutoSize = true,
                    BackColor = Color.Transparent
                };
                countLabel.Location = new Point(panelWidth - countLabel.PreferredWidth - 10, 5);
                categoryPanel.Controls.Add(countLabel);

                // Kliknięcie na panel też zaznacza
                categoryPanel.Click += (s, e) => categoryCheckbox.Checked = !categoryCheckbox.Checked;

                permissionsFlowPanel.Controls.Add(categoryPanel);

                // Lista modułów w kategorii
                categoryCheckboxes[category] = new List<CheckBox>();
                int moduleIndex = 0;

                foreach (var module in group)
                {
                    bool hasAccess = false;
                    var position = accessMap.FirstOrDefault(x => x.Value == module.Key).Key;
                    if (position >= 0 && position < accessString.Length)
                        hasAccess = accessString[position] == '1';

                    // Panel pojedynczego modułu - minimalny
                    var modulePanel = new Panel
                    {
                        Width = panelWidth,
                        Height = 28,
                        BackColor = moduleIndex % 2 == 0 ? Color.White : Colors.RowAlt,
                        Margin = new Padding(0, 0, 0, 0),
                        Cursor = Cursors.Hand
                    };

                    // Pasek koloru po lewej
                    var colorBar = new Panel
                    {
                        Width = 3,
                        Height = 28,
                        BackColor = categoryColor,
                        Location = new Point(0, 0)
                    };
                    modulePanel.Controls.Add(colorBar);

                    // Ikona
                    var iconLabel = new Label
                    {
                        Text = module.Icon,
                        Font = new Font("Segoe UI Emoji", 11),
                        ForeColor = categoryColor,
                        Location = new Point(8, 3),
                        Size = new Size(22, 22),
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    modulePanel.Controls.Add(iconLabel);

                    // Nazwa modułu
                    var nameLabel = new Label
                    {
                        Text = module.DisplayName,
                        Font = new Font("Segoe UI", 9, FontStyle.Bold),
                        ForeColor = Colors.TextDark,
                        Location = new Point(32, 1),
                        AutoSize = true
                    };
                    modulePanel.Controls.Add(nameLabel);

                    // Opis - skrócony jeśli za długi
                    string desc = module.Description;
                    if (desc.Length > 45) desc = desc.Substring(0, 42) + "...";
                    var descLabel = new Label
                    {
                        Text = desc,
                        Font = new Font("Segoe UI", 7),
                        ForeColor = Colors.TextGray,
                        Location = new Point(32, 15),
                        AutoSize = true
                    };
                    modulePanel.Controls.Add(descLabel);

                    // Checkbox dostępu - pozycja od prawej
                    var accessCheckbox = new CheckBox
                    {
                        Checked = hasAccess,
                        Location = new Point(panelWidth - 25, 5),
                        Size = new Size(18, 18),
                        Cursor = Cursors.Hand,
                        Tag = module.Key
                    };
                    accessCheckbox.CheckedChanged += (s, e) => UpdateCategoryHeaderState(category);
                    modulePanel.Controls.Add(accessCheckbox);
                    categoryCheckboxes[category].Add(accessCheckbox);

                    // Label "Dostęp" - przed checkboxem
                    var accessLabel = new Label
                    {
                        Text = "Dostęp",
                        Font = new Font("Segoe UI", 7),
                        ForeColor = Colors.TextGray,
                        Location = new Point(panelWidth - 68, 7),
                        AutoSize = true
                    };
                    modulePanel.Controls.Add(accessLabel);

                    // Hover effect
                    int idx = moduleIndex;
                    modulePanel.MouseEnter += (s, e) => modulePanel.BackColor = Colors.RowHover;
                    modulePanel.MouseLeave += (s, e) => modulePanel.BackColor = idx % 2 == 0 ? Color.White : Colors.RowAlt;

                    // Kliknięcie na panel przełącza checkbox
                    modulePanel.Click += (s, e) => accessCheckbox.Checked = !accessCheckbox.Checked;
                    nameLabel.Click += (s, e) => accessCheckbox.Checked = !accessCheckbox.Checked;
                    descLabel.Click += (s, e) => accessCheckbox.Checked = !accessCheckbox.Checked;
                    iconLabel.Click += (s, e) => accessCheckbox.Checked = !accessCheckbox.Checked;

                    permissionsFlowPanel.Controls.Add(modulePanel);
                    moduleIndex++;
                }

                // Zaktualizuj stan nagłówka kategorii
                UpdateCategoryHeaderState(category);
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
                        ORDER BY o.Name";

                    using (SqlDataAdapter adapter = new SqlDataAdapter(query, conn))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        usersGrid.DataSource = dt;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania użytkowników:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UsersGrid_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (usersGrid.DataSource is DataTable dt)
            {
                usersCountLabel.Text = $"Łącznie: {dt.Rows.Count} użytkowników";
            }
        }

        private void UsersGrid_SelectionChanged(object sender, EventArgs e)
        {
            if (usersGrid.SelectedRows.Count > 0)
            {
                selectedUserId = usersGrid.SelectedRows[0].Cells["ID"].Value?.ToString();
                if (!string.IsNullOrEmpty(selectedUserId))
                {
                    string userName = usersGrid.SelectedRows[0].Cells["Name"].Value?.ToString() ?? "Nieznany";
                    selectedUserLabel.Text = $"Użytkownik: {userName} (ID: {selectedUserId})";
                    selectedUserLabel.ForeColor = Colors.TextDark;
                    BuildPermissionsUI();
                }
            }
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            if (usersGrid.DataSource is DataTable dt)
            {
                string filter = searchBox.Text.Trim().Replace("'", "''");
                dt.DefaultView.RowFilter = string.IsNullOrEmpty(filter) ? "" : $"ID LIKE '%{filter}%' OR Name LIKE '%{filter}%'";
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
                MessageBox.Show("Wybierz użytkownika przed zapisaniem.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                MessageBox.Show("✓ Uprawnienia zostały zapisane pomyślnie!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    permissionsFlowPanel.Controls.Clear();
                    selectedUserId = null;
                    selectedUserLabel.Text = "Wybierz użytkownika z listy po lewej stronie";
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
            if (usersGrid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Wybierz użytkownika, któremu chcesz przypisać handlowców.",
                    "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string userId = usersGrid.SelectedRows[0].Cells["ID"].Value.ToString();
            string userName = usersGrid.SelectedRows[0].Cells["Name"].Value?.ToString() ?? "Nieznany";

            var dialog = new UserHandlowcyDialog(connectionString, handelConnectionString, userId, userName);
            dialog.HandlowcyZapisani += (s, ev) => LoadUsers();
            dialog.Show();
        }

        private void EditContactButton_Click(object sender, EventArgs e)
        {
            if (usersGrid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Wybierz użytkownika, któremu chcesz edytować dane kontaktowe.",
                    "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string userId = usersGrid.SelectedRows[0].Cells["ID"].Value.ToString();
            string userName = usersGrid.SelectedRows[0].Cells["Name"].Value?.ToString() ?? "Nieznany";

            var dialog = new EditOperatorContactDialog(connectionString, userId, userName);
            dialog.ShowDialog();
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
                new ModuleInfo("AnalizaWydajnosci", "Analiza Wydajności", "Porównanie masy żywca do tuszek", "Produkcja i Magazyn", "📈"),

                // ═══════════════════════════════════════════════════════════════════════
                // SPRZEDAŻ I CRM - Niebieski
                // ═══════════════════════════════════════════════════════════════════════
                new ModuleInfo("CRM", "Relacje z Klientami", "Zarządzanie relacjami z odbiorcami", "Sprzedaż i CRM", "🤝"),
                new ModuleInfo("KartotekaOdbiorcow", "Kartoteka Odbiorców", "Pełna baza danych klientów", "Sprzedaż i CRM", "👤"),
                new ModuleInfo("ZamowieniaOdbiorcow", "Zamówienia Klientów", "Przyjmowanie zamówień", "Sprzedaż i CRM", "🛒"),
                new ModuleInfo("DokumentySprzedazy", "Faktury Sprzedaży", "Przeglądanie faktur i WZ", "Sprzedaż i CRM", "🧾"),
                new ModuleInfo("PanelFaktur", "Panel Faktur", "Tworzenie faktur w Symfonii", "Sprzedaż i CRM", "📋"),
                new ModuleInfo("OfertaCenowa", "Kreator Ofert", "Tworzenie ofert cenowych", "Sprzedaż i CRM", "💰"),
                new ModuleInfo("ListaOfert", "Archiwum Ofert", "Historia ofert handlowych", "Sprzedaż i CRM", "📂"),
                new ModuleInfo("DashboardOfert", "Analiza Ofert", "Statystyki skuteczności ofert", "Sprzedaż i CRM", "📊"),
                new ModuleInfo("DashboardWyczerpalnosci", "Klasy Wagowe", "Rozdzielanie klas wagowych", "Sprzedaż i CRM", "⚖️"),
                new ModuleInfo("PanelReklamacji", "Reklamacje Klientów", "Obsługa reklamacji odbiorców", "Sprzedaż i CRM", "⚠️"),

                // ═══════════════════════════════════════════════════════════════════════
                // PLANOWANIE I ANALIZY - Fioletowy
                // ═══════════════════════════════════════════════════════════════════════
                new ModuleInfo("PrognozyUboju", "Prognoza Uboju", "Analiza średnich zakupów żywca", "Planowanie i Analizy", "🔮"),
                new ModuleInfo("PlanTygodniowy", "Plan Tygodniowy", "Harmonogram uboju i krojenia", "Planowanie i Analizy", "🗓️"),
                new ModuleInfo("AnalizaTygodniowa", "Dashboard Analityczny", "Analiza produkcji i sprzedaży", "Planowanie i Analizy", "📉"),

                // ═══════════════════════════════════════════════════════════════════════
                // OPAKOWANIA I TRANSPORT - Turkusowy
                // ═══════════════════════════════════════════════════════════════════════
                new ModuleInfo("PodsumowanieSaldOpak", "Zestawienie Opakowań", "Salda opakowań wg typu", "Opakowania i Transport", "📦"),
                new ModuleInfo("SaldaOdbiorcowOpak", "Salda Opakowań Klientów", "Salda dla kontrahentów", "Opakowania i Transport", "🏷️"),
                new ModuleInfo("UstalanieTranportu", "Planowanie Transportu", "Organizacja tras dostaw", "Opakowania i Transport", "🚚"),

                // ═══════════════════════════════════════════════════════════════════════
                // FINANSE I ZARZĄDZANIE - Szaroniebieski
                // ═══════════════════════════════════════════════════════════════════════
                new ModuleInfo("DaneFinansowe", "Wyniki Finansowe", "Przychody, koszty, marże", "Finanse i Zarządzanie", "💼"),
                new ModuleInfo("NotatkiZeSpotkan", "Notatki Służbowe", "Notatki ze spotkań biznesowych", "Finanse i Zarządzanie", "📝"),

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
                [35] = "AdminPermissions"
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
