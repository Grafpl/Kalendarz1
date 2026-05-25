// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Controls/AvatarControl.xaml.cs
// ════════════════════════════════════════════════════════════════════════════
// Okrągły avatar (WPF) — wierny oryginałowi WinForms (UserAvatarManager):
// zdjęcie PNG z sieci \\...\Avatary\{userId}.png, a gdy brak — inicjały na
// kolorowym tle (kolor deterministyczny z hasha ID, te same 8 kolorów co stary).
// Cache ścieżek i obrazów (statyczny) → brak powtarzanych odpytań sieci.
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kalendarz1;

namespace Kalendarz1.Transport.WPF.Controls
{
    public partial class AvatarControl : UserControl
    {
        private static readonly Dictionary<string, string?> _pathCache = new();
        private static readonly Dictionary<string, ImageSource?> _imgCache = new();

        public static readonly DependencyProperty UserIdProperty =
            DependencyProperty.Register(nameof(UserId), typeof(string), typeof(AvatarControl),
                new PropertyMetadata(null, OnChanged));
        public static readonly DependencyProperty DisplayNameProperty =
            DependencyProperty.Register(nameof(DisplayName), typeof(string), typeof(AvatarControl),
                new PropertyMetadata(null, OnChanged));
        public static readonly DependencyProperty DiameterProperty =
            DependencyProperty.Register(nameof(Diameter), typeof(double), typeof(AvatarControl),
                new PropertyMetadata(30.0, OnChanged));

        public string? UserId { get => (string?)GetValue(UserIdProperty); set => SetValue(UserIdProperty, value); }
        public string? DisplayName { get => (string?)GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }
        public double Diameter { get => (double)GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }

        public AvatarControl()
        {
            InitializeComponent();
            Loaded += (_, _) => Render();
            Render();
        }

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AvatarControl)d).Render();

        private void Render()
        {
            double dia = Diameter <= 0 ? 30 : Diameter;
            Width = Height = dia;
            Circle.Width = Circle.Height = dia;
            Initials.FontSize = Math.Max(8, dia / 2.6);

            var img = TryGetImage(UserId);
            if (img != null)
            {
                Circle.Fill = new ImageBrush(img) { Stretch = Stretch.UniformToFill };
                Initials.Visibility = Visibility.Collapsed;
            }
            else
            {
                Circle.Fill = new SolidColorBrush(KolorZId(UserId ?? DisplayName ?? "?"));
                Initials.Text = Inicjaly(DisplayName ?? UserId ?? "?");
                Initials.Visibility = Visibility.Visible;
            }
        }

        private static ImageSource? TryGetImage(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return null;
            if (_imgCache.TryGetValue(userId, out var cached)) return cached;

            if (!_pathCache.TryGetValue(userId, out var path))
            {
                try { path = UserAvatarManager.GetAvatarFilePathOrNull(userId); } catch { path = null; }
                _pathCache[userId] = path;
            }

            ImageSource? img = null;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(path);
                    bi.DecodePixelWidth = 96;
                    bi.EndInit();
                    bi.Freeze();
                    img = bi;
                }
                catch { img = null; }
            }
            _imgCache[userId] = img;
            return img;
        }

        private static string Inicjaly(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return (parts[0][0].ToString() + parts[1][0]).ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private static Color KolorZId(string id)
        {
            int hash = id?.GetHashCode() ?? 0;
            Color[] colors =
            {
                Color.FromRgb(46,125,50),  Color.FromRgb(25,118,210), Color.FromRgb(156,39,176),
                Color.FromRgb(230,81,0),   Color.FromRgb(0,137,123),  Color.FromRgb(194,24,91),
                Color.FromRgb(69,90,100),  Color.FromRgb(121,85,72)
            };
            return colors[Math.Abs(hash) % colors.Length];
        }
    }
}
