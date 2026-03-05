using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Kalendarz1.Partie.Models;
using Kalendarz1.Partie.Services;
using Kalendarz1.Partie.Windows;

namespace Kalendarz1.Partie.Views
{
    public partial class ProdukcjaDzisWidok : UserControl
    {
        private readonly PartiaService _service;
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _clockTimer;
        private List<PartiaModel> _dzisPartie = new();
        private List<HarmonogramItem> _harmonogram = new();

        public ProdukcjaDzisWidok()
        {
            InitializeComponent();
            _service = new PartiaService();

            TxtDzisData.Text = DateTime.Today.ToString("dddd, yyyy-MM-dd");
            TxtCzas.Text = DateTime.Now.ToString("HH:mm:ss");

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => TxtCzas.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _refreshTimer.Tick += async (s, e) => await LoadDataAsync(silent: true);

            this.PreviewKeyDown += OnPreviewKeyDown;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
            _refreshTimer.Start();
        }

        private async System.Threading.Tasks.Task LoadDataAsync(bool silent = false)
        {
            try
            {
                if (!silent) LoadingOverlay.Visibility = Visibility.Visible;

                var partieTask = _service.GetPartieDzisAsync();
                var harmTask = _service.GetDzisHarmonogramAsync();

                await System.Threading.Tasks.Task.WhenAll(partieTask, harmTask);

                _dzisPartie = partieTask.Result;
                _harmonogram = harmTask.Result;

                UpdateCards();
                UpdateStats();
                UpdateHarmonogram();
            }
            catch (Exception ex)
            {
                if (!silent)
                    MessageBox.Show($"Blad ladowania:\n{ex.Message}", "Blad",
                        MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!silent) LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateCards()
        {
            var active = _dzisPartie.Where(p => p.IsActive).OrderByDescending(p => p.CreateGodzina).ToList();
            var closed = _dzisPartie.Where(p => !p.IsActive).OrderByDescending(p => p.CloseGodzina).ToList();

            CardsActive.ItemsSource = active;
            CardsClosed.ItemsSource = closed;
        }

        private void UpdateStats()
        {
            int total = _dzisPartie.Count;
            int open = _dzisPartie.Count(p => p.IsActive);
            decimal totalKg = _dzisPartie.Sum(p => p.WydanoKg);

            var wydList = _dzisPartie.Where(p => p.WydajnoscProc.HasValue).ToList();
            decimal avgWyd = wydList.Any() ? wydList.Average(p => p.WydajnoscProc.Value) : 0;

            var tempList = _dzisPartie.Where(p => p.TempRampa.HasValue).ToList();
            decimal avgTemp = tempList.Any() ? tempList.Average(p => p.TempRampa.Value) : 0;

            TxtStatPartii.Text = total.ToString();
            TxtStatOtwartych.Text = open.ToString();
            TxtStatKg.Text = $"{totalKg:N0}";
            TxtStatWydajnosc.Text = avgWyd > 0 ? $"{avgWyd:N1}%" : "-";
            TxtStatTemp.Text = avgTemp != 0 ? $"{avgTemp:N1} C" : "-";
            TxtStatHarmonogram.Text = _harmonogram.Count.ToString();

            TxtFooter.Text = $"Partii dzis: {total} | Wydano: {totalKg:N0} kg | Ostatnie odsw.: {DateTime.Now:HH:mm:ss}";
        }

        private void UpdateHarmonogram()
        {
            ListHarmonogram.ItemsSource = _harmonogram;
        }

        // ═══════════════════════════════════════════════════════════════
        // EVENTS
        // ═══════════════════════════════════════════════════════════════

        private void Card_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is PartiaModel partia)
            {
                // Switch to WidokPartie tab / open detail
                var parent = Window.GetWindow(this);
                if (parent is ListaPartiiWindow listWin)
                {
                    // Navigate to WidokPartie with this partia selected
                    // For now show a detail summary
                }

                // Show quick action menu
                var menu = new ContextMenu();
                if (partia.IsActive)
                {
                    var miZamknij = new MenuItem { Header = "Zamknij partie" };
                    miZamknij.Click += async (s, ev) =>
                    {
                        var dialog = new ZamknijPartieDialog(partia);
                        dialog.Owner = Window.GetWindow(this);
                        if (dialog.ShowDialog() == true)
                            await LoadDataAsync();
                    };
                    menu.Items.Add(miZamknij);
                }
                else
                {
                    var miOtworz = new MenuItem { Header = "Otworz ponownie" };
                    miOtworz.Click += async (s, ev) =>
                    {
                        var dialog = new OtworzPartieDialog(partia);
                        dialog.Owner = Window.GetWindow(this);
                        if (dialog.ShowDialog() == true)
                            await LoadDataAsync();
                    };
                    menu.Items.Add(miOtworz);
                }

                border.ContextMenu = menu;
                menu.IsOpen = true;
            }
        }

        private async void Harmonogram_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is HarmonogramItem harm)
            {
                if (harm.MaPartie)
                {
                    MessageBox.Show($"Pozycja harmonogramu Lp={harm.Lp} ({harm.Dostawca}) ma juz przypisana partie.",
                        "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Open NowaPartiaDialog pre-filled from harmonogram
                var dialog = new NowaPartiaDialog(harm);
                dialog.Owner = Window.GetWindow(this);
                if (dialog.ShowDialog() == true)
                    await LoadDataAsync();
            }
        }

        private async void BtnNowaPartia_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NowaPartiaDialog();
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
                await LoadDataAsync();
        }

        private void BtnListaPartii_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to WidokPartie if we're in a tab control, or open new window
            var parent = Window.GetWindow(this);
            if (parent != null)
            {
                // Try to find parent TabControl and switch
                var content = parent.Content;
                if (content is TabControl tc)
                {
                    foreach (TabItem tab in tc.Items)
                    {
                        if (tab.Content is WidokPartie)
                        {
                            tc.SelectedItem = tab;
                            return;
                        }
                    }
                }
            }

            // Fallback: open WidokPartie in same window
            var win = new ListaPartiiWindow();
            win.Show();
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                BtnOdswiez_Click(sender, e);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
            {
                BtnNowaPartia_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
