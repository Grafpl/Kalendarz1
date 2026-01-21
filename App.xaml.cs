using Kalendarz1.Komunikator.Services;
using Kalendarz1.Services;
using Kalendarz1.Spotkania.Services;
using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace Kalendarz1
{
    public partial class App : Application
    {
        public static string UserID { get; set; }
        public static string UserFullName { get; set; }

        /// <summary>
        /// Uruchamia globalny serwis powiadomień o spotkaniach
        /// Wywoływane po zalogowaniu użytkownika
        /// </summary>
        public static void StartNotyfikacjeService()
        {
            if (!string.IsNullOrEmpty(UserID))
            {
                var service = NotyfikacjeManager.GetInstance(UserID);
                service.Start();
            }
        }

        /// <summary>
        /// Zatrzymuje serwis powiadomień
        /// Wywoływane przy wylogowaniu lub zamknięciu aplikacji
        /// </summary>
        public static void StopNotyfikacjeService()
        {
            NotyfikacjeManager.Shutdown();
        }

        /// <summary>
        /// Uruchamia globalny serwis powiadomień czatu
        /// Wywoływane po zalogowaniu użytkownika
        /// </summary>
        public static void StartChatNotificationService()
        {
            if (!string.IsNullOrEmpty(UserID))
            {
                GlobalChatManager.Start(UserID);
            }
        }

        /// <summary>
        /// Zatrzymuje serwis powiadomień czatu
        /// Wywoływane przy wylogowaniu lub zamknięciu aplikacji
        /// </summary>
        public static void StopChatNotificationService()
        {
            GlobalChatManager.Shutdown();
        }

        // === ITEXTSHARP WORKAROUND ===
        // Inicjalizacja iTextSharp rozwiązuje problem NullReferenceException
        // w Version.GetInstance() na .NET 8
        private static bool _iTextSharpInitialized = false;

        private static void InitializeITextSharp()
        {
            if (_iTextSharpInitialized) return;

            try
            {
                // KROK 1: Napraw pole 'version' przez reflection PRZED użyciem iTextSharp
                // To rozwiązuje NullReferenceException w Version.GetInstance() na .NET 8
                var versionType = typeof(iTextSharp.text.Version);

                // Sprawdź czy pole 'version' jest null
                var versionField = versionType.GetField("version", BindingFlags.NonPublic | BindingFlags.Static);
                if (versionField != null && versionField.GetValue(null) == null)
                {
                    // Utwórz instancję Version ręcznie przez reflection
                    var instance = Activator.CreateInstance(versionType, true);
                    versionField.SetValue(null, instance);
                }

                _iTextSharpInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"iTextSharp initialization warning: {ex.Message}");
                // Kontynuuj mimo błędu - może się uda bez inicjalizacji
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Inicjalizuj iTextSharp przed użyciem
            InitializeITextSharp();

            GlobalExceptionHandler.Initialize();

            Menu1 loginWindow = new Menu1();
            loginWindow.Show();
        }
    }
}