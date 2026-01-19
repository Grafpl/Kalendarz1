// Plik: WidokKlasWagowychDniaWPF.xaml.cs
// Code-behind dla widoku WPF rezerwacji klas wagowych wszystkich klientów dnia
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kalendarz1;

namespace Kalendarz.WPF
{
    public partial class WidokKlasWagowychDniaWPF : Window
    {
        private readonly DateTime _dataProdukcji;
        private readonly string _connLibra;
        private readonly Dictionary<int, int> _prognoza;
        
        public ObservableCollection<KlientRezerwacjaViewModel> Klienci { get; set; } = new();
        public ObservableCollection<SumaKlasyViewModel> SumyKlas { get; set; } = new();

        // Kolory klas
        private static readonly Dictionary<int, Color> KLASY_KOLORY = new()
        {
            { 5, Color.FromRgb(220, 38, 38) },
            { 6, Color.FromRgb(234, 88, 12) },
            { 7, Color.FromRgb(202, 138, 4) },
            { 8, Color.FromRgb(101, 163, 13) },
            { 9, Color.FromRgb(22, 163, 74) },
            { 10, Color.FromRgb(8, 145, 178) },
            { 11, Color.FromRgb(37, 99, 235) },
            { 12, Color.FromRgb(124, 58, 237) },
        };

