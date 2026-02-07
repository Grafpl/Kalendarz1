namespace MobileAPI.DTOs;

public class OrderCreateDto
{
    public int KlientId { get; set; }
    public string Odbiorca { get; set; } = string.Empty;
    public string Handlowiec { get; set; } = string.Empty;
    public DateTime? DataPrzyjecia { get; set; }
    public string? GodzinaPrzyjecia { get; set; }
    public DateTime? TerminOdbioru { get; set; }
    public DateTime? DataUboju { get; set; }
    public bool TrybE2 { get; set; }
    public bool MaFolie { get; set; }
    public bool MaHallal { get; set; }
    public string? Uwagi { get; set; }
    public string? Waluta { get; set; }
    public int? TransportKursId { get; set; }
    public List<OrderItemCreateDto> Pozycje { get; set; } = new();
}

public class OrderItemCreateDto
{
    public string KodTowaru { get; set; } = string.Empty;
    public string NazwaTowaru { get; set; } = string.Empty;
    public decimal Ilosc { get; set; }
    public decimal Cena { get; set; }
    public int Pojemniki { get; set; }
    public int Palety { get; set; }
    public bool E2 { get; set; }
    public bool Folia { get; set; }
    public bool Hallal { get; set; }
}
