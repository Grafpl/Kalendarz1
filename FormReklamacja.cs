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
        private string connectionString;
        private int idDokumentu;
        private int idKontrahenta;
        private string numerDokumentu;
        private string nazwaKontrahenta;
        private string userId;

        private TextBox txtOpis;
        private Label lblKontrahent;
        private Label lblNumerFaktury;
        private Label lblSumaKg;
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

        public FormReklamacja(string connString, int dokId, int kontrId, string nrDok, string nazwaKontr, string user)
        {
            connectionString = connString;
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
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(1000, 700);
            this.BackColor = ColorTranslator.FromHtml("#ecf0f1");

            // Panel nagłówka
            Panel panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = ColorTranslator.FromHtml("#e74c3c"),
                Padding = new Padding(20, 10, 20, 10)
            };

            Label lblTytul = new Label
            {
                Text = "⚠ FORMULARZ ZGŁOSZENIA REKLAMACJI",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 35
            };

            lblKontrahent = new Label
            {
                Text = $"Kontrahent: {nazwaKontrahenta}",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 25
            };

            lblNumerFaktury = new Label
            {
                Text = $"Faktura: {numerDokumentu}",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.White,
                Dock = DockStyle.Bottom,
                Height = 25
            };

            panelHeader.Controls.Add(lblTytul);
            panelHeader.Controls.Add(lblKontrahent);
            panelHeader.Controls.Add(lblNumerFaktury);

            // Panel główny z zawartością
            Panel panelMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // SplitContainer - lewa/prawa strona
            SplitContainer splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 700,
                BackColor = ColorTranslator.FromHtml("#bdc3c7")
            };

            // === LEWA STRONA ===
            Panel panelLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };

            // 1. Sekcja: Towary do reklamacji
            GroupBox grpTowary = new GroupBox
            {
                Text = "📦 Towary do reklamacji (zaznacz z faktury)",
                Dock = DockStyle.Top,
                Height = 180,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Padding = new Padding(10)
            };

            checkedListBoxTowary = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                Font = new Font("Segoe UI", 9F)
            };
            checkedListBoxTowary.ItemCheck += CheckedListBoxTowary_ItemCheck;

            grpTowary.Controls.Add(checkedListBoxTowary);

            // 2. Sekcja: Partie
            GroupBox grpPartie = new GroupBox
            {
                Text = "🔢 Numery partii (ostatnie 2 tygodnie)",
                Dock = DockStyle.Top,
                Height = 220,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Padding = new Padding(10)
            };

            checkedListBoxPartie = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                Font = new Font("Segoe UI", 9F)
            };

            grpPartie.Controls.Add(checkedListBoxPartie);

            // 3. Suma kg
            Panel panelSumaKg = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = ColorTranslator.FromHtml("#d5f4e6"),
                Padding = new Padding(10, 5, 10, 5)
            };

            lblSumaKg = new Label
            {
                Text = "⚖ Łączna ilość zaznaczonych towarów: 0.00 kg",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#27ae60"),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelSumaKg.Controls.Add(lblSumaKg);

            // 4. Opis reklamacji
            GroupBox grpOpis = new GroupBox
            {
                Text = "📝 Opis reklamacji",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Padding = new Padding(10),
                Margin = new Padding(0, 5, 0, 0)
            };

            txtOpis = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F),
                ScrollBars = ScrollBars.Vertical
            };

            grpOpis.Controls.Add(txtOpis);

            panelLeft.Controls.Add(grpOpis);
            panelLeft.Controls.Add(panelSumaKg);
            panelLeft.Controls.Add(grpPartie);
            panelLeft.Controls.Add(grpTowary);

            // === PRAWA STRONA ===
            Panel panelRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };

            // Sekcja: Zdjęcia
            GroupBox grpZdjecia = new GroupBox
            {
                Text = "📷 Zdjęcia reklamacji",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Padding = new Padding(10)
            };

            Panel panelZdjeciaButtons = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(0, 5, 0, 5)
            };

            btnDodajZdjecia = new Button
            {
                Text = "➕ Dodaj zdjęcia",
                Location = new Point(0, 5),
                Size = new Size(120, 30),
                BackColor = ColorTranslator.FromHtml("#3498db"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnDodajZdjecia.FlatAppearance.BorderSize = 0;
            btnDodajZdjecia.Click += BtnDodajZdjecia_Click;

            btnUsunZdjecie = new Button
            {
                Text = "🗑 Usuń zaznaczone",
                Location = new Point(130, 5),
                Size = new Size(130, 30),
                BackColor = ColorTranslator.FromHtml("#e74c3c"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnUsunZdjecie.FlatAppearance.BorderSize = 0;
            btnUsunZdjecie.Click += BtnUsunZdjecie_Click;

            panelZdjeciaButtons.Controls.Add(btnDodajZdjecia);
            panelZdjeciaButtons.Controls.Add(btnUsunZdjecie);

            SplitContainer splitZdjecia = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 150
            };

            listBoxZdjecia = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F)
            };
            listBoxZdjecia.SelectedIndexChanged += ListBoxZdjecia_SelectedIndexChanged;

            pictureBoxPodglad = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            splitZdjecia.Panel1.Controls.Add(listBoxZdjecia);
            splitZdjecia.Panel2.Controls.Add(pictureBoxPodglad);

            grpZdjecia.Controls.Add(splitZdjecia);
            grpZdjecia.Controls.Add(panelZdjeciaButtons);

            panelRight.Controls.Add(grpZdjecia);

            splitMain.Panel1.Controls.Add(panelLeft);
            splitMain.Panel2.Controls.Add(panelRight);

            panelMain.Controls.Add(splitMain);

            // Panel przycisków na dole
            Panel panelButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = ColorTranslator.FromHtml("#ecf0f1"),
                Padding = new Padding(10)
            };

            btnZapiszReklamacje = new Button
            {
                Text = "✓ Zgłoś reklamację",
                Size = new Size(150, 40),
                Location = new Point(panelButtons.Width - 320, 10),
                BackColor = ColorTranslator.FromHtml("#27ae60"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnZapiszReklamacje.FlatAppearance.BorderSize = 0;
            btnZapiszReklamacje.Click += BtnZapiszReklamacje_Click;

            btnAnuluj = new Button
            {
                Text = "✗ Anuluj",
                Size = new Size(150, 40),
                Location = new Point(panelButtons.Width - 160, 10),
                BackColor = ColorTranslator.FromHtml("#95a5a6"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                DialogResult = DialogResult.Cancel
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;

            panelButtons.Controls.Add(btnZapiszReklamacje);
            panelButtons.Controls.Add(btnAnuluj);

            this.Controls.Add(panelMain);
            this.Controls.Add(panelButtons);
            this.Controls.Add(panelHeader);

            this.AcceptButton = btnZapiszReklamacje;
            this.CancelButton = btnAnuluj;
        }

        private void InicjalizujFormularz()
        {
            // Inicjalizacja dodatkowych elementów jeśli potrzeba
        }

        private void WczytajTowaryZFaktury()
        {
            string query = @"
                SELECT 
                    DP.idtw AS TowarID,
                    DP.kod AS KodTowaru,
                    DP.ilosc AS Ilosc,
                    DP.cena AS Cena
                FROM [HANDEL].[HM].[DP] DP
                WHERE DP.super = @IdDokumentu
                ORDER BY DP.lp";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@IdDokumentu", idDokumentu);

                    var adapter = new SqlDataAdapter(cmd);
                    dtTowary = new DataTable();
                    adapter.Fill(dtTowary);

                    checkedListBoxTowary.Items.Clear();
                    foreach (DataRow row in dtTowary.Rows)
                    {
                        string display = $"{row["KodTowaru"]} - {row["Ilosc"]:N2} kg @ {row["Cena"]:N2} zł/kg";
                        checkedListBoxTowary.Items.Add(display);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania towarów: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WczytajPartie()
        {
            // Partie z ostatnich 2 tygodni
            DateTime dataOd = DateTime.Now.AddDays(-14);

            // Connection string do bazy LibraNet na innym serwerze
            string connectionStringLibraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

            string query = @"
        SELECT 
            [guid],
            [Partia],
            [CustomerID],
            [CustomerName],
            [CreateData],
            [CreateGodzina],
            CAST([CustomerID] AS VARCHAR) + ' - ' + [Partia] AS DisplayText
        FROM [dbo].[PartiaDostawca]
        WHERE [CreateData] >= @DataOd
        ORDER BY [CreateData] ASC, [CreateGodzina] ASC";

            try
            {
                using (var conn = new SqlConnection(connectionStringLibraNet))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@DataOd", dataOd);

                    var adapter = new SqlDataAdapter(cmd);
                    dtPartie = new DataTable();
                    adapter.Fill(dtPartie);

                    checkedListBoxPartie.Items.Clear();

                    if (dtPartie.Rows.Count > 0)
                    {
                        foreach (DataRow row in dtPartie.Rows)
                        {
                            string display = $"{row["DisplayText"]} ({row["CreateData"]:dd.MM.yyyy} {row["CreateGodzina"]})";
                            checkedListBoxPartie.Items.Add(display);
                        }
                    }
                    else
                    {
                        checkedListBoxPartie.Items.Add("(brak partii z ostatnich 2 tygodni)");
                        checkedListBoxPartie.Enabled = false;
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                // Specjalna obsługa błędów SQL
                if (sqlEx.Message.Contains("Invalid object name") ||
                    sqlEx.Message.Contains("Cannot open database"))
                {
                    checkedListBoxPartie.Items.Clear();
                    checkedListBoxPartie.Items.Add("(brak dostępu do bazy partii - opcjonalne)");
                    checkedListBoxPartie.Enabled = false;

                    dtPartie = new DataTable();
                    dtPartie.Columns.Add("Partia", typeof(string));
                }
                else
                {
                    MessageBox.Show($"Uwaga: Nie można wczytać partii.\nReklamację można zgłosić bez partii.\n\nSzczegóły: {sqlEx.Message}",
                        "Ostrzeżenie", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    checkedListBoxPartie.Items.Clear();
                    checkedListBoxPartie.Items.Add("(partie niedostępne - opcjonalne)");
                    checkedListBoxPartie.Enabled = false;

                    dtPartie = new DataTable();
                    dtPartie.Columns.Add("Partia", typeof(string));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Uwaga: Nie można wczytać partii.\nReklamację można zgłosić bez partii.\n\nSzczegóły: {ex.Message}",
                    "Ostrzeżenie", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                checkedListBoxPartie.Items.Clear();
                checkedListBoxPartie.Items.Add("(partie niedostępne - opcjonalne)");
                checkedListBoxPartie.Enabled = false;

                dtPartie = new DataTable();
                dtPartie.Columns.Add("Partia", typeof(string));
            }
        }
        private void CheckedListBoxTowary_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // Opóźnienie aktualizacji, bo ItemCheck wykonuje się przed zmianą
            BeginInvoke(new Action(() => ObliczSumeKg()));
        }

        private void ObliczSumeKg()
        {
            decimal sumaKg = 0;

            for (int i = 0; i < checkedListBoxTowary.CheckedIndices.Count; i++)
            {
                int index = checkedListBoxTowary.CheckedIndices[i];
                if (index < dtTowary.Rows.Count)
                {
                    sumaKg += Convert.ToDecimal(dtTowary.Rows[index]["Ilosc"]);
                }
            }

            lblSumaKg.Text = $"⚖ Łączna ilość zaznaczonych towarów: {sumaKg:N2} kg";
        }

        private void BtnDodajZdjecia_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Pliki obrazów|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
                openFileDialog.Multiselect = true;
                openFileDialog.Title = "Wybierz zdjęcia reklamacji";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    foreach (string filePath in openFileDialog.FileNames)
                    {
                        if (!sciezkiZdjec.Contains(filePath))
                        {
                            sciezkiZdjec.Add(filePath);
                            listBoxZdjecia.Items.Add(Path.GetFileName(filePath));
                        }
                    }
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
            }
        }

        private void ListBoxZdjecia_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxZdjecia.SelectedIndex >= 0 && listBoxZdjecia.SelectedIndex < sciezkiZdjec.Count)
            {
                try
                {
                    string sciezka = sciezkiZdjec[listBoxZdjecia.SelectedIndex];
                    pictureBoxPodglad.Image = Image.FromFile(sciezka);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd wczytywania zdjęcia: {ex.Message}", "Błąd",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    pictureBoxPodglad.Image = null;
                }
            }
        }

        private void BtnZapiszReklamacje_Click(object sender, EventArgs e)
        {
            // Walidacja
            if (checkedListBoxTowary.CheckedItems.Count == 0)
            {
                MessageBox.Show("Zaznacz przynajmniej jeden towar do reklamacji!", "Uwaga",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtOpis.Text))
            {
                MessageBox.Show("Wprowadź opis reklamacji!", "Uwaga",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Zbierz dane reklamacji
                var zaznaczoneTowary = new List<int>();
                for (int i = 0; i < checkedListBoxTowary.CheckedIndices.Count; i++)
                {
                    int index = checkedListBoxTowary.CheckedIndices[i];
                    zaznaczoneTowary.Add(Convert.ToInt32(dtTowary.Rows[index]["TowarID"]));
                }

                var zaznaczonePartie = new List<string>();

                // POPRAWIONE - sprawdź czy partie są dostępne
                if (checkedListBoxPartie.Enabled && dtPartie.Rows.Count > 0)
                {
                    for (int i = 0; i < checkedListBoxPartie.CheckedIndices.Count; i++)
                    {
                        int index = checkedListBoxPartie.CheckedIndices[i];
                        if (index < dtPartie.Rows.Count)
                        {
                            zaznaczonePartie.Add(dtPartie.Rows[index]["Partia"].ToString());
                        }
                    }
                }

                decimal sumaKg = 0;
                for (int i = 0; i < checkedListBoxTowary.CheckedIndices.Count; i++)
                {
                    int index = checkedListBoxTowary.CheckedIndices[i];
                    sumaKg += Convert.ToDecimal(dtTowary.Rows[index]["Ilosc"]);
                }

                // Zapisz do bazy danych
                ZapiszReklamacjeDoBAzy(zaznaczoneTowary, zaznaczonePartie, sumaKg);

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisywania reklamacji: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ZapiszReklamacjeDoBAzy(List<int> towary, List<string> partie, decimal sumaKg)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Zapisz główny rekord reklamacji
                        string queryGlowna = @"
                            INSERT INTO [dbo].[Reklamacje]
                            ([DataZgloszenia], [UserID], [IdDokumentu], [NumerDokumentu], 
                             [IdKontrahenta], [NazwaKontrahenta], [Opis], [SumaKg], [Status])
                            VALUES
                            (@DataZgloszenia, @UserID, @IdDokumentu, @NumerDokumentu, 
                             @IdKontrahenta, @NazwaKontrahenta, @Opis, @SumaKg, 'Nowa');
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int idReklamacji;
                        using (var cmd = new SqlCommand(queryGlowna, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@DataZgloszenia", DateTime.Now);
                            cmd.Parameters.AddWithValue("@UserID", userId);
                            cmd.Parameters.AddWithValue("@IdDokumentu", idDokumentu);
                            cmd.Parameters.AddWithValue("@NumerDokumentu", numerDokumentu);
                            cmd.Parameters.AddWithValue("@IdKontrahenta", idKontrahenta);
                            cmd.Parameters.AddWithValue("@NazwaKontrahenta", nazwaKontrahenta);
                            cmd.Parameters.AddWithValue("@Opis", txtOpis.Text);
                            cmd.Parameters.AddWithValue("@SumaKg", sumaKg);

                            idReklamacji = (int)cmd.ExecuteScalar();
                        }

                        // 2. Zapisz towary Z NAZWAMI
                        for (int i = 0; i < checkedListBoxTowary.CheckedIndices.Count; i++)
                        {
                            int index = checkedListBoxTowary.CheckedIndices[i];
                            DataRow row = dtTowary.Rows[index];

                            string queryTowar = @"
                                INSERT INTO [dbo].[ReklamacjeTowary]
                                ([IdReklamacji], [IdTowaru], [KodTowaru], [Ilosc], [Cena])
                                VALUES (@IdReklamacji, @IdTowaru, @KodTowaru, @Ilosc, @Cena)";

                            using (var cmd = new SqlCommand(queryTowar, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                cmd.Parameters.AddWithValue("@IdTowaru", Convert.ToInt32(row["TowarID"]));
                                cmd.Parameters.AddWithValue("@KodTowaru", row["KodTowaru"].ToString());
                                cmd.Parameters.AddWithValue("@Ilosc", Convert.ToDecimal(row["Ilosc"]));
                                cmd.Parameters.AddWithValue("@Cena", Convert.ToDecimal(row["Cena"]));
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 3. Zapisz partie (TYLKO JEŚLI ISTNIEJĄ)
                        if (partie != null && partie.Count > 0)
                        {
                            foreach (string partia in partie)
                            {
                                string queryPartia = @"
                                    INSERT INTO [dbo].[ReklamacjePartie]
                                    ([IdReklamacji], [Partia])
                                    VALUES (@IdReklamacji, @Partia)";

                                using (var cmd = new SqlCommand(queryPartia, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                    cmd.Parameters.AddWithValue("@Partia", partia);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // 4. Zapisz zdjęcia
                        string folderReklamacji = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "ReklamacjeZdjecia",
                            idReklamacji.ToString());

                        if (sciezkiZdjec.Count > 0)
                        {
                            Directory.CreateDirectory(folderReklamacji);

                            foreach (string sciezkaZrodlowa in sciezkiZdjec)
                            {
                                string nazwaPliku = Path.GetFileName(sciezkaZrodlowa);
                                string sciezkaDocelowa = Path.Combine(folderReklamacji, nazwaPliku);

                                File.Copy(sciezkaZrodlowa, sciezkaDocelowa, true);

                                string queryZdjecie = @"
                                    INSERT INTO [dbo].[ReklamacjeZdjecia]
                                    ([IdReklamacji], [NazwaPliku], [SciezkaPliku])
                                    VALUES (@IdReklamacji, @NazwaPliku, @SciezkaPliku)";

                                using (var cmd = new SqlCommand(queryZdjecie, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                    cmd.Parameters.AddWithValue("@NazwaPliku", nazwaPliku);
                                    cmd.Parameters.AddWithValue("@SciezkaPliku", sciezkaDocelowa);
                                    cmd.ExecuteNonQuery();
                                }
                            }
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
        }
    }
}