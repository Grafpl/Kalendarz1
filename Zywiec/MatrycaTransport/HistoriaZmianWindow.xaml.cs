using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Kalendarz1
{
    /// <summary>
    /// Okno historii zmian (audit log) dla Specyfikacji Surowca
    /// </summary>
    public partial class HistoriaZmianWindow : Window
    {
        private readonly string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private List<FarmerCalcChangeLogEntry> allChanges = new List<FarmerCalcChangeLogEntry>();

        public HistoriaZmianWindow()
        {
            InitializeComponent();

            // Ustaw domyslne daty
            dpDateFrom.SelectedDate = DateTime.Today;
            dpDateTo.SelectedDate = DateTime.Today;

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                allChanges.Clear();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Sprawdz czy tabela istnieje
                    string checkTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'FarmerCalcChangeLog'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTable, conn))
                    {
                        if ((int)checkCmd.ExecuteScalar() == 0)
                        {
                            MessageBox.Show(
                                "Tabela FarmerCalcChangeLog nie istnieje.\n\nUruchom skrypt SQL: Zywiec/SQL/SpecyfikacjaAudit_CreateTables.sql",
                                "Brak tabeli",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            return;
                        }
                    }

                    // Pobierz dane z widoku lub bezposrednio z tabel
                    string query = @"
                        SELECT TOP 5000
                            cl.ID,
                            cl.FarmerCalcID,
                            fc.CalcDate AS DzienUbojowy,
                            cl.FieldName,
                            cl.OldValue,
                            cl.NewValue,
                            cl.ChangedBy,
                            cl.ChangedAt,
                            cl.ChangeSource
                        FROM [dbo].[FarmerCalcChangeLog] cl
                        LEFT JOIN [dbo].[FarmerCalc] fc ON cl.FarmerCalcID = fc.ID
                        WHERE cl.ChangedAt >= @DateFrom AND cl.ChangedAt < @DateTo
                        ORDER BY cl.ChangedAt DESC";

                    DateTime dateFrom = dpDateFrom.SelectedDate ?? DateTime.Today;
                    DateTime dateTo = (dpDateTo.SelectedDate ?? DateTime.Today).AddDays(1); // Dodaj dzien dla pelnego dnia

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                        cmd.Parameters.AddWithValue("@DateTo", dateTo);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                allChanges.Add(new FarmerCalcChangeLogEntry
                                {
                                    ID = reader.GetInt32(0),
                                    FarmerCalcID = reader.GetInt32(1),
                                    DzienUbojowy = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2),
                                    FieldName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    OldValue = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    NewValue = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    ChangedBy = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    ChangedAt = reader.GetDateTime(7),
                                    ChangeSource = reader.IsDBNull(8) ? "" : reader.GetString(8)
                                });
                            }
                        }
                    }
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad podczas ladowania danych:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters()
        {
            var filtered = allChanges.AsEnumerable();

            // Filtr pola
            if (cboFieldFilter.SelectedIndex > 0)
            {
                string fieldFilter = ((ComboBoxItem)cboFieldFilter.SelectedItem).Content.ToString();
                filtered = filtered.Where(c => c.FieldName == fieldFilter);
            }

            var result = filtered.ToList();
            dgChanges.ItemsSource = result;

            // Aktualizuj statystyki
            lblRecordCount.Text = $" ({result.Count} rekordow)";
            lblPriceChanges.Text = result.Count(c => c.FieldName == "Price").ToString();
            lblLossChanges.Text = result.Count(c => c.FieldName == "Loss").ToString();
            lblPriceTypeChanges.Text = result.Count(c => c.FieldName == "PriceTypeID").ToString();
            lblOtherChanges.Text = result.Count(c => c.FieldName != "Price" && c.FieldName != "Loss" && c.FieldName != "PriceTypeID").ToString();
        }

        #region Przyciski okresu

        private void SetActivePeriodButton(Button activeBtn)
        {
            // Reset wszystkich przyciskow
            btnDzis.Style = (Style)FindResource("PeriodButtonStyle");
            btnWczoraj.Style = (Style)FindResource("PeriodButtonStyle");
            btnTydzien.Style = (Style)FindResource("PeriodButtonStyle");
            btnMiesiac.Style = (Style)FindResource("PeriodButtonStyle");

            // Ustaw aktywny
            activeBtn.Style = (Style)FindResource("PeriodButtonActiveStyle");
        }

        private void BtnDzis_Click(object sender, RoutedEventArgs e)
        {
            SetActivePeriodButton(btnDzis);
            dpDateFrom.SelectedDate = DateTime.Today;
            dpDateTo.SelectedDate = DateTime.Today;
            LoadData();
        }

        private void BtnWczoraj_Click(object sender, RoutedEventArgs e)
        {
            SetActivePeriodButton(btnWczoraj);
            dpDateFrom.SelectedDate = DateTime.Today.AddDays(-1);
            dpDateTo.SelectedDate = DateTime.Today.AddDays(-1);
            LoadData();
        }

        private void BtnTydzien_Click(object sender, RoutedEventArgs e)
        {
            SetActivePeriodButton(btnTydzien);
            dpDateFrom.SelectedDate = DateTime.Today.AddDays(-7);
            dpDateTo.SelectedDate = DateTime.Today;
            LoadData();
        }

        private void BtnMiesiac_Click(object sender, RoutedEventArgs e)
        {
            SetActivePeriodButton(btnMiesiac);
            dpDateFrom.SelectedDate = DateTime.Today.AddMonths(-1);
            dpDateTo.SelectedDate = DateTime.Today;
            LoadData();
        }

        #endregion

        #region Event handlers

        private void DateFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                LoadData();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                ApplyFilters();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = dgChanges.ItemsSource as List<FarmerCalcChangeLogEntry>;
                if (data == null || data.Count == 0)
                {
                    MessageBox.Show("Brak danych do eksportu.", "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SaveFileDialog dlg = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"HistoriaZmian_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dlg.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("ID;FarmerCalcID;DzienUbojowy;Pole;PoprzedniaWartosc;NowaWartosc;Zmienil;DataZmiany;Zrodlo");

                    foreach (var item in data)
                    {
                        sb.AppendLine($"{item.ID};{item.FarmerCalcID};{item.DzienUbojowy:yyyy-MM-dd};{item.FieldName};{item.OldValue};{item.NewValue};{item.ChangedBy};{item.ChangedAt:yyyy-MM-dd HH:mm:ss};{item.ChangeSource}");
                    }

                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Wyeksportowano {data.Count} rekordow.", "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad eksportu:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }

    /// <summary>
    /// Model danych dla wpisu w logu zmian FarmerCalc
    /// </summary>
    public class FarmerCalcChangeLogEntry
    {
        public int ID { get; set; }
        public int FarmerCalcID { get; set; }
        public DateTime? DzienUbojowy { get; set; }
        public string FieldName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }
        public string ChangeSource { get; set; }
    }
}
