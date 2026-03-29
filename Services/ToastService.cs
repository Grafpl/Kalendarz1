using System;
using System.Collections.Generic;
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
        private static ToastWindow? _toastWindow;
        private static readonly object _lock = new();

        /// <summary>
        /// Pokazuje toast — zawsze na wierzchu ekranu (prawy-dolny róg).
        /// Działa zarówno z WPF jak i WinForms.
        /// </summary>
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
                lock (_lock)
                {
                    if (_toastWindow == null || !_toastWindow.IsLoaded)
                    {
                        _toastWindow = new ToastWindow();
                        _toastWindow.Show();
                    }
                }

                _toastWindow.AddToast(message, type, durationMs);
            }
            catch
            {
                // Fallback: nie blokuj aplikacji jeśli toast się nie wyświetli
            }
        }
    }

    /// <summary>
    /// Bezramkowe, przezroczyste okno Topmost przyklejone do prawego-dolnego rogu ekranu.
    /// Służy jako kontener na stackowane toasty.
    /// </summary>
    internal class ToastWindow : Window
    {
        private readonly StackPanel _stack;
        private const double WINDOW_WIDTH = 420;
        private const double MARGIN_RIGHT = 24;
        private const double MARGIN_BOTTOM = 24;

        public ToastWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            Width = WINDOW_WIDTH;
            SizeToContent = SizeToContent.Height;
            MaxHeight = SystemParameters.WorkArea.Height - 40;
            ResizeMode = ResizeMode.NoResize;
            IsHitTestVisible = true;
            Focusable = false;

            _stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0)
            };

            Content = _stack;
            PositionBottomRight();

            // Reposition when resolution changes
            SystemParameters.StaticPropertyChanged += (_, _) => PositionBottomRight();
        }

        private void PositionBottomRight()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - WINDOW_WIDTH - MARGIN_RIGHT;
            Top = workArea.Top;
            Height = workArea.Height - MARGIN_BOTTOM;
        }

        protected override void OnMouseDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            // Prevent activation / focus stealing
            e.Handled = true;
        }

        public void AddToast(string message, ToastType type, int durationMs)
        {
            var (bgStart, bgEnd, accentHex, iconChar) = type switch
            {
                ToastType.Success => ("#1a3d25", "#14301d", "#4ADE80", "\u2713"),
                ToastType.Error   => ("#3d1a1a", "#301414", "#F87171", "\u2717"),
                ToastType.Warning => ("#3d3219", "#302714", "#FBBF24", "\u26A0"),
                ToastType.Info    => ("#19283d", "#142030", "#60A5FA", "\u2139"),
                _                 => ("#19283d", "#142030", "#60A5FA", "\u2139")
            };

            var accentColor = (Color)ColorConverter.ConvertFromString(accentHex);

            // ── Karta toasta ──
            var card = new Border
            {
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString(bgStart),
                    (Color)ColorConverter.ConvertFromString(bgEnd),
                    new Point(0, 0), new Point(1, 1)),
                CornerRadius = new CornerRadius(10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, accentColor.R, accentColor.G, accentColor.B)),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(16, 14, 14, 14),
                Margin = new Thickness(0, 8, 0, 0),
                MinWidth = 320,
                MaxWidth = WINDOW_WIDTH - 8,
                Opacity = 0,
                RenderTransform = new TranslateTransform(60, 0),
                Effect = new DropShadowEffect
                {
                    ShadowDepth = 6,
                    Opacity = 0.55,
                    BlurRadius = 24,
                    Color = Colors.Black,
                    Direction = 270
                }
            };

            // ── Lewy pasek akcentowy ──
            var accentBar = new Border
            {
                Width = 4,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(accentColor),
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // ── Ikona ──
            var iconCircle = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = new SolidColorBrush(Color.FromArgb(40, accentColor.R, accentColor.G, accentColor.B)),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = iconChar,
                    Foreground = new SolidColorBrush(accentColor),
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            // ── Tekst ──
            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)),
                FontSize = 13.5,
                FontWeight = FontWeights.Medium,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20,
                MaxWidth = WINDOW_WIDTH - 120,
                VerticalAlignment = VerticalAlignment.Center
            };

            // ── Przycisk zamknij ──
            var closeBtn = new Button
            {
                Content = "\u2715",
                Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(6, -2, 0, 0),
                Padding = new Thickness(6, 3, 6, 3),
                Width = 28,
                Height = 28
            };

            // ── Pasek postępu (countdown) ──
            var progressTrack = new Border
            {
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ClipToBounds = true
            };
            var progressBar = new Border
            {
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = new SolidColorBrush(Color.FromArgb(160, accentColor.R, accentColor.G, accentColor.B)),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = WINDOW_WIDTH - 60
            };
            progressTrack.Child = progressBar;

            // ── Layout ──
            var contentRow = new DockPanel();
            DockPanel.SetDock(accentBar, Dock.Left);
            DockPanel.SetDock(closeBtn, Dock.Right);
            DockPanel.SetDock(iconCircle, Dock.Left);
            contentRow.Children.Add(accentBar);
            contentRow.Children.Add(closeBtn);
            contentRow.Children.Add(iconCircle);
            contentRow.Children.Add(textBlock);

            var outerStack = new StackPanel();
            outerStack.Children.Add(contentRow);
            outerStack.Children.Add(progressTrack);

            card.Child = outerStack;
            _stack.Children.Add(card);

            // ── ANIMACJA WEJŚCIA ──
            var tt = (TranslateTransform)card.RenderTransform;

            var slideIn = new DoubleAnimation(60, 0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            tt.BeginAnimation(TranslateTransform.XProperty, slideIn);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            card.BeginAnimation(OpacityProperty, fadeIn);

            // ── PROGRESS BAR COUNTDOWN ──
            var progressShrink = new DoubleAnimation(WINDOW_WIDTH - 60, 0, TimeSpan.FromMilliseconds(durationMs));
            progressBar.BeginAnimation(WidthProperty, progressShrink);

            // ── DISMISS LOGIC ──
            DispatcherTimer? timer = null;
            bool dismissed = false;

            void Dismiss()
            {
                if (dismissed) return;
                dismissed = true;
                timer?.Stop();

                var slideOut = new DoubleAnimation(0, 80, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                fadeOut.Completed += (_, _) =>
                {
                    _stack.Children.Remove(card);
                    // Zamknij okno toastów jeśli puste
                    if (_stack.Children.Count == 0)
                    {
                        Close();
                    }
                };

                tt.BeginAnimation(TranslateTransform.XProperty, slideOut);
                card.BeginAnimation(OpacityProperty, fadeOut);
            }

            closeBtn.Click += (_, _) => Dismiss();

            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
            timer.Tick += (_, _) => Dismiss();
            timer.Start();

            // ── Hover: pauza timera ──
            card.MouseEnter += (_, _) => timer?.Stop();
            card.MouseLeave += (_, _) =>
            {
                if (!dismissed)
                {
                    timer?.Start();
                }
            };
        }
    }
}
