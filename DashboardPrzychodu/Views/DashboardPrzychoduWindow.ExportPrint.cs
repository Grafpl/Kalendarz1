using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ClosedXML.Excel;
using Kalendarz1.DashboardPrzychodu.Models;

namespace Kalendarz1.DashboardPrzychodu.Views
{
    /// <summary>
    /// #11 - Eksport raportu do Excel (ClosedXML) i drukowanie (WPF FlowDocument + PrintDialog).
    /// </summary>
    public partial class DashboardPrzychoduWindow
    {
        /// <summary>
        /// Eksport dziennego raportu do Excel (.xlsx) - 3 sekcje: nagłówek, podsumowanie, tabela dostaw.
        /// </summary>
        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedDate = dpData.SelectedDate ?? DateTime.Today;
                string fileName = $"PrzychodZywca_{selectedDate:yyyy-MM-dd}_{DateTime.Now:HHmmss}.xlsx";

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = fileName,
                    DefaultExt = ".xlsx",
                    Filter = "Pliki Excel (*.xlsx)|*.xlsx"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var ws = workbook.Worksheets.Add("Przychod Zywca");

                        // Naglowek
                        ws.Cell(1, 1).Value = $"PRZYCHOD ZYWCA - {selectedDate:dd.MM.yyyy}";
                        ws.Cell(1, 1).Style.Font.Bold = true;
                        ws.Cell(1, 1).Style.Font.FontSize = 16;
                        ws.Range(1, 1, 1, 9).Merge();

                        // Podsumowanie
                        ws.Cell(3, 1).Value = "PODSUMOWANIE:";
                        ws.Cell(3, 1).Style.Font.Bold = true;
                        ws.Cell(4, 1).Value = "Planowane [kg]:";
                        ws.Cell(4, 2).Value = _podsumowanie.KgPlanSuma;
                        ws.Cell(5, 1).Value = "Zwazone [kg]:";
                        ws.Cell(5, 2).Value = _podsumowanie.KgZwazoneSuma;
                        ws.Cell(6, 1).Value = "Odchylenie [kg]:";
                        ws.Cell(6, 2).Value = _podsumowanie.OdchylenieKgSuma;
                        ws.Cell(7, 1).Value = "Odchylenie [%]:";
                        ws.Cell(7, 2).Value = _podsumowanie.OdchylenieProc;

                        // Prognoza
                        ws.Cell(9, 1).Value = "PROGNOZA PRODUKCJI:";
                        ws.Cell(9, 1).Style.Font.Bold = true;
                        ws.Cell(10, 1).Value = "Tuszki ogolem [kg]:";
                        ws.Cell(10, 2).Value = _podsumowanie.PrognozaTuszekKg;
                        ws.Cell(11, 1).Value = "Klasa A [kg]:";
                        ws.Cell(11, 2).Value = _podsumowanie.PrognozaKlasaAKg;
                        ws.Cell(12, 1).Value = "Klasa B [kg]:";
                        ws.Cell(12, 2).Value = _podsumowanie.PrognozaKlasaBKg;

                        // Tabela dostaw
                        int startRow = 14;
                        ws.Cell(startRow, 1).Value = "LP";
                        ws.Cell(startRow, 2).Value = "Hodowca";
                        ws.Cell(startRow, 3).Value = "Plan [kg]";
                        ws.Cell(startRow, 4).Value = "Rzecz. [kg]";
                        ws.Cell(startRow, 5).Value = "Odchylenie [kg]";
                        ws.Cell(startRow, 6).Value = "Odchylenie [%]";
                        ws.Cell(startRow, 7).Value = "Sr. waga";
                        ws.Cell(startRow, 8).Value = "Status";
                        ws.Cell(startRow, 9).Value = "Przyjazd";

                        var headerRange = ws.Range(startRow, 1, startRow, 9);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                        headerRange.Style.Font.FontColor = XLColor.White;

                        int row = startRow + 1;
                        foreach (var d in _dostawy)
                        {
                            ws.Cell(row, 1).Value = d.NrKursu;
                            ws.Cell(row, 2).Value = d.Hodowca;
                            ws.Cell(row, 3).Value = d.KgPlan;
                            ws.Cell(row, 4).Value = d.KgRzeczywiste;
                            ws.Cell(row, 5).Value = d.OdchylenieKgCalc ?? 0;
                            ws.Cell(row, 6).Value = d.OdchylenieProcCalc ?? 0;
                            ws.Cell(row, 7).Value = d.SredniaWagaRzeczywistaCalc ?? 0;
                            ws.Cell(row, 8).Value = d.StatusText;
                            ws.Cell(row, 9).Value = d.PrzyjazdDisplay;

                            // Kolorowanie odchylenia
                            if (d.Poziom == PoziomOdchylenia.Problem)
                            {
                                ws.Cell(row, 5).Style.Font.FontColor = XLColor.Red;
                                ws.Cell(row, 6).Style.Font.FontColor = XLColor.Red;
                            }
                            else if (d.Poziom == PoziomOdchylenia.Uwaga)
                            {
                                ws.Cell(row, 5).Style.Font.FontColor = XLColor.Orange;
                                ws.Cell(row, 6).Style.Font.FontColor = XLColor.Orange;
                            }

                            row++;
                        }

