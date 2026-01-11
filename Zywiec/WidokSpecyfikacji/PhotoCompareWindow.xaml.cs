using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Kalendarz.Zywiec.WidokSpecyfikacji
{
    /// <summary>
    /// Okno do porównywania dwóch zdjęć z ważenia (TARA i BRUTTO) obok siebie
    /// </summary>
    public partial class PhotoCompareWindow : Window
    {
        private string _taraPath;
        private string _bruttoPath;
        private double _currentZoom = 1.0;
        private const double ZoomStep = 0.25;
        private const double MinZoom = 0.1;
        private const double MaxZoom = 10.0;

        // Flaga do blokowania rekurencyjnej synchronizacji
        private bool _isSyncing = false;

        public PhotoCompareWindow()
        {
            InitializeComponent();
        }

        public PhotoCompareWindow(string taraPath, string bruttoPath, string title = null) : this()
        {
            _taraPath = taraPath;
            _bruttoPath = bruttoPath;

            if (!string.IsNullOrEmpty(title))
            {
                Title = $"Porownanie zdjec - {title}";
            }

            LoadImages();
        }

        private void LoadImages()
        {
            LoadTaraImage();
            LoadBruttoImage();

            // Dopasuj do okna na start
            Loaded += (s, e) => FitToWindow();
        }

        private void LoadTaraImage()
        {
            if (string.IsNullOrEmpty(_taraPath))
            {
                ShowTaraError("Brak sciezki do zdjecia TARA");
                return;
            }

            if (!File.Exists(_taraPath))
            {
                string errorMsg = GetFileNotFoundMessage(_taraPath);
                ShowTaraError(errorMsg);
                return;
            }

            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_taraPath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                imgTara.Source = bitmap;
                overlayTara.Visibility = Visibility.Collapsed;

                txtTaraFileName.Text = $"TARA: {Path.GetFileName(_taraPath)}";
                txtTaraPath.Text = _taraPath;
            }
            catch (Exception ex)
            {
                ShowTaraError($"Blad ladowania: {ex.Message}");
            }
        }

        private void LoadBruttoImage()
        {
            if (string.IsNullOrEmpty(_bruttoPath))
            {
                ShowBruttoError("Brak sciezki do zdjecia BRUTTO");
                return;
            }

            if (!File.Exists(_bruttoPath))
            {
                string errorMsg = GetFileNotFoundMessage(_bruttoPath);
                ShowBruttoError(errorMsg);
                return;
            }

            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_bruttoPath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                imgBrutto.Source = bitmap;
                overlayBrutto.Visibility = Visibility.Collapsed;

                txtBruttoFileName.Text = $"BRUTTO: {Path.GetFileName(_bruttoPath)}";
                txtBruttoPath.Text = _bruttoPath;
            }
            catch (Exception ex)
            {
                ShowBruttoError($"Blad ladowania: {ex.Message}");
            }
        }

        private string GetFileNotFoundMessage(string path)
        {
            if (path.StartsWith(@"\\"))
            {
                int thirdSlash = path.IndexOf('\\', 2);
                string serverPath = thirdSlash > 0 ? path.Substring(0, thirdSlash) : path;
                return $"Nie mozna polaczyc z:\n{serverPath}";
            }
            return $"Plik nie istnieje:\n{path}";
        }

        private void ShowTaraError(string message)
        {
            overlayTara.Visibility = Visibility.Visible;
            txtTaraError.Text = message;
            txtTaraFileName.Text = "TARA: brak";
            txtTaraPath.Text = _taraPath ?? "";
        }

        private void ShowBruttoError(string message)
        {
            overlayBrutto.Visibility = Visibility.Visible;
            txtBruttoError.Text = message;
            txtBruttoFileName.Text = "BRUTTO: brak";
            txtBruttoPath.Text = _bruttoPath ?? "";
        }

        private void UpdateZoomLevel()
        {
            scaleTransformLeft.ScaleX = _currentZoom;
            scaleTransformLeft.ScaleY = _currentZoom;
            scaleTransformRight.ScaleX = _currentZoom;
            scaleTransformRight.ScaleY = _currentZoom;
            txtZoomLevel.Text = $"{_currentZoom * 100:F0}%";
        }

        private void SetZoom(double zoom)
        {
            _currentZoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
            UpdateZoomLevel();
        }

        private void FitToWindow()
        {
            double maxImageWidth = 0;
            double maxImageHeight = 0;

            if (imgTara.Source is BitmapImage taraBitmap)
            {
                maxImageWidth = Math.Max(maxImageWidth, taraBitmap.PixelWidth);
                maxImageHeight = Math.Max(maxImageHeight, taraBitmap.PixelHeight);
            }

            if (imgBrutto.Source is BitmapImage bruttoBitmap)
            {
                maxImageWidth = Math.Max(maxImageWidth, bruttoBitmap.PixelWidth);
                maxImageHeight = Math.Max(maxImageHeight, bruttoBitmap.PixelHeight);
            }

            if (maxImageWidth <= 0 || maxImageHeight <= 0) return;

            double viewportWidth = scrollViewerLeft.ViewportWidth;
            double viewportHeight = scrollViewerLeft.ViewportHeight;

            if (viewportWidth <= 0 || viewportHeight <= 0) return;

            double scaleX = viewportWidth / maxImageWidth;
            double scaleY = viewportHeight / maxImageHeight;
            double scale = Math.Min(scaleX, scaleY) * 0.95;

            SetZoom(scale);
        }

        #region Event Handlers - Zoom

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(_currentZoom + ZoomStep);
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(_currentZoom - ZoomStep);
        }

        private void BtnFitToWindow_Click(object sender, RoutedEventArgs e)
        {
            FitToWindow();
        }

        private void ScrollViewerLeft_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomDelta = e.Delta > 0 ? ZoomStep : -ZoomStep;
            SetZoom(_currentZoom + zoomDelta);
            e.Handled = true;
        }

        private void ScrollViewerRight_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomDelta = e.Delta > 0 ? ZoomStep : -ZoomStep;
            SetZoom(_currentZoom + zoomDelta);
            e.Handled = true;
        }

        #endregion

        #region Event Handlers - Scroll Sync

        private void ScrollViewerLeft_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncing || chkSyncZoom.IsChecked != true) return;

            _isSyncing = true;
            try
            {
                scrollViewerRight.ScrollToHorizontalOffset(scrollViewerLeft.HorizontalOffset);
                scrollViewerRight.ScrollToVerticalOffset(scrollViewerLeft.VerticalOffset);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private void ScrollViewerRight_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncing || chkSyncZoom.IsChecked != true) return;

            _isSyncing = true;
            try
            {
                scrollViewerLeft.ScrollToHorizontalOffset(scrollViewerRight.HorizontalOffset);
                scrollViewerLeft.ScrollToVerticalOffset(scrollViewerRight.VerticalOffset);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        #endregion

        #region Event Handlers - Buttons

        private void BtnOpenTaraFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderWithFile(_taraPath);
        }

        private void BtnOpenBruttoFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderWithFile(_bruttoPath);
        }

        private void OpenFolderWithFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                MessageBox.Show("Brak sciezki do pliku.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!File.Exists(filePath))
            {
                // Spróbuj otworzyć sam folder
                string dir = Path.GetDirectoryName(filePath);
                if (Directory.Exists(dir))
                {
                    try
                    {
                        Process.Start("explorer.exe", $"\"{dir}\"");
                    }
                    catch { }
                }
                else
                {
                    MessageBox.Show($"Plik i folder nie istnieja:\n{filePath}", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return;
            }

            try
            {
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
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
                    FitToWindow();
                    break;
            }
        }

        #endregion

        /// <summary>
        /// Statyczna metoda pomocnicza do wyświetlenia porównania zdjęć
        /// </summary>
        public static void ShowComparison(string taraPath, string bruttoPath, string title = null)
        {
            // Sprawdź czy jest przynajmniej jedno zdjęcie
            bool hasTara = !string.IsNullOrEmpty(taraPath) && File.Exists(taraPath);
            bool hasBrutto = !string.IsNullOrEmpty(bruttoPath) && File.Exists(bruttoPath);

            if (!hasTara && !hasBrutto)
            {
                string msg = "Brak zdjec dla tego wazenia.";

                if (!string.IsNullOrEmpty(taraPath) || !string.IsNullOrEmpty(bruttoPath))
                {
                    msg = "Pliki nie istnieja:";
                    if (!string.IsNullOrEmpty(taraPath))
                        msg += $"\nTARA: {taraPath}";
                    if (!string.IsNullOrEmpty(bruttoPath))
                        msg += $"\nBRUTTO: {bruttoPath}";
                }

                MessageBox.Show(msg, "Brak zdjec", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var viewer = new PhotoCompareWindow(taraPath, bruttoPath, title);
            viewer.Show();
        }
    }
}
