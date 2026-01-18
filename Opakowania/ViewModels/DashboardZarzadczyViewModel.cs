using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.ViewModels
{
    public class DashboardZarzadczyViewModel : INotifyPropertyChanged
    {
        private readonly SaldaService _saldaService;
        private readonly string _userId;

        // Ceny kaucji
        private const decimal CENA_E2 = 15m;
        private const decimal CENA_H1 = 80m;
        private const decimal CENA_EURO = 60m;
        private const decimal CENA_PCV = 50m;
        private const decimal CENA_DREW = 40m;

        public DashboardZarzadczyViewModel(string userId)
        {
            _userId = userId;
            _saldaService = new SaldaService();

            Top10Dluznikow = new ObservableCollection<Top10Item>();
            StatystykiHandlowcow = new ObservableCollection<StatystykaHandlowca>();

            OdswiezCommand = new RelayCommand(async _ => await LoadDataAsync());

            _ = LoadDataAsync();
        }

        #region Properties

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private int _liczbaKontrahentow;
        public int LiczbaKontrahentow
        {
            get => _liczbaKontrahentow;
            set { _liczbaKontrahentow = value; OnPropertyChanged(); }
        }

        private int _sumaE2;
        public int SumaE2
        {
            get => _sumaE2;
            set { _sumaE2 = value; OnPropertyChanged(); }
        }

        private int _sumaH1;
        public int SumaH1
        {
            get => _sumaH1;
            set { _sumaH1 = value; OnPropertyChanged(); }
        }

        private int _procentPotwierdzone;
        public int ProcentPotwierdzone
        {
            get => _procentPotwierdzone;
            set { _procentPotwierdzone = value; OnPropertyChanged(); }
        }

        private decimal _wartoscKaucji;
        public decimal WartoscKaucji
        {
            get => _wartoscKaucji;
            set { _wartoscKaucji = value; OnPropertyChanged(); }
        }

        private int _alertyKrytyczne;
        public int AlertyKrytyczne
        {
            get => _alertyKrytyczne;
            set { _alertyKrytyczne = value; OnPropertyChanged(); }
        }

        private int _alertyOstrzezenia;
        public int AlertyOstrzezenia
        {
            get => _alertyOstrzezenia;
            set { _alertyOstrzezenia = value; OnPropertyChanged(); }
        }

        private int _rozbieznosci;
        public int Rozbieznosci
        {
            get => _rozbieznosci;
            set { _rozbieznosci = value; OnPropertyChanged(); }
        }

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private DateTime _dataAktualizacji;
        public DateTime DataAktualizacji
        {
            get => _dataAktualizacji;
            set { _dataAktualizacji = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Top10Item> Top10Dluznikow { get; }
        public ObservableCollection<StatystykaHandlowca> StatystykiHandlowcow { get; }

        public ICommand OdswiezCommand { get; }

        #endregion

        private async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Ładowanie danych...";

                var salda = await _saldaService.PobierzWszystkieSaldaAsync(DateTime.Today);

                // Podstawowe statystyki
                LiczbaKontrahentow = salda.Count(s => s.MaSaldo);
                SumaE2 = salda.Where(s => s.E2 > 0).Sum(s => s.E2);
                SumaH1 = salda.Where(s => s.H1 > 0).Sum(s => s.H1);

                // Wartość kaucji
                var sumaEuro = salda.Where(s => s.EURO > 0).Sum(s => s.EURO);
                var sumaPcv = salda.Where(s => s.PCV > 0).Sum(s => s.PCV);
                var sumaDrew = salda.Where(s => s.DREW > 0).Sum(s => s.DREW);

                WartoscKaucji = (SumaE2 * CENA_E2) + (SumaH1 * CENA_H1) +
                               (sumaEuro * CENA_EURO) + (sumaPcv * CENA_PCV) + (sumaDrew * CENA_DREW);

                // Procent potwierdzonych
                var zSaldem = salda.Where(s => s.MaSaldo).ToList();
                var potwierdzone = zSaldem.Count(s =>
                    (s.E2 == 0 || s.E2Potwierdzone) && (s.H1 == 0 || s.H1Potwierdzone));
                ProcentPotwierdzone = zSaldem.Count > 0 ? (potwierdzone * 100 / zSaldem.Count) : 0;

                // Alerty
                AlertyKrytyczne = zSaldem.Count(s =>
                {
                    var dniE2 = s.E2DataPotwierdzenia.HasValue ? (DateTime.Today - s.E2DataPotwierdzenia.Value).TotalDays : 999;
                    var dniH1 = s.H1DataPotwierdzenia.HasValue ? (DateTime.Today - s.H1DataPotwierdzenia.Value).TotalDays : 999;
                    return (s.E2 != 0 && dniE2 > 90) || (s.H1 != 0 && dniH1 > 90);
                });

                AlertyOstrzezenia = zSaldem.Count(s =>
                {
                    var dniE2 = s.E2DataPotwierdzenia.HasValue ? (DateTime.Today - s.E2DataPotwierdzenia.Value).TotalDays : 999;
                    var dniH1 = s.H1DataPotwierdzenia.HasValue ? (DateTime.Today - s.H1DataPotwierdzenia.Value).TotalDays : 999;
                    return ((s.E2 != 0 && dniE2 > 30 && dniE2 <= 90) || (s.H1 != 0 && dniH1 > 30 && dniH1 <= 90));
                });

                Rozbieznosci = 0; // TODO: pobierz z bazy

                // TOP 10
                Top10Dluznikow.Clear();
                var top10 = salda
                    .Where(s => s.MaSaldo)
                    .OrderByDescending(s => Math.Abs(s.E2) + Math.Abs(s.H1))
                    .Take(10)
                    .Select((s, i) => new Top10Item
                    {
                        Pozycja = i + 1,
                        Kontrahent = s.Kontrahent,
                        Nazwa = s.Nazwa,
                        E2 = s.E2,
                        H1 = s.H1,
                        Handlowiec = s.Handlowiec
                    });

                foreach (var item in top10)
                    Top10Dluznikow.Add(item);

                // Statystyki per handlowiec
                StatystykiHandlowcow.Clear();
                var grupyHandlowcow = salda
                    .Where(s => s.MaSaldo)
                    .GroupBy(s => s.Handlowiec ?? "-")
                    .Select(g => new StatystykaHandlowca
                    {
                        Handlowiec = g.Key,
                        LiczbaKontrahentow = g.Count(),
                        SumaE2 = g.Where(s => s.E2 > 0).Sum(s => s.E2),
                        SumaH1 = g.Where(s => s.H1 > 0).Sum(s => s.H1),
                        ProcentPotwierdzone = g.Count() > 0
                            ? (g.Count(s => (s.E2 == 0 || s.E2Potwierdzone) && (s.H1 == 0 || s.H1Potwierdzone)) * 100 / g.Count())
                            : 0
                    })
                    .OrderByDescending(h => h.SumaE2 + h.SumaH1);

                foreach (var stat in grupyHandlowcow)
                    StatystykiHandlowcow.Add(stat);

                DataAktualizacji = DateTime.Now;
                StatusText = $"Załadowano dane dla {LiczbaKontrahentow} kontrahentów";
            }
            catch (Exception ex)
            {
                StatusText = $"Błąd: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class Top10Item
    {
        public int Pozycja { get; set; }
        public string Kontrahent { get; set; }
        public string Nazwa { get; set; }
        public int E2 { get; set; }
        public int H1 { get; set; }
        public string Handlowiec { get; set; }
    }

    public class StatystykaHandlowca
    {
        public string Handlowiec { get; set; }
        public int LiczbaKontrahentow { get; set; }
        public int SumaE2 { get; set; }
        public int SumaH1 { get; set; }
        public int ProcentPotwierdzone { get; set; }

        public SolidColorBrush KolorPotwierdzone => ProcentPotwierdzone >= 80
            ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
            : ProcentPotwierdzone >= 50
                ? new SolidColorBrush(Color.FromRgb(255, 152, 0))
                : new SolidColorBrush(Color.FromRgb(244, 67, 54));
    }
}
