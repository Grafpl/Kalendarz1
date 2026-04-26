using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class SprawdzalkaUmow : Form
    {
        private readonly string connectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Kolory do stylizacji UI
        private readonly Color _primaryColor = Color.FromArgb(92, 138, 58);
        private readonly Color _hoverColor = Color.FromArgb(75, 115, 47);

        // Cache avatarów
        private Dictionary<string, Image> _avatarCache = new Dictionary<string, Image>();

        // Rozmiar avatara
        private const int AVATAR_SIZE = 24;

        // Cache nazw operatorów (id → name) - używany do uzupełniania KtoUtw/KtoWysl/KtoOtrzym
        // przy lokalnym update wiersza, żeby uniknąć pełnego SQL reload.
        private Dictionary<int, string> _operatorNameCache = new Dictionary<int, string>();

        // Filtr archiwalnych: false = ostatnie 6 miesięcy (znacznie szybciej), true = wszystko od 2021
        private bool _pokazArchiwalne = false;
        private const int DOMYSLNY_ZAKRES_MIESIECY = 6;
        private CheckBox _chkPokazArchiwalne;
        private Label _lblStats;

        // Oryginalna tabela z SQL (bez wierszy-nagłówków). Filtrowanie rebuilduje display tabelę.
        private DataTable _originalRows;
        private static readonly System.Globalization.CultureInfo _plCulture =
            new System.Globalization.CultureInfo("pl-PL");
        private const int HEADER_ROW_HEIGHT = 32;
        private const int DATA_ROW_HEIGHT = 36;

        public string UserID { get; set; } = "";

        public SprawdzalkaUmow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            ApplyCustomStyles();

            dgvContracts.CurrentCellDirtyStateChanged += DataGridViewKalendarz_CurrentCellDirtyStateChanged;
            dgvContracts.CellContentClick += DataGridViewKalendarz_CellContentClick;
            chkShowOnlyIncomplete.CheckedChanged += (s, e) => ApplyCombinedFilter();

            LoadDataGridKalendarz();
        }

        private void ApplyCustomStyles()
        {
            ConfigureDataGridView(dgvContracts);
            btnAddContract.MouseEnter += (s, e) => btnAddContract.BackColor = _hoverColor;
            btnAddContract.MouseLeave += (s, e) => btnAddContract.BackColor = _primaryColor;
            dgvContracts.CellPainting += DgvContracts_CellPainting;
            dgvContracts.RowTemplate.Height = 36; // Zwiększ wysokość wierszy dla avatarów

            // Reorganizacja kontrolek nad tabelą (programatically, żeby nie nadpisywać Designer-a)
            ReorganizujPanelGorny();
        }

        private void ReorganizujPanelGorny()
        {
            var panel = chkShowOnlyIncomplete.Parent as Panel;
            if (panel == null) return;

            // Powiększ panel żeby zmieściły się statystyki + lepszy układ
            panel.Height = 90;

            // 1. PRZYCISK "Dodaj umowę" - lewy róg, większy z ikoną
            btnAddContract.Location = new Point(20, 18);
            btnAddContract.Size = new Size(170, 55);
            btnAddContract.Text = "📑  Dodaj umowę";
            btnAddContract.TextAlign = ContentAlignment.MiddleCenter;
            btnAddContract.Font = new Font("Segoe UI", 11F, FontStyle.Bold);

            // 2. SZUKAJ - środek, większy
            lblSearch.Location = new Point(220, 18);
            lblSearch.Text = "🔍  Szukaj po dostawcy lub osobie";
            lblSearch.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblSearch.ForeColor = Color.FromArgb(80, 80, 80);
            lblSearch.AutoSize = true;
            txtSearch.Location = new Point(220, 40);
            txtSearch.Size = new Size(280, 30);
            txtSearch.Font = new Font("Segoe UI", 11F, FontStyle.Regular);

            // 3. CHECKBOXY filtrów - prawo
            chkShowOnlyIncomplete.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chkShowOnlyIncomplete.Location = new Point(panel.Width - 220, 20);
            chkShowOnlyIncomplete.Text = "☐ Tylko nieuzupełnione";
            chkShowOnlyIncomplete.Font = new Font("Segoe UI", 9.75F);

            _chkPokazArchiwalne = new CheckBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AutoSize = true,
                Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 238),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location = new Point(panel.Width - 220, 50),
                Text = $"📦 Pokaż archiwalne (>{DOMYSLNY_ZAKRES_MIESIECY} mies.)",
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

            // 4. STATYSTYKI - środek-prawo (między szukaj a checkbox-ami)
            _lblStats = new Label
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AutoSize = false,
                Size = new Size(450, 60),
                Location = new Point(panel.Width - 700, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(80, 80, 80),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Ładowanie..."
            };
            panel.Controls.Add(_lblStats);

            // Subtelna dolna linia oddzielająca panel od tabeli
            panel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(220, 224, 228), 1))
                {
                    e.Graphics.DrawLine(pen, 0, panel.Height - 1, panel.Width, panel.Height - 1);
                }
            };
        }

        // Aktualizuj statystyki - liczone z ORYGINALNEJ tabeli (bez headerów + bez wpływu filtra)
        private void OdswiezStatystyki()
        {
            if (_lblStats == null || _originalRows == null) return;

            int total = 0, kompletne = 0, doUtworzenia = 0, doWyslania = 0, doOdebrania = 0, posrednicy = 0;

            foreach (DataRow r in _originalRows.Rows)
            {
                if (r["ID"] != DBNull.Value && Convert.ToInt32(r["ID"]) == -1) continue; // header
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
                $"📋  {total} dostaw     " +
                $"✅ {kompletne} kompletne     " +
                $"📝 {doUtworzenia} do utworzenia     " +
                $"📤 {doWyslania} do wysłania     " +
                $"📥 {doOdebrania} do odebrania     " +
                $"🤝 {posrednicy} pośrednicy";
        }

        private void ConfigureDataGridView(DataGridView dgv)
        {
            // DisplayedCells - mierzy tylko widoczne wiersze (znacznie szybsze niż AllCells przy 500+ wierszach)
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgv.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            // Edycja tylko programatyczna (klikamy checkboxy w handlerze) - nie pokazuje editing controls
            dgv.EditMode = DataGridViewEditMode.EditProgrammatically;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 249);
            dgv.DefaultCellStyle.SelectionBackColor = _hoverColor;
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.RowPrePaint += Dgv_RowPrePaint;

            // Blokada zmiany szerokości kolumn i wysokości wierszy
            dgv.AllowUserToResizeColumns = false;
            dgv.AllowUserToResizeRows = false;
            dgv.AllowUserToOrderColumns = false;
            dgv.RowHeadersVisible = false;          // brak lewej kolumny ze strzałką (oszczędza miejsce)

            // Grupowanie wizualne po dacie - rysuj grubą górną linię gdy data się zmienia
            dgv.RowPostPaint += Dgv_RowPostPaint;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        }

        // Rysuje wiersz-nagłówek (data + dzień tygodnia) jako baner across całego wiersza.
        // Dla zwykłych wierszy nic nie robi.
        private void Dgv_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv == null || e.RowIndex < 0) return;

            var row = dgv.Rows[e.RowIndex];
            if (!IsHeaderRow(row)) return;
            if (!dgv.Columns.Contains("DataOdbioru")) return;

            var dateVal = row.Cells["DataOdbioru"].Value;
            if (dateVal == null || dateVal == DBNull.Value) return;

            DateTime data = Convert.ToDateTime(dateVal);
            string dzienTygodnia = data.ToString("dddd", _plCulture).ToUpper();
            string dataPelna = data.ToString("d MMMM yyyy", _plCulture);
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

            // Tło banera - gradient grafitowy (slate)
            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                e.RowBounds, Color.FromArgb(55, 71, 79), Color.FromArgb(84, 110, 122),
                System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(brush, e.RowBounds);
            }

            // Lewa pionowa kreska akcentowa - bursztynowa (kontrast do grafitowego tła)
            using (var accent = new SolidBrush(Color.FromArgb(255, 152, 0)))
            {
                e.Graphics.FillRectangle(accent, e.RowBounds.Left, e.RowBounds.Top, 4, e.RowBounds.Height);
            }

            // Tekst: "📅 WTOREK • 28 KWIETNIA 2026 • DZIŚ"
            string label = $"📅  {dzienTygodnia}  •  {dataPelna}{wzgledna}";
            using (var font = new Font("Segoe UI", 11.5F, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.White))
            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                var textRect = new Rectangle(
                    e.RowBounds.Left + 16, e.RowBounds.Top,
                    e.RowBounds.Width - 32, e.RowBounds.Height);
                e.Graphics.DrawString(label, font, textBrush, textRect, sf);
            }
        }

        private void LoadDataGridKalendarz(bool preserveState = true)
        {
            (int firstRow, int? currentId, string? currentColName) state = default;
            if (preserveState)
                state = CaptureGridState();

            // Zakres dat: domyślnie ostatnie 6 mies. (znacznie szybciej niż od 2021).
            // Użytkownik może wymusić archiwalne checkbox-em "Pokaż archiwalne".
            string dolnaGranica = _pokazArchiwalne
                ? "'2021-01-01'"
                : "DATEADD(MONTH, -" + DOMYSLNY_ZAKRES_MIESIECY + ", GETDATE())";

            string query = $@"
                SELECT
                    h.[LP] AS ID, h.[DataOdbioru], h.[Dostawca],
                    CAST(ISNULL(h.[Utworzone],0) AS bit) AS Utworzone,
                    CAST(ISNULL(h.[Wysłane],0) AS bit) AS Wysłane,
                    CAST(ISNULL(h.[Otrzymane],0) AS bit) AS Otrzymane,
                    CAST(ISNULL(h.[Posrednik],0) AS bit) AS Posrednik,
                    h.[Auta], h.[SztukiDek], h.[WagaDek], h.[SztSzuflada],
                    ISNULL(u1.Name, h.KtoUtw) AS KtoUtw, h.[KiedyUtw],
                    CAST(h.KtoUtw AS VARCHAR(50)) AS KtoUtwID,
                    ISNULL(u2.Name, h.KtoWysl) AS KtoWysl, h.[KiedyWysl],
                    CAST(h.KtoWysl AS VARCHAR(50)) AS KtoWyslID,
                    ISNULL(u3.Name, h.KtoOtrzym) AS KtoOtrzym, h.[KiedyOtrzm],
                    CAST(h.KtoOtrzym AS VARCHAR(50)) AS KtoOtrzymID
                FROM [LibraNet].[dbo].[HarmonogramDostaw] h
                LEFT JOIN [LibraNet].[dbo].[operators] u1 ON TRY_CAST(h.KtoUtw AS INT) = u1.ID
                LEFT JOIN [LibraNet].[dbo].[operators] u2 ON TRY_CAST(h.KtoWysl AS INT) = u2.ID
                LEFT JOIN [LibraNet].[dbo].[operators] u3 ON TRY_CAST(h.KtoOtrzym AS INT) = u3.ID
                WHERE h.Bufor = 'Potwierdzony'
                  AND h.DataOdbioru BETWEEN {dolnaGranica} AND DATEADD(DAY, 2, GETDATE())
                ORDER BY h.DataOdbioru DESC;";

            using (var connection = new SqlConnection(connectionString))
            using (var adapter = new SqlDataAdapter(query, connection))
            {
                var table = new DataTable();
                adapter.Fill(table);

                dgvContracts.SuspendLayout();
                dgvContracts.AutoGenerateColumns = false;

                if (dgvContracts.Columns.Count == 0)
                {
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ID", Name = "ID", Visible = false, ReadOnly = true });
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DataOdbioru", Name = "DataOdbioru", HeaderText = "Data", DefaultCellStyle = { Format = "yyyy-MM-dd" }, Width = 100, ReadOnly = true });
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Dostawca", Name = "Dostawca", HeaderText = "Dostawca", Width = 200, ReadOnly = true });
                    // 4 checkboxy - jedyne edytowalne kolumny
                    dgvContracts.Columns.Add(MakeCheckColumn("Utworzone"));
                    dgvContracts.Columns.Add(MakeCheckColumn("Wysłane"));
                    dgvContracts.Columns.Add(MakeCheckColumn("Otrzymane"));
                    dgvContracts.Columns.Add(MakeCheckColumn("Posrednik", "Pośrednik"));
                    // Od Aut do końca - read-only, ustawione szerokości (bez AutoSize)
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
            }

            if (preserveState)
                RestoreGridState(state);

            OdswiezStatystyki();
        }

        // Buduje tabelę wyświetlaną w gridzie: filtr + wstawienie wierszy-nagłówków per data.
        // Wywoływane po Load i po każdej zmianie filtra/checkboxa.
        private void RebuildDisplayTable()
        {
            if (_originalRows == null) return;

            // 1. Zastosuj filtr na oryginalnych wierszach
            string filter = BuildRowFilter();
            DataRow[] visibleRows;
            if (string.IsNullOrEmpty(filter))
                visibleRows = _originalRows.Select("", "DataOdbioru DESC");
            else
                visibleRows = _originalRows.Select(filter, "DataOdbioru DESC");

            // 2. Zbuduj nową tabelę z separatorami dat
            var display = _originalRows.Clone();
            object prevDate = null;
            foreach (var r in visibleRows)
            {
                var currDate = r["DataOdbioru"];
                if (!Equals(currDate, prevDate))
                {
                    var sep = display.NewRow();
                    sep["ID"] = -1;                     // marker wiersza-nagłówka
                    sep["DataOdbioru"] = currDate;
                    sep["Dostawca"] = DBNull.Value;
                    // DataGridViewCheckBoxColumn wymaga bool (nie akceptuje DBNull) - inaczej FormatException
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

            // Wysokości wierszy: nagłówki niższe (32px), dane standardowe (36px)
            for (int i = 0; i < dgvContracts.Rows.Count; i++)
            {
                var idVal = dgvContracts.Rows[i].Cells["ID"].Value;
                bool isHeader = idVal != null && idVal != DBNull.Value && Convert.ToInt32(idVal) == -1;
                dgvContracts.Rows[i].Height = isHeader ? HEADER_ROW_HEIGHT : DATA_ROW_HEIGHT;
                if (isHeader)
                {
                    dgvContracts.Rows[i].DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 230, 215);
                    dgvContracts.Rows[i].DefaultCellStyle.SelectionForeColor = Color.FromArgb(44, 62, 80);
                }
            }
        }

        private static DataGridViewCheckBoxColumn MakeCheckColumn(string dataProperty, string header = null) => new DataGridViewCheckBoxColumn
        {
            DataPropertyName = dataProperty,
            Name = dataProperty,
            HeaderText = header ?? dataProperty,
            Width = 80,
            ReadOnly = false,                       // jedyne edytowalne
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        // Czy wiersz to "nagłówek daty" (separator) - sprawdzane po ID = -1
        private static bool IsHeaderRow(DataGridViewRow row)
        {
            if (row == null || !row.DataGridView.Columns.Contains("ID")) return false;
            var v = row.Cells["ID"].Value;
            return v != null && v != DBNull.Value && Convert.ToInt32(v) == -1;
        }

        private void Dgv_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv == null || e.RowIndex < 0) return;

            var row = dgv.Rows[e.RowIndex];

            // Wiersz-nagłówek daty - rysujemy baner i nie aplikujemy normalnego stylowania
            if (IsHeaderRow(row))
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(55, 71, 79);    // grafitowy slate
                row.DefaultCellStyle.ForeColor = Color.White;
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(55, 71, 79);
                row.DefaultCellStyle.SelectionForeColor = Color.White;
                return;
            }

            bool okU = GetBool(dgv, e.RowIndex, "Utworzone");
            bool okW = GetBool(dgv, e.RowIndex, "Wysłane");
            bool okO = GetBool(dgv, e.RowIndex, "Otrzymane");
            bool isPosrednik = GetBool(dgv, e.RowIndex, "Posrednik");

            // Pośrednik = od razu zielone (nie wymaga wysłania/otrzymania) + zamroź pozostałe checkboxy
            if (isPosrednik || (okU && okW && okO))
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(46, 204, 113); // zielony
                row.DefaultCellStyle.ForeColor = Color.White;
            }
            else
            {
                row.DefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
                row.DefaultCellStyle.BackColor = (e.RowIndex % 2 == 0) ? Color.White : dgv.AlternatingRowsDefaultCellStyle.BackColor;
            }

            // Zamroź pozostałe 3 checkboxy gdy Pośrednik=true
            // (Pośrednik wyklucza Wysłane/Otrzymane bo dostawca jest przez pośrednika)
            string[] checkCols = { "Utworzone", "Wysłane", "Otrzymane" };
            foreach (var col in checkCols)
            {
                if (!dgv.Columns.Contains(col)) continue;
                var cell = row.Cells[col];
                cell.ReadOnly = isPosrednik;
                if (isPosrednik)
                {
                    cell.Style.BackColor = Color.FromArgb(200, 230, 201);
                    cell.Style.ForeColor = Color.FromArgb(189, 195, 199);
                }
            }

            // Pogrubiona data tylko dla pierwszego wiersza danej daty (uzupełnia separator z RowPostPaint)
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
                    ? new Font(dgv.Font, FontStyle.Bold)
                    : new Font(dgv.Font, FontStyle.Regular);
                if (isFirstOfDate && !isPosrednik && !(okU && okW && okO))
                {
                    row.Cells["DataOdbioru"].Style.ForeColor = Color.FromArgb(92, 138, 58);
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

            // Wiersz-nagłówek daty - ignoruj klik
            if (IsHeaderRow(row)) return;

            // Jeśli klikany checkbox jest ReadOnly (np. zamrożone bo Pośrednik=true) - ignoruj
            if (row.Cells[col.Name].ReadOnly) return;
            if (!grid.Columns.Contains("ID")) return;
            int id = Convert.ToInt32(row.Cells["ID"].Value);

            // Explicit toggle - z EditMode=EditProgrammatically klik nie zmienia wartości checkboxa,
            // EditedFormattedValue zwraca stary stan. Czytamy aktualną wartość i odwracamy.
            bool currentValue = false;
            var rawVal = row.Cells[col.Name].Value;
            if (rawVal != null && rawVal != DBNull.Value)
                currentValue = Convert.ToBoolean(rawVal);
            bool newValue = !currentValue;

            // NATYCHMIASTOWY visual update - checkbox zmienia stan zanim pojawi się MessageBox
            row.Cells[col.Name].Value = newValue;

            string dostawca = row.Cells["Dostawca"]?.Value?.ToString() ?? "?";
            string dataStr = row.Cells["DataOdbioru"]?.Value is DateTime dt ? dt.ToString("yyyy-MM-dd") : "?";
            string msg = newValue
                ? string.Format("Ustawić '{0}' = TAK dla {1} ({2})?", col.HeaderText, dostawca, dataStr)
                : string.Format("Cofnąć '{0}' dla {1} ({2})?", col.HeaderText, dostawca, dataStr);
            if (MessageBox.Show(msg, "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                // User anulował - cofnij wizualną zmianę
                row.Cells[col.Name].Value = currentValue;
                return;
            }

            string ktoCol = null, kiedyCol = null;
            switch (col.Name)
            {
                case "Utworzone": ktoCol = "KtoUtw"; kiedyCol = "KiedyUtw"; break;
                case "Wysłane": ktoCol = "KtoWysl"; kiedyCol = "KiedyWysl"; break;
                case "Otrzymane": ktoCol = "KtoOtrzym"; kiedyCol = "KiedyOtrzm"; break;
            }

            try
            {
                UpdateKalendarzFlag_NoReload(id, col.Name, newValue);
                // Lokalna aktualizacja wiersza - bez pełnego SQL reload (oszczędza 500-1500ms)
                UpdateRowLocally(id, col.Name, newValue);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd aktualizacji: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                row.Cells[col.Name].Value = !newValue;
            }
        }

        private string BuildRowFilter()
        {
            string filterText = txtSearch.Text.Trim().Replace("'", "''");
            string textFilter = string.IsNullOrEmpty(filterText) ? string.Empty : $"Dostawca LIKE '%{filterText}%' OR CONVERT(DataOdbioru, 'System.String') LIKE '%{filterText}%' OR KtoUtw LIKE '%{filterText}%' OR KtoWysl LIKE '%{filterText}%' OR KtoOtrzym LIKE '%{filterText}%'";
            string incompleteFilter = "[Utworzone] = false OR [Wysłane] = false OR [Otrzymane] = false";

            if (chkShowOnlyIncomplete.Checked)
            {
                return string.IsNullOrEmpty(textFilter) ? incompleteFilter : $"({textFilter}) AND ({incompleteFilter})";
            }
            return textFilter;
        }

        private void ApplyCombinedFilter()
        {
            // Zamiast filtra na DataView - rebuilduj display tabelę żeby separator-wiersze daty
            // były spójne z aktualnym widokiem (bez orphan headerów).
            RebuildDisplayTable();
        }

        // Pobiera nazwę operatora z cache (lub z bazy, jeśli pierwszy raz)
        private string GetOperatorName(int userId)
        {
            if (_operatorNameCache.TryGetValue(userId, out var cached)) return cached;
            try
            {
                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand("SELECT TOP 1 Name FROM dbo.operators WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    conn.Open();
                    var v = cmd.ExecuteScalar();
                    string name = v == null || v == DBNull.Value ? userId.ToString() : v.ToString();
                    _operatorNameCache[userId] = name;
                    return name;
                }
            }
            catch
            {
                return userId.ToString();
            }
        }

        // Aktualizuj wiersz w lokalnym DataTable bez pełnego reload SQL.
        // Eliminuje 500-1500ms opóźnienia + ogromne CPU używane przez RestoreGridState.
        private void UpdateRowLocally(int id, string columnName, bool value)
        {
            // Znajdź wiersz w ORYGINALNEJ tabeli (display table jest rebuildowane przy filtrze)
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
                    targetRow[ktoCol] = GetOperatorName(uid);
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

            // Wymuś repaint wszystkich widocznych wierszy (kolory + ReadOnly checkboxów zależą od stanu).
            // Invalidate wystarczy - WPF/WinForms wykona PrePaint który ustawia kolory.
            dgvContracts.Invalidate();

            OdswiezStatystyki();
        }

        private void UpdateKalendarzFlag_NoReload(int id, string columnName, bool value)
        {
            string[] allowed = { "Utworzone", "Wysłane", "Otrzymane", "Posrednik" };
            if (Array.IndexOf(allowed, columnName) < 0) throw new InvalidOperationException("Nieobsługiwana kolumna: " + columnName);

            string? ktoCol = null, kiedyCol = null;
            switch (columnName)
            {
                case "Utworzone": ktoCol = "KtoUtw"; kiedyCol = "KiedyUtw"; break;
                case "Wysłane": ktoCol = "KtoWysl"; kiedyCol = "KiedyWysl"; break;
                case "Otrzymane": ktoCol = "KtoOtrzym"; kiedyCol = "KiedyOtrzm"; break;
            }

            if (value && ktoCol != null && !int.TryParse(UserID, out int userIdInt)) throw new InvalidOperationException("UserID musi być liczbą.");

            string sql = ktoCol == null
                ? $@"UPDATE dbo.HarmonogramDostaw SET [{columnName}] = @val WHERE [LP] = @id;"
                : $@"UPDATE dbo.HarmonogramDostaw SET [{columnName}] = @val, [{ktoCol}] = CASE WHEN @val = 1 THEN @kto ELSE NULL END, [{kiedyCol}] = CASE WHEN @val = 1 THEN GETDATE() ELSE NULL END WHERE [LP] = @id;";

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@val", value);
                cmd.Parameters.AddWithValue("@id", id);
                if (ktoCol != null) cmd.Parameters.AddWithValue("@kto", (object)int.Parse(UserID));

                conn.Open();
                if (cmd.ExecuteNonQuery() != 1) throw new Exception("Zaktualizowano nieprawidłową liczbę wierszy.");
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
            ApplyCombinedFilter();
        }

        private void DgvContracts_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            // Wiersz-nagłówek: nie renderuj zawartości komórek - cały rysunek robimy w RowPostPaint.
            if (IsHeaderRow(dgvContracts.Rows[e.RowIndex]))
            {
                e.Handled = true;
                return;
            }

            string colName = dgvContracts.Columns[e.ColumnIndex].Name;
            if (colName != "KtoUtw" && colName != "KtoWysl" && colName != "KtoOtrzym") return;

            // Określ kolumnę z ID
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

            if (string.IsNullOrWhiteSpace(name))
            {
                return; // Pozwól na domyślne renderowanie pustych komórek
            }

            e.PaintBackground(e.ClipBounds, true);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Pobierz lub wygeneruj avatar
            Image avatar = GetOrCreateAvatar(odbiorcaId, name);
            if (avatar != null)
            {
                int avatarY = e.CellBounds.Y + (e.CellBounds.Height - AVATAR_SIZE) / 2;
                int avatarX = e.CellBounds.X + 6;
                g.DrawImage(avatar, avatarX, avatarY, AVATAR_SIZE, AVATAR_SIZE);

                // Narysuj tekst obok avatara
                var textBounds = new Rectangle(
                    avatarX + AVATAR_SIZE + 6,
                    e.CellBounds.Y,
                    e.CellBounds.Width - AVATAR_SIZE - 18,
                    e.CellBounds.Height);

                bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
                Color textColor = isSelected ? Color.White : Color.FromArgb(44, 62, 80);

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

            // Spróbuj pobrać avatar z UserAvatarManager
            if (!string.IsNullOrWhiteSpace(odbiorcaId))
            {
                avatar = UserAvatarManager.GetAvatarRounded(odbiorcaId, AVATAR_SIZE);
            }

            // Jeśli brak avatara, wygeneruj domyślny z inicjałami
            if (avatar == null && !string.IsNullOrWhiteSpace(name))
            {
                avatar = UserAvatarManager.GenerateDefaultAvatar(name, cacheKey, AVATAR_SIZE);
            }

            if (avatar != null)
            {
                _avatarCache[cacheKey] = avatar;
            }

            return avatar;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Zwolnij zasoby avatarów
            foreach (var avatar in _avatarCache.Values)
            {
                avatar?.Dispose();
            }
            _avatarCache.Clear();
            base.OnFormClosed(e);
        }
    }
}