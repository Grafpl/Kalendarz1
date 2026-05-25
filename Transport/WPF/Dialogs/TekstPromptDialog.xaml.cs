using System.Windows;

namespace Kalendarz1.Transport.WPF.Dialogs
{
    /// <summary>Uniwersalny dialog wpisania tekstu (uwagi, trasa itp.).</summary>
    public partial class TekstPromptDialog : Window
    {
        public string Wartosc => TxtWartosc.Text;

        public TekstPromptDialog(string tytul, string etykieta, string wartoscPoczatkowa = "")
        {
            InitializeComponent();
            TytulText.Text = tytul;
            EtykietaText.Text = etykieta;
            TxtWartosc.Text = wartoscPoczatkowa ?? "";
            Loaded += (_, _) => { TxtWartosc.Focus(); TxtWartosc.SelectAll(); };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        private void BtnAnuluj_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
