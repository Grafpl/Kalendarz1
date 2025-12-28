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
        private readonly DataTable _dtHistoria = new();
        private bool _isLoading;

        public HistoriaZmianWindow(string connLibra, string connHandel)
        {
            InitializeComponent();
            _connLibra = connLibra;
            _connHandel = connHandel;

            InitializeDataTable();
            InitializeDates();
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
                Header = "Opis zmiany",
                Binding = new Binding("OpisZmiany"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        }

        private void InitializeDates()
        {
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            if (today.DayOfWeek == DayOfWeek.Sunday) startOfWeek = startOfWeek.AddDays(-7);

            dpOd.SelectedDate = startOfWeek;
            dpDo.SelectedDate = today;

            UpdateDateRangeText();
            _ = LoadDataAsync();
        }

        private void UpdateDateRangeText()
        {
            if (dpOd.SelectedDate.HasValue && dpDo.SelectedDate.HasValue)
            {
                txtDateRange.Text = $"Zakres dat: {dpOd.SelectedDate:yyyy-MM-dd} - {dpDo.SelectedDate:yyyy-MM-dd}";
            }
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (_isLoading) return;
            if (!dpOd.SelectedDate.HasValue || !dpDo.SelectedDate.HasValue) return;

            _isLoading = true;
            _dtHistoria.Rows.Clear();

            try
            {
                var startDate = dpOd.SelectedDate.Value;
                var endDate = dpDo.SelectedDate.Value;

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

                // Pobierz zamówienia i historię
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

                    // Pobierz zamówienia
                    string sqlOrders = @"SELECT Id, KlientId FROM dbo.ZamowieniaMieso
                                        WHERE DataPrzyjazdu BETWEEN @StartDate AND @EndDate";
                    await using var cmdOrders = new SqlCommand(sqlOrders, cnLibra);
                    cmdOrders.Parameters.AddWithValue("@StartDate", startDate);
                    cmdOrders.Parameters.AddWithValue("@EndDate", endDate);
                    await using var rdrOrders = await cmdOrders.ExecuteReaderAsync();

                    while (await rdrOrders.ReadAsync())
                    {
                        int orderId = rdrOrders.GetInt32(0);
                        int clientId = rdrOrders.GetInt32(1);
                        orderToClient[orderId] = clientId;
                    }

                    await rdrOrders.CloseAsync();

                    if (orderToClient.Count == 0)
                    {
                        txtTotalChanges.Text = "0";
                        PopulateFilterComboBoxes();
                        return;
                    }

                    // Pobierz historię
                    string orderIds = string.Join(",", orderToClient.Keys);
                    string sqlHistory = $@"SELECT Id, ZamowienieId, DataZmiany, TypZmiany, UzytkownikNazwa, OpisZmiany
                                          FROM HistoriaZmianZamowien
                                          WHERE ZamowienieId IN ({orderIds})
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

                        string handlowiec = "";
                        string odbiorca = "";
                        if (orderToClient.TryGetValue(zamowienieId, out int clientId) &&
                            contractors.TryGetValue(clientId, out var contr))
                        {
                            handlowiec = contr.Salesman;
                            odbiorca = contr.Name;
                        }

                        _dtHistoria.Rows.Add(id, zamowienieId, dataZmiany, typZmiany,
                            handlowiec, odbiorca, uzytkownikNazwa, opisZmiany);
                    }
                }

                txtTotalChanges.Text = _dtHistoria.Rows.Count.ToString("N0");
                PopulateFilterComboBoxes();
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

        private void PopulateFilterComboBoxes()
        {
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

            if (cmbKtoEdytowal.SelectedItem?.ToString() is string user && user != "(Wszystkie)")
                filters.Add($"UzytkownikNazwa = '{user.Replace("'", "''")}'");

            if (cmbOdbiorca.SelectedItem?.ToString() is string odbiorca && odbiorca != "(Wszystkie)")
                filters.Add($"Odbiorca = '{odbiorca.Replace("'", "''")}'");

            if (cmbTyp.SelectedItem?.ToString() is string typ && typ != "(Wszystkie)")
                filters.Add($"TypZmiany = '{typ.Replace("'", "''")}'");

            if (cmbHandlowiec.SelectedItem?.ToString() is string handlowiec && handlowiec != "(Wszystkie)")
                filters.Add($"Handlowiec = '{handlowiec.Replace("'", "''")}'");

            dv.RowFilter = filters.Count > 0 ? string.Join(" AND ", filters) : "";
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateDateRangeText();
            _ = LoadDataAsync();
        }

        private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            ApplyFilters();
        }

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            cmbKtoEdytowal.SelectedIndex = 0;
            cmbOdbiorca.SelectedIndex = 0;
            cmbTyp.SelectedIndex = 0;
            cmbHandlowiec.SelectedIndex = 0;
            _dtHistoria.DefaultView.RowFilter = "";
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
    }
}
