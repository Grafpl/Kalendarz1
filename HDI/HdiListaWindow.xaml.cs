using Kalendarz1.HDI.Models;
using Kalendarz1.HDI.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Kalendarz1.HDI
{
    public partial class HdiListaWindow : Window
    {
        private readonly HdiService _service = new();
        private List<HdiListItem> _items = new();

        public HdiListaWindow()
        {
            InitializeComponent();
            try { Kalendarz1.WindowIconHelper.SetIcon(this); } catch { }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            int rok = DateTime.Now.Year % 100;
            CmbRok.Items.Clear();
            CmbRok.Items.Add(new ComboBoxItem { Content = "Wszystkie", Tag = (int?)null });
            for (int i = 0; i < 5; i++)
            {
                int r = rok - i;
                CmbRok.Items.Add(new ComboBoxItem { Content = $"20{r:00}", Tag = (int?)r });
            }
            CmbRok.SelectedIndex = 1; // bieżący rok
            await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                int? rok = (CmbRok.SelectedItem as ComboBoxItem)?.Tag as int?;
                string? search = string.IsNullOrWhiteSpace(TxtSearch.Text) ? null : TxtSearch.Text.Trim();
                _items = await _service.GetListAsync(rok, search);
                GridList.ItemsSource = _items;
                LblCount.Text = $"{_items.Count} HDI";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd ładowania: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();
        private async void CmbRok_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) await LoadAsync();
        }
        private async void TxtSearch_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await LoadAsync();
        }

        private async void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            var win = new HdiEditWindow(null) { Owner = this };
            if (win.ShowDialog() == true) await LoadAsync();
        }

        // Ustawienie numeru początkowego dla bieżącego roku (np. wystawiono ręcznie 411 → next będzie 412)
        private async void BtnSetStartNumber_Click(object sender, RoutedEventArgs e)
        {
            int rok = DateTime.Now.Year % 100;
            try
            {
                int current = await _service.GetStartNumberAsync(rok);
                int nextSuggest = await _service.GetNextNumberAsync(rok);

                string? input = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Podaj NUMER POCZĄTKOWY dla roku 20{rok:00}.\n\n" +
                    $"Aktualny start number: {(current > 0 ? current.ToString() : "(nieustawiony)")}\n" +
                    $"Następny dostępny numer to teraz: {nextSuggest}\n\n" +
                    $"Wpisz numer od którego ma się liczyć NASTĘPNY HDI.\n" +
                    $"Przykład: jeśli ostatnia ręcznie wystawiona była 411, wpisz 412.",
                    $"🔢 Numer początkowy HDI dla roku 20{rok:00}",
                    nextSuggest.ToString());

                if (string.IsNullOrWhiteSpace(input)) return;
                if (!int.TryParse(input.Trim(), out int sn) || sn < 1)
                {
                    MessageBox.Show(this, "Podaj prawidłową liczbę całkowitą > 0.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await _service.SetStartNumberAsync(rok, sn, Kalendarz1.App.UserID);
                int verify = await _service.GetNextNumberAsync(rok);
                MessageBox.Show(this,
                    $"✓ Ustawiono.\n\nNastępny HDI dostanie numer: {verify}/{rok:00}",
                    "Numer początkowy zapisany", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GridList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridList.SelectedItem is HdiListItem it)
            {
                var win = new HdiEditWindow(it.Id) { Owner = this };
                if (win.ShowDialog() == true) await LoadAsync();
            }
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not HdiListItem it) return;
            var win = new HdiEditWindow(it.Id) { Owner = this };
            if (win.ShowDialog() == true) await LoadAsync();
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not HdiListItem it) return;

            // Usuwać HDI może TYLKO user 11111.
            if (Kalendarz1.App.UserID != "11111")
            {
                MessageBox.Show(this,
                    "Tylko administrator (login 11111) może usuwać HDI.\n\nSkontaktuj się z administratorem.",
                    "Brak uprawnień", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // user 11111 — wybór: anuluj (soft) lub usuń fizycznie (hard)
            var choice = MessageBox.Show(this,
                $"HDI {it.NumerPelny} — {it.KlientNazwa}\n\n" +
                $"Klient: {it.KlientNazwa}\nTowar: {it.OpisTowaru}\nWaga: {it.WagaNetto:N0} kg\n\n" +
                $"Co zrobić?\n\n" +
                $"  TAK     = USUŃ FIZYCZNIE z bazy (nieodwracalne)\n" +
                $"  NIE     = Anuluj (Status=ANULOWANY, soft delete)\n" +
                $"  ANULUJ  = Nie rób nic",
                "🛡️ Usunięcie HDI", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

            try
            {
                if (choice == MessageBoxResult.Yes)
                {
                    // Hard delete — drugie potwierdzenie (nieodwracalne)
                    var confirm = MessageBox.Show(this,
                        $"⚠ NIEODWRACALNE!\n\nUsunąć HDI {it.NumerPelny} oraz wszystkie jego partie z bazy?",
                        "Ostatnie potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirm != MessageBoxResult.Yes) return;
                    await _service.HardDeleteAsync(it.Id);
                    await LoadAsync();
                    MessageBox.Show(this, $"✓ HDI {it.NumerPelny} usunięte fizycznie.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (choice == MessageBoxResult.No)
                {
                    await _service.DeleteAsync(it.Id);
                    await LoadAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnPdf_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not HdiListItem it) return;
            try
            {
                var dok = await _service.GetByIdAsync(it.Id);
                if (dok == null) { MessageBox.Show(this, "Nie znaleziono."); return; }
                // Otwórz w nowym inline-preview oknie (zoom, drukuj, zapisz, thumbnails)
                var preview = new HdiPreviewWindow(dok) { Owner = this };
                preview.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd PDF: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
