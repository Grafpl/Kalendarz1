using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace ZPSP.Sales.Models
{
    /// <summary>
    /// Produkt (towar) z bazy Handel
    /// </summary>
    public class Product : INotifyPropertyChanged
    {
        private int _id;
        private string _kod;
        private string _nazwa;
        private int _katalog;
        private string _jm;
        private decimal _cena;
        private bool _aktywny = true;

        #region Properties

        /// <summary>
        /// ID towaru w tabeli TW
        /// </summary>
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// Kod towaru (nazwa wyświetlana)
        /// </summary>
        public string Kod
        {
            get => _kod;
            set => SetProperty(ref _kod, value);
        }

        /// <summary>
        /// Pełna nazwa towaru
        /// </summary>
        public string Nazwa
        {
            get => _nazwa;
            set => SetProperty(ref _nazwa, value);
        }

        /// <summary>
        /// ID katalogu (67095 = Kurczak A, 67153 = Kurczak B)
        /// </summary>
        public int Katalog
        {
            get => _katalog;
            set => SetProperty(ref _katalog, value);
        }

        /// <summary>
        /// Jednostka miary (kg)
        /// </summary>
        public string JM
        {
            get => _jm;
            set => SetProperty(ref _jm, value);
        }

        /// <summary>
        /// Cena podstawowa
        /// </summary>
        public decimal Cena
        {
            get => _cena;
            set => SetProperty(ref _cena, value);
        }

        /// <summary>
        /// Czy produkt jest aktywny
        /// </summary>
        public bool Aktywny
        {
            get => _aktywny;
            set => SetProperty(ref _aktywny, value);
        }

        #endregion

        #region Calculated Properties

        /// <summary>
        /// Czy to Kurczak A (katalog 67095)
        /// </summary>
        public bool IsKurczakA => Katalog == 67095;

        /// <summary>
        /// Czy to Kurczak B (katalog 67153)
        /// </summary>
        public bool IsKurczakB => Katalog == 67153;

        /// <summary>
        /// Nazwa wyświetlana (Kod lub Nazwa jeśli Kod pusty)
        /// </summary>
        public string DisplayName => !string.IsNullOrWhiteSpace(Kod) ? Kod : Nazwa;

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
    /// Agregacja produktu na dany dzień (plan, fakt, zamówienia, wydania, bilans)
    /// </summary>
    public class ProductAggregation
    {
        /// <summary>
        /// ID produktu
        /// </summary>
        public int ProductId { get; set; }

        /// <summary>
        /// Nazwa produktu
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// Nazwa grupy (jeśli produkt jest scalony)
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// Procent udziału w puli Kurczaka B
        /// </summary>
        public decimal ProcentUdzialu { get; set; }

        /// <summary>
        /// Planowany przychód (z konfiguracji)
        /// </summary>
        public decimal Plan { get; set; }

        /// <summary>
        /// Faktyczny przychód (z PWP)
        /// </summary>
        public decimal Fakt { get; set; }

        /// <summary>
        /// Stan magazynowy
        /// </summary>
        public decimal Stan { get; set; }

        /// <summary>
        /// Suma zamówień
        /// </summary>
        public decimal Zamowienia { get; set; }

        /// <summary>
        /// Suma wydań (z WZ)
        /// </summary>
        public decimal Wydania { get; set; }

        /// <summary>
        /// Bilans: (Fakt lub Plan) + Stan - (Zamówienia lub Wydania)
        /// </summary>
        public decimal Bilans { get; set; }

        /// <summary>
        /// Liczba klientów którzy zamówili ten produkt
        /// </summary>
        public int LiczbaKlientow { get; set; }

        /// <summary>
        /// Czy użyto wartości faktycznej (fakt > 0)
        /// </summary>
        public bool UzytoFakt => Fakt > 0;

        /// <summary>
        /// Cel (fakt jeśli > 0, inaczej plan)
        /// </summary>
        public decimal Cel => UzytoFakt ? Fakt : Plan;

        /// <summary>
        /// Procent realizacji
        /// </summary>
        public decimal ProcentRealizacji => Cel > 0 ? Math.Round(Zamowienia / Cel * 100, 1) : 0;

        /// <summary>
        /// Czy bilans jest dodatni
        /// </summary>
        public bool BilansDodatni => Bilans >= 0;

        /// <summary>
        /// Ikona produktu na podstawie nazwy
        /// </summary>
        public string Ikona
        {
            get
            {
                var nazwa = ProductName ?? "";
                if (nazwa.Contains("Skrzydło", StringComparison.OrdinalIgnoreCase)) return "🍗";
                if (nazwa.Contains("Korpus", StringComparison.OrdinalIgnoreCase)) return "🍖";
                if (nazwa.Contains("Ćwiartka", StringComparison.OrdinalIgnoreCase)) return "🍗";
                if (nazwa.Contains("Filet", StringComparison.OrdinalIgnoreCase)) return "🥩";
                if (nazwa.Contains("Noga", StringComparison.OrdinalIgnoreCase)) return "🍗";
                if (nazwa.Contains("Kurczak", StringComparison.OrdinalIgnoreCase)) return "🐔";
                return "";
            }
        }
    }
}
