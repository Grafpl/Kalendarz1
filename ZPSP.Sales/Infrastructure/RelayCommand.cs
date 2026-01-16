using System;
using System.Windows.Input;

namespace ZPSP.Sales.Infrastructure
{
    /// <summary>
    /// Implementacja ICommand dla synchronicznych operacji w MVVM.
    /// Umożliwia bindowanie metod do komend w XAML.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>
        /// Tworzy nową komendę z akcją wykonania
        /// </summary>
        /// <param name="execute">Akcja do wykonania</param>
        public RelayCommand(Action<object> execute) : this(execute, null) { }

        /// <summary>
        /// Tworzy nową komendę z akcją wykonania i warunkiem dostępności
        /// </summary>
        /// <param name="execute">Akcja do wykonania</param>
        /// <param name="canExecute">Predykat określający czy komenda może być wykonana</param>
        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Tworzy nową komendę bez parametru
        /// </summary>
        /// <param name="execute">Akcja do wykonania</param>
        public RelayCommand(Action execute) : this(_ => execute(), null) { }

        /// <summary>
        /// Tworzy nową komendę bez parametru z warunkiem dostępności
        /// </summary>
        /// <param name="execute">Akcja do wykonania</param>
        /// <param name="canExecute">Funkcja określająca czy komenda może być wykonana</param>
        public RelayCommand(Action execute, Func<bool> canExecute)
            : this(_ => execute(), _ => canExecute()) { }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// Wymusza ponowne sprawdzenie CanExecute
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Generyczna wersja RelayCommand z typowanym parametrem
    /// </summary>
    /// <typeparam name="T">Typ parametru komendy</typeparam>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public RelayCommand(Action<T> execute) : this(execute, null) { }

        public RelayCommand(Action<T> execute, Predicate<T> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (_canExecute == null)
                return true;

            if (parameter == null && typeof(T).IsValueType)
                return _canExecute(default);

            return _canExecute((T)parameter);
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
