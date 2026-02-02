using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.CRM.Dialogs
{
    public partial class ImportFirmDialog : Window
    {
        private readonly string _connectionString;
        private readonly string _currentUserID;
        private DataTable _importData;
        private string _selectedFilePath;
        private int _validCount;

        public int ImportedCount { get; private set; }

        public ImportFirmDialog(string connectionString, string currentUserID)
        {
            InitializeComponent();
            _connectionString = connectionString;
            _currentUserID = currentUserID;
        }

        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Pliki Excel (*.xlsx)|*.xlsx|Wszystkie pliki (*.*)|*.*",
                Title = "Wybierz plik z firmami do importu"
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedFilePath = dialog.FileName;
                txtFilePath.Text = _selectedFilePath;
                LoadPreview();
            }
        }

        private void LoadPreview()
        {
            try
            {
                using var workbook = new XLWorkbook(_selectedFilePath);
                var worksheet = workbook.Worksheet(1);

                // Convert to DataTable
                _importData = new DataTable();
                bool firstRow = true;

                foreach (var row in worksheet.RowsUsed())
                {
                    if (firstRow)
                    {
                        foreach (var cell in row.Cells())
                        {
                            _importData.Columns.Add(cell.Value.ToString().Trim());
                        }
                        firstRow = false;
                        continue;
                    }

                    var dataRow = _importData.NewRow();
                    int colIdx = 0;
                    foreach (var cell in row.Cells(1, _importData.Columns.Count))
                    {
                        if (colIdx < _importData.Columns.Count)
                            dataRow[colIdx] = cell.Value.ToString().Trim();
                        colIdx++;
                    }
                    _importData.Rows.Add(dataRow);
                }

                // Show preview (first 10 rows)
                if (_importData.Rows.Count > 0)
                {
                    var previewTable = _importData.Clone();
                    for (int i = 0; i < Math.Min(10, _importData.Rows.Count); i++)
                        previewTable.ImportRow(_importData.Rows[i]);

                    dgPreview.ItemsSource = previewTable.DefaultView;
                    dgPreview.Visibility = Visibility.Visible;
                    txtPlaceholder.Visibility = Visibility.Collapsed;
                }

                // Count valid/invalid
                int total = _importData.Rows.Count;
                int invalid = 0;
                int duplicates = 0;
                _validCount = 0;

                bool hasNazwa = _importData.Columns.Contains("Nazwa firmy *");
                bool hasTelefon = _importData.Columns.Contains("Telefon 1 *");
                bool hasMiasto = _importData.Columns.Contains("Miasto *");
                bool hasWoj = _importData.Columns.Contains("Wojewodztwo *") || _importData.Columns.Contains("Województwo *");
                bool hasPKD = _importData.Columns.Contains("PKD *");

                if (!hasNazwa || !hasTelefon || !hasMiasto || !hasPKD)
                {
                    txtStats.Text = $"Znaleziono {total} wierszy, ale brakuje wymaganych kolumn!";
                    txtWarnings.Text = "Wymagane kolumny: 'Nazwa firmy *', 'Telefon 1 *', 'Miasto *', 'Wojewodztwo *', 'PKD *'";
                    btnImport.IsEnabled = false;
                    return;
                }

                string wojCol = _importData.Columns.Contains("Wojewodztwo *") ? "Wojewodztwo *" : "Województwo *";

                foreach (DataRow row in _importData.Rows)
                {
                    string nazwa = row["Nazwa firmy *"]?.ToString()?.Trim();
                    string telefon = row["Telefon 1 *"]?.ToString()?.Trim();
                    string miasto = row["Miasto *"]?.ToString()?.Trim();
                    string woj = row[wojCol]?.ToString()?.Trim();
                    string pkd = row["PKD *"]?.ToString()?.Trim();

                    if (string.IsNullOrEmpty(nazwa) || string.IsNullOrEmpty(telefon) ||
                        string.IsNullOrEmpty(miasto) || string.IsNullOrEmpty(pkd))
                    {
                        invalid++;
                        continue;
                    }

                    _validCount++;
                }

                // Check duplicates if NIP column exists
                if (_importData.Columns.Contains("NIP"))
                {
                    try
                    {
                        using var conn = new SqlConnection(_connectionString);
                        conn.Open();
                        foreach (DataRow row in _importData.Rows)
                        {
                            string nip = row["NIP"]?.ToString()?.Trim();
                            if (!string.IsNullOrEmpty(nip))
                            {
                                var cmdCheck = new SqlCommand("SELECT COUNT(*) FROM OdbiorcyCRM WHERE NIP = @NIP", conn);
                                cmdCheck.Parameters.AddWithValue("@NIP", nip);
                                if ((int)cmdCheck.ExecuteScalar() > 0) duplicates++;
                            }
                        }
                    }
                    catch { }
                }

                txtStats.Text = $"Znaleziono: {_validCount} firm do importu";
                string warnings = "";
                if (invalid > 0) warnings += $"Pominiete: {invalid} (brak wymaganych pol)\n";
                if (duplicates > 0) warnings += $"Duplikaty (NIP): {duplicates} (zostana pominiete)";
                txtWarnings.Text = warnings;

                btnImport.Content = $"Importuj {_validCount} firm";
                btnImport.IsEnabled = _validCount > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad odczytu pliku: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (_importData == null || _validCount == 0) return;

            var confirm = MessageBox.Show($"Czy na pewno zaimportowac {_validCount} firm?",
                "Potwierdz import", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            int success = 0, failed = 0;

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Create import history entry
                var cmdHistory = new SqlCommand(
                    @"IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('crm_ImportHistory') AND type = 'U')
                      INSERT INTO crm_ImportHistory (ImportedBy, FileName, TotalRows) OUTPUT INSERTED.ID VALUES (@User, @File, @Total)
                    ELSE
                      SELECT 0", conn);
                cmdHistory.Parameters.AddWithValue("@User", _currentUserID);
                cmdHistory.Parameters.AddWithValue("@File", Path.GetFileName(_selectedFilePath));
                cmdHistory.Parameters.AddWithValue("@Total", _importData.Rows.Count);
                int importID = 0;
                var result = cmdHistory.ExecuteScalar();
                if (result != null) int.TryParse(result.ToString(), out importID);

                bool hasNazwa = _importData.Columns.Contains("Nazwa firmy *");
                bool hasTelefon = _importData.Columns.Contains("Telefon 1 *");
                bool hasMiasto = _importData.Columns.Contains("Miasto *");
                bool hasPKD = _importData.Columns.Contains("PKD *");
                string wojCol = _importData.Columns.Contains("Wojewodztwo *") ? "Wojewodztwo *" :
                    (_importData.Columns.Contains("Województwo *") ? "Województwo *" : null);

                foreach (DataRow row in _importData.Rows)
                {
                    try
                    {
                        string nazwa = row["Nazwa firmy *"]?.ToString()?.Trim();
                        string telefon = row["Telefon 1 *"]?.ToString()?.Trim();
                        string miasto = row["Miasto *"]?.ToString()?.Trim();
                        string woj = wojCol != null ? row[wojCol]?.ToString()?.Trim() : "";
                        string pkd = row["PKD *"]?.ToString()?.Trim();

                        if (string.IsNullOrEmpty(nazwa) || string.IsNullOrEmpty(telefon) ||
                            string.IsNullOrEmpty(miasto) || string.IsNullOrEmpty(pkd))
                        {
                            failed++;
                            continue;
                        }

                        // Check NIP duplicate
                        string nip = _importData.Columns.Contains("NIP") ? row["NIP"]?.ToString()?.Trim() : null;
                        if (!string.IsNullOrEmpty(nip) && chkOverwrite.IsChecked != true)
                        {
                            var cmdCheck = new SqlCommand("SELECT COUNT(*) FROM OdbiorcyCRM WHERE NIP = @NIP", conn);
                            cmdCheck.Parameters.AddWithValue("@NIP", nip);
                            if ((int)cmdCheck.ExecuteScalar() > 0)
                            {
                                failed++;
                                continue;
                            }
                        }

                        string status = chkMarkImport.IsChecked == true ? "Nowy - Import" : "Do zadzwonienia";

                        // Build INSERT into OdbiorcyCRM with correct column names
                        // OdbiorcyCRM columns: Nazwa, NIP, Telefon_K, Email, ULICA, KOD, MIASTO, Wojewodztwo, PKD_Opis, Tagi, Status
                        var cmdInsert = new SqlCommand(
                            @"INSERT INTO OdbiorcyCRM
                              (Nazwa, NIP, Telefon_K, Email, ULICA, KOD, MIASTO, Wojewodztwo,
                               PKD_Opis, Tagi, Status, IsFromImport, ImportID, ImportedBy)
                            VALUES
                              (@Nazwa, @NIP, @Tel1, @Email, @Ulica, @Kod, @Miasto, @Woj,
                               @PKD, @Branza, @Status, @IsFromImport, @ImportID, @ImportedBy)", conn);

                        cmdInsert.Parameters.AddWithValue("@Nazwa", nazwa);
                        cmdInsert.Parameters.AddWithValue("@NIP", (object)nip ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@Tel1", telefon);
                        cmdInsert.Parameters.AddWithValue("@Email", GetColumnValue(row, "Email"));
                        cmdInsert.Parameters.AddWithValue("@Ulica", GetColumnValue(row, "Ulica"));
                        cmdInsert.Parameters.AddWithValue("@Kod", GetColumnValue(row, "Kod pocztowy"));
                        cmdInsert.Parameters.AddWithValue("@Miasto", miasto);
                        cmdInsert.Parameters.AddWithValue("@Woj", woj ?? "");
                        cmdInsert.Parameters.AddWithValue("@PKD", pkd);
                        cmdInsert.Parameters.AddWithValue("@Branza", GetColumnValue(row, "Branza"));
                        cmdInsert.Parameters.AddWithValue("@IsFromImport", chkMarkMyImports.IsChecked == true ? 1 : 0);
                        cmdInsert.Parameters.AddWithValue("@ImportID", importID > 0 ? importID : (object)DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@ImportedBy", _currentUserID);
                        cmdInsert.Parameters.AddWithValue("@Status", status);

                        cmdInsert.ExecuteNonQuery();
                        success++;
                    }
                    catch
                    {
                        failed++;
                    }
                }

                // Update import history
                if (importID > 0)
                {
                    var cmdUpdateHistory = new SqlCommand(
                        "UPDATE crm_ImportHistory SET SuccessRows=@S, FailedRows=@F WHERE ID=@ID", conn);
                    cmdUpdateHistory.Parameters.AddWithValue("@S", success);
                    cmdUpdateHistory.Parameters.AddWithValue("@F", failed);
                    cmdUpdateHistory.Parameters.AddWithValue("@ID", importID);
                    cmdUpdateHistory.ExecuteNonQuery();
                }

                ImportedCount = success;

                MessageBox.Show($"Import zakonczony!\n\nZaimportowano: {success}\nPominietych: {failed}",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad importu: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private object GetColumnValue(DataRow row, string columnName)
        {
            if (_importData.Columns.Contains(columnName))
            {
                var val = row[columnName]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(val)) return val;
            }
            return DBNull.Value;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
