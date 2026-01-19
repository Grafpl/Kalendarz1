using Microsoft.Data.SqlClient;
using System;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;
using System.Linq;

namespace Kalendarz1
{
    public partial class WidokWaga : System.Windows.Forms.Form
    {
        private const string CONNECTION_STRING = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;Connection Timeout=30;";

        private DataTable? _dataSource;
        private CancellationTokenSource? _searchCancellationTokenSource;
        private readonly System.Windows.Forms.Timer _searchTimer;
        private bool _isLoading = false;

        public string? TextBoxValue { get; set; }

        public WidokWaga()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            InitializeForm();

            _searchTimer = new System.Windows.Forms.Timer();
            _searchTimer.Interval = 300;
            _searchTimer.Tick += SearchTimer_Tick;
        }

        private void InitializeForm()
        {
            this.Text = "Wagi - System Monitorowania";
            this.StartPosition = FormStartPosition.CenterScreen;

            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.DoubleBuffer, true);

            ConfigureDataGridView();

            // Dodaj menu kontekstowe do DataGridView
            AddContextMenu();

            _ = LoadDataAsync();
        }

        private void AddContextMenu()
        {
            var contextMenu = new ContextMenuStrip();

            // Opcja 1: Pokaż raport dla wybranego dostawcy
            var menuItemDostawca = new ToolStripMenuItem("📊 Raport dla tego dostawcy");
            menuItemDostawca.Click += (s, e) => ShowReportForSelectedSupplier();

            // Opcja 2: Pokaż raport dla wybranej partii
            var menuItemPartia = new ToolStripMenuItem("📈 Analiza partii");
            menuItemPartia.Click += (s, e) => ShowBatchAnalysis();

            // Separator
            contextMenu.Items.Add(menuItemDostawca);
            contextMenu.Items.Add(menuItemPartia);
            contextMenu.Items.Add(new ToolStripSeparator());

            // Opcja 3: Porównaj z innymi dostawcami
            var menuItemCompare = new ToolStripMenuItem("🔍 Porównaj z innymi");
            menuItemCompare.Click += (s, e) => ShowComparison();
            contextMenu.Items.Add(menuItemCompare);

            dataGridView1.ContextMenuStrip = contextMenu;
        }

        private void ConfigureDataGridView()
        {
            dataGridView1.SuspendLayout();

            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.BorderStyle = BorderStyle.None;
            dataGridView1.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
            dataGridView1.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridView1.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215);
            dataGridView1.DefaultCellStyle.SelectionForeColor = Color.White;
            dataGridView1.BackgroundColor = Color.White;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.EnableHeadersVisualStyles = false;

            dataGridView1.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 73, 94);
            dataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridView1.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridView1.ColumnHeadersHeight = 35;

            dataGridView1.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AllowUserToResizeRows = false;
            dataGridView1.ReadOnly = true;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = false;

            // Dodaj obsługę podwójnego kliknięcia
            dataGridView1.CellDoubleClick += DataGridView1_CellDoubleClick;

            dataGridView1.ResumeLayout();
        }

        private void DataGridView1_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                // Opcja A: Otwórz okno z raportami dla wybranego dostawcy
                ShowReportForSelectedSupplier();

                // Opcja B: Otwórz szczegóły w nowym oknie
                // ShowDetailedView();
            }
        }

        private void ShowReportForSelectedSupplier()
        {
            if (dataGridView1.CurrentRow == null) return;

            var dostawca = dataGridView1.CurrentRow.Cells["Dostawca"]?.Value?.ToString();
            if (string.IsNullOrEmpty(dostawca)) return;

            // Opcja 1: Otwórz RaportyDostawcy z pre-selected dostawcą
            var raportyForm = new RaportyDostawcy();
            raportyForm.SetDostawca(dostawca); // Musisz dodać tę metodę do RaportyDostawcy
            raportyForm.ShowDialog();
        }

        private void ShowBatchAnalysis()
        {
            if (dataGridView1.CurrentRow == null) return;

            var partia = dataGridView1.CurrentRow.Cells["Partia"]?.Value?.ToString();
            var dostawca = dataGridView1.CurrentRow.Cells["Dostawca"]?.Value?.ToString();

            // Otwórz dedykowane okno analizy partii
            var analysisForm = new BatchAnalysisForm(partia, dostawca, _dataSource);
            analysisForm.ShowDialog();
        }

        private void ShowComparison()
        {
            if (dataGridView1.CurrentRow == null) return;

            var currentDostawca = dataGridView1.CurrentRow.Cells["Dostawca"]?.Value?.ToString();

            // Otwórz okno porównawcze
            var comparisonForm = new SupplierComparisonForm(_dataSource, currentDostawca);
            comparisonForm.ShowDialog();
        }

        // Dodaj przycisk Raporty do górnego panelu
        private void AddReportsButton()
        {
            var btnReports = new Button
            {
                Text = "📊 Raporty",
                Location = new Point(770, 15),
                Size = new Size(95, 30),
                BackColor = Color.FromArgb(155, 89, 182),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnReports.FlatAppearance.BorderSize = 0;
            btnReports.Click += (s, e) =>
            {
                var raportyForm = new RaportyDostawcy();
                raportyForm.Show(); // Lub ShowDialog() dla modalnego
            };

            // Dodaj do panelTop (musisz mieć referencję do panelTop)
            // panelTop.Controls.Add(btnReports);
        }

        private void CreateColumns()
        {
            dataGridView1.Columns.Clear();

            var colData = new DataGridViewTextBoxColumn
            {
                Name = "Data",
                HeaderText = "Data",
                DataPropertyName = "Data",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd.MM.yyyy" }
            };

            var colPartia = new DataGridViewTextBoxColumn
            {
                Name = "Partia",
                HeaderText = "Partia",
                DataPropertyName = "Partia",
                Width = 80
            };

            var colDostawca = new DataGridViewTextBoxColumn
            {
                Name = "Dostawca",
                HeaderText = "Dostawca",
                DataPropertyName = "Dostawca",
                Width = 150,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };

            var colRoznicaDni = new DataGridViewTextBoxColumn
            {
                Name = "RoznicaDni",
                HeaderText = "Różnica dni",
                DataPropertyName = "RoznicaDni",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "# 'dni'",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            };

            var colWagaDek = new DataGridViewTextBoxColumn
            {
                Name = "WagaDek",
                HeaderText = "Waga Dek",
                DataPropertyName = "WagaDek",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "#,##0.00 'kg'",
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    ForeColor = Color.FromArgb(52, 73, 94)
                }
            };

            var colSredniaZywy = new DataGridViewTextBoxColumn
            {
                Name = "SredniaZywy",
                HeaderText = "Śr. żywy",
                DataPropertyName = "SredniaZywy",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "#,##0.00 'kg'",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            };

            var colRoznica = new DataGridViewTextBoxColumn
            {
                Name = "roznica",
                HeaderText = "Różnica",
                DataPropertyName = "roznica",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "+#,##0.00;-#,##0.00;0.00",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            };

            // Dodaj przycisk akcji do każdego wiersza
            var colAction = new DataGridViewButtonColumn
            {
                Name = "Action",
                HeaderText = "Akcje",
                Text = "📊",
                UseColumnTextForButtonValue = true,
                Width = 50
            };

            dataGridView1.Columns.AddRange(new DataGridViewColumn[]
            {
                colData, colPartia, colDostawca, colRoznicaDni,
                colWagaDek, colSredniaZywy, colRoznica, colAction
            });

            // Obsługa kliknięcia przycisku akcji
            dataGridView1.CellClick += (s, e) =>
            {
                if (e.ColumnIndex == dataGridView1.Columns["Action"].Index && e.RowIndex >= 0)
                {
                    ShowQuickReport(e.RowIndex);
                }
            };
        }

        private void ShowQuickReport(int rowIndex)
        {
            var row = dataGridView1.Rows[rowIndex];
            var dostawca = row.Cells["Dostawca"]?.Value?.ToString();
            var partia = row.Cells["Partia"]?.Value?.ToString();

            // Pokaż szybki raport w popup
            var quickReport = new QuickReportDialog(dostawca, partia, _dataSource);
            quickReport.ShowDialog();
        }

        private async Task LoadDataAsync()
        {
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                ShowLoadingIndicator(true);

                _dataSource = await Task.Run(() => GetDataFromDatabase());

                if (!IsDisposed)
                {
                    dataGridView1.SuspendLayout();
                    CreateColumns();
                    dataGridView1.DataSource = _dataSource;
                    ColorizeRows();
                    dataGridView1.ResumeLayout();

                    // Aktualizuj pasek statusu
                    UpdateStatusBar();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}",
                               "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isLoading = false;
                ShowLoadingIndicator(false);
            }
        }

        private DataTable GetDataFromDatabase()
        {
            const string query = @"
                SELECT 
                    k.CreateData AS Data,
                    k.P1 AS Partia,
                    Partia.CustomerName AS Dostawca,
                    DATEDIFF(day, wk.DataWstawienia, hd.DataOdbioru) AS RoznicaDni,
                    hd.WagaDek AS WagaDek,
                    CONVERT(decimal(18, 2), (15.0 / CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2))) * 1.22) AS SredniaZywy,
                    CONVERT(decimal(18, 2), ((15.0 / CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2))) * 1.22) - hd.WagaDek) AS roznica,
                    AVG(k.QntInCont) AS Srednia,
                    CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2)) AS SredniaDokładna
                FROM 
                    [LibraNet].[dbo].[In0E] k WITH (NOLOCK)
                JOIN 
                    [LibraNet].[dbo].[PartiaDostawca] Partia WITH (NOLOCK) ON k.P1 = Partia.Partia
                LEFT JOIN 
                    [LibraNet].[dbo].[HarmonogramDostaw] hd WITH (NOLOCK) ON k.CreateData = hd.DataOdbioru AND Partia.CustomerName = hd.Dostawca
                LEFT JOIN 
                    [LibraNet].[dbo].[WstawieniaKurczakow] wk WITH (NOLOCK) ON hd.LpW = wk.Lp
                WHERE 
                    k.ArticleID = 40 
                    AND k.QntInCont > 4
                GROUP BY 
                    k.CreateData, 
                    k.P1, 
                    Partia.CustomerName, 
                    hd.WagaDek,
                    wk.DataWstawienia,
                    hd.DataOdbioru
                ORDER BY 
                    k.P1 DESC, 
                    k.CreateData DESC";

            using (var connection = new SqlConnection(CONNECTION_STRING))
            using (var command = new SqlCommand(query, connection))
            {
                command.CommandTimeout = 60;

                using (var adapter = new SqlDataAdapter(command))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        private void ColorizeRows()
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.Cells["roznica"].Value != null && row.Cells["roznica"].Value != DBNull.Value)
                {
                    decimal roznica = Convert.ToDecimal(row.Cells["roznica"].Value);

                    if (roznica < 0)
                    {
                        row.Cells["roznica"].Style.ForeColor = Color.FromArgb(231, 76, 60);
                        row.Cells["roznica"].Style.Font = new Font(dataGridView1.Font, FontStyle.Bold);
                    }
                    else if (roznica > 0)
                    {
                        row.Cells["roznica"].Style.ForeColor = Color.FromArgb(39, 174, 96);
                        row.Cells["roznica"].Style.Font = new Font(dataGridView1.Font, FontStyle.Bold);
                    }
                }

                // Dodaj tooltip z dodatkowymi informacjami
                if (row.Cells["Dostawca"].Value != null)
                {
                    row.Cells["Dostawca"].ToolTipText = "Kliknij dwukrotnie aby zobaczyć raporty";
                }
            }
        }

        public void SetTextBoxValue()
        {
            if (!string.IsNullOrEmpty(TextBoxValue))
            {
                textBox1.Text = TextBoxValue;
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void SearchTimer_Tick(object? sender, EventArgs e)
        {
            _searchTimer.Stop();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_dataSource == null) return;

            string filterText = textBox1.Text.Trim();

            if (string.IsNullOrEmpty(filterText))
            {
                _dataSource.DefaultView.RowFilter = string.Empty;
            }
            else
            {
                filterText = filterText.Replace("'", "''");
                _dataSource.DefaultView.RowFilter = $"Dostawca LIKE '%{filterText}%' OR Partia LIKE '%{filterText}%'";
            }

            ColorizeRows();
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            if (_dataSource != null)
            {
                int visibleRows = _dataSource.DefaultView.Count;
                int totalRows = _dataSource.Rows.Count;

                this.Text = $"Wagi - System Monitorowania ({visibleRows} z {totalRows} rekordów)";

                // Jeśli masz StatusStrip
                if (statusStrip1 != null && toolStripStatusLabel1 != null)
                {
                    toolStripStatusLabel1.Text = $"Wyświetlono: {visibleRows} z {totalRows} rekordów";
                }
            }
        }

        private void ShowLoadingIndicator(bool show)
        {
            if (show)
            {
                this.Cursor = Cursors.WaitCursor;
                dataGridView1.Enabled = false;
                textBox1.Enabled = false;

                if (toolStripProgressBar1 != null)
                {
                    toolStripProgressBar1.Visible = true;
                    toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
                }
            }
            else
            {
                this.Cursor = Cursors.Default;
                dataGridView1.Enabled = true;
                textBox1.Enabled = true;

                if (toolStripProgressBar1 != null)
                {
                    toolStripProgressBar1.Visible = false;
                }
            }
        }

        public async Task RefreshDataAsync()
        {
            await LoadDataAsync();
        }

        private void ExportToExcel()
        {
            MessageBox.Show("Funkcja eksportu wymaga dodania biblioteki EPPlus lub ClosedXML",
                           "Eksport", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void btnRefresh_Click(object? sender, EventArgs e)
        {
            await RefreshDataAsync();
        }

        private void btnExport_Click(object? sender, EventArgs e)
        {
            ExportToExcel();
        }

        private void btnReports_Click(object? sender, EventArgs e)
        {
            // Otwórz główne okno raportów
            var raportyForm = new RaportyDostawcy();
            raportyForm.Show();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _searchTimer?.Dispose();
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource?.Dispose();
            base.OnFormClosed(e);
        }
    }

    // Klasa pomocnicza dla szybkich raportów
    public class QuickReportDialog : Form
    {
        public QuickReportDialog(string? dostawca, string? partia, DataTable? data)
        {
            this.Text = $"Szybki raport - {dostawca} / {partia}";
            this.Size = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterParent;

            var textBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 10F)
            };

            // Generuj prosty raport tekstowy
            var report = GenerateQuickReport(dostawca, partia, data);
            textBox.Text = report;

            this.Controls.Add(textBox);

            // Dodaj przycisk zamknij
            var btnClose = new Button
            {
                Text = "Zamknij",
                Dock = DockStyle.Bottom,
                Height = 30
            };
            btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(btnClose);
        }

        private string GenerateQuickReport(string? dostawca, string? partia, DataTable? data)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"RAPORT SZYBKI");
            sb.AppendLine($"{"".PadRight(50, '=')}");
            sb.AppendLine($"Dostawca: {dostawca}");
            sb.AppendLine($"Partia: {partia}");
            sb.AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();

            if (data != null && !string.IsNullOrEmpty(dostawca))
            {
                var dostawcaData = data.AsEnumerable()
                    .Where(r => r.Field<string>("Dostawca") == dostawca);

                if (dostawcaData.Any())
                {
                    var avgWaga = dostawcaData
                        .Where(r => r["WagaDek"] != DBNull.Value)
                        .Select(r => Convert.ToDecimal(r["WagaDek"]))
                        .DefaultIfEmpty(0)
                        .Average();

                    var avgRoznica = dostawcaData
                        .Where(r => r["roznica"] != DBNull.Value)
                        .Select(r => Convert.ToDecimal(r["roznica"]))
                        .DefaultIfEmpty(0)
                        .Average();

                    sb.AppendLine($"STATYSTYKI:");
                    sb.AppendLine($"  Średnia waga: {avgWaga:F2} kg");
                    sb.AppendLine($"  Średnia różnica: {avgRoznica:F2} kg");
                    sb.AppendLine($"  Liczba dostaw: {dostawcaData.Count()}");
                }
            }

            return sb.ToString();
        }
    }

    // Klasa dla analizy partii
    public class BatchAnalysisForm : Form
    {
        public BatchAnalysisForm(string? partia, string? dostawca, DataTable? data)
        {
            this.Text = $"Analiza partii - {partia}";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;

            // Tutaj dodaj szczegółową analizę partii
            // Możesz użyć wykresów, tabel, etc.
        }
    }

    // Klasa dla porównania dostawców
    public class SupplierComparisonForm : Form
    {
        public SupplierComparisonForm(DataTable? data, string? currentSupplier)
        {
            this.Text = $"Porównanie dostawców";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterParent;

            // Tutaj dodaj porównanie dostawców
            // Użyj wykresów porównawczych
        }
    }
}