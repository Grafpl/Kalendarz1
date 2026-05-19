using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.Transport
{
    public partial class TransportZmianyView : UserControl
    {
        private List<TransportZmiana> _allItems = new();

        public TransportZmianyView()
        {
            InitializeComponent();
        }

        private async void View_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto-detect new orders on window load
            TxtStatus.Text = "Automatyczne skanowanie zamowien...";
            var user = App.UserFullName ?? App.UserID ?? "system";
            var detected = await TransportZmianyService.DetectNewOrdersAsync(user);
            if (detected > 0)
                TxtStatus.Text = $"Wykryto {detected} nowych zamowien";

            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                TxtStatus.Text = "Ladowanie...";

                if (RbOczekuje.IsChecked == true)
                    // Tylko dzisiejsze oczekujące — zgodnie z badge count.
                    // Starsze nieakceptowane zmiany nie są już pokazywane jako bieżące notyfikacje.
                    _allItems = await TransportZmianyService.GetPendingTodayAsync();
                else if (RbZaakceptowane.IsChecked == true)
                    _allItems = await TransportZmianyService.GetByStatusAsync("Zaakceptowano");
                else if (RbOdrzucone.IsChecked == true)
                    _allItems = await TransportZmianyService.GetByStatusAsync("Odrzucono");
                else
                    _allItems = await TransportZmianyService.GetAllAsync();

                ListZmiany.ItemsSource = _allItems;

                var pendingOrders = _allItems.Where(z => z.StatusZmiany == "Oczekuje")
                    .Select(z => z.ZamowienieId).Distinct().Count();
                TxtCount.Text = pendingOrders > 0
                    ? $"Oczekujacych: {pendingOrders} zam."
                    : "Brak oczekujacych";

                TxtStatus.Text = $"Zaladowano {_allItems.Count} zmian";

                // Faza 9-D — refresh queue diagnostic
                await RefreshQueueStatusAsync();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Blad: {ex.Message}";
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Faza 9-D — Queue diagnostic + manual consume
        // ════════════════════════════════════════════════════════════════
        private async Task RefreshQueueStatusAsync()
        {
            try
            {
                int n = await TransportZmianyService.GetQueueUnprocessedCountAsync();
                if (n < 0)
                {
                    TxtQueueStatus.Text = "Queue: błąd";
                }
                else if (n == 0)
                {
                    TxtQueueStatus.Text = "Queue: ✓ pusta";
                }
                else
                {
                    TxtQueueStatus.Text = $"Queue: ⚠ {n} unprocessed";
                }
            }
            catch
            {
                TxtQueueStatus.Text = "Queue: —";
            }
        }

        private async void BtnConsumeQueueNow_Click(object sender, RoutedEventArgs e)
        {
            BtnConsumeQueueNow.IsEnabled = false;
            TxtStatus.Text = "Konsumpcja kolejki...";
            try
            {
                var user = App.UserFullName ?? App.UserID ?? "system";
                int detected = await TransportZmianyService.ConsumeQueueAsync(user);
                TxtStatus.Text = detected > 0
                    ? $"Wykryto {detected} zmian z kolejki"
                    : "Kolejka pusta — brak nowych zmian";
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Błąd consume: {ex.Message}";
            }
            finally
            {
                BtnConsumeQueueNow.IsEnabled = true;
            }
        }

        private async void Filter_Changed(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async void BtnAccept_Click(object sender, RoutedEventArgs e)
        {
            var selected = ListZmiany.SelectedItems.Cast<TransportZmiana>()
                .Where(z => z.StatusZmiany == "Oczekuje").ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Zaznacz oczekujace zmiany do akceptacji.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var user = App.UserFullName ?? App.UserID ?? "system";
            foreach (var z in selected)
                await TransportZmianyService.AcceptAsync(z.Id, user);

            TxtStatus.Text = $"Zaakceptowano {selected.Count} zmian";
            await LoadDataAsync();
        }

        private async void BtnReject_Click(object sender, RoutedEventArgs e)
        {
            var selected = ListZmiany.SelectedItems.Cast<TransportZmiana>()
                .Where(z => z.StatusZmiany == "Oczekuje").ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Zaznacz oczekujace zmiany do odrzucenia.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Czy na pewno odrzucic {selected.Count} zmian?",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            var user = App.UserFullName ?? App.UserID ?? "system";
            foreach (var z in selected)
                await TransportZmianyService.RejectAsync(z.Id, user);

            TxtStatus.Text = $"Odrzucono {selected.Count} zmian";
            await LoadDataAsync();
        }

        private async void BtnAcceptAll_Click(object sender, RoutedEventArgs e)
        {
            var pendingItems = _allItems.Where(z => z.StatusZmiany == "Oczekuje").ToList();
            var pendingOrders = pendingItems.Select(z => z.ZamowienieId).Distinct().Count();
            if (pendingItems.Count == 0)
            {
                MessageBox.Show("Brak oczekujacych zmian.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Czy na pewno zaakceptowac zmiany dla {pendingOrders} zamowien ({pendingItems.Count} zmian)?",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            var user = App.UserFullName ?? App.UserID ?? "system";
            await TransportZmianyService.AcceptAllAsync(user);

            TxtStatus.Text = $"Zaakceptowano zmiany dla {pendingOrders} zamowien";
            await LoadDataAsync();
        }

        private async void BtnDetect_Click(object sender, RoutedEventArgs e)
        {
            TxtStatus.Text = "Skanowanie nowych zamowien...";
            var user = App.UserFullName ?? App.UserID ?? "system";
            var detected = await TransportZmianyService.DetectNewOrdersAsync(user);

            if (detected > 0)
            {
                TxtStatus.Text = $"Wykryto {detected} nowych zamowien do zaakceptowania";
                await LoadDataAsync();
            }
            else
            {
                TxtStatus.Text = "Nie wykryto nowych zamowien";
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }
    }
}
