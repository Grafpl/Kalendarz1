using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Kalendarz1.Opakowania.ViewModels;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.Views
{
    /// <summary>
    /// Dashboard główny modułu opakowań zwrotnych
    /// </summary>
    public partial class OpakowaniaMainDashboard : Window
    {
        private readonly DashboardViewModel _viewModel;
        private readonly DispatcherTimer _diagnosticsTimer;
        private readonly Stopwatch _lastLoadStopwatch = new Stopwatch();
        private int _lastLoadRecordCount;
        private string _lastLoadSource = "--";

        public OpakowaniaMainDashboard(string userId)
        {
            InitializeComponent();
            _viewModel = new DashboardViewModel(userId);
            DataContext = _viewModel;

            // Timer do odświeżania diagnostyki co 2 sekundy
            _diagnosticsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _diagnosticsTimer.Tick += (s, e) => RefreshDiagnosticsPanel();
            _diagnosticsTimer.Start();

            // Subskrybuj zdarzenie ładowania
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.IsLoading))
                {
                    if (_viewModel.IsLoading)
                    {
                        _lastLoadStopwatch.Restart();
                    }
                    else
                    {
                        _lastLoadStopwatch.Stop();
                        _lastLoadRecordCount = _viewModel.LiczbaWynikow;
                        _lastLoadSource = "Cache/DB";
                        Dispatcher.BeginInvoke(() => RefreshDiagnosticsPanel());
                    }
                }
            };

            // Pierwsze odświeżenie
            Loaded += (s, e) => RefreshDiagnosticsPanel();
        }

        #region Diagnostyka

        /// <summary>
        /// Odświeża panel diagnostyki
        /// </summary>
        private void RefreshDiagnosticsPanel()
        {
            try
            {
                // Ostatnie ładowanie
                if (_lastLoadStopwatch.ElapsedMilliseconds > 0)
                {
                    txtOstatnieCzasLadowania.Text = $"Czas: {_lastLoadStopwatch.ElapsedMilliseconds} ms";
                    txtOstatnieCzasLadowania.Foreground = _lastLoadStopwatch.ElapsedMilliseconds < 100
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 201, 176))  // Zielony
                        : _lastLoadStopwatch.ElapsedMilliseconds < 2000
                            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0))  // Żółty
                            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 71, 71));  // Czerwony
                }
                txtOstatnieLiczbaRekordow.Text = $"Rekordów: {_lastLoadRecordCount}";
                txtOstatnieZrodlo.Text = $"Źródło: {_lastLoadSource}";

                // Stan cache - pobierz z SaldaService
                var cacheStatus = SaldaService.GetCacheStatus();
                ParseAndDisplayCacheStatus(cacheStatus);

                // Pełny raport profilera
                txtPelnyRaport.Text = PerformanceProfiler.GenerateReport();

                // Aktualizuj czasy operacji
                UpdateOperationTimings();
            }
            catch (Exception ex)
            {
                txtPelnyRaport.Text = $"Błąd diagnostyki: {ex.Message}";
            }
        }

        /// <summary>
        /// Parsuje status cache i wyświetla w panelu
        /// </summary>
        private void ParseAndDisplayCacheStatus(string status)
        {
            var lines = status.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("Salda:"))
                {
                    var value = line.Replace("Salda:", "").Trim();
                    txtCacheSalda.Text = value;
                    txtCacheSalda.Foreground = value.Contains("PUSTY")
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 71, 71))
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 201, 176));

                    // Wyciągnij wiek cache
                    if (value.Contains("wiek:"))
                    {
                        var idx = value.IndexOf("wiek:");
                        var wiek = value.Substring(idx + 5).Split(',')[0].Trim();
                        txtCacheWiek.Text = wiek;
                    }
                }
                else if (line.StartsWith("Potwierdzenia:"))
                {
                    var value = line.Replace("Potwierdzenia:", "").Trim();
                    txtCachePotwierdzenia.Text = value.Contains("PUSTY") ? "PUSTY" : value.Split(' ')[0];
                    txtCachePotwierdzenia.Foreground = value.Contains("PUSTY")
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 71, 71))
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 201, 176));
                }
                else if (line.StartsWith("Dokumenty:"))
                {
                    txtCacheDokumenty.Text = line.Replace("Dokumenty:", "").Trim();
                }
            }

            // Cache hits/misses
            var report = PerformanceProfiler.GenerateShortReport();
            int totalHits = 0, totalMisses = 0;

            // Parsowanie raportu dla hit/miss
            if (report.Contains("hits") || report.Contains("misses"))
            {
                // Szacowane wartości z raportu
                foreach (var line in report.Split('\n'))
                {
                    if (line.Contains("hit rate"))
                    {
                        // Wyciągnij procent
                    }
                }
            }

            // Pobierz stats z profilera (uproszczone)
            var shortReport = PerformanceProfiler.GenerateShortReport();
            if (shortReport.Contains("hits"))
            {
                // Parse hits and misses from short report
                foreach (var line in shortReport.Split('\n'))
                {
                    if (line.Contains("hits,"))
                    {
                        var parts = line.Split(' ');
                        foreach (var part in parts)
                        {
                            if (int.TryParse(part, out var num))
                            {
                                if (line.IndexOf(part) < line.IndexOf("hits"))
                                    totalHits += num;
                                else if (line.IndexOf(part) < line.IndexOf("misses"))
                                    totalMisses += num;
                            }
                        }
                    }
                }
            }

            txtCacheHits.Text = totalHits.ToString();
            txtCacheMisses.Text = totalMisses.ToString();

            var total = totalHits + totalMisses;
            if (total > 0)
            {
                var hitRate = (double)totalHits / total * 100;
                txtCacheHitRate.Text = $"{hitRate:F0}%";
                txtCacheHitRate.Foreground = hitRate > 80
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 201, 176))
                    : hitRate > 50
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0))
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 71, 71));
            }
        }

        /// <summary>
        /// Aktualizuje panel czasów operacji
        /// </summary>
        private void UpdateOperationTimings()
        {
            var shortReport = PerformanceProfiler.GenerateShortReport();
            var lines = shortReport.Split('\n');

            pnlCzasyOperacji.Children.Clear();

            bool inTimings = false;
            foreach (var line in lines)
            {
                if (line.Contains("===") && line.Contains("PROFILER"))
                {
                    inTimings = true;
                    continue;
                }
                if (line.Contains("===") && line.Contains("CACHE"))
                {
                    break;
                }

                if (inTimings && !string.IsNullOrWhiteSpace(line))
                {
                    var tb = new TextBlock
                    {
                        Text = line.Trim(),
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 10,
                        Foreground = line.Contains("ms") && !line.Contains("Średni")
                            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 220, 254))
                            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 220, 220)),
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    pnlCzasyOperacji.Children.Add(tb);
                }
            }

            if (pnlCzasyOperacji.Children.Count == 0)
            {
                pnlCzasyOperacji.Children.Add(new TextBlock
                {
                    Text = "(brak danych - uruchom test)",
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 10,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102)),
                    FontStyle = FontStyles.Italic
                });
            }
        }

        /// <summary>
        /// Uruchamia test wydajności
        /// </summary>
        private async void BtnRunTest_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔════════════════════════════════════════╗");
            sb.AppendLine("║     TEST WYDAJNOŚCI - START            ║");
            sb.AppendLine("╠════════════════════════════════════════╣");
            txtPelnyRaport.Text = sb.ToString();

            try
            {
                var service = new SaldaService();

                // Test 1: Pierwsze ładowanie
                sb.AppendLine("║ Test 1: Ładowanie sald...              ║");
                txtPelnyRaport.Text = sb.ToString();
                await Task.Delay(100); // UI update

                var sw1 = Stopwatch.StartNew();
                var salda1 = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
                sw1.Stop();

                _lastLoadStopwatch.Reset();
                _lastLoadStopwatch.Start();
                _lastLoadStopwatch.Stop();

                sb.AppendLine($"║   Czas: {sw1.ElapsedMilliseconds,6} ms                      ║");
                sb.AppendLine($"║   Rekordów: {salda1.Count,-6}                      ║");

                var status1 = sw1.ElapsedMilliseconds < 100 ? "CACHE HIT!" :
                              sw1.ElapsedMilliseconds < 2000 ? "OK (z bazy)" : "WOLNO!";
                _lastLoadSource = status1;
                _lastLoadRecordCount = salda1.Count;

                sb.AppendLine($"║   Status: {status1,-20}       ║");
                sb.AppendLine("║                                        ║");
                txtPelnyRaport.Text = sb.ToString();
                await Task.Delay(100);

                // Test 2: Drugie ładowanie (powinno być z cache)
                sb.AppendLine("║ Test 2: Ponowne ładowanie...           ║");
                txtPelnyRaport.Text = sb.ToString();
                await Task.Delay(100);

                var sw2 = Stopwatch.StartNew();
                var salda2 = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
                sw2.Stop();

                sb.AppendLine($"║   Czas: {sw2.ElapsedMilliseconds,6} ms                      ║");

                var status2 = sw2.ElapsedMilliseconds < 10 ? "SUPER! (cache)" :
                              sw2.ElapsedMilliseconds < 100 ? "OK (cache)" : "PROBLEM!";
                sb.AppendLine($"║   Status: {status2,-20}       ║");
                sb.AppendLine("║                                        ║");
                txtPelnyRaport.Text = sb.ToString();
                await Task.Delay(100);

                // Test 3: Dokumenty
                if (salda1.Count > 0)
                {
                    var kontrahent = salda1[0];
                    sb.AppendLine($"║ Test 3: Dokumenty ({kontrahent.Kontrahent})     ║");
                    txtPelnyRaport.Text = sb.ToString();
                    await Task.Delay(100);

                    var sw3 = Stopwatch.StartNew();
                    var docs = await service.PobierzDokumentyAsync(kontrahent.Id, DateTime.Today.AddMonths(-3), DateTime.Today);
                    sw3.Stop();

                    sb.AppendLine($"║   Czas: {sw3.ElapsedMilliseconds,6} ms                      ║");
                    sb.AppendLine($"║   Dokumentów: {docs.Count,-6}                    ║");
                    sb.AppendLine("║                                        ║");
                }

                // Podsumowanie
                sb.AppendLine("╠════════════════════════════════════════╣");
                sb.AppendLine("║          PODSUMOWANIE                  ║");
                sb.AppendLine("╠════════════════════════════════════════╣");

                if (sw2.ElapsedMilliseconds < 10)
                {
                    sb.AppendLine("║ ✓ CACHE DZIAŁA ŚWIETNIE!              ║");
                    sb.AppendLine("║   Dane z pamięci < 10ms               ║");
                }
                else if (sw1.ElapsedMilliseconds < 2000)
                {
                    sb.AppendLine("║ ○ WYDAJNOŚĆ OK                        ║");
                    sb.AppendLine("║   Pierwsze ładowanie akceptowalne     ║");
                }
                else
                {
                    sb.AppendLine("║ ✗ WOLNE ŁADOWANIE!                    ║");
                    sb.AppendLine("║   Sprawdź połączenie z serwerem       ║");
                }

                sb.AppendLine("╚════════════════════════════════════════╝");
                txtPelnyRaport.Text = sb.ToString();

                // Aktualizuj UI
                txtOstatnieCzasLadowania.Text = $"Czas: {sw1.ElapsedMilliseconds} ms";
                txtOstatnieLiczbaRekordow.Text = $"Rekordów: {salda1.Count}";
                txtOstatnieZrodlo.Text = $"Źródło: {_lastLoadSource}";

                RefreshDiagnosticsPanel();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"║ BŁĄD: {ex.Message.Substring(0, Math.Min(30, ex.Message.Length))}... ║");
                sb.AppendLine("╚════════════════════════════════════════╝");
                txtPelnyRaport.Text = sb.ToString();
            }
        }

        /// <summary>
        /// Kopiuje raport do schowka
        /// </summary>
        private void BtnKopiujRaport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var fullReport = new StringBuilder();
                fullReport.AppendLine("=== RAPORT DIAGNOSTYKI WYDAJNOŚCI ===");
                fullReport.AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                fullReport.AppendLine();
                fullReport.AppendLine($"Ostatnie ładowanie: {txtOstatnieCzasLadowania.Text}");
                fullReport.AppendLine($"{txtOstatnieLiczbaRekordow.Text}");
                fullReport.AppendLine($"{txtOstatnieZrodlo.Text}");
                fullReport.AppendLine();
                fullReport.AppendLine(SaldaService.GetCacheStatus());
                fullReport.AppendLine();
                fullReport.AppendLine(PerformanceProfiler.GenerateReport());

                Clipboard.SetText(fullReport.ToString());
                MessageBox.Show("Raport skopiowany do schowka!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Resetuje statystyki
        /// </summary>
        private void BtnResetStats_Click(object sender, RoutedEventArgs e)
        {
            PerformanceProfiler.Reset();
            _lastLoadRecordCount = 0;
            _lastLoadSource = "--";
            _lastLoadStopwatch.Reset();
            RefreshDiagnosticsPanel();
            MessageBox.Show("Statystyki zresetowane!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Nawigacja

        /// <summary>
        /// Wymusza pełne odświeżenie danych (invaliduje cache)
        /// </summary>
        private void BtnForceRefresh_Click(object sender, RoutedEventArgs e)
        {
            _lastLoadSource = "BAZA (force)";
            SaldaService.InvalidateAllCaches();
            _viewModel.OdswiezCommand.Execute(null);
        }

        /// <summary>
        /// Otwiera okno diagnostyki wydajności (zewnętrzne)
        /// </summary>
        private void BtnDiagnostyka_Click(object sender, RoutedEventArgs e)
        {
            var diagWindow = new DiagnostykaWindow();
            diagWindow.Owner = this;
            diagWindow.ShowDialog();
        }

        /// <summary>
        /// Otwiera listę kontrahentów dla E2
        /// </summary>
        private void TileE2_Click(object sender, MouseButtonEventArgs e)
        {
            OtworzListeOpakowania("E2", "Pojemnik Drobiowy E2");
        }

        /// <summary>
        /// Otwiera listę kontrahentów dla H1
        /// </summary>
        private void TileH1_Click(object sender, MouseButtonEventArgs e)
        {
            OtworzListeOpakowania("H1", "Paleta H1");
        }

        /// <summary>
        /// Otwiera listę kontrahentów dla EURO
        /// </summary>
        private void TileEURO_Click(object sender, MouseButtonEventArgs e)
        {
            OtworzListeOpakowania("EURO", "Paleta EURO");
        }

        /// <summary>
        /// Otwiera listę kontrahentów dla PCV
        /// </summary>
        private void TilePCV_Click(object sender, MouseButtonEventArgs e)
        {
            OtworzListeOpakowania("PCV", "Paleta plastikowa");
        }

        /// <summary>
        /// Otwiera listę kontrahentów dla DREW
        /// </summary>
        private void TileDREW_Click(object sender, MouseButtonEventArgs e)
        {
            OtworzListeOpakowania("DREW", "Paleta Drewniana");
        }

        /// <summary>
        /// Otwiera okno listy kontrahentów dla wybranego typu opakowania
        /// </summary>
        private void OtworzListeOpakowania(string kodOpakowania, string nazwaOpakowania)
        {
            var listaWindow = new ListaOpakowanWindow(kodOpakowania, nazwaOpakowania, _viewModel.DataDo, _viewModel.UserId);
            listaWindow.Owner = this;
            listaWindow.ShowDialog();

            // Po zamknięciu odśwież dane
            _viewModel.OdswiezCommand.Execute(null);
        }

        /// <summary>
        /// Dwuklik na wierszu - otwiera szczegóły kontrahenta
        /// </summary>
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.WybranyKontrahent != null)
            {
                var szczegolyWindow = new SzczegolyKontrahentaWindow(
                    _viewModel.WybranyKontrahent,
                    _viewModel.DataDo,
                    _viewModel.UserId);
                szczegolyWindow.Owner = this;
                szczegolyWindow.ShowDialog();

                // Po zamknięciu odśwież dane
                _viewModel.OdswiezCommand.Execute(null);
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _diagnosticsTimer?.Stop();
            _viewModel?.Dispose();
            base.OnClosed(e);
        }
    }
}
