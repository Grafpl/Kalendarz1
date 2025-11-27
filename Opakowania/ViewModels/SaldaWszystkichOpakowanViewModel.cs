using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.ViewModels
{
    /// <summary>
    /// ViewModel dla widoku salda wszystkich opakowań (WidokPojemniki)
    /// Pokazuje listę wszystkich kontrahentów z ich saldami dla wszystkich typów opakowań
    /// Z grupowaniem, sortowaniem, eksportem i alertami
    /// </summary>
    public class SaldaWszystkichOpakowanViewModel : ViewModelBase
    {
        private readonly OpakowaniaDataService _dataService;
        private readonly ExportService _exportService;
        private readonly UstawieniaService _ustawieniaService;
        private readonly string _userId;
        private string _handlowiecFilter;

        private DateTime _dataDo;
        private SaldoOpakowania _wybraneSaldo;
        private string _filtrTekstowy;
        private bool _isAdmin;
        private bool _grupujPoHandlowcu;
        private string _sortowaniePo = "SaldoCalkowite";
        private bool _sortowanieRosnaco = false;
        private bool _pokazujTylkoSwoich = true;

        private ObservableCollection<SaldoOpakowania> _salda;
        private ICollectionView _saldaView;

        // Statystyki
        private int _liczbaKontrahentow;
        private int _sumaE2;
        private int _sumaH1;
        private int _sumaEURO;
        private int _sumaPCV;
        private int _sumaDREW;

        // Progi
        private int _progOstrzezenia = 50;
        private int _progKrytyczny = 100;

        // Lista handlowców do filtra
        private ObservableCollection<string> _listaHandlowcow;
        private string _wybranyHandlowiec;

        // Szybkie filtry dat
        private ObservableCollection<FiltrDaty> _filtryDat;
        private FiltrDaty _wybranyFiltrDaty;

        public SaldaWszystkichOpakowanViewModel(string userId)
        {
            _userId = userId;
            _dataService = new OpakowaniaDataService();
            _exportService = new ExportService();
            _ustawieniaService = new UstawieniaService();

            _dataDo = DateTime.Today;

            Salda = new ObservableCollection<SaldoOpakowania>();
            ListaHandlowcow = new ObservableCollection<string> { "Wszyscy" };
            FiltryDat = new ObservableCollection<FiltrDaty>(FiltrDaty.GetFiltryDomyslne());

            // Komendy
            OdswiezCommand = new AsyncRelayCommand(OdswiezAsync);
            OtworzSzczegolyCommand = new RelayCommand(OtworzSzczegoly, _ => WybraneSaldo != null);
            EksportPDFCommand = new AsyncRelayCommand(EksportujPDFAsync);
            EksportExcelCommand = new AsyncRelayCommand(EksportujExcelAsync);
            DrukujCommand = new AsyncRelayCommand(DrukujAsync);
            ZadzwonCommand = new RelayCommand(Zadzwon, _ => WybraneSaldo != null && !string.IsNullOrEmpty(WybraneSaldo.Telefon));
            WyslijEmailCommand = new RelayCommand(WyslijEmail, _ => WybraneSaldo != null && !string.IsNullOrEmpty(WybraneSaldo.Email));
            ZapiszProgiCommand = new AsyncRelayCommand(ZapiszProgiAsync);
            ToggleGrupowanieCommand = new RelayCommand(_ => ToggleGrupowanie());
            UstawSortowanieCommand = new RelayCommand(UstawSortowanie);

            // Inicjalizacja
            InitializeAsync();
        }

        #region Properties

        public DateTime DataDo
        {
            get => _dataDo;
            set
            {
                if (SetProperty(ref _dataDo, value))
                {
                    // Resetuj wybrany filtr daty gdy zmieniono ręcznie
                    _wybranyFiltrDaty = null;
                    OnPropertyChanged(nameof(WybranyFiltrDaty));
                }
            }
        }

        public SaldoOpakowania WybraneSaldo
        {
            get => _wybraneSaldo;
            set
            {
                if (SetProperty(ref _wybraneSaldo, value))
                {
                    ((RelayCommand)OtworzSzczegolyCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)ZadzwonCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)WyslijEmailCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string FiltrTekstowy
        {
            get => _filtrTekstowy;
            set
            {
                if (SetProperty(ref _filtrTekstowy, value))
                    OdswiezWidok();
            }
        }

        public bool IsAdmin
        {
            get => _isAdmin;
            set => SetProperty(ref _isAdmin, value);
        }

        public bool GrupujPoHandlowcu
        {
            get => _grupujPoHandlowcu;
            set
            {
                if (SetProperty(ref _grupujPoHandlowcu, value))
                    OdswiezGrupowanie();
            }
        }

        public string SortowaniePo
        {
            get => _sortowaniePo;
            set
            {
                if (SetProperty(ref _sortowaniePo, value))
                    OdswiezSortowanie();
            }
        }

        public bool SortowanieRosnaco
        {
            get => _sortowanieRosnaco;
            set
            {
                if (SetProperty(ref _sortowanieRosnaco, value))
                    OdswiezSortowanie();
            }
        }

        public bool PokazujTylkoSwoich
        {
            get => _pokazujTylkoSwoich;
            set
            {
                if (SetProperty(ref _pokazujTylkoSwoich, value))
                    OdswiezCommand.Execute(null);
            }
        }

        public string NazwaHandlowca => string.IsNullOrEmpty(_handlowiecFilter) ? "Wszyscy" : _handlowiecFilter;

        public ObservableCollection<SaldoOpakowania> Salda
        {
            get => _salda;
            set => SetProperty(ref _salda, value);
        }

        public ICollectionView SaldaView
        {
            get => _saldaView;
            set => SetProperty(ref _saldaView, value);
        }

        public ObservableCollection<string> ListaHandlowcow
        {
            get => _listaHandlowcow;
            set => SetProperty(ref _listaHandlowcow, value);
        }

        public string WybranyHandlowiec
        {
            get => _wybranyHandlowiec;
            set
            {
                if (SetProperty(ref _wybranyHandlowiec, value))
                    OdswiezWidok();
            }
        }

        public ObservableCollection<FiltrDaty> FiltryDat
        {
            get => _filtryDat;
            set => SetProperty(ref _filtryDat, value);
        }

        public FiltrDaty WybranyFiltrDaty
        {
            get => _wybranyFiltrDaty;
            set
            {
                if (SetProperty(ref _wybranyFiltrDaty, value) && value != null)
                {
                    DataDo = value.DataDo;
                    OdswiezCommand.Execute(null);
                }
            }
        }

        // Statystyki
        public int LiczbaKontrahentow
        {
            get => _liczbaKontrahentow;
            set => SetProperty(ref _liczbaKontrahentow, value);
        }

        public int SumaE2
        {
            get => _sumaE2;
            set => SetProperty(ref _sumaE2, value);
        }

        public int SumaH1
        {
            get => _sumaH1;
            set => SetProperty(ref _sumaH1, value);
        }

        public int SumaEURO
        {
            get => _sumaEURO;
            set => SetProperty(ref _sumaEURO, value);
        }

        public int SumaPCV
        {
            get => _sumaPCV;
            set => SetProperty(ref _sumaPCV, value);
        }

        public int SumaDREW
        {
            get => _sumaDREW;
            set => SetProperty(ref _sumaDREW, value);
        }

        // Progi koloryzacji
        public int ProgOstrzezenia
        {
            get => _progOstrzezenia;
            set
            {
                if (SetProperty(ref _progOstrzezenia, value))
                {
                    SaldoOpakowania.ProgOstrzezenia = value;
                    OdswiezWidok();
                }
            }
        }

        public int ProgKrytyczny
        {
            get => _progKrytyczny;
            set
            {
                if (SetProperty(ref _progKrytyczny, value))
                {
                    SaldoOpakowania.ProgKrytyczny = value;
                    OdswiezWidok();
                }
            }
        }

        // Tekst dla przycisku grupowania
        public string TekstGrupowania => GrupujPoHandlowcu ? "Grupuj: WŁ" : "Grupuj: WYŁ";

        #endregion

        #region Commands

        public ICommand OdswiezCommand { get; }
        public ICommand OtworzSzczegolyCommand { get; }
        public ICommand EksportPDFCommand { get; }
        public ICommand EksportExcelCommand { get; }
        public ICommand DrukujCommand { get; }
        public ICommand ZadzwonCommand { get; }
        public ICommand WyslijEmailCommand { get; }
        public ICommand ZapiszProgiCommand { get; }
        public ICommand ToggleGrupowanieCommand { get; }
        public ICommand UstawSortowanieCommand { get; }

        #endregion

        #region Events

        public event Action<SaldoOpakowania> OtworzSzczegolyRequested;

        #endregion

        #region Methods

        private async void InitializeAsync()
        {
            await ExecuteAsync(async () =>
            {
                // Wczytaj ustawienia
                var ustawienia = await _ustawieniaService.WczytajUstawieniaAsync();
                ProgOstrzezenia = ustawienia.ProgOstrzezenia;
                ProgKrytyczny = ustawienia.ProgKrytyczny;
                GrupujPoHandlowcu = ustawienia.GrupujPoHandlowcu;
                _sortowaniePo = ustawienia.DomyslnySortowanie;
                _sortowanieRosnaco = ustawienia.SortowanieRosnaco;

                IsAdmin = _userId == "11111";

                if (!IsAdmin && ustawienia.PokazujTylkoSwoich)
                {
                    _handlowiecFilter = await _dataService.PobierzHandlowcaPoUserIdAsync(_userId);
                    _pokazujTylkoSwoich = true;
                }
                else if (!IsAdmin)
                {
                    // Pobierz handlowca dla użytkownika ale nie filtruj
                    var handlowiec = await _dataService.PobierzHandlowcaPoUserIdAsync(_userId);
                    _handlowiecFilter = null;
                    _wybranyHandlowiec = handlowiec;
                }

                OnPropertyChanged(nameof(NazwaHandlowca));
                OnPropertyChanged(nameof(PokazujTylkoSwoich));

                await PobierzDaneAsync();
            }, "Inicjalizacja...");
        }

        private async Task OdswiezAsync()
        {
            await ExecuteAsync(async () =>
            {
                await PobierzDaneAsync();
            }, "Odświeżanie danych...");
        }

        private async Task PobierzDaneAsync()
        {
            // Określ filtr handlowca
            string filtrHandlowca = null;
            if (!IsAdmin && PokazujTylkoSwoich)
            {
                filtrHandlowca = _handlowiecFilter;
            }
            else if (!string.IsNullOrEmpty(WybranyHandlowiec) && WybranyHandlowiec != "Wszyscy")
            {
                filtrHandlowca = WybranyHandlowiec;
            }

            // Pobierz wszystkie salda jednym zapytaniem (SZYBKO!)
            var salda = await _dataService.PobierzWszystkieSaldaAsync(DataDo, filtrHandlowca);

            Salda.Clear();
            var listaHandlowcow = new HashSet<string> { "Wszyscy" };

            foreach (var saldo in salda)
            {
                Salda.Add(saldo);
                if (!string.IsNullOrEmpty(saldo.Handlowiec) && saldo.Handlowiec != "-")
                    listaHandlowcow.Add(saldo.Handlowiec);
            }

            // Aktualizuj listę handlowców
            ListaHandlowcow.Clear();
            foreach (var h in listaHandlowcow.OrderBy(x => x == "Wszyscy" ? "" : x))
                ListaHandlowcow.Add(h);

            // Utwórz widok kolekcji
            SaldaView = CollectionViewSource.GetDefaultView(Salda);
            SaldaView.Filter = FiltrujSaldo;

            OdswiezGrupowanie();
            OdswiezSortowanie();
            ObliczStatystyki();
        }

        private bool FiltrujSaldo(object item)
        {
            if (item is SaldoOpakowania saldo)
            {
                // Filtr tekstowy
                if (!string.IsNullOrWhiteSpace(FiltrTekstowy))
                {
                    var filtr = FiltrTekstowy.ToLower();
                    if (!saldo.Kontrahent.ToLower().Contains(filtr) &&
                        !(saldo.Handlowiec?.ToLower().Contains(filtr) ?? false))
                        return false;
                }

                // Filtr handlowca (jeśli wybrano konkretnego)
                if (!string.IsNullOrEmpty(WybranyHandlowiec) && WybranyHandlowiec != "Wszyscy")
                {
                    if (saldo.Handlowiec != WybranyHandlowiec)
                        return false;
                }

                return true;
            }
            return false;
        }

        private void OdswiezWidok()
        {
            SaldaView?.Refresh();
            ObliczStatystyki();
        }

        private void OdswiezGrupowanie()
        {
            if (SaldaView == null) return;

            SaldaView.GroupDescriptions.Clear();
            if (GrupujPoHandlowcu)
            {
                SaldaView.GroupDescriptions.Add(new PropertyGroupDescription("Handlowiec"));
            }

            OnPropertyChanged(nameof(TekstGrupowania));
        }

        private void OdswiezSortowanie()
        {
            if (SaldaView == null) return;

            SaldaView.SortDescriptions.Clear();

            var kierunek = SortowanieRosnaco ? ListSortDirection.Ascending : ListSortDirection.Descending;
            SaldaView.SortDescriptions.Add(new SortDescription(SortowaniePo, kierunek));
        }

        private void ToggleGrupowanie()
        {
            GrupujPoHandlowcu = !GrupujPoHandlowcu;
        }

        private void UstawSortowanie(object parameter)
        {
            if (parameter is string kolumna)
            {
                if (SortowaniePo == kolumna)
                {
                    // Toggle kierunek
                    SortowanieRosnaco = !SortowanieRosnaco;
                }
                else
                {
                    SortowaniePo = kolumna;
                    SortowanieRosnaco = false; // Domyślnie malejąco
                }
            }
        }

        private void ObliczStatystyki()
        {
            var dane = Salda.Where(s => FiltrujSaldo(s)).ToList();

            LiczbaKontrahentow = dane.Count;
            SumaE2 = dane.Sum(s => s.SaldoE2);
            SumaH1 = dane.Sum(s => s.SaldoH1);
            SumaEURO = dane.Sum(s => s.SaldoEURO);
            SumaPCV = dane.Sum(s => s.SaldoPCV);
            SumaDREW = dane.Sum(s => s.SaldoDREW);
        }

        private void OtworzSzczegoly(object parameter)
        {
            if (parameter is SaldoOpakowania saldo)
            {
                OtworzSzczegolyRequested?.Invoke(saldo);
            }
            else if (WybraneSaldo != null)
            {
                OtworzSzczegolyRequested?.Invoke(WybraneSaldo);
            }
        }

        private async Task EksportujPDFAsync()
        {
            await ExecuteAsync(async () =>
            {
                var dane = Salda.Where(s => FiltrujSaldo(s)).ToList();
                var sciezka = await _exportService.EksportujSaldaWszystkichDoPDFAsync(dane, DataDo, WybranyHandlowiec);
                
                var result = MessageBox.Show(
                    $"Raport zapisany: {sciezka}\n\nCzy chcesz otworzyć plik?",
                    "Eksport PDF", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    _exportService.OtworzPlik(sciezka);

            }, "Eksport do PDF...");
        }

        private async Task EksportujExcelAsync()
        {
            await ExecuteAsync(async () =>
            {
                var dane = Salda.Where(s => FiltrujSaldo(s)).ToList();
                var sciezka = await _exportService.EksportujDoExcelAsync(dane, DataDo, WybranyHandlowiec);

                var result = MessageBox.Show(
                    $"Raport zapisany: {sciezka}\n\nCzy chcesz otworzyć plik?",
                    "Eksport Excel", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    _exportService.OtworzPlik(sciezka);

            }, "Eksport do Excel...");
        }

        private async Task DrukujAsync()
        {
            await ExecuteAsync(async () =>
            {
                var dane = Salda.Where(s => FiltrujSaldo(s)).ToList();
                var sciezka = await _exportService.EksportujSaldaWszystkichDoPDFAsync(dane, DataDo, WybranyHandlowiec);
                _exportService.OtworzPlik(sciezka);
            }, "Przygotowanie wydruku...");
        }

        private void Zadzwon(object parameter)
        {
            if (WybraneSaldo != null)
            {
                _exportService.Zadzwon(WybraneSaldo.Telefon);
            }
        }

        private void WyslijEmail(object parameter)
        {
            if (WybraneSaldo != null)
            {
                string temat = $"Saldo opakowań - {WybraneSaldo.Kontrahent}";
                string tresc = $"Szanowni Państwo,\n\nPrzesyłam informację o stanie opakowań:\n\n" +
                    $"E2: {WybraneSaldo.SaldoE2Tekst}\n" +
                    $"H1: {WybraneSaldo.SaldoH1Tekst}\n" +
                    $"EURO: {WybraneSaldo.SaldoEUROTekst}\n" +
                    $"PCV: {WybraneSaldo.SaldoPCVTekst}\n" +
                    $"DREW: {WybraneSaldo.SaldoDREWTekst}\n\n" +
                    $"Z poważaniem";

                _exportService.WyslijEmail(WybraneSaldo.Email, temat, tresc);
            }
        }

        private async Task ZapiszProgiAsync()
        {
            await _ustawieniaService.UstawProgiAsync(ProgOstrzezenia, ProgKrytyczny);
            
            // Zapisz też inne ustawienia
            var ustawienia = _ustawieniaService.Ustawienia;
            ustawienia.GrupujPoHandlowcu = GrupujPoHandlowcu;
            ustawienia.DomyslnySortowanie = SortowaniePo;
            ustawienia.SortowanieRosnaco = SortowanieRosnaco;
            ustawienia.PokazujTylkoSwoich = PokazujTylkoSwoich;
            await _ustawieniaService.ZapiszUstawieniaAsync();

            OdswiezWidok();
            MessageBox.Show("Ustawienia zapisane", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion
    }
}
