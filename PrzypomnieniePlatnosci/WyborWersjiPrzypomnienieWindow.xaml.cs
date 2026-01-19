using System.Windows;
using System.Windows.Input;

namespace Kalendarz1.PrzypomnieniePlatnosci
{
    public partial class WyborWersjiPrzypomnienieWindow : Window
    {
        public WersjaPrzypomnienia WybranaWersja { get; private set; }
        public int LiczbaDni { get; private set; }

        public WyborWersjiPrzypomnienieWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
        }

        private void BorderLagodna_Click(object sender, MouseButtonEventArgs e)
        {
            if (!int.TryParse(txtDniLagodna.Text, out int dni) || dni < 1)
            {
                MessageBox.Show("Podaj prawidłową liczbę dni (minimum 1).", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WybranaWersja = WersjaPrzypomnienia.Lagodna;
            LiczbaDni = dni;
            DialogResult = true;
            Close();
        }

        private void BorderMocna_Click(object sender, MouseButtonEventArgs e)
        {
            if (!int.TryParse(txtDniMocna.Text, out int dni) || dni < 1)
            {
                MessageBox.Show("Podaj prawidłową liczbę dni (minimum 1).", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WybranaWersja = WersjaPrzypomnienia.Mocna;
            LiczbaDni = dni;
            DialogResult = true;
            Close();
        }

        private void BorderPrzedsadowa_Click(object sender, MouseButtonEventArgs e)
        {
            if (!int.TryParse(txtDniPrzedsadowa.Text, out int dni) || dni < 1)
            {
                MessageBox.Show("Podaj prawidłową liczbę dni (minimum 1).", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WybranaWersja = WersjaPrzypomnienia.Przedsadowa;
            LiczbaDni = dni;
            DialogResult = true;
            Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}