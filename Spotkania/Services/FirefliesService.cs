using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Kalendarz1.Spotkania.Models;

namespace Kalendarz1.Spotkania.Services
{
    /// <summary>
    /// Serwis do integracji z Fireflies.ai API
    /// </summary>
    public class FirefliesService
    {
        private const string FIREFLIES_API_URL = "https://api.fireflies.ai/graphql";
        private const string CONNECTION_STRING = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova";

        private readonly HttpClient _httpClient;
        private string? _apiKey;

        public FirefliesService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        #region Konfiguracja

        /// <summary>
        /// Pobiera konfigurację Fireflies z bazy danych
        /// </summary>
        public async Task<FirefliesKonfiguracja?> PobierzKonfiguracje()
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                string sql = @"SELECT TOP 1 * FROM FirefliesKonfiguracja WHERE Aktywna = 1 ORDER BY ID DESC";

                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var config = new FirefliesKonfiguracja
                    {
                        ID = reader.GetInt32(reader.GetOrdinal("ID")),
                        ApiKey = reader.IsDBNull(reader.GetOrdinal("ApiKeyPlain")) ? null : reader.GetString(reader.GetOrdinal("ApiKeyPlain")),
                        AutoImportNotatek = reader.GetBoolean(reader.GetOrdinal("AutoImportNotatek")),
                        AutoSynchronizacja = reader.GetBoolean(reader.GetOrdinal("AutoSynchronizacja")),
                        InterwalSynchronizacjiMin = reader.GetInt32(reader.GetOrdinal("InterwalSynchronizacjiMin")),
                        MinimalnyCzasSpotkaniaSek = reader.GetInt32(reader.GetOrdinal("MinimalnyCzasSpotkaniaSek")),
                        Aktywna = reader.GetBoolean(reader.GetOrdinal("Aktywna"))
                    };

                    if (!reader.IsDBNull(reader.GetOrdinal("OstatniaSynchronizacja")))
                        config.OstatniaSynchronizacja = reader.GetDateTime(reader.GetOrdinal("OstatniaSynchronizacja"));

                    if (!reader.IsDBNull(reader.GetOrdinal("OstatniBladSynchronizacji")))
                        config.OstatniBladSynchronizacji = reader.GetString(reader.GetOrdinal("OstatniBladSynchronizacji"));

                    if (!reader.IsDBNull(reader.GetOrdinal("ImportujOdDaty")))
                        config.ImportujOdDaty = reader.GetDateTime(reader.GetOrdinal("ImportujOdDaty"));

