using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Kalendarz1.MapaFloty
{
    public partial class OsCzasuFlotyWindow : Window
    {
        private static readonly string WfAccount = "942879", WfUser = "Administrator", WfPass = "kaazZVY5";
        private static readonly string WfKey = "7a538868-96cf-4149-a9db-6e090de7276c";
        private static readonly string WfUrl = "https://csv.webfleet.com/extern";
        private static readonly string _auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{WfUser}:{WfPass}"));
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly string _conn = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const double BazaLat = 51.86857, BazaLon = 19.79476, BazaR = 1500;
        private readonly StringBuilder _debugLog = new();

        public OsCzasuFlotyWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            DateFrom.SelectedDate = DateTime.Today;
            DateTo.SelectedDate = DateTime.Today;
        }

        private void BtnDebug_Click(object s, RoutedEventArgs e)
        {
            var text = _debugLog.Length > 0 ? _debugLog.ToString() : "Brak logów — załaduj dane";
            var r = MessageBox.Show(text + "\n\nSkopiować do schowka?", "Debug — Oś czasu",
                MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (r == MessageBoxResult.Yes) Clipboard.SetText(text);
        }

        private void Log(string m) => _debugLog.AppendLine(m);

        private void BtnToday_Click(object s, RoutedEventArgs e) { DateFrom.SelectedDate = DateTime.Today; DateTo.SelectedDate = DateTime.Today; _ = LoadData(); }
        private void BtnWeek_Click(object s, RoutedEventArgs e) { DateFrom.SelectedDate = DateTime.Today.AddDays(-6); DateTo.SelectedDate = DateTime.Today; _ = LoadData(); }
        private void BtnLoad_Click(object s, RoutedEventArgs e) => _ = LoadData();

        private async Task LoadData()
        {
            var from = DateFrom.SelectedDate ?? DateTime.Today;
            var to = DateTo.SelectedDate ?? DateTime.Today;
            if (to < from) to = from;
            var days = (to - from).Days + 1;
            StatusText.Text = $"Ładowanie {days} dni...";
            TimelinePanel.Children.Clear();
            _debugLog.Clear();
            Log($"Zakres: {from:dd.MM.yyyy} — {to:dd.MM.yyyy} ({days} dni)");
            DetailPanel.Visibility = Visibility.Collapsed;

            try
            {
                // Pobierz UNIKALNE zmapowane pojazdy
                var vehicles = new Dictionary<string, (string objectNo, string name, string rejestracja)>();
                using (var conn = new SqlConnection(_conn))
                {
                    await conn.OpenAsync();
                    using var chk = conn.CreateCommand();
                    chk.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name='WebfleetVehicleMapping'";
                    if (Convert.ToInt32(await chk.ExecuteScalarAsync()) == 0) { StatusText.Text = "Brak mapowań"; return; }

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"SELECT DISTINCT m.WebfleetObjectNo, ISNULL(m.WebfleetObjectName,'') AS Name, ISNULL(p.Rejestracja,'') AS Rej
                        FROM WebfleetVehicleMapping m INNER JOIN Pojazd p ON p.PojazdID=m.PojazdID
                        WHERE m.WebfleetObjectNo IS NOT NULL AND m.PojazdID IS NOT NULL AND p.Aktywny=1";
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        var obj = r["WebfleetObjectNo"]?.ToString() ?? "";
                        if (!vehicles.ContainsKey(obj))
                            vehicles[obj] = (obj, r["Name"]?.ToString() ?? "", r["Rej"]?.ToString() ?? "");
                    }
                }
                if (vehicles.Count == 0) { StatusText.Text = "Brak pojazdów"; return; }

                // Pobierz WSZYSTKIE standstills naraz per pojazd (cały zakres)
                // showStandStills ma limit 6 req/min — robimy 1 request per pojazd na cały zakres
                var fromStr = from.ToString("yyyy-MM-dd");
                var toStr = to.ToString("yyyy-MM-dd");
                Log($"Pobieranie postojów: {vehicles.Count} pojazdów, zakres {fromStr} — {toStr}");

                var allSsMap = new Dictionary<string, List<StandStillItem>>();
                // Pobierz po 3 pojazdy naraz (rate limit 6/min)
                var vehicleList = vehicles.Values.OrderBy(v => v.rejestracja).ToList();
                for (int batch = 0; batch < vehicleList.Count; batch += 3)
                {
                    var chunk = vehicleList.Skip(batch).Take(3).ToList();
                    var chunkTasks = chunk.Select(v => (v, task: FetchStandStills(v.objectNo, fromStr, toStr))).ToList();
                    await Task.WhenAll(chunkTasks.Select(t => t.task));
                    foreach (var (v, task) in chunkTasks)
                        allSsMap[v.objectNo] = task.IsCompletedSuccessfully ? task.Result : new();
                    if (batch + 3 < vehicleList.Count)
                        await Task.Delay(500); // mini-delay między batches
                }

                // Per dzień — wyświetl
                for (var day = from; day <= to; day = day.AddDays(1))
                {
                    var dzien = day.ToString("ddd", new System.Globalization.CultureInfo("pl-PL")).ToUpper();
                    TimelinePanel.Children.Add(new TextBlock
                    {
                        Text = $"{day:dd.MM.yyyy} {dzien}",
                        FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(26, 35, 126)),
                        Margin = new Thickness(0, days > 1 ? 12 : 4, 0, 4)
                    });
                    AddHourHeader();
                    Log($"\n═══ {day:dd.MM.yyyy} {dzien} ═══");

                    foreach (var v in vehicleList)
                    {
                        var displayName = !string.IsNullOrEmpty(v.rejestracja) ? v.rejestracja : v.name;
                        var allSs = allSsMap.TryGetValue(v.objectNo, out var list) ? list : new();

                        // Filtruj postoje na ten dzień (start LUB end w tym dniu)
                        var daySs = allSs.Where(s =>
                            s.Start.Date == day.Date || s.End.Date == day.Date ||
                            (s.Start.Date < day.Date && s.End.Date > day.Date))
                            .Select(s => new StandStillItem
                            {
                                // Obcinaj do granic dnia
                                Start = s.Start < day.Date ? day.Date : s.Start,
                                End = s.End > day.Date.AddDays(1) ? day.Date.AddDays(1).AddSeconds(-1) : s.End,
                                Lat = s.Lat, Lon = s.Lon, Address = s.Address, AtBase = s.AtBase,
                                DurationMin = (int)((s.End < day.Date.AddDays(1) ? s.End : day.Date.AddDays(1)) - (s.Start > day.Date ? s.Start : day.Date)).TotalMinutes
                            }).Where(s => s.DurationMin > 0).ToList();

                        Log($"\n{displayName} (obj={v.objectNo}):");
                        if (daySs.Count == 0) Log("  Brak postojów");
                        foreach (var s in daySs.OrderBy(x => x.Start))
                            Log($"  {(s.AtBase ? "BAZA" : "POSTÓJ")} {s.Start:HH:mm}—{s.End:HH:mm} ({s.DurationMin}min) {s.Address}");

                        TimelinePanel.Children.Add(BuildVehicleRow(displayName, v.objectNo, day, daySs));
                    }
                }

                // Legenda
                AddLegend();

                // Panel dostępności (tylko dla dziś)
                if (to.Date >= DateTime.Today)
                    await BuildAvailabilityPanel(vehicleList, allSsMap);

                StatusText.Text = $"{vehicles.Count} pojazdów × {days} dni";
            }
            catch (Exception ex) { StatusText.Text = $"Błąd: {ex.Message}"; }
        }

        private async Task<List<StandStillItem>> FetchStandStills(string objectNo, string dateFrom, string dateTo)
        {
            try
            {
                var url = $"{WfUrl}?account={U(WfAccount)}&apikey={U(WfKey)}&lang=pl&outputformat=json&action=showStandStills" +
                    $"&objectno={U(objectNo)}&useISO8601=true&rangefrom_string={dateFrom}T00:00:00&rangeto_string={dateTo}T23:59:59";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _auth);
                using var res = await _http.SendAsync(req);
                var body = await res.Content.ReadAsStringAsync();
                if (!body.TrimStart().StartsWith("[")) return new();
                var arr = JArray.Parse(body);
                return arr.Select(o =>
                {
                    DateTime.TryParse(o["start_time"]?.ToString(), out var st);
                    DateTime.TryParse(o["end_time"]?.ToString(), out var et);
                    var lat = (o["latitude"]?.Value<double>() ?? 0) / 1e6;
                    var lon = (o["longitude"]?.Value<double>() ?? 0) / 1e6;
                    return new StandStillItem
                    {
                        Start = st, End = et, Lat = lat, Lon = lon,
                        Address = o["postext"]?.ToString() ?? "",
                        AtBase = HavM(lat, lon, BazaLat, BazaLon) < BazaR,
                        DurationMin = (int)(et - st).TotalMinutes
                    };
                }).Where(s => s.Lat != 0).ToList();
            }
            catch { return new(); }
        }

        private Border BuildVehicleRow(string name, string objectNo, DateTime day, List<StandStillItem> standstills)
        {
            var row = new Border
            {
                Background = Brushes.White, Margin = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(242, 242, 246)), BorderThickness = new Thickness(0, 0, 0, 1),
                Cursor = Cursors.Hand
            };
            row.MouseEnter += (_, _) => row.Background = new SolidColorBrush(Color.FromRgb(248, 249, 253));
            row.MouseLeave += (_, _) => row.Background = Brushes.White;
            var ssRef = standstills; var nameRef = name; var dayRef = day;
            row.MouseLeftButtonUp += (_, _) => ShowDetails(nameRef, dayRef, ssRef);

            var grid = new Grid { Height = 32 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            // Nazwa
            grid.Children.Add(SetCol(new TextBlock { Text = name, FontSize = 10.5, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(38, 50, 56)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 4, 0) }, 0));

            // Canvas oś 24h
            var canvas = new Canvas { ClipToBounds = true };
            grid.Children.Add(SetCol(canvas, 1));

            canvas.Loaded += (_, _) =>
            {
                var w = canvas.ActualWidth; if (w < 10) w = 900;
                canvas.Children.Add(new Rectangle { Width = w, Height = 24, Fill = new SolidColorBrush(Color.FromRgb(248, 248, 250)) });

                // Postoje
                foreach (var ss in standstills)
                {
                    var x1 = (ss.Start.Hour * 60 + ss.Start.Minute) / 1440.0 * w;
                    var x2 = (ss.End.Hour * 60 + ss.End.Minute) / 1440.0 * w;
                    var clr = ss.AtBase ? Color.FromRgb(200, 230, 201) : Color.FromRgb(255, 224, 178);
                    var rect = new Rectangle { Width = Math.Max(x2 - x1, 2), Height = 24, Fill = new SolidColorBrush(clr) };
                    rect.ToolTip = $"{(ss.AtBase ? "BAZA" : "POSTÓJ")} {ss.Start:HH:mm}—{ss.End:HH:mm} ({ss.DurationMin} min)" +
                        (ss.AtBase ? "" : $"\n{ss.Address}");
                    Canvas.SetLeft(rect, x1); canvas.Children.Add(rect);
                }

                // W trasie — TYLKO jeśli są jakiekolwiek postoje (inaczej = brak danych, szare)
                if (standstills.Count > 0)
                {
                    var cov = new bool[1440];
                    foreach (var ss in standstills)
                        for (int m = Math.Max(0, ss.Start.Hour * 60 + ss.Start.Minute); m < Math.Min(1440, ss.End.Hour * 60 + ss.End.Minute); m++) cov[m] = true;

                    // Znajdź zakres aktywności (od pierwszego do ostatniego postoju)
                    var firstMin = standstills.Min(s => s.Start.Hour * 60 + s.Start.Minute);
                    var lastMin = standstills.Max(s => s.End.Hour * 60 + s.End.Minute);

                    int? ts = null;
                    for (int m = firstMin; m <= lastMin; m++)
                    {
                        if (m < 1440 && !cov[m]) { ts ??= m; }
                        else if (ts != null)
                        {
                            if (m - ts.Value > 3)
                            {
                                var x1t = ts.Value / 1440.0 * w; var x2t = m / 1440.0 * w;
                                var tr = new Rectangle { Width = Math.Max(x2t - x1t, 2), Height = 24, Fill = new SolidColorBrush(Color.FromRgb(187, 222, 251)) };
                                tr.ToolTip = $"W TRASIE {ts.Value / 60:D2}:{ts.Value % 60:D2}—{m / 60:D2}:{m % 60:D2} ({m - ts.Value} min)";
                                Canvas.SetLeft(tr, x1t); canvas.Children.Add(tr);
                            }
                            ts = null;
                        }
                    }
                }

                // Godziny
                for (int h = 0; h <= 24; h++)
                    canvas.Children.Add(new Line { X1 = h / 24.0 * w, X2 = h / 24.0 * w, Y1 = 0, Y2 = 24,
                        Stroke = new SolidColorBrush(Color.FromRgb(220, 220, 228)), StrokeThickness = 0.3 });
            };

            if (standstills.Count == 0)
            {
                // Brak danych
                grid.Children.Add(SetCol(new TextBlock { Text = "—", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Brak danych GPS" }, 2));
                grid.Children.Add(SetCol(new TextBlock { Text = "—", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, 3));
            }
            else
            {
                var baseMin = standstills.Where(s => s.AtBase).Sum(s => s.DurationMin);
                var baseClr = baseMin > 600 ? Color.FromRgb(46, 125, 50) : baseMin > 300 ? Color.FromRgb(255, 152, 0) : Color.FromRgb(158, 158, 158);
                grid.Children.Add(SetCol(new TextBlock { Text = $"{baseMin / 60}h{baseMin % 60:D2}",
                    FontSize = 10, Foreground = new SolidColorBrush(baseClr), FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = $"Na bazie: {baseMin} min" }, 2));

                // Czas w trasie = czas między pierwszym a ostatnim postojem minus postoje
                var firstMin = standstills.Min(s => s.Start.Hour * 60 + s.Start.Minute);
                var lastMin = standstills.Max(s => s.End.Hour * 60 + s.End.Minute);
                var activeMin = lastMin - firstMin;
                var stopMin = standstills.Sum(s => s.DurationMin);
                var tripMin = Math.Max(0, activeMin - stopMin);
                var tripClr = tripMin > 300 ? Color.FromRgb(21, 101, 192) : tripMin > 0 ? Color.FromRgb(100, 150, 200) : Color.FromRgb(158, 158, 158);
                grid.Children.Add(SetCol(new TextBlock { Text = $"{tripMin / 60}h{tripMin % 60:D2}",
                    FontSize = 10, Foreground = new SolidColorBrush(tripClr), FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = $"W trasie: {tripMin} min (aktywność {firstMin / 60:D2}:{firstMin % 60:D2}—{lastMin / 60:D2}:{lastMin % 60:D2})" }, 3));
            }

            row.Child = grid;
            return row;
        }

        private void ShowDetails(string name, DateTime day, List<StandStillItem> standstills)
        {
            DetailPanel.Visibility = Visibility.Visible;
            DetailList.Children.Clear();

            var dzien = day.ToString("dddd dd.MM.yyyy", new System.Globalization.CultureInfo("pl-PL"));
            DetailList.Children.Add(new TextBlock { Text = $"{name} — {dzien}", FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(26, 35, 126)), Margin = new Thickness(0, 0, 0, 6) });

            if (standstills.Count == 0)
            {
                DetailList.Children.Add(new TextBlock { Text = "Brak danych GPS dla tego dnia", FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158)), Margin = new Thickness(0, 4, 0, 0) });
                return;
            }

            // Buduj chronologiczną listę: postoje + luki (w trasie) TYLKO między pierwszym a ostatnim wydarzeniem
            var sorted = standstills.OrderBy(s => s.Start).ToList();
            var timeline = new List<(DateTime start, DateTime end, string type, string detail, int durMin)>();

            for (int i = 0; i < sorted.Count; i++)
            {
                var ss = sorted[i];
                // Luka przed tym postojem = jazda (skąd → dokąd)
                if (i > 0)
                {
                    var prev = sorted[i - 1];
                    var prevEnd = prev.End;
                    if (ss.Start > prevEnd.AddMinutes(2))
                    {
                        var tripDur = (int)(ss.Start - prevEnd).TotalMinutes;
                        var fromName = prev.AtBase ? "Baza" : (prev.Address.Length > 30 ? prev.Address[..30] : prev.Address);
                        var toName = ss.AtBase ? "Baza" : (ss.Address.Length > 30 ? ss.Address[..30] : ss.Address);
                        if (string.IsNullOrEmpty(fromName)) fromName = "?";
                        if (string.IsNullOrEmpty(toName)) toName = "?";
                        timeline.Add((prevEnd, ss.Start, "TRASA", $"{fromName} → {toName}", tripDur));
                    }
                }
                // Postój
                var typ = ss.AtBase ? "BAZA" : "POSTÓJ";
                var addr = ss.AtBase ? "Ubojnia Koziołki 40" : ss.Address;
                timeline.Add((ss.Start, ss.End, typ, addr, ss.DurationMin));
            }

            // Wyświetl timeline
            foreach (var ev in timeline)
            {
                var clr = ev.type == "BAZA" ? Color.FromRgb(46, 125, 50) : ev.type == "TRASA" ? Color.FromRgb(21, 101, 192) : Color.FromRgb(230, 81, 0);
                var bgClr = ev.type == "BAZA" ? Color.FromRgb(240, 248, 240) : ev.type == "TRASA" ? Color.FromRgb(237, 246, 255) : Color.FromRgb(255, 248, 238);

                var item = new Border { Background = new SolidColorBrush(bgClr), CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 0, 2) };

                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // czas
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });  // typ
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });  // czas trwania
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // adres

                g.Children.Add(SetCol(new TextBlock { Text = $"{ev.start:HH:mm} — {ev.end:HH:mm}", FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(clr), VerticalAlignment = VerticalAlignment.Center }, 0));

                var typPill = new Border { Background = new SolidColorBrush(Color.FromArgb(30, clr.R, clr.G, clr.B)),
                    CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 1, 6, 1), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                typPill.Child = new TextBlock { Text = ev.type, FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(clr) };
                g.Children.Add(SetCol(typPill, 1));

                var durStr = ev.durMin >= 60 ? $"{ev.durMin / 60}h {ev.durMin % 60}min" : $"{ev.durMin} min";
                g.Children.Add(SetCol(new TextBlock { Text = durStr, FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(84, 110, 122)),
                    FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }, 2));

                if (!string.IsNullOrEmpty(ev.detail))
                    g.Children.Add(SetCol(new TextBlock { Text = ev.detail, FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
                        TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }, 3));

                item.Child = g;
                DetailList.Children.Add(item);
            }

            // Podsumowanie
            var totalBase = standstills.Where(s => s.AtBase).Sum(s => s.DurationMin);
            var totalStop = standstills.Where(s => !s.AtBase).Sum(s => s.DurationMin);
            var totalTrip = timeline.Where(e => e.type == "TRASA").Sum(e => e.durMin);
            var firstTime = sorted.First().Start;
            var lastTime = sorted.Last().End;

            var sumBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(245, 246, 250)), CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 6, 0, 0) };
            var sumStack = new StackPanel { Orientation = Orientation.Horizontal };
            void AddSum(string label, string val, Color c) {
                sumStack.Children.Add(new TextBlock { Text = $"{label}: ", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(120, 144, 156)), VerticalAlignment = VerticalAlignment.Center });
                sumStack.Children.Add(new TextBlock { Text = val, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(c), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center });
            }
            AddSum("Aktywność", $"{firstTime:HH:mm}—{lastTime:HH:mm}", Color.FromRgb(38, 50, 56));
            AddSum("Na bazie", $"{totalBase / 60}h {totalBase % 60}min", Color.FromRgb(46, 125, 50));
            AddSum("W trasie", $"{totalTrip / 60}h {totalTrip % 60}min", Color.FromRgb(21, 101, 192));
            AddSum("Postoje poza bazą", $"{totalStop / 60}h {totalStop % 60}min", Color.FromRgb(230, 81, 0));
            sumBorder.Child = sumStack;
            DetailList.Children.Add(sumBorder);
        }

        // ══════════════════════════════════════════════════════════════════
        // Panel dostępności + sugestie
        // ══════════════════════════════════════════════════════════════════

        private async Task BuildAvailabilityPanel(
            List<(string objectNo, string name, string rejestracja)> vehicles,
            Dictionary<string, List<StandStillItem>> allSsMap)
        {
            AvailPanel.Visibility = Visibility.Visible;
            AvailStats.Children.Clear();
            AvailList.Children.Clear();
            AvailTime.Text = $"Stan na {DateTime.Now:HH:mm}";

            // Pobierz live pozycje GPS
            var svc = new KursMonitorService();
            var liveStatus = new List<(string name, string rej, string status, string detail, DateTime? eta, Color clr, string kursInfo)>();

            // Pobierz dzisiejsze kursy per pojazd (PojazdID → Kurs info)
            var kursyPerPojazd = new Dictionary<int, (string trasa, string kierowca, string godzWyj, int ladunkow, List<string> klienci)>();
            try
            {
                using var conn2 = new SqlConnection(_conn);
                await conn2.OpenAsync();
                using var cmdK = conn2.CreateCommand();
                cmdK.CommandText = @"SELECT k.PojazdID, k.Trasa, k.GodzWyjazdu, CONCAT(ki.Imie,' ',ki.Nazwisko) AS Kierowca,
                    (SELECT COUNT(*) FROM Ladunek l WHERE l.KursID=k.KursID) AS Ladunkow
                    FROM Kurs k LEFT JOIN Kierowca ki ON k.KierowcaID=ki.KierowcaID
                    WHERE k.DataKursu=CAST(GETDATE() AS DATE) AND k.PojazdID IS NOT NULL";
                using var rdK = await cmdK.ExecuteReaderAsync();
                while (await rdK.ReadAsync())
                {
                    var pid = rdK.GetInt32(0);
                    kursyPerPojazd[pid] = (rdK["Trasa"]?.ToString() ?? "", rdK["Kierowca"]?.ToString() ?? "",
                        rdK["GodzWyjazdu"] == DBNull.Value ? "" : ((TimeSpan)rdK["GodzWyjazdu"]).ToString(@"hh\:mm"),
                        Convert.ToInt32(rdK["Ladunkow"]), new List<string>());
                }

                // Pobierz nazwy klientów per kurs
                // Pobierz klientów per kurs z awizacjami z ZamowieniaMieso
                using var connL = new SqlConnection("Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True");
                await connL.OpenAsync();
                using var cmdL = connL.CreateCommand();
                cmdL.CommandText = @"SELECT z.TransportKursID, k2.PojazdID,
                    c.Name AS KlientNazwa, z.DataPrzyjazdu,
                    (SELECT SUM(ISNULL(t.Ilosc,0)) FROM ZamowieniaMiesoTowar t WHERE t.ZamowienieId=z.Id) AS SumaKg
                    FROM ZamowieniaMieso z
                    INNER JOIN [192.168.0.109].TransportPL.dbo.Kurs k2 ON z.TransportKursID=k2.KursID
                    LEFT JOIN [192.168.0.112].Handel.SSCommon.STContractors c ON z.KlientId=c.Id
                    WHERE z.DataUboju=CAST(GETDATE() AS DATE) AND z.TransportKursID IS NOT NULL AND k2.PojazdID IS NOT NULL
                    ORDER BY k2.PojazdID";
                try
                {
                    using var rdL = await cmdL.ExecuteReaderAsync();
                    while (await rdL.ReadAsync())
                    {
                        var pid = Convert.ToInt32(rdL["PojazdID"]);
                        if (!kursyPerPojazd.TryGetValue(pid, out var ki)) continue;
                        var klientNazwa = rdL["KlientNazwa"]?.ToString() ?? "";
                        var awiz = rdL["DataPrzyjazdu"] == DBNull.Value ? "" :
                            ((DateTime)rdL["DataPrzyjazdu"]).Hour > 0 ? ((DateTime)rdL["DataPrzyjazdu"]).ToString("HH:mm") : "";
                        var sumaKg = rdL["SumaKg"] == DBNull.Value ? 0m : Convert.ToDecimal(rdL["SumaKg"]);
                        var info = klientNazwa;
                        if (!string.IsNullOrEmpty(awiz)) info += $" aw.{awiz}";
                        if (sumaKg > 0) info += $" {sumaKg:F0}kg";
                        if (!string.IsNullOrEmpty(info)) ki.klienci.Add(info);
                    }
                }
                catch
                {
                    // Fallback — linked server może nie działać, użyj Ladunek.Uwagi
                    using var cmdL2 = conn2.CreateCommand();
                    cmdL2.CommandText = @"SELECT k.PojazdID, l.Uwagi FROM Ladunek l
                        INNER JOIN Kurs k ON l.KursID=k.KursID
                        WHERE k.DataKursu=CAST(GETDATE() AS DATE) AND k.PojazdID IS NOT NULL
                        ORDER BY k.PojazdID, l.Kolejnosc";
                    using var rdL2 = await cmdL2.ExecuteReaderAsync();
                    while (await rdL2.ReadAsync())
                    {
                        var pid = Convert.ToInt32(rdL2["PojazdID"]);
                        var uwagi = rdL2["Uwagi"]?.ToString() ?? "";
                        if (kursyPerPojazd.TryGetValue(pid, out var ki2) && !string.IsNullOrEmpty(uwagi))
                            ki2.klienci.Add(uwagi);
                    }
                }
            }
            catch { }

            // Mapuj objectNo → PojazdID
            var objToPojazdId = new Dictionary<string, int>();
            try
            {
                using var conn3 = new SqlConnection(_conn);
                await conn3.OpenAsync();
                using var cmdM = conn3.CreateCommand();
                cmdM.CommandText = "SELECT WebfleetObjectNo, PojazdID FROM WebfleetVehicleMapping WHERE PojazdID IS NOT NULL";
                using var rdM = await cmdM.ExecuteReaderAsync();
                while (await rdM.ReadAsync())
                    objToPojazdId[rdM["WebfleetObjectNo"]?.ToString() ?? ""] = Convert.ToInt32(rdM["PojazdID"]);
            }
            catch { }

            // Pobierz LIVE pozycje GPS — wszystkie naraz (to jest źródło prawdy)
            var gpsTasks = vehicles.Select(v => (v, task: svc.PobierzPozycjeAsync(v.objectNo))).ToList();
            try { await Task.WhenAll(gpsTasks.Select(t => t.task)); } catch { }

            foreach (var (v, task) in gpsTasks)
            {
                var displayName = !string.IsNullOrEmpty(v.rejestracja) ? v.rejestracja : v.name;
                string status, detail;
                Color clr;
                DateTime? eta = null;

                var pos = task.IsCompletedSuccessfully ? task.Result : null;

                if (!pos.HasValue || (pos.Value.lat == 0 && pos.Value.lon == 0))
                {
                    status = "BRAK GPS"; detail = "Brak danych"; clr = Color.FromRgb(158, 158, 158);
                }
                else
                {
                    var distToBase = HavM(pos.Value.lat, pos.Value.lon, BazaLat, BazaLon);
                    var speed = pos.Value.speed;
                    var addr = pos.Value.address;

                    if (distToBase < BazaR)
                    {
                        // Na bazie — sprawdź od kiedy
                        status = "NA BAZIE"; clr = Color.FromRgb(46, 125, 50);
                        var ss = allSsMap.TryGetValue(v.objectNo, out var list) ? list : new();
                        var lastBase = ss.Where(s => s.AtBase).OrderByDescending(s => s.Start).FirstOrDefault();
                        detail = lastBase != null ? $"Od {lastBase.Start:HH:mm} — gotowy" : "Gotowy";
                    }
                    else if (speed > 3)
                    {
                        // Jedzie — szacuj powrót
                        status = "W TRASIE"; clr = Color.FromRgb(21, 101, 192);
                        var etaMin = (int)(distToBase / 1000.0 / Math.Max(speed, 30) * 60);
                        eta = DateTime.Now.AddMinutes(etaMin + 30); // +30 min bufor na rozładunek/postój
                        detail = $"{speed} km/h — {addr}";
                        if (distToBase < 50000) detail += $" (wraca ~{eta:HH:mm})";
                        else detail += $" ({distToBase / 1000:F0} km od bazy)";
                    }
                    else
                    {
                        // Stoi poza bazą
                        status = "POSTÓJ"; clr = Color.FromRgb(230, 81, 0);
                        var ss = allSsMap.TryGetValue(v.objectNo, out var list) ? list : new();
                        var currStop = ss.Where(s => !s.AtBase && HavM(s.Lat, s.Lon, pos.Value.lat, pos.Value.lon) < 500)
                            .OrderByDescending(s => s.Start).FirstOrDefault();
                        if (currStop != null)
                        {
                            var stopMin = (int)(DateTime.Now - currStop.Start).TotalMinutes;
                            detail = $"{currStop.Address} (od {currStop.Start:HH:mm}, {stopMin} min)";
                        }
                        else
                            detail = $"{addr} ({distToBase / 1000:F0} km od bazy)";
                    }
                }

                // Kurs info
                var kursInfo = "";
                if (objToPojazdId.TryGetValue(v.objectNo, out var pojazdId) && kursyPerPojazd.TryGetValue(pojazdId, out var kd))
                {
                    kursInfo = $"Kurs: {kd.trasa}";
                    if (!string.IsNullOrEmpty(kd.godzWyj)) kursInfo += $" | Wyj: {kd.godzWyj}";
                    if (!string.IsNullOrEmpty(kd.kierowca)) kursInfo += $" | Kier: {kd.kierowca}";
                    if (kd.klienci.Count > 0)
                        kursInfo += $"\n     Dostawy: {string.Join("  →  ", kd.klienci.Take(5))}";
                    if (kd.klienci.Count > 5) kursInfo += $" +{kd.klienci.Count - 5} więcej";
                }

                liveStatus.Add((displayName, v.rejestracja, status, detail, eta, clr, kursInfo));
            }

            // Statystyki
            var naBasie = liveStatus.Count(s => s.status == "NA BAZIE");
            var wTrasie = liveStatus.Count(s => s.status == "W TRASIE");
            var naPostoju = liveStatus.Count(s => s.status == "POSTÓJ");
            var brakGps = liveStatus.Count(s => s.status == "BRAK GPS");

            void AddStatBox(string label, int count, Color bg, Color fg)
            {
                var b = new Border { Background = new SolidColorBrush(bg), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 6, 0) };
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock { Text = $"{count}", FontSize = 16, FontWeight = FontWeights.ExtraBold, Foreground = new SolidColorBrush(fg), VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(new TextBlock { Text = $" {label}", FontSize = 11, Foreground = new SolidColorBrush(fg), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.8 });
                b.Child = sp;
                AvailStats.Children.Add(b);
            }
            AddStatBox("na bazie", naBasie, Color.FromRgb(232, 245, 233), Color.FromRgb(46, 125, 50));
            AddStatBox("w trasie", wTrasie, Color.FromRgb(227, 242, 253), Color.FromRgb(21, 101, 192));
            AddStatBox("na postoju", naPostoju, Color.FromRgb(255, 243, 224), Color.FromRgb(230, 81, 0));
            if (brakGps > 0) AddStatBox("brak GPS", brakGps, Color.FromRgb(245, 245, 245), Color.FromRgb(158, 158, 158));

            // Lista aut — posortowana: na bazie na górze
            foreach (var v in liveStatus.OrderBy(s => s.status == "NA BAZIE" ? 0 : s.status == "POSTÓJ" ? 1 : s.status == "W TRASIE" ? 2 : 3).ThenBy(s => s.name))
            {
                var row = new Border { Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 0, 0),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(245, 245, 248)), BorderThickness = new Thickness(0, 0, 0, 1) };
                var stack = new StackPanel();

                // Wiersz 1: pojazd + status + lokalizacja
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                g.Children.Add(SetCol(new TextBlock { Text = v.name, FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(38, 50, 56)), VerticalAlignment = VerticalAlignment.Center }, 0));

                var pill = new Border { Background = new SolidColorBrush(Color.FromArgb(25, v.clr.R, v.clr.G, v.clr.B)),
                    CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 1, 5, 1), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                pill.Child = new TextBlock { Text = v.status, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(v.clr) };
                g.Children.Add(SetCol(pill, 1));

                g.Children.Add(SetCol(new TextBlock { Text = v.detail, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
                    VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }, 2));
                stack.Children.Add(g);

                // Wiersz 2: kurs z dostawami
                if (!string.IsNullOrEmpty(v.kursInfo))
                {
                    var lines = v.kursInfo.Split('\n');
                    foreach (var line in lines)
                    {
                        var isDostawy = line.TrimStart().StartsWith("Dostawy:");
                        stack.Children.Add(new TextBlock
                        {
                            Text = line.TrimStart(),
                            FontSize = isDostawy ? 9 : 9.5,
                            Margin = new Thickness(100, 0, 0, 0),
                            Foreground = new SolidColorBrush(isDostawy ? Color.FromRgb(96, 125, 139) : Color.FromRgb(57, 73, 171)),
                            FontWeight = isDostawy ? FontWeights.Normal : FontWeights.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        });
                    }
                }
                else if (v.status == "NA BAZIE")
                {
                    stack.Children.Add(new TextBlock { Text = "Brak kursu na dziś — wolny", FontSize = 9.5, Margin = new Thickness(100, 1, 0, 2),
                        Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)), FontStyle = FontStyles.Italic });
                }

                row.Child = stack;
                AvailList.Children.Add(row);
            }

            // Sugestie
            var suggestions = new List<string>();

            // Pobierz kursy z TransportPL
            var pendingKursy = 0; var totalKursy = 0;
            try
            {
                using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Kurs WHERE DataKursu=CAST(GETDATE() AS DATE) AND PojazdID IS NULL";
                pendingKursy = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                using var cmd2 = conn.CreateCommand();
                cmd2.CommandText = "SELECT COUNT(*) FROM Kurs WHERE DataKursu=CAST(GETDATE() AS DATE)";
                totalKursy = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
            }
            catch { }

            if (totalKursy > 0)
                suggestions.Add($"Dzisiaj zaplanowano {totalKursy} kursów{(pendingKursy > 0 ? $" — {pendingKursy} bez pojazdu!" : ".")}");
            if (pendingKursy > 0)
                suggestions.Add($"{pendingKursy} kursów czeka na przypisanie pojazdu.");

            if (pendingKursy > naBasie && naBasie > 0)
                suggestions.Add($"Brakuje {pendingKursy - naBasie} pojazdów — {wTrasie} wracają z trasy.");

            var nextReturn = liveStatus.Where(s => s.eta.HasValue).OrderBy(s => s.eta).FirstOrDefault();
            if (nextReturn.eta.HasValue)
                suggestions.Add($"Najbliższy powrót: {nextReturn.name} ok. {nextReturn.eta:HH:mm}.");

            if (naBasie == 0 && wTrasie > 0)
                suggestions.Add("Brak wolnych pojazdów na bazie — wszystkie w trasie lub na postoju.");

            var wolneBezKursu = liveStatus.Count(s => s.status == "NA BAZIE" && string.IsNullOrEmpty(s.kursInfo));
            if (wolneBezKursu > 0)
                suggestions.Add($"{wolneBezKursu} pojazdów na bazie bez kursu — gotowe do dyspozycji.");

            var wTrasieBezKursu = liveStatus.Count(s => s.status == "W TRASIE" && string.IsNullOrEmpty(s.kursInfo));
            if (wTrasieBezKursu > 0)
                suggestions.Add($"{wTrasieBezKursu} pojazdów w trasie bez przypisanego kursu — niezaplanowane wyjazdy?");

            if (suggestions.Count > 0)
            {
                SuggestionBox.Visibility = Visibility.Visible;
                SuggestionText.Text = string.Join("\n", suggestions.Select(s => $"• {s}"));
            }
            else SuggestionBox.Visibility = Visibility.Collapsed;
        }

        private void AddHourHeader()
        {
            var header = new Grid { Height = 16 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            var canvas = new Canvas { ClipToBounds = true };
            Grid.SetColumn(canvas, 1); header.Children.Add(canvas);
            canvas.Loaded += (_, _) =>
            {
                var w = canvas.ActualWidth; if (w < 10) w = 900;
                for (int h = 0; h <= 24; h++)
                {
                    if (h < 24 && h % 2 == 0)
                    {
                        var tb = new TextBlock { Text = $"{h:D2}", FontSize = 8, Foreground = new SolidColorBrush(Color.FromRgb(160, 170, 180)) };
                        Canvas.SetLeft(tb, h / 24.0 * w + 1); canvas.Children.Add(tb);
                    }
                }
            };
            header.Children.Add(SetCol(new TextBlock { Text = "Baza", FontSize = 8, Foreground = new SolidColorBrush(Color.FromRgb(160, 170, 180)),
                HorizontalAlignment = HorizontalAlignment.Center }, 2));
            header.Children.Add(SetCol(new TextBlock { Text = "Trasa", FontSize = 8, Foreground = new SolidColorBrush(Color.FromRgb(160, 170, 180)),
                HorizontalAlignment = HorizontalAlignment.Center }, 3));
            TimelinePanel.Children.Add(header);
        }

        private void AddLegend()
        {
            var legend = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(110, 8, 0, 0) };
            void Add(string t, Color c) { legend.Children.Add(new Rectangle { Width = 12, Height = 12, Fill = new SolidColorBrush(c), RadiusX = 2, RadiusY = 2 });
                legend.Children.Add(new TextBlock { Text = t, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)), Margin = new Thickness(3, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center }); }
            Add("Na bazie", Color.FromRgb(200, 230, 201)); Add("W trasie", Color.FromRgb(187, 222, 251));
            Add("Postój poza bazą", Color.FromRgb(255, 224, 178)); Add("Brak danych", Color.FromRgb(248, 248, 250));
            legend.Children.Add(new TextBlock { Text = "  Kliknij wiersz aby zobaczyć szczegóły", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                FontStyle = FontStyles.Italic, VerticalAlignment = VerticalAlignment.Center });
            TimelinePanel.Children.Add(legend);
        }

        private static FrameworkElement SetCol(FrameworkElement el, int col) { Grid.SetColumn(el, col); return el; }
        private static double HavM(double a1, double o1, double a2, double o2)
        { var dA = (a2 - a1) * Math.PI / 180; var dO = (o2 - o1) * Math.PI / 180;
          var x = Math.Sin(dA / 2) * Math.Sin(dA / 2) + Math.Cos(a1 * Math.PI / 180) * Math.Cos(a2 * Math.PI / 180) * Math.Sin(dO / 2) * Math.Sin(dO / 2);
          return 6371000 * 2 * Math.Atan2(Math.Sqrt(x), Math.Sqrt(1 - x)); }
        private static string U(string s) => Uri.EscapeDataString(s);

        private class StandStillItem
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public double Lat { get; set; }
            public double Lon { get; set; }
            public string Address { get; set; } = "";
            public bool AtBase { get; set; }
            public int DurationMin { get; set; }
        }
    }
}
