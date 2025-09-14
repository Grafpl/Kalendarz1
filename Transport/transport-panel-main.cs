// Plik: Transport/transport-panel-main.cs
// Główny panel zarządzania transportem

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
    public partial class TransportMainForm : Form
    {
        private readonly TransportRepozytorium _repozytorium;
        private readonly string _currentUser;
        private DateTime _selectedDate;

        // Kontrolki główne
        private TabControl tabControl;
        private TabPage tabKursy;
        private TabPage tabKierowcy;
        private TabPage tabPojazdy;
        private TabPage tabRaporty;

        // Panel nawigacji dat
        private DateTimePicker dtpData;
        private Button btnPrevDay;
        private Button btnNextDay;
        private Button btnToday;
        private Label lblStatusBar;

        // Tab Kursy
        private DataGridView dgvKursy;
        private Button btnNowyKurs;
        private Button btnEdytujKurs;
        private Button btnUsunKurs;
        private Button btnKopiujKurs;
        private Button btnDrukujKurs;
        private Button btnOdswiez;
        private Panel panelPodsumowanie;
        private Label lblPodsumowanie;

        // Tab Kierowcy
        private DataGridView dgvKierowcy;
        private Button btnNowyKierowca;
        private Button btnEdytujKierowca;
        private Button btnUsunKierowca;
        private CheckBox chkTylkoAktywniKierowcy;

        // Tab Pojazdy
        private DataGridView dgvPojazdy;
        private Button btnNowyPojazd;
        private Button btnEdytujPojazd;
        private Button btnUsunPojazd;
        private CheckBox chkTylkoAktywnePojazdy;

        // Dane
        private List<Kurs> _kursy;
        private List<Kierowca> _kierowcy;
        private List<Pojazd> _pojazdy;
        private Dictionary<long, WynikPakowania> _wypelnienia;

        public TransportMainForm(TransportRepozytorium repozytorium, string uzytkownik = null)
        {
            _repozytorium = repozytorium ?? throw new ArgumentNullException(nameof(repozytorium));
            _currentUser = uzytkownik ?? Environment.UserName;
            _selectedDate = DateTime.Today;

            InitializeComponent();
            ConfigureForm();
            _ = LoadInitialDataAsync();
        }

        private void InitializeComponent()
        {
            Text = "Zarządzanie transportem";
            Size = new Size(1400, 800);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);

            // Panel główny
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Nagłówek
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Zawartość
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));  // Status bar

            // ========== PANEL NAGŁÓWKA ==========
            var panelHeader = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 240, 240),
                Padding = new Padding(10, 5, 10, 5)
            };

            var layoutHeader = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            // Nawigacja dat
            btnPrevDay = new Button
            {
                Text = "◀",
                Width = 30,
                Height = 30,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };
            btnPrevDay.Click += (s, e) => { _selectedDate = _selectedDate.AddDays(-1); dtpData.Value = _selectedDate; };

            dtpData = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Width = 120,
                Height = 30,
                Value = _selectedDate
            };
            dtpData.ValueChanged += DtpData_ValueChanged;

            btnNextDay = new Button
            {
                Text = "▶",
                Width = 30,
                Height = 30,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };
            btnNextDay.Click += (s, e) => { _selectedDate = _selectedDate.AddDays(1); dtpData.Value = _selectedDate; };

            btnToday = new Button
            {
                Text = "Dziś",
                Width = 60,
                Height = 30
            };
            btnToday.Click += (s, e) => { _selectedDate = DateTime.Today; dtpData.Value = _selectedDate; };

            btnOdswiez = new Button
            {
                Text = "🔄 Odśwież",
                Width = 100,
                Height = 30,
                Margin = new Padding(20, 3, 3, 3)
            };
            btnOdswiez.Click += async (s, e) => await LoadKursyAsync();

            layoutHeader.Controls.Add(new Label { Text = "Data:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 7, 5, 0) });
            layoutHeader.Controls.Add(btnPrevDay);
            layoutHeader.Controls.Add(dtpData);
            layoutHeader.Controls.Add(btnNextDay);
            layoutHeader.Controls.Add(btnToday);
            layoutHeader.Controls.Add(btnOdswiez);

            panelHeader.Controls.Add(layoutHeader);
            mainLayout.Controls.Add(panelHeader, 0, 0);

            // ========== TAB CONTROL ==========
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };

            // Tab Kursy
            tabKursy = new TabPage("Kursy");
            CreateTabKursy();
            tabControl.TabPages.Add(tabKursy);

            // Tab Kierowcy
            tabKierowcy = new TabPage("Kierowcy");
            CreateTabKierowcy();
            tabControl.TabPages.Add(tabKierowcy);

            // Tab Pojazdy
            tabPojazdy = new TabPage("Pojazdy");
            CreateTabPojazdy();
            tabControl.TabPages.Add(tabPojazdy);

            // Tab Raporty
            tabRaporty = new TabPage("Raporty");
            CreateTabRaporty();
            tabControl.TabPages.Add(tabRaporty);

            mainLayout.Controls.Add(tabControl, 0, 1);

            // ========== STATUS BAR ==========
            lblStatusBar = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(220, 220, 220),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0),
                Text = "Gotowy"
            };
            mainLayout.Controls.Add(lblStatusBar, 0, 2);

            Controls.Add(mainLayout);
        }

        private void CreateTabKursy()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Przyciski
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // Podsumowanie

            // Panel przycisków
            var panelButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(5)
            };

            btnNowyKurs = new Button
            {
                Text = "➕ Nowy kurs",
                Width = 120,
                Height = 35,
                BackColor = Color.Green,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnNowyKurs.Click += BtnNowyKurs_Click;

            btnEdytujKurs = new Button
            {
                Text = "✏️ Edytuj",
                Width = 100,
                Height = 35
            };
            btnEdytujKurs.Click += BtnEdytujKurs_Click;

            btnUsunKurs = new Button
            {
                Text = "🗑️ Usuń",
                Width = 80,
                Height = 35
            };
            btnUsunKurs.Click += BtnUsunKurs_Click;

            btnKopiujKurs = new Button
            {
                Text = "📋 Kopiuj",
                Width = 90,
                Height = 35
            };
            btnKopiujKurs.Click += BtnKopiujKurs_Click;

            btnDrukujKurs = new Button
            {
                Text = "🖨️ Drukuj",
                Width = 90,
                Height = 35
            };
            btnDrukujKurs.Click += BtnDrukujKurs_Click;

            panelButtons.Controls.Add(btnNowyKurs);
            panelButtons.Controls.Add(btnEdytujKurs);
            panelButtons.Controls.Add(btnUsunKurs);
            panelButtons.Controls.Add(btnKopiujKurs);
            panelButtons.Controls.Add(btnDrukujKurs);

            layout.Controls.Add(panelButtons, 0, 0);

            // DataGridView
            dgvKursy = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                ReadOnly = true
            };
            dgvKursy.CellFormatting += DgvKursy_CellFormatting;
            dgvKursy.CellDoubleClick += (s, e) => BtnEdytujKurs_Click(s, e);

            layout.Controls.Add(dgvKursy, 0, 1);

            // Panel podsumowania
            panelPodsumowanie = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 245, 245),
                BorderStyle = BorderStyle.FixedSingle
            };

            lblPodsumowanie = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                Font = new Font("Segoe UI", 10F)
            };

            panelPodsumowanie.Controls.Add(lblPodsumowanie);
            layout.Controls.Add(panelPodsumowanie, 0, 2);

            tabKursy.Controls.Add(layout);
        }

        private void CreateTabKierowcy()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Panel przycisków
            var panelButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(5)
            };

            btnNowyKierowca = new Button
            {
                Text = "➕ Nowy kierowca",
                Width = 130,
                Height = 35
            };
            btnNowyKierowca.Click += BtnNowyKierowca_Click;

            btnEdytujKierowca = new Button
            {
                Text = "✏️ Edytuj",
                Width = 100,
                Height = 35
            };
            btnEdytujKierowca.Click += BtnEdytujKierowca_Click;

            btnUsunKierowca = new Button
            {
                Text = "🗑️ Usuń",
                Width = 80,
                Height = 35
            };
            btnUsunKierowca.Click += BtnUsunKierowca_Click;

            chkTylkoAktywniKierowcy = new CheckBox
            {
                Text = "Tylko aktywni",
                Checked = true,
                AutoSize = true,
                Padding = new Padding(20, 10, 0, 0)
            };
            chkTylkoAktywniKierowcy.CheckedChanged += async (s, e) => await LoadKierowcyAsync();

            panelButtons.Controls.Add(btnNowyKierowca);
            panelButtons.Controls.Add(btnEdytujKierowca);
            panelButtons.Controls.Add(btnUsunKierowca);
            panelButtons.Controls.Add(chkTylkoAktywniKierowcy);

            layout.Controls.Add(panelButtons, 0, 0);

            // DataGridView
            dgvKierowcy = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ReadOnly = true
            };

            layout.Controls.Add(dgvKierowcy, 0, 1);
            tabKierowcy.Controls.Add(layout);
        }

        private void CreateTabPojazdy()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Panel przycisków
            var panelButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(5)
            };

            btnNowyPojazd = new Button
            {
                Text = "➕ Nowy pojazd",
                Width = 120,
                Height = 35
            };
            btnNowyPojazd.Click += BtnNowyPojazd_Click;

            btnEdytujPojazd = new Button
            {
                Text = "✏️ Edytuj",
                Width = 100,
                Height = 35
            };
            btnEdytujPojazd.Click += BtnEdytujPojazd_Click;

            btnUsunPojazd = new Button
            {
                Text = "🗑️ Usuń",
                Width = 80,
                Height = 35
            };
            btnUsunPojazd.Click += BtnUsunPojazd_Click;

            chkTylkoAktywnePojazdy = new CheckBox
            {
                Text = "Tylko aktywne",
                Checked = true,
                AutoSize = true,
                Padding = new Padding(20, 10, 0, 0)
            };
            chkTylkoAktywnePojazdy.CheckedChanged += async (s, e) => await LoadPojazdyAsync();

            panelButtons.Controls.Add(btnNowyPojazd);
            panelButtons.Controls.Add(btnEdytujPojazd);
            panelButtons.Controls.Add(btnUsunPojazd);
            panelButtons.Controls.Add(chkTylkoAktywnePojazdy);

            layout.Controls.Add(panelButtons, 0, 0);

            // DataGridView
            dgvPojazdy = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ReadOnly = true
            };

            layout.Controls.Add(dgvPojazdy, 0, 1);
            tabPojazdy.Controls.Add(layout);
        }

        private void CreateTabRaporty()
        {
            var label = new Label
            {
                Text = "Raporty - w przygotowaniu",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14F)
            };
            tabRaporty.Controls.Add(label);
        }

        private void ConfigureForm()
        {
            // Ustawienia formularza
            WindowState = FormWindowState.Maximized;
        }

        private async Task LoadInitialDataAsync()
        {
            try
            {
                SetStatus("Ładowanie danych...");

                await LoadKursyAsync();
                await LoadKierowcyAsync();
                await LoadPojazdyAsync();

                SetStatus("Gotowy");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Błąd ładowania danych");
            }
        }

        private async void DtpData_ValueChanged(object sender, EventArgs e)
        {
            _selectedDate = dtpData.Value.Date;
            await LoadKursyAsync();
        }

        private async Task LoadKursyAsync()
        {
            try
            {
                SetStatus("Ładowanie kursów...");
                Cursor = Cursors.WaitCursor;

                // Pobierz kursy - bezpieczniejsze wywołanie
                _kursy = await _repozytorium.PobierzKursyPoDacieAsync(_selectedDate);
                _wypelnienia = new Dictionary<long, WynikPakowania>();

                // Oblicz wypełnienia dla każdego kursu
                foreach (var kurs in _kursy)
                {
                    try
                    {
                        var wynik = await _repozytorium.ObliczPakowanieKursuAsync(kurs.KursID);
                        _wypelnienia[kurs.KursID] = wynik;
                    }
                    catch
                    {
                        // Jeśli błąd obliczania - ustaw domyślne wartości
                        _wypelnienia[kurs.KursID] = new WynikPakowania
                        {
                            SumaE2 = 0,
                            PaletyNominal = 0,
                            PaletyMax = 0,
                            ProcNominal = 0,
                            ProcMax = 0
                        };
                    }
                }

                // Przygotuj dane dla DataGridView
                var dt = new DataTable();
                dt.Columns.Add("KursID", typeof(long));
                dt.Columns.Add("Godzina", typeof(string));
                dt.Columns.Add("Kierowca", typeof(string));
                dt.Columns.Add("Pojazd", typeof(string));
                dt.Columns.Add("Trasa", typeof(string));
                dt.Columns.Add("Wypełnienie", typeof(string));
                dt.Columns.Add("Status", typeof(string));

                foreach (var kurs in _kursy)
                {
                    try
                    {
                        var godz = kurs.GodzWyjazdu?.ToString(@"hh\:mm") ?? "--:--";
                        var wypelnienie = _wypelnienia.ContainsKey(kurs.KursID)
                            ? $"{_wypelnienia[kurs.KursID].ProcNominal:F0}%"
                            : "0%";

                        dt.Rows.Add(
                            kurs.KursID,
                            godz,
                            kurs.KierowcaNazwa ?? "",
                            kurs.PojazdRejestracja ?? "",
                            kurs.Trasa ?? "",
                            wypelnienie,
                            GetStatusDisplay(kurs.Status ?? "Planowany")
                        );
                    }
                    catch (Exception rowEx)
                    {
                        // Jeśli błąd w pojedynczym wierszu - pomiń go
                        System.Diagnostics.Debug.WriteLine($"Błąd dodawania wiersza dla kursu {kurs.KursID}: {rowEx.Message}");
                    }
                }

                dgvKursy.DataSource = dt;

                // Ukryj kolumnę ID
                if (dgvKursy.Columns["KursID"] != null)
                    dgvKursy.Columns["KursID"].Visible = false;

                UpdatePodsumowanie();
                SetStatus($"Załadowano {_kursy.Count} kursów");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania kursów:\n{ex.Message}\n\nTyp błędu: {ex.GetType().Name}\n\nStack trace:\n{ex.StackTrace}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Błąd ładowania kursów");

                // Ustaw puste dane
                _kursy = new List<Kurs>();
                _wypelnienia = new Dictionary<long, WynikPakowania>();
                dgvKursy.DataSource = null;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private string GetStatusDisplay(string status)
        {
            return status switch
            {
                "Planowany" => "📋 Planowany",
                "Potwierdzony" => "✅ Potwierdzony",
                "W realizacji" => "🚚 W realizacji",
                "Zakończony" => "✔️ Zakończony",
                "Anulowany" => "❌ Anulowany",
                _ => status
            };
        }

        private void UpdatePodsumowanie()
        {
            if (_kursy == null || !_kursy.Any())
            {
                lblPodsumowanie.Text = "Brak kursów na wybrany dzień";
                return;
            }

            int liczbKursow = _kursy.Count;
            int planowane = _kursy.Count(k => k.Status == "Planowany");
            int potwierdzone = _kursy.Count(k => k.Status == "Potwierdzony");
            int wRealizacji = _kursy.Count(k => k.Status == "W realizacji");
            int zakonczone = _kursy.Count(k => k.Status == "Zakończony");

            var sumaE2 = _wypelnienia?.Sum(w => w.Value.SumaE2) ?? 0;
            var sumaPalet = _wypelnienia?.Sum(w => w.Value.PaletyNominal) ?? 0;

            var text = $"Kursy: {liczbKursow} | ";
            text += $"Planowane: {planowane} | ";
            text += $"Potwierdzone: {potwierdzone} | ";
            text += $"W realizacji: {wRealizacji} | ";
            text += $"Zakończone: {zakonczone}\n";
            text += $"Łącznie: {sumaE2} pojemników E2 | {sumaPalet} palet";

            lblPodsumowanie.Text = text;
        }

        private void DgvKursy_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dgvKursy.Rows[e.RowIndex];

            // Kolorowanie według wypełnienia
            if (dgvKursy.Columns[e.ColumnIndex].Name == "Wypełnienie")
            {
                if (e.Value != null)
                {
                    var wypelnienieStr = e.Value.ToString().Replace("%", "");
                    if (decimal.TryParse(wypelnienieStr, out var wypelnienie))
                    {
                        if (wypelnienie > 100)
                            e.CellStyle.ForeColor = Color.Red;
                        else if (wypelnienie > 90)
                            e.CellStyle.ForeColor = Color.Orange;
                        else
                            e.CellStyle.ForeColor = Color.Green;
                    }
                }
            }

            // Kolorowanie według statusu
            if (dgvKursy.Columns[e.ColumnIndex].Name == "Status")
            {
                if (e.Value != null)
                {
                    var status = e.Value.ToString();
                    if (status.Contains("Anulowany"))
                    {
                        row.DefaultCellStyle.BackColor = Color.LightGray;
                        row.DefaultCellStyle.ForeColor = Color.DarkGray;
                    }
                    else if (status.Contains("Zakończony"))
                    {
                        row.DefaultCellStyle.BackColor = Color.LightGreen;
                    }
                    else if (status.Contains("W realizacji"))
                    {
                        row.DefaultCellStyle.BackColor = Color.LightBlue;
                    }
                }
            }
        }

        #region Obsługa kursów

        private void BtnNowyKurs_Click(object sender, EventArgs e)
        {
            try
            {
                // Użyj konstruktora dla nowego kursu z datą
                using var dlg = new EdytorKursu(_repozytorium, _selectedDate, _currentUser);

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

                if (kurs == null)
                {
                    MessageBox.Show("Nie znaleziono wybranego kursu.",
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Użyj konstruktora dla edycji istniejącego kursu
                using var dlg = new EdytorKursu(_repozytorium, kurs, _currentUser);

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
            if (dgvKursy.CurrentRow == null)
            {
                MessageBox.Show("Proszę wybrać kurs do usunięcia.",
                    "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show("Czy na pewno usunąć wybrany kurs wraz ze wszystkimi ładunkami?",
                "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);

                Cursor = Cursors.WaitCursor;
                await _repozytorium.UsunKursAsync(kursId);

                MessageBox.Show("Kurs został usunięty.",
                    "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadKursyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas usuwania kursu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private async void BtnKopiujKurs_Click(object sender, EventArgs e)
        {
            if (dgvKursy.CurrentRow == null)
            {
                MessageBox.Show("Proszę wybrać kurs do skopiowania.",
                    "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var kursId = Convert.ToInt64(dgvKursy.CurrentRow.Cells["KursID"].Value);
                var kurs = _kursy.FirstOrDefault(k => k.KursID == kursId);

                if (kurs == null) return;

                // Utwórz kopię kursu
                var nowyKurs = new Kurs
                {
                    DataKursu = _selectedDate.AddDays(1), // Na następny dzień
                    KierowcaID = kurs.KierowcaID,
                    PojazdID = kurs.PojazdID,
                    Trasa = kurs.Trasa,
                    GodzWyjazdu = kurs.GodzWyjazdu,
                    GodzPowrotu = kurs.GodzPowrotu,
                    Status = "Planowany",
                    PlanE2NaPalete = kurs.PlanE2NaPalete
                };

                Cursor = Cursors.WaitCursor;
                var nowyKursId = await _repozytorium.DodajKursAsync(nowyKurs, _currentUser);

                // Skopiuj ładunki
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
                        PlanE2NaPaleteOverride = ladunek.PlanE2NaPaleteOverride,
                        Uwagi = ladunek.Uwagi
                    };
                    await _repozytorium.DodajLadunekAsync(nowyLadunek);
                }

                MessageBox.Show($"Kurs został skopiowany na {nowyKurs.DataKursu:yyyy-MM-dd}.",
                    "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Przełącz na nową datę
                _selectedDate = nowyKurs.DataKursu;
                dtpData.Value = _selectedDate;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas kopiowania kursu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void BtnDrukujKurs_Click(object sender, EventArgs e)
        {
            if (dgvKursy.CurrentRow == null)
            {
                MessageBox.Show("Proszę wybrać kurs do wydruku.",
                    "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show("Funkcja drukowania jest w przygotowaniu.",
                "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion

        #region Obsługa kierowców

        private async Task LoadKierowcyAsync()
        {
            try
            {
                SetStatus("Ładowanie kierowców...");
                Cursor = Cursors.WaitCursor;

                _kierowcy = await _repozytorium.PobierzKierowcowAsync(chkTylkoAktywniKierowcy.Checked);

                var dt = new DataTable();
                dt.Columns.Add("KierowcaID", typeof(int));
                dt.Columns.Add("Imię", typeof(string));
                dt.Columns.Add("Nazwisko", typeof(string));
                dt.Columns.Add("Telefon", typeof(string));
                dt.Columns.Add("Aktywny", typeof(bool));

                foreach (var kierowca in _kierowcy)
                {
                    dt.Rows.Add(
                        kierowca.KierowcaID,
                        kierowca.Imie,
                        kierowca.Nazwisko,
                        kierowca.Telefon,
                        kierowca.Aktywny
                    );
                }

                dgvKierowcy.DataSource = dt;

                if (dgvKierowcy.Columns["KierowcaID"] != null)
                    dgvKierowcy.Columns["KierowcaID"].Visible = false;

                SetStatus($"Załadowano {_kierowcy.Count} kierowców");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania kierowców: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Błąd ładowania kierowców");
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private async void BtnNowyKierowca_Click(object sender, EventArgs e)
        {
            using var dlg = new KierowcaEditorForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    await _repozytorium.DodajKierowceAsync(dlg.Kierowca);
                    await LoadKierowcyAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas dodawania kierowcy: {ex.Message}",
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void BtnEdytujKierowca_Click(object sender, EventArgs e)
        {
            if (dgvKierowcy.CurrentRow == null) return;

            var kierowcaId = Convert.ToInt32(dgvKierowcy.CurrentRow.Cells["KierowcaID"].Value);
            var kierowca = _kierowcy.FirstOrDefault(k => k.KierowcaID == kierowcaId);

            if (kierowca == null) return;

            using var dlg = new KierowcaEditorForm(kierowca);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    await _repozytorium.AktualizujKierowceAsync(dlg.Kierowca);
                    await LoadKierowcyAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas aktualizacji kierowcy: {ex.Message}",
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void BtnUsunKierowca_Click(object sender, EventArgs e)
        {
            if (dgvKierowcy.CurrentRow == null) return;

            if (MessageBox.Show("Czy na pewno dezaktywować wybranego kierowcę?",
                "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                var kierowcaId = Convert.ToInt32(dgvKierowcy.CurrentRow.Cells["KierowcaID"].Value);
                await _repozytorium.UstawAktywnyKierowcaAsync(kierowcaId, false);
                await LoadKierowcyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dezaktywacji kierowcy: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Obsługa pojazdów

        private async Task LoadPojazdyAsync()
        {
            try
            {
                SetStatus("Ładowanie pojazdów...");
                Cursor = Cursors.WaitCursor;

                _pojazdy = await _repozytorium.PobierzPojazdyAsync(chkTylkoAktywnePojazdy.Checked);

                var dt = new DataTable();
                dt.Columns.Add("PojazdID", typeof(int));
                dt.Columns.Add("Rejestracja", typeof(string));
                dt.Columns.Add("Marka", typeof(string));
                dt.Columns.Add("Model", typeof(string));
                dt.Columns.Add("Palety", typeof(int));
                dt.Columns.Add("Aktywny", typeof(bool));

                foreach (var pojazd in _pojazdy)
                {
                    dt.Rows.Add(
                        pojazd.PojazdID,
                        pojazd.Rejestracja,
                        pojazd.Marka,
                        pojazd.Model,
                        pojazd.PaletyH1,
                        pojazd.Aktywny
                    );
                }

                dgvPojazdy.DataSource = dt;

                if (dgvPojazdy.Columns["PojazdID"] != null)
                    dgvPojazdy.Columns["PojazdID"].Visible = false;

                SetStatus($"Załadowano {_pojazdy.Count} pojazdów");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania pojazdów: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Błąd ładowania pojazdów");
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private async void BtnNowyPojazd_Click(object sender, EventArgs e)
        {
            using var dlg = new PojazdEditorForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    await _repozytorium.DodajPojazdAsync(dlg.Pojazd);
                    await LoadPojazdyAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas dodawania pojazdu: {ex.Message}",
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void BtnEdytujPojazd_Click(object sender, EventArgs e)
        {
            if (dgvPojazdy.CurrentRow == null) return;

            var pojazdId = Convert.ToInt32(dgvPojazdy.CurrentRow.Cells["PojazdID"].Value);
            var pojazd = _pojazdy.FirstOrDefault(p => p.PojazdID == pojazdId);

            if (pojazd == null) return;

            using var dlg = new PojazdEditorForm(pojazd);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    await _repozytorium.AktualizujPojazdAsync(dlg.Pojazd);
                    await LoadPojazdyAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas aktualizacji pojazdu: {ex.Message}",
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void BtnUsunPojazd_Click(object sender, EventArgs e)
        {
            if (dgvPojazdy.CurrentRow == null) return;

            if (MessageBox.Show("Czy na pewno dezaktywować wybrany pojazd?",
                "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                var pojazdId = Convert.ToInt32(dgvPojazdy.CurrentRow.Cells["PojazdID"].Value);
                await _repozytorium.UstawAktywnyPojazdAsync(pojazdId, false);
                await LoadPojazdyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dezaktywacji pojazdu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        private void SetStatus(string text)
        {
            lblStatusBar.Text = $"{DateTime.Now:HH:mm:ss} - {text}";
            Application.DoEvents();
        }
    }

    // ========== FORMULARZE POMOCNICZE ==========

    public class KierowcaEditorForm : Form
    {
        public Kierowca Kierowca { get; private set; }

        private TextBox txtImie;
        private TextBox txtNazwisko;
        private TextBox txtTelefon;
        private CheckBox chkAktywny;
        private Button btnOK;
        private Button btnAnuluj;

        public KierowcaEditorForm(Kierowca kierowca = null)
        {
            Kierowca = kierowca ?? new Kierowca { Aktywny = true };
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            Text = Kierowca.KierowcaID > 0 ? "Edycja kierowcy" : "Nowy kierowca";
            Size = new Size(400, 250);
            StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(10)
            };

            layout.Controls.Add(new Label { Text = "Imię:", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            txtImie = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(txtImie, 1, 0);

            layout.Controls.Add(new Label { Text = "Nazwisko:", TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            txtNazwisko = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(txtNazwisko, 1, 1);

            layout.Controls.Add(new Label { Text = "Telefon:", TextAlign = ContentAlignment.MiddleRight }, 0, 2);
            txtTelefon = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(txtTelefon, 1, 2);

            chkAktywny = new CheckBox { Text = "Aktywny", Checked = true };
            layout.Controls.Add(chkAktywny, 1, 3);

            var panelButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            btnOK = new Button { Text = "OK", Width = 80, DialogResult = DialogResult.OK };
            btnOK.Click += BtnOK_Click;
            btnAnuluj = new Button { Text = "Anuluj", Width = 80, DialogResult = DialogResult.Cancel };

            panelButtons.Controls.Add(btnOK);
            panelButtons.Controls.Add(btnAnuluj);

            layout.SetColumnSpan(panelButtons, 2);
            layout.Controls.Add(panelButtons, 0, 4);

            Controls.Add(layout);
        }

        private void LoadData()
        {
            txtImie.Text = Kierowca.Imie;
            txtNazwisko.Text = Kierowca.Nazwisko;
            txtTelefon.Text = Kierowca.Telefon;
            chkAktywny.Checked = Kierowca.Aktywny;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtImie.Text) || string.IsNullOrWhiteSpace(txtNazwisko.Text))
            {
                MessageBox.Show("Imię i nazwisko są wymagane.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            Kierowca.Imie = txtImie.Text.Trim();
            Kierowca.Nazwisko = txtNazwisko.Text.Trim();
            Kierowca.Telefon = txtTelefon.Text.Trim();
            Kierowca.Aktywny = chkAktywny.Checked;
        }
    }

    public class PojazdEditorForm : Form
    {
        public Pojazd Pojazd { get; private set; }

        private TextBox txtRejestracja;
        private TextBox txtMarka;
        private TextBox txtModel;
        private NumericUpDown nudPalety;
        private CheckBox chkAktywny;
        private Button btnOK;
        private Button btnAnuluj;

        public PojazdEditorForm(Pojazd pojazd = null)
        {
            Pojazd = pojazd ?? new Pojazd { Aktywny = true, PaletyH1 = 33 };
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            Text = Pojazd.PojazdID > 0 ? "Edycja pojazdu" : "Nowy pojazd";
            Size = new Size(400, 280);
            StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                Padding = new Padding(10)
            };

            layout.Controls.Add(new Label { Text = "Rejestracja:", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            txtRejestracja = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(txtRejestracja, 1, 0);

            layout.Controls.Add(new Label { Text = "Marka:", TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            txtMarka = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(txtMarka, 1, 1);

            layout.Controls.Add(new Label { Text = "Model:", TextAlign = ContentAlignment.MiddleRight }, 0, 2);
            txtModel = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(txtModel, 1, 2);

            layout.Controls.Add(new Label { Text = "Liczba palet:", TextAlign = ContentAlignment.MiddleRight }, 0, 3);
            nudPalety = new NumericUpDown { Minimum = 1, Maximum = 50, Value = 33, Dock = DockStyle.Fill };
            layout.Controls.Add(nudPalety, 1, 3);

            chkAktywny = new CheckBox { Text = "Aktywny", Checked = true };
            layout.Controls.Add(chkAktywny, 1, 4);

            var panelButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            btnOK = new Button { Text = "OK", Width = 80, DialogResult = DialogResult.OK };
            btnOK.Click += BtnOK_Click;
            btnAnuluj = new Button { Text = "Anuluj", Width = 80, DialogResult = DialogResult.Cancel };

            panelButtons.Controls.Add(btnOK);
            panelButtons.Controls.Add(btnAnuluj);

            layout.SetColumnSpan(panelButtons, 2);
            layout.Controls.Add(panelButtons, 0, 5);

            Controls.Add(layout);
        }

        private void LoadData()
        {
            txtRejestracja.Text = Pojazd.Rejestracja;
            txtMarka.Text = Pojazd.Marka;
            txtModel.Text = Pojazd.Model;
            nudPalety.Value = Pojazd.PaletyH1;
            chkAktywny.Checked = Pojazd.Aktywny;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtRejestracja.Text))
            {
                MessageBox.Show("Rejestracja jest wymagana.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            Pojazd.Rejestracja = txtRejestracja.Text.Trim().ToUpper();
            Pojazd.Marka = txtMarka.Text.Trim();
            Pojazd.Model = txtModel.Text.Trim();
            Pojazd.PaletyH1 = (int)nudPalety.Value;
            Pojazd.Aktywny = chkAktywny.Checked;
        }
    }
}