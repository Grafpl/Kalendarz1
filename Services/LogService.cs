using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Poziomy logowania
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }

    /// <summary>
    /// Serwis logowania do pliku z asynchronicznym zapisem
    /// </summary>
    public class LogService : IDisposable
    {
        private static readonly Lazy<LogService> _instance = new(() => new LogService());
        public static LogService Instance => _instance.Value;

        private readonly ConcurrentQueue<LogEntry> _logQueue = new();
        private readonly string _logDirectory;
        private readonly Timer _flushTimer;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private bool _disposed;

        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;
        public bool ConsoleOutput { get; set; } = true;
        public int MaxLogFileSizeMB { get; set; } = 10;
        public int MaxLogFiles { get; set; } = 30;

        private LogService()
        {
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Kalendarz1", "Logs");

            Directory.CreateDirectory(_logDirectory);

            // Flush co 5 sekund
            _flushTimer = new Timer(_ => FlushAsync().Wait(), null,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            // Loguj start aplikacji
            Info("=== Aplikacja uruchomiona ===");
        }

        #region Metody logowania

        public void Debug(string message, params object[] args)
            => Log(LogLevel.Debug, message, args);

        public void Info(string message, params object[] args)
            => Log(LogLevel.Info, message, args);

        public void Warning(string message, params object[] args)
            => Log(LogLevel.Warning, message, args);

        public void Error(string message, params object[] args)
            => Log(LogLevel.Error, message, args);

        public void Error(Exception ex, string message = "", params object[] args)
        {
            var fullMessage = string.IsNullOrEmpty(message)
                ? $"{ex.GetType().Name}: {ex.Message}"
                : $"{FormatMessage(message, args)} | {ex.GetType().Name}: {ex.Message}";

            Log(LogLevel.Error, fullMessage);

            if (ex.StackTrace != null)
            {
                Log(LogLevel.Debug, $"StackTrace: {ex.StackTrace}");
            }

            if (ex.InnerException != null)
            {
                Log(LogLevel.Debug, $"InnerException: {ex.InnerException.Message}");
            }
        }

        public void Critical(string message, params object[] args)
            => Log(LogLevel.Critical, message, args);

        public void Critical(Exception ex, string message = "", params object[] args)
        {
            var fullMessage = string.IsNullOrEmpty(message)
                ? $"CRITICAL: {ex.GetType().Name}: {ex.Message}"
                : $"CRITICAL: {FormatMessage(message, args)} | {ex.GetType().Name}: {ex.Message}";

            Log(LogLevel.Critical, fullMessage);

            if (ex.StackTrace != null)
            {
                Log(LogLevel.Critical, $"StackTrace: {ex.StackTrace}");
            }
        }

        #endregion

        private void Log(LogLevel level, string message, params object[] args)
        {
            if (level < MinimumLevel) return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = FormatMessage(message, args),
                ThreadId = Environment.CurrentManagedThreadId
            };

            _logQueue.Enqueue(entry);

            if (ConsoleOutput)
            {
                WriteToConsole(entry);
            }
        }

        private string FormatMessage(string message, object[] args)
        {
            if (args == null || args.Length == 0) return message;

            try
            {
                return string.Format(message, args);
            }
            catch
            {
                return message;
            }
        }

        private void WriteToConsole(LogEntry entry)
        {
            var color = entry.Level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.DarkRed,
                _ => ConsoleColor.White
            };

            try
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(entry.ToString());
                Console.ForegroundColor = originalColor;
            }
            catch { /* Ignoruj błędy konsoli */ }
        }

        public async Task FlushAsync()
        {
            if (_logQueue.IsEmpty) return;

            await _writeLock.WaitAsync();
            try
            {
                var sb = new StringBuilder();
                while (_logQueue.TryDequeue(out var entry))
                {
                    sb.AppendLine(entry.ToString());
                }

                if (sb.Length > 0)
                {
                    var logFile = GetCurrentLogFile();
                    await File.AppendAllTextAsync(logFile, sb.ToString());

                    // Sprawdź rozmiar pliku i rotuj jeśli potrzeba
                    await RotateLogsIfNeededAsync(logFile);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogService.FlushAsync error: {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private string GetCurrentLogFile()
        {
            var date = DateTime.Now.ToString("yyyy-MM-dd");
            return Path.Combine(_logDirectory, $"Kalendarz1_{date}.log");
        }

        private async Task RotateLogsIfNeededAsync(string currentLogFile)
        {
            try
            {
                var fileInfo = new FileInfo(currentLogFile);
                if (fileInfo.Exists && fileInfo.Length > MaxLogFileSizeMB * 1024 * 1024)
                {
                    var newName = Path.Combine(_logDirectory,
                        $"Kalendarz1_{DateTime.Now:yyyy-MM-dd_HHmmss}.log");
                    File.Move(currentLogFile, newName);
                }

                // Usuń stare pliki
                var logFiles = Directory.GetFiles(_logDirectory, "Kalendarz1_*.log");
                if (logFiles.Length > MaxLogFiles)
                {
                    Array.Sort(logFiles);
                    for (int i = 0; i < logFiles.Length - MaxLogFiles; i++)
                    {
                        File.Delete(logFiles[i]);
                    }
                }
            }
            catch { /* Ignoruj błędy rotacji */ }
        }

        /// <summary>
        /// Otwiera folder z logami w Eksploratorze
        /// </summary>
        public void OpenLogFolder()
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", _logDirectory);
            }
            catch { }
        }

        /// <summary>
        /// Pobiera ścieżkę do folderu logów
        /// </summary>
        public string LogDirectory => _logDirectory;

        public void Dispose()
        {
            if (!_disposed)
            {
                Info("=== Aplikacja zamykana ===");
                FlushAsync().Wait();
                _flushTimer?.Dispose();
                _writeLock?.Dispose();
                _disposed = true;
            }
        }

        private class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public LogLevel Level { get; set; }
            public string Message { get; set; } = "";
            public int ThreadId { get; set; }

            public override string ToString()
            {
                var levelStr = Level.ToString().ToUpper().PadRight(8);
                return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{levelStr}] [T{ThreadId:D3}] {Message}";
            }
        }
    }

    /// <summary>
    /// Globalna obsługa nieobsłużonych wyjątków
    /// </summary>
    public static class GlobalExceptionHandler
    {
        private static bool _isInitialized;

        public static void Initialize()
        {
            if (_isInitialized) return;

            // WPF Dispatcher exceptions
            System.Windows.Application.Current.DispatcherUnhandledException += (s, e) =>
            {
                LogService.Instance.Critical(e.Exception, "Nieobsłużony wyjątek UI");
                ShowErrorDialog(e.Exception);
                e.Handled = true;
            };

            // AppDomain exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    LogService.Instance.Critical(ex, "Krytyczny błąd aplikacji");
                    ShowErrorDialog(ex, isCritical: true);
                }
            };

            // Task exceptions
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogService.Instance.Error(e.Exception, "Nieobserwowany wyjątek Task");
                e.SetObserved();
            };

            _isInitialized = true;
            LogService.Instance.Info("GlobalExceptionHandler zainicjalizowany");
        }

        private static void ShowErrorDialog(Exception ex, bool isCritical = false)
        {
            try
            {
                if (isCritical)
                {
                    // Błędy krytyczne nadal pokazujemy jako MessageBox
                    var message = $"Wystąpił krytyczny błąd aplikacji. Aplikacja zostanie zamknięta.\n\n{ex.Message}\n\nSzczegóły zostały zapisane w logach.";
                    System.Windows.MessageBox.Show(message, "Błąd krytyczny",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                else
                {
                    // Błędy niekrytyczne pokazujemy jako toast
                    ToastService.Show($"Błąd: {ex.Message}", ToastType.Error, 5000);
                }
            }
            catch { /* Ignoruj błędy wyświetlania dialogu */ }
        }

        /// <summary>
        /// Wyświetla globalny toast z dowolną wiadomością
        /// </summary>
        public static void ShowGlobalToast(string message, ToastType type = ToastType.Info)
        {
            ToastService.Show(message, type);
        }

        /// <summary>
        /// Bezpieczne wykonanie akcji z obsługą błędów
        /// </summary>
        public static void SafeExecute(Action action, string context = "")
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                var msg = string.IsNullOrEmpty(context) ? "SafeExecute" : context;
                LogService.Instance.Error(ex, msg);
            }
        }

        /// <summary>
        /// Bezpieczne wykonanie akcji async z obsługą błędów
        /// </summary>
        public static async Task SafeExecuteAsync(Func<Task> action, string context = "")
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                var msg = string.IsNullOrEmpty(context) ? "SafeExecuteAsync" : context;
                LogService.Instance.Error(ex, msg);
            }
        }
    }
}
