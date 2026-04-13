using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Kalendarz1.Zywiec.Kalendarz
{
    public partial class ChangeNotificationPopup : Window
    {
        private DispatcherTimer _dismissTimer;
        private bool _isClosing = false;
        private string _lp;
        private DateTime? _dataOdbioru;

        // Statyczne zarządzanie pozycją — stackowanie wielu popupów
        private static readonly List<ChangeNotificationPopup> _activePopups = new List<ChangeNotificationPopup>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Zdarzenie wywoływane po kliknięciu w powiadomienie.
        /// LP dostawy i data odbioru przekazywane do subskrybentów.
        /// </summary>
        public static event Action<string, DateTime?> NotificationClicked;

        public ChangeNotificationPopup()
        {
            InitializeComponent();
        }

        public void Configure(ChangeNotificationItem item)
        {
            if (item == null) return;

            _lp = item.LP;
            _dataOdbioru = item.Timestamp;

            // Kolor nagłówka
            Color headerColor = item.NotificationType switch
            {
                ChangeNotificationType.InlineEdit => Color.FromRgb(76, 175, 80),
                ChangeNotificationType.FormSave => Color.FromRgb(56, 142, 60),
                ChangeNotificationType.DragDrop => Color.FromRgb(59, 130, 246),
                ChangeNotificationType.Confirmation => Color.FromRgb(245, 158, 11),
                ChangeNotificationType.BulkOperation => Color.FromRgb(139, 92, 246),
                ChangeNotificationType.Delete => Color.FromRgb(239, 68, 68),
                _ => Color.FromRgb(76, 175, 80)
            };

            headerBorder.Background = new SolidColorBrush(headerColor);
            txtTitle.Text = item.Title ?? "Zmiana";
            txtSubtitle.Text = !string.IsNullOrEmpty(item.Dostawca)
                ? $"{item.Dostawca}  |  LP: {item.LP}"
                : $"LP: {item.LP}";
            txtTimestamp.Text = item.Timestamp.ToString("HH:mm:ss");

            // Avatar — inicjały + zdjęcie
            string initials = GetInitials(item.UserName ?? "");
            avatarInitials.Text = initials;
            avatarInitials.Foreground = new SolidColorBrush(headerColor);
            avatarBorder.Visibility = Visibility.Visible;
            avatarImage.Visibility = Visibility.Collapsed;

            try
            {
                if (!string.IsNullOrEmpty(item.UserId) && UserAvatarManager.HasAvatar(item.UserId))
                {
                    using (var avatar = UserAvatarManager.GetAvatarRounded(item.UserId, 72))
                    {
                        if (avatar != null)
                        {
                            var brush = new ImageBrush(ConvertToImageSource(avatar))
                            {
                                Stretch = Stretch.UniformToFill
                            };
                            avatarImage.Fill = brush;
                            avatarImage.Visibility = Visibility.Visible;
                            avatarBorder.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
            catch { }

            // Zbuduj wiersze zmian
            changesPanel.Children.Clear();
            if (item.Changes != null)
            {
                foreach (var change in item.Changes)
                {
                    changesPanel.Children.Add(BuildChangeRow(change));
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Wymuś layout aby ActualWidth/ActualHeight były dostępne
                this.UpdateLayout();

                // Pozycja: prawy górny róg ekranu
                var workArea = SystemParameters.WorkArea;
                double popupWidth = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
                this.Left = workArea.Right - popupWidth - 4;

                lock (_lock)
                {
                    // Oblicz offset Y — stackuj poniżej istniejących popupów
                    double offsetY = workArea.Top + 8;
                    foreach (var popup in _activePopups)
                    {
                        if (!popup._isClosing && popup.ActualHeight > 0)
                            offsetY += popup.ActualHeight + 4;
                    }
                    this.Top = offsetY;
                    _activePopups.Add(this);
                }

                // Animacja wejścia: slide z prawej + fade in
                var slideIn = new DoubleAnimation(400, 0, TimeSpan.FromMilliseconds(350))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));

                slideTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
                mainBorder.BeginAnimation(OpacityProperty, fadeIn);

                // Auto-dismiss po 6 sekundach
                _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                _dismissTimer.Tick += (s2, args) =>
                {
                    _dismissTimer.Stop();
                    AnimateClose();
                };
                _dismissTimer.Start();
            }
            catch
            {
                // Gdyby coś poszło nie tak — zamknij cicho
                try { this.Close(); } catch { }
            }
        }

        private void MainBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dismissTimer?.Stop();
            NotificationClicked?.Invoke(_lp, _dataOdbioru);
            AnimateClose();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _dismissTimer?.Stop();
            e.Handled = true; // Nie propaguj do MainBorder
            AnimateClose();
        }

        private void AnimateClose()
        {
            if (_isClosing) return;
            _isClosing = true;

            var slideOut = new DoubleAnimation(0, 400, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, e) =>
            {
                lock (_lock)
                {
                    _activePopups.Remove(this);
                    RepositionActivePopups();
                }
                this.Close();
            };

            slideTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
            mainBorder.BeginAnimation(OpacityProperty, fadeOut);
        }

        private static void RepositionActivePopups()
        {
            var workArea = SystemParameters.WorkArea;
            double offsetY = workArea.Top + 8;

            foreach (var popup in _activePopups)
            {
                if (popup._isClosing) continue;

                var slideY = new DoubleAnimation(popup.Top, offsetY, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                popup.BeginAnimation(Window.TopProperty, slideY);
                offsetY += popup.ActualHeight + 4;
            }
        }

        private static ImageSource ConvertToImageSource(System.Drawing.Image image)
        {
            using (var ms = new MemoryStream())
            {
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = ms;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[1][0])}";
            if (parts.Length == 1 && parts[0].Length >= 2)
                return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[0][1])}";
            return parts.Length > 0 ? parts[0].Substring(0, 1).ToUpper() : "";
        }

        private static Border BuildChangeRow(FieldChange change)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var fieldName = new TextBlock
            {
                Text = change.FieldName,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 8, 0)
            };
            Grid.SetColumn(fieldName, 0);
            grid.Children.Add(fieldName);

            var oldVal = new TextBlock
            {
                Text = string.IsNullOrEmpty(change.OldValue) ? "-" : change.OldValue,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                TextDecorations = TextDecorations.Strikethrough,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 4, 0)
            };
            Grid.SetColumn(oldVal, 1);
            grid.Children.Add(oldVal);

            var arrow = new TextBlock
            {
                Text = "\u2192",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0)
            };
            Grid.SetColumn(arrow, 2);
            grid.Children.Add(arrow);

            var newVal = new TextBlock
            {
                Text = string.IsNullOrEmpty(change.NewValue) ? "-" : change.NewValue,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(4, 0, 4, 0)
            };
            Grid.SetColumn(newVal, 3);
            grid.Children.Add(newVal);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(4, 2, 4, 2),
                Child = grid
            };
        }

        /// <summary>
        /// Statyczna fabryka — tworzy i pokazuje popup na UI uthread.
        /// Wywołaj z dowolnego miejsca (np. z WidokKalendarzaWPF).
        /// </summary>
        public static void ShowNotification(ChangeNotificationItem item)
        {
            if (item == null || item.Changes == null || item.Changes.Count == 0) return;

            // Ogranicz do max 5 aktywnych popupów
            lock (_lock)
            {
                if (_activePopups.Count >= 5) return;
            }

            // Musi być na głównym UI wątku
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => ShowNotification(item)));
                return;
            }

            try
            {
                var popup = new ChangeNotificationPopup();
                popup.Configure(item);
                popup.Show();
            }
            catch { /* cicho — nie blokuj głównej logiki */ }
        }
    }
}
