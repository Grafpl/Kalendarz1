using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZPSP.Sales.Models
{
    /// <summary>
    /// Zamówienie mięsa (ZamowieniaMieso)
    /// </summary>
    public class Order : INotifyPropertyChanged
    {
        private int _id;
        private int _klientId;
        private string _odbiorca;
        private string _handlowiec;
        private decimal _iloscZamowiona;
        private decimal _iloscFaktyczna;
        private decimal _roznica;
        private int _pojemniki;
        private decimal _palety;
        private string _trybE2;
        private DateTime _dataPrzyjecia;
        private string _godzinaPrzyjecia;
        private string _terminOdbioru;
        private DateTime? _dataUboju;
        private string _utworzonePrzez;
        private string _status;
        private bool _maNotatke;
        private bool _maFolie;
        private bool _maHallal;
        private bool _czyMaCeny;
        private decimal _sredniaCena;
        private string _uwagi;
        private long? _transportKursId;
        private bool _czyZrealizowane;
        private DateTime? _dataWydania;
        private string _waluta = "PLN";

        #region Properties

        /// <summary>
        /// ID zamówienia
        /// </summary>
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// ID klienta (kontrahenta) z bazy Handel
        /// </summary>
        public int KlientId
        {
            get => _klientId;
            set => SetProperty(ref _klientId, value);
        }

        /// <summary>
        /// Nazwa odbiorcy (Shortcut z STContractors)
        /// </summary>
        public string Odbiorca
        {
            get => _odbiorca;
            set => SetProperty(ref _odbiorca, value);
        }

        /// <summary>
        /// Handlowiec przypisany do klienta
        /// </summary>
        public string Handlowiec
        {
            get => _handlowiec;
            set => SetProperty(ref _handlowiec, value);
        }

        /// <summary>
        /// Suma zamówionych kilogramów
        /// </summary>
        public decimal IloscZamowiona
        {
            get => _iloscZamowiona;
            set => SetProperty(ref _iloscZamowiona, value);
        }

        /// <summary>
        /// Suma wydanych kilogramów (z WZ)
        /// </summary>
        public decimal IloscFaktyczna
        {
            get => _iloscFaktyczna;
            set => SetProperty(ref _iloscFaktyczna, value);
        }

        /// <summary>
        /// Różnica: Zamówiono - Wydano
        /// </summary>
        public decimal Roznica
        {
            get => _roznica;
            set => SetProperty(ref _roznica, value);
        }

        /// <summary>
        /// Liczba pojemników E2
        /// </summary>
        public int Pojemniki
        {
            get => _pojemniki;
            set => SetProperty(ref _pojemniki, value);
        }

        /// <summary>
        /// Liczba palet H1
        /// </summary>
        public decimal Palety
        {
            get => _palety;
            set => SetProperty(ref _palety, value);
        }

        /// <summary>
        /// Tryb pojemników: "E2 (40)" lub "STD (36)"
        /// </summary>
        public string TrybE2
        {
            get => _trybE2;
            set => SetProperty(ref _trybE2, value);
        }

        /// <summary>
        /// Data i godzina przyjazdu
        /// </summary>
        public DateTime DataPrzyjecia
        {
            get => _dataPrzyjecia;
            set => SetProperty(ref _dataPrzyjecia, value);
        }

        /// <summary>
        /// Godzina przyjazdu jako string (HH:mm)
        /// </summary>
        public string GodzinaPrzyjecia
        {
            get => _godzinaPrzyjecia;
            set => SetProperty(ref _godzinaPrzyjecia, value);
        }

        /// <summary>
        /// Sformatowany termin odbioru
        /// </summary>
        public string TerminOdbioru
        {
            get => _terminOdbioru;
            set => SetProperty(ref _terminOdbioru, value);
        }

        /// <summary>
        /// Data uboju (produkcji)
        /// </summary>
        public DateTime? DataUboju
        {
            get => _dataUboju;
            set => SetProperty(ref _dataUboju, value);
        }

        /// <summary>
        /// Informacja o tym kto i kiedy utworzył zamówienie
        /// </summary>
        public string UtworzonePrzez
        {
            get => _utworzonePrzez;
            set => SetProperty(ref _utworzonePrzez, value);
        }

        /// <summary>
        /// Status zamówienia: Nowe, Zrealizowane, Anulowane, Wydanie bez zamówienia
        /// </summary>
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// Czy zamówienie ma notatkę
        /// </summary>
        public bool MaNotatke
        {
            get => _maNotatke;
            set => SetProperty(ref _maNotatke, value);
        }

        /// <summary>
        /// Czy zamówienie zawiera produkty z folią
        /// </summary>
        public bool MaFolie
        {
            get => _maFolie;
            set => SetProperty(ref _maFolie, value);
        }

        /// <summary>
        /// Czy zamówienie zawiera produkty halal
        /// </summary>
        public bool MaHallal
        {
            get => _maHallal;
            set => SetProperty(ref _maHallal, value);
        }

        /// <summary>
        /// Czy wszystkie pozycje mają uzupełnioną cenę
        /// </summary>
        public bool CzyMaCeny
        {
            get => _czyMaCeny;
            set => SetProperty(ref _czyMaCeny, value);
        }

        /// <summary>
        /// Średnia ważona cena produktów
        /// </summary>
        public decimal SredniaCena
        {
            get => _sredniaCena;
            set => SetProperty(ref _sredniaCena, value);
        }

        /// <summary>
        /// Uwagi do zamówienia
        /// </summary>
        public string Uwagi
        {
            get => _uwagi;
            set => SetProperty(ref _uwagi, value);
        }

        /// <summary>
        /// ID kursu transportowego (jeśli przypisany)
        /// </summary>
        public long? TransportKursId
        {
            get => _transportKursId;
            set => SetProperty(ref _transportKursId, value);
        }

        /// <summary>
        /// Czy zamówienie zostało zrealizowane na produkcji
        /// </summary>
        public bool CzyZrealizowane
        {
            get => _czyZrealizowane;
            set => SetProperty(ref _czyZrealizowane, value);
        }

        /// <summary>
        /// Data wydania WZ
        /// </summary>
        public DateTime? DataWydania
        {
            get => _dataWydania;
            set => SetProperty(ref _dataWydania, value);
        }

        /// <summary>
        /// Waluta zamówienia
        /// </summary>
        public string Waluta
        {
            get => _waluta;
            set => SetProperty(ref _waluta, value);
        }

        #endregion

        #region Calculated Properties

        /// <summary>
        /// Czy zamówienie jest przypisane do transportu
        /// </summary>
        public bool MaTransport => TransportKursId.HasValue;

        /// <summary>
        /// Ikona informacji o cenie
        /// </summary>
        public string CenaInfo => CzyMaCeny ? "✓" : "✗";

        /// <summary>
        /// Procent realizacji zamówienia
        /// </summary>
        public decimal ProcentRealizacji => IloscZamowiona > 0
            ? Math.Round(IloscFaktyczna / IloscZamowiona * 100, 1)
            : 0;

        /// <summary>
        /// Czy zamówienie jest anulowane
        /// </summary>
        public bool IsAnulowane => Status == "Anulowane";

        /// <summary>
        /// Czy to jest wydanie bez zamówienia
        /// </summary>
        public bool IsWydanieBezZamowienia => Status == "Wydanie bez zamówienia";

        #endregion

        #region Pozycje

        /// <summary>
        /// Pozycje zamówienia
        /// </summary>
        public List<OrderItem> Pozycje { get; set; } = new List<OrderItem>();

        /// <summary>
        /// Sumy per grupa towarowa (dla kolumn dynamicznych)
        /// </summary>
        public Dictionary<string, decimal> SumyPerGrupa { get; set; } = new Dictionary<string, decimal>();

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}
