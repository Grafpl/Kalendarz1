using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZPSP.Sales.Services.Interfaces
{
    /// <summary>
    /// Interfejs centralnego serwisu cache.
    /// Zarządza cachowaniem danych: kontrahenci, produkty, wydania, przychody.
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Pobiera lub ładuje słownik kontrahentów (ID -> (Shortcut, Handlowiec))
        /// </summary>
        /// <param name="forceRefresh">Wymuś odświeżenie cache</param>
        Task<IDictionary<int, (string Shortcut, string Handlowiec)>> GetCustomersAsync(bool forceRefresh = false);

        /// <summary>
        /// Pobiera lub ładuje słownik produktów mięsnych (ID -> Kod)
        /// </summary>
        /// <param name="forceRefresh">Wymuś odświeżenie cache</param>
        Task<IDictionary<int, string>> GetProductsAsync(bool forceRefresh = false);

        /// <summary>
        /// Pobiera lub ładuje wydania per produkt na dany dzień
        /// </summary>
        /// <param name="date">Data</param>
        /// <param name="forceRefresh">Wymuś odświeżenie cache</param>
        Task<IDictionary<int, decimal>> GetReleasesAsync(DateTime date, bool forceRefresh = false);

        /// <summary>
        /// Pobiera lub ładuje przychody (PWP) per produkt na dany dzień
        /// </summary>
        /// <param name="date">Data</param>
        /// <param name="productIds">Lista ID produktów</param>
        /// <param name="forceRefresh">Wymuś odświeżenie cache</param>
        Task<IDictionary<int, decimal>> GetIncomeAsync(DateTime date, IEnumerable<int> productIds, bool forceRefresh = false);

        /// <summary>
        /// Pobiera nazwę kontrahenta po ID
        /// </summary>
        /// <param name="customerId">ID kontrahenta</param>
        Task<string> GetCustomerNameAsync(int customerId);

        /// <summary>
        /// Pobiera handlowca dla kontrahenta
        /// </summary>
        /// <param name="customerId">ID kontrahenta</param>
        Task<string> GetSalesmanForCustomerAsync(int customerId);

        /// <summary>
        /// Pobiera nazwę produktu po ID
        /// </summary>
        /// <param name="productId">ID produktu</param>
        Task<string> GetProductNameAsync(int productId);

        /// <summary>
        /// Pobiera słownik użytkowników (ID -> Nazwa)
        /// </summary>
        Task<IDictionary<string, string>> GetUsersAsync();

        /// <summary>
        /// Czyści cały cache
        /// </summary>
        void ClearAll();

        /// <summary>
        /// Czyści cache dla konkretnej daty (wydania, przychody)
        /// </summary>
        /// <param name="date">Data</param>
        void ClearForDate(DateTime date);

        /// <summary>
        /// Czyści cache kontrahentów
        /// </summary>
        void ClearCustomers();

        /// <summary>
        /// Czyści cache produktów
        /// </summary>
        void ClearProducts();

        /// <summary>
        /// Czas ostatniego odświeżenia cache kontrahentów
        /// </summary>
        DateTime? CustomersLastRefresh { get; }

        /// <summary>
        /// Czas ostatniego odświeżenia cache produktów
        /// </summary>
        DateTime? ProductsLastRefresh { get; }
    }
}
