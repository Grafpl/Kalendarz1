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
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Kalendarz1.MapaFloty
{
    public partial class HistoriaPojazdowWindow : Window
    {
        private static readonly string WfAccount = "942879", WfUser = "Administrator", WfPass = "kaazZVY5";
        private static readonly string WfKey = "7a538868-96cf-4149-a9db-6e090de7276c";
        private static readonly string WfUrl = "https://csv.webfleet.com/extern";
        private static readonly string _auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{WfUser}:{WfPass}"));
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
        private static readonly JsonSerializerSettings _js = new()
        { MissingMemberHandling = MissingMemberHandling.Ignore, Error = (_, a) => { a.ErrorContext.Handled = true; } };

        private readonly List<MapaFlotyView.VehiclePosition> _vehicles;
        private List<TrackPt> _pts = new();
        private TrackAnalysis? _analysis;
        private string _mapFolder = "";
        private bool _ready;
        private DispatcherTimer? _playTimer;
        private bool _playing;
        private int _playSpeed = 10;
        private readonly int[] _speeds = { 5, 10, 25, 50, 100 };
        private bool _sliderDragging;

        public HistoriaPojazdowWindow(List<MapaFlotyView.VehiclePosition> vehicles)
        {
            InitializeComponent();
            _vehicles = vehicles;
            try { WindowIconHelper.SetIcon(this); } catch { }
            DatePick.SelectedDate = DateTime.Today;
            foreach (var v in vehicles.OrderBy(v => v.ObjectName))
                VehicleCombo.Items.Add(new ComboBoxItem { Content = $"{v.ObjectName} — {v.Driver}", Tag = v.ObjectNo });
            if (VehicleCombo.Items.Count > 0) VehicleCombo.SelectedIndex = 0;
            Loaded += async (_, _) => await InitWebView();
            Closed += (_, _) => { _playTimer?.Stop(); CleanupTemp(); };
        }

        // ══════════════════════════════════════════════════════════════════
        // WebView2
        // ══════════════════════════════════════════════════════════════════

        private async Task InitWebView()
        {
            try
            {
                await HistoryWebView.EnsureCoreWebView2Async();
                _mapFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FlotyHist_" + Guid.NewGuid().ToString("N")[..8]);
                Directory.CreateDirectory(_mapFolder);
                File.WriteAllText(System.IO.Path.Combine(_mapFolder, "h.html"), GenHtml(), Encoding.UTF8);
                var cw = HistoryWebView.CoreWebView2;
                var et = cw.GetType().Assembly.GetType("Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind")!;
                cw.GetType().GetMethod("SetVirtualHostNameToFolderMapping")!
                    .Invoke(cw, new object[] { "hist.local", _mapFolder, Enum.Parse(et, "Allow") });
                HistoryWebView.NavigationCompleted += (_, a) => { _ready = a.IsSuccess; };
                cw.Navigate("https://hist.local/h.html");
            }
            catch (Exception ex) { StatusLabel.Text = $"Błąd: {ex.Message}"; }
        }

        private void CleanupTemp()
        { try { if (Directory.Exists(_mapFolder)) Directory.Delete(_mapFolder, true); } catch { } }

        private async Task Js(string code)
        { if (_ready) await HistoryWebView.CoreWebView2.ExecuteScriptAsync(code); }

        // ══════════════════════════════════════════════════════════════════
        // Ładowanie trasy
        // ══════════════════════════════════════════════════════════════════

        private async void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            if (VehicleCombo.SelectedItem is not ComboBoxItem item) return;
            var objectNo = item.Tag?.ToString() ?? "";
            var date = DatePick.SelectedDate ?? DateTime.Today;
            var dateStr = date.ToString("yyyy-MM-dd");

            try
            {
                StopPlay();
                StatusLabel.Text = $"Pobieranie {objectNo} z {dateStr}...";
                _pts = await FetchTrack(objectNo, dateStr);
                if (_pts.Count == 0) { StatusLabel.Text = "Brak danych GPS"; return; }

                _analysis = Analyze(_pts);
                var ptsJson = JsonConvert.SerializeObject(_pts);
                var aJson = JsonConvert.SerializeObject(_analysis);
                var name = item.Content?.ToString() ?? objectNo;
                await Js($"loadTrack({ptsJson},{aJson},'{Esc(name)}','{Esc(dateStr)}')");

                // WPF kontrolki
                TrackSlider.Maximum = _pts.Count - 1;
                TrackSlider.Value = 0;
                TimeStart.Text = TimeOnly(_pts.First().Time);
                TimeEnd.Text = TimeOnly(_pts.Last().Time);

                BuildStats(_analysis);
                BuildTimeline(_pts);
                BuildSegments(_analysis);
                BuildScore(_analysis, _pts);

                StatsBar.Visibility = Visibility.Visible;
                ScrubberBar.Visibility = Visibility.Visible;
                SegmentsBar.Visibility = Visibility.Visible;
                UpdateScrubInfo(0);
                StatusLabel.Text = $"{_pts.Count} pkt | {_analysis.TotalKm:F1} km | max {_analysis.MaxSpeed} km/h";
            }
            catch (Exception ex) { StatusLabel.Text = $"Błąd: {ex.Message}"; }
        }

        // ══════════════════════════════════════════════════════════════════
        // Slider WPF → JS
        // ══════════════════════════════════════════════════════════════════

        private void TrackSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var idx = (int)TrackSlider.Value;
            if (_pts.Count == 0 || idx >= _pts.Count) return;
            UpdateScrubInfo(idx);
            _ = Js($"moveCursor({idx})");
        }

        private void UpdateScrubInfo(int idx)
        {
            if (idx < 0 || idx >= _pts.Count) return;
            var p = _pts[idx];
            var t = TimeOnly(p.Time);
            var spd = p.Speed > 0 ? $"{p.Speed} km/h" : "postój";
            ScrubInfo.Text = $"{idx + 1} / {_pts.Count}  |  {t}  |  {spd}";
        }

        // ══════════════════════════════════════════════════════════════════
        // Play / kontrolki
        // ══════════════════════════════════════════════════════════════════

        private void BtnPlay_Click(object s, RoutedEventArgs e)
        {
            if (_playing) { StopPlay(); return; }
            _playing = true;
            BtnPlay.Content = "\u23F8";
            _playTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / _playSpeed) };
            _playTimer.Tick += (_, _) =>
            {
                if (TrackSlider.Value >= TrackSlider.Maximum) { StopPlay(); return; }
                TrackSlider.Value += 1;
            };
            _playTimer.Start();
        }

        private void StopPlay()
        {
            _playing = false;
            _playTimer?.Stop();
            BtnPlay.Content = "\u25B6";
        }

        private void BtnStepBack10_Click(object s, RoutedEventArgs e) => TrackSlider.Value = Math.Max(0, TrackSlider.Value - 10);
        private void BtnStepBack1_Click(object s, RoutedEventArgs e) => TrackSlider.Value = Math.Max(0, TrackSlider.Value - 1);
        private void BtnStepFwd1_Click(object s, RoutedEventArgs e) => TrackSlider.Value = Math.Min(TrackSlider.Maximum, TrackSlider.Value + 1);
        private void BtnStepFwd10_Click(object s, RoutedEventArgs e) => TrackSlider.Value = Math.Min(TrackSlider.Maximum, TrackSlider.Value + 10);
        private void BtnSpeed_Click(object s, RoutedEventArgs e)
        {
            var i = Array.IndexOf(_speeds, _playSpeed);
            _playSpeed = _speeds[(i + 1) % _speeds.Length];
            BtnSpeedLabel.Content = $"x{_playSpeed}";
            if (_playTimer != null) _playTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / _playSpeed);
        }

        // ══════════════════════════════════════════════════════════════════
        // Stats bar (WPF)
        // ══════════════════════════════════════════════════════════════════

        private void BuildStats(TrackAnalysis a)
        {
            StatsPanel.Children.Clear();
            void Add(string val, string lbl, bool warn = false)
            {
                var border = new Border { Padding = new Thickness(14, 6, 14, 6) };
                var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                stack.Children.Add(new TextBlock { Text = val, FontSize = 18, FontWeight = FontWeights.ExtraBold,
                    Foreground = new SolidColorBrush(warn ? Color.FromRgb(198, 40, 40) : Color.FromRgb(26, 35, 126)),
                    HorizontalAlignment = HorizontalAlignment.Center });
                stack.Children.Add(new TextBlock { Text = lbl, FontSize = 8.5, Foreground = new SolidColorBrush(Color.FromRgb(144, 164, 174)),
                    HorizontalAlignment = HorizontalAlignment.Center });
                border.Child = stack;
                StatsPanel.Children.Add(border);
            }
            Add($"{a.TotalKm} km", "Dystans");
            Add($"{a.AvgSpeed}", "Śr. km/h");
            Add($"{a.MaxSpeed}", "Max km/h", a.MaxSpeed > 90);
            Add($"{a.DrivingPct}%", "Jazda");
            Add($"{a.TotalPoints}", "Pkt GPS");
            Add($"{a.Stops.Count}", "Postojów");
            Add($"{a.OverSpeedCount}", ">90 km/h", a.OverSpeedCount > 0);
            Add($"{a.HarshBrakingCount}", "Ostre ham.", a.HarshBrakingCount > 0);
        }

        // ══════════════════════════════════════════════════════════════════
        // Timeline bar (kolorowy pasek WPF Canvas)
        // ══════════════════════════════════════════════════════════════════

        private void BuildTimeline(List<TrackPt> pts)
        {
            TimelineCanvas.Children.Clear();
            if (pts.Count < 2) return;
            double w = TimelineCanvas.ActualWidth > 10 ? TimelineCanvas.ActualWidth : 800;
            double step = w / pts.Count;
            for (int i = 0; i < pts.Count; i++)
            {
                var spd = pts[i].Speed;
                Color c = spd <= 0 ? Color.FromRgb(255, 183, 77)   // postój
                    : spd < 30 ? Color.FromRgb(102, 187, 106)
                    : spd < 60 ? Color.FromRgb(255, 238, 88)
                    : spd < 90 ? Color.FromRgb(255, 152, 0)
                    : Color.FromRgb(229, 57, 53);                   // >90
                var rect = new Rectangle { Width = Math.Max(step, 1), Height = 14, Fill = new SolidColorBrush(c) };
                Canvas.SetLeft(rect, i * step);
                TimelineCanvas.Children.Add(rect);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Segmenty (WPF lista)
        // ══════════════════════════════════════════════════════════════════

        private void BuildSegments(TrackAnalysis a)
        {
            SegmentsList.Children.Clear();
            var all = new List<(string type, string startTime, string endTime, string detail, double lat, double lng, double lat2, double lng2)>();
            foreach (var s in a.Segments)
                all.Add(("d", s.StartTime, s.EndTime, $"{s.Km} km  |  śr. {s.AvgSpeed}  |  max {s.MaxSpeed} km/h",
                    s.StartLat, s.StartLng, s.EndLat, s.EndLng));
            foreach (var s in a.Stops)
                all.Add(("s", s.StartTime, s.EndTime, $"{s.DurationMin} min postoju",
                    s.Lat, s.Lng, 0, 0));
            all.Sort((x, y) => string.Compare(x.startTime, y.startTime, StringComparison.Ordinal));

            foreach (var item in all)
            {
                var isStop = item.type == "s";
                var border = new Border
                {
                    Padding = new Thickness(12, 8, 12, 8),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(240, 240, 244)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                border.MouseEnter += (_, _) => border.Background = new SolidColorBrush(Color.FromRgb(245, 246, 250));
                border.MouseLeave += (_, _) => border.Background = Brushes.Transparent;
                var lat1 = item.lat; var lng1 = item.lng; var lat2 = item.lat2; var lng2 = item.lng2;
                border.MouseLeftButtonUp += async (_, _) =>
                {
                    if (lat2 != 0) await Js($"map.fitBounds([[{F(lat1)},{F(lng1)}],[{F(lat2)},{F(lng2)}]],{{padding:[40,40]}})");
                    else await Js($"map.setView([{F(lat1)},{F(lng1)}],16)");
                };

                var stack = new StackPanel();
                var row1 = new DockPanel();
                var pill = new Border
                {
                    Background = new SolidColorBrush(isStop ? Color.FromRgb(255, 243, 224) : Color.FromRgb(232, 245, 233)),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 2, 8, 2)
                };
                pill.Child = new TextBlock
                {
                    Text = isStop ? "Postój" : "Jazda", FontSize = 10, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(isStop ? Color.FromRgb(230, 81, 0) : Color.FromRgb(46, 125, 50))
                };
                row1.Children.Add(pill);
                var timeText = new TextBlock
                {
                    Text = $"{TimeOnly(item.startTime)} — {TimeOnly(item.endTime)}",
                    FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(84, 110, 122)),
                    HorizontalAlignment = HorizontalAlignment.Right, FontWeight = FontWeights.SemiBold
                };
                DockPanel.SetDock(timeText, Dock.Right);
                row1.Children.Insert(0, timeText);
                stack.Children.Add(row1);
                stack.Children.Add(new TextBlock
                {
                    Text = item.detail, FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(120, 144, 156)),
                    Margin = new Thickness(0, 3, 0, 0)
                });
                border.Child = stack;
                SegmentsList.Children.Add(border);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Ocena stylu jazdy
        // ══════════════════════════════════════════════════════════════════

        private void BuildScore(TrackAnalysis a, List<TrackPt> pts)
        {
            // Punktacja 1-10: mniej wydarzeń = lepiej
            int score = 10;
            score -= Math.Min(a.OverSpeedCount, 3);        // -1 za każde >90, max -3
            score -= Math.Min(a.HarshBrakingCount, 3);     // -1 za ostre hamowanie, max -3
            if (a.MaxSpeed > 120) score -= 2;
            score = Math.Max(1, Math.Min(10, score));

            Color bg, fg;
            string label;
            if (score >= 8) { bg = Color.FromRgb(232, 245, 233); fg = Color.FromRgb(46, 125, 50); label = $"Ocena jazdy: {score}/10 — dobra"; }
            else if (score >= 5) { bg = Color.FromRgb(255, 243, 224); fg = Color.FromRgb(230, 81, 0); label = $"Ocena jazdy: {score}/10 — do poprawy"; }
            else { bg = Color.FromRgb(255, 235, 238); fg = Color.FromRgb(198, 40, 40); label = $"Ocena jazdy: {score}/10 — zła"; }

            ScoreBadge.Background = new SolidColorBrush(bg);
            ScoreText.Text = label;
            ScoreText.Foreground = new SolidColorBrush(fg);
            ScoreBadge.Visibility = Visibility.Visible;
        }

        // ══════════════════════════════════════════════════════════════════
        // GPX Export
        // ══════════════════════════════════════════════════════════════════

        private void BtnExportGpx_Click(object sender, RoutedEventArgs e)
        {
            if (_pts.Count == 0) { StatusLabel.Text = "Najpierw załaduj trasę"; return; }
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                sb.AppendLine("<gpx version=\"1.1\" creator=\"MapaFloty\">");
                sb.AppendLine("<trk><name>Trasa</name><trkseg>");
                foreach (var p in _pts)
                {
                    sb.AppendLine($"<trkpt lat=\"{F(p.Lat)}\" lon=\"{F(p.Lng)}\">");
                    if (!string.IsNullOrEmpty(p.Time)) sb.AppendLine($"<time>{p.Time.Replace(" ", "T")}Z</time>");
                    sb.AppendLine($"<speed>{p.Speed}</speed>");
                    sb.AppendLine("</trkpt>");
                }
                sb.AppendLine("</trkseg></trk></gpx>");
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    $"Trasa_{DatePick.SelectedDate:yyyyMMdd}_{DateTime.Now:HHmmss}.gpx");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                StatusLabel.Text = $"GPX zapisany: {path}";
            }
            catch (Exception ex) { StatusLabel.Text = $"Błąd GPX: {ex.Message}"; }
        }

        // ══════════════════════════════════════════════════════════════════
        // Webfleet API
        // ══════════════════════════════════════════════════════════════════

        private async Task<List<TrackPt>> FetchTrack(string obj, string date)
        {
            var url = $"{WfUrl}?account={U(WfAccount)}&apikey={U(WfKey)}&lang=pl&outputformat=json&action=showTracks" +
                $"&objectno={U(obj)}&useISO8601=true&rangefrom_string={date}T00:00:00&rangeto_string={date}T23:59:59";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _auth);
            using var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();
            var resp = await res.Content.ReadAsStringAsync();
            if (resp.TrimStart().StartsWith("{"))
            { var o = JObject.Parse(resp); if (o["errorCode"] != null) throw new Exception($"Webfleet: {o["errorMsg"]}"); }
            var items = JsonConvert.DeserializeObject<List<WfTrk>>(resp, _js);
            return items?.Select(t => new TrackPt
            {
                Lat = (t.latitude_mdeg != 0 ? t.latitude_mdeg : t.latitude) / 1e6,
                Lng = (t.longitude_mdeg != 0 ? t.longitude_mdeg : t.longitude) / 1e6,
                Speed = t.speed, Time = t.pos_time ?? "", Course = t.course
            }).ToList() ?? new();
        }

        // ══════════════════════════════════════════════════════════════════
        // Analiza
        // ══════════════════════════════════════════════════════════════════

        private TrackAnalysis Analyze(List<TrackPt> pts)
        {
            double km = 0; int maxSpd = 0; int harshBraking = 0;
            var segs = new List<TripSegment>(); var stops = new List<StopInfo>();

            for (int i = 1; i < pts.Count; i++)
            {
                km += Hav(pts[i - 1].Lat, pts[i - 1].Lng, pts[i].Lat, pts[i].Lng);
                if (pts[i].Speed > maxSpd) maxSpd = pts[i].Speed;
                if (pts[i - 1].Speed - pts[i].Speed > 30) harshBraking++;
            }

            int start = 0; bool drv = pts.Count > 0 && pts[0].Speed > 0;
            for (int i = 1; i <= pts.Count; i++)
            {
                bool now = i < pts.Count && pts[i].Speed > 0;
                if (now != drv || i == pts.Count)
                {
                    var sp = pts.GetRange(start, i - start);
                    if (sp.Count >= 2)
                    {
                        double sk = 0; int sm = 0;
                        for (int j = 1; j < sp.Count; j++)
                        { sk += Hav(sp[j - 1].Lat, sp[j - 1].Lng, sp[j].Lat, sp[j].Lng); if (sp[j].Speed > sm) sm = sp[j].Speed; }

                        if (drv)
                            segs.Add(new TripSegment
                            {
                                Type = "d", StartTime = sp.First().Time, EndTime = sp.Last().Time,
                                Km = Math.Round(sk, 2), MaxSpeed = sm,
                                AvgSpeed = (int)sp.Where(p => p.Speed > 0).Select(p => p.Speed).DefaultIfEmpty(0).Average(),
                                Points = sp.Count,
                                StartLat = sp.First().Lat, StartLng = sp.First().Lng,
                                EndLat = sp.Last().Lat, EndLng = sp.Last().Lng
                            });
                        else
                            stops.Add(new StopInfo
                            {
                                StartTime = sp.First().Time, EndTime = sp.Last().Time,
                                Lat = sp.First().Lat, Lng = sp.First().Lng,
                                Points = sp.Count, DurationMin = Mins(sp.First().Time, sp.Last().Time)
                            });
                    }
                    start = i; drv = now;
                }
            }
            var dp = pts.Count(p => p.Speed > 0);
            return new TrackAnalysis
            {
                TotalKm = Math.Round(km, 1), MaxSpeed = maxSpd,
                AvgSpeed = (int)pts.Where(p => p.Speed > 0).Select(p => p.Speed).DefaultIfEmpty(0).Average(),
                TotalPoints = pts.Count, DrivingPct = pts.Count > 0 ? (int)(dp * 100.0 / pts.Count) : 0,
                TimeFrom = pts.FirstOrDefault()?.Time ?? "", TimeTo = pts.LastOrDefault()?.Time ?? "",
                Segments = segs, Stops = stops, OverSpeedCount = pts.Count(p => p.Speed > 90),
                HarshBrakingCount = harshBraking,
                TotalDrivingMin = Mins(segs.FirstOrDefault()?.StartTime ?? "", segs.LastOrDefault()?.EndTime ?? ""),
                TotalStopMin = stops.Sum(s => s.DurationMin)
            };
        }

        private int Mins(string a, string b)
        { try { if (DateTime.TryParse(a, out var d1) && DateTime.TryParse(b, out var d2)) return (int)(d2 - d1).TotalMinutes; } catch { } return 0; }
        private static double Hav(double a1, double o1, double a2, double o2)
        {
            const double R = 6371; var dA = (a2 - a1) * Math.PI / 180; var dO = (o2 - o1) * Math.PI / 180;
            var x = Math.Sin(dA / 2) * Math.Sin(dA / 2) + Math.Cos(a1 * Math.PI / 180) * Math.Cos(a2 * Math.PI / 180) * Math.Sin(dO / 2) * Math.Sin(dO / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(x), Math.Sqrt(1 - x));
        }
        private static string Esc(string? s) => s?.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "").Replace("\r", "") ?? "";
        private static string F(double d) => d.ToString("F6", CultureInfo.InvariantCulture);
        private static string U(string s) => Uri.EscapeDataString(s);
        private static string TimeOnly(string? s) { if (s == null) return ""; var p = s.Split(' '); return p.Length > 1 ? p[1] : p[0]; }

        // ══════════════════════════════════════════════════════════════════
        // HTML — tylko mapa Leaflet (kontrolki w WPF)
        // ══════════════════════════════════════════════════════════════════

        private string GenHtml() => @"<!DOCTYPE html><html><head>
