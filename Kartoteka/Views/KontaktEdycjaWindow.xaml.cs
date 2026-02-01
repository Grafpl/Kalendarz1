using System;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Kartoteka.Models;
using Kalendarz1.Kartoteka.Services;

namespace Kalendarz1.Kartoteka.Views
{
    public partial class KontaktEdycjaWindow : Window
    {
        private readonly KontaktOdbiorcy _kontakt;
        private readonly KartotekaService _service;

        public KontaktEdycjaWindow(KontaktOdbiorcy kontakt)
        {
            InitializeComponent();
            _kontakt = kontakt;

            var connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
            var connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
            _service = new KartotekaService(connLibra, connHandel);

            if (kontakt.Id > 0)
            {
                TextTytul.Text = $"Edycja kontaktu - {kontakt.PelneNazwisko}";
                LoadData();
            }
            else
            {
                TextTytul.Text = "Nowy kontakt";
            }
        }

        private void LoadData()
        {
            // Wybierz typ kontaktu
            for (int i = 0; i < CmbTypKontaktu.Items.Count; i++)
            {
                var item = CmbTypKontaktu.Items[i] as ComboBoxItem;
                if (item?.Content?.ToString() == _kontakt.TypKontaktu)
                {
                    CmbTypKontaktu.SelectedIndex = i;
                    break;
                }
            }

            TxtImie.Text = _kontakt.Imie;
            TxtNazwisko.Text = _kontakt.Nazwisko;
            TxtStanowisko.Text = _kontakt.Stanowisko;
            TxtTelefon.Text = _kontakt.Telefon;
            TxtEmail.Text = _kontakt.Email;
            TxtNotatka.Text = _kontakt.Notatka;
        }

        private async void ButtonZapisz_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _kontakt.TypKontaktu = (CmbTypKontaktu.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Inny";
                _kontakt.Imie = TxtImie.Text;
                _kontakt.Nazwisko = TxtNazwisko.Text;
                _kontakt.Stanowisko = TxtStanowisko.Text;
                _kontakt.Telefon = TxtTelefon.Text;
                _kontakt.Email = TxtEmail.Text;
                _kontakt.Notatka = TxtNotatka.Text;

                await _service.ZapiszKontaktAsync(_kontakt);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ButtonAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
