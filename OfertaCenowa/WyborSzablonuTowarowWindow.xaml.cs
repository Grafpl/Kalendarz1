using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Okno wyboru szablonu towarów
    /// </summary>
    public partial class WyborSzablonuTowarowWindow : Window
    {
        private readonly SzablonyManager _szablonyManager = new();
        private readonly ObservableCollection<TowarOferta> _dostepneTowary;
        private List<SzablonTowarow> _szablony = new();

        public SzablonTowarow? WybranySzablon { get; private set; }
        public bool OtworzEdytor { get; private set; } = false;

        public WyborSzablonuTowarowWindow(ObservableCollection<TowarOferta> dostepneTowary)
        {
            InitializeComponent();
            _dostepneTowary = dostepneTowary;
            WczytajSzablony();
        }

        private void WczytajSzablony()
        {
            _szablony = _szablonyManager.WczytajSzablonyTowarow();
            lstSzablony.ItemsSource = _szablony;
            placeholderBrak.Visibility = _szablony.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LstSzablony_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnWczytaj.IsEnabled = lstSzablony.SelectedItem != null;
        }

        private void LstSzablony_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstSzablony.SelectedItem is SzablonTowarow szablon)
            {
                WybranySzablon = szablon;
                DialogResult = true;
                Close();
            }
        }

        private void BtnWczytaj_Click(object sender, RoutedEventArgs e)
        {
            if (lstSzablony.SelectedItem is SzablonTowarow szablon)
            {
                WybranySzablon = szablon;
                DialogResult = true;
                Close();
            }
        }

        private void BtnEdytujSzablony_Click(object sender, RoutedEventArgs e)
        {
            var okno = new SzablonTowarowWindow(_dostepneTowary);
            okno.Owner = this.Owner;
            okno.ShowDialog();

            // Odśwież listę po zamknięciu edytora
            WczytajSzablony();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
