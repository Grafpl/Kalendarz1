using System.Windows;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    public partial class ZestawieniePorownanczeWindow : Window
    {
        public ZestawieniePorownanczeWindow(string userId)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            DataContext = new ZestawieniePorownanczeViewModel(userId);
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
