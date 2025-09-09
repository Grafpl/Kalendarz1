using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Transport
{
    internal sealed class TransportRepository
    {
        private readonly string _conn;
        public TransportRepository(string connLibra) { _conn = connLibra; }
        // ================== Clients ==================
        /// <summary>
        /// Zwraca s³ownik: ID kontrahenta -> Shortcut (skrót) z tabeli STContractors
        /// </summary>
        internal async Task<Dictionary<int, string>> GetClientNamesAsync(IEnumerable<int> ids)
        {
            var dict = new Dictionary<int, string>();
            var idList = ids?.ToList() ?? new List<int>();
            if (idList.Count == 0) return dict;
            using (var cn = new Microsoft.Data.SqlClient.SqlConnection(_conn))
            {
                await cn.OpenAsync();
                var paramNames = idList.Select((id, i) => "@id" + i).ToList();
                var cmd = cn.CreateCommand();
                for (int i = 0; i < idList.Count; i++)
                    cmd.Parameters.AddWithValue(paramNames[i], idList[i]);
                cmd.CommandText = $@"SELECT Id, Shortcut FROM [HANDEL].[SSCommon].[STContractors] WHERE Id IN ({string.Join(",", paramNames)})";
                using (var rd = await cmd.ExecuteReaderAsync())
                {
                    while (await rd.ReadAsync())
                    {
                        int id = rd.GetInt32(0);
                        string shortcut = rd.IsDBNull(1) ? string.Empty : rd.GetString(1);
                        dict[id] = shortcut;
                    }
                }
            }
            return dict;
        }
        // ================== Drivers ==================
        public async Task<DataTable> GetDrivers2Async(bool includeInactive = false)
            => throw new NotImplementedException();
        // ================== Vehicles ==================
        public async Task<DataTable> GetVehicles2Async(int? kind = null, bool includeInactive = false)
            => throw new NotImplementedException();
        // ================== Trips ==================
        public async Task<DataTable> GetTripsByDateAsync(DateTime date)
            => throw new NotImplementedException();
        public async Task<long> AddTripAsync(DateTime date, int driverId, int vehicleId, string? route, TimeSpan? dep, string user, DateTime? plannedDepartureDt = null, int? trailerVehicleId = null)
            => throw new NotImplementedException();
        public async Task UpdateTripHeaderAsync(long tripId, int driverId, int vehicleId, int? trailerVehicleId, string? route, TimeSpan? plannedDeparture, string status, string user, string? notes, DateTime? plannedDepartureDt)
            => throw new NotImplementedException();
        // ================== Loads ==================
        public async Task<DataTable> GetTripLoadsAsync(long tripId)
            => throw new NotImplementedException();
        public async Task<long> AddTripLoadAsync(long tripId, string? customer, decimal meatKg, int carcass, int pallets, int e2, string? comment)
            => throw new NotImplementedException();
        public async Task<long> AddTripLoadFromOrderAsync(long tripId, int orderId, string user)
            => throw new NotImplementedException();
        public async Task UpdateTripLoadAsync(long tripLoadId, int seq, string? customer, decimal meatKg, int carcass, int pallets, int e2, string? comment)
            => throw new NotImplementedException();
        public async Task DeleteTripLoadAsync(long tripLoadId)
            => throw new NotImplementedException();
        public async Task RenumberTripLoadsAsync(long tripId)
            => throw new NotImplementedException();
        public async Task<DataTable> GetAvailableOrdersForDateAsync(DateTime date)
            => throw new NotImplementedException();
    }
}
