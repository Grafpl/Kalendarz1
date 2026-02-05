using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Kalendarz1.MarketIntelligence.Models;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Logger zapisujacy szczegolowe logi pipeline briefingu do pliku TXT.
    /// Automatycznie tworzy nowy plik dla kazdej sesji w Documents\PiorkaBriefing\Logs
    /// </summary>
    public class BriefingFileLogger : IDisposable
    {
        private readonly StringBuilder _buffer;
        private readonly string _sessionId;
        private readonly DateTime _startTime;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        /// <summary>
        /// Sciezka do pliku logow
        /// </summary>
        public string LogFilePath { get; private set; }

        /// <summary>
        /// Folder z logami
        /// </summary>
        public string LogsFolder { get; private set; }

        /// <summary>
        /// Tryb pracy (Pelny/Szybki/Test)
        /// </summary>
        public string Mode { get; set; } = "Nieznany";

        public BriefingFileLogger()
        {
            _buffer = new StringBuilder(50000); // Pre-alloc 50KB
            _startTime = DateTime.Now;
            _sessionId = _startTime.ToString("yyyy-MM-dd_HH-mm-ss");
            _stopwatch = Stopwatch.StartNew();

            // Folder: Documents\PiorkaBriefing\Logs
            LogsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PiorkaBriefing", "Logs");

            try
            {
                Directory.CreateDirectory(LogsFolder);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BriefingFileLogger] Nie mozna utworzyc folderu logow: {ex.Message}");
                // Fallback do temp
                LogsFolder = Path.Combine(Path.GetTempPath(), "PiorkaBriefing", "Logs");
                Directory.CreateDirectory(LogsFolder);
            }

            LogFilePath = Path.Combine(LogsFolder, $"briefing_{_sessionId}.txt");

            // Naglowek sesji
            WriteHeader();
        }

        private void WriteHeader()
        {
            _buffer.AppendLine(new string('═', 80));
            _buffer.AppendLine($" SESJA PORANNY BRIEFING - {_startTime:yyyy-MM-dd HH:mm:ss}");
            _buffer.AppendLine($" Plik: {LogFilePath}");
            _buffer.AppendLine(new string('═', 80));
            _buffer.AppendLine();
        }

        /// <summary>
        /// Zapisuje naglowek sekcji
        /// </summary>
        public void LogSection(string title)
        {
            _buffer.AppendLine();
            _buffer.AppendLine(new string('═', 80));
            _buffer.AppendLine($" {title}");
            _buffer.AppendLine(new string('═', 80));
        }

        /// <summary>
        /// Zapisuje podsekcje (mniejszy naglowek)
        /// </summary>
        public void LogSubSection(string title)
        {
            _buffer.AppendLine();
            _buffer.AppendLine($"--- {title} ---");
        }

        /// <summary>
        /// Zapisuje linie logu z timestampem i poziomem
        /// </summary>
        public void Log(string message, string level = "INFO")
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var prefix = level.ToUpperInvariant() switch
            {
                "ERROR" => "[ERROR]",
                "WARN" => "[WARN] ",
                "WARNING" => "[WARN] ",
                "OK" => "[OK]   ",
                "SUCCESS" => "[OK]   ",
                "DEBUG" => "[DEBUG]",
                _ => "[INFO] "
            };
            _buffer.AppendLine($"{timestamp} {prefix} {message}");
        }

        /// <summary>
        /// Zapisuje blad z pelnym stack trace
        /// </summary>
        public void LogError(string message, Exception ex = null)
        {
            Log(message, "ERROR");
            if (ex != null)
            {
                _buffer.AppendLine($"           Exception: {ex.GetType().Name}");
                _buffer.AppendLine($"           Message: {ex.Message}");
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    _buffer.AppendLine("           StackTrace:");
                    foreach (var line in ex.StackTrace.Split('\n'))
                    {
                        _buffer.AppendLine($"             {line.Trim()}");
                    }
                }
            }
        }

        /// <summary>
        /// Zapisuje surowa zawartosc (np. odpowiedz API)
        /// </summary>
        public void LogRaw(string label, string content, int maxLength = 0)
        {
            _buffer.AppendLine();
            _buffer.AppendLine($"--- {label} START ---");

            if (string.IsNullOrEmpty(content))
            {
                _buffer.AppendLine("[BRAK ZAWARTOSCI / NULL]");
            }
            else
            {
                var toWrite = maxLength > 0 && content.Length > maxLength
                    ? content.Substring(0, maxLength) + $"\n... [OBCIETO - pelna dlugosc: {content.Length} znakow]"
                    : content;
                _buffer.AppendLine(toWrite);
            }

            _buffer.AppendLine($"--- {label} END ---");
            _buffer.AppendLine();
        }

        /// <summary>
        /// Loguje informacje o konfiguracji API
        /// </summary>
        public void LogApiConfig(string openAiKey, bool openAiConfigured, string braveKey, bool braveConfigured, string model)
        {
            LogSection("KONFIGURACJA API");
            Log($"OpenAI API: {(openAiConfigured ? "SKONFIGUROWANE" : "BRAK")}", openAiConfigured ? "OK" : "ERROR");
            Log($"OpenAI Key Preview: {openAiKey}");
            Log($"Model: {model}");
            Log($"Brave Search API: {(braveConfigured ? "SKONFIGUROWANE" : "BRAK")}", braveConfigured ? "OK" : "ERROR");
            Log($"Brave Key Preview: {braveKey}");
            Log($"Tryb: {Mode}");
        }

        /// <summary>
        /// Loguje wyniki wyszukiwania Brave
        /// </summary>
        public void LogBraveSearch(string query, string url, int statusCode, long elapsedMs, int articlesFound, string rawResponse = null)
        {
            LogSection("ETAP 1: BRAVE SEARCH");
            Log($"Query: \"{query}\"");
            Log($"URL: {url}");
            Log($"Status HTTP: {statusCode}", statusCode == 200 ? "OK" : "WARN");
            Log($"Czas odpowiedzi: {elapsedMs}ms");
            Log($"Znalezione artykuly: {articlesFound}", articlesFound > 0 ? "OK" : "WARN");

            if (!string.IsNullOrEmpty(rawResponse))
            {
                LogRaw("BRAVE RAW RESPONSE", rawResponse, 5000); // Max 5KB
            }
        }

        /// <summary>
        /// Loguje liste kandydatow z Brave
        /// </summary>
        public void LogCandidates(List<(string Title, string Url, string Source)> candidates)
        {
            LogSubSection($"LISTA KANDYDATOW ({candidates.Count})");
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                _buffer.AppendLine($"  [{i + 1,2}] {Truncate(c.Title, 60)}");
                _buffer.AppendLine($"       URL: {c.Url}");
                _buffer.AppendLine($"       Zrodlo: {c.Source}");
            }
        }

        /// <summary>
        /// Loguje przetwarzanie pojedynczego artykulu
        /// </summary>
        public void LogArticleProcessingStart(int index, int total, string title, string url)
        {
            LogSection($"PRZETWARZANIE ARTYKULU {index}/{total}");
            Log($"Tytul: {title}");
            Log($"URL: {url}");
        }

        /// <summary>
        /// Loguje wynik enrichmentu (pobierania tresci)
        /// </summary>
        public void LogEnrichment(string method, int statusCode, long elapsedMs, int contentLength, string contentPreview, bool isFallback)
        {
            LogSubSection("ENRICHMENT (POBIERANIE TRESCI)");
            Log($"Metoda: {method}", isFallback ? "WARN" : "OK");
            if (statusCode > 0)
            {
                Log($"Status HTTP: {statusCode}", statusCode == 200 ? "OK" : "WARN");
            }
            Log($"Czas: {elapsedMs}ms");
            Log($"Dlugosc tresci: {contentLength} znakow", contentLength >= 50 ? "OK" : "WARN");
            Log($"Fallback: {(isFallback ? "TAK (uzyto snippetu)" : "NIE")}");

            if (!string.IsNullOrEmpty(contentPreview))
            {
                LogRaw("POBRANA TRESC (pierwsze 1000 znakow)", contentPreview, 1000);
            }
        }

        /// <summary>
        /// Loguje wyslanie do OpenAI
        /// </summary>
        public void LogOpenAiRequest(int promptLength)
        {
            LogSubSection("OPENAI ANALYSIS");
            Log($"Wysylam do OpenAI...");
            Log($"Dlugosc promptu: {promptLength} znakow");
        }

        /// <summary>
        /// Loguje odpowiedz OpenAI
        /// </summary>
        public void LogOpenAiResponse(long elapsedMs, bool success, string rawResponse = null, string parsingError = null)
        {
            Log($"Czas odpowiedzi: {elapsedMs}ms", elapsedMs < 60000 ? "OK" : "WARN");
            Log($"Status: {(success ? "SUKCES" : "BLAD")}", success ? "OK" : "ERROR");

            if (!string.IsNullOrEmpty(rawResponse))
            {
                LogRaw("OPENAI RAW RESPONSE", rawResponse, 10000); // Max 10KB
            }

            if (!string.IsNullOrEmpty(parsingError))
            {
                LogRaw("BLAD PARSOWANIA", parsingError);
            }
        }

        /// <summary>
        /// Loguje pelny wynik analizy artykulu
        /// </summary>
        public void LogArticleResult(BriefingArticle article, int index)
        {
            LogSection($"WYNIK ARTYKULU {index}: {Truncate(article.Title, 50)}");

            // Metadane
            Log($"SmartTitle: {article.SmartTitle ?? "[BRAK]"}");
            Log($"Kategoria: {article.Category}");
            Log($"Severity: {article.Severity}");
            Log($"Sentiment: {article.SentimentScore:F2}");
            Log($"Impact: {article.Impact}");
            Log($"Zrodlo: {article.Source}");
            Log($"URL: {article.SourceUrl}");
            Log($"Tagi: {string.Join(", ", article.Tags ?? new List<string>())}");

            // Sprawdz czy to fallback/raw article
            if (article.FullContent?.Contains("[ARTYKUL BEZ ANALIZY AI") == true)
            {
                Log("UWAGA: Artykul bez analizy AI!", "WARN");
            }

            // Sekcje tresciowe
            LogContentSection("STRESZCZENIE", article.FullContent);
            LogContentSection("KONTEKST RYNKOWY", article.MarketContext);
            LogContentSection("KIM JEST / CO TO JEST", article.EducationalSection);
            LogContentSection("TLUMACZENIE POJEC", article.TermsExplanation);
            LogContentSection("ANALIZA CEO", article.AiAnalysisCeo);
            LogContentSection("ANALIZA HANDLOWIEC", article.AiAnalysisSales);
            LogContentSection("ANALIZA ZAKUPOWIEC", article.AiAnalysisBuyer);
            LogContentSection("LEKCJA BRANZOWA", article.IndustryLesson);
            LogContentSection("AKCJE CEO", article.RecommendedActionsCeo);
            LogContentSection("AKCJE HANDLOWIEC", article.RecommendedActionsSales);
            LogContentSection("AKCJE ZAKUPOWIEC", article.RecommendedActionsBuyer);
            LogContentSection("PYTANIA STRATEGICZNE", article.StrategicQuestions);
            LogContentSection("ZRODLA DO MONITOROWANIA", article.SourcesToMonitor);
        }

        private void LogContentSection(string name, string content)
        {
            _buffer.AppendLine();
            _buffer.AppendLine($"=== {name} ===");

            if (string.IsNullOrWhiteSpace(content))
            {
                _buffer.AppendLine("[BRAK]");
            }
            else if (content.Contains("niedostepna") || content.Contains("niedostępna"))
            {
                _buffer.AppendLine($"[NIEDOSTEPNE] {content}");
            }
            else
            {
                _buffer.AppendLine(content);
            }
        }

        /// <summary>
        /// Loguje podsumowanie sesji
        /// </summary>
        public void LogSummary(int totalArticles, int successCount, int failedCount, int withAiAnalysis, int withoutAiAnalysis)
        {
            _stopwatch.Stop();

            LogSection("PODSUMOWANIE SESJI");
            Log($"Czas trwania: {_stopwatch.Elapsed.TotalSeconds:F1} sekund");
            Log($"Artykulow przetworzonych: {totalArticles}");
            Log($"Sukces: {successCount}", successCount > 0 ? "OK" : "WARN");
            Log($"Bledy: {failedCount}", failedCount == 0 ? "OK" : "ERROR");
            Log($"Z analiza AI: {withAiAnalysis}");
            Log($"Bez analizy AI (fallback): {withoutAiAnalysis}", withoutAiAnalysis == 0 ? "OK" : "WARN");
            Log($"Plik logow: {LogFilePath}");
        }

        /// <summary>
        /// Zapisuje logi do pliku
        /// </summary>
        public void SaveToFile()
        {
            try
            {
                _buffer.AppendLine();
                _buffer.AppendLine(new string('═', 80));
                _buffer.AppendLine($" KONIEC LOGOW - {DateTime.Now:HH:mm:ss}");
                _buffer.AppendLine(new string('═', 80));

                File.WriteAllText(LogFilePath, _buffer.ToString(), Encoding.UTF8);
                Debug.WriteLine($"[BriefingFileLogger] Zapisano logi do: {LogFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BriefingFileLogger] Blad zapisu logow: {ex.Message}");
            }
        }

        /// <summary>
        /// Otwiera folder z logami w eksploratorze
        /// </summary>
        public void OpenLogsFolder()
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", LogsFolder);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BriefingFileLogger] Nie mozna otworzyc folderu: {ex.Message}");
            }
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "[BRAK]";
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                SaveToFile();
                _disposed = true;
            }
        }
    }
}
