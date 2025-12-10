using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private string wybranyHandlowiec = "";

        // Paleta kolorów dla handlowców (biało-czerwono-zielona gama)
        private readonly string[] koloryHandlowcow = new[]
        {
            "#DC2626", "#16A34A", "#B91C1C", "#15803D", "#EF4444", "#22C55E",
            "#F87171", "#4ADE80", "#991B1B", "#166534", "#FCA5A5", "#86EFAC",
            "#7F1D1D", "#14532D", "#FECACA", "#BBF7D0"
        };

        public DashboardCRMWindow(string connString)
        {
            connectionString = connString;
            InitializeComponent();
            InicjalizujKombo();
            Loaded += (s, e) => { isLoaded = true; UstawOkres(); WczytajDane(); };
            SizeChanged += (s, e) => { if (isLoaded) RysujWykres(); };
        }

        private void InicjalizujKombo()
        {
            int currentYear = DateTime.Today.Year;
            for (int y = currentYear; y >= currentYear - 3; y--)
                cmbRok.Items.Add(new ComboBoxItem { Content = y.ToString(), Tag = y });
            cmbRok.SelectedIndex = 0;
            cmbMiesiac.SelectedIndex = DateTime.Today.Month - 1;
            WczytajListeHandlowcow();
        }

        private void WczytajListeHandlowcow()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                        SELECT DISTINCT ISNULL(o.Name, h.KtoWykonal) as Handlowiec
                        FROM HistoriaZmianCRM h
                        LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                        WHERE h.KtoWykonal IS NOT NULL AND h.KtoWykonal != ''
                        ORDER BY Handlowiec", conn);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string nazwa = reader.IsDBNull(0) ? "" : reader.GetString(0);
                            if (!string.IsNullOrWhiteSpace(nazwa))
                                cmbHandlowiec.Items.Add(new ComboBoxItem { Content = nazwa, Tag = nazwa });
                        }
                    }
                }
            }
            catch { }
        }

        private void CmbHandlowiec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded || cmbHandlowiec.SelectedItem == null) return;
            var item = (ComboBoxItem)cmbHandlowiec.SelectedItem;
            wybranyHandlowiec = item.Tag?.ToString() ?? "";
            WczytajDane();
        }

        private void Handlowiec_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag == null) return;
            string nazwa = border.Tag.ToString();

            for (int i = 0; i < cmbHandlowiec.Items.Count; i++)
            {
                var item = cmbHandlowiec.Items[i] as ComboBoxItem;
                if (item?.Tag?.ToString() == nazwa)
                {
                    cmbHandlowiec.SelectedIndex = i;
                    break;
                }
            }
        }

        private void CmbOkres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded || cmbOkres == null || cmbOkres.SelectedIndex < 0) return;
            UstawOkres();
            WczytajDane();
        }

        private void CmbWykresOkres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;
            RysujWykres();
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
            }
        }

        private void WczytajDane()
        {
            try
            {
                WczytajStatystyki();
                WczytajRanking();
                WczytajAktywnoscDni();
                RysujWykres();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajStatystyki()
        {
            if (txtTelefonyTotal == null) return;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string filtrHandlowca = string.IsNullOrEmpty(wybranyHandlowiec) ? "" :
                    " AND (h.KtoWykonal = @handlowiec OR o.Name = @handlowiec)";

                var cmdTel = new SqlCommand($@"
                    SELECT COUNT(*) FROM HistoriaZmianCRM h
                    LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                    WHERE h.TypZmiany = 'Zmiana statusu'
                    AND h.WartoscNowa IN ('Próba kontaktu', 'Nawiązano kontakt')
                    AND h.DataZmiany >= @dataOd AND h.DataZmiany < @dataDo {filtrHandlowca}", conn);
                cmdTel.Parameters.AddWithValue("@dataOd", dataOd);
                cmdTel.Parameters.AddWithValue("@dataDo", dataDo);
                if (!string.IsNullOrEmpty(wybranyHandlowiec))
                    cmdTel.Parameters.AddWithValue("@handlowiec", wybranyHandlowiec);
                int tel = (int)cmdTel.ExecuteScalar();
                txtTelefonyTotal.Text = tel.ToString();
                txtTelefonyInfo.Text = okresDni > 1 ? $"śr. {(tel / (double)okresDni):F1}/dzień" : "połączeń";

                var cmdStat = new SqlCommand($@"
                    SELECT COUNT(*) FROM HistoriaZmianCRM h
                    LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                    WHERE h.TypZmiany = 'Zmiana statusu'
                    AND h.WartoscNowa NOT IN ('Próba kontaktu', 'Nawiązano kontakt', 'Do zadzwonienia')
                    AND h.DataZmiany >= @dataOd AND h.DataZmiany < @dataDo {filtrHandlowca}", conn);
                cmdStat.Parameters.AddWithValue("@dataOd", dataOd);
                cmdStat.Parameters.AddWithValue("@dataDo", dataDo);
                if (!string.IsNullOrEmpty(wybranyHandlowiec))
                    cmdStat.Parameters.AddWithValue("@handlowiec", wybranyHandlowiec);
                int stat = (int)cmdStat.ExecuteScalar();
                txtStatusyTotal.Text = stat.ToString();
                txtStatusyInfo.Text = okresDni > 1 ? $"śr. {(stat / (double)okresDni):F1}/dzień" : "zmian";

                string filtrNotatki = string.IsNullOrEmpty(wybranyHandlowiec) ? "" :
                    " AND (n.KtoDodal = @handlowiec OR o.Name = @handlowiec)";
                var cmdNot = new SqlCommand($@"
                    SELECT COUNT(*) FROM NotatkiCRM n
                    LEFT JOIN operators o ON n.KtoDodal = CAST(o.ID AS NVARCHAR)
                    WHERE n.DataUtworzenia >= @dataOd AND n.DataUtworzenia < @dataDo {filtrNotatki}", conn);
                cmdNot.Parameters.AddWithValue("@dataOd", dataOd);
                cmdNot.Parameters.AddWithValue("@dataDo", dataDo);
                if (!string.IsNullOrEmpty(wybranyHandlowiec))
                    cmdNot.Parameters.AddWithValue("@handlowiec", wybranyHandlowiec);
                int not = (int)cmdNot.ExecuteScalar();
                txtNotatkiTotal.Text = not.ToString();
                txtNotatkiInfo.Text = okresDni > 1 ? $"śr. {(not / (double)okresDni):F1}/dzień" : "dodanych";

                int suma = tel + stat + not;
                txtAktywnoscTotal.Text = suma.ToString();
                txtAktywnoscInfo.Text = okresDni > 1 ? $"śr. {(suma / (double)okresDni):F1}/dzień" : "akcji";
            }
        }

        private void RysujWykres()
        {
            if (canvasWykres == null || canvasWykres.ActualWidth <= 0) return;
            if (cmbMiesiac?.SelectedItem == null || cmbRok?.SelectedItem == null) return;

            canvasWykres.Children.Clear();
            panelOsX.Children.Clear();
            panelOsY.Children.Clear();
            panelLegenda.Children.Clear();

            int miesiac = int.Parse(((ComboBoxItem)cmbMiesiac.SelectedItem).Tag.ToString());
            int rok = int.Parse(((ComboBoxItem)cmbRok.SelectedItem).Tag.ToString());
            int liczbaDni = DateTime.DaysInMonth(rok, miesiac);

            var wykresOd = new DateTime(rok, miesiac, 1);
            var wykresDo = wykresOd.AddMonths(1);

            // Pobierz dane wszystkich handlowców
            var daneHandlowcow = PobierzDaneHandlowcow(wykresOd, wykresDo, liczbaDni);

            if (daneHandlowcow.Count == 0) return;

            double w = canvasWykres.ActualWidth;
            double h = canvasWykres.ActualHeight;
            double ml = 10, mr = 20, mt = 15, mb = 10;
            double cw = w - ml - mr;
            double ch = h - mt - mb;

            // Znajdź max wartość
            int maxVal = 1;
            foreach (var hd in daneHandlowcow.Values)
            {
                int m = hd.Max();
                if (m > maxVal) maxVal = m;
            }
            maxVal = (int)(Math.Ceiling(maxVal / 5.0) * 5);
            if (maxVal == 0) maxVal = 5;

            // Siatka pozioma
            for (int i = 0; i <= 4; i++)
            {
                double y = mt + (ch * i / 4);
                canvasWykres.Children.Add(new Line
                {
                    X1 = ml, Y1 = y, X2 = w - mr, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                    StrokeThickness = 1
                });
                panelOsY.Children.Add(new TextBlock
                {
                    Text = (maxVal - maxVal * i / 4).ToString(),
                    FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                    Margin = new Thickness(0, y - 6, 0, 0)
                });
            }

            // Rysuj linie dla każdego handlowca
            int kolorIndex = 0;
            foreach (var kv in daneHandlowcow.OrderByDescending(x => x.Value.Sum()))
            {
                string nazwa = kv.Key;
                int[] dane = kv.Value;
                string kolorHex = koloryHandlowcow[kolorIndex % koloryHandlowcow.Length];
                Color kolor = (Color)ColorConverter.ConvertFromString(kolorHex);

                RysujLinieHandlowca(dane, kolor, nazwa, maxVal, cw, ch, ml, mt, liczbaDni, rok, miesiac);

                // Dodaj do legendy
                var legendaItem = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 4) };
                legendaItem.Children.Add(new Ellipse { Fill = new SolidColorBrush(kolor), Width = 10, Height = 10, VerticalAlignment = VerticalAlignment.Center });
                legendaItem.Children.Add(new TextBlock
                {
                    Text = nazwa,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                panelLegenda.Children.Add(legendaItem);

                kolorIndex++;
            }

            // Etykiety X - daty
            var culture = new CultureInfo("pl-PL");
            double stepX = cw / liczbaDni;
            int krok = liczbaDni > 20 ? 5 : (liczbaDni > 10 ? 3 : 2);
            for (int d = 1; d <= liczbaDni; d += krok)
            {
                var data = new DateTime(rok, miesiac, d);
                panelOsX.Children.Add(new TextBlock
                {
                    Text = data.ToString("dd.MM"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                    Margin = new Thickness(ml + (d - 0.5) * stepX - 12, 0, 0, 0)
                });
            }
        }

        private void RysujLinieHandlowca(int[] values, Color color, string nazwa, int maxVal, double cw, double ch, double ml, double mt, int dni, int rok, int miesiac)
        {
            var points = new PointCollection();
            double stepX = cw / dni;

            for (int i = 0; i < values.Length; i++)
            {
                double x = ml + (i + 0.5) * stepX;
                double y = mt + ch - (values[i] / (double)maxVal * ch);
                points.Add(new Point(x, y));
            }

            // Linia
            canvasWykres.Children.Add(new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2.5,
                StrokeLineJoin = PenLineJoin.Round
            });

            // Punkty z tooltipem
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] > 0)
                {
                    var data = new DateTime(rok, miesiac, i + 1);
                    var el = new Ellipse
                    {
                        Width = 8, Height = 8,
                        Fill = new SolidColorBrush(color),
                        Stroke = Brushes.White, StrokeThickness = 2,
                        ToolTip = $"{nazwa}\n{data:dd.MM.yyyy}: {values[i]} akcji"
                    };
                    Canvas.SetLeft(el, points[i].X - 4);
                    Canvas.SetTop(el, points[i].Y - 4);
                    canvasWykres.Children.Add(el);
                }
            }

            // Podpis na końcu linii (przy ostatnim niezerowym punkcie)
            int ostatniIndex = -1;
            for (int i = values.Length - 1; i >= 0; i--)
            {
                if (values[i] > 0) { ostatniIndex = i; break; }
            }

            if (ostatniIndex >= 0 && points.Count > ostatniIndex)
            {
                var skrotNazwy = nazwa.Length > 8 ? nazwa.Substring(0, 8) + "…" : nazwa;
                var label = new TextBlock
                {
                    Text = skrotNazwy,
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(color)
                };
                Canvas.SetLeft(label, points[ostatniIndex].X + 6);
                Canvas.SetTop(label, points[ostatniIndex].Y - 6);
                canvasWykres.Children.Add(label);
            }
        }

        private Dictionary<string, int[]> PobierzDaneHandlowcow(DateTime od, DateTime doo, int dni)
        {
            var wynik = new Dictionary<string, int[]>();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Pobierz aktywność wszystkich handlowców dziennie
                var cmd = new SqlCommand(@"
                    SELECT ISNULL(o.Name, h.KtoWykonal) as Handlowiec, DAY(h.DataZmiany) as D, COUNT(*) as Cnt
                    FROM HistoriaZmianCRM h
                    LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                    WHERE h.TypZmiany = 'Zmiana statusu'
                    AND h.DataZmiany >= @od AND h.DataZmiany < @do
                    GROUP BY ISNULL(o.Name, h.KtoWykonal), DAY(h.DataZmiany)", conn);
                cmd.Parameters.AddWithValue("@od", od);
                cmd.Parameters.AddWithValue("@do", doo);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string h = r.IsDBNull(0) ? "?" : r.GetString(0);
                        int d = r.GetInt32(1) - 1;
                        int cnt = r.GetInt32(2);

                        if (!wynik.ContainsKey(h))
                            wynik[h] = new int[dni];

                        if (d >= 0 && d < dni)
                            wynik[h][d] += cnt;
                    }
                }

                // Dodaj notatki
                var cmdN = new SqlCommand(@"
                    SELECT ISNULL(o.Name, n.KtoDodal) as Handlowiec, DAY(n.DataUtworzenia) as D, COUNT(*) as Cnt
                    FROM NotatkiCRM n
                    LEFT JOIN operators o ON n.KtoDodal = CAST(o.ID AS NVARCHAR)
                    WHERE n.DataUtworzenia >= @od AND n.DataUtworzenia < @do
                    GROUP BY ISNULL(o.Name, n.KtoDodal), DAY(n.DataUtworzenia)", conn);
                cmdN.Parameters.AddWithValue("@od", od);
                cmdN.Parameters.AddWithValue("@do", doo);

                using (var r = cmdN.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string h = r.IsDBNull(0) ? "?" : r.GetString(0);
                        int d = r.GetInt32(1) - 1;
                        int cnt = r.GetInt32(2);

                        if (!wynik.ContainsKey(h))
                            wynik[h] = new int[dni];

                        if (d >= 0 && d < dni)
                            wynik[h][d] += cnt;
                    }
                }
            }

            return wynik;
        }

        private void WczytajRanking()
        {
            var lista = new List<HandlowiecRanking>();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT ISNULL(o.Name, h.KtoWykonal) as H,
                        SUM(CASE WHEN h.WartoscNowa IN ('Próba kontaktu', 'Nawiązano kontakt') THEN 1 ELSE 0 END) as T,
                        SUM(CASE WHEN h.WartoscNowa NOT IN ('Próba kontaktu', 'Nawiązano kontakt', 'Do zadzwonienia') THEN 1 ELSE 0 END) as S
                    FROM HistoriaZmianCRM h
                    LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                    WHERE h.TypZmiany = 'Zmiana statusu' AND h.DataZmiany >= @od AND h.DataZmiany < @do
                    GROUP BY h.KtoWykonal, o.Name", conn);
                cmd.Parameters.AddWithValue("@od", dataOd);
                cmd.Parameters.AddWithValue("@do", dataDo);

                var dane = new Dictionary<string, (int t, int s)>();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string n = r.IsDBNull(0) ? "?" : r.GetString(0);
                        dane[n] = (r.GetInt32(1), r.GetInt32(2));
                    }
                }

                var cmdN = new SqlCommand(@"
                    SELECT ISNULL(o.Name, n.KtoDodal), COUNT(*)
                    FROM NotatkiCRM n
                    LEFT JOIN operators o ON n.KtoDodal = CAST(o.ID AS NVARCHAR)
                    WHERE n.DataUtworzenia >= @od AND n.DataUtworzenia < @do
                    GROUP BY n.KtoDodal, o.Name", conn);
                cmdN.Parameters.AddWithValue("@od", dataOd);
                cmdN.Parameters.AddWithValue("@do", dataDo);

                var notatki = new Dictionary<string, int>();
                using (var r = cmdN.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string n = r.IsDBNull(0) ? "?" : r.GetString(0);
                        notatki[n] = r.GetInt32(1);
                    }
                }

                foreach (var n in dane.Keys.Union(notatki.Keys).Distinct())
                {
                    int t = dane.ContainsKey(n) ? dane[n].t : 0;
                    int s = dane.ContainsKey(n) ? dane[n].s : 0;
                    int no = notatki.ContainsKey(n) ? notatki[n] : 0;
                    if (t + s + no > 0)
                        lista.Add(new HandlowiecRanking { Nazwa = n, Telefony = t, Statusy = s, Notatki = no, Suma = t + s + no });
                }
            }

            lista = lista.OrderByDescending(x => x.Suma).ToList();

            for (int i = 0; i < lista.Count; i++)
            {
                var h = lista[i];
                h.Pozycja = i + 1;

                // Kolory pozycji - czerwono-zielone
                if (i == 0) h.KolorPozycji = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")); // złoto/czerwień
                else if (i == 1) h.KolorPozycji = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A")); // srebro/zieleń
                else if (i == 2) h.KolorPozycji = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B91C1C")); // brąz/ciemna czerwień
                else h.KolorPozycji = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));

                h.TloKarty = h.Nazwa == wybranyHandlowiec
                    ? new SolidColorBrush(Color.FromRgb(254, 226, 226)) // czerwone tło
                    : new SolidColorBrush(Color.FromRgb(249, 250, 251));

                h.KolorRamki = h.Nazwa == wybranyHandlowiec
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"))
                    : new SolidColorBrush(Colors.Transparent);

                h.TelefonyTekst = $"T:{h.Telefony}";
                h.StatusyTekst = $"S:{h.Statusy}";
                h.NotatkiTekst = $"N:{h.Notatki}";
            }

            listaHandlowcy.ItemsSource = lista;
        }

        private void WczytajAktywnoscDni()
        {
            var dni = new List<DzienAktywnosc>();
            int maxS = 1;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string filtr = string.IsNullOrEmpty(wybranyHandlowiec) ? "" :
                    " AND (h.KtoWykonal = @handlowiec OR o.Name = @handlowiec)";

                var cmd = new SqlCommand($@"
                    SELECT CAST(h.DataZmiany AS DATE) as D,
                        SUM(CASE WHEN h.WartoscNowa IN ('Próba kontaktu', 'Nawiązano kontakt') THEN 1 ELSE 0 END),
                        SUM(CASE WHEN h.WartoscNowa NOT IN ('Próba kontaktu', 'Nawiązano kontakt', 'Do zadzwonienia') THEN 1 ELSE 0 END)
                    FROM HistoriaZmianCRM h
                    LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                    WHERE h.TypZmiany = 'Zmiana statusu'
                    AND h.DataZmiany >= DATEADD(day, -13, GETDATE()) AND h.DataZmiany < DATEADD(day, 1, GETDATE()) {filtr}
                    GROUP BY CAST(h.DataZmiany AS DATE)", conn);
                if (!string.IsNullOrEmpty(wybranyHandlowiec))
                    cmd.Parameters.AddWithValue("@handlowiec", wybranyHandlowiec);

                var hist = new Dictionary<DateTime, (int t, int s)>();
                using (var r = cmd.ExecuteReader()) { while (r.Read()) hist[r.GetDateTime(0)] = (r.GetInt32(1), r.GetInt32(2)); }

                string filtrN = string.IsNullOrEmpty(wybranyHandlowiec) ? "" :
                    " AND (n.KtoDodal = @handlowiec OR o.Name = @handlowiec)";
                var cmdN = new SqlCommand($@"
                    SELECT CAST(n.DataUtworzenia AS DATE), COUNT(*)
                    FROM NotatkiCRM n
                    LEFT JOIN operators o ON n.KtoDodal = CAST(o.ID AS NVARCHAR)
                    WHERE n.DataUtworzenia >= DATEADD(day, -13, GETDATE()) AND n.DataUtworzenia < DATEADD(day, 1, GETDATE()) {filtrN}
                    GROUP BY CAST(n.DataUtworzenia AS DATE)", conn);
                if (!string.IsNullOrEmpty(wybranyHandlowiec))
                    cmdN.Parameters.AddWithValue("@handlowiec", wybranyHandlowiec);

                var notH = new Dictionary<DateTime, int>();
                using (var r = cmdN.ExecuteReader()) { while (r.Read()) notH[r.GetDateTime(0)] = r.GetInt32(1); }

                var culture = new CultureInfo("pl-PL");
                for (int i = 13; i >= 0; i--)
                {
                    var d = DateTime.Today.AddDays(-i);
                    int t = hist.ContainsKey(d) ? hist[d].t : 0;
                    int s = hist.ContainsKey(d) ? hist[d].s : 0;
                    int n = notH.ContainsKey(d) ? notH[d] : 0;
                    int sum = t + s + n;
                    if (sum > maxS) maxS = sum;

                    dni.Add(new DzienAktywnosc
                    {
                        DzienSkrot = culture.DateTimeFormat.GetAbbreviatedDayName(d.DayOfWeek).Substring(0, 2).ToUpper(),
                        DataSkrot = d.ToString("dd.MM"),
                        Telefony = t, Statusy = s, Notatki = n, Suma = sum,
                        TloKarty = d == DateTime.Today
                            ? new SolidColorBrush(Color.FromRgb(254, 226, 226))
                            : new SolidColorBrush(Color.FromRgb(249, 250, 251)),
                        KolorRamki = d == DateTime.Today
                            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"))
                            : new SolidColorBrush(Color.FromRgb(229, 231, 235))
                    });
                }
            }

            foreach (var d in dni)
            {
                d.WysokoscTelefony = maxS > 0 ? Math.Max((d.Telefony / (double)maxS) * 55, d.Telefony > 0 ? 4 : 0) : 0;
                d.WysokoscStatusy = maxS > 0 ? Math.Max((d.Statusy / (double)maxS) * 55, d.Statusy > 0 ? 4 : 0) : 0;
                d.WysokoscNotatki = maxS > 0 ? Math.Max((d.Notatki / (double)maxS) * 55, d.Notatki > 0 ? 4 : 0) : 0;
                d.TooltipTelefony = $"Telefony: {d.Telefony}";
                d.TooltipStatusy = $"Statusy: {d.Statusy}";
                d.TooltipNotatki = $"Notatki: {d.Notatki}";
            }

            listaAktywnoscDni.ItemsSource = dni;
        }
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
        public SolidColorBrush KolorRamki { get; set; }
    }

    public class DzienAktywnosc
    {
        public string DzienSkrot { get; set; }
        public string DataSkrot { get; set; }
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
        public SolidColorBrush KolorRamki { get; set; }
    }
}
