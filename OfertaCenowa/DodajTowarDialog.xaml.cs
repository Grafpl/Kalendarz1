using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Kalendarz1.OfertaCenowa
{
    public partial class DodajTowarDialog : Window
    {
        public TowarOferta? WybranyTowar { get; private set; }
        public decimal Ilosc { get; private set; }
        public decimal Cena { get; private set; }

        public DodajTowarDialog(List<TowarOferta> dostepneTowary)
        {
            InitializeComponent();

            cmbTowar.ItemsSource = dostepneTowary;

            txtIlosc.TextChanged += (s, e) => AktualizujWartosc();
            txtCena.TextChanged += (s, e) => AktualizujWartosc();
        }

        private void AktualizujWartosc()
        {
            if (decimal.TryParse(txtIlosc.Text, out decimal ilosc) &&
                decimal.TryParse(txtCena.Text, out decimal cena))
            {
                decimal wartosc = ilosc * cena;
                txtWartoscPozycji.Text = $"{wartosc:N2} zł";
            }
            else
            {
                txtWartoscPozycji.Text = "0,00 zł";
            }
        }

        private void BtnDodaj_Click(object sender, RoutedEventArgs e)
        {
            if (cmbTowar.SelectedItem == null)
            {
                MessageBox.Show("Wybierz produkt", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtIlosc.Text, out decimal ilosc) || ilosc <= 0)
            {
                MessageBox.Show("Podaj prawidłową ilość", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtCena.Text, out decimal cena) || cena <= 0)
            {
                MessageBox.Show("Podaj prawidłową cenę", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WybranyTowar = cmbTowar.SelectedItem as TowarOferta;
            Ilosc = ilosc;
            Cena = cena;

            this.DialogResult = true;
            this.Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}