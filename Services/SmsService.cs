using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Serwis SMS - obsługuje różne bramki SMS (SMSAPI.pl, SMSCenter, itp.)
    /// </summary>
    public class SmsService
    {
        private readonly string _apiKey;
        private readonly string _senderName;
        private readonly SmsProvider _provider;
        private static readonly HttpClient _httpClient = new HttpClient();

        public enum SmsProvider
        {
            SMSAPI,      // smsapi.pl
            SMSCenter,   // smscenter.pl
            SerwerSMS,   // serwersms.pl
            Custom       // własne API
        }

        public SmsService(string apiKey, string senderName = "PIORKOWSCY", SmsProvider provider = SmsProvider.SMSAPI)
        {
            _apiKey = apiKey;
            _senderName = senderName;
            _provider = provider;
        }

        /// <summary>
        /// Wysyła SMS z powiadomieniem o rozliczeniu
        /// </summary>
        public async Task<SmsResult> SendRozliczenieNotificationAsync(
            string phoneNumber,
            string hodowcaNazwa,
            DateTime dataUboju,
            decimal kwotaDoZaplaty,
            int iloscSztuk,
            decimal iloscKg)
        {
            // Formatuj numer telefonu (usuń spacje, dodaj prefix)
            phoneNumber = NormalizePhoneNumber(phoneNumber);

            if (string.IsNullOrEmpty(phoneNumber))
                return new SmsResult { Success = false, Message = "Nieprawidłowy numer telefonu" };

            // Krótka wiadomość SMS (max 160 znaków dla 1 SMS)
            string message = $"Piorkowscy: Rozliczenie {dataUboju:dd.MM} - " +
                           $"{iloscSztuk}szt, {iloscKg:N0}kg, " +
                           $"do zaplaty: {kwotaDoZaplaty:N2}zl. " +
                           $"PDF wyslany na email.";

            // Jeśli wiadomość jest za długa, skróć
            if (message.Length > 160)
            {
                message = $"Piorkowscy: Rozliczenie {dataUboju:dd.MM} - " +
                         $"{kwotaDoZaplaty:N2}zl. Szczegoly na email.";
            }

            return await SendSmsAsync(phoneNumber, message);
        }

        /// <summary>
        /// Wysyła SMS o planowanym załadunku
        /// </summary>
        public async Task<SmsResult> SendZaladunekReminderAsync(
            string phoneNumber,
            DateTime dataZaladunku,
            string kierowcaNazwa,
            string numerRejestracyjny)
        {
            phoneNumber = NormalizePhoneNumber(phoneNumber);

            string message = $"Piorkowscy: Jutro {dataZaladunku:dd.MM} zaladunek drobiu. " +
                           $"Kierowca: {kierowcaNazwa}, auto: {numerRejestracyjny}. " +
                           $"Prosimy o przygotowanie.";

            return await SendSmsAsync(phoneNumber, message);
        }

        /// <summary>
        /// Wysyła SMS o przyjeździe kierowcy
        /// </summary>
        public async Task<SmsResult> SendKierowcaInDrogaAsync(
            string phoneNumber,
            int szacowanyCzasMin)
        {
            phoneNumber = NormalizePhoneNumber(phoneNumber);

            string message = $"Piorkowscy: Kierowca w drodze, przyjazd za ok. {szacowanyCzasMin} min. " +
                           $"Prosimy o przygotowanie drobiu.";

            return await SendSmsAsync(phoneNumber, message);
        }

        /// <summary>
        /// Wysyła dowolny SMS
        /// </summary>
        public async Task<SmsResult> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                    return new SmsResult { Success = false, Message = "Brak klucza API SMS" };

                switch (_provider)
                {
                    case SmsProvider.SMSAPI:
                        return await SendViaSmsApiAsync(phoneNumber, message);
                    case SmsProvider.SMSCenter:
                        return await SendViaSMSCenterAsync(phoneNumber, message);
                    case SmsProvider.SerwerSMS:
                        return await SendViaSerwerSMSAsync(phoneNumber, message);
                    default:
                        return new SmsResult { Success = false, Message = "Nieobsługiwany provider SMS" };
                }
            }
            catch (Exception ex)
            {
                return new SmsResult { Success = false, Message = $"Błąd: {ex.Message}" };
            }
        }

        private async Task<SmsResult> SendViaSmsApiAsync(string phoneNumber, string message)
        {
            // SMSAPI.pl REST API
            var url = "https://api.smsapi.pl/sms.do";

            var content = new FormUrlEncodedContent(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("to", phoneNumber),
                new System.Collections.Generic.KeyValuePair<string, string>("message", message),
                new System.Collections.Generic.KeyValuePair<string, string>("from", _senderName),
                new System.Collections.Generic.KeyValuePair<string, string>("format", "json"),
                new System.Collections.Generic.KeyValuePair<string, string>("encoding", "utf-8")
            });

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && !responseBody.Contains("error"))
            {
                return new SmsResult { Success = true, Message = "SMS wysłany (SMSAPI)" };
            }

            return new SmsResult { Success = false, Message = $"Błąd SMSAPI: {responseBody}" };
        }

        private async Task<SmsResult> SendViaSMSCenterAsync(string phoneNumber, string message)
        {
            // SMSCenter.pl API
            var url = $"https://api.smscenter.pl/send?" +
                     $"login={_apiKey.Split(':')[0]}&pass={_apiKey.Split(':')[1]}" +
                     $"&to={phoneNumber}&text={Uri.EscapeDataString(message)}&from={_senderName}";

            var response = await _httpClient.GetAsync(url);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && responseBody.StartsWith("OK"))
            {
                return new SmsResult { Success = true, Message = "SMS wysłany (SMSCenter)" };
            }

            return new SmsResult { Success = false, Message = $"Błąd SMSCenter: {responseBody}" };
        }

        private async Task<SmsResult> SendViaSerwerSMSAsync(string phoneNumber, string message)
        {
            // SerwerSMS.pl API
            var url = "https://api2.serwersms.pl/messages/send_sms";

            var payload = new
            {
                username = _apiKey.Split(':')[0],
                password = _apiKey.Split(':')[1],
                phone = phoneNumber,
                text = message,
                sender = _senderName
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return new SmsResult { Success = true, Message = "SMS wysłany (SerwerSMS)" };
            }

            return new SmsResult { Success = false, Message = $"Błąd SerwerSMS: {responseBody}" };
        }

        /// <summary>
        /// Normalizuje numer telefonu do formatu międzynarodowego
        /// </summary>
        private string NormalizePhoneNumber(string phone)
        {
            if (string.IsNullOrEmpty(phone))
                return null;

            // Usuń wszystkie znaki oprócz cyfr
            var digits = new StringBuilder();
            foreach (char c in phone)
            {
                if (char.IsDigit(c))
                    digits.Append(c);
            }

            string normalized = digits.ToString();

            // Dodaj prefix Polski jeśli brak
            if (normalized.Length == 9)
                normalized = "48" + normalized;
            else if (normalized.StartsWith("0") && normalized.Length == 10)
                normalized = "48" + normalized.Substring(1);

            // Sprawdź długość (Polski numer: 11 cyfr z prefiksem)
            if (normalized.Length < 11)
                return null;

            return normalized;
        }

        /// <summary>
        /// Sprawdza czy serwis jest skonfigurowany
        /// </summary>
        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_apiKey);
        }
    }

    public class SmsResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string MessageId { get; set; }
        public decimal? Cost { get; set; }
    }
}
