using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZPSP.Sales.Models;
using ZPSP.Sales.Repositories.Interfaces;
using ZPSP.Sales.Services.Interfaces;

namespace ZPSP.Sales.Services
{
    /// <summary>
    /// Serwis produktów - logika biznesowa dla produktów, agregacji i bilansowania.
    /// </summary>
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly ICacheService _cacheService;

        public ProductService(
            IProductRepository productRepository,
            IOrderRepository orderRepository,
            ICacheService cacheService)
        {
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            return await _productRepository.GetAllMeatProductsAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<ProductAggregation>> GetProductAggregationsAsync(DateTime date, bool useReleases = false)
        {
            var aggregations = new List<ProductAggregation>();

            // Pobierz konfigurację
            var yieldConfig = await _productRepository.GetYieldConfigurationAsync(date);
            var productConfig = await _productRepository.GetProductConfigurationAsync(date);
            var groupMapping = await _productRepository.GetProductGroupMappingAsync();
            var productConfigDict = productConfig.ToDictionary(p => p.ProductId, p => p);

            // Pobierz masę z harmonogramu
            var scheduledMass = await _productRepository.GetScheduledMassAsync(date);
            var pulaCalkowita = scheduledMass * (yieldConfig.Wspolczynnik / 100m);
            var pulaB = pulaCalkowita * (yieldConfig.ProcentB / 100m);

            // Pobierz produkty i ich nazwy
            var products = await _cacheService.GetProductsAsync();
            var productIds = productConfigDict.Keys.ToList();

            // Pobierz dane agregowane
            var orderSummary = await _orderRepository.GetOrderSummaryPerProductAsync(date);
            var releases = await _cacheService.GetReleasesAsync(date);
            var income = await _cacheService.GetIncomeAsync(date, productIds);
            var stocks = await _productRepository.GetInventoryStocksAsync(date);

            foreach (var config in productConfig.OrderByDescending(c => c.ProcentUdzialu))
            {
                var productId = config.ProductId;
                var productName = products.TryGetValue(productId, out var name) ? name : $"[{productId}]";

                var agg = new ProductAggregation
                {
                    ProductId = productId,
                    ProductName = productName,
                    ProcentUdzialu = config.ProcentUdzialu,
                    GroupName = groupMapping.TryGetValue(productId, out var group) ? group : null,
                    Plan = pulaB * (config.ProcentUdzialu / 100m),
                    Fakt = income.TryGetValue(productId, out var fact) ? fact : 0,
                    Stan = stocks.TryGetValue(productId, out var stock) ? stock : 0,
                    Zamowienia = orderSummary.TryGetValue(productId, out var orders) ? orders.Suma : 0,
                    Wydania = releases.TryGetValue(productId, out var rel) ? rel : 0,
                    LiczbaKlientow = orderSummary.TryGetValue(productId, out var ordersData) ? ordersData.LiczbaKlientow : 0
                };

                // Oblicz bilans: (Fakt lub Plan) + Stan - (Zamówienia lub Wydania)
                decimal baseVal = agg.Fakt > 0 ? agg.Fakt : agg.Plan;
                decimal odejmij = useReleases ? agg.Wydania : agg.Zamowienia;
                agg.Bilans = baseVal + agg.Stan - odejmij;

                aggregations.Add(agg);
            }

            return aggregations;
        }

        /// <inheritdoc/>
        public async Task<ProductAggregation> GetProductAggregationAsync(int productId, DateTime date, bool useReleases = false)
        {
            var allAggregations = await GetProductAggregationsAsync(date, useReleases);
            return allAggregations.FirstOrDefault(a => a.ProductId == productId);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<ProductConfiguration>> GetProductConfigurationAsync(DateTime date)
        {
            return await _productRepository.GetProductConfigurationAsync(date);
        }

        /// <inheritdoc/>
        public async Task<YieldConfiguration> GetYieldConfigurationAsync(DateTime date)
        {
            return await _productRepository.GetYieldConfigurationAsync(date);
        }

        /// <inheritdoc/>
        public async Task<decimal> CalculateProductBalanceAsync(int productId, DateTime date, bool useReleases = false)
        {
            var agg = await GetProductAggregationAsync(productId, date, useReleases);
            return agg?.Bilans ?? 0;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Customer>> GetPotentialCustomersForProductAsync(int productId, DateTime date)
        {
            // W pełnej implementacji:
            // 1. Pobierz klientów którzy kupowali ten produkt w ostatnich 30 dniach
            // 2. Odfiltruj tych którzy już zamówili na ten dzień
            // Na razie zwracamy pustą listę
            return await Task.FromResult(Enumerable.Empty<Customer>());
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, string>> GetProductGroupMappingAsync()
        {
            return await _productRepository.GetProductGroupMappingAsync();
        }

        /// <inheritdoc/>
        public async Task<IDictionary<string, ProductAggregation>> GetGroupedAggregationsAsync(DateTime date, bool useReleases = false)
        {
            var allAggregations = await GetProductAggregationsAsync(date, useReleases);
            var groupMapping = await _productRepository.GetProductGroupMappingAsync();

            var grouped = new Dictionary<string, ProductAggregation>(StringComparer.OrdinalIgnoreCase);

            foreach (var agg in allAggregations)
            {
                var groupName = agg.GroupName;
                if (string.IsNullOrEmpty(groupName))
                    continue;

                if (!grouped.ContainsKey(groupName))
                {
                    grouped[groupName] = new ProductAggregation
                    {
                        ProductName = groupName,
                        GroupName = groupName
                    };
                }

                var group = grouped[groupName];
                group.Plan += agg.Plan;
                group.Fakt += agg.Fakt;
                group.Stan += agg.Stan;
                group.Zamowienia += agg.Zamowienia;
                group.Wydania += agg.Wydania;
                group.LiczbaKlientow += agg.LiczbaKlientow;
                group.ProcentUdzialu += agg.ProcentUdzialu;
            }

            // Oblicz bilans dla grup
            foreach (var group in grouped.Values)
            {
                decimal baseVal = group.Fakt > 0 ? group.Fakt : group.Plan;
                decimal odejmij = useReleases ? group.Wydania : group.Zamowienia;
                group.Bilans = baseVal + group.Stan - odejmij;
            }

            return grouped;
        }
    }
}
