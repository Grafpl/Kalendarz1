using Microsoft.Data.SqlClient;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Kalendarz1.Zywiec.Kalendarz.Dialogs;

namespace Kalendarz1.Zywiec.Kalendarz.Services
{
    // Wspólny klient do komunikacji z MacroDroid na telefonie pracownicy.
    // Endpointy obsługiwane:
    //   POST /sms   {"phone":"<E.164>","text":"<treść>"}   — wyślij SMS
    //   POST /call  {"phone":"<E.164>"}                    — zadzwoń (click-to-call)
    //
    // Konfiguracja per-user w pliku %LOCALAPPDATA%\Kalendarz1\sms-telefon-{userId}.json
    // (zarządzana przez TestSmsDialog.SmsTelefonSettings).
    public static class MacroDroidClient
    {
        public sealed class Wynik
        {
            public bool Sukces { get; init; }
            public int StatusKod { get; init; }
            public string Komunikat { get; init; } = "";
            public string TrescOdpowiedzi { get; init; } = "";
        }

        // Wysyła SMS przez MacroDroid (/sms).
        public static Task<Wynik> WyslijSmsAsync(string? userId, string phone, string text)
            => PostAsync(userId, "/sms", new { phone, text });

        // Wykonuje połączenie przez MacroDroid (/call).
        // Wysyła JSON {"phone":"+48..."} — taką samą strukturę jak /sms (sprawdzone że działa).
        // W makrze MacroDroid można skopiować strukturę z makra SMS: JSON Parse + Call.
        public static Task<Wynik> ZadzwonAsync(string? userId, string phone)
            => PostAsync(userId, "/call", new { phone });

        // Czy telefon jest skonfigurowany dla danego usera (IP + port niezerowy).
        public static bool CzyTelefonSkonfigurowany(string? userId)
        {
            try
            {
                var s = TestSmsDialog.WczytajUstawienia(userId);
                return s != null && !string.IsNullOrWhiteSpace(s.Ip) && s.Port > 0;
            }
            catch { return false; }
        }

        // ===== NORMALIZACJA NUMERU TELEFONU =====
        // Przyjmuje numer w dowolnym z popularnych formatów polskich i zwraca +48xxxxxxxxx.
        // Zwraca null jeśli numer jest pusty lub po oczyszczeniu ma < 9 cyfr.
        public static string? NormalizujNumer(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            // Usuń spacje, myślniki, nawiasy, kropki
            var sb = new StringBuilder();
            foreach (char c in raw)
            {
                if (char.IsDigit(c)) sb.Append(c);
                else if (c == '+' && sb.Length == 0) sb.Append('+');
            }
            string cyfry = sb.ToString();

            if (string.IsNullOrEmpty(cyfry)) return null;

            // Już z + → zostaw, ale sprawdź długość
            if (cyfry.StartsWith("+"))
            {
                string tylkocyfry = cyfry.Substring(1);
                return tylkocyfry.Length >= 9 ? "+" + tylkocyfry : null;
            }

            // 0048... → +48...
            if (cyfry.StartsWith("0048")) return "+" + cyfry.Substring(2);
            // 48xxxxxxxxx (11 cyfr) → +48xxxxxxxxx
            if (cyfry.StartsWith("48") && cyfry.Length == 11) return "+" + cyfry;
            // 0xxxxxxxxx (10 cyfr) → +48xxxxxxxxx
            if (cyfry.StartsWith("0") && cyfry.Length == 10) return "+48" + cyfry.Substring(1);
            // 9 cyfr bez prefiksu → +48xxxxxxxxx
            if (cyfry.Length == 9) return "+48" + cyfry;

            // Inne — jeśli ma sensowną długość, dodaj + na początek
            return cyfry.Length >= 9 ? "+" + cyfry : null;
        }

