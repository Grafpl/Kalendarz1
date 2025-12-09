// Plik: Transport/TransportMainFormImproved.cs
// Naprawiony główny panel zarządzania transportem

using Kalendarz1.Transport.Formularze;
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
    public partial class TransportMainFormImproved : Form
    {
        private readonly TransportRepozytorium _repozytorium;
        private readonly string _currentUser;
        private DateTime _selectedDate;

        // Główne kontrolki
        private Panel panelHeader;
        private Panel panelContent;
        private Panel panelSummary;

        // Nawigacja
        private DateTimePicker dtpData;
        private Button btnPrevDay;
        private Button btnNextDay;
        private Button btnToday;
        private Label lblDayName;

        // Grid kursów
        private DataGridView dgvKursy;

        // Przyciski akcji
        private Button btnNowyKurs;
        private Button btnEdytuj;
        private Button btnUsun;
        private Button btnKopiuj;
        private Button btnMapa;
        private Button btnKierowcy;
        private Button btnPojazdy;
        private Button btnPrzydziel; // Szybkie przypisanie kierowcy/pojazdu

        // Podsumowanie
        private Label lblSummaryKursy;
        private Label lblSummaryPojemniki;
        private Label lblSummaryPalety;
        private Label lblSummaryWypelnienie;

        // Dane
        private List<Kurs> _kursy;
        private Dictionary<long, WynikPakowania> _wypelnienia;

        public TransportMainFormImproved(TransportRepozytorium repozytorium, string uzytkownik = null)
        {
            _repozytorium = repozytorium ?? throw new ArgumentNullException(nameof(repozytorium));
            _currentUser = uzytkownik ?? Environment.UserName;
            _selectedDate = DateTime.Today;

            // Inicjalizuj puste kolekcje
            _kursy = new List<Kurs>();
            _wypelnienia = new Dictionary<long, WynikPakowania>();

            InitializeComponent();

            this.Load += async (s, e) =>
            {
                // NIE wywołuj UpdateSummary tutaj - niech LoadKursyAsync to zrobi
                await LoadInitialDataAsync();
            };
        }

        private void InitializeComponent()
        {
            Text = "Transport - Panel Zarządzania";
            Size = new Size(1400, 800);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(240, 242, 247);
            WindowState = FormWindowState.Maximized;

            // ========== HEADER ==========
            CreateHeader();

            // ========== CONTENT ==========
            CreateContent();

            // ========== SUMMARY ==========
            CreateSummary();

            // Layout główny
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));

            mainLayout.Controls.Add(panelHeader, 0, 0);
            mainLayout.Controls.Add(panelContent, 0, 1);
            mainLayout.Controls.Add(panelSummary, 0, 2);

            Controls.Add(mainLayout);
        }

        private void CreateHeader()
        {
            panelHeader = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(41, 44, 51),
                Padding = new Padding(20, 15, 20, 15)
            };

            // Panel nawigacji dat (lewa strona) - bez zmian
            var panelDate = new Panel
            {
                Location = new Point(20, 15),
                Size = new Size(500, 50),
                BackColor = Color.Transparent
            };

            btnPrevDay = CreateNavButton("◀", 0, -1);

            var datePanel = new Panel
            {
                Location = new Point(50, 0),
                Size = new Size(150, 50),
                BackColor = Color.FromArgb(52, 56, 64),
                BorderStyle = BorderStyle.None
            };

            dtpData = new DateTimePicker
            {
                Location = new Point(5, 12),
                Size = new Size(140, 26),
                Format = DateTimePickerFormat.Short,
                Value = _selectedDate,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                CalendarMonthBackground = Color.FromArgb(52, 56, 64),
                CalendarForeColor = Color.White
            };
            dtpData.ValueChanged += DtpData_ValueChanged;
            datePanel.Controls.Add(dtpData);

            btnNextDay = CreateNavButton("▶", 210, 1);

            btnToday = new Button
            {
                Text = "DZIŚ",
                Location = new Point(260, 0),
                Size = new Size(80, 50),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnToday.FlatAppearance.BorderSize = 0;
            btnToday.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 105, 217);
            btnToday.Click += (s, e) => { _selectedDate = DateTime.Today; dtpData.Value = _selectedDate; };

            lblDayName = new Label
            {
                Location = new Point(355, 0),
                Size = new Size(140, 50),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 193, 7),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelDate.Controls.AddRange(new Control[] { btnPrevDay, datePanel, btnNextDay, btnToday, lblDayName });

            // Panel przycisków (prawa strona) - DODANY PRZYCISK RAPORT
            var panelButtons = new FlowLayoutPanel
            {
                Location = new Point(600, 15),
                Size = new Size(1300, 50), // Zwiększono szerokość dla nowego przycisku
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            btnNowyKurs = CreateActionButton("NOWY KURS", Color.FromArgb(40, 167, 69), 120);
            btnNowyKurs.Click += BtnNowyKurs_Click;

            btnEdytuj = CreateActionButton("EDYTUJ", Color.FromArgb(255, 193, 7), 100);
            btnEdytuj.Click += BtnEdytujKurs_Click;

            btnKopiuj = CreateActionButton("KOPIUJ", Color.FromArgb(108, 117, 125), 100);
            btnKopiuj.Click += BtnKopiujKurs_Click;

            btnUsun = CreateActionButton("USUŃ", Color.FromArgb(220, 53, 69), 80);
            btnUsun.Click += BtnUsunKurs_Click;

            // NOWY PRZYCISK RAPORT
            var btnRaport = CreateActionButton("RAPORT", Color.FromArgb(155, 89, 182), 100);
            btnRaport.Click += BtnRaport_Click;

            btnMapa = CreateActionButton("🗺️ MAPA", Color.FromArgb(156, 39, 176), 90);
            btnMapa.Click += BtnMapa_Click;

            btnKierowcy = CreateActionButton("KIEROWCY", Color.FromArgb(52, 73, 94), 100);
            btnKierowcy.Click += SafeBtnKierowcy_Click;

            btnPojazdy = CreateActionButton("POJAZDY", Color.FromArgb(52, 73, 94), 100);
            btnPojazdy.Click += SafeBtnPojazdy_Click;

            // Przycisk szybkiego przypisania kierowcy/pojazdu - pomarańczowy
            btnPrzydziel = CreateActionButton("⚡ PRZYDZIEL", Color.FromArgb(230, 126, 34), 120);
            btnPrzydziel.Click += BtnPrzydziel_Click;
            btnPrzydziel.Enabled = false; // Aktywowany gdy wybrany kurs wymaga przypisania

            // Dodanie wszystkich przycisków do panelu - WŁĄCZNIE Z RAPORTEM i PRZYDZIEL
            panelButtons.Controls.AddRange(new Control[] {
        btnUsun, btnKopiuj, btnMapa, btnRaport, btnKierowcy, btnPojazdy, btnPrzydziel, btnEdytuj, btnNowyKurs
    });

            panelHeader.Controls.Add(panelDate);
            panelHeader.Controls.Add(panelButtons);

            // Obsługa zmiany rozmiaru
            panelHeader.Resize += (s, e) =>
            {
                if (panelButtons != null)
                {
                    panelButtons.Location = new Point(panelHeader.Width - panelButtons.Width - 20, 15);
                }
            };
        }

        // NOWA METODA obsługi przycisku RAPORT
        private void BtnRaport_Click(object sender, EventArgs e)
        {
            // Raport transportowy
            var connString = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
            try
            {
                var raportForm = new Kalendarz1.Transport.TransportRaportForm(connString);
                raportForm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania raportu transportowego: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // BEZPIECZNE obsługa przycisków - z pełną obsługą błędów
        private void SafeBtnKierowcy_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Opening KierowcyForm...");

                // Sprawdź czy formularz można utworzyć
                using (var testForm = new Form())
                {
                    testForm.Dispose();
                }

                var frm = new KierowcyForm();
                frm.ShowDialog(this);

                System.Diagnostics.Debug.WriteLine("KierowcyForm closed successfully");
            }
            catch (System.IO.FileNotFoundException ex)
            {
                MessageBox.Show($"Nie można znaleźć wymaganych plików dla formularza kierowców.\nBłąd: {ex.Message}",
                    "Brak plików", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (System.TypeLoadException ex)
            {
                MessageBox.Show($"Błąd ładowania typu formularza kierowców.\nSprawdź czy wszystkie klasy są poprawnie zdefiniowane.\nBłąd: {ex.Message}",
                    "Błąd typu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (System.MissingMethodException ex)
            {
                MessageBox.Show($"Brak konstruktora dla formularza kierowców.\nBłąd: {ex.Message}",
                    "Błąd konstruktora", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SafeBtnKierowcy_Click: {ex}");
                MessageBox.Show($"Nie można otworzyć formularza kierowców.\n\nSzczegóły błędu:\n{ex.Message}\n\nTyp błędu: {ex.GetType().Name}",
                    "Błąd krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SafeBtnPojazdy_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Opening PojazdyForm...");

                // Sprawdź czy formularz można utworzyć
                using (var testForm = new Form())
                {
                    testForm.Dispose();
                }

                var frm = new PojazdyForm();
                frm.ShowDialog(this);

                System.Diagnostics.Debug.WriteLine("PojazdyForm closed successfully");
            }
            catch (System.IO.FileNotFoundException ex)
            {
                MessageBox.Show($"Nie można znaleźć wymaganych plików dla formularza pojazdów.\nBłąd: {ex.Message}",
                    "Brak plików", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (System.TypeLoadException ex)
            {
                MessageBox.Show($"Błąd ładowania typu formularza pojazdów.\nSprawdź czy wszystkie klasy są poprawnie zdefiniowane.\nBłąd: {ex.Message}",
                    "Błąd typu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (System.MissingMethodException ex)
            {
                MessageBox.Show($"Brak konstruktora dla formularza pojazdów.\nBłąd: {ex.Message}",
                    "Błąd konstruktora", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SafeBtnPojazdy_Click: {ex}");
                MessageBox.Show($"Nie można otworzyć formularza pojazdów.\n\nSzczegóły błędu:\n{ex.Message}\n\nTyp błędu: {ex.GetType().Name}",
                    "Błąd krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Button CreateNavButton(string text, int x, int dayChange)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, 0),
                Size = new Size(40, 50),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 56, 64),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(72, 76, 84);

            if (dayChange != 0)
            {
                btn.Click += (s, e) => { _selectedDate = _selectedDate.AddDays(dayChange); dtpData.Value = _selectedDate; };
            }

            return btn;
        }

        private Button CreateActionButton(string text, Color color, int width)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(width, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = color,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(6, 4, 6, 4)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(color, 0.15f);

            return btn;
        }

        private void CreateContent()
        {
            panelContent = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 10, 20, 10)
            };

            dgvKursy = new DataGridView
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            // Stylizacja nagłówków
            dgvKursy.EnableHeadersVisualStyles = false;
            dgvKursy.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 252);
            dgvKursy.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(52, 73, 94);
            dgvKursy.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgvKursy.ColumnHeadersDefaultCellStyle.Padding = new Padding(8);
            dgvKursy.ColumnHeadersHeight = 45;
            dgvKursy.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

            // Stylizacja wierszy
            dgvKursy.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgvKursy.DefaultCellStyle.Padding = new Padding(8, 4, 8, 4);
            dgvKursy.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgvKursy.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvKursy.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);
            dgvKursy.RowTemplate.Height = 40;
            dgvKursy.GridColor = Color.FromArgb(236, 240, 241);

            dgvKursy.CellFormatting += DgvKursy_CellFormatting;
            dgvKursy.CellDoubleClick += (s, e) => BtnEdytujKurs_Click(s, e);
            dgvKursy.SelectionChanged += DgvKursy_SelectionChanged;

            panelContent.Controls.Add(dgvKursy);
        }

        private void CreateSummary()
        {
            panelSummary = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(33, 37, 43),
                Padding = new Padding(20, 15, 20, 15)
            };

            // PROSTY układ bez skomplikowanych tile'ów
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };

            // Ustawienia kolumn - równomierne
            for (int i = 0; i < 4; i++)
            {
                mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            }
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40)); // Tytuły
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60)); // Wartości

            // BEZPOŚREDNIE tworzenie labels
            var lblTytulKursy = new Label
            {
                Text = "KURSY DZISIAJ",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(173, 181, 189),
                TextAlign = ContentAlignment.BottomCenter,
                Dock = DockStyle.Fill
            };

            lblSummaryKursy = new Label
            {
                Text = "0",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 123, 255),
                TextAlign = ContentAlignment.TopCenter,
                Dock = DockStyle.Fill
            };

            var lblTytulPojemniki = new Label
            {
                Text = "POJEMNIKI",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(173, 181, 189),
                TextAlign = ContentAlignment.BottomCenter,
                Dock = DockStyle.Fill
            };

            lblSummaryPojemniki = new Label
            {
                Text = "0",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 193, 7),
                TextAlign = ContentAlignment.TopCenter,
                Dock = DockStyle.Fill
            };

            var lblTytulPalety = new Label
            {
                Text = "PALETY",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(173, 181, 189),
                TextAlign = ContentAlignment.BottomCenter,
                Dock = DockStyle.Fill
            };

            lblSummaryPalety = new Label
            {
                Text = "0",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = Color.FromArgb(156, 39, 176),
                TextAlign = ContentAlignment.TopCenter,
                Dock = DockStyle.Fill
            };

            var lblTytulWypelnienie = new Label
            {
                Text = "ŚREDNIE WYPEŁNIENIE",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(173, 181, 189),
                TextAlign = ContentAlignment.BottomCenter,
                Dock = DockStyle.Fill
            };

            lblSummaryWypelnienie = new Label
            {
                Text = "0%",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 167, 69),
                TextAlign = ContentAlignment.TopCenter,
                Dock = DockStyle.Fill
            };

            // Dodaj do layoutu
            mainLayout.Controls.Add(lblTytulKursy, 0, 0);
            mainLayout.Controls.Add(lblSummaryKursy, 0, 1);
            mainLayout.Controls.Add(lblTytulPojemniki, 1, 0);
            mainLayout.Controls.Add(lblSummaryPojemniki, 1, 1);
            mainLayout.Controls.Add(lblTytulPalety, 2, 0);
            mainLayout.Controls.Add(lblSummaryPalety, 2, 1);
            mainLayout.Controls.Add(lblTytulWypelnienie, 3, 0);
            mainLayout.Controls.Add(lblSummaryWypelnienie, 3, 1);

            panelSummary.Controls.Add(mainLayout);

            // Test czy labels są przypisane
            System.Diagnostics.Debug.WriteLine($"=== CreateSummary: Labels created ===");
            System.Diagnostics.Debug.WriteLine($"lblSummaryKursy: {lblSummaryKursy != null}");
            System.Diagnostics.Debug.WriteLine($"lblSummaryPojemniki: {lblSummaryPojemniki != null}");
            System.Diagnostics.Debug.WriteLine($"lblSummaryPalety: {lblSummaryPalety != null}");
            System.Diagnostics.Debug.WriteLine($"lblSummaryWypelnienie: {lblSummaryWypelnienie != null}");

            // TESTUJ funkcję aktualizacji z przykładowymi danymi
            if (lblSummaryKursy != null)
            {
                System.Diagnostics.Debug.WriteLine("Testing summary update with sample data...");
                lblSummaryKursy.Text = "Test";
                lblSummaryPojemniki.Text = "123";
                lblSummaryPalety.Text = "456";
                lblSummaryWypelnienie.Text = "78%";

                // Po krótkiej chwili przywróć wartości domyślne
                var timer = new System.Windows.Forms.Timer();
                timer.Interval = 1000; // 1 sekunda
                timer.Tick += (s, e) =>
                {
                    lblSummaryKursy.Text = "0";
                    lblSummaryPojemniki.Text = "0";
                    lblSummaryPalety.Text = "0";
                    lblSummaryWypelnienie.Text = "0%";
                    timer.Stop();
                    timer.Dispose();
                    System.Diagnostics.Debug.WriteLine("Sample data cleared, ready for real data");
                };
                timer.Start();
            }
        }

        private Panel CreateSummaryTile(string title, string value, Color color)
        {
            var tile = new Panel
            {
                Size = new Size(220, 70),
                BackColor = Color.FromArgb(52, 56, 64),
                Margin = new Padding(10, 5, 10, 5)
            };

            var lblTitle = new Label
            {
                Text = title,
                Location = new Point(15, 10),
                Size = new Size(190, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(173, 181, 189),
                Name = "lblTitle"
            };

            var lblValue = new Label
            {
                Text = value,
                Location = new Point(15, 32),
                Size = new Size(190, 30),
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = color,
                Name = "lblValue" // Dodajemy nazwę dla łatwiejszego znalezienia
            };

            var sideBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(4, 70),
                BackColor = color,
                Name = "sideBar"
            };

            tile.Controls.Add(lblTitle);    // Indeks [0]
            tile.Controls.Add(lblValue);    // Indeks [1] 
            tile.Controls.Add(sideBar);     // Indeks [2]

            return tile;
        }

        private async Task LoadInitialDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== LoadInitialDataAsync START ===");

                // Ustaw nazwę dnia tygodnia
                lblDayName.Text = _selectedDate.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));

                // Załaduj kursy - to automatycznie wywoła UpdateSummary
                await LoadKursyAsync();

                System.Diagnostics.Debug.WriteLine("=== LoadInitialDataAsync END ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadInitialDataAsync: {ex.Message}");
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void DtpData_ValueChanged(object sender, EventArgs e)
        {
            _selectedDate = dtpData.Value.Date;
            lblDayName.Text = _selectedDate.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));
            System.Diagnostics.Debug.WriteLine($"Date changed to: {_selectedDate:yyyy-MM-dd}");

            await LoadKursyAsync();

            // Wymuś wywołanie UpdateSummary po zmianie daty
            System.Diagnostics.Debug.WriteLine("Force calling UpdateSummary after date change");
            UpdateSummary();
        }

        private void DgvKursy_SelectionChanged(object sender, EventArgs e)
        {
            bool hasSelection = dgvKursy.CurrentRow != null;
            btnEdytuj.Enabled = hasSelection;
            btnUsun.Enabled = hasSelection;
            btnKopiuj.Enabled = hasSelection;
            btnMapa.Enabled = hasSelection;

            // Aktywuj przycisk PRZYDZIEL tylko dla kursów wymagających przypisania
            if (hasSelection && dgvKursy.CurrentRow.Cells["WymagaPrzydzialu"]?.Value != null)
            {
                btnPrzydziel.Enabled = Convert.ToBoolean(dgvKursy.CurrentRow.Cells["WymagaPrzydzialu"].Value);
            }
            else
            {
                btnPrzydziel.Enabled = false;
            }
        }

        private async Task LoadKursyAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                System.Diagnostics.Debug.WriteLine("=== LoadKursyAsync START ===");

                _kursy = await _repozytorium.PobierzKursyPoDacieAsync(_selectedDate);
                _wypelnienia = new Dictionary<long, WynikPakowania>();

                System.Diagnostics.Debug.WriteLine($"Loaded {_kursy?.Count ?? 0} courses");

                if (_kursy != null)
                {
                    foreach (var kurs in _kursy)
                    {
                        try
                        {
                            var wynik = await _repozytorium.ObliczPakowanieKursuAsync(kurs.KursID);
                            _wypelnienia[kurs.KursID] = wynik;
                            System.Diagnostics.Debug.WriteLine($"Course {kurs.KursID}: SumaE2={wynik?.SumaE2}, PaletyNominal={wynik?.PaletyNominal}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error calculating packing for course {kurs.KursID}: {ex.Message}");
                            _wypelnienia[kurs.KursID] = new WynikPakowania
                            {
                                SumaE2 = 0,
                                PaletyNominal = 0,
                                ProcNominal = 0
                            };
                        }
                    }
                }

                var dt = new DataTable();
                dt.Columns.Add("KursID", typeof(long));
                dt.Columns.Add("Godzina", typeof(string));
                dt.Columns.Add("Kierowca", typeof(string));
                dt.Columns.Add("Pojazd", typeof(string));
                dt.Columns.Add("Trasa", typeof(string));
                dt.Columns.Add("Pojemniki", typeof(int));
                dt.Columns.Add("Wypełnienie", typeof(decimal));
                dt.Columns.Add("Status", typeof(string));
                dt.Columns.Add("WymagaPrzydzialu", typeof(bool)); // Ukryta kolumna do formatowania

                if (_kursy != null)
                {
                    foreach (var kurs in _kursy.OrderBy(k => k.GodzWyjazdu))
                    {
                        var wyp = _wypelnienia.ContainsKey(kurs.KursID) ? _wypelnienia[kurs.KursID] : null;
                        var wymagaPrzydzialu = kurs.WymagaPrzydzialu;

                        // Wyświetl informację o braku przypisania
                        var kierowcaTekst = string.IsNullOrEmpty(kurs.KierowcaNazwa)
                            ? "⚠ DO PRZYDZIELENIA"
                            : kurs.KierowcaNazwa;

                        var pojazdTekst = string.IsNullOrEmpty(kurs.PojazdRejestracja)
                            ? "⚠ DO PRZYDZIELENIA"
                            : kurs.PojazdRejestracja;

                        // Status automatycznie "Do przydzielenia" jeśli brak zasobów
                        var status = wymagaPrzydzialu ? "Do przydzielenia" : (kurs.Status ?? "Planowany");

                        dt.Rows.Add(
                            kurs.KursID,
                            kurs.GodzWyjazdu?.ToString(@"hh\:mm") ?? "--:--",
                            kierowcaTekst,
                            pojazdTekst,
                            kurs.Trasa ?? "",
                            wyp?.SumaE2 ?? 0,
                            wyp?.ProcNominal ?? 0,
                            status,
                            wymagaPrzydzialu
                        );
                    }
                }

                dgvKursy.DataSource = dt;

                // Konfiguracja kolumn
                if (dgvKursy.Columns["KursID"] != null)
                    dgvKursy.Columns["KursID"].Visible = false;

                // Ukryj kolumnę pomocniczą
                if (dgvKursy.Columns["WymagaPrzydzialu"] != null)
                    dgvKursy.Columns["WymagaPrzydzialu"].Visible = false;

                if (dgvKursy.Columns["Godzina"] != null)
                {
                    dgvKursy.Columns["Godzina"].Width = 80;
                    dgvKursy.Columns["Godzina"].HeaderText = "Wyjazd";
                }

                if (dgvKursy.Columns["Kierowca"] != null)
                {
                    dgvKursy.Columns["Kierowca"].Width = 180;
                }

                if (dgvKursy.Columns["Pojazd"] != null)
                {
                    dgvKursy.Columns["Pojazd"].Width = 150;
                }

                if (dgvKursy.Columns["Wypełnienie"] != null)
                {
                    dgvKursy.Columns["Wypełnienie"].Width = 120;
                    dgvKursy.Columns["Wypełnienie"].HeaderText = "Wypełnienie";
                }
                System.Diagnostics.Debug.WriteLine("Calling UpdateSummary...");
                UpdateSummary();
                System.Diagnostics.Debug.WriteLine("UpdateSummary completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadKursyAsync: {ex.Message}");
                MessageBox.Show($"Błąd podczas ładowania kursów: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateSummary();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void UpdateSummary()
        {
            // DEBUG: Sprawdź stan labels
            System.Diagnostics.Debug.WriteLine("=== UpdateSummary START ===");
            System.Diagnostics.Debug.WriteLine($"lblSummaryKursy is null: {lblSummaryKursy == null}");
            System.Diagnostics.Debug.WriteLine($"lblSummaryPojemniki is null: {lblSummaryPojemniki == null}");
            System.Diagnostics.Debug.WriteLine($"lblSummaryPalety is null: {lblSummaryPalety == null}");
            System.Diagnostics.Debug.WriteLine($"lblSummaryWypelnienie is null: {lblSummaryWypelnienie == null}");

            // Sprawdź czy labels istnieją - jeśli nie, nie rób nic
            if (lblSummaryKursy == null || lblSummaryPojemniki == null ||
                lblSummaryPalety == null || lblSummaryWypelnienie == null)
            {
                System.Diagnostics.Debug.WriteLine("One or more labels are null, skipping UpdateSummary");

                // Spróbuj znaleźć labels ponownie
                TryFindLabelsInPanels();

                // Jeśli nadal null, wyjdź
                if (lblSummaryKursy == null || lblSummaryPojemniki == null ||
                    lblSummaryPalety == null || lblSummaryWypelnienie == null)
                {
                    System.Diagnostics.Debug.WriteLine("Still null after TryFindLabelsInPanels, exiting");
                    return;
                }
            }

            if (_kursy == null)
            {
                lblSummaryKursy.Text = "0";
                lblSummaryPojemniki.Text = "0";
                lblSummaryPalety.Text = "0";
                lblSummaryWypelnienie.Text = "0%";
                return;
            }

            int liczbaKursow = _kursy.Count;
            int sumaPojemnikow = 0;
            int sumaPalet = 0;
            decimal srednieWypelnienie = 0;

            // Bezpieczne obliczenia z sprawdzeniem null
            if (_wypelnienia != null && _wypelnienia.Any())
            {
                try
                {
                    sumaPojemnikow = _wypelnienia.Sum(w => w.Value?.SumaE2 ?? 0);
                    sumaPalet = _wypelnienia.Sum(w => w.Value?.PaletyNominal ?? 0);

                    var validEntries = _wypelnienia.Where(w => w.Value != null).ToList();
                    if (validEntries.Any())
                    {
                        srednieWypelnienie = validEntries.Average(w => w.Value.ProcNominal);
                    }
                }
                catch (Exception ex)
                {
                    // W przypadku błędu, użyj wartości domyślnych
                    System.Diagnostics.Debug.WriteLine($"Error in calculations: {ex.Message}");
                    sumaPojemnikow = 0;
                    sumaPalet = 0;
                    srednieWypelnienie = 0;
                }
            }

            try
            {
                lblSummaryKursy.Text = liczbaKursow.ToString();
                lblSummaryPojemniki.Text = sumaPojemnikow.ToString();
                lblSummaryPalety.Text = sumaPalet.ToString();
                lblSummaryWypelnienie.Text = $"{srednieWypelnienie:F0}%";

                // Kolorowanie według wypełnienia
                if (srednieWypelnienie > 90)
                    lblSummaryWypelnienie.ForeColor = Color.FromArgb(231, 76, 60);
                else if (srednieWypelnienie > 75)
                    lblSummaryWypelnienie.ForeColor = Color.FromArgb(243, 156, 18);
                else
                    lblSummaryWypelnienie.ForeColor = Color.FromArgb(46, 204, 113);

                System.Diagnostics.Debug.WriteLine("UpdateSummary completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting label values: {ex.Message}");
            }
        }

        private void TryFindLabelsInPanels()
        {
            try
            {
                if (panelSummary != null)
                {
                    // Szukaj wszystkich labels w panelSummary
                    var allLabels = FindAllLabelsRecursive(panelSummary).ToList();
                    System.Diagnostics.Debug.WriteLine($"Found {allLabels.Count} labels in panelSummary");

                    foreach (var label in allLabels)
                    {
                        System.Diagnostics.Debug.WriteLine($"Label text: '{label.Text}'");

                        // Przypisz na podstawie wartości tekstowych
                        if (label.Text == "0" && lblSummaryKursy == null &&
                            label.Font.Size >= 16) // Większa czcionka = label wartości
                        {
                            lblSummaryKursy = label;
                            System.Diagnostics.Debug.WriteLine("Assigned lblSummaryKursy");
                        }
                        else if (label.Text == "0" && lblSummaryPojemniki == null && lblSummaryKursy != null &&
                                 label.Font.Size >= 16)
                        {
                            lblSummaryPojemniki = label;
                            System.Diagnostics.Debug.WriteLine("Assigned lblSummaryPojemniki");
                        }
                        else if (label.Text == "0" && lblSummaryPalety == null && lblSummaryPojemniki != null &&
                                 label.Font.Size >= 16)
                        {
                            lblSummaryPalety = label;
                            System.Diagnostics.Debug.WriteLine("Assigned lblSummaryPalety");
                        }
                        else if (label.Text == "0%" && lblSummaryWypelnienie == null &&
                                 label.Font.Size >= 16)
                        {
                            lblSummaryWypelnienie = label;
                            System.Diagnostics.Debug.WriteLine("Assigned lblSummaryWypelnienie");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in TryFindLabelsInPanels: {ex.Message}");
            }
        }

        private IEnumerable<Label> FindAllLabelsRecursive(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is Label label)
                    yield return label;

                foreach (var childLabel in FindAllLabelsRecursive(control))
                    yield return childLabel;
            }
        }

        private void DgvKursy_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dgvKursy.Rows[e.RowIndex];

            // Sprawdź czy kurs wymaga przydzielenia zasobów
            bool wymagaPrzydzialu = false;
            if (row.Cells["WymagaPrzydzialu"]?.Value != null)
            {
                wymagaPrzydzialu = Convert.ToBoolean(row.Cells["WymagaPrzydzialu"].Value);
            }

            // Formatowanie wierszy wymagających przydzielenia - pomarańczowe tło
            if (wymagaPrzydzialu)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 225); // Jasno-żółte tło
            }

            // Formatowanie kolumny kierowca - pomarańczowy tekst gdy brak przypisania
            if (dgvKursy.Columns[e.ColumnIndex].Name == "Kierowca")
            {
                var kierowcaTekst = e.Value?.ToString() ?? "";
                if (kierowcaTekst.Contains("DO PRZYDZIELENIA"))
                {
                    e.CellStyle.ForeColor = Color.FromArgb(230, 126, 34); // Pomarańczowy
                    e.CellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                }
            }

            // Formatowanie kolumny pojazd - pomarańczowy tekst gdy brak przypisania
            if (dgvKursy.Columns[e.ColumnIndex].Name == "Pojazd")
            {
                var pojazdTekst = e.Value?.ToString() ?? "";
                if (pojazdTekst.Contains("DO PRZYDZIELENIA"))
                {
                    e.CellStyle.ForeColor = Color.FromArgb(230, 126, 34); // Pomarańczowy
                    e.CellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                }
            }

            // Formatowanie kolumny wypełnienie
            if (dgvKursy.Columns[e.ColumnIndex].Name == "Wypełnienie")
            {
                if (e.Value != null && decimal.TryParse(e.Value.ToString(), out var wypelnienie))
                {
                    e.Value = $"{wypelnienie:F0}%";

                    if (wypelnienie > 100)
                        e.CellStyle.ForeColor = Color.FromArgb(231, 76, 60);
                    else if (wypelnienie > 90)
                        e.CellStyle.ForeColor = Color.FromArgb(243, 156, 18);
                    else
                        e.CellStyle.ForeColor = Color.FromArgb(46, 204, 113);

                    e.CellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                }
            }

            // Formatowanie statusu
            if (dgvKursy.Columns[e.ColumnIndex].Name == "Status")
            {
                var status = e.Value?.ToString() ?? "";

                switch (status)
                {
                    case "Zakończony":
                        e.CellStyle.ForeColor = Color.FromArgb(46, 204, 113);
                        break;
                    case "W realizacji":
                        e.CellStyle.ForeColor = Color.FromArgb(52, 152, 219);
                        break;
                    case "Anulowany":
                        e.CellStyle.ForeColor = Color.FromArgb(231, 76, 60);
                        row.DefaultCellStyle.BackColor = Color.FromArgb(254, 245, 245);
                        break;
                    case "Do przydzielenia":
                        e.CellStyle.ForeColor = Color.FromArgb(230, 126, 34); // Pomarańczowy
                        e.CellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                        break;
                    default:
                        e.CellStyle.ForeColor = Color.FromArgb(127, 140, 141);
                        break;
                }
            }
        }

        // USUNIĘTE stare metody które nie działały:
        // - BtnKierowcy_Click
        // - BtnPojazdy_Click  
        // - CreateSummaryTile
        // - TryFindLabelsInPanels
        // - FindAllLabelsRecursive

        #region Event Handlers - Obsługa przycisków - NAPRAWIONE

        private void BtnNowyKurs_Click(object sender, EventArgs e)
        {
            try
            {
                using var dlg = new EdytorKursuWithPalety(_repozytorium, _selectedDate, _currentUser);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _ = LoadKursyAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas tworzenia nowego kursu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnEdytujKurs_Click(object sender, EventArgs e)
        {
            if (dgvKursy.CurrentRow == null)
            {
                MessageBox.Show("Proszę wybrać kurs do edycji.",
                    "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                var kurs = _kursy.FirstOrDefault(k => k.KursID == kursId);

                if (kurs == null) return;

                using var dlg = new EdytorKursuWithPalety(_repozytorium, kurs, _currentUser);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _ = LoadKursyAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas edycji kursu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Szybkie przypisanie kierowcy i pojazdu do kursu
        /// </summary>
        private async void BtnPrzydziel_Click(object sender, EventArgs e)
        {
            if (dgvKursy.CurrentRow == null) return;

            try
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                var kurs = _kursy.FirstOrDefault(k => k.KursID == kursId);

                if (kurs == null) return;

                // Otwórz dialog szybkiego przypisania
                using var dlg = new SzybkiePrzypisanieDialog(_repozytorium, kurs);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    await LoadKursyAsync();
                    MessageBox.Show("Zasoby zostały przydzielone do kursu.",
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas przypisywania zasobów: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnUsunKurs_Click(object sender, EventArgs e)
        {
            if (dgvKursy.CurrentRow == null) return;

            if (MessageBox.Show("Czy na pewno usunąć wybrany kurs wraz ze wszystkimi ładunkami?",
                "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                await _repozytorium.UsunKursAsync(kursId);
                await LoadKursyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas usuwania kursu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnKopiujKurs_Click(object sender, EventArgs e)
        {
            if (dgvKursy.CurrentRow == null) return;

            try
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                var kurs = _kursy.FirstOrDefault(k => k.KursID == kursId);
                if (kurs == null) return;

                var nowyKurs = new Kurs
                {
                    DataKursu = _selectedDate.AddDays(1),
                    KierowcaID = kurs.KierowcaID,
                    PojazdID = kurs.PojazdID,
                    Trasa = kurs.Trasa,
                    GodzWyjazdu = kurs.GodzWyjazdu,
                    GodzPowrotu = kurs.GodzPowrotu,
                    Status = "Planowany",
                    PlanE2NaPalete = kurs.PlanE2NaPalete
                };

                var nowyKursId = await _repozytorium.DodajKursAsync(nowyKurs, _currentUser);

                // Kopiuj ładunki
                var ladunki = await _repozytorium.PobierzLadunkiAsync(kursId);
                foreach (var ladunek in ladunki)
                {
                    var nowyLadunek = new Ladunek
                    {
                        KursID = nowyKursId,
                        Kolejnosc = ladunek.Kolejnosc,
                        KodKlienta = ladunek.KodKlienta,
                        PojemnikiE2 = ladunek.PojemnikiE2,
                        PaletyH1 = ladunek.PaletyH1,
                        Uwagi = ladunek.Uwagi
                    };
                    await _repozytorium.DodajLadunekAsync(nowyLadunek);
                }

                MessageBox.Show($"Kurs został skopiowany na {nowyKurs.DataKursu:yyyy-MM-dd}.",
                    "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);

                _selectedDate = nowyKurs.DataKursu;
                dtpData.Value = _selectedDate;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas kopiowania kursu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Obsługa Map Google

        private async void BtnMapa_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                // Connection strings
                var connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                var connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
                var connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

                // Open new Transport Map Window
                var mapWindow = new TransportMapWindow(connTransport, connHandel, connLibra, _selectedDate, _currentUser);
                mapWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania mapy transportu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private async Task OtworzMapeTrasy(long kursId)
        {
            try
            {
                var ladunki = await _repozytorium.PobierzLadunkiAsync(kursId);

                if (!ladunki.Any())
                {
                    MessageBox.Show("Kurs nie ma żadnych ładunków do wyświetlenia trasy.",
                        "Brak danych", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var adresy = new List<string>();
                var debugInfo = new List<string>();

                string bazaAdres = "Koziółki 40, 95-061 Dmosin, Polska";
                adresy.Add(bazaAdres);
                debugInfo.Add($"START: {bazaAdres}");

                int znalezioneAdresy = 0;
                foreach (var ladunek in ladunki.OrderBy(l => l.Kolejnosc))
                {
                    debugInfo.Add($"--- Ładunek {ladunek.Kolejnosc}: '{ladunek.KodKlienta}' ---");
                    debugInfo.Add($"  Uwagi: '{ladunek.Uwagi ?? "brak"}'");

                    string adres = "";

                    // Sprawdź czy to zamówienie
                    if (await CzyToZamowienie(ladunek.KodKlienta))
                    {
                        debugInfo.Add($"  ✓ To jest zamówienie");
                        adres = await PobierzAdresZZamowienia(ladunek.KodKlienta);
                        if (!string.IsNullOrEmpty(adres))
                        {
                            debugInfo.Add($"✓ ADRES Z ZAMÓWIENIA: {adres}");
                        }
                    }
                    else
                    {
                        // Szukaj po kodzie klienta
                        adres = await PobierzAdresPoNazwie(ladunek.KodKlienta);
                        if (!string.IsNullOrEmpty(adres))
                        {
                            debugInfo.Add($"✓ ADRES PO KODZIE KLIENTA: {adres}");
                        }
                        else if (!string.IsNullOrEmpty(ladunek.Uwagi))
                        {
                            // Szukaj po uwagach
                            adres = await PobierzAdresPoNazwie(ladunek.Uwagi);
                            if (!string.IsNullOrEmpty(adres))
                            {
                                debugInfo.Add($"✓ ADRES PO UWAGACH: {adres}");
                            }
                        }
                    }

                    // Dodaj do trasy jeśli znaleziono
                    if (!string.IsNullOrEmpty(adres) && adres.Trim().Length > 5)
                    {
                        if (!adres.ToLower().Contains("polska"))
                        {
                            adres += ", Polska";
                        }
                        adresy.Add(adres);
                        znalezioneAdresy++;
                        debugInfo.Add($"✓ DODANO DO TRASY: {adres}");
                    }
                    else
                    {
                        debugInfo.Add($"✗ BRAK ADRESU dla ładunku {ladunek.LadunekID}");
                    }
                }

                debugInfo.Add($"--- PODSUMOWANIE ---");
                debugInfo.Add($"Łącznie ładunków: {ladunki.Count}");
                debugInfo.Add($"Znalezionych adresów: {znalezioneAdresy}");
                debugInfo.Add($"Wszystkich punktów trasy: {adresy.Count}");

                // Pokaż debug w oknie
                var debugText = string.Join("\n", debugInfo);
                var debugForm = new Form
                {
                    Text = "Debug - Analiza trasy",
                    Size = new Size(1000, 700),
                    StartPosition = FormStartPosition.CenterParent
                };

                var textBox = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Both,
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 9),
                    Text = debugText,
                    ReadOnly = true
                };

                debugForm.Controls.Add(textBox);
                debugForm.ShowDialog(this);

                // Otwórz mapę jeśli znaleziono adresy
                if (znalezioneAdresy > 0)
                {
                    adresy.Add(bazaAdres); // Powrót do bazy
                    string googleMapsUrl = UtworzUrlGoogleMaps(adresy);

                    if (MessageBox.Show($"Znaleziono {znalezioneAdresy} adresów.\nOtworzyć Google Maps?",
                        "Potwierdź trasę", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = googleMapsUrl,
                                UseShellExecute = true
                            });
                        }
                        catch
                        {
                            try
                            {
                                Clipboard.SetText(googleMapsUrl);
                                MessageBox.Show($"URL skopiowany do schowka:\n{googleMapsUrl}",
                                    "URL skopiowany", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            catch
                            {
                                MessageBox.Show($"URL: {googleMapsUrl}",
                                    "Link do Google Maps", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<string> PobierzAdresZZamowienia(string kodKlienta)
        {
            try
            {
                var connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

                int zamId = 0;
                if (kodKlienta.StartsWith("ZAM_"))
                {
                    int.TryParse(kodKlienta.Substring(4), out zamId);
                }
                else
                {
                    int.TryParse(kodKlienta, out zamId);
                }

                if (zamId <= 0) return "";

                await using var cnLibra = new SqlConnection(connLibra);
                await cnLibra.OpenAsync();

                var sqlZam = "SELECT KlientId FROM dbo.ZamowieniaMieso WHERE Id = @ZamId";
                using var cmdZam = new SqlCommand(sqlZam, cnLibra);
                cmdZam.Parameters.AddWithValue("@ZamId", zamId);

                var klientIdObj = await cmdZam.ExecuteScalarAsync();
                if (klientIdObj == null) return "";

                int klientId = Convert.ToInt32(klientIdObj);
                return await PobierzAdresKlienta(klientId);
            }
            catch
            {
                return "";
            }
        }

        private async Task<string> PobierzAdresKlienta(int klientId)
        {
            try
            {
                var connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

                await using var cn = new SqlConnection(connHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT 
                        ISNULL(poa.Street, '') AS Ulica,
                        ISNULL(poa.Postcode, '') AS KodPocztowy,
                        ISNULL(poa.City, '') AS Miasto
                    FROM [HANDEL].[SSCommon].[STContractors] c
                    LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa 
                        ON poa.ContactGuid = c.ContactGuid 
                        AND poa.AddressName = N'adres domyślny'
                    WHERE c.Id = @KlientId";

                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", klientId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var ulica = reader.GetString(0).Trim();
                    var kodPocztowy = reader.GetString(1).Trim();
                    var miasto = reader.GetString(2).Trim();

                    var adresParts = new List<string>();
                    if (!string.IsNullOrEmpty(ulica)) adresParts.Add(ulica);
                    if (!string.IsNullOrEmpty(kodPocztowy) && !string.IsNullOrEmpty(miasto))
                        adresParts.Add($"{kodPocztowy} {miasto}");
                    else if (!string.IsNullOrEmpty(miasto))
                        adresParts.Add(miasto);

                    return adresParts.Any() ? string.Join(", ", adresParts) : "";
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        private async Task<string> PobierzAdresPoNazwie(string nazwaKlienta)
        {
            try
            {
                var connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

                var czystaNazwa = nazwaKlienta;
                var idx = czystaNazwa.IndexOf('(');
                if (idx > 0)
                {
                    czystaNazwa = czystaNazwa.Substring(0, idx).Trim();
                }

                await using var cn = new SqlConnection(connHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT TOP 1
                        ISNULL(poa.Street, '') AS Ulica,
                        ISNULL(poa.Postcode, '') AS KodPocztowy,
                        ISNULL(poa.City, '') AS Miasto
                    FROM [HANDEL].[SSCommon].[STContractors] c
                    LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa 
                        ON poa.ContactGuid = c.ContactGuid 
                        AND poa.AddressName = N'adres domyślny'
                    WHERE c.Shortcut = @Nazwa";

                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Nazwa", czystaNazwa);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var ulica = reader.GetString(0).Trim();
                    var kodPocztowy = reader.GetString(1).Trim();
                    var miasto = reader.GetString(2).Trim();

                    var adresParts = new List<string>();
                    if (!string.IsNullOrEmpty(ulica)) adresParts.Add(ulica);
                    if (!string.IsNullOrEmpty(kodPocztowy) && !string.IsNullOrEmpty(miasto))
                        adresParts.Add($"{kodPocztowy} {miasto}");
                    else if (!string.IsNullOrEmpty(miasto))
                        adresParts.Add(miasto);

                    return adresParts.Any() ? string.Join(", ", adresParts) : "";
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        private async Task<bool> CzyToZamowienie(string kodKlienta)
        {
            if (string.IsNullOrEmpty(kodKlienta)) return false;

            if (kodKlienta.StartsWith("ZAM_") && int.TryParse(kodKlienta.Substring(4), out _))
                return true;

            if (int.TryParse(kodKlienta, out int zamId))
            {
                try
                {
                    var connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                    await using var cn = new SqlConnection(connLibra);
                    await cn.OpenAsync();

                    var sql = "SELECT COUNT(*) FROM dbo.ZamowieniaMieso WHERE Id = @ZamId";
                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@ZamId", zamId);

                    var count = (int)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private string UtworzUrlGoogleMaps(List<string> adresy)
        {
            if (adresy.Count < 2) return "";

            var origin = Uri.EscapeDataString(adresy[0]);
            var destination = Uri.EscapeDataString(adresy[adresy.Count - 1]);

            var waypoints = "";
            if (adresy.Count > 2)
            {
                var waypointList = adresy.Skip(1).Take(adresy.Count - 2)
                    .Select(Uri.EscapeDataString);
                waypoints = "&waypoints=" + string.Join("|", waypointList);
            }

            return $"https://www.google.com/maps/dir/{origin}/{destination}?travelmode=driving{waypoints}";
        }

        #endregion
    }

    /// <summary>
    /// Dialog do szybkiego przypisania kierowcy i pojazdu do kursu
    /// </summary>
    public class SzybkiePrzypisanieDialog : Form
    {
        private readonly TransportRepozytorium _repozytorium;
        private readonly Kurs _kurs;
        private ComboBox cboKierowca;
        private ComboBox cboPojazd;
        private Label lblInfo;
        private Button btnZapisz;
        private Button btnAnuluj;

        public SzybkiePrzypisanieDialog(TransportRepozytorium repozytorium, Kurs kurs)
        {
            _repozytorium = repozytorium;
            _kurs = kurs;
            InitializeComponent();
            _ = LoadDataAsync();
        }

        private void InitializeComponent()
        {
            Text = "Szybkie przydzielenie zasobów";
            Size = new Size(450, 320);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(240, 242, 247);

            // Informacja o kursie
            lblInfo = new Label
            {
                Location = new Point(20, 20),
                Size = new Size(400, 60),
                Text = $"Przydziel kierowcę i pojazd do kursu:\n" +
                       $"Data: {_kurs.DataKursu:dd.MM.yyyy}\n" +
                       $"Trasa: {_kurs.Trasa ?? "Brak trasy"}",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(52, 73, 94)
            };

            // Kierowca
            var lblKierowca = new Label
            {
                Location = new Point(20, 95),
                Size = new Size(80, 25),
                Text = "Kierowca:",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                TextAlign = ContentAlignment.MiddleRight
            };

            cboKierowca = new ComboBox
            {
                Location = new Point(110, 95),
                Size = new Size(300, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                DisplayMember = "PelneNazwisko"
            };

            // Pojazd
            var lblPojazd = new Label
            {
                Location = new Point(20, 140),
                Size = new Size(80, 25),
                Text = "Pojazd:",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                TextAlign = ContentAlignment.MiddleRight
            };

            cboPojazd = new ComboBox
            {
                Location = new Point(110, 140),
                Size = new Size(300, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                DisplayMember = "Opis"
            };

            // Ostrzeżenie
            var lblOstrzezenie = new Label
            {
                Location = new Point(20, 185),
                Size = new Size(400, 40),
                Text = "⚠ Wybierz kierowcę i pojazd, aby zakończyć przydzielanie.",
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(230, 126, 34)
            };

            // Przyciski
            btnZapisz = new Button
            {
                Location = new Point(220, 235),
                Size = new Size(100, 35),
                Text = "Przydziel",
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.Click += BtnZapisz_Click;

            btnAnuluj = new Button
            {
                Location = new Point(330, 235),
                Size = new Size(80, 35),
                Text = "Anuluj",
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;

            Controls.AddRange(new Control[] {
                lblInfo, lblKierowca, cboKierowca, lblPojazd, cboPojazd,
                lblOstrzezenie, btnZapisz, btnAnuluj
            });

            AcceptButton = btnZapisz;
            CancelButton = btnAnuluj;
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var kierowcy = await _repozytorium.PobierzKierowcowAsync(true);
                var pojazdy = await _repozytorium.PobierzPojazdyAsync(true);

                cboKierowca.DataSource = kierowcy;
                cboPojazd.DataSource = pojazdy;

                // Jeśli kurs ma już przypisanego kierowcę lub pojazd, ustaw go
                if (_kurs.KierowcaID.HasValue)
                {
                    var kierowca = kierowcy.FirstOrDefault(k => k.KierowcaID == _kurs.KierowcaID.Value);
                    if (kierowca != null) cboKierowca.SelectedItem = kierowca;
                }

                if (_kurs.PojazdID.HasValue)
                {
                    var pojazd = pojazdy.FirstOrDefault(p => p.PojazdID == _kurs.PojazdID.Value);
                    if (pojazd != null) cboPojazd.SelectedItem = pojazd;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnZapisz_Click(object sender, EventArgs e)
        {
            var kierowca = cboKierowca.SelectedItem as Kierowca;
            var pojazd = cboPojazd.SelectedItem as Pojazd;

            if (kierowca == null || pojazd == null)
            {
                MessageBox.Show("Wybierz kierowcę i pojazd, aby przydzielić zasoby.",
                    "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;

                // Zaktualizuj kurs z nowymi wartościami
                _kurs.KierowcaID = kierowca.KierowcaID;
                _kurs.PojazdID = pojazd.PojazdID;
                _kurs.Status = "Planowany"; // Zmień status z "Do przydzielenia" na "Planowany"

                await _repozytorium.AktualizujNaglowekKursuAsync(_kurs, Environment.UserName);

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
    }
}