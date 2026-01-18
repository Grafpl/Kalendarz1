using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZPSP.Sales.Repositories.Interfaces;
using ZPSP.Sales.Services.Interfaces;

namespace ZPSP.Sales.Services
{
    /// <summary>
    /// Centralny serwis cache dla danych kontrahentów, produktów, wydań i przychodów.
    /// Używa ConcurrentDictionary dla thread-safety i SemaphoreSlim dla synchronizacji ładowania.
    /// </summary>
    public class CacheService : ICacheService, IDisposable
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IProductRepository _productRepository;

        // Cache dla kontrahentów i produktów (statyczne, rzadko się zmieniają)
        private Dictionary<int, (string Shortcut, string Handlowiec)> _customers;
        private Dictionary<int, string> _products;
        private Dictionary<string, string> _users;

        // Cache per data (dynamiczne)
        private readonly ConcurrentDictionary<DateTime, Dictionary<int, decimal>> _releasesCache = new();
        private readonly ConcurrentDictionary<(DateTime, string), Dictionary<int, decimal>> _incomeCache = new();

        // Timestamp ostatniego odświeżenia
        private DateTime? _customersLastRefresh;
        private DateTime? _productsLastRefresh;
        private DateTime? _usersLastRefresh;

        // TTL dla cache (minuty)
        private const int CustomersCacheTtlMinutes = 30;
        private const int ProductsCacheTtlMinutes = 60;
        private const int UsersCacheTtlMinutes = 60;
        private const int DateCacheTtlMinutes = 5;

        // Semaphory do synchronizacji ładowania
        private readonly SemaphoreSlim _customersSemaphore = new(1, 1);
        private readonly SemaphoreSlim _productsSemaphore = new(1, 1);
        private readonly SemaphoreSlim _usersSemaphore = new(1, 1);

        public DateTime? CustomersLastRefresh => _customersLastRefresh;
        public DateTime? ProductsLastRefresh => _productsLastRefresh;

