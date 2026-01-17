using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Kalendarz1.Zywiec.Kalendarz
{
    /// <summary>
    /// Widok Kalendarza WPF - Etap 1: Kalendarz + Tabela dostaw z kolorowaniem
    /// </summary>
    public partial class WidokKalendarzaWPF : Window
    {
        #region Pola prywatne

        private static readonly string ConnectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private ObservableCollection<DostawaModel> _dostawy = new ObservableCollection<DostawaModel>();
        private DateTime _selectedDate = DateTime.Today;
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _priceTimer;

        #endregion

        #region Właściwości publiczne

        public string UserID { get; set; }
        public string UserName { get; set; }

        #endregion

        #region Konstruktor

        public WidokKalendarzaWPF()
        {
            InitializeComponent();
            dgDostawy.ItemsSource = _dostawy;
            SetupTimers();
        }

        #endregion

        #region Inicjalizacja

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Ustaw użytkownika
            if (!string.IsNullOrEmpty(UserName))
                txtUserName.Text = UserName;
            else if (!string.IsNullOrEmpty(UserID))
                txtUserName.Text = GetUserNameById(UserID);

            // Ustaw kalendarz na dziś
            calendarMain.SelectedDate = DateTime.Today;
            _selectedDate = DateTime.Today;
            UpdateWeekNumber();

            // Załaduj dane
            LoadDostawy();
        }

        private void SetupTimers()
        {
            // Timer odświeżania danych co 10 minut
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(10)
            };
            _refreshTimer.Tick += (s, e) => LoadDostawy();
            _refreshTimer.Start();

            // Timer odświeżania cen co 30 minut
            _priceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(30)
            };
            _priceTimer.Tick += (s, e) => { /* TODO: Etap 4 - odświeżanie cen */ };
            _priceTimer.Start();
        }

        #endregion

        #region Ładowanie danych

        private void LoadDostawy()
        {
            try
            {
                _dostawy.Clear();

                DateTime startOfWeek = _selectedDate.AddDays(-(int)_selectedDate.DayOfWeek);
                DateTime endOfWeek = startOfWeek.AddDays(7);

                string sql = BuildDostawyQuery();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@startDate", startOfWeek);
                        cmd.Parameters.AddWithValue("@endDate", endOfWeek);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            DateTime? currentDate = null;

                            while (reader.Read())
                            {
                                DateTime dataOdbioru = reader.GetDateTime(reader.GetOrdinal("DataOdbioru"));

                                // Dodaj wiersz nagłówka dnia jeśli zmienia się data
                                if (currentDate == null || currentDate.Value.Date != dataOdbioru.Date)
                                {
                                    if (currentDate != null)
                                    {
                                        // Dodaj pusty wiersz separatora
                                        _dostawy.Add(new DostawaModel { IsHeaderRow = true, IsSeparator = true });
                                    }

                                    // Wiersz nagłówka dnia
                                    _dostawy.Add(new DostawaModel
                                    {
                                        IsHeaderRow = true,
                                        DataOdbioru = dataOdbioru,
                                        Dostawca = dataOdbioru.ToString("yyyy-MM-dd dddd", new CultureInfo("pl-PL"))
                                    });

                                    currentDate = dataOdbioru;
                                }

                                // Wiersz dostawy
                                var dostawa = new DostawaModel
                                {
                                    LP = reader["LP"]?.ToString(),
                                    DataOdbioru = dataOdbioru,
                                    Dostawca = reader["Dostawca"]?.ToString(),
                                    Auta = reader["Auta"] != DBNull.Value ? Convert.ToInt32(reader["Auta"]) : 0,
                                    SztukiDek = reader["SztukiDek"] != DBNull.Value ? Convert.ToDouble(reader["SztukiDek"]) : 0,
                                    WagaDek = reader["WagaDek"] != DBNull.Value ? Convert.ToDecimal(reader["WagaDek"]) : 0,
                                    Bufor = reader["bufor"]?.ToString(),
                                    TypCeny = reader["TypCeny"]?.ToString(),
                                    Cena = reader["Cena"] != DBNull.Value ? Convert.ToDecimal(reader["Cena"]) : 0,
                                    Distance = reader["Distance"] != DBNull.Value ? Convert.ToInt32(reader["Distance"]) : 0,
                                    Uwagi = reader["UWAGI"]?.ToString(),
                                    IsConfirmed = reader["bufor"]?.ToString() == "Potwierdzony",
                                    IsWstawienieConfirmed = reader["isConf"] != DBNull.Value && Convert.ToBoolean(reader["isConf"])
                                };

                                // Oblicz różnicę dni
                                if (reader["DataWstawienia"] != DBNull.Value)
                                {
                                    DateTime dataWstawienia = Convert.ToDateTime(reader["DataWstawienia"]);
                                    dostawa.RoznicaDni = (dataOdbioru - dataWstawienia).Days;
                                }

                                _dostawy.Add(dostawa);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string BuildDostawyQuery()
        {
            string sql = @"
                SELECT DISTINCT
                    HD.LP,
                    HD.DataOdbioru,
                    HD.Dostawca,
                    HD.Auta,
                    HD.SztukiDek,
                    HD.WagaDek,
                    HD.bufor,
                    HD.TypCeny,
                    HD.Cena,
                    WK.DataWstawienia,
                    D.Distance,
                    HD.Ubytek,
                    (SELECT TOP 1 N.Tresc FROM Notatki N WHERE N.IndeksID = HD.Lp ORDER BY N.DataUtworzenia DESC) AS UWAGI,
                    HD.PotwWaga,
                    HD.PotwSztuki,
                    WK.isConf,
                    CASE
                        WHEN HD.bufor = 'Potwierdzony' THEN 1
                        WHEN HD.bufor = 'B.Kontr.' THEN 2
                        WHEN HD.bufor = 'B.Wolny.' THEN 3
                        WHEN HD.bufor = 'Do Wykupienia' THEN 5
                        ELSE 4
                    END AS buforPriority
                FROM HarmonogramDostaw HD
                LEFT JOIN WstawieniaKurczakow WK ON HD.LpW = WK.Lp
                LEFT JOIN [LibraNet].[dbo].[Dostawcy] D ON HD.Dostawca = D.Name
                WHERE HD.DataOdbioru >= @startDate AND HD.DataOdbioru <= @endDate AND D.Halt = '0'";

            // Filtry
            if (chkAnulowane.IsChecked != true)
                sql += " AND bufor != 'Anulowany'";
            if (chkSprzedane.IsChecked != true)
                sql += " AND bufor != 'Sprzedany'";
            if (chkDoWykupienia.IsChecked != true)
                sql += " AND bufor != 'Do Wykupienia'";

            sql += @"
                ORDER BY
                    HD.DataOdbioru,
                    buforPriority,
                    HD.WagaDek DESC";

            return sql;
        }

        #endregion

        #region Obsługa kalendarza

        private void CalendarMain_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (calendarMain.SelectedDate.HasValue)
            {
                _selectedDate = calendarMain.SelectedDate.Value;
                UpdateWeekNumber();
                LoadDostawy();
            }
        }

        private void CalendarMain_DisplayDateChanged(object sender, CalendarDateChangedEventArgs e)
        {
            // Opcjonalnie - reaguj na zmianę wyświetlanego miesiąca
        }

        private void UpdateWeekNumber()
        {
            var cal = CultureInfo.CurrentCulture.Calendar;
            int week = cal.GetWeekOfYear(_selectedDate, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            txtWeekNumber.Text = week.ToString();
        }

        #endregion

        #region Nawigacja

        private void BtnPreviousWeek_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(-7);
            calendarMain.SelectedDate = _selectedDate;
            calendarMain.DisplayDate = _selectedDate;
            UpdateWeekNumber();
            LoadDostawy();
        }

        private void BtnNextWeek_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(7);
            calendarMain.SelectedDate = _selectedDate;
            calendarMain.DisplayDate = _selectedDate;
            UpdateWeekNumber();
            LoadDostawy();
        }

        private void BtnToday_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = DateTime.Today;
            calendarMain.SelectedDate = _selectedDate;
            calendarMain.DisplayDate = _selectedDate;
            UpdateWeekNumber();
            LoadDostawy();
        }

        #endregion

        #region Obsługa filtrów

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            // Pokaż/ukryj kolumnę ceny
            if (colCena != null)
                colCena.Visibility = chkPokazCeny.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            LoadDostawy();
        }

        #endregion

        #region Obsługa DataGrid

        private void DgDostawy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = dgDostawy.SelectedItem as DostawaModel;
            if (selected != null && !selected.IsHeaderRow)
            {
                // TODO: Etap 2 - załaduj szczegóły dostawy
            }
        }

        private void DgDostawy_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var selected = dgDostawy.SelectedItem as DostawaModel;
            if (selected != null && !selected.IsHeaderRow)
            {
                // TODO: Etap 2 - otwórz szczegóły dostawy
            }
        }

        private void DgDostawy_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            var dostawa = e.Row.DataContext as DostawaModel;
            if (dostawa == null) return;

            // Resetuj styl
            e.Row.Background = Brushes.White;
            e.Row.Foreground = Brushes.Black;
            e.Row.FontWeight = FontWeights.Normal;
            e.Row.Height = 32;

            // Separator
            if (dostawa.IsSeparator)
            {
                e.Row.Height = 8;
                e.Row.Background = new SolidColorBrush(Color.FromRgb(236, 239, 241));
                return;
            }

            // Nagłówek dnia
            if (dostawa.IsHeaderRow)
            {
                e.Row.Background = (SolidColorBrush)FindResource("HeaderDayBrush");
                e.Row.Foreground = Brushes.White;
                e.Row.FontWeight = FontWeights.Bold;
                e.Row.Height = 28;

                // Podświetl dzisiejszy dzień
                if (dostawa.DataOdbioru.Date == DateTime.Today)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(25, 118, 210)); // Niebieski
                }
                else if (dostawa.DataOdbioru.Date < DateTime.Today)
                {
                    e.Row.Background = Brushes.Black;
                }
                return;
            }

            // Kolorowanie według statusu
            switch (dostawa.Bufor)
            {
                case "Potwierdzony":
                    e.Row.Background = (SolidColorBrush)FindResource("StatusPotwierdzonyBrush");
                    e.Row.FontWeight = FontWeights.SemiBold;
                    break;
                case "Anulowany":
                    e.Row.Background = (SolidColorBrush)FindResource("StatusAnulowanyBrush");
                    break;
                case "Sprzedany":
                    e.Row.Background = (SolidColorBrush)FindResource("StatusSprzedanyBrush");
                    break;
                case "B.Kontr.":
                    e.Row.Background = (SolidColorBrush)FindResource("StatusBKontrBrush");
                    e.Row.Foreground = Brushes.White;
                    break;
                case "B.Wolny.":
                    e.Row.Background = (SolidColorBrush)FindResource("StatusBWolnyBrush");
                    break;
                case "Do Wykupienia":
                case "Do wykupienia":
                    e.Row.Background = (SolidColorBrush)FindResource("StatusDoWykupieniaBrush");
                    break;
            }
        }

        #endregion

        #region Akcje na dostawach

        private void BtnDateUp_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgDostawy.SelectedItem as DostawaModel;
            if (selected == null || selected.IsHeaderRow)
            {
                MessageBox.Show("Wybierz dostawę.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ChangeDeliveryDate(selected.LP, 1);
            LoadDostawy();
        }

        private void BtnDateDown_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgDostawy.SelectedItem as DostawaModel;
            if (selected == null || selected.IsHeaderRow)
            {
                MessageBox.Show("Wybierz dostawę.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ChangeDeliveryDate(selected.LP, -1);
            LoadDostawy();
        }

        private void ChangeDeliveryDate(string lp, int days)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    string sql = "UPDATE [LibraNet].[dbo].[HarmonogramDostaw] SET DataOdbioru = DATEADD(day, @dni, DataOdbioru) WHERE LP = @lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@dni", days);
                        cmd.Parameters.AddWithValue("@lp", lp);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zmiany daty: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnNowaDostawa_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Etap 2 - otwarcie formularza nowej dostawy
            MessageBox.Show("Funkcja dodawania nowej dostawy będzie dostępna w Etapie 2.", "Informacja",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDuplikuj_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgDostawy.SelectedItem as DostawaModel;
            if (selected == null || selected.IsHeaderRow)
            {
                MessageBox.Show("Wybierz dostawę do zduplikowania.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show("Czy na pewno chcesz zduplikować tę dostawę?", "Potwierdzenie",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DuplicateDelivery(selected.LP);
                LoadDostawy();
            }
        }

        private void DuplicateDelivery(string lp)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();

                    // Pobierz max LP
                    string getMaxLp = "SELECT MAX(Lp) FROM dbo.HarmonogramDostaw";
                    int newLp;
                    using (SqlCommand cmd = new SqlCommand(getMaxLp, conn))
                    {
                        newLp = Convert.ToInt32(cmd.ExecuteScalar()) + 1;
                    }

                    // Duplikuj
                    string sql = @"
                        INSERT INTO dbo.HarmonogramDostaw
                        (Lp, DataOdbioru, Dostawca, KmH, Kurnik, KmK, Auta, SztukiDek, WagaDek,
                        SztSzuflada, TypUmowy, TypCeny, Cena, Bufor, UWAGI, Dodatek, DataUtw, LpW, Ubytek, ktoStwo)
                        SELECT @newLp, DataOdbioru, Dostawca, KmH, Kurnik, KmK, Auta, SztukiDek, WagaDek,
                        SztSzuflada, TypUmowy, TypCeny, Cena, Bufor, UWAGI, Dodatek, GETDATE(), LpW, Ubytek, @userId
                        FROM dbo.HarmonogramDostaw WHERE Lp = @lp";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@newLp", newLp);
                        cmd.Parameters.AddWithValue("@lp", lp);
                        cmd.Parameters.AddWithValue("@userId", UserID ?? "0");
                        cmd.ExecuteNonQuery();
                    }

                    MessageBox.Show("Dostawa została zduplikowana.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas duplikowania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgDostawy.SelectedItem as DostawaModel;
            if (selected == null || selected.IsHeaderRow)
            {
                MessageBox.Show("Wybierz dostawę do usunięcia.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show("Czy na pewno chcesz usunąć tę dostawę? Nie lepiej anulować?", "Potwierdzenie",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                DeleteDelivery(selected.LP);
                LoadDostawy();
            }
        }

        private void DeleteDelivery(string lp)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    string sql = "DELETE FROM dbo.HarmonogramDostaw WHERE Lp = @lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", lp);
                        cmd.ExecuteNonQuery();
                    }
                    MessageBox.Show("Dostawa została usunięta.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas usuwania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDostawy();
        }

        #endregion

        #region Pomocnicze

        private string GetUserNameById(string userId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT Name FROM [LibraNet].[dbo].[operators] WHERE ID = @id";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", userId);
                        var result = cmd.ExecuteScalar();
                        return result?.ToString() ?? "-";
                    }
                }
            }
            catch
            {
                return "-";
            }
        }

        #endregion

        #region Cleanup

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer?.Stop();
            _priceTimer?.Stop();
            base.OnClosed(e);
        }

        #endregion
    }

    #region Model danych

    /// <summary>
    /// Model reprezentujący dostawę w tabeli
    /// </summary>
    public class DostawaModel : INotifyPropertyChanged
    {
        public string LP { get; set; }
        public DateTime DataOdbioru { get; set; }
        public string Dostawca { get; set; }
        public int Auta { get; set; }
        public double SztukiDek { get; set; }
        public decimal WagaDek { get; set; }
        public string Bufor { get; set; }
        public string TypCeny { get; set; }
        public decimal Cena { get; set; }
        public int Distance { get; set; }
        public string Uwagi { get; set; }
        public int? RoznicaDni { get; set; }

        private bool _isConfirmed;
        public bool IsConfirmed
        {
            get => _isConfirmed;
            set { _isConfirmed = value; OnPropertyChanged(); }
        }

        private bool _isWstawienieConfirmed;
        public bool IsWstawienieConfirmed
        {
            get => _isWstawienieConfirmed;
            set { _isWstawienieConfirmed = value; OnPropertyChanged(); }
        }

        // Flagi specjalne
        public bool IsHeaderRow { get; set; }
        public bool IsSeparator { get; set; }

        // Właściwości formatujące dla wyświetlania
        public string SztukiDekDisplay => IsHeaderRow ? "" : (SztukiDek > 0 ? $"{SztukiDek:#,0} szt" : "");
        public string WagaDekDisplay => IsHeaderRow ? "" : (WagaDek > 0 ? $"{WagaDek:0.00} kg" : "");
        public string CenaDisplay => IsHeaderRow ? "" : (Cena > 0 ? $"{Cena:0.00} zł" : "-");
        public string KmDisplay => IsHeaderRow ? "" : (Distance > 0 ? $"{Distance} km" : "-");
        public string RoznicaDniDisplay => IsHeaderRow ? "" : (RoznicaDni.HasValue ? $"{RoznicaDni} dni" : "-");

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    #endregion
}
