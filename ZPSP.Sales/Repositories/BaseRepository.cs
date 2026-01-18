using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using ZPSP.Sales.Infrastructure;

namespace ZPSP.Sales.Repositories
{
    /// <summary>
    /// Bazowa klasa repozytoriów z obsługą połączeń i prostym Dapper-like mapowaniem.
    /// </summary>
    public abstract class BaseRepository
    {
        protected readonly string _connectionString;

        protected BaseRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Tworzy nowe połączenie do bazy danych
        /// </summary>
        protected SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        /// <summary>
        /// Wykonuje zapytanie i zwraca listę obiektów (prosty mapping bez Dapper)
        /// </summary>
        /// <typeparam name="T">Typ encji</typeparam>
        /// <param name="sql">Zapytanie SQL</param>
        /// <param name="parameters">Parametry zapytania</param>
        /// <param name="mapper">Funkcja mapująca IDataReader na obiekt</param>
        protected async Task<List<T>> QueryAsync<T>(string sql, object parameters, Func<IDataReader, T> mapper)
        {
            var results = new List<T>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = CreateCommand(connection, sql, parameters);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                results.Add(mapper(reader));
            }

            return results;
        }

        /// <summary>
        /// Wykonuje zapytanie i zwraca pojedynczy obiekt
        /// </summary>
        protected async Task<T> QuerySingleAsync<T>(string sql, object parameters, Func<IDataReader, T> mapper)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = CreateCommand(connection, sql, parameters);
            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return mapper(reader);
            }

            return default;
        }

        /// <summary>
        /// Wykonuje zapytanie i zwraca skalar
        /// </summary>
        protected async Task<T> ExecuteScalarAsync<T>(string sql, object parameters = null)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = CreateCommand(connection, sql, parameters);
            var result = await command.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
                return default;

            return (T)Convert.ChangeType(result, typeof(T));
        }

        /// <summary>
        /// Wykonuje zapytanie i zwraca liczbę zmodyfikowanych wierszy
        /// </summary>
        protected async Task<int> ExecuteAsync(string sql, object parameters = null)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = CreateCommand(connection, sql, parameters);
            return await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Wykonuje zapytanie w transakcji
        /// </summary>
        protected async Task<int> ExecuteInTransactionAsync(SqlConnection connection, SqlTransaction transaction, string sql, object parameters = null)
        {
            await using var command = CreateCommand(connection, sql, parameters, transaction);
            return await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Tworzy komendę z parametrami
        /// </summary>
        private SqlCommand CreateCommand(SqlConnection connection, string sql, object parameters, SqlTransaction transaction = null)
        {
            var command = new SqlCommand(sql, connection)
            {
                CommandTimeout = 30
            };

            if (transaction != null)
                command.Transaction = transaction;

            if (parameters != null)
            {
                // Prosty mapping obiektów anonimowych lub Dictionary na parametry SQL
                if (parameters is IDictionary<string, object> dict)
                {
                    foreach (var kvp in dict)
                    {
                        command.Parameters.AddWithValue("@" + kvp.Key, kvp.Value ?? DBNull.Value);
                    }
                }
                else
                {
                    // Użyj refleksji dla obiektów anonimowych
                    foreach (var prop in parameters.GetType().GetProperties())
                    {
                        var value = prop.GetValue(parameters);
                        command.Parameters.AddWithValue("@" + prop.Name, value ?? DBNull.Value);
                    }
                }
            }

            return command;
        }

        #region Helper Methods

        /// <summary>
        /// Bezpieczne pobieranie wartości nullable z IDataReader
        /// </summary>
        protected static T GetValue<T>(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return default;

            var value = reader.GetValue(ordinal);
            if (value is T typedValue)
                return typedValue;

            return (T)Convert.ChangeType(value, typeof(T));
        }

        /// <summary>
        /// Bezpieczne pobieranie string z IDataReader
        /// </summary>
        protected static string GetString(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        /// <summary>
        /// Bezpieczne pobieranie int z IDataReader
        /// </summary>
        protected static int GetInt32(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
        }

        /// <summary>
        /// Bezpieczne pobieranie long z IDataReader
        /// </summary>
        protected static long GetInt64(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetInt64(ordinal);
        }

        /// <summary>
        /// Bezpieczne pobieranie decimal z IDataReader
        /// </summary>
        protected static decimal GetDecimal(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return 0;

            var value = reader.GetValue(ordinal);
            return Convert.ToDecimal(value);
        }

        /// <summary>
        /// Bezpieczne pobieranie bool z IDataReader
        /// </summary>
        protected static bool GetBoolean(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return false;

            var value = reader.GetValue(ordinal);
            if (value is bool b)
                return b;

            return Convert.ToBoolean(value);
        }

        /// <summary>
        /// Bezpieczne pobieranie DateTime z IDataReader
        /// </summary>
        protected static DateTime GetDateTime(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? DateTime.MinValue : reader.GetDateTime(ordinal);
        }

        /// <summary>
        /// Bezpieczne pobieranie DateTime? z IDataReader
        /// </summary>
        protected static DateTime? GetNullableDateTime(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }

        /// <summary>
        /// Bezpieczne pobieranie TimeSpan? z IDataReader
        /// </summary>
        protected static TimeSpan? GetNullableTimeSpan(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return null;

            var value = reader.GetValue(ordinal);
            if (value is TimeSpan ts)
                return ts;

            return null;
        }

        #endregion
    }
}
