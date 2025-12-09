using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Kalendarz1.CRM
{
    public partial class DashboardCRMWindow : Window
    {
        private readonly string connectionString;
        private int okresDni = 0;
        private DateTime dataOd;
        private DateTime dataDo;
        private bool isLoaded = false;

        public DashboardCRMWindow(string connString)
        {
            connectionString = connString;
            InitializeComponent();
            Loaded += (s, e) => { isLoaded = true; UstawOkres(); WczytajDane(); };
            SizeChanged += (s, e) => { if (isLoaded) RysujWykresLiniowy(); };
        }

        private void CmbOkres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded || cmbOkres == null || cmbOkres.SelectedIndex < 0) return;
            UstawOkres();
            WczytajDane();
        }

        private void UstawOkres()
        {
            dataDo = DateTime.Today.AddDays(1);

            switch (cmbOkres.SelectedIndex)
            {
                case 0: dataOd = DateTime.Today; okresDni = 1; break;
                case 1: dataOd = DateTime.Today.AddDays(-6); okresDni = 7; break;
                case 2: dataOd = DateTime.Today.AddDays(-13); okresDni = 14; break;
                case 3: dataOd = DateTime.Today.AddDays(-29); okresDni = 30; break;
                case 4:
                    dataOd = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    okresDni = DateTime.Today.Day;
                    break;
                case 5:
                    var prev = DateTime.Today.AddMonths(-1);
                    dataOd = new DateTime(prev.Year, prev.Month, 1);
                    dataDo = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    okresDni = DateTime.DaysInMonth(prev.Year, prev.Month);
                    break;
            }
        }

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e) => WczytajDane();

        private void WczytajDane()
        {
            try
            {
                WczytajStatystykiGlobalne();
                WczytajRankingHandlowcow();
                WczytajAktywnoscDni();
                RysujWykresLiniowy();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajStatystykiGlobalne()
        {
            if (txtTelefonyTotal == null) return;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Telefony
                var cmdTelefony = new SqlCommand(@"
                    SELECT COUNT(*) FROM HistoriaZmianCRM
                    WHERE TypZmiany = 'Zmiana statusu'
                    AND WartoscNowa IN ('Próba kontaktu', 'Nawiązano kontakt')
                    AND DataZmiany >= @dataOd AND DataZmiany < @dataDo", conn);
                cmdTelefony.Parameters.AddWithValue("@dataOd", dataOd);
                cmdTelefony.Parameters.AddWithValue("@dataDo", dataDo);
                int telefony = (int)cmdTelefony.ExecuteScalar();
                txtTelefonyTotal.Text = telefony.ToString();
                if (txtTelefonyInfo != null)
                    txtTelefonyInfo.Text = okresDni == 1 ? "wykonanych połączeń" : $"śr. {(telefony / (double)okresDni):F1}/dzień";

                // Statusy
                var cmdStatusy = new SqlCommand(@"
                    SELECT COUNT(*) FROM HistoriaZmianCRM
                    WHERE TypZmiany = 'Zmiana statusu'
                    AND WartoscNowa NOT IN ('Próba kontaktu', 'Nawiązano kontakt', 'Do zadzwonienia')
                    AND DataZmiany >= @dataOd AND DataZmiany < @dataDo", conn);
                cmdStatusy.Parameters.AddWithValue("@dataOd", dataOd);
                cmdStatusy.Parameters.AddWithValue("@dataDo", dataDo);
                int statusy = (int)cmdStatusy.ExecuteScalar();
                if (txtStatusyTotal != null) txtStatusyTotal.Text = statusy.ToString();
                if (txtStatusyInfo != null)
                    txtStatusyInfo.Text = okresDni == 1 ? "zmian statusów" : $"śr. {(statusy / (double)okresDni):F1}/dzień";

                // Notatki
                var cmdNotatki = new SqlCommand(@"
                    SELECT COUNT(*) FROM NotatkiCRM
                    WHERE DataUtworzenia >= @dataOd AND DataUtworzenia < @dataDo", conn);
                cmdNotatki.Parameters.AddWithValue("@dataOd", dataOd);
                cmdNotatki.Parameters.AddWithValue("@dataDo", dataDo);
                int notatki = (int)cmdNotatki.ExecuteScalar();
                if (txtNotatkiTotal != null) txtNotatkiTotal.Text = notatki.ToString();
                if (txtNotatkiInfo != null)
                    txtNotatkiInfo.Text = okresDni == 1 ? "dodanych notatek" : $"śr. {(notatki / (double)okresDni):F1}/dzień";

                // Suma
                int suma = telefony + statusy + notatki;
                if (txtAktywnoscTotal != null) txtAktywnoscTotal.Text = suma.ToString();
                if (txtAktywnoscInfo != null)
                    txtAktywnoscInfo.Text = okresDni == 1 ? "wszystkich akcji" : $"śr. {(suma / (double)okresDni):F1}/dzień";
            }
        }

        private List<MiesiacDane> daneMiesieczne = new List<MiesiacDane>();

        private void RysujWykresLiniowy()
        {
            if (canvasWykres == null || canvasWykres.ActualWidth <= 0 || canvasWykres.ActualHeight <= 0) return;

            canvasWykres.Children.Clear();
            panelOsX.Children.Clear();
            panelOsY.Children.Clear();

            // Pobierz dane miesięczne
            daneMiesieczne = PobierzDaneMiesieczne();
            if (daneMiesieczne.Count == 0) return;

            double width = canvasWykres.ActualWidth;
            double height = canvasWykres.ActualHeight;
            double marginLeft = 10;
            double marginRight = 20;
            double marginTop = 20;
            double marginBottom = 10;

            double chartWidth = width - marginLeft - marginRight;
            double chartHeight = height - marginTop - marginBottom;

            int maxValue = Math.Max(1, daneMiesieczne.Max(m => Math.Max(m.Telefony, Math.Max(m.Statusy, m.Notatki))));
            maxValue = (int)(Math.Ceiling(maxValue / 10.0) * 10); // Zaokrąglij do 10

            // Rysuj siatkę i osie
            for (int i = 0; i <= 5; i++)
            {
                double y = marginTop + (chartHeight * i / 5);
                var line = new Line
                {
                    X1 = marginLeft, Y1 = y,
                    X2 = width - marginRight, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    StrokeThickness = 1
                };
                canvasWykres.Children.Add(line);

                // Etykiety osi Y
                int val = maxValue - (maxValue * i / 5);
                var labelY = new TextBlock
                {
                    Text = val.ToString(),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    Margin = new Thickness(0, y - 8, 0, 0)
                };
                panelOsY.Children.Add(labelY);
            }

            // Rysuj linie danych
            var colorTelefony = Color.FromRgb(59, 130, 246);
            var colorStatusy = Color.FromRgb(139, 92, 246);
            var colorNotatki = Color.FromRgb(245, 158, 11);

            RysujLinie(daneMiesieczne.Select(m => m.Telefony).ToList(), colorTelefony, maxValue, chartWidth, chartHeight, marginLeft, marginTop);
            RysujLinie(daneMiesieczne.Select(m => m.Statusy).ToList(), colorStatusy, maxValue, chartWidth, chartHeight, marginLeft, marginTop);
            RysujLinie(daneMiesieczne.Select(m => m.Notatki).ToList(), colorNotatki, maxValue, chartWidth, chartHeight, marginLeft, marginTop);

            // Etykiety osi X
            double stepX = chartWidth / Math.Max(1, daneMiesieczne.Count - 1);
            for (int i = 0; i < daneMiesieczne.Count; i++)
            {
                var labelX = new TextBlock
                {
                    Text = daneMiesieczne[i].Nazwa,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    Width = stepX,
                    TextAlignment = TextAlignment.Center
                };
                panelOsX.Children.Add(labelX);
            }
        }

        private void RysujLinie(List<int> values, Color color, int maxValue, double chartWidth, double chartHeight, double marginLeft, double marginTop)
        {
            if (values.Count < 2) return;

            var points = new PointCollection();
            double stepX = chartWidth / Math.Max(1, values.Count - 1);

            for (int i = 0; i < values.Count; i++)
            {
                double x = marginLeft + (i * stepX);
                double y = marginTop + chartHeight - (values[i] / (double)maxValue * chartHeight);
                points.Add(new Point(x, y));
            }

            // Linia
            var polyline = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 3,
                StrokeLineJoin = PenLineJoin.Round
            };
            canvasWykres.Children.Add(polyline);

            // Punkty
            foreach (var p in points)
            {
                var ellipse = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = new SolidColorBrush(color),
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(ellipse, p.X - 5);
                Canvas.SetTop(ellipse, p.Y - 5);
                canvasWykres.Children.Add(ellipse);
            }

            // Obszar pod linią (gradient)
            var areaPoints = new PointCollection(points);
            areaPoints.Add(new Point(points.Last().X, marginTop + chartHeight));
            areaPoints.Add(new Point(points.First().X, marginTop + chartHeight));

            var polygon = new Polygon
            {
                Points = areaPoints,
                Fill = new LinearGradientBrush(
                    Color.FromArgb(60, color.R, color.G, color.B),
                    Color.FromArgb(10, color.R, color.G, color.B),
                    90)
            };
            canvasWykres.Children.Insert(0, polygon);
        }

        private List<MiesiacDane> PobierzDaneMiesieczne()
        {
            var dane = new List<MiesiacDane>();
            var culture = new CultureInfo("pl-PL");

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Ostatnie 6 miesięcy
                var cmd = new SqlCommand(@"
                    SELECT
                        YEAR(DataZmiany) as Rok, MONTH(DataZmiany) as Miesiac,
                        SUM(CASE WHEN WartoscNowa IN ('Próba kontaktu', 'Nawiązano kontakt') THEN 1 ELSE 0 END) as Telefony,
                        SUM(CASE WHEN WartoscNowa NOT IN ('Próba kontaktu', 'Nawiązano kontakt', 'Do zadzwonienia') THEN 1 ELSE 0 END) as Statusy
                    FROM HistoriaZmianCRM
                    WHERE TypZmiany = 'Zmiana statusu'
                    AND DataZmiany > DATEADD(month, -6, GETDATE())
                    GROUP BY YEAR(DataZmiany), MONTH(DataZmiany)
                    ORDER BY Rok, Miesiac", conn);

                var historiaData = new Dictionary<(int rok, int miesiac), (int tel, int stat)>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int rok = reader.GetInt32(0);
                        int mies = reader.GetInt32(1);
                        historiaData[(rok, mies)] = (reader.GetInt32(2), reader.GetInt32(3));
                    }
                }

                // Notatki
                var cmdNotatki = new SqlCommand(@"
                    SELECT YEAR(DataUtworzenia) as Rok, MONTH(DataUtworzenia) as Miesiac, COUNT(*) as Notatki
                    FROM NotatkiCRM
                    WHERE DataUtworzenia > DATEADD(month, -6, GETDATE())
                    GROUP BY YEAR(DataUtworzenia), MONTH(DataUtworzenia)", conn);

                var notatkiData = new Dictionary<(int rok, int miesiac), int>();
                using (var reader = cmdNotatki.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        notatkiData[(reader.GetInt32(0), reader.GetInt32(1))] = reader.GetInt32(2);
                    }
                }

                // Uzupełnij wszystkie 6 miesięcy
                for (int i = 5; i >= 0; i--)
                {
                    var date = DateTime.Today.AddMonths(-i);
                    int rok = date.Year;
                    int mies = date.Month;

                    int tel = historiaData.ContainsKey((rok, mies)) ? historiaData[(rok, mies)].tel : 0;
                    int stat = historiaData.ContainsKey((rok, mies)) ? historiaData[(rok, mies)].stat : 0;
                    int not = notatkiData.ContainsKey((rok, mies)) ? notatkiData[(rok, mies)] : 0;

                    dane.Add(new MiesiacDane
                    {
                        Nazwa = culture.DateTimeFormat.GetAbbreviatedMonthName(mies),
                        Telefony = tel,
                        Statusy = stat,
                        Notatki = not
                    });
                }
            }

            return dane;
        }

        private void WczytajRankingHandlowcow()
        {
            var handlowcy = new List<HandlowiecRanking>();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Historia
                var cmd = new SqlCommand(@"
                    SELECT
                        ISNULL(o.Name, h.KtoWykonal) as Handlowiec,
                        SUM(CASE WHEN h.WartoscNowa IN ('Próba kontaktu', 'Nawiązano kontakt') THEN 1 ELSE 0 END) as Telefony,
                        SUM(CASE WHEN h.WartoscNowa NOT IN ('Próba kontaktu', 'Nawiązano kontakt', 'Do zadzwonienia') THEN 1 ELSE 0 END) as Statusy
                    FROM HistoriaZmianCRM h
                    LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                    WHERE h.TypZmiany = 'Zmiana statusu'
                    AND h.DataZmiany >= @dataOd AND h.DataZmiany < @dataDo
                    GROUP BY h.KtoWykonal, o.Name", conn);
                cmd.Parameters.AddWithValue("@dataOd", dataOd);
                cmd.Parameters.AddWithValue("@dataDo", dataDo);

                var daneHistoria = new Dictionary<string, (int tel, int stat)>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string nazwa = reader.IsDBNull(0) ? "Nieznany" : reader.GetString(0);
                        daneHistoria[nazwa] = (reader.GetInt32(1), reader.GetInt32(2));
                    }
                }

                // Notatki
                var cmdNotatki = new SqlCommand(@"
                    SELECT ISNULL(o.Name, n.KtoDodal) as Handlowiec, COUNT(*) as Notatki
                    FROM NotatkiCRM n
                    LEFT JOIN operators o ON n.KtoDodal = CAST(o.ID AS NVARCHAR)
                    WHERE n.DataUtworzenia >= @dataOd AND n.DataUtworzenia < @dataDo
                    GROUP BY n.KtoDodal, o.Name", conn);
                cmdNotatki.Parameters.AddWithValue("@dataOd", dataOd);
                cmdNotatki.Parameters.AddWithValue("@dataDo", dataDo);

                var daneNotatki = new Dictionary<string, int>();
                using (var reader = cmdNotatki.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string nazwa = reader.IsDBNull(0) ? "Nieznany" : reader.GetString(0);
                        daneNotatki[nazwa] = reader.GetInt32(1);
                    }
                }

                // Połącz
                var nazwy = daneHistoria.Keys.Union(daneNotatki.Keys).Distinct();
                foreach (var nazwa in nazwy)
                {
                    int tel = daneHistoria.ContainsKey(nazwa) ? daneHistoria[nazwa].tel : 0;
                    int stat = daneHistoria.ContainsKey(nazwa) ? daneHistoria[nazwa].stat : 0;
                    int not = daneNotatki.ContainsKey(nazwa) ? daneNotatki[nazwa] : 0;
                    int suma = tel + stat + not;

                    if (suma > 0)
                    {
                        handlowcy.Add(new HandlowiecRanking
                        {
                            Nazwa = nazwa,
                            Telefony = tel,
                            Statusy = stat,
                            Notatki = not,
                            Suma = suma
                        });
                    }
                }
            }

            // Sortuj
            handlowcy = handlowcy.OrderByDescending(h => h.Suma).ToList();

            // Podium
            if (handlowcy.Count > 0) { txtTop1Nazwa.Text = handlowcy[0].Nazwa; txtTop1Punkty.Text = handlowcy[0].Suma + " pkt"; }
            else { txtTop1Nazwa.Text = "-"; txtTop1Punkty.Text = "0"; }

            if (handlowcy.Count > 1) { txtTop2Nazwa.Text = handlowcy[1].Nazwa; txtTop2Punkty.Text = handlowcy[1].Suma + " pkt"; }
            else { txtTop2Nazwa.Text = "-"; txtTop2Punkty.Text = "0"; }

            if (handlowcy.Count > 2) { txtTop3Nazwa.Text = handlowcy[2].Nazwa; txtTop3Punkty.Text = handlowcy[2].Suma + " pkt"; }
            else { txtTop3Nazwa.Text = "-"; txtTop3Punkty.Text = "0"; }

            // Lista pozostałych (od 4 miejsca)
            var pozostali = handlowcy.Skip(3).ToList();
            var kolory = new[] { "#16A34A", "#22C55E", "#4ADE80", "#86EFAC" };

            for (int i = 0; i < pozostali.Count; i++)
            {
                var h = pozostali[i];
                h.Pozycja = i + 4;
                h.KolorPozycji = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolory[Math.Min(i, kolory.Length - 1)]));
                h.TloKarty = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                h.TelefonyTekst = $"📞 {h.Telefony}";
                h.StatusyTekst = $"🔄 {h.Statusy}";
                h.NotatkiTekst = $"📝 {h.Notatki}";
            }

            listaHandlowcy.ItemsSource = pozostali;
        }

        private void WczytajAktywnoscDni()
        {
            var dni = new List<DzienAktywnosc>();
            int maxSuma = 1;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Historia
                var cmdHistoria = new SqlCommand(@"
                    SELECT CAST(DataZmiany AS DATE) as Dzien,
                        SUM(CASE WHEN WartoscNowa IN ('Próba kontaktu', 'Nawiązano kontakt') THEN 1 ELSE 0 END) as Telefony,
                        SUM(CASE WHEN WartoscNowa NOT IN ('Próba kontaktu', 'Nawiązano kontakt', 'Do zadzwonienia') THEN 1 ELSE 0 END) as Statusy
                    FROM HistoriaZmianCRM
                    WHERE TypZmiany = 'Zmiana statusu'
                    AND DataZmiany >= DATEADD(day, -13, GETDATE()) AND DataZmiany < DATEADD(day, 1, GETDATE())
                    GROUP BY CAST(DataZmiany AS DATE)", conn);

                var daneHistoria = new Dictionary<DateTime, (int tel, int stat)>();
                using (var reader = cmdHistoria.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        daneHistoria[reader.GetDateTime(0)] = (reader.GetInt32(1), reader.GetInt32(2));
                    }
                }

                // Notatki
                var cmdNotatki = new SqlCommand(@"
                    SELECT CAST(DataUtworzenia AS DATE) as Dzien, COUNT(*) as Notatki
                    FROM NotatkiCRM
                    WHERE DataUtworzenia >= DATEADD(day, -13, GETDATE()) AND DataUtworzenia < DATEADD(day, 1, GETDATE())
                    GROUP BY CAST(DataUtworzenia AS DATE)", conn);

                var daneNotatki = new Dictionary<DateTime, int>();
                using (var reader = cmdNotatki.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        daneNotatki[reader.GetDateTime(0)] = reader.GetInt32(1);
                    }
                }

                // Ostatnie 14 dni
                var culture = new CultureInfo("pl-PL");
                for (int i = 13; i >= 0; i--)
                {
                    var dzien = DateTime.Today.AddDays(-i);
                    int tel = daneHistoria.ContainsKey(dzien) ? daneHistoria[dzien].tel : 0;
                    int stat = daneHistoria.ContainsKey(dzien) ? daneHistoria[dzien].stat : 0;
                    int not = daneNotatki.ContainsKey(dzien) ? daneNotatki[dzien] : 0;
                    int suma = tel + stat + not;

                    if (suma > maxSuma) maxSuma = suma;

                    dni.Add(new DzienAktywnosc
                    {
                        DzienSkrot = culture.DateTimeFormat.GetAbbreviatedDayName(dzien.DayOfWeek).ToUpper(),
                        Data = dzien.ToString("dd.MM"),
                        Telefony = tel,
                        Statusy = stat,
                        Notatki = not,
                        Suma = suma,
                        TloKarty = dzien == DateTime.Today
                            ? new SolidColorBrush(Color.FromRgb(220, 252, 231))
                            : new SolidColorBrush(Color.FromRgb(248, 250, 252))
                    });
                }
            }

            // Oblicz wysokości słupków
            foreach (var d in dni)
            {
                d.WysokoscTelefony = maxSuma > 0 ? Math.Max((d.Telefony / (double)maxSuma) * 70, d.Telefony > 0 ? 4 : 0) : 0;
                d.WysokoscStatusy = maxSuma > 0 ? Math.Max((d.Statusy / (double)maxSuma) * 70, d.Statusy > 0 ? 4 : 0) : 0;
                d.WysokoscNotatki = maxSuma > 0 ? Math.Max((d.Notatki / (double)maxSuma) * 70, d.Notatki > 0 ? 4 : 0) : 0;
                d.TooltipTelefony = $"📞 Telefony: {d.Telefony}";
                d.TooltipStatusy = $"🔄 Statusy: {d.Statusy}";
                d.TooltipNotatki = $"📝 Notatki: {d.Notatki}";
            }

            listaAktywnoscDni.ItemsSource = dni;
        }
    }

    // Klasy pomocnicze
    public class MiesiacDane
    {
        public string Nazwa { get; set; }
        public int Telefony { get; set; }
        public int Statusy { get; set; }
        public int Notatki { get; set; }
    }

    public class HandlowiecRanking
    {
        public int Pozycja { get; set; }
        public string Nazwa { get; set; }
        public int Telefony { get; set; }
        public int Statusy { get; set; }
        public int Notatki { get; set; }
        public int Suma { get; set; }
        public string TelefonyTekst { get; set; }
        public string StatusyTekst { get; set; }
        public string NotatkiTekst { get; set; }
        public SolidColorBrush KolorPozycji { get; set; }
        public SolidColorBrush TloKarty { get; set; }
    }

    public class DzienAktywnosc
    {
        public string DzienSkrot { get; set; }
        public string Data { get; set; }
        public int Telefony { get; set; }
        public int Statusy { get; set; }
        public int Notatki { get; set; }
        public int Suma { get; set; }
        public double WysokoscTelefony { get; set; }
        public double WysokoscStatusy { get; set; }
        public double WysokoscNotatki { get; set; }
        public string TooltipTelefony { get; set; }
        public string TooltipStatusy { get; set; }
        public string TooltipNotatki { get; set; }
        public SolidColorBrush TloKarty { get; set; }
    }
}