                    _apiKey = config.ApiKey;
                    return config;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania konfiguracji Fireflies: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Zapisuje konfigurację Fireflies
        /// </summary>
        public async Task<bool> ZapiszKonfiguracje(FirefliesKonfiguracja config)
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                string sql;
                if (config.ID > 0)
                {
                    sql = @"UPDATE FirefliesKonfiguracja SET
                        ApiKeyPlain = @ApiKey,
                        AutoImportNotatek = @AutoImport,
                        AutoSynchronizacja = @AutoSync,
                        InterwalSynchronizacjiMin = @Interwal,
                        ImportujOdDaty = @ImportOd,
                        MinimalnyCzasSpotkaniaSek = @MinCzas,
                        Aktywna = @Aktywna,
                        DataModyfikacji = GETDATE()
                    WHERE ID = @ID";
                }
                else
                {
                    sql = @"INSERT INTO FirefliesKonfiguracja
                        (ApiKeyPlain, AutoImportNotatek, AutoSynchronizacja, InterwalSynchronizacjiMin,
                         ImportujOdDaty, MinimalnyCzasSpotkaniaSek, Aktywna, DataUtworzenia)
                    VALUES (@ApiKey, @AutoImport, @AutoSync, @Interwal, @ImportOd, @MinCzas, @Aktywna, GETDATE())";
                }

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ApiKey", (object?)config.ApiKey ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@AutoImport", config.AutoImportNotatek);
                cmd.Parameters.AddWithValue("@AutoSync", config.AutoSynchronizacja);
                cmd.Parameters.AddWithValue("@Interwal", config.InterwalSynchronizacjiMin);
                cmd.Parameters.AddWithValue("@ImportOd", (object?)config.ImportujOdDaty ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MinCzas", config.MinimalnyCzasSpotkaniaSek);
                cmd.Parameters.AddWithValue("@Aktywna", config.Aktywna);

                if (config.ID > 0)
                    cmd.Parameters.AddWithValue("@ID", config.ID);

                await cmd.ExecuteNonQueryAsync();
                _apiKey = config.ApiKey;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd zapisywania konfiguracji Fireflies: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Testuje połączenie z API Fireflies
        /// </summary>
        public async Task<(bool Success, string Message, FirefliesUserDto? User)> TestujPolaczenie(string apiKey)
        {
            try
            {
                string query = @"
                    query {
                        user {
                            user_id
                            email
                            name
                            minutes_consumed
                            is_admin
                        }
                    }";

                var response = await WykonajZapytanieGraphQL<UserQueryResponse>(query, apiKey: apiKey);

                if (response.HasErrors)
                {
                    return (false, $"Błąd API: {response.Errors?[0].Message}", null);
                }

                if (response.Data?.User == null)
                {
                    return (false, "Nie udało się pobrać danych użytkownika", null);
                }

                return (true, $"Połączono jako: {response.Data.User.Name} ({response.Data.User.Email})", response.Data.User);
            }
            catch (HttpRequestException ex)
            {
                return (false, $"Błąd połączenia: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                return (false, $"Nieoczekiwany błąd: {ex.Message}", null);
            }
        }

        #endregion

        #region Pobieranie transkrypcji

        /// <summary>
        /// Pobiera listę transkrypcji z Fireflies
        /// </summary>
        public async Task<List<FirefliesTranscriptDto>> PobierzListeTranskrypcji(int limit = 50, DateTime? odDaty = null)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                var config = await PobierzKonfiguracje();
                if (string.IsNullOrWhiteSpace(config?.ApiKey))
                    throw new InvalidOperationException("Brak skonfigurowanego klucza API Fireflies");
            }

            try
            {
                string query = @"
                    query Transcripts($limit: Int) {
                        transcripts(limit: $limit) {
                            id
                            title
                            date
                            duration
                            transcript_url
                            host_email
                            participants
                        }
                    }";

                var variables = new Dictionary<string, object> { { "limit", limit } };

                var response = await WykonajZapytanieGraphQL<TranscriptsQueryResponse>(query, variables);

                if (response.HasErrors)
                {
                    throw new Exception($"Błąd API Fireflies: {response.Errors?[0].Message}");
                }

                var transkrypcje = response.Data?.Transcripts ?? new List<FirefliesTranscriptDto>();

                // Filtruj po dacie jeśli podano
                if (odDaty.HasValue)
                {
                    transkrypcje = transkrypcje.FindAll(t =>
                        t.DateAsDateTime.HasValue && t.DateAsDateTime.Value >= odDaty.Value);
                }

                return transkrypcje;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania listy transkrypcji: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Pobiera szczegóły transkrypcji z Fireflies
        /// </summary>
        public async Task<FirefliesTranscriptDto?> PobierzSzczegolyTranskrypcji(string transcriptId)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                var config = await PobierzKonfiguracje();
                if (string.IsNullOrWhiteSpace(config?.ApiKey))
                    throw new InvalidOperationException("Brak skonfigurowanego klucza API Fireflies");
            }

            try
            {
                string query = @"
                    query Transcript($transcriptId: String!) {
                        transcript(id: $transcriptId) {
                            id
                            title
                            date
                            duration
                            transcript_url
                            host_email
                            participants
                            sentences {
                                index
                                text
                                speaker_id
                                speaker_name
                                start_time
                                end_time
                            }
                            summary {
                                keywords
                                action_items
                                overview
                                shorthand_bullet
                                outline
                            }
                        }
                    }";

                var variables = new Dictionary<string, object> { { "transcriptId", transcriptId } };

                var response = await WykonajZapytanieGraphQL<TranscriptQueryResponse>(query, variables);

                if (response.HasErrors)
                {
                    throw new Exception($"Błąd API Fireflies: {response.Errors?[0].Message}");
                }

                return response.Data?.Transcript;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania szczegółów transkrypcji: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Synchronizacja z bazą danych

        /// <summary>
        /// Synchronizuje transkrypcje z Fireflies do bazy danych
        /// </summary>
        public async Task<FirefliesSyncStatus> SynchronizujTranskrypcje(IProgress<FirefliesSyncStatus>? progress = null)
        {
            var status = new FirefliesSyncStatus { TrwaSynchronizacja = true, AktualnyEtap = "Inicjalizacja..." };
            progress?.Report(status);

            try
            {
                var config = await PobierzKonfiguracje();
                if (config == null || string.IsNullOrWhiteSpace(config.ApiKey))
                {
                    status.BladMessage = "Brak konfiguracji API Fireflies";
                    status.TrwaSynchronizacja = false;
                    return status;
                }

                // Pobierz listę transkrypcji
                status.AktualnyEtap = "Pobieranie listy transkrypcji...";
                progress?.Report(status);

                var transkrypcje = await PobierzListeTranskrypcji(100, config.ImportujOdDaty);
                status.MaksymalnyPostep = transkrypcje.Count;
                progress?.Report(status);

                int zaimportowano = 0;

                foreach (var t in transkrypcje)
                {
                    status.Postep++;
                    status.AktualnyEtap = $"Przetwarzanie: {t.Title ?? t.Id}";
                    progress?.Report(status);

                    // Sprawdź czy już istnieje
                    if (await CzyTranskrypcjaIstnieje(t.Id!))
                        continue;

                    // Filtruj krótkie spotkania
                    if (t.Duration.HasValue && t.Duration.Value < config.MinimalnyCzasSpotkaniaSek)
                        continue;

                    // Pobierz szczegóły
                    var szczegoly = await PobierzSzczegolyTranskrypcji(t.Id!);
                    if (szczegoly == null) continue;

                    // Zapisz do bazy
                    await ZapiszTranskrypcje(szczegoly);
                    zaimportowano++;
                }

                // Aktualizuj datę synchronizacji
                await AktualizujDateSynchronizacji(config.ID);

                status.ZaimportowanoTranskrypcji = zaimportowano;
                status.OstatniaSynchronizacja = DateTime.Now;
                status.AktualnyEtap = $"Zakończono. Zaimportowano: {zaimportowano}";
                status.TrwaSynchronizacja = false;
                progress?.Report(status);

                return status;
            }
            catch (Exception ex)
            {
                status.BladMessage = ex.Message;
                status.TrwaSynchronizacja = false;
                progress?.Report(status);

                // Zapisz błąd w bazie
                await ZapiszBladSynchronizacji(ex.Message);

                return status;
            }
        }

        /// <summary>
        /// Sprawdza czy transkrypcja już istnieje w bazie
        /// </summary>
        private async Task<bool> CzyTranskrypcjaIstnieje(string firefliesId)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = "SELECT COUNT(*) FROM FirefliesTranskrypcje WHERE FirefliesID = @ID";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", firefliesId);

            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        /// <summary>
        /// Zapisuje transkrypcję do bazy danych
        /// </summary>
        private async Task ZapiszTranskrypcje(FirefliesTranscriptDto dto)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = @"INSERT INTO FirefliesTranskrypcje
                (FirefliesID, Tytul, DataSpotkania, CzasTrwaniaSekundy, Uczestnicy, HostEmail,
                 Transkrypcja, TranskrypcjaUrl, Podsumowanie, AkcjeDoDziałania, SlowKluczowe,
                 NastepneKroki, StatusImportu, DataImportu)
            VALUES
                (@FirefliesID, @Tytul, @DataSpotkania, @CzasTrwania, @Uczestnicy, @HostEmail,
                 @Transkrypcja, @TranskrypcjaUrl, @Podsumowanie, @Akcje, @Slowa,
                 @Kroki, 'Zaimportowane', GETDATE())";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@FirefliesID", dto.Id);
            cmd.Parameters.AddWithValue("@Tytul", (object?)dto.Title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DataSpotkania", (object?)dto.DateAsDateTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CzasTrwania", dto.Duration ?? 0);

            // Uczestnicy jako JSON
            var uczestnicy = dto.Participants != null ? JsonSerializer.Serialize(dto.Participants) : null;
            cmd.Parameters.AddWithValue("@Uczestnicy", (object?)uczestnicy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@HostEmail", (object?)dto.HostEmail ?? DBNull.Value);

            // Transkrypcja - złącz zdania
            string? transkrypcjaTekst = null;
            if (dto.Sentences != null && dto.Sentences.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var s in dto.Sentences)
                {
                    sb.AppendLine($"[{s.SpeakerName ?? s.SpeakerId ?? "?"}]: {s.Text}");
                }
                transkrypcjaTekst = sb.ToString();
            }
            cmd.Parameters.AddWithValue("@Transkrypcja", (object?)transkrypcjaTekst ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TranskrypcjaUrl", (object?)dto.TranscriptUrl ?? DBNull.Value);

            // Summary
            cmd.Parameters.AddWithValue("@Podsumowanie", (object?)dto.Summary?.Overview ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Akcje", dto.Summary?.ActionItems != null
                ? JsonSerializer.Serialize(dto.Summary.ActionItems) : DBNull.Value);
            cmd.Parameters.AddWithValue("@Slowa", dto.Summary?.Keywords != null
                ? JsonSerializer.Serialize(dto.Summary.Keywords) : DBNull.Value);
            cmd.Parameters.AddWithValue("@Kroki", dto.Summary?.ShorthandBullet != null
                ? JsonSerializer.Serialize(dto.Summary.ShorthandBullet) : DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Aktualizuje datę ostatniej synchronizacji
        /// </summary>
        private async Task AktualizujDateSynchronizacji(int configId)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = "UPDATE FirefliesKonfiguracja SET OstatniaSynchronizacja = GETDATE(), OstatniBladSynchronizacji = NULL WHERE ID = @ID";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", configId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Zapisuje błąd synchronizacji
        /// </summary>
        private async Task ZapiszBladSynchronizacji(string blad)
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                string sql = "UPDATE FirefliesKonfiguracja SET OstatniBladSynchronizacji = @Blad WHERE Aktywna = 1";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Blad", blad);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
        }

        #endregion

        #region Pobieranie z bazy danych

        /// <summary>
        /// Pobiera listę transkrypcji z bazy danych
        /// </summary>
        public async Task<List<FirefliesTranskrypcjaListItem>> PobierzTranskrypcjeZBazy(
            DateTime? odDaty = null, DateTime? doDaty = null, bool tylkoNiepowiazane = false)
        {
            var lista = new List<FirefliesTranskrypcjaListItem>();

            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            var sql = new StringBuilder(@"
                SELECT
                    TranskrypcjaID, FirefliesID, Tytul, DataSpotkania, CzasTrwaniaSekundy,
                    Uczestnicy, SpotkaniID, NotatkaID, StatusImportu, DataImportu
                FROM FirefliesTranskrypcje
                WHERE 1=1");

            if (odDaty.HasValue)
                sql.Append(" AND DataSpotkania >= @OdDaty");
            if (doDaty.HasValue)
                sql.Append(" AND DataSpotkania <= @DoDaty");
            if (tylkoNiepowiazane)
                sql.Append(" AND SpotkaniID IS NULL AND NotatkaID IS NULL");

            sql.Append(" ORDER BY DataSpotkania DESC");

            using var cmd = new SqlCommand(sql.ToString(), conn);
            if (odDaty.HasValue)
                cmd.Parameters.AddWithValue("@OdDaty", odDaty.Value);
            if (doDaty.HasValue)
                cmd.Parameters.AddWithValue("@DoDaty", doDaty.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var item = new FirefliesTranskrypcjaListItem
                {
                    TranskrypcjaID = reader.GetInt64(0),
                    FirefliesID = reader.GetString(1),
                    Tytul = reader.IsDBNull(2) ? null : reader.GetString(2),
                    DataSpotkania = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                    CzasTrwaniaSekundy = reader.GetInt32(4),
                    MaSpotkanie = !reader.IsDBNull(6),
                    MaNotatke = !reader.IsDBNull(7),
                    StatusImportu = reader.GetString(8),
                    DataImportu = reader.GetDateTime(9)
                };

                // Policz uczestników z JSON
                if (!reader.IsDBNull(5))
                {
                    try
                    {
                        var uczestnicy = JsonSerializer.Deserialize<List<string>>(reader.GetString(5));
                        item.LiczbaUczestnikow = uczestnicy?.Count ?? 0;
                    }
                    catch { }
                }

                lista.Add(item);
            }

            return lista;
        }

        /// <summary>
        /// Pobiera szczegóły transkrypcji z bazy danych
        /// </summary>
        public async Task<FirefliesTranskrypcja?> PobierzTranskrypcjeZBazyPoId(long transkrypcjaId)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = @"SELECT * FROM FirefliesTranskrypcje WHERE TranskrypcjaID = @ID";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", transkrypcjaId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            var t = new FirefliesTranskrypcja
            {
                TranskrypcjaID = reader.GetInt64(reader.GetOrdinal("TranskrypcjaID")),
                FirefliesID = reader.GetString(reader.GetOrdinal("FirefliesID")),
                Tytul = reader.IsDBNull(reader.GetOrdinal("Tytul")) ? null : reader.GetString(reader.GetOrdinal("Tytul")),
                CzasTrwaniaSekundy = reader.GetInt32(reader.GetOrdinal("CzasTrwaniaSekundy")),
                Transkrypcja = reader.IsDBNull(reader.GetOrdinal("Transkrypcja")) ? null : reader.GetString(reader.GetOrdinal("Transkrypcja")),
                TranskrypcjaUrl = reader.IsDBNull(reader.GetOrdinal("TranskrypcjaUrl")) ? null : reader.GetString(reader.GetOrdinal("TranskrypcjaUrl")),
                Podsumowanie = reader.IsDBNull(reader.GetOrdinal("Podsumowanie")) ? null : reader.GetString(reader.GetOrdinal("Podsumowanie")),
                HostEmail = reader.IsDBNull(reader.GetOrdinal("HostEmail")) ? null : reader.GetString(reader.GetOrdinal("HostEmail")),
                StatusImportu = reader.GetString(reader.GetOrdinal("StatusImportu")),
                DataImportu = reader.GetDateTime(reader.GetOrdinal("DataImportu"))
            };

            if (!reader.IsDBNull(reader.GetOrdinal("DataSpotkania")))
                t.DataSpotkania = reader.GetDateTime(reader.GetOrdinal("DataSpotkania"));

            if (!reader.IsDBNull(reader.GetOrdinal("SpotkaniID")))
                t.SpotkaniID = reader.GetInt64(reader.GetOrdinal("SpotkaniID"));

            if (!reader.IsDBNull(reader.GetOrdinal("NotatkaID")))
                t.NotatkaID = reader.GetInt64(reader.GetOrdinal("NotatkaID"));

            // Parse JSON fields
            if (!reader.IsDBNull(reader.GetOrdinal("AkcjeDoDziałania")))
            {
                try
                {
                    t.AkcjeDoDziałania = JsonSerializer.Deserialize<List<string>>(
                        reader.GetString(reader.GetOrdinal("AkcjeDoDziałania"))) ?? new List<string>();
                }
                catch { }
            }

            if (!reader.IsDBNull(reader.GetOrdinal("SlowKluczowe")))
            {
                try
                {
                    t.SlowKluczowe = JsonSerializer.Deserialize<List<string>>(
                        reader.GetString(reader.GetOrdinal("SlowKluczowe"))) ?? new List<string>();
                }
                catch { }
            }

            if (!reader.IsDBNull(reader.GetOrdinal("NastepneKroki")))
            {
                try
                {
                    t.NastepneKroki = JsonSerializer.Deserialize<List<string>>(
                        reader.GetString(reader.GetOrdinal("NastepneKroki"))) ?? new List<string>();
                }
                catch { }
            }

            return t;
        }

        /// <summary>
        /// Powiązuje transkrypcję ze spotkaniem
        /// </summary>
        public async Task PowiazTranskrypcjeZeSpotkaniem(long transkrypcjaId, long spotkaniId)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            // Aktualizuj transkrypcję
            string sql1 = "UPDATE FirefliesTranskrypcje SET SpotkaniID = @SpotkaniID, DataModyfikacji = GETDATE() WHERE TranskrypcjaID = @ID";
            using var cmd1 = new SqlCommand(sql1, conn);
            cmd1.Parameters.AddWithValue("@SpotkaniID", spotkaniId);
            cmd1.Parameters.AddWithValue("@ID", transkrypcjaId);
            await cmd1.ExecuteNonQueryAsync();

            // Aktualizuj spotkanie
            // Pobierz FirefliesID
            string sql2 = "SELECT FirefliesID FROM FirefliesTranskrypcje WHERE TranskrypcjaID = @ID";
            using var cmd2 = new SqlCommand(sql2, conn);
            cmd2.Parameters.AddWithValue("@ID", transkrypcjaId);
            var firefliesId = await cmd2.ExecuteScalarAsync() as string;

            if (!string.IsNullOrEmpty(firefliesId))
            {
                string sql3 = "UPDATE Spotkania SET FirefliesTranscriptID = @FID, DataModyfikacji = GETDATE() WHERE SpotkaniID = @ID";
                using var cmd3 = new SqlCommand(sql3, conn);
                cmd3.Parameters.AddWithValue("@FID", firefliesId);
                cmd3.Parameters.AddWithValue("@ID", spotkaniId);
                await cmd3.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Powiązuje transkrypcję z notatką
        /// </summary>
        public async Task PowiazTranskrypcjeZNotatka(long transkrypcjaId, long notatkaId)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = "UPDATE FirefliesTranskrypcje SET NotatkaID = @NotatkaID, DataModyfikacji = GETDATE() WHERE TranskrypcjaID = @ID";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@NotatkaID", notatkaId);
            cmd.Parameters.AddWithValue("@ID", transkrypcjaId);
            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region Pomocnicze

        /// <summary>
        /// Wykonuje zapytanie GraphQL do API Fireflies
        /// </summary>
        private async Task<GraphQLResponse<T>> WykonajZapytanieGraphQL<T>(
            string query,
            Dictionary<string, object>? variables = null,
            string? apiKey = null)
        {
            var key = apiKey ?? _apiKey;
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("Brak klucza API Fireflies");

            var requestBody = new Dictionary<string, object> { { "query", query } };
            if (variables != null)
                requestBody["variables"] = variables;

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, FIREFLIES_API_URL);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Błąd API ({response.StatusCode}): {responseJson}");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<GraphQLResponse<T>>(responseJson, options)
                ?? new GraphQLResponse<T>();
        }

        #endregion
    }
}
