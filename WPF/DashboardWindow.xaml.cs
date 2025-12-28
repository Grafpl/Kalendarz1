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
    public partial class DashboardWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private readonly DataTable _dtDashboard = new();
        private DateTime _selectedDate;
        private bool _isLoading;

        public DashboardWindow(string connLibra, string connHandel, DateTime? initialDate = null)
        {
            InitializeComponent();
            _connLibra = connLibra;
            _connHandel = connHandel;
            _selectedDate = initialDate ?? DateTime.Today;

            InitializeDataTable();
            InitializeDate();
        }

        private void InitializeDataTable()
        {
            _dtDashboard.Columns.Add("Produkt", typeof(string));
            _dtDashboard.Columns.Add("Zamowione", typeof(decimal));
            _dtDashboard.Columns.Add("LiczbaZamowien", typeof(int));
            _dtDashboard.Columns.Add("Status", typeof(string));

            dgDashboardProdukty.ItemsSource = _dtDashboard.DefaultView;
            SetupDataGrid();
        }

        private void SetupDataGrid()
        {
            dgDashboardProdukty.Columns.Clear();

            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Produkt",
                Binding = new Binding("Produkt"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 200
            });

            var zamowioneStyle = new Style(typeof(TextBlock));
            zamowioneStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            zamowioneStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
            zamowioneStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 10, 0)));

            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Zamówione (kg)",
                Binding = new Binding("Zamowione") { StringFormat = "N0" },
                Width = new DataGridLength(120),
                ElementStyle = zamowioneStyle
            });

            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Liczba zam.",
                Binding = new Binding("LiczbaZamowien"),
                Width = new DataGridLength(90),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Status",
                Binding = new Binding("Status"),
                Width = new DataGridLength(80),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            dgDashboardProdukty.LoadingRow += DgDashboardProdukty_LoadingRow;
        }

        private void DgDashboardProdukty_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                var zamowione = rowView.Row.Field<decimal>("Zamowione");

                if (zamowione > 500)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(230, 255, 230));
                }
                else if (zamowione > 100)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 250, 205));
                }
                else
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 240, 240));
                }
            }
        }

        private void InitializeDate()
        {
            dpData.SelectedDate = _selectedDate;
            txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd dddd}";
            _ = LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                _dtDashboard.Rows.Clear();

                // Pobierz nazwy produktów z Handel
                var productNames = new Dictionary<int, string>();
                try
                {
                    await using var cnHandel = new SqlConnection(_connHandel);
                    await cnHandel.OpenAsync();
                    const string sqlProducts = "SELECT ID, kod FROM [HANDEL].[HM].[TW]";
                    await using var cmdProducts = new SqlCommand(sqlProducts, cnHandel);
                    await using var rdrProducts = await cmdProducts.ExecuteReaderAsync();
                    while (await rdrProducts.ReadAsync())
                    {
                        if (!rdrProducts.IsDBNull(0))
                        {
                            int id = rdrProducts.GetInt32(0);
                            string kod = rdrProducts.IsDBNull(1) ? $"ID:{id}" : Convert.ToString(rdrProducts.GetValue(1)) ?? $"ID:{id}";
                            productNames[id] = kod;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Błąd ładowania produktów: {ex.Message}");
                }

                // Pobierz zamówienia z dnia
                var orderIds = new List<int>();
                int zamowienAktywnych = 0;
                int zamowienAnulowanych = 0;

                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT Id, Status FROM dbo.ZamowieniaMieso WHERE DataPrzyjazdu = @Day";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", _selectedDate.Date);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        string status = rdr.IsDBNull(1) ? "" : rdr.GetString(1);

                        if (status == "Anulowane")
                        {
                            zamowienAnulowanych++;
                        }
                        else
                        {
                            orderIds.Add(id);
                            zamowienAktywnych++;
                        }
                    }
                }

                // Podsumowanie produktów z zamówień
                var productSummary = new Dictionary<int, (decimal ilosc, int liczbaZamowien)>();

                if (orderIds.Any())
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    var sql = $@"SELECT KodTowaru, SUM(Ilosc) as Suma, COUNT(DISTINCT ZamowienieId) as LiczbaZam
                                 FROM [dbo].[ZamowieniaMiesoTowar]
                                 WHERE ZamowienieId IN ({string.Join(",", orderIds)})
                                 GROUP BY KodTowaru
                                 ORDER BY SUM(Ilosc) DESC";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int kodTowaru = reader.GetInt32(0);
                        decimal suma = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                        int liczbaZam = reader.GetInt32(2);
                        productSummary[kodTowaru] = (suma, liczbaZam);
                    }
                }

                // Dodaj do tabeli
                decimal totalZamowione = 0m;
                int totalPozycji = 0;

                foreach (var kvp in productSummary.OrderByDescending(x => x.Value.ilosc))
                {
                    string productName = productNames.TryGetValue(kvp.Key, out var name) ? name : $"Produkt {kvp.Key}";
                    decimal ilosc = kvp.Value.ilosc;
                    int liczbaZam = kvp.Value.liczbaZamowien;

                    string status = ilosc > 500 ? "Dużo" : (ilosc > 100 ? "Średnio" : "Mało");

                    _dtDashboard.Rows.Add(productName, ilosc, liczbaZam, status);

                    totalZamowione += ilosc;
                    totalPozycji++;
                }

                // Aktualizuj KPI w nagłówku
                txtBilansOk.Text = zamowienAktywnych.ToString();
                txtBilansUwaga.Text = totalPozycji.ToString();
                txtBilansBrak.Text = zamowienAnulowanych.ToString();
                txtSumaZamowien.Text = $"{totalZamowione:N0} kg";
                txtPlanProdukcji.Text = $"{orderIds.Count} zamówień";

                // Aktualizuj karty dostępności - top 10 produktów
                var topProducts = productSummary
                    .OrderByDescending(x => x.Value.ilosc)
                    .Take(12)
                    .Select(kvp =>
                    {
                        string productName = productNames.TryGetValue(kvp.Key, out var name) ? name : $"Produkt {kvp.Key}";
                        decimal ilosc = kvp.Value.ilosc;
                        int liczbaZam = kvp.Value.liczbaZamowien;

                        var kolorRamki = ilosc > 500 ? Brushes.Green : (ilosc > 100 ? Brushes.Orange : Brushes.Red);
                        var kolorTla = ilosc > 500 ? Color.FromRgb(232, 248, 232) :
                                       (ilosc > 100 ? Color.FromRgb(255, 248, 225) : Color.FromRgb(255, 235, 235));
                        var kolorPaska = kolorRamki;
                        double maxIlosc = productSummary.Values.Max(x => (double)x.ilosc);
                        double procent = maxIlosc > 0 ? (double)ilosc / maxIlosc * 100 : 0;

                        return new DostepnoscProduktuModel
                        {
                            Nazwa = productName,
                            Bilans = ilosc,
                            BilansText = $"{ilosc:N0} kg",
                            PlanFaktText = $"{liczbaZam} zam.",
                            ZamowioneText = $"{ilosc:N0}",
                            ProcentText = $"{procent:N0}%",
                            SzerokoscPaska = Math.Max(10, Math.Min(150, procent * 1.5)),
                            KolorRamki = kolorRamki,
                            KolorTla = kolorTla,
                            KolorPaska = kolorPaska
                        };
                    })
                    .ToList();

                icDostepnoscProduktow.ItemsSource = topProducts;

                // Wydajność - ukryj bo nie mamy tych danych
                txtWydajnoscGlowna.Text = $"{zamowienAktywnych}";
                txtTuszkaA.Text = $"{totalPozycji}";
                txtTuszkaB.Text = $"{zamowienAnulowanych}";
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

        private void DpData_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (dpData.SelectedDate.HasValue)
            {
                _selectedDate = dpData.SelectedDate.Value;
                txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd dddd}";
                _ = LoadDataAsync();
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadDataAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // DostepnoscProduktuModel jest zdefiniowany w MainWindow.xaml.cs
}
