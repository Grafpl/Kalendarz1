using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Kalendarz1.Webfleet
{
    /// <summary>
    /// Współdzielony HttpClient dla wszystkich wywołań Webfleet.connect.
    ///
    /// Wcześniej 9 plików MapaFloty miało własne instancje HttpClient z różnymi timeoutami (20s/30s),
    /// co jest anti-patternem (socket exhaustion) i utrudniało dodanie cache/rate-limit.
    ///
    /// Pojedynczy singleton + BasicAuthHeader() helper czytający credentials z WebfleetConfig.
    /// </summary>
    public static class WebfleetHttp
    {
        private static readonly HttpClient _instance = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public static HttpClient Instance => _instance;

        /// <summary>
        /// Basic auth header zbudowany z WebfleetConfig.User/Pass. Wywołuj per request
        /// (NIE static cache) — credentials mogą zostać przeładowane z secrets.json.
        /// </summary>
        public static AuthenticationHeaderValue BasicAuthHeader()
        {
            var creds = $"{WebfleetConfig.User}:{WebfleetConfig.Pass}";
            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(creds));
            return new AuthenticationHeaderValue("Basic", b64);
        }

        /// <summary>
        /// Skrót: zbuduj URL z BaseUrl + standardowymi parametrami (account, apikey, lang, outputformat, action).
        /// Konsument dodaje resztę query stringu po '&'.
        /// </summary>
        public static string BuildUrlBase(string action)
        {
            return $"{WebfleetConfig.BaseUrl}?account={Uri.EscapeDataString(WebfleetConfig.Account)}" +
                   $"&apikey={Uri.EscapeDataString(WebfleetConfig.ApiKey)}" +
                   $"&lang=pl&outputformat=json" +
                   $"&action={Uri.EscapeDataString(action)}";
        }
    }
}
