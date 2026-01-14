using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Zywiec.WidokSpecyfikacji
{
    public partial class PartiaSelectWindow : Window
    {
        private readonly string _connectionString;
        private readonly string _customerGID;
        private readonly string _customerName;
        private readonly DateTime _dataUboju;
        private List<PartiaItem> _allPartie;

        public Guid? SelectedPartiaGuid { get; private set; }
        public string SelectedPartiaNumber { get; private set; }
        public bool PartiaRemoved { get; private set; } = false;

        public PartiaSelectWindow(string connectionString, string customerGID,
            string customerName, DateTime dataUboju)
        {
            InitializeComponent();

            _connectionString = connectionString;
            _customerGID = customerGID;
            _customerName = customerName;
            _dataUboju = dataUboju;

            txtDostawca.Text = $"Dostawca: {customerName} (ID: {customerGID})";

            LoadPartie();
        }

        private void LoadPartie()
        {
            try
            {
                _allPartie = new List<PartiaItem>();

                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Probuj pobrac CustomerID jako int (jesli GID jest liczbowy)
                int? customerIdInt = null;
                if (int.TryParse(_customerGID?.Trim(), out int parsed))
                {
                    customerIdInt = parsed;
                }

                string sql;
                SqlCommand cmd;

                if (customerIdInt.HasValue)
                {
                    // CustomerGID jest liczbowy - szukaj po CustomerID
                    sql = @"
                        SELECT guid, Partia, CustomerID, CustomerName, CreateData, CreateGodzina
                        FROM dbo.PartiaDostawca
                        WHERE CustomerID = @CustomerID
                        ORDER BY CreateData DESC, CreateGodzina DESC";
                    cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@CustomerID", customerIdInt.Value);
                }
                else
                {
                    // CustomerGID nie jest liczbowy - szukaj po nazwie lub wszystkie
                    sql = @"
                        SELECT guid, Partia, CustomerID, CustomerName, CreateData, CreateGodzina
                        FROM dbo.PartiaDostawca
                        WHERE CustomerName LIKE '%' + @CustomerName + '%'
                        ORDER BY CreateData DESC, CreateGodzina DESC";
                    cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@CustomerName", _customerName ?? "");
                }

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    _allPartie.Add(new PartiaItem
                    {
                        Guid = reader.GetGuid(0),
                        Partia = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        CustomerID = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                        CustomerName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        CreateData = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                        CreateGodzina = reader.IsDBNull(5) ? (TimeSpan?)null : reader.GetTimeSpan(5)
                    });
                }

                dgPartie.ItemsSource = _allPartie;
                txtInfo.Text = $"Znaleziono {_allPartie.Count} partii dla tego dostawcy";

                // Jesli brak partii - zaproponuj utworzenie
                if (_allPartie.Count == 0)
                {
                    txtInfo.Text = "Brak partii dla tego dostawcy. Kliknij 'Nowa partia' aby utworzyc.";
                    btnNowaPartia.Background = System.Windows.Media.Brushes.Orange;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania partii:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allPartie == null) return;

            var search = txtSearch.Text?.Trim().ToLower();

            if (string.IsNullOrEmpty(search))
            {
                dgPartie.ItemsSource = _allPartie;
            }
            else
            {
                dgPartie.ItemsSource = _allPartie
                    .Where(p => (p.Partia?.ToLower().Contains(search) ?? false) ||
                               (p.CustomerName?.ToLower().Contains(search) ?? false))
                    .ToList();
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadPartie();
        }

        private void BtnNowaPartia_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Generuj nowy numer partii w formacie YYDDDNNN
                var today = _dataUboju;
                var dayOfYear = today.DayOfYear.ToString("000");
                var year = (today.Year % 100).ToString("00");
                var prefix = $"{year}{dayOfYear}";

                // Znajdz nastepny numer dla tego dnia
                int nextNum = 1;

                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Pobierz max numer dla tego dnia
                var cmdMax = new SqlCommand(@"
                    SELECT MAX(CAST(SUBSTRING(Partia, 6, 3) AS INT))
                    FROM dbo.PartiaDostawca
                    WHERE Partia LIKE @Prefix + '%'
                    AND LEN(Partia) = 8", conn);
                cmdMax.Parameters.AddWithValue("@Prefix", prefix);

                var maxObj = cmdMax.ExecuteScalar();
                if (maxObj != DBNull.Value && maxObj != null)
                {
                    nextNum = Convert.ToInt32(maxObj) + 1;
                }

                var newPartia = $"{prefix}{nextNum:000}";

                // Potwierdz
                var result = MessageBox.Show(
                    $"Utworzyc nowa partie?\n\n" +
                    $"Numer: {newPartia}\n" +
                    $"Dostawca: {_customerName}\n" +
                    $"Data: {_dataUboju:dd.MM.yyyy}",
                    "Nowa partia",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // Utworz partie
                var newGuid = Guid.NewGuid();
                int customerIdInt = 0;
                int.TryParse(_customerGID?.Trim(), out customerIdInt);

                var cmdInsert = new SqlCommand(@"
                    INSERT INTO dbo.PartiaDostawca
                    (guid, Partia, CustomerID, CustomerName, CreateData, CreateGodzina)
                    VALUES (@guid, @Partia, @CustomerID, @CustomerName, @CreateData, @CreateGodzina)", conn);

                cmdInsert.Parameters.AddWithValue("@guid", newGuid);
                cmdInsert.Parameters.AddWithValue("@Partia", newPartia);
                cmdInsert.Parameters.AddWithValue("@CustomerID", customerIdInt);
                cmdInsert.Parameters.AddWithValue("@CustomerName", _customerName ?? "");
                cmdInsert.Parameters.AddWithValue("@CreateData", DateTime.Today);
                cmdInsert.Parameters.AddWithValue("@CreateGodzina", DateTime.Now.TimeOfDay);

                cmdInsert.ExecuteNonQuery();

                // Automatycznie wybierz nowa partie
                SelectedPartiaGuid = newGuid;
                SelectedPartiaNumber = newPartia;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad tworzenia partii:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgPartie_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SelectAndClose();
        }

        private void BtnWybierz_Click(object sender, RoutedEventArgs e)
        {
            SelectAndClose();
        }

        private void SelectAndClose()
        {
            var selected = dgPartie.SelectedItem as PartiaItem;
            if (selected == null)
            {
                MessageBox.Show("Wybierz partie z listy.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedPartiaGuid = selected.Guid;
            SelectedPartiaNumber = selected.Partia;
            DialogResult = true;
            Close();
        }

        private void BtnUsunPartie_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Czy na pewno usunac przypisanie partii do tej specyfikacji?",
                "Potwierdzenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SelectedPartiaGuid = null;
                SelectedPartiaNumber = null;
                PartiaRemoved = true;
                DialogResult = true;
                Close();
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// Model dla partii
    /// </summary>
    public class PartiaItem
    {
        public Guid Guid { get; set; }
        public string Partia { get; set; }
        public int CustomerID { get; set; }
        public string CustomerName { get; set; }
        public DateTime? CreateData { get; set; }
        public TimeSpan? CreateGodzina { get; set; }
    }
}
