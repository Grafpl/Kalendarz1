using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.WPF
{
    public partial class HistoriaZmianWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private readonly string _userId;
        private readonly DataTable _dtHistoria = new();
        private bool _isLoading;
        private Dictionary<int, string> _productNames = new();

        public HistoriaZmianWindow(string connLibra, string connHandel, string userId = "")
        {
            InitializeComponent();
            _connLibra = connLibra;
            _connHandel = connHandel;
            _userId = userId;

            InitializeDataTable();
            _ = LoadDataAsync();
        }

        private void InitializeDataTable()
        {
            _dtHistoria.Columns.Add("Id", typeof(int));
            _dtHistoria.Columns.Add("ZamowienieId", typeof(int));
            _dtHistoria.Columns.Add("DataZmiany", typeof(DateTime));
            _dtHistoria.Columns.Add("TypZmiany", typeof(string));
            _dtHistoria.Columns.Add("Handlowiec", typeof(string));
            _dtHistoria.Columns.Add("Odbiorca", typeof(string));
            _dtHistoria.Columns.Add("UzytkownikNazwa", typeof(string));
            _dtHistoria.Columns.Add("Towar", typeof(string));
            _dtHistoria.Columns.Add("KodTowaru", typeof(int));
            _dtHistoria.Columns.Add("OpisZmiany", typeof(string));

            dgHistoria.ItemsSource = _dtHistoria.DefaultView;
            SetupDataGrid();
        }

        private void SetupDataGrid()
        {
            dgHistoria.Columns.Clear();

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Data zmiany",
                Binding = new Binding("DataZmiany") { StringFormat = "yyyy-MM-dd HH:mm" },
                Width = new DataGridLength(130)
            });

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Typ",
                Binding = new Binding("TypZmiany"),
                Width = new DataGridLength(100)
            });

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Odbiorca",
                Binding = new Binding("Odbiorca"),
                Width = new DataGridLength(180)
            });

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Handlowiec",
                Binding = new Binding("Handlowiec"),
                Width = new DataGridLength(100)
            });

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Użytkownik",
                Binding = new Binding("UzytkownikNazwa"),
                Width = new DataGridLength(120)
            });

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Towar",
                Binding = new Binding("Towar"),
                Width = new DataGridLength(150)
            });

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Opis zmiany",
                Binding = new Binding("OpisZmiany"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (_isLoading) return;

            _isLoading = true;
            _dtHistoria.Rows.Clear();

            try
            {
                // Pobierz kontrahentów
                var contractors = new Dictionary<int, (string Name, string Salesman)>();
                await using (var cnHandel = new SqlConnection(_connHandel))
                {
                    await cnHandel.OpenAsync();
                    const string sqlContr = @"SELECT c.Id, c.Shortcut, wym.CDim_Handlowiec_Val
                                    FROM [HANDEL].[SSCommon].[STContractors] c
                                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym
                                    ON c.Id = wym.ElementId";
                    await using var cmdContr = new SqlCommand(sqlContr, cnHandel);
                    await using var rd = await cmdContr.ExecuteReaderAsync();

                    while (await rd.ReadAsync())
                    {
                        int id = rd.GetInt32(0);
                        string shortcut = rd.IsDBNull(1) ? "" : rd.GetString(1);
                        string salesman = rd.IsDBNull(2) ? "" : rd.GetString(2);
                        contractors[id] = (string.IsNullOrWhiteSpace(shortcut) ? $"KH {id}" : shortcut, salesman);
                    }
                }

                // Pobierz produkty z katalogu TW (tylko Świeże 67095 i Mrożone 67153)
                _productNames.Clear();
                try
                {
                    await using var cnHandel = new SqlConnection(_connHandel);
                    await cnHandel.OpenAsync();
                    const string sqlProducts = "SELECT ID, kod FROM [HANDEL].[HM].[TW] WHERE katalog IN (67095, 67153) ORDER BY kod";
                    await using var cmdProducts = new SqlCommand(sqlProducts, cnHandel);
                    await using var rdrProducts = await cmdProducts.ExecuteReaderAsync();
                    while (await rdrProducts.ReadAsync())
                    {
                        if (!rdrProducts.IsDBNull(0))
                        {
                            int id = rdrProducts.GetInt32(0);
                            string kod = rdrProducts.IsDBNull(1) ? $"ID:{id}" : rdrProducts.GetString(1);
                            _productNames[id] = kod;
                        }
                    }
                }
                catch { /* Ignoruj błędy ładowania produktów */ }

                // Pobierz wszystkie zamówienia
                var orderToClient = new Dictionary<int, int>();
                await using (var cnLibra = new SqlConnection(_connLibra))
                {
                    await cnLibra.OpenAsync();

                    // Sprawdź czy tabela istnieje
                    string checkSql = @"SELECT COUNT(*) FROM sys.objects
                                       WHERE object_id = OBJECT_ID(N'[dbo].[HistoriaZmianZamowien]') AND type in (N'U')";
                    using var checkCmd = new SqlCommand(checkSql, cnLibra);
                    var tableExists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                    if (!tableExists)
                    {
                        MessageBox.Show("Tabela historii zmian nie istnieje w bazie danych.", "Informacja",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // Pobierz wszystkie zamówienia
                    string sqlOrders = @"SELECT Id, KlientId FROM dbo.ZamowieniaMieso";
                    await using var cmdOrders = new SqlCommand(sqlOrders, cnLibra);
                    await using var rdrOrders = await cmdOrders.ExecuteReaderAsync();

                    while (await rdrOrders.ReadAsync())
                    {
                        int orderId = rdrOrders.GetInt32(0);
                        int clientId = rdrOrders.IsDBNull(1) ? 0 : rdrOrders.GetInt32(1);
                        orderToClient[orderId] = clientId;
                    }

                    await rdrOrders.CloseAsync();

                    // Sprawdź czy tabela ma kolumnę KodTowaru
                    bool hasKodTowaru = false;
                    const string checkColSql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                                                WHERE TABLE_NAME = 'HistoriaZmianZamowien' AND COLUMN_NAME = 'KodTowaru'";
                    await using (var checkColCmd = new SqlCommand(checkColSql, cnLibra))
                    {
                        hasKodTowaru = (int)await checkColCmd.ExecuteScalarAsync() > 0;
                    }

                    // Pobierz CAŁĄ historię - posortowaną od najnowszej do najstarszej
                    string sqlHistory = hasKodTowaru
                        ? @"SELECT Id, ZamowienieId, DataZmiany, TypZmiany, UzytkownikNazwa, OpisZmiany, KodTowaru
                            FROM HistoriaZmianZamowien
                            ORDER BY DataZmiany DESC"
                        : @"SELECT Id, ZamowienieId, DataZmiany, TypZmiany, UzytkownikNazwa, OpisZmiany
                            FROM HistoriaZmianZamowien
                            ORDER BY DataZmiany DESC";

                    await using var cmdHistory = new SqlCommand(sqlHistory, cnLibra);
                    await using var rdrHistory = await cmdHistory.ExecuteReaderAsync();

                    while (await rdrHistory.ReadAsync())
                    {
                        int id = rdrHistory.GetInt32(0);
                        int zamowienieId = rdrHistory.GetInt32(1);
                        DateTime dataZmiany = rdrHistory.GetDateTime(2);
                        string typZmiany = rdrHistory.IsDBNull(3) ? "" : rdrHistory.GetString(3);
                        string uzytkownikNazwa = rdrHistory.IsDBNull(4) ? "" : rdrHistory.GetString(4);
                        string opisZmiany = rdrHistory.IsDBNull(5) ? "" : rdrHistory.GetString(5);

                        // Pobierz towar jeśli kolumna istnieje
                        string towar = "";
                        int kodTowaru = 0;
                        if (hasKodTowaru && !rdrHistory.IsDBNull(6))
                        {
                            kodTowaru = rdrHistory.GetInt32(6);
                            towar = _productNames.TryGetValue(kodTowaru, out var name) ? name : $"ID:{kodTowaru}";
                        }
                        else
                        {
                            // Spróbuj wyciągnąć nazwę towaru z opisu zmiany
                            towar = ExtractProductFromDescription(opisZmiany);
                        }

                        string handlowiec = "";
                        string odbiorca = "";
                        if (orderToClient.TryGetValue(zamowienieId, out int clientId) &&
                            contractors.TryGetValue(clientId, out var contr))
                        {
                            handlowiec = contr.Salesman;
                            odbiorca = contr.Name;
                        }

                        _dtHistoria.Rows.Add(id, zamowienieId, dataZmiany, typZmiany,
                            handlowiec, odbiorca, uzytkownikNazwa, towar, kodTowaru, opisZmiany);
                    }
                }

                txtTotalChanges.Text = _dtHistoria.Rows.Count.ToString("N0");
                txtDateRange.Text = $"Łącznie: {_dtHistoria.Rows.Count:N0} zmian";
                PopulateFilterComboBoxes();
                UpdateDisplayedCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private string ExtractProductFromDescription(string opis)
        {
            if (string.IsNullOrEmpty(opis)) return "";

            // Wzorce do wyciągnięcia nazwy produktu z opisu
            var patterns = new[]
            {
                @"Zmiana ilości:\s*(.+?)\s+z\s+\d",
                @"Zmiana ceny:\s*(.+?)\s+z\s+\d",
                @"Produkt:\s*(.+?)(?:\s*[-,]|$)",
                @"Towar:\s*(.+?)(?:\s*[-,]|$)",
                @"^(.+?)\s*[-:]\s*(?:ilość|cena|zmiana)",
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(opis, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    var product = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(product) && product.Length > 2)
                        return product;
                }
            }

            return "";
        }

        private void PopulateFilterComboBoxes()
        {
            // ComboBox Towar - produkty z katalogu TW
            var towary = new List<string> { "(Wszystkie)" };
            towary.AddRange(_productNames.Values.OrderBy(x => x));
            cmbTowar.ItemsSource = towary;
            cmbTowar.SelectedIndex = 0;

            var users = new List<string> { "(Wszystkie)" };
            var odbiorcy = new List<string> { "(Wszystkie)" };
            var typy = new List<string> { "(Wszystkie)" };
            var handlowcy = new List<string> { "(Wszystkie)" };

            foreach (DataRow row in _dtHistoria.Rows)
            {
                var user = row["UzytkownikNazwa"]?.ToString();
                var odbiorca = row["Odbiorca"]?.ToString();
                var typ = row["TypZmiany"]?.ToString();
                var handlowiec = row["Handlowiec"]?.ToString();

                if (!string.IsNullOrEmpty(user) && !users.Contains(user)) users.Add(user);
                if (!string.IsNullOrEmpty(odbiorca) && !odbiorcy.Contains(odbiorca)) odbiorcy.Add(odbiorca);
                if (!string.IsNullOrEmpty(typ) && !typy.Contains(typ)) typy.Add(typ);
                if (!string.IsNullOrEmpty(handlowiec) && !handlowcy.Contains(handlowiec)) handlowcy.Add(handlowiec);
            }

            cmbKtoEdytowal.ItemsSource = users.OrderBy(x => x).ToList();
            cmbOdbiorca.ItemsSource = odbiorcy.OrderBy(x => x).ToList();
            cmbTyp.ItemsSource = typy.OrderBy(x => x).ToList();
            cmbHandlowiec.ItemsSource = handlowcy.OrderBy(x => x).ToList();

            cmbKtoEdytowal.SelectedIndex = 0;
            cmbOdbiorca.SelectedIndex = 0;
            cmbTyp.SelectedIndex = 0;
            cmbHandlowiec.SelectedIndex = 0;
        }

        private void ApplyFilters()
        {
            var dv = _dtHistoria.DefaultView;
            var filters = new List<string>();

            // Filtr produktu z ComboBox - szukaj w Towar LUB w OpisZmiany
            if (cmbTowar.SelectedItem?.ToString() is string towar && towar != "(Wszystkie)")
            {
                string escapedTowar = towar.Replace("'", "''").Replace("[", "[[]").Replace("*", "[*]").Replace("%", "[%]");
                filters.Add($"(Towar = '{escapedTowar}' OR OpisZmiany LIKE '*{escapedTowar}*')");
                txtHistoriaTitle.Text = $"HISTORIA ZMIAN - {towar.ToUpper()}";
            }
            else
            {
                txtHistoriaTitle.Text = "HISTORIA ZMIAN";
            }

            if (cmbKtoEdytowal.SelectedItem?.ToString() is string user && user != "(Wszystkie)")
                filters.Add($"UzytkownikNazwa = '{user.Replace("'", "''")}'");

            if (cmbOdbiorca.SelectedItem?.ToString() is string odbiorca && odbiorca != "(Wszystkie)")
                filters.Add($"Odbiorca = '{odbiorca.Replace("'", "''")}'");

            if (cmbTyp.SelectedItem?.ToString() is string typ && typ != "(Wszystkie)")
                filters.Add($"TypZmiany = '{typ.Replace("'", "''")}'");

            if (cmbHandlowiec.SelectedItem?.ToString() is string handlowiec && handlowiec != "(Wszystkie)")
                filters.Add($"Handlowiec = '{handlowiec.Replace("'", "''")}'");

            dv.RowFilter = filters.Count > 0 ? string.Join(" AND ", filters) : "";
            UpdateDisplayedCount();
        }

        private void UpdateDisplayedCount()
        {
            var dv = _dtHistoria.DefaultView;
            txtDisplayedCount.Text = dv.Count.ToString("N0");
        }

        private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            ApplyFilters();
        }

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            cmbTowar.SelectedIndex = 0;
            cmbKtoEdytowal.SelectedIndex = 0;
            cmbOdbiorca.SelectedIndex = 0;
            cmbTyp.SelectedIndex = 0;
            cmbHandlowiec.SelectedIndex = 0;
            txtHistoriaTitle.Text = "HISTORIA ZMIAN";
            _dtHistoria.DefaultView.RowFilter = "";
            UpdateDisplayedCount();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadDataAsync();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Eksport do Excel - funkcja do zaimplementowania", "Informacja",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DgHistoria_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Znajdź kliknięty wiersz
            var dep = (DependencyObject)e.OriginalSource;
            while (dep != null && !(dep is DataGridRow))
            {
                dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
            }

            if (dep is DataGridRow row && row.Item is DataRowView rowView)
            {
                // Pobierz ID zamówienia z wiersza
                var zamowienieId = rowView.Row.Field<int>("ZamowienieId");
                if (zamowienieId > 0)
                {
                    // Otwórz okno edycji zamówienia
                    var widokZamowienia = new Kalendarz1.WidokZamowienia(_userId, zamowienieId);
                    if (widokZamowienia.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        // Odśwież dane po edycji
                        _ = LoadDataAsync();
                    }
                }
            }
        }
    }
}
