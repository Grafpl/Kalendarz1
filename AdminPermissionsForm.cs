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
        private DataGridView usersGrid;
        private DataGridView permissionsGrid;
        private ComboBox userComboBox;
        private TextBox searchBox;
        private Button saveButton;
        private Button addUserButton;
        private Button deleteUserButton;
        private Button refreshButton;
        private Panel topPanel;
        private Panel leftPanel;
        private Panel rightPanel;
        private Label titleLabel;
        private string selectedUserId;

        public AdminPermissionsForm()
        {
            InitializeComponent();
            InitializeCustomComponents();
            LoadUsers();
            ApplyModernStyle();
        }

        private void InitializeComponent()
        {
            this.Text = "Panel Administracyjny - Zarządzanie Uprawnieniami";
            this.Size = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
        }

        private void InitializeCustomComponents()
        {
            // Panel górny
            topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(41, 53, 65)
            };

            titleLabel = new Label
            {
                Text = "⚙ PANEL ADMINISTRACYJNY - ZARZĄDZANIE UPRAWNIENIAMI",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(30, 25)
            };
            topPanel.Controls.Add(titleLabel);

            // Panel lewy - lista użytkowników
            leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 400,
                BackColor = Color.FromArgb(245, 245, 245),
                Padding = new Padding(20)
            };

            var usersLabel = new Label
            {
                Text = "UŻYTKOWNICY SYSTEMU",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(55, 71, 79),
                Location = new Point(20, 20),
                AutoSize = true
            };
            leftPanel.Controls.Add(usersLabel);

            // Pole wyszukiwania
            searchBox = new TextBox
            {
                Location = new Point(20, 50),
                Size = new Size(360, 30),
                Font = new Font("Segoe UI", 11),
                PlaceholderText = "🔍 Szukaj użytkownika..."
            };
            searchBox.TextChanged += SearchBox_TextChanged;
            leftPanel.Controls.Add(searchBox);

            // Grid z użytkownikami
            usersGrid = new DataGridView
            {
                Location = new Point(20, 90),
                Size = new Size(360, 400),
                Font = new Font("Segoe UI", 10),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            usersGrid.SelectionChanged += UsersGrid_SelectionChanged;
            usersGrid.DataBindingComplete += UsersGrid_DataBindingComplete;

            // Stylizacja grida
            usersGrid.EnableHeadersVisualStyles = false;
            usersGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 73, 94);
            usersGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            usersGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            usersGrid.ColumnHeadersHeight = 35;
            usersGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(41, 128, 185);
            usersGrid.DefaultCellStyle.SelectionForeColor = Color.White;
            usersGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);

            leftPanel.Controls.Add(usersGrid);

            // Przyciski zarządzania użytkownikami
            addUserButton = new Button
            {
                Text = "➕ Nowy użytkownik",
                Location = new Point(20, 500),
                Size = new Size(175, 40),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(46, 125, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            addUserButton.FlatAppearance.BorderSize = 0;
            addUserButton.Click += AddUserButton_Click;
            leftPanel.Controls.Add(addUserButton);

            deleteUserButton = new Button
            {
                Text = "🗑 Usuń użytkownika",
                Location = new Point(205, 500),
                Size = new Size(175, 40),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(229, 57, 53),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            deleteUserButton.FlatAppearance.BorderSize = 0;
            deleteUserButton.Click += DeleteUserButton_Click;
            leftPanel.Controls.Add(deleteUserButton);

            // Panel prawy - uprawnienia
            rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            var permissionsLabel = new Label
            {
                Text = "UPRAWNIENIA UŻYTKOWNIKA",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(55, 71, 79),
                Location = new Point(20, 20),
                AutoSize = true
            };
            rightPanel.Controls.Add(permissionsLabel);

            // ComboBox z użytkownikiem
            userComboBox = new ComboBox
            {
                Location = new Point(20, 50),
                Size = new Size(300, 30),
                Font = new Font("Segoe UI", 11),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            rightPanel.Controls.Add(userComboBox);

            // Grid z uprawnieniami
            permissionsGrid = new DataGridView
            {
                Location = new Point(20, 90),
                Size = new Size(720, 450),
                Font = new Font("Segoe UI", 10),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            // Stylizacja grida uprawnień
            permissionsGrid.EnableHeadersVisualStyles = false;
            permissionsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 73, 94);
            permissionsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            permissionsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            permissionsGrid.ColumnHeadersHeight = 35;
            permissionsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(41, 128, 185);
            permissionsGrid.DefaultCellStyle.SelectionForeColor = Color.White;
            permissionsGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);

            rightPanel.Controls.Add(permissionsGrid);

            // Przyciski akcji
            saveButton = new Button
            {
                Text = "💾 Zapisz zmiany",
                Location = new Point(20, 550),
                Size = new Size(180, 45),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += SaveButton_Click;
            rightPanel.Controls.Add(saveButton);

            refreshButton = new Button
            {
                Text = "🔄 Odśwież",
                Location = new Point(210, 550),
                Size = new Size(150, 45),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            refreshButton.FlatAppearance.BorderSize = 0;
            refreshButton.Click += RefreshButton_Click;
            rightPanel.Controls.Add(refreshButton);

            // Przyciski szybkiego ustawiania
            var selectAllButton = new Button
            {
                Text = "✓ Zaznacz wszystkie",
                Location = new Point(370, 550),
                Size = new Size(180, 45),
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            selectAllButton.FlatAppearance.BorderSize = 0;
            selectAllButton.Click += (s, e) => SetAllPermissions(true);
            rightPanel.Controls.Add(selectAllButton);

            var deselectAllButton = new Button
            {
                Text = "✗ Odznacz wszystkie",
                Location = new Point(560, 550),
                Size = new Size(180, 45),
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                BackColor = Color.FromArgb(244, 67, 54),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            deselectAllButton.FlatAppearance.BorderSize = 0;
            deselectAllButton.Click += (s, e) => SetAllPermissions(false);
            rightPanel.Controls.Add(deselectAllButton);

            // Dodaj panele do formy
            this.Controls.Add(rightPanel);
            this.Controls.Add(leftPanel);
            this.Controls.Add(topPanel);
        }

        private void LoadUsers()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"SELECT ID, Name 
                                   FROM operators 
                                   WHERE ID != '11111' 
                                   ORDER BY Name";

                    SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    usersGrid.DataSource = dt;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania użytkowników: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UsersGrid_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            // Ustaw szerokości kolumn po związaniu danych
            if (usersGrid.Columns.Count > 0)
            {
                if (usersGrid.Columns.Contains("ID"))
                {
                    usersGrid.Columns["ID"].HeaderText = "ID";
                    usersGrid.Columns["ID"].Width = 80;
                }
                if (usersGrid.Columns.Contains("Name"))
                {
                    usersGrid.Columns["Name"].HeaderText = "Nazwa użytkownika";
                    usersGrid.Columns["Name"].Width = 250;
                }
            }
        }

        private void LoadPermissions(string userId)
        {
            try
            {
                var modules = GetModulesList();
                var permissions = new DataTable();
                permissions.Columns.Add("Moduł", typeof(string));
                permissions.Columns.Add("Opis", typeof(string));
                permissions.Columns.Add("Dostęp", typeof(bool));

                // Mapowanie pozycji na moduły
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

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Pobierz string Access z bazy
                    string query = "SELECT Access FROM operators WHERE ID = @userId";
                    string accessString = "";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        var result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            accessString = result.ToString();
                        }
                    }

                    // Parsuj uprawnienia
                    foreach (var module in modules)
                    {
                        bool hasAccess = false;

                        // Znajdź pozycję modułu w mapie
                        var position = accessMap.FirstOrDefault(x => x.Value == module.Key).Key;
                        if (position >= 0 && position < accessString.Length)
                        {
                            hasAccess = accessString[position] == '1';
                        }

                        permissions.Rows.Add(module.Key, module.Value, hasAccess);
                    }
                }

                permissionsGrid.DataSource = permissions;
                // Reszta kodu pozostaje bez zmian...
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania uprawnień: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private Dictionary<string, string> GetModulesList()
        {
            return new Dictionary<string, string>
            {
                ["DaneHodowcy"] = "Zarządzanie danymi hodowców",
                ["ZakupPaszyPisklak"] = "Zakup paszy i piskląt",
                ["WstawieniaHodowcy"] = "Rejestracja wstawień u hodowców",
                ["TerminyDostawyZywca"] = "Kalendarz terminów dostaw żywca",
                ["PlachtyAviloga"] = "Zarządzanie płachtami Aviloga",
                ["DokumentyZakupu"] = "Dokumenty zakupowe i umowy",
                ["Specyfikacje"] = "Tworzenie i zarządzanie specyfikacjami",
                ["PlatnosciHodowcy"] = "Płatności dla hodowców",
                ["CRM"] = "System CRM dla odbiorców",
                ["ZamowieniaOdbiorcow"] = "Zarządzanie zamówieniami odbiorców",
                ["KalkulacjaKrojenia"] = "Kalkulacje krojenia i produkcji",
                ["PrzychodMrozni"] = "Ewidencja przychodu do mroźni",
                ["DokumentySprzedazy"] = "Faktury i dokumenty sprzedaży",
                ["PodsumowanieSaldOpak"] = "Podsumowanie sald opakowań",
                ["SaldaOdbiorcowOpak"] = "Salda opakowań u odbiorców",
                ["DaneFinansowe"] = "Raporty i dane finansowe",
                ["UstalanieTranportu"] = "Planowanie transportu",
                ["ZmianyUHodowcow"] = "Zgłoszenia zmian u hodowców",
                ["ProdukcjaPodglad"] = "Podgląd produkcji",
                ["OfertaCenowa"] = "Tworzenie ofert cenowych"
            };
        }

        private void CreatePermissionsTableIfNotExists(SqlConnection conn)
        {
            string createTableQuery = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UserPermissions' AND xtype='U')
                CREATE TABLE UserPermissions (
                    ID int IDENTITY(1,1) PRIMARY KEY,
                    UserID varchar(50) NOT NULL,
                    ModuleName varchar(100) NOT NULL,
                    HasAccess bit NOT NULL DEFAULT 0,
                    LastModified datetime DEFAULT GETDATE(),
                    ModifiedBy varchar(50),
                    UNIQUE(UserID, ModuleName)
                )";

            using (SqlCommand cmd = new SqlCommand(createTableQuery, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(selectedUserId))
            {
                MessageBox.Show("Wybierz użytkownika przed zapisaniem.",
                    "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // Buduj string Access
                char[] accessArray = new char[50];
                for (int i = 0; i < 50; i++)
                {
                    accessArray[i] = '0';
                }

                // Mapowanie modułów na pozycje
                var accessMap = new Dictionary<string, int>
                {
                    ["DaneHodowcy"] = 0,
                    ["ZakupPaszyPisklak"] = 1,
                    ["WstawieniaHodowcy"] = 2,
                    ["TerminyDostawyZywca"] = 3,
                    ["PlachtyAviloga"] = 4,
                    ["DokumentyZakupu"] = 5,
                    ["Specyfikacje"] = 6,
                    ["PlatnosciHodowcy"] = 7,
                    ["CRM"] = 8,
                    ["ZamowieniaOdbiorcow"] = 9,
                    ["KalkulacjaKrojenia"] = 10,
                    ["PrzychodMrozni"] = 11,
                    ["DokumentySprzedazy"] = 12,
                    ["PodsumowanieSaldOpak"] = 13,
                    ["SaldaOdbiorcowOpak"] = 14,
                    ["DaneFinansowe"] = 15,
                    ["UstalanieTranportu"] = 16,
                    ["ZmianyUHodowcow"] = 17,
                    ["ProdukcjaPodglad"] = 18,
                    ["OfertaCenowa"] = 19
                };

                foreach (DataGridViewRow row in permissionsGrid.Rows)
                {
                    string moduleName = row.Cells["Moduł"].Value.ToString();
                    bool hasAccess = Convert.ToBoolean(row.Cells["Dostęp"].Value);

                    if (accessMap.ContainsKey(moduleName) && hasAccess)
                    {
                        accessArray[accessMap[moduleName]] = '1';
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

                MessageBox.Show("Uprawnienia zostały zapisane pomyślnie!",
                    "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania uprawnień: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void AddUserButton_Click(object sender, EventArgs e)
        {
            var dialog = new AddUserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                LoadUsers();
            }
        }

        private void DeleteUserButton_Click(object sender, EventArgs e)
        {
            if (usersGrid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Wybierz użytkownika do usunięcia.",
                    "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string userId = usersGrid.SelectedRows[0].Cells["ID"].Value.ToString();
            string userName = usersGrid.SelectedRows[0].Cells["Name"].Value?.ToString() ?? "Nieznany";

            var result = MessageBox.Show($"Czy na pewno chcesz usunąć użytkownika {userName} (ID: {userId})?",
                "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();

                        // Usuń uprawnienia
                        string deletePermissions = "DELETE FROM UserPermissions WHERE UserID = @userId";
                        using (SqlCommand cmd = new SqlCommand(deletePermissions, conn))
                        {
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.ExecuteNonQuery();
                        }

                        // Usuń użytkownika
                        string deleteUser = "DELETE FROM operators WHERE ID = @userId";
                        using (SqlCommand cmd = new SqlCommand(deleteUser, conn))
                        {
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    MessageBox.Show("Użytkownik został usunięty.",
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadUsers();
                    permissionsGrid.DataSource = null;
                    selectedUserId = null;
                    userComboBox.Text = "";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas usuwania użytkownika: {ex.Message}",
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            LoadUsers();
            if (!string.IsNullOrEmpty(selectedUserId))
            {
                LoadPermissions(selectedUserId);
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
                    string displayName = $"{selectedUserId} - {userName}";
                    userComboBox.Text = displayName;
                    LoadPermissions(selectedUserId);
                }
            }
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            if (usersGrid.DataSource is DataTable dt)
            {
                string filter = searchBox.Text.Trim();
                if (string.IsNullOrEmpty(filter))
                {
                    dt.DefaultView.RowFilter = "";
                }
                else
                {
                    dt.DefaultView.RowFilter = $"ID LIKE '%{filter}%' OR Name LIKE '%{filter}%'";
                }
            }
        }

        private void SetAllPermissions(bool value)
        {
            foreach (DataGridViewRow row in permissionsGrid.Rows)
            {
                row.Cells["Dostęp"].Value = value;
            }
        }

        private void ApplyModernStyle()
        {
            this.BackColor = Color.FromArgb(236, 239, 241);
            this.Font = new Font("Segoe UI", 10, FontStyle.Regular);
        }
    }

    // Dialog dla dodawania nowego użytkownika
    public class AddUserDialog : Form
    {
        private TextBox idTextBox;
        private TextBox nameTextBox;
        private Button okButton;
        private Button cancelButton;
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public AddUserDialog()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "Dodaj nowego użytkownika";
            this.Size = new Size(400, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var idLabel = new Label
            {
                Text = "ID użytkownika:",
                Location = new Point(30, 30),
                AutoSize = true
            };
            this.Controls.Add(idLabel);

            idTextBox = new TextBox
            {
                Location = new Point(150, 27),
                Size = new Size(200, 25)
            };
            this.Controls.Add(idTextBox);

            var nameLabel = new Label
            {
                Text = "Nazwa:",
                Location = new Point(30, 70),
                AutoSize = true
            };
            this.Controls.Add(nameLabel);

            nameTextBox = new TextBox
            {
                Location = new Point(150, 67),
                Size = new Size(200, 25)
            };
            this.Controls.Add(nameTextBox);

            okButton = new Button
            {
                Text = "Dodaj",
                Location = new Point(100, 120),
                Size = new Size(90, 35),
                BackColor = Color.FromArgb(46, 125, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            okButton.Click += OkButton_Click;
            this.Controls.Add(okButton);

            cancelButton = new Button
            {
                Text = "Anuluj",
                Location = new Point(210, 120),
                Size = new Size(90, 35),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(cancelButton);
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(idTextBox.Text) ||
                string.IsNullOrWhiteSpace(nameTextBox.Text))
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
                    string query = @"INSERT INTO operators (ID, Name) 
                                   VALUES (@id, @name)";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", idTextBox.Text);
                        cmd.Parameters.AddWithValue("@name", nameTextBox.Text);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Użytkownik został dodany.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dodawania użytkownika: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.None;
            }
        }
    }
}