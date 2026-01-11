using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Kalendarz.Zywiec.WidokSpecyfikacji
{
    /// <summary>
    /// Okno do podglądu zdjęć z ważenia z funkcją zoom i pan
    /// </summary>
    public partial class PhotoViewerWindow : Window
    {
        private string _photoPath;
        private double _currentZoom = 1.0;
        private const double ZoomStep = 0.25;
        private const double MinZoom = 0.1;
        private const double MaxZoom = 10.0;

        // Zmienne do obsługi przeciągania (pan)
        private Point _lastMousePosition;
        private bool _isDragging = false;

        public PhotoViewerWindow()
        {
            InitializeComponent();
        }

        public PhotoViewerWindow(string photoPath, string title = null) : this()
        {
            _photoPath = photoPath;
            if (!string.IsNullOrEmpty(title))
            {
                Title = title;
            }
            LoadImage();
        }

        private void LoadImage()
        {
            if (string.IsNullOrEmpty(_photoPath))
            {
                ShowError("Nie podano sciezki do zdjecia.");
                return;
            }

            try
            {
                if (!File.Exists(_photoPath))
                {
                    ShowError($"Plik nie istnieje:\n{_photoPath}");
                    return;
                }

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_photoPath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze(); // Dla lepszej wydajności

                imgPhoto.Source = bitmap;

                // Ustaw informacje o pliku
                txtFileName.Text = Path.GetFileName(_photoPath);
                txtFilePath.Text = _photoPath;

                // Informacje o obrazie
                FileInfo fi = new FileInfo(_photoPath);
                string sizeStr = fi.Length > 1024 * 1024
                    ? $"{fi.Length / (1024.0 * 1024.0):F2} MB"
                    : $"{fi.Length / 1024.0:F1} KB";
                txtImageInfo.Text = $"Wymiary: {bitmap.PixelWidth} x {bitmap.PixelHeight} px | Rozmiar: {sizeStr}";

                // Dopasuj do okna na start
                Loaded += (s, e) => FitToWindow();
            }
            catch (Exception ex)
            {
                ShowError($"Blad ladowania zdjecia:\n{ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            txtFileName.Text = "Blad";
            txtFilePath.Text = message;
            txtImageInfo.Text = "";
        }

        private void UpdateZoomLevel()
        {
            scaleTransform.ScaleX = _currentZoom;
            scaleTransform.ScaleY = _currentZoom;
            txtZoomLevel.Text = $"{_currentZoom * 100:F0}%";
        }

        private void SetZoom(double zoom)
        {
            _currentZoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
            UpdateZoomLevel();
        }

        private void FitToWindow()
        {
            if (imgPhoto.Source == null) return;

            var bitmap = imgPhoto.Source as BitmapImage;
            if (bitmap == null) return;

            double viewportWidth = scrollViewer.ViewportWidth;
            double viewportHeight = scrollViewer.ViewportHeight;

            if (viewportWidth <= 0 || viewportHeight <= 0) return;

            double scaleX = viewportWidth / bitmap.PixelWidth;
            double scaleY = viewportHeight / bitmap.PixelHeight;
            double scale = Math.Min(scaleX, scaleY) * 0.95; // 95% dla marginesu

            SetZoom(scale);
        }

        #region Event Handlers - Zoom Buttons

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(_currentZoom + ZoomStep);
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(_currentZoom - ZoomStep);
        }

        private void BtnZoom100_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(1.0);
        }

        private void BtnFitToWindow_Click(object sender, RoutedEventArgs e)
        {
            FitToWindow();
        }

        private void BtnRotateLeft_Click(object sender, RoutedEventArgs e)
        {
            rotateTransform.Angle -= 90;
        }

        private void BtnRotateRight_Click(object sender, RoutedEventArgs e)
        {
            rotateTransform.Angle += 90;
        }

        #endregion

        #region Event Handlers - Mouse

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Ctrl + scroll = zoom
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                double zoomDelta = e.Delta > 0 ? ZoomStep : -ZoomStep;
                SetZoom(_currentZoom + zoomDelta);
                e.Handled = true;
            }
            else
            {
                // Bez Ctrl - standardowy scroll przez zoom
                double zoomDelta = e.Delta > 0 ? ZoomStep : -ZoomStep;
                SetZoom(_currentZoom + zoomDelta);
                e.Handled = true;
            }
        }

        private void ScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _lastMousePosition = e.GetPosition(scrollViewer);
            scrollViewer.Cursor = Cursors.ScrollAll;
            e.Handled = false; // Pozwól na dalsze przetwarzanie
        }

        private void ScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            scrollViewer.Cursor = Cursors.Hand;
        }

        private void ScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(scrollViewer);
                double deltaX = _lastMousePosition.X - currentPosition.X;
                double deltaY = _lastMousePosition.Y - currentPosition.Y;

                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + deltaX);
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + deltaY);

                _lastMousePosition = currentPosition;
            }
        }

        #endregion

        #region Event Handlers - Toolbar

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_photoPath) || !File.Exists(_photoPath))
            {
                MessageBox.Show("Plik nie istnieje.", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Otwórz folder z zaznaczonym plikiem
                Process.Start("explorer.exe", $"/select,\"{_photoPath}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie mozna otworzyc folderu:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region Event Handlers - Keyboard

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Close();
                    break;
                case Key.Add:
                case Key.OemPlus:
                    SetZoom(_currentZoom + ZoomStep);
                    break;
                case Key.Subtract:
                case Key.OemMinus:
                    SetZoom(_currentZoom - ZoomStep);
                    break;
                case Key.D0:
                case Key.NumPad0:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        FitToWindow();
                    else
                        SetZoom(1.0);
                    break;
                case Key.Left:
                    rotateTransform.Angle -= 90;
                    break;
                case Key.Right:
                    rotateTransform.Angle += 90;
                    break;
            }
        }

        #endregion

        /// <summary>
        /// Statyczna metoda pomocnicza do wyświetlenia zdjęcia
        /// </summary>
        public static void ShowPhoto(string photoPath, string title = null)
        {
            if (string.IsNullOrEmpty(photoPath))
            {
                MessageBox.Show("Brak zdjecia dla tego wazenia.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!File.Exists(photoPath))
            {
                // Sprawdź czy to problem z połączeniem sieciowym
                if (photoPath.StartsWith(@"\\"))
                {
                    string serverPath = photoPath.Substring(0, photoPath.IndexOf('\\', 2) > 0 ? photoPath.IndexOf('\\', 2) : photoPath.Length);
                    MessageBox.Show($"Nie mozna polaczyc z:\n{serverPath}\n\nSprawdz polaczenie sieciowe.",
                        "Blad polaczenia", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show($"Plik nie istnieje:\n{photoPath}", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return;
            }

            var viewer = new PhotoViewerWindow(photoPath, title);
            viewer.Show();
        }
    }
}
