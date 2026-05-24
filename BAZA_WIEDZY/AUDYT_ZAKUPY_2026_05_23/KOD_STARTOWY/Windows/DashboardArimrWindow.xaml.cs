// ════════════════════════════════════════════════════════════════════════════
// DashboardArimrWindow.xaml.cs — Faza 3 (compliance live + lista do zakontraktowania)
// Target: Kontrakty/Windows/DashboardArimrWindow.xaml.cs
//
// UWAGA: XAML pominięty świadomie — buduje się dynamicznie w code-behind żeby
// uniknąć zależności od konkretnej wersji LiveCharts. Dla wersji z wykresem —
// dodać LiveCharts.Wpf (jak w AnalitykaPelna, gotcha: NaN crash → guard 0.0).
//
// Minimalny UI: ProgressBar + 2 DataGrid (do zakontraktowania + wygasające).
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Kalendarz1.Kontrakty.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Windows
{
    public partial class DashboardArimrWindow : Window
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private readonly KontraktyService _svc = new();

        // Elementy budowane w kodzie (brak XAML — patrz nagłówek)
        private ProgressBar _prg = null!;
        private TextBlock _lbl = null!;
        private DataGrid _dgDoZakontraktowania = null!;
        private DataGrid _dgWygasajace = null!;

        public DashboardArimrWindow()
        {
            Title = "🎯 ARiMR Compliance";
            Width = 1000; Height = 780;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF7, 0xFA));
            BudujUi();
            Loaded += async (_, _) => await LoadAsync();
        }

        private void BudujUi()
        {
            var root = new DockPanel { Margin = new Thickness(16) };

            // Nagłówek
            var header = new TextBlock
            {
                Text = "🎯 ARiMR COMPLIANCE — kontrakty 3-letnie",
                FontSize = 20, FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            // Pasek compliance
            var komplianceBox = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            _prg = new ProgressBar { Height = 32, Minimum = 0, Maximum = 100, Value = 0,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)) };
            _lbl = new TextBlock { FontSize = 14, Margin = new Thickness(0, 6, 0, 0) };
            komplianceBox.Children.Add(_prg);
            komplianceBox.Children.Add(_lbl);
            DockPanel.SetDock(komplianceBox, Dock.Top);
            root.Children.Add(komplianceBox);

            // 2 grids w gridzie
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition());

            var lbl1 = new TextBlock { Text = "🟡 Hodowcy do zakontraktowania (high value, spot)",
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
            _dgDoZakontraktowania = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, FontSize = 12 };
            var sp1 = new StackPanel();
            sp1.Children.Add(lbl1); sp1.Children.Add(_dgDoZakontraktowania);
            Grid.SetRow(sp1, 0);

            var lbl2 = new TextBlock { Text = "⚠️ Kontrakty wygasające w 6 miesięcy",
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 4) };
            _dgWygasajace = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, FontSize = 12 };
            var sp2 = new StackPanel();
            sp2.Children.Add(lbl2); sp2.Children.Add(_dgWygasajace);
            Grid.SetRow(sp2, 2);

            grid.Children.Add(sp1);
            grid.Children.Add(sp2);
            root.Children.Add(grid);

            Content = root;
        }

        private async Task LoadAsync()
        {
            // 1. Compliance z view
            var snap = await _svc.GetArimrComplianceAsync();
            if (snap != null)
            {
                _prg.Value = (double)snap.ProcentArimr;
                _prg.Foreground = snap.Status switch
                {
                    "OK"   => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), // zielony
                    "WARN" => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)), // pomarańcz
                    _      => new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C))  // czerwony
                };
                _lbl.Text = $"Compliance: {snap.ProcentArimr:F1}%  (min 50%)  |  " +
                            $"Surowiec ARiMR: {snap.SurowiecArimrKg:N0} kg / {snap.SurowiecCaloscKg:N0} kg  |  " +
                            $"Hodowców pod 3-letnim: {snap.HodowcowArimr}/{snap.HodowcowOgolem}  |  " +
                            $"Status: {snap.Status}";
            }
            else
            {
                _lbl.Text = "Brak danych compliance (sprawdź view v_ArimrCompliance + dane w FarmerCalc).";
            }

            // 2. Hodowcy do zakontraktowania (spot z najwyższym wolumenem)
            _dgDoZakontraktowania.ItemsSource = await PobierzDoZakontraktowaniaAsync();

            // 3. Wygasające
            _dgWygasajace.ItemsSource = await PobierzWygasajaceAsync();
        }

        private async Task<List<object>> PobierzDoZakontraktowaniaAsync()
        {
            // Hodowcy z dużym wolumenem (FarmerCalc 12m) BEZ aktywnego kontraktu ARiMR
            const string sql = @"
WITH Okres AS (SELECT DATEADD(MONTH,-12,GETDATE()) AS Od, GETDATE() AS Do)
SELECT TOP 15
    fc.Dostawca AS Hodowca,
    COUNT(*) AS LiczbaDostaw,
    SUM(ISNULL(fc.NettoFarmWeight,0)) AS KgRoczne
FROM dbo.FarmerCalc fc, Okres o
WHERE fc.CalcDate BETWEEN o.Od AND o.Do
  AND NOT EXISTS (
      SELECT 1 FROM dbo.Kontrakty k
      WHERE k.DostawcaId = fc.Dostawca
        AND k.LiczySieDoArimr = 1
        AND k.Status IN ('ACTIVE','EXPIRING','SIGNED'))
GROUP BY fc.Dostawca
ORDER BY KgRoczne DESC;";

            var rows = new List<object>();
            try
            {
                using var conn = new SqlConnection(ConnLibra);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    rows.Add(new
                    {
                        Hodowca = rdr["Hodowca"]?.ToString(),
                        Dostaw = rdr["LiczbaDostaw"],
                        KgRoczne = Convert.ToDecimal(rdr["KgRoczne"]).ToString("N0")
                    });
                }
            }
            catch (Exception ex)
            {
                rows.Add(new { Hodowca = "BŁĄD: " + ex.Message, Dostaw = (object?)null, KgRoczne = "" });
            }
            return rows;
        }

        private async Task<List<object>> PobierzWygasajaceAsync()
        {
            const string sql = @"
SELECT NumerKontraktu, NazwaHodowcySnapshot AS Hodowca, DataObowiazujeDo AS Wygasa, Status
FROM dbo.Kontrakty
WHERE Status IN ('ACTIVE','EXPIRING')
  AND DataObowiazujeDo IS NOT NULL
  AND DataObowiazujeDo <= DATEADD(MONTH,6,GETDATE())
ORDER BY DataObowiazujeDo ASC;";

            var rows = new List<object>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var wygasa = (DateTime)rdr["Wygasa"];
                rows.Add(new
                {
                    Numer = rdr["NumerKontraktu"]?.ToString(),
                    Hodowca = rdr["Hodowca"]?.ToString(),
                    Wygasa = wygasa.ToString("dd.MM.yyyy"),
                    DniDo = (wygasa - DateTime.Today).Days,
                    Status = rdr["Status"]?.ToString()
                });
            }
            return rows;
        }
    }
}
