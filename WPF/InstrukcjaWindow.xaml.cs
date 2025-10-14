using System.Windows;

namespace Kalendarz1.WPF
{
    public partial class InstrukcjaWindow : Window
    {
        public InstrukcjaWindow()
        {
            InitializeComponent();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}