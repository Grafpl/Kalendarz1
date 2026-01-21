using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Kalendarz1.Komunikator.Services;

namespace Kalendarz1.Komunikator.Views
{
    /// <summary>
    /// Model dla wyświetlania nadawców w dymku
    /// </summary>
    public class BubbleSenderInfo : INotifyPropertyChanged
    {
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string Preview { get; set; }
        public BitmapSource Avatar { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// Dymek powiadomienia o nowych wiadomościach w prawym dolnym rogu
    /// </summary>
    public partial class ChatBubbleNotification : Window
    {
        private bool _isHovered;
        private int _currentCount;
        private ObservableCollection<BubbleSenderInfo> _senders;

        public event EventHandler BubbleClicked;

        public ChatBubbleNotification()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            _senders = new ObservableCollection<BubbleSenderInfo>();
            SendersPanel.ItemsSource = _senders;

            PositionInBottomRight();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Animacja wejścia
            var fadeIn = FindResource("FadeIn") as Storyboard;
            fadeIn?.Begin(this);

            // Odtwórz dźwięk
            try
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
            catch { }

            // Pulsuj badge
            var pulse = FindResource("PulseBadge") as Storyboard;
            pulse?.Begin(this);
        }

        /// <summary>
        /// Pozycjonuje okno w prawym dolnym rogu ekranu
        /// </summary>
        private void PositionInBottomRight()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 10;
            Top = workArea.Bottom - ActualHeight - 10;

            // Zaktualizuj pozycję po załadowaniu gdy znamy rzeczywistą wysokość
            this.SizeChanged += (s, e) =>
            {
                Top = workArea.Bottom - ActualHeight - 10;
            };
        }

        /// <summary>
        /// Aktualizuje licznik i listę nadawców
        /// </summary>
        public void UpdateCount(int count, List<NewMessageInfo> newMessages)
        {
            _currentCount = count;
            CountBadge.Text = count > 99 ? "99+" : count.ToString();

            // Aktualizuj podtytuł
            if (count == 1)
                SubtitleText.Text = "Masz 1 nieprzeczytaną wiadomość";
            else if (count < 5)
                SubtitleText.Text = $"Masz {count} nieprzeczytane wiadomości";
            else
                SubtitleText.Text = $"Masz {count} nieprzeczytanych wiadomości";

            // Zaktualizuj listę nadawców jeśli są nowe wiadomości
            if (newMessages != null && newMessages.Count > 0)
            {
                _senders.Clear();
                foreach (var msg in newMessages)
                {
                    var info = new BubbleSenderInfo
                    {
                        SenderId = msg.SenderId,
                        SenderName = msg.SenderName,
                        Preview = msg.Content?.Length > 40
                            ? msg.Content.Substring(0, 40) + "..."
                            : msg.Content,
                        Avatar = LoadAvatar(msg.SenderId, msg.SenderName)
                    };
                    _senders.Add(info);

                    if (_senders.Count >= 3) break; // Max 3 nadawców
                }

                SendersPanel.Visibility = _senders.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

                // Pulsuj badge przy nowych wiadomościach
                var pulse = FindResource("PulseBadge") as Storyboard;
                pulse?.Begin(this);

                // Dźwięk
                try
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }
                catch { }
            }

            // Zaktualizuj pozycję
            var workArea = SystemParameters.WorkArea;
            Top = workArea.Bottom - ActualHeight - 10;
        }

        /// <summary>
        /// Ładuje avatar użytkownika
        /// </summary>
        private BitmapSource LoadAvatar(string userId, string userName)
        {
            try
            {
                BitmapSource avatar = null;

                if (UserAvatarManager.HasAvatar(userId))
                {
                    using var img = UserAvatarManager.GetAvatarRounded(userId, 32);
                    if (img != null)
                        avatar = ConvertToBitmapSource(img);
                }

                if (avatar == null)
                {
                    using var img = UserAvatarManager.GenerateDefaultAvatar(userName, userId, 32);
                    avatar = ConvertToBitmapSource(img);
                }

                return avatar;
            }
            catch
            {
                return null;
            }
        }

        private void MainBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            BubbleClicked?.Invoke(this, EventArgs.Empty);
        }

        private void MainBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            _isHovered = true;
            MainBorder.BorderThickness = new Thickness(3);
        }

        private void MainBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            _isHovered = false;
            MainBorder.BorderThickness = new Thickness(2);
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

        #region Helpers

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private BitmapSource ConvertToBitmapSource(System.Drawing.Image image)
        {
            if (image == null) return null;

            using var bitmap = new System.Drawing.Bitmap(image);
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

        #endregion
    }
}
