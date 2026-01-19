using System;
using System.Drawing;
using System.Windows.Media.Imaging;

namespace Kalendarz1.Komunikator.Models
{
    /// <summary>
    /// Model użytkownika czatu
    /// </summary>
    public class ChatUser
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
        public int UnreadCount { get; set; }
        public string LastMessage { get; set; }
        public DateTime? LastMessageTime { get; set; }

        /// <summary>
        /// Pobiera avatar użytkownika jako BitmapSource dla WPF
        /// </summary>
        public BitmapSource GetAvatarBitmap(int size = 48)
        {
            try
            {
                if (UserAvatarManager.HasAvatar(UserId))
                {
                    using (var avatar = UserAvatarManager.GetAvatarRounded(UserId, size))
                    {
                        if (avatar != null)
                        {
                            return ConvertToBitmapSource(avatar);
                        }
                    }
                }

                // Wygeneruj domyślny avatar
                using (var defaultAvatar = UserAvatarManager.GenerateDefaultAvatar(Name, UserId, size))
                {
                    return ConvertToBitmapSource(defaultAvatar);
                }
            }
            catch
            {
                return null;
            }
        }

        private BitmapSource ConvertToBitmapSource(Image image)
        {
            if (image == null) return null;

            using (var bitmap = new Bitmap(image))
            {
                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        /// <summary>
        /// Formatuje czas ostatniej wiadomości
        /// </summary>
        public string FormattedLastMessageTime
        {
            get
            {
                if (!LastMessageTime.HasValue) return "";

                var diff = DateTime.Now - LastMessageTime.Value;

                if (diff.TotalMinutes < 1) return "Teraz";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min";
                if (diff.TotalHours < 24) return LastMessageTime.Value.ToString("HH:mm");
                if (diff.TotalDays < 7) return LastMessageTime.Value.ToString("ddd");
                return LastMessageTime.Value.ToString("dd.MM");
            }
        }

        /// <summary>
        /// Formatuje status online
        /// </summary>
        public string OnlineStatus
        {
            get
            {
                if (IsOnline) return "Online";
                if (!LastSeen.HasValue) return "Offline";

                var diff = DateTime.Now - LastSeen.Value;
                if (diff.TotalMinutes < 5) return "Przed chwilą";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min temu";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} godz. temu";
                return LastSeen.Value.ToString("dd.MM HH:mm");
            }
        }
    }
}
