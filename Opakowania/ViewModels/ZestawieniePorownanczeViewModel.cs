using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.ViewModels
{
    public class ZestawieniePorownanczeViewModel : INotifyPropertyChanged
    {
        private readonly SaldaService _saldaService;
        private readonly string _userId;

        public ZestawieniePorownanczeViewModel(string userId)
        {
            _userId = userId;
            _saldaService = new SaldaService();

            Wyniki = new ObservableCollection<PorownanieItem>();

            // Domyślne daty - poprzedni miesiąc vs teraz
            Data1 = DateTime.Today.AddMonths(-1);
            Data2 = DateTime.Today;

            PorownajCommand = new RelayCommand(async _ => await PorownajAsync());
        }

        #region Properties

        private DateTime _data1;
        public DateTime Data1
        {
            get => _data1;
            set { _data1 = value; OnPropertyChanged(); }
        }

        private DateTime _data2;
        public DateTime Data2
        {
            get => _data2;
            set { _data2 = value; OnPropertyChanged(); }
        }

        private int _wybranyTyp;
        public int WybranyTyp
        {
            get => _wybranyTyp;
            set { _wybranyTyp = value; OnPropertyChanged(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private int _sumaOkres1;
        public int SumaOkres1
        {
            get => _sumaOkres1;
            set { _sumaOkres1 = value; OnPropertyChanged(); }
        }

        private int _sumaOkres2;
        public int SumaOkres2
        {
            get => _sumaOkres2;
            set { _sumaOkres2 = value; OnPropertyChanged(); }
        }

        private int _roznica;
        public int Roznica
        {
            get => _roznica;
            set { _roznica = value; OnPropertyChanged(); OnPropertyChanged(nameof(RoznicaKolor)); }
        }

        private double _zmianaProcent;
        public double ZmianaProcent
        {
            get => _zmianaProcent;
            set { _zmianaProcent = value; OnPropertyChanged(); }
        }

        public SolidColorBrush RoznicaKolor => Roznica > 0
            ? new SolidColorBrush(Color.FromRgb(229, 57, 53))   // Czerwony - wzrost zadłużenia
            : Roznica < 0
                ? new SolidColorBrush(Color.FromRgb(67, 160, 71))  // Zielony - spadek
                : new SolidColorBrush(Color.FromRgb(117, 117, 117)); // Szary

        public ObservableCollection<PorownanieItem> Wyniki { get; }

        public ICommand PorownajCommand { get; }

        #endregion

        private async Task PorownajAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Pobieranie danych...";

                // Pobierz dane z obu okresów
                SaldaService.InvalidateCache();
                var salda1 = await _saldaService.PobierzWszystkieSaldaAsync(Data1);

                SaldaService.InvalidateCache();
                var salda2 = await _saldaService.PobierzWszystkieSaldaAsync(Data2);

                // Połącz dane
                var wszystkieKontrahenci = salda1.Select(s => s.Id)
                    .Union(salda2.Select(s => s.Id))
                    .Distinct();

                Wyniki.Clear();

                foreach (var kontrahentId in wszystkieKontrahenci)
                {
                    var s1 = salda1.FirstOrDefault(s => s.Id == kontrahentId);
                    var s2 = salda2.FirstOrDefault(s => s.Id == kontrahentId);

                    int saldo1, saldo2;

                    switch (WybranyTyp)
                    {
                        case 1: // E2
                            saldo1 = s1?.E2 ?? 0;
                            saldo2 = s2?.E2 ?? 0;
                            break;
                        case 2: // H1
                            saldo1 = s1?.H1 ?? 0;
                            saldo2 = s2?.H1 ?? 0;
                            break;
                        default: // Wszystkie
                            saldo1 = (s1?.E2 ?? 0) + (s1?.H1 ?? 0);
                            saldo2 = (s2?.E2 ?? 0) + (s2?.H1 ?? 0);
                            break;
                    }

                    // Pokaż tylko jeśli jest jakakolwiek zmiana lub saldo
                    if (saldo1 != 0 || saldo2 != 0)
                    {
                        var kontrahent = s1 ?? s2;
                        var roznica = saldo2 - saldo1;
                        var zmiana = saldo1 != 0 ? ((double)(saldo2 - saldo1) / saldo1 * 100) : (saldo2 != 0 ? 100 : 0);

                        Wyniki.Add(new PorownanieItem
                        {
                            KontrahentId = kontrahentId,
                            Kontrahent = kontrahent?.Kontrahent ?? "-",
                            Nazwa = kontrahent?.Nazwa ?? "-",
                            Handlowiec = kontrahent?.Handlowiec ?? "-",
                            SaldoOkres1 = saldo1,
                            SaldoOkres2 = saldo2,
                            Roznica = roznica,
                            ZmianaProcent = (int)zmiana
                        });
                    }
                }

                // Sortuj po największej różnicy
                var sorted = Wyniki.OrderByDescending(w => Math.Abs(w.Roznica)).ToList();
                Wyniki.Clear();
                foreach (var item in sorted)
                    Wyniki.Add(item);

                // Podsumowanie
                SumaOkres1 = Wyniki.Sum(w => w.SaldoOkres1);
                SumaOkres2 = Wyniki.Sum(w => w.SaldoOkres2);
                Roznica = SumaOkres2 - SumaOkres1;
                ZmianaProcent = SumaOkres1 != 0 ? ((double)(SumaOkres2 - SumaOkres1) / SumaOkres1 * 100) : 0;

                StatusText = $"Znaleziono {Wyniki.Count} kontrahentów ze zmianami";
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

    public class PorownanieItem
    {
        public int KontrahentId { get; set; }
        public string Kontrahent { get; set; }
        public string Nazwa { get; set; }
        public string Handlowiec { get; set; }
        public int SaldoOkres1 { get; set; }
        public int SaldoOkres2 { get; set; }
        public int Roznica { get; set; }
        public int ZmianaProcent { get; set; }

        public SolidColorBrush RoznicaKolor => Roznica > 0
            ? new SolidColorBrush(Color.FromRgb(229, 57, 53))
            : Roznica < 0
                ? new SolidColorBrush(Color.FromRgb(67, 160, 71))
                : new SolidColorBrush(Color.FromRgb(117, 117, 117));
    }
}
