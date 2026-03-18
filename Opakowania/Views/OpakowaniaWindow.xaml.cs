using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    /// Scalone okno opakowan - zastepuje ZestawienieOpakowanWindow i SaldaWszystkichOpakowanWindow
    /// </summary>
    public partial class OpakowaniaWindow : Window
    {
        private readonly OpakowaniaWindowViewModel _viewModel;
        private readonly DispatcherTimer _diagTimer;
        private readonly StringBuilder _logBuilder = new StringBuilder();
        private int _operationCount = 0;
        private long _lastLoadTimeMs = 0;

        public OpakowaniaWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            string userId = App.UserID ?? "11111";

            _viewModel = new OpakowaniaWindowViewModel(userId);
            DataContext = _viewModel;

            // Subskrybuj eventy
            _viewModel.OtworzSzczegolyWszystkieRequested += OnOtworzSzczegolyWszystkie;
            _viewModel.OtworzSzczegolyPerTypRequested += OnOtworzSzczegolyPerTyp;
            _viewModel.DodajPotwierdzenieRequested += OnDodajPotwierdzenie;

            // Subskrybuj diagnostyke z ViewModel
            _viewModel.DiagnostykaDaneZaladowane += OnDiagnostykaDaneZaladowane;

            // Aktualizuj wyglad przyciskow opakowan przy zmianie typu
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.WybranyTypOpakowania))
                    AktualizujWygladPrzyciskowOpakowan();
            };

            // Timer diagnostyki co 2 sekundy
            _diagTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _diagTimer.Tick += (s, e) => RefreshDiagnostics();
            _diagTimer.Start();

            Loaded += (s, e) =>
            {
                RefreshDiagnostics();
                AddLog("Okno uruchomione");
                AddLog($"User: {userId}");
            };

            // ESC zamyka okno
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    Close();
            };
        }

        #region View Toggle

        private void RbWszystkieTypy_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && _viewModel.ViewMode != ViewMode.WszystkieTypy)
                _viewModel.ViewMode = ViewMode.WszystkieTypy;
        }

        private void RbPerTyp_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && _viewModel.ViewMode != ViewMode.PerTyp)
                _viewModel.ViewMode = ViewMode.PerTyp;
        }

        #endregion

        #region Event Handlers - WszystkieTypy

        private void OnOtworzSzczegolyWszystkie(SaldoOpakowania saldo)
        {
            if (saldo == null || saldo.KontrahentId <= 0) return;

            var okno = new SaldoOdbiorcyWindow(saldo.KontrahentId, saldo.Kontrahent, App.UserID ?? "11111");
            okno.Owner = this;
            var result = okno.ShowDialog();

            if (result == true)
                _viewModel.OdswiezCommand.Execute(null);
        }

        private void DataGridSalda_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGridSalda.SelectedItem is SaldoOpakowania saldo)
                OnOtworzSzczegolyWszystkie(saldo);
        }

        private void BtnSzczegolyWszystkie_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SaldoOpakowania saldo)
                OnOtworzSzczegolyWszystkie(saldo);
        }

        private void MenuSzczegolyWszystkie_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridSalda.SelectedItem is SaldoOpakowania saldo)
                OnOtworzSzczegolyWszystkie(saldo);
        }

        private void MenuZadzwon_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ZadzwonCommand?.Execute(null);
        }

        private void MenuWyslijEmail_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.WyslijEmailCommand?.Execute(null);
        }

        #endregion

        #region Event Handlers - PerTyp

        private void OnOtworzSzczegolyPerTyp(ZestawienieSalda kontrahent)
        {
            if (kontrahent == null || kontrahent.KontrahentId <= 0) return;

            var szczegoly = new SaldoOdbiorcyWindow(
                kontrahent.KontrahentId,
                kontrahent.Kontrahent,
                App.UserID ?? "11111");

            szczegoly.Owner = this;
            var result = szczegoly.ShowDialog();

            if (result == true)
                _viewModel.OdswiezCommand.Execute(null);
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
                _viewModel.OdswiezCommand.Execute(null);
        }

        private void DataGridZestawienie_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGridZestawienie.SelectedItem is ZestawienieSalda kontrahent)
                OnOtworzSzczegolyPerTyp(kontrahent);
        }

        private void BtnSzczegolyPerTyp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ZestawienieSalda kontrahent)
                OnOtworzSzczegolyPerTyp(kontrahent);
        }

        private void BtnPotwierdzeniePerTyp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ZestawienieSalda kontrahent)
                OnDodajPotwierdzenie(kontrahent, _viewModel.WybranyTypOpakowania);
        }

        private void MenuSzczegolyPerTyp_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridZestawienie.SelectedItem is ZestawienieSalda kontrahent)
                OnOtworzSzczegolyPerTyp(kontrahent);
        }

        private void MenuDodajPotwierdzenie_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridZestawienie.SelectedItem is ZestawienieSalda kontrahent)
                OnDodajPotwierdzenie(kontrahent, _viewModel.WybranyTypOpakowania);
        }

        #endregion

        #region Shared Menu Handlers

        private void MenuEksportPDF_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.EksportPDFCommand?.Execute(null);
        }

        private void MenuEksportExcel_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.EksportExcelCommand?.Execute(null);
        }

        #endregion

        #region Diagnostyka

        private DiagInfo _lastDiag;

        /// <summary>
        /// Callback z ViewModel - automatycznie loguje kazde ladowanie danych
        /// </summary>
        private void OnDiagnostykaDaneZaladowane(DiagInfo info)
        {
            _lastDiag = info;
            _operationCount++;
            _lastLoadTimeMs = info.CzasCalkowityMs;

            Dispatcher.BeginInvoke(() =>
            {
                // Aktualizuj metryki
                txtAktywnyTryb.Text = info.Tryb == ViewMode.WszystkieTypy ? "Wszystkie typy" : $"Per typ: {info.TypOpakowania}";
                txtLastLoadTime.Text = $"{info.CzasCalkowityMs} ms";
                txtLastLoadTime.Foreground = info.CzasCalkowityMs < 200
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 255, 136))
                    : info.CzasCalkowityMs < 2000
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0))
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 80, 80));

                txtSqlTime.Text = $"{info.CzasZapytaniaMs} ms";
                txtUiTime.Text = $"{info.CzasUiMs} ms";
                txtLiczbaRekordow.Text = info.LiczbaRekordow.ToString();
                txtZrodloDanych.Text = info.ZCache ? "CACHE" : "SQL";
                txtZrodloDanych.Foreground = info.ZCache
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 255, 136))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0));
                txtTotalOperations.Text = _operationCount.ToString();

                // Loguj
                var cacheTag = info.ZCache ? "CACHE" : "SQL";
                var filtrTag = string.IsNullOrEmpty(info.FiltrHandlowca) ? "" : $" filtr={info.FiltrHandlowca}";
                AddLog($"[{info.Tryb}] {info.Operacja}");
                AddLog($"  {info.LiczbaRekordow} rek | {info.CzasCalkowityMs}ms total (SQL:{info.CzasZapytaniaMs}ms + UI:{info.CzasUiMs}ms) [{cacheTag}]{filtrTag}");

                // Analiza co jest waskim gardlem
                if (info.CzasCalkowityMs > 500)
                {
                    double pctSql = info.CzasCalkowityMs > 0 ? (double)info.CzasZapytaniaMs / info.CzasCalkowityMs * 100 : 0;
                    double pctUi = info.CzasCalkowityMs > 0 ? (double)info.CzasUiMs / info.CzasCalkowityMs * 100 : 0;
                    if (pctSql > 70)
                        AddLog($"  >> WASKIE GARDLO: SQL ({pctSql:F0}% czasu)");
                    else if (pctUi > 70)
                        AddLog($"  >> WASKIE GARDLO: UI/Render ({pctUi:F0}% czasu)");
                    else
                        AddLog($"  >> SQL: {pctSql:F0}% | UI: {pctUi:F0}%");
                }
            });
        }

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
                        saldaStatus = line.Contains("PUSTY") ? "Puste" : "OK";
                }

                var profilerReport = PerformanceProfiler.GenerateShortReport();
                var hitRateMatch = System.Text.RegularExpressions.Regex.Match(profilerReport, @"(\d+)% hit rate");
                string hitRateText = hitRateMatch.Success ? hitRateMatch.Groups[1].Value + " %" : "-- %";

                txtCacheStatus.Text = saldaStatus;
                txtCacheHitRate.Text = hitRateText;
                txtTotalOperations.Text = _operationCount.ToString();
                txtLastUpdate.Text = $"Aktualizacja: {DateTime.Now:HH:mm:ss}";

                if (_logBuilder.Length == 0)
                    txtDiagLog.Text = cacheStatus + "\n\n" + profilerReport;
            }
            catch (Exception ex)
            {
                AddLog($"Blad: {ex.Message}");
            }
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logBuilder.AppendLine($"[{timestamp}] {message}");

            if (_logBuilder.Length > 8000)
            {
                var text = _logBuilder.ToString();
                _logBuilder.Clear();
                _logBuilder.Append(text.Substring(text.Length - 5000));
            }

            txtDiagLog.Text = _logBuilder.ToString();
        }

        /// <summary>
        /// Szybki test - testuje aktualny tryb (WszystkieTypy lub PerTyp)
        /// </summary>
        private async void BtnRunTest_Click(object sender, RoutedEventArgs e)
        {
            btnRunTest.IsEnabled = false;
            btnRunTest.Content = "TESTOWANIE...";
            _logBuilder.Clear();

            try
            {
                var aktualnyTryb = _viewModel.ViewMode;
                AddLog($"=== SZYBKI TEST: {aktualnyTryb} ===");
                AddLog("");

                if (aktualnyTryb == ViewMode.WszystkieTypy)
                {
                    await TestWszystkieTypy();
                }
                else
                {
                    await TestPerTyp(_viewModel.WybranyTypOpakowania?.Kod ?? "E2");
                }

                AddLog("");
                AddLog("=== CACHE STATUS ===");
                AddLog(SaldaService.GetCacheStatus());
                AddLog(OpakowaniaDataService.GetZestawieniaCacheStatus());
                AddLog(OpakowaniaDataService.GetWszystkieSaldaCacheStatus());

                RefreshDiagnostics();
            }
            catch (Exception ex)
            {
                AddLog($"BLAD: {ex.Message}");
            }
            finally
            {
                btnRunTest.IsEnabled = true;
                btnRunTest.Content = "SZYBKI TEST";
            }
        }

        /// <summary>
        /// Pelny test - testuje oba tryby i wszystkie typy opakowan
        /// </summary>
        private async void BtnFullTest_Click(object sender, RoutedEventArgs e)
        {
            btnFullTest.IsEnabled = false;
            btnFullTest.Content = "TESTOWANIE...";
            _logBuilder.Clear();

            try
            {
                AddLog("=============================================");
                AddLog("  PELNY TEST WYDAJNOSCI - OBA TRYBY");
                AddLog("=============================================");
                AddLog("");

                // 1. Test WszystkieTypy
                AddLog("--- TRYB: WSZYSTKIE TYPY ---");
                AddLog("");
                await TestWszystkieTypy();
                AddLog("");

                // 2. Test PerTyp - kazdy typ opakowania
                AddLog("--- TRYB: PER TYP ---");
                AddLog("");

                var typy = new[] { "E2", "H1", "EURO", "PCV", "DREW" };
                var wyniki = new List<(string Typ, long Ms1, long Ms2, int Rek)>();

                foreach (var typ in typy)
                {
                    var (ms1, ms2, rek) = await TestPerTyp(typ);
                    wyniki.Add((typ, ms1, ms2, rek));
                    AddLog("");
                }

                // 3. Test dokumentow
                AddLog("--- TEST DOKUMENTOW ---");
                var saldaService = new SaldaService();
                var salda = await saldaService.PobierzWszystkieSaldaAsync(DateTime.Today);
                if (salda.Count > 0)
                {
                    var pierwszy = salda[0];
                    var nazwaSkrot = pierwszy.Kontrahent?.Substring(0, Math.Min(20, pierwszy.Kontrahent?.Length ?? 0));

                    AddLog($"Dokumenty [{nazwaSkrot}]...");
                    var sw = Stopwatch.StartNew();
                    var docs = await saldaService.PobierzDokumentyAsync(pierwszy.Id, DateTime.Today.AddMonths(-3), DateTime.Today);
                    sw.Stop();
                    _operationCount++;
                    var s = sw.ElapsedMilliseconds < 100 ? "OK" : sw.ElapsedMilliseconds < 1000 ? "WOLNE" : "B.WOLNE";
                    AddLog($"  {docs.Count} dok w {sw.ElapsedMilliseconds} ms [{s}]");

                    AddLog($"Potwierdzenia [{nazwaSkrot}]...");
                    var sw2 = Stopwatch.StartNew();
                    var potw = await saldaService.PobierzPotwierdzeniaKontrahentaAsync(pierwszy.Id);
                    sw2.Stop();
                    _operationCount++;
                    AddLog($"  {potw.Count} potw w {sw2.ElapsedMilliseconds} ms");
                }
                AddLog("");

                // 4. Podsumowanie
                AddLog("=============================================");
                AddLog("  PODSUMOWANIE");
                AddLog("=============================================");
                AddLog("");

                AddLog("WszystkieTypy:");
                AddLog($"  Pierwsze ladowanie -> metryki wyzej");
                AddLog("");

                AddLog("PerTyp (pierwsze | ponowne):");
                foreach (var w in wyniki)
                {
                    var s1 = w.Ms1 < 200 ? "OK" : w.Ms1 < 2000 ? "WOLNE" : "B.WOLNE";
                    var s2 = w.Ms2 < 50 ? "CACHE" : w.Ms2 < 200 ? "OK" : "WOLNE";
                    AddLog($"  {w.Typ,-5}: {w.Ms1,5}ms [{s1}] | {w.Ms2,5}ms [{s2}] | {w.Rek} rek");
                }
                AddLog("");

                // Co najbardziej obciaza?
                if (wyniki.Count > 0)
                {
                    var najwolniejszy = wyniki.OrderByDescending(w => w.Ms1).First();
                    var najszybszy = wyniki.OrderBy(w => w.Ms1).First();
                    AddLog($"Najwolniejszy typ: {najwolniejszy.Typ} ({najwolniejszy.Ms1} ms)");
                    AddLog($"Najszybszy typ:    {najszybszy.Typ} ({najszybszy.Ms1} ms)");

                    var bezCache = wyniki.Where(w => w.Ms2 > 50).ToList();
                    if (bezCache.Count > 0)
                        AddLog($"UWAGA: {bezCache.Count} typow nie uzywa cache!");
                    else
                        AddLog("Cache dziala prawidlowo dla wszystkich typow.");
                }

                AddLog("");
                AddLog("=== CACHE STATUS ===");
                AddLog(SaldaService.GetCacheStatus());
                AddLog(OpakowaniaDataService.GetZestawieniaCacheStatus());
                AddLog(OpakowaniaDataService.GetWszystkieSaldaCacheStatus());

                RefreshDiagnostics();
            }
            catch (Exception ex)
            {
                AddLog($"BLAD: {ex.Message}");
            }
            finally
            {
                btnFullTest.IsEnabled = true;
                btnFullTest.Content = "PELNY TEST (oba tryby)";
            }
        }

        private async Task TestWszystkieTypy()
        {
            var service = new SaldaService();

            AddLog("1. Ladowanie sald (WszystkieTypy)...");
            var sw1 = Stopwatch.StartNew();
            var salda = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
            sw1.Stop();
            _operationCount++;
            _lastLoadTimeMs = sw1.ElapsedMilliseconds;

            var s1 = sw1.ElapsedMilliseconds < 100 ? "CACHE" : sw1.ElapsedMilliseconds < 2000 ? "OK" : "WOLNE";
            AddLog($"  {salda.Count} rek w {sw1.ElapsedMilliseconds} ms [{s1}]");

            AddLog("2. Ponowne ladowanie (test cache)...");
            var sw2 = Stopwatch.StartNew();
            await service.PobierzWszystkieSaldaAsync(DateTime.Today);
            sw2.Stop();
            _operationCount++;

            var s2 = sw2.ElapsedMilliseconds < 10 ? "CACHE HIT" : "MISS";
            AddLog($"  {sw2.ElapsedMilliseconds} ms [{s2}]");

            // Analiza danych
            if (salda.Count > 0)
            {
                var zSaldem = salda.Count(s => s.SumaWszystkich > 0);
                var handlowcy = salda.Select(s => s.Handlowiec).Distinct().Count();
                AddLog($"  Kontrahentow z saldem: {zSaldem}/{salda.Count}");
                AddLog($"  Handlowcow: {handlowcy}");
            }
        }

        private async Task<(long Ms1, long Ms2, int Rek)> TestPerTyp(string kodTypu)
        {
            var dataService = new OpakowaniaDataService();
            var typ = TypOpakowania.WszystkieTypy.FirstOrDefault(t => t.Kod == kodTypu);
            if (typ == null) return (0, 0, 0);

            var dataOd = DateTime.Today.AddMonths(-2);
            var dataDo = DateTime.Today;

            AddLog($"PerTyp [{kodTypu}]: Ladowanie zestawienia...");
            var sw1 = Stopwatch.StartNew();
            var dane = await dataService.PobierzZestawienieSaldAsync(dataOd, dataDo, typ.NazwaSystemowa, null);
            sw1.Stop();
            _operationCount++;

            var s1 = sw1.ElapsedMilliseconds < 200 ? "OK" : sw1.ElapsedMilliseconds < 2000 ? "WOLNE" : "B.WOLNE";
            AddLog($"  {dane.Count} rek w {sw1.ElapsedMilliseconds} ms [{s1}]");

            AddLog($"PerTyp [{kodTypu}]: Ponowne (cache)...");
            var sw2 = Stopwatch.StartNew();
            await dataService.PobierzZestawienieSaldAsync(dataOd, dataDo, typ.NazwaSystemowa, null);
            sw2.Stop();
            _operationCount++;

            var s2 = sw2.ElapsedMilliseconds < 50 ? "CACHE" : "MISS";
            AddLog($"  {sw2.ElapsedMilliseconds} ms [{s2}]");

            // Analiza
            if (dane.Count > 0)
            {
                var potw = dane.Count(d => d.JestPotwierdzone);
                var zSaldem = dane.Count(d => d.Kontrahent != "Suma" && d.IloscDrugiZakres != 0);
                AddLog($"  Z saldem: {zSaldem} | Potwierdzone: {potw}");
            }

            return (sw1.ElapsedMilliseconds, sw2.ElapsedMilliseconds, dane.Count);
        }

        private void BtnCopyDiag_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=============================================");
                sb.AppendLine("  RAPORT DIAGNOSTYCZNY - OPAKOWANIA");
                sb.AppendLine($"  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("=============================================");
                sb.AppendLine();

                sb.AppendLine("=== METRYKI ===");
                sb.AppendLine($"Aktywny tryb:      {txtAktywnyTryb.Text}");
                sb.AppendLine($"Czas calkowity:    {txtLastLoadTime.Text}");
                sb.AppendLine($"  SQL/Cache:       {txtSqlTime.Text}");
                sb.AppendLine($"  UI/Render:       {txtUiTime.Text}");
                sb.AppendLine($"Rekordow:          {txtLiczbaRekordow.Text}");
                sb.AppendLine($"Zrodlo:            {txtZrodloDanych.Text}");
                sb.AppendLine($"Cache hit rate:    {txtCacheHitRate.Text}");
                sb.AppendLine($"Operacji:          {txtTotalOperations.Text}");
                sb.AppendLine($"Status cache:      {txtCacheStatus.Text}");
                sb.AppendLine();

                if (_lastDiag != null)
                {
                    sb.AppendLine("=== OSTATNIE LADOWANIE ===");
                    sb.AppendLine($"Tryb:              {_lastDiag.Tryb}");
                    sb.AppendLine($"Operacja:          {_lastDiag.Operacja}");
                    sb.AppendLine($"Czas total:        {_lastDiag.CzasCalkowityMs} ms");
                    sb.AppendLine($"Czas SQL:          {_lastDiag.CzasZapytaniaMs} ms");
                    sb.AppendLine($"Czas UI:           {_lastDiag.CzasUiMs} ms");
                    sb.AppendLine($"Rekordow:          {_lastDiag.LiczbaRekordow}");
                    sb.AppendLine($"Z cache:           {_lastDiag.ZCache}");
                    sb.AppendLine($"Typ opakowania:    {_lastDiag.TypOpakowania ?? "-"}");
                    sb.AppendLine($"Filtr handlowca:   {_lastDiag.FiltrHandlowca ?? "brak"}");
                    sb.AppendLine();
                }

                sb.AppendLine("=== LOG ===");
                sb.AppendLine(txtDiagLog.Text);
                sb.AppendLine();
                sb.AppendLine("=== PROFILER ===");
                sb.AppendLine(PerformanceProfiler.GenerateReport());
                sb.AppendLine();
                sb.AppendLine("=== CACHE SALDA ===");
                sb.AppendLine(SaldaService.GetCacheStatus());
                sb.AppendLine();
                sb.AppendLine("=== CACHE ZESTAWIENIA ===");
                sb.AppendLine(OpakowaniaDataService.GetZestawieniaCacheStatus());
                sb.AppendLine(OpakowaniaDataService.GetWszystkieSaldaCacheStatus());

                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Raport skopiowany do schowka!", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnResetDiag_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Zresetowac statystyki i cache?", "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                PerformanceProfiler.Reset();
                ExtendedDiagnostics.Reset();
                SaldaService.InvalidateAllCaches();
                OpakowaniaDataService.InvalidateZestawieniaCache();
                OpakowaniaDataService.InvalidateWszystkieSaldaCache();

                _logBuilder.Clear();
                _operationCount = 0;
                _lastLoadTimeMs = 0;
                _lastDiag = null;

                txtAktywnyTryb.Text = "--";
                txtSqlTime.Text = "-- ms";
                txtUiTime.Text = "-- ms";
                txtLiczbaRekordow.Text = "0";
                txtZrodloDanych.Text = "--";

                AddLog("Statystyki i cache zresetowane");
                AddLog("Nastepne ladowanie pobierze dane z SQL");
                RefreshDiagnostics();
            }
        }

        #endregion

        #region Window Chrome

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else
                DragMove();
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
            if (btnE2 == null) return;

            var buttons = new[] { btnE2, btnH1, btnEURO, btnPCV, btnDREW };
            foreach (var btn in buttons)
            {
                btn.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
                btn.Background = System.Windows.Media.Brushes.White;
            }

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
            _viewModel.OtworzSzczegolyWszystkieRequested -= OnOtworzSzczegolyWszystkie;
            _viewModel.OtworzSzczegolyPerTypRequested -= OnOtworzSzczegolyPerTyp;
            _viewModel.DodajPotwierdzenieRequested -= OnDodajPotwierdzenie;
            _viewModel.DiagnostykaDaneZaladowane -= OnDiagnostykaDaneZaladowane;
            base.OnClosed(e);
        }
    }
}
