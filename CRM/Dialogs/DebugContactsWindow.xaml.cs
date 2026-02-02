using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using Kalendarz1.CRM.Models;

namespace Kalendarz1.CRM.Dialogs
{
    public class DebugContactRow
    {
        public int Nr { get; set; }
        public int ID { get; set; }
        public string Nazwa { get; set; }
        public string Telefon { get; set; }
        public string Miasto { get; set; }
        public string Wojewodztwo { get; set; }
        public string PKD { get; set; }
        public string Status { get; set; }
        public string Branza { get; set; }
        public string IsImport { get; set; }
        public string ImportedBy { get; set; }
        public string Priority { get; set; }
    }

    public partial class DebugContactsWindow : Window
    {
        private readonly string _connectionString;
        private readonly HandlowiecConfigViewModel _handlowiec;

        public DebugContactsWindow(string connectionString, HandlowiecConfigViewModel handlowiec)
        {
            InitializeComponent();
            _connectionString = connectionString;
            _handlowiec = handlowiec;

            txtTitle.Text = $"Kontakty dla: {handlowiec.UserName} (ID: {handlowiec.UserID})";

            ShowConfigSummary();
            LoadContacts();
        }

        private void ShowConfigSummary()
        {
            var h = _handlowiec;

            // Config
            txtConfigEnabled.Text = $"Enabled: {(h.IsEnabled ? "TAK" : "NIE")}";
            txtConfigTimes.Text = $"Godziny: {h.Time1String}, {h.Time2String}";
            txtConfigCount.Text = $"Kontaktów/reminder: {h.ContactsPerReminder}";

            // Filters
            txtConfigOnlyNew.Text = $"Tylko nowe: {(h.ShowOnlyNewContacts ? "TAK" : "NIE")}";
            txtConfigOnlyAssigned.Text = $"Tylko przypisane: {(h.ShowOnlyAssigned ? "TAK" : "NIE")}";
            txtConfigOnlyMyImports.Text = $"Tylko moje importy: {(h.OnlyMyImports ? "TAK" : "NIE")}";

            // Territory
            if (h.SelectedWojewodztwa.Count > 0)
                txtConfigWoj.Text = $"Woj: {string.Join(", ", h.SelectedWojewodztwa)}";
            else
                txtConfigWoj.Text = "Woj: Cała Polska (brak filtra)";

            if (h.PKDPriorityCodes != null && h.PKDPriorityCodes.Count > 0)
                txtConfigPKD.Text = $"PKD: {string.Join(", ", h.PKDPriorityCodes)}";
            else
                txtConfigPKD.Text = "PKD: Brak priorytetów";

            txtConfigPKDWeight.Text = $"PKD waga: {h.PKDPriorityWeight}%";
        }

