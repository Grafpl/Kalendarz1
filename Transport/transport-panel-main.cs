// Plik: Transport/TransportMainFormImproved.cs
// Usprawniony główny panel zarządzania transportem - przejrzysty i prosty UI

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kalendarz1.Transport.Formularze;
using Kalendarz1.Transport.Pakowanie;
using Kalendarz1.Transport.Repozytorium;

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

        // Podsumowanie
        private Label lblSummaryKursy;
        private Label lblSummaryPojemniki;
        private Label lblSummaryWypelnienie;

        // Dane
        private List<Kurs> _kursy;
        private Dictionary<long, WynikPakowania> _wypelnienia;

        public TransportMainFormImproved(TransportRepozytorium repozytorium, string uzytkownik = null)
        {
            _repozytorium = repozytorium ?? throw new ArgumentNullException(nameof(repozytorium));
            _currentUser = uzytkownik ?? Environment.UserName;
            _selectedDate = DateTime.Today;

            InitializeComponent();
            _ = LoadInitialDataAsync();
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

            // Panel nawigacji dat (lewa strona)
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

            // Panel przycisków (prawa strona) - zwiększony rozmiar i margines
            var panelButtons = new FlowLayoutPanel
            {
                Location = new Point(panelHeader.Width - 620, 15),
                Size = new Size(600, 50),
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            btnNowyKurs = CreateActionButton("➕ NOWY KURS", Color.FromArgb(40, 167, 69), 150);
            btnNowyKurs.Click += BtnNowyKurs_Click;

            btnEdytuj = CreateActionButton("✏️ EDYTUJ", Color.FromArgb(255, 193, 7), 120);
            btnEdytuj.Click += BtnEdytujKurs_Click;

            btnKopiuj = CreateActionButton("📋 KOPIUJ", Color.FromArgb(108, 117, 125), 120);
            btnKopiuj.Click += BtnKopiujKurs_Click;

            btnUsun = CreateActionButton("🗑️ USUŃ", Color.FromArgb(220, 53, 69), 110);
            btnUsun.Click += BtnUsunKurs_Click;

            panelButtons.Controls.AddRange(new Control[] { btnUsun, btnKopiuj, btnEdytuj, btnNowyKurs });

            panelHeader.Controls.Add(panelDate);
            panelHeader.Controls.Add(panelButtons);

            // Dolna linia akcentowa
            var bottomLine = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 3,
                BackColor = Color.FromArgb(0, 123, 255)
            };
            panelHeader.Controls.Add(bottomLine);
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

            // Dodaj efekt cienia
            btn.Paint += (s, e) =>
            {
                var rect = new Rectangle(2, 2, btn.Width - 4, btn.Height - 4);
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    path.AddRectangle(rect);
                    using (var brush = new System.Drawing.Drawing2D.PathGradientBrush(path))
                    {
                        brush.CenterColor = btn.BackColor;
                        brush.SurroundColors = new[] { Color.FromArgb(50, 0, 0, 0) };
                    }
                }
            };

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

            // Górna linia akcentowa
            var topLine = new Panel
            {
                Dock = DockStyle.Top,
                Height = 3,
                BackColor = Color.FromArgb(0, 123, 255)
            };
            panelSummary.Controls.Add(topLine);

            var layoutSummary = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(10, 20, 10, 10)
            };

            // Kafelki podsumowania z nowymi kolorami
            var tile1 = CreateSummaryTile("KURSY DZISIAJ", "0", Color.FromArgb(0, 123, 255), "📊");
            var tile2 = CreateSummaryTile("POJEMNIKI", "0", Color.FromArgb(255, 193, 7), "📦");
            var tile3 = CreateSummaryTile("WYPEŁNIENIE", "0%", Color.FromArgb(40, 167, 69), "📈");

            lblSummaryKursy = tile1.Controls[2] as Label;
            lblSummaryPojemniki = tile2.Controls[2] as Label;
            lblSummaryWypelnienie = tile3.Controls[2] as Label;

            layoutSummary.Controls.Add(tile1);
            layoutSummary.Controls.Add(tile2);
            layoutSummary.Controls.Add(tile3);

            panelSummary.Controls.Add(layoutSummary);
        }

        private Panel CreateSummaryTile(string title, string value, Color color, string icon)
        {
            var tile = new Panel
            {
                Size = new Size(250, 70),
                BackColor = Color.FromArgb(52, 56, 64),
                Margin = new Padding(15, 5, 15, 5)
            };

            // Dodaj zaokrąglone rogi
            tile.Paint += (s, e) =>
            {
                var rect = tile.ClientRectangle;
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    int radius = 10;
                    path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                    path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
                    path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
                    path.CloseFigure();
                    tile.Region = new Region(path);
                }
            };

            var lblIcon = new Label
            {
                Text = icon,
                Location = new Point(15, 20),
                Size = new Size(30, 30),
                Font = new Font("Segoe UI", 16F),
                ForeColor = color
            };

            var lblTitle = new Label
            {
                Text = title,
                Location = new Point(50, 10),
                Size = new Size(180, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(173, 181, 189)
            };

            var lblValue = new Label
            {
                Text = value,
                Location = new Point(50, 32),
                Size = new Size(180, 30),
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = color
            };

            var sideBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(4, 70),
                BackColor = color
            };

            tile.Controls.Add(lblIcon);
            tile.Controls.Add(lblTitle);
            tile.Controls.Add(lblValue);
            tile.Controls.Add(sideBar);

            // Efekt hover
            tile.MouseEnter += (s, e) => tile.BackColor = Color.FromArgb(62, 66, 74);
            tile.MouseLeave += (s, e) => tile.BackColor = Color.FromArgb(52, 56, 64);

            return tile;
        }

        private async Task LoadInitialDataAsync()
        {
            try
            {
                await LoadKursyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void DtpData_ValueChanged(object sender, EventArgs e)
        {
            _selectedDate = dtpData.Value.Date;
            lblDayName.Text = _selectedDate.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));
            await LoadKursyAsync();
        }

        private void DgvKursy_SelectionChanged(object sender, EventArgs e)
        {
            bool hasSelection = dgvKursy.CurrentRow != null;
            btnEdytuj.Enabled = hasSelection;
            btnUsun.Enabled = hasSelection;
            btnKopiuj.Enabled = hasSelection;
        }

        private async Task LoadKursyAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                _kursy = await _repozytorium.PobierzKursyPoDacieAsync(_selectedDate);
                _wypelnienia = new Dictionary<long, WynikPakowania>();

                foreach (var kurs in _kursy)
                {
                    try
                    {
                        var wynik = await _repozytorium.ObliczPakowanieKursuAsync(kurs.KursID);
                        _wypelnienia[kurs.KursID] = wynik;
                    }
                    catch
                    {
                        _wypelnienia[kurs.KursID] = new WynikPakowania
                        {
                            SumaE2 = 0,
                            PaletyNominal = 0,
                            ProcNominal = 0
                        };
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

                foreach (var kurs in _kursy.OrderBy(k => k.GodzWyjazdu))
                {
                    var wyp = _wypelnienia.ContainsKey(kurs.KursID) ? _wypelnienia[kurs.KursID] : null;
                    dt.Rows.Add(
                        kurs.KursID,
                        kurs.GodzWyjazdu?.ToString(@"hh\:mm") ?? "--:--",
                        kurs.KierowcaNazwa ?? "",
                        kurs.PojazdRejestracja ?? "",
                        kurs.Trasa ?? "",
                        wyp?.SumaE2 ?? 0,
                        wyp?.ProcNominal ?? 0,
                        kurs.Status ?? "Planowany"
                    );
                }

                dgvKursy.DataSource = dt;

                // Konfiguracja kolumn
                if (dgvKursy.Columns["KursID"] != null)
                    dgvKursy.Columns["KursID"].Visible = false;

                if (dgvKursy.Columns["Godzina"] != null)
                {
                    dgvKursy.Columns["Godzina"].Width = 80;
                    dgvKursy.Columns["Godzina"].HeaderText = "Wyjazd";
                }

                if (dgvKursy.Columns["Wypełnienie"] != null)
                {
                    dgvKursy.Columns["Wypełnienie"].Width = 120;
                    dgvKursy.Columns["Wypełnienie"].HeaderText = "Wypełnienie";
                }

                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania kursów: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void UpdateSummary()
        {
            if (_kursy == null)
            {
                lblSummaryKursy.Text = "0";
                lblSummaryPojemniki.Text = "0";
                lblSummaryWypelnienie.Text = "0%";
                return;
            }

            int liczbaKursow = _kursy.Count;
            int sumaPojemnikow = _wypelnienia?.Sum(w => w.Value.SumaE2) ?? 0;
            decimal srednieWypelnienie = _wypelnienia?.Count > 0
                ? _wypelnienia.Average(w => w.Value.ProcNominal)
                : 0;

            lblSummaryKursy.Text = liczbaKursow.ToString();
            lblSummaryPojemniki.Text = sumaPojemnikow.ToString();
            lblSummaryWypelnienie.Text = $"{srednieWypelnienie:F0}%";

            // Kolorowanie według wypełnienia
            if (srednieWypelnienie > 90)
                lblSummaryWypelnienie.ForeColor = Color.FromArgb(231, 76, 60);
            else if (srednieWypelnienie > 75)
                lblSummaryWypelnienie.ForeColor = Color.FromArgb(243, 156, 18);
            else
                lblSummaryWypelnienie.ForeColor = Color.FromArgb(46, 204, 113);
        }

        private void DgvKursy_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dgvKursy.Rows[e.RowIndex];

            // Formatowanie kolumny wypełnienie
            if (dgvKursy.Columns[e.ColumnIndex].Name == "Wypełnienie")
            {
                if (e.Value != null && decimal.TryParse(e.Value.ToString(), out var wypelnienie))
                {
                    e.Value = $"{wypelnienie:F0}%";

                    // Pasek wypełnienia
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

        #region Event Handlers

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
    }
}