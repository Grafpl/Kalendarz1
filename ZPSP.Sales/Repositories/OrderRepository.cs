using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using ZPSP.Sales.Infrastructure;
using ZPSP.Sales.Models;
using ZPSP.Sales.Repositories.Interfaces;
using ZPSP.Sales.SQL;

namespace ZPSP.Sales.Repositories
{
    /// <summary>
    /// Repozytorium zamówień - dostęp do LibraNet.ZamowieniaMieso
    /// </summary>
    public class OrderRepository : BaseRepository, IOrderRepository
    {
        public OrderRepository() : base(DatabaseConnections.Instance.LibraNet)
        {
        }

        public OrderRepository(string connectionString) : base(connectionString)
        {
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Order>> GetOrdersForDateAsync(DateTime date, bool includeCancelled = true)
        {
            var orders = await QueryAsync(
                SqlQueries.GetOrdersForDate,
                new { Day = date.Date },
                MapOrder);

            if (!includeCancelled)
            {
                orders = orders.Where(o => o.Status != "Anulowane").ToList();
            }

            return orders;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Order>> GetOrdersForDateWithProductAsync(DateTime date, int productId)
        {
            return await QueryAsync(
                SqlQueries.GetOrdersForDateWithProduct,
                new { Day = date.Date, ProductId = productId },
                MapOrder);
        }

        /// <inheritdoc/>
        public async Task<Order> GetOrderByIdAsync(int id)
        {
            var sql = SqlQueries.GetOrdersForDate.Replace(
                "WHERE zm.DataUboju = @Day",
                "WHERE zm.Id = @Id");

            return await QuerySingleAsync(sql, new { Id = id }, MapOrder);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<OrderItem>> GetOrderItemsAsync(int orderId)
        {
            return await QueryAsync(
                SqlQueries.GetOrderItems,
                new { OrderId = orderId },
                MapOrderItem);
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, List<OrderItem>>> GetOrderItemsBatchAsync(IEnumerable<int> orderIds)
        {
            var idList = orderIds?.ToList() ?? new List<int>();
            if (!idList.Any())
                return new Dictionary<int, List<OrderItem>>();

            var items = await QueryAsync(
                SqlQueries.GetOrderItemsBatch,
                new { OrderIds = string.Join(",", idList) },
                reader => new OrderItem
                {
                    ZamowienieId = GetInt32(reader, "ZamowienieId"),
                    KodTowaru = GetInt32(reader, "KodTowaru"),
                    Ilosc = GetDecimal(reader, "Ilosc")
                });

            return items
                .GroupBy(i => i.ZamowienieId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, (decimal Suma, int LiczbaKlientow)>> GetOrderSummaryPerProductAsync(DateTime date)
        {
            var results = await QueryAsync(
                SqlQueries.GetOrderSummaryPerProduct,
                new { Day = date.Date },
                reader => new
                {
                    ProduktId = GetInt32(reader, "ProduktId"),
                    Suma = GetDecimal(reader, "SumaZamowien"),
                    LiczbaKlientow = GetInt32(reader, "LiczbaKlientow")
                });

            return results.ToDictionary(
                x => x.ProduktId,
                x => (x.Suma, x.LiczbaKlientow));
        }

        /// <inheritdoc/>
        public async Task<DashboardData> GetDashboardSummaryAsync(DateTime date)
        {
            var data = await QuerySingleAsync(
                SqlQueries.GetDashboardSummary,
                new { Day = date.Date },
                reader => new DashboardData
                {
                    Data = date.Date,
                    SumaZamowien = GetDecimal(reader, "SumaZamowien"),
                    LiczbaZamowien = GetInt32(reader, "LiczbaZamowien"),
                    LiczbaKlientow = GetInt32(reader, "LiczbaKlientow"),
                    SumaPalet = GetDecimal(reader, "SumaPalet"),
                    LiczbaAnulowanych = GetInt32(reader, "LiczbaAnulowanych")
                });

            return data ?? new DashboardData { Data = date.Date };
        }

        /// <inheritdoc/>
        public async Task<int> CreateOrderAsync(Order order, string userId)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var transaction = connection.BeginTransaction();

            try
            {
                // Pobierz nowy ID
                var newId = await ExecuteScalarAsync<int>(
                    "SELECT ISNULL(MAX(Id),0)+1 FROM dbo.ZamowieniaMieso");

                // Wstaw zamówienie
                var insertSql = @"
                    INSERT INTO dbo.ZamowieniaMieso
                    (Id, DataZamowienia, DataPrzyjazdu, KlientId, Uwagi, IdUser, DataUtworzenia,
                     LiczbaPojemnikow, LiczbaPalet, TrybE2, TransportStatus, DataUboju, Waluta)
                    VALUES
                    (@Id, @DataZamowienia, @DataPrzyjazdu, @KlientId, @Uwagi, @UserId, GETDATE(),
                     @Pojemniki, @Palety, @TrybE2, 'Oczekuje', @DataUboju, @Waluta)";

                await ExecuteInTransactionAsync(connection, transaction, insertSql, new
                {
                    Id = newId,
                    DataZamowienia = order.DataUboju ?? DateTime.Today,
                    DataPrzyjazdu = order.DataPrzyjecia,
                    KlientId = order.KlientId,
                    Uwagi = string.IsNullOrEmpty(order.Uwagi) ? (object)DBNull.Value : order.Uwagi,
                    UserId = userId,
                    Pojemniki = order.Pojemniki,
                    Palety = order.Palety,
                    TrybE2 = order.TrybE2 == "E2 (40)",
                    DataUboju = order.DataUboju ?? DateTime.Today,
                    Waluta = order.Waluta ?? "PLN"
                });

                // Wstaw pozycje
                foreach (var item in order.Pozycje)
                {
                    var itemSql = @"
                        INSERT INTO dbo.ZamowieniaMiesoTowar
                        (ZamowienieId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2, Folia, Hallal)
                        VALUES
                        (@ZamowienieId, @KodTowaru, @Ilosc, @Cena, @Pojemniki, @Palety, @E2, @Folia, @Hallal)";

                    await ExecuteInTransactionAsync(connection, transaction, itemSql, new
                    {
                        ZamowienieId = newId,
                        KodTowaru = item.KodTowaru,
                        Ilosc = item.Ilosc,
                        Cena = item.Cena ?? "",
                        Pojemniki = item.Pojemniki,
                        Palety = item.Palety,
                        E2 = item.E2,
                        Folia = item.Folia,
                        Hallal = item.Hallal
                    });
                }

                await transaction.CommitAsync();
                return newId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task UpdateOrderNotesAsync(int orderId, string notes)
        {
            await ExecuteAsync(SqlQueries.UpdateOrderNotes, new { Id = orderId, Uwagi = notes ?? "" });
        }

        /// <inheritdoc/>
        public async Task CancelOrderAsync(int orderId, string userId, string reason)
        {
            await ExecuteAsync(SqlQueries.CancelOrder, new
            {
                Id = orderId,
                AnulowanePrzez = userId,
                PrzyczynaAnulowania = reason
            });
        }

        /// <inheritdoc/>
        public async Task RestoreOrderAsync(int orderId)
        {
            await ExecuteAsync(SqlQueries.RestoreOrder, new { Id = orderId });
        }

        /// <inheritdoc/>
        public async Task<int> DuplicateOrderAsync(int sourceOrderId, DateTime targetDate, bool copyNotes = false)
        {
            var sourceOrder = await GetOrderByIdAsync(sourceOrderId);
            if (sourceOrder == null)
                throw new InvalidOperationException($"Zamówienie o ID {sourceOrderId} nie istnieje");

            var items = await GetOrderItemsAsync(sourceOrderId);

            var newOrder = new Order
            {
                KlientId = sourceOrder.KlientId,
                DataUboju = targetDate.Date,
                DataPrzyjecia = targetDate.Date.Add(sourceOrder.DataPrzyjecia.TimeOfDay),
                Pojemniki = sourceOrder.Pojemniki,
                Palety = sourceOrder.Palety,
                TrybE2 = sourceOrder.TrybE2,
                Uwagi = copyNotes ? sourceOrder.Uwagi : null,
                Waluta = sourceOrder.Waluta,
                Pozycje = items.ToList()
            };

            return await CreateOrderAsync(newOrder, sourceOrder.UtworzonePrzez?.Split(' ').FirstOrDefault() ?? "SYSTEM");
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<int>> GetClientIdsWithOrdersAsync(DateTime date)
        {
            return await QueryAsync(
                SqlQueries.GetClientIdsWithOrders,
                new { Day = date.Date },
                reader => GetInt32(reader, "KlientId"));
        }

        /// <inheritdoc/>
        public async Task<IDictionary<int, decimal>> GetAveragePricesForOrdersAsync(IEnumerable<int> orderIds)
        {
            var idList = orderIds?.ToList() ?? new List<int>();
            if (!idList.Any())
                return new Dictionary<int, decimal>();

            var results = await QueryAsync(
                SqlQueries.GetAveragePricesForOrders,
                new { OrderIds = string.Join(",", idList) },
                reader => new
                {
                    ZamowienieId = GetInt32(reader, "ZamowienieId"),
                    SredniaCena = GetDecimal(reader, "SredniaCena")
                });

            return results.ToDictionary(x => x.ZamowienieId, x => x.SredniaCena);
        }

        #region Private Mapping Methods

        private Order MapOrder(IDataReader reader)
        {
            return new Order
            {
                Id = GetInt32(reader, "Id"),
                KlientId = GetInt32(reader, "KlientId"),
                IloscZamowiona = GetDecimal(reader, "Ilosc"),
                DataPrzyjecia = GetDateTime(reader, "DataPrzyjazdu"),
                GodzinaPrzyjecia = GetDateTime(reader, "DataPrzyjazdu").ToString("HH:mm"),
                DataUboju = GetNullableDateTime(reader, "DataUboju"),
                Status = GetString(reader, "Status") ?? "Nowe",
                Pojemniki = GetInt32(reader, "LiczbaPojemnikow"),
                Palety = GetDecimal(reader, "LiczbaPalet"),
                TrybE2 = GetBoolean(reader, "TrybE2") ? "E2 (40)" : "STD (36)",
                Uwagi = GetString(reader, "Uwagi"),
                MaNotatke = !string.IsNullOrEmpty(GetString(reader, "Uwagi")),
                TransportKursId = GetValue<long?>(reader, "TransportKursID"),
                MaFolie = GetBoolean(reader, "MaFolie"),
                MaHallal = GetBoolean(reader, "MaHallal"),
                CzyMaCeny = GetBoolean(reader, "CzyMaCeny"),
                CzyZrealizowane = GetBoolean(reader, "CzyZrealizowane"),
                DataWydania = GetNullableDateTime(reader, "DataWydania"),
                Waluta = GetString(reader, "Waluta") ?? "PLN"
            };
        }

        private OrderItem MapOrderItem(IDataReader reader)
        {
            return new OrderItem
            {
                ZamowienieId = GetInt32(reader, "ZamowienieId"),
                KodTowaru = GetInt32(reader, "KodTowaru"),
                Ilosc = GetDecimal(reader, "Ilosc"),
                Cena = GetString(reader, "Cena"),
                Pojemniki = GetInt32(reader, "Pojemniki"),
                Palety = GetDecimal(reader, "Palety"),
                E2 = GetBoolean(reader, "E2"),
                Folia = GetBoolean(reader, "Folia"),
                Hallal = GetBoolean(reader, "Hallal")
            };
        }

        #endregion
    }
}
