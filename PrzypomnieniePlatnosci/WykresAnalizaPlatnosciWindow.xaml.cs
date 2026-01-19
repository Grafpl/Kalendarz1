using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Kalendarz1
{
    public partial class WykresAnalizaPlatnosciWindow : Window
    {
        private string connectionString;
        private string nazwaKontrahenta;
        private List<DaneWykresu> daneFaktur;
        private List<DaneWykresu> daneWplat;
        public PlotModel PlotModel { get; set; }

        public WykresAnalizaPlatnosciWindow(string connString, string kontrahent)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            if (string.IsNullOrEmpty(connString))
            {
                MessageBox.Show("BŁĄD: ConnectionString jest pusty!\n\nOkno nie otrzymało połączenia do bazy danych.",
                    "Błąd inicjalizacji", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
                return;
            }

            connectionString = connString;
            nazwaKontrahenta = kontrahent;

            txtKontrahent.Text = $"Kontrahent: {kontrahent}";

            // Ustaw domyślny zakres dat - ostatnie 12 tygodni
            dpDataDo.SelectedDate = DateTime.Now;
            dpDataOd.SelectedDate = DateTime.Now.AddDays(-84); // 12 tygodni

            DataContext = this;

            // Nie ładuj danych automatycznie - czekaj na kliknięcie przycisku
            panelBrakDanych.Visibility = Visibility.Visible;
            plotView.Visibility = Visibility.Collapsed;
        }

        private void DatePicker_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Opcjonalnie: automatyczne odświeżanie po zmianie daty
        }

        private void WidokWykresu_Changed(object sender, RoutedEventArgs e)
        {
            // Odśwież wykres gdy użytkownik zmieni widok
            if (daneFaktur != null && daneWplat != null && (daneFaktur.Any() || daneWplat.Any()))
            {
                AktualizujWykres();
            }
        }

        private void BtnAnalizuj_Click(object sender, RoutedEventArgs e)
        {
            if (!dpDataOd.SelectedDate.HasValue || !dpDataDo.SelectedDate.HasValue)
            {
                MessageBox.Show("⚠️ Proszę wybrać zakres dat!", "Brak dat",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dpDataOd.SelectedDate.Value > dpDataDo.SelectedDate.Value)
            {
                MessageBox.Show("⚠️ Data rozpoczęcia nie może być późniejsza niż data zakończenia!",
                    "Nieprawidłowy zakres", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WczytajIWyswietlDane();
        }

        private void BtnOstatnie12_Click(object sender, RoutedEventArgs e)
        {
            dpDataDo.SelectedDate = DateTime.Now;
            dpDataOd.SelectedDate = DateTime.Now.AddDays(-84); // 12 tygodni
            WczytajIWyswietlDane();
        }

        private void BtnOstatnie26_Click(object sender, RoutedEventArgs e)
        {
            dpDataDo.SelectedDate = DateTime.Now;
            dpDataOd.SelectedDate = DateTime.Now.AddDays(-182); // 26 tygodni (pół roku)
            WczytajIWyswietlDane();
        }

        private void WczytajIWyswietlDane()
        {
            try
            {
                if (!dpDataOd.SelectedDate.HasValue || !dpDataDo.SelectedDate.HasValue)
                    return;

                WczytajDaneZBazy(dpDataOd.SelectedDate.Value, dpDataDo.SelectedDate.Value);
                AktualizujWykres();
                AktualizujStatystyki();
                AktualizujTabele();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd ładowania danych: {ex.Message}\n\nSzczegóły:\n{ex.StackTrace}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                panelBrakDanych.Visibility = Visibility.Visible;
                plotView.Visibility = Visibility.Collapsed;
            }
        }

        private void WczytajDaneZBazy(DateTime dataOd, DateTime dataDo)
        {
            daneFaktur = new List<DaneWykresu>();
            daneWplat = new List<DaneWykresu>();

            string queryFaktury = @"
                SELECT
                    CONVERT(date, DK.data) AS Data,
                    CAST(SUM(DK.walbrutto) AS DECIMAL(18, 2)) AS Wartosc
                FROM [HANDEL].[HM].[DK] AS DK
                JOIN [HANDEL].[SSCommon].[STContractors] AS C ON DK.khid = C.id
                WHERE
                    DK.anulowany = 0
                    AND C.Shortcut = @NazwaKontrahenta
                    AND DK.data >= @DataOd
                    AND DK.data <= @DataDo
                GROUP BY CONVERT(date, DK.data)
                ORDER BY Data";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (var cmd = new SqlCommand(queryFaktury, conn))
                    {
                        cmd.Parameters.AddWithValue("@NazwaKontrahenta", nazwaKontrahenta);
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);
                        cmd.Parameters.AddWithValue("@DataDo", dataDo);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                daneFaktur.Add(new DaneWykresu
                                {
                                    Data = Convert.ToDateTime(reader["Data"]),
                                    Wartosc = Convert.ToDecimal(reader["Wartosc"])
                                });
                            }
                        }
                    }

                    string queryWplaty = @"
                        SELECT
                            CONVERT(date, PN.datarozl) AS Data,
                            CAST(SUM(ISNULL(PN.kwotarozl, 0)) AS DECIMAL(18, 2)) AS Wartosc
                        FROM [HANDEL].[HM].[PN] AS PN
                        JOIN [HANDEL].[HM].[DK] AS DK ON PN.dkid = DK.id
                        JOIN [HANDEL].[SSCommon].[STContractors] AS C ON DK.khid = C.id
                        WHERE
                            C.Shortcut = @NazwaKontrahenta
                            AND PN.datarozl >= @DataOd
                            AND PN.datarozl <= @DataDo
                            AND PN.datarozl IS NOT NULL
                        GROUP BY CONVERT(date, PN.datarozl)
                        ORDER BY Data";

                    using (var cmd = new SqlCommand(queryWplaty, conn))
                    {
                        cmd.Parameters.AddWithValue("@NazwaKontrahenta", nazwaKontrahenta);
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);
                        cmd.Parameters.AddWithValue("@DataDo", dataDo);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                daneWplat.Add(new DaneWykresu
                                {
                                    Data = Convert.ToDateTime(reader["Data"]),
                                    Wartosc = Convert.ToDecimal(reader["Wartosc"])
                                });
                            }
                        }
                    }
                }

                // Zawsze agreguj dane tygodniowo
                daneFaktur = AgregujDanaTygodniowo(daneFaktur);
                daneWplat = AgregujDanaTygodniowo(daneWplat);
            }
            catch (SqlException sqlEx)
            {
                throw new Exception($"Błąd połączenia z bazą danych:\n{sqlEx.Message}\n\nServer: {sqlEx.Server}\nNumer błędu: {sqlEx.Number}", sqlEx);
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd wczytywania danych:\n{ex.Message}", ex);
            }
        }

        private List<DaneWykresu> AgregujDanaTygodniowo(List<DaneWykresu> dane)
        {
            if (!dane.Any()) return dane;

            return dane.GroupBy(d => new
            {
                Rok = d.Data.Year,
                Tydzien = System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                    d.Data,
                    System.Globalization.CalendarWeekRule.FirstDay,
                    DayOfWeek.Monday)
            })
            .Select(g => new DaneWykresu
            {
                Data = g.First().Data.AddDays(-(int)g.First().Data.DayOfWeek + (g.First().Data.DayOfWeek == DayOfWeek.Sunday ? -6 : 1)),
                Wartosc = g.Sum(x => x.Wartosc),
                Etykieta = $"Tydzień {g.Key.Tydzien} ({g.Key.Rok})"
            })
            .OrderBy(d => d.Data)
            .ToList();
        }

        private void AktualizujWykres()
        {
            if (!daneFaktur.Any() && !daneWplat.Any())
            {
                panelBrakDanych.Visibility = Visibility.Visible;
                plotView.Visibility = Visibility.Collapsed;
                return;
            }

            panelBrakDanych.Visibility = Visibility.Collapsed;
            plotView.Visibility = Visibility.Visible;

            PlotModel = new PlotModel
            {
                Background = OxyColors.White,
                PlotAreaBorderColor = OxyColor.FromRgb(229, 231, 235),
                PlotAreaBorderThickness = new OxyThickness(1)
            };

            var wszystkieDaty = daneFaktur.Select(d => d.Data)
                .Union(daneWplat.Select(d => d.Data))
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            // Sprawdź który widok jest wybrany
            bool pokazLinieZera = false;

            if (rbWidokPorownanie.IsChecked == true)
            {
                // Widok: Faktury + Wpłaty
                var seriaFaktur = new LineSeries
                {
                    Title = "🔴 Faktury",
                    Color = OxyColor.FromRgb(239, 68, 68),
                    StrokeThickness = 3,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 6,
                    MarkerStroke = OxyColor.FromRgb(239, 68, 68),
                    MarkerFill = OxyColors.White,
                    MarkerStrokeThickness = 2
                };

                var seriaWplat = new LineSeries
                {
                    Title = "🟢 Wpłaty",
                    Color = OxyColor.FromRgb(16, 185, 129),
                    StrokeThickness = 3,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 6,
                    MarkerStroke = OxyColor.FromRgb(16, 185, 129),
                    MarkerFill = OxyColors.White,
                    MarkerStrokeThickness = 2
                };

                for (int i = 0; i < wszystkieDaty.Count; i++)
                {
                    var data = wszystkieDaty[i];
                    var faktury = daneFaktur.FirstOrDefault(d => d.Data == data)?.Wartosc ?? 0;
                    var wplaty = daneWplat.FirstOrDefault(d => d.Data == data)?.Wartosc ?? 0;

                    seriaFaktur.Points.Add(new DataPoint(i, (double)faktury));
                    seriaWplat.Points.Add(new DataPoint(i, (double)wplaty));
                }

                PlotModel.Series.Add(seriaFaktur);
                PlotModel.Series.Add(seriaWplat);
            }
            else if (rbWidokNaleznosc.IsChecked == true)
            {
                // Widok: Tylko Należność (różnica) - POKAŻ LINIĘ ZERA
                pokazLinieZera = true;

                var seriaNaleznosc = new LineSeries
                {
                    Title = "⚠️ Należność (Faktury - Wpłaty)",
                    Color = OxyColor.FromRgb(245, 158, 11),
                    StrokeThickness = 4,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 7,
                    MarkerStroke = OxyColor.FromRgb(245, 158, 11),
                    MarkerFill = OxyColors.White,
                    MarkerStrokeThickness = 2
                };

                for (int i = 0; i < wszystkieDaty.Count; i++)
                {
                    var data = wszystkieDaty[i];
                    var faktury = daneFaktur.FirstOrDefault(d => d.Data == data)?.Wartosc ?? 0;
                    var wplaty = daneWplat.FirstOrDefault(d => d.Data == data)?.Wartosc ?? 0;
                    var naleznosc = faktury - wplaty;

                    seriaNaleznosc.Points.Add(new DataPoint(i, (double)naleznosc));
                }

                PlotModel.Series.Add(seriaNaleznosc);
            }
            else if (rbWidokWszystko.IsChecked == true)
            {
                // Widok: Wszystkie 3 linie - POKAŻ LINIĘ ZERA
                pokazLinieZera = true;

                var seriaFaktur = new LineSeries
                {
                    Title = "🔴 Faktury",
                    Color = OxyColor.FromRgb(239, 68, 68),
                    StrokeThickness = 3,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 5,
                    MarkerStroke = OxyColor.FromRgb(239, 68, 68),
                    MarkerFill = OxyColors.White,
                    MarkerStrokeThickness = 2
                };

                var seriaWplat = new LineSeries
                {
                    Title = "🟢 Wpłaty",
                    Color = OxyColor.FromRgb(16, 185, 129),
                    StrokeThickness = 3,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 5,
                    MarkerStroke = OxyColor.FromRgb(16, 185, 129),
                    MarkerFill = OxyColors.White,
                    MarkerStrokeThickness = 2
                };

                var seriaNaleznosc = new LineSeries
                {
                    Title = "⚠️ Należność",
                    Color = OxyColor.FromRgb(245, 158, 11),
                    StrokeThickness = 3,
                    MarkerType = MarkerType.Diamond,
                    MarkerSize = 5,
                    MarkerStroke = OxyColor.FromRgb(245, 158, 11),
                    MarkerFill = OxyColors.White,
                    MarkerStrokeThickness = 2
                };

                for (int i = 0; i < wszystkieDaty.Count; i++)
                {
                    var data = wszystkieDaty[i];
                    var faktury = daneFaktur.FirstOrDefault(d => d.Data == data)?.Wartosc ?? 0;
                    var wplaty = daneWplat.FirstOrDefault(d => d.Data == data)?.Wartosc ?? 0;
                    var naleznosc = faktury - wplaty;

                    seriaFaktur.Points.Add(new DataPoint(i, (double)faktury));
                    seriaWplat.Points.Add(new DataPoint(i, (double)wplaty));
                    seriaNaleznosc.Points.Add(new DataPoint(i, (double)naleznosc));
                }

                PlotModel.Series.Add(seriaFaktur);
                PlotModel.Series.Add(seriaWplat);
                PlotModel.Series.Add(seriaNaleznosc);
            }

            // Dodaj linię zera jeśli potrzebna (dla należności)
            if (pokazLinieZera)
            {
                var liniaZera = new LineSeries
                {
                    Title = "── Zero",
                    Color = OxyColor.FromRgb(156, 163, 175), // Szary
                    StrokeThickness = 2,
                    LineStyle = LineStyle.Dash,
                    MarkerType = MarkerType.None
                };

                // Dodaj punkty linii zera
                for (int i = 0; i < wszystkieDaty.Count; i++)
                {
                    liniaZera.Points.Add(new DataPoint(i, 0));
                }

                // Dodaj linię zera jako pierwszą (w tle)
                PlotModel.Series.Insert(0, liniaZera);
            }

            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Bottom,
                ItemsSource = wszystkieDaty.Select(d => FormatujDateTygodniowo(d)).ToList(),
                Angle = 45,
                FontSize = 10,
                TextColor = OxyColor.FromRgb(107, 114, 128)
            };
            PlotModel.Axes.Add(categoryAxis);

            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Wartość (zł)",
                TitleFontSize = 12,
                FontSize = 11,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(243, 244, 246),
                MinorGridlineStyle = LineStyle.None,
                TextColor = OxyColor.FromRgb(107, 114, 128),
                TitleColor = OxyColor.FromRgb(31, 41, 55),
                StringFormat = "N0"
            };
            PlotModel.Axes.Add(valueAxis);

            plotView.Model = PlotModel;
            plotView.InvalidatePlot(true);
        }

        private string FormatujDateTygodniowo(DateTime data)
        {
            int tydzien = System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                data,
                System.Globalization.CalendarWeekRule.FirstDay,
                DayOfWeek.Monday);
            return $"T{tydzien}\n{data:dd.MM}";
        }

        private void AktualizujStatystyki()
        {
            decimal sumaFaktur = daneFaktur.Sum(d => d.Wartosc);
            decimal sumaWplat = daneWplat.Sum(d => d.Wartosc);
            decimal roznica = sumaFaktur - sumaWplat;
            decimal wskaznik = sumaFaktur > 0 ? (sumaWplat / sumaFaktur) * 100 : 0;

            int liczbaFaktur = daneFaktur.Count;
            int liczbaWplat = daneWplat.Count;

            txtSumaFaktur.Text = $"{sumaFaktur:N2} zł";
            txtLiczbaFaktur.Text = $"Tygodni: {liczbaFaktur}";

            txtSumaWplat.Text = $"{sumaWplat:N2} zł";
            txtLiczbaWplat.Text = $"Tygodni: {liczbaWplat}";

            txtRoznica.Text = $"{roznica:N2} zł";
            decimal procentRoznicy = sumaFaktur > 0 ? (roznica / sumaFaktur) * 100 : 0;
            txtProcentRoznicy.Text = $"{procentRoznicy:N1}% faktur";

            txtWskaznik.Text = $"{wskaznik:N1}%";

            // Skala oceny
            string ocena;
            Brush kolor;

            if (wskaznik >= 95)
            {
                ocena = "✅ Bardzo dobra";
                kolor = Brushes.Green;
            }
            else if (wskaznik >= 85)
            {
                ocena = "🟢 Dobra";
                kolor = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            else if (wskaznik >= 75)
            {
                ocena = "🟡 Średnia";
                kolor = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            }
            else if (wskaznik >= 60)
            {
                ocena = "🟠 Słaba";
                kolor = new SolidColorBrush(Color.FromRgb(251, 146, 60));
            }
            else
            {
                ocena = "🔴 Bardzo słaba";
                kolor = Brushes.Red;
            }

            txtWskaznik.Foreground = kolor;
            txtOcenaWskaznika.Text = ocena;
            txtOcenaWskaznika.Foreground = kolor;
        }

        private void AktualizujTabele()
        {
            var wszystkieDaty = daneFaktur.Select(d => d.Data)
                .Union(daneWplat.Select(d => d.Data))
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var wiersze = new List<WierszTabeli>();

            foreach (var data in wszystkieDaty)
            {
                var faktury = daneFaktur.FirstOrDefault(d => d.Data == data)?.Wartosc ?? 0;
                var wplaty = daneWplat.FirstOrDefault(d => d.Data == data)?.Wartosc ?? 0;
                var roznica = faktury - wplaty;
                var wskaznik = faktury > 0 ? (wplaty / faktury) * 100 : 0;

                string status;
                string uwagi;

                if (wskaznik >= 95)
                {
                    status = "✅ Bardzo dobra";
                    uwagi = "Spłacalność bardzo wysoka";
                }
                else if (wskaznik >= 85)
                {
                    status = "🟢 Dobra";
                    uwagi = "Dobra spłacalność";
                }
                else if (wskaznik >= 75)
                {
                    status = "🟡 Średnia";
                    uwagi = "Widoczne opóźnienia";
                }
                else if (wskaznik >= 60)
                {
                    status = "🟠 Słaba";
                    uwagi = "Znaczne opóźnienia";
                }
                else
                {
                    status = "🔴 Bardzo słaba";
                    uwagi = "Poważne problemy!";
                }

                var etykieta = daneFaktur.FirstOrDefault(d => d.Data == data)?.Etykieta
                    ?? daneWplat.FirstOrDefault(d => d.Data == data)?.Etykieta
                    ?? $"Tydzień {data:dd.MM.yyyy}";

                wiersze.Add(new WierszTabeli
                {
                    Okres = etykieta,
                    Faktury = faktury,
                    Wplaty = wplaty,
                    Roznica = roznica,
                    Wskaznik = wskaznik,
                    WskaznikProc = $"{wskaznik:N1}%",
                    Status = status,
                    Uwagi = uwagi
                });
            }

            // Dodaj wiersz podsumowania
            if (wiersze.Any())
            {
                var sumaFaktur = wiersze.Sum(w => w.Faktury);
                var sumaWplat = wiersze.Sum(w => w.Wplaty);
                var sumaRoznica = sumaFaktur - sumaWplat;
                var sumaWskaznik = sumaFaktur > 0 ? (sumaWplat / sumaFaktur) * 100 : 0;

                string podsumowanieStatus;
                string podsumowanieUwagi;

                if (sumaWskaznik >= 95)
                {
                    podsumowanieStatus = "✅ Bardzo dobra";
                    podsumowanieUwagi = "Ogólnie bardzo wysoka spłacalność";
                }
                else if (sumaWskaznik >= 85)
                {
                    podsumowanieStatus = "🟢 Dobra";
                    podsumowanieUwagi = "Ogólnie dobra spłacalność";
                }
                else if (sumaWskaznik >= 75)
                {
                    podsumowanieStatus = "🟡 Średnia";
                    podsumowanieUwagi = "Wymaga poprawy";
                }
                else if (sumaWskaznik >= 60)
                {
                    podsumowanieStatus = "🟠 Słaba";
                    podsumowanieUwagi = "Poważne problemy";
                }
                else
                {
                    podsumowanieStatus = "🔴 Bardzo słaba";
                    podsumowanieUwagi = "Krytyczny poziom!";
                }

                wiersze.Add(new WierszTabeli
                {
                    Okres = "━━━━━━━━━━━━━",
                    Faktury = 0,
                    Wplaty = 0,
                    Roznica = 0,
                    Wskaznik = 0,
                    WskaznikProc = "",
                    Status = "",
                    Uwagi = ""
                });

                wiersze.Add(new WierszTabeli
                {
                    Okres = "🏁 SUMA:",
                    Faktury = sumaFaktur,
                    Wplaty = sumaWplat,
                    Roznica = sumaRoznica,
                    Wskaznik = sumaWskaznik,
                    WskaznikProc = $"{sumaWskaznik:N1}%",
                    Status = podsumowanieStatus,
                    Uwagi = podsumowanieUwagi
                });
            }

            dgDane.ItemsSource = wiersze;
        }

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            if (!dpDataOd.SelectedDate.HasValue || !dpDataDo.SelectedDate.HasValue)
            {
                MessageBox.Show("⚠️ Proszę wybrać zakres dat!", "Brak dat",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WczytajIWyswietlDane();
            MessageBox.Show("✓ Dane zostały odświeżone!", "Informacja",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnEksportuj_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = $"Analiza_Platnosci_{nazwaKontrahenta}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var writer = new StreamWriter(saveDialog.FileName, false, System.Text.Encoding.UTF8))
                    {
                        writer.WriteLine("Tydzień;Faktury;Wpłaty;Należność;Wskaźnik;Ocena;Uwagi");

                        var wszystkieDaty = daneFaktur.Select(d => d.Data)
                            .Union(daneWplat.Select(d => d.Data))
                            .Distinct()
                            .OrderBy(d => d)
                            .ToList();

                        foreach (var data in wszystkieDaty)
                        {
                            var faktury = daneFaktur.FirstOrDefault(d => d.Data == data)?.Wartosc ?? 0;
                            var wplaty = daneWplat.FirstOrDefault(d => d.Data == data)?.Wartosc ?? 0;
                            var roznica = faktury - wplaty;
                            var wskaznik = faktury > 0 ? (wplaty / faktury) * 100 : 0;
                            var etykieta = daneFaktur.FirstOrDefault(d => d.Data == data)?.Etykieta ?? data.ToString("dd.MM.yyyy");

                            string status = wskaznik >= 95 ? "Bardzo dobra" :
                                          wskaznik >= 85 ? "Dobra" :
                                          wskaznik >= 75 ? "Średnia" :
                                          wskaznik >= 60 ? "Słaba" : "Bardzo słaba";

                            string uwagi = wskaznik >= 95 ? "Spłacalność bardzo wysoka" :
                                         wskaznik >= 85 ? "Dobra spłacalność" :
                                         wskaznik >= 75 ? "Widoczne opóźnienia" :
                                         wskaznik >= 60 ? "Znaczne opóźnienia" :
                                         "Poważne problemy!";

                            writer.WriteLine($"{etykieta};{faktury:N2};{wplaty:N2};{roznica:N2};{wskaznik:N1}%;{status};{uwagi}");
                        }

                        // Podsumowanie
                        var sumaFaktur = daneFaktur.Sum(d => d.Wartosc);
                        var sumaWplat = daneWplat.Sum(d => d.Wartosc);
                        var sumaRoznica = sumaFaktur - sumaWplat;
                        var sumaWskaznik = sumaFaktur > 0 ? (sumaWplat / sumaFaktur) * 100 : 0;

                        string podsumowanieStatus = sumaWskaznik >= 95 ? "Bardzo dobra" :
                                                   sumaWskaznik >= 85 ? "Dobra" :
                                                   sumaWskaznik >= 75 ? "Średnia" :
                                                   sumaWskaznik >= 60 ? "Słaba" : "Bardzo słaba";

                        writer.WriteLine();
                        writer.WriteLine($"SUMA;{sumaFaktur:N2};{sumaWplat:N2};{sumaRoznica:N2};{sumaWskaznik:N1}%;{podsumowanieStatus};");
                    }

                    MessageBox.Show($"✓ Dane wyeksportowane:\n{saveDialog.FileName}", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd eksportu: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEksportujWykres_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PlotModel == null || PlotModel.Series.Count == 0)
                {
                    MessageBox.Show("⚠️ Brak wykresu do eksportu!", "Brak danych",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "PNG Image (*.png)|*.png",
                    FileName = $"Wykres_{nazwaKontrahenta}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var pngExporter = new OxyPlot.Wpf.PngExporter
                    {
                        Width = 1920,
                        Height = 1080
                    };
                    pngExporter.ExportToFile(PlotModel, saveDialog.FileName);

                    MessageBox.Show($"✓ Wykres wyeksportowany:\n{saveDialog.FileName}", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd eksportu: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class DaneWykresu
    {
        public DateTime Data { get; set; }
        public decimal Wartosc { get; set; }
        public string Etykieta { get; set; }
    }

    public class WierszTabeli
    {
        public string Okres { get; set; }
        public decimal Faktury { get; set; }
        public decimal Wplaty { get; set; }
        public decimal Roznica { get; set; }
        public decimal Wskaznik { get; set; }
        public string WskaznikProc { get; set; }
        public string Status { get; set; }
        public string Uwagi { get; set; }
    }
}