using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Kalendarz1.WPF
{
    public partial class PrintPreviewByClientWindow : Window
    {
        private readonly FlowDocument _document;

        public PrintPreviewByClientWindow(DataTable orders, DateTime selectedDate, Dictionary<int, string> productCache)
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
            header.Inlines.Add($"Zestawienie zamówień - {selectedDate:dd.MM.yyyy dddd}");
            doc.Blocks.Add(header);

            // Podsumowanie
            decimal totalOrdered = 0;
            decimal totalReleased = 0;
            int orderCount = 0;

            foreach (DataRow row in orders.Rows)
            {
                var status = row.Field<string>("Status") ?? "";
                if (status != "SUMA" && status != "Wydanie bez zamówienia" && status != "Anulowane")
                {
                    totalOrdered += row.Field<decimal>("IloscZamowiona");
                    totalReleased += row.Field<decimal>("IloscFaktyczna");
                    orderCount++;
                }
            }

            var summary = new Paragraph
            {
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 20),
                Background = Brushes.LightGray,
                Padding = new Thickness(10)
            };
            summary.Inlines.Add($"Liczba zamówień: {orderCount} | ");
            summary.Inlines.Add($"Zamówiono: {totalOrdered:N0} kg | ");
            summary.Inlines.Add($"Wydano: {totalReleased:N0} kg | ");
            summary.Inlines.Add($"Realizacja: {(totalOrdered > 0 ? (totalReleased / totalOrdered * 100) : 0):N1}%");
            doc.Blocks.Add(summary);

            // Grupowanie według handlowców
            var salesmenGroups = orders.AsEnumerable()
                .Where(r => r.Field<string>("Status") != "SUMA" &&
                           r.Field<string>("Status") != "Anulowane" &&
                           r.Field<int>("Id") > 0)
                .GroupBy(r => r.Field<string>("Handlowiec") ?? "")
                .OrderBy(g => g.Key);

            foreach (var salesmanGroup in salesmenGroups)
            {
                // Nagłówek handlowca
                var salesmanHeader = new Paragraph
                {
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                    Padding = new Thickness(5),
                    Margin = new Thickness(0, 20, 0, 10)
                };
                salesmanHeader.Inlines.Add($"Handlowiec: {(string.IsNullOrEmpty(salesmanGroup.Key) ? "Brak" : salesmanGroup.Key)}");
                doc.Blocks.Add(salesmanHeader);

                // Tabela dla handlowca
                var table = new Table();
                table.CellSpacing = 0;
                table.BorderBrush = Brushes.Black;
                table.BorderThickness = new Thickness(1);

                // Kolumny
                table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });

                var tableGroup = new TableRowGroup();

                // Nagłówek tabeli
                var headerRow = new TableRow();
                headerRow.Background = new SolidColorBrush(Color.FromRgb(44, 62, 80));

                headerRow.Cells.Add(CreateCell("Odbiorca", true, Brushes.White));
                headerRow.Cells.Add(CreateCell("Zamówiono", true, Brushes.White));
                headerRow.Cells.Add(CreateCell("Wydano", true, Brushes.White));
                headerRow.Cells.Add(CreateCell("Palety", true, Brushes.White));
                headerRow.Cells.Add(CreateCell("Termin odbioru", true, Brushes.White));

                tableGroup.Rows.Add(headerRow);

                // Dane
                foreach (DataRow row in salesmanGroup.OrderBy(r => r.Field<string>("Odbiorca")))
                {
                    var dataRow = new TableRow();

                    var odbiorca = CleanName(row.Field<string>("Odbiorca") ?? "");
                    var zamowiono = row.Field<decimal>("IloscZamowiona");
                    var wydano = row.Field<decimal>("IloscFaktyczna");
                    var palety = row.Field<decimal>("Palety");
                    var termin = row.Field<string>("TerminOdbioru") ?? "";

                    dataRow.Cells.Add(CreateCell(odbiorca));
                    dataRow.Cells.Add(CreateCell($"{zamowiono:N0} kg"));
                    dataRow.Cells.Add(CreateCell($"{wydano:N0} kg"));
                    dataRow.Cells.Add(CreateCell($"{palety:N1}"));
                    dataRow.Cells.Add(CreateCell(termin));

                    tableGroup.Rows.Add(dataRow);
                }

                table.RowGroups.Add(tableGroup);
                doc.Blocks.Add(table);
            }

            // Stopka
            var footer = new Paragraph
            {
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 30, 0, 0),
                Foreground = Brushes.Gray
            };
            footer.Inlines.Add($"Wydrukowano: {DateTime.Now:yyyy-MM-dd HH:mm}");
            doc.Blocks.Add(footer);

            return doc;
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
                    "Zamówienia");

                MessageBox.Show("Dokument został wysłany do drukarki.",
                    "Drukowanie", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}