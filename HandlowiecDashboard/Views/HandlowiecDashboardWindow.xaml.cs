using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;
using LiveCharts;
using LiveCharts.Wpf;
using Kalendarz1.HandlowiecDashboard.Models;
using Color = System.Windows.Media.Color;

namespace Kalendarz1.HandlowiecDashboard.Views
{
    // Extension methods
    public static class EnumerableExtensions
    {
        public static TResult MaxOrDefault<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector, TResult defaultValue = default)
        {
            if (source == null || !source.Any()) return defaultValue;
            return source.Max(selector);
        }
    }

    // Klasa pomocnicza do ComboBox - rozwiazuje problem "Value = X, Text"
    public class ComboItem
    {
        public int Value { get; set; }
        public string Text { get; set; }
        public override string ToString() => Text;
    }

    public partial class HandlowiecDashboardWindow : Window
    {
        private readonly string _connectionStringHandel = Configuration.DatabaseConfig.HandelConnectionString;
        private readonly CultureInfo _kulturaPL = new CultureInfo("pl-PL");
        private bool _isInitialized = false;
        private bool _syncowanieDaty = false;

        #region Performance Optimization Fields

        /// <summary>
        /// Cache dla danych dashboardu - przechowuje dane przez 5 minut
        /// </summary>
        private readonly DashboardCache _cache = new DashboardCache { DefaultExpiry = TimeSpan.FromMinutes(5) };

        /// <summary>
        /// Token anulowania dla bieżącej operacji ładowania
        /// </summary>
        private CancellationTokenSource _loadingCts;

        /// <summary>
        /// Token anulowania dla debounce'a na ComboBoxach
        /// </summary>
        private CancellationTokenSource _debounceTokenSource;

        /// <summary>
        /// Czas opóźnienia debounce'a (300ms)
        /// </summary>
        private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(300);

        /// <summary>
        /// Zestaw załadowanych zakładek - dla lazy loading
        /// </summary>
        private readonly HashSet<int> _loadedTabs = new HashSet<int>();

        #endregion

        private static readonly string[] _nazwyMiesiecy = {
            "", "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze",
            "Lip", "Sie", "Wrz", "Paz", "Lis", "Gru"
        };

        private readonly Color[] _kolory = {
            (Color)ColorConverter.ConvertFromString("#FF6B6B"),
            (Color)ColorConverter.ConvertFromString("#4ECDC4"),
            (Color)ColorConverter.ConvertFromString("#FFE66D"),
            (Color)ColorConverter.ConvertFromString("#95E1D3"),
            (Color)ColorConverter.ConvertFromString("#F38181"),
            (Color)ColorConverter.ConvertFromString("#AA96DA"),
            (Color)ColorConverter.ConvertFromString("#FCBAD3"),
            (Color)ColorConverter.ConvertFromString("#A8D8EA"),
            (Color)ColorConverter.ConvertFromString("#F4A261"),
            (Color)ColorConverter.ConvertFromString("#2EC4B6")
        };

        // Cache avatarów handlowców (nazwa -> BitmapSource)
        private readonly Dictionary<string, BitmapSource> _handlowiecAvatarCache = new Dictionary<string, BitmapSource>();
        private Dictionary<string, string> _handlowiecMapowanie;
        private List<(string Handlowiec, double LastY, int ColorIdx)> _udzialChartData;
        private List<(string Handlowiec, double Wartosc)> _sprzedazChartData;
        private List<(string Handlowiec, double Wartosc)> _top15ChartData;
        private List<(string Handlowiec, double Wartosc)> _cenyChartData;

        // Formatery dla osi
        public Func<double, string> ZlFormatter { get; set; }
        public Func<double, string> KgFormatter { get; set; }
        public Func<double, string> PercentFormatter { get; set; }

        public HandlowiecDashboardWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            // Formatery z separatorem tysiecy
            ZlFormatter = val => $"{val:N0} zl";
            KgFormatter = val => $"{val:N0} kg";
            PercentFormatter = val => $"{val:F1}%";

            DataContext = this;
            Loaded += Window_Loaded;
            Closed += Window_Closed;
        }

        #region Performance Optimization Methods

        /// <summary>
        /// Debounce - czeka 300ms po ostatniej zmianie przed wykonaniem akcji
        /// </summary>
        private async Task DebounceAsync(Func<Task> action)
        {
            _debounceTokenSource?.Cancel();
            _debounceTokenSource = new CancellationTokenSource();

            try
            {
                await Task.Delay(_debounceDelay, _debounceTokenSource.Token);
                await action();
            }
            catch (TaskCanceledException)
            {
                // Użytkownik zmienił wartość ponownie - ignoruj
            }
        }

        /// <summary>
        /// Generuje klucz cache dla danej zakładki
        /// </summary>
        private string GetCacheKeyForTab(int tabIndex)
        {
            var rok = DateTime.Now.Year;
            var miesiac = DateTime.Now.Month;

            return tabIndex switch
            {
                0 => $"SprzedazMiesieczna_{rok}_{miesiac}",
                1 => $"Top10_{rok}_{miesiac}",
                2 => $"UdzialHandlowcow_{rok}_{miesiac}",
                3 => $"AnalizaCen_{rok}_{miesiac}",
                4 => $"SwiezeMrozone_{rok}_{miesiac}",
                5 => $"Porownanie_{rok}",
                6 => $"Trend_{rok}_{miesiac}",
                7 => $"Opakowania_{rok}_{miesiac}",
                8 => $"Platnosci_{rok}_{miesiac}",
                _ => $"Unknown_{tabIndex}"
            };
        }

        /// <summary>
        /// Ładuje dane pozostałych zakładek w tle (niski priorytet)
        /// </summary>
        private async Task PreloadDataInBackgroundAsync()
        {
            await Task.Delay(500); // Poczekaj aż UI się ustabilizuje

            try
            {
                await Task.Run(async () =>
                {
                    // Preload z niskim priorytetem - nie blokuj UI
                    System.Diagnostics.Debug.WriteLine("Background preload started...");

                    // Nic nie robimy tutaj - dane będą ładowane lazy przy pierwszym otwarciu zakładki
                    // Ten hook może być użyty w przyszłości do preloadowania krytycznych danych

                    await Task.Delay(100); // Placeholder
                    System.Diagnostics.Debug.WriteLine("Background preload completed.");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Preload error: {ex.Message}");
                // Nie pokazuj błędu - to tylko preload
            }
        }

        /// <summary>
        /// Cleanup przy zamykaniu okna - zwalnia zasoby
        /// </summary>
        private void Window_Closed(object sender, EventArgs e)
        {
            _loadingCts?.Cancel();
            _loadingCts?.Dispose();
            _debounceTokenSource?.Cancel();
            _debounceTokenSource?.Dispose();
            _cache?.Invalidate();
            _loadedTabs?.Clear();
        }

        #endregion

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                loadingOverlay.Visibility = Visibility.Visible;

                // Ustaw formatery osi
                axisYSprzedaz.LabelFormatter = ZlFormatter;
                axisXTop10.LabelFormatter = ZlFormatter;
                axisYUdzial.LabelFormatter = PercentFormatter;
                axisYCenyKg.LabelFormatter = KgFormatter;
                axisYSM.LabelFormatter = KgFormatter;
                axisYPorown.LabelFormatter = ZlFormatter;
                axisYTrend.LabelFormatter = ZlFormatter;

                WypelnijLataIMiesiace();
                _isInitialized = true;

                // 1. Najpierw załaduj pierwszą zakładkę (widoczną)
                await OdswiezSprzedazMiesiecznaAsync();
                _loadedTabs.Add(0);
                loadingOverlay.Visibility = Visibility.Collapsed;

                // 2. W tle załaduj pozostałe dane do cache (opcjonalnie)
                _ = PreloadDataInBackgroundAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad inicjalizacji: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void WypelnijLataIMiesiace()
        {
            var lata = Enumerable.Range(2020, DateTime.Now.Year - 2020 + 1).Reverse().ToList();
            var miesiace = Enumerable.Range(1, 12).Select(m => new ComboItem { Value = m, Text = _nazwyMiesiecy[m] }).ToList();

            // Sprzedaz miesieczna
            cmbRokSprzedaz.ItemsSource = lata;
            cmbRokSprzedaz.SelectedItem = DateTime.Now.Year;
            cmbMiesiacSprzedaz.ItemsSource = miesiace;
            cmbMiesiacSprzedaz.DisplayMemberPath = "Text";
            cmbMiesiacSprzedaz.SelectedValuePath = "Value";
            cmbMiesiacSprzedaz.SelectedValue = DateTime.Now.Month;

            // Top 10
            cmbRokTop10.ItemsSource = lata;
            cmbRokTop10.SelectedItem = DateTime.Now.Year;
            cmbMiesiacTop10.ItemsSource = miesiace;
            cmbMiesiacTop10.DisplayMemberPath = "Text";
            cmbMiesiacTop10.SelectedValuePath = "Value";
            cmbMiesiacTop10.SelectedValue = DateTime.Now.Month;
            WypelnijTowary(cmbTowarTop10);

            // Udzial handlowcow - zawsze 5 miesiecy do tylu
            cmbRokUdzialOd.ItemsSource = lata;
            cmbMiesiacUdzialOd.ItemsSource = miesiace;
            cmbMiesiacUdzialOd.DisplayMemberPath = "Text";
            cmbMiesiacUdzialOd.SelectedValuePath = "Value";
            cmbRokUdzialDo.ItemsSource = lata;
            cmbMiesiacUdzialDo.ItemsSource = miesiace;
            cmbMiesiacUdzialDo.DisplayMemberPath = "Text";
            cmbMiesiacUdzialDo.SelectedValuePath = "Value";
            UstawUdzialDaty5MiesiecyWstecz();
            // Zablokuj edycje dat - zawsze auto 5 miesiecy
            cmbRokUdzialOd.IsEnabled = false;
            cmbMiesiacUdzialOd.IsEnabled = false;
            cmbRokUdzialDo.IsEnabled = false;
            cmbMiesiacUdzialDo.IsEnabled = false;

            // Analiza cen
            cmbRokCeny.ItemsSource = lata;
            cmbRokCeny.SelectedItem = DateTime.Now.Year;
            cmbMiesiacCeny.ItemsSource = miesiace;
            cmbMiesiacCeny.DisplayMemberPath = "Text";
            cmbMiesiacCeny.SelectedValuePath = "Value";
            cmbMiesiacCeny.SelectedValue = DateTime.Now.Month;
            WypelnijTowary(cmbTowarCeny);

            // Swieze vs Mrozone
            cmbRokSM.ItemsSource = lata;
            cmbRokSM.SelectedItem = DateTime.Now.Year;
            cmbMiesiacSM.ItemsSource = miesiace;
            cmbMiesiacSM.DisplayMemberPath = "Text";
            cmbMiesiacSM.SelectedValuePath = "Value";
            cmbMiesiacSM.SelectedValue = DateTime.Now.Month;

            // Porownanie okresow
            cmbRokPorown1.ItemsSource = lata;
            cmbRokPorown1.SelectedItem = DateTime.Now.Year - 1;
            cmbRokPorown2.ItemsSource = lata;
            cmbRokPorown2.SelectedItem = DateTime.Now.Year;

            // Trend sprzedazy
            cmbOkres.ItemsSource = new[] { 3, 6, 9, 12, 18, 24 };
            cmbOkres.SelectedItem = 12;

            // Opakowania i Platnosci - wczytaj liste handlowcow
            WypelnijHandlowcow();
        }

        private void WypelnijHandlowcow()
        {
            var handlowcy = new List<ComboItem> { new ComboItem { Value = 0, Text = "Wszyscy handlowcy" } };
            try
            {
                using var cn = new SqlConnection(_connectionStringHandel);
                cn.Open();
                var sql = @"SELECT DISTINCT WYM.CDim_Handlowiec_Val AS Handlowiec
                           FROM [HANDEL].[SSCommon].[ContractorClassification] WYM
                           WHERE WYM.CDim_Handlowiec_Val IS NOT NULL
                             AND WYM.CDim_Handlowiec_Val NOT IN ('Ogolne', 'Ogólne')
                           ORDER BY Handlowiec";
                using var cmd = new SqlCommand(sql, cn);
                using var reader = cmd.ExecuteReader();
                int idx = 1;
                while (reader.Read())
                {
                    var handlowiec = reader.GetString(0);
                    handlowcy.Add(new ComboItem { Value = idx++, Text = handlowiec });
                    // Inicjalizuj globalne kolory dla handlowcow
                    if (!_handlowiecKolory.ContainsKey(handlowiec))
                    {
                        _handlowiecKolory[handlowiec] = _kolory[(_handlowiecKolory.Count) % _kolory.Length];
                    }
                }
                // Dodaj kolor dla "Nieprzypisany"
                if (!_handlowiecKolory.ContainsKey("Nieprzypisany"))
                {
                    _handlowiecKolory["Nieprzypisany"] = _kolory[_handlowiecKolory.Count % _kolory.Length];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad pobierania handlowcow:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            cmbHandlowiecOpak.ItemsSource = handlowcy;
            cmbHandlowiecOpak.DisplayMemberPath = "Text";
            cmbHandlowiecOpak.SelectedValuePath = "Value";
            cmbHandlowiecOpak.SelectedIndex = 0;

            // Inicjalizuj date pickers dla opakowan
            InicjalizujDatyOpak();

            cmbHandlowiecPlat.ItemsSource = handlowcy;
            cmbHandlowiecPlat.DisplayMemberPath = "Text";
            cmbHandlowiecPlat.SelectedValuePath = "Value";
            cmbHandlowiecPlat.SelectedIndex = 0;
        }

        private void WypelnijTowary(ComboBox cmb)
        {
            var towary = new List<ComboItem> { new ComboItem { Value = 0, Text = "Wszystkie towary" } };
            try
            {
                using var cn = new SqlConnection(_connectionStringHandel);
                cn.Open();
                var sql = @"SELECT TW.ID, TW.kod, TW.kod + ' - ' + ISNULL(TW.nazwa, '') as Nazwa
                           FROM [HANDEL].[HM].[TW] TW
                           WHERE TW.katalog IN (67095, 67153)
                           GROUP BY TW.ID, TW.kod, TW.nazwa
                           ORDER BY TW.kod";
                using var cmd = new SqlCommand(sql, cn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    towary.Add(new ComboItem { Value = reader.GetInt32(0), Text = reader.GetString(2) });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad pobierania towarow:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            cmb.ItemsSource = towary;
            cmb.DisplayMemberPath = "Text";
            cmb.SelectedValuePath = "Value";
            cmb.SelectedIndex = 0;
        }

        #region Event Handlers

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            // Przy ręcznym odświeżeniu - unieważnij cache dla aktualnej zakładki
            var cacheKey = GetCacheKeyForTab(tabControl.SelectedIndex);
            _cache.Invalidate(cacheKey);
            await OdswiezAktualnaZakladkeAsync();
        }

        private async void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || e.Source != tabControl) return;

            var tabIndex = tabControl.SelectedIndex;

            // Jeśli zakładka już była załadowana i dane są w cache - użyj ich
            if (_loadedTabs.Contains(tabIndex))
            {
                var cacheKey = GetCacheKeyForTab(tabIndex);
                if (_cache.Contains(cacheKey))
                {
                    // Dane w cache - szybkie wyświetlenie bez ponownego ładowania
                    return;
                }
            }

            await DebounceAsync(async () =>
            {
                await OdswiezAktualnaZakladkeAsync();
                _loadedTabs.Add(tabIndex);
            });
        }

        private async void CmbRokSprzedaz_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncowanieDaty) return;
            SynchronizujDateMiedzyZakladkami(cmbRokSprzedaz, cmbMiesiacSprzedaz);
            await OdswiezJesliGotoweAsync();
        }
        private async void CmbMiesiacSprzedaz_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncowanieDaty) return;
            SynchronizujDateMiedzyZakladkami(cmbRokSprzedaz, cmbMiesiacSprzedaz);
            await OdswiezJesliGotoweAsync();
        }
        private async void CmbTop10_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncowanieDaty) return;
            if (sender == cmbRokTop10 || sender == cmbMiesiacTop10)
                SynchronizujDateMiedzyZakladkami(cmbRokTop10, cmbMiesiacTop10);
            await OdswiezJesliGotoweAsync();
        }
        private async void CmbUdzial_SelectionChanged(object sender, SelectionChangedEventArgs e) => await OdswiezJesliGotoweAsync();
        private async void CmbCeny_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncowanieDaty) return;
            if (sender == cmbRokCeny || sender == cmbMiesiacCeny)
                SynchronizujDateMiedzyZakladkami(cmbRokCeny, cmbMiesiacCeny);
            await OdswiezJesliGotoweAsync();
        }

        /// <summary>
        /// Synchronizuje wybrana date miedzy zakladkami Sprzedaz, Top 15 i Analiza cen
        /// </summary>
        private void SynchronizujDateMiedzyZakladkami(ComboBox zrodloRok, ComboBox zrodloMiesiac)
        {
            if (_syncowanieDaty || !_isInitialized) return;
            _syncowanieDaty = true;
            try
            {
                var rok = zrodloRok.SelectedItem;
                var miesiac = zrodloMiesiac.SelectedValue;
                if (rok == null || miesiac == null) return;

                // Synchronizuj do wszystkich 3 zakladek (pomijaj zrodlo)
                if (zrodloRok != cmbRokSprzedaz) cmbRokSprzedaz.SelectedItem = rok;
                if (zrodloMiesiac != cmbMiesiacSprzedaz) cmbMiesiacSprzedaz.SelectedValue = miesiac;

                if (zrodloRok != cmbRokTop10) cmbRokTop10.SelectedItem = rok;
                if (zrodloMiesiac != cmbMiesiacTop10) cmbMiesiacTop10.SelectedValue = miesiac;

                if (zrodloRok != cmbRokCeny) cmbRokCeny.SelectedItem = rok;
                if (zrodloMiesiac != cmbMiesiacCeny) cmbMiesiacCeny.SelectedValue = miesiac;

                // Wyczysc cache zakladek ktore maja nieaktualne dane
                _loadedTabs.Remove(0); // Sprzedaz
                _loadedTabs.Remove(1); // Top 15
                _loadedTabs.Remove(3); // Analiza cen
            }
            finally
            {
                _syncowanieDaty = false;
            }
        }
        private async void CmbSM_SelectionChanged(object sender, SelectionChangedEventArgs e) => await OdswiezJesliGotoweAsync();
        private async void CmbPorown_SelectionChanged(object sender, SelectionChangedEventArgs e) => await OdswiezJesliGotoweAsync();
        private async void CmbTrend_SelectionChanged(object sender, SelectionChangedEventArgs e) => await OdswiezJesliGotoweAsync();
        private async void CmbOpakowania_SelectionChanged(object sender, SelectionChangedEventArgs e) => await OdswiezJesliGotoweAsync();
        private async void CmbPlatnosci_SelectionChanged(object sender, SelectionChangedEventArgs e) => await OdswiezJesliGotoweAsync();

        /// <summary>
        /// Obsluga przelacznika E2/H1/RAZEM - tylko odswiezenie UI bez pobierania danych
        /// </summary>
        private void RbOpakTryb_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            if (rbOpakE2?.IsChecked == true) _opakTryb = "E2";
            else if (rbOpakH1?.IsChecked == true) _opakTryb = "H1";
            else _opakTryb = "RAZEM";

            // Tylko odswiezamy UI - dane juz mamy
            OdswiezOpakowaniaUI();
        }

        /// <summary>
        /// Odświeża zakładkę z debounce'em - zapobiega wielokrotnym zapytaniom przy szybkich zmianach
        /// </summary>
        private async Task OdswiezJesliGotoweAsync()
        {
            if (!_isInitialized) return;
            await DebounceAsync(async () => await OdswiezAktualnaZakladkeAsync());
        }

        /// <summary>
        /// Odświeża aktualną zakładkę z obsługą anulowania i cache'owania
        /// </summary>
        private async Task OdswiezAktualnaZakladkeAsync()
        {
            if (!_isInitialized) return;

            // Anuluj poprzednie ładowanie jeśli trwa
            _loadingCts?.Cancel();
            _loadingCts = new CancellationTokenSource();
            var ct = _loadingCts.Token;

            try
            {
                loadingOverlay.Visibility = Visibility.Visible;

                switch (tabControl.SelectedIndex)
                {
                    case 0: await OdswiezSprzedazMiesiecznaAsync(); break;
                    case 1: await OdswiezTop10Async(); break;
                    case 2: await OdswiezUdzialHandlowcowAsync(); break;
                    case 3: await OdswiezAnalizeCenAsync(); break;
                    case 4: await OdswiezSwiezeMrozoneAsync(); break;
                    case 5: await OdswiezPorownanieAsync(); break;
                    case 6: await OdswiezTrendAsync(); break;
                    case 7: await OdswiezOpakowaniaAsync(); break;
                    case 8: await OdswiezPlatnosciAsync(); break;
                }
            }
            catch (OperationCanceledException)
            {
                // Anulowano - ignoruj
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                if (!ct.IsCancellationRequested)
                {
                    loadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        #endregion

        #region Sprzedaz Miesieczna

        private async System.Threading.Tasks.Task OdswiezSprzedazMiesiecznaAsync()
        {
            if (cmbRokSprzedaz.SelectedItem == null || cmbMiesiacSprzedaz.SelectedValue == null) return;

            int rok = (int)cmbRokSprzedaz.SelectedItem;
            int miesiac = (int)cmbMiesiacSprzedaz.SelectedValue;

            var series = new SeriesCollection();
            var labels = new List<string>();
            decimal suma = 0;
            var daneHandlowcow = new Dictionary<string, List<(string Klient, decimal Wartosc)>>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
                           C.shortcut AS Kontrahent, SUM(DP.wartNetto) AS WartoscSprzedazy
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                      AND WYM.CDim_Handlowiec_Val IS NOT NULL
                      AND WYM.CDim_Handlowiec_Val NOT IN ('Ogolne', 'Ogólne')
                    GROUP BY ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany'), C.shortcut
                    ORDER BY Handlowiec, WartoscSprzedazy DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var handlowiec = reader.GetString(0);
                    var klient = reader.GetString(1);
                    var wartosc = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));

                    if (!daneHandlowcow.ContainsKey(handlowiec))
                        daneHandlowcow[handlowiec] = new List<(string, decimal)>();

                    daneHandlowcow[handlowiec].Add((klient, wartosc));
                    suma += wartosc;
                }

                // Zaladuj avatary handlowcow
                await EnsureHandlowiecMappingLoadedAsync();
                _sprzedazChartData = new List<(string Handlowiec, double Wartosc)>();

                foreach (var h in daneHandlowcow.OrderByDescending(x => x.Value.Sum(v => v.Wartosc)))
                {
                    labels.Add(h.Key);
                    var wartosc = h.Value.Sum(v => v.Wartosc);
                    _sprzedazChartData.Add((h.Key, (double)wartosc));
                    EnsureAvatarCached(h.Key);
                }

                // StackedColumnSeries per handlowiec - kazdy z wlasnym kolorem
                // Na kazdej pozycji X tylko jeden handlowiec ma wartosc > 0, reszta = 0
                int hIdx = 0;
                var orderedHandlowcy = daneHandlowcow.OrderByDescending(x => x.Value.Sum(v => v.Wartosc)).ToList();
                foreach (var h in orderedHandlowcy)
                {
                    var kolor = GetHandlowiecColor(h.Key);
                    var values = new ChartValues<double>();
                    for (int i = 0; i < orderedHandlowcy.Count; i++)
                        values.Add(i == hIdx ? (double)h.Value.Sum(v => v.Wartosc) : 0);

                    series.Add(new StackedColumnSeries
                    {
                        Title = h.Key,
                        Values = values,
                        Fill = new SolidColorBrush(kolor),
                        DataLabels = false,
                        MaxColumnWidth = 80
                    });
                    hIdx++;
                }

                treeSprzedaz.Items.Clear();
                foreach (var h in daneHandlowcow.OrderByDescending(x => x.Value.Sum(v => v.Wartosc)))
                {
                    var sumaHandlowca = h.Value.Sum(v => v.Wartosc);
                    var procent = suma > 0 ? (sumaHandlowca / suma) * 100 : 0;

                    var item = new TreeViewItem
                    {
                        Header = $"{h.Key}: {sumaHandlowca:N0} zl ({procent:F1}%)",
                        Foreground = new SolidColorBrush(GetHandlowiecColor(h.Key)),
                        FontWeight = FontWeights.Bold
                    };

                    foreach (var (klient, wartosc) in h.Value)
                    {
                        // Oblicz % klienta w stosunku do handlowca
                        var procentKlienta = sumaHandlowca > 0 ? (wartosc / sumaHandlowca) * 100 : 0;
                        item.Items.Add(new TreeViewItem
                        {
                            Header = $"  {klient}: {wartosc:N0} zl ({procentKlienta:F1}%)",
                            Foreground = Brushes.White
                        });
                    }

                    treeSprzedaz.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad sprzedazy miesiecznej:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            chartSprzedaz.Series = series;
            axisXSprzedaz.Labels = labels;
            txtSumaSprzedaz.Text = $"CALKOWITA WARTOSC SPRZEDAZY: {suma:N0} zl";

            // Pozycjonuj avatary i etykiety na slupkach po renderingu (z opoznieniem aby chart sie wyrysował)
            var sprzedazLabels = _sprzedazChartData.Select(d => $"{d.Wartosc:N0} zl").ToList();
            OpoznionePozycjonowanie(() =>
                PozycjonujAvataryNaSlupkach(canvasSprzedazAvatary, chartSprzedaz, _sprzedazChartData, labelTexts: sprzedazLabels));
        }

        #endregion

        #region Top 10 Odbiorcy

        private async System.Threading.Tasks.Task OdswiezTop10Async()
        {
            if (cmbRokTop10.SelectedItem == null || cmbMiesiacTop10.SelectedValue == null) return;

            int rok = (int)cmbRokTop10.SelectedItem;
            int miesiac = (int)cmbMiesiacTop10.SelectedValue;
            int? towarId = cmbTowarTop10.SelectedValue as int?;
            if (towarId == 0) towarId = null;

            var series = new SeriesCollection();
            var labels = new List<string>();
            decimal sumaKg = 0;
            decimal sumaWartosc = 0;

            // Lista danych do odwrocenia (najwyzszy na gorze)
            var daneTop10 = new List<(string Kontrahent, string Handlowiec, decimal Kg, decimal Wartosc)>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT TOP 15 C.shortcut AS Kontrahent,
                           ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
                           SUM(DP.ilosc) AS SumaKg, SUM(DP.wartNetto) AS SumaWartosc
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                      AND (@TowarID IS NULL OR DP.idtw = @TowarID)
                    GROUP BY C.shortcut, ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany')
                    ORDER BY SumaWartosc DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);
                cmd.Parameters.AddWithValue("@TowarID", (object)towarId ?? DBNull.Value);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var kontrahent = reader.GetString(0);
                    var handlowiec = reader.GetString(1);
                    var kg = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
                    var wartosc = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3));

                    daneTop10.Add((kontrahent, handlowiec, kg, wartosc));
                    sumaKg += kg;
                    sumaWartosc += wartosc;
                }

                // Zaladuj avatary
                await EnsureHandlowiecMappingLoadedAsync();

                // Buduj liste etykiet i wartosci (od najnizszej do najwyzszej wartosci)
                int liczbaElementow = daneTop10.Count;
                var wartosci = new ChartValues<double>();
                var handlowcyNaSlupkach = new List<string>();

                for (int i = liczbaElementow - 1; i >= 0; i--)
                {
                    var d = daneTop10[i];
                    var labelTekst = d.Kontrahent.Length > 20 ? d.Kontrahent.Substring(0, 20) + ".." : d.Kontrahent;
                    labels.Add(labelTekst);
                    wartosci.Add((double)d.Wartosc);
                    handlowcyNaSlupkach.Add(d.Handlowiec);
                    EnsureAvatarCached(d.Handlowiec);
                }

                // Zapisz dane do pozycjonowania avatarów - lista od dolu do gory (jak na wykresie)
                _top15ChartData = new List<(string Handlowiec, double Wartosc)>();
                for (int i = 0; i < handlowcyNaSlupkach.Count; i++)
                    _top15ChartData.Add((handlowcyNaSlupkach[i], wartosci[i]));

                // StackedRowSeries per handlowiec - kazdy slupek ma kolor handlowca
                var unikatoweHandlowcy = handlowcyNaSlupkach.Distinct().ToList();
                foreach (var hNazwa in unikatoweHandlowcy)
                {
                    var kolor = GetHandlowiecColor(hNazwa);
                    var values = new ChartValues<double>();
                    for (int i = 0; i < liczbaElementow; i++)
                        values.Add(handlowcyNaSlupkach[i] == hNazwa ? wartosci[i] : 0);

                    series.Add(new StackedRowSeries
                    {
                        Title = hNazwa,
                        Values = values,
                        Fill = new SolidColorBrush(kolor),
                        DataLabels = false,
                        MaxRowHeight = 35,
                        RowPadding = 2
                    });
                }

                // Aktualizuj legende handlowcow z avatarami
                AktualizujLegendeTop15(daneTop10, handlowcyNaSlupkach);

                // Oblicz srednia cene
                decimal sredniaCena = sumaKg > 0 ? sumaWartosc / sumaKg : 0;
                txtTop10Info.Text = $"Suma: {sumaWartosc:N0} zl  |  Suma kg: {sumaKg:N0}  |  Srednia cena: {sredniaCena:F2} zl/kg";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad Top 10:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            chartTop10.Series = series;
            axisYTop10.Labels = labels;

            // Pozycjonuj avatary i etykiety na slupkach po renderingu (z opoznieniem aby chart sie wyrysował)
            var top15Labels = _top15ChartData.Select(d => $"{d.Wartosc:N0} zl | {d.Handlowiec}").ToList();
            OpoznionePozycjonowanie(() =>
                PozycjonujAvataryNaSlupkach(canvasTop10Avatary, chartTop10, _top15ChartData, isHorizontal: true, labelTexts: top15Labels));
        }

        private void AktualizujLegendeTop15(List<(string Kontrahent, string Handlowiec, decimal Kg, decimal Wartosc)> dane, List<string> handlowcyNaSlupkach)
        {
            var panelLegenda = FindName("panelLegendaTop15") as StackPanel;
            if (panelLegenda == null) return;

            panelLegenda.Children.Clear();

            // Podsumowanie: ile klientow ma kazdy handlowiec w Top 15
            var podsumowanie = dane
                .GroupBy(d => d.Handlowiec)
                .Select(g => new { Handlowiec = g.Key, Liczba = g.Count(), Suma = g.Sum(x => x.Wartosc) })
                .OrderByDescending(x => x.Suma)
                .ToList();

            foreach (var h in podsumowanie)
            {
                var kolor = GetHandlowiecColor(h.Handlowiec);

                var element = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 15, 0) };

                // Avatar zamiast prostokatu
                if (_handlowiecAvatarCache.TryGetValue(h.Handlowiec, out var avatarBmp))
                {
                    var avatarEl = CreateAvatarElement(h.Handlowiec, 22, kolor);
                    avatarEl.Margin = new Thickness(0, 0, 5, 0);
                    avatarEl.VerticalAlignment = VerticalAlignment.Center;
                    element.Children.Add(avatarEl);
                }
                else
                {
                    element.Children.Add(new System.Windows.Shapes.Rectangle
                    {
                        Width = 12, Height = 12,
                        Fill = new SolidColorBrush(kolor),
                        RadiusX = 6, RadiusY = 6,
                        Margin = new Thickness(0, 0, 5, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }

                element.Children.Add(new TextBlock
                {
                    Text = $"{h.Handlowiec}: {h.Liczba} klient. ({h.Suma:N0} zl)",
                    Foreground = Brushes.White,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center
                });

                panelLegenda.Children.Add(element);
            }
        }

        #endregion

        #region Udzial Handlowcow

        private void UstawUdzialDaty5MiesiecyWstecz()
        {
            var teraz = DateTime.Now;
            var piecMiesiecyTemu = teraz.AddMonths(-5);
            cmbRokUdzialOd.SelectedItem = piecMiesiecyTemu.Year;
            cmbMiesiacUdzialOd.SelectedValue = piecMiesiecyTemu.Month;
            cmbRokUdzialDo.SelectedItem = teraz.Year;
            cmbMiesiacUdzialDo.SelectedValue = teraz.Month;
        }

        private async System.Threading.Tasks.Task OdswiezUdzialHandlowcowAsync()
        {
            // Zawsze odswiez daty na 5 miesiecy wstecz
            UstawUdzialDaty5MiesiecyWstecz();
            if (cmbRokUdzialOd.SelectedItem == null || cmbMiesiacUdzialOd.SelectedValue == null ||
                cmbRokUdzialDo.SelectedItem == null || cmbMiesiacUdzialDo.SelectedValue == null) return;

            int rokOd = (int)cmbRokUdzialOd.SelectedItem;
            int miesiacOd = (int)cmbMiesiacUdzialOd.SelectedValue;
            int rokDo = (int)cmbRokUdzialDo.SelectedItem;
            int miesiacDo = (int)cmbMiesiacUdzialDo.SelectedValue;

            var series = new SeriesCollection();
            var labels = new List<string>();
            var daneHandlowcow = new Dictionary<string, Dictionary<string, decimal>>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT WYM.CDim_Handlowiec_Val AS Handlowiec, YEAR(DK.data) AS Rok, MONTH(DK.data) AS Miesiac,
                           SUM(DP.wartNetto) AS WartoscSprzedazy
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE (YEAR(DK.data) * 100 + MONTH(DK.data)) >= @OdData
                      AND (YEAR(DK.data) * 100 + MONTH(DK.data)) <= @DoData
                      AND WYM.CDim_Handlowiec_Val IS NOT NULL
                      AND WYM.CDim_Handlowiec_Val NOT IN ('Ogolne', 'Ogólne')
                    GROUP BY WYM.CDim_Handlowiec_Val, YEAR(DK.data), MONTH(DK.data)
                    ORDER BY Rok, Miesiac, Handlowiec";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@OdData", rokOd * 100 + miesiacOd);
                cmd.Parameters.AddWithValue("@DoData", rokDo * 100 + miesiacDo);

                var sumyMiesieczne = new Dictionary<string, decimal>();

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var handlowiec = reader.GetString(0);
                    var rok = reader.GetInt32(1);
                    var miesiac = reader.GetInt32(2);
                    var wartosc = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3));

                    var klucz = $"{_nazwyMiesiecy[miesiac]} {rok}";

                    if (!labels.Contains(klucz))
                        labels.Add(klucz);

                    if (!daneHandlowcow.ContainsKey(handlowiec))
                        daneHandlowcow[handlowiec] = new Dictionary<string, decimal>();

                    daneHandlowcow[handlowiec][klucz] = wartosc;

                    if (!sumyMiesieczne.ContainsKey(klucz))
                        sumyMiesieczne[klucz] = 0;
                    sumyMiesieczne[klucz] += wartosc;
                }

                // Znajdz maksymalny procent dla osi Y
                double maxProcent = 0;

                int idx = 0;
                foreach (var h in daneHandlowcow.OrderByDescending(x => x.Value.Values.Sum()))
                {
                    var wartosci = new ChartValues<double>();
                    foreach (var klucz in labels)
                    {
                        var wartosc = h.Value.ContainsKey(klucz) ? h.Value[klucz] : 0m;
                        var suma = sumyMiesieczne.ContainsKey(klucz) ? sumyMiesieczne[klucz] : 1m;
                        var procent = suma > 0 ? (double)(wartosc / suma * 100) : 0;
                        wartosci.Add(procent);
                        if (procent > maxProcent) maxProcent = procent;
                    }

                    // Dodaj nazwe handlowca na koncu ostatniego punktu
                    var lastValue = wartosci.LastOrDefault();

                    series.Add(new LineSeries
                    {
                        Title = h.Key,
                        Values = wartosci,
                        Stroke = new SolidColorBrush(_kolory[idx % _kolory.Length]),
                        Fill = Brushes.Transparent,
                        PointGeometry = DefaultGeometries.Circle,
                        PointGeometrySize = 8,
                        LineSmoothness = 0.3,
                        DataLabels = false,
                        Foreground = Brushes.White
                    });
                    idx++;
                }

                // Dodaj etykiety na koncu linii - dodajemy dodatkowa kolumne z nazwami
                if (labels.Count > 0)
                {
                    // Dodajemy pusta etykiete na koncu dla miejsca na nazwy
                    labels.Add("");

                    // Rozszerz wartosci o ostatni punkt (taki sam jak poprzedni) + tekst
                    foreach (LineSeries ls in series)
                    {
                        if (ls.Values.Count > 0)
                        {
                            var lastVal = (double)ls.Values[ls.Values.Count - 1];
                            ls.Values.Add(lastVal);
                        }
                    }
                }

                // Ustaw os Y do maksymalnego punktu + 10%
                axisYUdzial.MaxValue = maxProcent * 1.1;

                // Dodaj etykiety procentowe na wszystkich punktach + nazwe handlowca na koncu
                idx = 0;
                foreach (LineSeries ls in series)
                {
                    var handlowiecNazwa = ls.Title;
                    var color = _kolory[idx % _kolory.Length];
                    var valuesCount = ls.Values.Count;

                    // Ustaw etykiete na kazdym punkcie: % na wszystkich, + nazwe na ostatnim
                    ls.LabelPoint = p =>
                    {
                        if (p.Key == valuesCount - 1)
                            return $"{p.Y:F1}% {handlowiecNazwa}";
                        return $"{p.Y:F1}%";
                    };
                    ls.DataLabels = true;
                    idx++;
                }

                // Pobierz mapowanie i zaladuj avatary
                await EnsureHandlowiecMappingLoadedAsync();
                foreach (var h in daneHandlowcow.Keys)
                    EnsureAvatarCached(h);

                // Zapamietaj dane do pozycjonowania avatarów na wykresie
                _udzialChartData = new List<(string Handlowiec, double LastY, int ColorIdx)>();
                idx = 0;
                foreach (var h in daneHandlowcow.OrderByDescending(x => x.Value.Values.Sum()))
                {
                    if (series.Count > idx && series[idx].Values.Count > 0)
                    {
                        var lastY = (double)series[idx].Values[series[idx].Values.Count - 1];
                        _udzialChartData.Add((h.Key, lastY, idx));
                    }
                    idx++;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad udzialu handlowcow:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            chartUdzial.Series = series;
            axisXUdzial.Labels = labels;

            // Buduj legende z avatarami (uzywa cache)
            BudujLegendeUdzialu(daneHandlowcow);

            // Pozycjonuj avatary na wykresie po renderingu (z opoznieniem aby chart sie wyrysował)
            OpoznionePozycjonowanie(() => PozycjonujAvataryNaWykresie());
        }

        private void PozycjonujAvataryNaWykresie()
        {
            try
            {
                canvasUdzialAvatary.Children.Clear();
                if (_udzialChartData == null || !_udzialChartData.Any()) return;

                var model = chartUdzial.Model;
                if (model == null || model.DrawMargin == null) return;

                var drawMargin = model.DrawMargin;
                var maxY = axisYUdzial.MaxValue;
                var minY = axisYUdzial.MinValue;
                if (maxY <= minY) return;

                // Pozycja X - prawa strona wykresu (koniec linii)
                double plotRight = drawMargin.Left + drawMargin.Width;
                double plotTop = drawMargin.Top;
                double plotHeight = drawMargin.Height;
                double avatarSize = 32;

                foreach (var data in _udzialChartData)
                {
                    if (!_handlowiecAvatarCache.TryGetValue(data.Handlowiec, out var cachedAvatar)) continue;

                    var yRatio = 1.0 - (data.LastY - minY) / (maxY - minY);
                    var yPixel = plotTop + yRatio * plotHeight;

                    var avatarEl = CreateAvatarElement(data.Handlowiec, avatarSize, _kolory[data.ColorIdx % _kolory.Length]);
                    Canvas.SetLeft(avatarEl, plotRight + 6);
                    Canvas.SetTop(avatarEl, yPixel - avatarSize / 2);
                    canvasUdzialAvatary.Children.Add(avatarEl);
                }
            }
            catch { }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private BitmapSource ConvertToBitmapSource(System.Drawing.Image image)
        {
            if (image == null) return null;
            using (var bitmap = new System.Drawing.Bitmap(image))
            {
                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
        }

        private async Task EnsureHandlowiecMappingLoadedAsync()
        {
            if (_handlowiecMapowanie != null) return;
            _handlowiecMapowanie = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await using var cnLib = new SqlConnection(Configuration.DatabaseConfig.LibraNetConnectionString);
                await cnLib.OpenAsync();
                await using var cmdMap = new SqlCommand("SELECT HandlowiecName, UserID FROM UserHandlowcy", cnLib);
                await using var readerMap = await cmdMap.ExecuteReaderAsync();
                while (await readerMap.ReadAsync())
                    _handlowiecMapowanie[readerMap.GetString(0)] = readerMap.GetString(1);
            }
            catch { }
        }

        private void EnsureAvatarCached(string handlowiec, int size = 32)
        {
            if (_handlowiecAvatarCache.ContainsKey(handlowiec)) return;
            if (_handlowiecMapowanie == null) return;

            BitmapSource avatarBmp = null;
            if (_handlowiecMapowanie.TryGetValue(handlowiec, out var uid))
            {
                try
                {
                    if (UserAvatarManager.HasAvatar(uid))
                        using (var av = UserAvatarManager.GetAvatarRounded(uid, size))
                            if (av != null) avatarBmp = ConvertToBitmapSource(av);
                    if (avatarBmp == null)
                        using (var defAv = UserAvatarManager.GenerateDefaultAvatar(handlowiec, uid, size))
                            avatarBmp = ConvertToBitmapSource(defAv);
                }
                catch { }
            }
            if (avatarBmp == null)
            {
                try
                {
                    using (var defAv = UserAvatarManager.GenerateDefaultAvatar(handlowiec, handlowiec, size))
                        avatarBmp = ConvertToBitmapSource(defAv);
                }
                catch { }
            }
            if (avatarBmp != null)
            {
                avatarBmp.Freeze();
                _handlowiecAvatarCache[handlowiec] = avatarBmp;
            }
        }

        private Border CreateAvatarElement(string handlowiec, double size, Color borderColor)
        {
            var imgSize = size - 4;
            var avatarImg = new System.Windows.Controls.Image
            {
                Width = imgSize, Height = imgSize,
                Stretch = Stretch.UniformToFill
            };
            avatarImg.Clip = new EllipseGeometry(new System.Windows.Point(imgSize / 2, imgSize / 2), imgSize / 2, imgSize / 2);

            if (_handlowiecAvatarCache.TryGetValue(handlowiec, out var cachedAvatar))
                avatarImg.Source = cachedAvatar;

            return new Border
            {
                Width = size, Height = size,
                CornerRadius = new CornerRadius(size / 2),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(2),
                Child = avatarImg,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1D21"))
            };
        }

        private void OpoznionePozycjonowanie(Action action)
        {
            // Daj chartowi czas na renderowanie przed pozycjonowaniem overlayów
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                action();
            };
            timer.Start();
        }

        private TextBlock CreateOutlinedLabel(string text, double fontSize)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Opacity = 1,
                    Direction = 0
                }
            };
        }

        private double GetAxisActualMax(LiveCharts.Wpf.CartesianChart chart, bool isXAxis)
        {
            try
            {
                if (isXAxis && chart.AxisX != null && chart.AxisX.Count > 0)
                {
                    var ax = chart.AxisX[0];
                    if (!double.IsNaN(ax.MaxValue) && ax.MaxValue > 0) return ax.MaxValue;
                }
                else if (!isXAxis && chart.AxisY != null && chart.AxisY.Count > 0)
                {
                    var ax = chart.AxisY[0];
                    if (!double.IsNaN(ax.MaxValue) && ax.MaxValue > 0) return ax.MaxValue;
                }

                // Try reflection on model axis TopLimit
                var model = chart.Model;
                if (model != null)
                {
                    var axisList = isXAxis ? model.AxisX : model.AxisY;
                    if (axisList != null && axisList.Count > 0)
                    {
                        var core = axisList[0];
                        var prop = core.GetType().GetProperty("TopLimit");
                        if (prop != null)
                        {
                            var val = prop.GetValue(core);
                            if (val is double d && !double.IsNaN(d) && d > 0) return d;
                        }
                    }
                }
            }
            catch { }
            return double.NaN;
        }

        private void PozycjonujAvataryNaSlupkach(Canvas canvas, LiveCharts.Wpf.CartesianChart chart, List<(string Handlowiec, double Wartosc)> data, bool isHorizontal = false, List<string> labelTexts = null)
        {
            try
            {
                canvas.Children.Clear();
                if (data == null || !data.Any()) return;

                var model = chart.Model;
                if (model == null || model.DrawMargin == null) return;

                var dm = model.DrawMargin;
                int count = data.Count;
                if (count == 0) return;

                if (isHorizontal)
                {
                    // StackedRowSeries - horizontal bars (Top 15)
                    double avatarSize = 30;
                    double maxVal = data.Max(d => d.Wartosc);
                    if (maxVal <= 0) return;

                    // Try to get actual X axis max from chart, fallback to estimate
                    double axisMax = GetAxisActualMax(chart, isXAxis: true);
                    if (double.IsNaN(axisMax) || axisMax <= 0) axisMax = maxVal * 1.05;

                    // Each bar occupies 1 unit on Y axis, centered at index + 0.5
                    // LiveCharts row series: index 0 = bottom of chart
                    // Pixel Y = dm.Top maps to Y axis max, dm.Top + dm.Height maps to Y axis min (0)
                    double yAxisMax = count; // Y axis goes from 0 to count
                    double pixelsPerUnit = dm.Height / yAxisMax;

                    for (int i = 0; i < count; i++)
                    {
                        var d = data[i];

                        // Bar center Y: index i, center at (i + 0.5)
                        // In pixels: bottom of chart = dm.Top + dm.Height = Y axis 0
                        // top of chart = dm.Top = Y axis max (=count)
                        var barCenterY = i + 0.5;
                        var yPixel = dm.Top + dm.Height - barCenterY * pixelsPerUnit;

                        // X: right end of bar (value mapped to pixels)
                        var xEndPixel = dm.Left + (d.Wartosc / axisMax) * dm.Width;

                        // Render outlined label inside bar (centered vertically and horizontally)
                        var labelText = labelTexts != null && i < labelTexts.Count ? labelTexts[i] : $"{d.Wartosc:N0} zl | {d.Handlowiec}";
                        var label = CreateOutlinedLabel(labelText, 11);
                        label.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                        var labelWidth = label.DesiredSize.Width;
                        var labelHeight = label.DesiredSize.Height;
                        var barPixelWidth = xEndPixel - dm.Left;

                        // Center label in bar if fits, otherwise left-align with small margin
                        double labelX;
                        if (barPixelWidth > labelWidth + avatarSize + 20)
                            labelX = dm.Left + (barPixelWidth - labelWidth) / 2;
                        else
                            labelX = dm.Left + 5;

                        Canvas.SetLeft(label, labelX);
                        Canvas.SetTop(label, yPixel - labelHeight / 2);
                        canvas.Children.Add(label);

                        // Avatar at right end of bar
                        if (_handlowiecAvatarCache.ContainsKey(d.Handlowiec))
                        {
                            var avatarEl = CreateAvatarElement(d.Handlowiec, avatarSize, GetHandlowiecColor(d.Handlowiec));
                            Canvas.SetLeft(avatarEl, xEndPixel + 4);
                            Canvas.SetTop(avatarEl, yPixel - avatarSize / 2);
                            canvas.Children.Add(avatarEl);
                        }
                    }
                }
                else
                {
                    // StackedColumnSeries - vertical bars
                    double avatarSize = 36;
                    double maxVal = data.Max(d => d.Wartosc);
                    if (maxVal <= 0) return;

                    // Try to get actual Y axis max from chart, fallback to estimate
                    double axisMax = GetAxisActualMax(chart, isXAxis: false);
                    if (double.IsNaN(axisMax) || axisMax <= 0) axisMax = maxVal * 1.05;

                    // Each bar occupies 1 unit on X axis, centered at index + 0.5
                    double xAxisMax = count;
                    double pixelsPerUnitX = dm.Width / xAxisMax;

                    for (int i = 0; i < count; i++)
                    {
                        var d = data[i];

                        // X: center of bar at index i + 0.5
                        var xPixel = dm.Left + (i + 0.5) * pixelsPerUnitX;

                        // Y: top of bar - map value to pixels
                        // dm.Top + dm.Height = Y axis 0, dm.Top = Y axis max
                        var yPixel = dm.Top + dm.Height - (d.Wartosc / axisMax) * dm.Height;

                        // Avatar just above bar top
                        if (_handlowiecAvatarCache.ContainsKey(d.Handlowiec))
                        {
                            var avatarEl = CreateAvatarElement(d.Handlowiec, avatarSize, GetHandlowiecColor(d.Handlowiec));
                            Canvas.SetLeft(avatarEl, xPixel - avatarSize / 2);
                            Canvas.SetTop(avatarEl, yPixel - avatarSize - 2);
                            canvas.Children.Add(avatarEl);
                        }

                        // Render outlined label above avatar
                        var labelText = labelTexts != null && i < labelTexts.Count ? labelTexts[i] : $"{d.Wartosc:N0}";
                        var label = CreateOutlinedLabel(labelText, 12);
                        label.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                        Canvas.SetLeft(label, xPixel - label.DesiredSize.Width / 2);
                        Canvas.SetTop(label, yPixel - avatarSize - label.DesiredSize.Height - 4);
                        canvas.Children.Add(label);
                    }
                }
            }
            catch { }
        }

        private void BudujLegendeUdzialu(Dictionary<string, Dictionary<string, decimal>> daneHandlowcow)
        {
            panelUdzialLegenda.Children.Clear();

            int idx = 0;
            foreach (var h in daneHandlowcow.OrderByDescending(x => x.Value.Values.Sum()))
            {
                var handlowiec = h.Key;
                var color = _kolory[idx % _kolory.Length];
                var sumaWartosc = h.Value.Values.Sum();

                var row = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1D21")),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6),
                    Margin = new Thickness(0, 0, 0, 4)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var avatarBorder = new Border
                {
                    Width = 30, Height = 30,
                    CornerRadius = new CornerRadius(15),
                    BorderBrush = new SolidColorBrush(color),
                    BorderThickness = new Thickness(2),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252A31")),
                    ClipToBounds = true
                };

                if (_handlowiecAvatarCache.TryGetValue(handlowiec, out var cachedAvatar))
                {
                    var img = new System.Windows.Controls.Image
                    {
                        Source = cachedAvatar,
                        Width = 26, Height = 26,
                        Stretch = Stretch.UniformToFill
                    };
                    img.Clip = new EllipseGeometry(new System.Windows.Point(13, 13), 13, 13);
                    avatarBorder.Child = img;
                }
                else
                {
                    var initials = string.Join("", handlowiec.Split(' ').Where(s => s.Length > 0).Take(2).Select(s => s[0]));
                    avatarBorder.Child = new TextBlock
                    {
                        Text = initials.ToUpper(),
                        FontSize = 10, FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(color),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }

                Grid.SetColumn(avatarBorder, 0);
                grid.Children.Add(avatarBorder);

                var txtPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
                txtPanel.Children.Add(new TextBlock
                {
                    Text = handlowiec,
                    FontSize = 10, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(color),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                txtPanel.Children.Add(new TextBlock
                {
                    Text = $"{sumaWartosc:N0} zl",
                    FontSize = 9,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B949E"))
                });

                Grid.SetColumn(txtPanel, 1);
                grid.Children.Add(txtPanel);

                row.Child = grid;
                panelUdzialLegenda.Children.Add(row);
                idx++;
            }
        }

        #endregion

        #region Analiza Cen

        private List<string> _analizaCenyLabels = new List<string>();

        private async System.Threading.Tasks.Task OdswiezAnalizeCenAsync()
        {
            if (cmbRokCeny.SelectedItem == null || cmbMiesiacCeny.SelectedValue == null) return;

            int rok = (int)cmbRokCeny.SelectedItem;
            int miesiac = (int)cmbMiesiacCeny.SelectedValue;
            int? towarId = cmbTowarCeny.SelectedValue as int?;
            if (towarId == 0) towarId = null;

            var seriesCeny = new SeriesCollection();
            var seriesKg = new SeriesCollection();
            var labels = new List<string>();
            var daneTabeli = new List<HandlowiecCenyRow>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT WYM.CDim_Handlowiec_Val AS Handlowiec,
                           CASE WHEN SUM(DP.ilosc) > 0 THEN SUM(DP.wartNetto) / SUM(DP.ilosc) ELSE 0 END AS SredniaCena,
                           SUM(DP.ilosc) AS SumaKg,
                           MIN(DP.cena) AS MinCena,
                           MAX(DP.cena) AS MaxCena,
                           SUM(DP.wartNetto) AS SumaWartosc,
                           COUNT(*) AS LiczbaTransakcji,
                           COUNT(DISTINCT DK.khid) AS LiczbaKontrahentow
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                      AND TW.katalog IN (67095, 67153)
                      AND (@TowarID IS NULL OR DP.idtw = @TowarID)
                      AND WYM.CDim_Handlowiec_Val IS NOT NULL
                      AND WYM.CDim_Handlowiec_Val NOT IN ('Ogolne', 'Ogólne')
                    GROUP BY WYM.CDim_Handlowiec_Val
                    ORDER BY SredniaCena DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);
                cmd.Parameters.AddWithValue("@TowarID", (object)towarId ?? DBNull.Value);

                // Zaladuj avatary
                await EnsureHandlowiecMappingLoadedAsync();
                _cenyChartData = new List<(string Handlowiec, double Wartosc)>();

                var wartosciCeny = new ChartValues<decimal>();
                var wartosciKg = new ChartValues<decimal>();
                var tempHandlowcy = new List<string>();

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var handlowiec = reader.GetString(0);
                    var sredniaCena = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1));
                    var sumaKg = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
                    var minCena = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3));
                    var maxCena = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4));
                    var sumaWartosc = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5));
                    var liczbaTransakcji = reader.GetInt32(6);
                    var liczbaKontrahentow = reader.GetInt32(7);

                    labels.Add(handlowiec);
                    tempHandlowcy.Add(handlowiec);
                    wartosciCeny.Add(sredniaCena);
                    wartosciKg.Add(sumaKg);
                    EnsureAvatarCached(handlowiec);
                    _cenyChartData.Add((handlowiec, (double)sredniaCena));

                    daneTabeli.Add(new HandlowiecCenyRow
                    {
                        Handlowiec = handlowiec,
                        SumaKg = sumaKg,
                        SredniaCena = sredniaCena,
                        MinCena = minCena,
                        MaxCena = maxCena,
                        SumaWartosc = sumaWartosc,
                        LiczbaTransakcji = liczbaTransakcji,
                        LiczbaKontrahentow = liczbaKontrahentow
                    });
                }

                // StackedColumnSeries per handlowiec - kazdy z wlasnym kolorem
                for (int i = 0; i < tempHandlowcy.Count; i++)
                {
                    var kolor = GetHandlowiecColor(tempHandlowcy[i]);
                    var valuesCeny = new ChartValues<double>();
                    var valuesKg = new ChartValues<double>();
                    for (int j = 0; j < tempHandlowcy.Count; j++)
                    {
                        valuesCeny.Add(j == i ? (double)wartosciCeny[j] : 0);
                        valuesKg.Add(j == i ? (double)wartosciKg[j] : 0);
                    }

                    seriesCeny.Add(new StackedColumnSeries
                    {
                        Title = tempHandlowcy[i],
                        Values = valuesCeny,
                        Fill = new SolidColorBrush(kolor),
                        DataLabels = false,
                        MaxColumnWidth = 80
                    });

                    seriesKg.Add(new StackedColumnSeries
                    {
                        Title = tempHandlowcy[i],
                        Values = valuesKg,
                        Fill = new SolidColorBrush(kolor),
                        DataLabels = false,
                        MaxColumnWidth = 80
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad analizy cen:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            _analizaCenyLabels = labels;
            chartCeny.Series = seriesCeny;
            axisXCeny.Labels = labels;
            chartCenyKg.Series = seriesKg;
            axisXCenyKg.Labels = labels;
            gridAnalizaCeny.ItemsSource = daneTabeli;

            // Pozycjonuj avatary na slupkach
            var cenyKgData = _cenyChartData.Select(d =>
            {
                var idx = labels.IndexOf(d.Handlowiec);
                var kg = idx >= 0 && idx < daneTabeli.Count ? (double)daneTabeli[idx].SumaKg : 0;
                return (d.Handlowiec, kg);
            }).ToList();

            var cenyLabels = _cenyChartData.Select(d => $"{d.Wartosc:F2} zl").ToList();
            var kgLabels = cenyKgData.Select(d => $"{d.Item2:N0} kg").ToList();
            OpoznionePozycjonowanie(() =>
            {
                PozycjonujAvataryNaSlupkach(canvasCenyAvatary, chartCeny, _cenyChartData, labelTexts: cenyLabels);
                PozycjonujAvataryNaSlupkach(canvasCenyKgAvatary, chartCenyKg, cenyKgData, labelTexts: kgLabels);
            });
        }

        private void GridAnalizaCeny_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridAnalizaCeny.SelectedItem is HandlowiecCenyRow row)
            {
                int rok = (int)cmbRokCeny.SelectedItem;
                int miesiac = (int)cmbMiesiacCeny.SelectedValue;
                var okno = new AnalizaCenHandlowcaWindow(row.Handlowiec, rok, miesiac);
                okno.Show();
            }
        }

        #endregion

        #region Swieze vs Mrozone

        private async System.Threading.Tasks.Task OdswiezSwiezeMrozoneAsync()
        {
            if (cmbRokSM.SelectedItem == null || cmbMiesiacSM.SelectedValue == null) return;

            int rok = (int)cmbRokSM.SelectedItem;
            int miesiac = (int)cmbMiesiacSM.SelectedValue;

            var series = new SeriesCollection();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT CASE WHEN TW.katalog = 67153 THEN 'Mrozone' ELSE 'Swieze' END AS Typ,
                           SUM(DP.ilosc) AS SumaKg, SUM(DP.wartNetto) AS WartoscNetto
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                    WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                      AND TW.katalog IN (67095, 67153)
                    GROUP BY CASE WHEN TW.katalog = 67153 THEN 'Mrozone' ELSE 'Swieze' END";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);

                var dane = new Dictionary<string, (decimal Kg, decimal Wartosc)>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var typ = reader.GetString(0);
                    var kg = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1));
                    var wartosc = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
                    dane[typ] = (kg, wartosc);
                }

                var swiezeKg = dane.ContainsKey("Swieze") ? dane["Swieze"].Kg : 0m;
                var mrozoneKg = dane.ContainsKey("Mrozone") ? dane["Mrozone"].Kg : 0m;

                series.Add(new ColumnSeries
                {
                    Title = "Swieze",
                    Values = new ChartValues<decimal> { swiezeKg },
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ECDC4")),
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0} kg",
                    Foreground = Brushes.White
                });

                series.Add(new ColumnSeries
                {
                    Title = "Mrozone",
                    Values = new ChartValues<decimal> { mrozoneKg },
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#45B7D1")),
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0} kg",
                    Foreground = Brushes.White
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad swieze vs mrozone:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            chartSwiezeMrozone.Series = series;
            axisXSM.Labels = new[] { "" };
        }

        #endregion

        #region Porownanie okresow

        private async System.Threading.Tasks.Task OdswiezPorownanieAsync()
        {
            if (cmbRokPorown1.SelectedItem == null || cmbRokPorown2.SelectedItem == null) return;

            int rok1 = (int)cmbRokPorown1.SelectedItem;
            int rok2 = (int)cmbRokPorown2.SelectedItem;

            var series = new SeriesCollection();
            var labels = _nazwyMiesiecy.Skip(1).ToList();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT YEAR(DK.data) AS Rok, MONTH(DK.data) AS Miesiac, SUM(DP.wartNetto) AS Wartosc
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    WHERE YEAR(DK.data) IN (@Rok1, @Rok2)
                    GROUP BY YEAR(DK.data), MONTH(DK.data)
                    ORDER BY Rok, Miesiac";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok1", rok1);
                cmd.Parameters.AddWithValue("@Rok2", rok2);

                var daneRok1 = new decimal[12];
                var daneRok2 = new decimal[12];

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var rok = reader.GetInt32(0);
                    var miesiac = reader.GetInt32(1);
                    var wartosc = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));

                    if (rok == rok1 && miesiac >= 1 && miesiac <= 12)
                        daneRok1[miesiac - 1] = wartosc;
                    else if (rok == rok2 && miesiac >= 1 && miesiac <= 12)
                        daneRok2[miesiac - 1] = wartosc;
                }

                series.Add(new LineSeries
                {
                    Title = rok1.ToString(),
                    Values = new ChartValues<decimal>(daneRok1),
                    Stroke = new SolidColorBrush(_kolory[0]),
                    Fill = Brushes.Transparent,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 8,
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0}",
                    Foreground = Brushes.White
                });

                series.Add(new LineSeries
                {
                    Title = rok2.ToString(),
                    Values = new ChartValues<decimal>(daneRok2),
                    Stroke = new SolidColorBrush(_kolory[1]),
                    Fill = Brushes.Transparent,
                    PointGeometry = DefaultGeometries.Square,
                    PointGeometrySize = 8,
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0}",
                    Foreground = Brushes.White
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad porownania:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            chartPorownanie.Series = series;
            axisXPorown.Labels = labels;
        }

        #endregion

        #region Trend sprzedazy

        private async System.Threading.Tasks.Task OdswiezTrendAsync()
        {
            if (cmbOkres.SelectedItem == null) return;

            int okres = (int)cmbOkres.SelectedItem;

            var series = new SeriesCollection();
            var labels = new List<string>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var dataOd = DateTime.Now.AddMonths(-okres);

                var sql = @"
                    SELECT YEAR(DK.data) AS Rok, MONTH(DK.data) AS Miesiac, SUM(DP.wartNetto) AS Wartosc
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    WHERE DK.data >= @DataOd
                    GROUP BY YEAR(DK.data), MONTH(DK.data)
                    ORDER BY Rok, Miesiac";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DataOd", new DateTime(dataOd.Year, dataOd.Month, 1));

                var wartosci = new ChartValues<decimal>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var rok = reader.GetInt32(0);
                    var miesiac = reader.GetInt32(1);
                    var wartosc = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));

                    labels.Add($"{_nazwyMiesiecy[miesiac]} {rok}");
                    wartosci.Add(wartosc);
                }

                series.Add(new LineSeries
                {
                    Title = "Trend sprzedazy",
                    Values = wartosci,
                    Stroke = new SolidColorBrush(_kolory[4]),
                    Fill = new SolidColorBrush(Color.FromArgb(50, _kolory[4].R, _kolory[4].G, _kolory[4].B)),
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 10,
                    LineSmoothness = 0.5,
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0}",
                    Foreground = Brushes.White
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad trendu:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            chartTrend.Series = series;
            axisXTrend.Labels = labels;
        }

        #endregion

        #region Saldo Opakowan

        private DateTime GetLastSunday(DateTime date)
        {
            int diff = (7 + ((int)date.DayOfWeek - (int)DayOfWeek.Sunday)) % 7;
            return date.AddDays(-diff).Date;
        }

        private async Task<(decimal E2, decimal H1)> PobierzSaldoNaDzien(SqlConnection cn, DateTime data, string handlowiec)
        {
            var sql = @"
SELECT
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS DECIMAL(18,0)) AS E2,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS DECIMAL(18,0)) AS H1
FROM [HANDEL].[HM].[MZ] MZ
INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE MZ.data >= '2020-01-01' AND MZ.data <= @DataDo AND MG.anulowany = 0
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1')
  AND (@Handlowiec IS NULL OR WYM.CDim_Handlowiec_Val = @Handlowiec)";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@DataDo", data);
            cmd.Parameters.AddWithValue("@Handlowiec", (object)handlowiec ?? DBNull.Value);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (reader.IsDBNull(0) ? 0m : Convert.ToDecimal(reader.GetValue(0)),
                        reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1)));
            return (0, 0);
        }

        // Zmienne dla opakowan
        private List<OpakowanieRow> _opakowaniaData = new List<OpakowanieRow>();
        private string _wybranyKontrahentOpak = null;
        private Dictionary<string, Color> _handlowiecKolory = new Dictionary<string, Color>();
        private string _opakTryb = "RAZEM"; // E2, H1 lub RAZEM


        private Color GetHandlowiecColor(string handlowiec)
        {
            if (!_handlowiecKolory.ContainsKey(handlowiec))
            {
                _handlowiecKolory[handlowiec] = _kolory[_handlowiecKolory.Count % _kolory.Length];
            }
            return _handlowiecKolory[handlowiec];
        }

        private void InicjalizujDatyOpak()
        {
            // Domyslnie: od 6 tygodni temu do dzisiaj
            dpOpakOd.SelectedDate = DateTime.Today.AddDays(-42);
            dpOpakDo.SelectedDate = DateTime.Today;
        }

        private async void DpOpakowania_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpOpakOd.SelectedDate.HasValue && dpOpakDo.SelectedDate.HasValue)
            {
                await OdswiezOpakowaniaAsync();
            }
        }

        private async void BtnOpakOkres_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                int dni = int.Parse(btn.Tag.ToString());
                if (dni == 9999)
                {
                    // Calosc - od 2020 do dzisiaj
                    dpOpakOd.SelectedDate = new DateTime(2020, 1, 1);
                }
                else if (dni == 0)
                {
                    // Dzisiaj
                    dpOpakOd.SelectedDate = DateTime.Today;
                }
                else
                {
                    dpOpakOd.SelectedDate = DateTime.Today.AddDays(-dni);
                }
                dpOpakDo.SelectedDate = DateTime.Today;
                await OdswiezOpakowaniaAsync();
            }
        }

        private async System.Threading.Tasks.Task OdswiezOpakowaniaAsync()
        {
            string wybranyHandlowiec = null;
            if (cmbHandlowiecOpak.SelectedItem is ComboItem item && item.Value > 0)
                wybranyHandlowiec = item.Text;

            DateTime data1 = dpOpakOd.SelectedDate ?? DateTime.Today.AddDays(-7);
            DateTime data2 = dpOpakDo.SelectedDate ?? DateTime.Today;

            _opakowaniaData = new List<OpakowanieRow>();

            try
            {
                // Multiple Result Sets - oba zapytania w jednym wykonaniu
                var sqlCombined = @"
-- Saldo na date 1
SELECT
    C.shortcut AS Kontrahent,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS DECIMAL(18,0)) AS E2,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS DECIMAL(18,0)) AS H1
