using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kalendarz1.FakturyPanel.ViewModels;
using Kalendarz1.FakturyPanel.Models;

namespace Kalendarz1.FakturyPanel.Views
{
    /// <summary>
    /// Panel dla fakturzystek - widok zamówień handlowców z historią zmian
    /// </summary>
    public partial class FakturyPanelWindow : Window
    {
        private readonly FakturyPanelViewModel _viewModel;
        private System.Windows.Threading.DispatcherTimer _searchTimer;

        public FakturyPanelWindow()
        {
            InitializeComponent();

            _viewModel = new FakturyPanelViewModel();
            DataContext = _viewModel;

            // Timer do opóźnionego wyszukiwania
            _searchTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchTimer.Tick += SearchTimer_Tick;

            Loaded += FakturyPanelWindow_Loaded;
        }

        private async void FakturyPanelWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _viewModel.InitializeAsync();

                // Ustaw domyślny handlowiec
                if (cbHandlowiec.Items.Count > 0)
                    cbHandlowiec.SelectedIndex = 0;

                // Ustaw focus na pole wyszukiwania
                txtSearch.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Nawigacja tygodnia

        private void BtnPreviousWeek_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.PoprzedniTydzienCommand.Execute(null);
        }

        private void BtnNextWeek_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.NastepnyTydzienCommand.Execute(null);
        }

        private void BtnToday_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.DzisCommand.Execute(null);
        }

        #endregion

        #region Filtry

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Restart timera przy każdej zmianie tekstu
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private async void SearchTimer_Tick(object sender, EventArgs e)
        {
            _searchTimer.Stop();
            await _viewModel.SzukajAsync(txtSearch.Text);
        }

        private async void CbHandlowiec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbHandlowiec.SelectedItem != null)
            {
                await _viewModel.FiltrujPoHandlowcuAsync(cbHandlowiec.SelectedItem.ToString());
            }
        }

        private void ChkShowCanceled_Changed(object sender, RoutedEventArgs e)
        {
            _viewModel.OdswiezCommand.Execute(null);
        }

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "";
            _viewModel.WyczyscFiltryCommand.Execute(null);
        }

        #endregion

        #region Przyciski akcji

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.OdswiezCommand.Execute(null);
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ExportToExcel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToExcel()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Plik Excel (*.xlsx)|*.xlsx",
                FileName = $"Zamowienia_Faktury_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                DefaultExt = ".xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Prosty eksport do CSV (można rozszerzyć o pełny Excel)
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("ID;Odbiorca;Handlowiec;Data odbioru;Godzina;Transport;kg;Pojemniki;Palety;Notatka;Status");

                    foreach (var z in _viewModel.Zamowienia)
                    {
                        sb.AppendLine($"{z.Id};\"{z.Odbiorca}\";\"{z.Handlowiec}\";{z.DataOdbioru:yyyy-MM-dd};{z.GodzinaOdbioru};\"{z.TransportTekst}\";{z.SumaKg};{z.SumaPojemnikow};{z.SumaPalet};\"{z.Notatka?.Replace("\n", " ")}\";\"{z.StatusWyswietlany}\"");
                    }

                    var csvPath = dialog.FileName.Replace(".xlsx", ".csv");
                    System.IO.File.WriteAllText(csvPath, sb.ToString(), System.Text.Encoding.UTF8);

                    MessageBox.Show($"Wyeksportowano {_viewModel.Zamowienia.Count} zamówień do:\n{csvPath}",
                        "Eksport zakończony", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas eksportu: {ex.Message}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region DataGrid

        private void DgOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Obsługiwane przez binding
        }

        #endregion

        #region Skróty klawiszowe

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.F5)
            {
                _viewModel.OdswiezCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (!string.IsNullOrEmpty(txtSearch.Text))
                {
                    txtSearch.Text = "";
                    e.Handled = true;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.F:
                        txtSearch.Focus();
                        txtSearch.SelectAll();
                        e.Handled = true;
                        break;
                    case Key.Left:
                        _viewModel.PoprzedniTydzienCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Right:
                        _viewModel.NastepnyTydzienCommand.Execute(null);
                        e.Handled = true;
                        break;
                }
            }
        }

        #endregion
    }
}
