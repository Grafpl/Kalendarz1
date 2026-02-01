using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;

namespace Kalendarz1.Kartoteka.Features.Mapa
{
    public class GeokodowanieService
    {
        private readonly string _connLibra;
        private static readonly HttpClient _http = new HttpClient();
        private DateTime _lastRequest = DateTime.MinValue;

        static GeokodowanieService()
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "ZPSP-Kartoteka/1.0");
        }

        public GeokodowanieService(string connLibra)
        {
            _connLibra = connLibra;
        }

        public async Task EnsureColumnsExistAsync()
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.KartotekaOdbiorcyDane') AND name = 'Latitude')
                    ALTER TABLE dbo.KartotekaOdbiorcyDane ADD Latitude DECIMAL(9,6);
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.KartotekaOdbiorcyDane') AND name = 'Longitude')
                    ALTER TABLE dbo.KartotekaOdbiorcyDane ADD Longitude DECIMAL(9,6);
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.KartotekaOdbiorcyDane') AND name = 'GeokodowanieData')
                    ALTER TABLE dbo.KartotekaOdbiorcyDane ADD GeokodowanieData DATETIME;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.KartotekaOdbiorcyDane') AND name = 'GeokodowanieStatus')
                    ALTER TABLE dbo.KartotekaOdbiorcyDane ADD GeokodowanieStatus NVARCHAR(50);";

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Geokoduj adres za pomocą Nominatim (OpenStreetMap) - darmowe, bez API key.
        /// Limit: 1 request/sekundę.
        /// </summary>
        public async Task<(double Lat, double Lng)?> GeokodujAdresAsync(string adres, string miasto, string kodPocztowy)
        {
            // Rate limiting - minimum 1 sekunda między requestami
            var elapsed = DateTime.Now - _lastRequest;
            if (elapsed.TotalMilliseconds < 1100)
                await Task.Delay(1100 - (int)elapsed.TotalMilliseconds);

            string query = $"{adres}, {kodPocztowy} {miasto}, Polska";
            string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=1&countrycodes=pl";

            try
            {
                _lastRequest = DateTime.Now;
                var response = await _http.GetStringAsync(url);
                var array = JArray.Parse(response);
                if (array.Count > 0)
                {
                    double lat = array[0]["lat"].Value<double>();
                    double lng = array[0]["lon"].Value<double>();
                    return (lat, lng);
                }

                // Fallback: spróbuj tylko z miastem
                if (!string.IsNullOrEmpty(miasto))
                {
                    await Task.Delay(1100);
                    string fallbackUrl = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(miasto + ", Polska")}&format=json&limit=1&countrycodes=pl";
                    _lastRequest = DateTime.Now;
                    response = await _http.GetStringAsync(fallbackUrl);
                    array = JArray.Parse(response);
                    if (array.Count > 0)
                    {
                        double lat = array[0]["lat"].Value<double>();
                        double lng = array[0]["lon"].Value<double>();
                        return (lat, lng);
                    }
                }
            }
            catch { }

            return null;
        }

        public async Task ZapiszWspolrzedneAsync(int idSymfonia, double lat, double lng, string status)
        {
            const string sql = @"
                UPDATE dbo.KartotekaOdbiorcyDane
                SET Latitude = @Lat, Longitude = @Lng, GeokodowanieData = GETDATE(), GeokodowanieStatus = @Status
                WHERE IdSymfonia = @Id";

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Id", idSymfonia);
            cmd.Parameters.AddWithValue("@Lat", lat);
            cmd.Parameters.AddWithValue("@Lng", lng);
            cmd.Parameters.AddWithValue("@Status", status);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task ZapiszBladGeokodowaniaAsync(int idSymfonia, string status)
        {
            const string sql = @"
                UPDATE dbo.KartotekaOdbiorcyDane
                SET GeokodowanieData = GETDATE(), GeokodowanieStatus = @Status
                WHERE IdSymfonia = @Id";

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Id", idSymfonia);
            cmd.Parameters.AddWithValue("@Status", status);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<Dictionary<int, (double Lat, double Lng)>> PobierzWspolrzedneAsync()
        {
            const string sql = @"SELECT IdSymfonia, Latitude, Longitude FROM dbo.KartotekaOdbiorcyDane
                                 WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL";
            var wynik = new Dictionary<int, (double, double)>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    double lat = (double)rd.GetDecimal(1);
                    double lng = (double)rd.GetDecimal(2);
                    wynik[id] = (lat, lng);
                }
            }
            catch { }
            return wynik;
        }
    }
}