FROM [HANDEL].[HM].[MZ] MZ WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
INNER JOIN [HANDEL].[HM].[MG] MG WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON MG.khid = C.id
WHERE MZ.data <= @DataDo1 AND MG.anulowany = 0
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1')
GROUP BY C.shortcut
HAVING SUM(MZ.Ilosc) <> 0;

-- Saldo na date 2
SELECT
    C.shortcut AS Kontrahent,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS DECIMAL(18,0)) AS E2,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS DECIMAL(18,0)) AS H1
FROM [HANDEL].[HM].[MZ] MZ WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
INNER JOIN [HANDEL].[HM].[MG] MG WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON MG.khid = C.id
WHERE MZ.data <= @DataDo2 AND MG.anulowany = 0
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1')
GROUP BY C.shortcut
HAVING SUM(MZ.Ilosc) <> 0;";

                var saldoNaData1 = new Dictionary<string, (decimal E2, decimal H1)>();
                var saldoNaData2 = new Dictionary<string, (decimal E2, decimal H1)>();

                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                // Multiple Result Sets - jedno polaczenie, dwa zestawy wynikow
                await using var cmd = new SqlCommand(sqlCombined, cn);
                cmd.CommandTimeout = 60;
                cmd.Parameters.AddWithValue("@DataDo1", data1);
                cmd.Parameters.AddWithValue("@DataDo2", data2);

                await using var reader = await cmd.ExecuteReaderAsync();

                // Pierwszy zestaw wynikow - saldo na date 1
                while (await reader.ReadAsync())
                {
                    var kontrahent = reader.GetString(0);
                    var e2 = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                    var h1 = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);
                    saldoNaData1[kontrahent] = (e2, h1);
                }

                // Przejdz do drugiego zestawu wynikow - saldo na date 2
                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    var kontrahent = reader.GetString(0);
                    var e2 = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                    var h1 = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);
                    saldoNaData2[kontrahent] = (e2, h1);
                }

                // Polacz wyniki - unikalne kontrahenty
                var wszystkieKontrahenty = saldoNaData1.Keys.Union(saldoNaData2.Keys).Distinct().ToList();
                foreach (var kontrahent in wszystkieKontrahenty)
                {
                    var d1 = saldoNaData1.GetValueOrDefault(kontrahent, (0, 0));
                    var d2 = saldoNaData2.GetValueOrDefault(kontrahent, (0, 0));

                    _opakowaniaData.Add(new OpakowanieRow
                    {
                        Kontrahent = kontrahent,
                        Handlowiec = "",
                        PojemnikiE2 = d2.E2,
                        PaletaH1 = d2.H1,
                        Razem = d2.E2 + d2.H1,
                        E2Zmiana = d2.E2 - d1.E2,
                        H1Zmiana = d2.H1 - d1.H1,
                        ZmianaE2Tydzien = d2.E2 - d1.E2,
                        ZmianaH1Tydzien = d2.H1 - d1.H1
                    });
                }

                // Odswiezamy UI
                OdswiezOpakowaniaUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad wczytywania opakowan:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Odswiezenie UI dla zakladki Opakowania - bez pobierania danych z SQL
        /// Obsluguje przelacznik E2/H1/RAZEM
        /// </summary>
        private void OdswiezOpakowaniaUI()
        {
            if (_opakowaniaData == null || !_opakowaniaData.Any()) return;
            if (!chartOpakowaniaE2.IsLoaded) return;

            // Oblicz wartosci w zaleznosci od wybranego trybu
            Func<OpakowanieRow, decimal> getSaldo = _opakTryb switch
            {
                "E2" => d => d.PojemnikiE2,
                "H1" => d => d.PaletaH1,
                _ => d => d.PojemnikiE2 + d.PaletaH1
            };
            Func<OpakowanieRow, decimal> getZmiana = _opakTryb switch
            {
                "E2" => d => d.E2Zmiana,
                "H1" => d => d.H1Zmiana,
                _ => d => d.E2Zmiana + d.H1Zmiana
            };

            // Sumy globalne
            var sumaSaldo = _opakowaniaData.Sum(d => getSaldo(d));
            var sumaZmiana = _opakowaniaData.Sum(d => getZmiana(d));
            var sumaE2 = _opakowaniaData.Sum(d => d.PojemnikiE2);
            var sumaH1 = _opakowaniaData.Sum(d => d.PaletaH1);

            // Wartosc: E2 × 30 zł + H1 × 60 zł
            var wartosc = sumaE2 * 30 + sumaH1 * 60;

            // Klienci gdzie rosnie / maleje
            var klienciRosnie = _opakowaniaData.Where(d => getZmiana(d) > 0).ToList();
            var klienciMaleje = _opakowaniaData.Where(d => getZmiana(d) < 0).ToList();
            var sumaRosnie = klienciRosnie.Sum(d => getZmiana(d));
            var sumaMaleje = klienciMaleje.Sum(d => getZmiana(d));

            // Aktualizuj KPI Cards
            var sign = sumaZmiana >= 0 ? "+" : "";
            var kolorSaldo = _opakTryb switch
            {
                "E2" => "#10B981",
                "H1" => "#3B82F6",
                _ => "#8B5CF6"
            };
            txtOpakSaldo.Text = $"{sumaSaldo:N0}";
            txtOpakSaldo.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolorSaldo));
            txtOpakSaldoZmiana.Text = $"{sign}{sumaZmiana:N0} vs poprzedni okres";
            txtOpakSaldoZmiana.Foreground = sumaZmiana >= 0
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));

            txtOpakWartosc.Text = $"{wartosc:N0} zl";
            txtOpakWartoscSkladowe.Text = $"E2({sumaE2:N0}×30) + H1({sumaH1:N0}×60)";

            txtOpakRosnie.Text = $"{klienciRosnie.Count} klientow";
            txtOpakRosnieSuma.Text = $"+{sumaRosnie:N0} szt";

            txtOpakMaleje.Text = $"{klienciMaleje.Count} klientow";
            txtOpakMalejeSuma.Text = $"{sumaMaleje:N0} szt";

            // Zmiana w naglowku
            var zmianaE2 = _opakowaniaData.Sum(d => d.E2Zmiana);
            var zmianaH1 = _opakowaniaData.Sum(d => d.H1Zmiana);
            var signE2 = zmianaE2 >= 0 ? "+" : "";
            var signH1 = zmianaH1 >= 0 ? "+" : "";
            txtOpakZmiana.Text = $"Zmiana E2: {signE2}{zmianaE2:N0} | H1: {signH1}{zmianaH1:N0}";

            // Wykres klientow - Top 12 wg wybranego trybu
            var top12 = _opakowaniaData
                .Where(d => getSaldo(d) > 0)
                .OrderByDescending(d => getSaldo(d))
                .Take(12)
                .Reverse()
                .ToList();

            var listaWartosci = new List<double>();
            var listaWartosciE2 = new List<double>();
            var listaWartosciH1 = new List<double>();
            var etykiety = new List<string>();
            foreach (var d in top12)
            {
                var saldo = getSaldo(d);
                var zmiana = getZmiana(d);
                listaWartosci.Add((double)saldo);
                listaWartosciE2.Add((double)d.PojemnikiE2);
                listaWartosciH1.Add((double)d.PaletaH1);
                var signZ = zmiana >= 0 ? "+" : "";
                var nazwa = d.Kontrahent.Length > 15 ? d.Kontrahent.Substring(0, 15) + ".." : d.Kontrahent;
                etykiety.Add($"{saldo:N0} ({signZ}{zmiana:N0}) | {nazwa}");
            }

            // Aktualizuj tytul wykresu klientow
            txtOpakChartKlienciTytul.Text = _opakTryb switch
            {
                "E2" => "SALDO E2 WG KLIENTA (Top 12)",
                "H1" => "SALDO H1 WG KLIENTA (Top 12)",
                _ => "SALDO RAZEM WG KLIENTA (Top 12)"
            };
            txtOpakChartKlienciTytul.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolorSaldo));

            // Wylacz animacje, wyczysc, ustaw dane
            chartOpakowaniaE2.DisableAnimations = true;
            chartOpakowaniaE2.Series = new SeriesCollection();
            axisYOpakE2.Labels = null;
            axisYOpakE2.MinValue = 0;
            axisYOpakE2.MaxValue = listaWartosci.Count;
            axisYOpakE2.Separator.Step = 1;

            SeriesCollection seriesKlienci;
            if (_opakTryb == "RAZEM")
            {
                // Dwie serie obok siebie (stacked)
                seriesKlienci = new SeriesCollection
                {
                    new StackedRowSeries
                    {
                        Title = "E2",
                        Values = new ChartValues<double>(listaWartosciE2),
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                        DataLabels = false
                    },
                    new StackedRowSeries
                    {
                        Title = "H1",
                        Values = new ChartValues<double>(listaWartosciH1),
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")),
                        DataLabels = false
                    }
                };
            }
            else
            {
                var kolor = _opakTryb == "E2" ? "#10B981" : "#3B82F6";
                seriesKlienci = new SeriesCollection
                {
                    new RowSeries
                    {
                        Title = _opakTryb,
                        Values = new ChartValues<double>(listaWartosci),
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolor)),
                        DataLabels = false,
                        Foreground = Brushes.White
                    }
                };
            }
            chartOpakowaniaE2.Series = seriesKlienci;
            axisYOpakE2.Labels = etykiety.ToArray();
            chartOpakowaniaE2.DisableAnimations = false;

            var sumaData2 = _opakTryb switch
            {
                "E2" => sumaE2,
                "H1" => sumaH1,
                _ => sumaE2 + sumaH1
            };
            var zmianaWyswietl = _opakTryb switch
            {
                "E2" => zmianaE2,
                "H1" => zmianaH1,
                _ => zmianaE2 + zmianaH1
            };
            var signWyswietl = zmianaWyswietl >= 0 ? "+" : "";
            txtOpakE2Suma.Text = $"Razem: {sumaData2:N0} (zmiana: {signWyswietl}{zmianaWyswietl:N0})";

            // Wykres per handlowiec - grupowanie danych
            var perHandlowiec = _opakowaniaData
                .GroupBy(d => string.IsNullOrEmpty(d.Handlowiec) ? "Nieprzypisany" : d.Handlowiec)
                .Select(g => new
                {
                    Handlowiec = g.Key,
                    E2 = g.Sum(x => x.PojemnikiE2),
                    H1 = g.Sum(x => x.PaletaH1),
                    Razem = g.Sum(x => x.PojemnikiE2 + x.PaletaH1),
                    Klientow = g.Count()
                })
                .OrderByDescending(h => _opakTryb == "E2" ? h.E2 : _opakTryb == "H1" ? h.H1 : h.Razem)
                .Take(10)
                .Reverse()
                .ToList();

            var listaH = new List<double>();
            var etykietyH = new List<string>();
            foreach (var h in perHandlowiec)
            {
                var saldo = _opakTryb switch
                {
                    "E2" => h.E2,
                    "H1" => h.H1,
                    _ => h.Razem
                };
                listaH.Add((double)saldo);
                etykietyH.Add($"{h.Handlowiec} ({h.Klientow} kl.)");
            }

            chartOpakowaniaH1.DisableAnimations = true;
            chartOpakowaniaH1.Series = new SeriesCollection();
            axisYOpakH1.Labels = null;
            axisYOpakH1.MinValue = 0;
            axisYOpakH1.MaxValue = listaH.Count;
            axisYOpakH1.Separator.Step = 1;

            var seriesHandlowiec = new SeriesCollection
            {
                new RowSeries
                {
                    Title = "Handlowiec",
                    Values = new ChartValues<double>(listaH),
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F97316")),
                    DataLabels = false,
                    Foreground = Brushes.White
                }
            };
            chartOpakowaniaH1.Series = seriesHandlowiec;
            axisYOpakH1.Labels = etykietyH.ToArray();
            chartOpakowaniaH1.DisableAnimations = false;
            txtOpakH1Suma.Text = "";

            // Wyczysc wykres liniowy trendu
            txtOpakWybranyKontrahent.Text = "";
            txtOpakTrendKlient.Text = "(kliknij slupek)";
            chartOpakTrend.Series = new SeriesCollection();
            axisXOpakTrend.Labels = new string[0];
        }

        private async void ChartOpakowaniaE2_DataClick(object sender, ChartPoint chartPoint)
        {
            await PokazTrendKlienta(chartPoint, axisYOpakE2.Labels);
        }

        private async void ChartOpakowaniaH1_DataClick(object sender, ChartPoint chartPoint)
        {
            await PokazTrendKlienta(chartPoint, axisYOpakH1.Labels);
        }

        private async Task PokazTrendKlienta(ChartPoint chartPoint, IList<string> labels)
        {
            var idx = (int)chartPoint.Y;
            if (idx < 0 || idx >= labels.Count) return;

            // Znajdz pelna nazwe kontrahenta z etykiety (format: "123 (+5) | NazwaKlienta..")
            var etykieta = labels[idx];
            var parts = etykieta.Split('|');
            if (parts.Length < 2) return;
            var skrocona = parts[1].Trim();

            var kontrahent = _opakowaniaData.FirstOrDefault(d =>
                d.Kontrahent == skrocona ||
                (d.Kontrahent.Length > 15 && d.Kontrahent.Substring(0, 15) + ".." == skrocona))?.Kontrahent;

            if (string.IsNullOrEmpty(kontrahent)) return;

            _wybranyKontrahentOpak = kontrahent;
            txtOpakWybranyKontrahent.Text = $"Wybrany: {kontrahent}";

            // Pobierz dane trendu dla klienta - Multiple Result Sets
            var trendLabels = new List<string>();
            var valuesE2 = new ChartValues<double>();
            var valuesH1 = new ChartValues<double>();
            var niedziela = GetLastSunday(DateTime.Today);

            await using var cn = new SqlConnection(_connectionStringHandel);
            await cn.OpenAsync();

            // Build batch query for all 8 weeks - znacznie szybsze niz 8 osobnych zapytan
            var sqlBuilder = new System.Text.StringBuilder();
            var dates = new List<DateTime>();
            for (int i = 7; i >= 0; i--)
            {
                var data = niedziela.AddDays(-7 * i);
                dates.Add(data);
                var nrTygodnia = System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                    data, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                trendLabels.Add($"T{nrTygodnia}");

                sqlBuilder.AppendLine($@"
SELECT
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS DECIMAL(18,0)) AS E2,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS DECIMAL(18,0)) AS H1
FROM [HANDEL].[HM].[MZ] MZ WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
INNER JOIN [HANDEL].[HM].[MG] MG WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON MG.khid = C.id
WHERE MZ.data >= '2020-01-01' AND MZ.data <= @DataDo{i} AND MG.anulowany = 0
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1')
  AND C.shortcut = @Kontrahent;");
            }

            await using var cmd = new SqlCommand(sqlBuilder.ToString(), cn);
            cmd.CommandTimeout = 60;
            cmd.Parameters.AddWithValue("@Kontrahent", kontrahent);
            for (int i = 7; i >= 0; i--)
            {
                cmd.Parameters.AddWithValue($"@DataDo{i}", dates[7 - i]);
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            for (int week = 0; week < 8; week++)
            {
                if (week > 0) await reader.NextResultAsync();

                if (await reader.ReadAsync())
                {
                    valuesE2.Add(reader.IsDBNull(0) ? 0 : Convert.ToDouble(reader.GetDecimal(0)));
                    valuesH1.Add(reader.IsDBNull(1) ? 0 : Convert.ToDouble(reader.GetDecimal(1)));
                }
                else
                {
                    valuesE2.Add(0);
                    valuesH1.Add(0);
                }
            }

            // Aktualizuj wykres liniowy z dwoma liniami (E2 i H1)
            txtOpakTrendKlient.Text = $"- {kontrahent}";
            chartOpakTrend.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "E2",
                    Values = valuesE2,
                    Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(23, 165, 137)),
                    Fill = Brushes.Transparent,
                    PointGeometrySize = 8,
                    StrokeThickness = 2,
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0}",
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(23, 165, 137))
                },
                new LineSeries
                {
                    Title = "H1",
                    Values = valuesH1,
                    Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(192, 57, 43)),
                    Fill = Brushes.Transparent,
                    PointGeometrySize = 8,
                    StrokeThickness = 2,
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0}",
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(192, 57, 43))
                }
            };
            axisXOpakTrend.Labels = trendLabels;
        }

        #endregion

        #region Platnosci

        private string GetKategoriaWiekowa(int? dni)
        {
            if (dni == null || dni <= 0) return "OK";
            if (dni <= 30) return "0-30";
            if (dni <= 60) return "31-60";
            if (dni <= 90) return "61-90";
            return "90+";
        }

        private async System.Threading.Tasks.Task OdswiezPlatnosciAsync()
        {
            string wybranyHandlowiec = null;
            if (cmbHandlowiecPlat.SelectedItem is ComboItem item && item.Value > 0)
                wybranyHandlowiec = item.Text;

            var dane = new List<PlatnoscRow>();
            var agingData = new AgingData();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                // Multiple Result Sets - oba zapytania w jednym wykonaniu
                var sqlCombined = @"
-- Glowne zapytanie z liczba faktur
WITH PNAgg AS (
    SELECT PN.dkid, SUM(ISNULL(PN.kwotarozl,0)) AS KwotaRozliczona, MAX(PN.Termin) AS TerminPrawdziwy
    FROM [HANDEL].[HM].[PN] PN WITH (NOLOCK) GROUP BY PN.dkid
),
Dokumenty AS (
    SELECT DK.id, DK.khid, DK.walbrutto, DK.plattermin
    FROM [HANDEL].[HM].[DK] DK WITH (NOLOCK)
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON DK.khid = C.id
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON DK.khid = WYM.ElementId
    WHERE DK.anulowany = 0 AND (@Handlowiec IS NULL OR WYM.CDim_Handlowiec_Val = @Handlowiec)
),
Saldo AS (
    SELECT D.id, D.khid,
           (D.walbrutto - ISNULL(PA.KwotaRozliczona,0)) AS DoZaplacenia,
           ISNULL(PA.TerminPrawdziwy, D.plattermin) AS TerminPlatnosci,
           CASE WHEN (D.walbrutto - ISNULL(PA.KwotaRozliczona,0)) > 0.01 AND GETDATE() > ISNULL(PA.TerminPrawdziwy, D.plattermin)
                THEN DATEDIFF(day, ISNULL(PA.TerminPrawdziwy, D.plattermin), GETDATE()) ELSE 0 END AS DniPrzeterminowania
    FROM Dokumenty D LEFT JOIN PNAgg PA ON PA.dkid = D.id
    WHERE (D.walbrutto - ISNULL(PA.KwotaRozliczona,0)) > 0.01
)
SELECT C.Shortcut AS Kontrahent,
       ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
       ISNULL(C.LimitAmount, 0) AS LimitKredytu,
       CAST(SUM(S.DoZaplacenia) AS DECIMAL(18,2)) AS DoZaplaty,
       CAST(SUM(CASE WHEN S.DniPrzeterminowania = 0 THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS Terminowe,
       CAST(SUM(CASE WHEN S.DniPrzeterminowania > 0 THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS Przeterminowane,
       MAX(S.DniPrzeterminowania) AS MaxDniPrzeterminowania,
       COUNT(*) AS LiczbaFaktur,
       SUM(CASE WHEN S.DniPrzeterminowania > 0 THEN 1 ELSE 0 END) AS LiczbaFakturPrzeterminowanych
FROM Saldo S
JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = S.khid
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON C.id = WYM.ElementId
WHERE (@Handlowiec IS NULL OR WYM.CDim_Handlowiec_Val = @Handlowiec)
GROUP BY C.Shortcut, WYM.CDim_Handlowiec_Val, C.LimitAmount
ORDER BY Przeterminowane DESC, DoZaplaty DESC;

-- Aging analysis per faktura - NOWE PRZEDZIALY: 1-7, 8-14, 15-21, 21+
WITH PNAgg2 AS (
    SELECT PN.dkid, SUM(ISNULL(PN.kwotarozl,0)) AS KwotaRozliczona, MAX(PN.Termin) AS TerminPrawdziwy
    FROM [HANDEL].[HM].[PN] PN WITH (NOLOCK) GROUP BY PN.dkid
),
FakturyPrzeterminowane AS (
    SELECT (DK.walbrutto - ISNULL(PA.KwotaRozliczona,0)) AS Kwota,
           DATEDIFF(day, ISNULL(PA.TerminPrawdziwy, DK.plattermin), GETDATE()) AS DniPrzeterminowania
    FROM [HANDEL].[HM].[DK] DK WITH (NOLOCK)
    LEFT JOIN PNAgg2 PA ON PA.dkid = DK.id
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON DK.khid = WYM.ElementId
    WHERE DK.anulowany = 0
      AND (DK.walbrutto - ISNULL(PA.KwotaRozliczona,0)) > 0.01
      AND GETDATE() > ISNULL(PA.TerminPrawdziwy, DK.plattermin)
      AND (@Handlowiec IS NULL OR WYM.CDim_Handlowiec_Val = @Handlowiec)
)
SELECT
    CAST(SUM(CASE WHEN DniPrzeterminowania BETWEEN 1 AND 7 THEN Kwota ELSE 0 END) AS DECIMAL(18,2)) AS Kwota17,
    CAST(SUM(CASE WHEN DniPrzeterminowania BETWEEN 8 AND 14 THEN Kwota ELSE 0 END) AS DECIMAL(18,2)) AS Kwota814,
    CAST(SUM(CASE WHEN DniPrzeterminowania BETWEEN 15 AND 21 THEN Kwota ELSE 0 END) AS DECIMAL(18,2)) AS Kwota1521,
    CAST(SUM(CASE WHEN DniPrzeterminowania > 21 THEN Kwota ELSE 0 END) AS DECIMAL(18,2)) AS Kwota21Plus,
    SUM(CASE WHEN DniPrzeterminowania BETWEEN 1 AND 7 THEN 1 ELSE 0 END) AS Faktur17,
    SUM(CASE WHEN DniPrzeterminowania BETWEEN 8 AND 14 THEN 1 ELSE 0 END) AS Faktur814,
    SUM(CASE WHEN DniPrzeterminowania BETWEEN 15 AND 21 THEN 1 ELSE 0 END) AS Faktur1521,
    SUM(CASE WHEN DniPrzeterminowania > 21 THEN 1 ELSE 0 END) AS Faktur21Plus
FROM FakturyPrzeterminowane;";

                await using var cmd = new SqlCommand(sqlCombined, cn);
                cmd.CommandTimeout = 60;
                cmd.Parameters.AddWithValue("@Handlowiec", (object)wybranyHandlowiec ?? DBNull.Value);

                await using var reader = await cmd.ExecuteReaderAsync();

                // Pierwszy zestaw wynikow - glowne dane platnosci
                while (await reader.ReadAsync())
                {
                    var kontrahent = reader.GetString(0);
                    var handlowiec = reader.GetString(1);
                    var limitKredytu = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
                    var doZaplaty = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3));
                    var terminowe = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4));
                    var przeterminowane = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5));
                    var dniPrzeterminowania = reader.IsDBNull(6) ? (int?)null : Convert.ToInt32(reader.GetValue(6));
                    var liczbaFaktur = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7));
                    var liczbaFakturPrzet = reader.IsDBNull(8) ? 0 : Convert.ToInt32(reader.GetValue(8));

                    var przekroczonyLimit = limitKredytu > 0 && doZaplaty > limitKredytu ? doZaplaty - limitKredytu : 0;
                    var procentLimitu = limitKredytu > 0 ? (doZaplaty / limitKredytu) * 100 : 0;

                    dane.Add(new PlatnoscRow
                    {
                        Kontrahent = kontrahent,
                        Handlowiec = handlowiec,
                        LimitKredytu = limitKredytu,
                        DoZaplaty = doZaplaty,
                        Terminowe = terminowe,
                        Przeterminowane = przeterminowane,
                        PrzekroczonyLimit = przekroczonyLimit,
                        DniPrzeterminowania = dniPrzeterminowania,
                        PrzeterminowaneAlert = przeterminowane > 0,
                        PrzekroczonyLimitAlert = przekroczonyLimit > 0,
                        KategoriaWiekowa = GetKategoriaWiekowa(dniPrzeterminowania),
                        LiczbaFaktur = liczbaFaktur,
                        LiczbaFakturPrzeterminowanych = liczbaFakturPrzet,
                        ProcentLimitu = procentLimitu
                    });
                }

                // Drugi zestaw wynikow - aging data z nowymi przedzialami
                await reader.NextResultAsync();
                if (await reader.ReadAsync())
                {
                    agingData.Kwota17 = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetValue(0));
                    agingData.Kwota814 = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1));
                    agingData.Kwota1521 = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2));
                    agingData.Kwota21Plus = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3));
                    agingData.Faktur17 = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4));
                    agingData.Faktur814 = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5));
                    agingData.Faktur1521 = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6));
                    agingData.Faktur21PlusNew = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7));
                }

                // Statystyki glowne
                var sumaDoZaplaty = dane.Sum(d => d.DoZaplaty);
                var sumaTerminowe = dane.Sum(d => d.Terminowe);
                var sumaPrzeterminowane = dane.Sum(d => d.Przeterminowane);
                var sumaPrzekroczony = dane.Sum(d => d.PrzekroczonyLimit);
                var iloscKlientow = dane.Count;
                var iloscZPrzeterminowanymi = dane.Count(d => d.Przeterminowane > 0);
                var iloscZPrzekroczonym = dane.Count(d => d.PrzekroczonyLimit > 0);
                var sumaFakturPrzeterminowanych = dane.Sum(d => d.LiczbaFakturPrzeterminowanych);
                var maxDni = dane.Where(d => d.DniPrzeterminowania.HasValue && d.DniPrzeterminowania > 0).MaxOrDefault(d => d.DniPrzeterminowania.Value);
                var maxDniKlient = dane.FirstOrDefault(d => d.DniPrzeterminowania == maxDni)?.Kontrahent ?? "";

                // Aktualizuj karty
                txtPlatSumaDoZaplaty.Text = $"{sumaDoZaplaty:N0} zł";
                txtPlatIloscKlientow.Text = $"{iloscKlientow} klientów";
                txtPlatTerminowe.Text = $"{sumaTerminowe:N0} zł";
                txtPlatTerminoweProcent.Text = sumaDoZaplaty > 0 ? $"{sumaTerminowe / sumaDoZaplaty * 100:F1}%" : "0%";
                txtPlatPrzeterminowane.Text = $"{sumaPrzeterminowane:N0} zł";
                txtPlatPrzeterminowaneProcent.Text = $"{(sumaDoZaplaty > 0 ? sumaPrzeterminowane / sumaDoZaplaty * 100 : 0):F1}%";
                txtPlatLiczbaFakturPrzet.Text = $"{sumaFakturPrzeterminowanych}";
                txtPlatLiczbaKlientowPrzet.Text = $"u {iloscZPrzeterminowanymi} klientów";
                txtPlatPrzekroczony.Text = $"{sumaPrzekroczony:N0} zł";
                txtPlatPrzekroczonyIlosc.Text = $"{iloscZPrzekroczonym} klientów";
                txtPlatMaxDni.Text = $"{maxDni} dni";
                txtPlatMaxDniKlient.Text = maxDniKlient;

                // Aging analysis - NOWE PRZEDZIALY: 1-7, 8-14, 15-21, 21+
                var agingTotal = agingData.Kwota17 + agingData.Kwota814 + agingData.Kwota1521 + agingData.Kwota21Plus;

                // Aktualizuj teksty
                txtAging17.Text = $"{agingData.Kwota17:N0} zł";
                txtAging17Procent.Text = $"{(agingTotal > 0 ? agingData.Kwota17 / agingTotal * 100 : 0):F0}%";
                txtAging814.Text = $"{agingData.Kwota814:N0} zł";
                txtAging814Procent.Text = $"{(agingTotal > 0 ? agingData.Kwota814 / agingTotal * 100 : 0):F0}%";
                txtAging1521.Text = $"{agingData.Kwota1521:N0} zł";
                txtAging1521Procent.Text = $"{(agingTotal > 0 ? agingData.Kwota1521 / agingTotal * 100 : 0):F0}%";
                txtAging21Plus.Text = $"{agingData.Kwota21Plus:N0} zł";
                txtAging21PlusProcent.Text = $"{(agingTotal > 0 ? agingData.Kwota21Plus / agingTotal * 100 : 0):F0}%";

                // Aktualizuj slupki aging (szerokosci proporcjonalne)
                var maxBarWidth = 100.0;
                var maxAgingKwota = Math.Max(Math.Max(agingData.Kwota17, agingData.Kwota814), Math.Max(agingData.Kwota1521, agingData.Kwota21Plus));
                if (maxAgingKwota > 0)
                {
                    barAging17.Width = (double)(agingData.Kwota17 / maxAgingKwota) * maxBarWidth;
                    barAging814.Width = (double)(agingData.Kwota814 / maxAgingKwota) * maxBarWidth;
                    barAging1521.Width = (double)(agingData.Kwota1521 / maxAgingKwota) * maxBarWidth;
                    barAging21Plus.Width = (double)(agingData.Kwota21Plus / maxAgingKwota) * maxBarWidth;
                }

                // Przekroczone limity kredytowe wg kwoty przekroczenia
                var klienciZPrzekroczonymLimitem = dane.Where(d => d.PrzekroczonyLimit > 0).ToList();

                // do 100k przekroczenia
                var limit100kKwota = klienciZPrzekroczonymLimitem.Where(d => d.PrzekroczonyLimit <= 100000).Sum(d => d.PrzekroczonyLimit);
                var limit100kKlientow = klienciZPrzekroczonymLimitem.Count(d => d.PrzekroczonyLimit <= 100000);

                // 100-300k przekroczenia
                var limit300kKwota = klienciZPrzekroczonymLimitem.Where(d => d.PrzekroczonyLimit > 100000 && d.PrzekroczonyLimit <= 300000).Sum(d => d.PrzekroczonyLimit);
                var limit300kKlientow = klienciZPrzekroczonymLimitem.Count(d => d.PrzekroczonyLimit > 100000 && d.PrzekroczonyLimit <= 300000);

                // 300-500k przekroczenia
                var limit500kKwota = klienciZPrzekroczonymLimitem.Where(d => d.PrzekroczonyLimit > 300000 && d.PrzekroczonyLimit <= 500000).Sum(d => d.PrzekroczonyLimit);
                var limit500kKlientow = klienciZPrzekroczonymLimitem.Count(d => d.PrzekroczonyLimit > 300000 && d.PrzekroczonyLimit <= 500000);

                // 500k+ przekroczenia
                var limit500kPlusKwota = klienciZPrzekroczonymLimitem.Where(d => d.PrzekroczonyLimit > 500000).Sum(d => d.PrzekroczonyLimit);
                var limit500kPlusKlientow = klienciZPrzekroczonymLimitem.Count(d => d.PrzekroczonyLimit > 500000);

                // Aktualizuj UI przekroczonych limitow
                txtLimit100k.Text = $"{limit100kKwota:N0} zł";
                txtLimit100kKlientow.Text = $"{limit100kKlientow} kl.";
                txtLimit300k.Text = $"{limit300kKwota:N0} zł";
                txtLimit300kKlientow.Text = $"{limit300kKlientow} kl.";
                txtLimit500k.Text = $"{limit500kKwota:N0} zł";
                txtLimit500kKlientow.Text = $"{limit500kKlientow} kl.";
                txtLimit500kPlus.Text = $"{limit500kPlusKwota:N0} zł";
                txtLimit500kPlusKlientow.Text = $"{limit500kPlusKlientow} kl.";

                // Aktualizuj slupki limitow
                var maxLimitKwota = Math.Max(Math.Max(limit100kKwota, limit300kKwota), Math.Max(limit500kKwota, limit500kPlusKwota));
                if (maxLimitKwota > 0)
                {
                    barLimit100k.Width = (double)(limit100kKwota / maxLimitKwota) * maxBarWidth;
                    barLimit300k.Width = (double)(limit300kKwota / maxLimitKwota) * maxBarWidth;
                    barLimit500k.Width = (double)(limit500kKwota / maxLimitKwota) * maxBarWidth;
                    barLimit500kPlus.Width = (double)(limit500kPlusKwota / maxLimitKwota) * maxBarWidth;
                }

                // Rysuj donut chart
                RysujDonut(sumaTerminowe, sumaPrzeterminowane);

                // Top przeterminowani - 6 najgorszych
                var topPrzeterminowani = dane
                    .Where(d => d.Przeterminowane > 0 && d.DniPrzeterminowania.HasValue)
                    .OrderByDescending(d => d.Przeterminowane)
                    .Take(6)
                    .Select((d, idx) => new TopPrzeterminowanyRow
                    {
                        Pozycja = idx + 1,
                        Kontrahent = d.Kontrahent,
                        Kwota = d.Przeterminowane,
                        Dni = d.DniPrzeterminowania.Value
                    })
                    .ToList();
                listTopPrzeterminowani.ItemsSource = topPrzeterminowani;

                // Wskazniki platnosci
                var daneZPrzeterminowaniem = dane.Where(d => d.DniPrzeterminowania.HasValue && d.DniPrzeterminowania.Value > 0).ToList();
                var srednieDniOpoznienia = daneZPrzeterminowaniem.Any()
                    ? daneZPrzeterminowaniem.Average(d => d.DniPrzeterminowania.Value)
                    : 0;
                var procentPrzeterminowanych = sumaDoZaplaty > 0 ? (sumaPrzeterminowane / sumaDoZaplaty * 100) : 0;
                var dyscyplinaPlatnicza = sumaDoZaplaty > 0 ? (sumaTerminowe / sumaDoZaplaty * 100) : 100;

                txtWskaznikSrednieDni.Text = $"{srednieDniOpoznienia:F0} dni";
                txtWskaznikProcentPrzeterminowanych.Text = $"{procentPrzeterminowanych:F1}%";
                txtWskaznikLiczbaKlientow.Text = $"{iloscZPrzeterminowanymi}";
                txtWskaznikPrzekroczonyLimit.Text = $"{iloscZPrzekroczonym}";
                txtWskaznikDyscyplina.Text = $"{dyscyplinaPlatnicza:F1}%";

                // Ustawienie kolorow wskaznikow w zaleznosci od wartosci
                txtWskaznikDyscyplina.Foreground = new SolidColorBrush(
                    dyscyplinaPlatnicza >= 80 ? System.Windows.Media.Color.FromRgb(23, 165, 137) :
                    dyscyplinaPlatnicza >= 60 ? System.Windows.Media.Color.FromRgb(255, 230, 109) :
                    System.Windows.Media.Color.FromRgb(255, 107, 107));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad wczytywania platnosci:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            gridPlatnosci.ItemsSource = dane;
        }

        /// <summary>
        /// Rysuje wykres donut na Canvas
        /// </summary>
        private void RysujDonut(decimal terminowe, decimal przeterminowane)
        {
            canvasDonut.Children.Clear();

            var total = terminowe + przeterminowane;
            if (total <= 0)
            {
                txtDonutProcent.Text = "0%";
                txtDonutTerminowe.Text = "Terminowe 0%";
                txtDonutPrzeterminowane.Text = "Przeterminowane 0%";
                return;
            }

            var procentTerminowe = (double)(terminowe / total);
            var procentPrzeterminowane = (double)(przeterminowane / total);

            // Parametry donuta
            var centerX = canvasDonut.Width / 2;
            var centerY = canvasDonut.Height / 2;
            var outerRadius = Math.Min(centerX, centerY) - 5;
            var innerRadius = outerRadius * 0.6;

            // Segment terminowe (zielony)
            if (terminowe > 0)
            {
                var startAngle = -90.0; // Start od gory
                var sweepAngle = procentTerminowe * 360.0;
                var path = CreateArcPath(centerX, centerY, outerRadius, innerRadius, startAngle, sweepAngle, "#17A589");
                canvasDonut.Children.Add(path);
            }

            // Segment przeterminowane (czerwony)
            if (przeterminowane > 0)
            {
                var startAngle = -90.0 + procentTerminowe * 360.0;
                var sweepAngle = procentPrzeterminowane * 360.0;
                var path = CreateArcPath(centerX, centerY, outerRadius, innerRadius, startAngle, sweepAngle, "#EF4444");
                canvasDonut.Children.Add(path);
            }

            // Aktualizuj teksty
            txtDonutProcent.Text = $"{procentTerminowe * 100:F0}%";
            txtDonutTerminowe.Text = $"Terminowe {procentTerminowe * 100:F0}%";
            txtDonutPrzeterminowane.Text = $"Przeterminowane {procentPrzeterminowane * 100:F0}%";
        }

        /// <summary>
        /// Tworzy Path dla segmentu donuta
        /// </summary>
        private System.Windows.Shapes.Path CreateArcPath(double centerX, double centerY, double outerRadius, double innerRadius, double startAngle, double sweepAngle, string colorHex)
        {
            if (sweepAngle >= 360) sweepAngle = 359.99; // Unikaj problemu z pelnym kolem

            var startRad = startAngle * Math.PI / 180;
            var endRad = (startAngle + sweepAngle) * Math.PI / 180;

            var outerStartX = centerX + outerRadius * Math.Cos(startRad);
            var outerStartY = centerY + outerRadius * Math.Sin(startRad);
            var outerEndX = centerX + outerRadius * Math.Cos(endRad);
            var outerEndY = centerY + outerRadius * Math.Sin(endRad);

            var innerStartX = centerX + innerRadius * Math.Cos(startRad);
            var innerStartY = centerY + innerRadius * Math.Sin(startRad);
            var innerEndX = centerX + innerRadius * Math.Cos(endRad);
            var innerEndY = centerY + innerRadius * Math.Sin(endRad);

            var isLargeArc = sweepAngle > 180;

            var figure = new PathFigure
            {
                StartPoint = new Point(outerStartX, outerStartY),
                IsClosed = true
            };

            // Zewnetrzny luk
            figure.Segments.Add(new ArcSegment
            {
                Point = new Point(outerEndX, outerEndY),
                Size = new Size(outerRadius, outerRadius),
                IsLargeArc = isLargeArc,
                SweepDirection = SweepDirection.Clockwise
            });

            // Linia do wewnetrznego konca
            figure.Segments.Add(new LineSegment { Point = new Point(innerEndX, innerEndY) });

            // Wewnetrzny luk (odwrotny kierunek)
            figure.Segments.Add(new ArcSegment
            {
                Point = new Point(innerStartX, innerStartY),
                Size = new Size(innerRadius, innerRadius),
                IsLargeArc = isLargeArc,
                SweepDirection = SweepDirection.Counterclockwise
            });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            return new System.Windows.Shapes.Path
            {
                Data = geometry,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex))
            };
        }

        private void GridPlatnosci_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridPlatnosci.SelectedItem is PlatnoscRow row)
            {
                var okno = new KontrahentPlatnosciWindow(row.Kontrahent, row.Handlowiec);
                okno.Show();
            }
        }

        private void GridPlatnosci_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // Domyslnie sortuj malejaco (od najwyzszego do najnizszego)
            e.Handled = true;

            var column = e.Column;
            var direction = System.ComponentModel.ListSortDirection.Descending;

            // Jesli juz sortujemy malejaco, zmien na rosnaco
            if (column.SortDirection == System.ComponentModel.ListSortDirection.Descending)
            {
                direction = System.ComponentModel.ListSortDirection.Ascending;
            }

            column.SortDirection = direction;

            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(gridPlatnosci.ItemsSource);
            if (view != null)
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new System.ComponentModel.SortDescription(column.SortMemberPath, direction));
                view.Refresh();
            }
        }

        #endregion
    }

    // Klasa danych dla tabeli opakowan
    public class OpakowanieRow
    {
        public string Kontrahent { get; set; }
        public string Handlowiec { get; set; }
        public decimal PojemnikiE2 { get; set; }
        public decimal PaletaH1 { get; set; }
        public decimal Razem { get; set; }
        public decimal ZmianaE2Tydzien { get; set; }
        public decimal ZmianaH1Tydzien { get; set; }
        public decimal E2Zmiana { get; set; }
        public decimal H1Zmiana { get; set; }
        public decimal ZmianaRazem => ZmianaE2Tydzien + ZmianaH1Tydzien;
        public bool ZmianaE2TydzienAlert { get; set; }
        public bool ZmianaE2TydzienGood { get; set; }
        public bool ZmianaH1TydzienAlert { get; set; }
        public bool ZmianaH1TydzienGood { get; set; }
        public bool DuzyWzrostAlert => ZmianaRazem > 10;
        public bool DuzySpadekGood => ZmianaRazem < -10;

        // Formatowane teksty do bindowania
        public string PojemnikiE2Tekst => $"{PojemnikiE2:N0}";
        public string PaletaH1Tekst => $"{PaletaH1:N0}";
        public string RazemTekst => $"{Razem:N0}";
        public string ZmianaE2TydzienTekst => ZmianaE2Tydzien != 0 ? $"{(ZmianaE2Tydzien > 0 ? "+" : "")}{ZmianaE2Tydzien:N0}" : "0";
        public string ZmianaH1TydzienTekst => ZmianaH1Tydzien != 0 ? $"{(ZmianaH1Tydzien > 0 ? "+" : "")}{ZmianaH1Tydzien:N0}" : "0";
        public string ZmianaRazemTekst => ZmianaRazem != 0 ? $"{(ZmianaRazem > 0 ? "+" : "")}{ZmianaRazem:N0}" : "0";
    }

    // Klasa danych dla podsumowania per handlowiec
    public class HandlowiecOpakowanieRow
    {
        public string Handlowiec { get; set; }
        public decimal E2 { get; set; }
        public decimal H1 { get; set; }
        public decimal Razem => E2 + H1;
        public int LiczbaKontrahentow { get; set; }
    }

    // Klasa danych dla tabeli platnosci
    public class PlatnoscRow
    {
        public string Kontrahent { get; set; }
        public string Handlowiec { get; set; }
        public decimal LimitKredytu { get; set; }
        public decimal DoZaplaty { get; set; }
        public decimal Terminowe { get; set; }
        public decimal Przeterminowane { get; set; }
        public decimal PrzekroczonyLimit { get; set; }
        public int? DniPrzeterminowania { get; set; }
        public bool PrzeterminowaneAlert { get; set; }
        public bool PrzekroczonyLimitAlert { get; set; }
        public string KategoriaWiekowa { get; set; }
        public int LiczbaFaktur { get; set; }
        public int LiczbaFakturPrzeterminowanych { get; set; }
        public decimal ProcentLimitu { get; set; }

        // Status wierszy - kolor tla
        public bool IsGreenRow => Przeterminowane == 0 && PrzekroczonyLimit <= 0;
        public bool IsRedRow => DniPrzeterminowania.HasValue && DniPrzeterminowania.Value > 30;
        public bool IsOrangeRow => DniPrzeterminowania.HasValue && DniPrzeterminowania.Value > 0 && DniPrzeterminowania.Value <= 30;

        // Formatowane teksty do bindowania
        public string LimitKredytuTekst => LimitKredytu > 0 ? $"{LimitKredytu:N2} zl" : "-";
        public string DoZaplatyTekst => $"{DoZaplaty:N2} zl";
        public string TerminoweTekst => Terminowe > 0 ? $"{Terminowe:N2} zl" : "0,00 zl";
        public string PrzeterminowaneTekst => Przeterminowane > 0 ? $"{Przeterminowane:N2} zl" : "-";
        public string PrzekroczonyLimitTekst => PrzekroczonyLimit != 0 ? $"{PrzekroczonyLimit:N2} zl" : "-";
        public string ProcentLimituTekst => LimitKredytu > 0 ? $"{ProcentLimitu:F0}%" : "-";

        // Tekst najpozniejszej platnosci w stylu "X dni po terminie"
        public string NajpozniejszaPlatnoscTekst => DniPrzeterminowania.HasValue && DniPrzeterminowania.Value > 0
            ? $"{DniPrzeterminowania.Value} dni po terminie"
            : "";
    }

    // Klasa do przechowywania danych aging per faktura - NOWE PRZEDZIALY
    public class AgingData
    {
        // Stare przedzialy (dla kompatybilnosci)
        public decimal Kwota030 { get; set; }
        public decimal Kwota3160 { get; set; }
        public decimal Kwota6190 { get; set; }
        public decimal Kwota90Plus { get; set; }
        public int Faktur030 { get; set; }
        public int Faktur3160 { get; set; }
        public int Faktur6190 { get; set; }
        public int Faktur90Plus { get; set; }

        // Nowe przedzialy: 1-7, 8-14, 15-21, 21+
        public decimal Kwota17 { get; set; }
        public decimal Kwota814 { get; set; }
        public decimal Kwota1521 { get; set; }
        public decimal Kwota21Plus { get; set; }
        public int Faktur17 { get; set; }
        public int Faktur814 { get; set; }
        public int Faktur1521 { get; set; }
        public int Faktur21PlusNew { get; set; }
    }

    // Klasa dla listy Top Przeterminowani
    public class TopPrzeterminowanyRow
    {
        public int Pozycja { get; set; }
        public string Kontrahent { get; set; }
        public decimal Kwota { get; set; }
        public int Dni { get; set; }

        public string KwotaTekst => Kwota >= 1000000 ? $"{Kwota / 1000000:F1}M zl" :
                                    Kwota >= 1000 ? $"{Kwota / 1000:F0}k zl" :
                                    $"{Kwota:N0} zl";
        public string DniTekst => $"{Dni} dni po terminie";
    }

    // Klasa danych dla tabeli analizy cen handlowcow
    public class HandlowiecCenyRow
    {
        public string Handlowiec { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SredniaCena { get; set; }
        public decimal MinCena { get; set; }
        public decimal MaxCena { get; set; }
        public decimal SumaWartosc { get; set; }
        public int LiczbaTransakcji { get; set; }
        public int LiczbaKontrahentow { get; set; }

        public string SumaKgTekst => $"{SumaKg:N2}";
        public string SredniaCenaTekst => $"{SredniaCena:F2}";
        public string MinCenaTekst => $"{MinCena:F2}";
        public string MaxCenaTekst => $"{MaxCena:F2}";
        public string SumaWartoscTekst => $"{SumaWartosc:N2}";
    }
}
