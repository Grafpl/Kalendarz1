using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System;
using System.Collections.Generic;
using System.Globalization;
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

                    // Czytaj wszystkie strony
                    for (int page = 1; page <= reader.NumberOfPages; page++)
                    {
                        string pageText = PdfTextExtractor.GetTextFromPage(reader, page);
                        fullText.AppendLine(pageText);
                        fullText.AppendLine("---PAGE_BREAK---");
                    }

                    string text = fullText.ToString();

                    // Wyciągnij datę uboju z nagłówka
                    result.DataUboju = ExtractDataUboju(text);

                    // Parsuj wiersze transportowe - nowa metoda
                    result.Wiersze = ParseTransportRowsNew(text);
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
            // Szukamy: "DATA UBOJU : środa 03 grudzień 2025"
            var match = Regex.Match(text, @"DATA UBOJU\s*:\s*\w+\s+(\d{1,2})\s+(\w+)\s+(\d{4})", RegexOptions.IgnoreCase);
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

            // Alternatywny format dd/MM/yyyy
            var match2 = Regex.Match(text, @"(\d{2})/(\d{2})/(\d{4})");
            if (match2.Success)
            {
                try
                {
                    int day = int.Parse(match2.Groups[1].Value);
                    int month = int.Parse(match2.Groups[2].Value);
                    int year = int.Parse(match2.Groups[3].Value);
                    return new DateTime(year, month, day);
                }
                catch { }
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
        /// Nowa metoda parsowania - szuka wzorców pojazdów C: i wyciąga dane
        /// </summary>
        private List<AvilogTransportRow> ParseTransportRowsNew(string text)
        {
            var rows = new List<AvilogTransportRow>();

            // Znajdź wszystkie pojazdy (C: XXX) - to są unikalne markery wierszy
            var pojazdMatches = Regex.Matches(text, @"C:\s*([A-Z0-9]+)\s*N:\s*([A-Z0-9]+)", RegexOptions.IgnoreCase);

            foreach (Match pojazdMatch in pojazdMatches)
            {
                var row = new AvilogTransportRow();

                // Pojazd
                row.Ciagnik = pojazdMatch.Groups[1].Value.Trim();
                row.Naczepa = pojazdMatch.Groups[2].Value.Trim();

                int pojazdPos = pojazdMatch.Index;

                // Szukaj wstecz od pozycji pojazdu żeby znaleźć kierowcę i hodowcę
                string textBefore = text.Substring(Math.Max(0, pojazdPos - 800), Math.Min(800, pojazdPos));

                // Szukaj do przodu od pozycji pojazdu żeby znaleźć ilości i godziny
                string textAfter = text.Substring(pojazdPos, Math.Min(500, text.Length - pojazdPos));

                // Kierowca - szukaj imienia i nazwiska z telefonem
                ParseKierowcaFromText(textBefore, row);

                // Hodowca - szukaj pogrubionej nazwy (wielkie litery) z telefonem
                ParseHodowcaFromText(textBefore, row);

                // Ilości i godziny
                ParseQuantityFromText(textAfter, row);

                // Obserwacje (wózek)
                ParseObserwacjeFromText(textAfter, row);

                // Dodaj tylko jeśli mamy podstawowe dane
                if (!string.IsNullOrEmpty(row.Ciagnik) && row.Sztuki > 0)
                {
                    rows.Add(row);
                }
            }

            return rows;
        }

        private void ParseKierowcaFromText(string text, AvilogTransportRow row)
        {
            // Szukaj wzorca: Imię Nazwisko + telefon 9 cyfr
            // Przykłady: "Knapkiewicz Sylwester 609 258 813", "JANECZEK GRZEGORZ 507129854"

            // Najpierw znajdź telefon 9-cyfrowy
            var phoneMatches = Regex.Matches(text, @"(\d{3}[\s]?\d{3}[\s]?\d{3}|\d{9})");

            foreach (Match phoneMatch in phoneMatches)
            {
                int phonePos = phoneMatch.Index;
                string phoneBefore = text.Substring(Math.Max(0, phonePos - 100), Math.Min(100, phonePos));

                // Szukaj imienia i nazwiska przed telefonem
                // Format: słowo + słowo (np. "Knapkiewicz Sylwester" lub "JANECZEK GRZEGORZ")
                var nameMatch = Regex.Match(phoneBefore, @"([A-ZŻŹĆĄŚĘŁÓŃ][a-zżźćąśęłóń]+|[A-ZŻŹĆĄŚĘŁÓŃ]+)\s+([A-ZŻŹĆĄŚĘŁÓŃ][a-zżźćąśęłóń]+|[A-ZŻŹĆĄŚĘŁÓŃ]+)\s*$");

                if (nameMatch.Success)
                {
                    row.KierowcaNazwa = nameMatch.Value.Trim();
                    row.KierowcaTelefon = Regex.Replace(phoneMatch.Value, @"\s+", "");
                    return;
                }
            }
        }

        private void ParseHodowcaFromText(string text, AvilogTransportRow row)
        {
            // Szukaj nazwy hodowcy - WIELKIE LITERY (pogrubione w PDF)
            // Przykłady: "KIEŁBASA MARCIN", "MARKOWSKI KRZYSZTOF", "LAPIAK PIOTR / MONIKA"

            // Wzorzec: 2+ słowa wielkimi literami, może zawierać "/"
            var hodowcaMatches = Regex.Matches(text, @"([A-ZŻŹĆĄŚĘŁÓŃ]{3,}(?:\s+[A-ZŻŹĆĄŚĘŁÓŃ]{2,})+(?:\s*/\s*[A-ZŻŹĆĄŚĘŁÓŃ]+)?)");

            foreach (Match match in hodowcaMatches)
            {
                string nazwa = match.Value.Trim();

                // Pomiń jeśli to nagłówek tabeli lub inne słowa kluczowe
                if (nazwa.Contains("KIEROWCA") || nazwa.Contains("HODOWCA") || nazwa.Contains("POJAZD") ||
                    nazwa.Contains("AVILOG") || nazwa.Contains("POLSKA") || nazwa.Contains("DATA") ||
                    nazwa.Contains("OBSERWACJE") || nazwa.Contains("ZAKŁAD") || nazwa.Contains("ILOŚĆ") ||
                    nazwa.Length < 6)
                    continue;

                row.HodowcaNazwa = nazwa;

                // Szukaj adresu po nazwie hodowcy
                int namePos = match.Index + match.Length;
                if (namePos < text.Length)
                {
                    string afterName = text.Substring(namePos, Math.Min(300, text.Length - namePos));

                    // Szukaj adresu (ulica + numer lub miejscowość)
                    var adresMatch = Regex.Match(afterName, @"^[\s\n]*([A-Za-zżźćąśęłóńŻŹĆĄŚĘŁÓŃ\s]+\d+[A-Za-z]?)");
                    if (adresMatch.Success)
                    {
                        row.HodowcaAdres = adresMatch.Groups[1].Value.Trim();
                    }

                    // Szukaj kodu pocztowego i miejscowości
                    var zipMatch = Regex.Match(afterName, @"(\d{2}-\d{3})\s+([A-ZŻŹĆĄŚĘŁÓŃ]+)");
                    if (zipMatch.Success)
                    {
                        row.HodowcaKodPocztowy = zipMatch.Groups[1].Value;
                        row.HodowcaMiejscowosc = zipMatch.Groups[2].Value;
                    }

                    // Szukaj telefonu hodowcy
                    var telMatch = Regex.Match(afterName, @"Tel\.?\s*:?\s*(\d{3}[\s-]?\d{3}[\s-]?\d{3}|\d{9})");
                    if (telMatch.Success)
                    {
                        row.HodowcaTelefon = Regex.Replace(telMatch.Groups[1].Value, @"[\s-]+", "");
                    }
                }

                return; // Weź pierwszy pasujący
            }
        }

        private void ParseQuantityFromText(string text, AvilogTransportRow row)
        {
            // Szukaj ilości sztuk - duża liczba (np. 4 224, 5 544)
            var sztukiMatch = Regex.Match(text, @"(\d[\s]?\d{3})");
            if (sztukiMatch.Success)
            {
                string sztuki = Regex.Replace(sztukiMatch.Value, @"\s+", "");
                if (int.TryParse(sztuki, out int szt) && szt > 1000 && szt < 20000)
                {
                    row.Sztuki = szt;
                }
            }

            // Szukaj wymiaru skrzyń (np. 16 x 264, 21 x 264)
            var wymiaryMatch = Regex.Match(text, @"(\d+)\s*[xX]\s*(\d+)");
            if (wymiaryMatch.Success)
            {
                row.WymiarSkrzyn = $"{wymiaryMatch.Groups[1].Value} x {wymiaryMatch.Groups[2].Value}";
            }

            // Szukaj wagi (np. 2.80 Kg, 2.25 Kg)
            var wagaMatch = Regex.Match(text, @"(\d+[.,]\d+)\s*Kg", RegexOptions.IgnoreCase);
            if (wagaMatch.Success)
            {
                string waga = wagaMatch.Groups[1].Value.Replace(",", ".");
                if (decimal.TryParse(waga, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal w))
                {
                    row.WagaDek = w;
                }
            }

            // Szukaj dat i godzin
            // Format: dd/MM/yyyy HH:mm lub dd/MM/yyyy \n HH:mm
            var dateTimeMatches = Regex.Matches(text, @"(\d{2})/(\d{2})/(\d{4})[\s\n]*(\d{2}:\d{2})?");
            int dateIndex = 0;
            foreach (Match m in dateTimeMatches)
            {
                try
                {
                    int day = int.Parse(m.Groups[1].Value);
                    int month = int.Parse(m.Groups[2].Value);
                    int year = int.Parse(m.Groups[3].Value);
                    DateTime date = new DateTime(year, month, day);

                    if (m.Groups[4].Success && !string.IsNullOrEmpty(m.Groups[4].Value))
                    {
                        var timeParts = m.Groups[4].Value.Split(':');
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

            // Szukaj godziny załadunku (sama godzina HH:mm między datami)
            var allTimes = Regex.Matches(text, @"(\d{2}):(\d{2})");
            if (allTimes.Count >= 2)
            {
                // Druga godzina (po godzinie wyjazdu) to początek załadunku
                try
                {
                    var secondTime = allTimes[1];
                    int hour = int.Parse(secondTime.Groups[1].Value);
                    int minute = int.Parse(secondTime.Groups[2].Value);
                    if (hour >= 0 && hour < 24)
                    {
                        row.PoczatekZaladunku = new TimeSpan(hour, minute, 0);
                    }
                }
                catch { }
            }
        }

        private void ParseObserwacjeFromText(string text, AvilogTransportRow row)
        {
            // Szukaj informacji o wózku
            if (text.Contains("Wózek w obie strony"))
            {
                row.Obserwacje = "Wózek w obie strony";
            }
            else if (text.Contains("Wieziesz wózek"))
            {
                row.Obserwacje = "Wieziesz wózek";
            }
            else if (text.Contains("Zabierasz wózek"))
            {
                row.Obserwacje = "Zabierasz wózek";
            }
            else if (text.Contains("Przywozisz wózek"))
            {
                row.Obserwacje = "Przywozisz wózek";
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

        // Mapowanie na wewnętrzną bazę
        public int? MappedKierowcaGID { get; set; }
        public string MappedHodowcaGID { get; set; }
        public string MappedCiagnikID { get; set; }
        public string MappedNaczepaID { get; set; }
    }
}
