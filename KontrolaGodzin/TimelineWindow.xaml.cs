using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Kalendarz1.KontrolaGodzin
{
    public partial class TimelineWindow : Window
    {
        private List<RejestracjaModel> _dane;
        private DateTime _wybranaData;

        public TimelineWindow(List<RejestracjaModel> dane)
        {
            InitializeComponent();
            _dane = dane;
            _wybranaData = DateTime.Today;
            dpData.SelectedDate = _wybranaData;

            Loaded += (s, e) =>
            {
                RysujSkale();
                OdswiezTimeline();
            };

            SizeChanged += (s, e) =>
            {
                RysujSkale();
                OdswiezTimeline();
            };
        }

        private void DpData_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpData.SelectedDate.HasValue)
            {
                _wybranaData = dpData.SelectedDate.Value;
                OdswiezTimeline();
            }
        }

        private void BtnPoprzedni_Click(object sender, RoutedEventArgs e)
        {
            _wybranaData = _wybranaData.AddDays(-1);
            dpData.SelectedDate = _wybranaData;
        }

        private void BtnNastepny_Click(object sender, RoutedEventArgs e)
        {
            _wybranaData = _wybranaData.AddDays(1);
            dpData.SelectedDate = _wybranaData;
        }

        private void BtnDzis_Click(object sender, RoutedEventArgs e)
        {
            _wybranaData = DateTime.Today;
            dpData.SelectedDate = _wybranaData;
        }

        private void RysujSkale()
        {
            canvasSkala.Children.Clear();
            var width = canvasSkala.ActualWidth > 0 ? canvasSkala.ActualWidth : 1000;
            var godzinWidth = width / 24;

            for (int h = 0; h <= 24; h++)
            {
                var x = h * godzinWidth;

                // Linia
                var line = new Line
                {
                    X1 = x,
                    Y1 = 15,
                    X2 = x,
                    Y2 = 25,
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E0")),
                    StrokeThickness = 1
                };
                canvasSkala.Children.Add(line);

                // Etykieta (co 2 godziny)
                if (h % 2 == 0 && h < 24)
                {
                    var label = new TextBlock
                    {
                        Text = $"{h:D2}:00",
                        FontSize = 10,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568"))
                    };
                    Canvas.SetLeft(label, x + 2);
                    Canvas.SetTop(label, 0);
                    canvasSkala.Children.Add(label);
                }
            }
        }

        private void OdswiezTimeline()
        {
            txtData.Text = $"{_wybranaData:dd MMMM yyyy} ({_wybranaData:dddd})";

            var daneDnia = _dane
                .Where(r => r.DataCzas.Date == _wybranaData.Date)
                .GroupBy(r => r.PracownikId)
                .Select(g =>
                {
                    var rejestracje = g.OrderBy(r => r.DataCzas).ToList();
                    var wejscia = rejestracje.Where(r => r.TypInt == 1).ToList();
                    var wyjscia = rejestracje.Where(r => r.TypInt == 0).ToList();

                    return new TimelineItem
                    {
                        PracownikId = g.Key,
                        Pracownik = g.First().Pracownik,
                        Grupa = g.First().Grupa,
                        Rejestracje = rejestracje,
                        PierwszeWejscie = wejscia.FirstOrDefault()?.DataCzas,
                        OstatnieWyjscie = wyjscia.LastOrDefault()?.DataCzas,
                        MaWejscie = wejscia.Any(),
                        MaWyjscie = wyjscia.Any()
                    };
                })
                .OrderBy(x => x.PierwszeWejscie ?? DateTime.MaxValue)
                .ThenBy(x => x.Pracownik)
                .ToList();

            listPracownicy.ItemsSource = daneDnia;
            RysujPaskiCzasowe(daneDnia);
        }

        private void RysujPaskiCzasowe(List<TimelineItem> dane)
        {
            // Tworzymy własną listę pasków
            var items = new List<TimelineBar>();
            var width = canvasSkala.ActualWidth > 0 ? canvasSkala.ActualWidth : 1000;
            var godzinWidth = width / 24;

            foreach (var pracownik in dane)
            {
                var bar = new TimelineBar
                {
                    Segmenty = new List<TimelineSegment>()
                };

                if (pracownik.PierwszeWejscie.HasValue)
                {
                    var start = pracownik.PierwszeWejscie.Value;
                    var koniec = pracownik.OstatnieWyjscie ?? (_wybranaData == DateTime.Today ? DateTime.Now : _wybranaData.AddHours(23).AddMinutes(59));

                    // Główny segment obecności
                    var startX = (start.Hour + start.Minute / 60.0) * godzinWidth;
                    var koniecX = (koniec.Hour + koniec.Minute / 60.0) * godzinWidth;
                    var szerokosc = Math.Max(koniecX - startX, 5);

                    // Kolor w zależności od statusu
                    string kolor;
                    if (!pracownik.MaWyjscie && _wybranaData < DateTime.Today)
                        kolor = "#E53E3E"; // Czerwony - brak wyjścia
                    else if (!pracownik.MaWyjscie && _wybranaData == DateTime.Today)
                        kolor = "#38A169"; // Zielony - nadal obecny
                    else
                        kolor = "#38A169"; // Zielony - normalnie

                    // Sprawdź nadgodziny (>8h)
                    var czasPracy = (koniec - start).TotalHours;
                    if (czasPracy > 8 && pracownik.MaWyjscie)
                    {
                        // Normalne godziny (0-8h)
                        var normalneKoniecX = startX + 8 * godzinWidth;
                        bar.Segmenty.Add(new TimelineSegment
                        {
                            X = startX,
                            Szerokosc = Math.Min(8 * godzinWidth, szerokosc),
                            Kolor = "#38A169"
                        });

                        // Nadgodziny
                        if (szerokosc > 8 * godzinWidth)
                        {
                            bar.Segmenty.Add(new TimelineSegment
                            {
                                X = normalneKoniecX,
                                Szerokosc = szerokosc - 8 * godzinWidth,
                                Kolor = "#805AD5"
                            });
                        }
                    }
                    else
                    {
                        bar.Segmenty.Add(new TimelineSegment
                        {
                            X = startX,
                            Szerokosc = szerokosc,
                            Kolor = kolor
                        });
                    }

                    // Godziny nocne (22:00-06:00)
                    if (start.Hour < 6)
                    {
                        var nocneKoniecX = Math.Min(6 * godzinWidth, koniecX);
                        bar.Segmenty.Insert(0, new TimelineSegment
                        {
                            X = startX,
                            Szerokosc = nocneKoniecX - startX,
                            Kolor = "#3182CE"
                        });
                    }

                    // Tooltip
                    bar.Tooltip = $"{pracownik.Pracownik}\n" +
                                  $"Wejście: {start:HH:mm}\n" +
                                  $"Wyjście: {(pracownik.MaWyjscie ? koniec.ToString("HH:mm") : "brak")}\n" +
                                  $"Czas: {czasPracy:N1}h";
                }

                items.Add(bar);
            }

            // Renderuj paski
            RenderujPaski(items);
        }

        private void RenderujPaski(List<TimelineBar> paski)
        {
            var container = new StackPanel();

            foreach (var bar in paski)
            {
                var border = new Border
                {
                    Height = 48,
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                    BorderThickness = new Thickness(0, 0, 0, 1)
                };

                var canvas = new Canvas
                {
                    Background = Brushes.Transparent,
                    Margin = new Thickness(0, 8, 0, 8)
                };

                foreach (var segment in bar.Segmenty)
                {
                    var rect = new Border
                    {
                        Width = segment.Szerokosc,
                        Height = 28,
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(segment.Kolor)),
                        CornerRadius = new CornerRadius(4),
                        ToolTip = bar.Tooltip
                    };

                    Canvas.SetLeft(rect, segment.X);
                    Canvas.SetTop(rect, 2);
                    canvas.Children.Add(rect);
                }

                // Linie godzinowe (tło)
                var width = canvasSkala.ActualWidth > 0 ? canvasSkala.ActualWidth : 1000;
                var godzinWidth = width / 24;
                for (int h = 0; h <= 24; h += 6)
                {
                    var line = new Line
                    {
                        X1 = h * godzinWidth,
                        Y1 = 0,
                        X2 = h * godzinWidth,
                        Y2 = 32,
                        Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 2, 2 }
                    };
                    canvas.Children.Add(line);
                }

                border.Child = canvas;
                container.Children.Add(border);
            }

            listTimeline.ItemsSource = null;
            listTimeline.Items.Clear();

            // Bezpośrednio dodajemy
            var scrollContent = (ScrollViewer)((Grid)Content).Children[2];
            var grid = (Grid)scrollContent.Content;
            grid.Children.RemoveAt(1);
            
            var newBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFBFC"))
            };
            newBorder.Child = container;
            Grid.SetColumn(newBorder, 1);
            grid.Children.Add(newBorder);
        }
    }

    #region Timeline Models

    public class TimelineItem
    {
        public int PracownikId { get; set; }
        public string Pracownik { get; set; }
        public string Grupa { get; set; }
        public List<RejestracjaModel> Rejestracje { get; set; }
        public DateTime? PierwszeWejscie { get; set; }
        public DateTime? OstatnieWyjscie { get; set; }
        public bool MaWejscie { get; set; }
        public bool MaWyjscie { get; set; }
    }

    public class TimelineBar
    {
        public List<TimelineSegment> Segmenty { get; set; }
        public string Tooltip { get; set; }
    }

    public class TimelineSegment
    {
        public double X { get; set; }
        public double Szerokosc { get; set; }
        public string Kolor { get; set; }
    }

    #endregion
}
