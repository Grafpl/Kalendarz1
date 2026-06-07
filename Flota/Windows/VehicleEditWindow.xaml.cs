using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Flota.Services;

namespace Kalendarz1.Flota.Windows
{
    public partial class VehicleEditWindow : Window
    {
        private readonly FlotaService _svc;
        private readonly string? _carTrailerID;
        private bool _isNew;

        public VehicleEditWindow(FlotaService svc, string? carTrailerID)
        {
            InitializeComponent();
            _svc = svc;
            _carTrailerID = carTrailerID;
            _isNew = carTrailerID == null;
            Title = _isNew ? "Pojazd - nowy" : $"Pojazd - edycja [{carTrailerID}]";
            WindowIconHelper.SetIcon(this);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isNew && _carTrailerID != null)
            {
                TxtID.IsReadOnly = true;
                TxtID.Background = System.Windows.Media.Brushes.LightGray;
                await LoadVehicleDataAsync();
                // LoadAssignmentsAsync() / LoadServiceLogsAsync() wylaczone — pobieraja z LibraNet (legacy)
                await LoadWebfleetMappingAsync();
            }
            await LoadWebfleetDostepneAsync();   // lista dla obu trybów (nowy + edycja)
        }

        // ════════════════════════════════════════════════════════════════════
        // WEBFLEET MAPPING — TransportPL.WebfleetVehicleMapping
        // PojazdID (INT) ↔ WebfleetObjectNo (varchar20)
        // ════════════════════════════════════════════════════════════════════
        private const string _connTransportLocal =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private async System.Threading.Tasks.Task LoadWebfleetMappingAsync()
        {
            if (string.IsNullOrEmpty(_carTrailerID) || !int.TryParse(_carTrailerID, out int pid)) return;
            try
            {
                await using var cn = new Microsoft.Data.SqlClient.SqlConnection(_connTransportLocal);
                await cn.OpenAsync();
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    "SELECT TOP 1 WebfleetObjectNo, DataMapowania FROM dbo.WebfleetVehicleMapping WHERE PojazdID = @id", cn);
                cmd.Parameters.AddWithValue("@id", pid);
                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    TxtWebfleetObjectNo.Text = r["WebfleetObjectNo"]?.ToString() ?? "";
                    TxtWebfleetStatus.Text = "✓ Zmapowany";
                    TxtWebfleetStatus.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#27AE60")!;
                    if (r["DataMapowania"] != DBNull.Value)
                        TxtWebfleetInfo.Text = $"Zmapowano: {((DateTime)r["DataMapowania"]):yyyy-MM-dd HH:mm}";
                }
                else
                {
                    TxtWebfleetObjectNo.Text = "";
                    TxtWebfleetStatus.Text = "⊘ Niezmapowany";
                    TxtWebfleetStatus.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#E74C3C")!;
                    TxtWebfleetInfo.Text = "Pojazd nie ma jeszcze przypisanego urządzenia GPS.";
                }
            }
            catch (Exception ex)
            {
                TxtWebfleetStatus.Text = "⚠ Błąd";
                TxtWebfleetInfo.Text = ex.Message;
            }
        }

        private async System.Threading.Tasks.Task LoadWebfleetDostepneAsync()
        {
            try
            {
                var dt = new System.Data.DataTable();
                await using var cn = new Microsoft.Data.SqlClient.SqlConnection(_connTransportLocal);
                await cn.OpenAsync();
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT m.WebfleetObjectNo, p.Rejestracja, p.Marka, p.Model,
                           CONVERT(varchar(16), m.DataMapowania, 120) AS DataMapowania
                    FROM dbo.WebfleetVehicleMapping m
                    LEFT JOIN dbo.Pojazd p ON p.PojazdID = m.PojazdID
                    ORDER BY m.DataMapowania DESC, m.WebfleetObjectNo", cn);
                await using var r = await cmd.ExecuteReaderAsync();
                dt.Load(r);
                GridWebfleetDostepne.ItemsSource = dt.DefaultView;
            }
            catch { /* tichą porażka — grid zostaje pusty */ }
        }

        private async void BtnZapiszWebfleet_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_carTrailerID) || !int.TryParse(_carTrailerID, out int pid))
            {
                MessageBox.Show("Najpierw zapisz pojazd (zakładka Dane pojazdu → ZAPISZ), potem mapuj GPS.",
                    "Wymagany zapis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string objectNo = (TxtWebfleetObjectNo.Text ?? "").Trim();
            if (string.IsNullOrEmpty(objectNo))
            {
                MessageBox.Show("Wpisz WebfleetObjectNo (numer urządzenia z Webfleet).",
                    "Brak ObjectNo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnZapiszWebfleet.IsEnabled = false;
            try
            {
                // MERGE: jeśli ten ObjectNo już istnieje, podmień PojazdID. Inaczej INSERT.
                const string sql = @"
                    MERGE dbo.WebfleetVehicleMapping AS t
                    USING (SELECT @wfNo AS WebfleetObjectNo, @pid AS PojazdID) AS s
                    ON t.WebfleetObjectNo = s.WebfleetObjectNo
                    WHEN MATCHED THEN UPDATE SET PojazdID = s.PojazdID, DataMapowania = GETDATE()
                    WHEN NOT MATCHED THEN INSERT (WebfleetObjectNo, PojazdID, DataMapowania)
                                          VALUES (s.WebfleetObjectNo, s.PojazdID, GETDATE());";
                await using var cn = new Microsoft.Data.SqlClient.SqlConnection(_connTransportLocal);
                await cn.OpenAsync();
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@wfNo", objectNo);
                cmd.Parameters.AddWithValue("@pid", pid);
                await cmd.ExecuteNonQueryAsync();

                await LoadWebfleetMappingAsync();
                await LoadWebfleetDostepneAsync();
                MessageBox.Show($"✓ Pojazd {_carTrailerID} zmapowany z Webfleet ObjectNo: {objectNo}",
                    "Mapowanie zapisane", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu mapowania:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { BtnZapiszWebfleet.IsEnabled = true; }
        }

        private async void BtnUsunWebfleet_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_carTrailerID) || !int.TryParse(_carTrailerID, out int pid)) return;

            var r = MessageBox.Show($"Usunąć mapowanie Webfleet dla pojazdu {_carTrailerID}?\n\n" +
                "Wpis w tabeli WebfleetVehicleMapping NIE zostanie usunięty — tylko odpięty od tego pojazdu " +
                "(można go potem przypisać do innego).",
                "Usunąć mapowanie?", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            BtnUsunWebfleet.IsEnabled = false;
            try
            {
                await using var cn = new Microsoft.Data.SqlClient.SqlConnection(_connTransportLocal);
                await cn.OpenAsync();
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    "UPDATE dbo.WebfleetVehicleMapping SET PojazdID = NULL WHERE PojazdID = @id", cn);
                cmd.Parameters.AddWithValue("@id", pid);
                await cmd.ExecuteNonQueryAsync();

                await LoadWebfleetMappingAsync();
                await LoadWebfleetDostepneAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { BtnUsunWebfleet.IsEnabled = true; }
        }

        private void GridWebfleetDostepne_DoubleClick(object sender, DevExpress.Xpf.Grid.RowDoubleClickEventArgs e)
        {
            try
            {
                var rowHandle = e.HitInfo.RowHandle;
                if (rowHandle < 0) return;
                var grid = GridWebfleetDostepne;
                var row = grid.GetRow(rowHandle) as System.Data.DataRowView;
                if (row == null) return;
                string obj = row["WebfleetObjectNo"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(obj))
                {
                    TxtWebfleetObjectNo.Text = obj;
                    TxtWebfleetObjectNo.Focus();
                }
            }
            catch { /* ignore */ }
        }

        private async System.Threading.Tasks.Task LoadVehicleDataAsync()
        {
            try
            {
                var row = await _svc.GetVehicleByIDAsync(_carTrailerID!);
                if (row == null) { MessageBox.Show("Nie znaleziono pojazdu.", "Blad"); Close(); return; }

                TxtID.Text = _carTrailerID;

                int kind = row["Kind"] != DBNull.Value ? Convert.ToInt32(row["Kind"]) : 1;
                SelectComboByTag(CmbKind, kind.ToString());

                TxtMarka.Text = GetStr(row, "Brand");
                TxtModel.Text = GetStr(row, "Model");

                decimal cap = row["Capacity"] != DBNull.Value ? Convert.ToDecimal(row["Capacity"]) : 0;
                TxtLadownosc.Text = cap > 0 ? cap.ToString("0") : "";

                // VehicleDetails fields
                TxtRejestracja.Text = GetStr(row, "Registration");
                TxtVIN.Text = GetStr(row, "VIN");

                int rok = row.Table.Columns.Contains("RokProdukcji") && row["RokProdukcji"] != DBNull.Value
                    ? Convert.ToInt32(row["RokProdukcji"]) : 0;
                TxtRok.Text = rok > 0 ? rok.ToString() : "";

                string typNad = GetStr(row, "TypNadwozia");
                if (!string.IsNullOrEmpty(typNad)) CmbTypNadwozia.Text = typNad;

                TxtGPS.Text = GetStr(row, "GPSModul");

                if (row.Table.Columns.Contains("DataPrzegladu") && row["DataPrzegladu"] != DBNull.Value)
                    DpPrzeglad.SelectedDate = (DateTime)row["DataPrzegladu"];
                if (row.Table.Columns.Contains("DataUbezpieczenia") && row["DataUbezpieczenia"] != DBNull.Value)
                    DpUbezpieczenie.SelectedDate = (DateTime)row["DataUbezpieczenia"];

                TxtNrOC.Text = GetStr(row, "NrPolisyOC");
                TxtNrAC.Text = GetStr(row, "NrPolisyAC");
                TxtUbezpieczyciel.Text = GetStr(row, "Ubezpieczyciel");

                int przebieg = row.Table.Columns.Contains("PrzebiegKm") && row["PrzebiegKm"] != DBNull.Value
                    ? Convert.ToInt32(row["PrzebiegKm"]) : 0;
                TxtPrzebieg.Text = przebieg > 0 ? przebieg.ToString() : "";

                decimal spalanie = row.Table.Columns.Contains("SrednieSpalanie") && row["SrednieSpalanie"] != DBNull.Value
                    ? Convert.ToDecimal(row["SrednieSpalanie"]) : 0;
                TxtSpalanie.Text = spalanie > 0 ? spalanie.ToString("0.0") : "";

                int bak = row.Table.Columns.Contains("PojemnoscBaku") && row["PojemnoscBaku"] != DBNull.Value
                    ? Convert.ToInt32(row["PojemnoscBaku"]) : 0;
                TxtBak.Text = bak > 0 ? bak.ToString() : "";

                int maxPalet = row.Table.Columns.Contains("MaxPaletH1") && row["MaxPaletH1"] != DBNull.Value
                    ? Convert.ToInt32(row["MaxPaletH1"]) : 0;
                TxtMaxPalet.Text = maxPalet > 0 ? maxPalet.ToString() : "";

                int maxE2 = row.Table.Columns.Contains("MaxPojemnikE2") && row["MaxPojemnikE2"] != DBNull.Value
                    ? Convert.ToInt32(row["MaxPojemnikE2"]) : 0;
                TxtMaxE2.Text = maxE2 > 0 ? maxE2.ToString() : "";

                decimal tempMin = row.Table.Columns.Contains("TemperaturaMin") && row["TemperaturaMin"] != DBNull.Value
                    ? Convert.ToDecimal(row["TemperaturaMin"]) : 0;
                TxtTempMin.Text = tempMin != 0 ? tempMin.ToString("0.0") : "";

                decimal tempMax = row.Table.Columns.Contains("TemperaturaMax") && row["TemperaturaMax"] != DBNull.Value
                    ? Convert.ToDecimal(row["TemperaturaMax"]) : 0;
                TxtTempMax.Text = tempMax != 0 ? tempMax.ToString("0.0") : "";

                TxtUwagi.Text = GetStr(row, "VdUwagi") != "" ? GetStr(row, "VdUwagi") : GetStr(row, "Uwagi");

                UpdateDocStatusLabels();

                Title = $"Pojazd - edycja [{_carTrailerID}] {TxtMarka.Text} {TxtRejestracja.Text}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadAssignmentsAsync()
        {
            if (_carTrailerID == null) return;
            try
            {
                var dt = await _svc.GetAssignmentsForVehicleAsync(_carTrailerID);
                var activeView = dt.AsEnumerable().Where(r => r["DataDo"] == DBNull.Value).CopyToDataTable_Safe();
                var histView = dt.AsEnumerable().Where(r => r["DataDo"] != DBNull.Value).CopyToDataTable_Safe();

                GridAktualniKierowcy.ItemsSource = activeView?.DefaultView;
                GridHistoriaKierowcy.ItemsSource = histView?.DefaultView;
            }
            catch { }
        }

        private async System.Threading.Tasks.Task LoadServiceLogsAsync()
        {
            if (_carTrailerID == null) return;
            try
            {
                string? typFilter = (CmbSerwisTyp?.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (typFilter == "Wszystkie") typFilter = null;

                var dt = await _svc.GetServiceLogsAsync(_carTrailerID, typFilter);
                GridSerwis.ItemsSource = dt.DefaultView;

                decimal totalCost = await _svc.GetServiceCostYTDAsync(_carTrailerID);
                TxtSerwisPodsumowanie.Text = $"Koszty serwisowe (rok biezacy): {totalCost:N2} PLN";
            }
            catch { }
        }

        private void UpdateDocStatusLabels()
        {
            TxtPrzegladStatus.Text = FormatDocStatus(DpPrzeglad.SelectedDate);
            TxtPrzegladStatus.Foreground = GetDocBrush(DpPrzeglad.SelectedDate);
            TxtUbezpStatus.Text = FormatDocStatus(DpUbezpieczenie.SelectedDate);
            TxtUbezpStatus.Foreground = GetDocBrush(DpUbezpieczenie.SelectedDate);
        }

        private static string FormatDocStatus(DateTime? date)
        {
            if (!date.HasValue) return "(brak danych)";
            int days = (date.Value - DateTime.Today).Days;
            if (days < 0) return $"WYGASLO {Math.Abs(days)} dni temu!";
            if (days <= 30) return $"Wygasa za {days} dni!";
            return $"Wazne ({days} dni)";
        }

        private static System.Windows.Media.Brush GetDocBrush(DateTime? date)
        {
            if (!date.HasValue) return System.Windows.Media.Brushes.Gray;
            int days = (date.Value - DateTime.Today).Days;
            if (days < 0) return new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E74C3C"));
            if (days <= 30) return new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F39C12"));
            return new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27AE60"));
        }

        // ═══ Zapisz ═══
        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            string id = TxtID.Text.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                MessageBox.Show("Symbol (ID) jest wymagany.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                BtnZapisz.IsEnabled = false;
                string user = App.UserID ?? "system";

                int kind = 1;
                if (CmbKind.SelectedItem is ComboBoxItem kindItem && kindItem.Tag != null)
                    int.TryParse(kindItem.Tag.ToString(), out kind);

                await _svc.SaveVehicleAsync(
                    id, _isNew, kind,
                    NullIfEmpty(TxtMarka.Text), NullIfEmpty(TxtModel.Text),
                    ParseDecimal(TxtLadownosc.Text),
                    NullIfEmpty(TxtRejestracja.Text), NullIfEmpty(TxtVIN.Text),
                    ParseInt(TxtRok.Text),
                    DpPrzeglad.SelectedDate, DpUbezpieczenie.SelectedDate,
                    NullIfEmpty(TxtNrOC.Text), NullIfEmpty(TxtNrAC.Text),
                    NullIfEmpty(TxtUbezpieczyciel.Text),
                    ParseInt(TxtPrzebieg.Text), ParseDecimal(TxtSpalanie.Text),
                    ParseInt(TxtBak.Text),
                    ParseInt(TxtLadownosc.Text), ParseInt(TxtMaxPalet.Text), ParseInt(TxtMaxE2.Text),
                    NullIfEmpty(CmbTypNadwozia.Text),
                    ParseDecimal(TxtTempMin.Text), ParseDecimal(TxtTempMax.Text),
                    NullIfEmpty(TxtGPS.Text), NullIfEmpty(TxtUwagi.Text), null,
                    user);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisu:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnZapisz.IsEnabled = true;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ═══ Przypisania ═══
        private async void BtnPrzypiszKierowce_Click(object sender, RoutedEventArgs e)
        {
            if (_isNew) { MessageBox.Show("Najpierw zapisz pojazd.", "Info"); return; }

            var dlg = new AssignDriverDialog(_svc, vehicleID: _carTrailerID);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
                await LoadAssignmentsAsync();
        }

        private async void BtnZakonczPrzypisanieV_Click(object sender, RoutedEventArgs e)
        {
            var row = (GridAktualniKierowcy.SelectedItem as DataRowView)?.Row;
            if (row == null) { MessageBox.Show("Zaznacz przypisanie do zakonczenia.", "Info"); return; }

            int id = Convert.ToInt32(row["ID"]);
            if (MessageBox.Show("Zakonczyc to przypisanie?", "Potwierdzenie",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try
            {
                await _svc.EndAssignmentAsync(id, DateTime.Today);
                await LoadAssignmentsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══ Serwis ═══
        private async void BtnDodajSerwis_Click(object sender, RoutedEventArgs e)
        {
            if (_isNew || _carTrailerID == null)
            {
                MessageBox.Show("Najpierw zapisz pojazd.", "Info"); return;
            }

            string display = $"{TxtMarka.Text} {TxtRejestracja.Text}".Trim();
            var dlg = new ServiceLogDialog(_svc, _carTrailerID, display, null);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
                await LoadServiceLogsAsync();
        }

        private async void CmbSerwisTyp_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_carTrailerID != null)
                await LoadServiceLogsAsync();
        }

        // ═══ Helpers ═══
        private static string GetStr(DataRow row, string col)
        {
            return row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? row[col].ToString()! : "";
        }

        private static string? NullIfEmpty(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static int? ParseInt(string text)
        {
            return int.TryParse(text?.Trim(), out int v) ? v : null;
        }

        private static decimal? ParseDecimal(string text)
        {
            text = text?.Trim().Replace(',', '.') ?? "";
            return decimal.TryParse(text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal v) ? v : null;
        }

        private static void SelectComboByTag(ComboBox cmb, string tag)
        {
            foreach (ComboBoxItem item in cmb.Items)
            {
                if (item.Tag?.ToString() == tag)
                {
                    cmb.SelectedItem = item;
                    return;
                }
            }
        }
    }
}
