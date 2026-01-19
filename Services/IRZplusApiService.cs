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

namespace Kalendarz1.Services
{
    /// <summary>
    /// Serwis do komunikacji z API IRZplus (ARiMR) - wyslanie zgloszen ZURD
    /// </summary>
    public class IRZplusApiService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private string _accessToken;
        private DateTime _tokenExpiry;
        private bool _useTestEnv = false;

        // === ENDPOINTY API ===
        private const string AUTH_URL = "https://sso.arimr.gov.pl/auth/realms/ewniosekplus/protocol/openid-connect/token";
        private const string API_PROD_URL = "https://irz.arimr.gov.pl/api/drob/dokument/api/prod/zurd";
        private const string API_TEST_URL = "https://irz.arimr.gov.pl/api/drob/dokument/api/test/zurd";

        // Endpointy do sprawdzania statusu zgloszenia
        private const string API_PROD_STATUS = "https://irz.arimr.gov.pl/api/drob/dokument/api/prod/zurd/{0}";
        private const string API_TEST_STATUS = "https://irz.arimr.gov.pl/api/drob/dokument/api/test/zurd/{0}";

        private const string CLIENT_ID = "aplikacja-irzplus";

        // === STALE DANE RZEZNI ===
        public const string NUMER_PRODUCENTA = "039806095";
        public const string NUMER_RZEZNI = "039806095-001";
        public const string GATUNEK_KOD = "KURY";

        public IRZplusApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Ustawia srodowisko API (testowe lub produkcyjne)
        /// </summary>
        public void SetTestEnvironment(bool useTest)
        {
            _useTestEnv = useTest;
        }

        /// <summary>
        /// Zwraca aktualny URL API w zaleznosci od srodowiska
        /// </summary>
        private string GetApiUrl() => _useTestEnv ? API_TEST_URL : API_PROD_URL;

