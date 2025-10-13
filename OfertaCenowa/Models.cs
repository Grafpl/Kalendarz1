using System.ComponentModel;

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Language enumeration for PDF generation
    /// </summary>
    public enum JezykOferty
    {
        Polski,
        English
    }

    /// <summary>
    /// Logo type enumeration for PDF generation
    /// </summary>
    public enum TypLogo
    {
        Okragle,    // logo.png
        Dlugie      // logo-2-green.png
    }

    /// <summary>
    /// Model klienta dla oferty / Client model for offer
    /// </summary>
    public class KlientOferta : INotifyPropertyChanged
    {
        public string Id { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string NIP { get; set; } = "";
        public string KodPocztowy { get; set; } = "";
        public string Miejscowosc { get; set; } = "";
        public string Adres { get; set; } = "";
        public string OsobaKontaktowa { get; set; } = "";
        public string Telefon { get; set; } = "";
        public bool CzyReczny { get; set; } = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Model towaru dla oferty / Product model for offer
    /// </summary>
    public class TowarOferta : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Kod { get; set; } = ""; // Kod towaru (krótki)
        public string Nazwa { get; set; } = ""; // Pełna nazwa towaru
        public string Katalog { get; set; } = "";
        public string Opakowanie { get; set; } = "E2";

        private decimal _ilosc;
        public decimal Ilosc
        {
            get => _ilosc;
            set
            {
                _ilosc = value;
                OnPropertyChanged(nameof(Ilosc));
                OnPropertyChanged(nameof(Wartosc));
            }
        }

        private decimal _cenaJednostkowa;
        public decimal CenaJednostkowa
        {
            get => _cenaJednostkowa;
            set
            {
                _cenaJednostkowa = value;
                OnPropertyChanged(nameof(CenaJednostkowa));
                OnPropertyChanged(nameof(Wartosc));
            }
        }

        public decimal Wartosc => Ilosc * CenaJednostkowa;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Parametry oferty / Offer parameters
    /// </summary>
    public class ParametryOferty
    {
        public string TerminPlatnosci { get; set; } = "7 dni";
        public int DniPlatnosci { get; set; } = 7;
        public string WalutaKonta { get; set; } = "PLN";
        public bool PokazTylkoCeny { get; set; } = false;
        public JezykOferty Jezyk { get; set; } = JezykOferty.Polski;
        public TypLogo TypLogo { get; set; } = TypLogo.Okragle;

        // Parametry kontroli widoczności w PDF / PDF visibility control parameters
        public bool PokazOpakowanie { get; set; } = true;
        public bool PokazCene { get; set; } = true;
        public bool PokazIlosc { get; set; } = true;
        public bool PokazTerminPlatnosci { get; set; } = true;
    }

    /// <summary>
    /// Dane konta bankowego / Bank account data
    /// </summary>
    public class DaneKonta
    {
        public string NumerKonta { get; set; } = "";
        public string IBAN { get; set; } = "";
        public string NazwaBanku { get; set; } = "";
        public string SWIFT { get; set; } = "";
        public string AdresBanku { get; set; } = "";
        public string Waluta { get; set; } = "";
    }
}