using System;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.WPF.Helpers
{
    /// <summary>
    /// Helper do zarządzania responsywnym layoutem
    /// </summary>
    public static class ResponsiveLayoutHelper
    {
        #region Attached Properties

        /// <summary>
        /// Minimalna szerokość dla kolumny (auto-collapse poniżej)
        /// </summary>
        public static readonly DependencyProperty MinWidthForVisibilityProperty =
            DependencyProperty.RegisterAttached(
                "MinWidthForVisibility",
                typeof(double),
                typeof(ResponsiveLayoutHelper),
                new PropertyMetadata(0.0, OnMinWidthForVisibilityChanged));

        public static double GetMinWidthForVisibility(DependencyObject obj)
            => (double)obj.GetValue(MinWidthForVisibilityProperty);

        public static void SetMinWidthForVisibility(DependencyObject obj, double value)
            => obj.SetValue(MinWidthForVisibilityProperty, value);

        private static void OnMinWidthForVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                var window = Window.GetWindow(element);
                if (window != null)
                {
                    window.SizeChanged -= Window_SizeChanged;
                    window.SizeChanged += Window_SizeChanged;
                }
            }
        }

        private static void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Aktualizacja widoczności elementów responsywnych
            if (sender is Window window)
            {
                UpdateResponsiveElements(window, e.NewSize.Width);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Konfiguruje responsywny layout dla okna
        /// </summary>
        public static void ConfigureResponsiveWindow(Window window)
        {
            window.SizeChanged += (s, e) =>
            {
                var width = e.NewSize.Width;

                // Określ breakpoint
                var breakpoint = GetBreakpoint(width);

                // Zastosuj odpowiedni layout
                ApplyBreakpointLayout(window, breakpoint, width);
            };

            // Zastosuj początkowy layout
            window.Loaded += (s, e) =>
            {
                var breakpoint = GetBreakpoint(window.ActualWidth);
                ApplyBreakpointLayout(window, breakpoint, window.ActualWidth);
            };
        }

        /// <summary>
        /// Konfiguruje DataGrid z responsywnymi kolumnami
        /// </summary>
        public static void ConfigureResponsiveDataGrid(DataGrid dataGrid, ResponsiveColumnConfig[] columns)
        {
            var window = Window.GetWindow(dataGrid);
            if (window == null) return;

            window.SizeChanged += (s, e) =>
            {
                var breakpoint = GetBreakpoint(e.NewSize.Width);

                foreach (var config in columns)
                {
                    var column = FindColumnByHeader(dataGrid, config.Header);
                    if (column != null)
                    {
                        column.Visibility = config.VisibleAt(breakpoint)
                            ? Visibility.Visible
                            : Visibility.Collapsed;

                        if (config.WidthAt != null)
                        {
                            var newWidth = config.WidthAt(breakpoint);
                            if (newWidth.HasValue)
                            {
                                column.Width = new DataGridLength(newWidth.Value);
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Zapisuje preferencje rozmiaru okna
        /// </summary>
        public static void SaveWindowSize(Window window, string windowName)
        {
            try
            {
                var settings = new WindowSettings
                {
                    Width = window.Width,
                    Height = window.Height,
                    Left = window.Left,
                    Top = window.Top,
                    IsMaximized = window.WindowState == WindowState.Maximized
                };

                var path = GetSettingsPath(windowName);
                var json = System.Text.Json.JsonSerializer.Serialize(settings);
                System.IO.File.WriteAllText(path, json);
            }
            catch { /* Ignoruj błędy zapisu */ }
        }

        /// <summary>
        /// Wczytuje preferencje rozmiaru okna
        /// </summary>
        public static void LoadWindowSize(Window window, string windowName)
        {
            try
            {
                var path = GetSettingsPath(windowName);
                if (!System.IO.File.Exists(path)) return;

                var json = System.IO.File.ReadAllText(path);
                var settings = System.Text.Json.JsonSerializer.Deserialize<WindowSettings>(json);

                if (settings != null)
                {
                    window.Width = settings.Width;
                    window.Height = settings.Height;
                    window.Left = settings.Left;
                    window.Top = settings.Top;

                    if (settings.IsMaximized)
                    {
                        window.WindowState = WindowState.Maximized;
                    }
                }
            }
            catch { /* Ignoruj błędy odczytu */ }
        }

        #endregion

        #region Private Methods

        private static Breakpoint GetBreakpoint(double width)
        {
            if (width < 800) return Breakpoint.Small;
            if (width < 1200) return Breakpoint.Medium;
            if (width < 1600) return Breakpoint.Large;
            return Breakpoint.ExtraLarge;
        }

        private static void ApplyBreakpointLayout(Window window, Breakpoint breakpoint, double width)
        {
            // Znajdź elementy z tagiem "responsive" i dostosuj
            foreach (var element in FindVisualChildren<FrameworkElement>(window))
            {
                var minWidth = GetMinWidthForVisibility(element);
                if (minWidth > 0)
                {
                    element.Visibility = width >= minWidth
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
        }

        private static DataGridColumn? FindColumnByHeader(DataGrid dataGrid, string header)
        {
            foreach (var column in dataGrid.Columns)
            {
                if (column.Header?.ToString() == header)
                    return column;
            }
            return null;
        }

        private static void UpdateResponsiveElements(Window window, double width)
        {
            foreach (var element in FindVisualChildren<FrameworkElement>(window))
            {
                var minWidth = GetMinWidthForVisibility(element);
                if (minWidth > 0)
                {
                    element.Visibility = width >= minWidth
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent == null) yield break;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                    yield return typedChild;

                foreach (var grandChild in FindVisualChildren<T>(child))
                    yield return grandChild;
            }
        }

        private static string GetSettingsPath(string windowName)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = System.IO.Path.Combine(appData, "Kalendarz1", "WindowSettings");
            System.IO.Directory.CreateDirectory(folder);
            return System.IO.Path.Combine(folder, $"{windowName}.json");
        }

        #endregion
    }

    #region Models

    public enum Breakpoint
    {
        Small,      // < 800px
        Medium,     // 800-1199px
        Large,      // 1200-1599px
        ExtraLarge  // >= 1600px
    }

    public class ResponsiveColumnConfig
    {
        public string Header { get; set; } = "";
        public Func<Breakpoint, bool> VisibleAt { get; set; } = _ => true;
        public Func<Breakpoint, double?>? WidthAt { get; set; }

        public static ResponsiveColumnConfig Create(string header,
            Breakpoint minBreakpoint = Breakpoint.Small,
            double? smallWidth = null,
            double? mediumWidth = null,
            double? largeWidth = null)
        {
            return new ResponsiveColumnConfig
            {
                Header = header,
                VisibleAt = bp => bp >= minBreakpoint,
                WidthAt = bp => bp switch
                {
                    Breakpoint.Small => smallWidth,
                    Breakpoint.Medium => mediumWidth ?? smallWidth,
                    Breakpoint.Large => largeWidth ?? mediumWidth ?? smallWidth,
                    Breakpoint.ExtraLarge => largeWidth ?? mediumWidth ?? smallWidth,
                    _ => null
                }
            };
        }
    }

    public class WindowSettings
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public bool IsMaximized { get; set; }
    }

    #endregion
}
