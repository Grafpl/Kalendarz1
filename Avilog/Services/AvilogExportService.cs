using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Kalendarz1.Avilog.Models;

namespace Kalendarz1.Avilog.Services
{
    /// <summary>
    /// Serwis eksportu danych Avilog do Excel i PDF
    /// </summary>
    public class AvilogExportService
    {
        private readonly string _defaultExportPath;

        public AvilogExportService()
        {
            _defaultExportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Avilog");
            if (!Directory.Exists(_defaultExportPath))
            {
                Directory.CreateDirectory(_defaultExportPath);
            }
        }

        /// <summary>
        /// Eksportuje dane do pliku Excel
        /// </summary>
        public string ExportToExcel(
            List<AvilogKursModel> kursy,
            List<AvilogDayModel> dni,
            AvilogSummaryModel summary,
            DateTime dataOd,
            DateTime dataDo,
            decimal stawkaZaKg)
        {
            using (var workbook = new XLWorkbook())
            {
                // Arkusz 1: Podsumowanie
                CreateSummarySheet(workbook, dni, summary, dataOd, dataDo, stawkaZaKg);

                // Arkusz 2: Wszystkie kursy
                CreateKursySheet(workbook, kursy);

                // Arkusze dzienne
                var kursyByDay = kursy.GroupBy(k => k.CalcDate.Date).OrderBy(g => g.Key);
                foreach (var dayGroup in kursyByDay)
                {
                    CreateDaySheet(workbook, dayGroup.Key, dayGroup.ToList(), stawkaZaKg);
                }

                // Zapisz plik
                string fileName = $"Avilog_{dataOd:yyyy-MM-dd}_{dataDo:yyyy-MM-dd}.xlsx";
                string filePath = Path.Combine(_defaultExportPath, fileName);

                // Jeśli plik istnieje, dodaj timestamp
                if (File.Exists(filePath))
                {
                    fileName = $"Avilog_{dataOd:yyyy-MM-dd}_{dataDo:yyyy-MM-dd}_{DateTime.Now:HHmmss}.xlsx";
                    filePath = Path.Combine(_defaultExportPath, fileName);
                }

                workbook.SaveAs(filePath);
                return filePath;
            }
        }

        private void CreateSummarySheet(XLWorkbook workbook, List<AvilogDayModel> dni, AvilogSummaryModel summary,
            DateTime dataOd, DateTime dataDo, decimal stawkaZaKg)
        {
            var ws = workbook.Worksheets.Add("Podsumowanie");

            // Nagłówek
            ws.Cell(1, 1).Value = "ROZLICZENIE AVILOG - TRANSPORT ŻYWCA";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Range(1, 1, 1, 10).Merge();

            ws.Cell(2, 1).Value = $"Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}";
            ws.Cell(2, 1).Style.Font.FontSize = 12;
            ws.Range(2, 1, 2, 10).Merge();

            // Tabela podsumowania dziennego
            int startRow = 4;
            string[] headers = { "#", "Dzień", "Data", "Sztuki", "Brutto kg", "Tara kg", "Netto kg",
                                "Upadki szt", "Upadki kg", "Różnica kg", "KM", "Kursy", "Zestawy", "Godziny", "Koszt" };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(startRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#37474F");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int row = startRow + 1;
            int lp = 0;
            foreach (var dzien in dni)
            {
                lp++;
                ws.Cell(row, 1).Value = lp;
                ws.Cell(row, 2).Value = dzien.DzienTygodnia;
                ws.Cell(row, 3).Value = dzien.Data;
                ws.Cell(row, 3).Style.NumberFormat.Format = "dd.MM.yyyy";
                ws.Cell(row, 4).Value = dzien.SumaSztuk;
                ws.Cell(row, 5).Value = dzien.SumaBrutto;
                ws.Cell(row, 6).Value = dzien.SumaTara;
                ws.Cell(row, 7).Value = dzien.SumaNetto;
                ws.Cell(row, 8).Value = dzien.SumaUpadkowSzt;
                ws.Cell(row, 9).Value = dzien.SumaUpadkowKg;
                ws.Cell(row, 10).Value = dzien.SumaRoznicaKg;
                ws.Cell(row, 11).Value = dzien.SumaKM;
                ws.Cell(row, 12).Value = dzien.LiczbaKursow;
                ws.Cell(row, 13).Value = dzien.LiczbaZestawow;
                ws.Cell(row, 14).Value = dzien.SumaGodzin;
                ws.Cell(row, 14).Style.NumberFormat.Format = "0.00";
                ws.Cell(row, 15).Value = dzien.SumaRoznicaKg * stawkaZaKg;
                ws.Cell(row, 15).Style.NumberFormat.Format = "#,##0.00 zł";

                // Formatowanie liczbowe
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0";

                // Kolor co drugi wiersz
                if (lp % 2 == 0)
                {
                    ws.Range(row, 1, row, 15).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");
                }

                row++;
            }

            // Wiersz sumy
            ws.Cell(row, 1).Value = "";
            ws.Cell(row, 2).Value = "SUMA";
            ws.Cell(row, 3).Value = "";
            ws.Cell(row, 4).Value = summary.SumaSztuk;
            ws.Cell(row, 5).Value = summary.SumaBrutto;
            ws.Cell(row, 6).Value = summary.SumaTara;
            ws.Cell(row, 7).Value = summary.SumaNetto;
            ws.Cell(row, 8).Value = summary.SumaUpadkowSzt;
            ws.Cell(row, 9).Value = summary.SumaUpadkowKg;
            ws.Cell(row, 10).Value = summary.SumaRoznicaKg;
            ws.Cell(row, 11).Value = summary.SumaKM;
            ws.Cell(row, 12).Value = summary.LiczbaKursow;
            ws.Cell(row, 13).Value = summary.LiczbaZestawow;
            ws.Cell(row, 14).Value = summary.SumaGodzin;
            ws.Cell(row, 15).Value = summary.DoZaplaty;

            var sumRange = ws.Range(row, 1, row, 15);
            sumRange.Style.Font.Bold = true;
            sumRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#388E3C");
            sumRange.Style.Font.FontColor = XLColor.White;

            // Formatowanie sumy
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 14).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 15).Style.NumberFormat.Format = "#,##0.00 zł";

