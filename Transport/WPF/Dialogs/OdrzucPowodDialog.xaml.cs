// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Dialogs/OdrzucPowodDialog.xaml.cs — szybki dialog odrzucenia
// zmiany. 4 gotowe chipy + własny tekst. Klik chipa wypełnia pole, klik
// "Odrzuć" zwraca powód (null = bez powodu, dozwolone przez serwis).
// ════════════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.Transport.WPF.Dialogs
{
    public partial class OdrzucPowodDialog : Window
    {
        public string? Powod { get; private set; }

        public OdrzucPowodDialog(string kontekst)
        {
            InitializeComponent();
            TxtKontekst.Text = kontekst;
            Loaded += (_, _) => TxtPowod.Focus();
        }

        private void BtnChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Content is string txt)
            {
                TxtPowod.Text = txt;
                TxtPowod.CaretIndex = txt.Length;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void BtnOdrzuc_Click(object sender, RoutedEventArgs e)
        {
            Powod = string.IsNullOrWhiteSpace(TxtPowod.Text) ? null : TxtPowod.Text.Trim();
            DialogResult = true;
        }
    }
}
