using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;

namespace Kalendarz1
{
    public partial class ImportAvilogWindow : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private ObservableCollection<ImportAvilogRow> importData;
        private AvilogPdfParser parser;

        // Listy dla ComboBox
        public List<KierowcaItem> ListaKierowcow { get; set; }
        public List<HodowcaItem> ListaHodowcow { get; set; }

        // Mapowania z bazy
        private Dictionary<string, int> savedMappings = new Dictionary<string, int>();

        // Wynik importu
        public List<ImportAvilogRow> ImportedRows { get; private set; }
        public DateTime? ImportedDate { get; private set; }
        public bool ImportSuccess { get; private set; }

        public ImportAvilogWindow()
        {
            InitializeComponent();
            parser = new AvilogPdfParser();
            importData = new ObservableCollection<ImportAvilogRow>();

            DataContext = this;
            dataGridImport.ItemsSource = importData;

            LoadKierowcy();
            LoadHodowcy();
            LoadSavedMappings();
        }

        #region Ładowanie danych słownikowych

        private void LoadKierowcy()
        {
            ListaKierowcow = new List<KierowcaItem>();
            ListaKierowcow.Add(new KierowcaItem { GID = null, Name = "(nie wybrano)" });

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT GID, [Name] FROM [LibraNet].[dbo].[Driver] WHERE Deleted = 0 ORDER BY Name ASC";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ListaKierowcow.Add(new KierowcaItem
                            {
                                GID = reader.GetInt32(0),
                                Name = reader["Name"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania kierowców: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadHodowcy()
        {
            ListaHodowcow = new List<HodowcaItem>();
            ListaHodowcow.Add(new HodowcaItem { GID = null, ShortName = "(nie wybrano)" });

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT ID AS GID, ShortName, Name, City FROM dbo.Dostawcy WHERE halt = 0 ORDER BY ShortName";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ListaHodowcow.Add(new HodowcaItem
                            {
                                GID = reader.GetInt32(0),
                                ShortName = reader["ShortName"]?.ToString() ?? "",
                                FullName = reader["Name"]?.ToString() ?? "",
                                City = reader["City"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania hodowców: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSavedMappings()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Sprawdź czy tabela istnieje
                    string checkTable = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AvilogHodowcyMapping')
                        CREATE TABLE dbo.AvilogHodowcyMapping (
                            ID INT IDENTITY(1,1) PRIMARY KEY,
                            AvilogNazwa NVARCHAR(200) NOT NULL,
                            AvilogAdres NVARCHAR(300) NULL,
                            MappedGID INT NOT NULL,
                            CreatedDate DATETIME DEFAULT GETDATE(),
                            CreatedBy NVARCHAR(100) NULL,
                            CONSTRAINT UQ_AvilogNazwa UNIQUE (AvilogNazwa)
                        )";
                    using (SqlCommand cmd = new SqlCommand(checkTable, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // Załaduj istniejące mapowania
                    string query = "SELECT AvilogNazwa, MappedGID FROM dbo.AvilogHodowcyMapping";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string avilogNazwa = reader["AvilogNazwa"]?.ToString()?.ToUpper() ?? "";
                            int mappedGid = reader.GetInt32(1);
                            if (!savedMappings.ContainsKey(avilogNazwa))
                            {
                                savedMappings[avilogNazwa] = mappedGid;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania mapowań: {ex.Message}");
            }
        }

        #endregion

        #region Wybór i parsowanie PDF

        private void BtnWybierzPlik_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Pliki PDF (*.pdf)|*.pdf|Wszystkie pliki (*.*)|*.*",
                Title = "Wybierz plik PDF z planem transportu AVILOG"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                lblNazwaPliku.Text = System.IO.Path.GetFileName(filePath);

                ParseAndLoadPdf(filePath);
            }
        }

        private void ParseAndLoadPdf(string filePath)
        {
            try
            {
                lblStatus.Text = "Parsowanie pliku PDF...";
                importData.Clear();

                var result = parser.ParsePdf(filePath);

                if (!result.Success)
                {
                    MessageBox.Show($"Błąd parsowania PDF:\n{result.ErrorMessage}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    lblStatus.Text = "Błąd parsowania pliku";
                    return;
                }

                if (result.Wiersze.Count == 0)
                {
                    MessageBox.Show("Nie znaleziono danych transportowych w pliku PDF.\n\nUpewnij się, że wybrany plik to plan transportu AVILOG.",
                        "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                    lblStatus.Text = "Brak danych w pliku";
                    return;
                }

                // Ustaw datę
                ImportedDate = result.DataUboju;
                lblDataUboju.Text = result.DataUboju?.ToString("dd.MM.yyyy (dddd)") ?? "Nie rozpoznano";

                // Konwertuj na wiersze do wyświetlenia
                int lp = 1;
                foreach (var row in result.Wiersze)
                {
                    var importRow = new ImportAvilogRow
                    {
                        Lp = lp++,
                        AvilogKierowca = $"{row.KierowcaNazwa}\n{row.KierowcaTelefon}",
                        AvilogHodowca = row.HodowcaNazwa,
                        AvilogAdres = $"{row.HodowcaAdres}\n{row.HodowcaKodPocztowy} {row.HodowcaMiejscowosc}".Trim(),
                        Ciagnik = row.Ciagnik,
                        Naczepa = row.Naczepa,
                        Sztuki = row.Sztuki,
                        WagaDek = row.WagaDek,
                        WyjazdZaklad = row.WyjazdZaklad,
                        PoczatekZaladunku = row.PoczatekZaladunku,
                        PowrotZaklad = row.PowrotZaklad,
                        Obserwacje = row.Obserwacje,
                        OriginalData = row
                    };

                    // Spróbuj automatycznie zmapować kierowcę
                    AutoMapKierowca(importRow, row.KierowcaNazwa);

                    // Spróbuj zmapować hodowcę z zapisanych mapowań
                    AutoMapHodowca(importRow, row.HodowcaNazwa);

                    importData.Add(importRow);
                }

                lblLiczbaWierszy.Text = importData.Count.ToString();
                UpdateNiezamapowaniCount();

                btnImportuj.IsEnabled = true;
                lblStatus.Text = $"Załadowano {importData.Count} wierszy. Sprawdź mapowania hodowców.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas przetwarzania pliku:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                lblStatus.Text = "Błąd przetwarzania";
            }
        }

        #endregion

        #region Auto-mapowanie

        private void AutoMapKierowca(ImportAvilogRow row, string kierowcaNazwa)
        {
            if (string.IsNullOrWhiteSpace(kierowcaNazwa)) return;

            string searchName = kierowcaNazwa.ToUpper().Trim();

            // Szukaj dokładnego dopasowania
            var exactMatch = ListaKierowcow.FirstOrDefault(k =>
                k.Name != null && k.Name.ToUpper().Trim() == searchName);

            if (exactMatch != null && exactMatch.GID.HasValue)
            {
                row.MappedKierowcaGID = exactMatch.GID;
                return;
            }

            // Szukaj po nazwisku (drugie słowo)
            var nameParts = searchName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (nameParts.Length >= 2)
            {
                string lastName = nameParts.Last();
                var partialMatch = ListaKierowcow.FirstOrDefault(k =>
                    k.Name != null && k.Name.ToUpper().Contains(lastName));

                if (partialMatch != null && partialMatch.GID.HasValue)
                {
                    row.MappedKierowcaGID = partialMatch.GID;
                }
            }
        }

        private void AutoMapHodowca(ImportAvilogRow row, string hodowcaNazwa)
        {
            if (string.IsNullOrWhiteSpace(hodowcaNazwa)) return;

            string searchName = hodowcaNazwa.ToUpper().Trim();

            // Najpierw sprawdź zapisane mapowania
            if (savedMappings.ContainsKey(searchName))
            {
                row.MappedHodowcaGID = savedMappings[searchName];
                return;
            }

            // Szukaj dokładnego dopasowania w bazie
            var exactMatch = ListaHodowcow.FirstOrDefault(h =>
                h.ShortName != null && h.ShortName.ToUpper().Trim() == searchName);

            if (exactMatch != null && exactMatch.GID.HasValue)
            {
                row.MappedHodowcaGID = exactMatch.GID;
                return;
            }

            // Szukaj po pierwszym słowie (nazwisko)
            var nameParts = searchName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (nameParts.Length >= 1)
            {
                string firstPart = nameParts[0];
                var partialMatch = ListaHodowcow.FirstOrDefault(h =>
                    h.ShortName != null && h.ShortName.ToUpper().StartsWith(firstPart));

                if (partialMatch != null && partialMatch.GID.HasValue)
                {
                    row.MappedHodowcaGID = partialMatch.GID;
                }
            }
        }

        private void BtnAutoMapuj_Click(object sender, RoutedEventArgs e)
        {
            int mapped = 0;
            foreach (var row in importData)
            {
                if (!row.MappedHodowcaGID.HasValue)
                {
                    // Próbuj różne strategie dopasowania
                    string avilogNazwa = row.AvilogHodowca?.ToUpper().Trim() ?? "";

                    // Strategia 1: Fuzzy matching po słowach
                    var bestMatch = FindBestHodowcaMatch(avilogNazwa);
                    if (bestMatch != null)
                    {
                        row.MappedHodowcaGID = bestMatch.GID;
                        mapped++;
                    }
                }
            }

            UpdateNiezamapowaniCount();
            lblStatus.Text = $"Automatycznie zmapowano {mapped} hodowców.";
        }

        private HodowcaItem FindBestHodowcaMatch(string avilogNazwa)
        {
            if (string.IsNullOrWhiteSpace(avilogNazwa)) return null;

            var words = avilogNazwa.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return null;

            int bestScore = 0;
            HodowcaItem bestMatch = null;

            foreach (var hodowca in ListaHodowcow.Where(h => h.GID.HasValue))
            {
                string hodowcaNazwa = hodowca.ShortName?.ToUpper() ?? "";
                int score = 0;

                // Sprawdź ile słów się zgadza
                foreach (var word in words)
                {
                    if (word.Length >= 3 && hodowcaNazwa.Contains(word))
                    {
                        score += word.Length;
                    }
                }

                if (score > bestScore && score >= 5) // Minimum 5 znaków dopasowania
                {
                    bestScore = score;
                    bestMatch = hodowca;
                }
            }

            return bestMatch;
        }

        private void UpdateNiezamapowaniCount()
        {
            int niezamapowani = importData.Count(r => !r.MappedHodowcaGID.HasValue);
            lblNiezamapowani.Text = niezamapowani.ToString();

            // Odśwież widok żeby zaktualizować statusy
            dataGridImport.Items.Refresh();
        }

        #endregion

        #region Import

        private void BtnImportuj_Click(object sender, RoutedEventArgs e)
        {
            // Sprawdź czy są niezamapowani hodowcy
            var niezamapowani = importData.Where(r => !r.MappedHodowcaGID.HasValue).ToList();
            if (niezamapowani.Any())
            {
                var result = MessageBox.Show(
                    $"Nie wszystkie hodowcy zostali zmapowani ({niezamapowani.Count} niezamapowanych).\n\n" +
                    "Niezamapowani:\n" +
                    string.Join("\n", niezamapowani.Take(5).Select(r => $"  - {r.AvilogHodowca}")) +
                    (niezamapowani.Count > 5 ? $"\n  ... i {niezamapowani.Count - 5} więcej" : "") +
                    "\n\nCzy chcesz kontynuować? Niezamapowane wiersze zostaną pominięte.",
                    "Niezamapowani hodowcy",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            // Zapisz mapowania jeśli zaznaczono
            if (chkZapamietajMapowania.IsChecked == true)
            {
                SaveMappings();
            }

            // Przygotuj wynik
            ImportedRows = importData.Where(r => r.MappedHodowcaGID.HasValue).ToList();
            ImportSuccess = true;

            lblStatus.Text = $"Zaimportowano {ImportedRows.Count} wierszy.";
            DialogResult = true;
            Close();
        }

        private void SaveMappings()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    foreach (var row in importData.Where(r => r.MappedHodowcaGID.HasValue))
                    {
                        string avilogNazwa = row.AvilogHodowca?.ToUpper().Trim() ?? "";
                        if (string.IsNullOrEmpty(avilogNazwa)) continue;

                        // Sprawdź czy już istnieje
                        if (savedMappings.ContainsKey(avilogNazwa))
                        {
                            // Aktualizuj jeśli się zmieniło
                            if (savedMappings[avilogNazwa] != row.MappedHodowcaGID.Value)
                            {
                                string updateSql = @"UPDATE dbo.AvilogHodowcyMapping
                                                    SET MappedGID = @MappedGID
                                                    WHERE AvilogNazwa = @AvilogNazwa";
                                using (SqlCommand cmd = new SqlCommand(updateSql, conn))
                                {
                                    cmd.Parameters.AddWithValue("@AvilogNazwa", avilogNazwa);
                                    cmd.Parameters.AddWithValue("@MappedGID", row.MappedHodowcaGID.Value);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }
                        else
                        {
                            // Dodaj nowe mapowanie
                            string insertSql = @"INSERT INTO dbo.AvilogHodowcyMapping
                                                (AvilogNazwa, AvilogAdres, MappedGID, CreatedBy)
                                                VALUES (@AvilogNazwa, @AvilogAdres, @MappedGID, @CreatedBy)";
                            using (SqlCommand cmd = new SqlCommand(insertSql, conn))
                            {
                                cmd.Parameters.AddWithValue("@AvilogNazwa", avilogNazwa);
                                cmd.Parameters.AddWithValue("@AvilogAdres", row.AvilogAdres ?? "");
                                cmd.Parameters.AddWithValue("@MappedGID", row.MappedHodowcaGID.Value);
                                cmd.Parameters.AddWithValue("@CreatedBy", App.UserID);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd zapisywania mapowań: {ex.Message}");
            }
        }

        #endregion

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            ImportSuccess = false;
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// Wiersz do importu z AVILOG
    /// </summary>
    public class ImportAvilogRow : INotifyPropertyChanged
    {
        private int? _mappedKierowcaGID;
        private int? _mappedHodowcaGID;

        public int Lp { get; set; }

        // Dane z AVILOG
        public string AvilogKierowca { get; set; }
        public string AvilogHodowca { get; set; }
        public string AvilogAdres { get; set; }

        // Mapowane GID
        public int? MappedKierowcaGID
        {
            get => _mappedKierowcaGID;
            set
            {
                _mappedKierowcaGID = value;
                OnPropertyChanged(nameof(MappedKierowcaGID));
                OnPropertyChanged(nameof(StatusMapowania));
            }
        }

        public int? MappedHodowcaGID
        {
            get => _mappedHodowcaGID;
            set
            {
                _mappedHodowcaGID = value;
                OnPropertyChanged(nameof(MappedHodowcaGID));
                OnPropertyChanged(nameof(StatusMapowania));
            }
        }

        // Dane transportowe
        public string Ciagnik { get; set; }
        public string Naczepa { get; set; }
        public int Sztuki { get; set; }
        public decimal WagaDek { get; set; }
        public DateTime? WyjazdZaklad { get; set; }
        public TimeSpan? PoczatekZaladunku { get; set; }
        public DateTime? PowrotZaklad { get; set; }
        public string Obserwacje { get; set; }

        // Oryginalne dane z parsera
        public AvilogTransportRow OriginalData { get; set; }

        // Wyświetlanie godzin
        public string WyjazdDisplay => WyjazdZaklad?.ToString("HH:mm") ?? "-";
        public string ZaladunekDisplay => PoczatekZaladunku?.ToString(@"hh\:mm") ?? "-";
        public string PowrotDisplay => PowrotZaklad?.ToString("HH:mm") ?? "-";

        // Status mapowania
        public string StatusMapowania
        {
            get
            {
                if (MappedHodowcaGID.HasValue)
                    return "OK";
                else
                    return "BRAK";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Item kierowcy dla ComboBox
    /// </summary>
    public class KierowcaItem
    {
        public int? GID { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Item hodowcy dla ComboBox
    /// </summary>
    public class HodowcaItem
    {
        public int? GID { get; set; }
        public string ShortName { get; set; }
        public string FullName { get; set; }
        public string City { get; set; }
    }
}
