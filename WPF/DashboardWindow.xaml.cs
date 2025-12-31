using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.WPF
{
    public partial class DashboardWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private DateTime _selectedDate;
        private bool _isLoading;
        private bool _isPanelOpen;

        // Lista wszystkich dostępnych produktów z TW
        private List<ProductItem> _allProducts = new();
        // Produkty wybrane do dashboardu
        private List<int> _selectedProductIds = new();
        // Zapisane widoki
        private List<DashboardView> _savedViews = new();

        // Dane produktu do wyświetlenia
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
            public bool UzytoFakt { get; set; } // true = użyto faktyczny przychód, false = planowany
        }

        // Element produktu w liście wyboru
        public class ProductItem : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string Kod { get; set; } = "";
            public string Nazwa { get; set; } = "";

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        // Zapisany widok dashboardu
        private class DashboardView
        {
            public int Id { get; set; }
            public string Nazwa { get; set; } = "";
            public List<int> ProductIds { get; set; } = new();
        }

        // Kolory dla kart produktów
        private readonly Color[] _cardColors = new[]
        {
            Color.FromRgb(39, 174, 96),   // Zielony
            Color.FromRgb(52, 152, 219),  // Niebieski
            Color.FromRgb(155, 89, 182),  // Fioletowy
            Color.FromRgb(230, 126, 34),  // Pomarańczowy
            Color.FromRgb(231, 76, 60),   // Czerwony
            Color.FromRgb(26, 188, 156),  // Turkusowy
            Color.FromRgb(241, 196, 15),  // Żółty
            Color.FromRgb(52, 73, 94),    // Granatowy
        };

        public DashboardWindow(string connLibra, string connHandel, DateTime? initialDate = null)
        {
            InitializeComponent();
            _connLibra = connLibra;
            _connHandel = connHandel;
            _selectedDate = initialDate ?? DateTime.Today;

            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            dpData.SelectedDate = _selectedDate;
            txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd dddd}";

            await LoadProductsFromTWAsync();
            await LoadSavedViewsAsync();
            await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadProductsFromTWAsync()
        {
            try
            {
                _allProducts.Clear();

                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                // Pobierz produkty z katalogów Świeże (67095) i Mrożone (67153)
                const string sql = @"SELECT ID, kod, nazwa
                                     FROM [HANDEL].[HM].[TW]
                                     WHERE katalog IN (67095, 67153)
                                       AND aktywny = 1
                                     ORDER BY kod";

                await using var cmd = new SqlCommand(sql, cn);
                await using var rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    _allProducts.Add(new ProductItem
                    {
                        Id = rdr.GetInt32(0),
                        Kod = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                        Nazwa = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                        IsSelected = false
                    });
                }

                lstProducts.ItemsSource = _allProducts;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania produktów: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadSavedViewsAsync()
        {
            try
            {
                _savedViews.Clear();

                // Sprawdź czy tabela istnieje, jeśli nie - utwórz
                await EnsureViewsTableExistsAsync();

                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string sql = @"SELECT Id, Nazwa, ProduktyIds FROM dbo.DashboardWidoki ORDER BY Nazwa";

                await using var cmd = new SqlCommand(sql, cn);
                await using var rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    var view = new DashboardView
                    {
                        Id = rdr.GetInt32(0),
                        Nazwa = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                        ProductIds = new List<int>()
                    };

                    if (!rdr.IsDBNull(2))
                    {
                        var idsStr = rdr.GetString(2);
                        if (!string.IsNullOrEmpty(idsStr))
                        {
                            view.ProductIds = idsStr.Split(',')
                                .Where(s => int.TryParse(s, out _))
                                .Select(int.Parse)
                                .ToList();
                        }
                    }

                    _savedViews.Add(view);
                }

                // Aktualizuj ComboBox
                cmbWidok.Items.Clear();
                cmbWidok.Items.Add(new ComboBoxItem { Content = "(Wybierz widok)", Tag = null });
                foreach (var view in _savedViews)
                {
                    cmbWidok.Items.Add(new ComboBoxItem { Content = view.Nazwa, Tag = view });
                }
                cmbWidok.SelectedIndex = 0;
            }
            catch
            {
                // Ignoruj błędy - tabela może nie istnieć
            }
        }

        private async System.Threading.Tasks.Task EnsureViewsTableExistsAsync()
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string sql = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DashboardWidoki')
                    BEGIN
                        CREATE TABLE dbo.DashboardWidoki (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Nazwa NVARCHAR(100) NOT NULL,
                            ProduktyIds NVARCHAR(MAX),
                            DataUtworzenia DATETIME DEFAULT GETDATE()
                        )
                    END";

                await using var cmd = new SqlCommand(sql, cn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                // Sprawdź czy są wybrane produkty
                if (!_selectedProductIds.Any())
                {
                    placeholderPanel.Visibility = Visibility.Visible;
                    icProducts.ItemsSource = null;
                    txtDoSprzedania.Text = "0 kg";
                    txtZamowione.Text = "0 kg";
                    _isLoading = false;
                    return;
                }

                placeholderPanel.Visibility = Visibility.Collapsed;

                bool uzywajWydan = rbBilansWydania?.IsChecked == true;
                DateTime day = _selectedDate.Date;

                // 1. Pobierz konfigurację wydajności
                decimal wspolczynnikTuszki = 64m, procentA = 35m, procentB = 65m;
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    const string sql = @"SELECT WspolczynnikTuszki, ProcentTuszkiA, ProcentTuszkiB
                                         FROM dbo.KonfiguracjaWydajnosci WHERE Data = @Day";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", day);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    if (await rdr.ReadAsync())
                    {
                        wspolczynnikTuszki = rdr.IsDBNull(0) ? 64m : rdr.GetDecimal(0);
                        procentA = rdr.IsDBNull(1) ? 35m : rdr.GetDecimal(1);
                        procentB = rdr.IsDBNull(2) ? 65m : rdr.GetDecimal(2);
                    }
                }
                catch { }

                // 2. Pobierz konfigurację produktów (procenty)
                var konfiguracjaProcenty = new Dictionary<int, decimal>();
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    const string sql = @"SELECT ProduktId, Procent FROM dbo.KonfiguracjaProduktow WHERE Data = @Day";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", day);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        if (!rdr.IsDBNull(0) && !rdr.IsDBNull(1))
                            konfiguracjaProcenty[rdr.GetInt32(0)] = rdr.GetDecimal(1);
                    }
                }
                catch { }

                // 3. Pobierz harmonogram dostaw (dla obliczenia PLAN)
                decimal totalMassDek = 0m;
                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT WagaDek, SztukiDek FROM dbo.HarmonogramDostaw
                                         WHERE DataOdbioru = @Day AND Bufor = 'Potwierdzony'";
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

                // 4. Pobierz FAKT - przychody tuszki (sPWU)
                var faktTuszka = new Dictionary<int, decimal>();
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                                         FROM [HANDEL].[HM].[MZ] MZ
                                         JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                                         WHERE MG.seria = 'sPWU' AND MG.aktywny=1 AND MG.data = @Day
                                         GROUP BY MZ.idtw";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", day);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        decimal qty = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                        faktTuszka[id] = qty;
                    }
                }

                // 5. Pobierz FAKT - przychody elementów (sPWP, PWP)
                var faktElementy = new Dictionary<int, decimal>();
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                                         FROM [HANDEL].[HM].[MZ] MZ
                                         JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                                         WHERE MG.seria IN ('sPWP', 'PWP') AND MG.aktywny=1 AND MG.data = @Day
                                         GROUP BY MZ.idtw";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", day);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        decimal qty = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                        faktElementy[id] = qty;
                    }
                }

                // 6. Pobierz ZAMÓWIENIA dla wybranego dnia
                var orderSum = new Dictionary<int, decimal>();
                var orderIds = new List<int>();

                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT Id FROM dbo.ZamowieniaMieso
                                         WHERE DataUboju = @Day AND Status <> 'Anulowane'";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", day);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        orderIds.Add(rdr.GetInt32(0));
                    }
                }

                if (orderIds.Any())
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    var sql = $@"SELECT KodTowaru, SUM(Ilosc) FROM [dbo].[ZamowieniaMiesoTowar]
                                 WHERE ZamowienieId IN ({string.Join(",", orderIds)}) GROUP BY KodTowaru";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        decimal qty = rdr.IsDBNull(1) ? 0m : rdr.GetDecimal(1);
                        orderSum[id] = qty;
                    }
                }

                // 7. Pobierz WYDANIA (WZ)
                var wydaniaSum = new Dictionary<int, decimal>();
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                                         FROM [HANDEL].[HM].[MZ] MZ
                                         JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                                         WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 AND MG.data = @Day
                                         GROUP BY MZ.idtw";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", day);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        decimal qty = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                        wydaniaSum[id] = qty;
                    }
                }

                // 8. Pobierz STANY MAGAZYNOWE
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

                // 9. Pobierz info o produktach - czy to tuszka czy element
                var productInfo = new Dictionary<int, (string Kod, string Nazwa, bool IsTuszka)>();
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    var idList = string.Join(",", _selectedProductIds);
                    var sql = $@"SELECT ID, kod, nazwa, katalog FROM [HANDEL].[HM].[TW] WHERE ID IN ({idList})";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        string kod = rdr.IsDBNull(1) ? "" : rdr.GetString(1);
                        string nazwa = rdr.IsDBNull(2) ? "" : rdr.GetString(2);

                        // Katalog może być int lub string w bazie
                        int katalog = 0;
                        if (!rdr.IsDBNull(3))
                        {
                            var katObj = rdr.GetValue(3);
                            if (katObj is int ki)
                                katalog = ki;
                            else
                                int.TryParse(Convert.ToString(katObj), out katalog);
                        }

                        // Sprawdź czy to tuszka (np. po nazwie zawierającej "tuszk" lub "kurczak a/b")
                        bool isTuszka = kod.ToLower().Contains("tuszk") ||
                                       (kod.ToLower().Contains("kurczak") && (kod.ToLower().EndsWith(" a") || kod.ToLower().EndsWith(" b")));

                        productInfo[id] = (kod, nazwa, isTuszka);
                    }
                }

                // 10. Oblicz dane dla wybranych produktów
                var productsData = new List<ProductData>();
                foreach (var productId in _selectedProductIds)
                {
                    if (!productInfo.TryGetValue(productId, out var info))
                        continue;

                    decimal plan = 0m, fakt = 0m;
                    bool uzytoFakt = false;

                    if (info.IsTuszka)
                    {
                        // Dla tuszki - użyj puli tuszki A
                        plan = pulaTuszkiA;
                        fakt = faktTuszka.TryGetValue(productId, out var f) ? f : 0m;
                    }
                    else
                    {
                        // Dla elementów - użyj procentu z konfiguracji
                        if (konfiguracjaProcenty.TryGetValue(productId, out var procent))
                            plan = pulaTuszkiB * (procent / 100m);
                        fakt = faktElementy.TryGetValue(productId, out var f) ? f : 0m;
                    }

                    decimal stan = stanyMag.TryGetValue(productId, out var s) ? s : 0m;
                    decimal zam = orderSum.TryGetValue(productId, out var z) ? z : 0m;
                    decimal wyd = wydaniaSum.TryGetValue(productId, out var w) ? w : 0m;
                    decimal odejmij = uzywajWydan ? wyd : zam;

                    // Oblicz bilans - użyj faktycznego jeśli > 0, w przeciwnym razie planowany
                    decimal przychodDoUzycia = fakt > 0 ? fakt : plan;
                    uzytoFakt = fakt > 0;
                    decimal bilans = przychodDoUzycia + stan - odejmij;

                    productsData.Add(new ProductData
                    {
                        Id = productId,
                        Kod = info.Kod,
                        Nazwa = info.Nazwa,
                        Plan = plan,
                        Fakt = fakt,
                        Stan = stan,
                        Zamowienia = zam,
                        Wydania = wyd,
                        Bilans = bilans,
                        UzytoFakt = uzytoFakt
                    });
                }

                // 11. Utwórz karty produktów
                CreateProductCards(productsData);

                // 12. Aktualizuj nagłówek
                decimal totalBilans = productsData.Sum(p => p.Bilans);
                decimal totalZam = productsData.Sum(p => p.Zamowienia);
                txtDoSprzedania.Text = $"{Math.Max(0, totalBilans):N0} kg";
                txtZamowione.Text = $"{totalZam:N0} kg";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void CreateProductCards(List<ProductData> productsData)
        {
            icProducts.Items.Clear();

            int colorIndex = 0;
            foreach (var data in productsData)
            {
                var card = CreateProductCard(data, _cardColors[colorIndex % _cardColors.Length]);
                icProducts.Items.Add(card);
                colorIndex++;
            }
        }

        private Border CreateProductCard(ProductData data, Color headerColor)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(8),
                Width = 380,
                Height = 320
            };

            card.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                ShadowDepth = 2,
                Opacity = 0.1,
                BlurRadius = 15
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Nagłówek karty
            var headerBorder = new Border
            {
                CornerRadius = new CornerRadius(12, 12, 0, 0),
                Padding = new Thickness(20, 15, 20, 15)
            };

            var gradientBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            gradientBrush.GradientStops.Add(new GradientStop(headerColor, 0));
            gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(255,
                (byte)Math.Min(255, headerColor.R + 30),
                (byte)Math.Min(255, headerColor.G + 30),
                (byte)Math.Min(255, headerColor.B + 30)), 1));
            headerBorder.Background = gradientBrush;

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel();
            var titleText = new TextBlock
            {
                Text = data.Kod.ToUpper(),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            titleStack.Children.Add(titleText);

            // Wskaźnik czy użyto faktyczny czy planowany przychód
            var sourceText = new TextBlock
            {
                Text = data.UzytoFakt ? "Przychód: FAKTYCZNY" : "Przychód: PLANOWANY",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Margin = new Thickness(0, 3, 0, 0)
            };
            titleStack.Children.Add(sourceText);

            Grid.SetColumn(titleStack, 0);
            headerGrid.Children.Add(titleStack);

            var bilansText = new TextBlock
            {
                Text = $"{data.Bilans:N0} kg",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(bilansText, 1);
            headerGrid.Children.Add(bilansText);

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // Zawartość karty
            var contentGrid = new Grid { Margin = new Thickness(20) };
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Wykres słupkowy
            var chartGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            CreateBarChart(chartGrid, data, headerColor);
            Grid.SetRow(chartGrid, 0);
            contentGrid.Children.Add(chartGrid);

            // Statystyki
            var statsGrid = new Grid();
            for (int i = 0; i < 4; i++)
                statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labels = new[] { "PLAN", "FAKT", "STAN", "ZAM." };
            var values = new[] { data.Plan, data.Fakt, data.Stan, data.Zamowienia };
            var colors = new[]
            {
                Color.FromRgb(44, 62, 80),   // Plan - ciemny
                Color.FromRgb(39, 174, 96),  // Fakt - zielony
                Color.FromRgb(52, 152, 219), // Stan - niebieski
                Color.FromRgb(231, 76, 60)   // Zam - czerwony
            };

            for (int i = 0; i < 4; i++)
            {
                var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

                var labelText = new TextBlock
                {
                    Text = labels[i],
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                stack.Children.Add(labelText);

                var valueText = new TextBlock
                {
                    Text = $"{values[i]:N0}",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(colors[i]),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                stack.Children.Add(valueText);

                Grid.SetColumn(stack, i);
                statsGrid.Children.Add(stack);
            }

            Grid.SetRow(statsGrid, 1);
            contentGrid.Children.Add(statsGrid);

            Grid.SetRow(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);

            card.Child = mainGrid;
            return card;
        }

        private void CreateBarChart(Grid chartGrid, ProductData data, Color primaryColor)
        {
            chartGrid.Children.Clear();
            chartGrid.ColumnDefinitions.Clear();

            for (int i = 0; i < 4; i++)
                chartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            decimal maxValue = Math.Max(1, new[] { data.Plan, data.Fakt, data.Stan, data.Zamowienia }.Max());

            var values = new[] { data.Plan, data.Fakt, data.Stan, data.Zamowienia };
            var labels = new[] { "Plan", "Fakt", "Stan", "Zam" };
            var colors = new[]
            {
                Color.FromRgb(44, 62, 80),   // Plan - ciemny
                Color.FromRgb(39, 174, 96),  // Fakt - zielony
                Color.FromRgb(52, 152, 219), // Stan - niebieski
                Color.FromRgb(231, 76, 60)   // Zam - czerwony
            };

            for (int i = 0; i < 4; i++)
            {
                var stack = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(5, 0, 5, 0)
                };

                var valueText = new TextBlock
                {
                    Text = $"{values[i]:N0}",
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(colors[i]),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 3)
                };
                stack.Children.Add(valueText);

                double height = maxValue > 0 ? (double)(values[i] / maxValue) * 120 : 0;
                var bar = new Border
                {
                    Width = 40,
                    Height = Math.Max(5, height),
                    CornerRadius = new CornerRadius(4, 4, 0, 0),
                    Background = new SolidColorBrush(colors[i])
                };
                stack.Children.Add(bar);

                var label = new TextBlock
                {
                    Text = labels[i],
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 0)
                };
                stack.Children.Add(label);

                Grid.SetColumn(stack, i);
                chartGrid.Children.Add(stack);
            }
        }

        private void UpdateSelectedCount()
        {
            int count = _allProducts.Count(p => p.IsSelected);
            txtSelectedCount.Text = count.ToString();
        }

        #region Event Handlers

        private void DpData_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (dpData.SelectedDate.HasValue)
            {
                _selectedDate = dpData.SelectedDate.Value;
                txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd dddd}";
                _ = LoadDataAsync();
            }
        }

        private void RbBilans_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            _ = LoadDataAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnTogglePanel_Click(object sender, RoutedEventArgs e)
        {
            _isPanelOpen = !_isPanelOpen;
            sidePanel.Visibility = _isPanelOpen ? Visibility.Visible : Visibility.Collapsed;
            btnTogglePanel.Content = _isPanelOpen ? "Wybierz produkty ◀" : "Wybierz produkty ▶";
        }

        private void BtnClosePanel_Click(object sender, RoutedEventArgs e)
        {
            _isPanelOpen = false;
            sidePanel.Visibility = Visibility.Collapsed;
            btnTogglePanel.Content = "Wybierz produkty ▶";
        }

        private void TxtSearchProduct_TextChanged(object sender, TextChangedEventArgs e)
        {
            var search = txtSearchProduct.Text.ToLower();
            if (string.IsNullOrWhiteSpace(search))
            {
                lstProducts.ItemsSource = _allProducts;
            }
            else
            {
                lstProducts.ItemsSource = _allProducts
                    .Where(p => p.Kod.ToLower().Contains(search) || p.Nazwa.ToLower().Contains(search))
                    .ToList();
            }
        }

        private void ProductCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSelectedCount();
        }

        private void BtnApplySelection_Click(object sender, RoutedEventArgs e)
        {
            _selectedProductIds = _allProducts.Where(p => p.IsSelected).Select(p => p.Id).ToList();
            _ = LoadDataAsync();

            // Zamknij panel
            _isPanelOpen = false;
            sidePanel.Visibility = Visibility.Collapsed;
            btnTogglePanel.Content = "Wybierz produkty ▶";
        }

        private void BtnClearSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var p in _allProducts)
                p.IsSelected = false;
            UpdateSelectedCount();
        }

        private async void BtnSaveView_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedProductIds.Any())
            {
                MessageBox.Show("Najpierw wybierz produkty do zapisania.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new InputDialog("Zapisz widok", "Podaj nazwę widoku:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();

                    var idsStr = string.Join(",", _selectedProductIds);
                    const string sql = @"INSERT INTO dbo.DashboardWidoki (Nazwa, ProduktyIds) VALUES (@Nazwa, @Ids)";

                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Nazwa", dialog.ResponseText);
                    cmd.Parameters.AddWithValue("@Ids", idsStr);
                    await cmd.ExecuteNonQueryAsync();

                    await LoadSavedViewsAsync();
                    MessageBox.Show("Widok został zapisany.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas zapisywania widoku: {ex.Message}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnDeleteView_Click(object sender, RoutedEventArgs e)
        {
            if (cmbWidok.SelectedItem is ComboBoxItem item && item.Tag is DashboardView view)
            {
                var result = MessageBox.Show($"Czy na pewno chcesz usunąć widok '{view.Nazwa}'?",
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await using var cn = new SqlConnection(_connLibra);
                        await cn.OpenAsync();

                        const string sql = @"DELETE FROM dbo.DashboardWidoki WHERE Id = @Id";

                        await using var cmd = new SqlCommand(sql, cn);
                        cmd.Parameters.AddWithValue("@Id", view.Id);
                        await cmd.ExecuteNonQueryAsync();

                        await LoadSavedViewsAsync();
                        MessageBox.Show("Widok został usunięty.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd podczas usuwania widoku: {ex.Message}", "Błąd",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Wybierz widok do usunięcia.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CmbWidok_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            if (cmbWidok.SelectedItem is ComboBoxItem item && item.Tag is DashboardView view)
            {
                // Załaduj produkty z wybranego widoku
                foreach (var p in _allProducts)
                    p.IsSelected = view.ProductIds.Contains(p.Id);

                _selectedProductIds = view.ProductIds.ToList();
                UpdateSelectedCount();
                _ = LoadDataAsync();
            }
        }

        #endregion
    }

    // Dialog do wprowadzania nazwy widoku
    public class InputDialog : Window
    {
        private TextBox _textBox;
        public string ResponseText { get; private set; } = "";

        public InputDialog(string title, string question)
        {
            Title = title;
            Width = 350;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var stack = new StackPanel { Margin = new Thickness(20) };

            stack.Children.Add(new TextBlock { Text = question, Margin = new Thickness(0, 0, 0, 10) });

            _textBox = new TextBox { Height = 30, FontSize = 14 };
            stack.Children.Add(_textBox);

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            okButton.Click += (s, e) =>
            {
                ResponseText = _textBox.Text;
                DialogResult = true;
            };
            buttonsPanel.Children.Add(okButton);

            var cancelButton = new Button
            {
                Content = "Anuluj",
                Width = 80,
                Height = 30,
                IsCancel = true
            };
            buttonsPanel.Children.Add(cancelButton);

            stack.Children.Add(buttonsPanel);

            Content = stack;
        }
    }
}
