using System.Windows;

namespace Kalendarz1.WPF
{
    public partial class NoteWindow : Window
    {
        public string NoteText { get; private set; }
        public event EventHandler NoteSaved;

        public NoteWindow(string currentNote = "")
        {
            InitializeComponent();
            txtNote.Text = currentNote;
            txtNote.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            NoteText = txtNote.Text;
            NoteSaved?.Invoke(this, EventArgs.Empty);
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}