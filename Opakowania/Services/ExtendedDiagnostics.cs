using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// MEGA ROZSZERZONY system diagnostyczny - zbiera WSZYSTKIE możliwe informacje
    /// Wersja 2.0 - z CPU, RAM, dyskami, WMI, SQL Server stats
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

        // Dla monitorowania CPU
        private static PerformanceCounter _cpuCounter;
        private static PerformanceCounter _ramCounter;

        static ExtendedDiagnostics()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes", true);
                // Pierwsze odczyty są zawsze 0
                _cpuCounter.NextValue();
                _ramCounter.NextValue();
            }
            catch { }
        }

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
        /// Generuje MEGA ULTRA szczegółowy raport diagnostyczny z WSZYSTKIMI możliwymi informacjami
        /// </summary>
        public static async Task<string> GenerateFullReportAsync()
        {
            var sb = new StringBuilder();
            var totalSw = Stopwatch.StartNew();

            sb.AppendLine("╔════════════════════════════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                    PEŁNY RAPORT DIAGNOSTYCZNY v2.0                                                 ║");
            sb.AppendLine($"║                    {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}                                                            ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 1: SYSTEM OPERACYJNY
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 1. SYSTEM OPERACYJNY                                                                               │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");

            sb.AppendLine($"  Komputer: {Environment.MachineName}");
            sb.AppendLine($"  Użytkownik: {Environment.UserName}");
            sb.AppendLine($"  Domena: {Environment.UserDomainName}");
            sb.AppendLine($"  System: {Environment.OSVersion}");
            sb.AppendLine($"  64-bit OS: {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($"  64-bit Process: {Environment.Is64BitProcess}");
            sb.AppendLine($"  System Directory: {Environment.SystemDirectory}");
            sb.AppendLine($"  Current Directory: {Environment.CurrentDirectory}");
            sb.AppendLine($"  .NET Version: {Environment.Version}");
            sb.AppendLine($"  CLR: {RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"  OS Description: {RuntimeInformation.OSDescription}");
            sb.AppendLine($"  OS Architecture: {RuntimeInformation.OSArchitecture}");
            sb.AppendLine($"  Process Architecture: {RuntimeInformation.ProcessArchitecture}");
            sb.AppendLine($"  Tick Count: {Environment.TickCount64 / 1000}s ({TimeSpan.FromMilliseconds(Environment.TickCount64):d\\.hh\\:mm\\:ss} uptime)");
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 2: PROCESOR (CPU) - SZCZEGÓŁOWE INFORMACJE
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 2. PROCESOR (CPU) - SZCZEGÓŁOWE INFORMACJE                                                         │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");

            sb.AppendLine($"  Liczba procesorów logicznych: {Environment.ProcessorCount}");

            // WMI - szczegółowe info o CPU
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        sb.AppendLine($"  Nazwa: {obj["Name"]}");
                        sb.AppendLine($"  Producent: {obj["Manufacturer"]}");
                        sb.AppendLine($"  ID procesora: {obj["ProcessorId"]}");
                        sb.AppendLine($"  Rdzenie fizyczne: {obj["NumberOfCores"]}");
                        sb.AppendLine($"  Rdzenie logiczne: {obj["NumberOfLogicalProcessors"]}");
                        sb.AppendLine($"  Aktualna częstotliwość: {obj["CurrentClockSpeed"]} MHz");
                        sb.AppendLine($"  Maksymalna częstotliwość: {obj["MaxClockSpeed"]} MHz");
                        sb.AppendLine($"  Socket: {obj["SocketDesignation"]}");
                        sb.AppendLine($"  L2 Cache: {Convert.ToInt64(obj["L2CacheSize"] ?? 0) / 1024} MB");
                        sb.AppendLine($"  L3 Cache: {Convert.ToInt64(obj["L3CacheSize"] ?? 0) / 1024} MB");
                        sb.AppendLine($"  Obciążenie: {obj["LoadPercentage"]}%");
                        sb.AppendLine($"  Architektura: {GetArchitecture(Convert.ToInt32(obj["Architecture"] ?? 0))}");
                        sb.AppendLine($"  Status: {obj["Status"]}");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  Błąd WMI CPU: {ex.Message}");
            }

            // Performance Counter dla CPU
            try
            {
                if (_cpuCounter != null)
                {
                    Thread.Sleep(100); // Potrzebne dla dokładnego odczytu
                    var cpuUsage = _cpuCounter.NextValue();
                    sb.AppendLine($"  Aktualne użycie CPU: {cpuUsage:F1}%");
                }
            }
            catch { }

            // CPU per core
            try
            {
                sb.AppendLine("\n  --- Użycie CPU per rdzeń ---");
                for (int i = 0; i < Environment.ProcessorCount && i < 24; i++)
                {
                    try
                    {
                        using (var coreCounter = new PerformanceCounter("Processor", "% Processor Time", i.ToString(), true))
                        {
                            coreCounter.NextValue();
                            Thread.Sleep(50);
                            var usage = coreCounter.NextValue();
                            sb.AppendLine($"    Core {i}: {usage:F1}%");
                        }
                    }
                    catch { }
                }
            }
            catch { }
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 3: PAMIĘĆ RAM - SZCZEGÓŁOWE INFORMACJE
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 3. PAMIĘĆ RAM - SZCZEGÓŁOWE INFORMACJE                                                             │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");

            // WMI - Pamięć fizyczna
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var totalRam = Convert.ToInt64(obj["TotalPhysicalMemory"] ?? 0);
                        sb.AppendLine($"  Całkowita pamięć fizyczna: {totalRam / 1024 / 1024 / 1024:F2} GB ({totalRam / 1024 / 1024:N0} MB)");
                    }
                }

                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var freePhys = Convert.ToInt64(obj["FreePhysicalMemory"] ?? 0) * 1024;
                        var totalVirt = Convert.ToInt64(obj["TotalVirtualMemorySize"] ?? 0) * 1024;
                        var freeVirt = Convert.ToInt64(obj["FreeVirtualMemory"] ?? 0) * 1024;
                        sb.AppendLine($"  Wolna pamięć fizyczna: {freePhys / 1024 / 1024:N0} MB");
                        sb.AppendLine($"  Całkowita pamięć wirtualna: {totalVirt / 1024 / 1024:N0} MB");
                        sb.AppendLine($"  Wolna pamięć wirtualna: {freeVirt / 1024 / 1024:N0} MB");
                    }
                }

                // Moduły pamięci
                sb.AppendLine("\n  --- Moduły pamięci RAM ---");
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory"))
                {
                    int slot = 0;
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var capacity = Convert.ToInt64(obj["Capacity"] ?? 0);
                        var speed = obj["Speed"];
                        var manufacturer = obj["Manufacturer"];
                        sb.AppendLine($"    Slot {slot++}: {capacity / 1024 / 1024 / 1024} GB, {speed} MHz, {manufacturer}");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  Błąd WMI RAM: {ex.Message}");
            }

            // Performance counter
            try
            {
                if (_ramCounter != null)
                {
                    var availRam = _ramCounter.NextValue();
                    sb.AppendLine($"  Dostępna pamięć (Performance Counter): {availRam:N0} MB");
                }
            }
            catch { }
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 4: PROCES APLIKACJI
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 4. PROCES APLIKACJI                                                                                │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");

            var process = Process.GetCurrentProcess();
            sb.AppendLine($"  Nazwa procesu: {process.ProcessName}");
            sb.AppendLine($"  ID procesu (PID): {process.Id}");
            sb.AppendLine($"  Czas startu: {process.StartTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  Czas działania: {DateTime.Now - process.StartTime:d\\.hh\\:mm\\:ss}");
            sb.AppendLine($"  Priorytet: {process.PriorityClass}");
            sb.AppendLine($"  Priorytet bazowy: {process.BasePriority}");
            sb.AppendLine($"  Handles: {process.HandleCount}");
            sb.AppendLine($"  Threads: {process.Threads.Count}");
            sb.AppendLine($"  Moduły: {process.Modules.Count}");
            sb.AppendLine();
            sb.AppendLine("  --- Pamięć procesu ---");
            sb.AppendLine($"  Working Set: {process.WorkingSet64 / 1024 / 1024:N0} MB");
            sb.AppendLine($"  Peak Working Set: {process.PeakWorkingSet64 / 1024 / 1024:N0} MB");
            sb.AppendLine($"  Private Memory: {process.PrivateMemorySize64 / 1024 / 1024:N0} MB");
            sb.AppendLine($"  Virtual Memory: {process.VirtualMemorySize64 / 1024 / 1024:N0} MB");
            sb.AppendLine($"  Peak Virtual Memory: {process.PeakVirtualMemorySize64 / 1024 / 1024:N0} MB");
            sb.AppendLine($"  Paged Memory: {process.PagedMemorySize64 / 1024 / 1024:N0} MB");
            sb.AppendLine($"  Peak Paged Memory: {process.PeakPagedMemorySize64 / 1024 / 1024:N0} MB");
            sb.AppendLine($"  Paged System Memory: {process.PagedSystemMemorySize64 / 1024:N0} KB");
            sb.AppendLine($"  Nonpaged System Memory: {process.NonpagedSystemMemorySize64 / 1024:N0} KB");
            sb.AppendLine();
            sb.AppendLine("  --- Czas CPU procesu ---");
            sb.AppendLine($"  Total Processor Time: {process.TotalProcessorTime:hh\\:mm\\:ss\\.fff}");
            sb.AppendLine($"  User Processor Time: {process.UserProcessorTime:hh\\:mm\\:ss\\.fff}");
            sb.AppendLine($"  Privileged Processor Time: {process.PrivilegedProcessorTime:hh\\:mm\\:ss\\.fff}");
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 5: GARBAGE COLLECTOR
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 5. GARBAGE COLLECTOR                                                                               │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");

            sb.AppendLine($"  GC Total Memory: {GC.GetTotalMemory(false) / 1024 / 1024:N2} MB");
            sb.AppendLine($"  GC Gen0 Collections: {GC.CollectionCount(0)}");
            sb.AppendLine($"  GC Gen1 Collections: {GC.CollectionCount(1)}");
            sb.AppendLine($"  GC Gen2 Collections: {GC.CollectionCount(2)}");
            sb.AppendLine($"  GC Max Generation: {GC.MaxGeneration}");
            sb.AppendLine($"  GC Latency Mode: {System.Runtime.GCSettings.LatencyMode}");
            sb.AppendLine($"  GC Is Server GC: {System.Runtime.GCSettings.IsServerGC}");
            sb.AppendLine($"  Large Object Heap Compaction: {System.Runtime.GCSettings.LargeObjectHeapCompactionMode}");

            try
            {
                var gcInfo = GC.GetGCMemoryInfo();
                sb.AppendLine($"  Heap Size: {gcInfo.HeapSizeBytes / 1024 / 1024:N2} MB");
                sb.AppendLine($"  Fragmented Bytes: {gcInfo.FragmentedBytes / 1024 / 1024:N2} MB");
                sb.AppendLine($"  Memory Load: {gcInfo.MemoryLoadBytes / 1024 / 1024:N2} MB");
                sb.AppendLine($"  High Memory Load Threshold: {gcInfo.HighMemoryLoadThresholdBytes / 1024 / 1024:N2} MB");
                sb.AppendLine($"  Total Available Memory: {gcInfo.TotalAvailableMemoryBytes / 1024 / 1024:N2} MB");
            }
            catch { }
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 6: THREAD POOL
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 6. THREAD POOL                                                                                     │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");

            ThreadPool.GetMinThreads(out int minWorker, out int minIO);
            ThreadPool.GetMaxThreads(out int maxWorker, out int maxIO);
            ThreadPool.GetAvailableThreads(out int availWorker, out int availIO);

            sb.AppendLine($"  Worker Threads:");
            sb.AppendLine($"    Min: {minWorker}");
            sb.AppendLine($"    Max: {maxWorker}");
            sb.AppendLine($"    Dostępnych: {availWorker}");
            sb.AppendLine($"    Używanych: {maxWorker - availWorker}");
            sb.AppendLine($"  I/O Completion Threads:");
            sb.AppendLine($"    Min: {minIO}");
            sb.AppendLine($"    Max: {maxIO}");
            sb.AppendLine($"    Dostępnych: {availIO}");
            sb.AppendLine($"    Używanych: {maxIO - availIO}");
            sb.AppendLine($"  Pending Work Items: {ThreadPool.PendingWorkItemCount}");
            sb.AppendLine($"  Completed Work Items: {ThreadPool.CompletedWorkItemCount}");
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 7: DYSKI
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 7. DYSKI                                                                                           │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady)
                    {
                        sb.AppendLine($"  {drive.Name} ({drive.DriveType}, {drive.DriveFormat}):");
                        sb.AppendLine($"    Label: {drive.VolumeLabel}");
                        sb.AppendLine($"    Całkowita: {drive.TotalSize / 1024 / 1024 / 1024:N1} GB");
                        sb.AppendLine($"    Wolna: {drive.TotalFreeSpace / 1024 / 1024 / 1024:N1} GB");
                        sb.AppendLine($"    Użyta: {(drive.TotalSize - drive.TotalFreeSpace) / 1024 / 1024 / 1024:N1} GB ({100.0 * (drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize:F1}%)");
                    }
                }

                // WMI - Dyski fizyczne
                sb.AppendLine("\n  --- Dyski fizyczne (WMI) ---");
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var size = Convert.ToInt64(obj["Size"] ?? 0);
                        sb.AppendLine($"    {obj["Model"]}: {size / 1024 / 1024 / 1024:N0} GB, {obj["InterfaceType"]}, {obj["MediaType"]}");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  Błąd dysków: {ex.Message}");
            }
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 8: SIEĆ - SZCZEGÓŁOWE
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 8. SIEĆ - SZCZEGÓŁOWE INFORMACJE                                                                   │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");

            sb.AppendLine($"  Hostname: {Dns.GetHostName()}");
            try
            {
                var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in hostEntry.AddressList)
                {
                    sb.AppendLine($"  IP: {ip} ({ip.AddressFamily})");
                }
            }
            catch { }

            sb.AppendLine("\n  --- Interfejsy sieciowe ---");
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        sb.AppendLine($"  {ni.Name}:");
                        sb.AppendLine($"    Typ: {ni.NetworkInterfaceType}");
                        sb.AppendLine($"    Status: {ni.OperationalStatus}");
                        sb.AppendLine($"    Prędkość: {ni.Speed / 1000000} Mbps");
                        sb.AppendLine($"    MAC: {ni.GetPhysicalAddress()}");

                        var stats = ni.GetIPv4Statistics();
                        sb.AppendLine($"    Bajty wysłane: {stats.BytesSent / 1024 / 1024:N0} MB");
                        sb.AppendLine($"    Bajty odebrane: {stats.BytesReceived / 1024 / 1024:N0} MB");
                        sb.AppendLine($"    Pakiety wysłane: {stats.UnicastPacketsSent:N0}");
                        sb.AppendLine($"    Pakiety odebrane: {stats.UnicastPacketsReceived:N0}");
                        sb.AppendLine($"    Błędy wyjściowe: {stats.OutgoingPacketsWithErrors}");
                        sb.AppendLine($"    Błędy wejściowe: {stats.IncomingPacketsWithErrors}");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  Błąd sieci: {ex.Message}");
            }
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 9: PING I PORTY DO SERWERÓW SQL
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 9. TESTY POŁĄCZEŃ SIECIOWYCH DO SERWERÓW SQL                                                       │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");

            var servers = new[] {
                ("192.168.0.112", "Handel (SQL Server)"),
                ("192.168.0.109", "LibraNet (SQL Server)")
            };

            foreach (var (ip, name) in servers)
            {
                sb.AppendLine($"\n  === {name} ({ip}) ===");

                // DNS resolve
                try
                {
                    var sw = Stopwatch.StartNew();
                    var entry = await Dns.GetHostEntryAsync(ip);
                    sw.Stop();
                    sb.AppendLine($"    DNS Resolve: {sw.ElapsedMilliseconds}ms -> {entry.HostName}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"    DNS Resolve: FAILED - {ex.Message}");
                }

                // Ping test (10 prób)
                try
                {
                    using (var ping = new Ping())
                    {
                        var pingResults = new List<long>();
                        for (int i = 0; i < 10; i++)
                        {
                            var reply = await ping.SendPingAsync(ip, 1000);
                            if (reply.Status == IPStatus.Success)
                                pingResults.Add(reply.RoundtripTime);
                        }

                        if (pingResults.Any())
                        {
                            sb.AppendLine($"    Ping (10 prób): Min={pingResults.Min()}ms, Max={pingResults.Max()}ms, Avg={pingResults.Average():F1}ms, StdDev={StdDev(pingResults):F1}ms");
                            sb.AppendLine($"    Ping szczegóły: [{string.Join(", ", pingResults.Select(p => $"{p}ms"))}]");
                        }
                        else
                        {
                            sb.AppendLine($"    Ping: ALL FAILED");
                        }
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"    Ping ERROR: {ex.Message}");
                }

                // TCP Port test - rozszerzony
                var ports = new[] { 1433, 1434, 445, 135 };
                sb.AppendLine("    Porty:");
                foreach (var port in ports)
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        using (var client = new TcpClient())
                        {
                            var connectTask = client.ConnectAsync(ip, port);
                            if (await Task.WhenAny(connectTask, Task.Delay(2000)) == connectTask && client.Connected)
                            {
                                sw.Stop();
                                sb.AppendLine($"      Port {port}: OPEN ({sw.ElapsedMilliseconds}ms)");
                            }
                            else
                            {
                                sb.AppendLine($"      Port {port}: TIMEOUT (>2000ms)");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        sb.AppendLine($"      Port {port}: ERROR - {ex.Message.Split('\n')[0]}");
                    }
                }
            }
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 10: TESTY POŁĄCZEŃ SQL SERVER - SUPER SZCZEGÓŁOWE
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 10. TESTY POŁĄCZEŃ SQL SERVER - SUPER SZCZEGÓŁOWE                                                  │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");

            var connectionStrings = new Dictionary<string, string>
            {
                ["Handel"] = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True;Connection Timeout=30;Pooling=true;",
                ["LibraNet"] = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;Connection Timeout=30;Pooling=true;"
            };

            foreach (var kvp in connectionStrings)
            {
                sb.AppendLine($"\n  ══════ {kvp.Key} ══════");

                // Test połączeń
                var connectionTimes = new List<long>();
                for (int i = 1; i <= 5; i++)
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        using (var conn = new SqlConnection(kvp.Value))
                        {
                            await conn.OpenAsync();
                            sw.Stop();
                            connectionTimes.Add(sw.ElapsedMilliseconds);

                            if (i == 1) // Tylko dla pierwszego połączenia - szczegóły
                            {
                                sb.AppendLine($"  Server Version: {conn.ServerVersion}");
                                sb.AppendLine($"  Workstation ID: {conn.WorkstationId}");
                                sb.AppendLine($"  Client Connection ID: {conn.ClientConnectionId}");
                                sb.AppendLine($"  Database: {conn.Database}");
                                sb.AppendLine($"  Data Source: {conn.DataSource}");
                                sb.AppendLine($"  State: {conn.State}");
                                sb.AppendLine($"  Connection Timeout: {conn.ConnectionTimeout}s");
                                sb.AppendLine($"  Packet Size: {conn.PacketSize}");

                                // Server info
                                try
                                {
                                    using (var cmd = new SqlCommand(@"
                                        SELECT
                                            @@SERVERNAME as ServerName,
                                            @@VERSION as Version,
                                            @@SPID as SPID,
                                            SERVERPROPERTY('Edition') as Edition,
                                            SERVERPROPERTY('ProductLevel') as ProductLevel,
                                            SERVERPROPERTY('ProductVersion') as ProductVersion,
                                            SERVERPROPERTY('Collation') as Collation,
                                            SERVERPROPERTY('IsClustered') as IsClustered,
                                            SERVERPROPERTY('IsHadrEnabled') as IsHadrEnabled,
                                            cpu_count as CPUCount,
                                            physical_memory_kb / 1024 as PhysicalMemoryMB,
                                            committed_kb / 1024 as CommittedMemoryMB,
                                            committed_target_kb / 1024 as TargetMemoryMB,
                                            sqlserver_start_time as StartTime
                                        FROM sys.dm_os_sys_info
                                    ", conn))
                                    {
                                        using (var reader = await cmd.ExecuteReaderAsync())
                                        {
                                            if (await reader.ReadAsync())
                                            {
                                                sb.AppendLine($"\n  --- SQL Server Info ---");
                                                sb.AppendLine($"  Server Name: {reader["ServerName"]}");
                                                sb.AppendLine($"  Edition: {reader["Edition"]}");
                                                sb.AppendLine($"  Product Version: {reader["ProductVersion"]}");
                                                sb.AppendLine($"  Product Level: {reader["ProductLevel"]}");
                                                sb.AppendLine($"  Collation: {reader["Collation"]}");
                                                sb.AppendLine($"  Is Clustered: {reader["IsClustered"]}");
                                                sb.AppendLine($"  Is HADR Enabled: {reader["IsHadrEnabled"]}");
                                                sb.AppendLine($"  CPU Count: {reader["CPUCount"]}");
                                                sb.AppendLine($"  Physical Memory: {reader["PhysicalMemoryMB"]:N0} MB");
                                                sb.AppendLine($"  Committed Memory: {reader["CommittedMemoryMB"]:N0} MB");
                                                sb.AppendLine($"  Target Memory: {reader["TargetMemoryMB"]:N0} MB");
                                                sb.AppendLine($"  SQL Start Time: {reader["StartTime"]}");
                                                sb.AppendLine($"  SPID: {reader["SPID"]}");
                                            }
                                        }
                                    }
                                }
                                catch { }

                                // Wait stats
                                try
                                {
                                    sb.AppendLine($"\n  --- Top 5 Wait Stats ---");
                                    using (var cmd = new SqlCommand(@"
                                        SELECT TOP 5
                                            wait_type,
                                            wait_time_ms,
                                            waiting_tasks_count,
                                            signal_wait_time_ms
                                        FROM sys.dm_os_wait_stats
                                        WHERE wait_type NOT LIKE '%SLEEP%'
                                          AND wait_type NOT LIKE '%IDLE%'
                                          AND wait_type NOT LIKE '%QUEUE%'
                                          AND wait_type NOT IN ('CLR_AUTO_EVENT', 'BROKER_TASK_STOP', 'DISPATCHER_QUEUE_SEMAPHORE')
                                        ORDER BY wait_time_ms DESC
                                    ", conn))
                                    {
                                        using (var reader = await cmd.ExecuteReaderAsync())
                                        {
                                            while (await reader.ReadAsync())
                                            {
                                                sb.AppendLine($"    {reader["wait_type"]}: {Convert.ToInt64(reader["wait_time_ms"]) / 1000:N0}s, Tasks: {reader["waiting_tasks_count"]}");
                                            }
                                        }
                                    }
                                }
                                catch { }

                                // Active connections
                                try
                                {
                                    using (var cmd = new SqlCommand(@"
                                        SELECT
                                            COUNT(*) as TotalConnections,
                                            SUM(CASE WHEN status = 'running' THEN 1 ELSE 0 END) as Running,
                                            SUM(CASE WHEN status = 'sleeping' THEN 1 ELSE 0 END) as Sleeping,
                                            SUM(CASE WHEN status = 'suspended' THEN 1 ELSE 0 END) as Suspended
                                        FROM sys.dm_exec_sessions
                                        WHERE is_user_process = 1
                                    ", conn))
                                    {
                                        using (var reader = await cmd.ExecuteReaderAsync())
                                        {
                                            if (await reader.ReadAsync())
                                            {
                                                sb.AppendLine($"\n  --- Active Connections ---");
                                                sb.AppendLine($"  Total: {reader["TotalConnections"]}, Running: {reader["Running"]}, Sleeping: {reader["Sleeping"]}, Suspended: {reader["Suspended"]}");
                                            }
                                        }
                                    }
                                }
                                catch { }

                                // Buffer pool
                                try
                                {
                                    using (var cmd = new SqlCommand(@"
                                        SELECT
                                            (cntr_value * 8) / 1024 as BufferPoolMB
                                        FROM sys.dm_os_performance_counters
                                        WHERE counter_name = 'Database pages'
                                    ", conn))
                                    {
                                        var result = await cmd.ExecuteScalarAsync();
                                        if (result != null)
                                            sb.AppendLine($"  Buffer Pool: {result:N0} MB");
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        sb.AppendLine($"  Połączenie {i}: BŁĄD po {sw.ElapsedMilliseconds}ms - {ex.Message}");
                    }
                }

                if (connectionTimes.Any())
                {
                    sb.AppendLine($"\n  --- Czasy połączeń (5 prób) ---");
                    sb.AppendLine($"  Czasy: [{string.Join(", ", connectionTimes.Select(t => $"{t}ms"))}]");
                    sb.AppendLine($"  Min: {connectionTimes.Min()}ms, Max: {connectionTimes.Max()}ms, Avg: {connectionTimes.Average():F1}ms");
                    if (connectionTimes.Count >= 2)
                        sb.AppendLine($"  Cold (1st): {connectionTimes[0]}ms, Pool (2nd+): {connectionTimes.Skip(1).Average():F1}ms");
                }
            }
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 11: STATYSTYKI SESJI APLIKACJI
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 11. STATYSTYKI SESJI APLIKACJI                                                                     │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");

            var sessionDuration = DateTime.Now - _sessionStart;
            sb.AppendLine($"  Czas sesji diagnostycznej: {sessionDuration:hh\\:mm\\:ss\\.fff}");
            sb.AppendLine($"  Łączna liczba zapytań: {_totalQueries}");
            sb.AppendLine($"  Łączna liczba połączeń: {_totalConnections}");
            sb.AppendLine($"  Nieudane połączenia: {_failedConnections}");
            sb.AppendLine($"  Odczytane bajty: {_totalBytesRead / 1024:N0} KB ({_totalBytesRead / 1024 / 1024:N1} MB)");
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 12: CACHE STATUS - ROZSZERZONY
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 12. CACHE STATUS - ROZSZERZONY                                                                     │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine(SaldaService.GetCacheStatus());
            sb.AppendLine(OpakowaniaDataService.GetZestawieniaCacheStatus());
            sb.AppendLine(OpakowaniaDataService.GetSzczegolyCacheStatus());
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 13: PROFILER OPERACJI
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 13. PROFILER OPERACJI                                                                              │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine(PerformanceProfiler.GenerateReport());

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 14: LOADED ASSEMBLIES
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 14. LOADED ASSEMBLIES (Top 20 by name)                                                             │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");

            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .OrderBy(a => a.GetName().Name)
                    .Take(20);
                foreach (var asm in assemblies)
                {
                    var name = asm.GetName();
                    sb.AppendLine($"  {name.Name} v{name.Version}");
                }
            }
            catch { }
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // SEKCJA 15: ENVIRONMENT VARIABLES (SQL related)
            // ═══════════════════════════════════════════════════════════════════════════════════════
            sb.AppendLine("┌────────────────────────────────────────────────────────────────────────────────────────────────────┐");
            sb.AppendLine("│ 15. ENVIRONMENT VARIABLES (related)                                                                │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────────────────────────────┘");

            var relevantVars = new[] { "COMPUTERNAME", "USERDOMAIN", "PATH", "TEMP", "TMP", "PROCESSOR_IDENTIFIER", "NUMBER_OF_PROCESSORS" };
            foreach (var varName in relevantVars)
            {
                var value = Environment.GetEnvironmentVariable(varName);
                if (!string.IsNullOrEmpty(value))
                {
                    if (value.Length > 100)
                        value = value.Substring(0, 100) + "...";
                    sb.AppendLine($"  {varName}: {value}");
                }
            }
            sb.AppendLine();

            // ═══════════════════════════════════════════════════════════════════════════════════════
            // KONIEC
            // ═══════════════════════════════════════════════════════════════════════════════════════
            totalSw.Stop();
            sb.AppendLine("════════════════════════════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  RAPORT WYGENEROWANY W: {totalSw.ElapsedMilliseconds}ms ({totalSw.Elapsed.TotalSeconds:F1}s)");
            sb.AppendLine($"  ROZMIAR RAPORTU: {sb.Length:N0} znaków");
            sb.AppendLine("════════════════════════════════════════════════════════════════════════════════════════════════════");

            return sb.ToString();
        }

        private static string GetArchitecture(int arch)
        {
            return arch switch
            {
                0 => "x86",
                5 => "ARM",
                6 => "ia64",
                9 => "x64",
                12 => "ARM64",
                _ => $"Unknown ({arch})"
            };
        }

        private static double StdDev(List<long> values)
        {
            if (values.Count < 2) return 0;
            double avg = values.Average();
            double sum = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sum / (values.Count - 1));
        }

        /// <summary>
        /// Uruchamia pełny test wydajności z jeszcze większą ilością szczegółów
        /// </summary>
        public static async Task<string> RunFullPerformanceTestAsync()
        {
            var sb = new StringBuilder();
            var totalSw = Stopwatch.StartNew();

            sb.AppendLine("╔════════════════════════════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                    PEŁNY TEST WYDAJNOŚCI v2.0                                                      ║");
            sb.AppendLine($"║                    {DateTime.Now:yyyy-MM-dd HH:mm:ss}                                                              ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            // CPU przed testem
            float cpuBefore = 0;
            try { if (_cpuCounter != null) cpuBefore = _cpuCounter.NextValue(); } catch { }

            var service = new SaldaService();

            // TEST 1: Ładowanie sald (5 prób z pełnymi statystykami)
            sb.AppendLine("═══ TEST 1: ŁADOWANIE SALD (5 prób) ═══");
            var saldaTimes = new List<long>();
            var saldaCounts = new List<int>();
            for (int i = 0; i < 5; i++)
            {
                if (i > 0) SaldaService.InvalidateCache();

                var sw = Stopwatch.StartNew();
                var salda = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
                sw.Stop();
                saldaTimes.Add(sw.ElapsedMilliseconds);
                saldaCounts.Add(salda.Count);
                sb.AppendLine($"  Próba {i + 1}: {sw.ElapsedMilliseconds}ms ({salda.Count} rekordów) - {1000.0 * salda.Count / sw.ElapsedMilliseconds:F1} rek/s");
            }
            sb.AppendLine($"  ────────────────────────────────────────");
            sb.AppendLine($"  ŚREDNIA: {saldaTimes.Average():F0}ms");
            sb.AppendLine($"  MIN: {saldaTimes.Min()}ms, MAX: {saldaTimes.Max()}ms");
            sb.AppendLine($"  MEDIANA: {saldaTimes.OrderBy(x => x).ElementAt(saldaTimes.Count / 2)}ms");
            sb.AppendLine($"  STD DEV: {StdDev(saldaTimes):F1}ms");
            sb.AppendLine();

            // TEST 2: Cache test (10 prób)
            sb.AppendLine("═══ TEST 2: CACHE TEST (10 prób bez invalidacji) ═══");
            var cacheTimes = new List<long>();
            for (int i = 0; i < 10; i++)
            {
                var sw = Stopwatch.StartNew();
                var salda = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
                sw.Stop();
                cacheTimes.Add(sw.ElapsedMilliseconds);
            }
            sb.AppendLine($"  Czasy: [{string.Join(", ", cacheTimes.Select(t => $"{t}ms"))}]");
            sb.AppendLine($"  ŚREDNIA: {cacheTimes.Average():F1}ms (powinno być ~0-1ms)");
            sb.AppendLine($"  Cache działa: {(cacheTimes.Average() < 10 ? "TAK ✓" : "NIE ✗")}");
            sb.AppendLine();

            // TEST 3: Dokumenty dla pierwszych 10 kontrahentów
            sb.AppendLine("═══ TEST 3: DOKUMENTY (10 kontrahentów) ═══");
            var salda2 = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
            var docTimes = new List<(string Name, long Time, int Docs)>();
            foreach (var s in salda2.Take(10))
            {
                var sw = Stopwatch.StartNew();
                var docs = await service.PobierzDokumentyAsync(s.Id, DateTime.Today.AddMonths(-6), DateTime.Today);
                sw.Stop();
                var name = s.Kontrahent?.Length > 30 ? s.Kontrahent.Substring(0, 30) : s.Kontrahent ?? "?";
                docTimes.Add((name, sw.ElapsedMilliseconds, docs.Count));
                sb.AppendLine($"  {name}: {sw.ElapsedMilliseconds}ms ({docs.Count} docs)");
            }
            if (docTimes.Any())
            {
                sb.AppendLine($"  ────────────────────────────────────────");
                sb.AppendLine($"  ŚREDNIA: {docTimes.Average(d => d.Time):F0}ms");
                sb.AppendLine($"  MIN: {docTimes.Min(d => d.Time)}ms, MAX: {docTimes.Max(d => d.Time)}ms");
            }
            sb.AppendLine();

            // TEST 4: Równoległe ładowanie
            sb.AppendLine("═══ TEST 4: RÓWNOLEGŁE ŁADOWANIE (20 kontrahentów) ═══");
            OpakowaniaDataService.InvalidateSzczegolyCache();
            var swParallel = Stopwatch.StartNew();
            var tasks = salda2.Take(20).Select(s =>
                service.PobierzDokumentyAsync(s.Id, DateTime.Today.AddMonths(-3), DateTime.Today)).ToList();
            await Task.WhenAll(tasks);
            swParallel.Stop();
            sb.AppendLine($"  Łączny czas: {swParallel.ElapsedMilliseconds}ms dla 20 równoległych zapytań");
            sb.AppendLine($"  Średni czas per zapytanie: {swParallel.ElapsedMilliseconds / 20.0:F1}ms");
            sb.AppendLine($"  Speedup vs sequential: ~{20.0 * docTimes.Average(d => d.Time) / swParallel.ElapsedMilliseconds:F1}x");
            sb.AppendLine();

            // TEST 5: Cache szczegółów (ponowne wejście)
            sb.AppendLine("═══ TEST 5: CACHE SZCZEGÓŁÓW (ponowne wejście w tego samego kontrahenta) ═══");
            if (salda2.Any())
            {
                var testKontrahent = salda2.First();
                var dataService = new OpakowaniaDataService();

                // Pierwsze ładowanie
                var sw1 = Stopwatch.StartNew();
                await dataService.PobierzSaldoKontrahentaAsync(testKontrahent.Id, DateTime.Today.AddMonths(-2), DateTime.Today);
                sw1.Stop();
                sb.AppendLine($"  Pierwsze wejście: {sw1.ElapsedMilliseconds}ms");

                // Drugie ładowanie (powinno być z cache)
                var sw2 = Stopwatch.StartNew();
                await dataService.PobierzSaldoKontrahentaAsync(testKontrahent.Id, DateTime.Today.AddMonths(-2), DateTime.Today);
                sw2.Stop();
                sb.AppendLine($"  Drugie wejście (cache): {sw2.ElapsedMilliseconds}ms");

                sb.AppendLine($"  Przyspieszenie: {(sw1.ElapsedMilliseconds > 0 ? sw1.ElapsedMilliseconds / (sw2.ElapsedMilliseconds + 0.1) : 0):F0}x");
            }
            sb.AppendLine();

            // CPU po teście
            float cpuAfter = 0;
            try { if (_cpuCounter != null) { Thread.Sleep(100); cpuAfter = _cpuCounter.NextValue(); } } catch { }

            totalSw.Stop();
            sb.AppendLine("════════════════════════════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  CAŁKOWITY CZAS TESTU: {totalSw.ElapsedMilliseconds}ms ({totalSw.Elapsed.TotalSeconds:F1}s)");
            sb.AppendLine($"  CPU przed testem: {cpuBefore:F1}%");
            sb.AppendLine($"  CPU po teście: {cpuAfter:F1}%");
            sb.AppendLine("════════════════════════════════════════════════════════════════════════════════════════════════════");

            return sb.ToString();
        }
    }
}
