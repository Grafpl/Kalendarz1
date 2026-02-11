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
        private CancellationTokenSource _cts = new();
        private readonly HashSet<int> _loadedTabs = new();
        private DispatcherTimer _autoRefreshTimer;
        private bool _initialized;

        // Zamówienia - stan filtrów
        private DaneZamowieniaSzczegoly _zamSzczegoly;
        private bool _zamPokazDzis = true;
        private string _zamFiltrProdukt;

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
            try
            {
                // 1. Załaduj karty KPI
                await LoadKpiCardsAsync();

                // 2. Załaduj pierwszą zakładkę
                await LoadTabDataAsync(0);
                _loadedTabs.Add(0);

                // 3. Uruchom auto-refresh
                StartAutoRefresh();

                _initialized = true;
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
            if (!_initialized || e.Source != tabMain) return;
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

            // ── Plan tygodniowy żywca (Pon-Pt) z liczbą dostaw ──
            try
            {
                var plan = await _cache.GetOrLoadAsync("PlanTygodniowy",
                    () => _service.GetPlanTygodniowyAsync(_cts.Token));
                if (plan != null)
                {
                    zywPlanTygKg.Text = $"{plan.PlanTygodniaSumaKg:N0} kg";
                    zywRealTygKg.Text = $"{plan.RealizacjaTygodniaSumaKg:N0} kg";
                    zywRealTygProcent.Text = $"{plan.RealizacjaProcent}%";

                    var borders = new[] { zywPlanDzien0, zywPlanDzien1, zywPlanDzien2, zywPlanDzien3, zywPlanDzien4 };
                    var nazwy = new[] { zywPlanDzien0Nazwa, zywPlanDzien1Nazwa, zywPlanDzien2Nazwa, zywPlanDzien3Nazwa, zywPlanDzien4Nazwa };
                    var daty = new[] { zywPlanDzien0Data, zywPlanDzien1Data, zywPlanDzien2Data, zywPlanDzien3Data, zywPlanDzien4Data };
                    var reals = new[] { zywPlanDzien0Real, zywPlanDzien1Real, zywPlanDzien2Real, zywPlanDzien3Real, zywPlanDzien4Real };
                    var dosts = new[] { zywPlanDzien0Dost, zywPlanDzien1Dost, zywPlanDzien2Dost, zywPlanDzien3Dost, zywPlanDzien4Dost };
                    var plans = new[] { zywPlanDzien0Plan, zywPlanDzien1Plan, zywPlanDzien2Plan, zywPlanDzien3Plan, zywPlanDzien4Plan };
                    var procs = new[] { zywPlanDzien0Proc, zywPlanDzien1Proc, zywPlanDzien2Proc, zywPlanDzien3Proc, zywPlanDzien4Proc };

                    for (int i = 0; i < Math.Min(plan.Dni.Count, 5); i++)
                    {
                        var d = plan.Dni[i];
                        nazwy[i].Text = d.DzienTygodnia;
                        daty[i].Text = d.Data.ToString("dd.MM");
                        reals[i].Text = $"{d.RealizacjaKg:N0} kg";
                        dosts[i].Text = d.LiczbaDostaw > 0 ? $"dostaw: {d.LiczbaDostaw}" : "";
                        plans[i].Text = $"plan: {d.PlanKg:N0}";
                        procs[i].Text = d.ProcentRealizacji > 0 ? $"{d.ProcentRealizacji}%" : "";

                        if (d.CzyDzisiaj)
                            borders[i].Background = new SolidColorBrush(Color.FromArgb(40, 212, 168, 67));
                        else if (d.Data < DateTime.Today)
                            borders[i].Background = new SolidColorBrush(Color.FromArgb(20, 39, 174, 96));
                        else
                            borders[i].Background = new SolidColorBrush(Color.FromArgb(15, 52, 152, 219));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Plan tygodniowy w Żywiec error: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // TAB: ZAMÓWIENIA
        // ════════════════════════════════════════════════════════════════════

        private async Task LoadZamowieniaTabAsync()
        {
            _zamSzczegoly = await _cache.GetOrLoadAsync("ZamowieniaSzczegoly",
                () => _service.GetDaneZamowieniaSzczegolyAsync(_cts.Token));
            if (_zamSzczegoly == null) return;

            // KPI
            zamDzisInfo.Text = $"{_zamSzczegoly.LiczbaDzis} zam. / {_zamSzczegoly.SumaKgDzis:N0} kg";
            zamJutroInfo.Text = $"{_zamSzczegoly.LiczbaJutro} zam. / {_zamSzczegoly.SumaKgJutro:N0} kg";
            zamWartoscDzis.Text = $"{_zamSzczegoly.WartoscDzis:N0} zł";

            // Wypełnij ComboBox produktów
            zamCmbProdukt.Items.Clear();
            zamCmbProdukt.Items.Add("Wszystkie produkty");
            foreach (var p in _zamSzczegoly.UnikatoweProdukty)
                zamCmbProdukt.Items.Add(p);
            zamCmbProdukt.SelectedIndex = 0;

            // Konfiguruj DataGrid kolumny
            zamGridZamowienia.Columns.Clear();
            zamGridZamowienia.Columns.Add(new DataGridTextColumn { Header = "Klient", Binding = new System.Windows.Data.Binding("Klient"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            zamGridZamowienia.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new System.Windows.Data.Binding("Status"), Width = 80 });
            zamGridZamowienia.Columns.Add(new DataGridTextColumn { Header = "Produkt", Binding = new System.Windows.Data.Binding("Produkt"), Width = 200 });
            zamGridZamowienia.Columns.Add(new DataGridTextColumn { Header = "Ilość [kg]", Binding = new System.Windows.Data.Binding("IloscKg") { StringFormat = "N0" }, Width = 90 });
            zamGridZamowienia.Columns.Add(new DataGridTextColumn { Header = "Cena", Binding = new System.Windows.Data.Binding("Cena"), Width = 80 });

            // Pokaż dane
            OdswiezGridZamowien();
        }

        private void OdswiezGridZamowien()
        {
            if (_zamSzczegoly == null) return;

            var lista = _zamPokazDzis ? _zamSzczegoly.ZamowieniaDzis : _zamSzczegoly.ZamowieniaJutro;

            // Filtr produktu
            if (!string.IsNullOrEmpty(_zamFiltrProdukt))
                lista = lista.Where(z => z.Produkt == _zamFiltrProdukt).ToList();

            zamGridZamowienia.ItemsSource = lista;

            // Podsumowanie
            var unikZam = lista.Select(z => z.ZamowienieId).Distinct().Count();
            var sumaKg = lista.Sum(z => z.IloscKg);
            var dzien = _zamPokazDzis ? "DZIŚ" : "JUTRO";
            zamTxtPodsumowanie.Text = $"{dzien}: {unikZam} zamówień / {sumaKg:N0} kg / {lista.Count} pozycji";

            // Toggle style
            zamBtnDzis.Background = new SolidColorBrush(_zamPokazDzis
                ? (Color)ColorConverter.ConvertFromString("#D4A843")
                : (Color)ColorConverter.ConvertFromString("#3D4450"));
            zamBtnDzis.Foreground = new SolidColorBrush(_zamPokazDzis
                ? (Color)ColorConverter.ConvertFromString("#1A1D21")
                : (Color)ColorConverter.ConvertFromString("#8B949E"));

            zamBtnJutro.Background = new SolidColorBrush(!_zamPokazDzis
                ? (Color)ColorConverter.ConvertFromString("#D4A843")
                : (Color)ColorConverter.ConvertFromString("#3D4450"));
            zamBtnJutro.Foreground = new SolidColorBrush(!_zamPokazDzis
                ? (Color)ColorConverter.ConvertFromString("#1A1D21")
                : (Color)ColorConverter.ConvertFromString("#8B949E"));
        }

        private void ZamBtnDzis_Click(object sender, RoutedEventArgs e)
        {
            _zamPokazDzis = true;
            OdswiezGridZamowien();
        }

        private void ZamBtnJutro_Click(object sender, RoutedEventArgs e)
        {
            _zamPokazDzis = false;
            OdswiezGridZamowien();
        }

        private void ZamCmbProdukt_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized) return;
            var selected = zamCmbProdukt.SelectedItem?.ToString();
            _zamFiltrProdukt = selected == "Wszystkie produkty" ? null : selected;
            OdswiezGridZamowien();
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

        // ════════════════════════════════════════════════════════════════════
        // DIAGNOSTYKA - ZAAWANSOWANY DEBUGGER
        // ════════════════════════════════════════════════════════════════════

        private async void BtnDebug_Click(object sender, RoutedEventArgs e)
        {
            // Utwórz okno diagnostyczne w kodzie (bez XAML)
            var diagWindow = new Window
            {
                Title = "Diagnostyka Panelu Dyrektora - Zaawansowany Debugger SQL",
                Width = 1100,
                Height = 750,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1D21")),
                Foreground = new SolidColorBrush(Colors.White)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Nagłówek
            var header = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252A31")),
                Padding = new Thickness(16, 12, 16, 12)
            };
            var headerPanel = new StackPanel();
            headerPanel.Children.Add(new TextBlock
            {
                Text = "DIAGNOSTYKA SQL - Panel Dyrektora",
                FontSize = 16, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"))
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = "Testowanie połączeń, struktur tabel i zapytań. Proszę czekać...",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B949E")),
                Margin = new Thickness(0, 4, 0, 0)
            });
            header.Child = headerPanel;
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // TextBox z logami
            var logBox = new TextBox
            {
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D1117")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C9D1D9")),
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                Padding = new Thickness(12),
                BorderThickness = new Thickness(0),
                TextWrapping = TextWrapping.NoWrap,
                Text = "Uruchamianie diagnostyki...\n\nTestowanie połączeń z bazami danych:\n- LibraNet (192.168.0.109)\n- Handel (192.168.0.112)\n- TransportPL (192.168.0.109)\n\nProszę czekać..."
            };
            Grid.SetRow(logBox, 1);
            grid.Children.Add(logBox);

            // Stopka z przyciskami
            var footer = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252A31")),
                Padding = new Thickness(16, 10, 16, 10)
            };
            var footerPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var btnCopy = new Button
            {
                Content = "Kopiuj do schowka",
                Padding = new Thickness(16, 8, 16, 8),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB")),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnCopy.Click += (s2, e2) =>
            {
                try { Clipboard.SetText(logBox.Text); MessageBox.Show("Skopiowano do schowka!", "Info"); }
                catch { }
            };
            footerPanel.Children.Add(btnCopy);

            var btnSave = new Button
            {
                Content = "Zapisz do pliku",
                Padding = new Thickness(16, 8, 16, 8),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnSave.Click += (s2, e2) =>
            {
                try
                {
                    var path = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        $"Diagnostyka_Dyrektora_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    System.IO.File.WriteAllText(path, logBox.Text);
                    MessageBox.Show($"Zapisano do:\n{path}", "Zapisano");
                }
                catch (Exception ex2) { MessageBox.Show($"Błąd zapisu: {ex2.Message}", "Błąd"); }
            };
            footerPanel.Children.Add(btnSave);

            var btnClose = new Button
            {
                Content = "Zamknij",
                Padding = new Thickness(16, 8, 16, 8),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnClose.Click += (s2, e2) => diagWindow.Close();
            footerPanel.Children.Add(btnClose);

            footer.Child = footerPanel;
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            diagWindow.Content = grid;
            diagWindow.Show();

            // Uruchom diagnostykę asynchronicznie
            try
            {
                var result = await _service.RunDiagnosticsAsync(_cts.Token);
                logBox.Text = result;
                ((TextBlock)headerPanel.Children[1]).Text = "Diagnostyka zakończona. Sprawdź wyniki poniżej.";
                ((TextBlock)headerPanel.Children[1]).Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
            }
            catch (Exception ex)
            {
                logBox.Text = $"KRYTYCZNY BŁĄD DIAGNOSTYKI:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
                ((TextBlock)headerPanel.Children[1]).Text = "Diagnostyka zakończona z błędem krytycznym!";
            }
        }
    }
}
