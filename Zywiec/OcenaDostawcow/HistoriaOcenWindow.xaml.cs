using System;
using System.Data;
using System.Windows;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    /// <summary>
    /// Okno pokazujące historię wszystkich ocen danego dostawcy
    /// </summary>
    public partial class HistoriaOcenWindow : Window
    {
        private const string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string _dostawcaId;
        
        public HistoriaOcenWindow(string dostawcaId)
        {
            InitializeComponent();
            _dostawcaId = dostawcaId;
            
            LoadHistory();
            LoadSupplierName();
        }
        
        /// <summary>
        /// Ładuje nazwę dostawcy
        /// </summary>
        private async void LoadSupplierName()
        {
            try
            {
                string query = @"
                    SELECT Name, ShortName 
                    FROM [dbo].[Dostawcy] 
                    WHERE ID = @DostawcaID";
                    
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@DostawcaID", _dostawcaId);
                
                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    string nazwa = reader["Name"]?.ToString() ?? "Nieznany";
                    string skrot = reader["ShortName"]?.ToString() ?? "";
                    txtNazwaDostawcy.Text = $"{nazwa} ({skrot}) - ID: {_dostawcaId}";
                }
            }
            catch (Exception ex)
            {
                txtNazwaDostawcy.Text = $"Błąd ładowania: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Ładuje historię ocen z bazy danych
        /// </summary>
        private async void LoadHistory()
        {
            try
            {
                string query = @"
                    SELECT 
                        ID,
                        DataOceny,
                        NumerRaportu,
                        PunktySekcja1_5,
                        PunktySekcja6_20,
                        PunktyRazem,
                        OceniajacyUserID,
                        Uwagi,
                        DataUtworzenia,
                        Status
                    FROM [dbo].[OcenyDostawcow]
                    WHERE DostawcaID = @DostawcaID
                    ORDER BY DataOceny DESC";
                    
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@DostawcaID", _dostawcaId);
                
                using var adapter = new SqlDataAdapter(command);
                var dataTable = new DataTable();
                adapter.Fill(dataTable);
                
                // Przypisz dane do tabeli
                dgHistoria.ItemsSource = dataTable.DefaultView;
                
                // Oblicz statystyki
                CalculateStatistics(dataTable);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania historii: {ex.Message}", 
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Oblicza statystyki ocen
        /// </summary>
        private void CalculateStatistics(DataTable dataTable)
        {
            if (dataTable.Rows.Count == 0)
            {
                txtStatystyki.Text = "Brak ocen";
                return;
            }
            
            int liczbaOcen = dataTable.Rows.Count;
            int sumaWszystkich = 0;
            int najwyzsza = 0;
            int najnizsza = 100;
            
            foreach (DataRow row in dataTable.Rows)
            {
                if (row["PunktyRazem"] != DBNull.Value)
                {
                    int punkty = Convert.ToInt32(row["PunktyRazem"]);
                    sumaWszystkich += punkty;
                    
                    if (punkty > najwyzsza) najwyzsza = punkty;
                    if (punkty < najnizsza) najnizsza = punkty;
                }
            }
            
            double srednia = liczbaOcen > 0 ? (double)sumaWszystkich / liczbaOcen : 0;
            
            txtStatystyki.Text = $"Ocen: {liczbaOcen} | Śr: {srednia:F1} | Max: {najwyzsza} | Min: {najnizsza}";
        }
        
        /// <summary>
        /// Zamyka okno
        /// </summary>
        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
    
    /// <summary>
    /// Konwerter punktów na kolor (do kolorowania wierszy) - zwraca SolidColorBrush
    /// </summary>
    public class PointsToColorConverter : System.Windows.Data.IValueConverter
    {
        // Predefiniowane kolory
        private static readonly SolidColorBrush GreenBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57));    // Szmaragdowy
        private static readonly SolidColorBrush GoldBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37));     // Złoty
        private static readonly SolidColorBrush RedBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0x14, 0x3C));      // Karmazynowy
        private static readonly SolidColorBrush GrayBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));     // Szary

        static PointsToColorConverter()
        {
            // Zamrożenie brushy dla lepszej wydajności
            GreenBrush.Freeze();
            GoldBrush.Freeze();
            RedBrush.Freeze();
            GrayBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null || value == DBNull.Value)
                return GrayBrush;
                
            int punkty = System.Convert.ToInt32(value);
            
            if (punkty >= 30) return GreenBrush;   // Bardzo dobry
            if (punkty >= 20) return GoldBrush;    // Dobry
            return RedBrush;                        // Niezadowalający
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
