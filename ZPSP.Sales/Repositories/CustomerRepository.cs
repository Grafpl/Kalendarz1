using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using ZPSP.Sales.Infrastructure;
using ZPSP.Sales.Models;
using ZPSP.Sales.Repositories.Interfaces;
using ZPSP.Sales.SQL;

namespace ZPSP.Sales.Repositories
{
    /// <summary>
    /// Repozytorium kontrahentów - dostęp do Handel.STContractors
    /// </summary>
    public class CustomerRepository : BaseRepository, ICustomerRepository
    {
        public CustomerRepository() : base(DatabaseConnections.Instance.Handel)
        {
        }

        public CustomerRepository(string connectionString) : base(connectionString)
        {
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Customer>> GetAllCustomersAsync()
        {
            return await QueryAsync(
                SqlQueries.GetAllCustomers,
                null,
                MapCustomer);
        }

        /// <inheritdoc/>
        public async Task<Customer> GetCustomerByIdAsync(int id)
        {
            return await QuerySingleAsync(
                SqlQueries.GetCustomerById,
                new { Id = id },
                MapCustomer);
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, (string Shortcut, string Handlowiec)>> GetCustomersLookupAsync()
        {
            var results = await QueryAsync(
                SqlQueries.GetCustomersLookup,
                null,
                reader => new
                {
                    Id = GetInt32(reader, "Id"),
                    Shortcut = GetString(reader, "Shortcut"),
                    Handlowiec = GetString(reader, "Handlowiec")
                });

            return results.ToDictionary(
                x => x.Id,
                x => (x.Shortcut ?? "", x.Handlowiec ?? ""));
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, Customer>> GetCustomersByIdsAsync(IEnumerable<int> ids)
        {
            var idList = ids?.ToList() ?? new List<int>();
            if (!idList.Any())
                return new Dictionary<int, Customer>();

            var sql = $@"
                SELECT
                    c.Id,
                    c.Shortcut,
                    c.Name1 AS Nazwa,
                    c.NIP,
                    c.Street AS Adres,
                    c.City AS Miasto,
                    c.PostalCode AS KodPocztowy,
                    c.Phone AS Telefon,
                    c.Email,
                    wym.CDim_Handlowiec_Val AS Handlowiec
                FROM [HANDEL].[SSCommon].[STContractors] c
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId
                WHERE c.Id IN ({string.Join(",", idList)})";

            var customers = await QueryAsync(sql, null, MapCustomer);
            return customers.ToDictionary(c => c.Id, c => c);
        }

        #region Private Mapping Methods

        private Customer MapCustomer(IDataReader reader)
        {
            return new Customer
            {
                Id = GetInt32(reader, "Id"),
                Shortcut = GetString(reader, "Shortcut"),
                Nazwa = GetString(reader, "Nazwa"),
                NIP = GetString(reader, "NIP"),
                Adres = GetString(reader, "Adres"),
                Miasto = GetString(reader, "Miasto"),
                KodPocztowy = GetString(reader, "KodPocztowy"),
                Telefon = GetString(reader, "Telefon"),
                Email = GetString(reader, "Email"),
                Handlowiec = GetString(reader, "Handlowiec")
            };
        }

        #endregion
    }
}
