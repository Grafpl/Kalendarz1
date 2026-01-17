using System.Net.Http.Json;
using System.Text.Json;
using KalendarzMobile.Models;

namespace KalendarzMobile.Services;

/// <summary>
/// Implementacja serwisu zamówień - łączy się z REST API
/// </summary>
public class ZamowieniaService : IZamowieniaService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settings;
    private readonly JsonSerializerOptions _jsonOptions;

    public ZamowieniaService(ISettingsService settings)
    {
        _settings = settings;
        _httpClient = new HttpClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_settings.ApiBaseUrl);

        if (!string.IsNullOrEmpty(_settings.AuthToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AuthToken);
        }
    }

    public async Task<ZamowieniaResponse> GetZamowieniaAsync(ZamowieniaFilter? filter = null)
    {
        try
        {
            ConfigureHttpClient();

            var queryParams = new List<string>();

            if (filter != null)
            {
                if (filter.DataOd.HasValue)
                    queryParams.Add($"dataOd={filter.DataOd.Value:yyyy-MM-dd}");
                if (filter.DataDo.HasValue)
                    queryParams.Add($"dataDo={filter.DataDo.Value:yyyy-MM-dd}");
                if (filter.KlientId.HasValue)
                    queryParams.Add($"klientId={filter.KlientId.Value}");
                if (!string.IsNullOrEmpty(filter.Status))
                    queryParams.Add($"status={Uri.EscapeDataString(filter.Status)}");
                if (!string.IsNullOrEmpty(filter.Handlowiec))
                    queryParams.Add($"handlowiec={Uri.EscapeDataString(filter.Handlowiec)}");
                if (!string.IsNullOrEmpty(filter.Szukaj))
                    queryParams.Add($"szukaj={Uri.EscapeDataString(filter.Szukaj)}");

                queryParams.Add($"limit={filter.Limit}");
                queryParams.Add($"offset={filter.Offset}");
            }

            var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            var url = $"zamowienia{queryString}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ZamowieniaResponse>(_jsonOptions);
            return result ?? new ZamowieniaResponse();
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"HTTP Error: {ex.Message}");

            // W trybie offline/demo zwracamy przykładowe dane
            return GetDemoZamowienia(filter);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            return new ZamowieniaResponse();
        }
    }

    public async Task<Zamowienie?> GetZamowienieAsync(int id)
    {
        try
        {
            ConfigureHttpClient();

            var response = await _httpClient.GetAsync($"zamowienia/{id}");
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<Zamowienie>(_jsonOptions);
        }
        catch (HttpRequestException)
        {
            // Demo mode
            var demo = GetDemoZamowienia(null);
            return demo.Zamowienia.FirstOrDefault(z => z.Id == id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<Zamowienie>> GetZamowieniaNaDzienAsync(DateTime data)
    {
        var filter = new ZamowieniaFilter
        {
            DataOd = data.Date,
            DataDo = data.Date.AddDays(1).AddSeconds(-1),
            Limit = 100
        };

        var response = await GetZamowieniaAsync(filter);
        return response.Zamowienia;
    }

    public async Task<List<Zamowienie>> GetZamowieniaKlientaAsync(int klientId, int limit = 20)
    {
        var filter = new ZamowieniaFilter
        {
            KlientId = klientId,
            Limit = limit
        };

        var response = await GetZamowieniaAsync(filter);
        return response.Zamowienia;
    }

    public async Task<List<Kontrahent>> GetKontrahenciAsync(string? szukaj = null)
    {
        try
        {
            ConfigureHttpClient();

            var url = "kontrahenci";
            if (!string.IsNullOrEmpty(szukaj))
                url += $"?szukaj={Uri.EscapeDataString(szukaj)}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<List<Kontrahent>>(_jsonOptions)
                   ?? new List<Kontrahent>();
        }
        catch (HttpRequestException)
        {
            // Demo mode
            return GetDemoKontrahenci();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            return new List<Kontrahent>();
        }
    }

    public async Task<DzienneStatystyki> GetStatystykiDniaAsync(DateTime data)
    {
        try
        {
            ConfigureHttpClient();

            var response = await _httpClient.GetAsync($"statystyki/{data:yyyy-MM-dd}");
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<DzienneStatystyki>(_jsonOptions)
                   ?? new DzienneStatystyki { Data = data };
        }
        catch (HttpRequestException)
        {
            // Demo mode
            var zamowienia = await GetZamowieniaNaDzienAsync(data);
            return new DzienneStatystyki
            {
                Data = data,
                LiczbaZamowien = zamowienia.Count,
                LiczbaKlientow = zamowienia.Select(z => z.KlientId).Distinct().Count(),
                SumaKg = zamowienia.Sum(z => z.SumaKg),
                SumaPojemnikow = zamowienia.Sum(z => z.LiczbaPojemnikow),
                SumaPalet = zamowienia.Sum(z => z.LiczbaPalet),
                ZamowieniaOczekujace = zamowienia.Count(z => z.Status == "Oczekuje"),
                ZamowieniaZrealizowane = zamowienia.Count(z => z.Status == "Zrealizowane")
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            return new DzienneStatystyki { Data = data };
        }
    }

    #region Demo Data (do usunięcia w produkcji)

    /// <summary>
    /// Dane demonstracyjne do testowania bez API
    /// </summary>
    private ZamowieniaResponse GetDemoZamowienia(ZamowieniaFilter? filter)
    {
        var today = DateTime.Today;

        var demoZamowienia = new List<Zamowienie>
        {
            new Zamowienie
            {
                Id = 1,
                DataZamowienia = today,
                DataPrzyjazdu = today.AddHours(8),
                KlientId = 101,
                KlientNazwa = "Sklep Mięsny Kowalski",
                KlientMiasto = "Warszawa",
                Handlowiec = "JAN",
                LiczbaPojemnikow = 24,
                LiczbaPalet = 0.67m,
                Status = "Nowe",
                TransportStatus = "Oczekuje",
                Waluta = "PLN",
                Towary = new List<ZamowienieTowa>
                {
                    new ZamowienieTowa { Id = 1, KodTowaru = 1001, NazwaTowaru = "Filet z kurczaka", Ilosc = 150, Cena = 18.50m, Pojemniki = 10 },
                    new ZamowienieTowa { Id = 2, KodTowaru = 1002, NazwaTowaru = "Udo z kurczaka", Ilosc = 120, Cena = 12.00m, Pojemniki = 8 },
                    new ZamowienieTowa { Id = 3, KodTowaru = 1003, NazwaTowaru = "Skrzydełka", Ilosc = 90, Cena = 9.50m, Pojemniki = 6 }
                }
            },
            new Zamowienie
            {
                Id = 2,
                DataZamowienia = today,
                DataPrzyjazdu = today.AddHours(10),
                KlientId = 102,
                KlientNazwa = "Hurtownia Nowak",
                KlientMiasto = "Kraków",
                Handlowiec = "PIOTR",
                LiczbaPojemnikow = 72,
                LiczbaPalet = 2,
                Status = "W realizacji",
                TransportStatus = "Wlasny",
                Waluta = "PLN",
                ProcentRealizacji = 45,
                Towary = new List<ZamowienieTowa>
                {
                    new ZamowienieTowa { Id = 4, KodTowaru = 1001, NazwaTowaru = "Filet z kurczaka", Ilosc = 500, Cena = 17.80m, Pojemniki = 34, IloscZrealizowana = 225 },
                    new ZamowienieTowa { Id = 5, KodTowaru = 1004, NazwaTowaru = "Pierś z indyka", Ilosc = 300, Cena = 22.00m, Pojemniki = 20, IloscZrealizowana = 150 },
                    new ZamowienieTowa { Id = 6, KodTowaru = 1005, NazwaTowaru = "Wątróbka drobiowa", Ilosc = 270, Cena = 8.00m, Pojemniki = 18, IloscZrealizowana = 100 }
                }
            },
            new Zamowienie
            {
                Id = 3,
                DataZamowienia = today.AddDays(-1),
                DataPrzyjazdu = today.AddDays(-1).AddHours(14),
                KlientId = 103,
                KlientNazwa = "Restauracja Pod Złotym Kurczakiem",
                KlientMiasto = "Poznań",
                Handlowiec = "ANNA",
                LiczbaPojemnikow = 18,
                LiczbaPalet = 0.5m,
                Status = "Zrealizowane",
                TransportStatus = "Wlasny",
                Waluta = "PLN",
                ProcentRealizacji = 100,
                Towary = new List<ZamowienieTowa>
                {
                    new ZamowienieTowa { Id = 7, KodTowaru = 1001, NazwaTowaru = "Filet z kurczaka", Ilosc = 100, Cena = 19.00m, Pojemniki = 7, IloscZrealizowana = 100 },
                    new ZamowienieTowa { Id = 8, KodTowaru = 1006, NazwaTowaru = "Udko kurczaka", Ilosc = 80, Cena = 11.50m, Pojemniki = 6, IloscZrealizowana = 80 },
                    new ZamowienieTowa { Id = 9, KodTowaru = 1007, NazwaTowaru = "Kurczak cały", Ilosc = 75, Cena = 14.00m, Pojemniki = 5, IloscZrealizowana = 75 }
                }
            },
            new Zamowienie
            {
                Id = 4,
                DataZamowienia = today.AddDays(1),
                DataPrzyjazdu = today.AddDays(1).AddHours(6),
                KlientId = 104,
                KlientNazwa = "Sieć Delikatesów Premium",
                KlientMiasto = "Wrocław",
                Handlowiec = "JAN",
                LiczbaPojemnikow = 108,
                LiczbaPalet = 3,
                Status = "Oczekuje",
                TransportStatus = "Oczekuje",
                Waluta = "EUR",
                Towary = new List<ZamowienieTowa>
                {
                    new ZamowienieTowa { Id = 10, KodTowaru = 1001, NazwaTowaru = "Filet z kurczaka", Ilosc = 600, Cena = 4.20m, Pojemniki = 40, Hallal = true },
                    new ZamowienieTowa { Id = 11, KodTowaru = 1002, NazwaTowaru = "Udo z kurczaka", Ilosc = 450, Cena = 2.80m, Pojemniki = 30, Hallal = true },
                    new ZamowienieTowa { Id = 12, KodTowaru = 1008, NazwaTowaru = "Filet z piersi indyka", Ilosc = 570, Cena = 5.10m, Pojemniki = 38, Folia = true }
                }
            },
            new Zamowienie
            {
                Id = 5,
                DataZamowienia = today.AddDays(-2),
                DataPrzyjazdu = today.AddDays(-2).AddHours(12),
                KlientId = 105,
                KlientNazwa = "Masarnia Tradycyjna",
                KlientMiasto = "Gdańsk",
                Handlowiec = "PIOTR",
                LiczbaPojemnikow = 36,
                LiczbaPalet = 1,
                Status = "Anulowane",
                TransportStatus = "Oczekuje",
                Waluta = "PLN",
                Uwagi = "Klient zrezygnował z zamówienia",
                Towary = new List<ZamowienieTowa>
                {
                    new ZamowienieTowa { Id = 13, KodTowaru = 1001, NazwaTowaru = "Filet z kurczaka", Ilosc = 200, Cena = 18.00m, Pojemniki = 14 },
                    new ZamowienieTowa { Id = 14, KodTowaru = 1003, NazwaTowaru = "Skrzydełka", Ilosc = 180, Cena = 9.00m, Pojemniki = 12 },
                    new ZamowienieTowa { Id = 15, KodTowaru = 1009, NazwaTowaru = "Żołądki drobiowe", Ilosc = 150, Cena = 7.50m, Pojemniki = 10 }
                }
            }
        };

        var result = demoZamowienia.AsEnumerable();

        // Filtrowanie
        if (filter != null)
        {
            if (filter.DataOd.HasValue)
                result = result.Where(z => z.DataPrzyjazdu >= filter.DataOd.Value);
            if (filter.DataDo.HasValue)
                result = result.Where(z => z.DataPrzyjazdu <= filter.DataDo.Value);
            if (filter.KlientId.HasValue)
                result = result.Where(z => z.KlientId == filter.KlientId.Value);
            if (!string.IsNullOrEmpty(filter.Status))
                result = result.Where(z => z.Status == filter.Status);
            if (!string.IsNullOrEmpty(filter.Szukaj))
                result = result.Where(z =>
                    z.KlientNazwa.Contains(filter.Szukaj, StringComparison.OrdinalIgnoreCase) ||
                    z.KlientMiasto?.Contains(filter.Szukaj, StringComparison.OrdinalIgnoreCase) == true);
        }

        var list = result.OrderByDescending(z => z.DataPrzyjazdu).ToList();

        return new ZamowieniaResponse
        {
            Zamowienia = list,
            Total = list.Count,
            HasMore = false
        };
    }

    private List<Kontrahent> GetDemoKontrahenci()
    {
        return new List<Kontrahent>
        {
            new Kontrahent { Id = 101, Nazwa = "Sklep Mięsny Kowalski", Miasto = "Warszawa", Handlowiec = "JAN", Telefon = "501-123-456" },
            new Kontrahent { Id = 102, Nazwa = "Hurtownia Nowak", Miasto = "Kraków", Handlowiec = "PIOTR", Telefon = "502-234-567" },
            new Kontrahent { Id = 103, Nazwa = "Restauracja Pod Złotym Kurczakiem", Miasto = "Poznań", Handlowiec = "ANNA", Telefon = "503-345-678" },
            new Kontrahent { Id = 104, Nazwa = "Sieć Delikatesów Premium", Miasto = "Wrocław", Handlowiec = "JAN", Telefon = "504-456-789" },
            new Kontrahent { Id = 105, Nazwa = "Masarnia Tradycyjna", Miasto = "Gdańsk", Handlowiec = "PIOTR", Telefon = "505-567-890" }
        };
    }

    #endregion
}
