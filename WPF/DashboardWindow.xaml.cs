using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;

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
            public int OdbiorcyCount => Odbiorcy.Count; // Liczba odbiorców dla tabeli
        }

        // Zamówienie od odbiorcy
        private class OdbiorcaZamowienie
        {
            public int ZamowienieId { get; set; } // ID zamówienia do nawigacji
            public string NazwaOdbiorcy { get; set; } = "";
            public decimal Ilosc { get; set; }
        }

        // Lista danych produktów do DataGrid
        private List<ProductData> _productDataList = new();

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

        private Storyboard? _loadingStoryboard;

        private void ShowLoading()
        {
            loadingOverlay.Visibility = Visibility.Visible;

            // Animacja obracania
            _loadingStoryboard = new Storyboard();
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(1),
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(animation, txtLoadingIcon);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            _loadingStoryboard.Children.Add(animation);
            _loadingStoryboard.Begin();
        }

        private void HideLoading()
        {
            _loadingStoryboard?.Stop();
            loadingOverlay.Visibility = Visibility.Collapsed;
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
                ShowLoading();

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
                HideLoading();
                _isLoading = false;
            }
        }

        private void CreateProductCards(List<ProductData> productsData)
        {
            // Zapisz dane do listy dla DataGrid
            _productDataList = productsData;
            dgProducts.ItemsSource = _productDataList;

            icProducts.Items.Clear();

            int colorIndex = 0;
            foreach (var data in productsData)
            {
                var card = CreateProductCard(data, _cardColors[colorIndex % _cardColors.Length]);
                icProducts.Items.Add(card);
                colorIndex++;
            }
        }

        // Przełączanie widoku Karty/Tabela
        private void RbViewMode_Checked(object sender, RoutedEventArgs e)
        {
            if (svCards == null || dgProducts == null) return;

            if (rbViewCards?.IsChecked == true)
            {
                svCards.Visibility = Visibility.Visible;
                dgProducts.Visibility = Visibility.Collapsed;
            }
            else
            {
                svCards.Visibility = Visibility.Collapsed;
                dgProducts.Visibility = Visibility.Visible;
            }
        }

        // Podwójne kliknięcie na wiersz w tabeli - otwórz szczegóły produktu
        private void DgProducts_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgProducts.SelectedItem is ProductData data)
            {
                ShowProductDetails(data);
            }
        }

        // Pokazanie szczegółów produktu
        private void ShowProductDetails(ProductData data)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Produkt: [{data.Kod}] {data.Nazwa}");
            sb.AppendLine();
            sb.AppendLine($"Plan: {data.Plan:N0}");
            sb.AppendLine($"Fakt: {data.Fakt:N0}");
            sb.AppendLine($"Stan: {data.Stan:N0}");
            sb.AppendLine($"Zamówienia: {data.Zamowienia:N0}");
            sb.AppendLine($"Bilans: {data.Bilans:N0}");
            sb.AppendLine();
            sb.AppendLine($"Odbiorcy ({data.Odbiorcy.Count}):");
            foreach (var o in data.Odbiorcy)
            {
                sb.AppendLine($"  • {o.NazwaOdbiorcy}: {o.Ilosc:N0}");
            }

            MessageBox.Show(sb.ToString(), "Szczegóły produktu", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Okno szczegółów zamówienia odbiorcy
        private void ShowOdbiorcaOrderWindow(OdbiorcaZamowienie odbiorca, ProductData produkt)
        {
            var window = new Window
            {
                Title = $"Zamówienie - {odbiorca.NazwaOdbiorcy}",
                Width = 450,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(240, 242, 245)),
                ResizeMode = ResizeMode.NoResize
            };

            var mainPanel = new StackPanel { Margin = new Thickness(20) };

            // Nagłówek
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 15),
                Margin = new Thickness(0, 0, 0, 20)
            };
            var headerText = new TextBlock
            {
                Text = odbiorca.NazwaOdbiorcy,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            };
            header.Child = headerText;
            mainPanel.Children.Add(header);

            // Szczegóły
            var detailsPanel = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20)
            };
            detailsPanel.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                ShadowDepth = 2,
                Opacity = 0.1,
                BlurRadius = 10
            };

            var detailsStack = new StackPanel();

            // Produkt
            AddDetailRow(detailsStack, "Produkt:", $"[{produkt.Kod}]");
            AddDetailRow(detailsStack, "Nazwa:", produkt.Nazwa);

            // Separator
            detailsStack.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                Margin = new Thickness(0, 15, 0, 15)
            });

            // Ilość zamówiona
            var iloscPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            iloscPanel.Children.Add(new TextBlock
            {
                Text = "Ilość zamówiona:",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Width = 130
            });
            iloscPanel.Children.Add(new TextBlock
            {
                Text = $"{odbiorca.Ilosc:N0} kg",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219))
            });
            detailsStack.Children.Add(iloscPanel);

            // Procent udziału
            decimal sumaZamowien = produkt.Odbiorcy.Sum(o => o.Ilosc);
            decimal procent = sumaZamowien > 0 ? (odbiorca.Ilosc / sumaZamowien) * 100 : 0;
            var procentPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            procentPanel.Children.Add(new TextBlock
            {
                Text = "Udział w zamówieniach:",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Width = 130
            });
            procentPanel.Children.Add(new TextBlock
            {
                Text = $"{procent:N1}%",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96))
            });
            detailsStack.Children.Add(procentPanel);

            // Data
            AddDetailRow(detailsStack, "Data zamówienia:", _selectedDate.ToString("dd.MM.yyyy"));

            detailsPanel.Child = detailsStack;
            mainPanel.Children.Add(detailsPanel);

            // Przycisk zamknij
            var closeBtn = new Button
            {
                Content = "Zamknij",
                Padding = new Thickness(30, 10, 30, 10),
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            closeBtn.Click += (s, e) => window.Close();
            mainPanel.Children.Add(closeBtn);

            window.Content = mainPanel;
            window.ShowDialog();
        }

        private void AddDetailRow(StackPanel panel, string label, string value)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Width = 130
            });
            row.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 250
            });
            panel.Children.Add(row);
        }

        private Border CreateProductCard(ProductData data, Color headerColor)
        {
            // Stała wysokość karty - lista odbiorców jest scrollowalna
            double cardHeight = data.Odbiorcy.Any() ? 380 : 280;

            // Czy bilans jest problemowy (poza zakresem -1000 do 1000)?
            bool bilansProblem = data.Bilans < -1000 || data.Bilans > 1000;

            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(8),
                Width = 340,
                MinHeight = cardHeight,
                BorderBrush = bilansProblem
                    ? new SolidColorBrush(Color.FromRgb(231, 76, 60))  // Czerwona ramka - problem
                    : new SolidColorBrush(Color.FromRgb(44, 62, 80)),  // Normalna ramka
                BorderThickness = new Thickness(bilansProblem ? 3 : 2),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = data // Przechowuj dane produktu
            };

            // Kliknięcie na kartę - pokaż szczegóły
            card.MouseLeftButtonUp += (s, e) =>
            {
                if (s is Border b && b.Tag is ProductData pd)
                {
                    ShowProductDetails(pd);
                }
            };

            card.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                ShadowDepth = 2,
                Opacity = bilansProblem ? 0.3 : 0.15,
                BlurRadius = 10,
                Color = bilansProblem ? Color.FromRgb(231, 76, 60) : Colors.Black
            };

            var mainStack = new StackPanel { Margin = new Thickness(15, 12, 15, 12) };

            // === NAGŁÓWEK - nazwa produktu ===
            var titleText = new TextBlock
            {
                Text = $"[{data.Kod}]",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            mainStack.Children.Add(titleText);

            // === WYKRES PLAN/FAKT ===
            decimal maxBarValue = Math.Max(Math.Max(data.Plan, data.Fakt), Math.Max(data.Zamowienia, 1));
            double maxBarWidth = 280;

            // Pasek PLAN (przekreślony jeśli użyto fakt)
            var planBarContainer = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            planBarContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            planBarContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            double planWidth = data.Plan > 0 ? (double)(data.Plan / maxBarValue) * maxBarWidth : 5;
            var planBar = new Border
            {
                Height = 22,
                Width = Math.Max(planWidth, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(3),
                Background = data.UzytoFakt
                    ? new SolidColorBrush(Color.FromRgb(189, 195, 199)) // Szary gdy przekreślony
                    : new SolidColorBrush(Color.FromRgb(241, 196, 15))  // Żółty gdy aktywny
            };

            // Tekst na pasku plan
            var planTextBlock = new TextBlock
            {
                Text = $"plan {data.Plan:N0}",
                FontSize = 11,
                FontWeight = data.UzytoFakt ? FontWeights.Normal : FontWeights.Bold,
                Foreground = data.UzytoFakt
                    ? new SolidColorBrush(Color.FromRgb(127, 140, 141))
                    : new SolidColorBrush(Color.FromRgb(156, 127, 0)),  // Ciemny żółty tekst
                TextDecorations = data.UzytoFakt ? TextDecorations.Strikethrough : null,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            };
            Grid.SetColumn(planTextBlock, 1);

            planBarContainer.Children.Add(planBar);
            planBarContainer.Children.Add(planTextBlock);
            mainStack.Children.Add(planBarContainer);

            // Pasek FAKT (jeśli > 0)
            if (data.Fakt > 0)
            {
                var faktBarContainer = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                faktBarContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                faktBarContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                double faktWidth = (double)(data.Fakt / maxBarValue) * maxBarWidth;
                var faktBar = new Border
                {
                    Height = 22,
                    Width = Math.Max(faktWidth, 5),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromRgb(241, 196, 15)) // Żółty
                };

                var faktTextBlock = new TextBlock
                {
                    Text = $"fakt {data.Fakt:N0}",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(156, 127, 0)), // Ciemny żółty
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 0, 0)
                };
                Grid.SetColumn(faktTextBlock, 1);

                faktBarContainer.Children.Add(faktBar);
                faktBarContainer.Children.Add(faktTextBlock);
                mainStack.Children.Add(faktBarContainer);
            }

            // Pasek ZAMÓWIENIA
            var zamBarContainer = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            zamBarContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            zamBarContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            double zamWidth = data.Zamowienia > 0 ? (double)(data.Zamowienia / maxBarValue) * maxBarWidth : 5;
            var zamBar = new Border
            {
                Height = 22,
                Width = Math.Max(zamWidth, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)) // Niebieski
            };

            var zamTextBlock = new TextBlock
            {
                Text = $"zam {data.Zamowienia:N0}",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(41, 128, 185)), // Ciemny niebieski
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            };
            Grid.SetColumn(zamTextBlock, 1);

            zamBarContainer.Children.Add(zamBar);
            zamBarContainer.Children.Add(zamTextBlock);
            mainStack.Children.Add(zamBarContainer);

            // === OBLICZENIE BILANSU ===
            var calculationPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 15, 0, 0)
            };

            decimal przychodUzyty = data.UzytoFakt ? data.Fakt : data.Plan;
            Color przychodColor = Color.FromRgb(156, 127, 0); // Ciemny żółty dla plan/fakt
            // Bilans zielony gdy -1000 do 1000, czerwony w przeciwnym razie
            Color bilansColor = (data.Bilans >= -1000 && data.Bilans <= 1000)
                ? Color.FromRgb(39, 174, 96)   // Zielony - OK
                : Color.FromRgb(231, 76, 60);  // Czerwony - problem

            // Plan/Fakt + Stan - Zam. = Bilans
            AddCalculationItem(calculationPanel, data.UzytoFakt ? "Fakt" : "Plan", $"{przychodUzyty:N0}", przychodColor);
            AddCalculationOperator(calculationPanel, "+");
            AddCalculationItem(calculationPanel, "Stan", $"{data.Stan:N0}", Color.FromRgb(52, 73, 94));
            AddCalculationOperator(calculationPanel, "-");
            AddCalculationItem(calculationPanel, "Zam.", $"{data.Zamowienia:N0}", Color.FromRgb(41, 128, 185)); // Niebieski
            AddCalculationOperator(calculationPanel, "=");
            AddCalculationItem(calculationPanel, "Bilans", $"{data.Bilans:N0}", bilansColor, true);

            mainStack.Children.Add(calculationPanel);

            // === LISTA ODBIORCÓW (scrollowalna z procentami) ===
            if (data.Odbiorcy.Any())
            {
                var separator = new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    Margin = new Thickness(0, 12, 0, 5)
                };
                mainStack.Children.Add(separator);

                // Nagłówek sekcji
                var headerRow = new Grid { Margin = new Thickness(0, 0, 0, 3) };
                headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });

                var headerNazwa = new TextBlock
                {
                    Text = $"Odbiorcy ({data.Odbiorcy.Count})",
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120))
                };
                Grid.SetColumn(headerNazwa, 0);
                headerRow.Children.Add(headerNazwa);

                var headerIlosc = new TextBlock
                {
                    Text = "Ilość",
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(headerIlosc, 1);
                headerRow.Children.Add(headerIlosc);

                var headerProcent = new TextBlock
                {
                    Text = "%",
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(headerProcent, 2);
                headerRow.Children.Add(headerProcent);

                mainStack.Children.Add(headerRow);

                // Scrollowalna lista odbiorców
                var scrollViewer = new ScrollViewer
                {
                    MaxHeight = 120,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                };

                var odbiorcyStack = new StackPanel();

                // Suma zamówień dla obliczenia procentów
                decimal sumaZamowien = data.Odbiorcy.Sum(o => o.Ilosc);

                foreach (var odbiorca in data.Odbiorcy)
                {
                    var odbiorcaBorder = new Border
                    {
                        Margin = new Thickness(0, 1, 0, 1),
                        Padding = new Thickness(4, 3, 4, 3),
                        CornerRadius = new CornerRadius(3),
                        Background = new SolidColorBrush(Colors.Transparent),
                        Cursor = System.Windows.Input.Cursors.Hand
                    };

                    // Hover effect
                    odbiorcaBorder.MouseEnter += (s, e) =>
                    {
                        if (s is Border b)
                            b.Background = new SolidColorBrush(Color.FromRgb(236, 240, 241));
                    };
                    odbiorcaBorder.MouseLeave += (s, e) =>
                    {
                        if (s is Border b)
                            b.Background = new SolidColorBrush(Colors.Transparent);
                    };

                    // Kliknięcie na odbiorcę - otwórz okno zamówienia
                    var odbInfo = odbiorca; // capture
                    var prodInfo = data;
                    odbiorcaBorder.MouseLeftButtonUp += (s, e) =>
                    {
                        e.Handled = true; // Zatrzymaj propagację do karty
                        ShowOdbiorcaOrderWindow(odbInfo, prodInfo);
                    };

                    var odbiorcaRow = new Grid();
                    odbiorcaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    odbiorcaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                    odbiorcaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });

                    var nazwaText = new TextBlock
                    {
                        Text = odbiorca.NazwaOdbiorcy,
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    Grid.SetColumn(nazwaText, 0);
                    odbiorcaRow.Children.Add(nazwaText);

                    var iloscText = new TextBlock
                    {
                        Text = $"{odbiorca.Ilosc:N0}",
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    Grid.SetColumn(iloscText, 1);
                    odbiorcaRow.Children.Add(iloscText);

                    // Procent udziału
                    decimal procent = sumaZamowien > 0 ? (odbiorca.Ilosc / sumaZamowien) * 100 : 0;
                    var procentText = new TextBlock
                    {
                        Text = $"{procent:N0}%",
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    Grid.SetColumn(procentText, 2);
                    odbiorcaRow.Children.Add(procentText);

                    odbiorcaBorder.Child = odbiorcaRow;
                    odbiorcyStack.Children.Add(odbiorcaBorder);
                }

                scrollViewer.Content = odbiorcyStack;
                mainStack.Children.Add(scrollViewer);
            }

            card.Child = mainStack;
            return card;
        }

        private void AddCalculationItem(StackPanel panel, string label, string value, Color color, bool isBold = false)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 3, 0) };

            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stack.Children.Add(labelText);

            var valueText = new TextBlock
            {
                Text = value,
                FontSize = isBold ? 13 : 12,
                FontWeight = isBold ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stack.Children.Add(valueText);

            panel.Children.Add(stack);
        }

        private void AddCalculationOperator(StackPanel panel, string op)
        {
            var opText = new TextBlock
            {
                Text = op,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(4, 0, 4, 2)
            };
            panel.Children.Add(opText);
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

        // Odświeżenie danych
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadDataAsync();
        }

        // Eksport do Excel (CSV)
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_productDataList == null || !_productDataList.Any())
            {
                MessageBox.Show("Brak danych do eksportu.", "Eksport", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Plik CSV (*.csv)|*.csv|Plik Excel (*.xlsx)|*.xlsx",
                DefaultExt = ".csv",
                FileName = $"Dashboard_{_selectedDate:yyyy-MM-dd}"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8);

                    // Nagłówki
                    writer.WriteLine("Kod;Nazwa;Plan;Fakt;Stan;Zamówienia;Bilans;Liczba odbiorców;Odbiorcy");

                    // Dane
                    foreach (var p in _productDataList)
                    {
                        var odbiorcy = string.Join(", ", p.Odbiorcy.Select(o => $"{o.NazwaOdbiorcy}:{o.Ilosc:N0}"));
                        writer.WriteLine($"{p.Kod};{p.Nazwa};{p.Plan:N0};{p.Fakt:N0};{p.Stan:N0};{p.Zamowienia:N0};{p.Bilans:N0};{p.OdbiorcyCount};{odbiorcy}");
                    }

                    MessageBox.Show($"Dane wyeksportowane do:\n{dialog.FileName}", "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Screenshot do udostępniania
        private void BtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ukryj panel boczny na czas screenshota
                var panelWasVisible = sidePanel.Visibility == Visibility.Visible;
                if (panelWasVisible)
                    sidePanel.Visibility = Visibility.Collapsed;

                // Poczekaj na odświeżenie layoutu
                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                // Zrób screenshot głównej zawartości
                var mainGrid = (Grid)Content;
                var mainContent = mainGrid.Children[0] as Grid;

                if (mainContent == null) return;

                var bounds = VisualTreeHelper.GetDescendantBounds(mainContent);
                var renderTarget = new RenderTargetBitmap(
                    (int)mainContent.ActualWidth,
                    (int)mainContent.ActualHeight,
                    96, 96, PixelFormats.Pbgra32);

                var visual = new DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    var brush = new VisualBrush(mainContent);
                    context.DrawRectangle(brush, null, new Rect(new Point(), new Size(mainContent.ActualWidth, mainContent.ActualHeight)));
                }
                renderTarget.Render(visual);

                // Przywróć panel
                if (panelWasVisible)
                    sidePanel.Visibility = Visibility.Visible;

                // Zapisz do pliku
                var dialog = new SaveFileDialog
                {
                    Filter = "Obraz PNG (*.png)|*.png",
                    DefaultExt = ".png",
                    FileName = $"Dashboard_{_selectedDate:yyyy-MM-dd}_{DateTime.Now:HHmm}"
                };

                if (dialog.ShowDialog() == true)
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(renderTarget));
                    using var stream = File.Create(dialog.FileName);
                    encoder.Save(stream);

                    MessageBox.Show($"Screenshot zapisany:\n{dialog.FileName}", "Screenshot", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd tworzenia screenshota: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
