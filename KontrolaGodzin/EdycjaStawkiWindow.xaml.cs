using System;
using System.Globalization;
using System.Windows;

namespace Kalendarz1.KontrolaGodzin
{
    public partial class EdycjaStawkiWindow : Window
    {
        public StawkaModel Stawka { get; private set; }

        public EdycjaStawkiWindow(StawkaModel stawka)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            dpOdDaty.SelectedDate = DateTime.Today;

            if (stawka != null)
            {
                // Edycja istniejącej
                txtNazwa.Text = stawka.Nazwa;
                txtStawka.Text = stawka.StawkaPodstawowa.ToString("N2");
                txtNadgodziny.Text = stawka.MnoznikNadgodzin.ToString("N2");
                txtNocne.Text = stawka.MnoznikNocne.ToString("N2");
                txtSwiateczne.Text = stawka.MnoznikSwiateczne.ToString("N2");
                dpOdDaty.SelectedDate = stawka.OdDaty;
                dpDoDaty.SelectedDate = stawka.DoDaty;

                Title = $"Edycja stawki: {stawka.Nazwa}";
            }
            else
            {
                Title = "Nowa stawka";
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtNazwa.Text))
                {
                    MessageBox.Show("Podaj nazwę agencji/działu", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Stawka = new StawkaModel
                {
                    Nazwa = txtNazwa.Text.Trim().ToUpper(),
                    StawkaPodstawowa = ParseDecimal(txtStawka.Text),
                    MnoznikNadgodzin = ParseDecimal(txtNadgodziny.Text),
                    MnoznikNocne = ParseDecimal(txtNocne.Text),
                    MnoznikSwiateczne = ParseDecimal(txtSwiateczne.Text),
                    OdDaty = dpOdDaty.SelectedDate ?? DateTime.Today,
                    DoDaty = dpDoDaty.SelectedDate
                };

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private decimal ParseDecimal(string text)
        {
            text = text.Replace(",", ".");
            return decimal.Parse(text, CultureInfo.InvariantCulture);
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
