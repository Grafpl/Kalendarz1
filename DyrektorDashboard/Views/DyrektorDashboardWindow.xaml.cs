using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LiveCharts;
using LiveCharts.Wpf;
using Kalendarz1.DyrektorDashboard.Models;
using Kalendarz1.DyrektorDashboard.Services;
using Kalendarz1.HandlowiecDashboard.Models;

namespace Kalendarz1.DyrektorDashboard.Views
{
    public partial class DyrektorDashboardWindow : Window
    {
        private readonly DyrektorDashboardService _service;
        private readonly DashboardCache _cache = new() { DefaultExpiry = TimeSpan.FromMinutes(5) };
        private CancellationTokenSource _cts;
        private readonly HashSet<int> _loadedTabs = new();
        private DispatcherTimer _autoRefreshTimer;

        private static readonly string ConnLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string ConnHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private static readonly string ConnTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public Func<double, string> KgFormatter { get; set; }

        public DyrektorDashboardWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            KgFormatter = val => $"{val:N0} kg";
            DataContext = this;

            _service = new DyrektorDashboardService(ConnLibra, ConnHandel, ConnTransport);

            Loaded += Window_Loaded;
            Closed += Window_Closed;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();

            try
            {
                // 1. Załaduj karty KPI
                await LoadKpiCardsAsync();

                // 2. Załaduj pierwszą zakładkę
                await LoadTabDataAsync(0);
                _loadedTabs.Add(0);

                // 3. Uruchom auto-refresh
                StartAutoRefresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Window_Loaded error: {ex.Message}");
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _cts?.Cancel();
            _autoRefreshTimer?.Stop();
            _cache.Invalidate();
            _service?.Dispose();
        }

        // ════════════════════════════════════════════════════════════════════
        // KPI KARTY
        // ════════════════════════════════════════════════════════════════════

        private async Task LoadKpiCardsAsync()
        {
            try
            {
                var kpi = await _cache.GetOrLoadAsync("KpiKarty",
                    () => _service.GetKpiKartyAsync(_cts.Token));

                if (kpi == null) return;

                kpiZywiecKg.Text = $"{kpi.ZywiecDzisKg:N0} kg";
                kpiZywiecSub.Text = $"dostawy: {kpi.ZywiecDzisDostawy} | szt: {kpi.ZywiecSztukiDzis:N0}";

                kpiZamowieniaLiczba.Text = $"{kpi.ZamowieniaDzisLiczba} zam.";
                kpiZamowieniaSub.Text = $"{kpi.ZamowieniaDzisKg:N0} kg | {kpi.ZamowieniaDzisWartosc:N0} zł";

                kpiProdukcjaKg.Text = $"{kpi.ProdukcjaDzisKg:N0} kg";
                kpiProdukcjaSub.Text = $"LWP: {kpi.ProdukcjaLWPKg:N0} kg";

                kpiMagazynKg.Text = $"{kpi.MagazynStanKg:N0} kg";
                kpiMagazynSub.Text = $"wartość: {kpi.MagazynStanWartosc:N0} zł";

                kpiTransportKursy.Text = $"{kpi.TransportDzisKursy} kursów";
                kpiTransportSub.Text = $"aktywne: {kpi.TransportAktywneKursy} | kier.: {kpi.TransportKierowcyAktywni}";

                kpiReklamacjeOtwarte.Text = $"{kpi.ReklamacjeOtwarte} otwarte";
                kpiReklamacjeSub.Text = $"nowe: {kpi.ReklamacjeNowe} | {kpi.ReklamacjeSumaKg:N0} kg";

                txtLastRefresh.Text = $"Odświeżono: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadKpiCards error: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // ZARZĄDZANIE ZAKŁADKAMI (lazy loading)
        // ════════════════════════════════════════════════════════════════════

        private async void TabMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != tabMain) return;
            var idx = tabMain.SelectedIndex;

            if (_loadedTabs.Contains(idx)) return;

            try
            {
                await LoadTabDataAsync(idx);
                _loadedTabs.Add(idx);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tab {idx} load error: {ex.Message}");
            }
        }

