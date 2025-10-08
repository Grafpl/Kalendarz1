using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class CRM : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string operatorID = "";
        private int aktualnyOdbiorcaID = 0;
        private bool isDataLoading = false;

        private readonly string[] priorytetowePKD = new string[]
        {
            "Sprzedaż detaliczna mięsa i wyrobów z mięsa prowadzona w wyspecjalizowanych sklepach",
            "Przetwarzanie i konserwowanie mięsa z drobiu",
            "Produkcja wyrobów z mięsa, włączając wyroby z mięsa drobiowego",
            "Ubój zwierząt, z wyłączeniem drobiu i królików"
        };

        public string UserID { get; set; }

        public CRM()
        {
            InitializeComponent();

            dataGridViewOdbiorcy.EditMode = DataGridViewEditMode.EditOnEnter;

            comboBoxStatusFilter.Items.Clear();
            comboBoxStatusFilter.Items.Add("Wszystkie statusy");
            comboBoxStatusFilter.Items.AddRange(new object[] {
                "Do zadzwonienia", "Próba kontaktu", "Nawiązano kontakt", "Zgoda na dalszy kontakt",
                "Do wysłania oferta", "Nie zainteresowany", "Poprosił o usunięcie", "Błędny rekord (do raportu)"
            });
            comboBoxStatusFilter.SelectedIndex = 0;

            comboBoxPowiatFilter.Items.Clear();
            comboBoxPowiatFilter.Items.Add("Wszystkie powiaty");
            comboBoxPowiatFilter.SelectedIndex = 0;

            comboBoxPKD.Items.Clear();
            comboBoxPKD.Items.Add("Wszystkie Rodzaje");
            comboBoxPKD.SelectedIndex = 0;

            comboBoxPKD.DrawMode = DrawMode.OwnerDrawFixed;
            comboBoxPKD.DrawItem += ComboBoxPKD_DrawItem;

            comboBoxWoj.Items.Clear();
            comboBoxWoj.Items.Add("Wszystkie Woj.");
            comboBoxWoj.SelectedIndex = 0;

            splitContainerMain.SplitterDistance = (int)(this.ClientSize.Width * 0.75);

            DodajHoverEffect(button2);
            DodajHoverEffect(button3);
            DodajHoverEffect(buttonDodajNotatke);
        }

        private void ComboBoxPKD_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();

            string text = comboBoxPKD.Items[e.Index].ToString();
            bool isPriorytetowy = priorytetowePKD.Any(p => text.Contains(p.Substring(0, Math.Min(30, p.Length))));

            Font font = isPriorytetowy ? new Font(e.Font, FontStyle.Bold) : e.Font;
            Color color = isPriorytetowy ? Color.DarkRed : e.ForeColor;

            using (Brush brush = new SolidBrush(color))
            {
                e.Graphics.DrawString(text, font, brush, e.Bounds);
            }
            e.DrawFocusRectangle();
        }

        private void DodajHoverEffect(Button btn)
        {
            Color originalColor = btn.BackColor;
            btn.MouseEnter += (s, e) => {
                btn.BackColor = ControlPaint.Light(originalColor, 0.2f);
                btn.Cursor = Cursors.Hand;
            };
            btn.MouseLeave += (s, e) => {
                btn.BackColor = originalColor;
            };
        }

        private void CRM_Load(object sender, EventArgs e)
        {
            operatorID = UserID;

            SprawdzIUtworzTabeleHistorii();

            KonfigurujDataGridView();
            WczytajOdbiorcow();
            WczytajRankingHandlowcow();

            DodajPrzyciskHistoria();
            DodajPrzyciskMapa();
            DodajPrzyciskDodajOdbiorcę();
            DodajPrzyciskZadania();

            if (operatorID == "11111")
            {
                DodajPrzyciskAdmin();
            }

            DodajPrzyciskOdswiez();
        }

        private void SprawdzIUtworzTabeleHistorii()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    var cmdCheck = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HistoriaZmianCRM')
                        BEGIN
                            CREATE TABLE HistoriaZmianCRM (
                                ID INT IDENTITY(1,1) PRIMARY KEY,
                                IDOdbiorcy INT NOT NULL,
                                TypZmiany NVARCHAR(100),
                                WartoscStara NVARCHAR(500),
                                WartoscNowa NVARCHAR(500),
                                KtoWykonal NVARCHAR(50),
                                DataZmiany DATETIME DEFAULT GETDATE()
                            )
                        END", conn);
                    cmdCheck.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd tworzenia tabeli historii: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DodajPrzyciskOdswiez()
        {
            var btnOdswiez = new Button
            {
                Text = "🔄 Odśwież",
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnOdswiez.FlatAppearance.BorderSize = 0;
            btnOdswiez.Click += (s, e) => {
                dataGridViewOdbiorcy.DataSource = null;
                KonfigurujDataGridView();
                WczytajOdbiorcow();
                WczytajRankingHandlowcow();
            };

            panelSearch.Controls.Add(btnOdswiez);
            int pozycja = operatorID == "11111" ? -710 : -590;
            btnOdswiez.Location = new Point(panelSearch.Width + pozycja, 10);
            DodajHoverEffect(btnOdswiez);
        }

        private void DodajPrzyciskAdmin()
        {
            var btnAdmin = new Button
            {
                Text = "⚙ Admin",
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(243, 156, 18),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnAdmin.FlatAppearance.BorderSize = 0;
            btnAdmin.Click += (s, e) => {
                var panel = new PanelAdministracyjny(connectionString);
                panel.ShowDialog();
                WczytajOdbiorcow();
            };

            panelSearch.Controls.Add(btnAdmin);
            btnAdmin.Location = new Point(panelSearch.Width - 590, 10);
            DodajHoverEffect(btnAdmin);
        }

        private void DodajPrzyciskZadania()
        {
            var btnZadania = new Button
            {
                Text = "📋 Zadania",
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnZadania.FlatAppearance.BorderSize = 0;
            btnZadania.Click += (s, e) => {
                var formZadania = new FormZadania(connectionString, operatorID);
                formZadania.ShowDialog();
            };

            panelSearch.Controls.Add(btnZadania);
            btnZadania.Location = new Point(panelSearch.Width - 470, 10);
            DodajHoverEffect(btnZadania);
        }

        private void DodajPrzyciskDodajOdbiorcę()
        {
            var btnDodaj = new Button
            {
                Text = "➕ Dodaj",
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnDodaj.FlatAppearance.BorderSize = 0;
            btnDodaj.Click += (s, e) => {
                var formDodaj = new FormDodajOdbiorce(connectionString, operatorID);
                if (formDodaj.ShowDialog() == DialogResult.OK)
                {
                    WczytajOdbiorcow();
                }
            };

            panelSearch.Controls.Add(btnDodaj);
            btnDodaj.Location = new Point(panelSearch.Width - 350, 10);
            DodajHoverEffect(btnDodaj);
        }

        private void DodajPrzyciskMapa()
        {
            var btnMapa = new Button
            {
                Text = "🗺 Mapa",
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(155, 89, 182),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnMapa.FlatAppearance.BorderSize = 0;
            btnMapa.Click += (s, e) => {
                var formMapa = new FormMapaWojewodztwa(connectionString, operatorID);
                formMapa.ShowDialog();
            };

            panelSearch.Controls.Add(btnMapa);
            btnMapa.Location = new Point(panelSearch.Width - 230, 10);
            DodajHoverEffect(btnMapa);
        }

        private void DodajPrzyciskHistoria()
        {
            var btnHistoria = new Button
            {
                Text = "📜 Historia",
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(142, 68, 173),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnHistoria.FlatAppearance.BorderSize = 0;
            btnHistoria.Click += (s, e) => {
                if (aktualnyOdbiorcaID > 0)
                {
                    PokazHistorieZmianPelne(aktualnyOdbiorcaID);
                }
                else
                {
                    MessageBox.Show("Wybierz odbiorce aby zobaczyc pelna historie zmian.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            panelSearch.Controls.Add(btnHistoria);
            btnHistoria.Location = new Point(panelSearch.Width - 110, 10);
            DodajHoverEffect(btnHistoria);
        }

        private void PokazHistorieZmianPelne(int idOdbiorcy)
        {
            var formHistoria = new Form
            {
                Text = "📜 Pełna Historia Zmian",
                Size = new Size(1100, 650),
                StartPosition = FormStartPosition.CenterScreen,
                MinimizeBox = true,
                MaximizeBox = true,
                BackColor = Color.White
            };

            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowTemplate = { Height = 40 },
                AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(250, 250, 252) }
            };

            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(44, 62, 80);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(5);
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersHeight = 35;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                SELECT 
                    h.DataZmiany,
                    h.TypZmiany,
                    h.WartoscStara,
                    h.WartoscNowa,
                    ISNULL(
                        CASE 
                            WHEN CHARINDEX(' ', o.Name) > 0 
                            THEN LEFT(o.Name, CHARINDEX(' ', o.Name) - 1) + ' ' + LEFT(SUBSTRING(o.Name, CHARINDEX(' ', o.Name) + 1, LEN(o.Name)), 1) + '.'
                            ELSE o.Name
                        END,
                        'ID: ' + CAST(h.KtoWykonal AS NVARCHAR)
                    ) as OperatorID
                FROM HistoriaZmianCRM h
                LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                WHERE h.IDOdbiorcy = @id
                ORDER BY h.DataZmiany DESC", conn);
                    cmd.Parameters.AddWithValue("@id", idOdbiorcy);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    dgv.DataSource = dt;

                    if (dgv.Columns.Count > 0)
                    {
                        dgv.Columns[0].HeaderText = "Data i Czas";
                        dgv.Columns[0].DefaultCellStyle.Format = "dd.MM.yyyy HH:mm:ss";
                        dgv.Columns[0].FillWeight = 18;
                        dgv.Columns[0].MinimumWidth = 150;

                        dgv.Columns[1].HeaderText = "Typ Zmiany";
                        dgv.Columns[1].FillWeight = 20;
                        dgv.Columns[1].MinimumWidth = 140;

                        dgv.Columns[2].HeaderText = "Wartość Stara";
                        dgv.Columns[2].FillWeight = 28;
                        dgv.Columns[2].MinimumWidth = 220;
                        dgv.Columns[2].DefaultCellStyle.WrapMode = DataGridViewTriState.True;

                        dgv.Columns[3].HeaderText = "Wartość Nowa";
                        dgv.Columns[3].FillWeight = 28;
                        dgv.Columns[3].MinimumWidth = 220;
                        dgv.Columns[3].DefaultCellStyle.WrapMode = DataGridViewTriState.True;

                        dgv.Columns[4].HeaderText = "Kto wykonał";
                        dgv.Columns[4].FillWeight = 10;
                        dgv.Columns[4].MinimumWidth = 80;
                        dgv.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        dgv.Columns[4].DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                        dgv.Columns[4].DefaultCellStyle.ForeColor = Color.FromArgb(52, 152, 219);
                    }

                    var lblInfo = new Label
                    {
                        Text = $"📊 Liczba zmian: {dt.Rows.Count}",
                        Dock = DockStyle.Bottom,
                        Height = 35,
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding = new Padding(15, 0, 0, 0),
                        BackColor = Color.FromArgb(248, 249, 252),
                        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                        ForeColor = Color.FromArgb(44, 62, 80)
                    };
                    formHistoria.Controls.Add(lblInfo);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania historii: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                formHistoria.Close();
                return;
            }

            formHistoria.Controls.Add(dgv);
            formHistoria.ShowDialog();
        }
        private void WczytajOdbiorcow()
        {
            isDataLoading = true;
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("sp_PobierzOdbiorcow", conn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@OperatorID", operatorID);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    foreach (DataRow row in dt.Rows)
                    {
                        if (row["Status"].ToString() == "Nowy")
                            row["Status"] = "Do zadzwonienia";
                    }

                    dataGridViewOdbiorcy.DataSource = dt;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isDataLoading = false;
                dataGridViewOdbiorcy.Refresh();
                WypelnijFiltrPowiatow();
                WypelnijFiltrPKD();
                WypelnijFiltrWoj();
            }
        }

        private void WczytajRankingHandlowcow()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                SELECT 
                    ISNULL(
                        CASE 
                            WHEN CHARINDEX(' ', o.Name) > 0 
                            THEN LEFT(o.Name, CHARINDEX(' ', o.Name) - 1) + ' ' + LEFT(SUBSTRING(o.Name, CHARINDEX(' ', o.Name) + 1, LEN(o.Name)), 1) + '.'
                            ELSE o.Name
                        END,
                        'ID: ' + CAST(h.KtoWykonal AS NVARCHAR)
                    ) as 'Operator',
                    SUM(CASE WHEN h.WartoscNowa = 'Próba kontaktu' THEN 1 ELSE 0 END) as 'Próba kontaktu',
                    SUM(CASE WHEN h.WartoscNowa = 'Nawiązano kontakt' THEN 1 ELSE 0 END) as 'Nawiązano kontakt',
                    SUM(CASE WHEN h.WartoscNowa = 'Zgoda na dalszy kontakt' THEN 1 ELSE 0 END) as 'Zgoda na dalszy kontakt',
                    SUM(CASE WHEN h.WartoscNowa = 'Do wysłania oferta' THEN 1 ELSE 0 END) as 'Do wysłania oferta',
                    SUM(CASE WHEN h.WartoscNowa = 'Nie zainteresowany' THEN 1 ELSE 0 END) as 'Nie zainteresowany',
                    SUM(CASE WHEN h.WartoscNowa = 'Poprosił o usunięcie' THEN 1 ELSE 0 END) as 'Poprosił o usunięcie',
                    SUM(CASE WHEN h.WartoscNowa = 'Błędny rekord (do raportu)' THEN 1 ELSE 0 END) as 'Błędny rekord',
                    COUNT(*) as 'Suma',
                    MAX(h.DataZmiany) as 'Ostatnia zmiana'
                FROM HistoriaZmianCRM h
                LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                WHERE h.TypZmiany = 'Zmiana statusu'
                    AND h.WartoscStara != h.WartoscNowa
                    AND h.WartoscNowa != 'Nowy'
                GROUP BY h.KtoWykonal, o.Name
                ORDER BY COUNT(*) DESC", conn);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    dataGridViewRanking.DataSource = dt;

                    if (dataGridViewRanking.Columns.Count > 0)
                    {
                        dataGridViewRanking.Columns[0].FillWeight = 100;
                        dataGridViewRanking.Columns[0].MinimumWidth = 80;

                        for (int i = 1; i < dataGridViewRanking.Columns.Count - 2; i++)
                        {
                            dataGridViewRanking.Columns[i].FillWeight = 70;
                            dataGridViewRanking.Columns[i].MinimumWidth = 60;
                        }

                        if (dataGridViewRanking.Columns.Contains("Suma"))
                        {
                            var sumaCol = dataGridViewRanking.Columns["Suma"];
                            sumaCol.FillWeight = 50;
                            sumaCol.MinimumWidth = 50;
                        }

                        if (dataGridViewRanking.Columns.Contains("Ostatnia zmiana"))
                        {
                            var dataCol = dataGridViewRanking.Columns["Ostatnia zmiana"];
                            dataCol.FillWeight = 100;
                            dataCol.MinimumWidth = 120;
                            dataCol.DefaultCellStyle.Format = "dd.MM HH:mm";
                        }
                    }

                    if (dataGridViewRanking.Rows.Count > 0)
                    {
                        dataGridViewRanking.Rows[0].DefaultCellStyle.BackColor = Color.FromArgb(255, 243, 205);
                        dataGridViewRanking.Rows[0].DefaultCellStyle.ForeColor = Color.FromArgb(183, 110, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania rankingu: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void KonfigurujDataGridView()
        {
            dataGridViewOdbiorcy.AutoGenerateColumns = false;
            dataGridViewOdbiorcy.Columns.Clear();
            dataGridViewOdbiorcy.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridViewOdbiorcy.RowTemplate.Height = 65;
            dataGridViewOdbiorcy.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewOdbiorcy.AllowUserToResizeColumns = true;
            dataGridViewOdbiorcy.AllowUserToResizeRows = false;

            // Kolumna "Mój klient"
            var colCzyMoj = new DataGridViewTextBoxColumn
            {
                Name = "CzyMoj",
                DataPropertyName = "CzyMoj",
                HeaderText = "*",
                FillWeight = 3,
                MinimumWidth = 30,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 14, FontStyle.Bold),
                    ForeColor = Color.FromArgb(241, 196, 15)
                }
            };
            dataGridViewOdbiorcy.Columns.Add(colCzyMoj);

            // Nazwa - POPRAWIONA nazwa kolumny na NAZWA
            var colNazwa = new DataGridViewTextBoxColumn
            {
                Name = "Nazwa",
                DataPropertyName = "NAZWA",  // ✅ POPRAWIONE
                HeaderText = "Nazwa Firmy",
                FillWeight = 18,
                MinimumWidth = 160,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    WrapMode = DataGridViewTriState.True,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(44, 62, 80)
                }
            };
            dataGridViewOdbiorcy.Columns.Add(colNazwa);

            // Status
            var statusColumn = new DataGridViewComboBoxColumn
            {
                Name = "StatusColumn",
                DataPropertyName = "Status",
                HeaderText = "Status",
                FlatStyle = FlatStyle.Flat,
                FillWeight = 12,
                MinimumWidth = 120
            };
            statusColumn.Items.AddRange("Do zadzwonienia", "Próba kontaktu", "Nawiązano kontakt", "Zgoda na dalszy kontakt",
                "Do wysłania oferta", "Nie zainteresowany", "Poprosił o usunięcie", "Błędny rekord (do raportu)");
            dataGridViewOdbiorcy.Columns.Add(statusColumn);

            // Kod pocztowy - POPRAWIONA nazwa na KOD
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "KodPocztowy",
                DataPropertyName = "KOD",  // ✅ POPRAWIONE
                HeaderText = "Kod",
                FillWeight = 5,
                MinimumWidth = 60,
                ReadOnly = true
            });

            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "MIASTO", DataPropertyName = "MIASTO", HeaderText = "Miasto", FillWeight = 8, MinimumWidth = 80, ReadOnly = true });

            // Ulica - POPRAWIONA nazwa na ULICA
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Ulica",
                DataPropertyName = "ULICA",  // ✅ POPRAWIONE
                HeaderText = "Ulica",
                FillWeight = 7,
                MinimumWidth = 80,
                ReadOnly = true
            });

            // Telefon - POPRAWIONA nazwa na TELEFON_K
            var colTelefon = new DataGridViewTextBoxColumn
            {
                Name = "Telefon_K",
                DataPropertyName = "TELEFON_K",  // ✅ POPRAWIONE
                HeaderText = "Telefon",
                FillWeight = 8,
                MinimumWidth = 90,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(44, 62, 80)
                }
            };
            dataGridViewOdbiorcy.Columns.Add(colTelefon);

            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Wojewodztwo", DataPropertyName = "Wojewodztwo", HeaderText = "Województwo", FillWeight = 9, MinimumWidth = 100, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Powiat", DataPropertyName = "Powiat", HeaderText = "Powiat", FillWeight = 9, MinimumWidth = 100, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "PKD_Opis", DataPropertyName = "PKD_Opis", HeaderText = "Branza (PKD)", FillWeight = 12, MinimumWidth = 130, ReadOnly = true });

            // NOWA KOLUMNA: Ostatnia Zmiana
            var colOstatniaZmiana = new DataGridViewTextBoxColumn
            {
                Name = "OstatniaZmiana",
                DataPropertyName = "OstatniaZmiana",
                HeaderText = "Ostatnia Zmiana",
                FillWeight = 11,
                MinimumWidth = 120,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "dd.MM.yyyy HH:mm",
                    ForeColor = Color.FromArgb(231, 76, 60),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
                }
            };
            dataGridViewOdbiorcy.Columns.Add(colOstatniaZmiana);

            // Data ostatniej notatki
            var colData = new DataGridViewTextBoxColumn
            {
                Name = "DataOstatniejNotatki",
                DataPropertyName = "DataOstatniejNotatki",
                HeaderText = "Ostatnia Notatka",
                FillWeight = 10,
                MinimumWidth = 110,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "dd.MM.yyyy HH:mm",
                    ForeColor = Color.FromArgb(127, 140, 141)
                }
            };
            dataGridViewOdbiorcy.Columns.Add(colData);

            dataGridViewOdbiorcy.DefaultCellStyle.Font = new Font("Segoe UI", 9);
            dataGridViewOdbiorcy.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dataGridViewOdbiorcy.DefaultCellStyle.SelectionForeColor = Color.White;
            dataGridViewOdbiorcy.DefaultCellStyle.Padding = new Padding(5, 5, 5, 5);
            dataGridViewOdbiorcy.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 252);
        }

        private void dataGridViewOdbiorcy_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (!isDataLoading && e.RowIndex >= 0 && dataGridViewOdbiorcy.Columns[e.ColumnIndex].Name == "StatusColumn")
            {
                DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
                if (dt == null) return;

                DataRowView rowView = dataGridViewOdbiorcy.Rows[e.RowIndex].DataBoundItem as DataRowView;
                if (rowView == null) return;

                int idOdbiorcy = Convert.ToInt32(rowView["ID"]);
                string nowyStatus = dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["StatusColumn"].Value?.ToString() ?? "";

                string statusDlaBazy = nowyStatus == "Do zadzwonienia" ? "Nowy" : nowyStatus;
                string staryStatus = PobierzAktualnyStatus(idOdbiorcy);

                AktualizujStatusWBazie(idOdbiorcy, statusDlaBazy, staryStatus);
                dataGridViewOdbiorcy.InvalidateRow(e.RowIndex);

                if (aktualnyOdbiorcaID == idOdbiorcy)
                {
                    WczytajHistorieZmian(idOdbiorcy);
                }

                // Odśwież ranking po zmianie statusu
                WczytajRankingHandlowcow();

                var sugestia = ZaproponujNastepnyKrok(nowyStatus);
                if (sugestia != null)
                {
                    var result = MessageBox.Show(
                        $"Sugestia nastepnego kroku:\n\n{sugestia.Opis}\n\nCzy zaplanowac zadanie?",
                        "Nastepny krok",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );

                    if (result == DialogResult.Yes)
                        UtworzZadanie(idOdbiorcy, sugestia);
                }
            }
        }

        private string PobierzAktualnyStatus(int idOdbiorcy)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("SELECT Status FROM OdbiorcyCRM WHERE ID = @id", conn);
                    cmd.Parameters.AddWithValue("@id", idOdbiorcy);
                    return cmd.ExecuteScalar()?.ToString() ?? "Nowy";
                }
            }
            catch
            {
                return "Nowy";
            }
        }

        private void AktualizujStatusWBazie(int idOdbiorcy, string nowyStatus, string staryStatus)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            var cmdUpdate = new SqlCommand("UPDATE OdbiorcyCRM SET Status = @status WHERE ID = @id", conn, transaction);
                            cmdUpdate.Parameters.AddWithValue("@id", idOdbiorcy);
                            cmdUpdate.Parameters.AddWithValue("@status", (object)nowyStatus ?? DBNull.Value);
                            cmdUpdate.ExecuteNonQuery();

                            var cmdLog = new SqlCommand(@"
                                INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscStara, WartoscNowa, KtoWykonal, DataZmiany) 
                                VALUES (@idOdbiorcy, @typ, @stara, @nowa, @kto, GETDATE())", conn, transaction);
                            cmdLog.Parameters.AddWithValue("@idOdbiorcy", idOdbiorcy);
                            cmdLog.Parameters.AddWithValue("@typ", "Zmiana statusu");
                            cmdLog.Parameters.AddWithValue("@stara", (object)staryStatus ?? DBNull.Value);
                            cmdLog.Parameters.AddWithValue("@nowa", (object)nowyStatus ?? DBNull.Value);
                            cmdLog.Parameters.AddWithValue("@kto", operatorID);
                            cmdLog.ExecuteNonQuery();

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show($"Błąd transakcji: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd aktualizacji statusu: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private SugestiaKroku ZaproponujNastepnyKrok(string status) => status switch
        {
            "Próba kontaktu" => new SugestiaKroku { TypZadania = "Telefon", Opis = "Proba ponownego kontaktu telefonicznego", Termin = DateTime.Now.AddDays(2), Priorytet = 2 },
            "Nawiązano kontakt" => new SugestiaKroku { TypZadania = "Email", Opis = "Wyslac prezentacje firmy i oferty", Termin = DateTime.Now.AddDays(1), Priorytet = 3 },
            "Zgoda na dalszy kontakt" => new SugestiaKroku { TypZadania = "Oferta", Opis = "Przygotowac spersonalizowana oferte", Termin = DateTime.Now.AddHours(4), Priorytet = 3 },
            _ => null
        };

        private void UtworzZadanie(int idOdbiorcy, SugestiaKroku sugestia)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("INSERT INTO Zadania (IDOdbiorcy, OperatorID, TypZadania, Opis, TerminWykonania, Priorytet) VALUES (@odbiorca, @operator, @typ, @opis, @termin, @priorytet)", conn);
                    cmd.Parameters.AddWithValue("@odbiorca", idOdbiorcy);
                    cmd.Parameters.AddWithValue("@operator", operatorID);
                    cmd.Parameters.AddWithValue("@typ", sugestia.TypZadania);
                    cmd.Parameters.AddWithValue("@opis", sugestia.Opis);
                    cmd.Parameters.AddWithValue("@termin", sugestia.Termin);
                    cmd.Parameters.AddWithValue("@priorytet", sugestia.Priorytet);
                    cmd.ExecuteNonQuery();

                    MessageBox.Show("Zadanie zostalo utworzone pomyslnie!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd tworzenia zadania: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WczytajNotatki(int idOdbiorcy)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                SELECT 
                    n.Tresc, 
                    n.DataUtworzenia,
                    ISNULL(
                        CASE 
                            WHEN CHARINDEX(' ', o.Name) > 0 
                            THEN LEFT(o.Name, CHARINDEX(' ', o.Name) - 1) + ' ' + LEFT(SUBSTRING(o.Name, CHARINDEX(' ', o.Name) + 1, LEN(o.Name)), 1) + '.'
                            ELSE o.Name
                        END,
                        'ID: ' + CAST(n.KtoDodal AS NVARCHAR)
                    ) as Operator
                FROM NotatkiCRM n
                LEFT JOIN operators o ON n.KtoDodal = CAST(o.ID AS NVARCHAR)
                WHERE n.IDOdbiorcy = @id 
                ORDER BY n.DataUtworzenia DESC", conn);
                    cmd.Parameters.AddWithValue("@id", idOdbiorcy);
                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    dataGridViewNotatki.DataSource = dt;

                    if (dataGridViewNotatki.Columns.Count > 0)
                    {
                        dataGridViewNotatki.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

                        dataGridViewNotatki.Columns[0].HeaderText = "Treść notatki";
                        dataGridViewNotatki.Columns[0].FillWeight = 60;
                        dataGridViewNotatki.Columns[0].DefaultCellStyle.WrapMode = DataGridViewTriState.True;

                        if (dataGridViewNotatki.Columns.Count > 1)
                        {
                            dataGridViewNotatki.Columns[1].HeaderText = "Data";
                            dataGridViewNotatki.Columns[1].FillWeight = 25;
                            dataGridViewNotatki.Columns[1].DefaultCellStyle.Format = "dd.MM HH:mm";
                        }

                        if (dataGridViewNotatki.Columns.Count > 2)
                        {
                            dataGridViewNotatki.Columns[2].HeaderText = "Kto dodał";
                            dataGridViewNotatki.Columns[2].FillWeight = 15;
                            dataGridViewNotatki.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                            dataGridViewNotatki.Columns[2].DefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
                            dataGridViewNotatki.Columns[2].DefaultCellStyle.ForeColor = Color.FromArgb(52, 152, 219);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania notatek: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void WczytajHistorieZmian(int idOdbiorcy)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                SELECT TOP 10
                    h.DataZmiany,
                    h.WartoscStara as 'Status PRZED',
                    h.WartoscNowa as 'Status PO',
                    ISNULL(
                        CASE 
                            WHEN CHARINDEX(' ', o.Name) > 0 
                            THEN LEFT(o.Name, CHARINDEX(' ', o.Name) - 1) + ' ' + LEFT(SUBSTRING(o.Name, CHARINDEX(' ', o.Name) + 1, LEN(o.Name)), 1) + '.'
                            ELSE o.Name
                        END,
                        'ID: ' + CAST(h.KtoWykonal AS NVARCHAR)
                    ) as Operator
                FROM HistoriaZmianCRM h
                LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                WHERE h.IDOdbiorcy = @id
                  AND h.TypZmiany = 'Zmiana statusu'
                ORDER BY h.DataZmiany DESC", conn);
                    cmd.Parameters.AddWithValue("@id", idOdbiorcy);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    dataGridViewHistoria.DataSource = dt;

                    if (dataGridViewHistoria.Columns.Count > 0)
                    {
                        dataGridViewHistoria.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

                        dataGridViewHistoria.Columns[0].HeaderText = "Kiedy";
                        dataGridViewHistoria.Columns[0].FillWeight = 25;
                        dataGridViewHistoria.Columns[0].DefaultCellStyle.Format = "dd.MM HH:mm";
                        dataGridViewHistoria.Columns[0].MinimumWidth = 90;

                        dataGridViewHistoria.Columns[1].HeaderText = "Status PRZED";
                        dataGridViewHistoria.Columns[1].FillWeight = 30;
                        dataGridViewHistoria.Columns[1].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                        dataGridViewHistoria.Columns[1].DefaultCellStyle.ForeColor = Color.FromArgb(192, 57, 43);
                        dataGridViewHistoria.Columns[1].MinimumWidth = 100;

                        dataGridViewHistoria.Columns[2].HeaderText = "Status PO";
                        dataGridViewHistoria.Columns[2].FillWeight = 30;
                        dataGridViewHistoria.Columns[2].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                        dataGridViewHistoria.Columns[2].DefaultCellStyle.ForeColor = Color.FromArgb(39, 174, 96);
                        dataGridViewHistoria.Columns[2].DefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
                        dataGridViewHistoria.Columns[2].MinimumWidth = 100;

                        dataGridViewHistoria.Columns[3].HeaderText = "Kto zmienił";
                        dataGridViewHistoria.Columns[3].FillWeight = 15;
                        dataGridViewHistoria.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        dataGridViewHistoria.Columns[3].DefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
                        dataGridViewHistoria.Columns[3].DefaultCellStyle.ForeColor = Color.FromArgb(142, 68, 173);
                        dataGridViewHistoria.Columns[3].MinimumWidth = 60;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania historii: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void DodajNotatke(int idOdbiorcy, string tresc)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            var cmdNotatka = new SqlCommand("INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal) VALUES (@id, @tresc, @kto)", conn, transaction);
                            cmdNotatka.Parameters.AddWithValue("@id", idOdbiorcy);
                            cmdNotatka.Parameters.AddWithValue("@tresc", tresc);
                            cmdNotatka.Parameters.AddWithValue("@kto", operatorID);
                            cmdNotatka.ExecuteNonQuery();

                            var cmdLog = new SqlCommand(@"
                                INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany) 
                                VALUES (@id, @typ, @wartosc, @kto, GETDATE())", conn, transaction);
                            cmdLog.Parameters.AddWithValue("@id", idOdbiorcy);
                            cmdLog.Parameters.AddWithValue("@typ", "Dodanie notatki");
                            cmdLog.Parameters.AddWithValue("@wartosc", tresc.Length > 100 ? tresc.Substring(0, 100) + "..." : tresc);
                            cmdLog.Parameters.AddWithValue("@kto", operatorID);
                            cmdLog.ExecuteNonQuery();

                            transaction.Commit();

                            MessageBox.Show("Notatka zostala dodana!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
                WczytajNotatki(idOdbiorcy);
                WczytajHistorieZmian(idOdbiorcy);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd dodawania notatki: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WypelnijFiltrPowiatow()
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;

            var powiaty = dt.AsEnumerable()
                .Select(row => row.Field<string>("Powiat"))
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .OrderBy(p => p)
                .ToArray();

            comboBoxPowiatFilter.Items.Clear();
            comboBoxPowiatFilter.Items.Add("Wszystkie powiaty");
            comboBoxPowiatFilter.Items.AddRange(powiaty);
            comboBoxPowiatFilter.SelectedIndex = 0;
        }

        private void WypelnijFiltrPKD()
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;

            var wszystkiePKD = dt.AsEnumerable()
                .Where(row => !string.IsNullOrWhiteSpace(row.Field<string>("PKD_Opis")))
                .Select(row => row.Field<string>("PKD_Opis"))
                .Distinct()
                .ToList();

            var posortowane = new System.Collections.Generic.List<string>();

            foreach (var priorytet in priorytetowePKD)
            {
                var znalezione = wszystkiePKD.Where(p => p.Contains(priorytet.Substring(0, Math.Min(30, priorytet.Length)))).ToList();
                posortowane.AddRange(znalezione);
                foreach (var z in znalezione)
                    wszystkiePKD.Remove(z);
            }

            posortowane.AddRange(wszystkiePKD.OrderBy(x => x));

            comboBoxPKD.Items.Clear();
            comboBoxPKD.Items.Add("Wszystkie Rodzaje");
            foreach (var pkd in posortowane)
                comboBoxPKD.Items.Add(pkd);
            comboBoxPKD.SelectedIndex = 0;
        }

        private void WypelnijFiltrWoj()
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;

            var woj = dt.AsEnumerable()
                .Select(row => row.Field<string>("Wojewodztwo"))
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .OrderBy(p => p)
                .ToArray();

            comboBoxWoj.Items.Clear();
            comboBoxWoj.Items.Add("Wszystkie Woj.");
            comboBoxWoj.Items.AddRange(woj);
            comboBoxWoj.SelectedIndex = 0;
        }

        private void comboBoxStatusFilter_SelectedIndexChanged(object sender, EventArgs e) => ZastosujFiltry(sender, e);
        private void comboBoxPowiatFilter_SelectedIndexChanged(object sender, EventArgs e) => ZastosujFiltry(sender, e);

        private void ZastosujFiltry(object sender, EventArgs e)
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;

            var filtry = new System.Collections.Generic.List<string>();

            if (comboBoxStatusFilter.SelectedIndex > 0)
                filtry.Add($"Status = '{comboBoxStatusFilter.SelectedItem}'");

            if (comboBoxPowiatFilter.SelectedIndex > 0)
                filtry.Add($"Powiat = '{comboBoxPowiatFilter.SelectedItem}'");

            if (comboBoxPKD.SelectedIndex > 0)
                filtry.Add($"PKD_Opis = '{comboBoxPKD.SelectedItem}'");

            if (comboBoxWoj.SelectedIndex > 0)
                filtry.Add($"Wojewodztwo = '{comboBoxWoj.SelectedItem}'");

            dt.DefaultView.RowFilter = string.Join(" AND ", filtry);
        }

        private void dataGridViewOdbiorcy_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dataGridViewOdbiorcy.Rows.Count) return;

            DataGridViewRow row = dataGridViewOdbiorcy.Rows[e.RowIndex];
            string status = row.Cells["StatusColumn"].Value?.ToString() ?? "Do zadzwonienia";

            Color kolorWiersza = status switch
            {
                "Do zadzwonienia" => Color.FromArgb(236, 240, 241),
                "Próba kontaktu" => Color.FromArgb(174, 214, 241),
                "Nawiązano kontakt" => Color.FromArgb(133, 193, 233),
                "Zgoda na dalszy kontakt" => Color.FromArgb(169, 223, 191),
                "Do wysłania oferta" => Color.FromArgb(250, 219, 216),
                "Nie zainteresowany" => Color.FromArgb(245, 183, 177),
                "Poprosił o usunięcie" => Color.FromArgb(241, 148, 138),
                "Błędny rekord (do raportu)" => Color.FromArgb(248, 196, 113),
                _ => Color.White
            };

            row.DefaultCellStyle.BackColor = kolorWiersza;
            row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(
                kolorWiersza.A,
                Math.Max(0, kolorWiersza.R - 50),
                Math.Max(0, kolorWiersza.G - 50),
                Math.Max(0, kolorWiersza.B - 50)
            );
        }

        private void dataGridViewOdbiorcy_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.IsCurrentCellDirty)
                dataGridViewOdbiorcy.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dataGridViewOdbiorcy_CellEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                var row = dataGridViewOdbiorcy.Rows[e.RowIndex];
                if (row.IsNewRow) return;

                DataRowView rowView = row.DataBoundItem as DataRowView;
                if (rowView == null) return;

                if (rowView["ID"] == null || rowView["ID"] == DBNull.Value)
                    return;

                int idOdbiorcy = Convert.ToInt32(rowView["ID"]);
                if (aktualnyOdbiorcaID != idOdbiorcy)
                {
                    WczytajNotatki(idOdbiorcy);
                    WczytajHistorieZmian(idOdbiorcy);
                    aktualnyOdbiorcaID = idOdbiorcy;
                }
            }
        }

        private void DataGridViewRanking_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (e.ColumnIndex == 0) // Operator
            {
                e.CellStyle.Font = new Font(dataGridViewRanking.Font, FontStyle.Bold);
            }
            else if (dataGridViewRanking.Columns[e.ColumnIndex].Name == "Suma" ||
                     dataGridViewRanking.Columns[e.ColumnIndex].HeaderText == "Suma")
            {
                e.CellStyle.Font = new Font(dataGridViewRanking.Font, FontStyle.Bold);
                e.CellStyle.ForeColor = Color.FromArgb(0, 100, 0);
            }
            else
            {
                e.CellStyle.Font = new Font(dataGridViewRanking.Font, FontStyle.Regular);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.CurrentRow != null)
            {
                // ✅ POPRAWIONE - używa "Nazwa" jako nazwa kolumny w DataGridView
                string nazwaFirmy = dataGridViewOdbiorcy.CurrentRow.Cells["Nazwa"].Value?.ToString();
                if (!string.IsNullOrWhiteSpace(nazwaFirmy))
                {
                    string url = $"https://www.google.com/search?q={Uri.EscapeDataString(nazwaFirmy)}";
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                else
                    MessageBox.Show("Brak nazwy firmy do wyszukania.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.CurrentRow != null)
            {
                // ✅ POPRAWIONE - używa poprawnych nazw kolumn
                string ulica = dataGridViewOdbiorcy.CurrentRow.Cells["Ulica"].Value?.ToString() ?? "";
                string miasto = dataGridViewOdbiorcy.CurrentRow.Cells["MIASTO"].Value?.ToString() ?? "";
                string kodPocztowy = dataGridViewOdbiorcy.CurrentRow.Cells["KodPocztowy"].Value?.ToString() ?? "";
                string adresOdbiorcy = $"{ulica}, {kodPocztowy} {miasto}";
                string adresStartowy = "Koziołki 40, 95-061 Dmosin";

                if (!string.IsNullOrWhiteSpace(adresOdbiorcy))
                {
                    string url = $"https://www.google.com/maps/dir/{Uri.EscapeDataString(adresStartowy)}/{Uri.EscapeDataString(adresOdbiorcy)}";
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                else
                    MessageBox.Show("Brak danych adresowych.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void buttonDodajNotatke_Click(object sender, EventArgs e)
        {
            if (aktualnyOdbiorcaID > 0 && !string.IsNullOrWhiteSpace(textBoxNotatka.Text))
            {
                DodajNotatke(aktualnyOdbiorcaID, textBoxNotatka.Text);
                textBoxNotatka.Clear();
            }
            else
                MessageBox.Show("Wybierz odbiorce i wpisz tresc notatki.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void textBoxSzukaj_TextChanged(object sender, EventArgs e)
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;

            string szukanyTekst = textBoxSzukaj.Text.Trim().Replace("'", "''");
            // ✅ POPRAWIONE - używa "NAZWA" jako nazwa kolumny w DataTable
            dt.DefaultView.RowFilter = string.IsNullOrEmpty(szukanyTekst) ? "" : $"NAZWA LIKE '%{szukanyTekst}%'";
        }
    }

    public class SugestiaKroku
    {
        public string TypZadania { get; set; }
        public string Opis { get; set; }
        public DateTime Termin { get; set; }
        public int Priorytet { get; set; }
    }
}