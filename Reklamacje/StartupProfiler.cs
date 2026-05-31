using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Kalendarz1.Reklamacje
{
    // Advanced startup profiler dla Panel Reklamacji.
    // Mierzy: fazy z timingiem + memory + GC, SQL per zapytanie z fazami,
    // N+1 detection, snapshot bazy danych, network probe per DB, WPF visual tree stats,
    // peak memory, auto-rekomendacje, ASCII histogram, auto-save markdown do %TEMP%.
    // Wynik: TabControl 6 zakladek, kazda kopiowalna do schowka jako markdown.
    public static class StartupProfiler
    {
        // ===== Modele danych =====
        public sealed class SqlEntry
        {
            public int Index;
            public string Phase;
            public string Label;
            public string Database;
            public double Ms;
            public int Rows;
            public string Preview;
            public string Error;
        }

        public sealed class PhaseEntry
        {
            public int Index;
            public string Name;
            public double Ms;
            public int SqlCount;
            public double SqlMs;
            public long MemDeltaKB;
            public long MemPeakKB;
            public int Gen0Coll;
            public int Gen1Coll;
            public int Gen2Coll;
            public string Error;
            public List<SqlEntry> Sqls = new List<SqlEntry>();
        }

        public sealed class DbProbe
        {
            public string Label;
            public string Server;
            public string Database;
            public double OpenMs;
            public double QueryMs;
            public string ServerVersion;
            public long? PingMs;
            public string Error;
        }

        public sealed class DbSnapshotEntry
        {
            public string Database;
            public string Table;
            public string Metric;
            public string Value;
            public double Ms;
        }

        public sealed class WpfStats
        {
            public string WindowTitle;
            public double Width;
            public double Height;
            public int VisualTreeCount;
            public int NamedElements;
            public int Bindings;
            public int Bitmaps;
            public int DataGridColumns;
            public string RenderTier;
        }

        public sealed class Recommendation
        {
            public string Severity;   // INFO / WARN / CRIT
            public string Category;
            public string Message;
            public string Suggestion;
        }

        // ===== Stan =====
        private static readonly object _lock = new object();
        private static readonly List<PhaseEntry> _phases = new List<PhaseEntry>();
        private static readonly List<SqlEntry> _sqlLog = new List<SqlEntry>();
        private static readonly List<(string Key, string Value)> _notes = new List<(string, string)>();
        private static readonly List<DbProbe> _probes = new List<DbProbe>();
        private static readonly List<DbSnapshotEntry> _snapshot = new List<DbSnapshotEntry>();
        private static readonly Stopwatch _wallClock = new Stopwatch();
        private static PhaseEntry _currentPhase;
        private static WpfStats _wpfStats;
        private static long _memBeforeBytes;
        private static long _memAfterBytes;
        private static long _memPeakBytes;
        private static DateTime _startedAt;
        private static int _processStartupMsAt;
        private static string _machineInfo;
        private static string _userId;
        private static string _autoSavedPath;
        private static string _previousReportPath;
        private static int _gen0Start, _gen1Start, _gen2Start;
        private static int _seq = 0;

        public static bool Enabled { get; set; } = true;
        public static bool IsRunning => _wallClock.IsRunning;
        public static List<PhaseEntry> Phases { get { lock (_lock) return _phases.ToList(); } }
        public static List<SqlEntry> Sqls { get { lock (_lock) return _sqlLog.ToList(); } }
        public static List<DbProbe> Probes { get { lock (_lock) return _probes.ToList(); } }
        public static List<DbSnapshotEntry> Snapshot { get { lock (_lock) return _snapshot.ToList(); } }
        public static WpfStats Wpf => _wpfStats;
        public static string AutoSavedPath => _autoSavedPath;
        public static double WallMs => _wallClock.Elapsed.TotalMilliseconds;

        // ===== Lifecycle =====
        public static void Begin(string userId = null)
        {
            if (!Enabled) return;
            lock (_lock)
            {
                _phases.Clear();
                _sqlLog.Clear();
                _notes.Clear();
                _probes.Clear();
                _snapshot.Clear();
                _currentPhase = null;
                _wpfStats = null;
                _seq = 0;
                _userId = userId;
                _startedAt = DateTime.Now;
                _memBeforeBytes = GC.GetTotalMemory(false);
                _memPeakBytes = _memBeforeBytes;
                _gen0Start = GC.CollectionCount(0);
                _gen1Start = GC.CollectionCount(1);
                _gen2Start = GC.CollectionCount(2);
                _wallClock.Restart();
                try
                {
                    var p = Process.GetCurrentProcess();
                    _processStartupMsAt = (int)(DateTime.Now - p.StartTime).TotalMilliseconds;
                    var workingSet = p.WorkingSet64 / 1024 / 1024;
                    _machineInfo = $"{Environment.MachineName} | user={Environment.UserName} | OS={Environment.OSVersion.VersionString} | CPU cores={Environment.ProcessorCount} | .NET={Environment.Version} | working-set={workingSet}MB | proc-uptime={_processStartupMsAt}ms";
                }
                catch { _machineInfo = $"{Environment.MachineName}"; }
            }
        }

        public static void End()
        {
            if (!Enabled || !_wallClock.IsRunning) return;
            lock (_lock)
            {
                _wallClock.Stop();
                _memAfterBytes = GC.GetTotalMemory(false);
                if (_memAfterBytes > _memPeakBytes) _memPeakBytes = _memAfterBytes;
                AutoSaveReport();
            }
        }

        // ===== Fazy =====
        public static IDisposable Phase(string name)
        {
            if (!Enabled) return NullScope.Instance;
            var entry = new PhaseEntry { Name = name, Index = ++_seq };
            long memBefore = GC.GetTotalMemory(false);
            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);
            var sw = Stopwatch.StartNew();
            lock (_lock)
            {
                _phases.Add(entry);
                _currentPhase = entry;
            }
            return new ActionScope(() =>
            {
                sw.Stop();
                long memAfter = GC.GetTotalMemory(false);
                lock (_lock)
                {
                    entry.Ms = sw.Elapsed.TotalMilliseconds;
                    entry.MemDeltaKB = (memAfter - memBefore) / 1024;
                    entry.MemPeakKB = Math.Max(memBefore, memAfter) / 1024;
                    if (memAfter > _memPeakBytes) _memPeakBytes = memAfter;
                    entry.Gen0Coll = GC.CollectionCount(0) - gen0;
                    entry.Gen1Coll = GC.CollectionCount(1) - gen1;
                    entry.Gen2Coll = GC.CollectionCount(2) - gen2;
                    if (_currentPhase == entry) _currentPhase = null;
                }
            });
        }

        // ===== SQL =====
        public static IDisposable Sql(string label, string preview = null, string database = null)
        {
            if (!Enabled) return NullScope.Instance;
            var entry = new SqlEntry
            {
                Index = ++_seq,
                Phase = _currentPhase?.Name ?? "(root)",
                Label = label,
                Preview = preview != null ? Truncate(preview.Replace("\r", " ").Replace("\n", " "), 400) : null,
                Database = database
            };
            var sw = Stopwatch.StartNew();
            return new SqlScope(entry, sw);
        }

        public static void NoteRows(int rows)
        {
            if (!Enabled) return;
            lock (_lock) { if (_sqlLog.Count > 0) _sqlLog[_sqlLog.Count - 1].Rows = rows; }
        }

        public static void RecordSqlError(Exception ex)
        {
            if (!Enabled) return;
            lock (_lock) { if (_sqlLog.Count > 0) _sqlLog[_sqlLog.Count - 1].Error = ex.GetType().Name + ": " + ex.Message; }
        }

        public static void Note(string key, string value)
        {
            if (!Enabled) return;
            lock (_lock) _notes.Add((key, value));
        }
        public static void Note(string key, long value) => Note(key, value.ToString("N0", CultureInfo.InvariantCulture));
        public static void Note(string key, double value) => Note(key, value.ToString("F2", CultureInfo.InvariantCulture));

        public static void RecordError(string phaseName, Exception ex)
        {
            if (!Enabled) return;
            lock (_lock)
            {
                var p = _phases.FirstOrDefault(x => x.Name == phaseName);
                if (p != null) p.Error = ex.GetType().Name + ": " + ex.Message;
                else _notes.Add(("ERROR:" + phaseName, ex.GetType().Name + ": " + ex.Message));
            }
        }

        // ===== Network probe =====
        public static void ProbeConnection(string connStr, string label)
        {
            if (!Enabled) return;
            var probe = new DbProbe { Label = label };
            try
            {
                var b = new SqlConnectionStringBuilder(connStr);
                probe.Server = b.DataSource;
                probe.Database = b.InitialCatalog;
                try
                {
                    var serverHost = b.DataSource.Split(',')[0].Split('\\')[0];
                    if (!string.IsNullOrEmpty(serverHost) && serverHost != ".")
                    {
                        using (var ping = new Ping())
                        {
                            var r = ping.Send(serverHost, 1000);
                            if (r != null && r.Status == IPStatus.Success) probe.PingMs = r.RoundtripTime;
                        }
                    }
                }
                catch { /* ping moze nie dzialac (firewall) */ }

                var sw = Stopwatch.StartNew();
                using (var c = new SqlConnection(connStr))
                {
                    c.Open();
                    sw.Stop();
                    probe.OpenMs = sw.Elapsed.TotalMilliseconds;
                    probe.ServerVersion = c.ServerVersion;
                    sw.Restart();
                    using (var cmd = new SqlCommand("SELECT @@VERSION", c)) cmd.ExecuteScalar();
                    sw.Stop();
                    probe.QueryMs = sw.Elapsed.TotalMilliseconds;
                }
            }
            catch (Exception ex) { probe.Error = ex.GetType().Name + ": " + ex.Message; }
            lock (_lock) _probes.Add(probe);
        }

        public static async Task ProbeAllAsync(params (string connStr, string label)[] dbs)
        {
            if (!Enabled) return;
            await Task.Run(() => { foreach (var (cs, lbl) in dbs) ProbeConnection(cs, lbl); });
        }

        // ===== DB snapshot =====
        public static void CaptureDbSnapshot(string libraNetConn, string handelConn)
        {
            if (!Enabled) return;
            CaptureLibraSnap(libraNetConn);
            CaptureHandelSnap(handelConn);
        }

        private static void CaptureLibraSnap(string connStr)
        {
            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    SnapCount(conn, "LibraNet", "Reklamacje", "Total", "SELECT COUNT(*) FROM [dbo].[Reklamacje]");
                    SnapCount(conn, "LibraNet", "Reklamacje", "Korekty Symfonia", "SELECT COUNT(*) FROM [dbo].[Reklamacje] WHERE TypReklamacji='Faktura korygujaca'");
                    SnapCount(conn, "LibraNet", "Reklamacje", "Zamkniete", "SELECT COUNT(*) FROM [dbo].[Reklamacje] WHERE DataZakonczenia IS NOT NULL");
                    SnapCount(conn, "LibraNet", "Reklamacje", "Otwarte (StatusV2 IN ZGLOSZONA/W_ANALIZIE)", "SELECT COUNT(*) FROM [dbo].[Reklamacje] WHERE ISNULL(StatusV2,'ZGLOSZONA') IN ('ZGLOSZONA','W_ANALIZIE')");
                    SnapCount(conn, "LibraNet", "Reklamacje", "Wymaga uzupelnienia", "SELECT COUNT(*) FROM [dbo].[Reklamacje] WHERE WymagaUzupelnienia=1");
                    SnapCount(conn, "LibraNet", "ReklamacjeTowary", "Wierszy towarow", "SELECT COUNT(*) FROM [dbo].[ReklamacjeTowary]");
                    SnapCount(conn, "LibraNet", "ReklamacjeZdjecia", "Zdjec (rekordy)", "SELECT COUNT(*) FROM [dbo].[ReklamacjeZdjecia]");
                    SnapBytes(conn, "LibraNet", "ReklamacjeZdjecia", "Rozmiar BLOB-ow", "SELECT ISNULL(SUM(DATALENGTH(DaneZdjecia)),0) FROM [dbo].[ReklamacjeZdjecia] WHERE DaneZdjecia IS NOT NULL");
                    // Fix N: zamiast IF OBJECT_ID() IS NOT NULL SELECT COUNT(*) (320ms — fatalny plan, statement recompile)
                    // uzywamy sys.dm_db_partition_stats — lookup w O(1) z metadata cache.
                    SnapCountFast(conn, "LibraNet", "ReklamacjeKomentarze", "Komentarzy");
                    SnapCountFast(conn, "LibraNet", "ReklamacjeZalaczniki", "Zalacznikow");
                    SnapCountFast(conn, "LibraNet", "ReklamacjeHistoria", "Wpisow historii");
                    SnapCount(conn, "LibraNet", "operators", "Operatorow", "SELECT COUNT(*) FROM [dbo].[operators]");
                    SnapCount(conn, "LibraNet", "UserHandlowcy", "Mapowan handlowcow", "IF OBJECT_ID('dbo.UserHandlowcy') IS NOT NULL SELECT COUNT(*) FROM [dbo].[UserHandlowcy] ELSE SELECT 0");
                    SnapTopRow(conn, "LibraNet", "Reklamacje", "Ostatnia DataZgloszenia", "SELECT TOP 1 CONVERT(varchar, DataZgloszenia, 120) FROM [dbo].[Reklamacje] ORDER BY DataZgloszenia DESC");
                }
            }
            catch (Exception ex) { lock (_lock) _snapshot.Add(new DbSnapshotEntry { Database = "LibraNet", Table = "(snapshot)", Metric = "ERROR", Value = ex.Message }); }
        }

        private static void CaptureHandelSnap(string connStr)
        {
            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    SnapCount(conn, "HANDEL", "DK", "Korekt sFKS/sFKSB/sFWK total", "SELECT COUNT(*) FROM [HANDEL].[HM].[DK] WHERE seria IN ('sFKS','sFKSB','sFWK') AND anulowany=0");
                    SnapCount(conn, "HANDEL", "DK", "Korekty 30 dni", "SELECT COUNT(*) FROM [HANDEL].[HM].[DK] WHERE seria IN ('sFKS','sFKSB','sFWK') AND anulowany=0 AND data >= DATEADD(DAY,-30,GETDATE())");
                    SnapCount(conn, "HANDEL", "DK", "Korekty 6 mies", "SELECT COUNT(*) FROM [HANDEL].[HM].[DK] WHERE seria IN ('sFKS','sFKSB','sFWK') AND anulowany=0 AND data >= DATEADD(MONTH,-6,GETDATE())");
                    SnapCount(conn, "HANDEL", "ContractorClassification", "Mapowan handlowcow", "SELECT COUNT(*) FROM [HANDEL].[SSCommon].[ContractorClassification] WHERE CDim_Handlowiec_Val IS NOT NULL AND CDim_Handlowiec_Val <> '' AND CDim_Handlowiec_Val <> '-'");
                    SnapTopRow(conn, "HANDEL", "DK", "Ostatnia korekta (data)", "SELECT TOP 1 CONVERT(varchar, data, 120) FROM [HANDEL].[HM].[DK] WHERE seria IN ('sFKS','sFKSB','sFWK') AND anulowany=0 ORDER BY data DESC");
                }
            }
            catch (Exception ex) { lock (_lock) _snapshot.Add(new DbSnapshotEntry { Database = "HANDEL", Table = "(snapshot)", Metric = "ERROR", Value = ex.Message }); }
        }

        // Fix N: szybki count z sys.dm_db_partition_stats (metadata, nie skan tabeli).
        // Wynik moze byc lekko nieaktualny (sync metadata co ~kilka sek) ale dla snapshotu OK.
        // Zwraca "(brak)" jezeli tabela nie istnieje. ~5ms vs 320ms dla IF OBJECT_ID wrappera.
        private static void SnapCountFast(SqlConnection c, string db, string table, string metric)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using (var cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(ps.row_count), 0)
                    FROM sys.dm_db_partition_stats ps
                    INNER JOIN sys.objects o ON ps.object_id = o.object_id
                    WHERE o.name = @T AND ps.index_id IN (0, 1)", c))
                {
                    cmd.CommandTimeout = 5;
                    cmd.Parameters.AddWithValue("@T", table);
                    var v = cmd.ExecuteScalar();
                    sw.Stop();
                    long n = (v == null || v == DBNull.Value) ? 0 : Convert.ToInt64(v);
                    lock (_lock) _snapshot.Add(new DbSnapshotEntry { Database = db, Table = table, Metric = metric, Value = n.ToString("N0", CultureInfo.InvariantCulture), Ms = sw.Elapsed.TotalMilliseconds });
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                lock (_lock) _snapshot.Add(new DbSnapshotEntry { Database = db, Table = table, Metric = metric, Value = "(brak/error)", Ms = sw.Elapsed.TotalMilliseconds });
            }
        }

        private static void SnapCount(SqlConnection c, string db, string table, string metric, string sql)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using (var cmd = new SqlCommand(sql, c)) { cmd.CommandTimeout = 10; var v = cmd.ExecuteScalar(); sw.Stop(); lock (_lock) _snapshot.Add(new DbSnapshotEntry { Database = db, Table = table, Metric = metric, Value = (v == null || v == DBNull.Value) ? "0" : Convert.ToInt64(v).ToString("N0", CultureInfo.InvariantCulture), Ms = sw.Elapsed.TotalMilliseconds }); }
            }
            catch (Exception ex) { sw.Stop(); lock (_lock) _snapshot.Add(new DbSnapshotEntry { Database = db, Table = table, Metric = metric, Value = "ERR: " + ex.Message, Ms = sw.Elapsed.TotalMilliseconds }); }
        }

        private static void SnapBytes(SqlConnection c, string db, string table, string metric, string sql)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using (var cmd = new SqlCommand(sql, c)) { cmd.CommandTimeout = 30; var v = cmd.ExecuteScalar(); sw.Stop(); long bytes = (v == null || v == DBNull.Value) ? 0 : Convert.ToInt64(v); lock (_lock) _snapshot.Add(new DbSnapshotEntry { Database = db, Table = table, Metric = metric, Value = FormatBytes(bytes), Ms = sw.Elapsed.TotalMilliseconds }); }
            }
            catch (Exception ex) { sw.Stop(); lock (_lock) _snapshot.Add(new DbSnapshotEntry { Database = db, Table = table, Metric = metric, Value = "ERR: " + ex.Message, Ms = sw.Elapsed.TotalMilliseconds }); }
        }

        private static void SnapTopRow(SqlConnection c, string db, string table, string metric, string sql)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using (var cmd = new SqlCommand(sql, c)) { cmd.CommandTimeout = 10; var v = cmd.ExecuteScalar(); sw.Stop(); lock (_lock) _snapshot.Add(new DbSnapshotEntry { Database = db, Table = table, Metric = metric, Value = v?.ToString() ?? "(null)", Ms = sw.Elapsed.TotalMilliseconds }); }
            }
            catch (Exception ex) { sw.Stop(); lock (_lock) _snapshot.Add(new DbSnapshotEntry { Database = db, Table = table, Metric = metric, Value = "ERR: " + ex.Message, Ms = sw.Elapsed.TotalMilliseconds }); }
        }

        // ===== WPF Stats =====
        public static void CaptureWpfStats(Window w)
        {
            if (!Enabled || w == null) return;
            var s = new WpfStats { WindowTitle = w.Title, Width = w.ActualWidth, Height = w.ActualHeight };
            try
            {
                int count = 0, named = 0, bindings = 0, bitmaps = 0, dgCols = 0;
                Action<DependencyObject> walk = null;
                walk = obj =>
                {
                    if (obj == null) return;
                    count++;
                    if (obj is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name)) named++;
                    if (obj is System.Windows.Controls.DataGrid dg) dgCols += dg.Columns?.Count ?? 0;
                    if (obj is System.Windows.Controls.Image) bitmaps++;
                    var lf = obj as DependencyObject;
                    var locals = LocalValueEnumerator(lf);
                    foreach (var lv in locals)
                    {
                        if (lv.Value is BindingExpressionBase) bindings++;
                    }
                    int n = VisualTreeHelper.GetChildrenCount(obj);
                    for (int i = 0; i < n; i++) walk(VisualTreeHelper.GetChild(obj, i));
                };
                walk(w);
                s.VisualTreeCount = count;
                s.NamedElements = named;
                s.Bindings = bindings;
                s.Bitmaps = bitmaps;
                s.DataGridColumns = dgCols;
                int tier = (System.Windows.Media.RenderCapability.Tier >> 16);
                s.RenderTier = $"Tier {tier} (0=software, 1=partial HW, 2=full HW)";
            }
            catch (Exception ex) { s.RenderTier = "ERR: " + ex.Message; }
            _wpfStats = s;
        }

        private static IEnumerable<LocalValueEntry> LocalValueEnumerator(DependencyObject d)
        {
            var le = d.GetLocalValueEnumerator();
            while (le.MoveNext()) yield return le.Current;
        }

        // ===== Rekomendacje (auto-heuristics) =====
        public static List<Recommendation> GenerateRecommendations()
        {
            var recs = new List<Recommendation>();
            double wall = _wallClock.Elapsed.TotalMilliseconds;
            double sqlTotal = _sqlLog.Sum(s => s.Ms);

            // Total wall-clock
            if (wall > 5000) recs.Add(new Recommendation { Severity = "CRIT", Category = "Startup", Message = $"Startup {wall:F0}ms (>5s) — uzytkownik czeka.", Suggestion = "Spawn fazy async, zacznij od pustego grida + async load." });
            else if (wall > 2500) recs.Add(new Recommendation { Severity = "WARN", Category = "Startup", Message = $"Startup {wall:F0}ms (>2.5s) — odczuwalny.", Suggestion = "Sprawdz TOP 3 fazy w zakladce 'Fazy'." });

            // SQL dominacja
            if (wall > 0 && sqlTotal / wall > 0.6) recs.Add(new Recommendation { Severity = "WARN", Category = "SQL", Message = $"SQL = {sqlTotal:F0}ms = {sqlTotal / wall * 100:F0}% wall-clock.", Suggestion = "Optymalizuj zapytania, dodaj indeksy, batch'uj operacje." });

            // Per-faza dominacja
            foreach (var p in _phases.OrderByDescending(x => x.Ms).Take(3))
            {
                if (wall > 0 && p.Ms / wall > 0.30) recs.Add(new Recommendation { Severity = p.Ms / wall > 0.5 ? "CRIT" : "WARN", Category = "Phase", Message = $"Faza '{p.Name}' = {p.Ms:F0}ms = {p.Ms / wall * 100:F0}% wall-clock.", Suggestion = "Sprawdz drill-down SQL tej fazy w zakladce 'SQL detail'." });
            }

            // N+1 detection — grupuj po label
            var nPlus1 = _sqlLog.GroupBy(s => s.Label).Where(g => g.Count() >= 10).OrderByDescending(g => g.Count()).ToList();
            foreach (var grp in nPlus1)
            {
                double totalMs = grp.Sum(s => s.Ms);
                recs.Add(new Recommendation { Severity = grp.Count() >= 50 ? "CRIT" : "WARN", Category = "N+1", Message = $"Label '{grp.Key}' wykonany {grp.Count()}× ({totalMs:F0}ms total).", Suggestion = "Zamien petle na batch (jedno zapytanie + LINQ in-memory)." });
            }

            // Slow individual queries
            foreach (var s in _sqlLog.Where(x => x.Ms > 500).OrderByDescending(x => x.Ms).Take(5))
            {
                recs.Add(new Recommendation { Severity = s.Ms > 1500 ? "CRIT" : "WARN", Category = "Slow SQL", Message = $"'{s.Label}' = {s.Ms:F0}ms ({s.Database}).", Suggestion = "Sprawdz plan, czy indeks, czy WHERE selektywne, czy nie ladujesz za duzo wierszy." });
            }

            // Memory
            long memDeltaKB = (_memAfterBytes - _memBeforeBytes) / 1024;
            if (memDeltaKB > 50_000) recs.Add(new Recommendation { Severity = "WARN", Category = "Memory", Message = $"Pamiec wzrosla o {memDeltaKB / 1024}MB podczas startupu.", Suggestion = "Sprawdz czy nie trzymasz wszystkich miniatur/avatarów w RAM. Dispose() bitmap." });

            // Connection slow
            foreach (var p in _probes.Where(x => x.OpenMs > 200 && x.Error == null))
            {
                recs.Add(new Recommendation { Severity = p.OpenMs > 500 ? "CRIT" : "WARN", Category = "Network", Message = $"Otwarcie polaczenia do {p.Label} = {p.OpenMs:F0}ms.", Suggestion = "Sprawdz: latency sieciowa? SQL Server zajety? Connection pool wyczerpany?" });
            }
            foreach (var p in _probes.Where(x => x.Error != null))
            {
                recs.Add(new Recommendation { Severity = "CRIT", Category = "Network", Message = $"Polaczenie do {p.Label} FAILED: {p.Error}", Suggestion = "Sprawdz firewall, VPN, czy serwer dziala." });
            }

            // WPF visual tree
            if (_wpfStats != null && _wpfStats.VisualTreeCount > 5000) recs.Add(new Recommendation { Severity = "WARN", Category = "WPF", Message = $"Visual tree = {_wpfStats.VisualTreeCount} elementow.", Suggestion = "Virtualizacja DataGrid, redukcja zagniezdzonych szablonow." });
            if (_wpfStats != null && _wpfStats.RenderTier != null && _wpfStats.RenderTier.Contains("Tier 0")) recs.Add(new Recommendation { Severity = "WARN", Category = "WPF", Message = "Software rendering (Tier 0).", Suggestion = "RDP? Sterowniki? WPF nie ma GPU acceleration — wszystko CPU." });

            // GC pressure
            int gen2 = _phases.Sum(p => p.Gen2Coll);
            if (gen2 > 0) recs.Add(new Recommendation { Severity = "WARN", Category = "GC", Message = $"Gen2 collection wykonal sie {gen2}× podczas startupu.", Suggestion = "Duza presja pamieciowa. Zmniejsz alokacje obiektow w petli." });

            // Heurystyki specyficzne dla Reklamacji
            var sync = _phases.FirstOrDefault(p => p.Name.Contains("SyncFakturyKorygujace"));
            if (sync != null && sync.Ms > 800) recs.Add(new Recommendation { Severity = "WARN", Category = "Reklamacje", Message = $"SyncFakturyKorygujace = {sync.Ms:F0}ms.", Suggestion = "Zaladuj wszystkie istniejace IdDokumentu do HashSet jednym SELECT, zamiast SELECT COUNT(*) per korekta. Batch'uj INSERT-y w transakcji." });

            var migr = _phases.FirstOrDefault(p => p.Name.Contains("MigracjaBazy"));
            if (migr != null && migr.Ms > 500) recs.Add(new Recommendation { Severity = "WARN", Category = "Reklamacje", Message = $"MigracjaBazy = {migr.Ms:F0}ms wykonuje sie ZA KAZDYM otwarciem okna.", Suggestion = "Dodaj klucz w tabeli ustawien (MigrationVersion) — pomijaj jezeli juz wykonana. Albo migruj raz/dobe." });

            var rekl = _phases.FirstOrDefault(p => p.Name.Contains("WczytajReklamacje"));
            if (rekl != null && rekl.Ms > 800) recs.Add(new Recommendation { Severity = "WARN", Category = "Reklamacje", Message = $"WczytajReklamacje = {rekl.Ms:F0}ms.", Suggestion = "Dodaj indeks na (DataZgloszenia DESC, StatusV2, TypReklamacji). Limituj do TOP 500 lub paginuj." });

            if (recs.Count == 0) recs.Add(new Recommendation { Severity = "INFO", Category = "OK", Message = "Brak czerwonych flag — startup w normie.", Suggestion = "Nic do roboty 👍" });
            return recs;
        }

        // ===== Auto-save =====
        private static void AutoSaveReport()
        {
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "Kalendarz1-Profiler");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"reklamacje-startup-{_startedAt:yyyyMMdd-HHmmss}.md");
                _previousReportPath = _autoSavedPath;
                _autoSavedPath = path;
                File.WriteAllText(path, BuildFullReport(), Encoding.UTF8);
            }
            catch { /* nie blokuj startu */ }
        }

        // ===== Generatory raportu =====
        public static string BuildFullReport()
        {
            lock (_lock)
            {
                var sb = new StringBuilder(32_768);
                sb.AppendLine(BuildHeaderSection());
                sb.AppendLine();
                sb.AppendLine(BuildRecommendationsSection());
                sb.AppendLine();
                sb.AppendLine(BuildPhasesSection());
                sb.AppendLine();
                sb.AppendLine(BuildHistogramSection());
                sb.AppendLine();
                sb.AppendLine(BuildSqlSection());
                sb.AppendLine();
                sb.AppendLine(BuildPhaseDrillDown());
                sb.AppendLine();
                sb.AppendLine(BuildDbSnapshotSection());
                sb.AppendLine();
                sb.AppendLine(BuildNetworkSection());
                sb.AppendLine();
                sb.AppendLine(BuildWpfSection());
                sb.AppendLine();
                sb.AppendLine(BuildNotesSection());
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine("_Wygenerowane przez StartupProfiler. Skopiuj i wklej do Claude._");
                return sb.ToString();
            }
        }

        public static string BuildHeaderSection()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                double wall = _wallClock.Elapsed.TotalMilliseconds;
                double sqlTotal = _sqlLog.Sum(s => s.Ms);
                long memDeltaKB = (_memAfterBytes - _memBeforeBytes) / 1024;
                int gen0 = _phases.Sum(p => p.Gen0Coll);
                int gen1 = _phases.Sum(p => p.Gen1Coll);
                int gen2 = _phases.Sum(p => p.Gen2Coll);

                sb.AppendLine("# Reklamacje — Startup Profiler (advanced)");
                sb.AppendLine();
                sb.AppendLine($"- **Start:** {_startedAt:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"- **User:** {_userId ?? "(brak)"}");
                sb.AppendLine($"- **Maszyna:** {_machineInfo}");
                sb.AppendLine();
                sb.AppendLine("## ⏱ KLUCZOWE METRYKI");
                sb.AppendLine();
                sb.AppendLine("| Metryka | Wartosc |");
                sb.AppendLine("|---------|--------:|");
                sb.AppendLine($"| **Wall-clock startupu** | **{wall:F0} ms** ({wall / 1000:F2}s) |");
                sb.AppendLine($"| Liczba faz | {_phases.Count} |");
                sb.AppendLine($"| Liczba zapytan SQL | {_sqlLog.Count} |");
                sb.AppendLine($"| Suma SQL | {sqlTotal:F0} ms ({(wall > 0 ? sqlTotal / wall * 100 : 0):F0}% wall) |");
                sb.AppendLine($"| SQL min/avg/max | {(_sqlLog.Count > 0 ? _sqlLog.Min(s => s.Ms) : 0):F0} / {(_sqlLog.Count > 0 ? _sqlLog.Average(s => s.Ms) : 0):F0} / {(_sqlLog.Count > 0 ? _sqlLog.Max(s => s.Ms) : 0):F0} ms |");
                sb.AppendLine($"| Slow SQL (>200ms) | {_sqlLog.Count(s => s.Ms > 200)} |");
                sb.AppendLine($"| Slow SQL (>500ms) | {_sqlLog.Count(s => s.Ms > 500)} |");
                sb.AppendLine($"| Pamiec przed → po | {_memBeforeBytes / 1024 / 1024} MB → {_memAfterBytes / 1024 / 1024} MB (Δ {memDeltaKB:N0} KB) |");
                sb.AppendLine($"| Peak pamieci | {_memPeakBytes / 1024 / 1024} MB |");
                sb.AppendLine($"| GC Gen0/Gen1/Gen2 | {gen0} / {gen1} / {gen2} |");
                sb.AppendLine($"| Czas .exe → otwarcie | {_processStartupMsAt} ms |");
                sb.AppendLine($"| Auto-saved raport | `{_autoSavedPath ?? "(jeszcze nie zapisano)"}` |");
                return sb.ToString();
            }
        }

        public static string BuildRecommendationsSection()
        {
            var recs = GenerateRecommendations();
            var sb = new StringBuilder();
            sb.AppendLine("## 🎯 REKOMENDACJE (auto-heurystyki)");
            sb.AppendLine();
            int crit = recs.Count(r => r.Severity == "CRIT");
            int warn = recs.Count(r => r.Severity == "WARN");
            int info = recs.Count(r => r.Severity == "INFO");
            sb.AppendLine($"**Podsumowanie:** 🔴 CRIT={crit} • 🟡 WARN={warn} • 🟢 INFO={info}");
            sb.AppendLine();
            sb.AppendLine("| Sev | Kategoria | Problem | Sugestia |");
            sb.AppendLine("|-----|-----------|---------|----------|");
            foreach (var r in recs)
            {
                string icon = r.Severity == "CRIT" ? "🔴" : r.Severity == "WARN" ? "🟡" : "🟢";
                sb.AppendLine($"| {icon} {r.Severity} | {r.Category} | {r.Message} | {r.Suggestion} |");
            }
            return sb.ToString();
        }

        public static string BuildPhasesSection()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                double wall = _wallClock.Elapsed.TotalMilliseconds;
                foreach (var p in _phases)
                {
                    var phaseSql = _sqlLog.Where(s => s.Phase == p.Name).ToList();
                    p.SqlCount = phaseSql.Count;
                    p.SqlMs = phaseSql.Sum(s => s.Ms);
                    p.Sqls = phaseSql;
                }
                sb.AppendLine("## 📊 FAZY STARTUPU (chronologicznie)");
                sb.AppendLine();
                sb.AppendLine("| # | Faza | ms | % wall | SQL # | SQL ms | % SQL | Mem Δ KB | GC0/1/2 | Status |");
                sb.AppendLine("|---|------|---:|------:|------:|------:|------:|--------:|---------|--------|");
                for (int i = 0; i < _phases.Count; i++)
                {
                    var p = _phases[i];
                    double pctWall = wall > 0 ? p.Ms / wall * 100 : 0;
                    double pctSql = p.Ms > 0 ? p.SqlMs / p.Ms * 100 : 0;
                    string status = p.Error != null ? "❌ " + p.Error : (p.Ms > 1000 ? "🔴 SLOW" : (p.Ms > 300 ? "🟡 middle" : "🟢"));
                    sb.AppendLine($"| {i + 1} | {p.Name} | {p.Ms:F0} | {pctWall:F0}% | {p.SqlCount} | {p.SqlMs:F0} | {pctSql:F0}% | {p.MemDeltaKB:N0} | {p.Gen0Coll}/{p.Gen1Coll}/{p.Gen2Coll} | {status} |");
                }
                sb.AppendLine($"| | **SUMA** | **{_phases.Sum(p => p.Ms):F0}** | | **{_sqlLog.Count}** | **{_sqlLog.Sum(s => s.Ms):F0}** | | **{_phases.Sum(p => p.MemDeltaKB):N0}** | | |");
                return sb.ToString();
            }
        }

        public static string BuildHistogramSection()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("## 📈 HISTOGRAM (ASCII) — faza vs czas");
                sb.AppendLine();
                sb.AppendLine("```");
                double max = _phases.Count == 0 ? 1 : _phases.Max(p => p.Ms);
                if (max < 1) max = 1;
                foreach (var p in _phases)
                {
                    int bars = (int)Math.Round(p.Ms / max * 50);
                    if (bars < 1 && p.Ms > 0) bars = 1;
                    string bar = new string('█', bars);
                    string name = p.Name.Length > 50 ? p.Name.Substring(0, 47) + "..." : p.Name.PadRight(50);
                    sb.AppendLine($"{name} │{bar.PadRight(50)}│ {p.Ms,6:F0} ms");
                }
                sb.AppendLine("```");
                return sb.ToString();
            }
        }

        public static string BuildSqlSection()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("## 🐢 TOP 20 NAJWOLNIEJSZYCH ZAPYTAN SQL");
                sb.AppendLine();
                sb.AppendLine("| # | Faza | Etykieta | DB | ms | Wierszy | Podglad |");
                sb.AppendLine("|---|------|----------|----|---:|-------:|---------|");
                int i = 1;
                foreach (var s in _sqlLog.OrderByDescending(s => s.Ms).Take(20))
                {
                    string flag = s.Ms > 1000 ? "🔴" : s.Ms > 500 ? "🟡" : s.Ms > 200 ? "⚠" : "";
                    sb.AppendLine($"| {i++} | {s.Phase} | {flag} {s.Label} | {s.Database ?? "-"} | {s.Ms:F0} | {s.Rows} | `{s.Preview ?? "(brak)"}` |");
                }

                // N+1 detection
                sb.AppendLine();
                sb.AppendLine("## 🔁 DETEKCJA N+1 (te same label-e ≥3×)");
                sb.AppendLine();
                var groups = _sqlLog.GroupBy(s => s.Label).Where(g => g.Count() >= 3).OrderByDescending(g => g.Count()).ToList();
                if (groups.Count == 0) sb.AppendLine("_Brak — zadne zapytanie nie wykonalo sie >=3 razy._");
                else
                {
                    sb.AppendLine("| Label | Liczba | Total ms | Avg ms | Faza |");
                    sb.AppendLine("|-------|-------:|--------:|------:|------|");
                    foreach (var g in groups)
                    {
                        sb.AppendLine($"| {g.Key} | {g.Count()} | {g.Sum(x => x.Ms):F0} | {g.Average(x => x.Ms):F1} | {g.First().Phase} |");
                    }
                }

                // Per-DB summary
                sb.AppendLine();
                sb.AppendLine("## 🗄 SQL — PODSUMOWANIE per DATABASE");
                sb.AppendLine();
                sb.AppendLine("| Database | Liczba | Total ms | Avg ms | Wierszy |");
                sb.AppendLine("|----------|-------:|--------:|------:|-------:|");
                foreach (var g in _sqlLog.GroupBy(s => s.Database ?? "(brak)").OrderByDescending(g => g.Sum(s => s.Ms)))
                {
                    sb.AppendLine($"| {g.Key} | {g.Count()} | {g.Sum(s => s.Ms):F0} | {g.Average(s => s.Ms):F1} | {g.Sum(s => s.Rows):N0} |");
                }
                return sb.ToString();
            }
        }

        public static string BuildPhaseDrillDown()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("## 🔍 DRILL-DOWN — SQL per FAZA (chronologicznie)");
                sb.AppendLine();
                foreach (var p in _phases)
                {
                    var sqls = _sqlLog.Where(s => s.Phase == p.Name).ToList();
                    sb.AppendLine($"### {p.Index}. {p.Name} — {p.Ms:F0} ms ({sqls.Count} SQL, {sqls.Sum(s => s.Ms):F0} ms total)");
                    if (sqls.Count == 0) { sb.AppendLine("_brak SQL w tej fazie_"); sb.AppendLine(); continue; }
                    sb.AppendLine();
                    sb.AppendLine("| # | ms | Rows | DB | Etykieta | SQL |");
                    sb.AppendLine("|---|---:|----:|----|----------|-----|");
                    int i = 1;
                    foreach (var s in sqls)
                    {
                        string flag = s.Ms > 500 ? "🔴" : s.Ms > 200 ? "🟡" : "";
                        sb.AppendLine($"| {i++} | {flag} {s.Ms:F0} | {s.Rows} | {s.Database ?? "-"} | {s.Label} | `{s.Preview ?? "(brak)"}` |");
                    }
                    sb.AppendLine();
                }
                return sb.ToString();
            }
        }

        public static string BuildDbSnapshotSection()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("## 🗃 SNAPSHOT BAZY DANYCH");
                sb.AppendLine();
                if (_snapshot.Count == 0) { sb.AppendLine("_Snapshot jeszcze nie wykonany — uzyj przycisku 'Snapshot teraz'._"); return sb.ToString(); }
                sb.AppendLine("| Database | Tabela | Metryka | Wartosc | ms |");
                sb.AppendLine("|----------|--------|---------|---------|---:|");
                foreach (var s in _snapshot) sb.AppendLine($"| {s.Database} | {s.Table} | {s.Metric} | **{s.Value}** | {s.Ms:F0} |");
                return sb.ToString();
            }
        }

        public static string BuildNetworkSection()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("## 🌐 SIEC — Connection probe");
                sb.AppendLine();
                if (_probes.Count == 0) { sb.AppendLine("_Probe jeszcze nie wykonany._"); return sb.ToString(); }
                sb.AppendLine("| DB | Server | Database | Ping ms | Open ms | Query ms | Server Version | Error |");
                sb.AppendLine("|----|--------|----------|--------:|--------:|---------:|----------------|-------|");
                foreach (var p in _probes) sb.AppendLine($"| {p.Label} | {p.Server} | {p.Database} | {(p.PingMs?.ToString() ?? "?")} | {p.OpenMs:F0} | {p.QueryMs:F0} | {p.ServerVersion ?? "-"} | {p.Error ?? "-"} |");
                return sb.ToString();
            }
        }

        public static string BuildWpfSection()
        {
            var sb = new StringBuilder();
            sb.AppendLine("## 🎨 WPF — Visual tree stats");
            sb.AppendLine();
            if (_wpfStats == null) { sb.AppendLine("_Stats jeszcze nie wykonane._"); return sb.ToString(); }
            sb.AppendLine("| Metryka | Wartosc |");
            sb.AppendLine("|---------|--------:|");
            sb.AppendLine($"| Tytul okna | {_wpfStats.WindowTitle} |");
            sb.AppendLine($"| Rozmiar | {_wpfStats.Width:F0} × {_wpfStats.Height:F0} |");
            sb.AppendLine($"| Visual tree (rekurencyjnie) | **{_wpfStats.VisualTreeCount}** elementow |");
            sb.AppendLine($"| Nazwane (x:Name) | {_wpfStats.NamedElements} |");
            sb.AppendLine($"| Binding-i | {_wpfStats.Bindings} |");
            sb.AppendLine($"| Bitmapy (Image) | {_wpfStats.Bitmaps} |");
            sb.AppendLine($"| Kolumny DataGrid | {_wpfStats.DataGridColumns} |");
            sb.AppendLine($"| Render tier | {_wpfStats.RenderTier} |");
            return sb.ToString();
        }

        public static string BuildNotesSection()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("## 📝 NOTATKI DIAGNOSTYCZNE");
                sb.AppendLine();
                if (_notes.Count == 0) { sb.AppendLine("_brak_"); return sb.ToString(); }
                foreach (var n in _notes) sb.AppendLine($"- **{n.Key}:** {n.Value}");
                return sb.ToString();
            }
        }

        public static string BuildCsvSql()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Index;Phase;Label;Database;Ms;Rows;Error;Preview");
                foreach (var s in _sqlLog)
                    sb.AppendLine($"{s.Index};\"{s.Phase}\";\"{Escape(s.Label)}\";{s.Database};{s.Ms.ToString("F2", CultureInfo.InvariantCulture)};{s.Rows};\"{Escape(s.Error)}\";\"{Escape(s.Preview)}\"");
                return sb.ToString();
            }
        }

        public static string BuildJson()
        {
            lock (_lock)
            {
                // Prosty JSON bez external lib
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine($"  \"startedAt\": \"{_startedAt:O}\",");
                sb.AppendLine($"  \"wallMs\": {_wallClock.Elapsed.TotalMilliseconds:F1},");
                sb.AppendLine($"  \"userId\": \"{Escape(_userId)}\",");
                sb.AppendLine($"  \"machineInfo\": \"{Escape(_machineInfo)}\",");
                sb.AppendLine($"  \"memBeforeMB\": {_memBeforeBytes / 1024 / 1024},");
                sb.AppendLine($"  \"memAfterMB\": {_memAfterBytes / 1024 / 1024},");
                sb.AppendLine($"  \"phaseCount\": {_phases.Count},");
                sb.AppendLine($"  \"sqlCount\": {_sqlLog.Count},");
                sb.AppendLine($"  \"phases\": [");
                for (int i = 0; i < _phases.Count; i++)
                {
                    var p = _phases[i];
                    sb.Append($"    {{ \"index\": {p.Index}, \"name\": \"{Escape(p.Name)}\", \"ms\": {p.Ms:F1}, \"sqlCount\": {p.SqlCount}, \"sqlMs\": {p.SqlMs:F1}, \"memDeltaKB\": {p.MemDeltaKB} }}");
                    sb.AppendLine(i < _phases.Count - 1 ? "," : "");
                }
                sb.AppendLine("  ],");
                sb.AppendLine($"  \"sqls\": [");
                for (int i = 0; i < _sqlLog.Count; i++)
                {
                    var s = _sqlLog[i];
                    sb.Append($"    {{ \"index\": {s.Index}, \"phase\": \"{Escape(s.Phase)}\", \"label\": \"{Escape(s.Label)}\", \"db\": \"{Escape(s.Database)}\", \"ms\": {s.Ms:F1}, \"rows\": {s.Rows} }}");
                    sb.AppendLine(i < _sqlLog.Count - 1 ? "," : "");
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");
                return sb.ToString();
            }
        }

        // ===== Helpers =====
        private static string Escape(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
        private static string Truncate(string s, int max) { if (string.IsNullOrEmpty(s)) return s; return s.Length <= max ? s : s.Substring(0, max - 1) + "…"; }
        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / 1024.0 / 1024.0).ToString("F1") + " MB";
            return (bytes / 1024.0 / 1024.0 / 1024.0).ToString("F2") + " GB";
        }

        private sealed class ActionScope : IDisposable
        {
            private readonly Action _onDispose;
            public ActionScope(Action a) { _onDispose = a; }
            public void Dispose() { _onDispose?.Invoke(); }
        }

        private sealed class SqlScope : IDisposable
        {
            private readonly SqlEntry _entry;
            private readonly Stopwatch _sw;
            public SqlScope(SqlEntry e, Stopwatch sw) { _entry = e; _sw = sw; }
            public void Dispose()
            {
                _sw.Stop();
                _entry.Ms = _sw.Elapsed.TotalMilliseconds;
                lock (_lock)
                {
                    _sqlLog.Add(_entry);
                    if (_currentPhase != null && _currentPhase.Name == _entry.Phase)
                    {
                        _currentPhase.SqlCount++;
                        _currentPhase.SqlMs += _entry.Ms;
                    }
                }
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose() { }
        }
    }
}
