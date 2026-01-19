using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Kalendarz1
{
    /// <summary>
    /// Manager logo firmy - przechowuje logo jako plik PNG
    /// Logo jest zapisywane w folderze %AppData%/ZPSP/Logo/
    /// Rozwiązanie nr 2: Jeśli logo nie istnieje lokalnie, próbuje pobrać z serwera sieciowego
    /// Może być używane w różnych aplikacjach
    /// </summary>
    public static class CompanyLogoManager
    {
        private static string LogoFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZPSP", "Logo");

        private static string LogoPath => Path.Combine(LogoFolder, "company_logo.png");

        // Ścieżki sieciowe do logo firmy (rozwiązanie nr 2)
        private static readonly string NetworkLogoPath1 = @"\\192.168.0.170\Install\Prace Graficzne\Logo";
        private static readonly string NetworkLogoPath2 = @"\\192.168.0.171\Install\Prace Graficzne\Logo";
        private static readonly string NetworkLogoFileName = "company_logo";

        /// <summary>
        /// Inicjalizuje folder logo
        /// </summary>
        static CompanyLogoManager()
        {
            EnsureLogoFolderExists();
        }

        private static void EnsureLogoFolderExists()
        {
            if (!Directory.Exists(LogoFolder))
            {
                Directory.CreateDirectory(LogoFolder);
            }
        }

        /// <summary>
        /// Zwraca ścieżkę do pliku logo
        /// </summary>
        public static string GetLogoPath()
        {
            return LogoPath;
        }

        /// <summary>
        /// Sprawdza czy logo firmy jest zapisane (na serwerze sieciowym)
        /// </summary>
        public static bool HasLogo()
        {
            // Zawsze sprawdzaj na serwerze sieciowym
            var networkPath = GetNetworkLogoPath();
            return networkPath != null;
        }

        /// <summary>
        /// Próbuje znaleźć logo na serwerze sieciowym
        /// </summary>
        private static string GetNetworkLogoPath()
        {
            string[] extensions = { ".png", ".jpg", ".jpeg", ".bmp" };
            string[] networkPaths = { NetworkLogoPath1, NetworkLogoPath2 };

            foreach (var networkPath in networkPaths)
            {
                try
                {
                    if (!Directory.Exists(networkPath))
                        continue;

                    foreach (var ext in extensions)
                    {
                        string logoPath = Path.Combine(networkPath, $"{NetworkLogoFileName}{ext}");
                        if (File.Exists(logoPath))
                            return logoPath;
                    }
                }
                catch
                {
                    // Serwer niedostępny, spróbuj następny
                    continue;
                }
            }

            return null;
        }

        /// <summary>
        /// Pobiera logo firmy jako Image (null jeśli nie istnieje)
        /// Zawsze pobiera z serwera sieciowego
        /// </summary>
        public static Image GetLogo()
        {
            // Zawsze pobieraj z serwera sieciowego
            string networkPath = GetNetworkLogoPath();
            if (networkPath != null)
            {
                try
                {
                    // Wczytaj cały plik do pamięci, żeby nie blokować pliku sieciowego
                    byte[] imageData = File.ReadAllBytes(networkPath);
                    using (var ms = new MemoryStream(imageData))
                    {
                        // Utwórz kopię obrazu, która nie zależy od strumienia
                        using (var tempImage = Image.FromStream(ms))
                        {
                            return new Bitmap(tempImage);
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// Pobiera logo przeskalowane do podanego rozmiaru (zachowuje proporcje)
        /// </summary>
        public static Image GetLogoScaled(int maxWidth, int maxHeight)
        {
            var logo = GetLogo();
            if (logo == null) return null;

            try
            {
                return ScaleImage(logo, maxWidth, maxHeight);
            }
            finally
            {
                logo.Dispose();
            }
        }

        /// <summary>
        /// Zapisuje logo firmy z pliku - zapisuje na serwer sieciowy
        /// </summary>
        public static bool SaveLogo(string sourceImagePath)
        {
            try
            {
                using (var originalImage = Image.FromFile(sourceImagePath))
                {
                    return SaveLogo(originalImage);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Zapisuje logo z Image - zapisuje na serwer sieciowy
        /// </summary>
        public static bool SaveLogo(Image image)
        {
            try
            {
                var resized = ScaleImage(image, 512, 512);
                bool savedToNetwork = false;

                // Próbuj zapisać na serwer sieciowy
                string[] networkPaths = { NetworkLogoPath1, NetworkLogoPath2 };

                foreach (var networkPath in networkPaths)
                {
                    try
                    {
                        if (!Directory.Exists(networkPath))
                        {
                            try
                            {
                                Directory.CreateDirectory(networkPath);
                            }
                            catch
                            {
                                continue; // Nie można utworzyć folderu, spróbuj następny serwer
                            }
                        }

                        string targetPath = Path.Combine(networkPath, $"{NetworkLogoFileName}.png");

                        // Usuń stare pliki logo z innymi rozszerzeniami
                        string[] extensions = { ".png", ".jpg", ".jpeg", ".bmp" };
                        foreach (var ext in extensions)
                        {
                            string oldPath = Path.Combine(networkPath, $"{NetworkLogoFileName}{ext}");
                            if (File.Exists(oldPath) && ext != ".png")
                            {
                                try { File.Delete(oldPath); } catch { }
                            }
                        }

                        resized.Save(targetPath, ImageFormat.Png);
                        savedToNetwork = true;
                        break; // Zapisano pomyślnie, nie próbuj na drugim serwerze
                    }
                    catch
                    {
                        continue; // Spróbuj następny serwer
                    }
                }

                // Zapisz też lokalnie jako backup
                try
                {
                    EnsureLogoFolderExists();
                    resized.Save(LogoPath, ImageFormat.Png);
                }
                catch { }

                resized.Dispose();

                return savedToNetwork;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Usuwa logo firmy
        /// </summary>
        public static bool DeleteLogo()
        {
            try
            {
                if (File.Exists(LogoPath))
                {
                    File.Delete(LogoPath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Skaluje obraz zachowując proporcje
        /// </summary>
        private static Bitmap ScaleImage(Image image, int maxWidth, int maxHeight)
        {
            double ratioX = (double)maxWidth / image.Width;
            double ratioY = (double)maxHeight / image.Height;
            double ratio = Math.Min(ratioX, ratioY);

            int newWidth = (int)(image.Width * ratio);
            int newHeight = (int)(image.Height * ratio);

            var result = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                g.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            return result;
        }

        /// <summary>
        /// Generuje domyślne logo z tekstem "ZPSP"
        /// </summary>
        public static Bitmap GenerateDefaultLogo(int width, int height)
        {
            var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(result))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                // Tło z gradientem
                using (var brush = new LinearGradientBrush(
                    new Point(0, 0), new Point(width, height),
                    Color.FromArgb(45, 55, 65), Color.FromArgb(30, 40, 50)))
                {
                    g.FillRectangle(brush, 0, 0, width, height);
                }

                // Tekst "ZPSP"
                using (var font = new Font("Segoe UI", width / 4, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.FromArgb(76, 175, 80)))
                {
                    var text = "ZPSP";
                    var textSize = g.MeasureString(text, font);
                    float x = (width - textSize.Width) / 2;
                    float y = (height - textSize.Height) / 2;
                    g.DrawString(text, font, textBrush, x, y);
                }

                // Ramka
                using (var pen = new Pen(Color.FromArgb(76, 175, 80), 2))
                {
                    g.DrawRectangle(pen, 1, 1, width - 3, height - 3);
                }
            }

            return result;
        }
    }
}
