using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Kalendarz1.Reklamacje
{
    // ========================================
    // FORMULARZ SZCZEGÓŁÓW REKLAMACJI
    // ========================================

    // Klasa pomocnicza do przechowywania informacji o zdjęciu
    public class ZdjecieReklamacji
    {
        public int Id { get; set; }
        public string NazwaPliku { get; set; }
        public string SciezkaPliku { get; set; }
        public byte[] DaneZdjecia { get; set; }
    }

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

        // Lista zdjęć z danymi binarnymi
        private List<ZdjecieReklamacji> listaZdjec = new List<ZdjecieReklamacji>();

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
                if (lbZdjecia.SelectedIndex >= 0 && lbZdjecia.SelectedIndex < listaZdjec.Count)
                {
                    var zdjecie = listaZdjec[lbZdjecia.SelectedIndex];
                    try
                    {
                        pbZdjecie.Image?.Dispose();

                        // Najpierw spróbuj załadować z danych binarnych (BLOB)
                        if (zdjecie.DaneZdjecia != null && zdjecie.DaneZdjecia.Length > 0)
                        {
                            using (var ms = new MemoryStream(zdjecie.DaneZdjecia))
                            {
                                pbZdjecie.Image = Image.FromStream(ms);
                            }
                        }
                        // Jeśli brak BLOB, spróbuj z pliku
                        else if (!string.IsNullOrEmpty(zdjecie.SciezkaPliku) && File.Exists(zdjecie.SciezkaPliku))
                        {
                            pbZdjecie.Image = Image.FromFile(zdjecie.SciezkaPliku);
                        }
                        else
                        {
                            pbZdjecie.Image = null;
                        }
                    }
                    catch
                    {
                        pbZdjecie.Image = null;
                    }
                }
            };

            // Podwójne kliknięcie - pełnoekranowy podgląd
            lbZdjecia.DoubleClick += (s, e) =>
            {
                if (lbZdjecia.SelectedIndex >= 0 && lbZdjecia.SelectedIndex < listaZdjec.Count)
                {
                    var zdjecie = listaZdjec[lbZdjecia.SelectedIndex];
                    PokazPelnoekranowyPodglad(zdjecie);
                }
            };

            pbZdjecie = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorTranslator.FromHtml("#ecf0f1"),
                Cursor = Cursors.Hand
            };

            // Kliknięcie na zdjęcie otwiera pełnoekranowy podgląd
            pbZdjecie.Click += (s, e) =>
            {
                if (lbZdjecia.SelectedIndex >= 0 && lbZdjecia.SelectedIndex < listaZdjec.Count)
                {
                    var zdjecie = listaZdjec[lbZdjecia.SelectedIndex];
                    PokazPelnoekranowyPodglad(zdjecie);
                }
            };

            // Panel z przyciskiem powiększenia
            Panel panelPodglad = new Panel
            {
                Dock = DockStyle.Fill
            };

            Button btnPowieksz = new Button
            {
                Text = "🔍 Powiększ zdjęcie",
                Dock = DockStyle.Bottom,
                Height = 35,
                BackColor = ColorTranslator.FromHtml("#3498db"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnPowieksz.FlatAppearance.BorderSize = 0;
            btnPowieksz.Click += (s, e) =>
            {
                if (lbZdjecia.SelectedIndex >= 0 && lbZdjecia.SelectedIndex < listaZdjec.Count)
                {
                    var zdjecie = listaZdjec[lbZdjecia.SelectedIndex];
                    PokazPelnoekranowyPodglad(zdjecie);
                }
            };

            Label lblInfo = new Label
            {
                Text = "Kliknij na zdjęcie lub przycisk poniżej, aby powiększyć",
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8F, FontStyle.Italic)
            };

            panelPodglad.Controls.Add(pbZdjecie);
            panelPodglad.Controls.Add(btnPowieksz);
            panelPodglad.Controls.Add(lblInfo);

            splitZdjecia.Panel1.Controls.Add(lbZdjecia);
            splitZdjecia.Panel2.Controls.Add(panelPodglad);
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

                    // 1. Podstawowe informacje o reklamacji
                    WczytajPodstawoweInfo(conn);

                    // 2. Towary
                    WczytajTowary(conn);

                    // 3. Partie
                    WczytajPartie(conn);

                    // 4. Zdjęcia
                    WczytajZdjecia(conn);

                    // 5. Historia
                    WczytajHistorie(conn);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania szczegółów: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WczytajPodstawoweInfo(SqlConnection conn)
        {
            try
            {
                string query = @"SELECT * FROM [dbo].[Reklamacje] WHERE Id = @IdReklamacji";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            rtbInfo.Clear();
                            rtbInfo.AppendText($"ID REKLAMACJI: #{GetValue(reader, "Id")}\n");
                            rtbInfo.AppendText($"Data zgłoszenia: {GetValue(reader, "DataZgloszenia")}\n");
                            rtbInfo.AppendText($"Zgłosił: {GetValue(reader, "UserID")}\n\n");
                            rtbInfo.AppendText($"DOKUMENT\n");
                            rtbInfo.AppendText($"Nr dokumentu: {GetValue(reader, "NumerDokumentu")}\n");
                            rtbInfo.AppendText($"ID dokumentu: {GetValue(reader, "IdDokumentu")}\n\n");
                            rtbInfo.AppendText($"KONTRAHENT\n");
                            rtbInfo.AppendText($"Nazwa: {GetValue(reader, "NazwaKontrahenta")}\n");
                            rtbInfo.AppendText($"ID: {GetValue(reader, "IdKontrahenta")}\n\n");
                            rtbInfo.AppendText($"REKLAMACJA\n");
                            rtbInfo.AppendText($"Status: {GetValue(reader, "Status")}\n");
                            rtbInfo.AppendText($"Suma kg: {GetValue(reader, "SumaKg")} kg\n");
                            rtbInfo.AppendText($"Osoba rozpatrująca: {GetValue(reader, "OsobaRozpatrujaca")}\n");

                            var dataZamkniecia = GetValue(reader, "DataZamkniecia");
                            if (!string.IsNullOrEmpty(dataZamkniecia))
                                rtbInfo.AppendText($"Data zamknięcia: {dataZamkniecia}\n");

                            rtbInfo.AppendText($"\nOPIS PROBLEMU:\n{GetValue(reader, "Opis")}\n");

                            var komentarz = GetValue(reader, "Komentarz");
                            if (!string.IsNullOrEmpty(komentarz))
                                rtbInfo.AppendText($"\nKOMENTARZ:\n{komentarz}\n");

                            var rozwiazanie = GetValue(reader, "Rozwiazanie");
                            if (!string.IsNullOrEmpty(rozwiazanie))
                                rtbInfo.AppendText($"\nROZWIĄZANIE:\n{rozwiazanie}\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                rtbInfo.Text = $"Błąd wczytywania informacji: {ex.Message}";
            }
        }

        private void WczytajTowary(SqlConnection conn)
        {
            try
            {
                string query = @"SELECT * FROM [dbo].[ReklamacjeTowary] WHERE IdReklamacji = @IdReklamacji ORDER BY Id";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        dgvTowary.DataSource = dt;
                    }
                }
            }
            catch { }
        }

        private void WczytajPartie(SqlConnection conn)
        {
            try
            {
                lbPartie.Items.Clear();

                // Sprawdź jakie kolumny istnieją w tabeli
                string query = @"SELECT * FROM [dbo].[ReklamacjePartie] WHERE IdReklamacji = @IdReklamacji ORDER BY DataDodania DESC";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Szukaj kolumny z numerem partii (może być Partia lub NumerPartii)
                            string partia = "";
                            string dataDodania = "";

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string colName = reader.GetName(i);
                                if (!reader.IsDBNull(i))
                                {
                                    if (colName == "Partia" || colName == "NumerPartii")
                                        partia = reader[i].ToString();
                                    else if (colName == "DataDodania")
                                        dataDodania = reader[i].ToString();
                                    else if (colName == "CustomerName" && string.IsNullOrEmpty(partia))
                                        partia = reader[i].ToString(); // Użyj nazwy dostawcy jako fallback
                                }
                            }

                            if (!string.IsNullOrEmpty(partia))
                                lbPartie.Items.Add($"{partia} (dodano: {dataDodania})");
                        }
                    }
                }

                if (lbPartie.Items.Count == 0)
                    lbPartie.Items.Add("(brak partii)");
            }
            catch (Exception ex)
            {
                lbPartie.Items.Clear();
                lbPartie.Items.Add($"(błąd: {ex.Message})");
            }
        }

        private void WczytajZdjecia(SqlConnection conn)
        {
            try
            {
                lbZdjecia.Items.Clear();
                listaZdjec.Clear();

                // Najpierw sprawdź czy kolumna DaneZdjecia istnieje
                bool maKolumneDaneZdjecia = false;
                try
                {
                    using (var cmdCheck = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ReklamacjeZdjecia' AND COLUMN_NAME = 'DaneZdjecia'", conn))
                    {
                        maKolumneDaneZdjecia = Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0;
                    }
                }
                catch { }

                string query;
                if (maKolumneDaneZdjecia)
                {
                    query = @"SELECT Id, NazwaPliku, SciezkaPliku, DataDodania, DodanePrzez, DaneZdjecia
                              FROM [dbo].[ReklamacjeZdjecia] WHERE IdReklamacji = @IdReklamacji ORDER BY DataDodania";
                }
                else
                {
                    query = @"SELECT Id, NazwaPliku, SciezkaPliku, DataDodania, DodanePrzez
                              FROM [dbo].[ReklamacjeZdjecia] WHERE IdReklamacji = @IdReklamacji ORDER BY DataDodania";
                }

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var zdjecie = new ZdjecieReklamacji();

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string colName = reader.GetName(i);
                                if (!reader.IsDBNull(i))
                                {
                                    if (colName == "Id")
                                        zdjecie.Id = Convert.ToInt32(reader[i]);
                                    else if (colName == "NazwaPliku")
                                        zdjecie.NazwaPliku = reader[i].ToString();
                                    else if (colName == "SciezkaPliku")
                                        zdjecie.SciezkaPliku = reader[i].ToString();
                                    else if (colName == "DaneZdjecia")
                                        zdjecie.DaneZdjecia = (byte[])reader[i];
                                }
                            }

                            if (!string.IsNullOrEmpty(zdjecie.NazwaPliku) || zdjecie.DaneZdjecia != null)
                            {
                                listaZdjec.Add(zdjecie);
                                string info = zdjecie.NazwaPliku ?? "Zdjęcie";
                                if (zdjecie.DaneZdjecia != null && zdjecie.DaneZdjecia.Length > 0)
                                    info += " [DB]";
                                else if (!string.IsNullOrEmpty(zdjecie.SciezkaPliku) && File.Exists(zdjecie.SciezkaPliku))
                                    info += " [Plik]";
                                else
                                    info += " [Niedostępne]";
                                lbZdjecia.Items.Add(info);
                            }
                        }
                    }
                }

                if (lbZdjecia.Items.Count == 0)
                    lbZdjecia.Items.Add("(brak zdjęć)");
                else if (listaZdjec.Count > 0)
                    lbZdjecia.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                lbZdjecia.Items.Clear();
                lbZdjecia.Items.Add($"(błąd: {ex.Message})");
            }
        }

        private void WczytajHistorie(SqlConnection conn)
        {
            try
            {
                string query = @"SELECT * FROM [dbo].[ReklamacjeHistoria] WHERE IdReklamacji = @IdReklamacji ORDER BY DataZmiany DESC";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        dgvHistoria.DataSource = dt;
                    }
                }
            }
            catch { }
        }

        // Pomocnicza metoda do bezpiecznego pobierania wartości
        private string GetValue(SqlDataReader reader, string columnName)
        {
            try
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!reader.IsDBNull(i))
                            return reader[i].ToString();
                        return "";
                    }
                }
            }
            catch { }
            return "";
        }

        // Pełnoekranowy podgląd zdjęcia
        private void PokazPelnoekranowyPodglad(ZdjecieReklamacji zdjecie)
        {
            Image obrazek = null;

            try
            {
                // Załaduj zdjęcie z BLOB lub pliku
                if (zdjecie.DaneZdjecia != null && zdjecie.DaneZdjecia.Length > 0)
                {
                    using (var ms = new MemoryStream(zdjecie.DaneZdjecia))
                    {
                        obrazek = Image.FromStream(ms);
                    }
                }
                else if (!string.IsNullOrEmpty(zdjecie.SciezkaPliku) && File.Exists(zdjecie.SciezkaPliku))
                {
                    obrazek = Image.FromFile(zdjecie.SciezkaPliku);
                }

                if (obrazek == null)
                {
                    MessageBox.Show("Nie można załadować zdjęcia.", "Informacja",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania zdjęcia: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Utwórz okno podglądu
            Form formPodglad = new Form
            {
                Text = $"Podgląd: {zdjecie.NazwaPliku ?? "Zdjęcie"} - kliknij lub ESC aby zamknąć",
                WindowState = FormWindowState.Maximized,
                BackColor = Color.Black,
                FormBorderStyle = FormBorderStyle.None,
                KeyPreview = true
            };

            PictureBox pb = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = obrazek,
                Cursor = Cursors.Hand
            };

            pb.Click += (s, e) => formPodglad.Close();
            formPodglad.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                    formPodglad.Close();
            };

            formPodglad.Controls.Add(pb);
            formPodglad.FormClosed += (s, e) =>
            {
                pb.Image?.Dispose();
            };

            formPodglad.ShowDialog();
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