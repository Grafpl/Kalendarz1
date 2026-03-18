using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1.Zywiec.Kalendarz
{
    public partial class MapaDostawWindow : Window
    {
        private const double BazaLat = 51.907335;
        private const double BazaLng = 19.678605;

        private static readonly string ConnectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;Connection Timeout=15;Command Timeout=15";

        private DateTime _weekStart;
        private string _period = "week";
        private List<MapDostawa> _allPoints = new();
        private List<MapDostawa> _filteredPoints = new();
        private List<MapDostawa> _noGeoPoints = new(); // dostawy bez geokodu
        private bool _isWebViewReady;
        private HashSet<string> _selectedDays = new();

        private static readonly Dictionary<DayOfWeek, string> DayColors = new()
        {
            { DayOfWeek.Monday, "#3B82F6" },
            { DayOfWeek.Tuesday, "#22C55E" },
            { DayOfWeek.Wednesday, "#F59E0B" },
            { DayOfWeek.Thursday, "#EF4444" },
            { DayOfWeek.Friday, "#8B5CF6" },
            { DayOfWeek.Saturday, "#06B6D4" },
            { DayOfWeek.Sunday, "#EC4899" }
        };

        private static readonly string[] DniSkrot = { "niedz", "pon", "wt", "sr", "czw", "pt", "sob" };
        private static readonly string[] DniPelne = { "Niedziela", "Poniedzialek", "Wtorek", "Sroda", "Czwartek", "Piatek", "Sobota" };

        public MapaDostawWindow(DateTime weekStart)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _weekStart = weekStart;
            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                txtLoadingStatus.Text = "Pobieranie dostaw z bazy...";
                await LoadDataAsync();
                BuildDayFilters();

                txtLoadingStatus.Text = "Inicjalizacja WebView2...";
                await webView.EnsureCoreWebView2Async();
                webView.CoreWebView2.WebMessageReceived += (s2, args) =>
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(args.WebMessageAsJson);
                        string action = doc.RootElement.GetProperty("action").GetString();
                        if (action == "select")
                        {
                            string lp = doc.RootElement.GetProperty("lp").GetString();
                            Dispatcher.Invoke(() => ScrollToCard(lp));
                        }
                    }
                    catch { }
                };
                _isWebViewReady = true;

                txtLoadingStatus.Text = "Generowanie mapy...";
                await Task.Delay(100);
                RenderMap();
            }
            catch (Exception ex)
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Blad inicjalizacji mapy:\n{ex.Message}\n\n{ex.StackTrace}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        #region Data Loading

        private async Task LoadDataAsync()
        {
            var geoMap = new Dictionary<string, (double lat, double lng)>();
            var points = new List<MapDostawa>();
            var noGeo = new List<MapDostawa>();
            int totalFromDb = 0;
            var rangeStart = _weekStart.AddDays(-7);
            var rangeEnd = _weekStart.AddDays(21);

            await Task.Run(() =>
            {
                using var conn = new SqlConnection(ConnectionString);
                conn.Open();

                // Kody pocztowe
                using (var cmd = new SqlCommand("SELECT Kod, Latitude, Longitude FROM KodyPocztowe WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string kod = reader["Kod"]?.ToString()?.Trim();
                        if (string.IsNullOrEmpty(kod)) continue;
                        double lat = Convert.ToDouble(reader["Latitude"]);
                        double lng = Convert.ToDouble(reader["Longitude"]);
                        if (Math.Abs(lat) > 0.001 && Math.Abs(lng) > 0.001)
                            geoMap[kod.Replace("-", "")] = (lat, lng);
                    }
                }

                // Dostawy - BEZ filtra na Halt, BEZ filtra na status
                string sql = @"SELECT HD.LP, HD.DataOdbioru, HD.Dostawca, HD.Auta, HD.SztukiDek, HD.WagaDek, HD.bufor,
                               HD.TypCeny, HD.Cena, D.PostalCode, D.City, D.Address, D.Distance, D.Phone1
                               FROM HarmonogramDostaw HD
                               LEFT JOIN Dostawcy D ON HD.Dostawca = D.Name
                               WHERE HD.DataOdbioru >= @start AND HD.DataOdbioru < @end
                               ORDER BY HD.DataOdbioru, HD.Dostawca";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@start", rangeStart);
                    cmd.Parameters.AddWithValue("@end", rangeEnd);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        totalFromDb++;
                        string postalCode = reader["PostalCode"]?.ToString()?.Replace("-", "")?.Trim() ?? "";

                        var item = new MapDostawa
                        {
                            LP = reader["LP"]?.ToString() ?? "",
                            DataOdbioru = Convert.ToDateTime(reader["DataOdbioru"]),
                            Dostawca = reader["Dostawca"]?.ToString() ?? "",
                            Auta = reader["Auta"] != DBNull.Value ? Convert.ToInt32(reader["Auta"]) : 0,
                            SztukiDek = reader["SztukiDek"] != DBNull.Value ? Convert.ToDouble(reader["SztukiDek"]) : 0,
                            WagaDek = reader["WagaDek"] != DBNull.Value ? Convert.ToDecimal(reader["WagaDek"]) : 0,
                            Status = reader["bufor"]?.ToString() ?? "",
                            Cena = reader["Cena"] != DBNull.Value ? Convert.ToDecimal(reader["Cena"]) : 0,
                            TypCeny = reader["TypCeny"]?.ToString() ?? "",
                            Miasto = reader["City"]?.ToString() ?? "",
                            Adres = reader["Address"]?.ToString() ?? "",
                            KodPocztowy = reader["PostalCode"]?.ToString() ?? "",
                            Distance = reader["Distance"] != DBNull.Value ? Convert.ToInt32(reader["Distance"]) : 0,
                            Telefon = reader["Phone1"]?.ToString() ?? ""
                        };

                        if (geoMap.TryGetValue(postalCode, out var coords))
                        {
                            item.Lat = coords.lat;
                            item.Lng = coords.lng;
                            points.Add(item);
                        }
                        else
                        {
                            noGeo.Add(item);
                        }
                    }
                }
            });

            _allPoints = points;
            _noGeoPoints = noGeo;
            _diagTotalDb = totalFromDb;
            _diagGeoMapCount = geoMap.Count;
            _diagRangeStart = rangeStart;
            _diagRangeEnd = rangeEnd;

            txtDebug.Text = $"DB: {totalFromDb} | Mapa: {points.Count} | Brak geo: {noGeo.Count}";
        }

        // Diagnostics data
        private int _diagTotalDb;
        private int _diagGeoMapCount;
        private DateTime _diagRangeStart;
        private DateTime _diagRangeEnd;

        private void BtnDiagnostyka_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== DIAGNOSTYKA MAPY DOSTAW ===");
            sb.AppendLine($"Data generowania: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"WeekStart: {_weekStart:yyyy-MM-dd} ({_weekStart.DayOfWeek})");
            sb.AppendLine($"Okres: {_period}");
            var (ps, pe) = GetPeriodRange();
            sb.AppendLine($"Filtr okresu: {ps:yyyy-MM-dd} do {pe:yyyy-MM-dd}");
            sb.AppendLine($"Zakres SQL: {_diagRangeStart:yyyy-MM-dd} do {_diagRangeEnd:yyyy-MM-dd}");
            sb.AppendLine();

            sb.AppendLine("=== DANE Z BAZY ===");
            sb.AppendLine($"KodyPocztowe z koordynatami: {_diagGeoMapCount}");
            sb.AppendLine($"Dostawy z SQL (total): {_diagTotalDb}");
            sb.AppendLine($"Dostawy z geokod (na mapie): {_allPoints.Count}");
            sb.AppendLine($"Dostawy BEZ geokod: {_noGeoPoints.Count}");
            sb.AppendLine();

            sb.AppendLine("=== FILTRY ===");
            sb.AppendLine($"Zaznaczone dni: {_selectedDays.Count} [{string.Join(", ", _selectedDays.OrderBy(x => x))}]");
            sb.AppendLine($"chkPotwierdzony: {chkPotwierdzony.IsChecked}");
            sb.AppendLine($"chkDoWykupienia: {chkDoWykupienia.IsChecked}");
            sb.AppendLine($"chkInne: {chkInne.IsChecked}");
            sb.AppendLine($"chkAnulowany: {chkAnulowany.IsChecked}");
            sb.AppendLine($"chkSprzedany: {chkSprzedany.IsChecked}");
            sb.AppendLine($"Szukaj: '{txtSzukaj.Text}'");
            sb.AppendLine($"Po filtrach na mapie: {_filteredPoints.Count}");
            sb.AppendLine();

            // Statusy w danych
            sb.AppendLine("=== STATUSY (wszystkie dane w zakresie) ===");
            var statusGroups = _allPoints.GroupBy(d => string.IsNullOrEmpty(d.Status) ? "(pusty)" : d.Status)
                .OrderByDescending(g => g.Count());
            foreach (var g in statusGroups)
                sb.AppendLine($"  {g.Key}: {g.Count()}");
            sb.AppendLine();

            // Statusy w okresie
            sb.AppendLine("=== STATUSY (w wybranym okresie) ===");
            var inPeriod = _allPoints.Where(d => d.DataOdbioru >= ps && d.DataOdbioru < pe);
            var periodStatusGroups = inPeriod.GroupBy(d => string.IsNullOrEmpty(d.Status) ? "(pusty)" : d.Status)
                .OrderByDescending(g => g.Count());
            foreach (var g in periodStatusGroups)
                sb.AppendLine($"  {g.Key}: {g.Count()}");
            sb.AppendLine($"  RAZEM w okresie: {inPeriod.Count()}");
            sb.AppendLine();

            // Dostawy bez geokod - pogrupowane po kodzie
            if (_noGeoPoints.Count > 0)
            {
                sb.AppendLine("=== DOSTAWY BEZ GEOKODU (brak w KodyPocztowe) ===");
                var noGeoInPeriod = _noGeoPoints.Where(d => d.DataOdbioru >= ps && d.DataOdbioru < pe).ToList();
                sb.AppendLine($"W wybranym okresie: {noGeoInPeriod.Count}");

                var byPostal = noGeoInPeriod.GroupBy(d => string.IsNullOrEmpty(d.KodPocztowy) ? "(brak kodu)" : d.KodPocztowy)
                    .OrderByDescending(g => g.Count());
                foreach (var g in byPostal.Take(30))
                {
                    var names = string.Join(", ", g.Select(d => d.Dostawca).Distinct().Take(3));
                    sb.AppendLine($"  Kod: {g.Key} ({g.Count()} dostaw) - {names}");
                }
                sb.AppendLine();
            }

            // Dostawy po dniach w okresie
            sb.AppendLine("=== DOSTAWY WG DNI (w okresie, z geokod) ===");
            var byDay = _allPoints.Where(d => d.DataOdbioru >= ps && d.DataOdbioru < pe)
                .GroupBy(d => d.DataOdbioru.Date).OrderBy(g => g.Key);
            foreach (var g in byDay)
            {
                string dayName = DniSkrot[(int)g.Key.DayOfWeek];
                bool isSelected = _selectedDays.Contains(g.Key.ToString("yyyy-MM-dd"));
                sb.AppendLine($"  {dayName} {g.Key:dd.MM}: {g.Count()} dostaw, {g.Sum(d => d.Auta)} aut {(isSelected ? "[V]" : "[X - odfiltrowane]")}");
            }

            string report = sb.ToString();
            Clipboard.SetText(report);
            MessageBox.Show(report + "\n\n--- Skopiowano do schowka ---", "Diagnostyka Mapy Dostaw", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Filters

        private void BuildDayFilters()
        {
            panelDniFiltr.Children.Clear();
            _selectedDays.Clear();

            var (start, end) = GetPeriodRange();
            for (var d = start; d < end; d = d.AddDays(1))
            {
                string label = $"{DniSkrot[(int)d.DayOfWeek]} {d:dd.MM}";
                string color = DayColors.GetValueOrDefault(d.DayOfWeek, "#6B7280");
                string tag = d.ToString("yyyy-MM-dd");

                var cb = new CheckBox
                {
                    Content = label,
                    Tag = tag,
                    IsChecked = true,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 8, 3),
                    Cursor = Cursors.Hand
                };
                cb.Click += DayFilter_Changed;
                panelDniFiltr.Children.Add(cb);
                _selectedDays.Add(tag);
            }
        }

        private (DateTime start, DateTime end) GetPeriodRange()
        {
            return _period switch
            {
                "day" => (DateTime.Today, DateTime.Today.AddDays(1)),
                "2weeks" => (_weekStart, _weekStart.AddDays(14)),
                _ => (_weekStart, _weekStart.AddDays(7))
            };
        }

        private void BtnPeriod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton clicked) return;
            btnDzien.IsChecked = false;
            btnTydzien.IsChecked = false;
            btn2Tygodnie.IsChecked = false;
            clicked.IsChecked = true;

            _period = clicked.Tag?.ToString() ?? "week";
            if (_period == "day") _weekStart = DateTime.Today;

            BuildDayFilters();
            ApplyFilterAndRefresh();
        }

        private void DayFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is string tag)
            {
                if (cb.IsChecked == true) _selectedDays.Add(tag);
                else _selectedDays.Remove(tag);
            }
            ApplyFilterAndRefresh();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isWebViewReady) return;
            ApplyFilterAndRefresh();
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isWebViewReady) return;
            ApplyFilterAndRefresh();
        }

        private List<MapDostawa> ApplyFilter(List<MapDostawa> source)
        {
            var result = source.AsEnumerable();
            var (start, end) = GetPeriodRange();
            result = result.Where(d => d.DataOdbioru >= start && d.DataOdbioru < end);

            if (_selectedDays.Count > 0)
                result = result.Where(d => _selectedDays.Contains(d.DataOdbioru.ToString("yyyy-MM-dd")));
            else
                return new List<MapDostawa>();

            // Status - wszystkie statusy z checkboxami
            result = result.Where(d =>
            {
                string s = (d.Status ?? "").ToLower();
                if (s == "anulowany") return chkAnulowany.IsChecked == true;
                if (s == "sprzedany") return chkSprzedany.IsChecked == true;
                if (s == "potwierdzony") return chkPotwierdzony.IsChecked == true;
                if (s == "do wykupienia") return chkDoWykupienia.IsChecked == true;
                // B.Wolny., B.Kontr., inne
                return chkInne.IsChecked == true;
            });

            string search = txtSzukaj.Text?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(search))
                result = result.Where(d =>
                    d.Dostawca.ToLowerInvariant().Contains(search) ||
                    d.Miasto.ToLowerInvariant().Contains(search));

            return result.ToList();
        }

        #endregion

        #region Rendering

        private void RenderMap()
        {
            _filteredPoints = ApplyFilter(_allPoints);
            UpdateKpis();
            UpdateSidebar();
            UpdatePeriodLabel();

            string json = SerializePoints(_filteredPoints);
            string html = BuildMapHtml(json);
            string tempPath = Path.Combine(Path.GetTempPath(), "mapa_dostaw_zywca.html");
            File.WriteAllText(tempPath, html, Encoding.UTF8);
            webView.CoreWebView2.Navigate(tempPath);

            loadingOverlay.Visibility = Visibility.Collapsed;
        }

        private async void ApplyFilterAndRefresh()
        {
            _filteredPoints = ApplyFilter(_allPoints);
            UpdateKpis();
            UpdateSidebar();
            UpdatePeriodLabel();

            if (!_isWebViewReady) return;
            try
            {
                string json = SerializePoints(_filteredPoints);
                await webView.CoreWebView2.ExecuteScriptAsync("updateMarkers(" + json + ")");
            }
            catch { }
        }

        private void UpdatePeriodLabel()
        {
            var (start, end) = GetPeriodRange();
            txtOkres.Text = $"{start:dd.MM} - {end.AddDays(-1):dd.MM.yyyy}";
        }

        private void UpdateKpis()
        {
            txtKpiDostawy.Text = _filteredPoints.Count.ToString();
            txtKpiHodowcy.Text = _filteredPoints.Select(d => d.Dostawca).Distinct().Count().ToString();
            txtKpiAuta.Text = _filteredPoints.Sum(d => d.Auta).ToString();
        }

        private void UpdateSidebar()
        {
            panelDostawy.Children.Clear();
            var grouped = _filteredPoints.GroupBy(d => d.DataOdbioru.Date).OrderBy(g => g.Key);
            int total = 0;

            foreach (var dayGroup in grouped)
            {
                string color = DayColors.GetValueOrDefault(dayGroup.Key.DayOfWeek, "#6B7280");

                // Day header
                var dayHeader = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color + "33")),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(4, 6, 4, 2)
                };
                var hs = new StackPanel { Orientation = Orientation.Horizontal };
                hs.Children.Add(new TextBlock { Text = $"{DniPelne[(int)dayGroup.Key.DayOfWeek]} {dayGroup.Key:dd.MM}", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)) });
                hs.Children.Add(new TextBlock { Text = $"  ({dayGroup.Count()} dostaw, {dayGroup.Sum(d => d.Auta)} aut)", FontSize = 9, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")), VerticalAlignment = VerticalAlignment.Center });
                dayHeader.Child = hs;
                panelDostawy.Children.Add(dayHeader);

                foreach (var d in dayGroup.OrderBy(x => x.Dostawca))
                {
                    panelDostawy.Children.Add(CreateCard(d, color));
                    total++;
                }
            }
            txtLiczbaLista.Text = $"{total} dostaw na mapie";
        }

        private Border CreateCard(MapDostawa d, string dayColor)
        {
            var card = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(4, 1, 4, 1), Cursor = Cursors.Hand,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
                BorderThickness = new Thickness(1), Tag = d.LP
            };
            card.MouseLeftButtonUp += Card_Click;
            card.MouseEnter += (s, e) => ((Border)s).Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
            card.MouseLeave += (s, e) => ((Border)s).Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            var dot = new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dayColor)), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 8, 0) };
            Grid.SetColumn(dot, 0); grid.Children.Add(dot);

            var stack = new StackPanel(); Grid.SetColumn(stack, 1);
            stack.Children.Add(new TextBlock { Text = d.Dostawca, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")), TextTrimming = TextTrimming.CharacterEllipsis });
            stack.Children.Add(new TextBlock { Text = $"{d.Auta} aut | {d.SztukiDek:#,0} szt | {d.WagaDek:0.00} kg", FontSize = 9, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")), Margin = new Thickness(0, 1, 0, 0) });
            if (!string.IsNullOrWhiteSpace(d.Miasto))
                stack.Children.Add(new TextBlock { Text = $"{d.Miasto} | {d.Distance} km", FontSize = 9, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")), Margin = new Thickness(0, 1, 0, 0) });
            grid.Children.Add(stack);

            string sc = GetStatusColor(d.Status);
            var badge = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sc + "33")), CornerRadius = new CornerRadius(4), Padding = new Thickness(4, 1, 4, 1), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(4, 0, 0, 0) };
            string shortStatus = (d.Status ?? "").Length > 6 ? (d.Status ?? "").Substring(0, 6) + "." : d.Status ?? "";
            badge.Child = new TextBlock { Text = shortStatus, FontSize = 8, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sc)) };
            Grid.SetColumn(badge, 2); grid.Children.Add(badge);

            card.Child = grid;
            return card;
        }

        private static string GetStatusColor(string status)
        {
            return (status ?? "").ToLower() switch
            {
                "potwierdzony" => "#22C55E",
                "anulowany" => "#EF4444",
                "sprzedany" => "#3B82F6",
                "do wykupienia" => "#A855F7",
                "b.wolny." => "#FBBF24",
                "b.kontr." => "#7C3AED",
                _ => "#F59E0B"
            };
        }

        private async void Card_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string lp && _isWebViewReady)
            {
                try
                {
                    string safe = lp.Replace("\\", "\\\\").Replace("\"", "");
                    await webView.CoreWebView2.ExecuteScriptAsync($"focusMarker(\"{safe}\")");
                }
                catch { }
            }
        }

        private void ScrollToCard(string lp)
        {
            foreach (var child in panelDostawy.Children)
            {
                if (child is Border border && border.Tag is string cardLp && cardLp == lp)
                {
                    border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
                    border.BringIntoView();
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    timer.Tick += (s, ev) => { border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")); timer.Stop(); };
                    timer.Start();
                }
            }
        }

        #endregion

        #region Map HTML

        private static string SerializePoints(List<MapDostawa> points)
        {
            var data = points.Select(p => new
            {
                lp = p.LP, n = p.Dostawca, m = p.Miasto, a = p.Adres,
                au = p.Auta, sz = p.SztukiDek, w = p.WagaDek, s = p.Status,
                c = p.Cena, tc = p.TypCeny, km = p.Distance, tel = p.Telefon,
                dt = p.DataOdbioru.ToString("yyyy-MM-dd"), dw = (int)p.DataOdbioru.DayOfWeek,
                lat = p.Lat, lng = p.Lng
            });
            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        private string BuildMapHtml(string json)
        {
            // Use StringBuilder to avoid C# verbatim string escaping issues
            var sb = new StringBuilder();
            string bLat = BazaLat.ToString(CultureInfo.InvariantCulture);
            string bLng = BazaLng.ToString(CultureInfo.InvariantCulture);

            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
            sb.AppendLine("<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>");
            sb.AppendLine("<link rel='stylesheet' href='https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.css'/>");
            sb.AppendLine("<link rel='stylesheet' href='https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.Default.css'/>");
            sb.AppendLine("<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>");
            sb.AppendLine("<script src='https://unpkg.com/leaflet.markercluster@1.5.3/dist/leaflet.markercluster.js'></script>");
            sb.AppendLine("<style>");
            sb.AppendLine("*{margin:0;padding:0;box-sizing:border-box;}");
            sb.AppendLine("html,body{height:100%;font-family:'Segoe UI',sans-serif;}");
            sb.AppendLine("#map{height:100%;}");
            sb.AppendLine(".pn{font-size:14px;font-weight:700;margin-bottom:4px;color:#1e293b;}");
            sb.AppendLine(".pr{font-size:12px;color:#555;margin:2px 0;}");
            sb.AppendLine(".pd{display:inline-block;padding:2px 8px;border-radius:4px;font-size:11px;font-weight:700;color:#fff;margin-bottom:6px;}");
            sb.AppendLine(".pp{font-size:13px;color:#22c55e;font-weight:700;margin-top:4px;}");
            sb.AppendLine(".ps{display:flex;gap:8px;margin:6px 0;}");
            sb.AppendLine(".psi{background:#f1f5f9;border-radius:4px;padding:3px 8px;font-size:11px;font-weight:600;color:#334155;}");
            sb.AppendLine(".sb{display:inline-block;padding:2px 8px;border-radius:4px;font-size:11px;font-weight:700;color:#fff;}");
            sb.AppendLine(".rb{display:inline-block;margin-top:6px;padding:4px 12px;background:#22C55E;color:#fff;font-size:11px;font-weight:700;border:none;border-radius:6px;cursor:pointer;}");
            sb.AppendLine(".rb:hover{background:#16A34A;}");
            sb.AppendLine(".ri{position:fixed;bottom:20px;right:20px;background:#1e293b;border-radius:10px;padding:14px 18px;box-shadow:0 4px 20px rgba(0,0,0,.6);z-index:9999;font-size:12px;color:#e2e8f0;min-width:220px;display:none;border:1px solid #334155;}");
            sb.AppendLine(".ri-t{font-weight:700;font-size:13px;color:#86efac;margin-bottom:6px;}");
            sb.AppendLine(".ri-r{margin:3px 0;color:#94a3b8;} .ri-r b{color:#e2e8f0;}");
            sb.AppendLine(".ri-c{position:absolute;top:6px;right:10px;cursor:pointer;color:#94a3b8;font-size:16px;font-weight:700;}");
            sb.AppendLine(".ri-c:hover{color:#ef4444;}");
            sb.AppendLine(".lg{position:fixed;bottom:20px;left:20px;background:#1e293b;border-radius:10px;padding:12px 16px;box-shadow:0 2px 12px rgba(0,0,0,.5);z-index:9999;font-size:12px;color:#e2e8f0;}");
            sb.AppendLine(".lg-t{font-weight:700;margin-bottom:6px;font-size:13px;color:#86efac;}");
            sb.AppendLine(".lg-i{display:flex;align-items:center;margin:3px 0;}");
            sb.AppendLine(".lg-d{width:12px;height:12px;border-radius:50%;margin-right:8px;border:2px solid #334155;}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<div id='map'></div>");

            // Legend
            sb.AppendLine("<div class='lg'>");
            sb.AppendLine("<div class='lg-t'>Kolor = dzien tygodnia</div>");
            sb.AppendLine("<div class='lg-i'><div class='lg-d' style='background:#3B82F6'></div>Poniedzialek</div>");
            sb.AppendLine("<div class='lg-i'><div class='lg-d' style='background:#22C55E'></div>Wtorek</div>");
            sb.AppendLine("<div class='lg-i'><div class='lg-d' style='background:#F59E0B'></div>Sroda</div>");
            sb.AppendLine("<div class='lg-i'><div class='lg-d' style='background:#EF4444'></div>Czwartek</div>");
            sb.AppendLine("<div class='lg-i'><div class='lg-d' style='background:#8B5CF6'></div>Piatek</div>");
            sb.AppendLine("<div class='lg-i'><div class='lg-d' style='background:#06B6D4'></div>Sobota</div>");
            sb.AppendLine("</div>");

            // Route info
            sb.AppendLine("<div id='routeInfo' class='ri'>");
            sb.AppendLine("<span class='ri-c' onclick='clearRoute()'>X</span>");
            sb.AppendLine("<div id='routeTitle' class='ri-t'></div>");
            sb.AppendLine("<div id='routeDistance' class='ri-r'></div>");
            sb.AppendLine("<div id='routeDuration' class='ri-r'></div>");
            sb.AppendLine("</div>");

            // JavaScript - using single quotes in HTML attributes to avoid escaping issues
            sb.AppendLine("<script>");
            sb.AppendLine($"var BAZA_LAT={bLat};");
            sb.AppendLine($"var BAZA_LNG={bLng};");
            sb.AppendLine("var DAY_COLORS={0:'#EC4899',1:'#3B82F6',2:'#22C55E',3:'#F59E0B',4:'#EF4444',5:'#8B5CF6',6:'#06B6D4'};");
            sb.AppendLine("var DAY_NAMES=['niedz','pon','wt','sr','czw','pt','sob'];");
            sb.AppendLine("var STATUS_COLORS={'potwierdzony':'#22c55e','anulowany':'#ef4444','sprzedany':'#3b82f6','do wykupienia':'#a855f7','b.wolny.':'#fbbf24','b.kontr.':'#7c3aed'};");
            sb.AppendLine("");
            sb.AppendLine("var map=L.map('map').setView([52.0,19.5],7);");
            sb.AppendLine("L.tileLayer('https://mt1.google.com/vt/lyrs=m&hl=pl&x={x}&y={y}&z={z}',{attribution:'Google',maxZoom:20}).addTo(map);");
            sb.AppendLine("");

            // Base marker
            sb.AppendLine("var bazaIcon=L.divIcon({className:'',html:'<div style=\"width:28px;height:28px;border-radius:50%;background:#22C55E;border:3px solid #fff;box-shadow:0 0 12px rgba(34,197,94,.7);display:flex;align-items:center;justify-content:center;font-size:14px;font-weight:bold;color:#fff\">B</div>',iconSize:[28,28],iconAnchor:[14,14]});");
            sb.AppendLine($"L.marker([BAZA_LAT,BAZA_LNG],{{icon:bazaIcon,zIndexOffset:1000}}).addTo(map).bindPopup('<div class=pn style=color:#22C55E>BAZA - Ubojnia Drobiu Piorkowscy</div><div class=pr>Koziolki 40, 95-061 Dmosin</div>');");
            sb.AppendLine("");
            sb.AppendLine("var markers={};var markerData={};");
            sb.AppendLine("var markersLayer=L.markerClusterGroup({maxClusterRadius:40,spiderfyOnMaxZoom:true,showCoverageOnHover:false,zoomToBoundsOnClick:true,disableClusteringAtZoom:14}).addTo(map);");
            sb.AppendLine("var currentRoute=null;");
            sb.AppendLine("");
            sb.AppendLine("function clearRoute(){if(currentRoute){map.removeLayer(currentRoute);currentRoute=null;}document.getElementById('routeInfo').style.display='none';}");
            sb.AppendLine("");

            // showRoute
            sb.AppendLine("function showRoute(lp){");
            sb.AppendLine("  clearRoute();var d=markerData[lp];if(!d)return;");
            sb.AppendLine("  var url='https://router.project-osrm.org/route/v1/driving/'+BAZA_LNG+','+BAZA_LAT+';'+d.lng+','+d.lat+'?overview=full&geometries=geojson';");
            sb.AppendLine("  fetch(url).then(function(r){return r.json();}).then(function(data){");
            sb.AppendLine("    if(!data.routes||data.routes.length===0)return;");
            sb.AppendLine("    var route=data.routes[0];var coords=route.geometry.coordinates.map(function(c){return[c[1],c[0]];});");
            sb.AppendLine("    var color=DAY_COLORS[d.dw]||'#22C55E';");
            sb.AppendLine("    currentRoute=L.polyline(coords,{color:color,weight:4,opacity:0.8,dashArray:'10,8',lineCap:'round'}).addTo(map);");
            sb.AppendLine("    var distKm=(route.distance/1000).toFixed(1);var durMin=Math.round(route.duration/60);");
            sb.AppendLine("    var hours=Math.floor(durMin/60);var mins=durMin%60;");
            sb.AppendLine("    var durStr=hours>0?hours+'h '+mins+'min':mins+' min';");
            sb.AppendLine("    document.getElementById('routeTitle').textContent=d.n+' ('+DAY_NAMES[d.dw]+' '+d.dt.substring(5)+')';");
            sb.AppendLine("    document.getElementById('routeDistance').innerHTML='Dystans: <b>'+distKm+' km</b> (baza: '+d.km+' km)';");
            sb.AppendLine("    document.getElementById('routeDuration').innerHTML='Czas jazdy: <b>'+durStr+'</b>';");
            sb.AppendLine("    document.getElementById('routeInfo').style.display='block';");
            sb.AppendLine("  }).catch(function(err){console.error(err);});");
            sb.AppendLine("}");
            sb.AppendLine("");

            // spreadOverlapping
            sb.AppendLine("function spreadOverlapping(data){");
            sb.AppendLine("  var groups={};data.forEach(function(d){var key=d.lat.toFixed(5)+','+d.lng.toFixed(5);if(!groups[key])groups[key]=[];groups[key].push(d);});");
            sb.AppendLine("  Object.keys(groups).forEach(function(key){var g=groups[key];if(g.length<=1)return;for(var i=0;i<g.length;i++){var angle=(2*Math.PI*i)/g.length;var r=0.0008*(1+Math.floor(i/8)*0.5);g[i].lat+=Math.cos(angle)*r;g[i].lng+=Math.sin(angle)*r;}});");
            sb.AppendLine("}");
            sb.AppendLine("");

            // addMarkers - using backtick template literals for popup HTML
            sb.AppendLine("function addMarkers(data){");
            sb.AppendLine("  map.removeLayer(markersLayer);");
            sb.AppendLine("  markersLayer=L.markerClusterGroup({maxClusterRadius:35,spiderfyOnMaxZoom:true,showCoverageOnHover:false,zoomToBoundsOnClick:true,disableClusteringAtZoom:14});");
            sb.AppendLine("  markers={};markerData={};spreadOverlapping(data);");
            sb.AppendLine("  data.forEach(function(d){");
            sb.AppendLine("    markerData[d.lp]=d;");
            sb.AppendLine("    var color=DAY_COLORS[d.dw]||'#6b7280';");
            sb.AppendLine("    var conf=(d.s||'').toLowerCase()==='potwierdzony';");
            sb.AppendLine("    var sz=conf?16:12;var bw=conf?3:2;");
            sb.AppendLine("    var shadow=conf?'0 0 8px '+color:'0 0 4px rgba(0,0,0,.4)';");
            sb.AppendLine("    var icon=L.divIcon({className:'',html:'<div style=\"width:'+sz+'px;height:'+sz+'px;border-radius:50%;background:'+color+';border:'+bw+'px solid #fff;box-shadow:'+shadow+'\"></div>',iconSize:[sz,sz],iconAnchor:[sz/2,sz/2]});");
            sb.AppendLine("    var sc=STATUS_COLORS[(d.s||'').toLowerCase()]||'#f59e0b';");
            sb.AppendLine("    var dayLabel=DAY_NAMES[d.dw]+' '+d.dt.substring(5);");
            // Build popup with backtick template literal
            sb.AppendLine("    var popup=`<div class=pd style=background:${color}>${dayLabel}</div>`");
            sb.AppendLine("      +`<div class=pn>${d.n}</div>`");
            sb.AppendLine("      +`<div class=pr><span class=sb style=background:${sc}>${d.s}</span></div>`");
            sb.AppendLine("      +`<div class=ps><span class=psi>${d.au} aut</span><span class=psi>${Math.round(d.sz).toLocaleString()} szt</span><span class=psi>${Number(d.w).toFixed(2)} kg</span></div>`");
            sb.AppendLine("      +(d.a?`<div class=pr>${d.a}</div>`:'')+`<div class=pr>${d.m} | ${d.km} km</div>`");
            sb.AppendLine("      +(d.c>0?`<div class=pr>${Number(d.c).toFixed(2)} zl (${d.tc})</div>`:'')+");
            sb.AppendLine("      (d.tel?`<div class=pp>${d.tel}</div>`:'')+");
            sb.AppendLine("      `<div><button class=rb onclick=\"showRoute('${d.lp}')\">Pokaz trase z bazy</button></div>`;");
            sb.AppendLine("    var marker=L.marker([d.lat,d.lng],{icon:icon}).bindPopup(popup);");
            sb.AppendLine("    marker.on('click',function(){window.chrome.webview.postMessage(JSON.stringify({action:'select',lp:d.lp}));});");
            sb.AppendLine("    markers[d.lp]=marker;markersLayer.addLayer(marker);");
            sb.AppendLine("  });");
            sb.AppendLine("  map.addLayer(markersLayer);");
            sb.AppendLine("  if(data.length>0){var bounds=L.latLngBounds(data.map(function(d){return[d.lat,d.lng];}));bounds.extend([BAZA_LAT,BAZA_LNG]);if(bounds.isValid())map.fitBounds(bounds,{padding:[40,40]});}");
            sb.AppendLine("}");
            sb.AppendLine("");
            sb.AppendLine("function focusMarker(lp){var m=markers[lp];if(m){clearRoute();markersLayer.zoomToShowLayer(m,function(){m.openPopup();showRoute(lp);});}}");
            sb.AppendLine("function updateMarkers(data){try{clearRoute();addMarkers(data);}catch(e){console.error(e);}}");
            sb.AppendLine("");
            sb.AppendLine($"var initialData={json};");
            sb.AppendLine("addMarkers(initialData);");
            sb.AppendLine("</script></body></html>");

            return sb.ToString();
        }

        #endregion

        #region Model

        private class MapDostawa
        {
            public string LP { get; set; }
            public DateTime DataOdbioru { get; set; }
            public string Dostawca { get; set; }
            public int Auta { get; set; }
            public double SztukiDek { get; set; }
            public decimal WagaDek { get; set; }
            public string Status { get; set; }
            public decimal Cena { get; set; }
            public string TypCeny { get; set; }
            public string Miasto { get; set; }
            public string Adres { get; set; }
            public string KodPocztowy { get; set; }
            public int Distance { get; set; }
            public string Telefon { get; set; }
            public double Lat { get; set; }
            public double Lng { get; set; }
        }

        #endregion
    }
}
