using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Kalendarz1.HandlowiecDashboard.Models;
using Kalendarz1.HandlowiecDashboard.Services.Interfaces;
using Kalendarz1.HandlowiecDashboard.ViewModels.Base;

namespace Kalendarz1.HandlowiecDashboard.ViewModels
{
    /// <summary>
    /// ViewModel dla zakładki Realizacja Celów z Gauge
    /// </summary>
    public class RealizacjaCeluViewModel : ViewModelBase
    {
        private readonly ICeleService _celeService;
        private readonly ILoggingService _logger;

        public RealizacjaCeluViewModel(ICeleService celeService, ILoggingService logger)
        {
            _celeService = celeService;
            _logger = logger;

            WybranyRok = DateTime.Now.Year;
            WybranyMiesiac = DateTime.Now.Month;

            RefreshCommand = new AsyncRelayCommand(LoadDataAsync);
        }

        // Komendy
        public ICommand RefreshCommand { get; }

        // Filtry
        private int _wybranyRok;
        public int WybranyRok
        {
            get => _wybranyRok;
            set
            {
                if (SetProperty(ref _wybranyRok, value))
                    _ = LoadDataAsync();
            }
        }

        private int _wybranyMiesiac;
        public int WybranyMiesiac
        {
            get => _wybranyMiesiac;
            set
            {
                if (SetProperty(ref _wybranyMiesiac, value))
                    _ = LoadDataAsync();
            }
        }

        private string _wybranyHandlowiec;
        public string WybranyHandlowiec
        {
            get => _wybranyHandlowiec;
            set
            {
                if (SetProperty(ref _wybranyHandlowiec, value))
                    _ = LoadDataAsync();
            }
        }

        // Dane
        private ObservableCollection<RealizacjaCelu> _realizacjeWszystkich;
        public ObservableCollection<RealizacjaCelu> RealizacjeWszystkich
        {
            get => _realizacjeWszystkich;
            set => SetProperty(ref _realizacjeWszystkich, value);
        }

        private RealizacjaCelu _wybranaRealizacja;
        public RealizacjaCelu WybranaRealizacja
        {
            get => _wybranaRealizacja;
            set => SetProperty(ref _wybranaRealizacja, value);
        }

        // Dla Gauge - wartość 0-100 (lub więcej)
        public double GaugeValue => WybranaRealizacja?.RealizacjaWartoscProcent ?? 0;
        public string GaugeKolor => WybranaRealizacja?.KolorWartosci ?? "#8B949E";

        public async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                ClearError();

                if (string.IsNullOrEmpty(WybranyHandlowiec))
                {
                    // Załaduj wszystkich handlowców
                    var dane = await _celeService.PobierzRealizacjeWszystkichAsync(WybranyRok, WybranyMiesiac);
                    RealizacjeWszystkich = new ObservableCollection<RealizacjaCelu>(dane);

                    // Wybierz pierwszego jako domyślnego dla Gauge
                    if (dane.Count > 0)
                        WybranaRealizacja = dane[0];
                }
                else
                {
                    // Załaduj konkretnego handlowca
                    WybranaRealizacja = await _celeService.PobierzRealizacjeCeluAsync(
                        WybranyHandlowiec, WybranyRok, WybranyMiesiac);
                }

                // Powiadom o zmianie wartości Gauge
                OnPropertyChanged(nameof(GaugeValue));
                OnPropertyChanged(nameof(GaugeKolor));
            }
            catch (Exception ex)
            {
                _logger.LogError("Błąd ładowania realizacji celów", ex);
                SetError("Nie udało się załadować danych. Sprawdź połączenie z bazą.");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
