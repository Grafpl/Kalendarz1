using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.WPF
{
    public partial class StatystykiWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private readonly DataTable _dtStatystyki = new();
        private readonly DataTable _dtStatystykiPrzyczyny = new();
        private bool _isLoading;

        public StatystykiWindow(string connLibra, string connHandel)
        {
            InitializeComponent();
            _connLibra = connLibra;
            _connHandel = connHandel;

            InitializeDataTables();
            InitializeDates();
            _ = LoadHandlowcyAsync();
        }

        private void InitializeDataTables()
        {
            // Tabela statystyk per odbiorca
            _dtStatystyki.Columns.Add("Odbiorca", typeof(string));
            _dtStatystyki.Columns.Add("Handlowiec", typeof(string));
            _dtStatystyki.Columns.Add("LiczbaAnulowanych", typeof(int));
            _dtStatystyki.Columns.Add("SumaKg", typeof(decimal));
            _dtStatystyki.Columns.Add("OstatniaData", typeof(DateTime));

            dgStatystykiAnulowane.ItemsSource = _dtStatystyki.DefaultView;
            SetupStatystykiDataGrid();

            // Tabela przyczyn
            _dtStatystykiPrzyczyny.Columns.Add("Przyczyna", typeof(string));
            _dtStatystykiPrzyczyny.Columns.Add("Liczba", typeof(int));
            _dtStatystykiPrzyczyny.Columns.Add("Procent", typeof(string));

            dgStatystykiPrzyczyny.ItemsSource = _dtStatystykiPrzyczyny.DefaultView;
            SetupPrzyczynyDataGrid();
        }

        private void SetupStatystykiDataGrid()
        {
            dgStatystykiAnulowane.Columns.Clear();

            dgStatystykiAnulowane.Columns.Add(new DataGridTextColumn
            {
                Header = "Odbiorca",
                Binding = new Binding("Odbiorca"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 200
            });

            dgStatystykiAnulowane.Columns.Add(new DataGridTextColumn
            {
                Header = "Handlowiec",
                Binding = new Binding("Handlowiec"),
                Width = new DataGridLength(100),
                ElementStyle = (Style)FindResource("BoldCellStyle")
            });

            dgStatystykiAnulowane.Columns.Add(new DataGridTextColumn
            {
                Header = "Liczba anulowanych",
                Binding = new Binding("LiczbaAnulowanych"),
                Width = new DataGridLength(130),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            var sumStyle = new Style(typeof(TextBlock));
            sumStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            sumStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));

            dgStatystykiAnulowane.Columns.Add(new DataGridTextColumn
            {
                Header = "Suma kg",
                Binding = new Binding("SumaKg") { StringFormat = "N0" },
                Width = new DataGridLength(100),
                ElementStyle = sumStyle
            });

            dgStatystykiAnulowane.Columns.Add(new DataGridTextColumn
            {
                Header = "Ostatnia anulacja",
                Binding = new Binding("OstatniaData") { StringFormat = "yyyy-MM-dd" },
                Width = new DataGridLength(130),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            dgStatystykiAnulowane.LoadingRow += DgStatystykiAnulowane_LoadingRow;
        }

        private void DgStatystykiAnulowane_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                int count = rowView.Row.Field<int>("LiczbaAnulowanych");

                if (count >= 10)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 220, 220));
                    e.Row.FontWeight = FontWeights.Bold;
                }
                else if (count >= 5)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 245, 200));
                }
            }
        }

        private void SetupPrzyczynyDataGrid()
        {
            dgStatystykiPrzyczyny.Columns.Clear();

            dgStatystykiPrzyczyny.Columns.Add(new DataGridTextColumn
            {
                Header = "Przyczyna",
                Binding = new Binding("Przyczyna"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 150
            });

            var countStyle = new Style(typeof(TextBlock));
            countStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            countStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));

            dgStatystykiPrzyczyny.Columns.Add(new DataGridTextColumn
            {
                Header = "Liczba",
                Binding = new Binding("Liczba"),
                Width = new DataGridLength(70),
                ElementStyle = countStyle
            });

            var percentStyle = new Style(typeof(TextBlock));
            percentStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            percentStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(155, 89, 182))));
            percentStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));

            dgStatystykiPrzyczyny.Columns.Add(new DataGridTextColumn
            {
                Header = "%",
                Binding = new Binding("Procent"),
                Width = new DataGridLength(60),
                ElementStyle = percentStyle
            });

            dgStatystykiPrzyczyny.LoadingRow += DgStatystykiPrzyczyny_LoadingRow;
        }

        private void DgStatystykiPrzyczyny_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                string przyczyna = rowView.Row.Field<string>("Przyczyna") ?? "";

                if (przyczyna.Contains("Anulowanie", StringComparison.OrdinalIgnoreCase))
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 230, 230));
                }
                else if (przyczyna.Contains("Poprawa", StringComparison.OrdinalIgnoreCase))
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(230, 245, 255));
                }
            }
        }

        private void InitializeDates()
        {
            var today = DateTime.Today;
            dpOd.SelectedDate = new DateTime(today.Year, today.Month, 1);
            dpDo.SelectedDate = today;
            UpdateDateRangeText();
        }

        private void UpdateDateRangeText()
        {
            if (dpOd.SelectedDate.HasValue && dpDo.SelectedDate.HasValue)
            {
                txtDateRange.Text = $"Zakres dat: {dpOd.SelectedDate:yyyy-MM-dd} - {dpDo.SelectedDate:yyyy-MM-dd}";
            }
        }

        private async System.Threading.Tasks.Task LoadHandlowcyAsync()
        {
            var handlowcy = new List<string> { "(Wszystkie)" };

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                const string sql = @"SELECT DISTINCT wym.CDim_Handlowiec_Val
                                     FROM [HANDEL].[SSCommon].[ContractorClassification] wym
                                     WHERE wym.CDim_Handlowiec_Val IS NOT NULL AND wym.CDim_Handlowiec_Val <> ''
                                     ORDER BY wym.CDim_Handlowiec_Val";
                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    handlowcy.Add(reader.GetString(0));
                }
            }
            catch { }

            cmbHandlowiec.ItemsSource = handlowcy;
            cmbHandlowiec.SelectedIndex = 0;

            _ = LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (_isLoading) return;
            if (!dpOd.SelectedDate.HasValue || !dpDo.SelectedDate.HasValue) return;

            _isLoading = true;
            _dtStatystyki.Rows.Clear();
            _dtStatystykiPrzyczyny.Rows.Clear();

            try
            {
                DateTime dataOd = dpOd.SelectedDate.Value;
                DateTime dataDo = dpDo.SelectedDate.Value;
                string handlowiec = cmbHandlowiec?.SelectedItem?.ToString() ?? "(Wszystkie)";

                // Pobierz kontrahentow
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

                // Pobierz anulowane zamowienia w wybranym okresie
                var statystyki = new Dictionary<int, (int Count, decimal SumaKg, DateTime LastDate)>();
                var statystykiPrzyczyny = new Dictionary<string, int>();

                // Dodaj MARS do connection string
                var connWithMars = _connLibra.Contains("MultipleActiveResultSets")
                    ? _connLibra
                    : _connLibra + ";MultipleActiveResultSets=True";

                await using (var cnLibra = new SqlConnection(connWithMars))
                {
                    await cnLibra.OpenAsync();

                    // Statystyki per odbiorca
                    string sql = @"
                        SELECT zm.KlientId, COUNT(*) as Cnt, SUM(ISNULL(zmt.IloscSuma, 0)) as Suma,
                               MAX(COALESCE(zm.DataAnulowania, zm.DataPrzyjazdu, zm.DataZamowienia)) as LastDate
                        FROM [dbo].[ZamowieniaMieso] zm
                        LEFT JOIN (
                            SELECT ZamowienieId, SUM(Ilosc) as IloscSuma
                            FROM [dbo].[ZamowieniaMiesoTowar]
                            GROUP BY ZamowienieId
                        ) zmt ON zm.Id = zmt.ZamowienieId
                        WHERE zm.Status = 'Anulowane'
                          AND COALESCE(zm.DataAnulowania, zm.DataPrzyjazdu, zm.DataZamowienia) >= @DataOd
                          AND COALESCE(zm.DataAnulowania, zm.DataPrzyjazdu, zm.DataZamowienia) <= @DataDo
                        GROUP BY zm.KlientId";

                    await using var cmd = new SqlCommand(sql, cnLibra);
                    cmd.Parameters.AddWithValue("@DataOd", dataOd.Date);
                    cmd.Parameters.AddWithValue("@DataDo", dataDo.Date);

                    await using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        if (reader.IsDBNull(0)) continue;
                        int clientId = reader.GetInt32(0);
                        int count = reader.GetInt32(1);
                        decimal suma = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
                        DateTime lastDate = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3);

                        statystyki[clientId] = (count, suma, lastDate);
                    }

                    // Sprawdz czy kolumna PrzyczynaAnulowania istnieje
                    bool hasPrzyczynaColumn = false;
                    const string checkQuery = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                                                WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'PrzyczynaAnulowania'";
                    await using (var checkCmd = new SqlCommand(checkQuery, cnLibra))
                    {
                        hasPrzyczynaColumn = (int)await checkCmd.ExecuteScalarAsync() > 0;
                    }

                    if (hasPrzyczynaColumn)
                    {
                        // Statystyki przyczyn anulowania
                        string sqlPrzyczyny = @"
                            SELECT ISNULL(PrzyczynaAnulowania, 'Brak przyczyny') as Przyczyna, COUNT(*) as Cnt
                            FROM [dbo].[ZamowieniaMieso]
                            WHERE Status = 'Anulowane'
                              AND COALESCE(DataAnulowania, DataPrzyjazdu, DataZamowienia) >= @DataOd
                              AND COALESCE(DataAnulowania, DataPrzyjazdu, DataZamowienia) <= @DataDo
                            GROUP BY ISNULL(PrzyczynaAnulowania, 'Brak przyczyny')
                            ORDER BY Cnt DESC";

                        await using var cmdPrzyczyny = new SqlCommand(sqlPrzyczyny, cnLibra);
                        cmdPrzyczyny.Parameters.AddWithValue("@DataOd", dataOd.Date);
                        cmdPrzyczyny.Parameters.AddWithValue("@DataDo", dataDo.Date);

                        await using var readerPrzyczyny = await cmdPrzyczyny.ExecuteReaderAsync();

                        while (await readerPrzyczyny.ReadAsync())
                        {
                            string przyczyna = readerPrzyczyny.GetString(0);
                            int count = readerPrzyczyny.GetInt32(1);
                            statystykiPrzyczyny[przyczyna] = count;
                        }
                    }
                    else
                    {
                        // Jesli kolumna nie istnieje, dodaj pojedynczy rekord
                        int totalCancelled = statystyki.Values.Sum(s => s.Count);
                        if (totalCancelled > 0)
                        {
                            statystykiPrzyczyny["Brak przyczyny"] = totalCancelled;
                        }
                    }
                }

                // Uzupelnij tabele odbiorcow
                int totalAnulowane = 0;
                decimal totalKg = 0m;
                foreach (var kvp in statystyki)
                {
                    int clientId = kvp.Key;
                    var (count, suma, lastDate) = kvp.Value;

                    var (name, salesman) = contractors.TryGetValue(clientId, out var c) ? c : ($"Nieznany ({clientId})", "");

                    // Filtruj po handlowcu jesli wybrano
                    if (handlowiec != "(Wszystkie)" && salesman != handlowiec)
                        continue;

                    _dtStatystyki.Rows.Add(name, salesman, count, suma, lastDate);
                    totalAnulowane += count;
                    totalKg += suma;
                }

                // Sortuj po liczbie anulowanych malejaco
                _dtStatystyki.DefaultView.Sort = "LiczbaAnulowanych DESC";

                // Uzupelnij tabele przyczyn
                int totalPrzyczyny = statystykiPrzyczyny.Values.Sum();
                foreach (var kvp in statystykiPrzyczyny.OrderByDescending(x => x.Value))
                {
                    string przyczyna = kvp.Key;
                    int count = kvp.Value;
                    double procent = totalPrzyczyny > 0 ? (double)count / totalPrzyczyny * 100 : 0;
                    _dtStatystykiPrzyczyny.Rows.Add(przyczyna, count, $"{procent:F1}%");
                }

                // Aktualizuj podsumowanie
                txtTotalCancelled.Text = totalAnulowane.ToString("N0");
                txtSumaKg.Text = $"{totalKg:N0} kg";

                int odbiorcy = _dtStatystyki.Rows.Count;
                double srednia = odbiorcy > 0 ? (double)totalAnulowane / odbiorcy : 0;
                txtSrednia.Text = $"{srednia:F1} zamowien/odbiorce";

                UpdateDateRangeText();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad podczas ladowania danych: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void CmbOkres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpOd == null || dpDo == null) return;

            if (cmbOkres.SelectedItem is ComboBoxItem item && item.Tag is string period)
            {
                DateTime today = DateTime.Today;
                switch (period)
                {
                    case "Year":
                        dpOd.SelectedDate = new DateTime(today.Year, 1, 1);
                        dpDo.SelectedDate = today;
                        break;
                    case "Month":
                        dpOd.SelectedDate = new DateTime(today.Year, today.Month, 1);
                        dpDo.SelectedDate = today;
                        break;
                    case "Week":
                        int delta = ((int)today.DayOfWeek + 6) % 7;
                        dpOd.SelectedDate = today.AddDays(-delta);
                        dpDo.SelectedDate = today;
                        break;
                    case "Day":
                        dpOd.SelectedDate = today;
                        dpDo.SelectedDate = today;
                        break;
                }
            }
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
