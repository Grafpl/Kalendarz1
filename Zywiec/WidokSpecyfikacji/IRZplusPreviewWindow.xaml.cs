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
using Microsoft.Data.SqlClient;
using Kalendarz1.Services;
using Kalendarz1.Models.IRZplus;
using Kalendarz1.Zywiec.WidokSpecyfikacji;

namespace Kalendarz1
{
    public partial class IRZplusPreviewWindow : Window, INotifyPropertyChanged
    {
        private readonly IRZplusService _service;
        private readonly string _connectionString;
        private readonly DateTime _dataUboju;
        private ObservableCollection<SpecyfikacjaDoIRZplusViewModel> _specyfikacje;

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

                var specyfikacje = await _service.GetSpecyfikacjeAsync(_connectionString, _dataUboju);

                _specyfikacje.Clear();
                foreach (var spec in specyfikacje)
                {
                    _specyfikacje.Add(new SpecyfikacjaDoIRZplusViewModel(spec));
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

            // KG - Do zaplaty, Konfiskat, Padlych
            txtSumaKgDoZapl.Text = wybrane.Sum(s => s.KgDoZaplaty).ToString("N0") + " kg";
            txtSumaKgKonfiskat.Text = wybrane.Sum(s => s.KgKonfiskat).ToString("N0") + " kg";
            txtSumaKgPadlych.Text = wybrane.Sum(s => s.KgPadlych).ToString("N0") + " kg";

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
            }
            else
            {
                borderWarnings.Visibility = Visibility.Collapsed;
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
                        NumerPartiiDrobiu = string.IsNullOrEmpty(vm.IRZPlus) ? "" : vm.IRZPlus + "-001",
                        TypZdarzenia = TypZdarzeniaZURD.UbojRzezniczy,
                        LiczbaSztuk = vm.LiczbaSztukDrobiu,
                        MasaKg = vm.WagaNetto,
                        DataZdarzenia = vm.DataZdarzenia,
                        // Przyjete z dzialalnosci = numer dzialalnosci hodowcy (np. 038481631-001-001)
                        PrzyjeteZDzialalnosci = vm.PrzyjetaZDzialalnosci + "-001",
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
                    txtStatus.Text = $"Wyeksportowano do: {System.IO.Path.GetFileName(dialog.WyeksportowanyPlik)}";
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

        // Kolumna J - Przyjete z dzialalnosci (obliczane)
        public string PrzyjetaZDzialalnosci => string.IsNullOrEmpty(IRZPlus) ? "" : IRZPlus + "-001";

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
