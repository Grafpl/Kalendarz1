using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Opakowania.Models;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// Serwis do pobierania sald opakowań - ULTRA-ZOPTYMALIZOWANY
    /// CACHE: Dane ładowane RAZ i trzymane do ręcznego odświeżenia
    /// </summary>
    public class SaldaService : IDisposable
    {
        #region Connection Strings

        private static readonly string _connectionStringHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True;" +
            "Min Pool Size=2;Max Pool Size=10;Connection Lifetime=300;Pooling=true;";

        private static readonly string _connectionStringLibraNet =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;" +
            "Min Pool Size=1;Max Pool Size=5;Connection Lifetime=300;Pooling=true;";

        #endregion

        #region AGGRESSIVE Cache - dane ładowane RAZ

        // Cache sald - BARDZO DŁUGI (8 godzin lub do ręcznego odświeżenia)
        private static List<SaldoKontrahenta> _cacheSalda;
        private static DateTime _cacheSaldaTime;
        private static DateTime _cacheSaldaDataDo;
        private static string _cacheSaldaHandlowiec;
        private static readonly TimeSpan CacheSaldaLifetime = TimeSpan.FromHours(8); // 8 GODZIN!

        // Cache potwierdzeń - długi
        private static Dictionary<(int, string), DateTime> _cachePotwierdzenia;
        private static DateTime _cachePotwierdzeniaTime;
        private static readonly TimeSpan CachePotwierdzeniaLifetime = TimeSpan.FromHours(4);

        // Cache handlowców - bardzo długi (cały dzień)
        private static readonly ConcurrentDictionary<string, string> _cacheHandlowcy = new();
        private static DateTime _cacheHandlowcyTime;
        private static readonly TimeSpan CacheHandlowcyLifetime = TimeSpan.FromHours(24);

        // Cache dokumentów - per kontrahent (1 godzina)
        private static readonly ConcurrentDictionary<string, (List<DokumentSalda> Data, DateTime Time)> _cacheDokumenty = new();
        private static readonly TimeSpan CacheDokumentyLifetime = TimeSpan.FromHours(1);
        private const int MaxCacheDokumentySize = 100;

        // Lock objects
        private static readonly SemaphoreSlim _lockSalda = new(1, 1);
        private static readonly SemaphoreSlim _lockPotwierdzenia = new(1, 1);

        private static bool IsCacheSaldaValid(DateTime dataDo, string handlowiec)
        {
            if (_cacheSalda == null)
            {
                PerformanceProfiler.RecordCacheMiss("Salda");
                return false;
            }
            if (DateTime.Now - _cacheSaldaTime > CacheSaldaLifetime)
            {
                PerformanceProfiler.RecordCacheMiss("Salda-Expired");
                return false;
            }
            if (_cacheSaldaDataDo != dataDo.Date)
            {
                PerformanceProfiler.RecordCacheMiss("Salda-DateChanged");
                return false;
            }
            if (_cacheSaldaHandlowiec != handlowiec)
            {
                PerformanceProfiler.RecordCacheMiss("Salda-HandlowiecChanged");
                return false;
            }
            PerformanceProfiler.RecordCacheHit("Salda");
            return true;
        }

        private static bool IsCachePotwierdzeniaValid()
        {
            if (_cachePotwierdzenia == null)
            {
                PerformanceProfiler.RecordCacheMiss("Potwierdzenia");
                return false;
            }
            if (DateTime.Now - _cachePotwierdzeniaTime > CachePotwierdzeniaLifetime)
            {
                PerformanceProfiler.RecordCacheMiss("Potwierdzenia-Expired");
                return false;
            }
            PerformanceProfiler.RecordCacheHit("Potwierdzenia");
            return true;
        }

        /// <summary>
        /// Invaliduje cache sald (ręczne odświeżenie)
        /// </summary>
        public static void InvalidateCache()
        {
            _cacheSalda = null;
            Debug.WriteLine("[CACHE] Salda cache invalidated");
        }

        /// <summary>
        /// Invaliduje WSZYSTKIE cache'e
        /// </summary>
        public static void InvalidateAllCaches()
        {
            _cacheSalda = null;
            _cachePotwierdzenia = null;
            _cacheHandlowcy.Clear();
            _cacheDokumenty.Clear();
            Debug.WriteLine("[CACHE] All caches invalidated");
        }

        /// <summary>
        /// Invaliduje cache potwierdzeń
        /// </summary>
        public static void InvalidatePotwierdzeniaCache()
        {
            _cachePotwierdzenia = null;
        }

        /// <summary>
        /// Zwraca informacje o stanie cache
        /// </summary>
        public static string GetCacheStatus()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== STAN CACHE ===");

            if (_cacheSalda != null)
            {
                var age = DateTime.Now - _cacheSaldaTime;
                var remaining = CacheSaldaLifetime - age;
                sb.AppendLine($"Salda: {_cacheSalda.Count} rekordów, wiek: {age:hh\\:mm\\:ss}, pozostało: {remaining:hh\\:mm\\:ss}");
            }
            else
            {
                sb.AppendLine("Salda: PUSTY");
            }

            if (_cachePotwierdzenia != null)
            {
                var age = DateTime.Now - _cachePotwierdzeniaTime;
                sb.AppendLine($"Potwierdzenia: {_cachePotwierdzenia.Count} rekordów, wiek: {age:hh\\:mm\\:ss}");
            }
            else
            {
                sb.AppendLine("Potwierdzenia: PUSTY");
            }

            sb.AppendLine($"Handlowcy: {_cacheHandlowcy.Count} rekordów");
            sb.AppendLine($"Dokumenty: {_cacheDokumenty.Count} kontrahentów");

            return sb.ToString();
        }

        #endregion

        #region SQL Queries

        private static readonly string QuerySalda = @"
