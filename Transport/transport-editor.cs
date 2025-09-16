// Plik: Transport/EdytorKursuImproved.cs
// Usprawniony edytor kursu - prosty i funkcjonalny UI

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kalendarz1.Transport.Repozytorium;
using Kalendarz1.Transport.Pakowanie;

namespace Kalendarz1.Transport.Formularze
{
    public partial class EdytorKursuImproved : Form
    {
        private readonly TransportRepozytorium _repozytorium;
        private long? _kursId;
        private readonly string _uzytkownik;
        private Kurs _kurs;
        private List<Ladunek> _ladunki = new List<Ladunek>();
        private List<Kierowca> _kierowcy;
        private List<Pojazd> _pojazdy;

        // Kontrolki nagłówka
        private ComboBox cboKierowca;
        private ComboBox cboPojazd;
        private DateTimePicker dtpData;
        private MaskedTextBox txtGodzWyjazdu;
        private MaskedTextBox txtGodzPowrotu;
        private TextBox txtTrasa;

        // Grid ładunków
        private DataGridView dgvLadunki;

        // Panel dodawania
        private TextBox txtKlient;
        private NumericUpDown nudPojemniki;
        private TextBox txtUwagi;
        private Button btnDodaj;

        // Wskaźnik wypełnienia
        private ProgressBar progressWypelnienie;
        private Label lblWypelnienie;
        private Label lblStatystyki;

        // Przyciski główne
        private Button btnZapisz;
        private Button btnAnuluj;

        public EdytorKursuImproved(TransportRepozytorium repozytorium, DateTime data, string uzytkownik)
            : this(repozytorium, null, data, uzytkownik)
        {
        }

        public EdytorKursuImproved(TransportRepozytorium repozytorium, Kurs kurs, string uzytkownik)
            : this(repozytorium, kurs?.KursID, kurs?.DataKursu, uzytkownik)
        {
            _kurs = kurs;
        }

        private EdytorKursuImproved(TransportRepozytorium repozytorium, long? kursId, DateTime? data, string uzytkownik)
        {
            _repozytorium = repozytorium ?? throw new ArgumentNullException(nameof(repozytorium));
            _kursId = kursId;
            _uzytkownik = uzytkownik ?? Environment.UserName;

            InitializeComponent();
            dtpData.Value = data ?? DateTime.Today;
            _ = LoadDataAsync();
        }

