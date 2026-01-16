using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kalendarz1.Helpers
{
    /// <summary>
    /// Helper do ustawiania ikon okien WPF z emoji lub tekstu
    /// </summary>
    public static class WindowIconHelper
    {
        /// <summary>
        /// Ustawia ikonę okna WPF z emoji
        /// </summary>
        public static void SetWindowIcon(Window window, string emoji, System.Drawing.Color? backgroundColor = null)
        {
            try
            {
                var icon = CreateIconFromEmoji(emoji, backgroundColor ?? System.Drawing.Color.Transparent);
                if (icon != null)
                {
                    window.Icon = icon;
                }
            }
            catch
            {
                // Ignoruj błędy - okno będzie miało domyślną ikonę
            }
        }

        /// <summary>
        /// Ustawia ikonę okna WPF z emoji przy ładowaniu okna
        /// </summary>
        public static void SetWindowIconOnLoaded(Window window, string emoji, System.Drawing.Color? backgroundColor = null)
        {
            window.Loaded += (s, e) => SetWindowIcon(window, emoji, backgroundColor);
        }

        /// <summary>
        /// Tworzy BitmapSource z emoji
        /// </summary>
        public static BitmapSource CreateIconFromEmoji(string emoji, System.Drawing.Color backgroundColor)
        {
            int size = 64;

            using (var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    // Tło
                    if (backgroundColor != System.Drawing.Color.Transparent)
                    {
                        using (var brush = new SolidBrush(backgroundColor))
                        {
                            g.FillEllipse(brush, 2, 2, size - 4, size - 4);
                        }
                    }
                    else
                    {
                        g.Clear(System.Drawing.Color.Transparent);
                    }

                    // Emoji
                    using (var font = new Font("Segoe UI Emoji", 36, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel))
                    {
                        var textSize = g.MeasureString(emoji, font);
                        float x = (size - textSize.Width) / 2;
                        float y = (size - textSize.Height) / 2;
                        g.DrawString(emoji, font, System.Drawing.Brushes.Black, x, y);
                    }
                }

                return ConvertBitmapToBitmapSource(bitmap);
            }
        }

        /// <summary>
        /// Tworzy ikonę z kolorem i inicjałami
        /// </summary>
        public static BitmapSource CreateIconWithInitials(string initials, System.Drawing.Color backgroundColor, System.Drawing.Color textColor)
        {
            int size = 64;

            using (var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    // Tło - koło
                    using (var brush = new SolidBrush(backgroundColor))
                    {
                        g.FillEllipse(brush, 2, 2, size - 4, size - 4);
                    }

                    // Tekst
                    using (var font = new Font("Segoe UI", 24, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var textBrush = new SolidBrush(textColor))
                    {
                        var textSize = g.MeasureString(initials, font);
                        float x = (size - textSize.Width) / 2;
                        float y = (size - textSize.Height) / 2;
                        g.DrawString(initials, font, textBrush, x, y);
                    }
                }

                return ConvertBitmapToBitmapSource(bitmap);
            }
        }

        private static BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            var hBitmap = bitmap.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