            row += 3;

            // Sekcja DO ZAPŁATY
            ws.Cell(row, 1).Value = "PODSUMOWANIE FINANSOWE";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 14;
            ws.Range(row, 1, row, 5).Merge();
            row++;

            ws.Cell(row, 1).Value = "Stawka za kg:";
            ws.Cell(row, 2).Value = stawkaZaKg;
            ws.Cell(row, 2).Style.NumberFormat.Format = "0.000 zł/kg";
            row++;

            ws.Cell(row, 1).Value = "Różnica kg:";
            ws.Cell(row, 2).Value = summary.SumaRoznicaKg;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0 kg";
            row++;

            ws.Cell(row, 1).Value = "DO ZAPŁATY:";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 14;
            ws.Cell(row, 2).Value = summary.DoZaplaty;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 zł";
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 2).Style.Font.FontSize = 14;
            ws.Cell(row, 2).Style.Font.FontColor = XLColor.FromHtml("#1B5E20");

            // Autofit kolumn
            ws.Columns().AdjustToContents();

            // Ramki tabeli
            var tableRange = ws.Range(startRow, 1, startRow + dni.Count, 15);
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private void CreateKursySheet(XLWorkbook workbook, List<AvilogKursModel> kursy)
        {
            var ws = workbook.Worksheets.Add("Wszystkie kursy");

            string[] headers = { "LP", "Data", "Hodowca", "Szt", "Brutto", "Tara", "Netto",
                                "Upadki szt", "Upadki kg", "Różnica kg", "KM", "Start", "Koniec",
                                "Godziny", "Kierowca", "Auto", "Naczepa" };

            // Nagłówki
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#37474F");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Dane
            int row = 2;
            foreach (var kurs in kursy)
            {
                ws.Cell(row, 1).Value = kurs.LP;
                ws.Cell(row, 2).Value = kurs.CalcDate;
                ws.Cell(row, 2).Style.NumberFormat.Format = "dd.MM.yyyy";
                ws.Cell(row, 3).Value = kurs.HodowcaNazwa;
                ws.Cell(row, 4).Value = kurs.SztukiRazem;
                ws.Cell(row, 5).Value = kurs.BruttoHodowcy;
                ws.Cell(row, 6).Value = kurs.TaraHodowcy;
                ws.Cell(row, 7).Value = kurs.NettoHodowcy;
                ws.Cell(row, 8).Value = kurs.SztukiPadle;
                ws.Cell(row, 9).Value = kurs.UpadkiKg;
                ws.Cell(row, 10).Value = kurs.RoznicaKg;
                ws.Cell(row, 11).Value = kurs.DystansKM;
                ws.Cell(row, 12).Value = kurs.PoczatekUslugiFormatowany;
                ws.Cell(row, 13).Value = kurs.KoniecUslugiFormatowany;
                ws.Cell(row, 14).Value = kurs.CzasUslugiGodziny;
                ws.Cell(row, 14).Style.NumberFormat.Format = "0.00";
                ws.Cell(row, 15).Value = kurs.KierowcaNazwa;
                ws.Cell(row, 16).Value = kurs.CarID;
                ws.Cell(row, 17).Value = kurs.TrailerID;

                // Formatowanie liczbowe
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0";

                // Kolorowanie ostrzeżeń
                if (kurs.BrakKilometrow)
                {
                    ws.Range(row, 1, row, 17).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF8E1");
                }
                else if (kurs.BrakGodzin)
                {
                    ws.Range(row, 1, row, 17).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE0B2");
                }
                else if (kurs.DuzaRoznicaWag)
                {
                    ws.Range(row, 1, row, 17).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFCDD2");
                }
                else if (row % 2 == 0)
                {
                    ws.Range(row, 1, row, 17).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");
                }

                row++;
            }

            // Autofit i ramki
            ws.Columns().AdjustToContents();
            var tableRange = ws.Range(1, 1, row - 1, 17);
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Autofilter
            ws.RangeUsed().SetAutoFilter();
        }

