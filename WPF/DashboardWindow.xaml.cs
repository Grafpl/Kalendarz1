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
            _dtDashboard.Columns.Add("Plan", typeof(decimal));
            _dtDashboard.Columns.Add("Fakt", typeof(decimal));
            _dtDashboard.Columns.Add("Stan", typeof(decimal));
            _dtDashboard.Columns.Add("Zamowienia", typeof(decimal));
            _dtDashboard.Columns.Add("Wydania", typeof(decimal));
            _dtDashboard.Columns.Add("Bilans", typeof(decimal));
            _dtDashboard.Columns.Add("Produkt", typeof(string));
            _dtDashboard.Columns.Add("KodTowaru", typeof(int));
            _dtDashboard.Columns.Add("Katalog", typeof(string));

            dgDashboardProdukty.ItemsSource = _dtDashboard.DefaultView;
            SetupDataGrid();
        }

        private void SetupDataGrid()
        {
            dgDashboardProdukty.Columns.Clear();

            bool uzywajWydan = rbBilansWydania?.IsChecked == true;

            // Style dla wyrównania
            var rightStyle = new Style(typeof(TextBlock));
            rightStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            rightStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 10, 0)));

            var centerStyle = new Style(typeof(TextBlock));
            centerStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));

            var zamStyle = new Style(typeof(TextBlock));
            zamStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            zamStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 10, 0)));
            if (uzywajWydan)
            {
                zamStyle.Setters.Add(new Setter(TextBlock.TextDecorationsProperty, TextDecorations.Strikethrough));
                zamStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Gray));
            }

            var wydStyle = new Style(typeof(TextBlock));
            wydStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            wydStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 10, 0)));
            if (!uzywajWydan)
            {
                wydStyle.Setters.Add(new Setter(TextBlock.TextDecorationsProperty, TextDecorations.Strikethrough));
                wydStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Gray));
            }

            // 1. PLAN
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Plan",
                Binding = new Binding("Plan") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = rightStyle
            });

            // 2. FAKT
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Fakt",
                Binding = new Binding("Fakt") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = rightStyle
            });

            // 3. STAN
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Stan",
                Binding = new Binding("Stan") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = centerStyle
            });

            // 4. ZAM.
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = uzywajWydan ? "Zam." : "Zam. ✓",
                Binding = new Binding("Zamowienia") { StringFormat = "N0" },
                Width = new DataGridLength(85),
                ElementStyle = zamStyle
            });

            // 5. WYD.
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = uzywajWydan ? "Wyd. ✓" : "Wyd.",
                Binding = new Binding("Wydania") { StringFormat = "N0" },
                Width = new DataGridLength(85),
                ElementStyle = wydStyle
            });

            // 6. BILANS (DO SPRZEDANIA)
            var bilansStyle = new Style(typeof(TextBlock));
            bilansStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            bilansStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 10, 0)));
            bilansStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));

            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Do sprzedania",
                Binding = new Binding("Bilans") { StringFormat = "N0" },
                Width = new DataGridLength(100),
                ElementStyle = bilansStyle
            });

            // 7. PRODUKT
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Produkt",
                Binding = new Binding("Produkt"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 200
            });

            dgDashboardProdukty.LoadingRow -= DgDashboardProdukty_LoadingRow;
            dgDashboardProdukty.LoadingRow += DgDashboardProdukty_LoadingRow;
        }

        private void DgDashboardProdukty_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                var bilans = rowView.Row.Field<decimal>("Bilans");
                var produkt = rowView.Row.Field<string>("Produkt") ?? "";

                // Wiersz SUMA CAŁKOWITA
                if (produkt.StartsWith("═══"))
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    e.Row.Foreground = Brushes.White;
                    e.Row.FontWeight = FontWeights.Bold;
                    e.Row.FontSize = 14;
                    return;
                }

                // Wiersz kategorii (Kurczak A, Kurczak B)
                if (produkt.StartsWith("🐔"))
                {
                    if (produkt.Contains("Kurczak A"))
                        e.Row.Background = new SolidColorBrush(Color.FromRgb(200, 230, 201));
                    else
                        e.Row.Background = new SolidColorBrush(Color.FromRgb(179, 229, 252));
                    e.Row.FontWeight = FontWeights.SemiBold;
                    e.Row.FontSize = 13;
                    return;
                }

                // Kolorowanie według bilansu
                if (bilans > 500)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(232, 248, 232)); // Zielony - dużo dostępne
                }
                else if (bilans > 0)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 248, 225)); // Żółty - mało
                }
                else
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 235, 235)); // Czerwony - brak/ujemny
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
                txtStatusTime.Text = "Ładowanie...";

                bool uzywajWydan = rbBilansWydania?.IsChecked == true;
                // Katalog: 67095 = Świeże, 67153 = Mrożone
                int? katalogFilter = rbMrozone?.IsChecked == true ? 67153 : (rbSwieze?.IsChecked == true ? 67095 : null);

                // Pobierz nazwy i katalogi produktów z Handel
                var productInfo = new Dictionary<int, (string Nazwa, int? Katalog)>();
                try
                {
                    await using var cnHandel = new SqlConnection(_connHandel);
                    await cnHandel.OpenAsync();
                    const string sqlProducts = "SELECT ID, kod, katalog FROM [HANDEL].[HM].[TW]";
                    await using var cmdProducts = new SqlCommand(sqlProducts, cnHandel);
                    await using var rdrProducts = await cmdProducts.ExecuteReaderAsync();
                    while (await rdrProducts.ReadAsync())
                    {
                        if (!rdrProducts.IsDBNull(0))
                        {
                            int id = rdrProducts.GetInt32(0);
                            string kod = rdrProducts.IsDBNull(1) ? $"ID:{id}" : rdrProducts.GetString(1);
                            int? katalog = null;
                            if (!rdrProducts.IsDBNull(2))
                            {
                                var katVal = rdrProducts.GetValue(2);
                                if (katVal is int ki) katalog = ki;
                                else if (int.TryParse(Convert.ToString(katVal), out int kp)) katalog = kp;
                            }
                            productInfo[id] = (kod, katalog);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Błąd ładowania produktów: {ex.Message}");
                }

                // Pobierz stany magazynowe
                var productStates = new Dictionary<int, decimal>();
                try
                {
                    await using var cnHandel = new SqlConnection(_connHandel);
                    await cnHandel.OpenAsync();
                    const string sqlStany = @"SELECT towar, SUM(ilosc) as Stan FROM [HANDEL].[HM].[MAGAZYN] GROUP BY towar";
                    await using var cmdStany = new SqlCommand(sqlStany, cnHandel);
                    await using var rdrStany = await cmdStany.ExecuteReaderAsync();
                    while (await rdrStany.ReadAsync())
                    {
                        if (!rdrStany.IsDBNull(0))
                        {
                            int towarId = rdrStany.GetInt32(0);
                            decimal stan = rdrStany.IsDBNull(1) ? 0m : rdrStany.GetDecimal(1);
                            productStates[towarId] = stan;
                        }
                    }
                }
                catch { /* Ignoruj błędy */ }

                // Pobierz zamówienia (wszystkie aktywne)
                var orderIds = new List<int>();
                int zamowienAktywnych = 0;

                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT Id FROM dbo.ZamowieniaMieso WHERE Status <> 'Anulowane'";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        orderIds.Add(rdr.GetInt32(0));
                        zamowienAktywnych++;
                    }
                }

                // Podsumowanie produktów z zamówień
                var productSummary = new Dictionary<int, decimal>();

                if (orderIds.Any())
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    var sql = $@"SELECT KodTowaru, SUM(Ilosc) as Zamowione
                                 FROM [dbo].[ZamowieniaMiesoTowar]
                                 WHERE ZamowienieId IN ({string.Join(",", orderIds)})
                                 GROUP BY KodTowaru";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int kodTowaru = reader.GetInt32(0);
                        decimal zamowione = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                        productSummary[kodTowaru] = zamowione;
                    }
                }

                // Zbierz wszystkie produkty które mają zamówienia LUB stany
                var allProducts = productSummary.Keys
                    .Union(productStates.Keys)
                    .Where(k => productInfo.ContainsKey(k)) // Tylko produkty które znamy
                    .Distinct()
                    .ToList();

                decimal totalDoSprzedania = 0m;
                decimal totalZamowione = 0m;
                decimal totalBrakuje = 0m;
                decimal totalStan = 0m;
                int produktowCount = 0;

                // Dodaj wiersz SUMA CAŁKOWITA na początku
                var sumaRow = _dtDashboard.NewRow();
                sumaRow["Produkt"] = "═══ SUMA CAŁKOWITA ═══";
                sumaRow["Plan"] = 0m;
                sumaRow["Fakt"] = 0m;
                sumaRow["Stan"] = 0m;
                sumaRow["Zamowienia"] = 0m;
                sumaRow["Wydania"] = 0m;
                sumaRow["Bilans"] = 0m;
                _dtDashboard.Rows.Add(sumaRow);

                // Sortuj i dodaj produkty
                var sortedProducts = allProducts
                    .Select(k => {
                        productSummary.TryGetValue(k, out decimal zam);
                        productStates.TryGetValue(k, out decimal stan);
                        productInfo.TryGetValue(k, out var info);
                        decimal bilans = stan - zam;
                        return (KodTowaru: k, Nazwa: info.Nazwa, Katalog: info.Katalog, Stan: stan, Zam: zam, Bilans: bilans);
                    })
                    .Where(p => !katalogFilter.HasValue || p.Katalog == katalogFilter)
                    .OrderByDescending(p => p.Bilans)
                    .ToList();

                foreach (var p in sortedProducts)
                {
                    var row = _dtDashboard.NewRow();
                    row["Plan"] = 0m;
                    row["Fakt"] = 0m;
                    row["Stan"] = p.Stan;
                    row["Zamowienia"] = p.Zam;
                    row["Wydania"] = 0m;
                    row["Bilans"] = p.Bilans;
                    row["Produkt"] = p.Nazwa;
                    row["KodTowaru"] = p.KodTowaru;
                    row["Katalog"] = p.Katalog?.ToString() ?? "";
                    _dtDashboard.Rows.Add(row);

                    totalZamowione += p.Zam;
                    totalStan += p.Stan;
                    if (p.Bilans > 0) totalDoSprzedania += p.Bilans;
                    if (p.Bilans < 0) totalBrakuje += Math.Abs(p.Bilans);
                    produktowCount++;
                }

                // Aktualizuj wiersz sumy
                sumaRow["Stan"] = totalStan;
                sumaRow["Zamowienia"] = totalZamowione;
                sumaRow["Bilans"] = totalDoSprzedania - totalBrakuje;

                // Aktualizuj KPI w nagłówku
                txtBilansOk.Text = $"{totalDoSprzedania:N0} kg";
                txtBilansUwaga.Text = $"{totalZamowione:N0} kg";
                txtBilansBrak.Text = $"{totalBrakuje:N0} kg";
                txtSumaZamowien.Text = $"Suma zamówień: {totalZamowione:N0} kg | Stan magazynu: {totalStan:N0} kg";
                txtPlanProdukcji.Text = produktowCount.ToString();
                txtWydajnoscGlowna.Text = zamowienAktywnych.ToString();

                // Aktualizuj karty dostępności - top produkty z dodatnim bilansem
                var topProducts = sortedProducts
                    .Where(p => p.Bilans > 0)
                    .Take(16)
                    .Select(p =>
                    {
                        double maxBilans = sortedProducts.Any() ? (double)sortedProducts.Max(x => x.Bilans) : 1;
                        double procent = maxBilans > 0 ? (double)p.Bilans / maxBilans * 100 : 0;

                        var kolorRamki = p.Bilans > 1000 ? Brushes.Green :
                                        (p.Bilans > 500 ? new SolidColorBrush(Color.FromRgb(46, 204, 113)) :
                                        (p.Bilans > 100 ? Brushes.Orange : Brushes.OrangeRed));
                        var kolorTla = p.Bilans > 500 ? Color.FromRgb(232, 248, 232) :
                                       (p.Bilans > 100 ? Color.FromRgb(255, 248, 225) : Color.FromRgb(255, 243, 224));

                        return new DostepnoscProduktuModel
                        {
                            Nazwa = p.Nazwa,
                            BilansText = $"{p.Bilans:N0} kg",
                            PlanFaktText = $"Stan: {p.Stan:N0}",
                            ZamowioneText = $"{p.Zam:N0} kg",
                            StanText = $"{p.Stan:N0} kg",
                            ProcentText = $"{procent:N0}%",
                            SzerokoscPaska = Math.Max(10, Math.Min(170, procent * 1.7)),
                            KolorRamki = kolorRamki,
                            KolorTla = kolorTla,
                            KolorPaska = kolorRamki
                        };
                    })
                    .ToList();

                icDostepnoscProduktow.ItemsSource = topProducts;
                txtStatusTime.Text = $"Aktualizacja: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatusTime.Text = "Błąd ładowania";
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

        private void RbBilans_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            SetupDataGrid();
            _ = LoadDataAsync();
        }

        private void RbKatalog_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            _ = LoadDataAsync();
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
}
