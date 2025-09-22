// Plik: Transport/Formularze/KierowcyForm.cs
// Naprawiony formularz do zarządzania kierowcami - POPRAWKA DODAWANIA

using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Transport.Formularze
{
    public class KierowcyForm : Form
    {
        private readonly string _connectionString = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Kontrolki
        private DataGridView dgvKierowcy;
        private TextBox txtImie;
        private TextBox txtNazwisko;
        private TextBox txtTelefon;
        private CheckBox chkAktywny;
        private Button btnDodaj;
        private Button btnZapisz;
        private Button btnUsun;
        private Button btnAnuluj;
        private Panel panelEdycji;
        private Label lblTytul;
        private Label lblStatystyki;

        private DataTable _dtKierowcy;
        private int? _selectedKierowcaId;
        private bool _isAddingNew = false; // Flaga dla nowego rekordu

        public KierowcyForm()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _ = LoadKierowcyAsync();
        }

        private void InitializeComponent()
        {
            Text = "Zarządzanie kierowcami";
            Size = new Size(1200, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(240, 242, 247);

            // Panel główny - TablelayoutPanel zamiast SplitContainer
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            // Ustawienia kolumn - 70% grid, 30% edycja
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

            // Ustawienia wierszy - header i content
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
                Text = "ZARZĄDZANIE KIEROWCAMI",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 25),
                AutoSize = true
            };

            lblStatystyki = new Label
            {
                Text = "Kierowcy: 0 aktywnych / 0 wszystkich",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(255, 193, 7),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AutoSize = true
            };

            panelHeader.Controls.AddRange(new Control[] { lblTytul, lblStatystyki });

            // Pozycjonowanie statystyk po prawej stronie
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

            dgvKierowcy = new DataGridView
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

            // Stylizacja grida
            dgvKierowcy.EnableHeadersVisualStyles = false;
            dgvKierowcy.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 252);
            dgvKierowcy.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(52, 73, 94);
            dgvKierowcy.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgvKierowcy.ColumnHeadersHeight = 45;
            dgvKierowcy.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgvKierowcy.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgvKierowcy.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);
            dgvKierowcy.RowTemplate.Height = 35;
            dgvKierowcy.GridColor = Color.FromArgb(236, 240, 241);

            dgvKierowcy.SelectionChanged += DgvKierowcy_SelectionChanged;
            dgvKierowcy.CellFormatting += DgvKierowcy_CellFormatting;

            panelGrid.Controls.Add(dgvKierowcy);

            // PRAWA STRONA - Panel edycji
            panelEdycji = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10, 10, 20, 20)
            };

            var lblEdycjaTytul = new Label
            {
                Text = "SZCZEGÓŁY KIEROWCY",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(20, 20),
                Size = new Size(300, 30)
            };

            // Pola edycji
            var lblImie = CreateLabel("Imię:", 20, 70);
            txtImie = CreateTextBox(20, 95, 250);

            var lblNazwisko = CreateLabel("Nazwisko:", 20, 135);
            txtNazwisko = CreateTextBox(20, 160, 250);

            var lblTelefon = CreateLabel("Telefon:", 20, 200);
            txtTelefon = CreateTextBox(20, 225, 250);
            txtTelefon.PlaceholderText = "np. 601 234 567";

            chkAktywny = new CheckBox
            {
                Text = "Aktywny",
                Location = new Point(20, 275),
                Size = new Size(150, 30),
                Font = new Font("Segoe UI", 10F),
                Checked = true
            };

            // Przyciski
            var panelButtons = new FlowLayoutPanel
            {
                Location = new Point(20, 330),
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
                lblEdycjaTytul, lblImie, txtImie, lblNazwisko, txtNazwisko,
                lblTelefon, txtTelefon, chkAktywny, panelButtons
            });

            // Panel dolny z przyciskiem zamknij
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

            // Pozycjonowanie przycisku zamknij
            panelBottom.Resize += (s, e) =>
            {
                if (btnAnuluj != null)
                {
                    btnAnuluj.Location = new Point(panelBottom.Width - btnAnuluj.Width - 20, 10);
                }
            };

            // Dodaj kontrolki do layoutu
            mainLayout.Controls.Add(panelHeader, 0, 0);
            mainLayout.SetColumnSpan(panelHeader, 2); // Header na całą szerokość

            mainLayout.Controls.Add(panelGrid, 0, 1);
            mainLayout.Controls.Add(panelEdycji, 1, 1);

            // Dodaj layout i panel dolny do formularza
            var rootPanel = new Panel { Dock = DockStyle.Fill };
            rootPanel.Controls.Add(mainLayout);
            rootPanel.Controls.Add(panelBottom);

            // Panel dolny na dole
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
                Size = new Size(100, 20),
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

        private async Task LoadKierowcyAsync()
        {
            try
            {
                _dtKierowcy = new DataTable();

                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Zmienione zapytanie - konwertuje Aktywny na tekst już w SQL
                var sql = @"SELECT KierowcaID, Imie, Nazwisko, Telefon, 
                                  CASE WHEN Aktywny = 1 THEN 'Aktywny' ELSE 'Nieaktywny' END AS StatusText,
                                  Aktywny,
                                  UtworzonoUTC, ZmienionoUTC
                           FROM dbo.Kierowca
                           ORDER BY Nazwisko, Imie";

                using var adapter = new SqlDataAdapter(sql, connection);
                adapter.Fill(_dtKierowcy);

                dgvKierowcy.DataSource = _dtKierowcy;

                // Konfiguracja kolumn
                if (dgvKierowcy.Columns["KierowcaID"] != null)
                    dgvKierowcy.Columns["KierowcaID"].Visible = false;

                if (dgvKierowcy.Columns["Imie"] != null)
                {
                    dgvKierowcy.Columns["Imie"].HeaderText = "Imię";
                    dgvKierowcy.Columns["Imie"].Width = 120;
                }

                if (dgvKierowcy.Columns["Nazwisko"] != null)
                {
                    dgvKierowcy.Columns["Nazwisko"].HeaderText = "Nazwisko";
                    dgvKierowcy.Columns["Nazwisko"].Width = 150;
                }

                if (dgvKierowcy.Columns["Telefon"] != null)
                {
                    dgvKierowcy.Columns["Telefon"].HeaderText = "Telefon";
                    dgvKierowcy.Columns["Telefon"].Width = 120;
                }

                // Ukryj oryginalną kolumnę Aktywny
                if (dgvKierowcy.Columns["Aktywny"] != null)
                {
                    dgvKierowcy.Columns["Aktywny"].Visible = false;
                }

                // Wyświetl kolumnę StatusText
                if (dgvKierowcy.Columns["StatusText"] != null)
                {
                    dgvKierowcy.Columns["StatusText"].HeaderText = "Status";
                    dgvKierowcy.Columns["StatusText"].Width = 80;
                }

                if (dgvKierowcy.Columns["UtworzonoUTC"] != null)
                {
                    dgvKierowcy.Columns["UtworzonoUTC"].HeaderText = "Utworzono";
                    dgvKierowcy.Columns["UtworzonoUTC"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
                }

                if (dgvKierowcy.Columns["ZmienionoUTC"] != null)
                    dgvKierowcy.Columns["ZmienionoUTC"].Visible = false;

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
            if (_dtKierowcy == null) return;

            int wszystkich = _dtKierowcy.Rows.Count;
            int aktywnych = _dtKierowcy.AsEnumerable().Count(r => r.Field<bool>("Aktywny"));

            lblStatystyki.Text = $"Kierowcy: {aktywnych} aktywnych / {wszystkich} wszystkich";
        }

        private void DgvKierowcy_SelectionChanged(object sender, EventArgs e)
        {
            // Nie reaguj na zmianę zaznaczenia jeśli dodajemy nowy rekord
            if (_isAddingNew) return;

            if (dgvKierowcy.CurrentRow == null)
            {
                ClearForm();
                return;
            }

            try
            {
                var row = dgvKierowcy.CurrentRow;

                var kierowcaIdValue = row.Cells["KierowcaID"]?.Value;
                if (kierowcaIdValue == null || kierowcaIdValue == DBNull.Value)
                {
                    ClearForm();
                    return;
                }

                _selectedKierowcaId = Convert.ToInt32(kierowcaIdValue);

                txtImie.Text = row.Cells["Imie"]?.Value?.ToString() ?? "";
                txtNazwisko.Text = row.Cells["Nazwisko"]?.Value?.ToString() ?? "";
                txtTelefon.Text = row.Cells["Telefon"]?.Value?.ToString() ?? "";

                // Boolean wartość
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
                System.Diagnostics.Debug.WriteLine($"Error in DgvKierowcy_SelectionChanged: {ex}");
                ClearForm();
            }
        }

        private void DgvKierowcy_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // Formatuj kolumnę StatusText
            if (dgvKierowcy.Columns[e.ColumnIndex].Name == "StatusText")
            {
                if (e.Value != null && e.Value.ToString() == "Nieaktywny")
                {
                    e.CellStyle.ForeColor = Color.Gray;
                    e.CellStyle.Font = new Font(dgvKierowcy.Font, FontStyle.Italic);
                }
            }
        }

        private void ClearForm()
        {
            _selectedKierowcaId = null;
            txtImie.Clear();
            txtNazwisko.Clear();
            txtTelefon.Clear();
            chkAktywny.Checked = true;
            btnZapisz.Enabled = false;
            btnUsun.Enabled = false;
            txtImie.Focus();
        }

        private void BtnDodaj_Click(object sender, EventArgs e)
        {
            _isAddingNew = true;
            ClearForm();
            dgvKierowcy.ClearSelection();
            btnZapisz.Enabled = true; // Włącz przycisk zapisz dla nowego rekordu
            _isAddingNew = false;
        }

        private async void BtnZapisz_Click(object sender, EventArgs e)
        {
            // Walidacja
            if (string.IsNullOrWhiteSpace(txtImie.Text))
            {
                MessageBox.Show("Podaj imię kierowcy.", "Brak danych",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtImie.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNazwisko.Text))
            {
                MessageBox.Show("Podaj nazwisko kierowcy.", "Brak danych",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNazwisko.Focus();
                return;
            }

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                if (_selectedKierowcaId.HasValue)
                {
                    // Aktualizacja
                    var sql = @"UPDATE dbo.Kierowca 
                               SET Imie = @Imie, Nazwisko = @Nazwisko, 
                                   Telefon = @Telefon, Aktywny = @Aktywny,
                                   ZmienionoUTC = SYSUTCDATETIME()
                               WHERE KierowcaID = @KierowcaID";

                    using var cmd = new SqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@KierowcaID", _selectedKierowcaId.Value);
                    cmd.Parameters.AddWithValue("@Imie", txtImie.Text.Trim());
                    cmd.Parameters.AddWithValue("@Nazwisko", txtNazwisko.Text.Trim());
                    cmd.Parameters.AddWithValue("@Telefon",
                        string.IsNullOrWhiteSpace(txtTelefon.Text) ? DBNull.Value : txtTelefon.Text.Trim());
                    cmd.Parameters.AddWithValue("@Aktywny", chkAktywny.Checked);

                    await cmd.ExecuteNonQueryAsync();

                    MessageBox.Show("Dane kierowcy zostały zaktualizowane.",
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // Nowy kierowca
                    var sql = @"INSERT INTO dbo.Kierowca (Imie, Nazwisko, Telefon, Aktywny, UtworzonoUTC)
                               OUTPUT INSERTED.KierowcaID
                               VALUES (@Imie, @Nazwisko, @Telefon, @Aktywny, SYSUTCDATETIME())";

                    using var cmd = new SqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@Imie", txtImie.Text.Trim());
                    cmd.Parameters.AddWithValue("@Nazwisko", txtNazwisko.Text.Trim());
                    cmd.Parameters.AddWithValue("@Telefon",
                        string.IsNullOrWhiteSpace(txtTelefon.Text) ? DBNull.Value : txtTelefon.Text.Trim());
                    cmd.Parameters.AddWithValue("@Aktywny", chkAktywny.Checked);

                    var newId = await cmd.ExecuteScalarAsync();

                    MessageBox.Show($"Nowy kierowca został dodany (ID: {newId}).",
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                await LoadKierowcyAsync();
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
            if (!_selectedKierowcaId.HasValue) return;

            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć kierowcę {txtImie.Text} {txtNazwisko.Text}?\n\n" +
                "Uwaga: Kierowca nie może być usunięty jeśli jest przypisany do kursów.",
                "Potwierdzenie usunięcia",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Sprawdź czy kierowca jest używany
                var sqlCheck = @"SELECT COUNT(*) FROM dbo.Kurs WHERE KierowcaID = @KierowcaID";
                using var cmdCheck = new SqlCommand(sqlCheck, connection);
                cmdCheck.Parameters.AddWithValue("@KierowcaID", _selectedKierowcaId.Value);

                int count = (int)await cmdCheck.ExecuteScalarAsync();

                if (count > 0)
                {
                    MessageBox.Show(
                        $"Nie można usunąć kierowcy, ponieważ jest przypisany do {count} kursów.\n\n" +
                        "Możesz oznaczyć kierowcę jako nieaktywnego.",
                        "Nie można usunąć",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Usuń kierowcę
                var sqlDelete = "DELETE FROM dbo.Kierowca WHERE KierowcaID = @KierowcaID";
                using var cmdDelete = new SqlCommand(sqlDelete, connection);
                cmdDelete.Parameters.AddWithValue("@KierowcaID", _selectedKierowcaId.Value);

                await cmdDelete.ExecuteNonQueryAsync();

                MessageBox.Show("Kierowca został usunięty.",
                    "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadKierowcyAsync();
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