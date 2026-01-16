using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    public partial class SaldaMainWindow : Window
    {
        private readonly SaldaMainViewModel _viewModel;
        private readonly DispatcherTimer _diagTimer;
        private readonly StringBuilder _logBuilder = new StringBuilder();
        private int _operationCount = 0;
        private long _lastLoadTimeMs = 0;

        public SaldaMainWindow(string userId)
        {
            InitializeComponent();

            // Dodaj converter
            Resources.Add("BoolToVisibility", new BooleanToVisibilityConverter());

            _viewModel = new SaldaMainViewModel(userId);
            DataContext = _viewModel;

            // Timer do odświeżania diagnostyki co 2 sekundy
            _diagTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _diagTimer.Tick += (s, e) => RefreshDiagnostics();
            _diagTimer.Start();

            // Pierwsze odświeżenie
            Loaded += (s, e) => RefreshDiagnostics();

            AddLog("Okno uruchomione");
            AddLog($"User: {userId}");
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_viewModel.WybranyKontrahent != null)
            {
                var szczegolyWindow = new SaldaSzczegolyWindow(
                    _viewModel.WybranyKontrahent,
                    _viewModel.DataDo,
                    _viewModel.UserId);
                szczegolyWindow.Owner = this;
                var result = szczegolyWindow.ShowDialog();

                // Odśwież tylko jeśli coś zmieniono (DialogResult = true)
                if (result == true)
                {
                    _viewModel.OdswiezCommand.Execute(null);
                }
            }
        }

        private void RefreshDiagnostics()
        {
            try
            {
                // Pobierz status cache
                var cacheStatus = SaldaService.GetCacheStatus();

                // Parsuj status cache aby wyciągnąć metryki
                var lines = cacheStatus.Split('\n');
                int cacheHits = 0, cacheMisses = 0;
                string saldaStatus = "--";

                foreach (var line in lines)
                {
                    if (line.Contains("Salda:"))
                    {
                        saldaStatus = line.Contains("ZAŁADOWANE") ? "OK" : "Puste";
                    }
                }

                // Pobierz raport z profilera
                var profilerReport = PerformanceProfiler.GenerateShortReport();

                // Wyciągnij hit rate z raportu
                var hitRateMatch = System.Text.RegularExpressions.Regex.Match(profilerReport, @"(\d+)% hit rate");
                string hitRateText = hitRateMatch.Success ? hitRateMatch.Groups[1].Value + " %" : "-- %";

                // Policz operacje
                var opCountMatch = System.Text.RegularExpressions.Regex.Match(profilerReport, @"Wywołań: (\d+)");

                // Aktualizuj UI
                txtCacheStatus.Text = saldaStatus;
                txtCacheHitRate.Text = hitRateText;
                txtTotalOperations.Text = _operationCount.ToString();
                txtLastLoadTime.Text = _lastLoadTimeMs > 0 ? $"{_lastLoadTimeMs} ms" : "-- ms";
                txtLastUpdate.Text = $"Aktualizacja: {DateTime.Now:HH:mm:ss}";

                // Dodaj do logu tylko jeśli są nowe dane
                if (_logBuilder.Length == 0)
                {
                    txtDiagLog.Text = cacheStatus + "\n\n" + profilerReport;
                }
            }
            catch (Exception ex)
            {
                AddLog($"Błąd diagnostyki: {ex.Message}");
            }
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logBuilder.AppendLine($"[{timestamp}] {message}");

            // Ogranicz rozmiar logu
            if (_logBuilder.Length > 5000)
            {
                var text = _logBuilder.ToString();
                _logBuilder.Clear();
                _logBuilder.Append(text.Substring(text.Length - 3000));
            }

            txtDiagLog.Text = _logBuilder.ToString();
            txtDiagLog.ScrollToEnd();
        }

        private async void BtnRunTest_Click(object sender, RoutedEventArgs e)
        {
            btnRunTest.IsEnabled = false;
            btnRunTest.Content = "TESTOWANIE...";
            _logBuilder.Clear();

            try
            {
                AddLog("=== ROZPOCZĘCIE TESTU WYDAJNOŚCI ===");
                AddLog("");

                var service = new SaldaService();

                // Test 1: Ładowanie sald
                AddLog("TEST 1: Ładowanie wszystkich sald...");
                var sw1 = Stopwatch.StartNew();
                var salda = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
                sw1.Stop();
                _lastLoadTimeMs = sw1.ElapsedMilliseconds;
                _operationCount++;

                string status1 = sw1.ElapsedMilliseconds < 100 ? "CACHE HIT!" :
                                sw1.ElapsedMilliseconds < 2000 ? "OK" : "WOLNE!";
                AddLog($"  Wynik: {salda.Count} rekordów");
                AddLog($"  Czas: {sw1.ElapsedMilliseconds} ms [{status1}]");
                AddLog("");

                // Test 2: Ponowne ładowanie (cache)
                AddLog("TEST 2: Ponowne ładowanie (test cache)...");
                var sw2 = Stopwatch.StartNew();
                var salda2 = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
                sw2.Stop();
                _operationCount++;

                string status2 = sw2.ElapsedMilliseconds < 10 ? "CACHE HIT!" : "Wolne - sprawdź cache";
                AddLog($"  Wynik: {salda2.Count} rekordów");
                AddLog($"  Czas: {sw2.ElapsedMilliseconds} ms [{status2}]");
                AddLog("");

                // Test 3: Dokumenty (jeśli są salda)
                if (salda.Count > 0)
                {
                    var pierwszy = salda[0];
                    AddLog($"TEST 3: Dokumenty dla {pierwszy.Kontrahent}...");
                    var sw3 = Stopwatch.StartNew();
                    var docs = await service.PobierzDokumentyAsync(pierwszy.Id, DateTime.Today.AddMonths(-3), DateTime.Today);
                    sw3.Stop();
                    _operationCount++;

                    AddLog($"  Wynik: {docs.Count} dokumentów");
                    AddLog($"  Czas: {sw3.ElapsedMilliseconds} ms");
                    AddLog("");

                    // Test 4: Potwierdzenia kontrahenta
                    AddLog($"TEST 4: Potwierdzenia dla {pierwszy.Kontrahent}...");
                    var sw4 = Stopwatch.StartNew();
                    var potwierdzenia = await service.PobierzPotwierdzeniaKontrahentaAsync(pierwszy.Id);
                    sw4.Stop();
                    _operationCount++;

                    AddLog($"  Wynik: {potwierdzenia.Count} potwierdzeń");
                    AddLog($"  Czas: {sw4.ElapsedMilliseconds} ms");
                    AddLog("");
                }

                // Podsumowanie
                AddLog("=== PODSUMOWANIE ===");
                if (sw1.ElapsedMilliseconds < 100 && sw2.ElapsedMilliseconds < 10)
                {
                    AddLog("CACHE DZIAŁA POPRAWNIE!");
                    AddLog("Dane są serwowane z pamięci.");
                }
                else if (sw1.ElapsedMilliseconds < 3000)
                {
                    AddLog("Pierwsze ładowanie OK.");
                    AddLog("Kolejne będą z cache.");
                }
                else
                {
                    AddLog("WOLNE ŁADOWANIE!");
                    AddLog("Sprawdź połączenie z serwerem.");
                }

                // Dodaj status cache
                AddLog("");
                AddLog("=== STATUS CACHE ===");
                AddLog(SaldaService.GetCacheStatus());

                // Odśwież metryki
                RefreshDiagnostics();
            }
            catch (Exception ex)
            {
                AddLog($"BŁĄD: {ex.Message}");
                AddLog(ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0)) ?? "");
            }
            finally
            {
                btnRunTest.IsEnabled = true;
                btnRunTest.Content = "TEST WYDAJNOŚCI";
            }
        }

        private void BtnCopyDiag_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("╔════════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║       RAPORT DIAGNOSTYCZNY - MODUŁ OPAKOWAŃ                    ║");
                sb.AppendLine($"║       Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss}                    ║");
                sb.AppendLine("╚════════════════════════════════════════════════════════════════╝");
                sb.AppendLine();
                sb.AppendLine("=== METRYKI ===");
                sb.AppendLine($"Ostatnie ładowanie: {txtLastLoadTime.Text}");
                sb.AppendLine($"Cache hit rate: {txtCacheHitRate.Text}");
                sb.AppendLine($"Operacji łącznie: {txtTotalOperations.Text}");
                sb.AppendLine($"Status cache: {txtCacheStatus.Text}");
                sb.AppendLine();
                sb.AppendLine("=== LOG OPERACJI ===");
                sb.AppendLine(txtDiagLog.Text);
                sb.AppendLine();
                sb.AppendLine("=== SZCZEGÓŁOWY RAPORT PROFILERA ===");
                sb.AppendLine(PerformanceProfiler.GenerateReport());
                sb.AppendLine();
                sb.AppendLine("=== STATUS CACHE ===");
                sb.AppendLine(SaldaService.GetCacheStatus());

                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Raport skopiowany do schowka!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd kopiowania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnResetDiag_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Czy na pewno chcesz zresetować statystyki diagnostyczne?",
                "Potwierdzenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                PerformanceProfiler.Reset();
                _logBuilder.Clear();
                _operationCount = 0;
                _lastLoadTimeMs = 0;

                AddLog("Statystyki zresetowane");
                RefreshDiagnostics();
                MessageBox.Show("Statystyki zostały zresetowane.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _diagTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
