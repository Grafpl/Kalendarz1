using System;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.Opakowania.ViewModels;
using Kalendarz1.Opakowania.Models;

namespace Kalendarz1.Opakowania.Views
{
    /// <summary>
    /// Okno pokazujące salda wszystkich opakowań dla wszystkich kontrahentów
    /// </summary>
    public partial class SaldaWszystkichOpakowanWindow : Window
    {
        private readonly SaldaWszystkichOpakowanViewModel _viewModel;

        public SaldaWszystkichOpakowanWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            _viewModel = new SaldaWszystkichOpakowanViewModel(App.UserID);
            DataContext = _viewModel;

            // Subskrybuj eventy
            _viewModel.OtworzSzczegolyRequested += OnOtworzSzczegolyRequested;
        }

        #region Obsługa eventów

        private void OnOtworzSzczegolyRequested(SaldoOpakowania saldo)
        {
            OtworzSzczegolyKontrahenta(saldo);
        }

        private void OtworzSzczegolyKontrahenta(SaldoOpakowania saldo)
        {
            if (saldo == null || saldo.KontrahentId <= 0) return;

            var okno = new SaldoOdbiorcyWindow(saldo.KontrahentId, saldo.Kontrahent, App.UserID ?? "11111");
            okno.Owner = this;
            var result = okno.ShowDialog();

            // Odśwież dane tylko jeśli coś zmieniono (DialogResult = true)
            if (result == true)
            {
                _viewModel.OdswiezCommand.Execute(null);
            }
        }

        #endregion

        #region Obsługa przycisków w DataGrid

        private void BtnSzczegoly_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn)
            {
                // Pobierz DataContext przycisku (wiersz)
                if (btn.DataContext is SaldoOpakowania saldo)
                {
                    OtworzSzczegolyKontrahenta(saldo);
                }
            }
        }

        private void DgSalda_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGridSalda.SelectedItem is SaldoOpakowania saldo)
            {
                OtworzSzczegolyKontrahenta(saldo);
            }
        }

        private void DataGridSalda_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGridSalda.SelectedItem is SaldoOpakowania saldo)
            {
                OtworzSzczegolyKontrahenta(saldo);
            }
        }

        private void MenuSzczegoly_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridSalda.SelectedItem is SaldoOpakowania saldo)
            {
                OtworzSzczegolyKontrahenta(saldo);
            }
        }

        private void MenuZadzwon_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridSalda.SelectedItem is SaldoOpakowania saldo)
            {
                MessageBox.Show($"Dzwonienie do: {saldo.Kontrahent}", "Zadzwoń", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MenuWyslijEmail_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridSalda.SelectedItem is SaldoOpakowania saldo)
            {
                MessageBox.Show($"Wysyłanie emaila do: {saldo.Kontrahent}", "Email", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MenuEksportPDF_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.EksportPDFCommand?.Execute(null);
        }

        private void MenuEksportExcel_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.EksportExcelCommand?.Execute(null);
        }

        #endregion

        #region Obsługa okna

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized 
                    ? WindowState.Normal 
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized 
                ? WindowState.Normal 
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.OtworzSzczegolyRequested -= OnOtworzSzczegolyRequested;
            base.OnClosed(e);
        }
    }
}
