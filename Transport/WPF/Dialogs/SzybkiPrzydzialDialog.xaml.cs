using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Kalendarz1.Transport;

namespace Kalendarz1.Transport.WPF.Dialogs
{
    /// <summary>Szybki przydział kierowcy + pojazdu do kursu (bez pełnego edytora).</summary>
    public partial class SzybkiPrzydzialDialog : Window
    {
        public int? KierowcaID { get; private set; }
        public int? PojazdID { get; private set; }

        public SzybkiPrzydzialDialog(List<Kierowca> kierowcy, List<Pojazd> pojazdy,
            int? aktualnyKierowca, int? aktualnyPojazd, string naglowek)
        {
            InitializeComponent();
            TytulText.Text = naglowek;
            CmbKierowca.ItemsSource = kierowcy;
            CmbPojazd.ItemsSource = pojazdy;
            if (aktualnyKierowca.HasValue)
                CmbKierowca.SelectedItem = kierowcy.FirstOrDefault(k => k.KierowcaID == aktualnyKierowca.Value);
            if (aktualnyPojazd.HasValue)
                CmbPojazd.SelectedItem = pojazdy.FirstOrDefault(p => p.PojazdID == aktualnyPojazd.Value);
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            KierowcaID = (CmbKierowca.SelectedItem as Kierowca)?.KierowcaID;
            PojazdID = (CmbPojazd.SelectedItem as Pojazd)?.PojazdID;
            DialogResult = true;
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
