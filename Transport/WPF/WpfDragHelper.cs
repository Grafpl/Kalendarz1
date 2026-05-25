// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/WpfDragHelper.cs — pomocnik drag&drop dla DataGrid (sandbox WPF).
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Kalendarz1.Transport.WPF
{
    internal static class WpfDragHelper
    {
        /// <summary>Grupuje kolekcję po właściwości (nagłówki sekcji) + sortowanie. Dla WolneGrid → grupy per klient.</summary>
        public static void GrupujKolekcje(object source, string groupProp, params string[] sortProps)
        {
            var view = CollectionViewSource.GetDefaultView(source);
            if (view == null) return;
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(new PropertyGroupDescription(groupProp));
            view.SortDescriptions.Clear();
            foreach (var sp in sortProps)
                view.SortDescriptions.Add(new SortDescription(sp, ListSortDirection.Ascending));
        }

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

        /// <summary>DataGridRow (kontener) pod punktem — do podświetlania celu przy drag&drop.</summary>
        public static DataGridRow? GetRowAtPoint(DataGrid grid, Point p)
        {
            if (grid.InputHitTest(p) is not DependencyObject el) return null;
            return FindAncestor<DataGridRow>(el);
        }

        public static bool ExceededThreshold(Point a, Point b) =>
            Math.Abs(a.X - b.X) >= SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(a.Y - b.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }
}
