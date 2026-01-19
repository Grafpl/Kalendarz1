using System.Windows;

namespace Kalendarz1.WPF
{
    public partial class PrintOptionsWindow : Window
    {
        public bool GroupByProduct => rbGroupByProduct.IsChecked == true;

        public PrintOptionsWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}