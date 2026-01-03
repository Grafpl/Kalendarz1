using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.WPF
{
    public partial class GrupowanieTowarowWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private ObservableCollection<ProduktGrupaItem> _produkty = new();
        private ObservableCollection<ProduktGrupaItem> _produktyFiltered = new();
        private ObservableCollection<GrupaInfo> _grupy = new();

        public GrupowanieTowarowWindow(string connLibra, string connHandel)
        {
            InitializeComponent();
            _connLibra = connLibra;
            _connHandel = connHandel;

            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            _produkty.Clear();
            _grupy.Clear();

            try
            {
                // Pobierz produkty z katalogu TW (Świeże 67095 i Mrożone 67153)
                var produktyDict = new Dictionary<int, string>();
                await using (var cnHandel = new SqlConnection(_connHandel))
                {
                    await cnHandel.OpenAsync();
                    const string sql = "SELECT ID, kod FROM [HANDEL].[HM].[TW] WHERE katalog IN (67095, 67153) ORDER BY kod";
                    await using var cmd = new SqlCommand(sql, cnHandel);
                    await using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        string kod = reader.IsDBNull(1) ? $"ID:{id}" : reader.GetString(1);
                        produktyDict[id] = kod;
                    }
                }

                // Pobierz istniejące scalowania
                var scalowania = new Dictionary<int, (string Grupa, int Kolejnosc)>();
                await using (var cnLibra = new SqlConnection(_connLibra))
                {
                    await cnLibra.OpenAsync();

                    // Sprawdź czy tabela istnieje, jeśli nie - utwórz
                    await EnsureTableExistsAsync(cnLibra);

                    const string sql = "SELECT TowarIdtw, NazwaGrupy, ISNULL(Kolejnosc, 0) FROM [dbo].[ScalowanieTowarow]";
                    await using var cmd = new SqlCommand(sql, cnLibra);
                    await using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        int towarId = reader.GetInt32(0);
                        string nazwaGrupy = reader.GetString(1);
                        int kolejnosc = reader.GetInt32(2);
                        scalowania[towarId] = (nazwaGrupy, kolejnosc);
                    }
                }

                // Utwórz listę produktów
                foreach (var kv in produktyDict.OrderBy(p => p.Value))
                {
                    var item = new ProduktGrupaItem
                    {
                        TowarId = kv.Key,
                        NazwaProduktu = kv.Value,
                        NazwaGrupy = scalowania.TryGetValue(kv.Key, out var sc) ? sc.Grupa : "",
                        Kolejnosc = scalowania.TryGetValue(kv.Key, out var sc2) ? sc2.Kolejnosc : 0
                    };
                    _produkty.Add(item);
                }

                RefreshGrupyList();
                ApplyFilter();
                UpdateGrupyCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task EnsureTableExistsAsync(SqlConnection cn)
        {
            const string checkSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ScalowanieTowarow'";
            await using var checkCmd = new SqlCommand(checkSql, cn);
            bool exists = (int)await checkCmd.ExecuteScalarAsync()! > 0;

            if (!exists)
            {
                const string createSql = @"
                    CREATE TABLE [dbo].[ScalowanieTowarow] (
                        [Id] INT IDENTITY(1,1) PRIMARY KEY,
                        [TowarIdtw] INT NOT NULL,
                        [NazwaGrupy] NVARCHAR(100) NOT NULL,
                        [Kolejnosc] INT DEFAULT 0
                    )";
                await using var createCmd = new SqlCommand(createSql, cn);
                await createCmd.ExecuteNonQueryAsync();
            }
            else
            {
                const string checkColSql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                                            WHERE TABLE_NAME = 'ScalowanieTowarow' AND COLUMN_NAME = 'Kolejnosc'";
                await using var checkColCmd = new SqlCommand(checkColSql, cn);
                bool colExists = (int)await checkColCmd.ExecuteScalarAsync()! > 0;

                if (!colExists)
                {
                    const string alterSql = "ALTER TABLE [dbo].[ScalowanieTowarow] ADD [Kolejnosc] INT DEFAULT 0";
                    await using var alterCmd = new SqlCommand(alterSql, cn);
                    await alterCmd.ExecuteNonQueryAsync();
                }
            }
        }

        private void RefreshGrupyList()
        {
            _grupy.Clear();

            var grupyDict = _produkty
                .Where(p => !string.IsNullOrEmpty(p.NazwaGrupy))
                .GroupBy(p => p.NazwaGrupy)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var kv in grupyDict.OrderBy(g => g.Key))
            {
                _grupy.Add(new GrupaInfo { Nazwa = kv.Key, IloscProduktow = kv.Value });
            }

            lstGrupy.ItemsSource = _grupy;
        }

        private void ApplyFilter()
        {
            var search = txtSearch?.Text?.ToLower() ?? "";
            bool onlyGrouped = chkShowOnlyGrouped?.IsChecked == true;
            bool onlyUngrouped = chkShowOnlyUngrouped?.IsChecked == true;

            var filtered = _produkty.Where(p =>
            {
                bool matchesSearch = string.IsNullOrEmpty(search) ||
                    p.NazwaProduktu.ToLower().Contains(search) ||
                    p.TowarId.ToString().Contains(search);

                bool matchesGroupFilter = true;
                if (onlyGrouped)
                    matchesGroupFilter = !string.IsNullOrEmpty(p.NazwaGrupy);
                else if (onlyUngrouped)
                    matchesGroupFilter = string.IsNullOrEmpty(p.NazwaGrupy);

                return matchesSearch && matchesGroupFilter;
            }).ToList();

            _produktyFiltered = new ObservableCollection<ProduktGrupaItem>(filtered);
            dgProdukty.ItemsSource = _produktyFiltered;
        }

        private void UpdateGrupyCount()
        {
            int count = _produkty.Count(p => !string.IsNullOrEmpty(p.NazwaGrupy));
            txtGrupyCount.Text = count.ToString();
        }

        private void UpdateSelectedCount()
        {
            int count = dgProdukty.SelectedItems.Count;
            txtSelectedCount.Text = $"Zaznaczono: {count}";

            bool hasSelection = count > 0;
            bool hasGroupSelected = lstGrupy.SelectedItem != null;
            btnPrzypisz.IsEnabled = hasSelection && hasGroupSelected;
            btnOdpinij.IsEnabled = hasSelection;
        }

        private void UpdateGroupButtons()
        {
            bool hasGroupSelected = lstGrupy.SelectedItem != null;
            btnZmienNazwe.IsEnabled = hasGroupSelected;
            btnUsunGrupe.IsEnabled = hasGroupSelected;
            btnPrzypisz.IsEnabled = hasGroupSelected && dgProdukty.SelectedItems.Count > 0;
        }

        #region Event Handlers

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ChkShowOnlyGrouped_Changed(object sender, RoutedEventArgs e)
        {
            if (chkShowOnlyUngrouped != null && chkShowOnlyGrouped?.IsChecked == true)
                chkShowOnlyUngrouped.IsChecked = false;
            ApplyFilter();
        }

        private void ChkShowOnlyUngrouped_Changed(object sender, RoutedEventArgs e)
        {
            if (chkShowOnlyGrouped != null && chkShowOnlyUngrouped?.IsChecked == true)
                chkShowOnlyGrouped.IsChecked = false;
            ApplyFilter();
        }

        private void DgProdukty_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectedCount();
        }

        private void LstGrupy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateGroupButtons();
        }

        private void BtnNowaGrupa_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Nowa grupa", "Podaj nazwę nowej grupy (np. Ćwiartka):");
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
            {
                string nowaGrupa = dialog.ResponseText.Trim();

                if (_grupy.Any(g => g.Nazwa.Equals(nowaGrupa, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show($"Grupa '{nowaGrupa}' już istnieje!", "Uwaga",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _grupy.Add(new GrupaInfo { Nazwa = nowaGrupa, IloscProduktow = 0 });
                lstGrupy.ItemsSource = null;
                lstGrupy.ItemsSource = _grupy.OrderBy(g => g.Nazwa).ToList();

                // Zaznacz nową grupę
                lstGrupy.SelectedItem = _grupy.FirstOrDefault(g => g.Nazwa == nowaGrupa);

                MessageBox.Show($"Utworzono grupę '{nowaGrupa}'.\nTeraz zaznacz produkty i kliknij 'Przypisz do grupy'.",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnZmienNazwe_Click(object sender, RoutedEventArgs e)
        {
            if (lstGrupy.SelectedItem is not GrupaInfo selected) return;

            var dialog = new InputDialog("Zmień nazwę grupy", $"Nowa nazwa dla grupy '{selected.Nazwa}':");
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
            {
                string nowaNazwa = dialog.ResponseText.Trim();

                if (_grupy.Any(g => g.Nazwa.Equals(nowaNazwa, StringComparison.OrdinalIgnoreCase) && g != selected))
                {
                    MessageBox.Show($"Grupa '{nowaNazwa}' już istnieje!", "Uwaga",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string staraNazwa = selected.Nazwa;

                // Zmień nazwę we wszystkich produktach
                foreach (var produkt in _produkty.Where(p => p.NazwaGrupy == staraNazwa))
                {
                    produkt.NazwaGrupy = nowaNazwa;
                }

                RefreshGrupyList();
                ApplyFilter();

                MessageBox.Show($"Zmieniono nazwę grupy z '{staraNazwa}' na '{nowaNazwa}'.",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnUsunGrupe_Click(object sender, RoutedEventArgs e)
        {
            if (lstGrupy.SelectedItem is not GrupaInfo selected) return;

            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć grupę '{selected.Nazwa}'?\n\n" +
                $"Produkty ({selected.IloscProduktow}) zostaną odpięte od grupy.",
                "Potwierdź usunięcie",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // Usuń grupę z produktów
            foreach (var produkt in _produkty.Where(p => p.NazwaGrupy == selected.Nazwa))
            {
                produkt.NazwaGrupy = "";
            }

            RefreshGrupyList();
            ApplyFilter();
            UpdateGrupyCount();

            MessageBox.Show($"Usunięto grupę '{selected.Nazwa}'.", "Sukces",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnPrzypisz_Click(object sender, RoutedEventArgs e)
        {
            if (lstGrupy.SelectedItem is not GrupaInfo selectedGrupa) return;
            if (dgProdukty.SelectedItems.Count == 0) return;

            int count = 0;
            foreach (var item in dgProdukty.SelectedItems.OfType<ProduktGrupaItem>())
            {
                item.NazwaGrupy = selectedGrupa.Nazwa;
                count++;
            }

            RefreshGrupyList();
            ApplyFilter();
            UpdateGrupyCount();

            MessageBox.Show($"Przypisano {count} produktów do grupy '{selectedGrupa.Nazwa}'.",
                "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnOdpinij_Click(object sender, RoutedEventArgs e)
        {
            if (dgProdukty.SelectedItems.Count == 0) return;

            int count = 0;
            foreach (var item in dgProdukty.SelectedItems.OfType<ProduktGrupaItem>())
            {
                if (!string.IsNullOrEmpty(item.NazwaGrupy))
                {
                    item.NazwaGrupy = "";
                    count++;
                }
            }

            RefreshGrupyList();
            ApplyFilter();
            UpdateGrupyCount();

            if (count > 0)
                MessageBox.Show($"Usunięto {count} produktów z grup.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Usuń wszystkie istniejące scalowania
                const string deleteSql = "DELETE FROM [dbo].[ScalowanieTowarow]";
                await using var deleteCmd = new SqlCommand(deleteSql, cn);
                await deleteCmd.ExecuteNonQueryAsync();

                // Dodaj nowe scalowania
                int saved = 0;
                foreach (var produkt in _produkty.Where(p => !string.IsNullOrEmpty(p.NazwaGrupy)))
                {
                    const string insertSql = @"INSERT INTO [dbo].[ScalowanieTowarow] (TowarIdtw, NazwaGrupy, Kolejnosc)
                                              VALUES (@TowarId, @NazwaGrupy, @Kolejnosc)";
                    await using var insertCmd = new SqlCommand(insertSql, cn);
                    insertCmd.Parameters.AddWithValue("@TowarId", produkt.TowarId);
                    insertCmd.Parameters.AddWithValue("@NazwaGrupy", produkt.NazwaGrupy);
                    insertCmd.Parameters.AddWithValue("@Kolejnosc", produkt.Kolejnosc);
                    await insertCmd.ExecuteNonQueryAsync();
                    saved++;
                }

                MessageBox.Show($"Zapisano {saved} produktów w {_grupy.Count} grupach.",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisywania: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion
    }

    public class ProduktGrupaItem : INotifyPropertyChanged
    {
        private string _nazwaGrupy = "";
        private int _kolejnosc;

        public int TowarId { get; set; }
        public string NazwaProduktu { get; set; } = "";

        public string NazwaGrupy
        {
            get => _nazwaGrupy;
            set { _nazwaGrupy = value; OnPropertyChanged(nameof(NazwaGrupy)); }
        }

        public int Kolejnosc
        {
            get => _kolejnosc;
            set { _kolejnosc = value; OnPropertyChanged(nameof(Kolejnosc)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class GrupaInfo
    {
        public string Nazwa { get; set; } = "";
        public int IloscProduktow { get; set; }
    }
}
