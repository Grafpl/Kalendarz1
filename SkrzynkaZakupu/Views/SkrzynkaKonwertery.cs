using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Kalendarz1.SkrzynkaZakupu.Views
{
    /// <summary>Hex string (#RRGGBB) -> SolidColorBrush (z cache).</summary>
    public class HexToBrushConverter : IValueConverter
    {
        private static readonly System.Collections.Generic.Dictionary<string, Brush> _cache = new();
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            var hex = value as string;
            if (string.IsNullOrWhiteSpace(hex)) return Brushes.Gray;
            if (_cache.TryGetValue(hex, out var b)) return b;
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                brush.Freeze();
                _cache[hex] = brush;
                return brush;
            }
            catch { return Brushes.Gray; }
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>int 0 -> Collapsed, inaczej Visible.</summary>
    public class ZeroToHiddenConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => (value is int i && i > 0) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>bool true -> Visible, false -> Collapsed.</summary>
    public class BoolToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>IsReadLocal: true -> Normal, false (nieprzeczytane) -> Bold.</summary>
    public class ReadToWeightConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => (value is bool b && b) ? FontWeights.Normal : FontWeights.SemiBold;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>IsReadLocal: false (nieprzeczytane) -> Visible, true -> Collapsed.</summary>
    public class UnreadToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>IsFlagged: true -> złoty (★), false -> szary (☆).</summary>
    public class FlaggedToBrushConverter : IValueConverter
    {
        private static readonly System.Windows.Media.Brush Gold =
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B));
        private static readonly System.Windows.Media.Brush Gray =
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCB, 0xD5, 0xE1));
        public object Convert(object value, Type t, object p, CultureInfo c)
            => (value is bool b && b) ? Gold : Gray;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
}
