using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace ZPSP.Sales.Models
{
    /// <summary>
    /// Pozycja zamówienia (ZamowieniaMiesoTowar)
    /// </summary>
    public class OrderItem : INotifyPropertyChanged
    {
        private int _zamowienieId;
        private int _kodTowaru;
        private string _nazwaTowaru;
        private decimal _ilosc;
        private string _cena;
        private int _pojemniki;
        private decimal _palety;
        private bool _e2;
        private bool _folia;
        private bool _hallal;
        private decimal _wydano;

        #region Properties

        /// <summary>
        /// ID zamówienia nadrzędnego
        /// </summary>
        public int ZamowienieId
        {
            get => _zamowienieId;
            set => SetProperty(ref _zamowienieId, value);
        }

        /// <summary>
        /// Kod towaru (ID z tabeli TW w Handel)
        /// </summary>
        public int KodTowaru
        {
            get => _kodTowaru;
            set => SetProperty(ref _kodTowaru, value);
        }

        /// <summary>
        /// Nazwa towaru (kod z TW)
        /// </summary>
        public string NazwaTowaru
        {
            get => _nazwaTowaru;
            set => SetProperty(ref _nazwaTowaru, value);
        }

        /// <summary>
        /// Zamówiona ilość w kg
        /// </summary>
        public decimal Ilosc
        {
            get => _ilosc;
            set
            {
                SetProperty(ref _ilosc, value);
                OnPropertyChanged(nameof(Roznica));
            }
        }

        /// <summary>
        /// Cena jednostkowa (jako string - może być pusta)
        /// </summary>
        public string Cena
        {
            get => _cena;
            set => SetProperty(ref _cena, value);
        }

        /// <summary>
        /// Cena jako decimal (do obliczeń)
        /// </summary>
        public decimal CenaDecimal
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Cena))
                    return 0;

                if (decimal.TryParse(Cena, out var result))
                    return result;

                return 0;
            }
        }

        /// <summary>
        /// Liczba pojemników dla tej pozycji
        /// </summary>
        public int Pojemniki
        {
            get => _pojemniki;
            set => SetProperty(ref _pojemniki, value);
        }

        /// <summary>
        /// Liczba palet dla tej pozycji
        /// </summary>
        public decimal Palety
        {
            get => _palety;
            set => SetProperty(ref _palety, value);
        }

        /// <summary>
        /// Czy używa pojemników E2 (40 szt/paleta)
        /// </summary>
        public bool E2
        {
            get => _e2;
            set => SetProperty(ref _e2, value);
        }

        /// <summary>
        /// Czy produkt pakowany w folię
        /// </summary>
        public bool Folia
        {
            get => _folia;
            set => SetProperty(ref _folia, value);
        }

        /// <summary>
        /// Czy produkt halal
        /// </summary>
        public bool Hallal
        {
            get => _hallal;
            set => SetProperty(ref _hallal, value);
        }

        /// <summary>
        /// Faktycznie wydana ilość (z WZ)
        /// </summary>
        public decimal Wydano
        {
            get => _wydano;
            set
            {
                SetProperty(ref _wydano, value);
                OnPropertyChanged(nameof(Roznica));
            }
        }

        #endregion

        #region Calculated Properties

        /// <summary>
        /// Różnica: Zamówiono - Wydano
        /// </summary>
        public decimal Roznica => Ilosc - Wydano;

        /// <summary>
        /// Wartość pozycji (ilość × cena)
        /// </summary>
        public decimal Wartosc => Ilosc * CenaDecimal;

        /// <summary>
        /// Czy pozycja ma uzupełnioną cenę
        /// </summary>
        public bool MaCene => !string.IsNullOrWhiteSpace(Cena) && CenaDecimal > 0;

        /// <summary>
        /// Tryb pojemników jako string
        /// </summary>
        public string TrybPojemnikow => E2 ? "E2" : "STD";

        /// <summary>
        /// Ikona informacji o cenie dla DataGrid
        /// </summary>
        public string CenaInfo => MaCene ? "✓" : "✗";

        /// <summary>
        /// Sformatowana nazwa z ikonami (folia, halal)
        /// </summary>
        public string NazwaZIkonami
        {
            get
            {
                var nazwa = NazwaTowaru ?? $"[{KodTowaru}]";
                if (Folia) nazwa = "📦 " + nazwa;
                if (Hallal) nazwa = "🔪 " + nazwa;
                return nazwa;
            }
        }

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
