using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.ViewModels
{
    /// <summary>
    /// ViewModel dla widoku zestawienia opakowań wszystkich kontrahentów
    /// </summary>
    public class ZestawienieOpakowanViewModel : ViewModelBase
    {
        private readonly OpakowaniaDataService _dataService;
        private readonly ExportService _exportService;
        private readonly string _userId;
        private string _handlowiecFilter;

        private DateTime _dataOd;
        private DateTime _dataDo;
        private TypOpakowania _wybranyTypOpakowania;
        private ZestawienieSalda _wybranyKontrahent;
        private string _filtrTekstowy;
        private bool _pokazTylkoNiepotwierdzone;
        private bool _isAdmin;

        private ObservableCollection<ZestawienieSalda> _zestawienie;
        private ObservableCollection<ZestawienieSalda> _zestawienieFiltrowane;

        // Statystyki
        private int _liczbKontrahentow;
        private int _sumaSaldDodatnich;
        private int _sumaSaldUjemnych;
        private int _liczbaPotwierdzen;

        public ZestawienieOpakowanViewModel(string userId)
        {
            _userId = userId;
            _dataService = new OpakowaniaDataService();
            _exportService = new ExportService();

            // Domyślne wartości - OD = 2 miesiące wstecz, DO = dzisiaj
            var (dataOd, dataDo) = UstawieniaService.GetDomyslnyOkres();
            _dataOd = dataOd;
            _dataDo = dataDo;
            _wybranyTypOpakowania = TypOpakowania.WszystkieTypy[0]; // Pojemnik E2

            Zestawienie = new ObservableCollection<ZestawienieSalda>();
            ZestawienieFiltrowane = new ObservableCollection<ZestawienieSalda>();

            // Komendy
            OdswiezCommand = new AsyncRelayCommand(OdswiezAsync);
            GenerujPDFCommand = new AsyncRelayCommand(GenerujPDFAsync);
            GenerujZestawieniePDFCommand = new AsyncRelayCommand(GenerujZestawieniePDFAsync);
            OtworzSzczegolyCommand = new RelayCommand(OtworzSzczegoly, _ => WybranyKontrahent != null);
            DodajPotwierdzenieCommand = new RelayCommand(DodajPotwierdzenie, _ => WybranyKontrahent != null);
            WybierzTypOpakowaniCommand = new RelayCommand(WybierzTypOpakowania);

            // Inicjalizacja
            InitializeAsync();
        }

        #region Properties

        public DateTime DataOd
        {
            get => _dataOd;
            set
            {
                if (SetProperty(ref _dataOd, value))
                    OdswiezCommand.Execute(null);
            }
        }

        public DateTime DataDo
        {
            get => _dataDo;
            set
            {
                if (SetProperty(ref _dataDo, value))
                    OdswiezCommand.Execute(null);
            }
        }

        public TypOpakowania WybranyTypOpakowania
        {
            get => _wybranyTypOpakowania;
            set
            {
                if (SetProperty(ref _wybranyTypOpakowania, value))
                {
                    OnPropertyChanged(nameof(TytulTypuOpakowania));
                    OdswiezCommand.Execute(null);
                }
            }
        }

        public TypOpakowania[] WszystkieTypyOpakowan => TypOpakowania.WszystkieTypy;

        public string TytulTypuOpakowania => WybranyTypOpakowania?.Nazwa ?? "Opakowania";

        public ZestawienieSalda WybranyKontrahent
        {
            get => _wybranyKontrahent;
            set
            {
                if (SetProperty(ref _wybranyKontrahent, value))
                {
                    ((RelayCommand)OtworzSzczegolyCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DodajPotwierdzenieCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string FiltrTekstowy
        {
            get => _filtrTekstowy;
            set
            {
                if (SetProperty(ref _filtrTekstowy, value))
                    FiltrujZestawienie();
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

        public bool IsAdmin
        {
            get => _isAdmin;
            set => SetProperty(ref _isAdmin, value);
        }

        public string NazwaHandlowca => string.IsNullOrEmpty(_handlowiecFilter) ? "Wszyscy" : _handlowiecFilter;

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

        // Statystyki
        public int LiczbaKontrahentow
        {
            get => _liczbKontrahentow;
            set => SetProperty(ref _liczbKontrahentow, value);
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
        public ICommand GenerujPDFCommand { get; }
        public ICommand GenerujZestawieniePDFCommand { get; }
        public ICommand OtworzSzczegolyCommand { get; }
        public ICommand DodajPotwierdzenieCommand { get; }
        public ICommand WybierzTypOpakowaniCommand { get; }

        #endregion

        #region Events

        public event Action<ZestawienieSalda> OtworzSzczegolyRequested;
        public event Action<ZestawienieSalda, TypOpakowania> DodajPotwierdzenieRequested;

        #endregion

        #region Methods

        private async void InitializeAsync()
        {
            await ExecuteAsync(async () =>
            {
                // Sprawdź uprawnienia użytkownika
                IsAdmin = _userId == "11111";

                if (!IsAdmin)
                {
                    _handlowiecFilter = await _dataService.PobierzHandlowcaPoUserIdAsync(_userId);
                }

                OnPropertyChanged(nameof(NazwaHandlowca));

                // Pobierz dane
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
            if (WybranyTypOpakowania == null) return;

            var dane = await _dataService.PobierzZestawienieSaldAsync(
                DataOd, DataDo,
                WybranyTypOpakowania.NazwaSystemowa,
                _handlowiecFilter);

            Zestawienie.Clear();
            foreach (var item in dane)
            {
                Zestawienie.Add(item);
            }

            FiltrujZestawienie();
            ObliczStatystyki();
        }

        private void FiltrujZestawienie()
        {
            var filtrowane = Zestawienie.AsEnumerable();

            // Filtr tekstowy
            if (!string.IsNullOrWhiteSpace(FiltrTekstowy))
            {
                var filtr = FiltrTekstowy.ToLower();
                filtrowane = filtrowane.Where(z =>
                    z.Kontrahent.ToLower().Contains(filtr) ||
                    z.Handlowiec.ToLower().Contains(filtr));
            }

            // Filtr potwierdzeń
            if (PokazTylkoNiepotwierdzone)
            {
                filtrowane = filtrowane.Where(z => !z.JestPotwierdzone);
            }

            ZestawienieFiltrowane.Clear();
            foreach (var item in filtrowane)
            {
                ZestawienieFiltrowane.Add(item);
            }
        }

        private void ObliczStatystyki()
        {
            var daneDoStatystyk = ZestawienieFiltrowane.Where(z => z.Kontrahent != "Suma").ToList();

            LiczbaKontrahentow = daneDoStatystyk.Count;
            SumaSaldDodatnich = daneDoStatystyk.Where(z => z.IloscDrugiZakres > 0).Sum(z => z.IloscDrugiZakres);
            SumaSaldUjemnych = daneDoStatystyk.Where(z => z.IloscDrugiZakres < 0).Sum(z => Math.Abs(z.IloscDrugiZakres));
            LiczbaPotwierdzen = daneDoStatystyk.Count(z => z.JestPotwierdzone);
        }

        private async Task GenerujPDFAsync()
        {
            if (WybranyKontrahent == null || WybranyKontrahent.KontrahentId <= 0)
            {
                MessageBox.Show("Wybierz kontrahenta, aby wygenerować PDF.", "Generowanie PDF", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await ExecuteAsync(async () =>
            {
                // Pobierz szczegółowe dane kontrahenta
                var dokumenty = await _dataService.PobierzSaldoKontrahentaAsync(
                    WybranyKontrahent.KontrahentId, DataOd, DataDo);
                var saldo = await _dataService.PobierzSaldaWszystkichOpakowannAsync(
                    WybranyKontrahent.KontrahentId, DataDo);

                // Generuj PDF
                var sciezka = await _exportService.EksportujSaldoKontrahentaDoPDFAsync(
                    WybranyKontrahent.Kontrahent,
                    WybranyKontrahent.KontrahentId,
                    saldo,
                    dokumenty.ToList(),
                    DataOd,
                    DataDo);

                var result = MessageBox.Show(
                    $"Raport PDF zapisany:\n{sciezka}\n\nCzy chcesz otworzyć plik?",
                    "Eksport PDF", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    _exportService.OtworzPlik(sciezka);

            }, "Generowanie PDF...");
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
                // Generuj PDF zestawienia
                var sciezka = await _exportService.EksportujZestawienieDoPDFAsync(
                    ZestawienieFiltrowane.ToList(),
                    WybranyTypOpakowania,
                    DataOd,
                    DataDo,
                    _handlowiecFilter);

                var result = MessageBox.Show(
                    $"Zestawienie PDF zapisane:\n{sciezka}\n\nCzy chcesz otworzyć plik?",
                    "Eksport PDF", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    _exportService.OtworzPlik(sciezka);

            }, "Generowanie zestawienia PDF...");
        }

        private void OtworzSzczegoly(object parameter)
        {
            if (WybranyKontrahent != null && WybranyKontrahent.KontrahentId > 0)
            {
                OtworzSzczegolyRequested?.Invoke(WybranyKontrahent);
            }
        }

        private void DodajPotwierdzenie(object parameter)
        {
            if (WybranyKontrahent != null && WybranyKontrahent.KontrahentId > 0)
            {
                DodajPotwierdzenieRequested?.Invoke(WybranyKontrahent, WybranyTypOpakowania);
            }
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
    }
}
