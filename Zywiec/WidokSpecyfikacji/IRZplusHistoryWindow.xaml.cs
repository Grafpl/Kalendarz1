using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.Services;

namespace Kalendarz1
{
    public partial class IRZplusHistoryWindow : Window
    {
        private readonly IRZplusService _service;
        private List<IRZplusLocalHistory> _allHistory;

        public IRZplusHistoryWindow()
        {
            InitializeComponent();

            _service = new IRZplusService();
            _allHistory = new List<IRZplusLocalHistory>();

            // Domyslnie ostatnie 30 dni
            dateFrom.SelectedDate = DateTime.Now.AddDays(-30);
            dateTo.SelectedDate = DateTime.Now;

            LoadHistory();
        }

        private void LoadHistory()
        {
            try
            {
                txtStatus.Text = "Ladowanie...";

                _allHistory = _service.GetLocalHistory(dateFrom.SelectedDate, dateTo.SelectedDate);

                dgHistory.ItemsSource = _allHistory;
                txtRecordCount.Text = $"{_allHistory.Count} rekordow";

                txtStatus.Text = "Gotowe";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania historii:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Blad";
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadHistory();
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            LoadHistory();
        }

        private void BtnToday_Click(object sender, RoutedEventArgs e)
        {
            dateFrom.SelectedDate = DateTime.Today;
            dateTo.SelectedDate = DateTime.Today;
            LoadHistory();
        }

        private void BtnThisWeek_Click(object sender, RoutedEventArgs e)
        {
            var today = DateTime.Today;
            var dayOfWeek = (int)today.DayOfWeek;
            var monday = today.AddDays(-(dayOfWeek == 0 ? 6 : dayOfWeek - 1));

            dateFrom.SelectedDate = monday;
            dateTo.SelectedDate = today;
            LoadHistory();
        }

        private void BtnThisMonth_Click(object sender, RoutedEventArgs e)
        {
            var today = DateTime.Today;
            dateFrom.SelectedDate = new DateTime(today.Year, today.Month, 1);
            dateTo.SelectedDate = today;
            LoadHistory();
        }

        private void BtnAll_Click(object sender, RoutedEventArgs e)
        {
            dateFrom.SelectedDate = null;
            dateTo.SelectedDate = null;
            LoadHistory();
        }

        private async void BtnCheckStatus_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgHistory.SelectedItem as IRZplusLocalHistory;
            if (selected == null)
            {
                MessageBox.Show("Zaznacz zgloszenie do sprawdzenia statusu.",
                    "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(selected.NumerZgloszenia) || selected.NumerZgloszenia == "N/A")
            {
                MessageBox.Show("To zgloszenie nie ma przypisanego numeru (prawdopodobnie nie zostalo wyslane pomyslnie).",
                    "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                txtStatus.Text = "Sprawdzanie statusu...";

                var result = await _service.GetStatusZgloszeniaAsync(selected.NumerZgloszenia);

                if (result.Success)
                {
                    MessageBox.Show($"Numer zgloszenia: {selected.NumerZgloszenia}\n\nStatus: {result.Message}",
                        "Status zgloszenia", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Blad pobierania statusu:\n{result.Message}",
                        "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                txtStatus.Text = "Gotowe";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Blad";
            }
        }

        private void BtnShowDetails_Click(object sender, RoutedEventArgs e)
        {
            ShowSelectedDetails();
        }

        private void DgHistory_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ShowSelectedDetails();
        }

        private void ShowSelectedDetails()
        {
            var selected = dgHistory.SelectedItem as IRZplusLocalHistory;
            if (selected == null)
            {
                MessageBox.Show("Zaznacz zgloszenie do wyswietlenia szczegolow.",
                    "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string details = $"=== SZCZEGOLY ZGLOSZENIA ===\n\n" +
                    $"Data wyslania: {selected.DataWyslania:dd.MM.yyyy HH:mm:ss}\n" +
                    $"Numer zgloszenia: {selected.NumerZgloszenia}\n" +
                    $"Status: {selected.Status}\n" +
                    $"Data uboju: {selected.DataUboju:dd.MM.yyyy}\n" +
                    $"Liczba dostawcow: {selected.IloscDyspozycji}\n" +
                    $"Suma sztuk: {selected.SumaIloscSztuk:N0}\n" +
                    $"Suma wagi: {selected.SumaWagaKg:N2} kg\n" +
                    $"Uzytkownik: {selected.UzytkownikNazwa} ({selected.UzytkownikId})\n" +
                    $"Uwagi: {selected.Uwagi}\n\n";

                if (!string.IsNullOrEmpty(selected.RequestJson))
                {
                    try
                    {
                        var request = JsonSerializer.Deserialize<ZgloszenieZbiorczeRequest>(selected.RequestJson,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (request?.Dyspozycje != null && request.Dyspozycje.Count > 0)
                        {
                            details += "=== DOSTAWCY ===\n\n";
                            int idx = 1;
                            foreach (var d in request.Dyspozycje)
                            {
                                details += $"{idx}. Nr siedliska: {d.NumerSiedliska}\n" +
                                    $"   Gatunek: {d.GatunekDrobiu}\n" +
                                    $"   Ilosc: {d.IloscSztuk:N0} szt., Waga: {d.WagaKg:N2} kg\n" +
                                    $"   Padle: {d.IloscPadlych}\n\n";
                                idx++;
                            }
                        }
                    }
                    catch { }
                }

                // Wyswietl w oknie dialogowym z mozliwoscia kopiowania
                var detailsWindow = new Window
                {
                    Title = $"Szczegoly - {selected.NumerZgloszenia}",
                    Width = 600,
                    Height = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };

                var textBox = new System.Windows.Controls.TextBox
                {
                    Text = details,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    Padding = new Thickness(10)
                };

                detailsWindow.Content = textBox;
                detailsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad wyswietlania szczegolow:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _service?.Dispose();
        }
    }
}
