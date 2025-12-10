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
        private bool widokMiesieczny = false;

        // BARDZO ZRÓŻNICOWANE KOLORY - łatwe do odróżnienia
        private readonly string[] koloryHandlowcow = new[]
        {
            "#E11D48", // różowy
            "#2563EB", // niebieski
            "#16A34A", // zielony
            "#F59E0B", // pomarańczowy
            "#7C3AED", // fioletowy
            "#0891B2", // cyjan
            "#DC2626", // czerwony
            "#4F46E5", // indygo
            "#059669", // szmaragd
            "#D97706", // bursztyn
            "#9333EA", // purpura
            "#0D9488", // teal
            "#BE123C", // róż ciemny
            "#1D4ED8", // niebieski ciemny
            "#15803D", // zielony ciemny
            "#EA580C"  // pomarańcz ciemny
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

        private void CmbWidokWykresu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded || cmbWidokWykresu.SelectedItem == null) return;
            var item = (ComboBoxItem)cmbWidokWykresu.SelectedItem;
            widokMiesieczny = item.Tag?.ToString() == "miesiace";

            // Pokaż/ukryj wybór miesiąca
            panelMiesiac.Visibility = widokMiesieczny ? Visibility.Collapsed : Visibility.Visible;

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
            if (cmbRok?.SelectedItem == null) return;

            canvasWykres.Children.Clear();
            panelOsX.Children.Clear();
            panelOsY.Children.Clear();
            panelLegenda.Children.Clear();

            int rok = int.Parse(((ComboBoxItem)cmbRok.SelectedItem).Tag.ToString());

            if (widokMiesieczny)
                RysujWykresMiesieczny(rok);
            else
                RysujWykresDzienny(rok);
        }

        private void RysujWykresDzienny(int rok)
        {
            if (cmbMiesiac?.SelectedItem == null) return;

            int miesiac = int.Parse(((ComboBoxItem)cmbMiesiac.SelectedItem).Tag.ToString());
            int liczbaDni = DateTime.DaysInMonth(rok, miesiac);

            var wykresOd = new DateTime(rok, miesiac, 1);
            var wykresDo = wykresOd.AddMonths(1);

            var daneHandlowcow = PobierzDaneHandlowcowDzienne(wykresOd, wykresDo, liczbaDni);
            RysujLinie(daneHandlowcow, liczbaDni, rok, miesiac, false);
        }

        private void RysujWykresMiesieczny(int rok)
        {
            var wykresOd = new DateTime(rok, 1, 1);
            var wykresDo = new DateTime(rok + 1, 1, 1);

            var daneHandlowcow = PobierzDaneHandlowcowMiesieczne(wykresOd, wykresDo);
            RysujLinie(daneHandlowcow, 12, rok, 0, true);
        }

        private void RysujLinie(Dictionary<string, int[]> daneHandlowcow, int punkty, int rok, int miesiac, bool czyMiesiace)
        {
            if (daneHandlowcow.Count == 0) return;

            double w = canvasWykres.ActualWidth;
            double h = canvasWykres.ActualHeight;
            double ml = 10, mr = 80, mt = 15, mb = 10;
            double cw = w - ml - mr;
            double ch = h - mt - mb;

            int maxVal = 1;
            foreach (var hd in daneHandlowcow.Values)
            {
                int m = hd.Max();
                if (m > maxVal) maxVal = m;
            }
            maxVal = (int)(Math.Ceiling(maxVal / 5.0) * 5);
            if (maxVal == 0) maxVal = 5;

            // Siatka
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
                    FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Margin = new Thickness(0, y - 6, 0, 0)
                });
            }

            // Rysuj linie
            int kolorIndex = 0;
            double stepX = cw / punkty;

            foreach (var kv in daneHandlowcow.OrderByDescending(x => x.Value.Sum()))
            {
                string nazwa = kv.Key;
                int[] dane = kv.Value;
                string kolorHex = koloryHandlowcow[kolorIndex % koloryHandlowcow.Length];
                Color kolor = (Color)ColorConverter.ConvertFromString(kolorHex);

                var points = new PointCollection();
                for (int i = 0; i < dane.Length; i++)
                {
                    double x = ml + (i + 0.5) * stepX;
                    double y = mt + ch - (dane[i] / (double)maxVal * ch);
                    points.Add(new Point(x, y));
                }

                // Linia
                canvasWykres.Children.Add(new Polyline
                {
                    Points = points,
                    Stroke = new SolidColorBrush(kolor),
                    StrokeThickness = 3,
                    StrokeLineJoin = PenLineJoin.Round
                });

                // Punkty
                for (int i = 0; i < dane.Length; i++)
                {
                    if (dane[i] > 0)
                    {
                        string tooltip;
                        if (czyMiesiace)
                        {
                            var nazwyMies = new[] { "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" };
                            tooltip = $"{nazwa}\n{nazwyMies[i]} {rok}: {dane[i]}";
                        }
                        else
                        {
                            tooltip = $"{nazwa}\n{i + 1:00}.{miesiac:00}.{rok}: {dane[i]}";
                        }

                        var el = new Ellipse
                        {
                            Width = 10, Height = 10,
                            Fill = new SolidColorBrush(kolor),
                            Stroke = Brushes.White, StrokeThickness = 2,
                            ToolTip = tooltip
                        };
                        Canvas.SetLeft(el, points[i].X - 5);
                        Canvas.SetTop(el, points[i].Y - 5);
                        canvasWykres.Children.Add(el);
                    }
                }

                // Podpis na końcu linii
                int ostatniIndex = -1;
                for (int i = dane.Length - 1; i >= 0; i--)
                {
                    if (dane[i] > 0) { ostatniIndex = i; break; }
                }

                if (ostatniIndex >= 0)
                {
                    var label = new TextBlock
                    {
                        Text = nazwa.Length > 12 ? nazwa.Substring(0, 10) + ".." : nazwa,
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(kolor)
                    };
                    Canvas.SetLeft(label, points[ostatniIndex].X + 8);
                    Canvas.SetTop(label, points[ostatniIndex].Y - 7);
                    canvasWykres.Children.Add(label);
                }

                // Legenda
                var legendaItem = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 3) };
                legendaItem.Children.Add(new Rectangle { Fill = new SolidColorBrush(kolor), Width = 14, Height = 4, RadiusX = 2, RadiusY = 2, VerticalAlignment = VerticalAlignment.Center });
                legendaItem.Children.Add(new TextBlock
                {
                    Text = $"{nazwa} ({dane.Sum()})",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                    Margin = new Thickness(5, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                panelLegenda.Children.Add(legendaItem);

                kolorIndex++;
            }

            // Etykiety X
            if (czyMiesiace)
            {
                var nazwyMies = new[] { "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" };
                for (int m = 0; m < 12; m++)
                {
                    panelOsX.Children.Add(new TextBlock
                    {
                        Text = nazwyMies[m],
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                        Margin = new Thickness(ml + (m + 0.5) * stepX - 12, 0, 0, 0)
                    });
                }
            }
            else
            {
                int krok = punkty > 20 ? 5 : (punkty > 10 ? 3 : 2);
                for (int d = 1; d <= punkty; d += krok)
                {
                    panelOsX.Children.Add(new TextBlock
                    {
                        Text = $"{d:00}.{miesiac:00}",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                        Margin = new Thickness(ml + (d - 0.5) * stepX - 14, 0, 0, 0)
                    });
                }
            }
        }

        private Dictionary<string, int[]> PobierzDaneHandlowcowDzienne(DateTime od, DateTime doo, int dni)
        {
            var wynik = new Dictionary<string, int[]>();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                var cmd = new SqlCommand(@"
                    SELECT ISNULL(o.Name, h.KtoWykonal), DAY(h.DataZmiany), COUNT(*)
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

                        if (!wynik.ContainsKey(h)) wynik[h] = new int[dni];
                        if (d >= 0 && d < dni) wynik[h][d] += cnt;
                    }
                }

                var cmdN = new SqlCommand(@"
                    SELECT ISNULL(o.Name, n.KtoDodal), DAY(n.DataUtworzenia), COUNT(*)
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

                        if (!wynik.ContainsKey(h)) wynik[h] = new int[dni];
                        if (d >= 0 && d < dni) wynik[h][d] += cnt;
                    }
                }
            }
            return wynik;
        }

        private Dictionary<string, int[]> PobierzDaneHandlowcowMiesieczne(DateTime od, DateTime doo)
        {
            var wynik = new Dictionary<string, int[]>();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                var cmd = new SqlCommand(@"
                    SELECT ISNULL(o.Name, h.KtoWykonal), MONTH(h.DataZmiany), COUNT(*)
                    FROM HistoriaZmianCRM h
                    LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                    WHERE h.TypZmiany = 'Zmiana statusu'
                    AND h.DataZmiany >= @od AND h.DataZmiany < @do
                    GROUP BY ISNULL(o.Name, h.KtoWykonal), MONTH(h.DataZmiany)", conn);
                cmd.Parameters.AddWithValue("@od", od);
                cmd.Parameters.AddWithValue("@do", doo);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string h = r.IsDBNull(0) ? "?" : r.GetString(0);
                        int m = r.GetInt32(1) - 1;
                        int cnt = r.GetInt32(2);

                        if (!wynik.ContainsKey(h)) wynik[h] = new int[12];
                        if (m >= 0 && m < 12) wynik[h][m] += cnt;
                    }
                }

                var cmdN = new SqlCommand(@"
                    SELECT ISNULL(o.Name, n.KtoDodal), MONTH(n.DataUtworzenia), COUNT(*)
                    FROM NotatkiCRM n
                    LEFT JOIN operators o ON n.KtoDodal = CAST(o.ID AS NVARCHAR)
                    WHERE n.DataUtworzenia >= @od AND n.DataUtworzenia < @do
                    GROUP BY ISNULL(o.Name, n.KtoDodal), MONTH(n.DataUtworzenia)", conn);
                cmdN.Parameters.AddWithValue("@od", od);
                cmdN.Parameters.AddWithValue("@do", doo);

                using (var r = cmdN.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string h = r.IsDBNull(0) ? "?" : r.GetString(0);
                        int m = r.GetInt32(1) - 1;
                        int cnt = r.GetInt32(2);

                        if (!wynik.ContainsKey(h)) wynik[h] = new int[12];
                        if (m >= 0 && m < 12) wynik[h][m] += cnt;
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

            var koloryPoz = new[] { "#E11D48", "#2563EB", "#16A34A", "#64748B" };

            for (int i = 0; i < lista.Count; i++)
            {
                var h = lista[i];
                h.Pozycja = i + 1;
                h.KolorPozycji = new SolidColorBrush((Color)ColorConverter.ConvertFromString(koloryPoz[Math.Min(i, koloryPoz.Length - 1)]));
                h.TloKarty = h.Nazwa == wybranyHandlowiec
                    ? new SolidColorBrush(Color.FromRgb(241, 245, 249))
                    : new SolidColorBrush(Color.FromRgb(250, 250, 250));
                h.KolorRamki = h.Nazwa == wybranyHandlowiec
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"))
                    : new SolidColorBrush(Colors.Transparent);
                h.StatsTekst = $"Tel: {h.Telefony} | Stat: {h.Statusy} | Not: {h.Notatki}";
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
                    SELECT CAST(h.DataZmiany AS DATE),
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
                            ? new SolidColorBrush(Color.FromRgb(241, 245, 249))
                            : new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                        KolorRamki = d == DateTime.Today
                            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"))
                            : new SolidColorBrush(Color.FromRgb(226, 232, 240))
                    });
                }
            }

            foreach (var d in dni)
            {
                d.WysokoscTelefony = maxS > 0 ? Math.Max((d.Telefony / (double)maxS) * 50, d.Telefony > 0 ? 3 : 0) : 0;
                d.WysokoscStatusy = maxS > 0 ? Math.Max((d.Statusy / (double)maxS) * 50, d.Statusy > 0 ? 3 : 0) : 0;
                d.WysokoscNotatki = maxS > 0 ? Math.Max((d.Notatki / (double)maxS) * 50, d.Notatki > 0 ? 3 : 0) : 0;
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
        public string StatsTekst { get; set; }
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
