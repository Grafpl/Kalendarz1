using System;
using System.Windows;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    public partial class RaportZarzaduWindow : Window
    {
        private readonly RaportZarzaduViewModel _viewModel;

        public RaportZarzaduWindow(string userId)
        {
            InitializeComponent();
            _viewModel = new RaportZarzaduViewModel(userId);
            DataContext = _viewModel;
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
