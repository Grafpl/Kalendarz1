using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Primitives;
using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokPaszaPisklak : Form
    {
        // Uwaga: zachowuję Twój connection string
        private static readonly string connectionPermission =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private DataTable _rawData = new();     // oryginalne dane z SQL
        private DataView _view;                 // widok z filtrem
        private bool _grouping = false;         // stan grupowania

        public string TextBoxValue { get; set; } = string.Empty;

        private enum OkFilter { Nierozliczone = 0, Rozliczone = 1, Wszystkie = 2 }

        private DateTimePicker DtFrom => (DateTimePicker)_dtFromHost.Control;
        private DateTimePicker DtTo => (DateTimePicker)_dtToHost.Control;

        public WidokPaszaPisklak()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            BuildPolishedGrid();
            WireEvents();

            // Domyślny zakres: bieżący miesiąc
            var now = DateTime.Today;
            DtFrom.Value = new DateTime(now.Year, now.Month, 1);
            DtTo.Value = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));

            LoadData(); // pierwsze wczytanie
            ApplyInitialSearchText();
        }

        public WidokPaszaPisklak(string initialSearch) : this()
        {
            TextBoxValue = initialSearch ?? string.Empty;
            SetTextBoxValue();
        }

        private void WireEvents()
        {
            // Toolstrip
            txtSearch.TextChanged += (_, __) => ApplyFilter();
            btnRefresh.Click += (_, __) => LoadData();
            btnGroup.CheckedChanged += (_, __) => ToggleGrouping(btnGroup.Checked);
            btnCopy.Click += (_, __) => CopySelectionToClipboard();
            btnExportCsv.Click += (_, __) => ExportToCsv();
            btnColumns.Click += (_, __) => ShowColumnsChooser();

            // Status / daty
            cbStatus.SelectedIndexChanged += (_, __) => LoadData();
            DtFrom.ValueChanged += (_, __) => LoadData();
            DtTo.ValueChanged += (_, __) => LoadData();

            // Presety zakresów
            btnPresets.DropDownItems.Clear();
            btnPresets.DropDownItems.Add("Dziś", null, (_, __) => QuickRange("today"));
            btnPresets.DropDownItems.Add("Ostatnie 7 dni", null, (_, __) => QuickRange("7d"));
            btnPresets.DropDownItems.Add("Bieżący miesiąc", null, (_, __) => QuickRange("month"));
            btnPresets.DropDownItems.Add("Poprzedni miesiąc", null, (_, __) => QuickRange("prevmonth"));
            btnPresets.DropDownItems.Add(new ToolStripSeparator());
            btnPresets.DropDownItems.Add("12 miesięcy wstecz", null, (_, __) => QuickRange("12m"));

            // Grid
            dataGridView1.DataBindingComplete += (_, __) =>
            {
                // Mapuj 0/1 na opis w kolumnie StatusOK (jeśli istnieje)
                if (dataGridView1.Columns.Contains("StatusOK"))
                {
                    foreach (DataGridViewRow r in dataGridView1.Rows)
                    {
                        var v = r.Cells["StatusOK"]?.Value;
                        if (v is int iv)
                            r.Cells["StatusOK"].Value = iv == 1 ? "Rozliczone" : "Nierozliczone";
                    }
                }
                UpdateFooter();
            };
            dataGridView1.CellDoubleClick += (_, __) => ShowRowDetails();
            dataGridView1.KeyDown += Grid_KeyDown;

            // Context menu
            miCopyCell.Click += (_, __) => CopyCurrentCell();
            miCopyRow.Click += (_, __) => CopyCurrentRow();
            miFilterByValue.Click += (_, __) => FilterByCurrentCellValue();
            miClearFilter.Click += (_, __) => { txtSearch.Text = string.Empty; ApplyFilter(); };
            miHistory.Click += (_, __) => ShowContractorHistory();
        }

        private void BuildPolishedGrid()
        {
            // Nagłówki
            dataGridView1.EnableHeadersVisualStyles = false;
            dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            dataGridView1.ColumnHeadersDefaultCellStyle.Font =
                new Font(new FontFamily("Segoe UI"), 9f, FontStyle.Bold);

            // Ogólne
            dataGridView1.ReadOnly = true;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AllowUserToOrderColumns = true;
            dataGridView1.AllowUserToResizeRows = false;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = true;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dataGridView1.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 249, 249);

            // Płynne przewijanie (DoubleBuffered via reflection)
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null, dataGridView1, new object[] { true });
        }

        private void ApplyInitialSearchText()
        {
            if (!string.IsNullOrWhiteSpace(TextBoxValue))
                txtSearch.Text = TextBoxValue;
        }

        public void SetTextBoxValue()
        {
            if (!string.IsNullOrWhiteSpace(TextBoxValue))
            {
                txtSearch.Text = TextBoxValue;
                ApplyFilter();
            }
        }

        public void SetTextBoxValue(string value)
        {
            TextBoxValue = value ?? string.Empty;
            SetTextBoxValue();
        }

        private void QuickRange(string code)
        {
            var today = DateTime.Today;
            switch (code)
            {
                case "today":
                    DtFrom.Value = today;
                    DtTo.Value = today;
                    break;
                case "7d":
                    DtFrom.Value = today.AddDays(-6);
                    DtTo.Value = today;
                    break;
                case "month":
                    DtFrom.Value = new DateTime(today.Year, today.Month, 1);
                    DtTo.Value = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                    break;
                case "prevmonth":
                    var prev = today.AddMonths(-1);
                    DtFrom.Value = new DateTime(prev.Year, prev.Month, 1);
                    DtTo.Value = new DateTime(prev.Year, prev.Month, DateTime.DaysInMonth(prev.Year, prev.Month));
                    break;
                case "12m":
                    DtFrom.Value = today.AddMonths(-12).AddDays(1);
                    DtTo.Value = today;
                    break;
            }
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                const string sql = @"
SELECT
    ST.Shortcut         AS Kontrahent,
    CAST(DK.walBrutto AS DECIMAL(18,2)) AS WartośćBrutto,
    DK.kod              AS Kod,
    DK.Data             AS DataDokumentu,
    DK.ok               AS StatusOK
FROM [HANDEL].[HM].[DK] DK
JOIN [HANDEL].[SSCommon].[STContractors] ST ON DK.khid = ST.ID
WHERE (@ok IS NULL OR DK.ok = @ok)
  AND DK.Data >= @from AND DK.Data < DATEADD(day, 1, @to)
  AND DK.seria = 'sFPP'
ORDER BY DK.Data DESC, ST.Shortcut ASC;";

                using var cn = new SqlConnection(connectionPermission);
                using var cmd = new SqlCommand(sql, cn);

                int? okParam = cbStatus.SelectedIndex switch
                {
                    (int)OkFilter.Nierozliczone => 0,
                    (int)OkFilter.Rozliczone => 1,
                    _ => (int?)null
                };

                cmd.Parameters.AddWithValue("@ok", (object?)okParam ?? DBNull.Value);
                cmd.Parameters.Add("@from", SqlDbType.Date).Value = DtFrom.Value.Date;
                cmd.Parameters.Add("@to", SqlDbType.Date).Value = DtTo.Value.Date;

                cn.Open();
                using var rdr = cmd.ExecuteReader();

                _rawData = new DataTable();
                _rawData.Load(rdr);

                _view = new DataView(_rawData);
                dataGridView1.DataSource = _view;

                ConfigureColumns();
                ApplyFilter();
                UpdateFooter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd wczytywania danych:\n" + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void ConfigureColumns()
        {
            foreach (DataGridViewColumn c in dataGridView1.Columns)
            {
                c.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
                c.MinimumWidth = 80;
            }

            if (dataGridView1.Columns.Contains("WartośćBrutto"))
            {
                var col = dataGridView1.Columns["WartośćBrutto"];
                col.DefaultCellStyle.Format = "N2";
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }

            if (dataGridView1.Columns.Contains("DataDokumentu"))
            {
                var col = dataGridView1.Columns["DataDokumentu"];
                col.DefaultCellStyle.Format = "yyyy-MM-dd";
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.MinimumWidth = 110;
            }

            if (dataGridView1.Columns.Contains("StatusOK"))
            {
                var col = dataGridView1.Columns["StatusOK"];
                col.HeaderText = "Status";
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.MinimumWidth = 110;
            }

            dataGridView1.AutoResizeColumns();
        }

        private static decimal AsDecimal(object v)
        {
            if (v == null || v == DBNull.Value) return 0m;
            switch (v)
            {
                case decimal d: return d;
                case double db: return Convert.ToDecimal(db);
                case float f: return Convert.ToDecimal(f);
                case int i: return i;
                case long l: return l;
                case short s: return s;
                case string str when decimal.TryParse(str, NumberStyles.Any, CultureInfo.CurrentCulture, out var d1): return d1;
                case string str2 when decimal.TryParse(str2, NumberStyles.Any, CultureInfo.InvariantCulture, out var d2): return d2;
                default: return Convert.ToDecimal(v, CultureInfo.InvariantCulture);
            }
        }

        private void ToggleGrouping(bool enabled)
        {
            _grouping = enabled;
            if (_rawData.Rows.Count == 0) return;

            if (!_grouping)
            {
                _view = new DataView(_rawData);
                dataGridView1.DataSource = _view;
                ConfigureColumns();
                ApplyFilter();
                return;
            }

            // Agregacja po kontrahencie
            var grouped = _rawData
                .AsEnumerable()
                .GroupBy(r => r.Field<string>("Kontrahent"))
                .Select(g =>
                {
                    decimal sum = g.Sum(r => AsDecimal(r["WartośćBrutto"]));
                    int cnt = g.Count();
                    DateTime minD = g.Min(r => r.Field<DateTime>("DataDokumentu"));
                    DateTime maxD = g.Max(r => r.Field<DateTime>("DataDokumentu"));

                    return new
                    {
                        Kontrahent = g.Key,
                        LiczbaDokumentów = cnt,
                        SumaWartośćBrutto = sum,
                        NajstarszaData = minD,
                        NajnowszaData = maxD
                    };
                }).ToList();

            var dt = new DataTable();
            dt.Columns.Add("Kontrahent", typeof(string));
            dt.Columns.Add("LiczbaDokumentów", typeof(int));
            dt.Columns.Add("SumaWartośćBrutto", typeof(decimal));
            dt.Columns.Add("NajstarszaData", typeof(DateTime));
            dt.Columns.Add("NajnowszaData", typeof(DateTime));

            foreach (var x in grouped)
                dt.Rows.Add(x.Kontrahent, x.LiczbaDokumentów, x.SumaWartośćBrutto, x.NajstarszaData, x.NajnowszaData);

            _view = new DataView(dt);
            dataGridView1.DataSource = _view;

            if (dataGridView1.Columns.Contains("SumaWartośćBrutto"))
            {
                var c = dataGridView1.Columns["SumaWartośćBrutto"];
                c.DefaultCellStyle.Format = "N2";
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            if (dataGridView1.Columns.Contains("NajstarszaData"))
                dataGridView1.Columns["NajstarszaData"].DefaultCellStyle.Format = "yyyy-MM-dd";
            if (dataGridView1.Columns.Contains("NajnowszaData"))
                dataGridView1.Columns["NajnowszaData"].DefaultCellStyle.Format = "yyyy-MM-dd";

            dataGridView1.AutoResizeColumns();
            UpdateFooter();
        }

        // -------- Filtrowanie po wielu kolumnach -----------
        private void ApplyFilter()
        {
            if (_view == null) return;

            string text = (txtSearch.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                _view.RowFilter = string.Empty;
            }
            else
            {
                string esc(string s) => s.Replace("'", "''").Replace("[", "[[").Replace("]", "]]").Replace("%", "[%]").Replace("*", "[*]");
                string t = esc(text);

                // Szukamy w kluczowych kolumnach
                var filters = new[]
                {
                    $"Convert(Kontrahent, 'System.String') LIKE '%{t}%'",
                    $"Convert(Kod, 'System.String') LIKE '%{t}%'",
                    $"Convert(DataDokumentu, 'System.String') LIKE '%{t}%'"
                };
                _view.RowFilter = string.Join(" OR ", filters);
            }

            UpdateFooter();
        }

        private void UpdateFooter()
        {
            try
            {
                int rows = dataGridView1.Rows.Cast<DataGridViewRow>().Count(r => r.Visible);
                lblCount.Text = $"Wiersze: {rows}";

                decimal sum = 0m;

                // w trybie grupowania bierzemy kolumnę sumy, w zwykłym – wartość brutto
                string col = _grouping ? "SumaWartośćBrutto" : "WartośćBrutto";
                if (dataGridView1.Columns.Contains(col))
                {
                    foreach (DataGridViewRow r in dataGridView1.Rows)
                    {
                        if (!r.Visible) continue;
                        var v = r.Cells[col].Value;
                        sum += AsDecimal(v);
                    }
                }
                lblSum.Text = $"Suma walBrutto: {sum:N2}";
            }
            catch
            {
                lblSum.Text = "Suma walBrutto: -";
            }
        }

        // -------- Kopiowanie / eksport --------
        private void CopySelectionToClipboard()
        {
            dataGridView1.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            if (dataGridView1.GetCellCount(DataGridViewElementStates.Selected) > 0)
                Clipboard.SetDataObject(dataGridView1.GetClipboardContent());
        }

        private void CopyCurrentCell()
        {
            if (dataGridView1.CurrentCell != null && dataGridView1.CurrentCell.Value != null)
                Clipboard.SetText(dataGridView1.CurrentCell.Value.ToString());
        }

        private void CopyCurrentRow()
        {
            if (dataGridView1.CurrentRow == null) return;
            var sb = new StringBuilder();
            foreach (DataGridViewCell c in dataGridView1.CurrentRow.Cells)
            {
                if (c.OwningColumn != null)
                    sb.Append(c.OwningColumn.HeaderText).Append('=');
                sb.Append(c.Value?.ToString() ?? "").Append('\t');

            }
            Clipboard.SetText(sb.ToString());
        }

        private void ExportToCsv()
        {
            if (dataGridView1.Rows.Count == 0) return;

            using var sfd = new SaveFileDialog
            {
                Title = "Zapisz CSV",
                FileName = "pasza_pisklak.csv",
                Filter = "CSV (*.csv)|*.csv"
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var sep = ";";
                using var sw = new StreamWriter(sfd.FileName, false, Encoding.UTF8);

                // headers
                var headers = dataGridView1.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText);
                sw.WriteLine(string.Join(sep, headers.Select(EscapeCsv)));

                // rows
                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    if (row.IsNewRow || !row.Visible) continue;
                    var vals = row.Cells.Cast<DataGridViewCell>().Select(c => EscapeCsv(c.Value?.ToString() ?? ""));
                    sw.WriteLine(string.Join(sep, vals));
                }

                sw.Flush();
                MessageBox.Show(this, "Wyeksportowano do CSV.", "Gotowe", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd eksportu CSV:\n" + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            static string EscapeCsv(string s)
            {
                if (s.Contains("\"") || s.Contains(";") || s.Contains("\n"))
                    return "\"" + s.Replace("\"", "\"\"") + "\"";
                return s;
            }
        }

        // -------- Filtry kontekstowe --------
        private void FilterByCurrentCellValue()
        {
            if (dataGridView1.CurrentCell == null) return;
            var val = dataGridView1.CurrentCell.Value?.ToString();
            if (string.IsNullOrEmpty(val)) return;

            txtSearch.Text = val;
            ApplyFilter();
        }

        private void Grid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.F) { txtSearch.Focus(); txtSearch.SelectAll(); e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.C) { CopySelectionToClipboard(); e.Handled = true; }
            if (e.KeyCode == Keys.F5) { LoadData(); e.Handled = true; }
        }

        // -------- Podgląd szczegółów wiersza --------
        private void ShowRowDetails()
        {
            if (dataGridView1.CurrentRow == null) return;

            var sb = new StringBuilder();
            foreach (DataGridViewCell c in dataGridView1.CurrentRow.Cells)
                sb.AppendLine($"{c.OwningColumn?.HeaderText}: {c.Value}");

            MessageBox.Show(this, sb.ToString(), "Szczegóły pozycji", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // -------- Panel wyboru kolumn (widoczność) --------
        private void ShowColumnsChooser()
        {
            if (dataGridView1.Columns.Count == 0) return;

            using var dlg = new Form
            {
                Text = "Wybór kolumn",
                Width = 320,
                Height = 260,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };
            dlg.Controls.Add(panel);

            foreach (DataGridViewColumn col in dataGridView1.Columns)
            {
                var cb = new CheckBox { Text = col.HeaderText, Checked = col.Visible, Tag = col, AutoSize = true, Margin = new Padding(4) };
                panel.Controls.Add(cb);
            }

            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 36 };
            dlg.Controls.Add(btnOk);
            dlg.AcceptButton = btnOk;

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                foreach (var ctrl in panel.Controls.OfType<CheckBox>())
                {
                    if (ctrl.Tag is DataGridViewColumn col)
                        col.Visible = ctrl.Checked;
                }
            }
        }

        // -------- Historia kontrahenta (12 m-cy) --------
        private void ShowContractorHistory()
        {
            if (dataGridView1.CurrentRow == null) return;
            var kontr = dataGridView1.CurrentRow.Cells["Kontrahent"]?.Value?.ToString();
            if (string.IsNullOrWhiteSpace(kontr)) return;

            var to = DateTime.Today;
            var from = to.AddMonths(-12).AddDays(1);

            const string sql = @"
SELECT
    DK.Data AS DataDokumentu,
    DK.kod  AS Kod,
    CAST(DK.walBrutto AS DECIMAL(18,2)) AS WartośćBrutto,
    CASE DK.ok WHEN 1 THEN 'Rozliczone' ELSE 'Nierozliczone' END AS Status
FROM [HANDEL].[HM].[DK] DK
JOIN [HANDEL].[SSCommon].[STContractors] ST ON DK.khid = ST.ID
WHERE ST.Shortcut = @kontr
  AND DK.Data >= @from AND DK.Data < DATEADD(day,1,@to)
  AND DK.seria = 'sFPP'
ORDER BY DK.Data DESC;";

            try
            {
                using var cn = new SqlConnection(connectionPermission);
                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.Add("@kontr", SqlDbType.NVarChar, 128).Value = kontr;
                cmd.Parameters.Add("@from", SqlDbType.Date).Value = from.Date;
                cmd.Parameters.Add("@to", SqlDbType.Date).Value = to.Date;

                cn.Open();
                var dt = new DataTable();
                using var rdr = cmd.ExecuteReader();
                dt.Load(rdr);

                using var f = new Form
                {
                    Text = $"Historia: {kontr} (ostatnie 12 m-cy)",
                    Width = 900,
                    Height = 560,
                    StartPosition = FormStartPosition.CenterParent
                };
                var grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
                };
                grid.DataSource = dt;
                f.Controls.Add(grid);
                f.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd pobierania historii:\n" + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
