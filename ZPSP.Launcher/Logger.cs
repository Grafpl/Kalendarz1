using System;
using System.IO;

namespace ZPSP.Launcher;

/// <summary>
/// Prosty file logger do %LOCALAPPDATA%\ZPSP\launcher.log.
/// Każde uruchomienie launchera dopisuje do pliku — łatwy troubleshoot.
/// Plik trzymany do 1 MB - po przekroczeniu rotacja (.old).
/// </summary>
internal static class Logger
{
    private const int MaxLogSizeBytes = 1024 * 1024; // 1 MB

    private static readonly object _lock = new();
    private static string? _logPath;

    private static string LogPath
    {
        get
        {
            if (_logPath != null) return _logPath;
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ZPSP");
                Directory.CreateDirectory(dir);
                _logPath = Path.Combine(dir, "launcher.log");

                // Rotacja jeśli plik za duży
                try
                {
                    if (File.Exists(_logPath) && new FileInfo(_logPath).Length > MaxLogSizeBytes)
                    {
                        var oldPath = _logPath + ".old";
                        if (File.Exists(oldPath)) File.Delete(oldPath);
                        File.Move(_logPath, oldPath);
                    }
                }
                catch { }

                return _logPath;
            }
            catch
            {
                _logPath = Path.Combine(Path.GetTempPath(), "zpsp_launcher.log");
                return _logPath;
            }
        }
    }

    public static void Info(string msg) => Write("INFO ", msg);
    public static void Warn(string msg) => Write("WARN ", msg);
    public static void Error(string msg) => Write("ERROR", msg);
    public static void Error(Exception ex, string context = "")
    {
        var msg = string.IsNullOrEmpty(context)
            ? $"{ex.GetType().Name}: {ex.Message}"
            : $"{context} -> {ex.GetType().Name}: {ex.Message}";
        Write("ERROR", msg);
    }

    private static void Write(string level, string msg)
    {
        try
        {
            lock (_lock)
            {
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}{Environment.NewLine}";
                File.AppendAllText(LogPath, line);
            }
        }
        catch
        {
            // Logger jest best-effort - nigdy nie crashuje aplikacji
        }
    }
}
