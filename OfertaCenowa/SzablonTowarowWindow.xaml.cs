using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Wiersz produktu w szablonie (do wyświetlania w UI)
    /// </summary>
    public class ProduktWSzablonie : INotifyPropertyChanged
    {
        public int Lp { get; set; }
        public int TowarId { get; set; }
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string Katalog { get; set; } = "";
        public decimal Ilosc { get; set; }
        public decimal Cena { get; set; }
        public string Opakowanie { get; set; } = "E2";

        public string IloscStr => Ilosc == 0 ? "-" : $"{Ilosc:N0} kg";
        public string CenaStr => Cena == 0 ? "-" : $"{Cena:N2} zł";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Okno zarządzania szablonami towarów
    /// </summary>
    public partial class SzablonTowarowWindow : Window, INotifyPropertyChanged
    {
        private readonly SzablonyManager _szablonyManager = new();
        private readonly ObservableCollection<TowarOferta> _dostepneTowary;
        private ObservableCollection<SzablonTowarow> _szablony = new();
        private ObservableCollection<ProduktWSzablonie> _produktyWSzablonie = new();
        private SzablonTowarow? _aktualnyDoEdycji = null;
        private bool _trybEdycji = false;

        public ObservableCollection<TowarOferta> FiltrowaneTowary { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public SzablonTowarowWindow(ObservableCollection<TowarOferta> dostepneTowary)
        {
            InitializeComponent();
            DataContext = this;

            _dostepneTowary = dostepneTowary;
            FiltrowaneTowary = new ObservableCollection<TowarOferta>(dostepneTowary);

            WczytajSzablony();
            OdswiezListeProdukow();
        }

        private void WczytajSzablony()
        {
            var szablony = _szablonyManager.WczytajSzablonyTowarow();
            _szablony = new ObservableCollection<SzablonTowarow>(szablony);
            lstSzablony.ItemsSource = _szablony;
        }

        private void LstSzablony_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSzablony.SelectedItem is SzablonTowarow szablon)
            {
                btnUsunSzablon.IsEnabled = true;
                WczytajSzablonDoEdycji(szablon);
            }
            else
            {
                btnUsunSzablon.IsEnabled = false;
            }
        }

        private void WczytajSzablonDoEdycji(SzablonTowarow szablon)
        {
            _trybEdycji = true;
            _aktualnyDoEdycji = szablon;

            txtNazwaSzablonu.Text = szablon.Nazwa;
            txtOpisSzablonu.Text = szablon.Opis;

            _produktyWSzablonie.Clear();
            int lp = 1;
            foreach (var towar in szablon.Towary)
            {
                _produktyWSzablonie.Add(new ProduktWSzablonie
                {
                    Lp = lp++,
                    TowarId = towar.TowarId,
                    Kod = towar.Kod,
                    Nazwa = towar.Nazwa,
                    Katalog = towar.Katalog,
                    Ilosc = towar.DomyslnaIlosc,
                    Cena = towar.DomyslnaCena,
                    Opakowanie = towar.Opakowanie
                });
            }

            OdswiezListeProdukow();
        }

        private void BtnNowyMBtn_Click(object sender, RoutedEventArgs e)
        {
            _trybEdycji = false;
            _aktualnyDoEdycji = null;

            txtNazwaSzablonu.Text = "";
            txtOpisSzablonu.Text = "";
            _produktyWSzablonie.Clear();

            lstSzablony.SelectedItem = null;
            btnUsunSzablon.IsEnabled = false;

            OdswiezListeProdukow();
            txtNazwaSzablonu.Focus();
        }

        private void BtnDodajDoSzablonu_Click(object sender, RoutedEventArgs e)
        {
            if (cboDodajProdukt.SelectedItem is not TowarOferta towar)
            {
                MessageBox.Show("Wybierz produkt z listy.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Sprawdź czy już jest
            if (_produktyWSzablonie.Any(p => p.TowarId == towar.Id))
            {
                MessageBox.Show("Ten produkt jest już w szablonie.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Parsuj wartości
            decimal ilosc = 0;
            decimal cena = 0;

            if (!string.IsNullOrWhiteSpace(txtDodajIlosc.Text))
                decimal.TryParse(txtDodajIlosc.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out ilosc);

            if (!string.IsNullOrWhiteSpace(txtDodajCena.Text))
                decimal.TryParse(txtDodajCena.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out cena);

            string opakowanie = (cboDodajOpakowanie.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "E2";

            _produktyWSzablonie.Add(new ProduktWSzablonie
            {
                Lp = _produktyWSzablonie.Count + 1,
                TowarId = towar.Id,
                Kod = towar.Kod,
                Nazwa = towar.Nazwa,
                Katalog = towar.Katalog,
                Ilosc = ilosc,
                Cena = cena,
                Opakowanie = opakowanie
            });

            // Wyczyść pola
            cboDodajProdukt.SelectedItem = null;
            txtDodajIlosc.Text = "0";
            txtDodajCena.Text = "0";
            cboDodajOpakowanie.SelectedIndex = 0;

            OdswiezListeProdukow();
        }

        private void BtnUsunZSzablonu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProduktWSzablonie produkt)
            {
                _produktyWSzablonie.Remove(produkt);
                PrzenumerujProdukty();
                OdswiezListeProdukow();
            }
        }

        private void PrzenumerujProdukty()
        {
            int lp = 1;
            foreach (var p in _produktyWSzablonie)
                p.Lp = lp++;
        }

        private void OdswiezListeProdukow()
        {
            icProduktyWSzablonie.ItemsSource = null;
            icProduktyWSzablonie.ItemsSource = _produktyWSzablonie;
            placeholderBrakProduktow.Visibility = _produktyWSzablonie.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnZapiszSzablon_Click(object sender, RoutedEventArgs e)
        {
            string nazwa = txtNazwaSzablonu.Text.Trim();

            if (string.IsNullOrEmpty(nazwa))
            {
                MessageBox.Show("Podaj nazwę szablonu.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNazwaSzablonu.Focus();
                return;
            }

            if (_produktyWSzablonie.Count == 0)
            {
                MessageBox.Show("Dodaj przynajmniej jeden produkt do szablonu.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Utwórz lub aktualizuj szablon
            var szablon = _trybEdycji && _aktualnyDoEdycji != null
                ? _aktualnyDoEdycji
                : new SzablonTowarow { Id = _szablony.Count > 0 ? _szablony.Max(s => s.Id) + 1 : 1 };

            szablon.Nazwa = nazwa;
            szablon.Opis = txtOpisSzablonu.Text.Trim();
            szablon.Towary = _produktyWSzablonie.Select(p => new TowarSzablonu
            {
                TowarId = p.TowarId,
                Kod = p.Kod,
                Nazwa = p.Nazwa,
                Katalog = p.Katalog,
                DomyslnaIlosc = p.Ilosc,
                DomyslnaCena = p.Cena,
                Opakowanie = p.Opakowanie
            }).ToList();

            if (!_trybEdycji)
            {
                _szablony.Add(szablon);
            }

            // Zapisz wszystkie szablony
            _szablonyManager.ZapiszSzablonyTowarow(_szablony.ToList());

            MessageBox.Show($"Szablon \"{nazwa}\" został zapisany.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

            // Odśwież listę
            lstSzablony.ItemsSource = null;
            lstSzablony.ItemsSource = _szablony;
        }

        private void BtnUsunSzablon_Click(object sender, RoutedEventArgs e)
        {
            if (lstSzablony.SelectedItem is SzablonTowarow szablon)
            {
                var result = MessageBox.Show($"Czy na pewno usunąć szablon \"{szablon.Nazwa}\"?",
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _szablony.Remove(szablon);
                    _szablonyManager.ZapiszSzablonyTowarow(_szablony.ToList());

                    // Wyczyść edycję
                    BtnNowyMBtn_Click(sender, e);

                    lstSzablony.ItemsSource = null;
                    lstSzablony.ItemsSource = _szablony;
                }
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
