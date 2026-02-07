using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MobileAPI.Configuration;
using MobileAPI.DTOs;

namespace MobileAPI.Services;

public class AuthService
{
    private readonly string _connectionString;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IConfiguration configuration,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthService> logger)
    {
        _connectionString = configuration.GetConnectionString("LibraNet")
            ?? throw new InvalidOperationException("LibraNet connection string is missing.");
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public async Task<LoginResponse?> AuthenticateAsync(LoginRequest request)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Query the operators table in LibraNet for authentication
            const string sql = @"
                SELECT
                    o.Id,
                    o.Login,
                    o.Haslo,
                    o.Imie,
                    o.Nazwisko,
                    o.Handlowiec,
                    o.Aktywny
                FROM dbo.Operatorzy o
                WHERE o.Login = @Login AND o.Aktywny = 1";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Login", request.Login);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                _logger.LogWarning("Login attempt failed: user '{Login}' not found.", request.Login);
                return null;
            }

            var storedHash = reader.GetString(reader.GetOrdinal("Haslo"));
            var imie = reader.GetString(reader.GetOrdinal("Imie"));
            var nazwisko = reader.GetString(reader.GetOrdinal("Nazwisko"));
            var handlowiec = reader.IsDBNull(reader.GetOrdinal("Handlowiec"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("Handlowiec"));

            // Verify password - compare with stored hash
            if (!VerifyPassword(request.Password, storedHash))
            {
                _logger.LogWarning("Login attempt failed: invalid password for user '{Login}'.", request.Login);
                return null;
            }

            // Generate JWT token
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationInMinutes);
            var token = GenerateJwtToken(request.Login, imie, nazwisko, handlowiec, expiresAt);

            return new LoginResponse
            {
                Token = token,
                Login = request.Login,
                Imie = imie,
                Nazwisko = nazwisko,
                Handlowiec = handlowiec,
                ExpiresAt = expiresAt
            };
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Database error during authentication for user '{Login}'.", request.Login);
            throw;
        }
    }

    private bool VerifyPassword(string inputPassword, string storedHash)
    {
        // Hash the input password using SHA256 to compare with stored hash
        using var sha256 = SHA256.Create();
        var inputBytes = Encoding.UTF8.GetBytes(inputPassword);
        var hashBytes = sha256.ComputeHash(inputBytes);
        var inputHash = Convert.ToBase64String(hashBytes);

        return string.Equals(inputHash, storedHash, StringComparison.Ordinal);
    }

    private string GenerateJwtToken(
        string login, string imie, string nazwisko, string handlowiec, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, login),
            new Claim("imie", imie),
            new Claim("nazwisko", nazwisko),
            new Claim("handlowiec", handlowiec),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
