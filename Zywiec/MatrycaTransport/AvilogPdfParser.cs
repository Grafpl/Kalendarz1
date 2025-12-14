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
        /// Parsowanie - szuka wzorców "C:" i "N:" jako kotwic dla pojazdów
        /// Dane kierowcy i hodowcy są PRZED pozycją "C:" w tekście
        /// </summary>
        private List<AvilogTransportRow> ParseTransportRowsNew(string text)
        {
            var rows = new List<AvilogTransportRow>();
            var debugLines = new List<string>();

            // Szukaj wzorca "C: REJESTRACJA" - to oznacza ciągnik
            // Format w PDF: "C: WOT51407" lub "C:WOT51407"
            var ciagnikiMatches = Regex.Matches(text, @"C\s*:\s*([A-Z]{2,3}[0-9][0-9A-Z]{3,5})");

            debugLines.Add($"Znaleziono {ciagnikiMatches.Count} ciągników (C:)");

            // Pozycje wszystkich ciągników
            var ciagnikiPositions = new List<(string Ciagnik, int Pos)>();
            foreach (Match m in ciagnikiMatches)
            {
                ciagnikiPositions.Add((m.Groups[1].Value, m.Index));
                debugLines.Add($"  C: {m.Groups[1].Value} @ pozycja {m.Index}");
            }

            // Dla każdego ciągnika znajdź odpowiednią naczepę (N:) i dane
            for (int i = 0; i < ciagnikiPositions.Count; i++)
            {
                var row = new AvilogTransportRow();
                row.Ciagnik = ciagnikiPositions[i].Ciagnik;
                int ciagnikPos = ciagnikiPositions[i].Pos;

                // Znajdź naczepę - powinna być blisko po ciągniku (w zakresie 100 znaków)
                string afterCiagnik = text.Substring(ciagnikPos, Math.Min(150, text.Length - ciagnikPos));
                var naczepaMatch = Regex.Match(afterCiagnik, @"N\s*:\s*([A-Z]{2,3}[0-9][0-9A-Z]{3,5})");
                if (naczepaMatch.Success)
                {
                    row.Naczepa = naczepaMatch.Groups[1].Value;
                }

                // === KLUCZOWE: Wyciągnij tekst PRZED C: dla tego wiersza ===
                // Określ początek kontekstu - albo początek tekstu, albo koniec poprzedniego wiersza
                int startContext;
                if (i == 0)
                {
                    // Dla pierwszego wiersza - szukaj od początku tabeli (po nagłówkach)
                    var headerEnd = Regex.Match(text.Substring(0, ciagnikPos), @"OBSERWACJE", RegexOptions.IgnoreCase);
                    startContext = headerEnd.Success ? headerEnd.Index + headerEnd.Length : Math.Max(0, ciagnikPos - 800);
                }
                else
                {
                    // Dla kolejnych wierszy - zacznij od końca poprzedniego wiersza (po jego N:)
                    startContext = ciagnikiPositions[i - 1].Pos + 50;
                }

                // Tekst między początkiem kontekstu a C: zawiera dane kierowcy i hodowcy
                int contextLength = ciagnikPos - startContext;
                if (contextLength > 0 && contextLength < 2000)
                {
                    string beforeCiagnik = text.Substring(startContext, contextLength);

                    // Znajdź też tekst PO N: dla godzin i obserwacji
                    int naczepaEnd = ciagnikPos + (naczepaMatch.Success ? naczepaMatch.Index + naczepaMatch.Length : 50);
                    int endContext = (i + 1 < ciagnikiPositions.Count)
                        ? ciagnikiPositions[i + 1].Pos
                        : Math.Min(text.Length, naczepaEnd + 400);

                    string afterNaczepa = text.Substring(naczepaEnd, Math.Min(endContext - naczepaEnd, 400));

                    string fullContext = beforeCiagnik + " |POJAZD| " + afterNaczepa;

                    // DEBUG dla pierwszego wiersza
                    if (i == 0)
                    {
                        debugLines.Add($"\n=== WIERSZ 1 - ANALIZA ===");
                        debugLines.Add($"Ciągnik: {row.Ciagnik}, Naczepa: {row.Naczepa}");
                        debugLines.Add($"StartContext: {startContext}, CiagnikPos: {ciagnikPos}");
                        debugLines.Add($"=== TEKST PRZED C: (kierowca + hodowca) ===\n{beforeCiagnik}\n=== KONIEC ===");
                    }

                    // Parsuj dane z kontekstu (nowa metoda)
                    ParseContextDataNew(beforeCiagnik, afterNaczepa, row);
                }

                if (!string.IsNullOrEmpty(row.Ciagnik))
                {
                    rows.Add(row);
                }
            }

            // Zapisz debug
            try
            {
                string debugPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "avilog_parser_debug.txt");
                System.IO.File.WriteAllText(debugPath, string.Join("\n", debugLines));
            }
            catch { }

            return rows;
        }

        /// <summary>
        /// Nowa metoda parsowania - osobno tekst przed C: i po N:
        /// </summary>
        private void ParseContextDataNew(string beforeCiagnik, string afterNaczepa, AvilogTransportRow row)
        {
            // === KIEROWCA ===
            // Format: "Nazwisko Imię tel" lub "NAZWISKO IMIĘ tel"
            // Kierowca jest na POCZĄTKU tekstu przed C:, potem jest hodowca

            // Szukaj telefonu kierowcy - pierwszy 9-cyfrowy numer (nie poprzedzony "Tel.")
            var phoneMatches = Regex.Matches(beforeCiagnik, @"(?<!Tel\s*\.?\s*:?\s*)(\d{3})\s*(\d{3})\s*(\d{3})");
            var telColonMatch = Regex.Match(beforeCiagnik, @"Tel\s*\.?\s*:?\s*(\d{3})\s*(\d{3})\s*(\d{3})");

            string kierowcaTelefon = "";
            int kierowcaPhonePos = -1;

            // Pierwszy telefon który NIE jest poprzedzony "Tel." to kierowca
            foreach (Match phone in phoneMatches)
            {
                string phoneNum = phone.Groups[1].Value + phone.Groups[2].Value + phone.Groups[3].Value;
                // Sprawdź czy to nie jest telefon hodowcy (po "Tel.")
                if (!telColonMatch.Success || phoneNum != (telColonMatch.Groups[1].Value + telColonMatch.Groups[2].Value + telColonMatch.Groups[3].Value))
                {
                    kierowcaTelefon = phoneNum;
                    kierowcaPhonePos = phone.Index;
                    row.KierowcaTelefon = kierowcaTelefon;
                    break;
                }
            }

            // Nazwa kierowcy - 2 słowa TUŻ PRZED jego telefonem
            if (kierowcaPhonePos > 0)
            {
                string beforePhone = beforeCiagnik.Substring(0, kierowcaPhonePos).Trim();

                // Szukaj 2 ostatnich słów (imię i nazwisko)
                // Wzorzec: słowa zaczynające się wielką literą lub całe wielkimi
                var nameWords = Regex.Matches(beforePhone, @"([A-ZŻŹĆĄŚĘŁÓŃ][a-zżźćąśęłóń]{2,}|[A-ZŻŹĆĄŚĘŁÓŃ]{2,})");

                var validNames = new List<string>();
                foreach (Match w in nameWords)
                {
                    string word = w.Value;
                    if (!IsHeaderWord(word) && word.Length >= 3)
                    {
                        validNames.Add(word);
                    }
                }

                if (validNames.Count >= 2)
                {
                    // Weź ostatnie 2 słowa
                    row.KierowcaNazwa = $"{validNames[validNames.Count - 2]} {validNames[validNames.Count - 1]}";
                }
                else if (validNames.Count == 1)
                {
                    row.KierowcaNazwa = validNames[0];
                }
            }

            // === HODOWCA ===
            // Format: "NAZWISKO IMIĘ adres GPS kod Tel.: tel"
            // Hodowca jest PO kierowcy, przed C: (ale po telefonie kierowcy)

            // Telefon hodowcy (po "Tel.")
            if (telColonMatch.Success)
            {
                row.HodowcaTelefon = telColonMatch.Groups[1].Value + telColonMatch.Groups[2].Value + telColonMatch.Groups[3].Value;
            }

            // Nazwa hodowcy - szukaj WIELKICH LITER po telefonie kierowcy
            string hodowcaContext = kierowcaPhonePos > 0
                ? beforeCiagnik.Substring(kierowcaPhonePos)
                : beforeCiagnik;

            // Metoda 1: Szukaj 2 słów WIELKIMI LITERAMI (typowy format hodowcy)
            var hodowcaUpperMatch = Regex.Match(hodowcaContext,
                @"([A-ZŻŹĆĄŚĘŁÓŃ]{3,})\s+([A-ZŻŹĆĄŚĘŁÓŃ]{3,})(?:\s*/\s*[A-ZŻŹĆĄŚĘŁÓŃ]+)?");

            if (hodowcaUpperMatch.Success)
            {
                string h1 = hodowcaUpperMatch.Groups[1].Value;
                string h2 = hodowcaUpperMatch.Groups[2].Value;

                if (!IsHeaderWord(h1) && !IsHeaderWord(h2) && !IsPlaceName(h1))
                {
                    row.HodowcaNazwa = $"{h1} {h2}";
                }
            }

            // Metoda 2: Jeśli nie znaleziono, szukaj przed GPS
            if (string.IsNullOrEmpty(row.HodowcaNazwa))
            {
                var gpsMatch = Regex.Match(hodowcaContext, @"(\d{2}\.\d{4,})[,\s]+(\d{2}\.\d{4,})");
                if (gpsMatch.Success)
                {
                    row.HodowcaGpsLat = gpsMatch.Groups[1].Value;
                    row.HodowcaGpsLon = gpsMatch.Groups[2].Value;

                    string beforeGps = hodowcaContext.Substring(0, gpsMatch.Index);
                    // Usuń numer adresowy z końca
                    beforeGps = Regex.Replace(beforeGps, @"\s*\d+[A-Za-z]?\s*$", "").Trim();

                    var words = Regex.Matches(beforeGps, @"([A-ZŻŹĆĄŚĘŁÓŃ]{3,})");
                    var validWords = words.Cast<Match>()
                        .Select(m => m.Value)
                        .Where(w => !IsHeaderWord(w) && !IsPlaceName(w))
                        .ToList();

                    if (validWords.Count >= 2)
                    {
                        row.HodowcaNazwa = $"{validWords[0]} {validWords[1]}";
                    }
                }
            }

            // GPS (jeśli nie ustawione)
            if (string.IsNullOrEmpty(row.HodowcaGpsLat))
            {
                var gpsMatch = Regex.Match(beforeCiagnik, @"(\d{2}[.,]\d{4,})[,\s]+(\d{2}[.,]\d{4,})");
                if (gpsMatch.Success)
                {
                    row.HodowcaGpsLat = gpsMatch.Groups[1].Value.Replace(",", ".");
                    row.HodowcaGpsLon = gpsMatch.Groups[2].Value.Replace(",", ".");
                }
            }

            // Kod pocztowy i miejscowość
            var kodMatch = Regex.Match(beforeCiagnik, @"(\d{2}-\d{3})\s+([A-ZŻŹĆĄŚĘŁÓŃ]+)");
            if (kodMatch.Success)
            {
                row.HodowcaKodPocztowy = kodMatch.Groups[1].Value;
                row.HodowcaMiejscowosc = kodMatch.Groups[2].Value;
            }

            // === DANE Z afterNaczepa (ilości, godziny, obserwacje) ===
            ParseAfterNaczepaData(afterNaczepa, row);
        }

        /// <summary>
        /// Parsuje dane z tekstu po naczepie (ilości, godziny, obserwacje)
        /// </summary>
        private void ParseAfterNaczepaData(string context, AvilogTransportRow row)
        {
            // ILOŚĆ SZTUK (np. "4 224" lub "5 544")
            var sztukiMatch = Regex.Match(context, @"(\d)\s(\d{3})(?:\s|$|\n|[^0-9])");
            if (sztukiMatch.Success)
            {
                string sztuki = sztukiMatch.Groups[1].Value + sztukiMatch.Groups[2].Value;
                if (int.TryParse(sztuki, out int szt) && szt > 1000 && szt < 20000)
                {
                    row.Sztuki = szt;
                }
            }

            // WYMIAR SKRZYŃ (np. "16 x 264")
            var wymiaryMatch = Regex.Match(context, @"(\d+)\s*[xX]\s*(\d+)");
            if (wymiaryMatch.Success)
            {
                row.WymiarSkrzyn = $"{wymiaryMatch.Groups[1].Value} x {wymiaryMatch.Groups[2].Value}";
            }

            // WAGA (np. "2.80 Kg")
            var wagaMatch = Regex.Match(context, @"(\d+[.,]\d+)\s*Kg", RegexOptions.IgnoreCase);
            if (wagaMatch.Success)
            {
                string waga = wagaMatch.Groups[1].Value.Replace(",", ".");
                if (decimal.TryParse(waga, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal w))
                {
                    row.WagaDek = w;
                }
            }

            // GODZINY - szukaj sekwencji 3 godzin
            var allTimes = Regex.Matches(context, @"(\d{2}):(\d{2})");
            List<(TimeSpan Time, int Position)> validTimes = new List<(TimeSpan, int)>();

            foreach (Match g in allTimes)
            {
                if (int.TryParse(g.Groups[1].Value, out int h) && int.TryParse(g.Groups[2].Value, out int m))
                {
                    if (h >= 0 && h < 24 && m >= 0 && m < 60)
                    {
                        // Pomijamy godzinę jeśli jest częścią daty (np. "02/12/2025 11:37")
                        bool isHeaderTime = false;
                        if (g.Index >= 11)
                        {
                            string before = context.Substring(Math.Max(0, g.Index - 11), Math.Min(11, g.Index));
                            if (Regex.IsMatch(before, @"\d{2}/\d{2}/\d{4}\s*$"))
                            {
                                isHeaderTime = true;
                            }
                        }

                        if (!isHeaderTime)
                        {
                            validTimes.Add((new TimeSpan(h, m, 0), g.Index));
                        }
                    }
                }
            }

            // Przypisz godziny (WYJAZD, ZAŁADUNEK, POWRÓT)
            if (validTimes.Count >= 3)
            {
                row.WyjazdZaklad = DateTime.Today.Add(validTimes[0].Time);
                row.PoczatekZaladunku = validTimes[1].Time;
                row.PowrotZaklad = DateTime.Today.Add(validTimes[2].Time);
            }
            else if (validTimes.Count >= 2)
            {
                row.WyjazdZaklad = DateTime.Today.Add(validTimes[0].Time);
                row.PowrotZaklad = DateTime.Today.Add(validTimes[1].Time);
            }

            // OBSERWACJE (wózek)
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
        /// Sprawdza czy słowo to nazwa miejscowości (nie nazwisko)
        /// </summary>
        private bool IsPlaceName(string word)
        {
            // Typowe polskie nazwy miejscowości które mogą być mylone z nazwiskami
            var placeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "MICHAŁÓW", "MICHALOW", "KUTNO", "SŁUBICE", "SLUBICE", "STRAWCZYN",
                "OBLEGOREK", "OBLĘGOREK", "KOLUSZKI", "GODZIANÓW", "GODZIANOW",
                "JEŻÓW", "JEZOW", "DOBRZYKÓW", "DOBRZYKOW", "JORDANÓW", "JORDANOW",
                "FELICJANÓW", "FELICJANOW", "ZAPADY", "STUDZIENIEC", "MOŚCISKA", "MOSCISKA"
            };

            return placeNames.Contains(word.ToUpper());
        }

        /// <summary>
        /// Parsuje dane z kontekstu wokół pozycji pojazdu
        /// </summary>
        private void ParseContextData(string context, AvilogTransportRow row)
        {
            // ============ ILOŚĆ SZTUK ============
            var sztukiMatch = Regex.Match(context, @"(\d)\s(\d{3})(?:\s|$|\n|[^0-9])");
            if (sztukiMatch.Success)
            {
                string sztuki = sztukiMatch.Groups[1].Value + sztukiMatch.Groups[2].Value;
                if (int.TryParse(sztuki, out int szt) && szt > 1000 && szt < 20000)
                {
                    row.Sztuki = szt;
                }
            }

            // ============ WYMIAR SKRZYŃ ============
            var wymiaryMatch = Regex.Match(context, @"(\d+)\s*x\s*(\d+)");
            if (wymiaryMatch.Success)
            {
                row.WymiarSkrzyn = $"{wymiaryMatch.Groups[1].Value} x {wymiaryMatch.Groups[2].Value}";
            }

            // ============ WAGA ============
            var wagaMatch = Regex.Match(context, @"(\d+[.,]\d+)\s*Kg", RegexOptions.IgnoreCase);
            if (wagaMatch.Success)
            {
                string waga = wagaMatch.Groups[1].Value.Replace(",", ".");
                if (decimal.TryParse(waga, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal w))
                {
                    row.WagaDek = w;
                }
            }

            // ============ GODZINY ============
            // W tekście PDF kolejność to: ZAŁADUNEK (pierwsza), WYJAZD (druga), POWRÓT (trzecia)
            // Pomijamy godziny z nagłówka strony (dd/MM/yyyy HH:mm)
            var allTimes = Regex.Matches(context, @"(\d{2}):(\d{2})");
            List<(TimeSpan Time, int Position)> validTimes = new List<(TimeSpan, int)>();

            foreach (Match g in allTimes)
            {
                if (int.TryParse(g.Groups[1].Value, out int h) && int.TryParse(g.Groups[2].Value, out int m))
                {
                    if (h >= 0 && h < 24 && m >= 0 && m < 60)
                    {
                        // Pomijamy godzinę jeśli jest częścią daty (np. "02/12/2025 11:37")
                        bool isHeaderTime = false;
                        if (g.Index >= 11)
                        {
                            string before = context.Substring(g.Index - 11, 11);
                            if (Regex.IsMatch(before, @"\d{2}/\d{2}/\d{4}\s+$"))
                            {
                                isHeaderTime = true;
                            }
                        }

                        if (!isHeaderTime)
                        {
                            validTimes.Add((new TimeSpan(h, m, 0), g.Index));
                        }
                    }
                }
            }

            // Szukaj sekwencji 3 godzin blisko siebie (max 100 znaków)
            for (int i = 0; i < validTimes.Count - 2; i++)
            {
                int dist1 = validTimes[i + 1].Position - validTimes[i].Position;
                int dist2 = validTimes[i + 2].Position - validTimes[i + 1].Position;

                if (dist1 < 100 && dist2 < 100)
                {
                    // Kolejność w tekście: ZAŁADUNEK, WYJAZD, POWRÓT
                    row.PoczatekZaladunku = validTimes[i].Time;
                    row.WyjazdZaklad = DateTime.Today.Add(validTimes[i + 1].Time);
                    row.PowrotZaklad = DateTime.Today.Add(validTimes[i + 2].Time);
                    break;
                }
            }

            // ============ KROK 1: Znajdź wszystkie telefony ============
            var allPhones = Regex.Matches(context, @"(\d{3})\s*(\d{3})\s*(\d{3})");
            string kierowcaTelefon = "";
            string hodowcaTelefon = "";
            int kierowcaPhonePos = -1;
            int hodowcaPhonePos = -1;

            // Znajdź "Tel. :" - telefon po tym to HODOWCA
            var telColonMatch = Regex.Match(context, @"Tel\s*\.\s*:\s*(\d{3})\s*(\d{3})\s*(\d{3})");
            if (telColonMatch.Success)
            {
                hodowcaTelefon = telColonMatch.Groups[1].Value + telColonMatch.Groups[2].Value + telColonMatch.Groups[3].Value;
                hodowcaPhonePos = telColonMatch.Index;
                row.HodowcaTelefon = hodowcaTelefon;
            }

            // Pierwszy telefon który nie jest hodowcy to KIEROWCA
            foreach (Match phone in allPhones)
            {
                string phoneNum = phone.Groups[1].Value + phone.Groups[2].Value + phone.Groups[3].Value;
                if (phoneNum != hodowcaTelefon)
                {
                    kierowcaTelefon = phoneNum;
                    kierowcaPhonePos = phone.Index;
                    row.KierowcaTelefon = kierowcaTelefon;
                    break;
                }
            }

            // ============ KROK 2: KIEROWCA - szukaj imię i nazwisko przed telefonem ============
            if (kierowcaPhonePos > 0)
            {
                string beforeKierowcaPhone = context.Substring(0, kierowcaPhonePos);

                // Szukaj 2 słów (imion/nazwisk) przed telefonem
                // Wzorzec: dowolne słowa zaczynające się wielką literą
                var nameWords = Regex.Matches(beforeKierowcaPhone, @"([A-ZŻŹĆĄŚĘŁÓŃ][a-zżźćąśęłóń]{2,}|[A-ZŻŹĆĄŚĘŁÓŃ]{3,})");

                if (nameWords.Count >= 2)
                {
                    // Weź ostatnie 2 słowa przed telefonem
                    string word1 = nameWords[nameWords.Count - 2].Value;
                    string word2 = nameWords[nameWords.Count - 1].Value;

                    if (!IsHeaderWord(word1) && !IsHeaderWord(word2))
                    {
                        row.KierowcaNazwa = $"{word1} {word2}";
                    }
                }
                else if (nameWords.Count == 1)
                {
                    string word1 = nameWords[0].Value;
                    if (!IsHeaderWord(word1))
                    {
                        row.KierowcaNazwa = word1;
                    }
                }
            }

            // ============ KROK 3: HODOWCA - szukaj nazwę przed "Tel." ============
            // Hodowca ma format: NAZWISKO IMIĘ adres GPS kod Tel. : telefon
            // Szukaj wzorca: 2 słowa (UPPERCASE) + coś + GPS lub kod pocztowy + Tel.

            // Metoda 1: Szukaj wzorca NAZWISKO IMIĘ przed adresem
            var hodowcaPattern = Regex.Match(context,
                @"([A-ZŻŹĆĄŚĘŁÓŃ]{3,})\s+([A-ZŻŹĆĄŚĘŁÓŃ]{3,})(?:\s*/\s*[A-ZŻŹĆĄŚĘŁÓŃ]+)?\s+[A-Za-zżźćąśęłóńŻŹĆĄŚĘŁÓŃ\s]*\d",
                RegexOptions.None);

            if (hodowcaPattern.Success)
            {
                string h1 = hodowcaPattern.Groups[1].Value;
                string h2 = hodowcaPattern.Groups[2].Value;

                // Upewnij się że to nie jest kierowca
                if (!IsHeaderWord(h1) && !IsHeaderWord(h2) &&
                    h1.ToUpper() != row.KierowcaNazwa?.Split(' ').FirstOrDefault()?.ToUpper() &&
                    h2.ToUpper() != row.KierowcaNazwa?.Split(' ').LastOrDefault()?.ToUpper())
                {
                    row.HodowcaNazwa = $"{h1} {h2}";
                }
            }

            // Metoda 2: Jeśli nie znaleziono, szukaj mixed case przed GPS
            if (string.IsNullOrEmpty(row.HodowcaNazwa))
            {
                var gpsMatch = Regex.Match(context, @"(\d{2}\.\d{4,})[,\s]+(\d{2}\.\d{4,})");
                if (gpsMatch.Success)
                {
                    row.HodowcaGpsLat = gpsMatch.Groups[1].Value;
                    row.HodowcaGpsLon = gpsMatch.Groups[2].Value;

                    string beforeGps = context.Substring(0, gpsMatch.Index);
                    // Usuń adres (liczby na końcu)
                    beforeGps = Regex.Replace(beforeGps, @"\s*\d+[A-Za-z]?\s*$", "").Trim();

                    // Szukaj 2 słów przed GPS
                    var words = Regex.Matches(beforeGps, @"([A-ZŻŹĆĄŚĘŁÓŃa-zżźćąśęłóń]{3,})");
                    if (words.Count >= 2)
                    {
                        string w1 = words[words.Count - 2].Value;
                        string w2 = words[words.Count - 1].Value;

                        // Nie bierz słów które są częścią kierowcy
                        string kierowcaUpper = row.KierowcaNazwa?.ToUpper() ?? "";
                        if (!IsHeaderWord(w1) && !IsHeaderWord(w2) &&
                            !kierowcaUpper.Contains(w1.ToUpper()) &&
                            !kierowcaUpper.Contains(w2.ToUpper()))
                        {
                            row.HodowcaNazwa = $"{w1} {w2}";
                        }
                    }
                }
            }

            // Metoda 3: Szukaj po kodzie pocztowym
            if (string.IsNullOrEmpty(row.HodowcaNazwa))
            {
                var kodMatch = Regex.Match(context, @"(\d{2}-\d{3})\s+([A-ZŻŹĆĄŚĘŁÓŃ]+)");
                if (kodMatch.Success)
                {
                    row.HodowcaKodPocztowy = kodMatch.Groups[1].Value;
                    row.HodowcaMiejscowosc = kodMatch.Groups[2].Value;

                    string beforeKod = context.Substring(0, kodMatch.Index);
                    beforeKod = Regex.Replace(beforeKod, @"\s*\d+[A-Za-z]?\s*$", "").Trim();

                    var words = Regex.Matches(beforeKod, @"([A-ZŻŹĆĄŚĘŁÓŃa-zżźćąśęłóń]{3,})");
                    if (words.Count >= 2)
                    {
                        string w1 = words[words.Count - 2].Value;
                        string w2 = words[words.Count - 1].Value;

                        string kierowcaUpper = row.KierowcaNazwa?.ToUpper() ?? "";
                        if (!IsHeaderWord(w1) && !IsHeaderWord(w2) &&
                            !kierowcaUpper.Contains(w1.ToUpper()) &&
                            !kierowcaUpper.Contains(w2.ToUpper()))
                        {
                            row.HodowcaNazwa = $"{w1} {w2}";
                        }
                    }
                }
            }

            // ============ OBSERWACJE (WÓZEK) ============
            if (context.Contains("Wózek w obie strony") || context.Contains("Wozek w obie strony"))
                row.Obserwacje = "Wózek w obie strony";
            else if (context.Contains("Wieziesz wózek") || context.Contains("Wieziesz wozek"))
                row.Obserwacje = "Wieziesz wózek";
            else if (context.Contains("Zabierasz wózek") || context.Contains("Zabierasz wozek"))
                row.Obserwacje = "Zabierasz wózek";
            else if (context.Contains("Przywozisz wózek") || context.Contains("Przywozisz wozek"))
                row.Obserwacje = "Przywozisz wózek";

            // ============ ADRES HODOWCY (uzupełnienie) ============
            // Szukaj kodu pocztowego i miejscowości jeśli nie zostały jeszcze ustawione
            if (string.IsNullOrEmpty(row.HodowcaKodPocztowy))
            {
                var kodMiejscMatch2 = Regex.Match(context, @"(\d{2}-\d{3})\s+([A-ZŻŹĆĄŚĘŁÓŃ]+)");
                if (kodMiejscMatch2.Success)
                {
                    row.HodowcaKodPocztowy = kodMiejscMatch2.Groups[1].Value;
                    row.HodowcaMiejscowosc = kodMiejscMatch2.Groups[2].Value;
                }
            }

            // Szukaj współrzędnych GPS jeśli nie zostały jeszcze ustawione
            if (string.IsNullOrEmpty(row.HodowcaGpsLat))
            {
                var gpsMatch2 = Regex.Match(context, @"(\d{2}[.,]\d{4,})[,\s]+(\d{2}[.,]\d{4,})");
                if (gpsMatch2.Success)
                {
                    row.HodowcaGpsLat = gpsMatch2.Groups[1].Value.Replace(",", ".");
                    row.HodowcaGpsLon = gpsMatch2.Groups[2].Value.Replace(",", ".");
                }
            }
        }

        /// <summary>
        /// Sprawdza czy słowo to nagłówek/słowo kluczowe (nie nazwa osoby)
        /// </summary>
        private bool IsHeaderWord(string word)
        {
            if (string.IsNullOrEmpty(word)) return true;

            var headerWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "KIEROWCA", "HODOWCA", "POJAZD", "AVILOG", "POLSKA", "DATA", "UBOJU",
                "OBSERWACJE", "ZAKŁAD", "ILOŚĆ", "SZTUK", "WAGA", "GODZINA", "WYJAZD",
                "POWRÓT", "ZAŁADUNEK", "TRANSPORT", "PLANOWANIE", "CIĄGNIK", "NACZEPA",
                "TELEFON", "ADRES", "MIEJSCOWOŚĆ", "GPS", "LAT", "LONG", "LATITUDE", "LONGITUDE"
            };

            return headerWords.Contains(word.ToUpper());
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
