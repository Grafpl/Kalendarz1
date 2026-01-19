using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using Kalendarz1.Services;
using Kalendarz1.Models.IRZplus;
using Kalendarz1.Zywiec.WidokSpecyfikacji;

// Alias to resolve ambiguity between System.IO.Path and System.Windows.Shapes.Path
using Path = System.IO.Path;

namespace Kalendarz1
{
    public partial class IRZplusPreviewWindow : Window, INotifyPropertyChanged
    {
        private readonly IRZplusService _service;
        private readonly IRZplusApiService _apiService;
        private readonly string _connectionString;
        private readonly DateTime _dataUboju;
        private ObservableCollection<SpecyfikacjaDoIRZplusViewModel> _specyfikacje;
        private bool _hadWarnings = false;
        private bool _confettiShown = false;
        private readonly Random _random = new Random();

        public event PropertyChangedEventHandler PropertyChanged;

        public bool WysylkaZakonczona { get; private set; } = false;
        public string NumerZgloszenia { get; private set; }

        public IRZplusPreviewWindow(string connectionString, DateTime dataUboju)
        {
            InitializeComponent();

            _connectionString = connectionString;
            _dataUboju = dataUboju;
            _service = new IRZplusService();
            _apiService = new IRZplusApiService();
            _specyfikacje = new ObservableCollection<SpecyfikacjaDoIRZplusViewModel>();

            dgSpecyfikacje.ItemsSource = _specyfikacje;

            UpdateEnvironmentDisplay();
            LoadDataAsync();
        }

        private void UpdateEnvironmentDisplay()
        {
            var settings = _service.GetSettings();
            if (settings.UseTestEnvironment)
            {
                txtEnvironment.Text = "SRODOWISKO TESTOWE";
                borderEnv.Background = System.Windows.Media.Brushes.Orange;
            }
            else
            {
                txtEnvironment.Text = "PRODUKCJA";
                borderEnv.Background = System.Windows.Media.Brushes.Green;
            }

            txtDataUboju.Text = _dataUboju.ToString("dd.MM.yyyy");
        }

