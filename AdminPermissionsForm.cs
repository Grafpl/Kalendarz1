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
            // TOP TOOLBAR - kompaktowy, wszystko w jednej linii
            // ═══════════════════════════════════════════════════════════════════════════
            topToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Colors.Primary,
                Padding = new Padding(8, 6, 8, 6)
            };

            // Wyszukiwanie użytkowników - na początku
            searchBox = new TextBox
            {
                Location = new Point(10, 10),
                Size = new Size(180, 24),
                Font = new Font("Segoe UI", 9),
                PlaceholderText = "🔍 Szukaj użytkownika...",
                BorderStyle = BorderStyle.FixedSingle
            };
            searchBox.TextChanged += SearchBox_TextChanged;
            topToolbar.Controls.Add(searchBox);

            int btnX = 200;

            // Przyciski akcji - kompaktowe
            var saveBtn = CreateCompactButton("💾 Zapisz", Colors.Success, ref btnX);
            saveBtn.Click += SaveButton_Click;
            topToolbar.Controls.Add(saveBtn);

            var selectAllBtn = CreateCompactButton("✓ Wszystko", Color.FromArgb(76, 175, 80), ref btnX);
            selectAllBtn.Click += (s, e) => SetAllPermissions(true);
            topToolbar.Controls.Add(selectAllBtn);

            var selectNoneBtn = CreateCompactButton("✗ Nic", Color.FromArgb(180, 80, 80), ref btnX);
            selectNoneBtn.Click += (s, e) => SetAllPermissions(false);
            topToolbar.Controls.Add(selectNoneBtn);

            var invertBtn = CreateCompactButton("⇄ Odwróć", Colors.Warning, ref btnX);
            invertBtn.Click += InvertPermissions_Click;
            topToolbar.Controls.Add(invertBtn);

            btnX += 10;

            var addUserBtn = CreateCompactButton("➕ Nowy", Color.FromArgb(33, 150, 243), ref btnX);
            addUserBtn.Click += AddUserButton_Click;
            topToolbar.Controls.Add(addUserBtn);

            var deleteUserBtn = CreateCompactButton("🗑 Usuń", Color.FromArgb(180, 80, 80), ref btnX);
            deleteUserBtn.Click += DeleteUserButton_Click;
            topToolbar.Controls.Add(deleteUserBtn);

            btnX += 10;

            var handlowcyBtn = CreateCompactButton("👔 Handlowcy", Color.FromArgb(156, 39, 176), ref btnX);
            handlowcyBtn.Click += ManageHandlowcyButton_Click;
            topToolbar.Controls.Add(handlowcyBtn);

            var contactBtn = CreateCompactButton("📞 Kontakt", Color.FromArgb(0, 150, 170), ref btnX);
            contactBtn.Click += EditContactButton_Click;
            topToolbar.Controls.Add(contactBtn);

            // Wybrany użytkownik - po prawej stronie toolbara
            selectedUserLabel = new Label
            {
                Text = "👤 Wybierz użytkownika",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 210, 220),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            selectedUserLabel.Location = new Point(this.Width - selectedUserLabel.PreferredWidth - 20, 12);
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

        private Button CreateCompactButton(string text, Color color, ref int x)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(text.Length * 7 + 20, 28),
                Location = new Point(x, 8),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.15f);
            x += btn.Width + 4;
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
                    accessCheckbox.CheckedChanged += (s, e) => UpdateCategoryHeaderState(category);
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

            // Avatar z inicjałami - kompaktowy
            string initials = GetInitials(user.Name);
            Color avatarColor = GetAvatarColor(user.ID);

            var avatarPanel = new Panel
            {
                Size = new Size(32, 32),
                Location = new Point(4, 4),
                BackColor = Color.Transparent
            };
            avatarPanel.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
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

            // Hover i kliknięcie
            Action<bool> setHover = (hover) => {
                if (card != selectedUserCard)
                {
                    card.BackColor = hover ? Color.FromArgb(245, 247, 250) : Color.White;
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
            // Odznacz poprzednią kartę
            if (selectedUserCard != null)
            {
                selectedUserCard.BackColor = Color.White;
                selectedUserCard.Invalidate();
            }

            // Zaznacz nową kartę
            selectedUserCard = card;
            card.BackColor = Color.FromArgb(200, 230, 201); // Jasny zielony

            selectedUserId = user.ID;
            selectedUserLabel.Text = $"👤 {user.Name} (ID: {user.ID})";
            selectedUserLabel.ForeColor = Color.White;
            // Aktualizuj pozycję labela
            selectedUserLabel.Location = new Point(this.Width - selectedUserLabel.PreferredWidth - 30, 12);
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
                new ModuleInfo("CentrumSpotkan", "Centrum Spotkań", "Rejestr spotkań i wizyt", "Finanse i Zarządzanie", "📆"),
                new ModuleInfo("NotatkiZeSpotkan", "Notatki Służbowe", "Notatki ze spotkań biznesowych", "Finanse i Zarządzanie", "📝"),
                new ModuleInfo("KontrolaGodzin", "Kontrola Czasu Pracy", "Monitoring czasu pracy pracowników", "Finanse i Zarządzanie", "⏰"),

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
                [42] = "CentrumSpotkan"
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