        // ===== POBIERANIE NUMERU Z BAZY DOSTAWCY =====
        // Phone1; jeśli pusty → Phone2.
        public static async Task<(string? raw, string? normalized)> PobierzNumerDostawcyAsync(
            string connectionString, string dostawca)
        {
            if (string.IsNullOrWhiteSpace(dostawca)) return (null, null);
            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT TOP 1
                        ISNULL(Phone1, '') AS P1,
                        ISNULL(Phone2, '') AS P2
                    FROM dbo.Dostawcy
                    WHERE ShortName = @n OR Name = @n", conn);
                cmd.Parameters.AddWithValue("@n", dostawca);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    string p1 = r.GetString(0).Trim();
                    string p2 = r.GetString(1).Trim();
                    string wybrany = !string.IsNullOrEmpty(p1) && p1 != "-" ? p1 : p2;
                    return (wybrany, NormalizujNumer(wybrany));
                }
            }
            catch { }
            return (null, null);
        }

        // ===== CORE: HTTP POST plain text =====
        // Wysyła czysty tekst (np. numer telefonu) jako body — bez serializacji JSON.
        private static async Task<Wynik> PostPlainAsync(string? userId, string path, string plainBody)
        {
            var settings = TestSmsDialog.WczytajUstawienia(userId);
            if (settings == null || string.IsNullOrWhiteSpace(settings.Ip) || settings.Port <= 0)
                return new Wynik { Sukces = false, Komunikat = "Telefon nie jest skonfigurowany w ZPSP" };

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                if (!string.IsNullOrEmpty(settings.AuthToken))
                    http.DefaultRequestHeaders.Add("X-Auth-Token", settings.AuthToken);

                using var content = new StringContent(plainBody ?? "", Encoding.UTF8, "text/plain");
                string url = $"http://{settings.Ip}:{settings.Port}{path}";
                using var resp = await http.PostAsync(url, content);
                string body = await resp.Content.ReadAsStringAsync();
                return new Wynik
                {
                    Sukces = resp.IsSuccessStatusCode,
                    StatusKod = (int)resp.StatusCode,
                    Komunikat = resp.IsSuccessStatusCode ? $"HTTP {(int)resp.StatusCode}" : $"HTTP {(int)resp.StatusCode} — {body}",
                    TrescOdpowiedzi = body
                };
            }
            catch (TaskCanceledException) { return new Wynik { Sukces = false, Komunikat = "Telefon nieosiągalny (timeout 8s)" }; }
            catch (Exception ex) { return new Wynik { Sukces = false, Komunikat = $"{ex.GetType().Name}: {ex.Message}" }; }
        }

        // ===== CORE: HTTP POST =====
        private static async Task<Wynik> PostAsync(string? userId, string path, object payload)
        {
            var settings = TestSmsDialog.WczytajUstawienia(userId);
            if (settings == null || string.IsNullOrWhiteSpace(settings.Ip) || settings.Port <= 0)
            {
                return new Wynik { Sukces = false, Komunikat = "Telefon nie jest skonfigurowany w ZPSP" };
            }

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                if (!string.IsNullOrEmpty(settings.AuthToken))
                    http.DefaultRequestHeaders.Add("X-Auth-Token", settings.AuthToken);

                // UnsafeRelaxedJsonEscaping żeby "+" w numerze szedł jako "+" a nie "+"
                // (MacroDroid nie dekoduje unicode escapes w JSON value)
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                string url = $"http://{settings.Ip}:{settings.Port}{path}";
                using var resp = await http.PostAsync(url, content);
                string body = await resp.Content.ReadAsStringAsync();

                return new Wynik
                {
                    Sukces = resp.IsSuccessStatusCode,
                    StatusKod = (int)resp.StatusCode,
                    Komunikat = resp.IsSuccessStatusCode
                        ? $"HTTP {(int)resp.StatusCode}"
                        : $"HTTP {(int)resp.StatusCode} — {body}",
                    TrescOdpowiedzi = body
                };
            }
            catch (TaskCanceledException)
            {
                return new Wynik
                {
                    Sukces = false,
                    Komunikat = "Telefon nieosiągalny (timeout 8s) — sprawdź MacroDroid / IP / sieć"
                };
            }
            catch (HttpRequestException ex)
            {
                return new Wynik
                {
                    Sukces = false,
                    Komunikat = $"Błąd połączenia: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new Wynik
                {
                    Sukces = false,
                    Komunikat = $"{ex.GetType().Name}: {ex.Message}"
                };
            }
        }
    }
}