        private void LoadContacts()
        {
            var contacts = new List<DebugContactRow>();
            var sw = Stopwatch.StartNew();
            string errorMsg = null;

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Build territory JSON
                string territoryJson = null;
                if (_handlowiec.SelectedWojewodztwa.Count > 0)
                    territoryJson = JsonSerializer.Serialize(_handlowiec.SelectedWojewodztwa.ToList());

                // Show SQL params
                var sb = new StringBuilder();
                sb.Append($"EXEC GetRandomContactsForReminder @UserID='{_handlowiec.UserID}', @Count={_handlowiec.ContactsPerReminder}");
                sb.Append($", @OnlyNew={(_handlowiec.ShowOnlyNewContacts ? 1 : 0)}, @OnlyAssigned={(_handlowiec.ShowOnlyAssigned ? 1 : 0)}");
                sb.Append($", @PKDWeight={_handlowiec.PKDPriorityWeight}, @MaxAttempts={_handlowiec.MaxAttemptsPerContact}");
                sb.Append($", @CooldownDays={_handlowiec.CooldownDays}");
                sb.Append($", @Wojewodztwa={territoryJson ?? "NULL"}");
                sb.Append($", @OnlyMyImports={(_handlowiec.OnlyMyImports ? 1 : 0)}, @ImportedByUser='{_handlowiec.UserID}'");
                txtSqlParams.Text = sb.ToString();

                // Try stored procedure first
                bool usedSP = false;
                try
                {
                    var cmd = new SqlCommand("GetRandomContactsForReminder", conn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 30;
                    cmd.Parameters.AddWithValue("@UserID", _handlowiec.UserID);
                    cmd.Parameters.AddWithValue("@Count", _handlowiec.ContactsPerReminder);
                    cmd.Parameters.AddWithValue("@OnlyNew", _handlowiec.ShowOnlyNewContacts);
                    cmd.Parameters.AddWithValue("@OnlyAssigned", _handlowiec.ShowOnlyAssigned);
                    cmd.Parameters.AddWithValue("@SourcePriority", "mixed");
                    cmd.Parameters.AddWithValue("@ManualPercent", 50);
                    cmd.Parameters.AddWithValue("@PKDWeight", _handlowiec.PKDPriorityWeight);
                    cmd.Parameters.AddWithValue("@MaxAttempts", _handlowiec.MaxAttemptsPerContact);
                    cmd.Parameters.AddWithValue("@CooldownDays", _handlowiec.CooldownDays);
                    cmd.Parameters.AddWithValue("@Wojewodztwa", (object)territoryJson ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OnlyMyImports", _handlowiec.OnlyMyImports);
                    cmd.Parameters.AddWithValue("@ImportedByUser", _handlowiec.UserID);

                    using var reader = cmd.ExecuteReader();

                    // Build column map
                    var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < reader.FieldCount; i++)
                        colMap[reader.GetName(i)] = i;

                    int nr = 1;
                    while (reader.Read())
                    {
                        var row = new DebugContactRow
                        {
                            Nr = nr++,
                            ID = reader.GetInt32(0),
                            Nazwa = SafeStr(reader, 1),
                            Telefon = SafeStr(reader, 2),
                            Miasto = SafeStr(reader, 4),
                            Wojewodztwo = SafeStr(reader, 5),
                            Status = SafeStr(reader, 6),
                            Branza = SafeStr(reader, 7)
                        };

                        if (colMap.TryGetValue("PKD", out int colPkd))
                            row.PKD = SafeStr(reader, colPkd);
                        if (colMap.TryGetValue("Priority", out int colPrio))
                            row.Priority = SafeStr(reader, colPrio);
                        if (colMap.TryGetValue("IsFromImport", out int colImp))
                            row.IsImport = reader.IsDBNull(colImp) ? "" : reader.GetValue(colImp).ToString();
                        if (colMap.TryGetValue("ImportedBy", out int colImpBy))
                            row.ImportedBy = SafeStr(reader, colImpBy);

                        contacts.Add(row);
                    }
                    usedSP = true;
                }
                catch (Exception spEx)
                {
                    // SP failed - try fallback direct query
                    errorMsg = $"SP Error: {spEx.Message}\nUsing fallback query...";
                }

                // Fallback: direct query if SP not available
                if (!usedSP)
                {
                    contacts.Clear();
                    var fallbackSql = BuildFallbackQuery(territoryJson);
                    txtSqlParams.Text += $"\n\n-- FALLBACK (SP niedostępna) --\n{fallbackSql.Item1}";

                    var cmd2 = new SqlCommand(fallbackSql.Item1, conn);
                    cmd2.CommandTimeout = 30;
                    foreach (var p in fallbackSql.Item2)
                        cmd2.Parameters.Add(p);

                    using var reader2 = cmd2.ExecuteReader();
                    var colMap2 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < reader2.FieldCount; i++)
                        colMap2[reader2.GetName(i)] = i;

                    int nr = 1;
                    while (reader2.Read())
                    {
                        var row = new DebugContactRow { Nr = nr++ };
                        if (colMap2.TryGetValue("ID", out int c)) row.ID = reader2.GetInt32(c);
                        if (colMap2.TryGetValue("Nazwa", out int cn)) row.Nazwa = SafeStr(reader2, cn);
                        if (colMap2.TryGetValue("Telefon1", out int ct)) row.Telefon = SafeStr(reader2, ct);
                        if (colMap2.TryGetValue("Miasto", out int cm)) row.Miasto = SafeStr(reader2, cm);
                        if (colMap2.TryGetValue("Wojewodztwo", out int cw)) row.Wojewodztwo = SafeStr(reader2, cw);
                        if (colMap2.TryGetValue("PKD", out int cp)) row.PKD = SafeStr(reader2, cp);
                        if (colMap2.TryGetValue("Status", out int cs)) row.Status = SafeStr(reader2, cs);
                        if (colMap2.TryGetValue("Branza", out int cb)) row.Branza = SafeStr(reader2, cb);
                        if (colMap2.TryGetValue("IsFromImport", out int ci))
                            row.IsImport = reader2.IsDBNull(ci) ? "" : reader2.GetValue(ci).ToString();
                        if (colMap2.TryGetValue("ImportedBy", out int cib))
                            row.ImportedBy = SafeStr(reader2, cib);
                        contacts.Add(row);
                    }
                }
            }
            catch (Exception ex)
            {
                errorMsg = $"Błąd: {ex.Message}";
            }

