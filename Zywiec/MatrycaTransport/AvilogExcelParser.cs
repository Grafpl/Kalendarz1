using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kalendarz1
{
    /// <summary>
    /// Parser plików Excel z planowaniem transportu od AVILOG
    /// </summary>
    public class AvilogExcelParser
    {
        /// <summary>
        /// Parsuje plik Excel z AVILOG i zwraca listę wierszy transportowych
        /// </summary>
        public AvilogParseResult ParseExcel(string filePath)
        {
            var result = new AvilogParseResult();

            try
            {
                using (SpreadsheetDocument doc = SpreadsheetDocument.Open(filePath, false))
                {
                    WorkbookPart workbookPart = doc.WorkbookPart;
                    SharedStringTablePart stringTable = workbookPart.SharedStringTablePart;
                    WorksheetPart worksheetPart = workbookPart.WorksheetParts.First();
                    SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

                    var rows = sheetData.Elements<Row>().ToList();

                    // Wyciągnij datę uboju z nagłówka
                    result.DataUboju = ExtractDataUboju(rows, stringTable);

                    // Parsuj wiersze transportowe
                    result.Wiersze = ParseTransportRows(rows, stringTable);
                    result.Success = true;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Błąd parsowania Excel: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Pobiera wartość komórki jako string
        /// </summary>
        private string GetCellValue(Cell cell, SharedStringTablePart stringTable)
        {
            if (cell == null || cell.CellValue == null)
                return "";

            string value = cell.CellValue.InnerText;

            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                if (stringTable != null)
                {
                    int index = int.Parse(value);
                    return stringTable.SharedStringTable.ElementAt(index).InnerText;
                }
            }

            return value;
        }

        /// <summary>
        /// Pobiera wartość komórki z wiersza po indeksie kolumny (0-based)
        /// </summary>
        private string GetCellValueByIndex(Row row, int columnIndex, SharedStringTablePart stringTable)
        {
            string columnName = GetColumnName(columnIndex);
            uint rowIndex = row.RowIndex;
            string cellReference = columnName + rowIndex;

            Cell cell = row.Elements<Cell>().FirstOrDefault(c => c.CellReference == cellReference);
            return GetCellValue(cell, stringTable);
        }

        /// <summary>
        /// Pobiera wartość komórki z wiersza po nazwie kolumny (A, B, C, ...)
        /// </summary>
        private string GetCellValueByColumn(Row row, string columnName, SharedStringTablePart stringTable)
        {
            if (row == null) return "";
            uint rowIndex = row.RowIndex;
            string cellReference = columnName + rowIndex;

            Cell cell = row.Elements<Cell>().FirstOrDefault(c => c.CellReference == cellReference);
            return GetCellValue(cell, stringTable);
        }

        /// <summary>
        /// Konwertuje indeks kolumny (0-based) na nazwę (A, B, ..., Z, AA, AB, ...)
        /// </summary>
        private string GetColumnName(int columnIndex)
        {
            string columnName = "";
            int temp = columnIndex;

            while (temp >= 0)
            {
                columnName = (char)('A' + (temp % 26)) + columnName;
                temp = temp / 26 - 1;
            }

            return columnName;
        }

        /// <summary>
        /// Pobiera indeks kolumny z nazwy (A=0, B=1, ..., Z=25, AA=26, ...)
        /// </summary>
        private int GetColumnIndex(string columnName)
        {
            int index = 0;
            foreach (char c in columnName.ToUpper())
            {
                index = index * 26 + (c - 'A' + 1);
            }
            return index - 1;
        }

        /// <summary>
        /// Wyciąga datę uboju z nagłówka Excel
        /// </summary>
        private DateTime? ExtractDataUboju(List<Row> rows, SharedStringTablePart stringTable)
        {
            // Szukaj w pierwszych 10 wierszach
            for (int i = 0; i < Math.Min(10, rows.Count); i++)
            {
                Row row = rows[i];
                foreach (Cell cell in row.Elements<Cell>())
                {
                    string value = GetCellValue(cell, stringTable);
                    if (string.IsNullOrEmpty(value)) continue;

                    // Szukamy: "DATA UBOJU : poniedziałek 15 grudzień 2025"
                    var match = Regex.Match(value, @"DATA UBOJU\s*:\s*\w+\s+(\d{1,2})\s+(\w+)\s+(\d{4})", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        try
                        {
                            int day = int.Parse(match.Groups[1].Value);
                            string monthName = match.Groups[2].Value.ToLower();
                            int year = int.Parse(match.Groups[3].Value);
                            int month = ParsePolishMonth(monthName);
                            if (month > 0)
                            {
                                return new DateTime(year, month, day);
                            }
                        }
                        catch { }
                    }

                    // Alternatywny format: szukaj daty w formacie dd.MM.yyyy w nagłówku
                    var dateMatch = Regex.Match(value, @"(\d{2})\.(\d{2})\.(\d{4})");
                    if (dateMatch.Success)
                    {
                        try
                        {
                            int day = int.Parse(dateMatch.Groups[1].Value);
                            int month = int.Parse(dateMatch.Groups[2].Value);
                            int year = int.Parse(dateMatch.Groups[3].Value);
                            return new DateTime(year, month, day);
                        }
                        catch { }
                    }
                }
            }

            return null;
        }

        private int ParsePolishMonth(string monthName)
        {
            var months = new Dictionary<string, int>
            {
                {"styczeń", 1}, {"stycznia", 1}, {"styczen", 1},
                {"luty", 2}, {"lutego", 2},
                {"marzec", 3}, {"marca", 3},
                {"kwiecień", 4}, {"kwietnia", 4}, {"kwiecien", 4},
                {"maj", 5}, {"maja", 5},
                {"czerwiec", 6}, {"czerwca", 6},
                {"lipiec", 7}, {"lipca", 7},
                {"sierpień", 8}, {"sierpnia", 8}, {"sierpien", 8},
                {"wrzesień", 9}, {"września", 9}, {"wrzesien", 9},
                {"październik", 10}, {"października", 10}, {"pazdziernik", 10},
                {"listopad", 11}, {"listopada", 11},
                {"grudzień", 12}, {"grudnia", 12}, {"grudzien", 12}
            };

            monthName = monthName.ToLower().Trim();
            return months.ContainsKey(monthName) ? months[monthName] : 0;
        }

        /// <summary>
        /// Parsuje wiersze transportowe z Excel
        /// </summary>
        private List<AvilogTransportRow> ParseTransportRows(List<Row> rows, SharedStringTablePart stringTable)
        {
            var transportRows = new List<AvilogTransportRow>();

            // Znajdź indeksy wierszy gdzie zaczynają się nowe bloki kierowców
            // Kierowca zaczyna się gdy kolumna A ma nazwisko (WIELKIE LITERY, bez cyfr)
            var driverBlockStarts = new List<int>();

            for (int i = 8; i < rows.Count; i++) // Zacznij od wiersza 9 (po nagłówkach)
            {
                Row row = rows[i];
                string colA = GetCellValueByColumn(row, "A", stringTable)?.Trim() ?? "";

                // Sprawdź czy to początek bloku kierowcy
                // Kierowca ma nazwisko wielkimi literami (np. "MICHALAK", "Knapkiewicz")
                if (!string.IsNullOrEmpty(colA) &&
                    !Regex.IsMatch(colA, @"^\d") && // Nie zaczyna się od cyfry
                    !colA.Contains("SUMA") &&
                    !colA.Contains("KIEROWCA") &&
                    colA.Length >= 3 &&
                    Regex.IsMatch(colA, @"^[A-ZŻŹĆĄŚĘŁÓŃa-zżźćąśęłóń]+$")) // Tylko litery
                {
                    // Sprawdź czy w tym samym wierszu jest też hodowca (kolumna C) lub pojazd
                    string colC = GetCellValueByColumn(row, "C", stringTable)?.Trim() ?? "";
                    string colK = GetCellValueByColumn(row, "K", stringTable)?.Trim() ?? "";
                    string colL = GetCellValueByColumn(row, "L", stringTable)?.Trim() ?? "";

                    // Jeśli kolumna C ma tekst lub kolumna K/L ma rejestrację - to blok kierowcy
                    bool hasHodowca = !string.IsNullOrEmpty(colC) && colC.Length > 2;
                    bool hasVehicle = Regex.IsMatch(colK, @"[A-Z]{2,3}\d") || Regex.IsMatch(colL, @"[A-Z]{2,3}\d");

                    if (hasHodowca || hasVehicle)
                    {
                        driverBlockStarts.Add(i);
                    }
                }
            }

            // Parsuj każdy blok kierowcy
            for (int blockIdx = 0; blockIdx < driverBlockStarts.Count; blockIdx++)
            {
                int startRow = driverBlockStarts[blockIdx];
                int endRow = blockIdx < driverBlockStarts.Count - 1
                    ? driverBlockStarts[blockIdx + 1] - 1
                    : Math.Min(startRow + 20, rows.Count - 1);

                var transport = ParseDriverBlock(rows, startRow, endRow, stringTable);
                if (transport != null && (!string.IsNullOrEmpty(transport.Ciagnik) || !string.IsNullOrEmpty(transport.HodowcaNazwa)))
                {
                    transportRows.Add(transport);
                }
            }

            return transportRows;
        }

        /// <summary>
        /// Parsuje blok kierowcy (wiele wierszy Excel dla jednego transportu)
        /// </summary>
        private AvilogTransportRow ParseDriverBlock(List<Row> rows, int startRowIdx, int endRowIdx, SharedStringTablePart stringTable)
        {
            var transport = new AvilogTransportRow();

            try
            {
                // Zbierz wszystkie dane z bloku
                Row firstRow = rows[startRowIdx];

                // ========== KIEROWCA ==========
                // Wiersz 1: Nazwisko w kolumnie A
                string kierowcaNazwisko = GetCellValueByColumn(firstRow, "A", stringTable)?.Trim() ?? "";

                // Wiersz 2: Imię w kolumnie A
                string kierowcaImie = "";
                if (startRowIdx + 1 < rows.Count)
                {
                    Row secondRow = rows[startRowIdx + 1];
                    kierowcaImie = GetCellValueByColumn(secondRow, "A", stringTable)?.Trim() ?? "";

                    // Jeśli to nie imię (np. jest to telefon lub coś innego), zignoruj
                    if (Regex.IsMatch(kierowcaImie, @"\d{3}"))
                    {
                        kierowcaImie = "";
                    }
                }

                transport.KierowcaNazwa = $"{kierowcaNazwisko} {kierowcaImie}".Trim();

                // Telefon kierowcy - szukaj w kolumnie A wierszy poniżej
                for (int i = startRowIdx + 1; i <= Math.Min(startRowIdx + 4, endRowIdx) && i < rows.Count; i++)
                {
                    string colA = GetCellValueByColumn(rows[i], "A", stringTable)?.Trim() ?? "";
                    var phoneMatch = Regex.Match(colA, @"(\d{3})\s*(\d{3})\s*(\d{3})");
                    if (phoneMatch.Success)
                    {
                        transport.KierowcaTelefon = phoneMatch.Groups[1].Value + phoneMatch.Groups[2].Value + phoneMatch.Groups[3].Value;
                        break;
                    }
                    // Format bez spacji: 9 cyfr
                    var phoneMatch2 = Regex.Match(colA, @"^(\d{9})$");
                    if (phoneMatch2.Success)
                    {
                        transport.KierowcaTelefon = phoneMatch2.Groups[1].Value;
                        break;
                    }
                }

                // ========== HODOWCA ==========
                // Kolumna C lub B - nazwa hodowcy (WIELKIE LITERY)
                string hodowcaNazwa = GetCellValueByColumn(firstRow, "C", stringTable)?.Trim() ?? "";
                if (string.IsNullOrEmpty(hodowcaNazwa))
                {
                    hodowcaNazwa = GetCellValueByColumn(firstRow, "B", stringTable)?.Trim() ?? "";
                }
                transport.HodowcaNazwa = hodowcaNazwa;

                // Kolumna D - adres
                string adres = GetCellValueByColumn(firstRow, "D", stringTable)?.Trim() ?? "";
                if (startRowIdx + 1 < rows.Count)
                {
                    string adres2 = GetCellValueByColumn(rows[startRowIdx + 1], "D", stringTable)?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(adres2) && !Regex.IsMatch(adres2, @"\d{2}\.\d+"))
                    {
                        adres = $"{adres} {adres2}".Trim();
                    }
                }
                transport.HodowcaAdres = adres;

                // Kolumna E lub F - współrzędne GPS
                string gps = GetCellValueByColumn(firstRow, "E", stringTable)?.Trim() ?? "";
                if (string.IsNullOrEmpty(gps))
                {
                    gps = GetCellValueByColumn(firstRow, "F", stringTable)?.Trim() ?? "";
                }
                var gpsMatch = Regex.Match(gps, @"(\d{2}[.,]\d{4,})[,\s]+(\d{2}[.,]\d{4,})");
                if (gpsMatch.Success)
                {
                    transport.HodowcaGpsLat = gpsMatch.Groups[1].Value.Replace(",", ".");
                    transport.HodowcaGpsLon = gpsMatch.Groups[2].Value.Replace(",", ".");
                }

                // Szukaj telefonu hodowcy - "Tel. :" lub "Tel.:"
                for (int i = startRowIdx; i <= endRowIdx && i < rows.Count; i++)
                {
                    Row row = rows[i];
                    // Szukaj w kolumnach H-J
                    for (int col = GetColumnIndex("H"); col <= GetColumnIndex("J"); col++)
                    {
                        string cellValue = GetCellValueByIndex(row, col, stringTable)?.Trim() ?? "";
                        var telMatch = Regex.Match(cellValue, @"Tel\.?\s*:?\s*(\d{3})\s*(\d{3})\s*(\d{3})");
                        if (telMatch.Success)
                        {
                            transport.HodowcaTelefon = telMatch.Groups[1].Value + telMatch.Groups[2].Value + telMatch.Groups[3].Value;
                            break;
                        }
                        // Sprawdź czy samo "Tel. :" a numer w następnej komórce
                        if (cellValue.Contains("Tel") && col + 1 <= GetColumnIndex("K"))
                        {
                            string nextCell = GetCellValueByIndex(row, col + 1, stringTable)?.Trim() ?? "";
                            var numMatch = Regex.Match(nextCell, @"(\d{3})\s*(\d{3})\s*(\d{3})");
                            if (numMatch.Success)
                            {
                                transport.HodowcaTelefon = numMatch.Groups[1].Value + numMatch.Groups[2].Value + numMatch.Groups[3].Value;
                                break;
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(transport.HodowcaTelefon)) break;
                }

                // Szukaj kodu pocztowego i miejscowości w bloku
                for (int i = startRowIdx; i <= endRowIdx && i < rows.Count; i++)
                {
                    Row row = rows[i];
                    string colB = GetCellValueByColumn(row, "B", stringTable)?.Trim() ?? "";
                    string colC = GetCellValueByColumn(row, "C", stringTable)?.Trim() ?? "";
                    string colD = GetCellValueByColumn(row, "D", stringTable)?.Trim() ?? "";

                    // Kod pocztowy format: 99-423
                    var kodMatch = Regex.Match(colB, @"(\d{2}-\d{3})");
                    if (kodMatch.Success)
                    {
                        transport.HodowcaKodPocztowy = kodMatch.Groups[1].Value;
                        // Miejscowość może być w tej samej komórce lub następnej
                        string pozostaly = colB.Substring(kodMatch.Index + kodMatch.Length).Trim();
                        if (!string.IsNullOrEmpty(pozostaly))
                        {
                            transport.HodowcaMiejscowosc = pozostaly;
                        }
                        else if (!string.IsNullOrEmpty(colC) && !Regex.IsMatch(colC, @"\d"))
                        {
                            transport.HodowcaMiejscowosc = colC;
                        }
                        break;
                    }

                    // Może być też w kolumnie C lub D
                    kodMatch = Regex.Match(colC, @"(\d{2}-\d{3})");
                    if (kodMatch.Success)
                    {
                        transport.HodowcaKodPocztowy = kodMatch.Groups[1].Value;
                        string pozostaly = colC.Substring(kodMatch.Index + kodMatch.Length).Trim();
                        if (!string.IsNullOrEmpty(pozostaly))
                        {
                            transport.HodowcaMiejscowosc = pozostaly;
                        }
                        else if (!string.IsNullOrEmpty(colD) && !Regex.IsMatch(colD, @"\d"))
                        {
                            transport.HodowcaMiejscowosc = colD;
                        }
                        break;
                    }
                }

                // ========== POJAZD ==========
                // Szukaj w pierwszych wierszach bloku
                for (int i = startRowIdx; i <= Math.Min(startRowIdx + 3, endRowIdx) && i < rows.Count; i++)
                {
                    Row row = rows[i];

                    // Szukaj "C:" i numeru rejestracyjnego ciągnika
                    for (int col = GetColumnIndex("J"); col <= GetColumnIndex("O"); col++)
                    {
                        string cellValue = GetCellValueByIndex(row, col, stringTable)?.Trim() ?? "";

                        // Ciągnik - po "C:" lub bezpośrednio numer
                        if (cellValue == "C:" && string.IsNullOrEmpty(transport.Ciagnik))
                        {
                            // Numer jest w następnej komórce
                            string nextCell = GetCellValueByIndex(row, col + 1, stringTable)?.Trim() ?? "";
                            if (IsRegistrationNumber(nextCell))
                            {
                                transport.Ciagnik = nextCell;
                            }
                        }
                        else if (string.IsNullOrEmpty(transport.Ciagnik) && IsRegistrationNumber(cellValue))
                        {
                            transport.Ciagnik = cellValue;
                        }

                        // Naczepa - po "N:"
                        if (cellValue == "N:" && string.IsNullOrEmpty(transport.Naczepa))
                        {
                            string nextCell = GetCellValueByIndex(row, col + 1, stringTable)?.Trim() ?? "";
                            if (IsRegistrationNumber(nextCell))
                            {
                                transport.Naczepa = nextCell;
                            }
                        }
                    }

                    // Sprawdź też kolumny L, M, N dla rejestracji
                    string colL = GetCellValueByColumn(row, "L", stringTable)?.Trim() ?? "";
                    string colM = GetCellValueByColumn(row, "M", stringTable)?.Trim() ?? "";
                    string colN = GetCellValueByColumn(row, "N", stringTable)?.Trim() ?? "";

                    if (string.IsNullOrEmpty(transport.Ciagnik) && IsRegistrationNumber(colL))
                    {
                        transport.Ciagnik = colL;
                    }
                    if (string.IsNullOrEmpty(transport.Naczepa))
                    {
                        if (IsRegistrationNumber(colM))
                            transport.Naczepa = colM;
                        else if (IsRegistrationNumber(colN))
                            transport.Naczepa = colN;
                    }
                }

                // ========== ILOŚĆ SZTUK ==========
                for (int i = startRowIdx; i <= Math.Min(startRowIdx + 3, endRowIdx) && i < rows.Count; i++)
                {
                    Row row = rows[i];

                    // Szukaj w kolumnach N-P (ILOŚĆ)
                    for (int col = GetColumnIndex("N"); col <= GetColumnIndex("Q"); col++)
                    {
                        string cellValue = GetCellValueByIndex(row, col, stringTable)?.Trim() ?? "";

                        // Szukaj liczby w formacie "4 488" lub "4488"
                        var sztukiMatch = Regex.Match(cellValue, @"(\d)\s?(\d{3})(?:\s|$)");
                        if (sztukiMatch.Success)
                        {
                            string sztuki = sztukiMatch.Groups[1].Value + sztukiMatch.Groups[2].Value;
                            if (int.TryParse(sztuki, out int szt) && szt >= 1000 && szt <= 20000)
                            {
                                transport.Sztuki = szt;
                                break;
                            }
                        }
                    }
                    if (transport.Sztuki > 0) break;
                }

                // ========== WAGA ==========
                for (int i = startRowIdx; i <= endRowIdx && i < rows.Count; i++)
                {
                    Row row = rows[i];
                    // Szukaj "X.XX Kg" w dowolnej kolumnie bloku
                    for (int col = GetColumnIndex("L"); col <= GetColumnIndex("T"); col++)
                    {
                        string cellValue = GetCellValueByIndex(row, col, stringTable)?.Trim() ?? "";
                        var wagaMatch = Regex.Match(cellValue, @"(\d+[.,]\d+)\s*Kg", RegexOptions.IgnoreCase);
                        if (wagaMatch.Success)
                        {
                            string waga = wagaMatch.Groups[1].Value.Replace(",", ".");
                            if (decimal.TryParse(waga, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal w) && w > 0 && w < 10)
                            {
                                transport.WagaDek = w;
                                break;
                            }
                        }
                    }
                    if (transport.WagaDek > 0) break;
                }

                // ========== WYMIAR SKRZYŃ ==========
                for (int i = startRowIdx; i <= Math.Min(startRowIdx + 3, endRowIdx) && i < rows.Count; i++)
                {
                    Row row = rows[i];
                    for (int col = GetColumnIndex("N"); col <= GetColumnIndex("S"); col++)
                    {
                        string cellValue = GetCellValueByIndex(row, col, stringTable)?.Trim() ?? "";
                        var wymiaryMatch = Regex.Match(cellValue, @"(\d+)\s*x\s*(\d+)");
                        if (wymiaryMatch.Success)
                        {
                            transport.WymiarSkrzyn = $"{wymiaryMatch.Groups[1].Value} x {wymiaryMatch.Groups[2].Value}";
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(transport.WymiarSkrzyn)) break;
                }

                // ========== GODZINY (WYJAZD, ZAŁADUNEK, POWRÓT) ==========
                List<TimeSpan> foundTimes = new List<TimeSpan>();
                List<DateTime> foundDateTimes = new List<DateTime>();

                for (int i = startRowIdx; i <= Math.Min(startRowIdx + 5, endRowIdx) && i < rows.Count; i++)
                {
                    Row row = rows[i];

                    // Szukaj godzin w kolumnach P-V
                    for (int col = GetColumnIndex("P"); col <= GetColumnIndex("W"); col++)
                    {
                        string cellValue = GetCellValueByIndex(row, col, stringTable)?.Trim() ?? "";

                        // Format: "HH:mm" lub "HH:mm:"
                        var timeMatch = Regex.Match(cellValue, @"^(\d{2}):(\d{2}):?$");
                        if (timeMatch.Success)
                        {
                            int h = int.Parse(timeMatch.Groups[1].Value);
                            int m = int.Parse(timeMatch.Groups[2].Value);
                            if (h >= 0 && h < 24 && m >= 0 && m < 60)
                            {
                                foundTimes.Add(new TimeSpan(h, m, 0));
                            }
                        }

                        // Format: "HH:mm: DD.MM.YYYY" (powrót z datą)
                        var dateTimeMatch = Regex.Match(cellValue, @"(\d{2}):(\d{2}):\s*(\d{2})\.(\d{2})\.(\d{4})");
                        if (dateTimeMatch.Success)
                        {
                            try
                            {
                                int h = int.Parse(dateTimeMatch.Groups[1].Value);
                                int mi = int.Parse(dateTimeMatch.Groups[2].Value);
                                int d = int.Parse(dateTimeMatch.Groups[3].Value);
                                int mo = int.Parse(dateTimeMatch.Groups[4].Value);
                                int y = int.Parse(dateTimeMatch.Groups[5].Value);
                                foundDateTimes.Add(new DateTime(y, mo, d, h, mi, 0));
                            }
                            catch { }
                        }
                    }
                }

                // Przypisz godziny: pierwsza = wyjazd, druga = początek załadunku, trzecia/datetime = powrót
                if (foundTimes.Count >= 1)
                {
                    transport.WyjazdZaklad = DateTime.Today.Add(foundTimes[0]);
                }
                if (foundTimes.Count >= 2)
                {
                    transport.PoczatekZaladunku = foundTimes[1];
                }
                if (foundDateTimes.Count > 0)
                {
                    transport.PowrotZaklad = foundDateTimes[0];
                }
                else if (foundTimes.Count >= 3)
                {
                    transport.PowrotZaklad = DateTime.Today.Add(foundTimes[2]);
                }

                // ========== OBSERWACJE (WÓZEK) ==========
                for (int i = startRowIdx; i <= endRowIdx && i < rows.Count; i++)
                {
                    Row row = rows[i];

                    // Szukaj w kolumnach V-W
                    for (int col = GetColumnIndex("V"); col <= GetColumnIndex("X"); col++)
                    {
                        string cellValue = GetCellValueByIndex(row, col, stringTable)?.Trim() ?? "";

                        if (string.IsNullOrEmpty(cellValue) || cellValue.Length < 5) continue;

                        // Sprawdź obserwacje o wózku
                        if (cellValue.Contains("Wózek w obie strony") || cellValue.Contains("Wozek w obie strony"))
                        {
                            transport.Obserwacje = "Wózek w obie strony";
                            break;
                        }
                        else if (cellValue.Contains("Wieziesz wózek") || cellValue.Contains("Wieziesz wozek"))
                        {
                            transport.Obserwacje = "Wieziesz wózek";
                            break;
                        }
                        else if (cellValue.Contains("Zabierasz wózek") || cellValue.Contains("Zabierasz wozek"))
                        {
                            transport.Obserwacje = "Zabierasz wózek";
                            break;
                        }
                        else if (cellValue.Contains("Przywozisz wózek") || cellValue.Contains("Przywozisz wozek"))
                        {
                            transport.Obserwacje = "Przywozisz wózek";
                            break;
                        }
                        else if (!string.IsNullOrEmpty(cellValue) && !cellValue.StartsWith("Rdv"))
                        {
                            // Inne obserwacje
                            transport.Obserwacje = cellValue;
                        }
                    }
                    if (!string.IsNullOrEmpty(transport.Obserwacje)) break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd parsowania bloku kierowcy: {ex.Message}");
            }

            return transport;
        }

        /// <summary>
        /// Sprawdza czy string wygląda jak numer rejestracyjny
        /// </summary>
        private bool IsRegistrationNumber(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            value = value.Trim();

            // Polski numer rejestracyjny: 2-3 litery + cyfry + opcjonalnie litery
            // Przykłady: WPR6903T, WOT51407, WOT46L9, SK12345
            return value.Length >= 6 && value.Length <= 9 &&
                   Regex.IsMatch(value, @"^[A-Z]{2,3}\d[0-9A-Z]{3,5}$");
        }
    }
}
