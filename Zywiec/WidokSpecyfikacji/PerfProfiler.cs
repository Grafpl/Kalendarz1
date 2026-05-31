using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Kalendarz1.Zywiec.WidokSpecyfikacji
{
    /// <summary>
    /// Lekki profiler wydajności — mierzy czas kroków + liczbę zapytań SQL.
    /// Użycie:
    ///   using (PerfProfiler.Measure("LoadData")) { ... }
    ///   PerfProfiler.CountSql();   // przy każdym zapytaniu
    ///   string raport = PerfProfiler.GetReport();
    /// Wątkowo bezpieczny (lock). Reset() na początku operacji.
    /// </summary>
    public static class PerfProfiler
    {
        public class Wpis
        {
            public string Label { get; set; } = "";
            public string Kategoria { get; set; } = "";   // SQL / WPF / IO / CALC
            public long Ms { get; set; }
            public int Glebokosc { get; set; }
            public int SqlW { get; set; }     // liczba zapytan SQL w tym kroku
            public int IoW { get; set; }      // liczba operacji IO (File.Exists itp.)
        }

        private static readonly object _lock = new();
        private static readonly List<Wpis> _wpisy = new();
        private static int _sqlCount;
        private static int _ioCount;
        private static int _depth;
        private static readonly Stopwatch _total = new();
        public static bool Enabled { get; set; } = true;

        public static void Reset()
        {
            lock (_lock)
            {
                _wpisy.Clear();
                _sqlCount = 0;
                _ioCount = 0;
                _depth = 0;
                _total.Restart();
            }
        }

        public static void CountSql(int n = 1) { lock (_lock) _sqlCount += n; }
        public static void CountIo(int n = 1)  { lock (_lock) _ioCount += n; }

        /// <summary>Ręcznie dopisuje wpis (np. czas render zmierzony osobnym stopwatchem).</summary>
        public static void DodajWpis(string label, string kategoria, long ms, int glebokosc = 0)
        {
            lock (_lock)
                _wpisy.Add(new Wpis { Label = label, Kategoria = kategoria, Ms = ms, Glebokosc = glebokosc });
        }

        public static IDisposable Measure(string label, string kategoria = "")
        {
            if (!Enabled) return new NullScope();
            return new Scope(label, kategoria);
        }

        private sealed class NullScope : IDisposable { public void Dispose() { } }

        private sealed class Scope : IDisposable
        {
            private readonly string _label;
            private readonly string _kat;
            private readonly int _glebokosc;
            private readonly int _sqlStart;
            private readonly int _ioStart;
            private readonly Stopwatch _sw = Stopwatch.StartNew();

            public Scope(string label, string kat)
            {
                _label = label;
                _kat = kat;
                lock (_lock) { _glebokosc = _depth; _depth++; _sqlStart = _sqlCount; _ioStart = _ioCount; }
            }

            public void Dispose()
            {
                _sw.Stop();
                lock (_lock)
                {
                    _depth--;
                    _wpisy.Add(new Wpis
                    {
                        Label = _label,
                        Kategoria = _kat,
                        Ms = _sw.ElapsedMilliseconds,
                        Glebokosc = _glebokosc,
                        SqlW = _sqlCount - _sqlStart,
                        IoW = _ioCount - _ioStart
                    });
                }
            }
        }

        public static List<Wpis> Snapshot()
        {
            lock (_lock) return _wpisy.ToList();
        }

        public static (long totalMs, int sql, int io) Podsumowanie()
        {
            lock (_lock) return (_total.ElapsedMilliseconds, _sqlCount, _ioCount);
        }

        public static string GetReport()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("════════ RAPORT WYDAJNOSCI ════════");
                sb.AppendLine($"Razem: {_total.ElapsedMilliseconds} ms  |  SQL: {_sqlCount} zapytań  |  IO: {_ioCount} operacji");
                sb.AppendLine("───────────────────────────────────");

                // Posortowane wg czasu malejaco (top bottlenecks)
                var topCzas = _wpisy.OrderByDescending(w => w.Ms).Take(15).ToList();
                sb.AppendLine("TOP 15 najwolniejszych kroków:");
                foreach (var w in topCzas)
                {
                    double proc = _total.ElapsedMilliseconds > 0 ? (w.Ms * 100.0 / _total.ElapsedMilliseconds) : 0;
                    string extra = "";
                    if (w.SqlW > 0) extra += $"  [{w.SqlW} SQL]";
                    if (w.IoW > 0) extra += $"  [{w.IoW} IO]";
                    sb.AppendLine($"  {w.Ms,6} ms ({proc,5:F1}%)  {w.Kategoria,-4}  {w.Label}{extra}");
                }

                sb.AppendLine("───────────────────────────────────");
                sb.AppendLine("Kolejność wykonania (z wcięciami):");
                foreach (var w in _wpisy)
                {
                    string wciecie = new string(' ', w.Glebokosc * 2);
                    string extra = "";
                    if (w.SqlW > 0) extra += $"  [{w.SqlW} SQL]";
                    if (w.IoW > 0) extra += $"  [{w.IoW} IO]";
                    sb.AppendLine($"  {w.Ms,6} ms  {wciecie}{w.Label}{extra}");
                }
                return sb.ToString();
            }
        }
    }
}
