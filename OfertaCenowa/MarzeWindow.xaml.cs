using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Kalendarz1.OfertaCenowa
{
    public class ProduktDoMarzy : INotifyPropertyChanged
    {
        private bool _zaznaczony;
        
        public TowarOfertaWiersz Produkt { get; set; } = null!;
        public string Kod => Produkt?.Kod ?? "";
        public string Nazwa => Produkt?.Nazwa ?? "";
        public string Ilosc => Produkt?.IloscStr ?? "";
        
        public bool Zaznaczony 
        { 
            get => _zaznaczony; 
            set { _zaznaczony = value; OnPropertyChanged(); } 
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class MarzeWindow : Window
    {
        public ObservableCollection<ProduktDoMarzy> Produkty { get; set; } = new();
        private decimal _cenaBazowa = 10.00m;

        public MarzeWindow(System.Collections.Generic.List<TowarOfertaWiersz> towary)
        {
            InitializeComponent();
            dgProdukty.ItemsSource = Produkty;

            foreach (var towar in towary)
            {
                Produkty.Add(new ProduktDoMarzy { Produkt = towar, Zaznaczony = false });
            }
        }

        private void BtnZaznaczWszystkie_Click(object sender, RoutedEventArgs e)
        {
            foreach (var produkt in Produkty)
                produkt.Zaznaczony = true;
        }

        private void BtnOdznaczWszystkie_Click(object sender, RoutedEventArgs e)
        {
            foreach (var produkt in Produkty)
                produkt.Zaznaczony = false;
        }

        private void BtnZastosuj_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtMarza.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal marza))
            {
                MessageBox.Show("Podaj poprawną wartość marży.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var zaznaczone = Produkty.Where(p => p.Zaznaczony).ToList();
            if (!zaznaczone.Any())
            {
                MessageBox.Show("Zaznacz przynajmniej jeden produkt.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var produkt in zaznaczone)
            {
                decimal nowaCena = _cenaBazowa * (1 + marza / 100);
                produkt.Produkt.CenaJednostkowa = Math.Round(nowaCena, 2);
            }

            MessageBox.Show($"Zastosowano marżę {marza}% dla {zaznaczone.Count} produktów.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
    }
}
