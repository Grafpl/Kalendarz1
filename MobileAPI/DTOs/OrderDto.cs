namespace MobileAPI.DTOs;

public class OrderDto
{
    public int Id { get; set; }
    public int KlientId { get; set; }
    public string Odbiorca { get; set; } = string.Empty;
    public string NazwaKlienta { get; set; } = string.Empty;
    public string Handlowiec { get; set; } = string.Empty;
    public decimal IloscZamowiona { get; set; }
    public decimal IloscFaktyczna { get; set; }
    public decimal Roznica { get; set; }
    public int Pojemniki { get; set; }
    public int Palety { get; set; }
    public bool TrybE2 { get; set; }
    public DateTime? DataPrzyjecia { get; set; }
    public string? GodzinaPrzyjecia { get; set; }
    public DateTime? TerminOdbioru { get; set; }
    public DateTime? DataUboju { get; set; }
    public string? UtworzonePrzez { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool MaNotatke { get; set; }
    public bool MaFolie { get; set; }
    public bool MaHallal { get; set; }
    public bool CzyMaCeny { get; set; }
    public decimal SredniaCena { get; set; }
    public string? Uwagi { get; set; }
    public int? TransportKursId { get; set; }
    public bool CzyZrealizowane { get; set; }
    public DateTime? DataWydania { get; set; }
    public string? Waluta { get; set; }
    public List<OrderItemDto> Pozycje { get; set; } = new();
}
