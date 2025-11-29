using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Kalendarz1.Reklamacje
{
    // ========================================
    // FORMULARZ SZCZEGÓŁÓW REKLAMACJI
    // ========================================
    public partial class FormSzczegolyReklamacji : Form
    {
        private string connectionString;
        private int idReklamacji;
        private string userId;

        private TabControl tabControl;
        private RichTextBox rtbInfo;
        private DataGridView dgvTowary;
        private ListBox lbPartie;
        private ListBox lbZdjecia;
        private PictureBox pbZdjecie;
        private DataGridView dgvHistoria;

        public FormSzczegolyReklamacji(string connString, int reklamacjaId, string user)
        {
            connectionString = connString;
            idReklamacji = reklamacjaId;
            userId = user;

            InitializeComponent();
            WczytajSzczegoly();
        }

        private void InitializeComponent()
        {
            Text = $"📄 Szczegóły reklamacji #{idReklamacji}";
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ColorTranslator.FromHtml("#f5f7fa");

            // Główny panel z zakładkami
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                Padding = new Point(10, 5)
            };

            // Zakładka: Podstawowe informacje
            TabPage tabInfo = new TabPage("📋 Informacje podstawowe");
            tabInfo.BackColor = Color.White;
            tabInfo.Padding = new Padding(15);

            rtbInfo = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 10F),
                BackColor = ColorTranslator.FromHtml("#fafbfc"),
                BorderStyle = BorderStyle.None
            };
            tabInfo.Controls.Add(rtbInfo);

            // Zakładka: Towary
            TabPage tabTowary = new TabPage("📦 Towary");
            tabTowary.BackColor = Color.White;

            dgvTowary = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            tabTowary.Controls.Add(dgvTowary);

            // Zakładka: Partie
            TabPage tabPartie = new TabPage("🔢 Partie");
            tabPartie.BackColor = Color.White;

            lbPartie = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                BackColor = ColorTranslator.FromHtml("#fafbfc"),
                BorderStyle = BorderStyle.None,
                ItemHeight = 25
            };
            tabPartie.Controls.Add(lbPartie);

            // Zakładka: Zdjęcia
            TabPage tabZdjecia = new TabPage("📷 Zdjęcia");
            tabZdjecia.BackColor = Color.White;

            SplitContainer splitZdjecia = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 300
            };

            lbZdjecia = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F)
            };
            lbZdjecia.SelectedIndexChanged += (s, e) =>
            {
                if (lbZdjecia.SelectedIndex >= 0)
                {
                    string sciezka = lbZdjecia.SelectedItem.ToString().Split('|')[1].Trim();
                    if (File.Exists(sciezka))
                    {
                        try
                        {
                            pbZdjecie.Image?.Dispose();
                            pbZdjecie.Image = Image.FromFile(sciezka);
                        }
                        catch { }
                    }
                }
            };

            pbZdjecie = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorTranslator.FromHtml("#ecf0f1")
            };

            splitZdjecia.Panel1.Controls.Add(lbZdjecia);
            splitZdjecia.Panel2.Controls.Add(pbZdjecie);
            tabZdjecia.Controls.Add(splitZdjecia);

            // Zakładka: Historia
            TabPage tabHistoria = new TabPage("📜 Historia zmian");
            tabHistoria.BackColor = Color.White;

            dgvHistoria = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            tabHistoria.Controls.Add(dgvHistoria);

            tabControl.TabPages.Add(tabInfo);
            tabControl.TabPages.Add(tabTowary);
            tabControl.TabPages.Add(tabPartie);
            tabControl.TabPages.Add(tabZdjecia);
            tabControl.TabPages.Add(tabHistoria);

            // Panel przycisków
            Panel panelButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = ColorTranslator.FromHtml("#ecf0f1"),
                Padding = new Padding(15)
            };

            Button btnZmienStatus = new Button
            {
                Text = "✏ Zmień status",
                Size = new Size(150, 35),
                Location = new Point(15, 12),
                BackColor = ColorTranslator.FromHtml("#f39c12"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnZmienStatus.FlatAppearance.BorderSize = 0;
            btnZmienStatus.Click += (s, e) =>
            {
                var formZmiana = new FormZmianaStatusu(connectionString, idReklamacji, "", userId);
                if (formZmiana.ShowDialog() == DialogResult.OK)
                {
                    WczytajSzczegoly();
                    DialogResult = DialogResult.OK;
                }
            };

            Button btnOtworz = new Button
            {
                Text = "📂 Otwórz folder",
                Size = new Size(150, 35),
                Location = new Point(175, 12),
                BackColor = ColorTranslator.FromHtml("#3498db"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnOtworz.FlatAppearance.BorderSize = 0;
            btnOtworz.Click += (s, e) =>
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "ReklamacjeZdjecia",
                    idReklamacji.ToString());

                if (Directory.Exists(folder))
                    System.Diagnostics.Process.Start("explorer.exe", folder);
                else
                    MessageBox.Show("Folder ze zdjęciami nie istnieje.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Button btnZamknij = new Button
            {
                Text = "✗ Zamknij",
                Size = new Size(120, 35),
                Location = new Point(panelButtons.Width - 135, 12),
                BackColor = ColorTranslator.FromHtml("#95a5a6"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnZamknij.FlatAppearance.BorderSize = 0;
            btnZamknij.Click += (s, e) => Close();

            panelButtons.Controls.Add(btnZmienStatus);
            panelButtons.Controls.Add(btnOtworz);
            panelButtons.Controls.Add(btnZamknij);

            Controls.Add(tabControl);
            Controls.Add(panelButtons);
        }

        private void WczytajSzczegoly()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand("sp_PobierzSzczegolyReklamacji", conn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);

                    using (var reader = cmd.ExecuteReader())
                    {
                        // Podstawowe info
                        if (reader.Read())
                        {
                            rtbInfo.Clear();
                            rtbInfo.AppendText($"ID REKLAMACJI: #{reader["Id"]}\n");
                            rtbInfo.AppendText($"Data zgłoszenia: {reader["DataZgloszenia"]}\n");
                            rtbInfo.AppendText($"Zgłosił: {reader["UserID"]}\n\n");
                            rtbInfo.AppendText($"DOKUMENT\n");
                            rtbInfo.AppendText($"Nr dokumentu: {reader["NumerDokumentu"]}\n");
                            rtbInfo.AppendText($"ID dokumentu: {reader["IdDokumentu"]}\n\n");
                            rtbInfo.AppendText($"KONTRAHENT\n");
                            rtbInfo.AppendText($"Nazwa: {reader["NazwaKontrahenta"]}\n");
                            rtbInfo.AppendText($"ID: {reader["IdKontrahenta"]}\n\n");
                            rtbInfo.AppendText($"REKLAMACJA\n");
                            rtbInfo.AppendText($"Status: {reader["Status"]}\n");
                            rtbInfo.AppendText($"Suma kg: {reader["SumaKg"]} kg\n");
                            rtbInfo.AppendText($"Osoba rozpatrująca: {reader["OsobaRozpatrujaca"]}\n");
                            if (reader["DataZamkniecia"] != DBNull.Value)
                                rtbInfo.AppendText($"Data zamknięcia: {reader["DataZamkniecia"]}\n");
                            rtbInfo.AppendText($"\nOPIS PROBLEMU:\n{reader["Opis"]}\n");
                            if (reader["Komentarz"] != DBNull.Value)
                                rtbInfo.AppendText($"\nKOMENTARZ:\n{reader["Komentarz"]}\n");
                            if (reader["Rozwiazanie"] != DBNull.Value)
                                rtbInfo.AppendText($"\nROZWIĄZANIE:\n{reader["Rozwiazanie"]}\n");
                        }

                        // Towary
                        if (reader.NextResult())
                        {
                            DataTable dtTowary = new DataTable();
                            dtTowary.Load(reader);
                            dgvTowary.DataSource = dtTowary;
                        }

                        // Partie
                        if (reader.NextResult())
                        {
                            lbPartie.Items.Clear();
                            while (reader.Read())
                            {
                                lbPartie.Items.Add($"{reader["Partia"]} (dodano: {reader["DataDodania"]})");
                            }
                            if (lbPartie.Items.Count == 0)
                                lbPartie.Items.Add("(brak partii)");
                        }

                        // Zdjęcia
                        if (reader.NextResult())
                        {
                            lbZdjecia.Items.Clear();
                            try
                            {
                                while (reader.Read())
                                {
                                    string nazwaPliku = "";
                                    string sciezkaPliku = "";

                                    // Sprawdz czy kolumna istnieje
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        string colName = reader.GetName(i);
                                        if (colName == "NazwaPliku" && !reader.IsDBNull(i))
                                            nazwaPliku = reader.GetString(i);
                                        else if (colName == "SciezkaPliku" && !reader.IsDBNull(i))
                                            sciezkaPliku = reader.GetString(i);
                                    }

                                    if (!string.IsNullOrEmpty(nazwaPliku) || !string.IsNullOrEmpty(sciezkaPliku))
                                        lbZdjecia.Items.Add($"{nazwaPliku} | {sciezkaPliku}");
                                }
                            }
                            catch { /* Ignoruj bledy odczytu zdjec */ }

                            if (lbZdjecia.Items.Count == 0)
                                lbZdjecia.Items.Add("(brak zdjęć)");
                        }

                        // Historia
                        if (reader.NextResult())
                        {
                            DataTable dtHistoria = new DataTable();
                            dtHistoria.Load(reader);
                            dgvHistoria.DataSource = dtHistoria;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania szczegółów: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ========================================
    // FORMULARZ ZMIANY STATUSU REKLAMACJI
    // ========================================
    public partial class FormZmianaStatusu : Form
    {
        private string connectionString;
        private int idReklamacji;
        private string aktualnyStatus;
        private string userId;

        private ComboBox cmbStatus;
        private TextBox txtKomentarz;
        private TextBox txtRozwiazanie;
        private Label lblAktualnyStatus;

        public FormZmianaStatusu(string connString, int reklamacjaId, string currentStatus, string user)
        {
            connectionString = connString;
            idReklamacji = reklamacjaId;
            aktualnyStatus = currentStatus;
            userId = user;

            InitializeComponent();
            WczytajAktualnyStatus();
        }

        private void InitializeComponent()
        {
            Text = $"✏ Zmiana statusu reklamacji #{idReklamacji}";
            Size = new Size(600, 500);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Panel panelMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };

            Label lblTytul = new Label
            {
                Text = "Zmiana statusu reklamacji",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 20)
            };

            lblAktualnyStatus = new Label
            {
                Text = "Aktualny status: ",
                Font = new Font("Segoe UI", 10F),
                AutoSize = true,
                Location = new Point(20, 60)
            };

            Label lblNowyStatus = new Label
            {
                Text = "Nowy status:",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 100)
            };

            cmbStatus = new ComboBox
            {
                Location = new Point(20, 125),
                Width = 540,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F)
            };
            cmbStatus.Items.AddRange(new object[] { "Nowa", "W trakcie", "Zaakceptowana", "Odrzucona", "Zamknieta" });
            cmbStatus.SelectedIndexChanged += CmbStatus_SelectedIndexChanged;

            Label lblKomentarz = new Label
            {
                Text = "Komentarz:",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 165)
            };

            txtKomentarz = new TextBox
            {
                Location = new Point(20, 190),
                Width = 540,
                Height = 80,
                Multiline = true,
                Font = new Font("Segoe UI", 10F),
                ScrollBars = ScrollBars.Vertical
            };

            Label lblRozwiazanie = new Label
            {
                Text = "Rozwiązanie (opcjonalnie):",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 280),
                Visible = false
            };

            txtRozwiazanie = new TextBox
            {
                Location = new Point(20, 305),
                Width = 540,
                Height = 60,
                Multiline = true,
                Font = new Font("Segoe UI", 10F),
                ScrollBars = ScrollBars.Vertical,
                Visible = false
            };

            lblRozwiazanie.Tag = lblRozwiazanie;
            txtRozwiazanie.Tag = txtRozwiazanie;

            Panel panelButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = ColorTranslator.FromHtml("#ecf0f1")
            };

            Button btnZapisz = new Button
            {
                Text = "✓ Zapisz",
                Size = new Size(120, 40),
                Location = new Point(panelButtons.Width - 260, 10),
                BackColor = ColorTranslator.FromHtml("#27ae60"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.Click += BtnZapisz_Click;

            Button btnAnuluj = new Button
            {
                Text = "✗ Anuluj",
                Size = new Size(120, 40),
                Location = new Point(panelButtons.Width - 130, 10),
                BackColor = ColorTranslator.FromHtml("#95a5a6"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                DialogResult = DialogResult.Cancel
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;

            panelButtons.Controls.Add(btnZapisz);
            panelButtons.Controls.Add(btnAnuluj);

            panelMain.Controls.Add(lblTytul);
            panelMain.Controls.Add(lblAktualnyStatus);
            panelMain.Controls.Add(lblNowyStatus);
            panelMain.Controls.Add(cmbStatus);
            panelMain.Controls.Add(lblKomentarz);
            panelMain.Controls.Add(txtKomentarz);
            panelMain.Controls.Add(lblRozwiazanie);
            panelMain.Controls.Add(txtRozwiazanie);

            Controls.Add(panelMain);
            Controls.Add(panelButtons);
            AcceptButton = btnZapisz;
            CancelButton = btnAnuluj;
        }

        private void WczytajAktualnyStatus()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand("SELECT Status FROM Reklamacje WHERE Id = @Id", conn);
                    cmd.Parameters.AddWithValue("@Id", idReklamacji);
                    conn.Open();

                    aktualnyStatus = cmd.ExecuteScalar()?.ToString() ?? "Nieznany";
                    lblAktualnyStatus.Text = $"Aktualny status: {aktualnyStatus}";
                    lblAktualnyStatus.ForeColor = GetStatusColor(aktualnyStatus);
                }
            }
            catch { }
        }

        private Color GetStatusColor(string status)
        {
            switch (status)
            {
                case "Nowa": return ColorTranslator.FromHtml("#e74c3c");
                case "W trakcie": return ColorTranslator.FromHtml("#f39c12");
                case "Zaakceptowana": return ColorTranslator.FromHtml("#27ae60");
                case "Odrzucona": return ColorTranslator.FromHtml("#95a5a6");
                case "Zamknieta": return ColorTranslator.FromHtml("#34495e");
                default: return Color.Black;
            }
        }

        private void CmbStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            string nowyStatus = cmbStatus.SelectedItem?.ToString();
            bool pokazRozwiazanie = nowyStatus == "Zaakceptowana" || nowyStatus == "Odrzucona" || nowyStatus == "Zamknieta";

            foreach (Control ctrl in Controls[0].Controls)
            {
                if (ctrl.Tag != null && ctrl.Tag == ctrl)
                {
                    ctrl.Visible = pokazRozwiazanie;
                }
            }
        }

        private void BtnZapisz_Click(object sender, EventArgs e)
        {
            if (cmbStatus.SelectedIndex < 0)
            {
                MessageBox.Show("Wybierz nowy status!", "Uwaga", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtKomentarz.Text))
            {
                MessageBox.Show("Wprowadź komentarz!", "Uwaga", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand("sp_ZmienStatusReklamacji", conn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                    cmd.Parameters.AddWithValue("@NowyStatus", cmbStatus.SelectedItem.ToString());
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    cmd.Parameters.AddWithValue("@Komentarz", txtKomentarz.Text);

                    if (!string.IsNullOrWhiteSpace(txtRozwiazanie.Text))
                        cmd.Parameters.AddWithValue("@Rozwiazanie", txtRozwiazanie.Text);
                    else
                        cmd.Parameters.AddWithValue("@Rozwiazanie", DBNull.Value);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("Status reklamacji został zmieniony!", "Sukces",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zmiany statusu: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ========================================
    // FORMULARZ STATYSTYK REKLAMACJI
    // ========================================
    public partial class FormStatystykiReklamacji : Form
    {
        private string connectionString;

        public FormStatystykiReklamacji(string connString)
        {
            connectionString = connString;
            InitializeComponent();
            WczytajStatystyki();
        }

        private void InitializeComponent()
        {
            Text = "📊 Statystyki reklamacji";
            Size = new Size(900, 600);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ColorTranslator.FromHtml("#f5f7fa");

            RichTextBox rtbStats = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 10F),
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            Controls.Add(rtbStats);
        }

        private void WczytajStatystyki()
        {
            var rtb = (RichTextBox)Controls[0];
            rtb.Clear();
            rtb.AppendText("STATYSTYKI REKLAMACJI\n");
            rtb.AppendText("=" + new string('=', 80) + "\n\n");

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Statystyki według statusu
                    var cmd = new SqlCommand(@"
                        SELECT 
                            Status,
                            COUNT(*) AS Liczba,
                            SUM(SumaKg) AS SumaKg,
                            AVG(DniRozpatrywania) AS SredniCzas
                        FROM vw_ReklamacjePelneInfo
                        GROUP BY Status", conn);

                    using (var reader = cmd.ExecuteReader())
                    {
                        rtb.AppendText("WEDŁUG STATUSU:\n");
                        while (reader.Read())
                        {
                            rtb.AppendText($"  {reader["Status"],-20} Liczba: {reader["Liczba"],5}   Kg: {reader["SumaKg"],10:N2}   Średni czas: {reader["SredniCzas"],5:N1} dni\n");
                        }
                    }

                    rtb.AppendText("\n" + new string('-', 80) + "\n\n");

                    // Top kontrahenci
                    cmd = new SqlCommand(@"
                        SELECT TOP 10
                            NazwaKontrahenta,
                            COUNT(*) AS Liczba
                        FROM vw_ReklamacjePelneInfo
                        GROUP BY NazwaKontrahenta, IdKontrahenta
                        ORDER BY COUNT(*) DESC", conn);

                    using (var reader = cmd.ExecuteReader())
                    {
                        rtb.AppendText("TOP 10 KONTRAHENTÓW Z REKLAMACJAMI:\n");
                        int i = 1;
                        while (reader.Read())
                        {
                            rtb.AppendText($"  {i++}. {reader["NazwaKontrahenta"],-50} Reklamacji: {reader["Liczba"]}\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                rtb.AppendText($"BŁĄD: {ex.Message}");
            }
        }
    }
}