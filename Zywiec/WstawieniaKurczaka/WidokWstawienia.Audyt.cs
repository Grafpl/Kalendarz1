using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;

namespace Kalendarz1
{
    // ============================================================================
    // ROZBUDOWANY DEBUGGER ŁADOWANIA — pełna diagnostyka
    //
    // Zbiera:
    //  • System snapshot (RAM, GC, threads, handles) przed i po ładowaniu
    //  • Server snapshot (ping, wersja, drift czasu, MARS)
    //  • Fazy ładowania z sub-timings
    //  • Pełne SQL queries z parametrami
    //  • STATISTICS IO/TIME z każdego zapytania (logical reads, CPU time)
    //  • Indeksy na używanych tabelach (sys.indexes)
    //  • Liczbę wierszy w tabelach
    //  • Definicję widoku v_WstawieniaDoKontaktu
    //  • Statystyki cache (delivery, avatar)
    //  • Listę dostawców z duplikatami DataGrid
    //
    // Wszystko renderowane do tekstowego raportu copy-paste-friendly.
    // ============================================================================

    internal class LoadingAudit
    {
        public Stopwatch Total { get; } = Stopwatch.StartNew();
        public List<PhaseMetric> Phases { get; } = new();
        public PhaseMetric? Current { get; private set; }

        // Pełna diagnostyka
        public SystemSnapshot? SystemBefore { get; set; }
        public SystemSnapshot? SystemAfter { get; set; }
        public ServerSnapshot? Server { get; set; }
        public List<TableInfo> TableInfos { get; } = new();
        public List<IndexInfo> Indexes { get; } = new();
        public CacheStats? Cache { get; set; }
        public List<QueryLog> Queries { get; } = new();
        public QueryLog? CurrentQuery { get; private set; }
        public string? ViewDefinition_v_WstawieniaDoKontaktu { get; set; }
        public string? DiagError { get; set; }

        // SqlConnection.RetrieveStatistics() agregacja (z każdej Load* funkcji)
        public List<ConnStats> ConnectionStats { get; } = new();
        public List<MissingIndexHint> MissingIndexHints { get; } = new();
        public List<IndexUsage> IndexUsages { get; } = new();
        public List<TableSample> TableSamples { get; } = new();
        public PingBenchmark? PingBench { get; set; }
        public string? ShowPlanText { get; set; }
        public bool DiagDbCacheHit { get; set; }
        public bool DeepMode { get; set; }  // false = light (auto), true = pełna diagnostyka (button)

        public IDisposable Begin(string name)
        {
            var p = new PhaseMetric
            {
                Name = name,
                Sw = Stopwatch.StartNew(),
                StartOffsetMs = Total.ElapsedMilliseconds
            };
            Phases.Add(p);
            Current = p;
            return new Stopper(this, p);
        }

        public QueryLog BeginQuery(string label, string sql)
        {
            var q = new QueryLog
            {
                Label = label,
                Sql = sql,
                StartOffsetMs = Total.ElapsedMilliseconds,
                Sw = Stopwatch.StartNew()
            };
            Queries.Add(q);
            CurrentQuery = q;
            return q;
        }

        public void EndQuery(int? rowCount = null)
        {
            if (CurrentQuery != null)
            {
                CurrentQuery.Sw.Stop();
                CurrentQuery.RowCount = rowCount;
                CurrentQuery = null;
            }
        }

        public void Note(string text)
        {
            if (Current != null) Current.Notes.Add(text);
        }

        public void RowCount(int n)
        {
            if (Current != null) Current.RowCount = n;
        }

        public void Sub(string label, long ms)
        {
            if (Current != null) Current.SubTimes[label] = ms;
        }

        public class PhaseMetric
        {
            public string Name { get; set; } = "";
            public Stopwatch Sw { get; set; } = new();
            public long StartOffsetMs { get; set; }
            public int? RowCount { get; set; }
            public Dictionary<string, long> SubTimes { get; } = new();
            public List<string> Notes { get; } = new();
        }

        private class Stopper : IDisposable
        {
            private readonly LoadingAudit _a;
            private readonly PhaseMetric _p;
            public Stopper(LoadingAudit a, PhaseMetric p) { _a = a; _p = p; }
            public void Dispose()
            {
                _p.Sw.Stop();
                if (ReferenceEquals(_a.Current, _p)) _a.Current = null;
            }
        }
    }

    public class SystemSnapshot
    {
        public DateTime Timestamp { get; set; }
        public string MachineName { get; set; } = "";
        public string OS { get; set; } = "";
        public string DotNet { get; set; } = "";
        public int CpuCount { get; set; }
        public long WorkingSetMB { get; set; }
        public long PrivateMemoryMB { get; set; }
        public long GcManagedMB { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public int ProcessThreads { get; set; }
        public int HandleCount { get; set; }
        public TimeSpan ProcessTotalCpu { get; set; }
    }

    public class ServerSnapshot
    {
        public long PingMs { get; set; }
        public string Version { get; set; } = "";
        public DateTime ServerNow { get; set; }
        public DateTime ClientNow { get; set; }
        public TimeSpan ClockDrift { get; set; }
        public string CurrentDatabase { get; set; } = "";
        public string LoginName { get; set; } = "";
        public int ActiveConnections { get; set; }
        public string ConnectionString { get; set; } = "";
        public string? Error { get; set; }
    }

    public class TableInfo
    {
        public string Name { get; set; } = "";
        public long RowCount { get; set; }
        public long DataSizeKB { get; set; }
        public long IndexSizeKB { get; set; }
    }

    public class IndexInfo
    {
        public string TableName { get; set; } = "";
        public string IndexName { get; set; } = "";
        public string IndexType { get; set; } = "";  // CLUSTERED / NONCLUSTERED / HEAP
        public string KeyColumns { get; set; } = "";
        public string IncludedColumns { get; set; } = "";
        public bool IsUnique { get; set; }
        public bool IsPrimaryKey { get; set; }
    }

    public class CacheStats
    {
        public bool DeliveryCacheLoaded { get; set; }
        public DateTime DeliveryCacheTimestamp { get; set; }
        public int DeliveryCacheAge { get; set; }
        public int DeliveryCacheKeys { get; set; }
        public int DeliveryCacheRows { get; set; }
        public int AvatarCacheKeys { get; set; }
        public int AvatarMissingCount { get; set; }
    }

    public class QueryLog
    {
        public string Label { get; set; } = "";
        public string Sql { get; set; } = "";
        public Dictionary<string, object?> Parameters { get; set; } = new();
        public long StartOffsetMs { get; set; }
        public Stopwatch Sw { get; set; } = new();
        public List<string> InfoMessages { get; } = new();
        public int? RowCount { get; set; }
    }

    public class ConnStats
    {
        public string Label { get; set; } = "";
        public long BytesReceived { get; set; }
        public long BytesSent { get; set; }
        public long ConnectionTime { get; set; }   // ms — czas utrzymywania conn
        public long ExecutionTime { get; set; }    // ms — od open do close
        public long NetworkServerTime { get; set; } // ms — oczekiwanie na serwer
        public long ServerRoundtrips { get; set; }
        public long SelectCount { get; set; }
        public long SelectRows { get; set; }
        public long SumResultSets { get; set; }
        public long Transactions { get; set; }
        public long IduCount { get; set; }
        public long IduRows { get; set; }
    }

    public class MissingIndexHint
    {
        public string TableName { get; set; } = "";
        public string EqualityColumns { get; set; } = "";
        public string InequalityColumns { get; set; } = "";
        public string IncludedColumns { get; set; } = "";
        public double ImpactScore { get; set; }  // avg_user_impact * (seeks + scans)
        public long UserSeeks { get; set; }
        public long UserScans { get; set; }
        public double AvgImpactPct { get; set; }
        public string SuggestedDDL { get; set; } = "";
    }

    public class IndexUsage
    {
        public string TableName { get; set; } = "";
        public string IndexName { get; set; } = "";
        public long UserSeeks { get; set; }
        public long UserScans { get; set; }
        public long UserLookups { get; set; }
        public long UserUpdates { get; set; }
        public DateTime? LastUserSeek { get; set; }
        public DateTime? LastUserScan { get; set; }
    }

