using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Kalendarz1.KontrolaGodzin
{
    public partial class StatystykiWindow : Window
    {
        private List<RejestracjaModel> _dane;
        private int _miesiac;
        private int _rok;

        public StatystykiWindow(List<RejestracjaModel> dane)
        {
            InitializeComponent();
            _dane = dane;
            
            // Inicjalizuj combo
            var miesiace = new[] { "Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec",
                                   "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień" };
            cmbMiesiac.ItemsSource = miesiace;
            cmbMiesiac.SelectedIndex = DateTime.Now.Month - 1;

            var lata = Enumerable.Range(DateTime.Now.Year - 2, 5).ToList();
            cmbRok.ItemsSource = lata;
            cmbRok.SelectedItem = DateTime.Now.Year;

            _miesiac = DateTime.Now.Month;
            _rok = DateTime.Now.Year;

            Loaded += (s, e) => ObliczStatystyki();
        }

        private void CmbMiesiac_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                _miesiac = cmbMiesiac.SelectedIndex + 1;
                ObliczStatystyki();
            }
        }

        private void CmbRok_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && cmbRok.SelectedItem is int rok)
            {
                _rok = rok;
                ObliczStatystyki();
            }
        }

        private void ObliczStatystyki()
        {
            var miesiace = new[] { "Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec",
                                   "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień" };
            txtOkres.Text = $"{miesiace[_miesiac - 1]} {_rok}";

            var dataOd = new DateTime(_rok, _miesiac, 1);
            var dataDo = dataOd.AddMonths(1);

            var daneMiesiaca = _dane
                .Where(r => r.DataCzas >= dataOd && r.DataCzas < dataDo)
                .ToList();

            // Oblicz podstawowe statystyki
            var pracownicy = daneMiesiaca.Select(r => r.PracownikId).Distinct().Count();
            double sumaGodzin = 0;
            double sumaNadgodzin = 0;
            var godzinyDziennie = new Dictionary<int, double>();
            var frekwencjaDziennie = new Dictionary<int, int>();
            var godzinyDzialy = new Dictionary<string, double>();
            var godzinyPracownik = new Dictionary<int, (string Nazwa, double Godziny)>();

            var byPracownikDzien = daneMiesiaca.GroupBy(r => new { r.PracownikId, r.Pracownik, r.Grupa, Data = r.DataCzas.Date });

            foreach (var pd in byPracownikDzien)
            {
                var wejscia = pd.Where(r => r.TypInt == 1).OrderBy(r => r.DataCzas).ToList();
                var wyjscia = pd.Where(r => r.TypInt == 0).OrderByDescending(r => r.DataCzas).ToList();

                if (wejscia.Any() && wyjscia.Any())
                {
                    var czas = (wyjscia.First().DataCzas - wejscia.First().DataCzas).TotalHours;
                    if (czas > 0 && czas < 24)
                    {
                        sumaGodzin += czas;
                        sumaNadgodzin += Math.Max(0, czas - 8);

                        // Godziny dziennie
                        var dzien = pd.Key.Data.Day;
                        if (!godzinyDziennie.ContainsKey(dzien))
                            godzinyDziennie[dzien] = 0;
                        godzinyDziennie[dzien] += czas;

                        // Frekwencja
                        if (!frekwencjaDziennie.ContainsKey(dzien))
                            frekwencjaDziennie[dzien] = 0;
                        frekwencjaDziennie[dzien]++;

                        // Godziny wg działów
                        var grupa = pd.Key.Grupa ?? "Nieprzypisani";
                        if (!godzinyDzialy.ContainsKey(grupa))
                            godzinyDzialy[grupa] = 0;
                        godzinyDzialy[grupa] += czas;

                        // Godziny pracownika
                        if (!godzinyPracownik.ContainsKey(pd.Key.PracownikId))
                            godzinyPracownik[pd.Key.PracownikId] = (pd.Key.Pracownik, 0);
                        var (nazwa, godz) = godzinyPracownik[pd.Key.PracownikId];
                        godzinyPracownik[pd.Key.PracownikId] = (nazwa, godz + czas);
                    }
                }
            }

            // Aktualizuj karty
            txtSumaGodzin.Text = $"{sumaGodzin:N0}h";
            txtPracownikow.Text = pracownicy.ToString();
            txtSrednia.Text = pracownicy > 0 ? $"{sumaGodzin / pracownicy:N1}h" : "0h";
            txtNadgodziny.Text = $"{sumaNadgodzin:N0}h";
            txtNadgodzinyProcent.Text = sumaGodzin > 0 ? $"{sumaNadgodzin / sumaGodzin * 100:N0}% całości" : "0% całości";
            txtDniRoboczych.Text = godzinyDziennie.Count.ToString();

            // Szacowany koszt
            var stawka = UstawieniaStawekWindow.PobierzStawke("-- DOMYŚLNA --", dataOd);
            var koszt = (decimal)(sumaGodzin - sumaNadgodzin) * stawka.StawkaPodstawowa + 
                        (decimal)sumaNadgodzin * stawka.StawkaNadgodzin;
            txtKoszt.Text = $"{koszt:N0} zł";

            // Rysuj wykresy
            RysujWykresGodzinDziennie(godzinyDziennie, dataOd);
            RysujWykresDzialy(godzinyDzialy);
            RysujWykresFrekwencji(frekwencjaDziennie, dataOd);
            RysujTopPracownikow(godzinyPracownik);
        }

        private void RysujWykresGodzinDziennie(Dictionary<int, double> dane, DateTime dataOd)
        {
            chartGodzinyDziennie.Children.Clear();
            if (!dane.Any()) return;

            var width = chartGodzinyDziennie.ActualWidth > 0 ? chartGodzinyDziennie.ActualWidth : 400;
            var height = chartGodzinyDziennie.ActualHeight > 0 ? chartGodzinyDziennie.ActualHeight : 200;
            var dniWMiesiacu = DateTime.DaysInMonth(dataOd.Year, dataOd.Month);
            var maxWartosc = dane.Values.Max();
            var barWidth = (width - 40) / dniWMiesiacu - 2;

            for (int dzien = 1; dzien <= dniWMiesiacu; dzien++)
            {
                var wartosc = dane.ContainsKey(dzien) ? dane[dzien] : 0;
                var barHeight = maxWartosc > 0 ? (wartosc / maxWartosc) * (height - 40) : 0;

                var rect = new Rectangle
                {
                    Width = Math.Max(barWidth, 3),
                    Height = Math.Max(barHeight, 1),
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3182CE")),
                    RadiusX = 2,
                    RadiusY = 2
                };

                Canvas.SetLeft(rect, 20 + (dzien - 1) * (barWidth + 2));
                Canvas.SetTop(rect, height - 30 - barHeight);
                chartGodzinyDziennie.Children.Add(rect);

                // Etykieta dnia (co 5 dni)
                if (dzien % 5 == 0 || dzien == 1)
                {
                    var label = new TextBlock
                    {
                        Text = dzien.ToString(),
                        FontSize = 9,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#718096"))
                    };
                    Canvas.SetLeft(label, 20 + (dzien - 1) * (barWidth + 2));
                    Canvas.SetTop(label, height - 20);
                    chartGodzinyDziennie.Children.Add(label);
                }
            }
        }

        private void RysujWykresDzialy(Dictionary<string, double> dane)
        {
            if (!dane.Any())
            {
                chartDzialy.ItemsSource = null;
                return;
            }

            var maxWartosc = dane.Values.Max();
            var kolory = new[] { "#3182CE", "#38A169", "#805AD5", "#DD6B20", "#E53E3E", "#319795", "#D69E2E", "#667EEA" };
            var i = 0;

            var items = dane
                .OrderByDescending(x => x.Value)
                .Take(8)
                .Select(x => new
                {
                    Nazwa = x.Key.Length > 15 ? x.Key.Substring(0, 15) + "..." : x.Key,
                    Wartosc = $"{x.Value:N0}h",
                    Szerokosc = maxWartosc > 0 ? (x.Value / maxWartosc) * 200 : 0,
                    Kolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolory[i++ % kolory.Length]))
                })
                .ToList();

            chartDzialy.ItemsSource = items;
        }

        private void RysujWykresFrekwencji(Dictionary<int, int> dane, DateTime dataOd)
        {
            chartFrekwencja.Children.Clear();
            if (!dane.Any()) return;

            var width = chartFrekwencja.ActualWidth > 0 ? chartFrekwencja.ActualWidth : 400;
            var height = chartFrekwencja.ActualHeight > 0 ? chartFrekwencja.ActualHeight : 200;
            var dniWMiesiacu = DateTime.DaysInMonth(dataOd.Year, dataOd.Month);
            var maxWartosc = dane.Values.Max();
            var barWidth = (width - 40) / dniWMiesiacu - 2;

            for (int dzien = 1; dzien <= dniWMiesiacu; dzien++)
            {
                var wartosc = dane.ContainsKey(dzien) ? dane[dzien] : 0;
                var barHeight = maxWartosc > 0 ? ((double)wartosc / maxWartosc) * (height - 40) : 0;

                // Kolor w zależności od dnia tygodnia
                var data = new DateTime(dataOd.Year, dataOd.Month, dzien);
                var kolor = data.DayOfWeek == DayOfWeek.Saturday || data.DayOfWeek == DayOfWeek.Sunday
                    ? "#E53E3E" : "#38A169";

                var rect = new Rectangle
                {
                    Width = Math.Max(barWidth, 3),
                    Height = Math.Max(barHeight, 1),
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolor)),
                    RadiusX = 2,
                    RadiusY = 2
                };

                Canvas.SetLeft(rect, 20 + (dzien - 1) * (barWidth + 2));
                Canvas.SetTop(rect, height - 30 - barHeight);
                chartFrekwencja.Children.Add(rect);
            }
        }

        private void RysujTopPracownikow(Dictionary<int, (string Nazwa, double Godziny)> dane)
        {
            var top = dane
                .OrderByDescending(x => x.Value.Godziny)
                .Take(10)
                .Select((x, i) => new
                {
                    Pozycja = $"#{i + 1}",
                    Pracownik = x.Value.Nazwa,
                    Godziny = $"{x.Value.Godziny:N1}h"
                })
                .ToList();

            listTopPracownicy.ItemsSource = top;
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Export PDF wymaga biblioteki iTextSharp lub PdfSharp.\n\nZainstaluj pakiet NuGet:\nInstall-Package PdfSharp", 
                "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
