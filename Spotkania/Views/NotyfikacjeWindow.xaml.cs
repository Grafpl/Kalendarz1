using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Spotkania.Models;
using Kalendarz1.Spotkania.Services;

namespace Kalendarz1.Spotkania.Views
{
    public partial class NotyfikacjeWindow : Window
    {
        private readonly string _userID;
        private readonly NotyfikacjeService _notyfikacjeService;
        private List<NotyfikacjaModel> _notyfikacje = new();

        public NotyfikacjeWindow(string userID)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _userID = userID;
            _notyfikacjeService = NotyfikacjeManager.GetInstance(userID);

            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _notyfikacje = await _notyfikacjeService.PobierzWszystkie(100);
                ListaNotyfikacji.ItemsSource = _notyfikacje;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania powiadomień: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ListaNotyfikacji_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListaNotyfikacji.SelectedItem is NotyfikacjaModel notyfikacja)
            {
                if (!notyfikacja.CzyPrzeczytana)
                {
                    await _notyfikacjeService.OznaczJakoPrzeczytane(notyfikacja.NotyfikacjaID);
                    notyfikacja.CzyPrzeczytana = true;
                }

                // Jeśli ma link do spotkania
                if (!string.IsNullOrEmpty(notyfikacja.LinkSpotkania))
                {
                    var result = MessageBox.Show(
                        $"{notyfikacja.Tresc}\n\nCzy chcesz otworzyć link do spotkania?",
                        notyfikacja.Tytul ?? "Powiadomienie",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(notyfikacja.LinkSpotkania) { UseShellExecute = true });
                        }
                        catch { }
                    }
                }
            }
        }

        private async void BtnOznaczWszystkie_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _notyfikacjeService.OznaczJakoPrzeczytane();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
