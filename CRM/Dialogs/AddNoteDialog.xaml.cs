using System.Windows;

namespace Kalendarz1.CRM.Dialogs
{
    public partial class AddNoteDialog : Window
    {
        public string NoteText { get; private set; }

        public AddNoteDialog(string contactName)
        {
            InitializeComponent();
            txtContactName.Text = contactName;
            txtNote.Focus();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            NoteText = txtNote.Text?.Trim();

            if (string.IsNullOrWhiteSpace(NoteText))
            {
                MessageBox.Show("Wprowadź treść notatki!", "Wymagane pole", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNote.Focus();
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
