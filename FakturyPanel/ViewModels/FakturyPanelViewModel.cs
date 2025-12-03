using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Kalendarz1.FakturyPanel.Models;
using Kalendarz1.FakturyPanel.Services;

namespace Kalendarz1.FakturyPanel.ViewModels
{
    /// <summary>
    /// ViewModel dla panelu fakturzystek - widok zamówień handlowców
    /// </summary>
    public class FakturyPanelViewModel : INotifyPropertyChanged
    {
        private readonly FakturyDataService _dataService;

        private bool _isLoading;
        private string _errorMessage;
        private string _statusMessage;
        private ZamowienieFaktury _wybraneZamowienie;
        private ObservableCollection<ZamowienieFaktury> _zamowienia;
        private ObservableCollection<HistoriaZmianZamowienia> _historiaZmian;
        private ObservableCollection<string> _handlowcy;

        // Filtry
        private DateTime? _dataOd;
        private DateTime? _dataDo;
        private string _wybranyHandlowiec;
        private string _szukajTekst;
        private bool _pokazAnulowane;

        // Użytkownik
        private string _aktualnyUzytkownik;
        private string _aktualnyUzytkownikNazwa;

        public event PropertyChangedEventHandler PropertyChanged;

        public FakturyPanelViewModel()
        {
            _dataService = new FakturyDataService();
            _zamowienia = new ObservableCollection<ZamowienieFaktury>();
            _historiaZmian = new ObservableCollection<HistoriaZmianZamowienia>();
            _handlowcy = new ObservableCollection<string>();

            // Domyślne daty - bieżący tydzień
            _dataOd = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1);
            _dataDo = _dataOd.Value.AddDays(6);

            // Pobierz aktualnego użytkownika
            _aktualnyUzytkownik = App.UserID ?? Environment.UserName;
            _aktualnyUzytkownikNazwa = App.UserFullName ?? _aktualnyUzytkownik;

            // Inicjalizuj komendy
            OdswiezCommand = new RelayCommand(async () => await OdswiezDaneAsync());
            WyczyscFiltryCommand = new RelayCommand(WyczyscFiltry);
            PoprzedniTydzienCommand = new RelayCommand(PoprzedniTydzien);
            NastepnyTydzienCommand = new RelayCommand(NastepnyTydzien);
            DzisCommand = new RelayCommand(UstawDzis);
        }

