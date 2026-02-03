using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Kalendarz1.CRM.Dialogs
{
    public partial class DuplicateDetectionDialog : Window
    {
        private readonly string _connectionString;
        private DataTable _duplicatesTable;
        private HashSet<string> _ignoredPairs = new();

        public DuplicateDetectionDialog(string connectionString)
        {
            InitializeComponent();
            _connectionString = connectionString;
            LoadIgnoredPairs();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void LoadIgnoredPairs()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Sprawdz czy tabela istnieje
                var cmdCheck = new SqlCommand(@"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CRM_DuplicateIgnore')
                    BEGIN
                        CREATE TABLE CRM_DuplicateIgnore (
                            ID1 INT NOT NULL,
                            ID2 INT NOT NULL,
                            DataUtworzenia DATETIME DEFAULT GETDATE(),
                            PRIMARY KEY (ID1, ID2)
                        )
                    END", conn);
                cmdCheck.ExecuteNonQuery();

                // Wczytaj zignorowane pary
                var cmdLoad = new SqlCommand("SELECT ID1, ID2 FROM CRM_DuplicateIgnore", conn);
                using var reader = cmdLoad.ExecuteReader();
                while (reader.Read())
                {
                    int id1 = (int)reader["ID1"];
                    int id2 = (int)reader["ID2"];
                    _ignoredPairs.Add($"{Math.Min(id1, id2)}_{Math.Max(id1, id2)}");
                }
            }
            catch { }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!chkTelefon.IsChecked.GetValueOrDefault() &&
                !chkNIP.IsChecked.GetValueOrDefault() &&
                !chkNazwa.IsChecked.GetValueOrDefault() &&
                !chkEmail.IsChecked.GetValueOrDefault())
            {
                MessageBox.Show("Wybierz przynajmniej jedno kryterium wyszukiwania.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SearchDuplicates();
        }

        private void SearchDuplicates()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var conditions = new List<string>();

                if (chkTelefon.IsChecked == true)
                {
                    conditions.Add(@"
                        (a.TELEFON_K IS NOT NULL AND a.TELEFON_K <> '' AND a.TELEFON_K = b.TELEFON_K)");
                }

                if (chkNIP.IsChecked == true)
                {
                    conditions.Add(@"
                        (a.NIP IS NOT NULL AND a.NIP <> '' AND LEN(a.NIP) >= 10 AND a.NIP = b.NIP)");
                }

                if (chkNazwa.IsChecked == true)
                {
                    conditions.Add(@"
                        (a.NAZWA IS NOT NULL AND a.NAZWA <> '' AND
                         (a.NAZWA = b.NAZWA OR
                          DIFFERENCE(a.NAZWA, b.NAZWA) = 4 OR
                          a.NAZWA LIKE b.NAZWA + '%' OR b.NAZWA LIKE a.NAZWA + '%'))");
                }

                if (chkEmail.IsChecked == true)
                {
                    conditions.Add(@"
                        (a.EMAIL IS NOT NULL AND a.EMAIL <> '' AND a.EMAIL = b.EMAIL)");
                }

                string whereClause = string.Join(" OR ", conditions);

                string sql = $@"
                    WITH Duplicates AS (
                        SELECT DISTINCT
                            CASE WHEN a.ID < b.ID THEN a.ID ELSE b.ID END as ID1,
                            CASE WHEN a.ID < b.ID THEN b.ID ELSE a.ID END as ID2
                        FROM OdbiorcyCRM a
                        INNER JOIN OdbiorcyCRM b ON a.ID < b.ID
                        WHERE ({whereClause})
                    )
                    SELECT
                        o.ID, o.NAZWA, o.TELEFON_K, o.NIP, o.EMAIL, o.MIASTO, o.Status,
                        d.GroupNum as DuplicateGroup
                    FROM OdbiorcyCRM o
                    INNER JOIN (
                        SELECT ID1 as ID, DENSE_RANK() OVER (ORDER BY ID1) as GroupNum FROM Duplicates
                        UNION
                        SELECT ID2 as ID, DENSE_RANK() OVER (ORDER BY ID1) as GroupNum FROM Duplicates
                    ) d ON o.ID = d.ID
                    ORDER BY d.GroupNum, o.ID";

                var cmd = new SqlCommand(sql, conn);
                _duplicatesTable = new DataTable();
                new SqlDataAdapter(cmd).Fill(_duplicatesTable);

                // Filtruj zignorowane pary
                FilterIgnoredPairs();

                dgDuplicates.ItemsSource = _duplicatesTable.DefaultView;
                txtResultCount.Text = $"Znalezione grupy duplikatow: {_duplicatesTable.AsEnumerable().Select(r => r["DuplicateGroup"]).Distinct().Count()}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad podczas wyszukiwania: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterIgnoredPairs()
        {
            // Na razie nie filtrujemy - moze byc dodane pozniej
        }

        private void DgDuplicates_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool hasSelection = dgDuplicates.SelectedItems.Count > 0;
            btnIgnore.IsEnabled = hasSelection;
            btnMerge.IsEnabled = dgDuplicates.SelectedItems.Count >= 2;

            if (hasSelection)
            {
                txtSelectedInfo.Text = $"Wybrano: {dgDuplicates.SelectedItems.Count} kontaktow";
            }
            else
            {
                txtSelectedInfo.Text = "Wybierz kontakty do polaczenia";
            }
        }

        private void BtnIgnore_Click(object sender, RoutedEventArgs e)
        {
            if (dgDuplicates.SelectedItem is DataRowView row)
            {
                try
                {
                    int groupNum = Convert.ToInt32(row["DuplicateGroup"]);

                    // Pobierz wszystkie ID z tej grupy
                    var idsInGroup = _duplicatesTable.AsEnumerable()
                        .Where(r => Convert.ToInt32(r["DuplicateGroup"]) == groupNum)
                        .Select(r => Convert.ToInt32(r["ID"]))
                        .ToList();

                    using var conn = new SqlConnection(_connectionString);
                    conn.Open();

                    // Dodaj wszystkie pary do zignorowanych
                    for (int i = 0; i < idsInGroup.Count; i++)
                    {
                        for (int j = i + 1; j < idsInGroup.Count; j++)
                        {
                            int id1 = Math.Min(idsInGroup[i], idsInGroup[j]);
                            int id2 = Math.Max(idsInGroup[i], idsInGroup[j]);

                            var cmdInsert = new SqlCommand(@"
                                IF NOT EXISTS (SELECT 1 FROM CRM_DuplicateIgnore WHERE ID1 = @id1 AND ID2 = @id2)
                                    INSERT INTO CRM_DuplicateIgnore (ID1, ID2) VALUES (@id1, @id2)", conn);
                            cmdInsert.Parameters.AddWithValue("@id1", id1);
                            cmdInsert.Parameters.AddWithValue("@id2", id2);
                            cmdInsert.ExecuteNonQuery();

                            _ignoredPairs.Add($"{id1}_{id2}");
                        }
                    }

                    // Usun z widoku
                    var rowsToRemove = _duplicatesTable.AsEnumerable()
                        .Where(r => Convert.ToInt32(r["DuplicateGroup"]) == groupNum)
                        .ToList();

                    foreach (var r in rowsToRemove)
                        _duplicatesTable.Rows.Remove(r);

                    dgDuplicates.Items.Refresh();
                    txtResultCount.Text = $"Znalezione grupy duplikatow: {_duplicatesTable.AsEnumerable().Select(r => r["DuplicateGroup"]).Distinct().Count()}";

                    MessageBox.Show("Grupa zostala oznaczona jako zignorowana.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnMerge_Click(object sender, RoutedEventArgs e)
        {
            if (dgDuplicates.SelectedItems.Count < 2)
            {
                MessageBox.Show("Wybierz co najmniej 2 kontakty do polaczenia.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedRows = dgDuplicates.SelectedItems.Cast<DataRowView>().ToList();
            var ids = selectedRows.Select(r => Convert.ToInt32(r["ID"])).OrderBy(id => id).ToList();
            var names = selectedRows.Select(r => r["NAZWA"]?.ToString()).ToList();

            var result = MessageBox.Show(
                $"Czy na pewno chcesz polaczyc {ids.Count} kontaktow?\n\n" +
                $"Kontakty:\n{string.Join("\n", names.Select((n, i) => $"  {ids[i]}: {n}"))}\n\n" +
                $"Kontakt o najnizszym ID ({ids.First()}) zostanie zachowany, pozostale zostana usuniete.\n" +
                "Wszystkie notatki i historia zostana przeniesione.",
                "Potwierdzenie laczenia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                using var transaction = conn.BeginTransaction();

                try
                {
                    int keepId = ids.First();
                    var removeIds = ids.Skip(1).ToList();

                    foreach (int removeId in removeIds)
                    {
                        // Przenies notatki
                        var cmdNotes = new SqlCommand(
                            "UPDATE NotatkiCRM SET IDOdbiorcy = @keepId WHERE IDOdbiorcy = @removeId",
                            conn, transaction);
                        cmdNotes.Parameters.AddWithValue("@keepId", keepId);
                        cmdNotes.Parameters.AddWithValue("@removeId", removeId);
                        cmdNotes.ExecuteNonQuery();

                        // Przenies historie
                        var cmdHistory = new SqlCommand(
                            "UPDATE HistoriaZmianCRM SET IDOdbiorcy = @keepId WHERE IDOdbiorcy = @removeId",
                            conn, transaction);
                        cmdHistory.Parameters.AddWithValue("@keepId", keepId);
                        cmdHistory.Parameters.AddWithValue("@removeId", removeId);
                        cmdHistory.ExecuteNonQuery();

                        // Usun duplikat
                        var cmdDelete = new SqlCommand(
                            "DELETE FROM OdbiorcyCRM WHERE ID = @removeId",
                            conn, transaction);
                        cmdDelete.Parameters.AddWithValue("@removeId", removeId);
                        cmdDelete.ExecuteNonQuery();

                        // Dodaj wpis do historii
                        var cmdLog = new SqlCommand(@"
                            INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, DataZmiany)
                            VALUES (@keepId, 'Polaczenie duplikatow', @info, GETDATE())",
                            conn, transaction);
                        cmdLog.Parameters.AddWithValue("@keepId", keepId);
                        cmdLog.Parameters.AddWithValue("@info", $"Polaczono z ID: {removeId}");
                        cmdLog.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    // Odswiez wyniki
                    SearchDuplicates();

                    MessageBox.Show(
                        $"Pomyslnie polaczono {ids.Count} kontaktow.\nZachowano kontakt ID: {keepId}",
                        "Sukces",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad podczas laczenia: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
