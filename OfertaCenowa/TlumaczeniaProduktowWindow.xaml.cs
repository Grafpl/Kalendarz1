using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Diagnostics;

namespace Kalendarz1.OfertaCenowa
{
    public partial class TlumaczeniaProduktowWindow : Window
    {
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _sciezkaPliku = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OfertaHandlowa", "tlumaczenia.json");
        public ObservableCollection<TlumaczenieProduktu> Tlumaczenia { get; set; } = new();

        public TlumaczeniaProduktowWindow()
        {
            InitializeComponent();
            dgTlumaczenia.ItemsSource = Tlumaczenia;
            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            try
            {
                await LoadProduktyAsync();
                WczytajTlumaczenia();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadProduktyAsync()
        {
            Tlumaczenia.Clear();
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                var excludedProducts = new[] { "KURCZAK B", "FILET C" };
                await using var cmd = new SqlCommand("SELECT Kod, Nazwa FROM [HANDEL].[HM].[TW] WHERE katalog IN ('67095', '67153') ORDER BY Kod", cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    string kod = rd["Kod"]?.ToString() ?? "";
                    if (excludedProducts.Any(excluded => kod.ToUpper().Contains(excluded))) continue;

                    Tlumaczenia.Add(new TlumaczenieProduktu
                    {
                        Kod = kod,
                        NazwaPL = rd["Nazwa"]?.ToString() ?? "",
                        NazwaEN = ""
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd ładowania produktów: {ex.Message}");
            }
        }

        private void WczytajTlumaczenia()
        {
            if (!File.Exists(_sciezkaPliku)) return;

            try
            {
                string json = File.ReadAllText(_sciezkaPliku);
                var zapisane = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(json);
                
                if (zapisane != null)
                {
                    foreach (var tlumaczenie in Tlumaczenia)
                    {
                        if (zapisane.TryGetValue(tlumaczenie.Kod, out string? nazwaEN))
                        {
                            tlumaczenie.NazwaEN = nazwaEN;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd wczytywania tłumaczeń: {ex.Message}");
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var doZapisu = Tlumaczenia
                    .Where(t => !string.IsNullOrWhiteSpace(t.NazwaEN))
                    .ToDictionary(t => t.Kod, t => t.NazwaEN);

                string folder = Path.GetDirectoryName(_sciezkaPliku) ?? "";
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string json = JsonSerializer.Serialize(doZapisu, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_sciezkaPliku, json);

                MessageBox.Show($"Zapisano {doZapisu.Count} tłumaczeń.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnWczytaj_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Wybierz plik z tłumaczeniami"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(dialog.FileName);
                    var wczytane = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(json);

                    if (wczytane != null)
                    {
                        int licznik = 0;
                        foreach (var tlumaczenie in Tlumaczenia)
                        {
                            if (wczytane.TryGetValue(tlumaczenie.Kod, out string? nazwaEN))
                            {
                                tlumaczenie.NazwaEN = nazwaEN;
                                licznik++;
                            }
                        }

                        MessageBox.Show($"Wczytano {licznik} tłumaczeń z pliku.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd wczytywania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
