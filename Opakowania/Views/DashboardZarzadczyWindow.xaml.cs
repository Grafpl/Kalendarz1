using System.Windows;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    public partial class DashboardZarzadczyWindow : Window
    {
        public DashboardZarzadczyWindow(string userId)
        {
            InitializeComponent();
            DataContext = new DashboardZarzadczyViewModel(userId);
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
