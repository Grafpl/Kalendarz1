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

                string sql = $@"
                    SELECT zm.Id, zm.KlientId,
                           SUM(ISNULL(zmt.Ilosc, 0)) AS IloscZamowiona,
                           SUM(ISNULL(CAST(zmt.Cena AS decimal(18,2)) * zmt.Ilosc, 0)) AS Wartosc,
                           zm.DataZamowienia, zm.DataUboju, zm.Status, zm.IdUser
                           {fakturaSelect}
                    FROM [dbo].[ZamowieniaMieso] zm
                    LEFT JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
                    WHERE zm.DataUboju = @Day
                      AND zm.Status <> 'Anulowane'
                    GROUP BY zm.Id, zm.KlientId, zm.DataZamowienia, zm.DataUboju, zm.Status, zm.IdUser
                             {(hasFakturaColumn ? ", zm.CzyZafakturowane, zm.NumerFaktury" : "")}
                    ORDER BY zm.Id";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Day", _selectedDate.Date);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    int clientId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    decimal ilosc = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                    decimal wartosc = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
                    DateTime? dataZam = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
                    DateTime? dataUboju = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
                    string status = reader.IsDBNull(6) ? "" : reader.GetString(6);
                    string idUser = reader.IsDBNull(7) ? "" : reader.GetString(7);
                    bool czyZafakturowane = !reader.IsDBNull(8) && reader.GetBoolean(8);
                    string numerFaktury = reader.IsDBNull(9) ? "" : reader.GetString(9);

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

                    _dtOrders.Rows.Add(row);
                }

                txtOrdersCount.Text = $"{_dtOrders.Rows.Count} zamówień";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania zamówień: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
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
                bool czyZafakturowane = rowView.Row.Field<bool>("CzyZafakturowane");
                if (czyZafakturowane)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(200, 255, 200));
                    e.Row.FontStyle = FontStyles.Italic;
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
        }

        private async void BtnCreateInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue) return;

            // TODO: Tutaj będzie integracja z Symfonią Handel
            MessageBox.Show(
                "Funkcja tworzenia faktury w Symfonii Handel będzie dostępna wkrótce.\n\n" +
                "Na razie możesz:\n" +
                "1. Przejrzeć szczegóły zamówienia w tym panelu\n" +
                "2. Ręcznie przepisać dane do Symfonii\n" +
                "3. Oznaczyć zamówienie jako zafakturowane",
                "Integracja z Symfonią",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            if (chkMarkAsFakturowane.IsChecked == true)
            {
                await MarkAsFakturowaneAsync(_currentOrderId.Value, "");
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
