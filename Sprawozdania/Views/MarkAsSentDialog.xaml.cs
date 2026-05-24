using System.Windows;

namespace Kalendarz1.Sprawozdania.Views
{
    public partial class MarkAsSentDialog : Window
    {
        public string? NumerWPortalu { get; private set; }

        public MarkAsSentDialog(string okresLabel)
        {
            InitializeComponent();
            lblOkres.Text = $"Okres: {okresLabel}";
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            NumerWPortalu = string.IsNullOrWhiteSpace(txtNumer.Text) ? null : txtNumer.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
