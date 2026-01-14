using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Serwis integracji z systemem IRZplus ARiMR
    /// Obsługuje uwierzytelnianie OAuth 2.0 i komunikację REST API
    /// </summary>
    public class IRZplusService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly IRZplusSettings _settings;
        private TokenResponse _currentToken;
        private readonly string _settingsFilePath;
        private readonly string _historyDbPath;
        private bool _disposed = false;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public IRZplusService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);

            _settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ZPSP", "IRZplus_settings.json");

            _historyDbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ZPSP", "IRZplus_history");

            _settings = LoadSettings();
            EnsureDirectoriesExist();
        }

        #region Settings Management

        private void EnsureDirectoriesExist()
        {
            var settingsDir = Path.GetDirectoryName(_settingsFilePath);
            if (!Directory.Exists(settingsDir))
                Directory.CreateDirectory(settingsDir);

            if (!Directory.Exists(_historyDbPath))
                Directory.CreateDirectory(_historyDbPath);

            if (!string.IsNullOrEmpty(_settings.LocalExportPath) && !Directory.Exists(_settings.LocalExportPath))
                Directory.CreateDirectory(_settings.LocalExportPath);
        }

        public IRZplusSettings GetSettings() => _settings;

        public void SaveSettings(IRZplusSettings settings)
        {
            try
            {
                // Kopiuj ustawienia
                _settings.NumerUbojni = settings.NumerUbojni;
                _settings.NazwaUbojni = settings.NazwaUbojni;
                _settings.ClientId = settings.ClientId;
                _settings.ClientSecret = settings.ClientSecret;
                _settings.Username = settings.Username;
                _settings.Password = settings.Password;
                _settings.UseTestEnvironment = settings.UseTestEnvironment;
                _settings.AutoSendOnSave = settings.AutoSendOnSave;
                _settings.SaveLocalCopy = settings.SaveLocalCopy;
                _settings.LocalExportPath = settings.LocalExportPath;

                // Zapisz do pliku (zaszyfrowane dane logowania)
                var settingsToSave = new
                {
                    NumerUbojni = _settings.NumerUbojni,
                    NazwaUbojni = _settings.NazwaUbojni,
                    ClientId = EncodeCredential(_settings.ClientId),
                    ClientSecret = EncodeCredential(_settings.ClientSecret),
                    Username = EncodeCredential(_settings.Username),
                    Password = EncodeCredential(_settings.Password),
                    UseTestEnvironment = _settings.UseTestEnvironment,
                    AutoSendOnSave = _settings.AutoSendOnSave,
                    SaveLocalCopy = _settings.SaveLocalCopy,
                    LocalExportPath = _settings.LocalExportPath,
                    LastSuccessfulSync = _settings.LastSuccessfulSync
                };

                var json = JsonSerializer.Serialize(settingsToSave, _jsonOptions);
                File.WriteAllText(_settingsFilePath, json);

                // Wyczyść token po zmianie ustawień
                _currentToken = null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd zapisu ustawień: {ex.Message}", ex);
            }
        }

        private IRZplusSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                    return new IRZplusSettings
                    {
                        NumerUbojni = GetStringValue(loaded, "NumerUbojni", "10141607"),
                        NazwaUbojni = GetStringValue(loaded, "NazwaUbojni", "Ubojnia Drobiu Piórkowscy"),
                        ClientId = DecodeCredential(GetStringValue(loaded, "ClientId", "")),
                        ClientSecret = DecodeCredential(GetStringValue(loaded, "ClientSecret", "")),
                        Username = DecodeCredential(GetStringValue(loaded, "Username", "")),
                        Password = DecodeCredential(GetStringValue(loaded, "Password", "")),
                        UseTestEnvironment = GetBoolValue(loaded, "UseTestEnvironment", true),
                        AutoSendOnSave = GetBoolValue(loaded, "AutoSendOnSave", false),
                        SaveLocalCopy = GetBoolValue(loaded, "SaveLocalCopy", true),
                        LocalExportPath = GetStringValue(loaded, "LocalExportPath",
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IRZplus_Export"))
                    };
                }
            }
            catch { }

            // Domyślne ustawienia
            return new IRZplusSettings
            {
                LocalExportPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "IRZplus_Export")
            };
        }

        private string GetStringValue(Dictionary<string, JsonElement> dict, string key, string defaultValue)
        {
            if (dict.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.String)
                return element.GetString() ?? defaultValue;
            return defaultValue;
        }

        private bool GetBoolValue(Dictionary<string, JsonElement> dict, string key, bool defaultValue)
        {
            if (dict.TryGetValue(key, out var element))
            {
                if (element.ValueKind == JsonValueKind.True) return true;
                if (element.ValueKind == JsonValueKind.False) return false;
            }
            return defaultValue;
        }

        private string EncodeCredential(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        private string DecodeCredential(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return "";
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
            catch { return ""; }
        }

        #endregion

        #region OAuth 2.0 Authentication

        /// <summary>
        /// Pobiera token dostępu OAuth 2.0
        /// </summary>
        public async Task<IRZplusResult> AuthenticateAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.ClientId) || string.IsNullOrEmpty(_settings.ClientSecret))
                {
                    return IRZplusResult.Error("Brak danych uwierzytelniających. Skonfiguruj Client ID i Client Secret.");
                }

                var formData = new Dictionary<string, string>
                {
                    { "grant_type", "password" },
                    { "client_id", _settings.ClientId },
                    { "client_secret", _settings.ClientSecret },
                    { "username", _settings.Username },
                    { "password", _settings.Password },
                    { "scope", "irz_api" }
                };

                var content = new FormUrlEncodedContent(formData);
                var response = await _httpClient.PostAsync(_settings.TokenEndpoint, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _currentToken = JsonSerializer.Deserialize<TokenResponse>(responseBody, _jsonOptions);
                    _currentToken.ExpirationTime = DateTime.UtcNow.AddSeconds(_currentToken.ExpiresIn);
                    return IRZplusResult.Ok("Uwierzytelnianie zakończone pomyślnie");
                }

                return IRZplusResult.Error($"Błąd uwierzytelniania: {response.StatusCode} - {responseBody}");
            }
            catch (HttpRequestException ex)
            {
                return IRZplusResult.Error($"Błąd połączenia z serwerem ARiMR: {ex.Message}");
            }
            catch (Exception ex)
            {
                return IRZplusResult.Error($"Nieoczekiwany błąd: {ex.Message}");
            }
        }

        /// <summary>
        /// Odświeża token dostępu
        /// </summary>
        public async Task<IRZplusResult> RefreshTokenAsync()
        {
            try
            {
                if (_currentToken == null || string.IsNullOrEmpty(_currentToken.RefreshToken))
                {
                    return await AuthenticateAsync();
                }

                var formData = new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "client_id", _settings.ClientId },
                    { "client_secret", _settings.ClientSecret },
                    { "refresh_token", _currentToken.RefreshToken }
                };

                var content = new FormUrlEncodedContent(formData);
                var response = await _httpClient.PostAsync(_settings.TokenEndpoint, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _currentToken = JsonSerializer.Deserialize<TokenResponse>(responseBody, _jsonOptions);
                    _currentToken.ExpirationTime = DateTime.UtcNow.AddSeconds(_currentToken.ExpiresIn);
                    return IRZplusResult.Ok("Token odświeżony pomyślnie");
                }

                // Jeśli refresh token wygasł, pełne uwierzytelnianie
                return await AuthenticateAsync();
            }
            catch (Exception ex)
            {
                return IRZplusResult.Error($"Błąd odświeżania tokenu: {ex.Message}");
            }
        }

        /// <summary>
        /// Sprawdza i zapewnia ważny token
        /// </summary>
        private async Task<bool> EnsureValidTokenAsync()
        {
            if (_currentToken == null || _currentToken.IsExpired)
            {
                var result = await AuthenticateAsync();
                return result.Success;
            }
            return true;
        }

        /// <summary>
        /// Testuje połączenie z API IRZplus
        /// </summary>
        public async Task<IRZplusResult> TestConnectionAsync()
        {
            try
            {
                var authResult = await AuthenticateAsync();
                if (!authResult.Success)
                    return authResult;

                // Próba pobrania statusu/ping
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _currentToken.AccessToken);

                var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/status");

                if (response.IsSuccessStatusCode)
                {
                    return IRZplusResult.Ok($"Połączenie z API IRZplus ({(_settings.UseTestEnvironment ? "TEST" : "PROD")}) działa poprawnie");
                }

                return IRZplusResult.Ok($"Uwierzytelnianie OK. Endpoint status niedostępny ({response.StatusCode})");
            }
            catch (Exception ex)
            {
                return IRZplusResult.Error($"Błąd testu połączenia: {ex.Message}");
            }
        }

        public bool IsAuthenticated => _currentToken != null && !_currentToken.IsExpired;

        #endregion

        #region API Operations

        /// <summary>
        /// Wysyła zgłoszenie uboju do IRZplus
        /// </summary>
        public async Task<IRZplusResult> SendZgloszenieAsync(ZgloszenieZbiorczeRequest request)
        {
            try
            {
                if (!await EnsureValidTokenAsync())
                {
                    return IRZplusResult.Error("Nie udało się uzyskać tokenu dostępu");
                }

                // Ustaw numer ubojni
                request.NumerUbojni = _settings.NumerUbojni;
                request.DataZgloszenia = DateTime.Now.ToString("yyyy-MM-dd");

                var json = JsonSerializer.Serialize(request, _jsonOptions);

                // Zapisz lokalną kopię
                if (_settings.SaveLocalCopy)
                {
                    await SaveLocalCopyAsync(request, json);
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _currentToken.AccessToken);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_settings.ApiBaseUrl}/zgloszenia/uboj-drobiu", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<IRZplusApiResponse>(responseBody, _jsonOptions);

                    // Zapisz do historii
                    await SaveToHistoryAsync(request, apiResponse, responseBody);

                    _settings.LastSuccessfulSync = DateTime.Now;

                    return new IRZplusResult
                    {
                        Success = true,
                        Message = $"Zgłoszenie wysłane pomyślnie",
                        NumerZgloszenia = apiResponse?.NumerZgloszenia,
                        Warnings = apiResponse?.Warnings ?? new List<string>(),
                        Timestamp = DateTime.Now
                    };
                }

                // Obsługa błędów
                var errorResponse = TryParseErrorResponse(responseBody);
                return IRZplusResult.Error(
                    $"Błąd wysyłania: {response.StatusCode}",
                    errorResponse?.Errors ?? new List<string> { responseBody });
            }
            catch (HttpRequestException ex)
            {
                return IRZplusResult.Error($"Błąd połączenia: {ex.Message}");
            }
            catch (Exception ex)
            {
                return IRZplusResult.Error($"Nieoczekiwany błąd: {ex.Message}");
            }
        }

        /// <summary>
        /// Pobiera status zgłoszenia
        /// </summary>
        public async Task<IRZplusResult> GetStatusZgloszeniaAsync(string numerZgloszenia)
        {
            try
            {
                if (!await EnsureValidTokenAsync())
                {
                    return IRZplusResult.Error("Nie udało się uzyskać tokenu dostępu");
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _currentToken.AccessToken);

                var response = await _httpClient.GetAsync($"{_settings.ApiBaseUrl}/zgloszenia/{numerZgloszenia}/status");
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var status = JsonSerializer.Deserialize<StatusZgloszenia>(responseBody, _jsonOptions);
                    return new IRZplusResult
                    {
                        Success = true,
                        Message = $"Status: {status?.Status}",
                        NumerZgloszenia = numerZgloszenia
                    };
                }

                return IRZplusResult.Error($"Błąd pobierania statusu: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                return IRZplusResult.Error($"Błąd: {ex.Message}");
            }
        }

        /// <summary>
        /// Pobiera historię zgłoszeń z API
        /// </summary>
        public async Task<HistoriaZgloszen> GetHistoriaAsync(DateTime? dataOd = null, DateTime? dataDo = null, int page = 1, int pageSize = 50)
        {
            try
            {
                if (!await EnsureValidTokenAsync())
                {
                    return new HistoriaZgloszen();
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _currentToken.AccessToken);

                var queryParams = new List<string>
                {
                    $"page={page}",
                    $"pageSize={pageSize}",
                    $"numerUbojni={_settings.NumerUbojni}"
                };

                if (dataOd.HasValue)
                    queryParams.Add($"dataOd={dataOd.Value:yyyy-MM-dd}");
                if (dataDo.HasValue)
                    queryParams.Add($"dataDo={dataDo.Value:yyyy-MM-dd}");

                var url = $"{_settings.ApiBaseUrl}/zgloszenia?{string.Join("&", queryParams)}";
                var response = await _httpClient.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<HistoriaZgloszen>(responseBody, _jsonOptions);
                }
            }
            catch { }

            return new HistoriaZgloszen();
        }

        /// <summary>
        /// Anuluje zgłoszenie
        /// </summary>
        public async Task<IRZplusResult> AnulujZgloszenieAsync(string numerZgloszenia, string powod)
        {
            try
            {
                if (!await EnsureValidTokenAsync())
                {
                    return IRZplusResult.Error("Nie udało się uzyskać tokenu dostępu");
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _currentToken.AccessToken);

                var content = new StringContent(
                    JsonSerializer.Serialize(new { powod }),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_settings.ApiBaseUrl}/zgloszenia/{numerZgloszenia}/anuluj",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    return IRZplusResult.Ok($"Zgłoszenie {numerZgloszenia} zostało anulowane");
                }

                return IRZplusResult.Error($"Błąd anulowania: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                return IRZplusResult.Error($"Błąd: {ex.Message}");
            }
        }

        private IRZplusApiResponse TryParseErrorResponse(string responseBody)
        {
            try
            {
                return JsonSerializer.Deserialize<IRZplusApiResponse>(responseBody, _jsonOptions);
            }
            catch { return null; }
        }

        #endregion

        #region Local Storage

        private async Task SaveLocalCopyAsync(ZgloszenieZbiorczeRequest request, string json)
        {
            try
            {
                var dir = _settings.LocalExportPath ?? _historyDbPath;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var dataUboju = request.Dyspozycje.FirstOrDefault()?.DataUboju ?? DateTime.Now.ToString("yyyy-MM-dd");
                var fileName = $"IRZplus_{dataUboju}_{DateTime.Now:HHmmss}.json";
                var filePath = Path.Combine(dir, fileName);

                await File.WriteAllTextAsync(filePath, json);
            }
            catch { /* Ignoruj błędy zapisu lokalnego */ }
        }

        private async Task SaveToHistoryAsync(ZgloszenieZbiorczeRequest request, IRZplusApiResponse response, string responseJson)
        {
            try
            {
                var historyEntry = new IRZplusLocalHistory
                {
                    DataWyslania = DateTime.Now,
                    NumerZgloszenia = response?.NumerZgloszenia ?? "N/A",
                    Status = response?.Status ?? (response?.Success == true ? "WYSLANE" : "BLAD"),
                    DataUboju = DateTime.TryParse(request.Dyspozycje.FirstOrDefault()?.DataUboju, out var du) ? du : DateTime.Now,
                    IloscDyspozycji = request.Dyspozycje.Count,
                    SumaIloscSztuk = request.Dyspozycje.Sum(d => d.IloscSztuk),
                    SumaWagaKg = request.Dyspozycje.Sum(d => d.WagaKg),
                    UzytkownikId = App.UserID ?? Environment.UserName,
                    RequestJson = JsonSerializer.Serialize(request, _jsonOptions),
                    ResponseJson = responseJson
                };

                var historyFile = Path.Combine(_historyDbPath, $"history_{DateTime.Now:yyyyMM}.json");

                List<IRZplusLocalHistory> history;
                if (File.Exists(historyFile))
                {
                    var existingJson = await File.ReadAllTextAsync(historyFile);
                    history = JsonSerializer.Deserialize<List<IRZplusLocalHistory>>(existingJson, _jsonOptions) ?? new List<IRZplusLocalHistory>();
                }
                else
                {
                    history = new List<IRZplusLocalHistory>();
                }

                historyEntry.Id = history.Count + 1;
                history.Add(historyEntry);

                await File.WriteAllTextAsync(historyFile, JsonSerializer.Serialize(history, _jsonOptions));
            }
            catch { /* Ignoruj błędy historii */ }
        }

        /// <summary>
        /// Pobiera lokalną historię wysyłek
        /// </summary>
        public List<IRZplusLocalHistory> GetLocalHistory(DateTime? dataOd = null, DateTime? dataDo = null)
        {
            var result = new List<IRZplusLocalHistory>();

            try
            {
                var historyFiles = Directory.GetFiles(_historyDbPath, "history_*.json");

                foreach (var file in historyFiles)
                {
                    var json = File.ReadAllText(file);
                    var entries = JsonSerializer.Deserialize<List<IRZplusLocalHistory>>(json, _jsonOptions);
                    if (entries != null)
                    {
                        foreach (var entry in entries)
                        {
                            if (dataOd.HasValue && entry.DataUboju < dataOd.Value) continue;
                            if (dataDo.HasValue && entry.DataUboju > dataDo.Value) continue;
                            result.Add(entry);
                        }
                    }
                }
            }
            catch { }

            return result.OrderByDescending(x => x.DataWyslania).ToList();
        }

        #endregion

        #region Data Conversion

        /// <summary>
        /// Konwertuje specyfikacje z bazy danych na format IRZplus
        /// </summary>
        public ZgloszenieZbiorczeRequest ConvertToZgloszenie(List<SpecyfikacjaDoIRZplus> specyfikacje)
        {
            var request = new ZgloszenieZbiorczeRequest
            {
                NumerUbojni = _settings.NumerUbojni,
                DataZgloszenia = DateTime.Now.ToString("yyyy-MM-dd")
            };

            foreach (var spec in specyfikacje.Where(s => s.Wybrana))
            {
                request.Dyspozycje.Add(new DyspozycjaZZSSD
                {
                    DataUboju = spec.DataZdarzenia.ToString("yyyy-MM-dd"),
                    NumerSiedliska = spec.IRZPlus ?? "",
                    NumerUbojni = _settings.NumerUbojni,
                    GatunekDrobiu = "KURCZAK",
                    IloscSztuk = spec.LiczbaSztukDrobiu,
                    WagaKg = spec.WagaNetto,
                    IloscPadlych = spec.SztukiPadle,
                    NumerPartii = spec.NumerPartii ?? "",
                    NumerDokumentuPrzewozowego = ""
                });
            }

            return request;
        }

        /// <summary>
        /// Pobiera specyfikacje z bazy danych dla danej daty uboju
        /// </summary>
        public async Task<List<SpecyfikacjaDoIRZplus>> GetSpecyfikacjeAsync(string connectionString, DateTime dataUboju)
        {
            var result = new List<SpecyfikacjaDoIRZplus>();

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Zapytanie zgodne z formatem Excel ARiMR
                    // Pobieramy numer partii z tabeli PartiaDostawca przez PartiaGuid
                    var query = @"
                        SELECT
                            fc.ID,
                            fc.CalcDate AS DataZdarzenia,
                            ISNULL(d.ShortName, 'Nieznany') AS Hodowca,
                            LTRIM(RTRIM(ISNULL(fc.CustomerGID, ''))) AS IdHodowcy,
                            ISNULL(d.IRZPlus, '') AS IRZPlus,
                            ISNULL(pd.Partia, '') AS NumerPartii,
                            ISNULL(fc.DeclI1, 0) AS SztukiWszystkie,
                            ISNULL(fc.DeclI2, 0) AS SztukiPadle,
                            ISNULL(fc.DeclI3, 0) + ISNULL(fc.DeclI4, 0) + ISNULL(fc.DeclI5, 0) AS SztukiKonfiskaty,
                            ISNULL(fc.NettoWeight, 0) AS WagaNetto,
                            fc.CarLp AS LP
                        FROM dbo.FarmerCalc fc
                        LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(fc.CustomerGID)) = LTRIM(RTRIM(d.ID))
                        LEFT JOIN dbo.PartiaDostawca pd ON fc.PartiaGuid = pd.guid
                        WHERE CAST(fc.CalcDate AS DATE) = @DataUboju
                        ORDER BY fc.CarLp";

                    using (var cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@DataUboju", dataUboju.Date);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int sztukiWszystkie = reader.IsDBNull(reader.GetOrdinal("SztukiWszystkie")) ? 0 : reader.GetInt32(reader.GetOrdinal("SztukiWszystkie"));
                                int sztukiPadle = reader.IsDBNull(reader.GetOrdinal("SztukiPadle")) ? 0 : reader.GetInt32(reader.GetOrdinal("SztukiPadle"));
                                int sztukiKonfiskaty = reader.IsDBNull(reader.GetOrdinal("SztukiKonfiskaty")) ? 0 : reader.GetInt32(reader.GetOrdinal("SztukiKonfiskaty"));

                                // Liczba sztuk zdatnych = wszystkie - padłe - konfiskaty
                                int liczbaSztukZdatnych = sztukiWszystkie - sztukiPadle - sztukiKonfiskaty;
                                if (liczbaSztukZdatnych < 0) liczbaSztukZdatnych = 0;

                                result.Add(new SpecyfikacjaDoIRZplus
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("ID")),
                                    DataZdarzenia = reader.GetDateTime(reader.GetOrdinal("DataZdarzenia")),
                                    Hodowca = reader.IsDBNull(reader.GetOrdinal("Hodowca")) ? "Nieznany" : reader.GetString(reader.GetOrdinal("Hodowca")),
                                    IdHodowcy = reader.IsDBNull(reader.GetOrdinal("IdHodowcy")) ? "" : reader.GetString(reader.GetOrdinal("IdHodowcy")),
                                    IRZPlus = reader.IsDBNull(reader.GetOrdinal("IRZPlus")) ? "" : reader.GetString(reader.GetOrdinal("IRZPlus")),
                                    NumerPartii = reader.IsDBNull(reader.GetOrdinal("NumerPartii")) ? "" : reader.GetString(reader.GetOrdinal("NumerPartii")),
                                    LiczbaSztukDrobiu = liczbaSztukZdatnych,
                                    SztukiWszystkie = sztukiWszystkie,
                                    SztukiPadle = sztukiPadle,
                                    SztukiKonfiskaty = sztukiKonfiskaty,
                                    WagaNetto = reader.IsDBNull(reader.GetOrdinal("WagaNetto")) ? 0 : reader.GetDecimal(reader.GetOrdinal("WagaNetto")),
                                    TypZdarzenia = "Przybycie do rzeźni i ubój",
                                    KrajWywozu = "PL",
                                    Wybrana = true
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd pobierania specyfikacji: {ex.Message}", ex);
            }

            return result;
        }

        /// <summary>
        /// Tworzy podsumowanie dla podglądu przed wysyłką
        /// </summary>
        public IRZplusPodsumowanie CreatePodsumowanie(List<SpecyfikacjaDoIRZplus> specyfikacje)
        {
            var wybrane = specyfikacje.Where(s => s.Wybrana).ToList();

            return new IRZplusPodsumowanie
            {
                DataUboju = wybrane.FirstOrDefault()?.DataUboju ?? DateTime.Now,
                LiczbaSpecyfikacji = wybrane.Count,
                SumaIloscSztuk = wybrane.Sum(s => s.IloscSztuk),
                SumaWagaNetto = wybrane.Sum(s => s.WagaNetto),
                SumaIloscPadlych = wybrane.Sum(s => s.IloscPadlych),
                Specyfikacje = wybrane
            };
        }

        #endregion

        #region Database Logging

        /// <summary>
        /// Loguje wysyłkę IRZplus do bazy danych
        /// </summary>
        public async Task LogToDatabase(string connectionString, ZgloszenieZbiorczeRequest request, IRZplusResult result, string userId, string userName)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Upewnij się, że tabela istnieje
                    var createTableQuery = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'IRZplusLog')
                        CREATE TABLE IRZplusLog (
                            ID INT IDENTITY(1,1) PRIMARY KEY,
                            DataWyslania DATETIME NOT NULL,
                            NumerZgloszenia NVARCHAR(100),
                            Status NVARCHAR(50),
                            DataUboju DATE,
                            IloscDyspozycji INT,
                            SumaIloscSztuk INT,
                            SumaWagaKg DECIMAL(18,2),
                            UzytkownikId NVARCHAR(50),
                            UzytkownikNazwa NVARCHAR(200),
                            Uwagi NVARCHAR(MAX),
                            RequestJson NVARCHAR(MAX),
                            ResponseMessage NVARCHAR(MAX),
                            Srodowisko NVARCHAR(10)
                        )";

                    using (var cmd = new SqlCommand(createTableQuery, connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Wstaw log
                    var insertQuery = @"
                        INSERT INTO IRZplusLog
                        (DataWyslania, NumerZgloszenia, Status, DataUboju, IloscDyspozycji,
                         SumaIloscSztuk, SumaWagaKg, UzytkownikId, UzytkownikNazwa,
                         Uwagi, RequestJson, ResponseMessage, Srodowisko)
                        VALUES
                        (@DataWyslania, @NumerZgloszenia, @Status, @DataUboju, @IloscDyspozycji,
                         @SumaIloscSztuk, @SumaWagaKg, @UzytkownikId, @UzytkownikNazwa,
                         @Uwagi, @RequestJson, @ResponseMessage, @Srodowisko)";

                    using (var cmd = new SqlCommand(insertQuery, connection))
                    {
                        var dataUboju = DateTime.TryParse(request.Dyspozycje.FirstOrDefault()?.DataUboju, out var du) ? du : DateTime.Now;

                        cmd.Parameters.AddWithValue("@DataWyslania", DateTime.Now);
                        cmd.Parameters.AddWithValue("@NumerZgloszenia", (object)result.NumerZgloszenia ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Status", result.Success ? "WYSLANE" : "BLAD");
                        cmd.Parameters.AddWithValue("@DataUboju", dataUboju.Date);
                        cmd.Parameters.AddWithValue("@IloscDyspozycji", request.Dyspozycje.Count);
                        cmd.Parameters.AddWithValue("@SumaIloscSztuk", request.Dyspozycje.Sum(d => d.IloscSztuk));
                        cmd.Parameters.AddWithValue("@SumaWagaKg", request.Dyspozycje.Sum(d => d.WagaKg));
                        cmd.Parameters.AddWithValue("@UzytkownikId", userId ?? "");
                        cmd.Parameters.AddWithValue("@UzytkownikNazwa", userName ?? "");
                        cmd.Parameters.AddWithValue("@Uwagi", result.Message ?? "");
                        cmd.Parameters.AddWithValue("@RequestJson", JsonSerializer.Serialize(request, _jsonOptions));
                        cmd.Parameters.AddWithValue("@ResponseMessage", string.Join("; ", result.Errors.Concat(result.Warnings)));
                        cmd.Parameters.AddWithValue("@Srodowisko", _settings.UseTestEnvironment ? "TEST" : "PROD");

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch { /* Nie przerywaj głównej operacji z powodu błędu logowania */ }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
