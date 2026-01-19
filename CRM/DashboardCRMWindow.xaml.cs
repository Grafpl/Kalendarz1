using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
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
        private bool isLoaded = false;
        private double maxSzerokoscSlupka = 800;
        private Dictionary<string, DaneHandlowca> aktualneData = new Dictionary<string, DaneHandlowca>();
        private List<string> aktualneEtykiety = new List<string>();
        private DateTime aktualnaDataOd;
        private DateTime aktualnaDataDo;

        private readonly string[] kolory = new[]
        {
            "#10B981", "#3B82F6", "#F59E0B", "#EF4444", "#8B5CF6",
            "#EC4899", "#06B6D4", "#84CC16", "#F97316", "#6366F1",
            "#14B8A6", "#A855F7", "#22C55E", "#0EA5E9", "#FBBF24"
        };

        public DashboardCRMWindow(string connString)
        {
            connectionString = connString;
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            InicjalizujKombo();
            Loaded += (s, e) => { isLoaded = true; WczytajDane(); };
            SizeChanged += (s, e) => { if (isLoaded) AktualizujSzerokoscSlupkow(); };
        }

        private void InicjalizujKombo()
        {
            int currentYear = DateTime.Today.Year;
            for (int y = currentYear; y >= currentYear - 3; y--)
                cmbRok.Items.Add(new ComboBoxItem { Content = y.ToString(), Tag = y });
            cmbRok.SelectedIndex = 0;

            cmbMiesiacOd.SelectedIndex = 0;
            cmbMiesiacDo.SelectedIndex = DateTime.Today.Month - 1;
        }

        private bool TrybTygodniowy => rbTygodnie?.IsChecked == true;
        private string TypDanych => rbTelefony?.IsChecked == true ? "telefony" : (rbNotatki?.IsChecked == true ? "notatki" : "wszystko");

        private void CmbZakres_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (isLoaded) WczytajDane(); }
        private void RbTypDanych_Changed(object sender, RoutedEventArgs e) { if (isLoaded) WczytajDane(); }
        private void RbWidok_Changed(object sender, RoutedEventArgs e) { if (isLoaded) WczytajDane(); }
        private void BtnOdswiez_Click(object sender, RoutedEventArgs e) { WczytajDane(); }

        private void WczytajDane()
        {
            try
            {
                if (cmbRok?.SelectedItem == null || cmbMiesiacOd?.SelectedItem == null || cmbMiesiacDo?.SelectedItem == null)
                    return;

                int rok = int.Parse(((ComboBoxItem)cmbRok.SelectedItem).Tag.ToString());
                int miesiacOd = int.Parse(((ComboBoxItem)cmbMiesiacOd.SelectedItem).Tag.ToString());
                int miesiacDo = int.Parse(((ComboBoxItem)cmbMiesiacDo.SelectedItem).Tag.ToString());
                if (miesiacDo < miesiacOd) miesiacDo = miesiacOd;

                aktualnaDataOd = new DateTime(rok, miesiacOd, 1);
                aktualnaDataDo = new DateTime(rok, miesiacDo, 1).AddMonths(1);

                if (TrybTygodniowy)
                {
                    var (dane, etykiety) = PobierzDaneTygodniowe(aktualnaDataOd, aktualnaDataDo);
                    aktualneData = dane;
                    aktualneEtykiety = etykiety;
                    txtTabelaTytul.Text = "SZCZEGOLOWE DANE TYGODNIOWE";
                }
                else
                {
                    var (dane, etykiety) = PobierzDaneMiesieczne(aktualnaDataOd, aktualnaDataDo, miesiacOd, miesiacDo);
                    aktualneData = dane;
                    aktualneEtykiety = etykiety;
                    txtTabelaTytul.Text = "SZCZEGOLOWE DANE MIESIECZNE";
                }

                WypelnijWykresSlupkowy();
                WypelnijTabele();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (Dictionary<string, DaneHandlowca>, List<string>) PobierzDaneMiesieczne(DateTime od, DateTime doo, int miesiacOd, int miesiacDo)
        {
            var wynik = new Dictionary<string, DaneHandlowca>();
            int liczbaMiesiecy = miesiacDo - miesiacOd + 1;
            var nazwyMies = new[] { "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paz", "Lis", "Gru" };
            var etykiety = new List<string>();
            for (int m = miesiacOd - 1; m < miesiacDo; m++)
                etykiety.Add(nazwyMies[m]);

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string typ = TypDanych;

                // Telefony (zmiany statusow)
                if (typ == "wszystko" || typ == "telefony")
                {
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
                            string nazwa = r.IsDBNull(0) ? "?" : r.GetString(0);
                            int miesiac = r.GetInt32(1);
                            int cnt = r.GetInt32(2);
                            int idx = miesiac - miesiacOd;

                            if (!wynik.ContainsKey(nazwa))
                                wynik[nazwa] = new DaneHandlowca { Nazwa = nazwa, Dane = new int[liczbaMiesiecy] };

                            if (idx >= 0 && idx < liczbaMiesiecy)
                                wynik[nazwa].Dane[idx] += cnt;
                        }
                    }
                }

                // Notatki
                if (typ == "wszystko" || typ == "notatki")
                {
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
                            string nazwa = r.IsDBNull(0) ? "?" : r.GetString(0);
                            int miesiac = r.GetInt32(1);
                            int cnt = r.GetInt32(2);
                            int idx = miesiac - miesiacOd;

                            if (!wynik.ContainsKey(nazwa))
                                wynik[nazwa] = new DaneHandlowca { Nazwa = nazwa, Dane = new int[liczbaMiesiecy] };

                            if (idx >= 0 && idx < liczbaMiesiecy)
                                wynik[nazwa].Dane[idx] += cnt;
                        }
                    }
                }
            }

            foreach (var h in wynik.Values)
                h.Suma = h.Dane.Sum();

            return (wynik, etykiety);
        }

        private (Dictionary<string, DaneHandlowca>, List<string>) PobierzDaneTygodniowe(DateTime od, DateTime doo)
        {
            var wynik = new Dictionary<string, DaneHandlowca>();
            var etykiety = new List<string>();

            // Znajdz poniedzialek przed data "od"
            var start = od;
            while (start.DayOfWeek != DayOfWeek.Monday) start = start.AddDays(-1);

            int liczbaTygodni = (int)Math.Ceiling((doo - start).TotalDays / 7);
            if (liczbaTygodni <= 0) liczbaTygodni = 1;

            for (int i = 0; i < liczbaTygodni; i++)
            {
                var tydzienStart = start.AddDays(i * 7);
                etykiety.Add($"{tydzienStart:dd.MM}");
            }

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string typ = TypDanych;

                // Telefony
                if (typ == "wszystko" || typ == "telefony")
                {
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
                            string nazwa = r.IsDBNull(0) ? "?" : r.GetString(0);
                            DateTime data = r.GetDateTime(1);
                            int cnt = r.GetInt32(2);
                            int tydzien = (int)((data - start).TotalDays / 7);
                            if (tydzien < 0) tydzien = 0;
                            if (tydzien >= liczbaTygodni) tydzien = liczbaTygodni - 1;

                            if (!wynik.ContainsKey(nazwa))
                                wynik[nazwa] = new DaneHandlowca { Nazwa = nazwa, Dane = new int[liczbaTygodni] };

                            wynik[nazwa].Dane[tydzien] += cnt;
                        }
                    }
                }

                // Notatki
                if (typ == "wszystko" || typ == "notatki")
                {
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
                            string nazwa = r.IsDBNull(0) ? "?" : r.GetString(0);
                            DateTime data = r.GetDateTime(1);
                            int cnt = r.GetInt32(2);
                            int tydzien = (int)((data - start).TotalDays / 7);
                            if (tydzien < 0) tydzien = 0;
                            if (tydzien >= liczbaTygodni) tydzien = liczbaTygodni - 1;

                            if (!wynik.ContainsKey(nazwa))
                                wynik[nazwa] = new DaneHandlowca { Nazwa = nazwa, Dane = new int[liczbaTygodni] };

                            wynik[nazwa].Dane[tydzien] += cnt;
                        }
                    }
                }
            }

            foreach (var h in wynik.Values)
                h.Suma = h.Dane.Sum();

            return (wynik, etykiety);
        }

        private void WypelnijWykresSlupkowy()
        {
            var aktywni = aktualneData.Values.Where(x => x.Suma > 0).OrderByDescending(x => x.Suma).ToList();
            if (aktywni.Count == 0)
            {
                wykresSlupkowy.ItemsSource = null;
                return;
            }

            int maxWartosc = aktywni.Max(x => x.Suma);
            if (maxWartosc == 0) maxWartosc = 1;

            maxSzerokoscSlupka = Math.Max(200, ActualWidth - 350);

            var listaSlupkow = new List<SlupekWykresu>();
            int kolorIndex = 0;

            foreach (var h in aktywni)
            {
                string kolorHex = kolory[kolorIndex % kolory.Length];
                var kolor = (Color)ColorConverter.ConvertFromString(kolorHex);
                double szerokoscProcent = (double)h.Suma / maxWartosc;
                double szerokosc = Math.Max(20, szerokoscProcent * maxSzerokoscSlupka);

                listaSlupkow.Add(new SlupekWykresu
                {
                    Nazwa = h.Nazwa,
                    Suma = h.Suma,
                    SzerokoscSlupka = szerokosc,
                    Kolor = new SolidColorBrush(kolor),
                    KolorCien = kolor,
                    KolorHex = kolorHex
                });

                kolorIndex++;
            }

            wykresSlupkowy.ItemsSource = listaSlupkow;
        }

        private void AktualizujSzerokoscSlupkow()
        {
            if (wykresSlupkowy.ItemsSource == null) return;
            var lista = wykresSlupkowy.ItemsSource as List<SlupekWykresu>;
            if (lista == null || lista.Count == 0) return;

            int maxWartosc = lista.Max(x => x.Suma);
            if (maxWartosc == 0) maxWartosc = 1;

            maxSzerokoscSlupka = Math.Max(200, ActualWidth - 350);

            foreach (var s in lista)
            {
                double szerokoscProcent = (double)s.Suma / maxWartosc;
                s.SzerokoscSlupka = Math.Max(20, szerokoscProcent * maxSzerokoscSlupka);
            }

            wykresSlupkowy.ItemsSource = null;
            wykresSlupkowy.ItemsSource = lista;
        }

        private void WypelnijTabele()
        {
            if (tabelaDane == null) return;

            var aktywni = aktualneData.Values.Where(x => x.Suma > 0).OrderByDescending(x => x.Suma).ToList();

            while (tabelaDane.Columns.Count > 1)
                tabelaDane.Columns.RemoveAt(1);

            for (int i = 0; i < aktualneEtykiety.Count; i++)
            {
                var col = new DataGridTextColumn
                {
                    Header = aktualneEtykiety[i],
                    Binding = new System.Windows.Data.Binding($"Dane[{i}]"),
                    Width = TrybTygodniowy ? 50 : 50
                };
                col.ElementStyle = new Style(typeof(TextBlock))
                {
                    Setters = {
                        new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center),
                        new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center)
                    }
                };
                tabelaDane.Columns.Add(col);
            }

            var colSuma = new DataGridTextColumn
            {
                Header = "SUMA",
                Binding = new System.Windows.Data.Binding("Suma"),
                Width = 65
            };
            colSuma.ElementStyle = new Style(typeof(TextBlock))
            {
                Setters = {
                    new Setter(TextBlock.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))),
                    new Setter(TextBlock.FontWeightProperty, FontWeights.Bold),
                    new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center),
                    new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center)
                }
            };
            tabelaDane.Columns.Add(colSuma);

            tabelaDane.ItemsSource = aktywni;
        }

        private DateTime lastClickTime = DateTime.MinValue;
        private string lastClickedNazwa = "";

        private void Slupek_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.DataContext is SlupekWykresu slupek)
            {
                if ((DateTime.Now - lastClickTime).TotalMilliseconds < 500 && lastClickedNazwa == slupek.Nazwa)
                {
                    PokazWykresLiniowy(slupek.Nazwa, slupek.KolorHex);
                }
                lastClickTime = DateTime.Now;
                lastClickedNazwa = slupek.Nazwa;
            }
        }

        private void TabelaDane_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (tabelaDane.SelectedItem is DaneHandlowca dane)
            {
                int idx = aktualneData.Values.Where(x => x.Suma > 0).OrderByDescending(x => x.Suma).ToList().IndexOf(dane);
                string kolorHex = kolory[idx % kolory.Length];
                PokazWykresLiniowy(dane.Nazwa, kolorHex);
            }
        }

        private void PokazWykresLiniowy(string nazwaHandlowca, string kolorHex)
        {
            if (!aktualneData.ContainsKey(nazwaHandlowca)) return;

            var dane = aktualneData[nazwaHandlowca];
            var kolor = (Color)ColorConverter.ConvertFromString(kolorHex);

            var okno = new Window
            {
                Title = $"Wykres aktywnosci: {nazwaHandlowca}",
                Width = 900,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"))
            };

            var grid = new Grid { Margin = new Thickness(30) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Naglowek
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            header.Children.Add(new TextBlock
            {
                Text = nazwaHandlowca,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(kolor)
            });
            header.Children.Add(new TextBlock
            {
                Text = $" - {(TrybTygodniowy ? "tygodnie" : "miesiace")} ({TypDanych})",
                FontSize = 16,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(8, 0, 0, 2)
            });
            header.Children.Add(new TextBlock
            {
                Text = $"  SUMA: {dane.Suma}",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(20, 0, 0, 2)
            });
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // Canvas na wykres
            var canvas = new Canvas { Background = Brushes.Transparent, ClipToBounds = true };
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20)
            };
            border.Child = canvas;
            Grid.SetRow(border, 1);
            grid.Children.Add(border);

            okno.Content = grid;

            okno.Loaded += (s, e) =>
            {
                RysujWykresLiniowyNaCanvas(canvas, dane.Dane, aktualneEtykiety, kolor);
            };

            okno.Show();
        }

        private void RysujWykresLiniowyNaCanvas(Canvas canvas, int[] dane, List<string> etykiety, Color kolor)
        {
            canvas.Children.Clear();

            double w = canvas.ActualWidth;
            double h = canvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double ml = 50, mr = 20, mt = 20, mb = 40;
            double cw = w - ml - mr;
            double ch = h - mt - mb;

            int maxVal = dane.Max();
            if (maxVal == 0) maxVal = 1;
            int krok = maxVal <= 10 ? 1 : maxVal <= 50 ? 5 : maxVal <= 100 ? 10 : 25;
            maxVal = (int)(Math.Ceiling((maxVal + krok * 0.5) / (double)krok) * krok);

            // Os Y
            for (int i = 0; i <= 5; i++)
            {
                double y = mt + (ch * i / 5);
                int wartosc = maxVal - (maxVal * i / 5);

                canvas.Children.Add(new Line
                {
                    X1 = ml, Y1 = y, X2 = w - mr, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 4 }
                });

                var lblY = new TextBlock
                {
                    Text = wartosc.ToString(),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    TextAlignment = TextAlignment.Right,
                    Width = 40
                };
                Canvas.SetLeft(lblY, 5);
                Canvas.SetTop(lblY, y - 8);
                canvas.Children.Add(lblY);
            }

            // Punkty i linia
            int punkty = Math.Min(dane.Length, etykiety.Count);
            double stepX = cw / Math.Max(1, punkty - 1);

            var points = new PointCollection();
            for (int i = 0; i < punkty; i++)
            {
                double x = ml + i * stepX;
                double y = mt + ch - (dane[i] / (double)maxVal * ch);
                points.Add(new Point(x, y));

                // Etykieta X
                var lblX = new TextBlock
                {
                    Text = etykiety[i],
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
                };
                Canvas.SetLeft(lblX, x - 15);
                Canvas.SetTop(lblX, mt + ch + 8);
                canvas.Children.Add(lblX);
            }

            // Obszar pod linia (gradient)
            if (points.Count > 1)
            {
                var areaPoints = new PointCollection(points);
                areaPoints.Add(new Point(ml + (punkty - 1) * stepX, mt + ch));
                areaPoints.Add(new Point(ml, mt + ch));

                var area = new Polygon
                {
                    Points = areaPoints,
                    Fill = new LinearGradientBrush(
                        Color.FromArgb(80, kolor.R, kolor.G, kolor.B),
                        Color.FromArgb(10, kolor.R, kolor.G, kolor.B),
                        90)
                };
                canvas.Children.Add(area);
            }

            // Linia
            var linia = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(kolor),
                StrokeThickness = 3,
                StrokeLineJoin = PenLineJoin.Round
            };
            canvas.Children.Add(linia);

            // Punkty
            for (int i = 0; i < punkty; i++)
            {
                var el = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Fill = new SolidColorBrush(kolor),
                    Stroke = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                    StrokeThickness = 2,
                    ToolTip = $"{etykiety[i]}: {dane[i]}"
                };
                Canvas.SetLeft(el, points[i].X - 6);
                Canvas.SetTop(el, points[i].Y - 6);
                canvas.Children.Add(el);

                // Wartosc nad punktem
                var lblVal = new TextBlock
                {
                    Text = dane[i].ToString(),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(kolor)
                };
                Canvas.SetLeft(lblVal, points[i].X - 10);
                Canvas.SetTop(lblVal, points[i].Y - 22);
                canvas.Children.Add(lblVal);
            }
        }
    }

    public class DaneHandlowca
    {
        public string Nazwa { get; set; }
        public int[] Dane { get; set; }
        public int Suma { get; set; }
    }

    public class SlupekWykresu
    {
        public string Nazwa { get; set; }
        public int Suma { get; set; }
        public double SzerokoscSlupka { get; set; }
        public SolidColorBrush Kolor { get; set; }
        public Color KolorCien { get; set; }
        public string KolorHex { get; set; }
    }
}
