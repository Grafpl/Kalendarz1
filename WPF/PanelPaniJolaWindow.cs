using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.WPF
{
    /// <summary>
    /// Panel Pani Joli - uproszczony widok zamówień dla użytkownika dotykowego.
    /// Samodzielne okno z własnymi danymi i logiką.
    /// </summary>
    public class PanelPaniJolaWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private DateTime _selectedDate;

        // Dane
        private List<ProductData> _productDataList = new();
        private List<int> _selectedProductIds = new();
        private Dictionary<int, BitmapImage?> _productImages = new();

        // UI
        private int _viewIndex = 0;
        private bool _isAutoPlay = true; // AUTO włączone domyślnie
        private DispatcherTimer? _autoTimer;
        private DispatcherTimer? _clockTimer;
        private int _autoCountdown = 40; // 40 sekund
        private TextBlock? _clockText;
        private TextBlock? _countdownText;
        private ProgressBar? _countdownBar;
        private Grid _mainContainer;

        // Klasy danych
        private class ProductData
        {
            public int Id { get; set; }
            public string Kod { get; set; } = "";
            public string Nazwa { get; set; } = "";
            public decimal Plan { get; set; }
            public decimal Fakt { get; set; }
            public decimal Stan { get; set; }
            public decimal Zamowienia { get; set; }
            public decimal Wydania { get; set; }
            public decimal Bilans { get; set; }
            public List<OdbiorcaZamowienie> Odbiorcy { get; set; } = new();
        }

        private class OdbiorcaZamowienie
        {
            public int KlientId { get; set; }
            public string NazwaOdbiorcy { get; set; } = "";
            public decimal Zamowione { get; set; }
            public decimal Wydane { get; set; }
        }

        public PanelPaniJolaWindow(string connLibra, string connHandel)
        {
            _connLibra = connLibra;
            _connHandel = connHandel;
            _selectedDate = GetDefaultDate();

            Title = "Panel Pani Joli";
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
            Background = new SolidColorBrush(Color.FromRgb(25, 30, 35));
            ResizeMode = ResizeMode.NoResize;

            _mainContainer = new Grid();
            Content = _mainContainer;

            Loaded += async (s, e) => await InitializeAsync();
            Closed += (s, e) => { _autoTimer?.Stop(); _clockTimer?.Stop(); };

            KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape) Close();
                else if (e.Key == System.Windows.Input.Key.Up || e.Key == System.Windows.Input.Key.Left)
                {
                    _viewIndex = (_viewIndex - 1 + _productDataList.Count) % _productDataList.Count;
                    _autoCountdown = 40;
                    RefreshContent();
                }
                else if (e.Key == System.Windows.Input.Key.Down || e.Key == System.Windows.Input.Key.Right)
                {
                    _viewIndex = (_viewIndex + 1) % _productDataList.Count;
                    _autoCountdown = 40;
                    RefreshContent();
                }
            };
        }

        private static DateTime GetDefaultDate()
        {
            var now = DateTime.Now;
            var today = now.Date;
            if (now.Hour < 14) return today;
            var nextDay = today.AddDays(1);
            while (nextDay.DayOfWeek == DayOfWeek.Saturday || nextDay.DayOfWeek == DayOfWeek.Sunday)
                nextDay = nextDay.AddDays(1);
            return nextDay;
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            // Pokaż ładowanie
            _mainContainer.Children.Clear();
            var loadingText = new TextBlock
            {
                Text = "Ładowanie danych...",
                FontSize = 32,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _mainContainer.Children.Add(loadingText);

            try
            {
                await LoadDefaultViewProductsAsync();
                await LoadProductImagesAsync();
                await LoadDataAsync();

                if (_productDataList.Any())
                {
                    // Uruchom AUTO timer od razu przy starcie
                    StartAutoTimer();
                    RefreshContent();
                }
                else
                {
                    _mainContainer.Children.Clear();
                    var noDataText = new TextBlock
                    {
                        Text = "Brak produktów do wyświetlenia.\nUstaw domyślny widok w Dashboard.",
                        FontSize = 24,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    };
                    _mainContainer.Children.Add(noDataText);
                }
            }
            catch (Exception ex)
            {
                _mainContainer.Children.Clear();
                var errorText = new TextBlock
                {
                    Text = $"Błąd: {ex.Message}",
                    FontSize = 18,
                    Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(50)
                };
                _mainContainer.Children.Add(errorText);
            }
        }

        private async System.Threading.Tasks.Task LoadDefaultViewProductsAsync()
        {
            _selectedProductIds.Clear();

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            const string sql = @"SELECT TOP 1 ProduktyIds FROM dbo.DashboardWidoki WHERE IsDomyslny = 1";
            await using var cmd = new SqlCommand(sql, cn);
            var result = await cmd.ExecuteScalarAsync();

            if (result != null && result != DBNull.Value)
            {
                var idsStr = result.ToString();
                if (!string.IsNullOrEmpty(idsStr))
                {
                    _selectedProductIds = idsStr.Split(',')
                        .Where(s => int.TryParse(s, out _))
                        .Select(int.Parse)
                        .ToList();
                }
            }
        }

        private async System.Threading.Tasks.Task LoadProductImagesAsync()
        {
            try
            {
                _productImages.Clear();
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string sql = @"SELECT TowarId, Zdjecie FROM dbo.TowarZdjecia WHERE Aktywne = 1";
                await using var cmd = new SqlCommand(sql, cn);
                await using var rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    int towarId = rdr.GetInt32(0);
                    if (!rdr.IsDBNull(1))
                    {
                        byte[] imageData = (byte[])rdr["Zdjecie"];
                        var image = BytesToBitmapImage(imageData);
                        _productImages[towarId] = image;
                    }
                }
            }
            catch { }
        }

        private BitmapImage? BytesToBitmapImage(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            try
            {
                var image = new BitmapImage();
                using (var ms = new MemoryStream(data))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                }
                return image;
            }
            catch { return null; }
        }

        private BitmapImage? GetProductImage(int towarId) =>
            _productImages.TryGetValue(towarId, out var img) ? img : null;

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (!_selectedProductIds.Any()) return;

            DateTime day = _selectedDate.Date;

            // Konfiguracja wydajności
            decimal wspolczynnikTuszki = 64m, procentA = 35m, procentB = 65m;
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                const string sql = @"SELECT TOP 1 WspolczynnikTuszki, ProcentTuszkaA, ProcentTuszkaB
                                     FROM KonfiguracjaWydajnosci WHERE DataOd <= @Data AND Aktywny = 1 ORDER BY DataOd DESC";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", day);
                await using var rdr = await cmd.ExecuteReaderAsync();
                if (await rdr.ReadAsync())
                {
                    wspolczynnikTuszki = rdr.IsDBNull(0) ? 64m : rdr.GetDecimal(0);
                    procentA = rdr.IsDBNull(1) ? 35m : rdr.GetDecimal(1);
                    procentB = rdr.IsDBNull(2) ? 65m : rdr.GetDecimal(2);
                }
            }
            catch { }

            // Konfiguracja produktów
            var konfiguracjaProcenty = new Dictionary<int, decimal>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                const string sql = @"SELECT kp.TowarID, kp.ProcentUdzialu FROM KonfiguracjaProduktow kp
                                     INNER JOIN (SELECT MAX(DataOd) as MaxData FROM KonfiguracjaProduktow WHERE DataOd <= @Data AND Aktywny = 1) sub
                                     ON kp.DataOd = sub.MaxData WHERE kp.Aktywny = 1";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", day);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    if (!rdr.IsDBNull(0) && !rdr.IsDBNull(1))
                        konfiguracjaProcenty[rdr.GetInt32(0)] = rdr.GetDecimal(1);
                }
            }
            catch { }

            // Harmonogram dostaw
            decimal totalMassDek = 0m;
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                const string sql = @"SELECT WagaDek, SztukiDek FROM dbo.HarmonogramDostaw WHERE DataOdbioru = @Day AND Bufor = 'Potwierdzony'";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Day", day);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var weight = rdr.IsDBNull(0) ? 0m : Convert.ToDecimal(rdr.GetValue(0));
                    var quantity = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                    totalMassDek += (weight * quantity);
                }
            }
            decimal pulaTuszki = totalMassDek * (wspolczynnikTuszki / 100m);
            decimal pulaTuszkiA = pulaTuszki * (procentA / 100m);
            decimal pulaTuszkiB = pulaTuszki * (procentB / 100m);

            // FAKT - przychody tuszki
            var faktTuszka = new Dictionary<int, decimal>();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) FROM [HANDEL].[HM].[MZ] MZ
                                     JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                                     WHERE MG.seria = 'sPWU' AND MG.aktywny=1 AND MG.data = @Day GROUP BY MZ.idtw";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Day", day);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    faktTuszka[rdr.GetInt32(0)] = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                }
            }

            // FAKT - przychody elementów
            var faktElementy = new Dictionary<int, decimal>();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) FROM [HANDEL].[HM].[MZ] MZ
                                     JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                                     WHERE MG.seria IN ('sPWP', 'PWP') AND MG.aktywny=1 AND MG.data = @Day GROUP BY MZ.idtw";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Day", day);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    faktElementy[rdr.GetInt32(0)] = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                }
            }

            // Zamówienia
            var orderSum = new Dictionary<int, decimal>();
            var orderIds = new List<int>();
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                const string sql = @"SELECT Id FROM dbo.ZamowieniaMieso WHERE DataUboju = @Day AND Status <> 'Anulowane'";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Day", day);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync()) orderIds.Add(rdr.GetInt32(0));
            }

            // Kontrahenci
            var kontrahenci = new Dictionary<int, string>();
            await using (var cnHandel = new SqlConnection(_connHandel))
            {
                await cnHandel.OpenAsync();
                const string sqlKontr = @"SELECT Id, Shortcut FROM [HANDEL].[SSCommon].[STContractors]";
                await using var cmdKontr = new SqlCommand(sqlKontr, cnHandel);
                await using var rdKontr = await cmdKontr.ExecuteReaderAsync();
                while (await rdKontr.ReadAsync())
                {
                    int id = rdKontr.GetInt32(0);
                    string shortcut = rdKontr.IsDBNull(1) ? "" : rdKontr.GetString(1);
                    kontrahenci[id] = string.IsNullOrWhiteSpace(shortcut) ? $"KH {id}" : shortcut;
                }
            }

            var orderDetails = new Dictionary<int, List<(int KlientId, string Nazwa, decimal Ilosc)>>();
            if (orderIds.Any())
            {
                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    var sql = $@"SELECT KodTowaru, SUM(Ilosc) FROM [dbo].[ZamowieniaMiesoTowar]
                                 WHERE ZamowienieId IN ({string.Join(",", orderIds)}) GROUP BY KodTowaru";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        orderSum[rdr.GetInt32(0)] = rdr.IsDBNull(1) ? 0m : rdr.GetDecimal(1);
                    }
                }

                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    var sql = $@"SELECT t.KodTowaru, z.KlientId, SUM(t.Ilosc) as Ilosc
                                 FROM [dbo].[ZamowieniaMiesoTowar] t
                                 INNER JOIN [dbo].[ZamowieniaMieso] z ON t.ZamowienieId = z.Id
                                 WHERE t.ZamowienieId IN ({string.Join(",", orderIds)})
                                 GROUP BY t.KodTowaru, z.KlientId ORDER BY t.KodTowaru, SUM(t.Ilosc) DESC";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int productId = rdr.GetInt32(0);
                        int klientId = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                        decimal ilosc = rdr.IsDBNull(2) ? 0m : rdr.GetDecimal(2);
                        string odbiorcaNazwa = kontrahenci.TryGetValue(klientId, out var nazwa) ? nazwa : $"Nieznany ({klientId})";

                        if (!orderDetails.ContainsKey(productId))
                            orderDetails[productId] = new List<(int, string, decimal)>();
                        orderDetails[productId].Add((klientId, odbiorcaNazwa, ilosc));
                    }
                }
            }

            // Stany magazynowe
            var stanyMag = new Dictionary<int, decimal>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                const string sql = @"SELECT ProduktId, Stan FROM dbo.StanyMagazynowe WHERE Data = @Data";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", day);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    if (!rdr.IsDBNull(0))
                        stanyMag[rdr.GetInt32(0)] = rdr.IsDBNull(1) ? 0m : rdr.GetDecimal(1);
                }
            }
            catch { }

            // Info o produktach
            var productInfo = new Dictionary<int, (string Kod, string Nazwa)>();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                var idList = string.Join(",", _selectedProductIds);
                var sql = $@"SELECT ID, kod, nazwa FROM [HANDEL].[HM].[TW] WHERE ID IN ({idList})";
                await using var cmd = new SqlCommand(sql, cn);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    int id = rdr.GetInt32(0);
                    string kod = rdr.IsDBNull(1) ? "" : rdr.GetString(1);
                    string nazwa = rdr.IsDBNull(2) ? "" : rdr.GetString(2);
                    productInfo[id] = (kod, nazwa);
                }
            }

            // Oblicz dane produktów
            _productDataList.Clear();
            foreach (var productId in _selectedProductIds)
            {
                if (!productInfo.TryGetValue(productId, out var info)) continue;

                decimal plan = 0m, fakt = 0m;
                string kodLower = info.Kod.ToLower();

                if (kodLower.Contains("kurczak a") || kodLower.Contains("tuszka a") ||
                    (kodLower.Contains("kurczak") && kodLower.EndsWith(" a")))
                {
                    plan = pulaTuszkiA;
                    fakt = faktTuszka.TryGetValue(productId, out var f) ? f : 0m;
                }
                else if (kodLower.Contains("kurczak b") || kodLower.Contains("tuszka b") ||
                         (kodLower.Contains("kurczak") && kodLower.EndsWith(" b")))
                {
                    plan = pulaTuszkiB;
                    fakt = faktTuszka.TryGetValue(productId, out var f) ? f : 0m;
                }
                else if (konfiguracjaProcenty.TryGetValue(productId, out var procent))
                {
                    plan = pulaTuszkiB * (procent / 100m);
                    fakt = faktElementy.TryGetValue(productId, out var f) ? f : 0m;
                }
                else
                {
                    fakt = faktElementy.TryGetValue(productId, out var f) ? f : 0m;
                }

                decimal stan = stanyMag.TryGetValue(productId, out var s) ? s : 0m;
                decimal zam = orderSum.TryGetValue(productId, out var z) ? z : 0m;
                decimal przychodDoUzycia = fakt > 0 ? fakt : plan;
                decimal bilans = przychodDoUzycia + stan - zam;

                var odbiorcy = new List<OdbiorcaZamowienie>();
                if (orderDetails.TryGetValue(productId, out var details))
                {
                    odbiorcy = details.Select(d => new OdbiorcaZamowienie
                    {
                        KlientId = d.KlientId,
                        NazwaOdbiorcy = d.Nazwa,
                        Zamowione = d.Ilosc,
                        Wydane = 0m
                    }).ToList();
                }

                _productDataList.Add(new ProductData
                {
                    Id = productId,
                    Kod = info.Kod,
                    Nazwa = info.Nazwa,
                    Plan = plan,
                    Fakt = fakt,
                    Stan = stan,
                    Zamowienia = zam,
                    Bilans = bilans,
                    Odbiorcy = odbiorcy
                });
            }
        }

        private void StartAutoTimer()
        {
            if (_autoTimer != null) return; // już uruchomiony

            _isAutoPlay = true;
            _autoCountdown = 40;
            _autoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(40) };
            _autoTimer.Tick += (ts, te) =>
            {
                _viewIndex = (_viewIndex + 1) % _productDataList.Count;
                _autoCountdown = 40;
                RefreshContent();
            };
            _autoTimer.Start();
        }

        private void RefreshContent()
        {
            if (!_productDataList.Any()) return;

            var currentData = _productDataList[_viewIndex];
            _mainContainer.Children.Clear();

            var mainGrid = new Grid { Margin = new Thickness(10) };
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // LEWA KOLUMNA
            var leftPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };

            // Zegar
            var clockBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _clockText = new TextBlock
            {
                Text = DateTime.Now.ToString("HH:mm:ss"),
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            clockBorder.Child = _clockText;
            leftPanel.Children.Add(clockBorder);

            if (_clockTimer == null)
            {
                _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _clockTimer.Tick += (s, e) =>
                {
                    if (_clockText != null) _clockText.Text = DateTime.Now.ToString("HH:mm:ss");
                    if (_isAutoPlay && _countdownText != null && _countdownBar != null)
                    {
                        _autoCountdown--;
                        if (_autoCountdown <= 0) _autoCountdown = 40;
                        _countdownText.Text = $"{_autoCountdown}s";
                        _countdownBar.Value = _autoCountdown;
                    }
                };
                _clockTimer.Start();
            }

            // Zdjęcie produktu
            var productImage = GetProductImage(currentData.Id);
            var imageBorder = new Border
            {
                Width = 140, Height = 140,
                CornerRadius = new CornerRadius(10),
                Background = productImage != null
                    ? (Brush)new ImageBrush { ImageSource = productImage, Stretch = Stretch.UniformToFill }
                    : new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                Margin = new Thickness(0, 0, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            if (productImage == null)
                imageBorder.Child = new TextBlock { Text = "📦", FontSize = 40, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)) };
            leftPanel.Children.Add(imageBorder);

            // Nazwa produktu
            leftPanel.Children.Add(new TextBlock
            {
                Text = currentData.Kod,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Kafelki z informacjami
            bool uzyjFakt = currentData.Fakt > 0;
            decimal cel = uzyjFakt ? currentData.Fakt : currentData.Plan;
            decimal bilans = cel + currentData.Stan - currentData.Zamowienia;

            var bilansBorder = new Border
            {
                Background = new SolidColorBrush(bilans >= 0 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(231, 76, 60)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6),
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var bilansStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            bilansStack.Children.Add(new TextBlock { Text = "BILANS", FontSize = 11, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
            bilansStack.Children.Add(new TextBlock { Text = $"{bilans:N0} kg", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
            bilansBorder.Child = bilansStack;
            leftPanel.Children.Add(bilansBorder);

            leftPanel.Children.Add(CreateStatBox(uzyjFakt ? "FAKT" : "PLAN", $"{cel:N0}", uzyjFakt ? Color.FromRgb(155, 89, 182) : Color.FromRgb(52, 152, 219)));
            leftPanel.Children.Add(CreateStatBox("STAN", $"{currentData.Stan:N0}", Color.FromRgb(26, 188, 156)));
            leftPanel.Children.Add(CreateStatBox("ZAM.", $"{currentData.Zamowienia:N0}", Color.FromRgb(230, 126, 34)));

            // DatePicker
            var datePanel = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
            datePanel.Children.Add(new TextBlock { Text = "DATA:", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)), HorizontalAlignment = HorizontalAlignment.Center });

            var dateGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
            dateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnDatePrev = new Button { Content = "◀", FontSize = 16, Width = 32, Height = 32, Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
            var dateTxt = new TextBlock { Text = _selectedDate.ToString("dd.MM.yyyy"), FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
            var btnDateNext = new Button { Content = "▶", FontSize = 16, Width = 32, Height = 32, Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };

            btnDatePrev.Click += async (s, e) => { _selectedDate = _selectedDate.AddDays(-1); await LoadDataAsync(); RefreshContent(); };
            btnDateNext.Click += async (s, e) => { _selectedDate = _selectedDate.AddDays(1); await LoadDataAsync(); RefreshContent(); };

            Grid.SetColumn(btnDatePrev, 0); Grid.SetColumn(dateTxt, 1); Grid.SetColumn(btnDateNext, 2);
            dateGrid.Children.Add(btnDatePrev); dateGrid.Children.Add(dateTxt); dateGrid.Children.Add(btnDateNext);
            datePanel.Children.Add(dateGrid);

            string dzienTygodnia = _selectedDate.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));
            datePanel.Children.Add(new TextBlock { Text = dzienTygodnia, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });

            var btnDzis = new Button { Content = "DZIŚ", FontSize = 12, FontWeight = FontWeights.Bold, Padding = new Thickness(15, 5, 15, 5), Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 5, 0, 0), Cursor = System.Windows.Input.Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Center };
            btnDzis.Click += async (s, e) => { _selectedDate = DateTime.Today; await LoadDataAsync(); RefreshContent(); };
            datePanel.Children.Add(btnDzis);
            leftPanel.Children.Add(datePanel);

            // Nawigacja
            var navPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };

            var btnPrev = new Button { Content = "▲", FontSize = 36, Width = 80, Height = 65, Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 0, 5), Cursor = System.Windows.Input.Cursors.Hand };
            btnPrev.Click += (s, e) => { _viewIndex = (_viewIndex - 1 + _productDataList.Count) % _productDataList.Count; _autoCountdown = 40; RefreshContent(); };
            navPanel.Children.Add(btnPrev);

            navPanel.Children.Add(new TextBlock { Text = $"{_viewIndex + 1} z {_productDataList.Count}", FontSize = 16, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 3) });

            var btnNext = new Button { Content = "▼", FontSize = 36, Width = 80, Height = 65, Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 5, 0, 0), Cursor = System.Windows.Input.Cursors.Hand };
            btnNext.Click += (s, e) => { _viewIndex = (_viewIndex + 1) % _productDataList.Count; _autoCountdown = 40; RefreshContent(); };
            navPanel.Children.Add(btnNext);

            // AUTO button
            var btnAuto = new Button
            {
                Content = _isAutoPlay ? "⏹ STOP" : "▶ AUTO",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Width = 80,
                Padding = new Thickness(8, 10, 8, 10),
                Background = new SolidColorBrush(_isAutoPlay ? Color.FromRgb(231, 76, 60) : Color.FromRgb(39, 174, 96)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 8, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnAuto.Click += (s, e) =>
            {
                _isAutoPlay = !_isAutoPlay;
                if (_isAutoPlay)
                {
                    _autoCountdown = 40;
                    _autoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(40) };
                    _autoTimer.Tick += (ts, te) =>
                    {
                        _viewIndex = (_viewIndex + 1) % _productDataList.Count;
                        _autoCountdown = 40;
                        RefreshContent();
                    };
                    _autoTimer.Start();
                }
                else
                {
                    _autoTimer?.Stop();
                    _autoTimer = null;
                }
                RefreshContent();
            };
            navPanel.Children.Add(btnAuto);

            if (_isAutoPlay)
            {
                var countdownPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
                _countdownBar = new ProgressBar { Width = 80, Height = 8, Minimum = 0, Maximum = 40, Value = _autoCountdown, Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)), Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)) };
                countdownPanel.Children.Add(_countdownBar);
                _countdownText = new TextBlock { Text = $"{_autoCountdown}s", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
                countdownPanel.Children.Add(_countdownText);
                navPanel.Children.Add(countdownPanel);
            }

            var btnClose = new Button { Content = "✕ ZAMKNIJ", FontSize = 14, FontWeight = FontWeights.Bold, Padding = new Thickness(12, 8, 12, 8), Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 12, 0, 0), Cursor = System.Windows.Input.Cursors.Hand };
            btnClose.Click += (s, e) => Close();
            navPanel.Children.Add(btnClose);

            // Przycisk wyłączenia komputera
            var btnShutdown = new Button
            {
                Content = "⏻ WYŁĄCZ PC",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Color.FromRgb(120, 40, 40)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 8, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnShutdown.Click += (s, e) =>
            {
                var result = MessageBox.Show(
                    "Czy na pewno chcesz wyłączyć komputer?",
                    "Potwierdzenie wyłączenia",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start("shutdown", "/s /t 0");
                }
            };
            navPanel.Children.Add(btnShutdown);

            leftPanel.Children.Add(navPanel);
            Grid.SetColumn(leftPanel, 0);
            mainGrid.Children.Add(leftPanel);

            // PRAWA STRONA
            var rightPanel = new Grid { Margin = new Thickness(5, 0, 0, 0) };
            rightPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rightPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Tablice
            var tabliceGrid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            tabliceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tabliceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tabliceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var odbiorcy = currentData.Odbiorcy.OrderByDescending(o => o.Zamowione).ToList();
            int maxRowsPerTable = 12;
            var tablica1 = odbiorcy.Take(maxRowsPerTable).ToList();
            var tablica2 = odbiorcy.Skip(maxRowsPerTable).Take(maxRowsPerTable).ToList();

            // Produkty (kafle)
            var produktyPanel = new Border { Background = new SolidColorBrush(Color.FromRgb(35, 40, 48)), CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 5, 0), Padding = new Thickness(5) };
            var produktyGrid = new Grid();
            int maxProdukty = Math.Min(5, _productDataList.Count);
            for (int i = 0; i < maxProdukty; i++) produktyGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < maxProdukty; i++)
            {
                var prod = _productDataList[i];
                int prodIndex = i;
                bool isSelected = (i == _viewIndex);
                var prodImage = GetProductImage(prod.Id);
                var prodBorder = new Border
                {
                    Background = prodImage != null ? (Brush)new ImageBrush { ImageSource = prodImage, Stretch = Stretch.UniformToFill } : new SolidColorBrush(Color.FromRgb(80, 90, 100)),
                    CornerRadius = new CornerRadius(12),
                    BorderBrush = new SolidColorBrush(isSelected ? Color.FromRgb(52, 152, 219) : Color.FromRgb(100, 110, 120)),
                    BorderThickness = new Thickness(isSelected ? 5 : 2),
                    Margin = new Thickness(3),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                var nameText = new TextBlock
                {
                    Text = prod.Kod,
                    FontSize = isSelected ? 24 : 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(5, 0, 5, 12),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                nameText.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, Direction = 315, ShadowDepth = 3, Opacity = 0.9, BlurRadius = 6 };
                prodBorder.Child = nameText;
                prodBorder.MouseLeftButtonDown += (s, e) => { _viewIndex = prodIndex; _autoCountdown = 40; RefreshContent(); };
                Grid.SetRow(prodBorder, i);
                produktyGrid.Children.Add(prodBorder);
            }
            produktyPanel.Child = produktyGrid;
            Grid.SetColumn(produktyPanel, 0);
            tabliceGrid.Children.Add(produktyPanel);

            var tab1 = CreateTable(tablica1, 1);
            Grid.SetColumn(tab1, 1);
            tabliceGrid.Children.Add(tab1);

            var tab2 = CreateTable(tablica2, maxRowsPerTable + 1);
            Grid.SetColumn(tab2, 2);
            tabliceGrid.Children.Add(tab2);

            Grid.SetRow(tabliceGrid, 0);
            rightPanel.Children.Add(tabliceGrid);

            // Kamery
            var camerasGrid = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            camerasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            camerasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Action<int> openFullscreenCamera = (cameraNum) =>
            {
                var fullscreenWindow = new Window
                {
                    Title = $"Kamera {cameraNum}",
                    WindowState = WindowState.Maximized,
                    WindowStyle = WindowStyle.None,
                    Background = Brushes.Black,
                    ResizeMode = ResizeMode.NoResize,
                    Topmost = true
                };
                var fullscreenGrid = new Grid();
                var cameraContent = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                cameraContent.Children.Add(new TextBlock { Text = "📹", FontSize = 200, HorizontalAlignment = HorizontalAlignment.Center });
                cameraContent.Children.Add(new TextBlock { Text = $"KAMERA {cameraNum}", FontSize = 72, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) });
                cameraContent.Children.Add(new TextBlock { Text = "Kliknij aby zamknąć", FontSize = 18, Foreground = new SolidColorBrush(Color.FromRgb(80, 90, 100)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) });
                fullscreenGrid.Children.Add(cameraContent);
                var closeBtn = new Border { Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)), CornerRadius = new CornerRadius(10), Padding = new Thickness(25, 15, 25, 15), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 30, 30, 0), Cursor = System.Windows.Input.Cursors.Hand };
                closeBtn.Child = new TextBlock { Text = "✕ ZAMKNIJ", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
                closeBtn.MouseLeftButtonDown += (s, e) => { fullscreenWindow.Close(); e.Handled = true; };
                fullscreenGrid.Children.Add(closeBtn);
                fullscreenWindow.Content = fullscreenGrid;
                fullscreenGrid.MouseLeftButtonDown += (s, e) => fullscreenWindow.Close();
                fullscreenWindow.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) fullscreenWindow.Close(); };
                fullscreenWindow.ShowDialog();
            };

            var camera1Border = new Border { Background = new SolidColorBrush(Color.FromRgb(30, 35, 40)), CornerRadius = new CornerRadius(10), BorderBrush = new SolidColorBrush(Color.FromRgb(50, 55, 60)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 3, 0), Cursor = System.Windows.Input.Cursors.Hand };
            var camera1Content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            camera1Content.Children.Add(new TextBlock { Text = "📹", FontSize = 50, HorizontalAlignment = HorizontalAlignment.Center });
            camera1Content.Children.Add(new TextBlock { Text = "KAMERA 1", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(70, 80, 90)), HorizontalAlignment = HorizontalAlignment.Center });
            camera1Content.Children.Add(new TextBlock { Text = "Kliknij = PEŁNY EKRAN", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) });
            camera1Border.Child = camera1Content;
            camera1Border.MouseLeftButtonDown += (s, e) => openFullscreenCamera(1);
            Grid.SetColumn(camera1Border, 0);
            camerasGrid.Children.Add(camera1Border);

            var camera2Border = new Border { Background = new SolidColorBrush(Color.FromRgb(30, 35, 40)), CornerRadius = new CornerRadius(10), BorderBrush = new SolidColorBrush(Color.FromRgb(50, 55, 60)), BorderThickness = new Thickness(1), Margin = new Thickness(3, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand };
            var camera2Content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            camera2Content.Children.Add(new TextBlock { Text = "📹", FontSize = 50, HorizontalAlignment = HorizontalAlignment.Center });
            camera2Content.Children.Add(new TextBlock { Text = "KAMERA 2", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(70, 80, 90)), HorizontalAlignment = HorizontalAlignment.Center });
            camera2Content.Children.Add(new TextBlock { Text = "Kliknij = PEŁNY EKRAN", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) });
            camera2Border.Child = camera2Content;
            camera2Border.MouseLeftButtonDown += (s, e) => openFullscreenCamera(2);
            Grid.SetColumn(camera2Border, 1);
            camerasGrid.Children.Add(camera2Border);

            Grid.SetRow(camerasGrid, 1);
            rightPanel.Children.Add(camerasGrid);

            Grid.SetColumn(rightPanel, 1);
            mainGrid.Children.Add(rightPanel);

            _mainContainer.Children.Add(mainGrid);
        }

        private Border CreateStatBox(string label, string value, Color color)
        {
            var border = new Border { Background = new SolidColorBrush(color), CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(2) };
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(new TextBlock { Text = value, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
            border.Child = stack;
            return border;
        }

        private Border CreateTable(List<OdbiorcaZamowienie> odbiorcy, int startLp)
        {
            var tableBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(30, 40, 50)), CornerRadius = new CornerRadius(8), Margin = new Thickness(2), Padding = new Thickness(0) };
            var tableGrid = new Grid();
            tableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            tableGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var headerBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)), Padding = new Thickness(8, 6, 8, 6) };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

            headerGrid.Children.Add(new TextBlock { Text = "#", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 10, 0) });
            var hdrNazwa = new TextBlock { Text = "ODBIORCA", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            Grid.SetColumn(hdrNazwa, 1); headerGrid.Children.Add(hdrNazwa);
            var hdrZam = new TextBlock { Text = "ZAMÓWIONE", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113)), HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(hdrZam, 2); headerGrid.Children.Add(hdrZam);
            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            tableGrid.Children.Add(headerBorder);

            var listScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var listStack = new StackPanel();
            int lp = startLp;
            foreach (var o in odbiorcy)
            {
                var rowBorder = new Border { Background = new SolidColorBrush(lp % 2 == 0 ? Color.FromRgb(35, 45, 55) : Color.FromRgb(30, 40, 50)), Padding = new Thickness(8, 6, 8, 6) };
                var rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

                rowGrid.Children.Add(new TextBlock { Text = $"{lp}.", FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)), Margin = new Thickness(5, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
                var nazwaText = new TextBlock { Text = o.NazwaOdbiorcy, FontSize = 14, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(nazwaText, 1); rowGrid.Children.Add(nazwaText);
                var zamText = new TextBlock { Text = $"{o.Zamowione:N0} kg", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(zamText, 2); rowGrid.Children.Add(zamText);
                rowBorder.Child = rowGrid;
                listStack.Children.Add(rowBorder);
                lp++;
            }
            listScroll.Content = listStack;
            Grid.SetRow(listScroll, 1);
            tableGrid.Children.Add(listScroll);
            tableBorder.Child = tableGrid;
            return tableBorder;
        }
    }
}
