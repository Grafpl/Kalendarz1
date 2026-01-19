using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace Kalendarz1.AnkietyHodowcow
{
    public partial class Top20ReportWindowEnhanced : Window
    {
        private readonly string _connectionString;
        private List<Top20Item> _reportData;

        public Top20ReportWindowEnhanced(string connectionString)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            DateTextBlock.Text = $"Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            // Ładuj logo
            LoadLogo();
        }

        private void LoadLogo()
        {
            try
            {
                // Spróbuj znaleźć logo.png w różnych lokalizacjach
                string[] possiblePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png"),
                    Path.Combine(Environment.CurrentDirectory, "logo.png"),
                    Path.Combine(Directory.GetCurrentDirectory(), "logo.png"),
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
                else
                {
                    // Jeśli nie ma logo, ukryj kontener
                    // LogoImage.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                // Jeśli wystąpi błąd, po prostu nie pokazuj logo
            }
        }

        public async Task LoadDataAsync()
        {
            try
            {
                const string sql = @"
WITH DostawcaStats AS (
    SELECT
        h.Dostawca,
        COUNT(*) AS LiczbaOcen,
        SUM(
            COALESCE(CAST(f.OcenaCena AS DECIMAL(10,4)),0) +
            COALESCE(CAST(f.OcenaTransport AS DECIMAL(10,4)),0) +
            COALESCE(CAST(f.OcenaKomunikacja AS DECIMAL(10,4)),0) +
            COALESCE(CAST(f.OcenaElastycznosc AS DECIMAL(10,4)),0) +
            COALESCE(CAST(f.OcenaStanPtakow AS DECIMAL(10,4)),0)
        ) AS SumaPunktow,
        AVG(CAST(f.OcenaCena AS DECIMAL(10,4))) AS SredniaCena,
        AVG(CAST(f.OcenaTransport AS DECIMAL(10,4))) AS SredniaTransport,
        AVG(CAST(f.OcenaKomunikacja AS DECIMAL(10,4))) AS SredniaKomunikacja,
        AVG(CAST(f.OcenaElastycznosc AS DECIMAL(10,4))) AS SredniaElastycznosc,
        AVG(CAST(f.OcenaStanPtakow AS DECIMAL(10,4))) AS SredniaStanPtakow,
        AVG(
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
            )
        ) AS SredniaOgolna
    FROM dbo.DostawaFeedback f
    INNER JOIN dbo.HarmonogramDostaw h ON h.Lp = f.DostawaLp
    GROUP BY h.Dostawca
)
SELECT TOP 20
    ROW_NUMBER() OVER (ORDER BY SumaPunktow DESC, SredniaOgolna DESC) AS Miejsce,
    Dostawca,
    LiczbaOcen,
    CAST(ROUND(SumaPunktow, 1) AS DECIMAL(10,1)) AS SumaPunktow,
    CAST(ROUND(SredniaCena, 2) AS DECIMAL(10,2)) AS SredniaCena,
    CAST(ROUND(SredniaTransport, 2) AS DECIMAL(10,2)) AS SredniaTransport,
    CAST(ROUND(SredniaKomunikacja, 2) AS DECIMAL(10,2)) AS SredniaKomunikacja,
    CAST(ROUND(SredniaElastycznosc, 2) AS DECIMAL(10,2)) AS SredniaElastycznosc,
    CAST(ROUND(SredniaStanPtakow, 2) AS DECIMAL(10,2)) AS SredniaStanPtakow,
    CAST(ROUND(SredniaOgolna, 2) AS DECIMAL(10,2)) AS SredniaOgolna
FROM DostawcaStats
ORDER BY SumaPunktow DESC, SredniaOgolna DESC;";

                await Task.Run(() =>
                {
                    using var cn = new SqlConnection(_connectionString);
                    cn.Open();
                    using var cmd = new SqlCommand(sql, cn);
                    using var reader = cmd.ExecuteReader();

                    _reportData = new List<Top20Item>();
                    while (reader.Read())
                    {
                        _reportData.Add(new Top20Item
                        {
                            Miejsce = reader.GetInt64(0),
                            Dostawca = reader.GetString(1),
                            LiczbaOcen = reader.GetInt32(2),
                            SumaPunktow = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                            SredniaCena = reader.IsDBNull(4) ? (decimal?)null : reader.GetDecimal(4),
                            SredniaTransport = reader.IsDBNull(5) ? (decimal?)null : reader.GetDecimal(5),
                            SredniaKomunikacja = reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6),
                            SredniaElastycznosc = reader.IsDBNull(7) ? (decimal?)null : reader.GetDecimal(7),
                            SredniaStanPtakow = reader.IsDBNull(8) ? (decimal?)null : reader.GetDecimal(8),
                            SredniaOgolna = reader.IsDBNull(9) ? (decimal?)null : reader.GetDecimal(9)
                        });
                    }
                });

                Top20DataGrid.ItemsSource = _reportData;

                // Update summary statistics
                if (_reportData.Any())
                {
                    TotalCountTextBlock.Text = _reportData.Count.ToString();
                    AvgScoreTextBlock.Text = $"{_reportData.Average(x => x.SumaPunktow):N1}";
                    MaxScoreTextBlock.Text = $"{_reportData.Max(x => x.SumaPunktow):N1}";
                    MinScoreTextBlock.Text = $"{_reportData.Min(x => x.SumaPunktow):N1}";
                    TotalRatingsTextBlock.Text = _reportData.Sum(x => x.LiczbaOcen).ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych raportu:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    FlowDocument doc = CreatePrintDocument();

                    doc.PageHeight = printDialog.PrintableAreaHeight;
                    doc.PageWidth = printDialog.PrintableAreaWidth;
                    doc.PagePadding = new Thickness(40);
                    doc.ColumnWidth = printDialog.PrintableAreaWidth;

                    IDocumentPaginatorSource idpSource = doc;
                    printDialog.PrintDocument(idpSource.DocumentPaginator, "TOP 20 Hodowców - Raport");

                    MessageBox.Show("Raport został wysłany do drukarki.", "Drukowanie",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas drukowania:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FlowDocument CreatePrintDocument()
        {
            FlowDocument doc = new FlowDocument();

            // Logo (jeśli dostępne)
            if (LogoImage.Source != null)
            {
                BlockUIContainer logoContainer = new BlockUIContainer();
                Image printLogo = new Image
                {
                    Source = LogoImage.Source,
                    Width = 80,
                    Height = 80,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                logoContainer.Child = printLogo;
                doc.Blocks.Add(logoContainer);
            }

            // Title
            Paragraph title = new Paragraph(new Run("🏆 TOP 20 HODOWCÓW"))
            {
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 10, 0, 5)
            };
            doc.Blocks.Add(title);

            // Subtitle
            Paragraph subtitle = new Paragraph(new Run("Ranking według sumarycznej punktacji"))
            {
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 3)
            };
            doc.Blocks.Add(subtitle);

            // Date
            Paragraph date = new Paragraph(new Run($"Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm}"))
            {
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 15)
            };
            doc.Blocks.Add(date);

            // Summary statistics
            Paragraph stats = new Paragraph()
            {
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stats.Inlines.Add(new Run($"Hodowców: {TotalCountTextBlock.Text}  |  "));
            stats.Inlines.Add(new Run($"Średnia: {AvgScoreTextBlock.Text}  |  "));
            stats.Inlines.Add(new Run($"Max: {MaxScoreTextBlock.Text}  |  "));
            stats.Inlines.Add(new Run($"Min: {MinScoreTextBlock.Text}  |  "));
            stats.Inlines.Add(new Run($"Ocen: {TotalRatingsTextBlock.Text}"));
            doc.Blocks.Add(stats);

            // Table
            Table table = new Table
            {
                CellSpacing = 0,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1)
            };

            // Columns
            table.Columns.Add(new TableColumn { Width = new GridLength(40) });  // Miejsce
            table.Columns.Add(new TableColumn { Width = new GridLength(120) }); // Dostawca
            table.Columns.Add(new TableColumn { Width = new GridLength(50) });  // Liczba
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });  // Suma
            table.Columns.Add(new TableColumn { Width = new GridLength(50) });  // Cena
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });  // Transport
            table.Columns.Add(new TableColumn { Width = new GridLength(70) });  // Komunikacja
            table.Columns.Add(new TableColumn { Width = new GridLength(70) });  // Elastyczność
            table.Columns.Add(new TableColumn { Width = new GridLength(70) });  // Stan Ptaków
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });  // Średnia

            table.RowGroups.Add(new TableRowGroup());

            // Header row
            TableRow headerRow = new TableRow
            {
                Background = Brushes.DarkGray,
                FontWeight = FontWeights.Bold,
                FontSize = 9
            };

            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("#"))) { TextAlignment = TextAlignment.Center, Padding = new Thickness(3) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Hodowca"))) { TextAlignment = TextAlignment.Left, Padding = new Thickness(3) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Liczba"))) { TextAlignment = TextAlignment.Center, Padding = new Thickness(3) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Suma"))) { TextAlignment = TextAlignment.Right, Padding = new Thickness(3) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Cena"))) { TextAlignment = TextAlignment.Right, Padding = new Thickness(3) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Transport"))) { TextAlignment = TextAlignment.Right, Padding = new Thickness(3) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Komun."))) { TextAlignment = TextAlignment.Right, Padding = new Thickness(3) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Elast."))) { TextAlignment = TextAlignment.Right, Padding = new Thickness(3) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("S.Ptaków"))) { TextAlignment = TextAlignment.Right, Padding = new Thickness(3) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Średnia"))) { TextAlignment = TextAlignment.Right, Padding = new Thickness(3) });

            table.RowGroups[0].Rows.Add(headerRow);

            // Data rows
            foreach (var item in _reportData)
            {
                TableRow row = new TableRow { FontSize = 8 };

                // Highlight top 3
                if (item.Miejsce == 1)
                    row.Background = new SolidColorBrush(Color.FromRgb(255, 215, 0)); // Gold
                else if (item.Miejsce == 2)
                    row.Background = new SolidColorBrush(Color.FromRgb(192, 192, 192)); // Silver
                else if (item.Miejsce == 3)
                    row.Background = new SolidColorBrush(Color.FromRgb(205, 127, 50)); // Bronze

                row.Cells.Add(new TableCell(new Paragraph(new Run(item.Miejsce.ToString()))) { TextAlignment = TextAlignment.Center, Padding = new Thickness(3) });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.Dostawca))) { TextAlignment = TextAlignment.Left, Padding = new Thickness(3) });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.LiczbaOcen.ToString()))) { TextAlignment = TextAlignment.Center, Padding = new Thickness(3) });
                row.Cells.Add(new TableCell(new Paragraph(new Run($"{item.SumaPunktow:N1}"))) { TextAlignment = TextAlignment.Right, Padding = new Thickness(3) });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.SredniaCena.HasValue ? $"{item.SredniaCena.Value:N2}" : "-"))) { TextAlignment = TextAlignment.Right, Padding = new Thickness(3) });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.SredniaTransport.HasValue ? $"{item.SredniaTransport.Value:N2}" : "-"))) { TextAlignment = TextAlignment.Right, Padding = new Thickness(3) });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.SredniaKomunikacja.HasValue ? $"{item.SredniaKomunikacja.Value:N2}" : "-"))) { TextAlignment = TextAlignment.Right, Padding = new Thickness(3) });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.SredniaElastycznosc.HasValue ? $"{item.SredniaElastycznosc.Value:N2}" : "-"))) { TextAlignment = TextAlignment.Right, Padding = new Thickness(3) });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.SredniaStanPtakow.HasValue ? $"{item.SredniaStanPtakow.Value:N2}" : "-"))) { TextAlignment = TextAlignment.Right, Padding = new Thickness(3) });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.SredniaOgolna.HasValue ? $"{item.SredniaOgolna.Value:N2}" : "-"))) { TextAlignment = TextAlignment.Right, Padding = new Thickness(3) });

                table.RowGroups[0].Rows.Add(row);
            }

            doc.Blocks.Add(table);

            // Footer
            Paragraph footer = new Paragraph(new Run("* Ranking uwzględnia wszystkie kategorie: Cena, Transport, Komunikacja, Elastyczność i Stan Ptaków"))
            {
                FontSize = 9,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 10, 0, 0),
                TextAlignment = TextAlignment.Center
            };
            doc.Blocks.Add(footer);

            return doc;
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt",
                    DefaultExt = "csv",
                    FileName = $"TOP20_Hodowcow_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (StreamWriter writer = new StreamWriter(saveDialog.FileName, false, System.Text.Encoding.UTF8))
                    {
                        // Header
                        writer.WriteLine("Miejsce;Hodowca/Dostawca;Liczba Dostaw;Suma Punktów;Średnia Cena;Średnia Transport;Średnia Komunikacja;Średnia Elastyczność;Średni Stan Ptaków;Średnia Ogólna");

                        // Data
                        foreach (var item in _reportData)
                        {
                            writer.WriteLine($"{item.Miejsce};{item.Dostawca};{item.LiczbaOcen};" +
                                $"{item.SumaPunktow:N1};{item.SredniaCena?.ToString("N2") ?? "-"};" +
                                $"{item.SredniaTransport?.ToString("N2") ?? "-"};{item.SredniaKomunikacja?.ToString("N2") ?? "-"};" +
                                $"{item.SredniaElastycznosc?.ToString("N2") ?? "-"};{item.SredniaStanPtakow?.ToString("N2") ?? "-"};" +
                                $"{item.SredniaOgolna?.ToString("N2") ?? "-"}");
                        }
                    }

                    MessageBox.Show($"Raport został wyeksportowany do:\n{saveDialog.FileName}",
                        "Eksport zakończony", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas eksportu:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class Top20Item
    {
        public long Miejsce { get; set; }
        public string Dostawca { get; set; }
        public int LiczbaOcen { get; set; }
        public decimal SumaPunktow { get; set; }
        public decimal? SredniaCena { get; set; }
        public decimal? SredniaTransport { get; set; }
        public decimal? SredniaKomunikacja { get; set; }
        public decimal? SredniaElastycznosc { get; set; }
        public decimal? SredniaStanPtakow { get; set; }
        public decimal? SredniaOgolna { get; set; }
    }
}