SELECT
    C.id AS Id,
    C.Shortcut AS Kontrahent,
    C.Name AS Nazwa,
    ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS E2,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS H1,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS EURO,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS PCV,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS DREW,
    MAX(MG.Data) AS OstatniDokument
FROM [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MG] MG WITH (NOLOCK) ON MG.khid = C.id
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON C.Id = WYM.ElementId
WHERE MG.anulowany = 0
  AND MG.magazyn = 65559
  AND MG.typ_dk IN ('MW1', 'MP')
  AND MG.data <= @DataDo
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
  AND (@HandlowiecFilter IS NULL OR WYM.CDim_Handlowiec_Val = @HandlowiecFilter)
GROUP BY C.id, C.Shortcut, C.Name, WYM.CDim_Handlowiec_Val
HAVING ABS(SUM(CASE WHEN TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
              THEN MZ.Ilosc ELSE 0 END)) > 0
ORDER BY C.Shortcut";

        private static readonly string QueryPotwierdzenia = @"
SELECT KontrahentId, KodOpakowania, MAX(DataPotwierdzenia) AS DataPotwierdzenia
FROM [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan] WITH (NOLOCK)
WHERE StatusPotwierdzenia = 'Potwierdzone'
  AND DataPotwierdzenia >= DATEADD(DAY, -30, GETDATE())
GROUP BY KontrahentId, KodOpakowania";

        private static readonly string QuerySaldoPoczatkowe = @"
SELECT
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS E2,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS H1,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS EURO,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS PCV,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS DREW
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
WHERE MG.khid = @KontrahentId
  AND MG.anulowany = 0
  AND MG.magazyn = 65559
  AND MG.typ_dk IN ('MW1', 'MP')
  AND MG.data <= @DataOd
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')";

        private static readonly string QueryDokumenty = @"
