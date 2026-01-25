using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.CRM
{
    public partial class ImportCSVWindow : Window
    {
        private readonly string[] csvLines;
        private readonly string connectionString;
        private readonly string operatorID;
        private DataTable previewTable;
        private char separator = ';';

        public int ImportedCount { get; private set; } = 0;

        public ImportCSVWindow(string[] lines, string connString, string opId)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            csvLines = lines;
            connectionString = connString;
            operatorID = opId;

            ParseAndPreview();
        }

        private void ParseAndPreview()
        {
            try
            {
                previewTable = new DataTable();

                // Określ separator
                var firstLine = csvLines[0];
                if (firstLine.Contains('\t')) separator = '\t';
                else if (firstLine.Count(c => c == ',') > firstLine.Count(c => c == ';')) separator = ',';
                else separator = ';';

                // Parsuj nagłówki
                var headers = ParseCSVLine(csvLines[0]);
                foreach (var header in headers)
                {
                    string colName = string.IsNullOrWhiteSpace(header) ? $"Kolumna{previewTable.Columns.Count + 1}" : header.Trim();
                    // Upewnij się, że nazwa kolumny jest unikalna
                    string uniqueName = colName;
                    int suffix = 1;
                    while (previewTable.Columns.Contains(uniqueName))
                    {
                        uniqueName = $"{colName}_{suffix++}";
                    }
                    previewTable.Columns.Add(uniqueName);
                }

                // Parsuj dane (max 100 wierszy do podglądu)
                int maxPreview = Math.Min(csvLines.Length, 101);
                for (int i = 1; i < maxPreview; i++)
                {
                    if (string.IsNullOrWhiteSpace(csvLines[i])) continue;
                    var values = ParseCSVLine(csvLines[i]);
                    var row = previewTable.NewRow();
                    for (int j = 0; j < Math.Min(values.Length, previewTable.Columns.Count); j++)
                    {
                        row[j] = values[j];
                    }
                    previewTable.Rows.Add(row);
                }

                dgPreview.ItemsSource = previewTable.DefaultView;
                txtLiczbaWierszy.Text = $" ({csvLines.Length - 1} wierszy)";

                // Wypełnij dropdowny
                PopulateColumnDropdowns();

                // Automatyczne mapowanie
                AutoMapColumns();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd parsowania CSV: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string[] ParseCSVLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = "";

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == separator && !inQuotes)
                {
                    result.Add(current.Trim());
                    current = "";
                }
                else
                {
                    current += c;
                }
            }
            result.Add(current.Trim());

            return result.ToArray();
        }

        private void PopulateColumnDropdowns()
        {
            var columns = new List<string> { "(nie mapuj)" };
            foreach (DataColumn col in previewTable.Columns)
            {
                columns.Add(col.ColumnName);
            }

            cmbNazwa.ItemsSource = columns;
            cmbTelefon.ItemsSource = columns;
            cmbEmail.ItemsSource = columns;
            cmbMiasto.ItemsSource = columns;
            cmbUlica.ItemsSource = columns;
            cmbKod.ItemsSource = columns;
            cmbWojewodztwo.ItemsSource = columns;
            cmbBranza.ItemsSource = columns;
            cmbNIP.ItemsSource = columns;
            cmbOsoba.ItemsSource = columns;
            cmbWWW.ItemsSource = columns;

            // Ustaw domyślnie na "(nie mapuj)"
            cmbNazwa.SelectedIndex = 0;
            cmbTelefon.SelectedIndex = 0;
            cmbEmail.SelectedIndex = 0;
            cmbMiasto.SelectedIndex = 0;
            cmbUlica.SelectedIndex = 0;
            cmbKod.SelectedIndex = 0;
            cmbWojewodztwo.SelectedIndex = 0;
            cmbBranza.SelectedIndex = 0;
            cmbNIP.SelectedIndex = 0;
            cmbOsoba.SelectedIndex = 0;
            cmbWWW.SelectedIndex = 0;
        }

        private void AutoMapColumns()
        {
            // Automatyczne mapowanie na podstawie nazw kolumn
            foreach (DataColumn col in previewTable.Columns)
            {
                string colLower = col.ColumnName.ToLower();

                if (colLower.Contains("nazwa") || colLower.Contains("firma") || colLower.Contains("name") || colLower.Contains("company"))
                    SelectIfExists(cmbNazwa, col.ColumnName);
                else if (colLower.Contains("telefon") || colLower.Contains("phone") || colLower.Contains("tel"))
                    SelectIfExists(cmbTelefon, col.ColumnName);
                else if (colLower.Contains("email") || colLower.Contains("mail") || colLower.Contains("e-mail"))
                    SelectIfExists(cmbEmail, col.ColumnName);
                else if (colLower.Contains("miasto") || colLower.Contains("city") || colLower.Contains("miejscowość"))
                    SelectIfExists(cmbMiasto, col.ColumnName);
                else if (colLower.Contains("ulica") || colLower.Contains("street") || colLower.Contains("adres"))
                    SelectIfExists(cmbUlica, col.ColumnName);
                else if (colLower.Contains("kod") || colLower.Contains("postal") || colLower.Contains("zip"))
                    SelectIfExists(cmbKod, col.ColumnName);
                else if (colLower.Contains("woj") || colLower.Contains("region") || colLower.Contains("province"))
                    SelectIfExists(cmbWojewodztwo, col.ColumnName);
                else if (colLower.Contains("branża") || colLower.Contains("branza") || colLower.Contains("pkd") || colLower.Contains("industry"))
                    SelectIfExists(cmbBranza, col.ColumnName);
                else if (colLower.Contains("nip") || colLower.Contains("tax"))
                    SelectIfExists(cmbNIP, col.ColumnName);
                else if (colLower.Contains("osoba") || colLower.Contains("kontakt") || colLower.Contains("contact") || colLower.Contains("person"))
                    SelectIfExists(cmbOsoba, col.ColumnName);
                else if (colLower.Contains("www") || colLower.Contains("web") || colLower.Contains("strona") || colLower.Contains("url"))
                    SelectIfExists(cmbWWW, col.ColumnName);
            }
        }

        private void SelectIfExists(ComboBox cmb, string value)
        {
            var items = cmb.ItemsSource as List<string>;
            if (items != null && items.Contains(value))
            {
                cmb.SelectedItem = value;
            }
        }

        private void CmbSeparator_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSeparator.SelectedIndex == 0) separator = ';';
            else if (cmbSeparator.SelectedIndex == 1) separator = ',';
            else separator = '\t';

            ParseAndPreview();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnImportuj_Click(object sender, RoutedEventArgs e)
        {
            // Walidacja - nazwa firmy jest wymagana
            if (cmbNazwa.SelectedIndex == 0)
            {
                MessageBox.Show("Nazwa firmy jest wymagana. Przyporządkuj kolumnę z nazwą.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnImportuj.IsEnabled = false;
            txtStatus.Text = "Importowanie...";

            try
            {
                int imported = 0;
                int skipped = 0;
                var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Pobierz istniejące nazwy jeśli pomijamy duplikaty
                if (chkPominDuplikaty.IsChecked == true)
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        var cmd = new SqlCommand("SELECT LOWER(Nazwa) FROM OdbiorcyCRM", conn);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                existingNames.Add(reader.GetString(0));
                            }
                        }
                    }
                }

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Importuj każdy wiersz (pomijając nagłówek)
                    for (int i = 1; i < csvLines.Length; i++)
                    {
                        if (string.IsNullOrWhiteSpace(csvLines[i])) continue;

                        var values = ParseCSVLine(csvLines[i]);
                        var data = new Dictionary<string, string>();

                        // Mapuj wartości
                        MapValue(data, "Nazwa", cmbNazwa, values);
                        MapValue(data, "Telefon_K", cmbTelefon, values);
                        MapValue(data, "Email", cmbEmail, values);
                        MapValue(data, "MIASTO", cmbMiasto, values);
                        MapValue(data, "ULICA", cmbUlica, values);
                        MapValue(data, "KOD", cmbKod, values);
                        MapValue(data, "Wojewodztwo", cmbWojewodztwo, values);
                        MapValue(data, "PKD_Opis", cmbBranza, values);
                        MapValue(data, "NIP", cmbNIP, values);
                        MapValue(data, "OsobaKontaktowa", cmbOsoba, values);
                        MapValue(data, "WWW", cmbWWW, values);

                        // Pomiń jeśli brak nazwy
                        if (!data.ContainsKey("Nazwa") || string.IsNullOrWhiteSpace(data["Nazwa"]))
                        {
                            skipped++;
                            continue;
                        }

                        // Sprawdź duplikaty
                        if (chkPominDuplikaty.IsChecked == true && existingNames.Contains(data["Nazwa"].ToLower()))
                        {
                            skipped++;
                            continue;
                        }

                        // Wstaw do bazy
                        var insertCmd = new SqlCommand(@"
                            INSERT INTO OdbiorcyCRM (Nazwa, Telefon_K, Email, MIASTO, ULICA, KOD, Wojewodztwo, PKD_Opis, Status)
                            OUTPUT INSERTED.ID
                            VALUES (@nazwa, @telefon, @email, @miasto, @ulica, @kod, @woj, @pkd, 'Do zadzwonienia')", conn);

                        insertCmd.Parameters.AddWithValue("@nazwa", data.GetValueOrDefault("Nazwa", ""));
                        insertCmd.Parameters.AddWithValue("@telefon", data.GetValueOrDefault("Telefon_K", ""));
                        insertCmd.Parameters.AddWithValue("@email", data.GetValueOrDefault("Email", ""));
                        insertCmd.Parameters.AddWithValue("@miasto", data.GetValueOrDefault("MIASTO", ""));
                        insertCmd.Parameters.AddWithValue("@ulica", data.GetValueOrDefault("ULICA", ""));
                        insertCmd.Parameters.AddWithValue("@kod", data.GetValueOrDefault("KOD", ""));
                        insertCmd.Parameters.AddWithValue("@woj", data.GetValueOrDefault("Wojewodztwo", ""));
                        insertCmd.Parameters.AddWithValue("@pkd", data.GetValueOrDefault("PKD_Opis", ""));

                        var newId = (int)insertCmd.ExecuteScalar();

                        // Przypisz do operatora jeśli zaznaczono
                        if (chkPrzypisz.IsChecked == true)
                        {
                            var assignCmd = new SqlCommand("INSERT INTO WlascicieleOdbiorcow (IDOdbiorcy, OperatorID) VALUES (@id, @op)", conn);
                            assignCmd.Parameters.AddWithValue("@id", newId);
                            assignCmd.Parameters.AddWithValue("@op", operatorID);
                            assignCmd.ExecuteNonQuery();
                        }

                        // Dodaj do hashsetu żeby nie wstawiać duplikatów z tego samego pliku
                        existingNames.Add(data["Nazwa"].ToLower());
                        imported++;

                        // Aktualizuj status co 10 wierszy
                        if (imported % 10 == 0)
                        {
                            txtStatus.Text = $"Zaimportowano {imported}...";
                            System.Windows.Forms.Application.DoEvents();
                        }
                    }
                }

                ImportedCount = imported;
                MessageBox.Show($"Import zakończony!\n\nZaimportowano: {imported}\nPominięto: {skipped}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas importu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                btnImportuj.IsEnabled = true;
                txtStatus.Text = "";
            }
        }

        private void MapValue(Dictionary<string, string> data, string key, ComboBox cmb, string[] values)
        {
            if (cmb.SelectedIndex > 0)
            {
                string colName = cmb.SelectedItem.ToString();
                int colIndex = previewTable.Columns.IndexOf(colName);
                if (colIndex >= 0 && colIndex < values.Length)
                {
                    data[key] = values[colIndex].Trim().Trim('"');
                }
            }
        }
    }
}
