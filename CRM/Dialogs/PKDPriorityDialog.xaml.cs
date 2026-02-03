using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.CRM.Dialogs
{
    public partial class PKDPriorityDialog : Window
    {
        private readonly string _connectionString;
        private readonly List<PKDItem> _allPKDItems = new();
        private readonly HashSet<string> _priorityPKDs = new();

        public PKDPriorityDialog(string connectionString)
        {
            _connectionString = connectionString;
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Load existing priority PKDs
                try
                {
                    var cmdPriority = new SqlCommand("SELECT PKDCode FROM CRM_PKDPriority ORDER BY SortOrder", conn);
                    using var reader = cmdPriority.ExecuteReader();
                    while (reader.Read())
                    {
                        _priorityPKDs.Add(reader.GetString(0));
                    }
                }
                catch { }

                // Load all unique PKD codes from OdbiorcyCRM
                var cmdPKD = new SqlCommand(
                    @"SELECT DISTINCT PKD_Opis
                      FROM OdbiorcyCRM
                      WHERE PKD_Opis IS NOT NULL AND PKD_Opis <> ''
                      ORDER BY PKD_Opis", conn);

                using var readerPKD = cmdPKD.ExecuteReader();
                while (readerPKD.Read())
                {
                    string pkd = readerPKD.GetString(0).Trim();
                    if (!string.IsNullOrEmpty(pkd))
                    {
                        _allPKDItems.Add(new PKDItem
                        {
                            Code = pkd,
                            IsPriority = _priorityPKDs.Contains(pkd)
                        });
                    }
                }

                RefreshList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshList(string filter = null)
        {
            pnlPKDList.Children.Clear();

            // Sort: priority first, then alphabetically
            var items = _allPKDItems
                .Where(p => string.IsNullOrEmpty(filter) || p.Code.ToLower().Contains(filter.ToLower()))
                .OrderByDescending(p => p.IsPriority)
                .ThenBy(p => p.Code)
                .ToList();

            foreach (var item in items)
            {
                var cb = new CheckBox
                {
                    Content = item.Code,
                    IsChecked = item.IsPriority,
                    Tag = item,
                    Style = (Style)FindResource("PKDCheckBox")
                };
                cb.Checked += Cb_CheckChanged;
                cb.Unchecked += Cb_CheckChanged;
                pnlPKDList.Children.Add(cb);
            }

            UpdateCount();
        }

        private void Cb_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is PKDItem item)
            {
                item.IsPriority = cb.IsChecked == true;
                UpdateCount();
            }
        }

        private void UpdateCount()
        {
            int count = _allPKDItems.Count(p => p.IsPriority);
            txtCount.Text = $"{count} zaznaczonych";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshList(txtSearch.Text);
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _allPKDItems)
            {
                item.IsPriority = false;
            }
            RefreshList(txtSearch.Text);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Clear existing priorities
                new SqlCommand("DELETE FROM CRM_PKDPriority", conn).ExecuteNonQuery();

                // Insert new priorities
                int sortOrder = 0;
                foreach (var item in _allPKDItems.Where(p => p.IsPriority).OrderBy(p => p.Code))
                {
                    var cmdInsert = new SqlCommand(
                        "INSERT INTO CRM_PKDPriority (PKDCode, SortOrder) VALUES (@Code, @Order)", conn);
                    cmdInsert.Parameters.AddWithValue("@Code", item.Code);
                    cmdInsert.Parameters.AddWithValue("@Order", sortOrder++);
                    cmdInsert.ExecuteNonQuery();
                }

                MessageBox.Show($"Zapisano {sortOrder} priorytetowych branż.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private class PKDItem
        {
            public string Code { get; set; }
            public bool IsPriority { get; set; }
        }
    }

    public class InverseLengthToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int length)
                return length == 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotImplementedException();
    }
}
