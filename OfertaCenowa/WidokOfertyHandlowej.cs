// WidokOfertyHandlowej.cs
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokOfertyHandlowej : Form
    {
        private readonly string _connHandel;
        private readonly CultureInfo _pl = new("pl-PL");

        // Kontrolki UI
        private ComboBox cboKontrahent;
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

        private DataTable _dtProdukty;
        private List<Kontrahent> _kontrahenci = new();
        private List<Towar> _towary = new();

        public WidokOfertyHandlowej()
        {
            InitializeComponent();
            _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
            InitializeDataTables();
        }

        private void InitializeComponent()
        {
            this.Text = "📊 Oferta Handlowa - Ubojnia Drobiu Piórkowscy";
            this.Size = new Size(1400, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(249, 250, 251);
            this.Font = new Font("Segoe UI", 9.5f);

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
                Height = 200,
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            var lblTitle = new Label
            {
                Text = "📋 OFERTA CENOWA",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 24, 39),
                Location = new Point(20, 15),
                AutoSize = true
            };

            // Sekcja kontrahenta
            var lblKontrahent = CreateLabel("👤 Kontrahent:", 20, 60);
            cboKontrahent = new ComboBox
            {
                Location = new Point(150, 58),
                Width = 350,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboKontrahent.SelectedIndexChanged += CboKontrahent_SelectedIndexChanged;

            var lblAdres = CreateLabel("📍 Adres:", 520, 60);
            txtAdres = new TextBox
            {
                Location = new Point(600, 58),
                Width = 350,
                ReadOnly = true,
                BackColor = Color.FromArgb(243, 244, 246)
            };

            var lblNIP = CreateLabel("💳 NIP:", 20, 95);
            txtNIP = new TextBox
            {
                Location = new Point(150, 93),
                Width = 200,
                ReadOnly = true,
                BackColor = Color.FromArgb(243, 244, 246)
            };

            var lblKraj = CreateLabel("🌍 Kraj:", 370, 95);
            txtKraj = new TextBox
            {
                Location = new Point(430, 93),
                Width = 150,
                ReadOnly = true,
                BackColor = Color.FromArgb(243, 244, 246)
            };

            // Sekcja danych oferty
            var lblHandlowiec = CreateLabel("👔 Handlowiec:", 20, 130);
            cboHandlowiec = new ComboBox
            {
                Location = new Point(150, 128),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboHandlowiec.Items.AddRange(new[] { "Jola", "Radek", "Ania", "Maja" });

            var lblData = CreateLabel("📅 Data oferty:", 370, 130);
            dtpDataOferty = new DateTimePicker
            {
                Location = new Point(480, 128),
                Width = 150,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today
            };

            var lblNumer = CreateLabel("🔢 Numer:", 650, 130);
            txtNumerOferty = new TextBox
            {
                Location = new Point(750, 128),
                Width = 200,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ReadOnly = true,
                BackColor = Color.FromArgb(254, 249, 195),
                Text = GenerateOfferNumber()
            };

            panelHeader.Controls.AddRange(new Control[] {
                lblTitle, lblKontrahent, cboKontrahent, lblAdres, txtAdres,
                lblNIP, txtNIP, lblKraj, txtKraj, lblHandlowiec, cboHandlowiec,
                lblData, dtpDataOferty, lblNumer, txtNumerOferty
            });

            this.Controls.Add(panelHeader);
        }

        private void CreateProduktyPanel()
        {
            panelProdukty = new Panel
            {
                Dock = DockStyle.Top,
                Height = 300,
                BackColor = Color.White,
                Padding = new Padding(20, 10, 20, 10)
            };

            var lblSection = new Label
            {
                Text = "🥩 PRODUKTY",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(59, 130, 246),
                Location = new Point(20, 10),
                AutoSize = true
            };

            dgvProdukty = new DataGridView
            {
                Location = new Point(20, 40),
                Size = new Size(1340, 200),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersWidth = 25,
                Font = new Font("Segoe UI", 9.5f)
            };

            ConfigureProductGrid();

            lblSumaOferty = new Label
            {
                Text = "💰 SUMA: 0.00 zł",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(1100, 250),
                AutoSize = true
            };

            panelProdukty.Controls.AddRange(new Control[] { lblSection, dgvProdukty, lblSumaOferty });
            this.Controls.Add(panelProdukty);
        }

        private void CreateKosztyPanel()
        {
            panelKoszty = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                BackColor = Color.FromArgb(254, 243, 199),
                Padding = new Padding(20)
            };

            var lblSection = new Label
            {
                Text = "📊 KALKULACJA MARŻY",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(217, 119, 6),
                Location = new Point(20, 10),
                AutoSize = true
            };

            var lblZywiec = CreateLabel("🐔 Koszt żywca [zł/kg]:", 20, 45);
            txtKosztZywca = new TextBox
            {
                Location = new Point(180, 43),
                Width = 100,
                Text = "6.60",
                TextAlign = HorizontalAlignment.Right,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            txtKosztZywca.TextChanged += RecalculateMargin;

            var lblTransport = CreateLabel("🚚 Koszt transportu [zł/kg]:", 320, 45);
            txtKosztTransportu = new TextBox
            {
                Location = new Point(510, 43),
                Width = 100,
                Text = "0.20",
                TextAlign = HorizontalAlignment.Right,
                Font = new Font("Segoe UI", 10f)
            };
            txtKosztTransportu.TextChanged += RecalculateMargin;

            lblMarzaBrutto = new Label
            {
                Text = "💵 Marża: 0.00 zł/kg",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(34, 197, 94),
                Location = new Point(20, 85),
                AutoSize = true
            };

            lblMarzaProc = new Label
            {
                Text = "📈 Marża: 0.00 %",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(34, 197, 94),
                Location = new Point(220, 85),
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
                Dock = DockStyle.Top,
                Height = 180,
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            var lblSection = new Label
            {
                Text = "📝 WARUNKI HANDLOWE",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(99, 102, 241),
                Location = new Point(20, 10),
                AutoSize = true
            };

            var lblPlatnosc = CreateLabel("💳 Płatność:", 20, 45);
            cboPlatnosc = new ComboBox
            {
                Location = new Point(150, 43),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboPlatnosc.Items.AddRange(new[] { "Przelew 7 dni", "Przelew 14 dni", "Przelew 21 dni", "Hermes", "Gotówka" });
            cboPlatnosc.SelectedIndex = 0;

            var lblIncoterms = CreateLabel("🚛 Warunki dostawy:", 380, 45);
            cboIncoterms = new ComboBox
            {
                Location = new Point(520, 43),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboIncoterms.Items.AddRange(new[] { "EXW", "FCA", "DAP", "DDP", "CPT", "CIP" });
            cboIncoterms.SelectedIndex = 0;

            var lblTermin = CreateLabel("⏰ Ważność oferty:", 700, 45);
            numTerminWaznosci = new NumericUpDown
            {
                Location = new Point(840, 43),
                Width = 60,
                Minimum = 1,
                Maximum = 30,
                Value = 3
            };

            var lblDni = new Label
            {
                Text = "dni",
                Location = new Point(905, 46),
                AutoSize = true
            };

            var lblUwagi = CreateLabel("📌 Uwagi:", 20, 80);
            txtUwagi = new TextBox
            {
                Location = new Point(150, 78),
                Size = new Size(1000, 70),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };

            panelWarunki.Controls.AddRange(new Control[] {
                lblSection, lblPlatnosc, cboPlatnosc, lblIncoterms, cboIncoterms,
                lblTermin, numTerminWaznosci, lblDni, lblUwagi, txtUwagi
            });

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

            btnZapisz = CreateButton("💾 Zapisz", 20, 15, Color.FromArgb(34, 197, 94));
            btnZapisz.Click += async (s, e) => await SaveOffer();

            btnPDF = CreateButton("📄 PDF", 180, 15, Color.FromArgb(59, 130, 246));
            btnPDF.Click += (s, e) => GeneratePDF();

            btnEmail = CreateButton("✉️ Email", 340, 15, Color.FromArgb(99, 102, 241));
            btnEmail.Click += (s, e) => SendEmail();

            btnPorownaj = CreateButton("📊 Porównaj", 500, 15, Color.FromArgb(251, 191, 36));
            btnPorownaj.Click += (s, e) => CompareOffers();

            panelButtons.Controls.AddRange(new Control[] { btnZapisz, btnPDF, btnEmail, btnPorownaj });
            this.Controls.Add(panelButtons);
        }

        private void ConfigureProductGrid()
        {
            _dtProdukty = new DataTable();
            _dtProdukty.Columns.Add("TowarId", typeof(int));
            _dtProdukty.Columns.Add("Nazwa", typeof(string));
            _dtProdukty.Columns.Add("CenaNetto", typeof(decimal));
            _dtProdukty.Columns.Add("Jednostka", typeof(string));
            _dtProdukty.Columns.Add("IloscMin", typeof(decimal));
            _dtProdukty.Columns.Add("Wartosc", typeof(decimal));
            _dtProdukty.Columns.Add("Uwagi", typeof(string));

            dgvProdukty.DataSource = _dtProdukty;

            dgvProdukty.Columns["TowarId"].Visible = false;
            dgvProdukty.Columns["Nazwa"].HeaderText = "🥩 Produkt";
            dgvProdukty.Columns["Nazwa"].Width = 300;
            dgvProdukty.Columns["CenaNetto"].HeaderText = "💰 Cena netto [zł/kg]";
            dgvProdukty.Columns["CenaNetto"].DefaultCellStyle.Format = "N2";
            dgvProdukty.Columns["CenaNetto"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dgvProdukty.Columns["Jednostka"].HeaderText = "📏 Jedn.";
            dgvProdukty.Columns["Jednostka"].Width = 80;
            dgvProdukty.Columns["IloscMin"].HeaderText = "📦 Min. ilość";
            dgvProdukty.Columns["IloscMin"].DefaultCellStyle.Format = "N0";
            dgvProdukty.Columns["IloscMin"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dgvProdukty.Columns["Wartosc"].HeaderText = "💵 Wartość";
            dgvProdukty.Columns["Wartosc"].DefaultCellStyle.Format = "N2";
            dgvProdukty.Columns["Wartosc"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dgvProdukty.Columns["Wartosc"].ReadOnly = true;
            dgvProdukty.Columns["Uwagi"].HeaderText = "📝 Uwagi";

            dgvProdukty.CellValueChanged += DgvProdukty_CellValueChanged;
            dgvProdukty.EditingControlShowing += DgvProdukty_EditingControlShowing;

            // Styl nagłówków
            dgvProdukty.EnableHeadersVisualStyles = false;
            dgvProdukty.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            dgvProdukty.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(75, 85, 99);
            dgvProdukty.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
            dgvProdukty.ColumnHeadersHeight = 40;

            dgvProdukty.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            dgvProdukty.RowTemplate.Height = 30;
        }

        private void DgvProdukty_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dgvProdukty.CurrentCell.ColumnIndex == dgvProdukty.Columns["Nazwa"].Index)
            {
                if (e.Control is ComboBox combo)
                {
                    combo.DropDownStyle = ComboBoxStyle.DropDownList;
                    combo.DataSource = _towary.Select(t => t.Nazwa).ToList();
                }
            }
        }

        private void DgvProdukty_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dgvProdukty.Rows[e.RowIndex];
            if (row.Cells["CenaNetto"].Value != null && row.Cells["IloscMin"].Value != null)
            {
                decimal cena = Convert.ToDecimal(row.Cells["CenaNetto"].Value);
                decimal ilosc = Convert.ToDecimal(row.Cells["IloscMin"].Value);
                row.Cells["Wartosc"].Value = cena * ilosc;

                CalculateSummary();
                RecalculateMargin(null, null);
            }
        }

        private void CalculateSummary()
        {
            decimal suma = 0;
            foreach (DataGridViewRow row in dgvProdukty.Rows)
            {
                if (row.Cells["Wartosc"].Value != null)
                {
                    suma += Convert.ToDecimal(row.Cells["Wartosc"].Value);
                }
            }
            lblSumaOferty.Text = $"💰 SUMA: {suma:N2} zł";
        }

        private void RecalculateMargin(object sender, EventArgs e)
        {
            if (dgvProdukty.CurrentRow == null ||
                dgvProdukty.CurrentRow.Cells["CenaNetto"].Value == null) return;

            decimal cenaNetto = Convert.ToDecimal(dgvProdukty.CurrentRow.Cells["CenaNetto"].Value);
            decimal kosztZywca = ParseDecimal(txtKosztZywca.Text);
            decimal kosztTransportu = ParseDecimal(txtKosztTransportu.Text);

            decimal kosztCalkowity = kosztZywca + kosztTransportu;
            decimal marzaBrutto = cenaNetto - kosztCalkowity;
            decimal marzaProc = kosztCalkowity > 0 ? (marzaBrutto / kosztCalkowity * 100) : 0;

            lblMarzaBrutto.Text = $"💵 Marża: {marzaBrutto:N2} zł/kg";
            lblMarzaProc.Text = $"📈 Marża: {marzaProc:N1} %";

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

        private async Task LoadInitialData()
        {
            await LoadKontrahenci();
            await LoadTowary();

            // Dodaj przykładowe produkty
            _dtProdukty.Rows.Add(66443, "KURCZAK A (TUSZKA)", 9.60m, "kg", 1000m, 9600m, "Świeży, chłodzony");
            _dtProdukty.Rows.Add(66818, "ĆWIARTKA", 10.20m, "kg", 500m, 5100m, "Pakowane próżniowo");
            _dtProdukty.Rows.Add(66445, "FILET A", 18.50m, "kg", 300m, 5550m, "Bez skóry");

            CalculateSummary();
            RecalculateMargin(null, null);
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
            const string sql = @"
                SELECT Id, Kod 
                FROM [HANDEL].[HM].[TW] 
                WHERE katalog IN ('67095', '67153')
                  AND Kod NOT LIKE '%KURCZAK B%' 
                  AND Kod NOT LIKE '%FILET C%'
                ORDER BY Kod";

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                _towary.Clear();
                while (await rd.ReadAsync())
                {
                    _towary.Add(new Towar
                    {
                        Id = rd.GetInt32(0),
                        Kod = rd.GetInt32(0).ToString(),
                        Nazwa = rd.GetString(1)
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania towarów: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            if (_dtProdukty.Rows.Count == 0)
            {
                MessageBox.Show("Dodaj przynajmniej jeden produkt!", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                // Sprawdź czy tabela istnieje, jeśli nie - utwórz
                await CreateTableIfNotExists(cn);

                await using var tr = (SqlTransaction)await cn.BeginTransactionAsync();

                foreach (DataRow row in _dtProdukty.Rows)
                {
                    if (row.RowState == DataRowState.Deleted) continue;

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

                    decimal cenaNetto = Convert.ToDecimal(row["CenaNetto"]);
                    decimal kosztZywca = ParseDecimal(txtKosztZywca.Text);
                    decimal kosztTransportu = ParseDecimal(txtKosztTransportu.Text);
                    decimal marzaPLN = cenaNetto - kosztZywca - kosztTransportu;
                    decimal marzaProc = (kosztZywca + kosztTransportu) > 0 ?
                        (marzaPLN / (kosztZywca + kosztTransportu) * 100) : 0;

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

                MessageBox.Show("✅ Oferta została zapisana!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Wygeneruj nowy numer
                txtNumerOferty.Text = GenerateOfferNumber();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            // Tu implementacja generowania PDF
            MessageBox.Show("📄 Funkcja generowania PDF będzie dostępna wkrótce!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SendEmail()
        {
            // Tu implementacja wysyłki email
            MessageBox.Show("✉️ Funkcja wysyłki email będzie dostępna wkrótce!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CompareOffers()
        {
            // Tu implementacja porównania ofert
            MessageBox.Show("📊 Funkcja porównania ofert będzie dostępna wkrótce!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string GenerateOfferNumber()
        {
            var now = DateTime.Now;
            return $"OFR/{now.Year}/{now.Month:D2}/{now.Day:D2}/{now.Hour:D2}{now.Minute:D2}";
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

        private decimal ParseDecimal(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0m;
            if (decimal.TryParse(text, NumberStyles.Number, _pl, out var d)) return d;
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d2)) return d2;
            return 0m;
        }

        private void InitializeDataTables()
        {
            // Inicjalizacja struktur danych
        }

        // Klasy pomocnicze
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
}