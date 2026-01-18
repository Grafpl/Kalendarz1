using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Opakowania.Models;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// Serwis do komunikacji z bazą danych dla modułu opakowań
    /// ZOPTYMALIZOWANY z cache'owaniem zestawień
    /// </summary>
    public class OpakowaniaDataService
    {
        private readonly string _connectionStringHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True;Pooling=true;Min Pool Size=2;Max Pool Size=10;";
        private readonly string _connectionStringLibraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;Pooling=true;";

        #region Cache dla zestawień

        // Cache zestawień - klucz to kombinacja parametrów
        private static readonly ConcurrentDictionary<string, (List<ZestawienieSalda> Data, DateTime Time)> _cacheZestawienia = new();
        private static readonly TimeSpan CacheZestawieniaLifetime = TimeSpan.FromHours(4); // 4 godziny
        private static readonly SemaphoreSlim _lockZestawienia = new(1, 1);

        /// <summary>
        /// Invaliduje cache zestawień (np. po dodaniu potwierdzenia)
        /// </summary>
        public static void InvalidateZestawieniaCache()
        {
            _cacheZestawienia.Clear();
            Debug.WriteLine("[CACHE] Zestawienia cache invalidated");
        }

        /// <summary>
        /// Zwraca status cache'a zestawień
        /// </summary>
        public static string GetZestawieniaCacheStatus()
        {
            return $"Zestawienia cache: {_cacheZestawienia.Count} wpisów";
        }

        #endregion

        #region Cache dla wszystkich sald (WidokPojemnikiZestawienie)

        // Cache wszystkich sald - klucz to kombinacja parametrów
        private static readonly ConcurrentDictionary<string, (List<SaldoOpakowania> Data, DateTime Time)> _cacheWszystkieSalda = new();
        private static readonly TimeSpan CacheWszystkieSaldaLifetime = TimeSpan.FromHours(4); // 4 godziny

        /// <summary>
        /// Invaliduje cache wszystkich sald
        /// </summary>
        public static void InvalidateWszystkieSaldaCache()
        {
            _cacheWszystkieSalda.Clear();
            Debug.WriteLine("[CACHE] WszystkieSalda cache invalidated");
        }

        /// <summary>
        /// Zwraca status cache'a wszystkich sald
        /// </summary>
        public static string GetWszystkieSaldaCacheStatus()
        {
            return $"WszystkieSalda cache: {_cacheWszystkieSalda.Count} wpisów";
        }

        #endregion

        #region Cache dla szczegółów kontrahenta

        // Cache szczegółów kontrahenta - klucz to kontrahentId_dataOd_dataDo
        private static readonly ConcurrentDictionary<string, (SaldoOpakowania Saldo, List<DokumentOpakowania> Dokumenty, DateTime Time)> _cacheSzczegoly = new();
        private static readonly TimeSpan CacheSzczegolyLifetime = TimeSpan.FromHours(2); // 2 godziny
        private const int MaxCacheSzczegolySize = 50; // Max 50 kontrahentów w cache

        /// <summary>
        /// Invaliduje cache szczegółów kontrahenta
        /// </summary>
        public static void InvalidateSzczegolyCache(int? kontrahentId = null)
        {
            if (kontrahentId.HasValue)
            {
                // Usuń tylko dane tego kontrahenta
                var keysToRemove = _cacheSzczegoly.Keys.Where(k => k.StartsWith($"{kontrahentId}_")).ToList();
                foreach (var key in keysToRemove)
                {
                    _cacheSzczegoly.TryRemove(key, out _);
                }
                Debug.WriteLine($"[CACHE] Szczegoly cache invalidated for kontrahent {kontrahentId}");
            }
            else
            {
                _cacheSzczegoly.Clear();
                Debug.WriteLine("[CACHE] Szczegoly cache invalidated (all)");
            }
        }

        /// <summary>
        /// Zwraca status cache'a szczegółów
        /// </summary>
        public static string GetSzczegolyCacheStatus()
        {
            return $"Szczegoly cache: {_cacheSzczegoly.Count} kontrahentów";
        }

        // Czyszczenie starych wpisów w cache szczegółów
        private static void CleanupSzczegolyCache()
        {
            if (_cacheSzczegoly.Count >= MaxCacheSzczegolySize)
            {
                var oldest = _cacheSzczegoly
                    .OrderBy(x => x.Value.Time)
                    .Take(MaxCacheSzczegolySize / 2)
                    .Select(x => x.Key)
                    .ToList();

                foreach (var key in oldest)
                {
                    _cacheSzczegoly.TryRemove(key, out _);
                }
                Debug.WriteLine($"[CACHE] Cleaned up {oldest.Count} old szczegoly entries");
            }
        }

        #endregion

        #region Kontrahenci

        /// <summary>
        /// Pobiera listę wszystkich kontrahentów z saldem opakowań
        /// </summary>
        public async Task<List<Kontrahent>> PobierzKontrahentowAsync(string handlowiecFilter = null)
        {
            var kontrahenci = new List<Kontrahent>();

            string query = @"
                SELECT DISTINCT
                    C.id AS KontrahentId,
                    C.Shortcut,
                    C.Name AS Nazwa,
                    ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
                FROM [HANDEL].[SSCommon].[STContractors] C
                INNER JOIN [HANDEL].[HM].[MG] MG ON MG.khid = C.id
                INNER JOIN [HANDEL].[HM].[MZ] MZ ON MZ.super = MG.id
                INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
                WHERE MG.anulowany = 0
                  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
                  AND (@HandlowiecFilter IS NULL OR WYM.CDim_Handlowiec_Val = @HandlowiecFilter)
                ORDER BY C.Shortcut";

            try
            {
                using (var connection = new SqlConnection(_connectionStringHandel))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@HandlowiecFilter", (object)handlowiecFilter ?? DBNull.Value);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                kontrahenci.Add(new Kontrahent
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("KontrahentId")),
                                    Shortcut = reader.GetString(reader.GetOrdinal("Shortcut")),
                                    Nazwa = reader.IsDBNull(reader.GetOrdinal("Nazwa")) ? "" : reader.GetString(reader.GetOrdinal("Nazwa")),
                                    Handlowiec = reader.GetString(reader.GetOrdinal("Handlowiec"))
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania kontrahentów: {ex.Message}", ex);
            }

            return kontrahenci;
        }

        /// <summary>
        /// Pobiera nazwę handlowca na podstawie ID użytkownika
        /// </summary>
        public async Task<string> PobierzHandlowcaPoUserIdAsync(string userId)
        {
            if (userId == "11111") return null; // Admin widzi wszystkich

            string query = @"
                SELECT HandlowiecNazwa 
                FROM [LibraNet].[dbo].[MapowanieHandlowcow] 
                WHERE UserId = @UserId AND CzyAktywny = 1";

            try
            {
                using (var connection = new SqlConnection(_connectionStringLibraNet))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        var result = await command.ExecuteScalarAsync();
                        return result?.ToString();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Salda Opakowań

        /// <summary>
        /// Pobiera zestawienie sald dla wszystkich odbiorców danego typu opakowania
        /// ZOPTYMALIZOWANE z cache'owaniem
        /// </summary>
        public async Task<List<ZestawienieSalda>> PobierzZestawienieSaldAsync(DateTime dataOd, DateTime dataDo, string towar, string handlowiecFilter = null)
        {
            // Generuj klucz cache'a
            var cacheKey = $"{dataOd:yyyyMMdd}_{dataDo:yyyyMMdd}_{towar}_{handlowiecFilter ?? "ALL"}";

            // FAST PATH: Sprawdź cache
            if (_cacheZestawienia.TryGetValue(cacheKey, out var cached))
            {
                if (DateTime.Now - cached.Time <= CacheZestawieniaLifetime)
                {
                    Debug.WriteLine($"[CACHE HIT] Zestawienie for {towar} ({cached.Data.Count} records)");
                    PerformanceProfiler.RecordCacheHit("Zestawienie");
                    return cached.Data;
                }
                // Cache wygasł - usuń
                _cacheZestawienia.TryRemove(cacheKey, out _);
            }
            PerformanceProfiler.RecordCacheMiss("Zestawienie");

            // SLOW PATH: Pobierz z bazy
            var sw = Stopwatch.StartNew();
            var zestawienie = new List<ZestawienieSalda>();

            string query = @"
WITH WynikPierwszyZakres AS (
    SELECT 
        'Suma' AS Kontrahent,
        0 AS KontrahentId,
        CAST(ISNULL(SUM(MZ.Ilosc), 0) AS INT) AS SumaIlosci,
        '' AS Handlowiec
    FROM [HANDEL].[HM].[MZ] MZ
    INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id 
    INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id 
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id
    WHERE MZ.data >= '2020-01-01' 
      AND MZ.data <= @DataOd 
      AND MG.anulowany = 0
      AND TW.nazwa = @Towar

    UNION ALL

    SELECT
        C.Shortcut AS Kontrahent,
        C.id AS KontrahentId,
        CAST(ISNULL(SUM(MZ.Ilosc), 0) AS INT) AS SumaIlosci,
        ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
    FROM [HANDEL].[HM].[MZ] MZ
    INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id 
    INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id 
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
    WHERE MZ.data >= '2020-01-01' 
      AND MZ.data <= @DataOd 
      AND MG.anulowany = 0
      AND TW.nazwa = @Towar
    GROUP BY C.Shortcut, C.id, WYM.CDim_Handlowiec_Val
),

WynikDrugiZakres AS (
    SELECT 
        'Suma' AS Kontrahent,
        0 AS KontrahentId,
        CAST(ISNULL(SUM(MZ.Ilosc), 0) AS INT) AS SumaIlosci,
        '' AS Handlowiec
    FROM [HANDEL].[HM].[MZ] MZ
    INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id 
    INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id 
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id
    WHERE MZ.data >= '2020-01-01' 
      AND MZ.data <= @DataDo 
      AND MG.anulowany = 0
      AND TW.nazwa = @Towar

    UNION ALL

    SELECT
        C.Shortcut AS Kontrahent,
        C.id AS KontrahentId,
        CAST(ISNULL(SUM(MZ.Ilosc), 0) AS INT) AS SumaIlosci,
        ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
    FROM [HANDEL].[HM].[MZ] MZ
    INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id 
    INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id 
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
    WHERE MZ.data >= '2020-01-01' 
      AND MZ.data <= @DataDo 
      AND MG.anulowany = 0
      AND TW.nazwa = @Towar
    GROUP BY C.Shortcut, C.id, WYM.CDim_Handlowiec_Val
)

SELECT 
    COALESCE(P.Kontrahent, D.Kontrahent) AS [Kontrahent],
    COALESCE(P.KontrahentId, D.KontrahentId) AS [KontrahentId],
    CAST(ISNULL(P.SumaIlosci, 0) AS INT) AS [IloscPierwszyZakres],
    CAST(ISNULL(D.SumaIlosci, 0) AS INT) AS [IloscDrugiZakres],
    CAST(ISNULL(D.SumaIlosci, 0) - ISNULL(P.SumaIlosci, 0) AS INT) AS [Roznica],
    COALESCE(P.Handlowiec, D.Handlowiec) AS [Handlowiec],
    OD.DataOstatniegoDokumentu,
    OD.TowarZDokumentu
FROM WynikPierwszyZakres P
FULL OUTER JOIN WynikDrugiZakres D ON P.Kontrahent = D.Kontrahent

OUTER APPLY (
    SELECT TOP 1 
        MG.Data AS DataOstatniegoDokumentu,
        MG.Nazwa AS TowarZDokumentu
    FROM [HANDEL].[HM].[MG] MG
    INNER JOIN [HANDEL].[HM].[MZ] MZ ON MZ.super = MG.id
    INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id
    WHERE MG.anulowany = 0 
      AND MG.seria IN ('sMW', 'sMP')
      AND C.Shortcut = COALESCE(P.Kontrahent, D.Kontrahent)
      AND MG.Data <= @DataDo
    ORDER BY MG.Data DESC
) OD

WHERE 
    (ISNULL(P.SumaIlosci, 0) != 0 OR ISNULL(D.SumaIlosci, 0) != 0)
    AND (@HandlowiecFilter IS NULL OR COALESCE(P.Handlowiec, D.Handlowiec) = @HandlowiecFilter)
ORDER BY [IloscDrugiZakres] DESC, [Kontrahent]
OPTION (RECOMPILE)";

            try
            {
                // OPTYMALIZACJA: Pobierz wszystkie potwierdzenia JEDNYM zapytaniem (zamiast N+1)
                var potwierdzenia = await PobierzWszystkiePotwierdzenia(towar);

                using (var connection = new SqlConnection(_connectionStringHandel))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.CommandTimeout = 60; // 60 sekund timeout dla złożonego zapytania
                        command.Parameters.AddWithValue("@DataOd", dataOd);
                        command.Parameters.AddWithValue("@DataDo", dataDo);
                        command.Parameters.AddWithValue("@Towar", towar);
                        command.Parameters.AddWithValue("@HandlowiecFilter", (object)handlowiecFilter ?? DBNull.Value);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var item = new ZestawienieSalda
                                {
                                    Kontrahent = reader.GetString(reader.GetOrdinal("Kontrahent")),
                                    KontrahentId = Convert.ToInt32(reader["KontrahentId"]),
                                    IloscPierwszyZakres = Convert.ToInt32(reader["IloscPierwszyZakres"]),
                                    IloscDrugiZakres = Convert.ToInt32(reader["IloscDrugiZakres"]),
                                    Roznica = Convert.ToInt32(reader["Roznica"]),
                                    Handlowiec = reader.GetString(reader.GetOrdinal("Handlowiec")),
                                    DataOstatniegoDokumentu = reader.IsDBNull(reader.GetOrdinal("DataOstatniegoDokumentu"))
                                        ? null : reader.GetDateTime(reader.GetOrdinal("DataOstatniegoDokumentu")),
                                    TowarZDokumentu = reader.IsDBNull(reader.GetOrdinal("TowarZDokumentu"))
                                        ? null : reader.GetString(reader.GetOrdinal("TowarZDokumentu"))
                                };

                                // Sprawdź potwierdzenie z słownika (zamiast N+1 zapytań do bazy!)
                                if (item.KontrahentId > 0 && potwierdzenia.TryGetValue(item.KontrahentId, out var potw))
                                {
                                    item.JestPotwierdzone = potw.JestPotwierdzone;
                                    item.DataPotwierdzenia = potw.DataPotwierdzenia;
                                }

                                zestawienie.Add(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania zestawienia sald: {ex.Message}", ex);
            }

            // Zapisz w cache
            sw.Stop();
            _cacheZestawienia[cacheKey] = (zestawienie, DateTime.Now);
            Debug.WriteLine($"[CACHE] Zestawienie saved for {towar} ({zestawienie.Count} records in {sw.ElapsedMilliseconds}ms)");

            return zestawienie;
        }

        /// <summary>
        /// Pobiera szczegółowe saldo dla konkretnego kontrahenta
        /// ZOPTYMALIZOWANE z cache'owaniem - drugie wejście w szczegóły tego samego kontrahenta jest natychmiastowe
        /// </summary>
        public async Task<List<DokumentOpakowania>> PobierzSaldoKontrahentaAsync(int kontrahentId, DateTime dataOd, DateTime dataDo)
        {
            // Generuj klucz cache
            var cacheKey = $"{kontrahentId}_{dataOd:yyyyMMdd}_{dataDo:yyyyMMdd}";

            // FAST PATH: Sprawdź cache
            if (_cacheSzczegoly.TryGetValue(cacheKey, out var cached))
            {
                if (DateTime.Now - cached.Time <= CacheSzczegolyLifetime && cached.Dokumenty != null)
                {
                    Debug.WriteLine($"[CACHE HIT] Szczegoly for kontrahent {kontrahentId} ({cached.Dokumenty.Count} docs)");
                    PerformanceProfiler.RecordCacheHit("Szczegoly");
                    return cached.Dokumenty;
                }
                // Cache wygasł
                _cacheSzczegoly.TryRemove(cacheKey, out _);
            }
            PerformanceProfiler.RecordCacheMiss("Szczegoly");

            // SLOW PATH: Pobierz z bazy
            var sw = Stopwatch.StartNew();
            var dokumenty = new List<DokumentOpakowania>();

            string saldoPoczatkoweQuery = @"
SELECT 
    'Saldo początkowe' AS Dokumenty,
    CAST(ISNULL(SUM(CASE WHEN z.kod = 'Pojemnik drobiowy E2' THEN z.ilosc ELSE 0 END), 0) AS INT) AS E2,
    CAST(ISNULL(SUM(CASE WHEN z.kod = 'PALETA H1' THEN z.ilosc ELSE 0 END), 0) AS INT) AS H1,
    CAST(ISNULL(SUM(CASE WHEN z.kod = 'PALETA EURO' THEN z.ilosc ELSE 0 END), 0) AS INT) AS EURO,
    CAST(ISNULL(SUM(CASE WHEN z.kod = 'PALETA PLASTIKOWA' THEN z.ilosc ELSE 0 END), 0) AS INT) AS PCV,
    CAST(ISNULL(SUM(CASE WHEN z.kod = 'Paleta Drewniana' THEN z.ilosc ELSE 0 END), 0) AS INT) AS Drew
FROM hm.MG AS d
JOIN hm.MZ AS z ON d.id = z.super
WHERE d.khid = @kontrahentId
  AND d.magazyn = 65559
  AND d.typ_dk IN ('MW1', 'MP')
  AND d.anulowany = 0
  AND d.data <= @dataOd";

            string daneQuery = @"
SELECT 
    d.kod AS NrDok,
    d.data AS Data,
    DATENAME(WEEKDAY, d.data) AS DzienTyg,
    d.opis AS Dokumenty,
    CAST(ISNULL(SUM(CASE WHEN z.kod = 'Pojemnik drobiowy E2' THEN z.ilosc ELSE 0 END), 0) AS INT) AS E2,
    CAST(ISNULL(SUM(CASE WHEN z.kod = 'PALETA H1' THEN z.ilosc ELSE 0 END), 0) AS INT) AS H1,
    CAST(ISNULL(SUM(CASE WHEN z.kod = 'PALETA EURO' THEN z.ilosc ELSE 0 END), 0) AS INT) AS EURO,
    CAST(ISNULL(SUM(CASE WHEN z.kod = 'PALETA PLASTIKOWA' THEN z.ilosc ELSE 0 END), 0) AS INT) AS PCV,
    CAST(ISNULL(SUM(CASE WHEN z.kod = 'Paleta Drewniana' THEN z.ilosc ELSE 0 END), 0) AS INT) AS Drew
FROM hm.MG AS d
JOIN hm.MZ AS z ON d.id = z.super
WHERE d.khid = @kontrahentId
  AND d.magazyn = 65559
  AND d.typ_dk IN ('MW1', 'MP')
  AND d.anulowany = 0
  AND d.data BETWEEN DATEADD(DAY, 1, @dataOd) AND @dataDo
GROUP BY d.id, d.kod, d.data, d.opis
ORDER BY d.data DESC";

            try
            {
                using (var connection = new SqlConnection(_connectionStringHandel))
                {
                    await connection.OpenAsync();

                    // Pobierz saldo początkowe
                    using (var saldoCommand = new SqlCommand(saldoPoczatkoweQuery, connection))
                    {
                        saldoCommand.Parameters.AddWithValue("@kontrahentId", kontrahentId);
                        saldoCommand.Parameters.AddWithValue("@dataOd", dataOd);

                        using (var reader = await saldoCommand.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                dokumenty.Add(new DokumentOpakowania
                                {
                                    Dokumenty = $"Saldo na {dataOd:dd.MM.yyyy}",
                                    Data = dataOd,
                                    E2 = reader.IsDBNull(reader.GetOrdinal("E2")) ? 0 : reader.GetInt32(reader.GetOrdinal("E2")),
                                    H1 = reader.IsDBNull(reader.GetOrdinal("H1")) ? 0 : reader.GetInt32(reader.GetOrdinal("H1")),
                                    EURO = reader.IsDBNull(reader.GetOrdinal("EURO")) ? 0 : reader.GetInt32(reader.GetOrdinal("EURO")),
                                    PCV = reader.IsDBNull(reader.GetOrdinal("PCV")) ? 0 : reader.GetInt32(reader.GetOrdinal("PCV")),
                                    DREW = reader.IsDBNull(reader.GetOrdinal("Drew")) ? 0 : reader.GetInt32(reader.GetOrdinal("Drew")),
                                    JestSaldem = true
                                });
                            }
                        }
                    }

                    // Pobierz dokumenty
                    using (var daneCommand = new SqlCommand(daneQuery, connection))
                    {
                        daneCommand.Parameters.AddWithValue("@kontrahentId", kontrahentId);
                        daneCommand.Parameters.AddWithValue("@dataOd", dataOd);
                        daneCommand.Parameters.AddWithValue("@dataDo", dataDo);

                        using (var reader = await daneCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                dokumenty.Add(new DokumentOpakowania
                                {
                                    NrDok = reader.IsDBNull(reader.GetOrdinal("NrDok")) ? "" : reader.GetString(reader.GetOrdinal("NrDok")),
                                    Data = reader.GetDateTime(reader.GetOrdinal("Data")),
                                    DzienTyg = reader.IsDBNull(reader.GetOrdinal("DzienTyg")) ? "" : reader.GetString(reader.GetOrdinal("DzienTyg")),
                                    Dokumenty = reader.IsDBNull(reader.GetOrdinal("Dokumenty")) ? "" : reader.GetString(reader.GetOrdinal("Dokumenty")),
                                    E2 = reader.IsDBNull(reader.GetOrdinal("E2")) ? 0 : reader.GetInt32(reader.GetOrdinal("E2")),
                                    H1 = reader.IsDBNull(reader.GetOrdinal("H1")) ? 0 : reader.GetInt32(reader.GetOrdinal("H1")),
                                    EURO = reader.IsDBNull(reader.GetOrdinal("EURO")) ? 0 : reader.GetInt32(reader.GetOrdinal("EURO")),
                                    PCV = reader.IsDBNull(reader.GetOrdinal("PCV")) ? 0 : reader.GetInt32(reader.GetOrdinal("PCV")),
                                    DREW = reader.IsDBNull(reader.GetOrdinal("Drew")) ? 0 : reader.GetInt32(reader.GetOrdinal("Drew")),
                                    JestSaldem = false
                                });
                            }
                        }
                    }

                    // Oblicz saldo końcowe
                    if (dokumenty.Count > 0)
                    {
                        var saldoKoncowe = new DokumentOpakowania
                        {
                            Dokumenty = $"Saldo na {dataDo:dd.MM.yyyy}",
                            Data = dataDo,
                            JestSaldem = true
                        };

                        foreach (var dok in dokumenty)
                        {
                            saldoKoncowe.E2 += dok.E2;
                            saldoKoncowe.H1 += dok.H1;
                            saldoKoncowe.EURO += dok.EURO;
                            saldoKoncowe.PCV += dok.PCV;
                            saldoKoncowe.DREW += dok.DREW;
                        }

                        dokumenty.Insert(0, saldoKoncowe);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania salda kontrahenta: {ex.Message}", ex);
            }

            // Zapisz w cache
            sw.Stop();
            CleanupSzczegolyCache(); // Wyczyść stare wpisy jeśli za dużo

            // Zapisz dokumenty w cache (saldo będzie zapisane przez PobierzSaldaWszystkichOpakowannAsync)
            if (_cacheSzczegoly.TryGetValue(cacheKey, out var existing))
            {
                _cacheSzczegoly[cacheKey] = (existing.Saldo, dokumenty, DateTime.Now);
            }
            else
            {
                _cacheSzczegoly[cacheKey] = (null, dokumenty, DateTime.Now);
            }
            Debug.WriteLine($"[CACHE] Szczegoly saved for kontrahent {kontrahentId} ({dokumenty.Count} docs in {sw.ElapsedMilliseconds}ms)");

            return dokumenty;
        }

        /// <summary>
        /// Pobiera salda wszystkich opakowań dla konkretnego kontrahenta
        /// ZOPTYMALIZOWANE z cache'owaniem - drugie wejście w szczegóły jest natychmiastowe
        /// </summary>
        public async Task<SaldoOpakowania> PobierzSaldaWszystkichOpakowannAsync(int kontrahentId, DateTime dataDo)
        {
            // Generuj klucz cache - używamy tego samego klucza co dokumenty (ale z dowolną dataOd)
            var cacheKey = $"{kontrahentId}_saldo_{dataDo:yyyyMMdd}";

            // FAST PATH: Sprawdź cache dla salda
            if (_cacheSzczegoly.TryGetValue(cacheKey, out var cached))
            {
                if (DateTime.Now - cached.Time <= CacheSzczegolyLifetime && cached.Saldo != null)
                {
                    Debug.WriteLine($"[CACHE HIT] Saldo for kontrahent {kontrahentId}");
                    PerformanceProfiler.RecordCacheHit("SaldoOdbiorcy");
                    return cached.Saldo;
                }
            }
            PerformanceProfiler.RecordCacheMiss("SaldoOdbiorcy");

            // SLOW PATH: Pobierz z bazy
            var sw = Stopwatch.StartNew();
            SaldoOpakowania saldo = null;

            string query = @"
SELECT
    C.Shortcut AS Kontrahent,
    C.id AS KontrahentId,
    ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoE2,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoH1,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoEURO,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoPCV,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoDREW
FROM [HANDEL].[SSCommon].[STContractors] C
INNER JOIN [HANDEL].[HM].[MG] MG ON MG.khid = C.id
INNER JOIN [HANDEL].[HM].[MZ] MZ ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE C.id = @KontrahentId
  AND MG.anulowany = 0
  AND MG.data <= @DataDo
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
GROUP BY C.id, C.Shortcut, WYM.CDim_Handlowiec_Val
OPTION (RECOMPILE)";

            try
            {
                using (var connection = new SqlConnection(_connectionStringHandel))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@KontrahentId", kontrahentId);
                        command.Parameters.AddWithValue("@DataDo", dataDo);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                saldo = new SaldoOpakowania
                                {
                                    Kontrahent = reader.GetString(reader.GetOrdinal("Kontrahent")),
                                    KontrahentId = reader.GetInt32(reader.GetOrdinal("KontrahentId")),
                                    Handlowiec = reader.GetString(reader.GetOrdinal("Handlowiec")),
                                    SaldoE2 = reader.GetInt32(reader.GetOrdinal("SaldoE2")),
                                    SaldoH1 = reader.GetInt32(reader.GetOrdinal("SaldoH1")),
                                    SaldoEURO = reader.GetInt32(reader.GetOrdinal("SaldoEURO")),
                                    SaldoPCV = reader.GetInt32(reader.GetOrdinal("SaldoPCV")),
                                    SaldoDREW = reader.GetInt32(reader.GetOrdinal("SaldoDREW"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania salda: {ex.Message}", ex);
            }

            // Zapisz w cache
            sw.Stop();
            if (saldo != null)
            {
                CleanupSzczegolyCache();
                _cacheSzczegoly[cacheKey] = (saldo, null, DateTime.Now);
                Debug.WriteLine($"[CACHE] Saldo saved for kontrahent {kontrahentId} in {sw.ElapsedMilliseconds}ms");
            }

            return saldo;
        }

        /// <summary>
        /// Pobiera salda wszystkich opakowań dla WSZYSTKICH kontrahentów jednym zapytaniem (SZYBKA WERSJA)
        /// ZOPTYMALIZOWANE z cache'owaniem
        /// </summary>
        public async Task<List<SaldoOpakowania>> PobierzWszystkieSaldaAsync(DateTime dataDo, string handlowiecFilter = null)
        {
            // Generuj klucz cache'a
            var cacheKey = $"wszystkie_{dataDo:yyyyMMdd}_{handlowiecFilter ?? "ALL"}";

            // FAST PATH: Sprawdź cache
            if (_cacheWszystkieSalda.TryGetValue(cacheKey, out var cached))
            {
                if (DateTime.Now - cached.Time <= CacheWszystkieSaldaLifetime)
                {
                    Debug.WriteLine($"[CACHE HIT] WszystkieSalda ({cached.Data.Count} records)");
                    PerformanceProfiler.RecordCacheHit("WszystkieSalda");
                    return cached.Data;
                }
                // Cache wygasł - usuń
                _cacheWszystkieSalda.TryRemove(cacheKey, out _);
            }
            PerformanceProfiler.RecordCacheMiss("WszystkieSalda");

            // SLOW PATH: Pobierz z bazy
            var sw = Stopwatch.StartNew();
            var wyniki = new List<SaldoOpakowania>();

            string query = @"
SELECT 
    C.Shortcut AS Kontrahent,
    C.id AS KontrahentId,
    ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoE2,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoH1,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoEURO,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoPCV,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoDREW
FROM [HANDEL].[SSCommon].[STContractors] C
INNER JOIN [HANDEL].[HM].[MG] MG ON MG.khid = C.id
INNER JOIN [HANDEL].[HM].[MZ] MZ ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE MG.anulowany = 0
  AND MG.magazyn = 65559
  AND MG.typ_dk IN ('MW1', 'MP')
  AND MG.data <= @DataDo
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
  AND (@HandlowiecFilter IS NULL OR WYM.CDim_Handlowiec_Val = @HandlowiecFilter)
GROUP BY C.id, C.Shortcut, WYM.CDim_Handlowiec_Val
HAVING SUM(CASE WHEN TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana') 
           THEN ABS(MZ.Ilosc) ELSE 0 END) > 0
ORDER BY C.Shortcut";

            try
            {
                using (var connection = new SqlConnection(_connectionStringHandel))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.CommandTimeout = 60; // 60 sekund timeout
                        command.Parameters.AddWithValue("@DataDo", dataDo);
                        command.Parameters.AddWithValue("@HandlowiecFilter", (object)handlowiecFilter ?? DBNull.Value);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                wyniki.Add(new SaldoOpakowania
                                {
                                    Kontrahent = reader.GetString(reader.GetOrdinal("Kontrahent")),
                                    KontrahentId = reader.GetInt32(reader.GetOrdinal("KontrahentId")),
                                    Handlowiec = reader.IsDBNull(reader.GetOrdinal("Handlowiec")) ? "-" : reader.GetString(reader.GetOrdinal("Handlowiec")),
                                    SaldoE2 = reader.GetInt32(reader.GetOrdinal("SaldoE2")),
                                    SaldoH1 = reader.GetInt32(reader.GetOrdinal("SaldoH1")),
                                    SaldoEURO = reader.GetInt32(reader.GetOrdinal("SaldoEURO")),
                                    SaldoPCV = reader.GetInt32(reader.GetOrdinal("SaldoPCV")),
                                    SaldoDREW = reader.GetInt32(reader.GetOrdinal("SaldoDREW"))
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania sald: {ex.Message}", ex);
            }

            // Zapisz w cache
            sw.Stop();
            _cacheWszystkieSalda[cacheKey] = (wyniki, DateTime.Now);
            Debug.WriteLine($"[CACHE] WszystkieSalda saved ({wyniki.Count} records in {sw.ElapsedMilliseconds}ms)");

            return wyniki;
        }

        #endregion

        #region Historia dla wykresów

        /// <summary>
        /// Pobiera historię salda dla wykresu - ZOPTYMALIZOWANA WERSJA (1 zapytanie zamiast N)
        /// </summary>
        public async Task<List<HistoriaSaldaPunkt>> PobierzHistorieSaldaAsync(int kontrahentId, string kodOpakowania, DateTime dataOd, DateTime dataDo)
        {
            var historia = new List<HistoriaSaldaPunkt>();

            string kolumna = kodOpakowania switch
            {
                "E2" => "Pojemnik Drobiowy E2",
                "H1" => "Paleta H1",
                "EURO" => "Paleta EURO",
                "PCV" => "Paleta plastikowa",
                "DREW" => "Paleta Drewniana",
                _ => "Pojemnik Drobiowy E2"
            };

            // OPTYMALIZACJA: Pobierz saldo początkowe i wszystkie zmiany JEDNYM zapytaniem
            string saldoPoczatkoweQuery = @"
SELECT CAST(ISNULL(SUM(MZ.Ilosc), 0) AS INT) AS Saldo
FROM [HANDEL].[HM].[MG] MG
INNER JOIN [HANDEL].[HM].[MZ] MZ ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
WHERE MG.khid = @KontrahentId
  AND MG.anulowany = 0
  AND MG.data < @DataOd
  AND TW.nazwa = @Towar";

            string zmianyQuery = @"
SELECT
    CAST(MG.data AS DATE) AS Data,
    CAST(SUM(MZ.Ilosc) AS INT) AS Zmiana
FROM [HANDEL].[HM].[MG] MG
INNER JOIN [HANDEL].[HM].[MZ] MZ ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
WHERE MG.khid = @KontrahentId
  AND MG.anulowany = 0
  AND MG.data >= @DataOd
  AND MG.data <= @DataDo
  AND TW.nazwa = @Towar
GROUP BY CAST(MG.data AS DATE)
ORDER BY Data";

            try
            {
                using (var connection = new SqlConnection(_connectionStringHandel))
                {
                    await connection.OpenAsync();

                    // 1. Pobierz saldo początkowe (przed zakresem)
                    int saldoPoczatkowe = 0;
                    using (var cmd = new SqlCommand(saldoPoczatkoweQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@KontrahentId", kontrahentId);
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);
                        cmd.Parameters.AddWithValue("@Towar", kolumna);
                        var result = await cmd.ExecuteScalarAsync();
                        saldoPoczatkowe = result == DBNull.Value ? 0 : Convert.ToInt32(result);
                    }

                    // 2. Pobierz wszystkie zmiany w zakresie (jedno zapytanie!)
                    var zmianyPoDniach = new Dictionary<DateTime, int>();
                    using (var cmd = new SqlCommand(zmianyQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@KontrahentId", kontrahentId);
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);
                        cmd.Parameters.AddWithValue("@DataDo", dataDo);
                        cmd.Parameters.AddWithValue("@Towar", kolumna);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var data = reader.GetDateTime(0);
                                var zmiana = reader.GetInt32(1);
                                zmianyPoDniach[data.Date] = zmiana;
                            }
                        }
                    }

                    // 3. Oblicz saldo kumulatywne dla każdego dnia (w pamięci, bez zapytań)
                    int biezaceSaldo = saldoPoczatkowe;
                    var currentDate = dataOd;
                    while (currentDate <= dataDo)
                    {
                        if (zmianyPoDniach.TryGetValue(currentDate.Date, out int zmiana))
                        {
                            biezaceSaldo += zmiana;
                        }

                        historia.Add(new HistoriaSaldaPunkt
                        {
                            Data = currentDate,
                            Saldo = biezaceSaldo
                        });

                        currentDate = currentDate.AddDays(1);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania historii salda: {ex.Message}", ex);
            }

            return historia;
        }

        #endregion

        #region Potwierdzenia

        /// <summary>
        /// Pobiera WSZYSTKIE potwierdzenia dla danego typu opakowania jednym zapytaniem (rozwiązanie problemu N+1)
        /// </summary>
        private async Task<Dictionary<int, (bool JestPotwierdzone, DateTime? DataPotwierdzenia)>> PobierzWszystkiePotwierdzenia(string typOpakowania)
        {
            var wyniki = new Dictionary<int, (bool, DateTime?)>();

            string query = @"
SELECT KontrahentId, MAX(DataPotwierdzenia) AS DataPotwierdzenia
FROM [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan]
WHERE TypOpakowania = @TypOpakowania
  AND StatusPotwierdzenia = 'Potwierdzone'
  AND DataPotwierdzenia >= DATEADD(DAY, -30, GETDATE())
GROUP BY KontrahentId";

            try
            {
                using (var connection = new SqlConnection(_connectionStringLibraNet))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@TypOpakowania", typOpakowania);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var kontrahentId = reader.GetInt32(0);
                                var dataPotwierdzenia = reader.GetDateTime(1);
                                wyniki[kontrahentId] = (true, dataPotwierdzenia);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignoruj błędy - tabela może nie istnieć
            }

            return wyniki;
        }

        /// <summary>
        /// Sprawdza czy kontrahent ma potwierdzenie dla danego typu opakowania
        /// </summary>
        private async Task<(bool, DateTime?)> SprawdzPotwierdzenie(int kontrahentId, string typOpakowania)
        {
            string query = @"
SELECT TOP 1 DataPotwierdzenia 
FROM [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan]
WHERE KontrahentId = @KontrahentId 
  AND TypOpakowania = @TypOpakowania 
  AND StatusPotwierdzenia = 'Potwierdzone'
  AND DataPotwierdzenia >= DATEADD(DAY, -30, GETDATE())
ORDER BY DataPotwierdzenia DESC";

            try
            {
                using (var connection = new SqlConnection(_connectionStringLibraNet))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@KontrahentId", kontrahentId);
                        command.Parameters.AddWithValue("@TypOpakowania", typOpakowania);

                        var result = await command.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            return (true, (DateTime)result);
                        }
                    }
                }
            }
            catch
            {
                // Ignoruj błędy - tabela może nie istnieć
            }

            return (false, null);
        }

        /// <summary>
        /// Pobiera historię potwierdzeń dla kontrahenta
        /// </summary>
        public async Task<List<PotwierdzenieSalda>> PobierzPotwierdzeniaDlaKontrahentaAsync(int kontrahentId)
        {
            var potwierdzenia = new List<PotwierdzenieSalda>();

            string query = @"
SELECT * FROM [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan]
WHERE KontrahentId = @KontrahentId
ORDER BY DataPotwierdzenia DESC, DataWprowadzenia DESC";

            try
            {
                using (var connection = new SqlConnection(_connectionStringLibraNet))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@KontrahentId", kontrahentId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                potwierdzenia.Add(MapujPotwierdzenie(reader));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania potwierdzeń: {ex.Message}", ex);
            }

            return potwierdzenia;
        }

        /// <summary>
        /// Dodaje nowe potwierdzenie salda
        /// </summary>
        public async Task<int> DodajPotwierdzenie(PotwierdzenieSalda potwierdzenie)
        {
            string query = @"
INSERT INTO [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan]
(KontrahentId, KontrahentNazwa, KontrahentShortcut, TypOpakowania, KodOpakowania, 
 DataPotwierdzenia, IloscPotwierdzona, SaldoSystemowe, StatusPotwierdzenia, 
 NumerDokumentu, SciezkaZalacznika, Uwagi, UzytkownikId, UzytkownikNazwa)
VALUES
(@KontrahentId, @KontrahentNazwa, @KontrahentShortcut, @TypOpakowania, @KodOpakowania,
 @DataPotwierdzenia, @IloscPotwierdzona, @SaldoSystemowe, @StatusPotwierdzenia,
 @NumerDokumentu, @SciezkaZalacznika, @Uwagi, @UzytkownikId, @UzytkownikNazwa);
SELECT SCOPE_IDENTITY();";

            try
            {
                using (var connection = new SqlConnection(_connectionStringLibraNet))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@KontrahentId", potwierdzenie.KontrahentId);
                        command.Parameters.AddWithValue("@KontrahentNazwa", potwierdzenie.KontrahentNazwa ?? "");
                        command.Parameters.AddWithValue("@KontrahentShortcut", potwierdzenie.KontrahentShortcut ?? "");
                        command.Parameters.AddWithValue("@TypOpakowania", potwierdzenie.TypOpakowania);
                        command.Parameters.AddWithValue("@KodOpakowania", potwierdzenie.KodOpakowania);
                        command.Parameters.AddWithValue("@DataPotwierdzenia", potwierdzenie.DataPotwierdzenia);
                        command.Parameters.AddWithValue("@IloscPotwierdzona", potwierdzenie.IloscPotwierdzona);
                        command.Parameters.AddWithValue("@SaldoSystemowe", potwierdzenie.SaldoSystemowe);
                        command.Parameters.AddWithValue("@StatusPotwierdzenia", potwierdzenie.StatusPotwierdzenia);
                        command.Parameters.AddWithValue("@NumerDokumentu", (object)potwierdzenie.NumerDokumentu ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SciezkaZalacznika", (object)potwierdzenie.SciezkaZalacznika ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Uwagi", (object)potwierdzenie.Uwagi ?? DBNull.Value);
                        command.Parameters.AddWithValue("@UzytkownikId", potwierdzenie.UzytkownikId);
                        command.Parameters.AddWithValue("@UzytkownikNazwa", potwierdzenie.UzytkownikNazwa ?? "");

                        var result = await command.ExecuteScalarAsync();

                        // Invaliduj wszystkie cache'e po dodaniu potwierdzenia
                        InvalidateZestawieniaCache();
                        InvalidateWszystkieSaldaCache();
                        InvalidateSzczegolyCache(potwierdzenie.KontrahentId);
                        SaldaService.InvalidatePotwierdzeniaCache();

                        return Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas dodawania potwierdzenia: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Aktualizuje istniejące potwierdzenie
        /// </summary>
        public async Task AktualizujPotwierdzenie(PotwierdzenieSalda potwierdzenie)
        {
            string query = @"
UPDATE [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan]
SET StatusPotwierdzenia = @StatusPotwierdzenia,
    Uwagi = @Uwagi,
    DataModyfikacji = GETDATE(),
    ZmodyfikowalId = @ZmodyfikowalId
WHERE Id = @Id";

            try
            {
                using (var connection = new SqlConnection(_connectionStringLibraNet))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", potwierdzenie.Id);
                        command.Parameters.AddWithValue("@StatusPotwierdzenia", potwierdzenie.StatusPotwierdzenia);
                        command.Parameters.AddWithValue("@Uwagi", (object)potwierdzenie.Uwagi ?? DBNull.Value);
                        command.Parameters.AddWithValue("@ZmodyfikowalId", potwierdzenie.UzytkownikId);

                        await command.ExecuteNonQueryAsync();

                        // Invaliduj wszystkie cache'e po aktualizacji potwierdzenia
                        InvalidateZestawieniaCache();
                        InvalidateWszystkieSaldaCache();
                        SaldaService.InvalidatePotwierdzeniaCache();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas aktualizacji potwierdzenia: {ex.Message}", ex);
            }
        }

        private PotwierdzenieSalda MapujPotwierdzenie(SqlDataReader reader)
        {
            return new PotwierdzenieSalda
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                KontrahentId = reader.GetInt32(reader.GetOrdinal("KontrahentId")),
                KontrahentNazwa = reader.GetString(reader.GetOrdinal("KontrahentNazwa")),
                KontrahentShortcut = reader.IsDBNull(reader.GetOrdinal("KontrahentShortcut")) ? "" : reader.GetString(reader.GetOrdinal("KontrahentShortcut")),
                TypOpakowania = reader.GetString(reader.GetOrdinal("TypOpakowania")),
                KodOpakowania = reader.GetString(reader.GetOrdinal("KodOpakowania")),
                DataPotwierdzenia = reader.GetDateTime(reader.GetOrdinal("DataPotwierdzenia")),
                IloscPotwierdzona = reader.GetInt32(reader.GetOrdinal("IloscPotwierdzona")),
                SaldoSystemowe = reader.GetInt32(reader.GetOrdinal("SaldoSystemowe")),
                Roznica = reader.GetInt32(reader.GetOrdinal("Roznica")),
                StatusPotwierdzenia = reader.GetString(reader.GetOrdinal("StatusPotwierdzenia")),
                NumerDokumentu = reader.IsDBNull(reader.GetOrdinal("NumerDokumentu")) ? null : reader.GetString(reader.GetOrdinal("NumerDokumentu")),
                SciezkaZalacznika = reader.IsDBNull(reader.GetOrdinal("SciezkaZalacznika")) ? null : reader.GetString(reader.GetOrdinal("SciezkaZalacznika")),
                Uwagi = reader.IsDBNull(reader.GetOrdinal("Uwagi")) ? null : reader.GetString(reader.GetOrdinal("Uwagi")),
                UzytkownikId = reader.GetString(reader.GetOrdinal("UzytkownikId")),
                UzytkownikNazwa = reader.IsDBNull(reader.GetOrdinal("UzytkownikNazwa")) ? null : reader.GetString(reader.GetOrdinal("UzytkownikNazwa")),
                DataWprowadzenia = reader.GetDateTime(reader.GetOrdinal("DataWprowadzenia")),
                DataModyfikacji = reader.IsDBNull(reader.GetOrdinal("DataModyfikacji")) ? null : reader.GetDateTime(reader.GetOrdinal("DataModyfikacji"))
            };
        }

        #endregion
    }
}
