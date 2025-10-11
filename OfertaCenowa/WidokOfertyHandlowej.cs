// WidokOfertyHandlowej.cs - WERSJA 2.0
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokOfertyHandlowej : Form
    {
        private readonly string _connHandel;

        // Kontrolki UI
        private ComboBox cboKontrahent;
        private Button btnNowyKontrahent;
        private TextBox txtAdres;
        private TextBox txtNIP;
        private TextBox txtKraj;
        private ComboBox cboHandlowiec;
        private DateTimePicker dtpDataOferty;
        private TextBox txtNumerOferty;
        private DataGridView dgvProdukty;
        private TextBox txtKosztZywca;
        private TextBox txtKosztTransportu;
        private Label lblMarzaBrutto;
        private Label lblMarzaProc;
        private Label lblSumaOferty;
        private ComboBox cboPlatnosc;
        private ComboBox cboIncoterms;
        private NumericUpDown numTerminWaznosci;
        private TextBox txtUwagi;
        private Button btnZapisz;
        private Button btnPDF;
        private Button btnEmail;
        private Button btnPorownaj;
        private Panel panelHeader;
        private Panel panelProdukty;
        private Panel panelKoszty;
        private Panel panelWarunki;
        private RadioButton rbSwiezy;
        private RadioButton rbMrozony;

        private DataTable _dtProdukty;
        private DataView _viewProdukty;
        private List<Kontrahent> _kontrahenci = new();
        private List<Towar> _towary = new();
        private string _aktywnyKatalog = "67095";
        private bool _blokujZmiany = false;

        public WidokOfertyHandlowej()
        {
            InitializeComponent();
            _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        }

        private void InitializeComponent()
        {
            this.Text = "Oferta Handlowa - Ubojnia Drobiu Piórkowscy";
            this.Size = new Size(1600, 950);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(249, 250, 251);
            this.Font = new Font("Segoe UI", 9.5f);
            this.MinimumSize = new Size(1400, 800);

            CreateHeaderPanel();
            CreateProduktyPanel();
            CreateKosztyPanel();
            CreateWarunkiPanel();
            CreateButtonsPanel();

            this.Load += async (s, e) => await LoadInitialData();
        }

        private void CreateHeaderPanel()
        {
            panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                BackColor = Color.White,
                Padding = new Padding(20, 15, 20, 10)
            };

            var lblTitle = new Label
            {
                Text = "OFERTA CENOWA",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 24, 39),
                Location = new Point(20, 15),
                AutoSize = true
            };

            // Linia 1: Kontrahent
            var lblKontrahent = CreateLabel("Kontrahent:", 20, 55);
            cboKontrahent = new ComboBox
            {
                Location = new Point(120, 53),
                Width = 300,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboKontrahent.SelectedIndexChanged += CboKontrahent_SelectedIndexChanged;

            btnNowyKontrahent = CreateSmallButton("+ Nowy", 430, 53, Color.FromArgb(34, 197, 94));
            btnNowyKontrahent.Click += BtnNowyKontrahent_Click;

            var lblAdres = CreateLabel("Adres:", 500, 55);
            txtAdres = new TextBox
            {
                Location = new Point(560, 53),
                Width = 350,
                ReadOnly = true,
                BackColor = Color.FromArgb(243, 244, 246),
                Font = new Font("Segoe UI", 9.5f)
            };

            // Linia 2: NIP, Kraj, Typ produktu
            var lblNIP = CreateLabel("NIP:", 20, 90);
            txtNIP = new TextBox
            {
                Location = new Point(120, 88),
                Width = 200,
                ReadOnly = true,
                BackColor = Color.FromArgb(243, 244, 246),
                Font = new Font("Segoe UI", 9.5f)
            };

            var lblKraj = CreateLabel("Kraj:", 340, 90);
            txtKraj = new TextBox
            {
                Location = new Point(390, 88),
                Width = 120,
                ReadOnly = true,
                BackColor = Color.FromArgb(243, 244, 246),
                Font = new Font("Segoe UI", 9.5f)
            };

            var rbPanel = CreateRadioPanel();
            rbPanel.Location = new Point(530, 85);

            // Linia 3: Handlowiec, Data, Numer
            var lblHandlowiec = CreateLabel("Handlowiec:", 20, 125);
            cboHandlowiec = new ComboBox
            {
                Location = new Point(120, 123),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f)
            };
            cboHandlowiec.Items.AddRange(new[] { "Jola", "Radek", "Ania", "Maja" });

            var lblData = CreateLabel("Data oferty:", 340, 125);
            dtpDataOferty = new DateTimePicker
            {
                Location = new Point(440, 123),
                Width = 150,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Font = new Font("Segoe UI", 9.5f)
            };

            var lblNumer = CreateLabel("Numer:", 610, 125);
            txtNumerOferty = new TextBox
            {
                Location = new Point(680, 123),
                Width = 230,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ReadOnly = true,
                BackColor = Color.FromArgb(254, 249, 195),
                Text = GenerateOfferNumber()
            };

            panelHeader.Controls.AddRange(new Control[] {
                lblTitle, lblKontrahent, cboKontrahent, btnNowyKontrahent, lblAdres, txtAdres,
                lblNIP, txtNIP, lblKraj, txtKraj, rbPanel,
                lblHandlowiec, cboHandlowiec, lblData, dtpDataOferty, lblNumer, txtNumerOferty
            });

            panelHeader.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(229, 231, 235), 1);
                e.Graphics.DrawLine(pen, 0, panelHeader.Height - 1, panelHeader.Width, panelHeader.Height - 1);
            };

            this.Controls.Add(panelHeader);
        }

        private Panel CreateRadioPanel()
        {
            var panel = new Panel
            {
                Size = new Size(180, 32),
                BackColor = Color.FromArgb(249, 250, 251)
            };

            var lblTyp = new Label
            {
                Text = "Typ:",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(75, 85, 99),
                Location = new Point(0, 7),
                AutoSize = true
            };

            rbSwiezy = new RadioButton
            {
                Text = "Świeże",
                Font = new Font("Segoe UI", 9f),
                Location = new Point(45, 5),
                Size = new Size(65, 22),
                Checked = true,
                Cursor = Cursors.Hand
            };

            rbMrozony = new RadioButton
            {
                Text = "Mrożone",
                Font = new Font("Segoe UI", 9f),
                Location = new Point(110, 5),
                Size = new Size(70, 22),
                Cursor = Cursors.Hand
            };

            rbSwiezy.CheckedChanged += RbTypProduktu_CheckedChanged;
            rbMrozony.CheckedChanged += RbTypProduktu_CheckedChanged;

            panel.Controls.AddRange(new Control[] { lblTyp, rbSwiezy, rbMrozony });
            return panel;
        }

        private void RbTypProduktu_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is RadioButton rb && !rb.Checked) return;

            string nowyKatalog = rbSwiezy?.Checked == true ? "67095" : "67153";

            if (nowyKatalog == _aktywnyKatalog) return;

            _aktywnyKatalog = nowyKatalog;
            _viewProdukty.RowFilter = $"Katalog = '{_aktywnyKatalog}'";

            CalculateSummary();
        }

        private void CreateProduktyPanel()
        {
            panelProdukty = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20, 10, 20, 10)
            };

            var lblSection = new Label
            {
                Text = "PRODUKTY I CENY",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(59, 130, 246),
                Location = new Point(20, 10),
                AutoSize = true
            };

            dgvProdukty = new DataGridView
            {
                Location = new Point(20, 40),
                Size = new Size(1540, 420),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersWidth = 25,
                Font = new Font("Segoe UI", 9.5f),
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                EditMode = DataGridViewEditMode.EditOnKeystroke
            };

            ConfigureProductGrid();

            lblSumaOferty = new Label
            {
                Text = "SUMA: 0.00 zł",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(1320, 470),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                AutoSize = true
            };

            panelProdukty.Controls.AddRange(new Control[] { lblSection, dgvProdukty, lblSumaOferty });
            this.Controls.Add(panelProdukty);
        }

        private void ConfigureProductGrid()
        {
            // Krok 1: Utwórz DataTable
            _dtProdukty = new DataTable();
            _dtProdukty.Columns.Add("TowarId", typeof(int));
            _dtProdukty.Columns.Add("Kod", typeof(string));
            _dtProdukty.Columns.Add("Katalog", typeof(string));
            _dtProdukty.Columns.Add("CenaNetto", typeof(decimal));
            _dtProdukty.Columns.Add("Jednostka", typeof(string));
            _dtProdukty.Columns.Add("IloscMin", typeof(decimal));
            _dtProdukty.Columns.Add("Wartosc", typeof(decimal));
            _dtProdukty.Columns.Add("Uwagi", typeof(string));

            // Krok 2: Utwórz widok i przypisz DataSource
            _viewProdukty = new DataView(_dtProdukty);
            _viewProdukty.RowFilter = $"Katalog = '{_aktywnyKatalog}'";

            dgvProdukty.AutoGenerateColumns = true;
            dgvProdukty.DataSource = _viewProdukty;

            // Krok 3: Poczekaj aż kolumny się utworzą i skonfiguruj je
            dgvProdukty.Refresh();

            // Ukryj kolumny techniczne - z sprawdzeniem
            if (dgvProdukty.Columns["TowarId"] != null)
                dgvProdukty.Columns["TowarId"].Visible = false;

            if (dgvProdukty.Columns["Katalog"] != null)
                dgvProdukty.Columns["Katalog"].Visible = false;

            // Konfiguracja kolumn - z sprawdzeniem
            if (dgvProdukty.Columns["Kod"] != null)
            {
                var colKod = dgvProdukty.Columns["Kod"];
                colKod.HeaderText = "Produkt";
                colKod.ReadOnly = true;
                colKod.FillWeight = 180;
                colKod.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
                colKod.DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55);
            }

            if (dgvProdukty.Columns["CenaNetto"] != null)
            {
                var colCena = dgvProdukty.Columns["CenaNetto"];
                colCena.HeaderText = "Cena netto [zł/kg]";
                colCena.DefaultCellStyle.Format = "N2";
                colCena.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                colCena.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                colCena.DefaultCellStyle.ForeColor = Color.FromArgb(59, 130, 246);
                colCena.FillWeight = 90;
            }

            if (dgvProdukty.Columns["Jednostka"] != null)
            {
                var colJedn = dgvProdukty.Columns["Jednostka"];
                colJedn.HeaderText = "Jedn.";
                colJedn.ReadOnly = true;
                colJedn.FillWeight = 50;
                colJedn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            if (dgvProdukty.Columns["IloscMin"] != null)
            {
                var colIlosc = dgvProdukty.Columns["IloscMin"];
                colIlosc.HeaderText = "Min. ilość [kg]";
                colIlosc.DefaultCellStyle.Format = "N0";
                colIlosc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                colIlosc.FillWeight = 90;
            }

            if (dgvProdukty.Columns["Wartosc"] != null)
            {
                var colWartosc = dgvProdukty.Columns["Wartosc"];
                colWartosc.HeaderText = "Wartość [zł]";
                colWartosc.DefaultCellStyle.Format = "N2";
                colWartosc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                colWartosc.ReadOnly = true;
                colWartosc.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
                colWartosc.DefaultCellStyle.ForeColor = Color.FromArgb(16, 185, 129);
                colWartosc.FillWeight = 90;
            }

            if (dgvProdukty.Columns["Uwagi"] != null)
            {
                var colUwagi = dgvProdukty.Columns["Uwagi"];
                colUwagi.HeaderText = "Uwagi";
                colUwagi.FillWeight = 150;
            }

            // Styl nagłówków
            dgvProdukty.EnableHeadersVisualStyles = false;
            dgvProdukty.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            dgvProdukty.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(75, 85, 99);
            dgvProdukty.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
            dgvProdukty.ColumnHeadersHeight = 40;
            dgvProdukty.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            dgvProdukty.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            dgvProdukty.RowTemplate.Height = 32;
            dgvProdukty.DefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 242, 255);
            dgvProdukty.DefaultCellStyle.SelectionForeColor = Color.FromArgb(55, 65, 81);
            dgvProdukty.GridColor = Color.FromArgb(229, 231, 235);

            dgvProdukty.CellValueChanged += DgvProdukty_CellValueChanged;
            dgvProdukty.CurrentCellDirtyStateChanged += DgvProdukty_CurrentCellDirtyStateChanged;
            dgvProdukty.RowPostPaint += DgvProdukty_RowPostPaint;
        }

        private void DgvProdukty_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvProdukty.IsCurrentCellDirty)
            {
                dgvProdukty.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void DgvProdukty_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dgvProdukty.Rows[e.RowIndex];
            var dataRow = (row.DataBoundItem as DataRowView)?.Row;
            if (dataRow == null) return;

            decimal cena = dataRow.Field<decimal?>("CenaNetto") ?? 0m;
            decimal ilosc = dataRow.Field<decimal?>("IloscMin") ?? 0m;

            // Podświetl wiersze z wartościami
            if (cena > 0 && ilosc > 0)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(167, 243, 208);
            }
            else
            {
                row.DefaultCellStyle.BackColor = (e.RowIndex % 2 == 0) ? Color.White : Color.FromArgb(249, 250, 251);
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 242, 255);
            }
        }

        private void DgvProdukty_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_blokujZmiany || e.RowIndex < 0) return;

            var row = (dgvProdukty.Rows[e.RowIndex].DataBoundItem as DataRowView)?.Row;
            if (row == null) return;

            string changedColumn = dgvProdukty.Columns[e.ColumnIndex].Name;

            if (changedColumn == "CenaNetto" || changedColumn == "IloscMin")
            {
                decimal cena = row.Field<decimal?>("CenaNetto") ?? 0m;
                decimal ilosc = row.Field<decimal?>("IloscMin") ?? 0m;
                row["Wartosc"] = cena * ilosc;

                CalculateSummary();
                RecalculateMargin();
            }
        }

        private void CalculateSummary()
        {
            decimal suma = 0;
            foreach (DataRow row in _dtProdukty.Rows)
            {
                decimal wartosc = row.Field<decimal?>("Wartosc") ?? 0m;
                suma += wartosc;
            }
            lblSumaOferty.Text = $"SUMA: {suma:N2} zł";
        }

        private void RecalculateMargin()
        {
            // Znajdź pierwszy produkt z ceną
            decimal cenaNetto = 0m;
            foreach (DataRow row in _dtProdukty.Rows)
            {
                decimal cena = row.Field<decimal?>("CenaNetto") ?? 0m;
                if (cena > 0)
                {
                    cenaNetto = cena;
                    break;
                }
            }

            if (cenaNetto == 0) return;

            decimal kosztZywca = ParseDecimal(txtKosztZywca.Text);
            decimal kosztTransportu = ParseDecimal(txtKosztTransportu.Text);

            decimal kosztCalkowity = kosztZywca + kosztTransportu;
            decimal marzaBrutto = cenaNetto - kosztCalkowity;
            decimal marzaProc = kosztCalkowity > 0 ? (marzaBrutto / kosztCalkowity * 100) : 0;

            lblMarzaBrutto.Text = $"Marża: {marzaBrutto:N2} zł/kg";
            lblMarzaProc.Text = $"Marża: {marzaProc:N1} %";

            // Kolorowanie
            if (marzaProc < 10)
            {
                lblMarzaBrutto.ForeColor = Color.FromArgb(239, 68, 68);
                lblMarzaProc.ForeColor = Color.FromArgb(239, 68, 68);
            }
            else if (marzaProc < 20)
            {
                lblMarzaBrutto.ForeColor = Color.FromArgb(251, 191, 36);
                lblMarzaProc.ForeColor = Color.FromArgb(251, 191, 36);
            }
            else
            {
                lblMarzaBrutto.ForeColor = Color.FromArgb(34, 197, 94);
                lblMarzaProc.ForeColor = Color.FromArgb(34, 197, 94);
            }
        }

        private void CreateKosztyPanel()
        {
            panelKoszty = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 100,
                BackColor = Color.FromArgb(254, 243, 199),
                Padding = new Padding(20)
            };

            var lblSection = new Label
            {
                Text = "KALKULACJA MARŻY",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(217, 119, 6),
                Location = new Point(20, 10),
                AutoSize = true
            };

            var lblZywiec = CreateLabel("Koszt żywca [zł/kg]:", 20, 45);
            txtKosztZywca = new TextBox
            {
                Location = new Point(180, 43),
                Width = 100,
                Text = "6.60",
                TextAlign = HorizontalAlignment.Right,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            txtKosztZywca.TextChanged += (s, e) => RecalculateMargin();

            var lblTransport = CreateLabel("Koszt transportu [zł/kg]:", 320, 45);
            txtKosztTransportu = new TextBox
            {
                Location = new Point(510, 43),
                Width = 100,
                Text = "0.20",
                TextAlign = HorizontalAlignment.Right,
                Font = new Font("Segoe UI", 10f)
            };
            txtKosztTransportu.TextChanged += (s, e) => RecalculateMargin();

            lblMarzaBrutto = new Label
            {
                Text = "Marża: 0.00 zł/kg",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(34, 197, 94),
                Location = new Point(650, 43),
                AutoSize = true
            };

            lblMarzaProc = new Label
            {
                Text = "Marża: 0.00 %",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(34, 197, 94),
                Location = new Point(850, 43),
                AutoSize = true
            };

            panelKoszty.Controls.AddRange(new Control[] {
                lblSection, lblZywiec, txtKosztZywca, lblTransport, txtKosztTransportu,
                lblMarzaBrutto, lblMarzaProc
            });

            this.Controls.Add(panelKoszty);
        }

        private void CreateWarunkiPanel()
        {
            panelWarunki = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 140,
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            var lblSection = new Label
            {
                Text = "WARUNKI HANDLOWE",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(99, 102, 241),
                Location = new Point(20, 10),
                AutoSize = true
            };

            var lblPlatnosc = CreateLabel("Płatność:", 20, 45);
            cboPlatnosc = new ComboBox
            {
                Location = new Point(150, 43),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f)
            };
            cboPlatnosc.Items.AddRange(new[] { "Przelew 7 dni", "Przelew 14 dni", "Przelew 21 dni", "Hermes", "Gotówka" });
            cboPlatnosc.SelectedIndex = 0;

            var lblIncoterms = CreateLabel("Dostawa:", 380, 45);
            cboIncoterms = new ComboBox
            {
                Location = new Point(460, 43),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f)
            };
            cboIncoterms.Items.AddRange(new[] { "EXW", "FCA", "DAP", "DDP", "CPT", "CIP" });
            cboIncoterms.SelectedIndex = 0;

            var lblTermin = CreateLabel("Ważność oferty:", 640, 45);
            numTerminWaznosci = new NumericUpDown
            {
                Location = new Point(770, 43),
                Width = 60,
                Minimum = 1,
                Maximum = 30,
                Value = 3,
                Font = new Font("Segoe UI", 9.5f)
            };

            var lblDni = new Label
            {
                Text = "dni",
                Location = new Point(835, 46),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f)
            };

            var lblUwagi = CreateLabel("Uwagi:", 20, 80);
            txtUwagi = new TextBox
            {
                Location = new Point(150, 78),
                Size = new Size(850, 45),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9f)
            };

            panelWarunki.Controls.AddRange(new Control[] {
                lblSection, lblPlatnosc, cboPlatnosc, lblIncoterms, cboIncoterms,
                lblTermin, numTerminWaznosci, lblDni, lblUwagi, txtUwagi
            });

            panelWarunki.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(229, 231, 235), 1);
                e.Graphics.DrawLine(pen, 0, 0, panelWarunki.Width, 0);
            };

            this.Controls.Add(panelWarunki);
        }

        private void CreateButtonsPanel()
        {
            var panelButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = Color.FromArgb(31, 41, 55),
                Padding = new Padding(20)
            };

            btnZapisz = CreateButton("Zapisz", 20, 15, Color.FromArgb(34, 197, 94));
            btnZapisz.Click += async (s, e) => await SaveOffer();

            btnPDF = CreateButton("PDF", 180, 15, Color.FromArgb(59, 130, 246));
            btnPDF.Click += (s, e) => GeneratePDF();

            btnEmail = CreateButton("Email", 340, 15, Color.FromArgb(99, 102, 241));
            btnEmail.Click += (s, e) => SendEmail();

            btnPorownaj = CreateButton("Porównaj", 500, 15, Color.FromArgb(251, 191, 36));
            btnPorownaj.Click += (s, e) => CompareOffers();

            panelButtons.Controls.AddRange(new Control[] { btnZapisz, btnPDF, btnEmail, btnPorownaj });
            this.Controls.Add(panelButtons);
        }

        private async Task LoadInitialData()
        {
            try
            {
                await LoadKontrahenci();
                await LoadTowary();
                RecalculateMargin();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadKontrahenci()
        {
            const string sql = @"
                SELECT c.Id, c.Shortcut AS Nazwa, c.NIP,
                    poa.Postcode AS KodPocztowy, poa.Street AS Miejscowosc,
                    poa.Country AS Kraj
                FROM [HANDEL].[SSCommon].[STContractors] c
                LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa 
                    ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'
                ORDER BY c.Shortcut";

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                _kontrahenci.Clear();
                while (await rd.ReadAsync())
                {
                    _kontrahenci.Add(new Kontrahent
                    {
                        Id = rd.GetInt32(0),
                        Nazwa = rd.GetString(1),
                        NIP = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        Adres = $"{(rd.IsDBNull(3) ? "" : rd.GetString(3))} {(rd.IsDBNull(4) ? "" : rd.GetString(4))}",
                        Kraj = rd.IsDBNull(5) ? "PL" : rd.GetString(5)
                    });
                }

                cboKontrahent.DataSource = _kontrahenci;
                cboKontrahent.DisplayMember = "Nazwa";
                cboKontrahent.ValueMember = "Id";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania kontrahentów: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadTowary()
        {
            var excludedProducts = new HashSet<string> { "KURCZAK B", "FILET C" };

            var priorityOrder = new Dictionary<string, int>
            {
                { "KURCZAK A", 1 }, { "FILET A", 2 }, { "ĆWIARTKA", 3 }, { "SKRZYDŁO I", 4 },
                { "NOGA", 5 }, { "PAŁKA", 6 }, { "KORPUS", 7 }, { "POLĘDWICZKI", 8 },
                { "SERCE", 9 }, { "WĄTROBA", 10 }, { "ŻOŁĄDKI", 11 }
            };

            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();

            var katalogi = new[] { "67095", "67153" };

            foreach (var katalog in katalogi)
            {
                await using var cmd = new SqlCommand(
                    "SELECT Id, Kod FROM [HANDEL].[HM].[TW] WHERE katalog = @katalog ORDER BY Kod", cn);
                cmd.Parameters.AddWithValue("@katalog", katalog);

                await using var rd = await cmd.ExecuteReaderAsync();

                var tempList = new List<(int Id, string Kod, int Priority, string Katalog)>();

                while (await rd.ReadAsync())
                {
                    var kod = rd.GetString(1);

                    if (excludedProducts.Any(excluded => kod.ToUpper().Contains(excluded)))
                        continue;

                    int priority = int.MaxValue;
                    foreach (var kvp in priorityOrder)
                    {
                        if (kod.ToUpper().Contains(kvp.Key))
                        {
                            priority = kvp.Value;
                            break;
                        }
                    }

                    tempList.Add((rd.GetInt32(0), kod, priority, katalog));
                }

                var sortedList = tempList.OrderBy(x => x.Priority).ThenBy(x => x.Kod).ToList();

                foreach (var item in sortedList)
                {
                    _dtProdukty.Rows.Add(item.Id, item.Kod, item.Katalog, 0m, "kg", 0m, 0m, "");
                }
            }
        }

        private void CboKontrahent_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboKontrahent.SelectedItem is Kontrahent k)
            {
                txtAdres.Text = k.Adres;
                txtNIP.Text = k.NIP;
                txtKraj.Text = k.Kraj;
            }
        }

        private void BtnNowyKontrahent_Click(object sender, EventArgs e)
        {
            using var dialog = new NowyKontrahentDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var nowy = new Kontrahent
                {
                    Id = -1,
                    Nazwa = dialog.Nazwa,
                    NIP = dialog.NIP,
                    Adres = dialog.Adres,
                    Kraj = dialog.Kraj
                };

                _kontrahenci.Add(nowy);
                cboKontrahent.DataSource = null;
                cboKontrahent.DataSource = _kontrahenci;
                cboKontrahent.DisplayMember = "Nazwa";
                cboKontrahent.ValueMember = "Id";
                cboKontrahent.SelectedItem = nowy;
            }
        }

        private async Task SaveOffer()
        {
            if (cboKontrahent.SelectedValue == null)
            {
                MessageBox.Show("Wybierz kontrahenta!", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(cboHandlowiec.Text))
            {
                MessageBox.Show("Wybierz handlowca!", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var pozycje = _dtProdukty.AsEnumerable()
                .Where(r => r.Field<decimal>("CenaNetto") > 0 && r.Field<decimal>("IloscMin") > 0)
                .ToList();

            if (!pozycje.Any())
            {
                MessageBox.Show("Dodaj przynajmniej jeden produkt z ceną i ilością!", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await CreateTableIfNotExists(cn);

                await using var tr = (SqlTransaction)await cn.BeginTransactionAsync();

                foreach (var row in pozycje)
                {
                    decimal cenaNetto = row.Field<decimal>("CenaNetto");
                    decimal kosztZywca = ParseDecimal(txtKosztZywca.Text);
                    decimal kosztTransportu = ParseDecimal(txtKosztTransportu.Text);
                    decimal marzaPLN = cenaNetto - kosztZywca - kosztTransportu;
                    decimal marzaProc = (kosztZywca + kosztTransportu) > 0 ?
                        (marzaPLN / (kosztZywca + kosztTransportu) * 100) : 0;

                    var cmd = new SqlCommand(@"
                        INSERT INTO OfertyHandlowe 
                        (NumerOferty, Data, KontrahentID, Handlowiec, TowarID, CenaNetto, 
                         IloscMin, KosztZywca, KosztTransportu, MarzaPLN, MarzaProc,
                         WarunkiPlatnosci, WarunkiDostawy, TerminWaznosci, Uwagi, 
                         DataWprowadzenia, Uzytkownik)
                        VALUES 
                        (@num, @data, @kont, @hand, @tow, @cena, @ilosc, @zywiec, @trans,
                         @marzaPLN, @marzaProc, @plat, @dost, @term, @uwagi, GETDATE(), @user)",
                        cn, tr);

                    cmd.Parameters.AddWithValue("@num", txtNumerOferty.Text);
                    cmd.Parameters.AddWithValue("@data", dtpDataOferty.Value);
                    cmd.Parameters.AddWithValue("@kont", cboKontrahent.SelectedValue);
                    cmd.Parameters.AddWithValue("@hand", cboHandlowiec.Text);
                    cmd.Parameters.AddWithValue("@tow", row["TowarId"]);
                    cmd.Parameters.AddWithValue("@cena", cenaNetto);
                    cmd.Parameters.AddWithValue("@ilosc", row["IloscMin"]);
                    cmd.Parameters.AddWithValue("@zywiec", kosztZywca);
                    cmd.Parameters.AddWithValue("@trans", kosztTransportu);
                    cmd.Parameters.AddWithValue("@marzaPLN", marzaPLN);
                    cmd.Parameters.AddWithValue("@marzaProc", marzaProc);
                    cmd.Parameters.AddWithValue("@plat", cboPlatnosc.Text);
                    cmd.Parameters.AddWithValue("@dost", cboIncoterms.Text);
                    cmd.Parameters.AddWithValue("@term", numTerminWaznosci.Value);
                    cmd.Parameters.AddWithValue("@uwagi", txtUwagi.Text);
                    cmd.Parameters.AddWithValue("@user", Environment.UserName);

                    await cmd.ExecuteNonQueryAsync();
                }

                await tr.CommitAsync();

                MessageBox.Show($"Oferta {txtNumerOferty.Text} została zapisana!\nLiczba pozycji: {pozycje.Count}",
                    "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);

                txtNumerOferty.Text = GenerateOfferNumber();
                WyczyscFormularz();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WyczyscFormularz()
        {
            _blokujZmiany = true;
            foreach (DataRow r in _dtProdukty.Rows)
            {
                r["CenaNetto"] = 0m;
                r["IloscMin"] = 0m;
                r["Wartosc"] = 0m;
                r["Uwagi"] = "";
            }
            _blokujZmiany = false;
            txtUwagi.Text = "";
            CalculateSummary();
        }

        private async Task CreateTableIfNotExists(SqlConnection cn)
        {
            var checkTableCmd = new SqlCommand(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='OfertyHandlowe' AND xtype='U')
                CREATE TABLE OfertyHandlowe (
                    Id int IDENTITY(1,1) PRIMARY KEY,
                    NumerOferty nvarchar(50),
                    Data datetime,
                    KontrahentID int,
                    Handlowiec nvarchar(50),
                    TowarID int,
                    CenaNetto decimal(10,2),
                    IloscMin decimal(10,2),
                    KosztZywca decimal(10,2),
                    KosztTransportu decimal(10,2),
                    MarzaPLN decimal(10,2),
                    MarzaProc decimal(10,2),
                    WarunkiPlatnosci nvarchar(100),
                    WarunkiDostawy nvarchar(50),
                    TerminWaznosci int,
                    Uwagi nvarchar(max),
                    DataWprowadzenia datetime,
                    Uzytkownik nvarchar(50)
                )", cn);

            await checkTableCmd.ExecuteNonQueryAsync();
        }

        private void GeneratePDF()
        {
            MessageBox.Show("Funkcja generowania PDF będzie dostępna wkrótce!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SendEmail()
        {
            MessageBox.Show("Funkcja wysyłki email będzie dostępna wkrótce!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CompareOffers()
        {
            var porownanie = new WidokPorownanieOfert();
            porownanie.ShowDialog();
        }

        private string GenerateOfferNumber()
        {
            var now = DateTime.Now;
            return $"OFR/{now:yyyy}/{now:MM}/{now:dd}/{now:HHmm}";
        }

        private Label CreateLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(75, 85, 99)
            };
        }

        private Button CreateButton(string text, int x, int y, Color bgColor)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(140, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = bgColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10f),
                Cursor = Cursors.Hand
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.BackColor = ControlPaint.Dark(bgColor, 0.1f);
            btn.MouseLeave += (s, e) => btn.BackColor = bgColor;

            return btn;
        }

        private Button CreateSmallButton(string text, int x, int y, Color bgColor)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(60, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = bgColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8f),
                Cursor = Cursors.Hand
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.BackColor = ControlPaint.Dark(bgColor, 0.1f);
            btn.MouseLeave += (s, e) => btn.BackColor = bgColor;

            return btn;
        }

        private decimal ParseDecimal(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0m;
            text = text.Replace(",", ".");
            if (decimal.TryParse(text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
                return d;
            return 0m;
        }

        private class Kontrahent
        {
            public int Id { get; set; }
            public string Nazwa { get; set; } = "";
            public string NIP { get; set; } = "";
            public string Adres { get; set; } = "";
            public string Kraj { get; set; } = "";
        }

        private class Towar
        {
            public int Id { get; set; }
            public string Kod { get; set; } = "";
            public string Nazwa { get; set; } = "";
        }
    }

    // Dialog dla nowego kontrahenta
    public class NowyKontrahentDialog : Form
    {
        public string Nazwa { get; private set; } = "";
        public string NIP { get; private set; } = "";
        public string Adres { get; private set; } = "";
        public string Kraj { get; private set; } = "PL";

        private TextBox txtNazwa;
        private TextBox txtNIP;
        private TextBox txtAdres;
        private TextBox txtKraj;

        public NowyKontrahentDialog()
        {
            this.Text = "Nowy kontrahent";
            this.Size = new Size(500, 280);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var lblNazwa = new Label { Text = "Nazwa:", Location = new Point(20, 20), AutoSize = true };
            txtNazwa = new TextBox { Location = new Point(120, 18), Width = 340 };

            var lblNIP = new Label { Text = "NIP:", Location = new Point(20, 60), AutoSize = true };
            txtNIP = new TextBox { Location = new Point(120, 58), Width = 200 };

            var lblAdres = new Label { Text = "Adres:", Location = new Point(20, 100), AutoSize = true };
            txtAdres = new TextBox { Location = new Point(120, 98), Width = 340 };

            var lblKraj = new Label { Text = "Kraj:", Location = new Point(20, 140), AutoSize = true };
            txtKraj = new TextBox { Location = new Point(120, 138), Width = 100, Text = "PL" };

            var btnOK = new Button
            {
                Text = "Zapisz",
                Location = new Point(260, 190),
                Size = new Size(90, 35),
                DialogResult = DialogResult.OK
            };
            btnOK.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtNazwa.Text))
                {
                    MessageBox.Show("Podaj nazwę kontrahenta!", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
                Nazwa = txtNazwa.Text.Trim();
                NIP = txtNIP.Text.Trim();
                Adres = txtAdres.Text.Trim();
                Kraj = txtKraj.Text.Trim();
            };

            var btnCancel = new Button
            {
                Text = "Anuluj",
                Location = new Point(360, 190),
                Size = new Size(90, 35),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[] {
                lblNazwa, txtNazwa, lblNIP, txtNIP, lblAdres, txtAdres,
                lblKraj, txtKraj, btnOK, btnCancel
            });
        }
    }
}