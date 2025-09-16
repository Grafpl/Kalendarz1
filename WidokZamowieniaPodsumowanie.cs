// Plik: WidokZamowieniaPodsumowanie.cs
// WERSJA 7.0 – wszystkie wymagane poprawki:
// - Rozszerzone dublowanie (wiele dni)
// - Ostrzeżenie przy cyklicznych
// - Domyślne daty na okrągły tydzień
// - Usunięcie kursora ładowania
// - DataGridy tylko do odczytu
// - Dodawanie notatek z poziomu tabeli
// - Data i godzina przyjęcia w głównym gridzie

#nullable enable
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;

namespace Kalendarz1
{
    // Rozszerzony dialog dublowania - możliwość wyboru wielu dni
    public class MultipleDatePickerDialog : Form
    {
        public List<DateTime> SelectedDates { get; private set; } = new();
        private DateTimePicker dtpStartDate;
        private DateTimePicker dtpEndDate;
        private CheckedListBox clbDays;
        private Label lblInfo;

        public MultipleDatePickerDialog(string title)
        {
            Text = title;
            Size = new Size(400, 450);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblStart = new Label
            {
                Text = "Od dnia:",
                Location = new Point(20, 20),
                Size = new Size(60, 25)
            };

            dtpStartDate = new DateTimePicker
            {
                Location = new Point(90, 17),
                Size = new Size(250, 25),
                Format = DateTimePickerFormat.Long
            };
            dtpStartDate.Value = DateTime.Today.AddDays(1);
            dtpStartDate.ValueChanged += DateRange_Changed;

            var lblEnd = new Label
            {
                Text = "Do dnia:",
                Location = new Point(20, 55),
                Size = new Size(60, 25)
            };

            dtpEndDate = new DateTimePicker
            {
                Location = new Point(90, 52),
                Size = new Size(250, 25),
                Format = DateTimePickerFormat.Long
            };
            dtpEndDate.Value = DateTime.Today.AddDays(7); // domyślnie tydzień
            dtpEndDate.ValueChanged += DateRange_Changed;

            lblInfo = new Label
            {
                Text = "Wybierz dni do dublowania:",
                Location = new Point(20, 90),
                Size = new Size(350, 20)
            };

            clbDays = new CheckedListBox
            {
                Location = new Point(20, 115),
                Size = new Size(350, 200),
                CheckOnClick = true
            };

            var btnSelectAll = new Button
            {
                Text = "Zaznacz wszystkie",
                Location = new Point(20, 325),
                Size = new Size(120, 30)
            };
            btnSelectAll.Click += (s, e) => {
                for (int i = 0; i < clbDays.Items.Count; i++)
                    clbDays.SetItemChecked(i, true);
            };

            var btnDeselectAll = new Button
            {
                Text = "Odznacz wszystkie",
                Location = new Point(150, 325),
                Size = new Size(120, 30)
            };
            btnDeselectAll.Click += (s, e) => {
                for (int i = 0; i < clbDays.Items.Count; i++)
                    clbDays.SetItemChecked(i, false);
            };

            var btnOK = new Button
            {
                Text = "Dubluj",
                DialogResult = DialogResult.OK,
                Location = new Point(80, 370),
                Size = new Size(100, 35),
                BackColor = Color.SeaGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnOK.Click += (s, e) => {
                SelectedDates.Clear();
                foreach (DateTime date in clbDays.CheckedItems)
                    SelectedDates.Add(date);
            };

            var btnCancel = new Button
            {
                Text = "Anuluj",
                DialogResult = DialogResult.Cancel,
                Location = new Point(190, 370),
                Size = new Size(100, 35)
            };

            Controls.AddRange(new Control[] {
                lblStart, dtpStartDate, lblEnd, dtpEndDate,
                lblInfo, clbDays, btnSelectAll, btnDeselectAll,
                btnOK, btnCancel
            });

            PopulateDays();
        }

        private void DateRange_Changed(object? sender, EventArgs e)
        {
            PopulateDays();
        }

        private void PopulateDays()
        {
            clbDays.Items.Clear();
            if (dtpEndDate.Value < dtpStartDate.Value)
            {
                lblInfo.Text = "Data końcowa nie może być wcześniejsza niż początkowa!";
                lblInfo.ForeColor = Color.Red;
                return;
            }

            lblInfo.Text = "Wybierz dni do dublowania:";
            lblInfo.ForeColor = SystemColors.ControlText;

            var current = dtpStartDate.Value.Date;
            while (current <= dtpEndDate.Value.Date)
            {
                clbDays.Items.Add(current, true); // domyślnie zaznaczone
                current = current.AddDays(1);
            }
        }
    }

    public class CykliczneZamowieniaDialog : Form
    {
        public List<DateTime> SelectedDays { get; private set; } = new();
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }

        private DateTimePicker dtpStart, dtpEnd;
        private CheckBox[] dayCheckBoxes;
        private RadioButton rbCodziennie, rbWybraneDni, rbCoTydzien;
        private Label lblPreview;
        private Label lblWarning;

