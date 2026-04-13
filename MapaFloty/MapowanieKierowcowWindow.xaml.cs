using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Kalendarz1.MapaFloty
{
    public partial class MapowanieKierowcowWindow : Window
    {
        private static readonly string _conn =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public ObservableCollection<InternalDriver> InternalDrivers { get; } = new();
        private readonly List<DriverMappingRow> _rows = new();
        private static readonly string WfAccount = "942879", WfUser = "Administrator", WfPass = "kaazZVY5";
        private static readonly string WfKey = "7a538868-96cf-4149-a9db-6e090de7276c";
        private static readonly string WfUrl = "https://csv.webfleet.com/extern";
        private static readonly string _auth = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{WfUser}:{WfPass}"));
        private static readonly System.Net.Http.HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

        private readonly List<MapaFlotyView.VehiclePosition> _webfleetVehicles;

        public MapowanieKierowcowWindow(List<MapaFlotyView.VehiclePosition> webfleetVehicles)
        {
            InitializeComponent();
            DataContext = this;
            _webfleetVehicles = webfleetVehicles;
            try { WindowIconHelper.SetIcon(this); } catch { }
            Loaded += async (_, _) => await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                await EnsureTableExists();
                await LoadInternalDrivers();
                await LoadExistingMappings();
                UpdateStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task EnsureTableExists()
        {
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WebfleetDriverMapping')
                CREATE TABLE WebfleetDriverMapping (
                    WebfleetDriverId    varchar(30)     NOT NULL,
                    WebfleetDriverName  nvarchar(100)   NULL,
                    KierowcaID          int             NULL,
                    CreatedAtUTC        datetime2       NOT NULL DEFAULT SYSUTCDATETIME(),
                    ModifiedAtUTC       datetime2       NULL,
                    ModifiedBy          nvarchar(64)    NULL,
                    CONSTRAINT PK_WebfleetDriverMapping PRIMARY KEY (WebfleetDriverId)
                )";
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task LoadInternalDrivers()
        {
            InternalDrivers.Clear();
            InternalDrivers.Add(new InternalDriver { ID = -1, Display = "-- brak mapowania --" });

            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT KierowcaID, Imie, Nazwisko, Telefon
                FROM Kierowca WHERE Aktywny = 1 ORDER BY Nazwisko, Imie";

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var id = r.GetInt32(r.GetOrdinal("KierowcaID"));
                var imie = r["Imie"]?.ToString() ?? "";
                var nazwisko = r["Nazwisko"]?.ToString() ?? "";
                var tel = r["Telefon"]?.ToString() ?? "";
                var display = $"{nazwisko} {imie}".Trim();
                if (!string.IsNullOrEmpty(tel)) display += $" ({tel})";
                InternalDrivers.Add(new InternalDriver { ID = id, Display = display, Nazwisko = nazwisko, Imie = imie });
            }
            InternalCountText.Text = (InternalDrivers.Count - 1).ToString();
        }

        private async Task LoadExistingMappings()
        {
            // Istniejące mapowania
            var existing = new Dictionary<string, int?>();
            using (var conn = new SqlConnection(_conn))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT WebfleetDriverId, KierowcaID FROM WebfleetDriverMapping";
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var wfId = r["WebfleetDriverId"]?.ToString() ?? "";
                    int? kid = r["KierowcaID"] == DBNull.Value ? null : Convert.ToInt32(r["KierowcaID"]);
                    existing[wfId] = kid;
                }
            }

            _rows.Clear();
            var seen = new HashSet<string>();

            // 1. Pobierz WSZYSTKICH kierowców z Webfleet API (showDriverReportExtern)
            try
            {
                var url = $"{WfUrl}?account={Uri.EscapeDataString(WfAccount)}&apikey={Uri.EscapeDataString(WfKey)}" +
                    "&lang=pl&outputformat=json&action=showDriverReportExtern";
                using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _auth);
                using var res = await _http.SendAsync(req);
                var body = await res.Content.ReadAsStringAsync();

                if (body.TrimStart().StartsWith("["))
                {
                    var arr = Newtonsoft.Json.Linq.JArray.Parse(body);
                    foreach (var o in arr)
                    {
                        var driverId = o["driverno"]?.ToString() ?? "";
                        var driverName = o["driver_name"]?.ToString() ?? o["driverno"]?.ToString() ?? "";
                        var vehicle = o["objectname"]?.ToString() ?? "";
                        if (string.IsNullOrEmpty(driverId) || !seen.Add(driverId)) continue;

                        existing.TryGetValue(driverId, out var mappedId);
                        _rows.Add(new DriverMappingRow
                        {
                            WebfleetDriverId = driverId,
                            WebfleetDriverName = driverName,
                            WebfleetVehicle = vehicle,
                            KierowcaID = mappedId.HasValue && mappedId > 0 ? mappedId.Value : -1
                        });
                    }
                }
            }
            catch { }

            // 2. Fallback — dodaj kierowców z aktualnych pojazdów (jeśli nie były w showDriverReport)
            foreach (var wf in _webfleetVehicles.OrderBy(v => v.Driver))
            {
                var driverId = wf.WebfleetDriverId;
                if (string.IsNullOrEmpty(driverId) || driverId == "—" || !seen.Add(driverId)) continue;
                existing.TryGetValue(driverId, out var mappedId);
                _rows.Add(new DriverMappingRow
                {
                    WebfleetDriverId = driverId,
                    WebfleetDriverName = wf.Driver,
                    WebfleetVehicle = wf.ObjectName,
                    KierowcaID = mappedId.HasValue && mappedId > 0 ? mappedId.Value : -1
                });
            }

            MappingGrid.ItemsSource = _rows;
        }

        private void UpdateStats()
        {
            var mapped = _rows.Count(r => r.KierowcaID > 0);
            MappedCountText.Text = mapped.ToString();
            UnmappedCountText.Text = (_rows.Count - mapped).ToString();
        }

        private void BtnAutoMap_Click(object sender, RoutedEventArgs e)
        {
            int matched = 0;
            foreach (var row in _rows)
            {
                if (row.KierowcaID > 0) continue;
                var wfName = (row.WebfleetDriverName ?? "").ToUpper().Replace(" ", "");
                foreach (var drv in InternalDrivers)
                {
                    if (drv.ID <= 0) continue;
                    var fullName = $"{drv.Nazwisko}{drv.Imie}".ToUpper().Replace(" ", "");
                    var reversed = $"{drv.Imie}{drv.Nazwisko}".ToUpper().Replace(" ", "");
                    if (wfName.Contains(fullName) || wfName.Contains(reversed) ||
                        fullName.Contains(wfName) || reversed.Contains(wfName))
                    {
                        row.KierowcaID = drv.ID;
                        matched++;
                        break;
                    }
                }
            }
            MappingGrid.Items.Refresh();
            UpdateStats();
            MessageBox.Show($"Automatycznie zmapowano {matched} kierowców po nazwiskach.",
                "Auto-mapowanie", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();
                foreach (var row in _rows)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        MERGE WebfleetDriverMapping AS t
                        USING (SELECT @wfId AS WebfleetDriverId) AS s
                        ON t.WebfleetDriverId = s.WebfleetDriverId
                        WHEN MATCHED THEN
                            UPDATE SET KierowcaID = @kid, WebfleetDriverName = @wfName,
                                       ModifiedAtUTC = SYSUTCDATETIME(), ModifiedBy = @user
                        WHEN NOT MATCHED THEN
                            INSERT (WebfleetDriverId, WebfleetDriverName, KierowcaID, ModifiedBy)
                            VALUES (@wfId, @wfName, @kid, @user);";
                    cmd.Parameters.AddWithValue("@wfId", row.WebfleetDriverId);
                    cmd.Parameters.AddWithValue("@wfName", (object?)row.WebfleetDriverName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@kid", row.KierowcaID > 0 ? row.KierowcaID : DBNull.Value);
                    cmd.Parameters.AddWithValue("@user", App.UserFullName ?? "system");
                    await cmd.ExecuteNonQueryAsync();
                }
                UpdateStats();
                MessageBox.Show($"Zapisano mapowanie {_rows.Count} kierowców.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        public class InternalDriver
        {
            public int ID { get; set; }
            public string Display { get; set; } = "";
            public string Nazwisko { get; set; } = "";
            public string Imie { get; set; } = "";
        }

        public class DriverMappingRow : INotifyPropertyChanged
        {
            public string WebfleetDriverId { get; set; } = "";
            public string WebfleetDriverName { get; set; } = "";
            public string WebfleetVehicle { get; set; } = "";
            private int _kierowcaId = -1;
            public int KierowcaID
            {
                get => _kierowcaId;
                set { _kierowcaId = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KierowcaID))); }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
        }
    }
}
