using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace ZPSP.Sales.Models
{
    /// <summary>
    /// Kontrahent z bazy Handel (STContractors)
    /// </summary>
    public class Customer : INotifyPropertyChanged
    {
        private int _id;
        private string _shortcut;
        private string _nazwa;
        private string _handlowiec;
        private string _nip;
        private string _adres;
        private string _miasto;
        private string _kodPocztowy;
        private string _telefon;
        private string _email;
        private int? _terminPlatnosci;
        private decimal _limitKredytowy;
        private decimal _saldoNaleznosci;
        private bool _aktywny = true;

        #region Properties

        /// <summary>
        /// ID kontrahenta w STContractors
        /// </summary>
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// Skrócona nazwa (Shortcut)
        /// </summary>
        public string Shortcut
        {
            get => _shortcut;
            set => SetProperty(ref _shortcut, value);
        }

        /// <summary>
        /// Pełna nazwa firmy
        /// </summary>
        public string Nazwa
        {
            get => _nazwa;
            set => SetProperty(ref _nazwa, value);
        }

        /// <summary>
        /// Handlowiec przypisany do klienta (CDim_Handlowiec_Val)
        /// </summary>
        public string Handlowiec
        {
            get => _handlowiec;
            set => SetProperty(ref _handlowiec, value);
        }

        /// <summary>
        /// NIP
        /// </summary>
        public string NIP
        {
            get => _nip;
            set => SetProperty(ref _nip, value);
        }

        /// <summary>
        /// Adres
        /// </summary>
        public string Adres
        {
            get => _adres;
            set => SetProperty(ref _adres, value);
        }

        /// <summary>
        /// Miasto
        /// </summary>
        public string Miasto
        {
            get => _miasto;
            set => SetProperty(ref _miasto, value);
        }

        /// <summary>
        /// Kod pocztowy
        /// </summary>
        public string KodPocztowy
        {
            get => _kodPocztowy;
            set => SetProperty(ref _kodPocztowy, value);
        }

        /// <summary>
        /// Telefon
        /// </summary>
        public string Telefon
        {
            get => _telefon;
            set => SetProperty(ref _telefon, value);
        }

        /// <summary>
        /// Email
        /// </summary>
        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        /// <summary>
        /// Termin płatności w dniach
        /// </summary>
        public int? TerminPlatnosci
        {
            get => _terminPlatnosci;
            set => SetProperty(ref _terminPlatnosci, value);
        }

        /// <summary>
        /// Limit kredytowy
        /// </summary>
        public decimal LimitKredytowy
        {
            get => _limitKredytowy;
            set => SetProperty(ref _limitKredytowy, value);
        }

        /// <summary>
        /// Saldo należności (ile nam winien)
        /// </summary>
        public decimal SaldoNaleznosci
        {
            get => _saldoNaleznosci;
            set => SetProperty(ref _saldoNaleznosci, value);
        }

        /// <summary>
        /// Czy klient jest aktywny
        /// </summary>
        public bool Aktywny
        {
            get => _aktywny;
            set => SetProperty(ref _aktywny, value);
        }

        #endregion

        #region Calculated Properties

        /// <summary>
        /// Nazwa wyświetlana (Shortcut lub ID)
        /// </summary>
        public string DisplayName => !string.IsNullOrWhiteSpace(Shortcut) ? Shortcut : $"KH {Id}";

        /// <summary>
        /// Pełny adres
        /// </summary>
        public string PelnyAdres
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Adres)) parts.Add(Adres);
                if (!string.IsNullOrWhiteSpace(KodPocztowy) || !string.IsNullOrWhiteSpace(Miasto))
                    parts.Add($"{KodPocztowy} {Miasto}".Trim());
                return string.Join(", ", parts);
            }
        }

        /// <summary>
        /// Czy klient ma przekroczony limit kredytowy
        /// </summary>
        public bool PrzekroczonyLimit => LimitKredytowy > 0 && SaldoNaleznosci > LimitKredytowy;

        /// <summary>
        /// Procent wykorzystania limitu
        /// </summary>
        public decimal ProcentWykorzystaniaLimitu =>
            LimitKredytowy > 0 ? Math.Round(SaldoNaleznosci / LimitKredytowy * 100, 1) : 0;

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
