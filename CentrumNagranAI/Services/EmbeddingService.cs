using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// OpenAI text embeddings (text-embedding-3-small, 1536-dim).
    /// Multilingual — działa świetnie po polsku. ~$0.02/1M tokens, czyli grosze.
    ///
    /// Klucz brany z ENV OPENAI_API_KEY albo z secrets.json.
    /// </summary>
    public static class EmbeddingService
    {
        private const string ApiUrl = "https://api.openai.com/v1/embeddings";
        public const string Model = "text-embedding-3-small";
        public const int Dim = 1536;

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
        private static bool _headersSet;
        private static readonly object _initLock = new();
        private static string _apiKey = string.Empty;

        public static bool IsConfigured => !string.IsNullOrEmpty(GetApiKey());

        private static string GetApiKey()
        {
            if (!string.IsNullOrEmpty(_apiKey)) return _apiKey;
            CnaConfig.ZaladujJesliTrzeba();

            // Priorytet: secrets.json -> ENV
            try
            {
                if (File.Exists(CnaConfig.SecretsPath))
                {
                    var json = JObject.Parse(File.ReadAllText(CnaConfig.SecretsPath));
                    var fromSecrets = json["OpenAIApiKey"]?.Value<string>();
                    if (!string.IsNullOrEmpty(fromSecrets)) { _apiKey = fromSecrets; return _apiKey; }
                }
            }
            catch { }
            _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
            return _apiKey;
        }

        public static async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            string key = GetApiKey();
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("Brak klucza OpenAI. Ustaw ENV OPENAI_API_KEY lub OpenAIApiKey w secrets.json.");

            EnsureHeaders(key);

            var body = new { input = text, model = Model };
            string json = JsonConvert.SerializeObject(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(ApiUrl, content, ct);
            string respText = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"OpenAI embed HTTP {(int)resp.StatusCode}: {respText.Substring(0, Math.Min(300, respText.Length))}");

            var jo = JObject.Parse(respText);
            var arr = jo["data"]?[0]?["embedding"] as JArray;
            if (arr == null || arr.Count != Dim)
                throw new InvalidOperationException($"OpenAI embed: nieoczekiwany format (dim={arr?.Count})");

            var vec = new float[Dim];
            for (int i = 0; i < Dim; i++) vec[i] = arr[i].Value<float>();
            return vec;
        }

        private static void EnsureHeaders(string key)
        {
            if (_headersSet) return;
            lock (_initLock)
            {
                if (_headersSet) return;
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
                _headersSet = true;
            }
        }

        // ───── Lokalne KNN cosine ─────

        public static byte[] FloatArrayToBlob(float[] v)
        {
            byte[] bytes = new byte[v.Length * 4];
            Buffer.BlockCopy(v, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static float[] BlobToFloatArray(byte[] blob)
        {
            float[] v = new float[blob.Length / 4];
            Buffer.BlockCopy(blob, 0, v, 0, blob.Length);
            return v;
        }

        public static float Cosine(float[] a, float[] b)
        {
            // Zakładamy że oba znormalizowane (OpenAI zwraca znormalizowane).
            // Iloczyn skalarny = cosine similarity dla unit vectors.
            int n = Math.Min(a.Length, b.Length);
            float dot = 0;
            for (int i = 0; i < n; i++) dot += a[i] * b[i];
            return dot;
        }
    }
}
