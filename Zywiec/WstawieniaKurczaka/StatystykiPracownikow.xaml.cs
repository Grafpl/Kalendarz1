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
        public int KalendarzCeny { get; set; }
        public int KalendarzEdycje { get; set; }
        public int KalendarzNotatki { get; set; }
        public int KalendarzTotal => KalendarzDostawy + KalendarzPotwWagi + KalendarzPotwSztuk + KalendarzCeny + KalendarzEdycje + KalendarzNotatki;

        // Specyfikacja (wprowadzenia + weryfikacje z RozliczeniaZatwierdzenia)
        public int SpecWprowadzenia { get; set; }
        public int SpecWeryfikacje { get; set; }
        public int SpecyfikacjaTotal => SpecWprowadzenia + SpecWeryfikacje;

        // Pozyskiwanie Hodowcow (CRM: Pozyskiwanie_Aktywnosci)
        public int HodowcyZmianyStatusu { get; set; }
        public int HodowcyNotatki { get; set; }
        public int HodowcyTotal => HodowcyZmianyStatusu + HodowcyNotatki;

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

        // Audyt - analiza wzorcow pracy
        public int AudytPusteEdycje { get; set; }
        public int AudytSzybkieSerie { get; set; }
        public int AudytSamoPotwierdzenia { get; set; }
        public int AudytOdwrocenia { get; set; }
        public int AudytPozaGodzinami { get; set; }
        public int AudytPowtorzoneNotatki { get; set; }
        public int AudytTotal => AudytPusteEdycje + AudytSzybkieSerie + AudytSamoPotwierdzenia + AudytOdwrocenia + AudytPozaGodzinami + AudytPowtorzoneNotatki;

        // Efektywnosc - stosunek realnych akcji do edycji
        public double Efektywnosc => SumaAkcji > 0 ? Math.Round((double)(SumaAkcji - KalendarzEdycje) / SumaAkcji * 100, 0) : 0;
        public string EfektywnoscTekst => $"{Efektywnosc:F0}%";

        // ===== CZAS REAKCJI =====
        public double CzasPotwWstawienAvgMin { get; set; }
        public double CzasPotwWagiAvgMin { get; set; }
        public double LeadTimeDni { get; set; }
        public double RozpietoscGodzin { get; set; }

        public string CzasPotwWstawienTekst => CzasPotwWstawienAvgMin > 0 ? $"{CzasPotwWstawienAvgMin:F0} min" : "-";
        public string CzasPotwWagiTekst => CzasPotwWagiAvgMin > 0 ? $"{CzasPotwWagiAvgMin:F0} min" : "-";
        public string LeadTimeTekst => LeadTimeDni > 0 ? $"{LeadTimeDni:F1} dni" : "-";
        public string RozpietoscGodzinTekst => RozpietoscGodzin > 0 ? $"{RozpietoscGodzin:F1} h" : "-";

        public string CzasTooltip => $"Sr. czas potw. wstawien: {CzasPotwWstawienTekst}\nSr. czas potw. wagi: {CzasPotwWagiTekst}\nLead time dostaw: {LeadTimeTekst}\nRozpietosc godzin pracy: {RozpietoscGodzinTekst}";
        public string CzasSummary
        {
            get
            {
                var parts = new List<string>();
                if (CzasPotwWstawienAvgMin > 0) parts.Add($"Potw:{CzasPotwWstawienAvgMin:F0}m");
                if (LeadTimeDni > 0) parts.Add($"Lead:{LeadTimeDni:F1}d");
                if (RozpietoscGodzin > 0) parts.Add($"{RozpietoscGodzin:F1}h");
                return parts.Count > 0 ? string.Join(" ", parts) : "-";
            }
        }

        // ===== JAKOSC =====
        public double WstawieniaPotwProc { get; set; }
        public int UnikatoweDostawcy { get; set; }
        public long WolumenSzt { get; set; }
        public double WolumenKg { get; set; }

        public string WstawieniaPotwProcTekst => WstawieniaPotwProc > 0 ? $"{WstawieniaPotwProc:F0}%" : "-";
        public string UnikatoweDostawcyTekst => UnikatoweDostawcy > 0 ? UnikatoweDostawcy.ToString() : "-";
        public string WolumenSztTekst => WolumenSzt > 0 ? WolumenSzt.ToString("N0") : "-";
        public string WolumenKgTekst => WolumenKg > 0 ? $"{WolumenKg:N0} kg" : "-";

        public string JakoscTooltip => $"% potwierdzen wstawien: {WstawieniaPotwProcTekst}\nUnikatowi dostawcy: {UnikatoweDostawcyTekst}\nWolumen sztuk: {WolumenSztTekst}\nWolumen wagi: {WolumenKgTekst}";
        public string JakoscSummary
        {
            get
            {
                var parts = new List<string>();
                if (WstawieniaPotwProc > 0) parts.Add($"{WstawieniaPotwProc:F0}%");
                if (UnikatoweDostawcy > 0) parts.Add($"{UnikatoweDostawcy}dost.");
                if (WolumenSzt > 0) parts.Add($"{WolumenSzt:N0}szt");
                return parts.Count > 0 ? string.Join(" ", parts) : "-";
            }
        }

        // ===== REGULARNOSC =====
        public int DniAktywne { get; set; }
        public double AktywnoscProc { get; set; }
        public int NajdluzszaSeria { get; set; }
        public double SrAkcjiNaDzien { get; set; }

        public string DniAktywneTekst => DniAktywne > 0 ? DniAktywne.ToString() : "-";
        public string AktywnoscProcTekst => AktywnoscProc > 0 ? $"{AktywnoscProc:F0}%" : "-";
        public string NajdluzszaSeriaTekst => NajdluzszaSeria > 0 ? $"{NajdluzszaSeria} dni" : "-";
        public string SrAkcjiNaDzienTekst => SrAkcjiNaDzien > 0 ? $"{SrAkcjiNaDzien:F1}" : "-";

        public string RegularnoscTooltip => $"Dni aktywne: {DniAktywneTekst}\nAktywnosc: {AktywnoscProcTekst}\nNajdluzsza seria: {NajdluzszaSeriaTekst}\nSr. akcji/dzien: {SrAkcjiNaDzienTekst}";
        public string RegularnoscSummary
        {
            get
            {
                var parts = new List<string>();
                if (DniAktywne > 0) parts.Add($"{DniAktywne}dni");
                if (AktywnoscProc > 0) parts.Add($"{AktywnoscProc:F0}%");
                if (NajdluzszaSeria > 0) parts.Add($"seria:{NajdluzszaSeria}");
                return parts.Count > 0 ? string.Join(" ", parts) : "-";
            }
        }

        // Audyt procent
        public double AudytProc => SumaAkcji > 0 ? Math.Round((double)AudytTotal / SumaAkcji * 100, 1) : 0;
        public string AudytProcTekst => AudytProc > 0 ? $"{AudytProc:F1}%" : "-";

        // Tooltips
        public string WstawieniaTooltip => $"Utworzone: {WstawieniaUtworzone}\nPotwierdzone: {WstawieniaPotwierdzone}\n\nZrodlo: WstawieniaKurczakow\nDwuklik = szczegoly";
        public string KalendarzTooltip => $"Dostawy utworzone: {KalendarzDostawy}\nPotw. wagi: {KalendarzPotwWagi}\nPotw. sztuk: {KalendarzPotwSztuk}\nCeny dodane: {KalendarzCeny}\nEdycje (historia zmian): {KalendarzEdycje}\nNotatki dodane: {KalendarzNotatki}\n\nZrodla: HarmonogramDostaw, AuditLog_Dostawy, Notatki, CenyMinister./Roln./Tuszki\nDwuklik = szczegoly";
        public string SpecyfikacjaTooltip => $"Wprowadzenia: {SpecWprowadzenia}\nWeryfikacje: {SpecWeryfikacje}\n\nZrodlo: RozliczeniaZatwierdzenia\nDwuklik = szczegoly";
        public string HodowcyTooltip => $"Zmiany statusu: {HodowcyZmianyStatusu}\nNotatki: {HodowcyNotatki}\n\nZrodlo: Pozyskiwanie_Aktywnosci\nDwuklik = szczegoly";
        public string DokumentyTooltip => $"Utworzone: {DokumentyUtworzone}\nWyslane: {DokumentyWyslane}\nOtrzymane: {DokumentyOtrzymane}\n\nZrodlo: HarmonogramDostaw (flagi dok.)\nDwuklik = szczegoly";
        public string WnioskiTooltip => $"Zlozone: {WnioskiZlozone}\nRozpatrzone: {WnioskiRozpatrzone}\n\nZrodlo: DostawcyCR\nDwuklik = szczegoly";
        public string AudytTooltip => $"Edycje bez zmian: {AudytPusteEdycje}\nWielorazowe edycje: {AudytSzybkieSerie}\nUtw. i potw. ta sama osoba: {AudytSamoPotwierdzenia}\nCofniete do oryginalu: {AudytOdwrocenia}\nPoza godzinami (6-20): {AudytPozaGodzinami}\nPowtorzone notatki: {AudytPowtorzoneNotatki}\n\nDwuklik = szczegoly";

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

                // 6. Kalendarz - ceny dodane (CenaMinisterialna + CenaRolnicza + CenaTuszki)
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(c.KtoDodal AS VARCHAR(20)) AS UserID, COUNT(*) AS Cnt
                    FROM (
                        SELECT KtoDodal, KiedyDodal FROM dbo.CenaMinisterialna WHERE KtoDodal IS NOT NULL AND KiedyDodal >= @S AND KiedyDodal < @E
                        UNION ALL
                        SELECT KtoDodal, KiedyDodal FROM dbo.CenaRolnicza WHERE KtoDodal IS NOT NULL AND KiedyDodal >= @S AND KiedyDodal < @E
                        UNION ALL
                        SELECT KtoDodal, KiedyDodal FROM dbo.CenaTuszki WHERE KtoDodal IS NOT NULL AND KiedyDodal >= @S AND KiedyDodal < @E
                    ) c
                    LEFT JOIN dbo.operators o ON c.KtoDodal = o.ID
                    GROUP BY o.Name, c.KtoDodal",
                    (kpi, cnt) => kpi.KalendarzCeny += cnt);

                // 7. Kalendarz - edycje z historii zmian (AuditLog_Dostawy)
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(a.UserName, a.UserID) AS UserName, a.UserID, COUNT(*) AS Cnt
                    FROM dbo.AuditLog_Dostawy a
                    WHERE a.TypOperacji = 'UPDATE' AND a.DataZmiany >= @S AND a.DataZmiany < @E AND a.UserID IS NOT NULL
                    GROUP BY a.UserName, a.UserID",
                    (kpi, cnt) => kpi.KalendarzEdycje += cnt);

                // 8. Kalendarz - notatki dodane
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(n.KtoStworzyl AS VARCHAR(20)) AS UserID, COUNT(*) AS Cnt
                    FROM dbo.Notatki n
                    LEFT JOIN dbo.operators o ON n.KtoStworzyl = o.ID
                    WHERE n.DataUtworzenia >= @S AND n.DataUtworzenia < @E AND n.KtoStworzyl IS NOT NULL
                    GROUP BY o.Name, n.KtoStworzyl",
                    (kpi, cnt) => kpi.KalendarzNotatki += cnt);

                // 9. Specyfikacja - wprowadzenia (zatwierdzenia)
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

                // 8. Hodowcy - zmiany statusu
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(UzytkownikNazwa, 'Nieznany') AS UserName, ISNULL(UzytkownikId, '') AS UserID, COUNT(*) AS Cnt
                    FROM dbo.Pozyskiwanie_Aktywnosci
                    WHERE TypAktywnosci = 'Zmiana statusu' AND DataUtworzenia >= @S AND DataUtworzenia < @E AND UzytkownikId IS NOT NULL AND UzytkownikId <> 'IMPORT'
                    GROUP BY UzytkownikNazwa, UzytkownikId",
                    (kpi, cnt) => kpi.HodowcyZmianyStatusu += cnt);

                // 9. Hodowcy - notatki
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(UzytkownikNazwa, 'Nieznany') AS UserName, ISNULL(UzytkownikId, '') AS UserID, COUNT(*) AS Cnt
                    FROM dbo.Pozyskiwanie_Aktywnosci
                    WHERE TypAktywnosci = 'Notatka' AND DataUtworzenia >= @S AND DataUtworzenia < @E AND UzytkownikId IS NOT NULL AND UzytkownikId <> 'IMPORT'
                    GROUP BY UzytkownikNazwa, UzytkownikId",
                    (kpi, cnt) => kpi.HodowcyNotatki += cnt);

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

                // ========== PODEJRZANE AKTYWNOSCI ==========

                // P1. Puste edycje - stara wartosc = nowa wartosc w AuditLog
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(a.UserName, a.UserID) AS UserName, a.UserID, COUNT(*) AS Cnt
                    FROM dbo.AuditLog_Dostawy a
                    WHERE a.TypOperacji = 'UPDATE'
                      AND a.DataZmiany >= @S AND a.DataZmiany < @E
                      AND a.UserID IS NOT NULL
                      AND ISNULL(a.StaraWartosc, '') = ISNULL(a.NowaWartosc, '')
                    GROUP BY a.UserName, a.UserID",
                    (kpi, cnt) => kpi.AudytPusteEdycje += cnt);

                // P2. Szybkie serie - ten sam user, ten sam rekord+pole, >3 edycji w ciagu 2 minut
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT UserName, UserID, SUM(Cnt) AS Cnt FROM (
                        SELECT ISNULL(a.UserName, a.UserID) AS UserName, a.UserID,
                               COUNT(*) - 1 AS Cnt
                        FROM dbo.AuditLog_Dostawy a
                        WHERE a.TypOperacji = 'UPDATE'
                          AND a.DataZmiany >= @S AND a.DataZmiany < @E
                          AND a.UserID IS NOT NULL
                        GROUP BY a.UserName, a.UserID, a.RekordID, a.NazwaPola,
                                 DATEADD(MINUTE, DATEDIFF(MINUTE, 0, a.DataZmiany) / 2 * 2, 0)
                        HAVING COUNT(*) > 3
                    ) sub
                    GROUP BY UserName, UserID",
                    (kpi, cnt) => kpi.AudytSzybkieSerie += cnt);

                // P3. Samo-potwierdzenia wstawien - KtoStwo = KtoConf
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(w.KtoStwo AS VARCHAR(20)) AS UserID, COUNT(*) AS Cnt
                    FROM dbo.WstawieniaKurczakow w
                    LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                    WHERE w.isConf = 1
                      AND w.KtoStwo = w.KtoConf
                      AND w.DataUtw >= @S AND w.DataUtw < @E
                      AND w.KtoStwo IS NOT NULL
                    GROUP BY o.Name, w.KtoStwo",
                    (kpi, cnt) => kpi.AudytSamoPotwierdzenia += cnt);

                // P4. Odwrocone zmiany - A->B potem B->A w ciagu 10 min (ten sam user+rekord+pole)
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT UserName, UserID, COUNT(*) AS Cnt FROM (
                        SELECT ISNULL(a1.UserName, a1.UserID) AS UserName, a1.UserID
                        FROM dbo.AuditLog_Dostawy a1
                        INNER JOIN dbo.AuditLog_Dostawy a2
                            ON a1.UserID = a2.UserID
                            AND a1.RekordID = a2.RekordID
                            AND a1.NazwaPola = a2.NazwaPola
                            AND a2.DataZmiany > a1.DataZmiany
                            AND DATEDIFF(MINUTE, a1.DataZmiany, a2.DataZmiany) <= 10
                            AND ISNULL(a1.StaraWartosc,'') = ISNULL(a2.NowaWartosc,'')
                            AND ISNULL(a1.NowaWartosc,'') = ISNULL(a2.StaraWartosc,'')
                        WHERE a1.TypOperacji = 'UPDATE'
                          AND a1.DataZmiany >= @S AND a1.DataZmiany < @E
                    ) sub
                    GROUP BY UserName, UserID",
                    (kpi, cnt) => kpi.AudytOdwrocenia += cnt);

                // P5. Edycje poza godzinami pracy (przed 6:00 lub po 20:00)
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT ISNULL(a.UserName, a.UserID) AS UserName, a.UserID, COUNT(*) AS Cnt
                    FROM dbo.AuditLog_Dostawy a
                    WHERE a.DataZmiany >= @S AND a.DataZmiany < @E
                      AND a.UserID IS NOT NULL
                      AND (DATEPART(HOUR, a.DataZmiany) < 6 OR DATEPART(HOUR, a.DataZmiany) >= 20)
                    GROUP BY a.UserName, a.UserID",
                    (kpi, cnt) => kpi.AudytPozaGodzinami += cnt);

                // P6. Powtorzone notatki (ta sama tresc tego samego usera w tym samym dniu)
                RunModuleQuerySafe(conn, userMap, @"
                    SELECT UserName, UserID, SUM(Cnt) AS Cnt FROM (
                        SELECT ISNULL(UzytkownikNazwa, 'Nieznany') AS UserName, UzytkownikId AS UserID,
                               COUNT(*) - 1 AS Cnt
                        FROM dbo.Pozyskiwanie_Aktywnosci
                        WHERE TypAktywnosci = 'Notatka'
                          AND DataUtworzenia >= @S AND DataUtworzenia < @E
                          AND UzytkownikId IS NOT NULL AND UzytkownikId <> 'IMPORT'
                        GROUP BY UzytkownikNazwa, UzytkownikId, LEFT(Tresc, 50), CAST(DataUtworzenia AS DATE)
                        HAVING COUNT(*) > 1
                    ) sub
                    GROUP BY UserName, UserID",
                    (kpi, cnt) => kpi.AudytPowtorzoneNotatki += cnt);

                // ========== CZAS REAKCJI ==========

                // A1. Sr. czas potwierdzenia wstawienia (minuty)
                RunModuleQueryDouble(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(w.KtoConf AS VARCHAR(20)) AS UserID,
                           AVG(CAST(DATEDIFF(MINUTE, w.DataUtw, w.DataConf) AS FLOAT)) AS Val
                    FROM dbo.WstawieniaKurczakow w
                    LEFT JOIN dbo.operators o ON w.KtoConf = o.ID
                    WHERE w.isConf = 1 AND w.DataConf >= @S AND w.DataConf < @E
                      AND w.KtoConf IS NOT NULL AND w.DataUtw IS NOT NULL AND w.DataConf > w.DataUtw
                    GROUP BY o.Name, w.KtoConf",
                    (kpi, val) => kpi.CzasPotwWstawienAvgMin = val);

                // A2. Sr. czas potwierdzenia wagi (minuty)
                RunModuleQueryDouble(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(h.KtoWaga AS VARCHAR(20)) AS UserID,
                           AVG(CAST(DATEDIFF(MINUTE, h.DataUtw, h.KiedyWaga) AS FLOAT)) AS Val
                    FROM dbo.HarmonogramDostaw h
                    LEFT JOIN dbo.operators o ON h.KtoWaga = o.ID
                    WHERE h.PotwWaga = 1 AND h.KiedyWaga >= @S AND h.KiedyWaga < @E
                      AND h.KtoWaga IS NOT NULL AND h.DataUtw IS NOT NULL AND h.KiedyWaga > h.DataUtw
                    GROUP BY o.Name, h.KtoWaga",
                    (kpi, val) => kpi.CzasPotwWagiAvgMin = val);

                // A3. Lead time - ile dni wczesniej planuje dostawy
                RunModuleQueryDouble(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(h.ktoStwo AS VARCHAR(20)) AS UserID,
                           AVG(CAST(DATEDIFF(DAY, h.DataUtw, h.DataOdbioru) AS FLOAT)) AS Val
                    FROM dbo.HarmonogramDostaw h
                    LEFT JOIN dbo.operators o ON h.ktoStwo = o.ID
                    WHERE h.DataUtw >= @S AND h.DataUtw < @E
                      AND h.ktoStwo IS NOT NULL AND h.DataOdbioru IS NOT NULL AND h.DataOdbioru >= h.DataUtw
                    GROUP BY o.Name, h.ktoStwo",
                    (kpi, val) => kpi.LeadTimeDni = val);

                // A4. Rozpietosc godzin pracy (sr. roznica max-min godziny z AuditLog per dzien)
                RunModuleQueryDouble(conn, userMap, @"
                    SELECT UserName, UserID, AVG(Rozpietosc) AS Val FROM (
                        SELECT ISNULL(a.UserName, a.UserID) AS UserName, a.UserID,
                               CAST(MAX(DATEPART(HOUR, a.DataZmiany)) - MIN(DATEPART(HOUR, a.DataZmiany)) AS FLOAT) AS Rozpietosc
                        FROM dbo.AuditLog_Dostawy a
                        WHERE a.DataZmiany >= @S AND a.DataZmiany < @E AND a.UserID IS NOT NULL
                        GROUP BY a.UserName, a.UserID, CAST(a.DataZmiany AS DATE)
                        HAVING COUNT(*) >= 2
                    ) sub
                    GROUP BY UserName, UserID",
                    (kpi, val) => kpi.RozpietoscGodzin = val);

                // ========== JAKOSC ==========

                // B1. % wstawien potwierdzonych (per tworca)
                RunModuleQueryDouble(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(w.KtoStwo AS VARCHAR(20)) AS UserID,
                           CASE WHEN COUNT(*) > 0
                                THEN CAST(SUM(CASE WHEN w.isConf = 1 THEN 1 ELSE 0 END) AS FLOAT) / COUNT(*) * 100
                                ELSE 0 END AS Val
                    FROM dbo.WstawieniaKurczakow w
                    LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                    WHERE w.DataUtw >= @S AND w.DataUtw < @E AND w.KtoStwo IS NOT NULL
                    GROUP BY o.Name, w.KtoStwo",
                    (kpi, val) => kpi.WstawieniaPotwProc = val);

                // B2. Unikatowi dostawcy (UNION wstawien + harmonogram)
                RunModuleQueryInt(conn, userMap, @"
                    SELECT UserName, UserID, COUNT(DISTINCT Dostawca) AS Val FROM (
                        SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(w.KtoStwo AS VARCHAR(20)) AS UserID, w.Dostawca
                        FROM dbo.WstawieniaKurczakow w
                        LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                        WHERE w.DataUtw >= @S AND w.DataUtw < @E AND w.KtoStwo IS NOT NULL
                        UNION
                        SELECT ISNULL(o.Name, 'Nieznany'), CAST(h.ktoStwo AS VARCHAR(20)), h.Dostawca
                        FROM dbo.HarmonogramDostaw h
                        LEFT JOIN dbo.operators o ON h.ktoStwo = o.ID
                        WHERE h.DataUtw >= @S AND h.DataUtw < @E AND h.ktoStwo IS NOT NULL
                    ) sub
                    GROUP BY UserName, UserID",
                    (kpi, val) => kpi.UnikatoweDostawcy = val);

                // B3. Wolumen sztuk (suma wstawien + harmonogram)
                RunModuleQueryDouble(conn, userMap, @"
                    SELECT UserName, UserID, SUM(Szt) AS Val FROM (
                        SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(w.KtoStwo AS VARCHAR(20)) AS UserID,
                               CAST(ISNULL(w.IloscWstawienia, 0) AS FLOAT) AS Szt
                        FROM dbo.WstawieniaKurczakow w
                        LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                        WHERE w.DataUtw >= @S AND w.DataUtw < @E AND w.KtoStwo IS NOT NULL
                        UNION ALL
                        SELECT ISNULL(o.Name, 'Nieznany'), CAST(h.ktoStwo AS VARCHAR(20)),
                               CAST(ISNULL(h.SztukiDek, 0) AS FLOAT)
                        FROM dbo.HarmonogramDostaw h
                        LEFT JOIN dbo.operators o ON h.ktoStwo = o.ID
                        WHERE h.DataUtw >= @S AND h.DataUtw < @E AND h.ktoStwo IS NOT NULL
                    ) sub
                    GROUP BY UserName, UserID",
                    (kpi, val) => kpi.WolumenSzt = (long)val);

                // B4. Wolumen wagi (kg z harmonogramu)
                RunModuleQueryDouble(conn, userMap, @"
                    SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(h.ktoStwo AS VARCHAR(20)) AS UserID,
                           SUM(CAST(ISNULL(h.WagaDek, 0) AS FLOAT)) AS Val
                    FROM dbo.HarmonogramDostaw h
                    LEFT JOIN dbo.operators o ON h.ktoStwo = o.ID
                    WHERE h.DataUtw >= @S AND h.DataUtw < @E AND h.ktoStwo IS NOT NULL
                    GROUP BY o.Name, h.ktoStwo",
                    (kpi, val) => kpi.WolumenKg = val);
            }

            // ========== REGULARNOSC (C) - obliczane z AllActions CTE ==========
            LoadConsistencyMetrics(userMap);

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

            // Update alert cards
            txtAlertPuste.Text = list.Sum(k => k.AudytPusteEdycje).ToString();
            txtAlertSerie.Text = list.Sum(k => k.AudytSzybkieSerie).ToString();
            txtAlertSamo.Text = list.Sum(k => k.AudytSamoPotwierdzenia).ToString();
            txtAlertOdwrocone.Text = list.Sum(k => k.AudytOdwrocenia).ToString();
            txtAlertPozaGodz.Text = list.Sum(k => k.AudytPozaGodzinami).ToString();
            txtAlertPowtNotatki.Text = list.Sum(k => k.AudytPowtorzoneNotatki).ToString();
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

        private void RunModuleQueryDouble(SqlConnection conn, Dictionary<string, CrossModuleKpi> map,
            string sql, Action<CrossModuleKpi, double> assignAction)
        {
            try
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
                            double val = reader["Val"] != DBNull.Value ? Convert.ToDouble(reader["Val"]) : 0;

                            if (!map.ContainsKey(name))
                                map[name] = new CrossModuleKpi { Nazwa = name, UserID = userId };

                            if (string.IsNullOrEmpty(map[name].UserID) && !string.IsNullOrEmpty(userId))
                                map[name].UserID = userId;

                            assignAction(map[name], val);
                        }
                    }
                }
            }
            catch { }
        }

        private void RunModuleQueryInt(SqlConnection conn, Dictionary<string, CrossModuleKpi> map,
            string sql, Action<CrossModuleKpi, int> assignInt)
        {
            try
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
                            int val = reader["Val"] != DBNull.Value ? Convert.ToInt32(reader["Val"]) : 0;

                            if (!map.ContainsKey(name))
                                map[name] = new CrossModuleKpi { Nazwa = name, UserID = userId };

                            if (string.IsNullOrEmpty(map[name].UserID) && !string.IsNullOrEmpty(userId))
                                map[name].UserID = userId;

                            assignInt(map[name], val);
                        }
                    }
                }
            }
            catch { }
        }

        private void LoadConsistencyMetrics(Dictionary<string, CrossModuleKpi> userMap)
        {
            try
            {
                // Get all action dates per user via AllActions CTE
                var userDates = new Dictionary<string, List<DateTime>>();

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string sql = @"
                        ;WITH AllActions AS (
                            SELECT ISNULL(o.Name, 'Nieznany') AS UserName, CAST(w.KtoStwo AS VARCHAR(20)) AS UserID, w.DataUtw AS ActionTime
                            FROM dbo.WstawieniaKurczakow w LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                            WHERE w.DataUtw >= @S AND w.DataUtw < @E AND w.KtoStwo IS NOT NULL
                            UNION ALL
                            SELECT ISNULL(o.Name, 'Nieznany'), CAST(w.KtoConf AS VARCHAR(20)), w.DataConf
                            FROM dbo.WstawieniaKurczakow w LEFT JOIN dbo.operators o ON w.KtoConf = o.ID
                            WHERE w.isConf = 1 AND w.DataConf >= @S AND w.DataConf < @E AND w.KtoConf IS NOT NULL
                            UNION ALL
                            SELECT ISNULL(o.Name, 'Nieznany'), CAST(h.ktoStwo AS VARCHAR(20)), h.DataUtw
                            FROM dbo.HarmonogramDostaw h LEFT JOIN dbo.operators o ON h.ktoStwo = o.ID
                            WHERE h.DataUtw >= @S AND h.DataUtw < @E AND h.ktoStwo IS NOT NULL
                            UNION ALL
                            SELECT ISNULL(o.Name, 'Nieznany'), CAST(h.KtoWaga AS VARCHAR(20)), h.KiedyWaga
                            FROM dbo.HarmonogramDostaw h LEFT JOIN dbo.operators o ON h.KtoWaga = o.ID
                            WHERE h.PotwWaga = 1 AND h.KiedyWaga >= @S AND h.KiedyWaga < @E AND h.KtoWaga IS NOT NULL
                            UNION ALL
                            SELECT ISNULL(a.UserName, a.UserID), a.UserID, a.DataZmiany
                            FROM dbo.AuditLog_Dostawy a
                            WHERE a.DataZmiany >= @S AND a.DataZmiany < @E AND a.UserID IS NOT NULL
                        )
                        SELECT UserName, UserID, CAST(ActionTime AS DATE) AS ActionDate
                        FROM AllActions
                        GROUP BY UserName, UserID, CAST(ActionTime AS DATE)
                        ORDER BY UserName, ActionDate";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@S", currentStartDate);
                        cmd.Parameters.AddWithValue("@E", currentEndDate);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string name = reader["UserName"]?.ToString() ?? "Nieznany";
                                DateTime date = Convert.ToDateTime(reader["ActionDate"]);

                                if (!userDates.ContainsKey(name))
                                    userDates[name] = new List<DateTime>();
                                userDates[name].Add(date);
                            }
                        }
                    }
                }

                int dniRobocze = CountWorkingDays(currentStartDate, currentEndDate);

                foreach (var kvp in userDates)
                {
                    if (!userMap.ContainsKey(kvp.Key)) continue;
                    var kpi = userMap[kvp.Key];
                    var dates = kvp.Value.Distinct().OrderBy(d => d).ToList();

                    kpi.DniAktywne = dates.Count;
                    kpi.AktywnoscProc = dniRobocze > 0 ? Math.Round((double)dates.Count / dniRobocze * 100, 1) : 0;
                    kpi.NajdluzszaSeria = ComputeLongestStreak(dates);
                    kpi.SrAkcjiNaDzien = dates.Count > 0 ? Math.Round((double)kpi.SumaAkcji / dates.Count, 1) : 0;
                }
            }
            catch { }
        }

        private int ComputeLongestStreak(List<DateTime> sortedDates)
        {
            if (sortedDates.Count == 0) return 0;
            int maxStreak = 1, currentStreak = 1;
            for (int i = 1; i < sortedDates.Count; i++)
            {
                if ((sortedDates[i] - sortedDates[i - 1]).TotalDays == 1)
                    currentStreak++;
                else
                    currentStreak = 1;

                if (currentStreak > maxStreak) maxStreak = currentStreak;
            }
            return maxStreak;
        }

        private int CountWorkingDays(DateTime start, DateTime end)
        {
            int count = 0;
            for (var d = start; d < end; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                    count++;
            }
            return count > 0 ? count : 1;
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
            txtCmLiderHodowcyVal.Text = top != null ? $"{top.HodowcyZmianyStatusu} zmian + {top.HodowcyNotatki} notatek" : "";

            // Lider Ogolny
            var topAll = list.First();
            txtCmLiderOgolny.Text = topAll.Nazwa;
            txtCmLiderOgolnyVal.Text = $"{topAll.SumaAkcji} akcji lacznie";

            // Najszybszy (najnizszy czas potw. wstawien)
            var fastest = list.Where(k => k.CzasPotwWstawienAvgMin > 0).OrderBy(k => k.CzasPotwWstawienAvgMin).FirstOrDefault();
            txtCmLiderNajszybszy.Text = fastest?.Nazwa ?? "-";
            txtCmLiderNajszybszyVal.Text = fastest != null ? $"Sr. {fastest.CzasPotwWstawienAvgMin:F0} min potw." : "";

            // Najaktywniejszy (najwiecej dni aktywnych)
            var mostActive = list.Where(k => k.DniAktywne > 0).OrderByDescending(k => k.DniAktywne).FirstOrDefault();
            txtCmLiderNajaktywniejszy.Text = mostActive?.Nazwa ?? "-";
            txtCmLiderNajaktywniejszyVal.Text = mostActive != null ? $"{mostActive.DniAktywne} dni ({mostActive.AktywnoscProcTekst})" : "";

            // Najwiekszy wolumen
            var topVolume = list.Where(k => k.WolumenSzt > 0).OrderByDescending(k => k.WolumenSzt).FirstOrDefault();
            txtCmLiderWolumen.Text = topVolume?.Nazwa ?? "-";
            txtCmLiderWolumenVal.Text = topVolume != null ? $"{topVolume.WolumenSzt:N0} szt + {topVolume.WolumenKg:N0} kg" : "";

            // Najlepsza jakosc (% potwierdzen)
            var topQuality = list.Where(k => k.WstawieniaPotwProc > 0 && k.WstawieniaUtworzone >= 3).OrderByDescending(k => k.WstawieniaPotwProc).FirstOrDefault();
            txtCmLiderJakosc.Text = topQuality?.Nazwa ?? "-";
            txtCmLiderJakoscVal.Text = topQuality != null ? $"{topQuality.WstawieniaPotwProc:F0}% potwierdzen" : "";

            // Najdluzsza seria
            var topStreak = list.Where(k => k.NajdluzszaSeria > 0).OrderByDescending(k => k.NajdluzszaSeria).FirstOrDefault();
            txtCmLiderSeria.Text = topStreak?.Nazwa ?? "-";
            txtCmLiderSeriaVal.Text = topStreak != null ? $"{topStreak.NajdluzszaSeria} dni z rzedu" : "";
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
                // 0=#, 1=Zakupowiec, 2=Wstawienia, 3=Potwierdzenia, 4=Kalend., 5=Specyf., 6=Hodowcy, 7=Dok., 8=Wnioski, 9=SUMA, 10=Efektywn., 11=Audyt
                string moduleName;
                switch (colIndex)
                {
                    case 2: moduleName = "Wstawienia"; break;
                    case 3: moduleName = "Wstawienia"; break;
                    case 4: moduleName = "Kalendarz"; break;
                    case 5: moduleName = "Specyfikacja"; break;
                    case 6: moduleName = "Hodowcy"; break;
                    case 7: moduleName = "Dokumenty"; break;
                    case 8: moduleName = "Wnioski"; break;
                    case 9: moduleName = "SUMA"; break;
                    case 10: moduleName = "Efektywnosc"; break;
                    case 11: moduleName = "Audyt"; break;
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
