using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kalendarz1.Kontrakty.Models;

namespace Kalendarz1.Kontrakty.Views
{
    /// <summary>
    /// Dialog wyboru istniejącego kontraktu (do skopiowania pól + cykli).
    /// Wynik: WybranyId — Id kontraktu wybranego z listy.
    /// </summary>
    public partial class WybierzKontraktDialog : Window
    {
        public int? WybranyId { get; private set; }

        public WybierzKontraktDialog(string podtytul, List<KontraktListItem> kontrakty)
        {
            InitializeComponent();
            txtPodtytul.Text = podtytul;
            lstKontrakty.ItemsSource = kontrakty;
            txtPusto.Visibility = kontrakty.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            lstKontrakty.SelectionChanged += (_, _) =>
                btnOk.IsEnabled = lstKontrakty.SelectedItem is KontraktListItem;
            if (kontrakty.Count > 0)
            {
                lstKontrakty.SelectedIndex = 0;
                Loaded += (_, _) => lstKontrakty.Focus();
            }
        }

        private void LstKontrakty_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstKontrakty.SelectedItem is KontraktListItem) BtnOk_Click(sender, new RoutedEventArgs());
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (lstKontrakty.SelectedItem is not KontraktListItem k) return;
            WybranyId = k.Id;
            DialogResult = true;
            Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
