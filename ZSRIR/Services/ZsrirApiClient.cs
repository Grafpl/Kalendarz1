using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kalendarz1.ZSRIR.Models;

namespace Kalendarz1.ZSRIR.Services
{
    // Klient REST API ZSRIR (zsrir.minrol.gov.pl).
    // Token JWT cache'owany ~55 min (TTL 60 min wg dokumentacji API).
    // Wszystkie endpointy: /api/DataSupplierFormApi/* + /api/Auth/GetApiAccessToken.
    public class ZsrirApiClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly ZsrirSecrets _secrets;
        private string? _cachedToken;
        private DateTime _tokenExpiresAt = DateTime.MinValue;
        private readonly SemaphoreSlim _tokenLock = new(1, 1);

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        // Diagnostyka — dostępne po każdym AddFormAsync/AddFormZeroAsync (do debugger UI).
        public string? LastRequestUrl { get; private set; }
        public string? LastRequestJson { get; private set; }
        public string? LastResponseJson { get; private set; }
        public int? LastStatusCode { get; private set; }

        // Raw JSON ostatnich GET endpointów (do debuggera).
        public string? LastPeriodsRawJson { get; private set; }
        public string? LastFormConfigRawJson { get; private set; }
        public string? LastDataSuppliersRawJson { get; private set; }
        public string? LastFormsRawJson { get; private set; }

