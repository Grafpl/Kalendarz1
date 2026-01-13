using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// Wymagane pakiety NuGet:
// - ClosedXML (dla xlsx/xlsm)
// - ExcelDataReader + ExcelDataReader.DataSet (dla xls)
// Dla plików ODS można użyć biblioteki trzeciej strony lub konwersji

using ClosedXML.Excel;

namespace Kalendarz1.Zywiec.WidokSpecyfikacji
{
    /// <summary>
    /// Kreator importu specyfikacji drobiu z plików Excel/LibreOffice
    /// </summary>
    public partial class ImportSpecyfikacjeWizard : Window, INotifyPropertyChanged
    {
        #region Pola i właściwości

        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        
        private int _currentStep = 1;
        private string _selectedFilePath;
        private string _selectedSheetName;
        private DateTime _dataUboju;
        private XLWorkbook _workbook;
        
        // Dane z Excela
        private ObservableCollection<ImportRow> _importData = new ObservableCollection<ImportRow>();
        private ObservableCollection<SupplierMapping> _supplierMappings = new ObservableCollection<SupplierMapping>();
        
        // Lista dostawców z bazy
        public List<DostawcaItem> ListaDostawcow { get; set; } = new List<DostawcaItem>();
        
        // Lista pozycji z harmonogramu dostaw
        public List<HarmonogramItem> ListaHarmonogram { get; set; } = new List<HarmonogramItem>();
        
