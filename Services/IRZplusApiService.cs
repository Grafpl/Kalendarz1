using System;
using System.Collections.Generic;
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

        // === ENDPOINTY API ===
        private const string AUTH_URL = "https://sso.arimr.gov.pl/auth/realms/ewniosekplus/protocol/openid-connect/token";
        private const string API_PROD_URL = "https://irz.arimr.gov.pl/api/drob/dokument/api/prod/zurd";
        private const string CLIENT_ID = "aplikacja-irzplus";

        // === STALE DANE RZEZNI ===
        public const string NUMER_PRODUCENTA = "039806095";
        public const string NUMER_RZEZNI = "039806095-001";
        public const string GATUNEK_KOD = "KU";

        public IRZplusApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Pobiera token OAuth2 z serwera autoryzacji ARiMR
        /// </summary>
        public async Task<ApiResult> AuthenticateAsync(string username, string password)
        {
            try
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", username),
                    new KeyValuePair<string, string>("password", password),
                    new KeyValuePair<string, string>("client_id", CLIENT_ID),
                    new KeyValuePair<string, string>("grant_type", "password")
                });

                var response = await _httpClient.PostAsync(AUTH_URL, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonSerializer.Deserialize<ApiTokenResponse>(responseBody);
                    _accessToken = tokenResponse.AccessToken;
                    _tokenExpiry = DateTime.Now.AddSeconds(tokenResponse.ExpiresIn - 60); // 60s zapasu

                    return new ApiResult
                    {
                        Success = true,
                        Message = "Autoryzacja pomyslna"
                    };
                }
                else
                {
                    return new ApiResult
                    {
                        Success = false,
                        Message = $"Blad autoryzacji: {response.StatusCode}\n{responseBody}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ApiResult
                {
                    Success = false,
                    Message = $"Wyjatek podczas autoryzacji: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Sprawdza czy token jest wazny
        /// </summary>
        public bool IsTokenValid => !string.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiry;

        /// <summary>
        /// Wysyla zgloszenie ZURD do API produkcyjnego
        /// </summary>
        public async Task<ApiResult> WyslijZURDAsync(DyspozycjaZURDApi dyspozycja)
        {
            if (!IsTokenValid)
            {
                return new ApiResult
                {
                    Success = false,
                    Message = "Token niewazny. Zaloguj sie ponownie."
                };
            }

            try
            {
                var json = JsonSerializer.Serialize(dyspozycja, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await _httpClient.PostAsync(API_PROD_URL, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<ZURDApiResponse>(responseBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return new ApiResult
                    {
                        Success = true,
                        Message = "Zgloszenie wyslane pomyslnie",
                        NumerZgloszenia = result?.NumerZgloszenia,
                        ResponseJson = responseBody
                    };
                }
                else
                {
                    return new ApiResult
                    {
                        Success = false,
                        Message = $"Blad API: {response.StatusCode}\n{responseBody}",
                        ResponseJson = responseBody
                    };
                }
            }
            catch (Exception ex)
            {
                return new ApiResult
                {
                    Success = false,
                    Message = $"Wyjatek: {ex.Message}"
                };
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

    #region Modele API

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

    public class ZURDApiResponse
    {
        [JsonPropertyName("numerZgloszenia")]
        public string NumerZgloszenia { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("komunikaty")]
        public List<string> Komunikaty { get; set; }
    }

    public class ApiResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string NumerZgloszenia { get; set; }
        public string ResponseJson { get; set; }
    }

    #endregion
}