        public ZsrirApiClient(ZsrirSecrets secrets)
        {
            _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
            _http = new HttpClient
            {
                BaseAddress = new Uri(_secrets.ApiBaseUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(60)
            };
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // ============ AUTH ============
        private async Task<string> GetTokenAsync(CancellationToken ct = default)
        {
            await _tokenLock.WaitAsync(ct);
            try
            {
                if (_cachedToken != null && DateTime.UtcNow < _tokenExpiresAt) return _cachedToken;

                if (!_secrets.IsConfigured)
                    throw new InvalidOperationException("Brak loginu/hasła ZSRIR — skonfiguruj w ustawieniach.");

                var req = new TokenRequest { Username = _secrets.Username, Password = _secrets.Password };
                using var resp = await _http.PostAsJsonAsync("Auth/GetApiAccessToken", req, JsonOpts, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    string body = await resp.Content.ReadAsStringAsync(ct);
                    throw new ZsrirApiException(
                        $"Logowanie nieudane ({(int)resp.StatusCode}): {body}", (int)resp.StatusCode, body);
                }

                // ZSRIR może zwrócić surowy token (string) lub obiekt z polem accessToken.
                string raw = await resp.Content.ReadAsStringAsync(ct);
                string token;
                if (raw.TrimStart().StartsWith("{"))
                {
                    var tr = JsonSerializer.Deserialize<TokenResponse>(raw, JsonOpts);
                    token = tr?.ResolvedToken ?? "";
                }
                else
                {
                    token = raw.Trim('"', ' ', '\r', '\n');
                }
                if (string.IsNullOrWhiteSpace(token))
                    throw new ZsrirApiException("Pusty token w odpowiedzi z /Auth/GetApiAccessToken.", 0, raw);

                _cachedToken = token;
                _tokenExpiresAt = DateTime.UtcNow.AddMinutes(55); // buffer 5 min przed TTL=60
                return token;
            }
            finally { _tokenLock.Release(); }
        }

        private async Task PrepareAuthAsync(CancellationToken ct = default)
        {
            string token = await GetTokenAsync(ct);
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // ============ TEST POŁĄCZENIA ============
        public async Task<(bool ok, string msg)> TestConnectionAsync(CancellationToken ct = default)
        {
            try
            {
                await GetTokenAsync(ct);
                return (true, "Połączono. Token JWT pobrany pomyślnie.");
            }
            catch (ZsrirApiException ex) { return (false, ex.Message); }
            catch (Exception ex) { return (false, "Błąd: " + ex.Message); }
        }

        // ============ DATA SUPPLIERS ============
        public async Task<List<DataSupplier>> GetDataSuppliersAsync(CancellationToken ct = default)
        {
            await PrepareAuthAsync(ct);
            using var resp = await _http.GetAsync("DataSupplierFormApi/GetDataSuppliers", ct);
            string raw = await resp.Content.ReadAsStringAsync(ct);
            LastDataSuppliersRawJson = raw;
            if (!resp.IsSuccessStatusCode)
                throw new ZsrirApiException($"HTTP {(int)resp.StatusCode}: {raw}", (int)resp.StatusCode, raw);
            return string.IsNullOrWhiteSpace(raw) ? new List<DataSupplier>()
                : JsonSerializer.Deserialize<List<DataSupplier>>(raw, JsonOpts) ?? new List<DataSupplier>();
        }

        // ============ FORMS ============
        public async Task<List<FormInfo>> GetFormsAsync(int dataSupplierId, CancellationToken ct = default)
        {
            await PrepareAuthAsync(ct);
            using var resp = await _http.GetAsync(
                $"DataSupplierFormApi/GetForms?dataSupplierId={dataSupplierId}", ct);
            string raw = await resp.Content.ReadAsStringAsync(ct);
            LastFormsRawJson = raw;
            if (!resp.IsSuccessStatusCode)
                throw new ZsrirApiException($"HTTP {(int)resp.StatusCode}: {raw}", (int)resp.StatusCode, raw);
            return string.IsNullOrWhiteSpace(raw) ? new List<FormInfo>()
                : JsonSerializer.Deserialize<List<FormInfo>>(raw, JsonOpts) ?? new List<FormInfo>();
        }

        // ============ REPORTING PERIODS ============
        public async Task<List<ReportingPeriod>> GetReportingPeriodsAsync(int formId, CancellationToken ct = default)
        {
            await PrepareAuthAsync(ct);
            using var resp = await _http.GetAsync(
                $"DataSupplierFormApi/GetReportingPeriods?formId={formId}", ct);
            string raw = await resp.Content.ReadAsStringAsync(ct);
            LastPeriodsRawJson = raw;
            if (!resp.IsSuccessStatusCode)
                throw new ZsrirApiException($"HTTP {(int)resp.StatusCode}: {raw}", (int)resp.StatusCode, raw);
            return string.IsNullOrWhiteSpace(raw) ? new List<ReportingPeriod>()
                : JsonSerializer.Deserialize<List<ReportingPeriod>>(raw, JsonOpts) ?? new List<ReportingPeriod>();
        }

        // ============ FORM CONFIGURATION ============
        public async Task<FormConfiguration?> GetFormConfigurationAsync(int formId, CancellationToken ct = default)
        {
            await PrepareAuthAsync(ct);
            using var resp = await _http.GetAsync(
                $"DataSupplierFormApi/GetFormConfiguration?formId={formId}", ct);
            string raw = await resp.Content.ReadAsStringAsync(ct);
            LastFormConfigRawJson = raw;
            if (!resp.IsSuccessStatusCode)
                throw new ZsrirApiException($"HTTP {(int)resp.StatusCode}: {raw}", (int)resp.StatusCode, raw);
            return string.IsNullOrWhiteSpace(raw) ? null
                : JsonSerializer.Deserialize<FormConfiguration>(raw, JsonOpts);
        }

        // ============ ADD FORM ============
        public async Task<string> AddFormAsync(AddFormRequest body, CancellationToken ct = default)
        {
            await PrepareAuthAsync(ct);
            LastRequestUrl = new Uri(_http.BaseAddress!, "DataSupplierFormApi/AddForm").ToString();
            LastRequestJson = JsonSerializer.Serialize(body, JsonOpts);
            using var content = new StringContent(LastRequestJson, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync("DataSupplierFormApi/AddForm", content, ct);
            LastStatusCode = (int)resp.StatusCode;
            string raw = await resp.Content.ReadAsStringAsync(ct);
            LastResponseJson = raw;
            if (!resp.IsSuccessStatusCode)
                throw new ZsrirApiException($"AddForm nieudane ({(int)resp.StatusCode}): {raw}", (int)resp.StatusCode, raw);
            return raw;
        }

        // ============ ADD FORM ZERO ============
        public async Task<string> AddFormZeroAsync(AddFormZeroRequest body, CancellationToken ct = default)
        {
            await PrepareAuthAsync(ct);
            LastRequestUrl = new Uri(_http.BaseAddress!, "DataSupplierFormApi/AddFormZero").ToString();
            LastRequestJson = JsonSerializer.Serialize(body, JsonOpts);
            using var content = new StringContent(LastRequestJson, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync("DataSupplierFormApi/AddFormZero", content, ct);
            LastStatusCode = (int)resp.StatusCode;
            string raw = await resp.Content.ReadAsStringAsync(ct);
            LastResponseJson = raw;
            if (!resp.IsSuccessStatusCode)
                throw new ZsrirApiException($"AddFormZero nieudane ({(int)resp.StatusCode}): {raw}", (int)resp.StatusCode, raw);
            return raw;
        }

        // ============ Helpers ============
        private static async Task<T?> ParseAsync<T>(HttpResponseMessage resp, CancellationToken ct)
        {
            string raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new ZsrirApiException($"HTTP {(int)resp.StatusCode}: {raw}", (int)resp.StatusCode, raw);
            if (string.IsNullOrWhiteSpace(raw)) return default;
            return JsonSerializer.Deserialize<T>(raw, JsonOpts);
        }

        public void Dispose()
        {
            _http?.Dispose();
            _tokenLock?.Dispose();
        }
    }

    public class ZsrirApiException : Exception
    {
        public int StatusCode { get; }
        public string RawBody { get; }
        public ZsrirApiException(string msg, int statusCode, string rawBody) : base(msg)
        {
            StatusCode = statusCode;
            RawBody = rawBody;
        }
    }
}
