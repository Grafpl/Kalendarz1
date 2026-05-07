using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Twilio SMS dla alertów. Konfiguracja w secrets.json:
    ///   "Twilio": {
    ///     "AccountSid": "ACxxx",
    ///     "AuthToken": "xxx",
    ///     "From": "+48123456789",
    ///     "To": "+48987654321"
    ///   }
    /// Pominięte = SMS wyłączony, alerty tylko w bazie + log.
    /// </summary>
    public static class NotifyService
    {
        private class TwilioConfig
        {
            public string AccountSid = string.Empty;
            public string AuthToken = string.Empty;
            public string From = string.Empty;
            public string To = string.Empty;
            public bool IsValid => !string.IsNullOrEmpty(AccountSid) && !string.IsNullOrEmpty(AuthToken)
                                && !string.IsNullOrEmpty(From) && !string.IsNullOrEmpty(To);
        }

        private static TwilioConfig? _cached;
        private static readonly object _initLock = new();

        private static TwilioConfig? GetTwilio()
        {
            if (_cached != null) return _cached.IsValid ? _cached : null;
            lock (_initLock)
            {
                if (_cached != null) return _cached.IsValid ? _cached : null;
                var cfg = new TwilioConfig();
                try
                {
                    if (File.Exists(CnaConfig.SecretsPath))
                    {
                        var json = JObject.Parse(File.ReadAllText(CnaConfig.SecretsPath));
                        var t = json["Twilio"];
                        if (t != null)
                        {
                            cfg.AccountSid = (string?)t["AccountSid"] ?? string.Empty;
                            cfg.AuthToken = (string?)t["AuthToken"] ?? string.Empty;
                            cfg.From = (string?)t["From"] ?? string.Empty;
                            cfg.To = (string?)t["To"] ?? string.Empty;
                        }
                    }
                }
                catch { }
                _cached = cfg;
                return cfg.IsValid ? cfg : null;
            }
        }

        public static bool IsConfigured => GetTwilio() != null;

        public static async Task SendAlertSmsAsync(GuardRule rule, string cameraId, int score, string reason, CancellationToken ct = default)
        {
            var cfg = GetTwilio();
            if (cfg == null)
            {
                Log($"SMS pominięty (Twilio nieskonfigurowany) - reguła '{rule.Name}'");
                return;
            }

            string camName = CnaConfig.DisplayName(cameraId);
            string body = $"⚠ ZPSP-CNA: {rule.Name}\n" +
                          $"Kamera: {camName}\n" +
                          $"Score: {score}/100\n" +
                          $"{reason}\n" +
                          $"Czas: {DateTime.Now:HH:mm}";
            // Twilio limit ~1600 znaków, ale praktycznie 160 dla pojedynczego SMS - niech będzie krótko.
            if (body.Length > 320) body = body.Substring(0, 320);

            await SendSmsAsync(cfg, body, ct);
            Log($"SMS wysłany: reguła='{rule.Name}' kamera={camName}");
        }

        private static async Task SendSmsAsync(TwilioConfig cfg, string body, CancellationToken ct)
        {
            // Używamy Twilio SDK z istniejącego NuGet (już w solution: Twilio 7.1.0).
            try
            {
                Twilio.TwilioClient.Init(cfg.AccountSid, cfg.AuthToken);
                await Twilio.Rest.Api.V2010.Account.MessageResource.CreateAsync(
                    body: body,
                    from: new Twilio.Types.PhoneNumber(cfg.From),
                    to: new Twilio.Types.PhoneNumber(cfg.To)
                );
            }
            catch (Exception ex)
            {
                Log($"Twilio SDK fail: {ex.Message}");
                throw;
            }
        }

        private static void Log(string msg)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff} [CNA-Notify] {msg}";
            Debug.WriteLine(line);
            try
            {
                Directory.CreateDirectory(CnaConfig.AuditDir);
                File.AppendAllText(Path.Combine(CnaConfig.AuditDir, "cna_notify.log"),
                    line + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}
