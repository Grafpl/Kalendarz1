using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Kalendarz1.AnkietyHodowcow
{
    public partial class HistoriaHodowcyWindowPremium : Window
    {
        private readonly string _connectionString;
        private readonly string _dostawca;
        private DataTable _fullDataTable;
        private DataTable _filteredDataTable;

        public HistoriaHodowcyWindowPremium(string connectionString, string dostawca)
        {
            InitializeComponent();

            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _dostawca = dostawca ?? throw new ArgumentNullException(nameof(dostawca));

            Title = $"📊 Historia i Analityka - {dostawca}";
            DostawcaTextBlock.Text = $"Hodowca: {dostawca}";
            LastUpdateTextBlock.Text = $"Ostatnia aktualizacja: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            LoadLogo();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private void LoadLogo()
        {
            try
            {
                string[] possiblePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png"),
                    Path.Combine(Environment.CurrentDirectory, "logo.png"),
                    "logo.png"
                };

                string logoPath = possiblePaths.FirstOrDefault(File.Exists);
                if (logoPath != null)
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(logoPath, UriKind.RelativeOrAbsolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    LogoImage.Source = bitmap;
                }
            }
            catch { }
        }

        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string header = e.Column.Header.ToString();
            e.Column.Header = FormatColumnHeader(header);

            if (header == "Notatka")
                e.Column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            else if (header == "Dostawca")
                e.Column.Width = new DataGridLength(180);
            else
                e.Column.Width = DataGridLength.Auto;

            if (e.PropertyType == typeof(int) || e.PropertyType == typeof(int?))
            {
                if (header.StartsWith("Ocena"))
                {
                    var column = e.Column as DataGridTextColumn;
                    if (column != null) column.Binding.StringFormat = "{0} pkt.";
                }
            }
            else if (e.PropertyType == typeof(decimal) || e.PropertyType == typeof(decimal?))
            {
                if (header == "SredniaWiersza")
                {
                    var column = e.Column as DataGridTextColumn;
                    if (column != null) column.Binding.StringFormat = "{0:N1} pkt.";
                }
            }
            else if (e.PropertyType == typeof(DateTime) || e.PropertyType == typeof(DateTime?))
            {
                var column = e.Column as DataGridTextColumn;
                if (column != null)
                {
                    column.Binding.StringFormat = header == "DataDostawy" ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm";
                }
            }
        }

        private string FormatColumnHeader(string header)
        {
            return header switch
            {
                "DostawaLp" => "LP",
                "Dostawca" => "Hodowca",
                "Kto" => "Oceniający",
                "DataDostawy" => "Data Dostawy",
                "DataAnkiety" => "Data Oceny",
                "OcenaCena" => "💰 Cena",
                "OcenaTransport" => "🚚 Transport",
                "OcenaKomunikacja" => "💬 Komunikacja",
                "OcenaElastycznosc" => "🔄 Elastyczność",
                "OcenaStanPtakow" => "🐔 Stan Ptaków",
                "SredniaWiersza" => "⭐ Średnia",
                "Notatka" => "📝 Notatka",
                _ => header
            };
        }

        private async Task LoadDataAsync()
        {
            try
            {
                StatusTextBlock.Text = "⏳ Ładowanie...";

                const string sql = @"
SELECT
    f.DostawaLp,
    h.Dostawca,
    f.Kto,
    f.DataDostawy,
    f.DataAnkiety,
    f.OcenaCena,
    f.OcenaTransport,
    f.OcenaKomunikacja,
    f.OcenaElastycznosc,
    f.OcenaStanPtakow,
    CAST(ROUND(
        (
            COALESCE(CAST(f.OcenaCena AS DECIMAL(10,4)),0) +
            COALESCE(CAST(f.OcenaTransport AS DECIMAL(10,4)),0) +
            COALESCE(CAST(f.OcenaKomunikacja AS DECIMAL(10,4)),0) +
            COALESCE(CAST(f.OcenaElastycznosc AS DECIMAL(10,4)),0) +
            COALESCE(CAST(f.OcenaStanPtakow AS DECIMAL(10,4)),0)
        ) / NULLIF(
            (CASE WHEN f.OcenaCena IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN f.OcenaTransport IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN f.OcenaKomunikacja IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN f.OcenaElastycznosc IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN f.OcenaStanPtakow IS NOT NULL THEN 1 ELSE 0 END), 0
        ), 1) AS DECIMAL(10,1)) AS SredniaWiersza,
    f.Notatka
FROM dbo.DostawaFeedback f
INNER JOIN dbo.HarmonogramDostaw h ON h.Lp = f.DostawaLp
WHERE h.Dostawca = @dostawca
ORDER BY f.DataDostawy DESC, f.DataAnkiety DESC, f.DostawaLp DESC;";

                await Task.Run(() =>
                {
                    using var cn = new SqlConnection(_connectionString);
                    cn.Open();
                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@dostawca", _dostawca);
                    using var da = new SqlDataAdapter(cmd);
                    _fullDataTable = new DataTable();
                    da.Fill(_fullDataTable);
                });

                _filteredDataTable = _fullDataTable.Copy();
                ApplyFilters();
                
                StatusTextBlock.Text = $"✓ Załadowano pomyślnie";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "✗ Błąd ładowania";
            }
        }

        private void ApplyFilters()
        {
            if (_fullDataTable == null) return;

            var filteredRows = _fullDataTable.AsEnumerable();

            // Search filter
            string searchText = SearchTextBox.Text.Trim().ToLower();
            if (!string.IsNullOrEmpty(searchText))
            {
                filteredRows = filteredRows.Where(row =>
                    row["Dostawca"].ToString().ToLower().Contains(searchText) ||
                    row["Kto"].ToString().ToLower().Contains(searchText) ||
                    row["Notatka"].ToString().ToLower().Contains(searchText));
            }

            // Status filter
            if (StatusComboBox.SelectedIndex > 0)
            {
                filteredRows = StatusComboBox.SelectedIndex switch
                {
                    1 => filteredRows.Where(r => !r.IsNull("SredniaWiersza") && Convert.ToDecimal(r["SredniaWiersza"]) >= 4.5m),
                    2 => filteredRows.Where(r => !r.IsNull("SredniaWiersza") && Convert.ToDecimal(r["SredniaWiersza"]) >= 4.0m && Convert.ToDecimal(r["SredniaWiersza"]) < 4.5m),
                    3 => filteredRows.Where(r => !r.IsNull("SredniaWiersza") && Convert.ToDecimal(r["SredniaWiersza"]) < 4.0m),
                    _ => filteredRows
                };
            }

            // Period filter
            if (PeriodComboBox.SelectedIndex > 0)
            {
                DateTime cutoffDate = PeriodComboBox.SelectedIndex switch
                {
                    1 => DateTime.Now.AddMonths(-1),
                    2 => DateTime.Now.AddMonths(-3),
                    3 => DateTime.Now.AddMonths(-6),
                    4 => DateTime.Now.AddYears(-1),
                    _ => DateTime.MinValue
                };

                filteredRows = filteredRows.Where(r => !r.IsNull("DataDostawy") && Convert.ToDateTime(r["DataDostawy"]) >= cutoffDate);
            }

            // Create filtered table
            if (filteredRows.Any())
            {
                _filteredDataTable = filteredRows.CopyToDataTable();

                // Sort
                if (SortComboBox.SelectedIndex > 0)
                {
                    var sortedView = _filteredDataTable.DefaultView;
                    sortedView.Sort = SortComboBox.SelectedIndex switch
                    {
                        1 => "DataDostawy ASC",
                        2 => "SredniaWiersza DESC",
                        3 => "SredniaWiersza ASC",
                        _ => "DataDostawy DESC"
                    };
                    _filteredDataTable = sortedView.ToTable();
                }
            }
            else
            {
                _filteredDataTable = _fullDataTable.Clone();
            }

            HistoriaDataGrid.ItemsSource = _filteredDataTable.DefaultView;
            UpdateAnalytics();
            UpdateResultsInfo();
        }

        private void UpdateAnalytics()
        {
            if (_filteredDataTable == null || _filteredDataTable.Rows.Count == 0)
            {
                StatsLiczbaOcen.Text = "0";
                StatsSredniaCena.Text = "-";
                StatsSredniaTransport.Text = "-";
                StatsSredniaKomunikacja.Text = "-";
                StatsSredniaElastycznosc.Text = "-";
                StatsSredniaStanPtakow.Text = "-";
                StatsSredniaOgolna.Text = "-";
                return;
            }

            var rows = _filteredDataTable.AsEnumerable();
            StatsLiczbaOcen.Text = _filteredDataTable.Rows.Count.ToString();

            var avgCena = rows.Average(r => r.IsNull("OcenaCena") ? (double?)null : Convert.ToDouble(r["OcenaCena"]));
            var avgTransport = rows.Average(r => r.IsNull("OcenaTransport") ? (double?)null : Convert.ToDouble(r["OcenaTransport"]));
            var avgKomunikacja = rows.Average(r => r.IsNull("OcenaKomunikacja") ? (double?)null : Convert.ToDouble(r["OcenaKomunikacja"]));
            var avgElastycznosc = rows.Average(r => r.IsNull("OcenaElastycznosc") ? (double?)null : Convert.ToDouble(r["OcenaElastycznosc"]));
            var avgStanPtakow = rows.Average(r => r.IsNull("OcenaStanPtakow") ? (double?)null : Convert.ToDouble(r["OcenaStanPtakow"]));
            var avgOgolna = rows.Average(r => r.IsNull("SredniaWiersza") ? (double?)null : Convert.ToDouble(r["SredniaWiersza"]));

            StatsSredniaCena.Text = avgCena.HasValue ? $"{avgCena.Value:N2}" : "-";
            StatsSredniaTransport.Text = avgTransport.HasValue ? $"{avgTransport.Value:N2}" : "-";
            StatsSredniaKomunikacja.Text = avgKomunikacja.HasValue ? $"{avgKomunikacja.Value:N2}" : "-";
            StatsSredniaElastycznosc.Text = avgElastycznosc.HasValue ? $"{avgElastycznosc.Value:N2}" : "-";
            StatsSredniaStanPtakow.Text = avgStanPtakow.HasValue ? $"{avgStanPtakow.Value:N2}" : "-";
            StatsSredniaOgolna.Text = avgOgolna.HasValue ? $"{avgOgolna.Value:N2}" : "-";
        }

        private void UpdateResultsInfo()
        {
            if (_filteredDataTable == null || _fullDataTable == null) return;

            int filtered = _filteredDataTable.Rows.Count;
            int total = _fullDataTable.Rows.Count;

            ResultsInfoTextBlock.Text = filtered == total
                ? $"📊 Wyświetlono wszystkie {total} rekordów"
                : $"🔍 Znaleziono {filtered} z {total} rekordów ({(filtered * 100.0 / total):N1}%)";
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
        private void FilterChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();
        
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            StatusComboBox.SelectedIndex = 0;
            PeriodComboBox.SelectedIndex = 0;
            SortComboBox.SelectedIndex = 0;
            await LoadDataAsync();
        }

        private async void GenerateReportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "⏳ Generowanie raportu...";
                var reportWindow = new Top20ReportWindowEnhanced(_connectionString);
                await reportWindow.LoadDataAsync();
                reportWindow.Owner = this;
                reportWindow.ShowDialog();
                StatusTextBlock.Text = "✓ Raport wygenerowany";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "✗ Błąd raportu";
            }
        }

        private void GeneratePDFButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    DefaultExt = "pdf",
                    FileName = $"Historia_{_dostawca}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    StatusTextBlock.Text = "⏳ Generowanie PDF...";
                    GeneratePDF(saveDialog.FileName);
                    StatusTextBlock.Text = "✓ PDF wygenerowany";
                    MessageBox.Show($"PDF zapisany:\n{saveDialog.FileName}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "✗ Błąd PDF";
            }
        }

        private void GeneratePDF(string filePath)
        {
            Document document = new Document(PageSize.A4.Rotate(), 30, 30, 30, 30);
            
            try
            {
                PdfWriter.GetInstance(document, new FileStream(filePath, FileMode.Create));
                document.Open();

                // Fonts
                Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.BLACK);
                Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.BLACK);
                Font normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                Font smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, BaseColor.DARK_GRAY);

                // Title
                Paragraph title = new Paragraph($"Historia Hodowcy: {_dostawca}", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 10f;
                document.Add(title);

                // Date
                Paragraph dateP = new Paragraph($"Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm}", smallFont);
                dateP.Alignment = Element.ALIGN_CENTER;
                dateP.SpacingAfter = 20f;
                document.Add(dateP);

                // Statistics Section
                Paragraph statsHeader = new Paragraph("Statystyki", headerFont);
                statsHeader.SpacingBefore = 10f;
                statsHeader.SpacingAfter = 10f;
                document.Add(statsHeader);

                // Statistics Table
                PdfPTable statsTable = new PdfPTable(2);
                statsTable.WidthPercentage = 60;
                statsTable.HorizontalAlignment = Element.ALIGN_LEFT;
                statsTable.SpacingAfter = 20f;
                
                AddStatRow(statsTable, "Liczba ocen:", StatsLiczbaOcen.Text, normalFont);
                AddStatRow(statsTable, "Średnia Cena:", StatsSredniaCena.Text, normalFont);
                AddStatRow(statsTable, "Średnia Transport:", StatsSredniaTransport.Text, normalFont);
                AddStatRow(statsTable, "Średnia Komunikacja:", StatsSredniaKomunikacja.Text, normalFont);
                AddStatRow(statsTable, "Średnia Elastyczność:", StatsSredniaElastycznosc.Text, normalFont);
                AddStatRow(statsTable, "Średnia Stan Ptaków:", StatsSredniaStanPtakow.Text, normalFont);
                AddStatRow(statsTable, "Średnia Ogólna:", StatsSredniaOgolna.Text, normalFont);

                document.Add(statsTable);

                // Data Section
                Paragraph dataHeader = new Paragraph("Szczegółowe Dane", headerFont);
                dataHeader.SpacingBefore = 10f;
                dataHeader.SpacingAfter = 10f;
                document.Add(dataHeader);

                // Data Table
                if (_filteredDataTable != null && _filteredDataTable.Rows.Count > 0)
                {
                    int columnCount = _filteredDataTable.Columns.Count;
                    PdfPTable dataTable = new PdfPTable(columnCount);
                    dataTable.WidthPercentage = 100;
                    dataTable.HeaderRows = 1;

                    // Set column widths
                    float[] widths = new float[columnCount];
                    for (int i = 0; i < columnCount; i++)
                    {
                        widths[i] = _filteredDataTable.Columns[i].ColumnName == "Notatka" ? 3f : 1f;
                    }
                    dataTable.SetWidths(widths);

                    // Headers
                    Font headerCellFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, BaseColor.WHITE);
                    foreach (DataColumn column in _filteredDataTable.Columns)
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(FormatColumnHeader(column.ColumnName), headerCellFont));
                        cell.BackgroundColor = new BaseColor(102, 126, 234);
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.Padding = 5f;
                        dataTable.AddCell(cell);
                    }

                    // Data rows
                    Font dataCellFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, BaseColor.BLACK);
                    foreach (DataRow row in _filteredDataTable.Rows)
                    {
                        foreach (var item in row.ItemArray)
                        {
                            PdfPCell cell = new PdfPCell(new Phrase(item?.ToString() ?? "", dataCellFont));
                            cell.Padding = 4f;
                            cell.HorizontalAlignment = Element.ALIGN_LEFT;
                            dataTable.AddCell(cell);
                        }
                    }

                    document.Add(dataTable);
                }
            }
            finally
            {
                if (document.IsOpen())
                    document.Close();
            }
        }

        private void AddStatRow(PdfPTable table, string label, string value, Font font)
        {
            PdfPCell labelCell = new PdfPCell(new Phrase(label, font));
            labelCell.Border = Rectangle.NO_BORDER;
            labelCell.Padding = 5f;
            table.AddCell(labelCell);

            PdfPCell valueCell = new PdfPCell(new Phrase(value, font));
            valueCell.Border = Rectangle.NO_BORDER;
            valueCell.Padding = 5f;
            table.AddCell(valueCell);
        }
    }
}
