using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.OfertaCenowa
{
    public partial class SzablonTowarowWindow : Window
    {
        private readonly SzablonyManager _manager;
        private ObservableCollection<SzablonTowarow> _szablony;
        private SzablonTowarow? _aktualnyszablon;
        private readonly List<TowarOferta> _dostepneTowary;

        public SzablonTowarowWindow(List<TowarOferta> dostepneTowary)
        {
            InitializeComponent();
            _manager = new SzablonyManager();
            _dostepneTowary = dostepneTowary;
            _szablony = new ObservableCollection<SzablonTowarow>();
            
            LoadSzablony();
        }

        private void LoadSzablony()
        {
            var szablony = _manager.WczytajSzablonyTowarow();
            _szablony.Clear();
            foreach (var szablon in szablony)
            {
                _szablony.Add(szablon);
            }
            lstSzablony.ItemsSource = _szablony;
        }

        private void LstSzablony_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSzablony.SelectedItem is SzablonTowarow szablon)
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

        private void WczytajSzablon(SzablonTowarow szablon)
        {
            txtNazwaSzablonu.Text = szablon.Nazwa;
            txtOpisSzablonu.Text = szablon.Opis;
            dgTowaryWSzablonie.ItemsSource = new ObservableCollection<TowarSzablonu>(szablon.Towary);
        }

        private void BtnNowySzablon_Click(object sender, RoutedEventArgs e)
        {
            var nowySzablon = new SzablonTowarow
            {
                Id = 0,
                Nazwa = "Nowy szablon",
                Opis = "",
                Towary = new List<TowarSzablonu>()
            };

            _aktualnyszablon = nowySzablon;
            WczytajSzablon(nowySzablon);
            panelEdycji.IsEnabled = true;
            btnZapisz.IsEnabled = true;
            txtNazwaSzablonu.Focus();
            txtNazwaSzablonu.SelectAll();
        }

        private void BtnDodajTowarDoSzablonu_Click(object sender, RoutedEventArgs e)
        {
            if (_aktualnyszablon == null) return;

            var oknoWyboru = new WyborTowarowWindow(_dostepneTowary);
            if (oknoWyboru.ShowDialog() == true && oknoWyboru.WybraneTowary.Any())
            {
                var aktualneLista = dgTowaryWSzablonie.ItemsSource as ObservableCollection<TowarSzablonu> 
                    ?? new ObservableCollection<TowarSzablonu>();

                foreach (var towar in oknoWyboru.WybraneTowary)
                {
                    // Sprawdź czy towar już nie jest w szablonie
                    if (!aktualneLista.Any(t => t.TowarId == towar.Id))
                    {
                        aktualneLista.Add(new TowarSzablonu
                        {
                            TowarId = towar.Id,
                            Kod = towar.Kod,
                            Nazwa = towar.Nazwa,
                            Katalog = towar.Katalog,
                            Opakowanie = towar.Opakowanie,
                            DomyslnaIlosc = 1,
                            DomyslnaCena = 0
                        });
                    }
                }

                dgTowaryWSzablonie.ItemsSource = aktualneLista;
                dgTowaryWSzablonie.Items.Refresh();
            }
        }

        private void BtnUsunTowarZSzablonu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TowarSzablonu towar)
            {
                var lista = dgTowaryWSzablonie.ItemsSource as ObservableCollection<TowarSzablonu>;
                if (lista != null)
                {
                    lista.Remove(towar);
                    dgTowaryWSzablonie.Items.Refresh();
                }
            }
        }

        private void BtnZapiszSzablon_Click(object sender, RoutedEventArgs e)
        {
            if (_aktualnyszablon == null) return;

            if (string.IsNullOrWhiteSpace(txtNazwaSzablonu.Text))
            {
                MessageBox.Show("Podaj nazwę szablonu.", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var towary = dgTowaryWSzablonie.ItemsSource as ObservableCollection<TowarSzablonu>;
            if (towary == null || !towary.Any())
            {
                MessageBox.Show("Dodaj przynajmniej jeden towar do szablonu.", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _aktualnyszablon.Nazwa = txtNazwaSzablonu.Text;
            _aktualnyszablon.Opis = txtOpisSzablonu.Text;
            _aktualnyszablon.Towary = towary.ToList();

            try
            {
                _manager.ZapiszSzablonTowarow(_aktualnyszablon);
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
                        _manager.UsunSzablonTowarow(id);
                        LoadSzablony();
                        
                        // Wyczyść panel edycji
                        _aktualnyszablon = null;
                        txtNazwaSzablonu.Clear();
                        txtOpisSzablonu.Clear();
                        dgTowaryWSzablonie.ItemsSource = null;
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
