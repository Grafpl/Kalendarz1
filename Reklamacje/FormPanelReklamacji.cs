using Kalendarz1.Reklamacje;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class FormPanelReklamacji : Form
    {
        private string connectionString;
        private string userId;

        private DataGridView dgvReklamacje;
        private ComboBox cmbFiltrStatus;
        private DateTimePicker dtpOd;
        private DateTimePicker dtpDo;
        private Button btnOdswiez;
        private Button btnSzczegoly;
        private Button btnZmienStatus;
        private Button btnStatystyki;
        private Label lblLicznik;
        private TextBox txtSzukaj;

        private DataTable dtReklamacje;

        public FormPanelReklamacji(string connString, string user)
        {
            connectionString = connString;
            userId = user;

            InitializeComponent();
            WczytajReklamacje();
        }

        private void InitializeComponent()
        {
            this.Text = "📋 Panel Reklamacji - Zarządzanie";
            this.Size = new Size(1600, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ColorTranslator.FromHtml("#f5f7fa");

            // Panel nagłówka
            Panel panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 120,
                BackColor = ColorTranslator.FromHtml("#2c3e50"),
                Padding = new Padding(20)
            };

            Label lblTytul = new Label
            {
                Text = "📋 PANEL REKLAMACJI",
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 20)
            };
            panelHeader.Controls.Add(lblTytul);

            lblLicznik = new Label
            {
                Text = "Reklamacji: 0",
                Font = new Font("Segoe UI", 12F),
                ForeColor = ColorTranslator.FromHtml("#ecf0f1"),
                AutoSize = true,
                Location = new Point(20, 60)
            };
            panelHeader.Controls.Add(lblLicznik);

            this.Controls.Add(panelHeader);

            // Panel filtrów
            Panel panelFiltry = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.White,
                Padding = new Padding(20, 15, 20, 15)
            };

            Label lblStatus = new Label
            {
                Text = "Status:",
                Location = new Point(20, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true
            };
            panelFiltry.Controls.Add(lblStatus);

            cmbFiltrStatus = new ComboBox
            {
                Location = new Point(20, 38),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            cmbFiltrStatus.Items.AddRange(new object[] { "Wszystkie", "Nowa", "W trakcie", "Zaakceptowana", "Odrzucona", "Zamknięta" });
            cmbFiltrStatus.SelectedIndex = 0;
            cmbFiltrStatus.SelectedIndexChanged += (s, e) => WczytajReklamacje();
            panelFiltry.Controls.Add(cmbFiltrStatus);

            Label lblOd = new Label
            {
                Text = "Data od:",
                Location = new Point(190, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true
            };
            panelFiltry.Controls.Add(lblOd);

            dtpOd = new DateTimePicker
            {
                Location = new Point(190, 38),
                Size = new Size(150, 25),
                Format = DateTimePickerFormat.Short,
                Font = new Font("Segoe UI", 9F)
            };
            dtpOd.Value = DateTime.Now.AddMonths(-1);
            panelFiltry.Controls.Add(dtpOd);

            Label lblDo = new Label
            {
                Text = "Data do:",
                Location = new Point(360, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true
            };
            panelFiltry.Controls.Add(lblDo);

            dtpDo = new DateTimePicker
            {
                Location = new Point(360, 38),
                Size = new Size(150, 25),
                Format = DateTimePickerFormat.Short,
                Font = new Font("Segoe UI", 9F)
            };
            panelFiltry.Controls.Add(dtpDo);

            Label lblSzukaj = new Label
            {
                Text = "Szukaj:",
                Location = new Point(530, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true
            };
            panelFiltry.Controls.Add(lblSzukaj);

            txtSzukaj = new TextBox
            {
                Location = new Point(530, 38),
                Size = new Size(250, 25),
                Font = new Font("Segoe UI", 9F)
            };
            txtSzukaj.TextChanged += (s, e) => FiltrujReklamacje();
            panelFiltry.Controls.Add(txtSzukaj);

            btnOdswiez = new Button
            {
                Text = "🔄 Odśwież",
                Location = new Point(800, 35),
                Size = new Size(120, 30),
                BackColor = ColorTranslator.FromHtml("#3498db"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnOdswiez.FlatAppearance.BorderSize = 0;
            btnOdswiez.Click += (s, e) => WczytajReklamacje();
            panelFiltry.Controls.Add(btnOdswiez);

            btnStatystyki = new Button
            {
                Text = "📊 Statystyki",
                Location = new Point(940, 35),
                Size = new Size(120, 30),
                BackColor = ColorTranslator.FromHtml("#9b59b6"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnStatystyki.FlatAppearance.BorderSize = 0;
            btnStatystyki.Click += BtnStatystyki_Click;
            panelFiltry.Controls.Add(btnStatystyki);

            this.Controls.Add(panelFiltry);

            // Panel główny z DataGridView
            Panel panelMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 10, 20, 10),
                BackColor = ColorTranslator.FromHtml("#f5f7fa")
            };

            dgvReklamacje = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 9F),
                ColumnHeadersHeight = 40,
                RowTemplate = { Height = 35 }
            };

            dgvReklamacje.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#34495e");
            dgvReklamacje.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvReklamacje.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgvReklamacje.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgvReklamacje.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);

            dgvReklamacje.EnableHeadersVisualStyles = false;
            dgvReklamacje.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#f8f9fa");
            dgvReklamacje.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#3498db");
            dgvReklamacje.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvReklamacje.CellFormatting += DgvReklamacje_CellFormatting;
            dgvReklamacje.CellDoubleClick += (s, e) => OtworzSzczegoly();

            panelMain.Controls.Add(dgvReklamacje);
            this.Controls.Add(panelMain);

            // Panel dolny z przyciskami akcji
            Panel panelFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = ColorTranslator.FromHtml("#ecf0f1"),
                Padding = new Padding(20, 15, 20, 15)
            };

            btnSzczegoly = new Button
            {
                Text = "📄 Szczegóły reklamacji",
                Size = new Size(180, 40),
                Location = new Point(20, 15),
                BackColor = ColorTranslator.FromHtml("#3498db"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnSzczegoly.FlatAppearance.BorderSize = 0;
            btnSzczegoly.Click += (s, e) => OtworzSzczegoly();
            panelFooter.Controls.Add(btnSzczegoly);

            btnZmienStatus = new Button
            {
                Text = "✏ Zmień status",
                Size = new Size(180, 40),
                Location = new Point(220, 15),
                BackColor = ColorTranslator.FromHtml("#f39c12"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnZmienStatus.FlatAppearance.BorderSize = 0;
            btnZmienStatus.Click += BtnZmienStatus_Click;
            panelFooter.Controls.Add(btnZmienStatus);

            dgvReklamacje.SelectionChanged += (s, e) =>
            {
                bool selected = dgvReklamacje.SelectedRows.Count > 0;
                btnSzczegoly.Enabled = selected;
                btnZmienStatus.Enabled = selected;
            };

            this.Controls.Add(panelFooter);
        }

        private void WczytajReklamacje()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT 
                            Id,
                            DataZgloszenia,
                            NumerDokumentu,
                            NazwaKontrahenta,
                            LEFT(Opis, 100) + CASE WHEN LEN(Opis) > 100 THEN '...' ELSE '' END AS Opis,
                            SumaKg,
                            Status,
                            UserID,
                            OsobaRozpatrujaca
                        FROM [dbo].[Reklamacje]
                        WHERE DataZgloszenia BETWEEN @DataOd AND @DataDo";

                    if (cmbFiltrStatus.SelectedIndex > 0)
                    {
                        query += " AND Status = @Status";
                    }

                    query += " ORDER BY DataZgloszenia DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", dtpOd.Value.Date);
                        cmd.Parameters.AddWithValue("@DataDo", dtpDo.Value.Date.AddDays(1).AddSeconds(-1));

                        if (cmbFiltrStatus.SelectedIndex > 0)
                        {
                            cmd.Parameters.AddWithValue("@Status", cmbFiltrStatus.SelectedItem.ToString());
                        }

                        dtReklamacje = new DataTable();
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dtReklamacje);
                        }
                    }
                }

                dgvReklamacje.DataSource = dtReklamacje;

                // Konfiguracja kolumn z bezpiecznym sprawdzaniem
                KonfigurujKolumny();

                lblLicznik.Text = $"Reklamacji: {dtReklamacje.Rows.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania reklamacji:\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FiltrujReklamacje()
        {
            if (dtReklamacje == null) return;

            string filter = txtSzukaj.Text.Trim();

            if (string.IsNullOrEmpty(filter))
            {
                (dgvReklamacje.DataSource as DataTable).DefaultView.RowFilter = "";
            }
            else
            {
                try
                {
                    string rowFilter = $"NumerDokumentu LIKE '%{filter}%' OR " +
                                      $"NazwaKontrahenta LIKE '%{filter}%' OR " +
                                      $"Opis LIKE '%{filter}%' OR " +
                                      $"CONVERT(Id, 'System.String') LIKE '%{filter}%'";

                    (dgvReklamacje.DataSource as DataTable).DefaultView.RowFilter = rowFilter;
                }
                catch { }
            }
        }

        private void DgvReklamacje_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvReklamacje.Columns[e.ColumnIndex].Name == "Status" && e.Value != null)
            {
                string status = e.Value.ToString();

                switch (status)
                {
                    case "Nowa":
                        e.CellStyle.BackColor = ColorTranslator.FromHtml("#3498db");
                        e.CellStyle.ForeColor = Color.White;
                        e.CellStyle.Font = new Font(dgvReklamacje.Font, FontStyle.Bold);
                        break;
                    case "W trakcie":
                        e.CellStyle.BackColor = ColorTranslator.FromHtml("#f39c12");
                        e.CellStyle.ForeColor = Color.White;
                        e.CellStyle.Font = new Font(dgvReklamacje.Font, FontStyle.Bold);
                        break;
                    case "Zaakceptowana":
                        e.CellStyle.BackColor = ColorTranslator.FromHtml("#27ae60");
                        e.CellStyle.ForeColor = Color.White;
                        e.CellStyle.Font = new Font(dgvReklamacje.Font, FontStyle.Bold);
                        break;
                    case "Odrzucona":
                        e.CellStyle.BackColor = ColorTranslator.FromHtml("#e74c3c");
                        e.CellStyle.ForeColor = Color.White;
                        e.CellStyle.Font = new Font(dgvReklamacje.Font, FontStyle.Bold);
                        break;
                    case "Zamknięta":
                        e.CellStyle.BackColor = ColorTranslator.FromHtml("#95a5a6");
                        e.CellStyle.ForeColor = Color.White;
                        e.CellStyle.Font = new Font(dgvReklamacje.Font, FontStyle.Bold);
                        break;
                }
            }
        }

        private void OtworzSzczegoly()
        {
            if (dgvReklamacje.SelectedRows.Count > 0)
            {
                int idReklamacji = Convert.ToInt32(dgvReklamacje.SelectedRows[0].Cells["Id"].Value);

                using (FormSzczegolyReklamacji formSzczegoly = new FormSzczegolyReklamacji(connectionString, idReklamacji, userId))
                {
                    if (formSzczegoly.ShowDialog() == DialogResult.OK)
                    {
                        WczytajReklamacje();
                    }
                }
            }
        }

        private void BtnZmienStatus_Click(object sender, EventArgs e)
        {
            if (dgvReklamacje.SelectedRows.Count == 0) return;

            int idReklamacji = Convert.ToInt32(dgvReklamacje.SelectedRows[0].Cells["Id"].Value);
            string obecnyStatus = dgvReklamacje.SelectedRows[0].Cells["Status"].Value?.ToString();

            using (Form formStatus = new Form())
            {
                formStatus.Text = "Zmiana statusu reklamacji";
                formStatus.Size = new Size(400, 200);
                formStatus.StartPosition = FormStartPosition.CenterParent;
                formStatus.FormBorderStyle = FormBorderStyle.FixedDialog;
                formStatus.MaximizeBox = false;
                formStatus.MinimizeBox = false;

                Label lbl = new Label
                {
                    Text = "Wybierz nowy status:",
                    Location = new Point(20, 20),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold)
                };
                formStatus.Controls.Add(lbl);

                ComboBox cmbStatus = new ComboBox
                {
                    Location = new Point(20, 50),
                    Size = new Size(340, 25),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new Font("Segoe UI", 10F)
                };
                cmbStatus.Items.AddRange(new object[] { "Nowa", "W trakcie", "Zaakceptowana", "Odrzucona", "Zamknięta" });
                cmbStatus.SelectedItem = obecnyStatus;
                formStatus.Controls.Add(cmbStatus);

                Button btnOK = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new Point(180, 100),
                    Size = new Size(90, 35),
                    BackColor = ColorTranslator.FromHtml("#27ae60"),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold)
                };
                btnOK.FlatAppearance.BorderSize = 0;
                formStatus.Controls.Add(btnOK);

                Button btnCancel = new Button
                {
                    Text = "Anuluj",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(280, 100),
                    Size = new Size(90, 35),
                    BackColor = ColorTranslator.FromHtml("#95a5a6"),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold)
                };
                btnCancel.FlatAppearance.BorderSize = 0;
                formStatus.Controls.Add(btnCancel);

                formStatus.AcceptButton = btnOK;
                formStatus.CancelButton = btnCancel;

                if (formStatus.ShowDialog() == DialogResult.OK)
                {
                    string nowyStatus = cmbStatus.SelectedItem?.ToString();

                    if (nowyStatus != null && nowyStatus != obecnyStatus)
                    {
                        try
                        {
                            using (SqlConnection conn = new SqlConnection(connectionString))
                            {
                                conn.Open();
                                string query = @"
                                    UPDATE [dbo].[Reklamacje]
                                    SET Status = @Status,
                                        OsobaRozpatrujaca = @Osoba,
                                        DataModyfikacji = GETDATE()
                                    WHERE Id = @Id";

                                using (SqlCommand cmd = new SqlCommand(query, conn))
                                {
                                    cmd.Parameters.AddWithValue("@Status", nowyStatus);
                                    cmd.Parameters.AddWithValue("@Osoba", userId);
                                    cmd.Parameters.AddWithValue("@Id", idReklamacji);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            MessageBox.Show($"Status reklamacji #{idReklamacji} został zmieniony na: {nowyStatus}",
                                "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            WczytajReklamacje();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Błąd podczas zmiany statusu:\n{ex.Message}",
                                "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
        }

        private void KonfigurujKolumny()
        {
            try
            {
                if (dgvReklamacje == null || dgvReklamacje.Columns == null || dgvReklamacje.Columns.Count == 0)
                    return;

                // Pomocnicza funkcja do bezpiecznej konfiguracji kolumny
                void KonfigurujKolumne(string nazwa, string naglowek, int szerokosc, string format = null)
                {
                    DataGridViewColumn col = null;
                    try
                    {
                        col = dgvReklamacje.Columns[nazwa];
                    }
                    catch { }

                    if (col != null)
                    {
                        col.HeaderText = naglowek;
                        col.Width = szerokosc;
                        if (!string.IsNullOrEmpty(format))
                        {
                            col.DefaultCellStyle.Format = format;
                        }
                    }
                }

                KonfigurujKolumne("Id", "ID", 60);
                KonfigurujKolumne("DataZgloszenia", "Data zgłoszenia", 150, "yyyy-MM-dd HH:mm");
                KonfigurujKolumne("NumerDokumentu", "Nr faktury", 120);
                KonfigurujKolumne("NazwaKontrahenta", "Kontrahent", 250);
                KonfigurujKolumne("Opis", "Opis", 400);
                KonfigurujKolumne("SumaKg", "Suma kg", 80, "N2");
                KonfigurujKolumne("Status", "Status", 120);
                KonfigurujKolumne("UserID", "Zgłaszający", 100);
                KonfigurujKolumne("OsobaRozpatrujaca", "Rozpatruje", 100);
            }
            catch (Exception ex)
            {
                // Ignoruj błędy konfiguracji kolumn - to nie jest krytyczne
                System.Diagnostics.Debug.WriteLine($"Błąd konfiguracji kolumn: {ex.Message}");
            }
        }

        private void BtnStatystyki_Click(object sender, EventArgs e)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT 
                            Status,
                            COUNT(*) AS Liczba,
                            SUM(SumaKg) AS SumaKg
                        FROM [dbo].[Reklamacje]
                        WHERE DataZgloszenia >= @DataOd
                        GROUP BY Status
                        ORDER BY 
                            CASE Status
                                WHEN 'Nowa' THEN 1
                                WHEN 'W trakcie' THEN 2
                                WHEN 'Zaakceptowana' THEN 3
                                WHEN 'Odrzucona' THEN 4
                                WHEN 'Zamknięta' THEN 5
                            END";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", DateTime.Now.AddMonths(-1));

                        DataTable dt = new DataTable();
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }

                        string statystyki = "📊 STATYSTYKI REKLAMACJI (ostatni miesiąc)\n\n";

                        foreach (DataRow row in dt.Rows)
                        {
                            statystyki += $"{row["Status"],-15}: {row["Liczba"],3} szt | {Convert.ToDecimal(row["SumaKg"]),10:N2} kg\n";
                        }

                        MessageBox.Show(statystyki, "Statystyki", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas pobierania statystyk:\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}