        private void InitializeComponent()
        {
            Text = _kursId.HasValue ? "Edycja kursu transportowego" : "Nowy kurs transportowy";
            Size = new Size(1100, 750);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(240, 242, 247);

            // Dodaj ikonę okna
            try
            {
                Icon = SystemIcons.Application;
            }
            catch { }

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(25),
                BackColor = Color.FromArgb(240, 242, 247)
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); // Nagłówek
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Lista ładunków
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));  // Panel dodawania
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));  // Przyciski

            // ========== NAGŁÓWEK ==========
            mainLayout.Controls.Add(CreateHeaderPanel(), 0, 0);

            // ========== LISTA ŁADUNKÓW ==========
            mainLayout.Controls.Add(CreateLadunkiPanel(), 0, 1);

            // ========== PANEL DODAWANIA ==========
            mainLayout.Controls.Add(CreateAddPanel(), 0, 2);

            // ========== PRZYCISKI ==========
            mainLayout.Controls.Add(CreateButtonsPanel(), 0, 3);

            Controls.Add(mainLayout);
        }

        private Panel CreateHeaderPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(41, 44, 51),
                Padding = new Padding(15)
            };

            // Dodaj zaokrąglone rogi
            panel.Paint += (s, e) =>
            {
                var rect = panel.ClientRectangle;
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    int radius = 8;
                    path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                    path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
                    path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
                    path.CloseFigure();
                    panel.Region = new Region(path);
                }
            };

            // Pierwsza linia
            var lblKierowca = CreateLabel("KIEROWCA:", 20, 20, 90);
            lblKierowca.ForeColor = Color.FromArgb(173, 181, 189);
            lblKierowca.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            cboKierowca = new ComboBox
            {
                Location = new Point(115, 18),
                Size = new Size(230, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                DisplayMember = "PelneNazwisko",
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            var lblPojazd = CreateLabel("POJAZD:", 365, 20, 70);
            lblPojazd.ForeColor = Color.FromArgb(173, 181, 189);
            lblPojazd.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            cboPojazd = new ComboBox
            {
                Location = new Point(440, 18),
                Size = new Size(170, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                DisplayMember = "Opis",
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cboPojazd.SelectedIndexChanged += async (s, e) => await UpdateWypelnienie();

            var lblData = CreateLabel("DATA:", 630, 20, 50);
            lblData.ForeColor = Color.FromArgb(173, 181, 189);
            lblData.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            dtpData = new DateTimePicker
            {
                Location = new Point(685, 18),
                Size = new Size(140, 26),
                Format = DateTimePickerFormat.Short,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                CalendarMonthBackground = Color.FromArgb(52, 56, 64)
            };

            // Druga linia
            var lblGodziny = CreateLabel("GODZINY:", 20, 60, 90);
            lblGodziny.ForeColor = Color.FromArgb(173, 181, 189);
            lblGodziny.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            txtGodzWyjazdu = new MaskedTextBox
            {
                Location = new Point(115, 58),
                Size = new Size(65, 26),
                Mask = "00:00",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Text = "06:00",
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.FromArgb(255, 193, 7),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblDo = CreateLabel("➔", 185, 60, 30);
            lblDo.TextAlign = ContentAlignment.MiddleCenter;
            lblDo.ForeColor = Color.FromArgb(255, 193, 7);
            lblDo.Font = new Font("Segoe UI", 12F);

            txtGodzPowrotu = new MaskedTextBox
            {
                Location = new Point(220, 58),
                Size = new Size(65, 26),
                Mask = "00:00",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Text = "18:00",
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.FromArgb(255, 193, 7),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblTrasa = CreateLabel("TRASA:", 305, 60, 60);
            lblTrasa.ForeColor = Color.FromArgb(173, 181, 189);
            lblTrasa.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            txtTrasa = new TextBox
            {
                Location = new Point(370, 58),
                Size = new Size(455, 26),
                Font = new Font("Segoe UI", 10F),
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtTrasa.GotFocus += (s, e) => txtTrasa.BackColor = Color.FromArgb(62, 66, 74);
            txtTrasa.LostFocus += (s, e) => txtTrasa.BackColor = Color.FromArgb(52, 56, 64);

            // Trzecia linia - wskaźnik wypełnienia
            var panelWypelnienie = new Panel
            {
                Location = new Point(20, 100),
                Size = new Size(805, 40),
                BackColor = Color.FromArgb(33, 37, 43)
            };

            // Zaokrąglone rogi dla panelu wypełnienia
            panelWypelnienie.Paint += (s, e) =>
            {
                var rect = panelWypelnienie.ClientRectangle;
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    int radius = 5;
                    path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                    path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
                    path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
                    path.CloseFigure();
                    panelWypelnienie.Region = new Region(path);
                }
            };

            lblWypelnienie = new Label
            {
                Location = new Point(15, 10),
                Size = new Size(120, 20),
                Text = "WYPEŁNIENIE:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(173, 181, 189)
            };

            progressWypelnienie = new ProgressBar
            {
                Location = new Point(140, 10),
                Size = new Size(420, 22),
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };

            lblStatystyki = new Label
            {
                Location = new Point(570, 10),
                Size = new Size(220, 20),
                Text = "0 pojemników / 0 palet",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 193, 7),
                TextAlign = ContentAlignment.MiddleRight
            };

            panelWypelnienie.Controls.AddRange(new Control[] { lblWypelnienie, progressWypelnienie, lblStatystyki });

            panel.Controls.AddRange(new Control[] {
                lblKierowca, cboKierowca, lblPojazd, cboPojazd, lblData, dtpData,
                lblGodziny, txtGodzWyjazdu, lblDo, txtGodzPowrotu, lblTrasa, txtTrasa,
                panelWypelnienie
            });

            return panel;
        }

        private Panel CreateLadunkiPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 10)
            };

            dgvLadunki = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            // Stylizacja
            dgvLadunki.EnableHeadersVisualStyles = false;
            dgvLadunki.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 252);
            dgvLadunki.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(52, 73, 94);
            dgvLadunki.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgvLadunki.ColumnHeadersHeight = 35;

            dgvLadunki.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            dgvLadunki.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgvLadunki.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);
            dgvLadunki.RowTemplate.Height = 32;
            dgvLadunki.GridColor = Color.FromArgb(236, 240, 241);

            // Menu kontekstowe
            var contextMenu = new ContextMenuStrip();
            var menuUsun = new ToolStripMenuItem("🗑️ Usuń", null, async (s, e) => await UsunLadunek());
            var menuEdytuj = new ToolStripMenuItem("✏️ Edytuj", null, (s, e) => EdytujLadunek());
            contextMenu.Items.AddRange(new[] { menuEdytuj, menuUsun });
            dgvLadunki.ContextMenuStrip = contextMenu;

            panel.Controls.Add(dgvLadunki);
            return panel;
        }

        private Panel CreateAddPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20, 10, 20, 10)
            };

            var lblKlient = CreateLabel("Klient/Kod:", 0, 20, 90);
            txtKlient = new TextBox
            {
                Location = new Point(100, 18),
                Size = new Size(250, 26),
                Font = new Font("Segoe UI", 10F),
                PlaceholderText = "Nazwa klienta lub kod..."
            };

            var lblPojemniki = CreateLabel("Pojemniki:", 370, 20, 80);
            nudPojemniki = new NumericUpDown
            {
                Location = new Point(460, 18),
                Size = new Size(80, 26),
                Font = new Font("Segoe UI", 10F),
                Maximum = 1000,
                Minimum = 0,
                TextAlign = HorizontalAlignment.Center
            };

            var lblUwagi = CreateLabel("Uwagi:", 560, 20, 50);
            txtUwagi = new TextBox
            {
                Location = new Point(620, 18),
                Size = new Size(200, 26),
                Font = new Font("Segoe UI", 10F),
                PlaceholderText = "Opcjonalne..."
            };

            btnDodaj = new Button
            {
                Location = new Point(840, 15),
                Size = new Size(100, 35),
                Text = "➕ Dodaj",
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDodaj.FlatAppearance.BorderSize = 0;
            btnDodaj.Click += async (s, e) => await DodajLadunek();

            // Enter dodaje ładunek
            txtKlient.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await DodajLadunek(); };
            nudPojemniki.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await DodajLadunek(); };
            txtUwagi.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await DodajLadunek(); };

            panel.Controls.AddRange(new Control[] {
                lblKlient, txtKlient, lblPojemniki, nudPojemniki,
                lblUwagi, txtUwagi, btnDodaj
            });

            return panel;
        }

        private Panel CreateButtonsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(33, 37, 43),
                Padding = new Padding(0, 10, 0, 0)
            };

            btnZapisz = new Button
            {
                Size = new Size(140, 45),
                Text = "💾 ZAPISZ",
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.FlatAppearance.MouseOverBackColor = Color.FromArgb(33, 136, 56);
            btnZapisz.Location = new Point(panel.Width - btnZapisz.Width - 170, 10);
            btnZapisz.Click += BtnZapisz_Click;

            // Efekt hover
            btnZapisz.MouseEnter += (s, e) => btnZapisz.BackColor = Color.FromArgb(33, 136, 56);
            btnZapisz.MouseLeave += (s, e) => btnZapisz.BackColor = Color.FromArgb(40, 167, 69);

            btnAnuluj = new Button
            {
                Size = new Size(140, 45),
                Text = "❌ ANULUJ",
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11F),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;
            btnAnuluj.FlatAppearance.MouseOverBackColor = Color.FromArgb(90, 98, 104);
            btnAnuluj.Location = new Point(panel.Width - btnAnuluj.Width - 20, 10);
            btnAnuluj.Click += (s, e) => Close();

            // Efekt hover
            btnAnuluj.MouseEnter += (s, e) => btnAnuluj.BackColor = Color.FromArgb(90, 98, 104);
            btnAnuluj.MouseLeave += (s, e) => btnAnuluj.BackColor = Color.FromArgb(108, 117, 125);

            panel.Controls.AddRange(new Control[] { btnZapisz, btnAnuluj });

            // Obsługa zmiany rozmiaru
            panel.Resize += (s, e) => {
                btnAnuluj.Location = new Point(panel.Width - btnAnuluj.Width - 20, 10);
                btnZapisz.Location = new Point(panel.Width - btnZapisz.Width - 170, 10);
            };

            return panel;
        }

        private Label CreateLabel(string text, int x, int y, int width)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 23),
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(52, 73, 94),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private async Task LoadDataAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                _kierowcy = await _repozytorium.PobierzKierowcowAsync(true);
                _pojazdy = await _repozytorium.PobierzPojazdyAsync(true);

                cboKierowca.DataSource = _kierowcy;
                cboPojazd.DataSource = _pojazdy;

                if (_kursId.HasValue && _kursId.Value > 0)
                {
                    await LoadKursData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private async Task LoadKursData()
        {
            if (!_kursId.HasValue) return;

            var kursy = await _repozytorium.PobierzKursyPoDacieAsync(dtpData.Value);
            _kurs = kursy.FirstOrDefault(k => k.KursID == _kursId.Value);

            if (_kurs == null)
            {
                MessageBox.Show("Nie znaleziono kursu.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }

            // Ustaw wartości
            dtpData.Value = _kurs.DataKursu;
            cboKierowca.SelectedItem = _kierowcy.FirstOrDefault(k => k.KierowcaID == _kurs.KierowcaID);
            cboPojazd.SelectedItem = _pojazdy.FirstOrDefault(p => p.PojazdID == _kurs.PojazdID);

            if (_kurs.GodzWyjazdu.HasValue)
                txtGodzWyjazdu.Text = _kurs.GodzWyjazdu.Value.ToString(@"hh\:mm");
            if (_kurs.GodzPowrotu.HasValue)
                txtGodzPowrotu.Text = _kurs.GodzPowrotu.Value.ToString(@"hh\:mm");

            txtTrasa.Text = _kurs.Trasa ?? "";

            await LoadLadunki();
        }

        private async Task LoadLadunki()
        {
            if (!_kursId.HasValue) return;

            _ladunki = await _repozytorium.PobierzLadunkiAsync(_kursId.Value);

            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(long));
            dt.Columns.Add("Lp.", typeof(int));
            dt.Columns.Add("Klient", typeof(string));
            dt.Columns.Add("Pojemniki", typeof(int));
            dt.Columns.Add("Uwagi", typeof(string));

            int lp = 1;
            foreach (var ladunek in _ladunki.OrderBy(l => l.Kolejnosc))
            {
                dt.Rows.Add(
                    ladunek.LadunekID,
                    lp++,
                    ladunek.KodKlienta ?? "",
                    ladunek.PojemnikiE2,
                    ladunek.Uwagi ?? ""
                );
            }

            dgvLadunki.DataSource = dt;

            if (dgvLadunki.Columns["ID"] != null)
                dgvLadunki.Columns["ID"].Visible = false;
            if (dgvLadunki.Columns["Lp."] != null)
                dgvLadunki.Columns["Lp."].Width = 50;

            await UpdateWypelnienie();
        }

        private async Task UpdateWypelnienie()
        {
            try
            {
                if (cboPojazd.SelectedItem is not Pojazd pojazd)
                {
                    progressWypelnienie.Value = 0;
                    lblStatystyki.Text = "0 pojemników / 0 palet";
                    return;
                }

                int sumaPojemnikow = _ladunki?.Sum(l => l.PojemnikiE2) ?? 0;
                int paletyNominal = (int)Math.Ceiling(sumaPojemnikow / 36.0);
                int paletyPojazdu = pojazd.PaletyH1;
                int procent = paletyPojazdu > 0 ? (int)(paletyNominal * 100.0 / paletyPojazdu) : 0;

                progressWypelnienie.Value = Math.Min(100, procent);
                lblStatystyki.Text = $"{sumaPojemnikow} pojemników / {paletyNominal} palet";

                // Kolorowanie
                if (procent > 100)
                {
                    progressWypelnienie.ForeColor = Color.Red;
                    lblWypelnienie.ForeColor = Color.Red;
                }
                else if (procent > 90)
                {
                    progressWypelnienie.ForeColor = Color.Orange;
                    lblWypelnienie.ForeColor = Color.Orange;
                }
                else
                {
                    progressWypelnienie.ForeColor = Color.Green;
                    lblWypelnienie.ForeColor = Color.Green;
                }

                lblWypelnienie.Text = $"Wypełnienie: {procent}%";
            }
            catch
            {
                // Ignoruj błędy aktualizacji
            }
        }

        private async Task DodajLadunek()
        {
            if (string.IsNullOrWhiteSpace(txtKlient.Text))
            {
                MessageBox.Show("Podaj nazwę klienta lub kod.", "Brak danych",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtKlient.Focus();
                return;
            }

            if (nudPojemniki.Value <= 0)
            {
                MessageBox.Show("Podaj liczbę pojemników.", "Brak danych",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nudPojemniki.Focus();
                return;
            }

            // Jeśli nowy kurs - najpierw zapisz
            if (!_kursId.HasValue || _kursId.Value <= 0)
            {
                await SaveKurs();
                if (!_kursId.HasValue || _kursId.Value <= 0) return;
            }

            var ladunek = new Ladunek
            {
                KursID = _kursId.Value,
                KodKlienta = txtKlient.Text.Trim(),
                PojemnikiE2 = (int)nudPojemniki.Value,
                Uwagi = string.IsNullOrWhiteSpace(txtUwagi.Text) ? null : txtUwagi.Text.Trim()
            };

            await _repozytorium.DodajLadunekAsync(ladunek);

            // Wyczyść formularz
            txtKlient.Clear();
            nudPojemniki.Value = 0;
            txtUwagi.Clear();
            txtKlient.Focus();

            await LoadLadunki();
        }

        private async Task UsunLadunek()
        {
            if (dgvLadunki.CurrentRow == null) return;

            var ladunekId = Convert.ToInt64(dgvLadunki.CurrentRow.Cells["ID"].Value);
            await _repozytorium.UsunLadunekAsync(ladunekId);
            await LoadLadunki();
        }

        private void EdytujLadunek()
        {
            if (dgvLadunki.CurrentRow == null) return;

            // Wypełnij formularz danymi do edycji
            var row = dgvLadunki.CurrentRow;
            txtKlient.Text = row.Cells["Klient"].Value?.ToString() ?? "";
            nudPojemniki.Value = Convert.ToInt32(row.Cells["Pojemniki"].Value ?? 0);
            txtUwagi.Text = row.Cells["Uwagi"].Value?.ToString() ?? "";

            // Usuń stary i fokus na dodanie nowego
            _ = UsunLadunek();
        }

        private async void BtnZapisz_Click(object sender, EventArgs e)
        {
            if (cboKierowca.SelectedItem == null)
            {
                MessageBox.Show("Wybierz kierowcę.", "Brak danych",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cboPojazd.SelectedItem == null)
            {
                MessageBox.Show("Wybierz pojazd.", "Brak danych",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                await SaveKurs();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private async Task SaveKurs()
        {
            var kierowca = cboKierowca.SelectedItem as Kierowca;
            var pojazd = cboPojazd.SelectedItem as Pojazd;

            TimeSpan? godzWyjazdu = null;
            TimeSpan? godzPowrotu = null;

            if (TimeSpan.TryParse(txtGodzWyjazdu.Text, out var gw))
                godzWyjazdu = gw;
            if (TimeSpan.TryParse(txtGodzPowrotu.Text, out var gp))
                godzPowrotu = gp;

            var kurs = new Kurs
            {
                KursID = _kursId ?? 0,
                DataKursu = dtpData.Value.Date,
                KierowcaID = kierowca.KierowcaID,
                PojazdID = pojazd.PojazdID,
                Trasa = string.IsNullOrWhiteSpace(txtTrasa.Text) ? null : txtTrasa.Text.Trim(),
                GodzWyjazdu = godzWyjazdu,
                GodzPowrotu = godzPowrotu,
                Status = "Planowany",
                PlanE2NaPalete = 36
            };

            if (_kursId.HasValue && _kursId.Value > 0)
            {
                await _repozytorium.AktualizujNaglowekKursuAsync(kurs, _uzytkownik);
            }
            else
            {
                _kursId = await _repozytorium.DodajKursAsync(kurs, _uzytkownik);
                Text = "Edycja kursu";
            }
        }
    }
}