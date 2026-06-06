using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Views
{
    /// <summary>
    /// Dialog wyboru pól do zastąpienia danymi z Białej Listy MF.
    /// User decyduje, które pola nadpisać — pokazuje porównanie obecna vs nowa wartość.
    /// </summary>
    public partial class GusWyborDialog : Window
    {
        public bool WybranoNazwe { get; private set; }
        public bool WybranoRegon { get; private set; }
        public bool WybranoAdres { get; private set; }

        public string Nazwa { get; }
        public string Regon { get; }
        public string Adres { get; }

        public GusWyborDialog(string nip, GusApiService.GusResult r, string obecnaNazwa, string obecnyRegon, string obecnyAdres)
        {
            InitializeComponent();
            Nazwa = r.Nazwa ?? "";
            Regon = r.Regon ?? "";
            Adres = r.Adres ?? "";

            string status = string.IsNullOrEmpty(r.StatusVat) ? "" : $"  ·  VAT: {r.StatusVat}";
            txtPodtytul.Text = $"NIP {nip}{status}";

            bool brakDanych = false;

            // Nazwa
            if (string.IsNullOrWhiteSpace(Nazwa))
            {
                chkNazwa.IsEnabled = false; boxNazwa.Opacity = 0.5;
                txtNazwaNowa.Text = "(MF nie zwrócił nazwy)";
                txtNazwaNowa.Foreground = System.Windows.Media.Brushes.Gray;
                brakDanych = true;
            }
            else
            {
                txtNazwaNowa.Text = Nazwa;
                if (!string.IsNullOrWhiteSpace(obecnaNazwa) && !string.Equals(obecnaNazwa.Trim(), Nazwa.Trim(), System.StringComparison.OrdinalIgnoreCase))
                {
                    txtNazwaStara.Text = obecnaNazwa;
                    boxNazwaPorownanie.Visibility = Visibility.Visible;
                }
                // domyślnie zaznacz tylko gdy pole obecne jest PUSTE
                chkNazwa.IsChecked = string.IsNullOrWhiteSpace(obecnaNazwa);
            }

            // REGON
            if (string.IsNullOrWhiteSpace(Regon))
            {
                chkRegon.IsEnabled = false; boxRegon.Opacity = 0.5;
                txtRegonNowy.Text = "(MF nie zwrócił REGON)";
                txtRegonNowy.Foreground = System.Windows.Media.Brushes.Gray;
                brakDanych = true;
            }
            else
            {
                txtRegonNowy.Text = Regon;
                if (!string.IsNullOrWhiteSpace(obecnyRegon) && obecnyRegon.Trim() != Regon.Trim())
                {
                    txtRegonStary.Text = obecnyRegon;
                    boxRegonPorownanie.Visibility = Visibility.Visible;
                }
                chkRegon.IsChecked = string.IsNullOrWhiteSpace(obecnyRegon);
            }

            // Adres
            if (string.IsNullOrWhiteSpace(Adres))
            {
                chkAdres.IsEnabled = false; boxAdres.Opacity = 0.5;
                txtAdresNowy.Text = "(MF nie zwrócił adresu)";
                txtAdresNowy.Foreground = System.Windows.Media.Brushes.Gray;
                brakDanych = true;
            }
            else
            {
                txtAdresNowy.Text = Adres;
                if (!string.IsNullOrWhiteSpace(obecnyAdres) && !string.Equals(obecnyAdres.Trim(), Adres.Trim(), System.StringComparison.OrdinalIgnoreCase))
                {
                    txtAdresStary.Text = obecnyAdres;
                    boxAdresPorownanie.Visibility = Visibility.Visible;
                }
                chkAdres.IsChecked = string.IsNullOrWhiteSpace(obecnyAdres);
            }

            txtBrakDanych.Visibility = brakDanych ? Visibility.Visible : Visibility.Collapsed;
            AktualizujLicznikBtn();
        }

        private void Chk_Changed(object sender, RoutedEventArgs e) => AktualizujLicznikBtn();

        private void AktualizujLicznikBtn()
        {
            int n = (chkNazwa.IsChecked == true ? 1 : 0)
                  + (chkRegon.IsChecked == true ? 1 : 0)
                  + (chkAdres.IsChecked == true ? 1 : 0);
            btnOk.Content = n == 0 ? "✔  Zastosuj" : $"✔  Zastosuj ({n})";
            btnOk.IsEnabled = n > 0;
        }

        private void BtnZaznaczWszystko_Click(object sender, RoutedEventArgs e)
        {
            if (chkNazwa.IsEnabled) chkNazwa.IsChecked = true;
            if (chkRegon.IsEnabled) chkRegon.IsChecked = true;
            if (chkAdres.IsEnabled) chkAdres.IsChecked = true;
        }

        private void BtnOdznaczWszystko_Click(object sender, RoutedEventArgs e)
        {
            chkNazwa.IsChecked = false;
            chkRegon.IsChecked = false;
            chkAdres.IsChecked = false;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            WybranoNazwe = chkNazwa.IsChecked == true;
            WybranoRegon = chkRegon.IsChecked == true;
            WybranoAdres = chkAdres.IsChecked == true;
            DialogResult = true;
            Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
