using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class SprawdzalkaUmow : Form
    {
        // === REPOZYTORIUM (zamiast hardcoded SQL) ===
        private readonly HarmonogramDostawRepository _repo = new HarmonogramDostawRepository();

        // Cache avatarów (id+name → Image)
        private readonly Dictionary<string, Image> _avatarCache = new Dictionary<string, Image>();

        // Cache nazw operatorów (id → name) - do uzupełniania KtoUtw/KtoWysl/KtoOtrzym
        private readonly Dictionary<int, string> _operatorNameCache = new Dictionary<int, string>();

        // === FILTRY ===
        // Pokaż archiwalne (>6 mies)
        private bool _pokazArchiwalne = false;
        private const int DOMYSLNY_ZAKRES_MIESIECY = 6;
        private CheckBox _chkPokazArchiwalne;
        private Label _lblStats;

        // Quick-filter chip (jeden naraz, null = brak)
        private QuickFilter _aktywnyChip = QuickFilter.Brak;
        private readonly Dictionary<QuickFilter, Button> _chipy = new Dictionary<QuickFilter, Button>();

        // === DANE ===
        // Oryginalna tabela z SQL (bez wierszy-nagłówków). Filtrowanie rebuilduje display tabelę.
        private DataTable _originalRows;

        // === DEBOUNCE TXT SEARCH (#3) ===
        private readonly Timer _searchDebounce = new Timer { Interval = 250 };

        // === CONTEXT MENU dla audit log (#23) ===
        private ContextMenuStrip _rowContextMenu;

        // Stała kolumna `_isHeader` (bool) dodawana do tabel - eliminuje boxing w hot loops (#4).
        private const string COL_IS_HEADER = "_isHeader";

        public string UserID { get; set; } = "";

        public SprawdzalkaUmow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            ApplyCustomStyles();

            dgvContracts.CurrentCellDirtyStateChanged += DataGridViewKalendarz_CurrentCellDirtyStateChanged;
            dgvContracts.CellContentClick += DataGridViewKalendarz_CellContentClick;
            dgvContracts.CellDoubleClick += DataGridViewKalendarz_CellDoubleClick; // #12
            dgvContracts.CellMouseDown += DataGridViewKalendarz_CellMouseDown;     // #23 prawy klik
            chkShowOnlyIncomplete.CheckedChanged += (s, e) => ApplyCombinedFilter();

            // #3 - debounce search
            _searchDebounce.Tick += (s, e) =>
            {
                _searchDebounce.Stop();
                ApplyCombinedFilter();
            };

            // #23 - context menu
            BudujContextMenu();

            LoadDataGridKalendarz();
        }

        private void ApplyCustomStyles()
        {
            ConfigureDataGridView(dgvContracts);
            btnAddContract.MouseEnter += (s, e) => btnAddContract.BackColor = SprawdzalkaUmowStyles.PrimaryHover;
            btnAddContract.MouseLeave += (s, e) => btnAddContract.BackColor = SprawdzalkaUmowStyles.Primary;
            dgvContracts.CellPainting += DgvContracts_CellPainting;
            dgvContracts.RowTemplate.Height = SprawdzalkaUmowStyles.DataRowHeight;

            ReorganizujPanelGorny();
        }

        private void ReorganizujPanelGorny()
        {
            var panel = chkShowOnlyIncomplete.Parent as Panel;
            if (panel == null) return;

            // Kompaktowy panel: 76px (było 130) - 2 zwarte rzędy
            panel.Height = 76;
            panel.BackColor = Color.FromArgb(250, 251, 253);  // bardzo jasny tint

            // ─── ROW 1 (y=8, h=32): Add button + Search + Stats ───
            btnAddContract.Location = new Point(12, 8);
            btnAddContract.Size = new Size(130, 32);
            btnAddContract.Text = "📑  Dodaj umowę";
            btnAddContract.TextAlign = ContentAlignment.MiddleCenter;
            btnAddContract.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAddContract.FlatAppearance.BorderSize = 0;

            // Search bez osobnego labela - użyj placeholder
            lblSearch.Visible = false;
            txtSearch.Location = new Point(150, 8);
            txtSearch.Size = new Size(260, 32);
            txtSearch.Font = new Font("Segoe UI", 9.5F);
            try { txtSearch.PlaceholderText = "🔍  Szukaj po dostawcy lub osobie..."; } catch { }
            txtSearch.BorderStyle = BorderStyle.FixedSingle;

            // Debounced TextChanged
            txtSearch.TextChanged -= textBoxSearch_TextChanged;
            txtSearch.TextChanged += (s, e) =>
            {
                _searchDebounce.Stop();
                _searchDebounce.Start();
            };

            // STATS - prawy róg górnego rzędu, mniejszy font
            _lblStats = new Label
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AutoSize = false,
                Size = new Size(560, 32),
                Location = new Point(panel.Width - 572, 8),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = SprawdzalkaUmowStyles.TextMuted,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "Ładowanie..."
            };
            panel.Controls.Add(_lblStats);

            // ─── ROW 2 (y=44, h=26): Chips + Checkboxes ───
            BudujChipy(panel);

            chkShowOnlyIncomplete.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chkShowOnlyIncomplete.Location = new Point(panel.Width - 380, 47);
            chkShowOnlyIncomplete.Text = "Tylko niekompletne";
            chkShowOnlyIncomplete.Font = new Font("Segoe UI", 8.5F);
            chkShowOnlyIncomplete.AutoSize = true;

            _chkPokazArchiwalne = new CheckBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point, 238),
                ForeColor = SprawdzalkaUmowStyles.TextDark,
                Location = new Point(panel.Width - 220, 47),
                Text = $"Archiwalne (>{DOMYSLNY_ZAKRES_MIESIECY} mies.)",
                Checked = false,
                UseVisualStyleBackColor = true,
                TabIndex = 156
            };
            _chkPokazArchiwalne.CheckedChanged += (s, e) =>
            {
                _pokazArchiwalne = _chkPokazArchiwalne.Checked;
                LoadDataGridKalendarz();
            };
            panel.Controls.Add(_chkPokazArchiwalne);

            // Subtelne wykończenie - akcentowa linia po lewej (4px primary green)
            // + cienki separator pomiędzy rzędami + dolna ramka
            panel.Paint += (s, e) =>
            {
                // Lewy pasek akcentowy
                using (var brush = new SolidBrush(SprawdzalkaUmowStyles.Primary))
                    e.Graphics.FillRectangle(brush, 0, 0, 4, panel.Height);
                // Subtelny separator pomiędzy rzędami
                using (var pen = new Pen(Color.FromArgb(232, 234, 237), 1))
                    e.Graphics.DrawLine(pen, 12, 41, panel.Width - 12, 41);
                // Dolna ramka (cień 1px)
                e.Graphics.DrawLine(SprawdzalkaUmowStyles.BorderPen, 0, panel.Height - 1, panel.Width, panel.Height - 1);
            };
        }

        // Quick-filter chips - kompaktowe (88x26 było 130x30)
        private void BudujChipy(Panel panel)
        {
            int x = 12;
            int y = 44;
            (QuickFilter id, string label)[] defs =
            {
                (QuickFilter.Dzis, "📅 Dziś"),
                (QuickFilter.Jutro, "➡ Jutro"),
                (QuickFilter.TenTydzien, "📆 Tydzień"),
                (QuickFilter.Spoznione, "⚠ Spóźnione"),
                (QuickFilter.TylkoMoje, "👤 Moje"),
            };

            foreach (var (id, label) in defs)
            {
                var btn = new Button
                {
                    Text = label,
                    Location = new Point(x, y),
                    Size = new Size(88, 26),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    BackColor = SprawdzalkaUmowStyles.ChipInactiveBg,
                    ForeColor = SprawdzalkaUmowStyles.ChipInactiveFg,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Tag = id
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 224, 228);
                btn.Click += Chip_Click;
                _chipy[id] = btn;
                panel.Controls.Add(btn);
                x += 92;  // 88 + 4 spacing
            }
        }

        private void Chip_Click(object sender, EventArgs e)
        {
            var btn = (Button)sender;
            var clicked = (QuickFilter)btn.Tag;

            // Toggle: jeśli już aktywny → wyłącz, inaczej zmień
            _aktywnyChip = (_aktywnyChip == clicked) ? QuickFilter.Brak : clicked;
            OdswiezChipy();
            ApplyCombinedFilter();
        }

        private void OdswiezChipy()
        {
            foreach (var kvp in _chipy)
            {
                bool aktywny = kvp.Key == _aktywnyChip;
                kvp.Value.BackColor = aktywny ? SprawdzalkaUmowStyles.ChipActiveBg : SprawdzalkaUmowStyles.ChipInactiveBg;
                kvp.Value.ForeColor = aktywny ? SprawdzalkaUmowStyles.ChipActiveFg : SprawdzalkaUmowStyles.ChipInactiveFg;
            }
        }

        // #23 - context menu: Pokaż historię zmian / Skopiuj LP
        private void BudujContextMenu()
        {
            _rowContextMenu = new ContextMenuStrip();
            var miHistoria = new ToolStripMenuItem("📜  Pokaż historię zmian");
            miHistoria.Click += (s, e) => PokazHistorieAktualnegoWiersza();
            var miKopiuj = new ToolStripMenuItem("📋  Skopiuj LP");
            miKopiuj.Click += (s, e) =>
            {
                if (dgvContracts.CurrentRow == null) return;
                var v = dgvContracts.CurrentRow.Cells["ID"]?.Value;
                if (v != null && v != DBNull.Value)
                    Clipboard.SetText(v.ToString());
            };
            _rowContextMenu.Items.Add(miHistoria);
            _rowContextMenu.Items.Add(miKopiuj);
        }

        private void PokazHistorieAktualnegoWiersza()
        {
            if (dgvContracts.CurrentRow == null) return;
            var row = dgvContracts.CurrentRow;
            if (IsHeaderRow(row)) return;

            int lp = Convert.ToInt32(row.Cells["ID"].Value);
            string dostawca = row.Cells["Dostawca"]?.Value?.ToString() ?? "?";
            var history = _repo.GetAuditHistory(lp);
            using var dlg = new AuditHistoryDialog(lp, dostawca, history);
            dlg.ShowDialog(this);
        }

        // Aktualizuj statystyki - liczone z ORYGINALNEJ tabeli (bez wpływu filtra)
        private void OdswiezStatystyki()
        {
            if (_lblStats == null || _originalRows == null) return;

            int total = 0, kompletne = 0, doUtworzenia = 0, doWyslania = 0, doOdebrania = 0, posrednicy = 0;

            foreach (DataRow r in _originalRows.Rows)
            {
                total++;
                bool u = r["Utworzone"] != DBNull.Value && Convert.ToBoolean(r["Utworzone"]);
                bool w = r["Wysłane"] != DBNull.Value && Convert.ToBoolean(r["Wysłane"]);
                bool o = r["Otrzymane"] != DBNull.Value && Convert.ToBoolean(r["Otrzymane"]);
                bool p = r["Posrednik"] != DBNull.Value && Convert.ToBoolean(r["Posrednik"]);

                if (p) { posrednicy++; kompletne++; continue; }
                if (u && w && o) { kompletne++; continue; }
                if (!u) doUtworzenia++;
                else if (!w) doWyslania++;
                else if (!o) doOdebrania++;
            }

            _lblStats.Text =
                $"📋  {total}  ·  " +
                $"✅ {kompletne}  ·  " +
                $"📝 {doUtworzenia} utw  ·  " +
                $"📤 {doWyslania} wys  ·  " +
                $"📥 {doOdebrania} odb  ·  " +
                $"🤝 {posrednicy} pośr";
        }

        private void ConfigureDataGridView(DataGridView dgv)
        {
            // #6 DoubleBuffered (eliminuje flicker przy scrollu) - wymaga reflection bo property protected.
            typeof(DataGridView)
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(dgv, true);

            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgv.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dgv.EditMode = DataGridViewEditMode.EditProgrammatically;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = SprawdzalkaUmowStyles.RowAlternate;
            dgv.DefaultCellStyle.SelectionBackColor = SprawdzalkaUmowStyles.PrimaryHover;
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.RowPrePaint += Dgv_RowPrePaint;

            dgv.AllowUserToResizeColumns = false;
            dgv.AllowUserToResizeRows = false;
            dgv.AllowUserToOrderColumns = false;
            dgv.RowHeadersVisible = false;

            dgv.RowPostPaint += Dgv_RowPostPaint;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        }

        // Rysuje wiersz-nagłówek (data + dzień tygodnia) jako baner.
        // Dodatkowo: dla wszystkich wierszy z dzisiejszą datą (header + dane) → czerwone obramowanie.
        private void Dgv_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv == null || e.RowIndex < 0) return;
            if (!dgv.Columns.Contains("DataOdbioru")) return;

            var row = dgv.Rows[e.RowIndex];
            var dateVal = row.Cells["DataOdbioru"].Value;
            if (dateVal == null || dateVal == DBNull.Value) return;

            DateTime data = Convert.ToDateTime(dateVal);
            bool isToday = data.Date == DateTime.Today;

            // ===== HEADER ROW (banner z datą) =====
            if (IsHeaderRow(row))
            {
                string dzienTygodnia = data.ToString("dddd", SprawdzalkaUmowStyles.PlCulture).ToUpper();
                string dataPelna = data.ToString("d MMMM yyyy", SprawdzalkaUmowStyles.PlCulture);
                int diff = (data.Date - DateTime.Today).Days;
                string wzgledna = diff switch
                {
                    0 => "  •  DZIŚ",
                    1 => "  •  JUTRO",
                    -1 => "  •  WCZORAJ",
                    _ when diff > 0 && diff <= 7 => $"  •  za {diff} dni",
                    _ when diff < 0 && diff >= -7 => $"  •  {-diff} dni temu",
                    _ => ""
                };

                // Tło: żółty gradient dla DZIŚ, slate dla pozostałych
                if (isToday)
                {
                    using var brush = SprawdzalkaUmowStyles.CreateTodayHeaderBgBrush(e.RowBounds);
                    e.Graphics.FillRectangle(brush, e.RowBounds);
                }
                else
                {
                    using var brush = SprawdzalkaUmowStyles.CreateHeaderBgBrush(e.RowBounds);
                    e.Graphics.FillRectangle(brush, e.RowBounds);
                    e.Graphics.FillRectangle(SprawdzalkaUmowStyles.HeaderAccentBrush, e.RowBounds.Left, e.RowBounds.Top, 4, e.RowBounds.Height);
                }

                string label = $"📅  {dzienTygodnia}  •  {dataPelna}{wzgledna}";
                var textRect = new Rectangle(
                    e.RowBounds.Left + 16, e.RowBounds.Top,
                    e.RowBounds.Width - 32, e.RowBounds.Height);
                var textBrush = isToday ? SprawdzalkaUmowStyles.TodayHeaderTextBrush : SprawdzalkaUmowStyles.HeaderTextBrush;
                e.Graphics.DrawString(label, SprawdzalkaUmowStyles.HeaderBannerFont,
                    textBrush, textRect, SprawdzalkaUmowStyles.HeaderBannerFormat);
            }

            // ===== CZERWONE OBRAMOWANIE dla wszystkich wierszy z dzisiejszą datą =====
            // (header + wszystkie dane dostaw na dzisiaj)
            if (isToday)
            {
                var rect = new Rectangle(
                    e.RowBounds.Left, e.RowBounds.Top,
                    e.RowBounds.Width - 1, e.RowBounds.Height - 1);
                e.Graphics.DrawRectangle(SprawdzalkaUmowStyles.TodayBorderPen, rect);
            }
        }

        private void LoadDataGridKalendarz(bool preserveState = true)
        {
            (int firstRow, int? currentId, string? currentColName) state = default;
            if (preserveState)
                state = CaptureGridState();

            var table = _repo.LoadDostawy(_pokazArchiwalne, DOMYSLNY_ZAKRES_MIESIECY);

            // #4 - dodaj kolumnę _isHeader (bool, wszystkie dane = false)
            if (!table.Columns.Contains(COL_IS_HEADER))
                table.Columns.Add(COL_IS_HEADER, typeof(bool));
            foreach (DataRow r in table.Rows)
                r[COL_IS_HEADER] = false;

            dgvContracts.SuspendLayout();
            dgvContracts.AutoGenerateColumns = false;

            if (dgvContracts.Columns.Count == 0)
            {
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ID", Name = "ID", Visible = false, ReadOnly = true });
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DataOdbioru", Name = "DataOdbioru", HeaderText = "Data", DefaultCellStyle = { Format = "yyyy-MM-dd" }, Width = 100, ReadOnly = true });
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Dostawca", Name = "Dostawca", HeaderText = "Dostawca", Width = 200, ReadOnly = true });
                dgvContracts.Columns.Add(MakeCheckColumn("Utworzone"));
                dgvContracts.Columns.Add(MakeCheckColumn("Wysłane"));
                dgvContracts.Columns.Add(MakeCheckColumn("Otrzymane"));
                dgvContracts.Columns.Add(MakeCheckColumn("Posrednik", "Pośrednik"));
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Auta", Name = "Auta", HeaderText = "Aut", Width = 50, ReadOnly = true });
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SztukiDek", Name = "SztukiDek", HeaderText = "Sztuki", Width = 75, ReadOnly = true });
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "WagaDek", Name = "WagaDek", HeaderText = "Waga", Width = 75, ReadOnly = true });
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SztSzuflada", Name = "SztSzuflada", HeaderText = "sztPoj", Width = 60, ReadOnly = true });
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KtoUtw", Name = "KtoUtw", HeaderText = "Kto utworzył", Width = 180, ReadOnly = true });
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KtoUtwID", Name = "KtoUtwID", Visible = false, ReadOnly = true });
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KiedyUtw", Name = "KiedyUtw", HeaderText = "Kiedy utworzył", Width = 130, DefaultCellStyle = { Format = "yyyy-MM-dd HH:mm" }, ReadOnly = true });
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KtoWysl", Name = "KtoWysl", HeaderText = "Kto wysłał", Width = 180, ReadOnly = true });
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KtoWyslID", Name = "KtoWyslID", Visible = false, ReadOnly = true });
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KiedyWysl", Name = "KiedyWysl", HeaderText = "Kiedy wysłał", Width = 130, DefaultCellStyle = { Format = "yyyy-MM-dd HH:mm" }, ReadOnly = true });
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KtoOtrzym", Name = "KtoOtrzym", HeaderText = "Kto otrzymał", Width = 180, ReadOnly = true });
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KtoOtrzymID", Name = "KtoOtrzymID", Visible = false, ReadOnly = true });
                dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KiedyOtrzm", Name = "KiedyOtrzm", HeaderText = "Kiedy otrzymał", Width = 130, DefaultCellStyle = { Format = "yyyy-MM-dd HH:mm" }, ReadOnly = true });
            }

            _originalRows = table;
            RebuildDisplayTable();
            dgvContracts.ResumeLayout();

            if (preserveState)
                RestoreGridState(state);

            OdswiezStatystyki();
        }

        // Buduje tabelę wyświetlaną w gridzie: filtr + wstawienie wierszy-nagłówków per data.
        private void RebuildDisplayTable()
        {
            if (_originalRows == null) return;

            // 1. Zastosuj filtr (txtSearch + chkShowOnlyIncomplete + quick-chip)
            string filter = BuildRowFilter();
            DataRow[] visibleRows = string.IsNullOrEmpty(filter)
                ? _originalRows.Select("", "DataOdbioru DESC")
                : _originalRows.Select(filter, "DataOdbioru DESC");

            // 2. Zbuduj nową tabelę z separatorami dat
            var display = _originalRows.Clone();
            object prevDate = null;
            foreach (var r in visibleRows)
            {
                var currDate = r["DataOdbioru"];
                if (!Equals(currDate, prevDate))
                {
                    var sep = display.NewRow();
                    sep["ID"] = -1;
                    sep[COL_IS_HEADER] = true;        // #4 - flag bool zamiast porównań ID
                    sep["DataOdbioru"] = currDate;
                    sep["Dostawca"] = DBNull.Value;
                    sep["Utworzone"] = false;
                    sep["Wysłane"] = false;
                    sep["Otrzymane"] = false;
                    sep["Posrednik"] = false;
                    display.Rows.Add(sep);
                    prevDate = currDate;
                }
                display.ImportRow(r);
            }

            dgvContracts.DataSource = display.DefaultView;

            // Wysokości wierszy: nagłówki niższe, dane standardowe
            for (int i = 0; i < dgvContracts.Rows.Count; i++)
            {
                bool isHeader = IsHeaderRow(dgvContracts.Rows[i]);
                dgvContracts.Rows[i].Height = isHeader ? SprawdzalkaUmowStyles.HeaderRowHeight : SprawdzalkaUmowStyles.DataRowHeight;
                if (isHeader)
                {
                    dgvContracts.Rows[i].DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 230, 215);
                    dgvContracts.Rows[i].DefaultCellStyle.SelectionForeColor = SprawdzalkaUmowStyles.TextDark;
                }
            }
        }

        private static DataGridViewCheckBoxColumn MakeCheckColumn(string dataProperty, string header = null) => new DataGridViewCheckBoxColumn
        {
            DataPropertyName = dataProperty,
            Name = dataProperty,
            HeaderText = header ?? dataProperty,
            Width = 80,
            ReadOnly = false,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        // #4 - flag-based check zamiast Convert.ToInt32(ID) == -1
        private static bool IsHeaderRow(DataGridViewRow row)
        {
            if (row == null) return false;
            var dgv = row.DataGridView;
            if (dgv == null) return false;
            if (dgv.Columns.Contains(COL_IS_HEADER))
            {
                var v = row.Cells[COL_IS_HEADER].Value;
                return v is bool b && b;
            }
            // Fallback (gdyby kolumna jeszcze nie istniała)
            if (!dgv.Columns.Contains("ID")) return false;
            var idVal = row.Cells["ID"].Value;
            return idVal != null && idVal != DBNull.Value && Convert.ToInt32(idVal) == -1;
        }

        private void Dgv_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv == null || e.RowIndex < 0) return;

            var row = dgv.Rows[e.RowIndex];

            if (IsHeaderRow(row))
            {
                row.DefaultCellStyle.BackColor = SprawdzalkaUmowStyles.HeaderBgStart;
                row.DefaultCellStyle.ForeColor = Color.White;
                row.DefaultCellStyle.SelectionBackColor = SprawdzalkaUmowStyles.HeaderBgStart;
                row.DefaultCellStyle.SelectionForeColor = Color.White;
                return;
            }

            bool okU = GetBool(dgv, e.RowIndex, "Utworzone");
            bool okW = GetBool(dgv, e.RowIndex, "Wysłane");
            bool okO = GetBool(dgv, e.RowIndex, "Otrzymane");
            bool isPosrednik = GetBool(dgv, e.RowIndex, "Posrednik");

            // Sprawdź czy to dzisiejsza data (żółte tło - tylko dla niezakończonych)
            bool isToday = false;
            if (dgv.Columns.Contains("DataOdbioru"))
            {
                var dv = row.Cells["DataOdbioru"].Value;
                if (dv is DateTime dt) isToday = dt.Date == DateTime.Today;
            }

            if (isPosrednik || (okU && okW && okO))
            {
                row.DefaultCellStyle.BackColor = SprawdzalkaUmowStyles.RowComplete;
                row.DefaultCellStyle.ForeColor = Color.White;
            }
            else if (isToday)
            {
                // Dziś + niekompletne → żółte tło (alarmuje że trzeba domknąć dzisiaj)
                row.DefaultCellStyle.BackColor = SprawdzalkaUmowStyles.TodayRowBg;
                row.DefaultCellStyle.ForeColor = SprawdzalkaUmowStyles.TextDark;
            }
            else
            {
                row.DefaultCellStyle.ForeColor = SprawdzalkaUmowStyles.TextDark;
                row.DefaultCellStyle.BackColor = (e.RowIndex % 2 == 0) ? Color.White : dgv.AlternatingRowsDefaultCellStyle.BackColor;
            }

            // Zamroź pozostałe 3 checkboxy gdy Pośrednik=true
            string[] checkCols = { "Utworzone", "Wysłane", "Otrzymane" };
            foreach (var col in checkCols)
            {
                if (!dgv.Columns.Contains(col)) continue;
                var cell = row.Cells[col];
                cell.ReadOnly = isPosrednik;
                if (isPosrednik)
                {
                    cell.Style.BackColor = SprawdzalkaUmowStyles.FrozenCheckBg;
                    cell.Style.ForeColor = SprawdzalkaUmowStyles.FrozenCheckFg;
                }
            }

            // #2 - cached fonty zamiast new Font() per repaint
            if (dgv.Columns.Contains("DataOdbioru"))
            {
                bool isFirstOfDate = e.RowIndex == 0;
                if (e.RowIndex > 0)
                {
                    var curr = row.Cells["DataOdbioru"].Value;
                    var prev = dgv.Rows[e.RowIndex - 1].Cells["DataOdbioru"].Value;
                    isFirstOfDate = !Equals(curr, prev);
                }
                row.Cells["DataOdbioru"].Style.Font = isFirstOfDate
                    ? SprawdzalkaUmowStyles.CellBoldFont
                    : SprawdzalkaUmowStyles.CellRegularFont;
                if (isFirstOfDate && !isPosrednik && !(okU && okW && okO))
                {
                    row.Cells["DataOdbioru"].Style.ForeColor = SprawdzalkaUmowStyles.Primary;
                }
            }
        }

        private static bool GetBool(DataGridView dgv, int rowIndex, string colName)
        {
            if (rowIndex < 0 || !dgv.Columns.Contains(colName)) return false;
            var val = dgv.Rows[rowIndex].Cells[colName]?.Value;
            if (val == null || val == DBNull.Value) return false;
            if (val is bool b) return b;
            return bool.TryParse(val.ToString(), out var parsed) && parsed;
        }

        private void DataGridViewKalendarz_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvContracts.IsCurrentCellDirty && dgvContracts.CurrentCell is DataGridViewCheckBoxCell)
            {
                dgvContracts.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void DataGridViewKalendarz_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var grid = dgvContracts;
            var col = grid.Columns[e.ColumnIndex];

            if (col.Name != "Utworzone" && col.Name != "Wysłane" && col.Name != "Otrzymane" && col.Name != "Posrednik") return;

            var row = grid.Rows[e.RowIndex];
            if (IsHeaderRow(row)) return;
            if (row.Cells[col.Name].ReadOnly) return;
            if (!grid.Columns.Contains("ID")) return;
            int id = Convert.ToInt32(row.Cells["ID"].Value);

            bool currentValue = false;
            var rawVal = row.Cells[col.Name].Value;
            if (rawVal != null && rawVal != DBNull.Value)
                currentValue = Convert.ToBoolean(rawVal);
            bool newValue = !currentValue;

            row.Cells[col.Name].Value = newValue;

            string dostawca = row.Cells["Dostawca"]?.Value?.ToString() ?? "?";
            string dataStr = row.Cells["DataOdbioru"]?.Value is DateTime dt ? dt.ToString("yyyy-MM-dd") : "?";
            string msg = newValue
                ? string.Format("Ustawić '{0}' = TAK dla {1} ({2})?", col.HeaderText, dostawca, dataStr)
                : string.Format("Cofnąć '{0}' dla {1} ({2})?", col.HeaderText, dostawca, dataStr);
            if (MessageBox.Show(msg, "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                row.Cells[col.Name].Value = currentValue;
                return;
            }

            try
            {
                int? userIdInt = null;
                if (int.TryParse(UserID, out int parsedUid)) userIdInt = parsedUid;

                _repo.UpdateFlag(id, col.Name, newValue, userIdInt, out bool? oldValue);
                _repo.InsertAuditLog(id, col.Name, oldValue, newValue, userIdInt); // #23

                UpdateRowLocally(id, col.Name, newValue);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd aktualizacji: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                row.Cells[col.Name].Value = !newValue;
            }
        }

        // #12 - dwuklik wiersza otwiera UmowyForm dla danego LP
        private void DataGridViewKalendarz_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvContracts.Rows[e.RowIndex];
            if (IsHeaderRow(row)) return;

            // Ignoruj dwuklik na kolumnach checkboxów (toggle handler się tym zajmuje)
            string colName = e.ColumnIndex >= 0 ? dgvContracts.Columns[e.ColumnIndex].Name : "";
            if (colName == "Utworzone" || colName == "Wysłane" || colName == "Otrzymane" || colName == "Posrednik") return;

            var cellVal = row.Cells["ID"]?.Value;
            if (cellVal == null || cellVal == DBNull.Value) return;
            string lp = cellVal.ToString();

            var form = new UmowyForm(initialLp: lp, initialIdLibra: null) { UserID = App.UserID };
            form.FormClosed += (s, args) => LoadDataGridKalendarz();
            form.Show(this);
        }

        // #23 - prawy klik = pokaż context menu (Pokaż historię / Skopiuj LP)
        private void DataGridViewKalendarz_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            if (e.RowIndex < 0) return;

            var row = dgvContracts.Rows[e.RowIndex];
            if (IsHeaderRow(row)) return;

            // Zaznacz wiersz pod kursorem
            dgvContracts.CurrentCell = row.Cells[Math.Max(e.ColumnIndex, 0)];
            _rowContextMenu.Show(Cursor.Position);
        }

        private string BuildRowFilter()
        {
            var parts = new List<string>();

            // 1. Search
            string filterText = txtSearch.Text.Trim().Replace("'", "''");
            if (!string.IsNullOrEmpty(filterText))
            {
                parts.Add($"(Dostawca LIKE '%{filterText}%' OR CONVERT(DataOdbioru, 'System.String') LIKE '%{filterText}%' OR KtoUtw LIKE '%{filterText}%' OR KtoWysl LIKE '%{filterText}%' OR KtoOtrzym LIKE '%{filterText}%')");
            }

            // 2. Tylko nieuzupełnione
            if (chkShowOnlyIncomplete.Checked)
            {
                parts.Add("([Utworzone] = false OR [Wysłane] = false OR [Otrzymane] = false)");
            }

            // 3. Quick-filter chip (#9)
            string chipFilter = BuildChipFilter();
            if (!string.IsNullOrEmpty(chipFilter))
            {
                parts.Add(chipFilter);
            }

            return string.Join(" AND ", parts);
        }

        private string BuildChipFilter()
        {
            DateTime today = DateTime.Today;
            switch (_aktywnyChip)
            {
                case QuickFilter.Dzis:
                    return $"DataOdbioru = #{today:yyyy-MM-dd}#";

                case QuickFilter.Jutro:
                    return $"DataOdbioru = #{today.AddDays(1):yyyy-MM-dd}#";

                case QuickFilter.TenTydzien:
                    // Pon-Niedz danego tygodnia
                    int daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
                    DateTime mon = today.AddDays(-daysFromMonday);
                    DateTime sun = mon.AddDays(6);
                    return $"DataOdbioru >= #{mon:yyyy-MM-dd}# AND DataOdbioru <= #{sun:yyyy-MM-dd}#";

                case QuickFilter.Spoznione:
                    // data <= dziś AND nie kompletne (brak min. 1 z 3) AND nie pośrednik
                    return $"DataOdbioru <= #{today:yyyy-MM-dd}# AND ([Utworzone] = false OR [Wysłane] = false OR [Otrzymane] = false) AND [Posrednik] = false";

                case QuickFilter.TylkoMoje:
                    if (string.IsNullOrEmpty(UserID)) return "";
                    return $"(KtoUtwID = '{UserID}' OR KtoWyslID = '{UserID}' OR KtoOtrzymID = '{UserID}')";

                default:
                    return "";
            }
        }

        private void ApplyCombinedFilter()
        {
            RebuildDisplayTable();
        }

        // Aktualizuj wiersz w lokalnym DataTable bez pełnego reload SQL.
        private void UpdateRowLocally(int id, string columnName, bool value)
        {
            if (_originalRows == null) return;
            DataRow targetRow = null;
            foreach (DataRow r in _originalRows.Rows)
            {
                if (r["ID"] != DBNull.Value && Convert.ToInt32(r["ID"]) == id)
                {
                    targetRow = r;
                    break;
                }
            }
            if (targetRow == null) return;

            string ktoCol = null, kiedyCol = null;
            switch (columnName)
            {
                case "Utworzone": ktoCol = "KtoUtw"; kiedyCol = "KiedyUtw"; break;
                case "Wysłane": ktoCol = "KtoWysl"; kiedyCol = "KiedyWysl"; break;
                case "Otrzymane": ktoCol = "KtoOtrzym"; kiedyCol = "KiedyOtrzm"; break;
            }

            targetRow.BeginEdit();
            targetRow[columnName] = value;

            if (ktoCol != null)
            {
                if (value && int.TryParse(UserID, out int uid))
                {
                    targetRow[ktoCol] = GetOperatorNameCached(uid);
                    targetRow[ktoCol + "ID"] = uid.ToString();
                    targetRow[kiedyCol] = DateTime.Now;
                }
                else
                {
                    targetRow[ktoCol] = DBNull.Value;
                    targetRow[ktoCol + "ID"] = DBNull.Value;
                    targetRow[kiedyCol] = DBNull.Value;
                }
            }
            targetRow.EndEdit();

            // Zaktualizuj też wiersz w display table (jeśli widoczny)
            if (dgvContracts.DataSource is DataView dv)
            {
                foreach (DataRow displayRow in dv.Table.Rows)
                {
                    if (displayRow["ID"] != DBNull.Value && Convert.ToInt32(displayRow["ID"]) == id)
                    {
                        displayRow.BeginEdit();
                        displayRow[columnName] = targetRow[columnName];
                        if (ktoCol != null)
                        {
                            displayRow[ktoCol] = targetRow[ktoCol];
                            displayRow[ktoCol + "ID"] = targetRow[ktoCol + "ID"];
                            displayRow[kiedyCol] = targetRow[kiedyCol];
                        }
                        displayRow.EndEdit();
                        break;
                    }
                }
            }

            dgvContracts.Invalidate();
            OdswiezStatystyki();
        }

        // Cache wrapper dla _repo.GetOperatorName
        private string GetOperatorNameCached(int userId)
        {
            if (_operatorNameCache.TryGetValue(userId, out var cached)) return cached;
            try
            {
                var name = _repo.GetOperatorName(userId);
                _operatorNameCache[userId] = name;
                return name;
            }
            catch
            {
                return userId.ToString();
            }
        }

        private void CommandButton_Insert_Click(object sender, EventArgs e)
        {
            if (dgvContracts.CurrentRow == null)
            {
                MessageBox.Show("Zaznacz pozycję w kalendarzu.");
                return;
            }
            var cellVal = dgvContracts.CurrentRow.Cells["ID"]?.Value;
            if (cellVal == null || cellVal == DBNull.Value)
            {
                MessageBox.Show("Brak wartości LP (ID) w zaznaczonym wierszu.");
                return;
            }
            string lp = cellVal.ToString()!;
            var form = new UmowyForm(initialLp: lp, initialIdLibra: null) { UserID = App.UserID };
            form.FormClosed += (s, args) => LoadDataGridKalendarz();
            form.Show(this);
        }

        private void nieUzupelnione_CheckedChanged(object sender, EventArgs e)
        {
            ApplyCombinedFilter();
        }

        private (int firstRow, int? currentId, string? currentColName) CaptureGridState()
        {
            if (dgvContracts.RowCount == 0) return (-1, null, null);
            int first = dgvContracts.FirstDisplayedScrollingRowIndex;
            int? id = dgvContracts.CurrentRow?.Cells["ID"].Value as int?;
            string? colName = dgvContracts.CurrentCell != null ? dgvContracts.Columns[dgvContracts.CurrentCell.ColumnIndex].Name : null;
            return (first, id, colName);
        }

        private void RestoreGridState((int firstRow, int? currentId, string? currentColName) state)
        {
            if (state.firstRow >= 0 && state.firstRow < dgvContracts.RowCount)
            {
                try { dgvContracts.FirstDisplayedScrollingRowIndex = state.firstRow; } catch { /* ignore */ }
            }
            if (state.currentId.HasValue)
            {
                var row = dgvContracts.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Cells["ID"].Value is int val && val == state.currentId.Value);
                if (row != null)
                {
                    int colIndex = !string.IsNullOrEmpty(state.currentColName) && dgvContracts.Columns.Contains(state.currentColName) ? dgvContracts.Columns[state.currentColName].Index : 0;
                    try { dgvContracts.CurrentCell = row.Cells[colIndex]; } catch { /* ignore */ }
                }
            }
        }

        private void textBoxSearch_TextChanged(object sender, EventArgs e)
        {
            // Kept for Designer compatibility - rzeczywisty handler jest podpięty w ReorganizujPanelGorny (debounced)
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }

        private void DgvContracts_CellPainting(object sender, System.Windows.Forms.DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            // #6 - skip cells poza viewport (dla wierszy nagłówka tylko)
            if (IsHeaderRow(dgvContracts.Rows[e.RowIndex]))
            {
                e.Handled = true;
                return;
            }

            string colName = dgvContracts.Columns[e.ColumnIndex].Name;
            if (colName != "KtoUtw" && colName != "KtoWysl" && colName != "KtoOtrzym") return;

            string idColName = colName switch
            {
                "KtoUtw" => "KtoUtwID",
                "KtoWysl" => "KtoWyslID",
                "KtoOtrzym" => "KtoOtrzymID",
                _ => null
            };

            var row = dgvContracts.Rows[e.RowIndex];
            string name = row.Cells[colName]?.Value?.ToString();
            string odbiorcaId = idColName != null ? row.Cells[idColName]?.Value?.ToString() : null;

            if (string.IsNullOrWhiteSpace(name)) return;

            e.PaintBackground(e.ClipBounds, true);

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Image avatar = GetOrCreateAvatar(odbiorcaId, name);
            if (avatar != null)
            {
                int avatarY = e.CellBounds.Y + (e.CellBounds.Height - SprawdzalkaUmowStyles.AvatarSize) / 2;
                int avatarX = e.CellBounds.X + 6;
                g.DrawImage(avatar, avatarX, avatarY, SprawdzalkaUmowStyles.AvatarSize, SprawdzalkaUmowStyles.AvatarSize);

                var textBounds = new Rectangle(
                    avatarX + SprawdzalkaUmowStyles.AvatarSize + 6,
                    e.CellBounds.Y,
                    e.CellBounds.Width - SprawdzalkaUmowStyles.AvatarSize - 18,
                    e.CellBounds.Height);

                bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
                Color textColor = isSelected ? Color.White : SprawdzalkaUmowStyles.TextDark;

                TextRenderer.DrawText(g, name, e.CellStyle.Font, textBounds, textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            e.Handled = true;
        }

        private Image GetOrCreateAvatar(string odbiorcaId, string name)
        {
            string cacheKey = odbiorcaId ?? name ?? "unknown";
            if (_avatarCache.TryGetValue(cacheKey, out Image cachedAvatar))
                return cachedAvatar;

            Image avatar = null;
            if (!string.IsNullOrWhiteSpace(odbiorcaId))
                avatar = UserAvatarManager.GetAvatarRounded(odbiorcaId, SprawdzalkaUmowStyles.AvatarSize);

            if (avatar == null && !string.IsNullOrWhiteSpace(name))
                avatar = UserAvatarManager.GenerateDefaultAvatar(name, cacheKey, SprawdzalkaUmowStyles.AvatarSize);

            if (avatar != null)
                _avatarCache[cacheKey] = avatar;

            return avatar;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _searchDebounce?.Stop();
            _searchDebounce?.Dispose();
            _rowContextMenu?.Dispose();

            foreach (var avatar in _avatarCache.Values)
                avatar?.Dispose();
            _avatarCache.Clear();

            base.OnFormClosed(e);
        }

        // === ENUM dla quick-filter chipów (#9) ===
        private enum QuickFilter
        {
            Brak,
            Dzis,
            Jutro,
            TenTydzien,
            Spoznione,
            TylkoMoje
        }
    }
}
