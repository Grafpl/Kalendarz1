namespace MobileAPI.DTOs;

public class ProductDto
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazwa { get; set; } = string.Empty;
    public string Katalog { get; set; } = string.Empty;
    public string JM { get; set; } = string.Empty;
    public decimal Cena { get; set; }
    public bool Aktywny { get; set; }
}
