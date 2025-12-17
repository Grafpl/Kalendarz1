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

        // Kolory - maksymalnie zróżnicowane
        private readonly string[] koloryHandlowcow = new[]
        {
            "#FF6B6B", // czerwony
            "#4ECDC4", // turkusowy
            "#FFE66D", // żółty
            "#95E1D3", // miętowy
            "#F38181", // koralowy
            "#AA96DA", // lawendowy
            "#6C5CE7", // fioletowy intensywny
            "#00B894", // zielony morski
            "#FDCB6E", // pomarańczowy jasny
            "#E84393", // różowy intensywny
            "#00CEC9", // cyjan
            "#0984E3", // niebieski
            "#FF7675", // łososiowy
            "#A29BFE", // fioletowy jasny
            "#55EFC4", // zielony neonowy
            "#FAB1A0", // brzoskwiniowy
            "#74B9FF", // błękitny
            "#FD79A8", // różowy
            "#636E72", // szary
            "#FFEAA7"  // kremowy
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

            // Ustaw zakres od początku roku do obecnego miesiąca
            cmbMiesiacOd.SelectedIndex = 0; // Styczeń
            cmbMiesiacDo.SelectedIndex = DateTime.Today.Month - 1; // Obecny miesiąc

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

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            WczytajDane();
        }

        private void CmbZakres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;
            RysujWykres();
            WypelnijTabele();
        }

        private void RbWidok_Checked(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            RysujWykres();
            WypelnijTabele();
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
                RysujWykres();
                WypelnijTabele();
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
                    " AND o.Name = @handlowiec";

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
                    " AND o.Name = @handlowiec";
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

        private bool TrybTygodniowy => rbTygodnie?.IsChecked == true;

        private void RysujWykres()
        {
            if (canvasWykres == null || canvasWykres.ActualWidth <= 0) return;
            if (cmbRok?.SelectedItem == null || cmbMiesiacOd?.SelectedItem == null || cmbMiesiacDo?.SelectedItem == null) return;

            canvasWykres.Children.Clear();
            panelOsX.Children.Clear();
            panelOsY.Children.Clear();
            panelLegenda.Children.Clear();

            int rok = int.Parse(((ComboBoxItem)cmbRok.SelectedItem).Tag.ToString());
            int miesiacOd = int.Parse(((ComboBoxItem)cmbMiesiacOd.SelectedItem).Tag.ToString());
            int miesiacDo = int.Parse(((ComboBoxItem)cmbMiesiacDo.SelectedItem).Tag.ToString());

            if (miesiacDo < miesiacOd) miesiacDo = miesiacOd;

            var wykresOd = new DateTime(rok, miesiacOd, 1);
            var wykresDo = new DateTime(rok, miesiacDo, 1).AddMonths(1);

            if (TrybTygodniowy)
            {
                var (dane, etykiety) = PobierzDaneHandlowcowTygodniowe(wykresOd, wykresDo);
                RysujLinieUniwersalne(dane, etykiety);
            }
            else
            {
                int liczbaMiesiecy = miesiacDo - miesiacOd + 1;
                var dane = PobierzDaneHandlowcowMiesieczne(wykresOd, wykresDo, miesiacOd, liczbaMiesiecy);
                var nazwyMies = new[] { "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" };
                var etykiety = new List<string>();
                for (int m = 0; m < liczbaMiesiecy; m++)
                    etykiety.Add(nazwyMies[miesiacOd + m - 1]);
                RysujLinieUniwersalne(dane, etykiety);
            }
        }

        private void RysujLinieUniwersalne(Dictionary<string, int[]> daneHandlowcow, List<string> etykietyX)
        {
            // Filtruj handlowców z zerową aktywnością
            var aktywniHandlowcy = daneHandlowcow.Where(x => x.Value.Sum() > 0)
                                                  .ToDictionary(x => x.Key, x => x.Value);

            // Jeśli wybrany handlowiec, pokaż tylko jego
            if (!string.IsNullOrEmpty(wybranyHandlowiec) && aktywniHandlowcy.ContainsKey(wybranyHandlowiec))
            {
                aktywniHandlowcy = new Dictionary<string, int[]> { { wybranyHandlowiec, aktywniHandlowcy[wybranyHandlowiec] } };
            }

            if (aktywniHandlowcy.Count == 0 || etykietyX.Count == 0) return;

            int punkty = etykietyX.Count;
            double w = canvasWykres.ActualWidth;
            double h = canvasWykres.ActualHeight;
            double ml = 45, mr = 15, mt = 20, mb = 15;
            double cw = w - ml - mr;
            double ch = h - mt - mb;

            int maxVal = 1;
            foreach (var hd in aktywniHandlowcy.Values)
            {
                int m = hd.Max();
                if (m > maxVal) maxVal = m;
            }
            // Lepsze zaokrąglanie dla osi Y
            int[] ladneWartosci = { 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000 };
            int docelowy = (int)(maxVal * 1.1);
            maxVal = ladneWartosci.FirstOrDefault(v => v >= docelowy);
            if (maxVal == 0) maxVal = (int)(Math.Ceiling(docelowy / 10.0) * 10);
            if (maxVal < 5) maxVal = 5;

            // Oś Y z wartościami
            int liczbaLiniiY = 5;
            for (int i = 0; i <= liczbaLiniiY; i++)
            {
                double y = mt + (ch * i / liczbaLiniiY);
                int wartosc = maxVal - (maxVal * i / liczbaLiniiY);

                // Linia pozioma
                canvasWykres.Children.Add(new Line
                {
                    X1 = ml, Y1 = y, X2 = w - mr, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(40, 50, 70)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 3 }
                });

                // Etykieta Y
                var lblY = new TextBlock
                {
                    Text = wartosc.ToString(),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 140, 160)),
                    TextAlignment = TextAlignment.Right,
                    Width = 35
                };
                Canvas.SetLeft(lblY, 5);
                Canvas.SetTop(lblY, y - 8);
                canvasWykres.Children.Add(lblY);
            }

            // Pionowe linie siatki i etykiety X
            double stepX = cw / punkty;
            for (int i = 0; i < punkty; i++)
            {
                double x = ml + (i + 0.5) * stepX;

                // Linia pionowa
                canvasWykres.Children.Add(new Line
                {
                    X1 = x, Y1 = mt, X2 = x, Y2 = mt + ch,
                    Stroke = new SolidColorBrush(Color.FromRgb(40, 50, 70)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 3 }
                });

                // Etykieta X
                var lblX = new TextBlock
                {
                    Text = etykietyX[i],
                    FontSize = TrybTygodniowy ? 9 : 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 160, 180))
                };
                Canvas.SetLeft(lblX, x - (TrybTygodniowy ? 18 : 12));
                Canvas.SetTop(lblX, mt + ch + 5);
                canvasWykres.Children.Add(lblX);
            }

            // Rysuj linie handlowców
            int kolorIndex = 0;
            var posortowani = aktywniHandlowcy.OrderByDescending(x => x.Value.Sum()).ToList();

            foreach (var kv in posortowani)
            {
                string nazwa = kv.Key;
                int[] dane = kv.Value;
                string kolorHex = koloryHandlowcow[kolorIndex % koloryHandlowcow.Length];
                Color kolor = (Color)ColorConverter.ConvertFromString(kolorHex);

                var points = new PointCollection();
                for (int i = 0; i < Math.Min(dane.Length, punkty); i++)
                {
                    double x = ml + (i + 0.5) * stepX;
                    double y = mt + ch - (dane[i] / (double)maxVal * ch);
                    points.Add(new Point(x, y));
                }

                if (points.Count == 0) continue;

                // Główna linia (grubsza, klikalna)
                var linia = new Polyline
                {
                    Points = points,
                    Stroke = new SolidColorBrush(kolor),
                    StrokeThickness = aktywniHandlowcy.Count == 1 ? 4 : 3,
                    StrokeLineJoin = PenLineJoin.Round,
                    Cursor = Cursors.Hand,
                    Tag = nazwa,
                    ToolTip = $"Kliknij aby zobaczyć tylko: {nazwa}"
                };
                linia.MouseLeftButtonUp += LiniaWykresu_Click;
                canvasWykres.Children.Add(linia);

                // Punkty (klikalne)
                for (int i = 0; i < Math.Min(dane.Length, punkty); i++)
                {
                    string tooltip = $"{nazwa}\n{etykietyX[i]}: {dane[i]}\n\nKliknij aby filtrować";
                    var el = new Ellipse
                    {
                        Width = aktywniHandlowcy.Count == 1 ? 16 : 12,
                        Height = aktywniHandlowcy.Count == 1 ? 16 : 12,
                        Fill = new SolidColorBrush(kolor),
                        Stroke = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                        StrokeThickness = 2,
                        ToolTip = tooltip,
                        Cursor = Cursors.Hand,
                        Tag = nazwa
                    };
                    el.MouseLeftButtonUp += PunktWykresu_Click;
                    Canvas.SetLeft(el, points[i].X - (aktywniHandlowcy.Count == 1 ? 8 : 6));
                    Canvas.SetTop(el, points[i].Y - (aktywniHandlowcy.Count == 1 ? 8 : 6));
                    canvasWykres.Children.Add(el);

                    // Wartość nad punktem (tylko jeśli jeden handlowiec lub wartość > 0)
                    if (aktywniHandlowcy.Count == 1 || dane[i] > 0)
                    {
                        var lblVal = new TextBlock
                        {
                            Text = dane[i].ToString(),
                            FontSize = 9,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(kolor)
                        };
                        Canvas.SetLeft(lblVal, points[i].X - 8);
                        Canvas.SetTop(lblVal, points[i].Y - 20);
                        canvasWykres.Children.Add(lblVal);
                    }
                }

                // Legenda (klikalna)
                var legendaItem = new Border
                {
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Tag = nazwa,
                    Margin = new Thickness(0, 0, 12, 4),
                    Padding = new Thickness(4, 2, 4, 2),
                    CornerRadius = new CornerRadius(3),
                    ToolTip = $"Kliknij aby zobaczyć tylko: {nazwa}"
                };
                legendaItem.MouseLeftButtonUp += LegendaItem_Click;
                legendaItem.MouseEnter += (s, e) => ((Border)s).Background = new SolidColorBrush(Color.FromRgb(40, 50, 70));
                legendaItem.MouseLeave += (s, e) => ((Border)s).Background = Brushes.Transparent;

                var legendaStack = new StackPanel { Orientation = Orientation.Horizontal };
                legendaStack.Children.Add(new Ellipse
                {
                    Fill = new SolidColorBrush(kolor),
                    Width = 10, Height = 10,
                    VerticalAlignment = VerticalAlignment.Center
                });
                legendaStack.Children.Add(new TextBlock
                {
                    Text = $"{nazwa} ({dane.Sum()})",
                    FontSize = 10,
                    FontWeight = nazwa == wybranyHandlowiec ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 200)),
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                legendaItem.Child = legendaStack;
                panelLegenda.Children.Add(legendaItem);

                kolorIndex++;
            }

            // Przycisk "Pokaż wszystkich" jeśli filtrowany
            if (!string.IsNullOrEmpty(wybranyHandlowiec))
            {
                var btnWszyscy = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(60, 70, 90)),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 0, 12, 4),
                    Padding = new Thickness(8, 4, 8, 4),
                    CornerRadius = new CornerRadius(4)
                };
                btnWszyscy.MouseLeftButtonUp += (s, e) =>
                {
                    cmbHandlowiec.SelectedIndex = 0; // "Wszyscy"
                };
                btnWszyscy.MouseEnter += (s, e) => ((Border)s).Background = new SolidColorBrush(Color.FromRgb(80, 90, 110));
                btnWszyscy.MouseLeave += (s, e) => ((Border)s).Background = new SolidColorBrush(Color.FromRgb(60, 70, 90));
                btnWszyscy.Child = new TextBlock
                {
                    Text = "← Pokaż wszystkich",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 150))
                };
                panelLegenda.Children.Insert(0, btnWszyscy);
            }
        }

        private void LiniaWykresu_Click(object sender, MouseButtonEventArgs e)
        {
            var linia = sender as Polyline;
            if (linia?.Tag == null) return;
            WybierzHandlowcaZNazwy(linia.Tag.ToString());
        }

        private void PunktWykresu_Click(object sender, MouseButtonEventArgs e)
        {
            var punkt = sender as Ellipse;
            if (punkt?.Tag == null) return;
            WybierzHandlowcaZNazwy(punkt.Tag.ToString());
        }

        private void LegendaItem_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag == null) return;
            WybierzHandlowcaZNazwy(border.Tag.ToString());
        }

        private void WybierzHandlowcaZNazwy(string nazwa)
        {
            for (int i = 0; i < cmbHandlowiec.Items.Count; i++)
            {
                var item = cmbHandlowiec.Items[i] as ComboBoxItem;
                if (item?.Tag?.ToString() == nazwa || item?.Content?.ToString() == nazwa)
                {
                    cmbHandlowiec.SelectedIndex = i;
                    return;
                }
            }
        }

        private (Dictionary<string, int[]>, List<string>) PobierzDaneHandlowcowTygodniowe(DateTime od, DateTime doo)
        {
            var wynik = new Dictionary<string, int[]>();
            var etykiety = new List<string>();

            // Oblicz liczbę tygodni
            var start = od;
            while (start.DayOfWeek != DayOfWeek.Monday) start = start.AddDays(-1);
            var koniec = doo;
            while (koniec.DayOfWeek != DayOfWeek.Sunday) koniec = koniec.AddDays(1);

            int liczbaTygodni = (int)Math.Ceiling((koniec - start).TotalDays / 7);
            if (liczbaTygodni <= 0) liczbaTygodni = 1;

            // Generuj etykiety tygodni
            for (int i = 0; i < liczbaTygodni; i++)
            {
                var tydzienStart = start.AddDays(i * 7);
                etykiety.Add($"{tydzienStart:dd.MM}");
            }

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                var cmd = new SqlCommand(@"
                    SELECT ISNULL(o.Name, h.KtoWykonal), CAST(h.DataZmiany AS DATE), COUNT(*)
                    FROM HistoriaZmianCRM h
                    LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                    WHERE h.TypZmiany = 'Zmiana statusu'
                    AND h.DataZmiany >= @od AND h.DataZmiany < @do
                    GROUP BY ISNULL(o.Name, h.KtoWykonal), CAST(h.DataZmiany AS DATE)", conn);
                cmd.Parameters.AddWithValue("@od", od);
                cmd.Parameters.AddWithValue("@do", doo);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string h = r.IsDBNull(0) ? "?" : r.GetString(0);
                        DateTime data = r.GetDateTime(1);
                        int cnt = r.GetInt32(2);

                        int tydzien = (int)((data - start).TotalDays / 7);
                        if (tydzien < 0) tydzien = 0;
                        if (tydzien >= liczbaTygodni) tydzien = liczbaTygodni - 1;

                        if (!wynik.ContainsKey(h)) wynik[h] = new int[liczbaTygodni];
                        wynik[h][tydzien] += cnt;
                    }
                }

                var cmdN = new SqlCommand(@"
                    SELECT ISNULL(o.Name, n.KtoDodal), CAST(n.DataUtworzenia AS DATE), COUNT(*)
                    FROM NotatkiCRM n
                    LEFT JOIN operators o ON n.KtoDodal = CAST(o.ID AS NVARCHAR)
                    WHERE n.DataUtworzenia >= @od AND n.DataUtworzenia < @do
                    GROUP BY ISNULL(o.Name, n.KtoDodal), CAST(n.DataUtworzenia AS DATE)", conn);
                cmdN.Parameters.AddWithValue("@od", od);
                cmdN.Parameters.AddWithValue("@do", doo);

                using (var r = cmdN.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string h = r.IsDBNull(0) ? "?" : r.GetString(0);
                        DateTime data = r.GetDateTime(1);
                        int cnt = r.GetInt32(2);

                        int tydzien = (int)((data - start).TotalDays / 7);
                        if (tydzien < 0) tydzien = 0;
                        if (tydzien >= liczbaTygodni) tydzien = liczbaTygodni - 1;

                        if (!wynik.ContainsKey(h)) wynik[h] = new int[liczbaTygodni];
                        wynik[h][tydzien] += cnt;
                    }
                }
            }
            return (wynik, etykiety);
        }

        private Dictionary<string, int[]> PobierzDaneHandlowcowMiesieczne(DateTime od, DateTime doo, int miesiacStart, int liczbaMiesiecy)
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
                        int m = r.GetInt32(1) - miesiacStart;
                        int cnt = r.GetInt32(2);

                        if (!wynik.ContainsKey(h)) wynik[h] = new int[liczbaMiesiecy];
                        if (m >= 0 && m < liczbaMiesiecy) wynik[h][m] += cnt;
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
                        int m = r.GetInt32(1) - miesiacStart;
                        int cnt = r.GetInt32(2);

                        if (!wynik.ContainsKey(h)) wynik[h] = new int[liczbaMiesiecy];
                        if (m >= 0 && m < liczbaMiesiecy) wynik[h][m] += cnt;
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

            // Kolory pozycji - zielone akcenty
            var koloryPoz = new[] { "#10B981", "#34D399", "#6EE7B7", "#94A3B8" };

            for (int i = 0; i < lista.Count; i++)
            {
                var h = lista[i];
                h.Pozycja = i + 1;
                h.KolorPozycji = new SolidColorBrush((Color)ColorConverter.ConvertFromString(koloryPoz[Math.Min(i, koloryPoz.Length - 1)]));
                h.TloKarty = h.Nazwa == wybranyHandlowiec
                    ? new SolidColorBrush(Color.FromRgb(30, 41, 59))
                    : new SolidColorBrush(Color.FromRgb(15, 23, 42));
                h.KolorRamki = h.Nazwa == wybranyHandlowiec
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))
                    : new SolidColorBrush(Color.FromRgb(51, 65, 85));
                h.StatsTekst = $"T:{h.Telefony} S:{h.Statusy} N:{h.Notatki}";
            }

            listaHandlowcy.ItemsSource = lista;
        }

        private void WypelnijTabele()
        {
            if (tabelaDane == null || cmbRok?.SelectedItem == null || cmbMiesiacOd?.SelectedItem == null || cmbMiesiacDo?.SelectedItem == null) return;

            int rok = int.Parse(((ComboBoxItem)cmbRok.SelectedItem).Tag.ToString());
            int miesiacOd = int.Parse(((ComboBoxItem)cmbMiesiacOd.SelectedItem).Tag.ToString());
            int miesiacDo = int.Parse(((ComboBoxItem)cmbMiesiacDo.SelectedItem).Tag.ToString());
            if (miesiacDo < miesiacOd) miesiacDo = miesiacOd;

            var wykresOd = new DateTime(rok, miesiacOd, 1);
            var wykresDo = new DateTime(rok, miesiacDo, 1).AddMonths(1);

            Dictionary<string, int[]> daneHandlowcow;
            List<string> etykiety;

            if (TrybTygodniowy)
            {
                var result = PobierzDaneHandlowcowTygodniowe(wykresOd, wykresDo);
                daneHandlowcow = result.Item1;
                etykiety = result.Item2;
            }
            else
            {
                int liczbaMiesiecy = miesiacDo - miesiacOd + 1;
                daneHandlowcow = PobierzDaneHandlowcowMiesieczne(wykresOd, wykresDo, miesiacOd, liczbaMiesiecy);
                var nazwyMies = new[] { "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" };
                etykiety = new List<string>();
                for (int m = 0; m < liczbaMiesiecy; m++)
                    etykiety.Add(nazwyMies[miesiacOd + m - 1]);
            }

            // Filtruj handlowców z zerową aktywnością
            var aktywniHandlowcy = daneHandlowcow.Where(x => x.Value.Sum() > 0)
                                                  .ToDictionary(x => x.Key, x => x.Value);

            // Usuń stare kolumny (oprócz pierwszej - Handlowiec)
            while (tabelaDane.Columns.Count > 1)
                tabelaDane.Columns.RemoveAt(1);

            // Dodaj kolumny dla okresów
            for (int m = 0; m < etykiety.Count; m++)
            {
                var col = new DataGridTextColumn
                {
                    Header = etykiety[m],
                    Binding = new System.Windows.Data.Binding($"Okresy[{m}]"),
                    Width = TrybTygodniowy ? 50 : 55
                };
                tabelaDane.Columns.Add(col);
            }

            // Dodaj kolumnę SUMA na końcu
            var colSuma = new DataGridTextColumn
            {
                Header = "SUMA",
                Binding = new System.Windows.Data.Binding("Suma"),
                Width = 60
            };
            colSuma.CellStyle = new Style(typeof(DataGridCell))
            {
                Setters = {
                    new Setter(DataGridCell.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))),
                    new Setter(DataGridCell.FontWeightProperty, FontWeights.Bold)
                }
            };
            tabelaDane.Columns.Add(colSuma);

            // Przygotuj dane
            var listaWierszy = new List<TabelaWiersz>();
            foreach (var kv in aktywniHandlowcy.OrderByDescending(x => x.Value.Sum()))
            {
                var wiersz = new TabelaWiersz
                {
                    Nazwa = kv.Key,
                    Okresy = kv.Value,
                    Suma = kv.Value.Sum()
                };
                listaWierszy.Add(wiersz);
            }

            tabelaDane.ItemsSource = listaWierszy;
        }
    }

    public class TabelaWiersz
    {
        public string Nazwa { get; set; }
        public int[] Okresy { get; set; }
        public int Suma { get; set; }
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
        public string TelefonyTekst => $"T: {Telefony}";
        public string StatusyTekst => $"S: {Statusy}";
        public string NotatkiTekst => $"N: {Notatki}";
        public SolidColorBrush KolorPozycji { get; set; }
        public SolidColorBrush TloKarty { get; set; }
        public SolidColorBrush KolorRamki { get; set; }
    }

}
