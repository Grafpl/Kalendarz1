using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.ViewModels
{
    /// <summary>
    /// ViewModel dla głównego dashboardu opakowań zwrotnych - ZOPTYMALIZOWANY
    /// Techniki: Pre-loading, Debounced filtering, Lazy evaluation, Memory efficiency
    /// </summary>
    public class DashboardViewModel : ViewModelBase, IDisposable
    {
        private readonly SaldaService _service;
        private string _handlowiecFilter;
        private List<SaldoKontrahenta> _wszystkieSalda;
        private CancellationTokenSource _filterCts;
        private bool _disposed;

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

            // Start z pre-loadingiem
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
        /// Tekst filtra szukania (z debounce)
        /// </summary>
        public string FiltrTekst
        {
            get => _filtrTekst;
            set
            {
                if (SetProperty(ref _filtrTekst, value))
                {
                    OnPropertyChanged(nameof(HasFilter));
                    // Debounced filtering
                    DebouncedFilter();
                }
            }
        }

        /// <summary>
        /// Czy jest aktywny filtr
        /// </summary>
        public bool HasFilter => !string.IsNullOrEmpty(FiltrTekst);

        private SaldoKontrahenta _wybranyKontrahent;
        /// <summary>
        /// Wybrany kontrahent w tabeli - z pre-loadingiem dokumentów
        /// </summary>
        public SaldoKontrahenta WybranyKontrahent
        {
            get => _wybranyKontrahent;
            set
            {
                if (SetProperty(ref _wybranyKontrahent, value) && value != null)
                {
                    // Pre-load dokumenty dla wybranego kontrahenta
                    PreloadDokumentyForSelected(value);
                }
            }
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
        /// Inicjalizacja - pobiera uprawnienia i dane równolegle
        /// </summary>
        private async Task InitAsync()
        {
            // Parallel: pobierz handlowca i pre-load dane
            var taskHandlowiec = _service.PobierzHandlowcaAsync(UserId);

            // Fire-and-forget pre-load (nie czekamy)
            _service.PreloadDataAsync(DataDo);

            _handlowiecFilter = await taskHandlowiec;
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

                // Pre-load for tomorrow (background)
                _service.PreloadDataAsync(DataDo.AddDays(1), _handlowiecFilter);
            }, "Pobieranie danych...");
        }

        /// <summary>
        /// Oblicza statystyki dla kafelków - ZOPTYMALIZOWANE (single pass)
        /// </summary>
        private void ObliczStatystyki()
        {
            // Single pass calculation (O(n) zamiast O(5n))
            int sumaE2 = 0, sumaH1 = 0, sumaEURO = 0, sumaPCV = 0, sumaDREW = 0;
            int kontrahentowE2 = 0, kontrahentowH1 = 0, kontrahentowEURO = 0, kontrahentowPCV = 0, kontrahentowDREW = 0;
            int bezPotwierdzenia = 0;

            foreach (var s in _wszystkieSalda)
            {
                // E2
                if (s.E2 > 0)
                {
                    sumaE2 += s.E2;
                    kontrahentowE2++;
                }

                // H1
                if (s.H1 > 0)
                {
                    sumaH1 += s.H1;
                    kontrahentowH1++;
                }

                // EURO
                if (s.EURO > 0)
                {
                    sumaEURO += s.EURO;
                    kontrahentowEURO++;
                }

                // PCV
                if (s.PCV > 0)
                {
                    sumaPCV += s.PCV;
                    kontrahentowPCV++;
                }

                // DREW
                if (s.DREW > 0)
                {
                    sumaDREW += s.DREW;
                    kontrahentowDREW++;
                }

                // Bez potwierdzenia
                if ((s.E2 > 0 && !s.E2Potwierdzone) ||
                    (s.H1 > 0 && !s.H1Potwierdzone) ||
                    (s.EURO > 0 && !s.EUROPotwierdzone) ||
                    (s.PCV > 0 && !s.PCVPotwierdzone) ||
                    (s.DREW > 0 && !s.DREWPotwierdzone))
                {
                    bezPotwierdzenia++;
                }
            }

            // Batch property updates
            SumaE2 = sumaE2;
            SumaH1 = sumaH1;
            SumaEURO = sumaEURO;
            SumaPCV = sumaPCV;
            SumaDREW = sumaDREW;

            LiczbaKontrahentowE2 = kontrahentowE2;
            LiczbaKontrahentowH1 = kontrahentowH1;
            LiczbaKontrahentowEURO = kontrahentowEURO;
            LiczbaKontrahentowPCV = kontrahentowPCV;
            LiczbaKontrahentowDREW = kontrahentowDREW;

            SumaWszystkich = sumaE2 + sumaH1 + sumaEURO + sumaPCV + sumaDREW;
            LiczbaBezPotwierdzenia = bezPotwierdzenia;
        }

        /// <summary>
        /// Debounced filter - czeka 150ms przed filtrowaniem
        /// </summary>
        private void DebouncedFilter()
        {
            // Cancel previous filter
            _filterCts?.Cancel();
            _filterCts = new CancellationTokenSource();
            var token = _filterCts.Token;

            // Debounce 150ms
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(150, token);
                    if (!token.IsCancellationRequested)
                    {
                        // Execute on UI thread
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            if (!token.IsCancellationRequested)
                            {
                                FiltrujWyniki();
                            }
                        });
                    }
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancelled
                }
            }, token);
        }

        /// <summary>
        /// Filtruje listę wyników na podstawie tekstu szukania - ZOPTYMALIZOWANE
        /// </summary>
        private void FiltrujWyniki()
        {
            var filtr = FiltrTekst?.ToLower() ?? "";
            var isEmpty = string.IsNullOrEmpty(filtr);

            // Clear efficiently
            WynikiSzukania.Clear();

            // Pre-allocate capacity hint
            var przefiltrowane = isEmpty
                ? _wszystkieSalda
                    .OrderByDescending(s => s.SumaWszystkich)
                    .Take(100)
                : _wszystkieSalda
                    .Where(s => s.Kontrahent?.ToLower().Contains(filtr) == true ||
                                s.Nazwa?.ToLower().Contains(filtr) == true ||
                                s.Handlowiec?.ToLower().Contains(filtr) == true)
                    .OrderByDescending(s => s.SumaWszystkich)
                    .Take(100);

            // Bulk add (more efficient than individual adds)
            foreach (var s in przefiltrowane)
            {
                WynikiSzukania.Add(s);
            }

            LiczbaWynikow = WynikiSzukania.Count;
        }

        /// <summary>
        /// Pre-load dokumenty dla wybranego kontrahenta (background)
        /// </summary>
        private void PreloadDokumentyForSelected(SaldoKontrahenta kontrahent)
        {
            var dataOd = DataDo.AddMonths(-3);
            _service.PreloadDokumentyAsync(kontrahent.Id, dataOd, DataDo);
        }

        /// <summary>
        /// Pobiera listę kontrahentów dla określonego typu opakowania
        /// </summary>
        public List<SaldoKontrahenta> PobierzKontrahentowDlaOpakowania(string typOpakowania)
        {
            if (_wszystkieSalda == null) return new List<SaldoKontrahenta>();

            return typOpakowania switch
            {
                "E2" => _wszystkieSalda.Where(s => s.E2 != 0).OrderByDescending(s => Math.Abs(s.E2)).ToList(),
                "H1" => _wszystkieSalda.Where(s => s.H1 != 0).OrderByDescending(s => Math.Abs(s.H1)).ToList(),
                "EURO" => _wszystkieSalda.Where(s => s.EURO != 0).OrderByDescending(s => Math.Abs(s.EURO)).ToList(),
                "PCV" => _wszystkieSalda.Where(s => s.PCV != 0).OrderByDescending(s => Math.Abs(s.PCV)).ToList(),
                "DREW" => _wszystkieSalda.Where(s => s.DREW != 0).OrderByDescending(s => Math.Abs(s.DREW)).ToList(),
                _ => _wszystkieSalda.ToList()
            };
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _filterCts?.Cancel();
                _filterCts?.Dispose();
                _service?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}
