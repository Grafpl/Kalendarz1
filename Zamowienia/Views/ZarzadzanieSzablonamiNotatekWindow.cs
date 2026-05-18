using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Kalendarz1.Zamowienia.Views
{
    /// <summary>
    /// Okno zarządzania szablonami notatek: lista + edycja + usuwanie + pin.
    /// </summary>
    public sealed class ZarzadzanieSzablonamiNotatekWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _userId;
        private readonly DataGrid _grid;
        private readonly TextBox _tbFilter;
        private readonly ComboBox _cmbKategoriaFilter;
        private readonly ComboBox _cmbZakresFilter;
        private List<SzablonRow> _all = new();

        public ZarzadzanieSzablonamiNotatekWindow(string connLibra, string userId)
        {
            _connLibra = connLibra;
            _userId = userId ?? "";

            Title = "⚙ Zarządzanie szablonami notatek";
            Width = 1100; Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 12;
            Background = Brushes.White;

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Tytuł
            var hdr = new TextBlock
            {
                Text = "⚙ Szablony notatek",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x40, 0x9A)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(hdr, 0);
            root.Children.Add(hdr);

            // Filtry
            var filterRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _tbFilter = new TextBox { Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 8, 0) };
            _tbFilter.TextChanged += (s, e) => ApplyFilter();
            Grid.SetColumn(_tbFilter, 0);
            filterRow.Children.Add(_tbFilter);

            _cmbKategoriaFilter = new ComboBox { Width = 140, Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(0, 0, 8, 0) };
            _cmbKategoriaFilter.Items.Add("Wszystkie kategorie");
            foreach (var k in Kalendarz1.Zamowienia.Services.NotatkiService.Kategorie) _cmbKategoriaFilter.Items.Add(k);
            _cmbKategoriaFilter.SelectedIndex = 0;
            _cmbKategoriaFilter.SelectionChanged += (s, e) => ApplyFilter();
            Grid.SetColumn(_cmbKategoriaFilter, 1);
            filterRow.Children.Add(_cmbKategoriaFilter);

            _cmbZakresFilter = new ComboBox { Width = 140, Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(0, 0, 8, 0) };
            _cmbZakresFilter.Items.Add("Wszystkie zakresy");
            foreach (var z in Kalendarz1.Zamowienia.Services.NotatkiService.Zakresy) _cmbZakresFilter.Items.Add(z);
            _cmbZakresFilter.SelectedIndex = 0;
            _cmbZakresFilter.SelectionChanged += (s, e) => ApplyFilter();
            Grid.SetColumn(_cmbZakresFilter, 2);
            filterRow.Children.Add(_cmbZakresFilter);

            var btnReload = new Button { Content = "🔄 Odśwież", Padding = new Thickness(10, 4, 10, 4), Cursor = System.Windows.Input.Cursors.Hand };
            btnReload.Click += async (s, e) => await LoadAsync();
            Grid.SetColumn(btnReload, 3);
            filterRow.Children.Add(btnReload);

            Grid.SetRow(filterRow, 1);
            root.Children.Add(filterRow);

            // DataGrid
            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                IsReadOnly = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(0xF7, 0xFA, 0xFC)),
                RowHeight = 32
            };
            _grid.Columns.Add(new DataGridTextColumn { Header = "Id", Binding = new Binding("Id") { Mode = BindingMode.OneWay }, IsReadOnly = true, Width = 50 });
            _grid.Columns.Add(new DataGridCheckBoxColumn { Header = "📌", Binding = new Binding("Pinowane"), Width = 40 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Tekst", Binding = new Binding("Tekst") { Mode = BindingMode.TwoWay }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            var kategoriaCol = new DataGridComboBoxColumn { Header = "Kategoria", SelectedItemBinding = new Binding("Kategoria") { Mode = BindingMode.TwoWay }, Width = 110 };
            foreach (var k in Kalendarz1.Zamowienia.Services.NotatkiService.Kategorie) kategoriaCol.ItemsSource ??= Kalendarz1.Zamowienia.Services.NotatkiService.Kategorie;
            kategoriaCol.ItemsSource = Kalendarz1.Zamowienia.Services.NotatkiService.Kategorie;
            _grid.Columns.Add(kategoriaCol);
            var zakresCol = new DataGridComboBoxColumn { Header = "Zakres", SelectedItemBinding = new Binding("Zakres") { Mode = BindingMode.TwoWay }, Width = 110 };
            zakresCol.ItemsSource = Kalendarz1.Zamowienia.Services.NotatkiService.Zakresy;
            _grid.Columns.Add(zakresCol);
            _grid.Columns.Add(new DataGridTextColumn { Header = "KlientId", Binding = new Binding("KlientId") { Mode = BindingMode.TwoWay }, Width = 70 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "User", Binding = new Binding("UserId") { Mode = BindingMode.TwoWay }, Width = 70 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Użyć", Binding = new Binding("LiczbaUzyc") { Mode = BindingMode.OneWay }, IsReadOnly = true, Width = 50 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Ostatnio", Binding = new Binding("OstatnieUzycieDisplay") { Mode = BindingMode.OneWay }, IsReadOnly = true, Width = 100 });
            _grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Aktywne", Binding = new Binding("Aktywne"), Width = 60 });

            Grid.SetRow(_grid, 2);
            root.Children.Add(_grid);

            // Akcje
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            var btnSave = new Button { Content = "💾 Zapisz zmiany", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush(Color.FromRgb(0x21, 0x40, 0x9A)), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold, Cursor = System.Windows.Input.Cursors.Hand };
            btnSave.Click += async (s, e) => await SaveChangesAsync();
            actions.Children.Add(btnSave);

            var btnDelete = new Button { Content = "🗑 Usuń zaznaczone", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0), Cursor = System.Windows.Input.Cursors.Hand };
            btnDelete.Click += async (s, e) => await DeleteSelectedAsync();
            actions.Children.Add(btnDelete);

            var btnClose = new Button { Content = "Zamknij", Padding = new Thickness(12, 6, 12, 6), Cursor = System.Windows.Input.Cursors.Hand };
            btnClose.Click += (s, e) => Close();
            actions.Children.Add(btnClose);

            Grid.SetRow(actions, 3);
            root.Children.Add(actions);

            Content = root;
            Loaded += async (s, e) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            _all.Clear();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                const string sql = @"
                    SELECT Id, Tekst, ISNULL(Kategoria,'') AS Kategoria, Zakres, KlientId, UserId,
                           Pinowane, LiczbaUzyc, OstatnieUzycie, Aktywne
                    FROM dbo.NotatkiSzablony
                    ORDER BY Pinowane DESC, OstatnieUzycie DESC, LiczbaUzyc DESC";
                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    DateTime? ost = rd["OstatnieUzycie"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["OstatnieUzycie"]);
                    _all.Add(new SzablonRow
                    {
                        Id = Convert.ToInt32(rd["Id"]),
                        Tekst = rd["Tekst"]?.ToString() ?? "",
                        Kategoria = rd["Kategoria"]?.ToString() ?? "",
                        Zakres = rd["Zakres"]?.ToString() ?? "",
                        KlientId = rd["KlientId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["KlientId"]),
                        UserId = rd["UserId"]?.ToString() ?? "",
                        Pinowane = Convert.ToBoolean(rd["Pinowane"]),
                        LiczbaUzyc = Convert.ToInt32(rd["LiczbaUzyc"]),
                        OstatnieUzycie = ost,
                        Aktywne = Convert.ToBoolean(rd["Aktywne"])
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd wczytywania:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string text = (_tbFilter.Text ?? "").Trim().ToLowerInvariant();
            string kat = _cmbKategoriaFilter.SelectedIndex <= 0 ? "" : (_cmbKategoriaFilter.SelectedItem as string ?? "");
            string zak = _cmbZakresFilter.SelectedIndex <= 0 ? "" : (_cmbZakresFilter.SelectedItem as string ?? "");

            var filtered = new List<SzablonRow>();
            foreach (var r in _all)
            {
                if (!string.IsNullOrEmpty(text) && !r.Tekst.ToLowerInvariant().Contains(text)) continue;
                if (!string.IsNullOrEmpty(kat) && !string.Equals(r.Kategoria, kat, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(zak) && !string.Equals(r.Zakres, zak, StringComparison.OrdinalIgnoreCase)) continue;
                filtered.Add(r);
            }
            _grid.ItemsSource = filtered;
        }

        private async Task SaveChangesAsync()
        {
            int updated = 0;
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                foreach (var r in _all)
                {
                    string sql = @"UPDATE dbo.NotatkiSzablony
                                   SET Tekst = @t, Kategoria = @k, Zakres = @z, KlientId = @kid, UserId = @uid,
                                       Pinowane = @p, Aktywne = @a
                                   WHERE Id = @id";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@t", r.Tekst ?? "");
                    cmd.Parameters.AddWithValue("@k", (object?)(string.IsNullOrEmpty(r.Kategoria) ? null : r.Kategoria) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@z", string.IsNullOrEmpty(r.Zakres) ? Kalendarz1.Zamowienia.Services.NotatkiService.ZakresGlobalny : r.Zakres);
                    cmd.Parameters.AddWithValue("@kid", (object?)r.KlientId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@uid", (object?)(string.IsNullOrEmpty(r.UserId) ? null : r.UserId) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@p", r.Pinowane);
                    cmd.Parameters.AddWithValue("@a", r.Aktywne);
                    cmd.Parameters.AddWithValue("@id", r.Id);
                    updated += await cmd.ExecuteNonQueryAsync();
                }
                MessageBox.Show(this, $"Zaktualizowano: {updated} szablon(ów).", "Zapisano",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd zapisu:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteSelectedAsync()
        {
            if (_grid.SelectedItem is not SzablonRow row) return;
            var r = MessageBox.Show(this, $"Usunąć szablon #{row.Id}?\n\n{row.Tekst}",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("DELETE FROM dbo.NotatkiSzablony WHERE Id = @id", cn);
                cmd.Parameters.AddWithValue("@id", row.Id);
                await cmd.ExecuteNonQueryAsync();
                _all.RemoveAll(x => x.Id == row.Id);
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd usuwania:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public class SzablonRow : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string Tekst { get; set; } = "";
            public string Kategoria { get; set; } = "";
            public string Zakres { get; set; } = "";
            public int? KlientId { get; set; }
            public string UserId { get; set; } = "";
            public bool Pinowane { get; set; }
            public int LiczbaUzyc { get; set; }
            public DateTime? OstatnieUzycie { get; set; }
            public bool Aktywne { get; set; }
            public string OstatnieUzycieDisplay => OstatnieUzycie?.ToString("yyyy-MM-dd HH:mm") ?? "—";
            public event PropertyChangedEventHandler? PropertyChanged;
        }
    }
}
