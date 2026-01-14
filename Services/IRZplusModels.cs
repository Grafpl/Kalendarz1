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
        public string NumerProducenta { get; set; } = "039806095"; // Numer producenta ARiMR
        public string NumerDzialalnosci { get; set; } = "039806095-001"; // Numer działalności

        // Dane logowania OAuth - domyślne wartości produkcyjne
        public string ClientId { get; set; } = "aplikacja-irzplus"; // Stały client_id ARiMR
        public string ClientSecret { get; set; } = ""; // NIE UŻYWAĆ - ARiMR nie wymaga
        public string Username { get; set; } = "039806095"; // Numer producenta jako login
        public string Password { get; set; } = "Jpiorkowski51"; // Hasło konta ARiMR

        public bool UseTestEnvironment { get; set; } = false; // Domyślnie PRODUKCJA
        public bool AutoSendOnSave { get; set; } = false;
        public bool SaveLocalCopy { get; set; } = true;
        public string LocalExportPath { get; set; }
        public DateTime? LastSuccessfulSync { get; set; }

        // POPRAWNY URL tokenu OAuth z dokumentacji ARiMR (02.02.2023)
        // Ten sam URL dla testów i produkcji - różnica tylko w danych logowania
        public string TokenEndpoint => "https://sso.arimr.gov.pl/auth/realms/ewniosekplus/protocol/openid-connect/token";

        // Endpointy API IRZplus
        public string ApiBaseUrl => UseTestEnvironment
            ? "https://irz-test.arimr.gov.pl/api/v1"
            : "https://irz.arimr.gov.pl/api/v1";

        // Dane testowe (do testów bez wpływu na produkcję)
        public const string TEST_USERNAME = "api_test_portalirzplus1";
        public const string TEST_PASSWORD = "api_test_portalirzplus1";
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
    /// Model specyfikacji do wysyłki do IRZplus - zgodny z formatem ARiMR
    /// </summary>
    public class SpecyfikacjaDoIRZplus
    {
        public int Id { get; set; }

        // Kolumna B - Hodowca (nazwa)
        public string Hodowca { get; set; }

        // Kolumna C - Id na produkcji (Id hodowcy w systemie)
        public string IdHodowcy { get; set; }

        // Kolumna D - IRZ PLUS (numer z tabeli Dostawcy.IRZPlus)
        public string IRZPlus { get; set; }

        // Kolumna E - Numer Partii
        public string NumerPartii { get; set; }

        // Kolumna F - Liczba Sztuk Drobiu (zdatne = DeclI1 - padłe - konfiskaty)
        public int LiczbaSztukDrobiu { get; set; }

        // Kolumna G - Typ Zdarzenia (zawsze "Przybycie do rzeźni i ubój")
        public string TypZdarzenia { get; set; } = "Przybycie do rzeźni i ubój";

        // Kolumna H - Data zdarzenia (data uboju)
        public DateTime DataZdarzenia { get; set; }

        // Kolumna I - Kraj Wywozu (zawsze "PL")
        public string KrajWywozu { get; set; } = "PL";

        // Kolumna J - Przyjęte z działalności (IRZPlus + "-001")
        public string PrzyjetaZDzialalnosci => string.IsNullOrEmpty(IRZPlus) ? "" : IRZPlus + "-001";

        // Kolumna K - nr.dok.Arimr (do ręcznego uzupełnienia po wprowadzeniu)
        public string NrDokArimr { get; set; }

        // Kolumna L - przybycie (do ręcznego uzupełnienia po wprowadzeniu)
        public string Przybycie { get; set; }

        // Kolumna M - padnięcia (do ręcznego uzupełnienia po wprowadzeniu)
        public string Padniecia { get; set; }

        // Dane pomocnicze (nie wyświetlane w głównej tabeli)
        public int SztukiWszystkie { get; set; }  // DeclI1
        public int SztukiPadle { get; set; }       // DeclI2
        public int SztukiKonfiskaty { get; set; } // DeclI3 + DeclI4 + DeclI5

        // KG - do wysylki do ARiMR i odpadow
        public decimal KgDoZaplaty { get; set; }  // PayWgt - Do zapl.
        public decimal KgKonfiskat { get; set; }  // KG konfiskat
        public decimal KgPadlych { get; set; }    // KG padlych

        public bool Wybrana { get; set; } = true;

        // Dla zgodności wstecznej
        public string DostawcaNazwa { get => Hodowca; set => Hodowca = value; }
        public string NumerSiedliska { get => IRZPlus; set => IRZPlus = value; }
        public int IloscSztuk { get => LiczbaSztukDrobiu; set => LiczbaSztukDrobiu = value; }
        public DateTime DataUboju { get => DataZdarzenia; set => DataZdarzenia = value; }
        public int IloscPadlych { get => SztukiPadle; set => SztukiPadle = value; }
        public string GatunekDrobiu { get; set; } = "KURCZAK";
        public decimal WagaNetto { get; set; }
        public string NumerRejestracyjny { get; set; }
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

    #region Odpady - Uboczne Produkty Pochodzenia Zwierzęcego (UPPZ)

    /// <summary>
    /// Model odpadu/ubocznego produktu pochodzenia zwierzęcego do zgłoszenia IRZplus
    /// Kategorie: KAT1, KAT2, KAT3
    /// </summary>
    public class OdpadDoIRZplus : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _wybrana = true;
        private string _kategoriaOdpadu;
        private string _rodzajOdpadu;
        private decimal _iloscKg;
        private string _odbiorcaNazwa;
        private string _odbiorcaNIP;
        private string _odbiorcaWetNr;
        private string _numerDokumentu;
        private string _numerRejestracyjny;
        private string _uwagi;

        public int Id { get; set; }
        public DateTime DataWydania { get; set; }

        public string KategoriaOdpadu
        {
            get => _kategoriaOdpadu;
            set { _kategoriaOdpadu = value; OnPropertyChanged(nameof(KategoriaOdpadu)); }
        }

        public string RodzajOdpadu
        {
            get => _rodzajOdpadu;
            set { _rodzajOdpadu = value; OnPropertyChanged(nameof(RodzajOdpadu)); }
        }

        public decimal IloscKg
        {
            get => _iloscKg;
            set { _iloscKg = value; OnPropertyChanged(nameof(IloscKg)); }
        }

        public string OdbiorcaNazwa
        {
            get => _odbiorcaNazwa;
            set { _odbiorcaNazwa = value; OnPropertyChanged(nameof(OdbiorcaNazwa)); }
        }

        public string OdbiorcaNIP
        {
            get => _odbiorcaNIP;
            set { _odbiorcaNIP = value; OnPropertyChanged(nameof(OdbiorcaNIP)); }
        }

        public string OdbiorcaWetNr
        {
            get => _odbiorcaWetNr;
            set { _odbiorcaWetNr = value; OnPropertyChanged(nameof(OdbiorcaWetNr)); }
        }

        public string NumerDokumentu
        {
            get => _numerDokumentu;
            set { _numerDokumentu = value; OnPropertyChanged(nameof(NumerDokumentu)); }
        }

        public string NumerRejestracyjny
        {
            get => _numerRejestracyjny;
            set { _numerRejestracyjny = value; OnPropertyChanged(nameof(NumerRejestracyjny)); }
        }

        public string Uwagi
        {
            get => _uwagi;
            set { _uwagi = value; OnPropertyChanged(nameof(Uwagi)); }
        }

        public bool Wybrana
        {
            get => _wybrana;
            set { _wybrana = value; OnPropertyChanged(nameof(Wybrana)); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Kategorie odpadów poubojowych zgodne z rozporządzeniem (WE) nr 1069/2009
    /// </summary>
    public static class KategorieOdpadow
    {
        public const string KAT1 = "KAT1";  // Materiał szczególnego ryzyka (SRM)
        public const string KAT2 = "KAT2";  // Obornik, treść przewodu pokarmowego
        public const string KAT3 = "KAT3";  // Uboczne produkty nadające się do przetworzenia

        public static readonly Dictionary<string, string> Opisy = new()
        {
            { "KAT1", "Materiał szczególnego ryzyka (SRM)" },
            { "KAT2", "Obornik, treść przewodu pokarmowego, padłe zwierzęta" },
            { "KAT3", "Uboczne produkty nadające się do przetworzenia (pierze, krew, tłuszcz)" }
        };

        public static readonly List<string> RodzajeKAT1 = new()
        {
            "Materiał SRM",
            "Produkty z BSE",
            "Inne KAT1"
        };

        public static readonly List<string> RodzajeKAT2 = new()
        {
            "Obornik",
            "Treść przewodu pokarmowego",
            "Padłe zwierzęta",
            "Konfiskaty weterynaryjne",
            "Inne KAT2"
        };

        public static readonly List<string> RodzajeKAT3 = new()
        {
            "Pierze",
            "Krew",
            "Wnętrzności",
            "Tłuszcz",
            "Nogi",
            "Głowy",
            "Skóra",
            "Kości",
            "Odpadki mięsne",
            "Inne KAT3"
        };

        public static List<string> GetRodzajeForKategoria(string kategoria)
        {
            return kategoria switch
            {
                KAT1 => RodzajeKAT1,
                KAT2 => RodzajeKAT2,
                KAT3 => RodzajeKAT3,
                _ => RodzajeKAT3
            };
        }
    }

    /// <summary>
    /// Model odbiorcy odpadów
    /// </summary>
    public class OdbiorcaOdpadow
    {
        public int Id { get; set; }
        public string Nazwa { get; set; }
        public string NIP { get; set; }
        public string NumerWeterynaryjny { get; set; }
        public string Adres { get; set; }
        public string Telefon { get; set; }
        public bool Aktywny { get; set; } = true;
    }

    #endregion
}
