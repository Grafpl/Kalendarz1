using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Kalendarz1
{
    /// <summary>
    /// Typ logo - rozróżnienie między logo na ekranie logowania i logo po zalogowaniu
    /// </summary>
    public enum LogoType
    {
        /// <summary>Logo wyświetlane na ekranie logowania (Menu1)</summary>
        Login,
        /// <summary>Logo wyświetlane po zalogowaniu (Menu)</summary>
        Company
    }

    /// <summary>
    /// Manager logo firmy - przechowuje logo jako plik PNG
    /// Logo jest zapisywane w folderze %AppData%/ZPSP/Logo/
    /// Rozwiązanie nr 2: Jeśli logo nie istnieje lokalnie, próbuje pobrać z serwera sieciowego
    /// Może być używane w różnych aplikacjach
    /// Obsługuje dwa typy logo: Login (przed zalogowaniem) i Company (po zalogowaniu)
    /// </summary>
    public static class CompanyLogoManager
    {
        // ID admina który może zarządzać logami
        public const string AdminUserId = "11111";

        private static string LogoFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZPSP", "Logo");

        private static string GetLogoFileName(LogoType logoType) =>
            logoType == LogoType.Login ? "login_logo" : "company_logo";

        private static string GetLocalLogoPath(LogoType logoType) =>
            Path.Combine(LogoFolder, $"{GetLogoFileName(logoType)}.png");

        // Dla kompatybilności wstecznej
        private static string LogoPath => GetLocalLogoPath(LogoType.Company);

        // Ścieżki sieciowe do logo firmy (rozwiązanie nr 2)
        private static readonly string NetworkLogoPath1 = @"\\192.168.0.170\Install\Prace Graficzne\Logo";
        private static readonly string NetworkLogoPath2 = @"\\192.168.0.171\Install\Prace Graficzne\Logo";

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
        /// Sprawdza czy użytkownik może zarządzać logami (tylko admin)
        /// </summary>
        public static bool CanManageLogos(string userId)
        {
            return userId == AdminUserId;
        }

        /// <summary>
        /// Zwraca ścieżkę do pliku logo (kompatybilność wsteczna - logo Company)
        /// </summary>
        public static string GetLogoPath()
        {
            return LogoPath;
        }

        /// <summary>
        /// Zwraca ścieżkę do pliku logo określonego typu
        /// </summary>
        public static string GetLogoPath(LogoType logoType)
        {
            return GetLocalLogoPath(logoType);
        }

        /// <summary>
        /// Sprawdza czy logo firmy jest zapisane (kompatybilność wsteczna - logo Company)
        /// </summary>
        public static bool HasLogo()
        {
            return HasLogo(LogoType.Company);
        }

        /// <summary>
        /// Sprawdza czy logo określonego typu jest zapisane (na serwerze sieciowym)
        /// </summary>
        public static bool HasLogo(LogoType logoType)
        {
            var networkPath = GetNetworkLogoPath(logoType);
            return networkPath != null;
        }

        /// <summary>
        /// Próbuje znaleźć logo na serwerze sieciowym (kompatybilność wsteczna)
        /// </summary>
        private static string GetNetworkLogoPath()
        {
            return GetNetworkLogoPath(LogoType.Company);
        }

        /// <summary>
        /// Próbuje znaleźć logo określonego typu na serwerze sieciowym
        /// </summary>
        private static string GetNetworkLogoPath(LogoType logoType)
        {
            string[] extensions = { ".png", ".jpg", ".jpeg", ".bmp" };
            string[] networkPaths = { NetworkLogoPath1, NetworkLogoPath2 };
            string logoFileName = GetLogoFileName(logoType);

            foreach (var networkPath in networkPaths)
            {
                try
                {
                    if (!Directory.Exists(networkPath))
                        continue;

                    foreach (var ext in extensions)
                    {
                        string logoPath = Path.Combine(networkPath, $"{logoFileName}{ext}");
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
        /// Pobiera logo firmy jako Image (kompatybilność wsteczna - logo Company)
        /// </summary>
        public static Image GetLogo()
        {
            return GetLogo(LogoType.Company);
        }

        /// <summary>
        /// Pobiera logo określonego typu jako Image (null jeśli nie istnieje)
        /// Zawsze pobiera z serwera sieciowego
        /// </summary>
        public static Image GetLogo(LogoType logoType)
        {
            string networkPath = GetNetworkLogoPath(logoType);
            if (networkPath != null)
            {
                try
                {
                    byte[] imageData = File.ReadAllBytes(networkPath);
                    using (var ms = new MemoryStream(imageData))
                    {
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
        /// Pobiera logo przeskalowane do podanego rozmiaru (kompatybilność wsteczna - logo Company)
        /// </summary>
        public static Image GetLogoScaled(int maxWidth, int maxHeight)
        {
            return GetLogoScaled(LogoType.Company, maxWidth, maxHeight);
        }

        /// <summary>
        /// Pobiera logo określonego typu przeskalowane do podanego rozmiaru (zachowuje proporcje)
        /// </summary>
        public static Image GetLogoScaled(LogoType logoType, int maxWidth, int maxHeight)
        {
            var logo = GetLogo(logoType);
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
        /// Zapisuje logo firmy z pliku (kompatybilność wsteczna - logo Company)
        /// </summary>
        public static bool SaveLogo(string sourceImagePath)
        {
            return SaveLogo(LogoType.Company, sourceImagePath);
        }

        /// <summary>
        /// Zapisuje logo określonego typu z pliku - zapisuje na serwer sieciowy
        /// </summary>
        public static bool SaveLogo(LogoType logoType, string sourceImagePath)
        {
            try
            {
                using (var originalImage = Image.FromFile(sourceImagePath))
                {
                    return SaveLogo(logoType, originalImage);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Zapisuje logo z Image (kompatybilność wsteczna - logo Company)
        /// </summary>
        public static bool SaveLogo(Image image)
        {
            return SaveLogo(LogoType.Company, image);
        }

        /// <summary>
        /// Zapisuje logo określonego typu z Image - zapisuje na serwer sieciowy
        /// </summary>
        public static bool SaveLogo(LogoType logoType, Image image)
        {
            try
            {
                var resized = ScaleImage(image, 512, 512);
                bool savedToNetwork = false;
                string logoFileName = GetLogoFileName(logoType);

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
                                continue;
                            }
                        }

                        string targetPath = Path.Combine(networkPath, $"{logoFileName}.png");

                        // Usuń stare pliki logo z innymi rozszerzeniami
                        string[] extensions = { ".png", ".jpg", ".jpeg", ".bmp" };
                        foreach (var ext in extensions)
                        {
                            string oldPath = Path.Combine(networkPath, $"{logoFileName}{ext}");
                            if (File.Exists(oldPath) && ext != ".png")
                            {
                                try { File.Delete(oldPath); } catch { }
                            }
                        }

                        resized.Save(targetPath, ImageFormat.Png);
                        savedToNetwork = true;
                        break;
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Zapisz też lokalnie jako backup
                try
                {
                    EnsureLogoFolderExists();
                    resized.Save(GetLocalLogoPath(logoType), ImageFormat.Png);
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
        /// Usuwa logo firmy (kompatybilność wsteczna - logo Company)
        /// </summary>
        public static bool DeleteLogo()
        {
            return DeleteLogo(LogoType.Company);
        }

        /// <summary>
        /// Usuwa logo określonego typu
        /// </summary>
        public static bool DeleteLogo(LogoType logoType)
        {
            try
            {
                string logoFileName = GetLogoFileName(logoType);
                string[] extensions = { ".png", ".jpg", ".jpeg", ".bmp" };
                string[] networkPaths = { NetworkLogoPath1, NetworkLogoPath2 };

                // Usuń z serwerów sieciowych
                foreach (var networkPath in networkPaths)
                {
                    try
                    {
                        if (!Directory.Exists(networkPath))
                            continue;

                        foreach (var ext in extensions)
                        {
                            string logoPath = Path.Combine(networkPath, $"{logoFileName}{ext}");
                            if (File.Exists(logoPath))
                            {
                                try { File.Delete(logoPath); } catch { }
                            }
                        }
                    }
                    catch { }
                }

                // Usuń lokalną kopię
                string localPath = GetLocalLogoPath(logoType);
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
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
