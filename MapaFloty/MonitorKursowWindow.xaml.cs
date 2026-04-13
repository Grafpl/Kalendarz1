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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Kalendarz1.MapaFloty
{
    public partial class MonitorKursowWindow : Window
    {
        private readonly KursMonitorService _svc = new();
        private KursMonitorService.MonitorKurs? _kurs;
        private List<TrackPt> _histPts = new();
        private DispatcherTimer? _liveTimer, _playTimer;
        private string _mapFolder = "";
        private bool _mapReady, _isLive;
        private int _playSpeed = 10;
        private readonly int[] _speeds = { 5, 10, 25, 50, 100 };
        private double _axisZoom = 1.0;
        private bool _followCam = true; // kamera podąża za pinezką

        // Webfleet
        private static readonly string WfAccount = "942879", WfUser = "Administrator", WfPass = "kaazZVY5";
        private static readonly string WfKey = "7a538868-96cf-4149-a9db-6e090de7276c";
        private static readonly string WfUrl = "https://csv.webfleet.com/extern";
        private static readonly string _auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{WfUser}:{WfPass}"));
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly JsonSerializerSettings _js = new()
        { MissingMemberHandling = MissingMemberHandling.Ignore, Error = (_, a) => { a.ErrorContext.Handled = true; } };

        public MonitorKursowWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            DatePick.SelectedDate = DateTime.Today;
            Loaded += async (_, _) => { await InitMap(); await LoadKursy(); };
            Closed += (_, _) => { _liveTimer?.Stop(); _playTimer?.Stop(); Cleanup(); };
        }

        private void Cleanup() { try { if (Directory.Exists(_mapFolder)) Directory.Delete(_mapFolder, true); } catch { } }

        // ══════════════════════════════════════════════════════════════════
        // WebView2 init
        // ══════════════════════════════════════════════════════════════════

        private async Task InitMap()
        {
            await MapView.EnsureCoreWebView2Async();
            _mapFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FMonitor_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_mapFolder);
            File.WriteAllText(System.IO.Path.Combine(_mapFolder, "m.html"), GenHtml(), Encoding.UTF8);
            var cw = MapView.CoreWebView2;
            var et = cw.GetType().Assembly.GetType("Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind")!;
            cw.GetType().GetMethod("SetVirtualHostNameToFolderMapping")!
                .Invoke(cw, new object[] { "fmon.local", _mapFolder, Enum.Parse(et, "Allow") });
            MapView.NavigationCompleted += (_, a) => _mapReady = a.IsSuccess;
            cw.Navigate("https://fmon.local/m.html");
        }

        private async Task Js(string code) { if (_mapReady) await MapView.CoreWebView2.ExecuteScriptAsync(code); }
        private static string F(double d) => d.ToString("F6", CultureInfo.InvariantCulture);

        // ══════════════════════════════════════════════════════════════════
        // Lista kursów
        // ══════════════════════════════════════════════════════════════════

        private void DatePick_Changed(object? s, SelectionChangedEventArgs e) => _ = LoadKursy();
        private void BtnToday_Click(object s, RoutedEventArgs e) { DatePick.SelectedDate = DateTime.Today; }

        private async Task LoadKursy()
        {
            _liveTimer?.Stop();
            var date = DatePick.SelectedDate ?? DateTime.Today;
            _isLive = date.Date == DateTime.Today;

            // Tryb badge
            if (_isLive)
            { ModeBadge.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233)); ModeText.Text = "LIVE — odświeżanie co 30s"; ModeText.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)); }
            else
            { ModeBadge.Background = new SolidColorBrush(Color.FromRgb(232, 234, 246)); ModeText.Text = $"HISTORIA — {date:dd.MM.yyyy}"; ModeText.Foreground = new SolidColorBrush(Color.FromRgb(57, 73, 171)); }

            var kursy = await _svc.PobierzKursyNaDzienAsync(date);
            KursyPanel.Children.Clear();
            PrzystankiPanel.Children.Clear();
            PrzystankiHeader.Visibility = Visibility.Collapsed;

            foreach (var k in kursy)
            {
                var pc = k.Postep >= 100 ? Color.FromRgb(46, 125, 50) : k.Postep > 0 ? Color.FromRgb(21, 101, 192) : Color.FromRgb(158, 158, 158);

                var card = new Border { Padding = new Thickness(14, 8, 14, 8), Cursor = Cursors.Hand,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(240, 240, 244)), BorderThickness = new Thickness(0, 0, 0, 1) };
                card.MouseEnter += (_, _) => card.Background = new SolidColorBrush(Color.FromRgb(245, 247, 252));
                card.MouseLeave += (_, _) => card.Background = Brushes.Transparent;
                var kid = k.KursID;
                card.MouseLeftButtonUp += async (_, _) => await SelectKurs(kid);

                var st = new StackPanel();
                var r1 = new DockPanel();
                var badge = new Border { Background = new SolidColorBrush(Color.FromArgb(30, pc.R, pc.G, pc.B)),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1), HorizontalAlignment = HorizontalAlignment.Right };
                DockPanel.SetDock(badge, Dock.Right);
                badge.Child = new TextBlock { Text = $"{k.Obsluzonych}/{k.Ladunkow}", Foreground = new SolidColorBrush(pc), FontWeight = FontWeights.Bold, FontSize = 11 };
                r1.Children.Add(badge);
                r1.Children.Add(new TextBlock { Text = k.Trasa, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(38, 50, 56)), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
                st.Children.Add(r1);

                st.Children.Add(new TextBlock { Text = $"{k.GodzWyjazdu}  {k.Kierowca}  {k.Rejestracja}",
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 144, 156)), FontSize = 10, Margin = new Thickness(0, 2, 0, 0) });

                if (k.Pominietych > 0)
                    st.Children.Add(new TextBlock { Text = $"{k.Pominietych} pominięty!", Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40)), FontSize = 9.5, FontWeight = FontWeights.Bold });

                // Progress bar
                var pb = new Border { Background = new SolidColorBrush(Color.FromRgb(235, 237, 242)), CornerRadius = new CornerRadius(2), Height = 3, Margin = new Thickness(0, 4, 0, 0) };
                pb.Child = new Border { Background = new SolidColorBrush(pc), CornerRadius = new CornerRadius(2), Height = 3,
                    HorizontalAlignment = HorizontalAlignment.Left, Width = Math.Max(0, k.Postep / 100.0 * 280) };
                st.Children.Add(pb);
                card.Child = st;
                KursyPanel.Children.Add(card);
            }

            if (_isLive)
            {
                _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
                _liveTimer.Tick += async (_, _) => { if (_kurs != null) await RefreshLive(); };
                _liveTimer.Start();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Wybrany kurs
        // ══════════════════════════════════════════════════════════════════

        private async Task SelectKurs(long kursId)
        {
            StopPlay();
            _kurs = await _svc.PobierzKursDoMonitoruAsync(kursId);
            if (_kurs == null) return;

            InfoBar.Visibility = Visibility.Visible;
            PrzystankiHeader.Visibility = Visibility.Visible;
            InfoTitle.Text = $"{_kurs.Trasa} — {_kurs.Rejestracja}";
            InfoDetail.Text = $"Kierowca: {_kurs.Kierowca} | Wyjazd: {_kurs.GodzWyjazdu} | Powrót: {_kurs.GodzPowrotu}";
            PrzystankiTitle.Text = $"Przystanki — {_kurs.Trasa}";

            if (_isLive)
            {
                SliderBar.Visibility = Visibility.Collapsed;
                BtnGpx.Visibility = Visibility.Collapsed;
                await RefreshLive();
            }
            else
            {
                BtnGpx.Visibility = Visibility.Visible;
                BtnCompare.Visibility = Visibility.Visible;
                await LoadHistory();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // TRYB LIVE — GPS co 30s, proximity detection
        // ══════════════════════════════════════════════════════════════════

        private async Task RefreshLive()
        {
            if (_kurs == null) return;
            var pos = await _svc.PobierzPozycjeAsync(_kurs.WebfleetObjectNo);
            double vLat = 0, vLon = 0; int vSpd = 0; string vAddr = "";
            if (pos.HasValue) { vLat = pos.Value.lat; vLon = pos.Value.lon; vSpd = pos.Value.speed; vAddr = pos.Value.address; }

            if (vLat != 0) await _svc.AktualizujStatusyAsync(_kurs, vLat, vLon, vSpd);

            var obsl = _kurs.Przystanki.Count(p => p.Status == "Obsluzony");
            InfoPostep.Text = $"{obsl}/{_kurs.Przystanki.Count}";

            var json = SerializePrzystanki();
            await Js($"showKurs({json},{F(vLat)},{F(vLon)},{vSpd},'{Esc(vAddr)}',true)");
            BuildPrzystankiList();
        }

        // ══════════════════════════════════════════════════════════════════
        // TRYB HISTORIA — załaduj GPS track + slider
        // ══════════════════════════════════════════════════════════════════

        private List<StandStill> _standStills = new();
        private List<TripInfo> _trips = new();

        private async Task LoadHistory()
        {
            if (_kurs == null) return;
            if (string.IsNullOrEmpty(_kurs.WebfleetObjectNo)) { InfoPostep.Text = "Brak mapowania GPS"; return; }

            var dateStr = _kurs.DataKursu.ToString("yyyy-MM-dd");
            InfoPostep.Text = "Ładowanie danych...";

            // Pobierz 3 źródła danych równolegle
            var trackTask = FetchTrack(_kurs.WebfleetObjectNo, dateStr);
            var stopsTask = FetchStandStills(_kurs.WebfleetObjectNo, dateStr);
            var tripsTask = FetchTrips(_kurs.WebfleetObjectNo, dateStr);
            await Task.WhenAll(trackTask, stopsTask, tripsTask);
            _histPts = trackTask.Result;
            _standStills = stopsTask.Result;
            _trips = tripsTask.Result;

            if (_histPts.Count == 0) { InfoPostep.Text = "Brak danych GPS"; return; }

            // Dopasuj przystanki kursu do postojów GPS
            AnalyzeHistoricStops();

            // Scoring
            int harsh = 0;
            for (int i = 1; i < _histPts.Count; i++)
                if (_histPts[i - 1].Speed - _histPts[i].Speed > 30) harsh++;
            int score = Math.Max(1, 10 - Math.Min(_histPts.Count(p => p.Speed > 90), 3) - Math.Min(harsh, 3));
            var sc = score >= 8 ? Color.FromRgb(46, 125, 50) : score >= 5 ? Color.FromRgb(230, 81, 0) : Color.FromRgb(198, 40, 40);
            ScoreBadge.Background = new SolidColorBrush(Color.FromArgb(30, sc.R, sc.G, sc.B));
            ScoreText.Text = $"Ocena: {score}/10"; ScoreText.Foreground = new SolidColorBrush(sc);
            ScoreBadge.Visibility = Visibility.Visible;

            // Stats
            double km = 0; int maxSpd = 0;
            for (int i = 1; i < _histPts.Count; i++)
            { km += HavM(_histPts[i - 1].Lat, _histPts[i - 1].Lng, _histPts[i].Lat, _histPts[i].Lng) / 1000.0; if (_histPts[i].Speed > maxSpd) maxSpd = _histPts[i].Speed; }
            var obsl = _kurs.Przystanki.Count(p => p.Status == "Obsluzony");
            InfoPostep.Text = $"{obsl}/{_kurs.Przystanki.Count} | {km:F1} km | max {maxSpd} | {_standStills.Count} postojów | {_trips.Count} wyjazdów";

            // Slider + oś 24h
            SliderBar.Visibility = Visibility.Visible;
            Build24hAxis();

            // Zapis do bazy
            foreach (var p in _kurs.Przystanki) await _svc.SaveRealizacjaPublic(_kurs.KursID, p);

            // Wyślij do mapy — trasa + postoje Webfleet + przystanki kursu
            var przJson = SerializePrzystanki();
            var ptsJson = JsonConvert.SerializeObject(_histPts);
            var ssJson = JsonConvert.SerializeObject(_standStills);
            var driverInfo = $"{_kurs.Kierowca} | {_kurs.Rejestracja}";
            await Js($"curInfo='{Esc(driverInfo)}';showHistory({przJson},{ptsJson},{ssJson})");
            BuildPrzystankiList();
        }

        private void AnalyzeHistoricStops()
        {
            if (_kurs == null) return;
            // Dla każdego przystanku kursowego: znajdź najbliższy postój Webfleet
            foreach (var p in _kurs.Przystanki)
            {
                if (p.Lat == 0 || p.Lon == 0) continue;

                // Szukaj w standstills Webfleet
                StandStill? bestStop = null; double bestDist = double.MaxValue;
                foreach (var ss in _standStills)
                {
                    var d = HavM(ss.Lat, ss.Lon, p.Lat, p.Lon);
                    if (d < bestDist) { bestDist = d; bestStop = ss; }
                }

                p.OdlegloscMinM = (int)bestDist;
                if (bestStop != null && bestDist < 800) // < 800m = dotarł
                {
                    p.Status = "Obsluzony";
                    p.CzasDotarcia = bestStop.StartTime;
                    p.CzasOdjazdu = bestStop.EndTime;
                    p.CzasPostojuMin = bestStop.DurationMin;
                }
                else
                {
                    // Fallback — szukaj w GPS track
                    DateTime? arr = null, dep = null;
                    for (int i = 0; i < _histPts.Count; i++)
                    {
                        var d = HavM(_histPts[i].Lat, _histPts[i].Lng, p.Lat, p.Lon);
                        if (d < 500 && _histPts[i].Speed < 5 && arr == null)
                        { DateTime.TryParse(_histPts[i].Time, out var t); arr = t; }
                        if (arr != null && d > 1000 && dep == null)
                        { DateTime.TryParse(_histPts[i].Time, out var t); dep = t; }
                    }
                    if (arr != null)
                    {
                        p.Status = dep != null ? "Obsluzony" : "Dotarl";
                        p.CzasDotarcia = arr; p.CzasOdjazdu = dep;
                        if (arr.HasValue && dep.HasValue) p.CzasPostojuMin = (int)(dep.Value - arr.Value).TotalMinutes;
                    }
                    else if (bestDist > 2000) p.Status = "Pominiety";
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Slider + Play
        // ══════════════════════════════════════════════════════════════════

        private void Slider_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
        {
            var min = (int)TrackSlider.Value;
            var idx = FindPtByMinute(min);
            if (idx < 0 || idx >= _histPts.Count) return;
            var p = _histPts[idx];
            var h = min / 60; var m = min % 60;
            SliderInfo.Text = $"{h:D2}:{m:D2}  |  {(p.Speed > 0 ? $"{p.Speed} km/h" : "postój")}  |  pkt {idx + 1}/{_histPts.Count}";
            _ = Js($"moveCursor({idx},{(_followCam ? 1 : 0)})");

            // Scroll oś 24h do pozycji slidera
            if (_axisZoom > 1 && AxisScroll.ExtentWidth > AxisScroll.ViewportWidth)
            {
                var ratio = min / 1440.0;
                var targetX = ratio * AxisScroll.ExtentWidth - AxisScroll.ViewportWidth / 2;
                AxisScroll.ScrollToHorizontalOffset(Math.Max(0, targetX));
            }
        }

        private int FindPtByMinute(int targetMin)
        {
            if (_histPts.Count == 0) return 0;
            int best = 0; double bestDiff = double.MaxValue;
            for (int i = 0; i < _histPts.Count; i++)
            {
                if (DateTime.TryParse(_histPts[i].Time, out var dt))
                {
                    var ptMin = dt.Hour * 60 + dt.Minute;
                    var diff = Math.Abs(ptMin - targetMin);
                    if (diff < bestDiff) { bestDiff = diff; best = i; }
                }
            }
            return best;
        }

        private void BtnPlay_Click(object s, RoutedEventArgs e)
        {
            if (_playTimer != null && _playTimer.IsEnabled) { StopPlay(); return; }
            BtnPlay.Content = "\u23F8";
            _playTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / _playSpeed) };
            _playTimer.Tick += (_, _) => { if (TrackSlider.Value >= TrackSlider.Maximum) { StopPlay(); return; } TrackSlider.Value++; };
            _playTimer.Start();
        }

        private void StopPlay() { _playTimer?.Stop(); BtnPlay.Content = "\u25B6"; }
        private void BtnStep_Back10(object s, RoutedEventArgs e) => TrackSlider.Value = Math.Max(0, TrackSlider.Value - 10);
        private void BtnStep_Back1(object s, RoutedEventArgs e) => TrackSlider.Value = Math.Max(0, TrackSlider.Value - 1);
        private void BtnStep_Fwd1(object s, RoutedEventArgs e) => TrackSlider.Value = Math.Min(TrackSlider.Maximum, TrackSlider.Value + 1);
        private void BtnStep_Fwd10(object s, RoutedEventArgs e) => TrackSlider.Value = Math.Min(TrackSlider.Maximum, TrackSlider.Value + 10);
        private void AxisScroll_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Scroll w górę = zoom in, w dół = zoom out
            if (e.Delta > 0) _axisZoom = Math.Min(_axisZoom * 1.3, 20);
            else _axisZoom = Math.Max(_axisZoom / 1.3, 1);

            // Zapamiętaj pozycję scrolla przed przerysowaniem
            var scrollPos = AxisScroll.HorizontalOffset / Math.Max(AxisScroll.ExtentWidth, 1);

            Build24hAxis();

            // Przywróć proporcjonalną pozycję scrolla
            AxisScroll.ScrollToHorizontalOffset(scrollPos * AxisScroll.ExtentWidth);
            ZoomInfo.Text = $"Zoom: {(int)(_axisZoom * 100)}%";
            e.Handled = true;
        }

        private void BtnZoomReset_Click(object s, RoutedEventArgs e)
        {
            _axisZoom = 1.0;
            Build24hAxis();
            AxisScroll.ScrollToHorizontalOffset(0);
            ZoomInfo.Text = "Zoom: 100%";
        }

        private void BtnFollowCam_Click(object s, RoutedEventArgs e)
        {
            _followCam = !_followCam;
            BtnFollowCam.Background = new SolidColorBrush(_followCam ? Color.FromRgb(232, 245, 233) : Color.FromRgb(238, 238, 238));
            BtnFollowCam.Foreground = new SolidColorBrush(_followCam ? Color.FromRgb(46, 125, 50) : Color.FromRgb(158, 158, 158));
            BtnFollowCam.Content = _followCam ? "\U0001F441 Kamera" : "\U0001F441 Kamera OFF";
        }

        private void BtnSpeed_Click(object s, RoutedEventArgs e)
        {
            var i = Array.IndexOf(_speeds, _playSpeed);
            _playSpeed = _speeds[(i + 1) % _speeds.Length];
            BtnSpd.Content = $"x{_playSpeed}";
            if (_playTimer != null) _playTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / _playSpeed);
        }

        // ══════════════════════════════════════════════════════════════════
        // GPX Export
        // ══════════════════════════════════════════════════════════════════

        private async void BtnPlanVsFakt_Click(object sender, RoutedEventArgs e)
        {
            if (_kurs == null || _histPts.Count < 2) return;
            // Buduj planowaną trasę z OSRM (baza → klient1 → klient2 → ...)
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var points = new List<(double lat, double lon)> { (51.86857, 19.79476) }; // baza
            foreach (var p in _kurs.Przystanki.OrderBy(x => x.Kolejnosc))
                if (p.Lat != 0) points.Add((p.Lat, p.Lon));
            points.Add((51.86857, 19.79476)); // powrót do bazy

            if (points.Count < 3) return;
            var coords = string.Join(";", points.Select(p => $"{p.lon.ToString("F5", ci)},{p.lat.ToString("F5", ci)}"));
            try
            {
                var url = $"https://router.project-osrm.org/route/v1/driving/{coords}?overview=full&geometries=geojson";
                var resp = await new System.Net.Http.HttpClient().GetStringAsync(url);
                var json = Newtonsoft.Json.Linq.JObject.Parse(resp);
                var geom = json["routes"]?[0]?["geometry"]?["coordinates"];
                if (geom == null) return;

                var planPts = new List<(double lat, double lng)>();
                foreach (var c in geom) planPts.Add(((double)c[1], (double)c[0]));

                // Wyślij do JS — niebieska (plan) + czerwona (fakt)
                var planJson = Newtonsoft.Json.JsonConvert.SerializeObject(planPts.Select(p => new { Lat = p.lat, Lng = p.lng }));
                var faktJson = Newtonsoft.Json.JsonConvert.SerializeObject(_histPts.Select(p => new { p.Lat, p.Lng }));

                var planKm = 0.0;
                for (int i = 1; i < planPts.Count; i++)
                    planKm += HavM(planPts[i - 1].lat, planPts[i - 1].lng, planPts[i].lat, planPts[i].lng) / 1000.0;
                var faktKm = 0.0;
                for (int i = 1; i < _histPts.Count; i++)
                    faktKm += HavM(_histPts[i - 1].Lat, _histPts[i - 1].Lng, _histPts[i].Lat, _histPts[i].Lng) / 1000.0;
                var diff = faktKm - planKm;
                var diffPct = planKm > 0 ? diff / planKm * 100 : 0;

                await Js($"showPlanVsFakt({planJson},{faktJson})");
                SliderInfo.Text = $"Plan: {planKm:F0} km | Fakt: {faktKm:F0} km | Różnica: {(diff > 0 ? "+" : "")}{diff:F0} km ({diffPct:F0}%)";
            }
            catch (Exception ex) { SliderInfo.Text = $"Błąd: {ex.Message}"; }
        }

        private void BtnExportGpx_Click(object sender, RoutedEventArgs e)
        {
            if (_histPts.Count == 0) return;
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<gpx version=\"1.1\" creator=\"MapaFloty\">\n<trk><name>Trasa</name><trkseg>\n");
            foreach (var p in _histPts)
                sb.AppendLine($"<trkpt lat=\"{F(p.Lat)}\" lon=\"{F(p.Lng)}\"><time>{p.Time.Replace(" ", "T")}Z</time><speed>{p.Speed}</speed></trkpt>");
            sb.AppendLine("</trkseg></trk></gpx>");
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Trasa_{_kurs?.DataKursu:yyyyMMdd}_{DateTime.Now:HHmmss}.gpx");
            File.WriteAllText(path, sb.ToString()); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }

        // ══════════════════════════════════════════════════════════════════
        // Timeline + lista przystanków (WPF)
        // ══════════════════════════════════════════════════════════════════

        private void Build24hAxis()
        {
            double baseW = AxisScroll.ActualWidth > 10 ? AxisScroll.ActualWidth : 900;
            double w = baseW * _axisZoom;
            // Ustaw szerokość wszystkich Canvasów
            HourAxis.Width = w; TimelineCanvas.Width = w; StopMarksCanvas.Width = w;

            // Oś godzin 0-24
            HourAxis.Children.Clear();
            for (int h = 0; h <= 24; h++)
            {
                var x = h / 24.0 * w;
                HourAxis.Children.Add(new Line { X1 = x, X2 = x, Y1 = 0, Y2 = 14, Stroke = new SolidColorBrush(Color.FromRgb(210, 210, 218)), StrokeThickness = 0.5 });
                if (h % 2 == 0 && h < 24)
                {
                    var tb = new TextBlock { Text = $"{h:D2}:00", FontSize = 8, Foreground = new SolidColorBrush(Color.FromRgb(144, 164, 174)) };
                    Canvas.SetLeft(tb, x + 1); Canvas.SetTop(tb, 1); HourAxis.Children.Add(tb);
                }
            }

            // Timeline — szare tło + kolorowe punkty GPS
            TimelineCanvas.Children.Clear();
            TimelineCanvas.Children.Add(new Rectangle { Width = w, Height = 16, Fill = new SolidColorBrush(Color.FromRgb(240, 240, 244)) });
            foreach (var p in _histPts)
            {
                if (!DateTime.TryParse(p.Time, out var dt)) continue;
                var min = dt.Hour * 60 + dt.Minute;
                var x = min / 1440.0 * w;
                var c = p.Speed <= 0 ? Color.FromRgb(255, 183, 77) : p.Speed < 30 ? Color.FromRgb(102, 187, 106) :
                    p.Speed < 60 ? Color.FromRgb(255, 238, 88) : p.Speed < 90 ? Color.FromRgb(255, 152, 0) : Color.FromRgb(229, 57, 53);
                TimelineCanvas.Children.Add(new Rectangle { Width = Math.Max(w / 1440.0 * 2, 2), Height = 16, Fill = new SolidColorBrush(c) });
                Canvas.SetLeft(TimelineCanvas.Children[^1], x);
            }

            // Pasy postojów Webfleet (szare/pomarańczowe)
            foreach (var ss in _standStills)
            {
                var x1 = (ss.StartTime.Hour * 60 + ss.StartTime.Minute) / 1440.0 * w;
                var x2 = (ss.EndTime.Hour * 60 + ss.EndTime.Minute) / 1440.0 * w;
                var clr = ss.DurationMin > 30 ? Color.FromArgb(80, 198, 40, 40) : ss.DurationMin > 10 ? Color.FromArgb(60, 230, 81, 0) : Color.FromArgb(40, 120, 144, 156);
                TimelineCanvas.Children.Add(new Rectangle { Width = Math.Max(x2 - x1, 3), Height = 16, Fill = new SolidColorBrush(clr) });
                Canvas.SetLeft(TimelineCanvas.Children[^1], x1);
            }

            // Pasy przystanków kursu (niebieskie, nad postojami)
            if (_kurs != null)
            {
                foreach (var st in _kurs.Przystanki.Where(s => s.CzasDotarcia.HasValue && s.CzasOdjazdu.HasValue))
                {
                    var x1 = (st.CzasDotarcia!.Value.Hour * 60 + st.CzasDotarcia.Value.Minute) / 1440.0 * w;
                    var x2 = (st.CzasOdjazdu!.Value.Hour * 60 + st.CzasOdjazdu.Value.Minute) / 1440.0 * w;
                    TimelineCanvas.Children.Add(new Rectangle { Width = Math.Max(x2 - x1, 4), Height = 16,
                        Fill = new SolidColorBrush(Color.FromArgb(70, 21, 101, 192)) });
                    Canvas.SetLeft(TimelineCanvas.Children[^1], x1);
                }
            }

            // Markery pod osią — postoje Webfleet (P) + przystanki kursu (numer)
            StopMarksCanvas.Children.Clear();

            // Postoje Webfleet — małe "P"
            foreach (var ss in _standStills)
            {
                var x = (ss.StartTime.Hour * 60 + ss.StartTime.Minute) / 1440.0 * w;
                var clr = ss.DurationMin > 30 ? Color.FromRgb(198, 40, 40) : ss.DurationMin > 10 ? Color.FromRgb(230, 81, 0) : Color.FromRgb(158, 158, 158);
                var tb = new TextBlock { Text = $"{ss.DurationMin}m", FontSize = 7, Foreground = new SolidColorBrush(clr), FontWeight = FontWeights.SemiBold };
                Canvas.SetLeft(tb, x); Canvas.SetTop(tb, 0); StopMarksCanvas.Children.Add(tb);
            }

            // Przystanki kursu — numery z kolorem statusu
            if (_kurs != null)
            {
                foreach (var st in _kurs.Przystanki.Where(s => s.CzasDotarcia.HasValue))
                {
                    var x = (st.CzasDotarcia!.Value.Hour * 60 + st.CzasDotarcia.Value.Minute) / 1440.0 * w;
                    var clr = st.Status == "Obsluzony" ? Color.FromRgb(46, 125, 50) : st.Status == "Pominiety" ? Color.FromRgb(198, 40, 40) : Color.FromRgb(21, 101, 192);
                    var circle = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8),
                        Background = new SolidColorBrush(clr), HorizontalAlignment = HorizontalAlignment.Center };
                    circle.Child = new TextBlock { Text = st.Kolejnosc.ToString(), FontSize = 8, FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    Canvas.SetLeft(circle, x - 8); Canvas.SetTop(circle, 2); StopMarksCanvas.Children.Add(circle);
                }
            }

            // Zakres slidera — pełna doba ale minimum/maximum wg danych
            TrackSlider.Minimum = 0; TrackSlider.Maximum = 1440;
            if (_histPts.Count > 0 && DateTime.TryParse(_histPts.First().Time, out var dtF))
                TrackSlider.Value = dtF.Hour * 60 + dtF.Minute;
        }

        private void BuildPrzystankiList()
        {
            PrzystankiPanel.Children.Clear();
            if (_kurs == null) return;

            // ── Godzina wyjazdu z bazy — plan vs faktyczna ──
            // Logika: szukamy postoju przy bazie (< 1km, speed=0 przez min 3 pkt),
            // potem pierwszego ruchu (speed > 5) PO tym postoju i ODDALAJĄCEGO się od bazy.
            // Ignorujemy wcześniejsze przejazdy obok bazy (np. powrót z wczoraj).
            DateTime? faktWyjazd = null;
            DateTime? postojPrzyBazie = null;
            if (_histPts.Count > 0)
            {
                // Krok 1: znajdź postój przy bazie — szukamy w standstills Webfleet
                foreach (var ss in _standStills)
                {
                    if (HavM(ss.Lat, ss.Lon, 51.86857, 19.79476) < 1500 && ss.DurationMin >= 3)
                    {
                        // Bierzemy OSTATNI postój przy bazie przed planowanym wyjazdem (lub najbliższy)
                        if (!string.IsNullOrEmpty(_kurs.GodzWyjazdu))
                        {
                            var planMin = int.TryParse(_kurs.GodzWyjazdu.Split(':')[0], out var h2) && _kurs.GodzWyjazdu.Split(':').Length > 1 && int.TryParse(_kurs.GodzWyjazdu.Split(':')[1], out var m2) ? h2 * 60 + m2 : 0;
                            var ssMin = ss.EndTime.Hour * 60 + ss.EndTime.Minute;
                            // Postój kończy się w okolicach planowanego wyjazdu (max 60 min przed lub 30 min po)
                            if (ssMin >= planMin - 60 && ssMin <= planMin + 30)
                                postojPrzyBazie = ss.EndTime;
                        }
                        else
                        {
                            // Bez planu — bierz najdłuższy postój przy bazie
                            if (postojPrzyBazie == null || ss.DurationMin > 10)
                                postojPrzyBazie = ss.EndTime;
                        }
                    }
                }

                // Krok 2: fallback — szukaj w GPS track postoju przy bazie
                if (postojPrzyBazie == null)
                {
                    int stopCount = 0;
                    for (int i = 0; i < _histPts.Count; i++)
                    {
                        if (HavM(_histPts[i].Lat, _histPts[i].Lng, 51.86857, 19.79476) < 1500 && _histPts[i].Speed <= 2)
                            stopCount++;
                        else
                        {
                            if (stopCount >= 3 && DateTime.TryParse(_histPts[i].Time, out var dt))
                                postojPrzyBazie = dt;
                            stopCount = 0;
                        }
                    }
                }

                // Krok 3: znajdź pierwszy ruch PO postoju przy bazie, oddalający się od bazy
                if (postojPrzyBazie != null)
                {
                    bool afterStop = false;
                    double prevDist = 0;
                    for (int i = 0; i < _histPts.Count; i++)
                    {
                        if (!DateTime.TryParse(_histPts[i].Time, out var dt)) continue;
                        if (dt >= postojPrzyBazie) afterStop = true;
                        if (!afterStop) continue;

                        var dist = HavM(_histPts[i].Lat, _histPts[i].Lng, 51.86857, 19.79476);
                        if (_histPts[i].Speed > 5 && dist > prevDist && dist > 500)
                        { faktWyjazd = dt; break; }
                        prevDist = dist;
                    }
                }

                // Krok 4: last resort — pierwszy ruch > 5 km/h oddalający się od bazy
                if (faktWyjazd == null)
                {
                    double prevD = 0;
                    foreach (var pt in _histPts)
                    {
                        if (!DateTime.TryParse(pt.Time, out var dt)) continue;
                        var d = HavM(pt.Lat, pt.Lng, 51.86857, 19.79476);
                        if (pt.Speed > 5 && d > 1000 && d > prevD) { faktWyjazd = dt; break; }
                        prevD = d;
                    }
                }
            }
            var planWyjazd = _kurs.GodzWyjazdu;
            var wyjazdRow = new Border { Padding = new Thickness(14, 6, 14, 6), Background = new SolidColorBrush(Color.FromRgb(232, 234, 246)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 222, 236)), BorderThickness = new Thickness(0, 0, 0, 1) };
            var wyjazdStack = new StackPanel();
            wyjazdStack.Children.Add(new TextBlock { Text = "WYJAZD Z BAZY", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(26, 35, 126)) });
            var planStr = !string.IsNullOrEmpty(planWyjazd) ? $"Plan: {planWyjazd}" : "Plan: nie ustalono";
            var faktStr = faktWyjazd.HasValue ? $"Faktycznie: {faktWyjazd:HH:mm}" : "Faktycznie: brak danych";
            wyjazdStack.Children.Add(new TextBlock { Text = planStr, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)) });
            if (postojPrzyBazie.HasValue)
                wyjazdStack.Children.Add(new TextBlock { Text = $"Koniec postoju przy bazie: {postojPrzyBazie:HH:mm}", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(144, 164, 174)) });
            if (faktWyjazd.HasValue && !string.IsNullOrEmpty(planWyjazd))
            {
                var planMin = int.TryParse(planWyjazd.Split(':')[0], out var ph) && planWyjazd.Split(':').Length > 1 && int.TryParse(planWyjazd.Split(':')[1], out var pm) ? ph * 60 + pm : 0;
                var faktMin = faktWyjazd.Value.Hour * 60 + faktWyjazd.Value.Minute;
                var diff = faktMin - planMin;
                var diffClr = diff > 15 ? Color.FromRgb(198, 40, 40) : diff > 5 ? Color.FromRgb(230, 81, 0) : Color.FromRgb(46, 125, 50);
                var diffStr = diff > 0 ? $"+{diff} min spóźnienia" : diff < 0 ? $"{diff} min wcześniej" : "punktualnie";
                wyjazdStack.Children.Add(new TextBlock { Text = $"{faktStr} ({diffStr})", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(diffClr) });
            }
            else
                wyjazdStack.Children.Add(new TextBlock { Text = faktStr, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(120, 144, 156)) });
            wyjazdRow.Child = wyjazdStack;
            PrzystankiPanel.Children.Add(wyjazdRow);

            // ── Buduj scaloną listę: przystanki kursu + postoje Webfleet ──
            var allItems = new List<(DateTime? time, string type, object data)>();

            // Przystanki kursu
            foreach (var p in _kurs.Przystanki)
                allItems.Add((p.CzasDotarcia, "kurs", p));

            // Postoje Webfleet (tylko te które NIE pasują do żadnego przystanku kursu)
            foreach (var ss in _standStills)
            {
                bool matchesKurs = false;
                foreach (var p in _kurs.Przystanki)
                {
                    if (p.Lat == 0) continue;
                    if (HavM(ss.Lat, ss.Lon, p.Lat, p.Lon) < 800) { matchesKurs = true; break; }
                }
                if (!matchesKurs && ss.DurationMin >= 2)
                    allItems.Add((ss.StartTime, "postoj", ss));
            }

            // Sortuj po czasie (null na koniec)
            allItems.Sort((a, b) => (a.time ?? DateTime.MaxValue).CompareTo(b.time ?? DateTime.MaxValue));

            foreach (var item in allItems)
            {
                if (item.type == "kurs")
                {
                    var p = (KursMonitorService.MonitorPrzystanek)item.data;
                    PrzystankiPanel.Children.Add(BuildKursPrzystanekRow(p));
                }
                else
                {
                    var ss = (StandStill)item.data;
                    PrzystankiPanel.Children.Add(BuildPostojRow(ss));
                }
            }
        }

        private Border BuildPostojRow(StandStill ss)
        {
            var clr = ss.DurationMin > 30 ? Color.FromRgb(198, 40, 40) : ss.DurationMin > 10 ? Color.FromRgb(230, 81, 0) : Color.FromRgb(158, 158, 158);
            var row = new Border { Padding = new Thickness(14, 4, 14, 4), Background = new SolidColorBrush(Color.FromRgb(255, 253, 248)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(248, 245, 238)), BorderThickness = new Thickness(0, 0, 0, 1), Cursor = Cursors.Hand };
            row.MouseEnter += (_, _) => row.Background = new SolidColorBrush(Color.FromRgb(255, 248, 240));
            row.MouseLeave += (_, _) => row.Background = new SolidColorBrush(Color.FromRgb(255, 253, 248));
            var lat = ss.Lat; var lon = ss.Lon;
            row.MouseLeftButtonUp += async (_, _) => { if (lat != 0) await Js($"map.setView([{F(lat)},{F(lon)}],16)"); };

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

            var ico = new TextBlock { Text = "P", FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(clr), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(ico, 0); g.Children.Add(ico);

            var info = new StackPanel();
            var addrText = !string.IsNullOrEmpty(ss.Address) ? ss.Address : $"{ss.Lat:F4}, {ss.Lon:F4}";
            info.Children.Add(new TextBlock { Text = $"Postój — {addrText}", FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 100, 80)), TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(info, 1); g.Children.Add(info);

            var times = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            times.Children.Add(new TextBlock { Text = $"{ss.StartTime:HH:mm} — {ss.EndTime:HH:mm}", FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)), FontWeight = FontWeights.SemiBold });
            times.Children.Add(new TextBlock { Text = $"{ss.DurationMin} min", FontSize = 9,
                Foreground = new SolidColorBrush(clr), FontWeight = FontWeights.Bold });
            Grid.SetColumn(times, 2); g.Children.Add(times);

            row.Child = g;
            return row;
        }

        private Border BuildKursPrzystanekRow(KursMonitorService.MonitorPrzystanek p)
        {
            var sc = p.Status switch { "Obsluzony" => Color.FromRgb(46, 125, 50), "Dotarl" => Color.FromRgb(21, 101, 192),
                "WDrodze" => Color.FromRgb(255, 152, 0), "Pominiety" => Color.FromRgb(198, 40, 40), _ => Color.FromRgb(158, 158, 158) };

            var row = new Border { Padding = new Thickness(14, 5, 14, 5), BorderBrush = new SolidColorBrush(Color.FromRgb(240, 240, 244)),
                BorderThickness = new Thickness(0, 0, 0, 1), Cursor = Cursors.Hand };
            row.MouseEnter += (_, _) => row.Background = new SolidColorBrush(Color.FromRgb(248, 249, 253));
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
            var lat = p.Lat; var lon = p.Lon;
            row.MouseLeftButtonUp += async (_, _) => { if (lat != 0) await Js($"map.setView([{F(lat)},{F(lon)}],16)"); };

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

            var num = new TextBlock { Text = $"{p.Kolejnosc}.", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(sc), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(num, 0); g.Children.Add(num);
            var ico = new TextBlock { Text = p.StatusIkona, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(ico, 1); g.Children.Add(ico);

            var info = new StackPanel();
            info.Children.Add(new TextBlock { Text = p.NazwaKlienta, FontWeight = FontWeights.Bold, FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(38, 50, 56)), TextTrimming = TextTrimming.CharacterEllipsis });
            info.Children.Add(new TextBlock { Text = $"{p.Adres} | {p.PojemnikiE2} E2", FontSize = 9.5, Foreground = new SolidColorBrush(Color.FromRgb(144, 164, 174)) });
            Grid.SetColumn(info, 2); g.Children.Add(info);

            var pill = new Border { Background = new SolidColorBrush(Color.FromArgb(25, sc.R, sc.G, sc.B)),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 1, 5, 1), VerticalAlignment = VerticalAlignment.Center };
            pill.Child = new TextBlock { Text = p.StatusNazwa, Foreground = new SolidColorBrush(sc), FontSize = 9.5, FontWeight = FontWeights.SemiBold };
            Grid.SetColumn(pill, 3); g.Children.Add(pill);

            var times = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            if (p.CzasDotarcia.HasValue)
            {
                var dotStr = $"{p.CzasDotarcia:HH:mm}";
                if (p.CzasOdjazdu.HasValue) dotStr += $" — {p.CzasOdjazdu:HH:mm}";
                times.Children.Add(new TextBlock { Text = dotStr, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(38, 50, 56)), FontWeight = FontWeights.Bold });
            }
            if (p.CzasPostojuMin > 0)
            {
                var pc = p.CzasPostojuMin > 30 ? Color.FromRgb(198, 40, 40) : p.CzasPostojuMin > 15 ? Color.FromRgb(230, 81, 0) : Color.FromRgb(96, 125, 139);
                times.Children.Add(new TextBlock { Text = $"U klienta {p.CzasPostojuMin} min", FontSize = 9, Foreground = new SolidColorBrush(pc), FontWeight = FontWeights.SemiBold });
            }
            if (p.EtaMin > 0)
                times.Children.Add(new TextBlock { Text = $"ETA ~{p.EtaMin} min", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192)), FontWeight = FontWeights.SemiBold });
            Grid.SetColumn(times, 4); g.Children.Add(times);

            row.Child = g;
            return row;
        }

        // ══════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════

        private string SerializePrzystanki() => JsonConvert.SerializeObject(_kurs?.Przystanki.Select(p => new
        {
            p.Kolejnosc, p.NazwaKlienta, p.Adres, p.Lat, p.Lon, p.Status, p.StatusIkona, p.StatusNazwa,
            p.PojemnikiE2, p.Uwagi, CzasDot = p.CzasDotarcia?.ToString("HH:mm") ?? "", CzasOdj = p.CzasOdjazdu?.ToString("HH:mm") ?? "",
            p.CzasPostojuMin, p.DystansDoKm, p.EtaMin
        }) ?? Enumerable.Empty<object>());

        private async Task<List<TrackPt>> FetchTrack(string obj, string date)
        {
            var url = $"{WfUrl}?account={U(WfAccount)}&apikey={U(WfKey)}&lang=pl&outputformat=json&action=showTracks" +
                $"&objectno={U(obj)}&useISO8601=true&rangefrom_string={date}T00:00:00&rangeto_string={date}T23:59:59";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _auth);
            using var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (body.TrimStart().StartsWith("{")) { var o = JObject.Parse(body); if (o["errorCode"] != null) return new(); }
            var items = JsonConvert.DeserializeObject<List<WfTrk>>(body, _js);
            return items?.Select(t => new TrackPt
            {
                Lat = (t.latitude_mdeg != 0 ? t.latitude_mdeg : t.latitude) / 1e6,
                Lng = (t.longitude_mdeg != 0 ? t.longitude_mdeg : t.longitude) / 1e6,
                Speed = t.speed, Time = t.pos_time ?? ""
            }).ToList() ?? new();
        }

        private async Task<List<StandStill>> FetchStandStills(string obj, string date)
        {
            try
            {
                var url = $"{WfUrl}?account={U(WfAccount)}&apikey={U(WfKey)}&lang=pl&outputformat=json&action=showStandStills" +
                    $"&objectno={U(obj)}&useISO8601=true&rangefrom_string={date}T00:00:00&rangeto_string={date}T23:59:59";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _auth);
                using var res = await _http.SendAsync(req);
                var body = await res.Content.ReadAsStringAsync();
                if (!body.TrimStart().StartsWith("[")) return new();
                var arr = JArray.Parse(body);
                return arr.Select(o => new StandStill
                {
                    StartTime = DateTime.TryParse(o["start_time"]?.ToString(), out var st) ? st : DateTime.MinValue,
                    EndTime = DateTime.TryParse(o["end_time"]?.ToString(), out var et) ? et : DateTime.MinValue,
                    DurationMin = (o["duration"]?.Value<int>() ?? 0) / 60,
                    Lat = (o["latitude"]?.Value<double>() ?? 0) / 1e6,
                    Lon = (o["longitude"]?.Value<double>() ?? 0) / 1e6,
                    Address = o["postext"]?.ToString() ?? ""
                }).Where(s => s.Lat != 0).ToList();
            }
            catch { return new(); }
        }

        private async Task<List<TripInfo>> FetchTrips(string obj, string date)
        {
            try
            {
                var url = $"{WfUrl}?account={U(WfAccount)}&apikey={U(WfKey)}&lang=pl&outputformat=json&action=showTripReportExtern" +
                    $"&objectno={U(obj)}&useISO8601=true&rangefrom_string={date}T00:00:00&rangeto_string={date}T23:59:59";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _auth);
                using var res = await _http.SendAsync(req);
                var body = await res.Content.ReadAsStringAsync();
                if (!body.TrimStart().StartsWith("[")) return new();
                var arr = JArray.Parse(body);
                return arr.Select(o => new TripInfo
                {
                    StartTime = DateTime.TryParse(o["start_time"]?.ToString(), out var st) ? st : DateTime.MinValue,
                    EndTime = DateTime.TryParse(o["end_time"]?.ToString(), out var et) ? et : DateTime.MinValue,
                    StartAddress = o["start_postext"]?.ToString() ?? "",
                    EndAddress = o["end_postext"]?.ToString() ?? "",
                    DistanceKm = (o["distance"]?.Value<double>() ?? 0) / 1000.0,
                    DurationMin = (o["duration"]?.Value<int>() ?? 0) / 60,
                    AvgSpeed = o["avg_speed"]?.Value<int>() ?? 0,
                    MaxSpeed = o["max_speed"]?.Value<int>() ?? 0,
                    DriverName = o["drivername"]?.ToString() ?? ""
                }).ToList();
            }
            catch { return new(); }
        }

        private static double HavM(double a1, double o1, double a2, double o2)
        {
            var dA = (a2 - a1) * Math.PI / 180; var dO = (o2 - o1) * Math.PI / 180;
            var x = Math.Sin(dA / 2) * Math.Sin(dA / 2) + Math.Cos(a1 * Math.PI / 180) * Math.Cos(a2 * Math.PI / 180) * Math.Sin(dO / 2) * Math.Sin(dO / 2);
            return 6371000 * 2 * Math.Atan2(Math.Sqrt(x), Math.Sqrt(1 - x));
        }
        private static string Esc(string? s) => s?.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "").Replace("\r", "") ?? "";
        private static string U(string s) => Uri.EscapeDataString(s);
        private static string TimeOnly(string? s) { if (s == null) return ""; var p = s.Split(' '); return p.Length > 1 ? p[1] : p[0]; }

        // ══════════════════════════════════════════════════════════════════
        // HTML — jedna mapa, dwa tryby
        // ══════════════════════════════════════════════════════════════════

        private string GenHtml() => @"<!DOCTYPE html><html><head>
