using System.Windows;

namespace Kalendarz1.ColdChain
{
    /// <summary>Dialog opisu działania naprawczego przy korekcie incydentu temperaturowego (HACCP).</summary>
    public partial class KorektaDialog : Window
    {
        public string Korekta => txtKorekta.Text?.Trim() ?? "";

        public KorektaDialog(TempPomiar p, string? istniejacaKorekta = null)
        {
            InitializeComponent();
            txtInfo.Text = $"Partia: {p.PartiaId}  •  {p.MiejsceLabel}  •  {p.DataFormatted}  •  "
                + $"Średnia: {p.SredniaFormatted} (norma {p.NormaFormatted}).\n"
                + "Opisz przyczynę i podjęte działanie naprawcze.";
            if (!string.IsNullOrEmpty(istniejacaKorekta))
                txtKorekta.Text = istniejacaKorekta;
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
