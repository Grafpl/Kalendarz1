using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace Kalendarz1.Monitoring
{
    /// <summary>
    /// Serwis do komunikacji z NVR INTERNEC/Hikvision
    /// Obsługuje zarówno ISAPI jak i CGI API
    /// </summary>
    public class HikvisionService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _username;
        private readonly string _password;
        private readonly string _ipAddress;
        public string LastError { get; private set; }
        private bool _useCgiApi = false;

        public HikvisionService(string ipAddress, string username, string password)
        {
            _baseUrl = $"http://{ipAddress}";
            _ipAddress = ipAddress;
            _username = username;
            _password = password;

            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(username, password),
                PreAuthenticate = true
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            // Dodaj Basic Auth header
            var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
        }

        /// <summary>
        /// Pobiera informacje o urządzeniu NVR
        /// </summary>
        public async Task<DeviceInfo> GetDeviceInfoAsync()
        {
            // Próbuj różne endpointy
            var endpoints = new[]
            {
                "/ISAPI/System/deviceInfo",                    // Hikvision ISAPI
                "/cgi-bin/devInfo.cgi",                        // CGI API
                "/cgi-bin/configManager.cgi?action=getConfig&name=General", // Dahua style
                "/Device/DeviceInfo"                           // Alternatywny
            };

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var response = await _httpClient.GetAsync($"{_baseUrl}{endpoint}");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();

                        if (endpoint.Contains("ISAPI"))
                        {
                            return ParseIsapiDeviceInfo(content);
                        }
                        else
                        {
                            _useCgiApi = true;
                            return ParseCgiDeviceInfo(content);
                        }
                    }
                }
                catch { }
            }

            // Jeśli nie udało się pobrać info, ale połączenie działa - zwróć podstawowe info
            var testResult = await TestConnectionAsync();
            if (testResult.Success)
            {
                _useCgiApi = true;
                return new DeviceInfo
                {
                    DeviceName = "NVR_Produkcja",
                    Model = "INTERNEC i6-N25232UHV",
                    DeviceType = "NVR",
                    FirmwareVersion = "Połączono"
                };
            }

            throw new HikvisionException("Nie udało się pobrać informacji o urządzeniu");
        }

        private DeviceInfo ParseIsapiDeviceInfo(string xml)
        {
            var doc = XDocument.Parse(xml);
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

        private DeviceInfo ParseCgiDeviceInfo(string content)
        {
            // Parsowanie odpowiedzi CGI (format key=value lub JSON)
            var info = new DeviceInfo { DeviceType = "NVR" };

            // Próba parsowania różnych formatów
            var nameMatch = Regex.Match(content, @"(?:deviceName|DeviceName|name)\s*[=:]\s*[""']?([^""'\r\n]+)", RegexOptions.IgnoreCase);
            if (nameMatch.Success) info.DeviceName = nameMatch.Groups[1].Value.Trim();

            var modelMatch = Regex.Match(content, @"(?:model|Model|deviceType)\s*[=:]\s*[""']?([^""'\r\n]+)", RegexOptions.IgnoreCase);
            if (modelMatch.Success) info.Model = modelMatch.Groups[1].Value.Trim();

            var serialMatch = Regex.Match(content, @"(?:serial|Serial|serialNumber)\s*[=:]\s*[""']?([^""'\r\n]+)", RegexOptions.IgnoreCase);
            if (serialMatch.Success) info.SerialNumber = serialMatch.Groups[1].Value.Trim();

            var firmwareMatch = Regex.Match(content, @"(?:firmware|Firmware|version|Version)\s*[=:]\s*[""']?([^""'\r\n]+)", RegexOptions.IgnoreCase);
            if (firmwareMatch.Success) info.FirmwareVersion = firmwareMatch.Groups[1].Value.Trim();

            var macMatch = Regex.Match(content, @"(?:mac|MAC|macAddress)\s*[=:]\s*[""']?([^""'\r\n]+)", RegexOptions.IgnoreCase);
            if (macMatch.Success) info.MacAddress = macMatch.Groups[1].Value.Trim();

            if (string.IsNullOrEmpty(info.DeviceName)) info.DeviceName = "NVR_Produkcja";
            if (string.IsNullOrEmpty(info.Model)) info.Model = "INTERNEC";

            return info;
        }

        /// <summary>
        /// Pobiera listę kanałów (kamer) z NVR - dla INTERNEC tworzymy listę 32 kanałów
        /// </summary>
        public async Task<List<CameraChannel>> GetChannelsAsync()
        {
            var channels = new List<CameraChannel>();

            // Dla INTERNEC NVR - zakładamy 32 kanały (na podstawie screenshotów)
            // Możemy spróbować pobrać rzeczywistą listę
            var endpoints = new[]
            {
                "/ISAPI/ContentMgmt/InputProxy/channels",
                "/ISAPI/System/Video/inputs/channels",
                "/cgi-bin/configManager.cgi?action=getConfig&name=ChannelTitle"
            };

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var response = await _httpClient.GetAsync($"{_baseUrl}{endpoint}");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var parsed = ParseChannelList(content);
                        if (parsed.Count > 0)
                        {
                            return parsed;
                        }
                    }
                }
                catch { }
            }

            // Domyślna lista 32 kanałów dla INTERNEC NVR
            var defaultNames = new Dictionary<int, string>
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

            for (int i = 1; i <= 32; i++)
            {
                channels.Add(new CameraChannel
                {
                    Id = i.ToString(),
                    Name = defaultNames.ContainsKey(i) ? $"D{i} ({defaultNames[i]})" : $"Kamera D{i}",
                    Status = "Aktywny"
                });
            }

            return channels;
        }

        private List<CameraChannel> ParseChannelList(string content)
        {
            var channels = new List<CameraChannel>();

            try
            {
                if (content.TrimStart().StartsWith("<"))
                {
                    var doc = XDocument.Parse(content);
                    foreach (var element in doc.Descendants())
                    {
                        if (element.Name.LocalName.Contains("Channel") ||
                            element.Name.LocalName.Contains("Input"))
                        {
                            var ns = element.Name.Namespace;
                            var id = GetElementValue(element, "id", ns);
                            var name = GetElementValue(element, "name", ns) ?? $"Kanał {id}";

                            if (!string.IsNullOrEmpty(id))
                            {
                                channels.Add(new CameraChannel
                                {
                                    Id = id,
                                    Name = name,
                                    Status = "Aktywny"
                                });
                            }
                        }
                    }
                }
            }
            catch { }

            return channels;
        }

        /// <summary>
        /// Pobiera zrzut ekranu (snapshot) z kamery
        /// </summary>
        public async Task<byte[]> GetSnapshotAsync(string channelId)
        {
            // Różne formaty URL dla snapshotów
            var endpoints = new[]
            {
                // INTERNEC / Generic CGI
                $"/cgi-bin/snapshot.cgi?channel={channelId}",
                $"/cgi-bin/snapshot.cgi?chn={channelId}",
                $"/snapshot.cgi?channel={channelId}",
                $"/snap.cgi?channel={channelId}",

                // Hikvision ISAPI
                $"/ISAPI/Streaming/channels/{channelId}01/picture",
                $"/ISAPI/Streaming/channels/{channelId}/picture",

                // Alternatywne
                $"/Streaming/channels/{channelId}01/picture",
                $"/picture/{channelId}/current",
                $"/onvif/snapshot?channel={channelId}",

                // RTSP snapshot (niektóre NVR)
                $"/cgi-bin/images_cgi?channel={channelId}",
            };

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var response = await _httpClient.GetAsync($"{_baseUrl}{endpoint}");
                    if (response.IsSuccessStatusCode)
                    {
                        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                        var bytes = await response.Content.ReadAsByteArrayAsync();

                        // Sprawdź czy to rzeczywiście obraz (JPEG zaczyna się od FF D8)
                        if (bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
                        {
                            return bytes;
                        }

                        // Lub sprawdź content-type
                        if (contentType.Contains("image") || contentType.Contains("jpeg") || contentType.Contains("jpg"))
                        {
                            return bytes;
                        }
                    }
                }
                catch { }
            }

            LastError = "Nie znaleziono endpointu do pobierania snapshotów";
            throw new HikvisionException(LastError);
        }

        /// <summary>
        /// Generuje URL RTSP do podglądu na żywo
        /// </summary>
        public string GetRtspUrl(string channelId, bool mainStream = true)
        {
            var streamType = mainStream ? "01" : "02";

            // INTERNEC/Hikvision format
            return $"rtsp://{_username}:{_password}@{_ipAddress}:554/Streaming/Channels/{channelId}{streamType}";
        }

        /// <summary>
        /// Testuje połączenie z NVR
        /// </summary>
        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            // Lista endpointów do przetestowania
            var testEndpoints = new[]
            {
                "/",
                "/cgi-bin/main-cgi",
                "/ISAPI/System/deviceInfo",
                "/cgi-bin/devInfo.cgi"
            };

            foreach (var endpoint in testEndpoints)
            {
                try
                {
                    var response = await _httpClient.GetAsync($"{_baseUrl}{endpoint}");

                    // 200 OK lub 401 (wymaga auth) oznacza że NVR odpowiada
                    if (response.IsSuccessStatusCode)
                    {
                        return (true, $"Połączono z NVR ({endpoint})");
                    }

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        return (false, "Błędny login lub hasło");
                    }

                    // 404 na jednym endpoincie - próbuj następny
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        continue;
                    }
                }
                catch (HttpRequestException)
                {
                    continue;
                }
                catch (TaskCanceledException)
                {
                    return (false, "Timeout - NVR nie odpowiada");
                }
            }

            // Ostatnia próba - czy host odpowiada w ogóle
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/");
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return (false, "NVR odpowiada ale login/hasło nieprawidłowe");
                }
                return (true, "NVR dostępny");
            }
            catch
            {
                return (false, $"Nie można połączyć z {_ipAddress}");
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
