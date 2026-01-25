using Kalendarz1.OfertaCenowa;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Kalendarz1.CRM
{
    public partial class KalendarzCRMWindow : Window
    {
        private readonly string connectionString;
        private readonly string operatorID;
        private DateTime currentMonth;
        private DateTime selectedDate;
        private Dictionary<DateTime, List<KontaktKalendarz>> kontaktyWMiesiacu;
        private DispatcherTimer reminderTimer;
        private HashSet<int> pokazanePrzypomnienia;

        public KalendarzCRMWindow(string connString, string opId)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            connectionString = connString;
            operatorID = opId;
            currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            selectedDate = DateTime.Today;
            kontaktyWMiesiacu = new Dictionary<DateTime, List<KontaktKalendarz>>();
            pokazanePrzypomnienia = new HashSet<int>();

            Loaded += KalendarzCRMWindow_Loaded;
            Closed += KalendarzCRMWindow_Closed;
        }

        private void KalendarzCRMWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WczytajKontaktyMiesiaca();
            RenderCalendar();
            WczytajKontaktyDnia(selectedDate);
            StartReminderTimer();
        }

        private void KalendarzCRMWindow_Closed(object sender, EventArgs e)
        {
            reminderTimer?.Stop();
        }

        #region Kalendarz
        private void WczytajKontaktyMiesiaca()
        {
            kontaktyWMiesiacu.Clear();

            DateTime startDate = currentMonth.AddDays(-7); // Tydzień przed
            DateTime endDate = currentMonth.AddMonths(1).AddDays(7); // Tydzień po

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                        SELECT o.ID, o.Nazwa, o.MIASTO, o.Telefon_K, o.Status, o.DataNastepnegoKontaktu
                        FROM OdbiorcyCRM o
                        LEFT JOIN WlascicieleOdbiorcow w ON o.ID = w.IDOdbiorcy
                        WHERE o.DataNastepnegoKontaktu BETWEEN @start AND @end
                        AND (w.OperatorID = @op OR w.OperatorID IS NULL)
                        ORDER BY o.DataNastepnegoKontaktu", conn);

                    cmd.Parameters.AddWithValue("@start", startDate);
                    cmd.Parameters.AddWithValue("@end", endDate);
                    cmd.Parameters.AddWithValue("@op", operatorID);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var kontakt = new KontaktKalendarz
                            {
                                ID = reader.GetInt32(0),
                                Nazwa = reader["Nazwa"]?.ToString() ?? "",
                                Miasto = reader["MIASTO"]?.ToString() ?? "",
                                Telefon = reader["Telefon_K"]?.ToString() ?? "",
                                Status = reader["Status"]?.ToString() ?? "Do zadzwonienia",
                                DataKontaktu = reader.GetDateTime(5)
                            };

                            // Ustaw kolory statusu
                            SetStatusColors(kontakt);

                            DateTime dataKey = kontakt.DataKontaktu.Date;
                            if (!kontaktyWMiesiacu.ContainsKey(dataKey))
                                kontaktyWMiesiacu[dataKey] = new List<KontaktKalendarz>();

                            kontaktyWMiesiacu[dataKey].Add(kontakt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania kontaktów: {ex.Message}");
            }
        }

        private void SetStatusColors(KontaktKalendarz k)
        {
            switch (k.Status)
            {
                case "Próba kontaktu":
                    k.StatusBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEDD5"));
                    k.StatusFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9A3412"));
                    break;
                case "Nawiązano kontakt":
                    k.StatusBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
                    k.StatusFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534"));
                    break;
                case "Zgoda na dalszy kontakt":
                    k.StatusBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCFBF1"));
                    k.StatusFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D9488"));
                    break;
                case "Do wysłania oferta":
                    k.StatusBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE"));
                    k.StatusFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E40AF"));
                    break;
                case "Nie zainteresowany":
                    k.StatusBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                    k.StatusFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
                    break;
                default: // Do zadzwonienia
                    k.StatusBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
                    k.StatusFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                    break;
            }
        }

        private void RenderCalendar()
        {
            calendarGrid.Children.Clear();

            // Ustaw nagłówek
            txtMiesiac.Text = currentMonth.ToString("MMMM yyyy", new CultureInfo("pl-PL"));
            txtMiesiac.Text = char.ToUpper(txtMiesiac.Text[0]) + txtMiesiac.Text.Substring(1);

            // Znajdź pierwszy dzień miesiąca
            DateTime firstDay = new DateTime(currentMonth.Year, currentMonth.Month, 1);
            int startDayOfWeek = ((int)firstDay.DayOfWeek + 6) % 7; // Poniedziałek = 0

            // Dni poprzedniego miesiąca
            DateTime prevMonth = firstDay.AddDays(-startDayOfWeek);

            int daysInMonth = DateTime.DaysInMonth(currentMonth.Year, currentMonth.Month);

            for (int i = 0; i < 42; i++) // 6 tygodni x 7 dni
            {
                DateTime cellDate = prevMonth.AddDays(i);
                bool isCurrentMonth = cellDate.Month == currentMonth.Month;
                bool isToday = cellDate.Date == DateTime.Today;
                bool isSelected = cellDate.Date == selectedDate.Date;
                bool hasContacts = kontaktyWMiesiacu.ContainsKey(cellDate.Date);
                int contactCount = hasContacts ? kontaktyWMiesiacu[cellDate.Date].Count : 0;
                bool hasOverdue = hasContacts && cellDate.Date < DateTime.Today;

                var dayButton = new Button
                {
                    Style = (Style)FindResource("DayButton"),
                    Tag = cellDate,
                    Margin = new Thickness(2)
                };

                // Tło
                if (isToday)
                    dayButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
                else if (isSelected)
                    dayButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE"));
                else if (hasOverdue)
                    dayButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                else if (!isCurrentMonth)
                    dayButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB"));

                // Zawartość
                var content = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(4) };

                // Numer dnia
                var dayNumber = new TextBlock
                {
                    Text = cellDate.Day.ToString(),
                    FontSize = 14,
                    FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = isCurrentMonth
                        ? (isToday ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A")) : Brushes.Black)
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                content.Children.Add(dayNumber);

                // Wskaźnik kontaktów
                if (hasContacts && isCurrentMonth)
                {
                    var indicator = new Border
                    {
                        Background = hasOverdue
                            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"))
                            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(0, 4, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Left
                    };

                    var indicatorText = new TextBlock
                    {
                        Text = $"{contactCount} {(contactCount == 1 ? "kontakt" : (contactCount < 5 ? "kontakty" : "kontaktów"))}",
                        FontSize = 9,
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.SemiBold
                    };
                    indicator.Child = indicatorText;
                    content.Children.Add(indicator);
                }

                dayButton.Content = content;
                dayButton.Click += DayButton_Click;

                calendarGrid.Children.Add(dayButton);
            }
        }

        private void DayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DateTime date)
            {
                selectedDate = date;
                RenderCalendar();
                WczytajKontaktyDnia(date);
            }
        }

        private void WczytajKontaktyDnia(DateTime date)
        {
            // Aktualizuj nagłówek
            if (date.Date == DateTime.Today)
                txtWybranyDzien.Text = "Dzisiaj";
            else if (date.Date == DateTime.Today.AddDays(1))
                txtWybranyDzien.Text = "Jutro";
            else if (date.Date == DateTime.Today.AddDays(-1))
                txtWybranyDzien.Text = "Wczoraj";
            else
                txtWybranyDzien.Text = date.ToString("dddd", new CultureInfo("pl-PL"));

            txtWybranyDzien.Text = char.ToUpper(txtWybranyDzien.Text[0]) + txtWybranyDzien.Text.Substring(1);
            txtDataWybrana.Text = date.ToString("d MMMM yyyy", new CultureInfo("pl-PL"));

            // Pobierz kontakty
            var kontakty = kontaktyWMiesiacu.ContainsKey(date.Date)
                ? kontaktyWMiesiacu[date.Date]
                : new List<KontaktKalendarz>();

            listaKontaktow.ItemsSource = kontakty;

            // Statystyki
            txtStatZaplanowane.Text = kontakty.Count.ToString();

            // Zaległe - kontakty z przeszłości które nie są wykonane
            int zalegle = 0;
            foreach (var k in kontaktyWMiesiacu.Values.SelectMany(x => x))
            {
                if (k.DataKontaktu.Date < DateTime.Today && k.Status != "Nawiązano kontakt" && k.Status != "Nie zainteresowany")
                    zalegle++;
            }
            txtStatZalegle.Text = zalegle.ToString();

            // Wykonane dzisiaj
            int wykonane = 0;
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM HistoriaZmianCRM
                        WHERE KtoWykonal = @op AND CAST(DataZmiany AS DATE) = @date
                        AND TypZmiany = 'Zmiana statusu'", conn);
                    cmd.Parameters.AddWithValue("@op", operatorID);
                    cmd.Parameters.AddWithValue("@date", date.Date);
                    wykonane = (int)cmd.ExecuteScalar();
                }
            }
            catch { }
            txtStatWykonane.Text = wykonane.ToString();
        }

        private void BtnPoprzedni_Click(object sender, RoutedEventArgs e)
        {
            currentMonth = currentMonth.AddMonths(-1);
            WczytajKontaktyMiesiaca();
            RenderCalendar();
        }

        private void BtnNastepny_Click(object sender, RoutedEventArgs e)
        {
            currentMonth = currentMonth.AddMonths(1);
            WczytajKontaktyMiesiaca();
            RenderCalendar();
        }

        private void BtnDzisiaj_Click(object sender, RoutedEventArgs e)
        {
            currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            selectedDate = DateTime.Today;
            WczytajKontaktyMiesiaca();
            RenderCalendar();
            WczytajKontaktyDnia(DateTime.Today);
        }
        #endregion

        #region Przypomnienia
        private void StartReminderTimer()
        {
            reminderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1) // Sprawdzaj co minutę
            };
            reminderTimer.Tick += ReminderTimer_Tick;
            reminderTimer.Start();

            // Sprawdź od razu przy starcie
            CheckReminders();
        }

        private void ReminderTimer_Tick(object sender, EventArgs e)
        {
            if (chkPrzypomnienia.IsChecked == true)
            {
                CheckReminders();
            }
        }

        private void CheckReminders()
        {
            DateTime now = DateTime.Now;
            DateTime today = DateTime.Today;

            // Sprawdź kontakty na dzisiaj
            if (kontaktyWMiesiacu.ContainsKey(today))
            {
                var dzisiejsze = kontaktyWMiesiacu[today];
                foreach (var kontakt in dzisiejsze)
                {
                    // Pokaż przypomnienie raz dla każdego kontaktu
                    if (!pokazanePrzypomnienia.Contains(kontakt.ID))
                    {
                        // Przypomnienie o 9:00 dla kontaktów na dzisiaj
                        if (now.Hour >= 9 && now.Hour < 10)
                        {
                            ShowReminder(kontakt);
                            pokazanePrzypomnienia.Add(kontakt.ID);
                        }
                    }
                }
            }

            // Sprawdź zaległe kontakty (raz dziennie o 9:00)
            if (now.Hour == 9 && now.Minute < 2)
            {
                int zalegle = 0;
                foreach (var data in kontaktyWMiesiacu.Keys)
                {
                    if (data < today)
                    {
                        zalegle += kontaktyWMiesiacu[data].Count;
                    }
                }

                if (zalegle > 0 && !pokazanePrzypomnienia.Contains(-1))
                {
                    ShowReminderGeneric($"Masz {zalegle} zaległych kontaktów do wykonania!");
                    pokazanePrzypomnienia.Add(-1);
                }
            }
        }

        private async void ShowReminder(KontaktKalendarz kontakt)
        {
            txtReminderContent.Text = $"{kontakt.Nazwa}\n{kontakt.Miasto} • {kontakt.Telefon}";

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
            reminderToast.BeginAnimation(OpacityProperty, fadeIn);
            reminderToast.IsHitTestVisible = true;

            await Task.Delay(10000); // 10 sekund

            if (reminderToast.Opacity > 0)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
                reminderToast.BeginAnimation(OpacityProperty, fadeOut);
                reminderToast.IsHitTestVisible = false;
            }
        }

        private async void ShowReminderGeneric(string message)
        {
            txtReminderContent.Text = message;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
            reminderToast.BeginAnimation(OpacityProperty, fadeIn);
            reminderToast.IsHitTestVisible = true;

            await Task.Delay(8000);

            if (reminderToast.Opacity > 0)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
                reminderToast.BeginAnimation(OpacityProperty, fadeOut);
                reminderToast.IsHitTestVisible = false;
            }
        }

        private void BtnCloseReminder_Click(object sender, RoutedEventArgs e)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.2));
            reminderToast.BeginAnimation(OpacityProperty, fadeOut);
            reminderToast.IsHitTestVisible = false;
        }

        private void ChkPrzypomnienia_Changed(object sender, RoutedEventArgs e)
        {
            // Reset pokazanych przypomnień gdy włączamy ponownie
            if (chkPrzypomnienia.IsChecked == true)
            {
                pokazanePrzypomnienia.Clear();
            }
        }
        #endregion

        #region Akcje na kontaktach
        private void KontaktItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is KontaktKalendarz kontakt)
            {
                // Otwórz okno edycji
                var okno = new EdycjaKontaktuWindow { KlientID = kontakt.ID, OperatorID = operatorID };
                if (okno.ShowDialog() == true)
                {
                    WczytajKontaktyMiesiaca();
                    RenderCalendar();
                    WczytajKontaktyDnia(selectedDate);
                }
            }
        }

        private void BtnZadzwon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is KontaktKalendarz kontakt)
            {
                string tel = kontakt.Telefon.Replace(" ", "").Replace("-", "");
                if (!string.IsNullOrEmpty(tel))
                {
                    Process.Start(new ProcessStartInfo($"tel:{tel}") { UseShellExecute = true });
                }
            }
        }

        private void BtnEdytuj_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is KontaktKalendarz kontakt)
            {
                var okno = new EdycjaKontaktuWindow { KlientID = kontakt.ID, OperatorID = operatorID };
                if (okno.ShowDialog() == true)
                {
                    WczytajKontaktyMiesiaca();
                    RenderCalendar();
                    WczytajKontaktyDnia(selectedDate);
                }
            }
        }
        #endregion
    }

    public class KontaktKalendarz
    {
        public int ID { get; set; }
        public string Nazwa { get; set; }
        public string Miasto { get; set; }
        public string Telefon { get; set; }
        public string Status { get; set; }
        public DateTime DataKontaktu { get; set; }
        public Brush StatusBg { get; set; }
        public Brush StatusFg { get; set; }
    }
}
