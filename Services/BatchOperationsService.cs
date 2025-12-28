using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Serwis do wykonywania operacji batch na bazie danych
    /// </summary>
    public class BatchOperationsService
    {
        private readonly string _connectionString;

        public BatchOperationsService(string connectionString)
        {
            _connectionString = connectionString;
        }

        #region Batch Update

        /// <summary>
        /// Aktualizuje status wielu zamówień jednocześnie
        /// </summary>
        public async Task<int> BatchUpdateStatusAsync(IEnumerable<int> orderIds, string newStatus, string user)
        {
            var ids = orderIds.ToList();
            if (!ids.Any()) return 0;

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Użyj stored procedure jeśli istnieje
                var idsJson = JsonSerializer.Serialize(ids);

                await using var cmd = new SqlCommand("sp_BatchUpdateZamowieniaStatus", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@IdsJson", idsJson);
                cmd.Parameters.AddWithValue("@NowyStatus", newStatus);
                cmd.Parameters.AddWithValue("@Uzytkownik", user);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (SqlException ex) when (ex.Message.Contains("Could not find stored procedure"))
            {
                // Fallback: wykonaj UPDATE bezpośrednio
                return await BatchUpdateStatusDirectAsync(ids, newStatus, user);
            }
        }

        private async Task<int> BatchUpdateStatusDirectAsync(List<int> orderIds, string newStatus, string user)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = connection.BeginTransaction();

            try
            {
                int updated = 0;

                // Podziel na partie po 100
                var batches = orderIds
                    .Select((id, index) => new { id, index })
                    .GroupBy(x => x.index / 100)
                    .Select(g => g.Select(x => x.id).ToList());

                foreach (var batch in batches)
                {
                    var parameters = batch.Select((id, i) => $"@Id{i}").ToList();
                    var sql = $@"
                        UPDATE [dbo].[ZamowieniaMieso]
                        SET Status = @Status,
                            DataAnulowania = CASE WHEN @Status = 'Anulowane' THEN GETDATE() ELSE DataAnulowania END,
                            AnulowanePrzez = CASE WHEN @Status = 'Anulowane' THEN @User ELSE AnulowanePrzez END
                        WHERE Id IN ({string.Join(",", parameters)})";

                    await using var cmd = new SqlCommand(sql, connection, transaction);
                    cmd.Parameters.AddWithValue("@Status", newStatus);
                    cmd.Parameters.AddWithValue("@User", user);

                    for (int i = 0; i < batch.Count; i++)
                    {
                        cmd.Parameters.AddWithValue($"@Id{i}", batch[i]);
                    }

                    updated += await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return updated;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Batch Insert

        /// <summary>
        /// Wstawia wiele rekordów historii zmian jednocześnie
        /// </summary>
        public async Task<int> BatchInsertHistoryAsync(IEnumerable<HistoryEntry> entries)
        {
            var list = entries.ToList();
            if (!list.Any()) return 0;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Użyj Table-Valued Parameter lub batch insert
            var sql = @"
                INSERT INTO [dbo].[ZamowieniaMiesoHistoria]
                    (ZamowienieId, DataZmiany, TypZmiany, Uzytkownik, OpisZmiany, StaraWartosc, NowaWartosc, Pole)
                VALUES
                    (@ZamowienieId, @DataZmiany, @TypZmiany, @Uzytkownik, @OpisZmiany, @StaraWartosc, @NowaWartosc, @Pole)";

            int inserted = 0;
            foreach (var entry in list)
            {
                await using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@ZamowienieId", entry.OrderId);
                cmd.Parameters.AddWithValue("@DataZmiany", entry.ChangeDate);
                cmd.Parameters.AddWithValue("@TypZmiany", entry.ChangeType);
                cmd.Parameters.AddWithValue("@Uzytkownik", entry.User);
                cmd.Parameters.AddWithValue("@OpisZmiany", entry.Description);
                cmd.Parameters.AddWithValue("@StaraWartosc", (object?)entry.OldValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NowaWartosc", (object?)entry.NewValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Pole", (object?)entry.FieldName ?? DBNull.Value);

                inserted += await cmd.ExecuteNonQueryAsync();
            }

            return inserted;
        }

        /// <summary>
        /// Bulk insert używając SqlBulkCopy (najszybsza metoda)
        /// </summary>
        public async Task BulkInsertAsync<T>(string tableName, IEnumerable<T> data,
            Action<DataTable> configureColumns, Action<T, DataRow> fillRow)
        {
            var list = data.ToList();
            if (!list.Any()) return;

            // Utwórz DataTable i skonfiguruj kolumny
            var dt = new DataTable();
            configureColumns(dt);

            foreach (var item in list)
            {
                var row = dt.NewRow();
                fillRow(item, row);
                dt.Rows.Add(row);
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var bulkCopy = new SqlBulkCopy(connection)
            {
                DestinationTableName = tableName,
                BatchSize = 1000,
                BulkCopyTimeout = 60
            };

            await bulkCopy.WriteToServerAsync(dt);
        }

        #endregion

        #region Batch Delete

        /// <summary>
        /// Usuwa wiele rekordów jednocześnie
        /// </summary>
        public async Task<int> BatchDeleteAsync(string tableName, string idColumn, IEnumerable<int> ids)
        {
            var list = ids.ToList();
            if (!list.Any()) return 0;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            int deleted = 0;

            // Podziel na partie po 100
            var batches = list
                .Select((id, index) => new { id, index })
                .GroupBy(x => x.index / 100)
                .Select(g => g.Select(x => x.id).ToList());

            foreach (var batch in batches)
            {
                var parameters = batch.Select((id, i) => $"@Id{i}").ToList();
                var sql = $"DELETE FROM [{tableName}] WHERE [{idColumn}] IN ({string.Join(",", parameters)})";

                await using var cmd = new SqlCommand(sql, connection);
                for (int i = 0; i < batch.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@Id{i}", batch[i]);
                }

                deleted += await cmd.ExecuteNonQueryAsync();
            }

            return deleted;
        }

        #endregion

        #region Progress Reporting

        public class BatchProgress
        {
            public int Total { get; set; }
            public int Processed { get; set; }
            public double Percent => Total > 0 ? (double)Processed / Total * 100 : 0;
            public string Message { get; set; } = "";
        }

        /// <summary>
        /// Wykonuje operację batch z raportowaniem postępu
        /// </summary>
        public async Task ExecuteWithProgressAsync<T>(
            IEnumerable<T> items,
            Func<T, Task> operation,
            IProgress<BatchProgress>? progress = null,
            int batchSize = 10)
        {
            var list = items.ToList();
            var total = list.Count;
            var processed = 0;

            foreach (var item in list)
            {
                await operation(item);
                processed++;

                if (processed % batchSize == 0 || processed == total)
                {
                    progress?.Report(new BatchProgress
                    {
                        Total = total,
                        Processed = processed,
                        Message = $"Przetworzono {processed} z {total}"
                    });
                }
            }
        }

        #endregion
    }

    #region Models

    public class HistoryEntry
    {
        public int OrderId { get; set; }
        public DateTime ChangeDate { get; set; } = DateTime.Now;
        public string ChangeType { get; set; } = "";
        public string User { get; set; } = "";
        public string Description { get; set; } = "";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? FieldName { get; set; }
    }

    #endregion
}
