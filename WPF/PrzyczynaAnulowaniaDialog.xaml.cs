using System.Windows;

namespace Kalendarz1.WPF
{
    public partial class PrzyczynaAnulowaniaDialog : Window
    {
        public string? WybranaPrzyczyna { get; private set; }
        public bool CzyAnulowano { get; private set; }

        public PrzyczynaAnulowaniaDialog(string odbiorca, decimal ilosc)
        {
            InitializeComponent();
            txtOdbiorca.Text = odbiorca;
            txtIlosc.Text = $"{ilosc:N0} kg";
            CzyAnulowano = false;
        }

        private void BtnReason_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string reason)
            {
                WybranaPrzyczyna = reason;
                CzyAnulowano = true;
                DialogResult = true;
                Close();
            }
        }

        private void BtnInnaPrzyczyna_Click(object sender, RoutedEventArgs e)
        {
            var przyczyna = txtInnaPrzyczyna.Text?.Trim();
            if (string.IsNullOrEmpty(przyczyna))
            {
                MessageBox.Show("Wprowadź opis przyczyny anulowania.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WybranaPrzyczyna = $"Inna: {przyczyna}";
            CzyAnulowano = true;
            DialogResult = true;
            Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            CzyAnulowano = false;
            DialogResult = false;
            Close();
        }
    }
}
