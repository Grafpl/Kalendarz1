using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.OfertaCenowa
{
    public partial class SzablonParametrowWindow : Window
    {
        private readonly SzablonyManager _manager;
        private ObservableCollection<SzablonParametrow> _szablony;
        private SzablonParametrow? _aktualnyszablon;

        public SzablonParametrowWindow()
        {
            InitializeComponent();
            _manager = new SzablonyManager();
            _szablony = new ObservableCollection<SzablonParametrow>();
            
            LoadSzablony();
        }

        private void LoadSzablony()
        {
            var szablony = _manager.WczytajSzablonyParametrow();
            _szablony.Clear();
            foreach (var szablon in szablony)
            {
                _szablony.Add(szablon);
            }
            lstSzablony.ItemsSource = _szablony;
        }

        private void LstSzablony_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSzablony.SelectedItem is SzablonParametrow szablon)
            {
                _aktualnyszablon = szablon;
                WczytajSzablon(szablon);
                panelEdycji.IsEnabled = true;
                btnZapisz.IsEnabled = true;
            }
            else
            {
                panelEdycji.IsEnabled = false;
                btnZapisz.IsEnabled = false;
            }
        }

        private void WczytajSzablon(SzablonParametrow szablon)
        {
            txtNazwaSzablonu.Text = szablon.Nazwa;
            
            // Termin płatności
            foreach (ComboBoxItem item in cboTerminPlatnosci.Items)
            {
                if (item.Tag.ToString() == szablon.DniPlatnosci.ToString())
                {
                    cboTerminPlatnosci.SelectedItem = item;
                    break;
                }
            }

            // Konto
            foreach (ComboBoxItem item in cboKontoBankowe.Items)
            {
                if (item.Tag.ToString() == szablon.WalutaKonta)
                {
                    cboKontoBankowe.SelectedItem = item;
                    break;
                }
            }

            // Transport
            rbTransportWlasny.IsChecked = szablon.TransportTyp == "wlasny";
            rbTransportKlienta.IsChecked = szablon.TransportTyp == "klienta";

            // Język
            foreach (ComboBoxItem item in cboJezykPDF.Items)
            {
                if (item.Tag.ToString() == szablon.Jezyk.ToString())
                {
                    cboJezykPDF.SelectedItem = item;
                    break;
                }
            }

            // Logo
            foreach (ComboBoxItem item in cboTypLogo.Items)
            {
                if (item.Tag.ToString() == szablon.TypLogo.ToString())
                {
                    cboTypLogo.SelectedItem = item;
                    break;
                }
            }

            // Widoczność
            chkPokazOpakowanie.IsChecked = szablon.PokazOpakowanie;
            chkPokazCene.IsChecked = szablon.PokazCene;
            chkPokazIlosc.IsChecked = szablon.PokazIlosc;
            chkPokazTermin.IsChecked = szablon.PokazTerminPlatnosci;

            // Notatki
            chkDodajNotkeOCenach.IsChecked = szablon.DodajNotkeOCenach;
            txtNotatkaCustom.Text = szablon.NotatkaCustom;
        }

        private void BtnNowySzablon_Click(object sender, RoutedEventArgs e)
        {
            var nowySzablon = new SzablonParametrow
            {
                Id = 0,
                Nazwa = "Nowy szablon",
                TerminPlatnosci = "7 dni",
                DniPlatnosci = 7,
                WalutaKonta = "PLN",
                Jezyk = JezykOferty.Polski,
                TypLogo = TypLogo.Okragle,
                PokazOpakowanie = false,
                PokazCene = true,
                PokazIlosc = false,
                PokazTerminPlatnosci = true,
                TransportTyp = "wlasny",
                DodajNotkeOCenach = true,
                NotatkaCustom = ""
            };

            _aktualnyszablon = nowySzablon;
            WczytajSzablon(nowySzablon);
            panelEdycji.IsEnabled = true;
            btnZapisz.IsEnabled = true;
            txtNazwaSzablonu.Focus();
            txtNazwaSzablonu.SelectAll();
        }

        private void CboTerminPlatnosci_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Aktualizuje tekst terminu na podstawie wybranej opcji
        }

        private void BtnZapiszSzablon_Click(object sender, RoutedEventArgs e)
        {
            if (_aktualnyszablon == null) return;

            if (string.IsNullOrWhiteSpace(txtNazwaSzablonu.Text))
            {
                MessageBox.Show("Podaj nazwę szablonu.", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _aktualnyszablon.Nazwa = txtNazwaSzablonu.Text;

            // Termin płatności
            var terminItem = cboTerminPlatnosci.SelectedItem as ComboBoxItem;
            _aktualnyszablon.TerminPlatnosci = terminItem?.Content.ToString() ?? "7 dni";
            _aktualnyszablon.DniPlatnosci = int.Parse(terminItem?.Tag.ToString() ?? "7");

            // Konto
            _aktualnyszablon.WalutaKonta = (cboKontoBankowe.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "PLN";

            // Transport
            _aktualnyszablon.TransportTyp = rbTransportWlasny.IsChecked == true ? "wlasny" : "klienta";

            // Język
            string jezykTag = (cboJezykPDF.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "Polski";
            _aktualnyszablon.Jezyk = jezykTag == "English" ? JezykOferty.English : JezykOferty.Polski;

            // Logo
            string logoTag = (cboTypLogo.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "Okragle";
            _aktualnyszablon.TypLogo = logoTag == "Dlugie" ? TypLogo.Dlugie : TypLogo.Okragle;

            // Widoczność
            _aktualnyszablon.PokazOpakowanie = chkPokazOpakowanie.IsChecked == true;
            _aktualnyszablon.PokazCene = chkPokazCene.IsChecked == true;
            _aktualnyszablon.PokazIlosc = chkPokazIlosc.IsChecked == true;
            _aktualnyszablon.PokazTerminPlatnosci = chkPokazTermin.IsChecked == true;

            // Notatki
            _aktualnyszablon.DodajNotkeOCenach = chkDodajNotkeOCenach.IsChecked == true;
            _aktualnyszablon.NotatkaCustom = txtNotatkaCustom.Text;

            try
            {
                _manager.ZapiszSzablonParametrow(_aktualnyszablon);
                MessageBox.Show("Szablon został zapisany pomyślnie!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadSzablony();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnUsunSzablon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                var szablon = _szablony.FirstOrDefault(s => s.Id == id);
                if (szablon == null) return;

                var wynik = MessageBox.Show(
                    $"Czy na pewno chcesz usunąć szablon \"{szablon.Nazwa}\"?",
                    "Potwierdzenie usunięcia",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (wynik == MessageBoxResult.Yes)
                {
                    try
                    {
                        _manager.UsunSzablonParametrow(id);
                        LoadSzablony();
                        
                        // Wyczyść panel edycji
                        _aktualnyszablon = null;
                        txtNazwaSzablonu.Clear();
                        txtNotatkaCustom.Clear();
                        panelEdycji.IsEnabled = false;
                        btnZapisz.IsEnabled = false;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd podczas usuwania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
