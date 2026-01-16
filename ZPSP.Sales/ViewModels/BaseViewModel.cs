using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using ZPSP.Sales.Infrastructure;

namespace ZPSP.Sales.ViewModels
{
    /// <summary>
    /// Bazowa klasa dla wszystkich ViewModeli w aplikacji.
    /// Implementuje INotifyPropertyChanged i dostarcza wspólne funkcjonalności.
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged, IDisposable
    {
        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Powiadamia o zmianie właściwości
        /// </summary>
        /// <param name="propertyName">Nazwa właściwości (automatycznie wypełniana)</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Ustawia wartość właściwości i powiadamia o zmianie jeśli wartość się zmieniła
        /// </summary>
        /// <typeparam name="T">Typ właściwości</typeparam>
        /// <param name="field">Referencja do backing field</param>
        /// <param name="value">Nowa wartość</param>
        /// <param name="propertyName">Nazwa właściwości</param>
        /// <returns>True jeśli wartość została zmieniona</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Ustawia wartość i wywołuje dodatkową akcję po zmianie
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, Action onChanged, [CallerMemberName] string propertyName = null)
        {
            if (!SetProperty(ref field, value, propertyName))
                return false;

            onChanged?.Invoke();
            return true;
        }

        #endregion

        #region Loading State

        private bool _isLoading;
        private string _loadingMessage = "Ładowanie...";

        /// <summary>
        /// Wskazuje czy ViewModel aktualnie ładuje dane
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Komunikat wyświetlany podczas ładowania
        /// </summary>
        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

        #endregion

        #region Error Handling

        private string _errorMessage;
        private bool _hasError;

        /// <summary>
        /// Komunikat o błędzie (jeśli wystąpił)
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                SetProperty(ref _errorMessage, value);
                HasError = !string.IsNullOrEmpty(value);
            }
        }

        /// <summary>
        /// Wskazuje czy wystąpił błąd
        /// </summary>
        public bool HasError
        {
            get => _hasError;
            private set => SetProperty(ref _hasError, value);
        }

        /// <summary>
        /// Czyści komunikat o błędzie
        /// </summary>
        protected void ClearError()
        {
            ErrorMessage = null;
        }

        #endregion

        #region Status

        private string _statusMessage;
        private DateTime _lastUpdated;

        /// <summary>
        /// Komunikat statusu (pasek statusu)
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Czas ostatniej aktualizacji danych
        /// </summary>
        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value);
        }

        /// <summary>
        /// Sformatowany czas ostatniej aktualizacji
        /// </summary>
        public string LastUpdatedFormatted => LastUpdated == default
            ? ""
            : $"Ostatnia aktualizacja: {LastUpdated:HH:mm:ss}";

        #endregion

        #region Async Execution Helpers

        /// <summary>
        /// Wykonuje operację asynchroniczną z obsługą stanu ładowania i błędów
        /// </summary>
        /// <param name="action">Akcja do wykonania</param>
        /// <param name="loadingMessage">Komunikat podczas ładowania</param>
        /// <param name="showError">Czy wyświetlić błąd w MessageBox</param>
        protected async Task ExecuteAsync(Func<Task> action, string loadingMessage = "Ładowanie...", bool showError = true)
        {
            if (IsLoading)
                return;

            try
            {
                IsLoading = true;
                LoadingMessage = loadingMessage;
                ClearError();

                await action();

                LastUpdated = DateTime.Now;
                OnPropertyChanged(nameof(LastUpdatedFormatted));
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] Błąd: {ex}");

                if (showError)
                {
                    MessageBox.Show(
                        $"Wystąpił błąd: {ex.Message}",
                        "Błąd",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Wykonuje operację asynchroniczną z wartością zwracaną
        /// </summary>
        /// <typeparam name="T">Typ wartości zwracanej</typeparam>
        /// <param name="func">Funkcja do wykonania</param>
        /// <param name="loadingMessage">Komunikat podczas ładowania</param>
        /// <returns>Wynik operacji lub wartość domyślna</returns>
        protected async Task<T> ExecuteAsync<T>(Func<Task<T>> func, string loadingMessage = "Ładowanie...")
        {
            if (IsLoading)
                return default;

            try
            {
                IsLoading = true;
                LoadingMessage = loadingMessage;
                ClearError();

                var result = await func();

                LastUpdated = DateTime.Now;
                OnPropertyChanged(nameof(LastUpdatedFormatted));

                return result;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] Błąd: {ex}");
                return default;
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Validation

        /// <summary>
        /// Waliduje datę SQL Server (zakres 1753-9999)
        /// </summary>
        /// <param name="date">Data do walidacji</param>
        /// <returns>Zwalidowana data</returns>
        protected DateTime ValidateSqlDate(DateTime date)
        {
            if (date < new DateTime(1753, 1, 1))
                return new DateTime(1753, 1, 1);

            if (date > new DateTime(9999, 12, 31))
                return new DateTime(9999, 12, 31);

            return date;
        }

        #endregion

        #region IDisposable

        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Zwolnij zasoby zarządzane
                OnDispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Metoda do nadpisania w klasach pochodnych dla czyszczenia zasobów
        /// </summary>
        protected virtual void OnDispose() { }

        ~BaseViewModel()
        {
            Dispose(false);
        }

        #endregion
    }
}
