using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.WPF
{
    public partial class PanelFakturWindow : Window
    {
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public string UserID { get; set; } = string.Empty;

        private DateTime _selectedDate;
        private int? _currentOrderId;
        private bool _showFakturowane = false;
        private readonly DataTable _dtOrders = new();
        private readonly DataTable _dtDetails = new();
        private readonly List<Button> _dayButtons = new();
        private readonly Dictionary<Button, DateTime> _dayButtonDates = new();
        private readonly Dictionary<int, (string Name, string Salesman)> _contractorsCache = new();

        public PanelFakturWindow()
        {
            InitializeComponent();
            Loaded += PanelFakturWindow_Loaded;
        }

        private async void PanelFakturWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _selectedDate = DateTime.Today;
            SetupDayButtons();
            await LoadContractorsCacheAsync();
            await RefreshDataAsync();
        }

        private void SetupDayButtons()
        {
            panelDays.Children.Clear();
            _dayButtons.Clear();
            _dayButtonDates.Clear();

            string[] dayNames = { "Pon", "Wt", "Śr", "Czw", "Pt", "Sob", "Nd" };

            // Przycisk Dziś
            var btnToday = new Button
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = "Dziś", FontSize = 10, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock { Text = DateTime.Today.ToString("dd.MM"), FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center }
                    }
                },
                Style = (Style)FindResource("DayButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(25, 118, 210)),
                Foreground = Brushes.White
            };
            btnToday.Click += (s, e) =>
            {
                _selectedDate = DateTime.Today;
                UpdateDayButtonDates();
                _ = RefreshDataAsync();
            };
            panelDays.Children.Add(btnToday);

            // Separator
            panelDays.Children.Add(new Separator { Width = 2, Margin = new Thickness(5, 0, 5, 0) });

            // Przyciski dni tygodnia
            for (int i = 0; i < 7; i++)
            {
                var btn = new Button { Style = (Style)FindResource("DayButtonStyle") };
                var stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = dayNames[i], FontSize = 9, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
                stack.Children.Add(new TextBlock { Text = DateTime.Today.AddDays(i).ToString("dd.MM"), FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center });
                btn.Content = stack;
                btn.Click += DayButton_Click;
                _dayButtonDates[btn] = DateTime.Today.AddDays(i);
                _dayButtons.Add(btn);
                panelDays.Children.Add(btn);
            }

            UpdateDayButtonDates();
        }

        private void UpdateDayButtonDates()
        {
            int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
            DateTime startOfWeek = _selectedDate.AddDays(-delta);

            string[] dayNames = { "Pon", "Wt", "Śr", "Czw", "Pt", "Sob", "Nd" };

            for (int i = 0; i < _dayButtons.Count; i++)
            {
                var date = startOfWeek.AddDays(i);
                _dayButtonDates[_dayButtons[i]] = date;

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text = dayNames[i],
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                stack.Children.Add(new TextBlock
                {
                    Text = date.ToString("dd.MM"),
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                _dayButtons[i].Content = stack;

                // Podświetl wybrany dzień
                if (date.Date == _selectedDate.Date)
                {
                    _dayButtons[i].Background = new SolidColorBrush(Color.FromRgb(25, 118, 210));
                    _dayButtons[i].Foreground = Brushes.White;
                }
                else if (date.Date == DateTime.Today)
                {
                    _dayButtons[i].Background = new SolidColorBrush(Color.FromRgb(200, 230, 255));
                    _dayButtons[i].Foreground = Brushes.Black;
                }
                else
                {
                    _dayButtons[i].Background = Brushes.White;
                    _dayButtons[i].Foreground = Brushes.Black;
                }
            }
        }

        private async void DayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && _dayButtonDates.TryGetValue(btn, out var date))
            {
                _selectedDate = date;
                UpdateDayButtonDates();
                await RefreshDataAsync();
            }
        }

        private void BtnPrevWeek_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(-7);
            UpdateDayButtonDates();
            _ = RefreshDataAsync();
        }

        private void BtnNextWeek_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(7);
            UpdateDayButtonDates();
            _ = RefreshDataAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshDataAsync();
        }

        private async Task LoadContractorsCacheAsync()
        {
            _contractorsCache.Clear();
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                const string sql = @"SELECT c.Id, c.Shortcut, wym.CDim_Handlowiec_Val
                                     FROM [HANDEL].[SSCommon].[STContractors] c
                                     LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId";
                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    string shortcut = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    string salesman = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    _contractorsCache[id] = (shortcut, salesman);
                }
            }
            catch { }
        }

        private async Task RefreshDataAsync()
        {
            await LoadOrdersAsync();
            SetupOrdersDataGrid();
            ApplyFilters();
            ClearDetails();
        }

        private async Task LoadOrdersAsync()
        {
            // Wyczyść RowFilter przed modyfikacją kolumn
            if (_dtOrders.DefaultView != null)
            {
                _dtOrders.DefaultView.RowFilter = "";
            }

            _dtOrders.Clear();
            _dtOrders.Columns.Clear();

            _dtOrders.Columns.Add("Id", typeof(int));
            _dtOrders.Columns.Add("KlientId", typeof(int));
            _dtOrders.Columns.Add("Odbiorca", typeof(string));
            _dtOrders.Columns.Add("Handlowiec", typeof(string));
            _dtOrders.Columns.Add("IloscZamowiona", typeof(decimal));
            _dtOrders.Columns.Add("Wartosc", typeof(decimal));
            _dtOrders.Columns.Add("DataZamowienia", typeof(DateTime));
            _dtOrders.Columns.Add("DataUboju", typeof(DateTime));
            _dtOrders.Columns.Add("Status", typeof(string));
            _dtOrders.Columns.Add("CzyZafakturowane", typeof(bool));
            _dtOrders.Columns.Add("NumerFaktury", typeof(string));
            _dtOrders.Columns.Add("UtworzonePrzez", typeof(string));
            _dtOrders.Columns.Add("TransportKursID", typeof(long));
            _dtOrders.Columns.Add("GodzWyjazdu", typeof(string));
            _dtOrders.Columns.Add("Kierowca", typeof(string));
            _dtOrders.Columns.Add("Pojazd", typeof(string));

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Sprawdź czy kolumna CzyZafakturowane istnieje
                bool hasFakturaColumn = false;
                try
                {
                    await using var checkCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'CzyZafakturowane'", cn);
                    hasFakturaColumn = (int)await checkCmd.ExecuteScalarAsync()! > 0;
                }
                catch { }

                string fakturaSelect = hasFakturaColumn ? ", ISNULL(zm.CzyZafakturowane, 0) AS CzyZafakturowane, zm.NumerFaktury" : ", 0 AS CzyZafakturowane, NULL AS NumerFaktury";

                // Sprawdź czy kolumna TransportKursID istnieje
                bool hasTransportColumn = false;
                try
                {
                    await using var checkTransportCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'TransportKursID'", cn);
                    hasTransportColumn = (int)await checkTransportCmd.ExecuteScalarAsync()! > 0;
                }
                catch { }

                string transportSelect = hasTransportColumn ? ", zm.TransportKursID" : ", NULL AS TransportKursID";
                string transportGroupBy = hasTransportColumn ? ", zm.TransportKursID" : "";

                string sql = $@"
                    SELECT zm.Id, zm.KlientId,
                           SUM(ISNULL(zmt.Ilosc, 0)) AS IloscZamowiona,
                           SUM(ISNULL(CAST(zmt.Cena AS decimal(18,2)) * zmt.Ilosc, 0)) AS Wartosc,
                           zm.DataZamowienia, zm.DataUboju, zm.Status, zm.IdUser
                           {fakturaSelect}
                           {transportSelect}
                    FROM [dbo].[ZamowieniaMieso] zm
                    LEFT JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
                    WHERE zm.DataUboju = @Day
                      AND zm.Status <> 'Anulowane'
                    GROUP BY zm.Id, zm.KlientId, zm.DataZamowienia, zm.DataUboju, zm.Status, zm.IdUser
                             {(hasFakturaColumn ? ", zm.CzyZafakturowane, zm.NumerFaktury" : "")}
                             {transportGroupBy}
                    ORDER BY zm.Id";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Day", _selectedDate.Date);

                await using var reader = await cmd.ExecuteReaderAsync();
                var kursIds = new HashSet<long>();

                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    int clientId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    decimal ilosc = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                    decimal wartosc = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
                    DateTime? dataZam = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
                    DateTime? dataUboju = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
                    string status = reader.IsDBNull(6) ? "" : reader.GetString(6);
                    string idUser = reader.IsDBNull(7) ? "" : reader.GetValue(7).ToString() ?? "";
                    bool czyZafakturowane = !reader.IsDBNull(8) && Convert.ToBoolean(reader.GetValue(8));
                    string numerFaktury = reader.IsDBNull(9) ? "" : reader.GetValue(9).ToString() ?? "";
                    long? transportKursId = reader.IsDBNull(10) ? null : Convert.ToInt64(reader.GetValue(10));

                    var (name, salesman) = _contractorsCache.TryGetValue(clientId, out var c) ? c : ($"Klient {clientId}", "");

                    var row = _dtOrders.NewRow();
                    row["Id"] = id;
                    row["KlientId"] = clientId;
                    row["Odbiorca"] = name;
                    row["Handlowiec"] = salesman;
                    row["IloscZamowiona"] = ilosc;
                    row["Wartosc"] = wartosc;
                    row["DataZamowienia"] = dataZam ?? DateTime.MinValue;
                    row["DataUboju"] = dataUboju ?? DateTime.MinValue;
                    row["Status"] = czyZafakturowane ? "Zafakturowane" : status;
                    row["CzyZafakturowane"] = czyZafakturowane;
                    row["NumerFaktury"] = numerFaktury;
                    row["UtworzonePrzez"] = idUser;

                    if (transportKursId.HasValue)
                    {
                        row["TransportKursID"] = transportKursId.Value;
                        kursIds.Add(transportKursId.Value);
                    }

                    _dtOrders.Rows.Add(row);
                }

                // Pobierz informacje o transporcie z TransportPL
                if (kursIds.Count > 0)
                {
                    await LoadTransportInfoAsync(kursIds);
                }

                txtOrdersCount.Text = $"{_dtOrders.Rows.Count} zamówień";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania zamówień: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadTransportInfoAsync(HashSet<long> kursIds)
        {
            if (kursIds.Count == 0) return;

            // Polskie nazwy miesięcy (skrócone)
            string[] polskieMiesiace = { "", "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" };

            try
            {
                await using var cn = new SqlConnection(_connTransport);
                await cn.OpenAsync();

                // Pobierz dane kursów z kierowcami i pojazdami
                var kursIdsList = string.Join(",", kursIds);
                string sql = $@"
                    SELECT k.KursID, k.GodzWyjazdu, k.DataKursu,
                           ISNULL(kier.Imie + ' ' + kier.Nazwisko, '') AS Kierowca,
                           ISNULL(p.Rejestracja, '') AS Pojazd
                    FROM dbo.Kurs k
                    LEFT JOIN dbo.Kierowca kier ON k.KierowcaID = kier.KierowcaID
                    LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                    WHERE k.KursID IN ({kursIdsList})";

                var transportInfo = new Dictionary<long, (string GodzWyjazdu, string Kierowca, string Pojazd)>();

                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    long kursId = reader.GetInt64(0);
                    TimeSpan? godzWyjazdu = reader.IsDBNull(1) ? null : reader.GetTimeSpan(1);
                    DateTime? dataKursu = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
                    string kierowca = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    string pojazd = reader.IsDBNull(4) ? "" : reader.GetString(4);

                    // Format: "08:30 Sty 12"
                    string godzWyjazduStr = "";
                    if (godzWyjazdu.HasValue && dataKursu.HasValue)
                    {
                        string miesiac = polskieMiesiace[dataKursu.Value.Month];
                        godzWyjazduStr = $"{godzWyjazdu.Value:hh\\:mm} {miesiac} {dataKursu.Value.Day}";
                    }
                    else if (godzWyjazdu.HasValue)
                    {
                        godzWyjazduStr = godzWyjazdu.Value.ToString(@"hh\:mm");
                    }

                    transportInfo[kursId] = (godzWyjazduStr, kierowca, pojazd);
                }

                // Zaktualizuj wiersze w DataTable
                foreach (DataRow row in _dtOrders.Rows)
                {
                    if (row["TransportKursID"] != DBNull.Value)
                    {
                        long kursId = Convert.ToInt64(row["TransportKursID"]);
                        if (transportInfo.TryGetValue(kursId, out var info))
                        {
                            row["GodzWyjazdu"] = info.GodzWyjazdu;
                            row["Kierowca"] = info.Kierowca;
                            row["Pojazd"] = info.Pojazd;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania informacji o transporcie: {ex.Message}");
            }
        }

        private void SetupOrdersDataGrid()
        {
            dgOrders.ItemsSource = _dtOrders.DefaultView;
            dgOrders.Columns.Clear();

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Odbiorca",
                Binding = new System.Windows.Data.Binding("Odbiorca"),
                Width = new DataGridLength(180)
            });

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Handlowiec",
                Binding = new System.Windows.Data.Binding("Handlowiec"),
                Width = new DataGridLength(80)
            });

            var iloscStyle = new Style(typeof(TextBlock));
            iloscStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            iloscStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Ilość [kg]",
                Binding = new System.Windows.Data.Binding("IloscZamowiona") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = iloscStyle
            });

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Wartość [PLN]",
                Binding = new System.Windows.Data.Binding("Wartosc") { StringFormat = "N2" },
                Width = new DataGridLength(100),
                ElementStyle = iloscStyle
            });

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Status",
                Binding = new System.Windows.Data.Binding("Status"),
                Width = new DataGridLength(100)
            });

            // Kolumny transportowe
            var centerStyle = new Style(typeof(TextBlock));
            centerStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Wyjazd",
                Binding = new System.Windows.Data.Binding("GodzWyjazdu"),
                Width = new DataGridLength(95),
                ElementStyle = centerStyle
            });

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Kierowca",
                Binding = new System.Windows.Data.Binding("Kierowca"),
                Width = new DataGridLength(100)
            });

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Pojazd",
                Binding = new System.Windows.Data.Binding("Pojazd"),
                Width = new DataGridLength(80)
            });

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Nr faktury",
                Binding = new System.Windows.Data.Binding("NumerFaktury"),
                Width = new DataGridLength(100)
            });

            dgOrders.LoadingRow += DgOrders_LoadingRow;
        }

        private void DgOrders_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                string status = rowView.Row.Field<string>("Status") ?? "";
                bool czyZafakturowane = rowView.Row.Field<bool>("CzyZafakturowane");

                // Kolorowanie według statusu
                if (czyZafakturowane || status == "Zafakturowane")
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233)); // Jasno zielony
                    e.Row.FontStyle = FontStyles.Italic;
                }
                else if (status == "Zrealizowane")
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(241, 248, 233)); // Bardzo jasno zielony
                }
                else if (status == "W realizacji")
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)); // Jasno niebieski
                }
                else if (status == "Nowe")
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 248, 225)); // Jasno żółty/pomarańczowy
                }
            }
        }

        private void ApplyFilters()
        {
            var search = txtSearch?.Text?.Trim().Replace("'", "''") ?? "";
            var conditions = new List<string>();

            if (!string.IsNullOrEmpty(search))
                conditions.Add($"Odbiorca LIKE '%{search}%'");

            if (!_showFakturowane)
                conditions.Add("CzyZafakturowane = False");

            _dtOrders.DefaultView.RowFilter = conditions.Count > 0 ? string.Join(" AND ", conditions) : "";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ChkShowFakturowane_Changed(object sender, RoutedEventArgs e)
        {
            _showFakturowane = chkShowFakturowane.IsChecked == true;
            ApplyFilters();
        }

        private async void DgOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgOrders.SelectedItem is DataRowView rowView)
            {
                int id = rowView.Row.Field<int>("Id");
                if (id > 0)
                {
                    _currentOrderId = id;
                    await LoadOrderDetailsAsync(id);

                    bool czyZafakturowane = rowView.Row.Field<bool>("CzyZafakturowane");
                    btnCreateInvoice.IsEnabled = !czyZafakturowane;
                    btnMarkFakturowane.IsEnabled = !czyZafakturowane;

                    if (czyZafakturowane)
                    {
                        string nrFaktury = rowView.Row.Field<string>("NumerFaktury") ?? "";
                        txtInvoiceStatus.Text = $"To zamówienie zostało już zafakturowane.\nNr faktury: {nrFaktury}";
                    }
                    else
                    {
                        txtInvoiceStatus.Text = "Kliknij aby utworzyć fakturę w Symfonii Handel";
                    }
                    return;
                }
            }
            ClearDetails();
        }

        private void DgOrders_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Podwójne kliknięcie - otwórz szczegóły zamówienia
            if (_currentOrderId.HasValue)
            {
                var widokZamowienia = new Kalendarz1.WidokZamowienia(UserID, _currentOrderId.Value);
                widokZamowienia.ShowDialog();
            }
        }

        private async Task LoadOrderDetailsAsync(int orderId)
        {
            _dtDetails.Clear();
            _dtDetails.Columns.Clear();

            _dtDetails.Columns.Add("Produkt", typeof(string));
            _dtDetails.Columns.Add("Ilosc", typeof(decimal));
            _dtDetails.Columns.Add("Cena", typeof(string));
            _dtDetails.Columns.Add("Wartosc", typeof(decimal));

            try
            {
                // Pobierz info o odbiorcy
                var orderRow = _dtOrders.AsEnumerable().FirstOrDefault(r => r.Field<int>("Id") == orderId);
                if (orderRow != null)
                {
                    txtOdbiorca.Text = orderRow.Field<string>("Odbiorca") ?? "";
                    txtHandlowiec.Text = $"Handlowiec: {orderRow.Field<string>("Handlowiec") ?? "brak"}";
                    var dataZam = orderRow.Field<DateTime>("DataZamowienia");
                    txtDataZamowienia.Text = dataZam > DateTime.MinValue ? $"Data zamówienia: {dataZam:dd.MM.yyyy}" : "";

                    // Wyświetl informacje o transporcie
                    string godzWyjazdu = orderRow.Field<string>("GodzWyjazdu") ?? "";
                    string kierowca = orderRow.Field<string>("Kierowca") ?? "";
                    string pojazd = orderRow.Field<string>("Pojazd") ?? "";

                    if (!string.IsNullOrEmpty(godzWyjazdu) || !string.IsNullOrEmpty(kierowca) || !string.IsNullOrEmpty(pojazd))
                    {
                        borderTransport.Visibility = Visibility.Visible;
                        txtGodzWyjazdu.Text = godzWyjazdu;
                        txtKierowca.Text = kierowca;
                        txtPojazd.Text = pojazd;
                    }
                    else
                    {
                        borderTransport.Visibility = Visibility.Collapsed;
                    }
                }

                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Pobierz produkty z zamówienia
                var productNames = new Dictionary<int, string>();
                await using (var cnHandel = new SqlConnection(_connHandel))
                {
                    await cnHandel.OpenAsync();
                    await using var cmdProd = new SqlCommand("SELECT ID, nazwa FROM [HANDEL].[HM].[TW]", cnHandel);
                    await using var readerProd = await cmdProd.ExecuteReaderAsync();
                    while (await readerProd.ReadAsync())
                    {
                        productNames[readerProd.GetInt32(0)] = readerProd.IsDBNull(1) ? "" : readerProd.GetString(1);
                    }
                }

                string sql = @"SELECT KodTowaru, Ilosc, Cena FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = @OrderId";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@OrderId", orderId);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int kodTowaru = reader.GetInt32(0);
                    decimal ilosc = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                    string cena = reader.IsDBNull(2) ? "" : reader.GetString(2);

                    decimal cenaDecimal = 0;
                    decimal.TryParse(cena.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out cenaDecimal);

                    string produktNazwa = productNames.TryGetValue(kodTowaru, out var name) ? name : $"Produkt {kodTowaru}";

                    var row = _dtDetails.NewRow();
                    row["Produkt"] = produktNazwa;
                    row["Ilosc"] = ilosc;
                    row["Cena"] = cena;
                    row["Wartosc"] = ilosc * cenaDecimal;
                    _dtDetails.Rows.Add(row);
                }

                SetupDetailsDataGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania szczegółów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupDetailsDataGrid()
        {
            dgDetails.ItemsSource = _dtDetails.DefaultView;
            dgDetails.Columns.Clear();

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Produkt",
                Binding = new System.Windows.Data.Binding("Produkt"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            var rightStyle = new Style(typeof(TextBlock));
            rightStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Ilość",
                Binding = new System.Windows.Data.Binding("Ilosc") { StringFormat = "N2" },
                Width = new DataGridLength(70),
                ElementStyle = rightStyle
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Cena",
                Binding = new System.Windows.Data.Binding("Cena"),
                Width = new DataGridLength(70),
                ElementStyle = rightStyle
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Wartość",
                Binding = new System.Windows.Data.Binding("Wartosc") { StringFormat = "N2" },
                Width = new DataGridLength(80),
                ElementStyle = rightStyle
            });
        }

        private void ClearDetails()
        {
            _currentOrderId = null;
            txtOdbiorca.Text = "Wybierz zamówienie...";
            txtHandlowiec.Text = "";
            txtDataZamowienia.Text = "";
            _dtDetails.Clear();
            dgDetails.ItemsSource = null;
            btnCreateInvoice.IsEnabled = false;
            btnMarkFakturowane.IsEnabled = false;
            txtInvoiceStatus.Text = "Wybierz zamówienie aby utworzyć fakturę";

            // Ukryj panel transportu
            borderTransport.Visibility = Visibility.Collapsed;
            txtGodzWyjazdu.Text = "";
            txtKierowca.Text = "";
            txtPojazd.Text = "";
        }

        private async void BtnCreateInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue) return;

            try
            {
                btnCreateInvoice.IsEnabled = false;
                txtInvoiceStatus.Text = "Przygotowywanie danych...";

                // Pobierz dane zamówienia
                var orderRow = _dtOrders.AsEnumerable().FirstOrDefault(r => r.Field<int>("Id") == _currentOrderId.Value);
                if (orderRow == null)
                {
                    MessageBox.Show("Nie znaleziono zamówienia.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int klientId = orderRow.Field<int>("KlientId");
                string odbiorcaNazwa = orderRow.Field<string>("Odbiorca") ?? "";

                // Pobierz pozycje zamówienia z LibraNet
                var pozycje = new List<(int KodTowaru, decimal Ilosc, decimal Cena, string NazwaTowaru)>();
                await using (var cnLibra = new SqlConnection(_connLibra))
                {
                    await cnLibra.OpenAsync();
                    string sql = @"SELECT KodTowaru, Ilosc, Cena FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = @OrderId";
                    await using var cmd = new SqlCommand(sql, cnLibra);
                    cmd.Parameters.AddWithValue("@OrderId", _currentOrderId.Value);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int kodTowaru = reader.GetInt32(0);
                        decimal ilosc = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                        string cenaStr = reader.IsDBNull(2) ? "0" : reader.GetString(2);
                        decimal.TryParse(cenaStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal cena);
                        pozycje.Add((kodTowaru, ilosc, cena, ""));
                    }
                }

                if (pozycje.Count == 0)
                {
                    MessageBox.Show("Zamówienie nie ma pozycji do zaimportowania.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Sprawdź dopasowanie towarów i kontrahenta w Symfonii
                txtInvoiceStatus.Text = "Sprawdzanie danych w Symfonii...";

                var (kontrahentSymfoniaId, kontrahentNazwa) = await SprawdzKontrahentaWSymfoniiAsync(klientId);
                var towarySymfonia = await SprawdzTowaryWSymfoniiAsync(pozycje.Select(p => p.KodTowaru).ToList());

                // Przygotuj podgląd
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"=== PODGLĄD IMPORTU DO SYMFONII ===\n");
                sb.AppendLine($"Odbiorca: {odbiorcaNazwa}");
                sb.AppendLine($"ID w Symfonii: {(kontrahentSymfoniaId > 0 ? kontrahentSymfoniaId.ToString() : "NIE ZNALEZIONO!")}");
                sb.AppendLine($"\nPozycje ({pozycje.Count}):");
                sb.AppendLine("─────────────────────────────────────");

                decimal sumaWartosci = 0;
                int nieznalezione = 0;
                foreach (var poz in pozycje)
                {
                    bool znaleziony = towarySymfonia.TryGetValue(poz.KodTowaru, out var towarInfo);
                    string status = znaleziony ? "✓" : "✗";
                    string nazwa = znaleziony ? towarInfo.Nazwa : $"[Kod: {poz.KodTowaru}]";
                    decimal wartosc = poz.Ilosc * poz.Cena;
                    sumaWartosci += wartosc;

                    sb.AppendLine($"{status} {nazwa}");
                    sb.AppendLine($"   {poz.Ilosc:N2} kg × {poz.Cena:N2} zł = {wartosc:N2} zł");

                    if (!znaleziony) nieznalezione++;
                }

                sb.AppendLine("─────────────────────────────────────");
                sb.AppendLine($"SUMA: {sumaWartosci:N2} zł");

                if (kontrahentSymfoniaId <= 0 || nieznalezione > 0)
                {
                    sb.AppendLine($"\n⚠️ UWAGA: ");
                    if (kontrahentSymfoniaId <= 0)
                        sb.AppendLine("• Kontrahent nie został znaleziony w Symfonii");
                    if (nieznalezione > 0)
                        sb.AppendLine($"• {nieznalezione} towar(ów) nie ma w Symfonii");
                    sb.AppendLine("\nImport może być niekompletny!");
                }

                var result = MessageBox.Show(
                    sb.ToString() + "\n\nCzy utworzyć dokument WZ w buforze Symfonii?",
                    "Podgląd importu do Symfonii Handel",
                    MessageBoxButton.YesNo,
                    nieznalezione > 0 || kontrahentSymfoniaId <= 0 ? MessageBoxImage.Warning : MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (kontrahentSymfoniaId <= 0)
                    {
                        MessageBox.Show("Nie można utworzyć dokumentu bez kontrahenta w Symfonii.\nDodaj kontrahenta do Symfonii i spróbuj ponownie.",
                            "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    txtInvoiceStatus.Text = "Tworzenie dokumentu w Symfonii...";

                    var (sukces, numerDokumentu, komunikat) = await UtworzDokumentWSymfoniiAsync(
                        kontrahentSymfoniaId,
                        pozycje.Where(p => towarySymfonia.ContainsKey(p.KodTowaru)).ToList(),
                        towarySymfonia);

                    if (sukces)
                    {
                        MessageBox.Show(
                            $"Dokument został utworzony w buforze Symfonii!\n\nNumer: {numerDokumentu}\n\nSprawdź dokument w Symfonii Handel i zatwierdź go.",
                            "Sukces",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        if (chkMarkAsFakturowane.IsChecked == true)
                        {
                            await MarkAsFakturowaneAsync(_currentOrderId.Value, numerDokumentu);
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Błąd podczas tworzenia dokumentu:\n{komunikat}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas importu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnCreateInvoice.IsEnabled = true;
                txtInvoiceStatus.Text = "Kliknij aby utworzyć dokument w Symfonii Handel";
            }
        }

        private async Task<(int Id, string Nazwa)> SprawdzKontrahentaWSymfoniiAsync(int klientIdLibra)
        {
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                // Szukaj kontrahenta po ID (zakładając że ID w LibraNet = ID w Symfonii)
                string sql = @"SELECT Id, ISNULL(Shortcut, Name) AS Nazwa FROM [HANDEL].[SSCommon].[STContractors] WHERE Id = @Id";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", klientIdLibra);
                await using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return (reader.GetInt32(0), reader.IsDBNull(1) ? "" : reader.GetString(1));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd sprawdzania kontrahenta: {ex.Message}");
            }
            return (0, "");
        }

        private async Task<Dictionary<int, (string Nazwa, string Kod, int Magazyn)>> SprawdzTowaryWSymfoniiAsync(List<int> kodyTowarow)
        {
            var result = new Dictionary<int, (string Nazwa, string Kod, int Magazyn)>();
            if (kodyTowarow.Count == 0) return result;

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                string ids = string.Join(",", kodyTowarow);
                string sql = $@"SELECT ID, ISNULL(nazwa, '') AS Nazwa, ISNULL(kod, '') AS Kod, ISNULL(magazyn, 0) AS Magazyn
                               FROM [HANDEL].[HM].[TW] WHERE ID IN ({ids})";
                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    string nazwa = reader.GetString(1);
                    string kod = reader.GetString(2);
                    int magazyn = reader.GetInt32(3);
                    result[id] = (nazwa, kod, magazyn);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd sprawdzania towarów: {ex.Message}");
            }
            return result;
        }

        private async Task<(bool Sukces, string NumerDokumentu, string Komunikat)> UtworzDokumentWSymfoniiAsync(
            int kontrahentId,
            List<(int KodTowaru, decimal Ilosc, decimal Cena, string NazwaTowaru)> pozycje,
            Dictionary<int, (string Nazwa, string Kod, int Magazyn)> towarySymfonia)
        {
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var transaction = cn.BeginTransaction();

                try
                {
                    // Pobierz kolejny numer dokumentu dla serii WZ
                    string sqlNumer = @"SELECT ISNULL(MAX(numer), 0) + 1 FROM [HANDEL].[HM].[DK] WHERE seria = 'sWZ' AND YEAR(data) = YEAR(GETDATE())";
                    await using var cmdNumer = new SqlCommand(sqlNumer, cn, transaction);
                    int nowyNumer = Convert.ToInt32(await cmdNumer.ExecuteScalarAsync());

                    string numerDokumentu = $"WZ/{nowyNumer}/{DateTime.Now:yy}";

                    // Oblicz wartości
                    decimal wartoscNetto = pozycje.Sum(p => p.Ilosc * p.Cena);
                    decimal wartoscVat = wartoscNetto * 0.05m; // Stawka 5% dla mięsa
                    decimal wartoscBrutto = wartoscNetto + wartoscVat;

                    // Utwórz nagłówek dokumentu WZ
                    string sqlDK = @"
                        INSERT INTO [HANDEL].[HM].[DK]
                        (seria, numer, data, khid, wartnetto, wartbrutto, aktywny, datadod, uwagi)
                        OUTPUT INSERTED.id
                        VALUES
                        ('sWZ', @Numer, @Data, @KhId, @WartNetto, @WartBrutto, 1, GETDATE(), @Uwagi)";

                    await using var cmdDK = new SqlCommand(sqlDK, cn, transaction);
                    cmdDK.Parameters.AddWithValue("@Numer", nowyNumer);
                    cmdDK.Parameters.AddWithValue("@Data", DateTime.Today);
                    cmdDK.Parameters.AddWithValue("@KhId", kontrahentId);
                    cmdDK.Parameters.AddWithValue("@WartNetto", wartoscNetto);
                    cmdDK.Parameters.AddWithValue("@WartBrutto", wartoscBrutto);
                    cmdDK.Parameters.AddWithValue("@Uwagi", $"Import z Panelu Faktur - Zamówienie #{_currentOrderId}");

                    int dokumentId = Convert.ToInt32(await cmdDK.ExecuteScalarAsync());

                    // Utwórz pozycje dokumentu
                    int lp = 1;
                    foreach (var poz in pozycje)
                    {
                        if (!towarySymfonia.TryGetValue(poz.KodTowaru, out var towarInfo)) continue;

                        decimal wartoscPoz = poz.Ilosc * poz.Cena;

                        string sqlDP = @"
                            INSERT INTO [HANDEL].[HM].[DP]
                            (super, lp, idtw, kod, ilosc, cena, wartnetto, data)
                            VALUES
                            (@Super, @Lp, @IdTw, @Kod, @Ilosc, @Cena, @WartNetto, @Data)";

                        await using var cmdDP = new SqlCommand(sqlDP, cn, transaction);
                        cmdDP.Parameters.AddWithValue("@Super", dokumentId);
                        cmdDP.Parameters.AddWithValue("@Lp", lp++);
                        cmdDP.Parameters.AddWithValue("@IdTw", poz.KodTowaru);
                        cmdDP.Parameters.AddWithValue("@Kod", towarInfo.Kod);
                        cmdDP.Parameters.AddWithValue("@Ilosc", poz.Ilosc);
                        cmdDP.Parameters.AddWithValue("@Cena", poz.Cena);
                        cmdDP.Parameters.AddWithValue("@WartNetto", wartoscPoz);
                        cmdDP.Parameters.AddWithValue("@Data", DateTime.Today);

                        await cmdDP.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();
                    return (true, numerDokumentu, "");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return (false, "", ex.Message);
                }
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }

        private async void BtnMarkFakturowane_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue) return;

            var result = MessageBox.Show(
                "Czy na pewno chcesz oznaczyć to zamówienie jako zafakturowane?",
                "Potwierdzenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await MarkAsFakturowaneAsync(_currentOrderId.Value, "");
            }
        }

        private async Task MarkAsFakturowaneAsync(int orderId, string numerFaktury)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Najpierw upewnij się że kolumny istnieją
                await EnsureFakturaColumnsExistAsync(cn);

                string sql = "UPDATE [dbo].[ZamowieniaMieso] SET CzyZafakturowane = 1, NumerFaktury = @NrFaktury WHERE Id = @Id";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", orderId);
                cmd.Parameters.AddWithValue("@NrFaktury", string.IsNullOrEmpty(numerFaktury) ? DBNull.Value : numerFaktury);
                await cmd.ExecuteNonQueryAsync();

                MessageBox.Show("Zamówienie zostało oznaczone jako zafakturowane.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas oznaczania jako zafakturowane: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task EnsureFakturaColumnsExistAsync(SqlConnection cn)
        {
            try
            {
                // Sprawdź i dodaj kolumnę CzyZafakturowane
                await using var checkCmd1 = new SqlCommand(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'CzyZafakturowane') " +
                    "ALTER TABLE [dbo].[ZamowieniaMieso] ADD CzyZafakturowane BIT DEFAULT 0", cn);
                await checkCmd1.ExecuteNonQueryAsync();

                // Sprawdź i dodaj kolumnę NumerFaktury
                await using var checkCmd2 = new SqlCommand(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'NumerFaktury') " +
                    "ALTER TABLE [dbo].[ZamowieniaMieso] ADD NumerFaktury NVARCHAR(50) NULL", cn);
                await checkCmd2.ExecuteNonQueryAsync();
            }
            catch { }
        }
    }
}
