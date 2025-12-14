using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kalendarz1.SmsSprzedaz
{
    /// <summary>
    /// Klient do wysyłania SMS przez SMSAPI.pl
    /// Dokumentacja: https://www.smsapi.pl/docs
    /// </summary>
    public class SmsApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiToken;
        private readonly string _senderName;
        private const string API_URL = "https://api.smsapi.pl/sms.do";

        /// <summary>
        /// Tworzy nowego klienta SMSAPI
        /// </summary>
        /// <param name="apiToken">Token API z panelu SMSAPI.pl</param>
        /// <param name="senderName">Nazwa nadawcy (max 11 znaków, np. "PRONOVA")</param>
        public SmsApiClient(string apiToken, string senderName = null)
        {
            _apiToken = apiToken ?? throw new ArgumentNullException(nameof(apiToken));
            _senderName = senderName;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiToken}");
        }

        /// <summary>
        /// Wysyła SMS do podanego numeru
        /// </summary>
        /// <param name="phoneNumber">Numer telefonu (format: 48123456789 lub +48123456789)</param>
        /// <param name="message">Treść wiadomości (max 160 znaków dla 1 SMS)</param>
        /// <returns>Wynik wysyłki</returns>
        public async Task<SmsApiResult> WyslijSmsAsync(string phoneNumber, string message)
        {
            var result = new SmsApiResult();

            try
            {
                // Normalizuj numer telefonu
                var normalizedPhone = NormalizePhoneNumber(phoneNumber);
                if (string.IsNullOrEmpty(normalizedPhone))
                {
                    result.Sukces = false;
                    result.Blad = "Nieprawidłowy numer telefonu";
                    return result;
                }

                // Przygotuj parametry
                var parameters = new Dictionary<string, string>
                {
                    { "to", normalizedPhone },
                    { "message", message },
                    { "format", "json" },
                    { "encoding", "utf-8" }
                };

                // Dodaj nazwę nadawcy jeśli skonfigurowana
                if (!string.IsNullOrEmpty(_senderName))
                {
                    parameters.Add("from", _senderName);
                }

                // Wyślij request
                var content = new FormUrlEncodedContent(parameters);
                var response = await _httpClient.PostAsync(API_URL, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                // Parsuj odpowiedź
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JsonDocument.Parse(responseBody);
                    var root = jsonResponse.RootElement;

                    // Sprawdź czy jest błąd
                    if (root.TryGetProperty("error", out var errorElement))
                    {
                        result.Sukces = false;
                        result.Blad = errorElement.GetString();
                        result.KodBledu = root.TryGetProperty("error_code", out var code) ? code.GetInt32() : 0;
                    }
                    else if (root.TryGetProperty("list", out var listElement))
                    {
                        // Sukces - pobierz ID wiadomości
                        foreach (var item in listElement.EnumerateArray())
                        {
                            if (item.TryGetProperty("id", out var idElement))
                            {
                                result.SmsId = idElement.GetString();
                            }
                            if (item.TryGetProperty("points", out var pointsElement))
                            {
                                result.Punkty = pointsElement.GetDouble();
                            }
                            if (item.TryGetProperty("status", out var statusElement))
                            {
                                result.StatusSms = statusElement.GetString();
                            }
                        }
                        result.Sukces = true;
                    }
                    else
                    {
                        result.Sukces = true;
                        result.SmsId = "unknown";
                    }
                }
                else
                {
                    result.Sukces = false;
                    result.Blad = $"HTTP {(int)response.StatusCode}: {responseBody}";
                }
            }
            catch (HttpRequestException ex)
            {
                result.Sukces = false;
                result.Blad = $"Błąd połączenia: {ex.Message}";
            }
            catch (Exception ex)
            {
                result.Sukces = false;
                result.Blad = $"Błąd: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Sprawdza stan konta (ilość punktów/środków)
        /// </summary>
        public async Task<SmsApiAccountInfo> SprawdzKontoAsync()
        {
            var info = new SmsApiAccountInfo();

            try
            {
                var response = await _httpClient.GetAsync("https://api.smsapi.pl/profile");
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JsonDocument.Parse(responseBody);
                    var root = jsonResponse.RootElement;

                    if (root.TryGetProperty("points", out var pointsElement))
                    {
                        info.Punkty = pointsElement.GetDouble();
                    }
                    if (root.TryGetProperty("name", out var nameElement))
                    {
                        info.NazwaKonta = nameElement.GetString();
                    }

                    info.Sukces = true;
                }
                else
                {
                    info.Sukces = false;
                    info.Blad = $"HTTP {(int)response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                info.Sukces = false;
                info.Blad = ex.Message;
            }

            return info;
        }

        /// <summary>
        /// Normalizuje numer telefonu do formatu 48XXXXXXXXX
        /// </summary>
        private static string NormalizePhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            // Usuń spacje, myślniki, nawiasy
            var cleaned = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());

            // Usuń + z początku
            if (cleaned.StartsWith("+"))
                cleaned = cleaned.Substring(1);

            // Jeśli zaczyna się od 48 - OK
            if (cleaned.StartsWith("48") && cleaned.Length == 11)
                return cleaned;

            // Jeśli zaczyna się od 0 - zamień na 48
            if (cleaned.StartsWith("0") && cleaned.Length == 10)
                return "48" + cleaned.Substring(1);

            // Jeśli 9 cyfr - dodaj 48
            if (cleaned.Length == 9 && !cleaned.StartsWith("48"))
                return "48" + cleaned;

            // Jeśli już 11 cyfr z 48 - OK
            if (cleaned.Length == 11)
                return cleaned;

            return null;
        }
    }

    /// <summary>
    /// Wynik wysyłki SMS przez SMSAPI
    /// </summary>
    public class SmsApiResult
    {
        public bool Sukces { get; set; }
        public string SmsId { get; set; }
        public string Blad { get; set; }
        public int KodBledu { get; set; }
        public double Punkty { get; set; }
        public string StatusSms { get; set; }
    }

    /// <summary>
    /// Informacje o koncie SMSAPI
    /// </summary>
    public class SmsApiAccountInfo
    {
        public bool Sukces { get; set; }
        public double Punkty { get; set; }
        public string NazwaKonta { get; set; }
        public string Blad { get; set; }
    }
}
