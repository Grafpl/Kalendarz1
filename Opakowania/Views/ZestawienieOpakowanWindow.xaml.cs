using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    /// <summary>
    /// Główne okno zestawienia opakowań dla wszystkich kontrahentów
    /// </summary>
    public partial class ZestawienieOpakowanWindow : Window
    {
        private readonly ZestawienieOpakowanViewModel _viewModel;
        private readonly DispatcherTimer _diagTimer;
        private readonly StringBuilder _logBuilder = new StringBuilder();
        private int _operationCount = 0;
        private long _lastLoadTimeMs = 0;

        public ZestawienieOpakowanWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            // Pobierz UserID z aplikacji
            string userId = App.UserID ?? "11111"; // Domyślnie admin dla testów

            _viewModel = new ZestawienieOpakowanViewModel(userId);
            DataContext = _viewModel;

            // Subskrybuj eventy
            _viewModel.OtworzSzczegolyRequested += OnOtworzSzczegoly;
            _viewModel.DodajPotwierdzenieRequested += OnDodajPotwierdzenie;

            // Aktualizuj wygląd przycisków przy zmianie typu
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.WybranyTypOpakowania))
                {
                    AktualizujWygladPrzyciskowOpakowan();
                }
            };

            // Timer do odświeżania diagnostyki co 2 sekundy
            _diagTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _diagTimer.Tick += (s, e) => RefreshDiagnostics();
            _diagTimer.Start();

            // Pierwsze odświeżenie
            Loaded += (s, e) =>
            {
                RefreshDiagnostics();
                AddLog("Okno uruchomione");
                AddLog($"User: {userId}");
            };
        }

        #region Diagnostyka

        private void RefreshDiagnostics()
        {
            try
            {
                var cacheStatus = SaldaService.GetCacheStatus();

                string saldaStatus = "--";
                var lines = cacheStatus.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("Salda:"))
                    {
                        saldaStatus = line.Contains("ZAŁADOWANE") ? "OK" : "Puste";
                    }
                }

                var profilerReport = PerformanceProfiler.GenerateShortReport();
                var hitRateMatch = System.Text.RegularExpressions.Regex.Match(profilerReport, @"(\d+)% hit rate");
                string hitRateText = hitRateMatch.Success ? hitRateMatch.Groups[1].Value + " %" : "-- %";

                txtCacheStatus.Text = saldaStatus;
                txtCacheHitRate.Text = hitRateText;
                txtTotalOperations.Text = _operationCount.ToString();
                txtLastLoadTime.Text = _lastLoadTimeMs > 0 ? $"{_lastLoadTimeMs} ms" : "-- ms";
                txtLastUpdate.Text = $"Aktualizacja: {DateTime.Now:HH:mm:ss}";

                if (_logBuilder.Length == 0)
                {
                    txtDiagLog.Text = cacheStatus + "\n\n" + profilerReport;
                }
            }
            catch (Exception ex)
            {
                AddLog($"Błąd: {ex.Message}");
            }
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logBuilder.AppendLine($"[{timestamp}] {message}");

            if (_logBuilder.Length > 5000)
            {
                var text = _logBuilder.ToString();
                _logBuilder.Clear();
                _logBuilder.Append(text.Substring(text.Length - 3000));
            }

            txtDiagLog.Text = _logBuilder.ToString();
        }

        private async void BtnRunTest_Click(object sender, RoutedEventArgs e)
        {
            btnRunTest.IsEnabled = false;
            btnRunTest.Content = "⏳ TESTOWANIE...";
            _logBuilder.Clear();

            try
            {
                AddLog("=== TEST WYDAJNOŚCI ===");
                AddLog("");

                var service = new SaldaService();

                // Test 1: Ładowanie sald
                AddLog("TEST 1: Ładowanie sald...");
                var sw1 = Stopwatch.StartNew();
                var salda = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
                sw1.Stop();
                _lastLoadTimeMs = sw1.ElapsedMilliseconds;
                _operationCount++;

                string status1 = sw1.ElapsedMilliseconds < 100 ? "CACHE!" :
                                sw1.ElapsedMilliseconds < 2000 ? "OK" : "WOLNE";
                AddLog($"  {salda.Count} rekordów w {sw1.ElapsedMilliseconds} ms [{status1}]");
                AddLog("");

                // Test 2: Cache test
                AddLog("TEST 2: Ponowne ładowanie (cache)...");
                var sw2 = Stopwatch.StartNew();
                var salda2 = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
                sw2.Stop();
                _operationCount++;

                string status2 = sw2.ElapsedMilliseconds < 10 ? "CACHE HIT!" : "Wolne";
                AddLog($"  {salda2.Count} rekordów w {sw2.ElapsedMilliseconds} ms [{status2}]");
                AddLog("");

                // Test 3: Dokumenty
                if (salda.Count > 0)
                {
                    var pierwszy = salda[0];
                    AddLog($"TEST 3: Dokumenty [{pierwszy.Kontrahent?.Substring(0, Math.Min(20, pierwszy.Kontrahent?.Length ?? 0))}...]");
                    var sw3 = Stopwatch.StartNew();
                    var docs = await service.PobierzDokumentyAsync(pierwszy.Id, DateTime.Today.AddMonths(-3), DateTime.Today);
                    sw3.Stop();
                    _operationCount++;
                    AddLog($"  {docs.Count} dokumentów w {sw3.ElapsedMilliseconds} ms");
                    AddLog("");

                    // Test 4: Potwierdzenia
                    AddLog("TEST 4: Potwierdzenia...");
                    var sw4 = Stopwatch.StartNew();
                    var potwierdzenia = await service.PobierzPotwierdzeniaKontrahentaAsync(pierwszy.Id);
                    sw4.Stop();
                    _operationCount++;
                    AddLog($"  {potwierdzenia.Count} potwierdzeń w {sw4.ElapsedMilliseconds} ms");
                    AddLog("");
                }

                // Podsumowanie
                AddLog("=== WYNIK ===");
                if (sw1.ElapsedMilliseconds < 100 && sw2.ElapsedMilliseconds < 10)
                {
                    AddLog("CACHE DZIAŁA POPRAWNIE!");
                }
                else if (sw1.ElapsedMilliseconds < 3000)
                {
                    AddLog("Pierwsze ładowanie OK.");
                    AddLog("Kolejne z cache.");
                }
                else
                {
                    AddLog("WOLNE - sprawdź serwer.");
                }

                AddLog("");
                AddLog("=== CACHE STATUS ===");
                AddLog(SaldaService.GetCacheStatus());

                RefreshDiagnostics();
            }
            catch (Exception ex)
            {
                AddLog($"BŁĄD: {ex.Message}");
            }
            finally
            {
                btnRunTest.IsEnabled = true;
                btnRunTest.Content = "🚀 TEST WYDAJNOŚCI";
            }
        }

        private void BtnCopyDiag_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("╔════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║         RAPORT DIAGNOSTYCZNY - OPAKOWANIA                  ║");
                sb.AppendLine($"║         {DateTime.Now:yyyy-MM-dd HH:mm:ss}                            ║");
                sb.AppendLine("╚════════════════════════════════════════════════════════════╝");
                sb.AppendLine();
                sb.AppendLine("=== METRYKI ===");
                sb.AppendLine($"Ostatnie ładowanie: {txtLastLoadTime.Text}");
                sb.AppendLine($"Cache hit rate: {txtCacheHitRate.Text}");
                sb.AppendLine($"Operacji: {txtTotalOperations.Text}");
                sb.AppendLine($"Status cache: {txtCacheStatus.Text}");
                sb.AppendLine();
                sb.AppendLine("=== LOG ===");
                sb.AppendLine(txtDiagLog.Text);
                sb.AppendLine();
                sb.AppendLine("=== PROFILER ===");
                sb.AppendLine(PerformanceProfiler.GenerateReport());
                sb.AppendLine();
                sb.AppendLine("=== CACHE ===");
                sb.AppendLine(SaldaService.GetCacheStatus());

                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Raport skopiowany!", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnResetDiag_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Zresetować statystyki?", "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                PerformanceProfiler.Reset();
                ExtendedDiagnostics.Reset();
                _logBuilder.Clear();
                _operationCount = 0;
                _lastLoadTimeMs = 0;
                AddLog("Statystyki zresetowane");
                RefreshDiagnostics();
            }
        }

        private async void BtnFullDiag_Click(object sender, RoutedEventArgs e)
        {
            btnFullDiag.IsEnabled = false;
            btnFullDiag.Content = "⏳ Generowanie...";
            _logBuilder.Clear();
            AddLog("Generowanie pełnej diagnostyki...");
            AddLog("To może potrwać 10-30 sekund...");

            try
            {
                var report = await ExtendedDiagnostics.GenerateFullReportAsync();

                Clipboard.SetText(report);
                AddLog("GOTOWE!");
                AddLog("Raport skopiowany do schowka.");
                AddLog("Wklej go do rozmowy.");

                MessageBox.Show(
                    "Pełny raport diagnostyczny został skopiowany do schowka!\n\n" +
                    "Wklej go teraz do rozmowy (Ctrl+V).",
                    "Diagnostyka gotowa",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"BŁĄD: {ex.Message}");
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnFullDiag.IsEnabled = true;
                btnFullDiag.Content = "🔬 PEŁNA DIAGNOSTYKA";
            }
        }

        private async void BtnFullTest_Click(object sender, RoutedEventArgs e)
        {
            btnFullTest.IsEnabled = false;
            btnFullTest.Content = "⏳ Testowanie...";
            _logBuilder.Clear();
            AddLog("Uruchamianie pełnego testu wydajności...");
            AddLog("To może potrwać 30-60 sekund...");

            try
            {
                var report = await ExtendedDiagnostics.RunFullPerformanceTestAsync();

                // Dodaj też pełną diagnostykę
                var fullDiag = await ExtendedDiagnostics.GenerateFullReportAsync();

                var combined = report + "\n\n" + fullDiag;

                Clipboard.SetText(combined);
                AddLog("GOTOWE!");
                AddLog("Pełny raport skopiowany.");

                MessageBox.Show(
                    "Pełny test wydajności zakończony!\n\n" +
                    "Raport został skopiowany do schowka.\n" +
                    "Wklej go teraz do rozmowy (Ctrl+V).",
                    "Test zakończony",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"BŁĄD: {ex.Message}");
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnFullTest.IsEnabled = true;
                btnFullTest.Content = "🧪 PEŁNY TEST";
            }
        }

        #endregion

        #region Event Handlers

        private void OnOtworzSzczegoly(ZestawienieSalda kontrahent)
        {
            if (kontrahent == null || kontrahent.KontrahentId <= 0) return;

            var szczegoly = new SaldoOdbiorcyWindow(
                kontrahent.KontrahentId,
                kontrahent.Kontrahent,
                App.UserID ?? "11111");

            szczegoly.Owner = this;
            var result = szczegoly.ShowDialog();

            // Odśwież tylko jeśli coś zmieniono (DialogResult = true)
            // Nie odświeżaj po samym przeglądaniu szczegółów
            if (result == true)
            {
                _viewModel.OdswiezCommand.Execute(null);
            }
        }

        private void OnDodajPotwierdzenie(ZestawienieSalda kontrahent, TypOpakowania typOpakowania)
        {
            if (kontrahent == null || kontrahent.KontrahentId <= 0) return;

            var okno = new DodajPotwierdzenieWindow(
                kontrahent.KontrahentId,
                kontrahent.Kontrahent,
                kontrahent.Kontrahent,
                typOpakowania,
                kontrahent.IloscDrugiZakres,
                App.UserID ?? "11111");

            okno.Owner = this;
            if (okno.ShowDialog() == true)
            {
                _viewModel.OdswiezCommand.Execute(null);
            }
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.WybranyKontrahent != null)
            {
                OnOtworzSzczegoly(_viewModel.WybranyKontrahent);
            }
        }

        private void DataGridZestawienie_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGridZestawienie.SelectedItem is ZestawienieSalda kontrahent)
            {
                OnOtworzSzczegoly(kontrahent);
            }
        }

        private void BtnSzczegoly_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ZestawienieSalda kontrahent)
            {
                OnOtworzSzczegoly(kontrahent);
            }
        }

        private void BtnPotwierdzenie_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ZestawienieSalda kontrahent)
            {
                OnDodajPotwierdzenie(kontrahent, _viewModel.WybranyTypOpakowania);
            }
        }

        private void MenuSzczegoly_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridZestawienie.SelectedItem is ZestawienieSalda kontrahent)
            {
                OnOtworzSzczegoly(kontrahent);
            }
        }

        private void MenuDodajPotwierdzenie_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridZestawienie.SelectedItem is ZestawienieSalda kontrahent)
            {
                OnDodajPotwierdzenie(kontrahent, _viewModel.WybranyTypOpakowania);
            }
        }

        private void MenuZadzwon_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridZestawienie.SelectedItem is ZestawienieSalda kontrahent)
            {
                MessageBox.Show($"Dzwonienie do: {kontrahent.Kontrahent}", "Zadzwoń", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MenuWyslijEmail_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridZestawienie.SelectedItem is ZestawienieSalda kontrahent)
            {
                MessageBox.Show($"Wysyłanie emaila do: {kontrahent.Kontrahent}", "Email", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MenuEksportExcel_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funkcja eksportu do Excel zostanie wkrótce zaimplementowana.", "Eksport Excel", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuEksportPDF_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.WybranyKontrahent != null)
            {
                _viewModel.GenerujPDFCommand?.Execute(null);
            }
            else
            {
                MessageBox.Show("Wybierz kontrahenta, aby wygenerować PDF.", "Eksport PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Window Chrome

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region UI Helpers

        private void AktualizujWygladPrzyciskowOpakowan()
        {
            // Reset wszystkich przycisków
            var buttons = new[] { btnE2, btnH1, btnEURO, btnPCV, btnDREW };
            foreach (var btn in buttons)
            {
                btn.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
                btn.Background = System.Windows.Media.Brushes.White;
            }

            // Podświetl wybrany
            var wybranyKod = _viewModel.WybranyTypOpakowania?.Kod;
            System.Windows.Controls.Button wybranyBtn = wybranyKod switch
            {
                "E2" => btnE2,
                "H1" => btnH1,
                "EURO" => btnEURO,
                "PCV" => btnPCV,
                "DREW" => btnDREW,
                _ => null
            };

            if (wybranyBtn != null)
            {
                wybranyBtn.BorderBrush = (System.Windows.Media.Brush)FindResource("PrimaryGreenBrush");
                wybranyBtn.Background = (System.Windows.Media.Brush)FindResource("PrimaryGreenLightBrush");
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _diagTimer?.Stop();
            _viewModel.OtworzSzczegolyRequested -= OnOtworzSzczegoly;
            _viewModel.DodajPotwierdzenieRequested -= OnDodajPotwierdzenie;
            base.OnClosed(e);
        }
    }
}
