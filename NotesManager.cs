using System;
using System.IO;

namespace Kalendarz1
{
    /// <summary>
    /// Zarządza osobistymi notatkami użytkownika
    /// </summary>
    public static class NotesManager
    {
        private static readonly string NotesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZPSP", "Notes");

        /// <summary>
        /// Pobiera ścieżkę do pliku notatek użytkownika
        /// </summary>
        private static string GetNotesFilePath(string userId)
        {
            return Path.Combine(NotesDirectory, $"notes_{userId}.txt");
        }

        /// <summary>
        /// Pobiera notatki użytkownika
        /// </summary>
        public static string GetNotes(string userId)
        {
            try
            {
                var filePath = GetNotesFilePath(userId);
                if (File.Exists(filePath))
                    return File.ReadAllText(filePath);
            }
            catch { }

            return string.Empty;
        }

        /// <summary>
        /// Zapisuje notatki użytkownika
        /// </summary>
        public static bool SaveNotes(string userId, string notes)
        {
            try
            {
                if (!Directory.Exists(NotesDirectory))
                    Directory.CreateDirectory(NotesDirectory);

                var filePath = GetNotesFilePath(userId);
                File.WriteAllText(filePath, notes ?? string.Empty);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sprawdza czy użytkownik ma notatki
        /// </summary>
        public static bool HasNotes(string userId)
        {
            try
            {
                var filePath = GetNotesFilePath(userId);
                if (!File.Exists(filePath))
                    return false;

                return !string.IsNullOrWhiteSpace(File.ReadAllText(filePath));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Usuwa notatki użytkownika
        /// </summary>
        public static bool DeleteNotes(string userId)
        {
            try
            {
                var filePath = GetNotesFilePath(userId);
                if (File.Exists(filePath))
                    File.Delete(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
