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
        public AvatarPreviewWindow(BitmapSource avatar, string userName, bool isOnline, string status = null)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            // Ustaw avatar
            if (avatar != null)
                AvatarImage.ImageSource = avatar;

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
        public static void ShowPreview(BitmapSource avatar, string userName, bool isOnline, string status = null)
        {
            var preview = new AvatarPreviewWindow(avatar, userName, isOnline, status);
            preview.Show();
        }
    }
}