        private async void LoadDataAsync()
        {
            try
            {
                txtStatus.Text = "Ladowanie danych...";
                progressBar.Visibility = Visibility.Visible;
                progressBar.IsIndeterminate = true;

                // Upewnij się że kolumny IRZplus istnieją w tabeli FarmerCalc
                await _service.EnsureIRZplusColumnsExistAsync(_connectionString);

                var specyfikacje = await _service.GetSpecyfikacjeAsync(_connectionString, _dataUboju);

                _specyfikacje.Clear();
                int kolejnosc = 1;
                foreach (var spec in specyfikacje)
                {
                    var vm = new SpecyfikacjaDoIRZplusViewModel(spec);
                    vm.KolejnoscAuta = kolejnosc++;
                    _specyfikacje.Add(vm);
                }

                UpdateSummary();
                ValidateData();

                txtStatus.Text = $"Zaladowano {_specyfikacje.Count} specyfikacji";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Blad ladowania";
            }
            finally
            {
                progressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateSummary()
        {
            var wybrane = _specyfikacje.Where(s => s.Wybrana).ToList();

            txtLiczbaDostawcow.Text = wybrane.Count.ToString();
            txtSumaSztuk.Text = wybrane.Sum(s => s.LiczbaSztukDrobiu).ToString("N0");
            txtSumaWagi.Text = wybrane.Sum(s => s.WagaNetto).ToString("N2") + " kg";
            txtSumaPadlych.Text = wybrane.Sum(s => s.SztukiPadle).ToString("N0");

            // KG Do zaplaty, Szt Konfiskat, Szt Padlych
            txtSumaKgDoZapl.Text = wybrane.Sum(s => s.KgDoZaplaty).ToString("N0") + " kg";
            var sumaKonfiskat = wybrane.Sum(s => s.KgKonfiskat);
            var sumaPadlych = wybrane.Sum(s => s.KgPadlych);
            txtSumaKgKonfiskat.Text = sumaKonfiskat.ToString("N0") + " szt";
            txtSumaKgPadlych.Text = sumaPadlych.ToString("N0") + " szt";

            // Suma padlych i konfiskat
            txtSumaPadleKonfiskaty.Text = (sumaKonfiskat + sumaPadlych).ToString("N0") + " szt";

            btnSend.IsEnabled = wybrane.Count > 0;
        }

        private void ValidateData()
        {
            var warnings = new List<string>();

            foreach (var spec in _specyfikacje.Where(s => s.Wybrana))
            {
                if (string.IsNullOrWhiteSpace(spec.IRZPlus))
                {
                    warnings.Add($"Brak numeru IRZ PLUS dla: {spec.Hodowca}");
                }

                if (spec.LiczbaSztukDrobiu <= 0)
                {
                    warnings.Add($"Zerowa liczba sztuk zdatnych dla: {spec.Hodowca}");
                }
            }

            if (warnings.Count > 0)
            {
                txtWarnings.Text = string.Join("\n", warnings.Take(10));
                if (warnings.Count > 10)
                    txtWarnings.Text += $"\n... i {warnings.Count - 10} innych ostrzezen";
                borderWarnings.Visibility = Visibility.Visible;
                _hadWarnings = true;
            }
            else
            {
                borderWarnings.Visibility = Visibility.Collapsed;

                // Pokaz konfetti gdy wszystkie wiersze zostaly poprawnie wprowadzone
                // (tylko jesli wczesniej byly ostrzezenia i sa jakies wybrane wiersze)
                if (_hadWarnings && _specyfikacje.Any(s => s.Wybrana) && !_confettiShown)
                {
                    ShowConfetti();
                    txtStatus.Text = "Wszystkie dane wprowadzone poprawnie!";
                }
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var spec in _specyfikacje)
                spec.Wybrana = true;
            UpdateSummary();
            ValidateData();
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var spec in _specyfikacje)
                spec.Wybrana = false;
            UpdateSummary();
            ValidateData();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDataAsync();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new IRZplusSettingsWindow(_service);
            settingsWindow.Owner = this;
            if (settingsWindow.ShowDialog() == true)
            {
                UpdateEnvironmentDisplay();
            }
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            var wybrane = _specyfikacje.Where(s => s.Wybrana).ToList();
            if (wybrane.Count == 0)
            {
                MessageBox.Show("Zaznacz przynajmniej jedna specyfikacje do wyslania.",
                    "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var settings = _service.GetSettings();
            var envText = settings.UseTestEnvironment ? "TESTOWE" : "PRODUKCYJNE";

            var result = MessageBox.Show(
                $"Czy na pewno chcesz wyslac zgloszenie do IRZplus?\n\n" +
                $"Srodowisko: {envText}\n" +
                $"Data uboju: {_dataUboju:dd.MM.yyyy}\n" +
                $"Liczba dostawcow: {wybrane.Count}\n" +
                $"Suma sztuk zdatnych: {wybrane.Sum(s => s.LiczbaSztukDrobiu):N0}\n" +
                $"Suma wagi: {wybrane.Sum(s => s.WagaNetto):N2} kg",
                "Potwierdzenie wysylki",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            await SendToIRZplusAsync(wybrane);
        }

        /// <summary>
        /// Wysyla zgloszenie ZURD bezposrednio przez API IRZplus - KAZDY DOSTAWCA OSOBNO
        /// </summary>
        private async void BtnSendApi_Click(object sender, RoutedEventArgs e)
        {
            var wybrane = _specyfikacje.Where(s => s.Wybrana).ToList();
            if (wybrane.Count == 0)
            {
                MessageBox.Show("Zaznacz przynajmniej jedna specyfikacje.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pobierz dane logowania z ustawien
            var settings = _service.GetSettings();
            if (string.IsNullOrEmpty(settings.Username) || string.IsNullOrEmpty(settings.Password))
            {
                MessageBox.Show("Uzupelnij dane logowania w Ustawieniach (Username i Password).", "Brak danych logowania",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var envText = settings.UseTestEnvironment ? "TESTOWE" : "PRODUKCYJNE";
            var confirmResult = MessageBox.Show(
                $"Czy na pewno chcesz wyslac zgloszenia ZURD przez API?\n\n" +
                $"Srodowisko: {envText}\n" +
                $"Data uboju: {_dataUboju:dd.MM.yyyy}\n" +
                $"Liczba dostawcow: {wybrane.Count}\n" +
                $"Suma sztuk: {wybrane.Sum(s => s.LiczbaSztukDrobiu):N0}\n" +
                $"Suma wagi: {wybrane.Sum(s => s.WagaNetto):N2} kg\n\n" +
                $"UWAGA: Kazdy dostawca zostanie wyslany OSOBNO!",
                "Potwierdzenie wysylki API",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            try
            {
                btnSendApi.IsEnabled = false;
                txtStatus.Text = "Logowanie do API IRZplus...";
                progressBar.Visibility = Visibility.Visible;
                progressBar.IsIndeterminate = false;
                progressBar.Maximum = wybrane.Count;
                progressBar.Value = 0;

                // Ustaw srodowisko
                _apiService.SetTestEnvironment(settings.UseTestEnvironment);

                // Autoryzacja
                var authResult = await _apiService.AuthenticateAsync(settings.Username, settings.Password);
                if (!authResult.Success)
                {
                    ShowApiResultWindow(authResult, null);
                    txtStatus.Text = "Blad logowania";
                    return;
                }

                // Wysylaj KAZDA POZYCJE OSOBNO
                int successCount = 0;
                int errorCount = 0;
                var errors = new List<string>();
                var assignments = new List<(string Hodowca, string NumerDokumentu, int Id)>();

                for (int i = 0; i < wybrane.Count; i++)
                {
                    var spec = wybrane[i];
                    progressBar.Value = i + 1;
                    txtStatus.Text = $"Wysylanie {i + 1}/{wybrane.Count}: {spec.Hodowca}...";

                    // Przygotuj numer partii uboju (format: yyMMddNN gdzie NN = numer sekwencyjny dla kazdego dostawcy)
                    var numerPartii = _dataUboju.ToString("yyMMdd") + (i + 1).ToString("00");

                    // Przygotuj pojedyncza pozycje dla tego dostawcy
                    var pozycje = new List<PozycjaZURDApi>
                    {
                        new PozycjaZURDApi
                        {
                            Lp = 1,
                            NumerIdenPartiiDrobiu = numerPartii,
                            LiczbaDrobiu = spec.LiczbaSztukDrobiu,
                            MasaDrobiu = spec.WagaNetto,
                            TypZdarzenia = new KodValueApi { Kod = "ZURDUR" },
                            DataZdarzenia = spec.DataZdarzenia.ToString("yyyy-MM-dd"),
                            DataKupnaWwozu = spec.DataZdarzenia.ToString("yyyy-MM-dd"),
                            PrzyjeteZDzialalnosci = spec.IRZPlus,
                            UbojRytualny = false
                        }
                    };

                    // Utworz i wyslij dyspozycje
                    var dyspozycja = _apiService.UtworzDyspozycje(numerPartii, pozycje);
                    var result = await _apiService.WyslijZURDAsync(dyspozycja);

                    if (result.Success)
                    {
                        successCount++;
                        var numerDokumentu = result.NumerDokumentu ?? result.NumerZgloszenia ?? "N/A";

                        // Zapisz numer dokumentu do bazy danych
                        await _service.SaveNrDokArimrAsync(_connectionString, spec.Id, numerDokumentu);

                        // Zaktualizuj lokalnie ViewModel
                        spec.NrDokArimr = numerDokumentu;

                        // Dodaj do listy przypisan
                        assignments.Add((spec.Hodowca, numerDokumentu, spec.Id));
                    }
                    else
                    {
                        errorCount++;
                        errors.Add($"{spec.Hodowca}: {result.Message}");

                        // Pokaz blad ale kontynuuj wysylanie pozostalych
                        var continueResult = MessageBox.Show(
                            $"Blad wysylania dla: {spec.Hodowca}\n\n{result.Message}\n\nCzy kontynuowac wysylanie pozostalych?",
                            "Blad wysylania",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (continueResult != MessageBoxResult.Yes)
                            break;
                    }

                    // Krotka pauza miedzy requestami
                    await Task.Delay(500);
                }

                // Podsumowanie
                WysylkaZakonczona = successCount > 0;
                if (assignments.Any())
                {
                    NumerZgloszenia = assignments.Last().NumerDokumentu;
                }

                var summary = $"Wyslano: {successCount}/{wybrane.Count}";
                if (errorCount > 0)
                {
                    summary += $"\nBledy: {errorCount}";
                }
                txtStatus.Text = summary;

                // Pokaz podsumowanie z przypisaniami
                ShowAssignmentsSummary(assignments, errors, successCount, wybrane.Count);
            }
            catch (Exception ex)
            {
                var errorResult = new ApiResult
                {
                    Success = false,
                    Message = $"WYJATEK: {ex.Message}\n\n{ex.StackTrace}"
                };
                ShowApiResultWindow(errorResult, null);
                txtStatus.Text = "Wyjatek";
            }
            finally
            {
                btnSendApi.IsEnabled = true;
                progressBar.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Wyswietla szczegolowe okno z wynikiem operacji API
        /// </summary>
        private void ShowApiResultWindow(ApiResult result, string requestJson)
        {
            var window = new Window
            {
                Title = result.Success ? "Zgloszenie wyslane pomyslnie" : "Blad wysylania zgloszenia",
                Width = 800,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"))
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Naglowek z ikonka sukcesu/bledu
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(result.Success
                    ? (Color)ColorConverter.ConvertFromString("#4CAF50")
                    : (Color)ColorConverter.ConvertFromString("#F44336")),
                Padding = new Thickness(15, 10, 15, 10)
            };
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var iconText = new TextBlock
            {
                Text = result.Success ? "[OK]" : "[X]",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var headerText = new TextBlock
            {
                Text = result.Success ? "ZGLOSZENIE PRZYJETE" : "BLAD WYSYLANIA",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(iconText);
            headerPanel.Children.Add(headerText);
            headerBorder.Child = headerPanel;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // TabControl z zakladkami
            var tabControl = new TabControl { Margin = new Thickness(10) };

            // Tab 1: Wynik
            var tab1 = new TabItem { Header = "Wynik" };
            var scroll1 = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var txt1 = new TextBox
            {
                Text = BuildResultText(result),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Padding = new Thickness(10),
                BorderThickness = new Thickness(0),
                Background = Brushes.White
            };
            scroll1.Content = txt1;
            tab1.Content = scroll1;
            tabControl.Items.Add(tab1);

            // Tab 2: Response JSON
            var tab2 = new TabItem { Header = "Response JSON" };
            var scroll2 = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
            var txt2 = new TextBox
            {
                Text = FormatJson(result.ResponseJson ?? "(brak odpowiedzi)"),
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Padding = new Thickness(10),
                BorderThickness = new Thickness(0),
                Background = Brushes.White
            };
            scroll2.Content = txt2;
            tab2.Content = scroll2;
            tabControl.Items.Add(tab2);

            // Tab 3: Request JSON
            var tab3 = new TabItem { Header = "Request JSON" };
            var scroll3 = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
            var txt3 = new TextBox
            {
                Text = FormatJson(requestJson ?? "(brak requestu)"),
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Padding = new Thickness(10),
                BorderThickness = new Thickness(0),
                Background = Brushes.White
            };
            scroll3.Content = txt3;
            tab3.Content = scroll3;
            tabControl.Items.Add(tab3);

            // Tab 4: Bledy i ostrzezenia (jesli sa)
            if (result.Bledy.Any() || result.Ostrzezenia.Any() || result.Komunikaty.Any())
            {
                var tab4 = new TabItem { Header = "Komunikaty" };
                var scroll4 = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                var sb4 = new System.Text.StringBuilder();

                if (result.Bledy.Any())
                {
                    sb4.AppendLine("=== BLEDY WALIDACJI ===");
                    foreach (var b in result.Bledy)
                        sb4.AppendLine($"  [!] {b}");
                    sb4.AppendLine();
                }

                if (result.Ostrzezenia.Any())
                {
                    sb4.AppendLine("=== OSTRZEZENIA ===");
                    foreach (var o in result.Ostrzezenia)
                        sb4.AppendLine($"  [?] {o}");
                    sb4.AppendLine();
                }

                if (result.Komunikaty.Any())
                {
                    sb4.AppendLine("=== KOMUNIKATY ===");
                    foreach (var k in result.Komunikaty)
                        sb4.AppendLine($"  [i] {k}");
                }

                var txt4 = new TextBox
                {
                    Text = sb4.ToString(),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Padding = new Thickness(10),
                    BorderThickness = new Thickness(0),
                    Background = Brushes.White
                };
                scroll4.Content = txt4;
                tab4.Content = scroll4;
                tabControl.Items.Add(tab4);
            }

            Grid.SetRow(tabControl, 1);
            mainGrid.Children.Add(tabControl);

            // Panel przyciskow
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var btnCopy = new Button
            {
                Content = "Kopiuj wszystko",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(5),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1976D2")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCopy.Click += (s, ev) =>
            {
                var all = $"=== WYNIK ===\n{BuildResultText(result)}\n\n" +
                          $"=== RESPONSE JSON ===\n{result.ResponseJson ?? "(brak)"}\n\n" +
                          $"=== REQUEST JSON ===\n{requestJson ?? "(brak)"}";
                System.Windows.Clipboard.SetText(all);
                MessageBox.Show("Skopiowano wszystkie dane do schowka!", "Skopiowano",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            };
            buttonPanel.Children.Add(btnCopy);

            var btnOpenLog = new Button
            {
                Content = "Otworz folder logow",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(5),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnOpenLog.Click += (s, ev) =>
            {
                try
                {
                    var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IRZplus_Logs");
                    if (!Directory.Exists(logDir))
                        Directory.CreateDirectory(logDir);
                    System.Diagnostics.Process.Start("explorer.exe", logDir);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Blad otwierania folderu:\n{ex.Message}", "Blad",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            buttonPanel.Children.Add(btnOpenLog);

            var btnClose = new Button
            {
                Content = "Zamknij",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(5),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#757575")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnClose.Click += (s, ev) => window.Close();
            buttonPanel.Children.Add(btnClose);

            Grid.SetRow(buttonPanel, 2);
            mainGrid.Children.Add(buttonPanel);

            window.Content = mainGrid;
            window.ShowDialog();
        }

        /// <summary>
        /// Buduje tekst wyniku z ApiResult
        /// </summary>
        private string BuildResultText(ApiResult result)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"HTTP Status: {result.HttpStatusCode}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(result.NumerZgloszenia))
                sb.AppendLine($"Numer zgloszenia: {result.NumerZgloszenia}");

            if (!string.IsNullOrEmpty(result.NumerDokumentu))
                sb.AppendLine($"Numer dokumentu: {result.NumerDokumentu}");

            if (!string.IsNullOrEmpty(result.Status))
                sb.AppendLine($"Status: {result.Status}");

            if (!string.IsNullOrEmpty(result.StatusKod))
                sb.AppendLine($"Kod statusu: {result.StatusKod}");

            if (!string.IsNullOrEmpty(result.DataUtworzenia))
                sb.AppendLine($"Data utworzenia: {result.DataUtworzenia}");

            if (!string.IsNullOrEmpty(result.DataModyfikacji))
                sb.AppendLine($"Data modyfikacji: {result.DataModyfikacji}");

            if (result.Podsumowanie != null)
            {
                sb.AppendLine();
                sb.AppendLine("=== PODSUMOWANIE ===");
                sb.AppendLine($"  Zaakceptowanych: {result.Podsumowanie.LiczbaZaakceptowanych}");
                sb.AppendLine($"  Odrzuconych: {result.Podsumowanie.LiczbaOdrzuconych}");
                sb.AppendLine($"  Suma sztuk: {result.Podsumowanie.SumaSztuk}");
                sb.AppendLine($"  Suma masy: {result.Podsumowanie.SumaMasy} kg");
            }

            if (result.Pozycje != null && result.Pozycje.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"=== POZYCJE ({result.Pozycje.Count}) ===");
                foreach (var poz in result.Pozycje.Take(20))
                {
                    sb.AppendLine($"  Lp {poz.Lp}: {poz.Status} - {poz.NumerEwidencyjny}");
                }
                if (result.Pozycje.Count > 20)
                    sb.AppendLine($"  ... i {result.Pozycje.Count - 20} wiecej");
            }

            if (result.Komunikaty.Any())
            {
                sb.AppendLine();
                sb.AppendLine("=== KOMUNIKATY ===");
                foreach (var k in result.Komunikaty)
                    sb.AppendLine($"  {k}");
            }

            if (result.Bledy.Any())
            {
                sb.AppendLine();
                sb.AppendLine("=== BLEDY ===");
                foreach (var b in result.Bledy)
                    sb.AppendLine($"  [!] {b}");
            }

            if (result.Ostrzezenia.Any())
            {
                sb.AppendLine();
                sb.AppendLine("=== OSTRZEZENIA ===");
                foreach (var o in result.Ostrzezenia)
                    sb.AppendLine($"  [?] {o}");
            }

            sb.AppendLine();
            sb.AppendLine("=== PELNA WIADOMOSC ===");
            sb.AppendLine(result.Message ?? "(brak)");

            return sb.ToString();
        }

        /// <summary>
        /// Formatuje JSON dla ladniejszego wyswietlania
        /// </summary>
        private string FormatJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return "(brak danych)";

            try
            {
                using (var doc = System.Text.Json.JsonDocument.Parse(json))
                {
                    return System.Text.Json.JsonSerializer.Serialize(doc.RootElement,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                }
            }
            catch
            {
                return json;
            }
        }

        /// <summary>
        /// Pokazuje ladne okno sukcesu z numerem dokumentu i przyciskiem kopiowania
        /// </summary>
        private void ShowSuccessDialog(ApiResult result, string hodowcaNazwa = null)
        {
            var window = new Window
            {
                Title = "Zgloszenie wyslane pomyslnie",
                Width = 500,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
            };

            var mainStack = new StackPanel { Margin = new Thickness(20) };

            // Naglowek z ikona sukcesu
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            var successIcon = new TextBlock
            {
                Text = "[OK]",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            var headerText = new TextBlock
            {
                Text = "Zgloszenie zostalo przyjete!",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerStack.Children.Add(successIcon);
            headerStack.Children.Add(headerText);
            mainStack.Children.Add(headerStack);

            // Nazwa hodowcy jesli podana
            if (!string.IsNullOrEmpty(hodowcaNazwa))
            {
                var hodowcaText = new TextBlock
                {
                    Text = $"Hodowca: {hodowcaNazwa}",
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 15),
                    Foreground = Brushes.DarkGray
                };
                mainStack.Children.Add(hodowcaText);
            }

            // Panel z numerem dokumentu
            var docPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(240, 248, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 144, 255)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var docStack = new StackPanel();
            var docLabel = new TextBlock
            {
                Text = "Numer dokumentu:",
                FontSize = 12,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 5)
            };
            docStack.Children.Add(docLabel);

            var docValuePanel = new StackPanel { Orientation = Orientation.Horizontal };
            var numerDok = result.NumerDokumentu ?? result.NumerZgloszenia ?? "N/A";
            var docValue = new TextBlock
            {
                Text = numerDok,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 144, 255)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0)
            };
            docValuePanel.Children.Add(docValue);

            var copyButton = new Button
            {
                Content = "Kopiuj",
                Padding = new Thickness(15, 8, 15, 8),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold
            };
            copyButton.Click += (s, ev) =>
            {
                if (!string.IsNullOrEmpty(numerDok) && numerDok != "N/A")
                {
                    System.Windows.Clipboard.SetText(numerDok);
                    copyButton.Content = "Skopiowano!";
                    copyButton.Background = new SolidColorBrush(Color.FromRgb(56, 142, 60));
                }
            };
            docValuePanel.Children.Add(copyButton);
            docStack.Children.Add(docValuePanel);
            docPanel.Child = docStack;
            mainStack.Children.Add(docPanel);

            // Dodatkowe informacje
            var infoStack = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };

            if (!string.IsNullOrEmpty(result.Status))
            {
                infoStack.Children.Add(new TextBlock
                {
                    Text = $"Status: {result.Status}",
                    Margin = new Thickness(0, 3, 0, 3)
                });
            }
            if (!string.IsNullOrEmpty(result.DataUtworzenia))
            {
                infoStack.Children.Add(new TextBlock
                {
                    Text = $"Data utworzenia: {result.DataUtworzenia}",
                    Margin = new Thickness(0, 3, 0, 3)
                });
            }
            if (result.Podsumowanie != null)
            {
                infoStack.Children.Add(new TextBlock
                {
                    Text = $"Zaakceptowanych: {result.Podsumowanie.LiczbaZaakceptowanych}, Sztuk: {result.Podsumowanie.SumaSztuk}, Masa: {result.Podsumowanie.SumaMasy} kg",
                    Margin = new Thickness(0, 3, 0, 3)
                });
            }
            mainStack.Children.Add(infoStack);

            // Przycisk OK
            var closeButton = new Button
            {
                Content = "OK",
                Width = 120,
                Padding = new Thickness(10, 10, 10, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                BorderThickness = new Thickness(0)
            };
            closeButton.Click += (s, ev) => window.Close();
            mainStack.Children.Add(closeButton);

            window.Content = mainStack;
            window.ShowDialog();
        }

        private async Task SendToIRZplusAsync(List<SpecyfikacjaDoIRZplusViewModel> wybrane)
        {
            try
            {
                btnSend.IsEnabled = false;
                txtStatus.Text = "Wysylanie do IRZplus...";
                progressBar.Visibility = Visibility.Visible;
                progressBar.IsIndeterminate = false;
                progressBar.Maximum = wybrane.Count;
                progressBar.Value = 0;

                // Wysylaj KAZDA POZYCJE OSOBNO - sekwencyjnie
                int successCount = 0;
                int errorCount = 0;
                var errors = new List<string>();
                var assignments = new List<(string Hodowca, string NumerDokumentu, int Id)>();

                string userId = App.UserID ?? Environment.UserName;
                string userName = userId;
                try
                {
                    var nazwaZiD = new NazwaZiD();
                    userName = nazwaZiD.GetNameById(userId) ?? userId;
                }
                catch { }

                for (int i = 0; i < wybrane.Count; i++)
                {
                    var vm = wybrane[i];
                    progressBar.Value = i + 1;
                    txtStatus.Text = $"Wysylanie {i + 1}/{wybrane.Count}: {vm.Hodowca}...";

                    // Konwertuj pojedyncza specyfikacje
                    var spec = new SpecyfikacjaDoIRZplus
                    {
                        Id = vm.Id,
                        Hodowca = vm.Hodowca,
                        IdHodowcy = vm.IdHodowcy,
                        IRZPlus = vm.IRZPlus,
                        NumerPartii = vm.NumerPartii,
                        LiczbaSztukDrobiu = vm.LiczbaSztukDrobiu,
                        TypZdarzenia = vm.TypZdarzenia,
                        DataZdarzenia = vm.DataZdarzenia,
                        KrajWywozu = vm.KrajWywozu,
                        NrDokArimr = vm.NrDokArimr,
                        Przybycie = vm.Przybycie,
                        Padniecia = vm.Padniecia,
                        SztukiWszystkie = vm.SztukiWszystkie,
                        SztukiPadle = vm.SztukiPadle,
                        SztukiKonfiskaty = vm.SztukiKonfiskaty,
                        WagaNetto = vm.WagaNetto,
                        Wybrana = true
                    };

                    // Utworz zgloszenie dla pojedynczego dostawcy
                    var zgloszenie = _service.ConvertToZgloszenie(new List<SpecyfikacjaDoIRZplus> { spec });
                    var sendResult = await _service.SendZgloszenieAsync(zgloszenie);

                    // Logowanie do bazy
                    await _service.LogToDatabase(_connectionString, zgloszenie, sendResult, userId, userName);

                    if (sendResult.Success)
                    {
                        successCount++;
                        var numerDokumentu = sendResult.NumerZgloszenia ?? "N/A";

                        // Zapisz numer dokumentu do bazy danych
                        await _service.SaveNrDokArimrAsync(_connectionString, vm.Id, numerDokumentu);

                        // Zaktualizuj lokalnie ViewModel
                        vm.NrDokArimr = numerDokumentu;

                        // Dodaj do listy przypisan
                        assignments.Add((vm.Hodowca, numerDokumentu, vm.Id));
                    }
                    else
                    {
                        errorCount++;
                        errors.Add($"{vm.Hodowca}: {sendResult.Message}");

                        // Pokaz blad ale kontynuuj wysylanie pozostalych
                        var continueResult = MessageBox.Show(
                            $"Blad wysylania dla: {vm.Hodowca}\n\n{sendResult.Message}\n\nCzy kontynuowac wysylanie pozostalych?",
                            "Blad wysylania",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (continueResult != MessageBoxResult.Yes)
                            break;
                    }

                    // Krotka pauza miedzy requestami
                    await Task.Delay(500);
                }

                // Podsumowanie
                WysylkaZakonczona = successCount > 0;
                if (assignments.Any())
                {
                    NumerZgloszenia = assignments.Last().NumerDokumentu;
                }

                var summary = $"Wyslano: {successCount}/{wybrane.Count}";
                if (errorCount > 0)
                {
                    summary += $"\nBledy: {errorCount}";
                }
                txtStatus.Text = summary;

                // Pokaz podsumowanie z przypisaniami
                ShowAssignmentsSummary(assignments, errors, successCount, wybrane.Count);

                if (successCount == wybrane.Count)
                {
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystapil nieoczekiwany blad:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Blad";
            }
            finally
            {
                btnSend.IsEnabled = true;
                progressBar.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Pokazuje okno podsumowania z lista przypisan numerow dokumentow do hodowcow
        /// </summary>
        private void ShowAssignmentsSummary(List<(string Hodowca, string NumerDokumentu, int Id)> assignments, List<string> errors, int successCount, int totalCount)
        {
            var window = new Window
            {
                Title = "Podsumowanie wysylki IRZplus",
                Width = 700,
                Height = 550,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Naglowek
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(successCount == totalCount
                    ? Color.FromRgb(76, 175, 80)   // zielony - wszystko OK
                    : Color.FromRgb(255, 152, 0)), // pomaranczowy - sa bledy
                Padding = new Thickness(15, 12, 15, 12)
            };

            var headerStack = new StackPanel();
            var headerText = new TextBlock
            {
                Text = successCount == totalCount
                    ? "WYSYLKA ZAKONCZONA POMYSLNIE"
                    : "WYSYLKA ZAKONCZONA Z BLEDAMI",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            headerStack.Children.Add(headerText);

            var statsText = new TextBlock
            {
                Text = $"Wyslano: {successCount} z {totalCount} | Data uboju: {_dataUboju:dd.MM.yyyy}",
                FontSize = 12,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 5, 0, 0)
            };
            headerStack.Children.Add(statsText);

            headerBorder.Child = headerStack;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // TabControl z zakladkami
            var tabControl = new TabControl { Margin = new Thickness(10) };

            // Tab 1: Lista przypisan
            var tab1 = new TabItem { Header = $"Przypisane numery ({assignments.Count})" };
            var scroll1 = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var assignmentsPanel = new StackPanel { Margin = new Thickness(10) };

            if (assignments.Any())
            {
                // Naglowki kolumn
                var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });

                var col1 = new TextBlock { Text = "Lp.", FontWeight = FontWeights.Bold, Foreground = Brushes.Gray };
                var col2 = new TextBlock { Text = "Hodowca", FontWeight = FontWeights.Bold, Foreground = Brushes.Gray };
                var col3 = new TextBlock { Text = "Numer dokumentu ARIMR", FontWeight = FontWeights.Bold, Foreground = Brushes.Gray };
                Grid.SetColumn(col1, 0);
                Grid.SetColumn(col2, 1);
                Grid.SetColumn(col3, 2);
                headerGrid.Children.Add(col1);
                headerGrid.Children.Add(col2);
                headerGrid.Children.Add(col3);
                assignmentsPanel.Children.Add(headerGrid);

                // Separator
                assignmentsPanel.Children.Add(new Border
                {
                    Height = 1,
                    Background = Brushes.LightGray,
                    Margin = new Thickness(0, 0, 0, 10)
                });

                // Lista przypisan
                int lp = 1;
                foreach (var (hodowca, numerDokumentu, id) in assignments)
                {
                    var rowGrid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });

                    var lpText = new TextBlock
                    {
                        Text = $"{lp}.",
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Brushes.Gray
                    };
                    var hodowcaText = new TextBlock
                    {
                        Text = hodowca,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    var numerText = new TextBlock
                    {
                        Text = numerDokumentu,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(30, 144, 255))
                    };

                    Grid.SetColumn(lpText, 0);
                    Grid.SetColumn(hodowcaText, 1);
                    Grid.SetColumn(numerText, 2);
                    rowGrid.Children.Add(lpText);
                    rowGrid.Children.Add(hodowcaText);
                    rowGrid.Children.Add(numerText);

                    assignmentsPanel.Children.Add(rowGrid);
                    lp++;
                }
            }
            else
            {
                assignmentsPanel.Children.Add(new TextBlock
                {
                    Text = "Brak pomyslnie wyslanych zgloszen.",
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Gray
                });
            }

            scroll1.Content = assignmentsPanel;
            tab1.Content = scroll1;
            tabControl.Items.Add(tab1);

            // Tab 2: Bledy (jesli sa)
            if (errors.Any())
            {
                var tab2 = new TabItem { Header = $"Bledy ({errors.Count})" };
                var scroll2 = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                var errorsPanel = new StackPanel { Margin = new Thickness(10) };

                foreach (var error in errors)
                {
                    var errorBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(255, 235, 238)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 0, 0, 5),
                        CornerRadius = new CornerRadius(4)
                    };
                    errorBorder.Child = new TextBlock
                    {
                        Text = error,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28))
                    };
                    errorsPanel.Children.Add(errorBorder);
                }

                scroll2.Content = errorsPanel;
                tab2.Content = scroll2;
                tabControl.Items.Add(tab2);
            }

            Grid.SetRow(tabControl, 1);
            mainGrid.Children.Add(tabControl);

            // Panel przyciskow
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            // Przycisk kopiowania listy
            var btnCopy = new Button
            {
                Content = "Kopiuj liste",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(25, 118, 210)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCopy.Click += (s, ev) =>
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"PODSUMOWANIE WYSYLKI IRZplus - {_dataUboju:dd.MM.yyyy}");
                sb.AppendLine($"Wyslano: {successCount} z {totalCount}");
                sb.AppendLine();
                sb.AppendLine("PRZYPISANE NUMERY DOKUMENTOW:");
                sb.AppendLine("----------------------------");
                int lp = 1;
                foreach (var (hodowca, numerDokumentu, id) in assignments)
                {
                    sb.AppendLine($"{lp}. {hodowca} -> {numerDokumentu}");
                    lp++;
                }
                if (errors.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("BLEDY:");
                    foreach (var error in errors)
                        sb.AppendLine($"  - {error}");
                }
                System.Windows.Clipboard.SetText(sb.ToString());
                btnCopy.Content = "Skopiowano!";
                btnCopy.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            };
            buttonPanel.Children.Add(btnCopy);

            // Przycisk zamkniecia
            var btnClose = new Button
            {
                Content = "Zamknij",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(117, 117, 117)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnClose.Click += (s, ev) => window.Close();
            buttonPanel.Children.Add(btnClose);

            Grid.SetRow(buttonPanel, 2);
            mainGrid.Children.Add(buttonPanel);

            window.Content = mainGrid;
            window.ShowDialog();
        }

        private void BtnSaveLocal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wybrane = _specyfikacje.Where(s => s.Wybrana).ToList();
                if (wybrane.Count == 0)
                {
                    MessageBox.Show("Zaznacz przynajmniej jedna specyfikacje.",
                        "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var specyfikacje = wybrane.Select(vm => new SpecyfikacjaDoIRZplus
                {
                    Id = vm.Id,
                    Hodowca = vm.Hodowca,
                    IdHodowcy = vm.IdHodowcy,
                    IRZPlus = vm.IRZPlus,
                    NumerPartii = vm.NumerPartii,
                    LiczbaSztukDrobiu = vm.LiczbaSztukDrobiu,
                    TypZdarzenia = vm.TypZdarzenia,
                    DataZdarzenia = vm.DataZdarzenia,
                    KrajWywozu = vm.KrajWywozu,
                    SztukiWszystkie = vm.SztukiWszystkie,
                    SztukiPadle = vm.SztukiPadle,
                    SztukiKonfiskaty = vm.SztukiKonfiskaty,
                    WagaNetto = vm.WagaNetto,
                    Wybrana = true
                }).ToList();

                var zgloszenie = _service.ConvertToZgloszenie(specyfikacje);

                var settings = _service.GetSettings();
                var dir = settings.LocalExportPath;
                if (string.IsNullOrEmpty(dir))
                    dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IRZplus_Export");

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var fileName = $"IRZplus_{_dataUboju:yyyy-MM-dd}_{DateTime.Now:HHmmss}.json";
                var filePath = Path.Combine(dir, fileName);

                var json = JsonSerializer.Serialize(zgloszenie, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);

                MessageBox.Show($"Zapisano lokalna kopie:\n{filePath}",
                    "Zapisano", MessageBoxButton.OK, MessageBoxImage.Information);

                txtStatus.Text = $"Zapisano: {fileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisu:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Otwiera okno eksportu do pliku XML/CSV - alternatywa gdy API nie dziala
        /// </summary>
        private void BtnExportDialog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wybrane = _specyfikacje.Where(s => s.Wybrana).ToList();
                if (wybrane.Count == 0)
                {
                    MessageBox.Show("Zaznacz przynajmniej jedna specyfikacje do eksportu.",
                        "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Uzyj statycznej metody fabrykujacej - kazdy transport/aut staje sie POZYCJA
                var dialog = IRZplusExportDialog.UtworzZDanych(
                    dataUboju: _dataUboju,
                    transporty: wybrane,
                    mapujNaPozycje: (vm, lp) => new PozycjaZgloszeniaIRZ
                    {
                        Lp = lp,
                        // Numer partii drobiu = numer siedliska hodowcy (np. 038481631-001)
                        // IRZPlus juz zawiera pelny numer - NIE DODAWAC -001!
                        NumerPartiiDrobiu = vm.IRZPlus ?? "",
                        TypZdarzenia = TypZdarzeniaZURD.UbojRzezniczy,
                        LiczbaSztuk = vm.LiczbaSztukDrobiu,
                        MasaKg = vm.WagaNetto,
                        DataZdarzenia = vm.DataZdarzenia,
                        // Przyjete z dzialalnosci = numer siedliska hodowcy (np. 038481631-001)
                        // PrzyjetaZDzialalnosci juz zawiera pelny numer - NIE DODAWAC -001!
                        PrzyjeteZDzialalnosci = vm.PrzyjetaZDzialalnosci,
                        UbojRytualny = false,
                        LiczbaPadlych = vm.SztukiPadle,
                        NumerPartiiWewnetrzny = vm.NumerPartii,
                        Uwagi = vm.Hodowca
                    },
                    numerRzezni: "039806095-001",
                    numerProducenta: "039806095",
                    gatunek: GatunekDrobiu.Kury
                );

                dialog.Owner = this;

                if (dialog.ShowDialog() == true && dialog.Sukces)
                {
                    txtStatus.Text = $"Wyeksportowano do: {Path.GetFileName(dialog.WyeksportowanyPlik)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad otwierania okna eksportu:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Zapisuje numer IRZPlus do tabeli dbo.Dostawcy i aktualizuje wszystkie wiersze z tym dostawca
        /// </summary>
        private void BtnSaveIRZPlus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var row = button?.Tag as SpecyfikacjaDoIRZplusViewModel;

                if (row == null)
                {
                    MessageBox.Show("Nie mozna okreslic wiersza.", "Blad",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string newIRZPlus = row.IRZPlus?.Trim() ?? "";
                string idHodowcy = row.IdHodowcy?.Trim() ?? "";

                if (string.IsNullOrEmpty(idHodowcy))
                {
                    MessageBox.Show("Brak ID hodowcy - nie mozna zapisac.", "Blad",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Zapisz do bazy dbo.Dostawcy
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        UPDATE dbo.Dostawcy
                        SET IRZPlus = @IRZPlus
                        WHERE LTRIM(RTRIM(ID)) = @ID", conn);

                    cmd.Parameters.AddWithValue("@IRZPlus", newIRZPlus);
                    cmd.Parameters.AddWithValue("@ID", idHodowcy);

                    int affected = cmd.ExecuteNonQuery();

                    if (affected == 0)
                    {
                        MessageBox.Show($"Nie znaleziono dostawcy o ID: {idHodowcy}\nSprawdz dane w tabeli Dostawcy.",
                            "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Aktualizuj wszystkie wiersze z tym samym IdHodowcy
                foreach (var spec in _specyfikacje.Where(s => s.IdHodowcy?.Trim() == idHodowcy))
                {
                    spec.IRZPlus = newIRZPlus;
                }

                // Odswiez walidacje
                ValidateData();

                txtStatus.Text = $"Zapisano IRZPlus '{newIRZPlus}' dla hodowcy {row.Hodowca}";

                MessageBox.Show($"Zapisano IRZPlus dla hodowcy:\n\n" +
                    $"Hodowca: {row.Hodowca}\n" +
                    $"ID: {idHodowcy}\n" +
                    $"IRZPlus: {newIRZPlus}\n\n" +
                    $"Zaktualizowano wszystkie wiersze z tym hodowca.",
                    "Zapisano", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisu IRZPlus:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Zapisuje pola IRZ (nr.dok.Arimr, przybycie, padniecia) dla wszystkich specyfikacji do bazy
        /// </summary>
        private async void BtnSaveIRZFields_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtStatus.Text = "Zapisywanie danych IRZ...";
                progressBar.Visibility = Visibility.Visible;
                progressBar.IsIndeterminate = true;

                int savedCount = 0;
                int errorCount = 0;

                foreach (var spec in _specyfikacje)
                {
                    // Zapisz tylko jesli sa jakies dane do zapisu
                    bool hasData = !string.IsNullOrWhiteSpace(spec.NrDokArimr) ||
                                   !string.IsNullOrWhiteSpace(spec.Przybycie) ||
                                   !string.IsNullOrWhiteSpace(spec.Padniecia);

                    if (hasData)
                    {
                        var success = await _service.SaveIRZplusFieldsAsync(
                            _connectionString,
                            spec.Id,
                            spec.NrDokArimr,
                            spec.Przybycie,
                            spec.Padniecia);

                        if (success)
                            savedCount++;
                        else
                            errorCount++;
                    }
                }

                if (errorCount > 0)
                {
                    MessageBox.Show($"Zapisano: {savedCount} rekordow\nBledy: {errorCount} rekordow",
                        "Zapis czesciowy", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtStatus.Text = $"Zapisano {savedCount} rekordow, {errorCount} bledow";
                }
                else if (savedCount > 0)
                {
                    MessageBox.Show($"Pomyslnie zapisano dane IRZ dla {savedCount} rekordow.",
                        "Zapisano", MessageBoxButton.OK, MessageBoxImage.Information);
                    txtStatus.Text = $"Zapisano dane IRZ dla {savedCount} rekordow";
                }
                else
                {
                    MessageBox.Show("Brak danych do zapisania.\nWprowadz wartosci w kolumnach nr.dok.Arimr, przybycie lub padniecia.",
                        "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    txtStatus.Text = "Brak danych do zapisania";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisu danych IRZ:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Blad zapisu";
            }
            finally
            {
                progressBar.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Kopiuje numer partii do schowka
        /// </summary>
        private async void BtnKopiujPartie_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string numerPartii && !string.IsNullOrEmpty(numerPartii))
                {
                    System.Windows.Clipboard.SetText(numerPartii);

                    // Zapisz oryginalne wartosci
                    var originalContent = btn.Content;
                    var originalBackground = btn.Background;
                    var originalForeground = btn.Foreground;

                    // Zmien na "Skopiowano!" z zielonym tlem
                    btn.Content = "OK!";
                    btn.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50"));
                    btn.Foreground = System.Windows.Media.Brushes.White;

                    // Poczekaj 1 sekunde
                    await Task.Delay(1000);

                    // Przywroc oryginalne wartosci
                    btn.Content = originalContent;
                    btn.Background = originalBackground;
                    btn.Foreground = originalForeground;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad kopiowania:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Pokazuje animacje konfetti gdy wszystkie wiersze zostaly poprawnie wprowadzone
        /// </summary>
        private void ShowConfetti()
        {
            if (_confettiShown) return;
            _confettiShown = true;

            var colors = new[]
            {
                Color.FromRgb(255, 107, 107),  // czerwony
                Color.FromRgb(78, 205, 196),   // turkusowy
                Color.FromRgb(255, 230, 109),  // zolty
                Color.FromRgb(170, 111, 255),  // fioletowy
                Color.FromRgb(46, 213, 115),   // zielony
                Color.FromRgb(255, 159, 67),   // pomaranczowy
                Color.FromRgb(116, 185, 255),  // niebieski
                Color.FromRgb(255, 121, 198),  // rozowy
            };

            int confettiCount = 150;
            double windowWidth = ActualWidth > 0 ? ActualWidth : 1200;
            double windowHeight = ActualHeight > 0 ? ActualHeight : 700;

            for (int i = 0; i < confettiCount; i++)
            {
                var color = colors[_random.Next(colors.Length)];
                var shape = CreateConfettiPiece(color);

                double startX = _random.NextDouble() * windowWidth;
                double startY = -20 - _random.NextDouble() * 100;

                Canvas.SetLeft(shape, startX);
                Canvas.SetTop(shape, startY);
                confettiCanvas.Children.Add(shape);

                AnimateConfettiPiece(shape, startX, startY, windowWidth, windowHeight, i * 20);
            }

            // Usun konfetti po 5 sekundach
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                confettiCanvas.Children.Clear();
            };
            timer.Start();
        }

        private Shape CreateConfettiPiece(Color color)
        {
            int shapeType = _random.Next(3);

            if (shapeType == 0)
            {
                // Prostokat
                return new Rectangle
                {
                    Width = 8 + _random.NextDouble() * 6,
                    Height = 12 + _random.NextDouble() * 8,
                    Fill = new SolidColorBrush(color),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new RotateTransform(_random.NextDouble() * 360)
                };
            }
            else if (shapeType == 1)
            {
                // Kolo
                double size = 6 + _random.NextDouble() * 6;
                return new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = new SolidColorBrush(color)
                };
            }
            else
            {
                // Trojkat (jako Polygon)
                double size = 10 + _random.NextDouble() * 6;
                return new Polygon
                {
                    Points = new PointCollection
                    {
                        new Point(size / 2, 0),
                        new Point(size, size),
                        new Point(0, size)
                    },
                    Fill = new SolidColorBrush(color),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new RotateTransform(_random.NextDouble() * 360)
                };
            }
        }

        private void AnimateConfettiPiece(Shape shape, double startX, double startY, double windowWidth, double windowHeight, int delayMs)
        {
            var storyboard = new Storyboard();

            // Animacja Y (spadanie)
            double endY = windowHeight + 50;
            double duration = 2.5 + _random.NextDouble() * 2;
            var fallAnimation = new DoubleAnimation
            {
                From = startY,
                To = endY,
                Duration = TimeSpan.FromSeconds(duration),
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fallAnimation, shape);
            Storyboard.SetTargetProperty(fallAnimation, new PropertyPath("(Canvas.Top)"));
            storyboard.Children.Add(fallAnimation);

            // Animacja X (lekkie wahanie na boki)
            double swayAmount = 30 + _random.NextDouble() * 50;
            double swayDirection = _random.Next(2) == 0 ? 1 : -1;
            var swayAnimation = new DoubleAnimationUsingKeyFrames
            {
                BeginTime = TimeSpan.FromMilliseconds(delayMs)
            };
            swayAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(startX, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            swayAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(startX + swayAmount * swayDirection, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(duration * 0.25))));
            swayAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(startX - swayAmount * swayDirection * 0.5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(duration * 0.5))));
            swayAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(startX + swayAmount * swayDirection * 0.3, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(duration * 0.75))));
            swayAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(startX, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(duration))));
            Storyboard.SetTarget(swayAnimation, shape);
            Storyboard.SetTargetProperty(swayAnimation, new PropertyPath("(Canvas.Left)"));
            storyboard.Children.Add(swayAnimation);

            // Animacja rotacji
            if (shape.RenderTransform is RotateTransform rotateTransform)
            {
                double rotations = 1 + _random.NextDouble() * 3;
                double rotateDirection = _random.Next(2) == 0 ? 1 : -1;
                var rotateAnimation = new DoubleAnimation
                {
                    From = rotateTransform.Angle,
                    To = rotateTransform.Angle + 360 * rotations * rotateDirection,
                    Duration = TimeSpan.FromSeconds(duration),
                    BeginTime = TimeSpan.FromMilliseconds(delayMs)
                };
                Storyboard.SetTarget(rotateAnimation, shape);
                Storyboard.SetTargetProperty(rotateAnimation, new PropertyPath("(Shape.RenderTransform).(RotateTransform.Angle)"));
                storyboard.Children.Add(rotateAnimation);
            }

            // Animacja przezroczystosci (zanikanie pod koniec)
            var fadeAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.5),
                BeginTime = TimeSpan.FromMilliseconds(delayMs) + TimeSpan.FromSeconds(duration - 0.5)
            };
            Storyboard.SetTarget(fadeAnimation, shape);
            Storyboard.SetTargetProperty(fadeAnimation, new PropertyPath("Opacity"));
            storyboard.Children.Add(fadeAnimation);

            storyboard.Begin();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _service?.Dispose();
            _apiService?.Dispose();
        }
    }

    /// <summary>
    /// ViewModel dla specyfikacji - zgodny z formatem Excel ARiMR
    /// </summary>
    public class SpecyfikacjaDoIRZplusViewModel : INotifyPropertyChanged
    {
        private bool _wybrana;
        private string _nrDokArimr;
        private string _przybycie;
        private string _padniecia;
        private string _irzPlus;

        public event PropertyChangedEventHandler PropertyChanged;

        public int Id { get; set; }

        // Kolumna A - Kolejnosc auta (1, 2, 3...)
        public int KolejnoscAuta { get; set; }

        // Kolumna B - Hodowca
        public string Hodowca { get; set; }

        // Kolumna C - Id hodowcy
        public string IdHodowcy { get; set; }

        // Kolumna D - IRZ PLUS (edytowalne z zapisem do bazy)
        public string IRZPlus
        {
            get => _irzPlus;
            set
            {
                if (_irzPlus != value)
                {
                    _irzPlus = value;
                    OnPropertyChanged(nameof(IRZPlus));
                    OnPropertyChanged(nameof(PrzyjetaZDzialalnosci));
                }
            }
        }

        // Kolumna E - Numer Partii
        public string NumerPartii { get; set; }

        // Kolumna F - Liczba Sztuk Drobiu (zdatne)
        public int LiczbaSztukDrobiu { get; set; }

        // Kolumna G - Typ Zdarzenia
        public string TypZdarzenia { get; set; }

        // Kolumna H - Data Zdarzenia
        public DateTime DataZdarzenia { get; set; }

        // Kolumna I - Kraj Wywozu
        public string KrajWywozu { get; set; }

        // Kolumna J - Przyjete z dzialalnosci (numer siedliska hodowcy)
        // IRZPlus juz zawiera pelny numer np. "038481631-001" - NIE DODAWAC -001!
        public string PrzyjetaZDzialalnosci => IRZPlus ?? "";

        // Kolumna K - nr.dok.Arimr (edytowalne)
        public string NrDokArimr
        {
            get => _nrDokArimr;
            set
            {
                if (_nrDokArimr != value)
                {
                    _nrDokArimr = value;
                    OnPropertyChanged(nameof(NrDokArimr));
                }
            }
        }

        // Kolumna L - przybycie (edytowalne)
        public string Przybycie
        {
            get => _przybycie;
            set
            {
                if (_przybycie != value)
                {
                    _przybycie = value;
                    OnPropertyChanged(nameof(Przybycie));
                }
            }
        }

        // Kolumna M - padniecia (edytowalne)
        public string Padniecia
        {
            get => _padniecia;
            set
            {
                if (_padniecia != value)
                {
                    _padniecia = value;
                    OnPropertyChanged(nameof(Padniecia));
                }
            }
        }

        // Dane pomocnicze
        public int SztukiWszystkie { get; set; }
        public int SztukiPadle { get; set; }
        public int SztukiKonfiskaty { get; set; }
        public decimal WagaNetto { get; set; }

        // KG - Do zaplaty (PayWgt), Konfiskat, Padlych
        public decimal KgDoZaplaty { get; set; }
        public decimal KgKonfiskat { get; set; }
        public decimal KgPadlych { get; set; }

        public bool Wybrana
        {
            get => _wybrana;
            set
            {
                if (_wybrana != value)
                {
                    _wybrana = value;
                    OnPropertyChanged(nameof(Wybrana));
                }
            }
        }

        // Dla kompatybilnosci wstecznej
        public string DostawcaNazwa => Hodowca;
        public string NumerSiedliska => IRZPlus;
        public int IloscSztuk => LiczbaSztukDrobiu;
        public DateTime DataUboju => DataZdarzenia;
        public int IloscPadlych => SztukiPadle;

        public SpecyfikacjaDoIRZplusViewModel(SpecyfikacjaDoIRZplus source)
        {
            Id = source.Id;
            Hodowca = source.Hodowca;
            IdHodowcy = source.IdHodowcy;
            IRZPlus = source.IRZPlus;
            NumerPartii = source.NumerPartii;
            LiczbaSztukDrobiu = source.LiczbaSztukDrobiu;
            TypZdarzenia = source.TypZdarzenia ?? "Przybycie do rzeźni i ubój";
            DataZdarzenia = source.DataZdarzenia;
            KrajWywozu = source.KrajWywozu ?? "PL";
            NrDokArimr = source.NrDokArimr;
            Przybycie = source.Przybycie;
            Padniecia = source.Padniecia;
            SztukiWszystkie = source.SztukiWszystkie;
            SztukiPadle = source.SztukiPadle;
            SztukiKonfiskaty = source.SztukiKonfiskaty;
            WagaNetto = source.WagaNetto;
            KgDoZaplaty = source.KgDoZaplaty;
            KgKonfiskat = source.KgKonfiskat;
            KgPadlych = source.KgPadlych;
            Wybrana = source.Wybrana;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
