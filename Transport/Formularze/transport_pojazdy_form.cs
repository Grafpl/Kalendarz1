// Plik: Transport/Formularze/PojazdyForm.cs
// Poprawiony formularz do zarządzania pojazdami

using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Transport.Formularze
{
    public class PojazdyForm : Form
    {
        private readonly string _connectionString = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Kontrolki
        private DataGridView dgvPojazdy;
        private TextBox txtRejestracja;
        private TextBox txtMarka;
        private TextBox txtModel;
        private NumericUpDown nudPalety;
        private CheckBox chkAktywny;
        private Button btnDodaj;
        private Button btnZapisz;
        private Button btnUsun;
        private Button btnAnuluj;
        private Panel panelEdycji;
        private Label lblTytul;
        private Label lblStatystyki;

        private DataTable _dtPojazdy;
        private int? _selectedPojazdId;
        private bool _isAddingNew = false;

        public PojazdyForm()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _ = LoadPojazdyAsync();
        }

        private void InitializeComponent()
        {
            Text = "Zarządzanie pojazdami";
            Size = new Size(1200, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(240, 242, 247);

            // Panel główny
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Panel nagłówka
            var panelHeader = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(41, 44, 51),
                Padding = new Padding(20)
            };

            lblTytul = new Label
            {
                Text = "ZARZĄDZANIE POJAZDAMI",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 25),
                AutoSize = true
            };

            lblStatystyki = new Label
            {
                Text = "Pojazdy: 0 aktywnych / 0 wszystkich",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(255, 193, 7),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AutoSize = true
            };

            panelHeader.Controls.AddRange(new Control[] { lblTytul, lblStatystyki });

            panelHeader.Resize += (s, e) =>
            {
                if (lblStatystyki != null)
                {
                    lblStatystyki.Location = new Point(panelHeader.Width - lblStatystyki.Width - 20, 30);
                }
            };

            // LEWA STRONA - Grid
            var panelGrid = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 10, 10, 20),
                BackColor = Color.White
            };

            dgvPojazdy = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            dgvPojazdy.EnableHeadersVisualStyles = false;
            dgvPojazdy.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 252);
            dgvPojazdy.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(52, 73, 94);
            dgvPojazdy.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgvPojazdy.ColumnHeadersHeight = 45;
            dgvPojazdy.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgvPojazdy.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgvPojazdy.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);
            dgvPojazdy.RowTemplate.Height = 35;
            dgvPojazdy.GridColor = Color.FromArgb(236, 240, 241);

            dgvPojazdy.SelectionChanged += DgvPojazdy_SelectionChanged;
            dgvPojazdy.CellFormatting += DgvPojazdy_CellFormatting;

            panelGrid.Controls.Add(dgvPojazdy);

            // PRAWA STRONA - Panel edycji
            panelEdycji = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10, 10, 20, 20)
            };

            var lblEdycjaTytul = new Label
            {
                Text = "SZCZEGÓŁY POJAZDU",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(20, 20),
                Size = new Size(300, 30)
            };

            var lblRejestracja = CreateLabel("Numer rejestracyjny:", 20, 70);
            txtRejestracja = CreateTextBox(20, 95, 250);
            txtRejestracja.CharacterCasing = CharacterCasing.Upper;
            txtRejestracja.PlaceholderText = "np. EL 123AB";

            var lblMarka = CreateLabel("Marka:", 20, 135);
            txtMarka = CreateTextBox(20, 160, 250);
            txtMarka.PlaceholderText = "np. Mercedes-Benz";

            var lblModel = CreateLabel("Model:", 20, 200);
            txtModel = CreateTextBox(20, 225, 250);
            txtModel.PlaceholderText = "np. Actros 2545";

            var lblPalety = CreateLabel("Liczba palet H1:", 20, 265);
            nudPalety = new NumericUpDown
            {
                Location = new Point(20, 290),
                Size = new Size(150, 26),
                Font = new Font("Segoe UI", 10F),
                Minimum = 1,
                Maximum = 50,
                Value = 33,
                TextAlign = HorizontalAlignment.Center
            };

            var lblPaletyInfo = new Label
            {
                Text = "Standardowa naczepka: 33 palety",
                Location = new Point(20, 320),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.Gray
            };

            chkAktywny = new CheckBox
            {
                Text = "Aktywny",
                Location = new Point(20, 350),
                Size = new Size(150, 30),
                Font = new Font("Segoe UI", 10F),
                Checked = true
            };

            var panelButtons = new FlowLayoutPanel
            {
                Location = new Point(20, 390),
                Size = new Size(280, 120),
                FlowDirection = FlowDirection.TopDown
            };

            btnDodaj = CreateButton("NOWY", Color.FromArgb(40, 167, 69), 250);
            btnDodaj.Click += BtnDodaj_Click;

            btnZapisz = CreateButton("ZAPISZ", Color.FromArgb(0, 123, 255), 250);
            btnZapisz.Click += BtnZapisz_Click;
            btnZapisz.Enabled = false;

            btnUsun = CreateButton("USUŃ", Color.FromArgb(220, 53, 69), 250);
            btnUsun.Click += BtnUsun_Click;
            btnUsun.Enabled = false;

            panelButtons.Controls.AddRange(new Control[] { btnDodaj, btnZapisz, btnUsun });

            panelEdycji.Controls.AddRange(new Control[] {
                lblEdycjaTytul, lblRejestracja, txtRejestracja,
                lblMarka, txtMarka, lblModel, txtModel,
                lblPalety, nudPalety, lblPaletyInfo, chkAktywny, panelButtons
            });

            // Panel dolny
            var panelBottom = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(33, 37, 43),
                Padding = new Padding(20, 10, 20, 10)
            };

            btnAnuluj = new Button
            {
                Text = "ZAMKNIJ",
                Size = new Size(120, 40),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;
            btnAnuluj.Click += (s, e) => Close();

            panelBottom.Controls.Add(btnAnuluj);

            panelBottom.Resize += (s, e) =>
            {
                if (btnAnuluj != null)
                {
                    btnAnuluj.Location = new Point(panelBottom.Width - btnAnuluj.Width - 20, 10);
                }
            };

            mainLayout.Controls.Add(panelHeader, 0, 0);
            mainLayout.SetColumnSpan(panelHeader, 2);
            mainLayout.Controls.Add(panelGrid, 0, 1);
            mainLayout.Controls.Add(panelEdycji, 1, 1);

            var rootPanel = new Panel { Dock = DockStyle.Fill };
            rootPanel.Controls.Add(mainLayout);
            rootPanel.Controls.Add(panelBottom);

            panelBottom.Dock = DockStyle.Bottom;
            panelBottom.Height = 60;

            Controls.Add(rootPanel);
        }

        private Label CreateLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94)
            };
        }

        private TextBox CreateTextBox(int x, int y, int width)
        {
            return new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 26),
                Font = new Font("Segoe UI", 10F),
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private Button CreateButton(string text, Color color, int width)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(width, 35),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 5)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(color, 0.1f);
            return btn;
        }

        private async Task LoadPojazdyAsync()
        {
            try
            {
                _dtPojazdy = new DataTable();

                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Zmienione zapytanie - konwertuje Aktywny na tekst już w SQL
                var sql = @"SELECT PojazdID, Rejestracja, Marka, Model, PaletyH1, 
                                  CASE WHEN Aktywny = 1 THEN 'Aktywny' ELSE 'Nieaktywny' END AS StatusText,
                                  Aktywny,
                                  UtworzonoUTC, ZmienionoUTC
                           FROM dbo.Pojazd
                           ORDER BY Rejestracja";

                using var adapter = new SqlDataAdapter(sql, connection);
                adapter.Fill(_dtPojazdy);

                dgvPojazdy.DataSource = _dtPojazdy;

                // Konfiguracja kolumn
                if (dgvPojazdy.Columns["PojazdID"] != null)
                    dgvPojazdy.Columns["PojazdID"].Visible = false;

                if (dgvPojazdy.Columns["Rejestracja"] != null)
                {
                    dgvPojazdy.Columns["Rejestracja"].HeaderText = "Nr rejestracyjny";
                    dgvPojazdy.Columns["Rejestracja"].Width = 130;
                }

                if (dgvPojazdy.Columns["Marka"] != null)
                {
                    dgvPojazdy.Columns["Marka"].HeaderText = "Marka";
                    dgvPojazdy.Columns["Marka"].Width = 120;
                }

                if (dgvPojazdy.Columns["Model"] != null)
                {
                    dgvPojazdy.Columns["Model"].HeaderText = "Model";
                    dgvPojazdy.Columns["Model"].Width = 150;
                }

                if (dgvPojazdy.Columns["PaletyH1"] != null)
                {
                    dgvPojazdy.Columns["PaletyH1"].HeaderText = "Palety";
                    dgvPojazdy.Columns["PaletyH1"].Width = 70;
                    dgvPojazdy.Columns["PaletyH1"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }

                // Ukryj oryginalną kolumnę Aktywny
                if (dgvPojazdy.Columns["Aktywny"] != null)
                {
                    dgvPojazdy.Columns["Aktywny"].Visible = false;
                }

                // Wyświetl kolumnę StatusText
                if (dgvPojazdy.Columns["StatusText"] != null)
                {
                    dgvPojazdy.Columns["StatusText"].HeaderText = "Status";
                    dgvPojazdy.Columns["StatusText"].Width = 80;
                }

                if (dgvPojazdy.Columns["UtworzonoUTC"] != null)
                {
                    dgvPojazdy.Columns["UtworzonoUTC"].HeaderText = "Utworzono";
                    dgvPojazdy.Columns["UtworzonoUTC"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
                }

                if (dgvPojazdy.Columns["ZmienionoUTC"] != null)
                    dgvPojazdy.Columns["ZmienionoUTC"].Visible = false;

                UpdateStatystyki();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateStatystyki()
        {
            if (_dtPojazdy == null) return;

            int wszystkich = _dtPojazdy.Rows.Count;
            int aktywnych = _dtPojazdy.AsEnumerable().Count(r => r.Field<bool>("Aktywny"));
            int sumaPalet = _dtPojazdy.AsEnumerable()
                .Where(r => r.Field<bool>("Aktywny"))
                .Sum(r => r.Field<int>("PaletyH1"));

            lblStatystyki.Text = $"Pojazdy: {aktywnych} aktywnych / {wszystkich} wszystkich | Łącznie {sumaPalet} palet";
        }

        private void DgvPojazdy_SelectionChanged(object sender, EventArgs e)
        {
            if (_isAddingNew) return;

            if (dgvPojazdy.CurrentRow == null)
            {
                ClearForm();
                return;
            }

            try
            {
                var row = dgvPojazdy.CurrentRow;

                var pojazdIdValue = row.Cells["PojazdID"]?.Value;
                if (pojazdIdValue == null || pojazdIdValue == DBNull.Value)
                {
                    ClearForm();
                    return;
                }

                _selectedPojazdId = Convert.ToInt32(pojazdIdValue);

                txtRejestracja.Text = row.Cells["Rejestracja"]?.Value?.ToString() ?? "";
                txtMarka.Text = row.Cells["Marka"]?.Value?.ToString() ?? "";
                txtModel.Text = row.Cells["Model"]?.Value?.ToString() ?? "";

                var paletyValue = row.Cells["PaletyH1"]?.Value;
                if (paletyValue != null && paletyValue != DBNull.Value)
                {
                    nudPalety.Value = Convert.ToInt32(paletyValue);
                }
                else
                {
                    nudPalety.Value = 33;
                }

                var aktywnyValue = row.Cells["Aktywny"]?.Value;
                if (aktywnyValue != null && aktywnyValue != DBNull.Value)
                {
                    if (aktywnyValue is bool boolVal)
                    {
                        chkAktywny.Checked = boolVal;
                    }
                    else
                    {
                        chkAktywny.Checked = true;
                    }
                }
                else
                {
                    chkAktywny.Checked = true;
                }

                btnZapisz.Enabled = true;
                btnUsun.Enabled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DgvPojazdy_SelectionChanged: {ex}");
                ClearForm();
            }
        }

        private void DgvPojazdy_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // Formatuj kolumnę StatusText
            if (dgvPojazdy.Columns[e.ColumnIndex].Name == "StatusText")
            {
                if (e.Value != null && e.Value.ToString() == "Nieaktywny")
                {
                    e.CellStyle.ForeColor = Color.Gray;
                    e.CellStyle.Font = new Font(dgvPojazdy.Font, FontStyle.Italic);
                }
            }

            // Formatuj kolumnę PaletyH1
            if (dgvPojazdy.Columns[e.ColumnIndex].Name == "PaletyH1")
            {
                e.CellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);

                if (e.Value != null && int.TryParse(e.Value.ToString(), out int palety))
                {
                    if (palety >= 33)
                        e.CellStyle.ForeColor = Color.FromArgb(40, 167, 69);
                    else if (palety >= 20)
                        e.CellStyle.ForeColor = Color.FromArgb(255, 193, 7);
                    else
                        e.CellStyle.ForeColor = Color.FromArgb(220, 53, 69);
                }
            }
        }

        private void ClearForm()
        {
            _selectedPojazdId = null;
            txtRejestracja.Clear();
            txtMarka.Clear();
            txtModel.Clear();
            nudPalety.Value = 33;
            chkAktywny.Checked = true;
            btnZapisz.Enabled = false;
            btnUsun.Enabled = false;
            txtRejestracja.Focus();
        }

        private void BtnDodaj_Click(object sender, EventArgs e)
        {
            _isAddingNew = true;
            ClearForm();
            dgvPojazdy.ClearSelection();
            btnZapisz.Enabled = true;
            _isAddingNew = false;
        }

        private async void BtnZapisz_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtRejestracja.Text))
            {
                MessageBox.Show("Podaj numer rejestracyjny pojazdu.", "Brak danych",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRejestracja.Focus();
                return;
            }

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Sprawdź duplikat
                var sqlCheck = @"SELECT COUNT(*) FROM dbo.Pojazd 
                                WHERE Rejestracja = @Rejestracja 
                                AND (@PojazdID IS NULL OR PojazdID != @PojazdID)";

                using var cmdCheck = new SqlCommand(sqlCheck, connection);
                cmdCheck.Parameters.AddWithValue("@Rejestracja", txtRejestracja.Text.Trim().ToUpper());
                cmdCheck.Parameters.AddWithValue("@PojazdID",
                    _selectedPojazdId.HasValue ? (object)_selectedPojazdId.Value : DBNull.Value);

                int existingCount = (int)await cmdCheck.ExecuteScalarAsync();

                if (existingCount > 0)
                {
                    MessageBox.Show("Pojazd o tym numerze rejestracyjnym już istnieje.",
                        "Duplikat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtRejestracja.Focus();
                    return;
                }

                if (_selectedPojazdId.HasValue)
                {
                    // Aktualizacja
                    var sql = @"UPDATE dbo.Pojazd 
                               SET Rejestracja = @Rejestracja, Marka = @Marka, 
                                   Model = @Model, PaletyH1 = @PaletyH1, 
                                   Aktywny = @Aktywny, ZmienionoUTC = SYSUTCDATETIME()
                               WHERE PojazdID = @PojazdID";

                    using var cmd = new SqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@PojazdID", _selectedPojazdId.Value);
                    cmd.Parameters.AddWithValue("@Rejestracja", txtRejestracja.Text.Trim().ToUpper());
                    cmd.Parameters.AddWithValue("@Marka",
                        string.IsNullOrWhiteSpace(txtMarka.Text) ? DBNull.Value : txtMarka.Text.Trim());
                    cmd.Parameters.AddWithValue("@Model",
                        string.IsNullOrWhiteSpace(txtModel.Text) ? DBNull.Value : txtModel.Text.Trim());
                    cmd.Parameters.AddWithValue("@PaletyH1", (int)nudPalety.Value);
                    cmd.Parameters.AddWithValue("@Aktywny", chkAktywny.Checked);

                    await cmd.ExecuteNonQueryAsync();

                    MessageBox.Show("Dane pojazdu zostały zaktualizowane.",
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // Nowy pojazd
                    var sql = @"INSERT INTO dbo.Pojazd (Rejestracja, Marka, Model, PaletyH1, Aktywny, UtworzonoUTC)
                               OUTPUT INSERTED.PojazdID
                               VALUES (@Rejestracja, @Marka, @Model, @PaletyH1, @Aktywny, SYSUTCDATETIME())";

                    using var cmd = new SqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@Rejestracja", txtRejestracja.Text.Trim().ToUpper());
                    cmd.Parameters.AddWithValue("@Marka",
                        string.IsNullOrWhiteSpace(txtMarka.Text) ? DBNull.Value : txtMarka.Text.Trim());
                    cmd.Parameters.AddWithValue("@Model",
                        string.IsNullOrWhiteSpace(txtModel.Text) ? DBNull.Value : txtModel.Text.Trim());
                    cmd.Parameters.AddWithValue("@PaletyH1", (int)nudPalety.Value);
                    cmd.Parameters.AddWithValue("@Aktywny", chkAktywny.Checked);

                    var newId = await cmd.ExecuteScalarAsync();

                    MessageBox.Show($"Nowy pojazd został dodany (ID: {newId}).",
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                await LoadPojazdyAsync();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnUsun_Click(object sender, EventArgs e)
        {
            if (!_selectedPojazdId.HasValue) return;

            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć pojazd {txtRejestracja.Text}?\n\n" +
                "Uwaga: Pojazd nie może być usunięty jeśli jest przypisany do kursów.",
                "Potwierdzenie usunięcia",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Sprawdź czy pojazd jest używany
                var sqlCheck = @"SELECT COUNT(*) FROM dbo.Kurs WHERE PojazdID = @PojazdID";
                using var cmdCheck = new SqlCommand(sqlCheck, connection);
                cmdCheck.Parameters.AddWithValue("@PojazdID", _selectedPojazdId.Value);

                int count = (int)await cmdCheck.ExecuteScalarAsync();

                if (count > 0)
                {
                    MessageBox.Show(
                        $"Nie można usunąć pojazdu, ponieważ jest przypisany do {count} kursów.\n\n" +
                        "Możesz oznaczyć pojazd jako nieaktywny.",
                        "Nie można usunąć",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Usuń pojazd
                var sqlDelete = "DELETE FROM dbo.Pojazd WHERE PojazdID = @PojazdID";
                using var cmdDelete = new SqlCommand(sqlDelete, connection);
                cmdDelete.Parameters.AddWithValue("@PojazdID", _selectedPojazdId.Value);

                await cmdDelete.ExecuteNonQueryAsync();

                MessageBox.Show("Pojazd został usunięty.",
                    "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadPojazdyAsync();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas usuwania: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}