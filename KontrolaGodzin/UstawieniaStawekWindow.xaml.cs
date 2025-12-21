using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;

namespace Kalendarz1.KontrolaGodzin
{
    public partial class UstawieniaStawekWindow : Window
    {
        public ObservableCollection<StawkaModel> Stawki { get; set; }
        public UstawieniaZUS UstawieniaZus { get; set; }
        
        private static readonly string _sciezkaUstawien = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZPSP", "ustawienia_stawek.json");

        public UstawieniaStawekWindow()
        {
            InitializeComponent();
            Stawki = new ObservableCollection<StawkaModel>();
            UstawieniaZus = new UstawieniaZUS();
            
            WczytajUstawienia();
            
            gridStawki.ItemsSource = Stawki;
        }

        private void WczytajUstawienia()
        {
            try
            {
                if (File.Exists(_sciezkaUstawien))
                {
                    var json = File.ReadAllText(_sciezkaUstawien);
                    var dane = JsonSerializer.Deserialize<DaneUstawien>(json);
                    
                    if (dane?.Stawki != null)
                    {
                        foreach (var s in dane.Stawki)
                            Stawki.Add(s);
                    }
                    
                    if (dane?.Zus != null)
                    {
                        UstawieniaZus = dane.Zus;
                    }
                }
                else
                {
                    // Domyślne stawki
                    DodajDomyslneStawki();
                }

                // Ustaw wartości ZUS w kontrolkach
                txtZusPracownik.Text = UstawieniaZus.ZusPracownik.ToString("N2");
                txtZusPracodawca.Text = UstawieniaZus.ZusPracodawca.ToString("N2");
                txtPodatek.Text = UstawieniaZus.Podatek.ToString("N2");
                txtKwotaWolna.Text = UstawieniaZus.KwotaWolna.ToString("N2");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania ustawień: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                DodajDomyslneStawki();
            }
        }

        private void DodajDomyslneStawki()
        {
            Stawki.Clear();
            
            // Agencje
            Stawki.Add(new StawkaModel { Nazwa = "GURAVO", StawkaPodstawowa = 28.10m, MnoznikNadgodzin = 1.5m, MnoznikNocne = 1.2m, MnoznikSwiateczne = 2.0m, OdDaty = new DateTime(2024, 1, 1) });
            Stawki.Add(new StawkaModel { Nazwa = "AGENCJA IMPULS", StawkaPodstawowa = 27.50m, MnoznikNadgodzin = 1.5m, MnoznikNocne = 1.2m, MnoznikSwiateczne = 2.0m, OdDaty = new DateTime(2024, 1, 1) });
            Stawki.Add(new StawkaModel { Nazwa = "AGENCJA STAR-POL", StawkaPodstawowa = 28.00m, MnoznikNadgodzin = 1.5m, MnoznikNocne = 1.2m, MnoznikSwiateczne = 2.0m, OdDaty = new DateTime(2024, 1, 1) });
            Stawki.Add(new StawkaModel { Nazwa = "AGENCJA ECO-MEN", StawkaPodstawowa = 27.80m, MnoznikNadgodzin = 1.5m, MnoznikNocne = 1.2m, MnoznikSwiateczne = 2.0m, OdDaty = new DateTime(2024, 1, 1) });
            Stawki.Add(new StawkaModel { Nazwa = "AGENCJA ROB-JOB", StawkaPodstawowa = 28.50m, MnoznikNadgodzin = 1.5m, MnoznikNocne = 1.2m, MnoznikSwiateczne = 2.0m, OdDaty = new DateTime(2024, 1, 1) });
            
            // Własne działy
            Stawki.Add(new StawkaModel { Nazwa = "PRODUKCJA", StawkaPodstawowa = 30.00m, MnoznikNadgodzin = 1.5m, MnoznikNocne = 1.2m, MnoznikSwiateczne = 2.0m, OdDaty = new DateTime(2024, 1, 1) });
            Stawki.Add(new StawkaModel { Nazwa = "MECHANIK", StawkaPodstawowa = 35.00m, MnoznikNadgodzin = 1.5m, MnoznikNocne = 1.2m, MnoznikSwiateczne = 2.0m, OdDaty = new DateTime(2024, 1, 1) });
            Stawki.Add(new StawkaModel { Nazwa = "BIURO", StawkaPodstawowa = 32.00m, MnoznikNadgodzin = 1.5m, MnoznikNocne = 1.2m, MnoznikSwiateczne = 2.0m, OdDaty = new DateTime(2024, 1, 1) });
            Stawki.Add(new StawkaModel { Nazwa = "MYJKA", StawkaPodstawowa = 28.00m, MnoznikNadgodzin = 1.5m, MnoznikNocne = 1.2m, MnoznikSwiateczne = 2.0m, OdDaty = new DateTime(2024, 1, 1) });
            Stawki.Add(new StawkaModel { Nazwa = "CZYSTA", StawkaPodstawowa = 29.00m, MnoznikNadgodzin = 1.5m, MnoznikNocne = 1.2m, MnoznikSwiateczne = 2.0m, OdDaty = new DateTime(2024, 1, 1) });
            Stawki.Add(new StawkaModel { Nazwa = "BRUDNA", StawkaPodstawowa = 29.00m, MnoznikNadgodzin = 1.5m, MnoznikNocne = 1.2m, MnoznikSwiateczne = 2.0m, OdDaty = new DateTime(2024, 1, 1) });
            
            // Domyślna (fallback)
            Stawki.Add(new StawkaModel { Nazwa = "-- DOMYŚLNA --", StawkaPodstawowa = 28.10m, MnoznikNadgodzin = 1.5m, MnoznikNocne = 1.2m, MnoznikSwiateczne = 2.0m, OdDaty = new DateTime(2020, 1, 1) });
        }

