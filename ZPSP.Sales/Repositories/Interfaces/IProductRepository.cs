using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZPSP.Sales.Models;

namespace ZPSP.Sales.Repositories.Interfaces
{
    /// <summary>
    /// Interfejs repozytorium produktów (Handel.TW)
    /// </summary>
    public interface IProductRepository
    {
        /// <summary>
        /// Pobiera wszystkie produkty mięsne (katalogi 67095 i 67153)
        /// </summary>
        Task<IEnumerable<Product>> GetAllMeatProductsAsync();

        /// <summary>
        /// Pobiera produkt po ID
        /// </summary>
        Task<Product> GetProductByIdAsync(int id);

        /// <summary>
        /// Pobiera nazwy produktów dla listy ID
        /// </summary>
        Task<IDictionary<int, string>> GetProductNamesAsync(IEnumerable<int> ids);

        /// <summary>
        /// Pobiera faktyczny przychód (PWP) per produkt na dany dzień
        /// </summary>
        Task<IDictionary<int, decimal>> GetActualIncomeAsync(DateTime date, IEnumerable<int> productIds);

        /// <summary>
        /// Pobiera wydania (WZ) per produkt na dany dzień
        /// </summary>
        Task<IDictionary<int, decimal>> GetReleasesAsync(DateTime date);

        /// <summary>
        /// Pobiera wydania per klient i produkt na dany dzień
        /// </summary>
        Task<IDictionary<(int KlientId, int ProduktId), decimal>> GetReleasesPerClientAsync(DateTime date, IEnumerable<int> productIds);

        /// <summary>
        /// Pobiera stany magazynowe na dany dzień
        /// </summary>
        Task<IDictionary<int, decimal>> GetInventoryStocksAsync(DateTime date);

        /// <summary>
        /// Pobiera konfigurację produktów na dany dzień
        /// </summary>
        Task<IEnumerable<ProductConfiguration>> GetProductConfigurationAsync(DateTime date);

        /// <summary>
        /// Pobiera konfigurację wydajności na dany dzień
        /// </summary>
        Task<YieldConfiguration> GetYieldConfigurationAsync(DateTime date);

        /// <summary>
        /// Pobiera mapowanie scalania produktów w grupy
        /// </summary>
        Task<IDictionary<int, string>> GetProductGroupMappingAsync();

        /// <summary>
        /// Pobiera masę z harmonogramu dostaw na dany dzień
        /// </summary>
        Task<decimal> GetScheduledMassAsync(DateTime date);
    }
}
