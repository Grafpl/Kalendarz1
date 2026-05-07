using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kalendarz1.CentrumNagranAI.Services;

namespace Kalendarz1.CentrumNagranAI.Views
{
    public partial class HeatmapWindow : Window
    {
        public HeatmapWindow()
        {
            InitializeComponent();
            Render();
        }

        private void Render()
        {
            HeatmapGrid.Children.Clear();
            HeatmapGrid.RowDefinitions.Clear();
            HeatmapGrid.ColumnDefinitions.Clear();

            var data = ActivityService.GetHeatmap(7);
            CnaConfig.ZaladujJesliTrzeba();
            var cameras = CnaConfig.Kamery.Select(k => k.Id).Distinct().ToList();

            // Layout: 1 kolumna nazw + 24 kolumny godzin
            HeatmapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            for (int h = 0; h < 24; h++)
                HeatmapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

            // Wiersz nagłówków godzin
            HeatmapGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            for (int h = 0; h < 24; h++)
            {
                var label = new TextBlock
                {
                    Text = h.ToString("D2"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(label, 0);
                Grid.SetColumn(label, h + 1);
                HeatmapGrid.Children.Add(label);
            }

            // Znajdź max activity dla normalizacji koloru
            float maxAct = data.Count == 0 ? 1f : data.Values.Max();
            if (maxAct < 0.001f) maxAct = 0.001f;

            int row = 1;
            float totalCells = 0; int filledCells = 0;
            foreach (var cam in cameras)
            {
                HeatmapGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });

                var nameBlock = new TextBlock
                {
                    Text = CnaConfig.DisplayName(cam),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                    FontSize = 12,
                    Padding = new Thickness(8, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetRow(nameBlock, row);
                Grid.SetColumn(nameBlock, 0);
                HeatmapGrid.Children.Add(nameBlock);

                for (int h = 0; h < 24; h++)
                {
                    bool has = data.TryGetValue((cam, h), out var act);
                    var brush = has ? ActivityToColor(act, maxAct) : (Brush)new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                    var cell = new Border
                    {
                        Background = brush,
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)),
                        BorderThickness = new Thickness(1),
                        ToolTip = has
                            ? $"{CnaConfig.DisplayName(cam)} • godz {h:D2}: aktywność {act:F3}"
                            : $"{CnaConfig.DisplayName(cam)} • godz {h:D2}: brak danych"
                    };
                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, h + 1);
                    HeatmapGrid.Children.Add(cell);
                    if (has) { totalCells += act; filledCells++; }
                }
                row++;
            }

            LegendText.Text = filledCells == 0
                ? "Brak danych aktywności. Zaczekaj aż backfill zindeksuje klatki + przebuduje aktywność."
                : $"Komórek z danymi: {filledCells}, średnia aktywność: {totalCells / filledCells:F3}, max: {maxAct:F3}";
        }

        // Mapowanie 0..max → kolor: ciemny granat → niebieski → fiolet → magenta → żółty
        private static SolidColorBrush ActivityToColor(float v, float max)
        {
            float t = Math.Clamp(v / max, 0f, 1f);
            // Prosta interpolacja w 4 segmentach
            byte r, g, b;
            if (t < 0.25f)      { float k = t / 0.25f;        r = (byte)(20 + k * 40);  g = (byte)(30 + k * 60);  b = (byte)(50 + k * 150); }
            else if (t < 0.50f) { float k = (t - 0.25f) / 0.25f; r = (byte)(60 + k * 140); g = (byte)(90 - k * 60);  b = (byte)(200 - k * 50); }
            else if (t < 0.75f) { float k = (t - 0.50f) / 0.25f; r = (byte)(200 + k * 55); g = (byte)(30 + k * 50);  b = (byte)(150 - k * 100); }
            else                { float k = (t - 0.75f) / 0.25f; r = 255;                  g = (byte)(80 + k * 175); b = (byte)(50 - k * 50); }
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => Render();
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
