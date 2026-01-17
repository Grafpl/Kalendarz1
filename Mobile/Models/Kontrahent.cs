using System.Text.Json.Serialization;

namespace KalendarzMobile.Models;

/// <summary>
/// Model kontrahenta/klienta
/// </summary>
public class Kontrahent
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("shortcut")]
    public string Shortcut { get; set; } = string.Empty;

    [JsonPropertyName("nazwa")]
    public string Nazwa { get; set; } = string.Empty;

    [JsonPropertyName("handlowiec")]
    public string? Handlowiec { get; set; }

    [JsonPropertyName("adres")]
    public string? Adres { get; set; }

    [JsonPropertyName("miasto")]
    public string? Miasto { get; set; }

    [JsonPropertyName("kodPocztowy")]
    public string? KodPocztowy { get; set; }

    [JsonPropertyName("telefon")]
    public string? Telefon { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("nip")]
    public string? NIP { get; set; }

    [JsonPropertyName("ostatnieZamowienie")]
    public DateTime? OstatnieZamowienie { get; set; }

    // Właściwości dla UI
    [JsonIgnore]
    public string PelnyAdres => !string.IsNullOrEmpty(KodPocztowy) && !string.IsNullOrEmpty(Miasto)
        ? $"{KodPocztowy} {Miasto}"
        : Miasto ?? "";

    [JsonIgnore]
    public string NazwaZMiastem => !string.IsNullOrEmpty(Miasto)
        ? $"{Nazwa} ({Miasto})"
        : Nazwa;
}

/// <summary>
/// Filtr do wyszukiwania zamówień
/// </summary>
public class ZamowieniaFilter
{
    public DateTime? DataOd { get; set; }
    public DateTime? DataDo { get; set; }
    public int? KlientId { get; set; }
    public string? Status { get; set; }
    public string? Handlowiec { get; set; }
    public string? Szukaj { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; } = 0;
}

/// <summary>
/// Odpowiedź API z listą zamówień
/// </summary>
public class ZamowieniaResponse
{
    [JsonPropertyName("zamowienia")]
    public List<Zamowienie> Zamowienia { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}
