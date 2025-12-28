using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Kalendarz1.WPF.Controls
{
    public partial class SimplePieChart : UserControl
    {
        private static readonly Color[] DefaultColors = new[]
        {
            Color.FromRgb(231, 76, 60),   // Czerwony
            Color.FromRgb(243, 156, 18),  // Pomarańczowy
            Color.FromRgb(155, 89, 182),  // Fioletowy
            Color.FromRgb(52, 152, 219),  // Niebieski
            Color.FromRgb(39, 174, 96),   // Zielony
            Color.FromRgb(26, 188, 156),  // Turkusowy
            Color.FromRgb(52, 73, 94),    // Ciemnoszary
            Color.FromRgb(241, 196, 15),  // Żółty
            Color.FromRgb(230, 126, 34),  // Marchewkowy
            Color.FromRgb(149, 165, 166), // Szary
        };

        public SimplePieChart()
        {
            InitializeComponent();
        }

        #region Dependency Properties

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(SimplePieChart),
                new PropertyMetadata(""));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        #endregion

        #region Public Methods

        public void SetData(IEnumerable<PieChartItem> items)
        {
            chartCanvas.Children.Clear();
            legendPanel.Children.Clear();

            var itemsList = items.ToList();
            if (!itemsList.Any()) return;

            var total = itemsList.Sum(x => x.Value);
            if (total <= 0) return;

            double centerX = 100;
            double centerY = 100;
            double radius = 90;
            double startAngle = -90; // Zacznij od góry

            int colorIndex = 0;
            foreach (var item in itemsList)
            {
                double sweepAngle = (item.Value / total) * 360;

                // Użyj koloru z elementu lub domyślnego
                var color = item.Color ?? DefaultColors[colorIndex % DefaultColors.Length];
                var brush = new SolidColorBrush(color);

                // Rysuj wycinek
                if (sweepAngle > 0)
                {
                    var slice = CreatePieSlice(centerX, centerY, radius, startAngle, sweepAngle, brush);
                    chartCanvas.Children.Add(slice);
                }

                // Dodaj legendę
                AddLegendItem(item.Label, item.Value, total, brush);

                startAngle += sweepAngle;
                colorIndex++;
            }
        }

        public void Clear()
        {
            chartCanvas.Children.Clear();
            legendPanel.Children.Clear();
        }

        #endregion

        #region Private Methods

        private Path CreatePieSlice(double cx, double cy, double radius, double startAngle, double sweepAngle, Brush fill)
        {
            // Konwersja kątów na radiany
            double startRad = startAngle * Math.PI / 180;
            double endRad = (startAngle + sweepAngle) * Math.PI / 180;

            // Punkty łuku
            double x1 = cx + radius * Math.Cos(startRad);
            double y1 = cy + radius * Math.Sin(startRad);
            double x2 = cx + radius * Math.Cos(endRad);
            double y2 = cy + radius * Math.Sin(endRad);

            // Czy łuk jest większy niż 180 stopni?
            bool largeArc = sweepAngle > 180;

            // Tworzenie ścieżki
            var figure = new PathFigure
            {
                StartPoint = new Point(cx, cy),
                IsClosed = true
            };

            // Linia do początku łuku
            figure.Segments.Add(new LineSegment(new Point(x1, y1), true));

            // Łuk
            figure.Segments.Add(new ArcSegment(
                new Point(x2, y2),
                new Size(radius, radius),
                0,
                largeArc,
                SweepDirection.Clockwise,
                true));

            // Linia powrotna do środka
            figure.Segments.Add(new LineSegment(new Point(cx, cy), true));

            var geometry = new PathGeometry(new[] { figure });

            var path = new Path
            {
                Fill = fill,
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Data = geometry
            };

            // Tooltip
            path.ToolTip = $"{((PieChartItem)path.Tag)?.Label}: {((PieChartItem)path.Tag)?.Value:N0}";

            return path;
        }

        private void AddLegendItem(string label, double value, double total, Brush color)
        {
            var percent = (value / total) * 100;

            var item = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 3)
            };

            // Kolorowy kwadrat
            var colorBox = new Border
            {
                Width = 14,
                Height = 14,
                Background = color,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            item.Children.Add(colorBox);

            // Tekst
            var textPanel = new StackPanel();

            var labelText = new TextBlock
            {
                Text = TruncateLabel(label, 18),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            };
            textPanel.Children.Add(labelText);

            var valueText = new TextBlock
            {
                Text = $"{value:N0} ({percent:F1}%)",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141))
            };
            textPanel.Children.Add(valueText);

            item.Children.Add(textPanel);

            legendPanel.Children.Add(item);
        }

        private string TruncateLabel(string label, int maxLength)
        {
            if (string.IsNullOrEmpty(label)) return "";
            if (label.Length <= maxLength) return label;
            return label.Substring(0, maxLength - 3) + "...";
        }

        #endregion
    }

    #region Models

    public class PieChartItem
    {
        public string Label { get; set; } = "";
        public double Value { get; set; }
        public Color? Color { get; set; }

        public PieChartItem() { }

        public PieChartItem(string label, double value, Color? color = null)
        {
            Label = label;
            Value = value;
            Color = color;
        }
    }

    #endregion
}
