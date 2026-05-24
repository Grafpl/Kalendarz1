using System.Windows;

namespace Kalendarz1.ColdChain
{
    /// <summary>Dialog opisu działania naprawczego przy zamykaniu incydentu CCP (wymóg HACCP).</summary>
    public partial class KorektaDialog : Window
    {
        public string Korekta => txtKorekta.Text?.Trim() ?? "";

        public KorektaDialog(CCPIncydent inc)
        {
            InitializeComponent();
            txtInfo.Text = $"Punkt: {inc.PunktNazwa}  •  Start: {inc.StartFormatted}  •  "
                + $"Zakres: {inc.WartoscFormatted}  •  Czas: {inc.CzasTrwania}.\n"
                + "Opisz przyczynę i podjęte działanie naprawcze.";
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtKorekta.Text))
            {
                MessageBox.Show("Opisz działanie naprawcze (wymóg HACCP).", "Korekta",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }

        private void Anuluj_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
