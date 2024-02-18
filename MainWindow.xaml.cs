using System;
using System.Collections.Generic;
using Microsoft.Data;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace Kalendarz1
{

    public partial class MainWindow : Window
    {
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }


        public class Event
        {
            private DateTime _dataOdbioru;
            public DateTime DataOdbioru
            {
                get { return _dataOdbioru; }
                set { _dataOdbioru = value; }
            }

            public string FormattedDate
            {
                get { return _dataOdbioru.ToString("yyyy-MM-dd dddd", CultureInfo.InvariantCulture); }
            }

            public string Dostawca { get; set; }
            public string Auta { get; set; }
        }



        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Tutaj możesz wykonać inne operacje inicjalizacyjne
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            // Znajdź wybraną datę lub ustaw dzisiejszą datę, jeśli żadna nie jest wybrana
            DateTime selectedDate = MyCalendar.SelectedDate ?? DateTime.Today;

            // Znajdź początek tygodnia (zakładamy, że tydzień zaczyna się od poniedziałku)
            DateTime startOfWeek = selectedDate.AddDays(-(int)selectedDate.DayOfWeek + (int)DayOfWeek.Monday);
            DateTime endOfWeek = startOfWeek.AddDays(7); // Koniec tygodnia jest dniem po ostatnim dniu tygodnia

            // Pobierz dane z bazy danych dla zakresu dat
            List<Event> events = GetEventsFromDatabase(startOfWeek, endOfWeek);

            // Grupuj wydarzenia według dnia tygodnia
            var eventsByDay = events.GroupBy(ev => ev.DataOdbioru.DayOfWeek)
                                    .ToDictionary(g => g.Key, g => g.ToList());

            // Ustaw źródło danych dla każdego ListView
            // Zakładamy, że masz ListView dla każdego dnia tygodnia o nazwach ListViewMonday, ListViewTuesday, itd.
            ListViewPon.ItemsSource = eventsByDay.ContainsKey(DayOfWeek.Monday) ? eventsByDay[DayOfWeek.Monday] : new List<Event>();
            ListViewWT.ItemsSource = eventsByDay.ContainsKey(DayOfWeek.Tuesday) ? eventsByDay[DayOfWeek.Tuesday] : new List<Event>();
            ListViewSr.ItemsSource = eventsByDay.ContainsKey(DayOfWeek.Tuesday) ? eventsByDay[DayOfWeek.Wednesday] : new List<Event>();
            ListViewCzw.ItemsSource = eventsByDay.ContainsKey(DayOfWeek.Tuesday) ? eventsByDay[DayOfWeek.Thursday] : new List<Event>();
            ListViewPt.ItemsSource = eventsByDay.ContainsKey(DayOfWeek.Tuesday) ? eventsByDay[DayOfWeek.Friday] : new List<Event>();
            // ... i tak dalej dla każdego dnia tygodnia
        }

        private void MyCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButton_Click(sender, e);
        }


        private List<Event> GetEventsFromDatabase(DateTime startDate, DateTime endDate)
        {
            List<Event> events = new List<Event>();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionPermission))
                {
                    connection.Open();
                    string query = @"
                SELECT [DataOdbioru], [Dostawca], [Auta] 
                FROM [LibraNet].[dbo].[HarmonogramDostaw] 
                WHERE [DataOdbioru] >= @StartDate AND [DataOdbioru] < @EndDate order by DataOdbioru ASC";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        events.Add(new Event
                        {
                            DataOdbioru = reader["DataOdbioru"] != DBNull.Value ? Convert.ToDateTime(reader["DataOdbioru"]) : DateTime.MinValue,
                            Dostawca = reader["Dostawca"].ToString(),
                            Auta = reader["Auta"].ToString()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd pobierania danych: " + ex.Message);
            }

            return events;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Tworzenie nowej instancji Form1
            Dostawa form1 = new Dostawa();

            // Wyświetlanie Form1
            form1.Show();


            // Opcjonalnie, jeśli chcesz, aby użytkownik mógł interaktywnie korzystać z Form1 i wrócić do Form2, użyj form1.ShowDialog() zamiast form1.Show().
            // form1.ShowDialog();
        }

        private void Pokaz_Click(object sender, RoutedEventArgs e)
        {

            // Tworzenie nowej instancji Form1
            WidokKalendarza widokKalendarza = new WidokKalendarza();

            // Wyświetlanie Form1
            widokKalendarza.Show();


            // Opcjonalnie, jeśli chcesz, aby użytkownik mógł interaktywnie korzystać z Form1 i wrócić do Form2, użyj form1.ShowDialog() zamiast form1.Show().
            // form1.ShowDialog();
        }
    }
}