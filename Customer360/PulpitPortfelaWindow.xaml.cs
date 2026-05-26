using Kalendarz1.Customer360.Models;
using Kalendarz1.Customer360.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.Customer360
{
    public partial class PulpitPortfelaWindow : Window
    {
        private readonly PortfelService _service = new();
        private List<PortfelKlient> _portfel = new();

        public class TopRow
        {
            public int Pozycja { get; set; }
            public PortfelKlient Klient { get; set; } = new();
            public decimal UdzialProc { get; set; }
        }

        public PulpitPortfelaWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e) => await ZaladujAsync();

        private async System.Threading.Tasks.Task ZaladujAsync()
        {
            try
            {
                Cursor = System.Windows.Input.Cursors.Wait;
                LblStan.Text = "⏳ Ładuję portfel z HANDEL…";
                _portfel = await _service.GetPortfelAsync();

                if (_portfel.Count == 0 && !string.IsNullOrEmpty(_service.LastError))
                {
                    LblStan.Text = "❌ Błąd: " + _service.LastError;
                    return;
                }

                var p = _service.Podsumuj(_portfel);
                KpiKlienci.Text = p.LiczbaKlientow.ToString("N0");
                KpiObrot.Text = $"{p.ObrotPortfela12M:N0} zł";
                KpiPrzeterm.Text = $"{p.SumaPrzeterminowanych:N0} zł";
                KpiLiczbaPrzeterm.Text = p.LiczbaZPrzeterminowanymi.ToString();
                KpiLimit.Text = p.LiczbaPrzekroczonyLimit.ToString();
                KpiChurn.Text = p.LiczbaChurnZagrozonych.ToString();

                // Alerty kredytowe: przeterminowane lub przekroczony limit — sort wg przeterminowanych malejąco
                GridAlerty.ItemsSource = _portfel
                    .Where(k => k.MaPrzeterminowane || k.PrzekroczonyLimit)
                    .OrderByDescending(k => k.Przeterminowane)
                    .ThenByDescending(k => k.WykorzystanieLimitProc)
                    .ToList();

                // Churn: aktywni (obrót>0) z brakiem faktury > 60 dni — sort wg dni malejąco
                GridChurn.ItemsSource = _portfel
                    .Where(k => k.Obrot12M > 0 && k.DniOdOstatniej > 60)
                    .OrderByDescending(k => k.DniOdOstatniej)
                    .ToList();

                // Top klienci wg obrotu
                decimal suma = p.ObrotPortfela12M;
                GridTop.ItemsSource = _portfel
                    .OrderByDescending(k => k.Obrot12M)
                    .Take(50)
                    .Select((k, i) => new TopRow
                    {
                        Pozycja = i + 1,
                        Klient = k,
                        UdzialProc = suma > 0 ? k.Obrot12M / suma * 100m : 0m
                    })
                    .ToList();

                LblStan.Text = $"✅ {p.LiczbaKlientow} klientów · TOP 10 = {p.ObrotTop10Proc:N0}% obrotu · 💡 dwuklik wiersza = karta klienta · {DateTime.Now:HH:mm}";
            }
            catch (Exception ex)
            {
                LblStan.Text = "❌ Błąd: " + ex.Message;
            }
            finally { Cursor = System.Windows.Input.Cursors.Arrow; }
        }

        // Kolorowanie wierszy wg ryzyka
        private void Grid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            var bc = new System.Windows.Media.BrushConverter();
            System.Windows.Media.Brush B(string hex) { try { return (System.Windows.Media.Brush)bc.ConvertFromString(hex)!; } catch { return System.Windows.Media.Brushes.White; } }

            if (e.Row.Item is PortfelKlient k)
            {
                if (k.Przeterminowane > 0.01m && k.MaxDniOpoznienia > 30)
                    e.Row.Background = B("#FEE2E2");          // ciężkie przeterminowanie — czerwony
                else if (k.Przeterminowane > 0.01m)
                    e.Row.Background = B("#FEF3C7");          // przeterminowane — żółty
                else if (k.PrzekroczonyLimit)
                    e.Row.Background = B("#FFEDD5");          // przekroczony limit — pomarańczowy
                else if (k.DniOdOstatniej > 120)
                    e.Row.Background = B("#F3E8FF");          // długa cisza — fiolet
                else if (k.DniOdOstatniej > 60)
                    e.Row.Background = B("#FEF9C3");          // umiarkowana cisza — bladożółty
                else
                    e.Row.Background = System.Windows.Media.Brushes.White;
            }
        }

        private void Grid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            int? id = null;
            if (sender is DataGrid g)
            {
                if (g.SelectedItem is PortfelKlient k) id = k.Id;
                else if (g.SelectedItem is TopRow t) id = t.Klient.Id;
            }
            if (id.HasValue)
            {
                var karta = new Customer360Window(id.Value) { Owner = this };
                karta.Show();
            }
        }

        private DataGrid AktywnyGrid() => Tabs?.SelectedIndex switch { 1 => GridChurn, 2 => GridTop, _ => GridAlerty };

        private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl) Filtruj();
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e) => Filtruj();

        private void Filtruj()
        {
            foreach (var g in new[] { GridAlerty, GridChurn, GridTop })
            {
                if (g?.ItemsSource != null)
                {
                    var v = System.Windows.Data.CollectionViewSource.GetDefaultView(g.ItemsSource);
                    if (v != null) v.Filter = null;
                }
            }
            var grid = AktywnyGrid();
            if (grid?.ItemsSource == null || TxtSzukaj == null) return;
            string q = (TxtSzukaj.Text ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(q)) return;
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(grid.ItemsSource);
            if (view == null) return;
            view.Filter = item =>
            {
                var k = item as PortfelKlient ?? (item as TopRow)?.Klient;
                if (k == null) return false;
                return (k.Nazwa ?? "").ToLowerInvariant().Contains(q)
                    || (k.Handlowiec ?? "").ToLowerInvariant().Contains(q);
            };
        }

        private void BtnEksport_Click(object sender, RoutedEventArgs e)
        {
            var grid = AktywnyGrid();
            if (grid?.Items == null || grid.Items.Count == 0)
            {
                MessageBox.Show(this, "Brak danych do eksportu w tej zakładce.", "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                string nazwaTab = (Tabs.SelectedItem as TabItem)?.Header?.ToString() ?? "portfel";
                foreach (var c in System.IO.Path.GetInvalidFileNameChars()) nazwaTab = nazwaTab.Replace(c, '_');
                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"Portfel_{nazwaTab}_{DateTime.Now:yyyyMMdd_HHmm}.csv");

                string Csv(string? s) { s ??= ""; return (s.Contains(';') || s.Contains('"')) ? "\"" + s.Replace("\"", "\"\"") + "\"" : s; }
                var kolumny = grid.Columns.OfType<DataGridBoundColumn>().ToList();
                var pl = new System.Globalization.CultureInfo("pl-PL");
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(string.Join(";", kolumny.Select(k => Csv(k.Header?.ToString()))));
                foreach (var item in grid.Items)
                {
                    if (item == null) continue;
                    var cells = new List<string>();
                    foreach (var k in kolumny)
                    {
                        string txt = "";
                        if (k.Binding is System.Windows.Data.Binding b && !string.IsNullOrEmpty(b.Path?.Path))
                        {
                            object? cur = item;
                            foreach (var part in b.Path.Path.Split('.'))
                            {
                                if (cur == null) break;
                                cur = cur.GetType().GetProperty(part)?.GetValue(cur);
                            }
                            if (cur != null)
                                txt = !string.IsNullOrEmpty(b.StringFormat) ? string.Format(pl, b.StringFormat, cur) : Convert.ToString(cur, pl) ?? "";
                        }
                        cells.Add(Csv(txt));
                    }
                    sb.AppendLine(string.Join(";", cells));
                }
                System.IO.File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(true));
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Nie udało się wyeksportować: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnMapa_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mapa = new Kalendarz1.Kartoteka.Features.Mapa.MapaKlientowWindow { Owner = this };
                mapa.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Nie udało się otworzyć mapy: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e) => await ZaladujAsync();
    }
}
