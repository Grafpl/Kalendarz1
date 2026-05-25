using System.Windows;
using Kalendarz1.Transport;

namespace Kalendarz1.Transport.WPF.Dialogs
{
    /// <summary>Lekki dialog WPF dodawania pojazdu. Zwraca obiekt Pojazd (bez ID — zapis robi wołający przez repo).</summary>
    public partial class NowyPojazdWpfDialog : Window
    {
        public Pojazd? Wynik { get; private set; }

        public NowyPojazdWpfDialog()
        {
            InitializeComponent();
            Loaded += (_, _) => TxtRejestracja.Focus();
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            var rej = TxtRejestracja.Text.Trim();
            if (string.IsNullOrWhiteSpace(rej))
            {
                MessageBox.Show("Rejestracja jest wymagana.", "Brak danych",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(TxtPalety.Text.Trim(), out var palety) || palety < 1 || palety > 100)
                palety = 33;

            Wynik = new Pojazd
            {
                Rejestracja = rej,
                Marka = string.IsNullOrWhiteSpace(TxtMarka.Text) ? null : TxtMarka.Text.Trim(),
                Model = string.IsNullOrWhiteSpace(TxtModel.Text) ? null : TxtModel.Text.Trim(),
                PaletyH1 = palety,
                Aktywny = true
            };
            DialogResult = true;
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
