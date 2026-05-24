using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Kalendarz1.HDI
{
    /// <summary>
    /// Static logger dla HDI — każdy moduł (Service, EditWindow, PdfGenerator) loguje
    /// do wspólnego streamu. Okno HdiDiagWindow subskrybuje OnLog i pokazuje na żywo.
    /// Trzyma bufor ostatnich 500 wpisów żeby okno otwarte później dostało historię.
    ///
    /// FILE LOGGING (kluczowe!): każdy wpis idzie też do pliku natychmiast — nawet jak
    /// apka się zwiesi, plik ma dane do momentu zwiesia.
    /// Plik: %TEMP%\Kalendarz1_HDI_diag.log (overwrite przy każdym uruchomieniu apki)
    /// </summary>
    public static class HdiDiag
    {
        public static readonly string LogFilePath = Path.Combine(Path.GetTempPath(), "Kalendarz1_HDI_diag.log");
        private static readonly object _fileLock = new();
        private static bool _fileInitialized = false;

        private static void EnsureFileInitialized()
        {
            if (_fileInitialized) return;
            try
            {
                lock (_fileLock)
                {
                    if (_fileInitialized) return;
                    File.WriteAllText(LogFilePath,
                        $"=== HDI DIAG LOG — start {DateTime.Now:dd.MM.yyyy HH:mm:ss} ===\nFile: {LogFilePath}\n\n");
                    _fileInitialized = true;
                }
            }
            catch { /* nie krytyczne */ }
        }

        public class Entry
        {
            public DateTime When { get; set; }
            public string Level { get; set; } = "INFO";  // INFO / WARN / ERR / SQL / TIME
            public string Source { get; set; } = "";
            public string Message { get; set; } = "";
            public long? ElapsedMs { get; set; }

            public override string ToString()
            {
                string t = When.ToString("HH:mm:ss.fff");
                string elapsed = ElapsedMs.HasValue ? $" [{ElapsedMs}ms]" : "";
                string lvl = Level.PadRight(4);
                return $"{t} {lvl} {Source,-22} {Message}{elapsed}";
            }
        }

        private static readonly object _lock = new();
        private static readonly Queue<Entry> _buf = new();
        private const int MAX_BUF = 500;

        public static event Action<Entry>? OnLog;

        public static IReadOnlyList<Entry> Snapshot()
        {
            lock (_lock) { return new List<Entry>(_buf); }
        }

        public static void Clear()
        {
            lock (_lock) { _buf.Clear(); }
        }

        public static void Log(string source, string message, long? elapsedMs = null) =>
            Push(new Entry { When = DateTime.Now, Level = "INFO", Source = source, Message = message, ElapsedMs = elapsedMs });

        public static void Warn(string source, string message) =>
            Push(new Entry { When = DateTime.Now, Level = "WARN", Source = source, Message = message });

        public static void Error(string source, string message, Exception? ex = null) =>
            Push(new Entry { When = DateTime.Now, Level = "ERR", Source = source, Message = ex == null ? message : $"{message}\n  → {ex.GetType().Name}: {ex.Message}\n  {ex.StackTrace}" });

        public static void Sql(string source, string query, long? elapsedMs = null) =>
            Push(new Entry { When = DateTime.Now, Level = "SQL", Source = source, Message = query.Replace("\n", " ").Replace("\r", "").Trim(), ElapsedMs = elapsedMs });

        public static void Time(string source, string what, long elapsedMs) =>
            Push(new Entry { When = DateTime.Now, Level = "TIME", Source = source, Message = what, ElapsedMs = elapsedMs });

        // Helper: scope dla mierzenia czasu (using var s = HdiDiag.Scope(...))
        public static IDisposable Scope(string source, string what)
        {
            return new TimerScope(source, what);
        }

        private static void Push(Entry e)
        {
            // 1) Pamięciowy bufor (dla okna live)
            lock (_lock)
            {
                _buf.Enqueue(e);
                while (_buf.Count > MAX_BUF) _buf.Dequeue();
            }

            // 2) FILE — natychmiastowy append (bulletproof: jeśli apka się zwiesi, plik jest aktualny)
            try
            {
                EnsureFileInitialized();
                lock (_fileLock)
                {
                    File.AppendAllText(LogFilePath, e.ToString() + Environment.NewLine);
                }
            }
            catch { /* ignore — nie psuj apki */ }

            // 3) DEBUG OUTPUT (dla Visual Studio Debug → Output)
            try { Debug.WriteLine($"[HDI] {e}"); } catch { }

            // 4) Event do okna live (fire-and-forget — nie blokuj jeśli żadnego okna nie ma)
            try { OnLog?.Invoke(e); } catch { /* nie zatrzymuj log przez błąd UI */ }
        }

        private class TimerScope : IDisposable
        {
            private readonly string _source;
            private readonly string _what;
            private readonly Stopwatch _sw;
            public TimerScope(string source, string what)
            {
                _source = source; _what = what;
                _sw = Stopwatch.StartNew();
                Log(source, $"⟶ {what}");
            }
            public void Dispose()
            {
                _sw.Stop();
                Time(_source, $"✓ {_what}", _sw.ElapsedMilliseconds);
            }
        }
    }
}
