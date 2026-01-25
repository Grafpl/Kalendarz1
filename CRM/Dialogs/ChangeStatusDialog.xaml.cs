using System.Windows;

namespace Kalendarz1.CRM.Dialogs
{
    public partial class ChangeStatusDialog : Window
    {
        public string SelectedStatus { get; private set; }
        private string _currentStatus;

        public ChangeStatusDialog(string contactName, string currentStatus)
        {
            InitializeComponent();
            txtContactName.Text = contactName;
            _currentStatus = currentStatus;
            txtCurrentStatus.Text = $"Aktualny status: {currentStatus}";

            // Pre-select current status
            switch (currentStatus)
            {
                case "Do zadzwonienia":
                    rbDoZadzwonienia.IsChecked = true;
                    break;
                case "Próba kontaktu":
                    rbProba.IsChecked = true;
                    break;
                case "Nawiązano kontakt":
                    rbNawiazano.IsChecked = true;
                    break;
                case "Zgoda na dalszy kontakt":
                    rbZgoda.IsChecked = true;
                    break;
                case "Do wysłania oferta":
                    rbOferta.IsChecked = true;
                    break;
                case "Nie zainteresowany":
                    rbNieZainteresowany.IsChecked = true;
                    break;
                default:
                    rbDoZadzwonienia.IsChecked = true;
                    break;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (rbDoZadzwonienia.IsChecked == true) SelectedStatus = "Do zadzwonienia";
            else if (rbProba.IsChecked == true) SelectedStatus = "Próba kontaktu";
            else if (rbNawiazano.IsChecked == true) SelectedStatus = "Nawiązano kontakt";
            else if (rbZgoda.IsChecked == true) SelectedStatus = "Zgoda na dalszy kontakt";
            else if (rbOferta.IsChecked == true) SelectedStatus = "Do wysłania oferta";
            else if (rbNieZainteresowany.IsChecked == true) SelectedStatus = "Nie zainteresowany";

            if (SelectedStatus == _currentStatus)
            {
                MessageBox.Show("Wybierz inny status niż aktualny!", "Brak zmiany", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
