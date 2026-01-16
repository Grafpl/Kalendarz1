using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZPSP.Sales.Models;

namespace ZPSP.Sales.Repositories.Interfaces
{
    /// <summary>
    /// Interfejs repozytorium zamówień (LibraNet.ZamowieniaMieso)
    /// </summary>
    public interface IOrderRepository
    {
        /// <summary>
        /// Pobiera zamówienia dla wskazanej daty uboju
        /// </summary>
        /// <param name="date">Data uboju</param>
        /// <param name="includeCancelled">Czy uwzględnić anulowane zamówienia</param>
        Task<IEnumerable<Order>> GetOrdersForDateAsync(DateTime date, bool includeCancelled = true);

        /// <summary>
        /// Pobiera zamówienia dla wskazanej daty z filtrem po produkcie
        /// </summary>
        /// <param name="date">Data uboju</param>
        /// <param name="productId">ID produktu</param>
        Task<IEnumerable<Order>> GetOrdersForDateWithProductAsync(DateTime date, int productId);

        /// <summary>
        /// Pobiera zamówienie po ID
        /// </summary>
        /// <param name="id">ID zamówienia</param>
        Task<Order> GetOrderByIdAsync(int id);

        /// <summary>
        /// Pobiera pozycje zamówienia
        /// </summary>
        /// <param name="orderId">ID zamówienia</param>
        Task<IEnumerable<OrderItem>> GetOrderItemsAsync(int orderId);

        /// <summary>
        /// Pobiera pozycje dla wielu zamówień (batch loading)
        /// </summary>
        /// <param name="orderIds">Lista ID zamówień</param>
        Task<IDictionary<int, List<OrderItem>>> GetOrderItemsBatchAsync(IEnumerable<int> orderIds);

        /// <summary>
        /// Pobiera podsumowanie zamówień per produkt na dany dzień
        /// </summary>
        /// <param name="date">Data uboju</param>
        Task<IDictionary<int, (decimal Suma, int LiczbaKlientow)>> GetOrderSummaryPerProductAsync(DateTime date);

        /// <summary>
        /// Tworzy nowe zamówienie
        /// </summary>
        /// <param name="order">Zamówienie do utworzenia</param>
        /// <param name="userId">ID użytkownika tworzącego</param>
        Task<int> CreateOrderAsync(Order order, string userId);

        /// <summary>
        /// Aktualizuje uwagi zamówienia
        /// </summary>
        /// <param name="orderId">ID zamówienia</param>
        /// <param name="notes">Nowa treść uwag</param>
        Task UpdateOrderNotesAsync(int orderId, string notes);

        /// <summary>
        /// Anuluje zamówienie
        /// </summary>
        /// <param name="orderId">ID zamówienia</param>
        /// <param name="userId">ID użytkownika anulującego</param>
        /// <param name="reason">Przyczyna anulowania</param>
        Task CancelOrderAsync(int orderId, string userId, string reason);

        /// <summary>
        /// Przywraca anulowane zamówienie
        /// </summary>
        /// <param name="orderId">ID zamówienia</param>
        Task RestoreOrderAsync(int orderId);

        /// <summary>
        /// Duplikuje zamówienie na inną datę
        /// </summary>
        /// <param name="sourceOrderId">ID zamówienia źródłowego</param>
        /// <param name="targetDate">Docelowa data uboju</param>
        /// <param name="copyNotes">Czy kopiować uwagi</param>
        Task<int> DuplicateOrderAsync(int sourceOrderId, DateTime targetDate, bool copyNotes = false);

        /// <summary>
        /// Pobiera ID klientów z zamówieniami na dany dzień
        /// </summary>
        /// <param name="date">Data uboju</param>
        Task<IEnumerable<int>> GetClientIdsWithOrdersAsync(DateTime date);

        /// <summary>
        /// Pobiera dane dashboardu (jedno zoptymalizowane zapytanie)
        /// </summary>
        /// <param name="date">Data uboju</param>
        Task<DashboardData> GetDashboardSummaryAsync(DateTime date);

        /// <summary>
        /// Pobiera średnie ważone ceny dla zamówień
        /// </summary>
        /// <param name="orderIds">Lista ID zamówień</param>
        Task<IDictionary<int, decimal>> GetAveragePricesForOrdersAsync(IEnumerable<int> orderIds);
    }
}
