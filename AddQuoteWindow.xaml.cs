using System.Windows;

namespace Kalendarz1
{
    public partial class AddQuoteWindow : Window
    {
        public string QuoteText { get; private set; }
        public string QuoteAuthor { get; private set; }

        public AddQuoteWindow()
        {
            InitializeComponent();
            QuoteTextBox.Focus();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string quote = QuoteTextBox.Text?.Trim();
            string author = AuthorTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(quote))
            {
                MessageBox.Show("Proszę wprowadzić treść cytatu.", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                QuoteTextBox.Focus();
                return;
            }

            QuoteText = quote;
            QuoteAuthor = string.IsNullOrWhiteSpace(author) ? "Nieznany" : author;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
