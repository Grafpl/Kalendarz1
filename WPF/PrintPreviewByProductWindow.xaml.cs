using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace Kalendarz1.WPF
{
    public partial class PrintPreviewByProductWindow : Window
    {
        private readonly FlowDocument _document;
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public PrintPreviewByProductWindow(DataTable orders, DateTime selectedDate, Dictionary<int, string> productCache)
        {
            InitializeComponent();
            _document = CreateDocument(orders, selectedDate, productCache);
            documentViewer.Document = _document;
        }

        private FlowDocument CreateDocument(DataTable orders, DateTime selectedDate, Dictionary<int, string> productCache)
        {
            var doc = new FlowDocument
            {
                ColumnWidth = 999999,
                PagePadding = new Thickness(50),
                FontFamily = new FontFamily("Segoe UI")
            };

            // Nagłówek
            var header = new Paragraph
            {
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            header.Inlines.Add($"Zestawienie produktów - {selectedDate:dd.MM.yyyy dddd}");
            doc.Blocks.Add(header);

            // Zbierz wszystkie zamówienia z produktami
            var productOrders = new Dictionary<string, List<ProductOrderInfo>>();

            // Pobierz szczegóły zamówień asynchronicznie
            var orderDetails = GetOrderDetailsAsync(orders, productCache).GetAwaiter().GetResult();

            foreach (var detail in orderDetails)
            {
                if (!productOrders.ContainsKey(detail.ProductName))
                    productOrders[detail.ProductName] = new List<ProductOrderInfo>();

                productOrders[detail.ProductName].Add(detail);
            }

            // Posortuj produkty alfabetycznie
            var sortedProducts = productOrders.Keys.OrderBy(k => k).ToList();

            foreach (var productName in sortedProducts)
            {
                var productGroup = productOrders[productName];
                var totalQuantity = productGroup.Sum(p => p.Quantity);

                // Nagłówek produktu
                var productHeader = new Paragraph
                {
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush(Color.FromRgb(179, 229, 252)),
                    Padding = new Thickness(5),
                    Margin = new Thickness(0, 20, 0, 10)
                };
                productHeader.Inlines.Add($"📦 {productName} - SUMA: {totalQuantity:N0} kg");
                doc.Blocks.Add(productHeader);

                // Tabela dla produktu
                var table = new Table();
                table.CellSpacing = 0;
                table.BorderBrush = Brushes.Black;
                table.BorderThickness = new Thickness(1);

                // Kolumny
                table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

                var tableGroup = new TableRowGroup();

                // Nagłówek tabeli
                var headerRow = new TableRow();
                headerRow.Background = new SolidColorBrush(Color.FromRgb(44, 62, 80));

                headerRow.Cells.Add(CreateCell("Odbiorca", true, Brushes.White));
                headerRow.Cells.Add(CreateCell("Handlowiec", true, Brushes.White));
                headerRow.Cells.Add(CreateCell("Ilość", true, Brushes.White));
                headerRow.Cells.Add(CreateCell("Folia", true, Brushes.White));
                headerRow.Cells.Add(CreateCell("Palety", true, Brushes.White));

                tableGroup.Rows.Add(headerRow);

                // Dane posortowane według odbiorcy
                foreach (var order in productGroup.OrderBy(o => o.ClientName))
                {
                    var dataRow = new TableRow();

                    dataRow.Cells.Add(CreateCell(order.ClientName));
                    dataRow.Cells.Add(CreateCell(order.Salesman));
                    dataRow.Cells.Add(CreateCell($"{order.Quantity:N0} kg"));
                    dataRow.Cells.Add(CreateCell(order.HasFoil ? "TAK" : ""));
                    dataRow.Cells.Add(CreateCell($"{order.Pallets:N1}"));

                    tableGroup.Rows.Add(dataRow);
                }

                // Wiersz sumy
                var sumRow = new TableRow();
                sumRow.Background = new SolidColorBrush(Color.FromRgb(230, 230, 230));
                sumRow.Cells.Add(CreateCell("RAZEM:", true));
                sumRow.Cells.Add(CreateCell(""));
                sumRow.Cells.Add(CreateCell($"{totalQuantity:N0} kg", true));
                sumRow.Cells.Add(CreateCell(""));
                sumRow.Cells.Add(CreateCell($"{productGroup.Sum(p => p.Pallets):N1}", true));

                tableGroup.Rows.Add(sumRow);

                table.RowGroups.Add(tableGroup);
                doc.Blocks.Add(table);
            }

            // Podsumowanie końcowe
            var finalSummary = new Paragraph
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 30, 0, 10),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Foreground = Brushes.White,
                Padding = new Thickness(10)
            };

            var grandTotal = productOrders.Values.SelectMany(v => v).Sum(o => o.Quantity);
            var totalPallets = productOrders.Values.SelectMany(v => v).Sum(o => o.Pallets);

            finalSummary.Inlines.Add($"PODSUMOWANIE CAŁKOWITE: {grandTotal:N0} kg | ");
            finalSummary.Inlines.Add($"Palety: {totalPallets:N1} | ");
            finalSummary.Inlines.Add($"Produktów: {productOrders.Count}");
            doc.Blocks.Add(finalSummary);

            // Stopka
            var footer = new Paragraph
            {
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0),
                Foreground = Brushes.Gray
            };
            footer.Inlines.Add($"Wydrukowano: {DateTime.Now:yyyy-MM-dd HH:mm}");
            doc.Blocks.Add(footer);

            return doc;
        }

        private async Task<List<ProductOrderInfo>> GetOrderDetailsAsync(DataTable orders, Dictionary<int, string> productCache)
        {
            var result = new List<ProductOrderInfo>();
            var validOrderIds = orders.AsEnumerable()
                .Where(r => r.Field<int>("Id") > 0 &&
                           r.Field<string>("Status") != "SUMA" &&
                           r.Field<string>("Status") != "Anulowane" &&
                           r.Field<string>("Status") != "Wydanie bez zamówienia")
                .Select(r => r.Field<int>("Id"))
                .ToList();

            if (!validOrderIds.Any())
                return result;

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var sql = $@"
                    SELECT 
                        zmt.ZamowienieId,
                        zmt.KodTowaru,
                        zmt.Ilosc,
                        ISNULL(zmt.Folia, 0) as Folia,
                        zm.KlientId,
                        zm.LiczbaPalet
                    FROM ZamowieniaMiesoTowar zmt
                    INNER JOIN ZamowieniaMieso zm ON zmt.ZamowienieId = zm.Id
                    WHERE zmt.ZamowienieId IN ({string.Join(",", validOrderIds)})";

                using var cmd = new SqlCommand(sql, cn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var orderId = reader.GetInt32(0);
                    var productId = reader.GetInt32(1);
                    var quantity = reader.GetDecimal(2);
                    var hasFoil = reader.GetBoolean(3);
                    var clientId = reader.GetInt32(4);
                    var pallets = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5);

                    // Znajdź informacje o kliencie z DataTable
                    var orderRow = orders.AsEnumerable()
                        .FirstOrDefault(r => r.Field<int>("Id") == orderId);

                    if (orderRow != null && productCache.ContainsKey(productId))
                    {
                        var info = new ProductOrderInfo
                        {
                            ProductName = productCache[productId],
                            ClientName = CleanName(orderRow.Field<string>("Odbiorca") ?? ""),
                            Salesman = orderRow.Field<string>("Handlowiec") ?? "",
                            Quantity = quantity,
                            HasFoil = hasFoil,
                            Pallets = pallets / GetProductCountForOrder(orderId, validOrderIds, cn).GetAwaiter().GetResult()
                        };

                        result.Add(info);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas pobierania szczegółów: {ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return result;
        }

        private async Task<int> GetProductCountForOrder(int orderId, List<int> allOrderIds, SqlConnection connection)
        {
            var sql = "SELECT COUNT(DISTINCT KodTowaru) FROM ZamowieniaMiesoTowar WHERE ZamowienieId = @OrderId";
            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@OrderId", orderId);
            var result = await cmd.ExecuteScalarAsync();
            return Math.Max(1, Convert.ToInt32(result ?? 1));
        }

        private TableCell CreateCell(string text, bool isBold = false, Brush foreground = null)
        {
            var cell = new TableCell();
            var paragraph = new Paragraph(new Run(text));

            if (isBold)
                paragraph.FontWeight = FontWeights.Bold;

            if (foreground != null)
                paragraph.Foreground = foreground;

            paragraph.Margin = new Thickness(5);
            cell.Blocks.Add(paragraph);
            cell.BorderBrush = Brushes.Black;
            cell.BorderThickness = new Thickness(0.5);

            return cell;
        }

        private string CleanName(string name)
        {
            return name.Replace("📝", "")
                      .Replace("📦", "")
                      .Replace("🍗", "")
                      .Replace("🍖", "")
                      .Replace("🥩", "")
                      .Replace("🐔", "")
                      .Replace("└", "")
                      .Trim();
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                printDialog.PrintDocument(
                    ((IDocumentPaginatorSource)_document).DocumentPaginator,
                    "Produkty");

                MessageBox.Show("Dokument został wysłany do drukarki.",
                    "Drukowanie", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private class ProductOrderInfo
        {
            public string ProductName { get; set; }
            public string ClientName { get; set; }
            public string Salesman { get; set; }
            public decimal Quantity { get; set; }
            public bool HasFoil { get; set; }
            public decimal Pallets { get; set; }
        }
    }
}