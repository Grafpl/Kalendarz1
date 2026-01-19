using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Windows.Threading;

namespace Kalendarz1
{
    public partial class AnalizaWydajnosciKrojenia : Window
    {
        private string connectionStringHandel;
        private ObservableCollection<AnalizaDziennaModel> analizaDzienna;
        private ObservableCollection<SzczegolyElementyModel> szczegolyElementy;
        private ObservableCollection<TopProduktModel> topElementy;
        private ObservableCollection<TopProduktModel> topPodroby;
        private DispatcherTimer autoTimer;
        private decimal progTolerancji = 2.0m; // domyślnie 2%

        public AnalizaWydajnosciKrojenia(string connStringHandel)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            connectionStringHandel = connStringHandel;

            analizaDzienna = new ObservableCollection<AnalizaDziennaModel>();
            szczegolyElementy = new ObservableCollection<SzczegolyElementyModel>();
            topElementy = new ObservableCollection<TopProduktModel>();
            topPodroby = new ObservableCollection<TopProduktModel>();

            dgAnalizaDzienna.ItemsSource = analizaDzienna;
            dgSzczegolyElementy.ItemsSource = szczegolyElementy;
            dgTopElementy.ItemsSource = topElementy;
            dgTopPodroby.ItemsSource = topPodroby;

            // Ustaw domyślne daty - ostatnie 7 dni
            dpDataDo.SelectedDate = DateTime.Today;
            dpDataOd.SelectedDate = DateTime.Today.AddDays(-7);

            // Timer dla zegara
            var clockTimer = new DispatcherTimer();
            clockTimer.Tick += (s, e) => {
                txtDataCzas.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss",
                    new CultureInfo("pl-PL"));
            };
            clockTimer.Interval = TimeSpan.FromSeconds(1);
            clockTimer.Start();