<meta charset=""utf-8"">
<link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"" integrity=""sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY="" crossorigin=""""/>
<script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"" integrity=""sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo="" crossorigin=""""></script>
<style>
*{margin:0;padding:0;box-sizing:border-box}html,body,#map{width:100%;height:100%}
.stop-mk{width:22px;height:22px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:10px;font-weight:800;border:2px solid #fff;box-shadow:0 1px 5px rgba(0,0,0,.25);color:#fff}
.postoj-mk{width:14px;height:14px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:7px;font-weight:700;border:1.5px solid #fff;box-shadow:0 1px 3px rgba(0,0,0,.2);color:#fff;opacity:.8}
.vh-mk{width:16px;height:16px;background:#1565c0;border-radius:50%;border:2px solid #fff;box-shadow:0 2px 6px rgba(21,101,192,.4)}
.cursor-mk{width:16px;height:16px;background:#6a1b9a;border-radius:50%;border:2px solid #fff;box-shadow:0 2px 6px rgba(106,27,154,.4)}
.cursor-tip{background:rgba(255,255,255,.95)!important;border:1px solid #9575cd!important;border-radius:8px!important;padding:6px 10px!important;font-size:11px!important;color:#263238!important;box-shadow:0 4px 12px rgba(0,0,0,.15)!important;white-space:nowrap!important;line-height:1.4!important}
.cursor-tip:before{border-top-color:#9575cd!important}
.leaflet-popup-content-wrapper{border-radius:8px!important}
</style></head><body><div id=""map""></div>
<script>
var map=L.map('map').setView([51.89,19.92],8);
L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',{attribution:'&copy; OSM',maxZoom:19}).addTo(map);
L.control.scale({imperial:false}).addTo(map);
var layer=L.layerGroup().addTo(map),vhMk=null,curMk=null,curTrail=null,allPts=[];

function sC(s){return s==='Obsluzony'?'#2e7d32':s==='Dotarl'?'#1565c0':s==='WDrodze'?'#ff9800':s==='Pominiety'?'#c62828':'#9e9e9e'}
function spdC(s){return s<=0?'#90a4ae':s<30?'#66bb6a':s<60?'#fdd835':s<90?'#ff9800':'#e53935'}

function showKurs(stops,vLat,vLon,vSpd,vAddr,isLive){
    layer.clearLayers();
    if(!stops||stops.length===0)return;
    var pts=stops.filter(function(s){return s.Lat&&s.Lon}).map(function(s){return[s.Lat,s.Lon]});
    if(pts.length>1)L.polyline(pts,{color:'#90a4ae',weight:3,opacity:.4,dashArray:'8,6'}).addTo(layer);
    stops.forEach(function(s){
        if(!s.Lat||!s.Lon)return;
        var c=sC(s.Status);
        var ic=L.divIcon({className:'',html:'<div class=""stop-mk"" style=""background:'+c+'"">'+s.Kolejnosc+'</div>',iconSize:[22,22],iconAnchor:[11,11]});
        var pop='<b>'+s.Kolejnosc+'. '+(s.NazwaKlienta||'')+'</b><br><span style=""color:'+c+';font-weight:700"">'+s.StatusIkona+' '+s.StatusNazwa+'</span>'
            +(s.Adres?'<br>'+s.Adres:'')+'<br><b>'+s.PojemnikiE2+' E2</b>'
            +(s.CzasDot?' | Dot: '+s.CzasDot:'')+(s.CzasPostojuMin>0?' | '+s.CzasPostojuMin+' min':'')
            +(s.EtaMin>0?'<br><b style=""color:#1565c0"">ETA ~'+s.EtaMin+' min</b>':'');
        L.marker([s.Lat,s.Lon],{icon:ic}).addTo(layer).bindPopup(pop,{maxWidth:260});
    });
    if(vLat&&vLon){
        if(!vhMk)vhMk=L.marker([vLat,vLon],{icon:L.divIcon({className:'',html:'<div class=""vh-mk""></div>',iconSize:[16,16],iconAnchor:[8,8]}),zIndexOffset:9000}).addTo(map);
        else vhMk.setLatLng([vLat,vLon]);
        vhMk.unbindTooltip().bindTooltip(vSpd>0?vSpd+' km/h':'Postój',{permanent:false,direction:'top',offset:[0,-16]});
    }
    var b=[];stops.forEach(function(s){if(s.Lat&&s.Lon)b.push([s.Lat,s.Lon])});
    if(vLat&&vLon)b.push([vLat,vLon]);
    if(b.length>1)map.fitBounds(b,{padding:[30,30]});
}

var curInfo='',allStops=[],allSS=[];

function showHistory(stops,pts,standStills){
    layer.clearLayers();allPts=pts;allStops=stops||[];allSS=standStills||[];
    if(vhMk){map.removeLayer(vhMk);vhMk=null}
    if(curMk){map.removeLayer(curMk);curMk=null}
    if(curTrail){map.removeLayer(curTrail);curTrail=null}
    if(!pts||pts.length<2)return;

    // Trasa kolorowana wg prędkości
    for(var i=0;i<pts.length-1;i++)
        L.polyline([[pts[i].Lat,pts[i].Lng],[pts[i+1].Lat,pts[i+1].Lng]],{color:spdC(pts[i].Speed||0),weight:3,opacity:.65}).addTo(layer);

    // START / KONIEC
    var t0=(pts[0].Time||'').split(' ')[1]||'';
    L.circleMarker([pts[0].Lat,pts[0].Lng],{radius:6,color:'#1565c0',fillColor:'#fff',fillOpacity:1,weight:2}).addTo(layer)
        .bindTooltip('WYJAZD '+t0,{permanent:true,direction:'top',offset:[0,-12]});
    var last=pts[pts.length-1];var tN=(last.Time||'').split(' ')[1]||'';
    L.circleMarker([last.Lat,last.Lng],{radius:6,color:'#c62828',fillColor:'#fff',fillOpacity:1,weight:2}).addTo(layer)
        .bindTooltip('POWRÓT '+tN,{permanent:true,direction:'top',offset:[0,-12]});

    // BAZA
    L.marker([51.86857,19.79476],{icon:L.divIcon({className:'',html:'<div style=""background:#37474f;color:#fff;width:24px;height:24px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:12px;border:2px solid #fff;box-shadow:0 1px 5px rgba(0,0,0,.25)"">&#127981;</div>',iconSize:[24,24],iconAnchor:[12,12]})}).addTo(layer).bindPopup('<b>BAZA — Ubojnia</b>');

    // WSZYSTKIE POSTOJE z Webfleet (szare ikonki z czasem)
    if(standStills)standStills.forEach(function(ss,idx){
        if(!ss.Lat||!ss.Lon)return;
        var durMin=ss.DurationMin||0;
        var startT=ss.StartTime?ss.StartTime.split('T')[1]||ss.StartTime:'';if(startT.length>5)startT=startT.substring(0,5);
        var endT=ss.EndTime?ss.EndTime.split('T')[1]||ss.EndTime:'';if(endT.length>5)endT=endT.substring(0,5);
        var clr=durMin>30?'#c62828':durMin>10?'#e65100':'#78909c';
        var ic=L.divIcon({className:'',html:'<div class=""postoj-mk"" style=""background:'+clr+'"">P</div>',iconSize:[14,14],iconAnchor:[7,7]});
        var pop='<div style=""font-family:Segoe UI;min-width:200px"">'
            +'<div style=""font-weight:700;font-size:13px;color:'+clr+';margin-bottom:6px"">Postój — '+durMin+' min</div>'
            +'<table style=""width:100%;font-size:11px"">'
            +'<tr><td style=""color:#90a4ae"">Początek</td><td style=""text-align:right;font-weight:600"">'+startT+'</td></tr>'
            +'<tr><td style=""color:#90a4ae"">Koniec</td><td style=""text-align:right;font-weight:600"">'+endT+'</td></tr>'
            +(ss.Address?'<tr><td style=""color:#90a4ae"">Adres</td><td style=""text-align:right"">'+ss.Address+'</td></tr>':'')
            +'</table></div>';
        L.marker([ss.Lat,ss.Lon],{icon:ic}).addTo(layer).bindPopup(pop,{maxWidth:260});
    });

    // PRZYSTANKI KURSU — duże numerowane markery
    if(stops)stops.forEach(function(s){
        if(!s.Lat||!s.Lon)return;
        var c=sC(s.Status);
        var ic=L.divIcon({className:'',html:'<div class=""stop-mk"" style=""background:'+c+'"">'+s.Kolejnosc+'</div>',iconSize:[22,22],iconAnchor:[11,11]});
        var pop='<div style=""font-family:Segoe UI;min-width:230px"">'
            +'<div style=""font-size:15px;font-weight:700;color:#1a237e;margin-bottom:4px"">'+s.Kolejnosc+'. '+(s.NazwaKlienta||'')+'</div>'
            +'<div style=""padding:3px 10px;border-radius:10px;background:'+c+'22;color:'+c+';font-weight:700;font-size:11px;display:inline-block;margin-bottom:6px"">'+s.StatusIkona+' '+s.StatusNazwa+'</div>'
            +(s.Adres?'<div style=""color:#546e7a;font-size:11px;margin:4px 0"">'+s.Adres+'</div>':'')
            +'<table style=""width:100%;font-size:11.5px;border-collapse:collapse"">'
            +'<tr><td style=""color:#90a4ae;padding:3px 0"">Towar</td><td style=""text-align:right;font-weight:700"">'+s.PojemnikiE2+' E2</td></tr>'
            +(s.CzasDot?'<tr><td style=""color:#90a4ae;padding:3px 0"">Przyjazd</td><td style=""text-align:right;font-weight:700"">'+s.CzasDot+'</td></tr>':'')
            +(s.CzasOdj?'<tr><td style=""color:#90a4ae;padding:3px 0"">Odjazd</td><td style=""text-align:right;font-weight:700"">'+s.CzasOdj+'</td></tr>':'')
            +(s.CzasPostojuMin>0?'<tr><td style=""color:#90a4ae;padding:3px 0"">Czas u klienta</td><td style=""text-align:right;font-weight:800;color:#e65100"">'+s.CzasPostojuMin+' min</td></tr>':'')
            +'</table>'
            +(s.Uwagi?'<div style=""color:#78909c;font-size:10px;margin-top:4px;font-style:italic"">'+s.Uwagi+'</div>':'')
            +'</div>';
        L.marker([s.Lat,s.Lon],{icon:ic,zIndexOffset:5000}).addTo(layer).bindPopup(pop,{maxWidth:300});
    });

    // Cursor z tooltipem (przesuwającym się razem z pinezką)
    curMk=L.marker([pts[0].Lat,pts[0].Lng],{
        icon:L.divIcon({className:'',html:'<div class=""cursor-mk""></div>',iconSize:[16,16],iconAnchor:[8,8]}),
        draggable:true,zIndexOffset:9999
    }).addTo(map);
    curMk.bindTooltip('',{permanent:true,direction:'top',offset:[0,-14],className:'cursor-tip'});
    curTrail=L.polyline([],{color:'#6a1b9a',weight:3,opacity:.5,dashArray:'4,4'}).addTo(map);

    curMk.on('dragend',function(){
        var ll=curMk.getLatLng(),best=0,bd=1e18;
        for(var k=0;k<allPts.length;k++){var d=(allPts[k].Lat-ll.lat)*(allPts[k].Lat-ll.lat)+(allPts[k].Lng-ll.lng)*(allPts[k].Lng-ll.lng);if(d<bd){bd=d;best=k}}
        moveCursor(best);
    });

    map.fitBounds(layer.getBounds().pad(.06));
    setTimeout(function(){map.invalidateSize()},200);
    moveCursor(0);
}

var planLine=null,faktLine=null;
function showPlanVsFakt(plan,fakt){
    if(planLine){map.removeLayer(planLine);planLine=null}
    if(faktLine){map.removeLayer(faktLine);faktLine=null}
    if(plan&&plan.length>1){
        planLine=L.polyline(plan.map(function(p){return[p.Lat,p.Lng]}),{color:'#1565c0',weight:4,opacity:.7,dashArray:'8,6'}).addTo(map);
        planLine.bindTooltip('Trasa planowana (OSRM)',{sticky:true});
    }
    if(fakt&&fakt.length>1){
        faktLine=L.polyline(fakt.map(function(p){return[p.Lat,p.Lng]}),{color:'#c62828',weight:3,opacity:.6}).addTo(map);
        faktLine.bindTooltip('Trasa faktyczna (GPS)',{sticky:true});
    }
    if(planLine&&faktLine){
        var b=L.featureGroup([planLine,faktLine]);
        map.fitBounds(b.getBounds().pad(.08));
    }
}

function moveCursor(idx,follow){
    if(!allPts||idx<0||idx>=allPts.length)return;
    var p=allPts[idx];
    if(curMk)curMk.setLatLng([p.Lat,p.Lng]);
    var trail=[];for(var i=0;i<=idx;i++)trail.push([allPts[i].Lat,allPts[i].Lng]);
    if(curTrail)curTrail.setLatLngs(trail);

    // Tooltip — kierowca, czas, prędkość, najbliższy przystanek
    var t=(p.Time||'').split(' ')[1]||p.Time||'';
    var spd=p.Speed>0?p.Speed+' km/h':'postój';
    var tip=(typeof curInfo!=='undefined'&&curInfo?'<span style=""color:#5e35b1"">'+curInfo+'</span><br>':'')
        +'<b style=""font-size:13px"">'+t+'</b> | '+spd;

    // Najbliższy przystanek kursu
    if(allStops&&allStops.length>0){
        var nearest=null,nd=1e18;
        for(var j=0;j<allStops.length;j++){
            if(!allStops[j].Lat)continue;
            var dd=Math.pow(p.Lat-allStops[j].Lat,2)+Math.pow(p.Lng-allStops[j].Lon,2);
            if(dd<nd){nd=dd;nearest=allStops[j]}
        }
        if(nearest){
            var distKm=Math.sqrt(nd)*111;
            if(distKm<0.5)tip+='<br><b style=""color:#2e7d32"">U klienta: '+nearest.NazwaKlienta+'</b>';
            else tip+='<br>'+nearest.NazwaKlienta+': '+distKm.toFixed(1)+' km';
        }
    }

    // Najbliższy postój Webfleet
    if(allSS&&allSS.length>0){
        for(var k=0;k<allSS.length;k++){
            var ss=allSS[k];if(!ss.Lat)continue;
            var dd2=Math.pow(p.Lat-ss.Lat,2)+Math.pow(p.Lng-ss.Lon,2);
            if(Math.sqrt(dd2)*111<0.3&&ss.Address)
                {tip+='<br><span style=""color:#78909c;font-size:10px"">'+ss.Address+'</span>';break}
        }
    }

    if(curMk)curMk.setTooltipContent(tip);
    // Kamera podąża za pinezką (jeśli włączone)
    if(follow&&idx%3===0)map.panTo([p.Lat,p.Lng],{animate:false});
}
</script></body></html>";

        // ── Models ──
        private class WfTrk { public double latitude_mdeg{get;set;} public double longitude_mdeg{get;set;} public double latitude{get;set;} public double longitude{get;set;} public int speed{get;set;} public int course{get;set;} public string? pos_time{get;set;} }
        public class TrackPt { public double Lat{get;set;} public double Lng{get;set;} public int Speed{get;set;} public string Time{get;set;}=""; public int Course{get;set;} }
        public class StandStill { public DateTime StartTime{get;set;} public DateTime EndTime{get;set;} public int DurationMin{get;set;} public double Lat{get;set;} public double Lon{get;set;} public string Address{get;set;}=""; }
        public class TripInfo { public DateTime StartTime{get;set;} public DateTime EndTime{get;set;} public string StartAddress{get;set;}=""; public string EndAddress{get;set;}=""; public double DistanceKm{get;set;} public int DurationMin{get;set;} public int AvgSpeed{get;set;} public int MaxSpeed{get;set;} public string DriverName{get;set;}=""; }
    }
}