                        ws.Columns().AdjustToContents();
                        ws.Column(2).Width = 30;

                        workbook.SaveAs(saveDialog.FileName);
                    }

                    MessageBox.Show($"Eksport zakonczony pomyslnie!\n\n{saveDialog.FileName}",
                        "Eksport Excel", MessageBoxButton.OK, MessageBoxImage.Information);

                    Debug.WriteLine($"[DashboardPrzychodu] Eksport Excel: {saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardPrzychodu] Blad eksportu: {ex.Message}");
                MessageBox.Show($"Blad podczas eksportu:\n\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Drukowanie raportu (FlowDocument + PrintDialog).
        /// </summary>
        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedDate = dpData.SelectedDate ?? DateTime.Today;

                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var document = new FlowDocument
                    {
                        PagePadding = new Thickness(50),
                        ColumnWidth = double.MaxValue
                    };

                    var header = new Paragraph(new Run($"PRZYCHOD ZYWCA - {selectedDate:dd.MM.yyyy}"))
                    {
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 20)
                    };
                    document.Blocks.Add(header);

                    var summary = new Paragraph();
                    summary.Inlines.Add(new Run("PODSUMOWANIE\n") { FontWeight = FontWeights.Bold });
                    summary.Inlines.Add(new Run($"Planowane: {_podsumowanie.KgPlanSuma:N0} kg ({_podsumowanie.SztukiPlanSuma:N0} szt)\n"));
                    summary.Inlines.Add(new Run($"Zwazone: {_podsumowanie.KgZwazoneSuma:N0} kg ({_podsumowanie.SztukiZwazoneSuma:N0} szt)\n"));
                    summary.Inlines.Add(new Run($"Odchylenie: {_podsumowanie.OdchylenieKgSuma:+#;-#;0} kg ({_podsumowanie.OdchylenieProc:+0.0;-0.0;0}%)\n"));
                    summary.Inlines.Add(new Run($"Realizacja: {_podsumowanie.ProcentRealizacjiKg}%\n"));
                    summary.Margin = new Thickness(0, 0, 0, 20);
                    document.Blocks.Add(summary);

                    var prognosis = new Paragraph();
                    prognosis.Inlines.Add(new Run("PROGNOZA TUSZEK\n") { FontWeight = FontWeights.Bold });
                    prognosis.Inlines.Add(new Run($"Tuszki ogolem: {_podsumowanie.PrognozaTuszekKg:N0} kg (78%)\n"));
                    prognosis.Inlines.Add(new Run($"Klasa A: {_podsumowanie.PrognozaKlasaAKg:N0} kg (80%)\n"));
                    prognosis.Inlines.Add(new Run($"Klasa B: {_podsumowanie.PrognozaKlasaBKg:N0} kg (20%)\n"));
                    prognosis.Margin = new Thickness(0, 0, 0, 20);
                    document.Blocks.Add(prognosis);

                    var table = new Paragraph();
                    table.Inlines.Add(new Run("SZCZEGOLY DOSTAW\n\n") { FontWeight = FontWeights.Bold });

                    foreach (var d in _dostawy.Take(30))
                    {
                        table.Inlines.Add(new Run(
                            $"{d.NrKursu}. {d.Hodowca,-25} Plan: {d.KgPlan,8:N0} kg  Rzecz: {d.KgRzeczywiste,8:N0} kg  {d.StatusText}\n"));
                    }

                    if (_dostawy.Count > 30)
                    {
                        table.Inlines.Add(new Run($"\n... i {_dostawy.Count - 30} wiecej dostaw"));
                    }

                    document.Blocks.Add(table);

                    var footer = new Paragraph(new Run($"\nWygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm:ss}"))
                    {
                        FontSize = 10,
                        Foreground = Brushes.Gray
                    };
                    document.Blocks.Add(footer);

                    var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
                    printDialog.PrintDocument(paginator, $"Przychod Zywca - {selectedDate:dd.MM.yyyy}");

                    Debug.WriteLine("[DashboardPrzychodu] Drukowanie zakonczone");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardPrzychodu] Blad drukowania: {ex.Message}");
                MessageBox.Show($"Blad podczas drukowania:\n\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
