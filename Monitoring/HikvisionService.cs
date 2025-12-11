using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Kalendarz1.Monitoring
{
    /// <summary>
    /// Serwis do komunikacji z urządzeniami Hikvision przez ISAPI
    /// </summary>
    public class HikvisionService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public HikvisionService(string ipAddress, string username, string password)
        {
            _baseUrl = $"http://{ipAddress}";

            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(username, password),
                PreAuthenticate = true
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        /// <summary>
        /// Pobiera informacje o urządzeniu NVR/DVR
        /// </summary>
        public async Task<DeviceInfo> GetDeviceInfoAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/ISAPI/System/deviceInfo");
                response.EnsureSuccessStatusCode();

                var xml = await response.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(xml);
                XNamespace ns = "http://www.std-cgi.com/ver20/XMLSchema";

                var root = doc.Root;
                return new DeviceInfo
                {
                    DeviceName = root?.Element(ns + "deviceName")?.Value ?? "Nieznane",
                    DeviceID = root?.Element(ns + "deviceID")?.Value ?? "",
                    Model = root?.Element(ns + "model")?.Value ?? "",
                    SerialNumber = root?.Element(ns + "serialNumber")?.Value ?? "",
                    MacAddress = root?.Element(ns + "macAddress")?.Value ?? "",
                    FirmwareVersion = root?.Element(ns + "firmwareVersion")?.Value ?? "",
                    DeviceType = root?.Element(ns + "deviceType")?.Value ?? ""
                };
            }
            catch (Exception ex)
            {
                throw new HikvisionException($"Błąd pobierania informacji o urządzeniu: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Pobiera listę kanałów (kamer) z NVR
        /// </summary>
        public async Task<List<CameraChannel>> GetChannelsAsync()
        {
            var channels = new List<CameraChannel>();

            try
            {
                // Próba pobrania listy kanałów dla NVR
                var response = await _httpClient.GetAsync($"{_baseUrl}/ISAPI/ContentMgmt/InputProxy/channels");

                if (!response.IsSuccessStatusCode)
                {
                    // Alternatywny endpoint dla starszych urządzeń
                    response = await _httpClient.GetAsync($"{_baseUrl}/ISAPI/System/Video/inputs/channels");
                }

                if (response.IsSuccessStatusCode)
                {
                    var xml = await response.Content.ReadAsStringAsync();
                    var doc = XDocument.Parse(xml);

                    // Parsowanie różnych formatów odpowiedzi
                    foreach (var channel in doc.Descendants())
                    {
                        if (channel.Name.LocalName == "InputProxyChannel" ||
                            channel.Name.LocalName == "VideoInputChannel")
                        {
                            var ns = channel.Name.Namespace;
                            channels.Add(new CameraChannel
                            {
                                Id = channel.Element(ns + "id")?.Value ?? "",
                                Name = channel.Element(ns + "name")?.Value ??
                                       channel.Element(ns + "inputPort")?.Value ?? $"Kamera {channels.Count + 1}",
                                Status = channel.Element(ns + "online")?.Value == "true" ? "Online" :
                                         channel.Element(ns + "resDesc")?.Value ?? "Nieznany"
                            });
                        }
                    }
                }

                // Jeśli nie znaleziono kanałów, dodaj domyślne
                if (channels.Count == 0)
                {
                    for (int i = 1; i <= 8; i++)
                    {
                        channels.Add(new CameraChannel
                        {
                            Id = i.ToString(),
                            Name = $"Kamera {i}",
                            Status = "Sprawdź połączenie"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                throw new HikvisionException($"Błąd pobierania listy kanałów: {ex.Message}", ex);
            }

            return channels;
        }

        /// <summary>
        /// Pobiera zrzut ekranu z kamery (snapshot)
        /// </summary>
        public async Task<byte[]> GetSnapshotAsync(string channelId)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"{_baseUrl}/ISAPI/Streaming/channels/{channelId}01/picture");

                if (!response.IsSuccessStatusCode)
                {
                    // Alternatywny endpoint
                    response = await _httpClient.GetAsync(
                        $"{_baseUrl}/ISAPI/Streaming/channels/{channelId}/picture");
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                throw new HikvisionException($"Błąd pobierania zrzutu ekranu: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Pobiera URL streamu RTSP dla kamery
        /// </summary>
        public string GetRtspUrl(string channelId, string username, string password, string ipAddress)
        {
            // Format: rtsp://username:password@ip:554/Streaming/Channels/101
            // Gdzie 101 = kanał 1, główny strumień; 102 = kanał 1, substream
            return $"rtsp://{username}:{password}@{ipAddress}:554/Streaming/Channels/{channelId}01";
        }

        /// <summary>
        /// Testuje połączenie z urządzeniem
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/ISAPI/System/deviceInfo");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
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
    }

    public class HikvisionException : Exception
    {
        public HikvisionException(string message) : base(message) { }
        public HikvisionException(string message, Exception inner) : base(message, inner) { }
    }
}
