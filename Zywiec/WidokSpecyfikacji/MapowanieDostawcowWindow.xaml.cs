using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            ShowLoading("Ładowanie danych...");
            try
            {
                await Task.Run(() =>
                {
                    LoadDostawcyFromDb();
                    LoadKontrahenciFromDb();
                });

                Dispatcher.Invoke(() =>
                {
                    // Uzupełnij nazwy zmapowanych kontrahentów
                    UpdateMappedNames();
                    ApplyDostawcyFilter();
                    ApplyKontrahenciFilter();
                    UpdateStatistics();
                    txtStatus.Text = $"Załadowano {_allDostawcy.Count} dostawców i {_allKontrahenci.Count} kontrahentów";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    txtStatus.Text = $"Błąd: {ex.Message}";
                    MessageBox.Show($"Błąd ładowania danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                HideLoading();
            }
        }

        private void LoadDostawcyFromDb()
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
        }

        private void LoadKontrahenciFromDb()
        {
            _allKontrahenci = new List<KontrahentSymfonia>();

            using (var conn = new SqlConnection(symfoniaConnectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT Id, ISNULL(Shortcut,'') AS Code, ISNULL(NIP,'') AS NIP, ISNULL(Name,'') AS Name
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
        }

        private void UpdateMappedNames()
        {
            var kontrahenciDict = _allKontrahenci.ToDictionary(k => k.Id, k => k.Name);
            foreach (var d in _allDostawcy)
            {
                if (d.IdSymf > 0 && kontrahenciDict.TryGetValue(d.IdSymf, out var name))
                {
                    d.MappedName = name;
                }
                else
                {
                    d.MappedName = "";
                }
            }
        }

        private void UpdateStatistics()
        {
            var total = _allDostawcy.Count;
            var mapped = _allDostawcy.Count(d => d.IdSymf > 0);
            var unmapped = total - mapped;
            var percent = total > 0 ? (mapped * 100 / total) : 0;

            txtStatZmapowani.Text = mapped.ToString();
            txtStatNiezmapowani.Text = unmapped.ToString();
            txtStatProcent.Text = $"{percent}%";
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

            txtDostawcyCount.Text = $"({filtered.Count} z {_allDostawcy.Count})";
        }

        private void ApplyKontrahenciFilter()
        {
            var filter = txtFilterKontrahenci?.Text?.ToLower() ?? "";

            var filtered = _allKontrahenci
                .Where(k => string.IsNullOrEmpty(filter) ||
                           (k.Name?.ToLower().Contains(filter) == true) ||
                           (k.Code?.ToLower().Contains(filter) == true) ||
                           (k.NIP?.Contains(filter) == true))
                .Take(500) // Limit dla wydajności
                .ToList();

            Kontrahenci.Clear();
            foreach (var k in filtered)
                Kontrahenci.Add(k);

            var totalFiltered = _allKontrahenci.Count(k => string.IsNullOrEmpty(filter) ||
                           (k.Name?.ToLower().Contains(filter) == true) ||
                           (k.Code?.ToLower().Contains(filter) == true) ||
                           (k.NIP?.Contains(filter) == true));

            txtKontrahenciCount.Text = totalFiltered > 500
                ? $"(pokazano 500 z {totalFiltered})"
                : $"({totalFiltered} z {_allKontrahenci.Count})";
        }

        private void UpdateButtonStates()
        {
            var dostawca = dgDostawcy.SelectedItem as DostawcaMapowanie;
            var kontrahent = dgKontrahenci.SelectedItem as KontrahentSymfonia;

            btnPrzypisz.IsEnabled = dostawca != null && kontrahent != null;
            btnUsunMapowanie.IsEnabled = dostawca != null && dostawca.IdSymf > 0;
        }

        private void DgDostawcy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var dostawca = dgDostawcy.SelectedItem as DostawcaMapowanie;
            if (dostawca != null)
            {
                txtSelectedDostawca.Text = $"{dostawca.ShortName} (ID: {dostawca.ID})";
                if (dostawca.IdSymf > 0)
                {
                    txtSelectedDostawca.Text += $" → Symfonia ID: {dostawca.IdSymf}";
                    txtSelectedHint.Text = "🔄 Możesz zmienić przypisanie lub usunąć mapowanie";
                }
                else
                {
                    txtSelectedHint.Text = "💡 Wybierz kontrahenta poniżej i kliknij PRZYPISZ";
                }

                // Auto-filtruj kontrahentów po nazwie dostawcy
                if (string.IsNullOrEmpty(txtFilterKontrahenci.Text))
                {
                    var nameParts = dostawca.ShortName?.Split(' ');
                    if (nameParts != null && nameParts.Length > 0)
                    {
                        txtFilterKontrahenci.Text = nameParts[0];
                    }
                }
            }
            else
            {
                txtSelectedDostawca.Text = "(kliknij na dostawcę aby wybrać)";
                txtSelectedHint.Text = "💡 Dwuklik = szybkie przypisanie";
            }

            UpdateButtonStates();
        }

        private void DgDostawcy_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dostawca = dgDostawcy.SelectedItem as DostawcaMapowanie;
            if (dostawca != null && dostawca.IdSymf > 0)
            {
                // Jeśli już zmapowany - przewiń do kontrahenta
                var kontrahent = _allKontrahenci.FirstOrDefault(k => k.Id == dostawca.IdSymf);
                if (kontrahent != null)
                {
                    txtFilterKontrahenci.Text = "";
                    ApplyKontrahenciFilter();
                    dgKontrahenci.SelectedItem = Kontrahenci.FirstOrDefault(k => k.Id == kontrahent.Id);
                    dgKontrahenci.ScrollIntoView(dgKontrahenci.SelectedItem);
                }
            }
        }

        private void DgKontrahenci_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Dwuklik na kontrahenta = przypisz
            if (dgDostawcy.SelectedItem != null && dgKontrahenci.SelectedItem != null)
            {
                BtnPrzypisz_Click(sender, null);
            }
        }

        private void BtnPrzypisz_Click(object sender, RoutedEventArgs e)
        {
            var dostawca = dgDostawcy.SelectedItem as DostawcaMapowanie;
            var kontrahent = dgKontrahenci.SelectedItem as KontrahentSymfonia;

            if (dostawca == null)
            {
                MessageBox.Show("Wybierz dostawcę z górnej listy!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (kontrahent == null)
            {
                MessageBox.Show("Wybierz kontrahenta Symfonia z dolnej listy!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Szczegółowe potwierdzenie
            string message;
            if (dostawca.IdSymf > 0)
            {
                var oldKontrahent = _allKontrahenci.FirstOrDefault(k => k.Id == dostawca.IdSymf);
                message = $"ZMIANA MAPOWANIA\n\n" +
                          $"Dostawca LibraNet:\n" +
                          $"   {dostawca.ShortName} (ID: {dostawca.ID})\n\n" +
                          $"Obecne przypisanie:\n" +
                          $"   {oldKontrahent?.Name ?? "nieznany"} (IdSymf: {dostawca.IdSymf})\n\n" +
                          $"Nowe przypisanie:\n" +
                          $"   {kontrahent.Name} (Id: {kontrahent.Id})\n\n" +
                          $"Czy na pewno chcesz zmienić przypisanie?";
            }
            else
            {
                message = $"NOWE MAPOWANIE\n\n" +
                          $"Dostawca LibraNet:\n" +
                          $"   {dostawca.ShortName} (ID: {dostawca.ID})\n\n" +
                          $"Zostanie przypisany do:\n" +
                          $"   {kontrahent.Name}\n" +
                          $"   Kod: {kontrahent.Code}\n" +
                          $"   NIP: {kontrahent.NIP}\n" +
                          $"   Id Symfonia: {kontrahent.Id}\n\n" +
                          $"Czy potwierdzasz przypisanie?";
            }

            var result = MessageBox.Show(message, "Potwierdzenie mapowania",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                txtStatus.Text = "Zapisywanie mapowania...";

                using (var conn = new SqlConnection(libraNetConnectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("UPDATE dbo.Dostawcy SET IdSymf = @IdSymf WHERE ID = @ID", conn);
                    cmd.Parameters.AddWithValue("@IdSymf", kontrahent.Id);
                    cmd.Parameters.AddWithValue("@ID", dostawca.ID);
                    cmd.ExecuteNonQuery();
                }

                // Aktualizuj lokalnie
                dostawca.IdSymf = kontrahent.Id;
                dostawca.MappedName = kontrahent.Name;

                // Aktualizuj w źródłowej liście
                var sourceDostawca = _allDostawcy.FirstOrDefault(d => d.ID == dostawca.ID);
                if (sourceDostawca != null)
                {
                    sourceDostawca.IdSymf = kontrahent.Id;
                    sourceDostawca.MappedName = kontrahent.Name;
                }

                UpdateStatistics();
                UpdateButtonStates();

                txtStatus.Text = $"✅ Przypisano: {dostawca.ShortName} → {kontrahent.Name}";

                MessageBox.Show(
                    $"✅ MAPOWANIE ZAPISANE\n\n" +
                    $"{dostawca.ShortName}\n" +
                    $"   ↓\n" +
                    $"{kontrahent.Name}\n\n" +
                    $"IdSymf = {kontrahent.Id}",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                // Przejdź do następnego niezmapowanego
                MoveToNextUnmapped();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"❌ Błąd: {ex.Message}";
                MessageBox.Show($"Błąd zapisu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MoveToNextUnmapped()
        {
            var currentIndex = dgDostawcy.SelectedIndex;
            for (int i = currentIndex + 1; i < Dostawcy.Count; i++)
            {
                if (Dostawcy[i].IsUnmapped)
                {
                    dgDostawcy.SelectedIndex = i;
                    dgDostawcy.ScrollIntoView(Dostawcy[i]);
                    txtFilterKontrahenci.Text = "";
                    return;
                }
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

            var kontrahent = _allKontrahenci.FirstOrDefault(k => k.Id == dostawca.IdSymf);

            var result = MessageBox.Show(
                $"USUWANIE MAPOWANIA\n\n" +
                $"Dostawca LibraNet:\n" +
                $"   {dostawca.ShortName} (ID: {dostawca.ID})\n\n" +
                $"Obecne przypisanie:\n" +
                $"   {kontrahent?.Name ?? "nieznany"} (IdSymf: {dostawca.IdSymf})\n\n" +
                $"Czy na pewno chcesz USUNĄĆ to mapowanie?",
                "Potwierdzenie usunięcia", MessageBoxButton.YesNo, MessageBoxImage.Warning);

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

                // Aktualizuj lokalnie
                dostawca.IdSymf = 0;
                dostawca.MappedName = "";

                // Aktualizuj w źródłowej liście
                var sourceDostawca = _allDostawcy.FirstOrDefault(d => d.ID == dostawca.ID);
                if (sourceDostawca != null)
                {
                    sourceDostawca.IdSymf = 0;
                    sourceDostawca.MappedName = "";
                }

                UpdateStatistics();
                UpdateButtonStates();

                txtStatus.Text = $"🗑️ Usunięto mapowanie: {dostawca.ShortName}";

                MessageBox.Show(
                    $"Mapowanie dla '{dostawca.ShortName}' zostało usunięte.",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"❌ Błąd: {ex.Message}";
                MessageBox.Show($"Błąd:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAutoMatch_Click(object sender, RoutedEventArgs e)
        {
            var unmapped = _allDostawcy.Where(d => d.IsUnmapped).ToList();
            if (unmapped.Count == 0)
            {
                MessageBox.Show("Wszyscy dostawcy są już zmapowani!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var matches = new List<(DostawcaMapowanie dostawca, KontrahentSymfonia kontrahent)>();

            foreach (var d in unmapped)
            {
                // Szukaj dokładnego dopasowania po nazwie
                var match = _allKontrahenci.FirstOrDefault(k =>
                    k.Name?.Equals(d.ShortName, StringComparison.OrdinalIgnoreCase) == true ||
                    k.Code?.Equals(d.ShortName, StringComparison.OrdinalIgnoreCase) == true);

                if (match != null)
                {
                    matches.Add((d, match));
                }
            }

            if (matches.Count == 0)
            {
                MessageBox.Show(
                    $"Nie znaleziono automatycznych dopasowań.\n\n" +
                    $"Sprawdzono {unmapped.Count} niezmapowanych dostawców.\n" +
                    $"Dopasowanie wymaga identycznej nazwy.",
                    "Brak dopasowań", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var message = $"Znaleziono {matches.Count} potencjalnych dopasowań:\n\n";
            foreach (var m in matches.Take(10))
            {
                message += $"• {m.dostawca.ShortName} → {m.kontrahent.Name}\n";
            }
            if (matches.Count > 10)
            {
                message += $"... i {matches.Count - 10} więcej\n";
            }
            message += $"\nCzy chcesz automatycznie przypisać te mapowania?";

            var result = MessageBox.Show(message, "Auto-dopasowanie",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                txtStatus.Text = "Zapisywanie auto-dopasowań...";
                int saved = 0;

                using (var conn = new SqlConnection(libraNetConnectionString))
                {
                    conn.Open();
                    foreach (var m in matches)
                    {
                        var cmd = new SqlCommand("UPDATE dbo.Dostawcy SET IdSymf = @IdSymf WHERE ID = @ID", conn);
                        cmd.Parameters.AddWithValue("@IdSymf", m.kontrahent.Id);
                        cmd.Parameters.AddWithValue("@ID", m.dostawca.ID);
                        cmd.ExecuteNonQuery();

                        m.dostawca.IdSymf = m.kontrahent.Id;
                        m.dostawca.MappedName = m.kontrahent.Name;

                        var source = _allDostawcy.FirstOrDefault(d => d.ID == m.dostawca.ID);
                        if (source != null)
                        {
                            source.IdSymf = m.kontrahent.Id;
                            source.MappedName = m.kontrahent.Name;
                        }

                        saved++;
                    }
                }

                ApplyDostawcyFilter();
                UpdateStatistics();

                txtStatus.Text = $"✅ Auto-dopasowano {saved} dostawców";
                MessageBox.Show($"Automatycznie przypisano {saved} mapowań.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"❌ Błąd: {ex.Message}";
                MessageBox.Show($"Błąd:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (btnPrzypisz.IsEnabled)
                    BtnPrzypisz_Click(sender, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                if (btnUsunMapowanie.IsEnabled)
                    BtnUsunMapowanie_Click(sender, null);
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                BtnRefresh_Click(sender, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void ShowLoading(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtLoading.Text = message;
                loadingOverlay.Visibility = Visibility.Visible;
            });
        }

        private void HideLoading()
        {
            Dispatcher.Invoke(() =>
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            });
        }

        // Event handlers
        private void TxtFilterDostawcy_TextChanged(object sender, TextChangedEventArgs e) => ApplyDostawcyFilter();
        private void ChkOnlyUnmapped_Changed(object sender, RoutedEventArgs e) => ApplyDostawcyFilter();
        private void TxtFilterKontrahenci_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyKontrahenciFilter();
            UpdateButtonStates();
        }
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadDataAsync();
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
        private string _mappedName;

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
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusBackground));
                OnPropertyChanged(nameof(StatusForeground));
                OnPropertyChanged(nameof(IsUnmapped));
            }
        }

        public string MappedName
        {
            get => _mappedName;
            set { _mappedName = value; OnPropertyChanged(nameof(MappedName)); }
        }

        public string StatusIcon => IdSymf > 0 ? "✓ Zmapowany" : "⚠ Brak";
        public Brush StatusBackground => IdSymf > 0 ? new SolidColorBrush(Color.FromRgb(200, 230, 201)) : new SolidColorBrush(Color.FromRgb(255, 224, 178));
        public Brush StatusForeground => IdSymf > 0 ? new SolidColorBrush(Color.FromRgb(46, 125, 50)) : new SolidColorBrush(Color.FromRgb(230, 81, 0));
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