        // Callback do odświeżenia po imporcie
        public Action OnImportCompleted { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Konstruktor

        public ImportSpecyfikacjeWizard()
        {
            InitializeComponent();
            DataContext = this;
            LoadDostawcyFromDatabase();
        }

        public ImportSpecyfikacjeWizard(string connectionString) : this()
        {
            this.connectionString = connectionString;
        }

        #endregion

        #region Ładowanie dostawców z bazy

        private async void LoadDostawcyFromDatabase()
        {
            try
            {
                await Task.Run(() =>
                {
                    var dostawcy = new List<DostawcaItem>();
                    
                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string query = @"
                            SELECT LTRIM(RTRIM(ID)) as ID, 
                                   LTRIM(RTRIM(Name)) as Name,
                                   ISNULL(AnimNo, '') as AnimNo
                            FROM dbo.Dostawcy 
                            WHERE Halt = 0 AND IsDeliverer = 1
                            ORDER BY Name";
                        
                        using (var cmd = new SqlCommand(query, connection))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                dostawcy.Add(new DostawcaItem
                                {
                                    ID = reader["ID"].ToString(),
                                    Nazwa = reader["Name"].ToString(),
                                    AnimNo = reader["AnimNo"].ToString()
                                });
                            }
                        }
                    }

                    Dispatcher.Invoke(() =>
                    {
                        ListaDostawcow = dostawcy;
                        OnPropertyChanged(nameof(ListaDostawcow));
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania dostawców z bazy:\n{ex.Message}", 
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Ładuje listę pozycji z harmonogramu dostaw dla wybranej daty
        /// </summary>
        private async void LoadHarmonogramDostawy(DateTime dataUboju)
        {
            try
            {
                await Task.Run(() =>
                {
                    var harmonogram = new List<HarmonogramItem>();
                    
                    // Dodaj pustą opcję na początku
                    harmonogram.Add(new HarmonogramItem
                    {
                        LP = 0,
                        Dostawca = "",
                        DisplayText = "(brak - nie przypisuj LP)"
                    });
                    
                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string query = @"
                            SELECT 
                                LP,
                                ISNULL(Dostawca, '') as Dostawca,
                                ISNULL(SztukiDek, 0) as SztukiDek,
                                ISNULL(Cena, 0) as Cena,
                                ISNULL(Ubytek, 0) as Ubytek,
                                ISNULL(typCeny, '') as TypCeny
                            FROM [LibraNet].[dbo].[HarmonogramDostaw]
                            WHERE DataOdbioru = @DataUboju
                            AND Bufor IN ('Potwierdzony', 'Potwierdzone')
                            ORDER BY LP";
                        
                        using (var cmd = new SqlCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@DataUboju", dataUboju.Date);
                            
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int lp = reader["LP"] != DBNull.Value ? Convert.ToInt32(reader["LP"]) : 0;
                                    string dostawca = reader["Dostawca"]?.ToString()?.Trim() ?? "";
                                    int sztuki = reader["SztukiDek"] != DBNull.Value ? Convert.ToInt32(reader["SztukiDek"]) : 0;
                                    decimal cena = reader["Cena"] != DBNull.Value ? Convert.ToDecimal(reader["Cena"]) : 0;
                                    decimal ubytek = reader["Ubytek"] != DBNull.Value ? Convert.ToDecimal(reader["Ubytek"]) : 0;
                                    string typCeny = reader["TypCeny"]?.ToString()?.Trim() ?? "";

                                    harmonogram.Add(new HarmonogramItem
                                    {
                                        LP = lp,
                                        Dostawca = dostawca,
                                        SztukiDek = sztuki,
                                        Cena = cena,
                                        Ubytek = ubytek,
                                        TypCeny = typCeny,
                                        DisplayText = $"LP:{lp} - {dostawca} (Szt:{sztuki})"
                                    });
                                }
                            }
                        }
                    }

                    Dispatcher.Invoke(() =>
                    {
                        ListaHarmonogram = harmonogram;
                        OnPropertyChanged(nameof(ListaHarmonogram));
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania harmonogramu: {ex.Message}");
            }
        }

        #endregion

        #region Obsługa kroków

        private void UpdateStepIndicators()
        {
            // Reset wszystkich
            step1Border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
            step2Border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
            step3Border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
            step4Border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));

            step1Text.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#757575"));
            step2Text.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#757575"));
            step3Text.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#757575"));
            step4Text.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#757575"));

            step1Text.FontWeight = FontWeights.Normal;
            step2Text.FontWeight = FontWeights.Normal;
            step3Text.FontWeight = FontWeights.Normal;
            step4Text.FontWeight = FontWeights.Normal;

            // Aktywny krok
            var activeBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1976D2"));
            var completedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#388E3C"));

            for (int i = 1; i <= 4; i++)
            {
                var border = (Border)FindName($"step{i}Border");
                var text = (TextBlock)FindName($"step{i}Text");

                if (i < _currentStep)
                {
                    border.Background = completedBrush;
                    ((TextBlock)border.Child).Foreground = Brushes.White;
                    text.Foreground = completedBrush;
                }
                else if (i == _currentStep)
                {
                    border.Background = activeBrush;
                    ((TextBlock)border.Child).Foreground = Brushes.White;
                    text.Foreground = activeBrush;
                    text.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    ((TextBlock)border.Child).Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#757575"));
                }
            }

            // Widoczność paneli
            step1Panel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            step2Panel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            step3Panel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
            step4Panel.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;

            // Przyciski
            btnPrevious.Visibility = _currentStep > 1 && _currentStep < 4 ? Visibility.Visible : Visibility.Collapsed;
            btnNext.Visibility = _currentStep < 3 ? Visibility.Visible : Visibility.Collapsed;
            btnImport.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
            btnClose.Visibility = postImportPanel.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void GoToStep(int step)
        {
            _currentStep = step;
            UpdateStepIndicators();

            // Akcje przy przejściu do kroku
            switch (step)
            {
                case 2:
                    // Przygotuj dane do podglądu
                    dataGridPreview.ItemsSource = _importData;
                    lblDataUboju.Text = _dataUboju.ToString("dd.MM.yyyy");
                    lblRowCount.Text = _importData.Count.ToString();
                    lblSupplierCount.Text = _importData.Select(x => x.DostawcaExcel).Distinct().Count().ToString();
                    break;

                case 3:
                    // Załaduj harmonogram dostaw dla tej daty
                    LoadHarmonogramDostawy(_dataUboju);
                    // Przygotuj mapowanie dostawców
                    PrepareSupplierMapping();
                    dataGridMapping.ItemsSource = _supplierMappings;
                    break;

                case 4:
                    // Podsumowanie
                    lblSummaryDate.Text = _dataUboju.ToString("dd.MM.yyyy");
                    lblSummaryRows.Text = _importData.Count.ToString();
                    lblSummarySuppliers.Text = _supplierMappings.Count.ToString();
                    lblSummaryTotal.Text = _importData.Sum(x => x.SztukiDek).ToString("N0");
                    break;
            }
        }

        #endregion

        #region Krok 1: Wybór pliku

        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Wybierz plik ze specyfikacjami",
                Filter = "Pliki Excel (*.xlsx;*.xlsm;*.xls)|*.xlsx;*.xlsm;*.xls|Pliki LibreOffice (*.ods)|*.ods|Wszystkie pliki (*.*)|*.*",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedFilePath = dialog.FileName;
                lblSelectedFile.Text = _selectedFilePath;
                selectedFilePanel.Visibility = Visibility.Visible;

                LoadWorkbook();
            }
        }

        private void LoadWorkbook()
        {
            try
            {
                // Zamknij poprzedni workbook jeśli otwarty
                _workbook?.Dispose();

                string extension = Path.GetExtension(_selectedFilePath).ToLower();

                if (extension == ".ods")
                {
                    MessageBox.Show("Pliki ODS nie są jeszcze obsługiwane.\nProszę zapisać plik jako .xlsx w LibreOffice Calc.",
                        "Format nieobsługiwany", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _workbook = new XLWorkbook(_selectedFilePath);

                // Pokaż listę arkuszy
                cmbSheets.Items.Clear();
                foreach (var sheet in _workbook.Worksheets)
                {
                    cmbSheets.Items.Add(sheet.Name);
                }

                // Domyślnie wybierz "Wpisywałka" jeśli istnieje
                if (cmbSheets.Items.Contains("Wpisywałka"))
                {
                    cmbSheets.SelectedItem = "Wpisywałka";
                }
                else if (cmbSheets.Items.Count > 0)
                {
                    cmbSheets.SelectedIndex = 0;
                }

                sheetSelectionPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania pliku:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbSheets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSheets.SelectedItem == null) return;

            _selectedSheetName = cmbSheets.SelectedItem.ToString();

            try
            {
                var worksheet = _workbook.Worksheet(_selectedSheetName);
                int rowCount = CountDataRows(worksheet);
                
                lblSheetInfo.Text = $"Arkusz zawiera {rowCount} wierszy z danymi";
                
                // Spróbuj załadować dane
                if (LoadDataFromSheet(worksheet))
                {
                    btnNext.IsEnabled = true;
                    lblSheetInfo.Text += $" | Data uboju: {_dataUboju:dd.MM.yyyy}";
                }
                else
                {
                    btnNext.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                lblSheetInfo.Text = $"Błąd odczytu arkusza: {ex.Message}";
                btnNext.IsEnabled = false;
            }
        }

        private int CountDataRows(IXLWorksheet worksheet)
        {
            int count = 0;
            for (int row = 3; row <= 50; row++)
            {
                var cellB = worksheet.Cell(row, 2).Value; // Kolumna B - nr specyfikacji
                if (!cellB.IsBlank && cellB.ToString() != "")
                {
                    // Sprawdź czy to nie jest wiersz z datą (A = "Data")
                    var cellA = worksheet.Cell(row, 1).Value;
                    if (cellA.ToString()?.ToLower() != "data")
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private bool LoadDataFromSheet(IXLWorksheet worksheet)
        {
            _importData.Clear();
            _dataUboju = DateTime.Today;

            try
            {
                // Znajdź datę uboju (B21 lub wiersz gdzie A = "Data")
                for (int row = 3; row <= 30; row++)
                {
                    var cellA = worksheet.Cell(row, 1).Value;
                    if (cellA.ToString()?.ToLower() == "data")
                    {
                        var cellB = worksheet.Cell(row, 2).Value;
                        if (cellB.IsDateTime)
                        {
                            _dataUboju = cellB.GetDateTime();
                        }
                        else if (DateTime.TryParse(cellB.ToString(), out DateTime parsed))
                        {
                            _dataUboju = parsed;
                        }
                        break;
                    }
                }

                // Fallback - sprawdź B21
                if (_dataUboju == DateTime.Today)
                {
                    var cellB21 = worksheet.Cell(21, 2).Value;
                    if (cellB21.IsDateTime)
                    {
                        _dataUboju = cellB21.GetDateTime();
                    }
                }

                // Wczytaj dane wierszy
                for (int row = 3; row <= 50; row++)
                {
                    var cellA = worksheet.Cell(row, 1).Value;
                    var cellB = worksheet.Cell(row, 2).Value;
                    var cellC = worksheet.Cell(row, 3).Value;

                    // Pomiń jeśli to wiersz daty lub pusty
                    if (cellA.ToString()?.ToLower() == "data") continue;
                    if (cellB.IsBlank || string.IsNullOrWhiteSpace(cellB.ToString())) continue;
                    if (cellC.IsBlank || string.IsNullOrWhiteSpace(cellC.ToString())) continue;

                    var importRow = new ImportRow
                    {
                        NrAuta = GetIntValue(worksheet.Cell(row, 1).Value),           // A
                        NrSpecyfikacji = GetIntValue(worksheet.Cell(row, 2).Value),   // B
                        DostawcaExcel = worksheet.Cell(row, 3).Value.ToString()?.Trim(), // C
                        SztukiDek = GetIntValue(worksheet.Cell(row, 4).Value),        // D
                        Padle = GetIntValue(worksheet.Cell(row, 5).Value),            // E
                        CH = GetIntValue(worksheet.Cell(row, 6).Value),               // F
                        NW = GetIntValue(worksheet.Cell(row, 7).Value),               // G
                        ZM = GetIntValue(worksheet.Cell(row, 8).Value),               // H
                        BruttoHodowcy = GetDecimalValue(worksheet.Cell(row, 9).Value),  // I
                        TaraHodowcy = GetDecimalValue(worksheet.Cell(row, 10).Value),   // J
                        BruttoUbojni = GetDecimalValue(worksheet.Cell(row, 11).Value),  // K
                        TaraUbojni = GetDecimalValue(worksheet.Cell(row, 12).Value),    // L
                        LUMEL = GetIntValue(worksheet.Cell(row, 13).Value),           // M
                        SztukiProdukcja = GetIntValue(worksheet.Cell(row, 14).Value), // N
                        KilogramyProdukcja = GetDecimalValue(worksheet.Cell(row, 15).Value), // O
                        TypCeny = worksheet.Cell(row, 16).Value.ToString()?.Trim(),   // P - typ ceny główny
                        Cena = 0,
                        Dodatek = GetDecimalValue(worksheet.Cell(row, 21).Value),     // U - dodatek do ceny
                        PiK = worksheet.Cell(row, 22).Value.ToString()?.ToLower() != "tak", // V - PiK (Tak = odznaczony, inaczej = zaznaczony)
                        Ubytek = GetDecimalValue(worksheet.Cell(row, 23).Value),      // W - ubytek
                        DataUboju = _dataUboju
                    };

                    // Oblicz cenę
                    // P3 = typ główny, Q3 = cena 1, R3 = typ 1 (łączona), S3 = typ 2 (łączona), T3 = cena 2
                    decimal cena1 = GetDecimalValue(worksheet.Cell(row, 17).Value); // Q - cena 1
                    string typ1 = worksheet.Cell(row, 18).Value.ToString()?.Trim(); // R - typ 1 (łączona)
                    string typ2 = worksheet.Cell(row, 19).Value.ToString()?.Trim(); // S - typ 2 (łączona)
                    decimal cena2 = GetDecimalValue(worksheet.Cell(row, 20).Value); // T - cena 2

                    // Jeśli jest cena łączona (typ2 ma wartość i cena2 > 0)
                    if (!string.IsNullOrWhiteSpace(typ2) && cena2 > 0)
                    {
                        importRow.Cena = (cena1 + cena2) / 2; // Średnia dwóch cen
                        importRow.TypCeny = "łączona";
                    }
                    else
                    {
                        importRow.Cena = cena1;
                    }

                    // Normalizuj typ ceny
                    if (!string.IsNullOrWhiteSpace(importRow.TypCeny))
                    {
                        importRow.TypCeny = NormalizeTypCeny(importRow.TypCeny);
                    }

                    _importData.Add(importRow);
                }

                return _importData.Count > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd parsowania danych:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private string NormalizeTypCeny(string typCeny)
        {
            if (string.IsNullOrWhiteSpace(typCeny)) return "wolnyrynek";
            
            typCeny = typCeny.ToLower().Trim();
            
            if (typCeny.Contains("wolno") || typCeny.Contains("rynk"))
                return "wolnyrynek";
            if (typCeny.Contains("rolni"))
                return "rolnicza";
            if (typCeny.Contains("łącz") || typCeny.Contains("lacz"))
                return "łączona";
            if (typCeny.Contains("mini") || typCeny.Contains("minist"))
                return "ministerialna";
                
            return typCeny;
        }

        private int GetIntValue(XLCellValue value)
        {
            if (value.IsBlank) return 0;
            if (value.IsNumber) return (int)value.GetNumber();
            if (int.TryParse(value.ToString(), out int result)) return result;
            return 0;
        }

        private decimal GetDecimalValue(XLCellValue value)
        {
            if (value.IsBlank) return 0;
            if (value.IsNumber) return (decimal)value.GetNumber();
            if (decimal.TryParse(value.ToString()?.Replace(",", "."), 
                System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, 
                out decimal result)) return result;
            return 0;
        }

        #endregion

        #region Krok 3: Mapowanie dostawców

        private void PrepareSupplierMapping()
        {
            _supplierMappings.Clear();

            var groupedBySupplier = _importData
                .GroupBy(x => x.DostawcaExcel)
                .OrderBy(g => g.Key);

            foreach (var group in groupedBySupplier)
            {
                var mapping = new SupplierMapping
                {
                    DostawcaExcel = group.Key,
                    IloscWierszy = group.Count()
                };

                // Spróbuj automatycznie dopasować dostawcę
                var match = FindBestMatch(group.Key);
                if (match != null)
                {
                    mapping.WybranyDostawca = match;
                }

                // Spróbuj automatycznie dopasować LP z harmonogramu
                var harmonogramMatch = FindBestHarmonogramMatch(group.Key);
                if (harmonogramMatch != null)
                {
                    mapping.WybranyHarmonogram = harmonogramMatch;
                }

                _supplierMappings.Add(mapping);
            }
        }

        private HarmonogramItem FindBestHarmonogramMatch(string excelName)
        {
            if (string.IsNullOrWhiteSpace(excelName)) return null;
            if (ListaHarmonogram == null || ListaHarmonogram.Count <= 1) return null; // Tylko pusta opcja

            var searchName = excelName.ToLower().Trim();

            // 1. Dokładne dopasowanie
            var exact = ListaHarmonogram.FirstOrDefault(h => 
                h.LP > 0 && h.Dostawca?.ToLower().Trim() == searchName);
            if (exact != null) return exact;

            // 2. Dopasowanie po części nazwy (bez prefiksu jak "De Heus – ")
            var parts = searchName.Split(new[] { '–', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                var lastName = parts.Last().Trim();
                var partial = ListaHarmonogram.FirstOrDefault(h =>
                    h.LP > 0 && h.Dostawca?.ToLower().Contains(lastName) == true);
                if (partial != null) return partial;
            }

            // 3. Dopasowanie po zawartości
            var contains = ListaHarmonogram.FirstOrDefault(h =>
                h.LP > 0 && (h.Dostawca?.ToLower().Contains(searchName) == true ||
                searchName.Contains(h.Dostawca?.ToLower() ?? "")));
            if (contains != null) return contains;

            return null;
        }

        private DostawcaItem FindBestMatch(string excelName)
        {
            if (string.IsNullOrWhiteSpace(excelName)) return null;

            var searchName = excelName.ToLower().Trim();

            // 1. Dokładne dopasowanie
            var exact = ListaDostawcow.FirstOrDefault(d => 
                d.Nazwa?.ToLower().Trim() == searchName);
            if (exact != null) return exact;

            // 2. Dopasowanie po części nazwy (bez prefiksu jak "De Heus – ")
            var parts = searchName.Split(new[] { '–', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                var lastName = parts.Last().Trim();
                var partial = ListaDostawcow.FirstOrDefault(d =>
                    d.Nazwa?.ToLower().Contains(lastName) == true);
                if (partial != null) return partial;
            }

            // 3. Dopasowanie po zawartości
            var contains = ListaDostawcow.FirstOrDefault(d =>
                d.Nazwa?.ToLower().Contains(searchName) == true ||
                searchName.Contains(d.Nazwa?.ToLower() ?? ""));
            if (contains != null) return contains;

            return null;
        }

        #endregion

        #region Krok 4: Import

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            // Sprawdź czy wszystkie mapowania są uzupełnione
            var unmapped = _supplierMappings.Where(m => m.WybranyDostawca == null).ToList();
            if (unmapped.Any())
            {
                var result = MessageBox.Show(
                    $"Nie wszystkie dostawcy są zmapowani ({unmapped.Count} bez przypisania).\n\n" +
                    "Czy chcesz kontynuować? Wiersze bez mapowania zostaną pominięte.",
                    "Brakujące mapowania",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;
            }

            GoToStep(4);
            
            // Pokaż panel importowania
            preImportPanel.Visibility = Visibility.Collapsed;
            importingPanel.Visibility = Visibility.Visible;
            btnImport.Visibility = Visibility.Collapsed;
            btnPrevious.Visibility = Visibility.Collapsed;
            btnCancel.IsEnabled = false;

            // WAŻNE: Odczytaj wartość checkboxa PRZED Task.Run (dostęp do UI tylko z głównego wątku)
            bool overwriteExisting = chkOverwrite.IsChecked == true;
            DateTime dataUboju = _dataUboju;
            var importData = _importData.ToList(); // Kopia danych
            var supplierMappings = _supplierMappings.ToList(); // Kopia mapowań

            try
            {
                int imported = 0;
                int skipped = 0;
                int errors = 0;
                string lastError = ""; // Przechowuje pierwszy błąd do wyświetlenia

                await Task.Run(() =>
                {
                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();

                        // Sprawdź czy istnieją dane z tej daty
                        if (overwriteExisting)
                        {
                            Dispatcher.Invoke(() => lblProgress.Text = "Usuwanie istniejących danych...");
                            
                            string deleteQuery = "DELETE FROM dbo.FarmerCalc WHERE CalcDate = @Date";
                            using (var deleteCmd = new SqlCommand(deleteQuery, connection))
                            {
                                deleteCmd.Parameters.AddWithValue("@Date", dataUboju.Date);
                                deleteCmd.ExecuteNonQuery();
                            }
                        }

                        // Importuj dane
                        int total = importData.Count;
                        int current = 0;

                        foreach (var row in importData)
                        {
                            current++;
                            Dispatcher.Invoke(() => 
                            {
                                lblProgress.Text = $"Importowanie {current}/{total}...";
                                progressBar.IsIndeterminate = false;
                                progressBar.Maximum = total;
                                progressBar.Value = current;
                            });

                            // Znajdź mapowanie dla tego dostawcy
                            var mapping = supplierMappings.FirstOrDefault(m => m.DostawcaExcel == row.DostawcaExcel);
                            if (mapping?.WybranyDostawca == null)
                            {
                                skipped++;
                                continue;
                            }

                            // Pobierz LP z harmonogramu (jeśli wybrano)
                            int lpDostawy = mapping.WybranyHarmonogram?.LP ?? 0;

                            try
                            {
                                InsertSpecyfikacja(connection, row, mapping.WybranyDostawca, lpDostawy);
                                imported++;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Błąd importu wiersza {row.NrSpecyfikacji}: {ex.Message}");
                                // Zapisz pierwszy błąd do wyświetlenia
                                if (string.IsNullOrEmpty(lastError))
                                {
                                    lastError = $"Wiersz {row.NrSpecyfikacji} ({row.DostawcaExcel}):\n{ex.Message}";
                                }
                                errors++;
                            }
                        }
                    }
                });

                // Pokaż wynik
                importingPanel.Visibility = Visibility.Collapsed;
                postImportPanel.Visibility = Visibility.Visible;

                if (errors == 0 && skipped == 0)
                {
                    lblResultIcon.Text = "✅";
                    lblResultTitle.Text = "Import zakończony pomyślnie!";
                    lblResultTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#388E3C"));
                    lblResultDetails.Text = $"Zaimportowano {imported} specyfikacji do bazy danych.";
                }
                else if (imported > 0)
                {
                    lblResultIcon.Text = "⚠️";
                    lblResultTitle.Text = "Import zakończony z uwagami";
                    lblResultTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F57C00"));
                    string details = $"Zaimportowano: {imported}\nPominięto (brak mapowania): {skipped}\nBłędy: {errors}";
                    if (!string.IsNullOrEmpty(lastError))
                    {
                        details += $"\n\nPierwszy błąd:\n{lastError}";
                    }
                    lblResultDetails.Text = details;
                }
                else
                {
                    lblResultIcon.Text = "❌";
                    lblResultTitle.Text = "Import nie powiódł się";
                    lblResultTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F"));
                    string details = $"Zaimportowano: 0\nPominięto: {skipped}\nBłędy: {errors}";
                    if (!string.IsNullOrEmpty(lastError))
                    {
                        details += $"\n\nBłąd:\n{lastError}";
                    }
                    lblResultDetails.Text = details;
                }

                btnClose.Visibility = Visibility.Visible;
                
                // Wywołaj callback
                OnImportCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                importingPanel.Visibility = Visibility.Collapsed;
                postImportPanel.Visibility = Visibility.Visible;

                lblResultIcon.Text = "❌";
                lblResultTitle.Text = "Błąd importu";
                lblResultTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F"));
                lblResultDetails.Text = ex.Message;

                btnClose.Visibility = Visibility.Visible;
            }
            finally
            {
                btnCancel.IsEnabled = true;
            }
        }

        private void InsertSpecyfikacja(SqlConnection connection, ImportRow row, DostawcaItem dostawca, int lpDostawy)
        {
            // Oblicz netto wagi
            decimal nettoHodowcy = row.BruttoHodowcy - row.TaraHodowcy;
            decimal nettoUbojni = row.BruttoUbojni - row.TaraUbojni;

            // Mapowanie PriceTypeID
            int priceTypeId = GetPriceTypeId(row.TypCeny);

            // Pobierz następne ID
            int nextId = 1;
            using (var idCmd = new SqlCommand("SELECT ISNULL(MAX(ID), 0) + 1 FROM dbo.FarmerCalc", connection))
            {
                nextId = (int)idCmd.ExecuteScalar();
            }

            string query = @"
                INSERT INTO dbo.FarmerCalc 
                (ID, Number, CalcDate, CarLp, CustomerGID, CustomerRealGID, DeclI1, DeclI2, DeclI3, DeclI4, DeclI5, 
                 LumQnt, ProdQnt, ProdWgt, Price, Addition, Loss, IncDeadConf, 
                 NettoWeight, PriceTypeID, LpDostawy,
                 FullWeight, EmptyWeight, FullFarmWeight, EmptyFarmWeight, NettoFarmWeight)
                VALUES 
                (@ID, @Number, @CalcDate, @CarLp, @CustomerGID, @CustomerRealGID, @DeclI1, @DeclI2, @DeclI3, @DeclI4, @DeclI5, 
                 @LumQnt, @ProdQnt, @ProdWgt, @Price, @Addition, @Loss, @IncDeadConf,
                 @NettoWeight, @PriceTypeID, @LpDostawy,
                 @FullWeight, @EmptyWeight, @FullFarmWeight, @EmptyFarmWeight, @NettoFarmWeight)";

            using (var cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@ID", nextId);
                cmd.Parameters.AddWithValue("@Number", row.NrSpecyfikacji);  // Numer specyfikacji z kolumny B
                cmd.Parameters.AddWithValue("@CalcDate", row.DataUboju.Date);
                cmd.Parameters.AddWithValue("@CarLp", row.NrAuta);
                cmd.Parameters.AddWithValue("@CustomerGID", dostawca.ID);
                cmd.Parameters.AddWithValue("@CustomerRealGID", dostawca.ID);  // Ta sama wartość co CustomerGID
                cmd.Parameters.AddWithValue("@DeclI1", row.SztukiDek);  // Sztuki deklarowane
                cmd.Parameters.AddWithValue("@DeclI2", row.Padle);       // Padłe
                cmd.Parameters.AddWithValue("@DeclI3", row.CH);          // Chore
                cmd.Parameters.AddWithValue("@DeclI4", row.NW);          // NW
                cmd.Parameters.AddWithValue("@DeclI5", row.ZM);          // ZM
                cmd.Parameters.AddWithValue("@LumQnt", row.LUMEL);
                cmd.Parameters.AddWithValue("@ProdQnt", row.SztukiProdukcja);
                cmd.Parameters.AddWithValue("@ProdWgt", row.KilogramyProdukcja);
                cmd.Parameters.AddWithValue("@Price", row.Cena);
                cmd.Parameters.AddWithValue("@Addition", row.Dodatek);
                cmd.Parameters.AddWithValue("@Loss", row.Ubytek);
                cmd.Parameters.AddWithValue("@IncDeadConf", row.PiK ? 1 : 0);
                cmd.Parameters.AddWithValue("@NettoWeight", nettoUbojni > 0 ? nettoUbojni : nettoHodowcy);
                cmd.Parameters.AddWithValue("@PriceTypeID", priceTypeId);
                cmd.Parameters.AddWithValue("@LpDostawy", lpDostawy > 0 ? (object)lpDostawy : DBNull.Value);  // LP z harmonogramu
                // Wagi ubojni (z Excela kolumny I, J - "hodowca" w Excelu = ubojnia w bazie)
                cmd.Parameters.AddWithValue("@FullWeight", row.BruttoUbojni);       // Brutto ubojni -> FullWeight
                cmd.Parameters.AddWithValue("@EmptyWeight", row.TaraUbojni);        // Tara ubojni -> EmptyWeight
                // Wagi hodowcy (z Excela kolumny K, L - "ubojnia" w Excelu = hodowca w bazie)
                cmd.Parameters.AddWithValue("@FullFarmWeight", row.BruttoHodowcy);  // Brutto hodowcy -> FullFarmWeight
                cmd.Parameters.AddWithValue("@EmptyFarmWeight", row.TaraHodowcy);   // Tara hodowcy -> EmptyFarmWeight
                cmd.Parameters.AddWithValue("@NettoFarmWeight", nettoHodowcy);      // Netto hodowcy -> NettoFarmWeight

                cmd.ExecuteNonQuery();
            }
        }

        private int GetPriceTypeId(string typCeny)
        {
            // Mapowanie typów cen na ID - dostosuj do swojej bazy
            switch (typCeny?.ToLower())
            {
                case "wolnyrynek":
                case "wolnorynkowa":
                    return 1;
                case "rolnicza":
                    return 2;
                case "łączona":
                case "laczona":
                    return 3;
                case "ministerialna":
                    return 4;
                default:
                    return 1; // Domyślnie wolny rynek
            }
        }

        #endregion

        #region Nawigacja

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < 4)
            {
                GoToStep(_currentStep + 1);
            }
        }

        private void BtnPrevious_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                GoToStep(_currentStep - 1);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Czy na pewno chcesz anulować import?", 
                "Anuluj", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                _workbook?.Dispose();
                DialogResult = false;
                Close();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _workbook?.Dispose();
            DialogResult = true;
            Close();
        }

        #endregion

        #region INotifyPropertyChanged

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    #region Modele danych

    /// <summary>
    /// Model wiersza importowanego z Excela
    /// </summary>
    public class ImportRow : INotifyPropertyChanged
    {
        public int NrAuta { get; set; }
        public int NrSpecyfikacji { get; set; }
        public string DostawcaExcel { get; set; }
        public int SztukiDek { get; set; }
        public int Padle { get; set; }
        public int CH { get; set; }
        public int NW { get; set; }
        public int ZM { get; set; }
        public decimal BruttoHodowcy { get; set; }
        public decimal TaraHodowcy { get; set; }
        public decimal BruttoUbojni { get; set; }
        public decimal TaraUbojni { get; set; }
        public int LUMEL { get; set; }
        public int SztukiProdukcja { get; set; }
        public decimal KilogramyProdukcja { get; set; }
        public string TypCeny { get; set; }
        public decimal Cena { get; set; }
        public decimal Dodatek { get; set; }
        public bool PiK { get; set; }
        public decimal Ubytek { get; set; }
        public DateTime DataUboju { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Model mapowania dostawców Excel -> Baza danych
    /// </summary>
    public class SupplierMapping : INotifyPropertyChanged
    {
        private DostawcaItem _wybranyDostawca;
        private HarmonogramItem _wybranyHarmonogram;

        public string DostawcaExcel { get; set; }
        public int IloscWierszy { get; set; }

        public DostawcaItem WybranyDostawca
        {
            get => _wybranyDostawca;
            set
            {
                _wybranyDostawca = value;
                OnPropertyChanged(nameof(WybranyDostawca));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        public HarmonogramItem WybranyHarmonogram
        {
            get => _wybranyHarmonogram;
            set
            {
                _wybranyHarmonogram = value;
                OnPropertyChanged(nameof(WybranyHarmonogram));
            }
        }

        public string StatusText => WybranyDostawca != null ? "OK" : "Brak";
        
        public SolidColorBrush StatusColor => WybranyDostawca != null
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#388E3C"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Model dostawcy z bazy danych (jeśli nie istnieje w głównym projekcie)
    /// </summary>
    public class DostawcaItem
    {
        public string ID { get; set; }
        public string Nazwa { get; set; }
        public string AnimNo { get; set; }
        
        public string DisplayName => string.IsNullOrWhiteSpace(AnimNo) 
            ? Nazwa 
            : $"{Nazwa} ({AnimNo})";

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Model pozycji z harmonogramu dostaw
    /// </summary>
    public class HarmonogramItem
    {
        public int LP { get; set; }
        public string Dostawca { get; set; }
        public int SztukiDek { get; set; }
        public decimal Cena { get; set; }
        public decimal Ubytek { get; set; }
        public string TypCeny { get; set; }
        public string DisplayText { get; set; }

        public override string ToString() => DisplayText;
    }

    #endregion
}
