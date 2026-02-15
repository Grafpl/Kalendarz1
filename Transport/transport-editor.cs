// Plik: Transport/EdytorKursuWithPalety.cs
// Wersja z transakcyjnym zapisem i automatycznym tworzeniem zamówień
// POPRAWKA 2025-01-15: Wykluczenie zamówień z własnym transportem
// Zamówienia z TransportStatus = 'Własny' nie są pokazywane w wolnych zamówieniach
// (linia ~1534)


using Kalendarz1.Transport.Pakowanie;
using Kalendarz1.Transport.Repozytorium;
using Kalendarz1.Services;
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
        private Timer _autoUpdateTimer;

        private RadioButton rbDataUboju;
        private RadioButton rbDataOdbioru;
        private bool UzywajDataUboju => rbDataUboju?.Checked ?? true;

        // Listy do śledzenia zmian
        private List<ZamowienieDoTransportu> _zamowieniaDoDodania = new List<ZamowienieDoTransportu>();
        private List<int> _zamowieniaDoUsuniecia = new List<int>();
        private bool _dataLoaded = false;

        // Szukaj zamówień
        private TextBox txtSzukajZamowienia;
        private string _filtrZamowien = "";

        // Drag & drop
        private bool _isDragging;
        private int _dragRowIndex = -1;
        private string _dragSource;
        private Rectangle _dragBoxFromMouseDown;
        private bool _isUpdating;

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

        // Przyciski zarządzania kolejnością
        private Button btnMoveUp;
        private Button btnMoveDown;
        private Button btnSortujPoKolejnosci;

        // Wskaźnik wypełnienia
        private Panel progressWypelnienie;
        private Panel _progressFill;
        private Label _progressLabel;
        private Label lblWypelnienie;
        private Label lblStatystyki;
        private Label lblAutoUpdate;
        private Panel _panelInfoKursu;
        private string _infoUtworzylId;
        private string _infoUtworzylName;
        private string _infoUtworzylDate;
        private string _infoZmienilId;
        private string _infoZmienilName;
        private string _infoZmienilDate;
        private string _infoHandlowcy;
        private Dictionary<string, Image> _editorAvatarCache = new Dictionary<string, Image>();

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
            public byte? PlanE2NaPaleteOverride { get; set; }
            public string? NazwaKlienta { get; set; }
            public bool ZmienionyWZamowieniu { get; set; }
            public bool AnulowanyWZamowieniu { get; set; }
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
            public string TransportStatus { get; set; } = "Oczekuje";
            public string Handlowiec { get; set; } = "";
            public string Adres { get; set; } = "";
        }
        public class DodajKierowceDialog : Form
        {
            public Kierowca NowyKierowca { get; private set; }
            private TextBox txtImie, txtNazwisko, txtTelefon;
            private Label lblBladImie, lblBladNazwisko;

            public DodajKierowceDialog()
            {
                Text = "Nowy kierowca";
                Size = new Size(440, 320);
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                BackColor = Color.FromArgb(30, 33, 40);
                ForeColor = Color.White;
                Font = new Font("Segoe UI", 10F);

                // Header
                var header = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(37, 99, 235) };
                var lblTitle = new Label
                {
                    Text = "Dodaj nowego kierowcę",
                    Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                    ForeColor = Color.White,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                header.Controls.Add(lblTitle);

                // Form fields
                var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(25, 15, 25, 10) };

                int y = 15;
                body.Controls.Add(CreateFieldLabel("Imię *", y));
                txtImie = CreateFieldTextBox(y + 20); body.Controls.Add(txtImie);
                lblBladImie = CreateErrorLabel(y + 48); body.Controls.Add(lblBladImie);
                y += 60;

                body.Controls.Add(CreateFieldLabel("Nazwisko *", y));
                txtNazwisko = CreateFieldTextBox(y + 20); body.Controls.Add(txtNazwisko);
                lblBladNazwisko = CreateErrorLabel(y + 48); body.Controls.Add(lblBladNazwisko);
                y += 60;

                body.Controls.Add(CreateFieldLabel("Telefon (opcjonalnie)", y));
                txtTelefon = CreateFieldTextBox(y + 20); body.Controls.Add(txtTelefon);

                // Buttons
                var footer = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(25, 10, 25, 10) };

                var btnZapisz = new Button
                {
                    Text = "✔  ZAPISZ",
                    Size = new Size(140, 40),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(34, 154, 67),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Right
                };
                btnZapisz.FlatAppearance.BorderSize = 0;
                btnZapisz.Location = new Point(footer.Width - 290, 10);

                var btnAnuluj = new Button
                {
                    Text = "Anuluj",
                    Size = new Size(100, 40),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = Color.FromArgb(156, 163, 175),
                    Font = new Font("Segoe UI", 10F),
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Right
                };
                btnAnuluj.FlatAppearance.BorderColor = Color.FromArgb(100, 105, 115);
                btnAnuluj.Location = new Point(footer.Width - 140, 10);

                btnZapisz.Click += (s, e) =>
                {
                    lblBladImie.Text = "";
                    lblBladNazwisko.Text = "";
                    bool valid = true;

                    if (string.IsNullOrWhiteSpace(txtImie.Text))
                    { lblBladImie.Text = "Pole wymagane"; valid = false; }
                    if (string.IsNullOrWhiteSpace(txtNazwisko.Text))
                    { lblBladNazwisko.Text = "Pole wymagane"; valid = false; }

                    if (!valid) return;

                    var imie = txtImie.Text.Trim();
                    var nazwisko = txtNazwisko.Text.Trim();
                    var tel = string.IsNullOrWhiteSpace(txtTelefon.Text) ? "" : txtTelefon.Text.Trim();

                    var potwierdzenie = $"Czy na pewno chcesz dodać kierowcę?\n\n" +
                        $"   Imię:         {imie}\n" +
                        $"   Nazwisko:  {nazwisko}\n" +
                        (tel != "" ? $"   Telefon:     {tel}\n" : "") +
                        $"\nTa operacja doda nowy rekord do bazy danych.";

                    if (MessageBox.Show(potwierdzenie, "Potwierdzenie zapisu",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) != DialogResult.Yes)
                        return;

                    NowyKierowca = new Kierowca
                    {
                        Imie = imie,
                        Nazwisko = nazwisko,
                        Telefon = tel == "" ? null : tel,
                        Aktywny = true
                    };
                    DialogResult = DialogResult.OK;
                };

                btnAnuluj.Click += (s, e) => { DialogResult = DialogResult.Cancel; };

                footer.Controls.AddRange(new Control[] { btnZapisz, btnAnuluj });

                Controls.Add(body);
                Controls.Add(footer);
                Controls.Add(header);

                // Focus on first field
                Shown += (s, e) => txtImie.Focus();

                // Enter = submit, Escape = cancel
                AcceptButton = btnZapisz;
                CancelButton = btnAnuluj;
            }

            private Label CreateFieldLabel(string text, int y) => new Label
            {
                Text = text,
                Location = new Point(0, y),
                Size = new Size(370, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(156, 163, 175)
            };

            private TextBox CreateFieldTextBox(int y) => new TextBox
            {
                Location = new Point(0, y),
                Size = new Size(370, 28),
                Font = new Font("Segoe UI", 11F),
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            private Label CreateErrorLabel(int y) => new Label
            {
                Location = new Point(0, y),
                Size = new Size(370, 16),
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(248, 113, 113),
                Text = ""
            };
        }

        public class DodajPojazdDialog : Form
        {
            public Pojazd NowyPojazd { get; private set; }
            private TextBox txtRejestracja, txtMarka, txtModel;
            private NumericUpDown nudPalety;
            private Label lblBladRej;

            public DodajPojazdDialog()
            {
                Text = "Nowy pojazd";
                Size = new Size(440, 390);
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                BackColor = Color.FromArgb(30, 33, 40);
                ForeColor = Color.White;
                Font = new Font("Segoe UI", 10F);

                // Header
                var header = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(126, 87, 194) };
                var lblTitle = new Label
                {
                    Text = "Dodaj nowy pojazd",
                    Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                    ForeColor = Color.White,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                header.Controls.Add(lblTitle);

                // Form fields
                var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(25, 15, 25, 10) };

                int y = 15;
                body.Controls.Add(CreateFieldLabel("Nr rejestracyjny *", y));
                txtRejestracja = CreateFieldTextBox(y + 20); body.Controls.Add(txtRejestracja);
                txtRejestracja.CharacterCasing = CharacterCasing.Upper;
                lblBladRej = CreateErrorLabel(y + 48); body.Controls.Add(lblBladRej);
                y += 60;

                body.Controls.Add(CreateFieldLabel("Marka (opcjonalnie)", y));
                txtMarka = CreateFieldTextBox(y + 20); body.Controls.Add(txtMarka);
                y += 55;

                body.Controls.Add(CreateFieldLabel("Model (opcjonalnie)", y));
                txtModel = CreateFieldTextBox(y + 20); body.Controls.Add(txtModel);
                y += 55;

                body.Controls.Add(CreateFieldLabel("Pojemność (palety H1)", y));
                nudPalety = new NumericUpDown
                {
                    Location = new Point(0, y + 20),
                    Size = new Size(120, 28),
                    Minimum = 1,
                    Maximum = 50,
                    Value = 33,
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    BackColor = Color.FromArgb(52, 56, 64),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };
                body.Controls.Add(nudPalety);

                var lblPaletyInfo = new Label
                {
                    Text = "(domyślnie 33 dla naczepy)",
                    Location = new Point(130, y + 24),
                    Size = new Size(200, 20),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                    ForeColor = Color.FromArgb(120, 130, 140)
                };
                body.Controls.Add(lblPaletyInfo);

                // Buttons
                var footer = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(25, 10, 25, 10) };

                var btnZapisz = new Button
                {
                    Text = "✔  ZAPISZ",
                    Size = new Size(140, 40),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(34, 154, 67),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Right
                };
                btnZapisz.FlatAppearance.BorderSize = 0;
                btnZapisz.Location = new Point(footer.Width - 290, 10);

                var btnAnuluj = new Button
                {
                    Text = "Anuluj",
                    Size = new Size(100, 40),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = Color.FromArgb(156, 163, 175),
                    Font = new Font("Segoe UI", 10F),
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Right
                };
                btnAnuluj.FlatAppearance.BorderColor = Color.FromArgb(100, 105, 115);
                btnAnuluj.Location = new Point(footer.Width - 140, 10);

                btnZapisz.Click += (s, e) =>
                {
                    lblBladRej.Text = "";
                    if (string.IsNullOrWhiteSpace(txtRejestracja.Text))
                    {
                        lblBladRej.Text = "Pole wymagane";
                        return;
                    }

                    var rej = txtRejestracja.Text.Trim().ToUpper();
                    var marka = string.IsNullOrWhiteSpace(txtMarka.Text) ? "" : txtMarka.Text.Trim();
                    var model = string.IsNullOrWhiteSpace(txtModel.Text) ? "" : txtModel.Text.Trim();
                    var palety = (int)nudPalety.Value;

                    var opis = marka != "" ? $"{marka} {model} ({rej})" : rej;
                    var potwierdzenie = $"Czy na pewno chcesz dodać pojazd?\n\n" +
                        $"   Rejestracja:   {rej}\n" +
                        (marka != "" ? $"   Marka:           {marka}\n" : "") +
                        (model != "" ? $"   Model:           {model}\n" : "") +
                        $"   Palety H1:      {palety}\n" +
                        $"\nTa operacja doda nowy rekord do bazy danych.";

                    if (MessageBox.Show(potwierdzenie, "Potwierdzenie zapisu",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) != DialogResult.Yes)
                        return;

                    NowyPojazd = new Pojazd
                    {
                        Rejestracja = rej,
                        Marka = marka == "" ? null : marka,
                        Model = model == "" ? null : model,
                        PaletyH1 = palety,
                        Aktywny = true
                    };
                    DialogResult = DialogResult.OK;
                };

                btnAnuluj.Click += (s, e) => { DialogResult = DialogResult.Cancel; };

                footer.Controls.AddRange(new Control[] { btnZapisz, btnAnuluj });

                Controls.Add(body);
                Controls.Add(footer);
                Controls.Add(header);

                // Focus + shortcuts
                Shown += (s, e) => txtRejestracja.Focus();
                AcceptButton = btnZapisz;
                CancelButton = btnAnuluj;
            }

            private Label CreateFieldLabel(string text, int y) => new Label
            {
                Text = text,
                Location = new Point(0, y),
                Size = new Size(370, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(156, 163, 175)
            };

            private TextBox CreateFieldTextBox(int y) => new TextBox
            {
                Location = new Point(0, y),
                Size = new Size(370, 28),
                Font = new Font("Segoe UI", 11F),
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            private Label CreateErrorLabel(int y) => new Label
            {
                Location = new Point(0, y),
                Size = new Size(370, 16),
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(248, 113, 113),
                Text = ""
            };
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
            WindowIconHelper.SetIcon(this);
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
            _autoUpdateTimer.Tick += async (s, e) =>
            {
                await CheckForZamowieniaUpdates();
                await RefreshWolneZamowieniaSilently();
            };
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
                RowCount = 3,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(240, 242, 247)
            };

            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));  // Lewa: header + ładunki
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));  // Prawa: wolne zamówienia
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));        // Header kursu (duży)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));        // Ładunki (kompaktowe)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));       // Przyciski

            // Najpierw utwórz headerPanel (inicjalizuje dtpData potrzebne przez zamowieniaPanel)
            var headerPanel = CreateHeaderPanel();

            // Wiersz 0, Kol 0: Header kursu (tylko lewa strona)
            mainLayout.Controls.Add(headerPanel, 0, 0);

            // Wiersz 0-1, Kol 1: Wolne zamówienia (pełna wysokość prawa strona)
            var zamowieniaPanel = CreateZamowieniaPanel();
            mainLayout.Controls.Add(zamowieniaPanel, 1, 0);
            mainLayout.SetRowSpan(zamowieniaPanel, 2);

            // Wiersz 1, Kol 0: Ładunki
            mainLayout.Controls.Add(CreateLadunkiPanel(), 0, 1);

            // Wiersz 2: Przyciski (cała szerokość)
            var buttonsPanel = CreateButtonsPanel();
            mainLayout.Controls.Add(buttonsPanel, 0, 2);
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

            // === Wiersz 1 (Y=18): Kierowca ===
            var lblKierowca = CreateLabel("KIEROWCA:", 15, 18, 90);
            lblKierowca.ForeColor = Color.FromArgb(156, 163, 175);
            lblKierowca.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            cboKierowca = new ComboBox
            {
                Location = new Point(110, 15),
                Size = new Size(240, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                DropDownHeight = 600,
                Font = new Font("Segoe UI", 10F),
                DisplayMember = "PelneNazwisko",
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            btnNowyKierowca = new Button
            {
                Text = "+",
                Location = new Point(355, 15),
                Size = new Size(28, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnNowyKierowca.FlatAppearance.BorderSize = 0;
            btnNowyKierowca.Click += BtnNowyKierowca_Click;

            // === Wiersz 1 (kontynuacja): Pojazd ===
            var lblPojazd = CreateLabel("POJAZD:", 400, 18, 65);
            lblPojazd.ForeColor = Color.FromArgb(156, 163, 175);
            lblPojazd.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            cboPojazd = new ComboBox
            {
                Location = new Point(468, 15),
                Size = new Size(160, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                DropDownHeight = 600,
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
                Location = new Point(633, 15),
                Size = new Size(28, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnNowyPojazd.FlatAppearance.BorderSize = 0;
            btnNowyPojazd.Click += BtnNowyPojazd_Click;

            // === Wiersz 2 (Y=58): Data + Godziny ===
            var lblData = CreateLabel("DATA:", 15, 58, 45);
            lblData.ForeColor = Color.FromArgb(156, 163, 175);
            lblData.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            dtpData = new DateTimePicker
            {
                Location = new Point(65, 55),
                Size = new Size(140, 28),
                Format = DateTimePickerFormat.Short,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                CalendarMonthBackground = Color.FromArgb(52, 56, 64)
            };
            dtpData.ValueChanged += async (s, e) => await LoadWolneZamowienia();

            var lblGodziny = CreateLabel("GODZINY:", 225, 58, 75);
            lblGodziny.ForeColor = Color.FromArgb(156, 163, 175);
            lblGodziny.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            txtGodzWyjazdu = new MaskedTextBox
            {
                Location = new Point(305, 55),
                Size = new Size(65, 28),
                Mask = "00:00",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Text = "06:00",
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.FromArgb(255, 193, 7),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblDo = CreateLabel("→", 375, 58, 25);
            lblDo.TextAlign = ContentAlignment.MiddleCenter;
            lblDo.ForeColor = Color.FromArgb(255, 193, 7);
            lblDo.Font = new Font("Segoe UI", 12F);

            txtGodzPowrotu = new MaskedTextBox
            {
                Location = new Point(405, 55),
                Size = new Size(65, 28),
                Mask = "00:00",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Text = "18:00",
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.FromArgb(255, 193, 7),
                BorderStyle = BorderStyle.FixedSingle
            };

            lblAutoUpdate = new Label
            {
                Location = new Point(485, 58),
                Size = new Size(200, 22),
                Text = "",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Orange,
                Visible = false
            };

            // === Wiersz 3 (Y=98): Trasa ===
            var lblTrasa = CreateLabel("TRASA:", 15, 98, 55);
            lblTrasa.ForeColor = Color.FromArgb(156, 163, 175);
            lblTrasa.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            txtTrasa = new TextBox
            {
                Location = new Point(75, 96),
                Size = new Size(590, 26),
                Font = new Font("Segoe UI", 10F),
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Trasa zostanie uzupełniona automatycznie...",
                ReadOnly = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // === Wiersz 4 (Y=138): Wypełnienie ===
            var panelWypelnienie = new Panel
            {
                Location = new Point(15, 138),
                Size = new Size(650, 50),
                BackColor = Color.FromArgb(33, 37, 43),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblWypelnienie = new Label
            {
                Location = new Point(10, 15),
                Size = new Size(110, 20),
                Text = "WYPEŁNIENIE:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(156, 163, 175)
            };

            progressWypelnienie = new Panel
            {
                Location = new Point(125, 12),
                Size = new Size(300, 26),
                BackColor = Color.FromArgb(55, 60, 70)
            };
            _progressFill = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(0, 26),
                BackColor = Color.FromArgb(34, 197, 94)
            };
            _progressLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Text = "0%"
            };
            progressWypelnienie.Controls.Add(_progressLabel);
            progressWypelnienie.Controls.Add(_progressFill);
            _progressLabel.BringToFront();

            lblStatystyki = new Label
            {
                Location = new Point(430, 15),
                Size = new Size(215, 20),
                Text = "0 palet z 0 dostępnych",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 193, 7),
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            panelWypelnienie.Controls.AddRange(new Control[] { lblWypelnienie, progressWypelnienie, lblStatystyki });

            // === Wiersz 5 (Y=198): Info bar ===
            _panelInfoKursu = new Panel
            {
                Location = new Point(15, 198),
                Size = new Size(650, 22),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _panelInfoKursu.Paint += PanelInfoKursu_Paint;

            panel.Controls.AddRange(new Control[] {
                lblKierowca, cboKierowca, btnNowyKierowca,
                lblPojazd, cboPojazd, btnNowyPojazd,
                lblData, dtpData,
                lblGodziny, txtGodzWyjazdu, lblDo, txtGodzPowrotu, lblAutoUpdate,
                lblTrasa, txtTrasa,
                panelWypelnienie,
                _panelInfoKursu
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
            var menuEdytujZamowienie = new ToolStripMenuItem("Edytuj zamówienie", null, async (s, e) => await EdytujPrzypisaneZamowienie());
            var menuUsun = new ToolStripMenuItem("Usuń z kursu", null, async (s, e) => await UsunLadunek());
            var menuGora = new ToolStripMenuItem("Przesuń w górę", null, async (s, e) => await PrzesunLadunek(-1));
            var menuDol = new ToolStripMenuItem("Przesuń w dół", null, async (s, e) => await PrzesunLadunek(1));
            var menuOdswiez = new ToolStripMenuItem("Odśwież z zamówienia", null, async (s, e) => await OdswiezZamowienie());

            contextMenu.Items.Add(menuEdytujZamowienie);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(menuOdswiez);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(menuUsun);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(menuGora);
            contextMenu.Items.Add(menuDol);

            dgvLadunki.ContextMenuStrip = contextMenu;

            // Drag & drop
            dgvLadunki.AllowDrop = true;
            dgvLadunki.MouseDown += DgvLadunki_MouseDown;
            dgvLadunki.MouseMove += DgvLadunki_MouseMove;
            dgvLadunki.DragEnter += DgvLadunki_DragEnter;
            dgvLadunki.DragOver += DgvTarget_DragOver;
            dgvLadunki.DragDrop += DgvLadunki_DragDrop;
            dgvLadunki.DragLeave += DgvTarget_DragLeave;

            panel.Controls.Add(dgvLadunki);
            panel.Controls.Add(panelKolejnosc);

            return panel;
        }


        private Panel CreateZamowieniaPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Toolbar na górze (kompaktowy, w jednej linii)
            var panelToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(155, 89, 182),
                Padding = new Padding(8, 4, 8, 4)
            };

            lblZamowieniaInfo = new Label
            {
                Text = "WOLNE ZAMÓWIENIA",
                Location = new Point(8, 8),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White
            };

            rbDataUboju = new RadioButton
            {
                Text = "Ubój",
                Location = new Point(190, 9),
                Size = new Size(55, 18),
                Checked = true,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.White
            };
            rbDataUboju.CheckedChanged += async (s, e) =>
            {
                if (rbDataUboju.Checked && _dataLoaded)
                    await LoadWolneZamowieniaForDate(_dtpZamowienia?.Value ?? dtpData.Value);
            };

            rbDataOdbioru = new RadioButton
            {
                Text = "Odbiór",
                Location = new Point(248, 9),
                Size = new Size(65, 18),
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.FromArgb(220, 200, 240)
            };
            rbDataOdbioru.CheckedChanged += async (s, e) =>
            {
                if (rbDataOdbioru.Checked && _dataLoaded)
                    await LoadWolneZamowieniaForDate(_dtpZamowienia?.Value ?? dtpData.Value);
            };

            txtSzukajZamowienia = new TextBox
            {
                Location = new Point(320, 6),
                Size = new Size(200, 24),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.Gray,
                Text = "Szukaj..."
            };
            txtSzukajZamowienia.GotFocus += (s, e) =>
            {
                if (txtSzukajZamowienia.ForeColor == Color.Gray)
                {
                    txtSzukajZamowienia.Text = "";
                    txtSzukajZamowienia.ForeColor = Color.Black;
                }
            };
            txtSzukajZamowienia.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtSzukajZamowienia.Text))
                {
                    txtSzukajZamowienia.ForeColor = Color.Gray;
                    txtSzukajZamowienia.Text = "Szukaj...";
                    _filtrZamowien = "";
                }
            };
            txtSzukajZamowienia.TextChanged += (s, e) =>
            {
                if (txtSzukajZamowienia.ForeColor != Color.Gray)
                {
                    _filtrZamowien = txtSzukajZamowienia.Text.Trim();
                    ShowZamowieniaInGrid();
                }
            };

            var dtpZamowienia = new DateTimePicker
            {
                Location = new Point(530, 6),
                Size = new Size(120, 24),
                Format = DateTimePickerFormat.Short,
                Font = new Font("Segoe UI", 9F),
                Value = dtpData.Value
            };
            dtpZamowienia.ValueChanged += async (s, e) =>
            {
                await LoadWolneZamowieniaForDate(dtpZamowienia.Value);
            };

            var btnRefreshZamowienia = new Button
            {
                Text = "⟳",
                Location = new Point(655, 5),
                Size = new Size(30, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(120, 70, 150),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
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
                Location = new Point(690, 5),
                Size = new Size(45, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(120, 70, 150),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8F),
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
                Location = new Point(745, 8),
                Size = new Size(100, 20),
                Text = "0 zam.",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 220, 255),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelToolbar.Controls.AddRange(new Control[] {
                lblZamowieniaInfo,
                rbDataUboju, rbDataOdbioru,
                txtSzukajZamowienia,
                dtpZamowienia,
                btnRefreshZamowienia,
                btnToday,
                lblLiczbaZamowien
            });

            _dtpZamowienia = dtpZamowienia;
            _lblLiczbaZamowien = lblLiczbaZamowien;

            // Grid wolnych zamówień - styl jak w widoku głównym
            dgvWolneZamowienia = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            dgvWolneZamowienia.EnableHeadersVisualStyles = false;
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250);
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(100, 100, 120);
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 6, 4, 6);
            dgvWolneZamowienia.ColumnHeadersHeight = 28;
            dgvWolneZamowienia.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            dgvWolneZamowienia.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F);
            dgvWolneZamowienia.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
            dgvWolneZamowienia.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgvWolneZamowienia.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvWolneZamowienia.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 252);
            dgvWolneZamowienia.RowTemplate.Height = 24;
            dgvWolneZamowienia.GridColor = Color.FromArgb(240, 242, 245);
            dgvWolneZamowienia.CellPainting += DgvEditorWolneZamowienia_CellPainting;

            dgvWolneZamowienia.CellDoubleClick += async (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    var row = dgvWolneZamowienia.Rows[e.RowIndex];
                    if (row.Cells["IsGroupRow"]?.Value != null && Convert.ToBoolean(row.Cells["IsGroupRow"].Value))
                        return;
                    await DodajZamowienieDoKursu();
                }
            };

            // Drag & drop
            dgvWolneZamowienia.AllowDrop = true;
            dgvWolneZamowienia.MouseDown += DgvWolneZamowienia_MouseDown;
            dgvWolneZamowienia.MouseMove += DgvWolneZamowienia_MouseMove;
            dgvWolneZamowienia.DragEnter += DgvWolneZamowienia_DragEnter;
            dgvWolneZamowienia.DragOver += DgvTarget_DragOver;
            dgvWolneZamowienia.DragDrop += DgvWolneZamowienia_DragDrop;
            dgvWolneZamowienia.DragLeave += DgvTarget_DragLeave;

            var contextMenuWolne = new ContextMenuStrip();
            var menuDodajDoKursu = new ToolStripMenuItem("Dodaj do kursu", null, async (s, e) => await DodajZamowienieDoKursu());
            var menuEdytujZamowienie = new ToolStripMenuItem("Edytuj zamówienie", null, async (s, e) => await EdytujWolneZamowienie());
            var menuSzczegolyZamowienia = new ToolStripMenuItem("Szczegóły", null, async (s, e) => await PokazSzczegolyZamowienia());

            contextMenuWolne.Items.Add(menuDodajDoKursu);
            contextMenuWolne.Items.Add(menuEdytujZamowienie);
            contextMenuWolne.Items.Add(menuSzczegolyZamowienia);

            dgvWolneZamowienia.ContextMenuStrip = contextMenuWolne;

            // Przycisk dodawania - na dole
            btnDodajZamowienie = new Button
            {
                Text = "⬇  Dodaj zaznaczone do kursu",
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = Color.FromArgb(34, 154, 67),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDodajZamowienie.FlatAppearance.BorderSize = 0;
            btnDodajZamowienie.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 130, 56);
            btnDodajZamowienie.Click += async (s, e) => await DodajZamowienieDoKursu();

            panel.Controls.Add(dgvWolneZamowienia);
            panel.Controls.Add(btnDodajZamowienie);
            panel.Controls.Add(panelToolbar);

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
                Size = new Size(170, 48),
                Text = "✔  ZAPISZ KURS",
                BackColor = Color.FromArgb(34, 154, 67),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 130, 56);
            btnZapisz.Location = new Point(panel.Width - btnZapisz.Width - 170, 13);
            btnZapisz.Click += BtnZapisz_Click;

            btnAnuluj = new Button
            {
                Size = new Size(120, 42),
                Text = "ANULUJ",
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(180, 185, 195),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnAnuluj.FlatAppearance.BorderColor = Color.FromArgb(100, 105, 115);
            btnAnuluj.FlatAppearance.BorderSize = 1;
            btnAnuluj.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 60, 70);
            btnAnuluj.Location = new Point(panel.Width - btnAnuluj.Width - 20, 16);
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
                btnAnuluj.Location = new Point(panel.Width - btnAnuluj.Width - 20, 16);
                btnZapisz.Location = new Point(panel.Width - btnZapisz.Width - 160, 13);
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

                // Używamy BindingSource z możliwością pustego wyboru
                var kierowcySource = new BindingSource();
                kierowcySource.DataSource = _kierowcy;
                cboKierowca.DataSource = kierowcySource;

                var pojazdySource = new BindingSource();
                pojazdySource.DataSource = _pojazdy;
                cboPojazd.DataSource = pojazdySource;

                // Domyślnie brak wyboru - pozwala na zapis kursu bez przypisania
                cboKierowca.SelectedIndex = -1;
                cboPojazd.SelectedIndex = -1;

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

            // Obsługa nullable kierowcy i pojazdu
            if (_kurs.KierowcaID.HasValue)
            {
                cboKierowca.SelectedItem = _kierowcy.FirstOrDefault(k => k.KierowcaID == _kurs.KierowcaID.Value);
            }
            else
            {
                cboKierowca.SelectedIndex = -1;
            }

            if (_kurs.PojazdID.HasValue)
            {
                cboPojazd.SelectedItem = _pojazdy.FirstOrDefault(p => p.PojazdID == _kurs.PojazdID.Value);
            }
            else
            {
                cboPojazd.SelectedIndex = -1;
            }

            if (_kurs.GodzWyjazdu.HasValue)
                txtGodzWyjazdu.Text = _kurs.GodzWyjazdu.Value.ToString(@"hh\:mm");
            if (_kurs.GodzPowrotu.HasValue)
                txtGodzPowrotu.Text = _kurs.GodzPowrotu.Value.ToString(@"hh\:mm");

            txtTrasa.Text = _kurs.Trasa ?? "";

            // Pokaż info o utworzeniu/modyfikacji kursu
            UpdateInfoKursu();

            await LoadLadunki();

            // Uzupełnij info o handlowcach (async, po załadowaniu ładunków)
            _ = UpdateHandlowcyInfoAsync();
        }

        private void UpdateInfoKursu(string handlowcyInfo = null)
        {
            if (_panelInfoKursu == null) return;

            if (_kurs != null)
            {
                _infoUtworzylId = _kurs.Utworzyl ?? "";
                _infoUtworzylDate = _kurs.UtworzonoUTC.ToLocalTime().ToString("dd.MM HH:mm");
                if (_kurs.ZmienionoUTC.HasValue && !string.IsNullOrEmpty(_kurs.Zmienil))
                {
                    _infoZmienilId = _kurs.Zmienil;
                    _infoZmienilDate = _kurs.ZmienionoUTC.Value.ToLocalTime().ToString("dd.MM HH:mm");
                }
            }
            if (!string.IsNullOrEmpty(handlowcyInfo))
                _infoHandlowcy = handlowcyInfo;

            // Pobierz pełne nazwy użytkowników asynchronicznie
            _ = ResolveInfoNamesAndRepaint();
        }

        private async Task ResolveInfoNamesAndRepaint()
        {
            try
            {
                var ids = new List<string>();
                if (!string.IsNullOrEmpty(_infoUtworzylId)) ids.Add(_infoUtworzylId);
                if (!string.IsNullOrEmpty(_infoZmienilId)) ids.Add(_infoZmienilId);
                if (ids.Count > 0)
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    var paramNames = ids.Select((_, i) => $"@u{i}").ToList();
                    var sql = $"SELECT ID, ISNULL(Name, ID) FROM operators WHERE ID IN ({string.Join(",", paramNames)})";
                    await using var cmd = new SqlCommand(sql, cn);
                    for (int i = 0; i < ids.Count; i++)
                        cmd.Parameters.AddWithValue($"@u{i}", ids[i]);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        var id = rdr.GetString(0);
                        var name = rdr.GetString(1);
                        if (id == _infoUtworzylId) _infoUtworzylName = name;
                        if (id == _infoZmienilId) _infoZmienilName = name;
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(_infoUtworzylName)) _infoUtworzylName = _infoUtworzylId;
            if (!string.IsNullOrEmpty(_infoZmienilId) && string.IsNullOrEmpty(_infoZmienilName))
                _infoZmienilName = _infoZmienilId;

            _panelInfoKursu?.Invalidate();
        }

        private Image GetEditorAvatar(string userId, string displayName, int size)
        {
            var key = $"{userId}_{size}";
            if (_editorAvatarCache.TryGetValue(key, out var cached))
                return cached;

            Image avatar = null;
            try
            {
                if (UserAvatarManager.HasAvatar(userId))
                    avatar = UserAvatarManager.GetAvatarRounded(userId, size);
            }
            catch { }

            if (avatar == null)
                avatar = UserAvatarManager.GenerateDefaultAvatar(displayName ?? userId, userId, size);

            _editorAvatarCache[key] = avatar;
            return avatar;
        }

        private void PanelInfoKursu_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            var avatarSize = 16;
            var x = 5;
            var labelColor = Color.FromArgb(100, 110, 130);
            var nameColor = Color.FromArgb(200, 210, 225);
            var dateColor = Color.FromArgb(130, 140, 155);

            // Utworzył
            if (!string.IsNullOrEmpty(_infoUtworzylId))
            {
                using (var font = new Font("Segoe UI", 7.5F))
                using (var brush = new SolidBrush(labelColor))
                {
                    g.DrawString("Utworzył:", font, brush, x, 1);
                    x += (int)g.MeasureString("Utworzył:", font).Width + 3;
                }

                var avatar = GetEditorAvatar(_infoUtworzylId, _infoUtworzylName ?? _infoUtworzylId, avatarSize);
                if (avatar != null)
                {
                    g.DrawImage(avatar, x, 1, avatarSize, avatarSize);
                    x += avatarSize + 3;
                }

                var name = _infoUtworzylName ?? _infoUtworzylId;
                using (var font = new Font("Segoe UI", 7.5F, FontStyle.Bold))
                using (var brush = new SolidBrush(nameColor))
                {
                    g.DrawString(name, font, brush, x, 1);
                    x += (int)g.MeasureString(name, font).Width + 2;
                }

                using (var font = new Font("Segoe UI", 7F))
                using (var brush = new SolidBrush(dateColor))
                {
                    g.DrawString($"({_infoUtworzylDate})", font, brush, x, 2);
                    x += (int)g.MeasureString($"({_infoUtworzylDate})", font).Width;
                }
            }

            // Zmienił
            if (!string.IsNullOrEmpty(_infoZmienilId))
            {
                x += 15;
                using (var font = new Font("Segoe UI", 7.5F))
                using (var brush = new SolidBrush(labelColor))
                {
                    g.DrawString("Zmienił:", font, brush, x, 1);
                    x += (int)g.MeasureString("Zmienił:", font).Width + 3;
                }

                var avatar = GetEditorAvatar(_infoZmienilId, _infoZmienilName ?? _infoZmienilId, avatarSize);
                if (avatar != null)
                {
                    g.DrawImage(avatar, x, 1, avatarSize, avatarSize);
                    x += avatarSize + 3;
                }

                var name = _infoZmienilName ?? _infoZmienilId;
                using (var font = new Font("Segoe UI", 7.5F, FontStyle.Bold))
                using (var brush = new SolidBrush(nameColor))
                {
                    g.DrawString(name, font, brush, x, 1);
                    x += (int)g.MeasureString(name, font).Width + 2;
                }

                using (var font = new Font("Segoe UI", 7F))
                using (var brush = new SolidBrush(dateColor))
                {
                    g.DrawString($"({_infoZmienilDate})", font, brush, x, 2);
                    x += (int)g.MeasureString($"({_infoZmienilDate})", font).Width;
                }
            }

            // Handlowcy
            if (!string.IsNullOrEmpty(_infoHandlowcy))
            {
                x += 15;
                using (var font = new Font("Segoe UI", 7.5F))
                using (var brush = new SolidBrush(labelColor))
                {
                    g.DrawString("Handlowcy:", font, brush, x, 1);
                    x += (int)g.MeasureString("Handlowcy:", font).Width + 3;
                }

                var names = _infoHandlowcy.Split(',').Select(n => n.Trim()).Where(n => !string.IsNullOrEmpty(n)).ToList();
                foreach (var hName in names.Take(3))
                {
                    var avatar = GetEditorAvatar(hName, hName, avatarSize);
                    if (avatar != null)
                    {
                        g.DrawImage(avatar, x, 1, avatarSize, avatarSize);
                        x += avatarSize + 2;
                    }
                }

                using (var font = new Font("Segoe UI", 7.5F, FontStyle.Bold))
                using (var brush = new SolidBrush(nameColor))
                    g.DrawString(_infoHandlowcy, font, brush, x, 1);
            }
        }

        private async Task UpdateHandlowcyInfoAsync()
        {
            try
            {
                var klientIds = new HashSet<int>();
                foreach (var lad in _ladunki)
                {
                    if (lad.KodKlienta?.StartsWith("ZAM_") == true &&
                        int.TryParse(lad.KodKlienta.Substring(4), out int zamId))
                    {
                        // Potrzebujemy KlientId - pobierz z LibraNet
                    }
                }

                // Pobierz KlientIds z zamówień w LibraNet
                var zamIds = _ladunki
                    .Where(l => l.KodKlienta?.StartsWith("ZAM_") == true)
                    .Select(l => int.TryParse(l.KodKlienta!.Substring(4), out int id) ? id : 0)
                    .Where(id => id > 0).Distinct().ToList();

                if (zamIds.Count == 0) return;

                await using var cnLibra = new SqlConnection(_connLibra);
                await cnLibra.OpenAsync();
                var sqlZam = $"SELECT Id, KlientId FROM dbo.ZamowieniaMieso WHERE Id IN ({string.Join(",", zamIds)})";
                await using var cmdZam = new SqlCommand(sqlZam, cnLibra);
                await using var rdrZam = await cmdZam.ExecuteReaderAsync();
                while (await rdrZam.ReadAsync())
                    klientIds.Add(rdrZam.GetInt32(1));

                if (klientIds.Count == 0) return;

                // Pobierz Handlowców z Handel DB
                await using var cnHandel = new SqlConnection(_connHandel);
                await cnHandel.OpenAsync();
                var sqlH = $@"SELECT DISTINCT ISNULL(wym.CDim_Handlowiec_Val, '')
                             FROM SSCommon.STContractors c
                             LEFT JOIN SSCommon.ContractorClassification wym ON c.Id = wym.ElementId
                             WHERE c.Id IN ({string.Join(",", klientIds)})
                               AND ISNULL(wym.CDim_Handlowiec_Val, '') <> ''";
                await using var cmdH = new SqlCommand(sqlH, cnHandel);
                await using var rdrH = await cmdH.ExecuteReaderAsync();
                var handlowcy = new List<string>();
                while (await rdrH.ReadAsync())
                    handlowcy.Add(rdrH.GetString(0));

                if (handlowcy.Count > 0)
                    UpdateInfoKursu(string.Join(", ", handlowcy));
            }
            catch { }
        }

        private async Task LoadLadunki()
        {
            if (!_kursId.HasValue && _ladunki.Count == 0) return;

            // Reload z DB tylko przy pierwszym ładowaniu (przed _dataLoaded)
            if (_kursId.HasValue && _ladunki.Count == 0 && !_dataLoaded)
            {
                _ladunki = await LoadLadunkiFromDatabase(_kursId.Value);
            }

            // Sprawdź live dane i automatycznie usuń anulowane/własne/nieistniejące
            var doUsuniecia = new List<LadunekWithPalety>();
            foreach (var ladunek in _ladunki.Where(l => l.KodKlienta?.StartsWith("ZAM_") == true).ToList())
            {
                if (int.TryParse(ladunek.KodKlienta.Substring(4), out int zamId))
                {
                    var aktualneZamowienie = await PobierzAktualneZamowienie(zamId);

                    if (aktualneZamowienie == null ||
                        aktualneZamowienie.Status == "Anulowane" ||
                        aktualneZamowienie.TransportStatus == "Wlasny")
                    {
                        doUsuniecia.Add(ladunek);
                        if (!_zamowieniaDoUsuniecia.Contains(zamId))
                            _zamowieniaDoUsuniecia.Add(zamId);
                    }
                    else
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

            if (doUsuniecia.Count > 0)
            {
                foreach (var lad in doUsuniecia)
                    _ladunki.Remove(lad);
                for (int i = 0; i < _ladunki.Count; i++)
                    _ladunki[i].Kolejnosc = i + 1;
                System.Diagnostics.Debug.WriteLine($"[Transport] LoadLadunki: auto-usunięto {doUsuniecia.Count} nieaktualnych zamówień");
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
                if (ladunek.AnulowanyWZamowieniu)
                {
                    status = "⚠ Anulowane/Własny";
                }
                else if (ladunek.ZmienionyWZamowieniu)
                {
                    status = "⚠ Zmienione (niezapisane)";
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
                var ladunekId = Convert.ToInt64(row.Cells["ID"].Value);
                var lad = _ladunki.FirstOrDefault(l => l.LadunekID == ladunekId);

                if (lad != null && lad.AnulowanyWZamowieniu)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 220, 220);
                    row.DefaultCellStyle.ForeColor = Color.DarkRed;
                }
                else if (lad != null && lad.ZmienionyWZamowieniu)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 243, 205);
                }
                else
                {
                    var statusCell = row.Cells["Status"];
                    if (statusCell?.Value?.ToString()?.Contains("niezapisane") == true)
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 250, 230);
                    }
                }
            }

            UpdateTrasa();
            await UpdateWypelnienie();
        }

        /// <summary>
        /// Synchronicznie odświeża grid ładunków z aktualnej listy _ladunki.
        /// Nie wywołuje żadnych operacji async/DB - używa danych już w pamięci.
        /// Używane przez drag & drop i operacje usuwania/dodawania.
        /// </summary>
        private void RefreshLadunkiGrid()
        {
            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(long));
            dt.Columns.Add("Lp.", typeof(int));
            dt.Columns.Add("Klient", typeof(string));
            dt.Columns.Add("Data uboju", typeof(string));
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
                        klientNazwa = idx > 0 ? ladunek.Uwagi.Substring(0, idx).Trim() : ladunek.Uwagi;
                        ladunek.NazwaKlienta = klientNazwa;
                    }
                    else
                    {
                        klientNazwa = ladunek.KodKlienta ?? "";
                        ladunek.NazwaKlienta = klientNazwa;
                    }
                }

                string dataUbojuStr = ladunek.DataUboju.HasValue
                    ? ladunek.DataUboju.Value.ToString("yyyy-MM-dd") : "---";

                string status = "";
                if (ladunek.AnulowanyWZamowieniu)
                    status = "⚠ Anulowane/Własny";
                else if (ladunek.ZmienionyWZamowieniu)
                    status = "⚠ Zmienione (niezapisane)";

                dt.Rows.Add(
                    ladunek.LadunekID,
                    ladunek.Kolejnosc,
                    klientNazwa,
                    dataUbojuStr,
                    ladunek.Palety,
                    ladunek.PojemnikiE2,
                    ladunek.Adres ?? "",
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
                dgvLadunki.Columns["Uwagi"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            foreach (DataGridViewRow row in dgvLadunki.Rows)
            {
                var ladunekId = Convert.ToInt64(row.Cells["ID"].Value);
                var lad = _ladunki.FirstOrDefault(l => l.LadunekID == ladunekId);

                if (lad != null && lad.AnulowanyWZamowieniu)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 220, 220);
                    row.DefaultCellStyle.ForeColor = Color.DarkRed;
                }
                else if (lad != null && lad.ZmienionyWZamowieniu)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 243, 205);
                }
            }

            UpdateTrasa();

            // Inline UpdateWypelnienie (sync)
            try
            {
                if (cboPojazd.SelectedItem is Pojazd pojazd)
                {
                    decimal sumaPalet = _ladunki?.Sum(l => l.Palety) ?? 0m;
                    int paletyPojazdu = pojazd.PaletyH1;
                    decimal procent = paletyPojazdu > 0 ? (sumaPalet / paletyPojazdu * 100m) : 0m;

                    int fillWidth = (int)(Math.Min(100, procent) / 100m * progressWypelnienie.Width);
                    _progressFill.Width = Math.Max(0, fillWidth);
                    _progressLabel.Text = $"{procent:F1}%";
                    lblStatystyki.Text = $"{sumaPalet:N1} palet z {paletyPojazdu} dostępnych ({procent:N1}%)";

                    Color barColor;
                    if (procent > 100)
                    {
                        barColor = Color.FromArgb(220, 38, 38);
                        lblWypelnienie.ForeColor = Color.FromArgb(248, 113, 113);
                        lblWypelnienie.Text = $"⚠ PRZEPEŁNIENIE: {procent:F1}%";
                    }
                    else if (procent > 90)
                    {
                        barColor = Color.FromArgb(245, 158, 11);
                        lblWypelnienie.ForeColor = Color.FromArgb(251, 191, 36);
                        lblWypelnienie.Text = $"WYPEŁNIENIE: {procent:F1}%";
                    }
                    else if (procent > 70)
                    {
                        barColor = Color.FromArgb(234, 179, 8);
                        lblWypelnienie.ForeColor = Color.FromArgb(156, 163, 175);
                        lblWypelnienie.Text = $"WYPEŁNIENIE: {procent:F1}%";
                    }
                    else
                    {
                        barColor = Color.FromArgb(34, 197, 94);
                        lblWypelnienie.ForeColor = Color.FromArgb(156, 163, 175);
                        lblWypelnienie.Text = $"WYPEŁNIENIE: {procent:F1}%";
                    }
                    _progressFill.BackColor = barColor;
                }
                else
                {
                    _progressFill.Width = 0;
                    _progressLabel.Text = "0%";
                    lblStatystyki.Text = "0 palet z 0 dostępnych";
                }
            }
            catch { }
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
                              Uwagi, ISNULL(TrybE2, 0) AS TrybE2,
                              PlanE2NaPaleteOverride
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
                    TrybE2 = reader.GetBoolean(7),
                    PlanE2NaPaleteOverride = reader.IsDBNull(8) ? null : reader.GetByte(8)
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
                        && transportStatus != "Wlasny"  // NOWE: wykluczenie własnego transportu (DB przechowuje bez ł)
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
                    _lblLiczbaZamowien.Text = $"{_wolneZamowienia.Count} zam.";

                    if (_wolneZamowienia.Count == 0)
                        _lblLiczbaZamowien.ForeColor = Color.FromArgb(180, 180, 200);
                    else if (_wolneZamowienia.Count > 15)
                        _lblLiczbaZamowien.ForeColor = Color.FromArgb(255, 180, 120);
                    else
                        _lblLiczbaZamowien.ForeColor = Color.FromArgb(220, 220, 255);
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
            var plCulture = new System.Globalization.CultureInfo("pl-PL");
            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(int));
            dt.Columns.Add("IsGroupRow", typeof(bool));
            dt.Columns.Add("Ubój", typeof(string));
            dt.Columns.Add("Odbiór", typeof(string));
            dt.Columns.Add("Godz.", typeof(string));
            dt.Columns.Add("Palety", typeof(string));
            dt.Columns.Add("Poj.", typeof(int));
            dt.Columns.Add("Klient", typeof(string));
            dt.Columns.Add("Adres", typeof(string));

            var zamowieniaDoPokazania = _wolneZamowienia.AsEnumerable();
            if (!string.IsNullOrEmpty(_filtrZamowien))
            {
                var filtr = _filtrZamowien.ToLower();
                zamowieniaDoPokazania = zamowieniaDoPokazania.Where(z =>
                    (z.KlientNazwa ?? "").ToLower().Contains(filtr) ||
                    (z.Handlowiec ?? "").ToLower().Contains(filtr) ||
                    (z.Adres ?? "").ToLower().Contains(filtr) ||
                    z.ZamowienieId.ToString().Contains(filtr));
            }

            // Grupuj po dacie odbioru (jak w widoku głównym)
            var grouped = zamowieniaDoPokazania
                .OrderBy(z => z.DataOdbioru.Date)
                .ThenBy(z => z.DataPrzyjazdu)
                .GroupBy(z => z.DataOdbioru.Date);

            foreach (var group in grouped)
            {
                // Wiersz nagłówka grupy
                var dzienTygodnia = group.Key.ToString("dddd", plCulture);
                var groupHeader = $"▸ {group.Key:dd.MM} ({dzienTygodnia}) — {group.Count()} zam.";
                var groupRow = dt.NewRow();
                groupRow["ID"] = 0;
                groupRow["IsGroupRow"] = true;
                groupRow["Ubój"] = "";
                groupRow["Odbiór"] = groupHeader;
                groupRow["Godz."] = "";
                groupRow["Palety"] = "";
                groupRow["Poj."] = 0;
                groupRow["Klient"] = "";
                groupRow["Adres"] = "";
                dt.Rows.Add(groupRow);

                foreach (var zam in group)
                {
                    var ubojDzien = zam.DataUboju.HasValue
                        ? zam.DataUboju.Value.ToString("dd.MM", plCulture) + " " + zam.DataUboju.Value.ToString("ddd", plCulture)
                        : "---";
                    var odbiorDzien = zam.DataOdbioru.ToString("dd.MM", plCulture)
                        + " " + zam.DataOdbioru.ToString("ddd", plCulture);

                    dt.Rows.Add(
                        zam.ZamowienieId,
                        false,
                        ubojDzien,
                        odbiorDzien,
                        zam.GodzinaStr,
                        zam.Palety.ToString("N1"),
                        zam.Pojemniki,
                        zam.KlientNazwa,
                        zam.Adres
                    );
                }
            }

            dgvWolneZamowienia.DataSource = dt;
            dgvWolneZamowienia.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            if (dgvWolneZamowienia.Columns["ID"] != null)
                dgvWolneZamowienia.Columns["ID"].Visible = false;
            if (dgvWolneZamowienia.Columns["IsGroupRow"] != null)
                dgvWolneZamowienia.Columns["IsGroupRow"].Visible = false;
            if (dgvWolneZamowienia.Columns["Ubój"] != null)
                dgvWolneZamowienia.Columns["Ubój"].Width = 70;
            if (dgvWolneZamowienia.Columns["Odbiór"] != null)
                dgvWolneZamowienia.Columns["Odbiór"].Width = 70;
            if (dgvWolneZamowienia.Columns["Godz."] != null)
                dgvWolneZamowienia.Columns["Godz."].Width = 46;
            if (dgvWolneZamowienia.Columns["Palety"] != null)
                dgvWolneZamowienia.Columns["Palety"].Width = 48;
            if (dgvWolneZamowienia.Columns["Poj."] != null)
                dgvWolneZamowienia.Columns["Poj."].Width = 42;
            if (dgvWolneZamowienia.Columns["Klient"] != null)
                dgvWolneZamowienia.Columns["Klient"].Width = 140;
            if (dgvWolneZamowienia.Columns["Adres"] != null)
                dgvWolneZamowienia.Columns["Adres"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            // Formatuj wiersze grupujące i koloruj dane
            foreach (DataGridViewRow row in dgvWolneZamowienia.Rows)
            {
                if (row.Cells["IsGroupRow"]?.Value != null && Convert.ToBoolean(row.Cells["IsGroupRow"].Value))
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(235, 230, 245);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(100, 60, 140);
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                    row.Height = 30;
                    continue;
                }

                var zamId = Convert.ToInt32(row.Cells["ID"].Value);
                var zamowienie = _wolneZamowienia.FirstOrDefault(z => z.ZamowienieId == zamId);

                if (zamowienie != null)
                {
                    var dataDoPorownan = UzywajDataUboju && zamowienie.DataUboju.HasValue
                        ? zamowienie.DataUboju.Value.Date
                        : zamowienie.DataOdbioru.Date;

                    var dzis = DateTime.Today;

                    if (dataDoPorownan == dzis.AddDays(1))
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 253, 230);
                    else if (dataDoPorownan == dzis.AddDays(2))
                        row.DefaultCellStyle.BackColor = Color.FromArgb(230, 240, 255);
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

        private async Task DodajZamowienieDoKursu()
        {
            if (dgvWolneZamowienia.CurrentRow == null) return;

            // Pomiń wiersze nagłówków grup
            if (dgvWolneZamowienia.CurrentRow.Cells["IsGroupRow"]?.Value != null
                && Convert.ToBoolean(dgvWolneZamowienia.CurrentRow.Cells["IsGroupRow"].Value))
                return;

            var zamId = Convert.ToInt32(dgvWolneZamowienia.CurrentRow.Cells["ID"].Value);
            var zamowienie = _wolneZamowienia.FirstOrDefault(z => z.ZamowienieId == zamId);
            if (zamowienie == null) return;

            _isUpdating = true;
            try
            {
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
                RefreshLadunkiGrid();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private async Task UsunLadunek()
        {
            if (dgvLadunki.CurrentRow == null) return;

            var ladunekId = Convert.ToInt64(dgvLadunki.CurrentRow.Cells["ID"].Value);
            var ladunek = _ladunki.FirstOrDefault(l => l.LadunekID == ladunekId);
            if (ladunek == null) return;

            _isUpdating = true;
            try
            {
                // 1. SYNCHRONICZNIE: Usuń z listy NATYCHMIAST (przed async)
                _ladunki.Remove(ladunek);
                for (int i = 0; i < _ladunki.Count; i++)
                    _ladunki[i].Kolejnosc = i + 1;

                // 2. Tracking + zwrot do wolnych
                if (ladunek.KodKlienta?.StartsWith("ZAM_") == true)
                {
                    var zamId = int.Parse(ladunek.KodKlienta.Substring(4));

                    var zamDoDodania = _zamowieniaDoDodania.FirstOrDefault(z => z.ZamowienieId == zamId);
                    if (zamDoDodania != null)
                        _zamowieniaDoDodania.Remove(zamDoDodania);
                    else if (ladunekId > 0)
                        _zamowieniaDoUsuniecia.Add(zamId);

                    var zamowienie = await PobierzAktualneZamowienie(zamId);
                    if (zamowienie != null)
                        _wolneZamowienia.Add(zamowienie);
                }

                // 3. Odśwież gridy SYNCHRONICZNIE
                RefreshLadunkiGrid();
                ShowZamowieniaInGrid();
            }
            finally
            {
                _isUpdating = false;
            }
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

            RefreshLadunkiGrid();

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
                RefreshLadunkiGrid();
            }
        }

        #endregion

        #region Pomocnicze metody

        private async Task CheckForZamowieniaUpdates()
        {
            if (!_dataLoaded || _isUpdating) return;
            _isUpdating = true;

            try
            {
                bool anyChanges = false;
                int usuniete = 0;
                int zaktualizowane = 0;
                var doUsuniecia = new List<LadunekWithPalety>();

                foreach (var ladunek in _ladunki.Where(l => l.KodKlienta?.StartsWith("ZAM_") == true).ToList())
                {
                    if (int.TryParse(ladunek.KodKlienta.Substring(4), out int zamId))
                    {
                        var aktualneZamowienie = await PobierzAktualneZamowienie(zamId);

                        // Zamówienie nie istnieje, anulowane lub transport własny → usuń z kursu
                        if (aktualneZamowienie == null ||
                            aktualneZamowienie.Status == "Anulowane" ||
                            aktualneZamowienie.TransportStatus == "Wlasny")
                        {
                            doUsuniecia.Add(ladunek);
                            if (!_zamowieniaDoUsuniecia.Contains(zamId))
                                _zamowieniaDoUsuniecia.Add(zamId);
                            anyChanges = true;
                            continue;
                        }

                        bool changed = false;

                        if (ladunek.Palety != aktualneZamowienie.Palety ||
                            ladunek.PojemnikiE2 != aktualneZamowienie.Pojemniki)
                        {
                            ladunek.PoprzedniePalety = ladunek.Palety;
                            ladunek.PoprzedniePojemniki = ladunek.PojemnikiE2;
                            ladunek.Palety = aktualneZamowienie.Palety;
                            ladunek.PojemnikiE2 = aktualneZamowienie.Pojemniki;
                            changed = true;
                        }

                        if (ladunek.TrybE2 != aktualneZamowienie.TrybE2)
                        {
                            ladunek.TrybE2 = aktualneZamowienie.TrybE2;
                            changed = true;
                        }

                        if (ladunek.DataUboju != aktualneZamowienie.DataUboju)
                        {
                            ladunek.DataUboju = aktualneZamowienie.DataUboju;
                            changed = true;
                        }

                        if (!string.IsNullOrEmpty(aktualneZamowienie.KlientNazwa) &&
                            ladunek.NazwaKlienta != aktualneZamowienie.KlientNazwa)
                        {
                            ladunek.NazwaKlienta = aktualneZamowienie.KlientNazwa;
                            changed = true;
                        }

                        if (!string.IsNullOrEmpty(aktualneZamowienie.Adres) &&
                            ladunek.Adres != aktualneZamowienie.Adres)
                        {
                            ladunek.Adres = aktualneZamowienie.Adres;
                            changed = true;
                        }

                        if (changed)
                        {
                            ladunek.ZmienionyWZamowieniu = true;
                            zaktualizowane++;
                            anyChanges = true;
                        }
                    }
                }

                // Usuń anulowane/własne z listy ładunków
                if (doUsuniecia.Count > 0)
                {
                    foreach (var lad in doUsuniecia)
                        _ladunki.Remove(lad);
                    for (int i = 0; i < _ladunki.Count; i++)
                        _ladunki[i].Kolejnosc = i + 1;
                    usuniete = doUsuniecia.Count;
                }

                if (anyChanges)
                {
                    RefreshLadunkiGrid();
                    await UpdateWypelnienie();

                    if (lblAutoUpdate != null)
                    {
                        string msg;
                        if (usuniete > 0 && zaktualizowane > 0)
                            msg = $"⚠ Usunięto {usuniete} anulowanych, zaktualizowano {zaktualizowane} pozycji";
                        else if (usuniete > 0)
                            msg = $"⚠ Usunięto {usuniete} anulowanych/własnych zamówień z kursu";
                        else
                            msg = $"Zaktualizowano {zaktualizowane} pozycji z zamówień";

                        lblAutoUpdate.Text = msg;
                        lblAutoUpdate.ForeColor = usuniete > 0 ? Color.Red : Color.Orange;
                        lblAutoUpdate.Visible = true;

                        var hideTimer = new Timer { Interval = 8000 };
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
                System.Diagnostics.Debug.WriteLine($"Błąd podczas automatycznej aktualizacji: {ex.Message}");
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private async Task RefreshWolneZamowieniaSilently()
        {
            if (!_dataLoaded || _isUpdating) return;

            try
            {
                var previousIds = new HashSet<int>(_wolneZamowienia.Select(z => z.ZamowienieId));

                // Zapamiętaj zaznaczony wiersz
                int? selectedZamId = null;
                if (dgvWolneZamowienia?.CurrentRow != null)
                {
                    selectedZamId = Convert.ToInt32(dgvWolneZamowienia.CurrentRow.Cells["ID"].Value);
                }

                await LoadWolneZamowieniaForDate(_dtpZamowienia?.Value ?? dtpData.Value);

                var currentIds = new HashSet<int>(_wolneZamowienia.Select(z => z.ZamowienieId));

                if (!previousIds.SetEquals(currentIds))
                {
                    int noweCount = currentIds.Except(previousIds).Count();
                    if (noweCount > 0 && lblAutoUpdate != null)
                    {
                        lblAutoUpdate.Text = $"+{noweCount} nowych zamówień";
                        lblAutoUpdate.ForeColor = Color.FromArgb(52, 152, 219);
                        lblAutoUpdate.Visible = true;

                        var hideTimer = new Timer { Interval = 4000 };
                        hideTimer.Tick += (s, e) =>
                        {
                            lblAutoUpdate.Visible = false;
                            hideTimer.Stop();
                            hideTimer.Dispose();
                        };
                        hideTimer.Start();
                    }
                }

                // Przywróć zaznaczony wiersz
                if (selectedZamId.HasValue && dgvWolneZamowienia != null)
                {
                    foreach (DataGridViewRow row in dgvWolneZamowienia.Rows)
                    {
                        if (Convert.ToInt32(row.Cells["ID"].Value) == selectedZamId.Value)
                        {
                            row.Selected = true;
                            var firstVisCol = dgvWolneZamowienia.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.Visible);
                            if (firstVisCol != null)
                                dgvWolneZamowienia.CurrentCell = row.Cells[firstVisCol.Index];
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd odświeżania wolnych zamówień: {ex.Message}");
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
                        SUM(ISNULL(zmt.Ilosc, 0)) AS IloscKg,
                        zm.DataUboju,
                        ISNULL(zm.TransportStatus, 'Oczekuje') AS TransportStatus,
                        zm.DataZamowienia
                    FROM dbo.ZamowieniaMieso zm
                    LEFT JOIN dbo.ZamowieniaMiesoTowar zmt ON zm.Id = zmt.ZamowienieId
                    WHERE zm.Id = @ZamowienieId
                    GROUP BY zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.Status,
                             zm.LiczbaPalet, zm.LiczbaPojemnikow, zm.TrybE2,
                             zm.DataUboju, zm.TransportStatus, zm.DataZamowienia";

                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@ZamowienieId", zamowienieId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var zamowienie = new ZamowienieDoTransportu
                    {
                        ZamowienieId = reader.GetInt32(0),
                        KlientId = reader.GetInt32(1),
                        DataPrzyjazdu = reader.GetDateTime(2),
                        Status = reader.IsDBNull(3) ? "Nowe" : reader.GetString(3),
                        Palety = reader.GetDecimal(4),
                        Pojemniki = reader.GetInt32(5),
                        TrybE2 = reader.GetBoolean(6),
                        IloscKg = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                        DataUboju = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                        TransportStatus = reader.GetString(9),
                        DataOdbioru = reader.IsDBNull(10) ? DateTime.Today : reader.GetDateTime(10)
                    };

                    // Pobierz dane klienta z Handel DB
                    try
                    {
                        await using var cnHandel = new SqlConnection(_connHandel);
                        await cnHandel.OpenAsync();

                        var sqlKlient = @"
                            SELECT
                                ISNULL(c.Shortcut, 'KH ' + CAST(c.Id AS VARCHAR(10))) AS Nazwa,
                                ISNULL(wym.CDim_Handlowiec_Val, '') AS Handlowiec,
                                ISNULL(poa.Postcode, '') + ' ' + ISNULL(poa.Street, '') AS Adres
                            FROM SSCommon.STContractors c
                            LEFT JOIN SSCommon.ContractorClassification wym ON c.Id = wym.ElementId
                            LEFT JOIN SSCommon.STPostOfficeAddresses poa ON poa.ContactGuid = c.ContactGuid
                                AND poa.AddressName = N'adres domyślny'
                            WHERE c.Id = @KlientId";

                        using var cmdKlient = new SqlCommand(sqlKlient, cnHandel);
                        cmdKlient.Parameters.AddWithValue("@KlientId", zamowienie.KlientId);

                        using var readerKlient = await cmdKlient.ExecuteReaderAsync();
                        if (await readerKlient.ReadAsync())
                        {
                            zamowienie.KlientNazwa = readerKlient.GetString(0);
                            zamowienie.Handlowiec = readerKlient.GetString(1);
                            zamowienie.Adres = readerKlient.GetString(2).Trim();
                        }
                    }
                    catch { /* Handel DB niedostępna - kontynuuj bez danych klienta */ }

                    return zamowienie;
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
                    _progressFill.Width = 0;
                    _progressLabel.Text = "0%";
                    lblStatystyki.Text = "0 palet z 0 dostępnych";
                    return;
                }

                decimal sumaPalet = _ladunki?.Sum(l => l.Palety) ?? 0m;
                int paletyPojazdu = pojazd.PaletyH1;
                decimal procent = paletyPojazdu > 0 ? (sumaPalet / paletyPojazdu * 100m) : 0m;

                int fillWidth = (int)(Math.Min(100, procent) / 100m * progressWypelnienie.Width);
                _progressFill.Width = Math.Max(0, fillWidth);
                _progressLabel.Text = $"{procent:F1}%";
                lblStatystyki.Text = $"{sumaPalet:N1} palet z {paletyPojazdu} dostępnych ({procent:N1}%)";

                Color barColor;
                if (procent > 100)
                {
                    barColor = Color.FromArgb(220, 38, 38);
                    lblWypelnienie.ForeColor = Color.FromArgb(248, 113, 113);
                    lblWypelnienie.Text = $"⚠ PRZEPEŁNIENIE: {procent:F1}%";
                }
                else if (procent > 90)
                {
                    barColor = Color.FromArgb(245, 158, 11);
                    lblWypelnienie.ForeColor = Color.FromArgb(251, 191, 36);
                    lblWypelnienie.Text = $"WYPEŁNIENIE: {procent:F1}%";
                }
                else if (procent > 70)
                {
                    barColor = Color.FromArgb(234, 179, 8);
                    lblWypelnienie.ForeColor = Color.FromArgb(156, 163, 175);
                    lblWypelnienie.Text = $"WYPEŁNIENIE: {procent:F1}%";
                }
                else
                {
                    barColor = Color.FromArgb(34, 197, 94);
                    lblWypelnienie.ForeColor = Color.FromArgb(156, 163, 175);
                    lblWypelnienie.Text = $"WYPEŁNIENIE: {procent:F1}%";
                }
                _progressFill.BackColor = barColor;
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
                    RefreshLadunkiGrid();

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

        #region Drag & Drop

        private void DgvWolneZamowienia_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var hitTest = dgvWolneZamowienia.HitTest(e.X, e.Y);
                if (hitTest.RowIndex >= 0)
                {
                    // Nie rozpoczynaj drag z wiersza grupy
                    var row = dgvWolneZamowienia.Rows[hitTest.RowIndex];
                    if (row.Cells["IsGroupRow"]?.Value != null && Convert.ToBoolean(row.Cells["IsGroupRow"].Value))
                    {
                        _dragBoxFromMouseDown = Rectangle.Empty;
                        _dragSource = null;
                        return;
                    }

                    _dragRowIndex = hitTest.RowIndex;
                    _dragSource = "wolne";
                    Size dragSize = SystemInformation.DragSize;
                    _dragBoxFromMouseDown = new Rectangle(
                        new Point(e.X - dragSize.Width / 2, e.Y - dragSize.Height / 2), dragSize);
                }
                else
                {
                    _dragBoxFromMouseDown = Rectangle.Empty;
                    _dragSource = null;
                }
            }
        }

        private void DgvLadunki_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var hitTest = dgvLadunki.HitTest(e.X, e.Y);
                if (hitTest.RowIndex >= 0)
                {
                    _dragRowIndex = hitTest.RowIndex;
                    _dragSource = "ladunki";
                    Size dragSize = SystemInformation.DragSize;
                    _dragBoxFromMouseDown = new Rectangle(
                        new Point(e.X - dragSize.Width / 2, e.Y - dragSize.Height / 2), dragSize);
                }
                else
                {
                    _dragBoxFromMouseDown = Rectangle.Empty;
                    _dragSource = null;
                }
            }
        }

        private void DgvWolneZamowienia_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _dragBoxFromMouseDown != Rectangle.Empty && _dragSource == "wolne")
            {
                if (!_dragBoxFromMouseDown.Contains(e.X, e.Y))
                {
                    _isDragging = true;
                    // Przekazujemy ID zamówienia zamiast rowIndex (bezpieczniejsze)
                    if (_dragRowIndex >= 0 && _dragRowIndex < dgvWolneZamowienia.Rows.Count)
                    {
                        var zamId = Convert.ToInt32(dgvWolneZamowienia.Rows[_dragRowIndex].Cells["ID"].Value);
                        dgvWolneZamowienia.DoDragDrop(zamId, DragDropEffects.Move);
                    }
                    _isDragging = false;
                    _dragBoxFromMouseDown = Rectangle.Empty;
                }
            }
        }

        private void DgvLadunki_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _dragBoxFromMouseDown != Rectangle.Empty && _dragSource == "ladunki")
            {
                if (!_dragBoxFromMouseDown.Contains(e.X, e.Y))
                {
                    _isDragging = true;
                    // Przekazujemy LadunekID zamiast rowIndex (bezpieczniejsze)
                    if (_dragRowIndex >= 0 && _dragRowIndex < dgvLadunki.Rows.Count)
                    {
                        var ladunekId = Convert.ToInt64(dgvLadunki.Rows[_dragRowIndex].Cells["ID"].Value);
                        dgvLadunki.DoDragDrop(ladunekId, DragDropEffects.Move);
                    }
                    _isDragging = false;
                    _dragBoxFromMouseDown = Rectangle.Empty;
                }
            }
        }

        private void DgvLadunki_DragEnter(object sender, DragEventArgs e)
        {
            // Akceptujemy drop z wolnych zamówień
            if (_dragSource == "wolne" && e.Data.GetDataPresent(typeof(int)))
            {
                e.Effect = DragDropEffects.Move;
                dgvLadunki.BackgroundColor = Color.FromArgb(220, 240, 255);
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void DgvWolneZamowienia_DragEnter(object sender, DragEventArgs e)
        {
            // Akceptujemy drop z ładunków - ale tylko ZAM_
            if (_dragSource == "ladunki" && e.Data.GetDataPresent(typeof(long)))
            {
                var ladunekId = (long)e.Data.GetData(typeof(long));
                var ladunek = _ladunki.FirstOrDefault(l => l.LadunekID == ladunekId);
                if (ladunek?.KodKlienta?.StartsWith("ZAM_") == true)
                {
                    e.Effect = DragDropEffects.Move;
                    dgvWolneZamowienia.BackgroundColor = Color.FromArgb(220, 240, 255);
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void DgvTarget_DragOver(object sender, DragEventArgs e)
        {
            // KRYTYCZNE: DragOver musi podtrzymywać e.Effect, inaczej drop nie zadziała
            if (sender == dgvLadunki && _dragSource == "wolne" && e.Data.GetDataPresent(typeof(int)))
            {
                e.Effect = DragDropEffects.Move;
            }
            else if (sender == dgvWolneZamowienia && _dragSource == "ladunki" && e.Data.GetDataPresent(typeof(long)))
            {
                var ladunekId = (long)e.Data.GetData(typeof(long));
                var ladunek = _ladunki.FirstOrDefault(l => l.LadunekID == ladunekId);
                e.Effect = ladunek?.KodKlienta?.StartsWith("ZAM_") == true
                    ? DragDropEffects.Move
                    : DragDropEffects.None;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void DgvLadunki_DragDrop(object sender, DragEventArgs e)
        {
            dgvLadunki.BackgroundColor = Color.White;
            _dragBoxFromMouseDown = Rectangle.Empty;

            if (_dragSource != "wolne" || !e.Data.GetDataPresent(typeof(int)))
            {
                _dragSource = null;
                return;
            }

            var zamId = (int)e.Data.GetData(typeof(int));
            _dragSource = null;

            var zamowienie = _wolneZamowienia.FirstOrDefault(z => z.ZamowienieId == zamId);
            if (zamowienie == null) return;

            _isUpdating = true;
            try
            {
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
                    DataUboju = zamowienie.DataUboju,
                    Kolejnosc = _ladunki.Count + 1
                };

                _ladunki.Add(ladunek);
                _wolneZamowienia.Remove(zamowienie);

                ShowZamowieniaInGrid();
                RefreshLadunkiGrid();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private async void DgvWolneZamowienia_DragDrop(object sender, DragEventArgs e)
        {
            dgvWolneZamowienia.BackgroundColor = Color.White;
            _dragBoxFromMouseDown = Rectangle.Empty;

            if (_dragSource != "ladunki" || !e.Data.GetDataPresent(typeof(long)))
            {
                _dragSource = null;
                return;
            }

            var ladunekId = (long)e.Data.GetData(typeof(long));
            _dragSource = null;

            var ladunek = _ladunki.FirstOrDefault(l => l.LadunekID == ladunekId);
            if (ladunek == null) return;
            if (ladunek.KodKlienta?.StartsWith("ZAM_") != true) return;

            var zamIdStr = ladunek.KodKlienta.Substring(4);
            if (!int.TryParse(zamIdStr, out var zamId)) return;

            // Blokada timera na czas operacji
            _isUpdating = true;
            try
            {
                // 1. SYNCHRONICZNIE: Usuń ładunek z listy
                _ladunki.Remove(ladunek);
                for (int i = 0; i < _ladunki.Count; i++)
                    _ladunki[i].Kolejnosc = i + 1;

                // 2. Aktualizuj tracking list
                var zamDoDodania = _zamowieniaDoDodania.FirstOrDefault(z => z.ZamowienieId == zamId);
                if (zamDoDodania != null)
                    _zamowieniaDoDodania.Remove(zamDoDodania);
                else if (ladunekId > 0)
                    _zamowieniaDoUsuniecia.Add(zamId);

                // 3. SYNCHRONICZNIE: Odśwież grid kursu NATYCHMIAST (przed jakimkolwiek await)
                RefreshLadunkiGrid();

                // 4. ASYNC: Pobierz dane zamówienia i dodaj do wolnych
                var zamowienie = await PobierzAktualneZamowienie(zamId);
                if (zamowienie != null)
                    _wolneZamowienia.Add(zamowienie);
                ShowZamowieniaInGrid();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void DgvTarget_DragLeave(object sender, EventArgs e)
        {
            if (sender is DataGridView dgv)
            {
                dgv.BackgroundColor = Color.White;
            }
        }

        private void DgvEditorWolneZamowienia_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvWolneZamowienia.Rows[e.RowIndex];
            if (row.Cells["IsGroupRow"]?.Value == null || !Convert.ToBoolean(row.Cells["IsGroupRow"].Value))
                return;

            e.Handled = true;
            var bgColor = row.Selected ? e.CellStyle.SelectionBackColor : Color.FromArgb(235, 230, 245);
            using (var brush = new SolidBrush(bgColor))
                e.Graphics.FillRectangle(brush, e.CellBounds);

            var colName = dgvWolneZamowienia.Columns[e.ColumnIndex].Name;

            // Rysuj tekst nagłówka grupy scalony w kolumnach Odbiór + Godz.
            if (colName == "Odbiór")
            {
                var headerText = row.Cells["Odbiór"]?.Value?.ToString() ?? "";
                var odbiorWidth = dgvWolneZamowienia.Columns["Odbiór"]?.Width ?? 0;
                var godzWidth = dgvWolneZamowienia.Columns["Godz."]?.Width ?? 0;
                var mergedWidth = odbiorWidth + godzWidth;

                using (var font = new Font("Segoe UI", 7.5F, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.FromArgb(100, 60, 140)))
                {
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                    e.Graphics.DrawString(headerText, font, textBrush,
                        new RectangleF(e.CellBounds.Left + 4, e.CellBounds.Top, mergedWidth - 6, e.CellBounds.Height), sf);
                }
            }

            // Dolna linia
            using (var pen = new Pen(Color.FromArgb(210, 200, 225)))
                e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                    e.CellBounds.Right, e.CellBounds.Bottom - 1);
        }

        #endregion

        #region Zapis

        private async void BtnZapisz_Click(object sender, EventArgs e)
        {
            // Kierowca i pojazd są teraz opcjonalne - można przypisać później
            // Pokazujemy ostrzeżenie, ale pozwalamy zapisać
            var kierowca = cboKierowca.SelectedItem as Kierowca;
            var pojazd = cboPojazd.SelectedItem as Pojazd;

            if (kierowca == null || pojazd == null)
            {
                var brakujace = new List<string>();
                if (kierowca == null) brakujace.Add("kierowca");
                if (pojazd == null) brakujace.Add("pojazd");

                var wynik = MessageBox.Show(
                    $"Nie przypisano: {string.Join(", ", brakujace)}.\n\n" +
                    "Kurs zostanie zapisany ze statusem 'Planowany' - będzie oznaczony wizualnie do przydzielenia.\n" +
                    "Czy kontynuować?",
                    "Brak przypisania zasobów",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (wynik != DialogResult.Yes)
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

            // Status zawsze "Planowany" - brak przypisania jest wizualnie oznaczony w UI
            var status = "Planowany";

            var kurs = new Kurs
            {
                KursID = _kursId ?? 0,
                DataKursu = dtpData.Value.Date,
                // Kierowca i pojazd mogą być null - zostanie przypisane później
                KierowcaID = kierowca?.KierowcaID,
                PojazdID = pojazd?.PojazdID,
                Trasa = string.IsNullOrWhiteSpace(txtTrasa.Text) ? null : txtTrasa.Text.Trim(),
                GodzWyjazdu = godzWyjazdu,
                GodzPowrotu = godzPowrotu,
                Status = status,
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
            await using var transaction = await cn.BeginTransactionAsync();

            try
            {
                var sqlDelete = "DELETE FROM dbo.Ladunek WHERE KursID = @KursID";
                using var cmdDelete = new SqlCommand(sqlDelete, cn, (SqlTransaction)transaction);
                cmdDelete.Parameters.AddWithValue("@KursID", _kursId.Value);
                await cmdDelete.ExecuteNonQueryAsync();

                foreach (var ladunek in _ladunki.OrderBy(l => l.Kolejnosc))
                {
                    var sqlInsert = @"INSERT INTO dbo.Ladunek
                        (KursID, Kolejnosc, KodKlienta, PaletyH1, PojemnikiE2, Uwagi, TrybE2, PlanE2NaPaleteOverride)
                        VALUES (@KursID, @Kolejnosc, @KodKlienta, @Palety, @Pojemniki, @Uwagi, @TrybE2, @PlanE2NaPaleteOverride)";

                    using var cmdInsert = new SqlCommand(sqlInsert, cn, (SqlTransaction)transaction);
                    cmdInsert.Parameters.AddWithValue("@KursID", _kursId.Value);
                    cmdInsert.Parameters.AddWithValue("@Kolejnosc", ladunek.Kolejnosc);
                    cmdInsert.Parameters.AddWithValue("@KodKlienta", (object)ladunek.KodKlienta ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@Palety", ladunek.Palety);
                    cmdInsert.Parameters.AddWithValue("@Pojemniki", ladunek.PojemnikiE2);
                    cmdInsert.Parameters.AddWithValue("@Uwagi", (object)ladunek.Uwagi ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@TrybE2", ladunek.TrybE2);
                    cmdInsert.Parameters.AddWithValue("@PlanE2NaPaleteOverride", (object)ladunek.PlanE2NaPaleteOverride ?? DBNull.Value);
                    await cmdInsert.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
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

        private async void BtnNowyKierowca_Click(object sender, EventArgs e)
        {
            using var dlg = new DodajKierowceDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.NowyKierowca != null)
            {
                try
                {
                    await _repozytorium.DodajKierowceAsync(dlg.NowyKierowca);

                    _kierowcy = (await _repozytorium.PobierzKierowcowAsync()).ToList();
                    cboKierowca.DataSource = null;
                    cboKierowca.DataSource = _kierowcy;
                    cboKierowca.DisplayMember = "PelneNazwisko";

                    // Auto-select the new driver
                    var nowy = _kierowcy.FirstOrDefault(k =>
                        k.Imie == dlg.NowyKierowca.Imie && k.Nazwisko == dlg.NowyKierowca.Nazwisko);
                    if (nowy != null) cboKierowca.SelectedItem = nowy;

                    MessageBox.Show($"Kierowca {dlg.NowyKierowca.PelneNazwisko} został dodany.",
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd zapisu kierowcy:\n{ex.Message}",
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void BtnNowyPojazd_Click(object sender, EventArgs e)
        {
            using var dlg = new DodajPojazdDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.NowyPojazd != null)
            {
                try
                {
                    await _repozytorium.DodajPojazdAsync(dlg.NowyPojazd);

                    _pojazdy = (await _repozytorium.PobierzPojazdyAsync()).ToList();
                    cboPojazd.DataSource = null;
                    cboPojazd.DataSource = _pojazdy;
                    cboPojazd.DisplayMember = "Opis";

                    // Auto-select the new vehicle
                    var nowy = _pojazdy.FirstOrDefault(p => p.Rejestracja == dlg.NowyPojazd.Rejestracja);
                    if (nowy != null) cboPojazd.SelectedItem = nowy;

                    MessageBox.Show($"Pojazd {dlg.NowyPojazd.Rejestracja} został dodany.",
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd zapisu pojazdu:\n{ex.Message}",
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
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

        #region Skróty klawiszowe

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Ctrl+S → Zapisz kurs
            if (keyData == (Keys.Control | Keys.S))
            {
                BtnZapisz_Click(this, EventArgs.Empty);
                return true;
            }

            // Escape → Zamknij edytor
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }

            // Delete → Usuń zaznaczony ładunek (gdy fokus na dgvLadunki)
            if (keyData == Keys.Delete && dgvLadunki != null && dgvLadunki.Focused)
            {
                _ = UsunLadunek();
                return true;
            }

            // Enter → Dodaj zaznaczone zamówienie do kursu (gdy fokus na wolnych zamówieniach)
            if (keyData == Keys.Enter && dgvWolneZamowienia != null && dgvWolneZamowienia.Focused)
            {
                _ = DodajZamowienieDoKursu();
                return true;
            }

            // Ctrl+Up → Przesuń ładunek w górę
            if (keyData == (Keys.Control | Keys.Up) && dgvLadunki != null && dgvLadunki.Focused)
            {
                _ = PrzesunLadunek(-1);
                return true;
            }

            // Ctrl+Down → Przesuń ładunek w dół
            if (keyData == (Keys.Control | Keys.Down) && dgvLadunki != null && dgvLadunki.Focused)
            {
                _ = PrzesunLadunek(1);
                return true;
            }

            // Ctrl+F → Fokus na szukaj zamówień
            if (keyData == (Keys.Control | Keys.F) && txtSzukajZamowienia != null)
            {
                txtSzukajZamowienia.Focus();
                if (txtSzukajZamowienia.ForeColor == Color.Gray)
                {
                    txtSzukajZamowienia.Text = "";
                    txtSzukajZamowienia.ForeColor = Color.Black;
                }
                txtSzukajZamowienia.SelectAll();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion
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