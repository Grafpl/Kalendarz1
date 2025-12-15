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
                        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                            {
                                UseHeaderRow = false
                            }
                        });

                        if (dataSet.Tables.Count == 0)
                        {
                            result.Success = false;
                            result.ErrorMessage = "Plik Excel nie zawiera żadnych arkuszy.";
                            return result;
                        }

                        DataTable table = dataSet.Tables[0];

                        // Wyciągnij datę uboju
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
        /// Szuka tekstu we wszystkich kolumnach wiersza
        /// </summary>
        private string FindInRow(DataRow row, string pattern, int maxCol = 30)
        {
            for (int col = 0; col < Math.Min(maxCol, row.Table.Columns.Count); col++)
            {
                string val = GetCellValue(row, col);
                if (!string.IsNullOrEmpty(val) && Regex.IsMatch(val, pattern))
                    return val;
            }
            return "";
        }

        /// <summary>
        /// Szuka numeru rejestracyjnego w wierszu
        /// </summary>
        private string FindRegistrationInRow(DataRow row, int startCol = 0, int maxCol = 30)
        {
            for (int col = startCol; col < Math.Min(maxCol, row.Table.Columns.Count); col++)
            {
                string val = GetCellValue(row, col);
                if (IsRegistrationNumber(val))
                    return val;
            }
            return "";
        }

        private DateTime? ExtractDataUboju(DataTable table)
        {
            for (int i = 0; i < Math.Min(10, table.Rows.Count); i++)
            {
                DataRow row = table.Rows[i];
                for (int col = 0; col < Math.Min(20, table.Columns.Count); col++)
                {
                    string value = GetCellValue(row, col);
                    if (string.IsNullOrEmpty(value)) continue;

                    // "DATA UBOJU : poniedziałek 15 grudzień 2025"
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

        private List<AvilogTransportRow> ParseTransportRows(DataTable table)
        {
            var transportRows = new List<AvilogTransportRow>();
            var driverBlockStarts = new List<int>();

            // Znajdź wiersze gdzie zaczyna się nowy transport
            // Szukamy: kolumna A ma nazwisko (tekst bez cyfr), a gdzieś w wierszu jest numer rejestracyjny
            for (int i = 5; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                string colA = GetCellValue(row, 0);

                // Kolumna A powinna mieć nazwisko kierowcy
                if (string.IsNullOrEmpty(colA)) continue;
                if (colA.Length < 3) continue;
                if (Regex.IsMatch(colA, @"\d")) continue; // Nie może zawierać cyfr
                if (colA.ToUpper().Contains("SUMA")) continue;
                if (colA.ToUpper().Contains("KIEROWCA")) continue;
                if (colA.ToUpper().Contains("AVILOG")) continue;
                if (colA.ToUpper().Contains("MOUSSET")) continue;

                // Sprawdź czy gdzieś w wierszu jest numer rejestracyjny (oznacza nowy transport)
                string reg = FindRegistrationInRow(row);
                if (!string.IsNullOrEmpty(reg))
                {
                    driverBlockStarts.Add(i);
                }
            }

            // Parsuj każdy blok
            for (int blockIdx = 0; blockIdx < driverBlockStarts.Count; blockIdx++)
            {
                int startRow = driverBlockStarts[blockIdx];
                int endRow = blockIdx < driverBlockStarts.Count - 1
                    ? driverBlockStarts[blockIdx + 1] - 1
                    : Math.Min(startRow + 15, table.Rows.Count - 1);

                var transport = ParseDriverBlock(table, startRow, endRow);
                if (transport != null)
                {
                    transportRows.Add(transport);
                }
            }

            return transportRows;
        }

        private AvilogTransportRow ParseDriverBlock(DataTable table, int startRowIdx, int endRowIdx)
        {
            var transport = new AvilogTransportRow();

            try
            {
                DataRow row0 = table.Rows[startRowIdx];
                DataRow row1 = startRowIdx + 1 < table.Rows.Count ? table.Rows[startRowIdx + 1] : null;
                DataRow row2 = startRowIdx + 2 < table.Rows.Count ? table.Rows[startRowIdx + 2] : null;

                // ========== KIEROWCA ==========
                // Wiersz 0: Nazwisko w kolumnie A
                // Wiersz 1: Imię w kolumnie A
                // Wiersz 2: Telefon w kolumnie A
                string nazwisko = GetCellValue(row0, 0);
                string imie = row1 != null ? GetCellValue(row1, 0) : "";

                // Sprawdź czy imię nie jest telefonem
                if (Regex.IsMatch(imie, @"\d{3}"))
                {
                    transport.KierowcaTelefon = Regex.Replace(imie, @"\s+", "");
                    imie = "";
                }

                transport.KierowcaNazwa = $"{nazwisko} {imie}".Trim();

                // Szukaj telefonu kierowcy
                if (string.IsNullOrEmpty(transport.KierowcaTelefon))
                {
                    for (int i = startRowIdx + 1; i <= Math.Min(startRowIdx + 3, endRowIdx) && i < table.Rows.Count; i++)
                    {
                        string colA = GetCellValue(table.Rows[i], 0);
                        var phoneMatch = Regex.Match(colA, @"(\d{3})\s*(\d{3})\s*(\d{3})");
                        if (phoneMatch.Success)
                        {
                            transport.KierowcaTelefon = phoneMatch.Groups[1].Value + phoneMatch.Groups[2].Value + phoneMatch.Groups[3].Value;
                            break;
                        }
                    }
                }

                // ========== HODOWCA ==========
                // Kolumna B: Nazwa hodowcy (pierwszy wiersz), potem adres
                string hodowcaNazwa = GetCellValue(row0, 1);
                transport.HodowcaNazwa = hodowcaNazwa;

                // Adres w kolumnie B lub C, wiersz 1
                if (row1 != null)
                {
                    string adres = GetCellValue(row1, 1);
                    if (string.IsNullOrEmpty(adres))
                        adres = GetCellValue(row1, 2);
                    transport.HodowcaAdres = adres;
                }

                // GPS - szukaj formatu "XX.XXXXX XX.XXXXX" lub "XX.XXXXX,XX.XXXXX"
                for (int i = startRowIdx; i <= Math.Min(startRowIdx + 2, endRowIdx) && i < table.Rows.Count; i++)
                {
                    for (int col = 2; col < Math.Min(8, table.Columns.Count); col++)
                    {
                        string val = GetCellValue(table.Rows[i], col);
                        var gpsMatch = Regex.Match(val, @"(\d{2}[.,]\d{4,})\s*[,\s]\s*(\d{2}[.,]\d{4,})");
                        if (gpsMatch.Success)
                        {
                            transport.HodowcaGpsLat = gpsMatch.Groups[1].Value.Replace(",", ".");
                            transport.HodowcaGpsLon = gpsMatch.Groups[2].Value.Replace(",", ".");
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(transport.HodowcaGpsLat)) break;
                }

                // Telefon hodowcy - szukaj "Tel" + numer
                for (int i = startRowIdx; i <= endRowIdx && i < table.Rows.Count; i++)
                {
                    for (int col = 0; col < Math.Min(15, table.Columns.Count); col++)
                    {
                        string val = GetCellValue(table.Rows[i], col);
                        var telMatch = Regex.Match(val, @"Tel[\.:\s]*(\d{3})\s*(\d{3})\s*(\d{3})");
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
                    for (int col = 1; col < Math.Min(5, table.Columns.Count); col++)
                    {
                        string val = GetCellValue(table.Rows[i], col);
                        var kodMatch = Regex.Match(val, @"(\d{2}-\d{3})");
                        if (kodMatch.Success)
                        {
                            transport.HodowcaKodPocztowy = kodMatch.Groups[1].Value;
                            // Miejscowość może być w tej samej komórce lub następnej
                            string pozostaly = val.Substring(kodMatch.Index + kodMatch.Length).Trim();
                            if (!string.IsNullOrEmpty(pozostaly))
                            {
                                transport.HodowcaMiejscowosc = pozostaly;
                            }
                            else
                            {
                                string nextVal = GetCellValue(table.Rows[i], col + 1);
                                if (!string.IsNullOrEmpty(nextVal) && !Regex.IsMatch(nextVal, @"\d"))
                                {
                                    transport.HodowcaMiejscowosc = nextVal;
                                }
                            }
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(transport.HodowcaKodPocztowy)) break;
                }

                // ========== POJAZDY ==========
                // Szukaj numerów rejestracyjnych - pierwszy to ciągnik, drugi to naczepa
                List<string> rejestracje = new List<string>();
                for (int i = startRowIdx; i <= Math.Min(startRowIdx + 2, endRowIdx) && i < table.Rows.Count; i++)
                {
                    for (int col = 0; col < Math.Min(25, table.Columns.Count); col++)
                    {
                        string val = GetCellValue(table.Rows[i], col);
                        if (IsRegistrationNumber(val) && !rejestracje.Contains(val))
                        {
                            rejestracje.Add(val);
                        }
                    }
                }

                if (rejestracje.Count >= 1)
                    transport.Ciagnik = rejestracje[0];
                if (rejestracje.Count >= 2)
                    transport.Naczepa = rejestracje[1];

                // ========== ILOŚĆ SZTUK ==========
                // Szukaj formatu "X XXX" (np. "4 488") lub "XXXX"
                for (int i = startRowIdx; i <= Math.Min(startRowIdx + 2, endRowIdx) && i < table.Rows.Count; i++)
                {
                    for (int col = 0; col < Math.Min(25, table.Columns.Count); col++)
                    {
                        string val = GetCellValue(table.Rows[i], col);
                        // Format "4 488" lub "5 280"
                        var sztukiMatch = Regex.Match(val, @"^(\d)\s(\d{3})$");
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
                    for (int col = 0; col < Math.Min(25, table.Columns.Count); col++)
                    {
                        string val = GetCellValue(table.Rows[i], col);
                        var wagaMatch = Regex.Match(val, @"(\d+[.,]\d+)\s*Kg", RegexOptions.IgnoreCase);
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
                for (int i = startRowIdx; i <= Math.Min(startRowIdx + 2, endRowIdx) && i < table.Rows.Count; i++)
                {
                    for (int col = 0; col < Math.Min(25, table.Columns.Count); col++)
                    {
                        string val = GetCellValue(table.Rows[i], col);
                        var wymiaryMatch = Regex.Match(val, @"(\d+)\s*x\s*(\d+)");
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
                DateTime? powrotDateTime = null;

                for (int i = startRowIdx; i <= endRowIdx && i < table.Rows.Count; i++)
                {
                    for (int col = 0; col < Math.Min(30, table.Columns.Count); col++)
                    {
                        string val = GetCellValue(table.Rows[i], col);

                        // Format: "HH:mm: DD.MM.YYYY" (powrót z datą)
                        var dateTimeMatch = Regex.Match(val, @"(\d{2}):(\d{2}):\s*(\d{2})\.(\d{2})\.(\d{4})");
                        if (dateTimeMatch.Success && powrotDateTime == null)
                        {
                            try
                            {
                                int h = int.Parse(dateTimeMatch.Groups[1].Value);
                                int mi = int.Parse(dateTimeMatch.Groups[2].Value);
                                int d = int.Parse(dateTimeMatch.Groups[3].Value);
                                int mo = int.Parse(dateTimeMatch.Groups[4].Value);
                                int y = int.Parse(dateTimeMatch.Groups[5].Value);
                                powrotDateTime = new DateTime(y, mo, d, h, mi, 0);
                            }
                            catch { }
                            continue;
                        }

                        // Format: "HH:mm" lub "HH:mm:"
                        var timeMatch = Regex.Match(val, @"^(\d{2}):(\d{2}):?$");
                        if (timeMatch.Success)
                        {
                            int h = int.Parse(timeMatch.Groups[1].Value);
                            int m = int.Parse(timeMatch.Groups[2].Value);
                            if (h >= 0 && h < 24 && m >= 0 && m < 60)
                            {
                                foundTimes.Add(new TimeSpan(h, m, 0));
                            }
                        }
                    }
                }

                // Przypisz godziny: pierwsza = wyjazd, druga = załadunek
                if (foundTimes.Count >= 1)
                    transport.WyjazdZaklad = DateTime.Today.Add(foundTimes[0]);
                if (foundTimes.Count >= 2)
                    transport.PoczatekZaladunku = foundTimes[1];
                if (powrotDateTime.HasValue)
                    transport.PowrotZaklad = powrotDateTime;
                else if (foundTimes.Count >= 3)
                    transport.PowrotZaklad = DateTime.Today.Add(foundTimes[2]);

                // ========== OBSERWACJE ==========
                string[] wozekPatterns = {
                    "Wózek w obie strony", "Wozek w obie strony",
                    "Wieziesz wózek", "Wieziesz wozek",
                    "Zabierasz wózek", "Zabierasz wozek",
                    "Przywozisz wózek", "Przywozisz wozek"
                };

                for (int i = startRowIdx; i <= endRowIdx && i < table.Rows.Count; i++)
                {
                    for (int col = 0; col < Math.Min(30, table.Columns.Count); col++)
                    {
                        string val = GetCellValue(table.Rows[i], col);
                        if (string.IsNullOrEmpty(val)) continue;

                        foreach (var pattern in wozekPatterns)
                        {
                            if (val.Contains(pattern))
                            {
                                transport.Obserwacje = pattern.Replace("ozek", "ózek"); // Normalize
                                break;
                            }
                        }
                        if (!string.IsNullOrEmpty(transport.Obserwacje)) break;
                    }
                    if (!string.IsNullOrEmpty(transport.Obserwacje)) break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd parsowania bloku: {ex.Message}");
            }

            return transport;
        }

        private bool IsRegistrationNumber(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            value = value.Trim();

            // Polski numer: 2-3 litery + cyfra + 3-5 znaków alfanumerycznych
            // Przykłady: WPR6903T, WOT51407, WOT46L9, WOT97L4
            return value.Length >= 6 && value.Length <= 9 &&
                   Regex.IsMatch(value, @"^[A-Z]{2,3}\d[0-9A-Z]{3,5}$");
        }
    }
}
