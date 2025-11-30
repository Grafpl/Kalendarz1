using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Serwis integracji z ARIMR (Agencja Restrukturyzacji i Modernizacji Rolnictwa)
    /// Obsługuje zgłoszenia do systemu IRZplus dla drobiu
    /// </summary>
    public class ArimrService
    {
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private readonly string _numerZakladu; // Numer weterynaryjny ubojni
        private readonly string _exportPath;
        private static readonly HttpClient _httpClient = new HttpClient();

        // Środowisko produkcyjne ARIMR
        private const string PROD_URL = "https://irz.arimr.gov.pl/api/v1";
        // Środowisko testowe
        private const string TEST_URL = "https://irz-test.arimr.gov.pl/api/v1";

        public ArimrService(string apiKey = null, string numerZakladu = "10141607", bool useTestEnv = false)
        {
            _apiKey = apiKey;
            _numerZakladu = numerZakladu; // Numer weterynaryjny Piórkowscy
            _apiUrl = useTestEnv ? TEST_URL : PROD_URL;

            _exportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ARIMR_Export");

            if (!Directory.Exists(_exportPath))
                Directory.CreateDirectory(_exportPath);
        }

        /// <summary>
        /// Generuje zgłoszenie uboju drobiu do ARIMR
        /// Format XML zgodny z wymaganiami IRZplus
        /// </summary>
        public ArimrResult GenerateUbojReport(UbojDrobiuData uboj)
        {
            try
            {
                var xml = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    new XElement("ZgloszenieUboju",
                        new XAttribute("wersja", "1.0"),
                        new XAttribute("dataGenerowania", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")),

                        new XElement("Ubojnia",
                            new XElement("NumerWeterynaryjny", _numerZakladu),
                            new XElement("Nazwa", "Ubojnia Drobiu Piórkowscy"),
                            new XElement("Adres", "Koziołki 40, 95-061 Dmosin")
                        ),

                        new XElement("Dostawca",
                            new XElement("NumerSiedziby", uboj.NumerSiedzibyStada ?? ""),
                            new XElement("NIP", uboj.DostawcaNIP ?? ""),
                            new XElement("Nazwa", uboj.DostawcaNazwa),
                            new XElement("NumerWeterynaryjny", uboj.DostawcaNumerWet ?? ""),
                            new XElement("Adres", uboj.DostawcaAdres ?? "")
                        ),

                        new XElement("DaneUboju",
                            new XElement("DataUboju", uboj.DataUboju.ToString("yyyy-MM-dd")),
                            new XElement("DataDostawy", uboj.DataDostawy.ToString("yyyy-MM-dd")),
                            new XElement("GodzinaDostawy", uboj.GodzinaDostawy.ToString("HH:mm")),
                            new XElement("NumerDokumentuPrzewozowego", uboj.NumerDokumentuPrzewozowego ?? ""),
                            new XElement("NumerRejestracyjnyPojazdu", uboj.NumerRejestracyjnyPojazdu ?? "")
                        ),

                        GeneratePozycjeElement(uboj.Pozycje),

                        new XElement("Podsumowanie",
                            new XElement("LacznaIloscSztuk", uboj.LacznaIloscSztuk),
                            new XElement("LacznaWagaKg", uboj.LacznaWagaKg.ToString("F2")),
                            new XElement("IloscPadlych", uboj.IloscPadlych),
                            new XElement("IloscOdrzuconych", uboj.IloscOdrzuconych)
                        ),

                        new XElement("Uwagi", uboj.Uwagi ?? "")
                    )
                );

                string fileName = $"ARIMR_Uboj_{uboj.DataUboju:yyyyMMdd}_{uboj.DostawcaNazwa.Replace(" ", "_")}_{DateTime.Now:HHmmss}.xml";
                string filePath = Path.Combine(_exportPath, fileName);

                xml.Save(filePath);

                return new ArimrResult
                {
                    Success = true,
                    FilePath = filePath,
                    Message = $"Zgłoszenie wygenerowane: {filePath}"
                };
            }
            catch (Exception ex)
            {
                return new ArimrResult
                {
                    Success = false,
                    Message = $"Błąd generowania zgłoszenia: {ex.Message}"
                };
            }
        }

        private XElement GeneratePozycjeElement(List<PozycjaUboju> pozycje)
        {
            var element = new XElement("Pozycje");

            foreach (var poz in pozycje)
            {
                element.Add(new XElement("Pozycja",
                    new XElement("GatunekDrobiu", poz.GatunekDrobiu),
                    new XElement("KodGatunku", GetKodGatunku(poz.GatunekDrobiu)),
                    new XElement("KategoriaWiekowa", poz.KategoriaWiekowa ?? ""),
                    new XElement("IloscSztuk", poz.IloscSztuk),
                    new XElement("WagaBruttoKg", poz.WagaBruttoKg.ToString("F2")),
                    new XElement("WagaTaraKg", poz.WagaTaraKg.ToString("F2")),
                    new XElement("WagaNettoKg", poz.WagaNettoKg.ToString("F2")),
                    new XElement("Padniete", poz.Padniete),
                    new XElement("Odrzucone", poz.Odrzucone),
                    new XElement("NumerPartii", poz.NumerPartii ?? "")
                ));
            }

            return element;
        }

        /// <summary>
        /// Generuje raport zbiorczy tygodniowy dla ARIMR
        /// </summary>
        public ArimrResult GenerateWeeklyReport(DateTime weekStart, List<UbojDrobiuData> uboje)
        {
            try
            {
                var weekEnd = weekStart.AddDays(6);

                var xml = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    new XElement("RaportTygodniowy",
                        new XAttribute("wersja", "1.0"),
                        new XElement("Okres",
                            new XElement("Od", weekStart.ToString("yyyy-MM-dd")),
                            new XElement("Do", weekEnd.ToString("yyyy-MM-dd"))
                        ),
                        new XElement("Ubojnia",
                            new XElement("NumerWeterynaryjny", _numerZakladu),
                            new XElement("Nazwa", "Ubojnia Drobiu Piórkowscy")
                        ),
                        GeneratePodsumowanieTygodniowe(uboje),
                        GenerateUbojeElement(uboje)
                    )
                );

                string fileName = $"ARIMR_Tydzien_{weekStart:yyyyMMdd}_{weekEnd:yyyyMMdd}.xml";
                string filePath = Path.Combine(_exportPath, fileName);

                xml.Save(filePath);

                return new ArimrResult
                {
                    Success = true,
                    FilePath = filePath,
                    Message = $"Raport tygodniowy wygenerowany: {filePath}"
                };
            }
            catch (Exception ex)
            {
                return new ArimrResult
                {
                    Success = false,
                    Message = $"Błąd generowania raportu: {ex.Message}"
                };
            }
        }

        private XElement GeneratePodsumowanieTygodniowe(List<UbojDrobiuData> uboje)
        {
            int lacznieSztuk = 0;
            decimal lacznieKg = 0;
            int laczniePadlych = 0;
            int laczenieOdrzuconych = 0;
            int liczbaDostaw = uboje.Count;

            foreach (var uboj in uboje)
            {
                lacznieSztuk += uboj.LacznaIloscSztuk;
                lacznieKg += uboj.LacznaWagaKg;
                laczniePadlych += uboj.IloscPadlych;
                laczenieOdrzuconych += uboj.IloscOdrzuconych;
            }

            return new XElement("Podsumowanie",
                new XElement("LiczbaDostaw", liczbaDostaw),
                new XElement("LacznaIloscSztuk", lacznieSztuk),
                new XElement("LacznaWagaKg", lacznieKg.ToString("F2")),
                new XElement("LacznaIloscPadlych", laczniePadlych),
                new XElement("LacznaIloscOdrzuconych", laczenieOdrzuconych),
                new XElement("SredniaWagaSztuki", lacznieSztuk > 0 ? (lacznieKg / lacznieSztuk).ToString("F3") : "0")
            );
        }

        private XElement GenerateUbojeElement(List<UbojDrobiuData> uboje)
        {
            var element = new XElement("Uboje");

            foreach (var uboj in uboje)
            {
                element.Add(new XElement("Uboj",
                    new XElement("Data", uboj.DataUboju.ToString("yyyy-MM-dd")),
                    new XElement("Dostawca", uboj.DostawcaNazwa),
                    new XElement("NumerSiedziby", uboj.NumerSiedzibyStada ?? ""),
                    new XElement("IloscSztuk", uboj.LacznaIloscSztuk),
                    new XElement("WagaKg", uboj.LacznaWagaKg.ToString("F2")),
                    new XElement("Padniete", uboj.IloscPadlych),
                    new XElement("Odrzucone", uboj.IloscOdrzuconych)
                ));
            }

            return element;
        }

        /// <summary>
        /// Wysyła zgłoszenie do API ARIMR (wymaga klucza API)
        /// </summary>
        public async Task<ArimrResult> SendToArimrAsync(string xmlFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    return new ArimrResult
                    {
                        Success = false,
                        Message = "Brak klucza API ARIMR. Skonfiguruj klucz w ustawieniach."
                    };
                }

                if (!File.Exists(xmlFilePath))
                {
                    return new ArimrResult
                    {
                        Success = false,
                        Message = $"Plik nie istnieje: {xmlFilePath}"
                    };
                }

                string xmlContent = File.ReadAllText(xmlFilePath);

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/zgloszenia/uboj");
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");
                request.Headers.Add("X-Numer-Zakladu", _numerZakladu);
                request.Content = new StringContent(xmlContent, Encoding.UTF8, "application/xml");

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return new ArimrResult
                    {
                        Success = true,
                        Message = "Zgłoszenie wysłane do ARIMR",
                        NumerZgloszenia = ParseNumerZgloszenia(responseBody)
                    };
                }

                return new ArimrResult
                {
                    Success = false,
                    Message = $"Błąd ARIMR: {response.StatusCode} - {responseBody}"
                };
            }
            catch (Exception ex)
            {
                return new ArimrResult
                {
                    Success = false,
                    Message = $"Błąd wysyłania: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Pobiera status zgłoszenia z ARIMR
        /// </summary>
        public async Task<ArimrResult> GetStatusAsync(string numerZgloszenia)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    return new ArimrResult
                    {
                        Success = false,
                        Message = "Brak klucza API ARIMR"
                    };
                }

                var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiUrl}/zgloszenia/{numerZgloszenia}/status");
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return new ArimrResult
                    {
                        Success = true,
                        Message = responseBody,
                        NumerZgloszenia = numerZgloszenia
                    };
                }

                return new ArimrResult
                {
                    Success = false,
                    Message = $"Błąd: {responseBody}"
                };
            }
            catch (Exception ex)
            {
                return new ArimrResult
                {
                    Success = false,
                    Message = $"Błąd połączenia: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Konwertuje nazwę gatunku na kod ARIMR
        /// </summary>
        private string GetKodGatunku(string gatunek)
        {
            return gatunek?.ToUpperInvariant() switch
            {
                "KURCZAK" or "KURCZĘTA" or "BROJLER" => "DRB_KUR",
                "KACZKA" or "KACZKI" => "DRB_KAC",
                "GĘSI" or "GĘŚ" => "DRB_GES",
                "INDYK" or "INDYKI" => "DRB_IND",
                "PERLICZKA" or "PERLICZKI" => "DRB_PER",
                "PRZEPIÓRKA" or "PRZEPIÓRKI" => "DRB_PRZ",
                _ => "DRB_INN" // Inne
            };
        }

        private string ParseNumerZgloszenia(string response)
        {
            try
            {
                var json = JsonConvert.DeserializeObject<dynamic>(response);
                return json?.numerZgloszenia?.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Pobiera ścieżkę folderu eksportu
        /// </summary>
        public string GetExportPath() => _exportPath;

        /// <summary>
        /// Sprawdza czy serwis jest skonfigurowany do wysyłania online
        /// </summary>
        public bool IsConfigured() => !string.IsNullOrEmpty(_apiKey);
    }

    #region Data Models

    public class UbojDrobiuData
    {
        // Dane dostawcy (hodowcy)
        public string DostawcaNazwa { get; set; }
        public string DostawcaNIP { get; set; }
        public string DostawcaAdres { get; set; }
        public string DostawcaNumerWet { get; set; }
        public string NumerSiedzibyStada { get; set; }

        // Dane transportu
        public DateTime DataDostawy { get; set; }
        public DateTime GodzinaDostawy { get; set; }
        public DateTime DataUboju { get; set; }
        public string NumerDokumentuPrzewozowego { get; set; }
        public string NumerRejestracyjnyPojazdu { get; set; }

        // Pozycje (różne gatunki/kategorie)
        public List<PozycjaUboju> Pozycje { get; set; } = new List<PozycjaUboju>();

        // Podsumowanie
        public int LacznaIloscSztuk { get; set; }
        public decimal LacznaWagaKg { get; set; }
        public int IloscPadlych { get; set; }
        public int IloscOdrzuconych { get; set; }

        public string Uwagi { get; set; }
    }

    public class PozycjaUboju
    {
        public string GatunekDrobiu { get; set; } // Kurczak, Kaczka, Gęś, Indyk, itp.
        public string KategoriaWiekowa { get; set; }
        public int IloscSztuk { get; set; }
        public decimal WagaBruttoKg { get; set; }
        public decimal WagaTaraKg { get; set; }
        public decimal WagaNettoKg { get; set; }
        public int Padniete { get; set; }
        public int Odrzucone { get; set; }
        public string NumerPartii { get; set; }
    }

    public class ArimrResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string FilePath { get; set; }
        public string NumerZgloszenia { get; set; }
    }

    #endregion
}
