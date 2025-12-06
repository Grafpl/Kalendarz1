using System;
using System.Drawing;
using System.IO;

namespace Kalendarz1.Reklamacje
{
    /// <summary>
    /// Klasa pomocnicza do ładowania logo firmy
    /// Logo: PIÓRKOWSCY UBOJNIA DROBIU
    /// </summary>
    public static class LogoFirmyBase64
    {
        private static string _cachedLogoPath = null;

        /// <summary>
        /// Zwraca logo firmy jako Image
        /// </summary>
        public static Image GetLogo()
        {
            string path = GetLogoPath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    return Image.FromFile(path);
                }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// Szuka pliku logo w wielu możliwych lokalizacjach
        /// </summary>
        public static string GetLogoPath()
        {
            // Jeśli już znaleźliśmy ścieżkę, użyj jej
            if (!string.IsNullOrEmpty(_cachedLogoPath) && File.Exists(_cachedLogoPath))
            {
                return _cachedLogoPath;
            }

            string nazwaPliku = "logo-2-green.png";

            // Lista możliwych ścieżek do sprawdzenia
            var sciezki = new[]
            {
                // Katalog roboczy aplikacji
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nazwaPliku),
                // Katalogi nadrzędne (z bin/Debug do głównego)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", nazwaPliku),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", nazwaPliku),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", nazwaPliku),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", nazwaPliku),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", nazwaPliku),
                // Typowa ścieżka projektu
                Path.Combine(Environment.CurrentDirectory, nazwaPliku),
                Path.Combine(Environment.CurrentDirectory, "..", nazwaPliku),
                Path.Combine(Environment.CurrentDirectory, "..", "..", nazwaPliku),
                Path.Combine(Environment.CurrentDirectory, "..", "..", "..", nazwaPliku),
                // Ścieżki absolutne jako fallback
                "/home/user/Kalendarz1/logo-2-green.png",
                "C:\\Projects\\Kalendarz1\\logo-2-green.png",
                "D:\\Kalendarz1\\logo-2-green.png",
                // Obok pliku wykonywalnego w różnych konfiguracjach
                Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", nazwaPliku),
                Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "..", "..", "..", nazwaPliku),
                Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "..", "..", "..", "..", nazwaPliku),
            };

            foreach (var sciezka in sciezki)
            {
                try
                {
                    string pelnasciezka = Path.GetFullPath(sciezka);
                    if (File.Exists(pelnasciezka))
                    {
                        _cachedLogoPath = pelnasciezka;
                        return pelnasciezka;
                    }
                }
                catch { }
            }

            return null;
        }
    }
}