    public class TableSample
    {
        public string TableName { get; set; } = "";
        public List<string> Columns { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
    }

    public class PingBenchmark
    {
        public List<long> Samples { get; set; } = new();
        public long Min => Samples.Count == 0 ? 0 : Samples.Min();
        public long Max => Samples.Count == 0 ? 0 : Samples.Max();
        public double Avg => Samples.Count == 0 ? 0 : Samples.Average();
        public long P50 => Percentile(50);
        public long P95 => Percentile(95);
        private long Percentile(int p)
        {
            if (Samples.Count == 0) return 0;
            var sorted = Samples.OrderBy(x => x).ToList();
            int idx = (int)Math.Ceiling(p / 100.0 * sorted.Count) - 1;
            return sorted[Math.Max(0, Math.Min(idx, sorted.Count - 1))];
        }
    }

    public partial class WidokWstawienia
    {
        private static bool _audytPokazany = false;
        internal LoadingAudit? _audyt;

        // Static cache dla diagnostyki DB — sys.indexes/sys.tables nie zmieniają się
        // często, więc po pierwszym zebraniu reusujemy przez 30 minut.
        private static List<TableInfo>? _cachedTableInfos;
        private static List<IndexInfo>? _cachedIndexes;
        private static string? _cachedViewDef;
        private static DateTime _diagDbCacheTimestamp = DateTime.MinValue;
        private const int DIAG_DB_CACHE_TTL_MIN = 30;

        // ===== ZBIERANIE SYSTEM SNAPSHOT =====
        private static SystemSnapshot ZbierzSystemSnapshot()
        {
            try
            {
                var p = Process.GetCurrentProcess();
                return new SystemSnapshot
                {
                    Timestamp = DateTime.Now,
                    MachineName = Environment.MachineName,
                    OS = $"{Environment.OSVersion.VersionString} (64-bit: {Environment.Is64BitOperatingSystem})",
                    DotNet = Environment.Version.ToString(),
                    CpuCount = Environment.ProcessorCount,
                    WorkingSetMB = p.WorkingSet64 / 1024 / 1024,
                    PrivateMemoryMB = p.PrivateMemorySize64 / 1024 / 1024,
                    GcManagedMB = GC.GetTotalMemory(false) / 1024 / 1024,
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2),
                    ProcessThreads = p.Threads.Count,
                    HandleCount = p.HandleCount,
                    ProcessTotalCpu = p.TotalProcessorTime
                };
            }
            catch
            {
                return new SystemSnapshot { Timestamp = DateTime.Now, MachineName = Environment.MachineName };
            }
        }

        // ===== ZBIERANIE SERVER SNAPSHOT =====
        private static ServerSnapshot ZbierzServerSnapshot(string connectionString)
        {
            var snap = new ServerSnapshot
            {
                ClientNow = DateTime.Now,
                ConnectionString = MaskuPassword(connectionString)
            };
            try
            {
                using var conn = new SqlConnection(connectionString);
                var sw = Stopwatch.StartNew();
                conn.Open();
                using (var cmd = new SqlCommand("SELECT 1", conn))
                    cmd.ExecuteScalar();
                snap.PingMs = sw.ElapsedMilliseconds;

                // UWAGA: sys.dm_exec_connections wymaga VIEW SERVER STATE → zwykle deny.
                // @@CONNECTIONS to LICZNIK SESJI od startu serwera (nie aktualne), ale jest dostępny.
                using (var cmd = new SqlCommand(@"
                    SELECT
                        CAST(@@VERSION AS VARCHAR(500)) AS Version,
                        GETDATE() AS ServerNow,
                        DB_NAME() AS CurrentDb,
                        SUSER_NAME() AS LoginName,
                        @@CONNECTIONS AS ActiveConn", conn))
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        snap.Version = (r["Version"]?.ToString() ?? "").Replace("\n", " ").Replace("\t", " ").Trim();
                        snap.ServerNow = r["ServerNow"] != DBNull.Value ? Convert.ToDateTime(r["ServerNow"]) : DateTime.MinValue;
                        snap.CurrentDatabase = r["CurrentDb"]?.ToString() ?? "";
                        snap.LoginName = r["LoginName"]?.ToString() ?? "";
                        snap.ActiveConnections = r["ActiveConn"] != DBNull.Value ? Convert.ToInt32(r["ActiveConn"]) : 0;
                    }
                }
                snap.ClockDrift = snap.ServerNow - snap.ClientNow;
            }
            catch (Exception ex)
            {
                snap.Error = ex.Message;
            }
            return snap;
        }

        private static string MaskuPassword(string cs)
        {
            // Ukryj hasło dla raportu
            var sb = new StringBuilder();
            foreach (var part in cs.Split(';'))
            {
                if (part.Trim().StartsWith("Password", StringComparison.OrdinalIgnoreCase))
                    sb.Append("Password=***;");
                else
                {
                    sb.Append(part);
                    sb.Append(';');
                }
            }
            return sb.ToString().TrimEnd(';');
        }

