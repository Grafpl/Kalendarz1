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
        /// Sprawdza czy logo firmy jest zapisane (lokalnie lub na serwerze sieciowym)
        /// </summary>
        public static bool HasLogo()
        {
            // Najpierw sprawdź lokalnie
            if (File.Exists(LogoPath))
                return true;

            // Rozwiązanie nr 2: Sprawdź na serwerze sieciowym
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
        /// Najpierw sprawdza lokalnie, potem na serwerze sieciowym (rozwiązanie nr 2)
        /// </summary>
        public static Image GetLogo()
        {
            // Najpierw sprawdź lokalnie
            if (File.Exists(LogoPath))
            {
                try
                {
                    using (var fs = new FileStream(LogoPath, FileMode.Open, FileAccess.Read))
                    {
                        return Image.FromStream(fs);
                    }
                }
                catch { }
            }

            // Rozwiązanie nr 2: Pobierz z serwera sieciowego
            string networkPath = GetNetworkLogoPath();
            if (networkPath != null)
            {
                try
                {
                    using (var fs = new FileStream(networkPath, FileMode.Open, FileAccess.Read))
                    {
                        return Image.FromStream(fs);
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
        /// Zapisuje logo firmy z pliku
        /// </summary>
        public static bool SaveLogo(string sourceImagePath)
        {
            try
            {
                EnsureLogoFolderExists();

                using (var originalImage = Image.FromFile(sourceImagePath))
                {
                    // Zapisz w oryginalnym rozmiarze (max 512px)
                    var resized = ScaleImage(originalImage, 512, 512);
                    resized.Save(LogoPath, ImageFormat.Png);
                    resized.Dispose();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Zapisuje logo z Image
        /// </summary>
        public static bool SaveLogo(Image image)
        {
            try
            {
                EnsureLogoFolderExists();

                var resized = ScaleImage(image, 512, 512);
                resized.Save(LogoPath, ImageFormat.Png);
                resized.Dispose();

                return true;
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
