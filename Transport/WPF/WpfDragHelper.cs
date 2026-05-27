// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/WpfDragHelper.cs — pomocnik drag&drop dla DataGrid (sandbox WPF).
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace Kalendarz1.Transport.WPF
{
    /// <summary>Ghost (chip) podążający za kursorem podczas drag&drop. Dodawany na AdornerLayer okna.</summary>
    internal sealed class DragGhostAdorner : Adorner
    {
        private readonly Border _child;
        private Point _pos;

        public DragGhostAdorner(UIElement adorned, string text) : base(adorned)
        {
            IsHitTestVisible = false;
            _child = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x00, 0x83, 0x8F)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                Opacity = 0.93,
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12
                }
            };
        }

        public void SetPosition(Point p) { _pos = new Point(p.X + 14, p.Y + 10); InvalidateArrange(); }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _child;
        protected override Size MeasureOverride(Size constraint) { _child.Measure(constraint); return _child.DesiredSize; }
        protected override Size ArrangeOverride(Size finalSize) { _child.Arrange(new Rect(_pos, _child.DesiredSize)); return finalSize; }
    }

    internal static class WpfDragHelper
    {
        /// <summary>Wspólny format DataObject dla przeciąganych wolnych zamówień (List&lt;WolneZamowienieWpf&gt;).</summary>
        public const string FmtWolne = "ZPSP_wolne";

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