        public CykliczneZamowieniaDialog()
        {
            Text = "Utwórz zamówienia cykliczne";
            Size = new Size(450, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Ostrzeżenie
            lblWarning = new Label
            {
                Text = "⚠️ UWAGA: Tworzysz cykliczne zamówienie dla zaznaczonego zamówienia.\n" +
                      "Pamiętaj o ostrożnym ustawianiu dat!",
                Location = new Point(20, 10),
                Size = new Size(400, 40),
                ForeColor = Color.DarkOrange,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.LightYellow,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(5)
            };

            // Zakres dat
            var lblStart = new Label { Text = "Od dnia:", Location = new Point(20, 65), Size = new Size(60, 25) };
            dtpStart = new DateTimePicker { Location = new Point(90, 62), Size = new Size(200, 25) };
            dtpStart.Value = DateTime.Today.AddDays(1);
            dtpStart.ValueChanged += (s, e) => UpdatePreview();

            var lblEnd = new Label { Text = "Do dnia:", Location = new Point(20, 95), Size = new Size(60, 25) };
            dtpEnd = new DateTimePicker { Location = new Point(90, 92), Size = new Size(200, 25) };
            // Domyślnie ustawienie na okrągły tydzień
            dtpEnd.Value = DateTime.Today.AddDays(7);
            dtpEnd.ValueChanged += (s, e) => UpdatePreview();

            // Opcje częstotliwości
            var gbOpcje = new GroupBox
            {
                Text = "Częstotliwość",
                Location = new Point(20, 130),
                Size = new Size(400, 150)
            };

            rbCodziennie = new RadioButton
            {
                Text = "Codziennie (poniedziałek - piątek)",
                Location = new Point(15, 25),
                Size = new Size(250, 25),
                Checked = true
            };
            rbCodziennie.CheckedChanged += (s, e) => { UpdateCheckboxesState(); UpdatePreview(); };

            rbWybraneDni = new RadioButton
            {
                Text = "Wybrane dni tygodnia:",
                Location = new Point(15, 55),
                Size = new Size(150, 25)
            };
            rbWybraneDni.CheckedChanged += (s, e) => { UpdateCheckboxesState(); UpdatePreview(); };

            rbCoTydzien = new RadioButton
            {
                Text = "Co tydzień w:",
                Location = new Point(15, 115),
                Size = new Size(100, 25)
            };
            rbCoTydzien.CheckedChanged += (s, e) => { UpdateCheckboxesState(); UpdatePreview(); };

            // Checkboxy dni tygodnia
            string[] dni = { "Pn", "Wt", "Śr", "Cz", "Pt", "So", "Nd" };
            dayCheckBoxes = new CheckBox[7];
            for (int i = 0; i < 7; i++)
            {
                dayCheckBoxes[i] = new CheckBox
                {
                    Text = dni[i],
                    Location = new Point(170 + (i * 45), 55),
                    Size = new Size(40, 25),
                    Checked = i < 5,
                    Enabled = false
                };
                dayCheckBoxes[i].CheckedChanged += (s, e) => UpdatePreview();
                gbOpcje.Controls.Add(dayCheckBoxes[i]);
            }

            gbOpcje.Controls.AddRange(new Control[] { rbCodziennie, rbWybraneDni, rbCoTydzien });

            // Podgląd
            lblPreview = new Label
            {
                Text = "Zostanie utworzonych: 0 zamówień",
                Location = new Point(20, 295),
                Size = new Size(400, 60),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(5),
                BackColor = Color.LightYellow
            };

            // Przyciski
            var btnOK = new Button
            {
                Text = "Utwórz zamówienia",
                DialogResult = DialogResult.OK,
                Location = new Point(100, 370),
                Size = new Size(120, 35),
                BackColor = Color.DarkOrange,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnOK.Click += (s, e) => CalculateSelectedDays();

            var btnCancel = new Button
            {
                Text = "Anuluj",
                DialogResult = DialogResult.Cancel,
                Location = new Point(230, 370),
                Size = new Size(100, 35)
            };

            Controls.AddRange(new Control[] {
                lblWarning, lblStart, dtpStart, lblEnd, dtpEnd, gbOpcje, lblPreview, btnOK, btnCancel
            });

            UpdatePreview();
        }

        private void UpdateCheckboxesState()
        {
            bool enable = rbWybraneDni.Checked || rbCoTydzien.Checked;
            foreach (var cb in dayCheckBoxes)
            {
                cb.Enabled = enable;
                if (rbCoTydzien.Checked)
                {
                    cb.Checked = false;
                }
            }
            if (rbCoTydzien.Checked && dayCheckBoxes.Length > 0)
            {
                dayCheckBoxes[0].Checked = true;
            }
        }

        private void UpdatePreview()
        {
            CalculateSelectedDays();
            lblPreview.Text = $"Zostanie utworzonych: {SelectedDays.Count} zamówień\n" +
                             (SelectedDays.Count > 0 ?
                              $"Pierwsze: {SelectedDays.First():yyyy-MM-dd}\n" +
                              $"Ostatnie: {SelectedDays.Last():yyyy-MM-dd}" : "");
        }

        private void CalculateSelectedDays()
        {
            SelectedDays.Clear();
            StartDate = dtpStart.Value.Date;
            EndDate = dtpEnd.Value.Date;

            if (EndDate < StartDate)
            {
                MessageBox.Show("Data końcowa nie może być wcześniejsza niż początkowa!",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var current = StartDate;
            while (current <= EndDate)
            {
                var dayOfWeek = (int)current.DayOfWeek;
                var dayIndex = dayOfWeek == 0 ? 6 : dayOfWeek - 1;

                if (rbCodziennie.Checked)
                {
                    if (dayIndex < 5)
                        SelectedDays.Add(current);
                }
                else if (rbWybraneDni.Checked)
                {
                    if (dayCheckBoxes[dayIndex].Checked)
                        SelectedDays.Add(current);
                }
                else if (rbCoTydzien.Checked)
                {
                    if (dayCheckBoxes[dayIndex].Checked)
                    {
                        var weeksDiff = ((current - StartDate).Days / 7);
                        if ((current - StartDate).Days % 7 == 0 ||
                            current.DayOfWeek == StartDate.DayOfWeek)
                            SelectedDays.Add(current);
                    }
                }

                current = current.AddDays(1);
            }
        }
    }

    // Dialog do dodawania/edycji notatek
    public class NotatkiDialog : Form
    {
        public string Notatka { get; private set; } = "";
        private TextBox txtNotatka;

        public NotatkiDialog(string currentNote = "")
        {
            Text = "Edytuj notatkę do zamówienia";
            Size = new Size(500, 300);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblInfo = new Label
            {
                Text = "Wprowadź notatkę:",
                Location = new Point(20, 20),
                Size = new Size(450, 20)
            };

            txtNotatka = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(20, 45),
                Size = new Size(450, 150),
                Text = currentNote
            };

            var btnOK = new Button
            {
                Text = "Zapisz",
                DialogResult = DialogResult.OK,
                Location = new Point(140, 210),
                Size = new Size(100, 35),
                BackColor = Color.SeaGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOK.Click += (s, e) => Notatka = txtNotatka.Text;

            var btnCancel = new Button
            {
                Text = "Anuluj",
                DialogResult = DialogResult.Cancel,
                Location = new Point(250, 210),
                Size = new Size(100, 35)
            };

            Controls.AddRange(new Control[] { lblInfo, txtNotatka, btnOK, btnCancel });
        }
    }

    public partial class WidokZamowieniaPodsumowanie : Form
    {
        // ====== Połączenia ======
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // ====== Stan UI ======
        public string UserID { get; set; } = string.Empty;
        private DateTime _selectedDate;
        private int? _aktualneIdZamowienia;
        private readonly List<Button> _dayButtons = new();

        // ====== Dane i Cache ======
        private readonly DataTable _dtZamowienia = new();
        private readonly BindingSource _bsZamowienia = new();
        private readonly Dictionary<int, string> _twKodCache = new();
        private readonly Dictionary<int, string> _twKatalogCache = new();
        private readonly Dictionary<string, string> _userCache = new();
        private readonly List<string> _handlowcyCache = new();

        private readonly Dictionary<string, decimal> YieldByKod = new(StringComparer.OrdinalIgnoreCase)
        {
            // {"Filet", 0.32m}, {"Ćwiartka", 0.22m}, {"Skrzydło", 0.09m}, ...
        };

        private NazwaZiD nazwaZiD = new NazwaZiD();

        public WidokZamowieniaPodsumowanie()
        {
            InitializeComponent();
            Load += WidokZamowieniaPodsumowanie_Load;
            btnUsun.Visible = false;

            if (dgvPojTuszki != null)
            {
                nazwaZiD.PokazPojTuszki(dgvPojTuszki);
            }
        }

        private async void WidokZamowieniaPodsumowanie_Load(object? sender, EventArgs e)
        {
            _selectedDate = DateTime.Today;
            UstawPrzyciskiDniTygodnia();

            if (dgvPojTuszki == null)
            {
                dgvPojTuszki = new DataGridView();
                dgvPojTuszki.Name = "dgvPojTuszki";
                dgvPojTuszki.Height = 110;
                dgvPojTuszki.Dock = DockStyle.Bottom;
                dgvPojTuszki.ReadOnly = true;
                dgvPojTuszki.AllowUserToAddRows = false;
                dgvPojTuszki.AllowUserToDeleteRows = false;
                dgvPojTuszki.RowHeadersVisible = false;
                panelPrzychody.Controls.Add(dgvPojTuszki);
                panelPrzychody.Controls.SetChildIndex(dgvPojTuszki, 0);
            }

            SzybkiGrid(dgvZamowienia);
            SzybkiGrid(dgvSzczegoly);
            SzybkiGrid(dgvAgregacja);
            SzybkiGrid(dgvPrzychody);
            SzybkiGrid(dgvPojTuszki);

            btnUsun.Visible = (UserID == "11111");
            nazwaZiD.PokazPojTuszki(dgvPojTuszki);

            await ZaladujDanePoczatkoweAsync();
            await OdswiezWszystkieDaneAsync();
        }

        #region Helpers
        private static string SafeString(IDataRecord r, int i)
            => r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i)) ?? string.Empty;

        private static int? SafeInt32N(IDataRecord r, int i)
            => r.IsDBNull(i) ? (int?)null : Convert.ToInt32(r.GetValue(i));

        private static DateTime? SafeDateTimeN(IDataRecord r, int i)
            => r.IsDBNull(i) ? (DateTime?)null : Convert.ToDateTime(r.GetValue(i));

        private static decimal SafeDecimal(IDataRecord r, int i)
        {
            if (r.IsDBNull(i)) return 0m;
            return Convert.ToDecimal(r.GetValue(i));
        }

        private static object DbOrNull(DateTime? dt) => dt.HasValue ? dt.Value : DBNull.Value;
        private static object DbOrNull(object? v) => v ?? DBNull.Value;

        private static decimal ReadDecimal(IDataRecord r, int i)
        {
            if (r.IsDBNull(i)) return 0m;
            return Convert.ToDecimal(r.GetValue(i));
        }

        private static string AsString(IDataRecord r, int i)
        {
            if (r.IsDBNull(i)) return "";
            return Convert.ToString(r.GetValue(i)) ?? "";
        }

        private static bool IsKurczakB(string kod)
            => kod.IndexOf("Kurczak B", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsKurczakA(string kod)
            => kod.IndexOf("Kurczak A", StringComparison.OrdinalIgnoreCase) >= 0;
        #endregion

        #region Inicjalizacja i UI
        private void UstawPrzyciskiDniTygodnia()
        {
            _dayButtons.AddRange(new[] { btnPon, btnWt, btnSr, btnCzw, btnPt, btnSo, btnNd });
            foreach (var btn in _dayButtons)
                btn.Click += DzienButton_Click;
            AktualizujDatyPrzyciskow();
        }

        private void SzybkiGrid(DataGridView dgv)
        {
            if (dgv == null) return;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.AllowUserToResizeRows = false;
            dgv.AllowUserToResizeColumns = true;
            dgv.RowHeadersVisible = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.BackgroundColor = Color.White;
            dgv.BorderStyle = BorderStyle.None;
            dgv.RowTemplate.Height = 30;
            dgv.Font = new Font("Segoe UI", 9f);
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            dgv.ReadOnly = true; // WSZYSTKIE GRIDY READONLY
            TryEnableDoubleBuffer(dgv);
        }

        private static void TryEnableDoubleBuffer(Control c)
        {
            try
            {
                var pi = c.GetType().GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                pi?.SetValue(c, true, null);
            }
            catch { }
        }
        #endregion

        #region Nawigacja i zdarzenia
        private async void DzienButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button clickedButton && clickedButton.Tag is DateTime date)
            {
                _selectedDate = date;
                AktualizujDatyPrzyciskow();
                await OdswiezWszystkieDaneAsync();
            }
        }

        private async void btnTydzienPrev_Click(object? sender, EventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(-7);
            AktualizujDatyPrzyciskow();
            await OdswiezWszystkieDaneAsync();
        }

        private async void btnTydzienNext_Click(object? sender, EventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(7);
            AktualizujDatyPrzyciskow();
            await OdswiezWszystkieDaneAsync();
        }

        private void AktualizujDatyPrzyciskow()
        {
            int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
            DateTime startOfWeek = _selectedDate.AddDays(-delta);
            lblZakresDat.Text = $"{startOfWeek:dd.MM.yyyy} - {startOfWeek.AddDays(6):dd.MM.yyyy}";

            for (int i = 0; i < 7; i++)
            {
                var dt = startOfWeek.AddDays(i);
                _dayButtons[i].Tag = dt;
                _dayButtons[i].Text = $"{_dayButtons[i].Name.Substring(3)}\n{dt:dd.MM}";
                _dayButtons[i].BackColor = dt.Date == _selectedDate.Date ? SystemColors.Highlight : SystemColors.Control;
                _dayButtons[i].ForeColor = dt.Date == _selectedDate.Date ? Color.White : SystemColors.ControlText;
            }
        }

        private async void btnOdswiez_Click(object? sender, EventArgs e)
        {
            await OdswiezWszystkieDaneAsync();
        }

        private void btnNoweZamowienie_Click(object? sender, EventArgs e)
        {
            using var widokZamowienia = new WidokZamowienia(UserID, null);
            if (widokZamowienia.ShowDialog(this) == DialogResult.OK)
            {
                Task.Run(async () => await OdswiezWszystkieDaneAsync());
            }
        }

        private void btnModyfikuj_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                MessageBox.Show("Najpierw kliknij wiersz z zamówieniem, aby je wybrać.", "Brak wyboru",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var widokZamowienia = new WidokZamowienia(UserID, id);
            if (widokZamowienia.ShowDialog(this) == DialogResult.OK)
            {
                Task.Run(async () => await OdswiezWszystkieDaneAsync());
            }
        }

        // Nowy przycisk do dodawania notatek
        private async void btnDodajNotatke_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                MessageBox.Show("Najpierw wybierz zamówienie, do którego chcesz dodać notatkę.", "Brak wyboru",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Pobierz aktualną notatkę
            string currentNote = "";
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                var cmd = new SqlCommand("SELECT Uwagi FROM ZamowieniaMieso WHERE Id = @Id", cn);
                cmd.Parameters.AddWithValue("@Id", id);
                var result = await cmd.ExecuteScalarAsync();
                currentNote = result?.ToString() ?? "";
            }

            using var dlg = new NotatkiDialog(currentNote);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    var cmd = new SqlCommand("UPDATE ZamowieniaMieso SET Uwagi = @Uwagi WHERE Id = @Id", cn);
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Uwagi", string.IsNullOrEmpty(dlg.Notatka) ? DBNull.Value : dlg.Notatka);
                    await cmd.ExecuteNonQueryAsync();

                    MessageBox.Show("Notatka została zapisana.", "Sukces",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await WyswietlSzczegolyZamowieniaAsync(id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas zapisywania notatki: {ex.Message}", "Błąd",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void btnAnuluj_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                MessageBox.Show("Najpierw kliknij wiersz z zamówieniem, które chcesz anulować.", "Brak wyboru",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show("Czy na pewno chcesz anulować wybrane zamówienie? Tej operacji nie można cofnąć.",
                "Potwierdź anulowanie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    await using var cmd = new SqlCommand("UPDATE dbo.ZamowieniaMieso SET Status = 'Anulowane' WHERE Id = @Id", cn);
                    cmd.Parameters.AddWithValue("@Id", _aktualneIdZamowienia.Value);
                    await cmd.ExecuteNonQueryAsync();

                    MessageBox.Show("Zamówienie zostało anulowane.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await OdswiezWszystkieDaneAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Wystąpił błąd podczas anulowania zamówienia: {ex.Message}", "Błąd krytyczny",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void Filtry_Changed(object? sender, EventArgs e)
        {
            ZastosujFiltry();
            AktualizujPodsumowanieDnia();
        }

        private async void btnDuplikuj_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                MessageBox.Show("Najpierw wybierz zamówienie do duplikacji.", "Brak wyboru",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new MultipleDatePickerDialog("Wybierz dni dla duplikatu zamówienia");
            if (dlg.ShowDialog() == DialogResult.OK && dlg.SelectedDates.Any())
            {
                try
                {
                    int utworzono = 0;
                    foreach (var date in dlg.SelectedDates)
                    {
                        await DuplikujZamowienie(id, date);
                        utworzono++;
                    }

                    MessageBox.Show($"Zamówienie zostało zduplikowane na {utworzono} dni.\n" +
                                  $"Od {dlg.SelectedDates.Min():yyyy-MM-dd} do {dlg.SelectedDates.Max():yyyy-MM-dd}",
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Przejdź do pierwszego dnia z duplikatem
                    _selectedDate = dlg.SelectedDates.First();
                    AktualizujDatyPrzyciskow();
                    await OdswiezWszystkieDaneAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas duplikowania: {ex.Message}", "Błąd",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task DuplikujZamowienie(int sourceId, DateTime targetDate)
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var tr = cn.BeginTransaction();

            try
            {
                int klientId = 0;
                string uwagi = "";
                DateTime godzinaPrzyjazdu = DateTime.Today.AddHours(8);

                using (var cmd = new SqlCommand(
                    @"SELECT KlientId, Uwagi, DataPrzyjazdu 
                      FROM ZamowieniaMieso WHERE Id = @Id", cn, tr))
                {
                    cmd.Parameters.AddWithValue("@Id", sourceId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        klientId = reader.GetInt32(0);
                        uwagi = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        godzinaPrzyjazdu = reader.GetDateTime(2);
                        godzinaPrzyjazdu = targetDate.Date.Add(godzinaPrzyjazdu.TimeOfDay);
                    }
                }

                var cmdGetId = new SqlCommand("SELECT ISNULL(MAX(Id),0)+1 FROM ZamowieniaMieso", cn, tr);
                int newId = Convert.ToInt32(await cmdGetId.ExecuteScalarAsync());

                var cmdInsert = new SqlCommand(
                    @"INSERT INTO ZamowieniaMieso (Id, DataZamowienia, DataPrzyjazdu, KlientId, Uwagi, IdUser, DataUtworzenia) 
                      VALUES (@id, @dz, @dp, @kid, @uw, @u, GETDATE())", cn, tr);
                cmdInsert.Parameters.AddWithValue("@id", newId);
                cmdInsert.Parameters.AddWithValue("@dz", targetDate.Date);
                cmdInsert.Parameters.AddWithValue("@dp", godzinaPrzyjazdu);
                cmdInsert.Parameters.AddWithValue("@kid", klientId);
                cmdInsert.Parameters.AddWithValue("@uw", string.IsNullOrEmpty(uwagi) ? DBNull.Value : uwagi + " [DUPLIKAT]");
                cmdInsert.Parameters.AddWithValue("@u", UserID);
                await cmdInsert.ExecuteNonQueryAsync();

                var cmdCopyItems = new SqlCommand(
                    @"INSERT INTO ZamowieniaMiesoTowar (ZamowienieId, KodTowaru, Ilosc, Cena)
                      SELECT @newId, KodTowaru, Ilosc, Cena 
                      FROM ZamowieniaMiesoTowar 
                      WHERE ZamowienieId = @sourceId", cn, tr);
                cmdCopyItems.Parameters.AddWithValue("@newId", newId);
                cmdCopyItems.Parameters.AddWithValue("@sourceId", sourceId);
                await cmdCopyItems.ExecuteNonQueryAsync();

                await tr.CommitAsync();
            }
            catch
            {
                await tr.RollbackAsync();
                throw;
            }
        }

        private async void btnCykliczne_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                MessageBox.Show("Najpierw wybierz zamówienie wzorcowe dla cyklu.", "Brak wyboru",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new CykliczneZamowieniaDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    int utworzono = 0;
                    foreach (var date in dlg.SelectedDays)
                    {
                        await DuplikujZamowienie(id, date);
                        utworzono++;
                    }

                    MessageBox.Show($"Utworzono {utworzono} zamówień cyklicznych.\n" +
                                  $"Od {dlg.StartDate:yyyy-MM-dd} do {dlg.EndDate:yyyy-MM-dd}",
                                  "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    await OdswiezWszystkieDaneAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas tworzenia zamówień cyklicznych: {ex.Message}",
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        #endregion

        #region Wczytywanie i przetwarzanie
        private async Task OdswiezWszystkieDaneAsync()
        {
            // Usunięcie kursora ładowania
            // Cursor = Cursors.WaitCursor; // USUNIĘTE
            try
            {
                await WczytajZamowieniaDlaDniaAsync(_selectedDate);
                await WczytajDanePrzychodowAsync(_selectedDate);
                await WyswietlAgregacjeProduktowAsync(_selectedDate);
                AktualizujPodsumowanieDnia();
                WyczyscSzczegoly();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas odświeżania danych: {ex.Message}\n\nSTACKTRACE:\n{ex.StackTrace}\n\nINNER: {ex.InnerException}",
                    "Błąd Krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Cursor = Cursors.Default; // USUNIĘTE
            }
        }

        private async Task ZaladujDanePoczatkoweAsync()
        {
            _twKodCache.Clear();
            _twKatalogCache.Clear();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT ID, kod, katalog FROM [HANDEL].[HM].[TW]", cn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int idtw = reader.GetInt32(0);
                    string kod = AsString(reader, 1);

                    object katObj = reader.GetValue(2);
                    bool w67095 = false;
                    if (!(katObj is DBNull))
                    {
                        if (katObj is int ki) w67095 = (ki == 67095);
                        else w67095 = string.Equals(Convert.ToString(katObj), "67095", StringComparison.Ordinal);
                    }

                    _twKodCache[idtw] = kod;
                    if (w67095)
                        _twKatalogCache[idtw] = kod;
                }
            }

            var listaTowarow = _twKatalogCache
                .OrderBy(x => x.Value)
                .Select(k => new KeyValuePair<int, string>(k.Key, k.Value))
                .ToList();
            listaTowarow.Insert(0, new KeyValuePair<int, string>(0, "— Wszystkie towary —"));
            cbFiltrujTowar.DataSource = new BindingSource(listaTowarow, null);
            cbFiltrujTowar.DisplayMember = "Value";
            cbFiltrujTowar.ValueMember = "Key";
            cbFiltrujTowar.SelectedIndexChanged += Filtry_Changed;
            cbFiltrujTowar.SelectedIndex = 0;

            _userCache.Clear();
            await using (var cn2 = new SqlConnection(_connLibra))
            {
                await cn2.OpenAsync();
                await using var cmd = new SqlCommand("SELECT ID, Name FROM dbo.operators", cn2);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var idStr = AsString(reader, 0);
                    var name = AsString(reader, 1);
                    if (!string.IsNullOrEmpty(idStr))
                        _userCache[idStr] = name;
                }
            }

            _handlowcyCache.Clear();
            await using (var cn3 = new SqlConnection(_connHandel))
            {
                await cn3.OpenAsync();
                await using var cmd = new SqlCommand(@"
                    SELECT DISTINCT CDim_Handlowiec_Val 
                    FROM [HANDEL].[SSCommon].[ContractorClassification] 
                    WHERE CDim_Handlowiec_Val IS NOT NULL
                    ORDER BY 1", cn3);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var val = AsString(reader, 0);
                    if (!string.IsNullOrWhiteSpace(val))
                        _handlowcyCache.Add(val);
                }
            }

            cbFiltrujHandlowca.Items.Clear();
            cbFiltrujHandlowca.Items.Add("— Wszyscy —");
            cbFiltrujHandlowca.Items.AddRange(_handlowcyCache.ToArray());
            cbFiltrujHandlowca.SelectedIndexChanged += Filtry_Changed;
            cbFiltrujHandlowca.SelectedIndex = 0;

            txtFiltrujOdbiorce.TextChanged += Filtry_Changed;
        }

        private async Task WczytajZamowieniaDlaDniaAsync(DateTime dzien)
        {
            // Dodanie kolumn dla daty i godziny przyjęcia
            if (_dtZamowienia.Columns.Count == 0)
            {
                _dtZamowienia.Columns.Add("Id", typeof(int));
                _dtZamowienia.Columns.Add("Odbiorca", typeof(string));
                _dtZamowienia.Columns.Add("Handlowiec", typeof(string));
                _dtZamowienia.Columns.Add("IloscZamowiona", typeof(decimal));
                _dtZamowienia.Columns.Add("IloscFaktyczna", typeof(decimal));
                _dtZamowienia.Columns.Add("DataPrzyjecia", typeof(DateTime)); // NOWA KOLUMNA
                _dtZamowienia.Columns.Add("GodzinaPrzyjecia", typeof(string)); // NOWA KOLUMNA
                var colDataUtw = new DataColumn("DataUtworzenia", typeof(DateTime));
                colDataUtw.AllowDBNull = true;
                _dtZamowienia.Columns.Add(colDataUtw);
                _dtZamowienia.Columns.Add("Utworzyl", typeof(string));
                _dtZamowienia.Columns.Add("Status", typeof(string));
            }
            else
            {
                _dtZamowienia.Clear();
            }

            var kontrahenci = new Dictionary<int, (string Nazwa, string Handlowiec)>();
            await using (var cnHandel = new SqlConnection(_connHandel))
            {
                await cnHandel.OpenAsync();
                const string sqlKontr = @"
                    SELECT c.Id, c.Shortcut, wym.CDim_Handlowiec_Val 
                    FROM [HANDEL].[SSCommon].[STContractors] c
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId";
                await using var cmdKontr = new SqlCommand(sqlKontr, cnHandel);
                await using var rd = await cmdKontr.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    string shortcut = SafeString(rd, 1);
                    string handl = SafeString(rd, 2);
                    kontrahenci[id] = (string.IsNullOrWhiteSpace(shortcut) ? $"KH {id}" : shortcut, handl);
                }
            }

            int? selectedProductId = null;
            if (cbFiltrujTowar.SelectedIndex > 0 && cbFiltrujTowar.SelectedValue is int selectedTowarId)
                selectedProductId = selectedTowarId;

            var temp = new DataTable();
            if (_twKatalogCache.Keys.Any())
            {
                await using (var cnLibra = new SqlConnection(_connLibra))
                {
                    await cnLibra.OpenAsync();
                    var idwList = string.Join(",", _twKatalogCache.Keys);
                    string sql = $@"
                        SELECT zm.Id, zm.KlientId, SUM(ISNULL(zmt.Ilosc,0)) AS Ilosc, 
                               zm.DataPrzyjazdu, zm.DataUtworzenia, zm.IdUser, zm.Status
                        FROM [dbo].[ZamowieniaMieso] zm
                        JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
                        WHERE zm.DataZamowienia = @Dzien AND zmt.KodTowaru IN ({idwList}) " +
                            (selectedProductId.HasValue ? "AND zmt.KodTowaru = @TowarId " : "") +
                            @"GROUP BY zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.DataUtworzenia, zm.IdUser, zm.Status
                          ORDER BY zm.Id";

                    await using var cmd = new SqlCommand(sql, cnLibra);
                    cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                    if (selectedProductId.HasValue)
                        cmd.Parameters.AddWithValue("@TowarId", selectedProductId.Value);
                    using var da = new SqlDataAdapter(cmd);
                    da.Fill(temp);
                }
            }

            var wydaniaPerKhidIdtw = await PobierzWydaniaPerKhidIdtwAsync(dzien);
            var klienciZamowien = new HashSet<int>(temp.Rows.Cast<DataRow>()
                .Select(r => r["KlientId"] == DBNull.Value ? 0 : Convert.ToInt32(r["KlientId"])));

            foreach (DataRow r in temp.Rows)
            {
                int id = r["Id"] == DBNull.Value ? 0 : Convert.ToInt32(r["Id"]);
                int klientId = r["KlientId"] == DBNull.Value ? 0 : Convert.ToInt32(r["KlientId"]);
                decimal ilosc = r["Ilosc"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Ilosc"]);
                DateTime? dataPrzyjazdu = (r["DataPrzyjazdu"] is DBNull or null) ? (DateTime?)null : Convert.ToDateTime(r["DataPrzyjazdu"]);
                DateTime? dataUtw = (r["DataUtworzenia"] is DBNull or null) ? (DateTime?)null : Convert.ToDateTime(r["DataUtworzenia"]);
                string idUser = r["IdUser"]?.ToString() ?? "";
                string status = r["Status"]?.ToString() ?? "Nowe";

                var (nazwa, handlowiec) = kontrahenci.TryGetValue(klientId, out var kh)
                    ? kh
                    : ($"Nieznany ({klientId})", "");

                decimal wydane = 0m;
                if (wydaniaPerKhidIdtw.TryGetValue(klientId, out var perIdtw))
                {
                    wydane = perIdtw.Values.Sum();
                }

                _dtZamowienia.Rows.Add(
                    id,
                    nazwa,
                    handlowiec,
                    ilosc,
                    wydane,
                    dataPrzyjazdu?.Date ?? dzien,                    // Data przyjęcia
                    dataPrzyjazdu?.ToString("HH:mm") ?? "08:00",    // Godzina przyjęcia
                    dataUtw.HasValue ? (object)dataUtw.Value : DBNull.Value,
                    _userCache.TryGetValue(idUser, out var user) ? user : "Brak",
                    status
                );
            }

            var wydaniaBezZamowien = new List<DataRow>();
            foreach (var kv in wydaniaPerKhidIdtw)
            {
                int khid = kv.Key;
                if (klienciZamowien.Contains(khid)) continue;
                decimal wydane = kv.Value.Values.Sum();
                var (nazwa, handlowiec) = kontrahenci.TryGetValue(khid, out var kh)
                    ? kh
                    : ($"Nieznany ({khid})", "");
                var row = _dtZamowienia.NewRow();
                row["Id"] = 0;
                row["Odbiorca"] = nazwa;
                row["Handlowiec"] = handlowiec;
                row["IloscZamowiona"] = 0m;
                row["IloscFaktyczna"] = wydane;
                row["DataPrzyjecia"] = dzien;
                row["GodzinaPrzyjecia"] = "";
                row["DataUtworzenia"] = DBNull.Value;
                row["Utworzyl"] = "";
                row["Status"] = "Wydanie bez zamówienia";
                wydaniaBezZamowien.Add(row);
            }

            foreach (var row in wydaniaBezZamowien.OrderByDescending(r => (decimal)r["IloscFaktyczna"]))
                _dtZamowienia.Rows.Add(row.ItemArray);

            _bsZamowienia.DataSource = _dtZamowienia;
            dgvZamowienia.DataSource = _bsZamowienia;
            _bsZamowienia.Sort = "Status ASC, IloscZamowiona DESC";
            dgvZamowienia.ClearSelection();

            // Ustawienia kolumn z datą i godziną przyjęcia
            if (dgvZamowienia.Columns["Id"] != null) dgvZamowienia.Columns["Id"].Visible = false;
            if (dgvZamowienia.Columns["Odbiorca"] != null)
                dgvZamowienia.Columns["Odbiorca"].Width = 140;
            if (dgvZamowienia.Columns["Handlowiec"] != null)
                dgvZamowienia.Columns["Handlowiec"].Width = 100;
            if (dgvZamowienia.Columns["IloscZamowiona"] != null)
            {
                dgvZamowienia.Columns["IloscZamowiona"].DefaultCellStyle.Format = "N0";
                dgvZamowienia.Columns["IloscZamowiona"].HeaderText = "Zamówiono";
                dgvZamowienia.Columns["IloscZamowiona"].Width = 80;
            }
            if (dgvZamowienia.Columns["IloscFaktyczna"] != null)
            {
                dgvZamowienia.Columns["IloscFaktyczna"].DefaultCellStyle.Format = "N0";
                dgvZamowienia.Columns["IloscFaktyczna"].HeaderText = "Wydano";
                dgvZamowienia.Columns["IloscFaktyczna"].Width = 80;
            }
            if (dgvZamowienia.Columns["DataPrzyjecia"] != null)
            {
                dgvZamowienia.Columns["DataPrzyjecia"].HeaderText = "Data odbioru";
                dgvZamowienia.Columns["DataPrzyjecia"].DefaultCellStyle.Format = "dd.MM";
                dgvZamowienia.Columns["DataPrzyjecia"].Width = 80;
            }
            if (dgvZamowienia.Columns["GodzinaPrzyjecia"] != null)
            {
                dgvZamowienia.Columns["GodzinaPrzyjecia"].HeaderText = "Godz.";
                dgvZamowienia.Columns["GodzinaPrzyjecia"].Width = 50;
            }
            if (dgvZamowienia.Columns["DataUtworzenia"] != null)
            {
                dgvZamowienia.Columns["DataUtworzenia"].HeaderText = "Utworzono";
                dgvZamowienia.Columns["DataUtworzenia"].DefaultCellStyle.Format = "dd.MM HH:mm";
                dgvZamowienia.Columns["DataUtworzenia"].Width = 100;
            }
            if (dgvZamowienia.Columns["Utworzyl"] != null)
                dgvZamowienia.Columns["Utworzyl"].Width = 80;
            if (dgvZamowienia.Columns["Status"] != null)
            {
                dgvZamowienia.Columns["Status"].Width = 120;
                dgvZamowienia.Columns["Status"].DisplayIndex = dgvZamowienia.Columns.Count - 1;
            }

            ZastosujFiltry();
        }

        private async Task<Dictionary<int, decimal>> PobierzFaktyczneWydaniaAsync(DateTime dzien, int? towarId = null)
        {
            var wynik = new Dictionary<int, decimal>();
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            string sqlWz = @"
                SELECT DK.khid, SUM(ABS(MZ.ilosc))
                FROM [HANDEL].[HM].[MZ] MZ
                JOIN [HANDEL].[HM].[DK] ON MZ.super = DK.id
                WHERE DK.seria IN ('sWZ', 'sWZ-W') AND DK.data = @Dzien " +
                    (towarId.HasValue ? "AND MZ.idtw = @TowarId " : "") +
                    "GROUP BY DK.khid";

            await using var cmd = new SqlCommand(sqlWz, cn);
            cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
            if (towarId.HasValue) cmd.Parameters.AddWithValue("@TowarId", towarId.Value);

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                int khid = rd.GetInt32(0);
                decimal ilosc = SafeDecimal(rd, 1);
                wynik[khid] = ilosc;
            }
            return wynik;
        }

        private async Task<Dictionary<int, Dictionary<int, decimal>>> PobierzWydaniaPerKhidIdtwAsync(DateTime dzien)
        {
            var dict = new Dictionary<int, Dictionary<int, decimal>>();
            if (!_twKatalogCache.Keys.Any()) return dict;

            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            var idwList = string.Join(",", _twKatalogCache.Keys);
            string sql = $@"
                SELECT MG.khid, MZ.idtw, SUM(ABS(MZ.ilosc)) AS qty
                FROM [HANDEL].[HM].[MZ] MZ
                JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny = 1 AND MG.data = @Dzien AND MG.khid IS NOT NULL
                AND MZ.idtw IN ({idwList})
                GROUP BY MG.khid, MZ.idtw";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                int khid = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                int idtw = rdr.GetInt32(1);
                decimal qty = ReadDecimal(rdr, 2);
                if (!dict.TryGetValue(khid, out var perIdtw))
                {
                    perIdtw = new Dictionary<int, decimal>();
                    dict[khid] = perIdtw;
                }
                if (perIdtw.ContainsKey(idtw)) perIdtw[idtw] += qty;
                else perIdtw[idtw] = qty;
            }
            return dict;
        }

        private async void dgvZamowienia_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvZamowienia.CurrentRow != null && dgvZamowienia.CurrentRow.Index >= 0)
            {
                await HandleGridSelection(dgvZamowienia.CurrentRow.Index);
            }
            else
            {
                WyczyscSzczegoly();
            }
        }

        private async void dgvZamowienia_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                await HandleGridSelection(e.RowIndex);
            }
        }

        private async Task HandleGridSelection(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvZamowienia.Rows.Count)
            {
                WyczyscSzczegoly();
                return;
            }

            var row = dgvZamowienia.Rows[rowIndex];
            if (row.DataBoundItem is DataRowView drv)
            {
                var status = drv.Row.Field<string>("Status") ?? "";
                if (status == "Wydanie bez zamówienia")
                {
                    var odbiorca = drv.Row.Field<string>("Odbiorca") ?? "";
                    await WyswietlSzczegolyWydaniaBezZamowieniaAsync(odbiorca, _selectedDate);
                    return;
                }
            }

            if (TrySetAktualneIdZamowieniaFromGrid(out var id))
            {
                await WyswietlSzczegolyZamowieniaAsync(id);
            }
            else
            {
                WyczyscSzczegoly();
            }
        }

        private async Task WyswietlSzczegolyWydaniaBezZamowieniaAsync(string odbiorca, DateTime dzien)
        {
            var dt = new DataTable();
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("Wydano", typeof(decimal));

            var khId = 0;
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                var cmdKh = new SqlCommand("SELECT Id FROM [HANDEL].[SSCommon].[STContractors] WHERE Shortcut = @Odbiorca", cn);
                cmdKh.Parameters.AddWithValue("@Odbiorca", odbiorca);
                var result = await cmdKh.ExecuteScalarAsync();
                if (result != null) khId = Convert.ToInt32(result);
            }

            if (khId > 0)
            {
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"
                        SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) 
                        FROM [HANDEL].[HM].[MZ] MZ 
                        JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id 
                        JOIN [HANDEL].[HM].[TW] ON MZ.idtw = TW.id
                        WHERE MG.seria IN ('sWZ','sWZ-W') 
                          AND MG.aktywny = 1 
                          AND MG.data = @Dzien 
                          AND MG.khid = @Khid
                          AND TW.katalog = 67095
                        GROUP BY MZ.idtw";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                    cmd.Parameters.AddWithValue("@Khid", khId);
                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        int idtw = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                        decimal ilosc = SafeDecimal(rd, 1);
                        string produkt = _twKatalogCache.TryGetValue(idtw, out var kod) ? kod : $"Nieznany ({idtw})";
                        dt.Rows.Add(produkt, ilosc);
                    }
                }
            }
            txtNotatki.Text = "Wydanie bez zamówienia (tylko towary z katalogu 67095)";
            dgvSzczegoly.DataSource = dt;
            if (dgvSzczegoly.Columns["Wydano"] != null) dgvSzczegoly.Columns["Wydano"].DefaultCellStyle.Format = "N0";
        }