        // ===== DIAGNOSTYKA DB (jedna runda, 3 result sety zamiast N+1) =====
        // Wcześniejsza implementacja robiła 5+ round-tripów. Teraz: 1 connection,
        // 1 SqlCommand z 3 SELECT-ami (tabele, indeksy, widok), iteracja przez NextResult().
        //
        // OPTYMALIZACJA: wynik cachowany statycznie przez 30 min (sys.indexes/sys.tables
        // nie zmieniają się dynamicznie). Cache HIT = 0 ms (nawet bez round-tripa).
        private static void ZbierzDiagnostykeDb(string connectionString, IEnumerable<string> tables,
            string viewName, LoadingAudit audit)
        {
            // CACHE HIT: użyj static jeśli świeży
            if (_cachedTableInfos != null && _cachedIndexes != null &&
                (DateTime.Now - _diagDbCacheTimestamp).TotalMinutes < DIAG_DB_CACHE_TTL_MIN)
            {
                audit.TableInfos.AddRange(_cachedTableInfos);
                audit.Indexes.AddRange(_cachedIndexes);
                audit.ViewDefinition_v_WstawieniaDoKontaktu = _cachedViewDef;
                audit.DiagDbCacheHit = true;
                audit.Current?.Notes.Add($"Cache HIT (wiek {(DateTime.Now - _diagDbCacheTimestamp).TotalSeconds:0} s, TTL {DIAG_DB_CACHE_TTL_MIN} min)");
                return;
            }
            string tableList = string.Join(",", tables.Select(t => "'" + t.Replace("'", "''") + "'"));
            string sql = $@"
                SET NOCOUNT ON;

                IF OBJECT_ID('tempdb..#tabs') IS NOT NULL DROP TABLE #tabs;
                CREATE TABLE #tabs (Name SYSNAME, ObjId INT);
                INSERT INTO #tabs (Name, ObjId)
                SELECT s.[Name], OBJECT_ID(s.[Name])
                FROM (VALUES {string.Join(",", tables.Select(t => "(N'" + t.Replace("'", "''") + "')"))}) s([Name]);

                -- ===== RS1: ROZMIARY TABEL =====
                SELECT
                    t.Name AS TableName,
                    ISNULL(SUM(p.rows), 0) AS RowCnt,
                    ISNULL(SUM(a.total_pages), 0) * 8 AS TotalKB,
                    ISNULL(SUM(CASE WHEN a.type = 1 THEN a.used_pages ELSE 0 END), 0) * 8 AS DataKB
                FROM #tabs t
                LEFT JOIN sys.indexes idx ON idx.object_id = t.ObjId AND idx.index_id < 2
                LEFT JOIN sys.partitions p ON idx.object_id = p.object_id AND idx.index_id = p.index_id
                LEFT JOIN sys.allocation_units a ON p.partition_id = a.container_id
                GROUP BY t.Name;

                -- ===== RS2: INDEKSY (klucze + INCLUDE z FOR XML PATH) =====
                SELECT
                    t.Name AS TableName,
                    i.name AS IndexName,
                    i.type_desc AS IndexType,
                    i.is_unique,
                    i.is_primary_key,
                    STUFF((
                        SELECT ', ' + c.name + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END
                        FROM sys.index_columns ic
                        JOIN sys.columns c ON c.object_id = i.object_id AND c.column_id = ic.column_id
                        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                        ORDER BY ic.key_ordinal
                        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS KeyCols,
                    STUFF((
                        SELECT ', ' + c.name
                        FROM sys.index_columns ic
                        JOIN sys.columns c ON c.object_id = i.object_id AND c.column_id = ic.column_id
                        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
                        ORDER BY ic.key_ordinal
                        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS IncludeCols
                FROM #tabs t
                JOIN sys.indexes i ON i.object_id = t.ObjId
                WHERE i.type > 0  -- skip HEAP itself (pokazujemy go osobno przez markera type_desc=HEAP)
                UNION ALL
                -- HEAP markery dla tabel BEZ indeksów (i.type = 0 = HEAP)
                SELECT
                    t.Name AS TableName,
                    '(HEAP — brak clustered/PK)' AS IndexName,
                    'HEAP' AS IndexType,
                    CAST(0 AS BIT) AS is_unique,
                    CAST(0 AS BIT) AS is_primary_key,
                    '' AS KeyCols,
                    '' AS IncludeCols
                FROM #tabs t
                JOIN sys.indexes i ON i.object_id = t.ObjId AND i.type = 0
                WHERE NOT EXISTS (
                    SELECT 1 FROM sys.indexes i2 WHERE i2.object_id = t.ObjId AND i2.type = 1
                )
                ORDER BY TableName, is_primary_key DESC, IndexType;

                -- ===== RS3: DEFINICJA WIDOKU =====
                SELECT OBJECT_DEFINITION(OBJECT_ID(@v)) AS ViewDef;

                DROP TABLE #tabs;";

            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
                cmd.Parameters.AddWithValue("@v", viewName);
                using var r = cmd.ExecuteReader();

                // RS1: tabele
                while (r.Read())
                {
                    long rowCnt = r["RowCnt"] != DBNull.Value ? Convert.ToInt64(r["RowCnt"]) : 0;
                    long total = r["TotalKB"] != DBNull.Value ? Convert.ToInt64(r["TotalKB"]) : 0;
                    long data = r["DataKB"] != DBNull.Value ? Convert.ToInt64(r["DataKB"]) : 0;
                    audit.TableInfos.Add(new TableInfo
                    {
                        Name = r["TableName"]?.ToString() ?? "",
                        RowCount = rowCnt,
                        DataSizeKB = data,
                        IndexSizeKB = total - data
                    });
                }

                // RS2: indeksy
                r.NextResult();
                while (r.Read())
                {
                    audit.Indexes.Add(new IndexInfo
                    {
                        TableName = r["TableName"]?.ToString() ?? "",
                        IndexName = r["IndexName"]?.ToString() ?? "",
                        IndexType = r["IndexType"]?.ToString() ?? "",
                        IsUnique = r["is_unique"] != DBNull.Value && Convert.ToBoolean(r["is_unique"]),
                        IsPrimaryKey = r["is_primary_key"] != DBNull.Value && Convert.ToBoolean(r["is_primary_key"]),
                        KeyColumns = r["KeyCols"]?.ToString() ?? "",
                        IncludedColumns = r["IncludeCols"]?.ToString() ?? ""
                    });
                }

                // RS3: definicja widoku
                r.NextResult();
                if (r.Read())
                {
                    var def = r["ViewDef"];
                    if (def != DBNull.Value && def != null)
                        audit.ViewDefinition_v_WstawieniaDoKontaktu = def.ToString();
                }

                // Cache dla kolejnych wywołań w tej sesji
                _cachedTableInfos = audit.TableInfos.ToList();
                _cachedIndexes = audit.Indexes.ToList();
                _cachedViewDef = audit.ViewDefinition_v_WstawieniaDoKontaktu;
                _diagDbCacheTimestamp = DateTime.Now;
            }
            catch (Exception ex)
            {
                audit.DiagError = (audit.DiagError ?? "") + "[Diag DB] " + ex.Message + "\n";
            }
        }

        // ===== PING BENCHMARK =====
        // Wykonuje N krótkich SELECT 1 żeby zmierzyć stabilność/jitter sieci LAN.
        private static PingBenchmark ZbierzPingBenchmark(string connectionString, int samples = 5)
        {
            var bench = new PingBenchmark();
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();
                using var cmd = new SqlCommand("SELECT 1", conn);
                for (int i = 0; i < samples; i++)
                {
                    var sw = Stopwatch.StartNew();
                    cmd.ExecuteScalar();
                    bench.Samples.Add(sw.ElapsedMilliseconds);
                }
            }
            catch { }
            return bench;
        }

        // ===== SAMPLE DANYCH (TOP 3 wiersze z każdej tabeli) =====
        // Pokazuje strukturę realnych danych — pomaga zrozumieć co tam jest naprawdę.
        private static List<TableSample> ZbierzSampleTabel(string connectionString, IEnumerable<string> tables)
        {
            var wynik = new List<TableSample>();
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();
                foreach (var t in tables)
                {
                    var sample = new TableSample { TableName = t };
                    try
                    {
                        using var cmd = new SqlCommand($"SELECT TOP 3 * FROM {t}", conn);
                        using var r = cmd.ExecuteReader();
                        for (int c = 0; c < r.FieldCount; c++)
                            sample.Columns.Add(r.GetName(c));
                        while (r.Read())
                        {
                            var row = new List<string>();
                            for (int c = 0; c < r.FieldCount; c++)
                            {
                                if (r.IsDBNull(c)) row.Add("NULL");
                                else
                                {
                                    string val = r.GetValue(c)?.ToString() ?? "";
                                    if (val.Length > 60) val = val.Substring(0, 57) + "...";
                                    row.Add(val);
                                }
                            }
                            sample.Rows.Add(row);
                        }
                    }
                    catch { /* tabela nieosiągalna — pomijamy */ }
                    if (sample.Columns.Count > 0)
                        wynik.Add(sample);
                }
            }
            catch { }
            return wynik;
        }

        // ===== CONNECTION STATS (per Load*) =====
        // Wywoływane przed Dispose connection, czyta SqlConnection.RetrieveStatistics().
        private void ZbierzConnStats(SqlConnection conn, string label)
        {
            if (_audyt == null) return;
            try
            {
                if (!conn.StatisticsEnabled) return;
                var stats = conn.RetrieveStatistics();
                if (stats == null) return;

                long Get(string key)
                {
                    if (stats.Contains(key) && stats[key] is long l) return l;
                    if (stats.Contains(key) && stats[key] is int i) return i;
                    return 0;
                }

                _audyt.ConnectionStats.Add(new ConnStats
                {
                    Label = label,
                    BytesReceived = Get("BytesReceived"),
                    BytesSent = Get("BytesSent"),
                    ConnectionTime = Get("ConnectionTime"),
                    ExecutionTime = Get("ExecutionTime"),
                    NetworkServerTime = Get("NetworkServerTime"),
                    ServerRoundtrips = Get("ServerRoundtrips"),
                    SelectCount = Get("SelectCount"),
                    SelectRows = Get("SelectRows"),
                    SumResultSets = Get("SumResultSets"),
                    Transactions = Get("Transactions"),
                    IduCount = Get("IduCount"),
                    IduRows = Get("IduRows")
                });
            }
            catch { }
        }

        // ===== DMV — MISSING INDEX SUGGESTIONS =====
        // SQL Server analizuje plany wykonania ostatnich zapytań i podpowiada indeksy
        // które uczyniłyby JE szybszymi. To DARMOWA wartość — wymaga tylko VIEW SERVER STATE.
        private static List<MissingIndexHint> ZbierzMissingIndexHints(string connectionString, string dbName)
        {
            var wynik = new List<MissingIndexHint>();
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();
                // Bezpieczne — jeśli user nie ma VIEW SERVER STATE, query zwróci pustkę
                // (a nie błąd) ponieważ DMV są dostępne tylko z odpowiednim grantem.
                string sql = @"
                    SELECT TOP 20
                        OBJECT_NAME(d.object_id) AS TableName,
                        d.equality_columns,
                        d.inequality_columns,
                        d.included_columns,
                        s.user_seeks,
                        s.user_scans,
                        s.avg_user_impact,
                        s.avg_user_impact * (s.user_seeks + s.user_scans) AS Score
                    FROM sys.dm_db_missing_index_groups g
                    JOIN sys.dm_db_missing_index_group_stats s ON g.index_group_handle = s.group_handle
                    JOIN sys.dm_db_missing_index_details d ON g.index_handle = d.index_handle
                    WHERE d.database_id = DB_ID(@db)
                    ORDER BY Score DESC";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@db", dbName);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var hint = new MissingIndexHint
                    {
                        TableName = r["TableName"]?.ToString() ?? "",
                        EqualityColumns = r["equality_columns"]?.ToString() ?? "",
                        InequalityColumns = r["inequality_columns"]?.ToString() ?? "",
                        IncludedColumns = r["included_columns"]?.ToString() ?? "",
                        UserSeeks = r["user_seeks"] != DBNull.Value ? Convert.ToInt64(r["user_seeks"]) : 0,
                        UserScans = r["user_scans"] != DBNull.Value ? Convert.ToInt64(r["user_scans"]) : 0,
                        AvgImpactPct = r["avg_user_impact"] != DBNull.Value ? Convert.ToDouble(r["avg_user_impact"]) : 0,
                        ImpactScore = r["Score"] != DBNull.Value ? Convert.ToDouble(r["Score"]) : 0
                    };

                    var keyParts = new List<string>();
                    if (!string.IsNullOrEmpty(hint.EqualityColumns)) keyParts.Add(hint.EqualityColumns);
                    if (!string.IsNullOrEmpty(hint.InequalityColumns)) keyParts.Add(hint.InequalityColumns);
                    string keys = string.Join(", ", keyParts);
                    string incl = string.IsNullOrEmpty(hint.IncludedColumns)
                        ? ""
                        : $"\n    INCLUDE ({hint.IncludedColumns})";
                    string ddlIdxName = "IX_" + hint.TableName + "_AutoSuggested_" + Math.Abs(keys.GetHashCode()).ToString().Substring(0, Math.Min(6, Math.Abs(keys.GetHashCode()).ToString().Length));
                    hint.SuggestedDDL = $"CREATE NONCLUSTERED INDEX {ddlIdxName}\n    ON dbo.{hint.TableName} ({keys}){incl};";
                    wynik.Add(hint);
                }
            }
            catch { /* VIEW SERVER STATE denied — pomijamy cicho */ }
            return wynik;
        }

