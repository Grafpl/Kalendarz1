// Plik: WidokZamowieniaPodsumowanie.cs
// WERSJA 9.9 – POPRAWIONA - Wszystkie MessageBox z parent=this
#nullable enable
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    // Ulepszona klasa dialogu dublowania
    public class MultipleDatePickerDialog : Form
    {
        public List<DateTime> SelectedDates { get; private set; } = new();
        public bool CopyNotes { get; private set; } = false;
        private DateTimePicker dtpStartDate;
        private DateTimePicker dtpEndDate;
        private CheckedListBox clbDays;
        private CheckBox chkCopyNotes;
        private Label lblInfo;

        public MultipleDatePickerDialog(string title)
        {
            Text = title;
            Size = new Size(400, 500);
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
            dtpEndDate.Value = DateTime.Today.AddDays(7);
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

            chkCopyNotes = new CheckBox
            {
                Text = "Kopiuj notatkę z oryginalnego zamówienia",
                Location = new Point(20, 365),
                Size = new Size(350, 25),
                Checked = false
            };

            var btnOK = new Button
            {
                Text = "Dubluj",
                DialogResult = DialogResult.OK,
                Location = new Point(80, 405),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.Click += (s, e) => {
                SelectedDates.Clear();
                foreach (DateTime date in clbDays.CheckedItems)
                    SelectedDates.Add(date);
                CopyNotes = chkCopyNotes.Checked;
            };

            var btnCancel = new Button
            {
                Text = "Anuluj",
                DialogResult = DialogResult.Cancel,
                Location = new Point(190, 405),
                Size = new Size(100, 35)
            };

            Controls.AddRange(new Control[] {
                lblStart, dtpStartDate, lblEnd, dtpEndDate,
                lblInfo, clbDays, btnSelectAll, btnDeselectAll,
                chkCopyNotes, btnOK, btnCancel
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
                clbDays.Items.Add(current, false);
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

            lblWarning = new Label
            {
                Text = "⚠️ UWAGA: Tworzysz cykliczne zamówienie dla zaznaczonego zamówienia.\n" +
                      "Pamiętaj o ostrożnym ustawianiu dat!",
                Location = new Point(20, 10),
                Size = new Size(400, 40),
                ForeColor = Color.DarkOrange,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(255, 243, 224),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(5)
            };

            var lblStart = new Label { Text = "Od dnia:", Location = new Point(20, 65), Size = new Size(60, 25) };
            dtpStart = new DateTimePicker { Location = new Point(90, 62), Size = new Size(200, 25) };
            dtpStart.Value = DateTime.Today.AddDays(1);
            dtpStart.ValueChanged += (s, e) => UpdatePreview();

            var lblEnd = new Label { Text = "Do dnia:", Location = new Point(20, 95), Size = new Size(60, 25) };
            dtpEnd = new DateTimePicker { Location = new Point(90, 92), Size = new Size(200, 25) };
            dtpEnd.Value = DateTime.Today.AddDays(7);
            dtpEnd.ValueChanged += (s, e) => UpdatePreview();

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

            lblPreview = new Label
            {
                Text = "Zostanie utworzonych: 0 zamówień",
                Location = new Point(20, 295),
                Size = new Size(400, 60),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(5),
                BackColor = Color.FromArgb(255, 253, 235)
            };

            var btnOK = new Button
            {
                Text = "Utwórz zamówienia",
                DialogResult = DialogResult.OK,
                Location = new Point(100, 370),
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(230, 126, 34),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnOK.FlatAppearance.BorderSize = 0;
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
                MessageBox.Show(this, "Data końcowa nie może być wcześniejsza niż początkowa!",
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
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnOK.FlatAppearance.BorderSize = 0;
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
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // ====== Stan UI ======
        public string UserID { get; set; } = string.Empty;
        private DateTime _selectedDate;
        private int? _aktualneIdZamowienia;
        private readonly List<Button> _dayButtons = new();
        private Button btnDodajNotatke;
        private Dictionary<int, string> _mapowanieScalowania = new(); // TowarIdtw -> NazwaGrupy
        private List<string> _grupyTowaroweNazwy = new(); // Lista nazw grup towarowych dla kolumn
        private bool _pokazujPoDatachUboju = true;
        private bool _dataUbojuKolumnaIstnieje = true;
        private bool _isInitialized = false;

        // ====== Dane i Cache ======
        private readonly DataTable _dtZamowienia = new();
        private readonly BindingSource _bsZamowienia = new();
        private readonly Dictionary<int, string> _twKodCache = new();
        private readonly Dictionary<int, string> _twKatalogCache = new();
        private readonly Dictionary<string, string> _userCache = new();
        private readonly List<string> _handlowcyCache = new();

        // ====== Kolorowanie Handlowców ======
        private readonly Dictionary<string, Color> _handlowiecColors = new Dictionary<string, Color>();
        private readonly List<Color> _palette = new List<Color>
        {
            Color.FromArgb(230, 255, 230), Color.FromArgb(230, 242, 255), Color.FromArgb(255, 240, 230),
            Color.FromArgb(230, 255, 247), Color.FromArgb(255, 230, 242), Color.FromArgb(245, 245, 220),
            Color.FromArgb(255, 228, 225), Color.FromArgb(240, 255, 255), Color.FromArgb(240, 248, 255)
        };
        private int _colorIndex = 0;

        private readonly Dictionary<string, decimal> YieldByKod = new(StringComparer.OrdinalIgnoreCase)
        {
            // {"Filet", 0.32m}, {"Ćwiartka", 0.22m}, {"Skrzydło", 0.09m}, ...
        };

        private NazwaZiD nazwaZiD = new NazwaZiD();

        private bool _pokazujWydaniaBezZamowien = true;

        #region MessageBox Helpers
        private void ShowInfo(string message, string title = "Informacja")
        {
            MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowWarning(string message, string title = "Ostrzeżenie")
        {
            MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ShowError(string message, string title = "Błąd")
        {
            MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private DialogResult ShowQuestion(string message, string title = "Pytanie")
        {
            return MessageBox.Show(this, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }

        private DialogResult ShowWarningQuestion(string message, string title = "Uwaga")
        {
            return MessageBox.Show(this, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        }
        #endregion

        #region Menu kontekstowe
        private void UtworzMenuKontekstowe()
        {
            var contextMenu = new ContextMenuStrip();

            var menuModyfikuj = new ToolStripMenuItem("✏️ Modyfikuj zamówienie");
            menuModyfikuj.Click += (s, e) => btnModyfikuj_Click(s, e);

            var menuDuplikuj = new ToolStripMenuItem("🔄 Duplikuj zamówienie");
            menuDuplikuj.Click += (s, e) => btnDuplikuj.PerformClick();

            var menuNotatka = new ToolStripMenuItem("📝 Dodaj/Edytuj notatkę");
            menuNotatka.Click += (s, e) => btnDodajNotatke_Click(s, e);

            var menuHistoriaZmian = new ToolStripMenuItem("📜 Historia zmian zamówienia");
            menuHistoriaZmian.Click += async (s, e) => await PokazHistorieZmianAsync();

            var menuAnuluj = new ToolStripMenuItem("❌ Anuluj zamówienie");
            menuAnuluj.Click += (s, e) => btnAnuluj.PerformClick();

            var menuOdswiez = new ToolStripMenuItem("🔄 Odśwież dane");
            menuOdswiez.Click += (s, e) => btnOdswiez_Click(s, e);

            contextMenu.Items.Add(menuModyfikuj);
            contextMenu.Items.Add(menuDuplikuj);
            contextMenu.Items.Add(menuNotatka);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(menuHistoriaZmian);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(menuOdswiez);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(menuAnuluj);

            dgvZamowienia.ContextMenuStrip = contextMenu;
        }

        private async Task PokazHistorieZmianAsync()
        {
            if (!_aktualneIdZamowienia.HasValue)
            {
                ShowInfo("Wybierz zamówienie z listy.", "Historia zmian");
                return;
            }

            int orderId = _aktualneIdZamowienia.Value;
            string odbiorca = "Nieznany";

            // Pobierz nazwę odbiorcy z aktualnie zaznaczonego wiersza
            if (dgvZamowienia.SelectedRows.Count > 0)
            {
                var row = dgvZamowienia.SelectedRows[0];
                if (row.Cells["Odbiorca"] != null && row.Cells["Odbiorca"].Value != null)
                {
                    odbiorca = row.Cells["Odbiorca"].Value.ToString() ?? "Nieznany";
                }
            }

            try
            {
                var historia = new System.Text.StringBuilder();
                historia.AppendLine($"📜 HISTORIA ZMIAN ZAMÓWIENIA #{orderId}");
                historia.AppendLine($"Odbiorca: {odbiorca}");
                historia.AppendLine(new string('━', 60));
                historia.AppendLine();

                using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();

                    // Sprawdź czy tabela istnieje
                    var checkSql = @"SELECT COUNT(*) FROM sys.objects
                        WHERE object_id = OBJECT_ID(N'[dbo].[HistoriaZmianZamowien]') AND type in (N'U')";
                    using var checkCmd = new SqlCommand(checkSql, cn);
                    var tableExists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                    if (!tableExists)
                    {
                        ShowInfo("Brak zapisanej historii zmian dla tego zamówienia.\n\n" +
                            "Historia zmian będzie dostępna po wprowadzeniu pierwszych zmian.", "Historia zmian");
                        return;
                    }

                    var sql = @"
                        SELECT
                            TypZmiany,
                            PoleZmienione,
                            WartoscPoprzednia,
                            WartoscNowa,
                            ISNULL(UzytkownikNazwa, Uzytkownik) as Uzytkownik,
                            DataZmiany,
                            OpisZmiany
                        FROM HistoriaZmianZamowien
                        WHERE ZamowienieId = @ZamowienieId
                        ORDER BY DataZmiany DESC";

                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@ZamowienieId", orderId);

                    using var reader = await cmd.ExecuteReaderAsync();
                    int licznik = 0;

                    while (await reader.ReadAsync())
                    {
                        licznik++;
                        string typZmiany = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        string? poleZmienione = reader.IsDBNull(1) ? null : reader.GetString(1);
                        string? wartoscPoprzednia = reader.IsDBNull(2) ? null : reader.GetString(2);
                        string? wartoscNowa = reader.IsDBNull(3) ? null : reader.GetString(3);
                        string uzytkownik = reader.IsDBNull(4) ? "Nieznany" : reader.GetString(4);
                        DateTime dataZmiany = reader.GetDateTime(5);
                        string? opisZmiany = reader.IsDBNull(6) ? null : reader.GetString(6);

                        string ikona = typZmiany switch
                        {
                            "UTWORZENIE" => "➕",
                            "EDYCJA" => "✏️",
                            "ANULOWANIE" => "❌",
                            "PRZYWROCENIE" => "✅",
                            "USUNIECIE" => "🗑️",
                            _ => "📝"
                        };

                        historia.AppendLine($"{ikona} {dataZmiany:yyyy-MM-dd HH:mm} | {uzytkownik}");

                        if (!string.IsNullOrEmpty(opisZmiany))
                        {
                            historia.AppendLine($"   {opisZmiany}");
                        }
                        else if (!string.IsNullOrEmpty(poleZmienione))
                        {
                            historia.AppendLine($"   {poleZmienione}: '{wartoscPoprzednia ?? "(puste)"}' → '{wartoscNowa ?? "(puste)"}'");
                        }
                        else
                        {
                            historia.AppendLine($"   {typZmiany}");
                        }
                        historia.AppendLine();
                    }

                    if (licznik == 0)
                    {
                        historia.AppendLine("Brak zapisanych zmian dla tego zamówienia.");
                    }
                    else
                    {
                        historia.AppendLine(new string('━', 60));
                        historia.AppendLine($"Łącznie: {licznik} zmian");
                    }
                }

                ShowInfo(historia.ToString(), $"Historia zmian - Zamówienie #{orderId}");
            }
            catch (Exception ex)
            {
                ShowError($"Błąd podczas pobierania historii zmian:\n{ex.Message}", "Błąd");
            }
        }
        #endregion

        public WidokZamowieniaPodsumowanie()
        {
            InitializeComponent();
            Load += WidokZamowieniaPodsumowanie_Load;

            // Dodanie menu kontekstowego do gridu zamówień
            UtworzMenuKontekstowe();

            if (btnDodajNotatke == null)
            {
                btnDodajNotatke = new Button();
                btnDodajNotatke.Name = "btnDodajNotatke";
                btnDodajNotatke.Text = "Notatka";
                btnDodajNotatke.Click += btnDodajNotatke_Click;
                panelNawigacja.Controls.Add(btnDodajNotatke);
            }

            chkPokazWydaniaBezZamowien.Checked = _pokazujWydaniaBezZamowien;

            btnUsun.Visible = false;
            btnCykliczne.Visible = true;
        }

        private async void WidokZamowieniaPodsumowanie_Load(object? sender, EventArgs e)
        {
            if (_isInitialized) return;
            _isInitialized = true;

            UstawPrzyciskiDniTygodnia();
            ApplyModernUI();

            this.SizeChanged += (s, ev) => ApplyModernUI();

            SzybkiGrid(dgvZamowienia);
            SzybkiGrid(dgvSzczegoly);
            SzybkiGrid(dgvAgregacja);

            // Menu kontekstowe dla podsumowania dnia (prawy przycisk myszy)
            InicjalizujMenuKontekstoweAgregacji();

            AttachGridEventHandlers();

            btnUsun.Visible = (UserID == "11111");
            btnCykliczne.Visible = true;

            await ZaladujDanePoczatkoweAsync();
            await ZaladujMapowanieScalowaniaAsync();

            _selectedDate = DateTime.Today;
            AktualizujDatyPrzyciskow();
            await OdswiezWszystkieDaneAsync();
        }

        private void ChkPokazWydaniaBezZamowien_CheckedChanged(object sender, EventArgs e)
        {
            _pokazujWydaniaBezZamowien = chkPokazWydaniaBezZamowien.Checked;
            ZastosujFiltry();
            AktualizujPodsumowanieDnia();
        }

        private void AttachGridEventHandlers()
        {
            dgvZamowienia.SelectionChanged -= dgvZamowienia_SelectionChanged;
            dgvZamowienia.CellClick -= dgvZamowienia_CellClick;
            dgvZamowienia.CellDoubleClick -= dgvZamowienia_CellDoubleClick;
            dgvZamowienia.CellFormatting -= dgvZamowienia_CellFormatting;
            dgvZamowienia.RowPrePaint -= dgvZamowienia_RowPrePaint;

            dgvSzczegoly.CellFormatting -= dgvSzczegoly_CellFormatting;

            dgvZamowienia.SelectionChanged += dgvZamowienia_SelectionChanged;
            dgvZamowienia.CellClick += dgvZamowienia_CellClick;
            dgvZamowienia.CellDoubleClick += dgvZamowienia_CellDoubleClick;
            dgvZamowienia.CellFormatting += dgvZamowienia_CellFormatting;
            dgvZamowienia.RowPrePaint += dgvZamowienia_RowPrePaint;

            dgvSzczegoly.CellFormatting += dgvSzczegoly_CellFormatting;
        }

        private void ApplyModernUI()
        {
            this.BackColor = Color.FromArgb(245, 247, 250);

            panelNawigacja.BackColor = Color.White;
            panelNawigacja.Paint += (s, e) =>
            {
                using (var brush = new LinearGradientBrush(
                    new Rectangle(0, panelNawigacja.Height - 3, panelNawigacja.Width, 3),
                    Color.FromArgb(40, Color.Black),
                    Color.Transparent,
                    LinearGradientMode.Vertical))
                {
                    e.Graphics.FillRectangle(brush, 0, panelNawigacja.Height - 3, panelNawigacja.Width, 3);
                }
            };

            bool isCompact = this.Width < 1400;
            int btnWidth = isCompact ? 75 : 95;
            int btnHeight = isCompact ? 35 : 40;
            Font btnFont = new Font("Segoe UI", isCompact ? 8f : 9f, FontStyle.Bold);

            StyleActionButton(btnNoweZamowienie, Color.FromArgb(46, 204, 113), isCompact ? "+ Nowe" : "+ Nowe", btnWidth, btnHeight, btnFont);
            StyleActionButton(btnModyfikuj, Color.FromArgb(52, 152, 219), isCompact ? "Modyfikuj" : "✏ Modyfikuj", btnWidth, btnHeight, btnFont);
            StyleActionButton(btnDuplikuj, Color.FromArgb(155, 89, 182), isCompact ? "Duplikuj" : "⧉ Duplikuj", btnWidth, btnHeight, btnFont);
            StyleActionButton(btnCykliczne, Color.FromArgb(230, 126, 34), isCompact ? "Cykliczne" : "⟲ Cykliczne", btnWidth, btnHeight, btnFont);
            StyleActionButton(btnDodajNotatke, Color.FromArgb(241, 196, 15), isCompact ? "Notatka" : "✎ Notatka", btnWidth, btnHeight, btnFont);
            StyleActionButton(btnAnuluj, Color.FromArgb(231, 76, 60), isCompact ? "Anuluj" : "✕ Anuluj", btnWidth, btnHeight, btnFont);
            StyleActionButton(btnOdswiez, Color.FromArgb(149, 165, 166), isCompact ? "Odśwież" : "⟲ Odśwież", btnWidth, btnHeight, btnFont);

            if (btnUsun.Visible)
            {
                StyleActionButton(btnUsun, Color.FromArgb(44, 62, 80), isCompact ? "Usuń" : "✕ Usuń", btnWidth, btnHeight, btnFont);
            }

            btnTydzienPrev.Location = new Point(10, 12);
            btnTydzienPrev.Size = new Size(40, 40);

            lblZakresDat.Location = new Point(55, 9);
            lblZakresDat.Size = new Size(90, 49);
            lblZakresDat.TextAlign = ContentAlignment.MiddleCenter;
            lblZakresDat.Font = new Font("Segoe UI", 8f, FontStyle.Regular);

            btnTydzienNext.Location = new Point(150, 12);
            btnTydzienNext.Size = new Size(40, 40);

            panelDni.Width = 560;
            panelDni.Height = 52;
            panelDni.Location = new Point(200, 6);
            panelDni.AutoSize = false;

            int dayButtonWidth = 75;
            int dayButtonHeight = 45;
            int daySpacing = 5;

            if (_dayButtons.Count >= 7)
            {
                for (int i = 0; i < 7; i++)
                {
                    var btn = _dayButtons[i];
                    btn.Size = new Size(dayButtonWidth, dayButtonHeight);
                    btn.Location = new Point(i * (dayButtonWidth + daySpacing), 3);
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderSize = 0;
                    btn.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                    btn.Cursor = Cursors.Hand;
                    btn.Visible = true;
                }
            }

            int spacing = 5;
            int currentX = panelNawigacja.Width - btnWidth - 10;

            btnOdswiez.Location = new Point(currentX, 12);
            btnOdswiez.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            currentX -= (btnWidth + spacing);

            btnAnuluj.Location = new Point(currentX, 12);
            btnAnuluj.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            currentX -= (btnWidth + spacing);

            if (btnDodajNotatke != null)
            {
                btnDodajNotatke.Location = new Point(currentX, 12);
                btnDodajNotatke.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                currentX -= (btnWidth + spacing);
            }

            btnCykliczne.Location = new Point(currentX, 12);
            btnCykliczne.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            currentX -= (btnWidth + spacing);

            btnDuplikuj.Location = new Point(currentX, 12);
            btnDuplikuj.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            currentX -= (btnWidth + spacing);

            btnModyfikuj.Location = new Point(currentX, 12);
            btnModyfikuj.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            currentX -= (btnWidth + spacing);

            btnNoweZamowienie.Location = new Point(currentX, 12);
            btnNoweZamowienie.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            currentX -= (btnWidth + spacing);

            if (btnUsun.Visible)
            {
                btnUsun.Location = new Point(currentX, 12);
                btnUsun.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            }

            panelFiltry.BackColor = Color.FromArgb(236, 240, 241);
            txtFiltrujOdbiorce.BorderStyle = BorderStyle.FixedSingle;
            cbFiltrujHandlowca.FlatStyle = FlatStyle.Flat;
            cbFiltrujTowar.FlatStyle = FlatStyle.Flat;

            panelPodsumowanie.BackColor = Color.FromArgb(44, 62, 80);
            lblPodsumowanie.ForeColor = Color.White;
            lblPodsumowanie.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            txtNotatki.Font = new Font("Segoe UI", 14f, FontStyle.Regular);
        }

        private void StyleActionButton(Button btn, Color color, string text, int width = 95, int height = 40, Font? font = null)
        {
            btn.Text = text;
            btn.BackColor = color;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = font ?? new Font("Segoe UI", 9f, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            btn.Size = new Size(width, height);

            btn.MouseEnter -= BtnMouseEnter;
            btn.MouseLeave -= BtnMouseLeave;

            btn.MouseEnter += BtnMouseEnter;
            btn.MouseLeave += BtnMouseLeave;
            btn.Tag = color;
        }

        private void BtnMouseEnter(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.Tag is Color color)
            {
                btn.BackColor = ControlPaint.Light(color, 0.1f);
            }
        }

        private void BtnMouseLeave(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.Tag is Color color)
            {
                btn.BackColor = color;
            }
        }

        #region Helpers
        private static string SafeString(IDataRecord r, int i) => r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i)) ?? string.Empty;
        private static int? SafeInt32N(IDataRecord r, int i) => r.IsDBNull(i) ? (int?)null : Convert.ToInt32(r.GetValue(i));
        private static DateTime? SafeDateTimeN(IDataRecord r, int i) => r.IsDBNull(i) ? (DateTime?)null : Convert.ToDateTime(r.GetValue(i));
        private static decimal SafeDecimal(IDataRecord r, int i) => r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i));
        private static object DbOrNull(DateTime? dt) => dt.HasValue ? dt.Value : DBNull.Value;
        private static object DbOrNull(object? v) => v ?? DBNull.Value;
        private static decimal ReadDecimal(IDataRecord r, int i) => r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i));
        private static string AsString(IDataRecord r, int i) => r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i)) ?? "";
        private static bool IsKurczakB(string kod) => kod.IndexOf("Kurczak B", StringComparison.OrdinalIgnoreCase) >= 0;
        private static bool IsKurczakA(string kod) => kod.IndexOf("Kurczak A", StringComparison.OrdinalIgnoreCase) >= 0;

        private Color GetColorForHandlowiec(string handlowiec)
        {
            if (string.IsNullOrEmpty(handlowiec))
                return Color.White;

            if (!_handlowiecColors.ContainsKey(handlowiec))
            {
                _handlowiecColors[handlowiec] = _palette[_colorIndex % _palette.Count];
                _colorIndex++;
            }
            return _handlowiecColors[handlowiec];
        }
        #endregion

        #region Inicjalizacja i UI
        private void UstawPrzyciskiDniTygodnia()
        {
            _dayButtons.Clear();
            _dayButtons.AddRange(new[] { btnPon, btnWt, btnSr, btnCzw, btnPt, btnSo, btnNd });

            foreach (var btn in _dayButtons)
            {
                if (btn != null)
                {
                    btn.Click -= DzienButton_Click;
                    btn.Click += DzienButton_Click;
                }
            }

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
            dgv.BackgroundColor = Color.White;
            dgv.BorderStyle = BorderStyle.None;
            dgv.RowTemplate.Height = 30;
            dgv.Font = new Font("Segoe UI", 9f);
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 73, 94);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.EnableHeadersVisualStyles = false;
            dgv.ReadOnly = true;

            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;

            TryEnableDoubleBuffer(dgv);
        }

        private static void TryEnableDoubleBuffer(Control c)
        {
            try
            {
                var pi = c.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
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
            int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
            _selectedDate = _selectedDate.AddDays(-delta);
            AktualizujDatyPrzyciskow();
            await OdswiezWszystkieDaneAsync();
        }

        private async void btnTydzienNext_Click(object? sender, EventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(7);
            int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
            _selectedDate = _selectedDate.AddDays(-delta);
            AktualizujDatyPrzyciskow();
            await OdswiezWszystkieDaneAsync();
        }

        private void AktualizujDatyPrzyciskow()
        {
            int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
            DateTime startOfWeek = _selectedDate.AddDays(-delta);

            lblZakresDat.Text = $"{startOfWeek:dd.MM.yyyy}\n{startOfWeek.AddDays(6):dd.MM.yyyy}";
            lblZakresDat.TextAlign = ContentAlignment.MiddleCenter;

            string[] dniNazwy = { "Pon", "Wt", "Śr", "Czw", "Pt", "So", "Nd" };

            for (int i = 0; i < Math.Min(7, _dayButtons.Count); i++)
            {
                var dt = startOfWeek.AddDays(i);
                var btn = _dayButtons[i];

                if (btn == null) continue;

                btn.Tag = dt;
                btn.Text = $"{dniNazwy[i]}\n{dt:dd.MM}";
                btn.Visible = true;

                if (dt.Date == _selectedDate.Date)
                {
                    btn.BackColor = Color.FromArgb(52, 152, 219);
                    btn.ForeColor = Color.White;
                }
                else if (dt.Date == DateTime.Today)
                {
                    btn.BackColor = Color.FromArgb(241, 196, 15);
                    btn.ForeColor = Color.White;
                }
                else
                {
                    btn.BackColor = Color.FromArgb(236, 240, 241);
                    btn.ForeColor = Color.FromArgb(44, 62, 80);
                }
            }
        }

        private async void btnOdswiez_Click(object? sender, EventArgs e)
        {
            await OdswiezWszystkieDaneAsync();
        }

        private async void btnNoweZamowienie_Click(object? sender, EventArgs e)
        {
            var widokZamowienia = new WidokZamowienia(UserID, null);
            if (widokZamowienia.ShowDialog(this) == DialogResult.OK)
            {
                await OdswiezWszystkieDaneAsync();
            }
        }

        private async void btnModyfikuj_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                ShowInfo("Najpierw kliknij wiersz z zamówieniem, aby je wybrać.", "Brak wyboru");
                return;
            }

            var widokZamowienia = new WidokZamowienia(UserID, id);
            if (widokZamowienia.ShowDialog(this) == DialogResult.OK)
            {
                await OdswiezWszystkieDaneAsync();
            }
        }

        private async void btnDodajNotatke_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                ShowInfo("Najpierw wybierz zamówienie, do którego chcesz dodać notatkę.", "Brak wyboru");
                return;
            }

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
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    var cmd = new SqlCommand("UPDATE ZamowieniaMieso SET Uwagi = @Uwagi WHERE Id = @Id", cn);
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Uwagi", string.IsNullOrEmpty(dlg.Notatka) ? DBNull.Value : dlg.Notatka);
                    await cmd.ExecuteNonQueryAsync();

                    ShowInfo("Notatka została zapisana.", "Sukces");
                    await WyswietlSzczegolyZamowieniaAsync(id);
                    await OdswiezWszystkieDaneAsync();
                }
                catch (Exception ex)
                {
                    ShowError($"Błąd podczas zapisywania notatki: {ex.Message}");
                }
            }
        }

        private async void btnAnuluj_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                ShowInfo("Najpierw kliknij wiersz z zamówieniem, które chcesz anulować.", "Brak wyboru");
                return;
            }

            var result = ShowWarningQuestion("Czy na pewno chcesz anulować wybrane zamówienie? Tej operacji nie można cofnąć.", "Potwierdź anulowanie");
            if (result == DialogResult.Yes)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    await using var cmd = new SqlCommand("UPDATE dbo.ZamowieniaMieso SET Status = 'Anulowane' WHERE Id = @Id", cn);
                    cmd.Parameters.AddWithValue("@Id", _aktualneIdZamowienia.Value);
                    await cmd.ExecuteNonQueryAsync();

                    ShowInfo("Zamówienie zostało anulowane.", "Sukces");
                    await OdswiezWszystkieDaneAsync();
                }
                catch (Exception ex)
                {
                    ShowError($"Wystąpił błąd podczas anulowania zamówienia: {ex.Message}", "Błąd krytyczny");
                }
            }
        }

        private async void Filtry_Changed(object? sender, EventArgs e)
        {
            if (sender == cbFiltrujTowar)
            {
                await OdswiezWszystkieDaneAsync();
                return;
            }

            ZastosujFiltry();
            AktualizujPodsumowanieDnia();
        }

        private async void btnDuplikuj_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                ShowInfo("Najpierw wybierz zamówienie do duplikacji.", "Brak wyboru");
                return;
            }

            using var dlg = new MultipleDatePickerDialog("Wybierz dni dla duplikatu zamówienia");
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedDates.Any())
            {
                try
                {
                    int utworzono = 0;
                    foreach (var date in dlg.SelectedDates)
                    {
                        await DuplikujZamowienie(id, date, dlg.CopyNotes);
                        utworzono++;
                    }

                    ShowInfo($"Zamówienie zostało zduplikowane na {utworzono} dni.\n" +
                                  $"Od {dlg.SelectedDates.Min():yyyy-MM-dd} do {dlg.SelectedDates.Max():yyyy-MM-dd}",
                        "Sukces");

                    _selectedDate = dlg.SelectedDates.First();
                    AktualizujDatyPrzyciskow();
                    await OdswiezWszystkieDaneAsync();
                }
                catch (Exception ex)
                {
                    ShowError($"Błąd podczas duplikowania: {ex.Message}");
                }
            }
        }

        private async Task DuplikujZamowienie(int sourceId, DateTime targetDate, bool copyNotes = false)
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var tr = cn.BeginTransaction();

            try
            {
                int klientId = 0;
                string uwagi = "";
                DateTime godzinaPrzyjazdu = DateTime.Today.AddHours(8);
                int liczbaPojemnikow = 0;
                decimal liczbaPalet = 0m;
                bool trybE2 = false;

                using (var cmd = new SqlCommand(@"SELECT KlientId, Uwagi, DataPrzyjazdu, LiczbaPojemnikow, LiczbaPalet, TrybE2 FROM ZamowieniaMieso WHERE Id = @Id", cn, tr))
                {
                    cmd.Parameters.AddWithValue("@Id", sourceId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        klientId = reader.GetInt32(0);
                        uwagi = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        godzinaPrzyjazdu = reader.GetDateTime(2);
                        godzinaPrzyjazdu = targetDate.Date.Add(godzinaPrzyjazdu.TimeOfDay);
                        liczbaPojemnikow = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        liczbaPalet = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4);
                        trybE2 = reader.IsDBNull(5) ? false : reader.GetBoolean(5);
                    }
                }

                var cmdGetId = new SqlCommand("SELECT ISNULL(MAX(Id),0)+1 FROM ZamowieniaMieso", cn, tr);
                int newId = Convert.ToInt32(await cmdGetId.ExecuteScalarAsync());

                var cmdInsert = new SqlCommand(@"INSERT INTO ZamowieniaMieso (Id, DataZamowienia, DataPrzyjazdu, KlientId, Uwagi, IdUser, DataUtworzenia, LiczbaPojemnikow, LiczbaPalet, TrybE2, TransportStatus) VALUES (@id, @dz, @dp, @kid, @uw, @u, GETDATE(), @poj, @pal, @e2, 'Oczekuje')", cn, tr);
                cmdInsert.Parameters.AddWithValue("@id", newId);
                cmdInsert.Parameters.AddWithValue("@dz", targetDate.Date);
                cmdInsert.Parameters.AddWithValue("@dp", godzinaPrzyjazdu);
                cmdInsert.Parameters.AddWithValue("@kid", klientId);

                string finalNotes = copyNotes && !string.IsNullOrEmpty(uwagi) ? uwagi : "";
                cmdInsert.Parameters.AddWithValue("@uw", string.IsNullOrEmpty(finalNotes) ? DBNull.Value : finalNotes);

                cmdInsert.Parameters.AddWithValue("@u", UserID);
                cmdInsert.Parameters.AddWithValue("@poj", liczbaPojemnikow);
                cmdInsert.Parameters.AddWithValue("@pal", liczbaPalet);
                cmdInsert.Parameters.AddWithValue("@e2", trybE2);
                await cmdInsert.ExecuteNonQueryAsync();

                var cmdCopyItems = new SqlCommand(@"INSERT INTO ZamowieniaMiesoTowar (ZamowienieId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2) SELECT @newId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2 FROM ZamowieniaMiesoTowar WHERE ZamowienieId = @sourceId", cn, tr);
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
                ShowInfo("Najpierw wybierz zamówienie wzorcowe dla cyklu.", "Brak wyboru");
                return;
            }

            using var dlg = new CykliczneZamowieniaDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    int utworzono = 0;
                    foreach (var date in dlg.SelectedDays)
                    {
                        await DuplikujZamowienie(id, date, false);
                        utworzono++;
                    }

                    ShowInfo($"Utworzono {utworzono} zamówień cyklicznych.\n" +
                                  $"Od {dlg.StartDate:yyyy-MM-dd} do {dlg.EndDate:yyyy-MM-dd}",
                                  "Sukces");

                    await OdswiezWszystkieDaneAsync();
                }
                catch (Exception ex)
                {
                    ShowError($"Błąd podczas tworzenia zamówień cyklicznych: {ex.Message}");
                }
            }
        }
        #endregion

        #region Sprawdzanie i tworzenie kolumn bazy danych
        private async Task SprawdzIUtworzKolumneDataUboju()
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string checkSql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'DataUboju'";

                await using var cmdCheck = new SqlCommand(checkSql, cn);
                int count = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());

                if (count == 0)
                {
                    const string alterSql = @"ALTER TABLE [dbo].[ZamowieniaMieso] ADD DataUboju DATE NULL";

                    await using var cmdAlter = new SqlCommand(alterSql, cn);
                    await cmdAlter.ExecuteNonQueryAsync();

                    _dataUbojuKolumnaIstnieje = true;
                    ShowInfo("Kolumna 'DataUboju' została dodana do bazy danych.", "Aktualizacja bazy");
                }
                else
                {
                    _dataUbojuKolumnaIstnieje = true;
                }
            }
            catch (Exception ex)
            {
                _dataUbojuKolumnaIstnieje = false;
                ShowWarning($"Nie można dodać kolumny DataUboju do bazy danych.\n" +
                               $"Funkcja filtrowania po dacie uboju będzie niedostępna.\n\n" +
                               $"Błąd: {ex.Message}\n\n" +
                               $"Aby włączyć tę funkcję, wykonaj SQL:\n" +
                               $"ALTER TABLE [dbo].[ZamowieniaMieso] ADD DataUboju DATE NULL");

                if (rbDataUboju != null)
                {
                    rbDataUboju.Enabled = false;
                    rbDataUboju.Text = "Data uboju (niedostępne)";
                }
            }
        }
        #endregion

        #region Wczytywanie i przetwarzanie
        private async Task OdswiezWszystkieDaneAsync()
        {
            try
            {
                await WczytajZamowieniaDlaDniaAsync(_selectedDate);
                await WyswietlAgregacjeProduktowAsync(_selectedDate);
                AktualizujPodsumowanieDnia();

                if (_aktualneIdZamowienia.HasValue && _aktualneIdZamowienia.Value > 0)
                {
                    await WyswietlSzczegolyZamowieniaAsync(_aktualneIdZamowienia.Value);
                }
                else
                {
                    WyczyscSzczegoly();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Błąd podczas odświeżania danych: {ex.Message}\n\nSTACKTRACE:\n{ex.StackTrace}\n\nINNER: {ex.InnerException}", "Błąd Krytyczny");
            }
        }

        private async Task ZaladujDanePoczatkoweAsync()
        {
            await SprawdzIUtworzKolumneDataUboju();

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
                    bool wKatalogu = false;
                    if (!(katObj is DBNull))
                    {
                        if (katObj is int ki)
                            wKatalogu = (ki == 67095 || ki == 67153);
                        else
                        {
                            string katStr = Convert.ToString(katObj);
                            wKatalogu = (katStr == "67095" || katStr == "67153");
                        }
                    }
                    _twKodCache[idtw] = kod;
                    if (wKatalogu)
                        _twKatalogCache[idtw] = kod;
                }
            }

            var listaTowarow = _twKatalogCache.OrderBy(x => x.Value).Select(k => new KeyValuePair<int, string>(k.Key, k.Value)).ToList();
            listaTowarow.Insert(0, new KeyValuePair<int, string>(0, "— Wszystkie towary —"));

            cbFiltrujTowar.SelectedIndexChanged -= Filtry_Changed;
            cbFiltrujTowar.DataSource = new BindingSource(listaTowarow, null);
            cbFiltrujTowar.DisplayMember = "Value";
            cbFiltrujTowar.ValueMember = "Key";
            cbFiltrujTowar.SelectedIndex = 0;
            cbFiltrujTowar.SelectedIndexChanged += Filtry_Changed;

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
                await using var cmd = new SqlCommand(@"SELECT DISTINCT CDim_Handlowiec_Val FROM [HANDEL].[SSCommon].[ContractorClassification] WHERE CDim_Handlowiec_Val IS NOT NULL ORDER BY 1", cn3);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var val = AsString(reader, 0);
                    if (!string.IsNullOrWhiteSpace(val))
                        _handlowcyCache.Add(val);
                }
            }

            cbFiltrujHandlowca.SelectedIndexChanged -= Filtry_Changed;
            cbFiltrujHandlowca.Items.Clear();
            cbFiltrujHandlowca.Items.Add("— Wszyscy —");
            cbFiltrujHandlowca.Items.AddRange(_handlowcyCache.ToArray());
            cbFiltrujHandlowca.SelectedIndex = 0;
            cbFiltrujHandlowca.SelectedIndexChanged += Filtry_Changed;

            txtFiltrujOdbiorce.TextChanged -= Filtry_Changed;
            txtFiltrujOdbiorce.TextChanged += Filtry_Changed;

            if (rbDataOdbioru != null && rbDataUboju != null)
            {
                rbDataOdbioru.CheckedChanged -= RbDataOdbioru_CheckedChanged;
                rbDataUboju.CheckedChanged -= RbDataUboju_CheckedChanged;

                rbDataUboju.Enabled = _dataUbojuKolumnaIstnieje;
                if (!_dataUbojuKolumnaIstnieje)
                {
                    rbDataUboju.Text = "Data uboju (niedostępne)";
                    rbDataOdbioru.Checked = true;
                }
                else
                {
                    rbDataUboju.Checked = true;
                }

                rbDataOdbioru.CheckedChanged += RbDataOdbioru_CheckedChanged;
                rbDataUboju.CheckedChanged += RbDataUboju_CheckedChanged;
            }
        }

        private async void RbDataOdbioru_CheckedChanged(object? sender, EventArgs e)
        {
            if (rbDataOdbioru.Checked)
            {
                _pokazujPoDatachUboju = false;
                await OdswiezWszystkieDaneAsync();
            }
        }

        private async void RbDataUboju_CheckedChanged(object? sender, EventArgs e)
        {
            if (rbDataUboju.Checked && _dataUbojuKolumnaIstnieje)
            {
                _pokazujPoDatachUboju = true;
                await OdswiezWszystkieDaneAsync();
            }
            else if (rbDataUboju.Checked && !_dataUbojuKolumnaIstnieje)
            {
                rbDataOdbioru.Checked = true;
                ShowInfo("Kolumna DataUboju nie istnieje w bazie danych.\n" +
                               "Filtrowanie po dacie uboju jest niedostępne.",
                               "Funkcja niedostępna");
            }
        }

        private async Task WczytajZamowieniaDlaDniaAsync(DateTime dzien)
        {
            // Zawsze czyść i odtwarzaj kolumny - grupy mogą się zmienić
            _dtZamowienia.Clear();
            _dtZamowienia.Columns.Clear();

            // Podstawowe kolumny
            _dtZamowienia.Columns.Add("Id", typeof(int));
            _dtZamowienia.Columns.Add("KlientId", typeof(int));
            _dtZamowienia.Columns.Add("Odbiorca", typeof(string));
            _dtZamowienia.Columns.Add("Handlowiec", typeof(string));
            _dtZamowienia.Columns.Add("IloscZamowiona", typeof(decimal));
            _dtZamowienia.Columns.Add("IloscFaktyczna", typeof(decimal));
            _dtZamowienia.Columns.Add("Pojemniki", typeof(int));
            _dtZamowienia.Columns.Add("Palety", typeof(decimal));
            _dtZamowienia.Columns.Add("TrybE2", typeof(string));
            _dtZamowienia.Columns.Add("DataPrzyjecia", typeof(DateTime));
            _dtZamowienia.Columns.Add("GodzinaPrzyjecia", typeof(string));
            _dtZamowienia.Columns.Add("TerminOdbioru", typeof(string));
            _dtZamowienia.Columns.Add("DataUboju", typeof(DateTime));
            _dtZamowienia.Columns.Add("UtworzonePrzez", typeof(string));
            _dtZamowienia.Columns.Add("Status", typeof(string));
            _dtZamowienia.Columns.Add("MaNotatke", typeof(bool));

            // Dynamiczne kolumny dla grup towarowych
            foreach (var grupaName in _grupyTowaroweNazwy)
            {
                _dtZamowienia.Columns.Add($"Grupa_{grupaName}", typeof(decimal));
            }

            var kontrahenci = new Dictionary<int, (string Nazwa, string Handlowiec)>();
            await using (var cnHandel = new SqlConnection(_connHandel))
            {
                await cnHandel.OpenAsync();
                const string sqlKontr = @"SELECT c.Id, c.Shortcut, wym.CDim_Handlowiec_Val 
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

            var klienciZamowien = new HashSet<int>();
            await using (var cnLibra = new SqlConnection(_connLibra))
            {
                await cnLibra.OpenAsync();
                string dataKolumnaDoSprawdzenia = (_pokazujPoDatachUboju && _dataUbojuKolumnaIstnieje) ? "DataUboju" : "DataZamowienia";
                string sqlKlienci = $"SELECT DISTINCT KlientId FROM [dbo].[ZamowieniaMieso] WHERE {dataKolumnaDoSprawdzenia} = @Dzien AND Status <> 'Anulowane' AND KlientId IS NOT NULL";
                await using var cmdKlienci = new SqlCommand(sqlKlienci, cnLibra);
                cmdKlienci.Parameters.AddWithValue("@Dzien", dzien.Date);
                await using var readerKlienci = await cmdKlienci.ExecuteReaderAsync();
                while (await readerKlienci.ReadAsync())
                {
                    klienciZamowien.Add(readerKlienci.GetInt32(0));
                }
            }

            if (_twKatalogCache.Keys.Any())
            {
                await using (var cnLibra = new SqlConnection(_connLibra))
                {
                    await cnLibra.OpenAsync();
                    var idwList = string.Join(",", _twKatalogCache.Keys);
                    string dataKolumna = (_pokazujPoDatachUboju && _dataUbojuKolumnaIstnieje) ? "zm.DataUboju" : "zm.DataZamowienia";
                    string dataUbojuSelect = _dataUbojuKolumnaIstnieje ? ", zm.DataUboju" : "";
                    string dataUbojuGroupBy = _dataUbojuKolumnaIstnieje ? ", zm.DataUboju" : "";

                    string sql = $@"
                SELECT zm.Id, zm.KlientId, SUM(ISNULL(zmt.Ilosc,0)) AS Ilosc, 
                       zm.DataPrzyjazdu, zm.DataUtworzenia, zm.IdUser, zm.Status,
                       zm.LiczbaPojemnikow, zm.LiczbaPalet, zm.TrybE2, zm.Uwagi{dataUbojuSelect}
                FROM [dbo].[ZamowieniaMieso] zm
                JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
                WHERE {dataKolumna} = @Dzien AND zmt.KodTowaru IN ({idwList}) AND zm.Status <> 'Anulowane' " +
                        (selectedProductId.HasValue ? "AND zmt.KodTowaru = @TowarId " : "") +
                        $@"GROUP BY zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.DataUtworzenia, zm.IdUser, zm.Status,
                         zm.LiczbaPojemnikow, zm.LiczbaPalet, zm.TrybE2, zm.Uwagi{dataUbojuGroupBy}
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

            // Pobierz sumy per zamówienie per grupa towarowa
            var sumaPerZamowieniePerGrupa = new Dictionary<int, Dictionary<string, decimal>>();
            if (_grupyTowaroweNazwy.Any() && temp.Rows.Count > 0)
            {
                var zamowieniaIds = temp.AsEnumerable().Select(r => Convert.ToInt32(r["Id"])).Where(id => id > 0).ToList();
                if (zamowieniaIds.Any())
                {
                    await using var cnLibraGrupy = new SqlConnection(_connLibra);
                    await cnLibraGrupy.OpenAsync();
                    var sqlGrupy = $"SELECT ZamowienieId, KodTowaru, SUM(Ilosc) AS Ilosc FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId IN ({string.Join(",", zamowieniaIds)}) GROUP BY ZamowienieId, KodTowaru";
                    await using var cmdGrupy = new SqlCommand(sqlGrupy, cnLibraGrupy);
                    await using var readerGrupy = await cmdGrupy.ExecuteReaderAsync();
                    while (await readerGrupy.ReadAsync())
                    {
                        int zamId = readerGrupy.GetInt32(0);
                        int kodTowaru = readerGrupy.GetInt32(1);
                        decimal iloscTowaru = ReadDecimal(readerGrupy, 2);

                        // Znajdź grupę dla tego towaru
                        if (_mapowanieScalowania.TryGetValue(kodTowaru, out var nazwaGrupy))
                        {
                            if (!sumaPerZamowieniePerGrupa.ContainsKey(zamId))
                                sumaPerZamowieniePerGrupa[zamId] = new Dictionary<string, decimal>();

                            if (!sumaPerZamowieniePerGrupa[zamId].ContainsKey(nazwaGrupy))
                                sumaPerZamowieniePerGrupa[zamId][nazwaGrupy] = 0m;

                            sumaPerZamowieniePerGrupa[zamId][nazwaGrupy] += iloscTowaru;
                        }
                    }
                }
            }

            var cultureInfo = new CultureInfo("pl-PL");
            // Polskie skróty miesięcy
            string[] polskieMiesiaceSkrot = { "", "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" };

            foreach (DataRow r in temp.Rows)
            {
                int id = r["Id"] == DBNull.Value ? 0 : Convert.ToInt32(r["Id"]);
                int klientId = r["KlientId"] == DBNull.Value ? 0 : Convert.ToInt32(r["KlientId"]);
                decimal ilosc = r["Ilosc"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Ilosc"]);
                DateTime? dataPrzyjazdu = r["DataPrzyjazdu"] is DBNull or null ? (DateTime?)null : Convert.ToDateTime(r["DataPrzyjazdu"]);
                DateTime? dataUtw = r["DataUtworzenia"] is DBNull or null ? (DateTime?)null : Convert.ToDateTime(r["DataUtworzenia"]);
                DateTime? dataUboju = null;
                if (_dataUbojuKolumnaIstnieje && temp.Columns.Contains("DataUboju"))
                {
                    dataUboju = r["DataUboju"] is DBNull or null ? (DateTime?)null : Convert.ToDateTime(r["DataUboju"]);
                }
                string idUser = r["IdUser"]?.ToString() ?? "";
                string status = r["Status"]?.ToString() ?? "Nowe";
                string uwagi = r["Uwagi"]?.ToString() ?? "";
                bool maNotatke = !string.IsNullOrWhiteSpace(uwagi);

                int pojemniki = r["LiczbaPojemnikow"] == DBNull.Value ? 0 : (int)Math.Round(Convert.ToDecimal(r["LiczbaPojemnikow"]));
                decimal palety = Math.Ceiling(r["LiczbaPalet"] == DBNull.Value ? 0m : Convert.ToDecimal(r["LiczbaPalet"]));
                bool trybE2 = r["TrybE2"] != DBNull.Value && Convert.ToBoolean(r["TrybE2"]);
                string trybText = trybE2 ? "E2 (40)" : "STD (36)";

                var (nazwa, handlowiec) = kontrahenci.TryGetValue(klientId, out var kh) ? kh : ($"Nieznany ({klientId})", "");

                if (maNotatke)
                {
                    nazwa = "📝 " + nazwa;
                }

                decimal wydane = 0m;
                if (wydaniaPerKhidIdtw.TryGetValue(klientId, out var perIdtw))
                {
                    wydane = selectedProductId.HasValue ?
                        perIdtw.TryGetValue(selectedProductId.Value, out var w) ? w : 0m :
                        perIdtw.Values.Sum();
                }

                string terminOdbioru = dataPrzyjazdu.HasValue ?
                    dataPrzyjazdu.Value.ToString("yyyy-MM-dd dddd HH:mm", cultureInfo) :
                    dzien.ToString("yyyy-MM-dd dddd", cultureInfo);

                // Format: "Sty 12 (Ania)" - skrócony miesiąc, dzień i imię
                string utworzonePrzez = "";
                string userName = _userCache.TryGetValue(idUser, out var user) ? user : "Brak";
                if (dataUtw.HasValue)
                {
                    string miesiacSkrot = polskieMiesiaceSkrot[dataUtw.Value.Month];
                    utworzonePrzez = $"{miesiacSkrot} {dataUtw.Value.Day} ({userName})";
                }
                else
                {
                    utworzonePrzez = userName;
                }

                // Tworzenie wiersza z dynamicznymi kolumnami grup
                var newRow = _dtZamowienia.NewRow();
                newRow["Id"] = id;
                newRow["KlientId"] = klientId;
                newRow["Odbiorca"] = nazwa;
                newRow["Handlowiec"] = handlowiec;
                newRow["IloscZamowiona"] = ilosc;
                newRow["IloscFaktyczna"] = wydane;
                newRow["Pojemniki"] = pojemniki;
                newRow["Palety"] = palety;
                newRow["TrybE2"] = trybText;
                newRow["DataPrzyjecia"] = dataPrzyjazdu?.Date ?? dzien;
                newRow["GodzinaPrzyjecia"] = dataPrzyjazdu?.ToString("HH:mm") ?? "08:00";
                newRow["TerminOdbioru"] = terminOdbioru;
                newRow["DataUboju"] = dataUboju.HasValue ? (object)dataUboju.Value : DBNull.Value;
                newRow["UtworzonePrzez"] = utworzonePrzez;
                newRow["Status"] = status;
                newRow["MaNotatke"] = maNotatke;

                // Wypełnij kolumny grup towarowych
                foreach (var grupaName in _grupyTowaroweNazwy)
                {
                    decimal sumaGrupy = 0m;
                    if (sumaPerZamowieniePerGrupa.TryGetValue(id, out var grupyDict) &&
                        grupyDict.TryGetValue(grupaName, out var suma))
                    {
                        sumaGrupy = suma;
                    }
                    newRow[$"Grupa_{grupaName}"] = sumaGrupy;
                }

                _dtZamowienia.Rows.Add(newRow);
            }

            var wydaniaBezZamowien = new List<DataRow>();
            foreach (var kv in wydaniaPerKhidIdtw)
            {
                int khid = kv.Key;
                if (klienciZamowien.Contains(khid)) continue;
                decimal wydane = selectedProductId.HasValue ?
                    kv.Value.TryGetValue(selectedProductId.Value, out var w) ? w : 0m :
                    kv.Value.Values.Sum();
                if (wydane == 0) continue;

                var (nazwa, handlowiec) = kontrahenci.TryGetValue(khid, out var kh) ? kh : ($"Nieznany ({khid})", "");
                var row = _dtZamowienia.NewRow();
                row["Id"] = 0;
                row["KlientId"] = khid;
                row["Odbiorca"] = nazwa;
                row["Handlowiec"] = handlowiec;
                row["IloscZamowiona"] = 0m;
                row["IloscFaktyczna"] = wydane;
                row["Pojemniki"] = 0;
                row["Palety"] = 0m;
                row["TrybE2"] = "";
                row["DataPrzyjecia"] = dzien;
                row["GodzinaPrzyjecia"] = "";
                row["TerminOdbioru"] = dzien.ToString("yyyy-MM-dd dddd", cultureInfo);
                row["DataUboju"] = DBNull.Value;
                row["UtworzonePrzez"] = "";
                row["Status"] = "Wydanie bez zamówienia";
                row["MaNotatke"] = false;

                // Kolumny grup dla wydań bez zamówień = 0
                foreach (var grupaName in _grupyTowaroweNazwy)
                {
                    row[$"Grupa_{grupaName}"] = 0m;
                }

                wydaniaBezZamowien.Add(row);
            }

            foreach (var row in wydaniaBezZamowien.OrderByDescending(r => (decimal)r["IloscFaktyczna"]))
                _dtZamowienia.Rows.Add(row.ItemArray);

            _bsZamowienia.DataSource = _dtZamowienia;
            dgvZamowienia.DataSource = _bsZamowienia;
            _bsZamowienia.Sort = "Handlowiec ASC, IloscZamowiona DESC";
            dgvZamowienia.ClearSelection();

            if (dgvZamowienia.Columns["Id"] != null) dgvZamowienia.Columns["Id"].Visible = false;
            if (dgvZamowienia.Columns["KlientId"] != null) dgvZamowienia.Columns["KlientId"].Visible = false;
            if (dgvZamowienia.Columns["MaNotatke"] != null) dgvZamowienia.Columns["MaNotatke"].Visible = false;
            if (dgvZamowienia.Columns["TrybE2"] != null) dgvZamowienia.Columns["TrybE2"].Visible = false;
            if (dgvZamowienia.Columns["DataUboju"] != null) dgvZamowienia.Columns["DataUboju"].Visible = false;
            if (dgvZamowienia.Columns["Pojemniki"] != null) dgvZamowienia.Columns["Pojemniki"].Visible = false;
            if (dgvZamowienia.Columns["DataPrzyjecia"] != null) dgvZamowienia.Columns["DataPrzyjecia"].Visible = false;
            if (dgvZamowienia.Columns["GodzinaPrzyjecia"] != null) dgvZamowienia.Columns["GodzinaPrzyjecia"].Visible = false;

            if (dgvZamowienia.Columns["Odbiorca"] != null)
            {
                dgvZamowienia.Columns["Odbiorca"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                dgvZamowienia.Columns["Odbiorca"].DefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
                dgvZamowienia.Columns["Odbiorca"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dgvZamowienia.Columns["Odbiorca"].FillWeight = 180;
            }

            if (dgvZamowienia.Columns["Handlowiec"] != null)
            {
                dgvZamowienia.Columns["Handlowiec"].AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                dgvZamowienia.Columns["Handlowiec"].DefaultCellStyle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            }

            if (dgvZamowienia.Columns["IloscZamowiona"] != null)
            {
                dgvZamowienia.Columns["IloscZamowiona"].HeaderText = "Zamówiono";
                dgvZamowienia.Columns["IloscZamowiona"].DefaultCellStyle.Format = "N0";
                dgvZamowienia.Columns["IloscZamowiona"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }

            if (dgvZamowienia.Columns["IloscFaktyczna"] != null)
            {
                dgvZamowienia.Columns["IloscFaktyczna"].HeaderText = "Wydano";
                dgvZamowienia.Columns["IloscFaktyczna"].DefaultCellStyle.Format = "N0";
                dgvZamowienia.Columns["IloscFaktyczna"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }

            if (dgvZamowienia.Columns["Palety"] != null)
            {
                dgvZamowienia.Columns["Palety"].DefaultCellStyle.Format = "N0";
                dgvZamowienia.Columns["Palety"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                dgvZamowienia.Columns["Palety"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                dgvZamowienia.Columns["Palety"].Width = 50;
            }

            if (dgvZamowienia.Columns["TerminOdbioru"] != null)
            {
                dgvZamowienia.Columns["TerminOdbioru"].HeaderText = "Termin Odbioru";
                dgvZamowienia.Columns["TerminOdbioru"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                dgvZamowienia.Columns["TerminOdbioru"].Width = 200;
            }

            if (dgvZamowienia.Columns["UtworzonePrzez"] != null)
            {
                dgvZamowienia.Columns["UtworzonePrzez"].HeaderText = "Utworzone przez";
                dgvZamowienia.Columns["UtworzonePrzez"].AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            }

            // Konfiguracja kolumn grup towarowych
            foreach (var grupaName in _grupyTowaroweNazwy)
            {
                var colName = $"Grupa_{grupaName}";
                if (dgvZamowienia.Columns[colName] != null)
                {
                    dgvZamowienia.Columns[colName].HeaderText = grupaName;
                    dgvZamowienia.Columns[colName].DefaultCellStyle.Format = "N0";
                    dgvZamowienia.Columns[colName].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    dgvZamowienia.Columns[colName].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                    dgvZamowienia.Columns[colName].MinimumWidth = 60;
                    dgvZamowienia.Columns[colName].DefaultCellStyle.BackColor = Color.FromArgb(240, 248, 255);
                    dgvZamowienia.Columns[colName].DefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                }
            }

            ZastosujFiltry();
        }

        private async Task<Dictionary<int, Dictionary<int, decimal>>> PobierzWydaniaPerKhidIdtwAsync(DateTime dzien)
        {
            var dict = new Dictionary<int, Dictionary<int, decimal>>();
            if (!_twKatalogCache.Keys.Any()) return dict;

            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            var idwList = string.Join(",", _twKatalogCache.Keys);
            string sql = $@"SELECT MG.khid, MZ.idtw, SUM(ABS(MZ.ilosc)) AS qty FROM [HANDEL].[HM].[MZ] MZ JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny = 1 AND MG.data = @Dzien AND MG.khid IS NOT NULL AND MZ.idtw IN ({idwList}) GROUP BY MG.khid, MZ.idtw";
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

        private async void dgvZamowienia_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0) return;
                var widokZamowienia = new WidokZamowienia(UserID, id);
                if (widokZamowienia.ShowDialog() == DialogResult.OK)
                {
                    await OdswiezWszystkieDaneAsync();
                }
            }
        }

        private void dgvZamowienia_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dgvZamowienia.Columns[e.ColumnIndex].Name == "Palety")
            {
                if (e.Value is decimal val && val > 34)
                {
                    e.CellStyle.BackColor = Color.FromArgb(255, 100, 100);
                    e.CellStyle.ForeColor = Color.White;
                    e.CellStyle.Font = new Font(dgvZamowienia.Font, FontStyle.Bold);
                    e.FormattingApplied = true;
                }
            }
        }

        private void dgvSzczegoly_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dgvSzczegoly.Columns[e.ColumnIndex].Name == "Folia" && e.Value != null)
            {
                string value = e.Value.ToString() ?? "";

                if (value.Equals("TAK", StringComparison.OrdinalIgnoreCase))
                {
                    e.Value = "✔";
                    e.CellStyle.Font = new Font("Segoe UI Symbol", 12f, FontStyle.Bold);
                    e.CellStyle.ForeColor = Color.Green;
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    e.FormattingApplied = true;
                }
                else if (value.Equals("NIE", StringComparison.OrdinalIgnoreCase))
                {
                    e.Value = "";
                    e.FormattingApplied = true;
                }
                else if (value.Equals("B/D", StringComparison.OrdinalIgnoreCase))
                {
                    e.Value = "?";
                    e.CellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
                    e.CellStyle.ForeColor = Color.Gray;
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    e.FormattingApplied = true;
                }
            }

            if (dgvSzczegoly.Columns[e.ColumnIndex].Name == "Różnica" && e.Value != null)
            {
                if (e.Value is decimal roznica)
                {
                    if (roznica < 0)
                    {
                        e.CellStyle.ForeColor = Color.Red;
                        e.CellStyle.Font = new Font(dgvSzczegoly.Font, FontStyle.Bold);
                    }
                    else if (roznica > 0)
                    {
                        e.CellStyle.ForeColor = Color.Green;
                        e.CellStyle.Font = new Font(dgvSzczegoly.Font, FontStyle.Bold);
                    }
                }
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
                    var klientId = drv.Row.Field<int>("KlientId");
                    _aktualneIdZamowienia = null;
                    await WyswietlSzczegolyWydaniaBezZamowieniaAsync(klientId, _selectedDate);
                    return;
                }

                var id = drv.Row.Field<int>("Id");

                if (id > 0)
                {
                    _aktualneIdZamowienia = id;
                    await WyswietlSzczegolyZamowieniaAsync(id);
                    return;
                }
            }

            WyczyscSzczegoly();
        }

        private async Task WyswietlSzczegolyWydaniaBezZamowieniaAsync(int khId, DateTime dzien)
        {
            var dt = new DataTable();
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("Wydano", typeof(decimal));

            string odbiorcaNazwa = "Nieznany";
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                var cmdNazwa = new SqlCommand("SELECT Shortcut FROM [HANDEL].[SSCommon].[STContractors] WHERE Id = @Id", cn);
                cmdNazwa.Parameters.AddWithValue("@Id", khId);
                var result = await cmdNazwa.ExecuteScalarAsync();
                if (result != null)
                    odbiorcaNazwa = result.ToString() ?? $"KH {khId}";
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
                AND TW.katalog IN (67095, 67153) 
                GROUP BY MZ.idtw
                ORDER BY SUM(ABS(MZ.ilosc)) DESC";

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

            txtNotatki.Text = $"📦 Wydanie bez zamówienia\n\nOdbiorca: {odbiorcaNazwa} (ID: {khId})\nData: {dzien:yyyy-MM-dd dddd}\n\nPoniżej lista wydanych produktów (tylko towary z katalogów 67095 i 67153)";

            dgvSzczegoly.DataSource = dt;

            if (dgvSzczegoly.Columns["Produkt"] != null)
            {
                dgvSzczegoly.Columns["Produkt"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dgvSzczegoly.Columns["Produkt"].HeaderText = "Produkt";
            }

            if (dgvSzczegoly.Columns["Wydano"] != null)
            {
                dgvSzczegoly.Columns["Wydano"].DefaultCellStyle.Format = "N0";
                dgvSzczegoly.Columns["Wydano"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                dgvSzczegoly.Columns["Wydano"].HeaderText = "Wydano (kg)";
                dgvSzczegoly.Columns["Wydano"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                dgvSzczegoly.Columns["Wydano"].Width = 120;
            }
        }

        private async Task<Dictionary<int, decimal>> PobierzStanyMagazynowe(DateTime dzien)
        {
            var stany = new Dictionary<int, decimal>();

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                string sql = @"SELECT idtw, 0 as ilosc FROM [HANDEL].[HM].[TW] WHERE katalog IN (67095, 67153)";

                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int idtw = reader.GetInt32(0);
                    decimal ilosc = SafeDecimal(reader, 1);
                    stany[idtw] = ilosc;
                }
            }
            catch (Exception ex)
            {
                // Nie przerywaj działania aplikacji
            }

            return stany;
        }

        #region Scalowanie towarów
        private void InicjalizujMenuKontekstoweAgregacji()
        {
            var menuAgregacja = new ContextMenuStrip();

            var menuScalowanie = new ToolStripMenuItem("Konfiguruj scalowanie towarów");
            menuScalowanie.Click += async (s, e) =>
            {
                using var dialog = new ScalowanieTowarowDialog(_connLibra, _twKatalogCache);
                dialog.ShowDialog(this);

                // Po zamknięciu dialogu odśwież mapowanie
                await ZaladujMapowanieScalowaniaAsync();
                await WyswietlAgregacjeProduktowAsync(_selectedDate);
            };

            var menuOdswiez = new ToolStripMenuItem("Odśwież podsumowanie");
            menuOdswiez.Click += async (s, e) =>
            {
                await ZaladujMapowanieScalowaniaAsync();
                await WyswietlAgregacjeProduktowAsync(_selectedDate);
            };

            menuAgregacja.Items.Add(menuScalowanie);
            menuAgregacja.Items.Add(new ToolStripSeparator());
            menuAgregacja.Items.Add(menuOdswiez);

            dgvAgregacja.ContextMenuStrip = menuAgregacja;
        }

        private async Task ZaladujMapowanieScalowaniaAsync()
        {
            try
            {
                _mapowanieScalowania = await ScalowanieTowarowManager.PobierzMapowanieTowarowAsync(_connLibra);
                // Ustaw listę unikalnych nazw grup towarowych (posortowane)
                _grupyTowaroweNazwy = _mapowanieScalowania.Values.Distinct().OrderBy(n => n).ToList();
            }
            catch
            {
                _mapowanieScalowania = new Dictionary<int, string>();
                _grupyTowaroweNazwy = new List<string>();
            }
        }
        #endregion

        private async Task WyswietlAgregacjeProduktowAsync(DateTime dzien)
        {
            var dtAg = new DataTable();
            dtAg.Columns.Add("Produkt", typeof(string));
            dtAg.Columns.Add("PlanowanyPrzychód", typeof(decimal));
            dtAg.Columns.Add("FaktycznyPrzychód", typeof(decimal));
            dtAg.Columns.Add("Zamówienia", typeof(decimal));
            dtAg.Columns.Add("Bilans", typeof(decimal));

            var (planPrzychodu, faktPrzychodu) = await PrognozaIFaktPrzychoduPerProduktAsync(dzien);

            var sumaZamowien = new Dictionary<int, decimal>();
            var zamowieniaIds = _dtZamowienia.AsEnumerable()
                .Where(r => !string.Equals(r.Field<string>("Status"), "Anulowane", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Field<int>("Id"))
                .Where(id => id > 0)
                .ToList();

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

            // Agregacja z uwzględnieniem scalania towarów
            // Słownik: nazwa produktu (lub grupa) -> (plan, fakt, zam)
            var agregowane = new Dictionary<string, (decimal plan, decimal fakt, decimal zam)>(StringComparer.OrdinalIgnoreCase);
            var towaryWGrupach = new HashSet<int>(); // idtw towarów już scalonych

            // Najpierw zbierz towary w grupach scalania
            foreach (var towar in _twKatalogCache)
            {
                if (_mapowanieScalowania.TryGetValue(towar.Key, out var nazwaGrupy))
                {
                    towaryWGrupach.Add(towar.Key);

                    var plan = planPrzychodu.TryGetValue(towar.Key, out var p) ? p : 0m;
                    var fakt = faktPrzychodu.TryGetValue(towar.Key, out var f) ? f : 0m;
                    var zam = sumaZamowien.TryGetValue(towar.Key, out var z) ? z : 0m;

                    if (agregowane.ContainsKey(nazwaGrupy))
                    {
                        var existing = agregowane[nazwaGrupy];
                        agregowane[nazwaGrupy] = (existing.plan + plan, existing.fakt + fakt, existing.zam + zam);
                    }
                    else
                    {
                        agregowane[nazwaGrupy] = (plan, fakt, zam);
                    }
                }
            }

            // Dodaj towary niescalone
            foreach (var towar in _twKatalogCache.OrderBy(kvp => kvp.Value))
            {
                if (towaryWGrupach.Contains(towar.Key)) continue;

                var kod = towar.Value;
                if (IsKurczakB(kod)) continue;

                var plan = planPrzychodu.TryGetValue(towar.Key, out var p) ? p : 0m;
                var fakt = faktPrzychodu.TryGetValue(towar.Key, out var f) ? f : 0m;
                var zam = sumaZamowien.TryGetValue(towar.Key, out var z) ? z : 0m;

                agregowane[kod] = (plan, fakt, zam);
            }

            // Dodaj do DataTable
            foreach (var kv in agregowane.OrderBy(x => x.Key))
            {
                var bilans = kv.Value.fakt - kv.Value.zam;
                dtAg.Rows.Add(kv.Key, kv.Value.plan, kv.Value.fakt, kv.Value.zam, bilans);
            }

            dgvAgregacja.DataSource = dtAg;

            dgvAgregacja.Sort(dgvAgregacja.Columns["PlanowanyPrzychód"], System.ComponentModel.ListSortDirection.Descending);

            foreach (DataGridViewColumn col in dgvAgregacja.Columns)
            {
                if (col.Name != "Produkt")
                    col.DefaultCellStyle.Format = "N0";
            }

            if (dgvAgregacja.Columns["PlanowanyPrzychód"] != null)
                dgvAgregacja.Columns["PlanowanyPrzychód"].HeaderText = "Plan przychód";
            if (dgvAgregacja.Columns["FaktycznyPrzychód"] != null)
                dgvAgregacja.Columns["FaktycznyPrzychód"].HeaderText = "Fakt przychód";
        }

        private void AktualizujPodsumowanieDnia()
        {
            int liczbaZamowien = 0;
            int liczbaWydanBezZamowien = 0;
            decimal sumaKgZamowiono = 0;
            decimal sumaKgWydano = 0;
            int sumaPojemnikow = 0;
            decimal sumaPalet = 0m;
            var handlowiecStat = new Dictionary<string, (int zZam, int bezZam, decimal kgZam, decimal kgWyd)>();

            if (_bsZamowienia.List is System.Collections.IEnumerable list)
            {
                foreach (var item in list)
                {
                    if (item is DataRowView drv)
                    {
                        var status = drv.Row.Field<string>("Status") ?? "";
                        if (status == "Anulowane") continue;

                        var handlowiec = drv.Row.Field<string>("Handlowiec") ?? "BRAK";
                        var iloscZam = drv.Row.Field<decimal?>("IloscZamowiona") ?? 0m;
                        var iloscWyd = drv.Row.Field<decimal?>("IloscFaktyczna") ?? 0m;
                        var pojemniki = drv.Row.Field<int?>("Pojemniki") ?? 0;
                        var palety = drv.Row.Field<decimal?>("Palety") ?? 0m;

                        if (!handlowiecStat.ContainsKey(handlowiec))
                            handlowiecStat[handlowiec] = (0, 0, 0, 0);

                        if (status == "Wydanie bez zamówienia")
                        {
                            liczbaWydanBezZamowien++;
                            sumaKgWydano += iloscWyd;
                            handlowiecStat[handlowiec] = (handlowiecStat[handlowiec].zZam, handlowiecStat[handlowiec].bezZam + 1, handlowiecStat[handlowiec].kgZam, handlowiecStat[handlowiec].kgWyd + iloscWyd);
                        }
                        else
                        {
                            liczbaZamowien++;
                            sumaKgZamowiono += iloscZam;
                            sumaKgWydano += iloscWyd;
                            sumaPojemnikow += pojemniki;
                            sumaPalet += palety;
                            handlowiecStat[handlowiec] = (handlowiecStat[handlowiec].zZam + 1, handlowiecStat[handlowiec].bezZam, handlowiecStat[handlowiec].kgZam + iloscZam, handlowiecStat[handlowiec].kgWyd + iloscWyd);
                        }
                    }
                }
            }
            int suma = liczbaZamowien + liczbaWydanBezZamowien;
            string perHandlowiec = string.Join(" | ", handlowiecStat.OrderBy(h => h.Key).Select(h => $"{h.Key}: {h.Value.zZam}/{h.Value.bezZam} ({h.Value.kgZam:N0}/{h.Value.kgWyd:N0}kg)"));
            lblPodsumowanie.Text = $"Suma: {suma} ({liczbaZamowien} zam. / {liczbaWydanBezZamowien} wyd.) | " + $"Zamówiono: {sumaKgZamowiono:N0} kg | Wydano: {sumaKgWydano:N0} kg | " + $"Pojemn.: {sumaPojemnikow:N0} | Palet: {sumaPalet:N1} | {perHandlowiec}";
        }

        private void WyczyscSzczegoly()
        {
            dgvSzczegoly.DataSource = null;
            txtNotatki.Clear();
            _aktualneIdZamowienia = null;
        }

        private async Task<Dictionary<int, decimal>> PobierzSumeWydanPoProdukcieAsync(DateTime dzien)
        {
            var sumaWydan = new Dictionary<int, decimal>();
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) FROM [HANDEL].[HM].[MZ] MZ JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id WHERE MG.seria IN ('sWZ', 'sWZ-W') AND MG.aktywny=1 AND MG.data = @Dzien GROUP BY MZ.idtw";
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
                const string sql = @"SELECT WagaDek, SztukiDek FROM dbo.HarmonogramDostaw WHERE DataOdbioru = @Dzien AND Bufor = 'Potwierdzony'";
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
                if (IsKurczakA(kod)) plan[idtw] = pulaA;
                else if (IsKurczakB(kod)) plan[idtw] = pulaB;
                else
                {
                    if (YieldByKod.TryGetValue(kod, out var share) && share > 0m) plan[idtw] = Math.Max(0m, pulaB * share);
                    else plan[idtw] = 0m;
                }
            }

            var fakt = new Dictionary<int, decimal>();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) FROM [HANDEL].[HM].[MZ] MZ JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id WHERE MG.seria = 'sPWU' AND MG.aktywny=1 AND MG.data = @Dzien GROUP BY MZ.idtw";
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

            var status = rowObj.Row.Table.Columns.Contains("Status") ? rowObj.Row["Status"]?.ToString() : null;
            var row = dgvZamowienia.Rows[e.RowIndex];

            row.DefaultCellStyle.BackColor = SystemColors.Window;
            row.DefaultCellStyle.ForeColor = SystemColors.ControlText;
            row.DefaultCellStyle.Font = new Font(dgvZamowienia.Font, FontStyle.Regular);

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

                if (id > 0)
                {
                    _aktualneIdZamowienia = id;
                    return true;
                }
                else
                {
                    _aktualneIdZamowienia = null;
                    return false;
                }
            }

            if (dgvZamowienia.Columns.Contains("Id"))
            {
                var cellVal = dgvZamowienia.CurrentRow.Cells["Id"]?.Value;
                if (cellVal != null && cellVal != DBNull.Value &&
                    int.TryParse(cellVal.ToString(), out id) && id > 0)
                {
                    _aktualneIdZamowienia = id;
                    return true;
                }
            }

            _aktualneIdZamowienia = null;
            return false;
        }
        #endregion

        private async Task WyswietlSzczegolyZamowieniaAsync(int zamowienieId)
        {
            try
            {
                dgvSzczegoly.DataSource = null;
                txtNotatki.Clear();

                int klientId = 0;
                string notatki = "";
                var pozycjeZamowienia = new List<(int KodTowaru, decimal Ilosc, bool Folia)>();

                DateTime dataDoWyszukaniaWydan = _selectedDate.Date;

                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();

                    string dataUbojuSelect = _dataUbojuKolumnaIstnieje ? ", DataUboju" : "";
                    using (var cmdInfo = new SqlCommand($@"
                        SELECT KlientId, Uwagi, DataZamowienia{dataUbojuSelect} 
                        FROM dbo.ZamowieniaMieso 
                        WHERE Id = @Id", cn))
                    {
                        cmdInfo.Parameters.AddWithValue("@Id", zamowienieId);
                        using var readerInfo = await cmdInfo.ExecuteReaderAsync();
                        if (await readerInfo.ReadAsync())
                        {
                            klientId = readerInfo.IsDBNull(0) ? 0 : readerInfo.GetInt32(0);
                            notatki = readerInfo.IsDBNull(1) ? "" : readerInfo.GetString(1);

                            var dataZamowienia = readerInfo.GetDateTime(2);
                            DateTime? dataUboju = null;
                            if (_dataUbojuKolumnaIstnieje && !readerInfo.IsDBNull(3))
                            {
                                dataUboju = readerInfo.GetDateTime(3);
                            }

                            dataDoWyszukaniaWydan = (_pokazujPoDatachUboju && dataUboju.HasValue) ? dataUboju.Value : dataZamowienia;
                        }
                    }

                    using (var cmdPozycje = new SqlCommand(@"
                SELECT KodTowaru, Ilosc, ISNULL(Folia, 0) as Folia
                FROM dbo.ZamowieniaMiesoTowar
                WHERE ZamowienieId = @Id", cn))
                    {
                        cmdPozycje.Parameters.AddWithValue("@Id", zamowienieId);
                        using var readerPozycje = await cmdPozycje.ExecuteReaderAsync();
                        while (await readerPozycje.ReadAsync())
                        {
                            int kodTowaru = readerPozycje.GetInt32(0);
                            decimal ilosc = readerPozycje.IsDBNull(1) ? 0m : readerPozycje.GetDecimal(1);
                            bool folia = readerPozycje.GetBoolean(2);

                            pozycjeZamowienia.Add((kodTowaru, ilosc, folia));
                        }
                    }
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
                    WHERE MG.seria IN ('sWZ','sWZ-W') 
                    AND MG.aktywny = 1 
                    AND MG.data = @Dzien 
                    AND MG.khid = @Khid 
                    GROUP BY MZ.idtw";

                        await using var cmd = new SqlCommand(sql, cn);
                        cmd.Parameters.AddWithValue("@Dzien", dataDoWyszukaniaWydan.Date);
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
                dt.Columns.Add("Różnica", typeof(decimal));
                dt.Columns.Add("Folia", typeof(string));

                foreach (var pozycja in pozycjeZamowienia)
                {
                    if (!_twKatalogCache.ContainsKey(pozycja.KodTowaru))
                        continue;

                    string produkt = _twKatalogCache.TryGetValue(pozycja.KodTowaru, out var kod) ?
                        kod : $"Nieznany ({pozycja.KodTowaru})";
                    decimal zamowiono = pozycja.Ilosc;
                    decimal wydano = wydania.TryGetValue(pozycja.KodTowaru, out var w) ? w : 0m;
                    decimal roznica = wydano - zamowiono;

                    dt.Rows.Add(
                        produkt,
                        zamowiono,
                        wydano,
                        roznica,
                        pozycja.Folia ? "TAK" : "NIE"
                    );

                    wydania.Remove(pozycja.KodTowaru);
                }

                foreach (var kv in wydania)
                {
                    if (!_twKatalogCache.ContainsKey(kv.Key))
                        continue;

                    string produkt = _twKatalogCache.TryGetValue(kv.Key, out var kod) ?
                        kod : $"Nieznany ({kv.Key})";
                    dt.Rows.Add(
                        produkt,
                        0m,
                        kv.Value,
                        kv.Value,
                        "B/D"
                    );
                }

                txtNotatki.Text = notatki;

                if (dt.Rows.Count > 0)
                {
                    dgvSzczegoly.DataSource = dt;

                    if (dgvSzczegoly.Columns["Zamówiono"] != null)
                    {
                        dgvSzczegoly.Columns["Zamówiono"].DefaultCellStyle.Format = "N0";
                        dgvSzczegoly.Columns["Zamówiono"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                    if (dgvSzczegoly.Columns["Wydano"] != null)
                    {
                        dgvSzczegoly.Columns["Wydano"].DefaultCellStyle.Format = "N0";
                        dgvSzczegoly.Columns["Wydano"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                    if (dgvSzczegoly.Columns["Różnica"] != null)
                    {
                        dgvSzczegoly.Columns["Różnica"].DefaultCellStyle.Format = "N0";
                        dgvSzczegoly.Columns["Różnica"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                    if (dgvSzczegoly.Columns["Folia"] != null)
                    {
                        dgvSzczegoly.Columns["Folia"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                        dgvSzczegoly.Columns["Folia"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    }
                    if (dgvSzczegoly.Columns["Produkt"] != null)
                    {
                        dgvSzczegoly.Columns["Produkt"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    }
                }
                else
                {
                    var dtEmpty = new DataTable();
                    dtEmpty.Columns.Add("Info", typeof(string));
                    dtEmpty.Rows.Add("Brak pozycji w zamówieniu");
                    dgvSzczegoly.DataSource = dtEmpty;
                }
            }
            catch (Exception ex)
            {
                ShowError($"Błąd podczas wczytywania szczegółów zamówienia:\n{ex.Message}");

                dgvSzczegoly.DataSource = null;
                txtNotatki.Clear();
            }
        }

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

            if (!_pokazujWydaniaBezZamowien)
            {
                warunki.Add("Status <> 'Wydanie bez zamówienia'");
            }

            _dtZamowienia.DefaultView.RowFilter = string.Join(" AND ", warunki);
        }

        private async void btnUsun_Click(object? sender, EventArgs e)
        {
            if (!TrySetAktualneIdZamowieniaFromGrid(out var id) || id <= 0)
            {
                ShowInfo("Najpierw wybierz zamówienie do usunięcia.", "Brak wyboru");
                return;
            }

            var result = ShowWarningQuestion("Czy na pewno chcesz TRWALE usunąć wybrane zamówienie? Tej operacji nie można cofnąć.", "Potwierdź usunięcie");
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
                    ShowInfo("Zamówienie zostało trwale usunięte.", "Sukces");
                    await OdswiezWszystkieDaneAsync();
                }
                catch (Exception ex)
                {
                    ShowError($"Błąd podczas usuwania zamówienia: {ex.Message}", "Błąd krytyczny");
                }
            }
        }
    }
}
