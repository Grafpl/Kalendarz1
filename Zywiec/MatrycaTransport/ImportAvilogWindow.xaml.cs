using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
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
        public List<PojazdItem> ListaCiagnikow { get; set; }
        public List<PojazdItem> ListaNaczep { get; set; }

        // Mapowania z bazy
        private Dictionary<string, string> savedMappings = new Dictionary<string, string>();

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
            LoadCiagniki();
            LoadNaczepy();
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
            ListaCiagnikow = new List<PojazdItem>();
            ListaCiagnikow.Add(new PojazdItem { ID = null });

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
                            ListaCiagnikow.Add(new PojazdItem
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
            ListaNaczep = new List<PojazdItem>();
            ListaNaczep.Add(new PojazdItem { ID = null });

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
                            ListaNaczep.Add(new PojazdItem
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

                    // Sprawdź czy tabela istnieje
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
                    // Pokaż fragment tekstu dla diagnostyki
                    string preview = "";
                    if (!string.IsNullOrEmpty(result.DebugText))
                    {
                        preview = result.DebugText.Length > 1000
                            ? result.DebugText.Substring(0, 1000) + "..."
                            : result.DebugText;
                    }

                    string message = "Nie znaleziono danych transportowych w pliku PDF.\n\n" +
                        "Upewnij się, że wybrany plik to plan transportu AVILOG.\n\n" +
                        "Tekst zapisano do pliku avilog_debug_text.txt obok PDF.\n\n" +
                        $"Fragment tekstu z PDF:\n{preview}";

                    MessageBox.Show(message, "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                    // Auto-mapuj pojazdy (ciągnik i naczepa)
                    AutoMapCiagnik(importRow, row.Ciagnik);
                    AutoMapNaczepa(importRow, row.Naczepa);

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

            // Wyczyść numer telefonu z nazwy kierowcy
            string cleanName = Regex.Replace(kierowcaNazwa, @"[\d\s-]{9,}", "").Trim();
            string searchName = cleanName.ToUpper().Trim();

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
                // Szukaj po nazwisku (zwykle drugie słowo) - np. "Knapkiewicz Sylwester" -> "SYLWESTER" lub "KNAPKIEWICZ"
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
                // Tylko jedno słowo - szukaj częściowo
                var match = ListaKierowcow.FirstOrDefault(k =>
                    k.Name != null && k.Name.ToUpper().Contains(nameParts[0]));

                if (match != null && match.GID.HasValue)
                {
                    row.MappedKierowcaGID = match.GID;
                }
            }
        }

        private void AutoMapHodowca(ImportAvilogRow row, string hodowcaNazwa)
        {
            if (string.IsNullOrWhiteSpace(hodowcaNazwa)) return;

            // Weź tylko pierwszą linię (pogrubiona nazwa) - usuń adresy, kody pocztowe itp.
            string cleanName = hodowcaNazwa.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? hodowcaNazwa;

            // Usuń numery telefonów, współrzędne GPS, kody pocztowe
            cleanName = Regex.Replace(cleanName, @"Tel\.?\s*:?\s*[\d\s-]+", "", RegexOptions.IgnoreCase);
            cleanName = Regex.Replace(cleanName, @"\d{2}[.,]\d+", ""); // GPS
            cleanName = Regex.Replace(cleanName, @"\d{2}-\d{3}", ""); // kod pocztowy
            cleanName = cleanName.Trim();

            string searchName = cleanName.ToUpper().Trim();

            // Najpierw sprawdź zapisane mapowania
            if (savedMappings.ContainsKey(searchName))
            {
                row.MappedHodowcaGID = savedMappings[searchName];
                return;
            }

            // Szukaj dokładnego dopasowania w bazie
            var exactMatch = ListaHodowcow.FirstOrDefault(h =>
                h.ShortName != null && h.ShortName.ToUpper().Trim() == searchName);

            if (exactMatch != null && !string.IsNullOrEmpty(exactMatch.GID))
            {
                row.MappedHodowcaGID = exactMatch.GID;
                return;
            }

            // Szukaj po słowach - np. "KIEŁBASA MARCIN" powinno znaleźć "Kiełbasa Marcin" lub "KIEŁBASA"
            var nameParts = searchName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Spróbuj znaleźć po pierwszym słowie (nazwisko)
            if (nameParts.Length >= 1 && nameParts[0].Length >= 4)
            {
                string firstPart = nameParts[0];

                // Szukaj hodowcy który zawiera to słowo
                var partialMatch = ListaHodowcow.FirstOrDefault(h =>
                    h.ShortName != null && h.ShortName.ToUpper().Contains(firstPart));

                if (partialMatch != null && !string.IsNullOrEmpty(partialMatch.GID))
                {
                    row.MappedHodowcaGID = partialMatch.GID;
                    return;
                }
            }

            // Jeśli są dwa słowa, spróbuj znaleźć po obu
            if (nameParts.Length >= 2)
            {
                foreach (var hodowca in ListaHodowcow.Where(h => !string.IsNullOrEmpty(h.GID)))
                {
                    string hodowcaNazwaUpper = hodowca.ShortName?.ToUpper() ?? "";

                    // Sprawdź czy zawiera oba słowa
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

        private void AutoMapCiagnik(ImportAvilogRow row, string ciagnikAvilog)
        {
            if (string.IsNullOrWhiteSpace(ciagnikAvilog)) return;

            string searchID = ciagnikAvilog.ToUpper().Trim();

            // Szukaj dokładnego dopasowania
            var exactMatch = ListaCiagnikow.FirstOrDefault(c =>
                c.ID != null && c.ID.ToUpper().Trim() == searchID);

            if (exactMatch != null && !string.IsNullOrEmpty(exactMatch.ID))
            {
                row.MappedCiagnikID = exactMatch.ID;
                return;
            }

            // Szukaj częściowego dopasowania (np. "WOT51407" zawiera się w "WOT51407")
            var partialMatch = ListaCiagnikow.FirstOrDefault(c =>
                c.ID != null && (c.ID.ToUpper().Contains(searchID) || searchID.Contains(c.ID.ToUpper())));

            if (partialMatch != null && !string.IsNullOrEmpty(partialMatch.ID))
            {
                row.MappedCiagnikID = partialMatch.ID;
            }
        }

        private void AutoMapNaczepa(ImportAvilogRow row, string naczepaAvilog)
        {
            if (string.IsNullOrWhiteSpace(naczepaAvilog)) return;

            string searchID = naczepaAvilog.ToUpper().Trim();

            // Szukaj dokładnego dopasowania
            var exactMatch = ListaNaczep.FirstOrDefault(n =>
                n.ID != null && n.ID.ToUpper().Trim() == searchID);

            if (exactMatch != null && !string.IsNullOrEmpty(exactMatch.ID))
            {
                row.MappedNaczepaID = exactMatch.ID;
                return;
            }

            // Szukaj częściowego dopasowania
            var partialMatch = ListaNaczep.FirstOrDefault(n =>
                n.ID != null && (n.ID.ToUpper().Contains(searchID) || searchID.Contains(n.ID.ToUpper())));

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

            foreach (var hodowca in ListaHodowcow.Where(h => !string.IsNullOrEmpty(h.GID)))
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
            int niezamapowani = importData.Count(r => string.IsNullOrEmpty(r.MappedHodowcaGID));
            lblNiezamapowani.Text = niezamapowani.ToString();

            // Odśwież widok żeby zaktualizować statusy
            dataGridImport.Items.Refresh();
        }

        #endregion

        #region Import

        private void BtnImportuj_Click(object sender, RoutedEventArgs e)
        {
            // Sprawdź czy są niezamapowani hodowcy
            var niezamapowani = importData.Where(r => string.IsNullOrEmpty(r.MappedHodowcaGID)).ToList();
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
            ImportedRows = importData.Where(r => !string.IsNullOrEmpty(r.MappedHodowcaGID)).ToList();
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
                        string avilogNazwa = row.AvilogHodowca?.ToUpper().Trim() ?? "";
                        if (string.IsNullOrEmpty(avilogNazwa)) continue;

                        // Sprawdź czy już istnieje
                        if (savedMappings.ContainsKey(avilogNazwa))
                        {
                            // Aktualizuj jeśli się zmieniło
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
                            // Dodaj nowe mapowanie
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

    /// <summary>
    /// Wiersz do importu z AVILOG
    /// </summary>
    public class ImportAvilogRow : INotifyPropertyChanged
    {
        private int? _mappedKierowcaGID;
        private string _mappedHodowcaGID;
        private string _mappedCiagnikID;
        private string _mappedNaczepaID;

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

        public string MappedHodowcaGID
        {
            get => _mappedHodowcaGID;
            set
            {
                _mappedHodowcaGID = value;
                OnPropertyChanged(nameof(MappedHodowcaGID));
                OnPropertyChanged(nameof(StatusMapowania));
            }
        }

        public string MappedCiagnikID
        {
            get => _mappedCiagnikID;
            set
            {
                _mappedCiagnikID = value;
                OnPropertyChanged(nameof(MappedCiagnikID));
            }
        }

        public string MappedNaczepaID
        {
            get => _mappedNaczepaID;
            set
            {
                _mappedNaczepaID = value;
                OnPropertyChanged(nameof(MappedNaczepaID));
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
                if (!string.IsNullOrEmpty(MappedHodowcaGID))
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
        public string GID { get; set; }
        public string ShortName { get; set; }
        public string FullName { get; set; }
        public string City { get; set; }
    }

    /// <summary>
    /// Item pojazdu (ciągnik/naczepa) dla ComboBox
    /// </summary>
    public class PojazdItem
    {
        public string ID { get; set; }
    }
}
