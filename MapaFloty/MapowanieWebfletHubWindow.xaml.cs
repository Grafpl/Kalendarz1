using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Kalendarz1.MapaFloty
{
    /// <summary>
    /// Osobne lekkie okno (bez mapy) do mapowania Webfleet GPS ↔ TransportPL.
    /// Pobiera vehicles z Webfleet API i pokazuje progress mapowania per typ.
    /// </summary>
    public partial class MapowanieWebfletHubWindow : Window
    {
        private static string WfAccount => Kalendarz1.Webfleet.WebfleetConfig.Account;
        private static string WfUser    => Kalendarz1.Webfleet.WebfleetConfig.User;
        private static string WfPass    => Kalendarz1.Webfleet.WebfleetConfig.Pass;
        private static string WfApiKey  => Kalendarz1.Webfleet.WebfleetConfig.ApiKey;
        private static string WfBaseUrl => Kalendarz1.Webfleet.WebfleetConfig.BaseUrl;
        private static string BasicAuth => Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{WfUser}:{WfPass}"));
        private static HttpClient Http => Kalendarz1.Webfleet.WebfleetHttp.Instance;

        private const string _connTransport =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private List<MapaFlotyView.VehiclePosition> _vehicles = new();

        public MapowanieWebfletHubWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            Loaded += async (_, _) => await LoadAllAsync();
        }

        private async Task LoadAllAsync()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            BtnPojazdy.IsEnabled = false;
            BtnKierowcy.IsEnabled = false;
            TxtPojazdyProgress.Text = "—";
            TxtKierowcyProgress.Text = "—";
            ProgressPojazdy.Width = 0;
            ProgressKierowcy.Width = 0;

            try
            {
                TxtLoading.Text = "Pobieranie pojazdów z Webfleet...";
                _vehicles = await FetchVehiclesAsync();

                TxtLoading.Text = "Liczenie zmapowanych...";
                var (mappedVeh, mappedDrv) = await FetchMappedCountsAsync();

                UpdateProgress(_vehicles.Count, mappedVeh, mappedDrv);

                BtnPojazdy.IsEnabled = _vehicles.Count > 0;
                BtnKierowcy.IsEnabled = _vehicles.Count > 0;

                TxtStatus.Text = $"Webfleet: {_vehicles.Count} pojazdów  ·  " +
                                 $"Zmapowane pojazdy: {mappedVeh}/{_vehicles.Count}  ·  " +
                                 $"Zmapowani kierowcy: {mappedDrv}  ·  " +
                                 $"Aktualizacja: {DateTime.Now:HH:mm:ss}";
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                TxtStatus.Text = $"Błąd: {ex.Message}";
                MessageBox.Show(
                    $"Nie udało się pobrać danych z Webfleet:\n{ex.Message}\n\n" +
                    "Sprawdź połączenie z internetem i konfigurację Webfleet API.",
                    "Błąd Webfleet", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateProgress(int totalWebfleet, int mappedVeh, int mappedDrv)
        {
            int uniqueDrivers = _vehicles.Where(v => !string.IsNullOrEmpty(v.WebfleetDriverId))
                                          .Select(v => v.WebfleetDriverId).Distinct().Count();
            int totalDriversOnGps = Math.Max(uniqueDrivers, 1);

            // Pojazdy progress
            TxtPojazdyProgress.Text = totalWebfleet > 0 ? $"{mappedVeh} / {totalWebfleet}" : "0 / 0";
            double pojWidth = totalWebfleet > 0 ? Math.Min(238, (double)mappedVeh / totalWebfleet * 238) : 0;
            ProgressPojazdy.Width = pojWidth;
            ProgressPojazdy.Background = ColorForPct(mappedVeh, totalWebfleet);
            TxtPojazdyProgress.Foreground = ProgressPojazdy.Background;

            // Kierowcy progress — pokazujemy zmapowanych vs unikalni z GPS
            TxtKierowcyProgress.Text = uniqueDrivers > 0 ? $"{mappedDrv} / {uniqueDrivers}" : $"{mappedDrv}";
            double drvWidth = uniqueDrivers > 0 ? Math.Min(238, (double)mappedDrv / uniqueDrivers * 238) : 0;
            ProgressKierowcy.Width = drvWidth;
            ProgressKierowcy.Background = ColorForPct(mappedDrv, uniqueDrivers);
            TxtKierowcyProgress.Foreground = ProgressKierowcy.Background;
        }

        private static SolidColorBrush ColorForPct(int done, int total)
        {
            if (total == 0) return new SolidColorBrush(Color.FromRgb(176, 190, 197));   // #B0BEC5 — szary
            double pct = (double)done / total;
            if (pct >= 1.0)  return new SolidColorBrush(Color.FromRgb(46, 125, 50));   // #2E7D32 — zielony (full)
            if (pct >= 0.7)  return new SolidColorBrush(Color.FromRgb(67, 160, 71));   // #43A047 — zielony
            if (pct >= 0.3)  return new SolidColorBrush(Color.FromRgb(245, 124, 0));   // #F57C00 — pomarańczowy
            return new SolidColorBrush(Color.FromRgb(229, 81, 0));                       // #E55100 — czerwono-pomarańczowy
        }

        private async Task<List<MapaFlotyView.VehiclePosition>> FetchVehiclesAsync()
        {
            string url = $"{WfBaseUrl}?account={Uri.EscapeDataString(WfAccount)}" +
                         $"&apikey={Uri.EscapeDataString(WfApiKey)}&lang=pl&outputformat=json" +
                         $"&action=showObjectReportExtern";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", BasicAuth);
            using var res = await Http.SendAsync(req);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync();

            var items = JsonConvert.DeserializeObject<List<WfObj>>(json) ?? new();
            return items.Select(o => new MapaFlotyView.VehiclePosition
            {
                ObjectNo = o.objectno ?? "",
                ObjectName = o.objectname ?? o.objectno ?? "?",
                Driver = !string.IsNullOrEmpty(o.drivername) ? o.drivername
                       : !string.IsNullOrEmpty(o.driver) ? o.driver : "—",
                WebfleetDriverId = o.driveruid ?? o.driver ?? ""
            }).ToList();
        }

        private async Task<(int mappedVeh, int mappedDrv)> FetchMappedCountsAsync()
        {
            int mv = 0, md = 0;
            try
            {
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        (SELECT COUNT(*) FROM WebfleetVehicleMapping WHERE PojazdID IS NOT NULL AND PojazdID > 0) AS MappedVeh,
                        (SELECT COUNT(*) FROM WebfleetDriverMapping WHERE KierowcaID IS NOT NULL AND KierowcaID > 0) AS MappedDrv";
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    mv = r["MappedVeh"] == DBNull.Value ? 0 : Convert.ToInt32(r["MappedVeh"]);
                    md = r["MappedDrv"] == DBNull.Value ? 0 : Convert.ToInt32(r["MappedDrv"]);
                }
            }
            catch { /* tabele moga jeszcze nie istniec — pokaze 0 */ }
            return (mv, md);
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllAsync();
        }

        private async void BtnPojazdy_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new MapowaniePojazdowWindow(_vehicles) { Owner = this };
            dlg.ShowDialog();
            await LoadAllAsync();  // refresh stats po zamknieciu
        }

        private async void BtnKierowcy_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new MapowanieKierowcowWindow(_vehicles) { Owner = this };
            dlg.ShowDialog();
            await LoadAllAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private class WfObj
        {
            public string? objectno { get; set; }
            public string? objectname { get; set; }
            public string? drivername { get; set; }
            public string? driveruid { get; set; }
            public string? driver { get; set; }
        }
    }
}
