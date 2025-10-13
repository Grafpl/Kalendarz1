using System.Windows;

namespace Kalendarz1.WPF
{
    public partial class NoteWindow : Window
    {
        public string NoteText { get; private set; }

        public NoteWindow(string currentNote = "")
        {
            InitializeComponent();
            txtNote.Text = currentNote;
            txtNote.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            NoteText = txtNote.Text;
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