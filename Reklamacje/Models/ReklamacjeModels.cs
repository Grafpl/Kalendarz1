using System;
using System.ComponentModel;
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
}
