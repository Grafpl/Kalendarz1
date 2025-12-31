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
    public partial class TransportWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private readonly string _connTransport;
        private readonly DataTable _dtTransport = new();
        private DateTime _selectedDate;
        private bool _isLoading;

        // Cache dla kolorów tras (grupowanie po KursID)
        private readonly Dictionary<long, Color> _routeColors = new();
        private readonly List<Color> _colorPalette = new List<Color>
        {
            Color.FromRgb(232, 245, 233), // jasny zielony
            Color.FromRgb(227, 242, 253), // jasny niebieski
            Color.FromRgb(255, 243, 224), // jasny pomarańczowy
            Color.FromRgb(243, 229, 245), // jasny fioletowy
            Color.FromRgb(255, 235, 238), // jasny różowy
            Color.FromRgb(224, 247, 250), // jasny cyjan
            Color.FromRgb(255, 249, 196), // jasny żółty
            Color.FromRgb(225, 245, 254), // jasny błękitny
        };
        private int _colorIndex = 0;

        // Timer do debounce
        private System.Windows.Threading.DispatcherTimer _filterDebounceTimer;

        // Do śledzenia grup (przerwy między trasami)
        private long? _previousKursId = null;

        public TransportWindow(string connLibra, string connHandel, string connTransport, DateTime? initialDate = null)
        {
            InitializeComponent();
            _connLibra = connLibra;
            _connHandel = connHandel;
            _connTransport = connTransport;
            _selectedDate = initialDate ?? DateTime.Today;

            InitializeDataTable();
            InitializeDebounce();
            InitializeDate();
        }

        private void InitializeDataTable()
        {
            _dtTransport.Columns.Add("Id", typeof(int));
            _dtTransport.Columns.Add("KlientId", typeof(int));
            _dtTransport.Columns.Add("KursId", typeof(long)); // Do grupowania
            _dtTransport.Columns.Add("DataUboju", typeof(DateTime));
            _dtTransport.Columns.Add("Odbiorca", typeof(string));
            _dtTransport.Columns.Add("Handlowiec", typeof(string));
            _dtTransport.Columns.Add("IloscZamowiona", typeof(decimal));
            _dtTransport.Columns.Add("IloscWydana", typeof(decimal));
            _dtTransport.Columns.Add("Palety", typeof(decimal));
            _dtTransport.Columns.Add("Kierowca", typeof(string));
            _dtTransport.Columns.Add("Pojazd", typeof(string));
            _dtTransport.Columns.Add("GodzWyjazdu", typeof(string));
            _dtTransport.Columns.Add("Trasa", typeof(string));
            _dtTransport.Columns.Add("Status", typeof(string));
            _dtTransport.Columns.Add("GrupaIndex", typeof(int)); // Do sortowania grup

            dgTransport.ItemsSource = _dtTransport.DefaultView;
            SetupDataGrid();
        }

        private void InitializeDebounce()
        {
            _filterDebounceTimer = new System.Windows.Threading.DispatcherTimer();
            _filterDebounceTimer.Interval = TimeSpan.FromMilliseconds(300);
            _filterDebounceTimer.Tick += (s, e) =>
            {
                _filterDebounceTimer.Stop();
                ApplyFilters();
            };
        }

        private void SetupDataGrid()
        {
            dgTransport.Columns.Clear();

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Data",
                Binding = new Binding("DataUboju") { StringFormat = "yyyy-MM-dd" },
                Width = new DataGridLength(90)
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Odbiorca",
                Binding = new Binding("Odbiorca"),
                Width = new DataGridLength(200)
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Hand",
                Binding = new Binding("Handlowiec"),
                Width = new DataGridLength(55),
                ElementStyle = (Style)FindResource("BoldCellStyle")
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Zam.",
                Binding = new Binding("IloscZamowiona") { StringFormat = "N0" },
                Width = new DataGridLength(70),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Wyd.",
                Binding = new Binding("IloscWydana") { StringFormat = "N0" },
                Width = new DataGridLength(70),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Palety",
                Binding = new Binding("Palety") { StringFormat = "N1" },
                Width = new DataGridLength(65),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Kierowca",
                Binding = new Binding("Kierowca"),
                Width = new DataGridLength(160)
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Pojazd",
                Binding = new Binding("Pojazd"),
                Width = new DataGridLength(100),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Godz",
                Binding = new Binding("GodzWyjazdu"),
                Width = new DataGridLength(60),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Trasa",
                Binding = new Binding("Trasa"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 150
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Status",
                Binding = new Binding("Status"),
                Width = new DataGridLength(90),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            dgTransport.LoadingRow += DgTransport_LoadingRow;
        }

        private void DgTransport_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                var status = rowView.Row.Field<string>("Status") ?? "";
                var kursId = rowView.Row.IsNull("KursId") ? 0L : rowView.Row.Field<long>("KursId");

                if (status == "Brak" || kursId == 0)
                {
                    // Bez przypisanego transportu - jasne czerwone tło
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 235, 235));
                    e.Row.BorderThickness = new Thickness(0);
                }
                else
                {
                    // Kolorowanie wg trasy
                    var color = GetColorForRoute(kursId);
                    e.Row.Background = new SolidColorBrush(color);

                    // Sprawdź czy to pierwsza pozycja w nowej grupie (trasie)
                    var rowIndex = _dtTransport.DefaultView.Cast<DataRowView>().ToList().IndexOf(rowView);
                    if (rowIndex > 0)
                    {
                        var prevRow = _dtTransport.DefaultView[rowIndex - 1];
                        var prevKursId = prevRow.Row.IsNull("KursId") ? 0L : prevRow.Row.Field<long>("KursId");

                        if (prevKursId != kursId && prevKursId != 0 && kursId != 0)
                        {
                            // Nowa grupa - dodaj grubszą górną linię
                            e.Row.BorderBrush = new SolidColorBrush(Color.FromRgb(44, 62, 80));
                            e.Row.BorderThickness = new Thickness(0, 3, 0, 0);
                        }
                        else
                        {
                            e.Row.BorderThickness = new Thickness(0);
                        }
                    }
                }
            }
        }

        private Color GetColorForRoute(long kursId)
        {
            if (kursId == 0) return Colors.White;

            if (!_routeColors.TryGetValue(kursId, out var color))
            {
                color = _colorPalette[_colorIndex % _colorPalette.Count];
                _routeColors[kursId] = color;
                _colorIndex++;
            }
            return color;
        }

        private void InitializeDate()
        {
            dpData.SelectedDate = _selectedDate;
            txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd}";
            _ = LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (_isLoading) return;
            _isLoading = true;
            txtStatus.Text = "Ładowanie...";

            try
            {
                _dtTransport.Rows.Clear();
                _routeColors.Clear();
                _colorIndex = 0;

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

                // Pobierz zamówienia TYLKO dla wybranej daty (wg DataUboju)
                var orders = new List<(int Id, int KlientId, decimal IloscZam, decimal IloscWyd, decimal Palety, long? KursId, DateTime DataUboju)>();
                await using (var cnLibra = new SqlConnection(_connLibra))
                {
                    await cnLibra.OpenAsync();
                    const string sql = @"SELECT Id, KlientId, TransportKursID, DataUboju
                                         FROM dbo.ZamowieniaMieso
                                         WHERE Status <> 'Anulowane'
                                           AND DataUboju = @SelectedDate
                                         ORDER BY DataUboju DESC";
                    await using var cmd = new SqlCommand(sql, cnLibra);
                    cmd.Parameters.AddWithValue("@SelectedDate", _selectedDate.Date);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        int klientId = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                        long? kursId = rdr.IsDBNull(2) ? null : rdr.GetInt64(2);
                        DateTime dataUboju = rdr.IsDBNull(3) ? DateTime.MinValue : rdr.GetDateTime(3);
                        orders.Add((id, klientId, 0m, 0m, 0m, kursId, dataUboju));
                    }
                }

                // Pobierz ilości zamówione per zamówienie
                var orderQuantities = new Dictionary<int, (decimal Zam, decimal Wyd, decimal Palety)>();
                if (orders.Any())
                {
                    var orderIds = string.Join(",", orders.Select(o => o.Id));
                    await using var cnLibra = new SqlConnection(_connLibra);
                    await cnLibra.OpenAsync();
                    var sql = $@"SELECT zmt.ZamowienieId, SUM(zmt.Ilosc) as Zam
                                 FROM [dbo].[ZamowieniaMiesoTowar] zmt
                                 WHERE zmt.ZamowienieId IN ({orderIds})
                                 GROUP BY zmt.ZamowienieId";
                    await using var cmd = new SqlCommand(sql, cnLibra);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int orderId = rdr.GetInt32(0);
                        decimal zam = rdr.IsDBNull(1) ? 0m : rdr.GetDecimal(1);
                        orderQuantities[orderId] = (zam, 0m, 0m);
                    }
                }

                // Pobierz dane transportu
                var transportDetails = new Dictionary<long, (string Kierowca, string Pojazd, string Trasa, TimeSpan? GodzWyjazdu)>();
                var kursIds = orders.Where(o => o.KursId.HasValue).Select(o => o.KursId!.Value).Distinct().ToList();
                if (kursIds.Any())
                {
                    try
                    {
                        await using var cnTransport = new SqlConnection(_connTransport);
                        await cnTransport.OpenAsync();
                        var kursIdsList = string.Join(",", kursIds);
                        var sqlKurs = $@"SELECT k.KursID, k.Trasa, k.GodzWyjazdu,
                                        CONCAT(ki.Imie, ' ', ki.Nazwisko) as Kierowca,
                                        p.Rejestracja
                                        FROM dbo.Kurs k
                                        LEFT JOIN dbo.Kierowca ki ON k.KierowcaID = ki.KierowcaID
                                        LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                                        WHERE k.KursID IN ({kursIdsList})";
                        await using var cmdKurs = new SqlCommand(sqlKurs, cnTransport);
                        await using var rdKurs = await cmdKurs.ExecuteReaderAsync();
                        while (await rdKurs.ReadAsync())
                        {
                            long kursId = rdKurs.GetInt64(0);
                            string trasa = rdKurs.IsDBNull(1) ? "" : rdKurs.GetString(1);
                            TimeSpan? godzWyjazdu = rdKurs.IsDBNull(2) ? null : rdKurs.GetTimeSpan(2);
                            string kierowca = rdKurs.IsDBNull(3) ? "" : rdKurs.GetString(3);
                            string pojazd = rdKurs.IsDBNull(4) ? "" : rdKurs.GetString(4);
                            transportDetails[kursId] = (kierowca, pojazd, trasa, godzWyjazdu);
                        }
                    }
                    catch { /* Ignoruj błędy transportu */ }
                }

                // Przypisz indeksy grup do sortowania
                var kursIdToGroupIndex = new Dictionary<long, int>();
                int groupIdx = 0;
                foreach (var kursId in kursIds.OrderBy(k =>
                    transportDetails.TryGetValue(k, out var td) ? td.GodzWyjazdu ?? TimeSpan.MaxValue : TimeSpan.MaxValue)
                    .ThenBy(k => transportDetails.TryGetValue(k, out var td) ? td.Trasa : ""))
                {
                    kursIdToGroupIndex[kursId] = groupIdx++;
                }

                // Buduj wiersze
                foreach (var order in orders)
                {
                    var (name, salesman) = contractors.TryGetValue(order.KlientId, out var c) ? c : ($"KH {order.KlientId}", "");
                    var (zam, wyd, palety) = orderQuantities.TryGetValue(order.Id, out var q) ? q : (0m, 0m, 0m);

                    string kierowca = "";
                    string pojazd = "";
                    string trasa = "";
                    string godzWyjazdu = "";
                    string status = "Brak";
                    int grupaIndex = int.MaxValue; // Bez transportu na końcu

                    if (order.KursId.HasValue && transportDetails.TryGetValue(order.KursId.Value, out var td))
                    {
                        kierowca = td.Kierowca;
                        pojazd = td.Pojazd;
                        trasa = td.Trasa;
                        godzWyjazdu = td.GodzWyjazdu?.ToString(@"hh\:mm") ?? "";
                        status = "Przypisany";
                        grupaIndex = kursIdToGroupIndex.TryGetValue(order.KursId.Value, out var gi) ? gi : int.MaxValue - 1;
                    }

                    _dtTransport.Rows.Add(
                        order.Id,
                        order.KlientId,
                        order.KursId ?? 0L,
                        order.DataUboju,
                        name,
                        salesman,
                        zam,
                        wyd,
                        palety,
                        kierowca,
                        pojazd,
                        godzWyjazdu,
                        trasa,
                        status,
                        grupaIndex);
                }

                // Sortuj wg grupy (trasy), potem godziny wyjazdu, potem odbiorcy
                _dtTransport.DefaultView.Sort = "GrupaIndex ASC, GodzWyjazdu ASC, Trasa ASC, Odbiorca ASC";

                // Aktualizuj statystyki
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                txtStatus.Text = "Gotowy";
            }
        }

        private void UpdateStatistics()
        {
            var view = _dtTransport.DefaultView;
            int przypisanych = 0;
            int bezTransportu = 0;
            decimal sumaKg = 0m;
            decimal sumaPalet = 0m;

            foreach (DataRowView row in view)
            {
                var status = row.Row.Field<string>("Status") ?? "";
                if (status == "Przypisany") przypisanych++;
                else bezTransportu++;

                if (!row.Row.IsNull("IloscZamowiona"))
                    sumaKg += Convert.ToDecimal(row["IloscZamowiona"]);
                if (!row.Row.IsNull("Palety"))
                    sumaPalet += Convert.ToDecimal(row["Palety"]);
            }

            txtPrzypisanych.Text = przypisanych.ToString();
            txtBezTransportu.Text = bezTransportu.ToString();
            txtSumaKg.Text = $"{sumaKg:N0}";
            txtLiczbaWierszy.Text = view.Count.ToString();
            txtSumaPalet.Text = $"{sumaPalet:N1}";
        }

        private void ApplyFilters()
        {
            var filters = new List<string>();

            // Filtr odbiorcy
            if (!string.IsNullOrWhiteSpace(txtFilterOdbiorca.Text))
            {
                filters.Add($"Odbiorca LIKE '%{txtFilterOdbiorca.Text.Replace("'", "''")}%'");
            }

            // Filtr statusu
            if (cmbFilterStatus.SelectedItem is ComboBoxItem item && item.Content.ToString() != "Wszystkie")
            {
                filters.Add($"Status = '{item.Content}'");
            }

            _dtTransport.DefaultView.RowFilter = filters.Any() ? string.Join(" AND ", filters) : "";
            UpdateStatistics();
        }

        private void DpData_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (dpData.SelectedDate.HasValue)
            {
                _selectedDate = dpData.SelectedDate.Value;
                txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd}";
                _ = LoadDataAsync();
            }
        }

        private void TxtFilterOdbiorca_TextChanged(object sender, TextChangedEventArgs e)
        {
            _filterDebounceTimer?.Stop();
            _filterDebounceTimer?.Start();
        }

        private void CmbFilterStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilters();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
