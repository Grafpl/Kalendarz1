using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kalendarz1.Zamowienia
{
    /// <summary>
    /// Okno wyboru towaru do filtrowania Podsumowania dnia.
    /// Pokazuje wszystkie towary (kafelki ze zdjęciami), w sekcjach: Zamówione dziś / Pozostałe.
    /// Klik w towar → ustawia wynik i zamyka okno. Klik "Wszystkie" → wynik = null (zdejmij filtr).
    /// </summary>
    public partial class WyborTowaruWindow : Window
    {
        public int? WybranyTowarId { get; private set; }
        public bool Wybrano { get; private set; }

        private readonly List<(int? id, string name, BitmapImage? image, bool inToday)> _wszystkie;

        public WyborTowaruWindow(List<(int? id, string name, BitmapImage? image, bool inToday)> towary, int? aktualny)
        {
            InitializeComponent();
            // Pomijamy pozycję "Wszystkie" z listy (jest osobnym przyciskiem)
            _wszystkie = towary.Where(t => t.id.HasValue).ToList();
            WybranyTowarId = aktualny;
            Loaded += (_, _) => { Render(""); txtSzukaj.Focus(); };
        }

        private void Render(string filtr)
        {
            spTowary.Children.Clear();
            filtr = (filtr ?? "").Trim();

            IEnumerable<(int? id, string name, BitmapImage? image, bool inToday)> baza = _wszystkie;
            if (filtr.Length > 0)
                baza = baza.Where(t => (t.name ?? "").Contains(filtr, StringComparison.OrdinalIgnoreCase));

            var dzis = baza.Where(t => t.inToday).OrderBy(t => t.name).ToList();
            var reszta = baza.Where(t => !t.inToday).OrderBy(t => t.name).ToList();

            if (dzis.Count > 0)
            {
                spTowary.Children.Add(Naglowek($"▼ ZAMÓWIONE DZIŚ ({dzis.Count})", "#27AE60"));
                foreach (var t in dzis) spTowary.Children.Add(Kafelek(t, true));
            }
            if (reszta.Count > 0)
            {
                spTowary.Children.Add(Naglowek($"▼ POZOSTAŁE ({reszta.Count})", "#95A5A6"));
                foreach (var t in reszta) spTowary.Children.Add(Kafelek(t, false));
            }
            if (dzis.Count == 0 && reszta.Count == 0)
            {
                spTowary.Children.Add(new TextBlock
                {
                    Text = $"Brak wyników dla \"{filtr}\"",
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(6, 10, 0, 0)
                });
            }
        }

        private static TextBlock Naglowek(string tekst, string hex) => new()
        {
            Text = tekst,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!,
            Margin = new Thickness(4, 10, 0, 4)
        };

        private Border Kafelek((int? id, string name, BitmapImage? image, bool inToday) t, bool dzis)
        {
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = t.id == WybranyTowarId
                    ? new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71))
                    : new SolidColorBrush(Color.FromRgb(0xE0, 0xE4, 0xE8)),
                BorderThickness = new Thickness(t.id == WybranyTowarId ? 2 : 1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Zdjęcie
            Border imgBox;
            if (t.image != null)
            {
                var im = new System.Windows.Controls.Image { Source = t.image, Width = 34, Height = 34, Stretch = Stretch.Uniform };
                RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
                imgBox = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = im };
            }
            else
            {
                imgBox = new Border
                {
                    Width = 36, Height = 36, CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xF0, 0xF1)),
                    Child = new TextBlock { Text = "📦", FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
            }
            imgBox.Margin = new Thickness(0, 0, 10, 0);
            imgBox.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(imgBox, 0);
            grid.Children.Add(imgBox);

            // Nazwa
            var nazwa = new TextBlock
            {
                Text = t.name,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nazwa, 1);
            grid.Children.Add(nazwa);

            if (dzis)
            {
                var dot = new TextBlock
                {
                    Text = "● dziś",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(dot, 2);
                grid.Children.Add(dot);
            }

            border.Child = grid;
            border.MouseEnter += (s, e) => { if (t.id != WybranyTowarId) border.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF7, 0xF2)); };
            border.MouseLeave += (s, e) => border.Background = Brushes.White;
            border.MouseLeftButtonUp += (s, e) =>
            {
                WybranyTowarId = t.id;
                Wybrano = true;
                DialogResult = true;
                Close();
            };
            return border;
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e) => Render(txtSzukaj.Text);

        private void BtnWszystkie_Click(object sender, RoutedEventArgs e)
        {
            WybranyTowarId = null;
            Wybrano = true;
            DialogResult = true;
            Close();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Wybrano = false;
            DialogResult = false;
            Close();
        }
    }
}
