using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Język oferty PDF
    /// </summary>
    public enum JezykOferty
    {
        Polski,
        English
    }

    /// <summary>
    /// Typ logo w PDF
    /// </summary>
    public enum TypLogo
    {
        Okragle,
        Dlugie
    }

    /// <summary>
    /// Model klienta dla oferty
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
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Model towaru dla oferty
    /// </summary>
    public class TowarOferta : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string Katalog { get; set; } = "";
        public string Opakowanie { get; set; } = "E2";

        // WAŻNE: Właściwość do wyświetlania w ComboBox
        public string NazwaZKodem => $"{Kod} - {Nazwa}";

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
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Parametry oferty
    /// </summary>
    public class ParametryOferty
    {
        public string TerminPlatnosci { get; set; } = "14 dni";
        public int DniPlatnosci { get; set; } = 14;
        public int DniWaznosci { get; set; } = 1;
        public string WalutaKonta { get; set; } = "PLN";
        public JezykOferty Jezyk { get; set; } = JezykOferty.Polski;
        public TypLogo TypLogo { get; set; } = TypLogo.Okragle;
        public bool PokazOpakowanie { get; set; } = true;
        public bool PokazCene { get; set; } = true;
        public bool PokazIlosc { get; set; } = false;
        public bool PokazTerminPlatnosci { get; set; } = true;
        public string WystawiajacyNazwa { get; set; } = "";
        public string WystawiajacyEmail { get; set; } = "";
        public string WystawiajacyTelefon { get; set; } = "";

        /// <summary>
        /// Czy oferta jest generowana bez danych odbiorcy (oferta ogólna)
        /// </summary>
        public bool BezOdbiorcy { get; set; } = false;
    }

    /// <summary>
    /// Dane konta bankowego
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

    /// <summary>
    /// Szablon zestawu towarów
    /// </summary>
    public class SzablonTowarow
    {
        public int Id { get; set; }
        public string Nazwa { get; set; } = "";
        public string Opis { get; set; } = "";
        public List<TowarSzablonu> Towary { get; set; } = new List<TowarSzablonu>();
        public int LiczbaTowrow => Towary?.Count ?? 0;
        public override string ToString() => Nazwa;
    }

    /// <summary>
    /// Towar w szablonie
    /// </summary>
    public class TowarSzablonu
    {
        public int TowarId { get; set; }
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string Katalog { get; set; } = "";
        public string Opakowanie { get; set; } = "E2";
        public decimal DomyslnaIlosc { get; set; } = 1;
        public decimal DomyslnaCena { get; set; } = 0;
    }

    /// <summary>
    /// Szablon parametrów oferty
    /// </summary>
    public class SzablonParametrow
    {
        public int Id { get; set; }
        public string Nazwa { get; set; } = "";
        public string TerminPlatnosci { get; set; } = "7 dni";
        public int DniPlatnosci { get; set; } = 7;
        public string WalutaKonta { get; set; } = "PLN";
        public JezykOferty Jezyk { get; set; } = JezykOferty.Polski;
        public TypLogo TypLogo { get; set; } = TypLogo.Okragle;
        public bool PokazOpakowanie { get; set; } = true;
        public bool PokazCene { get; set; } = true;
        public bool PokazIlosc { get; set; } = false;
        public bool PokazTerminPlatnosci { get; set; } = true;
        public string TransportTyp { get; set; } = "wlasny";
        public bool DodajNotkeOCenach { get; set; } = false;
        public string NotatkaCustom { get; set; } = "";
        public override string ToString() => Nazwa;
    }

    /// <summary>
    /// Szablon zestawu odbiorców - dla szybkiego wyboru grupy odbiorców
    /// </summary>
    public class SzablonOdbiorcow
    {
        public int Id { get; set; }
        public string Nazwa { get; set; } = "";
        public string Opis { get; set; } = "";
        public string OperatorId { get; set; } = ""; // ID handlowca - każdy ma swoje szablony
        public DateTime DataUtworzenia { get; set; } = DateTime.Now;
        public DateTime DataModyfikacji { get; set; } = DateTime.Now;
        public List<OdbiorcaSzablonu> Odbiorcy { get; set; } = new List<OdbiorcaSzablonu>();
        public int LiczbaOdbiorcow => Odbiorcy?.Count ?? 0;
        public override string ToString() => $"{Nazwa} ({LiczbaOdbiorcow} odb.)";
    }

    /// <summary>
    /// Odbiorca w szablonie
    /// </summary>
    public class OdbiorcaSzablonu
    {
        public string Id { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string NIP { get; set; } = "";
        public string Adres { get; set; } = "";
        public string KodPocztowy { get; set; } = "";
        public string Miejscowosc { get; set; } = "";
        public string Telefon { get; set; } = "";
        public string Email { get; set; } = "";
        public string OsobaKontaktowa { get; set; } = "";
        public string Zrodlo { get; set; } = "HANDEL"; // HANDEL, CRM, RECZNY
    }

    /// <summary>
    /// Tłumaczenie produktu na angielski
    /// </summary>
    public class TlumaczenieProduktu : INotifyPropertyChanged
    {
        private int _idTowaru;
        private string _kodTowaru = "";
        private string _nazwaPL = "";
        private string _nazwaEN = "";

        public int IdTowaru { get => _idTowaru; set { _idTowaru = value; OnPropertyChanged(); } }
        public string KodTowaru { get => _kodTowaru; set { _kodTowaru = value; OnPropertyChanged(); OnPropertyChanged(nameof(Kod)); } }

        // Alias dla kompatybilności
        public string Kod { get => _kodTowaru; set { _kodTowaru = value; OnPropertyChanged(); OnPropertyChanged(nameof(KodTowaru)); } }

        public string NazwaPL { get => _nazwaPL; set { _nazwaPL = value; OnPropertyChanged(); } }
        public string NazwaEN { get => _nazwaEN; set { _nazwaEN = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}