        private async Task WyswietlSzczegolyZamowieniaAsync(int zamowienieId)
        {
            var dtZam = new DataTable();
            int klientId = 0;
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                const string sql = @"
                    SELECT zmt.KodTowaru, zmt.Ilosc, zm.Uwagi, zm.KlientId
                    FROM [dbo].[ZamowieniaMiesoTowar] zmt
                    INNER JOIN [dbo].[ZamowieniaMieso] zm ON zm.Id = zmt.ZamowienieId
                    WHERE zmt.ZamowienieId = @Id";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", zamowienieId);
                using var da = new SqlDataAdapter(cmd);
                da.Fill(dtZam);
                if (dtZam.Rows.Count > 0 && dtZam.Columns.Contains("KlientId"))
                    klientId = dtZam.Rows[0]["KlientId"] is DBNull ? 0 : Convert.ToInt32(dtZam.Rows[0]["KlientId"]);
            }

            var wydania = new Dictionary<int, decimal>();
            if (klientId > 0)
            {
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"
                        SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                        FROM [HANDEL].[HM].[MZ] MZ
                        JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                        WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny = 1 AND MG.data = @Dzien AND MG.khid = @Khid
                        GROUP BY MZ.idtw";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Dzien", _selectedDate.Date);
                    cmd.Parameters.AddWithValue("@Khid", klientId);
                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        int idtw = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                        decimal ilosc = SafeDecimal(rd, 1);
                        wydania[idtw] = ilosc;
                    }
                }
            }

            var dt = new DataTable();
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("Zamówiono", typeof(decimal));
            dt.Columns.Add("Wydano", typeof(decimal));

            foreach (DataRow r in dtZam.Rows)
            {
                int idTowaru = r["KodTowaru"] == DBNull.Value ? 0 : Convert.ToInt32(r["KodTowaru"]);
                if (!_twKatalogCache.ContainsKey(idTowaru)) continue;

                string produkt = _twKatalogCache.TryGetValue(idTowaru, out var kod) ? kod : $"Nieznany ({idTowaru})";
                decimal zamowiono = r["Ilosc"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Ilosc"]);
                decimal wydano = wydania.TryGetValue(idTowaru, out var w) ? w : 0m;
                dt.Rows.Add(produkt, zamowiono, wydano);
                wydania.Remove(idTowaru);
            }

            foreach (var kv in wydania)
            {
                if (!_twKatalogCache.ContainsKey(kv.Key)) continue;
                string produkt = _twKatalogCache.TryGetValue(kv.Key, out var kod) ? kod : $"Nieznany ({kv.Key})";
                dt.Rows.Add(produkt, 0m, kv.Value);
            }

            string notatki = dtZam.Rows.Count > 0 ? (dtZam.Rows[0]["Uwagi"]?.ToString() ?? "") : "";
            txtNotatki.Text = notatki;
            dgvSzczegoly.DataSource = dt;

            if (dgvSzczegoly.Columns["Zamówiono"] != null)
            {
                dgvSzczegoly.Columns["Zamówiono"].DefaultCellStyle.Format = "N0";
                dgvSzczegoly.Columns["Zamówiono"].HeaderText = "Zamówiono (kg)";
            }
            if (dgvSzczegoly.Columns["Wydano"] != null)
            {
                dgvSzczegoly.Columns["Wydano"].DefaultCellStyle.Format = "N0";
                dgvSzczegoly.Columns["Wydano"].HeaderText = "Wydano (kg)";
            }
            if (dgvSzczegoly.Columns["Produkt"] != null) dgvSzczegoly.Columns["Produkt"].DisplayIndex = 0;
        }

        private async Task WyswietlAgregacjeProduktowAsync(DateTime dzien)
        {
            var dtAg = new DataTable();
            dtAg.Columns.Add("Produkt", typeof(string));
            dtAg.Columns.Add("Zamówiono", typeof(decimal));
            dtAg.Columns.Add("Wydano", typeof(decimal));
            dtAg.Columns.Add("Różnica", typeof(decimal));
            dtAg.Columns.Add("PlanowanyPrzychód", typeof(decimal));
            dtAg.Columns.Add("FaktycznyPrzychód", typeof(decimal));

            var sumaWydan = await PobierzSumeWydanPoProdukcieAsync(dzien);
            var (planPrzychodu, faktPrzychodu) = await PrognozaIFaktPrzychoduPerProduktAsync(dzien);

            var sumaZamowien = new Dictionary<int, decimal>();
            var zamowieniaIds = _dtZamowienia.AsEnumerable()
                .Where(r => !string.Equals(r.Field<string>("Status"), "Anulowane", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Field<int>("Id")).ToList();
            if (zamowieniaIds.Any())
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var sql = $"SELECT KodTowaru, SUM(Ilosc) FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId IN ({string.Join(",", zamowieniaIds)}) GROUP BY KodTowaru";
                using var cmd = new SqlCommand(sql, cn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    sumaZamowien[reader.GetInt32(0)] = ReadDecimal(reader, 1);
            }

            foreach (var towar in _twKatalogCache.OrderBy(kvp => kvp.Value))
            {
                var kod = towar.Value;
                if (IsKurczakB(kod)) continue;

                var zam = sumaZamowien.TryGetValue(towar.Key, out var z) ? z : 0m;
                var wyd = sumaWydan.TryGetValue(towar.Key, out var w) ? w : 0m;
                var diff = zam - wyd;
                var plan = planPrzychodu.TryGetValue(towar.Key, out var p) ? p : 0m;
                var fakt = faktPrzychodu.TryGetValue(towar.Key, out var f) ? f : 0m;

                dtAg.Rows.Add(kod, zam, wyd, diff, plan, fakt);
            }

            dgvAgregacja.DataSource = dtAg;
            foreach (DataGridViewColumn col in dgvAgregacja.Columns)
            {
                if (col.Name != "Produkt") col.DefaultCellStyle.Format = "N0";
            }
        }

        private void AktualizujPodsumowanieDnia()
        {
            int liczbaZamowien = 0;
            int liczbaWydanBezZamowien = 0;
            decimal sumaKgZamowiono = 0;
            decimal sumaKgWydano = 0;
            var handlowiecStat = new Dictionary<string, (int zZam, int bezZam, decimal kgZam, decimal kgWyd)>();

            if (_bsZamowienia.List is System.Collections.IEnumerable list)
            {
                foreach (var item in list)
                {
                    if (item is DataRowView drv)
                    {
                        var status = drv.Row.Field<string>("Status") ?? "";
                        var handlowiec = drv.Row.Field<string>("Handlowiec") ?? "BRAK";
                        var iloscZam = drv.Row.Field<decimal?>("IloscZamowiona") ?? 0m;
                        var iloscWyd = drv.Row.Field<decimal?>("IloscFaktyczna") ?? 0m;

                        if (!handlowiecStat.ContainsKey(handlowiec))
                            handlowiecStat[handlowiec] = (0, 0, 0, 0);

                        if (status == "Wydanie bez zamówienia")
                        {
                            liczbaWydanBezZamowien++;
                            sumaKgWydano += iloscWyd;
                            handlowiecStat[handlowiec] = (handlowiecStat[handlowiec].zZam,
                                handlowiecStat[handlowiec].bezZam + 1,
                                handlowiecStat[handlowiec].kgZam,
                                handlowiecStat[handlowiec].kgWyd + iloscWyd);
                        }
                        else if (status != "Anulowane")
                        {
                            liczbaZamowien++;
                            sumaKgZamowiono += iloscZam;
                            sumaKgWydano += iloscWyd;
                            handlowiecStat[handlowiec] = (handlowiecStat[handlowiec].zZam + 1,
                                handlowiecStat[handlowiec].bezZam,
                                handlowiecStat[handlowiec].kgZam + iloscZam,
                                handlowiecStat[handlowiec].kgWyd + iloscWyd);
                        }
                    }
                }
            }
            int suma = liczbaZamowien + liczbaWydanBezZamowien;
            string perHandlowiec = string.Join(" | ", handlowiecStat.OrderBy(h => h.Key)
                .Select(h => $"{h.Key}: {h.Value.zZam}/{h.Value.bezZam} ({h.Value.kgZam:N0}/{h.Value.kgWyd:N0}kg)"));
            lblPodsumowanie.Text = $"Suma: {suma} ({liczbaZamowien} zam. / {liczbaWydanBezZamowien} wyd.) | " +
                                  $"Zamówiono: {sumaKgZamowiono:N0} kg | Wydano: {sumaKgWydano:N0} kg | {perHandlowiec}";
        }

        private void WyczyscSzczegoly()
        {
            dgvSzczegoly.DataSource = null;
            txtNotatki.Clear();
            _aktualneIdZamowienia = null;
        }

        private async Task WczytajDanePrzychodowAsync(DateTime dzien)
        {
            var dtP = new DataTable();
            dtP.Columns.Add("Produkt", typeof(string));
            dtP.Columns.Add("Plan (kg)", typeof(decimal));
            dtP.Columns.Add("Fakt (kg)", typeof(decimal));
            dtP.Columns.Add("Różnica", typeof(decimal));

            var (plan, fakt) = await PrognozaIFaktPrzychoduPerProduktAsync(dzien);

            foreach (var p in _twKatalogCache.OrderBy(x => x.Value))
            {
                var kod = p.Value;
                var planKg = plan.TryGetValue(p.Key, out var v1) ? v1 : 0m;
                var faktKg = fakt.TryGetValue(p.Key, out var v2) ? v2 : 0m;
                var roznica = planKg - faktKg;

                dtP.Rows.Add(kod, planKg, faktKg, roznica);
            }

            dgvPrzychody.DataSource = dtP;
            if (dgvPrzychody.Columns["Plan (kg)"] != null) dgvPrzychody.Columns["Plan (kg)"].DefaultCellStyle.Format = "N0";
            if (dgvPrzychody.Columns["Fakt (kg)"] != null) dgvPrzychody.Columns["Fakt (kg)"].DefaultCellStyle.Format = "N0";
            if (dgvPrzychody.Columns["Różnica"] != null) dgvPrzychody.Columns["Różnica"].DefaultCellStyle.Format = "N0";
        }

        private async Task<Dictionary<int, decimal>> PobierzSumeWydanPoProdukcieAsync(DateTime dzien)
        {
            var sumaWydan = new Dictionary<int, decimal>();
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            const string sql = @"
                SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                FROM [HANDEL].[HM].[MZ] MZ 
                JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                WHERE MG.seria IN ('sWZ', 'sWZ-W') AND MG.aktywny=1 AND MG.data = @Dzien 
                GROUP BY MZ.idtw";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                sumaWydan[reader.GetInt32(0)] = ReadDecimal(reader, 1);
            return sumaWydan;
        }

        private async Task<(Dictionary<int, decimal> plan, Dictionary<int, decimal> fakt)> PrognozaIFaktPrzychoduPerProduktAsync(DateTime dzien)
        {
            decimal sumaMasyDek = 0m;
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                const string sql = @"
                    SELECT WagaDek, SztukiDek 
                    FROM dbo.HarmonogramDostaw 
                    WHERE DataOdbioru = @Dzien AND Bufor = 'Potwierdzony'";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var waga = ReadDecimal(rdr, 0);
                    var szt = ReadDecimal(rdr, 1);
                    sumaMasyDek += (waga * szt);
                }
            }

            decimal p_tuszka = 0.78m;
            decimal udzA = 0.85m;
            decimal udzB = 0.15m;

            decimal pula = sumaMasyDek * p_tuszka;
            decimal pulaA = pula * udzA;
            decimal pulaB = pula * udzB;

            var plan = new Dictionary<int, decimal>();
            foreach (var kv in _twKatalogCache)
            {
                var idtw = kv.Key;
                var kod = kv.Value;

                if (IsKurczakA(kod))
                {
                    plan[idtw] = pulaA;
                }
                else if (IsKurczakB(kod))
                {
                    plan[idtw] = pulaB;
                }
                else
                {
                    if (YieldByKod.TryGetValue(kod, out var share) && share > 0m)
                        plan[idtw] = Math.Max(0m, pulaB * share);
                    else
                        plan[idtw] = 0m;
                }
            }

            var fakt = new Dictionary<int, decimal>();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                const string sql = @"
                    SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) 
                    FROM [HANDEL].[HM].[MZ] MZ 
                    JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id 
                    WHERE MG.seria = 'sPWU' AND MG.aktywny=1 AND MG.data = @Dzien 
                    GROUP BY MZ.idtw";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Dzien", dzien.Date);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    fakt[rdr.GetInt32(0)] = ReadDecimal(rdr, 1);
            }
            return (plan, fakt);
        }

        private void dgvZamowienia_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var rowObj = dgvZamowienia.Rows[e.RowIndex].DataBoundItem as DataRowView;
            if (rowObj == null) return;

            var status = rowObj.Row.Table.Columns.Contains("Status")
                ? rowObj.Row["Status"]?.ToString()
                : null;

            DateTime? dataUtw = null;
            if (rowObj.Row.Table.Columns.Contains("DataUtworzenia"))
            {
                var val = rowObj.Row["DataUtworzenia"];
                if (val != DBNull.Value && val != null)
                    dataUtw = (DateTime)val;
            }

            var row = dgvZamowienia.Rows[e.RowIndex];
            if (status == "Wydanie bez zamówienia")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 240, 200);
                row.DefaultCellStyle.ForeColor = Color.Black;
                row.DefaultCellStyle.Font = new Font(dgvZamowienia.Font, FontStyle.Italic);
            }
            else if (status == "Anulowane")
            {
                row.DefaultCellStyle.ForeColor = Color.Gray;
                row.DefaultCellStyle.Font = new Font(dgvZamowienia.Font, FontStyle.Strikeout);
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 230, 230);
            }
            else if (status == "Zrealizowane")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(220, 255, 220);
                row.DefaultCellStyle.ForeColor = SystemColors.ControlText;
                row.DefaultCellStyle.Font = new Font(dgvZamowienia.Font, FontStyle.Regular);
            }
            else
            {
                row.DefaultCellStyle.ForeColor = SystemColors.ControlText;
                row.DefaultCellStyle.BackColor = (e.RowIndex % 2 == 0) ? Color.White : Color.FromArgb(248, 248, 248);
                row.DefaultCellStyle.Font = new Font(dgvZamowienia.Font, FontStyle.Regular);
            }
        }

        private bool TrySetAktualneIdZamowieniaFromGrid(out int id)
        {
            id = 0;
            if (dgvZamowienia.CurrentRow == null) return false;

            if (dgvZamowienia.CurrentRow.DataBoundItem is DataRowView rv &&
                rv.Row.Table.Columns.Contains("Id") &&
                rv.Row["Id"] != DBNull.Value)
            {
                id = Convert.ToInt32(rv.Row["Id"]);
                if (id > 0) { _aktualneIdZamowienia = id; return true; }
            }

            if (dgvZamowienia.Columns.Contains("Id"))
            {
                var cellVal = dgvZamowienia.CurrentRow.Cells["Id"]?.Value;
                if (cellVal != null && cellVal != DBNull.Value && int.TryParse(cellVal.ToString(), out id) && id > 0)
                {
                    _aktualneIdZamowienia = id;
                    return true;
                }
            }

            _aktualneIdZamowienia = null;
            return false;
        }
        #endregion

        #region Filtrowanie
        private void ZastosujFiltry()
        {
            if (_dtZamowienia.DefaultView == null) return;

            var warunki = new List<string>();

            var txt = txtFiltrujOdbiorce.Text?.Trim().Replace("'", "''");
            if (!string.IsNullOrEmpty(txt))
                warunki.Add($"Odbiorca LIKE '%{txt}%'");

            if (cbFiltrujHandlowca.SelectedIndex > 0)
            {
                var hand = cbFiltrujHandlowca.SelectedItem?.ToString()?.Replace("'", "''");
                if (!string.IsNullOrEmpty(hand))
                    warunki.Add($"Handlowiec = '{hand}'");
            }

            _dtZamowienia.DefaultView.RowFilter = string.Join(" AND ", warunki);
        }
        #endregion

        private async void btnUsun_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                MessageBox.Show("Najpierw wybierz zamówienie do usunięcia.", "Brak wyboru",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show("Czy na pewno chcesz TRWALE usunąć wybrane zamówienie? Tej operacji nie można cofnąć.",
                "Potwierdź usunięcie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    using (var cmd = new SqlCommand("DELETE FROM dbo.ZamowieniaMiesoTowar WHERE ZamowienieId = @Id", cn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    using (var cmd = new SqlCommand("DELETE FROM dbo.ZamowieniaMieso WHERE Id = @Id", cn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    MessageBox.Show("Zamówienie zostało trwale usunięte.", "Sukces",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await OdswiezWszystkieDaneAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas usuwania zamówienia: {ex.Message}", "Błąd krytyczny",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}