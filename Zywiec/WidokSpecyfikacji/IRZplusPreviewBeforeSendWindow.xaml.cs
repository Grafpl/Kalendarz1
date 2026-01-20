using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Kalendarz1.Services;

namespace Kalendarz1.Zywiec.WidokSpecyfikacji
{
    /// <summary>
    /// Okno podgladu danych przed wyslaniem do API IRZplus.
    /// Pozwala uzytkownikowi zobaczyc dokladnie jakie dane zostana wyslane.
    /// </summary>
    public partial class IRZplusPreviewBeforeSendWindow : Window
    {
        private readonly List<ApiPreviewItem> _previewItems;
        private readonly List<DyspozycjaZURDApi> _dyspozycje;
        private readonly bool _useTestEnvironment;
        private readonly DateTime _dataUboju;

        /// <summary>
        /// Czy uzytkownik potwierdzil wyslanie
        /// </summary>
        public bool ConfirmedSend { get; private set; } = false;

        /// <summary>
        /// Tworzy okno podgladu
        /// </summary>
        /// <param name="previewItems">Lista pozycji do podgladu</param>
        /// <param name="dyspozycje">Lista dyspozycji API do wyslania</param>
        /// <param name="useTestEnvironment">Czy uzywamy srodowiska testowego</param>
        /// <param name="dataUboju">Data uboju</param>
        public IRZplusPreviewBeforeSendWindow(
            List<ApiPreviewItem> previewItems,
            List<DyspozycjaZURDApi> dyspozycje,
            bool useTestEnvironment,
            DateTime dataUboju)
        {
            InitializeComponent();

            _previewItems = previewItems;
            _dyspozycje = dyspozycje;
            _useTestEnvironment = useTestEnvironment;
            _dataUboju = dataUboju;

            LoadData();
        }

        private void LoadData()
        {
            // Ustaw srodowisko
            if (_useTestEnvironment)
            {
                txtEnvironment.Text = "SRODOWISKO TESTOWE";
                borderEnv.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
            }
            else
            {
                txtEnvironment.Text = "PRODUKCJA";
                borderEnv.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F"));
            }

            // Ustaw podsumowanie
            txtDataUboju.Text = _dataUboju.ToString("dd.MM.yyyy");
            txtLiczbaPozycji.Text = _previewItems.Count.ToString();
            txtSumaSztuk.Text = _previewItems.Sum(x => x.LiczbaDrobiu).ToString("N0");
            txtSumaWagi.Text = _previewItems.Sum(x => x.MasaDrobiu).ToString("N2");

            if (_dyspozycje.Any())
            {
                txtNumerRzezni.Text = _dyspozycje.First().Zgloszenie?.NumerRzezni ?? "039806095-001";
                txtGatunek.Text = _dyspozycje.First().Zgloszenie?.Gatunek?.Kod ?? "KURY";
            }

            // Zaladuj dane do tabeli
            dgPodglad.ItemsSource = _previewItems;

            // Wygeneruj JSON
            GenerateJsonPreview();
        }

        private void GenerateJsonPreview()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };

                if (_dyspozycje.Count == 1)
                {
                    // Pojedyncza dyspozycja - pokaz ja bezposrednio
                    txtJson.Text = JsonSerializer.Serialize(_dyspozycje[0], options);
                }
                else
                {
                    // Wiele dyspozycji - pokaz wszystkie
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"// UWAGA: Bedzie wyslanych {_dyspozycje.Count} OSOBNYCH zgloszen!");
                    sb.AppendLine($"// Kazdy dostawca jest wysylany jako oddzielne zgloszenie ZURD.");
                    sb.AppendLine();

                    for (int i = 0; i < _dyspozycje.Count; i++)
                    {
                        sb.AppendLine($"// === ZGLOSZENIE {i + 1}/{_dyspozycje.Count}: {_previewItems[i].Hodowca} ===");
                        sb.AppendLine(JsonSerializer.Serialize(_dyspozycje[i], options));
                        sb.AppendLine();
                    }

                    txtJson.Text = sb.ToString();
                }
            }
            catch (Exception ex)
            {
                txtJson.Text = $"Blad generowania JSON: {ex.Message}";
            }
        }

        private void BtnCopyJson_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(txtJson.Text);
                btnCopyJson.Content = "Skopiowano!";
                btnCopyJson.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                btnCopyJson.Foreground = Brushes.White;

                // Przywroc oryginalny wyglad po 2 sekundach
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, ev) =>
                {
                    timer.Stop();
                    btnCopyJson.Content = "Kopiuj JSON";
                    btnCopyJson.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD"));
                    btnCopyJson.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1976D2"));
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad kopiowania:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            var envText = _useTestEnvironment ? "TESTOWE" : "PRODUKCYJNE";
            var result = MessageBox.Show(
                $"Czy na pewno chcesz wyslac {_dyspozycje.Count} zgloszen ZURD do API?\n\n" +
                $"Srodowisko: {envText}\n" +
                $"Data uboju: {_dataUboju:dd.MM.yyyy}\n" +
                $"Suma sztuk: {_previewItems.Sum(x => x.LiczbaDrobiu):N0}\n" +
                $"Suma wagi: {_previewItems.Sum(x => x.MasaDrobiu):N2} kg\n\n" +
                $"Po wyslaniu dane zostana przeslane do systemu ARiMR.",
                "Potwierdzenie wysylki",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ConfirmedSend = true;
                DialogResult = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            ConfirmedSend = false;
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// Model do wyswietlania podgladu pozycji w tabeli
    /// </summary>
    public class ApiPreviewItem
    {
        public int Lp { get; set; }
        public string Hodowca { get; set; }
        public string NumerIdenPartiiDrobiu { get; set; }
        public int LiczbaDrobiu { get; set; }
        public decimal MasaDrobiu { get; set; }
        public string TypZdarzenia { get; set; }
        public string DataZdarzenia { get; set; }
        public string DataKupnaWwozu { get; set; }
        public string PrzyjeteZDzialalnosci { get; set; }
        public bool UbojRytualny { get; set; }
    }
}
