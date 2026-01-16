using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.ViewModels
{
    /// <summary>
    /// ViewModel dla szczegółów kontrahenta
    /// </summary>
    public class SzczegolyKontrahentaViewModel : ViewModelBase
    {
        private readonly SaldaService _service;
        private readonly ExportService _exportService;

        public string UserId { get; }
        public SaldoKontrahenta Kontrahent { get; }

        /// <summary>
        /// Callback do otwierania dialogu potwierdzenia
        /// </summary>
        public Action<string, int> RequestOpenPotwierdzenieDialog { get; set; }

        public SzczegolyKontrahentaViewModel(SaldoKontrahenta kontrahent, DateTime dataDo, string userId)
        {
            Kontrahent = kontrahent;
            UserId = userId;
            _service = new SaldaService();
            _exportService = new ExportService();

            _dataDo = dataDo;
            _dataOd = dataDo.AddDays(-30);

            ListaDokumentow = new ObservableCollection<DokumentSalda>();
            ListaPotwierdzen = new ObservableCollection<Potwierdzenie>();

            // Komendy
            OdswiezDokumentyCommand = new AsyncRelayCommand(OdswiezDokumentyAsync);
            OdswiezPotwierdzeniaCommand = new AsyncRelayCommand(OdswiezPotwierdzeniaAsync);

            PotwierdzenieE2Command = new RelayCommand(_ => OtworzPotwierdzenie("E2", Kontrahent.E2));
            PotwierdzenieH1Command = new RelayCommand(_ => OtworzPotwierdzenie("H1", Kontrahent.H1));
            PotwierdzenieEUROCommand = new RelayCommand(_ => OtworzPotwierdzenie("EURO", Kontrahent.EURO));
            PotwierdzeniePCVCommand = new RelayCommand(_ => OtworzPotwierdzenie("PCV", Kontrahent.PCV));
            PotwierdzenieDREWCommand = new RelayCommand(_ => OtworzPotwierdzenie("DREW", Kontrahent.DREW));

            EksportPdfCommand = new AsyncRelayCommand(EksportPdfAsync);
            EksportExcelCommand = new AsyncRelayCommand(EksportExcelAsync);
            DrukujCommand = new AsyncRelayCommand(DrukujAsync);

            // Załaduj dane
            _ = InitAsync();
        }

        #region Properties

        public string TytulOkna => $"{Kontrahent.Kontrahent} - Szczegóły opakowań";
        public string NazwaKontrahenta => $"{Kontrahent.Kontrahent} - {Kontrahent.Nazwa}";

        private DateTime _dataOd;
        public DateTime DataOd
        {
            get => _dataOd;
            set => SetProperty(ref _dataOd, value);
        }

        private DateTime _dataDo;
        public DateTime DataDo
        {
            get => _dataDo;
            set => SetProperty(ref _dataDo, value);
        }

        public ObservableCollection<DokumentSalda> ListaDokumentow { get; }
        public ObservableCollection<Potwierdzenie> ListaPotwierdzen { get; }

        // Status potwierdzeń per typ
        public string E2StatusText => GetStatusText(Kontrahent.E2Potwierdzone, Kontrahent.E2DataPotwierdzenia);
        public string H1StatusText => GetStatusText(Kontrahent.H1Potwierdzone, Kontrahent.H1DataPotwierdzenia);
        public string EUROStatusText => GetStatusText(Kontrahent.EUROPotwierdzone, Kontrahent.EURODataPotwierdzenia);
        public string PCVStatusText => GetStatusText(Kontrahent.PCVPotwierdzone, Kontrahent.PCVDataPotwierdzenia);
        public string DREWStatusText => GetStatusText(Kontrahent.DREWPotwierdzone, Kontrahent.DREWDataPotwierdzenia);

        // Visibility dla przycisków potwierdzeń
        public bool CzyE2Niezerowe => Kontrahent.E2 != 0;
        public bool CzyH1Niezerowe => Kontrahent.H1 != 0;
        public bool CzyEURONiezerowe => Kontrahent.EURO != 0;
        public bool CzyPCVNiezerowe => Kontrahent.PCV != 0;
        public bool CzyDREWNiezerowe => Kontrahent.DREW != 0;

        #endregion

        #region Commands

        public ICommand OdswiezDokumentyCommand { get; }
        public ICommand OdswiezPotwierdzeniaCommand { get; }

        public ICommand PotwierdzenieE2Command { get; }
        public ICommand PotwierdzenieH1Command { get; }
        public ICommand PotwierdzenieEUROCommand { get; }
        public ICommand PotwierdzeniePCVCommand { get; }
        public ICommand PotwierdzenieDREWCommand { get; }

        public ICommand EksportPdfCommand { get; }
        public ICommand EksportExcelCommand { get; }
        public ICommand DrukujCommand { get; }

        #endregion

        #region Methods

        private async Task InitAsync()
        {
            await OdswiezDokumentyAsync();
            await OdswiezPotwierdzeniaAsync();
        }

        private async Task OdswiezDokumentyAsync()
        {
            await ExecuteAsync(async () =>
            {
                var dokumenty = await _service.PobierzDokumentyAsync(Kontrahent.Id, DataOd, DataDo);

                ListaDokumentow.Clear();
                foreach (var doc in dokumenty)
                {
                    ListaDokumentow.Add(doc);
                }
            }, "Pobieranie dokumentów...");
        }

        private async Task OdswiezPotwierdzeniaAsync()
        {
            await ExecuteAsync(async () =>
            {
                var potwierdzenia = await _service.PobierzPotwierdzeniaKontrahentaAsync(Kontrahent.Id);

                ListaPotwierdzen.Clear();
                foreach (var p in potwierdzenia)
                {
                    ListaPotwierdzen.Add(p);
                }
            }, "Pobieranie potwierdzeń...");
        }

        private void OtworzPotwierdzenie(string typOpakowania, int saldoSystemowe)
        {
            RequestOpenPotwierdzenieDialog?.Invoke(typOpakowania, saldoSystemowe);
        }

        private string GetStatusText(bool potwierdzone, DateTime? dataPotwierdzenia)
        {
            if (potwierdzone && dataPotwierdzenia.HasValue)
            {
                return $"✓ {dataPotwierdzenia.Value:dd.MM}";
            }
            return "✗ brak";
        }

        private async Task EksportPdfAsync()
        {
            await ExecuteAsync(async () =>
            {
                await Task.Run(() =>
                {
                    _exportService.EksportujPdf(
                        Kontrahent,
                        ListaDokumentow,
                        DataOd,
                        DataDo);
                });
                StatusMessage = "PDF wygenerowany";
            }, "Generowanie PDF...");
        }

        private async Task EksportExcelAsync()
        {
            await ExecuteAsync(async () =>
            {
                await Task.Run(() =>
                {
                    _exportService.EksportujExcel(
                        Kontrahent,
                        ListaDokumentow,
                        DataOd,
                        DataDo);
                });
                StatusMessage = "Excel wygenerowany";
            }, "Generowanie Excel...");
        }

        private async Task DrukujAsync()
        {
            await ExecuteAsync(async () =>
            {
                await Task.Run(() =>
                {
                    _exportService.Drukuj(
                        Kontrahent,
                        ListaDokumentow,
                        DataOd,
                        DataDo);
                });
                StatusMessage = "Drukowanie...";
            }, "Przygotowywanie do druku...");
        }

        #endregion
    }
}
