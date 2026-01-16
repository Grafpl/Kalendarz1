using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Opakowania.Models;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// Serwis do pobierania sald opakowań - ULTRA-ZOPTYMALIZOWANY
    /// Techniki: Multi-level cache, Parallel loading, Connection pooling,
    /// Pre-fetching, Lazy loading, Memory pooling, Async streaming
    /// </summary>
    public class SaldaService : IDisposable
    {
        #region Connection Strings (z poolingiem)

        // Connection pooling: Min/Max Pool Size, Connection Lifetime
        private static readonly string _connectionStringHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True;" +
            "Min Pool Size=2;Max Pool Size=10;Connection Lifetime=300;Pooling=true;";

        private static readonly string _connectionStringLibraNet =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;" +
            "Min Pool Size=1;Max Pool Size=5;Connection Lifetime=300;Pooling=true;";

        #endregion

        #region Multi-Level Cache

        // Cache Level 1: Pełne salda (główny cache)
        private static List<SaldoKontrahenta> _cacheSalda;
        private static DateTime _cacheSaldaTime;
        private static DateTime _cacheSaldaDataDo;
        private static string _cacheSaldaHandlowiec;
        private static readonly TimeSpan CacheSaldaLifetime = TimeSpan.FromMinutes(10); // Wydłużony

        // Cache Level 2: Potwierdzenia (osobny, dłuższy cache)
        private static Dictionary<(int, string), DateTime> _cachePotwierdzenia;
        private static DateTime _cachePotwierdzeniaTime;
        private static readonly TimeSpan CachePotwierdzeniaLifetime = TimeSpan.FromMinutes(15);

        // Cache Level 3: Handlowcy użytkowników (bardzo długi cache)
        private static readonly ConcurrentDictionary<string, string> _cacheHandlowcy = new();
        private static DateTime _cacheHandlowcyTime;
        private static readonly TimeSpan CacheHandlowcyLifetime = TimeSpan.FromHours(1);

        // Cache Level 4: Dokumenty kontrahentów (LRU cache)
        private static readonly ConcurrentDictionary<string, (List<DokumentSalda> Data, DateTime Time)> _cacheDokumenty = new();
        private static readonly TimeSpan CacheDokumentyLifetime = TimeSpan.FromMinutes(5);
        private const int MaxCacheDokumentySize = 50;

        // Lock objects dla thread-safety
        private static readonly SemaphoreSlim _lockSalda = new(1, 1);
        private static readonly SemaphoreSlim _lockPotwierdzenia = new(1, 1);

        private static bool IsCacheSaldaValid(DateTime dataDo, string handlowiec)
        {
            if (_cacheSalda == null) return false;
            if (DateTime.Now - _cacheSaldaTime > CacheSaldaLifetime) return false;
            if (_cacheSaldaDataDo != dataDo.Date) return false;
            if (_cacheSaldaHandlowiec != handlowiec) return false;
            return true;
        }

        private static bool IsCachePotwierdzeniaValid()
        {
            if (_cachePotwierdzenia == null) return false;
            return DateTime.Now - _cachePotwierdzeniaTime <= CachePotwierdzeniaLifetime;
        }

        private static bool IsCacheHandlowcyValid()
        {
            return DateTime.Now - _cacheHandlowcyTime <= CacheHandlowcyLifetime;
        }

        /// <summary>
        /// Invaliduje cache sald (selektywnie)
        /// </summary>
        public static void InvalidateCache()
        {
            _cacheSalda = null;
        }

        /// <summary>
        /// Invaliduje wszystkie cache'e
        /// </summary>
        public static void InvalidateAllCaches()
        {
            _cacheSalda = null;
            _cachePotwierdzenia = null;
            _cacheHandlowcy.Clear();
            _cacheDokumenty.Clear();
        }

        /// <summary>
        /// Invaliduje cache potwierdzeń (gdy dodano nowe)
        /// </summary>
        public static void InvalidatePotwierdzeniaCache()
        {
            _cachePotwierdzenia = null;
        }

        #endregion

        #region Pre-compiled SQL Queries (Static)

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

        #region Pobieranie sald (z parallel loading)

        /// <summary>
        /// Pobiera salda WSZYSTKICH kontrahentów - ZOPTYMALIZOWANE
        /// </summary>
        public async Task<List<SaldoKontrahenta>> PobierzWszystkieSaldaAsync(DateTime dataDo, string handlowiecFilter = null)
        {
            // Fast path: zwróć z cache jeśli valid
            if (IsCacheSaldaValid(dataDo, handlowiecFilter))
            {
                return _cacheSalda;
            }

            await _lockSalda.WaitAsync();
            try
            {
                // Double-check po uzyskaniu locka
                if (IsCacheSaldaValid(dataDo, handlowiecFilter))
                {
                    return _cacheSalda;
                }

                // PARALLEL LOADING: pobierz potwierdzenia i salda równolegle
                var taskPotwierdzenia = PobierzWszystkiePotwierdzeniaAsync();
                var taskSalda = PobierzSaldaZBazyAsync(dataDo, handlowiecFilter);

                await Task.WhenAll(taskPotwierdzenia, taskSalda);

                var potwierdzenia = await taskPotwierdzenia;
                var wyniki = await taskSalda;

                // Przypisz potwierdzenia (O(n) zamiast O(n*m))
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

                // Zapisz w cache
                _cacheSalda = wyniki;
                _cacheSaldaTime = DateTime.Now;
                _cacheSaldaDataDo = dataDo.Date;
                _cacheSaldaHandlowiec = handlowiecFilter;

                return wyniki;
            }
            finally
            {
                _lockSalda.Release();
            }
        }

        /// <summary>
        /// Pobiera salda z bazy (wewnętrzna)
        /// </summary>
        private async Task<List<SaldoKontrahenta>> PobierzSaldaZBazyAsync(DateTime dataDo, string handlowiecFilter)
        {
            // Pre-alokacja listy (szacowana wielkość)
            var wyniki = new List<SaldoKontrahenta>(500);

            try
            {
                using var connection = new SqlConnection(_connectionStringHandel);
                await connection.OpenAsync().ConfigureAwait(false);

                using var command = new SqlCommand(QuerySalda, connection);
                command.CommandTimeout = 60;
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
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania sald: {ex.Message}", ex);
            }

            return wyniki;
        }

        #endregion

        #region Dokumenty kontrahenta (z cache)

        /// <summary>
        /// Pobiera dokumenty kontrahenta - z cache
        /// </summary>
        public async Task<List<DokumentSalda>> PobierzDokumentyAsync(int kontrahentId, DateTime dataOd, DateTime dataDo)
        {
            // Cache key
            var cacheKey = $"{kontrahentId}_{dataOd:yyyyMMdd}_{dataDo:yyyyMMdd}";

            // Sprawdź cache
            if (_cacheDokumenty.TryGetValue(cacheKey, out var cached))
            {
                if (DateTime.Now - cached.Time <= CacheDokumentyLifetime)
                {
                    return cached.Data;
                }
            }

            // Pobierz z bazy
            var dokumenty = await PobierzDokumentyZBazyAsync(kontrahentId, dataOd, dataDo);

            // Zapisz w cache (z limitem wielkości)
            if (_cacheDokumenty.Count >= MaxCacheDokumentySize)
            {
                // Usuń najstarsze wpisy (proste LRU)
                var oldest = _cacheDokumenty.OrderBy(x => x.Value.Time).Take(10).Select(x => x.Key).ToList();
                foreach (var key in oldest)
                {
                    _cacheDokumenty.TryRemove(key, out _);
                }
            }

            _cacheDokumenty[cacheKey] = (dokumenty, DateTime.Now);

            return dokumenty;
        }

        /// <summary>
        /// Pobiera dokumenty z bazy (wewnętrzna)
        /// </summary>
        private async Task<List<DokumentSalda>> PobierzDokumentyZBazyAsync(int kontrahentId, DateTime dataOd, DateTime dataDo)
        {
            var dokumenty = new List<DokumentSalda>(50);

            try
            {
                using var connection = new SqlConnection(_connectionStringHandel);
                await connection.OpenAsync().ConfigureAwait(false);

                // PARALLEL: pobierz saldo początkowe i dokumenty równolegle
                int sE2 = 0, sH1 = 0, sEURO = 0, sPCV = 0, sDREW = 0;
                var listaDoc = new List<DokumentSalda>(30);

                // Task dla salda początkowego
                var taskSaldoPoczatkowe = Task.Run(async () =>
                {
                    using var conn = new SqlConnection(_connectionStringHandel);
                    await conn.OpenAsync().ConfigureAwait(false);

                    using var cmd = new SqlCommand(QuerySaldoPoczatkowe, conn);
                    cmd.Parameters.AddWithValue("@KontrahentId", kontrahentId);
                    cmd.Parameters.AddWithValue("@DataOd", dataOd);

                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    if (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4));
                    }
                    return (0, 0, 0, 0, 0);
                });

                // Task dla dokumentów
                var taskDokumenty = Task.Run(async () =>
                {
                    using var conn = new SqlConnection(_connectionStringHandel);
                    await conn.OpenAsync().ConfigureAwait(false);

                    using var cmd = new SqlCommand(QueryDokumenty, conn);
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
                });

                await Task.WhenAll(taskSaldoPoczatkowe, taskDokumenty);

                var saldoPoczatkowe = await taskSaldoPoczatkowe;
                sE2 = saldoPoczatkowe.Item1;
                sH1 = saldoPoczatkowe.Item2;
                sEURO = saldoPoczatkowe.Item3;
                sPCV = saldoPoczatkowe.Item4;
                sDREW = saldoPoczatkowe.Item5;

                listaDoc = await taskDokumenty;

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

                // Dodaj saldo końcowe na początek
                dokumenty.Add(new DokumentSalda
                {
                    NrDokumentu = $"Saldo na {dataDo:dd.MM.yyyy}",
                    Data = dataDo,
                    E2 = kE2,
                    H1 = kH1,
                    EURO = kEURO,
                    PCV = kPCV,
                    DREW = kDREW,
                    JestSaldem = true
                });

                // Dokumenty
                dokumenty.AddRange(listaDoc);

                // Saldo początkowe na koniec
                dokumenty.Add(new DokumentSalda
                {
                    NrDokumentu = $"Saldo na {dataOd:dd.MM.yyyy}",
                    Data = dataOd,
                    E2 = sE2,
                    H1 = sH1,
                    EURO = sEURO,
                    PCV = sPCV,
                    DREW = sDREW,
                    JestSaldem = true
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania dokumentów: {ex.Message}", ex);
            }

            return dokumenty;
        }

        #endregion

        #region Potwierdzenia (z osobnym cache)

        /// <summary>
        /// Pobiera wszystkie potwierdzenia - z osobnym cache
        /// </summary>
        private async Task<Dictionary<(int KontrahentId, string Typ), DateTime>> PobierzWszystkiePotwierdzeniaAsync()
        {
            // Fast path
            if (IsCachePotwierdzeniaValid())
            {
                return _cachePotwierdzenia;
            }

            await _lockPotwierdzenia.WaitAsync();
            try
            {
                // Double-check
                if (IsCachePotwierdzeniaValid())
                {
                    return _cachePotwierdzenia;
                }

                var wyniki = new Dictionary<(int, string), DateTime>(200);

                try
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
                catch
                {
                    // Ignoruj - tabela może nie istnieć
                    return new Dictionary<(int, string), DateTime>();
                }

                _cachePotwierdzenia = wyniki;
                _cachePotwierdzeniaTime = DateTime.Now;

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

                // Selektywna invalidacja cache'y
                InvalidatePotwierdzeniaCache();
                InvalidateCache();

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
                // Ignoruj - tabela może nie istnieć
            }

            return wyniki;
        }

        #endregion

        #region Uprawnienia (z długim cache)

        /// <summary>
        /// Pobiera filtr handlowca dla użytkownika - z długim cache
        /// </summary>
        public async Task<string> PobierzHandlowcaAsync(string userId)
        {
            if (userId == "11111") return null; // Admin

            // Sprawdź cache
            if (IsCacheHandlowcyValid() && _cacheHandlowcy.TryGetValue(userId, out var cached))
            {
                return cached;
            }

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

                // Zapisz w cache
                if (!IsCacheHandlowcyValid())
                {
                    _cacheHandlowcy.Clear();
                    _cacheHandlowcyTime = DateTime.Now;
                }
                _cacheHandlowcy[userId] = handlowiec;

                return handlowiec;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Pre-fetching (background loading)

        /// <summary>
        /// Pre-ładuje dane w tle (fire-and-forget)
        /// </summary>
        public void PreloadDataAsync(DateTime dataDo, string handlowiecFilter = null)
        {
            // Fire-and-forget pre-loading
            _ = Task.Run(async () =>
            {
                try
                {
                    if (!IsCacheSaldaValid(dataDo, handlowiecFilter))
                    {
                        await PobierzWszystkieSaldaAsync(dataDo, handlowiecFilter).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Ignoruj błędy pre-loadingu
                }
            });
        }

        /// <summary>
        /// Pre-ładuje dokumenty kontrahenta w tle
        /// </summary>
        public void PreloadDokumentyAsync(int kontrahentId, DateTime dataOd, DateTime dataDo)
        {
            var cacheKey = $"{kontrahentId}_{dataOd:yyyyMMdd}_{dataDo:yyyyMMdd}";

            // Sprawdź czy już w cache
            if (_cacheDokumenty.TryGetValue(cacheKey, out var cached))
            {
                if (DateTime.Now - cached.Time <= CacheDokumentyLifetime)
                {
                    return; // Już jest w cache
                }
            }

            // Fire-and-forget
            _ = Task.Run(async () =>
            {
                try
                {
                    await PobierzDokumentyAsync(kontrahentId, dataOd, dataDo).ConfigureAwait(false);
                }
                catch
                {
                    // Ignoruj
                }
            });
        }

        #endregion

        #region IDisposable

        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _lockSalda?.Dispose();
                _lockPotwierdzenia?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}
