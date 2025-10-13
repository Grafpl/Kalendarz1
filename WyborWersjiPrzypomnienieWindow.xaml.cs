using System.Windows;

namespace Kalendarz1
{
    public enum WersjaPrzypomnienia
    {
        Lagodna,
        Mocna
    }

    public partial class WyborWersjiPrzypomnienieWindow : Window
    {
        public WersjaPrzypomnienia WybranaWersja { get; private set; }

        public WyborWersjiPrzypomnienieWindow()
        {
            InitializeComponent();
        }

        private void BtnLagodna_Click(object sender, RoutedEventArgs e)
        {
            WybranaWersja = WersjaPrzypomnienia.Lagodna;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnMocna_Click(object sender, RoutedEventArgs e)
        {
            WybranaWersja = WersjaPrzypomnienia.Mocna;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}