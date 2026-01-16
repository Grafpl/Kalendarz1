using System;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    /// <summary>
    /// Lista kontrahentów dla wybranego typu opakowania
    /// </summary>
    public partial class ListaOpakowanWindow : Window
    {
        private readonly ListaOpakowanViewModel _viewModel;

        public ListaOpakowanWindow(string kodOpakowania, string nazwaOpakowania, DateTime dataDo, string userId)
        {
            InitializeComponent();
            _viewModel = new ListaOpakowanViewModel(kodOpakowania, nazwaOpakowania, dataDo, userId);
            DataContext = _viewModel;
        }

        /// <summary>
        /// Zamknij okno
        /// </summary>
        private void BtnWstecz_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Dwuklik - otwiera szczegóły kontrahenta
        /// </summary>
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.WybranyKontrahent != null)
            {
                var szczegolyWindow = new SzczegolyKontrahentaWindow(
                    _viewModel.WybranyKontrahent,
                    _viewModel.DataDo,
                    _viewModel.UserId);
                szczegolyWindow.Owner = this;
                szczegolyWindow.ShowDialog();

                // Odśwież po zamknięciu
                _viewModel.OdswiezCommand.Execute(null);
            }
        }
    }
}
