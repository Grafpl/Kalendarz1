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
    public partial class MapowaniePojazdowWindow : Window
    {
        // Tabela mapowania w TransportPL (obok Pojazd, Kierowca, Kurs)
        private static readonly string _connTransport =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public ObservableCollection<InternalVehicle> InternalVehicles { get; } = new();
        private readonly List<MappingRow> _rows = new();
        private readonly List<MapaFlotyView.VehiclePosition> _webfleetVehicles;

        public MapowaniePojazdowWindow(List<MapaFlotyView.VehiclePosition> webfleetVehicles)
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
                await LoadInternalVehicles();
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
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            // Jeśli istnieje stara tabela z CarTrailerID — usuń ją
            cmd.CommandText = @"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WebfleetVehicleMapping') AND name = 'CarTrailerID')
                    DROP TABLE WebfleetVehicleMapping;

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WebfleetVehicleMapping')
                CREATE TABLE WebfleetVehicleMapping (
                    WebfleetObjectNo    varchar(20)     NOT NULL,
                    WebfleetObjectName  nvarchar(100)   NULL,
                    PojazdID            int             NULL,
                    CreatedAtUTC        datetime2       NOT NULL DEFAULT SYSUTCDATETIME(),
                    ModifiedAtUTC       datetime2       NULL,
                    ModifiedBy          nvarchar(64)    NULL,
                    CONSTRAINT PK_WebfleetVehicleMapping PRIMARY KEY (WebfleetObjectNo)
                )";
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task LoadInternalVehicles()
        {
            InternalVehicles.Clear();
            InternalVehicles.Add(new InternalVehicle { ID = -1, Display = "-- brak mapowania --" });

            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT PojazdID, Rejestracja, Marka, Model
                FROM Pojazd
                WHERE Aktywny = 1
                ORDER BY Rejestracja";

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var id = r.GetInt32(r.GetOrdinal("PojazdID"));
                var reg = r["Rejestracja"]?.ToString() ?? "";
                var marka = r["Marka"]?.ToString() ?? "";
                var model = r["Model"]?.ToString() ?? "";
                var desc = $"{marka} {model}".Trim();
                var display = !string.IsNullOrEmpty(desc) ? $"{reg} — {desc}" : reg;
                InternalVehicles.Add(new InternalVehicle { ID = id, Display = display, Registration = reg });
            }

            InternalCountText.Text = (InternalVehicles.Count - 1).ToString();
        }

        private async Task LoadExistingMappings()
        {
            var existing = new Dictionary<string, int?>();
            using (var conn = new SqlConnection(_connTransport))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT WebfleetObjectNo, PojazdID FROM WebfleetVehicleMapping";
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var wfNo = r["WebfleetObjectNo"]?.ToString() ?? "";
                    int? pid = r["PojazdID"] == DBNull.Value ? null : Convert.ToInt32(r["PojazdID"]);
                    existing[wfNo] = pid;
                }
            }

            _rows.Clear();
            foreach (var wf in _webfleetVehicles.OrderBy(v => v.ObjectName))
            {
                existing.TryGetValue(wf.ObjectNo, out var mappedId);
                _rows.Add(new MappingRow
                {
                    WebfleetObjectNo = wf.ObjectNo,
                    WebfleetObjectName = wf.ObjectName,
                    WebfleetDriver = wf.Driver,
                    PojazdID = mappedId.HasValue && mappedId > 0 ? mappedId.Value : -1
                });
            }

            MappingGrid.ItemsSource = _rows;
        }

        private void UpdateStats()
        {
            var mapped = _rows.Count(r => r.PojazdID > 0);
            MappedCountText.Text = mapped.ToString();
            UnmappedCountText.Text = (_rows.Count - mapped).ToString();
        }

        private void BtnAutoMap_Click(object sender, RoutedEventArgs e)
        {
            int matched = 0;
            foreach (var row in _rows)
            {
                if (row.PojazdID > 0) continue;

                var wfName = (row.WebfleetObjectName ?? "").ToUpper().Replace(" ", "").Replace("-", "");
                foreach (var iv in InternalVehicles)
                {
                    if (iv.ID <= 0 || string.IsNullOrEmpty(iv.Registration)) continue;
                    var reg = iv.Registration.ToUpper().Replace(" ", "").Replace("-", "");
                    if (reg.Length >= 4 && wfName.Contains(reg))
                    {
                        row.PojazdID = iv.ID;
                        matched++;
                        break;
                    }
                }
            }

            MappingGrid.Items.Refresh();
            UpdateStats();
            MessageBox.Show($"Automatycznie zmapowano {matched} pojazdów po numerach rejestracyjnych.",
                "Auto-mapowanie", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync();

                foreach (var row in _rows)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        MERGE WebfleetVehicleMapping AS t
                        USING (SELECT @wfNo AS WebfleetObjectNo) AS s
                        ON t.WebfleetObjectNo = s.WebfleetObjectNo
                        WHEN MATCHED THEN
                            UPDATE SET PojazdID = @pid, WebfleetObjectName = @wfName,
                                       ModifiedAtUTC = SYSUTCDATETIME(), ModifiedBy = @user
                        WHEN NOT MATCHED THEN
                            INSERT (WebfleetObjectNo, WebfleetObjectName, PojazdID, ModifiedBy)
                            VALUES (@wfNo, @wfName, @pid, @user);";

                    cmd.Parameters.AddWithValue("@wfNo", row.WebfleetObjectNo);
                    cmd.Parameters.AddWithValue("@wfName", (object?)row.WebfleetObjectName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pid", row.PojazdID > 0 ? row.PojazdID : DBNull.Value);
                    cmd.Parameters.AddWithValue("@user", App.UserFullName ?? "system");
                    await cmd.ExecuteNonQueryAsync();
                }

                UpdateStats();
                MessageBox.Show($"Zapisano mapowanie {_rows.Count} pojazdów.", "Sukces",
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

        // ── Modele ──────────────────────────────────────────────────────

        public class InternalVehicle
        {
            public int ID { get; set; }
            public string Display { get; set; } = "";
            public string Registration { get; set; } = "";
        }

        public class MappingRow : INotifyPropertyChanged
        {
            public string WebfleetObjectNo { get; set; } = "";
            public string WebfleetObjectName { get; set; } = "";
            public string WebfleetDriver { get; set; } = "";
            private int _pojazdId = -1;
            public int PojazdID
            {
                get => _pojazdId;
                set { _pojazdId = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PojazdID))); }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
        }
    }
}
