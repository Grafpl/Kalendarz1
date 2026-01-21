using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.Zywiec.WidokSpecyfikacji
{
    /// <summary>
    /// Okno mapowania dostawców z LibraNet do kontrahentów w Symfonii Handel
    /// </summary>
    public partial class MapowanieDostawcowWindow : Window, INotifyPropertyChanged
    {
        private string libraNetConnectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string symfoniaConnectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public ObservableCollection<DostawcaMapowanie> Dostawcy { get; set; }
        public ObservableCollection<KontrahentSymfonia> Kontrahenci { get; set; }

        private List<DostawcaMapowanie> _allDostawcy;
        private List<KontrahentSymfonia> _allKontrahenci;

        public event PropertyChangedEventHandler PropertyChanged;

        public MapowanieDostawcowWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            DataContext = this;

            Dostawcy = new ObservableCollection<DostawcaMapowanie>();
            Kontrahenci = new ObservableCollection<KontrahentSymfonia>();

            _allDostawcy = new List<DostawcaMapowanie>();
            _allKontrahenci = new List<KontrahentSymfonia>();

            LoadData();
        }

        private void LoadData()
        {
            txtStatus.Text = "Ładowanie danych...";
            try
            {
                LoadDostawcy();
                LoadKontrahenci();
                txtStatus.Text = $"Załadowano {_allDostawcy.Count} dostawców i {_allKontrahenci.Count} kontrahentów";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Błąd ładowania: {ex.Message}";
                MessageBox.Show($"Błąd ładowania danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDostawcy()
        {
            _allDostawcy = new List<DostawcaMapowanie>();

            using (var conn = new SqlConnection(libraNetConnectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT ID, ShortName, ISNULL(IdSymf, 0) AS IdSymf
                    FROM dbo.Dostawcy
                    ORDER BY ShortName", conn);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _allDostawcy.Add(new DostawcaMapowanie
                        {
                            ID = reader["ID"]?.ToString()?.Trim() ?? "",
                            ShortName = reader["ShortName"]?.ToString()?.Trim() ?? "",
                            IdSymf = Convert.ToInt32(reader["IdSymf"])
                        });
                    }
                }
            }

            ApplyDostawcyFilter();
        }

        private void LoadKontrahenci()
        {
            _allKontrahenci = new List<KontrahentSymfonia>();

            using (var conn = new SqlConnection(symfoniaConnectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT Id, ISNULL(Code,'') AS Code, ISNULL(NIP,'') AS NIP, ISNULL(Name,'') AS Name
                    FROM SSCommon.STContractors
                    ORDER BY Name", conn);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _allKontrahenci.Add(new KontrahentSymfonia
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            Code = reader["Code"]?.ToString()?.Trim() ?? "",
                            NIP = reader["NIP"]?.ToString()?.Trim() ?? "",
                            Name = reader["Name"]?.ToString()?.Trim() ?? ""
                        });
                    }
                }
            }

            ApplyKontrahenciFilter();
        }

        private void ApplyDostawcyFilter()
        {
            var filter = txtFilterDostawcy?.Text?.ToLower() ?? "";
            var onlyUnmapped = chkOnlyUnmapped?.IsChecked ?? false;

            var filtered = _allDostawcy
                .Where(d => string.IsNullOrEmpty(filter) ||
                           (d.ShortName?.ToLower().Contains(filter) == true) ||
                           (d.ID?.ToLower().Contains(filter) == true))
                .Where(d => !onlyUnmapped || d.IsUnmapped)
                .ToList();

            Dostawcy.Clear();
            foreach (var d in filtered)
                Dostawcy.Add(d);
        }

        private void ApplyKontrahenciFilter()
        {
            var filter = txtFilterKontrahenci?.Text?.ToLower() ?? "";

            var filtered = _allKontrahenci
                .Where(k => string.IsNullOrEmpty(filter) ||
                           (k.Name?.ToLower().Contains(filter) == true) ||
                           (k.Code?.ToLower().Contains(filter) == true) ||
                           (k.NIP?.Contains(filter) == true))
                .ToList();

            Kontrahenci.Clear();
            foreach (var k in filtered)
                Kontrahenci.Add(k);
        }

        private void DgDostawcy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var dostawca = dgDostawcy.SelectedItem as DostawcaMapowanie;
            if (dostawca != null)
            {
                txtSelectedDostawca.Text = $"{dostawca.ShortName} (ID: {dostawca.ID})";
            }
            else
            {
                txtSelectedDostawca.Text = "(nie wybrano)";
            }
        }

        private void BtnPrzypisz_Click(object sender, RoutedEventArgs e)
        {
            var dostawca = dgDostawcy.SelectedItem as DostawcaMapowanie;
            var kontrahent = dgKontrahenci.SelectedItem as KontrahentSymfonia;

            if (dostawca == null)
            {
                MessageBox.Show("Wybierz dostawcę z listy!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (kontrahent == null)
            {
                MessageBox.Show("Wybierz kontrahenta Symfonia z listy!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Potwierdzenie jeśli nadpisujemy
            if (dostawca.IdSymf > 0)
            {
                var result = MessageBox.Show(
                    $"Dostawca '{dostawca.ShortName}' ma już przypisane mapowanie (IdSymf={dostawca.IdSymf}).\n\nCzy chcesz je nadpisać?",
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            try
            {
                txtStatus.Text = "Zapisywanie...";

                using (var conn = new SqlConnection(libraNetConnectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("UPDATE dbo.Dostawcy SET IdSymf = @IdSymf WHERE ID = @ID", conn);
                    cmd.Parameters.AddWithValue("@IdSymf", kontrahent.Id);
                    cmd.Parameters.AddWithValue("@ID", dostawca.ID);
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show(
                    $"Przypisano pomyślnie!\n\n{dostawca.ShortName} → {kontrahent.Name}\n(IdSymf = {kontrahent.Id})",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                txtStatus.Text = $"Zapisano: {dostawca.ShortName} → {kontrahent.Name}";
                LoadDostawcy(); // Odśwież listę
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Błąd zapisu: {ex.Message}";
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnUsunMapowanie_Click(object sender, RoutedEventArgs e)
        {
            var dostawca = dgDostawcy.SelectedItem as DostawcaMapowanie;

            if (dostawca == null)
            {
                MessageBox.Show("Wybierz dostawcę z listy!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dostawca.IdSymf == 0)
            {
                MessageBox.Show("Ten dostawca nie ma przypisanego mapowania.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Czy na pewno usunąć mapowanie dla '{dostawca.ShortName}'?\n\nObecne mapowanie: IdSymf = {dostawca.IdSymf}",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                txtStatus.Text = "Usuwanie mapowania...";

                using (var conn = new SqlConnection(libraNetConnectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("UPDATE dbo.Dostawcy SET IdSymf = NULL WHERE ID = @ID", conn);
                    cmd.Parameters.AddWithValue("@ID", dostawca.ID);
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show($"Mapowanie dla '{dostawca.ShortName}' zostało usunięte.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                txtStatus.Text = $"Usunięto mapowanie: {dostawca.ShortName}";
                LoadDostawcy();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Błąd: {ex.Message}";
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Event handlers dla filtrów
        private void TxtFilterDostawcy_TextChanged(object sender, TextChangedEventArgs e) => ApplyDostawcyFilter();
        private void ChkOnlyUnmapped_Changed(object sender, RoutedEventArgs e) => ApplyDostawcyFilter();
        private void TxtFilterKontrahenci_TextChanged(object sender, TextChangedEventArgs e) => ApplyKontrahenciFilter();
        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadData();
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Model dostawcy z LibraNet do mapowania
    /// </summary>
    public class DostawcaMapowanie : INotifyPropertyChanged
    {
        private string _id;
        private string _shortName;
        private int _idSymf;

        public string ID
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(ID)); }
        }

        public string ShortName
        {
            get => _shortName;
            set { _shortName = value; OnPropertyChanged(nameof(ShortName)); }
        }

        public int IdSymf
        {
            get => _idSymf;
            set
            {
                _idSymf = value;
                OnPropertyChanged(nameof(IdSymf));
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(IsUnmapped));
            }
        }

        public string Status => IdSymf > 0 ? "Zmapowany" : "Niezmapowany";
        public bool IsUnmapped => IdSymf == 0;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Model kontrahenta z Symfonii
    /// </summary>
    public class KontrahentSymfonia : INotifyPropertyChanged
    {
        private int _id;
        private string _code;
        private string _nip;
        private string _name;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string Code
        {
            get => _code;
            set { _code = value; OnPropertyChanged(nameof(Code)); }
        }

        public string NIP
        {
            get => _nip;
            set { _nip = value; OnPropertyChanged(nameof(NIP)); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        // Do wyświetlania w UI
        public string DisplayText => $"{Code} - {Name} (NIP: {NIP})";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
