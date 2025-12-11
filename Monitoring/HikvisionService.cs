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
    /// Serwis do komunikacji z NVR Hikvision/INTERNEC przez ISAPI
    /// Architektura: Program → NVR → Kamery (NVR agreguje wszystkie kamery)
    /// </summary>
    public class HikvisionService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _username;
        private readonly string _password;
        public string LastError { get; private set; }

        public HikvisionService(string ipAddress, string username, string password)
        {
            _baseUrl = $"http://{ipAddress}";
            _username = username;
            _password = password;

            // Konfiguracja HttpClient z Basic Auth (INTERNEC/Hikvision obsługuje oba)
            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(username, password),
                PreAuthenticate = true
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            // Dodaj Basic Auth header jako fallback
            var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
        }

        /// <summary>
        /// Pobiera informacje o urządzeniu NVR
        /// </summary>
        public async Task<DeviceInfo> GetDeviceInfoAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/ISAPI/System/deviceInfo");

                if (!response.IsSuccessStatusCode)
                {
                    LastError = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                    throw new HikvisionException(LastError);
                }

                var xml = await response.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(xml);

                // Znajdź namespace (może być różny dla różnych urządzeń)
                var root = doc.Root;
                var ns = root?.Name.Namespace ?? "";

                return new DeviceInfo
                {
                    DeviceName = GetElementValue(root, "deviceName", ns) ?? "NVR",
                    DeviceID = GetElementValue(root, "deviceID", ns) ?? "",
                    Model = GetElementValue(root, "model", ns) ?? "",
                    SerialNumber = GetElementValue(root, "serialNumber", ns) ?? "",
                    MacAddress = GetElementValue(root, "macAddress", ns) ?? "",
                    FirmwareVersion = GetElementValue(root, "firmwareVersion", ns) ?? "",
                    DeviceType = GetElementValue(root, "deviceType", ns) ?? "NVR"
                };
            }
            catch (HttpRequestException ex)
            {
                LastError = $"Błąd sieci: {ex.Message}";
                throw new HikvisionException(LastError, ex);
            }
            catch (TaskCanceledException)
            {
                LastError = "Timeout - NVR nie odpowiada";
                throw new HikvisionException(LastError);
            }
            catch (Exception ex) when (!(ex is HikvisionException))
            {
                LastError = $"Błąd: {ex.Message}";
                throw new HikvisionException(LastError, ex);
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
                // Endpoint dla NVR - lista kanałów wejściowych (proxy do kamer IP)
                var endpoints = new[]
                {
                    "/ISAPI/ContentMgmt/InputProxy/channels",
                    "/ISAPI/System/Video/inputs/channels",
                    "/ISAPI/Streaming/channels"
                };

                string xml = null;
                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync($"{_baseUrl}{endpoint}");
                        if (response.IsSuccessStatusCode)
                        {
                            xml = await response.Content.ReadAsStringAsync();
                            break;
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(xml))
                {
                    var doc = XDocument.Parse(xml);

                    // Parsowanie InputProxyChannelList (dla NVR)
                    foreach (var element in doc.Descendants())
                    {
                        if (element.Name.LocalName == "InputProxyChannel" ||
                            element.Name.LocalName == "VideoInputChannel" ||
                            element.Name.LocalName == "StreamingChannel")
                        {
                            var ns = element.Name.Namespace;
                            var id = GetElementValue(element, "id", ns);
                            var name = GetElementValue(element, "name", ns)
                                    ?? GetElementValue(element, "channelName", ns)
                                    ?? $"Kanał {id}";

                            var online = GetElementValue(element, "online", ns);
                            var status = online?.ToLower() == "true" || online == "1" ? "Online" :
                                        online?.ToLower() == "false" || online == "0" ? "Offline" : "Aktywny";

                            if (!string.IsNullOrEmpty(id))
                            {
                                channels.Add(new CameraChannel
                                {
                                    Id = id,
                                    Name = name,
                                    Status = status
                                });
                            }
                        }
                    }
                }

                // Jeśli nie udało się pobrać kanałów, utwórz domyślną listę (32 kanały dla NVR)
                if (channels.Count == 0)
                {
                    for (int i = 1; i <= 32; i++)
                    {
                        channels.Add(new CameraChannel
                        {
                            Id = i.ToString(),
                            Name = $"Kamera D{i}",
                            Status = "Sprawdź"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LastError = $"Błąd pobierania kanałów: {ex.Message}";
                // Zwróć domyślną listę zamiast rzucać wyjątek
                for (int i = 1; i <= 32; i++)
                {
                    channels.Add(new CameraChannel
                    {
                        Id = i.ToString(),
                        Name = $"Kamera D{i}",
                        Status = "Brak danych"
                    });
                }
            }

            return channels;
        }

        /// <summary>
        /// Pobiera zrzut ekranu (snapshot) z kamery przez NVR
        /// Format kanału: 101 = kanał 1 główny strumień, 102 = kanał 1 substream
        /// </summary>
        public async Task<byte[]> GetSnapshotAsync(string channelId)
        {
            var endpoints = new[]
            {
                $"/ISAPI/Streaming/channels/{channelId}01/picture",
                $"/ISAPI/Streaming/channels/{channelId}/picture",
                $"/ISAPI/ContentMgmt/InputProxy/channels/{channelId}/image",
                $"/Streaming/channels/{channelId}01/picture"
            };

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var response = await _httpClient.GetAsync($"{_baseUrl}{endpoint}");
                    if (response.IsSuccessStatusCode)
                    {
                        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                        if (contentType.Contains("image") || contentType.Contains("jpeg") || contentType.Contains("octet"))
                        {
                            return await response.Content.ReadAsByteArrayAsync();
                        }
                    }
                }
                catch { }
            }

            LastError = "Nie udało się pobrać obrazu z kamery";
            throw new HikvisionException(LastError);
        }

        /// <summary>
        /// Generuje URL RTSP do podglądu na żywo
        /// </summary>
        public string GetRtspUrl(string channelId, bool mainStream = true)
        {
            // Format: rtsp://user:pass@ip:554/Streaming/Channels/XY
            // X = numer kanału, Y = 01 (główny) lub 02 (substream)
            var streamType = mainStream ? "01" : "02";
            var uri = new Uri(_baseUrl);
            return $"rtsp://{_username}:{_password}@{uri.Host}:554/Streaming/Channels/{channelId}{streamType}";
        }

        /// <summary>
        /// Testuje połączenie z NVR
        /// </summary>
        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/ISAPI/System/deviceInfo");

                if (response.IsSuccessStatusCode)
                    return (true, "Połączono z NVR");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return (false, "Błędny login lub hasło");

                return (false, $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            }
            catch (HttpRequestException ex)
            {
                return (false, $"Błąd sieci: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                return (false, "Timeout - NVR nie odpowiada (sprawdź IP)");
            }
            catch (Exception ex)
            {
                return (false, $"Błąd: {ex.Message}");
            }
        }

        private string GetElementValue(XElement parent, string name, XNamespace ns)
        {
            return parent?.Element(ns + name)?.Value
                ?? parent?.Element(name)?.Value;
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
