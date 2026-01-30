using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Kalendarz1.Kartoteka.Models;
using Kalendarz1.Kartoteka.Services;

namespace Kalendarz1.Kartoteka.ViewModels
{
    public class KartotekaViewModel : INotifyPropertyChanged
    {
        private readonly KartotekaService _service;
        private readonly string _userId;
        private readonly string _userName;

        private ObservableCollection<Odbiorca> _odbiorcy = new();
        private ICollectionView _odbiorcyView;
        private Odbiorca _wybranyOdbiorca;
        private ObservableCollection<KontaktOdbiorcy> _kontakty = new();
        private ObservableCollection<FakturaOdbiorcy> _faktury = new();

        private string _tekstSzukaj = "";
        private string _filtrKategoria;
        private string _filtrHandlowiec;
        private bool _tylkoAlerty;
        private bool _isLoading;
        private string _statusMessage;
        private string _errorMessage;
        private int _wybranaZakladka;

        public KartotekaViewModel(KartotekaService service, string userId, string userName)
        {
            _service = service;
            _userId = userId;
            _userName = userName;

            OdswiezCommand = new RelayCommand(async () => await LoadDataAsync());
            ExportExcelCommand = new RelayCommand(() => { }); // will be set from code-behind
        }

        public bool IsAdmin => _userId == "11111";
        public string TekstHandlowca => IsAdmin ? "Administrator - wszystkie dane" : $"Handlowiec: {_userName}";

        public ObservableCollection<Odbiorca> Odbiorcy
        {
            get => _odbiorcy;
            set { _odbiorcy = value; OnPropertyChanged(); SetupFiltering(); }
        }

        public ICollectionView OdbiorcyView
        {
            get => _odbiorcyView;
            private set { _odbiorcyView = value; OnPropertyChanged(); }
        }

