using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Kalendarz1.Zywiec.Kalendarz;

namespace Kalendarz1
{
    public class PracownikStat : INotifyPropertyChanged
    {
        public string NazwaPracownika { get; set; }
        public string UserID { get; set; }
        public int LiczbaAkcji { get; set; }

        private ImageSource _avatarSource;
        public ImageSource AvatarSource
        {
            get => _avatarSource;
            set
            {
                _avatarSource = value;
                OnPropertyChanged(nameof(AvatarSource));
            }
        }

        public string Initials => GetInitials(NazwaPracownika);

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

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

    public class WstawienieDetails : INotifyPropertyChanged
    {
        public int Lp { get; set; }
        public string Dostawca { get; set; }
        public DateTime? DataWstawienia { get; set; }
        public int? IloscWstawienia { get; set; }
        public string KtoStworzyl { get; set; }
        public string KtoStworzylID { get; set; }
        public DateTime? DataUtw { get; set; }
        public string Status { get; set; }
        public string KtoPotwierdził { get; set; }
        public string KtoPotwierdziłID { get; set; }
        public DateTime? DataConf { get; set; }

        private ImageSource _avatarStworzyl;
        public ImageSource AvatarStworzyl
        {
            get => _avatarStworzyl;
            set
            {
                _avatarStworzyl = value;
                OnPropertyChanged(nameof(AvatarStworzyl));
            }
        }

        private ImageSource _avatarPotwierdził;
        public ImageSource AvatarPotwierdził
        {
            get => _avatarPotwierdził;
            set
            {
                _avatarPotwierdził = value;
                OnPropertyChanged(nameof(AvatarPotwierdził));
            }
        }

        public string InitialsStworzyl => GetInitials(KtoStworzyl);
        public string InitialsPotwierdził => GetInitials(KtoPotwierdził);

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "-") return "?";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // ==================== KPI Model ====================
    public class ZakupowiecKpi : INotifyPropertyChanged
    {
        public int Pozycja { get; set; }
        public string Nazwa { get; set; }
        public string UserID { get; set; }
        public int Utworzone { get; set; }
        public int Potwierdzone { get; set; }
        public double ProcentPotwierdzonych { get; set; }
        public double SredniCzasPotwGodziny { get; set; }
        public int LiczbaDostawcow { get; set; }
        public long Wolumen { get; set; }
        public double Produktywnosc { get; set; }

