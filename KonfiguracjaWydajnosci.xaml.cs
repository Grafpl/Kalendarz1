using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class KonfiguracjaWydajnosci : Window
    {
        private string connectionString;
        private ObservableCollection<KonfiguracjaWydajnosciModel> konfiguracje;

        public KonfiguracjaWydajnosci(string connString)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            connectionString = connString;
            konfiguracje = new ObservableCollection<KonfiguracjaWydajnosciModel>();

            dpDataOd.SelectedDate = DateTime.Today;

            dgKonfiguracje.ItemsSource = konfiguracje;
            WczytajKonfiguracje();
        }

        private void WczytajKonfiguracje()
        {
            konfiguracje.Clear();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT ID, DataOd, WspolczynnikTuszki, ProcentTuszkaA, ProcentTuszkaB, 
                               Aktywny, DataModyfikacji, ModyfikowalPrzez
                        FROM KonfiguracjaWydajnosci
                        ORDER BY DataOd DESC, ID DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            bool aktywny = !reader.IsDBNull(reader.GetOrdinal("Aktywny")) && Convert.ToBoolean(reader["Aktywny"]);

                            konfiguracje.Add(new KonfiguracjaWydajnosciModel
                            {
                                ID = Convert.ToInt32(reader["ID"]),
                                DataOd = Convert.ToDateTime(reader["DataOd"]),
                                WspolczynnikTuszki = Convert.ToDecimal(reader["WspolczynnikTuszki"]),
                                ProcentTuszkaA = Convert.ToDecimal(reader["ProcentTuszkaA"]),
                                ProcentTuszkaB = Convert.ToDecimal(reader["ProcentTuszkaB"]),
                                Aktywny = aktywny,
                                StatusTekst = aktywny ? "✓ Aktywna" : "✕ Nieaktywna",
                                DataModyfikacji = reader["DataModyfikacji"] != DBNull.Value ?
                                    Convert.ToDateTime(reader["DataModyfikacji"]) : DateTime.Now,
                                ModyfikowalPrzez = reader["ModyfikowalPrzez"]?.ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania konfiguracji: {ex.Message}",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDodaj_Click(object sender, RoutedEventArgs e)
        {
            // Walidacja daty
            if (!dpDataOd.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz datę od której ma obowiązywać nowa konfiguracja.",
                              "Brak daty", MessageBoxButton.OK, MessageBoxImage.Warning);
                dpDataOd.Focus();
                return;
            }

            // Walidacja współczynnika tuszki
            if (!decimal.TryParse(txtWspolczynnikTuszki.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal wspolczynnikTuszki) ||
                wspolczynnikTuszki <= 0 || wspolczynnikTuszki > 100)
            {
                MessageBox.Show("Współczynnik tuszki musi być liczbą z przedziału 0-100.\n\nPrzykład: 78 lub 78.5",
                              "Błędna wartość", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtWspolczynnikTuszki.Focus();
                txtWspolczynnikTuszki.SelectAll();
                return;
            }

            // Walidacja procentu A
            if (!decimal.TryParse(txtProcentA.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal procentA) ||
                procentA < 0 || procentA > 100)
            {
                MessageBox.Show("Procent tuszki A musi być liczbą z przedziału 0-100.\n\nPrzykład: 85 lub 85.5",
                              "Błędna wartość", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtProcentA.Focus();
                txtProcentA.SelectAll();
                return;
            }

            // Walidacja procentu B
            if (!decimal.TryParse(txtProcentB.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal procentB) ||
                procentB < 0 || procentB > 100)
            {
                MessageBox.Show("Procent tuszki B musi być liczbą z przedziału 0-100.\n\nPrzykład: 15 lub 15.5",
                              "Błędna wartość", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtProcentB.Focus();
                txtProcentB.SelectAll();
                return;
            }

            // Sprawdź czy suma A+B wynosi 100
            decimal suma = procentA + procentB;
            if (Math.Abs(suma - 100) > 0.1m)
            {
                var result = MessageBox.Show(
                    $"UWAGA: Suma procentów A i B wynosi {suma:F2}%, a powinna wynosić 100%.\n\n" +
                    $"A: {procentA}%\n" +
                    $"B: {procentB}%\n" +
                    $"Suma: {suma}%\n\n" +
                    $"Czy na pewno chcesz dodać tę konfigurację?",
                    "Ostrzeżenie - Nieprawidłowa suma",
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

                    // Sprawdź czy już istnieje konfiguracja dla tej daty
                    string checkQuery = "SELECT COUNT(*) FROM KonfiguracjaWydajnosci WHERE DataOd = @DataOd AND Aktywny = 1";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@DataOd", dpDataOd.SelectedDate.Value);
                        int count = (int)checkCmd.ExecuteScalar();

                        if (count > 0)
                        {
                            var result = MessageBox.Show(
                                $"Istnieje już aktywna konfiguracja dla daty {dpDataOd.SelectedDate.Value:yyyy-MM-dd}.\n\n" +
                                $"Czy chcesz ją zastąpić nową?",
                                "Konfiguracja istnieje",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (result == MessageBoxResult.No)
                                return;

                            // Dezaktywuj starą
                            string deactivateQuery = "UPDATE KonfiguracjaWydajnosci SET Aktywny = 0 WHERE DataOd = @DataOd";
                            using (SqlCommand deactivateCmd = new SqlCommand(deactivateQuery, conn))
                            {
                                deactivateCmd.Parameters.AddWithValue("@DataOd", dpDataOd.SelectedDate.Value);
                                deactivateCmd.ExecuteNonQuery();
                            }
                        }
                    }

                    // Dodaj nową konfigurację
                    string insertQuery = @"
                        INSERT INTO KonfiguracjaWydajnosci 
                            (DataOd, WspolczynnikTuszki, ProcentTuszkaA, ProcentTuszkaB, Aktywny, ModyfikowalPrzez, DataModyfikacji)
                        VALUES 
                            (@DataOd, @WspolczynnikTuszki, @ProcentA, @ProcentB, 1, @User, GETDATE())";

                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", dpDataOd.SelectedDate.Value);
                        cmd.Parameters.AddWithValue("@WspolczynnikTuszki", wspolczynnikTuszki);
                        cmd.Parameters.AddWithValue("@ProcentA", procentA);
                        cmd.Parameters.AddWithValue("@ProcentB", procentB);
                        cmd.Parameters.AddWithValue("@User", Environment.UserName);

                        cmd.ExecuteNonQuery();

                        MessageBox.Show(
                            $"✓ Konfiguracja została dodana!\n\n" +
                            $"Od daty: {dpDataOd.SelectedDate.Value:yyyy-MM-dd}\n" +
                            $"Tuszka: {wspolczynnikTuszki}%\n" +
                            $"A/B: {procentA}/{procentB}",
                            "Sukces",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // Odśwież listę
                        WczytajKonfiguracje();

                        // Wyczyść formularz
                        dpDataOd.SelectedDate = DateTime.Today;
                        txtWspolczynnikTuszki.Text = "78.00";
                        txtProcentA.Text = "85.00";
                        txtProcentB.Text = "15.00";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd dodawania konfiguracji:\n\n{ex.Message}",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var konfig = button?.Tag as KonfiguracjaWydajnosciModel;

            if (konfig != null)
            {
                var result = MessageBox.Show(
                    $"Czy na pewno dezaktywować konfigurację?\n\n" +
                    $"Data od: {konfig.DataOd:yyyy-MM-dd}\n" +
                    $"Tuszka: {konfig.WspolczynnikTuszki}%\n" +
                    $"A/B: {konfig.ProcentTuszkaA}/{konfig.ProcentTuszkaB}\n\n" +
                    $"Konfiguracja zostanie zachowana w bazie, ale nie będzie używana w obliczeniach.",
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
                                UPDATE KonfiguracjaWydajnosci 
                                SET Aktywny = 0, 
                                    DataModyfikacji = GETDATE(),
                                    ModyfikowalPrzez = @User
                                WHERE ID = @ID";

                            using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@ID", konfig.ID);
                                cmd.Parameters.AddWithValue("@User", Environment.UserName);
                                cmd.ExecuteNonQuery();
                            }
                        }

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

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }

    public class KonfiguracjaWydajnosciModel
    {
        public int ID { get; set; }
        public DateTime DataOd { get; set; }
        public decimal WspolczynnikTuszki { get; set; }
        public decimal ProcentTuszkaA { get; set; }
        public decimal ProcentTuszkaB { get; set; }
        public bool Aktywny { get; set; }
        public string StatusTekst { get; set; }
        public DateTime DataModyfikacji { get; set; }
        public string ModyfikowalPrzez { get; set; }
    }
}