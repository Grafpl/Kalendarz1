using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace Kalendarz1.OfertaCenowa
{
    public partial class WyborSzablonuWindow : Window
    {
        public int WybranyIndex { get; private set; } = -1;

        public WyborSzablonuWindow(List<object> szablony, string tytul)
        {
            InitializeComponent();
            txtTytul.Text = tytul;
            lstSzablony.ItemsSource = szablony;
        }

        private void BtnWybierz_Click(object sender, RoutedEventArgs e)
        {
            if (lstSzablony.SelectedIndex >= 0)
            {
                WybranyIndex = lstSzablony.SelectedIndex;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("Wybierz szablon z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void LstSzablony_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstSzablony.SelectedIndex >= 0)
            {
                BtnWybierz_Click(sender, e);
            }
        }
    }
}
