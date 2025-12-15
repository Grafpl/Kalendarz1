using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Kalendarz1
{
    /// <summary>
    /// Parser plików Excel (.xls i .xlsx) z planowaniem transportu od AVILOG
    /// </summary>
    public class AvilogExcelParser
    {
        static AvilogExcelParser()
        {
            // Wymagane dla ExcelDataReader - rejestracja kodowania dla starszych plików .xls
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// Parsuje plik Excel z AVILOG i zwraca listę wierszy transportowych
        /// </summary>
        public AvilogParseResult ParseExcel(string filePath)
        {
            var result = new AvilogParseResult();

            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        // Konwertuj do DataSet
                        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                            {
                                UseHeaderRow = false // Nie używaj pierwszego wiersza jako nagłówka
                            }
                        });

                        if (dataSet.Tables.Count == 0)
                        {
                            result.Success = false;
                            result.ErrorMessage = "Plik Excel nie zawiera żadnych arkuszy.";
                            return result;
                        }

                        // Weź pierwszy arkusz
                        DataTable table = dataSet.Tables[0];

                        // Wyciągnij datę uboju z nagłówka
                        result.DataUboju = ExtractDataUboju(table);

                        // Parsuj wiersze transportowe
                        result.Wiersze = ParseTransportRows(table);
                        result.Success = true;
                    }
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
        private string GetCellValue(DataRow row, int columnIndex)
        {
            if (row == null || columnIndex < 0 || columnIndex >= row.Table.Columns.Count)
                return "";

            var value = row[columnIndex];
            if (value == null || value == DBNull.Value)
                return "";

            return value.ToString()?.Trim() ?? "";
        }

        /// <summary>
        /// Wyciąga datę uboju z nagłówka Excel
        /// </summary>
        private DateTime? ExtractDataUboju(DataTable table)
        {
            // Szukaj w pierwszych 10 wierszach
            for (int i = 0; i < Math.Min(10, table.Rows.Count); i++)
            {
                DataRow row = table.Rows[i];

                for (int col = 0; col < table.Columns.Count; col++)
                {
                    string value = GetCellValue(row, col);
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
        private List<AvilogTransportRow> ParseTransportRows(DataTable table)
        {
            var transportRows = new List<AvilogTransportRow>();

            // Znajdź indeksy wierszy gdzie zaczynają się nowe bloki kierowców
            var driverBlockStarts = new List<int>();

            for (int i = 8; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                string colA = GetCellValue(row, 0); // Kolumna A

                // Sprawdź czy to początek bloku kierowcy
                if (!string.IsNullOrEmpty(colA) &&
                    !Regex.IsMatch(colA, @"^\d") && // Nie zaczyna się od cyfry
                    !colA.ToUpper().Contains("SUMA") &&
                    !colA.ToUpper().Contains("KIEROWCA") &&
                    colA.Length >= 3 &&
                    Regex.IsMatch(colA, @"^[A-ZŻŹĆĄŚĘŁÓŃa-zżźćąśęłóń]+$"))
                {
                    // Sprawdź czy w tym samym wierszu jest też hodowca lub pojazd
                    string colC = GetCellValue(row, 2); // Kolumna C

                    // Szukaj numeru rejestracyjnego w wierszu
                    bool hasVehicle = false;
                    for (int col = 10; col < Math.Min(20, table.Columns.Count); col++)
                    {
                        string cellValue = GetCellValue(row, col);
                        if (IsRegistrationNumber(cellValue))
                        {
                            hasVehicle = true;
                            break;
                        }
                    }

                    bool hasHodowca = !string.IsNullOrEmpty(colC) && colC.Length > 2 &&
                                      !Regex.IsMatch(colC, @"^\d") &&
                                      Regex.IsMatch(colC, @"[A-ZŻŹĆĄŚĘŁÓŃ]");

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
                    : Math.Min(startRow + 20, table.Rows.Count - 1);

                var transport = ParseDriverBlock(table, startRow, endRow);
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
        private AvilogTransportRow ParseDriverBlock(DataTable table, int startRowIdx, int endRowIdx)
        {
            var transport = new AvilogTransportRow();

            try
            {
                DataRow firstRow = table.Rows[startRowIdx];

                // ========== KIEROWCA ==========
                string kierowcaNazwisko = GetCellValue(firstRow, 0); // Kolumna A

                // Wiersz 2: Imię w kolumnie A
                string kierowcaImie = "";
                if (startRowIdx + 1 < table.Rows.Count)
                {
                    kierowcaImie = GetCellValue(table.Rows[startRowIdx + 1], 0);
                    if (Regex.IsMatch(kierowcaImie, @"\d{3}"))
                    {
                        kierowcaImie = "";
                    }
                }

                transport.KierowcaNazwa = $"{kierowcaNazwisko} {kierowcaImie}".Trim();

                // Telefon kierowcy
                for (int i = startRowIdx + 1; i <= Math.Min(startRowIdx + 4, endRowIdx) && i < table.Rows.Count; i++)
                {
                    string colA = GetCellValue(table.Rows[i], 0);
                    var phoneMatch = Regex.Match(colA, @"(\d{3})\s*(\d{3})\s*(\d{3})");
                    if (phoneMatch.Success)
                    {
                        transport.KierowcaTelefon = phoneMatch.Groups[1].Value + phoneMatch.Groups[2].Value + phoneMatch.Groups[3].Value;
                        break;
                    }
                    var phoneMatch2 = Regex.Match(colA, @"^(\d{9})$");
                    if (phoneMatch2.Success)
                    {
                        transport.KierowcaTelefon = phoneMatch2.Groups[1].Value;
                        break;
                    }
                }

                // ========== HODOWCA ==========
                // Kolumna C lub B
                string hodowcaNazwa = GetCellValue(firstRow, 2); // C
                if (string.IsNullOrEmpty(hodowcaNazwa))
                {
                    hodowcaNazwa = GetCellValue(firstRow, 1); // B
                }
                transport.HodowcaNazwa = hodowcaNazwa;

                // Kolumna D - adres
                string adres = GetCellValue(firstRow, 3); // D
                if (startRowIdx + 1 < table.Rows.Count)
                {
                    string adres2 = GetCellValue(table.Rows[startRowIdx + 1], 3);
                    if (!string.IsNullOrEmpty(adres2) && !Regex.IsMatch(adres2, @"\d{2}\.\d+"))
                    {
                        adres = $"{adres} {adres2}".Trim();
                    }
                }
                transport.HodowcaAdres = adres;

                // Kolumna E lub F - GPS
                string gps = GetCellValue(firstRow, 4); // E
                if (string.IsNullOrEmpty(gps))
                {
                    gps = GetCellValue(firstRow, 5); // F
                }
                var gpsMatch = Regex.Match(gps, @"(\d{2}[.,]\d{4,})[,\s]+(\d{2}[.,]\d{4,})");
                if (gpsMatch.Success)
                {
                    transport.HodowcaGpsLat = gpsMatch.Groups[1].Value.Replace(",", ".");
                    transport.HodowcaGpsLon = gpsMatch.Groups[2].Value.Replace(",", ".");
                }

                // Telefon hodowcy - szukaj "Tel. :" lub "Tel.:"
                for (int i = startRowIdx; i <= endRowIdx && i < table.Rows.Count; i++)
                {
                    DataRow row = table.Rows[i];
                    for (int col = 7; col <= Math.Min(12, table.Columns.Count - 1); col++) // H-L
                    {
                        string cellValue = GetCellValue(row, col);
                        var telMatch = Regex.Match(cellValue, @"Tel\.?\s*:?\s*(\d{3})\s*(\d{3})\s*(\d{3})");
                        if (telMatch.Success)
                        {
                            transport.HodowcaTelefon = telMatch.Groups[1].Value + telMatch.Groups[2].Value + telMatch.Groups[3].Value;
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(transport.HodowcaTelefon)) break;
                }

                // Kod pocztowy i miejscowość
                for (int i = startRowIdx; i <= endRowIdx && i < table.Rows.Count; i++)
                {
                    DataRow row = table.Rows[i];
                    for (int col = 1; col <= 3; col++) // B-D
                    {
                        string cellValue = GetCellValue(row, col);
                        var kodMatch = Regex.Match(cellValue, @"(\d{2}-\d{3})");
                        if (kodMatch.Success)
                        {
                            transport.HodowcaKodPocztowy = kodMatch.Groups[1].Value;
                            string pozostaly = cellValue.Substring(kodMatch.Index + kodMatch.Length).Trim();
                            if (!string.IsNullOrEmpty(pozostaly))
                            {
                                transport.HodowcaMiejscowosc = pozostaly;
                            }
                            else
                            {
                                // Miejscowość może być w następnej kolumnie
                                string nextCol = GetCellValue(row, col + 1);
                                if (!string.IsNullOrEmpty(nextCol) && !Regex.IsMatch(nextCol, @"\d"))
                                {
                                    transport.HodowcaMiejscowosc = nextCol;
                                }
                            }
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(transport.HodowcaKodPocztowy)) break;
                }

                // ========== POJAZD ==========
                for (int i = startRowIdx; i <= Math.Min(startRowIdx + 3, endRowIdx) && i < table.Rows.Count; i++)
                {
                    DataRow row = table.Rows[i];

                    for (int col = 10; col <= Math.Min(18, table.Columns.Count - 1); col++) // K-R
                    {
                        string cellValue = GetCellValue(row, col);

                        // Ciągnik - po "C:" lub bezpośrednio numer
                        if (cellValue == "C:" && string.IsNullOrEmpty(transport.Ciagnik))
                        {
                            string nextCell = GetCellValue(row, col + 1);
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
                            string nextCell = GetCellValue(row, col + 1);
                            if (IsRegistrationNumber(nextCell))
                            {
                                transport.Naczepa = nextCell;
                            }
                        }
                        else if (!string.IsNullOrEmpty(transport.Ciagnik) &&
                                 string.IsNullOrEmpty(transport.Naczepa) &&
                                 IsRegistrationNumber(cellValue) &&
                                 cellValue != transport.Ciagnik)
                        {
                            transport.Naczepa = cellValue;
                        }
                    }
                }

                // ========== ILOŚĆ SZTUK ==========
                for (int i = startRowIdx; i <= Math.Min(startRowIdx + 3, endRowIdx) && i < table.Rows.Count; i++)
                {
                    DataRow row = table.Rows[i];

                    for (int col = 13; col <= Math.Min(18, table.Columns.Count - 1); col++) // N-R
                    {
                        string cellValue = GetCellValue(row, col);

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
                for (int i = startRowIdx; i <= endRowIdx && i < table.Rows.Count; i++)
                {
                    DataRow row = table.Rows[i];
                    for (int col = 11; col <= Math.Min(20, table.Columns.Count - 1); col++)
                    {
                        string cellValue = GetCellValue(row, col);
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
                for (int i = startRowIdx; i <= Math.Min(startRowIdx + 3, endRowIdx) && i < table.Rows.Count; i++)
                {
                    DataRow row = table.Rows[i];
                    for (int col = 13; col <= Math.Min(20, table.Columns.Count - 1); col++)
                    {
                        string cellValue = GetCellValue(row, col);
                        var wymiaryMatch = Regex.Match(cellValue, @"(\d+)\s*x\s*(\d+)");
                        if (wymiaryMatch.Success)
                        {
                            transport.WymiarSkrzyn = $"{wymiaryMatch.Groups[1].Value} x {wymiaryMatch.Groups[2].Value}";
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(transport.WymiarSkrzyn)) break;
                }

                // ========== GODZINY ==========
                List<TimeSpan> foundTimes = new List<TimeSpan>();
                List<DateTime> foundDateTimes = new List<DateTime>();

                for (int i = startRowIdx; i <= Math.Min(startRowIdx + 5, endRowIdx) && i < table.Rows.Count; i++)
                {
                    DataRow row = table.Rows[i];

                    for (int col = 15; col <= Math.Min(24, table.Columns.Count - 1); col++) // P-X
                    {
                        string cellValue = GetCellValue(row, col);

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

                        // Format: "HH:mm: DD.MM.YYYY"
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

                // ========== OBSERWACJE ==========
                for (int i = startRowIdx; i <= endRowIdx && i < table.Rows.Count; i++)
                {
                    DataRow row = table.Rows[i];

                    for (int col = 21; col <= Math.Min(26, table.Columns.Count - 1); col++) // V-Z
                    {
                        string cellValue = GetCellValue(row, col);

                        if (string.IsNullOrEmpty(cellValue) || cellValue.Length < 5) continue;

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
            return value.Length >= 6 && value.Length <= 9 &&
                   Regex.IsMatch(value, @"^[A-Z]{2,3}\d[0-9A-Z]{3,5}$");
        }
    }
}