            // Wczytaj dane
            AnalizujDane();
        }

        private void AnalizujDane()
        {
            if (!dpDataOd.SelectedDate.HasValue || !dpDataDo.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz zakres dat", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pobierz próg tolerancji
            string progText = (cbProg.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "± 2%";
            progTolerancji = decimal.Parse(progText.Replace("±", "").Replace("%", "").Trim());

            // Wyczyść kolekcje
            analizaDzienna.Clear();
            szczegolyElementy.Clear();
            topElementy.Clear();
            topPodroby.Clear();

            // Pobierz dane
            PobierzDaneTuszkiB();
            PobierzDaneElementow();
            ObliczAnalize();
            PobierzTopProdukty();
            RysujWykres();
            AktualizujKartyKPI();
        }

        private void PobierzDaneTuszkiB()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringHandel))
                {
                    conn.Open();

                    // Zapytanie o tuszki A i B - dokładnie jak podał użytkownik
                    string query = @"
                        SELECT 
                            CAST(MG.[data] AS DATE) AS DataProdukcji,
                            MZ.[kod],
                            ABS(SUM(CASE WHEN MG.[seria] = 'sPWU' THEN MZ.[ilosc] ELSE 0 END)) AS Przychod,
                            SUM(CASE WHEN MG.[seria] = 'RWP' THEN ABS(MZ.[ilosc]) ELSE 0 END) AS Krojenie
                        FROM [HM].[MZ] MZ
                        INNER JOIN [HM].[MG] MG ON MZ.[super] = MG.[id] 
                        WHERE 
                            MZ.[kod] IN ('Kurczak B', 'Kurczak A') 
                            AND MZ.[magazyn] = 65554 
                            AND MG.[data] >= @DataOd
                            AND MG.[data] <= @DataDo
                        GROUP BY 
                            CAST(MG.[data] AS DATE),
                            MZ.[kod]
                        ORDER BY 
                            DataProdukcji, MZ.[kod]";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", dpDataOd.SelectedDate.Value);
                        cmd.Parameters.AddWithValue("@DataDo", dpDataDo.SelectedDate.Value.AddDays(1));

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DateTime data = Convert.ToDateTime(reader["DataProdukcji"]);
                                string kod = reader["kod"].ToString();
                                decimal przychod = Convert.ToDecimal(reader["Przychod"]);
                                decimal krojenie = Convert.ToDecimal(reader["Krojenie"]);

                                // Znajdź lub utwórz rekord dla tej daty
                                var dzien = analizaDzienna.FirstOrDefault(d => d.DataProdukcji == data);
                                if (dzien == null)
                                {
                                    dzien = new AnalizaDziennaModel
                                    {
                                        DataProdukcji = data,
                                        DzienTygodnia = data.ToString("ddd", new CultureInfo("pl-PL"))
                                    };
                                    analizaDzienna.Add(dzien);
                                }

                                // Tuszka B idzie do krojenia
                                if (kod == "Kurczak B")
                                {
                                    dzien.TuszkaBKg = krojenie; // To co idzie do krojenia (RWP)
                                }
                                // Można też śledzić tuszkę A jeśli potrzeba
                                else if (kod == "Kurczak A")
                                {
                                    dzien.TuszkaAKg = przychod; // Tuszka A sprzedawana bezpośrednio
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania danych tuszek:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PobierzDaneElementow()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringHandel))
                {
                    conn.Open();

                    // Zapytanie o elementy i podroby - dokładnie jak podał użytkownik
                    string query = @"
                        WITH DaneSurowe AS (
                            SELECT 
                                CAST(MG.[data] AS DATE) AS DataProdukcji,
                                MZ.[kod] AS KodMZ,
                                MZ.[idtw],
                                MG.[Seria],
                                MG.[Kod] AS KodMG,
                                TW.[katalog],
                                TW.[nazwa],
                                -- Przypisanie kategorii na podstawie katalogu
                                CASE 
                                    WHEN TW.katalog IN (67095, 67104) THEN 'Mięso'
                                    WHEN TW.katalog = 67153 THEN 'Mrozony'
                                    WHEN TW.katalog = 65882 THEN 'Zywy'
                                    WHEN TW.katalog = 67094 THEN 'Odpady'
                                    ELSE 'Inne'
                                END AS Kategoria,
                                SUM(CASE WHEN MG.[seria] IN ('PWP', 'sPWU') THEN ABS(MZ.[ilosc]) ELSE 0 END) AS Przychod,
                                SUM(CASE WHEN MG.[seria] = 'RWP' THEN ABS(MZ.[ilosc]) ELSE 0 END) AS Krojenie
                            FROM [HM].[MZ] MZ
                            INNER JOIN [HM].[MG] MG ON MZ.[super] = MG.[id] 
                            INNER JOIN [HM].[TW] TW ON MZ.idtw = TW.id
                            WHERE 
                                MZ.[magazyn] = 65554 
                                AND MG.[data] >= @DataOd
                                AND MG.[data] <= @DataDo
                                AND MG.[seria] IN ('PWP', 'RWP', 'sPWU')
                            GROUP BY 
                                CAST(MG.[data] AS DATE),
                                MZ.[kod],
                                MZ.[idtw],
                                MG.[Seria],
                                MG.[Kod],
                                TW.[katalog],
                                TW.[nazwa]
                        )
                        SELECT *
                        FROM DaneSurowe
                        WHERE Przychod > 0 OR Krojenie > 0
                        ORDER BY DataProdukcji, KodMZ";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", dpDataOd.SelectedDate.Value);
                        cmd.Parameters.AddWithValue("@DataDo", dpDataDo.SelectedDate.Value.AddDays(1));

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            var podrobySet = new HashSet<string>();

                            while (reader.Read())
                            {
                                DateTime data = Convert.ToDateTime(reader["DataProdukcji"]);
                                string kodMZ = reader["KodMZ"].ToString();
                                string seria = reader["Seria"].ToString();
                                string kategoria = reader["Kategoria"].ToString();
                                string nazwa = reader["nazwa"]?.ToString() ?? kodMZ;
                                decimal przychod = Convert.ToDecimal(reader["Przychod"]);
                                decimal krojenie = Convert.ToDecimal(reader["Krojenie"]);

                                // Dodaj do szczegółów
                                szczegolyElementy.Add(new SzczegolyElementyModel
                                {
                                    DataProdukcji = data,
                                    KodProduktu = kodMZ,
                                    NazwaProduktu = nazwa,
                                    Kategoria = kategoria,
                                    Przychod = przychod,
                                    Krojenie = krojenie,
                                    Seria = seria,
                                    Magazyn = "65554"
                                });

                                // Znajdź dzień w analizie
                                var dzien = analizaDzienna.FirstOrDefault(d => d.DataProdukcji == data);
                                if (dzien != null)
                                {
                                    // Elementy z krojenia (seria PWP)
                                    if (seria == "PWP" && przychod > 0)
                                    {
                                        dzien.ElementyKg += przychod;
                                    }
                                    // Podroby (seria sPWU) - wątroba, żołądki, serce
                                    else if (seria == "sPWU" && przychod > 0)
                                    {
                                        dzien.PodrobyKg += przychod;

                                        // Zbierz typy podrobów
                                        if (kodMZ.ToLower().Contains("wątrob") ||
                                            kodMZ.ToLower().Contains("żołąd") ||
                                            kodMZ.ToLower().Contains("serc"))
                                        {
                                            podrobySet.Add(kodMZ);
                                        }
                                    }
                                }
                            }

                            // Ustaw typy podrobów w karcie
                            if (podrobySet.Count > 0)
                            {
                                txtPodrobyTypy.Text = string.Join(", ", podrobySet.Take(3));
                                if (podrobySet.Count > 3)
                                    txtPodrobyTypy.Text += $" +{podrobySet.Count - 3}";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania danych elementów:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ObliczAnalize()
        {
            int dniZProblemem = 0;
            decimal sumaRoznic = 0;
            decimal minWydajnosc = 100;
            decimal maxWydajnosc = 0;

            foreach (var dzien in analizaDzienna)
            {
                // Suma wyjścia = elementy + podroby
                dzien.SumaWyjscie = dzien.ElementyKg + dzien.PodrobyKg;

                // Wydajność = (wyjście / wejście) * 100
                if (dzien.TuszkaBKg > 0)
                {
                    dzien.WydajnoscProcent = (dzien.SumaWyjscie / dzien.TuszkaBKg) * 100;

                    // Aktualizuj min/max
                    if (dzien.WydajnoscProcent < minWydajnosc) minWydajnosc = dzien.WydajnoscProcent;
                    if (dzien.WydajnoscProcent > maxWydajnosc) maxWydajnosc = dzien.WydajnoscProcent;
                }

                // Różnica
                dzien.RoznicaKg = dzien.SumaWyjscie - dzien.TuszkaBKg;
                dzien.CzyUjemna = dzien.RoznicaKg < 0;
                sumaRoznic += dzien.RoznicaKg;

                // Sprawdź czy jest problem (wydajność poza progiem)
                decimal wydajnoscOptymalna = 95.0m; // zakładamy 95% jako optymalną
                decimal roznicaWydajnosci = Math.Abs(dzien.WydajnoscProcent - wydajnoscOptymalna);

                if (roznicaWydajnosci > progTolerancji)
                {
                    dzien.CzyProblem = true;
                    dzien.Status = "⚠️ Problem";
                    dzien.Uwagi = $"Wydajność {dzien.WydajnoscProcent:F1}% (norma: {wydajnoscOptymalna}% ±{progTolerancji}%)";
                    dniZProblemem++;
                }
                else
                {
                    dzien.CzyProblem = false;
                    dzien.Status = "✅ OK";
                    dzien.Uwagi = "W normie";
                }

                // Dodatkowe uwagi
                if (dzien.WydajnoscProcent > 100)
                {
                    dzien.Uwagi = "❗ Wydajność >100% - sprawdź dane!";
                    dzien.CzyProblem = true;
                }
                else if (dzien.WydajnoscProcent < 85)
                {
                    dzien.Uwagi = "❗ Bardzo niska wydajność!";
                    dzien.CzyProblem = true;
                }
            }

            // Filtruj tylko problemy jeśli zaznaczono
            if (chkTylkoProblemy.IsChecked == true)
            {
                var problemy = analizaDzienna.Where(d => d.CzyProblem).ToList();
                analizaDzienna.Clear();
                foreach (var p in problemy)
                    analizaDzienna.Add(p);
            }

            // Ustaw alerty
            txtAlertyBadge.Text = dniZProblemem.ToString();
            txtDniZProblemem.Text = $"{dniZProblemem} / {analizaDzienna.Count}";
            txtRoznicaKg.Text = $"{sumaRoznic:N2}";

            if (sumaRoznic < 0)
            {
                txtRoznicaKg.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
            }
            else
            {
                txtRoznicaKg.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
            }

            // Zakres wydajności
            if (analizaDzienna.Count > 0)
            {
                txtWydajnoscZakres.Text = $"{minWydajnosc:F1}% - {maxWydajnosc:F1}%";
            }
        }

        private void PobierzTopProdukty()
        {
            try
            {
                // TOP Elementy (seria PWP)
                var elementy = szczegolyElementy
                    .Where(e => e.Seria == "PWP" && e.Przychod > 0)
                    .GroupBy(e => e.NazwaProduktu)
                    .Select(g => new TopProduktModel
                    {
                        Nazwa = g.Key,
                        Ilosc = g.Sum(x => x.Przychod)
                    })
                    .OrderByDescending(x => x.Ilosc)
                    .Take(10)
                    .ToList();

                topElementy.Clear();
                foreach (var e in elementy)
                    topElementy.Add(e);

                // TOP Podroby (seria sPWU)
                var podroby = szczegolyElementy
                    .Where(e => e.Seria == "sPWU" && e.Przychod > 0)
                    .GroupBy(e => e.NazwaProduktu)
                    .Select(g => new TopProduktModel
                    {
                        Nazwa = g.Key,
                        Ilosc = g.Sum(x => x.Przychod)
                    })
                    .OrderByDescending(x => x.Ilosc)
                    .Take(10)
                    .ToList();

                topPodroby.Clear();
                foreach (var p in podroby)
                    topPodroby.Add(p);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd analizy TOP produktów:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AktualizujKartyKPI()
        {
            if (analizaDzienna.Count == 0)
            {
                txtTuszkaBWejscie.Text = "0.00";
                txtTuszkaBSrednio.Text = "0.00";
                txtElementyWyjscie.Text = "0.00";
                txtElementyPozycje.Text = "0";
                txtPodrobyIlosc.Text = "0.00";
                txtWydajnoscSrednia.Text = "0.0%";
                txtPodsumowanie.Text = "Brak danych dla wybranego okresu";
                return;
            }

            // Sumy
            decimal sumaTuszkaB = analizaDzienna.Sum(d => d.TuszkaBKg);
            decimal sumaElementy = analizaDzienna.Sum(d => d.ElementyKg);
            decimal sumaPodroby = analizaDzienna.Sum(d => d.PodrobyKg);
            decimal sredniaWydajnosc = analizaDzienna.Average(d => d.WydajnoscProcent);
            int dniAnalizy = analizaDzienna.Count;

            // Tuszka B
            txtTuszkaBWejscie.Text = $"{sumaTuszkaB:N2}";
            txtTuszkaBSrednio.Text = $"{(sumaTuszkaB / dniAnalizy):N2}";

            // Elementy
            txtElementyWyjscie.Text = $"{sumaElementy:N2}";
            txtElementyPozycje.Text = szczegolyElementy
                .Where(e => e.Seria == "PWP")
                .Select(e => e.KodProduktu)
                .Distinct()
                .Count()
                .ToString();

            // Podroby
            txtPodrobyIlosc.Text = $"{sumaPodroby:N2}";

            // Wydajność
            txtWydajnoscSrednia.Text = $"{sredniaWydajnosc:F1}%";

            if (sredniaWydajnosc >= 93 && sredniaWydajnosc <= 97)
            {
                txtWydajnoscSrednia.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
                txtWydajnoscStatus.Text = "✓";
                txtWydajnoscStatus.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
            }
            else if (sredniaWydajnosc >= 90 && sredniaWydajnosc <= 100)
            {
                txtWydajnoscSrednia.Foreground = new SolidColorBrush(Color.FromRgb(243, 156, 18));
                txtWydajnoscStatus.Text = "!";
                txtWydajnoscStatus.Foreground = new SolidColorBrush(Color.FromRgb(243, 156, 18));
            }
            else
            {
                txtWydajnoscSrednia.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                txtWydajnoscStatus.Text = "✗";
                txtWydajnoscStatus.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
            }

            // Podsumowanie
            txtPodsumowanie.Text = $"Okres: {dpDataOd.SelectedDate:dd.MM} - {dpDataDo.SelectedDate:dd.MM} | " +
                                   $"Dni analizy: {dniAnalizy} | " +
                                   $"Średnia wydajność: {sredniaWydajnosc:F1}% | " +
                                   $"Bilans: {(sumaElementy + sumaPodroby - sumaTuszkaB):+#,##0.00;-#,##0.00;0} kg";
        }

        private void RysujWykres()
        {
            canvasWykres.Children.Clear();

            if (analizaDzienna.Count < 2) return;

            double width = canvasWykres.ActualWidth > 0 ? canvasWykres.ActualWidth : 800;
            double height = canvasWykres.ActualHeight > 0 ? canvasWykres.ActualHeight : 100;

            if (width <= 0 || height <= 0) return;

            // Znajdź min/max wydajności
            decimal minWyd = analizaDzienna.Min(d => d.WydajnoscProcent);
            decimal maxWyd = analizaDzienna.Max(d => d.WydajnoscProcent);
            decimal zakres = maxWyd - minWyd;
            if (zakres == 0) zakres = 1;

            // Rysuj linię trendu
            var polyline = new Polyline();
            polyline.Stroke = new SolidColorBrush(Color.FromRgb(52, 152, 219));
            polyline.StrokeThickness = 2;

            for (int i = 0; i < analizaDzienna.Count; i++)
            {
                double x = (i / (double)(analizaDzienna.Count - 1)) * (width - 20) + 10;
                double y = height - ((double)(analizaDzienna[i].WydajnoscProcent - minWyd) / (double)zakres * (height - 20)) - 10;
                polyline.Points.Add(new Point(x, y));

                // Dodaj punkt
                var ellipse = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = analizaDzienna[i].CzyProblem ?
                        new SolidColorBrush(Color.FromRgb(231, 76, 60)) :
                        new SolidColorBrush(Color.FromRgb(39, 174, 96))
                };
                Canvas.SetLeft(ellipse, x - 3);
                Canvas.SetTop(ellipse, y - 3);
                canvasWykres.Children.Add(ellipse);
            }

            canvasWykres.Children.Add(polyline);

            // Linia normy (95%)
            decimal norma = 95.0m;
            if (norma >= minWyd && norma <= maxWyd)
            {
                double yNorma = height - ((double)(norma - minWyd) / (double)zakres * (height - 20)) - 10;
                var liniaNormy = new Line
                {
                    X1 = 10,
                    Y1 = yNorma,
                    X2 = width - 10,
                    Y2 = yNorma,
                    Stroke = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 5, 3 },
                    Opacity = 0.5
                };
                canvasWykres.Children.Add(liniaNormy);
            }
        }

        private void BtnAnalizuj_Click(object sender, RoutedEventArgs e)
        {
            AnalizujDane();
        }

        private void BtnRaport_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funkcja generowania raportu będzie dostępna wkrótce.",
                "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAutoAnaliza_Click(object sender, RoutedEventArgs e)
        {
            if (autoTimer == null)
            {
                autoTimer = new DispatcherTimer();
                autoTimer.Interval = TimeSpan.FromMinutes(5);
                autoTimer.Tick += (s, ev) => AnalizujDane();
                autoTimer.Start();

                MessageBox.Show("Automatyczna analiza włączona - odświeżanie co 5 minut.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                autoTimer.Stop();
                autoTimer = null;

                MessageBox.Show("Automatyczna analiza wyłączona.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnUstawieniaNorm_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funkcja ustawień norm wydajności będzie dostępna wkrótce.\n\n" +
                "Obecnie używana norma: 95% ±2%",
                "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            autoTimer?.Stop();
            Close();
        }
    }

    // Modele danych
    public class AnalizaDziennaModel
    {
        public DateTime DataProdukcji { get; set; }
        public string DzienTygodnia { get; set; }
        public decimal TuszkaAKg { get; set; }  // Tuszka A (sprzedaż bezpośrednia)
        public decimal TuszkaBKg { get; set; }  // Tuszka B (do krojenia)
        public decimal ElementyKg { get; set; }  // Elementy z krojenia
        public decimal PodrobyKg { get; set; }   // Podroby
        public decimal SumaWyjscie { get; set; } // Elementy + Podroby
        public decimal WydajnoscProcent { get; set; }
        public decimal RoznicaKg { get; set; }
        public bool CzyUjemna { get; set; }
        public bool CzyProblem { get; set; }
        public string Status { get; set; }
        public string Uwagi { get; set; }
    }

    public class SzczegolyElementyModel
    {
        public DateTime DataProdukcji { get; set; }
        public string KodProduktu { get; set; }
        public string NazwaProduktu { get; set; }
        public string Kategoria { get; set; }
        public decimal Przychod { get; set; }
        public decimal Krojenie { get; set; }
        public string Seria { get; set; }
        public string Magazyn { get; set; }
    }

    public class TopProduktModel
    {
        public string Nazwa { get; set; }
        public decimal Ilosc { get; set; }
    }
}