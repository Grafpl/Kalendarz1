using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        // Connection string bezpośrednio w kodzie
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
            
            // Connection string
            connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True;Connection Timeout=30";
            
            InitializeBindingSources();
            InitializeEventHandlers();
            SetupDataGridViewStyles();
            
            // Ustaw domyślne wartości
            if (comboBoxFiltr != null)
                comboBoxFiltr.SelectedIndex = 0;
            
            // Inicjalizacja ToolTip
            if (toolTip1 == null)
                toolTip1 = new System.Windows.Forms.ToolTip();
            
            toolTip1.InitialDelay = 500;
            toolTip1.ShowAlways = true;
            
            // Automatyczne odświeżenie przy starcie
            this.Load += async (s, e) => await RefreshDataAsync();
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
            dataGridView1.CellFormatting += DataGridView1_CellFormatting;
            dataGridView1.DataError += DataGridView_DataError;
            dataGridView2.DataError += DataGridView_DataError;
            dataGridView2.RowPrePaint += DataGridView2_RowPrePaint;
            dataGridView2.CellFormatting += DataGridView2_CellFormatting;
            
            textBox1.TextChanged += TextBox1_TextChanged;
            showAllCheckBox.CheckedChanged += ShowAllCheckBox_CheckedChanged;
            
            // Obsługa sortowania
            dataGridView1.ColumnHeaderMouseClick += DataGridView_ColumnHeaderMouseClick;
            dataGridView2.ColumnHeaderMouseClick += DataGridView_ColumnHeaderMouseClick;
            
            // Obsługa zamykania formularza
            FormClosing += Platnosci_FormClosing;
        }

        private void SetupDataGridViewStyles()
        {
            // Styl dla obu DataGridView
            ConfigureDataGridView(dataGridView1);
            ConfigureDataGridView(dataGridView2);
        }

        private void ConfigureDataGridView(DataGridView dgv)
        {
            // Podstawowe ustawienia
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
            
            // Styl nagłówków
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(37, 99, 235);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(5);
            dgv.ColumnHeadersHeight = 40;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            
            // Styl wierszy
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f);
            dgv.DefaultCellStyle.Padding = new Padding(5, 3, 5, 3);
            dgv.RowTemplate.Height = 35;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(147, 197, 253);
            dgv.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 58, 138);
            
            // Double buffering dla płynności
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.SetProperty,
                null, dgv, new object[] { true });
        }

        private async void refreshButton_Click(object sender, EventArgs e)
        {
            await RefreshDataAsync();
        }

        private async Task RefreshDataAsync()
        {
            if (isLoading) return;
            
            // Anuluj poprzednie operacje jeśli są w toku
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                isLoading = true;
                ShowLoadingState(true);
                refreshButton.Enabled = false;
                
                // Równoległe ładowanie danych
                var task1 = LoadHodowcyDataAsync(cancellationTokenSource.Token);
                var task2 = LoadUbojniaDataAsync(cancellationTokenSource.Token);
                
                await Task.WhenAll(task1, task2);
                
                // Zastosuj formatowanie i filtry
                FormatujKolumny();
                UkryjKolumny();
                ObliczISumujDoZaplacenia();
                
                // Aktualizuj status
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

        private async Task LoadHodowcyDataAsync(CancellationToken cancellationToken)
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
                        WHEN (DATEDIFF(day, DK.data, PN.Termin) - DATEDIFF(day, DK.data, GETDATE())) <= 7 THEN 'Zbliżający się termin'
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

            await LoadDataAsync(query, dataTable1, bindingSource1, cancellationToken);
        }

        private async Task LoadUbojniaDataAsync(CancellationToken cancellationToken)
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
                    CASE 
                        WHEN (DATEDIFF(day, DK.data, PN.Termin) - DATEDIFF(day, DK.data, GETDATE())) <= -14 THEN 'Krytycznie przeterminowane'
                        WHEN (DATEDIFF(day, DK.data, PN.Termin) - DATEDIFF(day, DK.data, GETDATE())) <= -7 THEN 'Bardzo przeterminowane'
                        WHEN (DATEDIFF(day, DK.data, PN.Termin) - DATEDIFF(day, DK.data, GETDATE())) <= 0 THEN 'Przeterminowane'
                        ELSE 'W terminie'
                    END AS StatusPlatnosci
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

            await LoadDataAsync(query, dataTable2, bindingSource2, cancellationToken);
        }

        private async Task LoadDataAsync(string query, DataTable targetTable, BindingSource targetBinding, CancellationToken cancellationToken)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 60;
                    
                    await connection.OpenAsync(cancellationToken);
                    
                    using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        var newTable = new DataTable();
                        newTable.Load(reader);
                        
                        // Aktualizuj DataTable w głównym wątku
                        if (!cancellationToken.IsCancellationRequested)
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
                }
            }
        }

        private void DataGridView1_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            
            DataGridViewRow row = dataGridView1.Rows[e.RowIndex];
            
            if (row.Cells["Roznica"].Value != null && int.TryParse(row.Cells["Roznica"].Value.ToString(), out int roznica))
            {
                if (roznica <= -7)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(254, 226, 226);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(153, 27, 27);
                }
                else if (roznica <= 0)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(146, 64, 14);
                }
                else if (roznica <= 7)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(254, 249, 195);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(133, 77, 14);
                }
                else
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(220, 252, 231);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(22, 101, 52);
                }
            }
        }
        
        private void DataGridView2_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            
            DataGridViewRow row = dataGridView2.Rows[e.RowIndex];
            
            if (row.Cells["Roznica"].Value != null && int.TryParse(row.Cells["Roznica"].Value.ToString(), out int roznica))
            {
                if (roznica <= -14)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(254, 202, 202);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(127, 29, 29);
                }
                else if (roznica <= -7)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(254, 226, 226);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(153, 27, 27);
                }
                else if (roznica <= 0)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(146, 64, 14);
                }
                else
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(220, 252, 231);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(22, 101, 52);
                }
            }
        }

        private void DataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            FormatCell(sender, e);
        }
        
        private void DataGridView2_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            FormatCell(sender, e);
        }
        
        private void FormatCell(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            
            var dgv = (DataGridView)sender;
            
            // Formatowanie dat
            if (dgv.Columns[e.ColumnIndex].Name.Contains("Data") && e.Value != null)
            {
                if (DateTime.TryParse(e.Value.ToString(), out DateTime date))
                {
                    e.Value = date.ToString("yyyy-MM-dd");
                    e.FormattingApplied = true;
                }
            }
            
            // Podświetlenie ujemnych wartości
            if (dgv.Columns[e.ColumnIndex].Name == "DoZaplacenia" && e.Value != null)
            {
                if (decimal.TryParse(e.Value.ToString(), out decimal value) && value < 0)
                {
                    e.CellStyle.ForeColor = Color.Red;
                    e.CellStyle.Font = new Font(dgv.Font, FontStyle.Bold);
                }
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
                // Formatowanie kolumn kwotowych
                if (column.Name == "Kwota" || column.Name == "Rozliczone" || column.Name == "DoZaplacenia")
                {
                    column.DefaultCellStyle.Format = "N2";
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    
                    if (column.Name == "DoZaplacenia")
                    {
                        column.DefaultCellStyle.Font = new Font(dgv.Font, FontStyle.Bold);
                    }
                }
                
                // Formatowanie dat
                if (column.Name.Contains("Data") || column.Name.Contains("Termin"))
                {
                    column.DefaultCellStyle.Format = "dd.MM.yyyy";
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
                
                // Formatowanie liczb
                if (column.Name == "Roznica" || column.Name == "Obecny" || column.Name == "Termin")
                {
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
                
                // Auto-size dla lepszej widoczności
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            }
        }

        private void TextBox1_TextChanged(object sender, EventArgs e)
        {
            string filterText = textBox1.Text.Trim();
            var activeGrid = tabControl1.SelectedIndex == 0 ? bindingSource1 : bindingSource2;
            
            if (string.IsNullOrEmpty(filterText))
            {
                activeGrid.RemoveFilter();
            }
            else
            {
                string columnName = tabControl1.SelectedIndex == 0 ? "Hodowca" : "Klient";
                activeGrid.Filter = $"{columnName} LIKE '%{filterText}%'";
            }
            
            ObliczISumujDoZaplacenia();
            UpdateStatusBar($"Znaleziono rekordów: {(tabControl1.SelectedIndex == 0 ? dataGridView1 : dataGridView2).RowCount}");
        }

        private void UkryjKolumny()
        {
            if (!showAllCheckBox.Checked)
            {
                // Pokaż tylko najważniejsze kolumny dla hodowców
                string[] visibleColumns1 = { "Hodowca", "DoZaplacenia", "Roznica", "Status", "DataOdbioru" };
                foreach (DataGridViewColumn column in dataGridView1.Columns)
                {
                    column.Visible = visibleColumns1.Contains(column.Name);
                }
                
                // Pokaż tylko najważniejsze kolumny dla ubojni
                string[] visibleColumns2 = { "Klient", "DoZaplacenia", "Roznica", "StatusPlatnosci", "DataSprzedazy" };
                foreach (DataGridViewColumn column in dataGridView2.Columns)
                {
                    column.Visible = visibleColumns2.Contains(column.Name);
                }
            }
            else
            {
                // Pokaż wszystkie kolumny
                foreach (DataGridViewColumn column in dataGridView1.Columns)
                {
                    column.Visible = true;
                }
                foreach (DataGridViewColumn column in dataGridView2.Columns)
                {
                    column.Visible = true;
                }
            }
        }

        private void ObliczISumujDoZaplacenia()
        {
            // Oblicz dla hodowców
            decimal sumaDoZaplaceniaHodowcy = 0;
            decimal sumaPrzeterminowaneHodowcy = 0;
            decimal sumaWTerminieHodowcy = 0;
            int liczbaPrzeterminowanychHodowcy = 0;
            int liczbaWTerminieHodowcy = 0;

            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.Cells["Roznica"].Value != null && 
                    int.TryParse(row.Cells["Roznica"].Value.ToString(), out int roznica))
                {
                    if (row.Cells["DoZaplacenia"].Value != null && 
                        decimal.TryParse(row.Cells["DoZaplacenia"].Value.ToString(), out decimal wartosc))
                    {
                        if (roznica <= 0)
                        {
                            sumaPrzeterminowaneHodowcy += wartosc;
                            liczbaPrzeterminowanychHodowcy++;
                        }
                        else
                        {
                            sumaWTerminieHodowcy += wartosc;
                            liczbaWTerminieHodowcy++;
                        }
                        
                        sumaDoZaplaceniaHodowcy += wartosc;
                    }
                }
            }

            // Oblicz dla ubojni
            decimal sumaUbojnia = 0;
            int przeterminowaneUbojnia = 0;
            
            foreach (DataGridViewRow row in dataGridView2.Rows)
            {
                if (row.Cells["DoZaplacenia"] != null && row.Cells["DoZaplacenia"].Value != null)
                {
                    if (decimal.TryParse(row.Cells["DoZaplacenia"].Value.ToString(), out decimal wartosc))
                    {
                        sumaUbojnia += wartosc;
                        
                        if (row.Cells["Roznica"] != null && row.Cells["Roznica"].Value != null)
                        {
                            if (int.TryParse(row.Cells["Roznica"].Value.ToString(), out int roznica) && roznica <= 0)
                            {
                                przeterminowaneUbojnia++;
                            }
                        }
                    }
                }
            }

            // Aktualizuj pola tekstowe w zależności od aktywnej karty
            if (tabControl1.SelectedIndex == 0) // Hodowcy
            {
                textBox2.Text = $"{sumaDoZaplaceniaHodowcy:N2} zł";
                if (textBoxPrzeterminowane != null)
                    textBoxPrzeterminowane.Text = $"{sumaPrzeterminowaneHodowcy:N2} zł ({liczbaPrzeterminowanychHodowcy})";
                if (textBoxWTerminie != null)
                    textBoxWTerminie.Text = $"{sumaWTerminieHodowcy:N2} zł ({liczbaWTerminieHodowcy})";
            }
            else // Ubojnia
            {
                textBox2.Text = $"{sumaUbojnia:N2} zł";
                if (textBoxPrzeterminowane != null)
                    textBoxPrzeterminowane.Text = $"{przeterminowaneUbojnia} faktur";
                if (textBoxWTerminie != null)
                    textBoxWTerminie.Text = $"{dataGridView2.RowCount - przeterminowaneUbojnia} faktur";
            }
            
            // Zapisz sumę ubojni
            if (textBoxSumaUbojnia != null)
            {
                textBoxSumaUbojnia.Text = $"{sumaUbojnia:N2} zł";
                toolTip1.SetToolTip(textBoxSumaUbojnia, $"Przeterminowane: {przeterminowaneUbojnia} faktur");
            }
            
            // Dodaj dodatkowe informacje w tooltip
            var info = tabControl1.SelectedIndex == 0 
                ? $"Przeterminowane: {sumaPrzeterminowaneHodowcy:N2} zł ({liczbaPrzeterminowanychHodowcy} faktur)\n" +
                  $"W terminie: {sumaWTerminieHodowcy:N2} zł ({liczbaWTerminieHodowcy} faktur)\n" +
                  $"RAZEM: {sumaDoZaplaceniaHodowcy:N2} zł"
                : $"Do otrzymania: {sumaUbojnia:N2} zł\n" +
                  $"Przeterminowane: {przeterminowaneUbojnia} faktur";
            
            toolTip1.SetToolTip(textBox2, info);
        }

        private void ShowAllCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UkryjKolumny();
        }

        private async void button1_Click_1(object sender, EventArgs e)
        {
            try
            {
                var report = GenerateDetailedReport();
                Clipboard.SetText(report);
                
                MessageBox.Show("Raport został skopiowany do schowka.", 
                    "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas generowania raportu: {ex.Message}", 
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GenerateDetailedReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== RAPORT PŁATNOŚCI - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            sb.AppendLine();
            
            // Podsumowanie dla hodowców
            sb.AppendLine("HODOWCY:");
            decimal sumaHodowcy = 0;
            int przeterminowaneHodowcy = 0;
            
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.Cells["DoZaplacenia"].Value != null)
                {
                    decimal kwota = Convert.ToDecimal(row.Cells["DoZaplacenia"].Value);
                    sumaHodowcy += kwota;
                    
                    if (row.Cells["Roznica"].Value != null && 
                        Convert.ToInt32(row.Cells["Roznica"].Value) <= 0)
                    {
                        przeterminowaneHodowcy++;
                    }
                }
            }
            
            sb.AppendLine($"Łączna kwota: {sumaHodowcy:N2} zł");
            sb.AppendLine($"Liczba przeterminowanych: {przeterminowaneHodowcy}");
            sb.AppendLine();
            
            // Podsumowanie dla ubojni
            sb.AppendLine("UBOJNIA:");
            decimal sumaUbojnia = 0;
            int przeterminowaneUbojnia = 0;
            
            foreach (DataGridViewRow row in dataGridView2.Rows)
            {
                if (row.Cells["DoZaplacenia"].Value != null)
                {
                    decimal kwota = Convert.ToDecimal(row.Cells["DoZaplacenia"].Value);
                    sumaUbojnia += kwota;
                    
                    if (row.Cells["Roznica"].Value != null && 
                        Convert.ToInt32(row.Cells["Roznica"].Value) <= 0)
                    {
                        przeterminowaneUbojnia++;
                    }
                }
            }
            
            sb.AppendLine($"Łączna kwota: {sumaUbojnia:N2} zł");
            sb.AppendLine($"Liczba przeterminowanych: {przeterminowaneUbojnia}");
            sb.AppendLine();
            
            // Bilans
            sb.AppendLine("BILANS:");
            sb.AppendLine($"Do zapłaty hodowcom: {sumaHodowcy:N2} zł");
            sb.AppendLine($"Do otrzymania od ubojni: {sumaUbojnia:N2} zł");
            sb.AppendLine($"Różnica: {(sumaUbojnia - sumaHodowcy):N2} zł");
            
            return sb.ToString();
        }

        private void DataGridView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var dgv = (DataGridView)sender;
            var column = dgv.Columns[e.ColumnIndex];
            
            // Implementacja sortowania
            if (dgv.DataSource is BindingSource bs && bs.DataSource is DataTable dt)
            {
                string sortColumn = column.DataPropertyName;
                if (string.IsNullOrEmpty(sortColumn))
                    sortColumn = column.Name;
                    
                string currentSort = dt.DefaultView.Sort;
                
                if (currentSort.StartsWith(sortColumn + " ASC"))
                {
                    dt.DefaultView.Sort = sortColumn + " DESC";
                }
                else
                {
                    dt.DefaultView.Sort = sortColumn + " ASC";
                }
            }
        }

        private void DataGridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            // Obsługa błędów danych
            e.ThrowException = false;
            LogError(new Exception($"DataGridView error at row {e.RowIndex}, column {e.ColumnIndex}"));
        }

        private void Platnosci_FormClosing(object sender, FormClosingEventArgs e)
        {
            cancellationTokenSource?.Cancel();
        }

        private void ShowLoadingState(bool show)
        {
            if (show)
            {
                this.Cursor = Cursors.WaitCursor;
                if (progressBar1 != null)
                {
                    progressBar1.Visible = true;
                    progressBar1.Style = ProgressBarStyle.Marquee;
                }
            }
            else
            {
                this.Cursor = Cursors.Default;
                if (progressBar1 != null)
                {
                    progressBar1.Visible = false;
                }
            }
        }

        private void UpdateStatusBar(string message)
        {
            if (lblStatus != null)
            {
                lblStatus.Text = message;
            }
        }

        private void LogError(Exception ex)
        {
            try
            {
                string logPath = "errors.log";
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.Message}\n{ex.StackTrace}\n\n";
                System.IO.File.AppendAllText(logPath, logMessage);
            }
            catch
            {
                // Ignoruj błędy logowania
            }
        }

        private void buttonExportExcel_Click(object sender, EventArgs e)
        {
            ExportToExcel();
        }

        private void comboBoxFiltr_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplyStatusFilter();
        }
        
        private void ApplyStatusFilter()
        {
            if (comboBoxFiltr == null) return;
            
            var activeBinding = tabControl1.SelectedIndex == 0 ? bindingSource1 : bindingSource2;
            if (activeBinding == null) return;
            
            string selectedFilter = comboBoxFiltr.SelectedItem?.ToString() ?? "Wszystkie";
            
            switch (selectedFilter)
            {
                case "Przeterminowane":
                    activeBinding.Filter = "Roznica <= 0";
                    break;
                case "Do 7 dni":
                    activeBinding.Filter = "Roznica > 0 AND Roznica <= 7";
                    break;
                case "W terminie":
                    activeBinding.Filter = "Roznica > 7";
                    break;
                default:
                    activeBinding.RemoveFilter();
                    break;
            }
            
            ObliczISumujDoZaplacenia();
        }
        
        private void ExportToExcel()
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                saveDialog.FileName = $"Platnosci_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    using (var writer = new System.IO.StreamWriter(saveDialog.FileName, false, Encoding.UTF8))
                    {
                        // Nagłówki dla hodowców
                        writer.WriteLine("=== PŁATNOŚCI HODOWCÓW ===");
                        writer.WriteLine("Hodowca;Numer Dokumentu;Kwota;Rozliczone;Do Zapłacenia;Data Odbioru;Termin;Różnica;Status");
                        
                        // Dane hodowców
                        foreach (DataGridViewRow row in dataGridView1.Rows)
                        {
                            var values = new List<string>();
                            foreach (DataGridViewCell cell in row.Cells)
                            {
                                values.Add(cell.Value?.ToString()?.Replace(";", ",") ?? "");
                            }
                            writer.WriteLine(string.Join(";", values));
                        }
                        
                        writer.WriteLine();
                        writer.WriteLine("=== PŁATNOŚCI UBOJNI ===");
                        writer.WriteLine("Klient;Numer Faktury;Kwota;Rozliczone;Do Zapłacenia;Data Sprzedaży;Termin;Różnica;Status");
                        
                        // Dane ubojni
                        foreach (DataGridViewRow row in dataGridView2.Rows)
                        {
                            var values = new List<string>();
                            foreach (DataGridViewCell cell in row.Cells)
                            {
                                values.Add(cell.Value?.ToString()?.Replace(";", ",") ?? "");
                            }
                            writer.WriteLine(string.Join(";", values));
                        }
                        
                        // Podsumowanie
                        writer.WriteLine();
                        writer.WriteLine("=== PODSUMOWANIE ===");
                        writer.WriteLine($"Data raportu: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    }
                    
                    MessageBox.Show($"Dane zostały wyeksportowane do pliku:\n{saveDialog.FileName}", 
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Przelicz sumy dla aktywnej karty
            ObliczISumujDoZaplacenia();
            
            // Zaktualizuj etykietę wyszukiwania
            if (label6 != null)
            {
                label6.Text = tabControl1.SelectedIndex == 0 ? "Szukaj hodowcy:" : "Szukaj klienta:";
            }
            
            // Wyczyść pole wyszukiwania
            textBox1.Text = "";
        }
    }
}