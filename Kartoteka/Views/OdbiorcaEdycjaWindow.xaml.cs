using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Kalendarz1.Kartoteka.Models;
using Kalendarz1.Kartoteka.Services;

namespace Kalendarz1.Kartoteka.Views
{
    public partial class OdbiorcaEdycjaWindow : Window
    {
        private readonly OdbiorcaHandlowca _odbiorca;
        private readonly KartotekaService _service;
        private readonly string _userName;
        private readonly HashSet<string> _highlightFields;

        public OdbiorcaEdycjaWindow(OdbiorcaHandlowca odbiorca, KartotekaService service, string userName)
            : this(odbiorca, service, userName, null) { }

        public OdbiorcaEdycjaWindow(OdbiorcaHandlowca odbiorca, KartotekaService service, string userName, IEnumerable<string> highlightFields)
        {
            InitializeComponent();
            _odbiorca = odbiorca;
            _service = service;
            _userName = userName;
            _highlightFields = highlightFields != null ? new HashSet<string>(highlightFields) : new HashSet<string>();

            LoadData();
        }

        private void LoadData()
        {
            TextNazwaFirmy.Text = _odbiorca.NazwaFirmy;
            TextDodatkoweInfo.Text = $"{_odbiorca.Miasto} • NIP: {_odbiorca.NIP} • {_odbiorca.Handlowiec}";

            TxtOsobaKontaktowa.Text = _odbiorca.OsobaKontaktowa;
            TxtTelefon.Text = _odbiorca.TelefonKontakt;
            TxtEmail.Text = _odbiorca.EmailKontakt;
            TxtAsortyment.Text = _odbiorca.Asortyment;
            TxtPakowanie.Text = _odbiorca.PreferencjePakowania;
            TxtJakosc.Text = _odbiorca.PreferencjeJakosci;
            TxtDniDostawy.Text = _odbiorca.PreferowanyDzienDostawy;
            TxtGodzinaDostawy.Text = _odbiorca.PreferowanaGodzinaDostawy;
            TxtTrasa.Text = _odbiorca.Trasa;
            TxtAdresDostawy.Text = _odbiorca.AdresDostawyInny;
            TxtPreferencjeDostawy.Text = _odbiorca.PreferencjeDostawy;
            TxtNotatki.Text = _odbiorca.Notatki;

            switch (_odbiorca.KategoriaHandlowca)
            {
                case "A": RadioKatA.IsChecked = true; break;
                case "B": RadioKatB.IsChecked = true; break;
                case "D": RadioKatD.IsChecked = true; break;
                default: RadioKatC.IsChecked = true; break;
            }

            // Apply pulsing highlight to fields that need attention
            if (_highlightFields.Count > 0)
            {
                if (_highlightFields.Contains("OsobaKontaktowa")) ApplyPulse(TxtOsobaKontaktowa);
                if (_highlightFields.Contains("Email")) ApplyPulse(TxtEmail);
                if (_highlightFields.Contains("Telefon")) ApplyPulse(TxtTelefon);
                if (_highlightFields.Contains("Asortyment")) ApplyPulse(TxtAsortyment);
                if (_highlightFields.Contains("DniDostawy")) ApplyPulse(TxtDniDostawy);
                if (_highlightFields.Contains("Notatki")) ApplyPulse(TxtNotatki);
            }
        }

        private void ApplyPulse(TextBox textBox)
        {
            textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // #DC2626
            textBox.BorderThickness = new Thickness(2);
            textBox.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242)); // #FEF2F2

            var storyboard = (Storyboard)FindResource("PulseRed");
            storyboard.Begin(textBox, true);

            // Stop pulsing when user starts typing
            textBox.TextChanged += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(textBox.Text))
                {
                    storyboard.Stop(textBox);
                    textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // green
                    textBox.BorderThickness = new Thickness(2);
                    textBox.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244)); // #F0FDF4
                }
            };
        }

        private async void ButtonZapisz_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _odbiorca.OsobaKontaktowa = TxtOsobaKontaktowa.Text;
                _odbiorca.TelefonKontakt = TxtTelefon.Text;
                _odbiorca.EmailKontakt = TxtEmail.Text;
                _odbiorca.Asortyment = TxtAsortyment.Text;
                _odbiorca.PreferencjePakowania = TxtPakowanie.Text;
                _odbiorca.PreferencjeJakosci = TxtJakosc.Text;
                _odbiorca.PreferowanyDzienDostawy = TxtDniDostawy.Text;
                _odbiorca.PreferowanaGodzinaDostawy = TxtGodzinaDostawy.Text;
                _odbiorca.Trasa = TxtTrasa.Text;
                _odbiorca.AdresDostawyInny = TxtAdresDostawy.Text;
                _odbiorca.PreferencjeDostawy = TxtPreferencjeDostawy.Text;
                _odbiorca.Notatki = TxtNotatki.Text;

                if (RadioKatA.IsChecked == true) _odbiorca.KategoriaHandlowca = "A";
                else if (RadioKatB.IsChecked == true) _odbiorca.KategoriaHandlowca = "B";
                else if (RadioKatD.IsChecked == true) _odbiorca.KategoriaHandlowca = "D";
                else _odbiorca.KategoriaHandlowca = "C";

                await _service.ZapiszDaneWlasneAsync(_odbiorca, _userName);

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
