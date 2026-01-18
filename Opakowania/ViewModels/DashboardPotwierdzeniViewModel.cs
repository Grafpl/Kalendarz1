using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;
using Kalendarz1.Opakowania.Views;

namespace Kalendarz1.Opakowania.ViewModels
{
    public class DashboardPotwierdzeniViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SaldaService _saldaService;
        private readonly EmailService _emailService;
        private readonly PrzypomnienieService _przypomnienieService;
        private readonly string _userId;
        private readonly Window _ownerWindow;
        private List<StatusPotwierdzeniaKontrahenta> _wszystkieKontrahenci;

        public DashboardPotwierdzeniViewModel(string userId, Window ownerWindow)
        {
            _userId = userId;
            _ownerWindow = ownerWindow;
            _saldaService = new SaldaService();
            _emailService = new EmailService();
            _przypomnienieService = new PrzypomnienieService();

            ListaKontrahentow = new ObservableCollection<StatusPotwierdzeniaKontrahenta>();
            _wszystkieKontrahenci = new List<StatusPotwierdzeniaKontrahenta>();

            // Komendy
            OdswiezCommand = new RelayCommand(async _ => await OdswiezAsync());
            WyslijPrzypomnienia = new RelayCommand(async _ => await WyslijMasowePrzypomnienia(), _ => LiczbaDoWyslania > 0);
            WyslijPrzypomnienieCommand = new RelayCommand(async p => await WyslijPojedynczePrzypomnienie(p as StatusPotwierdzeniaKontrahenta));
            DodajPotwierdzenieCommand = new RelayCommand(p => OtworzDodajPotwierdzenie(p as StatusPotwierdzeniaKontrahenta));
            SzczegolyCommand = new RelayCommand(p => OtworzSzczegoly(p as StatusPotwierdzeniaKontrahenta));

            // Start
            _ = OdswiezAsync();
        }

        #region Properties

        public ObservableCollection<StatusPotwierdzeniaKontrahenta> ListaKontrahentow { get; }

        private StatusPotwierdzeniaKontrahenta _wybranyKontrahent;
        public StatusPotwierdzeniaKontrahenta WybranyKontrahent
        {
            get => _wybranyKontrahent;
            set { _wybranyKontrahent = value; OnPropertyChanged(); }
        }

        private string _filtrTekst;
        public string FiltrTekst
        {
            get => _filtrTekst;
            set { _filtrTekst = value; OnPropertyChanged(); FiltrujListe(); }
        }

        private int _wybranyFiltrStatusu;
        public int WybranyFiltrStatusu
        {
            get => _wybranyFiltrStatusu;
            set { _wybranyFiltrStatusu = value; OnPropertyChanged(); FiltrujListe(); }
        }

