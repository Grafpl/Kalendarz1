// Plik: WidokZamowienia.cs
// WERSJA 8.1 - NAPRAWA BŁĘDÓW KOMPILACJI
// Zmiany:
// 1. Poprawiono błąd `InvalidateHeader` na `Invalidate`, aby odświeżanie nagłówków działało poprawnie.
// 2. Plik jest teraz w pełni zgodny z poprawionym plikiem Designera.

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
        private readonly int? _idZamowieniaDoEdycji;

        // ===== Połączenia z Bazą =====
        private readonly string _connLibra;
        private readonly string _connHandel;

        // ===== Stałe przeliczeniowe =====
        private const decimal POJEMNIKOW_NA_PALECIE = 36m;
        private const decimal KG_NA_POJEMNIKU = 15m;
        private const decimal KG_NA_PALECIE = POJEMNIKOW_NA_PALECIE * KG_NA_POJEMNIKU; // 540

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

        // ===== GŁÓWNA METODA ŁADUJĄCA (ASYynchroniczna) =====
        private async void WidokZamowienia_Load(object? sender, EventArgs e)
        {
            CreateHeaderIcons();
            SzybkiGrid();
            WireShortcuts();
            BuildDataTableSchema();
            InitDefaults();

            try
            {
                await LoadInitialDataInBackground();
                WireUpUIEvents();

                if (_idZamowieniaDoEdycji.HasValue)
                {
                    await LoadZamowienieAsync(_idZamowieniaDoEdycji.Value);
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
            dataGridViewZamowienie.GridColor = Color.FromArgb(224, 224, 224);
            dataGridViewZamowienie.Font = new Font("Segoe UI", 11f);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(242, 242, 242);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            dataGridViewZamowienie.RowTemplate.Height = 32;
            dataGridViewZamowienie.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
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
            _dt.Columns.Add("Palety", typeof(decimal));
            _dt.Columns.Add("Pojemniki", typeof(decimal));
            _dt.Columns.Add("Ilosc", typeof(decimal));
            _dt.Columns.Add("KodTowaru", typeof(string)); // Duplikat
            _view = new DataView(_dt);
            dataGridViewZamowienie.DataSource = _view;

            // Konfiguracja i kolejność kolumn
            dataGridViewZamowienie.Columns["Id"]!.Visible = false;

            var cKod = dataGridViewZamowienie.Columns["Kod"]!;
            cKod.ReadOnly = true;
            cKod.FillWeight = 250;
            cKod.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            var cPalety = dataGridViewZamowienie.Columns["Palety"]!;
            cPalety.FillWeight = 90;
            cPalety.DefaultCellStyle.Format = "N0";
            cPalety.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            var cPojemniki = dataGridViewZamowienie.Columns["Pojemniki"]!;
            cPojemniki.FillWeight = 110;
            cPojemniki.DefaultCellStyle.Format = "N0";
            cPojemniki.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            var cIlosc = dataGridViewZamowienie.Columns["Ilosc"]!;
            cIlosc.FillWeight = 120;
            cIlosc.DefaultCellStyle.Format = "N0";
            cIlosc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            cIlosc.HeaderText = "Ilość (kg)";
            cIlosc.DefaultCellStyle.Font = new Font(dataGridViewZamowienie.Font, FontStyle.Bold);

            var cKodTowaru = dataGridViewZamowienie.Columns["KodTowaru"]!;
            cKodTowaru.ReadOnly = true;
            cKodTowaru.FillWeight = 250;
            cKodTowaru.HeaderText = "Kod Towaru";
            cKodTowaru.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        }

        private void WireUpUIEvents()
        {
            dataGridViewZamowienie.CellValueChanged += DataGridViewZamowienie_CellValueChanged;
            dataGridViewZamowienie.EditingControlShowing += DataGridViewZamowienie_EditingControlShowing;
            dataGridViewZamowienie.CellPainting += DataGridViewZamowienie_CellPainting;
            dataGridViewZamowienie.ColumnWidthChanged += (s, e) => dataGridViewZamowienie.Invalidate(); // POPRAWKA


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
                _dt.Rows.Add(rd.GetInt32(0), kod, 0m, 0m, 0m, kod);
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

            try
            {
                switch (changedColumnName)
                {
                    case "Ilosc":
                        decimal ilosc = ParseDec(row["Ilosc"]);
                        row["Pojemniki"] = (ilosc > 0 && KG_NA_POJEMNIKU > 0) ? Math.Round(ilosc / KG_NA_POJEMNIKU, 2) : 0m;
                        row["Palety"] = (ilosc > 0 && KG_NA_PALECIE > 0) ? Math.Round(ilosc / KG_NA_PALECIE, 2) : 0m;
                        MarkInvalid(dataGridViewZamowienie.Rows[e.RowIndex].Cells["Ilosc"], ilosc < 0);
                        break;
                    case "Pojemniki":
                        decimal pojemniki = ParseDec(row["Pojemniki"]);
                        row["Ilosc"] = pojemniki * KG_NA_POJEMNIKU;
                        row["Palety"] = (pojemniki > 0 && POJEMNIKOW_NA_PALECIE > 0) ? Math.Round(pojemniki / POJEMNIKOW_NA_PALECIE, 2) : 0m;
                        break;
                    case "Palety":
                        decimal palety = ParseDec(row["Palety"]);
                        row["Pojemniki"] = palety * POJEMNIKOW_NA_PALECIE;
                        row["Ilosc"] = palety * KG_NA_PALECIE;
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
            decimal sumaIlosc = _dt.AsEnumerable().Sum(r => r.Field<decimal?>("Ilosc") ?? 0m);
            decimal sumaPalety = _dt.AsEnumerable().Sum(r => r.Field<decimal?>("Palety") ?? 0m);
            decimal sumaPojemniki = _dt.AsEnumerable().Sum(r => r.Field<decimal?>("Pojemniki") ?? 0m);

            //summaryLabelKg.Text = $"{sumaIlosc:N0} kg Towaru";
            summaryLabelPalety.Text = $"{sumaPalety:N0} Palety";
            summaryLabelPojemniki.Text = $"{sumaPojemniki:N0} Pojemników";
        }

        private decimal ParseDec(object? v)
        {
            var s = v?.ToString()?.Trim();
            if (string.IsNullOrEmpty(s)) return 0m;
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Number, _pl, out var d)) return d;
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d2)) return d2;
            return 0m;
        }

        private void MarkInvalid(DataGridViewCell cell, bool invalid) => cell.Style.BackColor = invalid ? Color.MistyRose : dataGridViewZamowienie.DefaultCellStyle.BackColor;

        private void OnlyNumeric_KeyPress(object? sender, KeyPressEventArgs e)
        {
            char dec = _pl.NumberFormat.NumberDecimalSeparator[0];
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != dec) e.Handled = true;
            if (e.KeyChar == dec && sender is TextBox tb && tb.Text.Contains(dec)) e.Handled = true;
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
                        rows[0]["Pojemniki"] = (ilosc > 0 && KG_NA_POJEMNIKU > 0) ? Math.Round(ilosc / KG_NA_POJEMNIKU, 2) : 0m;
                        rows[0]["Palety"] = (ilosc > 0 && KG_NA_PALECIE > 0) ? Math.Round(ilosc / KG_NA_PALECIE, 2) : 0m;
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
            this.Enabled = false;

            try
            {
                await SaveOrderAsync();
                string summary = BuildOrderSummary();
                string title = _idZamowieniaDoEdycji.HasValue ? "Zamówienie zaktualizowane" : "Zamówienie zapisane";
                string message = $"Pomyślnie zapisano następujące pozycje:\n\n{summary}";
                MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd Krytyczny", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                this.Enabled = true;
            }
        }

        private string BuildOrderSummary()
        {
            var sb = new StringBuilder();
            var orderedItems = _dt.AsEnumerable()
                .Where(r => r.Field<decimal?>("Ilosc") > 0m)
                .ToList();

            foreach (var item in orderedItems)
            {
                sb.AppendLine($"- {item.Field<string>("Kod")}: {item.Field<decimal>("Ilosc"):N0} kg");
            }

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
            _headerIcons["KodTowaru"] = CreateIconForText("PROD");
        }

        private void DataGridViewZamowienie_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex < 0) return;

            string colName = dataGridViewZamowienie.Columns[e.ColumnIndex].Name;
            if (!_headerIcons.ContainsKey(colName)) return;

            e.PaintBackground(e.CellBounds, true);

            var g = e.Graphics;
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
            var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            using var brownPen = new Pen(Color.SaddleBrown, 2);
            g.DrawRectangle(brownPen, 2, 8, 12, 6);
            g.DrawLine(brownPen, 2, 11, 14, 11);
            g.FillRectangle(Brushes.Peru, 4, 3, 3, 5);
            g.FillRectangle(Brushes.Peru, 9, 3, 3, 5);
            return bmp;
        }

        private Image CreateContainerIcon()
        {
            var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            using var grayBrush = new SolidBrush(Color.Silver);
            g.FillRectangle(grayBrush, 2, 4, 12, 9);
            g.DrawRectangle(Pens.Gray, 2, 4, 12, 9);
            return bmp;
        }

        private Image CreateScaleIcon()
        {
            var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var darkGrayPen = new Pen(Color.DimGray, 2);
            g.DrawLine(darkGrayPen, 2, 14, 14, 14);
            g.DrawLine(darkGrayPen, 8, 14, 8, 4);
            g.DrawLine(darkGrayPen, 2, 5, 14, 5);
            return bmp;
        }

        private Image CreateIconForText(string text)
        {
            var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            using var font = new Font("Segoe UI", 7, FontStyle.Bold);
            TextRenderer.DrawText(g, text, font, new Point(0, 2), Color.FromArgb(100, 100, 100));
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
                _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                _grid.RowTemplate.Height = 28;
                _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
                _grid.Columns["Id"]!.Visible = false;

                var bar = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, Padding = new Padding(8), AutoSize = true };
                bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                var lblF = new Label { Text = "Filtr:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 8, 6, 0) };
                var lblH = new Label { Text = "Handlowiec:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(12, 8, 6, 0) };
                bar.Controls.Add(lblF, 0, 0); bar.Controls.Add(_tbFilter, 1, 0); bar.Controls.Add(lblH, 2, 0); bar.Controls.Add(_cbHandlowiec, 3, 0);

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

                var ok = new Button { Text = "Wybierz", AutoSize = true, Padding = new Padding(12, 8, 12, 8), DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Anuluj", AutoSize = true, Padding = new Padding(12, 8, 12, 8), DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => { if (_grid.CurrentRow != null) SelectedId = _grid.CurrentRow.Cells["Id"].Value?.ToString(); };

                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8), AutoSize = true };
                buttons.Controls.Add(ok); buttons.Controls.Add(cancel);

                Controls.Add(_grid); Controls.Add(bar); Controls.Add(buttons);

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

