using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        // _autoRefreshTimer usuniety — automatyczny refresh alertow (LibraNet) wylaczony

        public WidokFlota()
        {
            _svc = new FlotaService(connectionString);
            InitializeComponent();
        }

        /// <summary>
        /// Faza 8-B — pozwala zewnętrznym wywołującym (FlotaWindow ctor)
        /// ustawić startową zakładkę: 0=Kierowcy, 1=Pojazdy, 2=Przypisania.
        /// </summary>
        public void SetStartTab(int index)
        {
            if (MainTabs != null && index >= 0 && index < MainTabs.Items.Count)
                MainTabs.SelectedIndex = index;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // ═══ TYLKO TransportPL ═══ Brak wywolan: EnsureTablesExistAsync (LibraNet sys.tables),
            // LoadAssignmentsAsync (LibraNet DriverVehicleAssignment), LoadAlertsAsync (LibraNet DriverDetails/VehicleDetails).
            var errors = new System.Collections.Generic.List<string>();

            try { await LoadDriversAsync(); }
            catch (Exception ex) { errors.Add($"[Kierowcy] {ex.GetType().Name}: {ex.Message}"); }

            try { await LoadVehiclesAsync(); }
            catch (Exception ex) { errors.Add($"[Pojazdy] {ex.GetType().Name}: {ex.Message}"); }

            // Tab Przypisania + Alerty panel — wylaczone (legacy LibraNet); nie ladujemy automatycznie.
            if (PanelAlerty != null) PanelAlerty.Visibility = Visibility.Collapsed;

            if (errors.Count > 0)
            {
                string msg = "Bledy podczas ladowania modulu Flota:\n\n" + string.Join("\n\n", errors);
                MessageBox.Show(msg, "Flota - bledy ladowania", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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

            // Status filter (Aktywny=1 vs 0)
            string status = (CmbStatusKierowcy.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Aktywni";
            if (status == "Aktywni") filters.Add("Aktywny = 1");
            else if (status == "Wstrzymani") filters.Add("Aktywny = 0");

            // Search filter — Imie/Nazwisko/Telefon
            string search = TxtSzukajKierowce?.Text?.Trim() ?? "";
            if (search.Length > 0)
            {
                string s = search.Replace("'", "''");
                filters.Add($"(Imie LIKE '%{s}%' OR Nazwisko LIKE '%{s}%' OR Telefon LIKE '%{s}%')");
            }

            dv.RowFilter = string.Join(" AND ", filters);
            GridKierowcy.ItemsSource = dv;
        }

        private async System.Threading.Tasks.Task UpdateDriverStatusAsync()
        {
            try
            {
                var (total, active, halted) = await _svc.GetDriverCountsAsync();
                if (KpiKierTotal != null) KpiKierTotal.Text = total.ToString();
                if (KpiKierAktywni != null) KpiKierAktywni.Text = active.ToString();
                if (KpiKierWstrzymani != null) KpiKierWstrzymani.Text = halted.ToString();
                TxtStatusKierowcy.Text = $"Kierowcy: {total}  ·  Aktywni: {active}  ·  Wstrzymani: {halted}";
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
                if (EmptyStateKierowca != null) EmptyStateKierowca.Visibility = Visibility.Visible;
                if (ScrollDetailKierowca != null) ScrollDetailKierowca.Visibility = Visibility.Collapsed;
                return;
            }
            if (EmptyStateKierowca != null) EmptyStateKierowca.Visibility = Visibility.Collapsed;
            if (ScrollDetailKierowca != null) ScrollDetailKierowca.Visibility = Visibility.Visible;

            // Nazwa
            TxtKierowcaNazwa.Text = row["Name"]?.ToString() ?? "—";

            // Status pill
            bool aktywny = row.Table.Columns.Contains("Aktywny") && Convert.ToBoolean(row["Aktywny"]);
            TxtKierowcaTyp.Text = aktywny ? "● Aktywny" : "● Wstrzymany";
            if (KierStatusPill != null)
            {
                KierStatusPill.Background = aktywny
                    ? new SolidColorBrush(Color.FromRgb(200, 230, 201))   // #C8E6C9
                    : new SolidColorBrush(Color.FromRgb(255, 224, 178));  // #FFE0B2
                TxtKierowcaTyp.Foreground = aktywny
                    ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                    : new SolidColorBrush(Color.FromRgb(230, 81, 0));
            }

            // Telefon
            string tel = row.Table.Columns.Contains("Telefon") ? row["Telefon"]?.ToString() ?? "" : "";
            TxtKierowcaTelefon.Text = string.IsNullOrEmpty(tel) ? "—" : tel;

            // ID
            TxtKierowcaOd.Text = $"#{row["GID"]}";

            // Daty
            TxtKierowcaPJ.Text = FormatDateOrDash(row, "UtworzonoUTC");
            TxtKierowcaBadania.Text = FormatDateOrDash(row, "ZmienionoUTC");
        }

        private static string FormatDateOrDash(System.Data.DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col)) return "—";
            if (row[col] == DBNull.Value) return "—";
            var d = (DateTime)row[col];
            // UTC → local
            return d.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
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
                var (total, active, _) = await _svc.GetVehicleCountsAsync();
                if (KpiPojTotal != null) KpiPojTotal.Text = total.ToString();
                if (KpiPojAktywne != null) KpiPojAktywne.Text = active.ToString();

                // Suma palet (z DataTable bo TransportPL.Pojazd nie ma agregatu)
                int sumaPalet = 0;
                if (_dtPojazdy != null)
                {
                    foreach (System.Data.DataRow r in _dtPojazdy.Rows)
                        if (r["MaxPaletH1"] != DBNull.Value)
                            sumaPalet += Convert.ToInt32(r["MaxPaletH1"]);
                }
                if (KpiPojPalety != null) KpiPojPalety.Text = sumaPalet.ToString();

                TxtStatusPojazdy.Text = $"Pojazdy: {total}  ·  Aktywne: {active}  ·  Suma palet H1: {sumaPalet}";
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
                if (EmptyStatePojazd != null) EmptyStatePojazd.Visibility = Visibility.Visible;
                if (ScrollDetailPojazd != null) ScrollDetailPojazd.Visibility = Visibility.Collapsed;
                return;
            }
            if (EmptyStatePojazd != null) EmptyStatePojazd.Visibility = Visibility.Collapsed;
            if (ScrollDetailPojazd != null) ScrollDetailPojazd.Visibility = Visibility.Visible;

            string brand = row["Brand"]?.ToString() ?? "";
            string model = row["Model"]?.ToString() ?? "";
            string nazwa = $"{brand} {model}".Trim();
            TxtPojazdNazwa.Text = string.IsNullOrEmpty(nazwa) ? "—" : nazwa;
            TxtPojazdRejestracja.Text = row["Registration"]?.ToString() ?? row["ID"]?.ToString() ?? "";

            // Status pill
            bool aktywny = row.Table.Columns.Contains("Aktywny") && Convert.ToBoolean(row["Aktywny"]);
            TxtPojazdTyp.Text = aktywny ? "● Aktywny" : "● Wstrzymany";
            if (PojStatusPill != null)
            {
                PojStatusPill.Background = aktywny
                    ? new SolidColorBrush(Color.FromRgb(200, 230, 201))
                    : new SolidColorBrush(Color.FromRgb(255, 224, 178));
                TxtPojazdTyp.Foreground = aktywny
                    ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                    : new SolidColorBrush(Color.FromRgb(230, 81, 0));
            }

            int palety = row.Table.Columns.Contains("MaxPaletH1") && row["MaxPaletH1"] != DBNull.Value
                ? Convert.ToInt32(row["MaxPaletH1"]) : 0;
            TxtPojazdVIN.Text = palety > 0 ? palety.ToString() : "—";
            TxtPojazdPrzeglad.Text = $"#{row["ID"]}";
            TxtPojazdOC.Text = FormatDateOrDash(row, "UtworzonoUTC");
            TxtPojazdKierowca.Text = FormatDateOrDash(row, "ZmienionoUTC");
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

        private async void GridPrzypisania_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var rowView = GridPrzypisania.SelectedItem as DataRowView;
            if (rowView == null) return;
            var row = rowView.Row;

            int driverGID = Convert.ToInt32(row["DriverGID"]);
            var dlg = new DriverEditWindow(_svc, driverGID);
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true)
            {
                await LoadAssignmentsAsync();
                await LoadDriversAsync();
                await LoadVehiclesAsync();
            }
        }

        private async void BtnDebug_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var btn = sender as Button;
                if (btn != null) { btn.IsEnabled = false; btn.Content = "Sprawdzam..."; }

                var issues = await _svc.RunDiagnosticsAsync();

                string report = "=== DIAGNOSTYKA MODULU FLOTA ===\n"
                    + $"Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n"
                    + $"Uzytkownik: {App.UserID ?? "?"}\n"
                    + new string('=', 50) + "\n\n"
                    + string.Join("\n", issues)
                    + "\n\n" + new string('=', 50)
                    + "\n(Raport skopiowany do schowka)";

                Clipboard.SetText(report);

                MessageBox.Show(report, "Diagnostyka Flota", MessageBoxButton.OK, MessageBoxImage.Information);

                if (btn != null) { btn.IsEnabled = true; btn.Content = "DEBUG"; }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad diagnostyki:\n{ex}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

                if (ItemsAlerty != null) ItemsAlerty.ItemsSource = items;
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