SELECT
    MG.id AS Id,
    MG.kod AS NrDokumentu,
    MG.data AS Data,
    MG.opis AS Opis,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS E2,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS H1,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS EURO,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS PCV,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS DREW
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
WHERE MG.khid = @KontrahentId
  AND MG.anulowany = 0
  AND MG.magazyn = 65559
  AND MG.typ_dk IN ('MW1', 'MP')
  AND MG.data > @DataOd
  AND MG.data <= @DataDo
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
GROUP BY MG.id, MG.kod, MG.data, MG.opis
ORDER BY MG.data DESC";

        #endregion

        #region Pobieranie sald

        /// <summary>
        /// Pobiera salda - z CACHE (dane ładowane RAZ)
        /// </summary>
        public async Task<List<SaldoKontrahenta>> PobierzWszystkieSaldaAsync(DateTime dataDo, string handlowiecFilter = null)
        {
            // FAST PATH: zwróć z cache
            if (IsCacheSaldaValid(dataDo, handlowiecFilter))
            {
                Debug.WriteLine($"[CACHE HIT] Salda returned from cache ({_cacheSalda.Count} records)");
                return _cacheSalda;
            }

            await _lockSalda.WaitAsync();
            try
            {
                // Double-check
                if (IsCacheSaldaValid(dataDo, handlowiecFilter))
                {
                    return _cacheSalda;
                }

                Debug.WriteLine("[CACHE MISS] Loading salda from database...");

                using (PerformanceProfiler.MeasureOperation("PobierzWszystkieSalda_TOTAL"))
                {
                    // PARALLEL: pobierz potwierdzenia i salda równolegle
                    Task<Dictionary<(int, string), DateTime>> taskPotwierdzenia;
                    Task<List<SaldoKontrahenta>> taskSalda;

                    using (PerformanceProfiler.MeasureOperation("StartParallelTasks"))
                    {
                        taskPotwierdzenia = PobierzWszystkiePotwierdzeniaAsync();
                        taskSalda = PobierzSaldaZBazyAsync(dataDo, handlowiecFilter);
                    }

                    await Task.WhenAll(taskPotwierdzenia, taskSalda);

                    var potwierdzenia = await taskPotwierdzenia;
                    var wyniki = await taskSalda;

                    using (PerformanceProfiler.MeasureOperation("MergePotwierdzenia"))
                    {
                        // Przypisz potwierdzenia O(n)
                        foreach (var saldo in wyniki)
                        {
                            if (potwierdzenia.TryGetValue((saldo.Id, "E2"), out var pe2))
                            {
                                saldo.E2Potwierdzone = true;
                                saldo.E2DataPotwierdzenia = pe2;
                            }
                            if (potwierdzenia.TryGetValue((saldo.Id, "H1"), out var ph1))
                            {
                                saldo.H1Potwierdzone = true;
                                saldo.H1DataPotwierdzenia = ph1;
                            }
                            if (potwierdzenia.TryGetValue((saldo.Id, "EURO"), out var peuro))
                            {
                                saldo.EUROPotwierdzone = true;
                                saldo.EURODataPotwierdzenia = peuro;
                            }
                            if (potwierdzenia.TryGetValue((saldo.Id, "PCV"), out var ppcv))
                            {
                                saldo.PCVPotwierdzone = true;
                                saldo.PCVDataPotwierdzenia = ppcv;
                            }
                            if (potwierdzenia.TryGetValue((saldo.Id, "DREW"), out var pdrew))
                            {
                                saldo.DREWPotwierdzone = true;
                                saldo.DREWDataPotwierdzenia = pdrew;
                            }
                        }
                    }

                    // Zapisz w cache
                    _cacheSalda = wyniki;
                    _cacheSaldaTime = DateTime.Now;
                    _cacheSaldaDataDo = dataDo.Date;
                    _cacheSaldaHandlowiec = handlowiecFilter;

                    Debug.WriteLine($"[CACHE] Saved {wyniki.Count} records, valid for {CacheSaldaLifetime.TotalHours}h");

                    return wyniki;
                }
            }
            finally
            {
                _lockSalda.Release();
            }
        }

        /// <summary>
        /// Pobiera salda z bazy
        /// </summary>
        private async Task<List<SaldoKontrahenta>> PobierzSaldaZBazyAsync(DateTime dataDo, string handlowiecFilter)
        {
            var wyniki = new List<SaldoKontrahenta>(500);
            var sw = Stopwatch.StartNew();

            try
            {
                using (PerformanceProfiler.MeasureOperation("SQL_OpenConnection_Handel"))
                {
                    using var connection = new SqlConnection(_connectionStringHandel);
                    await connection.OpenAsync().ConfigureAwait(false);

                    using (PerformanceProfiler.MeasureOperation("SQL_ExecuteQuery_Salda"))
                    {
                        using var command = new SqlCommand(QuerySalda, connection);
                        command.CommandTimeout = 120;
                        command.Parameters.AddWithValue("@DataDo", dataDo);
                        command.Parameters.AddWithValue("@HandlowiecFilter", (object)handlowiecFilter ?? DBNull.Value);

                        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            wyniki.Add(new SaldoKontrahenta
                            {
                                Id = reader.GetInt32(0),
                                Kontrahent = reader.GetString(1),
                                Nazwa = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Handlowiec = reader.GetString(3),
                                E2 = reader.GetInt32(4),
                                H1 = reader.GetInt32(5),
                                EURO = reader.GetInt32(6),
                                PCV = reader.GetInt32(7),
                                DREW = reader.GetInt32(8),
                                OstatniDokument = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] PobierzSaldaZBazy: {ex.Message}");
                throw new Exception($"Błąd podczas pobierania sald: {ex.Message}", ex);
            }

            sw.Stop();
            Debug.WriteLine($"[PERF] PobierzSaldaZBazy: {wyniki.Count} records in {sw.ElapsedMilliseconds}ms");
            PerformanceProfiler.RecordTiming("PobierzSaldaZBazy_Records", sw.Elapsed, wyniki.Count);

            return wyniki;
        }

        #endregion

        #region Dokumenty

        /// <summary>
        /// Pobiera dokumenty kontrahenta - z cache
        /// </summary>
        public async Task<List<DokumentSalda>> PobierzDokumentyAsync(int kontrahentId, DateTime dataOd, DateTime dataDo)
        {
            var cacheKey = $"{kontrahentId}_{dataOd:yyyyMMdd}_{dataDo:yyyyMMdd}";

            // Sprawdź cache
            if (_cacheDokumenty.TryGetValue(cacheKey, out var cached))
            {
                if (DateTime.Now - cached.Time <= CacheDokumentyLifetime)
                {
                    PerformanceProfiler.RecordCacheHit("Dokumenty");
                    Debug.WriteLine($"[CACHE HIT] Dokumenty for {kontrahentId}");
                    return cached.Data;
                }
            }
            PerformanceProfiler.RecordCacheMiss("Dokumenty");

            // Pobierz z bazy
            List<DokumentSalda> dokumenty;
            using (PerformanceProfiler.MeasureOperation("PobierzDokumenty"))
            {
                dokumenty = await PobierzDokumentyZBazyAsync(kontrahentId, dataOd, dataDo);
            }

            // Cache management
            if (_cacheDokumenty.Count >= MaxCacheDokumentySize)
            {
                var oldest = _cacheDokumenty.OrderBy(x => x.Value.Time).Take(20).Select(x => x.Key).ToList();
                foreach (var key in oldest)
                {
                    _cacheDokumenty.TryRemove(key, out _);
                }
            }

            _cacheDokumenty[cacheKey] = (dokumenty, DateTime.Now);

            return dokumenty;
        }

        /// <summary>
        /// Pobiera dokumenty z bazy
        /// </summary>
        private async Task<List<DokumentSalda>> PobierzDokumentyZBazyAsync(int kontrahentId, DateTime dataOd, DateTime dataDo)
        {
            var dokumenty = new List<DokumentSalda>(50);
            var sw = Stopwatch.StartNew();

            try
            {
                // PARALLEL: pobierz saldo początkowe i dokumenty równolegle
                var taskSaldoPoczatkowe = Task.Run(async () =>
                {
                    using (PerformanceProfiler.MeasureOperation("SQL_SaldoPoczatkowe"))
                    {
                        using var conn = new SqlConnection(_connectionStringHandel);
                        await conn.OpenAsync().ConfigureAwait(false);

                        using var cmd = new SqlCommand(QuerySaldoPoczatkowe, conn);
                        cmd.CommandTimeout = 60;
                        cmd.Parameters.AddWithValue("@KontrahentId", kontrahentId);
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);

                        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                        if (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4));
                        }
                        return (0, 0, 0, 0, 0);
                    }
                });

                var taskDokumenty = Task.Run(async () =>
                {
                    using (PerformanceProfiler.MeasureOperation("SQL_ListaDokumentow"))
                    {
                        using var conn = new SqlConnection(_connectionStringHandel);
                        await conn.OpenAsync().ConfigureAwait(false);

                        using var cmd = new SqlCommand(QueryDokumenty, conn);
                        cmd.CommandTimeout = 60;
                        cmd.Parameters.AddWithValue("@KontrahentId", kontrahentId);
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);
                        cmd.Parameters.AddWithValue("@DataDo", dataDo);

                        var docs = new List<DokumentSalda>(30);
                        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            docs.Add(new DokumentSalda
                            {
                                Id = reader.GetInt32(0),
                                NrDokumentu = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Data = reader.GetDateTime(2),
                                Opis = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                E2 = reader.GetInt32(4),
                                H1 = reader.GetInt32(5),
                                EURO = reader.GetInt32(6),
                                PCV = reader.GetInt32(7),
                                DREW = reader.GetInt32(8),
                                JestSaldem = false
                            });
                        }
                        return docs;
                    }
                });

                await Task.WhenAll(taskSaldoPoczatkowe, taskDokumenty);

                var (sE2, sH1, sEURO, sPCV, sDREW) = await taskSaldoPoczatkowe;
                var listaDoc = await taskDokumenty;

                // Oblicz saldo końcowe
                int kE2 = sE2, kH1 = sH1, kEURO = sEURO, kPCV = sPCV, kDREW = sDREW;
                foreach (var doc in listaDoc)
                {
                    kE2 += doc.E2;
                    kH1 += doc.H1;
                    kEURO += doc.EURO;
                    kPCV += doc.PCV;
                    kDREW += doc.DREW;
                }

                // Saldo końcowe
                dokumenty.Add(new DokumentSalda
                {
                    NrDokumentu = $"Saldo na {dataDo:dd.MM.yyyy}",
                    Data = dataDo,
                    E2 = kE2, H1 = kH1, EURO = kEURO, PCV = kPCV, DREW = kDREW,
                    JestSaldem = true
                });

                dokumenty.AddRange(listaDoc);

                // Saldo początkowe
                dokumenty.Add(new DokumentSalda
                {
                    NrDokumentu = $"Saldo na {dataOd:dd.MM.yyyy}",
                    Data = dataOd,
                    E2 = sE2, H1 = sH1, EURO = sEURO, PCV = sPCV, DREW = sDREW,
                    JestSaldem = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] PobierzDokumentyZBazy: {ex.Message}");
                throw new Exception($"Błąd podczas pobierania dokumentów: {ex.Message}", ex);
            }

            sw.Stop();
            Debug.WriteLine($"[PERF] PobierzDokumentyZBazy: {dokumenty.Count} records in {sw.ElapsedMilliseconds}ms");

            return dokumenty;
        }

        #endregion

        #region Potwierdzenia

        private async Task<Dictionary<(int KontrahentId, string Typ), DateTime>> PobierzWszystkiePotwierdzeniaAsync()
        {
            if (IsCachePotwierdzeniaValid())
            {
                return _cachePotwierdzenia;
            }

            await _lockPotwierdzenia.WaitAsync();
            try
            {
                if (IsCachePotwierdzeniaValid())
                {
                    return _cachePotwierdzenia;
                }

                var wyniki = new Dictionary<(int, string), DateTime>(200);
                var sw = Stopwatch.StartNew();

                try
                {
                    using (PerformanceProfiler.MeasureOperation("SQL_Potwierdzenia"))
                    {
                        using var connection = new SqlConnection(_connectionStringLibraNet);
                        await connection.OpenAsync().ConfigureAwait(false);

                        using var command = new SqlCommand(QueryPotwierdzenia, connection);
                        command.CommandTimeout = 30;

                        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            var kontrahentId = reader.GetInt32(0);
                            var kod = reader.GetString(1);
                            var data = reader.GetDateTime(2);
                            wyniki[(kontrahentId, kod)] = data;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WARN] Potwierdzenia error (ignored): {ex.Message}");
                    return new Dictionary<(int, string), DateTime>();
                }

                _cachePotwierdzenia = wyniki;
                _cachePotwierdzeniaTime = DateTime.Now;

                sw.Stop();
                Debug.WriteLine($"[PERF] PobierzPotwierdzenia: {wyniki.Count} records in {sw.ElapsedMilliseconds}ms");

                return wyniki;
            }
            finally
            {
                _lockPotwierdzenia.Release();
            }
        }

        /// <summary>
        /// Dodaje nowe potwierdzenie
        /// </summary>
        public async Task<int> DodajPotwierdzenieAsync(int kontrahentId, string kontrahentNazwa, string typOpakowania,
            int iloscPotwierdzona, int saldoSystemowe, string status, string uwagi, string uzytkownikId, string uzytkownikNazwa)
        {
            string query = @"
INSERT INTO [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan]
(KontrahentId, KontrahentNazwa, KontrahentShortcut, TypOpakowania, KodOpakowania,
 DataPotwierdzenia, IloscPotwierdzona, SaldoSystemowe, StatusPotwierdzenia,
 Uwagi, UzytkownikId, UzytkownikNazwa)
VALUES
(@KontrahentId, @KontrahentNazwa, @KontrahentNazwa, @TypOpakowania, @KodOpakowania,
 GETDATE(), @IloscPotwierdzona, @SaldoSystemowe, @StatusPotwierdzenia,
 @Uwagi, @UzytkownikId, @UzytkownikNazwa);
SELECT SCOPE_IDENTITY();";

            try
            {
                using var connection = new SqlConnection(_connectionStringLibraNet);
                await connection.OpenAsync().ConfigureAwait(false);

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@KontrahentId", kontrahentId);
                command.Parameters.AddWithValue("@KontrahentNazwa", kontrahentNazwa ?? "");
                command.Parameters.AddWithValue("@TypOpakowania", GetNazwaSystemowa(typOpakowania));
                command.Parameters.AddWithValue("@KodOpakowania", typOpakowania);
                command.Parameters.AddWithValue("@IloscPotwierdzona", iloscPotwierdzona);
                command.Parameters.AddWithValue("@SaldoSystemowe", saldoSystemowe);
                command.Parameters.AddWithValue("@StatusPotwierdzenia", status);
                command.Parameters.AddWithValue("@Uwagi", (object)uwagi ?? DBNull.Value);
                command.Parameters.AddWithValue("@UzytkownikId", uzytkownikId);
                command.Parameters.AddWithValue("@UzytkownikNazwa", uzytkownikNazwa ?? "");

                var result = await command.ExecuteScalarAsync().ConfigureAwait(false);

                // Invalidate tylko potwierdzenia (nie salda!)
                InvalidatePotwierdzeniaCache();

                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas dodawania potwierdzenia: {ex.Message}", ex);
            }
        }

        private static string GetNazwaSystemowa(string kod) => kod switch
        {
            "E2" => "Pojemnik Drobiowy E2",
            "H1" => "Paleta H1",
            "EURO" => "Paleta EURO",
            "PCV" => "Paleta plastikowa",
            "DREW" => "Paleta Drewniana",
            _ => kod
        };

        /// <summary>
        /// Pobiera potwierdzenia dla konkretnego kontrahenta
        /// </summary>
        public async Task<List<Potwierdzenie>> PobierzPotwierdzeniaKontrahentaAsync(int kontrahentId)
        {
            var wyniki = new List<Potwierdzenie>(20);

            string query = @"
SELECT Id, KontrahentId, TypOpakowania, KodOpakowania, DataPotwierdzenia,
       IloscPotwierdzona, SaldoSystemowe, StatusPotwierdzenia, Uwagi,
       UzytkownikId, UzytkownikNazwa, DataWprowadzenia
FROM [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan] WITH (NOLOCK)
WHERE KontrahentId = @KontrahentId
ORDER BY DataPotwierdzenia DESC, DataWprowadzenia DESC";

            try
            {
                using var connection = new SqlConnection(_connectionStringLibraNet);
                await connection.OpenAsync().ConfigureAwait(false);

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@KontrahentId", kontrahentId);

                using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    wyniki.Add(new Potwierdzenie
                    {
                        Id = reader.GetInt32(0),
                        KontrahentId = reader.GetInt32(1),
                        TypOpakowania = reader.IsDBNull(3) ? reader.GetString(2) : reader.GetString(3),
                        DataPotwierdzenia = reader.GetDateTime(4),
                        IloscPotwierdzona = reader.GetInt32(5),
                        SaldoSystemowe = reader.GetInt32(6),
                        Status = reader.IsDBNull(7) ? "Oczekujące" : reader.GetString(7),
                        Uwagi = reader.IsDBNull(8) ? null : reader.GetString(8),
                        Uzytkownik = reader.IsDBNull(10) ? reader.GetString(9) : reader.GetString(10),
                        DataWprowadzenia = reader.GetDateTime(11)
                    });
                }
            }
            catch
            {
                // Ignoruj
            }

            return wyniki;
        }

        #endregion

        #region Handlowcy

        /// <summary>
        /// Pobiera filtr handlowca dla użytkownika
        /// </summary>
        public async Task<string> PobierzHandlowcaAsync(string userId)
        {
            if (userId == "11111") return null; // Admin

            if (_cacheHandlowcy.TryGetValue(userId, out var cached))
            {
                PerformanceProfiler.RecordCacheHit("Handlowcy");
                return cached;
            }
            PerformanceProfiler.RecordCacheMiss("Handlowcy");

            string query = @"
SELECT HandlowiecNazwa
FROM [LibraNet].[dbo].[MapowanieHandlowcow] WITH (NOLOCK)
WHERE UserId = @UserId AND CzyAktywny = 1";

            try
            {
                using var connection = new SqlConnection(_connectionStringLibraNet);
                await connection.OpenAsync().ConfigureAwait(false);

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserId", userId);

                var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
                var handlowiec = result?.ToString();

                _cacheHandlowcy[userId] = handlowiec;

                return handlowiec;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Pre-loading

        public void PreloadDataAsync(DateTime dataDo, string handlowiecFilter = null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (!IsCacheSaldaValid(dataDo, handlowiecFilter))
                    {
                        await PobierzWszystkieSaldaAsync(dataDo, handlowiecFilter).ConfigureAwait(false);
                    }
                }
                catch { }
            });
        }

        public void PreloadDokumentyAsync(int kontrahentId, DateTime dataOd, DateTime dataDo)
        {
            var cacheKey = $"{kontrahentId}_{dataOd:yyyyMMdd}_{dataDo:yyyyMMdd}";
            if (_cacheDokumenty.TryGetValue(cacheKey, out var cached) &&
                DateTime.Now - cached.Time <= CacheDokumentyLifetime)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try { await PobierzDokumentyAsync(kontrahentId, dataOd, dataDo).ConfigureAwait(false); }
                catch { }
            });
        }

        #endregion

        #region IDisposable

        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        #endregion
    }
}
