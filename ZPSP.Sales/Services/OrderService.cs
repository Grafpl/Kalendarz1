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
    /// Serwis zamówień - logika biznesowa dla operacji na zamówieniach.
    /// </summary>
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly ICacheService _cacheService;

        public OrderService(
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            ICacheService cacheService)
        {
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Order>> GetOrdersForDateAsync(DateTime date, bool includeCancelled = true, int? productIdFilter = null)
        {
            // Pobierz zamówienia
            var orders = productIdFilter.HasValue
                ? await _orderRepository.GetOrdersForDateWithProductAsync(date, productIdFilter.Value)
                : await _orderRepository.GetOrdersForDateAsync(date, includeCancelled);

            var orderList = orders.ToList();
            if (!orderList.Any())
                return orderList;

            // Wzbogać o dane kontrahentów (batch)
            var customerIds = orderList.Select(o => o.KlientId).Distinct().ToList();
            var customers = await _cacheService.GetCustomersAsync();

            foreach (var order in orderList)
            {
                if (customers.TryGetValue(order.KlientId, out var customer))
                {
                    order.Odbiorca = customer.Shortcut;
                    order.Handlowiec = customer.Handlowiec;
                }
                else
                {
                    order.Odbiorca = $"[KH {order.KlientId}]";
                }

                // Formatuj termin odbioru
                order.TerminOdbioru = order.DataPrzyjecia.ToString("dd.MM HH:mm");

                // Status domyślny
                if (string.IsNullOrEmpty(order.Status))
                    order.Status = "Nowe";
            }

            // Pobierz wydania dla tych zamówień (batch)
            var releases = await _cacheService.GetReleasesAsync(date);

            // Pobierz pozycje zamówień (batch) dla obliczenia wydań per zamówienie
            var orderIds = orderList.Select(o => o.Id).ToList();
            var orderItems = await _orderRepository.GetOrderItemsBatchAsync(orderIds);

            // Oblicz faktyczne ilości wydane per zamówienie
            var releasesPerClient = await _productRepository.GetReleasesPerClientAsync(
                date,
                orderItems.Values.SelectMany(items => items.Select(i => i.KodTowaru)).Distinct());

            foreach (var order in orderList)
            {
                if (orderItems.TryGetValue(order.Id, out var items))
                {
                    decimal totalReleased = 0;
                    foreach (var item in items)
                    {
                        if (releasesPerClient.TryGetValue((order.KlientId, item.KodTowaru), out var released))
                        {
                            totalReleased += released;
                        }
                    }
                    order.IloscFaktyczna = totalReleased;
                    order.Roznica = order.IloscZamowiona - order.IloscFaktyczna;
                }
            }

            // Pobierz średnie ceny
            var avgPrices = await _orderRepository.GetAveragePricesForOrdersAsync(orderIds);
            foreach (var order in orderList)
            {
                if (avgPrices.TryGetValue(order.Id, out var avgPrice))
                {
                    order.SredniaCena = avgPrice;
                }
            }

            return orderList;
        }

        /// <inheritdoc/>
        public async Task<Order> GetOrderWithDetailsAsync(int orderId)
        {
            var order = await _orderRepository.GetOrderByIdAsync(orderId);
            if (order == null)
                return null;

            // Wzbogać o dane kontrahenta
            order.Odbiorca = await _cacheService.GetCustomerNameAsync(order.KlientId);
            order.Handlowiec = await _cacheService.GetSalesmanForCustomerAsync(order.KlientId);

            // Pobierz pozycje z nazwami
            order.Pozycje = (await GetOrderItemsWithNamesAsync(orderId)).ToList();

            return order;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<OrderItem>> GetOrderItemsWithNamesAsync(int orderId)
        {
            var items = await _orderRepository.GetOrderItemsAsync(orderId);
            var itemList = items.ToList();

            // Pobierz nazwy produktów
            var products = await _cacheService.GetProductsAsync();
            foreach (var item in itemList)
            {
                if (products.TryGetValue(item.KodTowaru, out var name))
                {
                    item.NazwaTowaru = name;
                }
                else
                {
                    item.NazwaTowaru = $"[{item.KodTowaru}]";
                }
            }

            return itemList;
        }

        /// <inheritdoc/>
        public async Task<int> CreateOrderAsync(Order order, string userId)
        {
            // Walidacja
            if (order.KlientId <= 0)
                throw new ArgumentException("Wymagany ID klienta");

            if (!order.DataUboju.HasValue)
                throw new ArgumentException("Wymagana data uboju");

            if (!order.Pozycje.Any())
                throw new ArgumentException("Zamówienie musi mieć co najmniej jedną pozycję");

            // Utwórz zamówienie
            var newId = await _orderRepository.CreateOrderAsync(order, userId);

            System.Diagnostics.Debug.WriteLine($"[OrderService] Utworzono zamówienie ID={newId} dla klienta {order.KlientId}");

            return newId;
        }

        /// <inheritdoc/>
        public async Task UpdateNotesAsync(int orderId, string notes, string userId)
        {
            var order = await _orderRepository.GetOrderByIdAsync(orderId);
            if (order == null)
                throw new InvalidOperationException($"Zamówienie {orderId} nie istnieje");

            if (!CanEditOrder(order))
                throw new InvalidOperationException("Zamówienie nie może być edytowane");

            await _orderRepository.UpdateOrderNotesAsync(orderId, notes);

            System.Diagnostics.Debug.WriteLine($"[OrderService] Zaktualizowano uwagi dla zamówienia {orderId}");
        }

        /// <inheritdoc/>
        public async Task CancelOrderAsync(int orderId, string userId, string reason)
        {
            var order = await _orderRepository.GetOrderByIdAsync(orderId);
            if (order == null)
                throw new InvalidOperationException($"Zamówienie {orderId} nie istnieje");

            if (order.Status == "Anulowane")
                throw new InvalidOperationException("Zamówienie jest już anulowane");

            await _orderRepository.CancelOrderAsync(orderId, userId, reason);

            System.Diagnostics.Debug.WriteLine($"[OrderService] Anulowano zamówienie {orderId}. Powód: {reason}");
        }

        /// <inheritdoc/>
        public async Task RestoreOrderAsync(int orderId, string userId)
        {
            var order = await _orderRepository.GetOrderByIdAsync(orderId);
            if (order == null)
                throw new InvalidOperationException($"Zamówienie {orderId} nie istnieje");

            if (order.Status != "Anulowane")
                throw new InvalidOperationException("Można przywrócić tylko anulowane zamówienie");

            await _orderRepository.RestoreOrderAsync(orderId);

            System.Diagnostics.Debug.WriteLine($"[OrderService] Przywrócono zamówienie {orderId}");
        }

        /// <inheritdoc/>
        public async Task<int> DuplicateOrderAsync(int sourceOrderId, DateTime targetDate, bool copyNotes = false)
        {
            return await _orderRepository.DuplicateOrderAsync(sourceOrderId, targetDate, copyNotes);
        }

        /// <inheritdoc/>
        public async Task<DashboardData> GetDashboardDataAsync(DateTime date)
        {
            // Pobierz podstawowe podsumowanie
            var dashboard = await _orderRepository.GetDashboardSummaryAsync(date);

            // Pobierz konfigurację wydajności
            var yieldConfig = await _productRepository.GetYieldConfigurationAsync(date);
            dashboard.WspolczynnikWydajnosci = yieldConfig.Wspolczynnik;

            // Pobierz masę z harmonogramu
            var scheduledMass = await _productRepository.GetScheduledMassAsync(date);

            // Oblicz pule
            var pulaCalkowita = scheduledMass * (yieldConfig.Wspolczynnik / 100m);
            dashboard.PulaKurczakA = pulaCalkowita * (yieldConfig.ProcentA / 100m);
            dashboard.PulaKurczakB = pulaCalkowita * (yieldConfig.ProcentB / 100m);

            // Pobierz faktyczne przychody
            var products = await _cacheService.GetProductsAsync();
            var productIds = products.Keys.ToList();

            var actualIncome = await _cacheService.GetIncomeAsync(date, productIds);
            dashboard.FaktKurczakA = actualIncome.Values.Sum(); // Uproszczone - w pełnej wersji filtruj po Kurczak A

            // Pobierz wydania
            var releases = await _cacheService.GetReleasesAsync(date);
            dashboard.SumaWydan = releases.Values.Sum();

            // Oblicz bilans
            var cel = dashboard.FaktKurczakA > 0 ? dashboard.FaktKurczakA : (dashboard.PulaKurczakA + dashboard.PulaKurczakB);
            dashboard.BilansCalkowity = cel - dashboard.SumaZamowien;

            return dashboard;
        }

        /// <inheritdoc/>
        public async Task<decimal> CalculateOrderDifferenceAsync(int orderId, DateTime date)
        {
            var order = await _orderRepository.GetOrderByIdAsync(orderId);
            if (order == null)
                return 0;

            var items = await _orderRepository.GetOrderItemsAsync(orderId);
            var productIds = items.Select(i => i.KodTowaru).Distinct().ToList();

            var releases = await _productRepository.GetReleasesPerClientAsync(date, productIds);

            decimal totalOrdered = items.Sum(i => i.Ilosc);
            decimal totalReleased = releases
                .Where(r => r.Key.KlientId == order.KlientId && productIds.Contains(r.Key.ProduktId))
                .Sum(r => r.Value);

            return totalOrdered - totalReleased;
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, decimal>> GetOrderSumsPerProductAsync(DateTime date)
        {
            var summary = await _orderRepository.GetOrderSummaryPerProductAsync(date);
            return summary.ToDictionary(x => x.Key, x => x.Value.Suma);
        }

        /// <inheritdoc/>
        public bool CanEditOrder(Order order)
        {
            if (order == null)
                return false;

            // Nie można edytować anulowanych lub zrealizowanych
            if (order.Status == "Anulowane")
                return false;

            if (order.CzyZrealizowane)
                return false;

            return true;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<OrderChangeHistory>> GetOrderHistoryAsync(int orderId)
        {
            // W pełnej implementacji użylibyśmy HistoryRepository
            // Na razie zwracamy pustą listę
            return await Task.FromResult(Enumerable.Empty<OrderChangeHistory>());
        }
    }
}
