using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace Kalendarz1
{
    /// <summary>
    /// Okno importu danych z pliku Excel AVILOG do Matrycy Transport
    /// </summary>
    public partial class ImportExcelWindow : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private ObservableCollection<ImportExcelRow> importData;
        private AvilogExcelParser parser;

        // Listy dla ComboBox
        public List<KierowcaItemExcel> ListaKierowcow { get; set; }
        public List<HodowcaItemExcel> ListaHodowcow { get; set; }
        public List<PojazdItemExcel> ListaCiagnikow { get; set; }
        public List<PojazdItemExcel> ListaNaczep { get; set; }

        // Mapowania zapisane w bazie
        private Dictionary<string, string> savedMappings = new Dictionary<string, string>();

        // Wynik importu
        public List<ImportExcelRow> ImportedRows { get; private set; }
        public DateTime? ImportedDate { get; private set; }
        public bool ImportSuccess { get; private set; }

        public ImportExcelWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            parser = new AvilogExcelParser();
            importData = new ObservableCollection<ImportExcelRow>();

            DataContext = this;
            dataGridImport.ItemsSource = importData;

            LoadKierowcy();
            LoadHodowcy();
            LoadCiagniki();
            LoadNaczepy();
            LoadSavedMappings();
        }

        #region Ładowanie danych słownikowych

        private void LoadKierowcy()
        {
            ListaKierowcow = new List<KierowcaItemExcel>();
            ListaKierowcow.Add(new KierowcaItemExcel { GID = null, Name = "(nie wybrano)" });

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
                            ListaKierowcow.Add(new KierowcaItemExcel
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
            ListaHodowcow = new List<HodowcaItemExcel>();
            ListaHodowcow.Add(new HodowcaItemExcel { GID = null, ShortName = "(nie wybrano)" });

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
                            ListaHodowcow.Add(new HodowcaItemExcel
                            {
                                GID = reader["GID"]?.ToString() ?? "",
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

        private void LoadCiagniki()
        {
            ListaCiagnikow = new List<PojazdItemExcel>();
            ListaCiagnikow.Add(new PojazdItemExcel { ID = null });

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT DISTINCT ID FROM dbo.CarTrailer WHERE kind = '1' ORDER BY ID DESC";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ListaCiagnikow.Add(new PojazdItemExcel
                            {
                                ID = reader["ID"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania ciągników: {ex.Message}");
            }
        }

        private void LoadNaczepy()
        {
            ListaNaczep = new List<PojazdItemExcel>();
            ListaNaczep.Add(new PojazdItemExcel { ID = null });

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT DISTINCT ID FROM dbo.CarTrailer WHERE kind = '2' ORDER BY ID DESC";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ListaNaczep.Add(new PojazdItemExcel
                            {
                                ID = reader["ID"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania naczep: {ex.Message}");
            }
        }

        private void LoadSavedMappings()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Sprawdź czy tabela istnieje (używamy tej samej co dla PDF)
                    string checkTable = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AvilogHodowcyMapping')
                        CREATE TABLE dbo.AvilogHodowcyMapping (
                            ID INT IDENTITY(1,1) PRIMARY KEY,
                            AvilogNazwa NVARCHAR(200) NOT NULL,
                            AvilogAdres NVARCHAR(300) NULL,
                            MappedGID NVARCHAR(50) NOT NULL,
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
                            string mappedGid = reader["MappedGID"]?.ToString() ?? "";
                            if (!savedMappings.ContainsKey(avilogNazwa) && !string.IsNullOrEmpty(mappedGid))
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

        #region Wybór i parsowanie Excel

        private void BtnWybierzPlik_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Pliki Excel (*.xls;*.xlsx)|*.xls;*.xlsx|Wszystkie pliki (*.*)|*.*",
                Title = "Wybierz plik Excel z planem transportu AVILOG"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                lblNazwaPliku.Text = System.IO.Path.GetFileName(filePath);

                ParseAndLoadExcel(filePath);
            }
        }

        private void ParseAndLoadExcel(string filePath)
        {
            try
            {
                lblStatus.Text = "Parsowanie pliku Excel...";
                importData.Clear();

                var result = parser.ParseExcel(filePath);

                if (!result.Success)
                {
                    MessageBox.Show($"Błąd parsowania Excel:\n{result.ErrorMessage}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    lblStatus.Text = "Błąd parsowania pliku";
                    return;
                }

                if (result.Wiersze.Count == 0)
                {
                    MessageBox.Show("Nie znaleziono danych transportowych w pliku Excel.\n\n" +
                        "Upewnij się, że wybrany plik to plan transportu AVILOG.",
                        "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                    lblStatus.Text = "Brak danych w pliku";
                    return;
                }

                // Ustaw datę
                ImportedDate = result.DataUboju;
                lblDataUboju.Text = result.DataUboju?.ToString("dd.MM.yyyy (dddd)") ?? "Nie rozpoznano";

                // Dodaj do kolekcji i automatycznie mapuj
                foreach (var row in result.Wiersze)
                {
                    // Auto-mapowanie kierowcy
                    AutoMapKierowca(row);

                    // Auto-mapowanie hodowcy
                    AutoMapHodowca(row);

                    // Auto-mapowanie pojazdów
                    AutoMapCiagnik(row);
                    AutoMapNaczepa(row);

                    importData.Add(row);
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

        private void AutoMapKierowca(ImportExcelRow row)
        {
            if (string.IsNullOrWhiteSpace(row.KierowcaNazwa)) return;

            string searchName = row.KierowcaNazwa.ToUpper().Trim();

            // Szukaj dokładnego dopasowania
            var exactMatch = ListaKierowcow.FirstOrDefault(k =>
                k.Name != null && k.Name.ToUpper().Trim() == searchName);

            if (exactMatch != null && exactMatch.GID.HasValue)
            {
                row.MappedKierowcaGID = exactMatch.GID;
                return;
            }

            // Rozbij na części (imię, nazwisko)
            var nameParts = searchName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (nameParts.Length >= 2)
            {
                // Szukaj po każdym słowie
                foreach (var part in nameParts)
                {
                    if (part.Length < 3) continue;

                    var match = ListaKierowcow.FirstOrDefault(k =>
                        k.Name != null && k.Name.ToUpper().Contains(part));

                    if (match != null && match.GID.HasValue)
                    {
                        row.MappedKierowcaGID = match.GID;
                        return;
                    }
                }
            }
            else if (nameParts.Length == 1 && nameParts[0].Length >= 4)
            {
                var match = ListaKierowcow.FirstOrDefault(k =>
                    k.Name != null && k.Name.ToUpper().Contains(nameParts[0]));

                if (match != null && match.GID.HasValue)
                {
                    row.MappedKierowcaGID = match.GID;
                }
            }
        }

        private void AutoMapHodowca(ImportExcelRow row)
        {
            if (string.IsNullOrWhiteSpace(row.HodowcaNazwa)) return;

            string searchName = row.HodowcaNazwa.ToUpper().Trim();

            // Najpierw sprawdź zapisane mapowania
            if (savedMappings.ContainsKey(searchName))
            {
                row.MappedHodowcaGID = savedMappings[searchName];
                return;
            }

            // Szukaj dokładnego dopasowania
            var exactMatch = ListaHodowcow.FirstOrDefault(h =>
                h.ShortName != null && h.ShortName.ToUpper().Trim() == searchName);

            if (exactMatch != null && !string.IsNullOrEmpty(exactMatch.GID))
            {
                row.MappedHodowcaGID = exactMatch.GID;
                return;
            }

            // Szukaj po słowach
            var nameParts = searchName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Szukaj po pierwszym słowie (nazwisko)
            if (nameParts.Length >= 1 && nameParts[0].Length >= 4)
            {
                string firstPart = nameParts[0];

                var partialMatch = ListaHodowcow.FirstOrDefault(h =>
                    h.ShortName != null && h.ShortName.ToUpper().Contains(firstPart));

                if (partialMatch != null && !string.IsNullOrEmpty(partialMatch.GID))
                {
                    row.MappedHodowcaGID = partialMatch.GID;
                    return;
                }
            }

            // Jeśli są dwa słowa, szukaj po obu
            if (nameParts.Length >= 2)
            {
                foreach (var hodowca in ListaHodowcow.Where(h => !string.IsNullOrEmpty(h.GID)))
                {
                    string hodowcaNazwaUpper = hodowca.ShortName?.ToUpper() ?? "";

                    bool containsAll = nameParts.All(part =>
                        part.Length >= 3 && hodowcaNazwaUpper.Contains(part));

                    if (containsAll)
                    {
                        row.MappedHodowcaGID = hodowca.GID;
                        return;
                    }
                }
            }
        }

        private void AutoMapCiagnik(ImportExcelRow row)
        {
            if (string.IsNullOrWhiteSpace(row.Ciagnik)) return;

            string searchID = row.Ciagnik.ToUpper().Replace(" ", "").Trim();

            // Szukaj dopasowania ignorując spacje
            var match = ListaCiagnikow.FirstOrDefault(c =>
                c.ID != null && c.ID.ToUpper().Replace(" ", "").Trim() == searchID);

            if (match != null && !string.IsNullOrEmpty(match.ID))
            {
                row.MappedCiagnikID = match.ID;
                return;
            }

            // Częściowe dopasowanie
            var partialMatch = ListaCiagnikow.FirstOrDefault(c =>
                c.ID != null && (
                    c.ID.ToUpper().Replace(" ", "").Contains(searchID) ||
                    searchID.Contains(c.ID.ToUpper().Replace(" ", ""))
                ));

            if (partialMatch != null && !string.IsNullOrEmpty(partialMatch.ID))
            {
                row.MappedCiagnikID = partialMatch.ID;
            }
        }

        private void AutoMapNaczepa(ImportExcelRow row)
        {
            if (string.IsNullOrWhiteSpace(row.Naczepa)) return;

            string searchID = row.Naczepa.ToUpper().Replace(" ", "").Trim();

            var match = ListaNaczep.FirstOrDefault(n =>
                n.ID != null && n.ID.ToUpper().Replace(" ", "").Trim() == searchID);

            if (match != null && !string.IsNullOrEmpty(match.ID))
            {
                row.MappedNaczepaID = match.ID;
                return;
            }

            var partialMatch = ListaNaczep.FirstOrDefault(n =>
                n.ID != null && (
                    n.ID.ToUpper().Replace(" ", "").Contains(searchID) ||
                    searchID.Contains(n.ID.ToUpper().Replace(" ", ""))
                ));

            if (partialMatch != null && !string.IsNullOrEmpty(partialMatch.ID))
            {
                row.MappedNaczepaID = partialMatch.ID;
            }
        }

        private void BtnAutoMapuj_Click(object sender, RoutedEventArgs e)
        {
            int mapped = 0;
            foreach (var row in importData)
            {
                if (string.IsNullOrEmpty(row.MappedHodowcaGID))
                {
                    string avilogNazwa = row.HodowcaNazwa?.ToUpper().Trim() ?? "";

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

        private HodowcaItemExcel FindBestHodowcaMatch(string avilogNazwa)
        {
            if (string.IsNullOrWhiteSpace(avilogNazwa)) return null;

            var words = avilogNazwa.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return null;

            int bestScore = 0;
            HodowcaItemExcel bestMatch = null;

            foreach (var hodowca in ListaHodowcow.Where(h => !string.IsNullOrEmpty(h.GID)))
            {
                string hodowcaNazwa = hodowca.ShortName?.ToUpper() ?? "";
                int score = 0;

                foreach (var word in words)
                {
                    if (word.Length >= 3 && hodowcaNazwa.Contains(word))
                    {
                        score += word.Length;
                    }
                }

                if (score > bestScore && score >= 5)
                {
                    bestScore = score;
                    bestMatch = hodowca;
                }
            }

            return bestMatch;
        }

        private void UpdateNiezamapowaniCount()
        {
            int niezamapowani = importData.Count(r => string.IsNullOrEmpty(r.MappedHodowcaGID));
            lblNiezamapowani.Text = niezamapowani.ToString();

            dataGridImport.Items.Refresh();
        }

        #endregion

        #region Import

        private void BtnImportuj_Click(object sender, RoutedEventArgs e)
        {
            var niezamapowani = importData.Where(r => string.IsNullOrEmpty(r.MappedHodowcaGID)).ToList();
            if (niezamapowani.Any())
            {
                var result = MessageBox.Show(
                    $"Nie wszystkie hodowcy zostali zmapowani ({niezamapowani.Count} niezamapowanych).\n\n" +
                    "Niezamapowani:\n" +
                    string.Join("\n", niezamapowani.Take(5).Select(r => $"  - {r.HodowcaNazwa ?? "(brak nazwy)"}")) +
                    (niezamapowani.Count > 5 ? $"\n  ... i {niezamapowani.Count - 5} więcej" : "") +
                    "\n\nCzy chcesz kontynuować? Niezamapowane wiersze będą zaimportowane do ręcznego poprawienia.",
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

            ImportedRows = importData.ToList();
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

                    foreach (var row in importData.Where(r => !string.IsNullOrEmpty(r.MappedHodowcaGID)))
                    {
                        string avilogNazwa = row.HodowcaNazwa?.ToUpper().Trim() ?? "";
                        if (string.IsNullOrEmpty(avilogNazwa)) continue;

                        if (savedMappings.ContainsKey(avilogNazwa))
                        {
                            if (savedMappings[avilogNazwa] != row.MappedHodowcaGID)
                            {
                                string updateSql = @"UPDATE dbo.AvilogHodowcyMapping
                                                    SET MappedGID = @MappedGID
                                                    WHERE AvilogNazwa = @AvilogNazwa";
                                using (SqlCommand cmd = new SqlCommand(updateSql, conn))
                                {
                                    cmd.Parameters.AddWithValue("@AvilogNazwa", avilogNazwa);
                                    cmd.Parameters.AddWithValue("@MappedGID", row.MappedHodowcaGID);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }
                        else
                        {
                            string insertSql = @"INSERT INTO dbo.AvilogHodowcyMapping
                                                (AvilogNazwa, AvilogAdres, MappedGID, CreatedBy)
                                                VALUES (@AvilogNazwa, @AvilogAdres, @MappedGID, @CreatedBy)";
                            using (SqlCommand cmd = new SqlCommand(insertSql, conn))
                            {
                                cmd.Parameters.AddWithValue("@AvilogNazwa", avilogNazwa);
                                cmd.Parameters.AddWithValue("@AvilogAdres", row.AvilogAdres ?? "");
                                cmd.Parameters.AddWithValue("@MappedGID", row.MappedHodowcaGID);
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

    #region Klasy pomocnicze dla ComboBox

    /// <summary>
    /// Item kierowcy dla ComboBox (Excel)
    /// </summary>
    public class KierowcaItemExcel
    {
        public int? GID { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Item hodowcy dla ComboBox (Excel)
    /// </summary>
    public class HodowcaItemExcel
    {
        public string GID { get; set; }
        public string ShortName { get; set; }
        public string FullName { get; set; }
        public string City { get; set; }
    }

    /// <summary>
    /// Item pojazdu dla ComboBox (Excel)
    /// </summary>
    public class PojazdItemExcel
    {
        public string ID { get; set; }
    }

    #endregion
}
