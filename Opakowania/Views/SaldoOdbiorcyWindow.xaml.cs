using System;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    /// <summary>
    /// Okno szczegółów salda dla konkretnego odbiorcy
    /// </summary>
    public partial class SaldoOdbiorcyWindow : Window
    {
        private readonly SaldoOdbiorcyViewModel _viewModel;

        public SaldoOdbiorcyWindow(int kontrahentId, string kontrahentNazwa, string userId)
        {
            InitializeComponent();

            _viewModel = new SaldoOdbiorcyViewModel(kontrahentId, kontrahentNazwa, userId);
            DataContext = _viewModel;
        }

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

        private void BtnPowrot_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        #endregion
    }
}
