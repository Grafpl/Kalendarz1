// Plik: Transport/EdytorKursuWithPalety.cs
// Wersja z transakcyjnym zapisem i automatycznym tworzeniem zamówień
// POPRAWKA 2025-01-15: Wykluczenie zamówień z własnym transportem
// Zamówienia z TransportStatus = 'Własny' nie są pokazywane w wolnych zamówieniach
// (linia ~1534)


using Kalendarz1.Transport.Pakowanie;
using Kalendarz1.Transport.Repozytorium;
using Kalendarz1.FakturyPanel.Services;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1.Transport.Formularze
{
    public partial class EdytorKursuWithPalety : Form
    {
        private readonly TransportRepozytorium _repozytorium;
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private long? _kursId;
        private readonly string _uzytkownik;
        private Kurs _kurs;
        private List<LadunekWithPalety> _ladunki = new List<LadunekWithPalety>();
        private List<Kierowca> _kierowcy;
        private List<Pojazd> _pojazdy;
        private List<ZamowienieDoTransportu> _wolneZamowienia = new List<ZamowienieDoTransportu>();
        private List<KontrahentInfo> _kontrahenci = new List<KontrahentInfo>();
        private Timer _autoUpdateTimer;

        private RadioButton rbDataUboju;
        private RadioButton rbDataOdbioru;
        private bool UzywajDataUboju => rbDataUboju?.Checked ?? true;

        // Listy do śledzenia zmian
        private List<ZamowienieDoTransportu> _zamowieniaDoDodania = new List<ZamowienieDoTransportu>();
        private List<int> _zamowieniaDoUsuniecia = new List<int>();
        private bool _dataLoaded = false;

        // Kontrolki nagłówka
        private ComboBox cboKierowca;
        private ComboBox cboPojazd;
        private DateTimePicker dtpData;
        private MaskedTextBox txtGodzWyjazdu;
        private MaskedTextBox txtGodzPowrotu;
        private TextBox txtTrasa;
        private Button btnNowyKierowca;
        private Button btnNowyPojazd;

        // Grid ładunków
        private DataGridView dgvLadunki;

        // Panel zamówień
        private DataGridView dgvWolneZamowienia;
        private Button btnDodajZamowienie;
        private Label lblZamowieniaInfo;
        private DateTimePicker _dtpZamowienia;
        private Label _lblLiczbaZamowien;

        // Panel ręcznego dodawania - nowe kontrolki
        private ComboBox cboKlient;
        private ComboBox cbFiltrHandlowca;
        private NumericUpDown nudPalety;
        private MaskedTextBox txtGodzina;
        private TextBox txtUwagi;
        private Button btnDodaj;

        // Przyciski zarządzania kolejnością
        private Button btnMoveUp;
        private Button btnMoveDown;
        private Button btnSortujPoKolejnosci;

        // Wskaźnik wypełnienia
        private ProgressBar progressWypelnienie;
        private Label lblWypelnienie;
        private Label lblStatystyki;
        private Label lblAutoUpdate;

        // Przyciski główne
        private Button btnZapisz;
        private Button btnAnuluj;

        public string? UserID { get; set; }

        #region Klasy pomocnicze

        public class LadunekWithPalety
        {
            public long LadunekID { get; set; }
            public long KursID { get; set; }
            public int Kolejnosc { get; set; }
            public string? KodKlienta { get; set; }
            public decimal Palety { get; set; }
            public int PojemnikiE2 { get; set; }
            public string? Uwagi { get; set; }
            public string? Adres { get; set; }
            public bool TrybE2 { get; set; }
            public string? NazwaKlienta { get; set; }
            public bool ZmienionyWZamowieniu { get; set; }
            public decimal PoprzedniePalety { get; set; }
            public int PoprzedniePojemniki { get; set; }
            public DateTime? DataUboju { get; set; }  // NOWE
        }
        public class KontrahentInfo
        {
            public int Id { get; set; }
            public string Nazwa { get; set; } = "";
            public string NIP { get; set; } = "";
            public string KodPocztowy { get; set; } = "";
            public string Miejscowosc { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public string Adres => $"{KodPocztowy} {Miejscowosc}".Trim();

            public override string ToString() =>
                string.IsNullOrEmpty(Handlowiec)
                    ? $"{Nazwa} - {Adres}"
                    : $"{Nazwa} ({Handlowiec}) - {Adres}";
        }

        // 2. Zmodyfikuj klasę ZamowienieDoTransportu - dodaj właściwość:
        public class ZamowienieDoTransportu
        {
            public int ZamowienieId { get; set; }
            public int KlientId { get; set; }
            public string KlientNazwa { get; set; } = "";
            public decimal IloscKg { get; set; }
            public decimal Palety { get; set; }
            public int Pojemniki { get; set; }
            public bool TrybE2 { get; set; }
            public DateTime DataPrzyjazdu { get; set; }
            public DateTime DataOdbioru { get; set; }
            public DateTime? DataUboju { get; set; }  // NOWE
            public string GodzinaStr => DataPrzyjazdu.ToString("HH:mm");
            public string Status { get; set; } = "Nowe";
            public string Handlowiec { get; set; } = "";
            public string Adres { get; set; } = "";
        }
        public class DodajKierowceDialog : Form
        {
            public Kierowca NowyKierowca { get; private set; }
            private TextBox txtImie, txtNazwisko, txtTelefon;

            public DodajKierowceDialog()
            {
                Text = "Dodaj nowego kierowcę";
                Size = new Size(400, 250);
                StartPosition = FormStartPosition.CenterParent;

                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20) };

                txtImie = new TextBox { Dock = DockStyle.Fill };
                txtNazwisko = new TextBox { Dock = DockStyle.Fill };
                txtTelefon = new TextBox { Dock = DockStyle.Fill };

                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

                layout.Controls.Add(new Label { Text = "Imię:", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
                layout.Controls.Add(txtImie, 1, 0);
                layout.Controls.Add(new Label { Text = "Nazwisko:", TextAlign = ContentAlignment.MiddleRight }, 0, 1);
                layout.Controls.Add(txtNazwisko, 1, 1);
                layout.Controls.Add(new Label { Text = "Telefon:", TextAlign = ContentAlignment.MiddleRight }, 0, 2);
                layout.Controls.Add(txtTelefon, 1, 2);

                var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
                var btnOK = new Button { Text = "Dodaj", DialogResult = DialogResult.OK, Width = 80 };
                var btnCancel = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel, Width = 80 };

                btnOK.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(txtImie.Text) || string.IsNullOrWhiteSpace(txtNazwisko.Text))
                    {
                        MessageBox.Show("Podaj imię i nazwisko.");
                        DialogResult = DialogResult.None;
                        return;
                    }

                    NowyKierowca = new Kierowca
                    {
                        Imie = txtImie.Text.Trim(),
                        Nazwisko = txtNazwisko.Text.Trim(),
                        Telefon = string.IsNullOrWhiteSpace(txtTelefon.Text) ? null : txtTelefon.Text.Trim(),
                        Aktywny = true
                    };
                };

                btnPanel.Controls.Add(btnCancel);
                btnPanel.Controls.Add(btnOK);
                layout.SetColumnSpan(btnPanel, 2);
                layout.Controls.Add(btnPanel, 0, 3);

                Controls.Add(layout);
            }
        }

        public class DodajPojazdDialog : Form
        {
            public Pojazd NowyPojazd { get; private set; }
            private TextBox txtRejestracja, txtMarka, txtModel;
            private NumericUpDown nudPalety;

            public DodajPojazdDialog()
            {
                Text = "Dodaj nowy pojazd";
                Size = new Size(400, 280);
                StartPosition = FormStartPosition.CenterParent;

                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20) };

                txtRejestracja = new TextBox { Dock = DockStyle.Fill };
                txtMarka = new TextBox { Dock = DockStyle.Fill };
                txtModel = new TextBox { Dock = DockStyle.Fill };
                nudPalety = new NumericUpDown
                {
                    Dock = DockStyle.Fill,
                    Minimum = 1,
                    Maximum = 50,
                    Value = 33
                };

                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

                layout.Controls.Add(new Label { Text = "Rejestracja:", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
                layout.Controls.Add(txtRejestracja, 1, 0);
                layout.Controls.Add(new Label { Text = "Marka:", TextAlign = ContentAlignment.MiddleRight }, 0, 1);
                layout.Controls.Add(txtMarka, 1, 1);
                layout.Controls.Add(new Label { Text = "Model:", TextAlign = ContentAlignment.MiddleRight }, 0, 2);
                layout.Controls.Add(txtModel, 1, 2);
                layout.Controls.Add(new Label { Text = "Palety H1:", TextAlign = ContentAlignment.MiddleRight }, 0, 3);
                layout.Controls.Add(nudPalety, 1, 3);

                var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
                var btnOK = new Button { Text = "Dodaj", DialogResult = DialogResult.OK, Width = 80 };
                var btnCancel = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel, Width = 80 };

                btnOK.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(txtRejestracja.Text))
                    {
                        MessageBox.Show("Podaj numer rejestracyjny.");
                        DialogResult = DialogResult.None;
                        return;
                    }

                    NowyPojazd = new Pojazd
                    {
                        Rejestracja = txtRejestracja.Text.Trim().ToUpper(),
                        Marka = string.IsNullOrWhiteSpace(txtMarka.Text) ? null : txtMarka.Text.Trim(),
                        Model = string.IsNullOrWhiteSpace(txtModel.Text) ? null : txtModel.Text.Trim(),
                        PaletyH1 = (int)nudPalety.Value,
                        Aktywny = true
                    };
                };

                btnPanel.Controls.Add(btnCancel);
                btnPanel.Controls.Add(btnOK);
                layout.SetColumnSpan(btnPanel, 2);
                layout.Controls.Add(btnPanel, 0, 4);

                Controls.Add(layout);
            }
        }

        #endregion

        #region Konstruktory

        public EdytorKursuWithPalety(TransportRepozytorium repozytorium, DateTime data, string uzytkownik)
            : this(repozytorium, null, data, uzytkownik)
        {
        }

        public EdytorKursuWithPalety(TransportRepozytorium repozytorium, Kurs kurs, string uzytkownik)
            : this(repozytorium, kurs?.KursID, kurs?.DataKursu, uzytkownik)
        {
            _kurs = kurs;
        }

        private EdytorKursuWithPalety(TransportRepozytorium repozytorium, long? kursId, DateTime? data, string uzytkownik)
        {
            _repozytorium = repozytorium ?? throw new ArgumentNullException(nameof(repozytorium));
            _kursId = kursId;
            _uzytkownik = uzytkownik ?? Environment.UserName;
            UserID = uzytkownik;

            InitializeComponent();
            dtpData.Value = data ?? DateTime.Today;

            InitializeAutoUpdateTimer();

            _ = LoadDataAsync();
        }

        #endregion

        #region Inicjalizacja UI

        private void InitializeAutoUpdateTimer()
        {
            _autoUpdateTimer = new Timer();
            _autoUpdateTimer.Interval = 10000;
            _autoUpdateTimer.Tick += async (s, e) => await CheckForZamowieniaUpdates();
            _autoUpdateTimer.Start();
        }

        private void InitializeComponent()
        {
            Text = _kursId.HasValue ? "Edycja kursu transportowego" : "Nowy kurs transportowy";
            Size = new Size(1600, 1050); // Zwiększone z 1000 na 1050
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(240, 242, 247);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(25),
                BackColor = Color.FromArgb(240, 242, 247)
            };

            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80)); // Zwiększone 

            var headerPanel = CreateHeaderPanel();
            mainLayout.Controls.Add(headerPanel, 0, 0);
            mainLayout.SetColumnSpan(headerPanel, 2);

            mainLayout.Controls.Add(CreateLadunkiPanel(), 0, 1);
            mainLayout.Controls.Add(CreateAddPanel(), 0, 2);

            mainLayout.Controls.Add(CreateZamowieniaPanel(), 1, 1);
            mainLayout.SetRowSpan(mainLayout.GetControlFromPosition(1, 1), 2);

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

            var lblKierowca = CreateLabel("KIEROWCA:", 20, 20, 90);
            lblKierowca.ForeColor = Color.FromArgb(173, 181, 189);
            lblKierowca.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            cboKierowca = new ComboBox
            {
                Location = new Point(115, 18),
                Size = new Size(200, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                DisplayMember = "PelneNazwisko",
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            btnNowyKierowca = new Button
            {
                Text = "+",
                Location = new Point(320, 18),
                Size = new Size(30, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnNowyKierowca.FlatAppearance.BorderSize = 0;
            btnNowyKierowca.Click += BtnNowyKierowca_Click;

            var lblPojazd = CreateLabel("POJAZD:", 365, 20, 70);
            lblPojazd.ForeColor = Color.FromArgb(173, 181, 189);
            lblPojazd.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            cboPojazd = new ComboBox
            {
                Location = new Point(440, 18),
                Size = new Size(150, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                DisplayMember = "Opis",
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cboPojazd.SelectedIndexChanged += async (s, e) => await UpdateWypelnienie();

            btnNowyPojazd = new Button
            {
                Text = "+",
                Location = new Point(595, 18),
                Size = new Size(30, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnNowyPojazd.FlatAppearance.BorderSize = 0;
            btnNowyPojazd.Click += BtnNowyPojazd_Click;

            var lblData = CreateLabel("DATA:", 640, 20, 50);
            lblData.ForeColor = Color.FromArgb(173, 181, 189);
            lblData.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            dtpData = new DateTimePicker
            {
                Location = new Point(695, 18),
                Size = new Size(140, 26),
                Format = DateTimePickerFormat.Short,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                CalendarMonthBackground = Color.FromArgb(52, 56, 64)
            };
            dtpData.ValueChanged += async (s, e) => await LoadWolneZamowienia();

            lblAutoUpdate = new Label
            {
                Location = new Point(850, 20),
                Size = new Size(300, 23),
                Text = "",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Orange,
                Visible = false
            };

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
                PlaceholderText = "Trasa zostanie uzupełniona automatycznie...",
                ReadOnly = true
            };

            var panelWypelnienie = new Panel
            {
                Location = new Point(20, 110),
                Size = new Size(1200, 50),
                BackColor = Color.FromArgb(33, 37, 43)
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
                Text = "0 palet z 0 dostępnych",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 193, 7),
                TextAlign = ContentAlignment.MiddleRight
            };

            panelWypelnienie.Controls.AddRange(new Control[] { lblWypelnienie, progressWypelnienie, lblStatystyki });

            panel.Controls.AddRange(new Control[] {
                lblKierowca, cboKierowca, btnNowyKierowca,
                lblPojazd, cboPojazd, btnNowyPojazd,
                lblData, dtpData, lblAutoUpdate,
                lblGodziny, txtGodzWyjazdu, lblDo, txtGodzPowrotu,
                lblTrasa, txtTrasa,
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

            var panelKolejnosc = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
                BackColor = Color.FromArgb(248, 249, 252),
                Padding = new Padding(10, 8, 10, 8)
            };

            var lblKolejnosc = new Label
            {
                Text = "KOLEJNOŚĆ:",
                Location = new Point(10, 12),
                Size = new Size(80, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94)
            };

            btnMoveUp = new Button
            {
                Text = "▲",
                Location = new Point(100, 8),
                Size = new Size(40, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnMoveUp.FlatAppearance.BorderSize = 0;
            btnMoveUp.Click += async (s, e) => await PrzesunLadunek(-1);

            var toolTip = new ToolTip();
            toolTip.SetToolTip(btnMoveUp, "Przesuń w górę");

            btnMoveDown = new Button
            {
                Text = "▼",
                Location = new Point(145, 8),
                Size = new Size(40, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnMoveDown.FlatAppearance.BorderSize = 0;
            btnMoveDown.Click += async (s, e) => await PrzesunLadunek(1);
            toolTip.SetToolTip(btnMoveDown, "Przesuń w dół");

            btnSortujPoKolejnosci = new Button
            {
                Text = "Sortuj",
                Location = new Point(195, 8),
                Size = new Size(80, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            toolTip.SetToolTip(btnSortujPoKolejnosci, "Zmień kolejność");
            btnSortujPoKolejnosci.FlatAppearance.BorderSize = 0;
            btnSortujPoKolejnosci.Click += async (s, e) => await OtworzDialogKolejnosci();

            panelKolejnosc.Controls.AddRange(new Control[] {
                lblKolejnosc, btnMoveUp, btnMoveDown, btnSortujPoKolejnosci
            });

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

            var contextMenu = new ContextMenuStrip();
            var menuEdytuj = new ToolStripMenuItem("Edytuj pozycję", null, (s, e) => EdytujLadunek());
            var menuEdytujZamowienie = new ToolStripMenuItem("Edytuj zamówienie", null, async (s, e) => await EdytujPrzypisaneZamowienie());
            var menuUsun = new ToolStripMenuItem("Usuń z kursu", null, async (s, e) => await UsunLadunek());
            var menuGora = new ToolStripMenuItem("Przesuń w górę", null, async (s, e) => await PrzesunLadunek(-1));
            var menuDol = new ToolStripMenuItem("Przesuń w dół", null, async (s, e) => await PrzesunLadunek(1));
            var menuOdswiez = new ToolStripMenuItem("Odśwież z zamówienia", null, async (s, e) => await OdswiezZamowienie());

            contextMenu.Items.Add(menuEdytuj);
            contextMenu.Items.Add(menuEdytujZamowienie);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(menuOdswiez);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(menuUsun);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(menuGora);
            contextMenu.Items.Add(menuDol);

            dgvLadunki.ContextMenuStrip = contextMenu;

            panel.Controls.Add(dgvLadunki);
            panel.Controls.Add(panelKolejnosc);

            return panel;
        }

        private Panel CreateAddPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20, 5, 20, 5),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblTytul = CreateLabel("Ręczne dodawanie z utworzeniem zamówienia:", 5, 3, 320);
            lblTytul.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            // Filtr handlowca
            var lblFiltr = CreateLabel("Handlowiec:", 5, 28, 80);
            cbFiltrHandlowca = new ComboBox
            {
                Location = new Point(90, 25),
                Size = new Size(150, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            cbFiltrHandlowca.SelectedIndexChanged += FiltrujKontrahentow;

            // Wybór klienta
            var lblKlient = CreateLabel("Kontrahent:", 250, 28, 70);
            cboKlient = new ComboBox
            {
                Location = new Point(325, 25),
                Size = new Size(250, 26),
                Font = new Font("Segoe UI", 9F),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DisplayMember = "Nazwa",
                ValueMember = "Id"
            };

            // Liczba palet
            var lblPalety = CreateLabel("Palety:", 585, 28, 50);
            nudPalety = new NumericUpDown
            {
                Location = new Point(640, 25),
                Size = new Size(70, 26),
                Font = new Font("Segoe UI", 9F),
                Maximum = 100,
                Minimum = 0,
                DecimalPlaces = 1,
                Increment = 0.5m,
                TextAlign = HorizontalAlignment.Center
            };

            // Godzina
            var lblGodzina = CreateLabel("Godz.:", 720, 28, 45);
            txtGodzina = new MaskedTextBox
            {
                Location = new Point(770, 25),
                Size = new Size(55, 26),
                Mask = "00:00",
                Font = new Font("Segoe UI", 9F),
                Text = "08:00",
                TextAlign = HorizontalAlignment.Center
            };

            // Uwagi
            var lblUwagi = CreateLabel("Uwagi:", 835, 28, 45);
            txtUwagi = new TextBox
            {
                Location = new Point(885, 25),
                Size = new Size(120, 26),
                Font = new Font("Segoe UI", 9F),
                PlaceholderText = "Opcjonalne..."
            };

            // Przycisk dodaj
            btnDodaj = new Button
            {
                Location = new Point(1015, 22),
                Size = new Size(110, 32),
                Text = "Utwórz i dodaj",
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDodaj.FlatAppearance.BorderSize = 0;
            btnDodaj.Click += async (s, e) => await DodajLadunekRecznyZZamowieniem();

            // Info
            var lblInfo = new Label
            {
                Location = new Point(5, 58),
                Size = new Size(1120, 18),
                Text = "Wybierz kontrahenta, wpisz palety i godzinę - zostanie utworzone nowe zamówienie i dodane do kursu",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100)
            };

            panel.Controls.AddRange(new Control[] {
                lblTytul, lblFiltr, cbFiltrHandlowca,
                lblKlient, cboKlient, lblPalety, nudPalety,
                lblGodzina, txtGodzina, lblUwagi, txtUwagi,
                btnDodaj, lblInfo
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

            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 110,  // Zwiększone z 70 na 110
                BackColor = Color.FromArgb(248, 249, 252),
                Padding = new Padding(10, 5, 10, 5)
            };

            lblZamowieniaInfo = new Label
            {
                Text = "WOLNE ZAMÓWIENIA:",
                Location = new Point(10, 10),
                Size = new Size(250, 25),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // NOWE: RadioButtony
            var lblFiltrujPoData = new Label
            {
                Text = "Filtruj wg:",
                Location = new Point(10, 40),
                Size = new Size(70, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94)
            };

            rbDataUboju = new RadioButton
            {
                Text = "Data uboju",
                Location = new Point(85, 40),
                Size = new Size(100, 20),
                Checked = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(52, 73, 94)
            };
            rbDataUboju.CheckedChanged += async (s, e) =>
            {
                if (rbDataUboju.Checked && _dataLoaded)
                {
                    await LoadWolneZamowieniaForDate(_dtpZamowienia?.Value ?? dtpData.Value);
                }
            };

            rbDataOdbioru = new RadioButton
            {
                Text = "Data odbioru (stary system)",
                Location = new Point(195, 40),
                Size = new Size(180, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.Gray
            };
            rbDataOdbioru.CheckedChanged += async (s, e) =>
            {
                if (rbDataOdbioru.Checked && _dataLoaded)
                {
                    await LoadWolneZamowieniaForDate(_dtpZamowienia?.Value ?? dtpData.Value);
                }
            };

            var dtpZamowienia = new DateTimePicker
            {
                Location = new Point(10, 70),
                Size = new Size(150, 26),
                Format = DateTimePickerFormat.Short,
                Font = new Font("Segoe UI", 10F),
                Value = dtpData.Value
            };
            dtpZamowienia.ValueChanged += async (s, e) =>
            {
                await LoadWolneZamowieniaForDate(dtpZamowienia.Value);
            };

            var btnRefreshZamowienia = new Button
            {
                Text = "Odśwież",
                Location = new Point(170, 70),
                Size = new Size(70, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            btnRefreshZamowienia.FlatAppearance.BorderSize = 0;
            btnRefreshZamowienia.Click += async (s, e) =>
            {
                await LoadWolneZamowieniaForDate(dtpZamowienia.Value);
            };

            var btnToday = new Button
            {
                Text = "Dziś",
                Location = new Point(250, 70),
                Size = new Size(50, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            btnToday.FlatAppearance.BorderSize = 0;
            btnToday.Click += async (s, e) =>
            {
                dtpZamowienia.Value = DateTime.Today;
                await LoadWolneZamowieniaForDate(DateTime.Today);
            };

            var lblLiczbaZamowien = new Label
            {
                Location = new Point(310, 70),
                Size = new Size(120, 26),
                Text = "0 zamówień",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 152, 219),
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            panelHeader.Controls.AddRange(new Control[] {
        lblZamowieniaInfo,
        lblFiltrujPoData, rbDataUboju, rbDataOdbioru,  // NOWE
        dtpZamowienia,
        btnRefreshZamowienia,
        btnToday,
        lblLiczbaZamowien
    });

            _dtpZamowienia = dtpZamowienia;
            _lblLiczbaZamowien = lblLiczbaZamowien;

            dgvWolneZamowienia = new DataGridView
            {
                Location = new Point(10, 120),  // Zmienione z 80 na 120
                Size = new Size(panel.Width - 20, panel.Height - 180),  // Dostosowane
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

            dgvWolneZamowienia.CellDoubleClick += async (s, e) => await DodajZamowienieDoKursu();

            var contextMenuWolne = new ContextMenuStrip();
            var menuDodajDoKursu = new ToolStripMenuItem("Dodaj do kursu", null, async (s, e) => await DodajZamowienieDoKursu());
            var menuEdytujZamowienie = new ToolStripMenuItem("Edytuj zamówienie", null, async (s, e) => await EdytujWolneZamowienie());
            var menuSzczegolyZamowienia = new ToolStripMenuItem("Szczegóły", null, async (s, e) => await PokazSzczegolyZamowienia());

            contextMenuWolne.Items.Add(menuDodajDoKursu);
            contextMenuWolne.Items.Add(menuEdytujZamowienie);
            contextMenuWolne.Items.Add(menuSzczegolyZamowienia);

            dgvWolneZamowienia.ContextMenuStrip = contextMenuWolne;

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

            panel.Controls.AddRange(new Control[] { panelHeader, dgvWolneZamowienia, btnDodajZamowienie });

            return panel;
        }

        private Panel CreateButtonsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(33, 37, 43),
                Padding = new Padding(0, 15, 0, 15) // Zwiększone padding
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
            btnZapisz.Location = new Point(panel.Width - btnZapisz.Width - 170, 15); // Zmienione z 10 na 15
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
            btnAnuluj.Location = new Point(panel.Width - btnAnuluj.Width - 20, 15); // Zmienione z 10 na 15
            btnAnuluj.Click += (s, e) =>
            {
                if (_ladunki.Count > 0 || _zamowieniaDoDodania.Count > 0 || _zamowieniaDoUsuniecia.Count > 0)
                {
                    var result = MessageBox.Show(
                        "Masz niezapisane zmiany. Czy na pewno chcesz zamknąć bez zapisywania?",
                        "Niezapisane zmiany",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result != DialogResult.Yes)
                        return;
                }

                _autoUpdateTimer?.Stop();
                _autoUpdateTimer?.Dispose();
                Close();
            };

            panel.Controls.AddRange(new Control[] { btnZapisz, btnAnuluj });

            panel.Resize += (s, e) =>
            {
                btnAnuluj.Location = new Point(panel.Width - btnAnuluj.Width - 20, 15); // Zmienione
                btnZapisz.Location = new Point(panel.Width - btnZapisz.Width - 170, 15); // Zmienione
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

        #endregion

        #region Ładowanie danych

        private async Task LoadDataAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                _kierowcy = await _repozytorium.PobierzKierowcowAsync(true);
                _pojazdy = await _repozytorium.PobierzPojazdyAsync(true);

                cboKierowca.DataSource = _kierowcy;
                cboPojazd.DataSource = _pojazdy;

                await LoadKontrahenciAsync();
                await LoadWolneZamowienia();

                if (_kursId.HasValue && _kursId.Value > 0)
                {
                    await LoadKursData();
                }

                _dataLoaded = true;
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

        private async Task LoadKontrahenciAsync()
        {
            const string sql = @"
        SELECT 
            c.Id,
            ISNULL(c.Shortcut, 'KH ' + CAST(c.Id AS VARCHAR(10))) AS Nazwa,
            ISNULL(c.NIP, '') AS NIP,
            ISNULL(poa.Postcode, '') AS KodPocztowy,
            ISNULL(poa.Street, '') AS Miejscowosc, 
            ISNULL(wym.CDim_Handlowiec_Val, '') AS Handlowiec
        FROM [HANDEL].[SSCommon].[STContractors] c
        LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa 
            ON poa.ContactGuid = c.ContactGuid 
            AND poa.AddressName = N'adres domyślny'
        LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym 
            ON c.Id = wym.ElementId
                ORDER BY c.Shortcut";

            try
            {
                _kontrahenci.Clear();

                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    _kontrahenci.Add(new KontrahentInfo
                    {
                        Id = rd.GetInt32(0),
                        Nazwa = rd.GetString(1),
                        NIP = rd.GetString(2),
                        KodPocztowy = rd.GetString(3),
                        Miejscowosc = rd.GetString(4),
                        Handlowiec = rd.GetString(5)
                    });
                }

                // Wypełnij ComboBox klientów
                if (this.InvokeRequired)
                {
                    this.Invoke(() =>
                    {
                        cboKlient.DataSource = new BindingSource(_kontrahenci, null);
                        cboKlient.SelectedIndex = -1;

                        // Wypełnij filtr handlowców
                        var handlowcy = _kontrahenci
                            .Select(k => k.Handlowiec)
                            .Where(h => !string.IsNullOrEmpty(h))
                            .Distinct()
                            .OrderBy(h => h)
                            .ToList();

                        handlowcy.Insert(0, "— Wszyscy —");
                        cbFiltrHandlowca.DataSource = handlowcy;
                        cbFiltrHandlowca.SelectedIndex = 0;
                    });
                }
                else
                {
                    cboKlient.DataSource = new BindingSource(_kontrahenci, null);
                    cboKlient.SelectedIndex = -1;

                    var handlowcy = _kontrahenci
                        .Select(k => k.Handlowiec)
                        .Where(h => !string.IsNullOrEmpty(h))
                        .Distinct()
                        .OrderBy(h => h)
                        .ToList();

                    handlowcy.Insert(0, "— Wszyscy —");
                    cbFiltrHandlowca.DataSource = handlowcy;
                    cbFiltrHandlowca.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania kontrahentów: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FiltrujKontrahentow(object sender, EventArgs e)
        {
            var wybranyHandlowiec = cbFiltrHandlowca.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(wybranyHandlowiec) || wybranyHandlowiec == "— Wszyscy —")
            {
                cboKlient.DataSource = new BindingSource(_kontrahenci, null);
            }
            else
            {
                var przefiltrowane = _kontrahenci
                    .Where(k => k.Handlowiec == wybranyHandlowiec)
                    .ToList();
                cboKlient.DataSource = new BindingSource(przefiltrowane, null);
            }
            cboKlient.SelectedIndex = -1;
        }

        private async Task LoadKursData()
        {
            if (!_kursId.HasValue) return;

            _kurs = await _repozytorium.PobierzKursAsync(_kursId.Value);

            if (_kurs == null)
            {
                MessageBox.Show("Nie znaleziono kursu.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }

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
            if (!_kursId.HasValue && _ladunki.Count == 0) return;

            if (_kursId.HasValue && _ladunki.Count == 0)
            {
                _ladunki = await LoadLadunkiFromDatabase(_kursId.Value);
            }

            foreach (var ladunek in _ladunki.Where(l => l.KodKlienta?.StartsWith("ZAM_") == true))
            {
                if (int.TryParse(ladunek.KodKlienta.Substring(4), out int zamId))
                {
                    var aktualneZamowienie = await PobierzAktualneZamowienie(zamId);
                    if (aktualneZamowienie != null)
                    {
                        if (ladunek.Palety != aktualneZamowienie.Palety ||
                            ladunek.PojemnikiE2 != aktualneZamowienie.Pojemniki)
                        {
                            ladunek.PoprzedniePalety = ladunek.Palety;
                            ladunek.PoprzedniePojemniki = ladunek.PojemnikiE2;
                            ladunek.Palety = aktualneZamowienie.Palety;
                            ladunek.PojemnikiE2 = aktualneZamowienie.Pojemniki;
                            ladunek.ZmienionyWZamowieniu = true;
                        }
                    }
                }
            }

            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(long));
            dt.Columns.Add("Lp.", typeof(int));
            dt.Columns.Add("Klient", typeof(string));
            dt.Columns.Add("Data uboju", typeof(string));  // NOWE
            dt.Columns.Add("Palety", typeof(decimal));
            dt.Columns.Add("Pojemniki", typeof(int));
            dt.Columns.Add("Adres", typeof(string));
            dt.Columns.Add("Uwagi", typeof(string));
            dt.Columns.Add("Status", typeof(string));

            foreach (var ladunek in _ladunki.OrderBy(l => l.Kolejnosc))
            {
                string klientNazwa = ladunek.NazwaKlienta ?? "";

                if (string.IsNullOrEmpty(klientNazwa))
                {
                    if (!string.IsNullOrEmpty(ladunek.Uwagi))
                    {
                        var idx = ladunek.Uwagi.IndexOf('(');
                        if (idx > 0)
                        {
                            klientNazwa = ladunek.Uwagi.Substring(0, idx).Trim();
                            ladunek.NazwaKlienta = klientNazwa;
                        }
                        else
                        {
                            klientNazwa = ladunek.Uwagi;
                            ladunek.NazwaKlienta = klientNazwa;
                        }
                    }
                    else
                    {
                        klientNazwa = ladunek.KodKlienta ?? "";
                        ladunek.NazwaKlienta = klientNazwa;
                    }
                }

                string adres = ladunek.Adres ?? "";
                if (string.IsNullOrEmpty(adres))
                {
                    adres = await PobierzAdresKlientaAsync(ladunek.KodKlienta);
                    ladunek.Adres = adres;
                }

                // NOWE: Pobierz datę uboju dla zamówienia
                string dataUbojuStr = "---";
                if (ladunek.KodKlienta?.StartsWith("ZAM_") == true &&
                    int.TryParse(ladunek.KodKlienta.Substring(4), out int zamId))
                {
                    if (!ladunek.DataUboju.HasValue)
                    {
                        ladunek.DataUboju = await PobierzDateUbojuAsync(zamId);
                    }
                    if (ladunek.DataUboju.HasValue)
                    {
                        dataUbojuStr = ladunek.DataUboju.Value.ToString("yyyy-MM-dd");
                    }
                }

                string status = "";
                if (ladunek.ZmienionyWZamowieniu)
                {
                    status = "Zaktualizowano (niezapisane)";
                }

                dt.Rows.Add(
                    ladunek.LadunekID,
                    ladunek.Kolejnosc,
                    klientNazwa,
                    dataUbojuStr,  // NOWE
                    ladunek.Palety,
                    ladunek.PojemnikiE2,
                    adres,
                    ladunek.Uwagi ?? "",
                    status
                );
            }

            dgvLadunki.DataSource = dt;

            if (dgvLadunki.Columns["ID"] != null)
                dgvLadunki.Columns["ID"].Visible = false;
            if (dgvLadunki.Columns["Lp."] != null)
                dgvLadunki.Columns["Lp."].Width = 40;
            if (dgvLadunki.Columns["Klient"] != null)
                dgvLadunki.Columns["Klient"].Width = 150;

            // NOWE: Kolumna Data uboju
            if (dgvLadunki.Columns["Data uboju"] != null)
            {
                dgvLadunki.Columns["Data uboju"].Width = 85;
                dgvLadunki.Columns["Data uboju"].DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                dgvLadunki.Columns["Data uboju"].DefaultCellStyle.ForeColor = Color.DarkBlue;
                dgvLadunki.Columns["Data uboju"].DefaultCellStyle.BackColor = Color.FromArgb(230, 240, 255);
            }

            if (dgvLadunki.Columns["Palety"] != null)
            {
                dgvLadunki.Columns["Palety"].Width = 70;
                dgvLadunki.Columns["Palety"].DefaultCellStyle.Format = "N1";
                dgvLadunki.Columns["Palety"].DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }

            if (dgvLadunki.Columns["Pojemniki"] != null)
            {
                dgvLadunki.Columns["Pojemniki"].Width = 80;
                dgvLadunki.Columns["Pojemniki"].DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                dgvLadunki.Columns["Pojemniki"].DefaultCellStyle.ForeColor = Color.Blue;
            }
            if (dgvLadunki.Columns["Adres"] != null)
            {
                dgvLadunki.Columns["Adres"].Width = 180;
                dgvLadunki.Columns["Adres"].DefaultCellStyle.Font = new Font("Segoe UI", 8.5F);
                dgvLadunki.Columns["Adres"].DefaultCellStyle.ForeColor = Color.FromArgb(100, 100, 100);
            }
            if (dgvLadunki.Columns["Status"] != null)
            {
                dgvLadunki.Columns["Status"].Width = 160;
                dgvLadunki.Columns["Status"].DefaultCellStyle.ForeColor = Color.Orange;
                dgvLadunki.Columns["Status"].DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }
            if (dgvLadunki.Columns["Uwagi"] != null)
            {
                dgvLadunki.Columns["Uwagi"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }

            foreach (DataGridViewRow row in dgvLadunki.Rows)
            {
                var statusCell = row.Cells["Status"];
                if (statusCell?.Value?.ToString()?.Contains("niezapisane") == true)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 250, 230);
                }
            }

            UpdateTrasa();
            await UpdateWypelnienie();
        }
        // 8. DODAJ nową metodę pomocniczą:
        private async Task<DateTime?> PobierzDateUbojuAsync(int zamowienieId)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var sql = "SELECT DataUboju FROM dbo.ZamowieniaMieso WHERE Id = @ZamId";
                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@ZamId", zamowienieId);

                var result = await cmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToDateTime(result);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        private async Task<List<LadunekWithPalety>> LoadLadunkiFromDatabase(long kursId)
        {
            var ladunki = new List<LadunekWithPalety>();

            await using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync();

            var sql = @"SELECT LadunekID, KursID, Kolejnosc, KodKlienta, 
                              ISNULL(PaletyH1, 0) AS Palety, 
                              ISNULL(PojemnikiE2, 0) AS Pojemniki,
                              Uwagi, ISNULL(TrybE2, 0) AS TrybE2
                       FROM dbo.Ladunek 
                       WHERE KursID = @KursID
                       ORDER BY Kolejnosc";

            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@KursID", kursId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var ladunek = new LadunekWithPalety
                {
                    LadunekID = reader.GetInt64(0),
                    KursID = reader.GetInt64(1),
                    Kolejnosc = reader.GetInt32(2),
                    KodKlienta = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Palety = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetInt32(4)),
                    PojemnikiE2 = reader.GetInt32(5),
                    Uwagi = reader.IsDBNull(6) ? null : reader.GetString(6),
                    TrybE2 = reader.GetBoolean(7)
                };

                if (ladunek.KodKlienta?.StartsWith("ZAM_") == true)
                {
                    if (int.TryParse(ladunek.KodKlienta.Substring(4), out int zamId))
                    {
                        if (_zamowieniaDoUsuniecia.Contains(zamId))
                            continue;
                    }
                }

                ladunki.Add(ladunek);
            }

            return ladunki;
        }

        private async Task LoadWolneZamowienia()
        {
            await LoadWolneZamowieniaForDate(_dtpZamowienia?.Value ?? dtpData.Value);
        }

        private async Task LoadWolneZamowieniaForDate(DateTime data)
        {
            try
            {
                _wolneZamowienia.Clear();

                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var dataOd = data.Date;
                var dataDo = data.Date.AddDays(2);

                // ZMIENIONE: Dynamiczne wybieranie kolumny
                string dataKolumna = UzywajDataUboju ? "zm.DataUboju" : "zm.DataZamowienia";

                var sql = $@"
            SELECT DISTINCT
                zm.Id AS ZamowienieId,
                zm.KlientId,
                zm.DataPrzyjazdu,
                zm.Status,
                zm.Uwagi,
                ISNULL(zm.LiczbaPalet, 0) AS LiczbaPalet,
                ISNULL(zm.LiczbaPojemnikow, 0) AS LiczbaPojemnikow,
                ISNULL(zm.TrybE2, 0) AS TrybE2,
                SUM(ISNULL(zmt.Ilosc, 0)) AS IloscKg,
                ISNULL(zm.TransportStatus, 'Oczekuje') AS TransportStatus,
                zm.DataZamowienia,
                zm.DataUboju
            FROM dbo.ZamowieniaMieso zm
            LEFT JOIN dbo.ZamowieniaMiesoTowar zmt ON zm.Id = zmt.ZamowienieId
            WHERE {dataKolumna} >= @DataOd AND {dataKolumna} <= @DataDo
              AND ISNULL(zm.Status, 'Nowe') NOT IN ('Anulowane')
            GROUP BY zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.Status, zm.Uwagi,
                     zm.LiczbaPalet, zm.LiczbaPojemnikow, zm.TrybE2, zm.TransportStatus, 
                     zm.DataZamowienia, zm.DataUboju
            ORDER BY {dataKolumna}, zm.DataPrzyjazdu";

                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DataOd", dataOd);
                cmd.Parameters.AddWithValue("@DataDo", dataDo);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var transportStatus = reader.GetString(9);
                    var zamId = reader.GetInt32(0);

                    bool jestWLiscieDoDodania = _zamowieniaDoDodania.Any(z => z.ZamowienieId == zamId);

                    if ((transportStatus == "Oczekuje" || string.IsNullOrEmpty(transportStatus) || _zamowieniaDoUsuniecia.Contains(zamId))
                        && transportStatus != "Własny"  // NOWE: wykluczenie własnego transportu
                        && !jestWLiscieDoDodania)
                    {
                        var zamowienie = new ZamowienieDoTransportu
                        {
                            ZamowienieId = zamId,
                            KlientId = reader.GetInt32(1),
                            DataPrzyjazdu = reader.GetDateTime(2),
                            Status = reader.IsDBNull(3) ? "Nowe" : reader.GetString(3),
                            Palety = reader.GetDecimal(5),
                            Pojemniki = reader.GetInt32(6),
                            TrybE2 = reader.GetBoolean(7),
                            IloscKg = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                            DataOdbioru = reader.GetDateTime(10),
                            DataUboju = reader.IsDBNull(11) ? null : reader.GetDateTime(11)  // NOWE
                        };
                        _wolneZamowienia.Add(zamowienie);
                    }
                }

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

                ShowZamowieniaInGrid();

                if (lblZamowieniaInfo != null)
                {
                    string tryb = UzywajDataUboju ? "Data uboju" : "Data odbioru";
                    lblZamowieniaInfo.Text = $"ZAMÓWIENIA ({tryb}):";
                }

                if (_lblLiczbaZamowien != null)
                {
                    _lblLiczbaZamowien.Text = $"{_wolneZamowienia.Count} zamówień";

                    if (_wolneZamowienia.Count == 0)
                        _lblLiczbaZamowien.ForeColor = Color.Gray;
                    else if (_wolneZamowienia.Count > 15)
                        _lblLiczbaZamowien.ForeColor = Color.OrangeRed;
                    else
                        _lblLiczbaZamowien.ForeColor = Color.FromArgb(52, 152, 219);
                }
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
            dt.Columns.Add("Data odbioru", typeof(DateTime));
            dt.Columns.Add("Data uboju", typeof(string));  // NOWE
            dt.Columns.Add("Klient", typeof(string));
            dt.Columns.Add("Godz.", typeof(string));
            dt.Columns.Add("Palety", typeof(decimal));
            dt.Columns.Add("Pojemniki", typeof(int));
            dt.Columns.Add("Status", typeof(string));
            dt.Columns.Add("Handlowiec", typeof(string));
            dt.Columns.Add("Adres", typeof(string));

            foreach (var zam in _wolneZamowienia.OrderBy(z => z.DataOdbioru).ThenBy(z => z.DataPrzyjazdu))
            {
                string dataUbojuStr = zam.DataUboju.HasValue
                    ? zam.DataUboju.Value.ToString("yyyy-MM-dd")
                    : "---";

                dt.Rows.Add(
                    zam.ZamowienieId,
                    zam.DataOdbioru,
                    dataUbojuStr,  // NOWE
                    zam.KlientNazwa,
                    zam.GodzinaStr,
                    zam.Palety,
                    zam.Pojemniki,
                    zam.Status,
                    zam.Handlowiec,
                    zam.Adres
                );
            }

            dgvWolneZamowienia.DataSource = dt;

            if (dgvWolneZamowienia.Columns["ID"] != null)
                dgvWolneZamowienia.Columns["ID"].Visible = false;

            if (dgvWolneZamowienia.Columns["Data odbioru"] != null)
            {
                dgvWolneZamowienia.Columns["Data odbioru"].Width = 85;
                dgvWolneZamowienia.Columns["Data odbioru"].DefaultCellStyle.Format = "yyyy-MM-dd";
                dgvWolneZamowienia.Columns["Data odbioru"].DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                dgvWolneZamowienia.Columns["Data odbioru"].DisplayIndex = 0;
            }

            // NOWE: Kolumna Data uboju
            if (dgvWolneZamowienia.Columns["Data uboju"] != null)
            {
                dgvWolneZamowienia.Columns["Data uboju"].Width = 85;
                dgvWolneZamowienia.Columns["Data uboju"].DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                dgvWolneZamowienia.Columns["Data uboju"].DefaultCellStyle.ForeColor = Color.DarkBlue;
                dgvWolneZamowienia.Columns["Data uboju"].DefaultCellStyle.BackColor = Color.FromArgb(230, 240, 255);
                dgvWolneZamowienia.Columns["Data uboju"].DisplayIndex = 1;
            }

            if (dgvWolneZamowienia.Columns["Klient"] != null)
            {
                dgvWolneZamowienia.Columns["Klient"].Width = 140;
                dgvWolneZamowienia.Columns["Klient"].DisplayIndex = 2;
            }

            // ... reszta konfiguracji kolumn bez zmian, tylko zmień DisplayIndex o +1
            if (dgvWolneZamowienia.Columns["Godz."] != null)
            {
                dgvWolneZamowienia.Columns["Godz."].Width = 50;
                dgvWolneZamowienia.Columns["Godz."].DisplayIndex = 3;
            }
            if (dgvWolneZamowienia.Columns["Palety"] != null)
            {
                dgvWolneZamowienia.Columns["Palety"].Width = 60;
                dgvWolneZamowienia.Columns["Palety"].DefaultCellStyle.Format = "N1";
                dgvWolneZamowienia.Columns["Palety"].DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                dgvWolneZamowienia.Columns["Palety"].DisplayIndex = 4;
            }
            if (dgvWolneZamowienia.Columns["Pojemniki"] != null)
            {
                dgvWolneZamowienia.Columns["Pojemniki"].Width = 70;
                dgvWolneZamowienia.Columns["Pojemniki"].DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                dgvWolneZamowienia.Columns["Pojemniki"].DefaultCellStyle.ForeColor = Color.Blue;
                dgvWolneZamowienia.Columns["Pojemniki"].DisplayIndex = 5;
            }
            if (dgvWolneZamowienia.Columns["Status"] != null)
            {
                dgvWolneZamowienia.Columns["Status"].Width = 100;
                dgvWolneZamowienia.Columns["Status"].DisplayIndex = 6;
            }
            if (dgvWolneZamowienia.Columns["Handlowiec"] != null)
            {
                dgvWolneZamowienia.Columns["Handlowiec"].Width = 100;
                dgvWolneZamowienia.Columns["Handlowiec"].DisplayIndex = 7;
            }
            if (dgvWolneZamowienia.Columns["Adres"] != null)
            {
                dgvWolneZamowienia.Columns["Adres"].Width = 150;
                dgvWolneZamowienia.Columns["Adres"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dgvWolneZamowienia.Columns["Adres"].DisplayIndex = 8;
            }

            // Kolorowanie wierszy - użyj DataUboju gdy dostępna
            foreach (DataGridViewRow row in dgvWolneZamowienia.Rows)
            {
                var zamId = Convert.ToInt32(row.Cells["ID"].Value);
                var zamowienie = _wolneZamowienia.FirstOrDefault(z => z.ZamowienieId == zamId);

                if (zamowienie != null)
                {
                    var dataDoPorownan = UzywajDataUboju && zamowienie.DataUboju.HasValue
                        ? zamowienie.DataUboju.Value.Date
                        : zamowienie.DataOdbioru.Date;

                    var dzis = DateTime.Today;

                    if (dataDoPorownan == dzis)
                    {
                        row.DefaultCellStyle.BackColor = Color.White;
                    }
                    else if (dataDoPorownan == dzis.AddDays(1))
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 253, 230);
                    }
                    else if (dataDoPorownan == dzis.AddDays(2))
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(230, 240, 255);
                    }
                    else if (dataDoPorownan < dzis)
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 230, 230);
                        row.DefaultCellStyle.ForeColor = Color.DarkRed;
                    }

                    if (zamowienie.Status == "Zrealizowane")
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(220, 255, 220);
                        row.DefaultCellStyle.ForeColor = Color.DarkGreen;
                    }
                }
            }
        }
        #endregion

        #region Operacje na ładunkach

        private async Task DodajLadunekRecznyZZamowieniem()
        {
            if (cboKlient.SelectedItem is not KontrahentInfo kontrahent)
            {
                MessageBox.Show("Wybierz kontrahenta z listy.", "Brak danych",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cboKlient.Focus();
                return;
            }

            if (nudPalety.Value <= 0)
            {
                MessageBox.Show("Podaj liczbę palet (większą niż 0).", "Brak danych",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nudPalety.Focus();
                return;
            }

            TimeSpan? godzinaPrzyjazdu = null;
            if (TimeSpan.TryParse(txtGodzina.Text, out var godz))
            {
                godzinaPrzyjazdu = godz;
            }

            try
            {
                Cursor = Cursors.WaitCursor;

                int noweZamowienieId = await UtworzZamowienieAsync(
                    kontrahent,
                    nudPalety.Value,
                    godzinaPrzyjazdu,
                    txtUwagi.Text.Trim());

                if (noweZamowienieId <= 0)
                {
                    MessageBox.Show("Nie udało się utworzyć zamówienia.", "Błąd",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                int pojemniki = (int)(nudPalety.Value * 36);

                var ladunek = new LadunekWithPalety
                {
                    LadunekID = -(noweZamowienieId + 1000),
                    KursID = _kursId ?? 0,
                    KodKlienta = $"ZAM_{noweZamowienieId}",
                    NazwaKlienta = kontrahent.Nazwa,
                    Palety = nudPalety.Value,
                    PojemnikiE2 = pojemniki,
                    TrybE2 = false,
                    Uwagi = string.IsNullOrWhiteSpace(txtUwagi.Text)
                        ? $"{kontrahent.Nazwa} ({godzinaPrzyjazdu?.ToString(@"hh\:mm") ?? "08:00"})"
                        : txtUwagi.Text.Trim(),
                    Adres = kontrahent.Adres,
                    Kolejnosc = _ladunki.Count + 1
                };

                _ladunki.Add(ladunek);

                var zamowienieDoTransportu = new ZamowienieDoTransportu
                {
                    ZamowienieId = noweZamowienieId,
                    KlientId = kontrahent.Id,
                    KlientNazwa = kontrahent.Nazwa,
                    Palety = nudPalety.Value,
                    Pojemniki = pojemniki,
                    TrybE2 = false,
                    DataPrzyjazdu = dtpData.Value.Date.Add(godzinaPrzyjazdu ?? TimeSpan.FromHours(8)),
                    DataOdbioru = dtpData.Value.Date,
                    Status = "Nowe",
                    Handlowiec = kontrahent.Handlowiec,
                    Adres = kontrahent.Adres
                };

                _zamowieniaDoDodania.Add(zamowienieDoTransportu);

                await LoadLadunki();
                await LoadWolneZamowienia();

                cboKlient.SelectedIndex = -1;
                nudPalety.Value = 0;
                txtGodzina.Text = "08:00";
                txtUwagi.Clear();
                cboKlient.Focus();

                var message = $"Utworzono zamówienie #{noweZamowienieId}\n" +
                              $"Dodano do kursu: {kontrahent.Nazwa}\n" +
                              $"{nudPalety.Value:N1} palet ({pojemniki} pojemników)\n\n" +
                              $"Pamiętaj: Kliknij 'ZAPISZ' aby zachować wszystkie zmiany!";

                MessageBox.Show(message, "Zamówienie utworzone",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas tworzenia zamówienia: {ex.Message}\n\n{ex.StackTrace}",
                    "Błąd krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private async Task<int> UtworzZamowienieAsync(
            KontrahentInfo kontrahent,
            decimal liczbaPalet,
            TimeSpan? godzinaPrzyjazdu,
            string uwagi)
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            using var transaction = cn.BeginTransaction();

            try
            {
                var sqlGetId = "SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.ZamowieniaMieso";
                await using var cmdId = new SqlCommand(sqlGetId, cn, transaction);
                int noweId = Convert.ToInt32(await cmdId.ExecuteScalarAsync());

                int liczbaPojemnikow = (int)(liczbaPalet * 36);
                var dataPrzyjazdu = dtpData.Value.Date.Add(godzinaPrzyjazdu ?? TimeSpan.FromHours(8));

                var sqlInsert = @"
                    INSERT INTO dbo.ZamowieniaMieso (
                        Id, DataZamowienia, DataPrzyjazdu, KlientId, 
                        Status, TransportStatus, TransportKursId,
                        LiczbaPalet, LiczbaPojemnikow, TrybE2,
                        Uwagi, IdUser, DataUtworzenia
                    ) VALUES (
                        @Id, @DataZamowienia, @DataPrzyjazdu, @KlientId,
                        @Status, @TransportStatus, @TransportKursId,
                        @LiczbaPalet, @LiczbaPojemnikow, @TrybE2,
                        @Uwagi, @IdUser, SYSDATETIME()
                    )";

                await using var cmdInsert = new SqlCommand(sqlInsert, cn, transaction);
                cmdInsert.Parameters.AddWithValue("@Id", noweId);
                cmdInsert.Parameters.AddWithValue("@DataZamowienia", dtpData.Value.Date);
                cmdInsert.Parameters.AddWithValue("@DataPrzyjazdu", dataPrzyjazdu);
                cmdInsert.Parameters.AddWithValue("@KlientId", kontrahent.Id);
                cmdInsert.Parameters.AddWithValue("@Status", "Nowe");
                cmdInsert.Parameters.AddWithValue("@TransportStatus", "Oczekuje");
                cmdInsert.Parameters.AddWithValue("@TransportKursId",
                    _kursId.HasValue && _kursId.Value > 0 ? (object)_kursId.Value : DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@LiczbaPalet", liczbaPalet);
                cmdInsert.Parameters.AddWithValue("@LiczbaPojemnikow", liczbaPojemnikow);
                cmdInsert.Parameters.AddWithValue("@TrybE2", false);
                cmdInsert.Parameters.AddWithValue("@Uwagi",
                    string.IsNullOrWhiteSpace(uwagi)
                        ? (object)$"Utworzone ręcznie z transportu przez {UserID ?? _uzytkownik}"
                        : uwagi);
                cmdInsert.Parameters.AddWithValue("@IdUser", UserID ?? _uzytkownik);

                await cmdInsert.ExecuteNonQueryAsync();

                transaction.Commit();

                return noweId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private async Task DodajZamowienieDoKursu()
        {
            if (dgvWolneZamowienia.CurrentRow == null) return;

            var zamId = Convert.ToInt32(dgvWolneZamowienia.CurrentRow.Cells["ID"].Value);
            var zamowienie = _wolneZamowienia.FirstOrDefault(z => z.ZamowienieId == zamId);
            if (zamowienie == null) return;

            _zamowieniaDoDodania.Add(zamowienie);

            _zamowieniaDoUsuniecia.Remove(zamId);

            var ladunek = new LadunekWithPalety
            {
                LadunekID = -(zamId + 1000),
                KursID = _kursId ?? 0,
                KodKlienta = $"ZAM_{zamowienie.ZamowienieId}",
                Palety = zamowienie.Palety,
                PojemnikiE2 = zamowienie.Pojemniki,
                TrybE2 = zamowienie.TrybE2,
                Uwagi = $"{zamowienie.KlientNazwa} ({zamowienie.GodzinaStr})",
                Adres = zamowienie.Adres,
                NazwaKlienta = zamowienie.KlientNazwa,
                Kolejnosc = _ladunki.Count + 1
            };

            _ladunki.Add(ladunek);

            _wolneZamowienia.Remove(zamowienie);
            ShowZamowieniaInGrid();
            await LoadLadunki();
        }

        private async Task UsunLadunek()
        {
            if (dgvLadunki.CurrentRow == null) return;

            var ladunekId = Convert.ToInt64(dgvLadunki.CurrentRow.Cells["ID"].Value);
            var ladunek = _ladunki.FirstOrDefault(l => l.LadunekID == ladunekId);
            if (ladunek == null) return;

            if (ladunek.KodKlienta?.StartsWith("ZAM_") == true)
            {
                var zamId = int.Parse(ladunek.KodKlienta.Substring(4));

                var zamDoDodania = _zamowieniaDoDodania.FirstOrDefault(z => z.ZamowienieId == zamId);
                if (zamDoDodania != null)
                {
                    _zamowieniaDoDodania.Remove(zamDoDodania);
                }
                else
                {
                    if (ladunekId > 0)
                    {
                        _zamowieniaDoUsuniecia.Add(zamId);
                    }
                }

                var zamowienie = await PobierzAktualneZamowienie(zamId);
                if (zamowienie != null)
                {
                    _wolneZamowienia.Add(zamowienie);
                }
            }

            _ladunki.Remove(ladunek);

            for (int i = 0; i < _ladunki.Count; i++)
            {
                _ladunki[i].Kolejnosc = i + 1;
            }

            ShowZamowieniaInGrid();
            await LoadLadunki();
        }

        private async Task PrzesunLadunek(int kierunek)
        {
            if (dgvLadunki.CurrentRow == null) return;

            var ladunekId = Convert.ToInt64(dgvLadunki.CurrentRow.Cells["ID"].Value);
            var ladunek = _ladunki.FirstOrDefault(l => l.LadunekID == ladunekId);
            if (ladunek == null) return;

            int currentIndex = _ladunki.IndexOf(ladunek);
            int newIndex = currentIndex + kierunek;

            if (newIndex < 0 || newIndex >= _ladunki.Count) return;

            var temp = _ladunki[currentIndex];
            _ladunki[currentIndex] = _ladunki[newIndex];
            _ladunki[newIndex] = temp;

            for (int i = 0; i < _ladunki.Count; i++)
            {
                _ladunki[i].Kolejnosc = i + 1;
            }

            await LoadLadunki();

            foreach (DataGridViewRow row in dgvLadunki.Rows)
            {
                if (Convert.ToInt64(row.Cells["ID"].Value) == ladunekId)
                {
                    row.Selected = true;
                    dgvLadunki.CurrentCell = row.Cells[1];
                    break;
                }
            }
        }

        private async Task OtworzDialogKolejnosci()
        {
            using var dlg = new KolejnoscLadunkuDialog(_ladunki);

            dlg.StartPosition = FormStartPosition.Manual;
            var leftPanelWidth = this.Width * 0.55;
            var centerX = this.Left + (int)(leftPanelWidth / 2) - (dlg.Width / 2);
            var centerY = this.Top + 250;

            var screen = Screen.FromControl(this);
            if (centerX < 20) centerX = 20;
            var maxRightPosition = this.Left + (int)leftPanelWidth - dlg.Width - 20;
            if (centerX > maxRightPosition) centerX = maxRightPosition;
            if (centerY < 20) centerY = 20;
            if (centerY + dlg.Height > screen.WorkingArea.Bottom)
                centerY = screen.WorkingArea.Bottom - dlg.Height - 20;

            dlg.Location = new Point(centerX, centerY);

            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ZmienioneKolejnosci != null)
            {
                _ladunki = dlg.ZmienioneKolejnosci;
                await LoadLadunki();
            }
        }

        private void EdytujLadunek()
        {
            if (dgvLadunki.CurrentRow == null) return;

            var row = dgvLadunki.CurrentRow;

            var klient = _kontrahenci.FirstOrDefault(k => k.Nazwa == row.Cells["Klient"].Value?.ToString());
            if (klient != null)
            {
                cboKlient.SelectedItem = klient;
            }

            nudPalety.Value = Convert.ToDecimal(row.Cells["Palety"].Value ?? 0);
            txtUwagi.Text = row.Cells["Uwagi"].Value?.ToString() ?? "";

            _ = UsunLadunek();
        }

        #endregion

        #region Pomocnicze metody

        private async Task CheckForZamowieniaUpdates()
        {
            if (!_dataLoaded) return;

            try
            {
                bool anyChanges = false;
                var zmienioneLadunki = new List<LadunekWithPalety>();

                foreach (var ladunek in _ladunki.Where(l => l.KodKlienta?.StartsWith("ZAM_") == true))
                {
                    if (int.TryParse(ladunek.KodKlienta.Substring(4), out int zamId))
                    {
                        var aktualneZamowienie = await PobierzAktualneZamowienie(zamId);

                        if (aktualneZamowienie != null)
                        {
                            if (ladunek.Palety != aktualneZamowienie.Palety ||
                                ladunek.PojemnikiE2 != aktualneZamowienie.Pojemniki)
                            {
                                ladunek.PoprzedniePalety = ladunek.Palety;
                                ladunek.PoprzedniePojemniki = ladunek.PojemnikiE2;

                                ladunek.Palety = aktualneZamowienie.Palety;
                                ladunek.PojemnikiE2 = aktualneZamowienie.Pojemniki;
                                ladunek.ZmienionyWZamowieniu = true;

                                zmienioneLadunki.Add(ladunek);
                                anyChanges = true;
                            }
                        }
                    }
                }

                if (anyChanges)
                {
                    await LoadLadunki();
                    await UpdateWypelnienie();

                    if (lblAutoUpdate != null)
                    {
                        lblAutoUpdate.Text = $"Zaktualizowano {zmienioneLadunki.Count} pozycji z zamówień (nie zapisano)";
                        lblAutoUpdate.ForeColor = Color.Orange;
                        lblAutoUpdate.Visible = true;

                        var hideTimer = new Timer { Interval = 5000 };
                        hideTimer.Tick += (s, e) =>
                        {
                            lblAutoUpdate.Visible = false;
                            hideTimer.Stop();
                            hideTimer.Dispose();
                        };
                        hideTimer.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas automatycznej aktualizacji: {ex.Message}");
            }
        }

        private async Task<ZamowienieDoTransportu> PobierzAktualneZamowienie(int zamowienieId)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var sql = @"
                    SELECT 
                        zm.Id AS ZamowienieId,
                        zm.KlientId,
                        zm.DataPrzyjazdu,
                        zm.Status,
                        ISNULL(zm.LiczbaPalet, 0) AS LiczbaPalet,
                        ISNULL(zm.LiczbaPojemnikow, 0) AS LiczbaPojemnikow,
                        ISNULL(zm.TrybE2, 0) AS TrybE2,
                        SUM(ISNULL(zmt.Ilosc, 0)) AS IloscKg
                    FROM dbo.ZamowieniaMieso zm
                    LEFT JOIN dbo.ZamowieniaMiesoTowar zmt ON zm.Id = zmt.ZamowienieId
                    WHERE zm.Id = @ZamowienieId
                    GROUP BY zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.Status, 
                             zm.LiczbaPalet, zm.LiczbaPojemnikow, zm.TrybE2";

                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@ZamowienieId", zamowienieId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new ZamowienieDoTransportu
                    {
                        ZamowienieId = reader.GetInt32(0),
                        KlientId = reader.GetInt32(1),
                        DataPrzyjazdu = reader.GetDateTime(2),
                        Status = reader.IsDBNull(3) ? "Nowe" : reader.GetString(3),
                        Palety = reader.GetDecimal(4),
                        Pojemniki = reader.GetInt32(5),
                        TrybE2 = reader.GetBoolean(6),
                        IloscKg = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7)
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd pobierania zamówienia {zamowienieId}: {ex.Message}");
                return null;
            }
        }

        private async Task<string> PobierzAdresKlientaAsync(string kodKlienta)
        {
            try
            {
                if (kodKlienta?.StartsWith("ZAM_") == true && int.TryParse(kodKlienta.Substring(4), out int zamId))
                {
                    await using var cnLibra = new SqlConnection(_connLibra);
                    await cnLibra.OpenAsync();

                    var sqlZam = "SELECT KlientId FROM dbo.ZamowieniaMieso WHERE Id = @ZamId";
                    using var cmdZam = new SqlCommand(sqlZam, cnLibra);
                    cmdZam.Parameters.AddWithValue("@ZamId", zamId);

                    var klientIdObj = await cmdZam.ExecuteScalarAsync();
                    if (klientIdObj != null)
                    {
                        int klientId = Convert.ToInt32(klientIdObj);

                        await using var cnHandel = new SqlConnection(_connHandel);
                        await cnHandel.OpenAsync();

                        var sqlAdres = @"
                            SELECT ISNULL(poa.Postcode, '') + ' ' + ISNULL(poa.Street, '') AS Adres
                            FROM SSCommon.STContractors c
                            LEFT JOIN SSCommon.STPostOfficeAddresses poa ON poa.ContactGuid = c.ContactGuid 
                                AND poa.AddressName = N'adres domyślny'
                            WHERE c.Id = @KlientId";

                        using var cmdAdres = new SqlCommand(sqlAdres, cnHandel);
                        cmdAdres.Parameters.AddWithValue("@KlientId", klientId);

                        var adres = await cmdAdres.ExecuteScalarAsync();
                        return adres?.ToString()?.Trim() ?? "";
                    }
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        private void UpdateTrasa()
        {
            var klientNazwy = new List<string>();

            foreach (var ladunek in _ladunki.OrderBy(l => l.Kolejnosc))
            {
                string nazwa = "";

                if (!string.IsNullOrEmpty(ladunek.NazwaKlienta))
                {
                    nazwa = ladunek.NazwaKlienta;
                }
                else if (!string.IsNullOrEmpty(ladunek.Uwagi))
                {
                    var idx = ladunek.Uwagi.IndexOf('(');
                    if (idx > 0)
                    {
                        nazwa = ladunek.Uwagi.Substring(0, idx).Trim();
                    }
                    else
                    {
                        nazwa = ladunek.Uwagi;
                    }
                }
                else if (!string.IsNullOrEmpty(ladunek.KodKlienta))
                {
                    nazwa = ladunek.KodKlienta;
                }

                if (!string.IsNullOrEmpty(nazwa))
                {
                    klientNazwy.Add(nazwa);
                }
            }

            if (klientNazwy.Count == 0)
            {
                txtTrasa.Text = "";
            }
            else
            {
                txtTrasa.Text = string.Join(" → ", klientNazwy.Distinct());
            }
        }

        private async Task UpdateWypelnienie()
        {
            try
            {
                if (cboPojazd.SelectedItem is not Pojazd pojazd)
                {
                    progressWypelnienie.Value = 0;
                    lblStatystyki.Text = "0 palet z 0 dostępnych";
                    return;
                }

                decimal sumaPalet = _ladunki?.Sum(l => l.Palety) ?? 0m;
                int paletyPojazdu = pojazd.PaletyH1;
                decimal procent = paletyPojazdu > 0 ? (sumaPalet / paletyPojazdu * 100m) : 0m;

                progressWypelnienie.Value = Math.Min(100, (int)procent);
                lblStatystyki.Text = $"{sumaPalet:N1} palet z {paletyPojazdu} dostępnych ({procent:N1}%)";

                if (procent > 100)
                {
                    progressWypelnienie.ForeColor = Color.Red;
                    lblWypelnienie.ForeColor = Color.Red;
                    lblWypelnienie.Text = $"PRZEPEŁNIENIE: {procent:F1}%";
                }
                else if (procent > 90)
                {
                    progressWypelnienie.ForeColor = Color.Orange;
                    lblWypelnienie.ForeColor = Color.Orange;
                    lblWypelnienie.Text = $"Wypełnienie: {procent:F1}% (mało miejsca)";
                }
                else
                {
                    progressWypelnienie.ForeColor = Color.Green;
                    lblWypelnienie.ForeColor = Color.Green;
                    lblWypelnienie.Text = $"Wypełnienie: {procent:F1}%";
                }
            }
            catch
            {
                // Ignoruj błędy
            }
        }

        private async Task OdswiezZamowienie()
        {
            if (dgvLadunki.CurrentRow == null) return;

            var ladunekId = Convert.ToInt64(dgvLadunki.CurrentRow.Cells["ID"].Value);
            var ladunek = _ladunki.FirstOrDefault(l => l.LadunekID == ladunekId);

            if (ladunek?.KodKlienta?.StartsWith("ZAM_") == true)
            {
                var zamId = int.Parse(ladunek.KodKlienta.Substring(4));
                var aktualneZamowienie = await PobierzAktualneZamowienie(zamId);

                if (aktualneZamowienie != null)
                {
                    ladunek.Palety = aktualneZamowienie.Palety;
                    ladunek.PojemnikiE2 = aktualneZamowienie.Pojemniki;
                    await LoadLadunki();

                    MessageBox.Show("Dane zamówienia zostały zaktualizowane (niezapisane).", "Odświeżono",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private async Task EdytujWolneZamowienie()
        {
            if (dgvWolneZamowienia.CurrentRow == null) return;

            var zamId = Convert.ToInt32(dgvWolneZamowienia.CurrentRow.Cells["ID"].Value);
            var zamowienie = _wolneZamowienia.FirstOrDefault(z => z.ZamowienieId == zamId);
            if (zamowienie == null) return;

            var widokZamowienia = new WidokZamowienia(UserID ?? _uzytkownik, zamowienie.ZamowienieId);
            if (widokZamowienia.ShowDialog() == DialogResult.OK)
            {
                await LoadWolneZamowienia();
            }
        }
        private async Task PokazSzczegolyZamowienia()
        {
            if (dgvWolneZamowienia.CurrentRow == null) return;

            var zamId = Convert.ToInt32(dgvWolneZamowienia.CurrentRow.Cells["ID"].Value);
            var zamowienie = _wolneZamowienia.FirstOrDefault(z => z.ZamowienieId == zamId);
            if (zamowienie == null) return;

            var szczegoly = $@"
Zamówienie #{zamowienie.ZamowienieId}
Klient: {zamowienie.KlientNazwa}
Data przyjazdu: {zamowienie.DataPrzyjazdu:yyyy-MM-dd HH:mm}
Status: {zamowienie.Status}
Palety: {zamowienie.Palety:N1}
Pojemniki: {zamowienie.Pojemniki}
Ilość kg: {zamowienie.IloscKg:N0}
Tryb E2: {(zamowienie.TrybE2 ? "TAK" : "NIE")}
Handlowiec: {zamowienie.Handlowiec}
Adres: {zamowienie.Adres}";

            MessageBox.Show(szczegoly, "Szczegóły zamówienia",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task EdytujPrzypisaneZamowienie()
        {
            if (dgvLadunki.CurrentRow == null) return;

            var ladunekId = Convert.ToInt64(dgvLadunki.CurrentRow.Cells["ID"].Value);
            var ladunek = _ladunki.FirstOrDefault(l => l.LadunekID == ladunekId);

            if (ladunek?.KodKlienta?.StartsWith("ZAM_") == true)
            {
                var zamId = int.Parse(ladunek.KodKlienta.Substring(4));

                var widokZamowienia = new WidokZamowienia(UserID ?? _uzytkownik, zamId);
                if (widokZamowienia.ShowDialog() == DialogResult.OK)
                {
                    await CheckForZamowieniaUpdates();
                    await LoadWolneZamowienia();
                }
            }
            else
            {
                MessageBox.Show("Ta pozycja nie jest powiązana z zamówieniem.",
                    "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        #region Zapis

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

                if (!_kursId.HasValue || _kursId.Value <= 0)
                {
                    MessageBox.Show("Błąd podczas zapisu kursu.", "Błąd",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                await SaveAllLadunkiToDatabase();

                await UpdateAllTransportStatuses();

                _autoUpdateTimer?.Stop();
                _autoUpdateTimer?.Dispose();

                MessageBox.Show("Kurs został pomyślnie zapisany.", "Sukces",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

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
                Text = "Edycja kursu transportowego";
            }
        }

        private async Task SaveAllLadunkiToDatabase()
        {
            if (!_kursId.HasValue || _kursId.Value <= 0)
                return;

            await using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync();

            var sqlDelete = "DELETE FROM dbo.Ladunek WHERE KursID = @KursID";
            using var cmdDelete = new SqlCommand(sqlDelete, cn);
            cmdDelete.Parameters.AddWithValue("@KursID", _kursId.Value);
            await cmdDelete.ExecuteNonQueryAsync();

            foreach (var ladunek in _ladunki.OrderBy(l => l.Kolejnosc))
            {
                var sqlInsert = @"INSERT INTO dbo.Ladunek 
                    (KursID, Kolejnosc, KodKlienta, PaletyH1, PojemnikiE2, Uwagi, TrybE2) 
                    VALUES (@KursID, @Kolejnosc, @KodKlienta, @Palety, @Pojemniki, @Uwagi, @TrybE2)";

                using var cmdInsert = new SqlCommand(sqlInsert, cn);
                cmdInsert.Parameters.AddWithValue("@KursID", _kursId.Value);
                cmdInsert.Parameters.AddWithValue("@Kolejnosc", ladunek.Kolejnosc);
                cmdInsert.Parameters.AddWithValue("@KodKlienta", (object)ladunek.KodKlienta ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@Palety", ladunek.Palety);
                cmdInsert.Parameters.AddWithValue("@Pojemniki", ladunek.PojemnikiE2);
                cmdInsert.Parameters.AddWithValue("@Uwagi", (object)ladunek.Uwagi ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@TrybE2", ladunek.TrybE2);
                await cmdInsert.ExecuteNonQueryAsync();
            }
        }

        private async Task UpdateAllTransportStatuses()
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Pobierz informacje o kursie do logowania
                string kursInfo = "";
                if (_kurs != null)
                {
                    string kierowca = _kurs.KierowcaNazwa ?? "Nieprzypisany";
                    string pojazd = _kurs.PojazdRejestracja ?? "Nieprzypisany";
                    kursInfo = $"Kurs #{_kursId}: {kierowca}, {pojazd}";
                }

                foreach (var zamId in _zamowieniaDoUsuniecia)
                {
                    var sql = @"UPDATE dbo.ZamowieniaMieso
                               SET TransportStatus = 'Oczekuje', TransportKursId = NULL
                               WHERE Id = @ZamowienieId";

                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@ZamowienieId", zamId);
                    await cmd.ExecuteNonQueryAsync();

                    // Loguj usunięcie z kursu
                    await HistoriaZmianService.LogujEdycje(zamId, _uzytkownik, App.UserFullName,
                        "Transport - usunięcie z kursu",
                        kursInfo,
                        "Oczekuje na przypisanie",
                        $"Zamówienie usunięte z kursu transportowego");
                }

                foreach (var zam in _zamowieniaDoDodania)
                {
                    var sql = @"UPDATE dbo.ZamowieniaMieso
                               SET TransportStatus = 'Przypisany', TransportKursId = @KursId
                               WHERE Id = @ZamowienieId";

                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@ZamowienieId", zam.ZamowienieId);
                    cmd.Parameters.AddWithValue("@KursId", _kursId.Value);
                    await cmd.ExecuteNonQueryAsync();

                    // Loguj przypisanie do kursu
                    await HistoriaZmianService.LogujEdycje(zam.ZamowienieId, _uzytkownik, App.UserFullName,
                        "Transport - przypisanie do kursu",
                        "Oczekuje na przypisanie",
                        kursInfo,
                        $"Zamówienie przypisane do kursu transportowego");
                }

                _zamowieniaDoDodania.Clear();
                _zamowieniaDoUsuniecia.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas aktualizacji statusów: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void BtnNowyKierowca_Click(object sender, EventArgs e)
        {
            using var dlg = new DodajKierowceDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _ = Task.Run(async () =>
                {
                    await _repozytorium.DodajKierowceAsync(dlg.NowyKierowca);
                    await LoadDataAsync();
                });
            }
        }

        private void BtnNowyPojazd_Click(object sender, EventArgs e)
        {
            using var dlg = new DodajPojazdDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _ = Task.Run(async () =>
                {
                    await _repozytorium.DodajPojazdAsync(dlg.NowyPojazd);
                    await LoadDataAsync();
                });
            }
        }

        #endregion

        private string _connectionString => "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoUpdateTimer?.Stop();
                _autoUpdateTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class KolejnoscLadunkuDialog : Form
    {
        public List<EdytorKursuWithPalety.LadunekWithPalety> ZmienioneKolejnosci { get; private set; }
        private ListBox lstLadunki;
        private Button btnUp, btnDown, btnOK, btnCancel;
        private List<EdytorKursuWithPalety.LadunekWithPalety> _ladunki;

        public KolejnoscLadunkuDialog(List<EdytorKursuWithPalety.LadunekWithPalety> ladunki)
        {
            _ladunki = ladunki.OrderBy(l => l.Kolejnosc).ToList();
            InitializeComponent();
            RefreshList();
        }

        private void InitializeComponent()
        {
            Text = "Zmień kolejność ładunków";
            Size = new Size(600, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            lstLadunki = new ListBox
            {
                Location = new Point(20, 20),
                Size = new Size(450, 400),
                Font = new Font("Segoe UI", 10F)
            };

            btnUp = new Button
            {
                Text = "▲",
                Location = new Point(480, 50),
                Size = new Size(80, 40),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };
            btnUp.Click += (s, e) => MoveItem(-1);

            btnDown = new Button
            {
                Text = "▼",
                Location = new Point(480, 100),
                Size = new Size(80, 40),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };
            btnDown.Click += (s, e) => MoveItem(1);

            btnOK = new Button
            {
                Text = "OK",
                Location = new Point(380, 430),
                Size = new Size(90, 30),
                DialogResult = DialogResult.OK
            };
            btnOK.Click += (s, e) =>
            {
                for (int i = 0; i < _ladunki.Count; i++)
                {
                    _ladunki[i].Kolejnosc = i + 1;
                }
                ZmienioneKolejnosci = _ladunki;
            };

            btnCancel = new Button
            {
                Text = "Anuluj",
                Location = new Point(480, 430),
                Size = new Size(90, 30),
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange(new Control[] { lstLadunki, btnUp, btnDown, btnOK, btnCancel });
        }

        private void RefreshList()
        {
            lstLadunki.Items.Clear();
            foreach (var ladunek in _ladunki)
            {
                string text = $"{ladunek.Kolejnosc}. {ladunek.NazwaKlienta ?? ladunek.KodKlienta} - {ladunek.Palety:N1} palet";
                lstLadunki.Items.Add(text);
            }
        }

        private void MoveItem(int direction)
        {
            int index = lstLadunki.SelectedIndex;
            if (index < 0) return;

            int newIndex = index + direction;
            if (newIndex < 0 || newIndex >= _ladunki.Count) return;

            var temp = _ladunki[index];
            _ladunki[index] = _ladunki[newIndex];
            _ladunki[newIndex] = temp;

            RefreshList();
            lstLadunki.SelectedIndex = newIndex;
        }
    }
}
#endregion