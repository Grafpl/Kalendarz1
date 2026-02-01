using System;
using System.IO;
using Kalendarz1.HandlowiecDashboard.Services.Interfaces;

namespace Kalendarz1.HandlowiecDashboard.Services
{
    /// <summary>
    /// Serwis logowania - zapisuje logi do pliku i Debug output
    /// </summary>
    public class LoggingService : ILoggingService
    {
        private readonly string _logDirectory;
        private readonly object _fileLock = new object();

        public LoggingService()
        {
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HandlowiecDashboard",
                "Logs");

            Directory.CreateDirectory(_logDirectory);
        }

        public void LogInfo(string message) => WriteLog("INFO", message);

        public void LogWarning(string message) => WriteLog("WARN", message);

        public void LogDebug(string message)
        {
#if DEBUG
            WriteLog("DEBUG", message);
#endif
        }

        public void LogError(string message, Exception ex = null)
        {
            var fullMessage = ex != null
                ? $"{message}\n  Exception: {ex.GetType().Name}: {ex.Message}\n  StackTrace: {ex.StackTrace}"
                : message;

            WriteLog("ERROR", fullMessage);
        }

        private void WriteLog(string level, string message)
        {
            var timestamp = DateTime.Now;
            var logEntry = $"[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{level,-5}] {message}";

            // Zawsze do Debug output
            System.Diagnostics.Debug.WriteLine(logEntry);

            // Do pliku
            lock (_fileLock)
            {
                try
                {
                    var fileName = $"dashboard_{timestamp:yyyy-MM-dd}.log";
                    var filePath = Path.Combine(_logDirectory, fileName);
                    File.AppendAllText(filePath, logEntry + Environment.NewLine);
                }
                catch
                {
                    // Jeśli zapis do pliku się nie uda - nie crashuj aplikacji
                    System.Diagnostics.Debug.WriteLine($"[LOG WRITE FAILED] {logEntry}");
                }
            }
        }

        /// <summary>
        /// Zwraca ścieżkę do katalogu z logami
        /// </summary>
        public string GetLogDirectory() => _logDirectory;
    }
}
