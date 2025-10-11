// Plik: ZamowieniePozycja.cs
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1
{
    public class ZamowieniePozycja : INotifyPropertyChanged
    {
        private readonly WidokZamowienia _parentWindow;
        private bool _isRecalculating = false;
        private bool _e2, _folia;
        private decimal _palety, _pojemniki, _ilosc;
        public event PropertyChangedEventHandler? PropertyChanged;
        public ZamowieniePozycja(WidokZamowienia parent) { _parentWindow = parent; }
        public int Id { get; set; }
        public string Kod { get; set; } = "";
        public string Katalog { get; set; } = "";

        // NOWA WŁAŚCIWOŚĆ DO SORTOWANIA I STYLOWANIA WIERSZY
        public bool HasValue => Ilosc > 0;

        public bool Folia { get => _folia; set { _folia = value; OnPropertyChanged(); } }
        public bool E2 { get => _e2; set { if (_e2 != value) { _e2 = value; OnPropertyChanged(); PrzeliczWszystko(nameof(E2)); } } }
        public decimal Palety { get => _palety; set { if (_palety != value) { _palety = value; OnPropertyChanged(); PrzeliczWszystko(nameof(Palety)); } } }
        public decimal Pojemniki { get => _pojemniki; set { if (_pojemniki != value) { _pojemniki = value; OnPropertyChanged(); PrzeliczWszystko(nameof(Pojemniki)); } } }
        public decimal Ilosc
        {
            get => _ilosc;
            set
            {
                if (_ilosc != value)
                {
                    _ilosc = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasValue)); // Informuj o zmianie HasValue
                    PrzeliczWszystko(nameof(Ilosc));
                }
            }
        }

        private void PrzeliczWszystko(string zrodloZmiany)
        {
            if (_isRecalculating) return;
            _isRecalculating = true;
            _parentWindow.PrzeliczPozycje(this, zrodloZmiany);
            if (zrodloZmiany != nameof(Palety)) OnPropertyChanged(nameof(Palety));
            if (zrodloZmiany != nameof(Pojemniki)) OnPropertyChanged(nameof(Pojemniki));
            if (zrodloZmiany != nameof(Ilosc)) OnPropertyChanged(nameof(Ilosc));

            // Informuj o zmianie HasValue także przy zmianach pośrednich
            OnPropertyChanged(nameof(HasValue));

            _isRecalculating = false;
        }
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}