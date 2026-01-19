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
            WindowIconHelper.SetIcon(this);
            connectionString = connString;
            connectionStringHandel = connStringHandel;
            konfiguracjaProcentow = konfig;

            dostepneProdukty = new ObservableCollection<ProduktModel>();
            wszystkieProdukty = new ObservableCollection<ProduktModel>();
            skonfigurowaneProdukty = new ObservableCollection<ProduktKonfiguracjaModel>();

            dpDataOd.SelectedDate = DateTime.Today;

            WczytajProdukty();
            WczytajKonfiguracje();

            listDostepne.ItemsSource = dostepneProdukty;
            dgKonfiguracje.ItemsSource = skonfigurowaneProdukty;

            ObliczSumeProcentow();
        }

        private void WczytajProdukty()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringHandel))
                {
                    conn.Open();

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
                        SELECT k.ID, k.TowarID, k.NazwaTowaru, k.ProcentUdzialu, k.DataOd, k.Aktywny,
                               k.DataModyfikacji, k.ModyfikowalPrzez
                        FROM KonfiguracjaProduktow k
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

                            skonfigurowaneProdukty.Add(new ProduktKonfiguracjaModel
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

        private ObservableCollection<ProduktSzczegolyModel> produktySzczegoly = new ObservableCollection<ProduktSzczegolyModel>();

        private void BtnDodaj_Click(object sender, RoutedEventArgs e)
        {
            var wybrany = listDostepne.SelectedItem as ProduktModel;
            if (wybrany == null)
            {
                MessageBox.Show("Wybierz produkt z listy dostępnych produktów.",
                              "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (produktySzczegoly.Any(p => p.TowarID == wybrany.ID))
            {
                MessageBox.Show("Ten produkt jest już w konfiguracji.",
                              "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            produktySzczegoly.Add(new ProduktSzczegolyModel
            {
                TowarID = wybrany.ID,
                Kod = wybrany.Kod,
                Nazwa = wybrany.Nazwa,
                Procent = 0m
            });

            if (listSkonfigurowane.ItemsSource == null)
            {
                listSkonfigurowane.ItemsSource = produktySzczegoly;
            }

            ObliczSumeProcentow();
            listDostepne.SelectedItem = null;
        }

        private void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var produkt = button?.Tag as ProduktSzczegolyModel;

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
                    produktySzczegoly.Remove(produkt);
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
            if (txtSumaProcentow == null || produktySzczegoly == null) return;

            decimal suma = produktySzczegoly.Sum(p => p.Procent);
            txtSumaProcentow.Text = $"{suma:F1}%";

            SolidColorBrush kolorTekstu;
            SolidColorBrush kolorTla;
            SolidColorBrush kolorObramowania;

            if (suma >= 93 && suma <= 95)
            {
                kolorTekstu = new SolidColorBrush(Color.FromRgb(92, 138, 58));
                kolorTla = new SolidColorBrush(Color.FromRgb(232, 245, 233));
                kolorObramowania = new SolidColorBrush(Color.FromRgb(92, 138, 58));
            }
            else if (suma >= 90 && suma <= 98)
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
                "Aktualna konfiguracja zostanie wyczyszczona.",
                "Potwierdzenie przywrócenia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                produktySzczegoly.Clear();

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
                        produktySzczegoly.Add(new ProduktSzczegolyModel
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

                listSkonfigurowane.ItemsSource = produktySzczegoly;
                ObliczSumeProcentow();
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

            if (produktySzczegoly.Count == 0)
            {
                MessageBox.Show("Dodaj przynajmniej jeden produkt do konfiguracji.",
                              "Brak produktów", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal suma = produktySzczegoly.Sum(p => p.Procent);

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
                            DateTime dataOd = dpDataOd.SelectedDate.Value.Date;

                            // Sprawdź i dodaj kolumnę GrupaScalowania jeśli nie istnieje
                            string checkColumnQuery = @"
                                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                                               WHERE TABLE_NAME = 'KonfiguracjaProduktow' AND COLUMN_NAME = 'GrupaScalowania')
                                BEGIN
                                    ALTER TABLE KonfiguracjaProduktow ADD GrupaScalowania NVARCHAR(100) NULL
                                END";
                            using (SqlCommand cmd = new SqlCommand(checkColumnQuery, conn, trans))
                            {
                                cmd.ExecuteNonQuery();
                            }

                            // Dezaktywuj konfiguracje dla tej samej daty
                            string deactivateQuery = "UPDATE KonfiguracjaProduktow SET Aktywny = 0 WHERE DataOd = @DataOd";
                            using (SqlCommand cmd = new SqlCommand(deactivateQuery, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@DataOd", dataOd);
                                cmd.ExecuteNonQuery();
                            }

                            // Dodaj nowe produkty
                            foreach (var produkt in produktySzczegoly)
                            {
                                string query = @"
                                    INSERT INTO KonfiguracjaProduktow
                                        (TowarID, NazwaTowaru, ProcentUdzialu, DataOd, Aktywny, ModyfikowalPrzez, DataModyfikacji, GrupaScalowania)
                                    VALUES (@TowarID, @Nazwa, @Procent, @DataOd, 1, @User, GETDATE(), @GrupaScalowania)";

                                using (SqlCommand cmd = new SqlCommand(query, conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@TowarID", produkt.TowarID);
                                    cmd.Parameters.AddWithValue("@Nazwa", produkt.Nazwa);
                                    cmd.Parameters.AddWithValue("@Procent", produkt.Procent);
                                    cmd.Parameters.AddWithValue("@DataOd", dataOd);
                                    cmd.Parameters.AddWithValue("@User", Environment.UserName);
                                    cmd.Parameters.AddWithValue("@GrupaScalowania", string.IsNullOrWhiteSpace(produkt.GrupaScalowania) ? (object)DBNull.Value : produkt.GrupaScalowania);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            trans.Commit();

                            MessageBox.Show(
                                $"✓ Konfiguracja została zapisana pomyślnie!\n\n" +
                                $"Data od: {dataOd:yyyy-MM-dd}\n" +
                                $"Liczba produktów: {produktySzczegoly.Count}\n" +
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
            if (produktySzczegoly.Count > 0)
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

        private void BtnEdytujKonfiguracje_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var konfig = button?.Tag as ProduktKonfiguracjaModel;

            if (konfig != null)
            {
                WczytajKonfiguracjeDoEdycji(konfig.DataOd);
            }
        }

        private void DgKonfiguracje_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var konfig = dgKonfiguracje.SelectedItem as ProduktKonfiguracjaModel;
            if (konfig != null)
            {
                WczytajKonfiguracjeDoEdycji(konfig.DataOd);
            }
        }

        private void WczytajKonfiguracjeDoEdycji(DateTime dataOd)
        {
            try
            {
                // Wyczyść aktualną listę produktów
                produktySzczegoly.Clear();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Sprawdź czy kolumna GrupaScalowania istnieje
                    bool hasGrupaColumn = false;
                    string checkColumnQuery = @"
                        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME = 'KonfiguracjaProduktow' AND COLUMN_NAME = 'GrupaScalowania'";
                    using (SqlCommand cmd = new SqlCommand(checkColumnQuery, conn))
                    {
                        hasGrupaColumn = cmd.ExecuteScalar() != null;
                    }

                    string query = hasGrupaColumn
                        ? @"SELECT TowarID, NazwaTowaru, ProcentUdzialu, GrupaScalowania
                           FROM KonfiguracjaProduktow
                           WHERE DataOd = @DataOd
                           ORDER BY ID"
                        : @"SELECT TowarID, NazwaTowaru, ProcentUdzialu
                           FROM KonfiguracjaProduktow
                           WHERE DataOd = @DataOd
                           ORDER BY ID";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int towarId = Convert.ToInt32(reader["TowarID"]);
                                // Znajdź kod produktu z listy wszystkich produktów
                                var produktInfo = wszystkieProdukty.FirstOrDefault(p => p.ID == towarId);

                                var produkt = new ProduktSzczegolyModel
                                {
                                    TowarID = towarId,
                                    Nazwa = reader["NazwaTowaru"].ToString(),
                                    Kod = produktInfo?.Kod ?? "",
                                    Procent = Convert.ToDecimal(reader["ProcentUdzialu"]),
                                    GrupaScalowania = hasGrupaColumn && !reader.IsDBNull(reader.GetOrdinal("GrupaScalowania"))
                                        ? reader["GrupaScalowania"].ToString()
                                        : ""
                                };

                                produktySzczegoly.Add(produkt);
                            }
                        }
                    }
                }

                // Ustaw datę
                dpDataOd.SelectedDate = dataOd;

                // Ustaw ItemsSource jeśli jeszcze nie jest ustawione
                if (listSkonfigurowane.ItemsSource == null)
                {
                    listSkonfigurowane.ItemsSource = produktySzczegoly;
                }

                ObliczSumeProcentow();

                MessageBox.Show(
                    $"Wczytano konfigurację z dnia {dataOd:yyyy-MM-dd}.\n\n" +
                    $"Liczba produktów: {produktySzczegoly.Count}\n\n" +
                    $"Możesz teraz edytować produkty i zapisać jako nową konfigurację\n" +
                    $"lub zmienić datę aby nadpisać istniejącą.",
                    "Konfiguracja wczytana",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania konfiguracji: {ex.Message}",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnUsunKonfiguracje_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var konfig = button?.Tag as ProduktKonfiguracjaModel;

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
                                UPDATE KonfiguracjaProduktow 
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

                        skonfigurowaneProdukty.Clear();
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

    public class ProduktModel
    {
        public int ID { get; set; }
        public string Kod { get; set; }
        public string Nazwa { get; set; }
    }

    public class ProduktSzczegolyModel : INotifyPropertyChanged
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

        private string _grupaScalowania = "";
        public string GrupaScalowania
        {
            get => _grupaScalowania;
            set
            {
                if (_grupaScalowania != value)
                {
                    _grupaScalowania = value ?? "";
                    OnPropertyChanged(nameof(GrupaScalowania));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ProduktKonfiguracjaModel
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