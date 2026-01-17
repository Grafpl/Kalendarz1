namespace KalendarzMobile.Api.Models;

public class Zamowienie
{
    public int Id { get; set; }
    public DateTime DataZamowienia { get; set; }
    public DateTime DataPrzyjazdu { get; set; }
    public DateTime? DataProdukcji { get; set; }
    public int KlientId { get; set; }
    public string KlientNazwa { get; set; } = string.Empty;
    public string? KlientMiasto { get; set; }
    public string? Handlowiec { get; set; }
    public string? Uwagi { get; set; }
    public string? IdUser { get; set; }
    public DateTime DataUtworzenia { get; set; }
    public int LiczbaPojemnikow { get; set; }
    public decimal LiczbaPalet { get; set; }
    public bool TrybE2 { get; set; }
    public string? TransportStatus { get; set; }
    public string Waluta { get; set; } = "PLN";
    public string Status { get; set; } = "Nowe";
    public decimal ProcentRealizacji { get; set; }
    public bool CzyCzesciowoZrealizowane { get; set; }
    public List<ZamowienieTowa> Towary { get; set; } = new();
}

public class ZamowienieTowa
{
    public int Id { get; set; }
    public int ZamowienieId { get; set; }
    public int KodTowaru { get; set; }
    public string NazwaTowaru { get; set; } = string.Empty;
    public decimal Ilosc { get; set; }
    public decimal Cena { get; set; }
    public int Pojemniki { get; set; }
    public decimal Palety { get; set; }
    public bool E2 { get; set; }
    public bool Folia { get; set; }
    public bool Hallal { get; set; }
    public decimal IloscZrealizowana { get; set; }
    public string? PowodBraku { get; set; }
}

public class Kontrahent
{
    public int Id { get; set; }
    public string Shortcut { get; set; } = string.Empty;
    public string Nazwa { get; set; } = string.Empty;
    public string? Handlowiec { get; set; }
    public string? Adres { get; set; }
    public string? Miasto { get; set; }
    public string? KodPocztowy { get; set; }
    public string? Telefon { get; set; }
    public string? Email { get; set; }
    public string? NIP { get; set; }
    public DateTime? OstatnieZamowienie { get; set; }
}

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

public class ZamowieniaResponse
{
    public List<Zamowienie> Zamowienia { get; set; } = new();
    public int Total { get; set; }
    public bool HasMore { get; set; }
}

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