        public WidokKlasWagowychDniaWPF(DateTime dataProdukcji, string connLibra, Dictionary<int, int> prognoza)
        {
            _dataProdukcji = dataProdukcji;
            _connLibra = connLibra;
            _prognoza = prognoza ?? new Dictionary<int, int>();

            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            txtDataProdukcji.Text = $"Data produkcji: {_dataProdukcji:dd.MM.yyyy (dddd)}";
            dataGrid.ItemsSource = Klienci;
            itemsSumyKlas.ItemsSource = SumyKlas;

            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var rezerwacje = await PobierzRezerwacjeAsync();

                // Grupuj po kliencie
                var klienciGrupy = rezerwacje
                    .GroupBy(r => new { r.ZamowienieId, r.Odbiorca, r.Handlowiec })
                    .Select(g => new KlientRezerwacjaViewModel
                    {
                        Odbiorca = g.Key.Odbiorca,
                        Handlowiec = g.Key.Handlowiec,
                        Kl5 = g.Where(r => r.Klasa == 5).Sum(r => r.IloscPojemnikow),
                        Kl6 = g.Where(r => r.Klasa == 6).Sum(r => r.IloscPojemnikow),
                        Kl7 = g.Where(r => r.Klasa == 7).Sum(r => r.IloscPojemnikow),
                        Kl8 = g.Where(r => r.Klasa == 8).Sum(r => r.IloscPojemnikow),
                        Kl9 = g.Where(r => r.Klasa == 9).Sum(r => r.IloscPojemnikow),
                        Kl10 = g.Where(r => r.Klasa == 10).Sum(r => r.IloscPojemnikow),
                        Kl11 = g.Where(r => r.Klasa == 11).Sum(r => r.IloscPojemnikow),
                        Kl12 = g.Where(r => r.Klasa == 12).Sum(r => r.IloscPojemnikow),
                        JestPodsumowaniem = false
                    })
                    .OrderBy(k => k.Odbiorca)
                    .ToList();

                // Oblicz sumy
                var sumyZajete = new Dictionary<int, int>();
                for (int i = 5; i <= 12; i++)
                    sumyZajete[i] = klienciGrupy.Sum(k => k.GetKlasa(i));

                int sumaZajeteOgolna = sumyZajete.Values.Sum();
                int sumaPrognozaOgolna = _prognoza.Values.Sum();
                int sumaWolneOgolna = sumaPrognozaOgolna - sumaZajeteOgolna;

                // Aktualizuj UI
                Dispatcher.Invoke(() =>
                {
                    Klienci.Clear();
                    foreach (var k in klienciGrupy)
                        Klienci.Add(k);

                    // Dodaj wiersze podsumowań
                    Klienci.Add(new KlientRezerwacjaViewModel
                    {
                        Odbiorca = "═══ SUMA ZAJĘTE ═══",
                        Kl5 = sumyZajete[5], Kl6 = sumyZajete[6], Kl7 = sumyZajete[7], Kl8 = sumyZajete[8],
                        Kl9 = sumyZajete[9], Kl10 = sumyZajete[10], Kl11 = sumyZajete[11], Kl12 = sumyZajete[12],
                        JestPodsumowaniem = true
                    });

                    Klienci.Add(new KlientRezerwacjaViewModel
                    {
                        Odbiorca = "═══ PROGNOZA ═══",
                        Kl5 = _prognoza.GetValueOrDefault(5), Kl6 = _prognoza.GetValueOrDefault(6),
                        Kl7 = _prognoza.GetValueOrDefault(7), Kl8 = _prognoza.GetValueOrDefault(8),
                        Kl9 = _prognoza.GetValueOrDefault(9), Kl10 = _prognoza.GetValueOrDefault(10),
                        Kl11 = _prognoza.GetValueOrDefault(11), Kl12 = _prognoza.GetValueOrDefault(12),
                        JestPodsumowaniem = true
                    });

                    Klienci.Add(new KlientRezerwacjaViewModel
                    {
                        Odbiorca = "═══ WOLNE ═══",
                        Kl5 = _prognoza.GetValueOrDefault(5) - sumyZajete[5],
                        Kl6 = _prognoza.GetValueOrDefault(6) - sumyZajete[6],
                        Kl7 = _prognoza.GetValueOrDefault(7) - sumyZajete[7],
                        Kl8 = _prognoza.GetValueOrDefault(8) - sumyZajete[8],
                        Kl9 = _prognoza.GetValueOrDefault(9) - sumyZajete[9],
                        Kl10 = _prognoza.GetValueOrDefault(10) - sumyZajete[10],
                        Kl11 = _prognoza.GetValueOrDefault(11) - sumyZajete[11],
                        Kl12 = _prognoza.GetValueOrDefault(12) - sumyZajete[12],
                        JestPodsumowaniem = true
                    });

                    // Aktualizuj nagłówek
                    txtPrognozaSuma.Text = $"{sumaPrognozaOgolna} poj. ({sumaPrognozaOgolna / 36m:N1} pal)";
                    txtZajeteSuma.Text = $"{sumaZajeteOgolna} poj. ({sumaZajeteOgolna / 36m:N1} pal)";
                    txtWolneSuma.Text = $"{sumaWolneOgolna} poj. ({sumaWolneOgolna / 36m:N1} pal)";

                    // Kolor wolnych
                    txtWolneSuma.Foreground = sumaWolneOgolna >= 0 
                        ? new SolidColorBrush(Color.FromRgb(92, 138, 58))
                        : new SolidColorBrush(Color.FromRgb(220, 38, 38));

                    // Aktualizuj sumy klas w footerze
                    SumyKlas.Clear();
                    for (int i = 5; i <= 12; i++)
                    {
                        if (sumyZajete[i] > 0)
                        {
                            SumyKlas.Add(new SumaKlasyViewModel
                            {
                                NazwaKlasy = $"Kl.{i}",
                                Ilosc = sumyZajete[i],
                                Kolor = new SolidColorBrush(KLASY_KOLORY[i])
                            });
                        }
                    }

                    txtSumaOgolna.Text = $"Łącznie zajęte: {sumaZajeteOgolna} poj. = {sumaZajeteOgolna / 36m:N2} palet";

                    // Buduj pasek wizualizacji
                    BuildPasekKlas(sumyZajete, sumaPrognozaOgolna);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildPasekKlas(Dictionary<int, int> sumyZajete, int sumaPrognoza)
        {
            gridPasekKlas.ColumnDefinitions.Clear();
            gridPasekKlas.Children.Clear();

            if (sumaPrognoza == 0) return;

            int col = 0;
            foreach (int klasa in new[] { 5, 6, 7, 8, 9, 10, 11, 12 })
            {
                int zajete = sumyZajete.GetValueOrDefault(klasa, 0);
                if (zajete == 0) continue;

                double procent = zajete * 100.0 / sumaPrognoza;

                gridPasekKlas.ColumnDefinitions.Add(new ColumnDefinition 
                { 
                    Width = new GridLength(procent, GridUnitType.Star) 
                });

                var border = new Border
                {
                    Background = new SolidColorBrush(KLASY_KOLORY[klasa]),
                    ToolTip = $"Klasa {klasa}: {zajete} poj. ({procent:N1}%)"
                };

                if (procent > 8)
                {
                    border.Child = new TextBlock
                    {
                        Text = klasa.ToString(),
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }

                Grid.SetColumn(border, col);
                gridPasekKlas.Children.Add(border);
                col++;
            }

            // Dodaj wolne miejsce
            int sumaZajete = sumyZajete.Values.Sum();
            int wolne = sumaPrognoza - sumaZajete;
            if (wolne > 0)
            {
                double procentWolne = wolne * 100.0 / sumaPrognoza;
                gridPasekKlas.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(procentWolne, GridUnitType.Star)
                });

                var borderWolne = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)),
                    ToolTip = $"Wolne: {wolne} poj. ({procentWolne:N1}%)"
                };

                if (procentWolne > 10)
                {
                    borderWolne.Child = new TextBlock
                    {
                        Text = "✓",
                        Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)),
                        FontWeight = FontWeights.Bold,
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }

