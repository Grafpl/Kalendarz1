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
        private static string WebfleetAccount  => Kalendarz1.Webfleet.WebfleetConfig.Account;
        private static string WebfleetUsername => Kalendarz1.Webfleet.WebfleetConfig.User;
        private static string WebfleetPassword => Kalendarz1.Webfleet.WebfleetConfig.Pass;
        private static string WebfleetApiKey   => Kalendarz1.Webfleet.WebfleetConfig.ApiKey;
        private static string WebfleetBaseUrl  => Kalendarz1.Webfleet.WebfleetConfig.BaseUrl;
        // Baza — ubojnia Koziołki 40 (TODO Faza 0.4 → Shared/Geo/FirmaLokalizacje.UbojniaKoziolki40)
        private const double UbLat = 51.86857, UbLon = 19.79476;

        private static string _basicAuth => Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{WebfleetUsername}:{WebfleetPassword}"));
        private static HttpClient _http => Kalendarz1.Webfleet.WebfleetHttp.Instance;
        private static readonly JsonSerializerSettings _js = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Error = (_, args) => { args.ErrorContext.Handled = true; }
        };
        private static readonly string _connTransport =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string _connLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string _connHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

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
            // Polishing — skróty 1-4 dla filter chips
            else if (e.Key == Key.D1 || e.Key == Key.NumPad1) { _chipFilter = "moving";  UpdateChipsActiveBorder(); if (_displayVehicles.Count > 0) UpdateSidePanel(_displayVehicles); e.Handled = true; }
            else if (e.Key == Key.D2 || e.Key == Key.NumPad2) { _chipFilter = "stopped"; UpdateChipsActiveBorder(); if (_displayVehicles.Count > 0) UpdateSidePanel(_displayVehicles); e.Handled = true; }
            else if (e.Key == Key.D3 || e.Key == Key.NumPad3) { _chipFilter = "all";     UpdateChipsActiveBorder(); if (_displayVehicles.Count > 0) UpdateSidePanel(_displayVehicles); e.Handled = true; }
            else if (e.Key == Key.D4 || e.Key == Key.NumPad4) { _chipFilter = "base";    UpdateChipsActiveBorder(); if (_displayVehicles.Count > 0) UpdateSidePanel(_displayVehicles); e.Handled = true; }
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

        // ══════════════════════════════════════════════════════════════════
        // Wolne zamówienia (Faza 4-C) — warstwa layer w mapie floty
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Public API (Faza 4-D) — wywołane np. przez TransportMainFormImproved.BtnMapa_Click.
        /// Włącza toggle i ładuje zamówienia dnia. Jeśli mapa nie jest gotowa, kolejkuje na po NavigationCompleted.
        /// </summary>
        public void ShowOrdersForDate(DateTime date)
        {
            if (OrdersDate != null) OrdersDate.SelectedDate = date;
            if (BtnToggleOrders != null) BtnToggleOrders.IsChecked = true;
            if (_mapReady)
                _ = LoadAndRenderOrdersAsync();
            else
                _pendingOrdersDate = date;
        }

        private DateTime? _pendingOrdersDate;

        private async void BtnToggleOrders_Click(object sender, RoutedEventArgs e)
        {
            if (BtnToggleOrders?.IsChecked == true)
                await LoadAndRenderOrdersAsync();
            else if (_mapReady)
                await MapWebView.CoreWebView2.ExecuteScriptAsync("clearOrders()");
        }

        private async void OrdersDate_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (BtnToggleOrders?.IsChecked == true)
                await LoadAndRenderOrdersAsync();
        }

        private async Task LoadAndRenderOrdersAsync()
        {
            if (!_mapReady) return;
            var date = OrdersDate?.SelectedDate ?? DateTime.Today;
            StatusText.Text = $"Wczytuję wolne zamówienia ({date:dd.MM})...";
            try
            {
                var svc = new Kalendarz1.Transport.Services.WolneZamowieniaMapaService(_connLibra, _connHandel);
                var orders = await svc.LoadMarkersAsync(date, useGoogleFallback: false);
                var mapped = orders.Where(o => o.Lat.HasValue).Select(o => new
                {
                    o.Id,
                    o.Klient,
                    o.Adres,
                    o.Handlowiec,
                    Palety = (double)o.Palety,
                    o.Pojemniki,
                    DataUboju = o.DataUboju.ToString("yyyy-MM-dd"),
                    Lat = o.Lat!.Value,
                    Lng = o.Lng!.Value
                }).ToList();
                var json = JsonConvert.SerializeObject(mapped);
                await MapWebView.CoreWebView2.ExecuteScriptAsync($"setOrders({json})");
                StatusText.Text = $"Wolne zamówienia: {mapped.Count}/{orders.Count} z GPS";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd zamówień: {ex.Message}";
            }
        }

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

                    // Faza 4-D — consume pending orders date (queued przez ShowOrdersForDate)
                    if (_pendingOrdersDate.HasValue)
                    {
                        _pendingOrdersDate = null;
                        _ = LoadAndRenderOrdersAsync();
                    }
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

        // Faza 10-C — chip filter w panelu bocznym (all/moving/stopped/base)
        private string _chipFilter = "all";

        private void ChipFilter_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is string tag)
            {
                _chipFilter = tag;
                UpdateChipsActiveBorder();
                if (_displayVehicles.Count > 0) UpdateSidePanel(_displayVehicles);
            }
        }

        private void UpdateChipsActiveBorder()
        {
            // Compact chipy — active state przez Background (gdzie inactive=Transparent)
            var inactive = System.Windows.Media.Brushes.Transparent;
            var movingActive = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 245, 233));
            var stoppedActive = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 243, 224));
            var baseActive = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 245, 254));
            var allActive = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 246));

            if (ChipMoving != null)  ChipMoving.Background  = _chipFilter == "moving"  ? movingActive  : inactive;
            if (ChipStopped != null) ChipStopped.Background = _chipFilter == "stopped" ? stoppedActive : inactive;
            if (ChipBase != null)    ChipBase.Background    = _chipFilter == "base"    ? baseActive    : inactive;
            if (ChipAll != null)     ChipAll.Background     = _chipFilter == "all"     ? allActive     : inactive;
        }

        private void UpdateSidePanel(List<VehiclePosition> vehicles)
        {
            var filter = SearchBox.Text.Trim().ToLower();
            VehicleListPanel.Children.Clear();

            var list = string.IsNullOrEmpty(filter) ? vehicles :
                vehicles.Where(v => v.ObjectName.ToLower().Contains(filter) || v.Driver.ToLower().Contains(filter) ||
                    v.Address.ToLower().Contains(filter) || (v.InternalName ?? "").ToLower().Contains(filter) ||
                    (v.KursTrasa ?? "").ToLower().Contains(filter)).ToList();

            // Faza 10-C — chip filter (moving/stopped/base/all)
            list = _chipFilter switch
            {
                "moving"  => list.Where(v => v.IsMoving).ToList(),
                "stopped" => list.Where(v => !v.IsMoving && !v.InGeofence).ToList(),
                "base"    => list.Where(v => v.InGeofence).ToList(),
                _ => list
            };

            list = (SortCombo.SelectedIndex switch
            {
                1 => list.OrderByDescending(v => v.Speed).ThenBy(v => v.ObjectName),
                2 => list.OrderBy(v => v.ObjectName),
                3 => list.OrderByDescending(v => v.LastUpdate),
                _ => list.OrderByDescending(v => v.IsMoving).ThenByDescending(v => v.Speed).ThenBy(v => v.ObjectName)
            }).ToList();

            // Polishing — empty state gdy filter daje 0 wyników
            if (list.Count == 0)
            {
                var emptyMsg = _chipFilter switch
                {
                    "moving"  => "🚛 Brak pojazdów w trasie",
                    "stopped" => "⏸ Brak pojazdów na postoju",
                    "base"    => "🏠 Żaden pojazd nie jest w bazie",
                    _ => string.IsNullOrEmpty(filter) ? "Brak pojazdów" : "Brak pojazdów spełniających filtr"
                };
                VehicleListPanel.Children.Add(new TextBlock
                {
                    Text = emptyMsg,
                    Foreground = new SolidColorBrush(Color.FromRgb(176, 190, 197)),
                    FontSize = 12,
                    FontStyle = FontStyles.Italic,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 24, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                });
                return;
            }

            foreach (var v in list) VehicleListPanel.Children.Add(CreateVehicleCard(v));
        }

        // Polishing — spójne kolory 4 stanów (matching markery na mapie)
        private static Color GetStateColor(VehiclePosition v)
        {
            if (v.IsMoving)      return Color.FromRgb(67, 160, 71);   // #43a047 zielony (matches marker moving)
            if (v.InGeofence)    return Color.FromRgb(25, 118, 210);  // #1976d2 niebieski (matches marker base)
            if (v.Ignition)      return Color.FromRgb(245, 124, 0);   // #f57c00 pomarańczowy (matches marker idle)
            return                      Color.FromRgb(96, 125, 139);  // #607d8b szary (matches marker off)
        }

        private static string GetStateLabel(VehiclePosition v)
        {
            if (v.IsMoving)   return "W trasie";
            if (v.InGeofence) return "W bazie";
            if (v.Ignition)   return "Postój (zapłon)";
            return                    "Wyłączony";
        }

        private static string GetStateShortLabel(VehiclePosition v)
        {
            if (v.IsMoving)   return "trasa";
            if (v.InGeofence) return "baza";
            if (v.Ignition)   return "postój";
            return                    "off";
        }

        // Polish++ pomysł 3 — skraca "Jan Kowalski" → "Jan K." dla compact card
        private static string ShortenDriver(string? driver)
        {
            if (string.IsNullOrWhiteSpace(driver)) return "";
            var parts = driver.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "";
            if (parts.Length == 1) return parts[0];
            return $"{parts[0]} {parts[parts.Length - 1][..1]}.";
        }

        // Polish++ pomysł 3 — pełen detal w Tooltip na hover (zamiast 7 rzędów w card)
        private static string BuildVehicleTooltip(VehiclePosition v, string ageText)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"📍 {v.ObjectName}");
            sb.AppendLine($"   {GetStateLabel(v)}{(v.IsMoving ? $" · {v.Speed} km/h" : "")}");
            if (!string.IsNullOrWhiteSpace(v.Driver)) sb.AppendLine($"👤 Kierowca GPS: {v.Driver}");
            if (!string.IsNullOrWhiteSpace(v.Address)) sb.AppendLine($"🗺 {v.Address}");
            if (!string.IsNullOrEmpty(v.KursTrasa))
            {
                sb.AppendLine($"📋 Kurs: {v.KursTrasa}");
                if (!string.IsNullOrEmpty(v.KursGodzWyjazdu))
                {
                    var godziny = v.KursGodzWyjazdu + (string.IsNullOrEmpty(v.KursGodzPowrotu) ? "" : $" → {v.KursGodzPowrotu}");
                    sb.AppendLine($"   ⏱ {godziny}");
                }
                if (!string.IsNullOrEmpty(v.KursStatus)) sb.AppendLine($"   Status: {v.KursStatus}");
                if (!string.IsNullOrEmpty(v.KursKierowca))
                {
                    var diff = !string.IsNullOrEmpty(v.Driver) && v.KursKierowca != v.Driver ? " ⚠" : "";
                    sb.AppendLine($"   Przypisany: {v.KursKierowca}{diff}");
                }
            }
            if (!string.IsNullOrEmpty(v.InternalName)) sb.AppendLine($"🚛 Transport: {v.InternalName}");
            sb.AppendLine($"📡 GPS: {ageText}");
            if (v.DistToUbojnia > 0) sb.AppendLine($"↗ {v.DistToUbojnia:F1} km do ubojni" + (v.EtaMinutes > 0 ? $" · ETA ~{v.EtaMinutes} min" : ""));
            sb.Append("\nKlik = śledź na mapie");
            return sb.ToString();
        }

        // Polishing — wiek GPS jako readable string + warning gdy stary
        private static (string text, bool warning) FormatGpsAge(string? lastUpdate)
        {
            if (string.IsNullOrWhiteSpace(lastUpdate)) return ("brak GPS", true);
            if (!DateTime.TryParse(lastUpdate, out var t)) return (lastUpdate ?? "—", false);
            var diff = DateTime.Now - t;
            if (diff.TotalMinutes < 1)   return ("teraz", false);
            if (diff.TotalMinutes < 60)  return ($"{(int)diff.TotalMinutes} min temu", false);
            if (diff.TotalHours < 24)    return ($"{(int)diff.TotalHours} godz. temu", diff.TotalHours > 1);
            if (diff.TotalDays < 7)      return ($"{(int)diff.TotalDays} dni temu", true);
            return (t.ToString("yyyy-MM-dd HH:mm"), true);
        }

        private Border CreateVehicleCard(VehiclePosition v)
        {
            // Polish++ pomysł 3 — uproszczona card (3 rzędy zamiast 7).
            // Pełen detal w Tooltip na hover.
            var red = Color.FromRgb(198, 40, 40);
            var sc = GetStateColor(v);
            var sb = new SolidColorBrush(sc);
            var (ageText, ageWarn) = FormatGpsAge(v.LastUpdate);

            var card = new Border
            {
                Background = Brushes.White, CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 4), Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                BorderBrush = new SolidColorBrush(Color.FromRgb(232, 234, 240)),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                    { ShadowDepth = 1, BlurRadius = 4, Opacity = 0.06, Direction = 270 },
                ToolTip = BuildVehicleTooltip(v, ageText) // pełen detal na hover
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

            var stack = new StackPanel { Margin = new Thickness(10, 6, 10, 6) };
            Grid.SetColumn(stack, 1);
            outerGrid.Children.Add(stack);

            // Row 1: name (bold) + speed/status pill (right)
            var r1 = new DockPanel();
            var rightBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(38, sc.R, sc.G, sc.B)),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(rightBadge, Dock.Right);
            rightBadge.Child = new TextBlock
            {
                Text = v.IsMoving ? $"{v.Speed} km/h" : GetStateShortLabel(v),
                Foreground = sb, FontSize = 10, FontWeight = FontWeights.Bold
            };
            r1.Children.Add(rightBadge);
            r1.Children.Add(new TextBlock
            {
                Text = v.ObjectName, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(38, 50, 56)),
                FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(r1);

            // Row 2: driver · kurs LUB driver · address
            string secondLine;
            Color secondColor;
            if (!string.IsNullOrEmpty(v.KursTrasa))
            {
                var drv = ShortenDriver(v.Driver);
                secondLine = string.IsNullOrEmpty(drv) ? v.KursTrasa : $"{drv} · {v.KursTrasa}";
                secondColor = Color.FromRgb(94, 53, 177);  // fioletowy = ma aktywny kurs
            }
            else if (!string.IsNullOrWhiteSpace(v.Driver) || !string.IsNullOrWhiteSpace(v.Address))
            {
                var drv = ShortenDriver(v.Driver);
                var loc = string.IsNullOrWhiteSpace(v.Address) ? "—" : v.Address;
                secondLine = string.IsNullOrEmpty(drv) ? loc : $"{drv} · {loc}";
                secondColor = Color.FromRgb(96, 125, 139);
            }
            else
            {
                secondLine = "—"; secondColor = Color.FromRgb(176, 190, 197);
            }
            stack.Children.Add(new TextBlock
            {
                Text = secondLine,
                Foreground = new SolidColorBrush(secondColor),
                FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            });

            // Row 3: WARUNKOWY — tylko gdy alert/info wart pokazania
            var warnings = new List<(string text, Color color)>();
            if (ageWarn) warnings.Add(("📡 GPS " + ageText, Color.FromRgb(230, 81, 0)));
            if (v.InGeofence) warnings.Add(("🏠 w bazie", Color.FromRgb(25, 118, 210)));
            if (v.IsMoving && v.EtaMinutes > 0 && v.DistToUbojnia < 50)
                warnings.Add(($"↗ {v.DistToUbojnia:F0} km · ~{v.EtaMinutes} min", Color.FromRgb(21, 101, 192)));

            if (warnings.Count > 0)
            {
                var r3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
                for (int i = 0; i < warnings.Count; i++)
                {
                    if (i > 0) r3.Children.Add(new TextBlock { Text = " · ", Foreground = new SolidColorBrush(Color.FromRgb(207, 216, 220)), FontSize = 9.5 });
                    r3.Children.Add(new TextBlock
                    {
                        Text = warnings[i].text,
                        Foreground = new SolidColorBrush(warnings[i].color),
                        FontSize = 9.5, FontWeight = FontWeights.SemiBold
                    });
                }
                stack.Children.Add(r3);
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
.vi{position:relative;filter:drop-shadow(0 3px 8px rgba(0,0,0,0.5))}
.vi-body{width:40px;height:40px;border-radius:50%;display:flex;align-items:center;justify-content:center;border:3px solid #fff;transition:all .3s}
.vi-body svg{width:26px;height:26px;fill:#fff;transition:transform .4s ease-out}
.vi-spd{position:absolute;top:-4px;right:-8px;background:#111;color:#fff;font-size:10px;font-weight:800;padding:2px 5px;border-radius:8px;border:1px solid #555;line-height:1.1;box-shadow:0 1px 4px rgba(0,0,0,.4)}
.vi-name{position:absolute;bottom:-18px;left:50%;transform:translateX(-50%);background:rgba(15,23,42,.88);color:#fff;font-size:10px;padding:2px 7px;border-radius:5px;white-space:nowrap;font-weight:700;pointer-events:none;letter-spacing:.3px;box-shadow:0 1px 3px rgba(0,0,0,.4);border:1px solid rgba(255,255,255,.15)}
/* Stany pojazdu — 4 kolory + animacje */
@keyframes pulseGreen{0%,100%{transform:scale(1);box-shadow:0 0 0 0 rgba(67,160,71,.55)}50%{transform:scale(1.06);box-shadow:0 0 0 14px rgba(67,160,71,0)}}
.vi-moving .vi-body{animation:pulseGreen 1.8s ease-in-out infinite}
@keyframes pulseBlue{0%,100%{box-shadow:0 0 0 0 rgba(25,118,210,.45)}50%{box-shadow:0 0 0 12px rgba(25,118,210,0)}}
.vi-base .vi-body{animation:pulseBlue 2s ease-in-out infinite}
.vi-idle .vi-body{box-shadow:0 0 0 3px rgba(245,124,0,.25)}
.vi-off .vi-body{opacity:.65}
.vi-off .vi-body svg{opacity:.85}
@keyframes glow{0%,100%{box-shadow:0 0 4px 2px rgba(229,57,53,.3)}50%{box-shadow:0 0 14px 5px rgba(229,57,53,.65)}}
.vi-geo .vi-body{animation:glow 1.5s ease-in-out infinite!important;border-color:#ef5350!important}
.vp{min-width:300px;padding:0}
.vp-head{background:linear-gradient(135deg,#1a237e,#283593);color:#fff;padding:14px 16px;border-radius:8px 8px 0 0;margin:-14px -20px 12px}
.vp-head h3{margin:0;font-size:16px;font-weight:700}.vp-head .sub{opacity:.75;font-size:11px;margin-top:3px}
/* Status pill — duży, na górze popup'u */
.vp-state{display:flex;align-items:center;justify-content:space-between;padding:10px 12px;border-radius:8px;margin:-4px 0 10px;font-size:13px;font-weight:700}
.vp-state.moving{background:linear-gradient(135deg,#43a047,#388e3c);color:#fff}
.vp-state.base{background:linear-gradient(135deg,#1976d2,#1565c0);color:#fff}
.vp-state.idle{background:linear-gradient(135deg,#f57c00,#e65100);color:#fff}
.vp-state.off{background:linear-gradient(135deg,#78909c,#607d8b);color:#fff}
.vp-state-emoji{font-size:18px;margin-right:8px}
.vp-state-meta{font-size:11px;opacity:.9;font-weight:500}
/* Kurs box — prominent gdy aktywny */
.vp-kurs-box{background:linear-gradient(135deg,#ede7f6,#d1c4e9);padding:10px 12px;border-radius:8px;margin:8px 0;border-left:4px solid #7b1fa2}
.vp-kurs-h{font-size:10px;font-weight:700;color:#7b1fa2;letter-spacing:.4px;margin-bottom:4px}
.vp-kurs-route{font-size:13px;font-weight:700;color:#263238;margin-bottom:4px}
.vp-kurs-meta{font-size:11px;color:#546e7a;line-height:1.5}
.vp-kurs-meta b{color:#263238}
.vp-r{display:flex;justify-content:space-between;padding:6px 0;border-bottom:1px solid #f0f0f5;font-size:12px}
.vp-r:last-child{border:none}.vp-r .k{color:#78909c}.vp-r .v{font-weight:600;color:#263238;max-width:170px;text-align:right}
.vp-btn{display:block;width:100%;margin-top:5px;padding:8px;text-align:center;border:none;border-radius:6px;cursor:pointer;font-size:11.5px;font-weight:600;transition:.15s}
.vp-btn.tr{background:#1565c0;color:#fff}.vp-btn.tr:hover{background:#0d47a1}
.vp-btn.rp{background:#6a1b9a;color:#fff}.vp-btn.rp:hover{background:#4a148c}
.vp-btn.fw{background:#00695c;color:#fff}.vp-btn.fw:hover{background:#004d40}
.vp-btn.cl{background:#eceff1;color:#546e7a;margin-top:3px}.vp-btn.cl:hover{background:#cfd8dc}
.vp-date{width:100%;padding:6px;margin-top:6px;border:1px solid #cfd8dc;border-radius:4px;font-size:11px}
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
var ordersLayer=L.layerGroup(),orderMarkers={},ordersVisible=false;
var replayData=null,replayIdx=0,replayTimer=null,replayPaused=false,replaySpeedVal=10,replayMarker=null,replayTrail=null;

// Strzałka nawigacyjna (czubek na góre = kierunek 0°), reagująca na Course
var arrowPath='M15 3 L26 24 L15 19 L4 24 Z';
function mkIcon(v){
    // 4 stany kolorystyczne (Faza 10-A):
    //   moving = zielony pulse (jedzie)
    //   base = niebieski pulse (w geofence ubojni)
    //   idle = pomarańczowy (zapłon wł., postój)
    //   off = szary semi-transparent (silnik wyłączony)
    var state, bgColor;
    if(v.IsMoving){ state='moving'; bgColor='#43a047'; }
    else if(v.InGeofence){ state='base'; bgColor='#1976d2'; }
    else if(v.Ignition){ state='idle'; bgColor='#f57c00'; }
    else{ state='off'; bgColor='#607d8b'; }

    var cls='vi vi-'+state+(v.InGeofence&&!v.IsMoving?' vi-geo':'');
    var spd=v.Speed>0?'<div class=""vi-spd"">'+v.Speed+'</div>':'';
    var name='<div class=""vi-name"">'+esc(v.ObjectName.substring(0,14))+'</div>';
    // Zawsze pokazuj last known heading (nawet stop)
    var rot=v.Course||0;
    return L.divIcon({className:'',html:'<div class=""'+cls+'""><div class=""vi-body"" style=""background:'+bgColor+'""><svg viewBox=""0 0 30 30"" style=""transform:rotate('+rot+'deg)""><path d=""'+arrowPath+'""/></svg></div>'+spd+name+'</div>',iconSize:[40,40],iconAnchor:[20,20],popupAnchor:[0,-22]});
}

function mkPopup(v){
    // Faza 10-B — status pill na górze + prominent kurs box
    var stateCls, stateEmoji, stateLabel, stateMeta;
    if(v.IsMoving){
        stateCls='moving'; stateEmoji='&#9889;'; // ⚡
        stateLabel='W TRASIE'; stateMeta=v.Speed+' km/h';
    } else if(v.InGeofence){
        stateCls='base'; stateEmoji='&#127968;'; // 🏠
        stateLabel='W BAZIE'; stateMeta='Łyszkowice';
    } else if(v.Ignition){
        stateCls='idle'; stateEmoji='&#9208;'; // ⏸
        stateLabel='POSTÓJ'; stateMeta='Zapłon wł.';
    } else {
        stateCls='off'; stateEmoji='&#9209;'; // ⏹
        stateLabel='WYŁĄCZONY'; stateMeta='Silnik off';
    }

    var dirs=['N','NE','E','SE','S','SW','W','NW'];
    var dirIdx=Math.round((v.Course||0)/45)%8;
    var dirName=dirs[dirIdx];
    var dist=v.DistToUbojnia!=null?v.DistToUbojnia.toFixed(1)+' km':'—';
    var eta=v.EtaMinutes>0?'ok. '+v.EtaMinutes+' min':'—';

    // Kurs box (gdy aktualny kurs przypisany)
    var kursHtml='';
    if(v.KursTrasa){
        kursHtml='<div class=""vp-kurs-box""><div class=""vp-kurs-h"">&#128205; DZISIEJSZY KURS</div>'
            +'<div class=""vp-kurs-route"">'+esc(v.KursTrasa)+'</div>'
            +'<div class=""vp-kurs-meta"">';
        if(v.KursGodzWyjazdu){kursHtml+='&#9201; <b>'+v.KursGodzWyjazdu+'</b>';if(v.KursGodzPowrotu)kursHtml+=' &#8594; <b>'+v.KursGodzPowrotu+'</b>';kursHtml+='<br>'}
        if(v.KursStatus)kursHtml+='Status: <b>'+esc(v.KursStatus)+'</b><br>';
        if(v.KursKierowca){
            // Highlight gdy kierowca z kursu różni się od kierowcy GPS
            var diff=v.Driver&&v.KursKierowca!==v.Driver?' <span style=""color:#c62828"">&#9888;</span>':'';
            kursHtml+='Kierowca przypisany: <b>'+esc(v.KursKierowca)+'</b>'+diff;
        }
        kursHtml+='</div></div>';
    } else {
        kursHtml='<div style=""padding:6px 12px;background:#fafafa;border-radius:6px;margin:6px 0;font-size:11px;color:#90a4ae"">'
            +'&#128276; Brak dzisiejszego kursu w planowaniu</div>';
    }

    var today=new Date().toISOString().split('T')[0];
    return '<div class=""vp""><div class=""vp-head""><h3>'+esc(v.ObjectName)+'</h3><div class=""sub"">Kierowca GPS: '+esc(v.Driver||'—')+'</div></div>'
      +'<div class=""vp-state '+stateCls+'""><span><span class=""vp-state-emoji"">'+stateEmoji+'</span>'+stateLabel+'</span><span class=""vp-state-meta"">'+stateMeta+'</span></div>'
      +kursHtml
      +'<div class=""vp-r""><span class=""k"">Kierunek</span><span class=""v"">'+dirName+' ('+(v.Course||0)+'°)</span></div>'
      +'<div class=""vp-r""><span class=""k"">Pozycja</span><span class=""v"">'+esc(v.Address||'brak danych')+'</span></div>'
      +'<div class=""vp-r""><span class=""k"">Odl. od ubojni</span><span class=""v"">'+dist+'</span></div>'
      +(v.IsMoving?'<div class=""vp-r""><span class=""k"">ETA do ubojni</span><span class=""v"" style=""color:#1565c0"">'+eta+'</span></div>':'')
      +'<div class=""vp-r""><span class=""k"">Ostatni GPS</span><span class=""v"">'+(v.LastUpdate||'—')+'</span></div>'
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
            m.setTooltipContent(mkTooltip(v));
        }else{
            markers[v.ObjectNo]=L.marker([v.Latitude,v.Longitude],{icon:ic}).addTo(map)
                .bindPopup(pop,{maxWidth:340}).bindTooltip(mkTooltip(v),{direction:'top',offset:[0,-28]});
        }
    });
}
// Polishing — tooltip wzbogacony o kurs
function mkTooltip(v){
    var status = v.IsMoving ? (v.Speed+' km/h')
        : (v.InGeofence ? 'w bazie'
        : (v.Ignition ? 'postój' : 'wyłączony'));
    var s = v.ObjectName + ' — ' + status;
    if(v.KursTrasa) s += '  ·  ' + v.KursTrasa;
    return s;
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

// ══ Wolne zamówienia (Faza 4-C) — warstwa markerów odbiorców do zaplanowania ══
function setOrders(orders){
    ordersLayer.clearLayers();orderMarkers={};
    if(!orders||orders.length===0){if(ordersVisible)map.removeLayer(ordersLayer);ordersVisible=false;return}
    orders.forEach(function(o){
        if(o.Lat==null||o.Lng==null)return;
        var color=o.Pojemniki>200?'#c62828':(o.Pojemniki>100?'#e65100':'#1565c0');
        var ic=L.divIcon({className:'',iconSize:[28,28],iconAnchor:[14,14],
            html:'<div style=""background:'+color+';color:#fff;width:28px;height:28px;border-radius:50%;display:flex;align-items:center;justify-content:center;border:2px solid #fff;box-shadow:0 2px 6px rgba(0,0,0,.4);font-size:11px;font-weight:700"">&#128230;</div>'});
        var m=L.marker([o.Lat,o.Lng],{icon:ic}).addTo(ordersLayer);
        var ub=(o.DataUboju||'').split('T')[0];
        var pop='<div class=""vp""><div class=""vp-head""><h3>'+esc(o.Klient||'?')+'</h3><div class=""sub"">Zam. #'+o.Id+' &middot; '+esc(o.Handlowiec||'—')+'</div></div>'
            +'<div class=""vp-r""><span class=""k"">Adres</span><span class=""v"">'+esc(o.Adres||'—')+'</span></div>'
            +'<div class=""vp-r""><span class=""k"">Ubój</span><span class=""v"">'+ub+'</span></div>'
            +'<div class=""vp-r""><span class=""k"">Pojemniki E2</span><span class=""v"">'+o.Pojemniki+'</span></div>'
            +'<div class=""vp-r""><span class=""k"">Palety</span><span class=""v"">'+(o.Palety||0)+'</span></div></div>';
        m.bindPopup(pop,{maxWidth:300}).bindTooltip(esc(o.Klient||'')+' — '+o.Pojemniki+' E2',{direction:'top',offset:[0,-14]});
        orderMarkers[o.Id]=m;
    });
    if(!ordersVisible){map.addLayer(ordersLayer);ordersVisible=true}
}
function ordersToggle(on){
    ordersVisible=on;
    if(on)map.addLayer(ordersLayer);else map.removeLayer(ordersLayer);
}
function clearOrders(){ordersLayer.clearLayers();orderMarkers={};map.removeLayer(ordersLayer);ordersVisible=false}

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
