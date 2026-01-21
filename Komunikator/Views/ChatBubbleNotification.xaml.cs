using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
    /// Kompaktowy dymek powiadomienia o nowych wiadomościach
    /// Pojawia się w prawym dolnym rogu, rozszerza się na hover
    /// Auto-ukrywa się po 8 sekundach
    /// </summary>
    public partial class ChatBubbleNotification : Window
    {
        private const int AUTO_HIDE_SECONDS = 8;

        private bool _isExpanded;
        private bool _isMouseOver;
        private int _currentCount;
        private DispatcherTimer _autoHideTimer;
        private ObservableCollection<BubbleSenderInfo> _senders;

        public event EventHandler BubbleClicked;

        public ChatBubbleNotification()
        {
            InitializeComponent();

            _senders = new ObservableCollection<BubbleSenderInfo>();
            SendersPanel.ItemsSource = _senders;

            // Timer auto-ukrywania
            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(AUTO_HIDE_SECONDS)
            };
            _autoHideTimer.Tick += AutoHideTimer_Tick;

            PositionInBottomRight();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Animacja wejścia
            var slideIn = FindResource("SlideIn") as Storyboard;
            slideIn?.Begin(this);

            // Subtelny dźwięk (opcjonalnie)
            try
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
            catch { }

            // Pulsuj badge
            PulseBadge();

            // Uruchom timer auto-ukrywania
            _autoHideTimer.Start();
        }

        /// <summary>
        /// Pozycjonuje okno w prawym dolnym rogu
        /// </summary>
        private void PositionInBottomRight()
        {
            var workArea = SystemParameters.WorkArea;

            // Wymusza layout aby poznać rzeczywiste rozmiary
            this.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            this.Arrange(new Rect(0, 0, DesiredSize.Width, DesiredSize.Height));

            Left = workArea.Right - ActualWidth - 8;
            Top = workArea.Bottom - ActualHeight - 8;

            // Aktualizuj pozycję przy zmianie rozmiaru
            this.SizeChanged += (s, e) =>
            {
                Left = workArea.Right - ActualWidth - 8;
                Top = workArea.Bottom - ActualHeight - 8;
            };
        }

        /// <summary>
        /// Aktualizuje licznik i listę nadawców
        /// </summary>
        public void UpdateCount(int count, List<NewMessageInfo> newMessages)
        {
            _currentCount = count;
            CountBadge.Text = count > 99 ? "99+" : count.ToString();

            // Tytuł
            if (count == 1)
            {
                TitleText.Text = "Nowa wiadomość";
                SubtitleText.Text = "Kliknij aby przeczytać";
            }
            else
            {
                TitleText.Text = $"{count} nowych wiadomości";
                SubtitleText.Text = "Kliknij aby otworzyć";
            }

            // Zaktualizuj listę nadawców
            if (newMessages != null && newMessages.Count > 0)
            {
                _senders.Clear();
                foreach (var msg in newMessages)
                {
                    var info = new BubbleSenderInfo
                    {
                        SenderId = msg.SenderId,
                        SenderName = msg.SenderName,
                        Preview = TruncateText(msg.Content, 35),
                        Avatar = LoadAvatar(msg.SenderId, msg.SenderName)
                    };
                    _senders.Add(info);
                    if (_senders.Count >= 3) break;
                }

                // Pulsuj badge i reset timer
                PulseBadge();
                ResetAutoHideTimer();
            }

            // Aktualizuj pozycję
            PositionInBottomRight();
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
        }

        private void PulseBadge()
        {
            var pulse = FindResource("PulseBadge") as Storyboard;
            pulse?.Begin(this);
        }

        #region Mouse Events

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseOver = true;
            _autoHideTimer.Stop(); // Zatrzymaj auto-ukrywanie gdy hover

            // Pokaż przycisk zamknij
            CloseBtn.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));

            // Rozwiń jeśli są nadawcy do pokazania
            if (_senders.Count > 0 && !_isExpanded)
            {
                Expand();
            }
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseOver = false;

            // Ukryj przycisk zamknij
            CloseBtn.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)));

            // Zwiń po chwili
            if (_isExpanded)
            {
                Collapse();
            }

            // Wznów timer auto-ukrywania
            ResetAutoHideTimer();
        }

        private void MainBorder_Click(object sender, MouseButtonEventArgs e)
        {
            BubbleClicked?.Invoke(this, EventArgs.Empty);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true; // Nie propaguj do MainBorder
            CloseWithAnimation();
        }

        #endregion

        #region Expand/Collapse

        private void Expand()
        {
            if (_isExpanded) return;
            _isExpanded = true;

            var expand = FindResource("Expand") as Storyboard;
            expand?.Begin(this);
        }

        private void Collapse()
        {
            if (!_isExpanded) return;
            _isExpanded = false;

            var collapse = FindResource("Collapse") as Storyboard;
            collapse?.Begin(this);
        }

        #endregion

        #region Auto-hide

        private void ResetAutoHideTimer()
        {
            _autoHideTimer.Stop();
            if (!_isMouseOver)
            {
                _autoHideTimer.Start();
            }
        }

        private void AutoHideTimer_Tick(object sender, EventArgs e)
        {
            _autoHideTimer.Stop();
            if (!_isMouseOver)
            {
                CloseWithAnimation();
            }
        }

        #endregion

        #region Close

        private void CloseWithAnimation()
        {
            _autoHideTimer.Stop();

            var slideOut = FindResource("SlideOut") as Storyboard;
            if (slideOut != null)
            {
                var clone = slideOut.Clone();
                clone.Completed += (s, e) => Close();
                clone.Begin(this);
            }
            else
            {
                Close();
            }
        }

        #endregion

        #region Avatar Helper

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
