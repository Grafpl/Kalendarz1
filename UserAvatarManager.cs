using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Kalendarz1
{
    /// <summary>
    /// Manager avatarów użytkowników - przechowuje avatary jako pliki PNG
    /// Avatary są zapisywane w folderze %AppData%/ZPSP/Avatars/
    /// Rozwiązanie nr 2: Jeśli avatar nie istnieje lokalnie, próbuje pobrać z serwera sieciowego
    /// </summary>
    public static class UserAvatarManager
    {
        private static string AvatarsFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZPSP", "Avatars");

        // Ścieżki sieciowe do avatarów (rozwiązanie nr 2)
        private static readonly string NetworkAvatarsPath1 = @"\\192.168.0.170\Install\Prace Graficzne\Avatary";
        private static readonly string NetworkAvatarsPath2 = @"\\192.168.0.171\Install\Prace Graficzne\Avatary";

        /// <summary>
        /// Inicjalizuje folder avatarów
        /// </summary>
        static UserAvatarManager()
        {
            EnsureAvatarsFolderExists();
        }

        private static void EnsureAvatarsFolderExists()
        {
            if (!Directory.Exists(AvatarsFolder))
            {
                Directory.CreateDirectory(AvatarsFolder);
            }
        }

        /// <summary>
        /// Zwraca ścieżkę do pliku avatara dla danego użytkownika
        /// </summary>
        public static string GetAvatarPath(string userId)
        {
            return Path.Combine(AvatarsFolder, $"{userId}.png");
        }

        /// <summary>
        /// Sprawdza czy użytkownik ma zapisany avatar (lokalnie lub na serwerze sieciowym)
        /// </summary>
        public static bool HasAvatar(string userId)
        {
            // Najpierw sprawdź lokalnie
            if (File.Exists(GetAvatarPath(userId)))
                return true;

            // Rozwiązanie nr 2: Sprawdź na serwerze sieciowym
            var networkPath = GetNetworkAvatarPath(userId);
            return networkPath != null;
        }

        /// <summary>
        /// Próbuje znaleźć avatar na serwerze sieciowym
        /// </summary>
        private static string GetNetworkAvatarPath(string userId)
        {
            string[] extensions = { ".png", ".jpg", ".jpeg", ".bmp" };
            string[] networkPaths = { NetworkAvatarsPath1, NetworkAvatarsPath2 };

            foreach (var networkPath in networkPaths)
            {
                try
                {
                    if (!Directory.Exists(networkPath))
                        continue;

                    foreach (var ext in extensions)
                    {
                        string avatarPath = Path.Combine(networkPath, $"{userId}{ext}");
                        if (File.Exists(avatarPath))
                            return avatarPath;
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
        /// Pobiera avatar użytkownika jako Image (null jeśli nie istnieje)
        /// Najpierw sprawdza lokalnie, potem na serwerze sieciowym (rozwiązanie nr 2)
        /// </summary>
        public static Image GetAvatar(string userId)
        {
            // Najpierw sprawdź lokalnie
            string localPath = GetAvatarPath(userId);
            if (File.Exists(localPath))
            {
                try
                {
                    using (var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read))
                    {
                        return Image.FromStream(fs);
                    }
                }
                catch { }
            }

            // Rozwiązanie nr 2: Pobierz z serwera sieciowego
            string networkPath = GetNetworkAvatarPath(userId);
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
        /// Pobiera avatar przeskalowany do podanego rozmiaru (okrągły)
        /// </summary>
        public static Image GetAvatarRounded(string userId, int size)
        {
            var avatar = GetAvatar(userId);
            if (avatar == null) return null;

            try
            {
                return CreateRoundedAvatar(avatar, size);
            }
            finally
            {
                avatar.Dispose();
            }
        }

        /// <summary>
        /// Zapisuje avatar dla użytkownika (przeskalowuje do 128x128)
        /// </summary>
        public static bool SaveAvatar(string userId, string sourceImagePath)
        {
            try
            {
                EnsureAvatarsFolderExists();

                using (var originalImage = Image.FromFile(sourceImagePath))
                {
                    // Przeskaluj do 128x128 (kwadratowy)
                    var resized = ResizeAndCropToSquare(originalImage, 128);

                    string targetPath = GetAvatarPath(userId);
                    resized.Save(targetPath, ImageFormat.Png);
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
        /// Zapisuje avatar z Image
        /// </summary>
        public static bool SaveAvatar(string userId, Image image)
        {
            try
            {
                EnsureAvatarsFolderExists();

                var resized = ResizeAndCropToSquare(image, 128);
                string targetPath = GetAvatarPath(userId);
                resized.Save(targetPath, ImageFormat.Png);
                resized.Dispose();

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Usuwa avatar użytkownika
        /// </summary>
        public static bool DeleteAvatar(string userId)
        {
            try
            {
                string path = GetAvatarPath(userId);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Przeskalowuje i przycina obraz do kwadratu
        /// </summary>
        private static Bitmap ResizeAndCropToSquare(Image image, int size)
        {
            int sourceSize = Math.Min(image.Width, image.Height);
            int sourceX = (image.Width - sourceSize) / 2;
            int sourceY = (image.Height - sourceSize) / 2;

            var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                g.DrawImage(image,
                    new Rectangle(0, 0, size, size),
                    new Rectangle(sourceX, sourceY, sourceSize, sourceSize),
                    GraphicsUnit.Pixel);
            }

            return result;
        }

        /// <summary>
        /// Tworzy okrągły avatar z obrazu
        /// </summary>
        public static Bitmap CreateRoundedAvatar(Image source, int size)
        {
            var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(result))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // Stwórz okrągłą ścieżkę
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(0, 0, size - 1, size - 1);
                    g.SetClip(path);

                    // Rysuj obraz
                    g.DrawImage(source, new Rectangle(0, 0, size, size));
                }

                // Dodaj subtelną ramkę
                using (var pen = new Pen(Color.FromArgb(60, 0, 0, 0), 1))
                {
                    g.DrawEllipse(pen, 0, 0, size - 1, size - 1);
                }
            }

            return result;
        }

        /// <summary>
        /// Generuje domyślny avatar z inicjałami
        /// </summary>
        public static Bitmap GenerateDefaultAvatar(string name, string odbiorcaId, int size)
        {
            var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            string initials = GetInitials(name);
            Color bgColor = GetColorFromId(odbiorcaId);

            using (var g = Graphics.FromImage(result))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                // Okrągłe tło
                using (var brush = new SolidBrush(bgColor))
                {
                    g.FillEllipse(brush, 0, 0, size - 1, size - 1);
                }

                // Inicjały
                using (var font = new Font("Segoe UI", size / 3, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.White))
                {
                    var textSize = g.MeasureString(initials, font);
                    float x = (size - textSize.Width) / 2;
                    float y = (size - textSize.Height) / 2;
                    g.DrawString(initials, font, textBrush, x, y);
                }

                // Ramka
                using (var pen = new Pen(Color.FromArgb(40, 0, 0, 0), 1))
                {
                    g.DrawEllipse(pen, 0, 0, size - 1, size - 1);
                }
            }

            return result;
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private static Color GetColorFromId(string id)
        {
            int hash = id?.GetHashCode() ?? 0;
            Color[] colors = {
                Color.FromArgb(46, 125, 50),   // Zielony
                Color.FromArgb(25, 118, 210),  // Niebieski
                Color.FromArgb(156, 39, 176),  // Fioletowy
                Color.FromArgb(230, 81, 0),    // Pomarańczowy
                Color.FromArgb(0, 137, 123),   // Teal
                Color.FromArgb(194, 24, 91),   // Różowy
                Color.FromArgb(69, 90, 100),   // Szary
                Color.FromArgb(121, 85, 72)    // Brązowy
            };
            return colors[Math.Abs(hash) % colors.Length];
        }
    }
}