        public CacheService(ICustomerRepository customerRepository, IProductRepository productRepository)
        {
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, (string Shortcut, string Handlowiec)>> GetCustomersAsync(bool forceRefresh = false)
        {
            if (!forceRefresh && _customers != null && IsCacheValid(_customersLastRefresh, CustomersCacheTtlMinutes))
            {
                return _customers;
            }

            await _customersSemaphore.WaitAsync();
            try
            {
                // Double-check po uzyskaniu locka
                if (!forceRefresh && _customers != null && IsCacheValid(_customersLastRefresh, CustomersCacheTtlMinutes))
                {
                    return _customers;
                }

                var lookup = await _customerRepository.GetCustomersLookupAsync();
                _customers = new Dictionary<int, (string Shortcut, string Handlowiec)>(lookup);
                _customersLastRefresh = DateTime.Now;

                System.Diagnostics.Debug.WriteLine($"[CacheService] Załadowano {_customers.Count} kontrahentów");
                return _customers;
            }
            finally
            {
                _customersSemaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, string>> GetProductsAsync(bool forceRefresh = false)
        {
            if (!forceRefresh && _products != null && IsCacheValid(_productsLastRefresh, ProductsCacheTtlMinutes))
            {
                return _products;
            }

            await _productsSemaphore.WaitAsync();
            try
            {
                if (!forceRefresh && _products != null && IsCacheValid(_productsLastRefresh, ProductsCacheTtlMinutes))
                {
                    return _products;
                }

                var allProducts = await _productRepository.GetAllMeatProductsAsync();
                _products = allProducts.ToDictionary(p => p.Id, p => p.Kod ?? p.Nazwa);
                _productsLastRefresh = DateTime.Now;

                System.Diagnostics.Debug.WriteLine($"[CacheService] Załadowano {_products.Count} produktów");
                return _products;
            }
            finally
            {
                _productsSemaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, decimal>> GetReleasesAsync(DateTime date, bool forceRefresh = false)
        {
            var cacheKey = date.Date;

            if (!forceRefresh && _releasesCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var releases = await _productRepository.GetReleasesAsync(date);
            var releasesDict = new Dictionary<int, decimal>(releases);

            _releasesCache[cacheKey] = releasesDict;
            System.Diagnostics.Debug.WriteLine($"[CacheService] Załadowano wydania dla {date:yyyy-MM-dd}: {releasesDict.Count} produktów");

            return releasesDict;
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, decimal>> GetIncomeAsync(DateTime date, IEnumerable<int> productIds, bool forceRefresh = false)
        {
            var idList = productIds?.ToList() ?? new List<int>();
            if (!idList.Any())
                return new Dictionary<int, decimal>();

            var cacheKey = (date.Date, string.Join(",", idList.OrderBy(x => x)));

            if (!forceRefresh && _incomeCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var income = await _productRepository.GetActualIncomeAsync(date, idList);
            var incomeDict = new Dictionary<int, decimal>(income);

            _incomeCache[cacheKey] = incomeDict;
            System.Diagnostics.Debug.WriteLine($"[CacheService] Załadowano przychody dla {date:yyyy-MM-dd}: {incomeDict.Count} produktów");

            return incomeDict;
        }

        /// <inheritdoc/>
        public async Task<string> GetCustomerNameAsync(int customerId)
        {
            var customers = await GetCustomersAsync();
            return customers.TryGetValue(customerId, out var customer) ? customer.Shortcut : $"[KH {customerId}]";
        }

        /// <inheritdoc/>
        public async Task<string> GetSalesmanForCustomerAsync(int customerId)
        {
            var customers = await GetCustomersAsync();
            return customers.TryGetValue(customerId, out var customer) ? customer.Handlowiec : "";
        }

        /// <inheritdoc/>
        public async Task<string> GetProductNameAsync(int productId)
        {
            var products = await GetProductsAsync();
            return products.TryGetValue(productId, out var name) ? name : $"[Towar {productId}]";
        }

        /// <inheritdoc/>
        public async Task<IDictionary<string, string>> GetUsersAsync()
        {
            if (_users != null && IsCacheValid(_usersLastRefresh, UsersCacheTtlMinutes))
            {
                return _users;
            }

            await _usersSemaphore.WaitAsync();
            try
            {
                if (_users != null && IsCacheValid(_usersLastRefresh, UsersCacheTtlMinutes))
                {
                    return _users;
                }

                // Użytkownicy są w LibraNet.operators - tutaj prosty słownik
                // W pełnej implementacji pobralibyśmy z OrderRepository
                _users = new Dictionary<string, string>();
                _usersLastRefresh = DateTime.Now;

                return _users;
            }
            finally
            {
                _usersSemaphore.Release();
            }
        }

        /// <inheritdoc/>
        public void ClearAll()
        {
            _customers = null;
            _products = null;
            _users = null;
            _customersLastRefresh = null;
            _productsLastRefresh = null;
            _usersLastRefresh = null;
            _releasesCache.Clear();
            _incomeCache.Clear();

            System.Diagnostics.Debug.WriteLine("[CacheService] Wyczyszczono cały cache");
        }

        /// <inheritdoc/>
        public void ClearForDate(DateTime date)
        {
            var cacheKey = date.Date;
            _releasesCache.TryRemove(cacheKey, out _);

            // Wyczyść też income cache dla tej daty
            var keysToRemove = _incomeCache.Keys.Where(k => k.Item1 == cacheKey).ToList();
            foreach (var key in keysToRemove)
            {
                _incomeCache.TryRemove(key, out _);
            }

            System.Diagnostics.Debug.WriteLine($"[CacheService] Wyczyszczono cache dla {date:yyyy-MM-dd}");
        }

        /// <inheritdoc/>
        public void ClearCustomers()
        {
            _customers = null;
            _customersLastRefresh = null;
        }

        /// <inheritdoc/>
        public void ClearProducts()
        {
            _products = null;
            _productsLastRefresh = null;
        }

        private static bool IsCacheValid(DateTime? lastRefresh, int ttlMinutes)
        {
            if (!lastRefresh.HasValue)
                return false;

            return (DateTime.Now - lastRefresh.Value).TotalMinutes < ttlMinutes;
        }

        public void Dispose()
        {
            _customersSemaphore?.Dispose();
            _productsSemaphore?.Dispose();
            _usersSemaphore?.Dispose();
        }
    }
}