        // ===== DMV — INDEX USAGE =====
        // Pokazuje które indeksy są realnie używane (seeks/scans) vs ignorowane (0/0).
        // Pomaga znaleźć indeksy do USUNIĘCIA (zajmują miejsce, spowalniają INSERT/UPDATE).
        private static List<IndexUsage> ZbierzIndexUsage(string connectionString, IEnumerable<string> tables)
        {
            var wynik = new List<IndexUsage>();
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();
                string tableList = string.Join(",", tables.Select(t => "OBJECT_ID(N'" + t.Replace("'", "''") + "')"));
                if (string.IsNullOrEmpty(tableList)) return wynik;

                string sql = $@"
                    SELECT
                        OBJECT_NAME(i.object_id) AS TableName,
                        i.name AS IndexName,
                        ISNULL(us.user_seeks, 0) AS user_seeks,
                        ISNULL(us.user_scans, 0) AS user_scans,
                        ISNULL(us.user_lookups, 0) AS user_lookups,
                        ISNULL(us.user_updates, 0) AS user_updates,
                        us.last_user_seek,
                        us.last_user_scan
                    FROM sys.indexes i
                    LEFT JOIN sys.dm_db_index_usage_stats us
                        ON us.object_id = i.object_id
                       AND us.index_id = i.index_id
                       AND us.database_id = DB_ID()
                    WHERE i.object_id IN ({tableList})
                      AND i.type > 0
                    ORDER BY OBJECT_NAME(i.object_id), us.user_seeks + us.user_scans DESC";
                using var cmd = new SqlCommand(sql, conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    wynik.Add(new IndexUsage
                    {
                        TableName = r["TableName"]?.ToString() ?? "",
                        IndexName = r["IndexName"]?.ToString() ?? "",
                        UserSeeks = r["user_seeks"] != DBNull.Value ? Convert.ToInt64(r["user_seeks"]) : 0,
                        UserScans = r["user_scans"] != DBNull.Value ? Convert.ToInt64(r["user_scans"]) : 0,
                        UserLookups = r["user_lookups"] != DBNull.Value ? Convert.ToInt64(r["user_lookups"]) : 0,
                        UserUpdates = r["user_updates"] != DBNull.Value ? Convert.ToInt64(r["user_updates"]) : 0,
                        LastUserSeek = r["last_user_seek"] != DBNull.Value ? Convert.ToDateTime(r["last_user_seek"]) : (DateTime?)null,
                        LastUserScan = r["last_user_scan"] != DBNull.Value ? Convert.ToDateTime(r["last_user_scan"]) : (DateTime?)null
                    });
                }
            }
            catch { }
            return wynik;
        }

        // ===== ZBIERANIE CACHE STATS =====
        private CacheStats ZbierzCacheStats()
        {
            var stats = new CacheStats
            {
                DeliveryCacheLoaded = _deliveryCacheLoaded,
                DeliveryCacheTimestamp = _deliveryCacheTimestamp,
                DeliveryCacheAge = _deliveryCacheLoaded
                    ? (int)(DateTime.Now - _deliveryCacheTimestamp).TotalSeconds : -1,
                DeliveryCacheKeys = _deliveryCache?.Count ?? 0,
                DeliveryCacheRows = _deliveryCache?.Sum(kv => kv.Value?.Count ?? 0) ?? 0,
                AvatarCacheKeys = _avatarBrushCache?.Count ?? 0,
                AvatarMissingCount = _avatarMissingCache?.Count ?? 0
            };
            return stats;
        }

        // ===== BUILD REPORT =====
        private string BuildAudytRaport(LoadingAudit a)
        {
            var sb = new StringBuilder();
            long total = a.Total.ElapsedMilliseconds;

            string mode = a.DeepMode ? "DEEP (pełna diagnostyka)" : "LIGHT (auto, minimalna)";
            sb.AppendLine("🔍 AUDYT ŁADOWANIA — Cykle Wstawień (WidokWstawienia)");
            sb.AppendLine($"   Tryb: {mode}");
            if (!a.DeepMode)
                sb.AppendLine("   → Kliknij przycisk \"🔍 Audyt\" w nagłówku żeby zobaczyć PEŁNĄ diagnostykę");
            sb.AppendLine(new string('=', 78));
            sb.AppendLine();

            // === INFO OGÓLNE ===
            sb.AppendLine("📋 INFORMACJE OGÓLNE");
            sb.AppendLine(new string('-', 78));
            sb.AppendLine($"Data raportu:    {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"User:            {App.UserID}");
            sb.AppendLine($"Sesja audytu:    {(_audytPokazany ? "powtórna" : "PIERWSZA")}");
            sb.AppendLine();

            // === PODSUMOWANIE ===
            sb.AppendLine("📊 PODSUMOWANIE CZASU");
            sb.AppendLine(new string('-', 78));
            sb.AppendLine($"Całkowity czas (Total):    {total} ms");
            sb.AppendLine($"Suma czasu faz:            {a.Phases.Sum(p => p.Sw.ElapsedMilliseconds)} ms");
            sb.AppendLine($"Czas ping serwera:         {(a.Server?.PingMs.ToString() ?? "?")} ms");
            if (a.DiagDbCacheHit)
                sb.AppendLine($"Diagnostyka DB:            CACHE HIT (sys.indexes/widok z pamięci)");
            sb.AppendLine();

            // === SYSTEM ===
            sb.AppendLine("💻 SYSTEM (snapshot przed → po ładowaniu)");
            sb.AppendLine(new string('-', 78));
            if (a.SystemBefore != null && a.SystemAfter != null)
            {
                var bf = a.SystemBefore;
                var af = a.SystemAfter;
                sb.AppendLine($"Maszyna:                   {bf.MachineName}");
                sb.AppendLine($"OS:                        {bf.OS}");
                sb.AppendLine($".NET:                      {bf.DotNet}");
                sb.AppendLine($"CPU cores:                 {bf.CpuCount}");
                sb.AppendLine($"Working Set:               {bf.WorkingSetMB} MB  →  {af.WorkingSetMB} MB   (Δ {af.WorkingSetMB - bf.WorkingSetMB:+0;-0;0} MB)");
                sb.AppendLine($"Private Memory:            {bf.PrivateMemoryMB} MB  →  {af.PrivateMemoryMB} MB   (Δ {af.PrivateMemoryMB - bf.PrivateMemoryMB:+0;-0;0} MB)");
                sb.AppendLine($"GC managed memory:         {bf.GcManagedMB} MB  →  {af.GcManagedMB} MB   (Δ {af.GcManagedMB - bf.GcManagedMB:+0;-0;0} MB)");
                sb.AppendLine($"GC Gen0/Gen1/Gen2:         {bf.Gen0Collections}/{bf.Gen1Collections}/{bf.Gen2Collections}  →  {af.Gen0Collections}/{af.Gen1Collections}/{af.Gen2Collections}");
                sb.AppendLine($"Process threads:           {bf.ProcessThreads} → {af.ProcessThreads}");
                sb.AppendLine($"Handles:                   {bf.HandleCount} → {af.HandleCount}");
                sb.AppendLine($"CPU consumed (process):    {(af.ProcessTotalCpu - bf.ProcessTotalCpu).TotalMilliseconds:0} ms");
            }
            else { sb.AppendLine("(brak snapshotów)"); }
            sb.AppendLine();

            // === PING BENCHMARK === (tylko deep)
            if (a.DeepMode && a.PingBench != null && a.PingBench.Samples.Count > 0)
            {
                sb.AppendLine("📡 PING BENCHMARK (5× SELECT 1 — stabilność sieci)");
                sb.AppendLine(new string('-', 78));
                sb.AppendLine($"Próbki:    [{string.Join(", ", a.PingBench.Samples)}] ms");
                sb.AppendLine($"Min:       {a.PingBench.Min} ms");
                sb.AppendLine($"P50:       {a.PingBench.P50} ms");
                sb.AppendLine($"P95:       {a.PingBench.P95} ms");
                sb.AppendLine($"Max:       {a.PingBench.Max} ms");
                sb.AppendLine($"Avg:       {a.PingBench.Avg:0.0} ms");
                if (a.PingBench.Max > a.PingBench.Min * 3 && a.PingBench.Max > 20)
                    sb.AppendLine("⚠️  Duży jitter (max > 3× min) — sieć niestabilna lub serwer pod obciążeniem");
                sb.AppendLine();
            }

            // === SQL SERVER ===
            sb.AppendLine("🗄️  SQL SERVER");
            sb.AppendLine(new string('-', 78));
            if (a.Server != null)
            {
                if (!string.IsNullOrEmpty(a.Server.Error))
                    sb.AppendLine($"BŁĄD: {a.Server.Error}");
                else
                {
                    sb.AppendLine($"Connection:                {a.Server.ConnectionString}");
                    sb.AppendLine($"Database:                  {a.Server.CurrentDatabase}");
                    sb.AppendLine($"Login:                     {a.Server.LoginName}");
                    sb.AppendLine($"Ping (SELECT 1):           {a.Server.PingMs} ms  (oczekiwane <10 ms w LAN)");
                    sb.AppendLine($"Server now:                {a.Server.ServerNow:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"Client now:                {a.Server.ClientNow:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"Clock drift (server-cli):  {a.Server.ClockDrift.TotalSeconds:+0;-0;0} s");
                    sb.AppendLine($"Aktywne połączenia (DB):   {a.Server.ActiveConnections}");
                    sb.AppendLine($"Version:                   {a.Server.Version}");
                }
            }
            else { sb.AppendLine("(brak)"); }
            sb.AppendLine();

            // === FAZY ===
            sb.AppendLine("🔢 FAZY ŁADOWANIA (posortowane od najwolniejszej)");
            sb.AppendLine(new string('-', 78));

            // ASCII bar chart
            long maxPhaseMs = a.Phases.Any() ? a.Phases.Max(p => p.Sw.ElapsedMilliseconds) : 1;
            int barWidth = 40;
            int idx = 1;
            foreach (var p in a.Phases.OrderByDescending(p => p.Sw.ElapsedMilliseconds))
            {
                double pct = total > 0 ? 100.0 * p.Sw.ElapsedMilliseconds / total : 0;
                int filled = maxPhaseMs > 0 ? (int)Math.Round(barWidth * (p.Sw.ElapsedMilliseconds / (double)maxPhaseMs)) : 0;
                string bar = new string('█', filled) + new string('░', barWidth - filled);
                sb.AppendLine($"{idx,2}. [{p.Sw.ElapsedMilliseconds,5} ms] {bar} {pct,5:0.0}%  {p.Name}");
                if (p.RowCount.HasValue) sb.AppendLine($"      Wierszy:  {p.RowCount.Value:N0}");
                foreach (var sub in p.SubTimes)
                    sb.AppendLine($"      [{sub.Value,5} ms]  └─ {sub.Key}");
                foreach (var note in p.Notes)
                    sb.AppendLine($"      ⚠️ {note}");
                sb.AppendLine();
                idx++;
            }

            // === SAMPLE DANYCH === (tylko deep)
            if (a.DeepMode && a.TableSamples.Count > 0)
            {
            sb.AppendLine("🔬 SAMPLE DANYCH (TOP 3 wiersze z każdej tabeli — żeby wiedzieć co tam jest)");
            sb.AppendLine(new string('-', 78));
            foreach (var s in a.TableSamples)
            {
                sb.AppendLine($"  {s.TableName}:");
                sb.AppendLine("    " + string.Join(" | ", s.Columns.Select(c => c.Length > 12 ? c.Substring(0, 12) : c.PadRight(12))));
                sb.AppendLine("    " + new string('-', Math.Min(s.Columns.Count * 15, 80)));
                foreach (var row in s.Rows)
                {
                    sb.AppendLine("    " + string.Join(" | ", row.Select(v => (v ?? "").Length > 12 ? v!.Substring(0, 12) : (v ?? "").PadRight(12))));
                }
                sb.AppendLine();
            }
            sb.AppendLine();
            }  // end if DeepMode

            // === QUERIES (pełne SQL + STATISTICS IO/TIME) ===
            sb.AppendLine("📜 ZAPYTANIA SQL — pełne treści + STATISTICS IO/TIME");
            sb.AppendLine(new string('-', 78));
            if (a.Queries.Count == 0)
                sb.AppendLine("(brak zarejestrowanych zapytań)");
            else
            {
                foreach (var q in a.Queries)
                {
                    sb.AppendLine();
                    sb.AppendLine($"▼ {q.Label}");
                    sb.AppendLine($"    Czas wykonania: {q.Sw.ElapsedMilliseconds} ms");
                    if (q.RowCount.HasValue) sb.AppendLine($"    Wierszy:        {q.RowCount.Value:N0}");
                    if (q.Parameters.Count > 0)
                    {
                        sb.AppendLine($"    Parametry:");
                        foreach (var kv in q.Parameters)
                            sb.AppendLine($"        {kv.Key} = {kv.Value ?? "NULL"}");
                    }
                    sb.AppendLine("    SQL:");
                    foreach (var line in q.Sql.Split('\n'))
                        sb.AppendLine("    │ " + line.TrimEnd('\r'));
                    if (q.InfoMessages.Count > 0)
                    {
                        sb.AppendLine("    STATISTICS / INFO MESSAGES (z serwera):");
                        foreach (var m in q.InfoMessages)
                            sb.AppendLine("    » " + m);
                    }
                    sb.AppendLine();
                }
            }
            sb.AppendLine();

            // === CONNECTION STATISTICS ===
            sb.AppendLine("🌐 STATYSTYKI CONNECTION (SqlConnection.RetrieveStatistics)");
            sb.AppendLine(new string('-', 78));
            if (a.ConnectionStats.Count == 0)
            {
                sb.AppendLine("(brak — StatisticsEnabled nie ustawione lub przed pierwszym Load*)");
            }
            else
            {
                sb.AppendLine($"{"Label",-32} {"Bytes↓",10} {"Bytes↑",8} {"NetSrvTime",10} {"RT",4} {"SELs",5} {"Rows",6}");
                sb.AppendLine(new string('-', 78));
                long totalRecv = 0, totalSent = 0, totalSrvTime = 0, totalRT = 0;
                foreach (var c in a.ConnectionStats)
                {
                    sb.AppendLine($"{c.Label,-32} {FormatBytes(c.BytesReceived),10} {FormatBytes(c.BytesSent),8} {c.NetworkServerTime + " ms",10} {c.ServerRoundtrips,4} {c.SelectCount,5} {c.SelectRows,6}");
                    totalRecv += c.BytesReceived;
                    totalSent += c.BytesSent;
                    totalSrvTime += c.NetworkServerTime;
                    totalRT += c.ServerRoundtrips;
                }
                sb.AppendLine(new string('-', 78));
                sb.AppendLine($"{"RAZEM",-32} {FormatBytes(totalRecv),10} {FormatBytes(totalSent),8} {totalSrvTime + " ms",10} {totalRT,4}");
            }
            sb.AppendLine();

            // === MISSING INDEX HINTS (DMV) === (tylko deep)
            if (!a.DeepMode) goto skipDmv;
            sb.AppendLine("🎯 MISSING INDEX HINTS (z sys.dm_db_missing_index_*)");
            sb.AppendLine(new string('-', 78));
            if (a.MissingIndexHints.Count == 0)
            {
                sb.AppendLine("(brak sugestii lub VIEW SERVER STATE denied)");
            }
            else
            {
                sb.AppendLine($"SQL Server analizuje plany ostatnich zapytań i sam podpowiada indeksy.");
                sb.AppendLine($"Score = avg_impact_% × (seeks + scans) — im wyżej tym ważniejszy.");
                sb.AppendLine();
                int n = 1;
                foreach (var h in a.MissingIndexHints.OrderByDescending(x => x.ImpactScore).Take(10))
                {
                    sb.AppendLine($"  #{n}. {h.TableName}   (score: {h.ImpactScore:0.0}, seeks: {h.UserSeeks:N0}, impact: {h.AvgImpactPct:0.0}%)");
                    if (!string.IsNullOrEmpty(h.EqualityColumns))
                        sb.AppendLine($"      equality:   {h.EqualityColumns}");
                    if (!string.IsNullOrEmpty(h.InequalityColumns))
                        sb.AppendLine($"      inequality: {h.InequalityColumns}");
                    if (!string.IsNullOrEmpty(h.IncludedColumns))
                        sb.AppendLine($"      include:    {h.IncludedColumns}");
                    sb.AppendLine($"      DDL: {h.SuggestedDDL.Replace("\n", "\n           ")}");
                    sb.AppendLine();
                    n++;
                }
            }
            sb.AppendLine();

            // === INDEX USAGE (DMV) ===
            sb.AppendLine("📈 INDEX USAGE STATS (sys.dm_db_index_usage_stats — od startu serwera)");
            sb.AppendLine(new string('-', 78));
            if (a.IndexUsages.Count == 0)
            {
                sb.AppendLine("(brak — VIEW SERVER STATE denied lub baza świeżo startowała)");
            }
            else
            {
                sb.AppendLine($"{"Tabela",-26} {"Indeks",-42} {"Seeks",7} {"Scans",7} {"Updates",8}");
                sb.AppendLine(new string('-', 78));
                foreach (var grp in a.IndexUsages.GroupBy(x => x.TableName))
                {
                    foreach (var u in grp.OrderByDescending(x => x.UserSeeks + x.UserScans))
                    {
                        string marker = (u.UserSeeks + u.UserScans == 0) ? " ⚠️ NIEUŻYWANY" : "";
                        sb.AppendLine($"{u.TableName,-26} {u.IndexName,-42} {u.UserSeeks,7:N0} {u.UserScans,7:N0} {u.UserUpdates,8:N0}{marker}");
                    }
                }
            }
            sb.AppendLine();

        skipDmv:
            // === INDEKSY === (tylko deep)
            if (!a.DeepMode) goto skipIndexes;
            sb.AppendLine("📚 INDEKSY NA UŻYWANYCH TABELACH");
            sb.AppendLine(new string('-', 78));
            foreach (var grp in a.Indexes.GroupBy(x => x.TableName))
            {
                var ti = a.TableInfos.FirstOrDefault(t => t.Name == grp.Key);
                string rows = ti != null
                    ? $" ({ti.RowCount:N0} wierszy, {ti.DataSizeKB:N0} KB data, {ti.IndexSizeKB:N0} KB indexes)"
                    : "";
                sb.AppendLine($"  {grp.Key}{rows}");
                foreach (var ix in grp)
                {
                    string flags = "";
                    if (ix.IsPrimaryKey) flags += " [PK]";
                    if (ix.IsUnique && !ix.IsPrimaryKey) flags += " [UQ]";
                    sb.AppendLine($"     • {ix.IndexName} ({ix.IndexType}){flags}");
                    if (!string.IsNullOrEmpty(ix.KeyColumns))
                        sb.AppendLine($"           key:      {ix.KeyColumns}");
                    if (!string.IsNullOrEmpty(ix.IncludedColumns))
                        sb.AppendLine($"           include:  {ix.IncludedColumns}");
                }
                sb.AppendLine();
            }

        skipIndexes:
            // === WIDOK === (tylko deep)
            if (!a.DeepMode) goto skipView;
            sb.AppendLine("🔍 DEFINICJA WIDOKU v_WstawieniaDoKontaktu");
            sb.AppendLine(new string('-', 78));
            if (!string.IsNullOrEmpty(a.ViewDefinition_v_WstawieniaDoKontaktu))
            {
                foreach (var line in a.ViewDefinition_v_WstawieniaDoKontaktu.Split('\n'))
                    sb.AppendLine("    │ " + line.TrimEnd('\r'));
            }
            else { sb.AppendLine("(nie pobrano lub brak uprawnień)"); }
            sb.AppendLine();

        skipView:
            // === CACHE ===
            sb.AppendLine("💾 STATYSTYKI CACHE");
            sb.AppendLine(new string('-', 78));
            if (a.Cache != null)
            {
                sb.AppendLine($"  _deliveryCache:");
                sb.AppendLine($"     • Załadowany:        {(a.Cache.DeliveryCacheLoaded ? "TAK" : "NIE")}");
                if (a.Cache.DeliveryCacheLoaded)
                {
                    sb.AppendLine($"     • Wiek:              {a.Cache.DeliveryCacheAge} sek (TTL: 60 s)");
                    sb.AppendLine($"     • Timestamp:         {a.Cache.DeliveryCacheTimestamp:HH:mm:ss}");
                }
                sb.AppendLine($"     • Wstawień (LpW):    {a.Cache.DeliveryCacheKeys:N0}");
                sb.AppendLine($"     • Wierszy dostaw:    {a.Cache.DeliveryCacheRows:N0}");
                sb.AppendLine();
                sb.AppendLine($"  _avatarBrushCache:");
                sb.AppendLine($"     • Avatarów w cache:  {a.Cache.AvatarCacheKeys}");
                sb.AppendLine($"     • Userów bez avat.:  {a.Cache.AvatarMissingCount}");
            }
            sb.AppendLine();

            // === WĄSKIE GARDŁA ===
            sb.AppendLine("🚨 WĄSKIE GARDŁA (TOP 3)");
            sb.AppendLine(new string('-', 78));
            int idxBn = 1;
            foreach (var p in a.Phases.OrderByDescending(p => p.Sw.ElapsedMilliseconds).Take(3))
            {
                double pct = total > 0 ? 100.0 * p.Sw.ElapsedMilliseconds / total : 0;
                sb.AppendLine($"  {idxBn}. {p.Name} — {p.Sw.ElapsedMilliseconds} ms ({pct:0.0} %)");
                idxBn++;
            }
            sb.AppendLine();

            // === REKOMENDACJE ===
            sb.AppendLine("💡 AUTOMATYCZNE REKOMENDACJE");
            sb.AppendLine(new string('-', 78));
            foreach (var r in WygenerujRekomendacje(a)) sb.AppendLine("  • " + r);
            sb.AppendLine();

            // === GOTOWE DDL ===
            var ddl = WygenerujDDL(a);
            if (ddl.Count > 0)
            {
                sb.AppendLine("🛠️  GOTOWE DDL DO URUCHOMIENIA (uporządkowane od najważniejszego)");
                sb.AppendLine(new string('-', 78));
                sb.AppendLine("-- Wszystkie polecenia poniżej można skopiować do SSMS i uruchomić jednym F5.");
                sb.AppendLine("-- Uwaga: DDL może wymagać krótkiego LOCK; uruchamiać poza godzinami szczytu.");
                sb.AppendLine();
                int n = 1;
                foreach (var d in ddl)
                {
                    sb.AppendLine($"-- [{n}] {d.Comment}");
                    sb.AppendLine(d.Sql);
                    sb.AppendLine();
                    n++;
                }
            }

            if (!string.IsNullOrEmpty(a.DiagError))
            {
                sb.AppendLine("⚠️ BŁĘDY DIAGNOSTYKI");
                sb.AppendLine(new string('-', 78));
                sb.AppendLine(a.DiagError);
                sb.AppendLine();
            }

            sb.AppendLine(new string('=', 78));
            sb.AppendLine("📋 Pełny raport został skopiowany do schowka (Ctrl+V w czacie).");
            return sb.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            return (bytes / 1024.0 / 1024.0).ToString("0.00") + " MB";
        }

        private List<string> WygenerujRekomendacje(LoadingAudit a)
        {
            var rec = new List<string>();
            var byName = a.Phases.ToDictionary(p => p.Name, p => p);

            // ===== A. SIEĆ =====
            if (a.Server != null && a.Server.PingMs > 50)
                rec.Add($"PING={a.Server.PingMs} ms — to dużo dla LAN, każdy SQL = +{a.Server.PingMs} ms round-trip overhead.");

            // ===== B. HEAP TABLES (krytyczne!) =====
            var heapTables = a.Indexes
                .Where(i => i.IndexType == "HEAP")
                .Select(i => i.TableName)
                .Distinct()
                .ToList();
            foreach (var t in heapTables)
            {
                var ti = a.TableInfos.FirstOrDefault(x => x.Name == t);
                long rows = ti?.RowCount ?? 0;
                string tShort = t.Replace("dbo.", "");
                string pkColumn = tShort.Equals("WstawieniaKurczakow", StringComparison.OrdinalIgnoreCase) ? "Lp"
                                : tShort.Equals("HarmonogramDostaw", StringComparison.OrdinalIgnoreCase) ? "Lp"
                                : "ID";
                rec.Add($"🔥 KRYTYCZNE: Tabela {t} jest HEAPEM ({rows:N0} wierszy) bez clustered index/PK — KAŻDY lookup po kluczu = pełny skan. DDL:\n" +
                        $"      ALTER TABLE {t} ADD CONSTRAINT PK_{tShort} PRIMARY KEY CLUSTERED ({pkColumn});");
            }

            // ===== C. BRAKUJĄCE INDEKSY =====
            var chIndexes = a.Indexes.Where(i => i.TableName.IndexOf("ContactHistory", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            bool hasIdxCreatedAt = chIndexes.Any(i => i.KeyColumns.IndexOf("CreatedAt", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!hasIdxCreatedAt)
                rec.Add("Brak indeksu na ContactHistory.CreatedAt — używane w LoadHistoria (WHERE CreatedAt >= @CutoffDate) i agregatach LoadPrzypomnienia. DDL:\n" +
                        "      CREATE NONCLUSTERED INDEX IX_ContactHistory_CreatedAt\n" +
                        "         ON dbo.ContactHistory(CreatedAt DESC)\n" +
                        "         INCLUDE (LpWstawienia, Reason, SnoozedUntil, ContactDate, ContactID, UserID, Dostawca);");

            bool hasIdxSnooze = chIndexes.Any(i => i.KeyColumns.IndexOf("SnoozedUntil", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!hasIdxSnooze)
                rec.Add("Brak indeksu na ContactHistory.SnoozedUntil — filtrowanie SnoozedCandidates wymaga skanu. DDL (filtered index — zajmie tylko wpisy ze snooze):\n" +
                        "      CREATE NONCLUSTERED INDEX IX_ContactHistory_SnoozedUntil\n" +
                        "         ON dbo.ContactHistory(SnoozedUntil)\n" +
                        "         INCLUDE (LpWstawienia, Reason, ContactID)\n" +
                        "         WHERE SnoozedUntil IS NOT NULL;");

            // Indeks na (LpWstawienia, ContactID DESC) INCLUDE Reason — eliminuje Worktable w OUTER APPLY (last)
            bool hasIdxLpContact = chIndexes.Any(i =>
                i.KeyColumns.IndexOf("LpWstawienia", StringComparison.OrdinalIgnoreCase) >= 0 &&
                i.KeyColumns.IndexOf("ContactID", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!hasIdxLpContact)
                rec.Add("Worktable 282 scans / 5790 logical reads w 'Aktywne' — OUTER APPLY (TOP 1 Reason ORDER BY ContactID DESC) wymusza sort. DDL eliminujący sort:\n" +
                        "      CREATE NONCLUSTERED INDEX IX_ContactHistory_LpWst_ContactID\n" +
                        "         ON dbo.ContactHistory(LpWstawienia, ContactID DESC)\n" +
                        "         INCLUDE (Reason, SnoozedUntil, CreatedAt);");

            // ===== D. FAZY =====
            if (byName.TryGetValue("PreloadDeliveryCache", out var preload))
            {
                if (preload.RowCount.HasValue && preload.RowCount.Value > 50_000)
                    rec.Add($"PreloadDeliveryCache ładuje {preload.RowCount.Value:N0} wierszy bez filtra daty — dodaj WHERE WK.DataWstawienia >= DATEADD(MONTH, -12, GETDATE()).");
                if (preload.Sw.ElapsedMilliseconds > 1000)
                    rec.Add($"PreloadDeliveryCache > 1 s — rozważ lazy load tylko dla widocznych wierszy.");
            }

            if (byName.TryGetValue("Diagnostyka SQL (indeksy/tabele/widok)", out var diag) && diag.Sw.ElapsedMilliseconds > 200)
                rec.Add($"Diagnostyka SQL ({diag.Sw.ElapsedMilliseconds} ms) — sys.indexes/sys.partitions zwykle są szybkie; jeśli zżera czas, można ją uruchomić raz na sesję i cachować w pamięci.");

            // ===== E. RÓWNOLEGŁOŚĆ =====
            long sumPhases = a.Phases.Sum(p => p.Sw.ElapsedMilliseconds);
            long maxPhase = a.Phases.Any() ? a.Phases.Max(p => p.Sw.ElapsedMilliseconds) : 0;
            if (sumPhases > 500 && (sumPhases - maxPhase) > 200)
                rec.Add($"Fazy SEKWENCYJNE (suma {sumPhases} ms, max {maxPhase} ms) — równoległe Task.WhenAll ucięłoby do ~{maxPhase} ms.");

            // ===== F. DRIFT =====
            if (a.Server != null && Math.Abs(a.Server.ClockDrift.TotalSeconds) > 5)
                rec.Add($"Drift zegara client↔server: {a.Server.ClockDrift.TotalSeconds:0} s.");

            // ===== G. STATISTICS IO HEURISTICS =====
            foreach (var q in a.Queries)
            {
                foreach (var msg in q.InfoMessages)
                {
                    if (msg.Contains("Worktable", StringComparison.OrdinalIgnoreCase))
                    {
                        // Wyciągnij scan count
                        var match = System.Text.RegularExpressions.Regex.Match(msg, @"Worktable.*Scan count (\d+), logical reads (\d+)");
                        if (match.Success)
                        {
                            int scans = int.Parse(match.Groups[1].Value);
                            int reads = int.Parse(match.Groups[2].Value);
                            if (reads > 1000)
                            {
                                rec.Add($"[{q.Label}] Worktable: {scans} scans / {reads} logical reads → plan używa tempdb dla sort/spool. Indeks pokrywający może to wyeliminować.");
                                break;  // jedno per query
                            }
                        }
                    }
                }
            }

            if (rec.Count == 0)
                rec.Add("Nie wykryto wąskich gardeł na progu alarmu.");

            return rec;
        }

        private class DdlPropozycja
        {
            public int Priority { get; set; }  // 1=critical, 2=high, 3=nice-to-have
            public string Comment { get; set; } = "";
            public string Sql { get; set; } = "";
        }

        private List<DdlPropozycja> WygenerujDDL(LoadingAudit a)
        {
            var ddl = new List<DdlPropozycja>();

            // === HEAP → PK CLUSTERED ===
            // Sprawdzamy WstawieniaKurczakow / HarmonogramDostaw — czy mają clustered index
            foreach (var grp in a.Indexes.GroupBy(i => i.TableName))
            {
                bool hasClustered = grp.Any(i => i.IndexType == "CLUSTERED");
                if (hasClustered) continue;

                // Brak clustered — propozycja
                string t = grp.Key;
                string tShort = t.Replace("dbo.", "");
                string pkCol = tShort.Equals("WstawieniaKurczakow", StringComparison.OrdinalIgnoreCase) ? "Lp"
                             : tShort.Equals("HarmonogramDostaw", StringComparison.OrdinalIgnoreCase) ? "Lp"
                             : "Id";
                var ti = a.TableInfos.FirstOrDefault(x => x.Name == t);
                ddl.Add(new DdlPropozycja
                {
                    Priority = 1,
                    Comment = $"KRYTYCZNE: {t} jest HEAPEM ({ti?.RowCount ?? 0:N0} wierszy). Dodaj clustered PK na {pkCol} — wyeliminuje pełne skany przy JOIN-ach.",
                    Sql = $"ALTER TABLE {t}\n    ADD CONSTRAINT PK_{tShort} PRIMARY KEY CLUSTERED ({pkCol});"
                });
            }

            // === INDEKSY NA ContactHistory ===
            var chIdx = a.Indexes.Where(i => i.TableName.IndexOf("ContactHistory", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            bool hasIdxLpContact = chIdx.Any(i =>
                i.KeyColumns.IndexOf("LpWstawienia", StringComparison.OrdinalIgnoreCase) >= 0 &&
                i.KeyColumns.IndexOf("ContactID", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!hasIdxLpContact)
                ddl.Add(new DdlPropozycja
                {
                    Priority = 1,
                    Comment = "Eliminuje Worktable/sort w 'OUTER APPLY TOP 1 ORDER BY ContactID DESC' (LoadPrzypomnienia aktywne).",
                    Sql = "CREATE NONCLUSTERED INDEX IX_ContactHistory_LpWst_ContactID\n" +
                          "    ON dbo.ContactHistory(LpWstawienia, ContactID DESC)\n" +
                          "    INCLUDE (Reason, SnoozedUntil, CreatedAt);"
                });

            bool hasIdxCreated = chIdx.Any(i => i.KeyColumns.IndexOf("CreatedAt", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!hasIdxCreated)
                ddl.Add(new DdlPropozycja
                {
                    Priority = 2,
                    Comment = "Przyspiesza LoadHistoria (WHERE CreatedAt >= @CutoffDate) i agregaty 'aktywne' (ch.CreatedAt >= w.DataWstawienia).",
                    Sql = "CREATE NONCLUSTERED INDEX IX_ContactHistory_CreatedAt\n" +
                          "    ON dbo.ContactHistory(CreatedAt DESC)\n" +
                          "    INCLUDE (LpWstawienia, Reason, SnoozedUntil, ContactDate, UserID, Dostawca);"
                });

            bool hasIdxSnooze = chIdx.Any(i => i.KeyColumns.IndexOf("SnoozedUntil", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!hasIdxSnooze)
                ddl.Add(new DdlPropozycja
                {
                    Priority = 2,
                    Comment = "Filtered index — przyspiesza SnoozedCandidates w LoadPrzypomnienia (typowo 10-100 wpisów ze snooze).",
                    Sql = "CREATE NONCLUSTERED INDEX IX_ContactHistory_SnoozedUntil\n" +
                          "    ON dbo.ContactHistory(SnoozedUntil)\n" +
                          "    INCLUDE (LpWstawienia, Reason, ContactID)\n" +
                          "    WHERE SnoozedUntil IS NOT NULL;"
                });

            return ddl.OrderBy(d => d.Priority).ToList();
        }

        internal void PokazAudytJesliPotrzeba(bool forceShow)
        {
            if (_audyt == null) return;
            _audyt.Total.Stop();
            if (!forceShow && _audytPokazany) return;
            _audytPokazany = true;

            string raport = BuildAudytRaport(_audyt);
            try { Clipboard.SetText(raport); } catch { }
            PokazRaportWindow(raport);
        }

        private void PokazRaportWindow(string raport)
        {
            var w = new Window
            {
                Title = "🔍 Audyt ładowania — Cykle Wstawień (pełna diagnostyka)",
                Width = 1050,
                Height = 820,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = System.Windows.Media.Brushes.WhiteSmoke
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            var header = new System.Windows.Controls.TextBlock
            {
                Text = "Raport został skopiowany do schowka. Wklej (Ctrl+V) w czacie żeby przeanalizować.",
                Margin = new System.Windows.Thickness(12, 12, 12, 8),
                FontSize = 12,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x5C, 0x8A, 0x3A)),
                TextWrapping = System.Windows.TextWrapping.Wrap
            };
            System.Windows.Controls.Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var tb = new System.Windows.Controls.TextBox
            {
                Text = raport,
                IsReadOnly = true,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11.5,
                AcceptsReturn = true,
                TextWrapping = System.Windows.TextWrapping.NoWrap,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                Margin = new System.Windows.Thickness(12, 0, 12, 8),
                Background = System.Windows.Media.Brushes.White
            };
            System.Windows.Controls.Grid.SetRow(tb, 1);
            grid.Children.Add(tb);

            var panel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new System.Windows.Thickness(12, 0, 12, 12)
            };

            var btnCopy = new System.Windows.Controls.Button
            {
                Content = "📋 Skopiuj ponownie",
                Padding = new System.Windows.Thickness(14, 6, 14, 6),
                Margin = new System.Windows.Thickness(0, 0, 8, 0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x34, 0x98, 0xDB)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new System.Windows.Thickness(0),
                FontWeight = System.Windows.FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCopy.Click += (_, _) => { try { Clipboard.SetText(raport); } catch { } };
            panel.Children.Add(btnCopy);

            var btnClose = new System.Windows.Controls.Button
            {
                Content = "✖ Zamknij",
                Padding = new System.Windows.Thickness(14, 6, 14, 6),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x95, 0xA5, 0xA6)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new System.Windows.Thickness(0),
                FontWeight = System.Windows.FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnClose.Click += (_, _) => w.Close();
            panel.Children.Add(btnClose);

            System.Windows.Controls.Grid.SetRow(panel, 2);
            grid.Children.Add(panel);

            w.Content = grid;
            w.ShowDialog();
        }
    }
}
