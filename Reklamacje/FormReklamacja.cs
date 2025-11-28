using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private Label lblSumaKg;
        private Label lblLicznikTowary;
        private Label lblLicznikPartie;
        private Label lblLicznikZdjecia;
        private DataGridView dgvTowary;
        private DataGridView dgvPartie;
        private ListBox listBoxZdjecia;
        private Button btnDodajZdjecia;
        private Button btnUsunZdjecie;
        private Button btnZapiszReklamacje;
        private Button btnAnuluj;
        private PictureBox pictureBoxPodglad;
        private ComboBox cmbTypReklamacji;
        private ComboBox cmbPriorytet;

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
            WczytajTowaryZFaktury();
            WczytajPartie();
            AktualizujLiczniki();
        }

        private void InitializeComponent()
        {
            this.Text = "Zgłoszenie reklamacji";
            this.Size = new Size(1500, 950);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(1300, 850);
            this.BackColor = ColorTranslator.FromHtml("#f0f2f5");
            this.Font = new Font("Segoe UI", 9F);

            // ========== NAGŁÓWEK ==========
            Panel panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 120,
                BackColor = ColorTranslator.FromHtml("#1a73e8"),
                Padding = new Padding(30, 20, 30, 20)
            };

            // Gradient dla nagłówka
            panelHeader.Paint += (s, e) =>
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    panelHeader.ClientRectangle,
                    ColorTranslator.FromHtml("#1a73e8"),
                    ColorTranslator.FromHtml("#0d47a1"),
                    LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillRectangle(brush, panelHeader.ClientRectangle);
                }
            };

            Label lblTytul = new Label
            {
                Text = "NOWE ZGŁOSZENIE REKLAMACJI",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(30, 15),
                BackColor = Color.Transparent
            };

            Label lblPodtytul = new Label
            {
                Text = "Wypełnij formularz, aby zgłosić reklamację dla wybranej faktury",
                Font = new Font("Segoe UI", 11F),
                ForeColor = ColorTranslator.FromHtml("#bbdefb"),
                AutoSize = true,
                Location = new Point(32, 55),
                BackColor = Color.Transparent
            };

            // Info box po prawej
            Panel panelInfo = new Panel
            {
                Size = new Size(400, 80),
                Location = new Point(panelHeader.Width - 450, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(40, 255, 255, 255)
            };

            Label lblKontrahentInfo = new Label
            {
                Text = $"Kontrahent: {nazwaKontrahenta}",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(15, 12),
                BackColor = Color.Transparent,
                MaximumSize = new Size(370, 0)
            };

            Label lblFakturaInfo = new Label
            {
                Text = $"Faktura: {numerDokumentu}",
                Font = new Font("Segoe UI", 10F),
                ForeColor = ColorTranslator.FromHtml("#e3f2fd"),
                AutoSize = true,
                Location = new Point(15, 45),
                BackColor = Color.Transparent
            };

            panelInfo.Controls.AddRange(new Control[] { lblKontrahentInfo, lblFakturaInfo });
            panelHeader.Controls.AddRange(new Control[] { lblTytul, lblPodtytul, panelInfo });
            this.Controls.Add(panelHeader);

            // ========== GŁÓWNA ZAWARTOŚĆ ==========
            Panel panelMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(25, 20, 25, 10),
                AutoScroll = true
            };

            // Layout: dwie kolumny
            TableLayoutPanel layoutGlowny = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 700,
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = true
            };
            layoutGlowny.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            layoutGlowny.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            layoutGlowny.RowStyles.Add(new RowStyle(SizeType.Absolute, 350F));
            layoutGlowny.RowStyles.Add(new RowStyle(SizeType.Absolute, 350F));

            // ===== KARTA 1: TOWARY =====
            Panel kartaTowary = StworzKarte("TOWARY Z FAKTURY", "Zaznacz produkty objęte reklamacją", ColorTranslator.FromHtml("#e8f5e9"), ColorTranslator.FromHtml("#2e7d32"));
            kartaTowary.Dock = DockStyle.Fill;
            kartaTowary.Margin = new Padding(0, 0, 10, 10);

            dgvTowary = new DataGridView
            {
                Location = new Point(15, 70),
                Size = new Size(kartaTowary.Width - 30, 220),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Segoe UI", 9.5F),
                RowTemplate = { Height = 32 },
                EnableHeadersVisualStyles = false
            };
            dgvTowary.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#2e7d32");
            dgvTowary.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvTowary.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvTowary.ColumnHeadersHeight = 40;
            dgvTowary.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#f5f5f5");
            dgvTowary.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#c8e6c9");
            dgvTowary.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgvTowary.CellClick += (s, e) => AktualizujLiczniki();

            lblLicznikTowary = new Label
            {
                Text = "Zaznaczono: 0 towar(ów)",
                Location = new Point(15, 300),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#2e7d32"),
                AutoSize = true
            };

            kartaTowary.Controls.Add(dgvTowary);
            kartaTowary.Controls.Add(lblLicznikTowary);
            layoutGlowny.Controls.Add(kartaTowary, 0, 0);

            // ===== KARTA 2: DANE REKLAMACJI =====
            Panel kartaDane = StworzKarte("DANE REKLAMACJI", "Określ typ i priorytet zgłoszenia", ColorTranslator.FromHtml("#fff3e0"), ColorTranslator.FromHtml("#ef6c00"));
            kartaDane.Dock = DockStyle.Fill;
            kartaDane.Margin = new Padding(10, 0, 0, 10);

            Label lblTyp = new Label
            {
                Text = "Typ reklamacji:",
                Location = new Point(15, 75),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#424242"),
                AutoSize = true
            };

            cmbTypReklamacji = new ComboBox
            {
                Location = new Point(15, 100),
                Size = new Size(250, 35),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 11F),
                FlatStyle = FlatStyle.Flat
            };
            cmbTypReklamacji.Items.AddRange(new object[] { "Jakość produktu", "Ilość / Brak towaru", "Uszkodzenie w transporcie", "Termin ważności", "Niezgodność z zamówieniem", "Inne" });
            cmbTypReklamacji.SelectedIndex = 0;

            Label lblPriorytetLabel = new Label
            {
                Text = "Priorytet:",
                Location = new Point(15, 150),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#424242"),
                AutoSize = true
            };

            cmbPriorytet = new ComboBox
            {
                Location = new Point(15, 175),
                Size = new Size(250, 35),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 11F),
                FlatStyle = FlatStyle.Flat
            };
            cmbPriorytet.Items.AddRange(new object[] { "Niski", "Normalny", "Wysoki", "Krytyczny" });
            cmbPriorytet.SelectedIndex = 1;
            cmbPriorytet.DrawMode = DrawMode.OwnerDrawFixed;
            cmbPriorytet.DrawItem += CmbPriorytet_DrawItem;

            Label lblOpisLabel = new Label
            {
                Text = "Opis problemu: *",
                Location = new Point(15, 225),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#424242"),
                AutoSize = true
            };

            txtOpis = new TextBox
            {
                Location = new Point(15, 250),
                Size = new Size(kartaDane.Width - 50, 80),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                Font = new Font("Segoe UI", 10F),
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = ScrollBars.Vertical
            };

            kartaDane.Controls.AddRange(new Control[] { lblTyp, cmbTypReklamacji, lblPriorytetLabel, cmbPriorytet, lblOpisLabel, txtOpis });
            layoutGlowny.Controls.Add(kartaDane, 1, 0);

            // ===== KARTA 3: PARTIE =====
            Panel kartaPartie = StworzKarte("PARTIE DOSTAWCY", "Wybierz partie powiązane z reklamacją (ostatnie 14 dni)", ColorTranslator.FromHtml("#e3f2fd"), ColorTranslator.FromHtml("#1565c0"));
            kartaPartie.Dock = DockStyle.Fill;
            kartaPartie.Margin = new Padding(0, 10, 10, 0);

            dgvPartie = new DataGridView
            {
                Location = new Point(15, 70),
                Size = new Size(kartaPartie.Width - 30, 220),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Segoe UI", 9.5F),
                RowTemplate = { Height = 32 },
                EnableHeadersVisualStyles = false
            };
            dgvPartie.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#1565c0");
            dgvPartie.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvPartie.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvPartie.ColumnHeadersHeight = 40;
            dgvPartie.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#f5f5f5");
            dgvPartie.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#bbdefb");
            dgvPartie.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgvPartie.CellClick += (s, e) => AktualizujLiczniki();

            lblLicznikPartie = new Label
            {
                Text = "Zaznaczono: 0 parti(i)",
                Location = new Point(15, 300),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#1565c0"),
                AutoSize = true
            };

            kartaPartie.Controls.Add(dgvPartie);
            kartaPartie.Controls.Add(lblLicznikPartie);
            layoutGlowny.Controls.Add(kartaPartie, 0, 1);

            // ===== KARTA 4: ZDJĘCIA =====
            Panel kartaZdjecia = StworzKarte("DOKUMENTACJA ZDJĘCIOWA", "Dodaj zdjęcia dokumentujące problem (opcjonalnie)", ColorTranslator.FromHtml("#fce4ec"), ColorTranslator.FromHtml("#c2185b"));
            kartaZdjecia.Dock = DockStyle.Fill;
            kartaZdjecia.Margin = new Padding(10, 10, 0, 0);

            listBoxZdjecia = new ListBox
            {
                Location = new Point(15, 70),
                Size = new Size(200, 180),
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.FixedSingle
            };
            listBoxZdjecia.SelectedIndexChanged += ListBoxZdjecia_SelectedIndexChanged;

            btnDodajZdjecia = new Button
            {
                Text = "+ Dodaj",
                Size = new Size(90, 35),
                Location = new Point(15, 260),
                BackColor = ColorTranslator.FromHtml("#c2185b"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDodajZdjecia.FlatAppearance.BorderSize = 0;
            btnDodajZdjecia.Click += BtnDodajZdjecia_Click;

            btnUsunZdjecie = new Button
            {
                Text = "- Usuń",
                Size = new Size(90, 35),
                Location = new Point(115, 260),
                BackColor = ColorTranslator.FromHtml("#757575"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnUsunZdjecie.FlatAppearance.BorderSize = 0;
            btnUsunZdjecie.Click += BtnUsunZdjecie_Click;

            pictureBoxPodglad = new PictureBox
            {
                Location = new Point(230, 70),
                Size = new Size(200, 180),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorTranslator.FromHtml("#fafafa")
            };

            lblLicznikZdjecia = new Label
            {
                Text = "Dodano: 0 zdjęć",
                Location = new Point(230, 260),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#c2185b"),
                AutoSize = true
            };

            kartaZdjecia.Controls.AddRange(new Control[] { listBoxZdjecia, btnDodajZdjecia, btnUsunZdjecie, pictureBoxPodglad, lblLicznikZdjecia });
            layoutGlowny.Controls.Add(kartaZdjecia, 1, 1);

            panelMain.Controls.Add(layoutGlowny);
            this.Controls.Add(panelMain);

            // ========== STOPKA ==========
            Panel panelFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                BackColor = Color.White,
                Padding = new Padding(25, 15, 25, 15)
            };

            // Linia górna
            Panel liniaGorna = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = ColorTranslator.FromHtml("#e0e0e0")
            };
            panelFooter.Controls.Add(liniaGorna);

            lblSumaKg = new Label
            {
                Text = "Suma kg: 0,00 kg",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#1a73e8"),
                AutoSize = true,
                Location = new Point(25, 25)
            };

            btnZapiszReklamacje = new Button
            {
                Text = "ZGŁOŚ REKLAMACJĘ",
                Size = new Size(220, 50),
                Location = new Point(panelFooter.Width - 470, 15),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = ColorTranslator.FromHtml("#1a73e8"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnZapiszReklamacje.FlatAppearance.BorderSize = 0;
            btnZapiszReklamacje.Click += BtnZapiszReklamacje_Click;
            btnZapiszReklamacje.MouseEnter += (s, e) => btnZapiszReklamacje.BackColor = ColorTranslator.FromHtml("#1557b0");
            btnZapiszReklamacje.MouseLeave += (s, e) => btnZapiszReklamacje.BackColor = ColorTranslator.FromHtml("#1a73e8");

            btnAnuluj = new Button
            {
                Text = "Anuluj",
                Size = new Size(180, 50),
                Location = new Point(panelFooter.Width - 230, 15),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = ColorTranslator.FromHtml("#f5f5f5"),
                ForeColor = ColorTranslator.FromHtml("#424242"),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F),
                Cursor = Cursors.Hand
            };
            btnAnuluj.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#bdbdbd");
            btnAnuluj.FlatAppearance.BorderSize = 1;
            btnAnuluj.Click += (s, e) => this.Close();
            btnAnuluj.MouseEnter += (s, e) => btnAnuluj.BackColor = ColorTranslator.FromHtml("#eeeeee");
            btnAnuluj.MouseLeave += (s, e) => btnAnuluj.BackColor = ColorTranslator.FromHtml("#f5f5f5");

            panelFooter.Controls.AddRange(new Control[] { lblSumaKg, btnZapiszReklamacje, btnAnuluj });
            this.Controls.Add(panelFooter);
        }

        private Panel StworzKarte(string tytul, string podtytul, Color kolorTla, Color kolorAkcentu)
        {
            Panel karta = new Panel
            {
                BackColor = Color.White,
                Padding = new Padding(15)
            };

            // Pasek kolorowy u góry
            Panel pasek = new Panel
            {
                Dock = DockStyle.Top,
                Height = 5,
                BackColor = kolorAkcentu
            };

            Label lblTytul = new Label
            {
                Text = tytul,
                Location = new Point(15, 15),
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = kolorAkcentu,
                AutoSize = true
            };

            Label lblPodtytul = new Label
            {
                Text = podtytul,
                Location = new Point(15, 42),
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColorTranslator.FromHtml("#757575"),
                AutoSize = true
            };

            karta.Controls.AddRange(new Control[] { pasek, lblTytul, lblPodtytul });

            // Cień
            karta.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, karta.ClientRectangle,
                    ColorTranslator.FromHtml("#e0e0e0"), 1, ButtonBorderStyle.Solid,
                    ColorTranslator.FromHtml("#e0e0e0"), 1, ButtonBorderStyle.Solid,
                    ColorTranslator.FromHtml("#e0e0e0"), 1, ButtonBorderStyle.Solid,
                    ColorTranslator.FromHtml("#e0e0e0"), 1, ButtonBorderStyle.Solid);
            };

            return karta;
        }

        private void CmbPriorytet_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            e.DrawBackground();

            Color kolor = Color.Black;
            string tekst = cmbPriorytet.Items[e.Index].ToString();

            switch (e.Index)
            {
                case 0: kolor = ColorTranslator.FromHtml("#4caf50"); break; // Niski - zielony
                case 1: kolor = ColorTranslator.FromHtml("#2196f3"); break; // Normalny - niebieski
                case 2: kolor = ColorTranslator.FromHtml("#ff9800"); break; // Wysoki - pomarańczowy
                case 3: kolor = ColorTranslator.FromHtml("#f44336"); break; // Krytyczny - czerwony
            }

            using (Brush brush = new SolidBrush(kolor))
            {
                e.Graphics.FillEllipse(brush, new Rectangle(e.Bounds.X + 5, e.Bounds.Y + 8, 10, 10));
            }

            using (Brush brush = new SolidBrush(e.ForeColor))
            {
                e.Graphics.DrawString(tekst, e.Font, brush, e.Bounds.X + 22, e.Bounds.Y + 4);
            }

            e.DrawFocusRectangle();
        }

        private void WczytajTowaryZFaktury()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringHandel))
                {
                    conn.Open();

                    string query = @"
                        SELECT
                            DP.id AS ID,
                            DP.kod AS Symbol,
                            TW.kod AS Nazwa,
                            CAST(DP.ilosc AS DECIMAL(10,2)) AS Ilość,
                            CAST(DP.ilosc AS DECIMAL(10,2)) AS [Waga (kg)]
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

                dgvTowary.DataSource = dtTowary;

                if (dgvTowary.Columns.Contains("ID"))
                    dgvTowary.Columns["ID"].Visible = false;

                if (dgvTowary.Columns.Contains("Symbol"))
                {
                    dgvTowary.Columns["Symbol"].HeaderText = "Symbol";
                    dgvTowary.Columns["Symbol"].FillWeight = 30;
                }
                if (dgvTowary.Columns.Contains("Nazwa"))
                {
                    dgvTowary.Columns["Nazwa"].HeaderText = "Nazwa towaru";
                    dgvTowary.Columns["Nazwa"].FillWeight = 45;
                }
                if (dgvTowary.Columns.Contains("Ilość"))
                {
                    dgvTowary.Columns["Ilość"].HeaderText = "Ilość";
                    dgvTowary.Columns["Ilość"].FillWeight = 12;
                    dgvTowary.Columns["Ilość"].DefaultCellStyle.Format = "N2";
                }
                if (dgvTowary.Columns.Contains("Waga (kg)"))
                {
                    dgvTowary.Columns["Waga (kg)"].HeaderText = "Waga (kg)";
                    dgvTowary.Columns["Waga (kg)"].FillWeight = 13;
                    dgvTowary.Columns["Waga (kg)"].DefaultCellStyle.Format = "N2";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania towarów:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WczytajPartie()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringLibraNet))
                {
                    conn.Open();

                    string query = @"
                        SELECT
                            [guid] AS ID,
                            [Partia] AS [Nr partii],
                            [CustomerID] AS [ID dostawcy],
                            [CustomerName] AS [Nazwa dostawcy],
                            CONVERT(VARCHAR, [CreateData], 104) + ' ' + LEFT([CreateGodzina], 8) AS [Data utworzenia]
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

                dgvPartie.DataSource = dtPartie;

                if (dgvPartie.Columns.Contains("ID"))
                    dgvPartie.Columns["ID"].Visible = false;

                if (dgvPartie.Columns.Contains("ID dostawcy"))
                {
                    dgvPartie.Columns["ID dostawcy"].FillWeight = 12;
                }
                if (dgvPartie.Columns.Contains("Nr partii"))
                {
                    dgvPartie.Columns["Nr partii"].FillWeight = 15;
                }
                if (dgvPartie.Columns.Contains("Nazwa dostawcy"))
                {
                    dgvPartie.Columns["Nazwa dostawcy"].FillWeight = 40;
                }
                if (dgvPartie.Columns.Contains("Data utworzenia"))
                {
                    dgvPartie.Columns["Data utworzenia"].FillWeight = 33;
                }
            }
            catch (Exception ex)
            {
                // Partie mogą nie istnieć - nie pokazuj błędu
                dtPartie = new DataTable();
            }
        }

        private void AktualizujLiczniki()
        {
            int zaznaczoneTowary = dgvTowary?.SelectedRows.Count ?? 0;
            int zaznaczonePartie = dgvPartie?.SelectedRows.Count ?? 0;
            int liczbaZdjec = sciezkiZdjec.Count;

            if (lblLicznikTowary != null)
                lblLicznikTowary.Text = $"Zaznaczono: {zaznaczoneTowary} towar(ów)";

            if (lblLicznikPartie != null)
                lblLicznikPartie.Text = $"Zaznaczono: {zaznaczonePartie} parti(i)";

            if (lblLicznikZdjecia != null)
                lblLicznikZdjecia.Text = $"Dodano: {liczbaZdjec} zdjęć";

            AktualizujSumeKg();
        }

        private void AktualizujSumeKg()
        {
            decimal suma = 0;

            if (dgvTowary != null && dtTowary != null)
            {
                foreach (DataGridViewRow row in dgvTowary.SelectedRows)
                {
                    if (row.Index < dtTowary.Rows.Count)
                    {
                        var waga = dtTowary.Rows[row.Index]["Waga (kg)"];
                        if (waga != DBNull.Value)
                            suma += Convert.ToDecimal(waga);
                    }
                }
            }

            if (lblSumaKg != null)
                lblSumaKg.Text = $"Suma kg: {suma:N2} kg";
        }

        private void ListBoxZdjecia_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxZdjecia.SelectedIndex >= 0 && listBoxZdjecia.SelectedIndex < sciezkiZdjec.Count)
            {
                try
                {
                    pictureBoxPodglad.Image?.Dispose();
                    pictureBoxPodglad.Image = Image.FromFile(sciezkiZdjec[listBoxZdjecia.SelectedIndex]);
                    btnUsunZdjecie.Enabled = true;
                }
                catch
                {
                    pictureBoxPodglad.Image = null;
                }
            }
            else
            {
                btnUsunZdjecie.Enabled = false;
            }
        }

        private void BtnDodajZdjecia_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Pliki graficzne|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Wszystkie pliki|*.*";
                ofd.Multiselect = true;
                ofd.Title = "Wybierz zdjęcia do reklamacji";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    foreach (string plik in ofd.FileNames)
                    {
                        if (!sciezkiZdjec.Contains(plik))
                        {
                            sciezkiZdjec.Add(plik);
                            listBoxZdjecia.Items.Add(Path.GetFileName(plik));
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
                pictureBoxPodglad.Image?.Dispose();
                pictureBoxPodglad.Image = null;
                btnUsunZdjecie.Enabled = false;
                AktualizujLiczniki();
            }
        }

        private void BtnZapiszReklamacje_Click(object sender, EventArgs e)
        {
            // Walidacja
            if (dgvTowary.SelectedRows.Count == 0)
            {
                MessageBox.Show("Zaznacz przynajmniej jeden towar do reklamacji!", "Uwaga",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtOpis.Text))
            {
                MessageBox.Show("Wprowadź opis problemu!", "Uwaga",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtOpis.Focus();
                return;
            }

            // Oblicz sumę kg
            decimal sumaKg = 0;
            foreach (DataGridViewRow row in dgvTowary.SelectedRows)
            {
                if (row.Index < dtTowary.Rows.Count)
                {
                    var waga = dtTowary.Rows[row.Index]["Waga (kg)"];
                    if (waga != DBNull.Value)
                        sumaKg += Convert.ToDecimal(waga);
                }
            }

            int idReklamacji = 0;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringLibraNet))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Zapisz główny rekord reklamacji
                            string queryReklamacja = @"
                                INSERT INTO [dbo].[Reklamacje]
                                (DataZgloszenia, UserID, IdDokumentu, NumerDokumentu, IdKontrahenta, NazwaKontrahenta, Opis, SumaKg, Status, TypReklamacji, Priorytet)
                                VALUES
                                (GETDATE(), @UserID, @IdDokumentu, @NumerDokumentu, @IdKontrahenta, @NazwaKontrahenta, @Opis, @SumaKg, 'Nowa', @TypReklamacji, @Priorytet);
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
                                cmd.Parameters.AddWithValue("@TypReklamacji", cmbTypReklamacji.SelectedItem?.ToString() ?? "Inne");
                                cmd.Parameters.AddWithValue("@Priorytet", cmbPriorytet.SelectedItem?.ToString() ?? "Normalny");

                                idReklamacji = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            // 2. Zapisz towary
                            string queryTowary = @"
                                INSERT INTO [dbo].[ReklamacjeTowary]
                                (IdReklamacji, IdTowaru, Symbol, Nazwa, Ilosc, Waga)
                                VALUES
                                (@IdReklamacji, @IdTowaru, @Symbol, @Nazwa, @Ilosc, @Waga)";

                            foreach (DataGridViewRow row in dgvTowary.SelectedRows)
                            {
                                if (row.Index < dtTowary.Rows.Count)
                                {
                                    DataRow dataRow = dtTowary.Rows[row.Index];
                                    using (SqlCommand cmd = new SqlCommand(queryTowary, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                        cmd.Parameters.AddWithValue("@IdTowaru", dataRow["ID"]);
                                        cmd.Parameters.AddWithValue("@Symbol", dataRow["Symbol"] ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@Nazwa", dataRow["Nazwa"] ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@Ilosc", dataRow["Ilość"] ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@Waga", dataRow["Waga (kg)"] ?? DBNull.Value);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            // 3. Zapisz partie (jeśli są)
                            if (dgvPartie.SelectedRows.Count > 0 && dtPartie != null && dtPartie.Rows.Count > 0)
                            {
                                string queryPartie = @"
                                    INSERT INTO [dbo].[ReklamacjePartie]
                                    (IdReklamacji, GuidPartii, NumerPartii, CustomerID, CustomerName)
                                    VALUES
                                    (@IdReklamacji, @GuidPartii, @NumerPartii, @CustomerID, @CustomerName)";

                                foreach (DataGridViewRow row in dgvPartie.SelectedRows)
                                {
                                    if (row.Index < dtPartie.Rows.Count)
                                    {
                                        DataRow dataRow = dtPartie.Rows[row.Index];
                                        using (SqlCommand cmd = new SqlCommand(queryPartie, conn, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                            cmd.Parameters.AddWithValue("@GuidPartii", dataRow["ID"] ?? DBNull.Value);
                                            cmd.Parameters.AddWithValue("@NumerPartii", dataRow["Nr partii"] ?? DBNull.Value);
                                            cmd.Parameters.AddWithValue("@CustomerID", dataRow["ID dostawcy"] ?? DBNull.Value);
                                            cmd.Parameters.AddWithValue("@CustomerName", dataRow["Nazwa dostawcy"] ?? DBNull.Value);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }

                            // 4. Zapisz zdjęcia (jeśli są)
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

                            // 5. Dodaj wpis do historii
                            string queryHistoria = @"
                                INSERT INTO [dbo].[ReklamacjeHistoria]
                                (IdReklamacji, UserID, PoprzedniStatus, NowyStatus, Komentarz, TypAkcji)
                                VALUES
                                (@IdReklamacji, @UserID, NULL, 'Nowa', 'Utworzenie reklamacji', 'Utworzenie')";

                            using (SqlCommand cmd = new SqlCommand(queryHistoria, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                cmd.Parameters.AddWithValue("@UserID", userId);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();

                            MessageBox.Show(
                                $"Reklamacja nr {idReklamacji} została pomyślnie zgłoszona!\n\n" +
                                $"Typ: {cmbTypReklamacji.SelectedItem}\n" +
                                $"Priorytet: {cmbPriorytet.SelectedItem}\n" +
                                $"Towarów: {dgvTowary.SelectedRows.Count}\n" +
                                $"Suma kg: {sumaKg:N2}",
                                "Sukces",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception($"Błąd podczas zapisywania: {ex.Message}");
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
