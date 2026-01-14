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

            txtDostawca.Text = $"Dostawca: {customerName} | Data uboju: {dataUboju:dd.MM.yyyy}";

            LoadPartie();
        }

        private void LoadPartie()
        {
            try
            {
                _allPartie = new List<PartiaItem>();

                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // UWAGA: Wszystkie kolumny w PartiaDostawca sa VARCHAR!
                // CustomerID to varchar(10), CreateData to varchar(10), CreateGodzina to varchar(8)
                // Filtrujemy po dacie uboju (CreateData)
                string dataUbojuStr = _dataUboju.ToString("yyyy-MM-dd");

                string sql = @"
                    SELECT guid, Partia, CustomerID, CustomerName, CreateData, CreateGodzina
                    FROM dbo.PartiaDostawca
                    WHERE CreateData = @DataUboju
                    ORDER BY CreateGodzina DESC, Partia DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@DataUboju", dataUbojuStr);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    // Wszystko czytamy jako string i parsujemy
                    string guidStr = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    Guid guidValue = Guid.Empty;
                    Guid.TryParse(guidStr, out guidValue);

                    string createDataStr = reader.IsDBNull(4) ? "" : reader.GetString(4);
                    DateTime? createData = null;
                    if (DateTime.TryParse(createDataStr, out var parsedDate))
                        createData = parsedDate;

                    string createGodzinaStr = reader.IsDBNull(5) ? "" : reader.GetString(5);
                    TimeSpan? createGodzina = null;
                    if (TimeSpan.TryParse(createGodzinaStr, out var parsedTime))
                        createGodzina = parsedTime;

                    string customerIdStr = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    int customerIdInt = 0;
                    int.TryParse(customerIdStr, out customerIdInt);

                    _allPartie.Add(new PartiaItem
                    {
                        Guid = guidValue,
                        Partia = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        CustomerID = customerIdInt,
                        CustomerName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        CreateData = createData,
                        CreateGodzina = createGodzina
                    });
                }

                dgPartie.ItemsSource = _allPartie;
                txtInfo.Text = $"Znaleziono {_allPartie.Count} partii na dzien {_dataUboju:dd.MM.yyyy}";

                // Jesli brak partii - zaproponuj utworzenie
                if (_allPartie.Count == 0)
                {
                    txtInfo.Text = $"Brak partii na dzien {_dataUboju:dd.MM.yyyy}. Kliknij 'Nowa partia' aby utworzyc.";
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

                // Utworz partie - WSZYSTKIE kolumny sa VARCHAR!
                var newGuid = Guid.NewGuid();

                var cmdInsert = new SqlCommand(@"
                    INSERT INTO dbo.PartiaDostawca
                    (guid, Partia, CustomerID, CustomerName, CreateData, CreateGodzina)
                    VALUES (@guid, @Partia, @CustomerID, @CustomerName, @CreateData, @CreateGodzina)", conn);

                // Wszystko zapisujemy jako string (wszystkie kolumny to VARCHAR)
                cmdInsert.Parameters.AddWithValue("@guid", newGuid.ToString());
                cmdInsert.Parameters.AddWithValue("@Partia", newPartia);
                cmdInsert.Parameters.AddWithValue("@CustomerID", _customerGID?.Trim() ?? "");
                cmdInsert.Parameters.AddWithValue("@CustomerName", _customerName ?? "");
                cmdInsert.Parameters.AddWithValue("@CreateData", DateTime.Today.ToString("yyyy-MM-dd"));
                cmdInsert.Parameters.AddWithValue("@CreateGodzina", DateTime.Now.ToString("HH:mm:ss"));

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
