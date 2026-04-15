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
        private int _aggregatedCount = 1;
        private ChangeNotificationType _lastNotificationType;

        // Jeden aktywny popup - nowe zmiany są dodawane do istniejącego okna
        private static ChangeNotificationPopup _currentPopup;
        private static readonly object _lock = new object();

        // C: Kolejka zmian z debouncing (flush co 300ms)
        private static readonly Queue<ChangeNotificationItem> _pendingQueue = new Queue<ChangeNotificationItem>();
        private static DispatcherTimer _debounceTimer;

        /// <summary>
        /// Zdarzenie wywoływane po kliknięciu w powiadomienie (przycisk Przejdź).
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
            _lastNotificationType = item.NotificationType;

            // Kolor akcentu (dla avatara w stopce)
            Color accentColor = item.NotificationType switch
            {
                ChangeNotificationType.InlineEdit => Color.FromRgb(76, 175, 80),
                ChangeNotificationType.FormSave => Color.FromRgb(56, 142, 60),
                ChangeNotificationType.DragDrop => Color.FromRgb(59, 130, 246),
                ChangeNotificationType.Confirmation => Color.FromRgb(245, 158, 11),
                ChangeNotificationType.BulkOperation => Color.FromRgb(139, 92, 246),
                ChangeNotificationType.Delete => Color.FromRgb(239, 68, 68),
                _ => Color.FromRgb(76, 175, 80)
            };

            avatarBorder.Background = new SolidColorBrush(accentColor);
            txtTitle.Text = item.Title ?? "Zmiana";
            txtSubtitle.Text = !string.IsNullOrEmpty(item.Dostawca)
                ? $"{item.Dostawca}  |  LP: {item.LP}"
                : $"LP: {item.LP}";
            txtTimestamp.Text = item.Timestamp.ToString("HH:mm:ss");

            // Data odbioru dostawy - prominentny badge
            if (item.DataOdbioru.HasValue)
            {
                var culture = new System.Globalization.CultureInfo("pl-PL");
                string dzien = culture.DateTimeFormat.GetAbbreviatedDayName(item.DataOdbioru.Value.DayOfWeek).ToLower();
                txtDataOdbioru.Text = $"{dzien} {item.DataOdbioru.Value:dd.MM.yyyy}";
                borderDataOdbioru.Visibility = Visibility.Visible;
            }
            else
            {
                borderDataOdbioru.Visibility = Visibility.Collapsed;
            }

            // Avatar — inicjały + zdjęcie
            string initials = GetInitials(item.UserName ?? "");
            avatarInitials.Text = initials;
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

            // Zbuduj wiersze zmian (każdy z własnym przyciskiem "Przejdź")
            changesPanel.Children.Clear();
            if (item.Changes != null)
            {
                foreach (var change in item.Changes)
                {
                    changesPanel.Children.Add(BuildChangeRow(change, item.LP, item.DataOdbioru));
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Wymuś layout aby ActualWidth/ActualHeight były dostępne
                this.UpdateLayout();

                // Pozycja: prawy dolny róg ekranu
                var workArea = SystemParameters.WorkArea;
                double popupWidth = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
                double popupHeight = this.ActualHeight > 0 ? this.ActualHeight : this.Height;
                this.Left = workArea.Right - popupWidth - 4;
                this.Top = workArea.Bottom - popupHeight - 8;

                // Animacja wejścia: slide z prawej + fade in
                slideTransform.X = 400;
                var slideIn = new DoubleAnimation(400, 0, TimeSpan.FromMilliseconds(350))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));

                slideTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
                mainBorder.BeginAnimation(OpacityProperty, fadeIn);

                StartDismissTimer();
            }
            catch
            {
                // Gdyby coś poszło nie tak — zamknij cicho
                try { this.Close(); } catch { }
            }
        }

        private void StartDismissTimer()
        {
            _dismissTimer?.Stop();
            _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _dismissTimer.Tick += (s2, args) =>
            {
                _dismissTimer.Stop();
                AnimateClose();
            };
            _dismissTimer.Start();
        }

        /// <summary>
        /// Dodaje kolejne zmiany do istniejącego okienka. Reset timera auto-zamknięcia.
        /// Przelicza pozycję (Top) gdy wysokość rośnie - żeby trzymać się dolnej krawędzi.
        /// </summary>
        public void AppendChanges(ChangeNotificationItem item)
        {
            if (item == null || item.Changes == null || item.Changes.Count == 0) return;

            _aggregatedCount++;

            // Dodaj nowe wiersze zmian z separatorem (label z LP/dostawca)
            var sepGrid = new Grid();
            sepGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var sepText = new TextBlock
            {
                Text = !string.IsNullOrEmpty(item.Dostawca) ? $"▸ {item.Dostawca}  |  LP: {item.LP}" : $"▸ LP: {item.LP}",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(7, 4, 4, 1)
            };
            sepGrid.Children.Add(sepText);
            changesPanel.Children.Add(sepGrid);

            foreach (var change in item.Changes)
            {
                changesPanel.Children.Add(BuildChangeRow(change, item.LP, item.DataOdbioru));
            }

            // Zaktualizuj stopkę: pokaż licznik zagregowanych zmian
            txtTitle.Text = $"Zmiany ({_aggregatedCount})";
            txtSubtitle.Text = "Wiele dostaw zmodyfikowanych";
            txtTimestamp.Text = DateTime.Now.ToString("HH:mm:ss");

            // Aktualizuj Data odbioru: jeśli wszystkie zmiany na ten sam dzień - pokaż; inaczej ukryj
            if (item.DataOdbioru.HasValue)
            {
                // Jeśli nowa data różni się od wyświetlanej - ukryj (dotyczy różnych dni)
                var culture = new System.Globalization.CultureInfo("pl-PL");
                string dzien = culture.DateTimeFormat.GetAbbreviatedDayName(item.DataOdbioru.Value.DayOfWeek).ToLower();
                string newDisplay = $"{dzien} {item.DataOdbioru.Value:dd.MM.yyyy}";
                if (borderDataOdbioru.Visibility == Visibility.Visible && txtDataOdbioru.Text != newDisplay)
                {
                    // Różne dni - pokaż zakres zamiast konkretnej daty
                    txtDataOdbioru.Text = "⚠ różne dni";
                }
                // jeśli ta sama data - zostaw bez zmian
            }

            // Reset auto-dismiss (nowa zmiana przedłuża wyświetlanie)
            StartDismissTimer();

            // Po layout'cie popraw Top aby okno trzymało się dolnej krawędzi
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    this.UpdateLayout();
                    var workArea = SystemParameters.WorkArea;
                    this.Top = workArea.Bottom - this.ActualHeight - 8;
                }
                catch { }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void MainBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // G: Najazd myszą - zatrzymaj auto-close
            _dismissTimer?.Stop();
        }

        private void MainBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // G: Zjazd myszą - wznów auto-close (4s, bo już widział popup)
            if (!_isClosing)
            {
                _dismissTimer?.Stop();
                _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                _dismissTimer.Tick += (s2, args) => { _dismissTimer.Stop(); AnimateClose(); };
                _dismissTimer.Start();
            }
        }

        private void BtnRowGoTo_Click(object sender, RoutedEventArgs e)
        {
            // Przejdź do konkretnej dostawy (z wiersza zmian)
            e.Handled = true;
            if (sender is FrameworkElement fe && fe.Tag is RowGoToInfo info)
            {
                _dismissTimer?.Stop();
                NotificationClicked?.Invoke(info.LP, info.DataOdbioru);
                AnimateClose();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _dismissTimer?.Stop();
            e.Handled = true;
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
                    if (_currentPopup == this) _currentPopup = null;
                }
                this.Close();
            };

            slideTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
            mainBorder.BeginAnimation(OpacityProperty, fadeOut);
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

        private static Color GetChangeColor(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return Color.FromRgb(148, 163, 184); // szary
            var f = fieldName.ToLowerInvariant();
            // 🟢 Fizyczne: waga, sztuki, auta
            if (f.Contains("waga") || f.Contains("szt") || f.Contains("aut") || f.Contains("ubytek"))
                return Color.FromRgb(22, 163, 74); // zielony
            // 🟡 Finansowe: cena, typ ceny, dodatek
            if (f.Contains("cen") || f.Contains("dodat"))
                return Color.FromRgb(234, 179, 8); // żółty/złoty
            // 🔵 Logistyczne: data, odbioru
            if (f.Contains("dat") || f.Contains("odbi"))
                return Color.FromRgb(59, 130, 246); // niebieski
            // 🟣 Status/potwierdzenie
            if (f.Contains("status") || f.Contains("potw") || f.Contains("bufor") || f.Contains("konfir"))
                return Color.FromRgb(139, 92, 246); // fioletowy
            // 🟠 Notatki/uwagi
            if (f.Contains("uwag") || f.Contains("notat") || f.Contains("komentar"))
                return Color.FromRgb(249, 115, 22); // pomarańczowy
            return Color.FromRgb(148, 163, 184); // szary (default)
        }

        private class RowGoToInfo
        {
            public string LP { get; set; }
            public DateTime? DataOdbioru { get; set; }
        }

        private Border BuildChangeRow(FieldChange change, string lp, DateTime? dataOdbioru)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) }); // kolorowy pasek
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // guzik Przejdź

            // Kolorowy pasek per typ zmiany (I)
            var accentColor = GetChangeColor(change.FieldName);
            var colorBar = new Border
            {
                Background = new SolidColorBrush(accentColor),
                CornerRadius = new CornerRadius(2, 0, 0, 2),
                Margin = new Thickness(0, 2, 4, 2)
            };
            Grid.SetColumn(colorBar, 0);
            grid.Children.Add(colorBar);

            var fieldName = new TextBlock
            {
                Text = change.FieldName,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(fieldName, 1);
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
            Grid.SetColumn(oldVal, 2);
            grid.Children.Add(oldVal);

            var arrow = new TextBlock
            {
                Text = "\u2192",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 3, 0)
            };
            Grid.SetColumn(arrow, 3);
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
            Grid.SetColumn(newVal, 4);
            grid.Children.Add(newVal);

            // Przycisk "Przejdź" dla tej konkretnej zmiany
            var goToButton = new Button
            {
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(6, 2, 6, 2),
                ToolTip = $"Przejdź do LP: {lp}",
                Tag = new RowGoToInfo { LP = lp, DataOdbioru = dataOdbioru }
            };
            goToButton.Template = new ControlTemplate(typeof(Button))
            {
                VisualTree = CreateGoToButtonTemplate()
            };
            goToButton.Click += BtnRowGoTo_Click;
            Grid.SetColumn(goToButton, 5);
            grid.Children.Add(goToButton);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 4, 7, 4),
                Margin = new Thickness(4, 2, 4, 2),
                Child = grid
            };
        }

        private static FrameworkElementFactory CreateGoToButtonTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(224, 242, 254)));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            border.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));

            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetValue(TextBlock.TextProperty, "➡");
            text.SetValue(TextBlock.FontSizeProperty, 11.0);
            text.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            text.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(3, 105, 161)));
            text.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            border.AppendChild(text);
            return border;
        }

        /// <summary>
        /// Statyczna fabryka — tworzy i pokazuje popup na UI uthread.
        /// Wywołaj z dowolnego miejsca (np. z WidokKalendarzaWPF).
        /// </summary>
        /// <summary>
        /// Zgłasza zmianę do wyświetlenia. Z debouncing (300ms) - kolejne zmiany w tym oknie
        /// czasowym są agregowane w jednym popupie aby uniknąć spamowania UI.
        /// </summary>
        public static void ShowNotification(ChangeNotificationItem item)
        {
            if (item == null || item.Changes == null || item.Changes.Count == 0) return;

            // Musi być na głównym UI wątku
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => ShowNotification(item)));
                return;
            }

            // C: Dodaj do kolejki i zaplanuj flush za 300ms
            lock (_lock)
            {
                _pendingQueue.Enqueue(item);

                if (_debounceTimer == null)
                {
                    _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                    _debounceTimer.Tick += (s, e) =>
                    {
                        _debounceTimer.Stop();
                        FlushQueue();
                    };
                }

                // Restart timera - każda nowa zmiana resetuje debounce
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
        }

        /// <summary>
        /// Przetwarza wszystkie zakolejkowane zmiany - tworzy jeden popup z pierwszego
        /// i dopisuje kolejne. Dzięki temu bulk-operacje (np. potwierdź 10 dostaw) generują
        /// jeden popup zamiast 10 kolejnych Append calls.
        /// </summary>
        private static void FlushQueue()
        {
            List<ChangeNotificationItem> batch;
            lock (_lock)
            {
                if (_pendingQueue.Count == 0) return;
                batch = new List<ChangeNotificationItem>(_pendingQueue);
                _pendingQueue.Clear();
            }

            try
            {
                ChangeNotificationPopup existing;
                lock (_lock) { existing = _currentPopup; }

                if (existing != null && !existing._isClosing)
                {
                    // Dopisz wszystkie zebrane zmiany do istniejącego okna
                    foreach (var it in batch) existing.AppendChanges(it);
                    return;
                }

                // Utwórz nowe okno z pierwszą zmianą
                var popup = new ChangeNotificationPopup();
                popup.Configure(batch[0]);
                lock (_lock) { _currentPopup = popup; }
                popup.Show();

                // Dopisz pozostałe zmiany
                for (int i = 1; i < batch.Count; i++)
                {
                    popup.AppendChanges(batch[i]);
                }
            }
            catch { /* cicho — nie blokuj głównej logiki */ }
        }
    }
}
