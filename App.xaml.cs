using Kalendarz1.Services;
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

        // === ITEXTSHARP WORKAROUND ===
        // Inicjalizacja iTextSharp rozwiązuje problem NullReferenceException
        // w Version.GetInstance() na .NET 8
        private static bool _iTextSharpInitialized = false;

        private static void InitializeITextSharp()
        {
            if (_iTextSharpInitialized) return;

            try
            {
                // Wymuszamy inicjalizację iTextSharp przez utworzenie prostego dokumentu
                var testDoc = new iTextSharp.text.Document();
                using (var ms = new MemoryStream())
                {
                    var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(testDoc, ms);
                    testDoc.Open();
                    testDoc.Close();
                }
                _iTextSharpInitialized = true;
            }
            catch
            {
                // Jeśli standardowa inicjalizacja nie działa, próbujemy reflection
                try
                {
                    var versionType = typeof(iTextSharp.text.Version);
                    var field = versionType.GetField("version", BindingFlags.NonPublic | BindingFlags.Static);
                    if (field != null && field.GetValue(null) == null)
                    {
                        var constructor = versionType.GetConstructor(
                            BindingFlags.NonPublic | BindingFlags.Instance,
                            null, Type.EmptyTypes, null);
                        if (constructor != null)
                        {
                            var instance = constructor.Invoke(null);
                            field.SetValue(null, instance);
                        }
                    }
                    _iTextSharpInitialized = true;
                }
                catch
                {
                    // Ignoruj - aplikacja spróbuje kontynuować bez inicjalizacji
                }
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