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

        /// <summary>
        /// Uruchamia indekser Centrum nagrań AI w tle (po zalogowaniu).
        /// Bezpieczne — jeśli brak konfiguracji NVR / klucza, po prostu nie startuje.
        /// Działa dopóki ZPSP otwarte. Dla 24/7 użyj osobnego CnaService (Windows Service).
        /// </summary>
        public static void StartCnaService()
        {
            try
            {
                Kalendarz1.CentrumNagranAI.Services.CnaConfig.ZaladujJesliTrzeba();
                if (Kalendarz1.CentrumNagranAI.Services.CnaConfig.Kamery.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[CNA] Brak kamer w secrets.json - indekser nie startuje.");
                    return;
                }
                _ = Kalendarz1.CentrumNagranAI.Services.IndexerBackgroundService.Instance.StartAsync();
                _ = Kalendarz1.CentrumNagranAI.Services.EmbeddingBackfillService.Instance.StartAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CNA] Start fail: {ex.Message}");
            }
        }

        public static void StopCnaService()
        {
            try
            {
                Kalendarz1.CentrumNagranAI.Services.IndexerBackgroundService.Instance.Stop();
                Kalendarz1.CentrumNagranAI.Services.EmbeddingBackfillService.Instance.Stop();
            }
            catch { }
        }

        // === ITEXTSHARP WORKAROUND ===
        // Inicjalizacja iTextSharp rozwiązuje problem NullReferenceException
        // w Version.GetInstance() na .NET 8
        private static bool _iTextSharpInitialized = false;
        private static Assembly _bouncyCastleAssembly;

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

                // KROK 2: Binding redirect dla BouncyCastle.Cryptography
                // iTextSharp 5.5.13.4 prosi o wersję 2.0.0.0, projekt ma 2.6.2 — w .NET 8
                // bindingRedirect z app.config nie działa, więc robimy resolver ręcznie
                try { _bouncyCastleAssembly = Assembly.Load("BouncyCastle.Cryptography"); } catch { }
                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    var requested = new AssemblyName(args.Name);
                    if (requested.Name == "BouncyCastle.Cryptography" && _bouncyCastleAssembly != null)
                        return _bouncyCastleAssembly;
                    return null;
                };

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

            // Tryb deweloperski Centrum nagrań AI: --cna-test [sekundy]
            // Pomija normalne logowanie i menu, uruchamia tylko indexer i kończy proces.
            // Bez flagi zachowanie identyczne jak przed zmianą.
            var argv = Environment.GetCommandLineArgs();
            int idx = Array.IndexOf(argv, "--cna-test");
            if (idx >= 0)
            {
                // Brak okna — bez tego Application sam zamknie się po OnStartup.
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                int sek = (idx + 1 < argv.Length && int.TryParse(argv[idx + 1], out int s)) ? s : 30;
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try { await Kalendarz1.CentrumNagranAI.Test.CnaSelfTest.RunAsync(sek); }
                    finally { Dispatcher.Invoke(() => Shutdown()); }
                });
                return;
            }

            int idxVlm = Array.IndexOf(argv, "--cna-test-vlm");
            if (idxVlm >= 0)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try { await Kalendarz1.CentrumNagranAI.Test.CnaSelfTest.RunVlmHelloAsync(); }
                    finally { Dispatcher.Invoke(() => Shutdown()); }
                });
                return;
            }

            int idxSearch = Array.IndexOf(argv, "--cna-test-search");
            if (idxSearch >= 0 && idxSearch + 1 < argv.Length)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                string query = argv[idxSearch + 1];
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try { await Kalendarz1.CentrumNagranAI.Test.CnaSelfTest.RunSearchAsync(query); }
                    finally { Dispatcher.Invoke(() => Shutdown()); }
                });
                return;
            }

            int idxShow = Array.IndexOf(argv, "--cna-show-window");
            if (idxShow >= 0)
            {
                // Bezpośrednio otwiera placeholder okno CNA, pomija login/menu.
                var win = new Kalendarz1.CentrumNagranAI.Views.CentrumNagranAIWindow();
                win.Show();
                return;
            }

            // Inicjalizuj iTextSharp przed użyciem
            InitializeITextSharp();

            GlobalExceptionHandler.Initialize();

            Menu1 loginWindow = new Menu1();
            loginWindow.Show();
        }
    }
}