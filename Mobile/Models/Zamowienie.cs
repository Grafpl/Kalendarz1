using System.Text.Json.Serialization;

namespace KalendarzMobile.Models;

/// <summary>
/// Model zamówienia - odpowiednik tabeli ZamowieniaMieso
/// </summary>
public class Zamowienie
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("dataZamowienia")]
    public DateTime DataZamowienia { get; set; }

    [JsonPropertyName("dataPrzyjazdu")]
    public DateTime DataPrzyjazdu { get; set; }

    [JsonPropertyName("dataProdukcji")]
    public DateTime? DataProdukcji { get; set; }

    [JsonPropertyName("klientId")]
    public int KlientId { get; set; }

    [JsonPropertyName("klientNazwa")]
    public string KlientNazwa { get; set; } = string.Empty;

    [JsonPropertyName("klientMiasto")]
    public string? KlientMiasto { get; set; }

    [JsonPropertyName("handlowiec")]
    public string? Handlowiec { get; set; }

    [JsonPropertyName("uwagi")]
    public string? Uwagi { get; set; }

    [JsonPropertyName("idUser")]
    public string? IdUser { get; set; }

    [JsonPropertyName("dataUtworzenia")]
    public DateTime DataUtworzenia { get; set; }

    [JsonPropertyName("liczbaPojemnikow")]
    public int LiczbaPojemnikow { get; set; }

    [JsonPropertyName("liczbaPalet")]
    public decimal LiczbaPalet { get; set; }

    [JsonPropertyName("trybE2")]
    public bool TrybE2 { get; set; }

    [JsonPropertyName("transportStatus")]
    public string? TransportStatus { get; set; }

    [JsonPropertyName("waluta")]
    public string Waluta { get; set; } = "PLN";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Nowe";

    [JsonPropertyName("procentRealizacji")]
    public decimal ProcentRealizacji { get; set; }

    [JsonPropertyName("czyCzesciowoZrealizowane")]
    public bool CzyCzesciowoZrealizowane { get; set; }

    [JsonPropertyName("towary")]
    public List<ZamowienieTowa> Towary { get; set; } = new();

    // Właściwości obliczane dla UI
    [JsonIgnore]
    public string DataZamowieniaFormatted => DataZamowienia.ToString("dd.MM.yyyy");

    [JsonIgnore]
    public string DataPrzyjazduFormatted => DataPrzyjazdu.ToString("dd.MM.yyyy HH:mm");

    [JsonIgnore]
    public string StatusColor => Status switch
    {
        "Nowe" => "#2196F3",
        "Oczekuje" => "#FFC107",
        "W realizacji" => "#FF9800",
        "Zrealizowane" => "#4CAF50",
        "Anulowane" => "#F44336",
        _ => "#757575"
    };

    [JsonIgnore]
    public string TransportIcon => TransportStatus switch
    {
        "Wlasny" => "🚗",
        "Oczekuje" => "🚛",
        _ => "📦"
    };

    [JsonIgnore]
    public decimal SumaKg => Towary.Sum(t => t.Ilosc);

    [JsonIgnore]
    public decimal SumaWartosci => Towary.Sum(t => t.Wartosc);

    [JsonIgnore]
    public string PodsumowanieKrotkie => $"{LiczbaPojemnikow} poj. | {LiczbaPalet:F1} pal. | {SumaKg:F0} kg";
}

/// <summary>
/// Model pozycji zamówienia - odpowiednik tabeli ZamowieniaMiesoTowar
/// </summary>
public class ZamowienieTowa
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("zamowienieId")]
    public int ZamowienieId { get; set; }

    [JsonPropertyName("kodTowaru")]
    public int KodTowaru { get; set; }

    [JsonPropertyName("nazwaTowaru")]
    public string NazwaTowaru { get; set; } = string.Empty;

    [JsonPropertyName("ilosc")]
    public decimal Ilosc { get; set; }

    [JsonPropertyName("cena")]
    public decimal Cena { get; set; }

    [JsonPropertyName("pojemniki")]
    public int Pojemniki { get; set; }

    [JsonPropertyName("palety")]
    public decimal Palety { get; set; }

    [JsonPropertyName("e2")]
    public bool E2 { get; set; }

    [JsonPropertyName("folia")]
    public bool Folia { get; set; }

    [JsonPropertyName("hallal")]
    public bool Hallal { get; set; }

    [JsonPropertyName("iloscZrealizowana")]
    public decimal IloscZrealizowana { get; set; }

    [JsonPropertyName("powodBraku")]
    public string? PowodBraku { get; set; }

    // Właściwości obliczane
    [JsonIgnore]
    public decimal Wartosc => Ilosc * Cena;

    [JsonIgnore]
    public string WartoscFormatted => $"{Wartosc:N2}";

    [JsonIgnore]
    public string IloscFormatted => $"{Ilosc:N2} kg";

    [JsonIgnore]
    public string CenaFormatted => $"{Cena:N2}";

    [JsonIgnore]
    public string FlagiText
    {
        get
        {
            var flagi = new List<string>();
            if (E2) flagi.Add("E2");
            if (Folia) flagi.Add("Folia");
            if (Hallal) flagi.Add("Hallal");
            return flagi.Count > 0 ? string.Join(", ", flagi) : "-";
        }
    }

    [JsonIgnore]
    public decimal ProcentRealizacji => Ilosc > 0 ? (IloscZrealizowana / Ilosc) * 100 : 0;
}
