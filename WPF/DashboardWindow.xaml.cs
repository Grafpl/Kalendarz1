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
        private DateTime _selectedDateDo; // Data końcowa dla zakresu
        private bool _zakresDat; // true = tryb zakresu dat
        private bool _isLoading;
        private bool _isPanelOpen;
        private bool _uzywajWydan; // true = wydania, false = zamówienia
        private bool _pokazWydaniaBezZamowien; // true = pokaż odbiorców z wydaniami bez zamówień

        // Progi kolorów paska postępu (w procentach)
        private int _progZielony = 80;  // >= 80% = zielony
        private int _progZolty = 50;    // >= 50% = żółty, poniżej = czerwony

        // Lista wszystkich dostępnych produktów z TW
        private List<ProductItem> _allProducts = new();
        // Produkty wybrane do dashboardu
        private List<int> _selectedProductIds = new();
        // Zapisane widoki
        private List<DashboardView> _savedViews = new();

        // Cache zdjęć produktów: TowarId -> ImageSource
        private Dictionary<int, BitmapImage?> _productImages = new();

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
            public int KlientId { get; set; } // ID klienta dla wydań
            public string NazwaOdbiorcy { get; set; } = "";
            public decimal Zamowione { get; set; } // ile zamówione
            public decimal Wydane { get; set; } // ile wydane
            public decimal ProcentUdzial { get; set; } // % udziału w zamówieniach
        }

        // Dane historyczne klienta dla produktu
        private class HistoricalClientData
        {
            public int KlientId { get; set; }
            public string NazwaKlienta { get; set; } = "";
            public decimal SumaZamowien { get; set; } // Suma zamówień w okresie
            public int LiczbaZamowien { get; set; } // Ilość zamówień
            public DateTime OstatnieZamowienie { get; set; } // Data ostatniego zamówienia
            public DateTime PierwszeZamowienie { get; set; } // Data pierwszego zamówienia
            public decimal SredniaZamowienie { get; set; } // Średnia wielkość zamówienia
            public int DniOdOstatniego { get; set; } // Dni od ostatniego zamówienia
        }

        // Trend miesięczny
        private class MonthlyTrend
        {
            public int Rok { get; set; }
            public int Miesiac { get; set; }
            public decimal Suma { get; set; }
            public int LiczbaZamowien { get; set; }
            public string Label => $"{Rok}-{Miesiac:D2}";
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
            public bool IsDomyslny { get; set; }
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

        private bool _openPanelJolaOnStart = false;

        public DashboardWindow(string connLibra, string connHandel, DateTime? initialDate = null, bool openPanelJola = false)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _connLibra = connLibra;
            _connHandel = connHandel;
            _selectedDate = initialDate ?? GetDefaultDate();
            _openPanelJolaOnStart = openPanelJola;

            // Jeśli uruchamiamy bezpośrednio Panel Pani Jola - ukryj główne okno całkowicie
            if (_openPanelJolaOnStart)
            {
                this.WindowState = WindowState.Normal;
                this.ShowInTaskbar = false;
                this.Opacity = 0; // Całkowicie przezroczyste - niewidoczne
                this.Width = 1;
                this.Height = 1;
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = -10000; // Poza ekranem
                this.Top = -10000;
            }

            InitializeAsync();
        }

        // Wybierz domyślną datę - następny dzień roboczy lub dziś jeśli przed 14:00
        private static DateTime GetDefaultDate()
        {
            var now = DateTime.Now;
            var today = now.Date;

            // Jeśli przed 14:00 - pokaż dziś
            if (now.Hour < 14)
                return today;

            // Po 14:00 - pokaż następny dzień roboczy
            var nextDay = today.AddDays(1);

            // Pomiń weekendy
            while (nextDay.DayOfWeek == DayOfWeek.Saturday || nextDay.DayOfWeek == DayOfWeek.Sunday)
                nextDay = nextDay.AddDays(1);

            return nextDay;
        }

        private async void InitializeAsync()
        {
            dpData.SelectedDate = _selectedDate;
            txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd dddd}";

            await LoadProductsFromTWAsync();
            await LoadSavedViewsAsync();
            await LoadDataAsync();

            // Jeśli uruchomiono w trybie Panel Pani Jola - otwórz go automatycznie
            if (_openPanelJolaOnStart && _productDataList.Any())
            {
                ShowSimplifiedPanelJola(_productDataList.First(), 0);
            }
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

                const string sql = @"SELECT Id, Nazwa, ProduktyIds, ISNULL(IsDomyslny, 0) as IsDomyslny FROM dbo.DashboardWidoki ORDER BY Nazwa";

                await using var cmd = new SqlCommand(sql, cn);
                await using var rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    var view = new DashboardView
                    {
                        Id = rdr.GetInt32(0),
                        Nazwa = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                        ProductIds = new List<int>(),
                        IsDomyslny = rdr.GetBoolean(3)
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
                int defaultIndex = 0;
                DashboardView? defaultView = null;
                int idx = 1;
                foreach (var view in _savedViews)
                {
                    var displayName = view.IsDomyslny ? $"⭐ {view.Nazwa}" : view.Nazwa;
                    cmbWidok.Items.Add(new ComboBoxItem { Content = displayName, Tag = view });
                    if (view.IsDomyslny)
                    {
                        defaultIndex = idx;
                        defaultView = view;
                    }
                    idx++;
                }
                cmbWidok.SelectedIndex = defaultIndex;

                // Załaduj produkty z domyślnego widoku (bo CmbWidok_SelectionChanged nie zadziała przed IsLoaded)
                if (defaultView != null)
                {
                    foreach (var p in _allProducts)
                        p.IsSelected = defaultView.ProductIds.Contains(p.Id);
                    _selectedProductIds = defaultView.ProductIds.ToList();
                    UpdateSelectedCount();
                }
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
                            DataUtworzenia DATETIME DEFAULT GETDATE(),
                            IsDomyslny BIT DEFAULT 0
                        )
                    END
                    ELSE
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DashboardWidoki') AND name = 'IsDomyslny')
                        BEGIN
                            ALTER TABLE dbo.DashboardWidoki ADD IsDomyslny BIT DEFAULT 0
                        END
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

                // Załaduj zdjęcia produktów (jeśli jeszcze nie załadowane)
                if (_productImages.Count == 0)
                {
                    await LoadProductImagesAsync();
                }

                _uzywajWydan = rbBilansWydania?.IsChecked == true;

                // Obsługa zakresu dat
                DateTime dayStart = _selectedDate.Date;
                DateTime dayEnd = _zakresDat ? _selectedDateDo.Date : dayStart;
                DateTime day = dayStart; // Dla kompatybilności z istniejącym kodem

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

                // 3. Pobierz harmonogram dostaw (dla obliczenia PLAN) - sumuj dla zakresu dat
                decimal totalMassDek = 0m;
                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT WagaDek, SztukiDek FROM dbo.HarmonogramDostaw
                                         WHERE DataOdbioru BETWEEN @DayStart AND @DayEnd AND Bufor = 'Potwierdzony'";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@DayStart", dayStart);
                    cmd.Parameters.AddWithValue("@DayEnd", dayEnd);
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

                // 4. Pobierz FAKT - przychody tuszki (sPWU) - sumuj dla zakresu dat
                var faktTuszka = new Dictionary<int, decimal>();
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                                         FROM [HANDEL].[HM].[MZ] MZ
                                         JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                                         WHERE MG.seria = 'sPWU' AND MG.aktywny=1 AND MG.data BETWEEN @DayStart AND @DayEnd
                                         GROUP BY MZ.idtw";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@DayStart", dayStart);
                    cmd.Parameters.AddWithValue("@DayEnd", dayEnd);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        decimal qty = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                        faktTuszka[id] = qty;
                    }
                }

                // 5. Pobierz FAKT - przychody elementów (sPWP, PWP) - sumuj dla zakresu dat
                var faktElementy = new Dictionary<int, decimal>();
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                                         FROM [HANDEL].[HM].[MZ] MZ
                                         JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                                         WHERE MG.seria IN ('sPWP', 'PWP') AND MG.aktywny=1 AND MG.data BETWEEN @DayStart AND @DayEnd
                                         GROUP BY MZ.idtw";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@DayStart", dayStart);
                    cmd.Parameters.AddWithValue("@DayEnd", dayEnd);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        decimal qty = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                        faktElementy[id] = qty;
                    }
                }

                // 6. Pobierz ZAMÓWIENIA dla wybranego dnia/zakresu dat
                var orderSum = new Dictionary<int, decimal>();
                var orderIds = new List<int>();

                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT Id FROM dbo.ZamowieniaMieso
                                         WHERE DataUboju BETWEEN @DayStart AND @DayEnd AND Status <> 'Anulowane'";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@DayStart", dayStart);
                    cmd.Parameters.AddWithValue("@DayEnd", dayEnd);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        orderIds.Add(rdr.GetInt32(0));
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[Dashboard] Data: {dayStart:yyyy-MM-dd} - {dayEnd:yyyy-MM-dd}, Znaleziono zamówień: {orderIds.Count}");

                // === ZAAWANSOWANA DIAGNOSTYKA ===
                if (orderIds.Any())
                {
                    try
                    {
                        await using var cnDiag = new SqlConnection(_connLibra);
                        await cnDiag.OpenAsync();

                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine("\n=== DIAGNOSTYKA ODBIORCÓW ===");
                        sb.AppendLine($"Data: {day:yyyy-MM-dd}");
                        sb.AppendLine($"Zamówienia IDs: {string.Join(",", orderIds)}");
                        sb.AppendLine($"Wybrane produkty IDs (TW.ID): {string.Join(",", _selectedProductIds)}");
                        sb.AppendLine();

                        // 1. Sprawdź strukturę tabeli ZamowieniaMiesoTowar
                        var schemaSql = @"SELECT COLUMN_NAME, DATA_TYPE
                                          FROM INFORMATION_SCHEMA.COLUMNS
                                          WHERE TABLE_NAME = 'ZamowieniaMiesoTowar'
                                          ORDER BY ORDINAL_POSITION";
                        await using (var cmdSchema = new SqlCommand(schemaSql, cnDiag))
                        await using (var rdrSchema = await cmdSchema.ExecuteReaderAsync())
                        {
                            sb.AppendLine("--- Struktura tabeli ZamowieniaMiesoTowar:");
                            while (await rdrSchema.ReadAsync())
                            {
                                sb.AppendLine($"    {rdrSchema.GetString(0)} ({rdrSchema.GetString(1)})");
                            }
                        }
                        sb.AppendLine();

                        // 2. Pokaż jakie KodTowaru są w zamówieniach
                        var kodySql = $@"SELECT t.KodTowaru, SUM(t.Ilosc) as SumaIlosc, COUNT(*) as LiczbaZamowien
                                         FROM ZamowieniaMiesoTowar t
                                         WHERE t.ZamowienieId IN ({string.Join(",", orderIds)})
                                         GROUP BY t.KodTowaru
                                         ORDER BY SUM(t.Ilosc) DESC";
                        await using (var cmdKody = new SqlCommand(kodySql, cnDiag))
                        await using (var rdrKody = await cmdKody.ExecuteReaderAsync())
                        {
                            sb.AppendLine("--- KodTowaru w zamówieniach:");
                            int cnt = 0;
                            while (await rdrKody.ReadAsync())
                            {
                                var kodTowaru = rdrKody.GetValue(0);
                                var suma = rdrKody.GetValue(1);
                                var liczba = rdrKody.GetValue(2);
                                sb.AppendLine($"    KodTowaru={kodTowaru} (typ:{kodTowaru?.GetType().Name}), Suma={suma}, Zamówień={liczba}");
                                cnt++;
                            }
                            sb.AppendLine($"    Łącznie różnych KodTowaru: {cnt}");
                        }
                        sb.AppendLine();

                        // 3. Sprawdź wybrane produkty z TW
                        sb.AppendLine("--- Wybrane produkty z TW (HANDEL):");
                        await using var cnHandel = new SqlConnection(_connHandel);
                        await cnHandel.OpenAsync();
                        var twSql = $@"SELECT ID, kod, nazwa FROM [HANDEL].[HM].[TW] WHERE ID IN ({string.Join(",", _selectedProductIds)})";
                        await using (var cmdTw = new SqlCommand(twSql, cnHandel))
                        await using (var rdrTw = await cmdTw.ExecuteReaderAsync())
                        {
                            while (await rdrTw.ReadAsync())
                            {
                                sb.AppendLine($"    ID={rdrTw.GetInt32(0)}, Kod='{rdrTw.GetString(1)}', Nazwa='{rdrTw.GetString(2)}'");
                            }
                        }
                        sb.AppendLine();

                        // 4. Sprawdź czy KodTowaru może odpowiadać kolumnie 'kod' z TW
                        sb.AppendLine("--- Próba dopasowania po kodzie (tekst):");
                        var matchSql = $@"SELECT t.KodTowaru, tw.ID, tw.kod, tw.nazwa, SUM(t.Ilosc) as Suma
                                          FROM ZamowieniaMiesoTowar t
                                          INNER JOIN [HANDEL].[HM].[TW] tw ON CAST(t.KodTowaru as NVARCHAR) = tw.kod
                                          WHERE t.ZamowienieId IN ({string.Join(",", orderIds)})
                                          GROUP BY t.KodTowaru, tw.ID, tw.kod, tw.nazwa";
                        try
                        {
                            await using (var cmdMatch = new SqlCommand(matchSql, cnDiag))
                            await using (var rdrMatch = await cmdMatch.ExecuteReaderAsync())
                            {
                                int matches = 0;
                                while (await rdrMatch.ReadAsync())
                                {
                                    sb.AppendLine($"    DOPASOWANIE: KodTowaru={rdrMatch.GetValue(0)} -> TW.ID={rdrMatch.GetValue(1)}, Kod='{rdrMatch.GetValue(2)}', Suma={rdrMatch.GetValue(4)}");
                                    matches++;
                                }
                                sb.AppendLine($"    Dopasowań: {matches}");
                            }
                        }
                        catch (Exception exMatch)
                        {
                            sb.AppendLine($"    Błąd cross-database: {exMatch.Message}");
                            sb.AppendLine("    (Może wymagać linked server lub innego podejścia)");
                        }

                        sb.AppendLine("\n=== KONIEC DIAGNOSTYKI ===");
                        System.Diagnostics.Debug.WriteLine(sb.ToString());
                    }
                    catch (Exception exDiag)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Dashboard] Błąd diagnostyki: {exDiag.Message}");
                    }
                }

                // Słownik: productId -> lista (KlientId, nazwa, ilość zamówiona)
                var orderDetails = new Dictionary<int, List<(int KlientId, string Nazwa, decimal Ilosc)>>();

                // Pobierz słownik kontrahentów z HANDEL (potrzebny dla zamówień i wydań bez zamówień)
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

                    // Pobierz szczegóły zamówień: KodTowaru, KlientId, Ilosc
                    await using (var cn = new SqlConnection(_connLibra))
                    {
                        await cn.OpenAsync();
                        var sql = $@"SELECT t.KodTowaru, z.KlientId, SUM(t.Ilosc) as Ilosc
                                     FROM [dbo].[ZamowieniaMiesoTowar] t
                                     INNER JOIN [dbo].[ZamowieniaMieso] z ON t.ZamowienieId = z.Id
                                     WHERE t.ZamowienieId IN ({string.Join(",", orderIds)})
                                     GROUP BY t.KodTowaru, z.KlientId
                                     ORDER BY t.KodTowaru, SUM(t.Ilosc) DESC";
                        await using var cmd = new SqlCommand(sql, cn);
                        await using var rdr = await cmd.ExecuteReaderAsync();
                        while (await rdr.ReadAsync())
                        {
                            int productId = rdr.GetInt32(0);
                            int klientId = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                            decimal ilosc = rdr.IsDBNull(2) ? 0m : rdr.GetDecimal(2);

                            // Pobierz nazwę odbiorcy ze słownika kontrahentów
                            string odbiorcaNazwa = kontrahenci.TryGetValue(klientId, out var nazwa) ? nazwa : $"Nieznany ({klientId})";

                            if (!orderDetails.ContainsKey(productId))
                                orderDetails[productId] = new List<(int KlientId, string Nazwa, decimal Ilosc)>();
                            orderDetails[productId].Add((klientId, odbiorcaNazwa, ilosc));
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[Dashboard] Odbiorcy dla produktów: {orderDetails.Count}, Klucze: {string.Join(",", orderDetails.Keys.Take(10))}");
                    System.Diagnostics.Debug.WriteLine($"[Dashboard] Wybrane produkty: {string.Join(",", _selectedProductIds.Take(10))}");
                }

                // 7. Pobierz WYDANIA (WZ) - per produkt - sumuj dla zakresu dat
                var wydaniaSum = new Dictionary<int, decimal>();
                // Wydania per klient per produkt: (produktId, klientId) -> ilość
                var wydaniaPerKlientProdukt = new Dictionary<(int produktId, int klientId), decimal>();
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT MZ.idtw, MG.khid, SUM(ABS(MZ.ilosc))
                                         FROM [HANDEL].[HM].[MZ] MZ
                                         JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                                         WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 AND MG.data BETWEEN @DayStart AND @DayEnd
                                         GROUP BY MZ.idtw, MG.khid";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@DayStart", dayStart);
                    cmd.Parameters.AddWithValue("@DayEnd", dayEnd);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int produktId = rdr.GetInt32(0);
                        int klientId = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                        decimal qty = rdr.IsDBNull(2) ? 0m : Convert.ToDecimal(rdr.GetValue(2));

                        // Suma per produkt
                        if (!wydaniaSum.ContainsKey(produktId))
                            wydaniaSum[produktId] = 0m;
                        wydaniaSum[produktId] += qty;

                        // Per klient per produkt
                        wydaniaPerKlientProdukt[(produktId, klientId)] = qty;
                    }
                }

                // 8. Pobierz STANY MAGAZYNOWE (użyj pierwszego dnia zakresu jako stan początkowy)
                var stanyMag = new Dictionary<int, decimal>();
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    const string sql = @"SELECT ProduktId, Stan FROM dbo.StanyMagazynowe WHERE Data = @Data";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Data", dayStart);
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

                // 10. Załaduj zapisaną kolejność produktów i posortuj
                var savedOrder = await LoadProductOrderAsync();
                var orderedProductIds = _selectedProductIds
                    .OrderBy(id => savedOrder.TryGetValue(id, out var pos) ? pos : int.MaxValue)
                    .ThenBy(id => id) // Dodatkowe sortowanie po ID dla produktów bez zapisanej kolejności
                    .ToList();

                // 11. Oblicz dane dla wybranych produktów (logika jak w MainWindow)
                var productsData = new List<ProductData>();
                foreach (var productId in orderedProductIds)
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
                    decimal odejmij = _uzywajWydan ? wyd : zam;

                    // Oblicz bilans - użyj faktycznego jeśli > 0, w przeciwnym razie planowany
                    decimal przychodDoUzycia = fakt > 0 ? fakt : plan;
                    uzytoFakt = fakt > 0;
                    decimal bilans = przychodDoUzycia + stan - odejmij;

                    // Pobierz listę odbiorców dla tego produktu
                    var odbiorcy = new List<OdbiorcaZamowienie>();
                    var klienciZZamowieniami = new HashSet<int>();

                    if (orderDetails.TryGetValue(productId, out var details))
                    {
                        decimal sumaZamowien = details.Sum(d => d.Ilosc);
                        odbiorcy = details.Select(d => new OdbiorcaZamowienie
                        {
                            KlientId = d.KlientId,
                            NazwaOdbiorcy = d.Nazwa,
                            Zamowione = d.Ilosc,
                            Wydane = wydaniaPerKlientProdukt.TryGetValue((productId, d.KlientId), out var wyd) ? wyd : 0m,
                            ProcentUdzial = sumaZamowien > 0 ? (d.Ilosc / sumaZamowien) * 100m : 0m
                        }).ToList();

                        foreach (var d in details)
                            klienciZZamowieniami.Add(d.KlientId);
                    }

                    // Dodaj klientów z wydaniami bez zamówień (jeśli zaznaczono checkbox)
                    if (_pokazWydaniaBezZamowien)
                    {
                        var klienciZWydaniami = wydaniaPerKlientProdukt
                            .Where(kvp => kvp.Key.produktId == productId && !klienciZZamowieniami.Contains(kvp.Key.klientId))
                            .ToList();

                        foreach (var kvp in klienciZWydaniami)
                        {
                            string nazwaKlienta = kontrahenci.TryGetValue(kvp.Key.klientId, out var nazwa)
                                ? nazwa
                                : $"Nieznany ({kvp.Key.klientId})";

                            odbiorcy.Add(new OdbiorcaZamowienie
                            {
                                KlientId = kvp.Key.klientId,
                                NazwaOdbiorcy = nazwaKlienta,
                                Zamowione = 0m,
                                Wydane = kvp.Value,
                                ProcentUdzial = 0m
                            });
                        }
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
                sb.AppendLine($"  • {o.NazwaOdbiorcy}: zam={o.Zamowione:N0}, wyd={o.Wydane:N0}");
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
                Padding = new Thickness(20, 15, 20, 15),
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
                Text = $"{odbiorca.Zamowione:N0} kg",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219))
            });
            detailsStack.Children.Add(iloscPanel);

            // Wydane
            var wydanePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            wydanePanel.Children.Add(new TextBlock
            {
                Text = "Wydane:",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Width = 130
            });
            wydanePanel.Children.Add(new TextBlock
            {
                Text = odbiorca.Wydane > 0 ? $"{odbiorca.Wydane:N0} kg" : "-",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = odbiorca.Wydane > 0
                    ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
                    : new SolidColorBrush(Color.FromRgb(180, 180, 180))
            });
            detailsStack.Children.Add(wydanePanel);

            // Procent udziału
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
                Text = $"{odbiorca.ProcentUdzial:N1}%",
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
            double cardHeight = data.Odbiorcy.Any() ? 550 : 300;

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

            // Menu kontekstowe (prawy przycisk myszy)
            var contextMenu = new ContextMenu();

            var menuAddPhoto = new MenuItem { Header = "📷 Dodaj/zmień zdjęcie" };
            var dataForMenu = data;
            menuAddPhoto.Click += (s, e) => ImportProductImage(dataForMenu.Id, dataForMenu.Kod);
            contextMenu.Items.Add(menuAddPhoto);

            var menuDeletePhoto = new MenuItem { Header = "🗑 Usuń zdjęcie" };
            menuDeletePhoto.Click += async (s, e) => await DeleteProductImageWithRefresh(dataForMenu.Id);
            contextMenu.Items.Add(menuDeletePhoto);

            contextMenu.Items.Add(new Separator());

            var menuExpand = new MenuItem { Header = "🔍 Powiększ kartę (szczegóły)" };
            menuExpand.Click += (s, e) => ShowExpandedProductCard(dataForMenu);
            contextMenu.Items.Add(menuExpand);

            var menuPotencjalni = new MenuItem { Header = "👥 Potencjalni odbiorcy" };
            menuPotencjalni.Click += (s, e) =>
            {
                var okno = new PotencjalniOdbiorcy(
                    _connHandel,
                    dataForMenu.Id,
                    dataForMenu.Kod,
                    dataForMenu.Plan,
                    dataForMenu.Fakt,
                    dataForMenu.Zamowienia,
                    dataForMenu.Bilans,
                    _selectedDate);
                okno.Show();
            };
            contextMenu.Items.Add(menuPotencjalni);

            contextMenu.Items.Add(new Separator());

            var menuJola = new MenuItem { Header = "👩‍🦳 Panel Pani Joli (uproszczony)" };
            menuJola.Click += (s, e) => ShowSimplifiedPanelJola(dataForMenu);
            contextMenu.Items.Add(menuJola);

            card.ContextMenu = contextMenu;

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

            // === NAGŁÓWEK - zdjęcie + nazwa produktu ===
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Zdjęcie
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Nazwa

            // Miniaturka zdjęcia
            var productImage = GetProductImage(data.Id);
            var imageBorder = new Border
            {
                Width = 65,
                Height = 65,
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 12, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = productImage != null ? "Kliknij aby zmienić zdjęcie" : "Kliknij aby dodać zdjęcie"
            };

            if (productImage != null)
            {
                var img = new System.Windows.Controls.Image
                {
                    Source = productImage,
                    Width = 63,
                    Height = 63,
                    Stretch = Stretch.Uniform
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                imageBorder.Child = img;
            }
            else
            {
                imageBorder.Background = new SolidColorBrush(Color.FromRgb(236, 240, 241));
                imageBorder.Child = new TextBlock
                {
                    Text = "📷",
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166))
                };
            }

            // Kliknięcie na zdjęcie - import
            var dataCapture = data;
            imageBorder.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                ImportProductImage(dataCapture.Id, dataCapture.Kod);
            };

            Grid.SetColumn(imageBorder, 0);
            headerGrid.Children.Add(imageBorder);

            // Nazwa produktu
            var titleText = new TextBlock
            {
                Text = $"[{data.Kod}]",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(titleText, 1);
            headerGrid.Children.Add(titleText);

            mainStack.Children.Add(headerGrid);

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

            // === PASEK POSTĘPU: ZAMÓWIENIA vs PLAN/FAKT (z metą) ===
            decimal zamWydValue = _uzywajWydan ? data.Wydania : data.Zamowienia;
            string zamWydLabel = _uzywajWydan ? "Wyd" : "Zam";
            decimal goalValue = data.UzytoFakt ? data.Fakt : data.Plan; // Meta = fakt jeśli dostępny, inaczej plan
            string goalLabel = data.UzytoFakt ? "FAKT" : "PLAN";

            // Kontener na nagłówki: "Zam X kg" po lewej, "PLAN/FAKT X kg" po prawej
            var zamHeaderContainer = new Grid { Margin = new Thickness(0, 10, 0, 4) };
            zamHeaderContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            zamHeaderContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var zamHeaderText = new TextBlock
            {
                Text = $"{zamWydLabel}",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)) // Niebieski
            };
            var zamValueText = new TextBlock
            {
                Text = $"{zamWydValue:N0} kg",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Margin = new Thickness(5, 0, 0, 0)
            };
            var zamHeaderStack = new StackPanel { Orientation = Orientation.Horizontal };
            zamHeaderStack.Children.Add(zamHeaderText);
            zamHeaderStack.Children.Add(zamValueText);

            var goalHeaderText = new TextBlock
            {
                Text = goalLabel,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15)) // Żółty
            };
            var goalValueText = new TextBlock
            {
                Text = $"{goalValue:N0} kg",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                Margin = new Thickness(5, 0, 0, 0)
            };
            var goalHeaderStack = new StackPanel { Orientation = Orientation.Horizontal };
            goalHeaderStack.Children.Add(goalHeaderText);
            goalHeaderStack.Children.Add(goalValueText);
            Grid.SetColumn(goalHeaderStack, 1);

            zamHeaderContainer.Children.Add(zamHeaderStack);
            zamHeaderContainer.Children.Add(goalHeaderStack);
            mainStack.Children.Add(zamHeaderContainer);

            // Pasek postępu z metą (żółta linia)
            var progressContainer = new Grid { Height = 22, Margin = new Thickness(0, 0, 0, 8) };

            // Tło paska (szare)
            var progressBg = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)),
                CornerRadius = new CornerRadius(6),
                Height = 18,
                VerticalAlignment = VerticalAlignment.Center
            };
            progressContainer.Children.Add(progressBg);

            // Oblicz procent realizacji
            double progressPercent = goalValue > 0 ? (double)(zamWydValue / goalValue) : 0;
            double progressPercentClamped = Math.Min(progressPercent, 1.5); // max 150% szerokości
            int percentDisplay = (int)(progressPercent * 100);

            // Kolor paska zależny od realizacji (konfigurowalne progi)
            Color progressColor;
            if (progressPercent >= _progZielony / 100.0)
                progressColor = Color.FromRgb(39, 174, 96);   // Zielony - dobra realizacja
            else if (progressPercent >= _progZolty / 100.0)
                progressColor = Color.FromRgb(241, 196, 15);  // Żółty - średnia realizacja
            else
                progressColor = Color.FromRgb(231, 76, 60);   // Czerwony - słaba realizacja

            // Pasek postępu z dynamicznym kolorem
            var progressBar = new Border
            {
                Name = "progressBar",
                Background = new SolidColorBrush(progressColor),
                CornerRadius = new CornerRadius(6),
                Height = 18,
                Width = 0, // Startuje od 0 dla animacji
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            progressContainer.Children.Add(progressBar);

            // Procent na pasku
            var percentText = new TextBlock
            {
                Text = $"{percentDisplay}%",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(8, 0, 0, 0)
            };
            progressContainer.Children.Add(percentText);

            // Żółta meta (pionowa linia) - na końcu paska (100%)
            var goalLine = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                Width = 4,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(maxBarWidth - 2, 0, 0, 0), // Na końcu
                CornerRadius = new CornerRadius(2)
            };
            progressContainer.Children.Add(goalLine);

            mainStack.Children.Add(progressContainer);

            // Animacja paska postępu
            double targetWidth = progressPercentClamped * maxBarWidth;
            var widthAnimation = new DoubleAnimation
            {
                From = 0,
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(600),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            progressBar.BeginAnimation(Border.WidthProperty, widthAnimation);

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

            // Plan/Fakt + Stan - Zam/Wyd = Bilans
            string zamWydCalcLabel = _uzywajWydan ? "Wyd." : "Zam.";
            AddCalculationItem(calculationPanel, data.UzytoFakt ? "Fakt" : "Plan", $"{przychodUzyty:N0}", przychodColor);
            AddCalculationOperator(calculationPanel, "+");
            AddCalculationItem(calculationPanel, "Stan", $"{data.Stan:N0}", Color.FromRgb(52, 73, 94));
            AddCalculationOperator(calculationPanel, "-");
            AddCalculationItem(calculationPanel, zamWydCalcLabel, $"{zamWydValue:N0}", Color.FromRgb(41, 128, 185)); // Niebieski
            AddCalculationOperator(calculationPanel, "=");
            AddCalculationItem(calculationPanel, "Bilans", $"{data.Bilans:N0}", bilansColor, true);

            mainStack.Children.Add(calculationPanel);

            // === LISTA ODBIORCÓW ===
            if (data.Odbiorcy.Any())
            {
                var separator = new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    Margin = new Thickness(0, 12, 0, 5)
                };
                mainStack.Children.Add(separator);

                // Nagłówek sekcji: "Odbiorca | zam | wyd | %" - z marginesem na scrollbar
                var headerRow = new Grid { Margin = new Thickness(0, 0, 0, 3) };
                headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Nazwa
                headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) }); // zam
                headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) }); // wyd
                headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) }); // %

                var grayTextColor = new SolidColorBrush(Color.FromRgb(100, 100, 100));

                var headerNazwa = new TextBlock { Text = "Odbiorca", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = grayTextColor };
                Grid.SetColumn(headerNazwa, 0);
                headerRow.Children.Add(headerNazwa);

                var headerZam = new TextBlock { Text = "zam", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = grayTextColor, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(headerZam, 1);
                headerRow.Children.Add(headerZam);

                var headerWyd = new TextBlock { Text = "wyd", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = grayTextColor, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(headerWyd, 2);
                headerRow.Children.Add(headerWyd);

                var headerProc = new TextBlock { Text = "%", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = grayTextColor, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(headerProc, 3);
                headerRow.Children.Add(headerProc);

                mainStack.Children.Add(headerRow);

                // Scrollowalna lista odbiorców - zwiększony limit dla lepszej widoczności
                var scrollViewer = new ScrollViewer
                {
                    MaxHeight = 300,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                };

                var odbiorcyStack = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };

                foreach (var odbiorca in data.Odbiorcy)
                {
                    var odbiorcaBorder = new Border
                    {
                        Margin = new Thickness(0, 1, 0, 1),
                        Padding = new Thickness(4, 2, 4, 2),
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

                    // Wiersz: [nazwa] | zam | wyd | % - dopasowany do nagłówka
                    var odbiorcaRow = new Grid { Margin = new Thickness(0, 0, 16, 0) }; // Prawy margines na scrollbar
                    odbiorcaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Nazwa
                    odbiorcaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) }); // zam
                    odbiorcaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) }); // wyd
                    odbiorcaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) }); // %

                    var nazwaText = new TextBlock
                    {
                        Text = odbiorca.NazwaOdbiorcy,
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    Grid.SetColumn(nazwaText, 0);
                    odbiorcaRow.Children.Add(nazwaText);

                    var zamText = new TextBlock
                    {
                        Text = $"{odbiorca.Zamowione:N0}",
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(41, 128, 185)), // Niebieski
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    Grid.SetColumn(zamText, 1);
                    odbiorcaRow.Children.Add(zamText);

                    var wydText = new TextBlock
                    {
                        Text = odbiorca.Wydane > 0 ? $"{odbiorca.Wydane:N0}" : "-",
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = odbiorca.Wydane > 0
                            ? new SolidColorBrush(Color.FromRgb(39, 174, 96))  // Zielony
                            : new SolidColorBrush(Color.FromRgb(180, 180, 180)), // Szary
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    Grid.SetColumn(wydText, 2);
                    odbiorcaRow.Children.Add(wydText);

                    var procText = new TextBlock
                    {
                        Text = $"{odbiorca.ProcentUdzial:N0}",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    Grid.SetColumn(procText, 3);
                    odbiorcaRow.Children.Add(procText);

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

                // W trybie zakresu upewnij się że data końcowa >= początkowa
                if (_zakresDat && _selectedDateDo < _selectedDate)
                {
                    dpDataDo.SelectedDate = _selectedDate;
                    _selectedDateDo = _selectedDate;
                }

                UpdateDateDisplay();
                _ = LoadDataAsync();
            }
        }

        private void RbBilans_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            _ = LoadDataAsync();
        }

        private void ChkWydaniaBezZamowien_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            _pokazWydaniaBezZamowien = chkWydaniaBezZamowien.IsChecked == true;
            _ = LoadDataAsync();
        }

        // === SZYBKIE FILTRY DAT ===
        private void BtnDateToday_Click(object sender, RoutedEventArgs e)
        {
            if (_zakresDat)
            {
                // W trybie zakresu - ustaw dziś jako jedyny dzień
                dpData.SelectedDate = DateTime.Today;
                dpDataDo.SelectedDate = DateTime.Today;
            }
            else
            {
                dpData.SelectedDate = DateTime.Today;
            }
        }

        private void BtnDateTomorrow_Click(object sender, RoutedEventArgs e)
        {
            var tomorrow = DateTime.Today.AddDays(1);
            // Pomiń weekend
            while (tomorrow.DayOfWeek == DayOfWeek.Saturday || tomorrow.DayOfWeek == DayOfWeek.Sunday)
                tomorrow = tomorrow.AddDays(1);

            if (_zakresDat)
            {
                dpData.SelectedDate = tomorrow;
                dpDataDo.SelectedDate = tomorrow;
            }
            else
            {
                dpData.SelectedDate = tomorrow;
            }
        }

        private void BtnDateWeek_Click(object sender, RoutedEventArgs e)
        {
            // Znajdź poniedziałek bieżącego tygodnia
            var today = DateTime.Today;
            int daysUntilMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var monday = today.AddDays(-daysUntilMonday);
            var friday = monday.AddDays(4);

            // Włącz tryb zakresu i ustaw pon-pt
            chkZakresDat.IsChecked = true;
            dpData.SelectedDate = monday;
            dpDataDo.SelectedDate = friday;
        }

        // === ZAKRES DAT ===
        private void ChkZakresDat_Changed(object sender, RoutedEventArgs e)
        {
            _zakresDat = chkZakresDat.IsChecked == true;

            // Pokaż/ukryj drugi DatePicker
            txtDateRangeTo.Visibility = _zakresDat ? Visibility.Visible : Visibility.Collapsed;
            dpDataDo.Visibility = _zakresDat ? Visibility.Visible : Visibility.Collapsed;

            if (_zakresDat && dpDataDo.SelectedDate == null)
            {
                // Domyślnie ustaw datę końcową na datę początkową
                dpDataDo.SelectedDate = dpData.SelectedDate;
            }

            if (IsLoaded)
            {
                UpdateDateDisplay();
                _ = LoadDataAsync();
            }
        }

        private void DpDataDo_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpDataDo.SelectedDate.HasValue)
            {
                _selectedDateDo = dpDataDo.SelectedDate.Value;

                // Upewnij się że data końcowa >= początkowa
                if (_selectedDateDo < _selectedDate)
                {
                    dpDataDo.SelectedDate = _selectedDate;
                    _selectedDateDo = _selectedDate;
                }

                if (IsLoaded)
                {
                    UpdateDateDisplay();
                    _ = LoadDataAsync();
                }
            }
        }

        private void UpdateDateDisplay()
        {
            if (_zakresDat)
            {
                txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd} - {_selectedDateDo:yyyy-MM-dd}";
            }
            else
            {
                txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd dddd}";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Panel Pani Joli - uproszczony widok
        private void BtnPanelJola_Click(object sender, RoutedEventArgs e)
        {
            if (!_productDataList.Any())
            {
                MessageBox.Show("Brak produktów do wyświetlenia. Najpierw wybierz produkty.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ShowSimplifiedPanelJola(_productDataList.First(), 0);
        }

        // Odświeżenie danych
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadDataAsync();
        }

        // Ustawienie kolejności produktów
        private void BtnOrder_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedProductIds.Any())
            {
                MessageBox.Show("Najpierw wybierz produkty do wyświetlenia.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ShowProductOrderDialog();
        }

        // Ustawienia progów kolorów
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            // Tworzymy proste okno dialogowe
            var dialog = new Window
            {
                Title = "Ustawienia progów kolorów",
                Width = 350,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(20) };

            // Nagłówek
            stack.Children.Add(new TextBlock
            {
                Text = "Progi kolorów paska postępu",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            });

            // Próg zielony
            var greenStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            greenStack.Children.Add(new Border { Width = 20, Height = 20, Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)), CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 10, 0) });
            greenStack.Children.Add(new TextBlock { Text = "Zielony od:", VerticalAlignment = VerticalAlignment.Center, Width = 80 });
            var txtGreen = new TextBox { Text = _progZielony.ToString(), Width = 60, Margin = new Thickness(5, 0, 5, 0) };
            greenStack.Children.Add(txtGreen);
            greenStack.Children.Add(new TextBlock { Text = "%", VerticalAlignment = VerticalAlignment.Center });
            stack.Children.Add(greenStack);

            // Próg żółty
            var yellowStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            yellowStack.Children.Add(new Border { Width = 20, Height = 20, Background = new SolidColorBrush(Color.FromRgb(241, 196, 15)), CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 10, 0) });
            yellowStack.Children.Add(new TextBlock { Text = "Żółty od:", VerticalAlignment = VerticalAlignment.Center, Width = 80 });
            var txtYellow = new TextBox { Text = _progZolty.ToString(), Width = 60, Margin = new Thickness(5, 0, 5, 0) };
            yellowStack.Children.Add(txtYellow);
            yellowStack.Children.Add(new TextBlock { Text = "%", VerticalAlignment = VerticalAlignment.Center });
            stack.Children.Add(yellowStack);

            // Info o czerwonym
            var redStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            redStack.Children.Add(new Border { Width = 20, Height = 20, Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)), CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 10, 0) });
            redStack.Children.Add(new TextBlock { Text = "Czerwony poniżej żółtego", VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)) });
            stack.Children.Add(redStack);

            // Przyciski
            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOK = new Button { Content = "Zapisz", Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(0, 0, 10, 0), Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            var btnCancel = new Button { Content = "Anuluj", Padding = new Thickness(20, 8, 20, 8), Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };

            btnOK.Click += (s, args) =>
            {
                if (int.TryParse(txtGreen.Text, out int green) && int.TryParse(txtYellow.Text, out int yellow))
                {
                    if (green > yellow && green <= 100 && yellow >= 0)
                    {
                        _progZielony = green;
                        _progZolty = yellow;
                        dialog.DialogResult = true;
                        _ = LoadDataAsync(); // Odśwież z nowymi progami
                    }
                    else
                    {
                        MessageBox.Show("Próg zielony musi być większy od żółtego i w zakresie 0-100%", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Wprowadź poprawne liczby", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            btnCancel.Click += (s, args) => dialog.DialogResult = false;

            btnStack.Children.Add(btnOK);
            btnStack.Children.Add(btnCancel);
            stack.Children.Add(btnStack);

            dialog.Content = stack;
            dialog.ShowDialog();
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
                        var odbiorcy = string.Join(", ", p.Odbiorcy.Select(o => $"{o.NazwaOdbiorcy}:zam={o.Zamowione:N0}/wyd={o.Wydane:N0}"));
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

        // Tryb pełnoekranowy - ukrycie pasków
        private bool _isFullscreen = false;

        private void BtnFullscreen_Click(object sender, RoutedEventArgs e)
        {
            _isFullscreen = true;
            borderHeader.Visibility = Visibility.Collapsed;
            borderFilters.Visibility = Visibility.Collapsed;
            btnExitFullscreen.Visibility = Visibility.Visible;

            // Ukryj też panel boczny w trybie pełnoekranowym
            if (_isPanelOpen)
            {
                _isPanelOpen = false;
                sidePanel.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnExitFullscreen_Click(object sender, RoutedEventArgs e)
        {
            _isFullscreen = false;
            borderHeader.Visibility = Visibility.Visible;
            borderFilters.Visibility = Visibility.Visible;
            btnExitFullscreen.Visibility = Visibility.Collapsed;
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

        private async void BtnSetDefaultView_Click(object sender, RoutedEventArgs e)
        {
            if (cmbWidok.SelectedItem is ComboBoxItem item && item.Tag is DashboardView view)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();

                    // Najpierw usuń domyślny z wszystkich
                    const string sqlClear = @"UPDATE dbo.DashboardWidoki SET IsDomyslny = 0";
                    await using (var cmdClear = new SqlCommand(sqlClear, cn))
                    {
                        await cmdClear.ExecuteNonQueryAsync();
                    }

                    // Ustaw wybrany jako domyślny
                    const string sqlSet = @"UPDATE dbo.DashboardWidoki SET IsDomyslny = 1 WHERE Id = @Id";
                    await using (var cmdSet = new SqlCommand(sqlSet, cn))
                    {
                        cmdSet.Parameters.AddWithValue("@Id", view.Id);
                        await cmdSet.ExecuteNonQueryAsync();
                    }

                    await LoadSavedViewsAsync();
                    MessageBox.Show($"Widok '{view.Nazwa}' został ustawiony jako domyślny.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Wybierz widok do ustawienia jako domyślny.", "Informacja",
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

        #region Obsługa zdjęć produktów

        /// <summary>
        /// Ładuje wszystkie zdjęcia produktów do cache
        /// </summary>
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadProductImages] Błąd: {ex.Message}");
                // Ignoruj błąd - zdjęcia są opcjonalne
            }
        }

        /// <summary>
        /// Pobiera zdjęcie produktu z cache lub null jeśli brak
        /// </summary>
        private BitmapImage? GetProductImage(int towarId)
        {
            return _productImages.TryGetValue(towarId, out var img) ? img : null;
        }

        /// <summary>
        /// Konwertuje bajty na BitmapImage
        /// </summary>
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
                    image.DecodePixelWidth = 140;
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

        /// <summary>
        /// Zapisuje zdjęcie produktu do bazy
        /// </summary>
        private async System.Threading.Tasks.Task SaveProductImageAsync(int towarId, string filePath)
        {
            try
            {
                byte[] imageData = await System.IO.File.ReadAllBytesAsync(filePath);
                string fileName = System.IO.Path.GetFileName(filePath);
                string extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

                string mimeType = extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".webp" => "image/webp",
                    _ => "image/unknown"
                };

                // Pobierz wymiary obrazka
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

                // Sprawdź czy tabela istnieje
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
                {
                    await cmdCheck.ExecuteNonQueryAsync();
                }

                // Dezaktywuj poprzednie zdjęcia
                const string sqlDeactivate = @"UPDATE dbo.TowarZdjecia SET Aktywne = 0 WHERE TowarId = @TowarId";
                await using (var cmdDe = new SqlCommand(sqlDeactivate, cn))
                {
                    cmdDe.Parameters.AddWithValue("@TowarId", towarId);
                    await cmdDe.ExecuteNonQueryAsync();
                }

                // Dodaj nowe zdjęcie
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

                // Zaktualizuj cache
                _productImages[towarId] = BytesToBitmapImage(imageData);

                MessageBox.Show($"Zdjęcie zostało zapisane!\n\nPlik: {fileName}\nRozmiar: {sizeKB} KB\nWymiary: {width}x{height}",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania zdjęcia:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Otwiera dialog wyboru pliku i importuje zdjęcie
        /// </summary>
        private async void ImportProductImage(int towarId, string productName)
        {
            var dialog = new OpenFileDialog
            {
                Title = $"Wybierz zdjęcie dla: {productName}",
                Filter = "Obrazy|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp|Wszystkie pliki|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                await SaveProductImageAsync(towarId, dialog.FileName);
                // Odśwież widok
                _ = LoadDataAsync();
            }
        }

        /// <summary>
        /// Usuwa zdjęcie produktu
        /// </summary>
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

                _productImages.Remove(towarId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas usuwania zdjęcia:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Usuwa zdjęcie i odświeża widok
        /// </summary>
        private async System.Threading.Tasks.Task DeleteProductImageWithRefresh(int towarId)
        {
            var result = MessageBox.Show("Czy na pewno chcesz usunąć zdjęcie tego produktu?",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await DeleteProductImageAsync(towarId);
                await LoadDataAsync();
            }
        }

        #endregion

        #region Kolejność produktów

        /// <summary>
        /// Pobiera zapisaną kolejność produktów z bazy
        /// </summary>
        private async System.Threading.Tasks.Task<Dictionary<int, int>> LoadProductOrderAsync()
        {
            var order = new Dictionary<int, int>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Sprawdź czy tabela istnieje
                const string checkTable = @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KolejnoscTowarow')
                    CREATE TABLE dbo.KolejnoscTowarow (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        TowarId INT NOT NULL,
                        Pozycja INT NOT NULL,
                        DataModyfikacji DATETIME DEFAULT GETDATE(),
                        ZmodyfikowalPrzez NVARCHAR(100) NULL,
                        CONSTRAINT UQ_KolejnoscTowarow_TowarId UNIQUE (TowarId)
                    )";
                await using (var cmdCheck = new SqlCommand(checkTable, cn))
                {
                    await cmdCheck.ExecuteNonQueryAsync();
                }

                const string sql = @"SELECT TowarId, Pozycja FROM dbo.KolejnoscTowarow ORDER BY Pozycja";
                await using var cmd = new SqlCommand(sql, cn);
                await using var rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    int towarId = rdr.GetInt32(0);
                    int pozycja = rdr.GetInt32(1);
                    order[towarId] = pozycja;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadProductOrder] Błąd: {ex.Message}");
            }
            return order;
        }

        /// <summary>
        /// Zapisuje kolejność produktów do bazy
        /// </summary>
        private async System.Threading.Tasks.Task SaveProductOrderAsync(List<int> productIds)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Usuń starą kolejność
                const string sqlDelete = @"DELETE FROM dbo.KolejnoscTowarow";
                await using (var cmdDel = new SqlCommand(sqlDelete, cn))
                {
                    await cmdDel.ExecuteNonQueryAsync();
                }

                // Dodaj nową kolejność
                for (int i = 0; i < productIds.Count; i++)
                {
                    const string sqlInsert = @"INSERT INTO dbo.KolejnoscTowarow (TowarId, Pozycja, ZmodyfikowalPrzez)
                                               VALUES (@TowarId, @Pozycja, @User)";
                    await using var cmdIns = new SqlCommand(sqlInsert, cn);
                    cmdIns.Parameters.AddWithValue("@TowarId", productIds[i]);
                    cmdIns.Parameters.AddWithValue("@Pozycja", i + 1);
                    cmdIns.Parameters.AddWithValue("@User", Environment.UserName);
                    await cmdIns.ExecuteNonQueryAsync();
                }

                MessageBox.Show("Kolejność produktów została zapisana!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania kolejności:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Otwiera okno do ustawiania kolejności produktów
        /// </summary>
        private void ShowProductOrderDialog()
        {
            var dialog = new Window
            {
                Title = "Ustaw kolejność produktów",
                Width = 450,
                Height = 550,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.CanResize
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Nagłówek
            var header = new TextBlock
            {
                Text = "Przeciągnij produkty lub użyj przycisków ▲▼ aby zmienić kolejność:",
                Margin = new Thickness(15, 15, 15, 10),
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Lista produktów z przyciskami
            var scrollViewer = new ScrollViewer { Margin = new Thickness(15, 0, 15, 0) };
            var listStack = new StackPanel();

            // Pobierz produkty w aktualnej kolejności
            var orderedProducts = _selectedProductIds
                .Select(id => _allProducts.FirstOrDefault(p => p.Id == id))
                .Where(p => p != null)
                .ToList();

            var productList = new List<(int Id, string Name, Border Border)>();

            foreach (var product in orderedProducts)
            {
                if (product == null) continue;

                var itemBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(222, 226, 230)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(0, 2, 0, 2),
                    Padding = new Thickness(10, 8, 10, 8)
                };

                var itemGrid = new Grid();
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var nameText = new TextBlock
                {
                    Text = $"[{product.Kod}] {product.Nazwa}",
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(nameText, 0);
                itemGrid.Children.Add(nameText);

                var btnStack = new StackPanel { Orientation = Orientation.Horizontal };

                var btnUp = new Button
                {
                    Content = "▲",
                    Width = 30,
                    Margin = new Thickness(5, 0, 2, 0),
                    Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var btnDown = new Button
                {
                    Content = "▼",
                    Width = 30,
                    Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                btnStack.Children.Add(btnUp);
                btnStack.Children.Add(btnDown);
                Grid.SetColumn(btnStack, 1);
                itemGrid.Children.Add(btnStack);

                itemBorder.Child = itemGrid;
                listStack.Children.Add(itemBorder);

                productList.Add((product.Id, product.Kod, itemBorder));

                // Event handlers dla przycisków
                var currentProduct = product;
                btnUp.Click += (s, e) =>
                {
                    int index = productList.FindIndex(p => p.Id == currentProduct.Id);
                    if (index > 0)
                    {
                        // Zamień miejscami
                        var temp = productList[index];
                        productList[index] = productList[index - 1];
                        productList[index - 1] = temp;

                        // Przebuduj UI
                        listStack.Children.Clear();
                        foreach (var p in productList)
                            listStack.Children.Add(p.Border);
                    }
                };

                btnDown.Click += (s, e) =>
                {
                    int index = productList.FindIndex(p => p.Id == currentProduct.Id);
                    if (index < productList.Count - 1)
                    {
                        // Zamień miejscami
                        var temp = productList[index];
                        productList[index] = productList[index + 1];
                        productList[index + 1] = temp;

                        // Przebuduj UI
                        listStack.Children.Clear();
                        foreach (var p in productList)
                            listStack.Children.Add(p.Border);
                    }
                };
            }

            scrollViewer.Content = listStack;
            Grid.SetRow(scrollViewer, 1);
            mainGrid.Children.Add(scrollViewer);

            // Przyciski na dole
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(15)
            };

            var btnSave = new Button
            {
                Content = "💾 Zapisz jako domyślną",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var btnCancel = new Button
            {
                Content = "Anuluj",
                Padding = new Thickness(15, 8, 15, 8),
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            btnSave.Click += async (s, e) =>
            {
                var orderedIds = productList.Select(p => p.Id).ToList();
                await SaveProductOrderAsync(orderedIds);
                _selectedProductIds = orderedIds;
                dialog.DialogResult = true;
                _ = LoadDataAsync();
            };

            btnCancel.Click += (s, e) => dialog.DialogResult = false;

            buttonPanel.Children.Add(btnSave);
            buttonPanel.Children.Add(btnCancel);
            Grid.SetRow(buttonPanel, 2);
            mainGrid.Children.Add(buttonPanel);

            dialog.Content = mainGrid;
            dialog.ShowDialog();
        }

        #endregion

        #region Powiększona karta produktu

        /// <summary>
        /// Pobiera dane historyczne zamówień dla produktu
        /// </summary>
        private async System.Threading.Tasks.Task<(List<HistoricalClientData> Last3Months, List<HistoricalClientData> Older, List<MonthlyTrend> Trends)> LoadHistoricalDataAsync(int productId, string productCode)
        {
            var last3Months = new List<HistoricalClientData>();
            var olderClients = new List<HistoricalClientData>();
            var trends = new List<MonthlyTrend>();

            try
            {
                var today = DateTime.Today;
                var date3MonthsAgo = today.AddMonths(-3);
                var date12MonthsAgo = today.AddMonths(-12);

                // Pobierz kontrahentów
                var kontrahenci = new Dictionary<int, string>();
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT ID, nazwa FROM [HM].[KH] WHERE nazwa IS NOT NULL";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        string nazwa = rdr.IsDBNull(1) ? "" : rdr.GetString(1).Trim();
                        kontrahenci[id] = nazwa;
                    }
                }

                // Pobierz zamówienia z ostatnich 12 miesięcy dla tego produktu
                var clientOrders = new Dictionary<int, List<(DateTime Data, decimal Ilosc)>>();

                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();

                    // Najpierw pobierz ID zamówień z ostatnich 12 miesięcy
                    const string sqlOrders = @"
                        SELECT z.Id, z.KlientId, z.DataUboju, t.Ilosc
                        FROM dbo.ZamowieniaMieso z
                        INNER JOIN dbo.ZamowieniaMiesoTowar t ON z.Id = t.ZamowienieId
                        WHERE z.DataUboju >= @DateFrom
                          AND z.DataUboju <= @DateTo
                          AND z.Status <> 'Anulowane'
                          AND t.KodTowaru = @ProductCode";

                    await using var cmd = new SqlCommand(sqlOrders, cn);
                    cmd.Parameters.AddWithValue("@DateFrom", date12MonthsAgo);
                    cmd.Parameters.AddWithValue("@DateTo", today);
                    cmd.Parameters.AddWithValue("@ProductCode", productCode);

                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int klientId = rdr.GetInt32(1);
                        DateTime dataUboju = rdr.GetDateTime(2);
                        decimal ilosc = rdr.IsDBNull(3) ? 0 : rdr.GetDecimal(3);

                        if (!clientOrders.ContainsKey(klientId))
                            clientOrders[klientId] = new List<(DateTime, decimal)>();
                        clientOrders[klientId].Add((dataUboju, ilosc));
                    }
                }

                // Przetwórz dane klientów
                foreach (var kvp in clientOrders)
                {
                    int klientId = kvp.Key;
                    var orders = kvp.Value;

                    var ordersLast3Months = orders.Where(o => o.Data >= date3MonthsAgo).ToList();
                    var ordersOlder = orders.Where(o => o.Data < date3MonthsAgo).ToList();

                    string nazwaKlienta = kontrahenci.TryGetValue(klientId, out var nazwa) ? nazwa : $"Nieznany ({klientId})";

                    // Klient zamawiał w ostatnich 3 miesiącach
                    if (ordersLast3Months.Any())
                    {
                        var suma = ordersLast3Months.Sum(o => o.Ilosc);
                        var liczba = ordersLast3Months.Count;
                        var ostatnie = ordersLast3Months.Max(o => o.Data);
                        var pierwsze = ordersLast3Months.Min(o => o.Data);

                        last3Months.Add(new HistoricalClientData
                        {
                            KlientId = klientId,
                            NazwaKlienta = nazwaKlienta,
                            SumaZamowien = suma,
                            LiczbaZamowien = liczba,
                            OstatnieZamowienie = ostatnie,
                            PierwszeZamowienie = pierwsze,
                            SredniaZamowienie = liczba > 0 ? suma / liczba : 0,
                            DniOdOstatniego = (int)(today - ostatnie).TotalDays
                        });
                    }
                    // Klient zamawiał tylko w starszym okresie (potencjalny klient do odzyskania)
                    else if (ordersOlder.Any())
                    {
                        var suma = ordersOlder.Sum(o => o.Ilosc);
                        var liczba = ordersOlder.Count;
                        var ostatnie = ordersOlder.Max(o => o.Data);
                        var pierwsze = ordersOlder.Min(o => o.Data);

                        olderClients.Add(new HistoricalClientData
                        {
                            KlientId = klientId,
                            NazwaKlienta = nazwaKlienta,
                            SumaZamowien = suma,
                            LiczbaZamowien = liczba,
                            OstatnieZamowienie = ostatnie,
                            PierwszeZamowienie = pierwsze,
                            SredniaZamowienie = liczba > 0 ? suma / liczba : 0,
                            DniOdOstatniego = (int)(today - ostatnie).TotalDays
                        });
                    }
                }

                // Oblicz trendy miesięczne
                var allOrders = clientOrders.Values.SelectMany(x => x).ToList();
                var groupedByMonth = allOrders
                    .GroupBy(o => new { o.Data.Year, o.Data.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

                foreach (var group in groupedByMonth)
                {
                    trends.Add(new MonthlyTrend
                    {
                        Rok = group.Key.Year,
                        Miesiac = group.Key.Month,
                        Suma = group.Sum(o => o.Ilosc),
                        LiczbaZamowien = group.Count()
                    });
                }

                // Sortuj listy
                last3Months = last3Months.OrderByDescending(c => c.SumaZamowien).ToList();
                olderClients = olderClients.OrderByDescending(c => c.SumaZamowien).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadHistoricalData] Błąd: {ex.Message}");
            }

            return (last3Months, olderClients, trends);
        }

        /// <summary>
        /// Wyświetla powiększoną kartę produktu - widok prezentacyjny na projektor
        /// </summary>
        private void ShowExpandedProductCard(ProductData data, int currentIndex = -1, bool autoSlideshow = false)
        {
            // Jeśli nie podano indexu, znajdź go w liście
            if (currentIndex < 0)
            {
                currentIndex = _productDataList.FindIndex(p => p.Id == data.Id);
                if (currentIndex < 0) currentIndex = 0;
            }

            // === STAN WIDOKU - można zmieniać dynamicznie ===
            int viewIndex = currentIndex;
            bool viewUseWydania = _uzywajWydan;

            var dialog = new Window
            {
                Title = $"[{data.Kod}] {data.Nazwa}",
                WindowState = WindowState.Maximized,
                WindowStyle = WindowStyle.None,
                Background = new SolidColorBrush(Color.FromRgb(20, 25, 30)),
                ResizeMode = ResizeMode.NoResize
            };

            // Kontener na dynamiczną treść
            var mainContainer = new Grid();
            dialog.Content = mainContainer;

            // Timer dla slideshow
            System.Windows.Threading.DispatcherTimer? slideshowTimer = null;
            int countdown = 10;
            bool slideshowActive = false;
            Button? slideshowBtnRef = null;
            TextBlock? productInfoLabelRef = null;

            // === SYMULACJA REDUKCJI (tymczasowe, bez zapisu do bazy) ===
            // Klucz: (ProductId, KlientId), Wartość: redukcja w kg (ujemna wartość)
            var simulatedReductions = new Dictionary<(int ProductId, int KlientId), decimal>();

            // === METODA ODŚWIEŻAJĄCA ZAWARTOŚĆ ===
            Action refreshContent = null!;
            refreshContent = () =>
            {
                var currentData = _productDataList[viewIndex];
                dialog.Title = $"[{currentData.Kod}] {currentData.Nazwa}";

                // Wyczyść i przebuduj
                mainContainer.Children.Clear();

                // Oblicz symulowaną redukcję dla tego produktu
                decimal totalReduction = simulatedReductions
                    .Where(kvp => kvp.Key.ProductId == currentData.Id)
                    .Sum(kvp => kvp.Value);

                // Obliczenia z uwzględnieniem symulacji
                bool uzyjFakt = currentData.Fakt > 0;
                decimal cel = uzyjFakt ? currentData.Fakt : currentData.Plan;
                decimal zamowieniaSymulowane = currentData.Zamowienia + totalReduction; // totalReduction jest ujemne
                decimal zamLubWyd = viewUseWydania ? currentData.Wydania : zamowieniaSymulowane;
                decimal bilans = cel + currentData.Stan - zamLubWyd;
                decimal doWydania = zamowieniaSymulowane - currentData.Wydania;
                decimal procentRealizacji = cel > 0 ? (zamLubWyd / cel) * 100 : 0;
                bool przekroczono = procentRealizacji > 100;
                bool maSymulacje = totalReduction != 0;

                // === GŁÓWNY KONTENER ===
                var mainGrid = new Grid { Margin = new Thickness(40) };
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // === NAGŁÓWEK ===
                var headerPanel = new Grid { Margin = new Thickness(0, 0, 0, 20) };
                headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Zdjęcie produktu
                var productImage = GetProductImage(currentData.Id);
                var imageBorder = new Border
                {
                    Width = 100, Height = 100,
                    CornerRadius = new CornerRadius(12),
                    ClipToBounds = true,
                    Margin = new Thickness(0, 0, 25, 0)
                };
                if (productImage != null)
                {
                    var img = new System.Windows.Controls.Image { Source = productImage, Width = 96, Height = 96, Stretch = Stretch.Uniform };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    imageBorder.Child = img;
                }
                else
                {
                    imageBorder.Background = new SolidColorBrush(Color.FromRgb(52, 73, 94));
                    imageBorder.Child = new TextBlock { Text = "📦", FontSize = 40, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)) };
                }
                Grid.SetColumn(imageBorder, 0);
                headerPanel.Children.Add(imageBorder);

                // Nazwa i data z DatePicker
                var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                titleRow.Children.Add(new TextBlock { Text = currentData.Kod, FontSize = 42, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 30, 0) });

                var datePicker = new DatePicker
                {
                    SelectedDate = _selectedDate,
                    FontSize = 28,
                    Width = 220,
                    Height = 50,
                    Padding = new Thickness(10, 5, 10, 5),
                    VerticalAlignment = VerticalAlignment.Center
                };
                // Style dla czarnego tła i białej czcionki
                datePicker.Resources.Add(SystemColors.WindowBrushKey, new SolidColorBrush(Color.FromRgb(30, 30, 30)));
                datePicker.Resources.Add(SystemColors.WindowTextBrushKey, Brushes.White);
                datePicker.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                datePicker.Foreground = Brushes.White;
                datePicker.BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80));
                datePicker.BorderThickness = new Thickness(2);
                datePicker.SelectedDateChanged += async (s, e) =>
                {
                    if (datePicker.SelectedDate.HasValue)
                    {
                        _selectedDate = datePicker.SelectedDate.Value;
                        await LoadDataAsync();
                        if (_productDataList.Count > 0)
                        {
                            viewIndex = Math.Min(viewIndex, _productDataList.Count - 1);
                            refreshContent();
                        }
                    }
                };
                titleRow.Children.Add(datePicker);

                if (_zakresDat)
                {
                    titleRow.Children.Add(new TextBlock { Text = " - ", FontSize = 28, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) });
                    var datePickerDo = new DatePicker
                    {
                        SelectedDate = _selectedDateDo,
                        FontSize = 28,
                        Width = 220,
                        Height = 50,
                        Padding = new Thickness(10, 5, 10, 5),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    datePickerDo.Resources.Add(SystemColors.WindowBrushKey, new SolidColorBrush(Color.FromRgb(30, 30, 30)));
                    datePickerDo.Resources.Add(SystemColors.WindowTextBrushKey, Brushes.White);
                    datePickerDo.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                    datePickerDo.Foreground = Brushes.White;
                    datePickerDo.BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80));
                    datePickerDo.BorderThickness = new Thickness(2);
                    datePickerDo.SelectedDateChanged += async (s, e) =>
                    {
                        if (datePickerDo.SelectedDate.HasValue)
                        {
                            _selectedDateDo = datePickerDo.SelectedDate.Value;
                            await LoadDataAsync();
                            if (_productDataList.Count > 0)
                            {
                                viewIndex = Math.Min(viewIndex, _productDataList.Count - 1);
                                refreshContent();
                            }
                        }
                    };
                    titleRow.Children.Add(datePickerDo);
                }

                titleStack.Children.Add(titleRow);
                Grid.SetColumn(titleStack, 1);
                headerPanel.Children.Add(titleStack);

                // BILANS (z informacją o symulacji)
                var bilansBorder = new Border
                {
                    Background = new SolidColorBrush(bilans >= 0 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(231, 76, 60)),
                    CornerRadius = new CornerRadius(15), Padding = new Thickness(30, 15, 30, 15),
                    Margin = new Thickness(20, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center
                };
                // Jeśli jest symulacja - dodaj obramowanie żółte
                if (maSymulacje)
                {
                    bilansBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(241, 196, 15));
                    bilansBorder.BorderThickness = new Thickness(4);
                }
                var bilansStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                bilansStack.Children.Add(new TextBlock
                {
                    Text = maSymulacje ? "📉 BILANS (symulacja)" : "BILANS",
                    FontSize = 14,
                    Foreground = maSymulacje ? new SolidColorBrush(Color.FromRgb(241, 196, 15)) : Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                bilansStack.Children.Add(new TextBlock { Text = $"{bilans:N0} kg", FontSize = 48, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
                if (maSymulacje)
                {
                    bilansStack.Children.Add(new TextBlock
                    {
                        Text = $"(redukcja: {totalReduction:N0} kg)",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                }
                bilansBorder.Child = bilansStack;
                Grid.SetColumn(bilansBorder, 2);
                headerPanel.Children.Add(bilansBorder);

                // Przycisk zamknij
                var closeBtn = new Button { Content = "✕", FontSize = 28, Width = 50, Height = 50, Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
                closeBtn.Click += (s, e) => { slideshowTimer?.Stop(); dialog.Close(); };
                Grid.SetColumn(closeBtn, 3);
                headerPanel.Children.Add(closeBtn);
                Grid.SetRow(headerPanel, 0);
                mainGrid.Children.Add(headerPanel);

                // === GŁÓWNA SEKCJA ===
                var contentGrid = new Grid();
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // === LEWA STRONA ===
                var leftPanel = new StackPanel();

                // Panel kontroli
                var controlPanel = new Border { Background = new SolidColorBrush(Color.FromRgb(30, 40, 50)), CornerRadius = new CornerRadius(12), Padding = new Thickness(25, 18, 25, 18), Margin = new Thickness(0, 0, 0, 20) };
                var controlStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                controlStack.Children.Add(new TextBlock { Text = "ROZLICZENIE: ", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 20, 0) });

                var radioZam = new RadioButton { Content = "ZAMÓWIENIA", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(230, 126, 34)), IsChecked = !viewUseWydania, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 30, 0) };
                var radioWyd = new RadioButton { Content = "WYDANIA", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43)), IsChecked = viewUseWydania, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 40, 0) };

                radioZam.Checked += (s, e) => { viewUseWydania = false; refreshContent(); };
                radioWyd.Checked += (s, e) => { viewUseWydania = true; refreshContent(); };

                controlStack.Children.Add(radioZam);
                controlStack.Children.Add(radioWyd);
                controlStack.Children.Add(new Border { Width = 2, Height = 35, Background = new SolidColorBrush(Color.FromRgb(80, 90, 100)), Margin = new Thickness(0, 0, 25, 0) });

                var checkWydBezZam = new CheckBox { Content = "Wydania bez zamówień", FontSize = 18, Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)), VerticalAlignment = VerticalAlignment.Center, IsChecked = _pokazWydaniaBezZamowien };
                checkWydBezZam.Checked += async (s, e) => { _pokazWydaniaBezZamowien = true; await LoadDataAsync(); viewIndex = Math.Min(viewIndex, _productDataList.Count - 1); refreshContent(); };
                checkWydBezZam.Unchecked += async (s, e) => { _pokazWydaniaBezZamowien = false; await LoadDataAsync(); viewIndex = Math.Min(viewIndex, _productDataList.Count - 1); refreshContent(); };
                controlStack.Children.Add(checkWydBezZam);
                controlPanel.Child = controlStack;
                leftPanel.Children.Add(controlPanel);

                // === BANNER SYMULACJI (gdy aktywna) ===
                if (maSymulacje)
                {
                    var simBanner = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(20, 12, 20, 12),
                        Margin = new Thickness(0, 0, 0, 15),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    var simBannerContent = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                    simBannerContent.Children.Add(new TextBlock
                    {
                        Text = "📊 TRYB SYMULACJI",
                        FontSize = 20,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    simBannerContent.Children.Add(new TextBlock
                    {
                        Text = $"   |   Redukcja zamówień: {totalReduction:N0} kg",
                        FontSize = 18,
                        Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    simBanner.Child = simBannerContent;
                    leftPanel.Children.Add(simBanner);
                }

                // === FORMUŁA - ULEPSZONA ===
                var formulaContainer = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(25, 15, 25, 15),
                    Margin = new Thickness(0, 0, 0, 20),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                if (maSymulacje && !viewUseWydania)
                {
                    // SYMULACJA AKTYWNA - pokaż dwie linie
                    var formulaStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

                    // Linia 1: Oryginalne wartości (mniejsze, szare)
                    var origLine = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
                    origLine.Children.Add(new TextBlock { Text = "Oryginał: ", FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)), VerticalAlignment = VerticalAlignment.Center });
                    string celText = uzyjFakt ? $"FAKT {currentData.Fakt:N0}" : $"PLAN {currentData.Plan:N0}";
                    origLine.Children.Add(new TextBlock { Text = celText, FontSize = 16, Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)), VerticalAlignment = VerticalAlignment.Center });
                    origLine.Children.Add(new TextBlock { Text = $" + STAN {currentData.Stan:N0} − ZAM {currentData.Zamowienia:N0} = ", FontSize = 16, Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)), VerticalAlignment = VerticalAlignment.Center });
                    decimal bilansOryg = cel + currentData.Stan - currentData.Zamowienia;
                    origLine.Children.Add(new TextBlock
                    {
                        Text = $"{bilansOryg:N0}",
                        FontSize = 18,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(bilansOryg >= 0 ? Color.FromRgb(100, 150, 100) : Color.FromRgb(180, 80, 80)),
                        TextDecorations = TextDecorations.Strikethrough,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    formulaStack.Children.Add(origLine);

                    // Linia 2: Symulowane wartości (większe, kolorowe)
                    var simLine = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                    simLine.Children.Add(new TextBlock { Text = "Symulacja: ", FontSize = 16, Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15)), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });

                    if (uzyjFakt)
                        simLine.Children.Add(new TextBlock { Text = $"FAKT {currentData.Fakt:N0}", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(155, 89, 182)), VerticalAlignment = VerticalAlignment.Center });
                    else
                        simLine.Children.Add(new TextBlock { Text = $"PLAN {currentData.Plan:N0}", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)), VerticalAlignment = VerticalAlignment.Center });

                    simLine.Children.Add(new TextBlock { Text = "  +  ", FontSize = 26, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
                    simLine.Children.Add(new TextBlock { Text = $"STAN {currentData.Stan:N0}", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(26, 188, 156)), VerticalAlignment = VerticalAlignment.Center });
                    simLine.Children.Add(new TextBlock { Text = "  −  ", FontSize = 26, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
                    simLine.Children.Add(new TextBlock { Text = $"ZAM {zamowieniaSymulowane:N0}", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15)), VerticalAlignment = VerticalAlignment.Center });
                    simLine.Children.Add(new TextBlock { Text = "  =  ", FontSize = 26, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
                    simLine.Children.Add(new TextBlock { Text = $"{bilans:N0}", FontSize = 34, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(bilans >= 0 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(231, 76, 60)), VerticalAlignment = VerticalAlignment.Center });
                    formulaStack.Children.Add(simLine);

                    // Linia 3: Różnica
                    decimal roznica = bilans - bilansOryg;
                    var diffLine = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
                    diffLine.Children.Add(new TextBlock
                    {
                        Text = $"Zmiana bilansu: {roznica:+#,##0;-#,##0;0} kg",
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(roznica >= 0 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(231, 76, 60)),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    formulaStack.Children.Add(diffLine);

                    formulaContainer.Child = formulaStack;
                }
                else
                {
                    // BEZ SYMULACJI - standardowa formuła w jednej linii
                    var formulaPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                    if (uzyjFakt)
                    {
                        formulaPanel.Children.Add(new TextBlock { Text = $"PLAN {currentData.Plan:N0}", FontSize = 22, Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)), TextDecorations = TextDecorations.Strikethrough, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 15, 0) });
                        formulaPanel.Children.Add(new TextBlock { Text = $"FAKT {currentData.Fakt:N0}", FontSize = 30, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(155, 89, 182)), VerticalAlignment = VerticalAlignment.Center });
                    }
                    else
                        formulaPanel.Children.Add(new TextBlock { Text = $"PLAN {currentData.Plan:N0}", FontSize = 30, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)), VerticalAlignment = VerticalAlignment.Center });

                    formulaPanel.Children.Add(new TextBlock { Text = "  +  ", FontSize = 30, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
                    formulaPanel.Children.Add(new TextBlock { Text = $"STAN {currentData.Stan:N0}", FontSize = 30, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(26, 188, 156)), VerticalAlignment = VerticalAlignment.Center });
                    formulaPanel.Children.Add(new TextBlock { Text = "  −  ", FontSize = 30, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });

                    if (viewUseWydania)
                        formulaPanel.Children.Add(new TextBlock { Text = $"WYD {currentData.Wydania:N0}", FontSize = 30, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43)), VerticalAlignment = VerticalAlignment.Center });
                    else
                        formulaPanel.Children.Add(new TextBlock { Text = $"ZAM {currentData.Zamowienia:N0}", FontSize = 30, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(230, 126, 34)), VerticalAlignment = VerticalAlignment.Center });

                    formulaPanel.Children.Add(new TextBlock { Text = "  =  ", FontSize = 30, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
                    formulaPanel.Children.Add(new TextBlock { Text = $"{bilans:N0}", FontSize = 36, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(bilans >= 0 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(231, 76, 60)), VerticalAlignment = VerticalAlignment.Center });

                    formulaContainer.Child = formulaPanel;
                }
                leftPanel.Children.Add(formulaContainer);

                // Pasek postępu
                var progressBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(39, 55, 70)), CornerRadius = new CornerRadius(15), Padding = new Thickness(30), Margin = new Thickness(0, 0, 0, 25) };
                var progressStack = new StackPanel();
                var celLabel = uzyjFakt ? "FAKT" : "PLAN";
                var celColor = uzyjFakt ? Color.FromRgb(155, 89, 182) : Color.FromRgb(52, 152, 219);

                // Obliczenia dla dwóch celów (Plan i Plan+Stan)
                decimal celPlan = cel;
                decimal celPlanStan = cel + currentData.Stan;
                bool maDwaCele = currentData.Stan > 0;

                const double maxScale = 140.0;
                double markerPosPlan = (100.0 / maxScale) * 100; // CEL Plan na 100%
                double markerPosPlanStan = maDwaCele ? Math.Min((double)(celPlanStan / celPlan) * 100.0 / maxScale * 100, 100) : 0; // CEL Plan+Stan

                Color barColor = przekroczono ? Color.FromRgb(155, 89, 182) : procentRealizacji >= _progZielony ? Color.FromRgb(39, 174, 96) : procentRealizacji >= _progZolty ? Color.FromRgb(241, 196, 15) : Color.FromRgb(231, 76, 60);
                double displayPct = Math.Min((double)procentRealizacji / maxScale * 100, 100);

                var barBg = new Border { Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)), CornerRadius = new CornerRadius(20), Height = 70 };
                var barGrid = new Grid();
                var fillGrid = new Grid();
                fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(displayPct, 0.1), GridUnitType.Star) });
                fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(100 - displayPct, 0.1), GridUnitType.Star) });
                fillGrid.Children.Add(new Border { Background = new SolidColorBrush(barColor), CornerRadius = new CornerRadius(20, displayPct >= 99 ? 20 : 0, displayPct >= 99 ? 20 : 0, 20) });
                barGrid.Children.Add(fillGrid);

                // Marker dla CEL Plan (biały)
                var markerGridPlan = new Grid();
                markerGridPlan.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(markerPosPlan, GridUnitType.Star) });
                markerGridPlan.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                markerGridPlan.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - markerPosPlan, GridUnitType.Star) });
                markerGridPlan.Children.Add(new Border { Width = 4, Height = 70, Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Right });
                barGrid.Children.Add(markerGridPlan);

                // Marker dla CEL Plan+Stan (zielony) - tylko gdy Stan > 0
                if (maDwaCele && markerPosPlanStan > markerPosPlan)
                {
                    var markerGridPlanStan = new Grid();
                    markerGridPlanStan.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(markerPosPlanStan, GridUnitType.Star) });
                    markerGridPlanStan.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    markerGridPlanStan.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - markerPosPlanStan, GridUnitType.Star) });
                    markerGridPlanStan.Children.Add(new Border { Width = 4, Height = 70, Background = new SolidColorBrush(Color.FromRgb(26, 188, 156)), HorizontalAlignment = HorizontalAlignment.Right });
                    barGrid.Children.Add(markerGridPlanStan);
                }

                // Tekst na pasku: procent + ilość kg
                var barTextStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                barTextStack.Children.Add(new TextBlock { Text = $"{procentRealizacji:N0}%", FontSize = 32, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
                barTextStack.Children.Add(new TextBlock { Text = $"{zamLubWyd:N0} kg", FontSize = 18, Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)), HorizontalAlignment = HorizontalAlignment.Center });
                barGrid.Children.Add(barTextStack);
                barBg.Child = barGrid;
                progressStack.Children.Add(barBg);

                // Etykiety pod paskiem
                var labelGrid = new Grid { Margin = new Thickness(0, 5, 0, 0) };
                labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(markerPosPlan, GridUnitType.Star) });
                labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - markerPosPlan, GridUnitType.Star) });
                labelGrid.Children.Add(new TextBlock { Text = "0 kg", FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)) });
                var celLbl = new TextBlock { Text = $"▼ CEL {celLabel} {cel:N0} kg", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(celLbl, 0);
                labelGrid.Children.Add(celLbl);
                progressStack.Children.Add(labelGrid);

                // Druga linia etykiet - CEL Plan+Stan (tylko gdy Stan > 0)
                if (maDwaCele)
                {
                    var labelGrid2 = new Grid { Margin = new Thickness(0, 2, 0, 0) };
                    labelGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(markerPosPlanStan, GridUnitType.Star) });
                    labelGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - markerPosPlanStan, GridUnitType.Star) });
                    var celLbl2 = new TextBlock { Text = $"▼ CEL {celLabel}+STAN {celPlanStan:N0} kg", FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(26, 188, 156)), HorizontalAlignment = HorizontalAlignment.Right };
                    Grid.SetColumn(celLbl2, 0);
                    labelGrid2.Children.Add(celLbl2);
                    progressStack.Children.Add(labelGrid2);
                }

                progressBorder.Child = progressStack;
                leftPanel.Children.Add(progressBorder);
                Grid.SetColumn(leftPanel, 0);
                contentGrid.Children.Add(leftPanel);

                // === PRAWA STRONA - ODBIORCY ===
                var rightScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                var rightPanel = new StackPanel();
                var odbiorcy = currentData.Odbiorcy.OrderByDescending(o => o.Zamowione + o.Wydane).ToList();

                // Oblicz sumę dla % udziału
                decimal sumaGlowna = viewUseWydania
                    ? odbiorcy.Sum(o => o.Wydane)
                    : odbiorcy.Sum(o => o.Zamowione);

                rightPanel.Children.Add(new TextBlock { Text = $"👥 ODBIORCY ({odbiorcy.Count})", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 12) });
                var hdrRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                hdrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Nazwa
                hdrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) }); // ZAM
                hdrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) }); // WYD
                hdrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // %

                hdrRow.Children.Add(new TextBlock { Text = "ODBIORCA", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)) });
                var hZam = new TextBlock { Text = "ZAM", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(230, 126, 34)), HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(hZam, 1);
                hdrRow.Children.Add(hZam);
                var hWyd = new TextBlock { Text = "WYD", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43)), HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(hWyd, 2);
                hdrRow.Children.Add(hWyd);
                var hPct = new TextBlock { Text = "%", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)), HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(hPct, 3);
                hdrRow.Children.Add(hPct);
                rightPanel.Children.Add(hdrRow);
                rightPanel.Children.Add(new Border { Height = 2, Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)), Margin = new Thickness(0, 0, 0, 10) });

                foreach (var odb in odbiorcy)
                {
                    bool bezZam = odb.Zamowione == 0 && odb.Wydane > 0;
                    var odbRef = odb;
                    var currentProductId = currentData.Id;
                    var currentDate = _selectedDate;

                    // Sprawdź czy jest symulowana redukcja dla tego odbiorcy
                    var reductionKey = (currentProductId, odbRef.KlientId);
                    decimal odbReduction = simulatedReductions.TryGetValue(reductionKey, out var red) ? red : 0;
                    decimal odbZamowioneSymulowane = odbRef.Zamowione + odbReduction;
                    bool maRedukcje = odbReduction != 0;

                    var row = new Border
                    {
                        Background = maRedukcje
                            ? new SolidColorBrush(Color.FromRgb(60, 50, 30)) // żółtawe tło dla symulacji
                            : bezZam ? new SolidColorBrush(Color.FromRgb(60, 35, 35)) : Brushes.Transparent,
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(8, 5, 8, 5),
                        Margin = new Thickness(0, 1, 0, 1),
                        Cursor = System.Windows.Input.Cursors.Hand
                    };
                    var rowGrid = new Grid();
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

                    // Nazwa odbiorcy (z ikonką jeśli ma redukcję)
                    var namePanel = new StackPanel { Orientation = Orientation.Horizontal };
                    if (maRedukcje)
                        namePanel.Children.Add(new TextBlock { Text = "📉 ", FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
                    namePanel.Children.Add(new TextBlock { Text = odbRef.NazwaOdbiorcy, FontSize = 16, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
                    rowGrid.Children.Add(namePanel);

                    // Zamówienia (z symulacją)
                    var zamStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                    if (maRedukcje)
                    {
                        zamStack.Children.Add(new TextBlock { Text = $"{odbRef.Zamowione:N0}", FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(150, 100, 50)), TextDecorations = TextDecorations.Strikethrough, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
                        zamStack.Children.Add(new TextBlock { Text = $"{odbZamowioneSymulowane:N0}", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15)), VerticalAlignment = VerticalAlignment.Center });
                    }
                    else
                    {
                        zamStack.Children.Add(new TextBlock { Text = odbRef.Zamowione > 0 ? $"{odbRef.Zamowione:N0}" : "-", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(230, 126, 34)) });
                    }
                    Grid.SetColumn(zamStack, 1);
                    rowGrid.Children.Add(zamStack);

                    // Wydania
                    var vWyd = new TextBlock { Text = odbRef.Wydane > 0 ? $"{odbRef.Wydane:N0}" : "-", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43)), HorizontalAlignment = HorizontalAlignment.Right };
                    Grid.SetColumn(vWyd, 2);
                    rowGrid.Children.Add(vWyd);

                    // % udziału (zależny od trybu ZAM/WYD) - używa symulowanej wartości
                    decimal odbWartosc = viewUseWydania ? odbRef.Wydane : odbZamowioneSymulowane;
                    decimal procent = sumaGlowna > 0 ? (odbWartosc / sumaGlowna) * 100 : 0;
                    var vPct = new TextBlock { Text = procent > 0 ? $"{procent:N1}%" : "-", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)), HorizontalAlignment = HorizontalAlignment.Right };
                    Grid.SetColumn(vPct, 3);
                    rowGrid.Children.Add(vPct);

                    row.Child = rowGrid;

                    // Hover effect
                    var defaultBg = row.Background;
                    row.MouseEnter += (s, e) => row.Background = new SolidColorBrush(Color.FromRgb(60, 80, 100));
                    row.MouseLeave += (s, e) => row.Background = defaultBg;

                    // === MENU KONTEKSTOWE (prawy przycisk) ===
                    var contextMenu = new ContextMenu();

                    // Edytuj zamówienie
                    var menuEdit = new MenuItem { Header = "✏️ Edytuj zamówienie", IsEnabled = odbRef.Zamowione > 0 };
                    menuEdit.Click += async (s, e) =>
                    {
                        if (odbRef.KlientId > 0 && odbRef.Zamowione > 0)
                        {
                            await OpenOrderEditorForClientAsync(odbRef.KlientId, currentProductId, odbRef.NazwaOdbiorcy, currentDate, refreshContent);
                        }
                    };
                    contextMenu.Items.Add(menuEdit);

                    contextMenu.Items.Add(new Separator());

                    // Zmniejsz ilość (symulacja)
                    var menuReduce = new MenuItem { Header = "📉 Zmniejsz ilość (symulacja)", IsEnabled = odbRef.Zamowione > 0 };
                    menuReduce.Click += (s, e) =>
                    {
                        ShowReductionDialog(odbRef.NazwaOdbiorcy, odbRef.Zamowione, odbReduction, (newReduction) =>
                        {
                            if (newReduction == 0)
                                simulatedReductions.Remove(reductionKey);
                            else
                                simulatedReductions[reductionKey] = newReduction;
                            refreshContent();
                        });
                    };
                    contextMenu.Items.Add(menuReduce);

                    // Resetuj symulację dla tego odbiorcy
                    if (maRedukcje)
                    {
                        var menuReset = new MenuItem { Header = "🔄 Resetuj symulację" };
                        menuReset.Click += (s, e) =>
                        {
                            simulatedReductions.Remove(reductionKey);
                            refreshContent();
                        };
                        contextMenu.Items.Add(menuReset);
                    }

                    row.ContextMenu = contextMenu;

                    // Tooltip z informacją
                    string tooltip = "Prawy przycisk myszy → opcje";
                    if (maRedukcje)
                        tooltip = $"Symulowana redukcja: {odbReduction:N0} kg\n{tooltip}";
                    row.ToolTip = tooltip;

                    rightPanel.Children.Add(row);
                }
                rightScroll.Content = rightPanel;
                Grid.SetColumn(rightScroll, 2);
                contentGrid.Children.Add(rightScroll);
                Grid.SetRow(contentGrid, 1);
                mainGrid.Children.Add(contentGrid);

                // === STOPKA ===
                var footerPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                var prevBtn = new Button { Content = "◀ Poprzedni", FontSize = 14, Padding = new Thickness(15, 8, 15, 8), Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 10, 0) };
                prevBtn.Click += (s, e) => { viewIndex = (viewIndex - 1 + _productDataList.Count) % _productDataList.Count; refreshContent(); };
                footerPanel.Children.Add(prevBtn);
                var nextBtn = new Button { Content = "Następny ▶", FontSize = 14, Padding = new Thickness(15, 8, 15, 8), Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 20, 0) };
                nextBtn.Click += (s, e) => { viewIndex = (viewIndex + 1) % _productDataList.Count; refreshContent(); };
                footerPanel.Children.Add(nextBtn);

                var slideshowBtn = new Button { Content = slideshowActive ? $"⏸ {countdown}s" : "▶ Auto (10s)", FontSize = 14, Padding = new Thickness(15, 8, 15, 8), Background = new SolidColorBrush(slideshowActive ? Color.FromRgb(231, 76, 60) : Color.FromRgb(39, 174, 96)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 15, 0) };
                slideshowBtnRef = slideshowBtn;
                slideshowBtn.Click += (s, e) =>
                {
                    if (!slideshowActive)
                    {
                        slideshowActive = true;
                        countdown = 10;
                        slideshowBtn.Background = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                        slideshowBtn.Content = $"⏸ {countdown}s";
                        slideshowTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                        slideshowTimer.Tick += (ts, te) =>
                        {
                            countdown--;
                            if (countdown <= 0)
                            {
                                countdown = 10;
                                viewIndex = (viewIndex + 1) % _productDataList.Count;
                                refreshContent();
                            }
                            else if (slideshowBtnRef != null)
                                slideshowBtnRef.Content = $"⏸ {countdown}s";
                        };
                        slideshowTimer.Start();
                    }
                    else
                    {
                        slideshowActive = false;
                        slideshowTimer?.Stop();
                        slideshowBtn.Background = new SolidColorBrush(Color.FromRgb(39, 174, 96));
                        slideshowBtn.Content = "▶ Auto (10s)";
                    }
                };
                footerPanel.Children.Add(slideshowBtn);

                // Panel symulacji - zawsze widoczny gdy są aktywne
                bool anySimulations = simulatedReductions.Any();
                if (anySimulations)
                {
                    // Przycisk podsumowania symulacji - DUŻY I WIDOCZNY
                    var summaryBtn = new Button
                    {
                        Content = $"📊 PODSUMOWANIE SYMULACJI ({simulatedReductions.Count})",
                        FontSize = 16,
                        Padding = new Thickness(25, 12, 25, 12),
                        Background = new SolidColorBrush(Color.FromRgb(155, 89, 182)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(3),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                        Margin = new Thickness(0, 0, 15, 0),
                        FontWeight = FontWeights.Bold,
                        Cursor = System.Windows.Input.Cursors.Hand
                    };
                    summaryBtn.Click += (s, e) =>
                    {
                        ShowSimulationSummaryDialog(simulatedReductions, _productDataList, refreshContent);
                    };
                    footerPanel.Children.Add(summaryBtn);

                    // Przycisk reset
                    var resetSimBtn = new Button
                    {
                        Content = "🔄 Resetuj wszystko",
                        FontSize = 14,
                        Padding = new Thickness(15, 8, 15, 8),
                        Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(0),
                        Margin = new Thickness(0, 0, 15, 0),
                        FontWeight = FontWeights.SemiBold,
                        Cursor = System.Windows.Input.Cursors.Hand
                    };
                    resetSimBtn.Click += (s, e) =>
                    {
                        simulatedReductions.Clear();
                        refreshContent();
                    };
                    footerPanel.Children.Add(resetSimBtn);
                }

                var productInfoLabel = new TextBlock { Text = $"[{viewIndex + 1}/{_productDataList.Count}]", FontSize = 16, Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 15, 0) };
                productInfoLabelRef = productInfoLabel;
                footerPanel.Children.Add(productInfoLabel);

                // Informacja o symulacjach w stopce
                string footerText = "ESC = zamknij  |  ← → nawigacja  |  PPM na odbiorcy = opcje";
                if (anySimulations)
                    footerText = $"📉 AKTYWNE SYMULACJE: {simulatedReductions.Count}  |  " + footerText;
                footerPanel.Children.Add(new TextBlock { Text = footerText, FontSize = 14, Foreground = new SolidColorBrush(anySimulations ? Color.FromRgb(241, 196, 15) : Color.FromRgb(100, 100, 100)), VerticalAlignment = VerticalAlignment.Center });
                Grid.SetRow(footerPanel, 2);
                mainGrid.Children.Add(footerPanel);

                mainContainer.Children.Add(mainGrid);
            };

            // Pierwsze wywołanie
            refreshContent();

            // Obsługa klawiatury
            dialog.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape) { slideshowTimer?.Stop(); dialog.Close(); }
                else if (e.Key == System.Windows.Input.Key.Left) { viewIndex = (viewIndex - 1 + _productDataList.Count) % _productDataList.Count; refreshContent(); }
                else if (e.Key == System.Windows.Input.Key.Right) { viewIndex = (viewIndex + 1) % _productDataList.Count; refreshContent(); }
            };

            // Auto slideshow
            if (autoSlideshow && _productDataList.Count > 1)
                slideshowBtnRef?.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            dialog.Show();
            dialog.Focus();
        }

        /// <summary>
        /// Tworzy box z wartością dla widoku prezentacyjnego - większe czcionki
        /// </summary>
        private StackPanel CreateValueBox(string label, string value, Color color)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 20,
                Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            return stack;
        }

        /// <summary>
        /// Tworzy duży box ze statystyką (do widoku prezentacyjnego)
        /// </summary>
        private Border CreateLargeStatBox(string label, string value, Color color)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(39, 55, 70)),
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(20, 15, 20, 15),
                Margin = new Thickness(5)
            };

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 36,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            border.Child = stack;
            return border;
        }

        /// <summary>
        /// Tworzy box podsumowania (aktywni/do odzyskania)
        /// </summary>
        private StackPanel CreateSummaryBox(string label, string value, Color color)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10) };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 42,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            return stack;
        }

        /// <summary>
        /// Tworzy nagłówek tabeli historii klientów
        /// </summary>
        private Grid CreateHistoryTableHeader()
        {
            var headerRow = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

            var headers = new[] { "Klient", "Suma kg", "Zam.", "Średnia", "Ost. zam." };
            for (int i = 0; i < headers.Length; i++)
            {
                var txt = new TextBlock
                {
                    Text = headers[i],
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141))
                };
                Grid.SetColumn(txt, i);
                headerRow.Children.Add(txt);
            }
            return headerRow;
        }

        /// <summary>
        /// Tworzy wiersz klienta w tabeli historii
        /// </summary>
        private Grid CreateHistoryClientRow(HistoricalClientData client, Color accentColor)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

            var name = new TextBlock
            {
                Text = client.NazwaKlienta,
                FontSize = 10,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = client.NazwaKlienta
            };
            Grid.SetColumn(name, 0);
            row.Children.Add(name);

            var suma = new TextBlock
            {
                Text = $"{client.SumaZamowien:N0}",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(accentColor)
            };
            Grid.SetColumn(suma, 1);
            row.Children.Add(suma);

            var liczba = new TextBlock
            {
                Text = $"{client.LiczbaZamowien}x",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219))
            };
            Grid.SetColumn(liczba, 2);
            row.Children.Add(liczba);

            var srednia = new TextBlock
            {
                Text = $"{client.SredniaZamowienie:N0}",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141))
            };
            Grid.SetColumn(srednia, 3);
            row.Children.Add(srednia);

            var ostatnie = new TextBlock
            {
                Text = client.DniOdOstatniego == 0 ? "dziś" :
                       client.DniOdOstatniego == 1 ? "wczoraj" :
                       $"{client.DniOdOstatniego} dni temu",
                FontSize = 9,
                Foreground = new SolidColorBrush(
                    client.DniOdOstatniego <= 7 ? Color.FromRgb(39, 174, 96) :
                    client.DniOdOstatniego <= 30 ? Color.FromRgb(52, 152, 219) :
                    client.DniOdOstatniego <= 60 ? Color.FromRgb(230, 126, 34) :
                    Color.FromRgb(192, 57, 43))
            };
            Grid.SetColumn(ostatnie, 4);
            row.Children.Add(ostatnie);

            return row;
        }

        /// <summary>
        /// Tworzy box ze statystyką
        /// </summary>
        private Border CreateStatBox(string label, string value, Color color)
        {
            var border = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(3)
            };

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            border.Child = stack;
            return border;
        }

        private void AddProgressBar(StackPanel parent, string label, decimal percent, Color color)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });

            var lbl = new TextBlock { Text = label, FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, 0);
            row.Children.Add(lbl);

            var barBg = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)),
                CornerRadius = new CornerRadius(4),
                Height = 16
            };
            barBg.Child = new Border
            {
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = (double)percent * 1.5
            };
            Grid.SetColumn(barBg, 1);
            row.Children.Add(barBg);

            var val = new TextBlock
            {
                Text = $"{percent:N0}%",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(val, 2);
            row.Children.Add(val);

            parent.Children.Add(row);
        }

        private void AddSummaryRow(StackPanel parent, string label, string value, string? colorHex = null)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lbl = new TextBlock { Text = label, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)) };
            Grid.SetColumn(lbl, 0);
            row.Children.Add(lbl);

            var val = new TextBlock
            {
                Text = value,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = colorHex != null ? (Brush)new BrushConverter().ConvertFromString(colorHex)! : Brushes.Black
            };
            Grid.SetColumn(val, 1);
            row.Children.Add(val);

            parent.Children.Add(row);
        }

        /// <summary>
        /// Uproszczony panel dla Pani Joli - duże przyciski, prosta nawigacja, bez symulacji.
        /// 3 tablice odbiorców, 2 kamery, działający datepicker.
        /// </summary>
        private void ShowSimplifiedPanelJola(ProductData data, int currentIndex = -1)
        {
            if (currentIndex < 0)
            {
                currentIndex = _productDataList.FindIndex(p => p.Id == data.Id);
                if (currentIndex < 0) currentIndex = 0;
            }

            int viewIndex = currentIndex;
            bool isAutoPlay = false;
            System.Windows.Threading.DispatcherTimer? autoTimer = null;
            System.Windows.Threading.DispatcherTimer? clockTimer = null;
            int autoCountdown = 15; // Countdown sekund do następnego produktu
            TextBlock? clockText = null; // Referencja do zegara
            TextBlock? countdownText = null; // Referencja do countdown
            System.Windows.Controls.ProgressBar? countdownBar = null; // Pasek postępu

            var dialog = new Window
            {
                Title = "Panel Pani Joli",
                WindowState = WindowState.Maximized,
                WindowStyle = WindowStyle.None,
                Background = new SolidColorBrush(Color.FromRgb(25, 30, 35)),
                ResizeMode = ResizeMode.NoResize
            };

            // Zatrzymaj timery przy zamknięciu okna
            dialog.Closed += (s, e) =>
            {
                autoTimer?.Stop();
                clockTimer?.Stop();

                // Jeśli uruchomiono bezpośrednio z menu - zamknij też DashboardWindow
                if (_openPanelJolaOnStart)
                {
                    this.Close();
                }
            };

            var mainContainer = new Grid();
            dialog.Content = mainContainer;

            Action refreshContent = null!;
            refreshContent = () =>
            {
                var currentData = _productDataList[viewIndex];
                mainContainer.Children.Clear();

                // Główny layout: 2 kolumny [Lewa (info+nawigacja) | Prawa (tablice + kamery)]
                var mainGrid = new Grid { Margin = new Thickness(10) };
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) }); // Lewa - info + nawigacja
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Prawa - tablice + kamery

                // === LEWA KOLUMNA - INFO + NAWIGACJA ===
                var leftPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };

                // === ZEGAR ===
                var clockBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8, 5, 8, 5),
                    Margin = new Thickness(0, 0, 0, 10),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                clockText = new TextBlock
                {
                    Text = DateTime.Now.ToString("HH:mm:ss"),
                    FontSize = 28,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                clockBorder.Child = clockText;
                leftPanel.Children.Add(clockBorder);

                // Uruchom timer zegara (jeśli jeszcze nie działa)
                if (clockTimer == null)
                {
                    clockTimer = new System.Windows.Threading.DispatcherTimer();
                    clockTimer.Interval = TimeSpan.FromSeconds(1);
                    clockTimer.Tick += (ts, te) =>
                    {
                        if (clockText != null)
                            clockText.Text = DateTime.Now.ToString("HH:mm:ss");

                        // Aktualizuj countdown jeśli AUTO jest włączone
                        if (isAutoPlay && countdownText != null && countdownBar != null)
                        {
                            autoCountdown--;
                            if (autoCountdown <= 0)
                                autoCountdown = 15;
                            countdownText.Text = $"{autoCountdown}s";
                            countdownBar.Value = autoCountdown;
                        }
                    };
                    clockTimer.Start();
                }

                // Zdjęcie produktu
                var productImage = GetProductImage(currentData.Id);
                var imageBorder = new Border
                {
                    Width = 140, Height = 140,
                    CornerRadius = new CornerRadius(10),
                    ClipToBounds = true,
                    Margin = new Thickness(0, 0, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                if (productImage != null)
                {
                    var img = new System.Windows.Controls.Image { Source = productImage, Width = 136, Height = 136, Stretch = Stretch.Uniform };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    imageBorder.Child = img;
                }
                else
                {
                    imageBorder.Background = new SolidColorBrush(Color.FromRgb(52, 73, 94));
                    imageBorder.Child = new TextBlock { Text = "📦", FontSize = 40, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)) };
                }
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

                // BILANS
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

                // Kafelki pionowo (jeden pod drugim)
                var planBorder = CreateJolaStatBoxCompact(uzyjFakt ? "FAKT" : "PLAN", $"{cel:N0}", uzyjFakt ? Color.FromRgb(155, 89, 182) : Color.FromRgb(52, 152, 219));
                leftPanel.Children.Add(planBorder);

                var stanBorder = CreateJolaStatBoxCompact("STAN", $"{currentData.Stan:N0}", Color.FromRgb(26, 188, 156));
                leftPanel.Children.Add(stanBorder);

                var zamBorder = CreateJolaStatBoxCompact("ZAM.", $"{currentData.Zamowienia:N0}", Color.FromRgb(230, 126, 34));
                leftPanel.Children.Add(zamBorder);

                // === DATEPICKER z dniem tygodnia ===
                var datePanel = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
                datePanel.Children.Add(new TextBlock { Text = "DATA:", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)), HorizontalAlignment = HorizontalAlignment.Center });

                var dateGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
                dateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                dateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                dateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var btnDatePrev = new Button { Content = "◀", FontSize = 16, Width = 32, Height = 32, Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
                var dateTxt = new TextBlock { Text = _selectedDate.ToString("dd.MM.yyyy"), FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
                var btnDateNext = new Button { Content = "▶", FontSize = 16, Width = 32, Height = 32, Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };

                // Zmiana daty - przeładowanie danych
                btnDatePrev.Click += async (s, e) => {
                    _selectedDate = _selectedDate.AddDays(-1);
                    await LoadDataAsync();
                    refreshContent();
                };
                btnDateNext.Click += async (s, e) => {
                    _selectedDate = _selectedDate.AddDays(1);
                    await LoadDataAsync();
                    refreshContent();
                };

                Grid.SetColumn(btnDatePrev, 0); Grid.SetColumn(dateTxt, 1); Grid.SetColumn(btnDateNext, 2);
                dateGrid.Children.Add(btnDatePrev); dateGrid.Children.Add(dateTxt); dateGrid.Children.Add(btnDateNext);
                datePanel.Children.Add(dateGrid);

                // Dzień tygodnia
                string dzienTygodnia = _selectedDate.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));
                datePanel.Children.Add(new TextBlock { Text = dzienTygodnia, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });

                // Przycisk DZIŚ
                var btnDzis = new Button { Content = "DZIŚ", FontSize = 12, FontWeight = FontWeights.Bold, Padding = new Thickness(15, 5, 15, 5), Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 5, 0, 0), Cursor = System.Windows.Input.Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Center };
                btnDzis.Click += async (s, e) => {
                    _selectedDate = DateTime.Today;
                    await LoadDataAsync();
                    refreshContent();
                };
                datePanel.Children.Add(btnDzis);

                leftPanel.Children.Add(datePanel);

                // === NAWIGACJA PRODUKTÓW ===
                var navPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };

                var btnPrev = new Button { Content = "▲", FontSize = 36, Width = 80, Height = 65, Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 0, 5), Cursor = System.Windows.Input.Cursors.Hand };
                btnPrev.Click += (s, e) => { viewIndex = (viewIndex - 1 + _productDataList.Count) % _productDataList.Count; refreshContent(); };
                navPanel.Children.Add(btnPrev);

                navPanel.Children.Add(new TextBlock { Text = $"{viewIndex + 1} z {_productDataList.Count}", FontSize = 16, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 3) });

                var btnNext = new Button { Content = "▼", FontSize = 36, Width = 80, Height = 65, Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 5, 0, 0), Cursor = System.Windows.Input.Cursors.Hand };
                btnNext.Click += (s, e) => { viewIndex = (viewIndex + 1) % _productDataList.Count; refreshContent(); };
                navPanel.Children.Add(btnNext);

                // Przycisk AUTO (cykliczne przesuwanie co 15 sekund)
                var btnAuto = new Button
                {
                    Content = isAutoPlay ? "⏹ STOP" : "▶ AUTO",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Width = 80,
                    Padding = new Thickness(8, 10, 8, 10),
                    Background = new SolidColorBrush(isAutoPlay ? Color.FromRgb(231, 76, 60) : Color.FromRgb(39, 174, 96)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(0, 8, 0, 0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                btnAuto.Click += (s, e) =>
                {
                    isAutoPlay = !isAutoPlay;
                    if (isAutoPlay)
                    {
                        // Uruchom timer i reset countdown
                        autoCountdown = 15;
                        autoTimer = new System.Windows.Threading.DispatcherTimer();
                        autoTimer.Interval = TimeSpan.FromSeconds(15);
                        autoTimer.Tick += (ts, te) =>
                        {
                            viewIndex = (viewIndex + 1) % _productDataList.Count;
                            autoCountdown = 15; // Reset countdown
                            refreshContent();
                        };
                        autoTimer.Start();
                    }
                    else
                    {
                        // Zatrzymaj timer
                        autoTimer?.Stop();
                        autoTimer = null;
                    }
                    refreshContent();
                };
                navPanel.Children.Add(btnAuto);

                // === COUNTDOWN BAR (widoczny tylko gdy AUTO jest włączone) ===
                if (isAutoPlay)
                {
                    var countdownPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };

                    // Pasek postępu
                    countdownBar = new System.Windows.Controls.ProgressBar
                    {
                        Width = 80,
                        Height = 8,
                        Minimum = 0,
                        Maximum = 15,
                        Value = autoCountdown,
                        Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                        Background = new SolidColorBrush(Color.FromRgb(44, 62, 80))
                    };
                    countdownPanel.Children.Add(countdownBar);

                    // Tekst z sekundami
                    countdownText = new TextBlock
                    {
                        Text = $"{autoCountdown}s",
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    countdownPanel.Children.Add(countdownText);

                    navPanel.Children.Add(countdownPanel);
                }

                // Przycisk ZAMKNIJ
                var btnClose = new Button { Content = "✕ ZAMKNIJ", FontSize = 14, FontWeight = FontWeights.Bold, Padding = new Thickness(12, 8, 12, 8), Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 12, 0, 0), Cursor = System.Windows.Input.Cursors.Hand };
                btnClose.Click += (s, e) => dialog.Close();
                navPanel.Children.Add(btnClose);

                leftPanel.Children.Add(navPanel);

                Grid.SetColumn(leftPanel, 0);
                mainGrid.Children.Add(leftPanel);

                // === PRAWA STRONA - 3 TABLICE + 2 KAMERY ===
                var rightPanel = new Grid { Margin = new Thickness(5, 0, 0, 0) };
                rightPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Tablice
                rightPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Kamery

                // === 3 TABLICE ODBIORCÓW ===
                var tabliceGrid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
                tabliceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Tablica 1
                tabliceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Tablica 2
                tabliceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Tablica 3

                var odbiorcy = currentData.Odbiorcy.OrderByDescending(o => o.Zamowione).ToList();
                int maxRowsPerTable = 12; // Max wierszy na tablicę - mniej żeby nie ucinało

                // Podziel odbiorców na 2 tablice (bo pierwsza kolumna to produkty)
                var tablica1 = odbiorcy.Take(maxRowsPerTable).ToList();
                var tablica2 = odbiorcy.Skip(maxRowsPerTable).Take(maxRowsPerTable).ToList();

                // === KOLUMNA 1: WIELKIE KAFLE PRODUKTÓW ===
                var produktyPanel = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(35, 40, 48)),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 5, 0),
                    Padding = new Thickness(5)
                };

                // Grid z równymi wierszami dla każdego produktu - wypełnia całą przestrzeń
                var produktyGrid = new Grid();
                int maxProdukty = Math.Min(5, _productDataList.Count);
                for (int i = 0; i < maxProdukty; i++)
                {
                    produktyGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                }

                // Wyświetl produkty jako wielkie kafle - tylko obraz i tekst z cieniem
                for (int i = 0; i < maxProdukty; i++)
                {
                    var prod = _productDataList[i];
                    int prodIndex = i; // Capture for closure
                    bool isSelected = (i == viewIndex);

                    // Duży kafel produktu - obraz + tekst
                    var prodImage = GetProductImage(prod.Id);
                    var prodBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(80, 90, 100)),
                        CornerRadius = new CornerRadius(12),
                        ClipToBounds = true,
                        BorderBrush = new SolidColorBrush(isSelected ? Color.FromRgb(52, 152, 219) : Color.FromRgb(100, 110, 120)),
                        BorderThickness = new Thickness(isSelected ? 5 : 2),
                        Margin = new Thickness(3),
                        Cursor = System.Windows.Input.Cursors.Hand
                    };

                    var tileGrid = new Grid();
                    if (prodImage != null)
                    {
                        var tileImg = new System.Windows.Controls.Image
                        {
                            Source = prodImage,
                            Stretch = Stretch.Uniform,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        RenderOptions.SetBitmapScalingMode(tileImg, BitmapScalingMode.HighQuality);
                        tileGrid.Children.Add(tileImg);
                    }

                    // Tylko tekst z cieniem na dole
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
                    // Efekt cienia
                    nameText.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black,
                        Direction = 315,
                        ShadowDepth = 3,
                        Opacity = 0.9,
                        BlurRadius = 6
                    };

                    tileGrid.Children.Add(nameText);
                    prodBorder.Child = tileGrid;
                    prodBorder.MouseLeftButtonDown += (s, e) => { viewIndex = prodIndex; refreshContent(); };

                    Grid.SetRow(prodBorder, i);
                    produktyGrid.Children.Add(prodBorder);
                }

                produktyPanel.Child = produktyGrid;
                Grid.SetColumn(produktyPanel, 0);
                tabliceGrid.Children.Add(produktyPanel);

                // === KOLUMNA 2: TABELA ODBIORCÓW 1 ===
                var tab1 = CreateJolaTable(tablica1, 1, 0, 0, false);
                Grid.SetColumn(tab1, 1);
                tabliceGrid.Children.Add(tab1);

                // === KOLUMNA 3: TABELA ODBIORCÓW 2 ===
                var tab2 = CreateJolaTable(tablica2, maxRowsPerTable + 1, 0, 0, false);
                Grid.SetColumn(tab2, 2);
                tabliceGrid.Children.Add(tab2);

                Grid.SetRow(tabliceGrid, 0);
                rightPanel.Children.Add(tabliceGrid);

                // === 2 KAMERY - kliknięcie otwiera PEŁNY EKRAN (nowe okno) ===
                var camerasGrid = new Grid { Margin = new Thickness(0, 5, 0, 0) };
                camerasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                camerasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Funkcja do otwierania kamery na pełny ekran (nowe okno)
                Action<int> openFullscreenCamera = (cameraNum) =>
                {
                    var fullscreenWindow = new Window
                    {
                        Title = $"Kamera {cameraNum} - Pełny ekran",
                        WindowState = WindowState.Maximized,
                        WindowStyle = WindowStyle.None,
                        Background = Brushes.Black,
                        ResizeMode = ResizeMode.NoResize,
                        Topmost = true // Na wierzchu wszystkiego
                    };

                    var fullscreenGrid = new Grid();

                    // Główna zawartość kamery
                    var cameraContent = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    cameraContent.Children.Add(new TextBlock { Text = "📹", FontSize = 200, HorizontalAlignment = HorizontalAlignment.Center });
                    cameraContent.Children.Add(new TextBlock { Text = $"KAMERA {cameraNum}", FontSize = 72, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) });
                    cameraContent.Children.Add(new TextBlock { Text = "PEŁNY EKRAN", FontSize = 28, Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 120)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) });
                    cameraContent.Children.Add(new TextBlock { Text = "Kliknij gdziekolwiek lub naciśnij ESC aby zamknąć", FontSize = 18, Foreground = new SolidColorBrush(Color.FromRgb(80, 90, 100)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) });
                    fullscreenGrid.Children.Add(cameraContent);

                    // Przycisk zamknięcia w górnym prawym rogu
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
                    closeBtn.Child = new TextBlock { Text = "✕ ZAMKNIJ", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
                    closeBtn.MouseLeftButtonDown += (s, e) => { fullscreenWindow.Close(); e.Handled = true; };
                    fullscreenGrid.Children.Add(closeBtn);

                    fullscreenWindow.Content = fullscreenGrid;

                    // Kliknięcie w tło zamyka okno
                    fullscreenGrid.MouseLeftButtonDown += (s, e) => fullscreenWindow.Close();

                    // ESC zamyka okno
                    fullscreenWindow.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) fullscreenWindow.Close(); };

                    fullscreenWindow.ShowDialog();
                };

                var camera1Border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 35, 40)),
                    CornerRadius = new CornerRadius(10),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(50, 55, 60)),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 3, 0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                var camera1Content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                camera1Content.Children.Add(new TextBlock { Text = "📹", FontSize = 50, HorizontalAlignment = HorizontalAlignment.Center });
                camera1Content.Children.Add(new TextBlock { Text = "KAMERA 1", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(70, 80, 90)), HorizontalAlignment = HorizontalAlignment.Center });
                camera1Content.Children.Add(new TextBlock { Text = "Kliknij = PEŁNY EKRAN", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) });
                camera1Border.Child = camera1Content;
                camera1Border.MouseLeftButtonDown += (s, e) => openFullscreenCamera(1);
                Grid.SetColumn(camera1Border, 0);
                camerasGrid.Children.Add(camera1Border);

                var camera2Border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 35, 40)),
                    CornerRadius = new CornerRadius(10),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(50, 55, 60)),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(3, 0, 0, 0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
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

                mainContainer.Children.Add(mainGrid);
            };

            refreshContent();

            // Klawiatura
            dialog.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape) dialog.Close();
                else if (e.Key == System.Windows.Input.Key.Up || e.Key == System.Windows.Input.Key.Left)
                {
                    viewIndex = (viewIndex - 1 + _productDataList.Count) % _productDataList.Count;
                    refreshContent();
                }
                else if (e.Key == System.Windows.Input.Key.Down || e.Key == System.Windows.Input.Key.Right)
                {
                    viewIndex = (viewIndex + 1) % _productDataList.Count;
                    refreshContent();
                }
            };

            dialog.ShowDialog();
        }

        /// <summary>
        /// Tworzy kafelek ze statystyką dla panelu Pani Joli
        /// </summary>
        private Border CreateJolaStatBox(string label, string value, Color color)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(15, 10, 15, 10),
                Margin = new Thickness(3)
            };
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = label, FontSize = 14, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(new TextBlock { Text = value, FontSize = 28, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
            border.Child = stack;
            return border;
        }

        /// <summary>
        /// Tworzy mniejszy kafelek ze statystyką dla panelu Pani Joli (wersja kompaktowa)
        /// </summary>
        private Border CreateJolaStatBoxCompact(string label, string value, Color color)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(2)
            };
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(new TextBlock { Text = value, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
            border.Child = stack;
            return border;
        }

        /// <summary>
        /// Tworzy pojedynczą tablicę odbiorców dla panelu Pani Joli
        /// </summary>
        private Border CreateJolaTable(List<OdbiorcaZamowienie> odbiorcy, int startLp, decimal sumaZam, decimal sumaWyd, bool showSuma)
        {
            var tableBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 40, 50)),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(2),
                Padding = new Thickness(0)
            };

            var tableGrid = new Grid();
            tableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Nagłówek
            tableGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Lista
            if (showSuma)
                tableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Suma

            // Nagłówek
            var headerBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)), Padding = new Thickness(8, 6, 8, 6) };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

            headerGrid.Children.Add(new TextBlock { Text = "#", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 10, 0) });
            var hdrNazwa = new TextBlock { Text = "ODBIORCA", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            Grid.SetColumn(hdrNazwa, 1); headerGrid.Children.Add(hdrNazwa);
            var hdrZam = new TextBlock { Text = "ZAMÓWIONE", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113)), HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(hdrZam, 2); headerGrid.Children.Add(hdrZam);
            var hdrWyd = new TextBlock { Text = "WYDANE", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15)), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 5, 0) };
            Grid.SetColumn(hdrWyd, 3); headerGrid.Children.Add(hdrWyd);

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            tableGrid.Children.Add(headerBorder);

            // Lista odbiorców
            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var listPanel = new StackPanel();

            if (odbiorcy.Any())
            {
                int lp = startLp;
                foreach (var odbiorca in odbiorcy)
                {
                    var rowBorder = new Border
                    {
                        Background = new SolidColorBrush(lp % 2 == 0 ? Color.FromRgb(38, 48, 58) : Color.FromRgb(32, 42, 52)),
                        Padding = new Thickness(8, 8, 8, 8)
                    };
                    var rowGrid = new Grid();
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

                    var lpTxt = new TextBlock { Text = $"{lp}.", FontSize = 17, Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 150)), Margin = new Thickness(5, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(lpTxt, 0); rowGrid.Children.Add(lpTxt);

                    var nazwaTxt = new TextBlock { Text = odbiorca.NazwaOdbiorcy ?? "Nieznany", FontSize = 17, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                    Grid.SetColumn(nazwaTxt, 1); rowGrid.Children.Add(nazwaTxt);

                    var zamTxt = new TextBlock { Text = $"{odbiorca.Zamowione:N0} kg", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(zamTxt, 2); rowGrid.Children.Add(zamTxt);

                    var wydTxt = new TextBlock { Text = $"{odbiorca.Wydane:N0} kg", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
                    Grid.SetColumn(wydTxt, 3); rowGrid.Children.Add(wydTxt);

                    rowBorder.Child = rowGrid;
                    listPanel.Children.Add(rowBorder);
                    lp++;
                }
            }

            scrollViewer.Content = listPanel;
            Grid.SetRow(scrollViewer, 1);
            tableGrid.Children.Add(scrollViewer);

            // Suma (tylko dla pierwszej tablicy)
            if (showSuma)
            {
                var sumaBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)), Padding = new Thickness(8, 8, 8, 8) };
                var sumaGrid = new Grid();
                sumaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                sumaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                sumaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                sumaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

                var sumaTxt = new TextBlock { Text = "SUMA:", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 0, 0) };
                Grid.SetColumn(sumaTxt, 1); sumaGrid.Children.Add(sumaTxt);

                var sumaZamTxt = new TextBlock { Text = $"{sumaZam:N0} kg", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(sumaZamTxt, 2); sumaGrid.Children.Add(sumaZamTxt);

                var sumaWydTxt = new TextBlock { Text = $"{sumaWyd:N0} kg", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 5, 0) };
                Grid.SetColumn(sumaWydTxt, 3); sumaGrid.Children.Add(sumaWydTxt);

                sumaBorder.Child = sumaGrid;
                Grid.SetRow(sumaBorder, 2);
                tableGrid.Children.Add(sumaBorder);
            }

            tableBorder.Child = tableGrid;
            return tableBorder;
        }

        private Border CreateCompactOdbiorcySection(string title, List<OdbiorcaZamowienie> odbiorcy, bool isWarning)
        {
            var border = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { ShadowDepth = 1, Opacity = 0.1, BlurRadius = 5 }
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = isWarning ? new SolidColorBrush(Color.FromRgb(211, 84, 0)) : Brushes.Black,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Nagłówek tabeli
            var headerRow = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            var headers = new[] { "Odbiorca", "Zam.", "Wyd.", "%", "Status" };
            for (int i = 0; i < headers.Length; i++)
            {
                var txt = new TextBlock
                {
                    Text = headers[i],
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141))
                };
                Grid.SetColumn(txt, i);
                headerRow.Children.Add(txt);
            }
            stack.Children.Add(headerRow);

            // Wiersze
            foreach (var odb in odbiorcy)
            {
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

                var name = new TextBlock
                {
                    Text = odb.NazwaOdbiorcy,
                    FontSize = 10,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = odb.NazwaOdbiorcy
                };
                Grid.SetColumn(name, 0);
                row.Children.Add(name);

                var zam = new TextBlock
                {
                    Text = $"{odb.Zamowione:N0}",
                    FontSize = 10,
                    Foreground = odb.Zamowione == 0 ? new SolidColorBrush(Color.FromRgb(189, 195, 199)) : Brushes.Black
                };
                Grid.SetColumn(zam, 1);
                row.Children.Add(zam);

                var wyd = new TextBlock
                {
                    Text = $"{odb.Wydane:N0}",
                    FontSize = 10,
                    FontWeight = isWarning ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = isWarning ? new SolidColorBrush(Color.FromRgb(211, 84, 0)) : Brushes.Black
                };
                Grid.SetColumn(wyd, 2);
                row.Children.Add(wyd);

                var proc = new TextBlock
                {
                    Text = $"{odb.ProcentUdzial:N0}%",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219))
                };
                Grid.SetColumn(proc, 3);
                row.Children.Add(proc);

                decimal realizacja = odb.Zamowione > 0 ? (odb.Wydane / odb.Zamowione) * 100 : 0;
                var status = new TextBlock
                {
                    Text = isWarning ? "⚠️ Brak zam." :
                           realizacja >= 100 ? "✅ OK" :
                           realizacja > 0 ? $"⏳ {realizacja:N0}%" : "⏸️ 0%",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(
                        isWarning ? Color.FromRgb(211, 84, 0) :
                        realizacja >= 100 ? Color.FromRgb(39, 174, 96) :
                        realizacja > 0 ? Color.FromRgb(230, 126, 34) :
                        Color.FromRgb(127, 140, 141))
                };
                Grid.SetColumn(status, 4);
                row.Children.Add(status);

                stack.Children.Add(row);
            }

            border.Child = stack;
            return border;
        }

        /// <summary>
        /// Wyświetla dialog do symulacji redukcji ilości zamówienia.
        /// Pozwala ustawić docelową ilość w kg lub jako procent oryginalnego zamówienia.
        /// DUŻE OKNO - łatwe do obsługi na projektorze/dotykowe.
        /// </summary>
        private void ShowReductionDialog(string odbiorcaNazwa, decimal aktualnIlosc, decimal obecnaRedukcja, Action<decimal> onApply)
        {
            // Oblicz aktualną wartość po symulacji
            decimal aktualnaWartosc = aktualnIlosc + obecnaRedukcja;

            var dialog = new Window
            {
                Title = "Symulacja - ustaw docelową ilość",
                Width = 800,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(Color.FromRgb(30, 35, 40)),
                ResizeMode = ResizeMode.NoResize
            };

            // Używamy Grid zamiast StackPanel aby przyciski były zawsze widoczne na dole
            var mainGrid = new Grid { Margin = new Thickness(25) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Nagłówek
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Info
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Tryb
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: Input
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 4: Preview
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 5: Spacer
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 6: Przyciski

            // Nagłówek
            var header = new TextBlock
            {
                Text = "📊 USTAW DOCELOWĄ ILOŚĆ",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Informacja o odbiorcy - kompaktowa
            var infoBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 12, 20, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var infoInner = new StackPanel();
            infoInner.Children.Add(new TextBlock
            {
                Text = odbiorcaNazwa,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var infoRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            infoRow.Children.Add(new TextBlock { Text = $"Oryginalne: {aktualnIlosc:N0} kg", FontSize = 16, Foreground = Brushes.White, Margin = new Thickness(0, 0, 30, 0) });
            if (obecnaRedukcja != 0)
                infoRow.Children.Add(new TextBlock { Text = $"Symulacja: {aktualnaWartosc:N0} kg", FontSize = 16, Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15)) });
            infoInner.Children.Add(infoRow);
            infoBorder.Child = infoInner;
            Grid.SetRow(infoBorder, 1);
            mainGrid.Children.Add(infoBorder);

            // Wybór trybu - przyciski
            var radioKg = new RadioButton { IsChecked = true };
            var radioPercent = new RadioButton();

            var modePanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
            var btnModeKg = new Button
            {
                Content = "📦  KILOGRAMY",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(30, 12, 30, 12),
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(3),
                BorderBrush = new SolidColorBrush(Color.FromRgb(41, 128, 185)),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            var btnModePercent = new Button
            {
                Content = "📊  PROCENT",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(30, 12, 30, 12),
                Background = new SolidColorBrush(Color.FromRgb(99, 110, 114)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(3),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 80, 84)),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            TextBlock lblUnit = null!;
            TextBox txtValue = null!;

            btnModeKg.Click += (s, e) =>
            {
                radioKg.IsChecked = true;
                btnModeKg.Background = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                btnModeKg.BorderBrush = new SolidColorBrush(Color.FromRgb(41, 128, 185));
                btnModePercent.Background = new SolidColorBrush(Color.FromRgb(99, 110, 114));
                btnModePercent.BorderBrush = new SolidColorBrush(Color.FromRgb(70, 80, 84));
            };
            btnModePercent.Click += (s, e) =>
            {
                radioPercent.IsChecked = true;
                btnModePercent.Background = new SolidColorBrush(Color.FromRgb(155, 89, 182));
                btnModePercent.BorderBrush = new SolidColorBrush(Color.FromRgb(142, 68, 173));
                btnModeKg.Background = new SolidColorBrush(Color.FromRgb(99, 110, 114));
                btnModeKg.BorderBrush = new SolidColorBrush(Color.FromRgb(70, 80, 84));
            };

            modePanel.Children.Add(btnModeKg);
            modePanel.Children.Add(btnModePercent);
            Grid.SetRow(modePanel, 2);
            mainGrid.Children.Add(modePanel);

            // DUŻE pole wprowadzania
            var inputPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
            txtValue = new TextBox
            {
                Width = 260,
                FontSize = 42,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(15, 10, 15, 10),
                Text = obecnaRedukcja != 0 ? $"{aktualnaWartosc:N0}" : $"{aktualnIlosc:N0}",
                Background = new SolidColorBrush(Color.FromRgb(60, 70, 80)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(4),
                BorderBrush = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            lblUnit = new TextBlock
            {
                Text = "kg",
                FontSize = 42,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(15, 0, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219))
            };
            inputPanel.Children.Add(txtValue);
            inputPanel.Children.Add(lblUnit);
            Grid.SetRow(inputPanel, 3);
            mainGrid.Children.Add(inputPanel);

            // Aktualizuj przy zmianie trybu
            radioKg.Checked += (s, e) =>
            {
                lblUnit.Text = "kg";
                lblUnit.Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                txtValue.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                if (decimal.TryParse(txtValue.Text.Replace(",", ".").Replace(" ", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal percent))
                {
                    decimal kgValue = Math.Round(aktualnIlosc * percent / 100, 0);
                    txtValue.Text = $"{kgValue:N0}";
                }
            };
            radioPercent.Checked += (s, e) =>
            {
                lblUnit.Text = "%";
                lblUnit.Foreground = new SolidColorBrush(Color.FromRgb(155, 89, 182));
                txtValue.BorderBrush = new SolidColorBrush(Color.FromRgb(155, 89, 182));
                if (decimal.TryParse(txtValue.Text.Replace(",", ".").Replace(" ", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal kg) && aktualnIlosc > 0)
                {
                    decimal percent = Math.Round(kg / aktualnIlosc * 100, 0);
                    txtValue.Text = $"{percent:N0}";
                }
            };

            // Podgląd wyniku
            var previewBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 12, 20, 12),
                Margin = new Thickness(0, 0, 0, 0)
            };
            var previewStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var previewText = new TextBlock { FontSize = 28, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center };
            var previewDiff = new TextBlock { FontSize = 16, Foreground = new SolidColorBrush(Color.FromRgb(200, 230, 200)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) };
            previewStack.Children.Add(previewText);
            previewStack.Children.Add(previewDiff);
            previewBorder.Child = previewStack;
            Grid.SetRow(previewBorder, 4);
            mainGrid.Children.Add(previewBorder);

            // Aktualizuj podgląd
            Action updatePreview = () =>
            {
                string cleanText = txtValue.Text.Replace(",", ".").Replace(" ", "");
                if (decimal.TryParse(cleanText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal value) && value >= 0)
                {
                    decimal targetKg = radioKg.IsChecked == true ? value : Math.Round(aktualnIlosc * value / 100, 0);
                    targetKg = Math.Min(targetKg, aktualnIlosc);
                    targetKg = Math.Max(targetKg, 0);
                    decimal reduction = targetKg - aktualnIlosc;

                    previewText.Text = $"➔  {targetKg:N0} kg";
                    if (reduction == 0)
                    {
                        previewDiff.Text = "Bez zmian";
                        previewBorder.Background = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                    }
                    else
                    {
                        decimal percentChange = aktualnIlosc > 0 ? (reduction / aktualnIlosc) * 100 : 0;
                        previewDiff.Text = $"Zmiana: {reduction:+0;-0;0} kg  ({percentChange:+0;-0;0}%)";
                        previewBorder.Background = new SolidColorBrush(Color.FromRgb(241, 196, 15));
                    }
                }
                else
                {
                    previewText.Text = "Wprowadź wartość";
                    previewDiff.Text = "";
                    previewBorder.Background = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                }
            };
            txtValue.TextChanged += (s, e) => updatePreview();
            radioPercent.Checked += (s, e) => updatePreview();
            radioKg.Checked += (s, e) => updatePreview();
            updatePreview();

            // PRZYCISKI - ZAWSZE NA DOLE
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };

            var btnApply = new Button
            {
                Content = "✓  ZASTOSUJ",
                Padding = new Thickness(40, 16, 40, 16),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var btnReset = new Button
            {
                Content = "↺  ORYGINAŁ",
                Padding = new Thickness(30, 16, 30, 16),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(230, 126, 34)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var btnCancel = new Button
            {
                Content = "✕  ANULUJ",
                Padding = new Thickness(30, 16, 30, 16),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            btnApply.Click += (s, e) =>
            {
                string cleanText = txtValue.Text.Replace(",", ".").Replace(" ", "");
                if (decimal.TryParse(cleanText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal value) && value >= 0)
                {
                    decimal targetKg = radioKg.IsChecked == true ? value : Math.Round(aktualnIlosc * value / 100, 0);
                    targetKg = Math.Min(targetKg, aktualnIlosc);
                    targetKg = Math.Max(targetKg, 0);
                    onApply(targetKg - aktualnIlosc);
                    dialog.Close();
                }
            };

            btnReset.Click += (s, e) =>
            {
                onApply(0);
                dialog.Close();
            };

            btnCancel.Click += (s, e) => dialog.Close();

            btnPanel.Children.Add(btnApply);
            btnPanel.Children.Add(btnReset);
            btnPanel.Children.Add(btnCancel);
            Grid.SetRow(btnPanel, 6);
            mainGrid.Children.Add(btnPanel);

            dialog.Content = mainGrid;
            dialog.ShowDialog();
        }

        /// <summary>
        /// Wyświetla podsumowanie wszystkich aktywnych symulacji.
        /// Pozwala edytować, usuwać pojedyncze symulacje lub resetować wszystko.
        /// </summary>
        private void ShowSimulationSummaryDialog(
            Dictionary<(int ProductId, int KlientId), decimal> simulatedReductions,
            List<ProductData> productDataList,
            Action refreshContent)
        {
            var dialog = new Window
            {
                Title = "📊 Podsumowanie symulacji",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(Color.FromRgb(30, 35, 40)),
                ResizeMode = ResizeMode.CanResize,
                MinWidth = 600,
                MinHeight = 400
            };

            var mainGrid = new Grid { Margin = new Thickness(25) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // === NAGŁÓWEK ===
            var headerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            headerStack.Children.Add(new TextBlock
            {
                Text = "📊 Podsumowanie symulacji",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = $"Aktywnych symulacji: {simulatedReductions.Count}",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                Margin = new Thickness(0, 5, 0, 0)
            });
            Grid.SetRow(headerStack, 0);
            mainGrid.Children.Add(headerStack);

            // === LISTA SYMULACJI ===
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var listStack = new StackPanel();

            // Grupuj symulacje według produktu
            var groupedByProduct = simulatedReductions
                .GroupBy(kvp => kvp.Key.ProductId)
                .OrderBy(g => productDataList.FirstOrDefault(p => p.Id == g.Key)?.Kod ?? "");

            decimal totalOriginal = 0;
            decimal totalReduction = 0;

            foreach (var productGroup in groupedByProduct)
            {
                var productData = productDataList.FirstOrDefault(p => p.Id == productGroup.Key);
                string productName = productData != null ? $"[{productData.Kod}] {productData.Nazwa}" : $"Produkt #{productGroup.Key}";

                // Nagłówek produktu
                var productBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(15, 10, 15, 10),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                var productStack = new StackPanel();

                var productHeader = new TextBlock
                {
                    Text = productName,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                };
                productStack.Children.Add(productHeader);

                decimal productTotalReduction = productGroup.Sum(kvp => kvp.Value);
                var productSummary = new TextBlock
                {
                    Text = $"Łączna redukcja: {productTotalReduction:N0} kg",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                    Margin = new Thickness(0, 5, 0, 10)
                };
                productStack.Children.Add(productSummary);

                // Lista klientów z symulacjami
                foreach (var kvp in productGroup)
                {
                    // Znajdź dane klienta
                    var clientName = "Nieznany klient";
                    decimal originalAmount = 0;

                    if (productData != null)
                    {
                        var client = productData.Odbiorcy?.FirstOrDefault(o => o.KlientId == kvp.Key.KlientId);
                        if (client != null)
                        {
                            clientName = client.NazwaOdbiorcy;
                            originalAmount = client.Zamowione;
                        }
                    }

                    totalOriginal += originalAmount;
                    totalReduction += kvp.Value;

                    decimal newAmount = originalAmount + kvp.Value;
                    decimal percentChange = originalAmount > 0 ? (kvp.Value / originalAmount) * 100 : 0;

                    var clientRow = new Grid { Margin = new Thickness(10, 3, 0, 3) };
                    clientRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    clientRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                    clientRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                    clientRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                    clientRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    clientRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

                    // Nazwa klienta
                    var nameText = new TextBlock
                    {
                        Text = clientName,
                        FontSize = 13,
                        Foreground = Brushes.White,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        ToolTip = clientName
                    };
                    Grid.SetColumn(nameText, 0);
                    clientRow.Children.Add(nameText);

                    // Oryginalna ilość (przekreślona)
                    var origText = new TextBlock
                    {
                        Text = $"{originalAmount:N0} kg",
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                        TextDecorations = TextDecorations.Strikethrough,
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    Grid.SetColumn(origText, 1);
                    clientRow.Children.Add(origText);

                    // Strzałka
                    var arrowText = new TextBlock
                    {
                        Text = "→",
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    Grid.SetColumn(arrowText, 2);
                    clientRow.Children.Add(arrowText);

                    // Nowa ilość
                    var newText = new TextBlock
                    {
                        Text = $"{newAmount:N0} kg",
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    Grid.SetColumn(newText, 3);
                    clientRow.Children.Add(newText);

                    // Zmiana procentowa
                    var changeText = new TextBlock
                    {
                        Text = $"({percentChange:+0;-0;0}%)",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(changeText, 4);
                    clientRow.Children.Add(changeText);

                    // Przycisk usunięcia
                    var deleteBtn = new Button
                    {
                        Content = "✕",
                        FontSize = 12,
                        Width = 28,
                        Height = 28,
                        Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(0),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        ToolTip = "Usuń tę symulację"
                    };
                    var capturedKey = kvp.Key;
                    deleteBtn.Click += (s, e) =>
                    {
                        simulatedReductions.Remove(capturedKey);
                        refreshContent();
                        if (simulatedReductions.Count == 0)
                            dialog.Close();
                        else
                            ShowSimulationSummaryDialog(simulatedReductions, productDataList, refreshContent);
                        dialog.Close();
                    };
                    Grid.SetColumn(deleteBtn, 5);
                    clientRow.Children.Add(deleteBtn);

                    productStack.Children.Add(clientRow);
                }

                productBorder.Child = productStack;
                listStack.Children.Add(productBorder);
            }

            scrollViewer.Content = listStack;
            Grid.SetRow(scrollViewer, 1);
            mainGrid.Children.Add(scrollViewer);

            // === PODSUMOWANIE TOTALU ===
            var totalBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 15, 20, 15),
                Margin = new Thickness(0, 15, 0, 15)
            };
            var totalGrid = new Grid();
            totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var totalOrigStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            totalOrigStack.Children.Add(new TextBlock { Text = "Suma przed", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)), HorizontalAlignment = HorizontalAlignment.Center });
            totalOrigStack.Children.Add(new TextBlock { Text = $"{totalOriginal:N0} kg", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(40, 40, 40)), HorizontalAlignment = HorizontalAlignment.Center });
            Grid.SetColumn(totalOrigStack, 0);
            totalGrid.Children.Add(totalOrigStack);

            var totalChangeStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            totalChangeStack.Children.Add(new TextBlock { Text = "Redukcja", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)), HorizontalAlignment = HorizontalAlignment.Center });
            totalChangeStack.Children.Add(new TextBlock { Text = $"{totalReduction:N0} kg", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43)), HorizontalAlignment = HorizontalAlignment.Center });
            Grid.SetColumn(totalChangeStack, 1);
            totalGrid.Children.Add(totalChangeStack);

            var totalNewStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            totalNewStack.Children.Add(new TextBlock { Text = "Suma po", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)), HorizontalAlignment = HorizontalAlignment.Center });
            totalNewStack.Children.Add(new TextBlock { Text = $"{totalOriginal + totalReduction:N0} kg", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96)), HorizontalAlignment = HorizontalAlignment.Center });
            Grid.SetColumn(totalNewStack, 2);
            totalGrid.Children.Add(totalNewStack);

            totalBorder.Child = totalGrid;
            Grid.SetRow(totalBorder, 2);
            mainGrid.Children.Add(totalBorder);

            // === PRZYCISKI ===
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            var btnClose = new Button
            {
                Content = "✓  OK - Kontynuuj podgląd",
                Padding = new Thickness(30, 12, 30, 12),
                FontSize = 16,
                Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 15, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontWeight = FontWeights.SemiBold
            };
            btnClose.Click += (s, e) => dialog.Close();
            btnPanel.Children.Add(btnClose);

            var btnResetAll = new Button
            {
                Content = "🔄  Resetuj wszystko",
                Padding = new Thickness(25, 12, 25, 12),
                FontSize = 16,
                Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnResetAll.Click += (s, e) =>
            {
                simulatedReductions.Clear();
                refreshContent();
                dialog.Close();
            };
            btnPanel.Children.Add(btnResetAll);

            Grid.SetRow(btnPanel, 3);
            mainGrid.Children.Add(btnPanel);

            dialog.Content = mainGrid;
            dialog.ShowDialog();
        }

        /// <summary>
        /// Pobiera zamówienia dla klienta na dany dzień/zakres dat i produkt.
        /// </summary>
        private async System.Threading.Tasks.Task<List<(int ZamowienieId, decimal Ilosc, string Status, DateTime DataUboju)>> GetOrdersForClientAndProductAsync(
            int klientId, int productId, DateTime dateStart, DateTime? dateEnd = null)
        {
            var result = new List<(int ZamowienieId, decimal Ilosc, string Status, DateTime DataUboju)>();

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var sql = @"SELECT z.Id, t.Ilosc, z.Status, z.DataUboju
                            FROM [dbo].[ZamowieniaMieso] z
                            INNER JOIN [dbo].[ZamowieniaMiesoTowar] t ON z.Id = t.ZamowienieId
                            WHERE z.KlientId = @KlientId
                              AND t.KodTowaru = @ProductId
                              AND z.DataUboju BETWEEN @DateStart AND @DateEnd
                              AND z.Status <> 'Anulowane'
                            ORDER BY z.DataUboju, z.Id";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", klientId);
                cmd.Parameters.AddWithValue("@ProductId", productId);
                cmd.Parameters.AddWithValue("@DateStart", dateStart.Date);
                cmd.Parameters.AddWithValue("@DateEnd", (dateEnd ?? dateStart).Date);

                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    int zamId = rdr.GetInt32(0);
                    decimal ilosc = rdr.IsDBNull(1) ? 0 : rdr.GetDecimal(1);
                    string status = rdr.IsDBNull(2) ? "" : rdr.GetString(2);
                    DateTime dataUboju = rdr.GetDateTime(3);
                    result.Add((zamId, ilosc, status, dataUboju));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetOrdersForClientAndProduct] Błąd: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Otwiera edycję zamówienia dla klienta. Jeśli jest więcej zamówień - pokazuje listę wyboru.
        /// </summary>
        private async System.Threading.Tasks.Task OpenOrderEditorForClientAsync(
            int klientId, int productId, string nazwaOdbiorcy, DateTime dateStart, Action refreshCallback)
        {
            DateTime? dateEnd = _zakresDat ? _selectedDateDo : (DateTime?)null;
            var orders = await GetOrdersForClientAndProductAsync(klientId, productId, dateStart, dateEnd);

            if (orders.Count == 0)
            {
                MessageBox.Show($"Nie znaleziono zamówień dla odbiorcy '{nazwaOdbiorcy}' na ten produkt.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int selectedOrderId;

            if (orders.Count == 1)
            {
                // Jedno zamówienie - od razu otwórz edycję
                selectedOrderId = orders[0].ZamowienieId;
            }
            else
            {
                // Więcej zamówień - pokaż dialog wyboru
                string dateLabel = _zakresDat
                    ? $"{dateStart:dd.MM} - {_selectedDateDo:dd.MM.yyyy}"
                    : $"{dateStart:dd.MM.yyyy}";

                var dialog = new Window
                {
                    Title = $"Zamówienia: {nazwaOdbiorcy}",
                    Width = 500,
                    Height = 380,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    ResizeMode = ResizeMode.NoResize
                };

                var mainStack = new StackPanel { Margin = new Thickness(20) };
                mainStack.Children.Add(new TextBlock
                {
                    Text = $"Odbiorca: {nazwaOdbiorcy}",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                });
                mainStack.Children.Add(new TextBlock
                {
                    Text = $"Data: {dateLabel} | Znaleziono {orders.Count} zamówień",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                    Margin = new Thickness(0, 0, 0, 15)
                });

                var listBox = new ListBox
                {
                    Height = 200,
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 0, 15)
                };

                foreach (var order in orders)
                {
                    listBox.Items.Add(new ListBoxItem
                    {
                        Content = $"#{order.ZamowienieId} | {order.DataUboju:dd.MM} | {order.Ilosc:N0} kg | {order.Status}",
                        Tag = order.ZamowienieId,
                        Padding = new Thickness(10, 8, 10, 8)
                    });
                }
                listBox.SelectedIndex = 0;
                mainStack.Children.Add(listBox);

                var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var btnOk = new Button
                {
                    Content = "Otwórz do edycji",
                    Padding = new Thickness(20, 8, 20, 8),
                    Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(0, 0, 10, 0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                var btnCancel = new Button
                {
                    Content = "Anuluj",
                    Padding = new Thickness(20, 8, 20, 8),
                    Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                int? chosenId = null;
                btnOk.Click += (s, e) =>
                {
                    if (listBox.SelectedItem is ListBoxItem item && item.Tag is int id)
                    {
                        chosenId = id;
                        dialog.Close();
                    }
                };
                btnCancel.Click += (s, e) => dialog.Close();
                listBox.MouseDoubleClick += (s, e) =>
                {
                    if (listBox.SelectedItem is ListBoxItem item && item.Tag is int id)
                    {
                        chosenId = id;
                        dialog.Close();
                    }
                };

                btnPanel.Children.Add(btnOk);
                btnPanel.Children.Add(btnCancel);
                mainStack.Children.Add(btnPanel);
                dialog.Content = mainStack;
                dialog.ShowDialog();

                if (!chosenId.HasValue)
                    return;

                selectedOrderId = chosenId.Value;
            }

            // Otwórz WidokZamowienia (WinForms) do edycji
            var widokZamowienia = new WidokZamowienia(App.UserID ?? "", selectedOrderId);
            var result = widokZamowienia.ShowDialog();

            // Po zamknięciu okna edycji - odśwież dane
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                // Odśwież dane
                await LoadDataAsync();
                refreshCallback?.Invoke();
            }
        }

        /// <summary>
        /// Publiczna metoda do wyświetlenia okna szczegółów produktu z zewnątrz.
        /// Pozwala wywołać to samo okno z innych miejsc aplikacji (np. z WidokZamowieniaPodsumowanie).
        /// </summary>
        /// <param name="productCode">Kod produktu do wyświetlenia</param>
        public void ShowProductDetailByCode(string productCode)
        {
            if (string.IsNullOrEmpty(productCode) || _productDataList == null || _productDataList.Count == 0)
                return;

            // Znajdź produkt po kodzie
            var product = _productDataList.FirstOrDefault(p =>
                p.Kod.Equals(productCode, StringComparison.OrdinalIgnoreCase) ||
                p.Nazwa.Equals(productCode, StringComparison.OrdinalIgnoreCase));

            if (product != null)
            {
                ShowExpandedProductCard(product);
            }
        }

        /// <summary>
        /// Publiczna statyczna metoda do otwarcia okna szczegółów produktu bezpośrednio.
        /// Tworzy tymczasowe DashboardWindow, ładuje dane i wyświetla szczegóły produktu.
        /// </summary>
        /// <param name="connLibra">Connection string do bazy LibraNet</param>
        /// <param name="connHandel">Connection string do bazy Handel</param>
        /// <param name="productCode">Kod produktu do wyświetlenia</param>
        /// <param name="date">Data dla której wyświetlić dane</param>
        public static async System.Threading.Tasks.Task OpenProductDetailDirectlyAsync(
            string connLibra,
            string connHandel,
            string productCode,
            DateTime date)
        {
            // Utwórz tymczasowe okno DashboardWindow (ukryte)
            var dashboard = new DashboardWindow(connLibra, connHandel, date);
            dashboard.WindowState = WindowState.Minimized;
            dashboard.ShowInTaskbar = false;
            dashboard.Opacity = 0;
            dashboard.Show();

            // Poczekaj aż dane się załadują (max 10 sekund)
            int waitCount = 0;
            while ((dashboard._productDataList == null || dashboard._productDataList.Count == 0) && waitCount < 100)
            {
                await System.Threading.Tasks.Task.Delay(100);
                waitCount++;
            }

            // Wyświetl szczegóły produktu
            if (dashboard._productDataList != null && dashboard._productDataList.Count > 0)
            {
                dashboard.ShowProductDetailByCode(productCode);
            }

            // Zamknij ukryte okno dashboard
            dashboard.Close();
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

            _textBox = new TextBox { Margin = new Thickness(0, 0, 0, 15) };
            stack.Children.Add(_textBox);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var btnOk = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 10, 0), IsDefault = true };
            btnOk.Click += (s, e) =>
            {
                ResponseText = _textBox.Text;
                DialogResult = true;
            };

            var btnCancel = new Button { Content = "Anuluj", Width = 75, IsCancel = true };
            btnCancel.Click += (s, e) => DialogResult = false;

            btnStack.Children.Add(btnOk);
            btnStack.Children.Add(btnCancel);
            stack.Children.Add(btnStack);

            Content = stack;
            Loaded += (s, e) => _textBox.Focus();
        }
    }
}
