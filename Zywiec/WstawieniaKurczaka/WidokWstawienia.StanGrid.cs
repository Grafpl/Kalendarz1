using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Kalendarz1
{
    // Zachowanie stanu DataGridów (selekcja + scroll) przy odświeżaniu danych.
    // textBoxFilter już jest zachowywany przez ApplyFilters() — tu obsługujemy
    // tylko brakujące dwa elementy: który wiersz był zaznaczony i pozycję scroll.
    //
    // Wzorzec użycia w Load* metodach:
    //   var stan = ZachowajStanGrid(dataGridX);
    //   ... ustaw ItemsSource + ApplyFilters ...
    //   PrzywrocStanGrid(dataGridX, stan);
    public partial class WidokWstawienia
    {
        internal sealed class StanGrid
        {
            public string? LpZaznaczony { get; set; }
            public double ScrollOffset { get; set; }
        }

        internal StanGrid ZachowajStanGrid(DataGrid grid)
        {
            var stan = new StanGrid();
            try
            {
                if (grid?.SelectedItem is DataRowView drv && drv.Row.Table.Columns.Contains("LP"))
                {
                    stan.LpZaznaczony = drv["LP"]?.ToString();
                }
                var sv = ZnajdzScrollViewer(grid);
                if (sv != null) stan.ScrollOffset = sv.VerticalOffset;
            }
            catch { }
            return stan;
        }

        // Przywraca selekcję + scroll po ApplyFilters / ItemsSource-set.
        // Używa Dispatcher.BeginInvoke z priorytetem ContextIdle żeby DataGrid
        // zdążył wyrenderować nowy widok zanim spróbujemy ustawić scroll.
        internal void PrzywrocStanGrid(DataGrid grid, StanGrid? stan)
        {
            if (grid == null || stan == null) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // Selekcja po LP
                    if (!string.IsNullOrEmpty(stan.LpZaznaczony) && grid.ItemsSource is DataView dv)
                    {
                        foreach (DataRowView row in dv)
                        {
                            if (row["LP"]?.ToString() == stan.LpZaznaczony)
                            {
                                grid.SelectedItem = row;
                                try { grid.ScrollIntoView(row); } catch { }
                                break;
                            }
                        }
                    }
                    // Scroll — po SelectedItem (żeby ScrollIntoView nie nadpisało)
                    var sv = ZnajdzScrollViewer(grid);
                    if (sv != null && stan.ScrollOffset > 0)
                    {
                        sv.ScrollToVerticalOffset(stan.ScrollOffset);
                    }
                }
                catch { }
            }), DispatcherPriority.ContextIdle);
        }

        private static ScrollViewer? ZnajdzScrollViewer(DependencyObject? root)
        {
            if (root == null) return null;
            if (root is ScrollViewer sv) return sv;
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var found = ZnajdzScrollViewer(VisualTreeHelper.GetChild(root, i));
                if (found != null) return found;
            }
            return null;
        }
    }
}
