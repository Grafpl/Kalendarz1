// Plik: Transport/EdytorKursuImproved.cs
// Wersja z maksymalizowanym oknem, adresami i checkboxem "40 E2"

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kalendarz1.Transport.Repozytorium;
using Kalendarz1.Transport.Pakowanie;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Transport.Formularze
{
    public partial class EdytorKursuImproved : Form
    {
        private readonly TransportRepozytorium _repozytorium;
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private long? _kursId;
        private readonly string _uzytkownik;
        private Kurs _kurs;
        private List<Ladunek> _ladunki = new List<Ladunek>();
        private List<Kierowca> _kierowcy;
        private List<Pojazd> _pojazdy;
        private List<ZamowienieDoTransportu> _wolneZamowienia = new List<ZamowienieDoTransportu>();

        // Kontrolki nagłówka
        private ComboBox cboKierowca;
        private ComboBox cboPojazd;
        private DateTimePicker dtpData;
        private MaskedTextBox txtGodzWyjazdu;
        private MaskedTextBox txtGodzPowrotu;
        private TextBox txtTrasa;
        private CheckBox chk40E2; // Nowy checkbox

        // Grid ładunków
        private DataGridView dgvLadunki;

        // Panel zamówień
        private DataGridView dgvWolneZamowienia;
        private Button btnDodajZamowienie;
        private Label lblZamowieniaInfo;

        // Panel ręcznego dodawania
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

        // Klasa pomocnicza dla zamówień
        public class ZamowienieDoTransportu
        {
            public int ZamowienieId { get; set; }
            public int KlientId { get; set; }
            public string KlientNazwa { get; set; } = "";
            public decimal IloscKg { get; set; }
            public int Pojemniki => (int)Math.Ceiling(IloscKg / 15m);
            public DateTime DataPrzyjazdu { get; set; }
            public string GodzinaStr => DataPrzyjazdu.ToString("HH:mm");
            public string Status { get; set; } = "Nowe";
            public string Handlowiec { get; set; } = "";
            public string Adres { get; set; } = "";
        }

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
            Size = new Size(1600, 1000); // Zwiększone rozmiary
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized; // Maksymalizowane okno
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(240, 242, 247);

            try
            {
                Icon = SystemIcons.Application;
            }
            catch { }

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(25),
                BackColor = Color.FromArgb(240, 242, 247)
            };

            // Podział na lewą i prawą część
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); // Lewa strona - kurs
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); // Prawa strona - zamówienia (więcej miejsca)

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180)); // Zwiększony nagłówek
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Lista ładunków / zamówień
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));  // Panel dodawania
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));  // Przyciski

            // ========== NAGŁÓWEK (rozciągnięty na 2 kolumny) ==========
            var headerPanel = CreateHeaderPanel();
            mainLayout.Controls.Add(headerPanel, 0, 0);
            mainLayout.SetColumnSpan(headerPanel, 2);

            // ========== LEWA STRONA - ŁADUNKI ==========
            mainLayout.Controls.Add(CreateLadunkiPanel(), 0, 1);
            mainLayout.Controls.Add(CreateAddPanel(), 0, 2);

            // ========== PRAWA STRONA - ZAMÓWIENIA ==========
            mainLayout.Controls.Add(CreateZamowieniaPanel(), 1, 1);
            mainLayout.SetRowSpan(mainLayout.GetControlFromPosition(1, 1), 2);

            // ========== PRZYCISKI (rozciągnięte na 2 kolumny) ==========
            var buttonsPanel = CreateButtonsPanel();
            mainLayout.Controls.Add(buttonsPanel, 0, 3);
            mainLayout.SetColumnSpan(buttonsPanel, 2);

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
            dtpData.ValueChanged += async (s, e) => await LoadWolneZamowienia();

            // NOWY CHECKBOX 40 E2
            chk40E2 = new CheckBox
            {
                Location = new Point(850, 20),
                Size = new Size(120, 26),
                Text = "40 E2 MAX",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 193, 7),
                BackColor = Color.Transparent,
                UseVisualStyleBackColor = false
            };
            chk40E2.CheckedChanged += async (s, e) => await UpdateWypelnienie();

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

            var lblDo = CreateLabel("→", 185, 60, 30);
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
                Size = new Size(700, 26),
                Font = new Font("Segoe UI", 10F),
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Trasa zostanie uzupełniona automatycznie na podstawie klientów..."
            };

            // Trzecia linia - wskaźnik wypełnienia
            var panelWypelnienie = new Panel
            {
                Location = new Point(20, 110),
                Size = new Size(1200, 50),
                BackColor = Color.FromArgb(33, 37, 43)
            };

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
                Location = new Point(15, 15),
                Size = new Size(120, 20),
                Text = "WYPEŁNIENIE:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(173, 181, 189)
            };

            progressWypelnienie = new ProgressBar
            {
                Location = new Point(140, 15),
                Size = new Size(700, 22),
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };

            lblStatystyki = new Label
            {
                Location = new Point(850, 15),
                Size = new Size(330, 20),
                Text = "0 pojemników / 0 palet",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 193, 7),
                TextAlign = ContentAlignment.MiddleRight
            };

            panelWypelnienie.Controls.AddRange(new Control[] { lblWypelnienie, progressWypelnienie, lblStatystyki });

            panel.Controls.AddRange(new Control[] {
                lblKierowca, cboKierowca, lblPojazd, cboPojazd, lblData, dtpData, chk40E2,
                lblGodziny, txtGodzWyjazdu, lblDo, txtGodzPowrotu, lblTrasa, txtTrasa,
                panelWypelnienie
            });

            return panel;
        }

        private Panel CreateZamowieniaPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            lblZamowieniaInfo = new Label
            {
                Text = "WOLNE ZAMÓWIENIA NA DZIEŃ:",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(248, 249, 252),
                Padding = new Padding(10, 0, 0, 0)
            };

            dgvWolneZamowienia = new DataGridView
            {
                Location = new Point(10, 40),
                Size = new Size(panel.Width - 20, panel.Height - 100),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };

            // Stylizacja
            dgvWolneZamowienia.EnableHeadersVisualStyles = false;
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 252);
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(52, 73, 94);
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgvWolneZamowienia.ColumnHeadersHeight = 30;

            dgvWolneZamowienia.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            dgvWolneZamowienia.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgvWolneZamowienia.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);
            dgvWolneZamowienia.RowTemplate.Height = 28;
            dgvWolneZamowienia.GridColor = Color.FromArgb(236, 240, 241);

            // Dwuklik dodaje zamówienie
            dgvWolneZamowienia.CellDoubleClick += async (s, e) => await DodajZamowienieDoKursu();

            btnDodajZamowienie = new Button
            {
                Text = "+ Dodaj do kursu",
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDodajZamowienie.FlatAppearance.BorderSize = 0;
            btnDodajZamowienie.Click += async (s, e) => await DodajZamowienieDoKursu();

            panel.Controls.AddRange(new Control[] { lblZamowieniaInfo, dgvWolneZamowienia, btnDodajZamowienie });

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
            var menuUsun = new ToolStripMenuItem("Usuń", null, async (s, e) => await UsunLadunek());
            var menuEdytuj = new ToolStripMenuItem("Edytuj", null, (s, e) => EdytujLadunek());
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

            var lblKlient = CreateLabel("Ręczne dodawanie:", 0, 20, 130);
            txtKlient = new TextBox
            {
                Location = new Point(140, 18),
                Size = new Size(200, 26),
                Font = new Font("Segoe UI", 10F),
                PlaceholderText = "Nazwa klienta..."
            };

            var lblPojemniki = CreateLabel("Pojemniki:", 350, 20, 80);
            nudPojemniki = new NumericUpDown
            {
                Location = new Point(440, 18),
                Size = new Size(80, 26),
                Font = new Font("Segoe UI", 10F),
                Maximum = 1000,
                Minimum = 0,
                TextAlign = HorizontalAlignment.Center
            };

            var lblUwagi = CreateLabel("Uwagi:", 530, 20, 50);
            txtUwagi = new TextBox
            {
                Location = new Point(590, 18),
                Size = new Size(150, 26),
                Font = new Font("Segoe UI", 10F),
                PlaceholderText = "Opcjonalne..."
            };

            btnDodaj = new Button
            {
                Location = new Point(760, 15),
                Size = new Size(100, 35),
                Text = "+ Dodaj",
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDodaj.FlatAppearance.BorderSize = 0;
            btnDodaj.Click += async (s, e) => await DodajLadunekReczny();

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
                Text = "ZAPISZ",
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.Location = new Point(panel.Width - btnZapisz.Width - 170, 10);
            btnZapisz.Click += BtnZapisz_Click;

            btnAnuluj = new Button
            {
                Size = new Size(140, 45),
                Text = "ANULUJ",
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11F),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;
            btnAnuluj.Location = new Point(panel.Width - btnAnuluj.Width - 20, 10);
            btnAnuluj.Click += (s, e) => Close();

            panel.Controls.AddRange(new Control[] { btnZapisz, btnAnuluj });

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

                await LoadWolneZamowienia();

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

        private async Task LoadWolneZamowienia()
        {
            try
            {
                _wolneZamowienia.Clear();

                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Pobierz zamówienia które nie są jeszcze w żadnym kursie
                var sql = @"
                    SELECT DISTINCT
                        zm.Id AS ZamowienieId,
                        zm.KlientId,
                        zm.DataPrzyjazdu,
                        zm.Status,
                        zm.Uwagi,
                        SUM(ISNULL(zmt.Ilosc, 0)) AS IloscKg
                    FROM dbo.ZamowieniaMieso zm
                    LEFT JOIN dbo.ZamowieniaMiesoTowar zmt ON zm.Id = zmt.ZamowienieId
                    WHERE zm.DataZamowienia = @Data
                      AND ISNULL(zm.Status, 'Nowe') NOT IN ('Anulowane', 'Zrealizowane')
                    GROUP BY zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.Status, zm.Uwagi
                    ORDER BY zm.DataPrzyjazdu";

                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", dtpData.Value.Date);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var zamowienie = new ZamowienieDoTransportu
                    {
                        ZamowienieId = reader.GetInt32(0),
                        KlientId = reader.GetInt32(1),
                        DataPrzyjazdu = reader.GetDateTime(2),
                        Status = reader.IsDBNull(3) ? "Nowe" : reader.GetString(3),
                        IloscKg = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5)
                    };
                    _wolneZamowienia.Add(zamowienie);
                }

                // Pobierz dane klientów z bazy Handel
                if (_wolneZamowienia.Any())
                {
                    await using var cnHandel = new SqlConnection(_connHandel);
                    await cnHandel.OpenAsync();

                    var klientIds = string.Join(",", _wolneZamowienia.Select(z => z.KlientId).Distinct());
                    var sqlKlienci = $@"
                        SELECT 
                            c.Id,
                            ISNULL(c.Shortcut, 'KH ' + CAST(c.Id AS VARCHAR(10))) AS Nazwa,
                            ISNULL(wym.CDim_Handlowiec_Val, '') AS Handlowiec,
                            ISNULL(poa.Postcode, '') + ' ' + ISNULL(poa.Street, '') AS Adres
                        FROM SSCommon.STContractors c
                        LEFT JOIN SSCommon.ContractorClassification wym ON c.Id = wym.ElementId
                        LEFT JOIN SSCommon.STPostOfficeAddresses poa ON poa.ContactGuid = c.ContactGuid 
                            AND poa.AddressName = N'adres domyślny'
                        WHERE c.Id IN ({klientIds})";

                    using var cmdKlienci = new SqlCommand(sqlKlienci, cnHandel);
                    using var readerKlienci = await cmdKlienci.ExecuteReaderAsync();

                    var klienciDict = new Dictionary<int, (string Nazwa, string Handlowiec, string Adres)>();
                    while (await readerKlienci.ReadAsync())
                    {
                        var id = readerKlienci.GetInt32(0);
                        var nazwa = readerKlienci.GetString(1);
                        var handlowiec = readerKlienci.GetString(2);
                        var adres = readerKlienci.GetString(3).Trim();
                        klienciDict[id] = (nazwa, handlowiec, adres);
                    }

                    // Uzupełnij dane zamówień
                    foreach (var zam in _wolneZamowienia)
                    {
                        if (klienciDict.TryGetValue(zam.KlientId, out var klient))
                        {
                            zam.KlientNazwa = klient.Nazwa;
                            zam.Handlowiec = klient.Handlowiec;
                            zam.Adres = klient.Adres;
                        }
                        else
                        {
                            zam.KlientNazwa = $"Klient {zam.KlientId}";
                        }
                    }
                }

                // Wyświetl w gridzie
                ShowZamowieniaInGrid();

                lblZamowieniaInfo.Text = $"WOLNE ZAMÓWIENIA NA {dtpData.Value:yyyy-MM-dd} ({_wolneZamowienia.Count})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania zamówień: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowZamowieniaInGrid()
        {
            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(int));
            dt.Columns.Add("Klient", typeof(string));
            dt.Columns.Add("Godz.", typeof(string));
            dt.Columns.Add("Kg", typeof(decimal));
            dt.Columns.Add("Poj.", typeof(int));
            dt.Columns.Add("Handlowiec", typeof(string));
            dt.Columns.Add("Adres", typeof(string)); // Nowa kolumna z adresem

            foreach (var zam in _wolneZamowienia.OrderBy(z => z.DataPrzyjazdu))
            {
                dt.Rows.Add(
                    zam.ZamowienieId,
                    zam.KlientNazwa,
                    zam.GodzinaStr,
                    zam.IloscKg,
                    zam.Pojemniki,
                    zam.Handlowiec,
                    zam.Adres
                );
            }

            dgvWolneZamowienia.DataSource = dt;

            // Ukryj ID
            if (dgvWolneZamowienia.Columns["ID"] != null)
                dgvWolneZamowienia.Columns["ID"].Visible = false;

            // Ustaw szerokości kolumn
            if (dgvWolneZamowienia.Columns["Klient"] != null)
            {
                dgvWolneZamowienia.Columns["Klient"].Width = 200;
            }
            if (dgvWolneZamowienia.Columns["Godz."] != null)
                dgvWolneZamowienia.Columns["Godz."].Width = 50;
            if (dgvWolneZamowienia.Columns["Kg"] != null)
            {
                dgvWolneZamowienia.Columns["Kg"].Width = 60;
                dgvWolneZamowienia.Columns["Kg"].DefaultCellStyle.Format = "N0";
            }
            if (dgvWolneZamowienia.Columns["Poj."] != null)
                dgvWolneZamowienia.Columns["Poj."].Width = 40;
            if (dgvWolneZamowienia.Columns["Handlowiec"] != null)
                dgvWolneZamowienia.Columns["Handlowiec"].Width = 100;
            if (dgvWolneZamowienia.Columns["Adres"] != null)
            {
                dgvWolneZamowienia.Columns["Adres"].Width = 250;
                dgvWolneZamowienia.Columns["Adres"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }

            // Kolorowanie według godzin
            foreach (DataGridViewRow row in dgvWolneZamowienia.Rows)
            {
                var godzStr = row.Cells["Godz."].Value?.ToString();
                if (!string.IsNullOrEmpty(godzStr) && TimeSpan.TryParse(godzStr, out var godz))
                {
                    if (godz.Hours < 10)
                        row.DefaultCellStyle.BackColor = Color.LightBlue; // Ranne dostawy
                    else if (godz.Hours > 14)
                        row.DefaultCellStyle.BackColor = Color.LightSalmon; // Późne dostawy
                }
            }
        }

        private async Task DodajZamowienieDoKursu()
        {
            if (dgvWolneZamowienia.CurrentRow == null) return;

            var zamId = Convert.ToInt32(dgvWolneZamowienia.CurrentRow.Cells["ID"].Value);
            var zamowienie = _wolneZamowienia.FirstOrDefault(z => z.ZamowienieId == zamId);
            if (zamowienie == null) return;

            // Jeśli nowy kurs - najpierw zapisz nagłówek
            if (!_kursId.HasValue || _kursId.Value <= 0)
            {
                await SaveKurs();
                if (!_kursId.HasValue || _kursId.Value <= 0) return;
            }

            // Dodaj jako ładunek z identyfikatorem zamówienia
            var ladunek = new Ladunek
            {
                KursID = _kursId.Value,
                KodKlienta = $"ZAM_{zamowienie.ZamowienieId}", // Unikalny identyfikator
                PojemnikiE2 = zamowienie.Pojemniki,
                Uwagi = $"{zamowienie.KlientNazwa} ({zamowienie.GodzinaStr}) - {zamowienie.Adres}"
            };

            await _repozytorium.DodajLadunekAsync(ladunek);

            // Usuń z listy wolnych
            _wolneZamowienia.Remove(zamowienie);
            ShowZamowieniaInGrid();

            // Odśwież ładunki
            await LoadLadunki();
        }

        private async Task LoadKursData()
        {
            if (!_kursId.HasValue) return;

            // Pobierz kurs używając metody z repozytorium
            _kurs = await _repozytorium.PobierzKursAsync(_kursId.Value);

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
            dt.Columns.Add("Zam.ID", typeof(string));
            dt.Columns.Add("Adres", typeof(string)); // Nowa kolumna z adresem
            dt.Columns.Add("Uwagi", typeof(string));

            var klientNazwy = new List<string>();
            int lp = 1;

            // Pobierz adresy dla ładunków z zamówień
            var zamowieniaIds = _ladunki
                .Where(l => l.KodKlienta?.StartsWith("ZAM_") == true)
                .Select(l => int.Parse(l.KodKlienta.Substring(4)))
                .ToList();

            var adresyZamowien = new Dictionary<int, string>();

            if (zamowieniaIds.Any())
            {
                try
                {
                    await using var cnHandel = new SqlConnection(_connHandel);
                    await cnHandel.OpenAsync();

                    // Pobierz klientów z zamówień
                    await using var cnLibra = new SqlConnection(_connLibra);
                    await cnLibra.OpenAsync();

                    var klientIds = new List<int>();
                    var sqlKlienci = $@"SELECT Id, KlientId FROM dbo.ZamowieniaMieso WHERE Id IN ({string.Join(",", zamowieniaIds)})";
                    using var cmdKlienci = new SqlCommand(sqlKlienci, cnLibra);
                    using var readerKlienci = await cmdKlienci.ExecuteReaderAsync();

                    var zamowienieKlientDict = new Dictionary<int, int>();
                    while (await readerKlienci.ReadAsync())
                    {
                        zamowienieKlientDict[readerKlienci.GetInt32(0)] = readerKlienci.GetInt32(1);
                        klientIds.Add(readerKlienci.GetInt32(1));
                    }

                    if (klientIds.Any())
                    {
                        var sqlAdresy = $@"
                            SELECT 
                                c.Id,
                                ISNULL(poa.Postcode, '') + ' ' + ISNULL(poa.Street, '') AS Adres
                            FROM SSCommon.STContractors c
                            LEFT JOIN SSCommon.STPostOfficeAddresses poa ON poa.ContactGuid = c.ContactGuid 
                                AND poa.AddressName = N'adres domyślny'
                            WHERE c.Id IN ({string.Join(",", klientIds.Distinct())})";

                        using var cmdAdresy = new SqlCommand(sqlAdresy, cnHandel);
                        using var readerAdresy = await cmdAdresy.ExecuteReaderAsync();

                        var klientAdresDict = new Dictionary<int, string>();
                        while (await readerAdresy.ReadAsync())
                        {
                            klientAdresDict[readerAdresy.GetInt32(0)] = readerAdresy.GetString(1).Trim();
                        }

                        // Mapuj adresy na zamówienia
                        foreach (var pair in zamowienieKlientDict)
                        {
                            if (klientAdresDict.TryGetValue(pair.Value, out var adres))
                            {
                                adresyZamowien[pair.Key] = adres;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Ignoruj błędy pobierania adresów
                    Console.WriteLine($"Błąd pobierania adresów: {ex.Message}");
                }
            }

            foreach (var ladunek in _ladunki.OrderBy(l => l.Kolejnosc))
            {
                string klientNazwa = ladunek.Uwagi ?? ladunek.KodKlienta ?? "";
                string zamId = "";
                string adres = "";

                // Sprawdź czy to zamówienie z bazy
                if (ladunek.KodKlienta?.StartsWith("ZAM_") == true)
                {
                    zamId = ladunek.KodKlienta.Substring(4);
                    if (int.TryParse(zamId, out var zamowienieId))
                    {
                        adresyZamowien.TryGetValue(zamowienieId, out adres);
                    }

                    // Pobierz nazwę klienta jeśli to zamówienie
                    if (!string.IsNullOrEmpty(klientNazwa) && klientNazwa.Contains("("))
                    {
                        var idx = klientNazwa.IndexOf("(");
                        if (idx > 0)
                        {
                            var nazwa = klientNazwa.Substring(0, idx).Trim();
                            klientNazwy.Add(nazwa);
                            klientNazwa = nazwa;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(ladunek.KodKlienta))
                {
                    klientNazwy.Add(ladunek.KodKlienta);
                }

                dt.Rows.Add(
                    ladunek.LadunekID,
                    lp++,
                    klientNazwa,
                    ladunek.PojemnikiE2,
                    zamId,
                    adres,
                    ladunek.Uwagi ?? ""
                );
            }

            dgvLadunki.DataSource = dt;

            if (dgvLadunki.Columns["ID"] != null)
                dgvLadunki.Columns["ID"].Visible = false;
            if (dgvLadunki.Columns["Lp."] != null)
                dgvLadunki.Columns["Lp."].Width = 40;
            if (dgvLadunki.Columns["Zam.ID"] != null)
                dgvLadunki.Columns["Zam.ID"].Width = 60;
            if (dgvLadunki.Columns["Adres"] != null)
            {
                dgvLadunki.Columns["Adres"].Width = 200;
                dgvLadunki.Columns["Adres"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }

            // Automatycznie aktualizuj trasę
            if (klientNazwy.Any())
            {
                txtTrasa.Text = string.Join(" → ", klientNazwy.Distinct());
            }

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

                // Jeśli zaznaczono checkbox 40 E2, użyj maksymalnej ilości pojemników na paletę
                int pojemnikiNaPalete = chk40E2.Checked ? 40 : 36;
                int paletyNominal = (int)Math.Ceiling(sumaPojemnikow / (double)pojemnikiNaPalete);
                int paletyPojazdu = pojazd.PaletyH1;
                int procent = paletyPojazdu > 0 ? (int)(paletyNominal * 100.0 / paletyPojazdu) : 0;

                progressWypelnienie.Value = Math.Min(100, procent);

                string tryb = chk40E2.Checked ? " (40 E2/pal.)" : " (36 E2/pal.)";
                lblStatystyki.Text = $"{sumaPojemnikow} pojemników / {paletyNominal} palet z {paletyPojazdu}{tryb}";

                // Kolorowanie
                if (procent > 100)
                {
                    progressWypelnienie.ForeColor = Color.Red;
                    lblWypelnienie.ForeColor = Color.Red;
                    lblWypelnienie.Text = $"PRZEPEŁNIENIE: {procent}%";
                }
                else if (procent > 90)
                {
                    progressWypelnienie.ForeColor = Color.Orange;
                    lblWypelnienie.ForeColor = Color.Orange;
                    lblWypelnienie.Text = $"Wypełnienie: {procent}% (mało miejsca)";
                }
                else
                {
                    progressWypelnienie.ForeColor = Color.Green;
                    lblWypelnienie.ForeColor = Color.Green;
                    lblWypelnienie.Text = $"Wypełnienie: {procent}%";
                }
            }
            catch
            {
                // Ignoruj błędy aktualizacji
            }
        }

        private async Task DodajLadunekReczny()
        {
            if (string.IsNullOrWhiteSpace(txtKlient.Text))
            {
                MessageBox.Show("Podaj nazwę klienta.", "Brak danych",
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

            // Sprawdź czy to zamówienie - jeśli tak, przywróć do wolnych
            var ladunek = _ladunki.FirstOrDefault(l => l.LadunekID == ladunekId);
            if (ladunek?.KodKlienta?.StartsWith("ZAM_") == true)
            {
                await _repozytorium.UsunLadunekAsync(ladunekId);
                await LoadWolneZamowienia(); // Odśwież listę wolnych zamówień
            }
            else
            {
                await _repozytorium.UsunLadunekAsync(ladunekId);
            }

            await LoadLadunki();
        }

        private void EdytujLadunek()
        {
            if (dgvLadunki.CurrentRow == null) return;

            var row = dgvLadunki.CurrentRow;
            txtKlient.Text = row.Cells["Klient"].Value?.ToString() ?? "";
            nudPojemniki.Value = Convert.ToInt32(row.Cells["Pojemniki"].Value ?? 0);
            txtUwagi.Text = row.Cells["Uwagi"].Value?.ToString() ?? "";

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

            // Ustaw odpowiednią wartość E2 na paletę w zależności od checkboxa
            byte planE2NaPalete = (byte)(chk40E2.Checked ? 40 : 36);

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
                PlanE2NaPalete = planE2NaPalete
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