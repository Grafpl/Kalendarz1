using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Kalendarz1.MapaFloty
{
    /// <summary>
    /// Osobne lekkie okno (bez mapy) do mapowania Webfleet GPS ↔ TransportPL.
    /// Pobiera vehicles z Webfleet API i pokazuje progress mapowania per typ.
    /// </summary>
    public partial class MapowanieWebfletHubWindow : Window
    {
        private static string WfAccount => Kalendarz1.Webfleet.WebfleetConfig.Account;
        private static string WfUser    => Kalendarz1.Webfleet.WebfleetConfig.User;
        private static string WfPass    => Kalendarz1.Webfleet.WebfleetConfig.Pass;
        private static string WfApiKey  => Kalendarz1.Webfleet.WebfleetConfig.ApiKey;
        private static string WfBaseUrl => Kalendarz1.Webfleet.WebfleetConfig.BaseUrl;
        private static string BasicAuth => Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{WfUser}:{WfPass}"));
        private static HttpClient Http => Kalendarz1.Webfleet.WebfleetHttp.Instance;

        private const string _connTransport =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private List<MapaFlotyView.VehiclePosition> _vehicles = new();

        public MapowanieWebfletHubWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            Loaded += async (_, _) =>
            {
                TxtBaseUrl.Text = Kalendarz1.Webfleet.WebfleetConfig.BaseUrl;
                await LoadAllAsync();
            };
        }

        private void BtnUrlCsv_Click(object sender, RoutedEventArgs e)
            => TxtBaseUrl.Text = "https://csv.webfleet.com/extern";

        private void BtnUrlApi_Click(object sender, RoutedEventArgs e)
            => TxtBaseUrl.Text = "https://api.webfleet.com/extern";

        private void BtnSaveUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string newUrl = TxtBaseUrl.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(newUrl) || !newUrl.StartsWith("http"))
                {
                    MessageBox.Show("URL musi się zaczynać od http(s)://", "Niepoprawny URL",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // Update secrets.json
                string path = Kalendarz1.Webfleet.WebfleetConfig.SecretsPath;
                Newtonsoft.Json.Linq.JObject json;
                if (System.IO.File.Exists(path))
                    json = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(path));
                else
                    json = new Newtonsoft.Json.Linq.JObject();
                json["BaseUrl"] = newUrl;
                System.IO.File.WriteAllText(path, json.ToString(Newtonsoft.Json.Formatting.Indented));
                Kalendarz1.Webfleet.WebfleetConfig.Przeladuj();
                _ = LoadAllAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się zapisać:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadAllAsync()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            BtnPojazdy.IsEnabled = false;
            BtnKierowcy.IsEnabled = false;
            TxtPojazdyProgress.Text = "—";
            TxtKierowcyProgress.Text = "—";
            ProgressPojazdy.Width = 0;
            ProgressKierowcy.Width = 0;

            try
            {
                TxtLoading.Text = "Pobieranie pojazdów z Webfleet...";
                _vehicles = await FetchVehiclesAsync();

                TxtLoading.Text = "Liczenie zmapowanych...";
                var (mappedVeh, mappedDrv) = await FetchMappedCountsAsync();

                UpdateProgress(_vehicles.Count, mappedVeh, mappedDrv);

                BtnPojazdy.IsEnabled = _vehicles.Count > 0;
                BtnKierowcy.IsEnabled = _vehicles.Count > 0;

                TxtStatus.Text = $"Webfleet: {_vehicles.Count} pojazdów  ·  " +
                                 $"Zmapowane pojazdy: {mappedVeh}/{_vehicles.Count}  ·  " +
                                 $"Zmapowani kierowcy: {mappedDrv}  ·  " +
                                 $"Aktualizacja: {DateTime.Now:HH:mm:ss}";
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                TxtStatus.Text = $"Błąd Webfleet: {ex.GetType().Name}: {ex.Message}";

                // Pokaz banner z wyjasnieniem migracji OAuth
                if (MigrationBanner != null) MigrationBanner.Visibility = Visibility.Visible;

                try
                {
                    var (mv, md) = await FetchMappedCountsAsync();
                    UpdateProgress(0, mv, md);
                    BtnPojazdy.IsEnabled = true;
                    BtnKierowcy.IsEnabled = true;
                }
                catch { }

                string report = await BuildFullDiagnosticReport(ex);
                ShowDiagnosticDialog(report);
            }
        }

        private async Task<string> BuildFullDiagnosticReport(Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("════════════════════════════════════════════════════════════════");
            sb.AppendLine("  DIAGNOSTYKA BŁĘDU WEBFLEET — MapowanieWebfletHubWindow");
            sb.AppendLine("════════════════════════════════════════════════════════════════");
            sb.AppendLine();

            // [1] CZAS + ENV
            sb.AppendLine("── [1] ŚRODOWISKO ─────────────────────────────────────────────");
            sb.AppendLine($"Data/godzina:    {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} (lokalna)");
            sb.AppendLine($"UTC:             {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Maszyna:         {Environment.MachineName}");
            sb.AppendLine($"Użytkownik OS:   {Environment.UserName}");
            sb.AppendLine($"OS:              {Environment.OSVersion}");
            sb.AppendLine($"App user:        {App.UserID ?? "—"} ({App.UserFullName ?? "—"})");
            sb.AppendLine($".NET:            {Environment.Version}");
            sb.AppendLine();

            // [2] WEBFLEET CONFIG (credentials zamaskowane)
            sb.AppendLine("── [2] WEBFLEET CONFIG ─────────────────────────────────────────");
            string baseUrl = Kalendarz1.Webfleet.WebfleetConfig.BaseUrl;
            string account = Kalendarz1.Webfleet.WebfleetConfig.Account;
            string user = Kalendarz1.Webfleet.WebfleetConfig.User;
            string pass = Kalendarz1.Webfleet.WebfleetConfig.Pass;
            string apiKey = Kalendarz1.Webfleet.WebfleetConfig.ApiKey;
            sb.AppendLine($"BaseUrl:         {baseUrl}");
            sb.AppendLine($"Account:         {account}");
            sb.AppendLine($"User:            {user}");
            sb.AppendLine($"Pass:            {Mask(pass)}  (len={pass?.Length ?? 0})");
            sb.AppendLine($"ApiKey:          {Mask(apiKey)}  (len={apiKey?.Length ?? 0})");
            sb.AppendLine($"Secrets path:    {Kalendarz1.Webfleet.WebfleetConfig.SecretsPath}");
            sb.AppendLine($"Secrets exists:  {System.IO.File.Exists(Kalendarz1.Webfleet.WebfleetConfig.SecretsPath)}");
            sb.AppendLine();

            // [3] PEŁEN URL (z apikey w plain — ten endpoint i tak musi mieć)
            string fullUrl = $"{baseUrl}?account={Uri.EscapeDataString(account)}" +
                             $"&apikey={Uri.EscapeDataString(apiKey)}" +
                             $"&lang=pl&outputformat=json&action=showObjectReportExtern";
            sb.AppendLine("── [3] FULL REQUEST URL ────────────────────────────────────────");
            sb.AppendLine(fullUrl);
            sb.AppendLine();

            // [4] DNS lookup
            sb.AppendLine("── [4] DNS LOOKUP (csv.webfleet.com) ──────────────────────────");
            try
            {
                var host = new Uri(baseUrl).Host;
                sb.AppendLine($"Host:            {host}");
                var addrs = await System.Net.Dns.GetHostAddressesAsync(host);
                if (addrs.Length == 0) sb.AppendLine("Result:          BRAK adresów IP!");
                else
                {
                    sb.AppendLine($"Result:          {addrs.Length} adres(ów)");
                    foreach (var a in addrs) sb.AppendLine($"                 • {a.AddressFamily}: {a}");
                }
            }
            catch (Exception dnsEx)
            {
                sb.AppendLine($"DNS FAILED:      {dnsEx.GetType().Name}: {dnsEx.Message}");
            }
            sb.AppendLine();

            // [5] TCP probe port 443
            sb.AppendLine("── [5] TCP CONNECT TEST (csv.webfleet.com:443) ────────────────");
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                var connectTask = tcp.ConnectAsync(new Uri(baseUrl).Host, 443);
                var completed = await Task.WhenAny(connectTask, Task.Delay(5000));
                if (completed != connectTask)
                {
                    sb.AppendLine("TCP:             TIMEOUT (>5s) — port nie odpowiada");
                }
                else
                {
                    await connectTask;
                    sb.AppendLine($"TCP:             OK — połączenie nawiązane, lokalny port: {(tcp.Client.LocalEndPoint as System.Net.IPEndPoint)?.Port}");
                }
                tcp.Close();
            }
            catch (Exception tcpEx)
            {
                sb.AppendLine($"TCP FAILED:      {tcpEx.GetType().Name}: {tcpEx.Message}");
                if (tcpEx.InnerException != null)
                    sb.AppendLine($"  → inner:       {tcpEx.InnerException.GetType().Name}: {tcpEx.InnerException.Message}");
            }
            sb.AppendLine();

            // [6] PROXY DETECTION
            sb.AppendLine("── [6] SYSTEM PROXY ───────────────────────────────────────────");
            try
            {
                var proxy = System.Net.WebRequest.GetSystemWebProxy();
                var proxyUri = proxy.GetProxy(new Uri(baseUrl));
                if (proxyUri == null) sb.AppendLine("Proxy:           brak (direct)");
                else if (proxyUri.ToString() == baseUrl) sb.AppendLine("Proxy:           brak (direct dla tego URL)");
                else sb.AppendLine($"Proxy:           {proxyUri}");
            }
            catch (Exception pEx)
            {
                sb.AppendLine($"Proxy check:     {pEx.Message}");
            }
            sb.AppendLine();

            // [7] EXCEPTION — pełen chain
            sb.AppendLine("── [7] EXCEPTION CHAIN ────────────────────────────────────────");
            var cur = ex;
            int level = 0;
            while (cur != null)
            {
                sb.AppendLine($"[level {level}] {cur.GetType().FullName}");
                sb.AppendLine($"   Message:      {cur.Message}");
                if (cur is System.Net.Http.HttpRequestException httpEx)
                {
                    sb.AppendLine($"   StatusCode:   {httpEx.StatusCode}");
                }
                if (cur is System.Net.Sockets.SocketException sockEx)
                {
                    sb.AppendLine($"   SocketError:  {sockEx.SocketErrorCode}");
                    sb.AppendLine($"   NativeError:  {sockEx.NativeErrorCode}");
                }
                if (cur.HResult != 0)
                    sb.AppendLine($"   HResult:      0x{cur.HResult:X8} ({cur.HResult})");
                if (!string.IsNullOrEmpty(cur.StackTrace))
                {
                    sb.AppendLine("   StackTrace:");
                    foreach (var line in cur.StackTrace.Split('\n'))
                        sb.AppendLine($"     {line.TrimEnd()}");
                }
                cur = cur.InnerException;
                level++;
                if (level > 10) { sb.AppendLine("(...truncated)"); break; }
            }
            sb.AppendLine();

            // [8] CHECKLISTA
            sb.AppendLine("── [8] CHECKLISTA ─────────────────────────────────────────────");
            sb.AppendLine("  □ Otwórz cmd → ping csv.webfleet.com");
            sb.AppendLine("  □ Otwórz w przeglądarce: https://csv.webfleet.com (powinno się wczytać)");
            sb.AppendLine("  □ Sprawdź czy MapaFloty działa (jeśli też pada — problem sieciowy)");
            sb.AppendLine("  □ Restart aplikacji (czasem credentials nie załadowane)");
            sb.AppendLine($"  □ Otwórz: {Kalendarz1.Webfleet.WebfleetConfig.SecretsPath}");
            sb.AppendLine("    i zweryfikuj BaseUrl/User/Pass/ApiKey/Account");
            sb.AppendLine();
            sb.AppendLine("════════════════════════════════════════════════════════════════");
            sb.AppendLine("Skopiuj cały raport (Ctrl+A → Ctrl+C w dialogu) i wklej do chatu.");
            sb.AppendLine("════════════════════════════════════════════════════════════════");

            return sb.ToString();
        }

        private static string Mask(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "(puste)";
            if (s.Length <= 4) return new string('•', s.Length);
            return s.Substring(0, 2) + new string('•', s.Length - 4) + s.Substring(s.Length - 2);
        }

        private void ShowDiagnosticDialog(string report)
        {
            // Guard: parent (this) mogl zostac zamkniety jesli user zamknal okno
            // podczas async fetch - nie da sie wtedy ustawic Owner. Pomijamy Owner w tym przypadku.
            bool parentAlive = System.Windows.PresentationSource.FromVisual(this) != null;

            // Custom window z TextBox (monospace, selectable) + przycisk Copy
            var win = new Window
            {
                Title = "Diagnostyka Webfleet — szczegółowy raport (skopiuj i wklej)",
                Width = 900,
                Height = 700,
                WindowStartupLocation = parentAlive ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                Background = System.Windows.Media.Brushes.White
            };
            if (parentAlive) win.Owner = this;

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            var hdr = new System.Windows.Controls.TextBlock
            {
                Text = "⚠  Webfleet API nie odpowiada — raport diagnostyczny",
                FontSize = 16,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(198, 40, 40)),
                Margin = new System.Windows.Thickness(16, 14, 16, 8)
            };
            System.Windows.Controls.Grid.SetRow(hdr, 0);
            grid.Children.Add(hdr);

            var tb = new System.Windows.Controls.TextBox
            {
                Text = report,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = System.Windows.TextWrapping.NoWrap,
                FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New"),
                FontSize = 11.5,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 251, 252)),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(38, 50, 56)),
                Padding = new System.Windows.Thickness(12),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 227, 234)),
                BorderThickness = new System.Windows.Thickness(1),
                Margin = new System.Windows.Thickness(16, 0, 16, 8),
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
            };
            System.Windows.Controls.Grid.SetRow(tb, 1);
            grid.Children.Add(tb);

            var footer = new System.Windows.Controls.DockPanel
            {
                Margin = new System.Windows.Thickness(16, 6, 16, 14)
            };

            var hint = new System.Windows.Controls.TextBlock
            {
                Text = "Kliknij \"Kopiuj do schowka\" lub Ctrl+A → Ctrl+C w polu tekstu",
                FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(96, 125, 139)),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            footer.Children.Add(hint);

            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            System.Windows.Controls.DockPanel.SetDock(btnPanel, System.Windows.Controls.Dock.Right);

            var btnCopy = new System.Windows.Controls.Button
            {
                Content = "📋  Kopiuj do schowka",
                Padding = new System.Windows.Thickness(16, 8, 16, 8),
                FontSize = 13,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 35, 126)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new System.Windows.Thickness(0, 0, 8, 0)
            };
            btnCopy.Click += (_, _) =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(report);
                    btnCopy.Content = "✓  Skopiowano!";
                    btnCopy.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 125, 50));
                }
                catch { btnCopy.Content = "Błąd kopiowania"; }
            };
            btnPanel.Children.Add(btnCopy);

            var btnClose = new System.Windows.Controls.Button
            {
                Content = "Zamknij",
                Padding = new System.Windows.Thickness(16, 8, 16, 8),
                FontSize = 13,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnClose.Click += (_, _) => win.Close();
            btnPanel.Children.Add(btnClose);

            footer.Children.Add(btnPanel);
            System.Windows.Controls.Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            win.Content = grid;
            win.ShowDialog();
        }

        private void UpdateProgress(int totalWebfleet, int mappedVeh, int mappedDrv)
        {
            int uniqueDrivers = _vehicles.Where(v => !string.IsNullOrEmpty(v.WebfleetDriverId))
                                          .Select(v => v.WebfleetDriverId).Distinct().Count();
            int totalDriversOnGps = Math.Max(uniqueDrivers, 1);

            // Pojazdy progress
            TxtPojazdyProgress.Text = totalWebfleet > 0 ? $"{mappedVeh} / {totalWebfleet}" : "0 / 0";
            double pojWidth = totalWebfleet > 0 ? Math.Min(238, (double)mappedVeh / totalWebfleet * 238) : 0;
            ProgressPojazdy.Width = pojWidth;
            ProgressPojazdy.Background = ColorForPct(mappedVeh, totalWebfleet);
            TxtPojazdyProgress.Foreground = ProgressPojazdy.Background;

            // Kierowcy progress — pokazujemy zmapowanych vs unikalni z GPS
            TxtKierowcyProgress.Text = uniqueDrivers > 0 ? $"{mappedDrv} / {uniqueDrivers}" : $"{mappedDrv}";
            double drvWidth = uniqueDrivers > 0 ? Math.Min(238, (double)mappedDrv / uniqueDrivers * 238) : 0;
            ProgressKierowcy.Width = drvWidth;
            ProgressKierowcy.Background = ColorForPct(mappedDrv, uniqueDrivers);
            TxtKierowcyProgress.Foreground = ProgressKierowcy.Background;
        }

        private static SolidColorBrush ColorForPct(int done, int total)
        {
            if (total == 0) return new SolidColorBrush(Color.FromRgb(176, 190, 197));   // #B0BEC5 — szary
            double pct = (double)done / total;
            if (pct >= 1.0)  return new SolidColorBrush(Color.FromRgb(46, 125, 50));   // #2E7D32 — zielony (full)
            if (pct >= 0.7)  return new SolidColorBrush(Color.FromRgb(67, 160, 71));   // #43A047 — zielony
            if (pct >= 0.3)  return new SolidColorBrush(Color.FromRgb(245, 124, 0));   // #F57C00 — pomarańczowy
            return new SolidColorBrush(Color.FromRgb(229, 81, 0));                       // #E55100 — czerwono-pomarańczowy
        }

        private async Task<List<MapaFlotyView.VehiclePosition>> FetchVehiclesAsync()
        {
            // Czytaj URL na zywo z config — moze byl zaktualizowany przez TxtBaseUrl/BtnSaveUrl
            string url = $"{Kalendarz1.Webfleet.WebfleetConfig.BaseUrl}?account={Uri.EscapeDataString(WfAccount)}" +
                         $"&apikey={Uri.EscapeDataString(WfApiKey)}&lang=pl&outputformat=json" +
                         $"&action=showObjectReportExtern";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", BasicAuth);
            using var res = await Http.SendAsync(req);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync();

            var items = JsonConvert.DeserializeObject<List<WfObj>>(json) ?? new();
            return items.Select(o => new MapaFlotyView.VehiclePosition
            {
                ObjectNo = o.objectno ?? "",
                ObjectName = o.objectname ?? o.objectno ?? "?",
                Driver = !string.IsNullOrEmpty(o.drivername) ? o.drivername
                       : !string.IsNullOrEmpty(o.driver) ? o.driver : "—",
                WebfleetDriverId = o.driveruid ?? o.driver ?? ""
            }).ToList();
        }

        private async Task<(int mappedVeh, int mappedDrv)> FetchMappedCountsAsync()
        {
            int mv = 0, md = 0;
            try
            {
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        (SELECT COUNT(*) FROM WebfleetVehicleMapping WHERE PojazdID IS NOT NULL AND PojazdID > 0) AS MappedVeh,
                        (SELECT COUNT(*) FROM WebfleetDriverMapping WHERE KierowcaID IS NOT NULL AND KierowcaID > 0) AS MappedDrv";
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    mv = r["MappedVeh"] == DBNull.Value ? 0 : Convert.ToInt32(r["MappedVeh"]);
                    md = r["MappedDrv"] == DBNull.Value ? 0 : Convert.ToInt32(r["MappedDrv"]);
                }
            }
            catch { /* tabele moga jeszcze nie istniec — pokaze 0 */ }
            return (mv, md);
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllAsync();
        }

        private async void BtnPojazdy_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new MapowaniePojazdowWindow(_vehicles) { Owner = this };
            dlg.ShowDialog();
            await LoadAllAsync();  // refresh stats po zamknieciu
        }

        private async void BtnKierowcy_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new MapowanieKierowcowWindow(_vehicles) { Owner = this };
            dlg.ShowDialog();
            await LoadAllAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private class WfObj
        {
            public string? objectno { get; set; }
            public string? objectname { get; set; }
            public string? drivername { get; set; }
            public string? driveruid { get; set; }
            public string? driver { get; set; }
        }
    }
}
