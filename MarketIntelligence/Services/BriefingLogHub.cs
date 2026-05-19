using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Centralny zbiornik logów dla modułu Poranny Briefing.
    /// Przechwytuje wszystkie Debug.WriteLine z prefiksami [Claude]/[Perplexity]/[Orchestrator] itd.
    /// i udostępnia jako:
    /// 1. Ring buffer 500 wpisów (BriefingLogHub.GetRecent())
    /// 2. Event OnMessage (do UI okna diagnostyki)
    /// 3. **Plik logu na żywo** &lt;exe-dir&gt;/logs/briefing-YYYY-MM-DD.log z AutoFlush —
    ///    nawet jak apka się zawiesi, log jest na dysku do diagnostyki
    /// </summary>
    public static class BriefingLogHub
    {
        private static readonly LinkedList<LogEntry> _buffer = new();
        private const int MaxBuffer = 500;
        private static readonly object _lock = new();
        private static bool _listenerAttached;
        private static readonly object _attachLock = new();

        // File logger — zapisuje każdy wpis NA ŻYWO do pliku (flush per write)
        private static StreamWriter _fileWriter;
        private static readonly object _fileLock = new();
        private static string _currentLogFile;

        /// <summary>Pełna ścieżka aktualnego pliku log (do pokazania w UI).</summary>
        public static string LogFilePath
        {
            get { lock (_fileLock) return _currentLogFile ?? GetLogFilePathForToday(); }
        }

        /// <summary>Folder logów (do otworzenia w Explorerze).</summary>
        public static string LogFolderPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "logs");

        private static string GetLogFilePathForToday() => Path.Combine(
            LogFolderPath, $"briefing-{DateTime.Today:yyyy-MM-dd}.log");

        private static void EnsureFileLogger()
        {
            lock (_fileLock)
            {
                var path = GetLogFilePathForToday();
                if (_currentLogFile == path && _fileWriter != null) return;

                // Zamknij stary (jeśli dzień się zmienił)
                try { _fileWriter?.Flush(); _fileWriter?.Dispose(); } catch { }
                _fileWriter = null;

                try
                {
                    Directory.CreateDirectory(LogFolderPath);
                    var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                    _fileWriter = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                    _currentLogFile = path;

                    // Marker startu sesji — łatwo znaleźć w pliku gdzie zaczęło się nowe uruchomienie
                    var sessionHeader =
                        $"{Environment.NewLine}" +
                        $"════════════════════════════════════════════════════════════════════════════{Environment.NewLine}" +
                        $"║  NOWA SESJA: {DateTime.Now:yyyy-MM-dd HH:mm:ss}  ║  PID: {Environment.ProcessId}  ║  {Environment.MachineName}\\{Environment.UserName}{Environment.NewLine}" +
                        $"════════════════════════════════════════════════════════════════════════════{Environment.NewLine}";
                    _fileWriter.Write(sessionHeader);
                }
                catch (Exception ex)
                {
                    // Fallback — file logger nie działa, ale UI/buffer wciąż OK
                    System.Diagnostics.Debug.WriteLine($"[BriefingLogHub] Nie udało się otworzyć pliku log: {ex.Message}");
                    _fileWriter = null;
                }
            }
        }

        private static void WriteToFile(LogEntry entry)
        {
            try
            {
                EnsureFileLogger();
                if (_fileWriter == null) return;
                lock (_fileLock)
                {
                    _fileWriter.WriteLine($"{entry.Timestamp:HH:mm:ss.fff} [{entry.Level,-7}] [{entry.Source,-22}] {entry.Message}");
                }
            }
            catch { /* file logger niezawiedny — nie wywal całości */ }
        }

        /// <summary>Wywoływany przy każdym nowym wpisie (UI subskrybuje aby dopisać do live log).</summary>
        public static event Action<LogEntry> OnMessage;

        /// <summary>Prefiksy wiadomości które łapiemy (pomijamy nieistotne logi systemowe).</summary>
        private static readonly string[] BriefingPrefixes =
        {
            "[Claude]", "[Perplexity]", "[Orchestrator]", "[Filter]",
            "[RssFeed]", "[ContextBuilder]", "[BriefingDataLoader]",
            "[WebScraper]", "[Briefing]", "[DatabaseSetup]", "[Scraper]",
            "[ContentEnrichment]", "[UserQueries]"
        };

        /// <summary>Jednorazowo dopina się do System.Diagnostics.Trace.Listeners aby łapać Debug.WriteLine z całej apki.</summary>
        public static void EnsureAttached()
        {
            if (_listenerAttached) return;
            lock (_attachLock)
            {
                if (_listenerAttached) return;
                Trace.Listeners.Add(new BriefingTraceListener());
                EnsureFileLogger(); // od razu otwórz plik
                _listenerAttached = true;
            }
        }

        /// <summary>Dopisz wpis ręcznie (dla kodu który nie używa Debug.WriteLine).</summary>
        public static void Log(string message)
        {
            AppendInternal(message);
        }

        /// <summary>Pobierz historię wszystkich aktualnie zbuforowanych wpisów (kopia, thread-safe).</summary>
        public static List<LogEntry> GetRecent()
        {
            lock (_lock) return _buffer.ToList();
        }

        /// <summary>Czyści bufor (np. przed nowym fetch).</summary>
        public static void Clear()
        {
            lock (_lock) _buffer.Clear();
        }

        internal static void AppendInternal(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            var trimmed = raw.TrimEnd('\r', '\n', ' ');
            if (trimmed.Length == 0) return;
            if (!IsBriefingMessage(trimmed)) return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Message = trimmed,
                Level = DetectLevel(trimmed),
                Source = DetectSource(trimmed)
            };

            lock (_lock)
            {
                _buffer.AddLast(entry);
                while (_buffer.Count > MaxBuffer) _buffer.RemoveFirst();
            }

            // Zapisz do pliku NA ŻYWO — AutoFlush gwarantuje że nawet po crashu jest na dysku
            WriteToFile(entry);

            try { OnMessage?.Invoke(entry); }
            catch { /* event handler exception nie może wywalić logu */ }
        }

        private static bool IsBriefingMessage(string msg)
        {
            foreach (var p in BriefingPrefixes)
                if (msg.Contains(p, StringComparison.Ordinal)) return true;
            return false;
        }

        private static LogLevel DetectLevel(string msg)
        {
            if (msg.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("error", StringComparison.Ordinal) ||
                msg.Contains("WARNING", StringComparison.OrdinalIgnoreCase) && msg.Contains("LIMIT") ||
                msg.Contains("DZIENNY LIMIT") ||
                msg.Contains("Exception"))
                return LogLevel.Error;

            if (msg.Contains("WARNING", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("UWAGA") ||
                msg.Contains("Duplicate"))
                return LogLevel.Warning;

            return LogLevel.Info;
        }

        private static string DetectSource(string msg)
        {
            foreach (var p in BriefingPrefixes)
            {
                if (msg.StartsWith(p, StringComparison.Ordinal))
                    return p.Trim('[', ']');
            }
            // Prefix może być gdzieś w środku
            foreach (var p in BriefingPrefixes)
            {
                var idx = msg.IndexOf(p, StringComparison.Ordinal);
                if (idx >= 0) return p.Trim('[', ']');
            }
            return "Other";
        }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Source { get; set; } = "";
        public string Message { get; set; } = "";
        public LogLevel Level { get; set; } = LogLevel.Info;

        public string TimestampDisplay => Timestamp.ToString("HH:mm:ss.fff");
    }

    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Custom TraceListener — wszystko co idzie do Debug.WriteLine / Trace.WriteLine
    /// trafia tu i jest filtrowane przez BriefingLogHub.
    /// </summary>
    internal class BriefingTraceListener : TraceListener
    {
        public override void Write(string message) => BriefingLogHub.AppendInternal(message);
        public override void WriteLine(string message) => BriefingLogHub.AppendInternal(message);
    }
}
