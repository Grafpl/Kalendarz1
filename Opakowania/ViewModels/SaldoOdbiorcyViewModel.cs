using System;
using System.Collections.Generic;
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
    /// ViewModel dla szczegółowego widoku salda odbiorcy - z eksportem i kontaktem
    /// </summary>
    public class SaldoOdbiorcyViewModel : ViewModelBase
    {
        private readonly OpakowaniaDataService _dataService;
        private readonly ExportService _exportService;
        private readonly string _userId;

        private int _kontrahentId;
        private string _kontrahentNazwa;
        private DateTime _dataOd;
        private DateTime _dataDo;

        private SaldoOpakowania _saldoAktualne;
        private ObservableCollection<DokumentOpakowania> _dokumenty;
        
        // Dane kontaktowe kontrahenta
        private string _telefon;
        private string _email;
        private string _adres;
        private string _nip;

        // Szybkie filtry dat
        private ObservableCollection<FiltrDaty> _filtryDat;
        private FiltrDaty _wybranyFiltrDaty;

        public SaldoOdbiorcyViewModel(int kontrahentId, string kontrahentNazwa, string userId)
        {
            _kontrahentId = kontrahentId;
            _kontrahentNazwa = kontrahentNazwa;
            _userId = userId;
            _dataService = new OpakowaniaDataService();
            _exportService = new ExportService();

            // Domyślny okres - poprzedni tydzień
            var (dataOd, dataDo) = UstawieniaService.GetDomyslnyOkres();
            _dataOd = dataOd;
            _dataDo = dataDo;

            Dokumenty = new ObservableCollection<DokumentOpakowania>();
            FiltryDat = new ObservableCollection<FiltrDaty>(FiltrDaty.GetFiltryDomyslne());

            // Komendy
            OdswiezCommand = new AsyncRelayCommand(OdswiezAsync);
            EksportPDFCommand = new AsyncRelayCommand(EksportujPDFAsync);
            EksportExcelCommand = new AsyncRelayCommand(EksportujExcelAsync);
            DrukujCommand = new AsyncRelayCommand(DrukujAsync);
            ZadzwonCommand = new RelayCommand(_ => Zadzwon(), _ => !string.IsNullOrEmpty(Telefon));
            WyslijEmailCommand = new RelayCommand(_ => WyslijEmail(), _ => !string.IsNullOrEmpty(Email));

            // Inicjalizacja
            InitializeAsync();
        }

        #region Properties

        public int KontrahentId
        {
            get => _kontrahentId;
            set => SetProperty(ref _kontrahentId, value);
        }

        public string KontrahentNazwa
        {
            get => _kontrahentNazwa;
            set => SetProperty(ref _kontrahentNazwa, value);
        }

        public DateTime DataOd
        {
            get => _dataOd;
            set
            {
                if (SetProperty(ref _dataOd, value))
                {
                    _wybranyFiltrDaty = null;
                    OnPropertyChanged(nameof(WybranyFiltrDaty));
                    // Automatyczne odświeżanie po zmianie daty
                    OdswiezCommand.Execute(null);
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
                    // Automatyczne odświeżanie po zmianie daty
                    OdswiezCommand.Execute(null);
                }
            }
        }

        public SaldoOpakowania SaldoAktualne
        {
            get => _saldoAktualne;
            set => SetProperty(ref _saldoAktualne, value);
        }

        public ObservableCollection<DokumentOpakowania> Dokumenty
        {
            get => _dokumenty;
            set => SetProperty(ref _dokumenty, value);
        }

        public string Telefon
        {
            get => _telefon;
            set
            {
                if (SetProperty(ref _telefon, value))
                    ((RelayCommand)ZadzwonCommand).RaiseCanExecuteChanged();
            }
        }

        public string Email
        {
            get => _email;
            set
            {
                if (SetProperty(ref _email, value))
                    ((RelayCommand)WyslijEmailCommand).RaiseCanExecuteChanged();
            }
        }

        public string Adres
        {
            get => _adres;
            set => SetProperty(ref _adres, value);
        }

        public string NIP
        {
            get => _nip;
            set => SetProperty(ref _nip, value);
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
                    _dataOd = value.DataOd;
                    _dataDo = value.DataDo;
                    OnPropertyChanged(nameof(DataOd));
                    OnPropertyChanged(nameof(DataDo));
                    OdswiezCommand.Execute(null);
                }
            }
        }

        // Liczba dokumentów
        public int LiczbaDokumentow => Dokumenty?.Count(d => !d.JestSaldem) ?? 0;

        // Dokumenty posortowane: Saldo DO na górze, dokumenty od najnowszych, Saldo OD na dole
        public IEnumerable<DokumentOpakowania> DokumentyPosortowane
        {
            get
            {
                if (Dokumenty == null) return Enumerable.Empty<DokumentOpakowania>();

                var wynik = new List<DokumentOpakowania>();

                // 1. Saldo DO na górze (saldo końcowe)
                var saldoDo = Dokumenty.FirstOrDefault(d => d.JestSaldem && d.NumerDokumentu?.Contains("DO") == true);
                if (saldoDo != null) wynik.Add(saldoDo);

                // 2. Dokumenty posortowane od najnowszych do najstarszych
                var dokumentyBezSalda = Dokumenty
                    .Where(d => !d.JestSaldem)
                    .OrderByDescending(d => d.Data)
                    .ToList();
                wynik.AddRange(dokumentyBezSalda);

                // 3. Saldo OD na dole (saldo początkowe)
                var saldoOd = Dokumenty.FirstOrDefault(d => d.JestSaldem && d.NumerDokumentu?.Contains("OD") == true);
                if (saldoOd != null) wynik.Add(saldoOd);

                // Jeśli nie ma rozróżnienia OD/DO, dodaj wszystkie salda na dole
                if (saldoDo == null && saldoOd == null)
                {
                    var salda = Dokumenty.Where(d => d.JestSaldem);
                    wynik.AddRange(salda);
                }

                return wynik;
            }
        }

        // Dane do wykresu tygodniowego E2/H1
        private ObservableCollection<SaldoTygodniowe> _saldaTygodniowe;
        public ObservableCollection<SaldoTygodniowe> SaldaTygodniowe
        {
            get => _saldaTygodniowe;
            set => SetProperty(ref _saldaTygodniowe, value);
        }

        // Właściwości dla bindowania do kart salda
        public int SaldoE2 => SaldoAktualne?.SaldoE2 ?? 0;
        public int SaldoH1 => SaldoAktualne?.SaldoH1 ?? 0;
        public int SaldoEURO => SaldoAktualne?.SaldoEURO ?? 0;
        public int SaldoPCV => SaldoAktualne?.SaldoPCV ?? 0;
        public int SaldoDREW => SaldoAktualne?.SaldoDREW ?? 0;

        public string SaldoE2Opis => SaldoE2 == 0 ? "Brak salda" : (SaldoE2 > 0 ? "Kontrahent winny" : "My winni");
        public string SaldoH1Opis => SaldoH1 == 0 ? "Brak salda" : (SaldoH1 > 0 ? "Kontrahent winny" : "My winni");
        public string SaldoEUROOpis => SaldoEURO == 0 ? "Brak salda" : (SaldoEURO > 0 ? "Kontrahent winny" : "My winni");
        public string SaldoPCVOpis => SaldoPCV == 0 ? "Brak salda" : (SaldoPCV > 0 ? "Kontrahent winny" : "My winni");
        public string SaldoDREWOpis => SaldoDREW == 0 ? "Brak salda" : (SaldoDREW > 0 ? "Kontrahent winny" : "My winni");

        // Widoczność danych kontaktowych
        public bool MaTelefon => !string.IsNullOrWhiteSpace(Telefon);
        public bool MaEmail => !string.IsNullOrWhiteSpace(Email);
        public bool MaAdres => !string.IsNullOrWhiteSpace(Adres);
        public bool MaNIP => !string.IsNullOrWhiteSpace(NIP);

        #endregion

        #region Commands

        public ICommand OdswiezCommand { get; }
        public ICommand EksportPDFCommand { get; }
        public ICommand EksportExcelCommand { get; }
        public ICommand DrukujCommand { get; }
        public ICommand ZadzwonCommand { get; }
        public ICommand WyslijEmailCommand { get; }

        #endregion

        #region Methods

        private async void InitializeAsync()
        {
            await ExecuteAsync(async () =>
            {
                await PobierzDaneAsync();
            }, "Ładowanie danych...");
        }

        private async Task OdswiezAsync()
        {
            await ExecuteAsync(async () =>
            {
                await PobierzDaneAsync();
            }, "Odświeżanie...");
        }

        private async Task PobierzDaneAsync()
        {
            // Pobierz aktualne saldo
            SaldoAktualne = await _dataService.PobierzSaldaWszystkichOpakowannAsync(KontrahentId, DataDo);

            // Pobierz dane kontaktowe (jeśli są w saldzie)
            if (SaldoAktualne != null)
            {
                Telefon = SaldoAktualne.Telefon;
                Email = SaldoAktualne.Email;
                Adres = SaldoAktualne.Adres;
                NIP = SaldoAktualne.NIP;
            }

            // Pobierz dokumenty
            var dokumenty = await _dataService.PobierzSaldoKontrahentaAsync(KontrahentId, DataOd, DataDo);
            Dokumenty.Clear();
            foreach (var dok in dokumenty)
            {
                Dokumenty.Add(dok);
            }

            // Oblicz salda tygodniowe dla wykresu
            ObliczSaldaTygodniowe();

            OnPropertyChanged(nameof(LiczbaDokumentow));
            OnPropertyChanged(nameof(DokumentyPosortowane));
            OnPropertyChanged(nameof(SaldoE2));
            OnPropertyChanged(nameof(SaldoH1));
            OnPropertyChanged(nameof(SaldoEURO));
            OnPropertyChanged(nameof(SaldoPCV));
            OnPropertyChanged(nameof(SaldoDREW));
            OnPropertyChanged(nameof(SaldoE2Opis));
            OnPropertyChanged(nameof(SaldoH1Opis));
            OnPropertyChanged(nameof(SaldoEUROOpis));
            OnPropertyChanged(nameof(SaldoPCVOpis));
            OnPropertyChanged(nameof(SaldoDREWOpis));
            OnPropertyChanged(nameof(MaTelefon));
            OnPropertyChanged(nameof(MaEmail));
            OnPropertyChanged(nameof(MaAdres));
            OnPropertyChanged(nameof(MaNIP));
        }

        /// <summary>
        /// Oblicza salda tygodniowe dla wykresu - kumulatywne saldo na koniec każdej niedzieli.
        /// Pokazuje jak zmieniało się zadłużenie odbiorcy w czasie.
        /// Wykres zaczyna od Salda OD i kumulatywnie dodaje dokumenty.
        /// </summary>
        private void ObliczSaldaTygodniowe()
        {
            SaldaTygodniowe = new ObservableCollection<SaldoTygodniowe>();

            // 1. Pobierz saldo początkowe - dokument z JestSaldem = true i "Saldo na {DataOd}"
            var saldoPoczatkowe = Dokumenty?.FirstOrDefault(d => d.JestSaldem && d.Data <= DataOd);
            if (saldoPoczatkowe == null)
            {
                saldoPoczatkowe = Dokumenty?.FirstOrDefault(d => d.JestSaldem);
            }

            int startE2 = saldoPoczatkowe?.E2 ?? 0;
            int startH1 = saldoPoczatkowe?.H1 ?? 0;

            // 2. Pobierz tylko dokumenty transakcyjne (bez salda), posortowane chronologicznie
            var dokumentyTransakcyjne = Dokumenty?
                .Where(d => !d.JestSaldem && d.Data.HasValue)
                .OrderBy(d => d.Data)
                .ToList() ?? new List<DokumentOpakowania>();

            // 3. Ustal zakres dat
            var dataStart = DataOd;
            var dataKoniec = DataDo;

            // 4. Znajdź pierwszą niedzielę - cofamy się do niedzieli przed lub równej dacie początkowej
            var niedziela = dataStart;
            while (niedziela.DayOfWeek != DayOfWeek.Sunday)
            {
                niedziela = niedziela.AddDays(-1);
            }

            // 5. Zmienne do kumulatywnego sumowania
            int kumulatywnyE2 = startE2;
            int kumulatywnyH1 = startH1;
            int indeksOstatniegoDokumentu = 0;

            // 6. Iteruj przez wszystkie niedziele aż do końca zakresu
            while (niedziela <= dataKoniec)
            {
                // Dodaj dokumenty, które mają datę <= tej niedzieli (od ostatnio przetworzonego)
                while (indeksOstatniegoDokumentu < dokumentyTransakcyjne.Count &&
                       dokumentyTransakcyjne[indeksOstatniegoDokumentu].Data.Value <= niedziela)
                {
                    var dok = dokumentyTransakcyjne[indeksOstatniegoDokumentu];
                    kumulatywnyE2 += dok.E2;
                    kumulatywnyH1 += dok.H1;
                    indeksOstatniegoDokumentu++;
                }

                SaldaTygodniowe.Add(new SaldoTygodniowe
                {
                    DataNiedziela = niedziela,
                    SaldoE2 = kumulatywnyE2,
                    SaldoH1 = kumulatywnyH1,
                    NumerTygodnia = GetNumerTygodnia(niedziela)
                });

                niedziela = niedziela.AddDays(7);
            }

            OnPropertyChanged(nameof(SaldaTygodniowe));
        }

        private int GetNumerTygodnia(DateTime data)
        {
            // Użyj ISO 8601 - tydzień zaczyna się w poniedziałek
            var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
            return cal.GetWeekOfYear(data,
                System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
        }

        private async Task EksportujPDFAsync()
        {
            await ExecuteAsync(async () =>
            {
                var sciezka = await _exportService.EksportujSaldoKontrahentaDoPDFAsync(
                    KontrahentNazwa,
                    KontrahentId,
                    SaldoAktualne,
                    Dokumenty.ToList(),
                    DataOd,
                    DataDo);

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
                var sciezka = await _exportService.EksportujDokumentyDoExcelAsync(
                    KontrahentNazwa,
                    Dokumenty.ToList(),
                    DataOd,
                    DataDo);

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
                var sciezka = await _exportService.EksportujSaldoKontrahentaDoPDFAsync(
                    KontrahentNazwa,
                    KontrahentId,
                    SaldoAktualne,
                    Dokumenty.ToList(),
                    DataOd,
                    DataDo);

                _exportService.OtworzPlik(sciezka);
            }, "Przygotowanie wydruku...");
        }

        private void Zadzwon()
        {
            _exportService.Zadzwon(Telefon);
        }

        private void WyslijEmail()
        {
            string temat = $"Saldo opakowań - {KontrahentNazwa}";
            string tresc = "";

            if (SaldoAktualne != null)
            {
                tresc = $"Szanowni Państwo,\n\nPrzesyłam informację o stanie opakowań:\n\n" +
                    $"E2: {SaldoAktualne.SaldoE2Tekst}\n" +
                    $"H1: {SaldoAktualne.SaldoH1Tekst}\n" +
                    $"EURO: {SaldoAktualne.SaldoEUROTekst}\n" +
                    $"PCV: {SaldoAktualne.SaldoPCVTekst}\n" +
                    $"DREW: {SaldoAktualne.SaldoDREWTekst}\n\n" +
                    $"Z poważaniem";
            }

            _exportService.WyslijEmail(Email, temat, tresc);
        }

        #endregion
    }
}
