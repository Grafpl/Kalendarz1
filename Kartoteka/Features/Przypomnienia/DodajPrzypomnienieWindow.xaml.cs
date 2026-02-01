using System;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Kartoteka.Models;

namespace Kalendarz1.Kartoteka.Features.Przypomnienia
{
    public partial class DodajPrzypomnienieWindow : Window
    {
        private readonly int? _klientId;

        public Przypomnienie NowePrzypomnienie { get; private set; }

        public DodajPrzypomnienieWindow(int? klientId = null, string nazwaKlienta = null)
        {
            InitializeComponent();
            _klientId = klientId;

            dpData.SelectedDate = DateTime.Today.AddDays(1);

            if (!string.IsNullOrEmpty(nazwaKlienta))
                Title = $"Nowe przypomnienie — {nazwaKlienta}";
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTytul.Text))
            {
                MessageBox.Show("Podaj tytuł przypomnienia.", "Walidacja",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!dpData.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz datę przypomnienia.", "Walidacja",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string typ = "CUSTOM";
            if (cmbTyp.SelectedItem is ComboBoxItem typItem && typItem.Tag is string t)
                typ = t;

            NowePrzypomnienie = new Przypomnienie
            {
                KlientId = _klientId,
                Typ = typ,
                Tytul = txtTytul.Text.Trim(),
                Opis = string.IsNullOrWhiteSpace(txtOpis.Text) ? null : txtOpis.Text.Trim(),
                Priorytet = cmbPriorytet.SelectedIndex + 1,
                DataPrzypomnienia = dpData.SelectedDate.Value,
                DataUtworzenia = DateTime.Now
            };

            DialogResult = true;
            Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
