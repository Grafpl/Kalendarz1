namespace MobileAPI.DTOs;

public class OrderItemDto
{
    public int ZamowienieId { get; set; }
    public string KodTowaru { get; set; } = string.Empty;
    public string NazwaTowaru { get; set; } = string.Empty;
    public decimal Ilosc { get; set; }
    public decimal Cena { get; set; }
    public int Pojemniki { get; set; }
    public int Palety { get; set; }
    public bool E2 { get; set; }
    public bool Folia { get; set; }
    public bool Hallal { get; set; }
    public decimal Wydano { get; set; }
}
