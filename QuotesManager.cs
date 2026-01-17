using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Kalendarz1
{
    /// <summary>
    /// Zarządza cytatami motywacyjnymi przechowywanymi lokalnie.
    /// Admin może importować/edytować cytaty z pliku JSON.
    /// </summary>
    public static class QuotesManager
    {
        private static readonly string QuotesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZPSP", "Quotes");

        private static readonly string QuotesFilePath = Path.Combine(QuotesDirectory, "quotes.json");

        // Domyślne cytaty (używane gdy brak własnych)
        private static readonly List<Quote> DefaultQuotes = new List<Quote>
        {
            new Quote { Text = "Sukces to suma małych wysiłków powtarzanych dzień po dniu.", Author = "Robert Collier" },
            new Quote { Text = "Jedynym sposobem na świetną pracę jest kochać to, co robisz.", Author = "Steve Jobs" },
            new Quote { Text = "Przyszłość należy do tych, którzy wierzą w piękno swoich marzeń.", Author = "Eleanor Roosevelt" },
            new Quote { Text = "Nie czekaj na idealny moment. Weź moment i uczyń go idealnym." },
            new Quote { Text = "Sukces nie jest kluczem do szczęścia. Szczęście jest kluczem do sukcesu.", Author = "Albert Schweitzer" },
            new Quote { Text = "Droga do sukcesu jest zawsze w budowie.", Author = "Lily Tomlin" },
            new Quote { Text = "Każdy dzień to nowa szansa, by zmienić swoje życie." },
            new Quote { Text = "Wielkie rzeczy nigdy nie przychodzą ze strefy komfortu." },
            new Quote { Text = "Postęp jest niemożliwy bez zmiany.", Author = "George Bernard Shaw" },
            new Quote { Text = "Zacznij tam, gdzie jesteś. Użyj tego, co masz. Zrób to, co możesz.", Author = "Arthur Ashe" },
            new Quote { Text = "Odwaga nie jest brakiem strachu, ale działaniem mimo niego.", Author = "Mark Twain" },
            new Quote { Text = "Najlepszy czas na posadzenie drzewa był 20 lat temu. Drugi najlepszy czas jest teraz." },
            new Quote { Text = "Twój czas jest ograniczony. Nie marnuj go żyjąc cudzym życiem.", Author = "Steve Jobs" },
            new Quote { Text = "Nie licz dni, spraw, by dni się liczyły.", Author = "Muhammad Ali" },
            new Quote { Text = "Jakość nie jest dziełem przypadku. Jest wynikiem inteligentnego wysiłku.", Author = "John Ruskin" },
            new Quote { Text = "Nie ma windy do sukcesu. Musisz iść po schodach.", Author = "Zig Ziglar" },
            new Quote { Text = "Rób to, czego się boisz, a strach na pewno zniknie.", Author = "Ralph Waldo Emerson" },
            new Quote { Text = "Praca zespołowa sprawia, że marzenia się spełniają." },
            new Quote { Text = "Bądź zmianą, którą chcesz widzieć w świecie.", Author = "Mahatma Gandhi" },
            new Quote { Text = "Jedyną granicą naszych jutrzejszych osiągnięć są nasze dzisiejsze wątpliwości.", Author = "Franklin D. Roosevelt" }
        };

        /// <summary>
        /// Model cytatu
        /// </summary>
        public class Quote
        {
            public string Text { get; set; }
            public string Author { get; set; }
        }

        /// <summary>
        /// Sprawdza czy istnieją własne cytaty
        /// </summary>
        public static bool HasCustomQuotes()
        {
            return File.Exists(QuotesFilePath);
        }

        /// <summary>
        /// Pobiera wszystkie cytaty (własne lub domyślne)
        /// </summary>
        public static List<Quote> GetAllQuotes()
        {
            try
            {
                if (File.Exists(QuotesFilePath))
                {
                    string json = File.ReadAllText(QuotesFilePath);
                    var quotes = JsonSerializer.Deserialize<List<Quote>>(json);
                    if (quotes != null && quotes.Count > 0)
                        return quotes;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAllQuotes error: {ex.Message}");
            }

            return DefaultQuotes;
        }

        /// <summary>
        /// Pobiera cytat dnia (na podstawie dnia roku)
        /// </summary>
        public static Quote GetQuoteOfTheDay()
        {
            var quotes = GetAllQuotes();
            int dayOfYear = DateTime.Now.DayOfYear;
            int index = dayOfYear % quotes.Count;
            return quotes[index];
        }

        /// <summary>
        /// Pobiera losowy cytat
        /// </summary>
        public static Quote GetRandomQuote()
        {
            var quotes = GetAllQuotes();
            var random = new Random();
            return quotes[random.Next(quotes.Count)];
        }

        /// <summary>
        /// Zapisuje listę cytatów
        /// </summary>
        public static bool SaveQuotes(List<Quote> quotes)
        {
            try
            {
                if (!Directory.Exists(QuotesDirectory))
                    Directory.CreateDirectory(QuotesDirectory);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string json = JsonSerializer.Serialize(quotes, options);
                File.WriteAllText(QuotesFilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveQuotes error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Dodaje nowy cytat
        /// </summary>
        public static bool AddQuote(string text, string author = null)
        {
            try
            {
                var quotes = GetAllQuotes().ToList();
                quotes.Add(new Quote { Text = text, Author = author });
                return SaveQuotes(quotes);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Usuwa cytat
        /// </summary>
        public static bool RemoveQuote(int index)
        {
            try
            {
                var quotes = GetAllQuotes().ToList();
                if (index >= 0 && index < quotes.Count)
                {
                    quotes.RemoveAt(index);
                    return SaveQuotes(quotes);
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Importuje cytaty z pliku JSON
        /// Format: [{"Text": "cytat"}, ...] lub ["cytat1", "cytat2", ...]
        /// </summary>
        public static (bool success, int count, string error) ImportFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return (false, 0, "Plik nie istnieje");

                string json = File.ReadAllText(filePath);

                List<Quote> importedQuotes = null;

                // Próbuj parsować jako listę obiektów Quote
                try
                {
                    importedQuotes = JsonSerializer.Deserialize<List<Quote>>(json);
                }
                catch
                {
                    // Próbuj parsować jako prostą listę stringów
                    try
                    {
                        var stringList = JsonSerializer.Deserialize<List<string>>(json);
                        if (stringList != null)
                        {
                            importedQuotes = stringList.Select(s => new Quote { Text = s }).ToList();
                        }
                    }
                    catch { }
                }

                if (importedQuotes == null || importedQuotes.Count == 0)
                    return (false, 0, "Plik nie zawiera cytatów lub ma nieprawidłowy format");

                // Walidacja
                var validQuotes = importedQuotes
                    .Where(q => !string.IsNullOrWhiteSpace(q.Text))
                    .Select(q => new Quote
                    {
                        Text = q.Text.Trim(),
                        Author = string.IsNullOrWhiteSpace(q.Author) ? null : q.Author.Trim()
                    })
                    .ToList();

                if (validQuotes.Count == 0)
                    return (false, 0, "Brak prawidłowych cytatów w pliku");

                // Pobierz istniejące i dodaj nowe (unikając duplikatów)
                var existingQuotes = HasCustomQuotes() ? GetAllQuotes() : new List<Quote>();
                var existingTexts = new HashSet<string>(existingQuotes.Select(q => q.Text.ToLower()));

                int addedCount = 0;
                foreach (var quote in validQuotes)
                {
                    if (!existingTexts.Contains(quote.Text.ToLower()))
                    {
                        existingQuotes.Add(quote);
                        existingTexts.Add(quote.Text.ToLower());
                        addedCount++;
                    }
                }

                if (addedCount > 0)
                {
                    SaveQuotes(existingQuotes);
                }

                return (true, addedCount, null);
            }
            catch (JsonException)
            {
                return (false, 0, "Nieprawidłowy format JSON. Oczekiwany format:\n[{\"Text\": \"cytat\"}, ...] lub [\"cytat1\", \"cytat2\", ...]");
            }
            catch (Exception ex)
            {
                return (false, 0, $"Błąd: {ex.Message}");
            }
        }

        /// <summary>
        /// Eksportuje cytaty do pliku JSON
        /// </summary>
        public static bool ExportToFile(string filePath)
        {
            try
            {
                var quotes = GetAllQuotes();
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string json = JsonSerializer.Serialize(quotes, options);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Resetuje cytaty do domyślnych
        /// </summary>
        public static bool ResetToDefaults()
        {
            try
            {
                if (File.Exists(QuotesFilePath))
                {
                    File.Delete(QuotesFilePath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Pobiera liczbę cytatów
        /// </summary>
        public static int GetQuotesCount()
        {
            return GetAllQuotes().Count;
        }

        /// <summary>
        /// Pobiera ścieżkę do pliku cytatów
        /// </summary>
        public static string GetQuotesFilePath()
        {
            return QuotesFilePath;
        }
    }
}
