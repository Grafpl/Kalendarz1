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

        // Domyślna lokalizacja (kod pocztowy w Polsce)
        private static string _location = "95-061,Poland";

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

        public class HourlyForecast
        {
            public DateTime DateTime { get; set; }
            public int Temperature { get; set; }
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
            // Kody pogody wttr.in -> czytelne tekstowe oznaczenia
            if (code == 113) return "[S]";   // Sunny - słonecznie
            if (code == 116) return "[S/C]"; // Partly cloudy
            if (code == 119 || code == 122) return "[C]";   // Cloudy
            if (code >= 176 && code <= 185) return "[d]";   // Light rain
            if (code >= 200 && code <= 232) return "[B]";   // Burza
            if (code >= 260 && code <= 284) return "[M]";   // Mgła
            if (code >= 293 && code <= 314) return "[D]";   // Deszcz
            if (code >= 317 && code <= 350) return "[DS]";  // Deszcz+Śnieg
            if (code >= 353 && code <= 367) return "[D]";   // Deszcz
            if (code >= 368 && code <= 395) return "[SN]";  // Śnieg
            return "[-]";
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

        /// <summary>
        /// Pobiera prognozę godzinową na 4 dni co 5 godzin
        /// </summary>
        public static async Task<List<HourlyForecast>> GetHourlyForecastAsync()
        {
            var result = new List<HourlyForecast>();

            try
            {
                var url = $"https://wttr.in/{_location}?format=j1";
                var response = await httpClient.GetStringAsync(url);
                var json = JsonDocument.Parse(response);

                var weatherList = json.RootElement.GetProperty("weather");
                var today = DateTime.Today;

                // wttr.in zwraca dane godzinowe co 3 godziny (8 wpisów na dzień)
                // indeksy: 0=00:00, 1=03:00, 2=06:00, 3=09:00, 4=12:00, 5=15:00, 6=18:00, 7=21:00
                int dayIndex = 0;
                int currentHour = DateTime.Now.Hour;

                foreach (var day in weatherList.EnumerateArray())
                {
                    if (dayIndex >= 4) break; // Tylko 4 dni

                    var date = DateTime.Parse(day.GetProperty("date").GetString());
                    var hourlyArray = day.GetProperty("hourly");

                    // Dla każdego dnia wybierz odpowiednie godziny (co 5 godzin)
                    // Zaczynamy od godziny startowej i bierzemy co 5 godzin
                    int[] targetHours;

                    if (dayIndex == 0)
                    {
                        // Pierwszy dzień - zacznij od najbliższej pełnej godziny
                        targetHours = GetTargetHoursFromCurrentHour(currentHour);
                    }
                    else
                    {
                        // Kolejne dni - standardowe godziny co 5h
                        targetHours = new[] { 0, 5, 10, 15, 20 };
                    }

                    foreach (var targetHour in targetHours)
                    {
                        // Znajdź najbliższy slot 3-godzinny
                        int slotIndex = GetClosestSlotIndex(targetHour);
                        if (slotIndex >= 0 && slotIndex < 8)
                        {
                            var hourlyData = hourlyArray[slotIndex];
                            var actualHour = slotIndex * 3;
                            var forecastTime = date.AddHours(actualHour);

                            // Pomiń przeszłe godziny z dzisiejszego dnia
                            if (dayIndex == 0 && forecastTime < DateTime.Now.AddHours(-1))
                                continue;

                            result.Add(new HourlyForecast
                            {
                                DateTime = forecastTime,
                                Temperature = int.Parse(hourlyData.GetProperty("tempC").GetString()),
                                Icon = GetWeatherIcon(int.Parse(hourlyData.GetProperty("weatherCode").GetString())),
                                Description = GetPolishDescription(hourlyData.GetProperty("weatherDesc")[0].GetProperty("value").GetString())
                            });
                        }
                    }

                    dayIndex++;
                }

                // Ogranicz do sensownej liczby punktów (ok. 19-20 dla 4 dni co 5h)
                return result.Take(20).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania prognozy godzinowej: {ex.Message}");
                return result;
            }
        }

        private static int[] GetTargetHoursFromCurrentHour(int currentHour)
        {
            var hours = new List<int>();
            // Zacznij od najbliższej godziny podzielnej przez 5 lub następnej
            int startHour = ((currentHour / 5) + 1) * 5;
            if (startHour > 20) return new int[0]; // Jeśli za późno, pomiń dzień

            for (int h = startHour; h <= 23; h += 5)
            {
                hours.Add(h);
            }
            return hours.ToArray();
        }

        private static int GetClosestSlotIndex(int targetHour)
        {
            // Sloty: 0, 3, 6, 9, 12, 15, 18, 21
            // Znajdź najbliższy
            int[] slots = { 0, 3, 6, 9, 12, 15, 18, 21 };
            int closestIndex = 0;
            int minDiff = Math.Abs(slots[0] - targetHour);

            for (int i = 1; i < slots.Length; i++)
            {
                int diff = Math.Abs(slots[i] - targetHour);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }
    }
}
