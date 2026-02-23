using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Kalendarz1.Hodowcy
{
    public partial class HodowcyDuplicateWindow : Window
    {
        private readonly string _connectionString;
        private HashSet<string> _ignoredPairs = new();
        private List<DuplicateRow> _results = new();

        public HodowcyDuplicateWindow(string connectionString)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
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

                var cmdCheck = new SqlCommand(@"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Pozyskiwanie_DuplicateIgnore')
                    BEGIN
                        CREATE TABLE Pozyskiwanie_DuplicateIgnore (
                            ID1 INT NOT NULL,
                            ID2 INT NOT NULL,
                            DataUtworzenia DATETIME DEFAULT GETDATE(),
                            PRIMARY KEY (ID1, ID2)
                        )
                    END", conn);
                cmdCheck.ExecuteNonQuery();

                var cmdLoad = new SqlCommand("SELECT ID1, ID2 FROM Pozyskiwanie_DuplicateIgnore", conn);
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

        private bool IsPairIgnored(int a, int b)
        {
            int id1 = Math.Min(a, b);
            int id2 = Math.Max(a, b);
            return _ignoredPairs.Contains($"{id1}_{id2}");
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!chkTelefon.IsChecked.GetValueOrDefault() && !chkNazwa.IsChecked.GetValueOrDefault())
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
                // 1. Load all active hodowcy
                var hodowcy = new List<HodowcaRecord>();
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = "SELECT Id, Dostawca, Tel1, Tel2, Tel3, Miejscowosc, Status, Towar FROM Pozyskiwanie_Hodowcy WHERE Aktywny=1";
                    using var cmd = new SqlCommand(sql, conn);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        hodowcy.Add(new HodowcaRecord
                        {
                            Id = reader.GetInt32(0),
                            Dostawca = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            Tel1 = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Tel2 = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            Tel3 = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            Miejscowosc = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            Status = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            Towar = reader.IsDBNull(7) ? "" : reader.GetString(7)
                        });
                    }
                }

                // 2. Normalize phones per hodowca
                var phoneMap = new Dictionary<string, List<int>>(); // normalized phone → list of IDs
                var hodowcaPhones = new Dictionary<int, HashSet<string>>(); // ID → set of normalized phones

                foreach (var h in hodowcy)
                {
                    var phones = new HashSet<string>();
                    foreach (var raw in new[] { h.Tel1, h.Tel2, h.Tel3 })
                    {
                        foreach (var num in SplitPhoneNumbers(raw))
                        {
                            string normalized = NormalizePhone(num);
                            if (normalized.Length >= 7)
                            {
                                phones.Add(normalized);
                                if (!phoneMap.ContainsKey(normalized))
                                    phoneMap[normalized] = new List<int>();
                                phoneMap[normalized].Add(h.Id);
                            }
                        }
                    }
                    hodowcaPhones[h.Id] = phones;
                }

                // 3. Union-Find for grouping
                var parent = new Dictionary<int, int>();
                foreach (var h in hodowcy)
                    parent[h.Id] = h.Id;

                int Find(int x)
                {
                    while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                    return x;
                }
                void Union(int a, int b)
                {
                    int ra = Find(a), rb = Find(b);
                    if (ra != rb) parent[ra] = rb;
                }

                // Track match reasons per pair
                var matchReasons = new Dictionary<string, string>();
                void SetReason(int a, int b, string reason)
                {
                    string key = $"{Math.Min(a, b)}_{Math.Max(a, b)}";
                    if (!matchReasons.ContainsKey(key))
                        matchReasons[key] = reason;
                }

                // 4. Phone matching
                if (chkTelefon.IsChecked == true)
                {
                    foreach (var kvp in phoneMap)
                    {
                        if (kvp.Value.Count >= 2)
                        {
                            for (int i = 0; i < kvp.Value.Count; i++)
                            {
                                for (int j = i + 1; j < kvp.Value.Count; j++)
                                {
                                    int a = kvp.Value[i], b = kvp.Value[j];
                                    if (!IsPairIgnored(a, b))
                                    {
                                        Union(a, b);
                                        SetReason(a, b, "Telefon");
                                    }
                                }
                            }
                        }
                    }
                }

                // 5. Name matching (substring containment)
                if (chkNazwa.IsChecked == true)
                {
                    var sorted = hodowcy.Where(h => !string.IsNullOrWhiteSpace(h.Dostawca)).ToList();
                    for (int i = 0; i < sorted.Count; i++)
                    {
                        string nameA = sorted[i].Dostawca.Trim().ToLowerInvariant();
                        if (nameA.Length < 4) continue;

                        for (int j = i + 1; j < sorted.Count; j++)
                        {
                            string nameB = sorted[j].Dostawca.Trim().ToLowerInvariant();
                            if (nameB.Length < 4) continue;

                            bool match = nameA == nameB
                                || (nameA.Length >= 5 && nameB.Contains(nameA))
                                || (nameB.Length >= 5 && nameA.Contains(nameB));

                            if (match && !IsPairIgnored(sorted[i].Id, sorted[j].Id))
                            {
                                Union(sorted[i].Id, sorted[j].Id);
                                SetReason(sorted[i].Id, sorted[j].Id, "Nazwa");
                            }
                        }
                    }
                }

                // 6. Build groups (only groups with 2+ members)
                var groups = new Dictionary<int, List<int>>();
                foreach (var h in hodowcy)
                {
                    int root = Find(h.Id);
                    if (!groups.ContainsKey(root))
                        groups[root] = new List<int>();
                    groups[root].Add(h.Id);
                }

                var hodowcyDict = hodowcy.ToDictionary(h => h.Id);
                _results = new List<DuplicateRow>();
                int groupNum = 0;

                foreach (var grp in groups.Values.Where(g => g.Count >= 2).OrderBy(g => g.Min()))
                {
                    groupNum++;
                    foreach (var id in grp.OrderBy(x => x))
                    {
                        var h = hodowcyDict[id];
                        // Find reason for this specific hodowca in group
                        string reason = "";
                        foreach (var otherId in grp.Where(x => x != id))
                        {
                            string key = $"{Math.Min(id, otherId)}_{Math.Max(id, otherId)}";
                            if (matchReasons.TryGetValue(key, out var r))
                            {
                                reason = r;
                                break;
                            }
                        }

                        string displayPhone = FormatPhone(h.Tel1);
                        if (string.IsNullOrEmpty(displayPhone)) displayPhone = FormatPhone(h.Tel2);
                        if (string.IsNullOrEmpty(displayPhone)) displayPhone = FormatPhone(h.Tel3);

                        _results.Add(new DuplicateRow
                        {
                            Grupa = groupNum,
                            Id = h.Id,
                            Dostawca = h.Dostawca,
                            Telefon = displayPhone,
                            Miejscowosc = h.Miejscowosc,
                            Status = h.Status,
                            Towar = h.Towar,
                            MatchReason = reason
                        });
                    }
                }

                dgDuplicates.ItemsSource = _results;
                txtResultCount.Text = $"Znaleziono: {groupNum} grup duplikatów ({_results.Count} hodowców)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wyszukiwania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgDuplicates_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool hasSelection = dgDuplicates.SelectedItem is DuplicateRow;
            btnIgnore.IsEnabled = hasSelection;
            btnMerge.IsEnabled = hasSelection;

            if (hasSelection)
            {
                var selected = (DuplicateRow)dgDuplicates.SelectedItem;
                int groupCount = _results.Count(r => r.Grupa == selected.Grupa);
                txtSelectedInfo.Text = $"Wybrano grupę {selected.Grupa} ({groupCount} hodowców)";
            }
            else
            {
                txtSelectedInfo.Text = "Wybierz grupę duplikatów";
            }
        }

        private void BtnIgnore_Click(object sender, RoutedEventArgs e)
        {
            if (dgDuplicates.SelectedItem is not DuplicateRow selected) return;

            try
            {
                int groupNum = selected.Grupa;
                var idsInGroup = _results.Where(r => r.Grupa == groupNum).Select(r => r.Id).ToList();

                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                for (int i = 0; i < idsInGroup.Count; i++)
                {
                    for (int j = i + 1; j < idsInGroup.Count; j++)
                    {
                        int id1 = Math.Min(idsInGroup[i], idsInGroup[j]);
                        int id2 = Math.Max(idsInGroup[i], idsInGroup[j]);

                        var cmdInsert = new SqlCommand(@"
                            IF NOT EXISTS (SELECT 1 FROM Pozyskiwanie_DuplicateIgnore WHERE ID1 = @id1 AND ID2 = @id2)
                                INSERT INTO Pozyskiwanie_DuplicateIgnore (ID1, ID2) VALUES (@id1, @id2)", conn);
                        cmdInsert.Parameters.AddWithValue("@id1", id1);
                        cmdInsert.Parameters.AddWithValue("@id2", id2);
                        cmdInsert.ExecuteNonQuery();

                        _ignoredPairs.Add($"{id1}_{id2}");
                    }
                }

                // Remove from results
                _results.RemoveAll(r => r.Grupa == groupNum);
                dgDuplicates.ItemsSource = null;
                dgDuplicates.ItemsSource = _results;

                int remaining = _results.Select(r => r.Grupa).Distinct().Count();
                txtResultCount.Text = $"Znaleziono: {remaining} grup duplikatów ({_results.Count} hodowców)";

                MessageBox.Show("Grupa została oznaczona jako zignorowana.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnMerge_Click(object sender, RoutedEventArgs e)
        {
            if (dgDuplicates.SelectedItem is not DuplicateRow selected) return;

            int groupNum = selected.Grupa;
            var rowsInGroup = _results.Where(r => r.Grupa == groupNum).OrderBy(r => r.Id).ToList();

            if (rowsInGroup.Count < 2)
            {
                MessageBox.Show("Grupa musi zawierać co najmniej 2 hodowców.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var keepRow = rowsInGroup.First();
            var removeRows = rowsInGroup.Skip(1).ToList();

            var result = MessageBox.Show(
                $"Czy na pewno chcesz połączyć {rowsInGroup.Count} hodowców?\n\n" +
                $"Hodowcy:\n{string.Join("\n", rowsInGroup.Select(r => $"  {r.Id}: {r.Dostawca}"))}\n\n" +
                $"Zachowany zostanie hodowca ID {keepRow.Id}: {keepRow.Dostawca}\n" +
                "Aktywności zostaną przeniesione, duplikaty dezaktywowane.",
                "Potwierdzenie łączenia",
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
                    foreach (var removeRow in removeRows)
                    {
                        // Move activities
                        var cmdAkt = new SqlCommand(
                            "UPDATE Pozyskiwanie_Aktywnosci SET HodowcaId = @keepId WHERE HodowcaId = @removeId",
                            conn, transaction);
                        cmdAkt.Parameters.AddWithValue("@keepId", keepRow.Id);
                        cmdAkt.Parameters.AddWithValue("@removeId", removeRow.Id);
                        cmdAkt.ExecuteNonQuery();

                        // Fill missing Tel2/Tel3 from duplicate
                        var cmdFill = new SqlCommand(@"
                            UPDATE k SET
                                Tel2 = CASE WHEN ISNULL(k.Tel2,'') = '' THEN d.Tel1 ELSE k.Tel2 END,
                                Tel3 = CASE WHEN ISNULL(k.Tel3,'') = '' THEN
                                    CASE WHEN ISNULL(d.Tel2,'') <> '' THEN d.Tel2 ELSE d.Tel3 END
                                    ELSE k.Tel3 END
                            FROM Pozyskiwanie_Hodowcy k
                            CROSS JOIN Pozyskiwanie_Hodowcy d
                            WHERE k.Id = @keepId AND d.Id = @removeId",
                            conn, transaction);
                        cmdFill.Parameters.AddWithValue("@keepId", keepRow.Id);
                        cmdFill.Parameters.AddWithValue("@removeId", removeRow.Id);
                        cmdFill.ExecuteNonQuery();

                        // Soft-delete duplicate
                        var cmdDel = new SqlCommand(
                            "UPDATE Pozyskiwanie_Hodowcy SET Aktywny = 0 WHERE Id = @removeId",
                            conn, transaction);
                        cmdDel.Parameters.AddWithValue("@removeId", removeRow.Id);
                        cmdDel.ExecuteNonQuery();

                        // Log activity
                        var cmdLog = new SqlCommand(@"
                            INSERT INTO Pozyskiwanie_Aktywnosci (HodowcaId, TypAktywnosci, Tresc, UzytkownikId, UzytkownikNazwa, DataUtworzenia)
                            VALUES (@keepId, 'System', @tresc, @userId, @userName, GETDATE())",
                            conn, transaction);
                        cmdLog.Parameters.AddWithValue("@keepId", keepRow.Id);
                        cmdLog.Parameters.AddWithValue("@tresc", $"Połączono z duplikatem Id: {removeRow.Id} ({removeRow.Dostawca})");
                        cmdLog.Parameters.AddWithValue("@userId", App.UserID ?? "system");
                        cmdLog.Parameters.AddWithValue("@userName", App.UserFullName ?? "System");
                        cmdLog.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    // Refresh results
                    SearchDuplicates();

                    MessageBox.Show(
                        $"Pomyślnie połączono {rowsInGroup.Count} hodowców.\nZachowano: {keepRow.Id} - {keepRow.Dostawca}",
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
                MessageBox.Show($"Błąd podczas łączenia: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #region Phone Helpers

        private static List<string> SplitPhoneNumbers(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            var parts = raw.Split(new[] { ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                string digits = new string(part.Where(char.IsDigit).ToArray());
                if (digits.Length == 18)
                {
                    result.Add(digits.Substring(0, 9));
                    result.Add(digits.Substring(9, 9));
                }
                else if (digits.Length == 20 && digits.StartsWith("48"))
                {
                    result.Add(digits.Substring(2, 9));
                    result.Add(digits.Substring(13, 9));
                }
                else if (digits.Length >= 7)
                {
                    result.Add(digits);
                }
            }
            return result;
        }

        private static string NormalizePhone(string digits)
        {
            if (digits.Length == 11 && digits.StartsWith("48"))
                return digits.Substring(2);
            if (digits.StartsWith("+48") || digits.StartsWith("0048"))
            {
                string d = new string(digits.Where(char.IsDigit).ToArray());
                if (d.StartsWith("48") && d.Length >= 11)
                    return d.Substring(2, 9);
            }
            return digits.Length > 9 ? digits.Substring(digits.Length - 9) : digits;
        }

        private static string FormatPhone(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return "";
            string digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 9)
                return $"{digits.Substring(0, 3)} {digits.Substring(3, 3)} {digits.Substring(6, 3)}";
            if (digits.Length == 11 && digits.StartsWith("48"))
                return $"{digits.Substring(2, 3)} {digits.Substring(5, 3)} {digits.Substring(8, 3)}";
            return phone;
        }

        #endregion

        #region Models

        private class HodowcaRecord
        {
            public int Id { get; set; }
            public string Dostawca { get; set; }
            public string Tel1 { get; set; }
            public string Tel2 { get; set; }
            public string Tel3 { get; set; }
            public string Miejscowosc { get; set; }
            public string Status { get; set; }
            public string Towar { get; set; }
        }

        private class DuplicateRow
        {
            public int Grupa { get; set; }
            public int Id { get; set; }
            public string Dostawca { get; set; }
            public string Telefon { get; set; }
            public string Miejscowosc { get; set; }
            public string Status { get; set; }
            public string Towar { get; set; }
            public string MatchReason { get; set; }
        }

        #endregion
    }
}
