using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.ViewModels
{
    /// <summary>
    /// ViewModel dla głównego dashboardu opakowań zwrotnych
    /// </summary>
    public class DashboardViewModel : ViewModelBase
    {
        private readonly SaldaService _service;
        private string _handlowiecFilter;
        private List<SaldoKontrahenta> _wszystkieSalda;

        public string UserId { get; }

        public DashboardViewModel(string userId)
        {
            UserId = userId;
            _service = new SaldaService();
            _dataDo = DateTime.Today;
            _wszystkieSalda = new List<SaldoKontrahenta>();
            WynikiSzukania = new ObservableCollection<SaldoKontrahenta>();

            // Komendy
            OdswiezCommand = new AsyncRelayCommand(OdswiezAsync);
            ClearFilterCommand = new RelayCommand(_ => FiltrTekst = "");

            // Start
            _ = InitAsync();
        }

        #region Properties - Data

        private DateTime _dataDo;
        /// <summary>
        /// Data do której obliczane są salda
        /// </summary>
        public DateTime DataDo
        {
            get => _dataDo;
            set
            {
                if (SetProperty(ref _dataDo, value))
                {
                    SaldaService.InvalidateCache();
                    _ = OdswiezAsync();
                }
            }
        }

        private string _filtrTekst;
        /// <summary>
        /// Tekst filtra szukania
        /// </summary>
        public string FiltrTekst
        {
            get => _filtrTekst;
            set
            {
                if (SetProperty(ref _filtrTekst, value))
                {
                    OnPropertyChanged(nameof(HasFilter));
                    FiltrujWyniki();
                }
            }
        }

        /// <summary>
        /// Czy jest aktywny filtr
        /// </summary>
        public bool HasFilter => !string.IsNullOrEmpty(FiltrTekst);

        private SaldoKontrahenta _wybranyKontrahent;
        /// <summary>
        /// Wybrany kontrahent w tabeli
        /// </summary>
        public SaldoKontrahenta WybranyKontrahent
        {
            get => _wybranyKontrahent;
            set => SetProperty(ref _wybranyKontrahent, value);
        }

        /// <summary>
        /// Lista wyników szukania / wszystkich kontrahentów
        /// </summary>
        public ObservableCollection<SaldoKontrahenta> WynikiSzukania { get; }

        /// <summary>
        /// Nazwa handlowca dla filtrowania
        /// </summary>
        public string NazwaHandlowca => string.IsNullOrEmpty(_handlowiecFilter) ? "Wszyscy handlowcy" : _handlowiecFilter;

        #endregion

        #region Properties - Statystyki E2

        private int _sumaE2;
        public int SumaE2
        {
            get => _sumaE2;
            set => SetProperty(ref _sumaE2, value);
        }

        private int _liczbaKontrahentowE2;
        public int LiczbaKontrahentowE2
        {
            get => _liczbaKontrahentowE2;
            set => SetProperty(ref _liczbaKontrahentowE2, value);
        }

        #endregion

        #region Properties - Statystyki H1

        private int _sumaH1;
        public int SumaH1
        {
            get => _sumaH1;
            set => SetProperty(ref _sumaH1, value);
        }

        private int _liczbaKontrahentowH1;
        public int LiczbaKontrahentowH1
        {
            get => _liczbaKontrahentowH1;
            set => SetProperty(ref _liczbaKontrahentowH1, value);
        }

        #endregion

        #region Properties - Statystyki EURO

        private int _sumaEURO;
        public int SumaEURO
        {
            get => _sumaEURO;
            set => SetProperty(ref _sumaEURO, value);
        }

        private int _liczbaKontrahentowEURO;
        public int LiczbaKontrahentowEURO
        {
            get => _liczbaKontrahentowEURO;
            set => SetProperty(ref _liczbaKontrahentowEURO, value);
        }

        #endregion

        #region Properties - Statystyki PCV

        private int _sumaPCV;
        public int SumaPCV
        {
            get => _sumaPCV;
            set => SetProperty(ref _sumaPCV, value);
        }

        private int _liczbaKontrahentowPCV;
        public int LiczbaKontrahentowPCV
        {
            get => _liczbaKontrahentowPCV;
            set => SetProperty(ref _liczbaKontrahentowPCV, value);
        }

        #endregion

        #region Properties - Statystyki DREW

        private int _sumaDREW;
        public int SumaDREW
        {
            get => _sumaDREW;
            set => SetProperty(ref _sumaDREW, value);
        }

        private int _liczbaKontrahentowDREW;
        public int LiczbaKontrahentowDREW
        {
            get => _liczbaKontrahentowDREW;
            set => SetProperty(ref _liczbaKontrahentowDREW, value);
        }

        #endregion

        #region Properties - Globalne statystyki

        private int _sumaWszystkich;
        public int SumaWszystkich
        {
            get => _sumaWszystkich;
            set => SetProperty(ref _sumaWszystkich, value);
        }

        private int _liczbaBezPotwierdzenia;
        public int LiczbaBezPotwierdzenia
        {
            get => _liczbaBezPotwierdzenia;
            set => SetProperty(ref _liczbaBezPotwierdzenia, value);
        }

        private int _liczbaWynikow;
        public int LiczbaWynikow
        {
            get => _liczbaWynikow;
            set => SetProperty(ref _liczbaWynikow, value);
        }

        #endregion

        #region Commands

        public ICommand OdswiezCommand { get; }
        public ICommand ClearFilterCommand { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Inicjalizacja - pobiera uprawnienia i dane
        /// </summary>
        private async Task InitAsync()
        {
            _handlowiecFilter = await _service.PobierzHandlowcaAsync(UserId);
            OnPropertyChanged(nameof(NazwaHandlowca));
            await OdswiezAsync();
        }

        /// <summary>
        /// Odświeża wszystkie dane z bazy
        /// </summary>
        private async Task OdswiezAsync()
        {
            await ExecuteAsync(async () =>
            {
                _wszystkieSalda = await _service.PobierzWszystkieSaldaAsync(DataDo, _handlowiecFilter);
                ObliczStatystyki();
                FiltrujWyniki();
            }, "Pobieranie danych...");
        }

        /// <summary>
        /// Oblicza statystyki dla kafelków
        /// </summary>
        private void ObliczStatystyki()
        {
            // E2
            var listaE2 = _wszystkieSalda.Where(s => s.E2 > 0).ToList();
            SumaE2 = listaE2.Sum(s => s.E2);
            LiczbaKontrahentowE2 = listaE2.Count;

            // H1
            var listaH1 = _wszystkieSalda.Where(s => s.H1 > 0).ToList();
            SumaH1 = listaH1.Sum(s => s.H1);
            LiczbaKontrahentowH1 = listaH1.Count;

            // EURO
            var listaEURO = _wszystkieSalda.Where(s => s.EURO > 0).ToList();
            SumaEURO = listaEURO.Sum(s => s.EURO);
            LiczbaKontrahentowEURO = listaEURO.Count;

            // PCV
            var listaPCV = _wszystkieSalda.Where(s => s.PCV > 0).ToList();
            SumaPCV = listaPCV.Sum(s => s.PCV);
            LiczbaKontrahentowPCV = listaPCV.Count;

            // DREW
            var listaDREW = _wszystkieSalda.Where(s => s.DREW > 0).ToList();
            SumaDREW = listaDREW.Sum(s => s.DREW);
            LiczbaKontrahentowDREW = listaDREW.Count;

            // Globalne
            SumaWszystkich = SumaE2 + SumaH1 + SumaEURO + SumaPCV + SumaDREW;

            // Liczba kontrahentów bez potwierdzenia w ostatnich 30 dniach
            LiczbaBezPotwierdzenia = _wszystkieSalda.Count(s =>
                (s.E2 > 0 && !s.E2Potwierdzone) ||
                (s.H1 > 0 && !s.H1Potwierdzone) ||
                (s.EURO > 0 && !s.EUROPotwierdzone) ||
                (s.PCV > 0 && !s.PCVPotwierdzone) ||
                (s.DREW > 0 && !s.DREWPotwierdzone));
        }

        /// <summary>
        /// Filtruje listę wyników na podstawie tekstu szukania
        /// </summary>
        private void FiltrujWyniki()
        {
            var filtr = FiltrTekst?.ToLower() ?? "";

            WynikiSzukania.Clear();

            var przefiltrowane = _wszystkieSalda
                .Where(s => string.IsNullOrEmpty(filtr) ||
                            s.Kontrahent?.ToLower().Contains(filtr) == true ||
                            s.Nazwa?.ToLower().Contains(filtr) == true ||
                            s.Handlowiec?.ToLower().Contains(filtr) == true)
                .OrderByDescending(s => s.SumaWszystkich)
                .Take(100) // Limit dla wydajności
                .ToList();

            foreach (var s in przefiltrowane)
            {
                WynikiSzukania.Add(s);
            }

            LiczbaWynikow = przefiltrowane.Count;
        }

        #endregion
    }
}
