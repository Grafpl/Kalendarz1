using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Kalendarz1
{
    public partial class WydanieDialog : Window
    {
        public bool WszystkoWydane { get; private set; } = false;
        public bool Zatwierdzone { get; private set; } = false;
        public string UwagiWydania { get; private set; } = "";
        public ObservableCollection<WydanieItem> Pozycje { get; set; } = new();

        private readonly string _klientNazwa;
        private readonly decimal _sumaZamowienia;

        public WydanieDialog(string klientNazwa, List<(int TowarId, string Nazwa, decimal Zamowiono)> pozycje)
        {
            InitializeComponent();
            _klientNazwa = klientNazwa;

            // Załaduj pozycje
            foreach (var p in pozycje)
            {
                Pozycje.Add(new WydanieItem
                {
                    TowarId = p.TowarId,
                    Produkt = p.Nazwa,
                    Zamowiono = p.Zamowiono,
                    Wydano = p.Zamowiono // Domyślnie wydano = zamówiono
                });
            }

            _sumaZamowienia = pozycje.Sum(p => p.Zamowiono);

            txtKlientNazwa.Text = klientNazwa;
            txtSumaZamowienia.Text = $"{_sumaZamowienia:N0} kg";
            dgvWydania.ItemsSource = Pozycje;
        }

        private void btnTak_Click(object sender, RoutedEventArgs e)
        {
            WszystkoWydane = true;
            Zatwierdzone = true;
            DialogResult = true;
            Close();
        }

        private void btnNie_Click(object sender, RoutedEventArgs e)
        {
            // Pokaż tabelę do edycji
            pnlPytanie.Visibility = Visibility.Collapsed;
            pnlEdycja.Visibility = Visibility.Visible;
            pnlUwagi.Visibility = Visibility.Visible;
            pnlZatwierdzenie.Visibility = Visibility.Visible;

            // Powiększ okno
            this.Height = 650;
        }

        private void btnZatwierdz_Click(object sender, RoutedEventArgs e)
        {
            // Sprawdź czy są różnice
            bool maRoznice = Pozycje.Any(p => p.Zamowiono != p.Wydano);

            if (!maRoznice)
            {
                var result = MessageBox.Show(
                    "Nie wprowadzono żadnych różnic - czy wszystko było wydane zgodnie z zamówieniem?",
                    "Potwierdzenie",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    WszystkoWydane = true;
                    Zatwierdzone = true;
                    DialogResult = true;
                    Close();
                    return;
                }
                return;
            }

            WszystkoWydane = false;
            Zatwierdzone = true;
            UwagiWydania = txtUwagiWydania.Text?.Trim() ?? "";
            DialogResult = true;
            Close();
        }

        private void btnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            Zatwierdzone = false;
            DialogResult = false;
            Close();
        }
    }

    public class WydanieItem : INotifyPropertyChanged
    {
        public int TowarId { get; set; }
        public string Produkt { get; set; } = "";

        private decimal _zamowiono;
        public decimal Zamowiono
        {
            get => _zamowiono;
            set { _zamowiono = value; OnPropertyChanged(); OnPropertyChanged(nameof(RoznicaDisplay)); }
        }

        private decimal _wydano;
        public decimal Wydano
        {
            get => _wydano;
            set { _wydano = value; OnPropertyChanged(); OnPropertyChanged(nameof(RoznicaDisplay)); }
        }

        public decimal Roznica => Wydano - Zamowiono;

        public string RoznicaDisplay
        {
            get
            {
                var diff = Roznica;
                if (diff == 0) return "-";
                return diff > 0 ? $"+{diff:N0}" : $"{diff:N0}";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
