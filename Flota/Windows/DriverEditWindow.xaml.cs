using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Flota.Services;

namespace Kalendarz1.Flota.Windows
{
    public partial class DriverEditWindow : Window
    {
        private readonly FlotaService _svc;
        private readonly int? _gid;
        private bool _isNew;

        public DriverEditWindow(FlotaService svc, int? gid)
        {
            InitializeComponent();
            _svc = svc;
            _gid = gid;
            _isNew = !gid.HasValue;
            Title = _isNew ? "Kierowca - nowy" : "Kierowca - edycja";
            WindowIconHelper.SetIcon(this);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DpStatsOd.SelectedDate = DateTime.Today.AddMonths(-3);
            DpStatsDo.SelectedDate = DateTime.Today;

            if (!_isNew && _gid.HasValue)
            {
                await LoadDriverDataAsync();
                await LoadAssignmentsAsync();
            }
        }

        private async System.Threading.Tasks.Task LoadDriverDataAsync()
        {
            try
            {
                var row = await _svc.GetDriverByGIDAsync(_gid!.Value);
                if (row == null) { MessageBox.Show("Nie znaleziono kierowcy.", "Blad"); Close(); return; }

                Title = $"Kierowca - edycja [{row["Name"]}]";

                // Dane osobowe
                TxtImie.Text = row.Table.Columns.Contains("FirstName") && row["FirstName"] != DBNull.Value
                    ? row["FirstName"].ToString() : "";
                TxtNazwisko.Text = row.Table.Columns.Contains("LastName") && row["LastName"] != DBNull.Value
                    ? row["LastName"].ToString() : "";

                // If FirstName/LastName empty, try to split Name
                if (string.IsNullOrWhiteSpace(TxtImie.Text) && string.IsNullOrWhiteSpace(TxtNazwisko.Text))
                {
                    string name = row["Name"]?.ToString() ?? "";
                    var parts = name.Split(' ', 2);
                    TxtImie.Text = parts.Length > 0 ? parts[0] : "";
                    TxtNazwisko.Text = parts.Length > 1 ? parts[1] : "";
                }

                TxtTelefon1.Text = GetStr(row, "Phone1");
                TxtTelefon2.Text = GetStr(row, "Phone2");
                TxtEmail.Text = GetStr(row, "Email");
                TxtPESEL.Text = GetStr(row, "PESEL");
                TxtUwagi.Text = GetStr(row, "Uwagi");

                ChkHalt.IsChecked = Convert.ToBoolean(row["Halt"]);

                string typZatr = GetStr(row, "TypZatrudnienia");
                SelectComboItem(CmbTypZatrudnienia, typZatr);

                if (row.Table.Columns.Contains("DataZatrudnienia") && row["DataZatrudnienia"] != DBNull.Value)
                    DpDataZatrudnienia.SelectedDate = (DateTime)row["DataZatrudnienia"];
                if (row.Table.Columns.Contains("DataZwolnienia") && row["DataZwolnienia"] != DBNull.Value)
                    DpDataZwolnienia.SelectedDate = (DateTime)row["DataZwolnienia"];

                // Dokumenty
                TxtNrPJ.Text = GetStr(row, "NrPrawaJazdy");
                TxtKategoriePJ.Text = GetStr(row, "KategoriePrawaJazdy");
                if (row.Table.Columns.Contains("DataWaznosciPJ") && row["DataWaznosciPJ"] != DBNull.Value)
                    DpDataPJ.SelectedDate = (DateTime)row["DataWaznosciPJ"];
                TxtNrBadan.Text = GetStr(row, "NrBadanLekarskich");
                if (row.Table.Columns.Contains("DataWazBadanLek") && row["DataWazBadanLek"] != DBNull.Value)
                    DpDataBadan.SelectedDate = (DateTime)row["DataWazBadanLek"];
                TxtNrBHP.Text = GetStr(row, "NrSzkoleniaBHP");
                if (row.Table.Columns.Contains("DataWazBHP") && row["DataWazBHP"] != DBNull.Value)
                    DpDataBHP.SelectedDate = (DateTime)row["DataWazBHP"];

                UpdateDocStatusLabels();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadAssignmentsAsync()
        {
            if (!_gid.HasValue) return;
            try
            {
                var dt = await _svc.GetAssignmentsForDriverAsync(_gid.Value);
                var activeView = dt.AsEnumerable().Where(r => r["DataDo"] == DBNull.Value).CopyToDataTable_Safe();
                var histView = dt.AsEnumerable().Where(r => r["DataDo"] != DBNull.Value).CopyToDataTable_Safe();

                GridAktualnePojazdy.ItemsSource = activeView?.DefaultView;
                GridHistoriaPojazdy.ItemsSource = histView?.DefaultView;
            }
            catch { }
        }

        private void UpdateDocStatusLabels()
        {
            TxtPJStatus.Text = FormatDocStatus(DpDataPJ.SelectedDate);
            TxtPJStatus.Foreground = GetDocBrush(DpDataPJ.SelectedDate);
            TxtBadaniaStatus.Text = FormatDocStatus(DpDataBadan.SelectedDate);
            TxtBadaniaStatus.Foreground = GetDocBrush(DpDataBadan.SelectedDate);
            TxtBHPStatus.Text = FormatDocStatus(DpDataBHP.SelectedDate);
            TxtBHPStatus.Foreground = GetDocBrush(DpDataBHP.SelectedDate);
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
            string firstName = TxtImie.Text.Trim();
            string lastName = TxtNazwisko.Text.Trim();

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                MessageBox.Show("Imie i nazwisko sa wymagane.", "Walidacja",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                BtnZapisz.IsEnabled = false;
                string user = App.UserID ?? "system";

                int? typ = null;
                string typZatr = (CmbTypZatrudnienia.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (typZatr == "Agencja") typ = 1;
                else if (typZatr == "B2B") typ = 2;

                await _svc.SaveDriverAsync(
                    _gid,
                    firstName, lastName,
                    ChkHalt.IsChecked == true,
                    typ,
                    NullIfEmpty(TxtTelefon1.Text), NullIfEmpty(TxtTelefon2.Text),
                    NullIfEmpty(TxtEmail.Text), NullIfEmpty(TxtPESEL.Text),
                    NullIfEmpty(TxtNrPJ.Text), NullIfEmpty(TxtKategoriePJ.Text),
                    DpDataPJ.SelectedDate,
                    NullIfEmpty(TxtNrBadan.Text), DpDataBadan.SelectedDate,
                    NullIfEmpty(TxtNrBHP.Text), DpDataBHP.SelectedDate,
                    DpDataZatrudnienia.SelectedDate, DpDataZwolnienia.SelectedDate,
                    typZatr, NullIfEmpty(TxtUwagi.Text), null,
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
        private async void BtnPrzypiszPojazd_Click(object sender, RoutedEventArgs e)
        {
            if (!_gid.HasValue) { MessageBox.Show("Najpierw zapisz kierowce.", "Info"); return; }

            var dlg = new AssignDriverDialog(_svc, _gid.Value);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
                await LoadAssignmentsAsync();
        }

        private async void BtnZakonczPrzypisanie_Click(object sender, RoutedEventArgs e)
        {
            var row = (GridAktualnePojazdy.SelectedItem as DataRowView)?.Row;
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

        // ═══ Statystyki ═══
        private async void BtnPokazStats_Click(object sender, RoutedEventArgs e)
        {
            if (!_gid.HasValue) { TxtStatKursySkup.Text = "Brak danych (nowy kierowca)"; return; }
            if (!DpStatsOd.SelectedDate.HasValue || !DpStatsDo.SelectedDate.HasValue) return;

            try
            {
                var dt = await _svc.GetDriverStatsAsync(_gid.Value, DpStatsOd.SelectedDate.Value, DpStatsDo.SelectedDate.Value);
                if (dt.Rows.Count > 0)
                {
                    var r = dt.Rows[0];
                    int kursy = r["KursySkup"] != DBNull.Value ? Convert.ToInt32(r["KursySkup"]) : 0;
                    int km = r["KmSkup"] != DBNull.Value ? Convert.ToInt32(r["KmSkup"]) : 0;
                    decimal ton = r["TonSkup"] != DBNull.Value ? Convert.ToDecimal(r["TonSkup"]) / 1000m : 0;
                    TxtStatKursySkup.Text = $"Liczba kursow: {kursy}  |  Km lacznie: {km:N0}  |  Ton zywca: {ton:N1}";
                    TxtStatPodsumowanie.Text = $"Sredni dystans na kurs: {(kursy > 0 ? km / kursy : 0)} km\n" +
                                               $"Srednia masa na kurs: {(kursy > 0 ? ton / kursy : 0):N1} ton";
                }
            }
            catch (Exception ex)
            {
                TxtStatKursySkup.Text = $"Blad: {ex.Message}";
            }
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

        private static void SelectComboItem(ComboBox cmb, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            foreach (ComboBoxItem item in cmb.Items)
            {
                if (item.Content?.ToString() == value)
                {
                    cmb.SelectedItem = item;
                    return;
                }
            }
        }
    }

    internal static class DataTableExtensions
    {
        public static DataTable? CopyToDataTable_Safe(this System.Collections.Generic.IEnumerable<DataRow> rows)
        {
            var list = rows.ToList();
            return list.Count > 0 ? list.CopyToDataTable() : null;
        }
    }
}