                Grid.SetColumn(borderWolne, col);
                gridPasekKlas.Children.Add(borderWolne);
            }
        }

        private async Task<List<RezerwacjaInfo>> PobierzRezerwacjeAsync()
        {
            var lista = new List<RezerwacjaInfo>();

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var checkSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RezerwacjeKlasWagowych'";
                await using var checkCmd = new SqlCommand(checkSql, cn);
                if ((int)await checkCmd.ExecuteScalarAsync() == 0) return lista;

                var sql = @"
                    SELECT r.ZamowienieId, r.Odbiorca, r.Handlowiec, r.Klasa, r.IloscPojemnikow
                    FROM [dbo].[RezerwacjeKlasWagowych] r
                    WHERE r.DataProdukcji = @Data AND r.Status = 'Aktywna'
                    ORDER BY r.Odbiorca, r.Klasa";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", _dataProdukcji.Date);

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    lista.Add(new RezerwacjaInfo
                    {
                        ZamowienieId = rd.GetInt32(0),
                        Odbiorca = rd.IsDBNull(1) ? "Nieznany" : rd.GetString(1),
                        Handlowiec = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        Klasa = rd.GetInt32(3),
                        IloscPojemnikow = rd.GetInt32(4)
                    });
                }
            }
            catch { }

            return lista;
        }

        private class RezerwacjaInfo
        {
            public int ZamowienieId { get; set; }
            public string Odbiorca { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public int Klasa { get; set; }
            public int IloscPojemnikow { get; set; }
        }
    }

    public class KlientRezerwacjaViewModel
    {
        public string Odbiorca { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public int Kl5 { get; set; }
        public int Kl6 { get; set; }
        public int Kl7 { get; set; }
        public int Kl8 { get; set; }
        public int Kl9 { get; set; }
        public int Kl10 { get; set; }
        public int Kl11 { get; set; }
        public int Kl12 { get; set; }
        public int Suma => Kl5 + Kl6 + Kl7 + Kl8 + Kl9 + Kl10 + Kl11 + Kl12;
        public decimal Palety => Suma / 36m;
        public bool JestPodsumowaniem { get; set; }

        public int GetKlasa(int klasa) => klasa switch
        {
            5 => Kl5, 6 => Kl6, 7 => Kl7, 8 => Kl8,
            9 => Kl9, 10 => Kl10, 11 => Kl11, 12 => Kl12,
            _ => 0
        };
    }

    public class SumaKlasyViewModel
    {
        public string NazwaKlasy { get; set; } = "";
        public int Ilosc { get; set; }
        public SolidColorBrush Kolor { get; set; } = Brushes.Gray;
    }
}
