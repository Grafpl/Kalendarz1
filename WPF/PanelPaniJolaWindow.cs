using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;

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

        // Separacja UI - kamery NIEZALEŻNE od RefreshContent()
        private Grid? _leftPanel;       // Lewy panel (produkty, nawigacja) - ODŚWIEŻANY
        private Grid? _contentPanel;    // Tabele odbiorców - ODŚWIEŻANE
        private Grid? _camerasArea;     // Kamery - STAŁE, NIGDY nie odświeżane

        // Flagi stanu kamer
        private bool _camerasInitialized = false;
        private bool _camera1Connected = false;
        private bool _camera2Connected = false;
        private DispatcherTimer? _reconnectTimer;

        // Kamery - konfiguracja RTSP przez NVR INTERNEC
        // Format URL: rtsp://admin:terePacja12%24@192.168.0.125:554/unicast/c{CHANNEL}/s{STREAM}/live
        // s0 = strumień główny (HD), s1 = podstrumień (SD - mniejsze obciążenie sieci)
        private static readonly List<CameraConfig> _cameras = new()
        {
            new CameraConfig
            {
                Name = "Kanał 6 - PROD_Waga",
                Channel = 6,
                RtspUrl = "rtsp://admin:terePacja12%24@192.168.0.125:554/unicast/c6/s1/live",     // s1 = substream (podgląd)
                RtspUrlHD = "rtsp://admin:terePacja12%24@192.168.0.125:554/unicast/c6/s0/live"    // s0 = main (fullscreen)
            },
            new CameraConfig
            {
                Name = "Kanał 21 - Zew_Tyl",
                Channel = 21,
                RtspUrl = "rtsp://admin:terePacja12%24@192.168.0.125:554/unicast/c21/s1/live",
                RtspUrlHD = "rtsp://admin:terePacja12%24@192.168.0.125:554/unicast/c21/s0/live"
            }
        };

        private class CameraConfig
        {
            public string Name { get; set; } = "";
            public int Channel { get; set; }
            public string RtspUrl { get; set; } = "";       // Substream dla podglądu
            public string RtspUrlHD { get; set; } = "";     // Main stream dla fullscreen
        }

        // LibVLC do streamingu RTSP
        private LibVLC? _libVLC;
        private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer1;
        private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer2;
        private VideoView? _videoView1;
        private VideoView? _videoView2;
        private TextBlock? _camera1Status;
        private TextBlock? _camera2Status;

        // Debug log dla kamer
        private static readonly List<string> _cameraDebugLog = new();

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

            // Inicjalizacja LibVLC dla streamingu RTSP - ZOPTYMALIZOWANE
            Core.Initialize();
            _libVLC = new LibVLC(
                "--rtsp-tcp",                       // TCP zamiast UDP (stabilniejsze)
                "--network-caching=1000",           // 1 sekunda bufora sieciowego
                "--live-caching=1000",              // Bufor dla live stream
                "--rtsp-frame-buffer-size=500000",  // Większy bufor ramek
                "--no-audio",                       // Bez dźwięku
                "--no-stats",                       // Bez statystyk (wydajność)
                "--no-osd",                         // Bez OSD
                "--avcodec-fast",                   // Szybkie dekodowanie
                "--avcodec-threads=2",              // 2 wątki dekodowania
                "--clock-jitter=0",                 // Minimalizuj jitter
                "--drop-late-frames",               // Pomijaj opóźnione ramki
                "--skip-frames"                     // Pomijaj ramki przy opóźnieniu
            );
            LogCamera($"[INIT] LibVLC zainicjalizowany z optymalizacjami");

            Title = "Panel Pani Joli";
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
            Background = new SolidColorBrush(Color.FromRgb(25, 30, 35));
            ResizeMode = ResizeMode.NoResize;

            _mainContainer = new Grid();
            Content = _mainContainer;

            Loaded += async (s, e) => await InitializeAsync();
            Closed += (s, e) => DisposeResources();

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
            // ========== ORYGINALNA STRUKTURA LAYOUTU ==========
            // rootGrid: 2 kolumny (lewa 170px, prawa reszta)
            // prawa kolumna: 2 wiersze (góra: tabele, dół: kamery)

            _mainContainer.Children.Clear();

            var rootGrid = new Grid { Margin = new Thickness(10) };
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) }); // Lewa kolumna
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Prawa kolumna

            // Lewy panel - produkty, nawigacja (ODŚWIEŻANY)
            _leftPanel = new Grid();
            Grid.SetColumn(_leftPanel, 0);
            rootGrid.Children.Add(_leftPanel);

            // Prawa strona z 2 wierszami
            var rightSide = new Grid { Margin = new Thickness(5, 0, 0, 0) };
            rightSide.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Tabele
            rightSide.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Kamery
            Grid.SetColumn(rightSide, 1);
            rootGrid.Children.Add(rightSide);

            // Panel z tabelami (ODŚWIEŻANY)
            _contentPanel = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            _contentPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _contentPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _contentPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(_contentPanel, 0);
            rightSide.Children.Add(_contentPanel);

            // Panel z kamerami - STAŁY, NIGDY nie odświeżany
            _camerasArea = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            _camerasArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _camerasArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(_camerasArea, 1);
            rightSide.Children.Add(_camerasArea);

            _mainContainer.Children.Add(rootGrid);

            // Pokaż ładowanie
            var loadingText = new TextBlock
            {
                Text = "Ładowanie danych...",
                FontSize = 32,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumnSpan(loadingText, 2);
            rootGrid.Children.Add(loadingText);

            // Inicjalizuj kamery RAZ - PRZED ładowaniem danych
            InitializeCameras();

            // Uruchom strumienie RTSP
            StartRtspStreams();

            // Uruchom timer auto-reconnect
            StartReconnectTimer();

            try
            {
                await LoadDefaultViewProductsAsync();
                await LoadProductImagesAsync();
                await LoadDataAsync();

                // Usuń loading text
                rootGrid.Children.Remove(loadingText);

                if (_productDataList.Any())
                {
                    // Uruchom AUTO timer od razu przy starcie
                    StartAutoTimer();
                    RefreshContent(); // Odświeża TYLKO _leftPanel i _contentPanel!
                }
                else
                {
                    var noDataText = new TextBlock
                    {
                        Text = "Brak produktów do wyświetlenia.\nUstaw domyślny widok w Dashboard.",
                        FontSize = 24,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    };
                    Grid.SetColumnSpan(noDataText, 2);
                    rootGrid.Children.Add(noDataText);
                }
            }
            catch (Exception ex)
            {
                _leftPanel.Children.Clear();
                _contentPanel.Children.Clear();
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
                _leftPanel.Children.Add(errorText);
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
            // Sprawdź czy panele są zainicjalizowane
            if (!_productDataList.Any() || _leftPanel == null || _contentPanel == null) return;

            var currentData = _productDataList[_viewIndex];

            // ========== CZYŚĆ TYLKO _leftPanel i _contentPanel, NIGDY _camerasArea! ==========
            _leftPanel.Children.Clear();
            _contentPanel.Children.Clear();

            // ========== LEWA KOLUMNA (produkty, nawigacja) ==========
            var leftStack = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };

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
            leftStack.Children.Add(clockBorder);

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
            leftStack.Children.Add(imageBorder);

            // Nazwa produktu
            leftStack.Children.Add(new TextBlock
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
            leftStack.Children.Add(bilansBorder);

            leftStack.Children.Add(CreateStatBox(uzyjFakt ? "FAKT" : "PLAN", $"{cel:N0}", uzyjFakt ? Color.FromRgb(155, 89, 182) : Color.FromRgb(52, 152, 219)));
            leftStack.Children.Add(CreateStatBox("STAN", $"{currentData.Stan:N0}", Color.FromRgb(26, 188, 156)));
            leftStack.Children.Add(CreateStatBox("ZAM.", $"{currentData.Zamowienia:N0}", Color.FromRgb(230, 126, 34)));

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
            leftStack.Children.Add(datePanel);

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

            leftStack.Children.Add(navPanel);

            // Dodaj lewą kolumnę do _leftPanel
            _leftPanel.Children.Add(leftStack);

            // ========== PRAWA STRONA - TABLICE (kamery są osobno w _camerasArea!) ==========
            var odbiorcy = currentData.Odbiorcy.OrderByDescending(o => o.Zamowione).ToList();
            int maxRowsPerTable = 12;
            var tablica1 = odbiorcy.Take(maxRowsPerTable).ToList();
            var tablica2 = odbiorcy.Skip(maxRowsPerTable).Take(maxRowsPerTable).ToList();

            // Produkty (kafle) - kolumna 0
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
            _contentPanel.Children.Add(produktyPanel);

            // Tabela 1 - kolumna 1
            var tab1 = CreateTable(tablica1, 1);
            Grid.SetColumn(tab1, 1);
            _contentPanel.Children.Add(tab1);

            // Tabela 2 - kolumna 2
            var tab2 = CreateTable(tablica2, maxRowsPerTable + 1);
            Grid.SetColumn(tab2, 2);
            _contentPanel.Children.Add(tab2);

            // ========== KAMERY NIE SĄ TUTAJ! ==========
            // Kamery są w _camerasArea, zainicjalizowane RAZ w InitializeCameras()
            // Działają niezależnie od RefreshContent()
        }

        #region Kamery RTSP

        /// <summary>
        /// Inicjalizuje kamery RAZ przy starcie - pozostaną aktywne przez cały czas.
        /// Ta metoda jest wywoływana TYLKO RAZ w InitializeAsync().
        /// </summary>
        private void InitializeCameras()
        {
            // Sprawdź czy już zainicjalizowane - NIE twórz ponownie!
            if (_camerasInitialized || _camerasArea == null || _libVLC == null) return;

            LogCamera("[INIT] Tworzenie kontenerów kamer (tylko raz)...");

            // === KAMERA 1 ===
            var camera1Border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 25, 30)),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 5, 0),
                ClipToBounds = true
            };
            var camera1Grid = new Grid();

            _videoView1 = new VideoView
            {
                Background = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            camera1Grid.Children.Add(_videoView1);

            _camera1Status = new TextBlock
            {
                Text = "⏳ Łączenie...",
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            camera1Grid.Children.Add(_camera1Status);

            var label1 = CreateCameraLabel(_cameras.Count > 0 ? _cameras[0].Name : "KAMERA 1");
            camera1Grid.Children.Add(label1);

            camera1Border.Child = camera1Grid;
            camera1Border.MouseLeftButtonDown += (s, e) => OpenFullscreenRtsp(0);
            Grid.SetColumn(camera1Border, 0);
            _camerasArea.Children.Add(camera1Border);

            // === KAMERA 2 ===
            var camera2Border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 25, 30)),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(5, 0, 0, 0),
                ClipToBounds = true
            };
            var camera2Grid = new Grid();

            _videoView2 = new VideoView
            {
                Background = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            camera2Grid.Children.Add(_videoView2);

            _camera2Status = new TextBlock
            {
                Text = "⏳ Łączenie...",
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            camera2Grid.Children.Add(_camera2Status);

            var label2 = CreateCameraLabel(_cameras.Count > 1 ? _cameras[1].Name : "KAMERA 2");
            camera2Grid.Children.Add(label2);

            camera2Border.Child = camera2Grid;
            camera2Border.MouseLeftButtonDown += (s, e) => OpenFullscreenRtsp(1);
            Grid.SetColumn(camera2Border, 1);
            _camerasArea.Children.Add(camera2Border);

            // Oznacz jako zainicjalizowane
            _camerasInitialized = true;
            LogCamera("[INIT] Kontenery kamer utworzone - NIE będą tworzone ponownie");
        }

        /// <summary>
        /// Tworzy etykietę z nazwą kamery
        /// </summary>
        private Border CreateCameraLabel(string name)
        {
            var label = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 6, 10, 6)
            };
            label.Child = new TextBlock
            {
                Text = name,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            return label;
        }

        /// <summary>
        /// Uruchamia strumienie RTSP dla obu kamer
        /// </summary>
        private void StartRtspStreams()
        {
            if (_libVLC == null) return;

            StartCameraStream(0);
            StartCameraStream(1);
        }

        /// <summary>
        /// Uruchamia strumień RTSP dla pojedynczej kamery
        /// </summary>
        private void StartCameraStream(int cameraIndex)
        {
            if (_libVLC == null || cameraIndex >= _cameras.Count) return;

            var camera = _cameras[cameraIndex];
            var videoView = cameraIndex == 0 ? _videoView1 : _videoView2;
            var statusText = cameraIndex == 0 ? _camera1Status : _camera2Status;

            if (videoView == null) return;

            try
            {
                // Zatrzymaj stary player jeśli istnieje
                var oldPlayer = cameraIndex == 0 ? _mediaPlayer1 : _mediaPlayer2;
                if (oldPlayer != null)
                {
                    try
                    {
                        oldPlayer.Stop();
                        oldPlayer.Dispose();
                    }
                    catch { }
                }

                // Nowy MediaPlayer
                var player = new LibVLCSharp.Shared.MediaPlayer(_libVLC)
                {
                    EnableHardwareDecoding = true
                };

                if (cameraIndex == 0)
                    _mediaPlayer1 = player;
                else
                    _mediaPlayer2 = player;

                videoView.MediaPlayer = player;

                // Event: odtwarzanie rozpoczęte
                player.Playing += (s, e) => Dispatcher.Invoke(() =>
                {
                    if (statusText != null)
                        statusText.Visibility = Visibility.Collapsed;

                    if (cameraIndex == 0)
                        _camera1Connected = true;
                    else
                        _camera2Connected = true;

                    LogCamera($"[CAM{cameraIndex + 1}] ✓ Połączono i odtwarza");
                });

                // Event: błąd
                player.EncounteredError += (s, e) => Dispatcher.Invoke(() =>
                {
                    if (statusText != null)
                    {
                        statusText.Text = "❌ Błąd - ponawiam...";
                        statusText.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                        statusText.Visibility = Visibility.Visible;
                    }

                    if (cameraIndex == 0)
                        _camera1Connected = false;
                    else
                        _camera2Connected = false;

                    LogCamera($"[CAM{cameraIndex + 1}] ✗ Błąd połączenia");
                });

                // Event: koniec strumienia (rozłączenie)
                player.EndReached += (s, e) => Dispatcher.Invoke(() =>
                {
                    if (cameraIndex == 0)
                        _camera1Connected = false;
                    else
                        _camera2Connected = false;

                    if (statusText != null)
                    {
                        statusText.Text = "⏳ Rozłączono - ponawiam...";
                        statusText.Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15));
                        statusText.Visibility = Visibility.Visible;
                    }

                    LogCamera($"[CAM{cameraIndex + 1}] Strumień zakończony");
                });

                // Utwórz media z opcjami
                var media = new Media(_libVLC, new Uri(camera.RtspUrl));
                media.AddOption(":rtsp-tcp");
                media.AddOption(":network-caching=1000");
                media.AddOption(":live-caching=1000");
                media.AddOption(":clock-jitter=0");
                media.AddOption(":rtsp-timeout=10");

                // Uruchom
                player.Play(media);

                // Ustaw status
                if (statusText != null)
                {
                    statusText.Text = "⏳ Łączenie...";
                    statusText.Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166));
                    statusText.Visibility = Visibility.Visible;
                }

                LogCamera($"[CAM{cameraIndex + 1}] Łączenie z: {camera.RtspUrl}");
            }
            catch (Exception ex)
            {
                LogCamera($"[CAM{cameraIndex + 1}] Wyjątek: {ex.Message}");

                if (cameraIndex == 0)
                    _camera1Connected = false;
                else
                    _camera2Connected = false;
            }
        }

        /// <summary>
        /// Timer automatycznego ponownego łączenia - sprawdza co 10 sekund
        /// </summary>
        private void StartReconnectTimer()
        {
            _reconnectTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _reconnectTimer.Tick += (s, e) =>
            {
                // Sprawdź kamerę 1
                if (!_camera1Connected || (_mediaPlayer1 != null && !_mediaPlayer1.IsPlaying))
                {
                    LogCamera("[RECONNECT] Ponawiam połączenie z kamerą 1...");
                    StartCameraStream(0);
                }

                // Sprawdź kamerę 2
                if (!_camera2Connected || (_mediaPlayer2 != null && !_mediaPlayer2.IsPlaying))
                {
                    LogCamera("[RECONNECT] Ponawiam połączenie z kamerą 2...");
                    StartCameraStream(1);
                }
            };
            _reconnectTimer.Start();
            LogCamera("[RECONNECT] Timer auto-reconnect uruchomiony (co 10s)");
        }

        /// <summary>
        /// Otwiera kamerę w trybie pełnoekranowym
        /// </summary>
        private void OpenFullscreenRtsp(int cameraIndex)
        {
            if (_libVLC == null || cameraIndex >= _cameras.Count) return;

            var camera = _cameras[cameraIndex];

            var fullscreenWindow = new Window
            {
                Title = camera.Name,
                WindowState = WindowState.Maximized,
                WindowStyle = WindowStyle.None,
                Background = Brushes.Black,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true
            };

            var fullscreenGrid = new Grid();

            // Pełnoekranowy VideoView
            var fullscreenVideoView = new VideoView { Background = Brushes.Black };
            fullscreenGrid.Children.Add(fullscreenVideoView);

            // MediaPlayer dla fullscreen - używaj RtspUrlHD (s0) dla lepszej jakości
            var fullscreenUrl = !string.IsNullOrEmpty(camera.RtspUrlHD) ? camera.RtspUrlHD : camera.RtspUrl;
            var fullscreenPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC)
            {
                EnableHardwareDecoding = true
            };
            fullscreenVideoView.MediaPlayer = fullscreenPlayer;

            var fullscreenMedia = new Media(_libVLC, new Uri(fullscreenUrl));
            fullscreenMedia.AddOption(":rtsp-tcp");
            fullscreenMedia.AddOption(":network-caching=1000");
            fullscreenMedia.AddOption(":live-caching=1000");
            fullscreenPlayer.Play(fullscreenMedia);

            LogCamera($"[FULLSCREEN] Otwarto kamerę {cameraIndex + 1}: {fullscreenUrl}");

            // Przycisk zamknięcia
            var closeBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(25, 15, 25, 15),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 30, 30, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            closeBtn.Child = new TextBlock
            {
                Text = "✕ ZAMKNIJ",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            closeBtn.MouseLeftButtonDown += (s, e) =>
            {
                fullscreenPlayer.Stop();
                fullscreenPlayer.Dispose();
                fullscreenWindow.Close();
                e.Handled = true;
            };
            fullscreenGrid.Children.Add(closeBtn);

            // Nazwa kamery
            var nameLabel = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(30, 30, 0, 0),
                Padding = new Thickness(15, 8, 15, 8),
                CornerRadius = new CornerRadius(5)
            };
            nameLabel.Child = new TextBlock
            {
                Text = camera.Name,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            fullscreenGrid.Children.Add(nameLabel);

            fullscreenWindow.Content = fullscreenGrid;
            fullscreenWindow.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    fullscreenPlayer.Stop();
                    fullscreenPlayer.Dispose();
                    fullscreenWindow.Close();
                }
            };
            fullscreenWindow.Closed += (s, e) =>
            {
                fullscreenPlayer.Stop();
                fullscreenPlayer.Dispose();
            };
            fullscreenWindow.ShowDialog();
        }

        /// <summary>
        /// Zwalnia zasoby LibVLC i MediaPlayer
        /// </summary>
        private void DisposeResources()
        {
            // Zatrzymaj wszystkie timery
            _autoTimer?.Stop();
            _clockTimer?.Stop();
            _reconnectTimer?.Stop();

            LogCamera("[DISPOSE] Zwalnianie zasobów...");

            try
            {
                // Zatrzymaj odtwarzacze
                _mediaPlayer1?.Stop();
                _mediaPlayer2?.Stop();

                // Poczekaj chwilę na zatrzymanie
                System.Threading.Thread.Sleep(100);

                // Zwolnij zasoby
                _mediaPlayer1?.Dispose();
                _mediaPlayer2?.Dispose();
                _libVLC?.Dispose();

                _mediaPlayer1 = null;
                _mediaPlayer2 = null;
                _libVLC = null;

                // Reset flag
                _camerasInitialized = false;
                _camera1Connected = false;
                _camera2Connected = false;
            }
            catch (Exception ex)
            {
                LogCamera($"[DISPOSE] Błąd: {ex.Message}");
            }

            LogCamera("[DISPOSE] Zasoby kamery zwolnione");
        }

        private static void LogCamera(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}";
            _cameraDebugLog.Add(logEntry);

            // Zachowaj tylko ostatnie 100 wpisów
            while (_cameraDebugLog.Count > 100)
                _cameraDebugLog.RemoveAt(0);

            // Zapisz do pliku debug
            try
            {
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "camera_debug.log");
                File.AppendAllText(logPath, logEntry + Environment.NewLine);
            }
            catch { }
        }

        #endregion

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