        /// <summary>
        /// Pobiera token OAuth2 z serwera autoryzacji ARiMR
        /// </summary>
        public async Task<ApiResult> AuthenticateAsync(string username, string password)
        {
            var result = new ApiResult();

            try
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", username),
                    new KeyValuePair<string, string>("password", password),
                    new KeyValuePair<string, string>("client_id", CLIENT_ID),
                    new KeyValuePair<string, string>("grant_type", "password")
                });

                System.Diagnostics.Debug.WriteLine("=== AUTH REQUEST ===");
                System.Diagnostics.Debug.WriteLine($"URL: {AUTH_URL}");
                System.Diagnostics.Debug.WriteLine($"Username: {username}");

                var response = await _httpClient.PostAsync(AUTH_URL, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                result.HttpStatusCode = (int)response.StatusCode;
                result.ResponseJson = responseBody;

                System.Diagnostics.Debug.WriteLine($"=== AUTH RESPONSE ===");
                System.Diagnostics.Debug.WriteLine($"Status: {result.HttpStatusCode}");
                System.Diagnostics.Debug.WriteLine(responseBody);

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonSerializer.Deserialize<ApiTokenResponse>(responseBody);
                    _accessToken = tokenResponse.AccessToken;
                    _tokenExpiry = DateTime.Now.AddSeconds(tokenResponse.ExpiresIn - 60); // 60s zapasu

                    result.Success = true;
                    result.Message = $"Autoryzacja pomyslna\nToken wazny do: {_tokenExpiry:HH:mm:ss}";
                }
                else
                {
                    result.Success = false;
                    result.Message = $"Blad autoryzacji: {response.StatusCode}\n{responseBody}";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Wyjatek podczas autoryzacji: {ex.Message}\n\n{ex.StackTrace}";
            }

            return result;
        }

        /// <summary>
        /// Sprawdza czy token jest wazny
        /// </summary>
        public bool IsTokenValid => !string.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiry;

        /// <summary>
        /// Wysyla zgloszenie ZURD do API z pelna obsluga odpowiedzi
        /// </summary>
        public async Task<ApiResult> WyslijZURDAsync(DyspozycjaZURDApi dyspozycja)
        {
            var result = new ApiResult();

            if (!IsTokenValid)
            {
                result.Success = false;
                result.Message = "Token niewazny. Zaloguj sie ponownie.";
                return result;
            }

            try
            {
                var json = JsonSerializer.Serialize(dyspozycja, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    // UWAGA: NIE uzywac WhenWritingNull/WhenWritingDefault - pola masaDrobiu i dataKupnaWwozu sa WYMAGANE!
                    WriteIndented = true
                });

                // LOGUJ REQUEST
                System.Diagnostics.Debug.WriteLine("=== REQUEST JSON ===");
                System.Diagnostics.Debug.WriteLine(json);

                result.RequestJson = json;

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _accessToken);

                var apiUrl = GetApiUrl();
                System.Diagnostics.Debug.WriteLine($"=== SENDING TO: {apiUrl} ===");

                var response = await _httpClient.PostAsync(apiUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                // LOGUJ RESPONSE
                System.Diagnostics.Debug.WriteLine("=== RESPONSE ===");
                System.Diagnostics.Debug.WriteLine($"Status: {(int)response.StatusCode} {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine(responseBody);

                result.HttpStatusCode = (int)response.StatusCode;
                result.ResponseJson = responseBody;

                if (response.IsSuccessStatusCode)
                {
                    var zurdResponse = JsonSerializer.Deserialize<ZURDResponse>(responseBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    result.Success = true;
                    result.NumerZgloszenia = zurdResponse?.NumerZgloszenia;
                    result.NumerDokumentu = zurdResponse?.NumerDokumentu;
                    result.Status = zurdResponse?.Status;
                    result.StatusKod = zurdResponse?.StatusKod;
                    result.DataUtworzenia = zurdResponse?.DataUtworzenia;
                    result.DataModyfikacji = zurdResponse?.DataModyfikacji;

                    // Zbierz komunikaty
                    if (zurdResponse?.Komunikaty != null)
                        result.Komunikaty.AddRange(zurdResponse.Komunikaty.Select(k => $"[{k.Typ}] {k.Kod}: {k.Tresc}"));

                    if (zurdResponse?.Ostrzezenia != null)
                        result.Ostrzezenia.AddRange(zurdResponse.Ostrzezenia);

                    // Buduj message
                    var sb = new StringBuilder();
                    sb.AppendLine("ZGLOSZENIE PRZYJETE");
                    sb.AppendLine();
                    if (!string.IsNullOrEmpty(result.NumerZgloszenia))
                        sb.AppendLine($"Numer zgloszenia: {result.NumerZgloszenia}");
                    if (!string.IsNullOrEmpty(result.NumerDokumentu))
                        sb.AppendLine($"Numer dokumentu: {result.NumerDokumentu}");
                    if (!string.IsNullOrEmpty(result.Status))
                        sb.AppendLine($"Status: {result.Status}");
                    if (!string.IsNullOrEmpty(result.StatusKod))
                        sb.AppendLine($"Kod statusu: {result.StatusKod}");
                    if (!string.IsNullOrEmpty(result.DataUtworzenia))
                        sb.AppendLine($"Data utworzenia: {result.DataUtworzenia}");
                    if (!string.IsNullOrEmpty(result.DataModyfikacji))
                        sb.AppendLine($"Data modyfikacji: {result.DataModyfikacji}");

                    if (zurdResponse?.Podsumowanie != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine("PODSUMOWANIE:");
                        sb.AppendLine($"   Zaakceptowanych: {zurdResponse.Podsumowanie.LiczbaZaakceptowanych}");
                        sb.AppendLine($"   Odrzuconych: {zurdResponse.Podsumowanie.LiczbaOdrzuconych}");
                        sb.AppendLine($"   Suma sztuk: {zurdResponse.Podsumowanie.SumaSztuk}");
                        sb.AppendLine($"   Suma masy: {zurdResponse.Podsumowanie.SumaMasy} kg");

                        result.Podsumowanie = zurdResponse.Podsumowanie;
                    }

                    if (zurdResponse?.Pozycje != null && zurdResponse.Pozycje.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"POZYCJE ({zurdResponse.Pozycje.Count}):");
                        foreach (var poz in zurdResponse.Pozycje.Take(10))
                        {
                            sb.AppendLine($"   Lp {poz.Lp}: {poz.Status} - {poz.NumerEwidencyjny}");
                        }
                        if (zurdResponse.Pozycje.Count > 10)
                            sb.AppendLine($"   ... i {zurdResponse.Pozycje.Count - 10} wiecej");

                        result.Pozycje = zurdResponse.Pozycje;
                    }

                    if (result.Komunikaty.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("KOMUNIKATY:");
                        foreach (var k in result.Komunikaty)
                            sb.AppendLine($"   {k}");
                    }

                    if (result.Ostrzezenia.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("OSTRZEZENIA:");
                        foreach (var o in result.Ostrzezenia)
                            sb.AppendLine($"   {o}");
                    }

                    result.Message = sb.ToString();
                }
                else
                {
                    result.Success = false;

                    // Probuj sparsowac bledy
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<ZURDResponse>(responseBody,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (errorResponse?.Bledy != null)
                            result.Bledy.AddRange(errorResponse.Bledy.Select(b => $"[{b.Pole}] {b.Kod}: {b.Komunikat}"));

                        if (errorResponse?.Komunikaty != null)
                            result.Komunikaty.AddRange(errorResponse.Komunikaty.Select(k => k.Tresc));
                    }
                    catch { }

                    var sb = new StringBuilder();
                    sb.AppendLine($"BLAD HTTP {result.HttpStatusCode}");
                    sb.AppendLine();

                    if (result.Bledy.Any())
                    {
                        sb.AppendLine("BLEDY WALIDACJI:");
                        foreach (var b in result.Bledy)
                            sb.AppendLine($"  * {b}");
                        sb.AppendLine();
                    }

                    if (result.Komunikaty.Any())
                    {
                        sb.AppendLine("KOMUNIKATY:");
                        foreach (var k in result.Komunikaty)
                            sb.AppendLine($"  * {k}");
                        sb.AppendLine();
                    }

                    sb.AppendLine("SUROWA ODPOWIEDZ:");
                    sb.AppendLine(responseBody.Length > 1000 ? responseBody.Substring(0, 1000) + "..." : responseBody);

                    result.Message = sb.ToString();
                }

                // Loguj do pliku
                LogApiCall(dyspozycja, result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"WYJATEK: {ex.Message}\n\n{ex.StackTrace}";

                // Loguj blad
                LogApiCall(dyspozycja, result);
            }

            return result;
        }

        /// <summary>
        /// Pobiera status zgloszenia po numerze
        /// </summary>
        public async Task<ApiResult> PobierzStatusZgloszeniaAsync(string numerZgloszenia)
        {
            var result = new ApiResult();

            if (!IsTokenValid)
            {
                result.Success = false;
                result.Message = "Token niewazny. Zaloguj sie ponownie.";
                return result;
            }

            try
            {
                var url = string.Format(_useTestEnv ? API_TEST_STATUS : API_PROD_STATUS, numerZgloszenia);

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _accessToken);

                System.Diagnostics.Debug.WriteLine($"=== GET STATUS: {url} ===");

                var response = await _httpClient.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();

                result.HttpStatusCode = (int)response.StatusCode;
                result.ResponseJson = responseBody;
                result.Success = response.IsSuccessStatusCode;

                System.Diagnostics.Debug.WriteLine($"=== STATUS RESPONSE {numerZgloszenia} ===");
                System.Diagnostics.Debug.WriteLine($"HTTP: {result.HttpStatusCode}");
                System.Diagnostics.Debug.WriteLine(responseBody);

                if (response.IsSuccessStatusCode)
                {
                    var status = JsonSerializer.Deserialize<ZURDResponse>(responseBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    result.NumerZgloszenia = status?.NumerZgloszenia;
                    result.NumerDokumentu = status?.NumerDokumentu;
                    result.Status = status?.Status;
                    result.StatusKod = status?.StatusKod;
                    result.DataUtworzenia = status?.DataUtworzenia;
                    result.DataModyfikacji = status?.DataModyfikacji;
                    result.Podsumowanie = status?.Podsumowanie;
                    result.Pozycje = status?.Pozycje;

                    if (status?.Komunikaty != null)
                        result.Komunikaty.AddRange(status.Komunikaty.Select(k => $"[{k.Typ}] {k.Tresc}"));

                    if (status?.Bledy != null)
                        result.Bledy.AddRange(status.Bledy.Select(b => $"[{b.Pole}] {b.Komunikat}"));

                    if (status?.Ostrzezenia != null)
                        result.Ostrzezenia.AddRange(status.Ostrzezenia);

                    var sb = new StringBuilder();
                    sb.AppendLine($"STATUS ZGLOSZENIA: {result.NumerZgloszenia}");
                    sb.AppendLine();
                    sb.AppendLine($"Status: {result.Status}");
                    if (!string.IsNullOrEmpty(result.NumerDokumentu))
                        sb.AppendLine($"Numer dokumentu: {result.NumerDokumentu}");
                    if (!string.IsNullOrEmpty(result.DataUtworzenia))
                        sb.AppendLine($"Data utworzenia: {result.DataUtworzenia}");
                    if (!string.IsNullOrEmpty(result.DataModyfikacji))
                        sb.AppendLine($"Data modyfikacji: {result.DataModyfikacji}");

                    if (result.Podsumowanie != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine("PODSUMOWANIE:");
                        sb.AppendLine($"   Zaakceptowanych: {result.Podsumowanie.LiczbaZaakceptowanych}");
                        sb.AppendLine($"   Odrzuconych: {result.Podsumowanie.LiczbaOdrzuconych}");
                    }

                    result.Message = sb.ToString();
                }
                else
                {
                    result.Message = $"Blad pobierania statusu: HTTP {result.HttpStatusCode}\n\n{responseBody}";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Wyjatek: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Loguje wywolanie API do pliku JSON
        /// </summary>
        private void LogApiCall(DyspozycjaZURDApi request, ApiResult result)
        {
            try
            {
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IRZplus_Logs");
                Directory.CreateDirectory(logDir);

                var fileName = $"ZURD_{DateTime.Now:yyyyMMdd_HHmmss}_{(result.Success ? "OK" : "ERROR")}.json";
                var filePath = Path.Combine(logDir, fileName);

                var log = new
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Environment = _useTestEnv ? "TEST" : "PROD",
                    Success = result.Success,
                    HttpStatus = result.HttpStatusCode,
                    NumerZgloszenia = result.NumerZgloszenia,
                    NumerDokumentu = result.NumerDokumentu,
                    Status = result.Status,
                    StatusKod = result.StatusKod,
                    DataUtworzenia = result.DataUtworzenia,
                    Request = request,
                    RequestJson = result.RequestJson,
                    ResponseJson = result.ResponseJson,
                    Komunikaty = result.Komunikaty,
                    Bledy = result.Bledy,
                    Ostrzezenia = result.Ostrzezenia
                };

                var json = JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json, Encoding.UTF8);

                System.Diagnostics.Debug.WriteLine($"Log zapisany: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad logowania: {ex.Message}");
            }
        }

        /// <summary>
        /// Tworzy obiekt DyspozycjaZURDApi z danych specyfikacji
        /// </summary>
        public DyspozycjaZURDApi UtworzDyspozycje(
            string numerPartiiUboju,
            List<PozycjaZURDApi> pozycje)
        {
            return new DyspozycjaZURDApi
            {
                NumerProducenta = NUMER_PRODUCENTA,
                Zgloszenie = new ZgloszenieZURDApi
                {
                    Gatunek = new KodValueApi { Kod = GATUNEK_KOD },
                    NumerRzezni = NUMER_RZEZNI,
                    NumerPartiiUboju = numerPartiiUboju,
                    Pozycje = pozycje
                }
            };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    #region Modele API - Odpowiedzi

    /// <summary>
    /// Pelna odpowiedz z API ZURD
    /// </summary>
    public class ZURDResponse
    {
        [JsonPropertyName("numerZgloszenia")]
        public string NumerZgloszenia { get; set; }

        [JsonPropertyName("numerDokumentu")]
        public string NumerDokumentu { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("statusKod")]
        public string StatusKod { get; set; }

        [JsonPropertyName("dataUtworzenia")]
        public string DataUtworzenia { get; set; }

        [JsonPropertyName("dataModyfikacji")]
        public string DataModyfikacji { get; set; }

        [JsonPropertyName("komunikaty")]
        public List<KomunikatResponse> Komunikaty { get; set; }

        [JsonPropertyName("bledy")]
        public List<BladResponse> Bledy { get; set; }

        [JsonPropertyName("ostrzezenia")]
        public List<string> Ostrzezenia { get; set; }

        [JsonPropertyName("pozycje")]
        public List<PozycjaResponse> Pozycje { get; set; }

        [JsonPropertyName("podsumowanie")]
        public PodsumowanieResponse Podsumowanie { get; set; }
    }

    /// <summary>
    /// Komunikat z API
    /// </summary>
    public class KomunikatResponse
    {
        [JsonPropertyName("kod")]
        public string Kod { get; set; }

        [JsonPropertyName("tresc")]
        public string Tresc { get; set; }

        [JsonPropertyName("typ")]
        public string Typ { get; set; }
    }

    /// <summary>
    /// Blad walidacji z API
    /// </summary>
    public class BladResponse
    {
        [JsonPropertyName("pole")]
        public string Pole { get; set; }

        [JsonPropertyName("komunikat")]
        public string Komunikat { get; set; }

        [JsonPropertyName("kod")]
        public string Kod { get; set; }
    }

    /// <summary>
    /// Status pozycji w zgloszeniu
    /// </summary>
    public class PozycjaResponse
    {
        [JsonPropertyName("lp")]
        public int Lp { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("numerEwidencyjny")]
        public string NumerEwidencyjny { get; set; }

        [JsonPropertyName("komunikaty")]
        public List<string> Komunikaty { get; set; }
    }

    /// <summary>
    /// Podsumowanie zgloszenia
    /// </summary>
    public class PodsumowanieResponse
    {
        [JsonPropertyName("liczbaZaakceptowanych")]
        public int LiczbaZaakceptowanych { get; set; }

        [JsonPropertyName("liczbaOdrzuconych")]
        public int LiczbaOdrzuconych { get; set; }

        [JsonPropertyName("sumaSztuk")]
        public int SumaSztuk { get; set; }

        [JsonPropertyName("sumaMasy")]
        public decimal SumaMasy { get; set; }
    }

    #endregion

    #region Modele API - Requesty

    public class ApiTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }
    }

    public class DyspozycjaZURDApi
    {
        [JsonPropertyName("numerProducenta")]
        public string NumerProducenta { get; set; }

        [JsonPropertyName("zgloszenie")]
        public ZgloszenieZURDApi Zgloszenie { get; set; }
    }

    public class ZgloszenieZURDApi
    {
        [JsonPropertyName("gatunek")]
        public KodValueApi Gatunek { get; set; }

        [JsonPropertyName("numerRzezni")]
        public string NumerRzezni { get; set; }

        [JsonPropertyName("numerPartiiUboju")]
        public string NumerPartiiUboju { get; set; }

        [JsonPropertyName("pozycje")]
        public List<PozycjaZURDApi> Pozycje { get; set; }
    }

    public class PozycjaZURDApi
    {
        [JsonPropertyName("lp")]
        public int Lp { get; set; }

        [JsonPropertyName("numerIdenPartiiDrobiu")]
        public string NumerIdenPartiiDrobiu { get; set; }

        [JsonPropertyName("liczbaDrobiu")]
        public int LiczbaDrobiu { get; set; }

        [JsonPropertyName("masaDrobiu")]
        public decimal MasaDrobiu { get; set; }

        [JsonPropertyName("typZdarzenia")]
        public KodValueApi TypZdarzenia { get; set; }

        [JsonPropertyName("dataZdarzenia")]
        public string DataZdarzenia { get; set; }

        [JsonPropertyName("dataKupnaWwozu")]
        public string DataKupnaWwozu { get; set; }

        [JsonPropertyName("przyjeteZDzialalnosci")]
        public string PrzyjeteZDzialalnosci { get; set; }

        [JsonPropertyName("ubojRytualny")]
        public bool UbojRytualny { get; set; }
    }

    public class KodValueApi
    {
        [JsonPropertyName("kod")]
        public string Kod { get; set; }
    }

    #endregion

    #region Modele wynikowe

    /// <summary>
    /// Rozbudowany wynik operacji API
    /// </summary>
    public class ApiResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        // Identyfikatory
        public string NumerZgloszenia { get; set; }
        public string NumerDokumentu { get; set; }

        // Statusy
        public string Status { get; set; }
        public string StatusKod { get; set; }

        // Daty
        public string DataUtworzenia { get; set; }
        public string DataModyfikacji { get; set; }

        // Surowe JSON-y do analizy
        public string RequestJson { get; set; }
        public string ResponseJson { get; set; }

        // HTTP
        public int HttpStatusCode { get; set; }

        // Listy komunikatow
        public List<string> Komunikaty { get; set; } = new List<string>();
        public List<string> Bledy { get; set; } = new List<string>();
        public List<string> Ostrzezenia { get; set; } = new List<string>();

        // Szczegoly
        public PodsumowanieResponse Podsumowanie { get; set; }
        public List<PozycjaResponse> Pozycje { get; set; }
    }

    // Stara klasa dla kompatybilnosci wstecznej
    [Obsolete("Uzyj ZURDResponse zamiast ZURDApiResponse")]
    public class ZURDApiResponse
    {
        [JsonPropertyName("numerZgloszenia")]
        public string NumerZgloszenia { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("komunikaty")]
        public List<string> Komunikaty { get; set; }
    }

    #endregion
}
