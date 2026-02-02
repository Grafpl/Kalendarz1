using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Kalendarz1.CRM.Models;
using Kalendarz1.CRM.Services;
using Kalendarz1.CRM;

namespace Kalendarz1.CRM.Dialogs
{
    public partial class EditHandlowiecWindow : Window
    {
        private readonly string _connectionString;
        private readonly HandlowiecConfigViewModel _handlowiec;
        private ObservableCollection<PKDPriorityItem> _pkdPriorities;
        private ObservableCollection<PKDItem> _availablePKD;

        public EditHandlowiecWindow(string connectionString, HandlowiecConfigViewModel handlowiec)
        {
            InitializeComponent();
            _connectionString = connectionString;
            _handlowiec = handlowiec;

            txtUserName.Text = handlowiec.UserName;
            InitializeControls();
            LoadWojewodztwa();
            LoadPKDPriorities();
            LoadAvailablePKD();
        }

        private void InitializeControls()
        {
            // Populate time combos with common times (user can also type any time like 07:45)
            var times = Enumerable.Range(6, 18).SelectMany(h => new[] { $"{h:D2}:00", $"{h:D2}:15", $"{h:D2}:30", $"{h:D2}:45" }).ToList();
            foreach (var t in times)
            {
                cmbTime1.Items.Add(t);
                cmbTime2.Items.Add(t);
            }

            // Populate counts
            for (int i = 1; i <= 10; i++)
                cmbCount.Items.Add(new ComboBoxItem { Content = i.ToString() });

            // Set values from ViewModel
            chkEnabled.IsChecked = _handlowiec.IsEnabled;
            chkOnlyNew.IsChecked = _handlowiec.ShowOnlyNewContacts;
            chkOnlyAssigned.IsChecked = _handlowiec.ShowOnlyAssigned;
            chkOnlyMyImports.IsChecked = _handlowiec.OnlyMyImports;
            txtDailyTarget.Text = _handlowiec.DailyCallTarget.ToString();
            txtWeeklyTarget.Text = _handlowiec.WeeklyCallTarget.ToString();

            // Set time1 - editable combo, just set the Text
            cmbTime1.Text = _handlowiec.Time1String ?? "10:00";

            // Set time2
            cmbTime2.Text = _handlowiec.Time2String ?? "13:00";

            // Select count
            int countIdx = _handlowiec.ContactsPerReminder - 1;
            if (countIdx >= 0 && countIdx < cmbCount.Items.Count)
                cmbCount.SelectedIndex = countIdx;
            else if (cmbCount.Items.Count > 0)
                cmbCount.SelectedIndex = 4; // default 5
        }

        private TimeSpan? ParseTime(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            // Try HH:mm
            if (TimeSpan.TryParseExact(text, new[] { "h\\:mm", "hh\\:mm" }, null, out var ts))
                return ts;
            // Try H.mm or HH.mm (dot separator)
            text = text.Replace('.', ':');
            if (TimeSpan.TryParseExact(text, new[] { "h\\:mm", "hh\\:mm" }, null, out ts))
                return ts;
            return null;
        }

