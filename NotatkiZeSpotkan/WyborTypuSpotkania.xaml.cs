using System.Windows;

namespace Kalendarz1.NotatkiZeSpotkan
{
    public partial class WyborTypuSpotkania : Window
    {
        public string WybranyTyp { get; private set; } = string.Empty;

        public WyborTypuSpotkania()
        {
            InitializeComponent();
        }

        private void BtnZespol_Click(object sender, RoutedEventArgs e)
        {
            WybranyTyp = "Zespół";
            DialogResult = true;
            Close();
        }

        private void BtnOdbiorca_Click(object sender, RoutedEventArgs e)
        {
            WybranyTyp = "Odbiorca";
            DialogResult = true;
            Close();
        }

        private void BtnHodowca_Click(object sender, RoutedEventArgs e)
        {
            WybranyTyp = "Hodowca";
            DialogResult = true;
            Close();
        }
    }
}