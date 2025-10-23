using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

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
                OnPropertyChanged(nameof(ProcentTekst));
            }
        }

        public string ProcentTekst => $"{_procent:F1}%";

        private double _procentWidth;
        public double ProcentWidth
        {
            get => _procentWidth;
            set
            {
                _procentWidth = value;
                OnPropertyChanged(nameof(ProcentWidth));
            }
        }

        private Brush _kolor;
        public Brush Kolor
        {
            get => _kolor;
            set
            {
                _kolor = value;
                OnPropertyChanged(nameof(Kolor));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class WstawienieDetails
    {
        public int Lp { get; set; }
        public string Dostawca { get; set; }
        public DateTime? DataWstawienia { get; set; }
        public int? IloscWstawienia { get; set; }
        public string KtoStworzyl { get; set; }
        public DateTime? DataUtw { get; set; }
        public string Status { get; set; }
        public string KtoPotwierdził { get; set; }
        public DateTime? DataConf { get; set; }
    }

    public partial class StatystykiPracownikow : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private DateTime currentStartDate;
        private DateTime currentEndDate;

        // Słownik do przechowywania stałych kolorów użytkowników
        private Dictionary<string, Color> userColors = new Dictionary<string, Color>();

        // Paleta kolorów dla wykresów
        private readonly Color[] chartColors = new Color[]
        {
            Color.FromRgb(92, 138, 58),   // Zielony
            Color.FromRgb(52, 152, 219),  // Niebieski
            Color.FromRgb(243, 156, 18),  // Pomarańczowy
            Color.FromRgb(155, 89, 182),  // Fioletowy
            Color.FromRgb(231, 76, 60),   // Czerwony
            Color.FromRgb(26, 188, 156),  // Turkusowy
            Color.FromRgb(241, 196, 15),  // Żółty
            Color.FromRgb(230, 126, 34),  // Pomarańczowy ciemny
            Color.FromRgb(149, 165, 166), // Szary
            Color.FromRgb(127, 140, 141)  // Szary ciemny
        };

        public StatystykiPracownikow()
        {
            InitializeComponent();
            // Domyślnie załaduj bieżący miesiąc
            FilterButton_Click(btnMiesiac, null);
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            // Pobierz okres z Tag przycisku
            string period = button.Tag?.ToString() ?? "";
            if (string.IsNullOrEmpty(period)) return;

            // Resetuj wygląd wszystkich przycisków
            ResetAllButtons();

            // Ustaw wygląd aktywnego przycisku (zielone tło, biały tekst)
            button.Background = new SolidColorBrush(Color.FromRgb(92, 138, 58)); // Zielony
            button.Foreground = Brushes.White;

            // Pobierz zakres dat i załaduj statystyki
            (DateTime startDate, DateTime endDate) = GetDateRangeFromTag(period);
            currentStartDate = startDate;
            currentEndDate = endDate;

            LoadAllStatistics();
        }

        private void ResetAllButtons()
        {
            // Kolor domyślny dla przycisków
            var defaultBackground = new SolidColorBrush(Color.FromRgb(234, 236, 238)); // #EAECEE
            var defaultForeground = new SolidColorBrush(Color.FromRgb(44, 62, 80));   // #2C3E50

            // Resetuj przyciski szybkiego wyboru
            var quickButtons = new[] { btnDzis, btnWczoraj, btnTydzien, btnPoprzedniTydzien, 
                                       btnMiesiac, btnPoprzedniMiesiac, btnRok, btnPoprzedniRok };
            foreach (var btn in quickButtons)
            {
                btn.Background = defaultBackground;
                btn.Foreground = defaultForeground;
            }

            // Resetuj wszystkie przyciski miesięcy w panelu głównym
            ResetMonthButtonsInPanel(this);
        }

        private void ResetMonthButtonsInPanel(DependencyObject parent)
        {
            var defaultBackground = new SolidColorBrush(Color.FromRgb(234, 236, 238)); // #EAECEE
            var defaultForeground = new SolidColorBrush(Color.FromRgb(44, 62, 80));   // #2C3E50

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is Button btn && btn.Tag != null && btn.Tag.ToString().Contains("-"))
                {
                    btn.Background = defaultBackground;
                    btn.Foreground = defaultForeground;
                }
                
                ResetMonthButtonsInPanel(child);
            }
        }

        private (DateTime, DateTime) GetDateRangeFromTag(string tag)
        {
            DateTime today = DateTime.Today;

            // Sprawdź czy to konkretny miesiąc (format: 2025-01, 2024-12 itp.)
            if (tag.Contains("-") && tag.Length == 7)
            {
                try
                {
                    var parts = tag.Split('-');
                    int year = int.Parse(parts[0]);
                    int month = int.Parse(parts[1]);
                    
                    DateTime startDate = new DateTime(year, month, 1);
                    DateTime endDate = startDate.AddMonths(1);
                    
                    // Jeśli to bieżący miesiąc, kończymy na dzisiejszej dacie + 1 dzień
                    if (year == today.Year && month == today.Month)
                    {
                        endDate = today.AddDays(1);
                    }
                    
                    return (startDate, endDate);
                }
                catch
                {
                    // Jeśli parsing się nie powiódł, zwróć dzisiaj
                    return (today, today.AddDays(1));
                }
            }

            // Szybkie opcje
            switch (tag)
            {
                case "Today":
                    return (today, today.AddDays(1));
                
                case "Yesterday":
                    return (today.AddDays(-1), today);
                
                case "Week":
                    DateTime startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
                    return (startOfWeek, today.AddDays(1));
                
                case "LastWeek":
                    DateTime lastWeekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday - 7);
                    DateTime lastWeekEnd = lastWeekStart.AddDays(7);
                    return (lastWeekStart, lastWeekEnd);
                
                case "Month":
                    DateTime startOfMonth = new DateTime(today.Year, today.Month, 1);
                    return (startOfMonth, today.AddDays(1));
                
                case "LastMonth":
                    DateTime lastMonthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                    DateTime lastMonthEnd = lastMonthStart.AddMonths(1);
                    return (lastMonthStart, lastMonthEnd);
                
                case "Year":
                    DateTime startOfYear = new DateTime(today.Year, 1, 1);
                    return (startOfYear, today.AddDays(1));
                
                case "LastYear":
                    DateTime lastYearStart = new DateTime(today.Year - 1, 1, 1);
                    DateTime lastYearEnd = new DateTime(today.Year, 1, 1);
                    return (lastYearStart, lastYearEnd);
                
                default:
                    return (today, today.AddDays(1));
            }
        }

        private void LoadAllStatistics()
        {
            try
            {
                // Załaduj podsumowania
                LoadSummary();

                // Załaduj statystyki utworzeń i potwierdzeń
                var stworzone = LoadStatistics("Stworzone", currentStartDate, currentEndDate);
                var potwierdzone = LoadStatistics("Potwierdzone", currentStartDate, currentEndDate);

                // Zbierz wszystkich unikalnych użytkowników i przypisz im kolory
                var allUsers = new HashSet<string>();
                foreach (var stat in stworzone) allUsers.Add(stat.NazwaPracownika);
                foreach (var stat in potwierdzone) allUsers.Add(stat.NazwaPracownika);

                // Wyczyść stare mapowanie kolorów i stwórz nowe dla wszystkich użytkowników
                userColors.Clear();
                int colorIndex = 0;
                foreach (var user in allUsers.OrderBy(u => u))
                {
                    userColors[user] = chartColors[colorIndex % chartColors.Length];
                    colorIndex++;
                }

                // Przypisz kolory zgodnie z mapowaniem
                AssignColors(stworzone);
                AssignColors(potwierdzone);

                // Ustaw źródła danych
                itemsStworzone.ItemsSource = stworzone;
                itemsPotwierdzone.ItemsSource = potwierdzone;

                // Narysuj wykresy kołowe
                DrawPieChart(canvasStworzone, stworzone);
                DrawPieChart(canvasPotwierdzone, potwierdzone);

                // Załaduj szczegółową tabelę
                LoadDetailsTable();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania statystyk: {ex.Message}", 
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSummary()
        {
            string query = @"
                SELECT 
                    COUNT(*) as TotalStworzone,
                    SUM(CASE WHEN isConf = 1 THEN 1 ELSE 0 END) as TotalPotwierdzone,
                    SUM(CASE WHEN isConf = 0 OR isConf IS NULL THEN 1 ELSE 0 END) as TotalOczekujace
                FROM dbo.WstawieniaKurczakow
                WHERE DataUtw >= @StartDate AND DataUtw < @EndDate";

            using (var connection = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@StartDate", currentStartDate);
                cmd.Parameters.AddWithValue("@EndDate", currentEndDate);

                connection.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int stworzone = Convert.ToInt32(reader["TotalStworzone"]);
                        int potwierdzone = Convert.ToInt32(reader["TotalPotwierdzone"]);
                        int oczekujace = Convert.ToInt32(reader["TotalOczekujace"]);

                        txtTotalStworzone.Text = stworzone.ToString();
                        txtTotalPotwierdzone.Text = potwierdzone.ToString();
                        txtTotalOczekujace.Text = oczekujace.ToString();

                        if (stworzone > 0)
                        {
                            double procent = (double)potwierdzone / stworzone * 100;
                            txtProcentPotwierdzone.Text = $"{procent:F1}%";
                        }
                        else
                        {
                            txtProcentPotwierdzone.Text = "0%";
                        }
                    }
                }
            }
        }

        private ObservableCollection<PracownikStat> LoadStatistics(string type, DateTime startDate, DateTime endDate)
        {
            var stats = new ObservableCollection<PracownikStat>();
            string query = "";

            switch (type)
            {
                case "Stworzone":
                    query = @"
                        SELECT ISNULL(o.Name, 'Nieznany') AS UserName, COUNT(*) AS TotalCount
                        FROM dbo.WstawieniaKurczakow w
                        LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                        WHERE w.DataUtw >= @StartDate AND w.DataUtw < @EndDate AND w.KtoStwo IS NOT NULL
                        GROUP BY o.Name
                        ORDER BY TotalCount DESC";
                    break;
                    
                case "Potwierdzone":
                    query = @"
                        SELECT ISNULL(o.Name, 'Nieznany') AS UserName, COUNT(*) AS TotalCount
                        FROM dbo.WstawieniaKurczakow w
                        LEFT JOIN dbo.operators o ON w.KtoConf = o.ID
                        WHERE w.isConf = 1 AND w.DataConf >= @StartDate AND w.DataConf < @EndDate AND w.KtoConf IS NOT NULL
                        GROUP BY o.Name
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

            // Oblicz procenty
            int totalActions = 0;
            foreach (var stat in stats) totalActions += stat.LiczbaAkcji;

            if (totalActions > 0)
            {
                double maxWidth = 300; // Maksymalna szerokość paska w pikselach
                foreach (var stat in stats)
                {
                    stat.Procent = (double)stat.LiczbaAkcji / totalActions * 100;
                    stat.ProcentWidth = (double)stat.LiczbaAkcji / totalActions * maxWidth;
                }
            }

            return stats;
        }

        private Color GetColorForUser(string userName)
        {
            // Jeśli użytkownik już ma przypisany kolor, zwróć go
            if (userColors.ContainsKey(userName))
            {
                return userColors[userName];
            }

            // Przypisz nowy kolor z palety
            int colorIndex = userColors.Count % chartColors.Length;
            Color newColor = chartColors[colorIndex];
            userColors[userName] = newColor;

            return newColor;
        }

        private void AssignColors(ObservableCollection<PracownikStat> stats)
        {
            foreach (var stat in stats)
            {
                stat.Kolor = new SolidColorBrush(GetColorForUser(stat.NazwaPracownika));
            }
        }

        private void DrawPieChart(Canvas canvas, ObservableCollection<PracownikStat> stats)
        {
            canvas.Children.Clear();

            if (stats.Count == 0) return;

            double centerX = canvas.Width / 2;
            double centerY = canvas.Height / 2;
            double radius = Math.Min(centerX, centerY) - 10;

            double startAngle = -90; // Rozpocznij od góry

            foreach (var stat in stats)
            {
                double angle = stat.Procent * 3.6; // 360 stopni / 100%

                // Narysuj wycinek
                var path = CreatePieSlice(centerX, centerY, radius, startAngle, angle, stat.Kolor);
                canvas.Children.Add(path);

                startAngle += angle;
            }

            // Dodaj białe koło w środku (efekt donut)
            var centerCircle = new Ellipse
            {
                Width = radius * 0.6,
                Height = radius * 0.6,
                Fill = Brushes.White
            };
            Canvas.SetLeft(centerCircle, centerX - radius * 0.3);
            Canvas.SetTop(centerCircle, centerY - radius * 0.3);
            canvas.Children.Add(centerCircle);
        }

        private Path CreatePieSlice(double centerX, double centerY, double radius, double startAngle, double angle, Brush fill)
        {
            double startAngleRad = startAngle * Math.PI / 180;
            double endAngleRad = (startAngle + angle) * Math.PI / 180;

            Point start = new Point(
                centerX + radius * Math.Cos(startAngleRad),
                centerY + radius * Math.Sin(startAngleRad)
            );

            Point end = new Point(
                centerX + radius * Math.Cos(endAngleRad),
                centerY + radius * Math.Sin(endAngleRad)
            );

            var figure = new PathFigure { StartPoint = new Point(centerX, centerY) };
            figure.Segments.Add(new LineSegment(start, true));
            figure.Segments.Add(new ArcSegment
            {
                Point = end,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = angle > 180
            });
            figure.Segments.Add(new LineSegment(new Point(centerX, centerY), true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            return new Path
            {
                Data = geometry,
                Fill = fill,
                Stroke = Brushes.White,
                StrokeThickness = 2
            };
        }

        private void LoadDetailsTable()
        {
            string query = @"
                SELECT 
                    w.Lp,
                    w.Dostawca,
                    w.DataWstawienia,
                    w.IloscWstawienia,
                    os.Name as KtoStworzyl,
                    w.DataUtw,
                    CASE WHEN w.isConf = 1 THEN 'Potwierdzone' ELSE 'Oczekujące' END as Status,
                    oc.Name as KtoPotwierdził,
                    w.DataConf
                FROM dbo.WstawieniaKurczakow w
                LEFT JOIN dbo.operators os ON w.KtoStwo = os.ID
                LEFT JOIN dbo.operators oc ON w.KtoConf = oc.ID
                WHERE w.DataUtw >= @StartDate AND w.DataUtw < @EndDate
                ORDER BY w.Lp DESC";

            var details = new List<WstawienieDetails>();

            using (var connection = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@StartDate", currentStartDate);
                cmd.Parameters.AddWithValue("@EndDate", currentEndDate);

                connection.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        details.Add(new WstawienieDetails
                        {
                            Lp = Convert.ToInt32(reader["Lp"]),
                            Dostawca = reader["Dostawca"]?.ToString() ?? "",
                            DataWstawienia = reader["DataWstawienia"] as DateTime?,
                            IloscWstawienia = reader["IloscWstawienia"] as int?,
                            KtoStworzyl = reader["KtoStworzyl"]?.ToString() ?? "Nieznany",
                            DataUtw = reader["DataUtw"] as DateTime?,
                            Status = reader["Status"]?.ToString() ?? "",
                            KtoPotwierdził = reader["KtoPotwierdził"]?.ToString() ?? "-",
                            DataConf = reader["DataConf"] as DateTime?
                        });
                    }
                }
            }

            dgSzczegoly.ItemsSource = details;
        }

        private void BtnPracownikDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button == null) return;

                string nazwaPracownika = button.Tag?.ToString();
                if (string.IsNullOrEmpty(nazwaPracownika)) return;

                // Otworz okno ze szczegolami pracownika
                var detailsWindow = new SzczegółyPracownika(nazwaPracownika, currentStartDate, currentEndDate);
                detailsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad otwierania szczegolow: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
