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
    /// Repozytorium produktów - dostęp do Handel.TW i LibraNet (konfiguracja, stany)
    /// </summary>
    public class ProductRepository : BaseRepository, IProductRepository
    {
        private readonly string _handelConnectionString;

        public ProductRepository()
            : base(DatabaseConnections.Instance.LibraNet)
        {
            _handelConnectionString = DatabaseConnections.Instance.Handel;
        }

        public ProductRepository(string libraNetConnection, string handelConnection)
            : base(libraNetConnection)
        {
            _handelConnectionString = handelConnection;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Product>> GetAllMeatProductsAsync()
        {
            var handelRepo = new BaseHandelRepository(_handelConnectionString);
            return await handelRepo.QueryAsync(
                SqlQueries.GetMeatProducts,
                null,
                reader => new Product
                {
                    Id = GetInt32(reader, "Id"),
                    Kod = GetString(reader, "Kod"),
                    Nazwa = GetString(reader, "Nazwa"),
                    Katalog = GetInt32(reader, "Katalog"),
                    JM = GetString(reader, "JM")
                });
        }

        /// <inheritdoc/>
        public async Task<Product> GetProductByIdAsync(int id)
        {
            var handelRepo = new BaseHandelRepository(_handelConnectionString);
            return await handelRepo.QuerySingleAsync(
                SqlQueries.GetProductById,
                new { Id = id },
                reader => new Product
                {
                    Id = GetInt32(reader, "Id"),
                    Kod = GetString(reader, "Kod"),
                    Nazwa = GetString(reader, "Nazwa"),
                    Katalog = GetInt32(reader, "Katalog"),
                    JM = GetString(reader, "JM")
                });
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, string>> GetProductNamesAsync(IEnumerable<int> ids)
        {
            var idList = ids?.ToList() ?? new List<int>();
            if (!idList.Any())
                return new Dictionary<int, string>();

            var handelRepo = new BaseHandelRepository(_handelConnectionString);
            var results = await handelRepo.QueryAsync(
                SqlQueries.GetProductNames,
                new { Ids = string.Join(",", idList) },
                reader => new
                {
                    Id = GetInt32(reader, "ID"),
                    Kod = GetString(reader, "Kod")
                });

            return results.ToDictionary(x => x.Id, x => x.Kod);
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, decimal>> GetActualIncomeAsync(DateTime date, IEnumerable<int> productIds)
        {
            var idList = productIds?.ToList() ?? new List<int>();
            if (!idList.Any())
                return new Dictionary<int, decimal>();

            var handelRepo = new BaseHandelRepository(_handelConnectionString);
            var results = await handelRepo.QueryAsync(
                SqlQueries.GetActualIncomePerProduct,
                new { Day = date.Date, ProductIds = string.Join(",", idList) },
                reader => new
                {
                    ProduktId = GetInt32(reader, "ProduktId"),
                    Ilosc = GetDecimal(reader, "Ilosc")
                });

            return results.ToDictionary(x => x.ProduktId, x => x.Ilosc);
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, decimal>> GetReleasesAsync(DateTime date)
        {
            var handelRepo = new BaseHandelRepository(_handelConnectionString);
            var results = await handelRepo.QueryAsync(
                SqlQueries.GetReleasesTotalPerProduct,
                new { Day = date.Date },
                reader => new
                {
                    ProduktId = GetInt32(reader, "ProduktId"),
                    Ilosc = GetDecimal(reader, "Ilosc")
                });

            return results.ToDictionary(x => x.ProduktId, x => x.Ilosc);
        }

        /// <inheritdoc/>
        public async Task<IDictionary<(int KlientId, int ProduktId), decimal>> GetReleasesPerClientAsync(DateTime date, IEnumerable<int> productIds)
        {
            var idList = productIds?.ToList() ?? new List<int>();
            if (!idList.Any())
                return new Dictionary<(int, int), decimal>();

            var handelRepo = new BaseHandelRepository(_handelConnectionString);
            var results = await handelRepo.QueryAsync(
                SqlQueries.GetReleasesPerClientProduct,
                new { Day = date.Date, ProductIds = string.Join(",", idList) },
                reader => new
                {
                    KlientId = GetInt32(reader, "KlientId"),
                    ProduktId = GetInt32(reader, "ProduktId"),
                    Ilosc = GetDecimal(reader, "Ilosc")
                });

            return results.ToDictionary(x => (x.KlientId, x.ProduktId), x => x.Ilosc);
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, decimal>> GetInventoryStocksAsync(DateTime date)
        {
            try
            {
                var results = await QueryAsync(
                    SqlQueries.GetInventoryStocks,
                    new { Day = date.Date },
                    reader => new
                    {
                        ProduktId = GetInt32(reader, "ProduktId"),
                        Stan = GetDecimal(reader, "Stan")
                    });

                return results.ToDictionary(x => x.ProduktId, x => x.Stan);
            }
            catch
            {
                // Tabela może nie istnieć - zwróć pusty słownik
                return new Dictionary<int, decimal>();
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<ProductConfiguration>> GetProductConfigurationAsync(DateTime date)
        {
            try
            {
                return await QueryAsync(
                    SqlQueries.GetProductConfiguration,
                    new { Day = date.Date },
                    reader => new ProductConfiguration
                    {
                        ProductId = GetInt32(reader, "ProduktId"),
                        ProcentUdzialu = GetDecimal(reader, "ProcentUdzialu"),
                        GrupaScalania = GetString(reader, "GrupaScalania"),
                        Kolejnosc = GetInt32(reader, "Kolejnosc")
                    });
            }
            catch
            {
                return Enumerable.Empty<ProductConfiguration>();
            }
        }

        /// <inheritdoc/>
        public async Task<YieldConfiguration> GetYieldConfigurationAsync(DateTime date)
        {
            try
            {
                return await QuerySingleAsync(
                    SqlQueries.GetYieldConfiguration,
                    new { Day = date.Date },
                    reader => new YieldConfiguration
                    {
                        Wspolczynnik = GetDecimal(reader, "Wspolczynnik"),
                        ProcentA = GetDecimal(reader, "ProcentA"),
                        ProcentB = GetDecimal(reader, "ProcentB")
                    }) ?? new YieldConfiguration { Wspolczynnik = 65, ProcentA = 45, ProcentB = 55 };
            }
            catch
            {
                // Domyślne wartości jeśli tabela nie istnieje
                return new YieldConfiguration { Wspolczynnik = 65, ProcentA = 45, ProcentB = 55 };
            }
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, string>> GetProductGroupMappingAsync()
        {
            try
            {
                var results = await QueryAsync(
                    SqlQueries.GetProductGroupMapping,
                    null,
                    reader => new
                    {
                        ProduktId = GetInt32(reader, "ProduktId"),
                        NazwaGrupy = GetString(reader, "NazwaGrupy")
                    });

                return results.ToDictionary(x => x.ProduktId, x => x.NazwaGrupy);
            }
            catch
            {
                return new Dictionary<int, string>();
            }
        }

        /// <inheritdoc/>
        public async Task<decimal> GetScheduledMassAsync(DateTime date)
        {
            try
            {
                return await ExecuteScalarAsync<decimal>(
                    SqlQueries.GetScheduledMass,
                    new { Day = date.Date });
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Wewnętrzna klasa do zapytań na bazie Handel
        /// </summary>
        private class BaseHandelRepository : BaseRepository
        {
            public BaseHandelRepository(string connectionString) : base(connectionString) { }

            public new Task<List<T>> QueryAsync<T>(string sql, object parameters, Func<IDataReader, T> mapper)
                => base.QueryAsync(sql, parameters, mapper);

            public new Task<T> QuerySingleAsync<T>(string sql, object parameters, Func<IDataReader, T> mapper)
                => base.QuerySingleAsync(sql, parameters, mapper);
        }
    }
}
