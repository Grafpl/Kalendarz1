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
            Text = $"Szczegóły reklamacji #{idReklamacji}";
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ColorTranslator.FromHtml("#f8f9fa");

            // Panel nagłówka
            Panel panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = ColorTranslator.FromHtml("#1e8449")
            };
            Panel redStripe = new Panel
            {
                Dock = DockStyle.Top,
                Height = 4,
                BackColor = ColorTranslator.FromHtml("#c0392b")
            };
            Label lblHeader = new Label
            {
                Text = $"REKLAMACJA #{idReklamacji}",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            panelHeader.Controls.Add(lblHeader);

            // Główny panel z zakładkami
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                Padding = new Point(12, 6)
            };

            // Zakładka: Podstawowe informacje
            TabPage tabInfo = new TabPage("Informacje");
            tabInfo.BackColor = Color.White;
            tabInfo.Padding = new Padding(15);

            rtbInfo = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 10F),
                BackColor = ColorTranslator.FromHtml("#f8fff8"),
                BorderStyle = BorderStyle.None
            };
            tabInfo.Controls.Add(rtbInfo);

            // Zakładka: Towary
            TabPage tabTowary = new TabPage("Towary");
            tabTowary.BackColor = Color.White;

            dgvTowary = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                GridColor = ColorTranslator.FromHtml("#d5f5e3")
            };
            dgvTowary.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#27ae60");
            dgvTowary.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvTowary.EnableHeadersVisualStyles = false;
            tabTowary.Controls.Add(dgvTowary);

            // Zakładka: Partie
            TabPage tabPartie = new TabPage("Partie");
            tabPartie.BackColor = Color.White;

            lbPartie = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                BackColor = ColorTranslator.FromHtml("#f8fff8"),
                BorderStyle = BorderStyle.None,
                ItemHeight = 28
            };
            tabPartie.Controls.Add(lbPartie);

            // Zakładka: Zdjęcia
            TabPage tabZdjecia = new TabPage("Zdjęcia");
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
                Text = "Powiększ zdjęcie",
                Dock = DockStyle.Bottom,
                Height = 38,
                BackColor = ColorTranslator.FromHtml("#27ae60"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnPowieksz.FlatAppearance.BorderSize = 0;
            btnPowieksz.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#1e8449");
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
                Height = 65,
                BackColor = ColorTranslator.FromHtml("#d5f5e3"),
                Padding = new Padding(15)
            };
            panelButtons.Paint += (s, e) =>
            {
                using (var pen = new Pen(ColorTranslator.FromHtml("#27ae60"), 2))
                {
                    e.Graphics.DrawLine(pen, 0, 0, panelButtons.Width, 0);
                }
            };

            Button btnZmienStatus = new Button
            {
                Text = "Zmień status",
                Size = new Size(130, 38),
                Location = new Point(15, 12),
                BackColor = ColorTranslator.FromHtml("#f39c12"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnZmienStatus.FlatAppearance.BorderSize = 0;
            btnZmienStatus.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#d68910");
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
                Text = "Otwórz folder",
                Size = new Size(120, 38),
                Location = new Point(155, 12),
                BackColor = ColorTranslator.FromHtml("#27ae60"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnOtworz.FlatAppearance.BorderSize = 0;
            btnOtworz.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#1e8449");
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

            // Przycisk eksportu do PDF
            Button btnExportPDF = new Button
            {
                Text = "Eksport PDF",
                Size = new Size(120, 38),
                Location = new Point(285, 12),
                BackColor = ColorTranslator.FromHtml("#c0392b"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnExportPDF.FlatAppearance.BorderSize = 0;
            btnExportPDF.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#922b21");
            btnExportPDF.Click += BtnExportPDF_Click;

            // Przycisk wysyłania email
            Button btnEmail = new Button
            {
                Text = "Wyślij email",
                Size = new Size(120, 38),
                Location = new Point(415, 12),
                BackColor = ColorTranslator.FromHtml("#e74c3c"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnEmail.FlatAppearance.BorderSize = 0;
            btnEmail.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#c0392b");
            btnEmail.Click += BtnEmail_Click;

            Button btnZamknij = new Button
            {
                Text = "Zamknij",
                Size = new Size(100, 38),
                Location = new Point(panelButtons.Width - 115, 12),
                BackColor = ColorTranslator.FromHtml("#7f8c8d"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnZamknij.FlatAppearance.BorderSize = 0;
            btnZamknij.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#5d6d7e");
            btnZamknij.Click += (s, e) => Close();

            panelButtons.Controls.Add(btnZmienStatus);
            panelButtons.Controls.Add(btnOtworz);
            panelButtons.Controls.Add(btnExportPDF);
            panelButtons.Controls.Add(btnEmail);
            panelButtons.Controls.Add(btnZamknij);

            // WAŻNE: Kolejność dodawania kontrolek z Dock jest kluczowa!
            // Najpierw Bottom, potem Top, na końcu Fill
            Controls.Add(panelButtons);    // Bottom - pierwszy
            Controls.Add(panelHeader);     // Top - drugi
            Controls.Add(redStripe);       // Top - trzeci (nad header)
            Controls.Add(tabControl);      // Fill - ostatni (wypełnia resztę)
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

        // ========================================
        // EKSPORT PDF I EMAIL
        // ========================================

        private void BtnExportPDF_Click(object sender, EventArgs e)
        {
            try
            {
                var generator = new ReklamacjePDFGenerator(connectionString);
                var sciezka = generator.GenerujRaportReklamacji(idReklamacji);

                var result = MessageBox.Show(
                    $"Raport został wygenerowany:\n{sciezka}\n\nCzy otworzyć raport w przeglądarce?",
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
                MessageBox.Show($"Błąd generowania raportu:\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnEmail_Click(object sender, EventArgs e)
        {
            // Formularz do wprowadzenia adresu email
            using (Form formEmail = new Form())
            {
                formEmail.Text = "Wyślij raport reklamacji";
                formEmail.Size = new Size(500, 250);
                formEmail.StartPosition = FormStartPosition.CenterParent;
                formEmail.FormBorderStyle = FormBorderStyle.FixedDialog;
                formEmail.MaximizeBox = false;
                formEmail.MinimizeBox = false;
                formEmail.BackColor = Color.White;

                Label lblInfo = new Label
                {
                    Text = $"Wyślij raport reklamacji #{idReklamacji} na email:",
                    Location = new Point(20, 20),
                    Size = new Size(440, 25),
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold)
                };
                formEmail.Controls.Add(lblInfo);

                Label lblEmail = new Label
                {
                    Text = "Adres email:",
                    Location = new Point(20, 60),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9F)
                };
                formEmail.Controls.Add(lblEmail);

                TextBox txtEmail = new TextBox
                {
                    Location = new Point(20, 85),
                    Size = new Size(440, 30),
                    Font = new Font("Segoe UI", 10F)
                };
                formEmail.Controls.Add(txtEmail);

                Label lblWarning = new Label
                {
                    Text = "Uwaga: Funkcja email wymaga konfiguracji serwera SMTP.",
                    Location = new Point(20, 125),
                    Size = new Size(440, 20),
                    ForeColor = Color.Gray,
                    Font = new Font("Segoe UI", 8F, FontStyle.Italic)
                };
                formEmail.Controls.Add(lblWarning);

                Button btnWyslij = new Button
                {
                    Text = "📧 Wyślij",
                    Location = new Point(250, 160),
                    Size = new Size(100, 35),
                    BackColor = ColorTranslator.FromHtml("#27ae60"),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold)
                };
                btnWyslij.FlatAppearance.BorderSize = 0;
                btnWyslij.Click += async (s, args) =>
                {
                    string email = txtEmail.Text.Trim();
                    if (string.IsNullOrEmpty(email) || !email.Contains("@"))
                    {
                        MessageBox.Show("Wprowadź poprawny adres email.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        btnWyslij.Enabled = false;
                        btnWyslij.Text = "Wysyłanie...";

                        // Najpierw generuj PDF
                        var generator = new ReklamacjePDFGenerator(connectionString);
                        var sciezka = generator.GenerujRaportReklamacji(idReklamacji);

                        // Wyślij email
                        var emailService = new ReklamacjeEmailService();
                        var result = await emailService.WyslijRaportReklamacji(email, idReklamacji, "", sciezka);

                        if (result.Success)
                        {
                            MessageBox.Show($"Raport został wysłany na adres:\n{email}",
                                "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            formEmail.Close();
                        }
                        else
                        {
                            MessageBox.Show($"Nie udało się wysłać email:\n{result.Message}",
                                "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd wysyłania:\n{ex.Message}",
                            "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        btnWyslij.Enabled = true;
                        btnWyslij.Text = "📧 Wyślij";
                    }
                };
                formEmail.Controls.Add(btnWyslij);

                Button btnAnuluj = new Button
                {
                    Text = "Anuluj",
                    Location = new Point(360, 160),
                    Size = new Size(100, 35),
                    BackColor = ColorTranslator.FromHtml("#95a5a6"),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    DialogResult = DialogResult.Cancel
                };
                btnAnuluj.FlatAppearance.BorderSize = 0;
                formEmail.Controls.Add(btnAnuluj);

                formEmail.CancelButton = btnAnuluj;
                formEmail.ShowDialog();
            }
        }
    }

    // ========================================
    // FORMULARZ ZMIANY STATUSU REKLAMACJI
    // Z PEŁNYM PRZEPŁYWEM PRACY (WORKFLOW)
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
        private Label lblWorkflow;
        private Label lblRozwiazanie;

        // Definicja dozwolonych przejść między statusami
        private static readonly Dictionary<string, List<string>> dozwolonePrzejscia = new Dictionary<string, List<string>>
        {
            { "Nowa", new List<string> { "W trakcie", "Odrzucona" } },
            { "W trakcie", new List<string> { "Zaakceptowana", "Odrzucona", "Nowa" } },
            { "Zaakceptowana", new List<string> { "Zamknieta", "W trakcie" } },
            { "Odrzucona", new List<string> { "Zamknieta", "Nowa", "W trakcie" } },
            { "Zamknieta", new List<string>() } // Zamknięta reklamacja - brak przejść (tylko admin może)
        };

        public FormZmianaStatusu(string connString, int reklamacjaId, string currentStatus, string user)
        {
            connectionString = connString;
            idReklamacji = reklamacjaId;
            aktualnyStatus = currentStatus;
            userId = user;

            InitializeComponent();
            WczytajAktualnyStatus();
            WczytajDozwoloneStatusy();
        }

        private void InitializeComponent()
        {
            Text = $"✏ Zmiana statusu reklamacji #{idReklamacji}";
            Size = new Size(600, 550);
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
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 55)
            };

            lblWorkflow = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(20, 80),
                MaximumSize = new Size(540, 0)
            };

            Label lblNowyStatus = new Label
            {
                Text = "Nowy status:",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 115)
            };

            cmbStatus = new ComboBox
            {
                Location = new Point(20, 140),
                Width = 540,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F)
            };
            cmbStatus.SelectedIndexChanged += CmbStatus_SelectedIndexChanged;

            Label lblKomentarz = new Label
            {
                Text = "Komentarz (wymagany):",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 180)
            };

            txtKomentarz = new TextBox
            {
                Location = new Point(20, 205),
                Width = 540,
                Height = 80,
                Multiline = true,
                Font = new Font("Segoe UI", 10F),
                ScrollBars = ScrollBars.Vertical
            };

            lblRozwiazanie = new Label
            {
                Text = "Rozwiązanie / Uzasadnienie:",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 295),
                Visible = false
            };

            txtRozwiazanie = new TextBox
            {
                Location = new Point(20, 320),
                Width = 540,
                Height = 80,
                Multiline = true,
                Font = new Font("Segoe UI", 10F),
                ScrollBars = ScrollBars.Vertical,
                Visible = false
            };

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
            panelMain.Controls.Add(lblWorkflow);
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
                    var cmd = new SqlCommand("SELECT Status FROM [dbo].[Reklamacje] WHERE Id = @Id", conn);
                    cmd.Parameters.AddWithValue("@Id", idReklamacji);
                    conn.Open();

                    aktualnyStatus = cmd.ExecuteScalar()?.ToString() ?? "Nieznany";
                    lblAktualnyStatus.Text = $"Aktualny status: {aktualnyStatus}";
                    lblAktualnyStatus.ForeColor = GetStatusColor(aktualnyStatus);
                }
            }
            catch { }
        }

        private void WczytajDozwoloneStatusy()
        {
            cmbStatus.Items.Clear();

            bool isAdmin = userId == "11111";

            if (isAdmin)
            {
                // Admin może zmienić na dowolny status
                cmbStatus.Items.AddRange(new object[] { "Nowa", "W trakcie", "Zaakceptowana", "Odrzucona", "Zamknieta" });
                lblWorkflow.Text = "Administrator - wszystkie przejścia dozwolone";
                lblWorkflow.ForeColor = ColorTranslator.FromHtml("#27ae60");
            }
            else
            {
                // Zwykły użytkownik - tylko dozwolone przejścia
                if (dozwolonePrzejscia.ContainsKey(aktualnyStatus))
                {
                    var dozwolone = dozwolonePrzejscia[aktualnyStatus];
                    foreach (var status in dozwolone)
                    {
                        cmbStatus.Items.Add(status);
                    }

                    if (dozwolone.Count == 0)
                    {
                        lblWorkflow.Text = "⚠ Reklamacja jest zamknięta. Tylko administrator może zmienić status.";
                        lblWorkflow.ForeColor = ColorTranslator.FromHtml("#e74c3c");
                    }
                    else
                    {
                        lblWorkflow.Text = $"Dozwolone przejścia z \"{aktualnyStatus}\": {string.Join(", ", dozwolone)}";
                        lblWorkflow.ForeColor = Color.Gray;
                    }
                }
            }

            // Jeśli nie ma żadnych dozwolonych przejść
            if (cmbStatus.Items.Count == 0)
            {
                cmbStatus.Enabled = false;
            }
        }

        private Color GetStatusColor(string status)
        {
            switch (status)
            {
                case "Nowa": return ColorTranslator.FromHtml("#3498db");
                case "W trakcie": return ColorTranslator.FromHtml("#f39c12");
                case "Zaakceptowana": return ColorTranslator.FromHtml("#27ae60");
                case "Odrzucona": return ColorTranslator.FromHtml("#e74c3c");
                case "Zamknieta": return ColorTranslator.FromHtml("#7f8c8d");
                default: return Color.Black;
            }
        }

        private void CmbStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            string nowyStatus = cmbStatus.SelectedItem?.ToString();
            bool pokazRozwiazanie = nowyStatus == "Zaakceptowana" || nowyStatus == "Odrzucona" || nowyStatus == "Zamknieta";

            lblRozwiazanie.Visible = pokazRozwiazanie;
            txtRozwiazanie.Visible = pokazRozwiazanie;

            // Zmień etykietę w zależności od statusu
            if (nowyStatus == "Odrzucona")
                lblRozwiazanie.Text = "Uzasadnienie odrzucenia (wymagane):";
            else if (nowyStatus == "Zaakceptowana")
                lblRozwiazanie.Text = "Sposób rozwiązania (wymagane):";
            else if (nowyStatus == "Zamknieta")
                lblRozwiazanie.Text = "Podsumowanie zamknięcia:";
        }

        private void BtnZapisz_Click(object sender, EventArgs e)
        {
            if (cmbStatus.SelectedIndex < 0)
            {
                MessageBox.Show("Wybierz nowy status!", "Uwaga", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string nowyStatus = cmbStatus.SelectedItem.ToString();

            if (nowyStatus == aktualnyStatus)
            {
                MessageBox.Show("Wybrany status jest taki sam jak aktualny.", "Uwaga", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtKomentarz.Text))
            {
                MessageBox.Show("Komentarz jest wymagany przy każdej zmianie statusu!", "Uwaga", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtKomentarz.Focus();
                return;
            }

            // Wymagaj rozwiązania/uzasadnienia przy niektórych statusach
            if ((nowyStatus == "Odrzucona" || nowyStatus == "Zaakceptowana") && string.IsNullOrWhiteSpace(txtRozwiazanie.Text))
            {
                string wymagane = nowyStatus == "Odrzucona" ? "uzasadnienie odrzucenia" : "sposób rozwiązania";
                MessageBox.Show($"Przy zmianie na status \"{nowyStatus}\" wymagane jest {wymagane}!", "Uwaga", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRozwiazanie.Focus();
                return;
            }

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Aktualizuj główny rekord reklamacji
                            string updateQuery = @"
                                UPDATE [dbo].[Reklamacje]
                                SET Status = @NowyStatus,
                                    OsobaRozpatrujaca = @Osoba,
                                    DataModyfikacji = GETDATE(),
                                    Komentarz = ISNULL(Komentarz, '') + CHAR(13) + CHAR(10) + @NowyKomentarz";

                            // Dodaj rozwiązanie jeśli podano
                            if (!string.IsNullOrWhiteSpace(txtRozwiazanie.Text))
                            {
                                updateQuery += ", Rozwiazanie = @Rozwiazanie";
                            }

                            // Ustaw datę zamknięcia dla statusu Zamknieta
                            if (nowyStatus == "Zamknieta")
                            {
                                updateQuery += ", DataZamkniecia = GETDATE()";
                            }

                            updateQuery += " WHERE Id = @Id";

                            using (var cmd = new SqlCommand(updateQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@NowyStatus", nowyStatus);
                                cmd.Parameters.AddWithValue("@Osoba", userId);
                                cmd.Parameters.AddWithValue("@NowyKomentarz", $"[{DateTime.Now:yyyy-MM-dd HH:mm}] {userId}: {txtKomentarz.Text}");
                                cmd.Parameters.AddWithValue("@Id", idReklamacji);

                                if (!string.IsNullOrWhiteSpace(txtRozwiazanie.Text))
                                {
                                    cmd.Parameters.AddWithValue("@Rozwiazanie", txtRozwiazanie.Text);
                                }

                                cmd.ExecuteNonQuery();
                            }

                            // 2. Dodaj wpis do historii
                            // Najpierw sprawdź czy kolumna ReklamacjaId czy IdReklamacji
                            string kolumnaId = "ReklamacjaId";
                            try
                            {
                                using (var cmdCheck = new SqlCommand(
                                    "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ReklamacjeHistoria' AND COLUMN_NAME IN ('ReklamacjaId', 'IdReklamacji')", conn, transaction))
                                {
                                    var result = cmdCheck.ExecuteScalar();
                                    if (result != null)
                                        kolumnaId = result.ToString();
                                }
                            }
                            catch { }

                            string historiaQuery = $@"
                                INSERT INTO [dbo].[ReklamacjeHistoria] ({kolumnaId}, StatusPoprzedni, StatusNowy, DataZmiany, ZmienionePrzez, Komentarz)
                                VALUES (@ReklamacjaId, @StatusPoprzedni, @StatusNowy, GETDATE(), @ZmienionePrzez, @Komentarz)";

                            using (var cmd = new SqlCommand(historiaQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReklamacjaId", idReklamacji);
                                cmd.Parameters.AddWithValue("@StatusPoprzedni", aktualnyStatus);
                                cmd.Parameters.AddWithValue("@StatusNowy", nowyStatus);
                                cmd.Parameters.AddWithValue("@ZmienionePrzez", userId);
                                cmd.Parameters.AddWithValue("@Komentarz", txtKomentarz.Text + (string.IsNullOrWhiteSpace(txtRozwiazanie.Text) ? "" : $"\nRozwiązanie: {txtRozwiazanie.Text}"));
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                MessageBox.Show($"Status reklamacji #{idReklamacji} zmieniony:\n{aktualnyStatus} → {nowyStatus}", "Sukces",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zmiany statusu:\n{ex.Message}", "Błąd",
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