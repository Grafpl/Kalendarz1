using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZPSP.Sales.Models
{
    /// <summary>
    /// Kurs transportowy (TransportPL.Kurs)
    /// </summary>
    public class TransportKurs : INotifyPropertyChanged
    {
        private long _kursId;
        private DateTime _dataKursu;
        private string _trasa;
        private TimeSpan? _godzWyjazdu;
        private TimeSpan? _godzPowrotu;
        private string _status;
        private int? _kierowcaId;
        private string _kierowca;
        private string _telefonKierowcy;
        private int? _pojazdId;
        private string _rejestracja;
        private string _markaPojazdu;
        private string _modelPojazdu;
        private int _maxPalety;
        private string _uwagi;

        #region Properties

        /// <summary>
        /// ID kursu
        /// </summary>
        public long KursId
        {
            get => _kursId;
            set => SetProperty(ref _kursId, value);
        }

        /// <summary>
        /// Data kursu
        /// </summary>
        public DateTime DataKursu
        {
            get => _dataKursu;
            set => SetProperty(ref _dataKursu, value);
        }

        /// <summary>
        /// Nazwa trasy
        /// </summary>
        public string Trasa
        {
            get => _trasa;
            set => SetProperty(ref _trasa, value);
        }

        /// <summary>
        /// Godzina wyjazdu z firmy
        /// </summary>
        public TimeSpan? GodzWyjazdu
        {
            get => _godzWyjazdu;
            set => SetProperty(ref _godzWyjazdu, value);
        }

        /// <summary>
        /// Godzina powrotu do firmy
        /// </summary>
        public TimeSpan? GodzPowrotu
        {
            get => _godzPowrotu;
            set => SetProperty(ref _godzPowrotu, value);
        }

        /// <summary>
        /// Status kursu
        /// </summary>
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// ID kierowcy
        /// </summary>
        public int? KierowcaId
        {
            get => _kierowcaId;
            set => SetProperty(ref _kierowcaId, value);
        }

        /// <summary>
        /// Imię i nazwisko kierowcy
        /// </summary>
        public string Kierowca
        {
            get => _kierowca;
            set => SetProperty(ref _kierowca, value);
        }

        /// <summary>
        /// Telefon kierowcy
        /// </summary>
        public string TelefonKierowcy
        {
            get => _telefonKierowcy;
            set => SetProperty(ref _telefonKierowcy, value);
        }

        /// <summary>
        /// ID pojazdu
        /// </summary>
        public int? PojazdId
        {
            get => _pojazdId;
            set => SetProperty(ref _pojazdId, value);
        }

        /// <summary>
        /// Numer rejestracyjny pojazdu
        /// </summary>
        public string Rejestracja
        {
            get => _rejestracja;
            set => SetProperty(ref _rejestracja, value);
        }

        /// <summary>
        /// Marka pojazdu
        /// </summary>
        public string MarkaPojazdu
        {
            get => _markaPojazdu;
            set => SetProperty(ref _markaPojazdu, value);
        }

        /// <summary>
        /// Model pojazdu
        /// </summary>
        public string ModelPojazdu
        {
            get => _modelPojazdu;
            set => SetProperty(ref _modelPojazdu, value);
        }

        /// <summary>
        /// Maksymalna liczba palet H1
        /// </summary>
        public int MaxPalety
        {
            get => _maxPalety;
            set => SetProperty(ref _maxPalety, value);
        }

        /// <summary>
        /// Uwagi do kursu
        /// </summary>
        public string Uwagi
        {
            get => _uwagi;
            set => SetProperty(ref _uwagi, value);
        }

        #endregion

        #region Collections

        /// <summary>
        /// Ładunki na tym kursie
        /// </summary>
        public List<TransportLadunek> Ladunki { get; set; } = new List<TransportLadunek>();

        #endregion

        #region Calculated Properties

        /// <summary>
        /// Sformatowana godzina wyjazdu
        /// </summary>
        public string GodzWyjazduFormatted => GodzWyjazdu?.ToString(@"hh\:mm") ?? "";

        /// <summary>
        /// Sformatowana godzina powrotu
        /// </summary>
        public string GodzPowrotuFormatted => GodzPowrotu?.ToString(@"hh\:mm") ?? "";

        /// <summary>
        /// Nazwa pojazdu (marka model)
        /// </summary>
        public string NazwaPojazdu => $"{MarkaPojazdu} {ModelPojazdu}".Trim();

        /// <summary>
        /// Suma palet wszystkich ładunków
        /// </summary>
        public decimal SumaPalet
        {
            get
            {
                decimal suma = 0;
                foreach (var ladunek in Ladunki)
                    suma += ladunek.Palety;
                return suma;
            }
        }

        /// <summary>
        /// Czy pojazd jest pełny
        /// </summary>
        public bool IsPelny => MaxPalety > 0 && SumaPalet >= MaxPalety;

        /// <summary>
        /// Procent zapełnienia
        /// </summary>
        public decimal ProcentZapelnienia => MaxPalety > 0 ? Math.Round(SumaPalet / MaxPalety * 100, 1) : 0;

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

    /// <summary>
    /// Ładunek na kursie transportowym
    /// </summary>
    public class TransportLadunek
    {
        /// <summary>
        /// ID ładunku
        /// </summary>
        public long LadunekId { get; set; }

        /// <summary>
        /// ID kursu
        /// </summary>
        public long KursId { get; set; }

        /// <summary>
        /// Kolejność dostawy
        /// </summary>
        public int Kolejnosc { get; set; }

        /// <summary>
        /// Kod klienta (może być ZAM_123 dla zamówienia)
        /// </summary>
        public string KodKlienta { get; set; }

        /// <summary>
        /// ID zamówienia (jeśli KodKlienta = ZAM_xxx)
        /// </summary>
        public int? ZamowienieId { get; set; }

        /// <summary>
        /// Nazwa klienta
        /// </summary>
        public string NazwaKlienta { get; set; }

        /// <summary>
        /// Liczba palet H1
        /// </summary>
        public int Palety { get; set; }

        /// <summary>
        /// Liczba pojemników E2
        /// </summary>
        public int Pojemniki { get; set; }

        /// <summary>
        /// Waga ładunku w kg
        /// </summary>
        public decimal Waga { get; set; }

        /// <summary>
        /// Uwagi do ładunku
        /// </summary>
        public string Uwagi { get; set; }

        /// <summary>
        /// Adres dostawy
        /// </summary>
        public string AdresDostawy { get; set; }
    }
}
