using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Kalendarz1.Services;

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
            txtSumaSztuk.Text = wybrane.Sum(s => s.IloscSztuk).ToString("N0");
            txtSumaWagi.Text = wybrane.Sum(s => s.WagaNetto).ToString("N2") + " kg";
            txtSumaPadlych.Text = wybrane.Sum(s => s.IloscPadlych).ToString("N0");

            btnSend.IsEnabled = wybrane.Count > 0;
        }

        private void ValidateData()
        {
            var warnings = new List<string>();

            foreach (var spec in _specyfikacje.Where(s => s.Wybrana))
            {
                if (string.IsNullOrWhiteSpace(spec.NumerSiedliska))
                {
                    warnings.Add($"Brak numeru siedliska dla: {spec.DostawcaNazwa}");
                }

                if (spec.IloscSztuk <= 0)
                {
                    warnings.Add($"Zerowa ilosc sztuk dla: {spec.DostawcaNazwa}");
                }

                if (spec.WagaNetto <= 0)
                {
                    warnings.Add($"Zerowa waga dla: {spec.DostawcaNazwa}");
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
                $"Suma sztuk: {wybrane.Sum(s => s.IloscSztuk):N0}\n" +
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
                    DataUboju = vm.DataUboju,
                    DostawcaNazwa = vm.DostawcaNazwa,
                    NumerSiedliska = vm.NumerSiedliska,
                    GatunekDrobiu = vm.GatunekDrobiu,
                    IloscSztuk = vm.IloscSztuk,
                    WagaNetto = vm.WagaNetto,
                    IloscPadlych = vm.IloscPadlych,
                    NumerPartii = vm.NumerPartii,
                    NumerRejestracyjny = vm.NumerRejestracyjny,
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
                    DataUboju = vm.DataUboju,
                    DostawcaNazwa = vm.DostawcaNazwa,
                    NumerSiedliska = vm.NumerSiedliska,
                    GatunekDrobiu = vm.GatunekDrobiu,
                    IloscSztuk = vm.IloscSztuk,
                    WagaNetto = vm.WagaNetto,
                    IloscPadlych = vm.IloscPadlych,
                    NumerPartii = vm.NumerPartii,
                    NumerRejestracyjny = vm.NumerRejestracyjny,
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

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _service?.Dispose();
        }
    }

    /// <summary>
    /// ViewModel dla specyfikacji z INotifyPropertyChanged
    /// </summary>
    public class SpecyfikacjaDoIRZplusViewModel : INotifyPropertyChanged
    {
        private bool _wybrana;
        private string _numerSiedliska;
        private string _gatunekDrobiu;

        public event PropertyChangedEventHandler PropertyChanged;

        public int Id { get; set; }
        public DateTime DataUboju { get; set; }
        public string DostawcaNazwa { get; set; }
        public int IloscSztuk { get; set; }
        public decimal WagaNetto { get; set; }
        public int IloscPadlych { get; set; }
        public string NumerPartii { get; set; }
        public string NumerRejestracyjny { get; set; }

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

        public string NumerSiedliska
        {
            get => _numerSiedliska;
            set
            {
                if (_numerSiedliska != value)
                {
                    _numerSiedliska = value;
                    OnPropertyChanged(nameof(NumerSiedliska));
                }
            }
        }

        public string GatunekDrobiu
        {
            get => _gatunekDrobiu;
            set
            {
                if (_gatunekDrobiu != value)
                {
                    _gatunekDrobiu = value;
                    OnPropertyChanged(nameof(GatunekDrobiu));
                }
            }
        }

        public SpecyfikacjaDoIRZplusViewModel(SpecyfikacjaDoIRZplus source)
        {
            Id = source.Id;
            DataUboju = source.DataUboju;
            DostawcaNazwa = source.DostawcaNazwa;
            NumerSiedliska = source.NumerSiedliska;
            GatunekDrobiu = source.GatunekDrobiu ?? "KURCZAK";
            IloscSztuk = source.IloscSztuk;
            WagaNetto = source.WagaNetto;
            IloscPadlych = source.IloscPadlych;
            NumerPartii = source.NumerPartii;
            NumerRejestracyjny = source.NumerRejestracyjny;
            Wybrana = source.Wybrana;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
