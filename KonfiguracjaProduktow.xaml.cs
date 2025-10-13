using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class KonfiguracjaProduktow : Window
    {
        private string connectionString;
        private string connectionStringHandel;
        private ObservableCollection<ProduktModel> dostepneProdukty;
        private ObservableCollection<ProduktModel> wszystkieProdukty;
        private ObservableCollection<ProduktKonfiguracjaModel> skonfigurowaneProdukty;
        private Dictionary<string, decimal> konfiguracjaProcentow;

        public KonfiguracjaProduktow(string connString, string connStringHandel, Dictionary<string, decimal> konfig)
        {
            InitializeComponent();
            connectionString = connString;
            connectionStringHandel = connStringHandel;
            konfiguracjaProcentow = konfig;

            dostepneProdukty = new ObservableCollection<ProduktModel>();
            wszystkieProdukty = new ObservableCollection<ProduktModel>();
            skonfigurowaneProdukty = new ObservableCollection<ProduktKonfiguracjaModel>();

            WczytajProdukty();
            WczytajKonfiguracje();

            listDostepne.ItemsSource = dostepneProdukty;
            listSkonfigurowane.ItemsSource = skonfigurowaneProdukty;

            ObliczSumeProcentow();
        }

        private void WczytajProdukty()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringHandel))
                {
                    conn.Open();

                    // Pobierz produkty z katalogu 67095 (drobne elementy)
                    string query = @"
                        SELECT id, kod, nazwa 
                        FROM HM.TW 
                        WHERE katalog = '67095' 
                        AND aktywny = 1
                        ORDER BY nazwa";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var produkt = new ProduktModel
                            {
                                ID = Convert.ToInt32(reader["id"]),
                                Kod = reader["kod"].ToString(),
                                Nazwa = reader["nazwa"].ToString()
                            };

                            dostepneProdukty.Add(produkt);
                            wszystkieProdukty.Add(produkt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania produktów: {ex.Message}",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajKonfiguracje()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT k.ID, k.TowarID, k.NazwaTowaru, k.ProcentUdzialu
                        FROM KonfiguracjaProduktow k
                        WHERE k.Aktywny = 1
                        ORDER BY k.ProcentUdzialu DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int towarID = Convert.ToInt32(reader["TowarID"]);
                            string nazwa = reader["NazwaTowaru"].ToString();

                            // Znajdź kod produktu
                            var produkt = wszystkieProdukty.FirstOrDefault(p => p.ID == towarID);
                            string kod = produkt?.Kod ?? "";

                            skonfigurowaneProdukty.Add(new ProduktKonfiguracjaModel
                            {
                                ID = Convert.ToInt32(reader["ID"]),
                                TowarID = towarID,
                                Kod = kod,
                                Nazwa = nazwa,
                                Procent = Convert.ToDecimal(reader["ProcentUdzialu"])
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania konfiguracji: {ex.Message}\n\nKonfiguracja będzie pusta.",
                              "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            string szukaj = txtSzukaj.Text.ToLower().Trim();

            dostepneProdukty.Clear();

            if (string.IsNullOrWhiteSpace(szukaj))
            {
                foreach (var produkt in wszystkieProdukty)
                {
                    dostepneProdukty.Add(produkt);
                }
            }
            else
            {
                var filtrowane = wszystkieProdukty
                    .Where(p => p.Nazwa.ToLower().Contains(szukaj) ||
                               p.Kod.ToLower().Contains(szukaj))
                    .ToList();

                foreach (var produkt in filtrowane)
                {
                    dostepneProdukty.Add(produkt);
                }
            }
        }

        private void BtnDodaj_Click(object sender, RoutedEventArgs e)
        {
            var wybrany = listDostepne.SelectedItem as ProduktModel;
            if (wybrany == null)
            {
                MessageBox.Show("Wybierz produkt z listy dostępnych produktów.",
                              "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Sprawdź czy już nie jest dodany
            if (skonfigurowaneProdukty.Any(p => p.TowarID == wybrany.ID))
            {
                MessageBox.Show("Ten produkt jest już w konfiguracji.",
                              "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Dodaj do konfiguracji z domyślnym 0%
            skonfigurowaneProdukty.Add(new ProduktKonfiguracjaModel
            {
                TowarID = wybrany.ID,
                Kod = wybrany.Kod,
                Nazwa = wybrany.Nazwa,
                Procent = 0m
            });

            ObliczSumeProcentow();

            // Wyczyść zaznaczenie
            listDostepne.SelectedItem = null;
        }

        private void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var produkt = button?.Tag as ProduktKonfiguracjaModel;

            if (produkt != null)
            {
                var result = MessageBox.Show(
                    $"Czy na pewno usunąć produkt z konfiguracji?\n\n" +
                    $"Produkt: {produkt.Nazwa}\n" +
                    $"Kod: {produkt.Kod}\n" +
                    $"Aktualny udział: {produkt.Procent:F1}%",
                    "Potwierdzenie usunięcia",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    skonfigurowaneProdukty.Remove(produkt);
                    ObliczSumeProcentow();
                }
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ObliczSumeProcentow();
        }

        private void ObliczSumeProcentow()
        {
            if (txtSumaProcentow == null) return;

            decimal suma = skonfigurowaneProdukty.Sum(p => p.Procent);
            txtSumaProcentow.Text = $"{suma:F1}%";

            // Zmień kolor w zależności od sumy
            SolidColorBrush kolorTekstu;
            SolidColorBrush kolorTla;
            SolidColorBrush kolorObramowania;

            if (suma >= 93 && suma <= 95)
            {
                // Idealnie - zielony
                kolorTekstu = new SolidColorBrush(Color.FromRgb(92, 138, 58));
                kolorTla = new SolidColorBrush(Color.FromRgb(232, 245, 233));
                kolorObramowania = new SolidColorBrush(Color.FromRgb(92, 138, 58));
            }
            else if (suma >= 90 && suma <= 98)
            {
                // Dopuszczalne - pomarańczowy
                kolorTekstu = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                kolorTla = new SolidColorBrush(Color.FromRgb(255, 243, 205));
                kolorObramowania = new SolidColorBrush(Color.FromRgb(255, 193, 7));
            }
            else
            {
                // Niedopuszczalne - czerwony
                kolorTekstu = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                kolorTla = new SolidColorBrush(Color.FromRgb(255, 235, 238));
                kolorObramowania = new SolidColorBrush(Color.FromRgb(231, 76, 60));
            }

            txtSumaProcentow.Foreground = kolorTekstu;
            borderSumaInfo.Background = kolorTla;
            borderSumaInfo.BorderBrush = kolorObramowania;
        }

        private void BtnPrzywrocDomyslne_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Czy na pewno przywrócić domyślną konfigurację?\n\n" +
                "Domyślne wartości:\n" +
                "• Ćwiartka: 38%\n" +
                "• Skrzydło: 9%\n" +
                "• Filet: 28%\n" +
                "• Korpus: 19%\n\n" +
                "Aktualna konfiguracja zostanie usunięta.",
                "Potwierdzenie przywrócenia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                skonfigurowaneProdukty.Clear();

                // Znajdź domyślne produkty
                var domyslne = new Dictionary<string, decimal>
                {
                    { "Ćwiartka", 38m },
                    { "Skrzydło", 9m },
                    { "Filet", 28m },
                    { "Korpus", 19m }
                };

                foreach (var domyslny in domyslne)
                {
                    var produkt = wszystkieProdukty.FirstOrDefault(p =>
                        p.Nazwa.Contains(domyslny.Key, StringComparison.OrdinalIgnoreCase));

                    if (produkt != null)
                    {
                        skonfigurowaneProdukty.Add(new ProduktKonfiguracjaModel
                        {
                            TowarID = produkt.ID,
                            Kod = produkt.Kod,
                            Nazwa = produkt.Nazwa,
                            Procent = domyslny.Value
                        });
                    }
                    else
                    {
                        MessageBox.Show($"Nie znaleziono produktu: {domyslny.Key}\n\n" +
                                      $"Musisz dodać go ręcznie z listy dostępnych produktów.",
                                      "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                ObliczSumeProcentow();
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            // Walidacja
            decimal suma = skonfigurowaneProdukty.Sum(p => p.Procent);

            if (suma < 90 || suma > 98)
            {
                var result = MessageBox.Show(
                    $"Suma procentów wynosi {suma:F1}%, co jest poza zalecanym zakresem (90-98%).\n\n" +
                    $"Czy na pewno chcesz zapisać tę konfigurację?",
                    "Ostrzeżenie",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                    return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (SqlTransaction trans = conn.BeginTransaction())
                    {
                        try
                        {
                            // Dezaktywuj wszystkie istniejące
                            string deactivateQuery = "UPDATE KonfiguracjaProduktow SET Aktywny = 0";
                            using (SqlCommand cmd = new SqlCommand(deactivateQuery, conn, trans))
                            {
                                cmd.ExecuteNonQuery();
                            }

                            // Wstaw nowe lub zaktualizuj istniejące
                            foreach (var produkt in skonfigurowaneProdukty)
                            {
                                string query = @"
                                    IF EXISTS (SELECT 1 FROM KonfiguracjaProduktow WHERE TowarID = @TowarID)
                                    BEGIN
                                        UPDATE KonfiguracjaProduktow 
                                        SET ProcentUdzialu = @Procent, 
                                            Aktywny = 1,
                                            DataModyfikacji = GETDATE(),
                                            ModyfikowalPrzez = @User
                                        WHERE TowarID = @TowarID
                                    END
                                    ELSE
                                    BEGIN
                                        INSERT INTO KonfiguracjaProduktow 
                                            (TowarID, NazwaTowaru, ProcentUdzialu, Aktywny, ModyfikowalPrzez, DataModyfikacji)
                                        VALUES (@TowarID, @Nazwa, @Procent, 1, @User, GETDATE())
                                    END";

                                using (SqlCommand cmd = new SqlCommand(query, conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@TowarID", produkt.TowarID);
                                    cmd.Parameters.AddWithValue("@Nazwa", produkt.Nazwa);
                                    cmd.Parameters.AddWithValue("@Procent", produkt.Procent);
                                    cmd.Parameters.AddWithValue("@User", Environment.UserName);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            trans.Commit();

                            MessageBox.Show(
                                $"✓ Konfiguracja została zapisana pomyślnie!\n\n" +
                                $"Liczba produktów: {skonfigurowaneProdukty.Count}\n" +
                                $"Suma procentów: {suma:F1}%\n\n" +
                                $"Zmiany będą widoczne w planie tygodniowym.",
                                "Sukces",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            DialogResult = true;
                            Close();
                        }
                        catch
                        {
                            trans.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisywania konfiguracji:\n\n{ex.Message}",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            if (skonfigurowaneProdukty.Count > 0)
            {
                var result = MessageBox.Show(
                    "Czy na pewno chcesz zamknąć okno bez zapisywania?\n\n" +
                    "Wszystkie niezapisane zmiany zostaną utracone.",
                    "Potwierdzenie",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                    return;
            }

            DialogResult = false;
            Close();
        }
    }

    // Modele danych
    public class ProduktModel
    {
        public int ID { get; set; }
        public string Kod { get; set; }
        public string Nazwa { get; set; }
    }

    public class ProduktKonfiguracjaModel : INotifyPropertyChanged
    {
        public int ID { get; set; }
        public int TowarID { get; set; }
        public string Kod { get; set; }
        public string Nazwa { get; set; }

        private decimal _procent;
        public decimal Procent
        {
            get => _procent;
            set
            {
                if (_procent != value)
                {
                    _procent = value;
                    OnPropertyChanged(nameof(Procent));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}