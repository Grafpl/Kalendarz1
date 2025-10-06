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
            this.Size = new Size(1400, 850);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(1200, 750);
            this.BackColor = ColorTranslator.FromHtml("#f5f7fa");

            // Panel nagłówka z gradientem
            Panel panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = ColorTranslator.FromHtml("#c0392b"),
                Padding = new Padding(25, 15, 25, 15)
            };

            Label lblTytul = new Label
            {
                Text = "⚠ FORMULARZ ZGŁOSZENIA REKLAMACJI",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Panel panelInfoHeader = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 5, 0, 0)
            };

            lblKontrahent = new Label
            {
                Text = $"👤 Kontrahent: {nazwaKontrahenta}",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#ecf0f1"),
                AutoSize = true,
                Location = new Point(0, 0)
            };

            lblNumerFaktury = new Label
            {
                Text = $"📄 Faktura: {numerDokumentu}",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#ecf0f1"),
                AutoSize = true,
                Location = new Point(0, 25)
            };

            panelInfoHeader.Controls.Add(lblKontrahent);
            panelInfoHeader.Controls.Add(lblNumerFaktury);
            panelHeader.Controls.Add(lblTytul);
            panelHeader.Controls.Add(panelInfoHeader);

            // Panel główny z zawartością
            Panel panelMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15),
                BackColor = ColorTranslator.FromHtml("#f5f7fa")
            };

            // SplitContainer - lewa/prawa strona
            SplitContainer splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 780,
                SplitterWidth = 8,
                BackColor = ColorTranslator.FromHtml("#cbd5e0")
            };

            // === LEWA STRONA ===
            Panel panelLeft = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                BackColor = ColorTranslator.FromHtml("#f5f7fa")
            };

            // 1. Sekcja: Towary do reklamacji
            GroupBox grpTowary = new GroupBox
            {
                Text = "",
                Dock = DockStyle.Top,
                Height = 200,
                Font = new Font("Segoe UI", 10F),
                Padding = new Padding(15),
                BackColor = Color.White,
                ForeColor = ColorTranslator.FromHtml("#2c3e50")
            };

            Panel panelTowaryHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                BackColor = Color.White
            };

            Label lblTowaryTytul = new Label
            {
                Text = "📦 Towary do reklamacji",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#2c3e50"),
                AutoSize = true,
                Location = new Point(0, 8)
            };

            lblLicznikTowary = new Label
            {
                Text = "Zaznaczono: 0",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#7f8c8d"),
                AutoSize = true,
                Location = new Point(200, 10)
            };

            panelTowaryHeader.Controls.Add(lblTowaryTytul);
            panelTowaryHeader.Controls.Add(lblLicznikTowary);

            checkedListBoxTowary = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                Font = new Font("Segoe UI", 9.5F),
                BackColor = ColorTranslator.FromHtml("#fafbfc"),
                BorderStyle = BorderStyle.None,
                ItemHeight = 22
            };
            checkedListBoxTowary.ItemCheck += CheckedListBoxTowary_ItemCheck;

            ToolTip tooltipTowary = new ToolTip();
            tooltipTowary.SetToolTip(checkedListBoxTowary, "Zaznacz towary, których dotyczy reklamacja");

            grpTowary.Controls.Add(checkedListBoxTowary);
            grpTowary.Controls.Add(panelTowaryHeader);

            // 2. Sekcja: Partie
            GroupBox grpPartie = new GroupBox
            {
                Text = "",
                Dock = DockStyle.Top,
                Height = 220,
                Font = new Font("Segoe UI", 10F),
                Padding = new Padding(15),
                BackColor = Color.White,
                ForeColor = ColorTranslator.FromHtml("#2c3e50"),
                Margin = new Padding(0, 10, 0, 0)
            };

            Panel panelPartieHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.White
            };

            Label lblPartieTytul = new Label
            {
                Text = "🔢 Numery partii",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#2c3e50"),
                AutoSize = true,
                Location = new Point(0, 8)
            };

            Label lblPartieInfo = new Label
            {
                Text = "Ostatnie 2 tygodnie (od najnowszych)",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = ColorTranslator.FromHtml("#95a5a6"),
                AutoSize = true,
                Location = new Point(0, 28)
            };

            lblLicznikPartie = new Label
            {
                Text = "Zaznaczono: 0",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#7f8c8d"),
                AutoSize = true,
                Location = new Point(200, 10)
            };

            panelPartieHeader.Controls.Add(lblPartieTytul);
            panelPartieHeader.Controls.Add(lblPartieInfo);
            panelPartieHeader.Controls.Add(lblLicznikPartie);

            checkedListBoxPartie = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                Font = new Font("Segoe UI", 9.5F),
                BackColor = ColorTranslator.FromHtml("#fafbfc"),
                BorderStyle = BorderStyle.None,
                ItemHeight = 22
            };
            checkedListBoxPartie.ItemCheck += CheckedListBoxPartie_ItemCheck;

            ToolTip tooltipPartie = new ToolTip();
            tooltipPartie.SetToolTip(checkedListBoxPartie, "Zaznacz partie produktów (opcjonalne)");

            grpPartie.Controls.Add(checkedListBoxPartie);
            grpPartie.Controls.Add(panelPartieHeader);

            // 3. Suma kg - bardziej widoczna
            Panel panelSumaKg = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = ColorTranslator.FromHtml("#d5f4e6"),
                Padding = new Padding(15, 10, 15, 10),
                Margin = new Padding(0, 10, 0, 0)
            };

            lblSumaKg = new Label
            {
                Text = "⚖ Łączna ilość zaznaczonych towarów: 0.00 kg",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#27ae60"),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelSumaKg.Controls.Add(lblSumaKg);

            // 4. Opis reklamacji
            GroupBox grpOpis = new GroupBox
            {
                Text = "",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                Padding = new Padding(15),
                BackColor = Color.White,
                ForeColor = ColorTranslator.FromHtml("#2c3e50"),
                Margin = new Padding(0, 10, 0, 0)
            };

            Panel panelOpisHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.White
            };

            Label lblOpisTytul = new Label
            {
                Text = "📝 Opis reklamacji",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#2c3e50"),
                AutoSize = true,
                Location = new Point(0, 8)
            };

            Label lblOpisInfo = new Label
            {
                Text = "Opisz dokładnie problem i jego charakter",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = ColorTranslator.FromHtml("#95a5a6"),
                AutoSize = true,
                Location = new Point(0, 28)
            };

            panelOpisHeader.Controls.Add(lblOpisTytul);
            panelOpisHeader.Controls.Add(lblOpisInfo);

            txtOpis = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                ScrollBars = ScrollBars.Vertical,
                BackColor = ColorTranslator.FromHtml("#fafbfc"),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(5)
            };

            ToolTip tooltipOpis = new ToolTip();
            tooltipOpis.SetToolTip(txtOpis, "Wprowadź szczegółowy opis reklamacji");

            grpOpis.Controls.Add(txtOpis);
            grpOpis.Controls.Add(panelOpisHeader);

            panelLeft.Controls.Add(grpOpis);
            panelLeft.Controls.Add(panelSumaKg);
            panelLeft.Controls.Add(grpPartie);
            panelLeft.Controls.Add(grpTowary);

            // === PRAWA STRONA ===
            Panel panelRight = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                BackColor = ColorTranslator.FromHtml("#f5f7fa")
            };

            // Sekcja: Zdjęcia
            GroupBox grpZdjecia = new GroupBox
            {
                Text = "",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                Padding = new Padding(15),
                BackColor = Color.White,
                ForeColor = ColorTranslator.FromHtml("#2c3e50")
            };

            Panel panelZdjeciaHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.White
            };

            Label lblZdjeciaTytul = new Label
            {
                Text = "📷 Zdjęcia reklamacji",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#2c3e50"),
                AutoSize = true,
                Location = new Point(0, 8)
            };

            Label lblZdjeciaInfo = new Label
            {
                Text = "Dodaj zdjęcia dokumentujące problem",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = ColorTranslator.FromHtml("#95a5a6"),
                AutoSize = true,
                Location = new Point(0, 28)
            };

            lblLicznikZdjecia = new Label
            {
                Text = "Zdjęć: 0",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#7f8c8d"),
                AutoSize = true,
                Location = new Point(230, 10)
            };

            panelZdjeciaHeader.Controls.Add(lblZdjeciaTytul);
            panelZdjeciaHeader.Controls.Add(lblZdjeciaInfo);
            panelZdjeciaHeader.Controls.Add(lblLicznikZdjecia);

            Panel panelZdjeciaButtons = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(0, 10, 0, 5),
                BackColor = Color.White
            };

            btnDodajZdjecia = new Button
            {
                Text = "➕ Dodaj zdjęcia",
                Location = new Point(0, 10),
                Size = new Size(140, 35),
                BackColor = ColorTranslator.FromHtml("#3498db"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnDodajZdjecia.FlatAppearance.BorderSize = 0;
            btnDodajZdjecia.MouseEnter += (s, e) => btnDodajZdjecia.BackColor = ColorTranslator.FromHtml("#2980b9");
            btnDodajZdjecia.MouseLeave += (s, e) => btnDodajZdjecia.BackColor = ColorTranslator.FromHtml("#3498db");
            btnDodajZdjecia.Click += BtnDodajZdjecia_Click;

            btnUsunZdjecie = new Button
            {
                Text = "🗑 Usuń zaznaczone",
                Location = new Point(150, 10),
                Size = new Size(150, 35),
                BackColor = ColorTranslator.FromHtml("#e74c3c"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnUsunZdjecie.FlatAppearance.BorderSize = 0;
            btnUsunZdjecie.MouseEnter += (s, e) => btnUsunZdjecie.BackColor = ColorTranslator.FromHtml("#c0392b");
            btnUsunZdjecie.MouseLeave += (s, e) => btnUsunZdjecie.BackColor = ColorTranslator.FromHtml("#e74c3c");
            btnUsunZdjecie.Click += BtnUsunZdjecie_Click;

            panelZdjeciaButtons.Controls.Add(btnDodajZdjecia);
            panelZdjeciaButtons.Controls.Add(btnUsunZdjecie);

            SplitContainer splitZdjecia = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 180,
                SplitterWidth = 6,
                BackColor = ColorTranslator.FromHtml("#cbd5e0")
            };

            Panel panelListaZdjec = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorTranslator.FromHtml("#fafbfc"),
                Padding = new Padding(5)
            };

            listBoxZdjecia = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F),
                BackColor = ColorTranslator.FromHtml("#fafbfc"),
                BorderStyle = BorderStyle.None,
                ItemHeight = 20
            };
            listBoxZdjecia.SelectedIndexChanged += ListBoxZdjecia_SelectedIndexChanged;

            panelListaZdjec.Controls.Add(listBoxZdjecia);

            Panel panelPodglad = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorTranslator.FromHtml("#ecf0f1"),
                Padding = new Padding(5)
            };

            pictureBoxPodglad = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorTranslator.FromHtml("#fafbfc"),
                BorderStyle = BorderStyle.None
            };

            panelPodglad.Controls.Add(pictureBoxPodglad);

            splitZdjecia.Panel1.Controls.Add(panelListaZdjec);
            splitZdjecia.Panel2.Controls.Add(panelPodglad);

            grpZdjecia.Controls.Add(splitZdjecia);
            grpZdjecia.Controls.Add(panelZdjeciaButtons);
            grpZdjecia.Controls.Add(panelZdjeciaHeader);

            panelRight.Controls.Add(grpZdjecia);

            splitMain.Panel1.Controls.Add(panelLeft);
            splitMain.Panel2.Controls.Add(panelRight);

            panelMain.Controls.Add(splitMain);

            // Panel przycisków na dole
            Panel panelButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 75,
                BackColor = ColorTranslator.FromHtml("#f5f7fa"),
                Padding = new Padding(15)
            };

            btnZapiszReklamacje = new Button
            {
                Text = "✓ Zgłoś reklamację",
                Size = new Size(180, 45),
                Location = new Point(panelButtons.Width - 380, 15),
                BackColor = ColorTranslator.FromHtml("#27ae60"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnZapiszReklamacje.FlatAppearance.BorderSize = 0;
            btnZapiszReklamacje.MouseEnter += (s, e) => btnZapiszReklamacje.BackColor = ColorTranslator.FromHtml("#229954");
            btnZapiszReklamacje.MouseLeave += (s, e) => btnZapiszReklamacje.BackColor = ColorTranslator.FromHtml("#27ae60");
            btnZapiszReklamacje.Click += BtnZapiszReklamacje_Click;

            btnAnuluj = new Button
            {
                Text = "✗ Anuluj",
                Size = new Size(180, 45),
                Location = new Point(panelButtons.Width - 190, 15),
                BackColor = ColorTranslator.FromHtml("#95a5a6"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                DialogResult = DialogResult.Cancel
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;
            btnAnuluj.MouseEnter += (s, e) => btnAnuluj.BackColor = ColorTranslator.FromHtml("#7f8c8d");
            btnAnuluj.MouseLeave += (s, e) => btnAnuluj.BackColor = ColorTranslator.FromHtml("#95a5a6");

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
            // Partie z ostatnich 2 tygodni - OD NAJNOWSZYCH (DESC)
            DateTime dataOd = DateTime.Now.AddDays(-14);

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
        ORDER BY [CreateData] DESC, [CreateGodzina] DESC";

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
            BeginInvoke(new Action(() =>
            {
                ObliczSumeKg();
                AktualizujLiczniki();
            }));
        }

        private void CheckedListBoxPartie_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            BeginInvoke(new Action(() => AktualizujLiczniki()));
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

        private void AktualizujLiczniki()
        {
            lblLicznikTowary.Text = $"Zaznaczono: {checkedListBoxTowary.CheckedItems.Count}";
            lblLicznikPartie.Text = $"Zaznaczono: {checkedListBoxPartie.CheckedItems.Count}";
            lblLicznikZdjecia.Text = $"Zdjęć: {sciezkiZdjec.Count}";
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
                var zaznaczoneTowary = new List<int>();
                for (int i = 0; i < checkedListBoxTowary.CheckedIndices.Count; i++)
                {
                    int index = checkedListBoxTowary.CheckedIndices[i];
                    zaznaczoneTowary.Add(Convert.ToInt32(dtTowary.Rows[index]["TowarID"]));
                }

                var zaznaczonePartie = new List<string>();

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

                ZapiszReklamacjeDoBAzy(zaznaczoneTowary, zaznaczonePartie, sumaKg);

                MessageBox.Show("Reklamacja została pomyślnie zgłoszona!", "Sukces",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

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