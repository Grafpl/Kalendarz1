using System;
using System.Collections.Generic;
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
    /// Klient Claude Anthropic API dla VLM (multimodal vision-language).
    /// Wzorzec ścisły z MarketIntelligence/Services/AI/ClaudeAnalysisService.cs:
    /// raw HttpClient, headers x-api-key + anthropic-version, klucz z CnaConfig
    /// (secrets.json) lub fallback ENV ANTHROPIC_API_KEY.
    ///
    /// Domyślny model: Claude Haiku 4.5 — najtańszy multimodal Anthropic
    /// ($1/MTok input, $5/MTok output, ~$0.001 per klatka 640x360 z krótkim opisem).
    /// </summary>
    public static class VlmClient
    {
        private const string ApiUrl = "https://api.anthropic.com/v1/messages";
        private const string ApiVersion = "2023-06-01";
        public const string ModelHaiku = "claude-haiku-4-5";
        public const string ModelSonnet = "claude-sonnet-4-6";

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
        private static bool _headersSet;
        private static readonly object _initLock = new();

        public class VlmResult
        {
            public string Text { get; set; } = string.Empty;
            public int InputTokens { get; set; }
            public int OutputTokens { get; set; }
            public long DurationMs { get; set; }
            public double CostUsd { get; set; }
        }

        /// <summary>
        /// Wysyła obraz JPEG + polski prompt do Claude. Zwraca tekst odpowiedzi + statystyki tokenów.
        /// </summary>
        /// <summary>
        /// Multi-image: VLM widzi sekwencję klatek (np. -10s, klatka, +10s) zamiast 1.
        /// Łapie ruch i kontekst, znacznie poprawia trafność (#A1).
        /// Koszt rośnie ~liniowo z liczbą klatek (każda to ~333 tokens input).
        /// </summary>
        public static async Task<VlmResult> AnalyzeMultiImageAsync(
            IList<string> jpegPaths,
            string polishPrompt,
            string model = ModelHaiku,
            int maxTokens = 500,
            CancellationToken ct = default)
        {
            CnaConfig.ZaladujJesliTrzeba();
            if (string.IsNullOrEmpty(CnaConfig.AnthropicApiKey))
                throw new InvalidOperationException("Brak klucza Anthropic.");
            if (jpegPaths.Count == 0)
                throw new ArgumentException("Lista klatek pusta.");

            EnsureHeaders();

            var contentList = new List<object>();
            for (int i = 0; i < jpegPaths.Count; i++)
            {
                if (!File.Exists(jpegPaths[i])) continue;
                byte[] bytes = await File.ReadAllBytesAsync(jpegPaths[i], ct);
                string b64 = Convert.ToBase64String(bytes);
                contentList.Add(new { type = "text", text = $"[Klatka {i + 1} z {jpegPaths.Count}]" });
                contentList.Add(new
                {
                    type = "image",
                    source = new { type = "base64", media_type = "image/jpeg", data = b64 }
                });
            }
            contentList.Add(new { type = "text", text = polishPrompt });

            var requestBody = new
            {
                model = model,
                max_tokens = maxTokens,
                messages = new[] { new { role = "user", content = contentList.ToArray() } }
            };

            string json = JsonConvert.SerializeObject(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var sw = Stopwatch.StartNew();
            using var resp = await _http.PostAsync(ApiUrl, content, ct);
            string respText = await resp.Content.ReadAsStringAsync(ct);
            sw.Stop();

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Anthropic multi-image {(int)resp.StatusCode}: {respText.Substring(0, Math.Min(300, respText.Length))}");

            var jo = JObject.Parse(respText);
            string text = (string?)jo["content"]?[0]?["text"] ?? string.Empty;
            int inTok = (int?)jo["usage"]?["input_tokens"] ?? 0;
            int outTok = (int?)jo["usage"]?["output_tokens"] ?? 0;
            double inPrice = model.Contains("haiku") ? 1.0 : 3.0;
            double outPrice = model.Contains("haiku") ? 5.0 : 15.0;
            double cost = (inTok * inPrice + outTok * outPrice) / 1_000_000.0;

            Log($"OK multi-image {model} n={jpegPaths.Count} in={inTok} out={outTok} {sw.ElapsedMilliseconds}ms ${cost:F4}");

            return new VlmResult { Text = text, InputTokens = inTok, OutputTokens = outTok, DurationMs = sw.ElapsedMilliseconds, CostUsd = cost };
        }

        public static async Task<VlmResult> AnalyzeImageAsync(
            string jpegPath,
            string polishPrompt,
            string model = ModelHaiku,
            int maxTokens = 500,
            CancellationToken ct = default)
        {
            CnaConfig.ZaladujJesliTrzeba();

            if (string.IsNullOrEmpty(CnaConfig.AnthropicApiKey))
                throw new InvalidOperationException(
                    "Brak klucza Anthropic. Sprawdź secrets.json w %LOCALAPPDATA%\\Kalendarz1\\CentrumNagranAI\\.");

            if (!File.Exists(jpegPath))
                throw new FileNotFoundException($"Brak pliku klatki: {jpegPath}");

            EnsureHeaders();

            byte[] imageBytes = await File.ReadAllBytesAsync(jpegPath, ct);
            string base64 = Convert.ToBase64String(imageBytes);

            var requestBody = new
            {
                model = model,
                max_tokens = maxTokens,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "image",
                                source = new
                                {
                                    type = "base64",
                                    media_type = "image/jpeg",
                                    data = base64
                                }
                            },
                            new { type = "text", text = polishPrompt }
                        }
                    }
                }
            };

            string json = JsonConvert.SerializeObject(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var sw = Stopwatch.StartNew();
            using var resp = await _http.PostAsync(ApiUrl, content, ct);
            string respText = await resp.Content.ReadAsStringAsync(ct);
            sw.Stop();

            if (!resp.IsSuccessStatusCode)
            {
                Log($"FAIL HTTP {(int)resp.StatusCode} dla {Path.GetFileName(jpegPath)}: {respText.Substring(0, Math.Min(300, respText.Length))}");
                throw new InvalidOperationException(
                    $"Anthropic API zwróciło {(int)resp.StatusCode}: {respText.Substring(0, Math.Min(500, respText.Length))}");
            }

            var jo = JObject.Parse(respText);
            string text = jo["content"]?[0]?["text"]?.Value<string>() ?? string.Empty;
            int inTok = jo["usage"]?["input_tokens"]?.Value<int>() ?? 0;
            int outTok = jo["usage"]?["output_tokens"]?.Value<int>() ?? 0;

            // Cennik Haiku 4.5: $1/MTok in, $5/MTok out. Sonnet 4.6: $3/$15.
            double inPrice = model.Contains("haiku") ? 1.0 : 3.0;
            double outPrice = model.Contains("haiku") ? 5.0 : 15.0;
            double cost = (inTok * inPrice + outTok * outPrice) / 1_000_000.0;

            Log($"OK {model} {Path.GetFileName(jpegPath)} in={inTok} out={outTok} {sw.ElapsedMilliseconds}ms ${cost:F4}");

            return new VlmResult
            {
                Text = text,
                InputTokens = inTok,
                OutputTokens = outTok,
                DurationMs = sw.ElapsedMilliseconds,
                CostUsd = cost
            };
        }

        private static void EnsureHeaders()
        {
            if (_headersSet) return;
            lock (_initLock)
            {
                if (_headersSet) return;
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("x-api-key", CnaConfig.AnthropicApiKey);
                _http.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
                _headersSet = true;
            }
        }

        private static void Log(string msg)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff} [CNA-VLM] {msg}";
            Debug.WriteLine(line);
            try
            {
                Directory.CreateDirectory(CnaConfig.AuditDir);
                File.AppendAllText(
                    Path.Combine(CnaConfig.AuditDir, "cna_vlm.log"),
                    line + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch { }
        }
    }
}
