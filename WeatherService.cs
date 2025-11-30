using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Kalendarz1
{
    /// <summary>
    /// Serwis pogodowy wykorzystujący darmowe API Open-Meteo (nie wymaga klucza API)
    /// </summary>
    public class WeatherService
    {
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // Współrzędne Dmosin (okolice ubojni)
        private const double LATITUDE = 51.93;
        private const double LONGITUDE = 19.85;

        /// <summary>
        /// Pobiera pogodę dla konkretnej daty i godziny
        /// </summary>
        public static async Task<WeatherInfo> GetWeatherAsync(DateTime dateTime)
        {
            try
            {
                string dateStr = dateTime.ToString("yyyy-MM-dd");
                int hour = dateTime.Hour;

                // Użyj InvariantCulture dla współrzędnych (unikaj przecinków)
                string latStr = LATITUDE.ToString(CultureInfo.InvariantCulture);
                string lonStr = LONGITUDE.ToString(CultureInfo.InvariantCulture);

                // Open-Meteo API - darmowe, bez klucza
                string url;

                // Sprawdź czy data jest w przeszłości (archiwum) czy niedawno (forecast)
                if (dateTime.Date < DateTime.Today.AddDays(-5))
                {
                    // Dane archiwalne
                    url = $"https://archive-api.open-meteo.com/v1/archive?" +
                          $"latitude={latStr}&longitude={lonStr}" +
                          $"&start_date={dateStr}&end_date={dateStr}" +
                          $"&hourly=temperature_2m,weathercode,relativehumidity_2m,windspeed_10m" +
                          $"&timezone=Europe/Warsaw";
                }
                else
                {
                    // Dane z ostatnich dni lub prognoza (max 7 dni w przyszłość)
                    url = $"https://api.open-meteo.com/v1/forecast?" +
                          $"latitude={latStr}&longitude={lonStr}" +
                          $"&hourly=temperature_2m,weathercode,relativehumidity_2m,windspeed_10m" +
                          $"&past_days=14&forecast_days=7" +
                          $"&timezone=Europe/Warsaw";
                }

                var response = await httpClient.GetStringAsync(url).ConfigureAwait(false);
                var json = JObject.Parse(response);

                var hourlyData = json["hourly"];
                var times = hourlyData["time"].ToObject<string[]>();
                var temperatures = hourlyData["temperature_2m"].ToObject<double?[]>();
                var weatherCodes = hourlyData["weathercode"].ToObject<int?[]>();
                var humidity = hourlyData["relativehumidity_2m"]?.ToObject<int?[]>();
                var windSpeed = hourlyData["windspeed_10m"]?.ToObject<double?[]>();

                // Znajdź indeks dla żądanej godziny
                string targetTime = $"{dateStr}T{hour:D2}:00";
                int index = Array.FindIndex(times, t => t == targetTime);

                if (index >= 0 && index < temperatures.Length)
                {
                    return new WeatherInfo
                    {
                        Temperature = temperatures[index] ?? 0,
                        WeatherCode = weatherCodes[index] ?? 0,
                        Humidity = humidity?[index] ?? 0,
                        WindSpeed = windSpeed?[index] ?? 0,
                        Description = GetWeatherDescription(weatherCodes[index] ?? 0),
                        Icon = GetWeatherIcon(weatherCodes[index] ?? 0),
                        DateTime = dateTime
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania pogody: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Synchroniczna wersja dla łatwiejszego użycia w PDF
        /// </summary>
        public static WeatherInfo GetWeather(DateTime dateTime)
        {
            try
            {
                // Sprawdź czy data jest w sensownym zakresie
                if (dateTime.Year < 2020 || dateTime > DateTime.Today.AddDays(7))
                {
                    return null;
                }

                // Użyj Task.Run z ConfigureAwait(false) aby uniknąć deadlocków w WPF
                var task = Task.Run(async () => await GetWeatherAsync(dateTime).ConfigureAwait(false));
                if (task.Wait(TimeSpan.FromSeconds(8)))
                {
                    return task.Result;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Konwertuje kod pogodowy WMO na opis po polsku
        /// </summary>
        private static string GetWeatherDescription(int code)
        {
            return code switch
            {
                0 => "Bezchmurnie",
                1 => "Przeważnie bezchmurnie",
                2 => "Częściowe zachmurzenie",
                3 => "Pochmurno",
                45 => "Mgła",
                48 => "Mgła szronowa",
                51 => "Lekka mżawka",
                53 => "Umiarkowana mżawka",
                55 => "Gęsta mżawka",
                56 => "Marznąca mżawka",
                57 => "Silna marznąca mżawka",
                61 => "Lekki deszcz",
                63 => "Umiarkowany deszcz",
                65 => "Silny deszcz",
                66 => "Marznący deszcz",
                67 => "Silny marznący deszcz",
                71 => "Lekki śnieg",
                73 => "Umiarkowany śnieg",
                75 => "Silny śnieg",
                77 => "Ziarna śniegu",
                80 => "Lekkie przelotne opady",
                81 => "Umiarkowane przelotne opady",
                82 => "Gwałtowne przelotne opady",
                85 => "Lekki śnieg przelotny",
                86 => "Silny śnieg przelotny",
                95 => "Burza",
                96 => "Burza z gradem",
                99 => "Silna burza z gradem",
                _ => "Brak danych"
            };
        }

        /// <summary>
        /// Zwraca emoji ikony pogody
        /// </summary>
        private static string GetWeatherIcon(int code)
        {
            return code switch
            {
                0 => "☀️",
                1 or 2 => "🌤️",
                3 => "☁️",
                45 or 48 => "🌫️",
                51 or 53 or 55 => "🌧️",
                56 or 57 => "🌧️❄️",
                61 or 63 or 65 => "🌧️",
                66 or 67 => "🌧️❄️",
                71 or 73 or 75 or 77 => "❄️",
                80 or 81 or 82 => "🌦️",
                85 or 86 => "🌨️",
                95 or 96 or 99 => "⛈️",
                _ => "❓"
            };
        }
    }

    /// <summary>
    /// Model danych pogodowych
    /// </summary>
    public class WeatherInfo
    {
        public double Temperature { get; set; }
        public int WeatherCode { get; set; }
        public int Humidity { get; set; }
        public double WindSpeed { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public DateTime DateTime { get; set; }

        public override string ToString()
        {
            return $"{Temperature:0.0}°C, {Description}";
        }

        public string ToShortString()
        {
            return $"{Temperature:0}°C {Description}";
        }
    }
}