        private int _wybranyTypOpakowania;
        public int WybranyTypOpakowania
        {
            get => _wybranyTypOpakowania;
            set { _wybranyTypOpakowania = value; OnPropertyChanged(); FiltrujListe(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _statusLadowania;
        public string StatusLadowania
        {
            get => _statusLadowania;
            set { _statusLadowania = value; OnPropertyChanged(); }
        }

        // Statystyki
        private int _liczbaKontrahentow;
        public int LiczbaKontrahentow
        {
            get => _liczbaKontrahentow;
            set { _liczbaKontrahentow = value; OnPropertyChanged(); }
        }

        private int _liczbaPotwierdzone;
        public int LiczbaPotwierdzone
        {
            get => _liczbaPotwierdzone;
            set { _liczbaPotwierdzone = value; OnPropertyChanged(); }
        }

        private int _liczbaOczekujace;
        public int LiczbaOczekujace
        {
            get => _liczbaOczekujace;
            set { _liczbaOczekujace = value; OnPropertyChanged(); }
        }

        private int _liczbaNiepotwierdzone;
        public int LiczbaNiepotwierdzone
        {
            get => _liczbaNiepotwierdzone;
            set { _liczbaNiepotwierdzone = value; OnPropertyChanged(); }
        }

        private int _liczbaRozbieznosci;
        public int LiczbaRozbieznosci
        {
            get => _liczbaRozbieznosci;
            set { _liczbaRozbieznosci = value; OnPropertyChanged(); }
        }

        private int _liczbaWyswietlonych;
        public int LiczbaWyswietlonych
        {
            get => _liczbaWyswietlonych;
            set { _liczbaWyswietlonych = value; OnPropertyChanged(); }
        }

        private int _liczbaDoWyslania;
        public int LiczbaDoWyslania
        {
            get => _liczbaDoWyslania;
            set { _liczbaDoWyslania = value; OnPropertyChanged(); }
        }

        #endregion

        #region Commands

        public ICommand OdswiezCommand { get; }
        public ICommand WyslijPrzypomnienia { get; }
        public ICommand WyslijPrzypomnienieCommand { get; }
        public ICommand DodajPotwierdzenieCommand { get; }
        public ICommand SzczegolyCommand { get; }

        #endregion

        #region Methods

        private async Task OdswiezAsync()
        {
            try
            {
                IsLoading = true;
                StatusLadowania = "Pobieranie danych...";

                // Pobierz salda kontrahentów
                var salda = await _saldaService.PobierzWszystkieSaldaAsync(DateTime.Today);

                // Pobierz historię przypomnień
                var historiaPrzypomnien = await _przypomnienieService.PobierzHistoriePrzypomnienAsync();

                // Przekształć na StatusPotwierdzeniaKontrahenta
                _wszystkieKontrahenci = salda
                    .Where(s => s.MaSaldo)
                    .Select(s => new StatusPotwierdzeniaKontrahenta
                    {
                        Id = s.Id,
                        Kontrahent = s.Kontrahent,
                        Nazwa = s.Nazwa,
                        Handlowiec = s.Handlowiec,
                        Email = s.Email,
                        E2 = s.E2,
                        H1 = s.H1,
                        E2Potwierdzone = s.E2Potwierdzone,
                        H1Potwierdzone = s.H1Potwierdzone,
                        E2DataPotwierdzenia = s.E2DataPotwierdzenia,
                        H1DataPotwierdzenia = s.H1DataPotwierdzenia,
                        OstatniePrzypomnienie = historiaPrzypomnien.TryGetValue(s.Id, out var data) ? data : null
                    })
                    .ToList();

                // Oblicz statystyki
                ObliczStatystyki();

                // Filtruj i wyświetl
                FiltrujListe();

                StatusLadowania = $"Załadowano {_wszystkieKontrahenci.Count} kontrahentów";
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

        private void ObliczStatystyki()
        {
            LiczbaKontrahentow = _wszystkieKontrahenci.Count;
            LiczbaPotwierdzone = _wszystkieKontrahenci.Count(k => k.JestPotwierdzone);
            LiczbaOczekujace = _wszystkieKontrahenci.Count(k => k.StatusOgolny == "Oczekujące");
            LiczbaNiepotwierdzone = _wszystkieKontrahenci.Count(k => !k.JestPotwierdzone && k.StatusOgolny != "Oczekujące");
            LiczbaRozbieznosci = _wszystkieKontrahenci.Count(k => k.MaRozbieznosc);
            LiczbaDoWyslania = _wszystkieKontrahenci.Count(k => k.WymagaPrzypomnienia && !string.IsNullOrEmpty(k.Email));
        }

        private void FiltrujListe()
        {
            var filtr = FiltrTekst?.ToLower() ?? "";

            var przefiltrowane = _wszystkieKontrahenci.AsEnumerable();

            // Filtr tekstowy
            if (!string.IsNullOrEmpty(filtr))
            {
                przefiltrowane = przefiltrowane.Where(k =>
                    k.Kontrahent.ToLower().Contains(filtr) ||
                    k.Nazwa?.ToLower().Contains(filtr) == true ||
                    k.Handlowiec?.ToLower().Contains(filtr) == true);
            }

            // Filtr statusu
            przefiltrowane = WybranyFiltrStatusu switch
            {
                1 => przefiltrowane.Where(k => k.JestPotwierdzone),
                2 => przefiltrowane.Where(k => k.StatusOgolny == "Oczekujące"),
                3 => przefiltrowane.Where(k => !k.JestPotwierdzone && k.StatusOgolny != "Oczekujące"),
                4 => przefiltrowane.Where(k => k.MaRozbieznosc),
                5 => przefiltrowane.Where(k => k.DniOdPotwierdzenia > 30),
                6 => przefiltrowane.Where(k => k.DniOdPotwierdzenia > 90),
                _ => przefiltrowane
            };

            // Filtr typu opakowania
            przefiltrowane = WybranyTypOpakowania switch
            {
                1 => przefiltrowane.Where(k => k.E2 != 0),
                2 => przefiltrowane.Where(k => k.H1 != 0),
                _ => przefiltrowane
            };

            // Sortuj - najpierw niepotwierdzone, potem po dniach od potwierdzenia
            przefiltrowane = przefiltrowane
                .OrderByDescending(k => k.WymagaPrzypomnienia)
                .ThenByDescending(k => k.DniOdPotwierdzenia);

            ListaKontrahentow.Clear();
            foreach (var k in przefiltrowane)
            {
                ListaKontrahentow.Add(k);
            }

            LiczbaWyswietlonych = ListaKontrahentow.Count;
        }

        private async Task WyslijMasowePrzypomnienia()
        {
            var doWyslania = ListaKontrahentow
                .Where(k => k.WymagaPrzypomnienia && !string.IsNullOrEmpty(k.Email))
                .ToList();

            if (doWyslania.Count == 0)
            {
                MessageBox.Show("Brak kontrahentów wymagających przypomnienia z adresem email.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Czy wysłać przypomnienia do {doWyslania.Count} kontrahentów?",
                "Potwierdzenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            StatusLadowania = "Wysyłanie przypomnień...";

            int wyslanych = 0;
            int bledow = 0;

            foreach (var kontrahent in doWyslania)
            {
                try
                {
                    StatusLadowania = $"Wysyłanie do {kontrahent.Kontrahent}... ({wyslanych + 1}/{doWyslania.Count})";

                    await _przypomnienieService.WyslijPrzypomnienieAsync(
                        kontrahent.Id,
                        kontrahent.Nazwa,
                        kontrahent.Email,
                        kontrahent.E2,
                        kontrahent.H1,
                        _userId);

                    kontrahent.OstatniePrzypomnienie = DateTime.Now;
                    wyslanych++;
                }
                catch
                {
                    bledow++;
                }
            }

            IsLoading = false;
            StatusLadowania = $"Wysłano: {wyslanych}, Błędy: {bledow}";

            MessageBox.Show(
                $"Wysłano: {wyslanych} przypomnień\nBłędy: {bledow}",
                "Wynik wysyłki",
                MessageBoxButton.OK,
                bledow > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            await OdswiezAsync();
        }

        private async Task WyslijPojedynczePrzypomnienie(StatusPotwierdzeniaKontrahenta kontrahent)
        {
            if (kontrahent == null) return;

            if (string.IsNullOrEmpty(kontrahent.Email))
            {
                MessageBox.Show("Kontrahent nie ma przypisanego adresu email.", "Brak emaila",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Czy wysłać przypomnienie do {kontrahent.Nazwa}?\n\nEmail: {kontrahent.Email}",
                "Potwierdzenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                IsLoading = true;
                StatusLadowania = $"Wysyłanie do {kontrahent.Kontrahent}...";

                await _przypomnienieService.WyslijPrzypomnienieAsync(
                    kontrahent.Id,
                    kontrahent.Nazwa,
                    kontrahent.Email,
                    kontrahent.E2,
                    kontrahent.H1,
                    _userId);

                kontrahent.OstatniePrzypomnienie = DateTime.Now;

                MessageBox.Show("Przypomnienie zostało wysłane.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wysyłania: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                StatusLadowania = "Gotowe";
            }
        }

        private void OtworzDodajPotwierdzenie(StatusPotwierdzeniaKontrahenta kontrahent)
        {
            if (kontrahent == null) return;

            var saldo = new SaldoKontrahenta
            {
                Id = kontrahent.Id,
                Kontrahent = kontrahent.Kontrahent,
                Nazwa = kontrahent.Nazwa,
                Handlowiec = kontrahent.Handlowiec,
                E2 = kontrahent.E2,
                H1 = kontrahent.H1
            };

            var window = new SaldaSzczegolyWindow(saldo, DateTime.Today, _userId);
            window.Owner = _ownerWindow;
            var result = window.ShowDialog();

            if (result == true)
            {
                _ = OdswiezAsync();
            }
        }

        private void OtworzSzczegoly(StatusPotwierdzeniaKontrahenta kontrahent)
        {
            if (kontrahent == null) return;

            var saldo = new SaldoKontrahenta
            {
                Id = kontrahent.Id,
                Kontrahent = kontrahent.Kontrahent,
                Nazwa = kontrahent.Nazwa,
                Handlowiec = kontrahent.Handlowiec,
                E2 = kontrahent.E2,
                H1 = kontrahent.H1,
                E2Potwierdzone = kontrahent.E2Potwierdzone,
                H1Potwierdzone = kontrahent.H1Potwierdzone,
                E2DataPotwierdzenia = kontrahent.E2DataPotwierdzenia,
                H1DataPotwierdzenia = kontrahent.H1DataPotwierdzenia
            };

            var window = new SaldaSzczegolyWindow(saldo, DateTime.Today, _userId);
            window.Owner = _ownerWindow;
            var result = window.ShowDialog();

            if (result == true)
            {
                _ = OdswiezAsync();
            }
        }

        public void Dispose()
        {
            _saldaService?.Dispose();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }

    /// <summary>
    /// Model statusu potwierdzenia dla dashboardu
    /// </summary>
    public class StatusPotwierdzeniaKontrahenta : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Kontrahent { get; set; }
        public string Nazwa { get; set; }
        public string Handlowiec { get; set; }
        public string Email { get; set; }

        public int E2 { get; set; }
        public int H1 { get; set; }

        public bool E2Potwierdzone { get; set; }
        public bool H1Potwierdzone { get; set; }

        public DateTime? E2DataPotwierdzenia { get; set; }
        public DateTime? H1DataPotwierdzenia { get; set; }

        private DateTime? _ostatniePrzypomnienie;
        public DateTime? OstatniePrzypomnienie
        {
            get => _ostatniePrzypomnienie;
            set { _ostatniePrzypomnienie = value; OnPropertyChanged(); OnPropertyChanged(nameof(WymagaPrzypomnienia)); }
        }

        // Status rozbieżności (do uzupełnienia z bazy)
        public bool MaRozbieznosc { get; set; }

        // Obliczenia
        public string E2Tekst => E2 == 0 ? "-" : E2.ToString();
        public string H1Tekst => H1 == 0 ? "-" : H1.ToString();

        public SolidColorBrush E2Kolor => GetKolor(E2);
        public SolidColorBrush H1Kolor => GetKolor(H1);

        public string E2StatusIkona => GetStatusIkona(E2Potwierdzone, E2 != 0);
        public string H1StatusIkona => GetStatusIkona(H1Potwierdzone, H1 != 0);

        public SolidColorBrush E2StatusKolor => GetStatusKolor(E2Potwierdzone, E2 != 0);
        public SolidColorBrush H1StatusKolor => GetStatusKolor(H1Potwierdzone, H1 != 0);

        public string E2StatusTooltip => GetStatusTooltip(E2Potwierdzone, E2DataPotwierdzenia, E2 != 0);
        public string H1StatusTooltip => GetStatusTooltip(H1Potwierdzone, H1DataPotwierdzenia, H1 != 0);

        public DateTime? OstatniePotwierdzenie
        {
            get
            {
                var daty = new[] { E2DataPotwierdzenia, H1DataPotwierdzenia }
                    .Where(d => d.HasValue)
                    .ToList();
                return daty.Any() ? daty.Max() : null;
            }
        }

        public string OstatniePotwierdzenieText => OstatniePotwierdzenie?.ToString("dd.MM.yyyy") ?? "Nigdy";

        public int DniOdPotwierdzenia
        {
            get
            {
                if (!OstatniePotwierdzenie.HasValue) return 999;
                return (int)(DateTime.Today - OstatniePotwierdzenie.Value).TotalDays;
            }
        }

        public SolidColorBrush DniOdPotwierdzeninaKolor
        {
            get
            {
                if (DniOdPotwierdzenia > 90) return new SolidColorBrush(Color.FromRgb(229, 57, 53));
                if (DniOdPotwierdzenia > 30) return new SolidColorBrush(Color.FromRgb(251, 140, 0));
                return new SolidColorBrush(Color.FromRgb(67, 160, 71));
            }
        }

        public bool JestPotwierdzone => (E2 == 0 || E2Potwierdzone) && (H1 == 0 || H1Potwierdzone);

        public string StatusOgolny
        {
            get
            {
                if (MaRozbieznosc) return "Rozbieżność";
                if (JestPotwierdzone) return "Potwierdzone";
                if (OstatniePrzypomnienie.HasValue && (DateTime.Now - OstatniePrzypomnienie.Value).TotalDays < 7)
                    return "Oczekujące";
                return "Niepotwierdzone";
            }
        }

        public bool WymagaPrzypomnienia
        {
            get
            {
                if (JestPotwierdzone) return false;
                if (OstatniePrzypomnienie.HasValue && (DateTime.Now - OstatniePrzypomnienie.Value).TotalDays < 7)
                    return false;
                return DniOdPotwierdzenia > 30;
            }
        }

        private static SolidColorBrush GetKolor(int saldo)
        {
            if (saldo > 0) return new SolidColorBrush(Color.FromRgb(220, 38, 38));
            if (saldo < 0) return new SolidColorBrush(Color.FromRgb(22, 163, 74));
            return new SolidColorBrush(Color.FromRgb(156, 163, 175));
        }

        private static string GetStatusIkona(bool potwierdzone, bool maSaldo)
        {
            if (!maSaldo) return "";
            return potwierdzone ? "\uE73E" : "\uE7BA";
        }

        private static SolidColorBrush GetStatusKolor(bool potwierdzone, bool maSaldo)
        {
            if (!maSaldo) return new SolidColorBrush(Colors.Transparent);
            return potwierdzone
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                : new SolidColorBrush(Color.FromRgb(229, 57, 53));
        }

        private static string GetStatusTooltip(bool potwierdzone, DateTime? data, bool maSaldo)
        {
            if (!maSaldo) return "";
            if (potwierdzone && data.HasValue)
                return $"Potwierdzone: {data.Value:dd.MM.yyyy}";
            return "Brak potwierdzenia";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
