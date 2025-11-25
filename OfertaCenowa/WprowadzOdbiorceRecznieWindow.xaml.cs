using System.Windows;

namespace Kalendarz1.OfertaCenowa
{
    public partial class WprowadzOdbiorceRecznieWindow : Window
    {
        public OdbiorcaOferta? Odbiorca { get; private set; }

        public WprowadzOdbiorceRecznieWindow()
        {
            InitializeComponent();
            txtNazwa.Focus();
        }

        private void BtnDodaj_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNazwa.Text))
            {
                MessageBox.Show("Podaj nazwę firmy.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNazwa.Focus();
                return;
            }

            Odbiorca = new OdbiorcaOferta
            {
                Id = $"RECZNY_{System.DateTime.Now.Ticks}",
                Nazwa = txtNazwa.Text.Trim(),
                NIP = txtNIP.Text.Trim(),
                Adres = txtAdres.Text.Trim(),
                KodPocztowy = txtKodPocztowy.Text.Trim(),
                Miejscowosc = txtMiejscowosc.Text.Trim(),
                Telefon = txtTelefon.Text.Trim(),
                Email = txtEmail.Text.Trim(),
                OsobaKontaktowa = txtOsobaKontaktowa.Text.Trim(),
                Wojewodztwo = cboWojewodztwo.SelectedItem != null 
                    ? (cboWojewodztwo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString() ?? cboWojewodztwo.Text 
                    : cboWojewodztwo.Text,
                Zrodlo = "RECZNY",
                Status = "Wprowadzony ręcznie"
            };

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