            sw.Stop();

            dgContacts.ItemsSource = contacts;

            var statusParts = new List<string>();
            statusParts.Add($"Znaleziono: {contacts.Count} kontaktów");
            statusParts.Add($"Czas: {sw.ElapsedMilliseconds}ms");

            if (contacts.Count > 0)
            {
                var wojGroups = contacts.Where(c => !string.IsNullOrEmpty(c.Wojewodztwo))
                    .GroupBy(c => c.Wojewodztwo).OrderByDescending(g => g.Count());
                statusParts.Add($"Woj: {string.Join(", ", wojGroups.Select(g => $"{g.Key}({g.Count()})"))}");
            }

            if (!string.IsNullOrEmpty(errorMsg))
                statusParts.Add(errorMsg);

            txtStatus.Text = string.Join(" | ", statusParts);
            txtStatus.Foreground = contacts.Count > 0
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 185, 80))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 81, 73));
        }

        private (string, List<SqlParameter>) BuildFallbackQuery(string territoryJson)
        {
            var parameters = new List<SqlParameter>();
            var sb = new StringBuilder();

            sb.AppendLine("SELECT TOP (@Count) k.ID, k.Nazwa, k.Telefon1, k.Miasto, k.Wojewodztwo,");
            sb.AppendLine("  k.PKD, k.Status, k.Branza");

            // Check if import columns exist
            sb.AppendLine("  , CASE WHEN COL_LENGTH('crm_Kontakty','IsFromImport') IS NOT NULL THEN CAST(k.IsFromImport AS NVARCHAR) ELSE '' END as IsFromImport");
            sb.AppendLine("  , CASE WHEN COL_LENGTH('crm_Kontakty','ImportedBy') IS NOT NULL THEN k.ImportedBy ELSE '' END as ImportedBy");

            sb.AppendLine("FROM crm_Kontakty k");
            sb.AppendLine("WHERE k.Telefon1 IS NOT NULL AND k.Telefon1 <> ''");

            parameters.Add(new SqlParameter("@Count", _handlowiec.ContactsPerReminder));

            // Territory filter
            if (!string.IsNullOrEmpty(territoryJson))
            {
                try
                {
                    var woj = JsonSerializer.Deserialize<List<string>>(territoryJson);
                    if (woj != null && woj.Count > 0)
                    {
                        var wojParams = new List<string>();
                        for (int i = 0; i < woj.Count; i++)
                        {
                            var pName = $"@woj{i}";
                            wojParams.Add(pName);
                            parameters.Add(new SqlParameter(pName, woj[i]));
                        }
                        sb.AppendLine($"  AND k.Wojewodztwo IN ({string.Join(",", wojParams)})");
                    }
                }
                catch { }
            }

            sb.AppendLine("ORDER BY NEWID()");

            return (sb.ToString(), parameters);
        }

        private static string SafeStr(SqlDataReader reader, int col)
        {
            if (col < 0 || col >= reader.FieldCount) return "";
            return reader.IsDBNull(col) ? "" : reader.GetValue(col).ToString();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadContacts();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
