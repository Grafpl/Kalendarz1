using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kalendarz1
{
    /// <summary>
    /// Zarządza pobieraniem danych pogodowych
    /// </summary>
    public static class WeatherManager
    {
        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZPSP", "Weather");

        private static readonly string CacheFile = Path.Combine(CacheDirectory, "weather_cache.json");
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // Domyślna lokalizacja (Polska)
        private static string _location = "Warsaw";

        public class WeatherData
        {
            public string Location { get; set; }
            public int Temperature { get; set; }
            public string Description { get; set; }
            public string Icon { get; set; }
            public DateTime UpdateTime { get; set; }
            public List<DayForecast> Forecast { get; set; } = new List<DayForecast>();
        }

        public class DayForecast
        {
            public string DayName { get; set; }
            public int TempMin { get; set; }
            public int TempMax { get; set; }
            public string Icon { get; set; }
            public string Description { get; set; }
        }

        /// <summary>
        /// Ustawia lokalizację dla pogody
        /// </summary>
        public static void SetLocation(string location)
        {
            _location = location;
        }

        /// <summary>
        /// Pobiera aktualną pogodę (z cache lub z sieci)
        /// </summary>
        public static async Task<WeatherData> GetWeatherAsync()
        {
            // Sprawdź cache (ważny 30 minut)
            var cached = LoadFromCache();
            if (cached != null && (DateTime.Now - cached.UpdateTime).TotalMinutes < 30)
                return cached;

            // Pobierz z sieci
            try
            {
                var weather = await FetchWeatherFromApiAsync();
                if (weather != null)
                {
                    SaveToCache(weather);
                    return weather;
                }
            }
            catch { }

            // Zwróć cache nawet jeśli stary
            return cached ?? GetDefaultWeather();
        }

        /// <summary>
        /// Pobiera pogodę synchronicznie (dla UI)
        /// </summary>
        public static WeatherData GetWeather()
        {
            var cached = LoadFromCache();
            if (cached != null && (DateTime.Now - cached.UpdateTime).TotalMinutes < 30)
                return cached;

            // Uruchom asynchronicznie w tle
            Task.Run(async () =>
            {
                try
                {
                    var weather = await FetchWeatherFromApiAsync();
                    if (weather != null)
                        SaveToCache(weather);
                }
                catch { }
            });

            return cached ?? GetDefaultWeather();
        }

        private static async Task<WeatherData> FetchWeatherFromApiAsync()
        {
            try
            {
                // Używamy wttr.in API - darmowe, bez klucza
                var url = $"https://wttr.in/{_location}?format=j1";
                var response = await httpClient.GetStringAsync(url);
                var json = JsonDocument.Parse(response);

                var current = json.RootElement.GetProperty("current_condition")[0];
                var weatherList = json.RootElement.GetProperty("weather");

                var weather = new WeatherData
                {
                    Location = _location,
                    Temperature = int.Parse(current.GetProperty("temp_C").GetString()),
                    Description = GetPolishDescription(current.GetProperty("weatherDesc")[0].GetProperty("value").GetString()),
                    Icon = GetWeatherIcon(int.Parse(current.GetProperty("weatherCode").GetString())),
                    UpdateTime = DateTime.Now,
                    Forecast = new List<DayForecast>()
                };

                // Prognoza na 7 dni
                var culture = new System.Globalization.CultureInfo("pl-PL");
                foreach (var day in weatherList.EnumerateArray())
                {
                    var date = DateTime.Parse(day.GetProperty("date").GetString());
                    var dayName = culture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek);
                    dayName = char.ToUpper(dayName[0]) + dayName.Substring(1);

                    var hourly = day.GetProperty("hourly")[4]; // Około południe
                    weather.Forecast.Add(new DayForecast
                    {
                        DayName = dayName,
                        TempMin = int.Parse(day.GetProperty("mintempC").GetString()),
                        TempMax = int.Parse(day.GetProperty("maxtempC").GetString()),
                        Icon = GetWeatherIcon(int.Parse(hourly.GetProperty("weatherCode").GetString())),
                        Description = GetPolishDescription(hourly.GetProperty("weatherDesc")[0].GetProperty("value").GetString())
                    });
                }

                return weather;
            }
            catch
            {
                return null;
            }
        }

        private static string GetWeatherIcon(int code)
        {
            // Kody pogody wttr.in -> emoji
            if (code == 113) return "☀️";  // Sunny
            if (code == 116) return "⛅";  // Partly cloudy
            if (code == 119 || code == 122) return "☁️";  // Cloudy
            if (code >= 176 && code <= 185) return "🌧️";  // Light rain
            if (code >= 200 && code <= 232) return "⛈️";  // Thunderstorm
            if (code >= 260 && code <= 284) return "🌫️";  // Fog
            if (code >= 293 && code <= 314) return "🌧️";  // Rain
            if (code >= 317 && code <= 350) return "🌨️";  // Sleet
            if (code >= 353 && code <= 395) return "🌧️";  // Rain/Snow
            if (code >= 368 && code <= 395) return "❄️";  // Snow
            return "🌡️";
        }

        private static string GetPolishDescription(string englishDesc)
        {
            var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Sunny", "Słonecznie" },
                { "Clear", "Bezchmurnie" },
                { "Partly cloudy", "Częściowe zachmurzenie" },
                { "Cloudy", "Pochmurno" },
                { "Overcast", "Zachmurzenie całkowite" },
                { "Mist", "Mgła" },
                { "Fog", "Mgła" },
                { "Light rain", "Lekki deszcz" },
                { "Rain", "Deszcz" },
                { "Heavy rain", "Silny deszcz" },
                { "Light snow", "Lekki śnieg" },
                { "Snow", "Śnieg" },
                { "Heavy snow", "Silny śnieg" },
                { "Thunderstorm", "Burza" },
                { "Patchy rain possible", "Możliwe opady" },
                { "Patchy snow possible", "Możliwy śnieg" },
                { "Patchy sleet possible", "Możliwe opady śniegu z deszczem" },
                { "Moderate rain", "Umiarkowany deszcz" },
                { "Moderate snow", "Umiarkowany śnieg" },
                { "Light drizzle", "Mżawka" },
                { "Freezing drizzle", "Marznąca mżawka" },
                { "Light freezing rain", "Lekki marznący deszcz" }
            };

            return translations.TryGetValue(englishDesc.Trim(), out var polish) ? polish : englishDesc;
        }

        private static WeatherData LoadFromCache()
        {
            try
            {
                if (!File.Exists(CacheFile))
                    return null;

                var json = File.ReadAllText(CacheFile);
                return JsonSerializer.Deserialize<WeatherData>(json);
            }
            catch
            {
                return null;
            }
        }

        private static void SaveToCache(WeatherData weather)
        {
            try
            {
                if (!Directory.Exists(CacheDirectory))
                    Directory.CreateDirectory(CacheDirectory);

                var json = JsonSerializer.Serialize(weather, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CacheFile, json);
            }
            catch { }
        }

        private static WeatherData GetDefaultWeather()
        {
            return new WeatherData
            {
                Location = _location,
                Temperature = 0,
                Description = "Brak danych",
                Icon = "🌡️",
                UpdateTime = DateTime.MinValue,
                Forecast = new List<DayForecast>()
            };
        }
    }
}
