using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace Kalendarz1.MapaFloty
{
    /// <summary>
    /// Osobne okno (bez mapy) do mapowania Webfleet GPS ↔ TransportPL.Kierowca/Pojazd.
    /// Pobiera vehicles z Webfleet API bezpośrednio i otwiera istniejące dialogi
    /// MapowaniePojazdowWindow / MapowanieKierowcowWindow jako modal.
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

        private List<MapaFlotyView.VehiclePosition> _vehicles = new();

        public MapowanieWebfletHubWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            Loaded += async (_, _) => await LoadWebfleetAsync();
        }

        private async Task LoadWebfleetAsync()
        {
            try
            {
                TxtStatus.Text = "Pobieranie pojazdów i kierowców z Webfleet...";

                _vehicles = await FetchVehiclesAsync();

                int uniqueDrivers = _vehicles.Where(v => !string.IsNullOrEmpty(v.WebfleetDriverId))
                                              .Select(v => v.WebfleetDriverId).Distinct().Count();

                TxtPojazdyStats.Text = $"Webfleet: {_vehicles.Count} pojazdów";
                TxtKierowcyStats.Text = $"Webfleet: {uniqueDrivers} kierowców w GPS";

                BtnPojazdy.IsEnabled = _vehicles.Count > 0;
                BtnKierowcy.IsEnabled = _vehicles.Count > 0;

                TxtStatus.Text = $"Gotowy. Załadowano {_vehicles.Count} pojazdów z Webfleet.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Błąd pobierania z Webfleet: {ex.Message}";
                MessageBox.Show(
                    $"Nie udało się pobrać danych z Webfleet:\n{ex.Message}\n\n" +
                    "Sprawdź połączenie z internetem i konfigurację Webfleet API.",
                    "Błąd Webfleet", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

            // Webfleet zwraca array obiektów z polami objectno, objectname, drivername, driveruid, driver
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

        private void BtnPojazdy_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new MapowaniePojazdowWindow(_vehicles) { Owner = this };
            dlg.ShowDialog();
        }

        private void BtnKierowcy_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new MapowanieKierowcowWindow(_vehicles) { Owner = this };
            dlg.ShowDialog();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // Minimalny DTO dla Webfleet response
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
