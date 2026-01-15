using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.Spotkania.Models
{
    /// <summary>
    /// Status spotkania
    /// </summary>
    public enum StatusSpotkania
    {
        Zaplanowane,
        WTrakcie,
        Zakonczone,
        Anulowane
    }

    /// <summary>
    /// Typ spotkania
    /// </summary>
    public enum TypSpotkania
    {
        Zespol,      // Spotkanie wewnętrzne zespołu
        Odbiorca,    // Spotkanie z odbiorcą
        Hodowca,     // Spotkanie z hodowcą
        Online       // Spotkanie online
    }

    /// <summary>
    /// Priorytet spotkania
    /// </summary>
    public enum PriorytetSpotkania
    {
        Niski,
        Normalny,
        Wysoki,
        Pilny
    }

    /// <summary>
    /// Status zaproszenia uczestnika
    /// </summary>
    public enum StatusZaproszenia
    {
        Oczekuje,
        Zaakceptowane,
        Odrzucone,
        Moze
    }

    /// <summary>
    /// Główny model spotkania
    /// </summary>
    public class SpotkanieModel : INotifyPropertyChanged
    {
        private long _spotkaniID;
        private string _tytul = string.Empty;
        private string? _opis;
        private DateTime _dataSpotkania;
        private DateTime? _dataZakonczenia;
        private int _czasTrwaniaMin = 60;
        private TypSpotkania _typSpotkania = TypSpotkania.Zespol;
        private string? _lokalizacja;
        private StatusSpotkania _status = StatusSpotkania.Zaplanowane;
        private string _organizatorID = string.Empty;
        private string? _organizatorNazwa;
        private string? _kontrahentID;
        private string? _kontrahentNazwa;
        private string? _kontrahentTyp;
        private string? _linkSpotkania;
        private string? _firefliesTranscriptID;
        private long? _notatkaID;
        private PriorytetSpotkania _priorytet = PriorytetSpotkania.Normalny;
        private string? _kategoria;
        private string _kolor = "#2196F3";
        private List<int> _przypomnienieMinuty = new List<int> { 1440, 60, 15 };

        public long SpotkaniID
        {
            get => _spotkaniID;
            set { _spotkaniID = value; OnPropertyChanged(); }
        }

        public string Tytul
        {
            get => _tytul;
            set { _tytul = value; OnPropertyChanged(); }
        }

        public string? Opis
        {
            get => _opis;
            set { _opis = value; OnPropertyChanged(); }
        }

        public DateTime DataSpotkania
        {
            get => _dataSpotkania;
            set { _dataSpotkania = value; OnPropertyChanged(); OnPropertyChanged(nameof(DataSpotkaniaTekst)); }
        }

        public DateTime? DataZakonczenia
        {
            get => _dataZakonczenia;
            set { _dataZakonczenia = value; OnPropertyChanged(); }
        }

        public int CzasTrwaniaMin
        {
            get => _czasTrwaniaMin;
            set { _czasTrwaniaMin = value; OnPropertyChanged(); OnPropertyChanged(nameof(CzasTrwaniaTekst)); }
        }

        public TypSpotkania TypSpotkania
        {
            get => _typSpotkania;
            set { _typSpotkania = value; OnPropertyChanged(); OnPropertyChanged(nameof(TypSpotkaniaDisplay)); }
        }

        public string? Lokalizacja
        {
            get => _lokalizacja;
            set { _lokalizacja = value; OnPropertyChanged(); }
        }

        public StatusSpotkania Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); }
        }

        public string OrganizatorID
        {
            get => _organizatorID;
            set { _organizatorID = value; OnPropertyChanged(); }
        }

        public string? OrganizatorNazwa
        {
            get => _organizatorNazwa;
            set { _organizatorNazwa = value; OnPropertyChanged(); }
        }

        public string? KontrahentID
        {
            get => _kontrahentID;
            set { _kontrahentID = value; OnPropertyChanged(); }
        }

        public string? KontrahentNazwa
        {
            get => _kontrahentNazwa;
            set { _kontrahentNazwa = value; OnPropertyChanged(); }
        }

        public string? KontrahentTyp
        {
            get => _kontrahentTyp;
            set { _kontrahentTyp = value; OnPropertyChanged(); }
        }

        public string? LinkSpotkania
        {
            get => _linkSpotkania;
            set { _linkSpotkania = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaLinkSpotkania)); }
        }

        public string? FirefliesTranscriptID
        {
            get => _firefliesTranscriptID;
            set { _firefliesTranscriptID = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaTranskrypcje)); }
        }

        public long? NotatkaID
        {
            get => _notatkaID;
            set { _notatkaID = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaNotatke)); }
        }

        public PriorytetSpotkania Priorytet
        {
            get => _priorytet;
            set { _priorytet = value; OnPropertyChanged(); OnPropertyChanged(nameof(PriorytetDisplay)); }
        }

        public string? Kategoria
        {
            get => _kategoria;
            set { _kategoria = value; OnPropertyChanged(); }
        }

        public string Kolor
        {
            get => _kolor;
            set { _kolor = value; OnPropertyChanged(); }
        }

        public List<int> PrzypomnienieMinuty
        {
            get => _przypomnienieMinuty;
            set { _przypomnienieMinuty = value; OnPropertyChanged(); }
        }

        // Kolekcje
        public List<UczestnikSpotkaniaModel> Uczestnicy { get; set; } = new List<UczestnikSpotkaniaModel>();
        public List<ZalacznikSpotkaniaModel> Zalaczniki { get; set; } = new List<ZalacznikSpotkaniaModel>();

        // Timestamps
        public DateTime DataUtworzenia { get; set; } = DateTime.Now;
        public DateTime? DataModyfikacji { get; set; }

        // Właściwości pomocnicze (computed)
        public string DataSpotkaniaTekst => DataSpotkania.ToString("dd.MM.yyyy HH:mm");
        public string CzasTrwaniaTekst => CzasTrwaniaMin < 60 ? $"{CzasTrwaniaMin} min" : $"{CzasTrwaniaMin / 60}h {CzasTrwaniaMin % 60}min";
        public bool MaLinkSpotkania => !string.IsNullOrWhiteSpace(LinkSpotkania);
        public bool MaTranskrypcje => !string.IsNullOrWhiteSpace(FirefliesTranscriptID);
        public bool MaNotatke => NotatkaID.HasValue;

        public string TypSpotkaniaDisplay => TypSpotkania switch
        {
            TypSpotkania.Zespol => "Zespół",
            TypSpotkania.Odbiorca => "Odbiorca",
            TypSpotkania.Hodowca => "Hodowca",
            TypSpotkania.Online => "Online",
            _ => "Nieznany"
        };

        public string StatusDisplay => Status switch
        {
            StatusSpotkania.Zaplanowane => "Zaplanowane",
            StatusSpotkania.WTrakcie => "W trakcie",
            StatusSpotkania.Zakonczone => "Zakończone",
            StatusSpotkania.Anulowane => "Anulowane",
            _ => "Nieznany"
        };

        public string PriorytetDisplay => Priorytet switch
        {
            PriorytetSpotkania.Niski => "Niski",
            PriorytetSpotkania.Normalny => "Normalny",
            PriorytetSpotkania.Wysoki => "Wysoki",
            PriorytetSpotkania.Pilny => "Pilny",
            _ => "Normalny"
        };

        public string IkonaPriorytetu => Priorytet switch
        {
            PriorytetSpotkania.Pilny => "🔴",
            PriorytetSpotkania.Wysoki => "🟠",
            PriorytetSpotkania.Normalny => "🟢",
            PriorytetSpotkania.Niski => "⚪",
            _ => "🟢"
        };

        public bool CzyNadchodzace => Status == StatusSpotkania.Zaplanowane && DataSpotkania > DateTime.Now;
        public bool CzyDzisiaj => DataSpotkania.Date == DateTime.Today;
        public bool CzyWKrotce => CzyNadchodzace && (DataSpotkania - DateTime.Now).TotalMinutes <= 60;

        public int MinutyDoSpotkania => CzyNadchodzace ? (int)(DataSpotkania - DateTime.Now).TotalMinutes : 0;

        public string CzasDoSpotkaniaDisplay
        {
            get
            {
                if (!CzyNadchodzace) return "";
                var diff = DataSpotkania - DateTime.Now;
                if (diff.TotalMinutes < 1) return "Za chwilę";
                if (diff.TotalMinutes < 60) return $"Za {(int)diff.TotalMinutes} min";
                if (diff.TotalHours < 24) return $"Za {(int)diff.TotalHours}h {diff.Minutes}min";
                return $"Za {(int)diff.TotalDays} dni";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Model uczestnika spotkania
    /// </summary>
    public class UczestnikSpotkaniaModel : INotifyPropertyChanged
    {
        private string _operatorID = string.Empty;
        private string? _operatorNazwa;
        private StatusZaproszenia _statusZaproszenia = StatusZaproszenia.Oczekuje;
        private bool _czyObowiazkowy;
        private bool _czyPowiadomiony;
        private bool _czyUczestniczyl;
        private string? _notatkaUczestnika;

        public long SpotkaniID { get; set; }

        public string OperatorID
        {
            get => _operatorID;
            set { _operatorID = value; OnPropertyChanged(); }
        }

        public string? OperatorNazwa
        {
            get => _operatorNazwa;
            set { _operatorNazwa = value; OnPropertyChanged(); }
        }

        public StatusZaproszenia StatusZaproszenia
        {
            get => _statusZaproszenia;
            set { _statusZaproszenia = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); OnPropertyChanged(nameof(IkonaStatusu)); }
        }

        public bool CzyObowiazkowy
        {
            get => _czyObowiazkowy;
            set { _czyObowiazkowy = value; OnPropertyChanged(); }
        }

        public bool CzyPowiadomiony
        {
            get => _czyPowiadomiony;
            set { _czyPowiadomiony = value; OnPropertyChanged(); }
        }

        public DateTime? DataPowiadomienia { get; set; }

        public bool CzyUczestniczyl
        {
            get => _czyUczestniczyl;
            set { _czyUczestniczyl = value; OnPropertyChanged(); }
        }

        public DateTime? DataDolaczenia { get; set; }

        public string? NotatkaUczestnika
        {
            get => _notatkaUczestnika;
            set { _notatkaUczestnika = value; OnPropertyChanged(); }
        }

        public string StatusDisplay => StatusZaproszenia switch
        {
            StatusZaproszenia.Oczekuje => "Oczekuje",
            StatusZaproszenia.Zaakceptowane => "Potwierdzone",
            StatusZaproszenia.Odrzucone => "Odrzucone",
            StatusZaproszenia.Moze => "Może",
            _ => "Nieznany"
        };

        public string IkonaStatusu => StatusZaproszenia switch
        {
            StatusZaproszenia.Zaakceptowane => "✓",
            StatusZaproszenia.Odrzucone => "✗",
            StatusZaproszenia.Moze => "?",
            _ => "○"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Model załącznika spotkania
    /// </summary>
    public class ZalacznikSpotkaniaModel
    {
        public long ZalacznikID { get; set; }
        public long SpotkaniID { get; set; }
        public string NazwaPliku { get; set; } = string.Empty;
        public string? SciezkaPliku { get; set; }
        public string? TypPliku { get; set; }
        public long RozmiarBajtow { get; set; }
        public string? Opis { get; set; }
        public string? DodanyPrzez { get; set; }
        public DateTime DataDodania { get; set; } = DateTime.Now;

        public string RozmiarDisplay
        {
            get
            {
                if (RozmiarBajtow < 1024) return $"{RozmiarBajtow} B";
                if (RozmiarBajtow < 1024 * 1024) return $"{RozmiarBajtow / 1024:F1} KB";
                return $"{RozmiarBajtow / 1024 / 1024:F1} MB";
            }
        }
    }

    /// <summary>
    /// DTO dla listy spotkań
    /// </summary>
    public class SpotkanieListItem
    {
        public long SpotkaniID { get; set; }
        public string Tytul { get; set; } = string.Empty;
        public DateTime DataSpotkania { get; set; }
        public int CzasTrwaniaMin { get; set; }
        public string TypSpotkania { get; set; } = "Zespół";
        public string Status { get; set; } = "Zaplanowane";
        public string? OrganizatorNazwa { get; set; }
        public string? KontrahentNazwa { get; set; }
        public string? Lokalizacja { get; set; }
        public string Priorytet { get; set; } = "Normalny";
        public string Kolor { get; set; } = "#2196F3";
        public int LiczbaUczestnikow { get; set; }
        public int LiczbaPotwierdzonych { get; set; }
        public bool MaNotatke { get; set; }
        public bool MaTranskrypcje { get; set; }
        public bool MaLink { get; set; }

        // Obliczane
        public string DataDisplay => DataSpotkania.ToString("dd.MM.yyyy HH:mm");
        public bool CzyNadchodzace => Status == "Zaplanowane" && DataSpotkania > DateTime.Now;
        public bool CzyDzisiaj => DataSpotkania.Date == DateTime.Today;

        public string UczestnicyDisplay => $"{LiczbaPotwierdzonych}/{LiczbaUczestnikow}";
    }

    /// <summary>
    /// Model dla kalendarza
    /// </summary>
    public class SpotkanieKalendarzItem
    {
        public long SpotkaniID { get; set; }
        public string Tytul { get; set; } = string.Empty;
        public DateTime DataSpotkania { get; set; }
        public DateTime? DataZakonczenia { get; set; }
        public string Kolor { get; set; } = "#2196F3";
        public string Status { get; set; } = "Zaplanowane";
        public string Priorytet { get; set; } = "Normalny";
        public string? Lokalizacja { get; set; }
        public bool MaLink { get; set; }
    }
}
