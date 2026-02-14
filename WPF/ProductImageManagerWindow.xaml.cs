using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace Kalendarz1.WPF
{
    public partial class ProductImageManagerWindow : Window
    {
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private List<ProductImageItem> _allItems = new();
        private List<ProductImageItem> _filteredItems = new();
        private DispatcherTimer? _searchTimer;

        public ProductImageManagerWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchTimer.Tick += (s, args) =>
            {
                _searchTimer.Stop();
                ApplyFilters();
            };

            await LoadDataAsync();
        }

        #region Ładowanie danych

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                loadingOverlay.Visibility = Visibility.Visible;
                _allItems.Clear();

                // 1. Pobierz produkty z Handel (katalogi: 67095=Świeże, 67153=Mrożone)
                var products = new Dictionary<int, ProductImageItem>();

                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT ID, kod, katalog
                                         FROM [HANDEL].[HM].[TW]
                                         WHERE katalog IN (67095, 67153)
                                         ORDER BY kod";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        string kod = rdr.IsDBNull(1) ? "" : rdr.GetString(1).Trim();
                        int katalog = 0;
                        if (!rdr.IsDBNull(2))
                        {
                            var katObj = rdr.GetValue(2);
                            if (katObj is int ki) katalog = ki;
                            else int.TryParse(Convert.ToString(katObj), out katalog);
                        }
                        products[id] = new ProductImageItem
                        {
                            Id = id,
                            Kod = kod,
                            Katalog = katalog
                        };
                    }
                }

                // 2. Pobierz metadane zdjęć z LibraNet
                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT TowarId, NazwaPliku, TypMIME, Szerokosc, Wysokosc,
                                                RozmiarKB, DataDodania, DodanyPrzez, Zdjecie
                                         FROM dbo.TowarZdjecia
                                         WHERE Aktywne = 1";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int towarId = rdr.GetInt32(0);
                        if (!products.ContainsKey(towarId)) continue;

                        var item = products[towarId];
                        item.HasImage = true;
                        item.NazwaPliku = rdr.IsDBNull(1) ? "" : rdr.GetString(1);
                        item.TypMIME = rdr.IsDBNull(2) ? "" : rdr.GetString(2);
                        item.Szerokosc = rdr.IsDBNull(3) ? 0 : rdr.GetInt32(3);
                        item.Wysokosc = rdr.IsDBNull(4) ? 0 : rdr.GetInt32(4);
                        item.RozmiarKB = rdr.IsDBNull(5) ? 0 : rdr.GetInt32(5);
                        item.DataDodania = rdr.IsDBNull(6) ? null : rdr.GetDateTime(6);
                        item.DodanyPrzez = rdr.IsDBNull(7) ? "" : rdr.GetString(7);

                        if (!rdr.IsDBNull(8))
                        {
                            byte[] imgData = (byte[])rdr[8];
                            item.Thumbnail = BytesToBitmapImage(imgData, 60);
                            item.FullImage = BytesToBitmapImage(imgData, 0);
                        }
                    }
                }

                _allItems = products.Values.OrderBy(p => p.Kod).ToList();
                ApplyFilters();
                UpdateStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Zapis / Usuwanie zdjęć

        private async System.Threading.Tasks.Task SaveProductImageAsync(int towarId, string filePath)
        {
            try
            {
                byte[] imageData = await File.ReadAllBytesAsync(filePath);
                string fileName = Path.GetFileName(filePath);
                string extension = Path.GetExtension(filePath).ToLowerInvariant();

                string mimeType = extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".webp" => "image/webp",
                    _ => "image/unknown"
                };

                int width = 0, height = 0;
                using (var ms = new MemoryStream(imageData))
                {
                    var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.Default);
                    if (decoder.Frames.Count > 0)
                    {
                        width = decoder.Frames[0].PixelWidth;
                        height = decoder.Frames[0].PixelHeight;
                    }
                }

                int sizeKB = imageData.Length / 1024;

                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string checkTable = @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TowarZdjecia')
                    CREATE TABLE dbo.TowarZdjecia (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        TowarId INT NOT NULL,
                        Zdjecie VARBINARY(MAX) NOT NULL,
                        NazwaPliku NVARCHAR(255) NULL,
                        TypMIME NVARCHAR(100) NULL,
                        Szerokosc INT NULL,
                        Wysokosc INT NULL,
                        RozmiarKB INT NULL,
                        DataDodania DATETIME DEFAULT GETDATE(),
                        DodanyPrzez NVARCHAR(100) NULL,
                        Aktywne BIT DEFAULT 1
                    )";
                await using (var cmdCheck = new SqlCommand(checkTable, cn))
                    await cmdCheck.ExecuteNonQueryAsync();

                const string sqlDeactivate = @"UPDATE dbo.TowarZdjecia SET Aktywne = 0 WHERE TowarId = @TowarId";
                await using (var cmdDe = new SqlCommand(sqlDeactivate, cn))
                {
                    cmdDe.Parameters.AddWithValue("@TowarId", towarId);
                    await cmdDe.ExecuteNonQueryAsync();
                }

                const string sqlInsert = @"INSERT INTO dbo.TowarZdjecia
                    (TowarId, Zdjecie, NazwaPliku, TypMIME, Szerokosc, Wysokosc, RozmiarKB, DodanyPrzez)
                    VALUES (@TowarId, @Zdjecie, @NazwaPliku, @TypMIME, @Szerokosc, @Wysokosc, @RozmiarKB, @DodanyPrzez)";

                await using var cmdIns = new SqlCommand(sqlInsert, cn);
                cmdIns.Parameters.AddWithValue("@TowarId", towarId);
                cmdIns.Parameters.AddWithValue("@Zdjecie", imageData);
                cmdIns.Parameters.AddWithValue("@NazwaPliku", fileName);
                cmdIns.Parameters.AddWithValue("@TypMIME", mimeType);
                cmdIns.Parameters.AddWithValue("@Szerokosc", width);
                cmdIns.Parameters.AddWithValue("@Wysokosc", height);
                cmdIns.Parameters.AddWithValue("@RozmiarKB", sizeKB);
                cmdIns.Parameters.AddWithValue("@DodanyPrzez", Environment.UserName);

                await cmdIns.ExecuteNonQueryAsync();

                MessageBox.Show($"Zdjęcie zapisane!\n\nPlik: {fileName}\nRozmiar: {sizeKB} KB\nWymiary: {width}×{height}",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania zdjęcia:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task DeleteProductImageAsync(int towarId)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string sql = @"UPDATE dbo.TowarZdjecia SET Aktywne = 0 WHERE TowarId = @TowarId";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@TowarId", towarId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas usuwania zdjęcia:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Konwersja obrazu

        private BitmapImage? BytesToBitmapImage(byte[] data, int decodeWidth)
        {
            if (data == null || data.Length == 0) return null;
            try
            {
                var image = new BitmapImage();
                using (var ms = new MemoryStream(data))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    if (decodeWidth > 0)
                        image.DecodePixelWidth = decodeWidth;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                }
                return image;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Filtrowanie

        private void ApplyFilters()
        {
            string searchText = txtSearch.Text?.Trim().ToLowerInvariant() ?? "";
            string kategoria = (cmbKategoria.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Wszystkie";
            string status = (cmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Wszystkie";

            _filteredItems = _allItems.Where(item =>
            {
                if (!string.IsNullOrEmpty(searchText) &&
                    !item.Kod.ToLowerInvariant().Contains(searchText))
                    return false;

                if (kategoria == "Świeże" && item.Katalog != 67095) return false;
                if (kategoria == "Mrożone" && item.Katalog != 67153) return false;

                if (status == "Ze zdjęciem" && !item.HasImage) return false;
                if (status == "Bez zdjęcia" && item.HasImage) return false;

                return true;
            }).ToList();

            lvProducts.ItemsSource = _filteredItems;
            txtListCount.Text = $"{_filteredItems.Count} z {_allItems.Count}";
        }

        private void UpdateStats()
        {
            int total = _allItems.Count;
            int withImage = _allItems.Count(x => x.HasImage);
            int noImage = total - withImage;

            txtBadgeAll.Text = total.ToString();
            txtBadgeWithImage.Text = withImage.ToString();
            txtBadgeNoImage.Text = noImage.ToString();
        }

        private void UpdateDetailPanel(ProductImageItem? item)
        {
            if (item == null)
            {
                detailPlaceholder.Visibility = Visibility.Visible;
                detailContent.Visibility = Visibility.Collapsed;
                btnUpload.IsEnabled = false;
                btnDownload.IsEnabled = false;
                btnDelete.IsEnabled = false;
                return;
            }

            detailPlaceholder.Visibility = Visibility.Collapsed;
            detailContent.Visibility = Visibility.Visible;
            btnUpload.IsEnabled = true;
            btnDownload.IsEnabled = item.HasImage;
            btnDelete.IsEnabled = item.HasImage;

            txtDetailKod.Text = item.Kod;

            if (item.HasImage && item.FullImage != null)
            {
                imgPreview.Source = item.FullImage;
                imgPreview.Visibility = Visibility.Visible;
                txtNoImage.Visibility = Visibility.Collapsed;

                txtMetaPlik.Text = item.NazwaPliku;
                txtMetaMIME.Text = item.TypMIME;
                txtMetaWymiary.Text = item.Szerokosc > 0 ? $"{item.Szerokosc} × {item.Wysokosc} px" : "—";
                txtMetaRozmiar.Text = item.RozmiarKB > 0 ? $"{item.RozmiarKB} KB" : "—";
                txtMetaData.Text = item.DataDodania?.ToString("yyyy-MM-dd HH:mm") ?? "—";
                txtMetaDodal.Text = !string.IsNullOrEmpty(item.DodanyPrzez) ? item.DodanyPrzez : "—";
                gridMeta.Visibility = Visibility.Visible;
            }
            else
            {
                imgPreview.Source = null;
                imgPreview.Visibility = Visibility.Collapsed;
                txtNoImage.Visibility = Visibility.Visible;
                gridMeta.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Event handlers

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer?.Stop();
            _searchTimer?.Start();
        }

        private void CmbKategoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilters();
        }

        private void CmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilters();
        }

        private void LvProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = lvProducts.SelectedItem as ProductImageItem;
            UpdateDetailPanel(selected);
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            var selected = lvProducts.SelectedItem as ProductImageItem;
            if (selected == null) return;

            var dialog = new OpenFileDialog
            {
                Title = $"Wybierz zdjęcie dla: {selected.Kod}",
                Filter = "Obrazy|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp|Wszystkie pliki|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                await SaveProductImageAsync(selected.Id, dialog.FileName);
                int selectedId = selected.Id;
                await LoadDataAsync();
                // Przywróć zaznaczenie
                var reselect = _filteredItems.FirstOrDefault(x => x.Id == selectedId);
                if (reselect != null) lvProducts.SelectedItem = reselect;
            }
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            var selected = lvProducts.SelectedItem as ProductImageItem;
            if (selected == null || !selected.HasImage) return;

            string ext = ".jpg";
            string filter = "JPEG|*.jpg";
            if (!string.IsNullOrEmpty(selected.TypMIME))
            {
                (ext, filter) = selected.TypMIME switch
                {
                    "image/png" => (".png", "PNG|*.png"),
                    "image/gif" => (".gif", "GIF|*.gif"),
                    "image/bmp" => (".bmp", "BMP|*.bmp"),
                    "image/webp" => (".webp", "WebP|*.webp"),
                    _ => (".jpg", "JPEG|*.jpg")
                };
            }

            var dialog = new SaveFileDialog
            {
                Title = $"Zapisz zdjęcie: {selected.Kod}",
                FileName = !string.IsNullOrEmpty(selected.NazwaPliku)
                    ? selected.NazwaPliku
                    : $"{selected.Kod}{ext}",
                Filter = $"{filter}|Wszystkie pliki|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();

                    const string sql = @"SELECT Zdjecie FROM dbo.TowarZdjecia
                                         WHERE TowarId = @TowarId AND Aktywne = 1";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@TowarId", selected.Id);
                    var result = await cmd.ExecuteScalarAsync();

                    if (result is byte[] imageData)
                    {
                        await File.WriteAllBytesAsync(dialog.FileName, imageData);
                        MessageBox.Show($"Zdjęcie zapisane:\n{dialog.FileName}",
                            "Pobrano", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd pobierania zdjęcia:\n{ex.Message}",
                        "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = lvProducts.SelectedItem as ProductImageItem;
            if (selected == null || !selected.HasImage) return;

            var result = MessageBox.Show($"Czy na pewno usunąć zdjęcie produktu \"{selected.Kod}\"?",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await DeleteProductImageAsync(selected.Id);
                int selectedId = selected.Id;
                await LoadDataAsync();
                var reselect = _filteredItems.FirstOrDefault(x => x.Id == selectedId);
                if (reselect != null) lvProducts.SelectedItem = reselect;
            }
        }

        #endregion
    }

    #region Model

    public class ProductImageItem
    {
        public int Id { get; set; }
        public string Kod { get; set; } = "";
        public int Katalog { get; set; }

        public bool HasImage { get; set; }
        public string NazwaPliku { get; set; } = "";
        public string TypMIME { get; set; } = "";
        public int Szerokosc { get; set; }
        public int Wysokosc { get; set; }
        public int RozmiarKB { get; set; }
        public DateTime? DataDodania { get; set; }
        public string DodanyPrzez { get; set; } = "";

        public BitmapImage? Thumbnail { get; set; }
        public BitmapImage? FullImage { get; set; }

        // Binding helpers
        public string KategoriaText => Katalog == 67095 ? "Świeże" : "Mrożone";
        public string StatusIcon => HasImage ? "✅" : "❌";
        public string RozmiarText => HasImage && RozmiarKB > 0 ? $"{RozmiarKB} KB" : "";
        public Visibility ImageVisibility => HasImage && Thumbnail != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility NoImageVisibility => HasImage && Thumbnail != null ? Visibility.Collapsed : Visibility.Visible;
        public Visibility MetaVisibility => HasImage ? Visibility.Visible : Visibility.Collapsed;
    }

    #endregion
}
