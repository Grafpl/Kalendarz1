using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Kalendarz1.HandlowiecDashboard.Constants;
using Kalendarz1.HandlowiecDashboard.Models;
using Kalendarz1.HandlowiecDashboard.Services.Interfaces;
using Kalendarz1.HandlowiecDashboard.ViewModels.Base;

namespace Kalendarz1.HandlowiecDashboard.ViewModels
{
    public class OpakowaniaWTerenieViewModel : ViewModelBase
    {
        private readonly IOpakowaniaService _opakowaniaService;
        private readonly ILoggingService _logger;

        public OpakowaniaWTerenieViewModel(IOpakowaniaService opakowaniaService, ILoggingService logger)
        {
            _opakowaniaService = opakowaniaService;
            _logger = logger;

            Handlowcy = new ObservableCollection<string> { BusinessConstants.Filtry.WszyscyHandlowcy };

            RefreshCommand = new AsyncRelayCommand(LoadAllDataAsync);
            EksportujCommand = new RelayCommand(Eksportuj);
            WyslijPrzypomnienieCommand = new RelayCommand<SaldoOpakowanKontrahenta>(WyslijPrzypomnienie);
        }

        // Commands
        public ICommand RefreshCommand { get; }
        public ICommand EksportujCommand { get; }
        public ICommand WyslijPrzypomnienieCommand { get; }

        // Lista handlowców do ComboBoxa
        public ObservableCollection<string> Handlowcy { get; }

        // Etykiety dla aging chart
        private string[] _agingLabels;
        public string[] AgingLabels
        {
            get => _agingLabels;
            set => SetProperty(ref _agingLabels, value);
        }

        // Filtr
        private string _wybranyHandlowiec = BusinessConstants.Filtry.WszyscyHandlowcy;
        public string WybranyHandlowiec
        {
            get => _wybranyHandlowiec;
            set
            {
                if (SetProperty(ref _wybranyHandlowiec, value))
                    _ = LoadAllDataAsync();
            }
        }

        // Dane
        private OpakowaniaKPI _kpi;
        public OpakowaniaKPI Kpi
        {
            get => _kpi;
            set => SetProperty(ref _kpi, value);
        }

        private ObservableCollection<SaldoOpakowanKontrahenta> _saldaKontrahentow;
        public ObservableCollection<SaldoOpakowanKontrahenta> SaldaKontrahentow
        {
            get => _saldaKontrahentow;
            set => SetProperty(ref _saldaKontrahentow, value);
        }

        private ObservableCollection<AgingOpakowan> _aging;
        public ObservableCollection<AgingOpakowan> Aging
        {
            get => _aging;
            set => SetProperty(ref _aging, value);
        }

        private ObservableCollection<AlertOpakowania> _alerty;
        public ObservableCollection<AlertOpakowania> Alerty
        {
            get => _alerty;
            set => SetProperty(ref _alerty, value);
        }

        // Dla wykresów LiveCharts
        private SeriesCollection _agingChartSeries;
        public SeriesCollection AgingChartSeries
        {
            get => _agingChartSeries;
            set => SetProperty(ref _agingChartSeries, value);
        }

        private SeriesCollection _riskMapSeries;
        public SeriesCollection RiskMapSeries
        {
            get => _riskMapSeries;
            set => SetProperty(ref _riskMapSeries, value);
        }

        // Statystyki
        public int LiczbaKrytycznych => SaldaKontrahentow?.Count(s => s.RiskScore >= 80) ?? 0;
        public int LiczbaOstrzezen => SaldaKontrahentow?.Count(s => s.RiskScore >= 60 && s.RiskScore < 80) ?? 0;
        public int LiczbaDoMonitoringu => SaldaKontrahentow?.Count(s => s.RiskScore >= 40 && s.RiskScore < 60) ?? 0;

        public async Task LoadAllDataAsync()
        {
            try
            {
                IsLoading = true;
                ClearError();

                // Ładuj równolegle
                var kpiTask = _opakowaniaService.PobierzKPIAsync();
                var saldaTask = _opakowaniaService.PobierzSaldaZRyzykiemAsync(WybranyHandlowiec);
                var agingTask = _opakowaniaService.PobierzAgingAsync();
                var alertyTask = _opakowaniaService.PobierzAlertyAsync();

                await Task.WhenAll(kpiTask, saldaTask, agingTask, alertyTask);

                Kpi = await kpiTask;
                SaldaKontrahentow = new ObservableCollection<SaldoOpakowanKontrahenta>(await saldaTask);
                Aging = new ObservableCollection<AgingOpakowan>(await agingTask);
                Alerty = new ObservableCollection<AlertOpakowania>(await alertyTask);

                // Aktualizuj statystyki
                OnPropertyChanged(nameof(LiczbaKrytycznych));
                OnPropertyChanged(nameof(LiczbaOstrzezen));
                OnPropertyChanged(nameof(LiczbaDoMonitoringu));

                // Buduj wykresy
                BudujAgingChart();
                await BudujRiskMapAsync();

                _logger.LogInfo($"Załadowano dashboard opakowań: {SaldaKontrahentow.Count} kontrahentów, {Alerty.Count} alertów");
            }
            catch (Exception ex)
            {
                _logger.LogError("Błąd ładowania dashboardu opakowań", ex);
                SetError("Nie udało się załadować danych. Sprawdź połączenie z bazą.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void BudujAgingChart()
        {
            if (Aging == null) return;

            AgingLabels = Aging.Select(a => a.Przedzial).ToArray();

            AgingChartSeries = new SeriesCollection
            {
                new StackedColumnSeries
                {
                    Title = "E2",
                    Values = new ChartValues<int>(Aging.Select(a => a.IloscE2)),
                    Fill = System.Windows.Media.Brushes.DodgerBlue
                },
                new StackedColumnSeries
                {
                    Title = "H1",
                    Values = new ChartValues<int>(Aging.Select(a => a.IloscH1)),
                    Fill = System.Windows.Media.Brushes.Orange
                }
            };
        }

        private async Task BudujRiskMapAsync()
        {
            var punkty = await _opakowaniaService.PobierzMapeRyzykaAsync(WybranyHandlowiec);
            
            RiskMapSeries = new SeriesCollection
            {
                new ScatterSeries
                {
                    Title = "Kontrahenci",
                    Values = new ChartValues<ScatterPoint>(
                        punkty.Select(p => new ScatterPoint(p.X, p.Y, p.Size))),
                    MinPointShapeDiameter = 10,
                    MaxPointShapeDiameter = 50
                }
            };
        }

        public void UstawHandlowcow(IEnumerable<string> handlowcy)
        {
            Handlowcy.Clear();
            Handlowcy.Add(BusinessConstants.Filtry.WszyscyHandlowcy);
            foreach (var h in handlowcy)
                Handlowcy.Add(h);
        }

        private void Eksportuj(object _)
        {
            // TODO: Eksport do Excel
            _logger.LogInfo("Eksport opakowań - do implementacji");
        }

        private void WyslijPrzypomnienie(SaldoOpakowanKontrahenta kontrahent)
        {
            if (kontrahent == null) return;
            // TODO: Wysłanie emaila/SMS do handlowca
            _logger.LogInfo($"Przypomnienie dla {kontrahent.Kontrahent} - do implementacji");
        }
    }
}
