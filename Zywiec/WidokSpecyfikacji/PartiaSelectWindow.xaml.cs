using System;
using System.Collections.Generic;
using System.ComponentModel;
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

                // Filtrujemy po dacie uboju (CreateData)
                string dataUbojuStr = _dataUboju.ToString("yyyy-MM-dd");

                // Pobierz partie z PartiaDostawca
                string sql = @"
                    SELECT guid, Partia, CustomerID, CustomerName, CreateData, CreateGodzina
                    FROM dbo.PartiaDostawca
                    WHERE CreateData = @DataUboju
                    ORDER BY CreateGodzina DESC, Partia DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@DataUboju", dataUbojuStr);

                var partieTemp = new List<PartiaItem>();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
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
                        string partiaStr = reader.IsDBNull(1) ? "" : reader.GetString(1);

                        partieTemp.Add(new PartiaItem
                        {
                            Guid = guidValue,
                            Partia = partiaStr,
                            CustomerID = customerIdStr,
                            CustomerName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            CreateData = createData,
                            CreateGodzina = createGodzina,
                            // Pelny numer partii: CustomerID + Partia
                            FullPartiaNumber = $"{customerIdStr}{partiaStr}"
                        });
                    }
                }

                // Sprawdz status zamkniecia w listapartii
                foreach (var partia in partieTemp)
                {
                    CheckPartiaCloseStatus(conn, partia);
                    _allPartie.Add(partia);
                }

                dgPartie.ItemsSource = _allPartie;
                txtInfo.Text = $"Znaleziono {_allPartie.Count} partii na dzien {_dataUboju:dd.MM.yyyy}";

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

        /// <summary>
        /// Sprawdza w listapartii czy partia jest zamknieta (IsClose = 1)
        /// </summary>
        private void CheckPartiaCloseStatus(SqlConnection conn, PartiaItem partia)
        {
            try
            {
                // Szukamy partii w listapartii po numerze Partia
                var cmdCheck = new SqlCommand(@"
                    SELECT IsClose, CalcData, CalcGodzina
                    FROM dbo.listapartii
                    WHERE Partia = @Partia", conn);
                cmdCheck.Parameters.AddWithValue("@Partia", partia.Partia ?? "");

                using var reader = cmdCheck.ExecuteReader();
                if (reader.Read())
                {
                    // IsClose jest smallint, sprawdzamy czy = 1
                    if (!reader.IsDBNull(0))
                    {
                        var isCloseValue = reader.GetInt16(0);
                        partia.IsClose = (isCloseValue == 1);
                    }

                    // CalcData i CalcGodzina sa varchar
                    if (!reader.IsDBNull(1))
                    {
                        string calcDataStr = reader.GetString(1);
                        if (DateTime.TryParse(calcDataStr, out var calcDate))
                            partia.CalcData = calcDate;
                    }

                    if (!reader.IsDBNull(2))
                    {
                        partia.CalcGodzina = reader.GetString(2);
                    }
                }
            }
            catch
            {
                // Ignoruj bledy sprawdzania statusu
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
                    .Where(p => (p.FullPartiaNumber?.ToLower().Contains(search) ?? false) ||
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

                int nextNum = 1;

                using var conn = new SqlConnection(_connectionString);
                conn.Open();

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
                var fullPartiaNumber = $"{_customerGID?.Trim()}{newPartia}";

                var result = MessageBox.Show(
                    $"Utworzyc nowa partie?\n\n" +
                    $"Pelny numer: {fullPartiaNumber}\n" +
                    $"Dostawca: {_customerName}\n" +
                    $"Data: {_dataUboju:dd.MM.yyyy}",
                    "Nowa partia",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                var newGuid = Guid.NewGuid();

                var cmdInsert = new SqlCommand(@"
                    INSERT INTO dbo.PartiaDostawca
                    (guid, Partia, CustomerID, CustomerName, CreateData, CreateGodzina)
                    VALUES (@guid, @Partia, @CustomerID, @CustomerName, @CreateData, @CreateGodzina)", conn);

                cmdInsert.Parameters.AddWithValue("@guid", newGuid.ToString());
                cmdInsert.Parameters.AddWithValue("@Partia", newPartia);
                cmdInsert.Parameters.AddWithValue("@CustomerID", _customerGID?.Trim() ?? "");
                cmdInsert.Parameters.AddWithValue("@CustomerName", _customerName ?? "");
                cmdInsert.Parameters.AddWithValue("@CreateData", DateTime.Today.ToString("yyyy-MM-dd"));
                cmdInsert.Parameters.AddWithValue("@CreateGodzina", DateTime.Now.ToString("HH:mm:ss"));

                cmdInsert.ExecuteNonQuery();

                // Zwroc pelny numer partii (CustomerID + Partia)
                SelectedPartiaGuid = newGuid;
                SelectedPartiaNumber = fullPartiaNumber;
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

            // Ostrzezenie dla zamknietej partii
            if (selected.IsClose)
            {
                var result = MessageBox.Show(
                    $"UWAGA: Ta partia jest ZAMKNIETA!\n\n" +
                    $"Partia: {selected.FullPartiaNumber}\n" +
                    $"Zamknieta: {selected.CalcData:dd.MM.yyyy} o {selected.CalcGodzina}\n\n" +
                    $"Czy na pewno chcesz ja wybrac?",
                    "Partia zamknieta",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            SelectedPartiaGuid = selected.Guid;
            // Zwracamy pelny numer partii (CustomerID + Partia)
            SelectedPartiaNumber = selected.FullPartiaNumber;
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
    /// Model dla partii z informacja o statusie zamkniecia
    /// </summary>
    public class PartiaItem : INotifyPropertyChanged
    {
        public Guid Guid { get; set; }
        public string Partia { get; set; }
        public string CustomerID { get; set; }
        public string CustomerName { get; set; }
        public DateTime? CreateData { get; set; }
        public TimeSpan? CreateGodzina { get; set; }

        // Pelny numer partii: CustomerID + Partia (bez separatora)
        public string FullPartiaNumber { get; set; }

        // Status zamkniecia z listapartii
        public bool IsClose { get; set; }
        public DateTime? CalcData { get; set; }
        public string CalcGodzina { get; set; }

        // Wyswietlanie statusu
        public string StatusDisplay => IsClose
            ? $"ZAMKNIETA ({CalcData:dd.MM} {CalcGodzina})"
            : "Otwarta";

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
