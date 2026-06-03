using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kalendarz1.Kontrakty.Services
{
    /// <summary>
    /// Pobieranie danych firmy z Białej Listy MF (https://wl-api.mf.gov.pl).
    /// API publiczne, bez klucza, limit 10 zapytań/min/IP.
    /// </summary>
    public static class GusApiService
    {
        private static readonly HttpClient Hc = new() { Timeout = TimeSpan.FromSeconds(8) };

        public class GusResult
        {
            public bool Znaleziono { get; set; }
            public string Nazwa { get; set; } = "";
            public string Regon { get; set; } = "";
            public string Adres { get; set; } = "";
            public string StatusVat { get; set; } = "";   // Czynny / Zwolniony / Niezarejestrowany
            public string? Komunikat { get; set; }        // np. „Niezarejestrowany w VAT"
        }

        public static async Task<GusResult> PobierzPoNipAsync(string nip)
        {
            string clean = new string((nip ?? "").Trim().ToCharArray());
            // tylko cyfry
            var sb = new System.Text.StringBuilder(10);
            foreach (var c in clean) if (char.IsDigit(c)) sb.Append(c);
            string n = sb.ToString();
            if (n.Length != 10) return new GusResult { Komunikat = "NIP musi mieć 10 cyfr" };

            string url = $"https://wl-api.mf.gov.pl/api/search/nip/{n}?date={DateTime.Today:yyyy-MM-dd}";
            try
            {
                using var resp = await Hc.GetAsync(url);
                if ((int)resp.StatusCode == 429)
                    return new GusResult { Komunikat = "MF: limit zapytań (10/min). Spróbuj za chwilę." };
                if (!resp.IsSuccessStatusCode)
                    return new GusResult { Komunikat = $"MF API: HTTP {(int)resp.StatusCode}" };

                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("result", out var res)) return new GusResult { Komunikat = "MF: pusta odpowiedź" };
                if (!res.TryGetProperty("subject", out var sub) || sub.ValueKind == JsonValueKind.Null)
                    return new GusResult { Komunikat = "MF: podmiot niezarejestrowany w VAT" };

                static string S(JsonElement e, string key)
                    => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

                string adres = S(sub, "workingAddress");
                if (string.IsNullOrWhiteSpace(adres)) adres = S(sub, "residenceAddress");

                return new GusResult
                {
                    Znaleziono = true,
                    Nazwa = S(sub, "name"),
                    Regon = S(sub, "regon"),
                    Adres = adres,
                    StatusVat = S(sub, "statusVat")
                };
            }
            catch (TaskCanceledException) { return new GusResult { Komunikat = "MF API: timeout (8s). Sprawdź połączenie." }; }
            catch (HttpRequestException ex) { return new GusResult { Komunikat = "Brak połączenia z MF: " + ex.Message }; }
            catch (Exception ex) { return new GusResult { Komunikat = "Błąd: " + ex.Message }; }
        }
    }
}
