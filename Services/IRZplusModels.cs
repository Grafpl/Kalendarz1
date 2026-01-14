using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kalendarz1.Services
{
    #region API Request Models

    /// <summary>
    /// Model dyspozycji - główny obiekt do zgłaszania uboju drobiu do IRZplus
    /// </summary>
    public class DyspozycjaZZSSD
    {
        [JsonPropertyName("dataUboju")]
        public string DataUboju { get; set; }

        [JsonPropertyName("numerSiedliska")]
        public string NumerSiedliska { get; set; }

        [JsonPropertyName("numerUbojni")]
        public string NumerUbojni { get; set; }

        [JsonPropertyName("gatunekDrobiu")]
        public string GatunekDrobiu { get; set; }

        [JsonPropertyName("iloscSztuk")]
        public int IloscSztuk { get; set; }

        [JsonPropertyName("wagaKg")]
        public decimal WagaKg { get; set; }

        [JsonPropertyName("iloscPadlych")]
        public int IloscPadlych { get; set; }

        [JsonPropertyName("uwagi")]
        public string Uwagi { get; set; }

        [JsonPropertyName("numerPartii")]
        public string NumerPartii { get; set; }

        [JsonPropertyName("numerDokumentuPrzewozowego")]
        public string NumerDokumentuPrzewozowego { get; set; }
    }

    /// <summary>
    /// Model żądania zbiorczego zgłoszenia
    /// </summary>
    public class ZgloszenieZbiorczeRequest
    {
        [JsonPropertyName("dyspozycje")]
        public List<DyspozycjaZZSSD> Dyspozycje { get; set; } = new List<DyspozycjaZZSSD>();

        [JsonPropertyName("dataZgloszenia")]
        public string DataZgloszenia { get; set; }

        [JsonPropertyName("numerUbojni")]
        public string NumerUbojni { get; set; }
    }

    #endregion

    #region API Response Models

    /// <summary>
    /// Odpowiedź na żądanie tokenu OAuth 2.0
    /// </summary>
    public class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("scope")]
        public string Scope { get; set; }

        // Dodatkowe pola do zarządzania tokenem
        [JsonIgnore]
        public DateTime ExpirationTime { get; set; }

        [JsonIgnore]
        public bool IsExpired => DateTime.UtcNow >= ExpirationTime.AddMinutes(-5);
    }

    /// <summary>
    /// Odpowiedź z API IRZplus
    /// </summary>
    public class IRZplusApiResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("numerZgloszenia")]
        public string NumerZgloszenia { get; set; }

        [JsonPropertyName("dataRejestracji")]
        public string DataRejestracji { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("errors")]
        public List<string> Errors { get; set; } = new List<string>();

        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Status zgłoszenia
    /// </summary>
    public class StatusZgloszenia
    {
        [JsonPropertyName("numerZgloszenia")]
        public string NumerZgloszenia { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("dataZgloszenia")]
        public string DataZgloszenia { get; set; }

        [JsonPropertyName("dataAktualizacji")]
        public string DataAktualizacji { get; set; }

        [JsonPropertyName("uwagi")]
        public string Uwagi { get; set; }

        [JsonPropertyName("bledy")]
        public List<string> Bledy { get; set; } = new List<string>();
    }

    /// <summary>
    /// Historia zgłoszeń
    /// </summary>
    public class HistoriaZgloszen
    {
        [JsonPropertyName("zgloszenia")]
        public List<ZgloszenieHistoria> Zgloszenia { get; set; } = new List<ZgloszenieHistoria>();

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        [JsonPropertyName("pageNumber")]
        public int PageNumber { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }
    }

    /// <summary>
    /// Pojedyncze zgłoszenie w historii
    /// </summary>
    public class ZgloszenieHistoria
    {
        [JsonPropertyName("numerZgloszenia")]
        public string NumerZgloszenia { get; set; }

        [JsonPropertyName("dataUboju")]
        public string DataUboju { get; set; }

        [JsonPropertyName("dataZgloszenia")]
        public string DataZgloszenia { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("iloscDyspozycji")]
        public int IloscDyspozycji { get; set; }

        [JsonPropertyName("sumaIloscSztuk")]
        public int SumaIloscSztuk { get; set; }

        [JsonPropertyName("sumaWagaKg")]
        public decimal SumaWagaKg { get; set; }
    }

    #endregion

    #region Local Models

    /// <summary>
    /// Ustawienia konfiguracyjne IRZplus
    /// </summary>
    public class IRZplusSettings
    {
        public string NumerUbojni { get; set; } = "10141607"; // Numer weterynaryjny Piórkowscy
        public string NazwaUbojni { get; set; } = "Ubojnia Drobiu Piórkowscy";
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UseTestEnvironment { get; set; } = true;
        public bool AutoSendOnSave { get; set; } = false;
        public bool SaveLocalCopy { get; set; } = true;
        public string LocalExportPath { get; set; }
        public DateTime? LastSuccessfulSync { get; set; }

        // Endpointy API
        public string TokenEndpoint => UseTestEnvironment
            ? "https://irz-test.arimr.gov.pl/oauth/token"
            : "https://irz.arimr.gov.pl/oauth/token";

        public string ApiBaseUrl => UseTestEnvironment
            ? "https://irz-test.arimr.gov.pl/api/v1"
            : "https://irz.arimr.gov.pl/api/v1";
    }

    /// <summary>
    /// Wynik operacji IRZplus
    /// </summary>
    public class IRZplusResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string NumerZgloszenia { get; set; }
        public string FilePath { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public DateTime? Timestamp { get; set; }

        public static IRZplusResult Ok(string message, string numerZgloszenia = null)
        {
            return new IRZplusResult
            {
                Success = true,
                Message = message,
                NumerZgloszenia = numerZgloszenia,
                Timestamp = DateTime.Now
            };
        }

        public static IRZplusResult Error(string message, List<string> errors = null)
        {
            return new IRZplusResult
            {
                Success = false,
                Message = message,
                Errors = errors ?? new List<string>(),
                Timestamp = DateTime.Now
            };
        }
    }

    /// <summary>
    /// Model do przechowywania historii lokalnej wysyłek
    /// </summary>
    public class IRZplusLocalHistory
    {
        public int Id { get; set; }
        public DateTime DataWyslania { get; set; }
        public string NumerZgloszenia { get; set; }
        public string Status { get; set; }
        public DateTime DataUboju { get; set; }
        public int IloscDyspozycji { get; set; }
        public int SumaIloscSztuk { get; set; }
        public decimal SumaWagaKg { get; set; }
        public string UzytkownikId { get; set; }
        public string UzytkownikNazwa { get; set; }
        public string Uwagi { get; set; }
        public string ResponseJson { get; set; }
        public string RequestJson { get; set; }
    }

    /// <summary>
    /// Model specyfikacji do wysyłki do IRZplus
    /// </summary>
    public class SpecyfikacjaDoIRZplus
    {
        public int Id { get; set; }
        public DateTime DataUboju { get; set; }
        public string DostawcaNazwa { get; set; }
        public string NumerSiedliska { get; set; }
        public string GatunekDrobiu { get; set; } = "KURCZAK";
        public int IloscSztuk { get; set; }
        public decimal WagaNetto { get; set; }
        public int IloscPadlych { get; set; }
        public string NumerPartii { get; set; }
        public string NumerDokumentuPrzewozowego { get; set; }
        public bool Wybrana { get; set; } = true;

        // Dodatkowe informacje
        public string NumerRejestracyjny { get; set; }
        public string KlasaA { get; set; }
        public string KlasaB { get; set; }
    }

    /// <summary>
    /// Podsumowanie wysyłki
    /// </summary>
    public class IRZplusPodsumowanie
    {
        public DateTime DataUboju { get; set; }
        public int LiczbaSpecyfikacji { get; set; }
        public int SumaIloscSztuk { get; set; }
        public decimal SumaWagaNetto { get; set; }
        public int SumaIloscPadlych { get; set; }
        public List<SpecyfikacjaDoIRZplus> Specyfikacje { get; set; } = new List<SpecyfikacjaDoIRZplus>();

        public string GetSummaryText()
        {
            return $"Data uboju: {DataUboju:yyyy-MM-dd}\n" +
                   $"Liczba dostawców: {LiczbaSpecyfikacji}\n" +
                   $"Suma sztuk: {SumaIloscSztuk:N0}\n" +
                   $"Suma wagi netto: {SumaWagaNetto:N2} kg\n" +
                   $"Suma padłych: {SumaIloscPadlych:N0}";
        }
    }

    #endregion

    #region Enums

    /// <summary>
    /// Status zgłoszenia w systemie IRZplus
    /// </summary>
    public enum IRZplusStatusZgloszenia
    {
        Nowe,
        Wysylane,
        Wyslane,
        Przyjete,
        Odrzucone,
        Blad,
        Anulowane
    }

    /// <summary>
    /// Gatunek drobiu zgodny z nomeklaturą IRZplus
    /// </summary>
    public enum GatunekDrobiuIRZplus
    {
        KURCZAK,
        KACZKA,
        GES,
        INDYK,
        PERLICZKA,
        PRZEPIORKA,
        INNE
    }

    #endregion
}
