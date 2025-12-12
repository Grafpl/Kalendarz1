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

                    // DEBUG: Zapisz tekst do pliku do analizy
                    try
                    {
                        string debugPath = System.IO.Path.Combine(
                            System.IO.Path.GetDirectoryName(filePath),
                            "avilog_debug_text.txt");
                        System.IO.File.WriteAllText(debugPath, text);
                        result.DebugText = text;
                    }
                    catch { }

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
        /// Parsowanie - szuka numerów rejestracyjnych i grupuje w pary (ciągnik, naczepa)
        /// </summary>
        private List<AvilogTransportRow> ParseTransportRowsNew(string text)
        {
            var rows = new List<AvilogTransportRow>();

            // Znajdź wszystkie numery rejestracyjne
            // Format polski: 2-3 litery + 4-6 znaków alfanumerycznych
            // Przykłady: WOT51407, WPR6904T, WOT46L9, WOT51L5, SK12345
            var rejMatches = Regex.Matches(text, @"\b([A-Z]{2,3}[0-9A-Z]{4,6})\b");

            var rejestracje = new List<(string Numer, int Pos)>();
            foreach (Match m in rejMatches)
            {
                string num = m.Groups[1].Value;
                // Musi mieć co najmniej jedną cyfrę (żeby nie łapać słów typu "POJAZD")
                if (num.Length >= 6 && num.Length <= 9 && Regex.IsMatch(num, @"\d"))
                {
                    rejestracje.Add((num, m.Index));
                }
            }

            // Bezpośrednio grupuj rejestracje w pary: ciągnik, naczepa, ciągnik, naczepa...
            // To najprostsze i najskuteczniejsze podejście
            for (int i = 0; i < rejestracje.Count - 1; i += 2)
            {
                var row = new AvilogTransportRow();
                row.Ciagnik = rejestracje[i].Numer;
                row.Naczepa = rejestracje[i + 1].Numer;

                // Wyciągnij kontekst - tekst wokół tej pary rejestracji
                int startPos = Math.Max(0, rejestracje[i].Pos - 300);
                int endPos = Math.Min(text.Length, rejestracje[i + 1].Pos + 400);
                string context = text.Substring(startPos, endPos - startPos);

                // Parsuj dane z kontekstu
                ParseContextData(context, row);

                rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// Parsuje dane z kontekstu wokół pozycji pojazdu
        /// </summary>
        private void ParseContextData(string context, AvilogTransportRow row)
        {
            // Szukaj ilości sztuk - wzorzec: cyfra + spacja + 3 cyfry (np. "5 544", "4 224")
            var sztukiMatch = Regex.Match(context, @"(\d)\s(\d{3})(?:\s|$|\n|[^0-9])");
            if (sztukiMatch.Success)
            {
                string sztuki = sztukiMatch.Groups[1].Value + sztukiMatch.Groups[2].Value;
                if (int.TryParse(sztuki, out int szt) && szt > 1000 && szt < 20000)
                {
                    row.Sztuki = szt;
                }
            }

            // Szukaj wymiaru skrzyń (np. "21 x 264", "16 x 264")
            var wymiaryMatch = Regex.Match(context, @"(\d+)\s*x\s*(\d+)");
            if (wymiaryMatch.Success)
            {
                row.WymiarSkrzyn = $"{wymiaryMatch.Groups[1].Value} x {wymiaryMatch.Groups[2].Value}";
            }

            // Szukaj wagi (np. "2.25 Kg", "2.80 Kg")
            var wagaMatch = Regex.Match(context, @"(\d+[.,]\d+)\s*Kg", RegexOptions.IgnoreCase);
            if (wagaMatch.Success)
            {
                string waga = wagaMatch.Groups[1].Value.Replace(",", ".");
                if (decimal.TryParse(waga, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal w))
                {
                    row.WagaDek = w;
                }
            }

            // Szukaj godzin (format HH:MM)
            var godziny = Regex.Matches(context, @"\b(\d{2}):(\d{2})\b");
            List<TimeSpan> times = new List<TimeSpan>();
            foreach (Match g in godziny)
            {
                if (int.TryParse(g.Groups[1].Value, out int h) && int.TryParse(g.Groups[2].Value, out int m))
                {
                    if (h >= 0 && h < 24 && m >= 0 && m < 60)
                    {
                        times.Add(new TimeSpan(h, m, 0));
                    }
                }
            }

            // Przypisz godziny
            if (times.Count >= 1) row.WyjazdZaklad = DateTime.Today.Add(times[0]);
            if (times.Count >= 2) row.PoczatekZaladunku = times[1];
            if (times.Count >= 3) row.PowrotZaklad = DateTime.Today.Add(times[2]);

            // Szukaj hodowcy - WIELKIE LITERY + Tel. : + telefon
            var hodowcaMatch = Regex.Match(context,
                @"([A-ZŻŹĆĄŚĘŁÓŃ]+)\s+([A-ZŻŹĆĄŚĘŁÓŃ]+)\s+Tel\s*\.\s*:\s*(\d{9}|\d{3}\s?\d{3}\s?\d{3})");
            if (hodowcaMatch.Success)
            {
                row.HodowcaNazwa = $"{hodowcaMatch.Groups[1].Value} {hodowcaMatch.Groups[2].Value}";
                row.HodowcaTelefon = Regex.Replace(hodowcaMatch.Groups[3].Value, @"\s+", "");
            }

            // Szukaj kierowcy - imię (mixed case) + Tel2. :
            var kierowcaMatch = Regex.Match(context, @"([A-ZŻŹĆĄŚĘŁÓŃ][a-zżźćąśęłóń]+)\s+Tel2\s*\.");
            if (kierowcaMatch.Success)
            {
                row.KierowcaNazwa = kierowcaMatch.Groups[1].Value;
            }
            // Alternatywnie: szukaj imienia przed hodowcą
            if (string.IsNullOrEmpty(row.KierowcaNazwa))
            {
                var altKierowca = Regex.Match(context, @"([A-ZŻŹĆĄŚĘŁÓŃ][a-zżźćąśęłóń]+)\s+[A-ZŻŹĆĄŚĘŁÓŃ]{3,}\s+[A-ZŻŹĆĄŚĘŁÓŃ]{3,}\s+Tel\s*\.");
                if (altKierowca.Success)
                {
                    row.KierowcaNazwa = altKierowca.Groups[1].Value;
                }
            }

            // Szukaj obserwacji o wózku
            if (context.Contains("Wózek w obie strony") || context.Contains("Wozek w obie strony"))
                row.Obserwacje = "Wózek w obie strony";
            else if (context.Contains("Wieziesz wózek") || context.Contains("Wieziesz wozek"))
                row.Obserwacje = "Wieziesz wózek";
            else if (context.Contains("Zabierasz wózek") || context.Contains("Zabierasz wozek"))
                row.Obserwacje = "Zabierasz wózek";
            else if (context.Contains("Przywozisz wózek") || context.Contains("Przywozisz wozek"))
                row.Obserwacje = "Przywozisz wózek";
        }

        /// <summary>
        /// Alternatywne parsowanie - na podstawie samych rejestracji
        /// </summary>
        private List<AvilogTransportRow> ParseByRegistrations(string text, List<(string Numer, int Pos)> rejestracje)
        {
            var rows = new List<AvilogTransportRow>();

            // Grupuj rejestracje w pary (ciągnik, naczepa)
            // Zakładamy że idą naprzemiennie: ciągnik, naczepa, ciągnik, naczepa...
            for (int i = 0; i < rejestracje.Count - 1; i += 2)
            {
                var row = new AvilogTransportRow();
                row.Ciagnik = rejestracje[i].Numer;
                row.Naczepa = rejestracje[i + 1].Numer;

                // Wyciągnij kontekst
                int startPos = Math.Max(0, rejestracje[i].Pos - 200);
                int endPos = Math.Min(text.Length, rejestracje[i + 1].Pos + 300);
                string context = text.Substring(startPos, endPos - startPos);

                ParseContextData(context, row);

                if (!string.IsNullOrEmpty(row.Ciagnik))
                {
                    rows.Add(row);
                }
            }

            return rows;
        }

        /// <summary>
        /// Alternatywne parsowanie - linia po linii
        /// </summary>
        private List<AvilogTransportRow> ParseByLines(string text)
        {
            var rows = new List<AvilogTransportRow>();
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            AvilogTransportRow currentRow = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                // Szukaj numeru rejestracyjnego (ciągnik)
                var rejestracja = Regex.Match(line, @"^([A-Z]{2,3}\d{4,6}[A-Z]?)$");
                if (rejestracja.Success)
                {
                    // To może być nowy ciągnik
                    if (currentRow == null || !string.IsNullOrEmpty(currentRow.Naczepa))
                    {
                        if (currentRow != null && !string.IsNullOrEmpty(currentRow.Ciagnik))
                        {
                            rows.Add(currentRow);
                        }
                        currentRow = new AvilogTransportRow();
                        currentRow.Ciagnik = rejestracja.Value;
                    }
                    else if (string.IsNullOrEmpty(currentRow.Naczepa))
                    {
                        // To jest naczepa
                        currentRow.Naczepa = rejestracja.Value;
                    }
                    continue;
                }

                // Szukaj ilości sztuk (np. "4 224" lub "5544")
                var sztukiMatch = Regex.Match(line, @"^(\d[\s]?\d{3})$");
                if (sztukiMatch.Success && currentRow != null)
                {
                    string sztuki = Regex.Replace(sztukiMatch.Value, @"\s+", "");
                    if (int.TryParse(sztuki, out int szt) && szt > 1000 && szt < 20000)
                    {
                        currentRow.Sztuki = szt;
                    }
                    continue;
                }

                // Szukaj wymiaru (np. "16 x 264")
                var wymiaryMatch = Regex.Match(line, @"^(\d+)\s*[xX]\s*(\d+)$");
                if (wymiaryMatch.Success && currentRow != null)
                {
                    currentRow.WymiarSkrzyn = line;
                    continue;
                }

                // Szukaj kierowcy (imię nazwisko + telefon)
                var kierowcaMatch = Regex.Match(line, @"^([A-ZŻŹĆĄŚĘŁÓŃ][a-zżźćąśęłóń]+|[A-ZŻŹĆĄŚĘŁÓŃ]+)\s+([A-ZŻŹĆĄŚĘŁÓŃ][a-zżźćąśęłóń]+|[A-ZŻŹĆĄŚĘŁÓŃ]+)\s+(\d{3}[\s]?\d{3}[\s]?\d{3})$");
                if (kierowcaMatch.Success && currentRow != null)
                {
                    currentRow.KierowcaNazwa = $"{kierowcaMatch.Groups[1].Value} {kierowcaMatch.Groups[2].Value}";
                    currentRow.KierowcaTelefon = Regex.Replace(kierowcaMatch.Groups[3].Value, @"\s+", "");
                    continue;
                }

                // Szukaj hodowcy (WIELKIE LITERY)
                if (Regex.IsMatch(line, @"^[A-ZŻŹĆĄŚĘŁÓŃ\s/]+$") && line.Length > 5 &&
                    !line.Contains("KIEROWCA") && !line.Contains("HODOWCA") && !line.Contains("POJAZD"))
                {
                    if (currentRow != null && string.IsNullOrEmpty(currentRow.HodowcaNazwa))
                    {
                        currentRow.HodowcaNazwa = line;
                    }
                }
            }

            // Dodaj ostatni wiersz
            if (currentRow != null && !string.IsNullOrEmpty(currentRow.Ciagnik))
            {
                rows.Add(currentRow);
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
        public string DebugText { get; set; }
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
