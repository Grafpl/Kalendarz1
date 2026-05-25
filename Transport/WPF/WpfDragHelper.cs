// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/WpfDragHelper.cs — pomocnik drag&drop dla DataGrid (sandbox WPF).
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.Transport.WPF
{
    internal static class WpfDragHelper
    {
        public static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>Element (DataItem) wiersza DataGrid pod podanym punktem (względem grida). Null jeśli brak.</summary>
        public static object? GetItemAtPoint(DataGrid grid, Point p)
        {
            if (grid.InputHitTest(p) is not DependencyObject el) return null;
            var row = FindAncestor<DataGridRow>(el);
            return row?.Item;
        }

        public static bool ExceededThreshold(Point a, Point b) =>
            Math.Abs(a.X - b.X) >= SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(a.Y - b.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }
}