        private void CreateDaySheet(XLWorkbook workbook, DateTime date, List<AvilogKursModel> kursy, decimal stawkaZaKg)
        {
            string sheetName = date.ToString("dd.MM ddd");
            // Excel ma limit 31 znaków na nazwę arkusza
            if (sheetName.Length > 31) sheetName = sheetName.Substring(0, 31);

            var ws = workbook.Worksheets.Add(sheetName);

            // Nagłówek dnia
            ws.Cell(1, 1).Value = $"Rozliczenie Avilog - {date:dd.MM.yyyy} ({date:dddd})";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 12).Merge();

            string[] headers = { "LP", "Hodowca", "Szt", "Brutto", "Tara", "Netto",
                                "Upadki szt", "Upadki kg", "Różnica kg", "KM", "Godz.", "Kierowca" };

            // Nagłówki
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(3, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#37474F");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Dane
            int row = 4;
            int lp = 0;
            decimal sumaNetto = 0, sumaUpadkow = 0, sumaRoznica = 0;
            int sumaSztuk = 0, sumaKM = 0;
            decimal sumaGodzin = 0;

            foreach (var kurs in kursy)
            {
                lp++;
                ws.Cell(row, 1).Value = lp;
                ws.Cell(row, 2).Value = kurs.HodowcaNazwa;
                ws.Cell(row, 3).Value = kurs.SztukiRazem;
                ws.Cell(row, 4).Value = kurs.BruttoHodowcy;
                ws.Cell(row, 5).Value = kurs.TaraHodowcy;
                ws.Cell(row, 6).Value = kurs.NettoHodowcy;
                ws.Cell(row, 7).Value = kurs.SztukiPadle;
                ws.Cell(row, 8).Value = kurs.UpadkiKg;
                ws.Cell(row, 9).Value = kurs.RoznicaKg;
                ws.Cell(row, 10).Value = kurs.DystansKM;
                ws.Cell(row, 11).Value = kurs.CzasUslugiGodziny;
                ws.Cell(row, 11).Style.NumberFormat.Format = "0.00";
                ws.Cell(row, 12).Value = kurs.KierowcaNazwa;

                // Formatowanie liczbowe
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0";

                // Sumowanie
                sumaSztuk += kurs.SztukiRazem;
                sumaNetto += kurs.NettoHodowcy;
                sumaUpadkow += kurs.UpadkiKg;
                sumaRoznica += kurs.RoznicaKg;
                sumaKM += kurs.DystansKM;
                sumaGodzin += kurs.CzasUslugiGodziny;

                if (row % 2 == 0)
                {
                    ws.Range(row, 1, row, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");
                }

                row++;
            }

            // Wiersz sumy
            ws.Cell(row, 1).Value = "";
            ws.Cell(row, 2).Value = "SUMA";
            ws.Cell(row, 3).Value = sumaSztuk;
            ws.Cell(row, 6).Value = sumaNetto;
            ws.Cell(row, 8).Value = sumaUpadkow;
            ws.Cell(row, 9).Value = sumaRoznica;
            ws.Cell(row, 10).Value = sumaKM;
            ws.Cell(row, 11).Value = sumaGodzin;

            var sumRange = ws.Range(row, 1, row, 12);
            sumRange.Style.Font.Bold = true;
            sumRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#388E3C");
            sumRange.Style.Font.FontColor = XLColor.White;

            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 11).Style.NumberFormat.Format = "0.00";

            row += 2;

            // Koszt dnia
            ws.Cell(row, 1).Value = "Koszt dnia:";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = sumaRoznica * stawkaZaKg;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 zł";
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 2).Style.Font.FontColor = XLColor.FromHtml("#1B5E20");

            // Autofit i ramki
            ws.Columns().AdjustToContents();
            var tableRange = ws.Range(3, 1, row - 2, 12);
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }
    }
}
