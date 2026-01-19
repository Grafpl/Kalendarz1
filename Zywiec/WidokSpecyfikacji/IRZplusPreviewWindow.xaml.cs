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

        private async Task SendToIRZplusAsync(List<SpecyfikacjaDoIRZplusViewModel> wybrane)
        {
            try
            {
                btnSend.IsEnabled = false;
                txtStatus.Text = "Wysylanie do IRZplus...";
                progressBar.Visibility = Visibility.Visible;
                progressBar.IsIndeterminate = true;

                // Konwertuj na model
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
                    NrDokArimr = vm.NrDokArimr,
                    Przybycie = vm.Przybycie,
                    Padniecia = vm.Padniecia,
                    SztukiWszystkie = vm.SztukiWszystkie,
                    SztukiPadle = vm.SztukiPadle,
                    SztukiKonfiskaty = vm.SztukiKonfiskaty,
                    WagaNetto = vm.WagaNetto,
                    Wybrana = true
                }).ToList();

                var zgloszenie = _service.ConvertToZgloszenie(specyfikacje);
                var sendResult = await _service.SendZgloszenieAsync(zgloszenie);

                // Logowanie do bazy
                string userId = App.UserID ?? Environment.UserName;
                string userName = userId;
                try
                {
                    var nazwaZiD = new NazwaZiD();
                    userName = nazwaZiD.GetNameById(userId) ?? userId;
                }
                catch { }

                await _service.LogToDatabase(_connectionString, zgloszenie, sendResult, userId, userName);

                if (sendResult.Success)
                {
                    WysylkaZakonczona = true;
                    NumerZgloszenia = sendResult.NumerZgloszenia;

                    var message = $"Zgloszenie zostalo wyslane pomyslnie!\n\n";
                    if (!string.IsNullOrEmpty(sendResult.NumerZgloszenia))
                        message += $"Numer zgloszenia: {sendResult.NumerZgloszenia}\n";

                    if (sendResult.Warnings.Count > 0)
                    {
                        message += $"\nOstrzezenia:\n{string.Join("\n", sendResult.Warnings)}";
                    }

                    MessageBox.Show(message, "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    txtStatus.Text = $"Wyslano pomyslnie: {sendResult.NumerZgloszenia}";

                    DialogResult = true;
                    Close();
                }
                else
                {
                    var errorMessage = $"Blad wysylania:\n{sendResult.Message}";
                    if (sendResult.Errors.Count > 0)
                    {
                        errorMessage += $"\n\nSzczegoly:\n{string.Join("\n", sendResult.Errors)}";
                    }

                    MessageBox.Show(errorMessage, "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtStatus.Text = "Blad wysylania";
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
