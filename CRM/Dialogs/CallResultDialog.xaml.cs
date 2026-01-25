using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.CRM.Dialogs
{
    public partial class CallResultDialog : Window
    {
        public string SelectedStatus { get; private set; }
        public string Note { get; private set; }

        public CallResultDialog(string contactName)
        {
            InitializeComponent();
            txtContactName.Text = contactName;
            rbNieOdebrano.IsChecked = true; // Default selection
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Get selected status
            if (rbNieOdebrano.IsChecked == true) SelectedStatus = "Próba kontaktu";
            else if (rbZajete.IsChecked == true) SelectedStatus = "Próba kontaktu";
            else if (rbZainteresowany.IsChecked == true) SelectedStatus = "Zgoda na dalszy kontakt";
            else if (rbNiezainteresowany.IsChecked == true) SelectedStatus = "Nie zainteresowany";
            else if (rbUmowiono.IsChecked == true) SelectedStatus = "Zgoda na dalszy kontakt";
            else if (rbOferta.IsChecked == true) SelectedStatus = "Do wysłania oferta";

            // Get note
            Note = txtNote.Text?.Trim();

            // Build automatic note based on result
            string autoNote = "";
            if (rbNieOdebrano.IsChecked == true) autoNote = "[Telefon] Nie odebrano";
            else if (rbZajete.IsChecked == true) autoNote = "[Telefon] Zajęte/Niedostępny";
            else if (rbZainteresowany.IsChecked == true) autoNote = "[Telefon] Rozmowa - klient zainteresowany";
            else if (rbNiezainteresowany.IsChecked == true) autoNote = "[Telefon] Rozmowa - klient niezainteresowany";
            else if (rbUmowiono.IsChecked == true) autoNote = "[Telefon] Rozmowa - umówiono dalszy kontakt";
            else if (rbOferta.IsChecked == true) autoNote = "[Telefon] Rozmowa - do wysłania oferta";

            // Combine auto note with user note
            if (!string.IsNullOrWhiteSpace(Note))
            {
                Note = $"{autoNote}. {Note}";
            }
            else
            {
                Note = autoNote;
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
