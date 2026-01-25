using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Kalendarz1.Spotkania.Models
{
    #region Konfiguracja Fireflies

    /// <summary>
    /// Konfiguracja integracji z Fireflies.ai
    /// </summary>
    public class FirefliesKonfiguracja : INotifyPropertyChanged
    {
        private int _id;
        private string? _apiKey;
        private bool _autoImportNotatek = true;
        private bool _autoSynchronizacja = true;
        private int _interwalSynchronizacjiMin = 15;
        private DateTime? _ostatniaSynchronizacja;
        private string? _ostatniBladSynchronizacji;
        private bool _aktywna = true;

        public int ID
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string? ApiKey
        {
            get => _apiKey;
            set { _apiKey = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaKlucz)); }
        }

        public bool AutoImportNotatek
        {
            get => _autoImportNotatek;
            set { _autoImportNotatek = value; OnPropertyChanged(); }
        }

        public bool AutoSynchronizacja
        {
            get => _autoSynchronizacja;
            set { _autoSynchronizacja = value; OnPropertyChanged(); }
        }

        public int InterwalSynchronizacjiMin
        {
            get => _interwalSynchronizacjiMin;
            set { _interwalSynchronizacjiMin = value; OnPropertyChanged(); }
        }

        public DateTime? ImportujOdDaty { get; set; }
        public int MinimalnyCzasSpotkaniaSek { get; set; } = 60;

        public DateTime? OstatniaSynchronizacja
        {
            get => _ostatniaSynchronizacja;
            set { _ostatniaSynchronizacja = value; OnPropertyChanged(); OnPropertyChanged(nameof(OstatniaSynchronizacjaDisplay)); }
        }

        public string? OstatniBladSynchronizacji
        {
            get => _ostatniBladSynchronizacji;
            set { _ostatniBladSynchronizacji = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaBlad)); }
        }

        public bool Aktywna
        {
            get => _aktywna;
            set { _aktywna = value; OnPropertyChanged(); }
        }

        public DateTime DataUtworzenia { get; set; } = DateTime.Now;
        public DateTime? DataModyfikacji { get; set; }

        // Właściwości pomocnicze
        public bool MaKlucz => !string.IsNullOrWhiteSpace(ApiKey);
        public bool MaBlad => !string.IsNullOrWhiteSpace(OstatniBladSynchronizacji);

        public string OstatniaSynchronizacjaDisplay =>
            OstatniaSynchronizacja?.ToString("dd.MM.yyyy HH:mm") ?? "Nigdy";

        public string StatusDisplay
        {
            get
            {
                if (!MaKlucz) return "Brak klucza API";
                if (!Aktywna) return "Wyłączone";
                if (MaBlad) return "Błąd synchronizacji";
                return "Aktywne";
            }
        }

        public string StatusKolor => StatusDisplay switch
        {
            "Aktywne" => "#4CAF50",
            "Wyłączone" => "#9E9E9E",
            "Brak klucza API" => "#FF9800",
            _ => "#F44336"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    #endregion

    #region Transkrypcja z Fireflies

    /// <summary>
    /// Model transkrypcji z Fireflies
    /// </summary>
    public class FirefliesTranskrypcja : INotifyPropertyChanged
    {
        private long _transkrypcjaID;
        private string _firefliesID = string.Empty;
        private string? _tytul;
        private DateTime? _dataSpotkania;
        private int _czasTrwaniaSekundy;
        private string? _podsumowanie;
        private long? _spotkaniID;
        private long? _notatkaID;

        public long TranskrypcjaID
        {
            get => _transkrypcjaID;
            set { _transkrypcjaID = value; OnPropertyChanged(); }
        }

        public string FirefliesID
        {
            get => _firefliesID;
            set { _firefliesID = value; OnPropertyChanged(); }
        }

        public string? Tytul
        {
            get => _tytul;
            set { _tytul = value; OnPropertyChanged(); }
        }

        public DateTime? DataSpotkania
        {
            get => _dataSpotkania;
            set { _dataSpotkania = value; OnPropertyChanged(); OnPropertyChanged(nameof(DataSpotkaniaDisplay)); }
        }

        public int CzasTrwaniaSekundy
        {
            get => _czasTrwaniaSekundy;
            set { _czasTrwaniaSekundy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CzasTrwaniaDisplay)); }
        }

        // Uczestnicy (JSON)
        public List<FirefliesUczestnik> Uczestnicy { get; set; } = new List<FirefliesUczestnik>();
        public string? HostEmail { get; set; }

        // Transkrypcja
        public string? Transkrypcja { get; set; }
        public string? TranskrypcjaUrl { get; set; }
        public List<FirefliesSentence> Zdania { get; set; } = new List<FirefliesSentence>();

        // Analiza NLP
        public string? Podsumowanie
        {
            get => _podsumowanie;
            set { _podsumowanie = value; OnPropertyChanged(); }
        }

        public List<string> AkcjeDoDziałania { get; set; } = new List<string>();
        public List<string> SlowKluczowe { get; set; } = new List<string>();
        public List<string> NastepneKroki { get; set; } = new List<string>();

        // Sentyment
        public string? SentymentOgolny { get; set; }

        // Powiązania
        public long? SpotkaniID
        {
            get => _spotkaniID;
            set { _spotkaniID = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaSpotkanie)); }
        }

        public long? NotatkaID
        {
            get => _notatkaID;
            set { _notatkaID = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaNotatke)); }
        }

        // Status
        public string StatusImportu { get; set; } = "Zaimportowane";
        public string? BladImportu { get; set; }

        // Timestamps
        public DateTime DataImportu { get; set; } = DateTime.Now;
        public DateTime? DataModyfikacji { get; set; }

        // Właściwości pomocnicze
        public string DataSpotkaniaDisplay => DataSpotkania?.ToString("dd.MM.yyyy HH:mm") ?? "—";

        public string CzasTrwaniaDisplay
        {
            get
            {
                if (CzasTrwaniaSekundy < 60) return $"{CzasTrwaniaSekundy} sek";
                if (CzasTrwaniaSekundy < 3600) return $"{CzasTrwaniaSekundy / 60} min";
                return $"{CzasTrwaniaSekundy / 3600}h {(CzasTrwaniaSekundy % 3600) / 60}min";
            }
        }

        public bool MaSpotkanie => SpotkaniID.HasValue;
        public bool MaNotatke => NotatkaID.HasValue;
        public bool MaAkcje => AkcjeDoDziałania.Count > 0;
        public bool MaNastepneKroki => NastepneKroki.Count > 0;

        public int LiczbaUczestnikow => Uczestnicy.Count;
        public string UczestnicyDisplay => string.Join(", ", Uczestnicy.ConvertAll(u => u.DisplayName ?? u.Email ?? "Nieznany"));

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Uczestnik spotkania z Fireflies
    /// </summary>
    public class FirefliesUczestnik
    {
        public string? SpeakerId { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public string? PrzypisanyUserID { get; set; } // Mapowanie na UserID z systemu

        public string Nazwa => DisplayName ?? Email ?? SpeakerId ?? "Nieznany";
    }

    /// <summary>
    /// Zdanie z transkrypcji
    /// </summary>
    public class FirefliesSentence
    {
        public int Index { get; set; }
        public string? Text { get; set; }
        public string? SpeakerId { get; set; }
        public string? SpeakerName { get; set; }
        public double StartTime { get; set; } // w sekundach
        public double EndTime { get; set; }

        public string CzasDisplay => TimeSpan.FromSeconds(StartTime).ToString(@"mm\:ss");
    }

    /// <summary>
    /// Element listy transkrypcji
    /// </summary>
    public class FirefliesTranskrypcjaListItem
    {
        public long TranskrypcjaID { get; set; }
        public string FirefliesID { get; set; } = string.Empty;
        public string? Tytul { get; set; }
        public DateTime? DataSpotkania { get; set; }
        public int CzasTrwaniaSekundy { get; set; }
        public int LiczbaUczestnikow { get; set; }
        public bool MaSpotkanie { get; set; }
        public bool MaNotatke { get; set; }
        public string StatusImportu { get; set; } = "Zaimportowane";
        public DateTime DataImportu { get; set; }
        public string? Kategoria { get; set; }
        public List<string> UczestnicyLista { get; set; } = new();

        public string DataSpotkaniaDisplay => DataSpotkania?.ToString("dd.MM.yyyy HH:mm") ?? "-";
        public string CzasTrwaniaDisplay => CzasTrwaniaSekundy < 60 ? $"{CzasTrwaniaSekundy}s" :
            CzasTrwaniaSekundy < 3600 ? $"{CzasTrwaniaSekundy / 60}min" : $"{CzasTrwaniaSekundy / 3600}h";
        public string UczestnicyDisplay => UczestnicyLista.Any()
            ? string.Join(", ", UczestnicyLista.Take(3)) + (UczestnicyLista.Count > 3 ? $" +{UczestnicyLista.Count - 3}" : "")
            : "-";
    }

    #endregion

    #region GraphQL API Models

    /// <summary>
    /// Odpowiedź GraphQL
    /// </summary>
    public class GraphQLResponse<T>
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }

        [JsonPropertyName("errors")]
        public List<GraphQLError>? Errors { get; set; }

        public bool HasErrors => Errors != null && Errors.Count > 0;
    }

    /// <summary>
    /// Błąd GraphQL
    /// </summary>
    public class GraphQLError
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("path")]
        public List<string>? Path { get; set; }
    }

    /// <summary>
    /// Odpowiedź zapytania transcripts
    /// </summary>
    public class TranscriptsQueryResponse
    {
        [JsonPropertyName("transcripts")]
        public List<FirefliesTranscriptDto>? Transcripts { get; set; }
    }

    /// <summary>
    /// Odpowiedź zapytania transcript
    /// </summary>
    public class TranscriptQueryResponse
    {
        [JsonPropertyName("transcript")]
        public FirefliesTranscriptDto? Transcript { get; set; }
    }

    /// <summary>
    /// DTO transkrypcji z API Fireflies
    /// </summary>
    public class FirefliesTranscriptDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("date")]
        public long? Date { get; set; } // Unix timestamp w milisekundach

        [JsonPropertyName("duration")]
        public double? Duration { get; set; } // w sekundach (może być float z API)

        [JsonPropertyName("transcript_url")]
        public string? TranscriptUrl { get; set; }

        [JsonPropertyName("audio_url")]
        public string? AudioUrl { get; set; }

        [JsonPropertyName("video_url")]
        public string? VideoUrl { get; set; }

        [JsonPropertyName("host_email")]
        public string? HostEmail { get; set; }

        [JsonPropertyName("organizer_email")]
        public string? OrganizerEmail { get; set; }

        [JsonPropertyName("participants")]
        public List<string>? Participants { get; set; }

        // Właściwość pomocnicza - zwraca host lub organizer email
        public string? EmailOrganizatora => HostEmail ?? OrganizerEmail;

        [JsonPropertyName("sentences")]
        public List<FirefliesSentenceDto>? Sentences { get; set; }

        [JsonPropertyName("summary")]
        public FirefliesSummaryDto? Summary { get; set; }

        // Konwersja daty
        public DateTime? DateAsDateTime => Date.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(Date.Value).LocalDateTime
            : null;
    }

    /// <summary>
    /// DTO zdania z API
    /// </summary>
    public class FirefliesSentenceDto
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("speaker_id")]
        public int? SpeakerId { get; set; } // API zwraca int, nie string

        [JsonPropertyName("speaker_name")]
        public string? SpeakerName { get; set; }

        [JsonPropertyName("start_time")]
        public double StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public double EndTime { get; set; }

        // Właściwość pomocnicza - speaker_id jako string
        public string? SpeakerIdString => SpeakerId?.ToString();
    }

    /// <summary>
    /// DTO podsumowania z API (uproszczone - tylko pola dostępne bez Business plan)
    /// </summary>
    public class FirefliesSummaryDto
    {
        [JsonPropertyName("keywords")]
        public List<string>? Keywords { get; set; }

        [JsonPropertyName("overview")]
        public string? Overview { get; set; }
    }

    /// <summary>
    /// Odpowiedź user query
    /// </summary>
    public class UserQueryResponse
    {
        [JsonPropertyName("user")]
        public FirefliesUserDto? User { get; set; }
    }

    /// <summary>
    /// DTO użytkownika Fireflies
    /// </summary>
    public class FirefliesUserDto
    {
        [JsonPropertyName("user_id")]
        public string? UserId { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("minutes_consumed")]
        public double? MinutesConsumed { get; set; }

        [JsonPropertyName("is_admin")]
        public bool? IsAdmin { get; set; }

        // Właściwość pomocnicza do wyświetlania
        public string MinutesConsumedDisplay => MinutesConsumed.HasValue
            ? $"{MinutesConsumed.Value:F0}"
            : "0";
    }

    #endregion

    #region Sync Status

    /// <summary>
    /// Status synchronizacji z Fireflies
    /// </summary>
    public class FirefliesSyncStatus : INotifyPropertyChanged
    {
        private bool _trwaSynchronizacja;
        private string? _aktualnyEtap;
        private int _postep;
        private int _maksymalnyPostep;
        private string? _bladMessage;
        private DateTime? _ostatniaSynchronizacja;
        private int _zaimportowanoTranskrypcji;

        public bool TrwaSynchronizacja
        {
            get => _trwaSynchronizacja;
            set { _trwaSynchronizacja = value; OnPropertyChanged(); }
        }

        public string? AktualnyEtap
        {
            get => _aktualnyEtap;
            set { _aktualnyEtap = value; OnPropertyChanged(); }
        }

        public int Postep
        {
            get => _postep;
            set { _postep = value; OnPropertyChanged(); OnPropertyChanged(nameof(PostepProcent)); }
        }

        public int MaksymalnyPostep
        {
            get => _maksymalnyPostep;
            set { _maksymalnyPostep = value; OnPropertyChanged(); OnPropertyChanged(nameof(PostepProcent)); }
        }

        public string? BladMessage
        {
            get => _bladMessage;
            set { _bladMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaBlad)); }
        }

        public DateTime? OstatniaSynchronizacja
        {
            get => _ostatniaSynchronizacja;
            set { _ostatniaSynchronizacja = value; OnPropertyChanged(); }
        }

        public int ZaimportowanoTranskrypcji
        {
            get => _zaimportowanoTranskrypcji;
            set { _zaimportowanoTranskrypcji = value; OnPropertyChanged(); }
        }

        public int PostepProcent => MaksymalnyPostep > 0 ? (int)((double)Postep / MaksymalnyPostep * 100) : 0;
        public bool MaBlad => !string.IsNullOrWhiteSpace(BladMessage);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    #endregion
}
