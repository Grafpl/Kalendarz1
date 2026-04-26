using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kalendarz1.Reklamacje
{
    // ================================================================
    // Workflow V2 — stale dla statusow i kategorii zakladek
    // ================================================================
    public static class StatusyV2
    {
        public const string ZGLOSZONA = "ZGLOSZONA";
        public const string W_ANALIZIE = "W_ANALIZIE";
        public const string ZASADNA = "ZASADNA";
        public const string POWIAZANA = "POWIAZANA";
        public const string ZAMKNIETA = "ZAMKNIETA";
        public const string ODRZUCONA = "ODRZUCONA";

        public static string Etykieta(string status) => status switch
        {
            ZGLOSZONA => "Zgloszona",
            W_ANALIZIE => "W analizie",
            ZASADNA => "Zasadna",
            POWIAZANA => "Powiazana",
            ZAMKNIETA => "Zamknieta",
            ODRZUCONA => "Odrzucona",
            _ => status ?? ""
        };

        public static string KategoriaZakladki(string statusV2, bool wymagaUzupelnienia)
        {
            if (statusV2 == ZAMKNIETA || statusV2 == ODRZUCONA || statusV2 == POWIAZANA || statusV2 == ZASADNA)
                return "ZAMKNIETE";
            if (statusV2 == ZGLOSZONA || wymagaUzupelnienia)
                return "DO_AKCJI";
            // W_ANALIZIE = Przyjeta
            return "W_TOKU";
        }
    }

    public class ReklamacjaItem : INotifyPropertyChanged
    {
        private int _id;
        private DateTime _dataZgloszenia;
        private string _numerDokumentu;
        private string _nazwaKontrahenta;
        private string _opis;
        private decimal _sumaKg;
        private string _status;
        private string _statusV2;
        private string _zglaszajacy;
        private string _osobaRozpatrujaca;
        private string _typReklamacji;
        private string _priorytet;
        private int? _powiazanaReklamacjaId;
        private string _zrodloZgloszenia;
        private string _numerFakturyOryginalnej;
        private int? _idFakturyOryginalnej;
        private string _decyzjaJakosci;
        private bool _wymagaUzupelnienia;
        private string _handlowiec;

        public int Id { get => _id; set { _id = value; OnPropertyChanged(nameof(Id)); } }
        public DateTime DataZgloszenia { get => _dataZgloszenia; set { _dataZgloszenia = value; OnPropertyChanged(nameof(DataZgloszenia)); } }
        public string NumerDokumentu { get => _numerDokumentu; set { _numerDokumentu = value; OnPropertyChanged(nameof(NumerDokumentu)); } }
        public string NazwaKontrahenta { get => _nazwaKontrahenta; set { _nazwaKontrahenta = value; OnPropertyChanged(nameof(NazwaKontrahenta)); } }
        public string Opis { get => _opis; set { _opis = value; OnPropertyChanged(nameof(Opis)); } }
        public decimal SumaKg { get => _sumaKg; set { _sumaKg = value; OnPropertyChanged(nameof(SumaKg)); } }
        public string Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }
        public string StatusV2
        {
            get => _statusV2;
            set
            {
                _statusV2 = value;
                OnPropertyChanged(nameof(StatusV2));
                OnPropertyChanged(nameof(StatusV2Etykieta));
                OnPropertyChanged(nameof(KategoriaZakladki));
            }
        }
        public string StatusV2Etykieta =>
            // Korekta bez uzupelnienia info = "Oczekuje" (czeka na handlowca)
            (StatusV2 == StatusyV2.ZGLOSZONA && WymagaUzupelnienia)
                ? "Oczekuje"
                : StatusyV2.Etykieta(StatusV2);
        public string Zglaszajacy { get => _zglaszajacy; set { _zglaszajacy = value; OnPropertyChanged(nameof(Zglaszajacy)); } }
        public string OsobaRozpatrujaca { get => _osobaRozpatrujaca; set { _osobaRozpatrujaca = value; OnPropertyChanged(nameof(OsobaRozpatrujaca)); } }
        public string TypReklamacji { get => _typReklamacji; set { _typReklamacji = value; OnPropertyChanged(nameof(TypReklamacji)); } }
        public string Priorytet { get => _priorytet; set { _priorytet = value; OnPropertyChanged(nameof(Priorytet)); } }
        public int? PowiazanaReklamacjaId { get => _powiazanaReklamacjaId; set { _powiazanaReklamacjaId = value; OnPropertyChanged(nameof(PowiazanaReklamacjaId)); OnPropertyChanged(nameof(MaPowiazanie)); OnPropertyChanged(nameof(TekstPowiazania)); } }

        // Pola Workflow V2
        public string ZrodloZgloszenia { get => _zrodloZgloszenia; set { _zrodloZgloszenia = value; OnPropertyChanged(nameof(ZrodloZgloszenia)); OnPropertyChanged(nameof(ZrodloIkona)); } }
        public string NumerFakturyOryginalnej { get => _numerFakturyOryginalnej; set { _numerFakturyOryginalnej = value; OnPropertyChanged(nameof(NumerFakturyOryginalnej)); } }
        public int? IdFakturyOryginalnej { get => _idFakturyOryginalnej; set { _idFakturyOryginalnej = value; OnPropertyChanged(nameof(IdFakturyOryginalnej)); } }
        public string DecyzjaJakosci { get => _decyzjaJakosci; set { _decyzjaJakosci = value; OnPropertyChanged(nameof(DecyzjaJakosci)); } }
        public bool WymagaUzupelnienia
        {
            get => _wymagaUzupelnienia;
            set
            {
                _wymagaUzupelnienia = value;
                OnPropertyChanged(nameof(WymagaUzupelnienia));
                OnPropertyChanged(nameof(KategoriaZakladki));
                OnPropertyChanged(nameof(StatusV2Etykieta));
            }
        }

        public string KategoriaZakladki => StatusyV2.KategoriaZakladki(StatusV2, WymagaUzupelnienia);

        // Handlowiec przypisany do kontrahenta
        public string Handlowiec
        {
            get => _handlowiec;
            set { _handlowiec = value; OnPropertyChanged(nameof(Handlowiec)); OnPropertyChanged(nameof(HandlowiecInitials)); }
        }
        public string HandlowiecId { get; set; }
        public ImageSource HandlowiecAvatar { get; set; }
        public string HandlowiecInitials => FormRozpatrzenieWindow.GetInitials(Handlowiec);
        public SolidColorBrush HandlowiecAvatarBrush => FormRozpatrzenieWindow.GetAvatarBrush(Handlowiec);
        public Visibility HandlowiecAvatarPhotoVis => HandlowiecAvatar != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility HandlowiecVis => string.IsNullOrEmpty(Handlowiec) || Handlowiec == "-" ? Visibility.Collapsed : Visibility.Visible;

        // Widocznosc info o fakturze bazowej (dla korekt)
        public Visibility FakturaBazowaVis => !string.IsNullOrEmpty(NumerFakturyOryginalnej) ? Visibility.Visible : Visibility.Collapsed;

        public string ZrodloIkona => ZrodloZgloszenia switch
        {
            "Handlowiec" => "Handlowiec",
            "Kierowca" => "Kierowca",
            "Klient" => "Klient",
            "Symfonia" => "Korekta symf.",
            "Jakosc" => "Jakosc",
            _ => ZrodloZgloszenia ?? "??"
        };

        public SolidColorBrush ZrodloBgBrush => ZrodloZgloszenia switch
        {
            "Handlowiec" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD")),
            "Kierowca" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0")),
            "Klient" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3E5F5")),
            "Symfonia" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9")),
            "Jakosc" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEBEE")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECEFF1"))
        };

        public SolidColorBrush ZrodloFgBrush => ZrodloZgloszenia switch
        {
            "Handlowiec" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0")),
            "Kierowca" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100")),
            "Klient" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6A1B9A")),
            "Symfonia" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32")),
            "Jakosc" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#546E7A"))
        };

        // SLA
        public int DniOdZgloszenia => (int)Math.Floor((DateTime.Today - DataZgloszenia.Date).TotalDays);
        public bool JestZagrozonySLA => KategoriaZakladki == "DO_AKCJI" && DniOdZgloszenia >= 7;
        public bool JestKrytycznySLA => KategoriaZakladki == "DO_AKCJI" && DniOdZgloszenia >= 14;

        public bool MaPowiazanie => PowiazanaReklamacjaId.HasValue && PowiazanaReklamacjaId.Value > 0;
        public string TekstPowiazania => MaPowiazanie ? $"#{PowiazanaReklamacjaId}" : "Brak";

        // Avatar support
        public string ZglaszajacyId { get; set; }
        public string RozpatrujacyId { get; set; }
        public ImageSource ZglaszajacyAvatar { get; set; }
        public ImageSource RozpatrujacyAvatar { get; set; }
        public string ZglaszajacyInitials => FormRozpatrzenieWindow.GetInitials(Zglaszajacy);
        public SolidColorBrush ZglaszajacyAvatarBrush => FormRozpatrzenieWindow.GetAvatarBrush(Zglaszajacy);
        public string RozpatrujacyInitials => FormRozpatrzenieWindow.GetInitials(OsobaRozpatrujaca);
        public SolidColorBrush RozpatrujacyAvatarBrush => FormRozpatrzenieWindow.GetAvatarBrush(OsobaRozpatrujaca);
        public Visibility ZglaszajacyAvatarPhotoVis => ZglaszajacyAvatar != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility RozpatrujacyAvatarPhotoVis => RozpatrujacyAvatar != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility RozpatrujacyVis => string.IsNullOrEmpty(OsobaRozpatrujaca) ? Visibility.Collapsed : Visibility.Visible;

        // SLA — daty pomocnicze
        public DateTime? DataAnalizy { get; set; }

        // PRIORYTET KOLOR — kropka pokazywana w badge'u Status
        public SolidColorBrush PriorytetKropkaKolor => Priorytet switch
        {
            "Niski" => new SolidColorBrush(Color.FromRgb(0x95, 0xA5, 0xA6)),     // szary
            "Normalny" => new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)),  // niebieski
            "Wysoki" => new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12)),    // pomaranczowy
            "Krytyczny" => new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B)), // czerwony
            _ => new SolidColorBrush(Color.FromRgb(0xBD, 0xC3, 0xC7))
        };
        public string PriorytetTooltip => $"Priorytet: {Priorytet ?? "Normalny"}";

        // BULK OPERATIONS — checkbox per wiersz
        private bool _isBulkSelected;
        public bool IsBulkSelected
        {
            get => _isBulkSelected;
            set { _isBulkSelected = value; OnPropertyChanged(nameof(IsBulkSelected)); }
        }

        // MATCH HELPER — score dopasowania (0-100) gdy uzywane w dialogu Powiaz
        public int MatchScore { get; set; }
        public string MatchScoreText => MatchScore > 0 ? $"{MatchScore}%" : "—";
        public SolidColorBrush MatchScoreColor => MatchScore >= 90 ? new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60))
                                                : MatchScore >= 70 ? new SolidColorBrush(Color.FromRgb(0xF1, 0xC4, 0x0F))
                                                : MatchScore >= 50 ? new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22))
                                                : new SolidColorBrush(Color.FromRgb(0xBD, 0xC3, 0xC7));
        public string MatchBadge => MatchScore >= 90 ? "🎯 IDEALNY"
                                  : MatchScore >= 70 ? "✓ MOCNY"
                                  : MatchScore >= 50 ? "~ MOZLIWY"
                                  : "?";

        // ZDJECIA INLINE — miniatury widoczne w gridzie
        public ImageSource Miniatura1 { get; set; }
        public ImageSource Miniatura2 { get; set; }
        public ImageSource Miniatura3 { get; set; }
        public int LiczbaZdjec { get; set; }
        public Visibility Miniatura1Vis => Miniatura1 != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility Miniatura2Vis => Miniatura2 != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility Miniatura3Vis => Miniatura3 != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BrakZdjecVis => LiczbaZdjec == 0 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility WiecejZdjecVis => LiczbaZdjec > 3 ? Visibility.Visible : Visibility.Collapsed;
        public string WiecejZdjecText => LiczbaZdjec > 3 ? $"+{LiczbaZdjec - 3}" : "";
        public string ZdjeciaTooltip => LiczbaZdjec == 0
            ? "Brak zdjec — kliknij aby dodac"
            : LiczbaZdjec == 1 ? "1 zdjecie — kliknij aby zobaczyc"
            : $"{LiczbaZdjec} zdjec — kliknij aby zobaczyc galerie";

        // ZAKONCZYL — kto i kiedy podjal decyzje koncowa (Zatwierdz/Odrzuc/Zamknij)
        public string UserZakonczeniaId { get; set; }
        public string UserZakonczenia { get; set; }
        public DateTime? DataZakonczenia { get; set; }
        public ImageSource ZakonczylAvatar { get; set; }
        public string ZakonczylInitials => FormRozpatrzenieWindow.GetInitials(UserZakonczenia);
        public SolidColorBrush ZakonczylAvatarBrush => FormRozpatrzenieWindow.GetAvatarBrush(UserZakonczenia);
        public Visibility ZakonczylAvatarPhotoVis => ZakonczylAvatar != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ZakonczylVis => string.IsNullOrEmpty(UserZakonczenia) ? Visibility.Collapsed : Visibility.Visible;
        public string ZakonczylDecyzja => StatusV2 switch
        {
            "ZASADNA" => "✓ Zatwierdzona",
            "POWIAZANA" => "✓ Zatwierdzona",
            "ZAMKNIETA" => "🏁 Zamknieta",
            "ODRZUCONA" => "✗ Odrzucona",
            _ => ""
        };
        public string ZakonczylTooltip
        {
            get
            {
                if (string.IsNullOrEmpty(UserZakonczenia)) return null;
                string data = DataZakonczenia.HasValue ? DataZakonczenia.Value.ToString("yyyy-MM-dd HH:mm") : "?";
                return $"{ZakonczylDecyzja}\nprzez: {UserZakonczenia}\ndnia: {data}";
            }
        }

        // ============== SLA — DWA ZEGARY ==============
        // Konfigurowalne progi SLA per typ reklamacji
        private const int SLA_JAKOSC_GODZIN = 24;       // ZGLOSZONA -> W_ANALIZIE
        private const int SLA_ROZWIAZANIE_DNI = 7;       // ZGLOSZONA -> ZAMKNIETA (dni robocze)

        // ZEGAR JAKOSCI: ile zostalo do przyjecia / ile minelo od zgloszenia
        public TimeSpan SlaJakoscUplynelo
        {
            get
            {
                if (DataAnalizy.HasValue) return DataAnalizy.Value - DataZgloszenia;
                return DateTime.Now - DataZgloszenia;
            }
        }
        public TimeSpan SlaJakoscPozostalo => TimeSpan.FromHours(SLA_JAKOSC_GODZIN) - SlaJakoscUplynelo;
        public bool SlaJakoscZakonczony => DataAnalizy.HasValue;
        public string SlaJakoscEtykieta
        {
            get
            {
                if (SlaJakoscZakonczony)
                {
                    var t = SlaJakoscUplynelo;
                    return t.TotalHours < 1 ? $"✓ {(int)t.TotalMinutes}m"
                         : t.TotalHours < 24 ? $"✓ {(int)t.TotalHours}h"
                         : $"✓ {(int)t.TotalDays}d";
                }
                var pos = SlaJakoscPozostalo;
                if (pos.TotalSeconds < 0)
                    return $"🔥 +{(int)Math.Abs(pos.TotalHours)}h po";
                return pos.TotalHours < 1 ? $"⏰ {(int)pos.TotalMinutes}m"
                     : $"⏰ {(int)pos.TotalHours}h";
            }
        }
        public SolidColorBrush SlaJakoscKolor
        {
            get
            {
                if (SlaJakoscZakonczony) return new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)); // ciemny zielony
                var godz = SlaJakoscPozostalo.TotalHours;
                if (godz < 0) return new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)); // czerwony
                if (godz < 6) return new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22)); // pomaranczowy
                if (godz < 12) return new SolidColorBrush(Color.FromRgb(0xF1, 0xC4, 0x0F)); // zolty
                return new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)); // zielony
            }
        }
        public string SlaJakoscTooltip => SlaJakoscZakonczony
            ? $"ZEGAR JAKOSCI: zakonczony\nPrzyjecie po {SlaJakoscEtykieta.Replace("✓ ", "")} od zgloszenia\nSLA: {SLA_JAKOSC_GODZIN}h"
            : $"ZEGAR JAKOSCI: oczekiwanie na przyjecie\nSLA: {SLA_JAKOSC_GODZIN}h od zgloszenia\nPozostalo: {SlaJakoscEtykieta}";

        // ZEGAR ROZWIAZANIA: ile zostalo do zamkniecia
        public bool SlaRozwiazanyZakonczony => DataZakonczenia.HasValue
            || StatusV2 == "ZAMKNIETA" || StatusV2 == "ODRZUCONA"
            || StatusV2 == "ZASADNA" || StatusV2 == "POWIAZANA";
        public TimeSpan SlaRozwiazanyUplynelo
        {
            get
            {
                var koniec = DataZakonczenia ?? DateTime.Now;
                return koniec - DataZgloszenia;
            }
        }
        public TimeSpan SlaRozwiazanyPozostalo => TimeSpan.FromDays(SLA_ROZWIAZANIE_DNI) - SlaRozwiazanyUplynelo;
        public string SlaRozwiazanyEtykieta
        {
            get
            {
                if (SlaRozwiazanyZakonczony)
                {
                    var t = SlaRozwiazanyUplynelo;
                    return t.TotalHours < 24 ? $"✓ {(int)t.TotalHours}h" : $"✓ {(int)t.TotalDays}d";
                }
                var pos = SlaRozwiazanyPozostalo;
                if (pos.TotalSeconds < 0)
                    return $"🔥 +{(int)Math.Abs(pos.TotalDays)}d po";
                return pos.TotalDays < 1 ? $"⏰ {(int)pos.TotalHours}h" : $"⏰ {(int)pos.TotalDays}d";
            }
        }
        public SolidColorBrush SlaRozwiazanyKolor
        {
            get
            {
                if (SlaRozwiazanyZakonczony) return new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                var dni = SlaRozwiazanyPozostalo.TotalDays;
                if (dni < 0) return new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
                if (dni < 1) return new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22));
                if (dni < 3) return new SolidColorBrush(Color.FromRgb(0xF1, 0xC4, 0x0F));
                return new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
            }
        }
        public string SlaRozwiazanyTooltip => SlaRozwiazanyZakonczony
            ? $"ZEGAR ROZWIAZANIA: zakonczony\nCzas calkowity: {SlaRozwiazanyEtykieta.Replace("✓ ", "")}\nSLA: {SLA_ROZWIAZANIE_DNI} dni"
            : $"ZEGAR ROZWIAZANIA: w trakcie\nSLA: {SLA_ROZWIAZANIE_DNI} dni od zgloszenia\nPozostalo: {SlaRozwiazanyEtykieta}";

        // Sortowanie SLA — w godzinach (ujemne dla po terminie)
        public double SlaSortKey => SlaJakoscZakonczony ? double.MaxValue : SlaJakoscPozostalo.TotalHours;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Modele dialogow "Nowa reklamacja" / "Bez faktury"
    public class FakturaSprzedazyItem
    {
        public int Id { get; set; }
        public string NumerDokumentu { get; set; }
        public DateTime Data { get; set; }
        public int IdKontrahenta { get; set; }
        public string NazwaKontrahenta { get; set; }
        public decimal Wartosc { get; set; }
        public decimal SumaKg { get; set; }
        public string Handlowiec { get; set; }
        public int DniTemu { get; set; }
        public string EtykietaCzasu { get; set; }
        public Brush EtykietaTloKolor { get; set; }
        public Brush EtykietaTekstKolor { get; set; }
        public string OpisDoCombobox => $"{Data:dd.MM.yyyy}   {NumerDokumentu}   |   {SumaKg:#,##0.00} kg   |   {Wartosc:#,##0.00} zl";
    }

    public class KontrahentItem
    {
        public int Id { get; set; }
        public string Shortcut { get; set; }
        public string Name { get; set; }
        public override string ToString() => Shortcut;
    }

    public class TowarFaktury
    {
        public string Symbol { get; set; }
        public string Nazwa { get; set; }
        public decimal Waga { get; set; }
        public decimal Cena { get; set; }
        public decimal Wartosc { get; set; }
    }

    // Modele szczegolow reklamacji
    public class TowarSzczegoly
    {
        public int Lp { get; set; }
        public string Symbol { get; set; }
        public string Nazwa { get; set; }
        public decimal Waga { get; set; }
        public decimal Cena { get; set; }
        public decimal Wartosc { get; set; }
    }

    public class ZdjecieViewModel
    {
        public int Id { get; set; }
        public string NazwaPliku { get; set; }
        public string SciezkaPliku { get; set; }
        public byte[] DaneZdjecia { get; set; }
        public BitmapImage Miniatura { get; set; }
    }

    public class PartiaViewModel
    {
        public string NumerPartii { get; set; }
        public string Dostawca { get; set; }
        public DateTime? DataDodania { get; set; }
        public string DataDodaniaStr => DataDodania?.ToString("dd.MM.yyyy HH:mm") ?? "";
    }

    public class HistoriaViewModel
    {
        public DateTime DataZmiany { get; set; }
        public string DataZmianyStr => DataZmiany.ToString("dd.MM.yyyy HH:mm");
        public string PoprzedniStatus { get; set; }
        public string StatusNowy { get; set; }
        public string Uzytkownik { get; set; }
        public string Komentarz { get; set; }
        public string Inicjaly { get; set; }
        public SolidColorBrush AvatarColor { get; set; }
        public ImageSource AvatarPhoto { get; set; }
        public Visibility AvatarPhotoVisibility => AvatarPhoto != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility KomentarzVisibility => string.IsNullOrWhiteSpace(Komentarz) ? Visibility.Collapsed : Visibility.Visible;

        public SolidColorBrush KolorStatusu
        {
            get
            {
                string hex = FormRozpatrzenieWindow.GetStatusColor(StatusNowy ?? "");
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
        }
    }

    // Model komentarza wewnetrznego
    public class KomentarzViewModel
    {
        public int Id { get; set; }
        public int IdReklamacji { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Tresc { get; set; }
        public DateTime DataDodania { get; set; }
        public string DataStr => DataDodania.ToString("dd.MM.yyyy HH:mm");
        public string Inicjaly => FormRozpatrzenieWindow.GetInitials(UserName);
        public SolidColorBrush AvatarColor => FormRozpatrzenieWindow.GetAvatarBrush(UserName);
        public ImageSource AvatarPhoto { get; set; }
        public Visibility AvatarPhotoVisibility => AvatarPhoto != null ? Visibility.Visible : Visibility.Collapsed;
    }

    // ===========================================================
    // Smart Search Parser — rozpoznaje intent uzytkownika w polu szukaj
    // ===========================================================
    public class SmartSearchResult
    {
        public string FreeText { get; set; }
        public bool HasFreeText => !string.IsNullOrEmpty(FreeText);
        public bool OnlyMine { get; set; }
        public bool OnlyNew { get; set; }
        public bool OnlyVip { get; set; }
        public decimal? MinKg { get; set; }
        public decimal? MaxKg { get; set; }
        public DateTime? DataOd { get; set; }
        public DateTime? DataDo { get; set; }
        public string Partia { get; set; }
        public string Description
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (HasFreeText) parts.Add($"szukaj: '{FreeText}'");
                if (OnlyMine) parts.Add("tylko moje");
                if (OnlyNew) parts.Add("nowe (24h)");
                if (OnlyVip) parts.Add("VIP klienci");
                if (MinKg.HasValue) parts.Add($"kg≥{MinKg.Value:N0}");
                if (MaxKg.HasValue) parts.Add($"kg≤{MaxKg.Value:N0}");
                if (DataOd.HasValue) parts.Add($"od {DataOd.Value:dd.MM.yyyy}");
                if (DataDo.HasValue) parts.Add($"do {DataDo.Value:dd.MM.yyyy}");
                if (!string.IsNullOrEmpty(Partia)) parts.Add($"partia:{Partia}");
                return parts.Count == 0 ? "" : string.Join(" • ", parts);
            }
        }
    }

    public static class SmartSearchParser
    {
        public static SmartSearchResult Parse(string input)
        {
            var r = new SmartSearchResult();
            if (string.IsNullOrWhiteSpace(input)) return r;

            var tokens = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var freeWords = new System.Collections.Generic.List<string>();

            foreach (var raw in tokens)
            {
                var t = raw.Trim();
                if (string.IsNullOrEmpty(t)) continue;
                var lower = t.ToLowerInvariant();

                // slowa-flagi
                if (lower == "moje" || lower == "mine") { r.OnlyMine = true; continue; }
                if (lower == "nowe" || lower == "new") { r.OnlyNew = true; continue; }
                if (lower == "vip") { r.OnlyVip = true; continue; }

                // od:data — rozne formaty
                if (lower.StartsWith("od:") || lower.StartsWith("from:"))
                {
                    var v = t.Substring(t.IndexOf(':') + 1);
                    var dt = ParseDate(v);
                    if (dt.HasValue) { r.DataOd = dt.Value; continue; }
                }
                if (lower.StartsWith("do:") || lower.StartsWith("to:"))
                {
                    var v = t.Substring(t.IndexOf(':') + 1);
                    var dt = ParseDate(v);
                    if (dt.HasValue) { r.DataDo = dt.Value.AddDays(1).AddSeconds(-1); continue; }
                }
                // partia:X
                if (lower.StartsWith("partia:"))
                {
                    r.Partia = t.Substring(7);
                    continue;
                }
                // kg>X kg<X kg=X
                var kgMatch = System.Text.RegularExpressions.Regex.Match(lower, @"^kg([><]=?|=)([\d,\.]+)$");
                if (kgMatch.Success)
                {
                    if (decimal.TryParse(kgMatch.Groups[2].Value.Replace(',', '.'),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal kg))
                    {
                        var op = kgMatch.Groups[1].Value;
                        if (op == ">" || op == ">=") r.MinKg = kg;
                        else if (op == "<" || op == "<=") r.MaxKg = kg;
                        else { r.MinKg = kg; r.MaxKg = kg; }
                        continue;
                    }
                }
                // ">100" "<50" jako kg
                var numMatch = System.Text.RegularExpressions.Regex.Match(lower, @"^([><]=?)([\d,\.]+)$");
                if (numMatch.Success)
                {
                    if (decimal.TryParse(numMatch.Groups[2].Value.Replace(',', '.'),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal v))
                    {
                        var op = numMatch.Groups[1].Value;
                        if (op.StartsWith(">")) r.MinKg = v;
                        else r.MaxKg = v;
                        continue;
                    }
                }

                // pozostale slowa to free text
                freeWords.Add(t);
            }

            r.FreeText = freeWords.Count > 0 ? string.Join(" ", freeWords) : null;
            return r;
        }

        private static DateTime? ParseDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.ToLowerInvariant();
            var today = DateTime.Today;
            if (s == "dzis" || s == "today") return today;
            if (s == "wczoraj" || s == "yesterday") return today.AddDays(-1);
            if (s == "tydzien" || s == "week") return today.AddDays(-7);
            if (s == "miesiac" || s == "month") return today.AddMonths(-1);
            // YYYY-MM lub YYYY-MM-DD lub DD.MM.YYYY
            if (DateTime.TryParseExact(s, new[] { "yyyy-MM-dd", "yyyy-MM", "dd.MM.yyyy", "dd-MM-yyyy", "yyyy" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d)) return d;
            if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d2)) return d2;
            return null;
        }
    }
}