<meta charset=""utf-8"">
<link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"" integrity=""sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY="" crossorigin=""""/>
<script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"" integrity=""sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo="" crossorigin=""""></script>
<style>
*{margin:0;padding:0;box-sizing:border-box}
html,body,#map{width:100%;height:100%}
.stop-mk{background:#e65100;color:#fff;width:28px;height:28px;border-radius:50%;
    display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:700;
    border:2.5px solid #fff;box-shadow:0 2px 8px rgba(0,0,0,.35)}
.cursor-mk{width:26px;height:26px;background:#1565c0;border-radius:50%;border:3px solid #fff;
    box-shadow:0 3px 12px rgba(21,101,192,.5)}
.arrow-mk{color:#546e7a;font-size:16px;font-weight:900;text-shadow:0 1px 3px rgba(0,0,0,.3)}
.leaflet-popup-content-wrapper{border-radius:10px!important;box-shadow:0 4px 16px rgba(0,0,0,.15)!important}
</style></head><body>
<div id=""map""></div>
<script>
var map=L.map('map').setView([51.89,19.92],8);
L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',{attribution:'&copy; OSM',maxZoom:19}).addTo(map);
L.control.scale({imperial:false}).addTo(map);

var trackLayer=L.layerGroup().addTo(map);
var cursorMarker=null,cursorTrail=null,allPts=[];

function spdC(s){if(s<=0)return'#90a4ae';if(s<30)return'#66bb6a';if(s<60)return'#fdd835';if(s<90)return'#ff9800';return'#e53935'}
function tOnly(s){if(!s)return'';var p=s.split(' ');return p[1]||p[0]}

function loadTrack(pts,analysis,name,dateStr){
    trackLayer.clearLayers();
    if(cursorMarker){map.removeLayer(cursorMarker);cursorMarker=null}
    if(cursorTrail){map.removeLayer(cursorTrail);cursorTrail=null}
    allPts=pts;
    if(!pts||pts.length<2)return;

    // Trasa kolorowana wg prędkości
    for(var i=0;i<pts.length-1;i++){
        L.polyline([[pts[i].Lat,pts[i].Lng],[pts[i+1].Lat,pts[i+1].Lng]],
            {color:spdC(pts[i].Speed||0),weight:5,opacity:.7}).addTo(trackLayer);
    }

    // Strzałki kierunku co ~20 punktów
    var arrowStep=Math.max(1,Math.floor(pts.length/30));
    for(var i=0;i<pts.length;i+=arrowStep){
        if(pts[i].Speed<=0)continue;
        var rot=pts[i].Course||0;
        var ic=L.divIcon({className:'arrow-mk',html:'<span style=""display:inline-block;transform:rotate('+rot+'deg)"">&#x25B2;</span>',iconSize:[16,16],iconAnchor:[8,8]});
        L.marker([pts[i].Lat,pts[i].Lng],{icon:ic,interactive:false}).addTo(trackLayer);
    }

    // Start
    L.circleMarker([pts[0].Lat,pts[0].Lng],{radius:9,color:'#1565c0',fillColor:'#fff',fillOpacity:1,weight:3})
        .addTo(trackLayer).bindTooltip('START '+tOnly(pts[0].Time),{permanent:true,direction:'top',offset:[0,-12]});
    // End
    var last=pts[pts.length-1];
    L.circleMarker([last.Lat,last.Lng],{radius:9,color:'#c62828',fillColor:'#fff',fillOpacity:1,weight:3})
        .addTo(trackLayer).bindTooltip('KONIEC '+tOnly(last.Time),{permanent:true,direction:'top',offset:[0,-12]});

    // Postoje z dokładnymi godzinami
    if(analysis.Stops){
        for(var si=0;si<analysis.Stops.length;si++){
            var st=analysis.Stops[si];
            if(st.DurationMin<1)continue;
            var ic=L.divIcon({className:'',html:'<div class=""stop-mk"">'+(si+1)+'</div>',iconSize:[28,28],iconAnchor:[14,14]});
            var html='<div style=""font-family:Segoe UI;min-width:200px"">'
                +'<div style=""font-weight:700;font-size:15px;color:#e65100;margin-bottom:8px"">Postój #'+(si+1)+'</div>'
                +'<table style=""width:100%;font-size:12px;border-collapse:collapse"">'
                +'<tr><td style=""color:#78909c;padding:3px 0"">Czas trwania</td><td style=""text-align:right;font-weight:700"">'+st.DurationMin+' min</td></tr>'
                +'<tr><td style=""color:#78909c;padding:3px 0"">Początek</td><td style=""text-align:right;font-weight:600"">'+tOnly(st.StartTime)+'</td></tr>'
                +'<tr><td style=""color:#78909c;padding:3px 0"">Koniec</td><td style=""text-align:right;font-weight:600"">'+tOnly(st.EndTime)+'</td></tr>'
                +'<tr><td style=""color:#78909c;padding:3px 0"">Współrzędne</td><td style=""text-align:right;font-size:10px"">'+st.Lat.toFixed(5)+', '+st.Lng.toFixed(5)+'</td></tr>'
                +'</table></div>';
            L.marker([st.Lat,st.Lng],{icon:ic}).addTo(trackLayer).bindPopup(html,{maxWidth:260});
        }
    }

    // Cursor (draggable)
    cursorMarker=L.marker([pts[0].Lat,pts[0].Lng],{
        icon:L.divIcon({className:'',html:'<div class=""cursor-mk""></div>',iconSize:[26,26],iconAnchor:[13,13]}),
        draggable:true,zIndexOffset:9999
    }).addTo(map);
    cursorTrail=L.polyline([],{color:'#1565c0',weight:3,opacity:.5,dashArray:'6,4'}).addTo(map);

    cursorMarker.on('dragend',function(){
        var ll=cursorMarker.getLatLng(),best=0,bestD=1e18;
        for(var k=0;k<allPts.length;k++){
            var dd=(allPts[k].Lat-ll.lat)*(allPts[k].Lat-ll.lat)+(allPts[k].Lng-ll.lng)*(allPts[k].Lng-ll.lng);
            if(dd<bestD){bestD=dd;best=k}
        }
        moveCursor(best);
        // Sync slider w WPF — nie mamy bezpośredniego dostępu, ale cursor się ustawi
    });

    map.fitBounds(trackLayer.getBounds().pad(.06));
    setTimeout(function(){map.invalidateSize()},200);
}

function moveCursor(idx){
    if(!allPts||idx<0||idx>=allPts.length)return;
    var p=allPts[idx];
    if(cursorMarker)cursorMarker.setLatLng([p.Lat,p.Lng]);
    var trail=[];for(var i=0;i<=idx;i++)trail.push([allPts[i].Lat,allPts[i].Lng]);
    if(cursorTrail)cursorTrail.setLatLngs(trail);
    // Auto-pan co 5 kroków
    if(idx%5===0&&cursorMarker)map.panTo([p.Lat,p.Lng],{animate:false});
}
</script></body></html>";

        // ══════════════════════════════════════════════════════════════════
        // Models
        // ══════════════════════════════════════════════════════════════════

        private class WfTrk
        {
            public double latitude_mdeg { get; set; }
            public double longitude_mdeg { get; set; }
            public double latitude { get; set; }
            public double longitude { get; set; }
            public int speed { get; set; }
            public int course { get; set; }
            public string? pos_time { get; set; }
        }

        public class TrackPt
        {
            public double Lat { get; set; }
            public double Lng { get; set; }
            public int Speed { get; set; }
            public string Time { get; set; } = "";
            public int Course { get; set; }
        }

        public class TrackAnalysis
        {
            public double TotalKm { get; set; }
            public int MaxSpeed { get; set; }
            public int AvgSpeed { get; set; }
            public int TotalPoints { get; set; }
            public int DrivingPct { get; set; }
            public string TimeFrom { get; set; } = "";
            public string TimeTo { get; set; } = "";
            public int OverSpeedCount { get; set; }
            public int HarshBrakingCount { get; set; }
            public int TotalDrivingMin { get; set; }
            public int TotalStopMin { get; set; }
            public List<TripSegment> Segments { get; set; } = new();
            public List<StopInfo> Stops { get; set; } = new();
        }

        public class TripSegment
        {
            public string Type { get; set; } = "";
            public string StartTime { get; set; } = "";
            public string EndTime { get; set; } = "";
            public double Km { get; set; }
            public int MaxSpeed { get; set; }
            public int AvgSpeed { get; set; }
            public int Points { get; set; }
            public double StartLat { get; set; }
            public double StartLng { get; set; }
            public double EndLat { get; set; }
            public double EndLng { get; set; }
        }

        public class StopInfo
        {
            public string StartTime { get; set; } = "";
            public string EndTime { get; set; } = "";
            public double Lat { get; set; }
            public double Lng { get; set; }
            public int Points { get; set; }
            public int DurationMin { get; set; }
        }
    }
}
