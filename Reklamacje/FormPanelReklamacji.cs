using Kalendarz1.Reklamacje;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Drawing;
using System.IO;
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
        private Button btnUsun;
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
            this.Text = "Panel Reklamacji - Zarządzanie";
            this.Size = new Size(1600, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ColorTranslator.FromHtml("#f8f9fa");

            // Panel nagłówka - gradient zielono-czerwony
            Panel panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = ColorTranslator.FromHtml("#1e8449"),
                Padding = new Padding(20)
            };

            // Pasek czerwony na górze
            Panel redStripe = new Panel
            {
                Dock = DockStyle.Top,
                Height = 6,
                BackColor = ColorTranslator.FromHtml("#c0392b")
            };
            this.Controls.Add(redStripe);

            Label lblTytul = new Label
            {
                Text = "PANEL REKLAMACJI",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(25, 18)
            };
            panelHeader.Controls.Add(lblTytul);

            lblLicznik = new Label
            {
                Text = "Reklamacji: 0",
                Font = new Font("Segoe UI", 11F),
                ForeColor = ColorTranslator.FromHtml("#d5f5e3"),
                AutoSize = true,
                Location = new Point(25, 55)
            };
            panelHeader.Controls.Add(lblLicznik);

            // Logo po prawej stronie nagłówka
            PictureBox pbLogo = new PictureBox
            {
                Size = new Size(180, 80),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(this.Width - 220, 10)
            };

            // Załaduj logo - szukaj w wielu lokalizacjach
            string logoPath = ZnajdzSciezkeLogo();
            if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
            {
                try { pbLogo.Image = Image.FromFile(logoPath); }
                catch { }
            }
            panelHeader.Controls.Add(pbLogo);

            this.Controls.Add(panelHeader);

            // Panel filtrów
            Panel panelFiltry = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = Color.White,
                Padding = new Padding(20, 15, 20, 15)
            };
            panelFiltry.Paint += (s, e) =>
            {
                // Dolna linia zielona
                using (var pen = new Pen(ColorTranslator.FromHtml("#27ae60"), 2))
                {
                    e.Graphics.DrawLine(pen, 0, panelFiltry.Height - 1, panelFiltry.Width, panelFiltry.Height - 1);
                }
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
            cmbFiltrStatus.Items.AddRange(new object[] { "Wszystkie", "Nowa", "W trakcie", "Zaakceptowana", "Odrzucona", "Zamknieta" });
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
                Text = "Odśwież",
                Location = new Point(800, 35),
                Size = new Size(110, 32),
                BackColor = ColorTranslator.FromHtml("#27ae60"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnOdswiez.FlatAppearance.BorderSize = 0;
            btnOdswiez.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#1e8449");
            btnOdswiez.Click += (s, e) => WczytajReklamacje();
            panelFiltry.Controls.Add(btnOdswiez);

            btnStatystyki = new Button
            {
                Text = "Statystyki",
                Location = new Point(920, 35),
                Size = new Size(110, 32),
                BackColor = ColorTranslator.FromHtml("#2ecc71"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnStatystyki.FlatAppearance.BorderSize = 0;
            btnStatystyki.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#27ae60");
            btnStatystyki.Click += BtnStatystyki_Click;
            panelFiltry.Controls.Add(btnStatystyki);

            // Przycisk eksportu zestawienia PDF
            Button btnExportZestawienie = new Button
            {
                Text = "Eksport PDF",
                Location = new Point(1040, 35),
                Size = new Size(110, 32),
                BackColor = ColorTranslator.FromHtml("#c0392b"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnExportZestawienie.FlatAppearance.BorderSize = 0;
            btnExportZestawienie.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#922b21");
            btnExportZestawienie.Click += BtnExportZestawienie_Click;
            panelFiltry.Controls.Add(btnExportZestawienie);

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

            dgvReklamacje.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#1e8449");
            dgvReklamacje.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvReklamacje.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgvReklamacje.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgvReklamacje.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);

            dgvReklamacje.EnableHeadersVisualStyles = false;
            dgvReklamacje.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#e8f8f5");
            dgvReklamacje.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#27ae60");
            dgvReklamacje.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvReklamacje.GridColor = ColorTranslator.FromHtml("#d5f5e3");
            dgvReklamacje.CellFormatting += DgvReklamacje_CellFormatting;
            dgvReklamacje.CellDoubleClick += (s, e) => OtworzSzczegoly();

            panelMain.Controls.Add(dgvReklamacje);
            this.Controls.Add(panelMain);

            // Panel dolny z przyciskami akcji
            Panel panelFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 75,
                BackColor = ColorTranslator.FromHtml("#d5f5e3"),
                Padding = new Padding(20, 15, 20, 15)
            };
            panelFooter.Paint += (s, e) =>
            {
                // Górna linia zielona
                using (var pen = new Pen(ColorTranslator.FromHtml("#27ae60"), 3))
                {
                    e.Graphics.DrawLine(pen, 0, 0, panelFooter.Width, 0);
                }
            };

            btnSzczegoly = new Button
            {
                Text = "Szczegóły",
                Size = new Size(140, 42),
                Location = new Point(20, 16),
                BackColor = ColorTranslator.FromHtml("#27ae60"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnSzczegoly.FlatAppearance.BorderSize = 0;
            btnSzczegoly.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#1e8449");
            btnSzczegoly.Click += (s, e) => OtworzSzczegoly();
            panelFooter.Controls.Add(btnSzczegoly);

            btnZmienStatus = new Button
            {
                Text = "Zmień status",
                Size = new Size(140, 42),
                Location = new Point(175, 16),
                BackColor = ColorTranslator.FromHtml("#2ecc71"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnZmienStatus.FlatAppearance.BorderSize = 0;
            btnZmienStatus.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#27ae60");
            btnZmienStatus.Click += BtnZmienStatus_Click;
            panelFooter.Controls.Add(btnZmienStatus);

            // Przycisk usuwania - tylko dla admina (11111)
            btnUsun = new Button
            {
                Text = "Usuń reklamację",
                Size = new Size(160, 42),
                Location = new Point(330, 16),
                BackColor = ColorTranslator.FromHtml("#c0392b"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false,
                Visible = (userId == "11111") // Tylko admin widzi ten przycisk
            };
            btnUsun.FlatAppearance.BorderSize = 0;
            btnUsun.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#922b21");
            btnUsun.Click += BtnUsun_Click;
            panelFooter.Controls.Add(btnUsun);

            dgvReklamacje.SelectionChanged += (s, e) =>
            {
                bool selected = dgvReklamacje.SelectedRows.Count > 0;
                btnSzczegoly.Enabled = selected;
                btnZmienStatus.Enabled = selected;
                btnUsun.Enabled = selected;
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
                        e.CellStyle.BackColor = ColorTranslator.FromHtml("#e74c3c"); // Czerwony - wymaga uwagi
                        e.CellStyle.ForeColor = Color.White;
                        e.CellStyle.Font = new Font(dgvReklamacje.Font, FontStyle.Bold);
                        break;
                    case "W trakcie":
                        e.CellStyle.BackColor = ColorTranslator.FromHtml("#f39c12"); // Pomarańczowy - w toku
                        e.CellStyle.ForeColor = Color.White;
                        e.CellStyle.Font = new Font(dgvReklamacje.Font, FontStyle.Bold);
                        break;
                    case "Zaakceptowana":
                        e.CellStyle.BackColor = ColorTranslator.FromHtml("#27ae60"); // Zielony - zaakceptowana
                        e.CellStyle.ForeColor = Color.White;
                        e.CellStyle.Font = new Font(dgvReklamacje.Font, FontStyle.Bold);
                        break;
                    case "Odrzucona":
                        e.CellStyle.BackColor = ColorTranslator.FromHtml("#c0392b"); // Ciemny czerwony - odrzucona
                        e.CellStyle.ForeColor = Color.White;
                        e.CellStyle.Font = new Font(dgvReklamacje.Font, FontStyle.Bold);
                        break;
                    case "Zamknieta":
                        e.CellStyle.BackColor = ColorTranslator.FromHtml("#1e8449"); // Ciemny zielony - zamknięta
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
                cmbStatus.Items.AddRange(new object[] { "Nowa", "W trakcie", "Zaakceptowana", "Odrzucona", "Zamknieta" });
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

                // Wyłącz AutoSize przed konfiguracją kolumn
                dgvReklamacje.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                // Pomocnicza funkcja do bezpiecznej konfiguracji kolumny
                void KonfigurujKolumne(string nazwa, string naglowek, int szerokosc, string format = null)
                {
                    try
                    {
                        if (!dgvReklamacje.Columns.Contains(nazwa))
                            return;

                        var col = dgvReklamacje.Columns[nazwa];
                        if (col == null)
                            return;

                        col.HeaderText = naglowek;

                        try
                        {
                            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                            col.Width = szerokosc;
                        }
                        catch { /* Ignoruj błędy ustawiania szerokości */ }

                        if (!string.IsNullOrEmpty(format))
                        {
                            col.DefaultCellStyle.Format = format;
                        }
                    }
                    catch { /* Ignoruj błędy pojedynczej kolumny */ }
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

                // Przywróć AutoSize dla ostatniej kolumny (wypełni resztę miejsca)
                if (dgvReklamacje.Columns.Contains("OsobaRozpatrujaca"))
                {
                    try
                    {
                        dgvReklamacje.Columns["OsobaRozpatrujaca"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                // Ignoruj błędy konfiguracji kolumn - to nie jest krytyczne
                System.Diagnostics.Debug.WriteLine($"Błąd konfiguracji kolumn: {ex.Message}");
            }
        }

        private void BtnUsun_Click(object sender, EventArgs e)
        {
            if (dgvReklamacje.SelectedRows.Count == 0) return;

            // Sprawdź czy użytkownik to admin
            if (userId != "11111")
            {
                MessageBox.Show("Tylko administrator może usuwać reklamacje.",
                    "Brak uprawnień", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int idReklamacji = Convert.ToInt32(dgvReklamacje.SelectedRows[0].Cells["Id"].Value);
            string numerDokumentu = dgvReklamacje.SelectedRows[0].Cells["NumerDokumentu"].Value?.ToString() ?? "";
            string kontrahent = dgvReklamacje.SelectedRows[0].Cells["NazwaKontrahenta"].Value?.ToString() ?? "";

            // Potwierdzenie usunięcia
            var result = MessageBox.Show(
                $"Czy na pewno chcesz TRWALE usunąć reklamację?\n\n" +
                $"ID: {idReklamacji}\n" +
                $"Nr dokumentu: {numerDokumentu}\n" +
                $"Kontrahent: {kontrahent}\n\n" +
                $"⚠ UWAGA: Operacja jest nieodwracalna!\n" +
                $"Zostaną usunięte wszystkie powiązane dane:\n" +
                $"- towary reklamacji\n" +
                $"- partie\n" +
                $"- zdjęcia\n" +
                $"- historia zmian",
                "Potwierdzenie usunięcia",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes) return;

            // Drugie potwierdzenie dla pewności
            var result2 = MessageBox.Show(
                "Czy NA PEWNO chcesz usunąć tę reklamację?\n\nTo jest ostateczne potwierdzenie.",
                "Ostateczne potwierdzenie",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Stop,
                MessageBoxDefaultButton.Button2);

            if (result2 != DialogResult.Yes) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Usuń zdjęcia reklamacji
                            using (SqlCommand cmd = new SqlCommand(
                                "DELETE FROM [dbo].[ReklamacjeZdjecia] WHERE ReklamacjaId = @Id", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Id", idReklamacji);
                                cmd.ExecuteNonQuery();
                            }

                            // Usuń partie reklamacji
                            using (SqlCommand cmd = new SqlCommand(
                                "DELETE FROM [dbo].[ReklamacjePartie] WHERE ReklamacjaId = @Id", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Id", idReklamacji);
                                cmd.ExecuteNonQuery();
                            }

                            // Usuń towary reklamacji
                            using (SqlCommand cmd = new SqlCommand(
                                "DELETE FROM [dbo].[ReklamacjeTowary] WHERE ReklamacjaId = @Id", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Id", idReklamacji);
                                cmd.ExecuteNonQuery();
                            }

                            // Usuń historię zmian
                            using (SqlCommand cmd = new SqlCommand(
                                "DELETE FROM [dbo].[ReklamacjeHistoria] WHERE ReklamacjaId = @Id", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Id", idReklamacji);
                                cmd.ExecuteNonQuery();
                            }

                            // Na końcu usuń główną reklamację
                            using (SqlCommand cmd = new SqlCommand(
                                "DELETE FROM [dbo].[Reklamacje] WHERE Id = @Id", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Id", idReklamacji);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();

                            MessageBox.Show(
                                $"Reklamacja #{idReklamacji} została trwale usunięta.",
                                "Sukces",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            WczytajReklamacje();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception($"Błąd podczas usuwania: {ex.Message}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas usuwania reklamacji:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
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
                                WHEN 'Zamknieta' THEN 5
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

        private void BtnExportZestawienie_Click(object sender, EventArgs e)
        {
            try
            {
                string status = cmbFiltrStatus.SelectedIndex > 0 ? cmbFiltrStatus.SelectedItem.ToString() : null;

                var generator = new ReklamacjePDFGenerator(connectionString);
                var sciezka = generator.GenerujZestawienie(dtpOd.Value, dtpDo.Value, status);

                var result = MessageBox.Show(
                    $"Zestawienie reklamacji zostało wygenerowane:\n{sciezka}\n\nCzy otworzyć raport w przeglądarce?",
                    "Sukces",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    generator.OtworzRaport(sciezka);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd generowania zestawienia:\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Szuka pliku logo w wielu możliwych lokalizacjach
        /// </summary>
        private string ZnajdzSciezkeLogo()
        {
            string nazwaPliku = "logo-2-green.png";

            // Lista możliwych ścieżek do sprawdzenia
            var sciezki = new[]
            {
                // Katalog roboczy aplikacji
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nazwaPliku),
                // Katalogi nadrzędne (z bin/Debug do głównego)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", nazwaPliku),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", nazwaPliku),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", nazwaPliku),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", nazwaPliku),
                // Typowa ścieżka projektu
                Path.Combine(Environment.CurrentDirectory, nazwaPliku),
                Path.Combine(Environment.CurrentDirectory, "..", nazwaPliku),
                Path.Combine(Environment.CurrentDirectory, "..", "..", nazwaPliku),
                // Ścieżka absolutna jako fallback
                "/home/user/Kalendarz1/logo-2-green.png",
                "C:\\Projects\\Kalendarz1\\logo-2-green.png",
                // Obok pliku wykonywalnego w różnych konfiguracjach
                Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", nazwaPliku),
                Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "..", "..", "..", nazwaPliku),
            };

            foreach (var sciezka in sciezki)
            {
                try
                {
                    string pelnasciezka = Path.GetFullPath(sciezka);
                    if (File.Exists(pelnasciezka))
                    {
                        return pelnasciezka;
                    }
                }
                catch { }
            }

            return null;
        }
    }
}