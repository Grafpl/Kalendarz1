using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.AnalitykaPelna.Controls
{
    public partial class LoadingOverlay : UserControl
    {
        public LoadingOverlay()
        {
            InitializeComponent();
        }

        public void Pokaz(string komunikat = "Ładowanie danych…")
        {
            txtKomunikat.Text = komunikat;
            Visibility = Visibility.Visible;
        }

        public void Ukryj() => Visibility = Visibility.Collapsed;
    }
}
