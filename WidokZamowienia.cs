// Plik: WidokZamowienia.cs
// WERSJA 10.0 - CHECKBOX E2 PER TOWAR + ULEPSZONE UI
// Zmiany:
// 1. Checkbox E2 dla każdego towaru osobno
// 2. Palety bez miejsc po przecinku
// 3. Ulepszone UI z gradientami i nowoczesnymi stylami

#nullable enable
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokZamowienia : Form
    {
        // ===== Publiczne Właściwości =====
        public string UserID { get; set; } = string.Empty;
        private int? _idZamowieniaDoEdycji;

        // ===== Połączenia z Bazą =====
        private readonly string _connLibra;
        private readonly string _connHandel;

        // ===== Stałe przeliczeniowe =====
        private const decimal POJEMNIKOW_NA_PALECIE = 36m;
        private const decimal POJEMNIKOW_NA_PALECIE_E2 = 40m; // Dla E2
        private const decimal KG_NA_POJEMNIKU = 15m;
        private const decimal KG_NA_PALECIE = POJEMNIKOW_NA_PALECIE * KG_NA_POJEMNIKU; // 540
        private const decimal KG_NA_PALECIE_E2 = POJEMNIKOW_NA_PALECIE_E2 * KG_NA_POJEMNIKU; // 600

        // ===== Zmienne Stanu Formularza =====
        private string? _selectedKlientId;
        private bool _blokujObslugeZmian;
        private readonly CultureInfo _pl = new("pl-PL");
        private readonly Dictionary<string, Image> _headerIcons = new();

        // ===== Dane i Cache =====
        private sealed class KontrahentInfo
        {
            public string Id { get; set; } = "";
            public string Nazwa { get; set; } = "";
            public string KodPocztowy { get; set; } = "";
            public string Miejscowosc { get; set; } = "";
            public string NIP { get; set; } = "";
            public string Handlowiec { get; set; } = "";
        }

        private readonly DataTable _dt = new();
        private DataView _view = default!;
        private readonly List<KontrahentInfo> _kontrahenci = new();

        // ===== Konstruktory =====
        public WidokZamowienia() : this(App.UserID ?? string.Empty, null) { }
        public WidokZamowienia(int? idZamowienia) : this(App.UserID ?? string.Empty, idZamowienia) { }

        public WidokZamowienia(string userId, int? idZamowienia = null)
        {
            InitializeComponent();
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;

            UserID = userId;
            _idZamowieniaDoEdycji = idZamowienia;

            _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
            _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

            this.Load += WidokZamowienia_Load;
        }

        // ===== GŁÓWNA METODA ŁADUJĄCA (ASYnchroniczna) =====
        private async void WidokZamowienia_Load(object? sender, EventArgs e)
        {
            ApplyModernUIStyles();
            CreateHeaderIcons();
            SzybkiGrid();
            WireShortcuts();
            BuildDataTableSchema();
            InitDefaults();

            // Ustaw format daty z dniem tygodnia
            dateTimePickerSprzedaz.Format = DateTimePickerFormat.Custom;
            dateTimePickerSprzedaz.CustomFormat = "yyyy-MM-dd (dddd)";

            try
            {
                _ = LoadInitialDataInBackground();
                WireUpUIEvents();

                if (_idZamowieniaDoEdycji.HasValue)
                {
                    _ = LoadZamowienieAsync(_idZamowieniaDoEdycji.Value);
                    lblTytul.Text = "Edycja zamówienia";
                    btnZapisz.Text = "Zapisz zmiany (Ctrl+S)";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił krytyczny błąd podczas ładowania danych: {ex.Message}\n\nSprawdź połączenie z bazą danych i poprawność zapytań SQL.", "Błąd Aplikacji", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
                btnZapisz.Enabled = true;
            }
        }

        #region Inicjalizacja i Ustawienia UI

        private void ApplyModernUIStyles()
        {
            // Tło formularza z gradientem
            this.BackColor = Color.FromArgb(245, 247, 250);

            // Stylizacja tytułu
            if (lblTytul != null)
            {
                lblTytul.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
                lblTytul.ForeColor = Color.FromArgb(37, 99, 235);
            }

            // Stylizacja przycisków
            StyleButton(btnZapisz, Color.FromArgb(34, 197, 94), Color.White);
            StyleButton(btnPickOdbiorca, Color.FromArgb(59, 130, 246), Color.White);

            // Stylizacja paneli
            if (panelDaneOdbiorcy != null)
            {
                panelDaneOdbiorcy.BackColor = Color.White;
                panelDaneOdbiorcy.BorderStyle = BorderStyle.None;
                panelDaneOdbiorcy.Paint += Panel_Paint;
            }

            // Stylizacja DateTimePickerów
            StyleDateTimePicker(dateTimePickerSprzedaz);
            StyleDateTimePicker(dateTimePickerGodzinaPrzyjazdu);

            // Stylizacja TextBoxów
            StyleTextBox(txtSzukajOdbiorcy);
            StyleTextBox(txtSzukajTowaru);
            StyleTextBox(textBoxUwagi);

            // Stylizacja etykiet sum
            if (summaryLabelPalety != null)
            {
                summaryLabelPalety.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
                summaryLabelPalety.ForeColor = Color.FromArgb(75, 85, 99);
            }
            if (summaryLabelPojemniki != null)
            {
                summaryLabelPojemniki.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
                summaryLabelPojemniki.ForeColor = Color.FromArgb(75, 85, 99);
            }
        }

        private void StyleButton(Button? btn, Color bgColor, Color fgColor)
        {
            if (btn == null) return;

            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = bgColor;
            btn.ForeColor = fgColor;
            btn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            btn.Padding = new Padding(16, 8, 16, 8);

            // Efekt hover
            btn.MouseEnter += (s, e) => {
                btn.BackColor = ControlPaint.Dark(bgColor, 0.1f);
            };
            btn.MouseLeave += (s, e) => {
                btn.BackColor = bgColor;
            };
        }

        private void StyleDateTimePicker(DateTimePicker? dtp)
        {
            if (dtp == null) return;
            dtp.Font = new Font("Segoe UI", 10f);
        }

        private void StyleTextBox(TextBox? tb)
        {
            if (tb == null) return;
            tb.Font = new Font("Segoe UI", 10f);
            tb.BorderStyle = BorderStyle.FixedSingle;
        }

        private void Panel_Paint(object? sender, PaintEventArgs e)
        {
            var panel = sender as Panel;
            if (panel == null) return;

            using var pen = new Pen(Color.FromArgb(229, 231, 235), 2);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        }

        private void SzybkiGrid()
        {
            dataGridViewZamowienie.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewZamowienie.AllowUserToAddRows = false;
            dataGridViewZamowienie.AllowUserToDeleteRows = false;
            dataGridViewZamowienie.RowHeadersVisible = false;
            dataGridViewZamowienie.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dataGridViewZamowienie.MultiSelect = true;
            dataGridViewZamowienie.EditMode = DataGridViewEditMode.EditOnKeystroke;
            dataGridViewZamowienie.BackgroundColor = Color.White;
            dataGridViewZamowienie.BorderStyle = BorderStyle.None;
            dataGridViewZamowienie.GridColor = Color.FromArgb(229, 231, 235);
            dataGridViewZamowienie.Font = new Font("Segoe UI", 10f);

            // Stylizacja nagłówków
            dataGridViewZamowienie.EnableHeadersVisualStyles = false;
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(75, 85, 99);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(248, 250, 252);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(75, 85, 99);
            dataGridViewZamowienie.ColumnHeadersHeight = 40;

            // Stylizacja wierszy
            dataGridViewZamowienie.RowTemplate.Height = 36;
            dataGridViewZamowienie.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dataGridViewZamowienie.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 64, 175);
            dataGridViewZamowienie.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);

            // Stylizacja komórek
            dataGridViewZamowienie.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;

            TryEnableDoubleBuffer(dataGridViewZamowienie);
        }

        private static void TryEnableDoubleBuffer(Control c)
        {
            try
            {
                var pi = c.GetType().GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                pi?.SetValue(c, true, null);
            }
            catch { /* Ignoruj błąd */ }
        }

        private void WireShortcuts()
        {
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.S) { e.SuppressKeyPress = true; btnZapisz.PerformClick(); }
                else if (e.KeyCode == Keys.F4) { e.SuppressKeyPress = true; btnPickOdbiorca.PerformClick(); }
                else if (e.KeyCode == Keys.Delete) { e.SuppressKeyPress = true; ZeroSelectedCells(); }
            };
        }

        private void InitDefaults()
        {
            this.Cursor = Cursors.WaitCursor;
            btnZapisz.Enabled = false;
            var dzis = DateTime.Now.Date;
            dateTimePickerSprzedaz.Value = (dzis.DayOfWeek == DayOfWeek.Friday) ? dzis.AddDays(3) : dzis.AddDays(1);
            dateTimePickerGodzinaPrzyjazdu.Value = DateTime.Today.AddHours(8);
            RecalcSum(); // Inicjalizuje sumy na 0
        }

        private void BuildDataTableSchema()
        {
            _dt.Columns.Add("Id", typeof(int));
            _dt.Columns.Add("Kod", typeof(string));
            _dt.Columns.Add("E2", typeof(bool)); // Checkbox dla E2
            _dt.Columns.Add("Palety", typeof(decimal));
            _dt.Columns.Add("Pojemniki", typeof(decimal));
            _dt.Columns.Add("Ilosc", typeof(decimal));
            _dt.Columns.Add("KodTowaru", typeof(string));

            _view = new DataView(_dt);
            dataGridViewZamowienie.DataSource = _view;

            // Konfiguracja kolumn
            dataGridViewZamowienie.Columns["Id"]!.Visible = false;

            var cKod = dataGridViewZamowienie.Columns["Kod"]!;
            cKod.ReadOnly = true;
            cKod.FillWeight = 200;
            cKod.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            cKod.DefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);

            // Kolumna checkbox E2
            var cE2 = dataGridViewZamowienie.Columns["E2"] as DataGridViewCheckBoxColumn;
            if (cE2 != null)
            {
                cE2.HeaderText = "E2";
                cE2.FillWeight = 40;
                cE2.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                cE2.ToolTipText = "Zaznacz dla 40 pojemników na paletę";
            }

            var cPalety = dataGridViewZamowienie.Columns["Palety"]!;
            cPalety.FillWeight = 80;
            cPalety.DefaultCellStyle.Format = "N0";
            cPalety.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            cPalety.DefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);

            var cPojemniki = dataGridViewZamowienie.Columns["Pojemniki"]!;
            cPojemniki.FillWeight = 100;
            cPojemniki.DefaultCellStyle.Format = "N0";
            cPojemniki.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            var cIlosc = dataGridViewZamowienie.Columns["Ilosc"]!;
            cIlosc.FillWeight = 110;
            cIlosc.DefaultCellStyle.Format = "N0";
            cIlosc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            cIlosc.HeaderText = "Ilość (kg)";
            cIlosc.DefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            cIlosc.DefaultCellStyle.ForeColor = Color.FromArgb(34, 197, 94);

            var cKodTowaru = dataGridViewZamowienie.Columns["KodTowaru"]!;
            cKodTowaru.ReadOnly = true;
            cKodTowaru.FillWeight = 200;
            cKodTowaru.HeaderText = "Kod Towaru";
            cKodTowaru.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            cKodTowaru.DefaultCellStyle.ForeColor = Color.FromArgb(107, 114, 128);
        }

        private void WireUpUIEvents()
        {
            dataGridViewZamowienie.CellValueChanged += DataGridViewZamowienie_CellValueChanged;
            dataGridViewZamowienie.EditingControlShowing += DataGridViewZamowienie_EditingControlShowing;
            dataGridViewZamowienie.CellPainting += DataGridViewZamowienie_CellPainting;
            dataGridViewZamowienie.ColumnWidthChanged += (s, e) => dataGridViewZamowienie.Invalidate();
            dataGridViewZamowienie.CurrentCellDirtyStateChanged += DataGridViewZamowienie_CurrentCellDirtyStateChanged;

            txtSzukajOdbiorcy.TextChanged += TxtSzukajOdbiorcy_TextChanged;
            txtSzukajOdbiorcy.KeyDown += TxtSzukajOdbiorcy_KeyDown;
            listaWynikowOdbiorcy.DoubleClick += ListaWynikowOdbiorcy_DoubleClick;
            listaWynikowOdbiorcy.KeyDown += ListaWynikowOdbiorcy_KeyDown;

            txtSzukajTowaru.TextChanged += TxtSzukajTowaru_TextChanged;
            btnPickOdbiorca.Click += (s, e) => OpenKontrahentPicker();

            var hands = _kontrahenci.Select(k => k.Handlowiec).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();
            hands.Insert(0, "— Wszyscy —");
            cbHandlowiecFilter.Items.Clear();
            cbHandlowiecFilter.Items.AddRange(hands.ToArray());
            cbHandlowiecFilter.SelectedIndex = 0;
            cbHandlowiecFilter.SelectedIndexChanged += (s, e) => TxtSzukajOdbiorcy_TextChanged(null, EventArgs.Empty);
        }

        // Obsługa checkboxa E2 - natychmiastowy commit
        private void DataGridViewZamowienie_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (dataGridViewZamowienie.CurrentCell is DataGridViewCheckBoxCell)
            {
                dataGridViewZamowienie.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        #endregion

        #region Asynchroniczne Ładowanie Danych

        private async Task LoadInitialDataInBackground()
        {
            var towaryTask = LoadTowaryAsRowsAsync();
            var kontrahenciTask = LoadKontrahenciAsync();
            await Task.WhenAll(towaryTask, kontrahenciTask);
        }

        private async Task LoadTowaryAsRowsAsync()
        {
            _dt.Clear();
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT Id, Kod FROM [HANDEL].[HM].[TW] WHERE katalog = '67095' ORDER BY Kod ASC", cn);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var kod = rd.GetString(1);
                _dt.Rows.Add(rd.GetInt32(0), kod, false, 0m, 0m, 0m, kod); // false dla E2
            }
        }

        private async Task LoadKontrahenciAsync()
        {
            const string sql = @"
                SELECT
                    c.Id,
                    c.Shortcut AS Nazwa,
                    c.NIP,
                    poa.Postcode AS KodPocztowy,
                    poa.Street AS Miejscowosc, 
                    wym.CDim_Handlowiec_Val AS Handlowiec
                FROM
                    [HANDEL].[SSCommon].[STContractors] c
                LEFT JOIN
                    [HANDEL].[SSCommon].[STPostOfficeAddresses] poa ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'
                LEFT JOIN
                    [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId
                ORDER BY
                    c.Shortcut;";

            _kontrahenci.Clear();
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            await using var rd = await cmd.ExecuteReaderAsync();

            while (await rd.ReadAsync())
            {
                _kontrahenci.Add(new KontrahentInfo
                {
                    Id = rd["Id"]?.ToString() ?? "",
                    Nazwa = rd["Nazwa"]?.ToString() ?? "",
                    NIP = rd["NIP"]?.ToString() ?? "",
                    KodPocztowy = rd["KodPocztowy"]?.ToString() ?? "",
                    Miejscowosc = rd["Miejscowosc"]?.ToString() ?? "",
                    Handlowiec = rd["Handlowiec"]?.ToString() ?? ""
                });
            }
        }

        #endregion

        #region Logika Biznesowa i Zdarzenia UI

        // --- Wybór Odbiorcy ---
        private void TxtSzukajOdbiorcy_TextChanged(object? sender, EventArgs e)
        {
            var query = txtSzukajOdbiorcy.Text.Trim().ToLower();
            var handlowiec = cbHandlowiecFilter.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(query) && handlowiec == "— Wszyscy —")
            {
                listaWynikowOdbiorcy.Visible = false;
                return;
            }

            IEnumerable<KontrahentInfo> zrodlo = _kontrahenci;

            if (handlowiec != null && handlowiec != "— Wszyscy —")
            {
                zrodlo = zrodlo.Where(k => k.Handlowiec == handlowiec);
            }

            var wyniki = zrodlo
                .Where(k => k.Nazwa.ToLower().Contains(query) || k.Miejscowosc.ToLower().Contains(query) || k.NIP.Contains(query))
                .Take(10)
                .ToList();

            listaWynikowOdbiorcy.DataSource = wyniki;
            listaWynikowOdbiorcy.DisplayMember = "Nazwa";
            listaWynikowOdbiorcy.ValueMember = "Id";
            listaWynikowOdbiorcy.Visible = wyniki.Any();
        }

        private void TxtSzukajOdbiorcy_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down && listaWynikowOdbiorcy.Visible && listaWynikowOdbiorcy.Items.Count > 0)
            {
                listaWynikowOdbiorcy.Focus();
                listaWynikowOdbiorcy.SelectedIndex = 0;
            }
            else if (e.KeyCode == Keys.Enter && listaWynikowOdbiorcy.Visible && listaWynikowOdbiorcy.Items.Count > 0)
            {
                WybierzOdbiorceZListy();
                e.SuppressKeyPress = true;
            }
        }

        private void ListaWynikowOdbiorcy_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                WybierzOdbiorceZListy();
                e.SuppressKeyPress = true;
            }
        }

        private void ListaWynikowOdbiorcy_DoubleClick(object? sender, EventArgs e) => WybierzOdbiorceZListy();

        private void WybierzOdbiorceZListy()
        {
            if (listaWynikowOdbiorcy.SelectedItem is KontrahentInfo wybrany)
            {
                UstawOdbiorce(wybrany.Id);
            }
        }

        private void OpenKontrahentPicker()
        {
            using var dlg = new KontrahentPicker(_kontrahenci, cbHandlowiecFilter.SelectedItem?.ToString());
            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedId))
            {
                UstawOdbiorce(dlg.SelectedId);
            }
        }

        private void UstawOdbiorce(string id)
        {
            _selectedKlientId = id;
            var info = _kontrahenci.FirstOrDefault(k => k.Id == id);
            if (info != null)
            {
                txtSzukajOdbiorcy.Text = info.Nazwa;
                listaWynikowOdbiorcy.Visible = false;
                panelDaneOdbiorcy.Visible = true;
                lblWybranyOdbiorca.Text = info.Nazwa;
                lblNip.Text = $"NIP: {info.NIP}";
                lblAdres.Text = $"{info.KodPocztowy} {info.Miejscowosc}";
                lblHandlowiec.Text = $"Opiekun: {info.Handlowiec}";
                txtSzukajTowaru.Focus();
            }
        }

        // --- Grid i Towary ---
        private void TxtSzukajTowaru_TextChanged(object? sender, EventArgs e)
        {
            var q = (sender as TextBox)?.Text?.Trim().Replace("'", "''") ?? "";
            _view.RowFilter = string.IsNullOrEmpty(q) ? string.Empty : $"Kod LIKE '%{q}%'";
        }

        private void DataGridViewZamowienie_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_blokujObslugeZmian || e.RowIndex < 0) return;

            var row = (dataGridViewZamowienie.Rows[e.RowIndex].DataBoundItem as DataRowView)?.Row;
            if (row == null) return;

            _blokujObslugeZmian = true;

            string changedColumnName = dataGridViewZamowienie.Columns[e.ColumnIndex].Name;

            // Sprawdź czy używać E2 dla tego towaru
            bool useE2 = row.Field<bool>("E2");
            decimal pojemnikNaPalete = useE2 ? POJEMNIKOW_NA_PALECIE_E2 : POJEMNIKOW_NA_PALECIE;
            decimal kgNaPalete = useE2 ? KG_NA_PALECIE_E2 : KG_NA_PALECIE;

            try
            {
                switch (changedColumnName)
                {
                    case "E2":
                        // Przelicz palety przy zmianie checkboxa
                        decimal currentIlosc = ParseDec(row["Ilosc"]);
                        if (currentIlosc > 0)
                        {
                            row["Palety"] = Math.Round(currentIlosc / kgNaPalete, 0);
                        }
                        break;

                    case "Ilosc":
                        decimal ilosc = ParseDec(row["Ilosc"]);
                        row["Pojemniki"] = (ilosc > 0 && KG_NA_POJEMNIKU > 0) ? Math.Round(ilosc / KG_NA_POJEMNIKU, 0) : 0m;
                        row["Palety"] = (ilosc > 0 && kgNaPalete > 0) ? Math.Round(ilosc / kgNaPalete, 0) : 0m;
                        MarkInvalid(dataGridViewZamowienie.Rows[e.RowIndex].Cells["Ilosc"], ilosc < 0);
                        break;

                    case "Pojemniki":
                        decimal pojemniki = ParseDec(row["Pojemniki"]);
                        row["Ilosc"] = pojemniki * KG_NA_POJEMNIKU;
                        row["Palety"] = (pojemniki > 0 && pojemnikNaPalete > 0) ? Math.Round(pojemniki / pojemnikNaPalete, 0) : 0m;
                        break;

                    case "Palety":
                        decimal palety = ParseDec(row["Palety"]);
                        row["Pojemniki"] = palety * pojemnikNaPalete;
                        row["Ilosc"] = palety * kgNaPalete;
                        break;
                }
            }
            finally
            {
                _blokujObslugeZmian = false;
            }
            RecalcSum();
        }

        private void DataGridViewZamowienie_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is TextBox tb)
            {
                tb.KeyPress -= OnlyNumeric_KeyPress;
                tb.KeyPress += OnlyNumeric_KeyPress;
            }
        }

        private void ZeroSelectedCells()
        {
            _blokujObslugeZmian = true;
            foreach (DataGridViewCell c in dataGridViewZamowienie.SelectedCells)
            {
                var row = (c.OwningRow.DataBoundItem as DataRowView)?.Row;
                if (row == null) continue;

                row["Palety"] = 0m;
                row["Pojemniki"] = 0m;
                row["Ilosc"] = 0m;
            }
            _blokujObslugeZmian = false;
            RecalcSum();
        }

        private void RecalcSum()
        {
            decimal sumaIlosc = 0m;
            decimal sumaPalety = 0m;
            decimal sumaPojemniki = 0m;

            foreach (DataRow row in _dt.Rows)
            {
                sumaIlosc += row.Field<decimal?>("Ilosc") ?? 0m;
                sumaPojemniki += row.Field<decimal?>("Pojemniki") ?? 0m;

                // Dla palet uwzględnij typ E2
                bool useE2 = row.Field<bool>("E2");
                decimal ilosc = row.Field<decimal?>("Ilosc") ?? 0m;
                decimal kgNaPalete = useE2 ? KG_NA_PALECIE_E2 : KG_NA_PALECIE;
                if (ilosc > 0 && kgNaPalete > 0)
                {
                    sumaPalety += Math.Round(ilosc / kgNaPalete, 0);
                }
            }

            summaryLabelPalety.Text = $"🎯 {sumaPalety:N0} Palet";
            summaryLabelPojemniki.Text = $"📦 {sumaPojemniki:N0} Pojemników";
        }

        private decimal ParseDec(object? v)
        {
            var s = v?.ToString()?.Trim();
            if (string.IsNullOrEmpty(s)) return 0m;
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Number, _pl, out var d)) return d;
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d2)) return d2;
            return 0m;
        }

        private void MarkInvalid(DataGridViewCell cell, bool invalid) => cell.Style.BackColor = invalid ? Color.FromArgb(254, 202, 202) : dataGridViewZamowienie.DefaultCellStyle.BackColor;

        private void OnlyNumeric_KeyPress(object? sender, KeyPressEventArgs e)
        {
            char dec = _pl.NumberFormat.NumberDecimalSeparator[0];
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != dec) e.Handled = true;
            if (e.KeyChar == dec && sender is TextBox tb && tb.Text.Contains(dec)) e.Handled = true;
        }

        // --- Nowa metoda czyszczenia formularza ---
        private void ClearFormForNewOrder()
        {
            // Resetuj ID do null (nowe zamówienie)
            _idZamowieniaDoEdycji = null;

            // Wyczyść odbiorcę
            _selectedKlientId = null;
            txtSzukajOdbiorcy.Text = "";
            panelDaneOdbiorcy.Visible = false;
            listaWynikowOdbiorcy.Visible = false;

            // Wyczyść filtr towarów
            txtSzukajTowaru.Text = "";
            _view.RowFilter = string.Empty;

            // Wyczyść ilości i checkboxy E2 w gridzie
            _blokujObslugeZmian = true;
            foreach (DataRow r in _dt.Rows)
            {
                r["E2"] = false;
                r["Ilosc"] = 0m;
                r["Pojemniki"] = 0m;
                r["Palety"] = 0m;
            }
            _blokujObslugeZmian = false;

            // Wyczyść uwagi
            textBoxUwagi.Text = "";

            // Ustaw domyślne daty
            var dzis = DateTime.Now.Date;
            dateTimePickerSprzedaz.Value = (dzis.DayOfWeek == DayOfWeek.Friday) ? dzis.AddDays(3) : dzis.AddDays(1);
            dateTimePickerGodzinaPrzyjazdu.Value = DateTime.Today.AddHours(8);

            // Zaktualizuj tytuł i przycisk
            lblTytul.Text = "Nowe zamówienie mięsa";
            btnZapisz.Text = "Zapisz (Ctrl+S)";

            // Odśwież sumy
            RecalcSum();

            // Ustaw focus na pole odbiorcy
            txtSzukajOdbiorcy.Focus();
        }

        #endregion

        #region Zapis i Odczyt Zamówienia (Async)

        private async Task LoadZamowienieAsync(int id)
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            await using (var cmdZ = new SqlCommand("SELECT DataZamowienia, KlientId, Uwagi, DataPrzyjazdu FROM [dbo].[ZamowieniaMieso] WHERE Id=@Id", cn))
            {
                cmdZ.Parameters.AddWithValue("@Id", id);
                await using var rd = await cmdZ.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    dateTimePickerSprzedaz.Value = rd.GetDateTime(0);
                    UstawOdbiorce(rd.GetInt32(1).ToString());
                    textBoxUwagi.Text = await rd.IsDBNullAsync(2) ? "" : rd.GetString(2);
                    dateTimePickerGodzinaPrzyjazdu.Value = rd.GetDateTime(3);
                }
            }

            _blokujObslugeZmian = true;
            foreach (DataRow r in _dt.Rows)
            {
                r["E2"] = false;
                r["Ilosc"] = 0m;
                r["Pojemniki"] = 0m;
                r["Palety"] = 0m;
            }

            await using (var cmdT = new SqlCommand("SELECT KodTowaru, Ilosc FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId=@Id", cn))
            {
                cmdT.Parameters.AddWithValue("@Id", id);
                await using var rd = await cmdT.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int towarId = rd.GetInt32(0);
                    var rows = _dt.Select($"Id = {towarId}");
                    if (rows.Any())
                    {
                        decimal ilosc = await rd.IsDBNullAsync(1) ? 0m : rd.GetDecimal(1);
                        rows[0]["Ilosc"] = ilosc;
                        rows[0]["Pojemniki"] = (ilosc > 0 && KG_NA_POJEMNIKU > 0) ? Math.Round(ilosc / KG_NA_POJEMNIKU, 0) : 0m;
                        rows[0]["Palety"] = (ilosc > 0 && KG_NA_PALECIE > 0) ? Math.Round(ilosc / KG_NA_PALECIE, 0) : 0m;
                    }
                }
            }
            _blokujObslugeZmian = false;
            RecalcSum();
        }

        private bool ValidateBeforeSave(out string message)
        {
            if (string.IsNullOrWhiteSpace(_selectedKlientId))
            {
                message = "Wybierz odbiorcę.";
                return false;
            }
            if (!_dt.AsEnumerable().Any(r => r.Field<decimal>("Ilosc") > 0m))
            {
                message = "Wpisz ilość (>0) dla przynajmniej jednego towaru.";
                return false;
            }
            if (_dt.AsEnumerable().Any(r => r.Field<decimal>("Ilosc") < 0m))
            {
                message = "Ilość nie może być ujemna.";
                return false;
            }
            message = "";
            return true;
        }

        private async void btnZapisz_Click(object? sender, EventArgs e)
        {
            if (!ValidateBeforeSave(out var msg))
            {
                MessageBox.Show(msg, "Błąd danych", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Cursor = Cursors.WaitCursor;
            btnZapisz.Enabled = false;

            try
            {
                await SaveOrderAsync();
                string summary = BuildOrderSummary();
                string title = _idZamowieniaDoEdycji.HasValue ? "✅ Zamówienie zaktualizowane" : "✅ Zamówienie zapisane";

                // Pokaż informację o zapisie
                MessageBox.Show(summary, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

                // ZMIANA: Zamiast zamykać okno, wyczyść formularz dla nowego zamówienia
                ClearFormForNewOrder();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu: " + ex.Message, "❌ Błąd Krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                btnZapisz.Enabled = true;
            }
        }

        private string BuildOrderSummary()
        {
            var sb = new StringBuilder();
            var orderedItems = _dt.AsEnumerable()
                .Where(r => r.Field<decimal?>("Ilosc") > 0m)
                .ToList();

            sb.AppendLine($"📍 Odbiorca: {lblWybranyOdbiorca.Text}");
            sb.AppendLine($"📅 Data sprzedaży: {dateTimePickerSprzedaz.Value:yyyy-MM-dd}");

            // Sprawdź czy są towary E2
            var e2Items = orderedItems.Where(r => r.Field<bool>("E2")).ToList();
            if (e2Items.Any())
            {
                sb.AppendLine($"ℹ️ Towary E2 (40 poj./pal.): {e2Items.Count}");
            }

            sb.AppendLine("\n📦 Zamówione towary:");

            foreach (var item in orderedItems)
            {
                string e2Marker = item.Field<bool>("E2") ? " [E2]" : "";
                sb.AppendLine($"  • {item.Field<string>("Kod")}{e2Marker}: {item.Field<decimal>("Ilosc"):N0} kg");
            }

            // Podsumowanie
            decimal totalKg = orderedItems.Sum(r => r.Field<decimal>("Ilosc"));
            decimal totalPojemniki = orderedItems.Sum(r => r.Field<decimal>("Pojemniki"));
            sb.AppendLine($"\n📊 Podsumowanie:");
            sb.AppendLine($"  • Łącznie: {totalKg:N0} kg");
            sb.AppendLine($"  • Pojemników: {totalPojemniki:N0}");

            return sb.ToString();
        }

        private async Task SaveOrderAsync()
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var tr = (SqlTransaction)await cn.BeginTransactionAsync();

            int orderId;

            if (_idZamowieniaDoEdycji.HasValue)
            {
                orderId = _idZamowieniaDoEdycji.Value;
                var cmdUpdate = new SqlCommand(@"UPDATE [dbo].[ZamowieniaMieso] SET DataZamowienia = @dz, DataPrzyjazdu = @dp, KlientId = @kid, Uwagi = @uw, KtoMod = @km, KiedyMod = SYSDATETIME() WHERE Id=@id", cn, tr);
                cmdUpdate.Parameters.AddWithValue("@dz", dateTimePickerSprzedaz.Value.Date);
                cmdUpdate.Parameters.AddWithValue("@dp", dateTimePickerGodzinaPrzyjazdu.Value);
                cmdUpdate.Parameters.AddWithValue("@kid", _selectedKlientId!);
                cmdUpdate.Parameters.AddWithValue("@uw", string.IsNullOrWhiteSpace(textBoxUwagi.Text) ? DBNull.Value : textBoxUwagi.Text);
                cmdUpdate.Parameters.AddWithValue("@km", UserID);
                cmdUpdate.Parameters.AddWithValue("@id", orderId);
                await cmdUpdate.ExecuteNonQueryAsync();

                var cmdDelete = new SqlCommand(@"DELETE FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId=@id", cn, tr);
                cmdDelete.Parameters.AddWithValue("@id", orderId);
                await cmdDelete.ExecuteNonQueryAsync();
            }
            else
            {
                var cmdGetId = new SqlCommand(@"SELECT ISNULL(MAX(Id),0)+1 FROM [dbo].[ZamowieniaMieso]", cn, tr);
                orderId = Convert.ToInt32(await cmdGetId.ExecuteScalarAsync());

                var cmdInsert = new SqlCommand(@"INSERT INTO [dbo].[ZamowieniaMieso] (Id, DataZamowienia, DataPrzyjazdu, KlientId, Uwagi, IdUser) VALUES (@id, @dz, @dp, @kid, @uw, @u)", cn, tr);
                cmdInsert.Parameters.AddWithValue("@id", orderId);
                cmdInsert.Parameters.AddWithValue("@dz", dateTimePickerSprzedaz.Value.Date);
                cmdInsert.Parameters.AddWithValue("@dp", dateTimePickerGodzinaPrzyjazdu.Value);
                cmdInsert.Parameters.AddWithValue("@kid", _selectedKlientId!);
                cmdInsert.Parameters.AddWithValue("@uw", string.IsNullOrWhiteSpace(textBoxUwagi.Text) ? DBNull.Value : textBoxUwagi.Text);
                cmdInsert.Parameters.AddWithValue("@u", UserID);
                await cmdInsert.ExecuteNonQueryAsync();
            }

            var cmdInsertItem = new SqlCommand(@"INSERT INTO [dbo].[ZamowieniaMiesoTowar] (ZamowienieId, KodTowaru, Ilosc, Cena) VALUES (@zid, @kt, @il, @ce)", cn, tr);
            cmdInsertItem.Parameters.Add("@zid", SqlDbType.Int);
            cmdInsertItem.Parameters.Add("@kt", SqlDbType.Int);
            cmdInsertItem.Parameters.Add("@il", SqlDbType.Decimal);
            cmdInsertItem.Parameters.Add("@ce", SqlDbType.Decimal);

            foreach (DataRow r in _dt.Rows)
            {
                if (r.Field<decimal>("Ilosc") <= 0m) continue;

                cmdInsertItem.Parameters["@zid"].Value = orderId;
                cmdInsertItem.Parameters["@kt"].Value = r.Field<int>("Id");
                cmdInsertItem.Parameters["@il"].Value = r.Field<decimal>("Ilosc");
                cmdInsertItem.Parameters["@ce"].Value = 0m;
                await cmdInsertItem.ExecuteNonQueryAsync();
            }

            await tr.CommitAsync();
        }

        #endregion

        #region Rysowanie Ikon w Nagłówkach

        private void CreateHeaderIcons()
        {
            _headerIcons["Kod"] = CreateIconForText("PROD");
            _headerIcons["Palety"] = CreatePalletIcon();
            _headerIcons["Pojemniki"] = CreateContainerIcon();
            _headerIcons["Ilosc"] = CreateScaleIcon();
            _headerIcons["KodTowaru"] = CreateIconForText("KOD");
        }

        private void DataGridViewZamowienie_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex < 0) return;

            string colName = dataGridViewZamowienie.Columns[e.ColumnIndex].Name;
            if (!_headerIcons.ContainsKey(colName)) return;

            e.PaintBackground(e.CellBounds, true);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var icon = _headerIcons[colName];

            int y = e.CellBounds.Y + (e.CellBounds.Height - icon.Height) / 2;
            g.DrawImage(icon, e.CellBounds.X + 6, y);

            var textBounds = new Rectangle(
                e.CellBounds.X + icon.Width + 12,
                e.CellBounds.Y,
                e.CellBounds.Width - icon.Width - 18,
                e.CellBounds.Height);

            TextRenderer.DrawText(g, e.Value?.ToString(), e.CellStyle.Font, textBounds, e.CellStyle.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

            e.Handled = true;
        }

        private Image CreatePalletIcon()
        {
            var bmp = new Bitmap(20, 20);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var brownPen = new Pen(Color.FromArgb(146, 64, 14), 2);
            using var brownBrush = new SolidBrush(Color.FromArgb(196, 110, 54));
            g.FillRectangle(brownBrush, 3, 10, 14, 7);
            g.DrawRectangle(brownPen, 3, 10, 14, 7);
            g.DrawLine(brownPen, 3, 13, 17, 13);
            g.FillRectangle(Brushes.Peru, 5, 5, 3, 5);
            g.FillRectangle(Brushes.Peru, 12, 5, 3, 5);
            return bmp;
        }

        private Image CreateContainerIcon()
        {
            var bmp = new Bitmap(20, 20);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var grayBrush = new LinearGradientBrush(new Rectangle(3, 5, 14, 10), Color.FromArgb(156, 163, 175), Color.FromArgb(107, 114, 128), 90f);
            g.FillRectangle(grayBrush, 3, 5, 14, 10);
            g.DrawRectangle(Pens.DimGray, 3, 5, 14, 10);
            return bmp;
        }

        private Image CreateScaleIcon()
        {
            var bmp = new Bitmap(20, 20);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var darkGrayPen = new Pen(Color.FromArgb(75, 85, 99), 2);
            g.DrawLine(darkGrayPen, 3, 16, 17, 16);
            g.DrawLine(darkGrayPen, 10, 16, 10, 6);
            g.DrawLine(darkGrayPen, 4, 7, 16, 7);
            using var greenBrush = new SolidBrush(Color.FromArgb(34, 197, 94));
            g.FillEllipse(greenBrush, 8, 3, 4, 4);
            return bmp;
        }

        private Image CreateIconForText(string text)
        {
            var bmp = new Bitmap(20, 20);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var font = new Font("Segoe UI", 8, FontStyle.Bold);
            TextRenderer.DrawText(g, text, font, new Point(0, 3), Color.FromArgb(107, 114, 128));
            return bmp;
        }

        #endregion

        // ===== Wewnętrzna Klasa Pickera Kontrahentów =====
        private sealed class KontrahentPicker : Form
        {
            private readonly DataView _view;
            private readonly TextBox _tbFilter = new() { Dock = DockStyle.Fill, PlaceholderText = "Szukaj: nazwa / NIP / miejscowość..." };
            private readonly ComboBox _cbHandlowiec = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            public string? SelectedId { get; private set; }

            public KontrahentPicker(List<KontrahentInfo> src, string? handlowiecPreselected)
            {
                Text = "Wybierz odbiorcę";
                StartPosition = FormStartPosition.CenterParent;
                MinimumSize = new Size(920, 640);
                BackColor = Color.FromArgb(245, 247, 250);

                var table = new DataTable();
                table.Columns.Add("Id", typeof(string));
                table.Columns.Add("Nazwa", typeof(string));
                table.Columns.Add("NIP", typeof(string));
                table.Columns.Add("KodPocztowy", typeof(string));
                table.Columns.Add("Miejscowosc", typeof(string));
                table.Columns.Add("Handlowiec", typeof(string));
                foreach (var k in src) table.Rows.Add(k.Id, k.Nazwa, k.NIP, k.KodPocztowy, k.Miejscowosc, k.Handlowiec);

                _view = new DataView(table);
                _grid.DataSource = _view;
                _grid.Font = new Font("Segoe UI", 10f);
                _grid.EnableHeadersVisualStyles = false;
                _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
                _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(75, 85, 99);
                _grid.RowTemplate.Height = 32;
                _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
                _grid.BackgroundColor = Color.White;
                _grid.GridColor = Color.FromArgb(229, 231, 235);
                _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
                _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 64, 175);
                _grid.Columns["Id"]!.Visible = false;

                var bar = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, Padding = new Padding(12), AutoSize = true, BackColor = Color.White };
                bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

                var lblF = new Label { Text = "Filtr:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 8, 6, 0), Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
                var lblH = new Label { Text = "Handlowiec:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(12, 8, 6, 0), Font = new Font("Segoe UI", 10f, FontStyle.Bold) };

                _tbFilter.Font = new Font("Segoe UI", 10f);
                _cbHandlowiec.Font = new Font("Segoe UI", 10f);

                bar.Controls.Add(lblF, 0, 0);
                bar.Controls.Add(_tbFilter, 1, 0);
                bar.Controls.Add(lblH, 2, 0);
                bar.Controls.Add(_cbHandlowiec, 3, 0);

                var hands = src.Select(k => k.Handlowiec).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();
                hands.Insert(0, "— Wszyscy —");
                _cbHandlowiec.Items.AddRange(hands.ToArray());
                if (!string.IsNullOrWhiteSpace(handlowiecPreselected) && _cbHandlowiec.Items.IndexOf(handlowiecPreselected) is var idx && idx >= 0)
                {
                    _cbHandlowiec.SelectedIndex = idx;
                }
                else
                {
                    _cbHandlowiec.SelectedIndex = 0;
                }

                var ok = new Button { Text = "✅ Wybierz", AutoSize = true, Padding = new Padding(16, 10, 16, 10), DialogResult = DialogResult.OK, BackColor = Color.FromArgb(34, 197, 94), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Cursor = Cursors.Hand };
                ok.FlatAppearance.BorderSize = 0;

                var cancel = new Button { Text = "Anuluj", AutoSize = true, Padding = new Padding(16, 10, 16, 10), DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(156, 163, 175), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10f), Cursor = Cursors.Hand };
                cancel.FlatAppearance.BorderSize = 0;

                ok.Click += (s, e) => { if (_grid.CurrentRow != null) SelectedId = _grid.CurrentRow.Cells["Id"].Value?.ToString(); };

                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), AutoSize = true, BackColor = Color.White };
                buttons.Controls.Add(ok);
                buttons.Controls.Add(cancel);

                Controls.Add(_grid);
                Controls.Add(bar);
                Controls.Add(buttons);

                _tbFilter.TextChanged += (s, e) => ApplyFilter();
                _cbHandlowiec.SelectedIndexChanged += (s, e) => ApplyFilter();
                _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) ok.PerformClick(); };
                AcceptButton = ok;
                CancelButton = cancel;
                ApplyFilter();
            }

            private void ApplyFilter()
            {
                var txt = _tbFilter.Text?.Trim().Replace("'", "''") ?? "";
                var hand = _cbHandlowiec.SelectedItem?.ToString();
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(txt)) parts.Add($"(Nazwa LIKE '%{txt}%' OR NIP LIKE '%{txt}%' OR Miejscowosc LIKE '%{txt}%')");
                if (!string.IsNullOrWhiteSpace(hand) && hand != "— Wszyscy —") parts.Add($"Handlowiec = '{hand.Replace("'", "''")}'");
                _view.RowFilter = string.Join(" AND ", parts);
            }
        }
    }
}