using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.MagazynLiczenie.Modele
{
    public class ProduktLiczenie : INotifyPropertyChanged
    {
        private int _produktId;
        private string _kodProduktu;
        private string _nazwaProduktu;
        private decimal _stanMagazynowy;
        private bool _jestZmodyfikowany;

        public int ProduktId
        {
            get => _produktId;
            set { _produktId = value; OnPropertyChanged(); }
        }

        public string KodProduktu
        {
            get => _kodProduktu;
            set { _kodProduktu = value; OnPropertyChanged(); }
        }

        public string NazwaProduktu
        {
            get => _nazwaProduktu;
            set { _nazwaProduktu = value; OnPropertyChanged(); }
        }

        public decimal StanMagazynowy
        {
            get => _stanMagazynowy;
            set
            {
                if (_stanMagazynowy != value)
                {
                    _stanMagazynowy = value;
                    JestZmodyfikowany = true;
                    OnPropertyChanged();
                }
            }
        }

        public bool JestZmodyfikowany
        {
            get => _jestZmodyfikowany;
            set { _jestZmodyfikowany = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}