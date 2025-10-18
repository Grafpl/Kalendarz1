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
    public partial class KonfiguracjaPodrobow : Window
    {
        private string connectionString;
        private string connectionStringHandel;
        private ObservableCollection<ProduktModel> dostepneProdukty;
        private ObservableCollection<ProduktModel> wszystkieProdukty;
        private ObservableCollection<PodrobyKonfiguracjaModel> skonfigurowanePodrobia;
        private ObservableCollection<PodrobySzczegolyModel> podrobySzczegoly;

        public KonfiguracjaPodrobow(string connString, string connStringHandel)
        {
            InitializeComponent();
            connectionString = connString;
            connectionStringHandel = connStringHandel;

            dostepneProdukty = new ObservableCollection<ProduktModel>();
            wszystkieProdukty = new ObservableCollection<ProduktModel>();
            skonfigurowanePodrobia = new ObservableCollection<PodrobyKonfiguracjaModel>();
            podrobySzczegoly = new ObservableCollection<PodrobySzczegolyModel>();

            dpDataOd.SelectedDate = DateTime.Today;

            WczytajProdukty();
            WczytajKonfiguracje();

            listDostepne.ItemsSource = dostepneProdukty;
            dgKonfiguracje.ItemsSource = skonfigurowanePodrobia;
            listSkonfigurowane.ItemsSource = podrobySzczegoly;

            ObliczSumeProcentow();
        }

        private void WczytajProdukty()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringHandel))
                {
                    conn.Open();

                    // Wczytaj WSZYSTKIE towary z WSZYSTKICH katalogów
                    string query = @"
                        SELECT id, kod, nazwa, katalog 
                        FROM HM.TW 
                        WHERE aktywny = 1
                        ORDER BY katalog, nazwa";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string katalog = reader["katalog"]?.ToString() ?? "";
                            string nazwa = reader["nazwa"]?.ToString() ?? "";

                            var produkt = new ProduktModel
                            {
                                ID = Convert.ToInt32(reader["id"]),
                                Kod = reader["kod"]?.ToString() ?? "",
                                Nazwa = $"[{katalog}] {nazwa}" // Dodaj katalog do nazwy dla łatwiejszej identyfikacji
                            };

                            dostepneProdukty.Add(produkt);
                            wszystkieProdukty.Add(produkt);
                        }
                    }
                }

                // Dodaj też specjalne kategorie ubytków które nie są towarami
                var dodatkoweProdukty = new List<ProduktModel>
                {
                    new ProduktModel { ID = -1, Kod = "SPEC_PIORA", Nazwa = "[SPECJALNE] Pióra" },
                    new ProduktModel { ID = -2, Kod = "SPEC_KREW", Nazwa = "[SPECJALNE] Krew" },
                    new ProduktModel { ID = -3, Kod = "SPEC_JELITA", Nazwa = "[SPECJALNE] Jelita" },
                    new ProduktModel { ID = -4, Kod = "SPEC_STRATY", Nazwa = "[SPECJALNE] Straty produkcyjne" },
                    new ProduktModel { ID = -5, Kod = "SPEC_WODA", Nazwa = "[SPECJALNE] Woda/Wilgoć" },
                    new ProduktModel { ID = -6, Kod = "SPEC_INNE", Nazwa = "[SPECJALNE] Inne ubytki" }
                };

                foreach (var prod in dodatkoweProdukty)
                {
                    dostepneProdukty.Add(prod);
                    wszystkieProdukty.Add(prod);
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

                    // Sprawdź czy tabela istnieje, jeśli nie - utwórz ją
                    string createTableQuery = @"
                        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='KonfiguracjaPodrobow' AND xtype='U')
                        CREATE TABLE KonfiguracjaPodrobow (
                            ID int IDENTITY(1,1) PRIMARY KEY,
                            TowarID int NOT NULL,
                            NazwaTowaru nvarchar(255) NOT NULL,
                            ProcentUdzialu decimal(5,2) NOT NULL,
                            DataOd date NOT NULL,
                            Aktywny bit NOT NULL DEFAULT 1,
                            DataModyfikacji datetime DEFAULT GETDATE(),
                            ModyfikowalPrzez nvarchar(100)
                        )";

                    using (SqlCommand cmd = new SqlCommand(createTableQuery, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    string query = @"
                        SELECT k.ID, k.TowarID, k.NazwaTowaru, k.ProcentUdzialu, k.DataOd, k.Aktywny,
                               k.DataModyfikacji, k.ModyfikowalPrzez
                        FROM KonfiguracjaPodrobow k
                        ORDER BY k.DataOd DESC, k.ID DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        var tempDict = new Dictionary<DateTime, List<(int ID, int TowarID, string Nazwa, decimal Procent, bool Aktywny, DateTime? DataMod, string User)>>();

                        while (reader.Read())
                        {
                            DateTime dataOd = reader.IsDBNull(reader.GetOrdinal("DataOd"))
                                ? DateTime.Today
                                : Convert.ToDateTime(reader["DataOd"]);

                            int towarID = Convert.ToInt32(reader["TowarID"]);
                            string nazwa = reader["NazwaTowaru"].ToString();
                            decimal procent = Convert.ToDecimal(reader["ProcentUdzialu"]);
                            bool aktywny = !reader.IsDBNull(reader.GetOrdinal("Aktywny")) && Convert.ToBoolean(reader["Aktywny"]);
                            DateTime? dataMod = reader.IsDBNull(reader.GetOrdinal("DataModyfikacji"))
                                ? null
                                : Convert.ToDateTime(reader["DataModyfikacji"]);
                            string user = reader.IsDBNull(reader.GetOrdinal("ModyfikowalPrzez"))
                                ? ""
                                : reader["ModyfikowalPrzez"].ToString();

                            if (!tempDict.ContainsKey(dataOd))
                                tempDict[dataOd] = new List<(int, int, string, decimal, bool, DateTime?, string)>();

                            tempDict[dataOd].Add((Convert.ToInt32(reader["ID"]), towarID, nazwa, procent, aktywny, dataMod, user));
                        }

                        // Grupuj po dacie i stwórz jeden wiersz na konfigurację
                        foreach (var kvp in tempDict.OrderByDescending(x => x.Key))
                        {
                            var produkty = kvp.Value;
                            decimal sumaProcent = produkty.Sum(p => p.Procent);
                            bool czyAktywny = produkty.Any(p => p.Aktywny);
                            int liczbaProd = produkty.Count;

                            var pierwszyProd = produkty.First();

                            skonfigurowanePodrobia.Add(new PodrobyKonfiguracjaModel
                            {
                                DataOd = kvp.Key,
                                LiczbaProduktow = liczbaProd,
                                SumaProcentow = sumaProcent,
                                Aktywny = czyAktywny,
                                StatusTekst = czyAktywny ? "✓ Aktywna" : "✕ Nieaktywna",
                                DataModyfikacji = pierwszyProd.DataMod ?? DateTime.Now,
                                ModyfikowalPrzez = pierwszyProd.User,
                                Produkty = string.Join(", ", produkty.Select(p => $"{p.Nazwa} ({p.Procent:F1}%)"))
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

            if (podrobySzczegoly.Any(p => p.TowarID == wybrany.ID))
            {
                MessageBox.Show("Ten produkt jest już w konfiguracji.",
                              "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            podrobySzczegoly.Add(new PodrobySzczegolyModel
            {
                TowarID = wybrany.ID,
                Kod = wybrany.Kod,
                Nazwa = wybrany.Nazwa,
                Procent = 0m
            });

            ObliczSumeProcentow();
            listDostepne.SelectedItem = null;
        }

        private void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var produkt = button?.Tag as PodrobySzczegolyModel;

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
                    podrobySzczegoly.Remove(produkt);
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
            if (txtSumaProcentow == null || podrobySzczegoly == null) return;

            decimal suma = podrobySzczegoly.Sum(p => p.Procent);
            txtSumaProcentow.Text = $"{suma:F1}%";

            // Oblicz także ile % zostaje na tuszkę
            decimal naTuszke = 100m - suma;
            txtPozostaleNaTuszke.Text = $"{naTuszke:F1}%";

            SolidColorBrush kolorTekstu;
            SolidColorBrush kolorTla;
            SolidColorBrush kolorObramowania;

            // Podroby to zazwyczaj 8-12% żywca, pióra 5-7%, krew 3-4%
            // Razem około 16-23% ubytków przed tuszką
            if (suma >= 15 && suma <= 25)
            {
                kolorTekstu = new SolidColorBrush(Color.FromRgb(92, 138, 58));
                kolorTla = new SolidColorBrush(Color.FromRgb(232, 245, 233));
                kolorObramowania = new SolidColorBrush(Color.FromRgb(92, 138, 58));
            }
            else if (suma >= 10 && suma <= 30)
            {
                kolorTekstu = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                kolorTla = new SolidColorBrush(Color.FromRgb(255, 243, 205));
                kolorObramowania = new SolidColorBrush(Color.FromRgb(255, 193, 7));
            }
            else
            {
                kolorTekstu = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                kolorTla = new SolidColorBrush(Color.FromRgb(255, 235, 238));
                kolorObramowania = new SolidColorBrush(Color.FromRgb(231, 76, 60));
            }

            txtSumaProcentow.Foreground = kolorTekstu;
            txtPozostaleNaTuszke.Foreground = kolorTekstu;
            borderSumaInfo.Background = kolorTla;
            borderSumaInfo.BorderBrush = kolorObramowania;
        }

        private void BtnPrzywrocDomyslne_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Czy na pewno przywrócić domyślną konfigurację?\n\n" +
                "Domyślne wartości:\n" +
                "• Serca: 0.8%\n" +
                "• Żołądki: 2.5%\n" +
                "• Wątróbka: 2.2%\n" +
                "• Pióra: 6.0%\n" +
                "• Krew: 3.5%\n" +
                "• Jelita: 5.0%\n" +
                "• Inne ubytki: 2.0%\n\n" +
                "RAZEM: 22% (zostaje 78% na tuszkę)\n\n" +
                "Aktualna konfiguracja zostanie wyczyszczona.\n" +
                "UWAGA: Jeśli nie znajdę odpowiednich towarów w bazie,\n" +
                "użyję kategorii specjalnych.",
                "Potwierdzenie przywrócenia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                podrobySzczegoly.Clear();

                var domyslne = new Dictionary<string, decimal>
                {
                    { "Serca", 0.8m },
                    { "Żołądki", 2.5m },
                    { "Wątróbka", 2.2m }
                };

                // Najpierw spróbuj znaleźć rzeczywiste towary
                foreach (var domyslny in domyslne)
                {
                    var produkt = wszystkieProdukty.FirstOrDefault(p =>
                        p.ID > 0 && // Tylko rzeczywiste towary
                        p.Nazwa.Contains(domyslny.Key, StringComparison.OrdinalIgnoreCase));

                    if (produkt != null)
                    {
                        podrobySzczegoly.Add(new PodrobySzczegolyModel
                        {
                            TowarID = produkt.ID,
                            Kod = produkt.Kod,
                            Nazwa = produkt.Nazwa,
                            Procent = domyslny.Value
                        });
                    }
                }

                // Dodaj kategorie specjalne dla ubytków
                var specjalne = new Dictionary<int, (string nazwa, decimal procent)>
                {
                    { -1, ("[SPECJALNE] Pióra", 6.0m) },
                    { -2, ("[SPECJALNE] Krew", 3.5m) },
                    { -3, ("[SPECJALNE] Jelita", 5.0m) },
                    { -6, ("[SPECJALNE] Inne ubytki", 2.0m) }
                };

                foreach (var spec in specjalne)
                {
                    var produkt = wszystkieProdukty.FirstOrDefault(p => p.ID == spec.Key);
                    if (produkt != null)
                    {
                        podrobySzczegoly.Add(new PodrobySzczegolyModel
                        {
                            TowarID = produkt.ID,
                            Kod = produkt.Kod,
                            Nazwa = produkt.Nazwa,
                            Procent = spec.Value.procent
                        });
                    }
                }

                ObliczSumeProcentow();

                MessageBox.Show($"Przywrócono domyślną konfigurację.\n\n" +
                              $"Dodano {podrobySzczegoly.Count} pozycji.\n" +
                              $"Suma ubytków: {podrobySzczegoly.Sum(p => p.Procent):F1}%",
                              "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (!dpDataOd.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz datę od której ma obowiązywać konfiguracja.",
                              "Brak daty", MessageBoxButton.OK, MessageBoxImage.Warning);
                dpDataOd.Focus();
                return;
            }

            if (podrobySzczegoly.Count == 0)
            {
                MessageBox.Show("Dodaj przynajmniej jeden produkt do konfiguracji.",
                              "Brak produktów", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal suma = podrobySzczegoly.Sum(p => p.Procent);

            if (suma < 10 || suma > 35)
            {
                var result = MessageBox.Show(
                    $"Suma procentów wynosi {suma:F1}%, co jest poza typowym zakresem (10-35%).\n\n" +
                    $"Pozostaje {100 - suma:F1}% na tuszkę.\n\n" +
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
                            DateTime dataOd = dpDataOd.SelectedDate.Value.Date;

                            // Dezaktywuj konfiguracje dla tej samej daty
                            string deactivateQuery = "UPDATE KonfiguracjaPodrobow SET Aktywny = 0 WHERE DataOd = @DataOd";
                            using (SqlCommand cmd = new SqlCommand(deactivateQuery, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@DataOd", dataOd);
                                cmd.ExecuteNonQuery();
                            }

                            // Dodaj nowe produkty
                            foreach (var produkt in podrobySzczegoly)
                            {
                                string query = @"
                                    INSERT INTO KonfiguracjaPodrobow 
                                        (TowarID, NazwaTowaru, ProcentUdzialu, DataOd, Aktywny, ModyfikowalPrzez, DataModyfikacji)
                                    VALUES (@TowarID, @Nazwa, @Procent, @DataOd, 1, @User, GETDATE())";

                                using (SqlCommand cmd = new SqlCommand(query, conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@TowarID", produkt.TowarID);
                                    cmd.Parameters.AddWithValue("@Nazwa", produkt.Nazwa);
                                    cmd.Parameters.AddWithValue("@Procent", produkt.Procent);
                                    cmd.Parameters.AddWithValue("@DataOd", dataOd);
                                    cmd.Parameters.AddWithValue("@User", Environment.UserName);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            trans.Commit();

                            MessageBox.Show(
                                $"✓ Konfiguracja została zapisana pomyślnie!\n\n" +
                                $"Data od: {dataOd:yyyy-MM-dd}\n" +
                                $"Liczba produktów: {podrobySzczegoly.Count}\n" +
                                $"Suma ubytków: {suma:F1}%\n" +
                                $"Pozostaje na tuszkę: {100 - suma:F1}%\n\n" +
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
            if (podrobySzczegoly.Count > 0)
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

        private void BtnUsunKonfiguracje_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var konfig = button?.Tag as PodrobyKonfiguracjaModel;

            if (konfig != null)
            {
                var result = MessageBox.Show(
                    $"Czy na pewno dezaktywować konfigurację?\n\n" +
                    $"Data od: {konfig.DataOd:yyyy-MM-dd}\n" +
                    $"Liczba produktów: {konfig.LiczbaProduktow}\n" +
                    $"Suma: {konfig.SumaProcentow:F1}%\n\n" +
                    $"Konfiguracja zostanie zachowana w bazie, ale nie będzie używana.",
                    "Potwierdzenie dezaktywacji",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection(connectionString))
                        {
                            conn.Open();

                            string updateQuery = @"
                                UPDATE KonfiguracjaPodrobow 
                                SET Aktywny = 0, 
                                    DataModyfikacji = GETDATE(),
                                    ModyfikowalPrzez = @User
                                WHERE DataOd = @DataOd AND Aktywny = 1";

                            using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@DataOd", konfig.DataOd);
                                cmd.Parameters.AddWithValue("@User", Environment.UserName);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        skonfigurowanePodrobia.Clear();
                        WczytajKonfiguracje();

                        MessageBox.Show("Konfiguracja została dezaktywowana.",
                                      "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd dezaktywacji: {ex.Message}",
                                      "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }

    public class PodrobySzczegolyModel : INotifyPropertyChanged
    {
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

    public class PodrobyKonfiguracjaModel
    {
        public DateTime DataOd { get; set; }
        public int LiczbaProduktow { get; set; }
        public decimal SumaProcentow { get; set; }
        public bool Aktywny { get; set; }
        public string StatusTekst { get; set; }
        public DateTime DataModyfikacji { get; set; }
        public string ModyfikowalPrzez { get; set; }
        public string Produkty { get; set; }
    }
}