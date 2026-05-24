using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kalendarz1.ZSRIR.Services
{
    // Secrets ZSRIR — poza repo (analogicznie do CentrumNagranAI).
    // Lokalizacja: %LOCALAPPDATA%\Kalendarz1\Zsrir\secrets.json
    public class ZsrirSecrets
    {
        [JsonPropertyName("apiBaseUrl")] public string ApiBaseUrl { get; set; } = "https://zsrir.minrol.gov.pl/api";
        [JsonPropertyName("username")]   public string Username { get; set; } = "";
        [JsonPropertyName("password")]   public string Password { get; set; } = "";
        [JsonPropertyName("dataSupplierId")] public int? DataSupplierId { get; set; }   // wybrany dostawca (jeśli kilka)
        [JsonPropertyName("formId")]     public int? FormId { get; set; }                // wybrany formularz "Drób rzeźny"

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
    }

    public static class ZsrirSecretsManager
    {
        private static string SecretsPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Kalendarz1", "Zsrir");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "secrets.json");
            }
        }

        public static ZsrirSecrets Load()
        {
            try
            {
                if (!File.Exists(SecretsPath)) return new ZsrirSecrets();
                string json = File.ReadAllText(SecretsPath);
                return JsonSerializer.Deserialize<ZsrirSecrets>(json) ?? new ZsrirSecrets();
            }
            catch { return new ZsrirSecrets(); }
        }

        public static void Save(ZsrirSecrets s)
        {
            string json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SecretsPath, json);
        }

        public static string GetSecretsPath() => SecretsPath;
    }
}
