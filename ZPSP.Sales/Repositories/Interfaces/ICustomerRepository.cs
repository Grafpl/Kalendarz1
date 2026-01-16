using System.Collections.Generic;
using System.Threading.Tasks;
using ZPSP.Sales.Models;

namespace ZPSP.Sales.Repositories.Interfaces
{
    /// <summary>
    /// Interfejs repozytorium kontrahentów (Handel.STContractors)
    /// </summary>
    public interface ICustomerRepository
    {
        /// <summary>
        /// Pobiera wszystkich kontrahentów
        /// </summary>
        Task<IEnumerable<Customer>> GetAllCustomersAsync();

        /// <summary>
        /// Pobiera kontrahenta po ID
        /// </summary>
        Task<Customer> GetCustomerByIdAsync(int id);

        /// <summary>
        /// Pobiera słownik kontrahentów (ID -> (Shortcut, Handlowiec))
        /// Optymalizacja dla cache
        /// </summary>
        Task<IDictionary<int, (string Shortcut, string Handlowiec)>> GetCustomersLookupAsync();

        /// <summary>
        /// Pobiera kontrahentów dla listy ID
        /// </summary>
        Task<IDictionary<int, Customer>> GetCustomersByIdsAsync(IEnumerable<int> ids);
    }
}
