using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Kalendarz1.Flota.Services;
using Kalendarz1.Flota.Windows;

namespace Kalendarz1.Flota.Views
{
    public partial class WidokFlota : UserControl
    {
        private readonly string connectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private readonly FlotaService _svc;
        private DataTable? _dtKierowcy;
        private DataTable? _dtPojazdy;
        private DataTable? _dtPrzypisania;
        private DispatcherTimer? _autoRefreshTimer;

        public WidokFlota()
        {
            _svc = new FlotaService(connectionString);
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var errors = new System.Collections.Generic.List<string>();

            try { await _svc.EnsureTablesExistAsync(); }
            catch (Exception ex) { errors.Add($"[CREATE TABLES] {ex.GetType().Name}: {ex.Message}"); }

            try { await LoadDriversAsync(); }
            catch (Exception ex) { errors.Add($"[Kierowcy] {ex.GetType().Name}: {ex.Message}"); }

            try { await LoadVehiclesAsync(); }
            catch (Exception ex) { errors.Add($"[Pojazdy] {ex.GetType().Name}: {ex.Message}"); }

            try { await LoadAssignmentsAsync(); }
            catch (Exception ex) { errors.Add($"[Przypisania] {ex.GetType().Name}: {ex.Message}"); }

            try { await LoadAlertsAsync(); }
            catch (Exception ex) { errors.Add($"[Alerty] {ex.GetType().Name}: {ex.Message}"); }

            if (errors.Count > 0)
            {
                string msg = "Bledy podczas ladowania modulu Flota:\n\n" + string.Join("\n\n", errors)
                    + "\n\nUpewnij sie, ze tabele zostaly utworzone (Flota/SQL/CreateFlotaTables.sql)."
                    + "\n\n(Ctrl+C na tym oknie skopiuje tresc do schowka)";
                MessageBox.Show(msg, "Flota - bledy ladowania", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            _autoRefreshTimer.Tick += async (s, a) => { try { await LoadAlertsAsync(); } catch { } };
            _autoRefreshTimer.Start();
        }

        // ══════════════════════════════════════════════════════════════
        // KIEROWCY - Loading & Filtering
        // ══════════════════════════════════════════════════════════════

        private async System.Threading.Tasks.Task LoadDriversAsync()
        {
            _dtKierowcy = await _svc.GetDriversAsync();
            ApplyDriverFilter();
            await UpdateDriverStatusAsync();
        }

        private void ApplyDriverFilter()
        {
            if (_dtKierowcy == null) return;

            var dv = _dtKierowcy.DefaultView;
            var filters = new System.Collections.Generic.List<string>();

            // Status filter
            string status = (CmbStatusKierowcy.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Aktywni";
            if (status == "Aktywni") filters.Add("Halt = 0");
            else if (status == "Wstrzymani") filters.Add("Halt = 1");

            // Type filter
            string typ = (CmbTypKierowcy.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Wszyscy";
            if (typ != "Wszyscy") filters.Add($"TypZatrudnienia = '{typ}'");

            // Search filter
            string search = TxtSzukajKierowce?.Text?.Trim() ?? "";
            if (search.Length > 0)
                filters.Add($"(Name LIKE '%{search.Replace("'", "''")}%' OR Phone1 LIKE '%{search.Replace("'", "''")}%')");

            dv.RowFilter = string.Join(" AND ", filters);
            GridKierowcy.ItemsSource = dv;
        }

        private async System.Threading.Tasks.Task UpdateDriverStatusAsync()
        {
            try
            {
                var (total, active, halted) = await _svc.GetDriverCountsAsync();
                TxtStatusKierowcy.Text = $"Kierowcow: {total} (aktywnych: {active}, wstrzymanych: {halted})";
            }
            catch { }
        }

        private void TxtSzukajKierowce_TextChanged(object sender, TextChangedEventArgs e) { if (IsLoaded) ApplyDriverFilter(); }
        private void FilterKierowcy_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) ApplyDriverFilter(); }

        private void GridKierowcy_SelectedItemChanged(object sender, DevExpress.Xpf.Grid.SelectedItemChangedEventArgs e)
        {
            UpdateDriverDetailPanel();
        }

        private void UpdateDriverDetailPanel()
        {
            var row = GetSelectedDriverRow();
            if (row == null)
            {
                TxtKierowcaNazwa.Text = "";
                TxtKierowcaTelefon.Text = "";
                TxtKierowcaTyp.Text = "";
                TxtKierowcaOd.Text = "";
                TxtKierowcaPJ.Text = "";
                TxtKierowcaBadania.Text = "";
                TxtKierowcaBHP.Text = "";
                TxtKierowcaPojazdy.Text = "";
                TxtKierowcaStats.Text = "";
                return;
            }

            TxtKierowcaNazwa.Text = row["Name"]?.ToString() ?? "";
            TxtKierowcaTelefon.Text = $"Tel: {row["Phone1"]}";
            TxtKierowcaTyp.Text = $"Typ: {row["TypZatrudnienia"]}";

            var dataZatr = row["DataZatrudnienia"];
            TxtKierowcaOd.Text = dataZatr != DBNull.Value ? $"Od: {((DateTime)dataZatr):yyyy-MM-dd}" : "";

            TxtKierowcaPJ.Text = FormatDocDate(row, "DataWaznosciPJ", "KategoriePrawaJazdy");
            TxtKierowcaBadania.Text = FormatDocDate(row, "DataWazBadanLek");
            TxtKierowcaBHP.Text = FormatDocDate(row, "DataWazBHP");

            TxtKierowcaPojazdy.Text = row["AktualneAuta"]?.ToString() ?? "(brak)";

            int kursy = row["KursySkup30d"] != DBNull.Value ? Convert.ToInt32(row["KursySkup30d"]) : 0;
            int km = row["Km30d"] != DBNull.Value ? Convert.ToInt32(row["Km30d"]) : 0;
            TxtKierowcaStats.Text = $"Kursy skup: {kursy}\nKm lacznie: {km:N0}";
        }

        private static string FormatDocDate(DataRow row, string dateField, string? extraField = null)
        {
            var val = row[dateField];
            if (val == DBNull.Value) return "(brak danych)";
            var date = (DateTime)val;
            int days = (date - DateTime.Today).Days;
            string extra = extraField != null && row[extraField] != DBNull.Value
                ? $"{row[extraField]} " : "";
            string icon = days < 0 ? "WYGASLO!" : days <= 30 ? $"zostalo {days} dni!" : $"wazne ({days} dni)";
            return $"{extra}do {date:yyyy-MM-dd} - {icon}";
        }

        private DataRowView? GetSelectedDriverRowView()
        {
            return GridKierowcy.SelectedItem as DataRowView;
        }

        private DataRow? GetSelectedDriverRow()
        {
            return GetSelectedDriverRowView()?.Row;
        }

        // ══════════════════════════════════════════════════════════════
        // KIEROWCY - Actions
        // ══════════════════════════════════════════════════════════════

        private async void BtnDodajKierowce_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new DriverEditWindow(_svc, null);
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true)
                await LoadDriversAsync();
        }

