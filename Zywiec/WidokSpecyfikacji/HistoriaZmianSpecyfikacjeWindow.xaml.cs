using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Kalendarz1
{
    /// <summary>
    /// Okno historii zmian specyfikacji z filtrowaniem i eksportem
    /// </summary>
    public partial class HistoriaZmianSpecyfikacjeWindow : Window
    {
        private readonly string _connectionString;
        private ObservableCollection<ChangeLogItem> _allChanges;
        private ICollectionView _changesView;

        public HistoriaZmianSpecyfikacjeWindow(string connectionString)
        {
            InitializeComponent();
            _connectionString = connectionString;
            _allChanges = new ObservableCollection<ChangeLogItem>();

            // Ustaw domyślne daty
            dateTo.SelectedDate = DateTime.Today;
            dateFrom.SelectedDate = DateTime.Today.AddDays(-30);

            // Załaduj dane
            LoadData();

            // Podłącz zdarzenie zmiany zaznaczenia
            dgHistory.SelectionChanged += DgHistory_SelectionChanged;
        }

        /// <summary>
        /// Ładuje dane z bazy
        /// </summary>
        private void LoadData()
        {
            try
            {
                txtStatus.Text = "Ładowanie danych...";
                _allChanges.Clear();

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string sql = @"SELECT FarmerCalcID, FieldName, OldValue, NewValue, Dostawca, ChangedBy, ChangeDate, CalcDate, Nr, CarID, UserID
                                   FROM [dbo].[FarmerCalcChangeLog]
                                   ORDER BY ChangeDate DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                _allChanges.Add(new ChangeLogItem
                                {
                                    FarmerCalcID = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                                    FieldName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    OldValue = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    NewValue = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    Dostawca = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    ChangedBy = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    ChangeDate = reader.IsDBNull(6) ? DateTime.MinValue : reader.GetDateTime(6),
                                    CalcDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                                    Nr = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                                    CarID = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                    UserID = reader.IsDBNull(10) ? "" : reader.GetString(10)
                                });
                            }
                        }
                    }
                }

                // Ustaw źródło danych i widok
                dgHistory.ItemsSource = _allChanges;
                _changesView = CollectionViewSource.GetDefaultView(_allChanges);

                // Wypełnij filtry
                PopulateFilters();

                // Zastosuj filtr
                ApplyFilter();

                txtTotal.Text = _allChanges.Count.ToString();
                txtRecordCount.Text = $"{_allChanges.Count} rekordów";
                txtStatus.Text = "Dane załadowane pomyślnie";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Błąd: {ex.Message}";
                MessageBox.Show($"Błąd ładowania danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Wypełnia listy filtrów unikalnymi wartościami
        /// </summary>
        private void PopulateFilters()
        {
            // Pola
            var fields = _allChanges.Select(c => c.FieldName).Distinct().OrderBy(f => f).ToList();
            cboField.Items.Clear();
            cboField.Items.Add(new ComboBoxItem { Content = "-- Wszystkie --", IsSelected = true });
            foreach (var field in fields)
            {
                cboField.Items.Add(new ComboBoxItem { Content = GetFieldDisplayName(field), Tag = field });
            }

            // Użytkownicy
            var users = _allChanges.Select(c => c.ChangedBy).Distinct().OrderBy(u => u).ToList();
            cboUser.Items.Clear();
            cboUser.Items.Add(new ComboBoxItem { Content = "-- Wszyscy --", IsSelected = true });
            foreach (var user in users)
            {
                cboUser.Items.Add(new ComboBoxItem { Content = user });
            }
        }

        /// <summary>
        /// Stosuje filtry do widoku
        /// </summary>
        private void ApplyFilter()
        {
            if (_changesView == null) return;

            _changesView.Filter = item =>
            {
                var change = item as ChangeLogItem;
                if (change == null) return false;

                // Filtr daty od
                if (dateFrom.SelectedDate.HasValue && change.ChangeDate < dateFrom.SelectedDate.Value)
                    return false;

                // Filtr daty do (włącznie z całym dniem)
                if (dateTo.SelectedDate.HasValue && change.ChangeDate >= dateTo.SelectedDate.Value.AddDays(1))
                    return false;

                // Filtr pola
                var selectedField = cboField.SelectedItem as ComboBoxItem;
                if (selectedField != null && selectedField.Tag != null)
                {
                    if (change.FieldName != selectedField.Tag.ToString())
                        return false;
                }

                // Filtr użytkownika
                var selectedUser = cboUser.SelectedItem as ComboBoxItem;
                if (selectedUser != null && !selectedUser.Content.ToString().StartsWith("--"))
                {
                    if (change.ChangedBy != selectedUser.Content.ToString())
                        return false;
                }

                // Filtr dostawcy (szukanie)
                if (!string.IsNullOrWhiteSpace(txtSearchSupplier.Text))
                {
                    if (!change.Dostawca.ToLower().Contains(txtSearchSupplier.Text.ToLower()))
                        return false;
                }

                return true;
            };

            _changesView.Refresh();

            int displayed = _changesView.Cast<object>().Count();
            txtDisplayed.Text = displayed.ToString();
            txtSubtitle.Text = $"Wyświetlono {displayed} z {_allChanges.Count} zmian";
        }

        /// <summary>
        /// Zwraca czytelną nazwę pola
        /// </summary>
        private string GetFieldDisplayName(string fieldName)
        {
            switch (fieldName)
            {
                // Specyfikacja - ceny
                case "Price":
                case "Cena": return "Cena";
                case "Addition":
                case "Dodatek": return "Dodatek";
                case "Loss":
                case "Ubytek": return "Ubytek";
                case "PriceTypeID": return "Typ ceny";
                case "IncDeadConf": return "PiK";
                case "TerminDni": return "Termin płatności";
                case "Opasienie": return "Opasienie";
                case "KlasaB": return "Klasa B";
                case "Szt.Dek": return "Sztuki deklarowane";
                case "Padłe": return "Padłe";
                case "CH": return "Chore";
                case "NW": return "Niedowaga";
                case "ZM": return "Zamarznięte";
                case "LUMEL": return "LUMEL";
                case "DeclI2": return "Padłe";

                // Avilog - wagi
                case "FullFarmWeight":
                case "Waga Brutto Hodowca": return "Waga Brutto Hod.";
                case "EmptyFarmWeight":
                case "Waga Tara Hodowca": return "Waga Tara Hod.";
                case "FullWeight":
                case "Waga Brutto Ubojnia": return "Waga Brutto Uboj.";
                case "EmptyWeight":
                case "Waga Tara Ubojnia": return "Waga Tara Uboj.";

                // Avilog - kilometry
                case "StartKM":
                case "KM Wyjazd": return "KM Wyjazd";
                case "StopKM":
                case "KM Powrót": return "KM Powrót";

                // Avilog - auto/naczepa
                case "CarID":
                case "Auto": return "Auto";
                case "TrailerID":
                case "Naczepa": return "Naczepa";

                // Avilog - czasy
                case "PoczatekUslugi":
                case "Początek Usługi": return "Początek Usługi";
                case "Wyjazd":
                case "Wyjazd Zakład": return "Wyjazd Zakład";
                case "DojazdHodowca":
                case "Dojazd Hodowca": return "Dojazd Hodowca";
                case "Zaladunek":
                case "Początek Załadunku": return "Początek Załad.";
                case "ZaladunekKoniec":
                case "Koniec Załadunku": return "Koniec Załad.";
                case "WyjazdHodowca":
                case "Wyjazd Hodowca": return "Wyjazd Hodowca";
                case "Przyjazd":
                case "Powrót Zakład": return "Powrót Zakład";
                case "KoniecUslugi":
                case "Koniec Usługi": return "Koniec Usługi";

                default: return fieldName;
            }
        }

        #region Event Handlers

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            dateFrom.SelectedDate = null;
            dateTo.SelectedDate = null;
            cboField.SelectedIndex = 0;
            cboUser.SelectedIndex = 0;
            txtSearchSupplier.Text = "";
            ApplyFilter();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            // Nie stosuj automatycznie - użytkownik kliknie "Filtruj"
        }

        private void BtnToday_Click(object sender, RoutedEventArgs e)
        {
            dateFrom.SelectedDate = DateTime.Today;
            dateTo.SelectedDate = DateTime.Today;
            ApplyFilter();
        }

        private void BtnLast7Days_Click(object sender, RoutedEventArgs e)
        {
            dateFrom.SelectedDate = DateTime.Today.AddDays(-7);
            dateTo.SelectedDate = DateTime.Today;
            ApplyFilter();
        }

        private void BtnThisMonth_Click(object sender, RoutedEventArgs e)
        {
            dateFrom.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dateTo.SelectedDate = DateTime.Today;
            ApplyFilter();
        }

        private void BtnAll_Click(object sender, RoutedEventArgs e)
        {
            dateFrom.SelectedDate = null;
            dateTo.SelectedDate = null;
            ApplyFilter();
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Plik CSV (*.csv)|*.csv|Wszystkie pliki (*.*)|*.*",
                    DefaultExt = ".csv",
                    FileName = $"HistoriaZmian_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Data zmiany;LP;Nr auta;Dostawca;Pole;Stara wartość;Nowa wartość;Użytkownik;UserID;Data specyfikacji;ID");

                    var items = _changesView.Cast<ChangeLogItem>().ToList();
                    foreach (var item in items)
                    {
                        sb.AppendLine($"{item.ChangeDate:dd.MM.yyyy HH:mm:ss};{item.Nr};{item.CarID};{item.Dostawca};{item.FieldDisplayName};{item.OldValue};{item.NewValue};{item.ChangedBy};{item.UserID};{item.CalcDate:dd.MM.yyyy};{item.FarmerCalcID}");
                    }

                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    txtStatus.Text = $"Wyeksportowano {items.Count} rekordów do {Path.GetFileName(dialog.FileName)}";
                    MessageBox.Show($"Wyeksportowano {items.Count} rekordów.", "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItems = dgHistory.SelectedItems.Cast<ChangeLogItem>().ToList();
                if (selectedItems.Count == 0)
                {
                    selectedItems = _changesView.Cast<ChangeLogItem>().ToList();
                }

                var sb = new StringBuilder();
                sb.AppendLine("Data zmiany\tLP\tNr auta\tDostawca\tPole\tStara wartość\tNowa wartość\tUżytkownik\tUserID");

                foreach (var item in selectedItems)
                {
                    sb.AppendLine($"{item.ChangeDate:dd.MM.yyyy HH:mm:ss}\t{item.Nr}\t{item.CarID}\t{item.Dostawca}\t{item.FieldDisplayName}\t{item.OldValue}\t{item.NewValue}\t{item.ChangedBy}\t{item.UserID}");
                }

                Clipboard.SetText(sb.ToString());
                txtStatus.Text = $"Skopiowano {selectedItems.Count} rekordów do schowka";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd kopiowania:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DgHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            txtSelected.Text = dgHistory.SelectedItems.Count.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Model danych dla wpisu w historii zmian
    /// </summary>
    public class ChangeLogItem : INotifyPropertyChanged
    {
        public int FarmerCalcID { get; set; }
        public string FieldName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string Dostawca { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangeDate { get; set; }
        public DateTime? CalcDate { get; set; }
        public int Nr { get; set; }
        public string CarID { get; set; }
        public string UserID { get; set; }

        /// <summary>
        /// Czytelna nazwa pola
        /// </summary>
        public string FieldDisplayName
        {
            get
            {
                switch (FieldName)
                {
                    // Specyfikacja - ceny
                    case "Price":
                    case "Cena": return "Cena";
                    case "Addition":
                    case "Dodatek": return "Dodatek";
                    case "Loss":
                    case "Ubytek": return "Ubytek";
                    case "PriceTypeID": return "Typ ceny";
                    case "IncDeadConf": return "PiK";
                    case "TerminDni": return "Termin płatności";
                    case "Opasienie": return "Opasienie";
                    case "KlasaB": return "Klasa B";
                    case "Szt.Dek": return "Sztuki dek.";
                    case "Padłe": return "Padłe";
                    case "CH": return "Chore";
                    case "NW": return "Niedowaga";
                    case "ZM": return "Zamarznięte";
                    case "LUMEL": return "LUMEL";
                    case "DeclI2": return "Padłe";

                    // Avilog - wagi
                    case "Waga Brutto Hodowca": return "Waga Brutto Hod.";
                    case "Waga Tara Hodowca": return "Waga Tara Hod.";
                    case "Waga Brutto Ubojnia": return "Waga Brutto Uboj.";
                    case "Waga Tara Ubojnia": return "Waga Tara Uboj.";

                    // Avilog - kilometry
                    case "KM Wyjazd": return "KM Wyjazd";
                    case "KM Powrót": return "KM Powrót";

                    // Avilog - auto/naczepa
                    case "Auto": return "Auto";
                    case "Naczepa": return "Naczepa";

                    // Avilog - czasy
                    case "Początek Usługi": return "Początek Usługi";
                    case "Wyjazd Zakład": return "Wyjazd Zakład";
                    case "Dojazd Hodowca": return "Dojazd Hodowca";
                    case "Początek Załadunku": return "Początek Załad.";
                    case "Koniec Załadunku": return "Koniec Załad.";
                    case "Wyjazd Hodowca": return "Wyjazd Hodowca";
                    case "Powrót Zakład": return "Powrót Zakład";
                    case "Koniec Usługi": return "Koniec Usługi";

                    default: return FieldName;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
