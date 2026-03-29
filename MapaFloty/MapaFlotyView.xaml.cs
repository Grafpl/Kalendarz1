using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Kalendarz1.MapaFloty
{
    public partial class MapaFlotyView : UserControl
    {
        // ══════════════════════════════════════════════════════════════════
        // Konfiguracja
        // ══════════════════════════════════════════════════════════════════
        private static readonly string WebfleetAccount = "942879";
        private static readonly string WebfleetUsername = "Administrator";
        private static readonly string WebfleetPassword = "kaazZVY5";
        private static readonly string WebfleetApiKey = "7a538868-96cf-4149-a9db-6e090de7276c";
        private static readonly string WebfleetBaseUrl = "https://csv.webfleet.com/extern";
        // Baza — ubojnia Koziołki 40
        private const double UbLat = 51.86857, UbLon = 19.79476;

        private static readonly string _basicAuth = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{WebfleetUsername}:{WebfleetPassword}"));
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly JsonSerializerSettings _js = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Error = (_, args) => { args.ErrorContext.Handled = true; }
        };
        private static readonly string _connTransport =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // ══════════════════════════════════════════════════════════════════
        // Stan
        // ══════════════════════════════════════════════════════════════════
        private DispatcherTimer? _refreshTimer, _countdownTimer;
        private List<VehiclePosition> _vehicles = new();
        private List<VehiclePosition> _displayVehicles = new();
        private Dictionary<string, string> _mappings = new();
        private List<KursInfo> _todayKursy = new();
        private bool _mapReady;
        private string _mapFolder = "";
        private int _countdown = 30;
        private string? _followVehicle;
        private readonly StringBuilder _debugLog = new();

        public MapaFlotyView()
        {
            InitializeComponent();
            Loaded += async (_, _) => await InitializeWebView();
            Unloaded += (_, _) => { _refreshTimer?.Stop(); _countdownTimer?.Stop(); CleanupTempFiles(); };
            KeyDown += OnKeyDown;
            Focusable = true;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5) { _ = RefreshVehiclePositions(); e.Handled = true; }
            else if (e.Key == Key.Escape && _mapReady)
            { _followVehicle = null; _ = MapWebView.CoreWebView2.ExecuteScriptAsync("stopFollow()"); e.Handled = true; }
        }

        private void Log(string m)
        {
            var l = $"[{DateTime.Now:HH:mm:ss.fff}] {m}";
            _debugLog.AppendLine(l);
            Dispatcher.Invoke(() => { DebugLog.AppendText(l + "\n"); DebugScroll.ScrollToEnd(); });
        }

        // ══════════════════════════════════════════════════════════════════
        // WebView2 Init
        // ══════════════════════════════════════════════════════════════════

        private async Task InitializeWebView()
        {
            try
            {
                StatusText.Text = "Inicjalizacja mapy...";
                await MapWebView.EnsureCoreWebView2Async();

                _mapFolder = Path.Combine(Path.GetTempPath(), "MapaFloty_" + Guid.NewGuid().ToString("N")[..8]);
                Directory.CreateDirectory(_mapFolder);
                File.WriteAllText(Path.Combine(_mapFolder, "map.html"), GenerateMapHtml(), Encoding.UTF8);

                var cw = MapWebView.CoreWebView2;
                var et = cw.GetType().Assembly.GetType("Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind")!;
                cw.GetType().GetMethod("SetVirtualHostNameToFolderMapping")!
                    .Invoke(cw, new object[] { "mapafloty.local", _mapFolder, Enum.Parse(et, "Allow") });

                cw.WebMessageReceived += (_, a) =>
                {
                    try
                    {
                        var raw = a.TryGetWebMessageAsString();
                        var msg = JsonConvert.DeserializeObject<MapMessage>(raw);
                        if (msg == null) return;
                        switch (msg.Action)
                        {
                            case "showTrack": _ = HandleShowTrack(msg.Data ?? "", null); break;
                            case "showTrackDate": _ = HandleShowTrack(msg.Data ?? "", msg.Date); break;
                            case "replay": _ = HandleReplay(msg.Data ?? ""); break;
                            case "follow": _followVehicle = msg.Data; StatusText.Text = $"Śledzenie: {msg.Data}"; break;
                            case "unfollow": _followVehicle = null; StatusText.Text = "Śledzenie wyłączone"; break;
                            case "log": Log($"JS: {msg.Data}"); break;
                        }
                    }
                    catch (Exception ex) { Log($"MSG ERR: {ex.Message}"); }
                };

                MapWebView.NavigationCompleted += (_, a) =>
                {
                    _mapReady = true;
                    if (!a.IsSuccess) { StatusText.Text = "Błąd mapy"; return; }
                    _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
                    _refreshTimer.Tick += async (_, _) => { _countdown = 30; await RefreshVehiclePositions(); };
                    _refreshTimer.Start();
                    _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    _countdownTimer.Tick += (_, _) => { if (_countdown > 0) _countdown--; CountdownText.Text = $"{_countdown}s"; };
                    _countdownTimer.Start();
                    _ = LoadMappingsAndRefresh();
                };

                cw.Navigate("https://mapafloty.local/map.html");
            }
            catch (Exception ex) { Log($"INIT ERR: {ex}"); StatusText.Text = $"Błąd: {ex.Message}"; }
        }

        private void CleanupTempFiles()
        { try { if (Directory.Exists(_mapFolder)) Directory.Delete(_mapFolder, true); } catch { } }

        // ══════════════════════════════════════════════════════════════════
        // Mapowania + Kursy
        // ══════════════════════════════════════════════════════════════════

        private async Task LoadMappingsAndRefresh()
        {
            await LoadMappings();
            await LoadTodayKursy();
            await RefreshVehiclePositions();
        }

        private async Task LoadMappings()
        {
            try
            {
                var map = new Dictionary<string, string>();
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync();
                using var chk = conn.CreateCommand();
                chk.CommandText = "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('WebfleetVehicleMapping') AND name='PojazdID'";
                if ((int)(await chk.ExecuteScalarAsync() ?? 0) == 0) { _mappings = map; return; }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT m.WebfleetObjectNo, m.PojazdID, p.Rejestracja, p.Marka, p.Model
                    FROM WebfleetVehicleMapping m INNER JOIN Pojazd p ON p.PojazdID=m.PojazdID WHERE m.PojazdID IS NOT NULL";
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var wf = r["WebfleetObjectNo"]?.ToString() ?? "";
                    var reg = r["Rejestracja"]?.ToString() ?? "";
                    var desc = $"{r["Marka"]} {r["Model"]}".Trim();
                    var display = !string.IsNullOrEmpty(desc) ? $"{reg} — {desc}" : reg;
                    map[wf] = $"{r["PojazdID"]}|{display}";
                }
                _mappings = map;
                Log($"Mapowania: {map.Count}");
            }
            catch { _mappings = new(); }
        }

        private async Task LoadTodayKursy()
        {
            try
            {
                var kursy = new List<KursInfo>();
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT k.KursID, k.Trasa, k.Status, k.GodzWyjazdu, k.GodzPowrotu,
                    k.PojazdID, CONCAT(ki.Imie,' ',ki.Nazwisko) AS KierowcaNazwa, p.Rejestracja
                    FROM Kurs k LEFT JOIN Kierowca ki ON k.KierowcaID=ki.KierowcaID
                    LEFT JOIN Pojazd p ON k.PojazdID=p.PojazdID
                    WHERE k.DataKursu=CAST(GETDATE() AS DATE) ORDER BY k.GodzWyjazdu";
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    kursy.Add(new KursInfo
                    {
                        KursID = r.GetInt64(r.GetOrdinal("KursID")),
                        Trasa = r["Trasa"]?.ToString() ?? "",
                        Status = r["Status"]?.ToString() ?? "",
                        GodzWyjazdu = r["GodzWyjazdu"] == DBNull.Value ? null : ((TimeSpan)r["GodzWyjazdu"]).ToString(@"hh\:mm"),
                        GodzPowrotu = r["GodzPowrotu"] == DBNull.Value ? null : ((TimeSpan)r["GodzPowrotu"]).ToString(@"hh\:mm"),
                        PojazdID = r["PojazdID"] == DBNull.Value ? null : Convert.ToInt32(r["PojazdID"]),
                        KierowcaNazwa = r["KierowcaNazwa"]?.ToString() ?? "",
                        Rejestracja = r["Rejestracja"]?.ToString() ?? ""
                    });
                }
                _todayKursy = kursy;
                Log($"Kursy dziś: {kursy.Count}");
            }
            catch (Exception ex) { Log($"Kursy ERR: {ex.Message}"); _todayKursy = new(); }
        }

        // ══════════════════════════════════════════════════════════════════
        // Odświeżanie pozycji
        // ══════════════════════════════════════════════════════════════════

        private async Task RefreshVehiclePositions()
        {
            if (!_mapReady) return;
            try
            {
                StatusText.Text = "Pobieranie pozycji...";
                _countdown = 30;
                var allVehicles = await FetchVehiclePositions();
                var vehicles = _mappings.Count > 0
                    ? allVehicles.Where(v => v.CarTrailerID != null).ToList()
                    : allVehicles;
                _vehicles = allVehicles;
                _displayVehicles = vehicles;

                foreach (var v in vehicles)
                {
                    v.DistToUbojnia = Haversine(v.Latitude, v.Longitude, UbLat, UbLon);
                    v.InGeofence = false;
                    // ETA do ubojni (min) — prosta kalkulacja
                    v.EtaMinutes = v.Speed > 5 ? (int)Math.Ceiling(v.DistToUbojnia / v.Speed * 60) : -1;
                    // Kurs powiązany z pojazdem
                    if (v.MappedPojazdID > 0)
                    {
                        var kurs = _todayKursy.FirstOrDefault(k => k.PojazdID == v.MappedPojazdID);
                        if (kurs != null)
                        {
                            v.KursTrasa = kurs.Trasa;
                            v.KursStatus = kurs.Status;
                            v.KursGodzWyjazdu = kurs.GodzWyjazdu;
                            v.KursGodzPowrotu = kurs.GodzPowrotu;
                            v.KursKierowca = kurs.KierowcaNazwa;
                        }
                    }
                }

                UpdateSidePanel(vehicles);
                var json = JsonConvert.SerializeObject(vehicles);
                await MapWebView.CoreWebView2.ExecuteScriptAsync($"updateVehicles({json})");

                if (_followVehicle != null)
                {
                    var fv = vehicles.FirstOrDefault(v => v.ObjectNo == _followVehicle);
                    if (fv != null) await MapWebView.CoreWebView2.ExecuteScriptAsync(
                        $"map.panTo([{F(fv.Latitude)},{F(fv.Longitude)}])");
                }

                var moving = vehicles.Count(v => v.IsMoving);
                var avgSpd = vehicles.Where(v => v.IsMoving).Select(v => v.Speed).DefaultIfEmpty(0).Average();
                AvgSpeedText.Text = ((int)avgSpd).ToString();
                InGeofenceText.Text = vehicles.Count(v => v.InGeofence).ToString();
                MovingCountText.Text = moving.ToString();
                StoppedCountText.Text = (vehicles.Count - moving).ToString();
                VehicleCountText.Text = vehicles.Count.ToString();
                var unmapped = _mappings.Count > 0 ? allVehicles.Count - vehicles.Count : 0;
                StatusText.Text = $"{vehicles.Count} pojazdów | {moving} jedzie | {DateTime.Now:HH:mm:ss}" +
                    (unmapped > 0 ? $" | {unmapped} ukrytych" : "");
                LastUpdateText.Text = DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception ex) { Log($"ERR: {ex.Message}"); StatusText.Text = $"Błąd: {ex.Message}"; }
        }

        private async Task<List<VehiclePosition>> FetchVehiclePositions()
        {
            var url = BuildUrl("showObjectReportExtern");
            var resp = await WebfleetGet(url);
            CheckApiError(resp, "objects");
            var items = JsonConvert.DeserializeObject<List<WfObject>>(resp, _js);
            return items?.Select(o =>
            {
                var no = o.objectno ?? "";
                _mappings.TryGetValue(no, out var mapVal);
                string? ctId = null, internalName = null;
                int mappedPid = 0;
                if (mapVal != null)
                {
                    var parts = mapVal.Split('|', 2);
                    ctId = parts[0];
                    int.TryParse(parts[0], out mappedPid);
                    internalName = parts.Length > 1 ? parts[1] : null;
                }
                return new VehiclePosition
                {
                    ObjectNo = no, ObjectName = o.objectname ?? o.objectno ?? "?",
                    Latitude = o.latitude_mdeg / 1e6, Longitude = o.longitude_mdeg / 1e6,
                    Speed = o.speed, Course = o.course, Address = o.postext ?? "",
                    Driver = !string.IsNullOrEmpty(o.drivername) ? o.drivername : !string.IsNullOrEmpty(o.driver) ? o.driver : "—",
                    LastUpdate = o.pos_time ?? o.msgtime ?? "", IsMoving = o.speed > 0, Ignition = o.ignition > 0,
                    CarTrailerID = ctId, InternalName = internalName, MappedPojazdID = mappedPid,
                    WebfleetDriverId = o.driveruid ?? o.driver ?? ""
                };
            }).ToList() ?? new();
        }

        // ══════════════════════════════════════════════════════════════════
        // Historia tras + Replay
        // ══════════════════════════════════════════════════════════════════

        private async Task HandleShowTrack(string objectNo, string? dateStr)
        {
            try
            {
                var dateLabel = dateStr ?? "dziś";
                StatusText.Text = $"Trasa {objectNo} ({dateLabel})...";

                // Buduj URL z datą lub range_pattern=d0 (dzisiaj)
                string url;
                if (!string.IsNullOrEmpty(dateStr))
                {
                    // Format ISO: "2026-03-28" → useISO8601 + rangefrom/rangeto
                    url = BuildUrl("showTracks")
                        + $"&objectno={Uri.EscapeDataString(objectNo)}&useISO8601=true"
                        + $"&rangefrom_string={dateStr}T00:00:00&rangeto_string={dateStr}T23:59:59";
                }
                else
                {
                    url = BuildUrl("showTracks") + $"&objectno={Uri.EscapeDataString(objectNo)}&range_pattern=d0";
                }

                var resp = await WebfleetGet(url);
                Log($"Track {objectNo}: {resp.Length} chars");
                CheckApiError(resp, "tracks");
                var items = JsonConvert.DeserializeObject<List<WfTrack>>(resp, _js);
                var pts = items?.Select(t => new TrackPoint
                {
                    Lat = (t.latitude_mdeg != 0 ? t.latitude_mdeg : t.latitude) / 1e6,
                    Lng = (t.longitude_mdeg != 0 ? t.longitude_mdeg : t.longitude) / 1e6,
                    Speed = t.speed, Time = t.pos_time ?? ""
                }).ToList() ?? new();

                double totalKm = 0; int maxSpd = 0;
                for (int i = 1; i < pts.Count; i++)
                {
                    totalKm += Haversine(pts[i - 1].Lat, pts[i - 1].Lng, pts[i].Lat, pts[i].Lng);
                    if (pts[i].Speed > maxSpd) maxSpd = pts[i].Speed;
                }
                var avgSpd = pts.Where(p => p.Speed > 0).Select(p => p.Speed).DefaultIfEmpty(0).Average();

                // Czas jazdy vs postój
                int drivingPts = pts.Count(p => p.Speed > 0);
                int stoppedPts = pts.Count(p => p.Speed <= 0);

                var stats = new
                {
                    km = Math.Round(totalKm, 1), maxSpd, avgSpd = (int)avgSpd, pts = pts.Count,
                    from = pts.FirstOrDefault()?.Time ?? "", to = pts.LastOrDefault()?.Time ?? "",
                    drivingPct = pts.Count > 0 ? (int)(drivingPts * 100.0 / pts.Count) : 0,
                    date = dateStr ?? DateTime.Now.ToString("yyyy-MM-dd")
                };

                var json = JsonConvert.SerializeObject(pts);
                var statsJson = JsonConvert.SerializeObject(stats);
                await MapWebView.CoreWebView2.ExecuteScriptAsync($"drawTrack({json},'{Esc(objectNo)}',{statsJson})");
                StatusText.Text = $"Trasa: {pts.Count} pkt, {stats.km} km, max {maxSpd} km/h";
            }
            catch (Exception ex) { Log($"Track ERR: {ex.Message}"); StatusText.Text = $"Błąd trasy: {ex.Message}"; }
        }

        private async Task HandleReplay(string objectNo)
        {
            // Pobierz dane trasy i wyślij do JS animacji replay
            try
            {
                StatusText.Text = $"Replay {objectNo}...";
                var url = BuildUrl("showTracks") + $"&objectno={Uri.EscapeDataString(objectNo)}&range_pattern=d0";
                var resp = await WebfleetGet(url);
                CheckApiError(resp, "replay");
                var items = JsonConvert.DeserializeObject<List<WfTrack>>(resp, _js);
                var pts = items?.Select(t => new TrackPoint
                {
                    Lat = (t.latitude_mdeg != 0 ? t.latitude_mdeg : t.latitude) / 1e6,
                    Lng = (t.longitude_mdeg != 0 ? t.longitude_mdeg : t.longitude) / 1e6,
                    Speed = t.speed, Time = t.pos_time ?? ""
                }).ToList() ?? new();

                var json = JsonConvert.SerializeObject(pts);
                await MapWebView.CoreWebView2.ExecuteScriptAsync($"startReplay({json},'{Esc(objectNo)}')");
                StatusText.Text = $"Replay: {pts.Count} punktów";
            }
            catch (Exception ex) { Log($"Replay ERR: {ex.Message}"); StatusText.Text = $"Błąd replay: {ex.Message}"; }
        }

        // ══════════════════════════════════════════════════════════════════
        // Webfleet HTTP
        // ══════════════════════════════════════════════════════════════════

        private string BuildUrl(string action) =>
            $"{WebfleetBaseUrl}?account={Uri.EscapeDataString(WebfleetAccount)}" +
            $"&apikey={Uri.EscapeDataString(WebfleetApiKey)}&lang=pl&outputformat=json&action={action}";

        private async Task<string> WebfleetGet(string url)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _basicAuth);
            using var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadAsStringAsync();
        }

        private void CheckApiError(string r, string a)
        {
            if (string.IsNullOrWhiteSpace(r) || !r.TrimStart().StartsWith("{")) return;
            try
            {
                var o = JObject.Parse(r); var c = o["errorCode"]?.ToString();
                if (!string.IsNullOrEmpty(c)) throw new Exception($"Webfleet [{a}] {c}: {o["errorMsg"]}");
            }
            catch (JsonException) { }
        }

        private static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            var dLat = (lat2 - lat1) * Math.PI / 180; var dLon = (lon2 - lon1) * Math.PI / 180;
            var a2 = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a2), Math.Sqrt(1 - a2));
        }

        private static string F(double d) => d.ToString("F6", CultureInfo.InvariantCulture);

        // ══════════════════════════════════════════════════════════════════
        // Panel boczny
        // ══════════════════════════════════════════════════════════════════

        private void UpdateSidePanel(List<VehiclePosition> vehicles)
        {
            var filter = SearchBox.Text.Trim().ToLower();
            VehicleListPanel.Children.Clear();

            var list = string.IsNullOrEmpty(filter) ? vehicles :
                vehicles.Where(v => v.ObjectName.ToLower().Contains(filter) || v.Driver.ToLower().Contains(filter) ||
                    v.Address.ToLower().Contains(filter) || (v.InternalName ?? "").ToLower().Contains(filter) ||
                    (v.KursTrasa ?? "").ToLower().Contains(filter)).ToList();

            list = (SortCombo.SelectedIndex switch
            {
                1 => list.OrderByDescending(v => v.Speed).ThenBy(v => v.ObjectName),
                2 => list.OrderBy(v => v.ObjectName),
                3 => list.OrderByDescending(v => v.LastUpdate),
                _ => list.OrderByDescending(v => v.IsMoving).ThenByDescending(v => v.Speed).ThenBy(v => v.ObjectName)
            }).ToList();

            foreach (var v in list) VehicleListPanel.Children.Add(CreateVehicleCard(v));
        }

        private Border CreateVehicleCard(VehiclePosition v)
        {
            var green = Color.FromRgb(46, 125, 50);
            var orange = Color.FromRgb(230, 81, 0);
            var gray = Color.FromRgb(120, 144, 156);
            var red = Color.FromRgb(198, 40, 40);
            var sc = v.IsMoving ? green : (v.Ignition ? orange : gray);
            var sb = new SolidColorBrush(sc);

            var card = new Border
            {
                Background = Brushes.White, CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 5), Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                BorderBrush = v.InGeofence ? new SolidColorBrush(Color.FromArgb(120, red.R, red.G, red.B))
                    : new SolidColorBrush(Color.FromRgb(232, 234, 240)),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                    { ShadowDepth = 1, BlurRadius = 4, Opacity = 0.06, Direction = 270 }
            };
            card.MouseEnter += (_, _) => card.Background = new SolidColorBrush(Color.FromRgb(248, 249, 253));
            card.MouseLeave += (_, _) => card.Background = Brushes.White;
            card.MouseLeftButtonUp += async (_, _) =>
            { if (_mapReady) await MapWebView.CoreWebView2.ExecuteScriptAsync($"focusVehicle('{Esc(v.ObjectNo)}')"); };

            // Główny grid z kolorowym paskiem z lewej
            var outerGrid = new Grid();
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var accentBar = new Border
            {
                Background = sb, CornerRadius = new CornerRadius(8, 0, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            Grid.SetColumn(accentBar, 0);
            outerGrid.Children.Add(accentBar);

            var stack = new StackPanel { Margin = new Thickness(10, 7, 10, 7) };
            Grid.SetColumn(stack, 1);
            outerGrid.Children.Add(stack);

            // Row 1: name + speed
            var r1 = new DockPanel { Margin = new Thickness(0, 0, 0, 2) };
            if (v.IsMoving)
            {
                var spdBg = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(35, sc.R, sc.G, sc.B)),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 1, 5, 1),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(spdBg, Dock.Right);
                spdBg.Child = new TextBlock { Text = $"{v.Speed}", Foreground = sb, FontSize = 12, FontWeight = FontWeights.ExtraBold };
                r1.Children.Add(spdBg);
            }
            r1.Children.Add(new TextBlock
            {
                Text = v.ObjectName, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(38, 50, 56)),
                FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis
            });
            stack.Children.Add(r1);

            // Row 2: driver + status pill
            var r2 = new DockPanel { Margin = new Thickness(0, 1, 0, 0) };
            var statusTxt = v.IsMoving ? "W trasie" : (v.Ignition ? "Postój (zapłon)" : "Wyłączony");
            var pill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, sc.R, sc.G, sc.B)),
                CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 1, 5, 1),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(pill, Dock.Right);
            pill.Child = new TextBlock { Text = statusTxt, Foreground = sb, FontSize = 9, FontWeight = FontWeights.SemiBold };
            r2.Children.Add(pill);
            r2.Children.Add(new TextBlock
            {
                Text = v.Driver, Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
                FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis
            });
            stack.Children.Add(r2);

            // Row 3: ETA + distance
            if (v.IsMoving || v.DistToUbojnia < 50)
            {
                var r3 = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
                var distTxt = new TextBlock
                {
                    Text = $"{v.DistToUbojnia:F1} km",
                    Foreground = new SolidColorBrush(Color.FromRgb(144, 164, 174)),
                    FontSize = 9, HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(distTxt, Dock.Right);
                r3.Children.Add(distTxt);
                var etaStr = v.EtaMinutes > 0 ? $"Do ubojni: ok. {v.EtaMinutes} min" : $"Do ubojni: {v.DistToUbojnia:F0} km";
                r3.Children.Add(new TextBlock
                {
                    Text = etaStr,
                    Foreground = new SolidColorBrush(v.EtaMinutes > 0 ? Color.FromRgb(21, 101, 192) : Color.FromRgb(144, 164, 174)),
                    FontSize = 9, FontWeight = v.EtaMinutes > 0 ? FontWeights.SemiBold : FontWeights.Normal
                });
                stack.Children.Add(r3);
            }

            // Row 4: address
            if (!string.IsNullOrWhiteSpace(v.Address))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = v.Address, Foreground = new SolidColorBrush(Color.FromRgb(144, 164, 174)),
                    FontSize = 9, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 0)
                });
            }

            // Row 5: internal mapping
            if (!string.IsNullOrEmpty(v.InternalName))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"Transport: {v.InternalName}",
                    Foreground = new SolidColorBrush(Color.FromRgb(57, 73, 171)),
                    FontSize = 9, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 0)
                });
            }

            // Row 6: Kurs info
            if (!string.IsNullOrEmpty(v.KursTrasa))
            {
                var kursLine = $"Kurs: {v.KursTrasa}";
                if (!string.IsNullOrEmpty(v.KursGodzWyjazdu)) kursLine += $" ({v.KursGodzWyjazdu})";
                var kursColor = v.KursStatus == "Planowany" ? Color.FromRgb(255, 152, 0)
                    : v.KursStatus == "W realizacji" ? Color.FromRgb(46, 125, 50) : Color.FromRgb(120, 144, 156);
                stack.Children.Add(new TextBlock
                {
                    Text = kursLine, Foreground = new SolidColorBrush(kursColor),
                    FontSize = 9, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 0)
                });
            }

            // Row 7: geofence
            if (v.InGeofence)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "W STREFIE ŁYSZKOWICE", Foreground = new SolidColorBrush(red),
                    FontSize = 8.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 2, 0, 0)
                });
            }

            card.Child = outerGrid;
            return card;
        }

        // ══════════════════════════════════════════════════════════════════
        // UI Events
        // ══════════════════════════════════════════════════════════════════

        private void BtnMapping_Click(object s, RoutedEventArgs e)
        {
            if (_vehicles.Count == 0) { StatusText.Text = "Najpierw poczekaj na załadowanie danych z Webfleet"; return; }
            var choice = MessageBox.Show(
                "Tak — mapuj pojazdy (Webfleet GPS → pojazd spedytora)\nNie — mapuj kierowców (Webfleet GPS → kierowca)\n\nAnuluj — zamknij",
                "Mapowanie Webfleet", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Yes)
            { var d = new MapowaniePojazdowWindow(_vehicles) { Owner = Window.GetWindow(this) }; if (d.ShowDialog() == true) _ = LoadMappingsAndRefresh(); }
            else if (choice == MessageBoxResult.No)
            { var d = new MapowanieKierowcowWindow(_vehicles) { Owner = Window.GetWindow(this) }; if (d.ShowDialog() == true) _ = LoadMappingsAndRefresh(); }
        }

        private void BtnMonitor_Click(object s, RoutedEventArgs e)
        {
            var win = new MonitorKursowWindow();
            win.Show();
        }

        private void BtnSyncKursy_Click(object s, RoutedEventArgs e)
        {
            var win = new SyncKursowWindow { Owner = Window.GetWindow(this) };
            win.Show();
        }

        private void BtnPdfReport_Click(object s, RoutedEventArgs e)
        {
            try
            {
                if (_displayVehicles.Count == 0) { StatusText.Text = "Brak danych do raportu"; return; }
                StatusText.Text = "Generowanie PDF...";
                var pdf = RaportFlotyPDF.Generuj(_displayVehicles);
                var path = Path.Combine(Path.GetTempPath(), $"RaportFloty_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
                File.WriteAllBytes(path, pdf);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                StatusText.Text = "Raport PDF otwarty";
            }
            catch (Exception ex) { StatusText.Text = $"Błąd PDF: {ex.Message}"; Log($"PDF ERR: {ex.Message}"); }
        }

        private void BtnDriverPanel_Click(object s, RoutedEventArgs e)
        {
            if (_displayVehicles.Count == 0) { StatusText.Text = "Brak danych"; return; }
            var win = new PanelKierowcyWindow(_displayVehicles) { Owner = Window.GetWindow(this) };
            win.Show();
        }

        private void BtnRefresh_Click(object s, RoutedEventArgs e) => _ = RefreshVehiclePositions();
        private void BtnCenterAll_Click(object s, RoutedEventArgs e)
        { if (_mapReady) _ = MapWebView.CoreWebView2.ExecuteScriptAsync("centerAll()"); }
        private void BtnToggleDebug_Click(object s, RoutedEventArgs e)
        { DebugPanel.Visibility = DebugPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; }
        private void BtnCopyDebug_Click(object s, RoutedEventArgs e)
        { try { Clipboard.SetText(_debugLog.ToString()); StatusText.Text = "Skopiowano"; } catch { } }
        private void BtnClearDebug_Click(object s, RoutedEventArgs e) { _debugLog.Clear(); DebugLog.Clear(); }
        private void SearchBox_TextChanged(object s, TextChangedEventArgs e) { if (_displayVehicles.Count > 0) UpdateSidePanel(_displayVehicles); }
        private void SortCombo_Changed(object s, SelectionChangedEventArgs e) { if (_displayVehicles.Count > 0) UpdateSidePanel(_displayVehicles); }

        private static string Esc(string? s) => s?.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "").Replace("\r", "") ?? "";

        // ══════════════════════════════════════════════════════════════════
        // HTML — Leaflet map z historią, replay, ETA, kursami
        // ══════════════════════════════════════════════════════════════════

        private string GenerateMapHtml()
        {
            return @"<!DOCTYPE html><html><head>
<meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"" integrity=""sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY="" crossorigin=""""/>
<script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"" integrity=""sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo="" crossorigin=""""></script>
<style>
*{margin:0;padding:0;box-sizing:border-box}
html,body,#map{width:100%;height:100%;background:#f0f0f0;font-family:'Segoe UI',system-ui,sans-serif}
.vi{position:relative;filter:drop-shadow(0 3px 6px rgba(0,0,0,0.5))}
.vi-body{width:32px;height:32px;border-radius:50%;display:flex;align-items:center;justify-content:center;border:2px solid #fff;transition:all .3s}
.vi-body svg{width:18px;height:18px;fill:#fff}
.vi-spd{position:absolute;top:-4px;right:-4px;background:#111;color:#fff;font-size:8px;font-weight:800;padding:0px 3px;border-radius:3px;border:1px solid #555;line-height:1.2}
.vi-name{position:absolute;bottom:-14px;left:50%;transform:translateX(-50%);background:rgba(0,0,0,.65);color:#fff;font-size:7px;padding:1px 4px;border-radius:3px;white-space:nowrap;font-weight:600;pointer-events:none}
@keyframes pulse{0%,100%{transform:scale(1);box-shadow:0 0 0 0 rgba(76,175,80,.4)}50%{transform:scale(1.05);box-shadow:0 0 0 12px rgba(76,175,80,0)}}
.vi-moving .vi-body{animation:pulse 2s ease-in-out infinite}
@keyframes glow{0%,100%{box-shadow:0 0 4px 2px rgba(229,57,53,.3)}50%{box-shadow:0 0 12px 4px rgba(229,57,53,.6)}}
.vi-geo .vi-body{animation:glow 1.5s ease-in-out infinite!important;border-color:#ef5350!important}
.vp{min-width:280px;padding:0}
.vp-head{background:linear-gradient(135deg,#1a237e,#283593);color:#fff;padding:14px 16px;border-radius:8px 8px 0 0;margin:-14px -20px 12px}
.vp-head h3{margin:0;font-size:15px;font-weight:700}.vp-head .sub{opacity:.7;font-size:11px;margin-top:3px}
.vp-badge{display:inline-block;padding:3px 10px;border-radius:12px;font-size:11px;font-weight:700;margin-bottom:8px}
.vp-badge.m{background:#e8f5e9;color:#2e7d32}.vp-badge.s{background:#fff3e0;color:#e65100}.vp-badge.o{background:#f5f5f5;color:#757575}
.vp-badge.g{background:#ffebee;color:#c62828;margin-left:4px}
.vp-r{display:flex;justify-content:space-between;padding:5px 0;border-bottom:1px solid #f0f0f5;font-size:12px}
.vp-r:last-child{border:none}.vp-r .k{color:#78909c}.vp-r .v{font-weight:600;color:#263238;max-width:170px;text-align:right}
.vp-btn{display:block;width:100%;margin-top:5px;padding:8px;text-align:center;border:none;border-radius:6px;cursor:pointer;font-size:11.5px;font-weight:600;transition:.15s}
.vp-btn.tr{background:#1565c0;color:#fff}.vp-btn.tr:hover{background:#0d47a1}
.vp-btn.rp{background:#6a1b9a;color:#fff}.vp-btn.rp:hover{background:#4a148c}
.vp-btn.fw{background:#00695c;color:#fff}.vp-btn.fw:hover{background:#004d40}
.vp-btn.cl{background:#eceff1;color:#546e7a;margin-top:3px}.vp-btn.cl:hover{background:#cfd8dc}
.vp-date{width:100%;padding:6px;margin-top:6px;border:1px solid #cfd8dc;border-radius:4px;font-size:11px}
.vp-kurs{background:#e8eaf6;padding:6px 10px;border-radius:6px;margin:6px 0;font-size:11px;color:#283593}
#trackStats{position:absolute;bottom:40px;left:50%;transform:translateX(-50%);z-index:1000;background:rgba(255,255,255,.95);backdrop-filter:blur(12px);color:#263238;padding:12px 20px;border-radius:12px;display:none;font-size:12px;box-shadow:0 4px 20px rgba(0,0,0,.15);border:1px solid #e0e0e0}
#trackStats .ts-row{display:flex;gap:16px;align-items:center}
#trackStats .ts-item{text-align:center}
#trackStats .ts-val{font-size:18px;font-weight:800;line-height:1.2}
#trackStats .ts-lbl{font-size:8px;opacity:.5;text-transform:uppercase;letter-spacing:.5px}
#trackStats .ts-close{position:absolute;top:4px;right:8px;cursor:pointer;opacity:.5;font-size:14px}
#speedLegend{position:absolute;bottom:40px;right:12px;z-index:900;background:rgba(255,255,255,.92);padding:8px 10px;border-radius:8px;display:none;font-size:10px;color:#546e7a;border:1px solid #e0e0e0}
#speedLegend .sl-row{display:flex;align-items:center;gap:6px;margin:2px 0}
#speedLegend .sl-dot{width:10px;height:10px;border-radius:50%}
/* Replay */
#replayBar{position:absolute;top:10px;left:50%;transform:translateX(-50%);z-index:1000;background:rgba(255,255,255,.95);padding:8px 16px;border-radius:10px;display:none;font-size:12px;box-shadow:0 4px 16px rgba(0,0,0,.15);border:1px solid #e0e0e0;align-items:center;gap:10px}
#replayBar button{border:none;padding:4px 10px;border-radius:4px;cursor:pointer;font-weight:700;font-size:12px}
#replayBar .rb-play{background:#1565c0;color:#fff}
#replayBar .rb-stop{background:#c62828;color:#fff}
#replayBar .rb-speed{background:#eceff1;color:#263238}
#replayBar .rb-info{color:#546e7a;font-size:11px;min-width:120px}
.geo-label{background:linear-gradient(135deg,#e53935,#b71c1c);color:#fff;padding:4px 12px;border-radius:14px;font-weight:700;font-size:11px;white-space:nowrap;border:none;box-shadow:0 2px 10px rgba(229,57,53,.5)}
.ub-label{background:linear-gradient(135deg,#37474f,#1a2328);color:#fff;padding:4px 10px;border-radius:14px;font-weight:700;font-size:11px;white-space:nowrap;border:none;box-shadow:0 2px 8px rgba(0,0,0,.3)}
.ub-icon{background:linear-gradient(135deg,#37474f,#263238);width:26px;height:26px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:13px;box-shadow:0 2px 6px rgba(0,0,0,.3);border:2px solid #fff}
.leaflet-control-layers{border-radius:10px!important;box-shadow:0 2px 14px rgba(0,0,0,.15)!important;border:none!important}
.leaflet-popup-content-wrapper{border-radius:10px!important;box-shadow:0 4px 20px rgba(0,0,0,.15)!important}
</style></head><body>
<div id=""map""></div>
<div id=""trackStats""><span class=""ts-close"" onclick=""clearTrack()"">&times;</span>
<div class=""ts-row"">
<div class=""ts-item""><div class=""ts-val"" id=""tsKm"">—</div><div class=""ts-lbl"">Dystans</div></div>
<div class=""ts-item""><div class=""ts-val"" id=""tsAvg"">—</div><div class=""ts-lbl"">Śr. km/h</div></div>
<div class=""ts-item""><div class=""ts-val"" id=""tsMax"">—</div><div class=""ts-lbl"">Max</div></div>
<div class=""ts-item""><div class=""ts-val"" id=""tsPts"">—</div><div class=""ts-lbl"">Punkty</div></div>
<div class=""ts-item""><div class=""ts-val"" id=""tsDrive"">—</div><div class=""ts-lbl"">% jazdy</div></div>
<div class=""ts-item""><div class=""ts-val"" id=""tsTime"">—</div><div class=""ts-lbl"">Okres</div></div>
</div></div>
<div id=""speedLegend""><div style=""font-weight:700;margin-bottom:4px"">Prędkość</div>
<div class=""sl-row""><div class=""sl-dot"" style=""background:#66bb6a""></div>&lt;30</div>
<div class=""sl-row""><div class=""sl-dot"" style=""background:#fdd835""></div>30–60</div>
<div class=""sl-row""><div class=""sl-dot"" style=""background:#ff9800""></div>60–90</div>
<div class=""sl-row""><div class=""sl-dot"" style=""background:#e53935""></div>&gt;90</div></div>
<div id=""replayBar"" style=""display:none"">
<button class=""rb-play"" onclick=""replayToggle()"" id=""rbPlayBtn"">&#9654;</button>
<button class=""rb-speed"" onclick=""replaySpeed()"" id=""rbSpeedBtn"">x10</button>
<span class=""rb-info"" id=""rbInfo"">0 / 0</span>
<button class=""rb-stop"" onclick=""replayStop()"">&#x2716;</button>
</div>
<script>
var map=L.map('map',{zoomControl:false}).setView([51.89,19.92],11);
L.control.zoom({position:'topright'}).addTo(map);
L.control.scale({imperial:false,position:'bottomleft'}).addTo(map);
var osm=L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',{attribution:'&copy; OSM',maxZoom:19});
var dark=L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',{attribution:'&copy; CARTO',maxZoom:20});
var sat=L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',{attribution:'&copy; Esri',maxZoom:19});
osm.addTo(map);
L.control.layers({'Mapa':osm,'Ciemna':dark,'Satelita':sat},null,{position:'topright'}).addTo(map);

var markers={},trackGroup=L.layerGroup().addTo(map),followId=null;
var replayData=null,replayIdx=0,replayTimer=null,replayPaused=false,replaySpeedVal=10,replayMarker=null,replayTrail=null;

var truckPath='M3,14h18v8H3v-8zm18,2h5l3,4v4h-8v-8zM6,24a2,2,0,1,0,0-4,2,2,0,0,0,0,4zm15,0a2,2,0,1,0,0-4,2,2,0,0,0,0,4z';
function mkIcon(v){
    var c=v.IsMoving?'#43a047':(v.Ignition?'#f57c00':'#607d8b');
    var cls='vi'+(v.IsMoving?' vi-moving':'')+(v.InGeofence?' vi-geo':'');
    var spd=v.Speed>0?'<div class=""vi-spd"">'+v.Speed+'</div>':'';
    var name='<div class=""vi-name"">'+esc(v.ObjectName.substring(0,14))+'</div>';
    var rot=v.IsMoving?(v.Course||0):0;
    return L.divIcon({className:'',html:'<div class=""'+cls+'""><div class=""vi-body"" style=""background:'+c+'""><svg viewBox=""0 0 30 30"" style=""transform:rotate('+rot+'deg)""><path d=""'+truckPath+'""/></svg></div>'+spd+name+'</div>',iconSize:[32,32],iconAnchor:[16,16],popupAnchor:[0,-18]});
}

function mkPopup(v){
    var bc=v.IsMoving?'m':(v.Ignition?'s':'o');
    var bt=v.IsMoving?'W trasie — '+v.Speed+' km/h':(v.Ignition?'Zapłon wł. — postój':'Silnik wyłączony');
    var geo=v.InGeofence?'<span class=""vp-badge g"">W STREFIE ŁYSZKOWICE</span>':'';
    var dirs=['N','NE','E','SE','S','SW','W','NW'];
    var dir=dirs[Math.round((v.Course||0)/45)%8];
    var dist=v.DistToUbojnia!=null?v.DistToUbojnia.toFixed(1)+' km':'—';
    var eta=v.EtaMinutes>0?'ok. '+v.EtaMinutes+' min':'—';
    var kursHtml='';
    if(v.KursTrasa){
        kursHtml='<div class=""vp-kurs"">';
        kursHtml+='<b>Dzisiejszy kurs:</b> '+esc(v.KursTrasa);
        if(v.KursGodzWyjazdu){kursHtml+='<br>Godz.: '+v.KursGodzWyjazdu;if(v.KursGodzPowrotu)kursHtml+=' — '+v.KursGodzPowrotu}
        if(v.KursStatus)kursHtml+='<br>Status: <b>'+esc(v.KursStatus)+'</b>';
        if(v.KursKierowca)kursHtml+='<br>Kierowca kursu: '+esc(v.KursKierowca);
        kursHtml+='</div>';
    }
    var today=new Date().toISOString().split('T')[0];
    return '<div class=""vp""><div class=""vp-head""><h3>'+esc(v.ObjectName)+'</h3><div class=""sub"">Kierowca: '+esc(v.Driver)+'</div></div>'
      +'<span class=""vp-badge '+bc+'"">'+bt+'</span>'+geo+kursHtml
      +'<div class=""vp-r""><span class=""k"">Kierunek</span><span class=""v"">'+dir+' ('+v.Course+'°)</span></div>'
      +'<div class=""vp-r""><span class=""k"">Pozycja</span><span class=""v"">'+esc(v.Address||'brak danych')+'</span></div>'
      +'<div class=""vp-r""><span class=""k"">Odległość do ubojni</span><span class=""v"">'+dist+'</span></div>'
      +'<div class=""vp-r""><span class=""k"">Szac. czas dojazdu</span><span class=""v"" style=""color:#1565c0;font-weight:700"">'+eta+'</span></div>'
      +'<div class=""vp-r""><span class=""k"">Ostatni sygnał GPS</span><span class=""v"">'+(v.LastUpdate||'—')+'</span></div>'
      +'<div style=""margin-top:8px;padding-top:8px;border-top:1px solid #eee"">'
      +'<div style=""display:flex;align-items:center;gap:6px;margin-bottom:6px"">'
      +'<span style=""font-size:11px;color:#546e7a"">Data trasy:</span>'
      +'<input type=""date"" class=""vp-date"" id=""trackDate_'+v.ObjectNo+'"" value=""'+today+'"" style=""flex:1""/></div>'
      +'<button class=""vp-btn tr"" onclick=""requestTrackDate(\''+escJs(v.ObjectNo)+'\')"">Pokaż trasę na mapie</button>'
      +'<button class=""vp-btn rp"" onclick=""requestReplay(\''+escJs(v.ObjectNo)+'\')"">Odtwórz przejazd (replay)</button>'
      +'<button class=""vp-btn fw"" onclick=""toggleFollow(\''+escJs(v.ObjectNo)+'\')"">'+
      (followId===v.ObjectNo?'Wyłącz śledzenie':'Śledź ten pojazd na żywo')+'</button>'
      +'<button class=""vp-btn cl"" onclick=""clearTrack()"">Wyczyść trasę z mapy</button></div></div>';
}

// ── Geofence + Ubojnia ──
// Baza — ubojnia Koziołki 40
L.marker([51.86857,19.79476],{icon:L.divIcon({className:'',html:'<div class=""ub-icon"">&#127981;</div>',iconSize:[26,26],iconAnchor:[13,13]})}).addTo(map).bindPopup('<b>Ubojnia Koziołki 40</b><br>Baza');
L.marker([51.86857,19.79476],{icon:L.divIcon({className:'ub-label',html:'BAZA — Ubojnia',iconSize:null,iconAnchor:[55,-22]}),interactive:false}).addTo(map);

// ── Vehicles ──
function updateVehicles(vs){
    var ids=vs.map(function(v){return v.ObjectNo});
    for(var id in markers){if(ids.indexOf(id)<0){map.removeLayer(markers[id]);delete markers[id]}}
    vs.forEach(function(v){
        var ic=mkIcon(v),pop=mkPopup(v);
        if(markers[v.ObjectNo]){
            var m=markers[v.ObjectNo],nll=L.latLng(v.Latitude,v.Longitude),oll=m.getLatLng();
            if(oll.distanceTo(nll)>5)animateMarker(m,oll,nll,800);
            m.setIcon(ic);m.setPopupContent(pop);
            m.setTooltipContent(v.ObjectName+(v.IsMoving?' — '+v.Speed+' km/h':' — postój'));
        }else{
            markers[v.ObjectNo]=L.marker([v.Latitude,v.Longitude],{icon:ic}).addTo(map)
                .bindPopup(pop,{maxWidth:340}).bindTooltip(v.ObjectName+(v.IsMoving?' — '+v.Speed+' km/h':' — postój'),{direction:'top',offset:[0,-28]});
        }
    });
}
function animateMarker(m,f,t,dur){
    var s=performance.now();
    (function step(n){var p=Math.min((n-s)/dur,1);p=p*p*(3-2*p);
    m.setLatLng([f.lat+(t.lat-f.lat)*p,f.lng+(t.lng-f.lng)*p]);
    if(p<1)requestAnimationFrame(step)})(s);
}
function focusVehicle(id){if(markers[id]){map.flyTo(markers[id].getLatLng(),15,{duration:.6});setTimeout(function(){markers[id].openPopup()},650)}}
function centerAll(){var p=[];for(var i in markers)p.push(markers[i].getLatLng());if(p.length)map.flyToBounds(L.latLngBounds(p).pad(.15),{duration:.5})}

// ── Follow ──
function toggleFollow(id){if(followId===id){followId=null;post({Action:'unfollow',Data:id})}else{followId=id;post({Action:'follow',Data:id})}}
function stopFollow(){followId=null;post({Action:'unfollow',Data:''})}

// ── Track z historią dat ──
function requestTrackDate(id){
    var el=document.getElementById('trackDate_'+id);
    var date=el?el.value:'';
    var today=new Date().toISOString().split('T')[0];
    if(date&&date!==today){post({Action:'showTrackDate',Data:id,Date:date})}
    else{post({Action:'showTrack',Data:id})}
}
function requestReplay(id){post({Action:'replay',Data:id})}

function drawTrack(pts,id,stats){
    clearTrack();if(!pts||pts.length<2)return;
    for(var i=0;i<pts.length-1;i++){
        L.polyline([[pts[i].Lat,pts[i].Lng],[pts[i+1].Lat,pts[i+1].Lng]],{color:spdColor(pts[i].Speed||0),weight:4,opacity:.85}).addTo(trackGroup);
    }
    L.circleMarker([pts[0].Lat,pts[0].Lng],{radius:7,color:'#1565c0',fillColor:'#fff',fillOpacity:1,weight:2.5}).addTo(trackGroup).bindTooltip('Start: '+(pts[0].Time||''));
    var last=pts[pts.length-1];
    L.circleMarker([last.Lat,last.Lng],{radius:7,color:'#c62828',fillColor:'#fff',fillOpacity:1,weight:2.5}).addTo(trackGroup).bindTooltip('Koniec: '+(last.Time||''));
    map.flyToBounds(trackGroup.getBounds().pad(.08),{duration:.4});
    if(stats){
        document.getElementById('tsKm').textContent=stats.km+' km';
        document.getElementById('tsAvg').textContent=stats.avgSpd;
        document.getElementById('tsMax').textContent=stats.maxSpd;
        document.getElementById('tsPts').textContent=stats.pts;
        document.getElementById('tsDrive').textContent=stats.drivingPct+'%';
        var ts='';if(stats.from&&stats.to){var f=stats.from.split(' ')[1]||stats.from;var t2=stats.to.split(' ')[1]||stats.to;ts=f+' — '+t2}
        document.getElementById('tsTime').textContent=ts||'—';
        document.getElementById('trackStats').style.display='block';
        document.getElementById('speedLegend').style.display='block';
    }
}
function spdColor(s){if(s<=0)return'#78909c';if(s<30)return'#66bb6a';if(s<60)return'#fdd835';if(s<90)return'#ff9800';return'#e53935'}
function clearTrack(){trackGroup.clearLayers();document.getElementById('trackStats').style.display='none';document.getElementById('speedLegend').style.display='none';replayStop()}

// ══ REPLAY — animacja trasy ══
function startReplay(pts,id){
    replayStop();clearTrack();
    if(!pts||pts.length<2)return;
    replayData=pts;replayIdx=0;replayPaused=false;
    // Rysuj pełną trasę bladą
    var ll=pts.map(function(p){return[p.Lat,p.Lng]});
    L.polyline(ll,{color:'#90a4ae',weight:2,opacity:.4,dashArray:'4,4'}).addTo(trackGroup);
    // Marker replay
    replayMarker=L.marker([pts[0].Lat,pts[0].Lng],{icon:L.divIcon({className:'',html:'<div style=""background:#6a1b9a;color:#fff;width:20px;height:20px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:11px;border:2px solid #fff;box-shadow:0 2px 8px rgba(0,0,0,.4)"">&#9654;</div>',iconSize:[20,20],iconAnchor:[10,10]})}).addTo(trackGroup);
    replayTrail=L.polyline([],{color:'#6a1b9a',weight:4,opacity:.8}).addTo(trackGroup);
    map.flyToBounds(L.latLngBounds(ll).pad(.1),{duration:.3});
    document.getElementById('replayBar').style.display='flex';
    replayTick();
}
function replayTick(){
    if(!replayData||replayIdx>=replayData.length){replayDone();return}
    if(replayPaused)return;
    var p=replayData[replayIdx];
    replayMarker.setLatLng([p.Lat,p.Lng]);
    replayTrail.addLatLng([p.Lat,p.Lng]);
    var info=(replayIdx+1)+' / '+replayData.length;
    if(p.Time){var t=p.Time.split(' ')[1]||p.Time;info+=' | '+t}
    if(p.Speed>0)info+=' | '+p.Speed+' km/h';
    document.getElementById('rbInfo').textContent=info;
    replayIdx++;
    replayTimer=setTimeout(replayTick,1000/replaySpeedVal);
}
function replayToggle(){replayPaused=!replayPaused;document.getElementById('rbPlayBtn').textContent=replayPaused?'&#9654;':'&#10074;&#10074;';if(!replayPaused)replayTick()}
function replaySpeed(){var speeds=[5,10,25,50,100];var i=speeds.indexOf(replaySpeedVal);replaySpeedVal=speeds[(i+1)%speeds.length];document.getElementById('rbSpeedBtn').textContent='x'+replaySpeedVal}
function replayStop(){if(replayTimer)clearTimeout(replayTimer);replayTimer=null;replayData=null;replayMarker=null;replayTrail=null;document.getElementById('replayBar').style.display='none'}
function replayDone(){document.getElementById('rbPlayBtn').textContent='&#10003;';document.getElementById('rbInfo').textContent='Zakończono';replayPaused=true}

function post(o){window.chrome.webview.postMessage(JSON.stringify(o))}
function esc(s){if(!s)return'';var d=document.createElement('div');d.appendChild(document.createTextNode(s));return d.innerHTML}
function escJs(s){return s?s.replace(/\\/g,'\\\\').replace(/'/g,""\\'""):''}
logToHost('Map ready');
function logToHost(m){try{post({Action:'log',Data:m})}catch(e){}}
</script></body></html>";
        }

        // ══════════════════════════════════════════════════════════════════
        // Models
        // ══════════════════════════════════════════════════════════════════

        private class WfObject
        {
            public string? objectno { get; set; }
            public string? objectname { get; set; }
            public double latitude_mdeg { get; set; }
            public double longitude_mdeg { get; set; }
            public int speed { get; set; }
            public int course { get; set; }
            public string? postext { get; set; }
            public string? pos_time { get; set; }
            public string? msgtime { get; set; }
            public string? driver { get; set; }
            public string? drivername { get; set; }
            public string? driveruid { get; set; }
            public int ignition { get; set; }
        }

        private class WfTrack
        {
            public double latitude_mdeg { get; set; }
            public double longitude_mdeg { get; set; }
            public double latitude { get; set; }
            public double longitude { get; set; }
            public int speed { get; set; }
            public string? pos_time { get; set; }
        }

        public class VehiclePosition
        {
            public string ObjectNo { get; set; } = "";
            public string ObjectName { get; set; } = "";
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public int Speed { get; set; }
            public int Course { get; set; }
            public string Address { get; set; } = "";
            public string Driver { get; set; } = "";
            public string LastUpdate { get; set; } = "";
            public bool IsMoving { get; set; }
            public bool Ignition { get; set; }
            public double DistToUbojnia { get; set; }
            public bool InGeofence { get; set; }
            public int EtaMinutes { get; set; }
            // Kurs transportowy (dzisiaj)
            public string? KursTrasa { get; set; }
            public string? KursStatus { get; set; }
            public string? KursGodzWyjazdu { get; set; }
            public string? KursGodzPowrotu { get; set; }
            public string? KursKierowca { get; set; }
            // Mapowanie
            [JsonIgnore] public string? CarTrailerID { get; set; }
            [JsonIgnore] public string? InternalName { get; set; }
            [JsonIgnore] public int MappedPojazdID { get; set; }
            [JsonIgnore] public string? WebfleetDriverId { get; set; }
        }

        public class TrackPoint
        {
            public double Lat { get; set; }
            public double Lng { get; set; }
            public int Speed { get; set; }
            public string Time { get; set; } = "";
        }

        private class KursInfo
        {
            public long KursID { get; set; }
            public string Trasa { get; set; } = "";
            public string Status { get; set; } = "";
            public string? GodzWyjazdu { get; set; }
            public string? GodzPowrotu { get; set; }
            public int? PojazdID { get; set; }
            public string KierowcaNazwa { get; set; } = "";
            public string Rejestracja { get; set; } = "";
        }

        private class MapMessage
        {
            public string? Action { get; set; }
            public string? Data { get; set; }
            public string? Date { get; set; }
        }
    }
}
