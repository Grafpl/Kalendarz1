using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZPSP.Sales.Models;

namespace ZPSP.Sales.Services.Interfaces
{
    /// <summary>
    /// Interfejs serwisu produktów.
    /// Zawiera logikę biznesową dla produktów, agregacji i bilansowania.
    /// </summary>
    public interface IProductService
    {
        /// <summary>
        /// Pobiera wszystkie produkty mięsne
        /// </summary>
        Task<IEnumerable<Product>> GetAllProductsAsync();

        /// <summary>
        /// Pobiera agregacje produktów na dany dzień (plan, fakt, zamówienia, wydania, bilans)
        /// </summary>
        /// <param name="date">Data uboju</param>
        /// <param name="useReleases">Czy używać wydań (true) czy zamówień (false) do bilansu</param>
        Task<IEnumerable<ProductAggregation>> GetProductAggregationsAsync(DateTime date, bool useReleases = false);

        /// <summary>
        /// Pobiera agregację dla konkretnego produktu
        /// </summary>
        Task<ProductAggregation> GetProductAggregationAsync(int productId, DateTime date, bool useReleases = false);

        /// <summary>
        /// Pobiera konfigurację produktów na dany dzień
        /// </summary>
        Task<IEnumerable<ProductConfiguration>> GetProductConfigurationAsync(DateTime date);

        /// <summary>
        /// Pobiera konfigurację wydajności na dany dzień
        /// </summary>
        Task<YieldConfiguration> GetYieldConfigurationAsync(DateTime date);

        /// <summary>
        /// Oblicza bilans produktu
        /// </summary>
        /// <param name="productId">ID produktu</param>
        /// <param name="date">Data</param>
        /// <param name="useReleases">Czy używać wydań do bilansu</param>
        Task<decimal> CalculateProductBalanceAsync(int productId, DateTime date, bool useReleases = false);

        /// <summary>
        /// Pobiera potencjalnych klientów dla produktu (którzy nie zamówili ale kupowali wcześniej)
        /// </summary>
        Task<IEnumerable<Customer>> GetPotentialCustomersForProductAsync(int productId, DateTime date);

        /// <summary>
        /// Pobiera mapowanie scalania produktów w grupy
        /// </summary>
        Task<IDictionary<int, string>> GetProductGroupMappingAsync();

        /// <summary>
        /// Pobiera sumę po grupach scalania
        /// </summary>
        Task<IDictionary<string, ProductAggregation>> GetGroupedAggregationsAsync(DateTime date, bool useReleases = false);
    }
}
