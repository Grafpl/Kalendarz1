using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1
{
    public class PracownikStat : INotifyPropertyChanged
    {
        public string NazwaPracownika { get; set; }
        public int LiczbaAkcji { get; set; }

        private double _procent;
        public double Procent
        {
            get => _procent;
            set
            {
                _procent = value;
                OnPropertyChanged(nameof(Procent));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class StatystykiPracownikow : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public StatystykiPracownikow()
        {
            InitializeComponent();
            FilterButton_Click(this.FindName("btnMiesiac"), null); // Domyślnie ładuj dane miesięczne
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            // Resetuj wygląd wszystkich przycisków
            foreach (var child in (button.Parent as Panel).Children)
            {
                if (child is Button b)
                {
                    b.Tag = "";
                    b.Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80));
                }
            }

            // Ustaw wygląd aktywnego przycisku
            button.Tag = "Selected";
            button.Foreground = Brushes.White;

            string period = button.Tag.ToString();
            LoadAllStatistics(period);
        }

        private void LoadAllStatistics(string period)
        {
            (DateTime startDate, DateTime endDate) = GetDateRange(period);

            itemsWstawienia.ItemsSource = LoadStatistics("Wstawienia", startDate, endDate);
            itemsPotwierdzenia.ItemsSource = LoadStatistics("Potwierdzenia", startDate, endDate);
            itemsKontakty.ItemsSource = LoadStatistics("Kontakty", startDate, endDate);
        }

        private (DateTime, DateTime) GetDateRange(string period)
        {
            DateTime today = DateTime.Today;
            switch (period)
            {
                case "Today":
                    return (today, today.AddDays(1));
                case "Week":
                    DateTime startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
                    return (startOfWeek, startOfWeek.AddDays(7));
                case "Month":
                    DateTime startOfMonth = new DateTime(today.Year, today.Month, 1);
                    return (startOfMonth, startOfMonth.AddMonths(1));
                case "Year":
                    DateTime startOfYear = new DateTime(today.Year, 1, 1);
                    return (startOfYear, startOfYear.AddYears(1));
                default:
                    return (today, today.AddDays(1));
            }
        }

        private ObservableCollection<PracownikStat> LoadStatistics(string type, DateTime startDate, DateTime endDate)
        {
            var stats = new ObservableCollection<PracownikStat>();
            string query = "";
            switch (type)
            {
                case "Wstawienia":
                    query = @"SELECT ISNULL(o.Name, 'Nieznany') AS UserName, COUNT(*) AS TotalCount
                              FROM dbo.WstawieniaKurczakow w
                              LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                              WHERE w.DataUtw >= @StartDate AND w.DataUtw < @EndDate AND w.KtoStwo IS NOT NULL
                              GROUP BY o.Name
                              ORDER BY TotalCount DESC";
                    break;
                case "Potwierdzenia":
                    query = @"SELECT ISNULL(o.Name, 'Nieznany') AS UserName, COUNT(*) AS TotalCount
                              FROM dbo.WstawieniaKurczakow w
                              LEFT JOIN dbo.operators o ON w.KtoConf = o.ID
                              WHERE w.isConf = 1 AND w.DataConf >= @StartDate AND w.DataConf < @EndDate AND w.KtoConf IS NOT NULL
                              GROUP BY o.Name
                              ORDER BY TotalCount DESC";
                    break;
                case "Kontakty":
                    query = @"SELECT ISNULL(o.Name, ch.UserID) AS UserName, COUNT(*) AS TotalCount
                              FROM dbo.ContactHistory ch
                              LEFT JOIN dbo.operators o ON ch.UserID = o.ID
                              WHERE ch.CreatedAt >= @StartDate AND ch.CreatedAt < @EndDate AND ch.UserID IS NOT NULL
                              GROUP BY o.Name, ch.UserID
                              ORDER BY TotalCount DESC";
                    break;
            }

            using (var connection = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@StartDate", startDate);
                cmd.Parameters.AddWithValue("@EndDate", endDate);

                connection.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        stats.Add(new PracownikStat
                        {
                            NazwaPracownika = reader["UserName"].ToString(),
                            LiczbaAkcji = Convert.ToInt32(reader["TotalCount"])
                        });
                    }
                }
            }

            int totalActions = 0;
            foreach (var stat in stats) totalActions += stat.LiczbaAkcji;

            if (totalActions > 0)
            {
                double maxWidth = 300; // Maksymalna szerokość paska w pikselach
                foreach (var stat in stats)
                {
                    stat.Procent = (double)stat.LiczbaAkcji / totalActions * maxWidth;
                }
            }
            return stats;
        }
    }
}