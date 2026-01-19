using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    // Nowa forma do zarządzania handlowcami
    public class ZarzadzanieHandlowcamiForm : Form
    {
        private readonly string _connectionString;
        private DataGridView dgvHandlowcy;
        private Button btnDodaj, btnEdytuj, btnUsun, btnAktywujDeaktywuj, btnOdswiez;
        private TextBox txtSzukaj;

        public ZarzadzanieHandlowcamiForm(string connectionString)
        {
            _connectionString = connectionString;
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            ZaladujHandlowcow();
        }

        private void InitializeComponent()
        {
            this.Text = "👥 Zarządzanie handlowcami";
            this.Size = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterParent;

            // Panel górny
            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                Padding = new Padding(10),
                BackColor = ColorTranslator.FromHtml("#ecf0f1")
            };

            Label lblSzukaj = new Label
            {
                Text = "🔍 Szukaj:",
                Location = new Point(10, 20),
                Size = new Size(60, 20)
            };

            txtSzukaj = new TextBox
            {
                Location = new Point(75, 18),
                Size = new Size(200, 23)
            };
            txtSzukaj.TextChanged += (s, e) => ZaladujHandlowcow();

            btnOdswiez = new Button
            {
                Text = "🔄 Odśwież",
                Location = new Point(290, 15),
                Size = new Size(100, 30),
                BackColor = ColorTranslator.FromHtml("#3498db"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOdswiez.FlatAppearance.BorderSize = 0;
            btnOdswiez.Click += (s, e) => ZaladujHandlowcow();

            topPanel.Controls.AddRange(new Control[] { lblSzukaj, txtSzukaj, btnOdswiez });

            // Panel z przyciskami
            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = ColorTranslator.FromHtml("#ecf0f1")
            };

            btnDodaj = CreateButton("➕ Dodaj", 10, ColorTranslator.FromHtml("#27ae60"));
            btnDodaj.Click += BtnDodaj_Click;

            btnEdytuj = CreateButton("✏ Edytuj", 120, ColorTranslator.FromHtml("#f39c12"));
            btnEdytuj.Click += BtnEdytuj_Click;

            btnUsun = CreateButton("🗑 Usuń", 230, ColorTranslator.FromHtml("#e74c3c"));
            btnUsun.Click += BtnUsun_Click;

            btnAktywujDeaktywuj = CreateButton("🔄 Zmień status", 340, ColorTranslator.FromHtml("#9b59b6"));
            btnAktywujDeaktywuj.Click += BtnAktywujDeaktywuj_Click;

            buttonPanel.Controls.AddRange(new Control[] { btnDodaj, btnEdytuj, btnUsun, btnAktywujDeaktywuj });

            // DataGridView
            dgvHandlowcy = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White
            };

            this.Controls.Add(dgvHandlowcy);
            this.Controls.Add(topPanel);
            this.Controls.Add(buttonPanel);
        }

        private Button CreateButton(string text, int x, Color backColor)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, 10),
                Size = new Size(100, 30),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void ZaladujHandlowcow()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    string query = @"
                        SELECT 
                            HandlowiecID AS [ID],
                            UserID AS [ID Użytkownika],
                            Nazwa AS [Imię],
                            Email AS [E-mail],
                            Telefon AS [Telefon],
                            CASE WHEN Aktywny = 1 THEN '✅ Aktywny' ELSE '❌ Nieaktywny' END AS [Status],
                            DataUtworzenia AS [Data utworzenia],
                            DataModyfikacji AS [Ostatnia modyfikacja],
                            ModyfikowanyPrzez AS [Zmodyfikowany przez]
                        FROM [HANDEL].[dbo].[Handlowcy]
                        WHERE (@szukaj IS NULL 
                               OR UserID LIKE '%' + @szukaj + '%' 
                               OR Nazwa LIKE '%' + @szukaj + '%'
                               OR Email LIKE '%' + @szukaj + '%')
                        ORDER BY Nazwa";

                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@szukaj",
                        string.IsNullOrWhiteSpace(txtSzukaj.Text) ? DBNull.Value : (object)txtSzukaj.Text);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    dgvHandlowcy.DataSource = dt;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd ładowania handlowców: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDodaj_Click(object sender, EventArgs e)
        {
            using (var form = new EdycjaHandlowcaForm(_connectionString, null))
            {
                if (form.ShowDialog() == DialogResult.OK)
                    ZaladujHandlowcow();
            }
        }

        private void BtnEdytuj_Click(object sender, EventArgs e)
        {
            if (dgvHandlowcy.SelectedRows.Count == 0)
            {
                MessageBox.Show("⚠ Wybierz handlowca do edycji.",
                    "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int handlowiecId = Convert.ToInt32(dgvHandlowcy.SelectedRows[0].Cells["ID"].Value);
            using (var form = new EdycjaHandlowcaForm(_connectionString, handlowiecId))
            {
                if (form.ShowDialog() == DialogResult.OK)
                    ZaladujHandlowcow();
            }
        }

        private void BtnUsun_Click(object sender, EventArgs e)
        {
            if (dgvHandlowcy.SelectedRows.Count == 0)
            {
                MessageBox.Show("⚠ Wybierz handlowca do usunięcia.",
                    "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string nazwa = dgvHandlowcy.SelectedRows[0].Cells["Imię"].Value.ToString();

            if (MessageBox.Show($"❓ Czy na pewno chcesz usunąć handlowca '{nazwa}'?",
                "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        var cmd = new SqlCommand(
                            "DELETE FROM [HANDEL].[dbo].[Handlowcy] WHERE HandlowiecID = @id", conn);
                        cmd.Parameters.AddWithValue("@id",
                            Convert.ToInt32(dgvHandlowcy.SelectedRows[0].Cells["ID"].Value));
                        cmd.ExecuteNonQuery();

                        MessageBox.Show("✅ Handlowiec został usunięty.",
                            "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        ZaladujHandlowcow();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Błąd usuwania: {ex.Message}",
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnAktywujDeaktywuj_Click(object sender, EventArgs e)
        {
            if (dgvHandlowcy.SelectedRows.Count == 0)
            {
                MessageBox.Show("⚠ Wybierz handlowca.",
                    "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                        UPDATE [HANDEL].[dbo].[Handlowcy] 
                        SET Aktywny = CASE WHEN Aktywny = 1 THEN 0 ELSE 1 END,
                            DataModyfikacji = GETDATE(),
                            ModyfikowanyPrzez = @user
                        WHERE HandlowiecID = @id", conn);

                    cmd.Parameters.AddWithValue("@id",
                        Convert.ToInt32(dgvHandlowcy.SelectedRows[0].Cells["ID"].Value));
                    cmd.Parameters.AddWithValue("@user", Environment.UserName);
                    cmd.ExecuteNonQuery();

                    MessageBox.Show("✅ Status handlowca został zmieniony.",
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    ZaladujHandlowcow();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd zmiany statusu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // Formularz do edycji/dodawania handlowca
    public class EdycjaHandlowcaForm : Form
    {
        private readonly string _connectionString;
        private readonly int? _handlowiecId;

        private TextBox txtUserID, txtNazwa, txtEmail, txtTelefon;
        private CheckBox chkAktywny, chkAdministrator;
        private Button btnZapisz, btnAnuluj;

        public EdycjaHandlowcaForm(string connectionString, int? handlowiecId)
        {
            _connectionString = connectionString;
            _handlowiecId = handlowiecId;
            InitializeComponent();

            if (_handlowiecId.HasValue)
                WczytajDaneHandlowca();
        }

        private void InitializeComponent()
        {
            this.Text = _handlowiecId.HasValue ? "✏ Edytuj handlowca" : "➕ Dodaj handlowca";
            this.Size = new Size(450, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            int y = 20;

            // User ID
            AddLabel("ID Użytkownika:", 20, y);
            txtUserID = AddTextBox(140, y, 200);
            y += 35;

            // Nazwa
            AddLabel("Imię/Nazwa:", 20, y);
            txtNazwa = AddTextBox(140, y, 200);
            y += 35;

            // Email
            AddLabel("E-mail:", 20, y);
            txtEmail = AddTextBox(140, y, 200);
            y += 35;

            // Telefon
            AddLabel("Telefon:", 20, y);
            txtTelefon = AddTextBox(140, y, 200);
            y += 35;

            // Aktywny
            chkAktywny = new CheckBox
            {
                Text = "✅ Aktywny",
                Location = new Point(140, y),
                Size = new Size(100, 25),
                Checked = true
            };
            this.Controls.Add(chkAktywny);

            // Administrator
            chkAdministrator = new CheckBox
            {
                Text = "👑 Administrator",
                Location = new Point(250, y),
                Size = new Size(120, 25)
            };
            this.Controls.Add(chkAdministrator);
            y += 35;

            // Przyciski
            btnZapisz = new Button
            {
                Text = "💾 Zapisz",
                Location = new Point(140, y + 20),
                Size = new Size(100, 35),
                BackColor = ColorTranslator.FromHtml("#27ae60"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.Click += BtnZapisz_Click;

            btnAnuluj = new Button
            {
                Text = "❌ Anuluj",
                Location = new Point(250, y + 20),
                Size = new Size(90, 35),
                BackColor = ColorTranslator.FromHtml("#95a5a6"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;

            this.Controls.Add(btnZapisz);
            this.Controls.Add(btnAnuluj);
        }

        private Label AddLabel(string text, int x, int y)
        {
            var label = new Label
            {
                Text = text,
                Location = new Point(x, y + 3),
                Size = new Size(110, 20),
                TextAlign = ContentAlignment.MiddleRight
            };
            this.Controls.Add(label);
            return label;
        }

        private TextBox AddTextBox(int x, int y, int width)
        {
            var textBox = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 23)
            };
            this.Controls.Add(textBox);
            return textBox;
        }

        private void WczytajDaneHandlowca()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(
                        "SELECT * FROM [HANDEL].[dbo].[Handlowcy] WHERE HandlowiecID = @id", conn);
                    cmd.Parameters.AddWithValue("@id", _handlowiecId.Value);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            txtUserID.Text = reader["UserID"].ToString();
                            txtNazwa.Text = reader["Nazwa"].ToString();
                            txtEmail.Text = reader["Email"]?.ToString() ?? "";
                            txtTelefon.Text = reader["Telefon"]?.ToString() ?? "";
                            chkAktywny.Checked = Convert.ToBoolean(reader["Aktywny"]);

                            // Sprawdź czy to administrator
                            chkAdministrator.Checked = reader["Nazwa"].ToString().ToLower().Contains("admin");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd wczytywania danych: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnZapisz_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUserID.Text) || string.IsNullOrWhiteSpace(txtNazwa.Text))
            {
                MessageBox.Show("⚠ ID Użytkownika i Nazwa są wymagane!",
                    "Błąd walidacji", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    SqlCommand cmd;

                    if (_handlowiecId.HasValue)
                    {
                        // Aktualizacja
                        cmd = new SqlCommand(@"
                            UPDATE [HANDEL].[dbo].[Handlowcy]
                            SET UserID = @userID,
                                Nazwa = @nazwa,
                                Email = @email,
                                Telefon = @telefon,
                                Aktywny = @aktywny,
                                DataModyfikacji = GETDATE(),
                                ModyfikowanyPrzez = @user
                            WHERE HandlowiecID = @id", conn);
                        cmd.Parameters.AddWithValue("@id", _handlowiecId.Value);
                    }
                    else
                    {
                        // Dodawanie
                        cmd = new SqlCommand(@"
                            INSERT INTO [HANDEL].[dbo].[Handlowcy] 
                            (UserID, Nazwa, Email, Telefon, Aktywny, DataUtworzenia, ModyfikowanyPrzez)
                            VALUES (@userID, @nazwa, @email, @telefon, @aktywny, GETDATE(), @user)", conn);
                    }

                    // Jeśli to administrator, dodaj specjalną nazwę
                    string nazwa = chkAdministrator.Checked && !txtNazwa.Text.ToLower().Contains("admin")
                        ? txtNazwa.Text + " (Administrator)"
                        : txtNazwa.Text;

                    cmd.Parameters.AddWithValue("@userID", txtUserID.Text.Trim());
                    cmd.Parameters.AddWithValue("@nazwa", nazwa);
                    cmd.Parameters.AddWithValue("@email",
                        string.IsNullOrWhiteSpace(txtEmail.Text) ? DBNull.Value : (object)txtEmail.Text);
                    cmd.Parameters.AddWithValue("@telefon",
                        string.IsNullOrWhiteSpace(txtTelefon.Text) ? DBNull.Value : (object)txtTelefon.Text);
                    cmd.Parameters.AddWithValue("@aktywny", chkAktywny.Checked);
                    cmd.Parameters.AddWithValue("@user", Environment.UserName);

                    cmd.ExecuteNonQuery();

                    MessageBox.Show("✅ Handlowiec został zapisany.",
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd zapisu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.None;
            }
        }
    }

    // Rozszerzenie AdminChangeRequestsForm o przycisk do zarządzania handlowcami
    public partial class AdminChangeRequestsForm
    {
        // Dodaj przycisk do zarządzania handlowcami w InitializeComponent
        private void DodajPrzyciskHandlowcy()
        {
            Button btnHandlowcy = new Button
            {
                Text = "👥 Zarządzaj handlowcami",
                Location = new Point(650, 10),  // Dostosuj pozycję
                Size = new Size(160, 35),
                BackColor = ColorTranslator.FromHtml("#8e44ad"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnHandlowcy.FlatAppearance.BorderSize = 0;
            btnHandlowcy.Click += (s, e) =>
            {
                using (var form = new ZarzadzanieHandlowcamiForm(_connString))
                {
                    form.ShowDialog(this);
                }
            };

            // Dodaj do panelu górnego lub gdzie chcesz
            this.Controls.Add(btnHandlowcy);  // lub dodaj do konkretnego panelu
        }
    }
}