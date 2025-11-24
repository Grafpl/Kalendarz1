using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kalendarz1.OfertaCenowa;

namespace Kalendarz1
{
    public partial class CRM : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string operatorID = "";
        private int aktualnyOdbiorcaID = 0;
        private bool isDataLoading = false;

        private ContextMenuStrip odbiorcaContextMenuStrip;
        private int clickedRowIndex = -1;

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

            InitializeFilters();

            splitContainerMain.SplitterDistance = (int)(this.ClientSize.Width * 0.75);

            DodajHoverEffect(button2);
            DodajHoverEffect(button3);
            DodajHoverEffect(buttonDodajNotatke);

            // UWAGA: Wywołanie InicjalizujContextMenu() zostało przeniesione do CRM_Load
        }

        private void InitializeFilters()
        {
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
        }

        private void InicjalizujContextMenu()
        {
            odbiorcaContextMenuStrip = new ContextMenuStrip();
            odbiorcaContextMenuStrip.Font = new Font("Segoe UI", 9.5F);

            // --- Akcje Główne ---
            var googleMenuItem = new ToolStripMenuItem("Wyszukaj w Google", SystemIcons.Information.ToBitmap(), GoogleMenuItem_Click);
            var trasaMenuItem = new ToolStripMenuItem("Pokaż trasę na mapie", SystemIcons.Application.ToBitmap(), TrasaMenuItem_Click);
            var ofertaMenuItem = new ToolStripMenuItem("Utwórz ofertę cenową", SystemIcons.Hand.ToBitmap(), OfertaMenuItem_Click);
            ofertaMenuItem.Font = new Font(this.Font, FontStyle.Bold);

            // --- Zmiana Statusu (dynamiczne podmenu) ---
            var statusParentMenuItem = new ToolStripMenuItem("Zmień status", SystemIcons.Warning.ToBitmap());
            var statusItems = ((DataGridViewComboBoxColumn)dataGridViewOdbiorcy.Columns["StatusColumn"]).Items;
            foreach (var status in statusItems)
            {
                var statusMenuItem = new ToolStripMenuItem(status.ToString(), null, StatusMenuItem_Click);
                statusMenuItem.Tag = status.ToString(); // Zapisujemy status w Tagu
                statusParentMenuItem.DropDownItems.Add(statusMenuItem);
            }

            // --- Kopiowanie Danych ---
            var copyParentMenuItem = new ToolStripMenuItem("Kopiuj do schowka", SystemIcons.Question.ToBitmap());
            var copyNipMenuItem = new ToolStripMenuItem("Kopiuj NIP", null, (s, e) => CopyDataToClipboard("NIP", "NIP"));
            var copyPhoneMenuItem = new ToolStripMenuItem("Kopiuj Telefon", null, (s, e) => CopyDataToClipboard("Telefon_K", "Telefon"));
            var copyAddressMenuItem = new ToolStripMenuItem("Kopiuj Adres", null, (s, e) =>
            {
                if (clickedRowIndex >= 0)
                {
                    var row = dataGridViewOdbiorcy.Rows[clickedRowIndex];
                    string ulica = row.Cells["Ulica"].Value?.ToString() ?? "";
                    string kod = row.Cells["KodPocztowy"].Value?.ToString() ?? "";
                    string miasto = row.Cells["MIASTO"].Value?.ToString() ?? "";
                    string pelnyAdres = $"{ulica}, {kod} {miasto}".Trim();
                    if (!string.IsNullOrEmpty(pelnyAdres) && pelnyAdres != ",")
                    {
                        Clipboard.SetText(pelnyAdres);
                    }
                }
            });
            copyParentMenuItem.DropDownItems.AddRange(new ToolStripItem[] { copyNipMenuItem, copyPhoneMenuItem, copyAddressMenuItem });


            // --- Składanie Menu ---
            odbiorcaContextMenuStrip.Items.Add(ofertaMenuItem);
            odbiorcaContextMenuStrip.Items.Add(new ToolStripSeparator());
            odbiorcaContextMenuStrip.Items.Add(googleMenuItem);
            odbiorcaContextMenuStrip.Items.Add(trasaMenuItem);
            odbiorcaContextMenuStrip.Items.Add(new ToolStripSeparator());
            odbiorcaContextMenuStrip.Items.Add(statusParentMenuItem);
            odbiorcaContextMenuStrip.Items.Add(copyParentMenuItem);


            dataGridViewOdbiorcy.CellMouseDown += DataGridViewOdbiorcy_CellMouseDown;
        }

        private void DataGridViewOdbiorcy_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    dataGridViewOdbiorcy.CurrentCell = dataGridViewOdbiorcy.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    clickedRowIndex = e.RowIndex;
                    odbiorcaContextMenuStrip.Show(Cursor.Position);
                }
            }
        }

        private void GoogleMenuItem_Click(object sender, EventArgs e)
        {
            if (clickedRowIndex >= 0)
            {
                string nazwaFirmy = dataGridViewOdbiorcy.Rows[clickedRowIndex].Cells["Nazwa"].Value?.ToString();
                if (!string.IsNullOrWhiteSpace(nazwaFirmy))
                {
                    string url = $"https://www.google.com/search?q={Uri.EscapeDataString(nazwaFirmy)}";
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
            }
        }

        private void TrasaMenuItem_Click(object sender, EventArgs e)
        {
            if (clickedRowIndex >= 0)
            {
                var row = dataGridViewOdbiorcy.Rows[clickedRowIndex];
                string ulica = row.Cells["Ulica"].Value?.ToString() ?? "";
                string miasto = row.Cells["MIASTO"].Value?.ToString() ?? "";
                string kodPocztowy = row.Cells["KodPocztowy"].Value?.ToString() ?? "";
                string adresOdbiorcy = $"{ulica}, {kodPocztowy} {miasto}";
                string adresStartowy = "Koziołki 40, 95-061 Dmosin";

                if (!string.IsNullOrWhiteSpace(ulica) && !string.IsNullOrWhiteSpace(miasto))
                {
                    string url = $"https://www.google.com/maps/dir/{Uri.EscapeDataString(adresStartowy)}/{Uri.EscapeDataString(adresOdbiorcy)}";
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
            }
        }

        private void OfertaMenuItem_Click(object sender, EventArgs e)
        {
            if (clickedRowIndex < 0) return;

            DataRowView rowView = dataGridViewOdbiorcy.Rows[clickedRowIndex].DataBoundItem as DataRowView;
            if (rowView != null)
            {
                try
                {
                    var klient = new KlientOferta
                    {
                        Nazwa = rowView["NAZWA"]?.ToString() ?? "",
                        NIP = rowView.Row.Table.Columns.Contains("NIP") ? rowView["NIP"]?.ToString() : "",
                        Adres = rowView["ULICA"]?.ToString() ?? "",
                        KodPocztowy = rowView["KOD"]?.ToString() ?? "",
                        Miejscowosc = rowView["MIASTO"]?.ToString() ?? "",
                        CzyReczny = true
                    };

                    //var ofertaWindow = new OfertaHandlowaWindow(klient);
                    //ofertaWindow.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Wystąpił błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void StatusMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && clickedRowIndex >= 0)
            {
                string nowyStatus = menuItem.Tag.ToString();
                dataGridViewOdbiorcy.Rows[clickedRowIndex].Cells["StatusColumn"].Value = nowyStatus;
            }
        }

        private void CopyDataToClipboard(string columnName, string dataName)
        {
            if (clickedRowIndex >= 0)
            {
                var value = dataGridViewOdbiorcy.Rows[clickedRowIndex].Cells[columnName].Value?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    Clipboard.SetText(value);
                }
                else
                {
                    MessageBox.Show($"Brak danych '{dataName}' do skopiowania.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
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

            // POPRAWKA: Inicjalizacja menu po skonfigurowaniu kolumn
            InicjalizujContextMenu();

            WczytajOdbiorcow();
            WczytajRankingHandlowcow();
            AddButtonsToPanel();
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

        private void AddButtonsToPanel()
        {
            flowLayoutButtons.Controls.Clear();

            var btnOdswiez = StworzPrzycisk("🔄 Odśwież", Color.FromArgb(41, 128, 185), (s, e) => {
                dataGridViewOdbiorcy.DataSource = null;
                WczytajOdbiorcow();
                WczytajRankingHandlowcow();
            });

            var btnHistoria = StworzPrzycisk("📜 Historia", Color.FromArgb(142, 68, 173), (s, e) => {
                if (aktualnyOdbiorcaID > 0)
                {
                    PokazHistorieZmianPelne(aktualnyOdbiorcaID);
                }
                else
                {
                    MessageBox.Show("Wybierz odbiorcę, aby zobaczyć pełną historię zmian.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            });

            var btnMapa = StworzPrzycisk("🗺 Mapa", Color.FromArgb(155, 89, 182), (s, e) => {
                var formMapa = new FormMapaWojewodztwa(connectionString, operatorID);
                formMapa.ShowDialog();
            });

            var btnDodaj = StworzPrzycisk("➕ Dodaj", Color.FromArgb(46, 204, 113), (s, e) => {
                var formDodaj = new FormDodajOdbiorce(connectionString, operatorID);
                if (formDodaj.ShowDialog() == DialogResult.OK)
                {
                    WczytajOdbiorcow();
                }
            });

            var btnZadania = StworzPrzycisk("📋 Zadania", Color.FromArgb(52, 152, 219), (s, e) => {
                var formZadania = new FormZadania(connectionString, operatorID);
                formZadania.ShowDialog();
            });

            flowLayoutButtons.Controls.Add(btnOdswiez);
            flowLayoutButtons.Controls.Add(btnHistoria);
            flowLayoutButtons.Controls.Add(btnMapa);
            flowLayoutButtons.Controls.Add(btnDodaj);
            flowLayoutButtons.Controls.Add(btnZadania);

            if (operatorID == "11111")
            {
                var btnAdmin = StworzPrzycisk("⚙ Admin", Color.FromArgb(243, 156, 18), (s, e) => {
                    var panel = new PanelAdministracyjny(connectionString);
                    panel.ShowDialog();
                    WczytajOdbiorcow();
                });
                flowLayoutButtons.Controls.Add(btnAdmin);
            }
        }

        private Button StworzPrzycisk(string text, Color color, EventHandler clickEvent)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(110, 32),
                BackColor = color,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(3, 0, 3, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += clickEvent;
            DodajHoverEffect(btn);
            return btn;
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
                        dgv.Columns["DataZmiany"].HeaderText = "Data i Czas";
                        dgv.Columns["DataZmiany"].DefaultCellStyle.Format = "dd.MM.yyyy HH:mm:ss";
                        dgv.Columns["DataZmiany"].FillWeight = 18;

                        dgv.Columns["TypZmiany"].HeaderText = "Typ Zmiany";
                        dgv.Columns["TypZmiany"].FillWeight = 20;

                        dgv.Columns["WartoscStara"].HeaderText = "Wartość Stara";
                        dgv.Columns["WartoscStara"].FillWeight = 28;
                        dgv.Columns["WartoscStara"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;

                        dgv.Columns["WartoscNowa"].HeaderText = "Wartość Nowa";
                        dgv.Columns["WartoscNowa"].FillWeight = 28;
                        dgv.Columns["WartoscNowa"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;

                        dgv.Columns["OperatorID"].HeaderText = "Kto wykonał";
                        dgv.Columns["OperatorID"].FillWeight = 10;
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
                        string status = row["Status"]?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(status) || status == "Nowy")
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
                    SUM(CASE WHEN h.WartoscNowa = 'Próba kontaktu' THEN 1 ELSE 0 END) as 'Próby',
                    SUM(CASE WHEN h.WartoscNowa = 'Nawiązano kontakt' THEN 1 ELSE 0 END) as 'Kontakt',
                    SUM(CASE WHEN h.WartoscNowa = 'Zgoda na dalszy kontakt' THEN 1 ELSE 0 END) as 'Zgoda',
                    SUM(CASE WHEN h.WartoscNowa = 'Do wysłania oferta' THEN 1 ELSE 0 END) as 'Oferta',
                    SUM(CASE WHEN h.WartoscNowa = 'Nie zainteresowany' THEN 1 ELSE 0 END) as 'Brak zaint.',
                    SUM(CASE WHEN h.WartoscNowa = 'Poprosił o usunięcie' THEN 1 ELSE 0 END) as 'Usunięcie',
                    SUM(CASE WHEN h.WartoscNowa = 'Błędny rekord (do raportu)' THEN 1 ELSE 0 END) as 'Błędny',
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
                        dataGridViewRanking.Columns["Operator"].FillWeight = 120;
                        dataGridViewRanking.Columns["Suma"].FillWeight = 60;
                        dataGridViewRanking.Columns["Ostatnia zmiana"].FillWeight = 110;
                        dataGridViewRanking.Columns["Ostatnia zmiana"].DefaultCellStyle.Format = "dd.MM HH:mm";

                        string[] kolumnyAktywnosci = { "Próby", "Kontakt", "Zgoda", "Oferta", "Brak zaint.", "Usunięcie", "Błędny" };
                        foreach (string kol in kolumnyAktywnosci)
                        {
                            if (dataGridViewRanking.Columns.Contains(kol))
                            {
                                dataGridViewRanking.Columns[kol].FillWeight = 75;
                            }
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
            dataGridViewOdbiorcy.AllowUserToResizeRows = false;

            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "CzyMoj", DataPropertyName = "CzyMoj", HeaderText = "*", FillWeight = 3, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.FromArgb(241, 196, 15) } });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Nazwa", DataPropertyName = "NAZWA", HeaderText = "Nazwa Firmy", FillWeight = 18, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(44, 62, 80) } });

            var statusColumn = new DataGridViewComboBoxColumn { Name = "StatusColumn", DataPropertyName = "Status", HeaderText = "Status", FlatStyle = FlatStyle.Flat, FillWeight = 12 };
            statusColumn.Items.AddRange("Do zadzwonienia", "Próba kontaktu", "Nawiązano kontakt", "Zgoda na dalszy kontakt", "Do wysłania oferta", "Nie zainteresowany", "Poprosił o usunięcie", "Błędny rekord (do raportu)");
            dataGridViewOdbiorcy.Columns.Add(statusColumn);

            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "KodPocztowy", DataPropertyName = "KOD", HeaderText = "Kod", FillWeight = 5, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "MIASTO", DataPropertyName = "MIASTO", HeaderText = "Miasto", FillWeight = 8, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ulica", DataPropertyName = "ULICA", HeaderText = "Ulica", FillWeight = 7, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Telefon_K", DataPropertyName = "TELEFON_K", HeaderText = "Telefon", FillWeight = 8, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(44, 62, 80) } });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Wojewodztwo", DataPropertyName = "Wojewodztwo", HeaderText = "Województwo", FillWeight = 9, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Powiat", DataPropertyName = "Powiat", HeaderText = "Powiat", FillWeight = 9, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "PKD_Opis", DataPropertyName = "PKD_Opis", HeaderText = "Branza (PKD)", FillWeight = 12, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "OstatniaZmiana", DataPropertyName = "OstatniaZmiana", HeaderText = "Ostatnia Zmiana", FillWeight = 11, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd.MM.yyyy HH:mm", ForeColor = Color.FromArgb(231, 76, 60), Font = new Font("Segoe UI", 8.5F, FontStyle.Regular) } });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "DataOstatniejNotatki", DataPropertyName = "DataOstatniejNotatki", HeaderText = "Ostatnia Notatka", FillWeight = 10, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd.MM.yyyy HH:mm", ForeColor = Color.FromArgb(127, 140, 141) } });

            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "NIP", DataPropertyName = "NIP", Visible = false });
        }

        private void dataGridViewOdbiorcy_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (isDataLoading || e.RowIndex < 0 || dataGridViewOdbiorcy.Columns[e.ColumnIndex].Name != "StatusColumn") return;

            DataRowView rowView = dataGridViewOdbiorcy.Rows[e.RowIndex].DataBoundItem as DataRowView;
            if (rowView == null) return;

            int idOdbiorcy = Convert.ToInt32(rowView["ID"]);
            string nowyStatus = dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["StatusColumn"].Value?.ToString() ?? "";
            string statusDlaBazy = nowyStatus == "Do zadzwonienia" ? "Nowy" : nowyStatus;
            string staryStatus = PobierzAktualnyStatus(idOdbiorcy);

            if (staryStatus != statusDlaBazy)
            {
                AktualizujStatusWBazie(idOdbiorcy, statusDlaBazy, staryStatus);
                dataGridViewOdbiorcy.InvalidateRow(e.RowIndex);

                if (aktualnyOdbiorcaID == idOdbiorcy) WczytajHistorieZmian(idOdbiorcy);
                WczytajRankingHandlowcow();

                var sugestia = ZaproponujNastepnyKrok(nowyStatus);
                if (sugestia != null && MessageBox.Show($"Sugestia następnego kroku:\n\n{sugestia.Opis}\n\nCzy zaplanować zadanie?", "Następny krok", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
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
            catch { return "Nowy"; }
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

        #region Unchanged Methods
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
                        dataGridViewNotatki.Columns["Tresc"].HeaderText = "Treść notatki";
                        dataGridViewNotatki.Columns["DataUtworzenia"].HeaderText = "Data";
                        dataGridViewNotatki.Columns["DataUtworzenia"].DefaultCellStyle.Format = "dd.MM HH:mm";
                        dataGridViewNotatki.Columns["Operator"].HeaderText = "Kto dodał";
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Błąd wczytywania notatek: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        private void WczytajHistorieZmian(int idOdbiorcy)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                SELECT TOP 10 h.DataZmiany, h.WartoscStara as 'Status PRZED', h.WartoscNowa as 'Status PO',
                    ISNULL(
                        CASE WHEN CHARINDEX(' ', o.Name) > 0 THEN LEFT(o.Name, CHARINDEX(' ', o.Name) - 1) + ' ' + LEFT(SUBSTRING(o.Name, CHARINDEX(' ', o.Name) + 1, LEN(o.Name)), 1) + '.' ELSE o.Name END,
                        'ID: ' + CAST(h.KtoWykonal AS NVARCHAR)
                    ) as Operator
                FROM HistoriaZmianCRM h
                LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                WHERE h.IDOdbiorcy = @id AND h.TypZmiany = 'Zmiana statusu'
                ORDER BY h.DataZmiany DESC", conn);
                    cmd.Parameters.AddWithValue("@id", idOdbiorcy);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    dataGridViewHistoria.DataSource = dt;

                    if (dataGridViewHistoria.Columns.Count > 0)
                    {
                        dataGridViewHistoria.Columns["DataZmiany"].HeaderText = "Kiedy";
                        dataGridViewHistoria.Columns["DataZmiany"].DefaultCellStyle.Format = "dd.MM HH:mm";
                        dataGridViewHistoria.Columns["Status PRZED"].DefaultCellStyle.ForeColor = Color.FromArgb(192, 57, 43);
                        dataGridViewHistoria.Columns["Status PO"].DefaultCellStyle.ForeColor = Color.FromArgb(39, 174, 96);
                        dataGridViewHistoria.Columns["Operator"].HeaderText = "Kto zmienił";
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Błąd wczytywania historii: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error); }
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
                        var cmdNotatka = new SqlCommand("INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal) VALUES (@id, @tresc, @kto)", conn, transaction);
                        cmdNotatka.Parameters.AddWithValue("@id", idOdbiorcy);
                        cmdNotatka.Parameters.AddWithValue("@tresc", tresc);
                        cmdNotatka.Parameters.AddWithValue("@kto", operatorID);
                        cmdNotatka.ExecuteNonQuery();

                        var cmdLog = new SqlCommand(@"INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany) VALUES (@id, @typ, @wartosc, @kto, GETDATE())", conn, transaction);
                        cmdLog.Parameters.AddWithValue("@id", idOdbiorcy);
                        cmdLog.Parameters.AddWithValue("@typ", "Dodanie notatki");
                        cmdLog.Parameters.AddWithValue("@wartosc", tresc.Length > 100 ? tresc.Substring(0, 100) + "..." : tresc);
                        cmdLog.Parameters.AddWithValue("@kto", operatorID);
                        cmdLog.ExecuteNonQuery();

                        transaction.Commit();
                        MessageBox.Show("Notatka zostala dodana!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                WczytajNotatki(idOdbiorcy);
                WczytajHistorieZmian(idOdbiorcy);
            }
            catch (Exception ex) { MessageBox.Show($"Błąd dodawania notatki: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        private void WypelnijFiltrPowiatow()
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource; if (dt == null) return;
            var powiaty = dt.AsEnumerable().Select(row => row.Field<string>("Powiat")).Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p).ToArray();
            comboBoxPowiatFilter.Items.Clear();
            comboBoxPowiatFilter.Items.Add("Wszystkie powiaty");
            comboBoxPowiatFilter.Items.AddRange(powiaty);
            comboBoxPowiatFilter.SelectedIndex = 0;
        }
        private void WypelnijFiltrPKD()
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource; if (dt == null) return;
            var wszystkiePKD = dt.AsEnumerable().Where(row => !string.IsNullOrWhiteSpace(row.Field<string>("PKD_Opis"))).Select(row => row.Field<string>("PKD_Opis")).Distinct().ToList();
            var posortowane = new System.Collections.Generic.List<string>();
            foreach (var priorytet in priorytetowePKD) { var znalezione = wszystkiePKD.Where(p => p.Contains(priorytet.Substring(0, Math.Min(30, priorytet.Length)))).ToList(); posortowane.AddRange(znalezione); foreach (var z in znalezione) wszystkiePKD.Remove(z); }
            posortowane.AddRange(wszystkiePKD.OrderBy(x => x));
            comboBoxPKD.Items.Clear();
            comboBoxPKD.Items.Add("Wszystkie Rodzaje");
            foreach (var pkd in posortowane) comboBoxPKD.Items.Add(pkd);
            comboBoxPKD.SelectedIndex = 0;
        }
        private void WypelnijFiltrWoj()
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource; if (dt == null) return;
            var woj = dt.AsEnumerable().Select(row => row.Field<string>("Wojewodztwo")).Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p).ToArray();
            comboBoxWoj.Items.Clear();
            comboBoxWoj.Items.Add("Wszystkie Woj.");
            comboBoxWoj.Items.AddRange(woj);
            comboBoxWoj.SelectedIndex = 0;
        }
        private void ZastosujFiltry(object sender, EventArgs e)
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource; if (dt == null) return;
            var filtry = new System.Collections.Generic.List<string>();
            if (comboBoxStatusFilter.SelectedIndex > 0) filtry.Add($"Status = '{comboBoxStatusFilter.SelectedItem}'");
            if (comboBoxPowiatFilter.SelectedIndex > 0) filtry.Add($"Powiat = '{comboBoxPowiatFilter.SelectedItem}'");
            if (comboBoxPKD.SelectedIndex > 0) filtry.Add($"PKD_Opis = '{comboBoxPKD.SelectedItem}'");
            if (comboBoxWoj.SelectedIndex > 0) filtry.Add($"Wojewodztwo = '{comboBoxWoj.SelectedItem}'");
            dt.DefaultView.RowFilter = string.Join(" AND ", filtry);
        }
        private void dataGridViewOdbiorcy_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dataGridViewOdbiorcy.Rows.Count) return;
            DataGridViewRow row = dataGridViewOdbiorcy.Rows[e.RowIndex];
            string status = row.Cells["StatusColumn"].Value?.ToString() ?? "Do zadzwonienia";
            Color kolorWiersza = status switch { "Do zadzwonienia" => Color.FromArgb(236, 240, 241), "Próba kontaktu" => Color.FromArgb(174, 214, 241), "Nawiązano kontakt" => Color.FromArgb(133, 193, 233), "Zgoda na dalszy kontakt" => Color.FromArgb(169, 223, 191), "Do wysłania oferta" => Color.FromArgb(250, 219, 216), "Nie zainteresowany" => Color.FromArgb(245, 183, 177), "Poprosił o usunięcie" => Color.FromArgb(241, 148, 138), "Błędny rekord (do raportu)" => Color.FromArgb(248, 196, 113), _ => Color.White };
            row.DefaultCellStyle.BackColor = kolorWiersza;
            row.DefaultCellStyle.SelectionBackColor = ControlPaint.Dark(kolorWiersza, 0.1f);
        }
        private void dataGridViewOdbiorcy_CurrentCellDirtyStateChanged(object sender, EventArgs e) { if (dataGridViewOdbiorcy.IsCurrentCellDirty) dataGridViewOdbiorcy.CommitEdit(DataGridViewDataErrorContexts.Commit); }
        private void dataGridViewOdbiorcy_CellEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                var row = dataGridViewOdbiorcy.Rows[e.RowIndex]; if (row.IsNewRow) return;
                DataRowView rowView = row.DataBoundItem as DataRowView; if (rowView == null || rowView["ID"] == DBNull.Value) return;
                int idOdbiorcy = Convert.ToInt32(rowView["ID"]);
                if (aktualnyOdbiorcaID != idOdbiorcy) { WczytajNotatki(idOdbiorcy); WczytajHistorieZmian(idOdbiorcy); aktualnyOdbiorcaID = idOdbiorcy; }
            }
        }
        private void DataGridViewRanking_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (e.ColumnIndex == 0) e.CellStyle.Font = new Font(dataGridViewRanking.Font, FontStyle.Bold);
            else if (dataGridViewRanking.Columns[e.ColumnIndex].Name == "Suma") { e.CellStyle.Font = new Font(dataGridViewRanking.Font, FontStyle.Bold); e.CellStyle.ForeColor = Color.FromArgb(0, 100, 0); }
        }
        private void button2_Click(object sender, EventArgs e) { if (dataGridViewOdbiorcy.CurrentRow != null) { string nazwaFirmy = dataGridViewOdbiorcy.CurrentRow.Cells["Nazwa"].Value?.ToString(); if (!string.IsNullOrWhiteSpace(nazwaFirmy)) { string url = $"https://www.google.com/search?q={Uri.EscapeDataString(nazwaFirmy)}"; System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); } } }
        private void button3_Click(object sender, EventArgs e) { if (dataGridViewOdbiorcy.CurrentRow != null) { string ulica = dataGridViewOdbiorcy.CurrentRow.Cells["Ulica"].Value?.ToString() ?? ""; string miasto = dataGridViewOdbiorcy.CurrentRow.Cells["MIASTO"].Value?.ToString() ?? ""; string kodPocztowy = dataGridViewOdbiorcy.CurrentRow.Cells["KodPocztowy"].Value?.ToString() ?? ""; string adresOdbiorcy = $"{ulica}, {kodPocztowy} {miasto}"; string adresStartowy = "Koziołki 40, 95-061 Dmosin"; if (!string.IsNullOrWhiteSpace(ulica) && !string.IsNullOrWhiteSpace(miasto)) { string url = $"https://www.google.com/maps/dir/{Uri.EscapeDataString(adresStartowy)}/{Uri.EscapeDataString(adresOdbiorcy)}"; System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); } } }
        private void buttonDodajNotatke_Click(object sender, EventArgs e) { if (aktualnyOdbiorcaID > 0 && !string.IsNullOrWhiteSpace(textBoxNotatka.Text)) { DodajNotatke(aktualnyOdbiorcaID, textBoxNotatka.Text); textBoxNotatka.Clear(); } else MessageBox.Show("Wybierz odbiorce i wpisz tresc notatki.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        private void textBoxSzukaj_TextChanged(object sender, EventArgs e) { DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource; if (dt == null) return; string szukanyTekst = textBoxSzukaj.Text.Trim().Replace("'", "''"); dt.DefaultView.RowFilter = string.IsNullOrEmpty(szukanyTekst) ? "" : $"NAZWA LIKE '%{szukanyTekst}%'"; }
        private void comboBoxStatusFilter_SelectedIndexChanged(object sender, EventArgs e) => ZastosujFiltry(sender, e);
        private void comboBoxPowiatFilter_SelectedIndexChanged(object sender, EventArgs e) => ZastosujFiltry(sender, e);
        #endregion
    }

    public class SugestiaKroku { public string TypZadania { get; set; } public string Opis { get; set; } public DateTime Termin { get; set; } public int Priorytet { get; set; } }
}

