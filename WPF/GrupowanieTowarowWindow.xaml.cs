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
        private List<string> _dostepneGrupy = new();

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
            _dostepneGrupy.Clear();
            _dostepneGrupy.Add(""); // Pusta opcja = brak grupy

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

                        // Dodaj do dostępnych grup
                        if (!_dostepneGrupy.Contains(nazwaGrupy))
                            _dostepneGrupy.Add(nazwaGrupy);
                    }
                }

                // Sortuj grupy
                _dostepneGrupy = _dostepneGrupy.OrderBy(g => g).ToList();
                _dostepneGrupy.Insert(0, ""); // Pusta na początku

                // Utwórz listę produktów
                foreach (var kv in produktyDict.OrderBy(p => p.Value))
                {
                    var item = new ProduktGrupaItem
                    {
                        TowarId = kv.Key,
                        NazwaProduktu = kv.Value,
                        NazwaGrupy = scalowania.TryGetValue(kv.Key, out var sc) ? sc.Grupa : "",
                        Kolejnosc = scalowania.TryGetValue(kv.Key, out var sc2) ? sc2.Kolejnosc : 0,
                        Aktywne = true
                    };
                    _produkty.Add(item);
                }

                dgProdukty.ItemsSource = _produkty;
                colGrupa.ItemsSource = _dostepneGrupy;

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
            // Sprawdź czy tabela istnieje
            const string checkSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ScalowanieTowarow'";
            await using var checkCmd = new SqlCommand(checkSql, cn);
            bool exists = (int)await checkCmd.ExecuteScalarAsync()! > 0;

            if (!exists)
            {
                // Utwórz tabelę
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
                // Sprawdź czy kolumna Kolejnosc istnieje
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

        private void UpdateGrupyCount()
        {
            int count = _produkty.Count(p => !string.IsNullOrEmpty(p.NazwaGrupy));
            txtGrupyCount.Text = count.ToString();
        }

        private void BtnNowaGrupa_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Nowa grupa", "Podaj nazwę nowej grupy (np. Ćwiartka):");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
            {
                string nowaGrupa = dialog.ResponseText.Trim();
                if (!_dostepneGrupy.Contains(nowaGrupa))
                {
                    _dostepneGrupy.Add(nowaGrupa);
                    _dostepneGrupy = _dostepneGrupy.OrderBy(g => g).ToList();
                    _dostepneGrupy.Remove("");
                    _dostepneGrupy.Insert(0, "");
                    colGrupa.ItemsSource = null;
                    colGrupa.ItemsSource = _dostepneGrupy;
                }

                // Ustaw grupę dla zaznaczonego produktu
                if (dgProdukty.SelectedItem is ProduktGrupaItem selected)
                {
                    selected.NazwaGrupy = nowaGrupa;
                    dgProdukty.Items.Refresh();
                    UpdateGrupyCount();
                }
            }
        }

        private void BtnUsunGrupe_Click(object sender, RoutedEventArgs e)
        {
            if (dgProdukty.SelectedItem is ProduktGrupaItem selected)
            {
                selected.NazwaGrupy = "";
                dgProdukty.Items.Refresh();
                UpdateGrupyCount();
            }
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
                foreach (var produkt in _produkty.Where(p => !string.IsNullOrEmpty(p.NazwaGrupy)))
                {
                    const string insertSql = @"INSERT INTO [dbo].[ScalowanieTowarow] (TowarIdtw, NazwaGrupy, Kolejnosc)
                                              VALUES (@TowarId, @NazwaGrupy, @Kolejnosc)";
                    await using var insertCmd = new SqlCommand(insertSql, cn);
                    insertCmd.Parameters.AddWithValue("@TowarId", produkt.TowarId);
                    insertCmd.Parameters.AddWithValue("@NazwaGrupy", produkt.NazwaGrupy);
                    insertCmd.Parameters.AddWithValue("@Kolejnosc", produkt.Kolejnosc);
                    await insertCmd.ExecuteNonQueryAsync();
                }

                MessageBox.Show($"Zapisano {_produkty.Count(p => !string.IsNullOrEmpty(p.NazwaGrupy))} produktów w grupach.",
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
    }

    public class ProduktGrupaItem : INotifyPropertyChanged
    {
        private string _nazwaGrupy = "";
        private int _kolejnosc;
        private bool _aktywne = true;

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

        public bool Aktywne
        {
            get => _aktywne;
            set { _aktywne = value; OnPropertyChanged(nameof(Aktywne)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // Prosty dialog do wprowadzania tekstu
    public class InputDialog : Window
    {
        private TextBox _textBox;
        public string ResponseText => _textBox.Text;

        public InputDialog(string title, string prompt)
        {
            Title = title;
            Width = 400;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            _textBox = new TextBox { Height = 28, FontSize = 13 };
            Grid.SetRow(_textBox, 1);
            grid.Children.Add(_textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var okButton = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 8, 0) };
            okButton.Click += (s, e) => { DialogResult = true; Close(); };
            buttonPanel.Children.Add(okButton);

            var cancelButton = new Button { Content = "Anuluj", Width = 80 };
            cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            Content = grid;

            _textBox.Focus();
        }
    }
}
