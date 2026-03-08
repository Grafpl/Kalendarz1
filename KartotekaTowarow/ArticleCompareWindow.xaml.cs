using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.KartotekaTowarow
{
    public partial class ArticleCompareWindow : Window
    {
        private readonly List<ArticleModel> _articles;

        public ArticleCompareWindow(List<ArticleModel> articles)
        {
            InitializeComponent();
            _articles = articles;
            BuildCompareTable();
        }

        private void BuildCompareTable()
        {
            var grid = CompareGrid;
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();
            grid.Children.Clear();

            int colCount = _articles.Count + 1; // label + articles
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(160) });
            for (int i = 0; i < _articles.Count; i++)
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Define rows
            var rows = new List<(string Label, System.Func<ArticleModel, string> Getter)>
            {
                ("ID", a => a.ID ?? "-"),
                ("Nazwa", a => a.Name ?? "-"),
                ("Skrot nazwy", a => a.ShortName ?? "-"),
                ("JM", a => a.JM ?? "-"),
                ("Cena 1", a => a.Cena1?.ToString("N2") ?? "-"),
                ("Cena 2", a => a.Cena2?.ToString("N2") ?? "-"),
                ("Cena 3", a => a.Cena3?.ToString("N2") ?? "-"),
                ("Grupa", a => a.Grupa?.ToString() ?? "-"),
                ("Rodzaj", a => a.Rodzaj switch { 0 => "Mieso", 1 => "Podroby", 2 => "Odpady", _ => a.Rodzaj?.ToString() ?? "-" }),
                ("WRC", a => a.WRC?.ToString("N2") ?? "-"),
                ("Wydajnosc", a => a.Wydajnosc?.ToString("N1") ?? "-"),
                ("Przelicznik", a => a.Przelicznik?.ToString("N2") ?? "-"),
                ("Wstrzymany", a => a.Halt == 1 ? "TAK" : "NIE"),
                ("Standard", a => a.isStandard == 1 ? "TAK" : "NIE"),
                ("Waga std", a => a.StandardWeight?.ToString("N2") ?? "-"),
                ("Tol. +", a => a.StandardTol?.ToString("N2") ?? "-"),
                ("Tol. -", a => a.StandardTolMinus?.ToString("N2") ?? "-"),
                ("Termin (dni)", a => a.Duration?.ToString() ?? "-"),
                ("Temperatura", a => a.TempOfStorage ?? "-"),
                ("Sklad", a => string.Join(", ", new[] { a.Ingredients1, a.Ingredients2, a.Ingredients3, a.Ingredients4 }
                    .Where(s => !string.IsNullOrWhiteSpace(s)))),
                ("Powiazany 1", a => a.RELATED_ID1 ?? "-"),
                ("Powiazany 2", a => a.RELATED_ID2 ?? "-"),
                ("Powiazany 3", a => a.RELATED_ID3 ?? "-"),
                ("Utworzony", a => $"{a.CreateData} {a.CreateGodzina}".Trim()),
                ("Zmodyfikowany", a => $"{a.ModificationData} {a.ModificationGodzina}".Trim()),
            };

            int totalRows = rows.Count + 1; // header + data rows
            for (int r = 0; r < totalRows; r++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header row
            AddCell(grid, 0, 0, "Pole", true, null);
            for (int c = 0; c < _articles.Count; c++)
                AddCell(grid, 0, c + 1, $"{_articles[c].ID} - {_articles[c].Name}", true, null);

            // Data rows
            int diffCount = 0;
            for (int r = 0; r < rows.Count; r++)
            {
                var (label, getter) = rows[r];
                var values = _articles.Select(getter).ToList();
                bool isDiff = values.Distinct().Count() > 1;
                if (isDiff) diffCount++;

                var bgColor = isDiff ? Color.FromRgb(0xFD, 0xED, 0xEC) :
                              (r % 2 == 0 ? Colors.White : Color.FromRgb(0xF8, 0xF9, 0xFA));

                AddCell(grid, r + 1, 0, label, false, bgColor, true);
                for (int c = 0; c < values.Count; c++)
                {
                    var fg = isDiff ? Color.FromRgb(0xC0, 0x39, 0x2B) : Color.FromRgb(0x2C, 0x3E, 0x50);
                    AddCell(grid, r + 1, c + 1, values[c], false, bgColor, false, fg);
                }
            }

            TxtDiffCount.Text = diffCount > 0 ? $"Roznice w {diffCount} polach" : "Brak roznic";
        }

        private static void AddCell(Grid grid, int row, int col, string text, bool isHeader,
            Color? bgColor, bool isLabel = false, Color? fgColor = null)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xEC, 0xF0, 0xF1)),
                BorderThickness = new Thickness(0, 0, 1, 1)
            };

            if (isHeader)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F));
                var tb = new TextBlock
                {
                    Text = text,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    Padding = new Thickness(10, 8, 10, 8),
                    TextWrapping = TextWrapping.Wrap
                };
                border.Child = tb;
            }
            else
            {
                border.Background = bgColor.HasValue ? new SolidColorBrush(bgColor.Value) : Brushes.White;
                var tb = new TextBlock
                {
                    Text = text,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11,
                    FontWeight = isLabel ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = isLabel ? new SolidColorBrush(Color.FromRgb(0x7F, 0x8C, 0x8D))
                                         : new SolidColorBrush(fgColor ?? Color.FromRgb(0x2C, 0x3E, 0x50)),
                    Padding = new Thickness(10, 6, 10, 6),
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                };
                border.Child = tb;
            }

            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            grid.Children.Add(border);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
