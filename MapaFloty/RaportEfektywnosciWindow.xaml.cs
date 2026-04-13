using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.MapaFloty
{
    public partial class RaportEfektywnosciWindow : Window
    {
        private static readonly string _conn = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly WebfleetReportService _rptSvc = new();
        private List<VehicleReport> _reports = new();

        public RaportEfektywnosciWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            DateFrom.SelectedDate = DateTime.Today.AddDays(-6);
            DateTo.SelectedDate = DateTime.Today;
        }

        private void BtnWeek_Click(object s, RoutedEventArgs e) { DateFrom.SelectedDate = DateTime.Today.AddDays(-6); DateTo.SelectedDate = DateTime.Today; }

        private async void BtnLoad_Click(object s, RoutedEventArgs e)
        {
            var from = DateFrom.SelectedDate ?? DateTime.Today.AddDays(-6);
            var to = DateTo.SelectedDate ?? DateTime.Today;
            var fromStr = from.ToString("yyyy-MM-dd");
            var toStr = to.ToString("yyyy-MM-dd");
            StatusText.Text = "Ładowanie...";
            ReportPanel.Children.Clear();
            _reports.Clear();

            // Pobierz zmapowane pojazdy
            var vehicles = new List<(string objectNo, string rej)>();
            try
            {
                using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT DISTINCT m.WebfleetObjectNo, ISNULL(p.Rejestracja, m.WebfleetObjectName) AS Rej
                    FROM WebfleetVehicleMapping m INNER JOIN Pojazd p ON p.PojazdID=m.PojazdID
                    WHERE m.WebfleetObjectNo IS NOT NULL AND m.PojazdID IS NOT NULL AND p.Aktywny=1";
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    vehicles.Add((r["WebfleetObjectNo"]?.ToString() ?? "", r["Rej"]?.ToString() ?? ""));
            }
            catch (Exception ex) { StatusText.Text = $"Błąd: {ex.Message}"; return; }

            // Pobierz trip summary per pojazd (po 3 równolegle)
            for (int i = 0; i < vehicles.Count; i += 3)
            {
                var batch = vehicles.Skip(i).Take(3).ToList();
                var tasks = batch.Select(v => _rptSvc.GetTripSummaryAsync(v.objectNo, fromStr, toStr)).ToList();
                await Task.WhenAll(tasks);
                for (int j = 0; j < batch.Count; j++)
                {
                    var summaries = tasks[j].IsCompletedSuccessfully ? tasks[j].Result : new();
                    var totalKm = summaries.Sum(s => s.DistanceKm);
                    var totalTripMin = summaries.Sum(s => s.TripTimeSec) / 60;
                    var totalStandMin = summaries.Sum(s => s.StandstillSec) / 60;
                    var totalFuel = summaries.Sum(s => s.FuelUsage);
                    var tours = summaries.Sum(s => s.Tours);
                    var days = summaries.Select(s => s.Date.Date).Distinct().Count();

                    _reports.Add(new VehicleReport
                    {
                        Vehicle = batch[j].rej,
                        TotalKm = totalKm, TotalTripMin = totalTripMin,
                        TotalStandMin = totalStandMin, TotalFuelL = totalFuel,
                        TotalTours = tours, ActiveDays = days,
                        DailySummaries = summaries
                    });
                }
                if (i + 3 < vehicles.Count) await Task.Delay(300);
            }

            // Wyświetl
            _reports = _reports.OrderByDescending(r => r.TotalKm).ToList();

            // Podsumowanie zbiorcze
            var totalAllKm = _reports.Sum(r => r.TotalKm);
            var totalAllTrip = _reports.Sum(r => r.TotalTripMin);
            var totalAllFuel = _reports.Sum(r => r.TotalFuelL);
            AddSummaryRow("PODSUMOWANIE FLOTY", $"{totalAllKm:F0} km | {totalAllTrip / 60}h {totalAllTrip % 60}min jazdy | {totalAllFuel:F0}L paliwa | {_reports.Count} pojazdów");

            // Per pojazd
            foreach (var r in _reports)
                AddVehicleRow(r);

            StatusText.Text = $"{_reports.Count} pojazdów, {from:dd.MM}—{to:dd.MM}";
        }

        private void AddSummaryRow(string title, string detail)
        {
            var border = new Border { Background = new SolidColorBrush(Color.FromRgb(232, 234, 246)), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 10, 16, 10), Margin = new Thickness(0, 0, 0, 8) };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(26, 35, 126)) });
            stack.Children.Add(new TextBlock { Text = detail, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(57, 73, 171)), Margin = new Thickness(0, 4, 0, 0) });
            border.Child = stack;
            ReportPanel.Children.Add(border);
        }

        private void AddVehicleRow(VehicleReport r)
        {
            var border = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 0, 4), BorderBrush = new SolidColorBrush(Color.FromRgb(240, 240, 244)), BorderThickness = new Thickness(1) };

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            void AddCell(int col, string text, Color? clr = null, bool bold = false)
            {
                var tb = new TextBlock { Text = text, FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(clr ?? Color.FromRgb(38, 50, 56)),
                    FontWeight = bold ? FontWeights.Bold : FontWeights.Normal };
                Grid.SetColumn(tb, col); g.Children.Add(tb);
            }

            AddCell(0, r.Vehicle, null, true);
            AddCell(1, $"{r.TotalKm:F0} km", Color.FromRgb(21, 101, 192), true);
            AddCell(2, $"{r.TotalTripMin / 60}h {r.TotalTripMin % 60}min", Color.FromRgb(46, 125, 50));
            AddCell(3, $"{r.TotalStandMin / 60}h {r.TotalStandMin % 60}min", Color.FromRgb(230, 81, 0));
            AddCell(4, r.TotalFuelL > 0 ? $"{r.TotalFuelL:F0}L" : "—");
            AddCell(5, $"{r.TotalTours} tras");

            // Sparkline — km per dzień
            var avgKm = r.ActiveDays > 0 ? r.TotalKm / r.ActiveDays : 0;
            AddCell(6, $"Śr. {avgKm:F0} km/dzień | {r.ActiveDays} dni aktywnych", Color.FromRgb(120, 144, 156));

            border.Child = g;
            ReportPanel.Children.Add(border);
        }

        private void BtnPdf_Click(object s, RoutedEventArgs e)
        {
            if (_reports.Count == 0) { StatusText.Text = "Najpierw załaduj dane"; return; }
            try
            {
                var from = DateFrom.SelectedDate ?? DateTime.Today.AddDays(-6);
                var to = DateTo.SelectedDate ?? DateTime.Today;
                var pdf = RaportEfektywnosciPDF.Generuj(_reports, from, to);
                var path = Path.Combine(Path.GetTempPath(), $"RaportEfektywnosci_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
                File.WriteAllBytes(path, pdf);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                StatusText.Text = "PDF otwarty";
            }
            catch (Exception ex) { StatusText.Text = $"Błąd PDF: {ex.Message}"; }
        }

        public class VehicleReport
        {
            public string Vehicle { get; set; } = "";
            public double TotalKm { get; set; }
            public int TotalTripMin { get; set; }
            public int TotalStandMin { get; set; }
            public double TotalFuelL { get; set; }
            public int TotalTours { get; set; }
            public int ActiveDays { get; set; }
            public List<WebfleetReportService.TripSummary> DailySummaries { get; set; } = new();
        }
    }
}
