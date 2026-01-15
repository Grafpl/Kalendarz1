using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.Spotkania.Models
{
    /// <summary>
    /// Typ powiadomienia
    /// </summary>
    public enum TypNotyfikacji
    {
        Zaproszenie,          // Nowe zaproszenie na spotkanie
        Przypomnienie24h,     // 24 godziny przed
        Przypomnienie1h,      // 1 godzina przed
        Przypomnienie15m,     // 15 minut przed
        Przypomnienie5m,      // 5 minut przed
        Zmiana,               // Zmiana w spotkaniu
        Anulowanie,           // Spotkanie anulowane
        AkceptacjaZaproszenia, // Ktoś zaakceptował zaproszenie
        OdrzucenieZaproszenia, // Ktoś odrzucił zaproszenie
        NowaTranskrypcja,     // Dostępna nowa transkrypcja z Fireflies
        NowaNotatka           // Dodano notatkę do spotkania
    }

    /// <summary>
    /// Model powiadomienia
    /// </summary>
    public class NotyfikacjaModel : INotifyPropertyChanged
    {
        private long _notyfikacjaID;
        private long _spotkaniID;
        private string _operatorID = string.Empty;
        private TypNotyfikacji _typNotyfikacji;
        private string? _tytul;
        private string? _tresc;
        private DateTime? _spotkanieDataSpotkania;
        private string? _spotkanieTytul;
        private bool _czyPrzeczytana;
        private DateTime? _dataPrzeczytania;
        private string? _linkAkcji;
        private DateTime _dataUtworzenia = DateTime.Now;

        public long NotyfikacjaID
        {
            get => _notyfikacjaID;
            set { _notyfikacjaID = value; OnPropertyChanged(); }
        }

        public long SpotkaniID
        {
            get => _spotkaniID;
            set { _spotkaniID = value; OnPropertyChanged(); }
        }

        public string OperatorID
        {
            get => _operatorID;
            set { _operatorID = value; OnPropertyChanged(); }
        }

        public TypNotyfikacji TypNotyfikacji
        {
            get => _typNotyfikacji;
            set { _typNotyfikacji = value; OnPropertyChanged(); OnPropertyChanged(nameof(Ikona)); OnPropertyChanged(nameof(TypDisplay)); }
        }

        public string? Tytul
        {
            get => _tytul;
            set { _tytul = value; OnPropertyChanged(); }
        }

        public string? Tresc
        {
            get => _tresc;
            set { _tresc = value; OnPropertyChanged(); }
        }

        public DateTime? SpotkanieDataSpotkania
        {
            get => _spotkanieDataSpotkania;
            set { _spotkanieDataSpotkania = value; OnPropertyChanged(); OnPropertyChanged(nameof(CzasDoSpotkania)); }
        }

        public string? SpotkanieTytul
        {
            get => _spotkanieTytul;
            set { _spotkanieTytul = value; OnPropertyChanged(); }
        }

        public bool CzyPrzeczytana
        {
            get => _czyPrzeczytana;
            set { _czyPrzeczytana = value; OnPropertyChanged(); }
        }

        public DateTime? DataPrzeczytania
        {
            get => _dataPrzeczytania;
            set { _dataPrzeczytania = value; OnPropertyChanged(); }
        }

        public string? LinkAkcji
        {
            get => _linkAkcji;
            set { _linkAkcji = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaAkcje)); }
        }

        public DateTime DataUtworzenia
        {
            get => _dataUtworzenia;
            set { _dataUtworzenia = value; OnPropertyChanged(); OnPropertyChanged(nameof(CzasOdUtworzenia)); }
        }

        public DateTime? DataWygasniecia { get; set; }

        // Dodatkowe dane ze spotkania
        public string? LinkSpotkania { get; set; }
        public string? Lokalizacja { get; set; }
        public int MinutyDoSpotkania { get; set; }

        // Właściwości obliczane
        public bool MaAkcje => !string.IsNullOrWhiteSpace(LinkAkcji) || !string.IsNullOrWhiteSpace(LinkSpotkania);

        public string Ikona => TypNotyfikacji switch
        {
            TypNotyfikacji.Zaproszenie => "📩",
            TypNotyfikacji.Przypomnienie24h => "📅",
            TypNotyfikacji.Przypomnienie1h => "⏰",
            TypNotyfikacji.Przypomnienie15m => "🔔",
            TypNotyfikacji.Przypomnienie5m => "🚨",
            TypNotyfikacji.Zmiana => "✏️",
            TypNotyfikacji.Anulowanie => "❌",
            TypNotyfikacji.AkceptacjaZaproszenia => "✅",
            TypNotyfikacji.OdrzucenieZaproszenia => "🚫",
            TypNotyfikacji.NowaTranskrypcja => "📝",
            TypNotyfikacji.NowaNotatka => "📋",
            _ => "🔔"
        };

        public string TypDisplay => TypNotyfikacji switch
        {
            TypNotyfikacji.Zaproszenie => "Zaproszenie",
            TypNotyfikacji.Przypomnienie24h => "Przypomnienie (24h)",
            TypNotyfikacji.Przypomnienie1h => "Przypomnienie (1h)",
            TypNotyfikacji.Przypomnienie15m => "Przypomnienie (15 min)",
            TypNotyfikacji.Przypomnienie5m => "Przypomnienie (5 min)",
            TypNotyfikacji.Zmiana => "Zmiana spotkania",
            TypNotyfikacji.Anulowanie => "Anulowanie",
            TypNotyfikacji.AkceptacjaZaproszenia => "Akceptacja",
            TypNotyfikacji.OdrzucenieZaproszenia => "Odrzucenie",
            TypNotyfikacji.NowaTranskrypcja => "Transkrypcja",
            TypNotyfikacji.NowaNotatka => "Notatka",
            _ => "Powiadomienie"
        };

        public string CzasOdUtworzenia
        {
            get
            {
                var diff = DateTime.Now - DataUtworzenia;
                if (diff.TotalMinutes < 1) return "Przed chwilą";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min temu";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h temu";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} dni temu";
                return DataUtworzenia.ToString("dd.MM.yyyy");
            }
        }

        public string? CzasDoSpotkania
        {
            get
            {
                if (!SpotkanieDataSpotkania.HasValue) return null;
                var diff = SpotkanieDataSpotkania.Value - DateTime.Now;
                if (diff.TotalMinutes < 0) return "Już było";
                if (diff.TotalMinutes < 1) return "Teraz!";
                if (diff.TotalMinutes < 60) return $"Za {(int)diff.TotalMinutes} min";
                if (diff.TotalHours < 24) return $"Za {(int)diff.TotalHours}h";
                return $"Za {(int)diff.TotalDays} dni";
            }
        }

        public bool CzyPilne => TypNotyfikacji == TypNotyfikacji.Przypomnienie5m ||
                                TypNotyfikacji == TypNotyfikacji.Przypomnienie15m ||
                                (SpotkanieDataSpotkania.HasValue && (SpotkanieDataSpotkania.Value - DateTime.Now).TotalMinutes <= 30);

        public string KolorTla => CzyPilne ? "#FFEBEE" : (CzyPrzeczytana ? "#FAFAFA" : "#E3F2FD");

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Licznik powiadomień dla dzwonka
    /// </summary>
    public class NotyfikacjeLicznik : INotifyPropertyChanged
    {
        private int _nieprzeczytane;
        private int _pilne;
        private bool _maNoweNotyfikacje;

        public int Nieprzeczytane
        {
            get => _nieprzeczytane;
            set
            {
                _nieprzeczytane = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LicznikDisplay));
                OnPropertyChanged(nameof(MaNotyfikacje));
            }
        }

        public int Pilne
        {
            get => _pilne;
            set { _pilne = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaPilne)); }
        }

        public bool MaNoweNotyfikacje
        {
            get => _maNoweNotyfikacje;
            set { _maNoweNotyfikacje = value; OnPropertyChanged(); }
        }

        public bool MaNotyfikacje => Nieprzeczytane > 0;
        public bool MaPilne => Pilne > 0;
        public string LicznikDisplay => Nieprzeczytane > 99 ? "99+" : Nieprzeczytane.ToString();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Ustawienia powiadomień użytkownika
    /// </summary>
    public class UstawieniaPowiadomien
    {
        public bool PowiadomieniaWlaczone { get; set; } = true;
        public bool Przypomnienie24h { get; set; } = true;
        public bool Przypomnienie1h { get; set; } = true;
        public bool Przypomnienie15m { get; set; } = true;
        public bool Przypomnienie5m { get; set; } = true;
        public bool DzwiekPowiadomienia { get; set; } = true;
        public bool PowiadomieniaSystemowe { get; set; } = true; // Windows toast notifications
    }
}
