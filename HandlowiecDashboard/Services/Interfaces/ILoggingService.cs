using System;

namespace Kalendarz1.HandlowiecDashboard.Services.Interfaces
{
    /// <summary>
    /// Interfejs serwisu logowania
    /// </summary>
    public interface ILoggingService
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception ex = null);
        void LogDebug(string message);
    }
}
