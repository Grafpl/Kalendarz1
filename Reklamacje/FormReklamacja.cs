using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class FormReklamacja : Form
    {
        // Connection string do Handel (.112) - pobieranie towarów z faktury
        private string connectionStringHandel;
        // Connection string do LibraNet (.109) - zapis reklamacji
        private string connectionStringLibraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private int idDokumentu;
        private int idKontrahenta;
        private string numerDokumentu;
        private string nazwaKontrahenta;
        private string userId;

        private TextBox txtOpis;
        private Label lblKontrahent;
        private Label lblNumerFaktury;
        private Label lblSumaKg;
        private Label lblLicznikTowary;
        private Label lblLicznikPartie;
        private Label lblLicznikZdjecia;
        private CheckedListBox checkedListBoxTowary;
        private CheckedListBox checkedListBoxPartie;
        private ListBox listBoxZdjecia;
        private Button btnDodajZdjecia;
        private Button btnUsunZdjecie;
        private Button btnZapiszReklamacje;
        private Button btnAnuluj;
        private PictureBox pictureBoxPodglad;

        private List<string> sciezkiZdjec = new List<string>();
        private DataTable dtTowary;
        private DataTable dtPartie;

        public FormReklamacja(string connStringHandel, int dokId, int kontrId, string nrDok, string nazwaKontr, string user)
        {
            connectionStringHandel = connStringHandel;
            idDokumentu = dokId;
            idKontrahenta = kontrId;
            numerDokumentu = nrDok;
            nazwaKontrahenta = nazwaKontr;
            userId = user;

            InitializeComponent();
            InicjalizujFormularz();
            WczytajTowaryZFaktury();
            WczytajPartie();
        }

        private void InitializeComponent()
        {
            this.Text = "⚠ Zgłoszenie reklamacji";
            this.Size = new Size(1400, 850);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(1200, 750);
            this.BackColor = ColorTranslator.FromHtml("#f5f7fa");

            // Panel nagłówka
            Panel panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = ColorTranslator.FromHtml("#e74c3c"),
                Padding = new Padding(25, 15, 25, 15)
            };

            Label lblTytul = new Label
            {
                Text = "⚠ ZGŁOSZENIE REKLAMACJI",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(25, 20)
            };

            lblKontrahent = new Label
            {
                Text = $"Kontrahent: {nazwaKontrahenta}",
                Font = new Font("Segoe UI", 11F),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(25, 55)
            };

            lblNumerFaktury = new Label
            {
                Text = $"📄 Faktura: {numerDokumentu}",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(400, 55)
            };

            panelHeader.Controls.AddRange(new Control[] { lblTytul, lblKontrahent, lblNumerFaktury });
            this.Controls.Add(panelHeader);

            // Panel główny z zawartością
            Panel panelMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                AutoScroll = true
            };
            this.Controls.Add(panelMain);

            int yPos = 10;

            // ===== SEKCJA 1: TOWARY =====
            Panel panelTowary = StworzSekcje("📦 TOWARY DO REKLAMACJI", yPos, 300);
            panelMain.Controls.Add(panelTowary);

            checkedListBoxTowary = new CheckedListBox
            {
                Location = new Point(20, 45),
                Size = new Size(panelTowary.Width - 40, 210),
                Font = new Font("Consolas", 9.5F),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true
            };
            checkedListBoxTowary.ItemCheck += (s, e) => this.BeginInvoke(new Action(() => AktualizujSumeKg()));
            panelTowary.Controls.Add(checkedListBoxTowary);

            lblLicznikTowary = new Label
            {
                Text = "Zaznaczono: 0 towarów",
                Location = new Point(20, 265),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#7f8c8d"),
                AutoSize = true
            };
            panelTowary.Controls.Add(lblLicznikTowary);

            yPos += 320;

            // ===== SEKCJA 2: PARTIE =====
            Panel panelPartie = StworzSekcje("🏷 PARTIE DOSTAWCY (ostatnie 14 dni)", yPos, 300);
            panelMain.Controls.Add(panelPartie);

            checkedListBoxPartie = new CheckedListBox
            {
                Location = new Point(20, 45),
                Size = new Size(panelPartie.Width - 40, 210),
                Font = new Font("Consolas", 9F),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true
            };
            checkedListBoxPartie.ItemCheck += (s, e) => this.BeginInvoke(new Action(AktualizujLiczniki));
            panelPartie.Controls.Add(checkedListBoxPartie);

            lblLicznikPartie = new Label
            {
                Text = "Zaznaczono: 0 partii",
                Location = new Point(20, 265),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#7f8c8d"),
                AutoSize = true
            };
            panelPartie.Controls.Add(lblLicznikPartie);

            yPos += 320;

            // ===== SEKCJA 3: OPIS =====
            Panel panelOpis = StworzSekcje("📝 OPIS REKLAMACJI", yPos, 180);
            panelMain.Controls.Add(panelOpis);

            txtOpis = new TextBox
            {
                Location = new Point(20, 45),
                Size = new Size(panelOpis.Width - 40, 115),
                Multiline = true,
                Font = new Font("Segoe UI", 10F),
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = ScrollBars.Vertical
            };
            panelOpis.Controls.Add(txtOpis);

            yPos += 200;

            // ===== SEKCJA 4: ZDJĘCIA =====
            Panel panelZdjecia = StworzSekcje("📷 ZDJĘCIA (opcjonalnie)", yPos, 300);
            panelMain.Controls.Add(panelZdjecia);

            Panel panelZdjeciaLewy = new Panel
            {
                Location = new Point(20, 45),
                Size = new Size(600, 230),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            listBoxZdjecia = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.None
            };
            listBoxZdjecia.SelectedIndexChanged += ListBoxZdjecia_SelectedIndexChanged;
            panelZdjeciaLewy.Controls.Add(listBoxZdjecia);
            panelZdjecia.Controls.Add(panelZdjeciaLewy);

            Panel panelPrzyciski = new Panel
            {
                Location = new Point(630, 45),
                Size = new Size(140, 230),
                BackColor = Color.Transparent
            };

            btnDodajZdjecia = new Button
            {
                Text = "➕ Dodaj zdjęcia",
                Size = new Size(140, 40),
                Location = new Point(0, 0),
                BackColor = ColorTranslator.FromHtml("#27ae60"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDodajZdjecia.FlatAppearance.BorderSize = 0;
            btnDodajZdjecia.Click += BtnDodajZdjecia_Click;

            btnUsunZdjecie = new Button
            {
                Text = "🗑 Usuń zdjęcie",
                Size = new Size(140, 40),
                Location = new Point(0, 50),
                BackColor = ColorTranslator.FromHtml("#e74c3c"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnUsunZdjecie.FlatAppearance.BorderSize = 0;
            btnUsunZdjecie.Click += BtnUsunZdjecie_Click;

            panelPrzyciski.Controls.AddRange(new Control[] { btnDodajZdjecia, btnUsunZdjecie });
            panelZdjecia.Controls.Add(panelPrzyciski);

            pictureBoxPodglad = new PictureBox
            {
                Location = new Point(780, 45),
                Size = new Size(300, 230),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorTranslator.FromHtml("#ecf0f1")
            };
            panelZdjecia.Controls.Add(pictureBoxPodglad);

            lblLicznikZdjecia = new Label
            {
                Text = "Dodano: 0 zdjęć",
                Location = new Point(20, 280),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#7f8c8d"),
                AutoSize = true
            };
            panelZdjecia.Controls.Add(lblLicznikZdjecia);

            // ===== PANEL DOLNY - PODSUMOWANIE I PRZYCISKI =====
            Panel panelFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                BackColor = ColorTranslator.FromHtml("#34495e"),
                Padding = new Padding(20)
            };

            lblSumaKg = new Label
            {
                Text = "⚖ Suma kg: 0.00 kg",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 25)
            };
            panelFooter.Controls.Add(lblSumaKg);

            btnZapiszReklamacje = new Button
            {
                Text = "✓ ZGŁOŚ REKLAMACJĘ",
                Size = new Size(200, 45),
                Location = new Point(panelFooter.Width - 430, 15),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = ColorTranslator.FromHtml("#27ae60"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnZapiszReklamacje.FlatAppearance.BorderSize = 0;
            btnZapiszReklamacje.Click += BtnZapiszReklamacje_Click;

            btnAnuluj = new Button
            {
                Text = "✕ Anuluj",
                Size = new Size(200, 45),
                Location = new Point(panelFooter.Width - 220, 15),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = ColorTranslator.FromHtml("#95a5a6"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;
            btnAnuluj.Click += (s, e) => this.Close();

            panelFooter.Controls.AddRange(new Control[] { btnZapiszReklamacje, btnAnuluj });
            this.Controls.Add(panelFooter);
        }

        private Panel StworzSekcje(string tytul, int yPos, int height)
        {
            Panel panel = new Panel
            {
                Location = new Point(0, yPos),
                Size = new Size(1120, height),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblTytul = new Label
            {
                Text = tytul,
                Location = new Point(15, 12),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#2c3e50"),
                AutoSize = true
            };

            Panel linia = new Panel
            {
                Location = new Point(0, 38),
                Size = new Size(panel.Width, 2),
                BackColor = ColorTranslator.FromHtml("#bdc3c7")
            };

            panel.Controls.AddRange(new Control[] { lblTytul, linia });
            return panel;
        }

        private void InicjalizujFormularz()
        {
            AktualizujLiczniki();
            AktualizujSumeKg();
        }

        private void WczytajTowaryZFaktury()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringHandel))
                {
                    conn.Open();

                    // Pobieranie towarów z faktury - serwer .112 baza Handel
                    string query = @"
                        SELECT
                            DP.id,
                            DP.kod AS Symbol,
                            TW.kod AS Nazwa,
                            DP.ilosc AS Ilosc,
                            DP.ilosc AS Waga
                        FROM [HM].[DP] DP
                        LEFT JOIN [HM].[TW] TW ON DP.idtw = TW.ID
                        WHERE DP.super = @IdDokumentu
                        ORDER BY DP.lp";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@IdDokumentu", idDokumentu);
                        dtTowary = new DataTable();
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dtTowary);
                        }
                    }
                }

                checkedListBoxTowary.Items.Clear();

                if (dtTowary.Rows.Count == 0)
                {
                    checkedListBoxTowary.Items.Add("(Brak towarów w fakturze)");
                    return;
                }

                foreach (DataRow row in dtTowary.Rows)
                {
                    string symbol = row["Symbol"]?.ToString() ?? "";
                    string nazwa = row["Nazwa"]?.ToString() ?? symbol;
                    decimal ilosc = row["Ilosc"] != DBNull.Value ? Convert.ToDecimal(row["Ilosc"]) : 0;
                    decimal waga = row["Waga"] != DBNull.Value ? Convert.ToDecimal(row["Waga"]) : 0;

                    string display = $"{symbol,-15} | {nazwa,-40} | {ilosc,8:N2} szt | {waga,10:N2} kg";
                    checkedListBoxTowary.Items.Add(display);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania towarów:\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                checkedListBoxTowary.Items.Clear();
                checkedListBoxTowary.Items.Add("(Błąd wczytywania towarów)");
            }
        }

        private void WczytajPartie()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringLibraNet))
                {
                    conn.Open();

                    // Pobieranie partii z LibraNet (.109)
                    string query = @"
                        SELECT
                            [guid],
                            [Partia],
                            [CustomerID],
                            [CustomerName],
                            [CreateData],
                            [CreateGodzina]
                        FROM [dbo].[PartiaDostawca]
                        WHERE [CreateData] >= DATEADD(DAY, -14, GETDATE())
                        ORDER BY [CreateData] DESC, [CreateGodzina] DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        dtPartie = new DataTable();
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dtPartie);
                        }
                    }
                }

                checkedListBoxPartie.Items.Clear();

                if (dtPartie.Rows.Count == 0)
                {
                    checkedListBoxPartie.Items.Add("(Brak partii z ostatnich 14 dni)");
                    return;
                }

                foreach (DataRow row in dtPartie.Rows)
                {
                    string customerID = row["CustomerID"]?.ToString() ?? "";
                    string partia = row["Partia"]?.ToString() ?? "";
                    DateTime createData = row["CreateData"] != DBNull.Value ? Convert.ToDateTime(row["CreateData"]) : DateTime.MinValue;
                    string createGodzina = row["CreateGodzina"]?.ToString() ?? "";
                    string customerName = row["CustomerName"]?.ToString() ?? "";

                    string display = $"{customerID,-6} | {partia,-12} | {createData:yyyy-MM-dd} {createGodzina} | {customerName}";
                    checkedListBoxPartie.Items.Add(display);
                }
            }
            catch (Exception ex)
            {
                // POPRAWIONE: Nie pokazujemy błędu jeśli partie są niedostępne
                checkedListBoxPartie.Items.Clear();
                checkedListBoxPartie.Items.Add("(Partie niedostępne - zgłoszenie reklamacji możliwe bez partii)");
                dtPartie = new DataTable(); // Pusta tabela
            }
        }

        private void BtnDodajZdjecia_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Pliki obrazów|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
                ofd.Multiselect = true;
                ofd.Title = "Wybierz zdjęcia reklamacji";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    foreach (string sciezka in ofd.FileNames)
                    {
                        if (!sciezkiZdjec.Contains(sciezka))
                        {
                            sciezkiZdjec.Add(sciezka);
                            listBoxZdjecia.Items.Add(Path.GetFileName(sciezka));
                        }
                    }
                    AktualizujLiczniki();
                }
            }
        }

        private void BtnUsunZdjecie_Click(object sender, EventArgs e)
        {
            if (listBoxZdjecia.SelectedIndex >= 0)
            {
                int index = listBoxZdjecia.SelectedIndex;
                sciezkiZdjec.RemoveAt(index);
                listBoxZdjecia.Items.RemoveAt(index);
                pictureBoxPodglad.Image = null;
                AktualizujLiczniki();
            }
        }

        private void ListBoxZdjecia_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnUsunZdjecie.Enabled = listBoxZdjecia.SelectedIndex >= 0;

            if (listBoxZdjecia.SelectedIndex >= 0)
            {
                try
                {
                    string sciezka = sciezkiZdjec[listBoxZdjecia.SelectedIndex];
                    pictureBoxPodglad.Image = Image.FromFile(sciezka);
                }
                catch
                {
                    pictureBoxPodglad.Image = null;
                }
            }
            else
            {
                pictureBoxPodglad.Image = null;
            }
        }

        private void AktualizujLiczniki()
        {
            int liczbaTowarow = checkedListBoxTowary.CheckedItems.Count;
            int liczbaPartii = checkedListBoxPartie.CheckedItems.Count;
            int liczbaZdjec = sciezkiZdjec.Count;

            lblLicznikTowary.Text = $"Zaznaczono: {liczbaTowarow} towar(ów)";
            lblLicznikPartie.Text = $"Zaznaczono: {liczbaPartii} parti(i)";
            lblLicznikZdjecia.Text = $"Dodano: {liczbaZdjec} zdjęć";
        }

        private void AktualizujSumeKg()
        {
            decimal sumaKg = 0;

            for (int i = 0; i < checkedListBoxTowary.CheckedIndices.Count; i++)
            {
                int index = checkedListBoxTowary.CheckedIndices[i];
                if (index < dtTowary.Rows.Count)
                {
                    if (dtTowary.Rows[index]["Waga"] != DBNull.Value)
                    {
                        sumaKg += Convert.ToDecimal(dtTowary.Rows[index]["Waga"]);
                    }
                }
            }

            lblSumaKg.Text = $"⚖ Suma kg: {sumaKg:N2} kg";
            AktualizujLiczniki();
        }

        private void BtnZapiszReklamacje_Click(object sender, EventArgs e)
        {
            // Walidacja
            if (checkedListBoxTowary.CheckedItems.Count == 0)
            {
                MessageBox.Show("Musisz zaznaczyć przynajmniej jeden towar do reklamacji!",
                    "Uwaga", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtOpis.Text))
            {
                MessageBox.Show("Musisz wpisać opis reklamacji!",
                    "Uwaga", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtOpis.Focus();
                return;
            }

            try
            {
                int idReklamacji = 0;
                decimal sumaKg = 0;

                // Oblicz sumę kg
                for (int i = 0; i < checkedListBoxTowary.CheckedIndices.Count; i++)
                {
                    int index = checkedListBoxTowary.CheckedIndices[i];
                    if (index < dtTowary.Rows.Count && dtTowary.Rows[index]["Waga"] != DBNull.Value)
                    {
                        sumaKg += Convert.ToDecimal(dtTowary.Rows[index]["Waga"]);
                    }
                }

                using (SqlConnection conn = new SqlConnection(connectionStringLibraNet))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Zapisz główny rekord reklamacji w LibraNet (.109)
                            string queryReklamacja = @"
                                INSERT INTO [dbo].[Reklamacje]
                                (DataZgloszenia, UserID, IdDokumentu, NumerDokumentu, IdKontrahenta, NazwaKontrahenta, Opis, SumaKg, Status)
                                VALUES
                                (GETDATE(), @UserID, @IdDokumentu, @NumerDokumentu, @IdKontrahenta, @NazwaKontrahenta, @Opis, @SumaKg, 'Nowa');
                                SELECT SCOPE_IDENTITY();";

                            using (SqlCommand cmd = new SqlCommand(queryReklamacja, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@UserID", userId);
                                cmd.Parameters.AddWithValue("@IdDokumentu", idDokumentu);
                                cmd.Parameters.AddWithValue("@NumerDokumentu", numerDokumentu);
                                cmd.Parameters.AddWithValue("@IdKontrahenta", idKontrahenta);
                                cmd.Parameters.AddWithValue("@NazwaKontrahenta", nazwaKontrahenta);
                                cmd.Parameters.AddWithValue("@Opis", txtOpis.Text.Trim());
                                cmd.Parameters.AddWithValue("@SumaKg", sumaKg);

                                idReklamacji = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            // 2. Zapisz towary
                            string queryTowary = @"
                                INSERT INTO [dbo].[ReklamacjeTowary]
                                (IdReklamacji, IdTowaru, Symbol, Nazwa, Ilosc, Waga)
                                VALUES
                                (@IdReklamacji, @IdTowaru, @Symbol, @Nazwa, @Ilosc, @Waga)";

                            for (int i = 0; i < checkedListBoxTowary.CheckedIndices.Count; i++)
                            {
                                int index = checkedListBoxTowary.CheckedIndices[i];
                                if (index < dtTowary.Rows.Count)
                                {
                                    DataRow row = dtTowary.Rows[index];
                                    using (SqlCommand cmd = new SqlCommand(queryTowary, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                        cmd.Parameters.AddWithValue("@IdTowaru", row["id"]);
                                        cmd.Parameters.AddWithValue("@Symbol", row["Symbol"] ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@Nazwa", row["Nazwa"] ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@Ilosc", row["Ilosc"] ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@Waga", row["Waga"] ?? DBNull.Value);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            // 3. Zapisz partie (jeśli są)
                            if (checkedListBoxPartie.CheckedItems.Count > 0 && dtPartie != null && dtPartie.Rows.Count > 0)
                            {
                                string queryPartie = @"
                                    INSERT INTO [dbo].[ReklamacjePartie]
                                    (IdReklamacji, GuidPartii, NumerPartii, CustomerID, CustomerName)
                                    VALUES
                                    (@IdReklamacji, @GuidPartii, @NumerPartii, @CustomerID, @CustomerName)";

                                for (int i = 0; i < checkedListBoxPartie.CheckedIndices.Count; i++)
                                {
                                    int index = checkedListBoxPartie.CheckedIndices[i];
                                    if (index < dtPartie.Rows.Count)
                                    {
                                        DataRow row = dtPartie.Rows[index];
                                        using (SqlCommand cmd = new SqlCommand(queryPartie, conn, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                            cmd.Parameters.AddWithValue("@GuidPartii", row["guid"]);
                                            cmd.Parameters.AddWithValue("@NumerPartii", row["Partia"]);
                                            cmd.Parameters.AddWithValue("@CustomerID", row["CustomerID"]);
                                            cmd.Parameters.AddWithValue("@CustomerName", row["CustomerName"]);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }

                            // 4. Zapisz zdjęcia
                            if (sciezkiZdjec.Count > 0)
                            {
                                string folderReklamacji = Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                    "ReklamacjeZdjecia",
                                    idReklamacji.ToString());

                                Directory.CreateDirectory(folderReklamacji);

                                string queryZdjecia = @"
                                    INSERT INTO [dbo].[ReklamacjeZdjecia]
                                    (IdReklamacji, NazwaPliku, SciezkaPliku)
                                    VALUES
                                    (@IdReklamacji, @NazwaPliku, @SciezkaPliku)";

                                foreach (string sciezkaZrodlowa in sciezkiZdjec)
                                {
                                    string nazwaPliku = Path.GetFileName(sciezkaZrodlowa);
                                    string nowaSciezka = Path.Combine(folderReklamacji, nazwaPliku);

                                    File.Copy(sciezkaZrodlowa, nowaSciezka, true);

                                    using (SqlCommand cmd = new SqlCommand(queryZdjecia, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                        cmd.Parameters.AddWithValue("@NazwaPliku", nazwaPliku);
                                        cmd.Parameters.AddWithValue("@SciezkaPliku", nowaSciezka);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            transaction.Commit();

                            MessageBox.Show(
                                $"✓ Reklamacja została pomyślnie zgłoszona!\n\n" +
                                $"Numer reklamacji: #{idReklamacji}\n" +
                                $"Kontrahent: {nazwaKontrahenta}\n" +
                                $"Faktura: {numerDokumentu}\n" +
                                $"Zaznaczonych towarów: {checkedListBoxTowary.CheckedItems.Count}\n" +
                                $"Suma kg: {sumaKg:N2} kg\n" +
                                $"Partie: {checkedListBoxPartie.CheckedItems.Count}\n" +
                                $"Zdjęć: {sciezkiZdjec.Count}",
                                "Sukces",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania reklamacji:\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}