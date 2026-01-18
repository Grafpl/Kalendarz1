using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZPSP.Sales.Models;

namespace ZPSP.Sales.Services.Interfaces
{
    /// <summary>
    /// Interfejs serwisu zamówień.
    /// Zawiera logikę biznesową dla operacji na zamówieniach.
    /// </summary>
    public interface IOrderService
    {
        /// <summary>
        /// Pobiera pełną listę zamówień na dany dzień z wzbogaceniem o kontrahentów i handlowców
        /// </summary>
        /// <param name="date">Data uboju</param>
        /// <param name="includeCancelled">Czy uwzględnić anulowane</param>
        /// <param name="productIdFilter">Opcjonalny filtr po produkcie</param>
        Task<IEnumerable<Order>> GetOrdersForDateAsync(DateTime date, bool includeCancelled = true, int? productIdFilter = null);

        /// <summary>
        /// Pobiera zamówienie po ID z pełnymi danymi
        /// </summary>
        Task<Order> GetOrderWithDetailsAsync(int orderId);

        /// <summary>
        /// Pobiera pozycje zamówienia z nazwami produktów
        /// </summary>
        Task<IEnumerable<OrderItem>> GetOrderItemsWithNamesAsync(int orderId);

        /// <summary>
        /// Tworzy nowe zamówienie
        /// </summary>
        /// <param name="order">Zamówienie do utworzenia</param>
        /// <param name="userId">ID użytkownika</param>
        Task<int> CreateOrderAsync(Order order, string userId);

        /// <summary>
        /// Aktualizuje uwagi zamówienia
        /// </summary>
        Task UpdateNotesAsync(int orderId, string notes, string userId);

        /// <summary>
        /// Anuluje zamówienie z logowaniem historii
        /// </summary>
        Task CancelOrderAsync(int orderId, string userId, string reason);

        /// <summary>
        /// Przywraca anulowane zamówienie
        /// </summary>
        Task RestoreOrderAsync(int orderId, string userId);

        /// <summary>
        /// Duplikuje zamówienie na inną datę
        /// </summary>
        Task<int> DuplicateOrderAsync(int sourceOrderId, DateTime targetDate, bool copyNotes = false);

        /// <summary>
        /// Pobiera dane dashboardu dla daty
        /// </summary>
        Task<DashboardData> GetDashboardDataAsync(DateTime date);

        /// <summary>
        /// Oblicza różnicę zamówienia vs wydania dla zamówienia
        /// </summary>
        Task<decimal> CalculateOrderDifferenceAsync(int orderId, DateTime date);

        /// <summary>
        /// Pobiera sumy zamówień per produkt na dany dzień
        /// </summary>
        Task<IDictionary<int, decimal>> GetOrderSumsPerProductAsync(DateTime date);

        /// <summary>
        /// Sprawdza czy zamówienie może być edytowane (nie anulowane, nie zrealizowane)
        /// </summary>
        bool CanEditOrder(Order order);

        /// <summary>
        /// Pobiera historię zmian zamówienia
        /// </summary>
        Task<IEnumerable<OrderChangeHistory>> GetOrderHistoryAsync(int orderId);
    }
}
