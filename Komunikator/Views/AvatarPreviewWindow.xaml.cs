using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Kalendarz1.Komunikator.Views
{
    public partial class AvatarPreviewWindow : Window
    {
        private const int HighResSize = 240; // Rozmiar wysokiej jakości avatara

        public AvatarPreviewWindow(string userId, string userName, bool isOnline, string status = null)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            // Załaduj avatar w wysokiej rozdzielczości
            LoadHighResAvatar(userId, userName);

            // Ustaw nazwę
            UserName.Text = userName;

            // Ustaw status online
            if (isOnline)
            {
                OnlineIndicator.Visibility = Visibility.Visible;
                GlowEffect.Opacity = 0.3;
                AvatarBorder.Stroke = new SolidColorBrush(Color.FromRgb(37, 211, 102)); // #25D366
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(37, 211, 102));
                UserStatus.Text = "Online";
                UserStatus.Foreground = new SolidColorBrush(Color.FromRgb(37, 211, 102));
            }
            else
            {
                OnlineIndicator.Visibility = Visibility.Collapsed;
                GlowEffect.Opacity = 0;
                UserStatus.Text = status ?? "Offline";
            }

            Loaded += AvatarPreviewWindow_Loaded;
        }

        private void LoadHighResAvatar(string userId, string userName)
        {
            try
            {
                BitmapSource avatar = null;

                // Próbuj załadować avatar użytkownika w wysokiej rozdzielczości
                if (UserAvatarManager.HasAvatar(userId))
                {
                    using (var img = UserAvatarManager.GetAvatarRounded(userId, HighResSize))
                    {
                        if (img != null)
                            avatar = ConvertToBitmapSource(img);
                    }
                }

                // Jeśli nie ma avatara, wygeneruj domyślny w wysokiej rozdzielczości
                if (avatar == null)
                {
                    using (var img = UserAvatarManager.GenerateDefaultAvatar(userName, userId, HighResSize))
                    {
                        avatar = ConvertToBitmapSource(img);
                    }
                }

                if (avatar != null)
                    AvatarImage.ImageSource = avatar;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading high-res avatar: {ex.Message}");
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

        private void AvatarPreviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Animacja wejścia
            var fadeIn = FindResource("FadeIn") as Storyboard;
            fadeIn?.Begin(this);

            // Focus na okno aby działały klawisze
            Focus();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Kliknięcie poza przyciskiem zamyka okno
            if (e.OriginalSource is not System.Windows.Controls.Button)
            {
                CloseWithAnimation();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Escape lub Enter zamyka okno
            if (e.Key == Key.Escape || e.Key == Key.Enter || e.Key == Key.Space)
            {
                CloseWithAnimation();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
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

        /// <summary>
        /// Statyczna metoda do pokazania podglądu avatara
        /// </summary>
        public static void ShowPreview(string userId, string userName, bool isOnline, string status = null)
        {
            var preview = new AvatarPreviewWindow(userId, userName, isOnline, status);
            preview.Show();
        }
    }
}
