using Kalendarz1.HDI.Models;
using Kalendarz1.HDI.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kalendarz1.HDI
{
    /// <summary>
    /// Inline podgląd HDI — renderuje strony PDF jako PNG.
    /// Sidebar z thumbnails + main preview z zoom + keyboard shortcuts.
    /// Maximized, dark background (jak Adobe). Ctrl+P drukuj, Ctrl+S zapisz, Esc zamknij, +/- zoom, W szerokość, 0 reset.
    /// </summary>
    public partial class HdiPreviewWindow : Window
    {
        public class PageVm
        {
            public BitmapImage? Image { get; set; }
            public int PageIndex { get; set; }
            public string Label { get; set; } = "";
        }

        private readonly HdiDokument _model;
        private readonly byte[] _pdfBytes;
        private readonly List<byte[]> _pageBytes;
        public ObservableCollection<PageVm> Pages { get; } = new();
        public ObservableCollection<PageVm> Thumbs { get; } = new();
        private double _zoom = 1.0;
        private bool _suppressFitWidth = false;

        public HdiPreviewWindow(HdiDokument model) : this(model, null, null) { }

        // Konstruktor z gotowym cache PDF + obrazów — instant otwarcie (preview było wygenerowane w tle)
        public HdiPreviewWindow(HdiDokument model, byte[]? cachedPdf, List<byte[]>? cachedImages)
        {
            InitializeComponent();
            try { Kalendarz1.WindowIconHelper.SetIcon(this); } catch { }

            _model = model;
            if (cachedPdf != null && cachedImages != null && cachedImages.Count > 0)
            {
                _pdfBytes = cachedPdf;
                _pageBytes = cachedImages;
            }
            else
            {
                var gen = new HdiPdfGenerator();
                _pdfBytes = gen.Generate(model);
                _pageBytes = gen.GenerateImages(model);
            }

            PagesList.ItemsSource = Pages;
            ThumbsList.ItemsSource = Thumbs;

            LblTitle.Text = $"HDI {_model.NumerPelny} — Podgląd";
            LblSubtitle.Text = string.IsNullOrWhiteSpace(_model.KlientNazwa)
                ? $"Wystawiony: {_model.DataWystawienia:dd.MM.yyyy}"
                : $"{_model.KlientNazwa}  ·  Wystawiony: {_model.DataWystawienia:dd.MM.yyyy}";
            LblPages.Text = _pageBytes.Count == 1 ? "1 strona" : $"{_pageBytes.Count} stron";
            LblStatus.Text = $"✓ PDF: {_pdfBytes.Length / 1024} KB · {_pageBytes.Count} stron";

            // Ukryj sidebar gdy jest tylko 1 strona — nie ma sensu pokazywać thumbnails
            if (_pageBytes.Count <= 1) ColThumbs.Width = new GridLength(0);

            BuildThumbnails();
            RebuildPages();

            // Po pełnym renderze (ContentRendered — okno ma już realne wymiary) → fit-to-width.
            // Loaded fire'uje za wcześnie (ActualWidth jeszcze 0 przy Maximized).
            ContentRendered += (s, e) => Dispatcher.BeginInvoke(new Action(FitToWidth),
                System.Windows.Threading.DispatcherPriority.Background);
            // Ponownie przy zmianie rozmiaru okna (resize/maximize)
            SizeChanged += (s, e) => { /* nie auto-fit przy każdym resize — tylko jeśli user nie zoomował ręcznie */ };
        }

        // Główny preview — strony z aktualnym zoomem
        private void RebuildPages()
        {
            Pages.Clear();
            for (int i = 0; i < _pageBytes.Count; i++)
            {
                var bi = LoadBitmap(_pageBytes[i], (int)(794 * _zoom));
                Pages.Add(new PageVm { Image = bi, PageIndex = i, Label = $"Strona {i + 1}" });
            }
            LblZoom.Text = $"{(int)(_zoom * 100)}%";
        }

        // Thumbnails — stała wysokość 160px, generowane raz
        private void BuildThumbnails()
        {
            Thumbs.Clear();
            for (int i = 0; i < _pageBytes.Count; i++)
            {
                var bi = LoadBitmap(_pageBytes[i], 120);  // mały thumbnail
                Thumbs.Add(new PageVm { Image = bi, PageIndex = i, Label = $"Strona {i + 1}" });
            }
        }

        private static BitmapImage LoadBitmap(byte[] bytes, int decodePixelWidth)
        {
            using var ms = new MemoryStream(bytes);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.DecodePixelWidth = decodePixelWidth;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }

        private void Thumb_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is int idx)
            {
                ScrollToPage(idx);
            }
        }

        private void ScrollToPage(int pageIndex)
        {
            // Znajdź Border w PagesList z odpowiednim Tag i przewiń ScrollViewer
            if (pageIndex < 0 || pageIndex >= Pages.Count) return;
            // Wymuś layout (Items aren't materialized until visible)
            PagesList.UpdateLayout();
            var container = PagesList.ItemContainerGenerator.ContainerFromIndex(pageIndex) as FrameworkElement;
            if (container != null)
            {
                container.BringIntoView();
                LblStatus.Text = $"📄 Strona {pageIndex + 1} / {Pages.Count}";
            }
        }

        private void PageBorder_Loaded(object sender, RoutedEventArgs e)
        {
            // możliwość detekcji aktywnej strony (skip w MVP)
        }

        // ── ZOOM ──────────────────────────────────────────────────────────
        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _zoom = Math.Min(_zoom + 0.15, 3.0);
            RebuildPages();
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _zoom = Math.Max(_zoom - 0.15, 0.3);
            RebuildPages();
        }

        private void BtnZoomFit_Click(object sender, RoutedEventArgs e)
        {
            _zoom = 1.0;
            RebuildPages();
        }

        private void BtnZoomFitWidth_Click(object sender, RoutedEventArgs e) => FitToWidth();

        // FitToWidth — domyślne dopasowanie: cała strona A4 widoczna (min z fit-width i fit-height).
        // A4 przy 96 DPI = 794 × 1123 px. Bierzemy mniejszy zoom żeby zmieścić w całości.
        private void FitToWidth()
        {
            try
            {
                double availW = ScrollPreview.ActualWidth - 50;
                double availH = ScrollPreview.ActualHeight - 50;
                if (availW <= 100 || availH <= 100) return;
                double zoomW = availW / 794.0;
                double zoomH = availH / 1123.0;
                _zoom = Math.Min(zoomW, zoomH);   // cała strona widoczna
                _zoom = Math.Clamp(_zoom, 0.3, 3.0);
                RebuildPages();
                LblStatus.Text = $"📏 Dopasowano stronę ({(int)(_zoom * 100)}%)";
            }
            catch { }
        }

        // ── KEYBOARD ──────────────────────────────────────────────────────
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled) return;
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (e.Key == Key.P && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { BtnPrint_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { BtnSaveAs_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.OemPlus || e.Key == Key.Add) { BtnZoomIn_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) { BtnZoomOut_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.D0 || e.Key == Key.NumPad0) { BtnZoomFit_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.W) { BtnZoomFitWidth_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.Home) { ScrollToPage(0); e.Handled = true; }
            else if (e.Key == Key.End) { ScrollToPage(Pages.Count - 1); e.Handled = true; }
        }

        // ── ACTIONS ───────────────────────────────────────────────────────
        // Drukowanie przez WPF PrintDialog + FixedDocument z PNG stron PDF.
        // DOMYŚLNIE: 1 ORYGINAŁ + 1 KOPIA (z etykietą w rogu).
        // Działa NIEZALEŻNIE od zainstalowanego PDF readera.
        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new System.Windows.Controls.PrintDialog();
                if (dialog.ShowDialog() != true) { LblStatus.Text = "Drukowanie anulowane"; return; }

                var doc = BuildPrintDocument(_pageBytes, dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
                dialog.PrintDocument(doc.DocumentPaginator, $"HDI {_model.NumerPelny}");
                LblStatus.Text = $"🖨️ Wysłano: oryginał + kopia → '{dialog.PrintQueue?.Name ?? "domyślna"}' ({_pageBytes.Count * 2} stron)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Błąd drukowania:\n\n" + ex.Message + "\n\nMożesz zapisać PDF i wydrukować ręcznie.",
                    "Drukowanie", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Buduje FixedDocument: najpierw wszystkie strony jako ORYGINAŁ, potem jako KOPIA.
        // Etykieta w prawym-górnym rogu każdej strony.
        public static System.Windows.Documents.FixedDocument BuildPrintDocument(List<byte[]> pages, double w, double h)
        {
            var doc = new System.Windows.Documents.FixedDocument();
            doc.DocumentPaginator.PageSize = new System.Windows.Size(w, h);
            string[] kopie = { "ORYGINAŁ", "KOPIA" };
            foreach (var label in kopie)
            {
                foreach (var bytes in pages)
                {
                    using var ms = new MemoryStream(bytes);
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.DecodePixelWidth = 1600;
                    bi.EndInit();
                    bi.Freeze();

                    var img = new System.Windows.Controls.Image
                    {
                        Source = bi,
                        Stretch = System.Windows.Media.Stretch.Uniform,
                        Width = w, Height = h
                    };
                    var fp = new System.Windows.Documents.FixedPage { Width = w, Height = h };
                    System.Windows.Documents.FixedPage.SetLeft(img, 0);
                    System.Windows.Documents.FixedPage.SetTop(img, 0);
                    fp.Children.Add(img);

                    // Etykieta ORYGINAŁ/KOPIA w prawym-górnym rogu
                    var lbl = new System.Windows.Controls.TextBlock
                    {
                        Text = label,
                        FontSize = 11,
                        FontWeight = System.Windows.FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.Gray
                    };
                    System.Windows.Documents.FixedPage.SetRight(lbl, 24);
                    System.Windows.Documents.FixedPage.SetTop(lbl, 14);
                    fp.Children.Add(lbl);

                    var pc = new System.Windows.Documents.PageContent();
                    ((System.Windows.Markup.IAddChild)pc).AddChild(fp);
                    doc.Pages.Add(pc);
                }
            }
            return doc;
        }

        private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            string safeKlient = SanitizeFileName(_model.KlientNazwa);
            var sfd = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = string.IsNullOrEmpty(safeKlient)
                    ? $"HDI_{_model.NumerPelny.Replace('/', '_')}.pdf"
                    : $"HDI_{_model.NumerPelny.Replace('/', '_')}_{safeKlient}.pdf",
                DefaultExt = "pdf"
            };
            if (sfd.ShowDialog(this) == true)
            {
                try
                {
                    File.WriteAllBytes(sfd.FileName, _pdfBytes);
                    LblStatus.Text = $"✓ Zapisano: {sfd.FileName}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Błąd zapisu: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private static string SanitizeFileName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var invalid = Path.GetInvalidFileNameChars();
            var clean = new string(s.Where(c => !invalid.Contains(c) && c != ',').ToArray()).Replace(' ', '_');
            return clean.Length > 40 ? clean.Substring(0, 40) : clean;
        }

        private void BtnOpenExternal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string tmp = Path.Combine(Path.GetTempPath(), $"HDI_{_model.NumerPelny.Replace('/', '_')}_preview.pdf");
                File.WriteAllBytes(tmp, _pdfBytes);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tmp,
                    UseShellExecute = true
                });
                LblStatus.Text = $"↗ Otwarto w domyślnej aplikacji";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd otwierania: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
