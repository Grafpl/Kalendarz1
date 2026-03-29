using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1.Zywiec.Kalendarz
{
    public partial class MapaDostawWindow : Window
    {
        private const double BazaLat = 51.907335;
        private const double BazaLng = 19.678605;
        private static readonly string ConnStr = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;Connection Timeout=15;Command Timeout=15";

        private DateTime _weekStart;
        private string _period = "week";
        private List<Dost> _all = new();
        private List<Dost> _noGeo = new();
        private List<Dost> _filtered = new();
        private bool _ready;
        private HashSet<string> _days = new();

        // diagnostics
        private int _dbTotal, _geoCount;
        private DateTime _sqlStart, _sqlEnd;

        static readonly Dictionary<DayOfWeek, string> DC = new()
        {
            {DayOfWeek.Monday,"#3B82F6"},{DayOfWeek.Tuesday,"#22C55E"},{DayOfWeek.Wednesday,"#F59E0B"},
            {DayOfWeek.Thursday,"#EF4444"},{DayOfWeek.Friday,"#8B5CF6"},{DayOfWeek.Saturday,"#06B6D4"},{DayOfWeek.Sunday,"#EC4899"}
        };
        static readonly string[] DN = {"niedz","pon","wt","sr","czw","pt","sob"};
        static readonly string[] DF = {"Niedziela","Poniedzialek","Wtorek","Sroda","Czwartek","Piatek","Sobota"};

        public MapaDostawWindow(DateTime weekStart)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _weekStart = weekStart;
            Loaded += async (s, e) => await InitAsync();
        }

        async Task InitAsync()
        {
            try
            {
                txtLoadingStatus.Text = "Pobieranie danych...";
                await LoadDataAsync();
                BuildDayFilters();
                SetPeriodButton(_period);

                txtLoadingStatus.Text = "Uruchamianie mapy...";
                await webView.EnsureCoreWebView2Async();
                webView.CoreWebView2.WebMessageReceived += (s, a) =>
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(a.WebMessageAsJson);
                        if (doc.RootElement.GetProperty("action").GetString() == "select")
                            Dispatcher.Invoke(() => ScrollTo(doc.RootElement.GetProperty("lp").GetString()));
                    }
                    catch { }
                };
                _ready = true;
                RenderMap();
            }
            catch (Exception ex)
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Blad:\n{ex.Message}\n\n{ex.StackTrace}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Data

        async Task LoadDataAsync()
        {
            var geo = new Dictionary<string, (double lat, double lng)>();
            var pts = new List<Dost>();
            var noG = new List<Dost>();
            int total = 0;
            _sqlStart = _weekStart.AddDays(-7);
            _sqlEnd = _weekStart.AddDays(21);

            await Task.Run(() =>
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();

                using (var cmd = new SqlCommand("SELECT Kod, Latitude, Longitude FROM KodyPocztowe WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string k = r["Kod"]?.ToString()?.Trim() ?? "";
                        if (k == "") continue;
                        double la = Convert.ToDouble(r["Latitude"]), lo = Convert.ToDouble(r["Longitude"]);
                        if (Math.Abs(la) > 0.001) geo[k.Replace("-", "")] = (la, lo);
                    }

                using (var cmd = new SqlCommand(@"SELECT HD.LP, HD.DataOdbioru, HD.Dostawca, HD.Auta, HD.SztukiDek, HD.WagaDek, HD.bufor,
                    HD.TypCeny, HD.Cena, D.PostalCode, D.City, D.Address, D.Distance, D.Phone1
                    FROM HarmonogramDostaw HD LEFT JOIN Dostawcy D ON HD.Dostawca = D.Name
                    WHERE HD.DataOdbioru >= @s AND HD.DataOdbioru < @e ORDER BY HD.DataOdbioru, HD.Dostawca", conn))
                {
                    cmd.Parameters.AddWithValue("@s", _sqlStart);
                    cmd.Parameters.AddWithValue("@e", _sqlEnd);
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        total++;
                        string pc = r["PostalCode"]?.ToString()?.Replace("-", "")?.Trim() ?? "";
                        var d = new Dost
                        {
                            LP = r["LP"]?.ToString() ?? "",
                            Data = Convert.ToDateTime(r["DataOdbioru"]),
                            Nazwa = r["Dostawca"]?.ToString() ?? "",
                            Auta = r["Auta"] != DBNull.Value ? Convert.ToInt32(r["Auta"]) : 0,
                            Szt = r["SztukiDek"] != DBNull.Value ? Convert.ToDouble(r["SztukiDek"]) : 0,
                            Waga = r["WagaDek"] != DBNull.Value ? Convert.ToDecimal(r["WagaDek"]) : 0,
                            Status = r["bufor"]?.ToString() ?? "",
                            Cena = r["Cena"] != DBNull.Value ? Convert.ToDecimal(r["Cena"]) : 0,
                            TypCeny = r["TypCeny"]?.ToString() ?? "",
                            Miasto = r["City"]?.ToString() ?? "",
                            Adres = r["Address"]?.ToString() ?? "",
                            Kod = r["PostalCode"]?.ToString() ?? "",
                            Km = r["Distance"] != DBNull.Value ? Convert.ToInt32(r["Distance"]) : 0,
                            Tel = r["Phone1"]?.ToString() ?? ""
                        };
                        if (geo.TryGetValue(pc, out var c)) { d.Lat = c.lat; d.Lng = c.lng; pts.Add(d); }
                        else noG.Add(d);
                    }
                }
            });

            _all = pts; _noGeo = noG; _dbTotal = total; _geoCount = geo.Count;
            txtDebug.Text = $"SQL: {total} | Mapa: {pts.Count} | Brak geo: {noG.Count}";
        }

        #endregion

        #region Filters

        (DateTime s, DateTime e) Range()
        {
            return _period switch
            {
                "day" => (DateTime.Today, DateTime.Today.AddDays(1)),
                "2weeks" => (_weekStart, _weekStart.AddDays(14)),
                _ => (_weekStart, _weekStart.AddDays(7))
            };
        }

        void BuildDayFilters()
        {
            panelDniFiltr.Children.Clear();
            _days.Clear();
            var (s, e) = Range();
            for (var d = s; d < e; d = d.AddDays(1))
            {
                string tag = d.ToString("yyyy-MM-dd");
                string col = DC.GetValueOrDefault(d.DayOfWeek, "#6B7280");
                var cb = new CheckBox
                {
                    Content = $"{DN[(int)d.DayOfWeek]} {d:dd.MM}", Tag = tag, IsChecked = true,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(col)),
                    FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 8, 2), Cursor = Cursors.Hand
                };
                cb.Click += (s2, e2) =>
                {
                    if (cb.IsChecked == true) _days.Add(tag); else _days.Remove(tag);
                    Refresh();
                };
                panelDniFiltr.Children.Add(cb);
                _days.Add(tag);
            }
        }

        void SetPeriodButton(string p)
        {
            _period = p;
            btnDzien.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(p == "day" ? "#1E40AF" : "#F1F5F9"));
            btnDzien.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(p == "day" ? "#FFFFFF" : "#64748B"));
            btnTydzien.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(p == "week" ? "#1E40AF" : "#F1F5F9"));
            btnTydzien.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(p == "week" ? "#FFFFFF" : "#64748B"));
            btn2Tygodnie.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(p == "2weeks" ? "#1E40AF" : "#F1F5F9"));
            btn2Tygodnie.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(p == "2weeks" ? "#FFFFFF" : "#64748B"));
        }

        void BtnPeriod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b) return;
            string p = b.Tag?.ToString() ?? "week";
            if (p == "day") _weekStart = DateTime.Today;
            SetPeriodButton(p);
            BuildDayFilters();
            Refresh();
        }

        void Filter_Changed(object sender, RoutedEventArgs e) { if (_ready) Refresh(); }
        void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e) { if (_ready) Refresh(); }

        List<Dost> Apply()
        {
            var (s, e) = Range();
            var r = _all.Where(d => d.Data >= s && d.Data < e);
            if (_days.Count > 0) r = r.Where(d => _days.Contains(d.Data.ToString("yyyy-MM-dd")));
            else return new();

            r = r.Where(d =>
            {
                string st = (d.Status ?? "").ToLower();
                if (st == "anulowany") return chkAnulowany.IsChecked == true;
                if (st == "sprzedany") return chkSprzedany.IsChecked == true;
                if (st == "potwierdzony") return chkPotwierdzony.IsChecked == true;
                if (st == "do wykupienia") return chkDoWykupienia.IsChecked == true;
                return chkInne.IsChecked == true;
            });

            string q = txtSzukaj.Text?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(q))
                r = r.Where(d => d.Nazwa.ToLowerInvariant().Contains(q) || d.Miasto.ToLowerInvariant().Contains(q));
            return r.ToList();
        }

        #endregion

        #region Render

        void RenderMap()
        {
            _filtered = Apply();
            UpdateUI();
            string json = Ser(_filtered);
            string html = Html(json);
            string path = Path.Combine(Path.GetTempPath(), "mapa_dostaw_zywca.html");
            File.WriteAllText(path, html, Encoding.UTF8);
            webView.CoreWebView2.Navigate(path);
            loadingOverlay.Visibility = Visibility.Collapsed;
        }

        async void Refresh()
        {
            _filtered = Apply();
            UpdateUI();
            if (!_ready) return;
            try { await webView.CoreWebView2.ExecuteScriptAsync("updateMarkers(" + Ser(_filtered) + ")"); }
            catch { }
        }

        void UpdateUI()
        {
            var (s, e) = Range();
            txtOkres.Text = $"{s:dd.MM} - {e.AddDays(-1):dd.MM.yyyy}";
            txtKpiDostawy.Text = _filtered.Count.ToString();
            txtKpiHodowcy.Text = _filtered.Select(d => d.Nazwa).Distinct().Count().ToString();
            txtKpiAuta.Text = _filtered.Sum(d => d.Auta).ToString();

            panelDostawy.Children.Clear();
            int n = 0;
            foreach (var g in _filtered.GroupBy(d => d.Data.Date).OrderBy(g => g.Key))
            {
                string col = DC.GetValueOrDefault(g.Key.DayOfWeek, "#6B7280");
                // Day header
                var hdr = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(col + "22")), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(2, 5, 2, 1) };
                hdr.Child = new TextBlock { Text = $"{DF[(int)g.Key.DayOfWeek]} {g.Key:dd.MM}  -  {g.Count()} dostaw, {g.Sum(d => d.Auta)} aut", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(col)) };
                panelDostawy.Children.Add(hdr);

                foreach (var d in g.OrderBy(x => x.Nazwa))
                {
                    panelDostawy.Children.Add(Card(d, col));
                    n++;
                }
            }
            txtLiczbaLista.Text = $"{n} dostaw na mapie";
        }

        Border Card(Dost d, string col)
        {
            var c = new Border
            {
                Background = Brushes.White, CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(2, 1, 2, 1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand, Tag = d.LP
            };
            c.MouseLeftButtonUp += async (s, e) =>
            {
                if (_ready) try { await webView.CoreWebView2.ExecuteScriptAsync($"focusMarker(\"{d.LP}\")"); } catch { }
            };
            c.MouseEnter += (s, e) => c.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
            c.MouseLeave += (s, e) => c.Background = Brushes.White;

            var g = new Grid();
            g.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dot = new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(col)), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 6, 0) };
            Grid.SetColumn(dot, 0); g.Children.Add(dot);

            string sc = SC(d.Status);
            var sp = new StackPanel(); Grid.SetColumn(sp, 1);
            var row1 = new DockPanel();
            row1.Children.Add(new TextBlock { Text = d.Nazwa, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")), TextTrimming = TextTrimming.CharacterEllipsis });
            var badge = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sc + "22")), CornerRadius = new CornerRadius(3), Padding = new Thickness(4, 0, 4, 0), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(4, 0, 0, 0) };
            string shortS = d.Status ?? "";
            if (shortS.Length > 7) shortS = shortS.Substring(0, 7) + ".";
            badge.Child = new TextBlock { Text = shortS, FontSize = 8, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sc)) };
            DockPanel.SetDock(badge, Dock.Right);
            row1.Children.Insert(0, badge);
            sp.Children.Add(row1);
            sp.Children.Add(new TextBlock { Text = $"{d.Auta} aut | {d.Szt:#,0} szt | {d.Waga:0.00}kg | {d.Km}km", FontSize = 9, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")) });
            g.Children.Add(sp);
            c.Child = g;
            return c;
        }

        void ScrollTo(string lp)
        {
            foreach (var ch in panelDostawy.Children)
                if (ch is Border b && b.Tag is string t && t == lp)
                {
                    b.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE"));
                    b.BringIntoView();
                    var tm = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    tm.Tick += (s, e) => { b.Background = Brushes.White; tm.Stop(); };
                    tm.Start();
                }
        }

        static string SC(string s) => (s ?? "").ToLower() switch
        {
            "potwierdzony" => "#16A34A", "anulowany" => "#DC2626", "sprzedany" => "#2563EB",
            "do wykupienia" => "#7C3AED", "b.wolny." => "#D97706", "b.kontr." => "#7C3AED", _ => "#D97706"
        };

        #endregion

        #region Diagnostyka

        void BtnDiagnostyka_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            var (ps, pe) = Range();

            sb.AppendLine("=== DIAGNOSTYKA MAPY DOSTAW ===");
            sb.AppendLine($"Czas: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"WeekStart: {_weekStart:yyyy-MM-dd} | Okres: {_period} | Filtr: {ps:yyyy-MM-dd} - {pe:yyyy-MM-dd}");
            sb.AppendLine($"SQL zakres: {_sqlStart:yyyy-MM-dd} - {_sqlEnd:yyyy-MM-dd}");
            sb.AppendLine($"KodyPocztowe: {_geoCount} | SQL total: {_dbTotal} | Z geo: {_all.Count} | Bez geo: {_noGeo.Count}");
            sb.AppendLine($"Filtry - Potw:{chkPotwierdzony.IsChecked} DoWyk:{chkDoWykupienia.IsChecked} Inne:{chkInne.IsChecked} Anul:{chkAnulowany.IsChecked} Sprz:{chkSprzedany.IsChecked}");
            sb.AppendLine($"Szukaj: '{txtSzukaj.Text}' | Po filtrach: {_filtered.Count}");
            sb.AppendLine();

            // Wszystkie dostawy w okresie (z geo + bez geo) pogrupowane po dniach
            var allInPeriod = _all.Where(d => d.Data >= ps && d.Data < pe).ToList();
            var noGeoInPeriod = _noGeo.Where(d => d.Data >= ps && d.Data < pe).ToList();
            var combined = allInPeriod.Select(d => (d, hasGeo: true))
                .Concat(noGeoInPeriod.Select(d => (d, hasGeo: false)))
                .OrderBy(x => x.d.Data).ThenBy(x => x.d.Nazwa)
                .ToList();

            sb.AppendLine($"=== WSZYSTKIE DOSTAWY W OKRESIE: {combined.Count} (mapa: {allInPeriod.Count}, brak geo: {noGeoInPeriod.Count}) ===");
            sb.AppendLine();

            foreach (var dayGroup in combined.GroupBy(x => x.d.Data.Date).OrderBy(g => g.Key))
            {
                string dayTag = dayGroup.Key.ToString("yyyy-MM-dd");
                bool dayChecked = _days.Contains(dayTag);
                int onMap = dayGroup.Count(x => x.hasGeo);
                int noGeoDay = dayGroup.Count(x => !x.hasGeo);

                sb.AppendLine($"--- {DN[(int)dayGroup.Key.DayOfWeek].ToUpper()} {dayGroup.Key:dd.MM.yyyy} --- razem: {dayGroup.Count()}, mapa: {onMap}, brak geo: {noGeoDay} {(dayChecked ? "" : "[DZIEN ODFILTROWANY]")}");

                foreach (var (d, hasGeo) in dayGroup.OrderBy(x => x.d.Nazwa))
                {
                    // Sprawdz czy przeszla filtr statusu
                    string st = (d.Status ?? "").ToLower();
                    bool statusOk;
                    string statusFilter;
                    if (st == "anulowany") { statusOk = chkAnulowany.IsChecked == true; statusFilter = "chkAnulowany"; }
                    else if (st == "sprzedany") { statusOk = chkSprzedany.IsChecked == true; statusFilter = "chkSprzedany"; }
                    else if (st == "potwierdzony") { statusOk = chkPotwierdzony.IsChecked == true; statusFilter = "chkPotwierdzony"; }
                    else if (st == "do wykupienia") { statusOk = chkDoWykupienia.IsChecked == true; statusFilter = "chkDoWykupienia"; }
                    else { statusOk = chkInne.IsChecked == true; statusFilter = "chkInne"; }

                    // Buduj info o widocznosci
                    string vis;
                    if (!hasGeo) vis = $"BRAK GEO (kod: {(string.IsNullOrEmpty(d.Kod) ? "PUSTY" : d.Kod)})";
                    else if (!dayChecked) vis = "DZIEN ODFILTROWANY";
                    else if (!statusOk) vis = $"STATUS ODFILTROWANY ({statusFilter}=False)";
                    else vis = "OK - na mapie";

                    sb.AppendLine($"  LP:{d.LP} | {d.Nazwa,-25} | {d.Auta}aut | {d.Status,-15} | kod:{d.Kod,-8} | {d.Miasto,-15} | {vis}");
                }
                sb.AppendLine();
            }

            // Podsumowanie statusow
            sb.AppendLine("=== STATUSY W OKRESIE ===");
            foreach (var g in combined.GroupBy(x => string.IsNullOrEmpty(x.d.Status) ? "(pusty)" : x.d.Status).OrderByDescending(g => g.Count()))
                sb.AppendLine($"  {g.Key}: {g.Count()} (geo: {g.Count(x => x.hasGeo)}, brak: {g.Count(x => !x.hasGeo)})");

            string report = sb.ToString();
            Clipboard.SetText(report);
            MessageBox.Show("Diagnostyka skopiowana do schowka!\n\nWklej w wiadomosc (Ctrl+V).\n\nRozmiar: " + report.Length + " znakow, " + combined.Count + " dostaw w okresie.",
                "Diagnostyka", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        async void BtnUzupelnijGeo_Click(object sender, RoutedEventArgs e)
        {
            // Zbierz brakujace kody
            var missing = _noGeo.Select(d => d.Kod?.Replace("-", "")?.Trim() ?? "")
                .Where(k => k.Length >= 5)
                .Distinct()
                .ToList();

            if (missing.Count == 0)
            {
                MessageBox.Show("Brak kodow pocztowych do uzupelnienia.\nDostawy bez geo maja pusty kod pocztowy w tabeli Dostawcy.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Znaleziono {missing.Count} brakujacych kodow pocztowych:\n{string.Join(", ", missing.Take(15))}{(missing.Count > 15 ? "..." : "")}\n\nPobrac koordynaty z Nominatim (OpenStreetMap) i zapisac do KodyPocztowe?\n(~1 sekunda na kod)", "Uzupelnij geokody", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            txtDebug.Text = "Geokodowanie...";
            int ok = 0, fail = 0;
            var log = new StringBuilder();

            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "Kalendarz1-MapaDostaw/1.0");

                foreach (var kod in missing)
                {
                    string kodFormatted = kod.Length >= 5 ? kod.Insert(2, "-") : kod;
                    txtDebug.Text = $"Geokodowanie {kodFormatted} ({ok + fail + 1}/{missing.Count})...";
                    await Task.Delay(1100); // Nominatim rate limit: 1 req/sec

                    try
                    {
                        string url = $"https://nominatim.openstreetmap.org/search?postalcode={kodFormatted}&country=Poland&format=json&limit=1";
                        string resp = await http.GetStringAsync(url);
                        using var doc = JsonDocument.Parse(resp);
                        var arr = doc.RootElement;

                        if (arr.GetArrayLength() > 0)
                        {
                            var item = arr[0];
                            double lat = double.Parse(item.GetProperty("lat").GetString(), CultureInfo.InvariantCulture);
                            double lng = double.Parse(item.GetProperty("lon").GetString(), CultureInfo.InvariantCulture);

                            using var conn = new SqlConnection(ConnStr);
                            await conn.OpenAsync();

                            // Sprawdz czy juz istnieje
                            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM KodyPocztowe WHERE Kod = @kod", conn))
                            {
                                cmd.Parameters.AddWithValue("@kod", kodFormatted);
                                int cnt = (int)await cmd.ExecuteScalarAsync();
                                if (cnt > 0)
                                {
                                    // Update
                                    using var upd = new SqlCommand("UPDATE KodyPocztowe SET Latitude = @lat, Longitude = @lng WHERE Kod = @kod", conn);
                                    upd.Parameters.AddWithValue("@lat", lat);
                                    upd.Parameters.AddWithValue("@lng", lng);
                                    upd.Parameters.AddWithValue("@kod", kodFormatted);
                                    await upd.ExecuteNonQueryAsync();
                                }
                                else
                                {
                                    // Insert
                                    using var ins = new SqlCommand("INSERT INTO KodyPocztowe (Kod, Latitude, Longitude) VALUES (@kod, @lat, @lng)", conn);
                                    ins.Parameters.AddWithValue("@kod", kodFormatted);
                                    ins.Parameters.AddWithValue("@lat", lat);
                                    ins.Parameters.AddWithValue("@lng", lng);
                                    await ins.ExecuteNonQueryAsync();
                                }
                            }

                            log.AppendLine($"OK: {kodFormatted} -> {lat}, {lng}");
                            ok++;
                        }
                        else
                        {
                            log.AppendLine($"NIE ZNALEZIONO: {kodFormatted}");
                            fail++;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.AppendLine($"BLAD: {kodFormatted} -> {ex.Message}");
                        fail++;
                    }
                }

                // Przeladuj dane
                txtDebug.Text = "Przeladowywanie danych...";
                await LoadDataAsync();
                BuildDayFilters();
                Refresh();

                MessageBox.Show($"Gotowe!\n\nDodano: {ok}\nNie znaleziono: {fail}\n\n{log}", "Geokodowanie", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region HTML

        static string Ser(List<Dost> pts)
        {
            var d = pts.Select(p => new { lp = p.LP, n = p.Nazwa, m = p.Miasto, a = p.Adres, au = p.Auta, sz = p.Szt, w = p.Waga, s = p.Status, c = p.Cena, tc = p.TypCeny, km = p.Km, tel = p.Tel, dt = p.Data.ToString("yyyy-MM-dd"), dw = (int)p.Data.DayOfWeek, lat = p.Lat, lng = p.Lng });
            return JsonSerializer.Serialize(d, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }

        string Html(string json)
        {
            var sb = new StringBuilder(16000);
            string bLat = BazaLat.ToString(CultureInfo.InvariantCulture);
            string bLng = BazaLng.ToString(CultureInfo.InvariantCulture);

            sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
            sb.Append("<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>");
            sb.Append("<link rel='stylesheet' href='https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.css'/>");
            sb.Append("<link rel='stylesheet' href='https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.Default.css'/>");
            sb.Append("<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>");
            sb.Append("<script src='https://unpkg.com/leaflet.markercluster@1.5.3/dist/leaflet.markercluster.js'></script>");
            sb.Append(@"<style>
*{margin:0;padding:0;box-sizing:border-box}html,body{height:100%;font-family:'Segoe UI',sans-serif}#map{height:100%}
.lg{position:fixed;bottom:16px;left:16px;background:#fff;border-radius:8px;padding:10px 14px;box-shadow:0 2px 8px rgba(0,0,0,.15);z-index:9999;font-size:11px;color:#334155}
.lg b{display:block;margin-bottom:4px;font-size:12px;color:#1e293b}
.lg-i{display:flex;align-items:center;margin:2px 0}.lg-d{width:10px;height:10px;border-radius:50%;margin-right:6px;border:2px solid #fff;box-shadow:0 0 2px rgba(0,0,0,.3)}
.ri{position:fixed;bottom:16px;right:16px;background:#fff;border-radius:8px;padding:12px 16px;box-shadow:0 2px 12px rgba(0,0,0,.2);z-index:9999;font-size:12px;color:#334155;min-width:200px;display:none}
.ri b{color:#1e293b}.ri-t{font-weight:700;font-size:13px;color:#1e293b;margin-bottom:4px}
.ri-c{position:absolute;top:4px;right:8px;cursor:pointer;color:#94a3b8;font-size:18px;font-weight:700}.ri-c:hover{color:#ef4444}
</style></head><body><div id='map'></div>");

            sb.Append("<div class='lg'><b>Dzien tygodnia</b>");
            sb.Append("<div class='lg-i'><div class='lg-d' style='background:#3B82F6'></div>Pon</div>");
            sb.Append("<div class='lg-i'><div class='lg-d' style='background:#22C55E'></div>Wt</div>");
            sb.Append("<div class='lg-i'><div class='lg-d' style='background:#F59E0B'></div>Sr</div>");
            sb.Append("<div class='lg-i'><div class='lg-d' style='background:#EF4444'></div>Czw</div>");
            sb.Append("<div class='lg-i'><div class='lg-d' style='background:#8B5CF6'></div>Pt</div>");
            sb.Append("<div class='lg-i'><div class='lg-d' style='background:#06B6D4'></div>Sob</div>");
            sb.Append("</div>");

            sb.Append("<div id='ri' class='ri'><span class='ri-c' onclick='clearRoute()'>x</span><div id='rt' class='ri-t'></div><div id='rd'></div><div id='rdu'></div></div>");

            sb.Append("<script>");
            sb.Append($"var BL={bLat},BN={bLng};");
            sb.Append("var DC={0:'#EC4899',1:'#3B82F6',2:'#22C55E',3:'#F59E0B',4:'#EF4444',5:'#8B5CF6',6:'#06B6D4'};");
            sb.Append("var DN=['niedz','pon','wt','sr','czw','pt','sob'];");
            sb.Append("var SC={'potwierdzony':'#16a34a','anulowany':'#dc2626','sprzedany':'#2563eb','do wykupienia':'#7c3aed','b.wolny.':'#d97706','b.kontr.':'#7c3aed'};");

            sb.Append("var map=L.map('map').setView([52,19.5],7);");
            sb.Append("L.tileLayer('https://mt1.google.com/vt/lyrs=m&hl=pl&x={x}&y={y}&z={z}',{maxZoom:20}).addTo(map);");

            // Baza marker
            sb.Append("L.marker([BL,BN],{icon:L.divIcon({className:'',html:'<div style=\"width:26px;height:26px;border-radius:50%;background:#16A34A;border:3px solid #fff;box-shadow:0 0 10px rgba(22,163,74,.6);display:flex;align-items:center;justify-content:center;color:#fff;font-weight:bold;font-size:12px\">B</div>',iconSize:[26,26],iconAnchor:[13,13]}),zIndexOffset:1000}).addTo(map).bindPopup('<b>BAZA - Ubojnia Drobiu Piorkowscy</b><br>Koziolki 40, 95-061 Dmosin');");

            sb.Append("var MK={},MD={},ML=L.markerClusterGroup({maxClusterRadius:40,spiderfyOnMaxZoom:true,showCoverageOnHover:false,zoomToBoundsOnClick:true,disableClusteringAtZoom:14}).addTo(map),CR=null;");

            sb.Append("function clearRoute(){if(CR){map.removeLayer(CR);CR=null}document.getElementById('ri').style.display='none'}");

            sb.Append(@"function showRoute(lp){clearRoute();var d=MD[lp];if(!d)return;
fetch('https://router.project-osrm.org/route/v1/driving/'+BN+','+BL+';'+d.lng+','+d.lat+'?overview=full&geometries=geojson')
.then(r=>r.json()).then(j=>{if(!j.routes||!j.routes.length)return;var rt=j.routes[0];
var co=rt.geometry.coordinates.map(c=>[c[1],c[0]]);
CR=L.polyline(co,{color:DC[d.dw]||'#22C55E',weight:4,opacity:.8,dashArray:'8,6'}).addTo(map);
var km=(rt.distance/1000).toFixed(1),mn=Math.round(rt.duration/60),h=Math.floor(mn/60),m=mn%60;
document.getElementById('rt').textContent=d.n+' ('+DN[d.dw]+' '+d.dt.substring(5)+')';
document.getElementById('rd').innerHTML='Dystans: <b>'+km+' km</b>';
document.getElementById('rdu').innerHTML='Czas: <b>'+(h>0?h+'h '+m+'min':m+' min')+'</b>';
document.getElementById('ri').style.display='block'}).catch(e=>console.error(e))}");

            sb.Append(@"function spread(data){var g={};data.forEach(d=>{var k=d.lat.toFixed(5)+','+d.lng.toFixed(5);(g[k]=g[k]||[]).push(d)});
Object.values(g).forEach(a=>{if(a.length<2)return;a.forEach((d,i)=>{var an=2*Math.PI*i/a.length,r=.0008*(1+Math.floor(i/8)*.5);d.lat+=Math.cos(an)*r;d.lng+=Math.sin(an)*r})})}");

            sb.Append(@"function addMarkers(data){map.removeLayer(ML);ML=L.markerClusterGroup({maxClusterRadius:35,spiderfyOnMaxZoom:true,showCoverageOnHover:false,zoomToBoundsOnClick:true,disableClusteringAtZoom:14});
MK={};MD={};spread(data);data.forEach(d=>{MD[d.lp]=d;var co=DC[d.dw]||'#888',cf=(d.s||'').toLowerCase()==='potwierdzony',sz=cf?16:11,bw=cf?3:2;
var ic=L.divIcon({className:'',html:'<div style=""width:'+sz+'px;height:'+sz+'px;border-radius:50%;background:'+co+';border:'+bw+'px solid #fff;box-shadow:0 0 '+(cf?8:3)+'px '+co+'""></div>',iconSize:[sz,sz],iconAnchor:[sz/2,sz/2]});
var sc=SC[(d.s||'').toLowerCase()]||'#d97706';
var p=`<div style='font-size:11px;font-weight:700;color:#fff;background:${co};padding:2px 8px;border-radius:4px;display:inline-block;margin-bottom:6px'>${DN[d.dw]} ${d.dt.substring(5)}</div>`
+`<div style='font-size:14px;font-weight:700;margin-bottom:4px'>${d.n}</div>`
+`<span style='display:inline-block;padding:1px 6px;border-radius:3px;font-size:10px;font-weight:700;color:#fff;background:${sc}'>${d.s}</span>`
+`<div style='display:flex;gap:6px;margin:6px 0;font-size:11px;font-weight:600;color:#334155'><span style='background:#f1f5f9;padding:2px 6px;border-radius:3px'>${d.au} aut</span><span style='background:#f1f5f9;padding:2px 6px;border-radius:3px'>${Math.round(d.sz)} szt</span><span style='background:#f1f5f9;padding:2px 6px;border-radius:3px'>${Number(d.w).toFixed(2)} kg</span></div>`
+(d.a?`<div style='font-size:11px;color:#555'>${d.a}</div>`:'')+`<div style='font-size:11px;color:#555'>${d.m} | ${d.km} km</div>`
+(d.c>0?`<div style='font-size:11px;color:#555'>${Number(d.c).toFixed(2)} zl (${d.tc})</div>`:'')+");

            // telefon i przycisk trasy - osobno bo zawiera onclick z apostrofami
            sb.Append(@"(d.tel?`<div style='font-size:12px;color:#16a34a;font-weight:700;margin-top:4px'>${d.tel}</div>`:'')+`<div style='margin-top:6px'><button style='padding:4px 12px;background:#16A34A;color:#fff;font-size:11px;font-weight:700;border:none;border-radius:6px;cursor:pointer' onclick=""showRoute('${d.lp}')"">Pokaz trase</button></div>`;");

            sb.Append(@"var mk=L.marker([d.lat,d.lng],{icon:ic}).bindPopup(p,{maxWidth:280});
mk.on('click',()=>window.chrome.webview.postMessage(JSON.stringify({action:'select',lp:d.lp})));
MK[d.lp]=mk;ML.addLayer(mk)});map.addLayer(ML);
if(data.length){var b=L.latLngBounds(data.map(d=>[d.lat,d.lng]));b.extend([BL,BN]);if(b.isValid())map.fitBounds(b,{padding:[40,40]})}}");

            sb.Append("function focusMarker(lp){var m=MK[lp];if(m){clearRoute();ML.zoomToShowLayer(m,()=>{m.openPopup();showRoute(lp)})}}");
            sb.Append("function updateMarkers(data){try{clearRoute();addMarkers(data)}catch(e){console.error(e)}}");
            sb.Append($"addMarkers({json});");
            sb.Append("</script></body></html>");
            return sb.ToString();
        }

        #endregion

        #region Model

        class Dost
        {
            public string LP { get; set; }
            public DateTime Data { get; set; }
            public string Nazwa { get; set; }
            public int Auta { get; set; }
            public double Szt { get; set; }
            public decimal Waga { get; set; }
            public string Status { get; set; }
            public decimal Cena { get; set; }
            public string TypCeny { get; set; }
            public string Miasto { get; set; }
            public string Adres { get; set; }
            public string Kod { get; set; }
            public int Km { get; set; }
            public string Tel { get; set; }
            public double Lat { get; set; }
            public double Lng { get; set; }
        }

        #endregion
    }
}
