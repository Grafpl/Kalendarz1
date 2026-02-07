namespace MobileAPI.DTOs;

public class CustomerDto
{
    public int Id { get; set; }
    public string Shortcut { get; set; } = string.Empty;
    public string Nazwa { get; set; } = string.Empty;
    public string? Handlowiec { get; set; }
    public string? NIP { get; set; }
    public string? Adres { get; set; }
    public string? Miasto { get; set; }
    public string? KodPocztowy { get; set; }
    public string? Telefon { get; set; }
    public string? Email { get; set; }
    public int TerminPlatnosci { get; set; }
    public decimal LimitKredytowy { get; set; }
    public decimal SaldoNaleznosci { get; set; }
    public bool Aktywny { get; set; }
}
