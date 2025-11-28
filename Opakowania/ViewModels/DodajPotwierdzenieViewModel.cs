using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.ViewModels
{
    /// <summary>
    /// ViewModel dla okna dodawania potwierdzenia salda
    /// </summary>
    public class DodajPotwierdzenieViewModel : ViewModelBase
    {
        private readonly OpakowaniaDataService _dataService;
        private readonly string _userId;

        private int _kontrahentId;
        private string _kontrahentNazwa;
        private string _kontrahentShortcut;
        private TypOpakowania _typOpakowania;

        private DateTime _dataPotwierdzenia;
        private int _iloscPotwierdzona;
        private int _saldoSystemowe;
        private string _numerDokumentu;
        private string _sciezkaZalacznika;
        private string _uwagi;
        private string _statusPotwierdzenia;

        public DodajPotwierdzenieViewModel(int kontrahentId, string kontrahentNazwa, string kontrahentShortcut, 
            TypOpakowania typOpakowania, int saldoSystemowe, string userId)
        {
            _kontrahentId = kontrahentId;
            _kontrahentNazwa = kontrahentNazwa;
            _kontrahentShortcut = kontrahentShortcut;
            _typOpakowania = typOpakowania;
            _saldoSystemowe = saldoSystemowe;
            _iloscPotwierdzona = saldoSystemowe; // Domyślnie taka sama
            _userId = userId;
            _dataPotwierdzenia = DateTime.Today;
            _statusPotwierdzenia = "Potwierdzone";

            _dataService = new OpakowaniaDataService();

            // Komendy
            ZapiszCommand = new AsyncRelayCommand(ZapiszCommandAsync, () => CanZapisz);
            AnulujCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
            WybierzZalacznikCommand = new RelayCommand(_ => WybierzZalacznik());
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

        public string KontrahentShortcut
        {
            get => _kontrahentShortcut;
            set => SetProperty(ref _kontrahentShortcut, value);
        }

        public TypOpakowania TypOpakowania
        {
            get => _typOpakowania;
            set => SetProperty(ref _typOpakowania, value);
        }

        public string TypOpakowaniaText => TypOpakowania?.Nazwa ?? "-";

        public DateTime DataPotwierdzenia
        {
            get => _dataPotwierdzenia;
            set
            {
                if (SetProperty(ref _dataPotwierdzenia, value))
                    ((AsyncRelayCommand)ZapiszCommand).RaiseCanExecuteChanged();
            }
        }

        public int IloscPotwierdzona
        {
            get => _iloscPotwierdzona;
            set
            {
                if (SetProperty(ref _iloscPotwierdzona, value))
                {
                    OnPropertyChanged(nameof(Roznica));
                    OnPropertyChanged(nameof(RoznicaTekst));
                    OnPropertyChanged(nameof(MaRozbieznosc));
                    AktualizujStatus();
                    ((AsyncRelayCommand)ZapiszCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public int SaldoSystemowe
        {
            get => _saldoSystemowe;
            set => SetProperty(ref _saldoSystemowe, value);
        }

        public int Roznica => IloscPotwierdzona - SaldoSystemowe;

        public string RoznicaTekst
        {
            get
            {
                var roznica = Roznica;
                if (roznica == 0) return "Brak różnicy";
                return roznica > 0 ? $"+{roznica} (odbiorca twierdzi, że ma więcej)" : $"{roznica} (odbiorca twierdzi, że ma mniej)";
            }
        }

        public bool MaRozbieznosc => Roznica != 0;

        public string NumerDokumentu
        {
            get => _numerDokumentu;
            set => SetProperty(ref _numerDokumentu, value);
        }

        public string SciezkaZalacznika
        {
            get => _sciezkaZalacznika;
            set => SetProperty(ref _sciezkaZalacznika, value);
        }

        public string Uwagi
        {
            get => _uwagi;
            set => SetProperty(ref _uwagi, value);
        }

        public string StatusPotwierdzenia
        {
            get => _statusPotwierdzenia;
            set => SetProperty(ref _statusPotwierdzenia, value);
        }

        public string[] DostepneStatusy => new[] { "Potwierdzone", "Rozbieżność", "Oczekujące" };

        public bool CanZapisz => DataPotwierdzenia <= DateTime.Today;

        #endregion

        #region Commands

        public ICommand ZapiszCommand { get; }
        public ICommand AnulujCommand { get; }
        public ICommand WybierzZalacznikCommand { get; }

        #endregion

        #region Events

        public event Action<bool?> RequestClose;
        public event Action WybierzZalacznikRequested;

        #endregion

        #region Properties Additional

        public string RoznicaDisplay
        {
            get
            {
                var r = Roznica;
                if (r == 0) return "0";
                return r > 0 ? $"+{r}" : r.ToString();
            }
        }

        #endregion

        #region Methods

        private void AktualizujStatus()
        {
            if (Roznica != 0)
            {
                StatusPotwierdzenia = "Rozbieżność";
            }
            else
            {
                StatusPotwierdzenia = "Potwierdzone";
            }
        }

        private void WybierzZalacznik()
        {
            WybierzZalacznikRequested?.Invoke();
        }

        public async Task<bool> ZapiszAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Zapisywanie potwierdzenia...";

                var potwierdzenie = new PotwierdzenieSalda
                {
                    KontrahentId = KontrahentId,
                    KontrahentNazwa = KontrahentNazwa,
                    KontrahentShortcut = KontrahentShortcut,
                    TypOpakowania = TypOpakowania.NazwaSystemowa,
                    KodOpakowania = TypOpakowania.Kod,
                    DataPotwierdzenia = DataPotwierdzenia,
                    IloscPotwierdzona = IloscPotwierdzona,
                    SaldoSystemowe = SaldoSystemowe,
                    StatusPotwierdzenia = StatusPotwierdzenia,
                    NumerDokumentu = NumerDokumentu,
                    SciezkaZalacznika = SciezkaZalacznika,
                    Uwagi = Uwagi,
                    UzytkownikId = _userId,
                    UzytkownikNazwa = _userId // Użyj ID jako nazwa
                };

                await _dataService.DodajPotwierdzenie(potwierdzenie);
                return true;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ZapiszCommandAsync()
        {
            bool success = await ZapiszAsync();
            if (success)
            {
                MessageBox.Show("Potwierdzenie zostało zapisane pomyślnie.", "Sukces", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                RequestClose?.Invoke(true);
            }
        }

        #endregion
    }
}
