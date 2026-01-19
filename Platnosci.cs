using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class Platnosci : Form
    {
        private readonly string connectionString;
        private CancellationTokenSource cancellationTokenSource;
        private BindingSource bindingSource1;
        private BindingSource bindingSource2;
        private DataTable dataTable1;
        private DataTable dataTable2;
        private readonly object dataLock = new object();
        private bool isLoading = false;

        public Platnosci()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True;Connection Timeout=30";

            InitializeBindingSources();
            InitializeEventHandlers();
            SetupDataGridViewStyles();

            comboBoxFiltr.SelectedIndex = 0;

            toolTip1 ??= new ToolTip { InitialDelay = 500, ShowAlways = true };

            this.Load += async (_, __) => await RefreshDataAsync();
        }

        private void InitializeBindingSources()
        {
            bindingSource1 = new BindingSource();
            bindingSource2 = new BindingSource();
            dataTable1 = new DataTable();
            dataTable2 = new DataTable();

            dataGridView1.DataSource = bindingSource1;
            dataGridView2.DataSource = bindingSource2;
        }

        private void InitializeEventHandlers()
        {
            dataGridView1.RowPrePaint += DataGridView1_RowPrePaint;
            dataGridView1.CellFormatting += DataGridView_CellFormatting;
            dataGridView1.DataError += DataGridView_DataError;

            dataGridView2.RowPrePaint += DataGridView2_RowPrePaint;
            dataGridView2.CellFormatting += DataGridView_CellFormatting;
            dataGridView2.DataError += DataGridView_DataError;

            textBox1.TextChanged += (_, __) => UpdateFiltersAndSums();
            showAllCheckBox.CheckedChanged += (_, __) => { UkryjKolumny(); UpdateTop3List(); };

            FormClosing += (_, __) => cancellationTokenSource?.Cancel();
        }

        private void SetupDataGridViewStyles()
        {
            ConfigureDataGridView(dataGridView1);
            ConfigureDataGridView(dataGridView2);
        }

        private void ConfigureDataGridView(DataGridView dgv)
        {
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly = true;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = true;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgv.RowHeadersVisible = false;
            dgv.BackgroundColor = Color.White;
            dgv.BorderStyle = BorderStyle.None;
            dgv.GridColor = Color.FromArgb(229, 231, 235);

            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(37, 99, 235);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(5);
            dgv.ColumnHeadersHeight = 40;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f);
            dgv.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);  // większe odstępy
            dgv.RowTemplate.Height = 36;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(147, 197, 253);
            dgv.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 58, 138);

            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.SetProperty,
                null, dgv, new object[] { true });
        }

        private async void refreshButton_Click(object sender, EventArgs e) => await RefreshDataAsync();

        private async Task RefreshDataAsync()
        {
            if (isLoading) return;

            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();

            try
            {
                isLoading = true;
                ShowLoadingState(true);
                refreshButton.Enabled = false;

                var t1 = LoadHodowcyDataAsync(cancellationTokenSource.Token);
                var t2 = LoadUbojniaDataAsync(cancellationTokenSource.Token);
                await Task.WhenAll(t1, t2);

                FormatujKolumny();
                UkryjKolumny();
                UpdateFiltersAndSums();
                UpdateTop3List();

                UpdateStatusBar($"Ostatnie odświeżenie: {DateTime.Now:HH:mm:ss}");
            }
            catch (OperationCanceledException)
            {
                UpdateStatusBar("Operacja anulowana");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas pobierania danych: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogError(ex);
            }
            finally
            {
                isLoading = false;
                ShowLoadingState(false);
                refreshButton.Enabled = true;
            }
        }

        private async Task LoadHodowcyDataAsync(CancellationToken ct)
        {
            const string query = @"
                SELECT DISTINCT 
                    C.Shortcut AS Hodowca,
                    DK.kod AS NumerDokumentu,
                    DK.walbrutto AS Kwota,
                    PN.kwotarozl AS Rozliczone,
                    PN.wartosc AS DoZaplacenia,
                    DK.data AS DataOdbioru,
                    DK.plattermin AS DataTermin,
                    PN.Termin AS TerminPrawdziwy,
                    DATEDIFF(day, DK.data, PN.Termin) AS Termin,
                    DATEDIFF(day, DK.data, GETDATE()) AS Obecny,
                    (DATEDIFF(day, DK.data, PN.Termin) - DATEDIFF(day, DK.data, GETDATE())) AS Roznica,
                    CASE 
                        WHEN (DATEDIFF(day, DK.data, PN.Termin) - DATEDIFF(day, DK.data, GETDATE())) <= -7 THEN 'Bardzo przeterminowane'
                        WHEN (DATEDIFF(day, DK.data, PN.Termin) - DATEDIFF(day, DK.data, GETDATE())) <= 0 THEN 'Przeterminowane'
                        WHEN (DATEDIFF(day, DK.data, PN.Termin) - DATEDIFF(day, DK.data, GETDATE())) <= 7 THEN 'Zbliżające (≤7 dni)'
                        ELSE 'W terminie'
                    END AS Status
                FROM [HANDEL].[HM].[DK] DK WITH (NOLOCK)
                JOIN [HANDEL].[HM].[DP] DP WITH (NOLOCK) ON DK.id = DP.super
                JOIN [HANDEL].[HM].[PN] PN WITH (NOLOCK) ON DK.id = PN.dkid
                JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON DK.khid = C.id
                WHERE DK.aktywny = 1
                    AND DK.ok = 0
                    AND DK.anulowany = 0
                    AND DK.typ_dk IN ('FVR', 'FVZ')
                    AND DP.kod IN ('Kurczak żywy - 8', 'Kurczak żywy -7')
                ORDER BY Roznica ASC";
            await LoadDataAsync(query, dataTable1, bindingSource1, ct);
        }

        private async Task LoadUbojniaDataAsync(CancellationToken ct)
        {
            const string query = @"
                SELECT 
                    C.Shortcut AS Klient,
                    DK.kod AS NumerFaktury,
                    DK.walbrutto AS Kwota,
                    PN.kwotarozl AS Rozliczone,
                    PN.wartosc AS DoZaplacenia,
                    DK.data AS DataSprzedazy,
                    DK.plattermin AS DataTermin,
                    PN.Termin AS TerminPrawdziwy,
                    DATEDIFF(day, DK.data, PN.Termin) AS Termin,
                    DATEDIFF(day, DK.data, GETDATE()) AS Obecny,
                    (DATEDIFF(day, DK.data, PN.Termin) - DATEDIFF(day, DK.data, GETDATE())) AS Roznica,
                    -- STATUS z dodanym 'Zbliżające (≤7 dni)'
                    CASE 
                        WHEN (DATEDIFF(day, DK.data, PN.Termin) - DATEDIFF(day, DK.data, GETDATE())) <= -14 THEN 'Krytycznie przeterminowane'
                        WHEN (DATEDIFF(day, DK.data, PN.Termin) - DATEDIFF(day, DK.data, GETDATE())) <= -7 THEN 'Bardzo przeterminowane'
                        WHEN (DATEDIFF(day, DK.data, PN.Termin) - DATEDIFF(day, DK.data, GETDATE())) <= 0 THEN 'Przeterminowane'
                        WHEN (DATEDIFF(day, DK.data, PN.Termin) - DATEDIFF(day, DK.data, GETDATE())) <= 7 THEN 'Zbliżające (≤7 dni)'
                        ELSE 'W terminie'
                    END AS StatusPlatnosci,
                    -- OPIS
                    CASE 
                        WHEN PN.wartosc > 0 AND PN.wartosc <= 800 THEN 'Podatek rolniczy'
                        WHEN PN.wartosc < 0 THEN 'Ubojnia nam'
                        ELSE 'My hodowcy'
                    END AS Opis
                FROM [HANDEL].[HM].[DK] DK WITH (NOLOCK)
                JOIN [HANDEL].[HM].[DP] DP WITH (NOLOCK) ON DK.id = DP.super
                JOIN [HANDEL].[HM].[PN] PN WITH (NOLOCK) ON DK.id = PN.dkid
                JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON DK.khid = C.id
                WHERE DK.aktywny = 1
                    AND DK.ok = 0
                    AND DK.anulowany = 0
                    AND DK.typ_dk IN ('FVR', 'FVZ', 'FVS')
                    AND DP.kod IN ('Kurczak żywy - 8 SPRZEDAŻ', 'Kurczak żywy - 7 SPRZEDAŻ')
                ORDER BY DataSprzedazy DESC";
            await LoadDataAsync(query, dataTable2, bindingSource2, ct);
        }

        private async Task LoadDataAsync(string query, DataTable targetTable, BindingSource targetBinding, CancellationToken ct)
        {
            using var conn = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(query, conn) { CommandTimeout = 60 };
            await conn.OpenAsync(ct);
            using var rdr = await cmd.ExecuteReaderAsync(ct);

            var newTable = new DataTable();
            newTable.Load(rdr);

            if (!ct.IsCancellationRequested)
            {
                BeginInvoke(new Action(() =>
                {
                    lock (dataLock)
                    {
                        targetTable.Clear();
                        targetTable.Merge(newTable);
                        targetBinding.DataSource = targetTable;
                    }
                }));
            }
        }

        private void DataGridView1_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dataGridView1.Rows[e.RowIndex];

            if (TryGetInt(row, "Roznica", out int roznica))
            {
                if (roznica <= -7)
                    SetRowColors(row, Color.FromArgb(254, 226, 226), Color.FromArgb(153, 27, 27));
                else if (roznica <= 0)
                    SetRowColors(row, Color.FromArgb(254, 243, 199), Color.FromArgb(146, 64, 14));
                else if (roznica <= 7) // Zbliżające się
                    SetRowColors(row, Color.FromArgb(254, 249, 195), Color.FromArgb(133, 77, 14));
                else
                    SetRowColors(row, Color.FromArgb(220, 252, 231), Color.FromArgb(22, 101, 52));
            }
        }

        private void DataGridView2_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dataGridView2.Rows[e.RowIndex];

            // Szary „Podatek rolniczy” (0–800] – gdy wiersze są ujawnione
            if (TryGetDecimal(row, "DoZaplacenia", out decimal dz) && dz > 0 && dz <= 800)
            {
                SetRowColors(row, Color.FromArgb(243, 244, 246), Color.FromArgb(75, 85, 99));
                return; // priorytet nad klasyfikacją daty
            }

            if (TryGetInt(row, "Roznica", out int roznica))
            {
                if (roznica <= -14)
                    SetRowColors(row, Color.FromArgb(254, 202, 202), Color.FromArgb(127, 29, 29));
                else if (roznica <= -7)
                    SetRowColors(row, Color.FromArgb(254, 226, 226), Color.FromArgb(153, 27, 27));
                else if (roznica <= 0)
                    SetRowColors(row, Color.FromArgb(254, 243, 199), Color.FromArgb(146, 64, 14));
                else if (roznica <= 7) // Zbliżające się
                    SetRowColors(row, Color.FromArgb(254, 249, 195), Color.FromArgb(133, 77, 14));
                else
                    SetRowColors(row, Color.FromArgb(220, 252, 231), Color.FromArgb(22, 101, 52));
            }
        }

        private static void SetRowColors(DataGridViewRow row, Color back, Color fore)
        {
            row.DefaultCellStyle.BackColor = back;
            row.DefaultCellStyle.ForeColor = fore;
        }

        private static bool TryGetInt(DataGridViewRow row, string col, out int value)
        {
            value = 0;
            var cell = row.Cells[col]?.Value;
            return cell != null && int.TryParse(Convert.ToString(cell), out value);
        }

        private static bool TryGetDecimal(DataGridViewRow row, string col, out decimal value)
        {
            value = 0m;
            var cell = row.Cells[col]?.Value;
            return cell != null && decimal.TryParse(Convert.ToString(cell, CultureInfo.InvariantCulture), out value);
        }

        private void DataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var dgv = (DataGridView)sender;

            // format dat
            if (dgv.Columns[e.ColumnIndex].Name.Contains("Data") || dgv.Columns[e.ColumnIndex].Name.Contains("Termin"))
            {
                if (e.Value != null && DateTime.TryParse(e.Value.ToString(), out var date))
                {
                    e.Value = date.ToString("yyyy-MM-dd");
                    e.FormattingApplied = true;
                }
            }

            // wyróżnienie wartości ujemnych
            if (dgv.Columns[e.ColumnIndex].Name == "DoZaplacenia" && e.Value != null &&
                decimal.TryParse(e.Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v < 0)
            {
                e.CellStyle.ForeColor = Color.Red;
                e.CellStyle.Font = new Font(dgv.Font, FontStyle.Bold);
            }
        }

        private void FormatujKolumny()
        {
            FormatujKolumnyDlaGrid(dataGridView1);
            FormatujKolumnyDlaGrid(dataGridView2);
        }

        private void FormatujKolumnyDlaGrid(DataGridView dgv)
        {
            foreach (DataGridViewColumn column in dgv.Columns)
            {
                if (column.Name is "Kwota" or "Rozliczone" or "DoZaplacenia")
                {
                    column.DefaultCellStyle.Format = "N2";
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    if (column.Name == "DoZaplacenia")
                        column.DefaultCellStyle.Font = new Font(dgv.Font, FontStyle.Bold);
                }

                if (column.Name.Contains("Data") || column.Name.Contains("Termin"))
                {
                    column.DefaultCellStyle.Format = "dd.MM.yyyy";
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }

                if (column.Name is "Roznica" or "Obecny" or "Termin")
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            }
        }

        // ====== FILTRY: wyszukiwarka + status + podatek rolniczy (0–800) ======
        private void comboBoxFiltr_SelectedIndexChanged(object sender, EventArgs e) => UpdateFiltersAndSums();
        private void chkPokazPodatekRolniczy_CheckedChanged(object sender, EventArgs e) => UpdateFiltersAndSums();

        private void UpdateFiltersAndSums()
        {
            var isUbojnia = tabControl1.SelectedIndex == 1;

            // wyszukiwarka
            string text = (textBox1.Text ?? "").Replace("'", "''");
            string nameCol = isUbojnia ? "Klient" : "Hodowca";
            string searchFilter = string.IsNullOrWhiteSpace(text) ? "" : $"{nameCol} LIKE '%{text}%'";

            // filtr statusu
            string statusSel = comboBoxFiltr.SelectedItem?.ToString() ?? "Wszystkie";
            string statusFilter = statusSel switch
            {
                "Przeterminowane" => "Roznica <= 0",
                "Zbliżające (≤7 dni)" => "Roznica > 0 AND Roznica <= 7",
                "W terminie" => "Roznica > 7",
                _ => ""
            };

            // filtr podatku rolniczego – tylko Ubojnia
            string taxFilter = "";
            if (isUbojnia && !chkPokazPodatekRolniczy.Checked)
                taxFilter = "NOT (DoZaplacenia > 0 AND DoZaplacenia <= 800)";

            string Compose(params string[] parts)
            {
                var nonEmpty = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
                return string.Join(" AND ", nonEmpty);
            }

            var bs = isUbojnia ? bindingSource2 : bindingSource1;
            bs.Filter = Compose(searchFilter, statusFilter, taxFilter);

            // UI dla checkboxa
            chkPokazPodatekRolniczy.Visible = isUbojnia;

            ObliczISumujDoZaplacenia();
            UpdateTop3List();
            UpdateStatusBar($"Znaleziono rekordów: {(isUbojnia ? dataGridView2 : dataGridView1).RowCount}");
        }

        private void UkryjKolumny()
        {
            if (!showAllCheckBox.Checked)
            {
                string[] visible1 = { "Hodowca", "DoZaplacenia", "Roznica", "Status", "DataOdbioru" };
                foreach (DataGridViewColumn c in dataGridView1.Columns) c.Visible = visible1.Contains(c.Name);

                string[] visible2 = { "Klient", "DoZaplacenia", "Roznica", "StatusPlatnosci", "Opis", "DataSprzedazy" };
                foreach (DataGridViewColumn c in dataGridView2.Columns) c.Visible = visible2.Contains(c.Name);
            }
            else
            {
                foreach (DataGridViewColumn c in dataGridView1.Columns) c.Visible = true;
                foreach (DataGridViewColumn c in dataGridView2.Columns) c.Visible = true;
            }
        }

        private void ObliczISumujDoZaplacenia()
        {
            // HODOWCY
            decimal sumaH = 0, sumPrzH = 0, sumZblH = 0, sumTermH = 0;
            int cntPrzH = 0, cntZblH = 0, cntTermH = 0;

            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (TryGetInt(row, "Roznica", out int r) &&
                    TryGetDecimal(row, "DoZaplacenia", out decimal v))
                {
                    if (r <= 0) { sumPrzH += v; cntPrzH++; }
                    else if (r <= 7) { sumZblH += v; cntZblH++; }
                    else { sumTermH += v; cntTermH++; }
                    sumaH += v;
                }
            }

            // UBOJNIA
            decimal sumaU = 0;
            int przU = 0, zblU = 0, inTermU = 0;

            foreach (DataGridViewRow row in dataGridView2.Rows)
            {
                if (TryGetDecimal(row, "DoZaplacenia", out decimal v))
                {
                    sumaU += v;
                    if (TryGetInt(row, "Roznica", out int r))
                    {
                        if (r <= 0) przU++;
                        else if (r <= 7) zblU++;
                        else inTermU++;
                    }
                }
            }

            if (tabControl1.SelectedIndex == 0)
            {
                textBox2.Text = $"{sumaH:N2} zł";
                textBoxPrzeterminowane.Text = $"{sumPrzH:N2} zł ({cntPrzH})";
                textBoxZblizajace.Text = $"{sumZblH:N2} zł ({cntZblH})";   // SUMA ZBLIŻAJĄCYCH – wyeksponowana
                textBoxWTerminie.Text = $"{sumTermH:N2} zł ({cntTermH})";
            }
            else
            {
                textBox2.Text = $"{sumaU:N2} zł";
                textBoxPrzeterminowane.Text = $"{przU} faktur";
                textBoxZblizajace.Text = $"{zblU} faktur";
                textBoxWTerminie.Text = $"{inTermU} faktur";
            }

            textBoxSumaUbojnia.Text = $"{sumaU:N2} zł";

            toolTip1.SetToolTip(textBox2,
                tabControl1.SelectedIndex == 0
                ? $"Przeterminowane: {sumPrzH:N2} zł ({cntPrzH})\nZbliżające (≤7 dni): {sumZblH:N2} zł ({cntZblH})\nW terminie: {sumTermH:N2} zł ({cntTermH})"
                : $"Przeterminowane: {przU} faktur\nZbliżające (≤7 dni): {zblU} faktur\nW terminie: {inTermU} faktur");
        }

        private void UpdateTop3List()
        {
            listViewTop3.BeginUpdate();
            listViewTop3.Items.Clear();

            var dgv = tabControl1.SelectedIndex == 0 ? dataGridView1 : dataGridView2;
            string kontrahentCol = tabControl1.SelectedIndex == 0 ? "Hodowca" : "Klient";

            var rows = dgv.Rows.Cast<DataGridViewRow>()
                .Where(r => r.Cells["DoZaplacenia"].Value != null &&
                            decimal.TryParse(Convert.ToString(r.Cells["DoZaplacenia"].Value), out _))
                .Select(r => new
                {
                    Nazwa = Convert.ToString(r.Cells[kontrahentCol].Value) ?? "",
                    Kwota = Convert.ToDecimal(r.Cells["DoZaplacenia"].Value)
                })
                .GroupBy(x => x.Nazwa)
                .Select(g => new { Nazwa = g.Key, Suma = g.Sum(z => z.Kwota) })
                .OrderByDescending(x => x.Suma)
                .Take(3)
                .ToList();

            int lp = 1;
            foreach (var r in rows)
            {
                var item = new System.Windows.Forms.ListViewItem(lp.ToString());
                item.SubItems.Add(r.Nazwa);
                item.SubItems.Add($"{r.Suma:N2} zł");
                listViewTop3.Items.Add(item);
                lp++;
            }
            listViewTop3.EndUpdate();

            toolTip1.SetToolTip(listViewTop3, tabControl1.SelectedIndex == 0
                ? "Top 3 hodowców wg sumy 'Do zapłacenia'"
                : "Top 3 klientów wg sumy 'Do zapłacenia'");
        }

        private void DataGridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            LogError(new Exception($"DataGridView error at row {e.RowIndex}, column {e.ColumnIndex}"));
        }

        private void ShowLoadingState(bool show)
        {
            this.Cursor = show ? Cursors.WaitCursor : Cursors.Default;
            progressBar1.Visible = show;
            if (show) progressBar1.Style = ProgressBarStyle.Marquee;
        }

        private void UpdateStatusBar(string message) => lblStatus.Text = message;

        private void LogError(Exception ex)
        {
            try
            {
                System.IO.File.AppendAllText("errors.log",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.Message}\n{ex.StackTrace}\n\n");
            }
            catch { }
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== RAPORT PŁATNOŚCI - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                sb.AppendLine();
                sb.AppendLine($"Zakładka: {(tabControl1.SelectedIndex == 0 ? "Hodowcy" : "Ubojnia")}");
                sb.AppendLine($"Suma: {textBox2.Text}");
                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Raport skopiowany do schowka.", "Sukces",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas generowania raportu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonExportExcel_Click(object sender, EventArgs e) => ExportToExcel();

        private void ExportToExcel()
        {
            try
            {
                using var dlg = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    FileName = $"Platnosci_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                using var w = new System.IO.StreamWriter(dlg.FileName, false, Encoding.UTF8);

                // Hodowcy
                w.WriteLine("=== PŁATNOŚCI HODOWCÓW ===");
                w.WriteLine("Hodowca;NumerDokumentu;Kwota;Rozliczone;DoZaplacenia;DataOdbioru;DataTermin;TerminPrawdziwy;Termin;Obecny;Roznica;Status");
                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    var values = row.Cells.Cast<DataGridViewCell>().Select(c => c.Value?.ToString()?.Replace(";", ",") ?? "");
                    w.WriteLine(string.Join(";", values));
                }

                // Ubojnia
                w.WriteLine();
                w.WriteLine("=== PŁATNOŚCI UBOJNI ===");
                w.WriteLine("Klient;NumerFaktury;Kwota;Rozliczone;DoZaplacenia;DataSprzedazy;DataTermin;TerminPrawdziwy;Termin;Obecny;Roznica;StatusPlatnosci;Opis");
                foreach (DataGridViewRow row in dataGridView2.Rows)
                {
                    var values = row.Cells.Cast<DataGridViewCell>().Select(c => c.Value?.ToString()?.Replace(";", ",") ?? "");
                    w.WriteLine(string.Join(";", values));
                }

                w.WriteLine();
                w.WriteLine("=== PODSUMOWANIE ===");
                w.WriteLine($"Data raportu: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                MessageBox.Show($"Wyeksportowano do pliku:\n{dlg.FileName}", "Sukces",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            label6.Text = tabControl1.SelectedIndex == 0 ? "Szukaj hodowcy:" : "Szukaj klienta:";
            chkPokazPodatekRolniczy.Visible = tabControl1.SelectedIndex == 1;
            textBox1.Text = "";
            UpdateFiltersAndSums();
        }
    }
}
