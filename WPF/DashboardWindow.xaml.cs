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
            public List<OdbiorcaZamowienie> Odbiorcy { get; set; } = new();
        }

        // Zamówienie od odbiorcy
        private class OdbiorcaZamowienie
        {
            public string NazwaOdbiorcy { get; set; } = "";
            public decimal Ilosc { get; set; }
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

                // 1. Pobierz konfigurację wydajności (tak jak w MainWindow)
                decimal wspolczynnikTuszki = 64m, procentA = 35m, procentB = 65m;
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    const string sql = @"SELECT TOP 1 WspolczynnikTuszki, ProcentTuszkaA, ProcentTuszkaB
                                         FROM KonfiguracjaWydajnosci
                                         WHERE DataOd <= @Data AND Aktywny = 1
                                         ORDER BY DataOd DESC";
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

                // 2. Pobierz konfigurację produktów (TowarID -> ProcentUdzialu) - tak jak w MainWindow
                var konfiguracjaProcenty = new Dictionary<int, decimal>();
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    const string sql = @"SELECT kp.TowarID, kp.ProcentUdzialu
                                         FROM KonfiguracjaProduktow kp
                                         INNER JOIN (
                                             SELECT MAX(DataOd) as MaxData
                                             FROM KonfiguracjaProduktow
                                             WHERE DataOd <= @Data AND Aktywny = 1
                                         ) sub ON kp.DataOd = sub.MaxData
                                         WHERE kp.Aktywny = 1";
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

                // Słownik: productId -> lista (odbiorca, ilość)
                var orderDetails = new Dictionary<int, List<(string Odbiorca, decimal Ilosc)>>();

                if (orderIds.Any())
                {
                    // Najpierw pobierz sumy per produkt
                    await using (var cn = new SqlConnection(_connLibra))
                    {
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

                    // Pobierz szczegóły zamówień z nazwami odbiorców
                    await using (var cn = new SqlConnection(_connLibra))
                    {
                        await cn.OpenAsync();
                        var sql = $@"SELECT t.KodTowaru, k.Nazwa, SUM(t.Ilosc) as Ilosc
                                     FROM [dbo].[ZamowieniaMiesoTowar] t
                                     INNER JOIN [dbo].[ZamowieniaMieso] z ON t.ZamowienieId = z.Id
                                     INNER JOIN [dbo].[Kontrahenci] k ON z.KlientId = k.Id
                                     WHERE t.ZamowienieId IN ({string.Join(",", orderIds)})
                                     GROUP BY t.KodTowaru, k.Nazwa
                                     ORDER BY t.KodTowaru, SUM(t.Ilosc) DESC";
                        await using var cmd = new SqlCommand(sql, cn);
                        await using var rdr = await cmd.ExecuteReaderAsync();
                        while (await rdr.ReadAsync())
                        {
                            int productId = rdr.GetInt32(0);
                            string odbiorca = rdr.IsDBNull(1) ? "Nieznany" : rdr.GetString(1);
                            decimal ilosc = rdr.IsDBNull(2) ? 0m : rdr.GetDecimal(2);

                            if (!orderDetails.ContainsKey(productId))
                                orderDetails[productId] = new List<(string, decimal)>();
                            orderDetails[productId].Add((odbiorca, ilosc));
                        }
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

                // 10. Oblicz dane dla wybranych produktów (logika jak w MainWindow)
                var productsData = new List<ProductData>();
                foreach (var productId in _selectedProductIds)
                {
                    if (!productInfo.TryGetValue(productId, out var info))
                        continue;

                    decimal plan = 0m, fakt = 0m;
                    bool uzytoFakt = false;
                    string kodLower = info.Kod.ToLower();

                    // Sprawdź typ produktu i oblicz plan - tak jak w MainWindow
                    if (kodLower.Contains("kurczak a") || kodLower.Contains("tuszka a") ||
                        (kodLower.Contains("kurczak") && kodLower.EndsWith(" a")))
                    {
                        // Kurczak A / Tuszka A - pula tuszki A
                        plan = pulaTuszkiA;
                        fakt = faktTuszka.TryGetValue(productId, out var f) ? f : 0m;
                    }
                    else if (kodLower.Contains("kurczak b") || kodLower.Contains("tuszka b") ||
                             (kodLower.Contains("kurczak") && kodLower.EndsWith(" b")))
                    {
                        // Kurczak B / Tuszka B - cała pula tuszki B
                        plan = pulaTuszkiB;
                        fakt = faktTuszka.TryGetValue(productId, out var f) ? f : 0m;
                    }
                    else if (konfiguracjaProcenty.TryGetValue(productId, out var procent))
                    {
                        // Element z konfiguracją - użyj procentu z pulaTuszkiB
                        plan = pulaTuszkiB * (procent / 100m);
                        fakt = faktElementy.TryGetValue(productId, out var f) ? f : 0m;
                    }
                    else
                    {
                        // Element bez konfiguracji - tylko faktyczny przychód
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

                    // Pobierz listę odbiorców dla tego produktu
                    var odbiorcy = new List<OdbiorcaZamowienie>();
                    if (orderDetails.TryGetValue(productId, out var details))
                    {
                        odbiorcy = details.Select(d => new OdbiorcaZamowienie
                        {
                            NazwaOdbiorcy = d.Odbiorca,
                            Ilosc = d.Ilosc
                        }).ToList();
                    }

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
                        UzytoFakt = uzytoFakt,
                        Odbiorcy = odbiorcy
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
            // Dynamiczna wysokość w zależności od liczby odbiorców
            int odbircyCount = Math.Min(data.Odbiorcy.Count, 5); // Max 5 odbiorców
            double cardHeight = 280 + (odbircyCount * 22);

            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(8),
                Width = 380,
                MinHeight = cardHeight,
                BorderBrush = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                BorderThickness = new Thickness(2)
            };

            card.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                ShadowDepth = 2,
                Opacity = 0.15,
                BlurRadius = 10
            };

            var mainStack = new StackPanel { Margin = new Thickness(15) };

            // === NAGŁÓWEK - nazwa produktu ===
            var titleText = new TextBlock
            {
                Text = $"[{data.Kod}]",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                Margin = new Thickness(0, 0, 0, 15)
            };
            mainStack.Children.Add(titleText);

            // === PASEK PLAN / FAKT ===
            var planFaktBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10, 8, 10, 8)
            };

            var planFaktStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            if (data.UzytoFakt)
            {
                // Plan przekreślony
                var planText = new TextBlock
                {
                    Text = $"plan {data.Plan:N0}",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                    TextDecorations = TextDecorations.Strikethrough,
                    VerticalAlignment = VerticalAlignment.Center
                };
                planFaktStack.Children.Add(planText);

                planFaktStack.Children.Add(new TextBlock { Text = "  →  ", FontSize = 14, VerticalAlignment = VerticalAlignment.Center });

                // Fakt aktywny
                var faktText = new TextBlock
                {
                    Text = $"fakt {data.Fakt:N0}",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                planFaktStack.Children.Add(faktText);
            }
            else
            {
                // Tylko plan (fakt = 0)
                var planText = new TextBlock
                {
                    Text = $"plan {data.Plan:N0}",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                planFaktStack.Children.Add(planText);

                if (data.Fakt > 0)
                {
                    planFaktStack.Children.Add(new TextBlock { Text = "   ", FontSize = 14 });
                    var faktText = new TextBlock
                    {
                        Text = $"fakt {data.Fakt:N0}",
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    planFaktStack.Children.Add(faktText);
                }
            }

            planFaktBorder.Child = planFaktStack;
            mainStack.Children.Add(planFaktBorder);

            // === PASEK ZAMÓWIENIA ===
            var zamBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10, 8, 10, 8)
            };

            var zamText = new TextBlock
            {
                Text = $"zam {data.Zamowienia:N0}",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            zamBorder.Child = zamText;
            mainStack.Children.Add(zamBorder);

            // === OBLICZENIE BILANSU ===
            var calculationGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            calculationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            calculationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            calculationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            calculationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            calculationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Nagłówki
            var headers = new[] { "Plan/Fakt", "Stan", "Zam.", "Bilans", "" };
            for (int i = 0; i < 4; i++)
            {
                var header = new TextBlock
                {
                    Text = headers[i],
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(10, 0, 10, 2)
                };
                Grid.SetColumn(header, i);
                calculationGrid.Children.Add(header);
            }

            // Wartości
            decimal przychodUzyty = data.UzytoFakt ? data.Fakt : data.Plan;
            var valueColors = new[]
            {
                data.UzytoFakt ? Color.FromRgb(39, 174, 96) : Color.FromRgb(44, 62, 80),
                Color.FromRgb(52, 152, 219),
                Color.FromRgb(231, 76, 60),
                data.Bilans >= 0 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(231, 76, 60)
            };

            var valuesRow = new Grid { Margin = new Thickness(0, 20, 0, 0) };
            for (int i = 0; i < 5; i++)
                valuesRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var displayValues = new[] { $"{przychodUzyty:N0}", $"{data.Stan:N0}", $"{data.Zamowienia:N0}", $"{data.Bilans:N0}" };
            var operators = new[] { "", "+", "-", "=" };

            for (int i = 0; i < 4; i++)
            {
                if (i > 0)
                {
                    var opText = new TextBlock
                    {
                        Text = operators[i],
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(5, 0, 5, 0)
                    };
                    var opContainer = new StackPanel { Orientation = Orientation.Horizontal };
                    opContainer.Children.Add(opText);

                    var valueText = new TextBlock
                    {
                        Text = displayValues[i],
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(valueColors[i]),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    opContainer.Children.Add(valueText);
                    Grid.SetColumn(opContainer, i);
                    valuesRow.Children.Add(opContainer);
                }
                else
                {
                    var valueText = new TextBlock
                    {
                        Text = displayValues[i],
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(valueColors[i]),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(10, 0, 10, 0)
                    };
                    Grid.SetColumn(valueText, i);
                    valuesRow.Children.Add(valueText);
                }
            }

            Grid.SetRow(valuesRow, 1);
            calculationGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            calculationGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainStack.Children.Add(calculationGrid);
            mainStack.Children.Add(valuesRow);

            // === LISTA ODBIORCÓW ===
            if (data.Odbiorcy.Any())
            {
                var separator = new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                    Margin = new Thickness(0, 10, 0, 10)
                };
                mainStack.Children.Add(separator);

                var odbiorcyHeader = new Grid { Margin = new Thickness(0, 0, 0, 5) };
                odbiorcyHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                odbiorcyHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var odbiorcaLabel = new TextBlock
                {
                    Text = "Odbiorca",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141))
                };
                Grid.SetColumn(odbiorcaLabel, 0);
                odbiorcyHeader.Children.Add(odbiorcaLabel);

                var iloscLabel = new TextBlock
                {
                    Text = "Ilość",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141))
                };
                Grid.SetColumn(iloscLabel, 1);
                odbiorcyHeader.Children.Add(iloscLabel);

                mainStack.Children.Add(odbiorcyHeader);

                // Lista odbiorców (max 5)
                foreach (var odbiorca in data.Odbiorcy.Take(5))
                {
                    var odbiorcaRow = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    odbiorcaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    odbiorcaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var nazwaText = new TextBlock
                    {
                        Text = $"[{odbiorca.NazwaOdbiorcy}]",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 250
                    };
                    Grid.SetColumn(nazwaText, 0);
                    odbiorcaRow.Children.Add(nazwaText);

                    var iloscText = new TextBlock
                    {
                        Text = $"{odbiorca.Ilosc:N0}",
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
                    };
                    Grid.SetColumn(iloscText, 1);
                    odbiorcaRow.Children.Add(iloscText);

                    mainStack.Children.Add(odbiorcaRow);
                }

                // Jeśli więcej niż 5 odbiorców
                if (data.Odbiorcy.Count > 5)
                {
                    var moreText = new TextBlock
                    {
                        Text = $"... i {data.Odbiorcy.Count - 5} więcej",
                        FontSize = 11,
                        FontStyle = FontStyles.Italic,
                        Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                        Margin = new Thickness(0, 5, 0, 0)
                    };
                    mainStack.Children.Add(moreText);
                }
            }

            card.Child = mainStack;
            return card;
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
