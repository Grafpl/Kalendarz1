using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Kalendarz1
{
    /// <summary>
    /// Manager kursów walut - pobiera kursy z API NBP
    /// </summary>
    public static class CurrencyManager
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static string _cacheDir;
        private static CurrencyData _cachedData;
        private static DateTime _lastFetch = DateTime.MinValue;
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(1);

        public class CurrencyData
        {
            public decimal EurRate { get; set; }
            public decimal UsdRate { get; set; }
            public string EurChange { get; set; } = "";
            public string UsdChange { get; set; } = "";
            public DateTime Date { get; set; }
            public bool IsValid { get; set; }
        }

        public class CurrencyHistoryItem
        {
            public DateTime Date { get; set; }
            public decimal Rate { get; set; }
        }

        static CurrencyManager()
        {
            _cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ZPSP", "Currency");
            Directory.CreateDirectory(_cacheDir);
        }

        public static CurrencyData GetCurrency()
        {
            // Sprawdź cache w pamięci
            if (_cachedData != null && DateTime.Now - _lastFetch < CacheExpiry)
            {
                return _cachedData;
            }

            // Sprawdź cache na dysku
            var cacheFile = Path.Combine(_cacheDir, "currency_cache.json");
            if (File.Exists(cacheFile))
            {
                try
                {
                    var cacheTime = File.GetLastWriteTime(cacheFile);
                    if (DateTime.Now - cacheTime < CacheExpiry)
                    {
                        var json = File.ReadAllText(cacheFile);
                        var cached = JObject.Parse(json);
                        _cachedData = new CurrencyData
                        {
                            EurRate = cached["eur"]?.Value<decimal>() ?? 0,
                            UsdRate = cached["usd"]?.Value<decimal>() ?? 0,
                            EurChange = cached["eurChange"]?.Value<string>() ?? "",
                            UsdChange = cached["usdChange"]?.Value<string>() ?? "",
                            Date = cached["date"]?.Value<DateTime>() ?? DateTime.Today,
                            IsValid = true
                        };
                        _lastFetch = DateTime.Now;
                        return _cachedData;
                    }
                }
                catch { }
            }

            // Pobierz świeże dane asynchronicznie
            Task.Run(async () => await FetchCurrencyAsync());

            // Zwróć stare dane lub puste
            return _cachedData ?? new CurrencyData { IsValid = false };
        }

        public static async Task<CurrencyData> GetCurrencyAsync()
        {
            // Sprawdź cache
            if (_cachedData != null && DateTime.Now - _lastFetch < CacheExpiry)
            {
                return _cachedData;
            }

            await FetchCurrencyAsync();
            return _cachedData ?? new CurrencyData { IsValid = false };
        }

        private static async Task FetchCurrencyAsync()
        {
            try
            {
                // Pobierz EUR
                var eurResponse = await _httpClient.GetStringAsync(
                    "https://api.nbp.pl/api/exchangerates/rates/a/eur/last/2/?format=json");
                var eurJson = JObject.Parse(eurResponse);
                var eurRates = eurJson["rates"] as JArray;

                decimal eurToday = eurRates?[1]?["mid"]?.Value<decimal>() ?? 0;
                decimal eurYesterday = eurRates?[0]?["mid"]?.Value<decimal>() ?? 0;
                decimal eurDiff = eurToday - eurYesterday;
                string eurChange = eurDiff >= 0 ? $"+{eurDiff:F4}" : $"{eurDiff:F4}";

                // Pobierz USD
                var usdResponse = await _httpClient.GetStringAsync(
                    "https://api.nbp.pl/api/exchangerates/rates/a/usd/last/2/?format=json");
                var usdJson = JObject.Parse(usdResponse);
                var usdRates = usdJson["rates"] as JArray;

                decimal usdToday = usdRates?[1]?["mid"]?.Value<decimal>() ?? 0;
                decimal usdYesterday = usdRates?[0]?["mid"]?.Value<decimal>() ?? 0;
                decimal usdDiff = usdToday - usdYesterday;
                string usdChange = usdDiff >= 0 ? $"+{usdDiff:F4}" : $"{usdDiff:F4}";

                _cachedData = new CurrencyData
                {
                    EurRate = eurToday,
                    UsdRate = usdToday,
                    EurChange = eurChange,
                    UsdChange = usdChange,
                    Date = DateTime.Today,
                    IsValid = true
                };
                _lastFetch = DateTime.Now;

                // Zapisz cache
                var cacheFile = Path.Combine(_cacheDir, "currency_cache.json");
                var cacheJson = new JObject
                {
                    ["eur"] = eurToday,
                    ["usd"] = usdToday,
                    ["eurChange"] = eurChange,
                    ["usdChange"] = usdChange,
                    ["date"] = DateTime.Today
                };
                File.WriteAllText(cacheFile, cacheJson.ToString());
            }
            catch (Exception)
            {
                // W przypadku błędu zachowaj stare dane
                if (_cachedData == null)
                {
                    _cachedData = new CurrencyData { IsValid = false };
                }
            }
        }

        /// <summary>
        /// Pobiera historyczne kursy EUR z ostatnich 2 miesiecy
        /// </summary>
        public static async Task<List<CurrencyHistoryItem>> GetEurHistoryAsync()
        {
            var result = new List<CurrencyHistoryItem>();

            try
            {
                // Oblicz daty - 2 miesiace wstecz
                var endDate = DateTime.Today;
                var startDate = endDate.AddMonths(-2);

                // NBP API format: YYYY-MM-DD
                var startStr = startDate.ToString("yyyy-MM-dd");
                var endStr = endDate.ToString("yyyy-MM-dd");

                // Pobierz dane z API NBP
                var url = $"https://api.nbp.pl/api/exchangerates/rates/a/eur/{startStr}/{endStr}/?format=json";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);
                var rates = json["rates"] as JArray;

                if (rates != null)
                {
                    foreach (var rate in rates)
                    {
                        var dateStr = rate["effectiveDate"]?.Value<string>();
                        var mid = rate["mid"]?.Value<decimal>() ?? 0;

                        if (DateTime.TryParse(dateStr, out var date))
                        {
                            result.Add(new CurrencyHistoryItem
                            {
                                Date = date,
                                Rate = mid
                            });
                        }
                    }
                }
            }
            catch (Exception)
            {
                // W przypadku bledu zwroc pusta liste
            }

            return result;
        }
    }
}
