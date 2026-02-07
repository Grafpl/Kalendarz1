namespace MobileAPI.DTOs;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Imie { get; set; } = string.Empty;
    public string Nazwisko { get; set; } = string.Empty;
    public string Handlowiec { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