        public string Inicjaly
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Nazwa)) return "?";
                var parts = Nazwa.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    return $"{parts[0][0]}{parts[1][0]}".ToUpper();
                return Nazwa.Length >= 2 ? Nazwa.Substring(0, 2).ToUpper() : Nazwa.ToUpper();
            }
        }

        public Brush KolorBrush { get; set; }

        private ImageSource _avatarSource;
        public ImageSource AvatarSource
        {
            get => _avatarSource;
            set
            {
                _avatarSource = value;
                OnPropertyChanged(nameof(AvatarSource));
            }
        }

        public string ProcentPotwTekst => $"{ProcentPotwierdzonych:F0}%";
        public double ProcentPotwWidth => ProcentPotwierdzonych * 0.6; // max ~60px for 100%

        public string SredniCzasPotwTekst
        {
            get
            {
                if (SredniCzasPotwGodziny <= 0) return "-";
                if (SredniCzasPotwGodziny < 1) return $"{SredniCzasPotwGodziny * 60:F0} min";
                if (SredniCzasPotwGodziny < 24) return $"{SredniCzasPotwGodziny:F1} godz.";
                return $"{SredniCzasPotwGodziny / 24:F1} dni";
            }
        }

        public string WolumenTekst => Wolumen.ToString("N0");
        public string ProduktywTekst => $"{Produktywnosc:F1}";

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // ==================== CROSS-MODULE KPI Model ====================
    public class CrossModuleKpi : INotifyPropertyChanged
    {
        public int Pozycja { get; set; }
        public string Nazwa { get; set; }
        public string UserID { get; set; }
        public Brush KolorBrush { get; set; }

        private ImageSource _avatarSource;
        public ImageSource AvatarSource
        {
            get => _avatarSource;
            set { _avatarSource = value; OnPropertyChanged(nameof(AvatarSource)); }
        }

        public string Inicjaly
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Nazwa)) return "?";
                var parts = Nazwa.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2) return $"{parts[0][0]}{parts[1][0]}".ToUpper();
                return Nazwa.Length >= 2 ? Nazwa.Substring(0, 2).ToUpper() : Nazwa.ToUpper();
            }
        }

        // Wstawienia
        public int WstawieniaUtworzone { get; set; }
        public int WstawieniaPotwierdzone { get; set; }
        public int WstawieniaTotal => WstawieniaUtworzone + WstawieniaPotwierdzone;

        // Kalendarz Dostaw
        public int KalendarzDostawy { get; set; }
        public int KalendarzPotwWagi { get; set; }
        public int KalendarzPotwSztuk { get; set; }
        public int KalendarzTotal => KalendarzDostawy + KalendarzPotwWagi + KalendarzPotwSztuk;

        // Specyfikacja (wprowadzenia + weryfikacje z RozliczeniaZatwierdzenia)
        public int SpecWprowadzenia { get; set; }
        public int SpecWeryfikacje { get; set; }
        public int SpecyfikacjaTotal => SpecWprowadzenia + SpecWeryfikacje;

        // Pozyskiwanie Hodowcow (CRM: Pozyskiwanie_Aktywnosci + OcenyDostawcow)
        public int HodowcyTelefony { get; set; }
        public int HodowcyNotatki { get; set; }
        public int HodowcyOceny { get; set; }
        public int HodowcyTotal => HodowcyTelefony + HodowcyNotatki + HodowcyOceny;

        // Dokumenty
        public int DokumentyUtworzone { get; set; }
        public int DokumentyWyslane { get; set; }
        public int DokumentyOtrzymane { get; set; }
        public int DokumentyTotal => DokumentyUtworzone + DokumentyWyslane + DokumentyOtrzymane;

        // Wnioski
        public int WnioskiZlozone { get; set; }
        public int WnioskiRozpatrzone { get; set; }
        public int WnioskiTotal => WnioskiZlozone + WnioskiRozpatrzone;

        // Suma
        public int SumaAkcji => WstawieniaTotal + KalendarzTotal + SpecyfikacjaTotal
                              + HodowcyTotal + DokumentyTotal + WnioskiTotal;

        // Tooltips
        public string WstawieniaTooltip => $"Utworzone: {WstawieniaUtworzone}\nPotwierdzone: {WstawieniaPotwierdzone}\n\nZrodlo: WstawieniaKurczakow\nDwuklik = szczegoly";
        public string KalendarzTooltip => $"Dostawy utworzone: {KalendarzDostawy}\nPotw. wagi: {KalendarzPotwWagi}\nPotw. sztuk: {KalendarzPotwSztuk}\n\nZrodlo: HarmonogramDostaw\nDwuklik = szczegoly";
        public string SpecyfikacjaTooltip => $"Wprowadzenia: {SpecWprowadzenia}\nWeryfikacje: {SpecWeryfikacje}\n\nZrodlo: RozliczeniaZatwierdzenia\nDwuklik = szczegoly";
        public string HodowcyTooltip => $"Telefony do hodowcow: {HodowcyTelefony}\nNotatki CRM: {HodowcyNotatki}\nOceny dostawcow: {HodowcyOceny}\n\nZrodlo: Pozyskiwanie_Aktywnosci + OcenyDostawcow\nDwuklik = szczegoly";
        public string DokumentyTooltip => $"Utworzone: {DokumentyUtworzone}\nWyslane: {DokumentyWyslane}\nOtrzymane: {DokumentyOtrzymane}\n\nZrodlo: HarmonogramDostaw (flagi dok.)\nDwuklik = szczegoly";
        public string WnioskiTooltip => $"Zlozone: {WnioskiZlozone}\nRozpatrzone: {WnioskiRozpatrzone}\n\nZrodlo: DostawcyCR\nDwuklik = szczegoly";

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class StatystykiPracownikow : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private DateTime currentStartDate;
        private DateTime currentEndDate;

        private Dictionary<string, Color> userColors = new Dictionary<string, Color>();

        private readonly Color[] chartColors = new Color[]
        {
            Color.FromRgb(92, 138, 58),
            Color.FromRgb(52, 152, 219),
            Color.FromRgb(243, 156, 18),
            Color.FromRgb(155, 89, 182),
            Color.FromRgb(231, 76, 60),
            Color.FromRgb(26, 188, 156),
            Color.FromRgb(241, 196, 15),
            Color.FromRgb(230, 126, 34),
            Color.FromRgb(149, 165, 166),
            Color.FromRgb(127, 140, 141)
        };

        public StatystykiPracownikow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            FilterButton_Click(btnMiesiac, null);
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            string period = button.Tag?.ToString() ?? "";
            if (string.IsNullOrEmpty(period)) return;

            ResetAllButtons();

            button.Background = new SolidColorBrush(Color.FromRgb(92, 138, 58));
            button.Foreground = Brushes.White;

            (DateTime startDate, DateTime endDate) = GetDateRangeFromTag(period);
            currentStartDate = startDate;
            currentEndDate = endDate;

            LoadAllStatistics();
        }

        private void ResetAllButtons()
        {
            var defaultBackground = new SolidColorBrush(Color.FromRgb(234, 236, 238));
            var defaultForeground = new SolidColorBrush(Color.FromRgb(44, 62, 80));

            var quickButtons = new[] { btnDzis, btnWczoraj, btnTydzien, btnPoprzedniTydzien,
                                       btnMiesiac, btnPoprzedniMiesiac, btnRok, btnPoprzedniRok };
            foreach (var btn in quickButtons)
            {
                btn.Background = defaultBackground;
                btn.Foreground = defaultForeground;
            }

            ResetMonthButtonsInPanel(this);
        }

        private void ResetMonthButtonsInPanel(DependencyObject parent)
        {
            var defaultBackground = new SolidColorBrush(Color.FromRgb(234, 236, 238));
            var defaultForeground = new SolidColorBrush(Color.FromRgb(44, 62, 80));

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

            if (tag.Contains("-") && tag.Length == 7)
            {
                try
                {
                    var parts = tag.Split('-');
                    int year = int.Parse(parts[0]);
                    int month = int.Parse(parts[1]);

                    DateTime startDate = new DateTime(year, month, 1);
                    DateTime endDate = startDate.AddMonths(1);

                    if (year == today.Year && month == today.Month)
                    {
                        endDate = today.AddDays(1);
                    }

                    return (startDate, endDate);
                }
                catch
                {
                    return (today, today.AddDays(1));
                }
            }

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
                LoadSummary();

                var stworzone = LoadStatistics("Stworzone", currentStartDate, currentEndDate);
                var potwierdzone = LoadStatistics("Potwierdzone", currentStartDate, currentEndDate);

                var allUsers = new HashSet<string>();
                foreach (var stat in stworzone) allUsers.Add(stat.NazwaPracownika);
                foreach (var stat in potwierdzone) allUsers.Add(stat.NazwaPracownika);

                userColors.Clear();
                int colorIndex = 0;
                foreach (var user in allUsers.OrderBy(u => u))
                {
                    userColors[user] = chartColors[colorIndex % chartColors.Length];
                    colorIndex++;
                }

                AssignColors(stworzone);
                AssignColors(potwierdzone);

                itemsStworzone.ItemsSource = stworzone;
                itemsPotwierdzone.ItemsSource = potwierdzone;

                DrawPieChart(canvasStworzone, stworzone);
                DrawPieChart(canvasPotwierdzone, potwierdzone);

                LoadDetailsTable();
                LoadKpiRanking();
                LoadCrossModuleKpi();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad podczas ladowania statystyk: {ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
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

                        // Header badges
                        txtHeaderUtw.Text = stworzone.ToString();
                        txtHeaderPotw.Text = potwierdzone.ToString();
                        txtHeaderOcz.Text = oczekujace.ToString();

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
                        SELECT ISNULL(o.Name, 'Nieznany') AS UserName,
                               CAST(w.KtoStwo AS VARCHAR(20)) AS UserID,
                               COUNT(*) AS TotalCount
                        FROM dbo.WstawieniaKurczakow w
                        LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                        WHERE w.DataUtw >= @StartDate AND w.DataUtw < @EndDate AND w.KtoStwo IS NOT NULL
                        GROUP BY o.Name, w.KtoStwo
                        ORDER BY TotalCount DESC";
                    break;

                case "Potwierdzone":
                    query = @"
                        SELECT ISNULL(o.Name, 'Nieznany') AS UserName,
                               CAST(w.KtoConf AS VARCHAR(20)) AS UserID,
                               COUNT(*) AS TotalCount
                        FROM dbo.WstawieniaKurczakow w
                        LEFT JOIN dbo.operators o ON w.KtoConf = o.ID
                        WHERE w.isConf = 1 AND w.DataConf >= @StartDate AND w.DataConf < @EndDate AND w.KtoConf IS NOT NULL
                        GROUP BY o.Name, w.KtoConf
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
                            UserID = reader["UserID"]?.ToString(),
                            LiczbaAkcji = Convert.ToInt32(reader["TotalCount"])
                        });
                    }
                }
            }

            int totalActions = 0;
            foreach (var stat in stats) totalActions += stat.LiczbaAkcji;

            if (totalActions > 0)
            {
                double maxWidth = 300;
                foreach (var stat in stats)
                {
                    stat.Procent = (double)stat.LiczbaAkcji / totalActions * 100;
                    stat.ProcentWidth = (double)stat.LiczbaAkcji / totalActions * maxWidth;
                }
            }

            LoadAvatarsForStats(stats);

            return stats;
        }

        private void LoadAvatarsForStats(ObservableCollection<PracownikStat> stats)
        {
            foreach (var stat in stats)
            {
                if (!string.IsNullOrEmpty(stat.UserID))
                {
                    string userId = stat.UserID;
                    Task.Run(() =>
                    {
                        var avatarBitmap = UserAvatarManager.GetAvatar(userId);
                        if (avatarBitmap != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                stat.AvatarSource = ConvertToImageSource(avatarBitmap);
                            });
                        }
                    });
                }
            }
        }

        private ImageSource ConvertToImageSource(System.Drawing.Image image)
        {
            using (var memory = new MemoryStream())
            {
                image.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = memory;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }

        private Color GetColorForUser(string userName)
        {
            if (userColors.ContainsKey(userName))
            {
                return userColors[userName];
            }

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

            double startAngle = -90;

            foreach (var stat in stats)
            {
                double angle = stat.Procent * 3.6;

                var path = CreatePieSlice(centerX, centerY, radius, startAngle, angle, stat.Kolor);
                canvas.Children.Add(path);

                startAngle += angle;
            }

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

        private System.Windows.Shapes.Path CreatePieSlice(double centerX, double centerY, double radius, double startAngle, double angle, Brush fill)
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

            return new System.Windows.Shapes.Path
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
                    CAST(w.KtoStwo AS VARCHAR(20)) as KtoStworzylID,
                    w.DataUtw,
                    CASE WHEN w.isConf = 1 THEN 'Potwierdzone' ELSE 'Oczekujace' END as Status,
                    oc.Name as KtoPotwierdził,
                    CAST(w.KtoConf AS VARCHAR(20)) as KtoPotwierdziłID,
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
                            KtoStworzylID = reader["KtoStworzylID"]?.ToString(),
                            DataUtw = reader["DataUtw"] as DateTime?,
                            Status = reader["Status"]?.ToString() ?? "",
                            KtoPotwierdził = reader["KtoPotwierdził"]?.ToString() ?? "-",
                            KtoPotwierdziłID = reader["KtoPotwierdziłID"]?.ToString(),
                            DataConf = reader["DataConf"] as DateTime?
                        });
                    }
                }
            }

            dgSzczegoly.ItemsSource = details;

            LoadAvatarsForDetails(details);
        }

        private void LoadAvatarsForDetails(List<WstawienieDetails> details)
        {
            foreach (var detail in details)
            {
                if (!string.IsNullOrEmpty(detail.KtoStworzylID))
                {
                    string userId = detail.KtoStworzylID;
                    Task.Run(() =>
                    {
                        var avatarBitmap = UserAvatarManager.GetAvatar(userId);
                        if (avatarBitmap != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                detail.AvatarStworzyl = ConvertToImageSource(avatarBitmap);
                            });
                        }
                    });
                }

                if (!string.IsNullOrEmpty(detail.KtoPotwierdziłID))
                {
                    string userId = detail.KtoPotwierdziłID;
                    Task.Run(() =>
                    {
                        var avatarBitmap = UserAvatarManager.GetAvatar(userId);
                        if (avatarBitmap != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                detail.AvatarPotwierdził = ConvertToImageSource(avatarBitmap);
                            });
                        }
                    });
                }
            }
        }

        // ==================== KPI RANKING ====================

        private void LoadKpiRanking()
        {
            string query = @"
                SELECT
                    ISNULL(o.Name, 'Nieznany') AS Nazwa,
                    CAST(w.KtoStwo AS VARCHAR(20)) AS UserID,
                    COUNT(*) AS Utworzone,
                    SUM(CASE WHEN w.isConf = 1 THEN 1 ELSE 0 END) AS Potwierdzone,
                    AVG(CASE WHEN w.isConf = 1 AND w.DataConf IS NOT NULL AND w.DataUtw IS NOT NULL
                        THEN DATEDIFF(MINUTE, w.DataUtw, w.DataConf)
                        ELSE NULL END) AS SrCzasMinut,
                    COUNT(DISTINCT w.Dostawca) AS LiczbaDostawcow,
                    ISNULL(SUM(CAST(w.IloscWstawienia AS BIGINT)), 0) AS Wolumen,
                    DATEDIFF(DAY, @StartDate, @EndDate) AS DniOkresu
                FROM dbo.WstawieniaKurczakow w
                LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                WHERE w.DataUtw >= @StartDate AND w.DataUtw < @EndDate
                    AND w.KtoStwo IS NOT NULL
                GROUP BY o.Name, w.KtoStwo
                ORDER BY Utworzone DESC";

            var kpiList = new List<ZakupowiecKpi>();

            using (var connection = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@StartDate", currentStartDate);
                cmd.Parameters.AddWithValue("@EndDate", currentEndDate);

                connection.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    int pos = 1;
                    while (reader.Read())
                    {
                        int utworzone = Convert.ToInt32(reader["Utworzone"]);
                        int potwierdzone = Convert.ToInt32(reader["Potwierdzone"]);
                        double srCzasMinut = reader["SrCzasMinut"] != DBNull.Value ? Convert.ToDouble(reader["SrCzasMinut"]) : 0;
                        int dniOkresu = Math.Max(1, Convert.ToInt32(reader["DniOkresu"]));
                        // Estimate working days as ~5/7 of calendar days, minimum 1
                        int dniRobocze = Math.Max(1, (int)Math.Ceiling(dniOkresu * 5.0 / 7.0));

                        string nazwa = reader["Nazwa"].ToString();
                        var kpi = new ZakupowiecKpi
                        {
                            Pozycja = pos++,
                            Nazwa = nazwa,
                            UserID = reader["UserID"]?.ToString(),
                            Utworzone = utworzone,
                            Potwierdzone = potwierdzone,
                            ProcentPotwierdzonych = utworzone > 0 ? (double)potwierdzone / utworzone * 100 : 0,
                            SredniCzasPotwGodziny = srCzasMinut / 60.0,
                            LiczbaDostawcow = Convert.ToInt32(reader["LiczbaDostawcow"]),
                            Wolumen = Convert.ToInt64(reader["Wolumen"]),
                            Produktywnosc = (double)utworzone / dniRobocze,
                            KolorBrush = new SolidColorBrush(GetColorForUser(nazwa))
                        };

                        kpiList.Add(kpi);
                    }
                }
            }

            dgKpiRanking.ItemsSource = kpiList;

            // Load avatars
            foreach (var kpi in kpiList)
            {
                if (!string.IsNullOrEmpty(kpi.UserID))
                {
                    string userId = kpi.UserID;
                    Task.Run(() =>
                    {
                        var avatarBitmap = UserAvatarManager.GetAvatar(userId);
                        if (avatarBitmap != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                kpi.AvatarSource = ConvertToImageSource(avatarBitmap);
                            });
                        }
                    });
                }
            }

            // Update top performer cards
            UpdateTopPerformers(kpiList);
        }

        private void UpdateTopPerformers(List<ZakupowiecKpi> kpiList)
        {
            if (kpiList.Count == 0)
            {
                txtTopWolumen.Text = "-";
                txtTopWolumenVal.Text = "";
                txtTopCzas.Text = "-";
                txtTopCzasVal.Text = "";
                txtTopProcent.Text = "-";
                txtTopProcentVal.Text = "";
                txtTopDostawcy.Text = "-";
                txtTopDostawcyVal.Text = "";
                return;
            }

            // Najwyzszy wolumen
            var topWol = kpiList.OrderByDescending(k => k.Wolumen).First();
            txtTopWolumen.Text = topWol.Nazwa;
            txtTopWolumenVal.Text = $"{topWol.WolumenTekst} szt.";

            // Najszybsze potwierdzenie (lowest avg time, excluding 0)
            var withTime = kpiList.Where(k => k.SredniCzasPotwGodziny > 0).ToList();
            if (withTime.Count > 0)
            {
                var topCzas = withTime.OrderBy(k => k.SredniCzasPotwGodziny).First();
                txtTopCzas.Text = topCzas.Nazwa;
                txtTopCzasVal.Text = topCzas.SredniCzasPotwTekst;
            }
            else
            {
                txtTopCzas.Text = "-";
                txtTopCzasVal.Text = "brak danych";
            }

            // Najwyzszy % potwierdzonych (min 5 wstawien)
            var withEnough = kpiList.Where(k => k.Utworzone >= 5).ToList();
            if (withEnough.Count > 0)
            {
                var topProc = withEnough.OrderByDescending(k => k.ProcentPotwierdzonych).First();
                txtTopProcent.Text = topProc.Nazwa;
                txtTopProcentVal.Text = $"{topProc.ProcentPotwierdzonych:F0}%";
            }
            else
            {
                // Fall back to all
                var topProc = kpiList.OrderByDescending(k => k.ProcentPotwierdzonych).First();
                txtTopProcent.Text = topProc.Nazwa;
                txtTopProcentVal.Text = $"{topProc.ProcentPotwierdzonych:F0}%";
            }

            // Najwiecej dostawcow
            var topDost = kpiList.OrderByDescending(k => k.LiczbaDostawcow).First();
            txtTopDostawcy.Text = topDost.Nazwa;
            txtTopDostawcyVal.Text = $"{topDost.LiczbaDostawcow} dostawcow";
        }

        private void BtnPracownikDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button == null) return;

                string nazwaPracownika = button.Tag?.ToString();
                if (string.IsNullOrEmpty(nazwaPracownika)) return;

                var detailsWindow = new SzczegółyPracownika(nazwaPracownika, currentStartDate, currentEndDate);
                detailsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad otwierania szczegolow: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== CROSS-MODULE KPI ====================

        private void LoadCrossModuleKpi()
        {
            var userMap = new Dictionary<string, CrossModuleKpi>(); // key = UserName

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // 1. Wstawienia - utworzone
                RunModuleQuery(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(w.KtoStwo AS VARCHAR(20)) AS UserID, COUNT(*) AS Cnt
                    FROM dbo.WstawieniaKurczakow w
                    LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                    WHERE w.DataUtw >= @S AND w.DataUtw < @E AND w.KtoStwo IS NOT NULL
                    GROUP BY o.Name, w.KtoStwo",
                    (kpi, cnt) => kpi.WstawieniaUtworzone += cnt);

                // 2. Wstawienia - potwierdzone
                RunModuleQuery(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(w.KtoConf AS VARCHAR(20)) AS UserID, COUNT(*) AS Cnt
                    FROM dbo.WstawieniaKurczakow w
                    LEFT JOIN dbo.operators o ON w.KtoConf = o.ID
                    WHERE w.isConf = 1 AND w.DataConf >= @S AND w.DataConf < @E AND w.KtoConf IS NOT NULL
                    GROUP BY o.Name, w.KtoConf",
                    (kpi, cnt) => kpi.WstawieniaPotwierdzone += cnt);

                // 3. Kalendarz - dostawy utworzone
                RunModuleQuery(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(h.ktoStwo AS VARCHAR(20)) AS UserID, COUNT(*) AS Cnt
                    FROM dbo.HarmonogramDostaw h
                    LEFT JOIN dbo.operators o ON h.ktoStwo = o.ID
                    WHERE h.DataUtw >= @S AND h.DataUtw < @E AND h.ktoStwo IS NOT NULL
                    GROUP BY o.Name, h.ktoStwo",
                    (kpi, cnt) => kpi.KalendarzDostawy += cnt);

                // 4. Kalendarz - potwierdzenia wagi
                RunModuleQuery(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(h.KtoWaga AS VARCHAR(20)) AS UserID, COUNT(*) AS Cnt
                    FROM dbo.HarmonogramDostaw h
                    LEFT JOIN dbo.operators o ON h.KtoWaga = o.ID
                    WHERE h.PotwWaga = 1 AND h.KiedyWaga >= @S AND h.KiedyWaga < @E AND h.KtoWaga IS NOT NULL
                    GROUP BY o.Name, h.KtoWaga",
                    (kpi, cnt) => kpi.KalendarzPotwWagi += cnt);

                // 5. Kalendarz - potwierdzenia sztuk
                RunModuleQuery(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(h.KtoSztuki AS VARCHAR(20)) AS UserID, COUNT(*) AS Cnt
                    FROM dbo.HarmonogramDostaw h
                    LEFT JOIN dbo.operators o ON h.KtoSztuki = o.ID
                    WHERE h.PotwSztuki = 1 AND h.KiedySztuki >= @S AND h.KiedySztuki < @E AND h.KtoSztuki IS NOT NULL
                    GROUP BY o.Name, h.KtoSztuki",
                    (kpi, cnt) => kpi.KalendarzPotwSztuk += cnt);

                // 6. Specyfikacja - wprowadzenia (zatwierdzenia)
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(ZatwierdzonePrzez, 'Nieznany') AS UserName, ISNULL(ZatwierdzoneByUserID, '') AS UserID, COUNT(*) AS Cnt
                    FROM dbo.RozliczeniaZatwierdzenia
                    WHERE Zatwierdzony = 1 AND DataZatwierdzenia >= @S AND DataZatwierdzenia < @E AND ZatwierdzoneByUserID IS NOT NULL
                    GROUP BY ZatwierdzonePrzez, ZatwierdzoneByUserID",
                    (kpi, cnt) => kpi.SpecWprowadzenia += cnt);

                // 7. Specyfikacja - weryfikacje
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(ZweryfikowanePrzez, 'Nieznany') AS UserName, ISNULL(ZweryfikowaneByUserID, '') AS UserID, COUNT(*) AS Cnt
                    FROM dbo.RozliczeniaZatwierdzenia
                    WHERE Zweryfikowany = 1 AND DataWeryfikacji >= @S AND DataWeryfikacji < @E AND ZweryfikowaneByUserID IS NOT NULL
                    GROUP BY ZweryfikowanePrzez, ZweryfikowaneByUserID",
                    (kpi, cnt) => kpi.SpecWeryfikacje += cnt);

                // 8. Hodowcy - telefony
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(UzytkownikNazwa, 'Nieznany') AS UserName, ISNULL(UzytkownikId, '') AS UserID, COUNT(*) AS Cnt
                    FROM dbo.Pozyskiwanie_Aktywnosci
                    WHERE TypAktywnosci = 'Telefon' AND DataUtworzenia >= @S AND DataUtworzenia < @E AND UzytkownikId IS NOT NULL AND UzytkownikId <> 'IMPORT'
                    GROUP BY UzytkownikNazwa, UzytkownikId",
                    (kpi, cnt) => kpi.HodowcyTelefony += cnt);

                // 12. Hodowcy - notatki
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(UzytkownikNazwa, 'Nieznany') AS UserName, ISNULL(UzytkownikId, '') AS UserID, COUNT(*) AS Cnt
                    FROM dbo.Pozyskiwanie_Aktywnosci
                    WHERE TypAktywnosci = 'Notatka' AND DataUtworzenia >= @S AND DataUtworzenia < @E AND UzytkownikId IS NOT NULL AND UzytkownikId <> 'IMPORT'
                    GROUP BY UzytkownikNazwa, UzytkownikId",
                    (kpi, cnt) => kpi.HodowcyNotatki += cnt);

                // 13. Hodowcy - oceny dostawcow
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, oc.OceniajacyUserID AS UserID, COUNT(*) AS Cnt
                    FROM dbo.OcenyDostawcow oc
                    LEFT JOIN dbo.operators o ON TRY_CAST(oc.OceniajacyUserID AS INT) = o.ID
                    WHERE oc.DataUtworzenia >= @S AND oc.DataUtworzenia < @E AND oc.OceniajacyUserID IS NOT NULL
                    GROUP BY o.Name, oc.OceniajacyUserID",
                    (kpi, cnt) => kpi.HodowcyOceny += cnt);

                // 14. Dokumenty - utworzone
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(h.KtoUtw AS VARCHAR(20)) AS UserID, COUNT(*) AS Cnt
                    FROM dbo.HarmonogramDostaw h
                    LEFT JOIN dbo.operators o ON h.KtoUtw = o.ID
                    WHERE h.Utworzone = 1 AND h.KiedyUtw >= @S AND h.KiedyUtw < @E AND h.KtoUtw IS NOT NULL
                    GROUP BY o.Name, h.KtoUtw",
                    (kpi, cnt) => kpi.DokumentyUtworzone += cnt);

                // 15. Dokumenty - wyslane
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(h.KtoWysl AS VARCHAR(20)) AS UserID, COUNT(*) AS Cnt
                    FROM dbo.HarmonogramDostaw h
                    LEFT JOIN dbo.operators o ON h.KtoWysl = o.ID
                    WHERE h.Wysłane = 1 AND h.KiedyWysl >= @S AND h.KiedyWysl < @E AND h.KtoWysl IS NOT NULL
                    GROUP BY o.Name, h.KtoWysl",
                    (kpi, cnt) => kpi.DokumentyWyslane += cnt);

                // 16. Dokumenty - otrzymane
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(h.KtoOtrzym AS VARCHAR(20)) AS UserID, COUNT(*) AS Cnt
                    FROM dbo.HarmonogramDostaw h
                    LEFT JOIN dbo.operators o ON h.KtoOtrzym = o.ID
                    WHERE h.Otrzymane = 1 AND h.KiedyOtrzm >= @S AND h.KiedyOtrzm < @E AND h.KtoOtrzym IS NOT NULL
                    GROUP BY o.Name, h.KtoOtrzym",
                    (kpi, cnt) => kpi.DokumentyOtrzymane += cnt);

                // 17. Wnioski - zlozone
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(o.Name, RequestedBy) AS UserName, RequestedBy AS UserID, COUNT(*) AS Cnt
                    FROM dbo.DostawcyCR cr
                    LEFT JOIN dbo.operators o ON TRY_CAST(cr.RequestedBy AS INT) = o.ID
                    WHERE cr.RequestedAtUTC >= @S AND cr.RequestedAtUTC < @E AND cr.RequestedBy IS NOT NULL
                    GROUP BY o.Name, cr.RequestedBy",
                    (kpi, cnt) => kpi.WnioskiZlozone += cnt);

                // 18. Wnioski - rozpatrzone (decyzje)
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(o.Name, DecyzjaKto) AS UserName, DecyzjaKto AS UserID, COUNT(*) AS Cnt
                    FROM dbo.DostawcyCR cr
                    LEFT JOIN dbo.operators o ON TRY_CAST(cr.DecyzjaKto AS INT) = o.ID
                    WHERE cr.DecyzjaKiedyUTC >= @S AND cr.DecyzjaKiedyUTC < @E AND cr.DecyzjaKto IS NOT NULL
                    GROUP BY o.Name, cr.DecyzjaKto",
                    (kpi, cnt) => kpi.WnioskiRozpatrzone += cnt);
            }

            // Build sorted list
            var list = userMap.Values
                .Where(k => k.SumaAkcji > 0)
                .OrderByDescending(k => k.SumaAkcji)
                .ToList();

            int pos = 1;
            foreach (var kpi in list)
            {
                kpi.Pozycja = pos++;
                kpi.KolorBrush = new SolidColorBrush(GetColorForUser(kpi.Nazwa));
            }

            dgCrossModule.ItemsSource = list;
            dgCrossModuleDetail.ItemsSource = list;

            // Load avatars
            foreach (var kpi in list)
            {
                if (!string.IsNullOrEmpty(kpi.UserID))
                {
                    string userId = kpi.UserID;
                    Task.Run(() =>
                    {
                        var avatarBitmap = UserAvatarManager.GetAvatar(userId);
                        if (avatarBitmap != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                kpi.AvatarSource = ConvertToImageSource(avatarBitmap);
                            });
                        }
                    });
                }
            }

            // Update leader cards
            UpdateCrossModuleLeaders(list);
        }

        private void RunModuleQuery(SqlConnection conn, Dictionary<string, CrossModuleKpi> map,
            string sql, Action<CrossModuleKpi, int> assignAction)
        {
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@S", currentStartDate);
                cmd.Parameters.AddWithValue("@E", currentEndDate);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string name = reader["UserName"]?.ToString() ?? "Nieznany";
                        string userId = reader["UserID"]?.ToString() ?? "";
                        int cnt = Convert.ToInt32(reader["Cnt"]);

                        if (!map.ContainsKey(name))
                            map[name] = new CrossModuleKpi { Nazwa = name, UserID = userId };

                        // Keep the first non-empty UserID
                        if (string.IsNullOrEmpty(map[name].UserID) && !string.IsNullOrEmpty(userId))
                            map[name].UserID = userId;

                        assignAction(map[name], cnt);
                    }
                }
            }
        }

        private void RunModuleQuerySafe(SqlConnection conn, Dictionary<string, CrossModuleKpi> map,
            string sql, Action<CrossModuleKpi, int> assignAction)
        {
            try
            {
                RunModuleQuery(conn, map, sql, assignAction);
            }
            catch
            {
                // Table might not exist yet - silently skip
            }
        }

        private void UpdateCrossModuleLeaders(List<CrossModuleKpi> list)
        {
            if (list.Count == 0)
            {
                txtCmLiderWstawien.Text = "-"; txtCmLiderWstawienVal.Text = "";
                txtCmLiderKalendarz.Text = "-"; txtCmLiderKalendarzVal.Text = "";
                txtCmLiderSpec.Text = "-"; txtCmLiderSpecVal.Text = "";
                txtCmLiderHodowcy.Text = "-"; txtCmLiderHodowcyVal.Text = "";
                txtCmLiderOgolny.Text = "-"; txtCmLiderOgolnyVal.Text = "";
                return;
            }

            // Lider Wstawien
            var top = list.Where(k => k.WstawieniaTotal > 0).OrderByDescending(k => k.WstawieniaTotal).FirstOrDefault();
            txtCmLiderWstawien.Text = top?.Nazwa ?? "-";
            txtCmLiderWstawienVal.Text = top != null ? $"{top.WstawieniaTotal} akcji" : "";

            // Lider Kalendarza
            top = list.Where(k => k.KalendarzTotal > 0).OrderByDescending(k => k.KalendarzTotal).FirstOrDefault();
            txtCmLiderKalendarz.Text = top?.Nazwa ?? "-";
            txtCmLiderKalendarzVal.Text = top != null ? $"{top.KalendarzTotal} akcji" : "";

            // Lider Specyfikacji
            top = list.Where(k => k.SpecyfikacjaTotal > 0).OrderByDescending(k => k.SpecyfikacjaTotal).FirstOrDefault();
            txtCmLiderSpec.Text = top?.Nazwa ?? "-";
            txtCmLiderSpecVal.Text = top != null ? $"{top.SpecyfikacjaTotal} edycji" : "";

            // Lider Hodowcow
            top = list.Where(k => k.HodowcyTotal > 0).OrderByDescending(k => k.HodowcyTotal).FirstOrDefault();
            txtCmLiderHodowcy.Text = top?.Nazwa ?? "-";
            txtCmLiderHodowcyVal.Text = top != null ? $"{top.HodowcyTotal} aktywnosci" : "";

            // Lider Ogolny
            var topAll = list.First();
            txtCmLiderOgolny.Text = topAll.Nazwa;
            txtCmLiderOgolnyVal.Text = $"{topAll.SumaAkcji} akcji lacznie";
        }

        // ==================== DRILL-DOWN ====================

        private void DgCrossModule_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                var dg = sender as DataGrid;
                if (dg == null) return;

                var kpi = dg.SelectedItem as CrossModuleKpi;
                if (kpi == null) return;

                // Determine which column was clicked
                var cell = GetClickedCell(dg, e);
                if (cell == null) return;

                int colIndex = cell.Column.DisplayIndex;

                // Map column index to module name
                // 0=#, 1=Zakupowiec, 2=Wstaw., 3=Kalend., 4=Specyf., 5=Hodowcy, 6=Dok., 7=Wnioski, 8=SUMA
                string moduleName;
                switch (colIndex)
                {
                    case 2: moduleName = "Wstawienia"; break;
                    case 3: moduleName = "Kalendarz"; break;
                    case 4: moduleName = "Specyfikacja"; break;
                    case 5: moduleName = "Hodowcy"; break;
                    case 6: moduleName = "Dokumenty"; break;
                    case 7: moduleName = "Wnioski"; break;
                    case 8: moduleName = "SUMA"; break;
                    default: moduleName = "SUMA"; break;
                }

                var window = new KpiDrillDownWindow(kpi.Nazwa, kpi.UserID, moduleName,
                    currentStartDate, currentEndDate);
                window.Owner = this;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad otwierania szczegolow: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private DataGridCell GetClickedCell(DataGrid dg, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dep = (DependencyObject)e.OriginalSource;
            while (dep != null && !(dep is DataGridCell))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }
            return dep as DataGridCell;
        }
    }
}
