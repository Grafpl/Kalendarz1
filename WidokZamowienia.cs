// Plik: WidokZamowienia.cs
// WERSJA 13.0 - POPRAWKA ZAPISU PALET I POJEMNIKÓW
// Zmiany:
// 1. Poprawione zapisywanie dokładnych wartości palet (30 zamiast 29.70)
// 2. Poprawione zapisywanie pojemników E2
// 3. Usunięty filtr "Szukaj w ofercie"

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
        private const decimal POJEMNIKOW_NA_PALECIE_E2 = 40m;
        private const decimal KG_NA_POJEMNIKU = 15m;
        private const decimal KG_NA_PALECIE = POJEMNIKOW_NA_PALECIE * KG_NA_POJEMNIKU; // 540
        private const decimal KG_NA_PALECIE_E2 = POJEMNIKOW_NA_PALECIE_E2 * KG_NA_POJEMNIKU; // 600

        // ===== Zmienne Stanu Formularza =====
        private string? _selectedKlientId;
        private bool _blokujObslugeZmian;
        private readonly CultureInfo _pl = new("pl-PL");
        private readonly Dictionary<string, Image> _headerIcons = new();

        // ===== Kontrolki dla grida ostatnich odbiorców =====
        private DataGridView? gridOstatniOdbiorcy;
        private Panel? panelOstatniOdbiorcy;
        private Label? lblOstatniOdbiorcy;

        // ===== Dane i Cache =====
        private sealed class KontrahentInfo
        {
            public string Id { get; set; } = "";
            public string Nazwa { get; set; } = "";
            public string KodPocztowy { get; set; } = "";
            public string Miejscowosc { get; set; } = "";
            public string NIP { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public DateTime? OstatnieZamowienie { get; set; }
        }

        private readonly DataTable _dt = new();
        private DataView _view = default!;
        private readonly List<KontrahentInfo> _kontrahenci = new();
        private readonly Dictionary<string, DateTime> _ostatnieZamowienia = new();

        // ===== Panel podsumowania =====
        private Panel? panelSummary;
        private Label? lblSumaPalet;
        private Label? lblSumaPojemnikow;
        private Label? lblSumaKg;

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

        // ===== GŁÓWNA METODA ŁADUJĄCA =====
        private async void WidokZamowienia_Load(object? sender, EventArgs e)
        {
            ApplyModernUIStyles();
            CreateHeaderIcons();
            SzybkiGrid();
            WireShortcuts();
            BuildDataTableSchema();
            InitDefaults();
            CreateOstatniOdbiorcyGrid();
            CreateSummaryPanel();

            dateTimePickerSprzedaz.Format = DateTimePickerFormat.Custom;
            dateTimePickerSprzedaz.CustomFormat = "yyyy-MM-dd (dddd)";

            // Ukryj panel szukania towaru (nie jest potrzebny)
            if (panelSzukajTowaru != null)
            {
                panelSzukajTowaru.Visible = false;
                panelSzukajTowaru.Height = 0;
            }
            if (txtSzukajTowaru != null)
            {
                txtSzukajTowaru.Visible = false;
            }

            try
            {
                await LoadInitialDataInBackground();
                WireUpUIEvents();
                await LoadOstatnieZamowienia();

                if (_idZamowieniaDoEdycji.HasValue)
                {
                    await LoadZamowienieAsync(_idZamowieniaDoEdycji.Value);
                    lblTytul.Text = "Edycja zamówienia";
                    btnZapisz.Text = "Zapisz zmiany (Ctrl+S)";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
                btnZapisz.Enabled = true;
            }
        }

        #region Grid Ostatnich Odbiorców

        private void CreateOstatniOdbiorcyGrid()
        {
            int yPosition = 630;
            int xPosition = 20;
            int width = 400;
            int height = 280;

            panelOstatniOdbiorcy = new Panel
            {
                Location = new Point(xPosition, yPosition),
                Size = new Size(width, height),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Visible = true
            };

            lblOstatniOdbiorcy = new Label
            {
                Text = "Wybierz odbiorcę:",
                Location = new Point(5, 5),
                Size = new Size(width - 10, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(75, 85, 99),
                BackColor = Color.White
            };

            gridOstatniOdbiorcy = new DataGridView
            {
                Location = new Point(5, 25),
                Size = new Size(width - 10, height - 30),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                ColumnHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(229, 231, 235),
                Font = new Font("Segoe UI", 9f),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Vertical
            };

            gridOstatniOdbiorcy.DefaultCellStyle.SelectionBackColor = Color.FromArgb(59, 130, 246);
            gridOstatniOdbiorcy.DefaultCellStyle.SelectionForeColor = Color.White;
            gridOstatniOdbiorcy.DefaultCellStyle.Padding = new Padding(5, 2, 5, 2);
            gridOstatniOdbiorcy.RowTemplate.Height = 24;

            gridOstatniOdbiorcy.CellClick += (s, e) => {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    var value = gridOstatniOdbiorcy.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        SelectOdbiorcaFromCell(value);
                    }
                }
            };

            panelOstatniOdbiorcy.Controls.Add(lblOstatniOdbiorcy);
            panelOstatniOdbiorcy.Controls.Add(gridOstatniOdbiorcy);
            this.Controls.Add(panelOstatniOdbiorcy);
            panelOstatniOdbiorcy.BringToFront();
        }

        private void SelectOdbiorcaFromCell(string nazwaOdbiorcy)
        {
            var odbiorca = _kontrahenci.FirstOrDefault(k => k.Nazwa == nazwaOdbiorcy);
            if (odbiorca != null)
            {
                if (_selectedKlientId != null && _selectedKlientId != odbiorca.Id)
                {
                    _blokujObslugeZmian = true;
                    foreach (DataRow r in _dt.Rows)
                    {
                        r["E2"] = false;
                        r["Ilosc"] = 0m;
                        r["Pojemniki"] = 0m;
                        r["Palety"] = 0m;
                    }
                    _blokujObslugeZmian = false;
                    textBoxUwagi.Text = "";
                }

                UstawOdbiorce(odbiorca.Id);
                RecalcSum();
            }
        }

        private void UpdateOstatniOdbiorcyGrid(string? handlowiec)
        {
            if (gridOstatniOdbiorcy == null || panelOstatniOdbiorcy == null) return;

            panelOstatniOdbiorcy.Visible = true;

            if (string.IsNullOrEmpty(handlowiec) || handlowiec == "— Wszyscy —")
            {
                lblOstatniOdbiorcy!.Text = "Wybierz handlowca aby zobaczyć odbiorców";
                gridOstatniOdbiorcy.DataSource = null;
                return;
            }

            var odbiorcy = _kontrahenci
                .Where(k => k.Handlowiec == handlowiec)
                .OrderBy(k => k.Nazwa)
                .Select(k => k.Nazwa)
                .ToList();

            if (!odbiorcy.Any())
            {
                lblOstatniOdbiorcy!.Text = $"Brak odbiorców dla: {handlowiec}";
                gridOstatniOdbiorcy.DataSource = null;
                return;
            }

            var dt = new DataTable();
            dt.Columns.Add("Kolumna1", typeof(string));
            dt.Columns.Add("Kolumna2", typeof(string));

            for (int i = 0; i < odbiorcy.Count; i += 2)
            {
                var row = dt.NewRow();
                row["Kolumna1"] = odbiorcy[i];
                row["Kolumna2"] = (i + 1 < odbiorcy.Count) ? odbiorcy[i + 1] : "";
                dt.Rows.Add(row);
            }

            gridOstatniOdbiorcy.DataSource = dt;

            if (gridOstatniOdbiorcy.Columns.Count > 0)
            {
                gridOstatniOdbiorcy.Columns["Kolumna1"]!.Width = 240;
                gridOstatniOdbiorcy.Columns["Kolumna2"]!.Width = 240;
            }

            foreach (DataGridViewRow row in gridOstatniOdbiorcy.Rows)
            {
                for (int col = 0; col < 2; col++)
                {
                    var nazwa = row.Cells[col].Value?.ToString();
                    if (!string.IsNullOrEmpty(nazwa))
                    {
                        var kontrahent = _kontrahenci.FirstOrDefault(k => k.Nazwa == nazwa);
                        if (kontrahent?.OstatnieZamowienie != null &&
                            kontrahent.OstatnieZamowienie >= DateTime.Now.AddMonths(-1))
                        {
                            row.Cells[col].Style.Font = new Font(gridOstatniOdbiorcy.Font, FontStyle.Bold);
                            row.Cells[col].Style.ForeColor = Color.FromArgb(34, 197, 94);
                        }
                    }
                }
            }

            lblOstatniOdbiorcy!.Text = $"Odbiorcy {handlowiec} ({odbiorcy.Count}):";
        }

        private async Task LoadOstatnieZamowienia()
        {
            const string sql = @"
                SELECT KlientId, MAX(DataZamowienia) as OstatnieZamowienie
                FROM [dbo].[ZamowieniaMieso]
                WHERE DataZamowienia >= DATEADD(MONTH, -4, GETDATE())
                GROUP BY KlientId";

            _ostatnieZamowienia.Clear();

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    string klientId = rd.GetInt32(0).ToString();
                    DateTime data = rd.GetDateTime(1);
                    _ostatnieZamowienia[klientId] = data;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania ostatnich zamówień: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            foreach (var k in _kontrahenci)
            {
                if (_ostatnieZamowienia.TryGetValue(k.Id, out var data))
                {
                    k.OstatnieZamowienie = data;
                }
                else
                {
                    k.OstatnieZamowienie = null;
                }
            }
        }

        #endregion

        #region Panel Podsumowania

        private void CreateSummaryPanel()
        {
            panelSummary = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Color.FromArgb(30, 41, 59),
                Parent = dataGridViewZamowienie.Parent
            };

            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(20, 10, 20, 10)
            };

            lblSumaPalet = CreateSummaryLabel("PALETY", "0");
            lblSumaPojemnikow = CreateSummaryLabel("POJEMNIKI", "0");
            lblSumaKg = CreateSummaryLabel("KILOGRAMY", "0");

            flowPanel.Controls.Add(lblSumaPalet);
            flowPanel.Controls.Add(CreateSeparator());
            flowPanel.Controls.Add(lblSumaPojemnikow);
            flowPanel.Controls.Add(CreateSeparator());
            flowPanel.Controls.Add(lblSumaKg);

            panelSummary.Controls.Add(flowPanel);

            if (dataGridViewZamowienie != null)
            {
                dataGridViewZamowienie.Height -= 50;
            }
        }

        private Label CreateSummaryLabel(string title, string value)
        {
            var panel = new Panel
            {
                Width = 180,
                Height = 30,
                Margin = new Padding(10, 0, 10, 0)
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(0, 0),
                AutoSize = true
            };

            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(0, 12),
                AutoSize = true,
                Name = $"lbl{title}"
            };

            panel.Controls.Add(lblTitle);
            panel.Controls.Add(lblValue);

            return panel.Controls[1] as Label ?? new Label();
        }

        private Panel CreateSeparator()
        {
            return new Panel
            {
                Width = 1,
                Height = 30,
                BackColor = Color.FromArgb(51, 65, 85),
                Margin = new Padding(0, 0, 0, 0)
            };
        }

        #endregion

        #region Inicjalizacja i Ustawienia UI

        private void ApplyModernUIStyles()
        {
            this.BackColor = Color.FromArgb(245, 247, 250);

            if (lblTytul != null)
            {
                lblTytul.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
                lblTytul.ForeColor = Color.FromArgb(37, 99, 235);
            }

            StyleButton(btnZapisz, Color.FromArgb(34, 197, 94), Color.White);
            StyleButton(btnPickOdbiorca, Color.FromArgb(59, 130, 246), Color.White);

            if (panelDaneOdbiorcy != null)
            {
                panelDaneOdbiorcy.BackColor = Color.White;
                panelDaneOdbiorcy.BorderStyle = BorderStyle.None;
                panelDaneOdbiorcy.Paint += Panel_Paint;
            }

            StyleDateTimePicker(dateTimePickerSprzedaz);
            StyleDateTimePicker(dateTimePickerGodzinaPrzyjazdu);
            StyleTextBox(txtSzukajOdbiorcy);
            StyleTextBox(textBoxUwagi);

            if (summaryLabelPalety != null) summaryLabelPalety.Visible = false;
            if (summaryLabelPojemniki != null) summaryLabelPojemniki.Visible = false;
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

            btn.MouseEnter += (s, e) => btn.BackColor = ControlPaint.Dark(bgColor, 0.1f);
            btn.MouseLeave += (s, e) => btn.BackColor = bgColor;
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

            dataGridViewZamowienie.EnableHeadersVisualStyles = false;
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 41, 59);
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            dataGridViewZamowienie.ColumnHeadersHeight = 45;
            dataGridViewZamowienie.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            dataGridViewZamowienie.RowTemplate.Height = 36;
            dataGridViewZamowienie.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dataGridViewZamowienie.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 64, 175);
            dataGridViewZamowienie.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
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
            catch { }
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
            RecalcSum();
        }

        private void BuildDataTableSchema()
        {
            _dt.Columns.Add("Id", typeof(int));
            _dt.Columns.Add("Kod", typeof(string));
            _dt.Columns.Add("E2", typeof(bool));
            _dt.Columns.Add("Palety", typeof(decimal));
            _dt.Columns.Add("Pojemniki", typeof(decimal));
            _dt.Columns.Add("Ilosc", typeof(decimal));
            _dt.Columns.Add("KodTowaru", typeof(string));

            _view = new DataView(_dt);
            dataGridViewZamowienie.DataSource = _view;

            dataGridViewZamowienie.Columns["Id"]!.Visible = false;

            var cKod = dataGridViewZamowienie.Columns["Kod"]!;
            cKod.ReadOnly = true;
            cKod.FillWeight = 200;
            cKod.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            cKod.DefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            cKod.DefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59);

            var cE2 = dataGridViewZamowienie.Columns["E2"] as DataGridViewCheckBoxColumn;
            if (cE2 != null)
            {
                cE2.HeaderText = "E2";
                cE2.FillWeight = 40;
                cE2.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                cE2.ToolTipText = "40 pojemników/paletę";
            }

            var cPalety = dataGridViewZamowienie.Columns["Palety"]!;
            cPalety.FillWeight = 80;
            cPalety.DefaultCellStyle.Format = "N0";
            cPalety.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cPalety.DefaultCellStyle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            cPalety.DefaultCellStyle.ForeColor = Color.FromArgb(239, 68, 68);

            var cPojemniki = dataGridViewZamowienie.Columns["Pojemniki"]!;
            cPojemniki.FillWeight = 100;
            cPojemniki.DefaultCellStyle.Format = "N0";
            cPojemniki.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cPojemniki.DefaultCellStyle.ForeColor = Color.FromArgb(59, 130, 246);

            var cIlosc = dataGridViewZamowienie.Columns["Ilosc"]!;
            cIlosc.FillWeight = 110;
            cIlosc.DefaultCellStyle.Format = "N0";
            cIlosc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            cIlosc.HeaderText = "Ilość (kg)";
            cIlosc.DefaultCellStyle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            cIlosc.DefaultCellStyle.ForeColor = Color.FromArgb(34, 197, 94);

            var cKodTowaru = dataGridViewZamowienie.Columns["KodTowaru"]!;
            cKodTowaru.Visible = false; // Ukryta kolumna - nie potrzebujemy duplikatu kodu
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

            btnPickOdbiorca.Click += (s, e) => OpenKontrahentPicker();

            var hands = _kontrahenci.Select(k => k.Handlowiec).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();
            hands.Insert(0, "— Wszyscy —");
            cbHandlowiecFilter.Items.Clear();
            cbHandlowiecFilter.Items.AddRange(hands.ToArray());
            cbHandlowiecFilter.SelectedIndex = 0;
            cbHandlowiecFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cbHandlowiecFilter.SelectedIndexChanged += CbHandlowiecFilter_SelectedIndexChanged;
        }

        private async void CbHandlowiecFilter_SelectedIndexChanged(object? sender, EventArgs e)
        {
            string? handlowiec = cbHandlowiecFilter.SelectedItem?.ToString();
            await LoadOstatnieZamowienia();
            UpdateOstatniOdbiorcyGrid(handlowiec);
            TxtSzukajOdbiorcy_TextChanged(null, EventArgs.Empty);
        }

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
                _dt.Rows.Add(rd.GetInt32(0), kod, false, 0m, 0m, 0m, kod);
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

        private void TxtSzukajOdbiorcy_TextChanged(object? sender, EventArgs e)
        {
            var query = txtSzukajOdbiorcy.Text.Trim().ToLower();
            var handlowiec = cbHandlowiecFilter.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(query))
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
                dataGridViewZamowienie.Focus();
            }
        }

        private void DataGridViewZamowienie_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_blokujObslugeZmian || e.RowIndex < 0) return;

            var row = (dataGridViewZamowienie.Rows[e.RowIndex].DataBoundItem as DataRowView)?.Row;
            if (row == null) return;

            _blokujObslugeZmian = true;

            string changedColumnName = dataGridViewZamowienie.Columns[e.ColumnIndex].Name;

            bool useE2 = row.Field<bool>("E2");
            decimal pojemnikNaPalete = useE2 ? POJEMNIKOW_NA_PALECIE_E2 : POJEMNIKOW_NA_PALECIE;
            decimal kgNaPalete = useE2 ? KG_NA_PALECIE_E2 : KG_NA_PALECIE;

            try
            {
                switch (changedColumnName)
                {
                    case "E2":
                        // Przelicz istniejące wartości dla nowego trybu E2
                        decimal currentPalety = ParseDec(row["Palety"]);
                        if (currentPalety > 0)
                        {
                            // Zachowaj liczbę palet, przelicz pojemniki i kg
                            row["Pojemniki"] = currentPalety * pojemnikNaPalete;
                            row["Ilosc"] = currentPalety * kgNaPalete;
                        }
                        break;

                    case "Ilosc":
                        decimal ilosc = ParseDec(row["Ilosc"]);
                        row["Pojemniki"] = (ilosc > 0 && KG_NA_POJEMNIKU > 0) ? Math.Round(ilosc / KG_NA_POJEMNIKU, 0) : 0m;
                        row["Palety"] = (ilosc > 0 && kgNaPalete > 0) ? ilosc / kgNaPalete : 0m;
                        MarkInvalid(dataGridViewZamowienie.Rows[e.RowIndex].Cells["Ilosc"], ilosc < 0);
                        break;

                    case "Pojemniki":
                        decimal pojemniki = ParseDec(row["Pojemniki"]);
                        row["Ilosc"] = pojemniki * KG_NA_POJEMNIKU;
                        row["Palety"] = (pojemniki > 0 && pojemnikNaPalete > 0) ? pojemniki / pojemnikNaPalete : 0m;
                        break;

                    case "Palety":
                        decimal palety = ParseDec(row["Palety"]);
                        // Zachowujemy dokładną wartość palet wpisaną przez użytkownika
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

        private void CreateHeaderIcons()
        {
            _headerIcons["Kod"] = CreateIconForText("PROD");
            _headerIcons["Palety"] = CreatePalletIcon();
            _headerIcons["Pojemniki"] = CreateContainerIcon();
            _headerIcons["Ilosc"] = CreateScaleIcon();
            _headerIcons["KodTowaru"] = CreateIconForText("PROD");
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

        private void RecalcSum()
        {
            decimal sumaIlosc = 0m;
            decimal sumaPalety = 0m;
            decimal sumaPojemniki = 0m;

            foreach (DataRow row in _dt.Rows)
            {
                decimal ilosc = row.Field<decimal?>("Ilosc") ?? 0m;
                decimal pojemniki = row.Field<decimal?>("Pojemniki") ?? 0m;
                decimal palety = row.Field<decimal?>("Palety") ?? 0m;

                sumaIlosc += ilosc;
                sumaPojemniki += pojemniki;
                sumaPalety += palety;
            }

            // Aktualizuj panel podsumowania
            if (lblSumaPalet != null) lblSumaPalet.Text = sumaPalety.ToString("N1");
            if (lblSumaPojemnikow != null) lblSumaPojemnikow.Text = sumaPojemniki.ToString("N0");
            if (lblSumaKg != null) lblSumaKg.Text = sumaIlosc.ToString("N0");
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

            TextRenderer.DrawText(g, e.Value?.ToString(), e.CellStyle.Font, textBounds,
                e.CellStyle.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

            e.Handled = true;
        }

        private void ClearFormForNewOrder()
        {
            var selectedHandlowiec = cbHandlowiecFilter.SelectedItem;

            _idZamowieniaDoEdycji = null;
            _selectedKlientId = null;
            txtSzukajOdbiorcy.Text = "";
            panelDaneOdbiorcy.Visible = false;
            listaWynikowOdbiorcy.Visible = false;
            _view.RowFilter = string.Empty;

            _blokujObslugeZmian = true;
            foreach (DataRow r in _dt.Rows)
            {
                r["E2"] = false;
                r["Ilosc"] = 0m;
                r["Pojemniki"] = 0m;
                r["Palety"] = 0m;
            }
            _blokujObslugeZmian = false;

            textBoxUwagi.Text = "";

            var dzis = DateTime.Now.Date;
            dateTimePickerSprzedaz.Value = (dzis.DayOfWeek == DayOfWeek.Friday) ? dzis.AddDays(3) : dzis.AddDays(1);
            dateTimePickerGodzinaPrzyjazdu.Value = DateTime.Today.AddHours(8);

            lblTytul.Text = "Nowe zamówienie mięsa";
            btnZapisz.Text = "Zapisz (Ctrl+S)";

            cbHandlowiecFilter.SelectedItem = selectedHandlowiec;

            RecalcSum();
            txtSzukajOdbiorcy.Focus();
        }

        #endregion

        #region Zapis i Odczyt Zamówienia

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

            // Wczytaj dane z nowymi kolumnami
            await using (var cmdT = new SqlCommand("SELECT KodTowaru, Ilosc, ISNULL(Pojemniki, 0) as Pojemniki, ISNULL(Palety, 0) as Palety, ISNULL(E2, 0) as E2 FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId=@Id", cn))
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
                        int pojemniki = rd.GetInt32(2);
                        decimal palety = rd.GetDecimal(3);
                        bool e2 = rd.GetBoolean(4);

                        rows[0]["Ilosc"] = ilosc;
                        rows[0]["Pojemniki"] = pojemniki;
                        rows[0]["Palety"] = palety;
                        rows[0]["E2"] = e2;
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
                message = "Wpisz ilość dla przynajmniej jednego towaru.";
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
                string title = _idZamowieniaDoEdycji.HasValue ? "Zamówienie zaktualizowane" : "Zamówienie zapisane";

                MessageBox.Show(summary, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadOstatnieZamowienia();
                UpdateOstatniOdbiorcyGrid(cbHandlowiecFilter.SelectedItem?.ToString());
                ClearFormForNewOrder();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            sb.AppendLine($"Odbiorca: {lblWybranyOdbiorca.Text}");
            sb.AppendLine($"Data sprzedaży: {dateTimePickerSprzedaz.Value:yyyy-MM-dd}");

            var e2Items = orderedItems.Where(r => r.Field<bool>("E2")).ToList();
            if (e2Items.Any())
            {
                sb.AppendLine($"Towary E2 (40 poj./pal.): {e2Items.Count}");
            }

            sb.AppendLine("\nZamówione towary:");

            decimal totalPojemniki = 0;
            decimal totalPalety = 0;

            foreach (var item in orderedItems)
            {
                string e2Marker = item.Field<bool>("E2") ? " [E2]" : "";
                decimal pojemniki = item.Field<decimal>("Pojemniki");
                decimal palety = item.Field<decimal>("Palety");

                totalPojemniki += pojemniki;
                totalPalety += palety;

                sb.AppendLine($"  {item.Field<string>("Kod")}{e2Marker}: {item.Field<decimal>("Ilosc"):N0} kg " +
                            $"({pojemniki:N0} poj., {palety:N1} pal.)");
            }

            decimal totalKg = orderedItems.Sum(r => r.Field<decimal>("Ilosc"));
            sb.AppendLine($"\nPodsumowanie:");
            sb.AppendLine($"  Łącznie: {totalKg:N0} kg");
            sb.AppendLine($"  Pojemników: {totalPojemniki:N0}");
            sb.AppendLine($"  Palet: {totalPalety:N1}");

            return sb.ToString();
        }

        private sealed class KontrahentPicker : Form
        {
            private readonly DataView _view;
            private readonly TextBox _tbFilter = new() { Dock = DockStyle.Fill, PlaceholderText = "Szukaj: nazwa / NIP / miejscowość..." };
            private readonly ComboBox _cbHandlowiec = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            private readonly DataGridView _grid = new()
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
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
                foreach (var k in src)
                    table.Rows.Add(k.Id, k.Nazwa, k.NIP, k.KodPocztowy, k.Miejscowosc, k.Handlowiec);

                _view = new DataView(table);
                _grid.DataSource = _view;
                _grid.Font = new Font("Segoe UI", 10f);
                _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                _grid.RowTemplate.Height = 28;
                _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
                _grid.Columns["Id"]!.Visible = false;

                var bar = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, Padding = new Padding(8), AutoSize = true };
                bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

                var lblF = new Label { Text = "Filtr:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 8, 6, 0) };
                var lblH = new Label { Text = "Handlowiec:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(12, 8, 6, 0) };
                bar.Controls.Add(lblF, 0, 0);
                bar.Controls.Add(_tbFilter, 1, 0);
                bar.Controls.Add(lblH, 2, 0);
                bar.Controls.Add(_cbHandlowiec, 3, 0);

                var hands = src.Select(k => k.Handlowiec).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();
                hands.Insert(0, "— Wszyscy —");
                _cbHandlowiec.Items.AddRange(hands.ToArray());

                if (!string.IsNullOrWhiteSpace(handlowiecPreselected) && _cbHandlowiec.Items.IndexOf(handlowiecPreselected) is var idx && idx >= 0)
                    _cbHandlowiec.SelectedIndex = idx;
                else
                    _cbHandlowiec.SelectedIndex = 0;

                var ok = new Button { Text = "Wybierz", AutoSize = true, Padding = new Padding(12, 8, 12, 8), DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Anuluj", AutoSize = true, Padding = new Padding(12, 8, 12, 8), DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => { if (_grid.CurrentRow != null) SelectedId = _grid.CurrentRow.Cells["Id"].Value?.ToString(); };

                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8), AutoSize = true };
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
                if (!string.IsNullOrEmpty(txt))
                    parts.Add($"(Nazwa LIKE '%{txt}%' OR NIP LIKE '%{txt}%' OR Miejscowosc LIKE '%{txt}%')");
                if (!string.IsNullOrWhiteSpace(hand) && hand != "— Wszyscy —")
                    parts.Add($"Handlowiec = '{hand.Replace("'", "''")}'");
                _view.RowFilter = string.Join(" AND ", parts);
            }
        }

        private async Task SaveOrderAsync()
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var tr = (SqlTransaction)await cn.BeginTransactionAsync();

            int orderId;

            // Oblicz sumaryczne wartości
            decimal sumaPojemnikow = 0;
            decimal sumaPalet = 0;
            bool czyJakikolwiekE2 = false;

            foreach (DataRow r in _dt.Rows)
            {
                if (r.Field<decimal>("Ilosc") > 0m)
                {
                    sumaPojemnikow += r.Field<decimal>("Pojemniki");
                    sumaPalet += r.Field<decimal>("Palety");
                    if (r.Field<bool>("E2")) czyJakikolwiekE2 = true;
                }
            }

            if (_idZamowieniaDoEdycji.HasValue)
            {
                orderId = _idZamowieniaDoEdycji.Value;
                var cmdUpdate = new SqlCommand(@"UPDATE [dbo].[ZamowieniaMieso] SET 
                    DataZamowienia = @dz, DataPrzyjazdu = @dp, KlientId = @kid, Uwagi = @uw, 
                    KtoMod = @km, KiedyMod = SYSDATETIME(),
                    LiczbaPojemnikow = @poj, LiczbaPalet = @pal, TrybE2 = @e2
                    WHERE Id=@id", cn, tr);
                cmdUpdate.Parameters.AddWithValue("@dz", dateTimePickerSprzedaz.Value.Date);
                cmdUpdate.Parameters.AddWithValue("@dp", dateTimePickerGodzinaPrzyjazdu.Value);
                cmdUpdate.Parameters.AddWithValue("@kid", _selectedKlientId!);
                cmdUpdate.Parameters.AddWithValue("@uw", string.IsNullOrWhiteSpace(textBoxUwagi.Text) ? DBNull.Value : textBoxUwagi.Text);
                cmdUpdate.Parameters.AddWithValue("@km", UserID);
                cmdUpdate.Parameters.AddWithValue("@id", orderId);
                cmdUpdate.Parameters.AddWithValue("@poj", (int)Math.Round(sumaPojemnikow));
                cmdUpdate.Parameters.AddWithValue("@pal", sumaPalet);
                cmdUpdate.Parameters.AddWithValue("@e2", czyJakikolwiekE2);
                await cmdUpdate.ExecuteNonQueryAsync();

                var cmdDelete = new SqlCommand(@"DELETE FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId=@id", cn, tr);
                cmdDelete.Parameters.AddWithValue("@id", orderId);
                await cmdDelete.ExecuteNonQueryAsync();
            }
            else
            {
                var cmdGetId = new SqlCommand(@"SELECT ISNULL(MAX(Id),0)+1 FROM [dbo].[ZamowieniaMieso]", cn, tr);
                orderId = Convert.ToInt32(await cmdGetId.ExecuteScalarAsync());

                var cmdInsert = new SqlCommand(@"INSERT INTO [dbo].[ZamowieniaMieso] 
                    (Id, DataZamowienia, DataPrzyjazdu, KlientId, Uwagi, IdUser, DataUtworzenia, 
                     LiczbaPojemnikow, LiczbaPalet, TrybE2, TransportStatus) 
                    VALUES (@id, @dz, @dp, @kid, @uw, @u, GETDATE(), @poj, @pal, @e2, 'Oczekuje')", cn, tr);
                cmdInsert.Parameters.AddWithValue("@id", orderId);
                cmdInsert.Parameters.AddWithValue("@dz", dateTimePickerSprzedaz.Value.Date);
                cmdInsert.Parameters.AddWithValue("@dp", dateTimePickerGodzinaPrzyjazdu.Value);
                cmdInsert.Parameters.AddWithValue("@kid", _selectedKlientId!);
                cmdInsert.Parameters.AddWithValue("@uw", string.IsNullOrWhiteSpace(textBoxUwagi.Text) ? DBNull.Value : textBoxUwagi.Text);
                cmdInsert.Parameters.AddWithValue("@u", UserID);
                cmdInsert.Parameters.AddWithValue("@poj", (int)Math.Round(sumaPojemnikow));
                cmdInsert.Parameters.AddWithValue("@pal", sumaPalet);
                cmdInsert.Parameters.AddWithValue("@e2", czyJakikolwiekE2);
                await cmdInsert.ExecuteNonQueryAsync();
            }

            // Zapisz towary z dokładnymi wartościami palet/pojemników
            var cmdInsertItem = new SqlCommand(@"INSERT INTO [dbo].[ZamowieniaMiesoTowar] 
                (ZamowienieId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2) 
                VALUES (@zid, @kt, @il, @ce, @poj, @pal, @e2)", cn, tr);
            cmdInsertItem.Parameters.Add("@zid", SqlDbType.Int);
            cmdInsertItem.Parameters.Add("@kt", SqlDbType.Int);
            cmdInsertItem.Parameters.Add("@il", SqlDbType.Decimal);
            cmdInsertItem.Parameters.Add("@ce", SqlDbType.Decimal);
            cmdInsertItem.Parameters.Add("@poj", SqlDbType.Int);
            cmdInsertItem.Parameters.Add("@pal", SqlDbType.Decimal);
            cmdInsertItem.Parameters.Add("@e2", SqlDbType.Bit);

            foreach (DataRow r in _dt.Rows)
            {
                if (r.Field<decimal>("Ilosc") <= 0m) continue;

                // Zapisuj dokładne wartości użytkownika
                decimal palety = r.Field<decimal>("Palety");
                decimal pojemniki = r.Field<decimal>("Pojemniki");
                bool e2 = r.Field<bool>("E2");

                cmdInsertItem.Parameters["@zid"].Value = orderId;
                cmdInsertItem.Parameters["@kt"].Value = r.Field<int>("Id");
                cmdInsertItem.Parameters["@il"].Value = r.Field<decimal>("Ilosc");
                cmdInsertItem.Parameters["@ce"].Value = 0m;
                cmdInsertItem.Parameters["@poj"].Value = (int)Math.Round(pojemniki);
                cmdInsertItem.Parameters["@pal"].Value = palety; // Dokładna wartość palet
                cmdInsertItem.Parameters["@e2"].Value = e2;
                await cmdInsertItem.ExecuteNonQueryAsync();
            }

            await tr.CommitAsync();
        }
    }
}
#endregion