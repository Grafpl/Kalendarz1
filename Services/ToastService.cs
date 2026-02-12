using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Kalendarz1.Services
{
    public enum ToastType
    {
        Success,
        Error,
        Warning,
        Info
    }

    public static class ToastService
    {
        public static void Show(string message, ToastType type = ToastType.Info, int durationMs = 4000)
        {
            if (Application.Current?.Dispatcher == null) return;

            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => Show(message, type, durationMs)));
                return;
            }

            try
            {
                var window = Application.Current.Windows.Count > 0
                    ? GetActiveWindow()
                    : null;

                if (window == null) return;

                var adornerLayer = FindOrCreateToastLayer(window);
                if (adornerLayer == null) return;

                ShowToastInLayer(adornerLayer, message, type, durationMs);
            }
            catch
            {
                // Fallback: nie blokuj aplikacji jeśli toast się nie wyświetli
            }
        }

        private static Window GetActiveWindow()
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (w.IsActive) return w;
            }
            return Application.Current.MainWindow;
        }

        private static Panel FindOrCreateToastLayer(Window window)
        {
            if (window.Content is Grid rootGrid)
            {
                foreach (var child in rootGrid.Children)
                {
                    if (child is StackPanel sp && sp.Tag as string == "ToastLayer")
                        return sp;
                }

                var toastLayer = CreateToastPanel();
                rootGrid.Children.Add(toastLayer);
                return toastLayer;
            }

            if (window.Content is UIElement existingContent)
            {
                var newGrid = new Grid();
                window.Content = null;
                newGrid.Children.Add(existingContent);
                var toastLayer = CreateToastPanel();
                newGrid.Children.Add(toastLayer);
                window.Content = newGrid;
                return toastLayer;
            }

            return null;
        }

        private static StackPanel CreateToastPanel()
        {
            return new StackPanel
            {
                Tag = "ToastLayer",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 20, 20),
                IsHitTestVisible = true
            };
        }

        private static void ShowToastInLayer(Panel layer, string message, ToastType type, int durationMs)
        {
            var (gradStart, gradEnd, borderAccent, iconText) = type switch
            {
                ToastType.Success => ("#2D6A3E", "#1E4D2B", "#4CAF50", "\u2713"),
                ToastType.Error   => ("#6A2D2D", "#4D1E1E", "#E74C3C", "\u2717"),
                ToastType.Warning => ("#6A5A2D", "#4D3F1E", "#F39C12", "\u26A0"),
                ToastType.Info    => ("#2D4A6A", "#1E334D", "#3498DB", "\u2139"),
                _                 => ("#2D4A6A", "#1E334D", "#3498DB", "\u2139")
            };

            var accentColor = (Color)ColorConverter.ConvertFromString(borderAccent);

            var border = new Border
            {
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString(gradStart),
                    (Color)ColorConverter.ConvertFromString(gradEnd),
                    0),
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Color.FromArgb(77, accentColor.R, accentColor.G, accentColor.B)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 6, 0, 0),
                MinWidth = 280,
                MaxWidth = 420,
                Opacity = 0,
                IsHitTestVisible = true,
                RenderTransform = new TranslateTransform(50, 0),
                Effect = new DropShadowEffect
                {
                    ShadowDepth = 4,
                    Opacity = 0.4,
                    BlurRadius = 16,
                    Color = Colors.Black
                }
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            // Icon in semi-transparent circle
            var iconCircle = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            iconCircle.Child = new TextBlock
            {
                Text = iconText,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(iconCircle);

            stack.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 320
            });

            // Close button
            var closeBtn = new Button
            {
                Content = "\u2715",
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8, -2, 0, 0),
                Padding = new Thickness(4, 2, 4, 2)
            };

            var outerGrid = new Grid();
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(stack, 0);
            Grid.SetColumn(closeBtn, 1);
            outerGrid.Children.Add(stack);
            outerGrid.Children.Add(closeBtn);

            border.Child = outerGrid;
            layer.Children.Add(border);

            // Entry animation: slide-in from right + fade-in
            var translateTransform = (TranslateTransform)border.RenderTransform;

            var slideIn = new DoubleAnimation(50, 0, TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            translateTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            border.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Dismiss action
            DispatcherTimer timer = null;
            Action dismiss = null;
            dismiss = () =>
            {
                timer?.Stop();

                var slideOut = new DoubleAnimation(0, 60, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                fadeOut.Completed += (s2, e2) =>
                {
                    layer.Children.Remove(border);
                };

                translateTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
                border.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };

            // Close button click
            closeBtn.Click += (s, e) => dismiss();

            // Auto-dismiss timer
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
            timer.Tick += (s, e) => dismiss();
            timer.Start();
        }
    }
}
