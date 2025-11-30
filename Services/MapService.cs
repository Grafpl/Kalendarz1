using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Serwis mapy hodowców - wizualizacja lokalizacji i tras
    /// Obsługuje geokodowanie adresów i generowanie map
    /// </summary>
    public class MapService
    {
        private readonly string _connectionString;
        private readonly string _geocodingApiKey;
        private static readonly HttpClient _httpClient = new HttpClient();

        // Lokalizacja ubojni Piórkowscy (Koziołki 40, Dmosin)
        private const double UBOJNIA_LAT = 51.9301;
        private const double UBOJNIA_LON = 19.8512;

        // Domyślny connection string do LibraNet
        private const string DEFAULT_CONNECTION_STRING =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public MapService(string connectionString = null, string geocodingApiKey = null)
        {
            _connectionString = connectionString ?? DEFAULT_CONNECTION_STRING;
            _geocodingApiKey = geocodingApiKey;
        }

        /// <summary>
        /// Pobiera listę hodowców z lokalizacjami
        /// </summary>
        public List<HodowcaLokalizacja> GetHodowcyLokalizacje(DateTime? odDaty = null)
        {
            odDaty ??= DateTime.Today.AddYears(-1);
            var hodowcy = new List<HodowcaLokalizacja>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        SELECT
                            c.GID as HodowcaId,
                            c.Name as Nazwa,
                            c.City as Miasto,
                            c.Address as Adres,
                            c.PostCode as KodPocztowy,
                            c.Latitude,
                            c.Longitude,
                            COUNT(fc.ID) as LiczbaDostaw,
                            SUM(fc.NettoWeight) as WagaSuma,
                            SUM((fc.Price + ISNULL(fc.Addition, 0)) * fc.NettoWeight) as WartoscSuma,
                            MAX(fc.DataPrzyjecia) as OstatniaDostwa
                        FROM [LibraNet].[dbo].[Customer] c
                        LEFT JOIN [LibraNet].[dbo].[FarmerCalc] fc
                            ON fc.CustomerGID = c.GID AND fc.DataPrzyjecia >= @od
                        WHERE c.CustomerType = 'Hodowca'
                           OR EXISTS (SELECT 1 FROM [LibraNet].[dbo].[FarmerCalc] WHERE CustomerGID = c.GID)
                        GROUP BY c.GID, c.Name, c.City, c.Address, c.PostCode, c.Latitude, c.Longitude
                        ORDER BY SUM(fc.NettoWeight) DESC", conn);

                    cmd.Parameters.AddWithValue("@od", odDaty.Value);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var h = new HodowcaLokalizacja
                            {
                                HodowcaId = Convert.ToInt32(reader["HodowcaId"]),
                                Nazwa = reader["Nazwa"]?.ToString(),
                                Miasto = reader["Miasto"]?.ToString(),
                                Adres = reader["Adres"]?.ToString(),
                                KodPocztowy = reader["KodPocztowy"]?.ToString(),
                                Latitude = reader["Latitude"] as double?,
                                Longitude = reader["Longitude"] as double?,
                                LiczbaDostaw = Convert.ToInt32(reader["LiczbaDostaw"]),
                                WagaSuma = reader["WagaSuma"] as decimal? ?? 0,
                                WartoscSuma = reader["WartoscSuma"] as decimal? ?? 0,
                                OstatniaDostwa = reader["OstatniaDostwa"] as DateTime?
                            };

                            // Oblicz odległość od ubojni (jeśli są współrzędne)
                            if (h.Latitude.HasValue && h.Longitude.HasValue)
                            {
                                h.OdlegloscKm = ObliczOdleglosc(
                                    UBOJNIA_LAT, UBOJNIA_LON,
                                    h.Latitude.Value, h.Longitude.Value);
                            }

                            // Określ kategorię (rozmiar markera na mapie)
                            h.Kategoria = OkreslKategorie(h.WagaSuma);

                            hodowcy.Add(h);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania hodowców: {ex.Message}");
            }

            return hodowcy;
        }

        /// <summary>
        /// Geokoduje adres hodowcy (pobiera współrzędne)
        /// Używa darmowego API Nominatim (OpenStreetMap)
        /// </summary>
        public async Task<GeocodingResult> GeocodeAddressAsync(string address, string city, string postCode)
        {
            try
            {
                string fullAddress = $"{address}, {postCode} {city}, Polska";
                string encodedAddress = Uri.EscapeDataString(fullAddress);

                // Nominatim API (OpenStreetMap) - darmowe, bez klucza
                string url = $"https://nominatim.openstreetmap.org/search?" +
                            $"q={encodedAddress}&format=json&limit=1&countrycodes=pl";

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Kalendarz1-PoultryApp/1.0");

                var response = await _httpClient.GetStringAsync(url);
                var results = JsonConvert.DeserializeObject<List<NominatimResult>>(response);

                if (results != null && results.Count > 0)
                {
                    return new GeocodingResult
                    {
                        Success = true,
                        Latitude = double.Parse(results[0].lat, CultureInfo.InvariantCulture),
                        Longitude = double.Parse(results[0].lon, CultureInfo.InvariantCulture),
                        DisplayName = results[0].display_name
                    };
                }

                return new GeocodingResult
                {
                    Success = false,
                    Message = "Nie znaleziono lokalizacji"
                };
            }
            catch (Exception ex)
            {
                return new GeocodingResult
                {
                    Success = false,
                    Message = $"Błąd geokodowania: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Aktualizuje współrzędne hodowcy w bazie danych
        /// </summary>
        public async Task<bool> UpdateHodowcaCoordinatesAsync(int hodowcaId, string address, string city, string postCode)
        {
            var result = await GeocodeAddressAsync(address, city, postCode);

            if (!result.Success)
                return false;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        UPDATE [LibraNet].[dbo].[Customer]
                        SET Latitude = @lat, Longitude = @lon
                        WHERE GID = @gid", conn);

                    cmd.Parameters.AddWithValue("@gid", hodowcaId);
                    cmd.Parameters.AddWithValue("@lat", result.Latitude);
                    cmd.Parameters.AddWithValue("@lon", result.Longitude);

                    cmd.ExecuteNonQuery();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Generuje plik HTML z interaktywną mapą (Leaflet.js)
        /// </summary>
        public string GenerateMapHtml(List<HodowcaLokalizacja> hodowcy, string title = "Mapa Hodowców")
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("<meta charset='utf-8'>");
            sb.AppendLine($"<title>{title}</title>");
            sb.AppendLine("<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>");
            sb.AppendLine("<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>");
            sb.AppendLine("<style>");
            sb.AppendLine("  body { margin: 0; padding: 0; }");
            sb.AppendLine("  #map { width: 100%; height: 100vh; }");
            sb.AppendLine("  .legend { padding: 10px; background: white; border-radius: 5px; }");
            sb.AppendLine("  .legend-item { margin: 5px 0; }");
            sb.AppendLine("  .legend-color { display: inline-block; width: 20px; height: 20px; margin-right: 5px; border-radius: 50%; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine("<div id='map'></div>");
            sb.AppendLine("<script>");

            // Inicjalizacja mapy
            sb.AppendLine($"var map = L.map('map').setView([{UBOJNIA_LAT.ToString(CultureInfo.InvariantCulture)}, {UBOJNIA_LON.ToString(CultureInfo.InvariantCulture)}], 9);");
            sb.AppendLine("L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {");
            sb.AppendLine("  attribution: '© OpenStreetMap contributors'");
            sb.AppendLine("}).addTo(map);");

            // Marker ubojni
            sb.AppendLine($"L.marker([{UBOJNIA_LAT.ToString(CultureInfo.InvariantCulture)}, {UBOJNIA_LON.ToString(CultureInfo.InvariantCulture)}], {{");
            sb.AppendLine("  icon: L.divIcon({ className: 'ubojnia-marker', html: '🏭', iconSize: [30, 30] })");
            sb.AppendLine("}).addTo(map).bindPopup('<b>Ubojnia Piórkowscy</b><br>Koziołki 40, Dmosin');");

            // Markery hodowców
            foreach (var h in hodowcy)
            {
                if (!h.Latitude.HasValue || !h.Longitude.HasValue)
                    continue;

                string color = h.Kategoria switch
                {
                    "A" => "#22c55e", // Zielony - duży
                    "B" => "#3b82f6", // Niebieski - średni
                    "C" => "#f59e0b", // Pomarańczowy - mały
                    _ => "#6b7280"    // Szary
                };

                int size = h.Kategoria switch
                {
                    "A" => 20,
                    "B" => 15,
                    "C" => 10,
                    _ => 8
                };

                string popup = $"<b>{EscapeJs(h.Nazwa)}</b><br>" +
                              $"{EscapeJs(h.Miasto)}<br>" +
                              $"Dostaw: {h.LiczbaDostaw}<br>" +
                              $"Waga: {h.WagaSuma:N0} kg<br>" +
                              $"Wartość: {h.WartoscSuma:N0} zł<br>" +
                              $"Odległość: {h.OdlegloscKm:F1} km";

                sb.AppendLine($"L.circleMarker([{h.Latitude.Value.ToString(CultureInfo.InvariantCulture)}, {h.Longitude.Value.ToString(CultureInfo.InvariantCulture)}], {{");
                sb.AppendLine($"  radius: {size},");
                sb.AppendLine($"  fillColor: '{color}',");
                sb.AppendLine($"  color: '#000',");
                sb.AppendLine($"  weight: 1,");
                sb.AppendLine($"  opacity: 1,");
                sb.AppendLine($"  fillOpacity: 0.8");
                sb.AppendLine($"}}).addTo(map).bindPopup('{popup}');");
            }

            // Legenda
            sb.AppendLine("var legend = L.control({position: 'bottomright'});");
            sb.AppendLine("legend.onAdd = function(map) {");
            sb.AppendLine("  var div = L.DomUtil.create('div', 'legend');");
            sb.AppendLine("  div.innerHTML = '<b>Kategorie hodowców</b><br>' +");
            sb.AppendLine("    '<div class=\"legend-item\"><span class=\"legend-color\" style=\"background:#22c55e\"></span>Kat. A (>50t)</div>' +");
            sb.AppendLine("    '<div class=\"legend-item\"><span class=\"legend-color\" style=\"background:#3b82f6\"></span>Kat. B (10-50t)</div>' +");
            sb.AppendLine("    '<div class=\"legend-item\"><span class=\"legend-color\" style=\"background:#f59e0b\"></span>Kat. C (<10t)</div>';");
            sb.AppendLine("  return div;");
            sb.AppendLine("};");
            sb.AppendLine("legend.addTo(map);");

            sb.AppendLine("</script>");
            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        /// <summary>
        /// Zapisuje mapę do pliku HTML
        /// </summary>
        public string SaveMapToFile(List<HodowcaLokalizacja> hodowcy, string fileName = null)
        {
            fileName ??= $"Mapa_Hodowcow_{DateTime.Now:yyyyMMdd_HHmmss}.html";

            string folderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Kalendarz1_Mapy");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string filePath = Path.Combine(folderPath, fileName);
            string html = GenerateMapHtml(hodowcy);

            File.WriteAllText(filePath, html, Encoding.UTF8);

            return filePath;
        }

        /// <summary>
        /// Pobiera statystyki regionalne
        /// </summary>
        public List<RegionStats> GetStatystykiRegionalne(DateTime? odDaty = null)
        {
            odDaty ??= DateTime.Today.AddYears(-1);
            var regiony = new List<RegionStats>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        SELECT
                            ISNULL(c.City, 'Nieznane') as Region,
                            COUNT(DISTINCT c.GID) as LiczbaHodowcow,
                            COUNT(fc.ID) as LiczbaDostaw,
                            SUM(fc.NettoWeight) as WagaSuma,
                            SUM((fc.Price + ISNULL(fc.Addition, 0)) * fc.NettoWeight) as WartoscSuma,
                            AVG(fc.Price + ISNULL(fc.Addition, 0)) as SredniaCena
                        FROM [LibraNet].[dbo].[Customer] c
                        JOIN [LibraNet].[dbo].[FarmerCalc] fc ON fc.CustomerGID = c.GID
                        WHERE fc.DataPrzyjecia >= @od
                        GROUP BY c.City
                        HAVING SUM(fc.NettoWeight) > 0
                        ORDER BY SUM(fc.NettoWeight) DESC", conn);

                    cmd.Parameters.AddWithValue("@od", odDaty.Value);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            regiony.Add(new RegionStats
                            {
                                Region = reader["Region"]?.ToString(),
                                LiczbaHodowcow = Convert.ToInt32(reader["LiczbaHodowcow"]),
                                LiczbaDostaw = Convert.ToInt32(reader["LiczbaDostaw"]),
                                WagaSuma = Convert.ToDecimal(reader["WagaSuma"]),
                                WartoscSuma = Convert.ToDecimal(reader["WartoscSuma"]),
                                SredniaCena = Convert.ToDecimal(reader["SredniaCena"])
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd statystyk regionalnych: {ex.Message}");
            }

            return regiony;
        }

        #region Helper Methods

        private double ObliczOdleglosc(double lat1, double lon1, double lat2, double lon2)
        {
            // Formuła Haversine
            const double R = 6371; // Promień Ziemi w km

            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private double ToRad(double deg) => deg * Math.PI / 180;

        private string OkreslKategorie(decimal waga)
        {
            if (waga >= 50000) return "A"; // >= 50 ton
            if (waga >= 10000) return "B"; // 10-50 ton
            if (waga > 0) return "C";      // < 10 ton
            return "D";                     // Brak dostaw
        }

        private string EscapeJs(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("'", "\\'").Replace("\n", " ").Replace("\r", "");
        }

        #endregion
    }

    #region Data Models

    public class HodowcaLokalizacja
    {
        public int HodowcaId { get; set; }
        public string Nazwa { get; set; }
        public string Miasto { get; set; }
        public string Adres { get; set; }
        public string KodPocztowy { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int LiczbaDostaw { get; set; }
        public decimal WagaSuma { get; set; }
        public decimal WartoscSuma { get; set; }
        public DateTime? OstatniaDostwa { get; set; }
        public double OdlegloscKm { get; set; }
        public string Kategoria { get; set; } // A, B, C, D
    }

    public class GeocodingResult
    {
        public bool Success { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string DisplayName { get; set; }
        public string Message { get; set; }
    }

    public class NominatimResult
    {
        public string lat { get; set; }
        public string lon { get; set; }
        public string display_name { get; set; }
    }

    public class RegionStats
    {
        public string Region { get; set; }
        public int LiczbaHodowcow { get; set; }
        public int LiczbaDostaw { get; set; }
        public decimal WagaSuma { get; set; }
        public decimal WartoscSuma { get; set; }
        public decimal SredniaCena { get; set; }
    }

    #endregion
}