        private void LoadWojewodztwa()
        {
            var selected = new HashSet<string>(_handlowiec.SelectedWojewodztwa);

            foreach (var woj in ConfigPresets.Wojewodztwa)
            {
                var cb = new CheckBox
                {
                    Content = woj,
                    FontSize = 10,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF)),
                    Margin = new Thickness(0, 2, 10, 2),
                    IsChecked = selected.Contains(woj)
                };
                // Apply GlassCheckBox style
                if (FindResource("GlassCheckBox") is Style glassStyle)
                    cb.Style = glassStyle;
                wojewodztwaPanel.Children.Add(cb);
            }
        }

        private void LoadPKDPriorities()
        {
            _pkdPriorities = new ObservableCollection<PKDPriorityItem>();

            if (_handlowiec.ConfigID > 0)
            {
                try
                {
                    using var conn = new SqlConnection(_connectionString);
                    conn.Open();

                    var cmd = new SqlCommand(
                        @"SELECT PKDCode, PKDName, ISNULL(Priority, 0) as SortOrder
                          FROM CallReminderPKDPriority
                          WHERE ConfigID = @ConfigID
                          ORDER BY Priority", conn);
                    cmd.Parameters.AddWithValue("@ConfigID", _handlowiec.ConfigID);

                    using var reader = cmd.ExecuteReader();
                    int order = 1;
                    while (reader.Read())
                    {
                        _pkdPriorities.Add(new PKDPriorityItem
                        {
                            PKDCode = reader.GetString(0),
                            PKDName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            SortOrder = order++
                        });
                    }
                }
                catch { }
            }

            lstPKDPriorities.ItemsSource = _pkdPriorities;
        }

        private void LoadAvailablePKD()
        {
            _availablePKD = new ObservableCollection<PKDItem>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Try PKD_Slownik table first
                var cmd = new SqlCommand(
                    @"IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('PKD_Slownik') AND type = 'U')
                      SELECT Kod, Nazwa, Kategoria FROM PKD_Slownik ORDER BY Kod
                    ELSE
                      SELECT '---' as Kod, 'Tabela PKD_Slownik nie istnieje' as Nazwa, '' as Kategoria", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var kod = reader.GetString(0);
                    if (kod == "---") break;
                    _availablePKD.Add(new PKDItem
                    {
                        Kod = kod,
                        Nazwa = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Kategoria = reader.IsDBNull(2) ? "" : reader.GetString(2)
                    });
                }
            }
            catch { }

            // If no PKD from DB, add hardcoded defaults
            if (_availablePKD.Count == 0)
            {
                _availablePKD.Add(new PKDItem { Kod = "10.11", Nazwa = "Przetwarzanie miesa z wylaczeniem drobiu", Kategoria = "MIESO" });
                _availablePKD.Add(new PKDItem { Kod = "10.12", Nazwa = "Przetwarzanie miesa z drobiu", Kategoria = "DROB" });
                _availablePKD.Add(new PKDItem { Kod = "10.13", Nazwa = "Produkcja wyrobow z miesa", Kategoria = "MIESO" });
                _availablePKD.Add(new PKDItem { Kod = "46.32", Nazwa = "Sprzedaz hurtowa miesa", Kategoria = "HURT" });
                _availablePKD.Add(new PKDItem { Kod = "47.22", Nazwa = "Sprzedaz detaliczna miesa", Kategoria = "DETAL" });
                _availablePKD.Add(new PKDItem { Kod = "56.10", Nazwa = "Restauracje", Kategoria = "GASTRO" });
                _availablePKD.Add(new PKDItem { Kod = "56.21", Nazwa = "Catering", Kategoria = "GASTRO" });
                _availablePKD.Add(new PKDItem { Kod = "56.29", Nazwa = "Pozostala gastronomia", Kategoria = "GASTRO" });
                _availablePKD.Add(new PKDItem { Kod = "55.10", Nazwa = "Hotele", Kategoria = "HOTEL" });
            }

            cmbAvailablePKD.ItemsSource = _availablePKD;
        }

        private void BtnAddPKD_Click(object sender, RoutedEventArgs e)
        {
            var selected = cmbAvailablePKD.SelectedItem as PKDItem;
            if (selected == null) return;

            if (_pkdPriorities.Any(p => p.PKDCode == selected.Kod))
            {
                MessageBox.Show("To PKD jest juz na liscie priorytetow.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _pkdPriorities.Add(new PKDPriorityItem
            {
                PKDCode = selected.Kod,
                PKDName = selected.Nazwa,
                SortOrder = _pkdPriorities.Count + 1
            });
        }

        private void BtnRemovePKD_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var item = btn?.DataContext as PKDPriorityItem;
            if (item != null)
            {
                _pkdPriorities.Remove(item);
                // Renumber
                int order = 1;
                foreach (var p in _pkdPriorities)
                    p.SortOrder = order++;
            }
        }

        private void BtnPKDPreset_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var tag = btn?.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            if (ConfigPresets.PKDPresets.TryGetValue(tag, out var codes))
            {
                _pkdPriorities.Clear();
                int order = 1;
                foreach (var code in codes)
                {
                    var existing = _availablePKD.FirstOrDefault(p => p.Kod == code);
                    _pkdPriorities.Add(new PKDPriorityItem
                    {
                        PKDCode = code,
                        PKDName = existing?.Nazwa ?? "",
                        SortOrder = order++
                    });
                }
            }
        }

        private void BtnSelectAllWoj_Click(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox cb in wojewodztwaPanel.Children.OfType<CheckBox>())
                cb.IsChecked = true;
        }

        private void BtnDeselectAllWoj_Click(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox cb in wojewodztwaPanel.Children.OfType<CheckBox>())
                cb.IsChecked = false;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 1. Update ViewModel from controls
            _handlowiec.IsEnabled = chkEnabled.IsChecked == true;
            _handlowiec.ShowOnlyNewContacts = chkOnlyNew.IsChecked == true;
            _handlowiec.ShowOnlyAssigned = chkOnlyAssigned.IsChecked == true;
            _handlowiec.OnlyMyImports = chkOnlyMyImports.IsChecked == true;

            if (int.TryParse(txtDailyTarget.Text, out int daily))
                _handlowiec.DailyCallTarget = daily;
            if (int.TryParse(txtWeeklyTarget.Text, out int weekly))
                _handlowiec.WeeklyCallTarget = weekly;

            // Parse times - editable combo, read from Text
            var time1 = ParseTime(cmbTime1.Text);
            var time2 = ParseTime(cmbTime2.Text);
            if (time1 == null || time2 == null)
            {
                MessageBox.Show("Nieprawidlowy format godziny. Uzyj formatu HH:mm (np. 09:30, 14:15).",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _handlowiec.ReminderTime1 = time1.Value;
            _handlowiec.Time1String = cmbTime1.Text.Trim();
            _handlowiec.ReminderTime2 = time2.Value;
            _handlowiec.Time2String = cmbTime2.Text.Trim();

            if (cmbCount.SelectedItem is ComboBoxItem cntItem && int.TryParse(cntItem.Content.ToString(), out int cnt))
                _handlowiec.ContactsPerReminder = cnt;

            // 2. Collect Wojewodztwa
            _handlowiec.SelectedWojewodztwa.Clear();
            foreach (CheckBox cb in wojewodztwaPanel.Children.OfType<CheckBox>())
            {
                if (cb.IsChecked == true)
                    _handlowiec.SelectedWojewodztwa.Add(cb.Content.ToString());
            }

            // 3. Save to database
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                using var tran = conn.BeginTransaction();

                try
                {
                    string territoryJson = _handlowiec.SelectedWojewodztwa.Count > 0
                        ? JsonSerializer.Serialize(_handlowiec.SelectedWojewodztwa.ToList())
                        : null;

                    // Check if config exists
                    var cmdCheck = new SqlCommand("SELECT COUNT(*) FROM CallReminderConfig WHERE UserID = @UserID", conn, tran);
                    cmdCheck.Parameters.AddWithValue("@UserID", _handlowiec.UserID);
                    bool exists = (int)cmdCheck.ExecuteScalar() > 0;

                    if (exists)
                    {
                        var cmdUpdate = new SqlCommand(
                            @"UPDATE CallReminderConfig SET
                                IsEnabled = @Enabled,
                                ReminderTime1 = @Time1,
                                ReminderTime2 = @Time2,
                                ContactsPerReminder = @Count,
                                ShowOnlyNewContacts = @OnlyNew,
                                ShowOnlyAssigned = @OnlyAssigned,
                                OnlyMyImports = @OnlyMyImports,
                                DailyCallTarget = @DailyTarget,
                                WeeklyCallTarget = @WeeklyTarget,
                                TerritoryWojewodztwa = @Territory,
                                ModifiedAt = GETDATE()
                            WHERE UserID = @UserID", conn, tran);

                        cmdUpdate.Parameters.AddWithValue("@UserID", _handlowiec.UserID);
                        cmdUpdate.Parameters.AddWithValue("@Enabled", _handlowiec.IsEnabled);
                        cmdUpdate.Parameters.AddWithValue("@Time1", _handlowiec.ReminderTime1);
                        cmdUpdate.Parameters.AddWithValue("@Time2", _handlowiec.ReminderTime2);
                        cmdUpdate.Parameters.AddWithValue("@Count", _handlowiec.ContactsPerReminder);
                        cmdUpdate.Parameters.AddWithValue("@OnlyNew", _handlowiec.ShowOnlyNewContacts);
                        cmdUpdate.Parameters.AddWithValue("@OnlyAssigned", _handlowiec.ShowOnlyAssigned);
                        cmdUpdate.Parameters.AddWithValue("@OnlyMyImports", _handlowiec.OnlyMyImports);
                        cmdUpdate.Parameters.AddWithValue("@DailyTarget", _handlowiec.DailyCallTarget);
                        cmdUpdate.Parameters.AddWithValue("@WeeklyTarget", _handlowiec.WeeklyCallTarget);
                        cmdUpdate.Parameters.AddWithValue("@Territory", (object)territoryJson ?? DBNull.Value);
                        cmdUpdate.ExecuteNonQuery();
                    }
                    else
                    {
                        var cmdInsert = new SqlCommand(
                            @"INSERT INTO CallReminderConfig (UserID, IsEnabled, ReminderTime1, ReminderTime2,
                                ContactsPerReminder, ShowOnlyNewContacts, ShowOnlyAssigned, OnlyMyImports,
                                DailyCallTarget, WeeklyCallTarget, TerritoryWojewodztwa)
                            VALUES (@UserID, @Enabled, @Time1, @Time2, @Count, @OnlyNew, @OnlyAssigned,
                                @OnlyMyImports, @DailyTarget, @WeeklyTarget, @Territory)", conn, tran);

                        cmdInsert.Parameters.AddWithValue("@UserID", _handlowiec.UserID);
                        cmdInsert.Parameters.AddWithValue("@Enabled", _handlowiec.IsEnabled);
                        cmdInsert.Parameters.AddWithValue("@Time1", _handlowiec.ReminderTime1);
                        cmdInsert.Parameters.AddWithValue("@Time2", _handlowiec.ReminderTime2);
                        cmdInsert.Parameters.AddWithValue("@Count", _handlowiec.ContactsPerReminder);
                        cmdInsert.Parameters.AddWithValue("@OnlyNew", _handlowiec.ShowOnlyNewContacts);
                        cmdInsert.Parameters.AddWithValue("@OnlyAssigned", _handlowiec.ShowOnlyAssigned);
                        cmdInsert.Parameters.AddWithValue("@OnlyMyImports", _handlowiec.OnlyMyImports);
                        cmdInsert.Parameters.AddWithValue("@DailyTarget", _handlowiec.DailyCallTarget);
                        cmdInsert.Parameters.AddWithValue("@WeeklyTarget", _handlowiec.WeeklyCallTarget);
                        cmdInsert.Parameters.AddWithValue("@Territory", (object)territoryJson ?? DBNull.Value);
                        cmdInsert.ExecuteNonQuery();
                    }

                    // Get ConfigID for PKD operations
                    int configID = _handlowiec.ConfigID;
                    if (configID == 0)
                    {
                        var cmdGetId = new SqlCommand("SELECT ID FROM CallReminderConfig WHERE UserID = @UserID", conn, tran);
                        cmdGetId.Parameters.AddWithValue("@UserID", _handlowiec.UserID);
                        var result = cmdGetId.ExecuteScalar();
                        if (result != null) configID = (int)result;
                        _handlowiec.ConfigID = configID;
                    }

                    // Delete old PKD priorities
                    if (configID > 0)
                    {
                        var cmdDeletePKD = new SqlCommand(
                            "DELETE FROM CallReminderPKDPriority WHERE ConfigID = @ConfigID", conn, tran);
                        cmdDeletePKD.Parameters.AddWithValue("@ConfigID", configID);
                        cmdDeletePKD.ExecuteNonQuery();

                        // Insert new PKD priorities
                        for (int i = 0; i < _pkdPriorities.Count; i++)
                        {
                            var cmdInsertPKD = new SqlCommand(
                                @"INSERT INTO CallReminderPKDPriority (ConfigID, PKDCode, PKDName, Priority)
                                  VALUES (@ConfigID, @PKDCode, @PKDName, @SortOrder)", conn, tran);
                            cmdInsertPKD.Parameters.AddWithValue("@ConfigID", configID);
                            cmdInsertPKD.Parameters.AddWithValue("@PKDCode", _pkdPriorities[i].PKDCode);
                            cmdInsertPKD.Parameters.AddWithValue("@PKDName", _pkdPriorities[i].PKDName ?? "");
                            cmdInsertPKD.Parameters.AddWithValue("@SortOrder", i + 1);
                            cmdInsertPKD.ExecuteNonQuery();
                        }
                    }

                    tran.Commit();

                    // Reload service config
                    try { CallReminderService.Instance.ReloadConfig(); } catch { }

                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    MessageBox.Show($"Blad zapisu: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad polaczenia: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
