using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Kalendarz1
{
    /// <summary>
    /// Parser plików PDF z planowaniem transportu od AVILOG
    /// </summary>
    public class AvilogPdfParser
    {
        /// <summary>
        /// Parsuje plik PDF z AVILOG i zwraca listę wierszy transportowych
        /// </summary>
        public AvilogParseResult ParsePdf(string filePath)
        {
            var result = new AvilogParseResult();

            try
            {
                using (PdfReader reader = new PdfReader(filePath))
                {
                    StringBuilder fullText = new StringBuilder();

                    for (int page = 1; page <= reader.NumberOfPages; page++)
                    {
                        string pageText = PdfTextExtractor.GetTextFromPage(reader, page);
                        fullText.AppendLine(pageText);
                    }

                    string text = fullText.ToString();

                    // Wyciągnij datę uboju z nagłówka
                    result.DataUboju = ExtractDataUboju(text);

                    // Parsuj wiersze transportowe
                    result.Wiersze = ParseTransportRows(text);
                    result.Success = true;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Błąd parsowania PDF: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Wyciąga datę uboju z nagłówka PDF
        /// </summary>
        private DateTime? ExtractDataUboju(string text)
        {
            // Szukamy: "DATA UBOJU : środa 03 grudzień 2025" lub podobne
            var datePatterns = new[]
            {
                @"DATA UBOJU\s*:\s*\w+\s+(\d{1,2})\s+(\w+)\s+(\d{4})",
                @"DATA UBOJU\s*:\s*(\d{1,2})[.\-/](\d{1,2})[.\-/](\d{4})",
                @"(\d{1,2})[.\-/](\d{1,2})[.\-/](\d{4})"
            };

            foreach (var pattern in datePatterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    try
                    {
                        // Próba parsowania z nazwą miesiąca
                        if (match.Groups.Count >= 4 && !int.TryParse(match.Groups[2].Value, out _))
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
                        else
                        {
                            // Format dd.MM.yyyy lub dd/MM/yyyy
                            int day = int.Parse(match.Groups[1].Value);
                            int month = int.Parse(match.Groups[2].Value);
                            int year = int.Parse(match.Groups[3].Value);
                            return new DateTime(year, month, day);
                        }
                    }
                    catch { }
                }
            }

            return null;
        }

        /// <summary>
        /// Konwertuje polską nazwę miesiąca na numer
        /// </summary>
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
        /// Parsuje wiersze transportowe z tekstu PDF
        /// </summary>
        private List<AvilogTransportRow> ParseTransportRows(string text)
        {
            var rows = new List<AvilogTransportRow>();
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            AvilogTransportRow currentRow = null;
            bool inDataSection = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                // Szukamy początku sekcji danych (po nagłówku KIEROWCA)
                if (line.Contains("KIEROWCA") && line.Contains("HODOWCA"))
                {
                    inDataSection = true;
                    continue;
                }

                if (!inDataSection) continue;

                // Sprawdź czy to nowy wiersz z kierowcą (zaczyna się od imienia i nazwiska + telefon)
                if (IsDriverLine(line))
                {
                    if (currentRow != null && !string.IsNullOrEmpty(currentRow.HodowcaNazwa))
                    {
                        rows.Add(currentRow);
                    }

                    currentRow = new AvilogTransportRow();
                    ParseDriverLine(line, currentRow);
                }
                // Linia z hodowcą (nazwa + adres)
                else if (currentRow != null && string.IsNullOrEmpty(currentRow.HodowcaNazwa) && IsHodowcaLine(line))
                {
                    ParseHodowcaLine(line, currentRow);
                }
                // Linia z pojazdem (C:, N:, W:)
                else if (currentRow != null && line.Contains("C:"))
                {
                    ParsePojazdLine(line, currentRow);
                }
                // Linia z ilością i godzinami
                else if (currentRow != null && ContainsQuantityData(line))
                {
                    ParseQuantityAndTimeLine(line, currentRow);
                }
                // Linia z obserwacjami
                else if (currentRow != null && (line.Contains("Wózek") || line.Contains("wózek") || line.Contains("Wieziesz") || line.Contains("Zabierasz")))
                {
                    currentRow.Obserwacje = line.Trim();
                }
            }

            // Dodaj ostatni wiersz
            if (currentRow != null && !string.IsNullOrEmpty(currentRow.HodowcaNazwa))
            {
                rows.Add(currentRow);
            }

            return rows;
        }

        private bool IsDriverLine(string line)
        {
            // Kierowca: Imię NAZWISKO + numer telefonu (9 cyfr)
            return Regex.IsMatch(line, @"^\w+\s+[A-ZŻŹĆĄŚĘŁÓŃ]+\s*$") ||
                   Regex.IsMatch(line, @"^\w+\s+[A-ZŻŹĆĄŚĘŁÓŃ]+.*\d{9}");
        }

        private bool IsHodowcaLine(string line)
        {
            // Hodowca to linia z nazwą i adresem, często zawiera współrzędne GPS
            return Regex.IsMatch(line, @"\d{2}[.,]\d+") || // współrzędne GPS
                   Regex.IsMatch(line, @"\d{2}-\d{3}") ||   // kod pocztowy
                   line.Contains("Tel.");
        }

        private bool ContainsQuantityData(string line)
        {
            // Linia z danymi ilościowymi: zawiera liczbę sztuk, wagę w Kg
            return Regex.IsMatch(line, @"\d+\s*x\s*\d+") || // np. 21 x 264
                   Regex.IsMatch(line, @"\d+[.,]\d+\s*Kg", RegexOptions.IgnoreCase);
        }

        private void ParseDriverLine(string line, AvilogTransportRow row)
        {
            // Format: "Knapkiewicz Sylwester 609 258 813" lub "JANECZEK GRZEGORZ\n507129854"
            var phoneMatch = Regex.Match(line, @"(\d[\d\s]{8,})");
            if (phoneMatch.Success)
            {
                row.KierowcaTelefon = Regex.Replace(phoneMatch.Value, @"\s+", "");
                row.KierowcaNazwa = line.Substring(0, phoneMatch.Index).Trim();
            }
            else
            {
                row.KierowcaNazwa = line.Trim();
            }
        }

        private void ParseHodowcaLine(string line, AvilogTransportRow row)
        {
            // Format: "KIEŁBASA MARCIN Tel. : 784897762"
            // lub: "STUDZIENIEC 8 52.376821 19.909136 Tel2. :"
            // lub: "09-533 SŁUBICE Tel. Ferma :"

            var telMatch = Regex.Match(line, @"Tel\.?\s*:?\s*(\d[\d\s-]+)");
            if (telMatch.Success)
            {
                row.HodowcaTelefon = Regex.Replace(telMatch.Groups[1].Value, @"\s+", "");
            }

            // GPS coordinates
            var gpsMatch = Regex.Match(line, @"(\d{2}[.,]\d+)[,\s]+(\d{2}[.,]\d+)");
            if (gpsMatch.Success)
            {
                row.HodowcaGpsLat = gpsMatch.Groups[1].Value;
                row.HodowcaGpsLon = gpsMatch.Groups[2].Value;
            }

            // Kod pocztowy i miejscowość
            var zipMatch = Regex.Match(line, @"(\d{2}-\d{3})\s+(\w+)");
            if (zipMatch.Success)
            {
                row.HodowcaKodPocztowy = zipMatch.Groups[1].Value;
                row.HodowcaMiejscowosc = zipMatch.Groups[2].Value;
            }

            // Nazwa hodowcy - pierwsza linia bez współrzędnych i telefonów
            if (string.IsNullOrEmpty(row.HodowcaNazwa))
            {
                string cleanLine = Regex.Replace(line, @"Tel\.?\s*:?\s*\d[\d\s-]*", "");
                cleanLine = Regex.Replace(cleanLine, @"\d{2}[.,]\d+", "");
                cleanLine = Regex.Replace(cleanLine, @"\d{2}-\d{3}", "");
                row.HodowcaNazwa = cleanLine.Trim();
            }
            else
            {
                // Dodaj do adresu
                string cleanLine = Regex.Replace(line, @"Tel\.?\s*:?\s*\d[\d\s-]*", "");
                cleanLine = Regex.Replace(cleanLine, @"\d{2}[.,]\d+", "");
                if (!string.IsNullOrWhiteSpace(cleanLine))
                {
                    row.HodowcaAdres = (row.HodowcaAdres + " " + cleanLine).Trim();
                }
            }
        }

        private void ParsePojazdLine(string line, AvilogTransportRow row)
        {
            // Format: "C: WPR6904T N: WOT51L5 W:" lub "C: WOT51407"
            var ciagnikMatch = Regex.Match(line, @"C:\s*(\w+)");
            if (ciagnikMatch.Success)
            {
                row.Ciagnik = ciagnikMatch.Groups[1].Value;
            }

            var naczepaMatch = Regex.Match(line, @"N:\s*(\w+)");
            if (naczepaMatch.Success)
            {
                row.Naczepa = naczepaMatch.Groups[1].Value;
            }

            var wozekMatch = Regex.Match(line, @"W:\s*(\w+)");
            if (wozekMatch.Success)
            {
                row.Wozek = wozekMatch.Groups[1].Value;
            }
        }

        private void ParseQuantityAndTimeLine(string line, AvilogTransportRow row)
        {
            // Format: "4 224 16 x 264 2.80 Kg 02/12/2025 22:30 03/12/2025"
            // lub: "5 544 21 x 264 2.25 Kg"

            // Ilość sztuk (pierwsza duża liczba)
            var sztukiMatch = Regex.Match(line, @"(\d[\d\s]{2,5})(?=\s+\d+\s*x)");
            if (sztukiMatch.Success)
            {
                string sztuki = Regex.Replace(sztukiMatch.Groups[1].Value, @"\s+", "");
                if (int.TryParse(sztuki, out int szt))
                {
                    row.Sztuki = szt;
                }
            }

            // Wymiary skrzyń (np. 16 x 264)
            var wymiaryMatch = Regex.Match(line, @"(\d+)\s*x\s*(\d+)");
            if (wymiaryMatch.Success)
            {
                row.WymiarSkrzyn = $"{wymiaryMatch.Groups[1].Value} x {wymiaryMatch.Groups[2].Value}";
            }

            // Waga deklarowana (np. 2.80 Kg)
            var wagaMatch = Regex.Match(line, @"(\d+[.,]\d+)\s*Kg", RegexOptions.IgnoreCase);
            if (wagaMatch.Success)
            {
                string waga = wagaMatch.Groups[1].Value.Replace(",", ".");
                if (decimal.TryParse(waga, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal w))
                {
                    row.WagaDek = w;
                }
            }

            // Daty i godziny
            // Format: dd/MM/yyyy HH:mm
            var dateTimeMatches = Regex.Matches(line, @"(\d{2})/(\d{2})/(\d{4})\s*(\d{2}:\d{2})?");
            int dateIndex = 0;
            foreach (Match m in dateTimeMatches)
            {
                try
                {
                    int day = int.Parse(m.Groups[1].Value);
                    int month = int.Parse(m.Groups[2].Value);
                    int year = int.Parse(m.Groups[3].Value);
                    DateTime date = new DateTime(year, month, day);

                    string timeStr = m.Groups[4].Success ? m.Groups[4].Value : null;
                    if (!string.IsNullOrEmpty(timeStr))
                    {
                        var timeParts = timeStr.Split(':');
                        date = date.AddHours(int.Parse(timeParts[0])).AddMinutes(int.Parse(timeParts[1]));
                    }

                    if (dateIndex == 0)
                    {
                        row.WyjazdZaklad = date;
                    }
                    else if (dateIndex == 1)
                    {
                        row.PowrotZaklad = date;
                    }
                    dateIndex++;
                }
                catch { }
            }

            // Godzina załadunku (sama godzina bez daty)
            var timeOnlyMatch = Regex.Match(line, @"(?<!\d{2}/\d{2}/\d{4}\s*)(\d{2}:\d{2})(?!\s*\d{2}/\d{2})");
            if (timeOnlyMatch.Success && row.PoczatekZaladunku == null)
            {
                // Szukamy godziny która nie jest częścią daty
                var allTimes = Regex.Matches(line, @"(\d{2}):(\d{2})");
                if (allTimes.Count >= 2)
                {
                    // Druga godzina to zazwyczaj początek załadunku
                    var secondTime = allTimes[1];
                    int hour = int.Parse(secondTime.Groups[1].Value);
                    int minute = int.Parse(secondTime.Groups[2].Value);
                    row.PoczatekZaladunku = new TimeSpan(hour, minute, 0);
                }
            }
        }
    }

    /// <summary>
    /// Wynik parsowania PDF z AVILOG
    /// </summary>
    public class AvilogParseResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime? DataUboju { get; set; }
        public List<AvilogTransportRow> Wiersze { get; set; } = new List<AvilogTransportRow>();
    }

    /// <summary>
    /// Pojedynczy wiersz transportowy z PDF AVILOG
    /// </summary>
    public class AvilogTransportRow
    {
        // Kierowca
        public string KierowcaNazwa { get; set; }
        public string KierowcaTelefon { get; set; }

        // Hodowca (dane z AVILOG)
        public string HodowcaNazwa { get; set; }
        public string HodowcaAdres { get; set; }
        public string HodowcaKodPocztowy { get; set; }
        public string HodowcaMiejscowosc { get; set; }
        public string HodowcaTelefon { get; set; }
        public string HodowcaGpsLat { get; set; }
        public string HodowcaGpsLon { get; set; }

        // Pojazd
        public string Ciagnik { get; set; }
        public string Naczepa { get; set; }
        public string Wozek { get; set; }

        // Ilości
        public int Sztuki { get; set; }
        public string WymiarSkrzyn { get; set; }
        public decimal WagaDek { get; set; }

        // Czasy
        public DateTime? WyjazdZaklad { get; set; }
        public TimeSpan? PoczatekZaladunku { get; set; }
        public DateTime? PowrotZaklad { get; set; }

        // Uwagi
        public string Obserwacje { get; set; }

        // Mapowanie na wewnętrzną bazę (wypełniane później)
        public int? MappedKierowcaGID { get; set; }
        public int? MappedHodowcaGID { get; set; }

        /// <summary>
        /// Pełny opis hodowcy dla wyświetlenia
        /// </summary>
        public string HodowcaFullDescription =>
            $"{HodowcaNazwa}\n{HodowcaAdres}\n{HodowcaKodPocztowy} {HodowcaMiejscowosc}".Trim();
    }
}
