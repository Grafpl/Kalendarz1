using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Kalendarz1.Komunikator.Models;

namespace Kalendarz1.Komunikator.Views
{
    /// <summary>
    /// Popup powiadomienia o nowej wiadomości
    /// </summary>
    public partial class ChatNotificationPopup : Window
    {
        private readonly ChatMessage _message;
        private DispatcherTimer _autoCloseTimer;

        public event EventHandler MessageClicked;

        public ChatNotificationPopup(ChatMessage message)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            _message = message;

            // Ustaw treść
            SenderName.Text = message.SenderName;
            MessagePreview.Text = message.Content?.Length > 50
                ? message.Content.Substring(0, 50) + "..."
                : message.Content;

            // Załaduj avatar
            LoadSenderAvatar();

            // Pozycja w prawym dolnym rogu
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 10;
            Top = workArea.Bottom - Height - 10;

            Loaded += ChatNotificationPopup_Loaded;
        }

        private void LoadSenderAvatar()
        {
            try
            {
                BitmapSource avatar = null;
                if (UserAvatarManager.HasAvatar(_message.SenderId))
                {
                    using (var img = UserAvatarManager.GetAvatarRounded(_message.SenderId, 48))
                    {
                        if (img != null)
                            avatar = ConvertToBitmapSource(img);
                    }
                }

                if (avatar == null)
                {
                    using (var img = UserAvatarManager.GenerateDefaultAvatar(_message.SenderName, _message.SenderId, 48))
                    {
                        avatar = ConvertToBitmapSource(img);
                    }
                }

                if (avatar != null)
                    SenderAvatar.ImageSource = avatar;
            }
            catch { }
        }

        private void ChatNotificationPopup_Loaded(object sender, RoutedEventArgs e)
        {
            // Animacja wejścia
            var fadeIn = FindResource("FadeIn") as Storyboard;
            fadeIn?.Begin(this);

            // Auto-zamknij po 5 sekundach
            _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _autoCloseTimer.Tick += (s, args) =>
            {
                _autoCloseTimer.Stop();
                CloseWithAnimation();
            };
            _autoCloseTimer.Start();

            // Odtwórz dźwięk powiadomienia
            try
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
            catch { }
        }

        private void Border_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _autoCloseTimer?.Stop();
            MessageClicked?.Invoke(this, EventArgs.Empty);
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer?.Stop();
            CloseWithAnimation();
        }

        private void CloseWithAnimation()
        {
            var fadeOut = FindResource("FadeOut") as Storyboard;
            if (fadeOut != null)
            {
                fadeOut.Completed += (s, e) => Close();
                fadeOut.Begin(this);
            }
            else
            {
                Close();
            }
        }

        private BitmapSource ConvertToBitmapSource(System.Drawing.Image image)
        {
            if (image == null) return null;

            using (var bitmap = new System.Drawing.Bitmap(image))
            {
                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
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
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
