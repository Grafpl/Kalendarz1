using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.ViewModels
{
    public class SaldaMainViewModel : INotifyPropertyChanged
    {
        private readonly SaldaService _service;
        private string _handlowiecFilter;

        public string UserId { get; }

        public SaldaMainViewModel(string userId)
        {
            UserId = userId;
            _service = new SaldaService();

            _dataDo = DateTime.Today;
            _wszystkieSalda = new List<SaldoKontrahenta>();

            ListaE2 = new ObservableCollection<SaldoKontrahenta>();
            ListaH1 = new ObservableCollection<SaldoKontrahenta>();
            ListaEURO = new ObservableCollection<SaldoKontrahenta>();
            ListaPCV = new ObservableCollection<SaldoKontrahenta>();
            ListaDREW = new ObservableCollection<SaldoKontrahenta>();

            OdswiezCommand = new RelayCommand(async _ => await OdswiezAsync());

            // Start
            _ = InitAsync();
        }

        #region Properties

        private DateTime _dataDo;
        public DateTime DataDo
        {
            get => _dataDo;
            set
            {
                if (_dataDo != value)
                {
                    _dataDo = value;
                    OnPropertyChanged();
                    SaldaService.InvalidateCache();
                    _ = OdswiezAsync();
                }
            }
        }

        private string _filtrTekst;
        public string FiltrTekst
        {
            get => _filtrTekst;
            set
            {
                if (_filtrTekst != value)
                {
                    _filtrTekst = value;
                    OnPropertyChanged();
                    FiltrujListy();
                }
            }
        }

        private int _wybranaZakladka;
        public int WybranaZakladka
        {
            get => _wybranaZakladka;
            set
            {
                if (_wybranaZakladka != value)
                {
                    _wybranaZakladka = value;
                    OnPropertyChanged();
                    ObliczStatystyki();
                }
            }
        }

        private SaldoKontrahenta _wybranyKontrahent;
        public SaldoKontrahenta WybranyKontrahent
        {
            get => _wybranyKontrahent;
            set
            {
                _wybranyKontrahent = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        private string _statusLadowania;
        public string StatusLadowania
        {
            get => _statusLadowania;
            set
            {
                _statusLadowania = value;
                OnPropertyChanged();
            }
        }

        public string NazwaHandlowca => string.IsNullOrEmpty(_handlowiecFilter) ? "Wszyscy" : _handlowiecFilter;

        // Listy per zakładka
        public ObservableCollection<SaldoKontrahenta> ListaE2 { get; }
        public ObservableCollection<SaldoKontrahenta> ListaH1 { get; }
        public ObservableCollection<SaldoKontrahenta> ListaEURO { get; }
        public ObservableCollection<SaldoKontrahenta> ListaPCV { get; }
        public ObservableCollection<SaldoKontrahenta> ListaDREW { get; }

        private List<SaldoKontrahenta> _wszystkieSalda;

        // Statystyki
        private int _liczbaKontrahentow;
        public int LiczbaKontrahentow
        {
            get => _liczbaKontrahentow;
            set { _liczbaKontrahentow = value; OnPropertyChanged(); }
        }

        private int _sumaWydane;
        public int SumaWydane
        {
            get => _sumaWydane;
            set { _sumaWydane = value; OnPropertyChanged(); }
        }

        private int _sumaZwroty;
        public int SumaZwroty
        {
            get => _sumaZwroty;
            set { _sumaZwroty = value; OnPropertyChanged(); }
        }

        #endregion

        #region Commands

        public ICommand OdswiezCommand { get; }

        #endregion

        #region Methods

        private async Task InitAsync()
        {
            // Pobierz filtr handlowca
            _handlowiecFilter = await _service.PobierzHandlowcaAsync(UserId);
            OnPropertyChanged(nameof(NazwaHandlowca));

            await OdswiezAsync();
        }

        private async Task OdswiezAsync()
        {
            try
            {
                IsLoading = true;
                StatusLadowania = "Pobieranie danych...";

                _wszystkieSalda = await _service.PobierzWszystkieSaldaAsync(DataDo, _handlowiecFilter);

                FiltrujListy();

                StatusLadowania = $"Załadowano {_wszystkieSalda.Count} kontrahentów";
            }
            catch (Exception ex)
            {
                StatusLadowania = $"Błąd: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void FiltrujListy()
        {
            var filtr = FiltrTekst?.ToLower() ?? "";

            var przefiltrowane = _wszystkieSalda
                .Where(s => string.IsNullOrEmpty(filtr) ||
                            s.Kontrahent.ToLower().Contains(filtr) ||
                            s.Nazwa?.ToLower().Contains(filtr) == true ||
                            s.Handlowiec?.ToLower().Contains(filtr) == true)
                .ToList();

            // E2 - tylko kontrahenci z saldem E2
            ListaE2.Clear();
            foreach (var s in przefiltrowane.Where(x => x.E2 != 0).OrderByDescending(x => x.E2))
                ListaE2.Add(s);

            // H1
            ListaH1.Clear();
            foreach (var s in przefiltrowane.Where(x => x.H1 != 0).OrderByDescending(x => x.H1))
                ListaH1.Add(s);

            // EURO
            ListaEURO.Clear();
            foreach (var s in przefiltrowane.Where(x => x.EURO != 0).OrderByDescending(x => x.EURO))
                ListaEURO.Add(s);

            // PCV
            ListaPCV.Clear();
            foreach (var s in przefiltrowane.Where(x => x.PCV != 0).OrderByDescending(x => x.PCV))
                ListaPCV.Add(s);

            // DREW
            ListaDREW.Clear();
            foreach (var s in przefiltrowane.Where(x => x.DREW != 0).OrderByDescending(x => x.DREW))
                ListaDREW.Add(s);

            ObliczStatystyki();
        }

        private void ObliczStatystyki()
        {
            var aktualnaLista = WybranaZakladka switch
            {
                0 => ListaE2,
                1 => ListaH1,
                2 => ListaEURO,
                3 => ListaPCV,
                4 => ListaDREW,
                _ => ListaE2
            };

            var typSalda = WybranaZakladka switch
            {
                0 => "E2",
                1 => "H1",
                2 => "EURO",
                3 => "PCV",
                4 => "DREW",
                _ => "E2"
            };

            LiczbaKontrahentow = aktualnaLista.Count;
            SumaWydane = aktualnaLista.Where(s => s.GetSaldo(typSalda) > 0).Sum(s => s.GetSaldo(typSalda));
            SumaZwroty = aktualnaLista.Where(s => s.GetSaldo(typSalda) < 0).Sum(s => Math.Abs(s.GetSaldo(typSalda)));
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}
