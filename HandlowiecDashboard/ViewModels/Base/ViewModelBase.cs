using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.HandlowiecDashboard.ViewModels.Base
{
    /// <summary>
    /// Bazowa klasa dla wszystkich ViewModeli - implementuje INotifyPropertyChanged
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Ustawia wartość pola i wywołuje PropertyChanged jeśli wartość się zmieniła
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Wspólne właściwości dla wszystkich ViewModeli

        private bool _isLoading;
        /// <summary>Czy trwa ładowanie danych</summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _errorMessage;
        /// <summary>Komunikat błędu do wyświetlenia</summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private bool _hasError;
        /// <summary>Czy wystąpił błąd</summary>
        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        /// <summary>
        /// Ustawia stan błędu
        /// </summary>
        protected void SetError(string message)
        {
            ErrorMessage = message;
            HasError = !string.IsNullOrEmpty(message);
        }

        /// <summary>
        /// Czyści stan błędu
        /// </summary>
        protected void ClearError()
        {
            ErrorMessage = null;
            HasError = false;
        }
    }
}
