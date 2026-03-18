using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.ViewModels
{
    public enum ViewMode
    {
        WszystkieTypy,
        PerTyp
    }

    /// <summary>
    /// Informacja diagnostyczna o zaladowanych danych
    /// </summary>
    public class DiagInfo
    {
        public ViewMode Tryb { get; set; }
        public string Operacja { get; set; }
        public long CzasCalkowityMs { get; set; }
        public long CzasZapytaniaMs { get; set; }
        public long CzasUiMs { get; set; }
        public int LiczbaRekordow { get; set; }
        public bool ZCache { get; set; }
        public string TypOpakowania { get; set; }
        public string FiltrHandlowca { get; set; }
        public DateTime DataOd { get; set; }
        public DateTime DataDo { get; set; }
    }

    /// <summary>
    /// Scalony ViewModel dla okna opakowań - łączy SaldaWszystkichOpakowanViewModel i ZestawienieOpakowanViewModel
    /// </summary>
    public class OpakowaniaWindowViewModel : ViewModelBase
    {
        private readonly OpakowaniaDataService _dataService;
        private readonly ExportService _exportService;
        private readonly UstawieniaService _ustawieniaService;
        private readonly string _userId;
        private string _handlowiecFilter;

        #region View Mode

        private ViewMode _viewMode = ViewMode.WszystkieTypy;
        public ViewMode ViewMode
        {
            get => _viewMode;
            set
            {
                if (SetProperty(ref _viewMode, value))
                {
                    OnPropertyChanged(nameof(IsWszystkieTypy));
                    OnPropertyChanged(nameof(IsPerTyp));
                    OdswiezCommand.Execute(null);
                }
            }
        }

        public bool IsWszystkieTypy => ViewMode == ViewMode.WszystkieTypy;
        public bool IsPerTyp => ViewMode == ViewMode.PerTyp;

        #endregion

        #region Shared Properties

        private DateTime _dataOd;
        private DateTime _dataDo;
        private string _filtrTekstowy;
        private bool _isAdmin;
        private bool _pokazujTylkoSwoich = true;

        private ObservableCollection<FiltrDaty> _filtryDat;
        private FiltrDaty _wybranyFiltrDaty;
        private ObservableCollection<string> _listaHandlowcow;
        private string _wybranyHandlowiec;

        public DateTime DataOd
        {
            get => _dataOd;
            set
            {
                if (SetProperty(ref _dataOd, value))
                {
                    if (IsPerTyp) OdswiezCommand.Execute(null);
                }
            }
        }

        public DateTime DataDo
        {
            get => _dataDo;
            set
            {
                if (SetProperty(ref _dataDo, value))
                {
                    _wybranyFiltrDaty = null;
                    OnPropertyChanged(nameof(WybranyFiltrDaty));
                    OdswiezCommand.Execute(null);
                }
            }
        }

        public string FiltrTekstowy
        {
            get => _filtrTekstowy;
            set
            {
                if (SetProperty(ref _filtrTekstowy, value))
                {
                    if (IsWszystkieTypy)
                        OdswiezWidokWszystkieTypy();
                    else
                        FiltrujZestawienie();
                }
            }
        }

        public bool IsAdmin
        {
            get => _isAdmin;
            set => SetProperty(ref _isAdmin, value);
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
                    _dataOd = value.DataOd;
                    _dataDo = value.DataDo;
                    OnPropertyChanged(nameof(DataOd));
                    OnPropertyChanged(nameof(DataDo));
                    OdswiezCommand.Execute(null);
                }
            }
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
                {
                    if (IsWszystkieTypy)
                        OdswiezWidokWszystkieTypy();
                    else
                        OdswiezCommand.Execute(null);
                }
            }
        }

        #endregion

        #region WszystkieTypy Properties

        private ObservableCollection<SaldoOpakowania> _salda;
        private ICollectionView _saldaView;
        private SaldoOpakowania _wybraneSaldo;
        private bool _grupujPoHandlowcu;
        private string _sortowaniePo = "SaldoCalkowite";
        private bool _sortowanieRosnaco = false;
        private int _progOstrzezenia = 50;
        private int _progKrytyczny = 100;

        // Statystyki - WszystkieTypy
        private int _liczbaKontrahentow;
        private int _sumaE2;
        private int _sumaH1;
        private int _sumaEURO;
        private int _sumaPCV;
        private int _sumaDREW;

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

        public SaldoOpakowania WybraneSaldo
        {
            get => _wybraneSaldo;
            set
            {
                if (SetProperty(ref _wybraneSaldo, value))
                {
                    ((RelayCommand)OtworzSzczegolyWszystkieCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)ZadzwonCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)WyslijEmailCommand).RaiseCanExecuteChanged();
                }
            }
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

        public int ProgOstrzezenia
        {
            get => _progOstrzezenia;
            set
            {
                if (SetProperty(ref _progOstrzezenia, value))
                {
                    SaldoOpakowania.ProgOstrzezenia = value;
                    OdswiezWidokWszystkieTypy();
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
                    OdswiezWidokWszystkieTypy();
                }
            }
        }

        public string TekstGrupowania => GrupujPoHandlowcu ? "Grupuj: WL" : "Grupuj: WYL";

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

        #endregion

        #region PerTyp Properties

        private TypOpakowania _wybranyTypOpakowania;
        private ZestawienieSalda _wybranyKontrahentPerTyp;
        private bool _pokazTylkoNiepotwierdzone;
        private ObservableCollection<ZestawienieSalda> _zestawienie;
        private ObservableCollection<ZestawienieSalda> _zestawienieFiltrowane;

        // Statystyki - PerTyp
        private int _liczbaKontrahentowPerTyp;
        private int _sumaSaldDodatnich;
        private int _sumaSaldUjemnych;
        private int _liczbaPotwierdzen;

        public TypOpakowania WybranyTypOpakowania
        {
            get => _wybranyTypOpakowania;
            set
            {
                if (SetProperty(ref _wybranyTypOpakowania, value))
                {
                    OnPropertyChanged(nameof(TytulTypuOpakowania));
                    if (IsPerTyp) OdswiezCommand.Execute(null);
                }
            }
        }

        public TypOpakowania[] WszystkieTypyOpakowan => TypOpakowania.WszystkieTypy;
        public string TytulTypuOpakowania => WybranyTypOpakowania?.Nazwa ?? "Opakowania";

        public ZestawienieSalda WybranyKontrahentPerTyp
        {
            get => _wybranyKontrahentPerTyp;
            set
            {
                if (SetProperty(ref _wybranyKontrahentPerTyp, value))
                {
                    ((RelayCommand)OtworzSzczegolyPerTypCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DodajPotwierdzenieCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool PokazTylkoNiepotwierdzone
        {
            get => _pokazTylkoNiepotwierdzone;
            set
            {
                if (SetProperty(ref _pokazTylkoNiepotwierdzone, value))
                    FiltrujZestawienie();
            }
        }

        public ObservableCollection<ZestawienieSalda> Zestawienie
        {
            get => _zestawienie;
            set => SetProperty(ref _zestawienie, value);
        }

        public ObservableCollection<ZestawienieSalda> ZestawienieFiltrowane
        {
            get => _zestawienieFiltrowane;
            set => SetProperty(ref _zestawienieFiltrowane, value);
        }

        public int LiczbaKontrahentowPerTyp
        {
            get => _liczbaKontrahentowPerTyp;
            set => SetProperty(ref _liczbaKontrahentowPerTyp, value);
        }

        public int SumaSaldDodatnich
        {
            get => _sumaSaldDodatnich;
            set => SetProperty(ref _sumaSaldDodatnich, value);
        }

        public int SumaSaldUjemnych
        {
            get => _sumaSaldUjemnych;
            set => SetProperty(ref _sumaSaldUjemnych, value);
        }

        public int LiczbaPotwierdzen
        {
            get => _liczbaPotwierdzen;
            set => SetProperty(ref _liczbaPotwierdzen, value);
        }

        #endregion

        #region Commands

        public ICommand OdswiezCommand { get; }
        public ICommand ToggleViewCommand { get; }

        // WszystkieTypy
        public ICommand OtworzSzczegolyWszystkieCommand { get; }
        public ICommand ToggleGrupowanieCommand { get; }
        public ICommand UstawSortowanieCommand { get; }
        public ICommand ZapiszProgiCommand { get; }
        public ICommand ZadzwonCommand { get; }
        public ICommand WyslijEmailCommand { get; }

        // PerTyp
        public ICommand OtworzSzczegolyPerTypCommand { get; }
        public ICommand DodajPotwierdzenieCommand { get; }
        public ICommand WybierzTypOpakowaniCommand { get; }
        public ICommand GenerujZestawieniePDFCommand { get; }
        public ICommand GenerujPDFiWyslijCommand { get; }

        // Shared
        public ICommand EksportPDFCommand { get; }
        public ICommand EksportExcelCommand { get; }
        public ICommand DrukujCommand { get; }

        #endregion

        #region Events

        public event Action<SaldoOpakowania> OtworzSzczegolyWszystkieRequested;
        public event Action<ZestawienieSalda> OtworzSzczegolyPerTypRequested;
        public event Action<ZestawienieSalda, TypOpakowania> DodajPotwierdzenieRequested;

        /// <summary>
        /// Zdarzenie diagnostyczne - informuje o zakonczeniu ladowania danych z timingami
        /// </summary>
        public event Action<DiagInfo> DiagnostykaDaneZaladowane;

        #endregion

        #region Constructor

        public OpakowaniaWindowViewModel(string userId, string domyslnyWidok = null)
        {
            _userId = userId;
            _dataService = new OpakowaniaDataService();
            _exportService = new ExportService();
            _ustawieniaService = new UstawieniaService();

            var (dataOd, dataDo) = UstawieniaService.GetDomyslnyOkres();
            _dataOd = dataOd;
            _dataDo = dataDo;
            _wybranyTypOpakowania = TypOpakowania.WszystkieTypy[0];

            Salda = new ObservableCollection<SaldoOpakowania>();
            Zestawienie = new ObservableCollection<ZestawienieSalda>();
            ZestawienieFiltrowane = new ObservableCollection<ZestawienieSalda>();
            ListaHandlowcow = new ObservableCollection<string> { "Wszyscy" };
            FiltryDat = new ObservableCollection<FiltrDaty>(FiltrDaty.GetFiltryDomyslne());

            // Commands
            OdswiezCommand = new AsyncRelayCommand(OdswiezAsync);
            ToggleViewCommand = new RelayCommand(_ => ToggleView());

            // WszystkieTypy
            OtworzSzczegolyWszystkieCommand = new RelayCommand(OtworzSzczegolyWszystkie, _ => WybraneSaldo != null);
            ToggleGrupowanieCommand = new RelayCommand(_ => GrupujPoHandlowcu = !GrupujPoHandlowcu);
            UstawSortowanieCommand = new RelayCommand(UstawSortowanie);
            ZapiszProgiCommand = new AsyncRelayCommand(ZapiszProgiAsync);
            ZadzwonCommand = new RelayCommand(Zadzwon, _ => WybraneSaldo != null && !string.IsNullOrEmpty(WybraneSaldo.Telefon));
            WyslijEmailCommand = new RelayCommand(WyslijEmail, _ => WybraneSaldo != null && !string.IsNullOrEmpty(WybraneSaldo.Email));

            // PerTyp
            OtworzSzczegolyPerTypCommand = new RelayCommand(OtworzSzczegolyPerTyp, _ => WybranyKontrahentPerTyp != null);
            DodajPotwierdzenieCommand = new RelayCommand(DodajPotwierdzenie, _ => WybranyKontrahentPerTyp != null);
            WybierzTypOpakowaniCommand = new RelayCommand(WybierzTypOpakowania);
            GenerujZestawieniePDFCommand = new AsyncRelayCommand(GenerujZestawieniePDFAsync);
            GenerujPDFiWyslijCommand = new AsyncRelayCommand(GenerujPDFiWyslijAsync);

            // Shared
            EksportPDFCommand = new AsyncRelayCommand(EksportujPDFAsync);
            EksportExcelCommand = new AsyncRelayCommand(EksportujExcelAsync);
            DrukujCommand = new AsyncRelayCommand(DrukujAsync);

            // Set initial view mode
            if (domyslnyWidok == "PerTyp")
                _viewMode = ViewMode.PerTyp;

            InitializeAsync();
        }

        #endregion

        #region Initialization

        private async void InitializeAsync()
        {
            await ExecuteAsync(async () =>
            {
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
                    var handlowiec = await _dataService.PobierzHandlowcaPoUserIdAsync(_userId);
                    _handlowiecFilter = null;
                    _wybranyHandlowiec = handlowiec;
                }

                OnPropertyChanged(nameof(NazwaHandlowca));
                OnPropertyChanged(nameof(PokazujTylkoSwoich));

                await PobierzDaneAsync();
            }, "Inicjalizacja...");
        }

        #endregion

        #region Data Loading

        private async Task OdswiezAsync()
        {
            await ExecuteAsync(async () =>
            {
                await PobierzDaneAsync();
            }, "Odswiezanie danych...");
        }

        private async Task PobierzDaneAsync()
        {
            if (IsWszystkieTypy)
                await PobierzDaneWszystkieTypyAsync();
            else
                await PobierzDanePerTypAsync();
        }

        private async Task PobierzDaneWszystkieTypyAsync()
        {
            var swTotal = Stopwatch.StartNew();
            string filtrHandlowca = OkreslFiltrHandlowca();

            // Pomiar zapytania SQL
            // Uzywa SaldaService (agresywny cache 8h + CTE + NOLOCK + MAXDOP 4)
            // zamiast OpakowaniaDataService.PobierzWszystkieSaldaAsync (osobny cache 4h, wolniejsze)
            var swQuery = Stopwatch.StartNew();
            var saldaKontrahentow = await _dataService.PobierzSaldaKontrahentowAsync(DataDo, filtrHandlowca);
            swQuery.Stop();

            // Pomiar budowania UI — mapowanie SaldoKontrahenta -> SaldoOpakowania
            var swUi = Stopwatch.StartNew();
            Salda.Clear();
            var listaHandlowcow = new HashSet<string> { "Wszyscy" };

            foreach (var sk in saldaKontrahentow)
            {
                var saldo = new SaldoOpakowania
                {
                    Kontrahent = sk.Kontrahent,
                    KontrahentId = sk.Id,
                    Handlowiec = sk.Handlowiec ?? "-",
                    SaldoE2 = sk.E2,
                    SaldoH1 = sk.H1,
                    SaldoEURO = sk.EURO,
                    SaldoPCV = sk.PCV,
                    SaldoDREW = sk.DREW,
                    Email = sk.Email,
                    Telefon = sk.Telefon
                };
                Salda.Add(saldo);
                if (!string.IsNullOrEmpty(sk.Handlowiec) && sk.Handlowiec != "-")
                    listaHandlowcow.Add(sk.Handlowiec);
            }

            ListaHandlowcow.Clear();
            foreach (var h in listaHandlowcow.OrderBy(x => x == "Wszyscy" ? "" : x))
                ListaHandlowcow.Add(h);

            SaldaView = CollectionViewSource.GetDefaultView(Salda);
            SaldaView.Filter = FiltrujSaldo;

            OdswiezGrupowanie();
            OdswiezSortowanie();
            ObliczStatystykiWszystkieTypy();
            swUi.Stop();

            swTotal.Stop();
            PerformanceProfiler.RecordTiming("WszystkieTypy_TOTAL", swTotal.Elapsed, saldaKontrahentow.Count);
            PerformanceProfiler.RecordTiming("WszystkieTypy_SQL", swQuery.Elapsed, saldaKontrahentow.Count);
            PerformanceProfiler.RecordTiming("WszystkieTypy_UI", swUi.Elapsed, saldaKontrahentow.Count);

            DiagnostykaDaneZaladowane?.Invoke(new DiagInfo
            {
                Tryb = ViewMode.WszystkieTypy,
                Operacja = "PobierzSaldaKontrahentow",
                CzasCalkowityMs = swTotal.ElapsedMilliseconds,
                CzasZapytaniaMs = swQuery.ElapsedMilliseconds,
                CzasUiMs = swUi.ElapsedMilliseconds,
                LiczbaRekordow = saldaKontrahentow.Count,
                ZCache = swQuery.ElapsedMilliseconds < 50,
                FiltrHandlowca = filtrHandlowca,
                DataDo = DataDo
            });
        }

        private async Task PobierzDanePerTypAsync()
        {
            if (WybranyTypOpakowania == null) return;

            var swTotal = Stopwatch.StartNew();
            string filtrHandlowca = OkreslFiltrHandlowca();

            // Pomiar zapytania SQL
            var swQuery = Stopwatch.StartNew();
            var dane = await _dataService.PobierzZestawienieSaldAsync(
                DataOd, DataDo,
                WybranyTypOpakowania.NazwaSystemowa,
                filtrHandlowca);
            swQuery.Stop();

            // Pomiar budowania UI
            var swUi = Stopwatch.StartNew();
            Zestawienie.Clear();
            foreach (var item in dane)
                Zestawienie.Add(item);

            FiltrujZestawienie();
            ObliczStatystykiPerTyp();
            swUi.Stop();

            swTotal.Stop();
            var opName = $"PerTyp_{WybranyTypOpakowania.Kod}";
            PerformanceProfiler.RecordTiming($"{opName}_TOTAL", swTotal.Elapsed, dane.Count);
            PerformanceProfiler.RecordTiming($"{opName}_SQL", swQuery.Elapsed, dane.Count);
            PerformanceProfiler.RecordTiming($"{opName}_UI", swUi.Elapsed, dane.Count);

            DiagnostykaDaneZaladowane?.Invoke(new DiagInfo
            {
                Tryb = ViewMode.PerTyp,
                Operacja = $"PobierzZestawienie_{WybranyTypOpakowania.Kod}",
                CzasCalkowityMs = swTotal.ElapsedMilliseconds,
                CzasZapytaniaMs = swQuery.ElapsedMilliseconds,
                CzasUiMs = swUi.ElapsedMilliseconds,
                LiczbaRekordow = dane.Count,
                ZCache = swQuery.ElapsedMilliseconds < 50,
                TypOpakowania = WybranyTypOpakowania.Kod,
                FiltrHandlowca = filtrHandlowca,
                DataOd = DataOd,
                DataDo = DataDo
            });
        }

        private string OkreslFiltrHandlowca()
        {
            if (!IsAdmin && PokazujTylkoSwoich)
                return _handlowiecFilter;
            if (!string.IsNullOrEmpty(WybranyHandlowiec) && WybranyHandlowiec != "Wszyscy")
                return WybranyHandlowiec;
            return null;
        }

        #endregion

        #region WszystkieTypy Logic

        private bool FiltrujSaldo(object item)
        {
            if (item is SaldoOpakowania saldo)
            {
                if (!string.IsNullOrWhiteSpace(FiltrTekstowy))
                {
                    var filtr = FiltrTekstowy.ToLower();
                    if (!saldo.Kontrahent.ToLower().Contains(filtr) &&
                        !(saldo.Handlowiec?.ToLower().Contains(filtr) ?? false))
                        return false;
                }

                if (!string.IsNullOrEmpty(WybranyHandlowiec) && WybranyHandlowiec != "Wszyscy")
                {
                    if (saldo.Handlowiec != WybranyHandlowiec)
                        return false;
                }

                return true;
            }
            return false;
        }

        private void OdswiezWidokWszystkieTypy()
        {
            SaldaView?.Refresh();
            ObliczStatystykiWszystkieTypy();
        }

        private void OdswiezGrupowanie()
        {
            if (SaldaView == null) return;

            SaldaView.GroupDescriptions.Clear();
            if (GrupujPoHandlowcu)
                SaldaView.GroupDescriptions.Add(new PropertyGroupDescription("Handlowiec"));

            OnPropertyChanged(nameof(TekstGrupowania));
        }

        private void OdswiezSortowanie()
        {
            if (SaldaView == null) return;

            SaldaView.SortDescriptions.Clear();
            var kierunek = SortowanieRosnaco ? ListSortDirection.Ascending : ListSortDirection.Descending;
            SaldaView.SortDescriptions.Add(new SortDescription(SortowaniePo, kierunek));
        }

        private void UstawSortowanie(object parameter)
        {
            if (parameter is string kolumna)
            {
                if (SortowaniePo == kolumna)
                    SortowanieRosnaco = !SortowanieRosnaco;
                else
                {
                    SortowaniePo = kolumna;
                    SortowanieRosnaco = false;
                }
            }
        }

        private void ObliczStatystykiWszystkieTypy()
        {
            var dane = Salda.Where(s => FiltrujSaldo(s)).ToList();

            LiczbaKontrahentow = dane.Count;
            SumaE2 = dane.Sum(s => s.SaldoE2);
            SumaH1 = dane.Sum(s => s.SaldoH1);
            SumaEURO = dane.Sum(s => s.SaldoEURO);
            SumaPCV = dane.Sum(s => s.SaldoPCV);
            SumaDREW = dane.Sum(s => s.SaldoDREW);
        }

        #endregion

        #region PerTyp Logic

        private void FiltrujZestawienie()
        {
            var filtrowane = Zestawienie.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(FiltrTekstowy))
            {
                var filtr = FiltrTekstowy.ToLower();
                filtrowane = filtrowane.Where(z =>
                    z.Kontrahent.ToLower().Contains(filtr) ||
                    z.Handlowiec.ToLower().Contains(filtr));
            }

            if (PokazTylkoNiepotwierdzone)
                filtrowane = filtrowane.Where(z => !z.JestPotwierdzone);

            ZestawienieFiltrowane.Clear();
            foreach (var item in filtrowane)
                ZestawienieFiltrowane.Add(item);

            ObliczStatystykiPerTyp();
        }

        private void ObliczStatystykiPerTyp()
        {
            var daneDoStatystyk = ZestawienieFiltrowane.Where(z => z.Kontrahent != "Suma").ToList();

            LiczbaKontrahentowPerTyp = daneDoStatystyk.Count;
            SumaSaldDodatnich = daneDoStatystyk.Where(z => z.IloscDrugiZakres > 0).Sum(z => z.IloscDrugiZakres);
            SumaSaldUjemnych = daneDoStatystyk.Where(z => z.IloscDrugiZakres < 0).Sum(z => Math.Abs(z.IloscDrugiZakres));
            LiczbaPotwierdzen = daneDoStatystyk.Count(z => z.JestPotwierdzone);
        }

        private void WybierzTypOpakowania(object parameter)
        {
            if (parameter is string kod)
            {
                WybranyTypOpakowania = TypOpakowania.WszystkieTypy.FirstOrDefault(t => t.Kod == kod)
                    ?? TypOpakowania.WszystkieTypy[0];
            }
            else if (parameter is TypOpakowania typ)
            {
                WybranyTypOpakowania = typ;
            }
        }

        #endregion

        #region View Toggle

        private void ToggleView()
        {
            ViewMode = ViewMode == ViewMode.WszystkieTypy ? ViewMode.PerTyp : ViewMode.WszystkieTypy;
        }

        #endregion

        #region Open Details / Confirm

        private void OtworzSzczegolyWszystkie(object parameter)
        {
            if (parameter is SaldoOpakowania saldo)
                OtworzSzczegolyWszystkieRequested?.Invoke(saldo);
            else if (WybraneSaldo != null)
                OtworzSzczegolyWszystkieRequested?.Invoke(WybraneSaldo);
        }

        private void OtworzSzczegolyPerTyp(object parameter)
        {
            if (WybranyKontrahentPerTyp != null && WybranyKontrahentPerTyp.KontrahentId > 0)
                OtworzSzczegolyPerTypRequested?.Invoke(WybranyKontrahentPerTyp);
        }

        private void DodajPotwierdzenie(object parameter)
        {
            if (WybranyKontrahentPerTyp != null && WybranyKontrahentPerTyp.KontrahentId > 0)
                DodajPotwierdzenieRequested?.Invoke(WybranyKontrahentPerTyp, WybranyTypOpakowania);
        }

        private void Zadzwon(object parameter)
        {
            if (WybraneSaldo != null)
                _exportService.Zadzwon(WybraneSaldo.Telefon);
        }

        private void WyslijEmail(object parameter)
        {
            if (WybraneSaldo != null)
            {
                string temat = $"Saldo opakowan - {WybraneSaldo.Kontrahent}";
                string tresc = $"Szanowni Panstwo,\n\nPrzesylam informacje o stanie opakowan:\n\n" +
                    $"E2: {WybraneSaldo.SaldoE2Tekst}\n" +
                    $"H1: {WybraneSaldo.SaldoH1Tekst}\n" +
                    $"EURO: {WybraneSaldo.SaldoEUROTekst}\n" +
                    $"PCV: {WybraneSaldo.SaldoPCVTekst}\n" +
                    $"DREW: {WybraneSaldo.SaldoDREWTekst}\n\n" +
                    $"Z powazaniem";
                _exportService.WyslijEmail(WybraneSaldo.Email, temat, tresc);
            }
        }

        #endregion

        #region Export

        private SaldoOpakowania MapToSaldoOpakowania(SaldoKontrahenta s) => new SaldoOpakowania
        {
            Kontrahent = s.Kontrahent,
            KontrahentId = s.Id,
            SaldoE2 = s.E2,
            SaldoH1 = s.H1,
            SaldoEURO = s.EURO,
            SaldoPCV = s.PCV,
            SaldoDREW = s.DREW,
            Handlowiec = s.Handlowiec
        };

        private async Task EksportujPDFAsync()
        {
            await ExecuteAsync(async () =>
            {
                if (IsWszystkieTypy)
                {
                    var dane = Salda.Where(s => FiltrujSaldo(s)).ToList();
                    var sciezka = await _exportService.EksportujSaldaWszystkichDoPDFAsync(dane, DataDo, WybranyHandlowiec);

                    var result = MessageBox.Show(
                        $"Raport zapisany: {sciezka}\n\nCzy chcesz otworzyc plik?",
                        "Eksport PDF", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                        _exportService.OtworzPlik(sciezka);
                }
                else
                {
                    if (WybranyKontrahentPerTyp != null && WybranyKontrahentPerTyp.KontrahentId > 0)
                    {
                        var dokumenty = await _dataService.PobierzSaldoKontrahentaAsync(
                            WybranyKontrahentPerTyp.KontrahentId, DataOd, DataDo);
                        var saldo = await _dataService.PobierzSaldaWszystkichOpakowannAsync(
                            WybranyKontrahentPerTyp.KontrahentId, DataDo);

                        var sciezka = await _exportService.EksportujSaldoKontrahentaDoPDFAsync(
                            WybranyKontrahentPerTyp.Kontrahent,
                            WybranyKontrahentPerTyp.KontrahentId,
                            saldo,
                            dokumenty.ToList(),
                            DataOd,
                            DataDo);

                        var result = MessageBox.Show(
                            $"Raport PDF zapisany:\n{sciezka}\n\nCzy chcesz otworzyc plik?",
                            "Eksport PDF", MessageBoxButton.YesNo, MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                            _exportService.OtworzPlik(sciezka);
                    }
                    else
                    {
                        MessageBox.Show("Wybierz kontrahenta, aby wygenerowac PDF.", "Generowanie PDF", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }, "Eksport do PDF...");
        }

        private async Task GenerujZestawieniePDFAsync()
        {
            if (ZestawienieFiltrowane == null || !ZestawienieFiltrowane.Any())
            {
                MessageBox.Show("Brak danych do eksportu.", "Generowanie PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await ExecuteAsync(async () =>
            {
                var sciezka = await _exportService.EksportujZestawienieDoPDFAsync(
                    ZestawienieFiltrowane.ToList(),
                    WybranyTypOpakowania,
                    DataOd,
                    DataDo,
                    _handlowiecFilter);

                var result = MessageBox.Show(
                    $"Zestawienie PDF zapisane:\n{sciezka}\n\nCzy chcesz otworzyc plik?",
                    "Eksport PDF", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    _exportService.OtworzPlik(sciezka);

            }, "Generowanie zestawienia PDF...");
        }

        private async Task GenerujPDFiWyslijAsync()
        {
            if (WybranyKontrahentPerTyp == null || WybranyKontrahentPerTyp.KontrahentId <= 0)
            {
                MessageBox.Show("Wybierz kontrahenta, aby wygenerowac PDF i wyslac email.", "PDF + Email", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await ExecuteAsync(async () =>
            {
                var dokumenty = await _dataService.PobierzSaldoKontrahentaAsync(
                    WybranyKontrahentPerTyp.KontrahentId, DataOd, DataDo);
                var saldo = await _dataService.PobierzSaldaWszystkichOpakowannAsync(
                    WybranyKontrahentPerTyp.KontrahentId, DataDo);

                await _exportService.GenerujPDFiWyslijEmailAsync(
                    WybranyKontrahentPerTyp.Kontrahent,
                    WybranyKontrahentPerTyp.KontrahentId,
                    saldo,
                    dokumenty.ToList(),
                    DataOd,
                    DataDo);

            }, "Generowanie PDF i przygotowywanie emaila...");
        }

        private async Task EksportujExcelAsync()
        {
            await ExecuteAsync(async () =>
            {
                var dane = Salda.Where(s => FiltrujSaldo(s)).ToList();
                var sciezka = await _exportService.EksportujDoExcelAsync(dane, DataDo, WybranyHandlowiec);

                var result = MessageBox.Show(
                    $"Raport zapisany: {sciezka}\n\nCzy chcesz otworzyc plik?",
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

        private async Task ZapiszProgiAsync()
        {
            await _ustawieniaService.UstawProgiAsync(ProgOstrzezenia, ProgKrytyczny);

            var ustawienia = _ustawieniaService.Ustawienia;
            ustawienia.GrupujPoHandlowcu = GrupujPoHandlowcu;
            ustawienia.DomyslnySortowanie = SortowaniePo;
            ustawienia.SortowanieRosnaco = SortowanieRosnaco;
            ustawienia.PokazujTylkoSwoich = PokazujTylkoSwoich;
            ustawienia.DomyslnyWidok = ViewMode == ViewMode.WszystkieTypy ? "WszystkieTypy" : "PerTyp";
            await _ustawieniaService.ZapiszUstawieniaAsync();

            OdswiezWidokWszystkieTypy();
            MessageBox.Show("Ustawienia zapisane", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion
    }
}
