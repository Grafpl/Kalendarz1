using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Kalendarz1.Zywiec.Kalendarz.Services
{
    /// <summary>
    /// Typ operacji w systemie audytu
    /// </summary>
    public enum AuditOperationType
    {
        INSERT,
        UPDATE,
        DELETE
    }

    /// <summary>
    /// Źródło zmiany - jak została wykonana modyfikacja
    /// </summary>
    public enum AuditChangeSource
    {
        // Dwuklik na komórkach
        DoubleClick_Auta,
        DoubleClick_Sztuki,
        DoubleClick_Waga,
        DoubleClick_Uwagi,
        DoubleClick_Cena,

        // Checkboxy
        Checkbox_Potwierdzenie,
        Checkbox_Wstawienie,
        Checkbox_PotwSztuki,
        Checkbox_PotwWaga,

        // Przyciski nawigacji dat
        Button_DataUp,
        Button_DataDown,

        // Drag & Drop
        DragDrop,

        // Formularze
        Form_Zapisz,
        Form_DodajNotatke,
        Form_NowaPartia,

        // Szybka notatka
        QuickNote,

        // Przyciski akcji
        Button_Duplikuj,
        Button_Usun,
        Button_Nowa,

        // Menu kontekstowe
        ContextMenu_Potwierdz,
        ContextMenu_Anuluj,
        ContextMenu_Usun,

        // Operacje masowe
        BulkConfirm,
        BulkCancel,
        BulkDateChange,

        // Inne
        Import,
        AutoSave,
        SystemCorrection
    }

    /// <summary>
    /// Model wpisu audytu
    /// </summary>
    public class AuditLogEntry
    {
        public long AuditID { get; set; }
        public DateTime DataZmiany { get; set; }
        public string UserID { get; set; }
        public string UserName { get; set; }
        public string NazwaTabeli { get; set; }
        public string RekordID { get; set; }
        public string TypOperacji { get; set; }
        public string ZrodloZmiany { get; set; }
        public string NazwaPola { get; set; }
        public string StaraWartosc { get; set; }
        public string NowaWartosc { get; set; }
        public string DodatkoweInfo { get; set; }
        public string OpisZmiany { get; set; }
        public string AdresIP { get; set; }
        public string NazwaKomputera { get; set; }
    }

    /// <summary>
    /// Dodatkowe informacje kontekstowe dla audytu
    /// </summary>
    public class AuditContextInfo
    {
        public string Dostawca { get; set; }
        public DateTime? DataOdbioru { get; set; }
        public int? IloscZaznaczonych { get; set; }
        public List<string> ZaznaczoneLPs { get; set; }
        public string DodatkowyKomentarz { get; set; }
        public string SourceTable { get; set; }
        public string RelatedLP { get; set; }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }
    }

    /// <summary>
    /// Serwis do zarządzania audytem zmian w systemie dostaw
    /// </summary>
    public class AuditLogService
    {
        private readonly string _connectionString;
        private readonly string _userId;
        private readonly string _userName;
        private static readonly string _computerName = Environment.MachineName;

        /// <summary>
        /// Konstruktor serwisu audytu
        /// </summary>
        /// <param name="connectionString">Connection string do bazy danych</param>
        /// <param name="userId">ID aktualnego użytkownika</param>
        /// <param name="userName">Nazwa aktualnego użytkownika (opcjonalna)</param>
        public AuditLogService(string connectionString, string userId, string userName = null)
        {
            _connectionString = connectionString;
            _userId = userId;
            _userName = userName;
        }

        #region Główne metody logowania

        /// <summary>
        /// Loguje zmianę pojedynczego pola
        /// </summary>
        public async Task LogFieldChangeAsync(
            string tableName,
            string recordId,
            AuditChangeSource source,
            string fieldName,
            object oldValue,
            object newValue,
            AuditContextInfo contextInfo = null,
            CancellationToken cancellationToken = default)
        {
            await LogAsync(
                tableName: tableName,
                recordId: recordId,
                operationType: AuditOperationType.UPDATE,
                source: source,
                fieldName: fieldName,
                oldValue: ConvertToString(oldValue),
                newValue: ConvertToString(newValue),
                contextInfo: contextInfo,
                description: GenerateDescription(AuditOperationType.UPDATE, fieldName, oldValue, newValue),
                cancellationToken: cancellationToken
            );
        }

        /// <summary>
        /// Loguje wstawienie nowego rekordu
        /// </summary>
        public async Task LogInsertAsync(
            string tableName,
            string recordId,
            AuditChangeSource source,
            Dictionary<string, object> newValues = null,
            AuditContextInfo contextInfo = null,
            CancellationToken cancellationToken = default)
        {
            string newValuesJson = newValues != null
                ? JsonSerializer.Serialize(newValues)
                : null;

            await LogAsync(
                tableName: tableName,
                recordId: recordId,
                operationType: AuditOperationType.INSERT,
                source: source,
                fieldName: null,
                oldValue: null,
                newValue: newValuesJson,
                contextInfo: contextInfo,
                description: $"Utworzono nowy rekord w tabeli {tableName}",
                cancellationToken: cancellationToken
            );
        }

        /// <summary>
        /// Loguje usunięcie rekordu
        /// </summary>
        public async Task LogDeleteAsync(
            string tableName,
            string recordId,
            AuditChangeSource source,
            Dictionary<string, object> oldValues = null,
            AuditContextInfo contextInfo = null,
            CancellationToken cancellationToken = default)
        {
            string oldValuesJson = oldValues != null
                ? JsonSerializer.Serialize(oldValues)
                : null;

            await LogAsync(
                tableName: tableName,
                recordId: recordId,
                operationType: AuditOperationType.DELETE,
                source: source,
                fieldName: null,
                oldValue: oldValuesJson,
                newValue: null,
                contextInfo: contextInfo,
                description: $"Usunięto rekord z tabeli {tableName}",
                cancellationToken: cancellationToken
            );
        }

        /// <summary>
        /// Loguje zmianę wielu pól naraz
        /// </summary>
        public async Task LogMultiFieldChangeAsync(
            string tableName,
            string recordId,
            AuditChangeSource source,
            Dictionary<string, (object OldValue, object NewValue)> changes,
            AuditContextInfo contextInfo = null,
            CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>();

            foreach (var change in changes)
            {
                if (!AreValuesEqual(change.Value.OldValue, change.Value.NewValue))
                {
                    tasks.Add(LogFieldChangeAsync(
                        tableName,
                        recordId,
                        source,
                        change.Key,
                        change.Value.OldValue,
                        change.Value.NewValue,
                        contextInfo,
                        cancellationToken
                    ));
                }
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Loguje operację masową (bulk)
        /// </summary>
        public async Task LogBulkOperationAsync(
            string tableName,
            List<string> recordIds,
            AuditChangeSource source,
            string fieldName,
            object newValue,
            AuditContextInfo contextInfo = null,
            CancellationToken cancellationToken = default)
        {
            if (contextInfo == null)
                contextInfo = new AuditContextInfo();

            contextInfo.IloscZaznaczonych = recordIds.Count;
            contextInfo.ZaznaczoneLPs = recordIds;

            var tasks = new List<Task>();

            foreach (var recordId in recordIds)
            {
                tasks.Add(LogFieldChangeAsync(
                    tableName,
                    recordId,
                    source,
                    fieldName,
                    null, // Nie znamy starej wartości w operacji masowej
                    newValue,
                    contextInfo,
                    cancellationToken
                ));
            }

            await Task.WhenAll(tasks);
        }

        #endregion

        #region Specyficzne metody logowania dla systemu dostaw

        /// <summary>
        /// Loguje zmianę ceny dostawy
        /// </summary>
        public async Task LogPriceChangeAsync(string lp, decimal? oldPrice, decimal? newPrice,
            AuditChangeSource source, string dostawca = null, DateTime? dataOdbioru = null,
            CancellationToken cancellationToken = default)
        {
            var context = new AuditContextInfo
            {
                Dostawca = dostawca,
                DataOdbioru = dataOdbioru
            };

            await LogFieldChangeAsync("HarmonogramDostaw", lp, source, "Cena",
                oldPrice, newPrice, context, cancellationToken);
        }

        /// <summary>
        /// Loguje zmianę wagi
        /// </summary>
        public async Task LogWeightChangeAsync(string lp, decimal? oldWeight, decimal? newWeight,
            AuditChangeSource source, string dostawca = null, DateTime? dataOdbioru = null,
            CancellationToken cancellationToken = default)
        {
            var context = new AuditContextInfo
            {
                Dostawca = dostawca,
                DataOdbioru = dataOdbioru
            };

            await LogFieldChangeAsync("HarmonogramDostaw", lp, source, "WagaDek",
                oldWeight, newWeight, context, cancellationToken);
        }

        /// <summary>
        /// Loguje zmianę sztuk
        /// </summary>
        public async Task LogQuantityChangeAsync(string lp, double? oldQty, double? newQty,
            AuditChangeSource source, string dostawca = null, DateTime? dataOdbioru = null,
            CancellationToken cancellationToken = default)
        {
            var context = new AuditContextInfo
            {
                Dostawca = dostawca,
                DataOdbioru = dataOdbioru
            };

            await LogFieldChangeAsync("HarmonogramDostaw", lp, source, "SztukiDek",
                oldQty, newQty, context, cancellationToken);
        }

        /// <summary>
        /// Loguje zmianę liczby aut
        /// </summary>
        public async Task LogVehicleCountChangeAsync(string lp, int? oldCount, int? newCount,
            AuditChangeSource source, string dostawca = null, DateTime? dataOdbioru = null,
            CancellationToken cancellationToken = default)
        {
            var context = new AuditContextInfo
            {
                Dostawca = dostawca,
                DataOdbioru = dataOdbioru
            };

            await LogFieldChangeAsync("HarmonogramDostaw", lp, source, "Auta",
                oldCount, newCount, context, cancellationToken);
        }

        /// <summary>
        /// Loguje zmianę daty odbioru
        /// </summary>
        public async Task LogDateChangeAsync(string lp, DateTime? oldDate, DateTime? newDate,
            AuditChangeSource source, string dostawca = null,
            CancellationToken cancellationToken = default)
        {
            var context = new AuditContextInfo
            {
                Dostawca = dostawca,
                DataOdbioru = newDate
            };

            await LogFieldChangeAsync("HarmonogramDostaw", lp, source, "DataOdbioru",
                oldDate?.ToString("yyyy-MM-dd"),
                newDate?.ToString("yyyy-MM-dd"),
                context, cancellationToken);
        }

        /// <summary>
        /// Loguje zmianę statusu (Bufor)
        /// </summary>
        public async Task LogStatusChangeAsync(string lp, string oldStatus, string newStatus,
            AuditChangeSource source, string dostawca = null, DateTime? dataOdbioru = null,
            CancellationToken cancellationToken = default)
        {
            var context = new AuditContextInfo
            {
                Dostawca = dostawca,
                DataOdbioru = dataOdbioru
            };

            await LogFieldChangeAsync("HarmonogramDostaw", lp, source, "Bufor",
                oldStatus, newStatus, context, cancellationToken);
        }

        /// <summary>
        /// Loguje zmianę potwierdzenia sztuk
        /// </summary>
        public async Task LogQuantityConfirmationChangeAsync(string lp, bool oldValue, bool newValue,
            AuditChangeSource source, string dostawca = null, DateTime? dataOdbioru = null,
            CancellationToken cancellationToken = default)
        {
            var context = new AuditContextInfo
            {
                Dostawca = dostawca,
                DataOdbioru = dataOdbioru
            };

            await LogFieldChangeAsync("HarmonogramDostaw", lp, source, "PotwSztuki",
                oldValue ? "Tak" : "Nie", newValue ? "Tak" : "Nie", context, cancellationToken);
        }

        /// <summary>
        /// Loguje zmianę potwierdzenia wagi
        /// </summary>
        public async Task LogWeightConfirmationChangeAsync(string lp, bool oldValue, bool newValue,
            AuditChangeSource source, string dostawca = null, DateTime? dataOdbioru = null,
            CancellationToken cancellationToken = default)
        {
            var context = new AuditContextInfo
            {
                Dostawca = dostawca,
                DataOdbioru = dataOdbioru
            };

            await LogFieldChangeAsync("HarmonogramDostaw", lp, source, "PotwWaga",
                oldValue ? "Tak" : "Nie", newValue ? "Tak" : "Nie", context, cancellationToken);
        }

        /// <summary>
        /// Loguje dodanie notatki
        /// </summary>
        public async Task LogNoteAddedAsync(string deliveryLp, string noteContent,
            AuditChangeSource source, string dostawca = null, DateTime? dataOdbioru = null,
            CancellationToken cancellationToken = default)
        {
            var context = new AuditContextInfo
            {
                Dostawca = dostawca,
                DataOdbioru = dataOdbioru,
                RelatedLP = deliveryLp
            };

            await LogInsertAsync("Notatki", deliveryLp, source,
                new Dictionary<string, object>
                {
                    { "IndeksID", deliveryLp },
                    { "Tresc", noteContent }
                },
                context, cancellationToken);
        }

        /// <summary>
        /// Loguje operację Drag & Drop
        /// </summary>
        public async Task LogDragDropAsync(string lp, DateTime oldDate, DateTime newDate,
            string dostawca = null, CancellationToken cancellationToken = default)
        {
            var context = new AuditContextInfo
            {
                Dostawca = dostawca,
                DataOdbioru = newDate,
                DodatkowyKomentarz = $"Przeciągnięto z {oldDate:dd.MM.yyyy} na {newDate:dd.MM.yyyy}"
            };

            await LogFieldChangeAsync("HarmonogramDostaw", lp, AuditChangeSource.DragDrop, "DataOdbioru",
                oldDate.ToString("yyyy-MM-dd"),
                newDate.ToString("yyyy-MM-dd"),
                context, cancellationToken);
        }

        /// <summary>
        /// Loguje duplikację dostawy
        /// </summary>
        public async Task LogDuplicateAsync(string originalLp, string newLp,
            string dostawca = null, DateTime? dataOdbioru = null,
            CancellationToken cancellationToken = default)
        {
            var context = new AuditContextInfo
            {
                Dostawca = dostawca,
                DataOdbioru = dataOdbioru,
                RelatedLP = originalLp,
                DodatkowyKomentarz = $"Kopia dostawy {originalLp}"
            };

            await LogInsertAsync("HarmonogramDostaw", newLp, AuditChangeSource.Button_Duplikuj,
                new Dictionary<string, object>
                {
                    { "OriginalLP", originalLp }
                },
                context, cancellationToken);
        }

        /// <summary>
        /// Loguje usunięcie dostawy
        /// </summary>
        public async Task LogDeliveryDeleteAsync(string lp, string dostawca,
            DateTime? dataOdbioru, int? auta, double? sztuki,
            CancellationToken cancellationToken = default)
        {
            var context = new AuditContextInfo
            {
                Dostawca = dostawca,
                DataOdbioru = dataOdbioru
            };

            await LogDeleteAsync("HarmonogramDostaw", lp, AuditChangeSource.Button_Usun,
                new Dictionary<string, object>
                {
                    { "Dostawca", dostawca },
                    { "DataOdbioru", dataOdbioru?.ToString("yyyy-MM-dd") },
                    { "Auta", auta },
                    { "SztukiDek", sztuki }
                },
                context, cancellationToken);
        }

        /// <summary>
        /// Loguje potwierdzenie wstawienia
        /// </summary>
        public async Task LogWstawienieConfirmationAsync(string lp, bool newValue,
            CancellationToken cancellationToken = default)
        {
            await LogFieldChangeAsync("WstawieniaKurczakow", lp, AuditChangeSource.Checkbox_Wstawienie,
                "isConf", !newValue ? "1" : "0", newValue ? "1" : "0", null, cancellationToken);
        }

        /// <summary>
        /// Loguje pełny zapis formularza dostawy
        /// </summary>
        public async Task LogFullDeliverySaveAsync(
            string lp,
            Dictionary<string, (object OldValue, object NewValue)> changes,
            string dostawca = null,
            DateTime? dataOdbioru = null,
            CancellationToken cancellationToken = default)
        {
            var context = new AuditContextInfo
            {
                Dostawca = dostawca,
                DataOdbioru = dataOdbioru
            };

            await LogMultiFieldChangeAsync("HarmonogramDostaw", lp, AuditChangeSource.Form_Zapisz,
                changes, context, cancellationToken);
        }

        #endregion

        #region Metody pobierania historii

        /// <summary>
        /// Pobiera historię zmian dla konkretnej dostawy
        /// </summary>
        public async Task<List<AuditLogEntry>> GetHistoryByLPAsync(string lp, int topN = 100,
            CancellationToken cancellationToken = default)
        {
            var entries = new List<AuditLogEntry>();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync(cancellationToken);

                using (var cmd = new SqlCommand("sp_AuditLog_GetByLP", conn))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@LP", lp);
                    cmd.Parameters.AddWithValue("@TopN", topN);

                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            entries.Add(MapReaderToEntry(reader));
                        }
                    }
                }
            }

            return entries;
        }

        /// <summary>
        /// Pobiera ostatnie zmiany
        /// </summary>
        public async Task<List<AuditLogEntry>> GetRecentChangesAsync(int hours = 24, int topN = 500,
            CancellationToken cancellationToken = default)
        {
            var entries = new List<AuditLogEntry>();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync(cancellationToken);

                using (var cmd = new SqlCommand("sp_AuditLog_GetRecent", conn))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Hours", hours);
                    cmd.Parameters.AddWithValue("@TopN", topN);

                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            entries.Add(MapReaderToEntry(reader));
                        }
                    }
                }
            }

            return entries;
        }

        #endregion

        #region Metody prywatne

        /// <summary>
        /// Główna metoda logowania do bazy danych
        /// </summary>
        private async Task LogAsync(
            string tableName,
            string recordId,
            AuditOperationType operationType,
            AuditChangeSource source,
            string fieldName,
            string oldValue,
            string newValue,
            AuditContextInfo contextInfo,
            string description,
            CancellationToken cancellationToken)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync(cancellationToken);

                    using (var cmd = new SqlCommand(@"
                        INSERT INTO AuditLog_Dostawy (
                            UserID, UserName, NazwaTabeli, RekordID, TypOperacji, ZrodloZmiany,
                            NazwaPola, StaraWartosc, NowaWartosc, DodatkoweInfo, OpisZmiany,
                            NazwaKomputera
                        )
                        VALUES (
                            @UserID, @UserName, @NazwaTabeli, @RekordID, @TypOperacji, @ZrodloZmiany,
                            @NazwaPola, @StaraWartosc, @NowaWartosc, @DodatkoweInfo, @OpisZmiany,
                            @NazwaKomputera
                        )", conn))
                    {
                        cmd.Parameters.AddWithValue("@UserID", _userId ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@UserName", _userName ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@NazwaTabeli", tableName);
                        cmd.Parameters.AddWithValue("@RekordID", recordId);
                        cmd.Parameters.AddWithValue("@TypOperacji", operationType.ToString());
                        cmd.Parameters.AddWithValue("@ZrodloZmiany", source.ToString());
                        cmd.Parameters.AddWithValue("@NazwaPola", fieldName ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@StaraWartosc", oldValue ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@NowaWartosc", newValue ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DodatkoweInfo", contextInfo?.ToJson() ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@OpisZmiany", description ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@NazwaKomputera", _computerName ?? (object)DBNull.Value);

                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                // Logowanie błędów audytu nie powinno przerywać głównej operacji
                System.Diagnostics.Debug.WriteLine($"AuditLog Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Konwertuje wartość na string do zapisu
        /// </summary>
        private string ConvertToString(object value)
        {
            if (value == null || value == DBNull.Value)
                return null;

            if (value is DateTime dt)
                return dt.ToString("yyyy-MM-dd HH:mm:ss");

            if (value is decimal dec)
                return dec.ToString("0.00");

            if (value is double dbl)
                return dbl.ToString("0.##");

            if (value is bool b)
                return b ? "Tak" : "Nie";

            return value.ToString();
        }

        /// <summary>
        /// Sprawdza czy wartości są równe
        /// </summary>
        private bool AreValuesEqual(object val1, object val2)
        {
            if (val1 == null && val2 == null) return true;
            if (val1 == null || val2 == null) return false;

            return ConvertToString(val1) == ConvertToString(val2);
        }

        /// <summary>
        /// Generuje czytelny opis zmiany
        /// </summary>
        private string GenerateDescription(AuditOperationType opType, string fieldName, object oldValue, object newValue)
        {
            if (opType == AuditOperationType.INSERT)
                return "Utworzono nowy rekord";

            if (opType == AuditOperationType.DELETE)
                return "Usunięto rekord";

            var oldStr = ConvertToString(oldValue) ?? "(brak)";
            var newStr = ConvertToString(newValue) ?? "(brak)";

            return $"Zmieniono {GetPolishFieldName(fieldName)}: {oldStr} → {newStr}";
        }

        /// <summary>
        /// Zwraca polską nazwę pola
        /// </summary>
        private string GetPolishFieldName(string fieldName)
        {
            return fieldName switch
            {
                "Auta" => "ilość aut",
                "SztukiDek" => "sztuki",
                "WagaDek" => "wagę",
                "Cena" => "cenę",
                "DataOdbioru" => "datę odbioru",
                "Bufor" => "status",
                "Dostawca" => "dostawcę",
                "TypCeny" => "typ ceny",
                "TypUmowy" => "typ umowy",
                "Dodatek" => "dodatek",
                "SztSzuflada" => "szt. na szufladę",
                "Tresc" => "treść notatki",
                "isConf" => "potwierdzenie",
                "PotwSztuki" => "potwierdzenie sztuk",
                "PotwWaga" => "potwierdzenie wagi",
                "UWAGI" => "uwagi",
                "Ubytek" => "ubytek",
                _ => fieldName
            };
        }

        /// <summary>
        /// Mapuje reader na obiekt AuditLogEntry
        /// </summary>
        private AuditLogEntry MapReaderToEntry(SqlDataReader reader)
        {
            return new AuditLogEntry
            {
                AuditID = reader.GetInt64(reader.GetOrdinal("AuditID")),
                DataZmiany = reader.GetDateTime(reader.GetOrdinal("DataZmiany")),
                UserID = reader.IsDBNull(reader.GetOrdinal("UserID")) ? null : reader.GetString(reader.GetOrdinal("UserID")),
                UserName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? null : reader.GetString(reader.GetOrdinal("UserName")),
                TypOperacji = reader.IsDBNull(reader.GetOrdinal("TypOperacji")) ? null : reader.GetString(reader.GetOrdinal("TypOperacji")),
                ZrodloZmiany = reader.IsDBNull(reader.GetOrdinal("ZrodloZmiany")) ? null : reader.GetString(reader.GetOrdinal("ZrodloZmiany")),
                NazwaPola = reader.IsDBNull(reader.GetOrdinal("NazwaPola")) ? null : reader.GetString(reader.GetOrdinal("NazwaPola")),
                StaraWartosc = reader.IsDBNull(reader.GetOrdinal("StaraWartosc")) ? null : reader.GetString(reader.GetOrdinal("StaraWartosc")),
                NowaWartosc = reader.IsDBNull(reader.GetOrdinal("NowaWartosc")) ? null : reader.GetString(reader.GetOrdinal("NowaWartosc")),
                OpisZmiany = reader.IsDBNull(reader.GetOrdinal("OpisZmiany")) ? null : reader.GetString(reader.GetOrdinal("OpisZmiany"))
            };
        }

        #endregion
    }
}
