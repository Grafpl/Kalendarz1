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

            OnPropertyChanged(nameof(LiczbaDokumentow));
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