        private async Task LoadTabDataAsync(int tabIndex)
        {
            switch (tabIndex)
            {
                case 0: await LoadZywiecTabAsync(); break;
                case 1: await LoadZamowieniaTabAsync(); break;
                case 2: await LoadProdukcjaTabAsync(); break;
                case 3: await LoadMagazynTabAsync(); break;
                case 4: await LoadTransportTabAsync(); break;
                case 5: await LoadReklamacjeTabAsync(); break;
                case 6: await LoadOpakowaniaTabAsync(); break;
                case 7: await LoadPlanTygodniowyTabAsync(); break;
                case 8: await LoadAlertyTabAsync(); break;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // TAB: ŻYWIEC
        // ════════════════════════════════════════════════════════════════════

        private async Task LoadZywiecTabAsync()
        {
            var dane = await _cache.GetOrLoadAsync("Zywiec",
                () => _service.GetDaneZywiecAsync(_cts.Token));
            if (dane == null) return;

            zywDzisKg.Text = $"{dane.DzisKg:N0} kg";
            zywTydzienKg.Text = $"{dane.TydzienKg:N0} kg";
            zywMiesiacKg.Text = $"{dane.MiesiacKg:N0} kg";
            zywSredniaCena.Text = $"{dane.SredniaCenaDzis:N2} zł/kg";
            zywUbytek.Text = $"{dane.SredniUbytekDzis:F1}%";

            // Wykres trend
            if (dane.Trend8Tygodni.Any())
            {
                chartZywiecTrend.Series = new SeriesCollection
                {
                    new ColumnSeries
                    {
                        Title = "Waga [kg]",
                        Values = new ChartValues<double>(dane.Trend8Tygodni.Select(t => (double)t.WagaKg)),
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),
                        MaxColumnWidth = 40
                    }
                };
                chartZywiecTrend.AxisX[0].Labels = dane.Trend8Tygodni.Select(t => t.Etykieta).ToList();
            }

            // Grid top hodowców
            gridTopHodowcy.Columns.Clear();
            gridTopHodowcy.Columns.Add(new DataGridTextColumn { Header = "#", Binding = new System.Windows.Data.Binding("Pozycja"), Width = 30 });
            gridTopHodowcy.Columns.Add(new DataGridTextColumn { Header = "Hodowca", Binding = new System.Windows.Data.Binding("Nazwa"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            gridTopHodowcy.Columns.Add(new DataGridTextColumn { Header = "Waga [kg]", Binding = new System.Windows.Data.Binding("WagaKg") { StringFormat = "N0" }, Width = 90 });
            gridTopHodowcy.Columns.Add(new DataGridTextColumn { Header = "Dostawy", Binding = new System.Windows.Data.Binding("LiczbaDostaw"), Width = 60 });
            gridTopHodowcy.ItemsSource = dane.TopHodowcy;

            // Grid dostawy dziś
            gridDostawyDzis.Columns.Clear();
            gridDostawyDzis.Columns.Add(new DataGridTextColumn { Header = "Godz.", Binding = new System.Windows.Data.Binding("Godzina") { StringFormat = "HH:mm" }, Width = 50 });
            gridDostawyDzis.Columns.Add(new DataGridTextColumn { Header = "Hodowca", Binding = new System.Windows.Data.Binding("Hodowca"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            gridDostawyDzis.Columns.Add(new DataGridTextColumn { Header = "Szt.", Binding = new System.Windows.Data.Binding("Sztuki") { StringFormat = "N0" }, Width = 60 });
            gridDostawyDzis.Columns.Add(new DataGridTextColumn { Header = "Waga [kg]", Binding = new System.Windows.Data.Binding("WagaKg") { StringFormat = "N0" }, Width = 80 });
            gridDostawyDzis.Columns.Add(new DataGridTextColumn { Header = "Cena", Binding = new System.Windows.Data.Binding("Cena") { StringFormat = "N2" }, Width = 60 });
            gridDostawyDzis.ItemsSource = dane.DostawyDzis;
        }

        // ════════════════════════════════════════════════════════════════════
        // TAB: ZAMÓWIENIA
        // ════════════════════════════════════════════════════════════════════

        private async Task LoadZamowieniaTabAsync()
        {
            var dane = await _cache.GetOrLoadAsync("Zamowienia",
                () => _service.GetDaneZamowieniaAsync(_cts.Token));
            if (dane == null) return;

            zamDzisInfo.Text = $"{dane.LiczbaZamowienDzis} zam. / {dane.SumaKgDzis:N0} kg";
            zamJutroInfo.Text = $"{dane.LiczbaZamowienJutro} zam. / {dane.SumaKgJutro:N0} kg";
            zamWartoscDzis.Text = $"{dane.SumaWartoscDzis:N0} zł";

            // Wykres trend
            if (dane.TrendDzienny.Any())
            {
                chartZamowieniaTrend.Series = new SeriesCollection
                {
                    new LineSeries
                    {
                        Title = "Kg zamówień",
                        Values = new ChartValues<double>(dane.TrendDzienny.Select(t => (double)t.SumaKg)),
                        Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB")),
                        Fill = new SolidColorBrush(Color.FromArgb(40, 52, 152, 219)),
                        PointGeometrySize = 6,
                        StrokeThickness = 2
                    },
                    new ColumnSeries
                    {
                        Title = "Liczba zamówień",
                        Values = new ChartValues<double>(dane.TrendDzienny.Select(t => (double)t.Liczba)),
                        Fill = new SolidColorBrush(Color.FromArgb(100, 39, 174, 96)),
                        MaxColumnWidth = 20,
                        ScalesYAt = 1
                    }
                };
                chartZamowieniaTrend.AxisX[0].Labels = dane.TrendDzienny.Select(t => t.Data.ToString("dd.MM")).ToList();

                if (chartZamowieniaTrend.AxisY.Count < 2)
                {
                    chartZamowieniaTrend.AxisY.Add(new Axis
                    {
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B949E")),
                        FontSize = 10,
                        Position = AxisPosition.RightTop
                    });
                }
            }

            // Grid top klientów
            gridTopKlienci.Columns.Clear();
            gridTopKlienci.Columns.Add(new DataGridTextColumn { Header = "Klient", Binding = new System.Windows.Data.Binding("Nazwa"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            gridTopKlienci.Columns.Add(new DataGridTextColumn { Header = "Kg", Binding = new System.Windows.Data.Binding("SumaKg") { StringFormat = "N0" }, Width = 90 });
            gridTopKlienci.Columns.Add(new DataGridTextColumn { Header = "Wartość [zł]", Binding = new System.Windows.Data.Binding("SumaWartosc") { StringFormat = "N0" }, Width = 100 });
            gridTopKlienci.Columns.Add(new DataGridTextColumn { Header = "Zam.", Binding = new System.Windows.Data.Binding("LiczbaZamowien"), Width = 50 });
            gridTopKlienci.ItemsSource = dane.TopKlienci;
        }

        // ════════════════════════════════════════════════════════════════════
        // TAB: PRODUKCJA
        // ════════════════════════════════════════════════════════════════════

        private async Task LoadProdukcjaTabAsync()
        {
            var dane = await _cache.GetOrLoadAsync("Produkcja",
                () => _service.GetDaneProdukcjaAsync(_cts.Token));
            if (dane == null) return;

            prodUbojKg.Text = $"{dane.UbojDzisKg:N0} kg";
            prodLWPKg.Text = $"{dane.KrojenieDzisKg:N0} kg";
            prodRWPKg.Text = $"{dane.RWPDzisKg:N0} kg";
            prodWydajnosc.Text = $"{dane.WydajnoscKrojeniaProcent:F1}%";

            // Wykres trend produkcji
            if (dane.TrendTygodniowy.Any())
            {
                chartProdukcjaTrend.Series = new SeriesCollection
                {
                    new StackedColumnSeries
                    {
                        Title = "Ubój (sPWU)",
                        Values = new ChartValues<double>(dane.TrendTygodniowy.Select(t => (double)t.UbojKg)),
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12")),
                        MaxColumnWidth = 40
                    },
                    new StackedColumnSeries
                    {
                        Title = "Krojenie (LWP)",
                        Values = new ChartValues<double>(dane.TrendTygodniowy.Select(t => (double)t.LWPKg)),
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB")),
                        MaxColumnWidth = 40
                    }
                };
                chartProdukcjaTrend.AxisX[0].Labels = dane.TrendTygodniowy
                    .Select(t => $"{t.DzienNazwa} {t.Data:dd.MM}").ToList();
            }

            // Grid top produktów
            gridTopProdukty.Columns.Clear();
            gridTopProdukty.Columns.Add(new DataGridTextColumn { Header = "Kod", Binding = new System.Windows.Data.Binding("Kod"), Width = 100 });
            gridTopProdukty.Columns.Add(new DataGridTextColumn { Header = "Nazwa", Binding = new System.Windows.Data.Binding("Nazwa"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            gridTopProdukty.Columns.Add(new DataGridTextColumn { Header = "Ilość [kg]", Binding = new System.Windows.Data.Binding("IloscKg") { StringFormat = "N0" }, Width = 90 });
            gridTopProdukty.Columns.Add(new DataGridTextColumn { Header = "Typ dok.", Binding = new System.Windows.Data.Binding("TypDokumentu"), Width = 70 });
            gridTopProdukty.ItemsSource = dane.TopProdukty;
        }

        // ════════════════════════════════════════════════════════════════════
        // TAB: MAGAZYN
        // ════════════════════════════════════════════════════════════════════

        private async Task LoadMagazynTabAsync()
        {
            var dane = await _cache.GetOrLoadAsync("Magazyn",
                () => _service.GetDaneMagazynAsync(_cts.Token));
            if (dane == null) return;

            magCaloscKg.Text = $"{dane.StanCaloscKg:N0} kg";
            magSwiezyKg.Text = $"{dane.StanSwiezyKg:N0} kg";
            magMrozonyKg.Text = $"{dane.StanMrozonyKg:N0} kg";
            magWartosc.Text = $"{dane.StanWartoscZl:N0} zł";

            gridMagazyn.Columns.Clear();
            gridMagazyn.Columns.Add(new DataGridTextColumn { Header = "Kod", Binding = new System.Windows.Data.Binding("Kod"), Width = 100 });
            gridMagazyn.Columns.Add(new DataGridTextColumn { Header = "Nazwa", Binding = new System.Windows.Data.Binding("Nazwa"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            gridMagazyn.Columns.Add(new DataGridTextColumn { Header = "Stan [kg]", Binding = new System.Windows.Data.Binding("IloscKg") { StringFormat = "N0" }, Width = 90 });
            gridMagazyn.Columns.Add(new DataGridTextColumn { Header = "Wartość [zł]", Binding = new System.Windows.Data.Binding("WartoscZl") { StringFormat = "N0" }, Width = 100 });
            gridMagazyn.Columns.Add(new DataGridTextColumn { Header = "Katalog", Binding = new System.Windows.Data.Binding("Katalog"), Width = 80 });
            gridMagazyn.ItemsSource = dane.TopProdukty;
        }

        // ════════════════════════════════════════════════════════════════════
        // TAB: TRANSPORT
        // ════════════════════════════════════════════════════════════════════

        private async Task LoadTransportTabAsync()
        {
            var dane = await _cache.GetOrLoadAsync("Transport",
                () => _service.GetDaneTransportAsync(_cts.Token));
            if (dane == null) return;

            trKursyDzis.Text = $"{dane.KursyDzis}";
            trWTrasie.Text = $"{dane.KursyWTrasie}";
            trZakonczone.Text = $"{dane.KursyZakonczone}";
            trKierowcy.Text = $"{dane.KierowcyAktywni}";

            gridKursy.Columns.Clear();
            gridKursy.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new System.Windows.Data.Binding("KursID"), Width = 60 });
            gridKursy.Columns.Add(new DataGridTextColumn { Header = "Kierowca", Binding = new System.Windows.Data.Binding("Kierowca"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            gridKursy.Columns.Add(new DataGridTextColumn { Header = "Pojazd", Binding = new System.Windows.Data.Binding("Pojazd"), Width = 120 });
            gridKursy.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new System.Windows.Data.Binding("Status"), Width = 100 });
            gridKursy.Columns.Add(new DataGridTextColumn { Header = "Ładunki", Binding = new System.Windows.Data.Binding("LiczbaLadunkow"), Width = 70 });
            gridKursy.ItemsSource = dane.Kursy;
        }

        // ════════════════════════════════════════════════════════════════════
        // TAB: REKLAMACJE
        // ════════════════════════════════════════════════════════════════════

        private async Task LoadReklamacjeTabAsync()
        {
            var dane = await _cache.GetOrLoadAsync("Reklamacje",
                () => _service.GetDaneReklamacjeAsync(_cts.Token));
            if (dane == null) return;

            rekNowe.Text = $"{dane.NoweCount}";
            rekWTrakcie.Text = $"{dane.WTrakcieCount}";
            rekZamkniete.Text = $"{dane.ZamknieteCount}";

            // Pie chart
            chartReklamacjePie.Series = new SeriesCollection();
            if (dane.NoweCount > 0)
                chartReklamacjePie.Series.Add(new PieSeries { Title = "Nowe", Values = new ChartValues<int> { dane.NoweCount }, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")), DataLabels = true });
            if (dane.WTrakcieCount > 0)
                chartReklamacjePie.Series.Add(new PieSeries { Title = "W trakcie", Values = new ChartValues<int> { dane.WTrakcieCount }, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12")), DataLabels = true });
            if (dane.ZaakceptowaneCount > 0)
                chartReklamacjePie.Series.Add(new PieSeries { Title = "Zaakceptowane", Values = new ChartValues<int> { dane.ZaakceptowaneCount }, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB")), DataLabels = true });
            if (dane.ZamknieteCount > 0)
                chartReklamacjePie.Series.Add(new PieSeries { Title = "Zamknięte", Values = new ChartValues<int> { dane.ZamknieteCount }, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")), DataLabels = true });

            // Grid
            gridReklamacje.Columns.Clear();
            gridReklamacje.Columns.Add(new DataGridTextColumn { Header = "Data", Binding = new System.Windows.Data.Binding("Data") { StringFormat = "dd.MM.yyyy" }, Width = 85 });
            gridReklamacje.Columns.Add(new DataGridTextColumn { Header = "Kontrahent", Binding = new System.Windows.Data.Binding("Kontrahent"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            gridReklamacje.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new System.Windows.Data.Binding("Status"), Width = 90 });
            gridReklamacje.Columns.Add(new DataGridTextColumn { Header = "Kg", Binding = new System.Windows.Data.Binding("IloscKg") { StringFormat = "N0" }, Width = 60 });
            gridReklamacje.ItemsSource = dane.OstatnieReklamacje;
        }

        // ════════════════════════════════════════════════════════════════════
        // TAB: OPAKOWANIA
        // ════════════════════════════════════════════════════════════════════

        private async Task LoadOpakowaniaTabAsync()
        {
            var dane = await _cache.GetOrLoadAsync("Opakowania",
                () => _service.GetDaneOpakowaniaAsync(_cts.Token));
            if (dane == null) return;

            opakE2.Text = $"{dane.SaldoE2:N0}";
            opakH1.Text = $"{dane.SaldoH1:N0}";
            opakInne.Text = $"{dane.SaldoInne:N0}";

            gridOpakowania.Columns.Clear();
            gridOpakowania.Columns.Add(new DataGridTextColumn { Header = "Typ opakowania", Binding = new System.Windows.Data.Binding("TypOpakowania"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            gridOpakowania.Columns.Add(new DataGridTextColumn { Header = "Wydane", Binding = new System.Windows.Data.Binding("Wydane") { StringFormat = "N0" }, Width = 100 });
            gridOpakowania.Columns.Add(new DataGridTextColumn { Header = "Przyjęte", Binding = new System.Windows.Data.Binding("Przyjete") { StringFormat = "N0" }, Width = 100 });
            gridOpakowania.Columns.Add(new DataGridTextColumn { Header = "Saldo", Binding = new System.Windows.Data.Binding("Saldo") { StringFormat = "N0" }, Width = 100 });
            gridOpakowania.ItemsSource = dane.SaldaWgTypu;
        }

        // ════════════════════════════════════════════════════════════════════
        // TAB: PLAN TYGODNIOWY
        // ════════════════════════════════════════════════════════════════════

        private async Task LoadPlanTygodniowyTabAsync()
        {
            var dane = await _cache.GetOrLoadAsync("PlanTygodniowy",
                () => _service.GetPlanTygodniowyAsync(_cts.Token));
            if (dane == null) return;

            planSumaKg.Text = $"{dane.PlanTygodniaSumaKg:N0} kg";
            planRealizacjaKg.Text = $"{dane.RealizacjaTygodniaSumaKg:N0} kg";
            planProcent.Text = $"{dane.RealizacjaProcent}%";

            if (dane.Dni.Any())
            {
                chartPlanTygodniowy.Series = new SeriesCollection
                {
                    new ColumnSeries
                    {
                        Title = "Plan",
                        Values = new ChartValues<double>(dane.Dni.Select(d => (double)d.PlanKg)),
                        Fill = new SolidColorBrush(Color.FromArgb(150, 52, 152, 219)),
                        MaxColumnWidth = 50
                    },
                    new ColumnSeries
                    {
                        Title = "Realizacja",
                        Values = new ChartValues<double>(dane.Dni.Select(d => (double)d.RealizacjaKg)),
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),
                        MaxColumnWidth = 50
                    }
                };
                chartPlanTygodniowy.AxisX[0].Labels = dane.Dni
                    .Select(d => $"{d.DzienTygodnia} {d.Data:dd.MM}" + (d.CzyDzisiaj ? " *" : "")).ToList();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // TAB: ALERTY
        // ════════════════════════════════════════════════════════════════════

        private async Task LoadAlertyTabAsync()
        {
            var alerty = await _cache.GetOrLoadAsync("Alerty",
                () => _service.GetAlertyAsync(_cts.Token));
            if (alerty == null) return;

            gridAlerty.Columns.Clear();
            gridAlerty.Columns.Add(new DataGridTextColumn { Header = "", Binding = new System.Windows.Data.Binding("Ikona"), Width = 30 });
            gridAlerty.Columns.Add(new DataGridTextColumn { Header = "Priorytet", Binding = new System.Windows.Data.Binding("Priorytet"), Width = 80 });
            gridAlerty.Columns.Add(new DataGridTextColumn { Header = "Typ", Binding = new System.Windows.Data.Binding("Typ"), Width = 90 });
            gridAlerty.Columns.Add(new DataGridTextColumn { Header = "Tytuł", Binding = new System.Windows.Data.Binding("Tytul"), Width = 180 });
            gridAlerty.Columns.Add(new DataGridTextColumn { Header = "Opis", Binding = new System.Windows.Data.Binding("Opis"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            gridAlerty.Columns.Add(new DataGridTextColumn { Header = "Data", Binding = new System.Windows.Data.Binding("Data") { StringFormat = "dd.MM.yyyy" }, Width = 85 });
            gridAlerty.ItemsSource = alerty;
        }

        // ════════════════════════════════════════════════════════════════════
        // AUTO-REFRESH
        // ════════════════════════════════════════════════════════════════════

        private void StartAutoRefresh()
        {
            _autoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(5)
            };
            _autoRefreshTimer.Tick += async (s, e) =>
            {
                _cache.Invalidate();
                _loadedTabs.Clear();
                await LoadKpiCardsAsync();
                await LoadTabDataAsync(tabMain.SelectedIndex);
                _loadedTabs.Add(tabMain.SelectedIndex);
            };
            _autoRefreshTimer.Start();
        }

        // ════════════════════════════════════════════════════════════════════
        // PRZYCISKI
        // ════════════════════════════════════════════════════════════════════

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _cache.Invalidate();
            _loadedTabs.Clear();
            loadingOverlay.Visibility = Visibility.Visible;

            try
            {
                await LoadKpiCardsAsync();
                await LoadTabDataAsync(tabMain.SelectedIndex);
                _loadedTabs.Add(tabMain.SelectedIndex);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