        #region Properties

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotLoading)); }
        }

        public bool IsNotLoading => !_isLoading;

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ZamowienieFaktury> Zamowienia
        {
            get => _zamowienia;
            set { _zamowienia = value; OnPropertyChanged(); }
        }

        public ObservableCollection<HistoriaZmianZamowienia> HistoriaZmian
        {
            get => _historiaZmian;
            set { _historiaZmian = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> Handlowcy
        {
            get => _handlowcy;
            set { _handlowcy = value; OnPropertyChanged(); }
        }

        public ZamowienieFaktury WybraneZamowienie
        {
            get => _wybraneZamowienie;
            set
            {
                _wybraneZamowienie = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MaWybraneZamowienie));
                if (value != null)
                {
                    _ = PobierzHistorieZamowieniaAsync(value.Id);
                }
            }
        }

        public bool MaWybraneZamowienie => _wybraneZamowienie != null;

        // Filtry
        public DateTime? DataOd
        {
            get => _dataOd;
            set { _dataOd = value; OnPropertyChanged(); OnPropertyChanged(nameof(ZakresDatTekst)); }
        }

        public DateTime? DataDo
        {
            get => _dataDo;
            set { _dataDo = value; OnPropertyChanged(); OnPropertyChanged(nameof(ZakresDatTekst)); }
        }

        public string WybranyHandlowiec
        {
            get => _wybranyHandlowiec;
            set { _wybranyHandlowiec = value; OnPropertyChanged(); }
        }

        public string SzukajTekst
        {
            get => _szukajTekst;
            set { _szukajTekst = value; OnPropertyChanged(); }
        }

        public bool PokazAnulowane
        {
            get => _pokazAnulowane;
            set { _pokazAnulowane = value; OnPropertyChanged(); }
        }

        public string ZakresDatTekst
        {
            get
            {
                if (_dataOd.HasValue && _dataDo.HasValue)
                    return $"{_dataOd.Value:dd.MM} - {_dataDo.Value:dd.MM.yyyy}";
                return "Wszystkie daty";
            }
        }

        public string AktualnyUzytkownik
        {
            get => _aktualnyUzytkownik;
            set { _aktualnyUzytkownik = value; OnPropertyChanged(); }
        }

        public string AktualnyUzytkownikNazwa
        {
            get => _aktualnyUzytkownikNazwa;
            set { _aktualnyUzytkownikNazwa = value; OnPropertyChanged(); }
        }

        #endregion

        #region Commands

        public ICommand OdswiezCommand { get; }
        public ICommand WyczyscFiltryCommand { get; }
        public ICommand PoprzedniTydzienCommand { get; }
        public ICommand NastepnyTydzienCommand { get; }
        public ICommand DzisCommand { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Inicjalizuje ViewModel - wywoływane przy starcie okna
        /// </summary>
        public async Task InitializeAsync()
        {
            IsLoading = true;
            ErrorMessage = null;
            StatusMessage = "Ładowanie danych...";

            try
            {
                // Pobierz handlowców
                var handlowcy = await _dataService.PobierzHandlowcowAsync();
                Handlowcy.Clear();
                foreach (var h in handlowcy)
                    Handlowcy.Add(h);

                // Pobierz zamówienia
                await OdswiezZamowieniaAsync();

                StatusMessage = $"Załadowano {Zamowienia.Count} zamówień";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Błąd ładowania danych: {ex.Message}";
                StatusMessage = "Błąd";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Odświeża dane (zamówienia)
        /// </summary>
        private async Task OdswiezDaneAsync()
        {
            await OdswiezZamowieniaAsync();
        }

        /// <summary>
        /// Pobiera zamówienia z uwzględnieniem filtrów
        /// </summary>
        private async Task OdswiezZamowieniaAsync()
        {
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                var filtr = new FiltrZamowien
                {
                    DataOd = _dataOd,
                    DataDo = _dataDo,
                    Handlowiec = _wybranyHandlowiec,
                    SzukajTekst = _szukajTekst,
                    PokazAnulowane = _pokazAnulowane
                };

                var zamowienia = await _dataService.PobierzZamowieniaAsync(filtr);

                Zamowienia.Clear();
                foreach (var z in zamowienia)
                    Zamowienia.Add(z);

                StatusMessage = $"Znaleziono {Zamowienia.Count} zamówień";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Błąd pobierania zamówień: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Pobiera historię zmian dla wybranego zamówienia
        /// </summary>
        private async Task PobierzHistorieZamowieniaAsync(int zamowienieId)
        {
            try
            {
                var historia = await _dataService.PobierzHistorieZamowieniaAsync(zamowienieId);

                HistoriaZmian.Clear();
                foreach (var h in historia)
                    HistoriaZmian.Add(h);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania historii: {ex.Message}");
            }
        }

        /// <summary>
        /// Czyści wszystkie filtry
        /// </summary>
        private void WyczyscFiltry()
        {
            DataOd = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1);
            DataDo = DataOd.Value.AddDays(6);
            WybranyHandlowiec = null;
            SzukajTekst = null;
            PokazAnulowane = false;

            _ = OdswiezZamowieniaAsync();
        }

        /// <summary>
        /// Przechodzi do poprzedniego tygodnia
        /// </summary>
        private void PoprzedniTydzien()
        {
            DataOd = DataOd?.AddDays(-7);
            DataDo = DataDo?.AddDays(-7);
            _ = OdswiezZamowieniaAsync();
        }

        /// <summary>
        /// Przechodzi do następnego tygodnia
        /// </summary>
        private void NastepnyTydzien()
        {
            DataOd = DataOd?.AddDays(7);
            DataDo = DataDo?.AddDays(7);
            _ = OdswiezZamowieniaAsync();
        }

        /// <summary>
        /// Ustawia bieżący tydzień
        /// </summary>
        private void UstawDzis()
        {
            DataOd = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1);
            DataDo = DataOd.Value.AddDays(6);
            _ = OdswiezZamowieniaAsync();
        }

        /// <summary>
        /// Filtruje zamówienia po handlowcu
        /// </summary>
        public async Task FiltrujPoHandlowcuAsync(string handlowiec)
        {
            WybranyHandlowiec = handlowiec;
            await OdswiezZamowieniaAsync();
        }

        /// <summary>
        /// Szuka zamówień po tekście
        /// </summary>
        public async Task SzukajAsync(string tekst)
        {
            SzukajTekst = tekst;
            await OdswiezZamowieniaAsync();
        }

        #endregion

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Implementacja ICommand dla wzorca MVVM
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        public void Execute(object parameter) => _execute();
    }
}