        private async void BtnEdytujKierowce_Click(object sender, RoutedEventArgs e)
        {
            var row = GetSelectedDriverRow();
            if (row == null) { MessageBox.Show("Zaznacz kierowce.", "Info"); return; }
            int gid = Convert.ToInt32(row["GID"]);

            var dlg = new DriverEditWindow(_svc, gid);
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true)
                await LoadDriversAsync();
        }

        private async void GridKierowcy_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            BtnEdytujKierowce_Click(sender, e);
        }

        private async void BtnWstrzymajKierowce_Click(object sender, RoutedEventArgs e)
        {
            var row = GetSelectedDriverRow();
            if (row == null) { MessageBox.Show("Zaznacz kierowce.", "Info"); return; }
            int gid = Convert.ToInt32(row["GID"]);
            bool halt = Convert.ToBoolean(row["Halt"]);
            string action = halt ? "aktywowac" : "wstrzymac";

            if (MessageBox.Show($"Czy chcesz {action} kierowce {row["Name"]}?", "Potwierdzenie",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                string user = App.UserID ?? "system";
                await _svc.ToggleDriverHaltAsync(gid, user);
                await LoadDriversAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExcelKierowcy_Click(object sender, RoutedEventArgs e)
        {
            ExportToExcel(GridKierowcy, "Kierowcy");
        }

        private async void BtnOdswiezKierowcy_Click(object sender, RoutedEventArgs e)
        {
            try { await LoadDriversAsync(); }
            catch (Exception ex) { MessageBox.Show(ex.ToString(), "Blad", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // ══════════════════════════════════════════════════════════════
        // POJAZDY - Loading & Filtering
        // ══════════════════════════════════════════════════════════════

        private async System.Threading.Tasks.Task LoadVehiclesAsync()
        {
            _dtPojazdy = await _svc.GetVehiclesAsync();
            ApplyVehicleFilter();
            await UpdateVehicleStatusAsync();
        }

        private void ApplyVehicleFilter()
        {
            if (_dtPojazdy == null) return;

            var dv = _dtPojazdy.DefaultView;
            var filters = new System.Collections.Generic.List<string>();

            string typ = (CmbTypPojazdu.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Wszystkie";
            if (typ == "Samochody") filters.Add("Kind = 1");
            else if (typ == "Naczepy") filters.Add("Kind = 2");

            string search = TxtSzukajPojazdy?.Text?.Trim() ?? "";
            if (search.Length > 0)
                filters.Add($"(ID LIKE '%{search.Replace("'", "''")}%' OR Registration LIKE '%{search.Replace("'", "''")}%' OR Brand LIKE '%{search.Replace("'", "''")}%')");

            dv.RowFilter = string.Join(" AND ", filters);
            GridPojazdy.ItemsSource = dv;
        }

        private async System.Threading.Tasks.Task UpdateVehicleStatusAsync()
        {
            try
            {
                var (total, cars, trailers) = await _svc.GetVehicleCountsAsync();
                TxtStatusPojazdy.Text = $"Pojazdow: {total} (aut: {cars}, naczep: {trailers})";
            }
            catch { }
        }

        private void TxtSzukajPojazdy_TextChanged(object sender, TextChangedEventArgs e) { if (IsLoaded) ApplyVehicleFilter(); }
        private void FilterPojazdy_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) ApplyVehicleFilter(); }

        private void GridPojazdy_SelectedItemChanged(object sender, DevExpress.Xpf.Grid.SelectedItemChangedEventArgs e)
        {
            UpdateVehicleDetailPanel();
        }

        private void UpdateVehicleDetailPanel()
        {
            var row = GetSelectedVehicleRow();
            if (row == null)
            {
                TxtPojazdNazwa.Text = "";
                TxtPojazdRejestracja.Text = "";
                TxtPojazdTyp.Text = "";
                TxtPojazdVIN.Text = "";
                TxtPojazdPrzeglad.Text = "";
                TxtPojazdOC.Text = "";
                TxtPojazdKierowca.Text = "";
                TxtPojazdSerwis.Text = "";
                TxtPojazdKoszty.Text = "";
                return;
            }

            string brand = row["Brand"]?.ToString() ?? "";
            string model = row["Model"]?.ToString() ?? "";
            TxtPojazdNazwa.Text = $"{brand} {model}".Trim();
            TxtPojazdRejestracja.Text = row["Registration"]?.ToString() ?? row["ID"]?.ToString() ?? "";

            int kind = row["Kind"] != DBNull.Value ? Convert.ToInt32(row["Kind"]) : 0;
            TxtPojazdTyp.Text = kind == 1 ? "Samochod" : kind == 2 ? "Naczepa" : $"Typ {kind}";

            TxtPojazdVIN.Text = row["VIN"] != DBNull.Value ? $"VIN: {row["VIN"]}" : "";

            TxtPojazdPrzeglad.Text = row["DataPrzegladu"] != DBNull.Value
                ? FormatDocDate(row, "DataPrzegladu")
                : "Przeglad: (brak danych)";

            TxtPojazdOC.Text = row["DataUbezpieczenia"] != DBNull.Value
                ? FormatDocDate(row, "DataUbezpieczenia")
                : "OC/AC: (brak danych)";

            TxtPojazdKierowca.Text = row["AktualnyKierowca"]?.ToString() ?? "(brak przypisanego)";
            TxtPojazdSerwis.Text = row["OstatniSerwis"]?.ToString() ?? "(brak wpisow)";

            decimal koszty = row["KosztyYTD"] != DBNull.Value ? Convert.ToDecimal(row["KosztyYTD"]) : 0;
            TxtPojazdKoszty.Text = $"{koszty:N2} PLN (rok biezacy)";
        }

        private DataRow? GetSelectedVehicleRow()
        {
            return (GridPojazdy.SelectedItem as DataRowView)?.Row;
        }

        // ══════════════════════════════════════════════════════════════
        // POJAZDY - Actions
        // ══════════════════════════════════════════════════════════════

        private async void BtnDodajPojazd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new VehicleEditWindow(_svc, null);
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true)
                await LoadVehiclesAsync();
        }

        private async void BtnEdytujPojazd_Click(object sender, RoutedEventArgs e)
        {
            var row = GetSelectedVehicleRow();
            if (row == null) { MessageBox.Show("Zaznacz pojazd.", "Info"); return; }
            string id = row["ID"]?.ToString()!;

            var dlg = new VehicleEditWindow(_svc, id);
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true)
                await LoadVehiclesAsync();
        }

        private void GridPojazdy_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            BtnEdytujPojazd_Click(sender, e);
        }

        private async void BtnSerwis_Click(object sender, RoutedEventArgs e)
        {
            var row = GetSelectedVehicleRow();
            if (row == null) { MessageBox.Show("Zaznacz pojazd.", "Info"); return; }
            string id = row["ID"]?.ToString()!;
            string display = $"{row["Brand"]} {row["Registration"] ?? id}".Trim();

            var dlg = new ServiceLogDialog(_svc, id, display, null);
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true)
                await LoadVehiclesAsync();
        }

        private async void BtnTankowanie_Click(object sender, RoutedEventArgs e)
        {
            var row = GetSelectedVehicleRow();
            if (row == null) { MessageBox.Show("Zaznacz pojazd.", "Info"); return; }
            string id = row["ID"]?.ToString()!;
            string display = $"{row["Brand"]} {row["Registration"] ?? id}".Trim();

            var dlg = new ServiceLogDialog(_svc, id, display, "Tankowanie");
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true)
                await LoadVehiclesAsync();
        }

        private void BtnExcelPojazdy_Click(object sender, RoutedEventArgs e)
        {
            ExportToExcel(GridPojazdy, "Pojazdy");
        }

        private async void BtnOdswiezPojazdy_Click(object sender, RoutedEventArgs e)
        {
            try { await LoadVehiclesAsync(); }
            catch (Exception ex) { MessageBox.Show(ex.ToString(), "Blad", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // ══════════════════════════════════════════════════════════════
        // PRZYPISANIA
        // ══════════════════════════════════════════════════════════════

        private async System.Threading.Tasks.Task LoadAssignmentsAsync()
        {
            bool onlyActive = (CmbHistoriaPrzyp?.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Aktualne";
            _dtPrzypisania = await _svc.GetAssignmentsAsync(onlyActive);
            GridPrzypisania.ItemsSource = _dtPrzypisania.DefaultView;
            await UpdateAssignmentStatusAsync();
        }

        private async System.Threading.Tasks.Task UpdateAssignmentStatusAsync()
        {
            try
            {
                var (active, freeD, freeV) = await _svc.GetAssignmentCountsAsync();
                TxtStatusPrzypisania.Text = $"Aktywnych przypisan: {active} | Wolnych kierowcow: {freeD} | Wolnych pojazdow: {freeV}";
            }
            catch { }
        }

        private async void FilterPrzypisania_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await LoadAssignmentsAsync();
        }

        private async void BtnPrzypiszKierowce_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AssignDriverDialog(_svc);
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true)
            {
                await LoadAssignmentsAsync();
                await LoadDriversAsync();
                await LoadVehiclesAsync();
            }
        }

        private async void BtnOdswiezPrzypisania_Click(object sender, RoutedEventArgs e)
        {
            try { await LoadAssignmentsAsync(); }
            catch (Exception ex) { MessageBox.Show(ex.ToString(), "Blad", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // ══════════════════════════════════════════════════════════════
        // ALERTY
        // ══════════════════════════════════════════════════════════════

        private async System.Threading.Tasks.Task LoadAlertsAsync()
        {
            try
            {
                var dtAlerts = await _svc.GetAlertsAsync();
                if (dtAlerts.Rows.Count == 0)
                {
                    PanelAlerty.Visibility = Visibility.Collapsed;
                    return;
                }

                var items = dtAlerts.AsEnumerable().Select(r =>
                {
                    int days = r["DniDoWygasniecia"] != DBNull.Value ? Convert.ToInt32(r["DniDoWygasniecia"]) : 0;
                    string icon = days < 0 ? "!!!" : "!!!";
                    string daysText = days < 0 ? $"wygaslo {Math.Abs(days)} dni temu!" : $"wygasa za {days} dni";
                    return new AlertItem
                    {
                        Icon = icon,
                        Text = $"{r["Typ"]}: {r["Kto"]} - {r["Dokument"]} {daysText} ({r["DataWaznosci"]:yyyy-MM-dd})"
                    };
                }).ToList();

                ListAlerty.ItemsSource = items;
                PanelAlerty.Visibility = Visibility.Visible;
            }
            catch { PanelAlerty.Visibility = Visibility.Collapsed; }
        }

        // ══════════════════════════════════════════════════════════════
        // EXCEL EXPORT
        // ══════════════════════════════════════════════════════════════

        private void ExportToExcel(DevExpress.Xpf.Grid.GridControl grid, string name)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"Flota_{name}_{DateTime.Now:yyyyMMdd}",
                    DefaultExt = ".xlsx",
                    Filter = "Excel|*.xlsx"
                };
                if (dlg.ShowDialog() == true)
                {
                    grid.View.ExportToXlsx(dlg.FileName);
                    MessageBox.Show($"Wyeksportowano do:\n{dlg.FileName}", "Eksport",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad eksportu:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class AlertItem
    {
        public string Icon { get; set; } = "";
        public string Text { get; set; } = "";
    }
}
