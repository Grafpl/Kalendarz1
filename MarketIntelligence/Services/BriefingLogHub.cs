using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Centralny zbiornik logów dla modułu Poranny Briefing.
    /// Przechwytuje wszystkie Debug.WriteLine z prefiksami [Claude]/[Perplexity]/[Orchestrator] itd.
    /// i udostępnia jako ring buffer + event dla okna diagnostycznego.
    /// </summary>
    public static class BriefingLogHub
    {
        private static readonly LinkedList<LogEntry> _buffer = new();
        private const int MaxBuffer = 500;
        private static readonly object _lock = new();
        private static bool _listenerAttached;
        private static readonly object _attachLock = new();

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