        public Odbiorca WybranyOdbiorca
        {
            get => _wybranyOdbiorca;
            set
            {
                _wybranyOdbiorca = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MaWybranegoOdbiorce));
                _ = LoadSzczegolyAsync();
            }
        }

        public bool MaWybranegoOdbiorce => _wybranyOdbiorca != null;

        public ObservableCollection<KontaktOdbiorcy> Kontakty
        {
            get => _kontakty;
            set { _kontakty = value; OnPropertyChanged(); }
        }

        public ObservableCollection<FakturaOdbiorcy> Faktury
        {
            get => _faktury;
            set { _faktury = value; OnPropertyChanged(); }
        }

        public string TekstSzukaj
        {
            get => _tekstSzukaj;
            set { _tekstSzukaj = value; OnPropertyChanged(); OdbiorcyView?.Refresh(); }
        }

        public string FiltrKategoria
        {
            get => _filtrKategoria;
            set { _filtrKategoria = value; OnPropertyChanged(); OdbiorcyView?.Refresh(); }
        }

        public string FiltrHandlowiec
        {
            get => _filtrHandlowiec;
            set { _filtrHandlowiec = value; OnPropertyChanged(); _ = LoadDataAsync(); }
        }

        public bool TylkoAlerty
        {
            get => _tylkoAlerty;
            set { _tylkoAlerty = value; OnPropertyChanged(); OdbiorcyView?.Refresh(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public int WybranaZakladka
        {
            get => _wybranaZakladka;
            set { _wybranaZakladka = value; OnPropertyChanged(); }
        }

        // Statystyki stopki
        public decimal SumaLimitow => Odbiorcy?.Sum(o => o.LimitKupiecki) ?? 0;
        public decimal SumaWykorzystanych => Odbiorcy?.Sum(o => o.WykorzystanoLimit) ?? 0;
        public decimal WolnyLimit => SumaLimitow - SumaWykorzystanych;
        public decimal SumaPrzeterminowanych => Odbiorcy?.Sum(o => o.KwotaPrzeterminowana) ?? 0;
        public int LiczbaOdbiorcow => OdbiorcyView?.Cast<object>().Count() ?? 0;

        public List<string> Handlowcy { get; set; } = new();
        public List<string> Kategorie { get; } = new() { null, "A", "B", "C" };
        public List<string> KategorieDisplay { get; } = new() { "Wszystkie", "A", "B", "C" };

        public ICommand OdswiezCommand { get; }
        public ICommand ExportExcelCommand { get; set; }

        private void SetupFiltering()
        {
            OdbiorcyView = CollectionViewSource.GetDefaultView(_odbiorcy);
            OdbiorcyView.Filter = FiltrujOdbiorcow;
            OdbiorcyView.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(LiczbaOdbiorcow));
            };
        }

        private bool FiltrujOdbiorcow(object obj)
        {
            if (obj is not Odbiorca o) return false;

            // Filtr tekstu
            if (!string.IsNullOrWhiteSpace(TekstSzukaj))
            {
                var szukaj = TekstSzukaj.ToLower();
                bool pasuje = (o.NazwaFirmy?.ToLower().Contains(szukaj) ?? false)
                    || (o.Miasto?.ToLower().Contains(szukaj) ?? false)
                    || (o.NIP?.Contains(szukaj) ?? false)
                    || (o.OsobaKontaktowa?.ToLower().Contains(szukaj) ?? false)
                    || (o.TelefonKontakt?.Contains(szukaj) ?? false);
                if (!pasuje) return false;
            }

            // Filtr kategorii
            if (!string.IsNullOrEmpty(FiltrKategoria))
            {
                if (o.KategoriaHandlowca != FiltrKategoria) return false;
            }

            // Filtr alertów
            if (TylkoAlerty)
            {
                if (o.AlertType == "None") return false;
            }

            return true;
        }

        public async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                StatusMessage = "Ładowanie danych...";

                await _service.EnsureTablesExistAsync();

                string handlowiec = IsAdmin ? null : _userName;
                bool pokazWszystkich = IsAdmin && (string.IsNullOrEmpty(FiltrHandlowiec) || FiltrHandlowiec == "Wszyscy");

                if (IsAdmin && !string.IsNullOrEmpty(FiltrHandlowiec) && FiltrHandlowiec != "Wszyscy")
                    handlowiec = FiltrHandlowiec;

                var odbiorcy = await _service.PobierzOdbiorcowAsync(handlowiec, pokazWszystkich);
                await _service.WczytajDaneWlasneAsync(odbiorcy);

                Odbiorcy = new ObservableCollection<Odbiorca>(odbiorcy);

                if (IsAdmin && Handlowcy.Count == 0)
                {
                    var handlowcy = await _service.PobierzHandlowcowAsync();
                    Handlowcy = new List<string> { "Wszyscy" };
                    Handlowcy.AddRange(handlowcy);
                    OnPropertyChanged(nameof(Handlowcy));
                }

                OnPropertyChanged(nameof(SumaLimitow));
                OnPropertyChanged(nameof(SumaWykorzystanych));
                OnPropertyChanged(nameof(WolnyLimit));
                OnPropertyChanged(nameof(SumaPrzeterminowanych));
                OnPropertyChanged(nameof(LiczbaOdbiorcow));

                StatusMessage = $"Załadowano {odbiorcy.Count} odbiorców";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                StatusMessage = "Błąd ładowania danych";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadSzczegolyAsync()
        {
            if (_wybranyOdbiorca == null) return;

            try
            {
                var kontakty = await _service.PobierzKontaktyAsync(_wybranyOdbiorca.IdSymfonia);
                Kontakty = new ObservableCollection<KontaktOdbiorcy>(kontakty);

                var faktury = await _service.PobierzFakturyAsync(_wybranyOdbiorca.IdSymfonia);
                Faktury = new ObservableCollection<FakturaOdbiorcy>(faktury);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Błąd ładowania szczegółów: {ex.Message}";
            }
        }

        public async Task ZapiszDaneOdbiorcy(Odbiorca odbiorca)
        {
            await _service.ZapiszDaneWlasneAsync(odbiorca, _userName);
        }

        public async Task ZapiszKontakt(KontaktOdbiorcy kontakt)
        {
            await _service.ZapiszKontaktAsync(kontakt);
            if (_wybranyOdbiorca != null)
                await LoadSzczegolyAsync();
        }

        public async Task UsunKontakt(int id)
        {
            await _service.UsunKontaktAsync(id);
            if (_wybranyOdbiorca != null)
                await LoadSzczegolyAsync();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private class RelayCommand : ICommand
        {
            private readonly Action _execute;
            public RelayCommand(Action execute) => _execute = execute;
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => _execute();
        }
    }
}
