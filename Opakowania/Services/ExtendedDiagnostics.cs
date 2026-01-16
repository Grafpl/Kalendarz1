using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// Rozszerzony system diagnostyczny - zbiera WSZYSTKIE możliwe informacje
    /// </summary>
    public static class ExtendedDiagnostics
    {
        private static readonly object _lock = new object();
        private static readonly List<DiagnosticEvent> _events = new List<DiagnosticEvent>();
        private static readonly Dictionary<string, List<double>> _timings = new Dictionary<string, List<double>>();
        private static DateTime _sessionStart = DateTime.Now;
        private static long _totalBytesRead = 0;
        private static int _totalQueries = 0;
        private static int _totalConnections = 0;
        private static int _failedConnections = 0;

        public class DiagnosticEvent
        {
            public DateTime Timestamp { get; set; }
            public string Category { get; set; }
            public string Operation { get; set; }
            public string Details { get; set; }
            public double? DurationMs { get; set; }
            public bool IsError { get; set; }
        }

        public static void Reset()
        {
            lock (_lock)
            {
                _events.Clear();
                _timings.Clear();
                _sessionStart = DateTime.Now;
                _totalBytesRead = 0;
                _totalQueries = 0;
                _totalConnections = 0;
                _failedConnections = 0;
            }
        }

        public static void LogEvent(string category, string operation, string details, double? durationMs = null, bool isError = false)
        {
            lock (_lock)
            {
                _events.Add(new DiagnosticEvent
                {
                    Timestamp = DateTime.Now,
                    Category = category,
                    Operation = operation,
                    Details = details,
                    DurationMs = durationMs,
                    IsError = isError
                });

                if (durationMs.HasValue)
                {
                    if (!_timings.ContainsKey(operation))
                        _timings[operation] = new List<double>();
                    _timings[operation].Add(durationMs.Value);
                }
            }
        }

        public static void RecordQuery() => Interlocked.Increment(ref _totalQueries);
        public static void RecordConnection() => Interlocked.Increment(ref _totalConnections);
        public static void RecordFailedConnection() => Interlocked.Increment(ref _failedConnections);
        public static void RecordBytesRead(long bytes) => Interlocked.Add(ref _totalBytesRead, bytes);

        /// <summary>
        /// Generuje MEGA szczegółowy raport diagnostyczny
        /// </summary>
        public static async Task<string> GenerateFullReportAsync()
        {
            var sb = new StringBuilder();
            var totalSw = Stopwatch.StartNew();

            sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                    PEŁNY RAPORT DIAGNOSTYCZNY                                        ║");
            sb.AppendLine($"║                    {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}                                        ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 1: INFORMACJE O SYSTEMIE
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 1. INFORMACJE O SYSTEMIE                                                             │");
            sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");

            sb.AppendLine($"  Komputer: {Environment.MachineName}");
            sb.AppendLine($"  Użytkownik: {Environment.UserName}");
            sb.AppendLine($"  Domena: {Environment.UserDomainName}");
            sb.AppendLine($"  System: {Environment.OSVersion}");
            sb.AppendLine($"  64-bit OS: {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($"  64-bit Process: {Environment.Is64BitProcess}");
            sb.AppendLine($"  Procesory: {Environment.ProcessorCount}");
            sb.AppendLine($"  .NET Version: {Environment.Version}");
            sb.AppendLine($"  CLR Version: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 2: PAMIĘĆ
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 2. PAMIĘĆ I ZASOBY                                                                   │");
            sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");

            var process = Process.GetCurrentProcess();
            sb.AppendLine($"  Working Set: {process.WorkingSet64 / 1024 / 1024:N0} MB");
            sb.AppendLine($"  Private Memory: {process.PrivateMemorySize64 / 1024 / 1024:N0} MB");
            sb.AppendLine($"  Virtual Memory: {process.VirtualMemorySize64 / 1024 / 1024:N0} MB");
            sb.AppendLine($"  Paged Memory: {process.PagedMemorySize64 / 1024 / 1024:N0} MB");
            sb.AppendLine($"  GC Total Memory: {GC.GetTotalMemory(false) / 1024 / 1024:N2} MB");
            sb.AppendLine($"  GC Gen0 Collections: {GC.CollectionCount(0)}");
            sb.AppendLine($"  GC Gen1 Collections: {GC.CollectionCount(1)}");
            sb.AppendLine($"  GC Gen2 Collections: {GC.CollectionCount(2)}");
            sb.AppendLine($"  Handles: {process.HandleCount}");
            sb.AppendLine($"  Threads: {process.Threads.Count}");
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 3: THREAD POOL
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 3. THREAD POOL                                                                       │");
            sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");

            ThreadPool.GetMinThreads(out int minWorker, out int minIO);
            ThreadPool.GetMaxThreads(out int maxWorker, out int maxIO);
            ThreadPool.GetAvailableThreads(out int availWorker, out int availIO);

            sb.AppendLine($"  Worker Threads - Min: {minWorker}, Max: {maxWorker}, Dostępnych: {availWorker}, Używanych: {maxWorker - availWorker}");
            sb.AppendLine($"  I/O Threads - Min: {minIO}, Max: {maxIO}, Dostępnych: {availIO}, Używanych: {maxIO - availIO}");
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 4: DIAGNOSTYKA SIECI
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 4. DIAGNOSTYKA SIECI                                                                 │");
            sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");

            // Test ping do obu serwerów
            var servers = new[] {
                ("192.168.0.112", "Handel (SQL Server)"),
                ("192.168.0.109", "LibraNet (SQL Server)")
            };

            foreach (var (ip, name) in servers)
            {
                sb.AppendLine($"\n  === {name} ({ip}) ===");

                // Ping test
                try
                {
                    using (var ping = new Ping())
                    {
                        var pingResults = new List<long>();
                        for (int i = 0; i < 5; i++)
                        {
                            var reply = await ping.SendPingAsync(ip, 1000);
                            if (reply.Status == IPStatus.Success)
                                pingResults.Add(reply.RoundtripTime);
                            else
                                sb.AppendLine($"    Ping {i + 1}: FAILED ({reply.Status})");
                        }

                        if (pingResults.Any())
                        {
                            sb.AppendLine($"    Ping (5 prób): Min={pingResults.Min()}ms, Max={pingResults.Max()}ms, Avg={pingResults.Average():F1}ms");
                        }
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"    Ping ERROR: {ex.Message}");
                }

                // TCP Port test
                var ports = new[] { 1433, 1434 };
                foreach (var port in ports)
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        using (var client = new TcpClient())
                        {
                            var connectTask = client.ConnectAsync(ip, port);
                            if (await Task.WhenAny(connectTask, Task.Delay(2000)) == connectTask)
                            {
                                sw.Stop();
                                sb.AppendLine($"    Port {port}: OPEN ({sw.ElapsedMilliseconds}ms)");
                            }
                            else
                            {
                                sb.AppendLine($"    Port {port}: TIMEOUT (>2000ms)");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        sb.AppendLine($"    Port {port}: ERROR - {ex.Message}");
                    }
                }
            }
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 5: TESTY POŁĄCZEŃ SQL
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 5. TESTY POŁĄCZEŃ SQL SERVER                                                         │");
            sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");

            var connectionStrings = new Dictionary<string, string>
            {
                ["Handel"] = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True;Connection Timeout=30;",
                ["LibraNet"] = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;Connection Timeout=30;"
            };

            foreach (var kvp in connectionStrings)
            {
                sb.AppendLine($"\n  === {kvp.Key} ===");

                // Test 1: Pierwsze połączenie (cold)
                var sw1 = Stopwatch.StartNew();
                try
                {
                    using (var conn = new SqlConnection(kvp.Value))
                    {
                        await conn.OpenAsync();
                        sw1.Stop();
                        sb.AppendLine($"    Połączenie #1 (cold): {sw1.ElapsedMilliseconds}ms");
                        sb.AppendLine($"    Server Version: {conn.ServerVersion}");
                        sb.AppendLine($"    Workstation ID: {conn.WorkstationId}");
                        sb.AppendLine($"    Client Connection ID: {conn.ClientConnectionId}");

                        // Test prostego query
                        var sw2 = Stopwatch.StartNew();
                        using (var cmd = new SqlCommand("SELECT @@VERSION, @@SERVERNAME, @@SPID, DB_NAME()", conn))
                        {
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    sw2.Stop();
                                    sb.AppendLine($"    SELECT @@VERSION: {sw2.ElapsedMilliseconds}ms");
                                    sb.AppendLine($"    Server: {reader.GetString(1)}, SPID: {reader.GetInt16(2)}, DB: {reader.GetString(3)}");
                                }
                            }
                        }

                        // Test GETDATE()
                        var sw3 = Stopwatch.StartNew();
                        using (var cmd = new SqlCommand("SELECT GETDATE(), SYSDATETIME()", conn))
                        {
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    sw3.Stop();
                                    var serverTime = reader.GetDateTime(0);
                                    var localTime = DateTime.Now;
                                    var diff = (serverTime - localTime).TotalMilliseconds;
                                    sb.AppendLine($"    GETDATE(): {sw3.ElapsedMilliseconds}ms");
                                    sb.AppendLine($"    Różnica czasu klient-serwer: {diff:F0}ms");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    sw1.Stop();
                    sb.AppendLine($"    BŁĄD: {ex.Message}");
                    sb.AppendLine($"    Czas do błędu: {sw1.ElapsedMilliseconds}ms");
                }

                // Test 2: Drugie połączenie (warm - pool)
                var sw4 = Stopwatch.StartNew();
                try
                {
                    using (var conn = new SqlConnection(kvp.Value))
                    {
                        await conn.OpenAsync();
                        sw4.Stop();
                        sb.AppendLine($"    Połączenie #2 (pool): {sw4.ElapsedMilliseconds}ms");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"    Pool BŁĄD: {ex.Message}");
                }

                // Test 3: Trzecie połączenie
                var sw5 = Stopwatch.StartNew();
                try
                {
                    using (var conn = new SqlConnection(kvp.Value))
                    {
                        await conn.OpenAsync();
                        sw5.Stop();
                        sb.AppendLine($"    Połączenie #3 (pool): {sw5.ElapsedMilliseconds}ms");
                    }
                }
                catch { }
            }
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 6: TEST ZAPYTAŃ SQL (Handel)
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 6. TEST ZAPYTAŃ SQL (HANDEL)                                                         │");
            sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");

            try
            {
                using (var conn = new SqlConnection(connectionStrings["Handel"]))
                {
                    await conn.OpenAsync();

                    // Test różnych zapytań
                    var queries = new Dictionary<string, string>
                    {
                        ["COUNT kontrahentów"] = "SELECT COUNT(*) FROM kh__Kontrahent WITH (NOLOCK)",
                        ["COUNT dokumentów"] = "SELECT COUNT(*) FROM dk__Dokument WITH (NOLOCK)",
                        ["COUNT pozycji"] = "SELECT COUNT(*) FROM dp__DokumentPozycja WITH (NOLOCK)",
                        ["TOP 1 kontrahent"] = "SELECT TOP 1 kh_Id, kh_Symbol FROM kh__Kontrahent WITH (NOLOCK)",
                        ["Salda (LIMIT 10)"] = @"SELECT TOP 10
                            kh.kh_Id, kh.kh_Symbol, kh.kh_Nazwa1,
                            ISNULL(SUM(CASE WHEN ob.ob_TwrTyp IN (1,2,3,4,5) THEN ob.ob_TwrIlosc ELSE 0 END), 0) as Suma
                        FROM kh__Kontrahent kh WITH (NOLOCK)
                        LEFT JOIN ob__Obroty ob WITH (NOLOCK) ON kh.kh_Id = ob.ob_KhId
                        GROUP BY kh.kh_Id, kh.kh_Symbol, kh.kh_Nazwa1"
                    };

                    foreach (var q in queries)
                    {
                        var sw = Stopwatch.StartNew();
                        try
                        {
                            using (var cmd = new SqlCommand(q.Value, conn))
                            {
                                cmd.CommandTimeout = 30;
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    int rows = 0;
                                    while (await reader.ReadAsync()) rows++;
                                    sw.Stop();
                                    sb.AppendLine($"    {q.Key}: {sw.ElapsedMilliseconds}ms ({rows} rows)");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            sw.Stop();
                            sb.AppendLine($"    {q.Key}: ERROR po {sw.ElapsedMilliseconds}ms - {ex.Message}");
                        }
                    }

                    // Test głównego zapytania sald (pełne)
                    sb.AppendLine("\n    --- PEŁNE ZAPYTANIE SALD ---");
                    var swFull = Stopwatch.StartNew();
                    var fullQuery = @"
                        SELECT
                            kh.kh_Id,
                            kh.kh_Symbol,
                            RTRIM(LTRIM(ISNULL(kh.kh_Nazwa1, ''))) as Nazwa,
                            ISNULL(SUM(CASE WHEN ob.ob_TwrTyp = 1 THEN ob.ob_TwrIlosc ELSE 0 END), 0) as E2,
                            ISNULL(SUM(CASE WHEN ob.ob_TwrTyp = 2 THEN ob.ob_TwrIlosc ELSE 0 END), 0) as H1,
                            ISNULL(SUM(CASE WHEN ob.ob_TwrTyp = 3 THEN ob.ob_TwrIlosc ELSE 0 END), 0) as EURO,
                            ISNULL(SUM(CASE WHEN ob.ob_TwrTyp = 4 THEN ob.ob_TwrIlosc ELSE 0 END), 0) as PCV,
                            ISNULL(SUM(CASE WHEN ob.ob_TwrTyp = 5 THEN ob.ob_TwrIlosc ELSE 0 END), 0) as DREW
                        FROM kh__Kontrahent kh WITH (NOLOCK)
                        LEFT JOIN ob__Obroty ob WITH (NOLOCK) ON kh.kh_Id = ob.ob_KhId
                        WHERE kh.kh_Rodzaj = 1
                        GROUP BY kh.kh_Id, kh.kh_Symbol, kh.kh_Nazwa1
                        HAVING
                            ISNULL(SUM(CASE WHEN ob.ob_TwrTyp = 1 THEN ob.ob_TwrIlosc ELSE 0 END), 0) <> 0 OR
                            ISNULL(SUM(CASE WHEN ob.ob_TwrTyp = 2 THEN ob.ob_TwrIlosc ELSE 0 END), 0) <> 0 OR
                            ISNULL(SUM(CASE WHEN ob.ob_TwrTyp = 3 THEN ob.ob_TwrIlosc ELSE 0 END), 0) <> 0 OR
                            ISNULL(SUM(CASE WHEN ob.ob_TwrTyp = 4 THEN ob.ob_TwrIlosc ELSE 0 END), 0) <> 0 OR
                            ISNULL(SUM(CASE WHEN ob.ob_TwrTyp = 5 THEN ob.ob_TwrIlosc ELSE 0 END), 0) <> 0
                        ORDER BY kh.kh_Nazwa1";

                    try
                    {
                        using (var cmd = new SqlCommand(fullQuery, conn))
                        {
                            cmd.CommandTimeout = 60;

                            var swExecute = Stopwatch.StartNew();
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                swExecute.Stop();
                                sb.AppendLine($"    ExecuteReader: {swExecute.ElapsedMilliseconds}ms");

                                var swRead = Stopwatch.StartNew();
                                int rows = 0;
                                while (await reader.ReadAsync()) rows++;
                                swRead.Stop();

                                swFull.Stop();
                                sb.AppendLine($"    Odczyt danych: {swRead.ElapsedMilliseconds}ms ({rows} rows)");
                                sb.AppendLine($"    ŁĄCZNIE: {swFull.ElapsedMilliseconds}ms");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        swFull.Stop();
                        sb.AppendLine($"    BŁĄD: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"    Błąd połączenia: {ex.Message}");
            }
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 7: STATYSTYKI SESJI
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 7. STATYSTYKI SESJI                                                                  │");
            sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");

            var sessionDuration = DateTime.Now - _sessionStart;
            sb.AppendLine($"  Czas sesji: {sessionDuration}");
            sb.AppendLine($"  Łączna liczba zapytań: {_totalQueries}");
            sb.AppendLine($"  Łączna liczba połączeń: {_totalConnections}");
            sb.AppendLine($"  Nieudane połączenia: {_failedConnections}");
            sb.AppendLine($"  Odczytane bajty: {_totalBytesRead / 1024:N0} KB");
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 8: STATYSTYKI TIMINGÓW
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 8. STATYSTYKI TIMINGÓW                                                               │");
            sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");

            lock (_lock)
            {
                foreach (var timing in _timings.OrderByDescending(t => t.Value.Sum()))
                {
                    var values = timing.Value;
                    sb.AppendLine($"  {timing.Key}:");
                    sb.AppendLine($"    Wywołań: {values.Count}, Suma: {values.Sum():F1}ms");
                    sb.AppendLine($"    Min: {values.Min():F1}ms, Max: {values.Max():F1}ms, Avg: {values.Average():F1}ms");
                }
            }
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 9: LOG ZDARZEŃ
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 9. LOG ZDARZEŃ (ostatnie 50)                                                         │");
            sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");

            lock (_lock)
            {
                foreach (var evt in _events.TakeLast(50))
                {
                    var prefix = evt.IsError ? "❌" : "✓";
                    var duration = evt.DurationMs.HasValue ? $" [{evt.DurationMs:F1}ms]" : "";
                    sb.AppendLine($"  {prefix} [{evt.Timestamp:HH:mm:ss.fff}] [{evt.Category}] {evt.Operation}{duration}");
                    if (!string.IsNullOrEmpty(evt.Details))
                        sb.AppendLine($"      {evt.Details}");
                }
            }
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 10: CACHE STATUS
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 10. CACHE STATUS                                                                     │");
            sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine(SaldaService.GetCacheStatus());
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 11: PROFILER
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 11. PROFILER OPERACJI                                                                │");
            sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine(PerformanceProfiler.GenerateReport());

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // KONIEC
            // ═══════════════════════════════════════════════════════════════════════════════════════
            totalSw.Stop();
            sb.AppendLine();
            sb.AppendLine("══════════════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  Raport wygenerowany w: {totalSw.ElapsedMilliseconds}ms");
            sb.AppendLine("══════════════════════════════════════════════════════════════════════════════════════");

            return sb.ToString();
        }

        /// <summary>
        /// Uruchamia pełny test wydajności
        /// </summary>
        public static async Task<string> RunFullPerformanceTestAsync()
        {
            var sb = new StringBuilder();
            var totalSw = Stopwatch.StartNew();

            sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                    PEŁNY TEST WYDAJNOŚCI                                             ║");
            sb.AppendLine($"║                    {DateTime.Now:yyyy-MM-dd HH:mm:ss}                                        ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            var service = new SaldaService();

            // TEST 1: Ładowanie sald (3 próby)
            sb.AppendLine("=== TEST 1: ŁADOWANIE SALD (3 próby) ===");
            var saldaTimes = new List<long>();
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) SaldaService.InvalidateCache(); // Wymuś ponowne ładowanie

                var sw = Stopwatch.StartNew();
                var salda = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
                sw.Stop();
                saldaTimes.Add(sw.ElapsedMilliseconds);
                sb.AppendLine($"  Próba {i + 1}: {sw.ElapsedMilliseconds}ms ({salda.Count} rekordów)");
            }
            sb.AppendLine($"  ŚREDNIA: {saldaTimes.Average():F0}ms, MIN: {saldaTimes.Min()}ms, MAX: {saldaTimes.Max()}ms");
            sb.AppendLine();

            // TEST 2: Cache test
            sb.AppendLine("=== TEST 2: CACHE TEST (5 prób bez invalidacji) ===");
            var cacheTimes = new List<long>();
            for (int i = 0; i < 5; i++)
            {
                var sw = Stopwatch.StartNew();
                var salda = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
                sw.Stop();
                cacheTimes.Add(sw.ElapsedMilliseconds);
                sb.AppendLine($"  Próba {i + 1}: {sw.ElapsedMilliseconds}ms");
            }
            sb.AppendLine($"  ŚREDNIA: {cacheTimes.Average():F0}ms (powinno być ~0ms)");
            sb.AppendLine();

            // TEST 3: Dokumenty dla pierwszych 5 kontrahentów
            sb.AppendLine("=== TEST 3: DOKUMENTY (5 kontrahentów) ===");
            var salda2 = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
            var docTimes = new List<long>();
            foreach (var s in salda2.Take(5))
            {
                var sw = Stopwatch.StartNew();
                var docs = await service.PobierzDokumentyAsync(s.Id, DateTime.Today.AddMonths(-6), DateTime.Today);
                sw.Stop();
                docTimes.Add(sw.ElapsedMilliseconds);
                sb.AppendLine($"  {s.Kontrahent?.Substring(0, Math.Min(25, s.Kontrahent?.Length ?? 0))}: {sw.ElapsedMilliseconds}ms ({docs.Count} docs)");
            }
            if (docTimes.Any())
                sb.AppendLine($"  ŚREDNIA: {docTimes.Average():F0}ms");
            sb.AppendLine();

            // TEST 4: Równoległe ładowanie
            sb.AppendLine("=== TEST 4: RÓWNOLEGŁE ŁADOWANIE (10 kontrahentów) ===");
            SaldaService.InvalidateCache();
            var swParallel = Stopwatch.StartNew();
            var tasks = salda2.Take(10).Select(s =>
                service.PobierzDokumentyAsync(s.Id, DateTime.Today.AddMonths(-3), DateTime.Today)).ToList();
            await Task.WhenAll(tasks);
            swParallel.Stop();
            sb.AppendLine($"  Łączny czas: {swParallel.ElapsedMilliseconds}ms dla 10 równoległych zapytań");
            sb.AppendLine();

            totalSw.Stop();
            sb.AppendLine($"═══════════════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  CAŁKOWITY CZAS TESTU: {totalSw.ElapsedMilliseconds}ms ({totalSw.Elapsed.TotalSeconds:F1}s)");
            sb.AppendLine($"═══════════════════════════════════════════════════════════════════════════════════════");

            return sb.ToString();
        }
    }
}
