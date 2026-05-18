using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace Kalendarz1.Admin
{
    /// <summary>
    /// Okno historii logowań (audit log z tabeli LoginAttempts).
    /// Filtry: login, daty, sukces/błąd. Wymóg BRC sekcja 3.3 + IFS 2.2.
    /// </summary>
    public partial class LoginAuditWindow : Window
    {
        private const string ConnectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private const int MaxRows = 1000;

        public LoginAuditWindow()
        {
            InitializeComponent();
            DateFromPicker.SelectedDate = DateTime.Today.AddDays(-7);
            DateToPicker.SelectedDate = DateTime.Today;
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var sql = new StringBuilder(@"
                    SELECT TOP (@max)
                        UserId, Success, AttemptedAt, MachineName, FailureReason
                    FROM dbo.LoginAttempts
                    WHERE 1=1");

                var userFilter = UserFilterBox.Text?.Trim();
                if (!string.IsNullOrEmpty(userFilter))
                    sql.Append(" AND UserId LIKE @user");

                if (DateFromPicker.SelectedDate.HasValue)
                    sql.Append(" AND AttemptedAt >= @from");
                if (DateToPicker.SelectedDate.HasValue)
                    sql.Append(" AND AttemptedAt < @to");

                var statusIdx = StatusFilterBox.SelectedIndex;
                if (statusIdx == 1) sql.Append(" AND Success = 1");
                else if (statusIdx == 2) sql.Append(" AND Success = 0");

                sql.Append(" ORDER BY AttemptedAt DESC");

                using var conn = new SqlConnection(ConnectionString);
                conn.Open();
                using var cmd = new SqlCommand(sql.ToString(), conn);
                cmd.Parameters.AddWithValue("@max", MaxRows);
                if (!string.IsNullOrEmpty(userFilter))
                    cmd.Parameters.AddWithValue("@user", "%" + userFilter + "%");
                if (DateFromPicker.SelectedDate.HasValue)
                    cmd.Parameters.AddWithValue("@from", DateFromPicker.SelectedDate.Value);
                if (DateToPicker.SelectedDate.HasValue)
                    cmd.Parameters.AddWithValue("@to", DateToPicker.SelectedDate.Value.AddDays(1));

                var list = new List<AttemptRow>();
                int success = 0, fail = 0;
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var ok = !r.IsDBNull(1) && r.GetBoolean(1);
                    if (ok) success++; else fail++;
                    list.Add(new AttemptRow
                    {
                        UserId = r.IsDBNull(0) ? "" : r.GetString(0),
                        AttemptedAt = r.IsDBNull(2) ? DateTime.MinValue : r.GetDateTime(2),
                        MachineName = r.IsDBNull(3) ? "" : r.GetString(3),
                        FailureReason = r.IsDBNull(4) ? "" : r.GetString(4),
                        SuccessText = ok ? "✅ Sukces" : "❌ Błąd",
                        SuccessColor = ok
                            ? new SolidColorBrush(Color.FromRgb(22, 163, 74))
                            : new SolidColorBrush(Color.FromRgb(220, 38, 38))
                    });
                }

                AttemptsGrid.ItemsSource = list;
                var total = success + fail;
                StatsText.Text = total == 0
                    ? "Brak wyników dla zadanych filtrów."
                    : $"Wyświetlone: {total}  •  ✅ Sukces: {success}  •  ❌ Błąd: {fail}" +
                      (total >= MaxRows ? $"  (limit {MaxRows} — zawęź filtry)" : "");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania danych:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => LoadData();
        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadData();

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            UserFilterBox.Text = "";
            DateFromPicker.SelectedDate = DateTime.Today.AddDays(-7);
            DateToPicker.SelectedDate = DateTime.Today;
            StatusFilterBox.SelectedIndex = 0;
            LoadData();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        public class AttemptRow
        {
            public string UserId { get; set; } = "";
            public DateTime AttemptedAt { get; set; }
            public string MachineName { get; set; } = "";
            public string FailureReason { get; set; } = "";
            public string SuccessText { get; set; } = "";
            public Brush SuccessColor { get; set; } = Brushes.Black;
        }
    }
}