        private void BtnDodajStawke_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new EdycjaStawkiWindow(null);
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true && dialog.Stawka != null)
            {
                Stawki.Add(dialog.Stawka);
            }
        }

        private void BtnEdytujStawke_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var stawka = button?.Tag as StawkaModel;
            
            if (stawka != null)
            {
                var dialog = new EdycjaStawkiWindow(stawka);
                dialog.Owner = this;
                
                if (dialog.ShowDialog() == true && dialog.Stawka != null)
                {
                    var index = Stawki.IndexOf(stawka);
                    if (index >= 0)
                    {
                        Stawki[index] = dialog.Stawka;
                    }
                }
            }
        }

        private void BtnUsunStawke_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var stawka = button?.Tag as StawkaModel;
            
            if (stawka != null)
            {
                if (MessageBox.Show($"Czy na pewno usunąć stawkę dla '{stawka.Nazwa}'?", 
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Stawki.Remove(stawka);
                }
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Parsuj wartości ZUS
                UstawieniaZus.ZusPracownik = decimal.Parse(txtZusPracownik.Text.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                UstawieniaZus.ZusPracodawca = decimal.Parse(txtZusPracodawca.Text.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                UstawieniaZus.Podatek = decimal.Parse(txtPodatek.Text.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                UstawieniaZus.KwotaWolna = decimal.Parse(txtKwotaWolna.Text.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);

                // Zapisz do pliku
                var dane = new DaneUstawien
                {
                    Stawki = Stawki.ToList(),
                    Zus = UstawieniaZus
                };

                var folder = Path.GetDirectoryName(_sciezkaUstawien);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var json = JsonSerializer.Serialize(dane, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_sciezkaUstawien, json);

                MessageBox.Show("Ustawienia zapisane!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisywania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Statyczna metoda do pobierania stawki dla danego działu i daty
        /// </summary>
        public static StawkaModel PobierzStawke(string nazwaGrupy, DateTime data)
        {
            try
            {
                if (File.Exists(_sciezkaUstawien))
                {
                    var json = File.ReadAllText(_sciezkaUstawien);
                    var dane = JsonSerializer.Deserialize<DaneUstawien>(json);

                    if (dane?.Stawki != null)
                    {
                        // Szukaj stawki pasującej do nazwy i daty
                        var stawka = dane.Stawki
                            .Where(s => nazwaGrupy?.ToUpper().Contains(s.Nazwa?.ToUpper() ?? "") == true)
                            .Where(s => s.OdDaty <= data && (s.DoDaty == null || s.DoDaty >= data))
                            .OrderByDescending(s => s.OdDaty)
                            .FirstOrDefault();

                        if (stawka != null)
                            return stawka;

                        // Fallback - domyślna
                        return dane.Stawki.FirstOrDefault(s => s.Nazwa == "-- DOMYŚLNA --") 
                               ?? new StawkaModel { StawkaPodstawowa = 28.10m, MnoznikNadgodzin = 1.5m, MnoznikNocne = 1.2m, MnoznikSwiateczne = 2.0m };
                    }
                }
            }
            catch { }

            // Domyślna stawka gdy brak pliku
            return new StawkaModel { StawkaPodstawowa = 28.10m, MnoznikNadgodzin = 1.5m, MnoznikNocne = 1.2m, MnoznikSwiateczne = 2.0m };
        }

        /// <summary>
        /// Pobiera ustawienia ZUS
        /// </summary>
        public static UstawieniaZUS PobierzUstawieniaZus()
        {
            try
            {
                if (File.Exists(_sciezkaUstawien))
                {
                    var json = File.ReadAllText(_sciezkaUstawien);
                    var dane = JsonSerializer.Deserialize<DaneUstawien>(json);
                    return dane?.Zus ?? new UstawieniaZUS();
                }
            }
            catch { }

            return new UstawieniaZUS();
        }
    }

    #region Models

    public class StawkaModel
    {
        public string Nazwa { get; set; }
        public decimal StawkaPodstawowa { get; set; }
        public decimal MnoznikNadgodzin { get; set; } = 1.5m;
        public decimal MnoznikNocne { get; set; } = 1.2m;
        public decimal MnoznikSwiateczne { get; set; } = 2.0m;
        public DateTime OdDaty { get; set; } = DateTime.Today;
        public DateTime? DoDaty { get; set; }

        public decimal StawkaNadgodzin => StawkaPodstawowa * MnoznikNadgodzin;
        public decimal StawkaNocna => StawkaPodstawowa * MnoznikNocne;
        public decimal StawkaSwiateczna => StawkaPodstawowa * MnoznikSwiateczne;
    }

    public class UstawieniaZUS
    {
        public decimal ZusPracownik { get; set; } = 13.71m;
        public decimal ZusPracodawca { get; set; } = 20.48m;
        public decimal Podatek { get; set; } = 12.00m;
        public decimal KwotaWolna { get; set; } = 300.00m;
    }

    public class DaneUstawien
    {
        public List<StawkaModel> Stawki { get; set; }
        public UstawieniaZUS Zus { get; set; }
    }

    #endregion
}
