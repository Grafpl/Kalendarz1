using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.ViewModels
{
    /// <summary>
    /// ViewModel dla listy kontrahentów dla wybranego typu opakowania
    /// </summary>
    public class ListaOpakowanViewModel : ViewModelBase
    {
        private readonly SaldaService _service;
        private readonly string _kodOpakowania;
        private readonly string _nazwaOpakowania;
        private string _handlowiecFilter;
        private List<SaldoKontrahentaOpakowania> _wszystkieDane;

        public string UserId { get; }

        public ListaOpakowanViewModel(string kodOpakowania, string nazwaOpakowania, DateTime dataDo, string userId)
        {
            _kodOpakowania = kodOpakowania;
            _nazwaOpakowania = nazwaOpakowania;
            _dataDo = dataDo;
            UserId = userId;
            _service = new SaldaService();
            _wszystkieDane = new List<SaldoKontrahentaOpakowania>();
            _tylkoNiezerowe = true;

            ListaKontrahentow = new ObservableCollection<SaldoKontrahentaOpakowania>();
            ListaHandlowcow = new ObservableCollection<string> { "Wszyscy" };

            OdswiezCommand = new AsyncRelayCommand(OdswiezAsync);

            _ = InitAsync();
        }

        #region Properties

        public string TytulOkna => $"{_nazwaOpakowania} - Salda";
        public string TytulOpakowania => _nazwaOpakowania.ToUpper();

        private DateTime _dataDo;
        public DateTime DataDo
        {
            get => _dataDo;
            set
            {
                if (SetProperty(ref _dataDo, value))
                {
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
                if (SetProperty(ref _filtrTekst, value))
                {
                    FiltrujListe();
                }
            }
        }

        private string _wybranyHandlowiec = "Wszyscy";
        public string WybranyHandlowiec
        {
            get => _wybranyHandlowiec;
            set
            {
                if (SetProperty(ref _wybranyHandlowiec, value))
                {
                    FiltrujListe();
                }
            }
        }

        private bool _tylkoNiezerowe;
        public bool TylkoNiezerowe
        {
            get => _tylkoNiezerowe;
            set
            {
                if (SetProperty(ref _tylkoNiezerowe, value))
                {
                    FiltrujListe();
                }
            }
        }

        private SaldoKontrahentaOpakowania _wybranyKontrahent;
        public SaldoKontrahentaOpakowania WybranyKontrahent
        {
            get => _wybranyKontrahent;
            set => SetProperty(ref _wybranyKontrahent, value);
        }

        public ObservableCollection<SaldoKontrahentaOpakowania> ListaKontrahentow { get; }
        public ObservableCollection<string> ListaHandlowcow { get; }

        // Statystyki
        private int _sumaWinni;
        public int SumaWinni
        {
            get => _sumaWinni;
            set => SetProperty(ref _sumaWinni, value);
        }

        private int _sumaMyWinni;
        public int SumaMyWinni
        {
            get => _sumaMyWinni;
            set => SetProperty(ref _sumaMyWinni, value);
        }

        private int _liczbaKontrahentow;
        public int LiczbaKontrahentow
        {
            get => _liczbaKontrahentow;
            set => SetProperty(ref _liczbaKontrahentow, value);
        }

        private int _liczbaBezPotwierdzenia;
        public int LiczbaBezPotwierdzenia
        {
            get => _liczbaBezPotwierdzenia;
            set => SetProperty(ref _liczbaBezPotwierdzenia, value);
        }

        #endregion

        #region Commands

        public ICommand OdswiezCommand { get; }

        #endregion

        #region Methods

        private async Task InitAsync()
        {
            _handlowiecFilter = await _service.PobierzHandlowcaAsync(UserId);
            await OdswiezAsync();
        }

        private async Task OdswiezAsync()
        {
            await ExecuteAsync(async () =>
            {
                var wszystkieSalda = await _service.PobierzWszystkieSaldaAsync(DataDo, _handlowiecFilter);

                _wszystkieDane = wszystkieSalda
                    .Select(s => new SaldoKontrahentaOpakowania
                    {
                        Id = s.Id,
                        Kontrahent = s.Kontrahent,
                        Nazwa = s.Nazwa,
                        Handlowiec = s.Handlowiec,
                        SaldoAktualneOpakowania = s.GetSaldo(_kodOpakowania),
                        JestPotwierdzone = s.GetPotwierdzone(_kodOpakowania),
                        DataPotwierdzenia = GetDataPotwierdzenia(s, _kodOpakowania),
                        OstatniDokument = s.OstatniDokument,
                        // Wszystkie salda dla szczegółów
                        E2 = s.E2,
                        H1 = s.H1,
                        EURO = s.EURO,
                        PCV = s.PCV,
                        DREW = s.DREW
                    })
                    .ToList();

                // Pobierz listę handlowców
                var handlowcy = _wszystkieDane
                    .Select(s => s.Handlowiec)
                    .Where(h => !string.IsNullOrEmpty(h) && h != "-")
                    .Distinct()
                    .OrderBy(h => h)
                    .ToList();

                ListaHandlowcow.Clear();
                ListaHandlowcow.Add("Wszyscy");
                foreach (var h in handlowcy)
                {
                    ListaHandlowcow.Add(h);
                }

                FiltrujListe();
            }, "Pobieranie danych...");
        }

        private DateTime? GetDataPotwierdzenia(SaldoKontrahenta s, string kod)
        {
            return kod switch
            {
                "E2" => s.E2DataPotwierdzenia,
                "H1" => s.H1DataPotwierdzenia,
                "EURO" => s.EURODataPotwierdzenia,
                "PCV" => s.PCVDataPotwierdzenia,
                "DREW" => s.DREWDataPotwierdzenia,
                _ => null
            };
        }

        private void FiltrujListe()
        {
            var filtr = FiltrTekst?.ToLower() ?? "";

            var przefiltrowane = _wszystkieDane
                .Where(s =>
                    // Filtr tekstowy
                    (string.IsNullOrEmpty(filtr) ||
                     s.Kontrahent?.ToLower().Contains(filtr) == true ||
                     s.Nazwa?.ToLower().Contains(filtr) == true) &&
                    // Filtr handlowca
                    (WybranyHandlowiec == "Wszyscy" || s.Handlowiec == WybranyHandlowiec) &&
                    // Tylko niezerowe
                    (!TylkoNiezerowe || s.SaldoAktualneOpakowania != 0))
                .OrderByDescending(s => s.SaldoAktualneOpakowania)
                .ToList();

            ListaKontrahentow.Clear();
            foreach (var s in przefiltrowane)
            {
                ListaKontrahentow.Add(s);
            }

            // Statystyki
            SumaWinni = przefiltrowane.Where(s => s.SaldoAktualneOpakowania > 0).Sum(s => s.SaldoAktualneOpakowania);
            SumaMyWinni = Math.Abs(przefiltrowane.Where(s => s.SaldoAktualneOpakowania < 0).Sum(s => s.SaldoAktualneOpakowania));
            LiczbaKontrahentow = przefiltrowane.Count;
            LiczbaBezPotwierdzenia = przefiltrowane.Count(s => !s.JestPotwierdzone && s.SaldoAktualneOpakowania != 0);
        }

        #endregion
    }

    /// <summary>
    /// Model pomocniczy dla listy kontrahentów opakowania
    /// </summary>
    public class SaldoKontrahentaOpakowania : SaldoKontrahenta
    {
        public int SaldoAktualneOpakowania { get; set; }
        public bool JestPotwierdzone { get; set; }
        public bool MaRozbieznosc { get; set; }
        public bool BrakPotwierdzenia => !JestPotwierdzone && !MaRozbieznosc;
        public DateTime? DataPotwierdzenia { get; set; }
        public string DataPotwierdzeniaText => DataPotwierdzenia?.ToString("dd.MM.yyyy") ?? "-";
    }
}
