// Plik: Transport/EdytorKursuWithPalety.cs
// Wersja z poprawioną obsługą duplikatów kluczy w tabeli Ladunek

using Kalendarz1.Transport.Pakowanie;
using Kalendarz1.Transport.Repozytorium;
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

        // Panel ręcznego dodawania
        private TextBox txtKlient;
        private NumericUpDown nudPalety;
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

        // Przyciski główne
        private Button btnZapisz;
        private Button btnAnuluj;

        // Rozszerzona klasa ładunku z paletami
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
        }

        // Dialog do dodawania kierowcy
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

        // Dialog do dodawania pojazdu
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

        // Klasa dla zamówień
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
            public string GodzinaStr => DataPrzyjazdu.ToString("HH:mm");
            public string Status { get; set; } = "Nowe";
            public string Handlowiec { get; set; } = "";
            public string Adres { get; set; } = "";
        }

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

            InitializeComponent();
            dtpData.Value = data ?? DateTime.Today;
            _ = LoadDataAsync();
        }

        private void InitializeComponent()
        {
            Text = _kursId.HasValue ? "Edycja kursu transportowego" : "Nowy kurs transportowy";
            Size = new Size(1600, 1000);
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));

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

            // Pierwsza linia - kierowca i pojazd
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

            // Druga linia - godziny i trasa
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
                PlaceholderText = "Trasa zostanie uzupełniona automatycznie na podstawie klientów...",
                ReadOnly = true
            };

            // Trzecia linia - wskaźnik wypełnienia
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
                lblData, dtpData,
                lblGodziny, txtGodzWyjazdu, lblDo, txtGodzPowrotu,
                lblTrasa, txtTrasa,
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

            // Panel z przyciskami kolejności
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
                Text = "🔄 Sortuj",
                Location = new Point(195, 8),
                Size = new Size(80, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            toolTip.SetToolTip(btnSortujPoKolejnosci, "Przywróć domyślną kolejność");
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
            var menuUsun = new ToolStripMenuItem("Usuń", null, async (s, e) => await UsunLadunek());
            var menuEdytuj = new ToolStripMenuItem("Edytuj", null, (s, e) => EdytujLadunek());
            var menuGora = new ToolStripMenuItem("Przesuń w górę", null, async (s, e) => await PrzesunLadunek(-1));
            var menuDol = new ToolStripMenuItem("Przesuń w dół", null, async (s, e) => await PrzesunLadunek(1));
            var separator = new ToolStripSeparator();
            contextMenu.Items.Add(menuEdytuj);
            contextMenu.Items.Add(menuUsun);
            contextMenu.Items.Add(separator);
            contextMenu.Items.Add(menuGora);
            contextMenu.Items.Add(menuDol);
            dgvLadunki.ContextMenuStrip = contextMenu;

            panel.Controls.Add(panelKolejnosc);
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

            var lblPalety = CreateLabel("Palety:", 350, 20, 60);
            nudPalety = new NumericUpDown
            {
                Location = new Point(420, 18),
                Size = new Size(100, 26),
                Font = new Font("Segoe UI", 10F),
                Maximum = 100,
                Minimum = 0,
                DecimalPlaces = 1,
                Increment = 0.5m,
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
                lblKlient, txtKlient, lblPalety, nudPalety,
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

            panel.Resize += (s, e) =>
            {
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

        private async Task PrzesunLadunek(int kierunek)
        {
            if (dgvLadunki.CurrentRow == null) return;

            var ladunekId = Convert.ToInt64(dgvLadunki.CurrentRow.Cells["ID"].Value);
            var ladunek = _ladunki.FirstOrDefault(l => l.LadunekID == ladunekId);
            if (ladunek == null) return;

            int currentIndex = _ladunki.IndexOf(ladunek);
            int newIndex = currentIndex + kierunek;

            if (newIndex < 0 || newIndex >= _ladunki.Count) return;

            // Zamień miejscami w liście
            var temp = _ladunki[currentIndex];
            _ladunki[currentIndex] = _ladunki[newIndex];
            _ladunki[newIndex] = temp;

            // Zaktualizuj kolejność
            for (int i = 0; i < _ladunki.Count; i++)
            {
                _ladunki[i].Kolejnosc = i + 1;
            }

            // Zapisz do bazy
            if (_kursId.HasValue)
            {
                await SaveKolejnoscToBaza();
            }

            // Odśwież widok zachowując selekcję
            await LoadLadunki();

            // Przywróć selekcję
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

            // Ustaw pozycję dialogu tak, aby nie nachodzić na panel zamówień
            dlg.StartPosition = FormStartPosition.Manual;

            // Oblicz pozycję względem lewego panelu (ładunki)
            // Dialog będzie wycentrowany nad lewą częścią okna (panel ładunków)
            var leftPanelWidth = this.Width * 0.55; // 55% szerokości to lewa kolumna
            var centerX = this.Left + (int)(leftPanelWidth / 2) - (dlg.Width / 2);
            var centerY = this.Top + 250; // Pozycja Y poniżej nagłówka

            // Sprawdź czy dialog mieści się w lewej części ekranu
            var screen = Screen.FromControl(this);

            // Upewnij się, że dialog nie wyjdzie poza lewą stronę
            if (centerX < 20)
                centerX = 20;

            // Upewnij się, że dialog nie nachodzi na prawy panel (zamówienia)
            var maxRightPosition = this.Left + (int)leftPanelWidth - dlg.Width - 20;
            if (centerX > maxRightPosition)
                centerX = maxRightPosition;

            // Sprawdź górną i dolną granicę ekranu
            if (centerY < 20)
                centerY = 20;
            if (centerY + dlg.Height > screen.WorkingArea.Bottom)
                centerY = screen.WorkingArea.Bottom - dlg.Height - 20;

            dlg.Location = new Point(centerX, centerY);

            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ZmienioneKolejnosci != null)
            {
                _ladunki = dlg.ZmienioneKolejnosci;

                if (_kursId.HasValue)
                {
                    await SaveKolejnoscToBaza();
                }

                await LoadLadunki();
            }
        }
        private async Task SaveKolejnoscToBaza()
        {
            try
            {
                await using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync();

                // Najpierw usuń istniejące ładunki z bazy
                var sqlDelete = "DELETE FROM dbo.Ladunek WHERE KursID = @KursID";
                using var cmdDelete = new SqlCommand(sqlDelete, cn);
                cmdDelete.Parameters.AddWithValue("@KursID", _kursId.Value);
                await cmdDelete.ExecuteNonQueryAsync();

                // Wstaw ponownie z nową kolejnością
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
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania kolejności: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateTrasa()
        {
            // Aktualizuj trasę na podstawie aktualnej kolejności
            var klientNazwy = new List<string>();

            foreach (var ladunek in _ladunki.OrderBy(l => l.Kolejnosc))
            {
                string nazwa = "";

                // Pobierz nazwę klienta
                if (!string.IsNullOrEmpty(ladunek.NazwaKlienta))
                {
                    nazwa = ladunek.NazwaKlienta;
                }
                else if (!string.IsNullOrEmpty(ladunek.Uwagi))
                {
                    // Próbuj wyciągnąć nazwę z uwag
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

            // Jeśli nie ma żadnych ładunków, wyczyść trasę
            if (klientNazwy.Count == 0)
            {
                txtTrasa.Text = "";
            }
            else
            {
                txtTrasa.Text = string.Join(" → ", klientNazwy.Distinct());
            }
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

                // Pobierz zamówienia z paletami
                var sql = @"
                    SELECT DISTINCT
                        zm.Id AS ZamowienieId,
                        zm.KlientId,
                        zm.DataPrzyjazdu,
                        zm.Status,
                        zm.Uwagi,
                        ISNULL(zm.LiczbaPalet, 0) AS LiczbaPalet,
                        ISNULL(zm.LiczbaPojemnikow, 0) AS LiczbaPojemnikow,
                        ISNULL(zm.TrybE2, 0) AS TrybE2,
                        SUM(ISNULL(zmt.Ilosc, 0)) AS IloscKg
                    FROM dbo.ZamowieniaMieso zm
                    LEFT JOIN dbo.ZamowieniaMiesoTowar zmt ON zm.Id = zmt.ZamowienieId
                    WHERE zm.DataZamowienia = @Data
                      AND ISNULL(zm.Status, 'Nowe') NOT IN ('Anulowane', 'Zrealizowane')
                      AND ISNULL(zm.TransportStatus, 'Oczekuje') = 'Oczekuje'
                    GROUP BY zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.Status, zm.Uwagi,
                             zm.LiczbaPalet, zm.LiczbaPojemnikow, zm.TrybE2
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
                        Palety = reader.GetDecimal(5),
                        Pojemniki = reader.GetInt32(6),
                        TrybE2 = reader.GetBoolean(7),
                        IloscKg = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8)
                    };
                    _wolneZamowienia.Add(zamowienie);
                }

                // Pobierz dane klientów
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
            dt.Columns.Add("Palety", typeof(decimal));
            dt.Columns.Add("Pojemniki", typeof(int));
            dt.Columns.Add("Handlowiec", typeof(string));
            dt.Columns.Add("Adres", typeof(string));

            foreach (var zam in _wolneZamowienia.OrderBy(z => z.DataPrzyjazdu))
            {
                dt.Rows.Add(
                    zam.ZamowienieId,
                    zam.KlientNazwa,
                    zam.GodzinaStr,
                    zam.Palety,
                    zam.Pojemniki,
                    zam.Handlowiec,
                    zam.Adres
                );
            }

            dgvWolneZamowienia.DataSource = dt;

            if (dgvWolneZamowienia.Columns["ID"] != null)
                dgvWolneZamowienia.Columns["ID"].Visible = false;

            if (dgvWolneZamowienia.Columns["Klient"] != null)
                dgvWolneZamowienia.Columns["Klient"].Width = 180;
            if (dgvWolneZamowienia.Columns["Godz."] != null)
                dgvWolneZamowienia.Columns["Godz."].Width = 50;
            if (dgvWolneZamowienia.Columns["Palety"] != null)
            {
                dgvWolneZamowienia.Columns["Palety"].Width = 60;
                dgvWolneZamowienia.Columns["Palety"].DefaultCellStyle.Format = "N1";
                dgvWolneZamowienia.Columns["Palety"].DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }
            if (dgvWolneZamowienia.Columns["Pojemniki"] != null)
            {
                dgvWolneZamowienia.Columns["Pojemniki"].Width = 80;
                dgvWolneZamowienia.Columns["Pojemniki"].DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                dgvWolneZamowienia.Columns["Pojemniki"].DefaultCellStyle.ForeColor = Color.Blue;
                dgvWolneZamowienia.Columns["Pojemniki"].HeaderText = "Pojemniki";
            }
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
                        row.DefaultCellStyle.BackColor = Color.LightBlue;
                    else if (godz.Hours > 14)
                        row.DefaultCellStyle.BackColor = Color.LightSalmon;
                }
            }
        }

        private async Task DodajZamowienieDoKursu()
        {
            if (dgvWolneZamowienia.CurrentRow == null) return;

            var zamId = Convert.ToInt32(dgvWolneZamowienia.CurrentRow.Cells["ID"].Value);
            var zamowienie = _wolneZamowienia.FirstOrDefault(z => z.ZamowienieId == zamId);
            if (zamowienie == null) return;

            if (!_kursId.HasValue || _kursId.Value <= 0)
            {
                await SaveKurs();
                if (!_kursId.HasValue || _kursId.Value <= 0) return;
            }

            // Dodaj jako ładunek z dokładną liczbą palet
            var ladunek = new LadunekWithPalety
            {
                KursID = _kursId.Value,
                KodKlienta = $"ZAM_{zamowienie.ZamowienieId}",
                Palety = zamowienie.Palety,
                PojemnikiE2 = zamowienie.Pojemniki,
                TrybE2 = zamowienie.TrybE2,
                Uwagi = $"{zamowienie.KlientNazwa} ({zamowienie.GodzinaStr})",
                Adres = zamowienie.Adres,
                NazwaKlienta = zamowienie.KlientNazwa,
                Kolejnosc = _ladunki.Count + 1
            };

            await SaveLadunekToDatabase(ladunek);

            // Zaktualizuj status transportu w zamówieniu
            await UpdateTransportStatus(zamowienie.ZamowienieId, "Przypisany");

            _wolneZamowienia.Remove(zamowienie);
            ShowZamowieniaInGrid();
            await LoadLadunki();
        }

        private async Task UpdateTransportStatus(int zamowienieId, string status)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var sql = @"UPDATE dbo.ZamowieniaMieso 
                           SET TransportStatus = @Status, TransportKursId = @KursId 
                           WHERE Id = @Id";

                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Status", status);
                cmd.Parameters.AddWithValue("@KursId", _kursId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Id", zamowienieId);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd aktualizacji statusu transportu: {ex.Message}");
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

            _ladunki = await LoadLadunkiFromDatabase(_kursId.Value);

            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(long));
            dt.Columns.Add("Lp.", typeof(int));
            dt.Columns.Add("Klient", typeof(string));
            dt.Columns.Add("Palety", typeof(decimal));
            dt.Columns.Add("Pojemniki", typeof(int));
            dt.Columns.Add("Adres", typeof(string));
            dt.Columns.Add("Uwagi", typeof(string));

            foreach (var ladunek in _ladunki.OrderBy(l => l.Kolejnosc))
            {
                string klientNazwa = ladunek.NazwaKlienta ?? "";

                // Jeśli nie mamy nazwy klienta, spróbuj ją wydobyć
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

                // Pobierz adres - najpierw sprawdź czy jest w obiekcie, jeśli nie to pobierz z bazy
                string adres = ladunek.Adres ?? "";
                if (string.IsNullOrEmpty(adres))
                {
                    adres = await PobierzAdresKlientaAsync(ladunek.KodKlienta);
                    ladunek.Adres = adres;
                }

                dt.Rows.Add(
                    ladunek.LadunekID,
                    ladunek.Kolejnosc,
                    klientNazwa,
                    ladunek.Palety,
                    ladunek.PojemnikiE2,
                    adres,
                    ladunek.Uwagi ?? ""
                );
            }

            dgvLadunki.DataSource = dt;

            // Konfiguracja kolumn
            if (dgvLadunki.Columns["ID"] != null)
                dgvLadunki.Columns["ID"].Visible = false;
            if (dgvLadunki.Columns["Lp."] != null)
                dgvLadunki.Columns["Lp."].Width = 40;
            if (dgvLadunki.Columns["Klient"] != null)
                dgvLadunki.Columns["Klient"].Width = 150;
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
                dgvLadunki.Columns["Pojemniki"].HeaderText = "Pojemniki";
            }
            if (dgvLadunki.Columns["Adres"] != null)
            {
                dgvLadunki.Columns["Adres"].Width = 180;
                dgvLadunki.Columns["Adres"].DefaultCellStyle.Font = new Font("Segoe UI", 8.5F);
                dgvLadunki.Columns["Adres"].DefaultCellStyle.ForeColor = Color.FromArgb(100, 100, 100);
            }
            if (dgvLadunki.Columns["Uwagi"] != null)
            {
                dgvLadunki.Columns["Uwagi"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }

            // Aktualizuj trasę
            UpdateTrasa();

            await UpdateWypelnienie();
        }

        // Metoda do pobierania adresu dla istniejącego ładunku na podstawie kodu klienta
        private async Task<string> PobierzAdresKlientaAsync(string kodKlienta)
        {
            try
            {
                // Jeśli to zamówienie (ZAM_ID), pobierz adres z bazy
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
                lblStatystyki.Text = $"{sumaPalet:N1} palet z {paletyPojazdu} dostępnych";

                // Kolorowanie
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

            if (nudPalety.Value <= 0)
            {
                MessageBox.Show("Podaj liczbę palet.", "Brak danych",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nudPalety.Focus();
                return;
            }

            if (!_kursId.HasValue || _kursId.Value <= 0)
            {
                await SaveKurs();
                if (!_kursId.HasValue || _kursId.Value <= 0) return;
            }

            var ladunek = new LadunekWithPalety
            {
                KursID = _kursId.Value,
                KodKlienta = txtKlient.Text.Trim(),
                NazwaKlienta = txtKlient.Text.Trim(),
                Palety = nudPalety.Value,
                PojemnikiE2 = (int)(nudPalety.Value * 36), // Domyślnie 36 poj/paletę
                TrybE2 = false,
                Uwagi = string.IsNullOrWhiteSpace(txtUwagi.Text) ? null : txtUwagi.Text.Trim(),
                Kolejnosc = _ladunki.Count + 1
            };

            await SaveLadunekToDatabase(ladunek);

            txtKlient.Clear();
            nudPalety.Value = 0;
            txtUwagi.Clear();
            txtKlient.Focus();

            await LoadLadunki();
        }

        private async Task UsunLadunek()
        {
            if (dgvLadunki.CurrentRow == null) return;

            var ladunekId = Convert.ToInt64(dgvLadunki.CurrentRow.Cells["ID"].Value);
            var ladunek = _ladunki.FirstOrDefault(l => l.LadunekID == ladunekId);

            // Jeśli to zamówienie, przywróć jego status
            if (ladunek?.KodKlienta?.StartsWith("ZAM_") == true)
            {
                var zamId = int.Parse(ladunek.KodKlienta.Substring(4));
                await UpdateTransportStatus(zamId, "Oczekuje");
            }

            await DeleteLadunekFromDatabase(ladunekId);
            await LoadWolneZamowienia();
            await LoadLadunki();
        }

        private void EdytujLadunek()
        {
            if (dgvLadunki.CurrentRow == null) return;

            var row = dgvLadunki.CurrentRow;
            txtKlient.Text = row.Cells["Klient"].Value?.ToString() ?? "";
            nudPalety.Value = Convert.ToDecimal(row.Cells["Palety"].Value ?? 0);
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

        // Dodatkowe metody do obsługi kierowców i pojazdów
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

        // Metody do pracy z bazą danych
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
                ladunki.Add(new LadunekWithPalety
                {
                    LadunekID = reader.GetInt64(0),
                    KursID = reader.GetInt64(1),
                    Kolejnosc = reader.GetInt32(2),
                    KodKlienta = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Palety = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetInt32(4)),
                    PojemnikiE2 = reader.GetInt32(5),
                    Uwagi = reader.IsDBNull(6) ? null : reader.GetString(6),
                    TrybE2 = reader.GetBoolean(7)
                });
            }

            return ladunki;
        }

        private async Task SaveLadunekToDatabase(LadunekWithPalety ladunek)
        {
            await using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync();

            // Najpierw usuń wszystkie istniejące ładunki
            var sqlDelete = "DELETE FROM dbo.Ladunek WHERE KursID = @KursID";
            using var cmdDelete = new SqlCommand(sqlDelete, cn);
            cmdDelete.Parameters.AddWithValue("@KursID", ladunek.KursID);
            await cmdDelete.ExecuteNonQueryAsync();

            // Dodaj z powrotem wszystkie ładunki z lokalnej listy
            _ladunki.Add(ladunek);

            foreach (var lad in _ladunki.OrderBy(l => l.Kolejnosc))
            {
                var sqlInsert = @"INSERT INTO dbo.Ladunek 
                    (KursID, Kolejnosc, KodKlienta, PaletyH1, PojemnikiE2, Uwagi, TrybE2) 
                    VALUES (@KursID, @Kolejnosc, @KodKlienta, @Palety, @Pojemniki, @Uwagi, @TrybE2)";

                using var cmdInsert = new SqlCommand(sqlInsert, cn);
                cmdInsert.Parameters.AddWithValue("@KursID", lad.KursID);
                cmdInsert.Parameters.AddWithValue("@Kolejnosc", lad.Kolejnosc);
                cmdInsert.Parameters.AddWithValue("@KodKlienta", (object)lad.KodKlienta ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@Palety", lad.Palety);
                cmdInsert.Parameters.AddWithValue("@Pojemniki", lad.PojemnikiE2);
                cmdInsert.Parameters.AddWithValue("@Uwagi", (object)lad.Uwagi ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@TrybE2", lad.TrybE2);
                await cmdInsert.ExecuteNonQueryAsync();
            }
        }

        private async Task DeleteLadunekFromDatabase(long ladunekId)
        {
            // Usuń z lokalnej listy
            _ladunki.RemoveAll(l => l.LadunekID == ladunekId);

            // Przenumeruj pozostałe
            for (int i = 0; i < _ladunki.Count; i++)
            {
                _ladunki[i].Kolejnosc = i + 1;
            }

            // Usuń wszystkie i dodaj ponownie
            await using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync();

            var sqlDelete = "DELETE FROM dbo.Ladunek WHERE KursID = @KursID";
            using var cmdDelete = new SqlCommand(sqlDelete, cn);
            cmdDelete.Parameters.AddWithValue("@KursID", _kursId.Value);
            await cmdDelete.ExecuteNonQueryAsync();

            // Dodaj wszystkie z powrotem
            foreach (var lad in _ladunki.OrderBy(l => l.Kolejnosc))
            {
                var sqlInsert = @"INSERT INTO dbo.Ladunek 
                    (KursID, Kolejnosc, KodKlienta, PaletyH1, PojemnikiE2, Uwagi, TrybE2) 
                    VALUES (@KursID, @Kolejnosc, @KodKlienta, @Palety, @Pojemniki, @Uwagi, @TrybE2)";

                using var cmdInsert = new SqlCommand(sqlInsert, cn);
                cmdInsert.Parameters.AddWithValue("@KursID", lad.KursID);
                cmdInsert.Parameters.AddWithValue("@Kolejnosc", lad.Kolejnosc);
                cmdInsert.Parameters.AddWithValue("@KodKlienta", (object)lad.KodKlienta ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@Palety", lad.Palety);
                cmdInsert.Parameters.AddWithValue("@Pojemniki", lad.PojemnikiE2);
                cmdInsert.Parameters.AddWithValue("@Uwagi", (object)lad.Uwagi ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@TrybE2", lad.TrybE2);
                await cmdInsert.ExecuteNonQueryAsync();
            }
        }

        // Dodaj tę metodę pomocniczą do pobierania pojedynczego kursu
        private async Task<Kurs> PobierzKursAsync(long kursId)
        {
            var kursy = await _repozytorium.PobierzKursyPoDacieAsync(DateTime.Today.AddYears(-1));
            return kursy.FirstOrDefault(k => k.KursID == kursId);
        }

        private string _connectionString => "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
    }

    // Dialog do zmiany kolejności ładunków
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