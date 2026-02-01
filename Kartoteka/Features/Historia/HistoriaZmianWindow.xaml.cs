using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Kartoteka.Models;

namespace Kalendarz1.Kartoteka.Features.Historia
{
    public partial class HistoriaZmianWindow : Window
    {
        private readonly HistoriaZmianService _service;
        private readonly int _klientId;
        private readonly string _nazwaKlienta;
        private readonly string _userId;
        private readonly string _userName;

        public HistoriaZmianWindow(string connLibra, int klientId, string nazwaKlienta,
            string userId, string userName)
        {
            InitializeComponent();

            _service = new HistoriaZmianService(connLibra);
            _klientId = klientId;
            _nazwaKlienta = nazwaKlienta;
            _userId = userId;
            _userName = userName;

            txtNaglowek.Text = $"📜 Historia zmian — {nazwaKlienta}";
            txtPodtytul.Text = $"ID klienta: {klientId}";

            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                await _service.EnsureTableExistsAsync();

                // Załaduj użytkowników do filtra
                var uzytkownicy = await _service.PobierzUzytkownikowAsync(_klientId);
                cmbUzytkownik.Items.Clear();
                cmbUzytkownik.Items.Add(new ComboBoxItem { Content = "Wszyscy", IsSelected = true });
                foreach (var u in uzytkownicy)
                    cmbUzytkownik.Items.Add(new ComboBoxItem { Content = u });

                await LoadHistoriaAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania historii:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadHistoriaAsync()
        {
            DateTime? odDaty = dpOd.SelectedDate;
            DateTime? doDaty = dpDo.SelectedDate;
            string uzytkownik = null;

            if (cmbUzytkownik.SelectedItem is ComboBoxItem item && item.Content?.ToString() != "Wszyscy")
                uzytkownik = item.Content?.ToString();

            var historia = await _service.PobierzHistorieKlientaAsync(_klientId, odDaty, doDaty, uzytkownik);

            // Zamień nazwy pól na przyjazne
            foreach (var h in historia)
            {
                if (!string.IsNullOrEmpty(h.PoleNazwa))
                    h.PoleNazwa = ChangeTracker.PobierzNazwePola(h.PoleNazwa);
            }

            dgHistoria.ItemsSource = historia;
            txtStatus.Text = $"Znaleziono {historia.Count} zmian";
        }

        private async void BtnFiltruj_Click(object sender, RoutedEventArgs e)
        {
            await LoadHistoriaAsync();
        }

        private void BtnResetuj_Click(object sender, RoutedEventArgs e)
        {
            dpOd.SelectedDate = null;
            dpDo.SelectedDate = null;
            if (cmbUzytkownik.Items.Count > 0)
                cmbUzytkownik.SelectedIndex = 0;

            _ = LoadHistoriaAsync();
        }

        private async void BtnCofnij_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is HistoriaZmiany zmiana)
            {
                if (zmiana.CzyCofniete)
                {
                    MessageBox.Show("Ta zmiana została już cofnięta.", "Informacja",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Czy na pewno chcesz cofnąć tę zmianę?\n\n" +
                    $"Pole: {zmiana.PoleNazwa}\n" +
                    $"Wartość zostanie przywrócona z: \"{zmiana.NowaWartosc}\" → \"{zmiana.StaraWartosc}\"\n\n" +
                    $"Zmiana z: {zmiana.DataZmiany:dd.MM.yyyy HH:mm} przez {zmiana.UzytkownikNazwa}",
                    "Potwierdź cofnięcie zmiany",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        bool success = await _service.CofnijZmianeAsync(zmiana.Id, _userName);
                        if (success)
                        {
                            MessageBox.Show("Zmiana została cofnięta.", "Sukces",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            await LoadHistoriaAsync();
                        }
                        else
                        {
                            MessageBox.Show("Nie udało się cofnąć zmiany.", "Błąd",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd cofania zmiany:\n{ex.Message}", "Błąd",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
