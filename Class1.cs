using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

public class OpenAiClient
{
    private readonly HttpClient _http;

    public OpenAiClient(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Brak klucza API.", nameof(apiKey));

        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> AskAsync(string prompt, string model = "gpt-5")
    {
        var url = "https://api.openai.com/v1/chat/completions";

        var payload = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = "Jesteś pomocnym asystentem." },
                new { role = "user",   content = prompt }
            },
        };

        var json = JsonConvert.SerializeObject(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.PostAsync(url, content);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"API {resp.StatusCode}: {body}");

        // minimalny model odpowiedzi (interesuje nas pierwszy choice)
        var dto = JsonConvert.DeserializeObject<ChatCompletionDto>(body);
        return dto?.choices?[0]?.message?.content?.Trim()
               ?? "(brak treści w odpowiedzi)";
    }

    // DTO pasujące do Chat Completions
    private class ChatCompletionDto
    {
        public Choice[] choices { get; set; }
    }

    private class Choice
    {
        public Message message { get; set; }
    }

    private class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }
}
