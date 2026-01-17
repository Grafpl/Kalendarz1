using KalendarzMobile.Models;

namespace KalendarzMobile.Services;

/// <summary>
/// Interfejs serwisu zamówień
/// </summary>
public interface IZamowieniaService
{
    /// <summary>
    /// Pobiera listę zamówień z filtrowaniem
    /// </summary>
    Task<ZamowieniaResponse> GetZamowieniaAsync(ZamowieniaFilter? filter = null);

    /// <summary>
    /// Pobiera szczegóły zamówienia
    /// </summary>
    Task<Zamowienie?> GetZamowienieAsync(int id);

    /// <summary>
    /// Pobiera zamówienia na konkretny dzień
    /// </summary>
    Task<List<Zamowienie>> GetZamowieniaNaDzienAsync(DateTime data);

    /// <summary>
    /// Pobiera zamówienia klienta
    /// </summary>
    Task<List<Zamowienie>> GetZamowieniaKlientaAsync(int klientId, int limit = 20);

    /// <summary>
    /// Pobiera listę kontrahentów
    /// </summary>
    Task<List<Kontrahent>> GetKontrahenciAsync(string? szukaj = null);

    /// <summary>
    /// Pobiera statystyki zamówień na dzień
    /// </summary>
    Task<DzienneStatystyki> GetStatystykiDniaAsync(DateTime data);
}

/// <summary>
/// Statystyki zamówień na dzień
/// </summary>
public class DzienneStatystyki
{
    public DateTime Data { get; set; }
    public int LiczbaZamowien { get; set; }
    public int LiczbaKlientow { get; set; }
    public decimal SumaKg { get; set; }
    public int SumaPojemnikow { get; set; }
    public decimal SumaPalet { get; set; }
    public int ZamowieniaOczekujace { get; set; }
    public int ZamowieniaZrealizowane { get; set; }
}
