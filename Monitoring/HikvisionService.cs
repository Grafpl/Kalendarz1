using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Kalendarz1.Monitoring
{
    /// <summary>
    /// Serwis do komunikacji z NVR INTERNEC
    /// Próbuje najpierw przez NVR, potem bezpośrednio z kamer
    /// </summary>
    public class HikvisionService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _nvrIp;
        private readonly string _username;
        private readonly string _password;
        public string LastError { get; private set; }

        // Mapowanie kanałów NVR na IP kamer (fallback)
        private static readonly Dictionary<int, string> CameraIpMap = new Dictionary<int, string>
        {
            {1, "192.168.0.247"},   {2, "192.168.0.115"},   {3, "192.168.0.116"},   {4, "192.168.0.117"},
            {5, "192.168.0.118"},   {6, "192.168.0.119"},   {7, "192.168.0.76"},    {8, "192.168.0.121"},
            {9, "192.168.0.122"},   {10, "192.168.0.135"},  {11, "192.168.0.113"},  {12, "192.168.0.235"},
            {13, "192.168.0.124"},  {14, "192.168.0.204"},  {15, "192.168.0.145"},  {16, "192.168.0.209"},
            {17, "192.168.0.231"},  {18, "192.168.0.211"},  {19, "192.168.0.243"},  {20, "192.168.0.137"},
            {21, "192.168.0.249"},  {22, "192.168.0.143"},  {23, "192.168.0.67"},   {24, "192.168.0.136"},
            {25, "192.168.0.78"},   {26, "192.168.0.237"},  {27, "192.168.0.138"},  {28, "192.168.0.120"},
            {29, "192.168.0.123"},  {30, "192.168.0.214"},  {31, "192.168.0.205"},  {32, "192.168.0.206"}
        };

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

            var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
        }

        public async Task<DeviceInfo> GetDeviceInfoAsync()
        {
            return await Task.FromResult(new DeviceInfo
            {
                DeviceName = "NVR_Produkcja",
                Model = "INTERNEC i6-N25232UHV",
                SerialNumber = "210235TLFF3214000029",
                FirmwareVersion = "NVR-B3601.37.35",
                DeviceType = "NVR"
            });
        }

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
                    Status = "Aktywny",
                    IpAddress = ip
                });
            }
            return Task.FromResult(channels);
        }

        /// <summary>
        /// Pobiera snapshot - najpierw przez NVR, potem bezpośrednio z kamery
        /// </summary>
        public async Task<byte[]> GetSnapshotAsync(string channelId)
        {
            if (!int.TryParse(channelId, out int channel))
            {
                throw new HikvisionException($"Nieprawidłowy kanał: {channelId}");
            }

            // ═══════════════════════════════════════════════════════════════
            // PRÓBA 1: Przez NVR (192.168.0.125)
            // ═══════════════════════════════════════════════════════════════
            var nvrEndpoints = new[]
            {
                // INTERNEC / Dahua style CGI
                $"http://{_nvrIp}/cgi-bin/snapshot.cgi?channel={channel}",
                $"http://{_nvrIp}/cgi-bin/snapshot.cgi?chn={channel}",
                $"http://{_nvrIp}/cgi-bin/snapshot.cgi?channel={channel}&subtype=0",
                $"http://{_nvrIp}/cgi-bin/snapshot.cgi?chn={channel}&subtype=0",

                // Z parametrami auth w URL
                $"http://{_nvrIp}/cgi-bin/snapshot.cgi?channel={channel}&loginuse={_username}&loginpas={_password}",
                $"http://{_nvrIp}/cgi-bin/snapshot.cgi?chn={channel}&u={_username}&p={_password}",

                // Dahua RPC style
                $"http://{_nvrIp}/cgi-bin/snapshot.cgi?channel={channel - 1}",  // 0-indexed

                // Inne CGI
                $"http://{_nvrIp}/snapshot.cgi?channel={channel}",
                $"http://{_nvrIp}/snap.cgi?chn={channel}",
                $"http://{_nvrIp}/webcapture.jpg?channel={channel}",
                $"http://{_nvrIp}/images/snapshot.jpg?channel={channel}",
                $"http://{_nvrIp}/tmpfs/snap_{channel}.jpg",
                $"http://{_nvrIp}/tmpfs/auto{channel}.jpg",

                // Hikvision ISAPI (na wszelki wypadek)
                $"http://{_nvrIp}/ISAPI/Streaming/channels/{channel}01/picture",
                $"http://{_nvrIp}/Streaming/channels/{channel}01/picture",

                // Picture CGI
                $"http://{_nvrIp}/cgi-bin/images_cgi?channel={channel}",
                $"http://{_nvrIp}/cgi-bin/images.cgi?channel={channel}",
                $"http://{_nvrIp}/picture/{channel}/current.jpg",
                $"http://{_nvrIp}/onvif/snapshot?channel={channel}",

                // XVR/DVR style
                $"http://{_nvrIp}/cgi-bin/video.cgi?mession=getimage&channel={channel}",
                $"http://{_nvrIp}/video.cgi?channel={channel}&format=jpeg",
            };

            foreach (var url in nvrEndpoints)
            {
                var result = await TryGetSnapshotFromUrl(url);
                if (result != null)
                {
                    return result;
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // PRÓBA 2: Bezpośrednio z kamery (fallback)
            // ═══════════════════════════════════════════════════════════════
            if (CameraIpMap.ContainsKey(channel))
            {
                var cameraIp = CameraIpMap[channel];
                var cameraEndpoints = new[]
                {
                    $"http://{cameraIp}/ISAPI/Streaming/channels/101/picture",
                    $"http://{cameraIp}/ISAPI/Streaming/channels/1/picture",
                    $"http://{cameraIp}/cgi-bin/snapshot.cgi",
                    $"http://{cameraIp}/snap.jpg",
                    $"http://{cameraIp}/snapshot.jpg",
                    $"http://{cameraIp}/onvif-http/snapshot",
                    $"http://{cameraIp}/jpg/image.jpg",
                    $"http://{cameraIp}/tmpfs/auto.jpg",
                };

                foreach (var url in cameraEndpoints)
                {
                    var result = await TryGetSnapshotFromUrl(url);
                    if (result != null)
                    {
                        return result;
                    }
                }

                LastError = $"NVR i kamera {cameraIp} nie odpowiadają";
            }
            else
            {
                LastError = $"Nie znaleziono endpointu dla kanału {channel}";
            }

            throw new HikvisionException(LastError);
        }

        private async Task<byte[]> TryGetSnapshotFromUrl(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();

                    // Sprawdź czy to JPEG (nagłówek FF D8 FF)
                    if (bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                    {
                        return bytes;
                    }

                    // Sprawdź PNG (89 50 4E 47)
                    if (bytes.Length > 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                    {
                        return bytes;
                    }
                }
            }
            catch
            {
                // Ignoruj błędy, próbuj następny endpoint
            }
            return null;
        }

        public string GetRtspUrl(string channelId, bool mainStream = true)
        {
            var streamType = mainStream ? "01" : "02";
            return $"rtsp://{_username}:{_password}@{_nvrIp}:554/Streaming/Channels/{channelId}{streamType}";
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"http://{_nvrIp}/");
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return (true, "Połączono z NVR");
                }
            }
            catch { }
            return (false, "Brak połączenia z NVR");
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

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
