using System;
using System.Windows;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    public partial class DashboardPotwierdzeniWindow : Window
    {
        private readonly DashboardPotwierdzeniViewModel _viewModel;

        public DashboardPotwierdzeniWindow(string userId)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            _viewModel = new DashboardPotwierdzeniViewModel(userId, this);
            DataContext = _viewModel;
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel?.Dispose();
            base.OnClosed(e);
        }
    }
}
