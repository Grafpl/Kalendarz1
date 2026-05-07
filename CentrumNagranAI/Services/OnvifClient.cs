using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Minimalistyczny klient ONVIF (Media + Replay) dla NVR Internec.
    /// Używamy WS-Security UsernameToken (PasswordDigest) — działa nawet gdy
    /// digest auth na samym RTSP jest kapryśny.
    ///
    /// Profile tokeny + URL streamów cache'owane na czas życia aplikacji
    /// (zwykle nie zmieniają się dopóki kamery nie są przekonfigurowane).
    /// </summary>
    public static class OnvifClient
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

        // Cache: cameraId → ProfileToken
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _profileTokenCache = new();

        /// <summary>
        /// Buduje URL do nagrania historycznego z NVR Internec.
        /// ONVIF replay protocol (GetReplayUri przez SOAP) na tym firmware nie zwraca
        /// poprawnego URL, więc używamy bezpośredniego formatu Internec znalezionego empirycznie:
        ///   rtsp://host:port/playback/c{channel}/s{stream}/{yyyyMMdd'T'HHmmss'Z'}/{...}
        /// Format dat: ONVIF style (yyyyMMddTHHmmssZ).
        /// </summary>
        public static Task<string?> GetReplayUriAsync(
            CnaCameraEndpoint kamera, DateTime fromUtc, DateTime toUtc)
        {
            string from = fromUtc.ToString("yyyyMMdd'T'HHmmss'Z'");
            string to = toUtc.ToString("yyyyMMdd'T'HHmmss'Z'");
            string url = $"rtsp://{kamera.Host}:{kamera.RtspPort}/playback/c{kamera.Channel}/s{kamera.StreamType}/{from}/{to}";
            return Task.FromResult<string?>(url);
        }

        private static async Task<string?> GetProfileTokenAsync(CnaCameraEndpoint kamera)
        {
            if (_profileTokenCache.TryGetValue(kamera.Id, out var cached)) return cached;

            string body = BuildSoap(kamera, "<GetProfiles xmlns=\"http://www.onvif.org/ver10/media/wsdl\"/>");
            string? resp = await CallSoapAsync(kamera, "/onvif/Media", body);
            if (resp == null) return null;

            // Format pattern: SourceToken = "{channel:D3}00" → 00100=ch1, 00400=ch4
            string expectedSource = $"{kamera.Channel:D3}00";
            string preferStream = $"/s{kamera.StreamType}";

            // Próbujemy zmatchować Profile z odpowiednim SourceToken + StreamType.
            // Greedy regex bo XML wielonamespace.
            var profilesMatches = Regex.Matches(resp,
                @"<trt:Profiles[^>]*token=""(?<tok>[^""]+)""[^>]*>(?<body>.*?)</trt:Profiles>",
                RegexOptions.Singleline);

            string? bestToken = null;
            foreach (Match pm in profilesMatches)
            {
                string profileBody = pm.Groups["body"].Value;
                if (!profileBody.Contains($"<tt:SourceToken>{expectedSource}</tt:SourceToken>")) continue;
                string tok = pm.Groups["tok"].Value;

                // Preferuj pasujący stream type, ale akceptuj inny jako fallback.
                if (tok.EndsWith(preferStream))
                {
                    bestToken = tok;
                    break;
                }
                bestToken ??= tok;
            }

            if (bestToken != null)
                _profileTokenCache[kamera.Id] = bestToken;
            return bestToken;
        }

        private static string BuildSoap(CnaCameraEndpoint k, string innerBody)
        {
            // WS-Security UsernameToken z PasswordDigest = base64(sha1(nonce + created + password))
            byte[] nonce = new byte[16];
            RandomNumberGenerator.Fill(nonce);
            string nonceB64 = Convert.ToBase64String(nonce);
            string created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            byte[] pass = Encoding.UTF8.GetBytes(k.Password);
            byte[] cBytes = Encoding.UTF8.GetBytes(created);
            byte[] combined = new byte[nonce.Length + cBytes.Length + pass.Length];
            Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
            Buffer.BlockCopy(cBytes, 0, combined, nonce.Length, cBytes.Length);
            Buffer.BlockCopy(pass, 0, combined, nonce.Length + cBytes.Length, pass.Length);
            string digest = Convert.ToBase64String(SHA1.HashData(combined));

            return
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\">" +
                "<s:Header><Security xmlns=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd\" s:mustUnderstand=\"1\">" +
                $"<UsernameToken><Username>{k.User}</Username>" +
                $"<Password Type=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest\">{digest}</Password>" +
                $"<Nonce EncodingType=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary\">{nonceB64}</Nonce>" +
                $"<Created xmlns=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd\">{created}</Created>" +
                "</UsernameToken></Security></s:Header>" +
                $"<s:Body>{innerBody}</s:Body></s:Envelope>";
        }

        private static async Task<string?> CallSoapAsync(CnaCameraEndpoint kamera, string endpoint, string body)
        {
            try
            {
                using var content = new StringContent(body, Encoding.UTF8, "application/soap+xml");
                using var resp = await _http.PostAsync($"http://{kamera.Host}{endpoint}", content);
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadAsStringAsync();
            }
            catch
            {
                return null;
            }
        }
    }
}
