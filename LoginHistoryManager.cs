using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Kalendarz1
{
    /// <summary>
    /// Zarządza historią logowań użytkowników
    /// </summary>
    public static class LoginHistoryManager
    {
        private static readonly string LoginHistoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZPSP", "LoginHistory", "history.json");

        public class LoginRecord
        {
            public string UserId { get; set; }
            public string UserName { get; set; }
            public DateTime LoginTime { get; set; }
        }

        /// <summary>
        /// Zapisuje nowe logowanie
        /// </summary>
        public static void SaveLogin(string userId, string userName)
        {
            try
            {
                var directory = Path.GetDirectoryName(LoginHistoryPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var history = GetAllLogins().ToList();
                history.Add(new LoginRecord
                {
                    UserId = userId,
                    UserName = userName,
                    LoginTime = DateTime.Now
                });

                // Zachowaj tylko ostatnie 100 logowań
                if (history.Count > 100)
                    history = history.OrderByDescending(h => h.LoginTime).Take(100).ToList();

                var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(LoginHistoryPath, json);
            }
            catch { }
        }

        /// <summary>
        /// Pobiera wszystkie logowania
        /// </summary>
        public static List<LoginRecord> GetAllLogins()
        {
            try
            {
                if (!File.Exists(LoginHistoryPath))
                    return new List<LoginRecord>();

                var json = File.ReadAllText(LoginHistoryPath);
                return JsonSerializer.Deserialize<List<LoginRecord>>(json) ?? new List<LoginRecord>();
            }
            catch
            {
                return new List<LoginRecord>();
            }
        }

        /// <summary>
        /// Pobiera ostatnie logowanie użytkownika
        /// </summary>
        public static LoginRecord GetLastLogin(string userId)
        {
            var logins = GetAllLogins()
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.LoginTime)
                .Skip(1) // Pomijamy bieżące logowanie
                .FirstOrDefault();

            return logins;
        }

        /// <summary>
        /// Pobiera ostatnie logowania z ostatnich N dni (unikalne użytkownicy)
        /// </summary>
        public static List<LoginRecord> GetRecentLogins(int days)
        {
            var cutoffDate = DateTime.Now.AddDays(-days);
            return GetAllLogins()
                .Where(l => l.LoginTime >= cutoffDate)
                .GroupBy(l => l.UserId)
                .Select(g => g.OrderByDescending(l => l.LoginTime).First())
                .OrderByDescending(l => l.LoginTime)
                .Take(10)
                .ToList();
        }
    }
}
