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

        // Filtry
        private Panel panelFilters;
        private ComboBox cboFiltrKierowca;
        private ComboBox cboFiltrPojazd;
        private ComboBox cboFiltrStatus;
        private Button btnWyczyscFiltry;
        private List<Kierowca> _wszyscyKierowcy;
        private List<Pojazd> _wszystkiePojazdy;

        // Menu kontekstowe
        private ContextMenuStrip contextMenuKurs;

        // Panel boczny - wolne zamówienia
        private Panel panelWolneZamowienia;
        private DataGridView dgvWolneZamowienia;
        private Label lblWolneZamowieniaInfo;
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // Podsumowanie
        private Label lblSummaryKursy;
        private Label lblSummaryPojemniki;
        private Label lblSummaryPalety;
        private Label lblSummaryWypelnienie;

        // Dane
        private List<Kurs> _kursy;
        private Dictionary<long, WynikPakowania> _wypelnienia;

        // Cache klientów dla szybszego ładowania
        private static Dictionary<int, string> _klienciCache = new Dictionary<int, string>();
        private static DateTime _klienciCacheTime = DateTime.MinValue;

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

            // ========== FILTERS ==========
            CreateFilters();

            // ========== CONTENT ==========
            CreateContent();

            // ========== SIDE PANEL - WOLNE ZAMÓWIENIA ==========
            CreateWolneZamowieniaPanel();

            // ========== CONTEXT MENU ==========
            CreateContextMenu();

            // ========== SUMMARY ==========
            CreateSummary();

            // Layout główny z dwoma kolumnami w środku
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));  // Filters - kompaktowe
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Content + Side panel
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));  // Summary - karty

            // Panel środkowy z dwoma kolumnami
            var contentWrapper = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            contentWrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));  // Kursy
            contentWrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));  // Wolne zamówienia

            contentWrapper.Controls.Add(panelContent, 0, 0);
            contentWrapper.Controls.Add(panelWolneZamowienia, 1, 0);

            mainLayout.Controls.Add(panelHeader, 0, 0);
            mainLayout.Controls.Add(panelFilters, 0, 1);
            mainLayout.Controls.Add(contentWrapper, 0, 2);
            mainLayout.Controls.Add(panelSummary, 0, 3);

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

        private void CreateFilters()
        {
            panelFilters = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20, 0, 20, 0)
            };

            // Dolna linia separatora
            var bottomLine = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Color.FromArgb(230, 232, 235)
            };

            var flowLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 8, 0, 8)
            };

            // Ikona filtra
            var lblIkona = new Label
            {
                Text = "⚙",
                Font = new Font("Segoe UI", 14F),
                ForeColor = Color.FromArgb(155, 89, 182),
                AutoSize = true,
                Margin = new Padding(0, 6, 8, 0)
            };

            // Filtr kierowcy - bez etykiety, z placeholder
            cboFiltrKierowca = new ComboBox
            {
                Width = 170,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 8, 12, 0),
                FlatStyle = FlatStyle.Flat
            };
            cboFiltrKierowca.SelectedIndexChanged += FiltrChanged;

            // Filtr pojazdu
            cboFiltrPojazd = new ComboBox
            {
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 8, 12, 0),
                FlatStyle = FlatStyle.Flat
            };
            cboFiltrPojazd.SelectedIndexChanged += FiltrChanged;

            // Filtr statusu
            cboFiltrStatus = new ComboBox
            {
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 8, 12, 0),
                FlatStyle = FlatStyle.Flat
            };
            cboFiltrStatus.Items.AddRange(new object[] {
                "Wszystkie statusy",
                "Planowany",
                "W realizacji",
                "Zakończony",
                "Anulowany"
            });
            cboFiltrStatus.SelectedIndex = 0;
            cboFiltrStatus.SelectedIndexChanged += FiltrChanged;

            // Przycisk wyczyść filtry - minimalistyczny
            btnWyczyscFiltry = new Button
            {
                Text = "Wyczyść",
                Size = new Size(70, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(240, 240, 245),
                ForeColor = Color.FromArgb(100, 100, 120),
                Font = new Font("Segoe UI", 8F),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 9, 0, 0)
            };
            btnWyczyscFiltry.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 210);
            btnWyczyscFiltry.FlatAppearance.BorderSize = 1;
            btnWyczyscFiltry.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 230, 235);
            btnWyczyscFiltry.Click += BtnWyczyscFiltry_Click;

            flowLayout.Controls.AddRange(new Control[] {
                lblIkona, cboFiltrKierowca, cboFiltrPojazd, cboFiltrStatus, btnWyczyscFiltry
            });

            panelFilters.Controls.Add(flowLayout);
            panelFilters.Controls.Add(bottomLine);
        }

        private void CreateContextMenu()
        {
            contextMenuKurs = new ContextMenuStrip();
            contextMenuKurs.Font = new Font("Segoe UI", 10F);

            var menuPodglad = new ToolStripMenuItem("📦 Podgląd ładunków", null, MenuPodgladLadunkow_Click);
            var menuHistoria = new ToolStripMenuItem("📋 Historia zmian", null, MenuHistoriaZmian_Click);
            var menuSeparator = new ToolStripSeparator();
            var menuEdytuj = new ToolStripMenuItem("✏️ Edytuj kurs", null, (s, e) => BtnEdytujKurs_Click(s, e));
            var menuUsun = new ToolStripMenuItem("🗑️ Usuń kurs", null, (s, e) => BtnUsunKurs_Click(s, e));

            contextMenuKurs.Items.AddRange(new ToolStripItem[] {
                menuPodglad, menuHistoria, menuSeparator, menuEdytuj, menuUsun
            });
        }

        private void CreateContent()
        {
            panelContent = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 12, 10, 12),
                BackColor = Color.FromArgb(245, 247, 250)
            };

            // Kontener dla grida z ramką
            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(220, 222, 228),
                Padding = new Padding(1)
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            // Stylizacja nagłówków - bardziej subtelna
            dgvKursy.EnableHeadersVisualStyles = false;
            dgvKursy.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253);
            dgvKursy.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(80, 90, 110);
            dgvKursy.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgvKursy.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 8, 10, 8);
            dgvKursy.ColumnHeadersHeight = 40;
            dgvKursy.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            // Stylizacja wierszy
            dgvKursy.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgvKursy.DefaultCellStyle.Padding = new Padding(10, 6, 10, 6);
            dgvKursy.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgvKursy.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvKursy.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 252, 254);
            dgvKursy.RowTemplate.Height = 42;
            dgvKursy.GridColor = Color.FromArgb(240, 242, 246);

            dgvKursy.CellFormatting += DgvKursy_CellFormatting;
            dgvKursy.CellDoubleClick += (s, e) => BtnEdytujKurs_Click(s, e);
            dgvKursy.SelectionChanged += DgvKursy_SelectionChanged;
            dgvKursy.MouseClick += DgvKursy_MouseClick;

            gridContainer.Controls.Add(dgvKursy);
            panelContent.Controls.Add(gridContainer);
        }

        private void DgvKursy_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hitTest = dgvKursy.HitTest(e.X, e.Y);
                if (hitTest.RowIndex >= 0)
                {
                    dgvKursy.ClearSelection();
                    dgvKursy.Rows[hitTest.RowIndex].Selected = true;
                    dgvKursy.CurrentCell = dgvKursy.Rows[hitTest.RowIndex].Cells[0];
                    contextMenuKurs.Show(dgvKursy, e.Location);
                }
            }
        }

        private void CreateWolneZamowieniaPanel()
        {
            panelWolneZamowienia = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(0)
            };

            // Lewa krawędź jako separator wizualny
            var leftBorder = new Panel
            {
                Dock = DockStyle.Left,
                Width = 3,
                BackColor = Color.FromArgb(155, 89, 182)
            };

            // Główny kontener z marginesem
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(250, 251, 253),
                Padding = new Padding(12, 8, 12, 8)
            };

            // Nagłówek - kompaktowy
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(155, 89, 182),
                Padding = new Padding(12, 0, 12, 0)
            };

            // Tytuł i info w jednej linii
            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var lblTytul = new Label
            {
                Text = "WOLNE ZAMÓWIENIA",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 12, 8, 0)
            };

            lblWolneZamowieniaInfo = new Label
            {
                Text = "0 zamówień",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(220, 220, 255),
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 14, 0, 0)
            };

            headerLayout.Controls.Add(lblTytul, 0, 0);
            headerLayout.Controls.Add(lblWolneZamowieniaInfo, 1, 0);
            panelHeader.Controls.Add(headerLayout);

            // Grid zamówień
            dgvWolneZamowienia = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            dgvWolneZamowienia.EnableHeadersVisualStyles = false;
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250);
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(100, 100, 120);
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            dgvWolneZamowienia.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 6, 4, 6);
            dgvWolneZamowienia.ColumnHeadersHeight = 28;
            dgvWolneZamowienia.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgvWolneZamowienia.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F);
            dgvWolneZamowienia.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
            dgvWolneZamowienia.DefaultCellStyle.SelectionBackColor = Color.FromArgb(155, 89, 182);
            dgvWolneZamowienia.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvWolneZamowienia.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 252);
            dgvWolneZamowienia.RowTemplate.Height = 26;
            dgvWolneZamowienia.GridColor = Color.FromArgb(240, 242, 245);

            // Kontener dla grida z ramką
            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(230, 232, 235),
                Padding = new Padding(1)
            };
            gridContainer.Controls.Add(dgvWolneZamowienia);

            mainContainer.Controls.Add(gridContainer);
            mainContainer.Controls.Add(panelHeader);

            panelWolneZamowienia.Controls.Add(mainContainer);
            panelWolneZamowienia.Controls.Add(leftBorder);
        }

        private void CreateSummary()
        {
            panelSummary = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(38, 42, 50),
                Padding = new Padding(20, 10, 20, 10)
            };

            // Górna linia dekoracyjna
            var topLine = new Panel
            {
                Dock = DockStyle.Top,
                Height = 2,
                BackColor = Color.FromArgb(60, 65, 75)
            };

            // Główny layout z kartami
            var mainLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 8, 0, 0)
            };

            // Karta 1 - Kursy
            var cardKursy = CreateSummaryCard("KURSY", Color.FromArgb(52, 152, 219), out lblSummaryKursy);

            // Karta 2 - Pojemniki E2
            var cardPojemniki = CreateSummaryCard("POJEMNIKI E2", Color.FromArgb(241, 196, 15), out lblSummaryPojemniki);

            // Karta 3 - Palety
            var cardPalety = CreateSummaryCard("PALETY", Color.FromArgb(155, 89, 182), out lblSummaryPalety);

            // Karta 4 - Wypełnienie
            var cardWypelnienie = CreateSummaryCard("WYPEŁNIENIE", Color.FromArgb(46, 204, 113), out lblSummaryWypelnienie);
            lblSummaryWypelnienie.Text = "0%";

            mainLayout.Controls.AddRange(new Control[] { cardKursy, cardPojemniki, cardPalety, cardWypelnienie });

            panelSummary.Controls.Add(mainLayout);
            panelSummary.Controls.Add(topLine);
        }

        private Panel CreateSummaryCard(string title, Color accentColor, out Label valueLabel)
        {
            var card = new Panel
            {
                Size = new Size(180, 70),
                BackColor = Color.FromArgb(48, 52, 60),
                Margin = new Padding(0, 0, 15, 0)
            };

            // Lewa linia akcentu
            var accent = new Panel
            {
                Dock = DockStyle.Left,
                Width = 4,
                BackColor = accentColor
            };

            // Tytuł
            var lblTitle = new Label
            {
                Text = title,
                Location = new Point(14, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 150, 165)
            };

            // Wartość
            valueLabel = new Label
            {
                Text = "0",
                Location = new Point(14, 30),
                AutoSize = true,
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = accentColor
            };

            card.Controls.Add(lblTitle);
            card.Controls.Add(valueLabel);
            card.Controls.Add(accent);

            return card;
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

                // Załaduj dane do filtrów
                await LoadFilterDataAsync();

                // Załaduj kursy - to automatycznie wywoła UpdateSummary
                await LoadKursyAsync();

                // Załaduj wolne zamówienia z dzisiejszego uboju
                await LoadWolneZamowieniaAsync();

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
            await LoadWolneZamowieniaAsync();

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
                dt.Columns.Add("KierowcaID", typeof(int)); // Ukryta - do filtrowania
                dt.Columns.Add("Pojazd", typeof(string));
                dt.Columns.Add("PojazdID", typeof(int)); // Ukryta - do filtrowania
                dt.Columns.Add("Trasa", typeof(string));
                dt.Columns.Add("Pojemniki", typeof(int));
                dt.Columns.Add("Wypełnienie", typeof(decimal));
                dt.Columns.Add("Status", typeof(string));
                dt.Columns.Add("Zmieniono", typeof(string)); // Historia zmian
                dt.Columns.Add("WymagaPrzydzialu", typeof(bool)); // Ukryta kolumna do formatowania

                // Pobierz aktywne filtry
                var filtrKierowcaId = (cboFiltrKierowca?.SelectedItem as Kierowca)?.KierowcaID;
                var filtrPojazdId = (cboFiltrPojazd?.SelectedItem as Pojazd)?.PojazdID;
                var filtrStatus = cboFiltrStatus?.SelectedItem?.ToString();
                if (filtrStatus == "Wszystkie statusy") filtrStatus = null;

                if (_kursy != null)
                {
                    foreach (var kurs in _kursy.OrderBy(k => k.GodzWyjazdu))
                    {
                        // Zastosuj filtry
                        if (filtrKierowcaId.HasValue && kurs.KierowcaID != filtrKierowcaId.Value)
                            continue;
                        if (filtrPojazdId.HasValue && kurs.PojazdID != filtrPojazdId.Value)
                            continue;
                        if (!string.IsNullOrEmpty(filtrStatus) && kurs.Status != filtrStatus)
                            continue;

                        var wyp = _wypelnienia.ContainsKey(kurs.KursID) ? _wypelnienia[kurs.KursID] : null;
                        var wymagaPrzydzialu = kurs.WymagaPrzydzialu;

                        // Wyświetl informację o braku przypisania
                        var kierowcaTekst = string.IsNullOrEmpty(kurs.KierowcaNazwa)
                            ? "⚠ DO PRZYDZIELENIA"
                            : kurs.KierowcaNazwa;

                        var pojazdTekst = string.IsNullOrEmpty(kurs.PojazdRejestracja)
                            ? "⚠ DO PRZYDZIELENIA"
                            : kurs.PojazdRejestracja;

                        // Status z bazy lub "Planowany" jako domyślny
                        var status = kurs.Status ?? "Planowany";

                        // Historia zmian - formatowanie
                        var zmieniono = "";
                        if (kurs.ZmienionoUTC.HasValue && !string.IsNullOrEmpty(kurs.Zmienil))
                        {
                            zmieniono = $"{kurs.Zmienil} ({kurs.ZmienionoUTC.Value.ToLocalTime():dd.MM HH:mm})";
                        }
                        else if (!string.IsNullOrEmpty(kurs.Utworzyl))
                        {
                            zmieniono = $"{kurs.Utworzyl} ({kurs.UtworzonoUTC.ToLocalTime():dd.MM HH:mm})";
                        }

                        dt.Rows.Add(
                            kurs.KursID,
                            kurs.GodzWyjazdu?.ToString(@"hh\:mm") ?? "--:--",
                            kierowcaTekst,
                            kurs.KierowcaID ?? 0,
                            pojazdTekst,
                            kurs.PojazdID ?? 0,
                            kurs.Trasa ?? "",
                            wyp?.SumaE2 ?? 0,
                            wyp?.ProcNominal ?? 0,
                            status,
                            zmieniono,
                            wymagaPrzydzialu
                        );
                    }
                }

                dgvKursy.DataSource = dt;

                // Konfiguracja kolumn
                if (dgvKursy.Columns["KursID"] != null)
                    dgvKursy.Columns["KursID"].Visible = false;

                // Ukryj kolumny pomocnicze
                if (dgvKursy.Columns["WymagaPrzydzialu"] != null)
                    dgvKursy.Columns["WymagaPrzydzialu"].Visible = false;
                if (dgvKursy.Columns["KierowcaID"] != null)
                    dgvKursy.Columns["KierowcaID"].Visible = false;
                if (dgvKursy.Columns["PojazdID"] != null)
                    dgvKursy.Columns["PojazdID"].Visible = false;

                if (dgvKursy.Columns["Godzina"] != null)
                {
                    dgvKursy.Columns["Godzina"].Width = 80;
                    dgvKursy.Columns["Godzina"].HeaderText = "Wyjazd";
                }

                if (dgvKursy.Columns["Kierowca"] != null)
                {
                    dgvKursy.Columns["Kierowca"].Width = 160;
                }

                if (dgvKursy.Columns["Pojazd"] != null)
                {
                    dgvKursy.Columns["Pojazd"].Width = 120;
                }

                if (dgvKursy.Columns["Wypełnienie"] != null)
                {
                    dgvKursy.Columns["Wypełnienie"].Width = 100;
                    dgvKursy.Columns["Wypełnienie"].HeaderText = "Wypełnienie";
                }

                if (dgvKursy.Columns["Zmieniono"] != null)
                {
                    dgvKursy.Columns["Zmieniono"].Width = 160;
                    dgvKursy.Columns["Zmieniono"].HeaderText = "Ostatnia zmiana";
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

        #region Filtry i menu kontekstowe

        private async Task LoadFilterDataAsync()
        {
            try
            {
                // Załaduj kierowców
                _wszyscyKierowcy = await _repozytorium.PobierzKierowcowAsync(true);
                cboFiltrKierowca.Items.Clear();
                cboFiltrKierowca.Items.Add("Wszyscy");
                foreach (var k in _wszyscyKierowcy)
                    cboFiltrKierowca.Items.Add(k);
                cboFiltrKierowca.SelectedIndex = 0;

                // Załaduj pojazdy
                _wszystkiePojazdy = await _repozytorium.PobierzPojazdyAsync(true);
                cboFiltrPojazd.Items.Clear();
                cboFiltrPojazd.Items.Add("Wszystkie");
                foreach (var p in _wszystkiePojazdy)
                    cboFiltrPojazd.Items.Add(p);
                cboFiltrPojazd.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading filter data: {ex.Message}");
            }
        }

        private async Task LoadWolneZamowieniaAsync()
        {
            try
            {
                // Zoptymalizowane zapytanie - filtrowanie w SQL
                var sql = @"
                    SELECT
                        zm.Id,
                        zm.KlientId,
                        zm.DataPrzyjazdu,
                        ISNULL(zm.LiczbaPalet, 0) AS Palety,
                        ISNULL(zm.LiczbaPojemnikow, 0) AS Pojemniki
                    FROM dbo.ZamowieniaMieso zm
                    WHERE zm.DataUboju = @Data
                      AND ISNULL(zm.Status, 'Nowe') <> 'Anulowane'
                      AND (ISNULL(zm.TransportStatus, 'Oczekuje') = 'Oczekuje'
                           OR zm.TransportStatus IS NULL OR zm.TransportStatus = '')
                      AND ISNULL(zm.TransportStatus, '') <> 'Własny'
                    ORDER BY zm.DataPrzyjazdu";

                var tempList = new List<(int Id, int KlientId, DateTime DataPrzyjazdu, decimal Palety, int Pojemniki)>();
                var klientIdsToFetch = new HashSet<int>();

                using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Data", _selectedDate.Date);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var klientId = reader.GetInt32(1);
                        tempList.Add((
                            reader.GetInt32(0),
                            klientId,
                            reader.IsDBNull(2) ? _selectedDate : reader.GetDateTime(2),
                            reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                            reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                        ));

                        // Sprawdź czy potrzebujemy pobrać nazwę klienta
                        if (!_klienciCache.ContainsKey(klientId))
                            klientIdsToFetch.Add(klientId);
                    }
                }

                // Pobierz tylko brakujących klientów (cache 30 min)
                if (klientIdsToFetch.Count > 0 || (DateTime.Now - _klienciCacheTime).TotalMinutes > 30)
                {
                    await LoadMissingKlienciAsync(klientIdsToFetch);
                }

                // Szybkie budowanie tabeli
                var dt = new DataTable();
                dt.Columns.Add("ID", typeof(int));
                dt.Columns.Add("Klient", typeof(string));
                dt.Columns.Add("Godz.", typeof(string));
                dt.Columns.Add("Palety", typeof(string));
                dt.Columns.Add("E2", typeof(int));

                foreach (var zam in tempList)
                {
                    var klientNazwa = _klienciCache.TryGetValue(zam.KlientId, out var nazwa)
                        ? nazwa
                        : $"KH {zam.KlientId}";
                    dt.Rows.Add(zam.Id, klientNazwa, zam.DataPrzyjazdu.ToString("HH:mm"),
                        zam.Palety.ToString("N1"), zam.Pojemniki);
                }

                dgvWolneZamowienia.DataSource = dt;

                // Konfiguracja kolumn
                if (dgvWolneZamowienia.Columns["ID"] != null)
                    dgvWolneZamowienia.Columns["ID"].Visible = false;
                if (dgvWolneZamowienia.Columns["Klient"] != null)
                    dgvWolneZamowienia.Columns["Klient"].FillWeight = 100;
                if (dgvWolneZamowienia.Columns["Godz."] != null)
                    dgvWolneZamowienia.Columns["Godz."].Width = 45;
                if (dgvWolneZamowienia.Columns["Palety"] != null)
                    dgvWolneZamowienia.Columns["Palety"].Width = 45;
                if (dgvWolneZamowienia.Columns["E2"] != null)
                    dgvWolneZamowienia.Columns["E2"].Width = 45;

                // Aktualizuj licznik
                lblWolneZamowieniaInfo.Text = tempList.Count.ToString();
                lblWolneZamowieniaInfo.ForeColor = tempList.Count == 0
                    ? Color.FromArgb(150, 255, 150)
                    : tempList.Count > 10
                        ? Color.FromArgb(255, 180, 120)
                        : Color.FromArgb(220, 220, 255);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading wolne zamowienia: {ex.Message}");
                lblWolneZamowieniaInfo.Text = "!";
                lblWolneZamowieniaInfo.ForeColor = Color.Red;
            }
        }

        private async Task LoadMissingKlienciAsync(HashSet<int> klientIds)
        {
            try
            {
                using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                // Pobierz wszystkich klientów na raz (szybsze niż pojedyncze zapytania)
                var sql = @"SELECT c.Id, ISNULL(c.Shortcut, 'KH ' + CAST(c.Id AS VARCHAR(10)))
                            FROM SSCommon.STContractors c";

                using var cmd = new SqlCommand(sql, cn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var nazwa = reader.GetString(1);
                    _klienciCache[id] = nazwa;
                }

                _klienciCacheTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading klienci cache: {ex.Message}");
            }
        }

        private async void FiltrChanged(object sender, EventArgs e)
        {
            await LoadKursyAsync();
        }

        private async void BtnWyczyscFiltry_Click(object sender, EventArgs e)
        {
            cboFiltrKierowca.SelectedIndex = 0;
            cboFiltrPojazd.SelectedIndex = 0;
            cboFiltrStatus.SelectedIndex = 0;
            await LoadKursyAsync();
        }

        private async void MenuPodgladLadunkow_Click(object sender, EventArgs e)
        {
            if (dgvKursy.CurrentRow == null) return;

            try
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                var kurs = _kursy.FirstOrDefault(k => k.KursID == kursId);
                if (kurs == null) return;

                var ladunki = await _repozytorium.PobierzLadunkiAsync(kursId);

                using var dlg = new PodgladLadunkowDialog(kurs, ladunki);
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas pobierania ładunków: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MenuHistoriaZmian_Click(object sender, EventArgs e)
        {
            if (dgvKursy.CurrentRow == null) return;

            try
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                var kurs = _kursy.FirstOrDefault(k => k.KursID == kursId);
                if (kurs == null) return;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"📋 HISTORIA ZMIAN KURSU #{kursId}");
                sb.AppendLine(new string('─', 40));
                sb.AppendLine();

                sb.AppendLine("📅 UTWORZONO:");
                sb.AppendLine($"   Data: {kurs.UtworzonoUTC.ToLocalTime():dd.MM.yyyy HH:mm:ss}");
                sb.AppendLine($"   Przez: {kurs.Utworzyl ?? "Nieznany"}");
                sb.AppendLine();

                if (kurs.ZmienionoUTC.HasValue)
                {
                    sb.AppendLine("✏️ OSTATNIA ZMIANA:");
                    sb.AppendLine($"   Data: {kurs.ZmienionoUTC.Value.ToLocalTime():dd.MM.yyyy HH:mm:ss}");
                    sb.AppendLine($"   Przez: {kurs.Zmienil ?? "Nieznany"}");
                }
                else
                {
                    sb.AppendLine("✏️ Kurs nie był jeszcze modyfikowany.");
                }

                sb.AppendLine();
                sb.AppendLine(new string('─', 40));
                sb.AppendLine($"Status: {kurs.Status}");
                sb.AppendLine($"Trasa: {kurs.Trasa ?? "Brak"}");
                sb.AppendLine($"Kierowca: {kurs.KierowcaNazwa ?? "Nieprzypisany"}");
                sb.AppendLine($"Pojazd: {kurs.PojazdRejestracja ?? "Nieprzypisany"}");

                MessageBox.Show(sb.ToString(), "Historia zmian kursu",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                _kurs.Status = "Planowany"; // Zachowaj status "Planowany"

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

    /// <summary>
    /// Dialog do szybkiego podglądu ładunków bez otwierania edytora
    /// </summary>
    public class PodgladLadunkowDialog : Form
    {
        private readonly Kurs _kurs;
        private readonly List<Ladunek> _ladunki;
        private DataGridView dgvLadunki;

        public PodgladLadunkowDialog(Kurs kurs, List<Ladunek> ladunki)
        {
            _kurs = kurs;
            _ladunki = ladunki;
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            Text = $"📦 Podgląd ładunków - Kurs #{_kurs.KursID}";
            Size = new Size(700, 450);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(240, 242, 247);

            // Panel nagłówka
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(41, 44, 51),
                Padding = new Padding(15)
            };

            var lblTytul = new Label
            {
                Text = $"🚚 {_kurs.KierowcaNazwa ?? "Brak kierowcy"} | {_kurs.PojazdRejestracja ?? "Brak pojazdu"}",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(15, 10)
            };

            var lblTrasa = new Label
            {
                Text = $"📍 {_kurs.Trasa ?? "Brak trasy"}",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(173, 181, 189),
                AutoSize = true,
                Location = new Point(15, 45)
            };

            panelHeader.Controls.AddRange(new Control[] { lblTytul, lblTrasa });

            // Grid ładunków
            dgvLadunki = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            dgvLadunki.EnableHeadersVisualStyles = false;
            dgvLadunki.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 252);
            dgvLadunki.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(52, 73, 94);
            dgvLadunki.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgvLadunki.ColumnHeadersHeight = 40;
            dgvLadunki.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgvLadunki.RowTemplate.Height = 35;
            dgvLadunki.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);

            // Panel podsumowania
            var panelFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(248, 249, 252),
                Padding = new Padding(15, 10, 15, 10)
            };

            var sumaE2 = _ladunki.Sum(l => l.PojemnikiE2);
            var lblSuma = new Label
            {
                Text = $"📊 Razem: {_ladunki.Count} pozycji | {sumaE2} pojemników E2",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Location = new Point(15, 15)
            };

            var btnZamknij = new Button
            {
                Text = "Zamknij",
                Size = new Size(100, 35),
                Location = new Point(550, 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnZamknij.FlatAppearance.BorderSize = 0;
            btnZamknij.Click += (s, e) => Close();

            panelFooter.Controls.AddRange(new Control[] { lblSuma, btnZamknij });

            Controls.Add(dgvLadunki);
            Controls.Add(panelHeader);
            Controls.Add(panelFooter);
        }

        private void LoadData()
        {
            var dt = new System.Data.DataTable();
            dt.Columns.Add("Lp", typeof(int));
            dt.Columns.Add("Klient", typeof(string));
            dt.Columns.Add("Pojemniki E2", typeof(int));
            dt.Columns.Add("Palety", typeof(string));
            dt.Columns.Add("Uwagi", typeof(string));

            int lp = 1;
            foreach (var ladunek in _ladunki.OrderBy(l => l.Kolejnosc))
            {
                var paletyTekst = ladunek.PaletyH1.HasValue ? ladunek.PaletyH1.Value.ToString() : "-";
                dt.Rows.Add(
                    lp++,
                    ladunek.KodKlienta ?? "-",
                    ladunek.PojemnikiE2,
                    paletyTekst,
                    ladunek.Uwagi ?? ""
                );
            }

            dgvLadunki.DataSource = dt;

            if (dgvLadunki.Columns["Lp"] != null)
                dgvLadunki.Columns["Lp"].Width = 50;
            if (dgvLadunki.Columns["Pojemniki E2"] != null)
                dgvLadunki.Columns["Pojemniki E2"].Width = 100;
            if (dgvLadunki.Columns["Palety"] != null)
                dgvLadunki.Columns["Palety"].Width = 80;
        }
    }
}