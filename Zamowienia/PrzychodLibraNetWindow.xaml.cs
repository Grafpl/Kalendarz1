using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Zamowienia
{
    /// <summary>
    /// Przychód produkcji z LibraNet (ważenia In0E) dla wybranego dnia —
    /// agregacja per towar + rozkład godzinowy po kliknięciu towaru.
    /// Otwierane guzikiem 📦 z nagłówka "Podsumowanie dnia" (Zamówienia Klientów).
    /// </summary>
    public partial class PrzychodLibraNetWindow : Window
    {
        private const string ConnLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Surowe ważenia dnia — trzymane w pamięci, filtr/detal liczone bez ponownego SQL
        private List<(string ArtId, string Towar, int Godz, decimal Kg)> _wazenia = new();
        private List<TowarPrzychodRow> _towary = new();
        private bool _loading;

        public PrzychodLibraNetWindow(DateTime dzien, string? initialFilter = null)
        {
            InitializeComponent();
            dpDzien.SelectedDate = dzien.Date;
            if (!string.IsNullOrWhiteSpace(initialFilter))
                txtFiltr.Text = initialFilter;
            Loaded += async (_, _) => await LoadDayAsync();
        }

        private async Task LoadDayAsync()
        {
            if (_loading) return;
            _loading = true;
            try
            {
                var day = (dpDzien.SelectedDate ?? DateTime.Today).Date;
                txtPodsumowanie.Text = "⏳ Ładowanie…";
                dgTowary.ItemsSource = null;
                dgGodziny.ItemsSource = null;

                var wazenia = new List<(string ArtId, string Towar, int Godz, decimal Kg)>();
                await using (var cn = new SqlConnection(ConnLibra))
                {
                    await cn.OpenAsync();
                    // In0E: Data trzymana jako data, Godzina jako varchar "HH:mm:ss" (SQL 2008 R2 — parsujemy w .NET)
                    const string sql = @"
                        SELECT e.ArticleID, e.ArticleName, e.Godzina, e.ActWeight
                        FROM dbo.In0E e
                        WHERE e.Data = @Day
                          AND ISNULL(e.ArticleName,'') <> ''
                          AND ISNULL(e.ActWeight, 0) > 0";
                    await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
                    cmd.Parameters.AddWithValue("@Day", day.ToString("yyyy-MM-dd"));
                    await using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        string artId = rd.IsDBNull(0) ? "" : rd.GetValue(0)?.ToString()?.Trim() ?? "";
                        string towar = rd.IsDBNull(1) ? "" : rd.GetValue(1)?.ToString()?.Trim() ?? "";
                        string godzStr = rd.IsDBNull(2) ? "" : rd.GetValue(2)?.ToString() ?? "";
                        decimal kg = rd.IsDBNull(3) ? 0m : Convert.ToDecimal(rd.GetValue(3));
                        int godz = -1;
                        if (TimeSpan.TryParse(godzStr, out var ts)) godz = ts.Hours;
                        if (towar.Length > 0 && kg > 0)
                            wazenia.Add((artId, towar, godz, kg));
                    }
                }

                _wazenia = wazenia;
                RebuildTowary();
            }
            catch (Exception ex)
            {
                txtPodsumowanie.Text = "❌ Błąd";
                MessageBox.Show($"Nie udało się pobrać przychodu z LibraNet:\n{ex.Message}",
                    "Przychód LibraNet", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _loading = false;
            }
        }

        private void RebuildTowary()
        {
            string filtr = (txtFiltr.Text ?? "").Trim();

            // Scalanie po numerze Article (ArticleID) — nazwa bywa niejednolita, numer jest kluczem
            _towary = _wazenia
                .Where(w => filtr.Length == 0
                            || w.Towar.Contains(filtr, StringComparison.OrdinalIgnoreCase)
                            || w.ArtId.Contains(filtr, StringComparison.OrdinalIgnoreCase))
                .GroupBy(w => w.ArtId)
                .Select(g => new TowarPrzychodRow
                {
                    ArtId = g.Key,
                    Towar = g.First().Towar,
                    Wazen = g.Count(),
                    SumaKg = g.Sum(x => x.Kg),
                    SrKg = g.Count() > 0 ? g.Sum(x => x.Kg) / g.Count() : 0m,
                    OdGodz = g.Any(x => x.Godz >= 0) ? $"{g.Where(x => x.Godz >= 0).Min(x => x.Godz):00}:00" : "—",
                    DoGodz = g.Any(x => x.Godz >= 0) ? $"{g.Where(x => x.Godz >= 0).Max(x => x.Godz):00}:59" : "—"
                })
                .OrderByDescending(t => t.SumaKg)
                .ToList();

            dgTowary.ItemsSource = _towary;

            decimal sumaKg = _towary.Sum(t => t.SumaKg);
            int sumaWazen = _towary.Sum(t => t.Wazen);
            txtPodsumowanie.Text = $"{_towary.Count} towarów  •  {sumaWazen:N0} ważeń  •  {sumaKg:N0} kg";

            dgGodziny.ItemsSource = null;
            txtDetailTowar.Text = "⏱ Rozkład godzinowy";
        }

        private void RebuildGodziny(string artId, string towar)
        {
            var rows = _wazenia
                .Where(w => w.ArtId == artId && w.Godz >= 0)
                .GroupBy(w => w.Godz)
                .OrderBy(g => g.Key)
                .Select(g => new GodzinaPrzychodRow
                {
                    Godzina = $"{g.Key:00}:00",
                    Wazen = g.Count(),
                    SumaKg = g.Sum(x => x.Kg)
                })
                .ToList();

            dgGodziny.ItemsSource = rows;
            txtDetailTowar.Text = $"⏱ [{artId}] {towar}";
        }

        private async void DpDzien_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) await LoadDayAsync();
        }

        private void TxtFiltr_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded && !_loading) RebuildTowary();
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e) => await LoadDayAsync();

        private void DgTowary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgTowary.SelectedItem is TowarPrzychodRow row)
                RebuildGodziny(row.ArtId, row.Towar);
        }

        private class TowarPrzychodRow
        {
            public string ArtId { get; set; } = "";
            public string Towar { get; set; } = "";
            public int Wazen { get; set; }
            public decimal SumaKg { get; set; }
            public decimal SrKg { get; set; }
            public string OdGodz { get; set; } = "";
            public string DoGodz { get; set; } = "";
        }

        private class GodzinaPrzychodRow
        {
            public string Godzina { get; set; } = "";
            public int Wazen { get; set; }
            public decimal SumaKg { get; set; }
        }
    }
}
