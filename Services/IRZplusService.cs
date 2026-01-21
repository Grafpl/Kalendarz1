using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Services
{
    public class IRZplusService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private IRZplusSettings _settings;
        private string _accessToken;
        private DateTime _tokenExpiry;

        private const string TOKEN_URL = "https://sso.arimr.gov.pl/auth/realms/ewniosekplus/protocol/openid-connect/token";
        private const string DEFAULT_CLIENT_ID = "aplikacja-irzplus";
        private const string API_URL_PROD = "https://irz.arimr.gov.pl/api/drob/dokument/api/prod/zurd";
        private const string API_URL_TEST = "https://irz.arimr.gov.pl/api/drob/dokument/api/test/zurd";

        private readonly string _settingsPath;
        private readonly string _historyPath;

        public IRZplusService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var irzPlusFolder = Path.Combine(appData, "ZPSP", "IRZplus");

            if (!Directory.Exists(irzPlusFolder))
                Directory.CreateDirectory(irzPlusFolder);

            _settingsPath = Path.Combine(irzPlusFolder, "settings.json");
            _historyPath = Path.Combine(irzPlusFolder, "history.json");

            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<IRZplusSettings>(json);
                }
            }
            catch { }

            if (_settings == null)
            {
                _settings = new IRZplusSettings
                {
                    NumerUbojni = "039806095-001",  // Numer działalności rzeźni w IRZplus
                    NazwaUbojni = "Ubojnia Drobiu Piórkowscy",
                    ClientId = DEFAULT_CLIENT_ID,
                    ClientSecret = "",
                    Username = "039806095",  // Numer producenta
                    Password = "Jpiorkowski51",
                    UseTestEnvironment = true,  // ŚRODOWISKO TESTOWE
                    SaveLocalCopy = true,
                    AutoSendOnSave = false,
                    LocalExportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IRZplus_Export")
                };
            }

            if (string.IsNullOrEmpty(_settings.ClientId))
                _settings.ClientId = DEFAULT_CLIENT_ID;
        }

        public IRZplusSettings GetSettings() => _settings;

        public void SaveSettings(IRZplusSettings settings)
        {
            _settings = settings;
            if (string.IsNullOrEmpty(_settings.ClientId))
                _settings.ClientId = DEFAULT_CLIENT_ID;

            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
            _accessToken = null;
            _tokenExpiry = DateTime.MinValue;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiry)
                return _accessToken;

            if (string.IsNullOrWhiteSpace(_settings.Username))
                throw new Exception("Nazwa użytkownika nie jest ustawiona!");

            if (string.IsNullOrWhiteSpace(_settings.Password))
                throw new Exception("Hasło nie jest ustawione!");

            var clientId = string.IsNullOrEmpty(_settings.ClientId) ? DEFAULT_CLIENT_ID : _settings.ClientId;

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("username", _settings.Username),
                new KeyValuePair<string, string>("password", _settings.Password)
            });

            var response = await _httpClient.PostAsync(TOKEN_URL, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                string errorMessage = $"Kod: {(int)response.StatusCode}";
                try
                {
                    var errorJson = JsonDocument.Parse(responseBody);
                    if (errorJson.RootElement.TryGetProperty("error_description", out var desc))
                        errorMessage = desc.GetString();
                }
                catch { }
                throw new Exception($"Błąd autoryzacji ARiMR: {errorMessage}");
            }

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody);
            _accessToken = tokenResponse.access_token;
            _tokenExpiry = DateTime.Now.AddSeconds(tokenResponse.expires_in - 60);
            return _accessToken;
        }

        public async Task<IRZplusResult> TestConnectionAsync()
        {
            try
            {
                await GetAccessTokenAsync();
                return new IRZplusResult { Success = true, Message = "Połączenie OK! Token uzyskany pomyślnie." };
            }
            catch (Exception ex)
            {
                return new IRZplusResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<List<SpecyfikacjaDoIRZplus>> GetSpecyfikacjeAsync(string connectionString, DateTime dataUboju)
        {
            var result = new List<SpecyfikacjaDoIRZplus>();
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // Rozszerzony SQL - pobiera wszystkie dane potrzebne do IRZplus
            // Dodane: DeclI4, DeclI5 (konfiskaty), NettoWeight (do obliczenia średniej wagi)
            // Dodane: NrDokArimr, Przybycie, Padniecia (pola edytowalne)
            var sql = @"SELECT
                    fc.ID,
                    fc.CalcDate,
                    fc.CustomerGID,
                    fc.LumQnt,
                    ISNULL(fc.DeclI2, 0) as DeclI2,
                    ISNULL(fc.DeclI3, 0) as DeclI3,
                    ISNULL(fc.DeclI4, 0) as DeclI4,
                    ISNULL(fc.DeclI5, 0) as DeclI5,
                    fc.PayWgt,
                    fc.NettoWeight,
                    fc.PartiaNumber,
                    d.ShortName as Hodowca,
                    d.IRZPlus,
                    fc.NrDokArimr,
                    fc.Przybycie,
                    fc.PadnieciaIRZ
                FROM dbo.FarmerCalc fc
                LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(fc.CustomerGID)) = LTRIM(RTRIM(d.ID))
                WHERE fc.CalcDate = @DataUboju AND ISNULL(fc.LumQnt, 0) > 0
                ORDER BY fc.CarLp ASC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DataUboju", dataUboju.Date);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var lumQnt = reader["LumQnt"] != DBNull.Value ? Convert.ToInt32(reader["LumQnt"]) : 0;
                var declI2 = Convert.ToInt32(reader["DeclI2"]); // padłe
                var declI3 = Convert.ToInt32(reader["DeclI3"]); // CH
                var declI4 = Convert.ToInt32(reader["DeclI4"]); // NW
                var declI5 = Convert.ToInt32(reader["DeclI5"]); // ZM
                var payWgt = reader["PayWgt"] != DBNull.Value ? Convert.ToDecimal(reader["PayWgt"]) : 0;
                var nettoWeight = reader["NettoWeight"] != DBNull.Value ? Convert.ToDecimal(reader["NettoWeight"]) : 0;

                // Oblicz średnią wagę: NettoWeight / (LumQnt + DeclI2)
                // LumQnt = sztuki z LUMEL, DeclI2 = padłe
                decimal sredniaWaga = (lumQnt + declI2) > 0 ? nettoWeight / (lumQnt + declI2) : 0;

                // Konfiskaty suma = CH + NW + ZM
                int konfiskatySuma = declI3 + declI4 + declI5;

                // Zdatne = LumQnt - CH - NW - ZM (bez padłych)
                int zdatne = lumQnt - declI3 - declI4 - declI5;

                result.Add(new SpecyfikacjaDoIRZplus
                {
                    Id = reader.GetInt32(reader.GetOrdinal("ID")),
                    Hodowca = reader["Hodowca"]?.ToString()?.Trim() ?? "",
                    IdHodowcy = reader["CustomerGID"]?.ToString()?.Trim() ?? "",
                    IRZPlus = reader["IRZPlus"]?.ToString()?.Trim() ?? "",
                    NumerPartii = reader["PartiaNumber"]?.ToString()?.Trim() ?? "",
                    LiczbaSztukDrobiu = zdatne,  // Zdatne = LumQnt - Padle - Konfiskaty
                    DataZdarzenia = reader.GetDateTime(reader.GetOrdinal("CalcDate")),
                    SztukiWszystkie = lumQnt,
                    SztukiPadle = declI2,
                    SztukiKonfiskaty = konfiskatySuma,
                    WagaNetto = payWgt,
                    KgDoZaplaty = payWgt,
                    KgKonfiskat = konfiskatySuma,  // Teraz to SZTUKI konfiskat, nie kg
                    KgPadlych = declI2,  // Teraz to SZTUKI padłych, nie kg
                    // Pola edytowalne - pobierz z bazy jeśli istnieją
                    NrDokArimr = reader["NrDokArimr"]?.ToString() ?? "",
                    Przybycie = reader["Przybycie"]?.ToString() ?? "",
                    Padniecia = reader["PadnieciaIRZ"]?.ToString() ?? "",
                    Wybrana = true
                });
            }
            return result;
        }

        /// <summary>
        /// Upewnia się że kolumny IRZplus istnieją w tabeli FarmerCalc
        /// </summary>
        public async Task EnsureIRZplusColumnsExistAsync(string connectionString)
        {
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FarmerCalc') AND name = 'NrDokArimr')
                    ALTER TABLE dbo.FarmerCalc ADD NrDokArimr NVARCHAR(100) NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FarmerCalc') AND name = 'Przybycie')
                    ALTER TABLE dbo.FarmerCalc ADD Przybycie NVARCHAR(100) NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FarmerCalc') AND name = 'PadnieciaIRZ')
                    ALTER TABLE dbo.FarmerCalc ADD PadnieciaIRZ NVARCHAR(100) NULL;
            ";

            using var cmd = new SqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Zapisuje pola edytowalne IRZplus do bazy danych
        /// </summary>
        public async Task<bool> SaveIRZplusFieldsAsync(string connectionString, int farmerCalcId, string nrDokArimr, string przybycie, string padniecia)
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                var sql = @"UPDATE dbo.FarmerCalc
                    SET NrDokArimr = @NrDokArimr,
                        Przybycie = @Przybycie,
                        PadnieciaIRZ = @Padniecia
                    WHERE ID = @Id";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", farmerCalcId);
                cmd.Parameters.AddWithValue("@NrDokArimr", (object)nrDokArimr ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Przybycie", (object)przybycie ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Padniecia", (object)padniecia ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Zapisuje tylko numer dokumentu ARIMR do bazy danych
        /// </summary>
        public async Task<bool> SaveNrDokArimrAsync(string connectionString, int farmerCalcId, string nrDokArimr)
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                var sql = @"UPDATE dbo.FarmerCalc
                    SET NrDokArimr = @NrDokArimr
                    WHERE ID = @Id";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", farmerCalcId);
                cmd.Parameters.AddWithValue("@NrDokArimr", (object)nrDokArimr ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<IRZplusResult> WyslijZgloszenieAsync(ZgloszenieZbiorczeRequest request)
        {
            try
            {
                var token = await GetAccessTokenAsync();

                // PRZEKSZTAŁĆ na strukturę zgodną z API ARiMR
                var dyspozycja = new DyspozycjaZURD
                {
                    NumerProducenta = _settings.Username,  // "039806095"
                    Zgloszenie = new ZgloszenieZURDDTO
                    {
                        NumerRzezni = _settings.NumerUbojni,  // "039806095-001"
                        NumerPartiiUboju = DateTime.Now.ToString("yy") + DateTime.Now.DayOfYear.ToString("000") + "001",
                        Gatunek = new KodOpisDto { Kod = "KURY" },
                        Pozycje = request.Dyspozycje.Select((d, idx) => new PozycjaZURDDTO
                        {
                            Lp = idx + 1,
                            NumerIdenPartiiDrobiu = d.NumerSiedliska,
                            LiczbaDrobiu = d.IloscSztuk,
                            MasaDrobiu = d.WagaKg,  // WYMAGANE! K1231
                            TypZdarzenia = new KodOpisDto { Kod = "ZURDUR" }, // ZURDUR = przybycie do rzeźni i ubój drobiu
                            DataZdarzenia = request.DataUboju.ToString("yyyy-MM-dd"),
                            DataKupnaWwozu = request.DataUboju.ToString("yyyy-MM-dd"),  // WYMAGANE! K0181
                            // NumerSiedliska juz zawiera pelny numer np. "038481631-001" - NIE DODAWAC -001!
                            PrzyjeteZDzialalnosci = d.NumerSiedliska,
                            UbojRytualny = false
                        }).ToList()
                    }
                };

                // Serializuj NOWĄ strukturę - NIE ignoruj null! masaDrobiu i dataKupnaWwozu sa WYMAGANE!
                var json = JsonSerializer.Serialize(dyspozycja, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });

                // Debug - zapisz JSON do pliku
                var debugPath = Path.Combine(_settings.LocalExportPath ?? "C:\\temp", $"debug_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                Directory.CreateDirectory(Path.GetDirectoryName(debugPath));
                File.WriteAllText(debugPath, json);

                // Wybierz URL
                var url = _settings.UseTestEnvironment
                    ? "https://irz.arimr.gov.pl/api/drob/dokument/api/test/zurd"
                    : "https://irz.arimr.gov.pl/api/drob/dokument/api/prod/zurd";

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                // Zapisz odpowiedź
                File.WriteAllText(debugPath.Replace("debug_", "response_"), responseBody);

                if (response.IsSuccessStatusCode)
                {
                    var numerZgloszenia = TryGetNumerZgloszenia(responseBody);
                    return new IRZplusResult { Success = true, Message = "Wysłano pomyślnie!", NumerZgloszenia = numerZgloszenia, ResponseData = responseBody };
                }
                else
                {
                    return new IRZplusResult { Success = false, Message = $"Błąd: {response.StatusCode}\n{responseBody}" };
                }
            }
            catch (Exception ex)
            {
                return new IRZplusResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<IRZplusResult> GetStatusZgloszeniaAsync(string numerZgloszenia)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var baseUrl = _settings.UseTestEnvironment
                    ? "https://irz.arimr.gov.pl/api/drob/dokument/api/test"
                    : "https://irz.arimr.gov.pl/api/drob/dokument/api/prod";
                var url = $"{baseUrl}/dokumentyZlozone?numerProducenta={_settings.Username}&numerDokumentu={numerZgloszenia}";
                var response = await _httpClient.GetAsync(url);
                var body = await response.Content.ReadAsStringAsync();
                return new IRZplusResult { Success = response.IsSuccessStatusCode, Message = body };
            }
            catch (Exception ex) { return new IRZplusResult { Success = false, Message = ex.Message }; }
        }

        private void SaveLocalCopy(object data, string prefix)
        {
            try
            {
                var path = _settings.LocalExportPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IRZplus_Export");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, $"{prefix}_{DateTime.Now:yyyy-MM-dd_HHmmss}.json"),
                    JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void SaveHistory(IRZplusLocalHistory entry)
        {
            try
            {
                var history = GetLocalHistory(null, null);
                history.Insert(0, entry);
                if (history.Count > 1000) history = history.Take(1000).ToList();
                File.WriteAllText(_historyPath, JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public List<IRZplusLocalHistory> GetLocalHistory(DateTime? from, DateTime? to)
        {
            try
            {
                if (!File.Exists(_historyPath)) return new List<IRZplusLocalHistory>();
                var history = JsonSerializer.Deserialize<List<IRZplusLocalHistory>>(File.ReadAllText(_historyPath)) ?? new List<IRZplusLocalHistory>();
                if (from.HasValue) history = history.Where(h => h.DataWyslania >= from.Value).ToList();
                if (to.HasValue) history = history.Where(h => h.DataWyslania <= to.Value.AddDays(1)).ToList();
                return history.OrderByDescending(h => h.DataWyslania).ToList();
            }
            catch { return new List<IRZplusLocalHistory>(); }
        }

        private string TryGetNumerZgloszenia(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("numerZgloszenia", out var n)) return n.GetString();
                if (doc.RootElement.TryGetProperty("numer", out var n2)) return n2.GetString();
                if (doc.RootElement.TryGetProperty("id", out var id)) return id.GetString();
            }
            catch { }
            return "N/A";
        }

        public void Dispose() => _httpClient?.Dispose();

        // ============ NOWE METODY ZURD (format ARiMR) ============

        /// <summary>
        /// Konwertuje specyfikacje do formatu ZURD wymaganego przez API ARiMR
        /// </summary>
        public DyspozycjaZURD ConvertToZURD(List<SpecyfikacjaDoIRZplus> specyfikacje, string numerPartiiUboju = null)
        {
            var wybrane = specyfikacje.Where(s => s.Wybrana).ToList();
            var dataUboju = wybrane.FirstOrDefault()?.DataZdarzenia ?? DateTime.Now;

            // Numer partii uboju - jeśli nie podany, generuj automatycznie (RRMMDDNNN)
            if (string.IsNullOrEmpty(numerPartiiUboju))
            {
                numerPartiiUboju = $"{dataUboju:yyMMdd}001";
            }

            var zurd = new DyspozycjaZURD
            {
                NumerProducenta = _settings.Username, // numer producenta ARiMR
                Zgloszenie = new ZgloszenieZURDDTO
                {
                    NumerRzezni = _settings.NumerUbojni,
                    NumerPartiiUboju = numerPartiiUboju,
                    Gatunek = new KodOpisDto { Kod = "KURY" },
                    Pozycje = new List<PozycjaZURDDTO>()
                }
            };

            long lp = 1;
            foreach (var spec in wybrane)
            {
                var irzPlus = spec.IRZPlus ?? "";
                zurd.Zgloszenie.Pozycje.Add(new PozycjaZURDDTO
                {
                    Lp = lp++,
                    NumerIdenPartiiDrobiu = irzPlus,  // np. "080640491-001"
                    LiczbaDrobiu = spec.LiczbaSztukDrobiu,
                    MasaDrobiu = spec.WagaNetto,  // WYMAGANE! K1231 - Pole Masa drobiu jest wymagane
                    TypZdarzenia = new KodOpisDto { Kod = "ZURDUR" }, // ZURDUR = przybycie do rzeźni i ubój drobiu
                    DataZdarzenia = spec.DataZdarzenia.ToString("yyyy-MM-dd"),
                    DataKupnaWwozu = spec.DataZdarzenia.ToString("yyyy-MM-dd"),  // WYMAGANE! K0181 - Pole Data kupna/wwozu
                    // irzPlus juz zawiera pelny numer np. "080640491-001" - NIE DODAWAC -001!
                    PrzyjeteZDzialalnosci = irzPlus,
                    UbojRytualny = false
                });
            }

            return zurd;
        }

        /// <summary>
        /// Wysyła zgłoszenie ZURD do API ARiMR
        /// </summary>
        public async Task<IRZplusResult> WyslijZURDAsync(DyspozycjaZURD zurd)
        {
            try
            {
                var token = await GetAccessTokenAsync();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                    // NIE uzywac WhenWritingNull - masaDrobiu i dataKupnaWwozu sa WYMAGANE!
                };
                var json = JsonSerializer.Serialize(zurd, jsonOptions);

                // Zapisz debug JSON
                var exportPath = _settings.LocalExportPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IRZplus_Export");
                if (!Directory.Exists(exportPath)) Directory.CreateDirectory(exportPath);
                File.WriteAllText(Path.Combine(exportPath, $"debug_zurd_{DateTime.Now:yyyyMMdd_HHmmss}.json"), json);

                // DEBUG: Pokaż JSON i zapytaj o potwierdzenie
                var dialogResult = MessageBox.Show(
                    json,
                    "JSON do wysłania - Czy wysłać?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (dialogResult != DialogResult.Yes)
                {
                    return new IRZplusResult { Success = false, Message = "Anulowano przez użytkownika" };
                }

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = _settings.UseTestEnvironment ? API_URL_TEST : API_URL_PROD;
                var response = await _httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                // Zapisz odpowiedź
                File.WriteAllText(Path.Combine(exportPath, $"response_zurd_{DateTime.Now:yyyyMMdd_HHmmss}.json"), responseBody);

                if (response.IsSuccessStatusCode)
                {
                    var numerZgloszenia = TryGetNumerZgloszenia(responseBody);
                    SaveHistory(new IRZplusLocalHistory
                    {
                        DataWyslania = DateTime.Now,
                        DataUboju = DateTime.TryParse(zurd.Zgloszenie.Pozycje.FirstOrDefault()?.DataZdarzenia, out var d) ? d : DateTime.Now,
                        NumerZgloszenia = numerZgloszenia,
                        Status = "WYSLANE",
                        IloscDyspozycji = zurd.Zgloszenie.Pozycje.Count,
                        SumaIloscSztuk = zurd.Zgloszenie.Pozycje.Sum(p => p.LiczbaDrobiu),
                        SumaWagaKg = 0,
                        RequestJson = json,
                        ResponseJson = responseBody
                    });
                    return new IRZplusResult { Success = true, Message = "Wysłano pomyślnie!", NumerZgloszenia = numerZgloszenia, ResponseData = responseBody };
                }

                return new IRZplusResult { Success = false, Message = $"Błąd {(int)response.StatusCode}: {responseBody}", ResponseData = responseBody };
            }
            catch (Exception ex)
            {
                return new IRZplusResult { Success = false, Message = ex.Message };
            }
        }

        // ============ STARE METODY (dla kompatybilności) ============

        // Alias dla kompatybilności z istniejącym kodem
        public Task<IRZplusResult> SendZgloszenieAsync(ZgloszenieZbiorczeRequest request) => WyslijZgloszenieAsync(request);

        public ZgloszenieZbiorczeRequest ConvertToZgloszenie(List<SpecyfikacjaDoIRZplus> specyfikacje)
        {
            var request = new ZgloszenieZbiorczeRequest
            {
                NumerUbojni = _settings.NumerUbojni,
                DataUboju = specyfikacje.FirstOrDefault()?.DataZdarzenia ?? DateTime.Now,
                TypZgloszenia = "UBOJ_DROBIU",
                Dyspozycje = new List<DyspozycjaUboju>()
            };

            foreach (var spec in specyfikacje.Where(s => s.Wybrana))
            {
                request.Dyspozycje.Add(new DyspozycjaUboju
                {
                    NumerSiedliska = spec.IRZPlus ?? "",
                    GatunekDrobiu = "KURCZAK",
                    IloscSztuk = spec.LiczbaSztukDrobiu,
                    WagaKg = spec.WagaNetto,
                    IloscPadlych = spec.SztukiPadle,
                    NumerPartii = spec.NumerPartii ?? ""
                });
            }

            return request;
        }

        public async Task LogToDatabase(string connectionString, ZgloszenieZbiorczeRequest request, IRZplusResult result, string userId, string userName)
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                var createTableSql = @"
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
                        Uwagi NVARCHAR(MAX)
                    )";

                using (var cmd = new SqlCommand(createTableSql, conn))
                    await cmd.ExecuteNonQueryAsync();

                var insertSql = @"
                    INSERT INTO IRZplusLog (DataWyslania, NumerZgloszenia, Status, DataUboju, IloscDyspozycji, SumaIloscSztuk, SumaWagaKg, UzytkownikId, UzytkownikNazwa, Uwagi)
                    VALUES (@DataWyslania, @NumerZgloszenia, @Status, @DataUboju, @IloscDyspozycji, @SumaIloscSztuk, @SumaWagaKg, @UzytkownikId, @UzytkownikNazwa, @Uwagi)";

                using var insertCmd = new SqlCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@DataWyslania", DateTime.Now);
                insertCmd.Parameters.AddWithValue("@NumerZgloszenia", (object)result.NumerZgloszenia ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Status", result.Success ? "WYSLANE" : "BLAD");
                insertCmd.Parameters.AddWithValue("@DataUboju", request.DataUboju.Date);
                insertCmd.Parameters.AddWithValue("@IloscDyspozycji", request.Dyspozycje?.Count ?? 0);
                insertCmd.Parameters.AddWithValue("@SumaIloscSztuk", request.Dyspozycje?.Sum(d => d.IloscSztuk) ?? 0);
                insertCmd.Parameters.AddWithValue("@SumaWagaKg", request.Dyspozycje?.Sum(d => d.WagaKg) ?? 0);
                insertCmd.Parameters.AddWithValue("@UzytkownikId", userId ?? "");
                insertCmd.Parameters.AddWithValue("@UzytkownikNazwa", userName ?? "");
                insertCmd.Parameters.AddWithValue("@Uwagi", result.Message ?? "");

                await insertCmd.ExecuteNonQueryAsync();
            }
            catch { /* Nie przerywaj głównej operacji */ }
        }
    }

    public class IRZplusSettings
    {
        public string NumerUbojni { get; set; }
        public string NazwaUbojni { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UseTestEnvironment { get; set; }
        public bool SaveLocalCopy { get; set; }
        public bool AutoSendOnSave { get; set; }
        public string LocalExportPath { get; set; }
    }

    public class IRZplusResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string NumerZgloszenia { get; set; }
        public string ResponseData { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class TokenResponse
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string refresh_token { get; set; }
    }

    public class SpecyfikacjaDoIRZplus
    {
        public int Id { get; set; }
        public string Hodowca { get; set; }
        public string IdHodowcy { get; set; }
        public string IRZPlus { get; set; }
        public string NumerPartii { get; set; }
        public int LiczbaSztukDrobiu { get; set; }
        public string TypZdarzenia { get; set; } = "Przybycie do rzeźni i ubój";
        public DateTime DataZdarzenia { get; set; }
        public string KrajWywozu { get; set; } = "PL";
        public string NrDokArimr { get; set; }
        public string Przybycie { get; set; }
        public string Padniecia { get; set; }
        public int SztukiWszystkie { get; set; }
        public int SztukiPadle { get; set; }
        public int SztukiKonfiskaty { get; set; }
        public decimal WagaNetto { get; set; }
        public decimal KgDoZaplaty { get; set; }
        public decimal KgKonfiskat { get; set; }
        public decimal KgPadlych { get; set; }
        public bool Wybrana { get; set; }
    }

    // ============ STARE KLASY (dla kompatybilności) ============
    public class ZgloszenieZbiorczeRequest
    {
        public string NumerUbojni { get; set; }
        public DateTime DataUboju { get; set; }
        public string TypZgloszenia { get; set; }
        public List<DyspozycjaUboju> Dyspozycje { get; set; }
    }

    public class DyspozycjaUboju
    {
        public string NumerSiedliska { get; set; }
        public string GatunekDrobiu { get; set; }
        public int IloscSztuk { get; set; }
        public decimal WagaKg { get; set; }
        public int IloscPadlych { get; set; }
        public string NumerPartii { get; set; }
    }

    // ============ NOWE KLASY ARiMR ZURD ============
    public class DyspozycjaZURD
    {
        [JsonPropertyName("numerProducenta")]
        public string NumerProducenta { get; set; }  // "039806095"

        [JsonPropertyName("zgloszenie")]
        public ZgloszenieZURDDTO Zgloszenie { get; set; }
    }

    public class ZgloszenieZURDDTO
    {
        [JsonPropertyName("numerRzezni")]
        public string NumerRzezni { get; set; }  // "039806095-001"

        [JsonPropertyName("numerPartiiUboju")]
        public string NumerPartiiUboju { get; set; }

        [JsonPropertyName("gatunek")]
        public KodOpisDto Gatunek { get; set; }  // {"kod": "KURY"}

        [JsonPropertyName("pozycje")]
        public List<PozycjaZURDDTO> Pozycje { get; set; } = new List<PozycjaZURDDTO>();
    }

    public class PozycjaZURDDTO
    {
        [JsonPropertyName("lp")]
        public long Lp { get; set; }

        [JsonPropertyName("numerIdenPartiiDrobiu")]
        public string NumerIdenPartiiDrobiu { get; set; }  // np. "080640491-001"

        [JsonPropertyName("liczbaDrobiu")]
        public int LiczbaDrobiu { get; set; }

        // WYMAGANE! K1231 - Pole Masa drobiu jest wymagane
        [JsonPropertyName("masaDrobiu")]
        public decimal MasaDrobiu { get; set; }

        [JsonPropertyName("typZdarzenia")]
        public KodOpisDto TypZdarzenia { get; set; }  // {"kod": "ZURDUR"} - przybycie do rzeźni i ubój drobiu

        [JsonPropertyName("dataZdarzenia")]
        public string DataZdarzenia { get; set; }  // format "2025-01-13"

        // WYMAGANE! K0181 - Pole Data kupna/wwozu - Brak danych
        [JsonPropertyName("dataKupnaWwozu")]
        public string DataKupnaWwozu { get; set; }  // format "2025-01-13"

        [JsonPropertyName("przyjeteZDzialalnosci")]
        public string PrzyjeteZDzialalnosci { get; set; }  // np. "080640491-001" (pelny numer siedliska)

        [JsonPropertyName("ubojRytualny")]
        public bool UbojRytualny { get; set; } = false;
    }

    public class KodOpisDto
    {
        [JsonPropertyName("kod")]
        public string Kod { get; set; }

        [JsonPropertyName("opis")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Opis { get; set; }
    }

    public class IRZplusLocalHistory
    {
        public DateTime DataWyslania { get; set; }
        public DateTime DataUboju { get; set; }
        public string NumerZgloszenia { get; set; }
        public string Status { get; set; }
        public int IloscDyspozycji { get; set; }
        public int SumaIloscSztuk { get; set; }
        public decimal SumaWagaKg { get; set; }
        public string RequestJson { get; set; }
        public string ResponseJson { get; set; }
        public string UzytkownikId { get; set; }
        public string UzytkownikNazwa { get; set; }
        public string Uwagi { get; set; }
    }

    public static class KategorieOdpadow
    {
        public static List<string> GetRodzajeForKategoria(string kat) => kat switch
        {
            "KAT1" => new List<string> { "SRM", "Inne KAT1" },
            "KAT2" => new List<string> { "Obornik", "Treść przewodu", "Padłe zwierzęta", "Inne KAT2" },
            "KAT3" => new List<string> { "Pierze", "Krew", "Wnętrzności", "Tłuszcz", "Nogi", "Głowy", "Skóra", "Inne KAT3" },
            _ => new List<string> { "Inne" }
        };
    }

    public class OdpadDoIRZplus
    {
        public int Id { get; set; }
        public DateTime DataWydania { get; set; }
        public string KategoriaOdpadu { get; set; }
        public string RodzajOdpadu { get; set; }
        public decimal IloscKg { get; set; }
        public string OdbiorcaNazwa { get; set; }
        public string OdbiorcaNIP { get; set; }
        public string OdbiorcaWetNr { get; set; }
        public string NumerDokumentu { get; set; }
        public string NumerRejestracyjny { get; set; }
        public string Uwagi { get; set; }
        public bool Wybrana { get; set; } = true;
    }
}
