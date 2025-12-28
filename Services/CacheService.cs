using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Serwis cache z automatycznym wygasaniem (TTL - Time To Live)
    /// </summary>
    public class CacheService<TKey, TValue> : IDisposable where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, CacheItem<TValue>> _cache = new();
        private readonly TimeSpan _defaultTtl;
        private readonly Timer _cleanupTimer;
        private bool _disposed;

        public CacheService(TimeSpan? defaultTtl = null, TimeSpan? cleanupInterval = null)
        {
            _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(10);
            var interval = cleanupInterval ?? TimeSpan.FromMinutes(1);
            _cleanupTimer = new Timer(CleanupExpiredItems, null, interval, interval);
        }

        /// <summary>
        /// Pobiera wartość z cache lub dodaje ją jeśli nie istnieje
        /// </summary>
        public TValue GetOrAdd(TKey key, Func<TValue> valueFactory, TimeSpan? ttl = null)
        {
            if (_cache.TryGetValue(key, out var item) && !item.IsExpired)
            {
                return item.Value;
            }

            var newValue = valueFactory();
            var expiresAt = DateTime.UtcNow.Add(ttl ?? _defaultTtl);
            _cache[key] = new CacheItem<TValue>(newValue, expiresAt);
            return newValue;
        }

        /// <summary>
        /// Asynchronicznie pobiera wartość z cache lub dodaje ją jeśli nie istnieje
        /// </summary>
        public async Task<TValue> GetOrAddAsync(TKey key, Func<Task<TValue>> valueFactory, TimeSpan? ttl = null)
        {
            if (_cache.TryGetValue(key, out var item) && !item.IsExpired)
            {
                return item.Value;
            }

            var newValue = await valueFactory();
            var expiresAt = DateTime.UtcNow.Add(ttl ?? _defaultTtl);
            _cache[key] = new CacheItem<TValue>(newValue, expiresAt);
            return newValue;
        }

        /// <summary>
        /// Próbuje pobrać wartość z cache
        /// </summary>
        public bool TryGet(TKey key, out TValue value)
        {
            if (_cache.TryGetValue(key, out var item) && !item.IsExpired)
            {
                value = item.Value;
                return true;
            }

            value = default!;
            return false;
        }

        /// <summary>
        /// Dodaje lub aktualizuje wartość w cache
        /// </summary>
        public void Set(TKey key, TValue value, TimeSpan? ttl = null)
        {
            var expiresAt = DateTime.UtcNow.Add(ttl ?? _defaultTtl);
            _cache[key] = new CacheItem<TValue>(value, expiresAt);
        }

        /// <summary>
        /// Usuwa wartość z cache
        /// </summary>
        public bool Remove(TKey key)
        {
            return _cache.TryRemove(key, out _);
        }

        /// <summary>
        /// Czyści cały cache
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Odświeża TTL dla danego klucza
        /// </summary>
        public bool Refresh(TKey key, TimeSpan? ttl = null)
        {
            if (_cache.TryGetValue(key, out var item) && !item.IsExpired)
            {
                var expiresAt = DateTime.UtcNow.Add(ttl ?? _defaultTtl);
                _cache[key] = new CacheItem<TValue>(item.Value, expiresAt);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Zwraca liczbę elementów w cache (włącznie z wygasłymi)
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// Zwraca liczbę aktywnych (niewygasłych) elementów
        /// </summary>
        public int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (var item in _cache.Values)
                {
                    if (!item.IsExpired) count++;
                }
                return count;
            }
        }

        private void CleanupExpiredItems(object? state)
        {
            foreach (var key in _cache.Keys)
            {
                if (_cache.TryGetValue(key, out var item) && item.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _cache.Clear();
                _disposed = true;
            }
        }

        private class CacheItem<T>
        {
            public T Value { get; }
            public DateTime ExpiresAt { get; }
            public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

            public CacheItem(T value, DateTime expiresAt)
            {
                Value = value;
                ExpiresAt = expiresAt;
            }
        }
    }

    /// <summary>
    /// Globalny manager cache dla aplikacji
    /// </summary>
    public static class AppCache
    {
        private static readonly Lazy<CacheService<string, object>> _instance =
            new(() => new CacheService<string, object>(TimeSpan.FromMinutes(10)));

        public static CacheService<string, object> Instance => _instance.Value;

        // Dedykowane cache dla różnych typów danych
        public static CacheService<int, string> Contractors { get; } =
            new(TimeSpan.FromMinutes(15));

        public static CacheService<int, string> Products { get; } =
            new(TimeSpan.FromMinutes(30));

        public static CacheService<string, string> Salesmen { get; } =
            new(TimeSpan.FromMinutes(60));

        public static CacheService<string, decimal> Prices { get; } =
            new(TimeSpan.FromMinutes(5));

        /// <summary>
        /// Czyści wszystkie cache
        /// </summary>
        public static void ClearAll()
        {
            Instance.Clear();
            Contractors.Clear();
            Products.Clear();
            Salesmen.Clear();
            Prices.Clear();
        }

        /// <summary>
        /// Zwraca statystyki cache
        /// </summary>
        public static string GetStats()
        {
            return $"Cache Stats:\n" +
                   $"  Global: {Instance.ActiveCount}/{Instance.Count}\n" +
                   $"  Contractors: {Contractors.ActiveCount}/{Contractors.Count}\n" +
                   $"  Products: {Products.ActiveCount}/{Products.Count}\n" +
                   $"  Salesmen: {Salesmen.ActiveCount}/{Salesmen.Count}\n" +
                   $"  Prices: {Prices.ActiveCount}/{Prices.Count}";
        }
    }
}
