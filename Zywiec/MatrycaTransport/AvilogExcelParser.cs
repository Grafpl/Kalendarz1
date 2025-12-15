using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kalendarz1
{
    /// <summary>
    /// Parser plików Excel (.xls/.xlsx) z planowaniem transportu od AVILOG
    /// Struktura: każdy transport to blok ~17 wierszy z danymi rozrzuconymi po kolumnach
    /// </summary>
    public class AvilogExcelParser
    {
        /// <summary>
        /// Parsuje plik Excel z AVILOG i zwraca listę wierszy transportowych
        /// </summary>
        public AvilogExcelParseResult ParseExcel(string filePath)
        {
            var result = new AvilogExcelParseResult();

            try
            {
                IWorkbook workbook;

                // Otwórz plik Excel (obsługa .xls i .xlsx)
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    string extension = Path.GetExtension(filePath).ToLower();
                    if (extension == ".xlsx")
                    {
                        workbook = new XSSFWorkbook(fs);
                    }
                    else // .xls
                    {
                        workbook = new HSSFWorkbook(fs);
                    }
                }

                ISheet sheet = workbook.GetSheetAt(0);
                if (sheet == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Nie znaleziono arkusza w pliku Excel";
                    return result;
                }

                // Wyciągnij datę uboju z nagłówka
                result.DataUboju = ExtractDataUboju(sheet);

                // Znajdź wszystkie bloki transportowe
                var transportBlocks = FindTransportBlocks(sheet);

                if (transportBlocks.Count == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "Nie znaleziono danych transportowych w pliku Excel.\n" +
                        "Upewnij się, że plik pochodzi z systemu AVILOG.";
                    return result;
                }

                // Parsuj każdy blok
                int lp = 1;
                foreach (int startRow in transportBlocks)
                {
                    var row = ParseTransportBlock(sheet, startRow, lp);
                    if (row != null)
                    {
                        result.Wiersze.Add(row);
                        lp++;
                    }
                }

                result.Success = true;

                // Debug - zapisz log
                try
                {
                    string debugPath = Path.Combine(
                        Path.GetDirectoryName(filePath),
                        "avilog_excel_debug.txt");
                    File.WriteAllText(debugPath,
                        $"Data uboju: {result.DataUboju}\n" +
                        $"Znaleziono bloków: {transportBlocks.Count}\n" +
                        $"Sparsowano wierszy: {result.Wiersze.Count}\n" +
                        $"Bloki w wierszach: {string.Join(", ", transportBlocks)}");
                }
                catch { }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Błąd parsowania Excel: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Wyciąga datę uboju z nagłówka Excela
        /// Szuka tekstu "DATA UBOJU : środa 03 grudzień 2025"
        /// </summary>
        private DateTime? ExtractDataUboju(ISheet sheet)
        {
            // Przeszukaj pierwsze 10 wierszy
            for (int i = 0; i <= Math.Min(10, sheet.LastRowNum); i++)
            {
                IRow row = sheet.GetRow(i);
                if (row == null) continue;

                for (int j = 0; j < row.LastCellNum; j++)
                {
                    ICell cell = row.GetCell(j);
                    if (cell == null) continue;

                    string cellValue = GetCellStringValue(cell);
                    if (string.IsNullOrEmpty(cellValue)) continue;

                    // Szukaj "DATA UBOJU"
                    if (cellValue.Contains("DATA UBOJU"))
                    {
                        // Format: "DATA UBOJU : środa 03 grudzień 2025"
                        var match = Regex.Match(cellValue, @"(\d{1,2})\s+(\w+)\s+(\d{4})");
                        if (match.Success)
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
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Konwertuje polską nazwę miesiąca na numer
        /// </summary>
        private int ParsePolishMonth(string monthName)
        {
            var months = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
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

            return months.ContainsKey(monthName) ? months[monthName] : 0;
        }

        /// <summary>
        /// Znajduje wszystkie wiersze będące początkiem bloku transportowego
        /// Blok zaczyna się od wiersza gdzie kolumna 0 zawiera "Imię Nazwisko\nTelefon"
        /// </summary>
        private List<int> FindTransportBlocks(ISheet sheet)
        {
            var blocks = new List<int>();

            for (int i = 0; i <= sheet.LastRowNum; i++)
            {
                IRow row = sheet.GetRow(i);
                if (row == null) continue;

                ICell cell = row.GetCell(0);
                if (cell == null) continue;

                string value = GetCellStringValue(cell);
                if (string.IsNullOrEmpty(value)) continue;

                // Sprawdź czy to początek bloku:
                // - Zawiera \n (nowa linia)
                // - Zawiera cyfry (telefon)
                // - NIE jest nagłówkiem (KIEROWCA, AVILOG itp.)
                if (value.Contains("\n") &&
                    Regex.IsMatch(value, @"\d{3}") &&
                    !value.ToUpper().Contains("KIEROWCA") &&
                    !value.ToUpper().Contains("AVILOG") &&
                    !value.ToUpper().Contains("HODOWCA"))
                {
                    blocks.Add(i);
                }
            }

            return blocks;
        }

        /// <summary>
        /// Parsuje pojedynczy blok transportowy (17 wierszy)
        /// </summary>
        private ImportExcelRow ParseTransportBlock(ISheet sheet, int startRow, int lp)
        {
            try
            {
                var result = new ImportExcelRow { Lp = lp };

                // ============ WIERSZ +0 (główny) ============
                IRow row0 = sheet.GetRow(startRow);
                if (row0 == null) return null;

                // Kolumna 0: Kierowca + telefon (format: "Imię Nazwisko\nTelefon")
                string kierowcaRaw = GetCellStringValue(row0.GetCell(0));
                if (!string.IsNullOrEmpty(kierowcaRaw) && kierowcaRaw.Contains("\n"))
                {
                    var parts = kierowcaRaw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    result.KierowcaNazwa = parts[0].Trim();
                    if (parts.Length > 1)
                    {
                        result.KierowcaTelefon = CleanPhoneNumber(parts[1]);
                    }
                }

                // Kolumna 2: Nazwa hodowcy
                result.HodowcaNazwa = GetCellStringValue(row0.GetCell(2))?.Trim();

                // Kolumna 8: Telefon hodowcy (format: "Tel. : 123456789...")
                string telHodowcyRaw = GetCellStringValue(row0.GetCell(8));
                if (!string.IsNullOrEmpty(telHodowcyRaw))
                {
                    var telMatch = Regex.Match(telHodowcyRaw, @"Tel\.\s*:\s*([\d\s-]+)");
                    if (telMatch.Success)
                    {
                        result.HodowcaTelefon = CleanPhoneNumber(telMatch.Groups[1].Value);
                    }
                }

                // Kolumna 12: Ciągnik
                result.Ciagnik = GetCellStringValue(row0.GetCell(12))?.Trim();

                // Kolumna 14: Ilość sztuk
                result.Sztuki = GetCellIntValue(row0.GetCell(14));

                // Kolumna 16: Data/godzina wyjazdu
                result.WyjazdZaklad = GetCellDateTimeValue(row0.GetCell(16));

                // ============ WIERSZ +1 ============
                IRow row1 = sheet.GetRow(startRow + 1);
                if (row1 != null)
                {
                    // Kolumna 2: Adres hodowcy
                    result.HodowcaAdres = GetCellStringValue(row1.GetCell(2))?.Trim();

                    // Kolumna 5: GPS
                    result.HodowcaGPS = GetCellStringValue(row1.GetCell(5))?.Trim();

                    // Kolumna 14: Wymiar skrzyń (np. " 21 x 264")
                    string wymiar = GetCellStringValue(row1.GetCell(14))?.Trim();
                    if (!string.IsNullOrEmpty(wymiar))
                    {
                        result.WymiarSkrzyn = wymiar.Trim();
                    }

                    // Kolumna 17: Godzina przyjazdu
                    result.PowrotZaklad = GetCellDateTimeValue(row1.GetCell(17));
                }

                // ============ WIERSZ +2 ============
                IRow row2 = sheet.GetRow(startRow + 2);
                if (row2 != null)
                {
                    // Kolumna 12: Naczepa
                    result.Naczepa = GetCellStringValue(row2.GetCell(12))?.Trim();
                }

                // ============ WIERSZ +8 ============
                IRow row8 = sheet.GetRow(startRow + 8);
                if (row8 != null)
                {
                    // Kolumna 15: Godzina załadunku (datetime)
                    DateTime? zaladunekDt = GetCellDateTimeValue(row8.GetCell(15));
                    if (zaladunekDt.HasValue)
                    {
                        result.GodzinaZaladunku = zaladunekDt.Value.TimeOfDay;
                    }

                    // Kolumna 19: Obserwacje/wózek
                    string obserwacje = GetCellStringValue(row8.GetCell(19))?.Trim();
                    if (!string.IsNullOrEmpty(obserwacje))
                    {
                        result.Obserwacje = obserwacje;
                    }
                }

                // ============ WIERSZ +12 ============
                IRow row12 = sheet.GetRow(startRow + 12);
                if (row12 != null)
                {
                    // Kolumna 2: Kod pocztowy
                    result.HodowcaKodPocztowy = GetCellStringValue(row12.GetCell(2))?.Trim();

                    // Kolumna 3: Miejscowość
                    result.HodowcaMiejscowosc = GetCellStringValue(row12.GetCell(3))?.Trim();

                    // Kolumna 12: Waga deklarowana (format: " 2.25 Kg")
                    string wagaRaw = GetCellStringValue(row12.GetCell(12));
                    if (!string.IsNullOrEmpty(wagaRaw))
                    {
                        result.WagaDek = ParseWaga(wagaRaw);
                    }
                }

                // Walidacja - czy mamy minimalne dane
                if (string.IsNullOrEmpty(result.HodowcaNazwa) && result.Sztuki == 0)
                {
                    return null;
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd parsowania bloku w wierszu {startRow}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Pobiera wartość komórki jako string
        /// </summary>
        private string GetCellStringValue(ICell cell)
        {
            if (cell == null) return null;

            try
            {
                switch (cell.CellType)
                {
                    case CellType.String:
                        return cell.StringCellValue;

                    case CellType.Numeric:
                        if (DateUtil.IsCellDateFormatted(cell))
                        {
                            return cell.DateCellValue.ToString("HH:mm");
                        }
                        return cell.NumericCellValue.ToString();

                    case CellType.Boolean:
                        return cell.BooleanCellValue.ToString();

                    case CellType.Formula:
                        try
                        {
                            return cell.StringCellValue;
                        }
                        catch
                        {
                            return cell.NumericCellValue.ToString();
                        }

                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Pobiera wartość komórki jako int
        /// </summary>
        private int GetCellIntValue(ICell cell)
        {
            if (cell == null) return 0;

            try
            {
                switch (cell.CellType)
                {
                    case CellType.Numeric:
                        return (int)cell.NumericCellValue;

                    case CellType.String:
                        string str = cell.StringCellValue.Replace(" ", "").Replace(",", "");
                        if (int.TryParse(str, out int val))
                            return val;
                        return 0;

                    default:
                        return 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Pobiera wartość komórki jako DateTime
        /// </summary>
        private DateTime? GetCellDateTimeValue(ICell cell)
        {
            if (cell == null) return null;

            try
            {
                switch (cell.CellType)
                {
                    case CellType.Numeric:
                        if (DateUtil.IsCellDateFormatted(cell))
                        {
                            return cell.DateCellValue;
                        }
                        // Może być liczbą reprezentującą czas (np. 0.8333 = 20:00)
                        double numVal = cell.NumericCellValue;
                        if (numVal >= 0 && numVal < 1)
                        {
                            return DateTime.Today.AddDays(numVal);
                        }
                        return null;

                    case CellType.String:
                        string str = cell.StringCellValue.Trim();
                        // Próbuj różne formaty
                        if (DateTime.TryParseExact(str, new[] { "HH:mm", "H:mm", "dd.MM.yyyy HH:mm", "dd/MM/yyyy HH:mm" },
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                        {
                            return dt;
                        }
                        // Samo "20:00" -> dziś + ta godzina
                        if (TimeSpan.TryParse(str, out TimeSpan ts))
                        {
                            return DateTime.Today.Add(ts);
                        }
                        return null;

                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Parsuje wagę z formatu " 2.25 Kg" na decimal
        /// </summary>
        private decimal ParseWaga(string wagaRaw)
        {
            if (string.IsNullOrEmpty(wagaRaw)) return 0;

            // Usuń "Kg" i spacje
            string cleaned = Regex.Replace(wagaRaw, @"[Kk][Gg]", "").Trim();
            // Zamień przecinek na kropkę
            cleaned = cleaned.Replace(",", ".");

            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal waga))
            {
                return waga;
            }

            return 0;
        }

        /// <summary>
        /// Czyści numer telefonu (usuwa spacje, myślniki)
        /// </summary>
        private string CleanPhoneNumber(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return "";
            return Regex.Replace(phone, @"[\s\-\(\)]", "").Trim();
        }
    }

    /// <summary>
    /// Wynik parsowania Excel z AVILOG
    /// </summary>
    public class AvilogExcelParseResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime? DataUboju { get; set; }
        public List<ImportExcelRow> Wiersze { get; set; } = new List<ImportExcelRow>();
    }

    /// <summary>
    /// Wiersz importowany z Excel AVILOG
    /// </summary>
    public class ImportExcelRow : System.ComponentModel.INotifyPropertyChanged
    {
        private int _lp;
        private string _kierowcaNazwa;
        private string _kierowcaTelefon;
        private string _hodowcaNazwa;
        private string _hodowcaAdres;
        private string _hodowcaKodPocztowy;
        private string _hodowcaMiejscowosc;
        private string _hodowcaTelefon;
        private string _hodowcaGPS;
        private string _ciagnik;
        private string _naczepa;
        private int _sztuki;
        private decimal _wagaDek;
        private string _wymiarSkrzyn;
        private DateTime? _wyjazdZaklad;
        private TimeSpan? _godzinaZaladunku;
        private DateTime? _powrotZaklad;
        private string _obserwacje;

        // Mapowanie na bazę danych
        private int? _mappedKierowcaGID;
        private string _mappedHodowcaGID;
        private string _mappedCiagnikID;
        private string _mappedNaczepaID;

        public int Lp
        {
            get => _lp;
            set { _lp = value; OnPropertyChanged(nameof(Lp)); }
        }

        public string KierowcaNazwa
        {
            get => _kierowcaNazwa;
            set { _kierowcaNazwa = value; OnPropertyChanged(nameof(KierowcaNazwa)); OnPropertyChanged(nameof(AvilogKierowca)); }
        }

        public string KierowcaTelefon
        {
            get => _kierowcaTelefon;
            set { _kierowcaTelefon = value; OnPropertyChanged(nameof(KierowcaTelefon)); OnPropertyChanged(nameof(AvilogKierowca)); }
        }

        public string HodowcaNazwa
        {
            get => _hodowcaNazwa;
            set { _hodowcaNazwa = value; OnPropertyChanged(nameof(HodowcaNazwa)); OnPropertyChanged(nameof(AvilogHodowca)); }
        }

        public string HodowcaAdres
        {
            get => _hodowcaAdres;
            set { _hodowcaAdres = value; OnPropertyChanged(nameof(HodowcaAdres)); OnPropertyChanged(nameof(AvilogAdres)); }
        }

        public string HodowcaKodPocztowy
        {
            get => _hodowcaKodPocztowy;
            set { _hodowcaKodPocztowy = value; OnPropertyChanged(nameof(HodowcaKodPocztowy)); OnPropertyChanged(nameof(AvilogAdres)); }
        }

        public string HodowcaMiejscowosc
        {
            get => _hodowcaMiejscowosc;
            set { _hodowcaMiejscowosc = value; OnPropertyChanged(nameof(HodowcaMiejscowosc)); OnPropertyChanged(nameof(AvilogAdres)); }
        }

        public string HodowcaTelefon
        {
            get => _hodowcaTelefon;
            set { _hodowcaTelefon = value; OnPropertyChanged(nameof(HodowcaTelefon)); }
        }

        public string HodowcaGPS
        {
            get => _hodowcaGPS;
            set { _hodowcaGPS = value; OnPropertyChanged(nameof(HodowcaGPS)); }
        }

        public string Ciagnik
        {
            get => _ciagnik;
            set { _ciagnik = value; OnPropertyChanged(nameof(Ciagnik)); }
        }

        public string Naczepa
        {
            get => _naczepa;
            set { _naczepa = value; OnPropertyChanged(nameof(Naczepa)); }
        }

        public int Sztuki
        {
            get => _sztuki;
            set { _sztuki = value; OnPropertyChanged(nameof(Sztuki)); }
        }

        public decimal WagaDek
        {
            get => _wagaDek;
            set { _wagaDek = value; OnPropertyChanged(nameof(WagaDek)); }
        }

        public string WymiarSkrzyn
        {
            get => _wymiarSkrzyn;
            set { _wymiarSkrzyn = value; OnPropertyChanged(nameof(WymiarSkrzyn)); }
        }

        public DateTime? WyjazdZaklad
        {
            get => _wyjazdZaklad;
            set { _wyjazdZaklad = value; OnPropertyChanged(nameof(WyjazdZaklad)); OnPropertyChanged(nameof(WyjazdDisplay)); }
        }

        public TimeSpan? GodzinaZaladunku
        {
            get => _godzinaZaladunku;
            set { _godzinaZaladunku = value; OnPropertyChanged(nameof(GodzinaZaladunku)); OnPropertyChanged(nameof(ZaladunekDisplay)); }
        }

        public DateTime? PowrotZaklad
        {
            get => _powrotZaklad;
            set { _powrotZaklad = value; OnPropertyChanged(nameof(PowrotZaklad)); OnPropertyChanged(nameof(PowrotDisplay)); }
        }

        public string Obserwacje
        {
            get => _obserwacje;
            set { _obserwacje = value; OnPropertyChanged(nameof(Obserwacje)); }
        }

        // Mapowanie na bazę
        public int? MappedKierowcaGID
        {
            get => _mappedKierowcaGID;
            set { _mappedKierowcaGID = value; OnPropertyChanged(nameof(MappedKierowcaGID)); OnPropertyChanged(nameof(StatusMapowania)); }
        }

        public string MappedHodowcaGID
        {
            get => _mappedHodowcaGID;
            set { _mappedHodowcaGID = value; OnPropertyChanged(nameof(MappedHodowcaGID)); OnPropertyChanged(nameof(StatusMapowania)); }
        }

        public string MappedCiagnikID
        {
            get => _mappedCiagnikID;
            set { _mappedCiagnikID = value; OnPropertyChanged(nameof(MappedCiagnikID)); }
        }

        public string MappedNaczepaID
        {
            get => _mappedNaczepaID;
            set { _mappedNaczepaID = value; OnPropertyChanged(nameof(MappedNaczepaID)); }
        }

        // Wyświetlane właściwości dla DataGrid
        public string AvilogKierowca => $"{KierowcaNazwa}\n{KierowcaTelefon}".Trim();
        public string AvilogHodowca => HodowcaNazwa;
        public string AvilogAdres => $"{HodowcaAdres}\n{HodowcaKodPocztowy} {HodowcaMiejscowosc}".Trim();

        public string WyjazdDisplay => WyjazdZaklad?.ToString("HH:mm") ?? "-";
        public string ZaladunekDisplay => GodzinaZaladunku?.ToString(@"hh\:mm") ?? "-";
        public string PowrotDisplay => PowrotZaklad?.ToString("HH:mm") ?? "-";

        public string StatusMapowania
        {
            get
            {
                if (!string.IsNullOrEmpty(MappedHodowcaGID))
                    return "OK";
                else
                    return "BRAK";
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
