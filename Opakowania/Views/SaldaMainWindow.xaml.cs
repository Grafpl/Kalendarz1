using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    public partial class SaldaMainWindow : Window
    {
        private readonly SaldaMainViewModel _viewModel;

        public SaldaMainWindow(string userId)
        {
            InitializeComponent();

            // Dodaj converter
            Resources.Add("BoolToVisibility", new BooleanToVisibilityConverter());

            _viewModel = new SaldaMainViewModel(userId);
            DataContext = _viewModel;
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_viewModel.WybranyKontrahent != null)
            {
                var szczegolyWindow = new SaldaSzczegolyWindow(
                    _viewModel.WybranyKontrahent,
                    _viewModel.DataDo,
                    _viewModel.UserId);
                szczegolyWindow.Owner = this;
                szczegolyWindow.ShowDialog();

                // Odśwież po zamknięciu (może być nowe potwierdzenie)
                _viewModel.OdswiezCommand.Execute(null);
            }
        }
    }
}
