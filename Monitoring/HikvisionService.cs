using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Kalendarz1.Monitoring
{
    /// <summary>
    /// Serwis do komunikacji z kamerami INTERNEC/Hikvision
    /// Pobiera snapshoty bezpośrednio z kamer (każda ma własne IP)
    /// </summary>
    public class HikvisionService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _nvrIp;
        private readonly string _username;
        private readonly string _password;
        public string LastError { get; private set; }

        // Mapowanie kanałów NVR na IP kamer (z konfiguracji NVR)
        private static readonly Dictionary<int, string> CameraIpMap = new Dictionary<int, string>
        {
            {1, "192.168.0.247"},   // D1 - IPCamera 05
            {2, "192.168.0.115"},   // D2 - CHLOD_Prawa
            {3, "192.168.0.116"},   // D3 - PROD_Kurczak
            {4, "192.168.0.117"},   // D4 - PROD_Filetowanie
            {5, "192.168.0.118"},   // D5 - CHLOD_Srodek
            {6, "192.168.0.119"},   // D6 - PROD_Waga
            {7, "192.168.0.76"},    // D7 - ZEW_WagaSam
            {8, "192.168.0.121"},   // D8 - ZEW_Waga_Auta
            {9, "192.168.0.122"},   // D9 - PROD_Akwarium
            {10, "192.168.0.135"},  // D10 - POJ_Rampa
            {11, "192.168.0.113"},  // D11 - POJ_Kanciapa
            {12, "192.168.0.235"},  // D12 - PROD_Ruchliwa
            {13, "192.168.0.124"},  // D13 - KORYTARZ_PR
            {14, "192.168.0.204"},  // D14 - DS-2CD2T43G2
            {15, "192.168.0.145"},  // D15 - IPC
            {16, "192.168.0.209"},  // D16 - DS-2CD2T43G2
            {17, "192.168.0.231"},  // D17 - Sklep (RTSP)
            {18, "192.168.0.211"},  // D18 - Zew_RampaWy
            {19, "192.168.0.243"},  // D19 - Zew_MyjkaSam
            {20, "192.168.0.137"},  // D20 - DYSTRYBUTOR
            {21, "192.168.0.249"},  // D21 - Zew_Tyl
            {22, "192.168.0.143"},  // D22 - Camera 01
            {23, "192.168.0.67"},   // D23 - Camera 01
            {24, "192.168.0.136"},  // D24 - MAS_Produkcja
            {25, "192.168.0.78"},   // D25 - MROZ_Wybijan
            {26, "192.168.0.237"},  // D26 - Masarnia (RTSP)
            {27, "192.168.0.138"},  // D27 - Dyst_Srodek
            {28, "192.168.0.120"},  // D28 - PROD_Wej_Ch
            {29, "192.168.0.123"},  // D29 - PROD_Podroby
            {30, "192.168.0.214"},  // D30 - PROD_Waga
            {31, "192.168.0.205"},  // D31 - Camera 01
            {32, "192.168.0.206"}   // D32 - Camera 01
        };

        // Nazwy kamer
        private static readonly Dictionary<int, string> CameraNames = new Dictionary<int, string>
        {
            {1, "IPCamera 05"}, {2, "CHLOD_Prawa"}, {3, "PROD_Kurczak"}, {4, "PROD_Filetowanie"},
            {5, "CHLOD_Srodek"}, {6, "PROD_Waga"}, {7, "ZEW_WagaSam"}, {8, "ZEW_Waga_Auta"},
            {9, "PROD_Akwarium"}, {10, "POJ_Rampa"}, {11, "POJ_Kanciapa"}, {12, "PROD_Ruchliwa"},
            {13, "KORYTARZ_PR"}, {14, "DS-2CD2T43G2"}, {15, "IPC"}, {16, "DS-2CD2T43G2"},
            {17, "Sklep"}, {18, "Zew_RampaWy"}, {19, "Zew_MyjkaSam"}, {20, "DYSTRYBUTOR"},
            {21, "Zew_Tyl"}, {22, "Camera 01"}, {23, "Camera 01"}, {24, "MAS_Produkcja"},
            {25, "MROZ_Wybijan"}, {26, "Masarnia"}, {27, "Dyst_Srodek"}, {28, "PROD_Wej_Ch"},
            {29, "PROD_Podroby"}, {30, "PROD_Waga"}, {31, "Camera 01"}, {32, "Camera 01"}
        };

        public HikvisionService(string nvrIp, string username, string password)
        {
            _nvrIp = nvrIp;
            _username = username;
            _password = password;

            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(username, password),
                PreAuthenticate = true
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            // Basic Auth header
            var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
        }

        /// <summary>
        /// Pobiera informacje o NVR
        /// </summary>
        public async Task<DeviceInfo> GetDeviceInfoAsync()
        {
            // Zwróć statyczne info o NVR (z konfiguracji)
            return await Task.FromResult(new DeviceInfo
            {
                DeviceName = "NVR_Produkcja",
                Model = "INTERNEC i6-N25232UHV",
                SerialNumber = "210235TLFF3214000029",
                FirmwareVersion = "NVR-B3601.37.35",
                DeviceType = "NVR",
                MacAddress = ""
            });
        }

        /// <summary>
        /// Pobiera listę kanałów (kamer)
        /// </summary>
        public Task<List<CameraChannel>> GetChannelsAsync()
        {
            var channels = new List<CameraChannel>();

            for (int i = 1; i <= 32; i++)
            {
                var name = CameraNames.ContainsKey(i) ? CameraNames[i] : $"Kamera {i}";
                var ip = CameraIpMap.ContainsKey(i) ? CameraIpMap[i] : "";

                channels.Add(new CameraChannel
                {
                    Id = i.ToString(),
                    Name = $"D{i} ({name})",
                    Status = string.IsNullOrEmpty(ip) ? "Brak IP" : "Aktywny",
                    IpAddress = ip
                });
            }

            return Task.FromResult(channels);
        }

        /// <summary>
        /// Pobiera snapshot bezpośrednio z kamery
        /// </summary>
        public async Task<byte[]> GetSnapshotAsync(string channelId)
        {
            if (!int.TryParse(channelId, out int channel) || !CameraIpMap.ContainsKey(channel))
            {
                throw new HikvisionException($"Nieznany kanał: {channelId}");
            }

            var cameraIp = CameraIpMap[channel];

            // Endpointy do snapshotów (różne dla różnych modeli kamer)
            var endpoints = new[]
            {
                // Hikvision ISAPI (najczęściej używane)
                $"http://{cameraIp}/ISAPI/Streaming/channels/101/picture",
                $"http://{cameraIp}/ISAPI/Streaming/channels/1/picture",

                // ONVIF / Generic
                $"http://{cameraIp}/onvif-http/snapshot?channel=1",
                $"http://{cameraIp}/snap.jpg",
                $"http://{cameraIp}/snapshot.jpg",
                $"http://{cameraIp}/cgi-bin/snapshot.cgi",
                $"http://{cameraIp}/jpg/image.jpg",

                // Dahua style
                $"http://{cameraIp}/cgi-bin/snapshot.cgi?channel=1",

                // INTERNEC
                $"http://{cameraIp}/webcapture.jpg?channel=0",
                $"http://{cameraIp}/tmpfs/auto.jpg",
            };

            foreach (var url in endpoints)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var bytes = await response.Content.ReadAsByteArrayAsync();

                        // Sprawdź czy to JPEG (FF D8 FF)
                        if (bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8)
                        {
                            return bytes;
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // Timeout - kamera nie odpowiada
                    continue;
                }
                catch (HttpRequestException)
                {
                    // Błąd sieci
                    continue;
                }
            }

            // Jeśli żaden endpoint nie zadziałał
            LastError = $"Kamera {cameraIp} nie odpowiada lub brak endpointu snapshot";
            throw new HikvisionException(LastError);
        }

        /// <summary>
        /// Generuje URL RTSP (przez NVR)
        /// </summary>
        public string GetRtspUrl(string channelId, bool mainStream = true)
        {
            var streamType = mainStream ? "01" : "02";
            return $"rtsp://{_username}:{_password}@{_nvrIp}:554/Streaming/Channels/{channelId}{streamType}";
        }

        /// <summary>
        /// Generuje URL RTSP bezpośrednio do kamery
        /// </summary>
        public string GetDirectRtspUrl(string channelId, bool mainStream = true)
        {
            if (int.TryParse(channelId, out int channel) && CameraIpMap.ContainsKey(channel))
            {
                var cameraIp = CameraIpMap[channel];
                var streamType = mainStream ? "01" : "02";
                return $"rtsp://{_username}:{_password}@{cameraIp}:554/Streaming/Channels/1{streamType}";
            }
            return GetRtspUrl(channelId, mainStream);
        }

        /// <summary>
        /// Testuje połączenie
        /// </summary>
        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            // Testuj połączenie z kilkoma kamerami
            int successCount = 0;

            foreach (var kvp in CameraIpMap)
            {
                if (kvp.Key > 3) break; // Testuj tylko pierwsze 3

                try
                {
                    var response = await _httpClient.GetAsync($"http://{kvp.Value}/");
                    if (response.StatusCode != HttpStatusCode.NotFound)
                    {
                        successCount++;
                    }
                }
                catch { }
            }

            if (successCount > 0)
            {
                return (true, $"Połączono ({successCount}/3 kamer odpowiada)");
            }

            return (false, "Brak połączenia z kamerami");
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // ============ MODELE ============

    public class DeviceInfo
    {
        public string DeviceName { get; set; }
        public string DeviceID { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string MacAddress { get; set; }
        public string FirmwareVersion { get; set; }
        public string DeviceType { get; set; }
    }

    public class CameraChannel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public string IpAddress { get; set; }
    }

    public class HikvisionException : Exception
    {
        public HikvisionException(string message) : base(message) { }
        public HikvisionException(string message, Exception inner) : base(message, inner) { }
    }
}
