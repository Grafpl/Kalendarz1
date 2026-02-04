using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Kalendarz1.CRM
{
    public partial class PanelManageraWindow : Window
    {
        string connStr;

        public PanelManageraWindow(string c)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            connStr = c;
            dpOd.SelectedDate = DateTime.Today.AddDays(-30);
            dpDo.SelectedDate = DateTime.Today;
            Wczytaj();
        }

        private void Dp_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => Wczytaj();
        private void BtnGeneruj_Click(object sender, RoutedEventArgs e) => Wczytaj();

        void Wczytaj()
        {
            if (dpOd.SelectedDate == null || dpDo.SelectedDate == null) return;

            try
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();

                var dateFrom = dpOd.SelectedDate.Value;
                var dateTo = dpDo.SelectedDate.Value;
                int daysDiff = Math.Max(1, (int)(dateTo - dateFrom).TotalDays + 1);

                // Load team comparison data
                LoadTeamComparison(conn, dateFrom, dateTo, daysDiff);

                // Load KPIs
                LoadKPIs(conn, dateFrom, dateTo, daysDiff);

                // Load funnel data
                LoadFunnel(conn, dateFrom, dateTo);

                // Load day activity
                LoadDayActivity(conn, dateFrom, dateTo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void LoadTeamComparison(SqlConnection conn, DateTime dateFrom, DateTime dateTo, int daysDiff)
        {
            var cmd = new SqlCommand(@"
                SELECT
                    ISNULL(o.Name, h.KtoWykonal) as Handlowiec,
                    COUNT(*) as WszystkieAkcje,
                    SUM(CASE WHEN TypZmiany='Zmiana statusu' THEN 1 ELSE 0 END) as ZmianyStatusow,
                    SUM(CASE WHEN WartoscNowa='Proba kontaktu' THEN 1 ELSE 0 END) as ProbyTel,
                    SUM(CASE WHEN WartoscNowa='Nawiazano kontakt' THEN 1 ELSE 0 END) as Kontakty,
                    SUM(CASE WHEN WartoscNowa LIKE '%oferta%' THEN 1 ELSE 0 END) as WyslaneOferty,
                    SUM(CASE WHEN WartoscNowa LIKE '%Zgoda%' OR WartoscNowa = 'Nawiazano kontakt' THEN 1 ELSE 0 END) as Pozytywne
                FROM HistoriaZmianCRM h
                LEFT JOIN operators o ON o.ID = h.KtoWykonal
                WHERE DataZmiany >= @od AND DataZmiany < DATEADD(day, 1, @do)
                GROUP BY o.Name, h.KtoWykonal
                ORDER BY WszystkieAkcje DESC", conn);

            cmd.Parameters.AddWithValue("@od", dateFrom);
            cmd.Parameters.AddWithValue("@do", dateTo);

            var dt = new DataTable();
            new SqlDataAdapter(cmd).Fill(dt);

            // Create view model list
            var teamData = new List<TeamMemberReport>();
            int position = 1;

            foreach (DataRow row in dt.Rows)
            {
                int akcje = Convert.ToInt32(row["WszystkieAkcje"]);
                int proby = Convert.ToInt32(row["ProbyTel"]);
                int pozytywne = Convert.ToInt32(row["Pozytywne"]);
                double konwersja = proby > 0 ? (double)pozytywne / proby * 100 : 0;

                teamData.Add(new TeamMemberReport
                {
                    Pozycja = position++,
                    Handlowiec = row["Handlowiec"]?.ToString() ?? "Nieznany",
                    WszystkieAkcje = akcje,
                    ProbyTel = proby,
                    Kontakty = Convert.ToInt32(row["Kontakty"]),
                    WyslaneOferty = Convert.ToInt32(row["WyslaneOferty"]),
                    Pozytywne = pozytywne,
                    KonwersjaTekst = $"{konwersja:F1}%",
                    SredniaDzienna = $"{(double)akcje / daysDiff:F1}"
                });
            }

            dgRaport.ItemsSource = teamData;
            txtTeamCount.Text = $" ({teamData.Count} handlowcow)";
        }

        void LoadKPIs(SqlConnection conn, DateTime dateFrom, DateTime dateTo, int daysDiff)
        {
            // Current period
            var cmdCurrent = new SqlCommand(@"
                SELECT
                    COUNT(*) as WszystkieAkcje,
                    SUM(CASE WHEN WartoscNowa='Proba kontaktu' THEN 1 ELSE 0 END) as ProbyTel,
                    SUM(CASE WHEN WartoscNowa LIKE '%Zgoda%' OR WartoscNowa = 'Nawiazano kontakt' THEN 1 ELSE 0 END) as Pozytywne,
                    SUM(CASE WHEN WartoscNowa LIKE '%oferta%' THEN 1 ELSE 0 END) as WyslaneOferty
                FROM HistoriaZmianCRM
                WHERE DataZmiany >= @od AND DataZmiany < DATEADD(day, 1, @do)", conn);

            cmdCurrent.Parameters.AddWithValue("@od", dateFrom);
            cmdCurrent.Parameters.AddWithValue("@do", dateTo);

            using var readerCurrent = cmdCurrent.ExecuteReader();
            int akcje = 0, proby = 0, pozytywne = 0, oferty = 0;

            if (readerCurrent.Read())
            {
                akcje = readerCurrent.IsDBNull(0) ? 0 : readerCurrent.GetInt32(0);
                proby = readerCurrent.IsDBNull(1) ? 0 : readerCurrent.GetInt32(1);
                pozytywne = readerCurrent.IsDBNull(2) ? 0 : readerCurrent.GetInt32(2);
                oferty = readerCurrent.IsDBNull(3) ? 0 : readerCurrent.GetInt32(3);
            }
            readerCurrent.Close();

            // Previous period for comparison
            var prevFrom = dateFrom.AddDays(-daysDiff);
            var prevTo = dateFrom.AddDays(-1);

            var cmdPrev = new SqlCommand(@"
                SELECT
                    COUNT(*) as WszystkieAkcje,
                    SUM(CASE WHEN WartoscNowa='Proba kontaktu' THEN 1 ELSE 0 END) as ProbyTel,
                    SUM(CASE WHEN WartoscNowa LIKE '%Zgoda%' OR WartoscNowa = 'Nawiazano kontakt' THEN 1 ELSE 0 END) as Pozytywne,
                    SUM(CASE WHEN WartoscNowa LIKE '%oferta%' THEN 1 ELSE 0 END) as WyslaneOferty
                FROM HistoriaZmianCRM
                WHERE DataZmiany >= @od AND DataZmiany < DATEADD(day, 1, @do)", conn);

            cmdPrev.Parameters.AddWithValue("@od", prevFrom);
            cmdPrev.Parameters.AddWithValue("@do", prevTo);

            using var readerPrev = cmdPrev.ExecuteReader();
            int prevAkcje = 0, prevProby = 0, prevPozytywne = 0, prevOferty = 0;

            if (readerPrev.Read())
            {
                prevAkcje = readerPrev.IsDBNull(0) ? 0 : readerPrev.GetInt32(0);
                prevProby = readerPrev.IsDBNull(1) ? 0 : readerPrev.GetInt32(1);
                prevPozytywne = readerPrev.IsDBNull(2) ? 0 : readerPrev.GetInt32(2);
                prevOferty = readerPrev.IsDBNull(3) ? 0 : readerPrev.GetInt32(3);
            }
            readerPrev.Close();

            // Update KPI cards
            txtKpiWszystkieAkcje.Text = akcje.ToString();
            txtKpiWszystkieAkcjeTrend.Text = GetTrendText(akcje, prevAkcje);
            txtKpiWszystkieAkcjeTrend.Foreground = GetTrendBrush(akcje, prevAkcje);

            txtKpiProby.Text = proby.ToString();
            txtKpiProbyTrend.Text = GetTrendText(proby, prevProby);
            txtKpiProbyTrend.Foreground = GetTrendBrush(proby, prevProby);

            txtKpiPozytywne.Text = pozytywne.ToString();
            txtKpiPozytywneTrend.Text = GetTrendText(pozytywne, prevPozytywne);
            txtKpiPozytywneTrend.Foreground = GetTrendBrush(pozytywne, prevPozytywne);

            txtKpiOferty.Text = oferty.ToString();
            txtKpiOfertyTrend.Text = GetTrendText(oferty, prevOferty);
            txtKpiOfertyTrend.Foreground = GetTrendBrush(oferty, prevOferty);

            double konwersja = proby > 0 ? (double)pozytywne / proby * 100 : 0;
            txtKpiKonwersja.Text = $"{konwersja:F1}%";

            double srednia = (double)akcje / daysDiff;
            txtKpiSrednia.Text = $"{srednia:F1}";
        }

        string GetTrendText(int current, int previous)
        {
            if (previous == 0) return current > 0 ? "+100% vs poprzedni" : "Brak danych";
            double change = ((double)current - previous) / previous * 100;
            string sign = change >= 0 ? "+" : "";
            return $"{sign}{change:F0}% vs poprzedni";
        }

        Brush GetTrendBrush(int current, int previous)
        {
            if (current > previous) return new SolidColorBrush(Color.FromRgb(34, 197, 94));  // Green
            if (current < previous) return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
            return new SolidColorBrush(Color.FromRgb(100, 116, 139)); // Gray
        }

        void LoadFunnel(SqlConnection conn, DateTime dateFrom, DateTime dateTo)
        {
            var cmd = new SqlCommand(@"
                SELECT Status, COUNT(*) as Cnt
                FROM OdbiorcyCRM
                WHERE Status IS NOT NULL
                GROUP BY Status", conn);

            var funnelData = new Dictionary<string, int>();

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string status = reader["Status"]?.ToString() ?? "";
                int count = Convert.ToInt32(reader["Cnt"]);
                funnelData[status] = count;
            }
            reader.Close();

            int doZadzwonienia = funnelData.GetValueOrDefault("Do zadzwonienia", 0);
            int probaKontaktu = funnelData.GetValueOrDefault("Proba kontaktu", 0);
            int nawiazanoKontakt = funnelData.GetValueOrDefault("Nawiazano kontakt", 0);
            int zgoda = funnelData.GetValueOrDefault("Zgoda na dalszy kontakt", 0);
            int oferta = funnelData.GetValueOrDefault("Do wyslania oferta", 0);
            int sukces = funnelData.GetValueOrDefault("Sukces", 0);

            int maxValue = Math.Max(1, new[] { doZadzwonienia, probaKontaktu, nawiazanoKontakt, zgoda, oferta, sukces }.Max());
            double maxWidth = 180;

            txtFunnelDoZadzwonienia.Text = doZadzwonienia.ToString();
            txtFunnelProbaKontaktu.Text = probaKontaktu.ToString();
            txtFunnelNawiazanoKontakt.Text = nawiazanoKontakt.ToString();
            txtFunnelZgoda.Text = zgoda.ToString();
            txtFunnelOferta.Text = oferta.ToString();
            txtFunnelSukces.Text = sukces.ToString();

            barDoZadzwonienia.Width = Math.Max(10, (double)doZadzwonienia / maxValue * maxWidth);
            barProbaKontaktu.Width = Math.Max(10, (double)probaKontaktu / maxValue * maxWidth);
            barNawiazanoKontakt.Width = Math.Max(10, (double)nawiazanoKontakt / maxValue * maxWidth);
            barZgoda.Width = Math.Max(10, (double)zgoda / maxValue * maxWidth);
            barOferta.Width = Math.Max(10, (double)oferta / maxValue * maxWidth);
            barSukces.Width = Math.Max(10, (double)sukces / maxValue * maxWidth);
        }

        void LoadDayActivity(SqlConnection conn, DateTime dateFrom, DateTime dateTo)
        {
            var cmd = new SqlCommand(@"
                SELECT DATEPART(dw, DataZmiany) as DayOfWeek, COUNT(*) as Cnt
                FROM HistoriaZmianCRM
                WHERE DataZmiany >= @od AND DataZmiany < DATEADD(day, 1, @do)
                GROUP BY DATEPART(dw, DataZmiany)
                ORDER BY DATEPART(dw, DataZmiany)", conn);

            cmd.Parameters.AddWithValue("@od", dateFrom);
            cmd.Parameters.AddWithValue("@do", dateTo);

            var dayData = new Dictionary<int, int>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int day = Convert.ToInt32(reader["DayOfWeek"]);
                int count = Convert.ToInt32(reader["Cnt"]);
                dayData[day] = count;
            }
            reader.Close();

            string[] dayNames = { "Nd", "Pn", "Wt", "Sr", "Cz", "Pt", "Sb" };
            var activities = new List<DayActivityItem>();
            int maxCount = Math.Max(1, dayData.Values.DefaultIfEmpty(1).Max());

            for (int i = 1; i <= 7; i++)
            {
                int count = dayData.GetValueOrDefault(i, 0);
                activities.Add(new DayActivityItem
                {
                    Day = dayNames[i - 1],
                    Count = count,
                    BarHeight = Math.Max(5, (double)count / maxCount * 100),
                    Color = GetDayColor(i, count, maxCount)
                });
            }

            icDayActivity.ItemsSource = activities;
        }

        Brush GetDayColor(int day, int count, int maxCount)
        {
            // Weekend = different color
            if (day == 1 || day == 7)
                return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red for weekend

            double intensity = (double)count / maxCount;
            if (intensity > 0.7)
                return new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green for high
            if (intensity > 0.4)
                return new SolidColorBrush(Color.FromRgb(59, 130, 246)); // Blue for medium
            return new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Yellow for low
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Eksportuj raport",
                    Filter = "CSV|*.csv|Wszystkie pliki|*.*",
                    FileName = $"raport_crm_{DateTime.Now:yyyyMMdd}.csv"
                };

                if (dlg.ShowDialog() == true)
                {
                    var lines = new List<string>();
                    lines.Add("Pozycja;Handlowiec;Akcje;Proby;Kontakty;Oferty;Pozytywne;Konwersja;Sr./dzien");

                    if (dgRaport.ItemsSource is List<TeamMemberReport> data)
                    {
                        foreach (var item in data)
                        {
                            lines.Add($"{item.Pozycja};{item.Handlowiec};{item.WszystkieAkcje};{item.ProbyTel};{item.Kontakty};{item.WyslaneOferty};{item.Pozytywne};{item.KonwersjaTekst};{item.SredniaDzienna}");
                        }
                    }

                    System.IO.File.WriteAllLines(dlg.FileName, lines, System.Text.Encoding.UTF8);
                    MessageBox.Show($"Raport wyeksportowany do:\n{dlg.FileName}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad eksportu: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class TeamMemberReport
    {
        public int Pozycja { get; set; }
        public string Handlowiec { get; set; }
        public int WszystkieAkcje { get; set; }
        public int ProbyTel { get; set; }
        public int Kontakty { get; set; }
        public int WyslaneOferty { get; set; }
        public int Pozytywne { get; set; }
        public string KonwersjaTekst { get; set; }
        public string SredniaDzienna { get; set; }
    }

    public class DayActivityItem
    {
        public string Day { get; set; }
        public int Count { get; set; }
        public double BarHeight { get; set; }
        public Brush Color { get; set; }
    }
}
