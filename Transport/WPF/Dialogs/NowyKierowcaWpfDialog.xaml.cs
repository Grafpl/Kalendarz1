using System.Windows;
using Kalendarz1.Transport;

namespace Kalendarz1.Transport.WPF.Dialogs
{
    /// <summary>Lekki dialog WPF dodawania kierowcy. Zwraca obiekt Kierowca (bez ID — zapis robi wołający przez repo).</summary>
    public partial class NowyKierowcaWpfDialog : Window
    {
        public Kierowca? Wynik { get; private set; }

        public NowyKierowcaWpfDialog()
        {
            InitializeComponent();
            Loaded += (_, _) => TxtImie.Focus();
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            var imie = TxtImie.Text.Trim();
            var nazwisko = TxtNazwisko.Text.Trim();
            if (string.IsNullOrWhiteSpace(imie) || string.IsNullOrWhiteSpace(nazwisko))
            {
                MessageBox.Show("Imię i nazwisko są wymagane.", "Brak danych",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Wynik = new Kierowca
            {
                Imie = imie,
                Nazwisko = nazwisko,
                Telefon = string.IsNullOrWhiteSpace(TxtTelefon.Text) ? null : TxtTelefon.Text.Trim(),
                Aktywny = true
            };
            DialogResult = true;
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
