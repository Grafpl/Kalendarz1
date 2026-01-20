using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Kalendarz1.Zywiec.Kalendarz
{
    public partial class AnulujWstawienieDialog : Window
    {
        public bool AnulujTylkoJedna { get; private set; } = false;

        private List<DostawaInfo> _dostawy;

        public AnulujWstawienieDialog(
            List<(string LP, DateTime DataOdbioru, int Auta, double SztukiDek, double WagaDek, string Bufor, string Dostawca)> dostawy,
            string dostawcaNazwa)
        {
            InitializeComponent();

            _dostawy = dostawy.Select(d => new DostawaInfo
            {
                LP = d.LP,
                DataOdbioru = d.DataOdbioru,
                Auta = d.Auta,
                SztukiDek = d.SztukiDek,
                WagaDek = d.WagaDek,
                Bufor = d.Bufor,
                Dostawca = d.Dostawca
            }).ToList();

            // Ustaw dane
            txtDostawca.Text = $"Hodowca: {dostawcaNazwa}";
            txtInfo.Text = $"Znaleziono {dostawy.Count} dostaw powiązanych z tym wstawieniem:";

            // Wypełnij DataGrid
            dgDostawy.ItemsSource = _dostawy;

            // Oblicz sumy
            int sumaAuta = _dostawy.Sum(d => d.Auta);
            double sumaSztuki = _dostawy.Sum(d => d.SztukiDek);
            double sumaWaga = _dostawy.Sum(d => d.WagaDek);

            txtSumaAuta.Text = $"{sumaAuta} aut";
            txtSumaSztuki.Text = $"{sumaSztuki:N0} sztuk";
            txtSumaWaga.Text = $"{sumaWaga:N2} kg";
        }

        private void BtnAnulujWszystkie_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            AnulujTylkoJedna = false;
            Close();
        }

        private void BtnAnulujJedna_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            AnulujTylkoJedna = true;
            Close();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            AnulujTylkoJedna = false;
            Close();
        }

        // Klasa pomocnicza do wyświetlania w DataGrid
        public class DostawaInfo
        {
            public string LP { get; set; }
            public DateTime DataOdbioru { get; set; }
            public int Auta { get; set; }
            public double SztukiDek { get; set; }
            public double WagaDek { get; set; }
            public string Bufor { get; set; }
            public string Dostawca { get; set; }
        }
    }
}
