using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kalendarz1.Zywiec.Kalendarz.Services
{
    /// <summary>
    /// Globalny cache listy hodowców (Dostawcy.Name).
    /// - Per-process (static), współdzielony między oknami kalendarza.
    /// - Auto-invalidacja gdy COUNT(*) z bazy != liczba w cache (tani check).
    /// - Manualna invalidacja przez Invalidate() po dodaniu/edycji hodowcy w innym module.
    /// </summary>
    public static class HodowcyCacheManager
    {
        private static readonly object _lock = new object();
        private static List<string> _cache = null;
        private static DateTime _cacheExpiry = DateTime.MinValue;
        private static int _lastCount = -1;

        // Standard TTL - 30 minut
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Wyczyść cache - następne wywołanie pobierze świeże dane z bazy.
        /// Wywołać po dodaniu/edycji hodowcy w innym module.
        /// </summary>
        public static void Invalidate()
        {
            lock (_lock)
            {
                _cache = null;
                _cacheExpiry = DateTime.MinValue;
                _lastCount = -1;
            }
        }

        /// <summary>
        /// Pobierz listę hodowców. Jeśli cache świeży i COUNT(*) zgodny - zwraca z cache.
        /// W przeciwnym razie pobiera z bazy.
        /// </summary>
        /// <param name="connectionString">Connection string LibraNet</param>
        /// <param name="forceReload">Wymuś przeładowanie ignorując cache</param>
        public static async Task<List<string>> GetAsync(string connectionString, bool forceReload = false)
        {
            // Sprawdź cache pod lockiem - bez I/O
            List<string> cached = null;
            bool needRefresh = forceReload;
            lock (_lock)
            {
                if (!forceReload && _cache != null && DateTime.Now < _cacheExpiry)
                {
                    cached = new List<string>(_cache);
                }
                else
                {
                    needRefresh = true;
                }
            }
            if (cached != null && !needRefresh) return cached;

            // Tani check: czy liczba hodowców się zmieniła? Jeśli nie, użyj cache nawet jak wygasł
            if (!forceReload && _cache != null)
            {
                int? currentCount = await TryGetCountAsync(connectionString);
                if (currentCount.HasValue && currentCount.Value == _lastCount)
                {
                    // Cache jest poprawny, tylko wygasł - przedłuż
                    lock (_lock)
                    {
                        _cacheExpiry = DateTime.Now.Add(CACHE_DURATION);
                        return new List<string>(_cache);
                    }
                }
            }

            // Pełny refresh z bazy
            var fresh = new List<string>();
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(
                        "SELECT Name FROM [LibraNet].[dbo].[Dostawcy] WHERE Halt = '0' ORDER BY Name", conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string name = reader["Name"]?.ToString();
                            if (!string.IsNullOrEmpty(name)) fresh.Add(name);
                        }
                    }
                }

                lock (_lock)
                {
                    _cache = fresh;
                    _cacheExpiry = DateTime.Now.Add(CACHE_DURATION);
                    _lastCount = fresh.Count;
                }
            }
            catch
            {
                // Jeśli refresh nie wyszedł a mamy stary cache - zwróć go
                lock (_lock)
                {
                    if (_cache != null) return new List<string>(_cache);
                }
            }

            return fresh;
        }

        /// <summary>
        /// Tani check - SELECT COUNT(*). Zwraca null jeśli błąd.
        /// </summary>
        private static async Task<int?> TryGetCountAsync(string connectionString)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM [LibraNet].[dbo].[Dostawcy] WHERE Halt = '0'", conn))
                    {
                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                            return Convert.ToInt32(result);
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Wyszukiwanie po fragmencie nazwy (case-insensitive).
        /// </summary>
        public static IEnumerable<string> Search(string searchText)
        {
            if (string.IsNullOrEmpty(searchText)) yield break;
            List<string> snapshot;
            lock (_lock)
            {
                if (_cache == null) yield break;
                snapshot = new List<string>(_cache);
            }
            foreach (var h in snapshot)
            {
                if (h.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    yield return h;
            }
        }
    }
}
