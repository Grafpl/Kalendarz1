using System;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.CRM
{
    public partial class UstawDateKontaktuDialog : Window
    {
        public DateTime? WybranaData { get; private set; }

        public UstawDateKontaktuDialog(string nazwaKlienta)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            txtNazwaKlienta.Text = nazwaKlienta;
            calendar.SelectedDate = DateTime.Today.AddDays(1);
            AktualizujWybranaData(DateTime.Today.AddDays(1));
        }

        private void AktualizujWybranaData(DateTime data)
        {
            WybranaData = data;

            string dzienTygodnia = data.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));
            dzienTygodnia = char.ToUpper(dzienTygodnia[0]) + dzienTygodnia.Substring(1);

            if (data.Date == DateTime.Today)
                txtWybranaData.Text = $"Dziś ({data:dd.MM.yyyy})";
            else if (data.Date == DateTime.Today.AddDays(1))
                txtWybranaData.Text = $"Jutro ({data:dd.MM.yyyy})";
            else
                txtWybranaData.Text = $"{dzienTygodnia}, {data:dd.MM.yyyy}";

            calendar.SelectedDate = data;
        }

        private void BtnDzis_Click(object sender, RoutedEventArgs e)
        {
            AktualizujWybranaData(DateTime.Today);
        }

        private void BtnJutro_Click(object sender, RoutedEventArgs e)
        {
            AktualizujWybranaData(DateTime.Today.AddDays(1));
        }

        private void BtnZa2Dni_Click(object sender, RoutedEventArgs e)
        {
            AktualizujWybranaData(DateTime.Today.AddDays(2));
        }

        private void BtnZaTydzien_Click(object sender, RoutedEventArgs e)
        {
            AktualizujWybranaData(DateTime.Today.AddDays(7));
        }

        private void BtnZa2Tygodnie_Click(object sender, RoutedEventArgs e)
        {
            AktualizujWybranaData(DateTime.Today.AddDays(14));
        }

        private void BtnZaMiesiac_Click(object sender, RoutedEventArgs e)
        {
            AktualizujWybranaData(DateTime.Today.AddMonths(1));
        }

        private void BtnZa3Miesiace_Click(object sender, RoutedEventArgs e)
        {
            AktualizujWybranaData(DateTime.Today.AddMonths(3));
        }

        private void Calendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (calendar.SelectedDate.HasValue)
            {
                AktualizujWybranaData(calendar.SelectedDate.Value);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            WybranaData = null;
            DialogResult = false;
            Close();
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (!WybranaData.HasValue)
            {
                MessageBox.Show("Wybierz datę kontaktu.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
