using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kalendarz1.WPF.Helpers
{
    /// <summary>
    /// Dane produktu do wyświetlenia w oknie szczegółów.
    /// Publiczna klasa umożliwiająca użycie z różnych miejsc aplikacji.
    /// </summary>
    public class ProductDetailData
    {
        public int Id { get; set; }
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public decimal Plan { get; set; }
        public decimal Fakt { get; set; }
        public decimal Stan { get; set; }
        public decimal Zamowienia { get; set; }
        public decimal Wydania { get; set; }
        public decimal Bilans { get; set; }
        public bool UzytoFakt => Fakt > 0;
        public DateTime Data { get; set; } = DateTime.Today;

        /// <summary>
        /// Lista odbiorców z zamówieniami na ten produkt
        /// </summary>
        public List<OdbiorcaInfo> Odbiorcy { get; set; } = new();
    }

    /// <summary>
    /// Informacja o odbiorcy dla produktu
    /// </summary>
    public class OdbiorcaInfo
    {
        public int ZamowienieId { get; set; }
        public int KlientId { get; set; }
        public string NazwaOdbiorcy { get; set; } = "";
        public decimal Zamowione { get; set; }
        public decimal Wydane { get; set; }
        public decimal ProcentUdzial { get; set; }
    }

    /// <summary>
    /// Helper do wyświetlania okna szczegółów produktu.
    /// Pozwala na współdzielenie funkcjonalności między różnymi oknami (Dashboard, Podsumowanie tygodniowe).
    /// </summary>
    public static class ProductDetailWindowHelper
    {
        /// <summary>
        /// Wyświetla powiększone okno szczegółów produktu - widok prezentacyjny.
        /// </summary>
        /// <param name="data">Dane produktu do wyświetlenia</param>
        /// <param name="useReleases">Czy używać wydań (true) czy zamówień (false) do bilansu</param>
        /// <param name="productImage">Opcjonalne zdjęcie produktu</param>
        public static void ShowExpandedProductCard(ProductDetailData data, bool useReleases = false, BitmapImage productImage = null)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            // Stan widoku
            bool viewUseWydania = useReleases;

            var dialog = new Window
            {
                Title = $"[{data.Kod}] {data.Nazwa}",
                WindowState = WindowState.Maximized,
                WindowStyle = WindowStyle.None,
                Background = new SolidColorBrush(Color.FromRgb(20, 25, 30)),
                ResizeMode = ResizeMode.NoResize
            };

            // Kontener na dynamiczną treść
            var mainContainer = new Grid();
            dialog.Content = mainContainer;

            // Metoda odświeżająca zawartość
            Action refreshContent = null;
            refreshContent = () =>
            {
                // Obliczenia
                bool uzyjFakt = data.Fakt > 0;
                decimal cel = uzyjFakt ? data.Fakt : data.Plan;
                decimal zamLubWyd = viewUseWydania ? data.Wydania : data.Zamowienia;
                decimal bilans = cel + data.Stan - zamLubWyd;
                decimal procentRealizacji = cel > 0 ? (zamLubWyd / cel) * 100 : 0;
                bool przekroczono = procentRealizacji > 100;

                // Wyczyść i przebuduj
                mainContainer.Children.Clear();

                // === GŁÓWNY KONTENER ===
                var mainGrid = new Grid { Margin = new Thickness(60) };
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // === NAGŁÓWEK ===
                var headerPanel = new Grid { Margin = new Thickness(0, 0, 0, 30) };
                headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Zdjęcie produktu
                var imageBorder = new Border
                {
                    Width = 120, Height = 120,
                    CornerRadius = new CornerRadius(15),
                    Background = productImage != null
                        ? (Brush)new ImageBrush { ImageSource = productImage, Stretch = Stretch.UniformToFill }
                        : new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                    Margin = new Thickness(0, 0, 30, 0)
                };
                if (productImage == null)
                {
                    imageBorder.Child = new TextBlock
                    {
                        Text = "📦",
                        FontSize = 50,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166))
                    };
                }
                Grid.SetColumn(imageBorder, 0);
                headerPanel.Children.Add(imageBorder);

                // Nazwa produktu i data
                var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                titleStack.Children.Add(new TextBlock
                {
                    Text = data.Nazwa,
                    FontSize = 48,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                });
                titleStack.Children.Add(new TextBlock
                {
                    Text = data.Data.ToString("dddd, d MMMM yyyy", new CultureInfo("pl-PL")),
                    FontSize = 20,
                    Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                    Margin = new Thickness(0, 5, 0, 0)
                });
                Grid.SetColumn(titleStack, 1);
                headerPanel.Children.Add(titleStack);

                // BILANS - duży wyświetlacz
                var bilansBorder = new Border
                {
                    Background = new SolidColorBrush(bilans >= 0 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(231, 76, 60)),
                    CornerRadius = new CornerRadius(20),
                    Padding = new Thickness(40, 20, 40, 20),
                    Margin = new Thickness(30, 0, 30, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var bilansStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                bilansStack.Children.Add(new TextBlock
                {
                    Text = "BILANS",
                    FontSize = 18,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.9
                });
                bilansStack.Children.Add(new TextBlock
                {
                    Text = $"{bilans:N0} kg",
                    FontSize = 56,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                bilansBorder.Child = bilansStack;
                Grid.SetColumn(bilansBorder, 2);
                headerPanel.Children.Add(bilansBorder);

                // Przycisk zamknij
                var closeBtn = new Button
                {
                    Content = "✕",
                    FontSize = 32,
                    Width = 60,
                    Height = 60,
                    Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                closeBtn.Click += (s, e) => dialog.Close();
                Grid.SetColumn(closeBtn, 3);
                headerPanel.Children.Add(closeBtn);
                Grid.SetRow(headerPanel, 0);
                mainGrid.Children.Add(headerPanel);

                // === GŁÓWNA SEKCJA - FORMUŁA I DANE ===
                var contentPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                // Panel kontroli
                var controlPanel = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 40, 50)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(25, 18, 25, 18),
                    Margin = new Thickness(0, 0, 0, 20),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var controlStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                controlStack.Children.Add(new TextBlock
                {
                    Text = "ROZLICZENIE: ",
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 20, 0)
                });

                var radioZam = new RadioButton
                {
                    Content = "ZAMÓWIENIA",
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(230, 126, 34)),
                    IsChecked = !viewUseWydania,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 30, 0)
                };
                var radioWyd = new RadioButton
                {
                    Content = "WYDANIA",
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43)),
                    IsChecked = viewUseWydania,
                    VerticalAlignment = VerticalAlignment.Center
                };

                radioZam.Checked += (s, e) => { viewUseWydania = false; refreshContent(); };
                radioWyd.Checked += (s, e) => { viewUseWydania = true; refreshContent(); };

                controlStack.Children.Add(radioZam);
                controlStack.Children.Add(radioWyd);
                controlPanel.Child = controlStack;
                contentPanel.Children.Add(controlPanel);

                // Formuła bilansu
                var formulaBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                    CornerRadius = new CornerRadius(15),
                    Padding = new Thickness(40, 25, 40, 25),
                    Margin = new Thickness(0, 0, 0, 40),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var formulaPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

                if (uzyjFakt)
                {
                    formulaPanel.Children.Add(new TextBlock
                    {
                        Text = $"PLAN {data.Plan:N0}",
                        FontSize = 28,
                        Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                        TextDecorations = TextDecorations.Strikethrough,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 20, 0)
                    });
                    formulaPanel.Children.Add(new TextBlock
                    {
                        Text = $"FAKT {data.Fakt:N0}",
                        FontSize = 36,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(155, 89, 182)),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
                else
                {
                    formulaPanel.Children.Add(new TextBlock
                    {
                        Text = $"PLAN {data.Plan:N0}",
                        FontSize = 36,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }

                formulaPanel.Children.Add(new TextBlock { Text = "  +  ", FontSize = 36, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
                formulaPanel.Children.Add(new TextBlock
                {
                    Text = $"STAN {data.Stan:N0}",
                    FontSize = 36,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(26, 188, 156)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                formulaPanel.Children.Add(new TextBlock { Text = "  −  ", FontSize = 36, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });

                string zamWydLabel = viewUseWydania ? "WYD" : "ZAM";
                formulaPanel.Children.Add(new TextBlock
                {
                    Text = $"{zamWydLabel} {zamLubWyd:N0}",
                    FontSize = 36,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(viewUseWydania ? Color.FromRgb(192, 57, 43) : Color.FromRgb(230, 126, 34)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                formulaPanel.Children.Add(new TextBlock { Text = "  =  ", FontSize = 36, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
                formulaPanel.Children.Add(new TextBlock
                {
                    Text = $"{bilans:N0}",
                    FontSize = 44,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(bilans >= 0 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(231, 76, 60)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                formulaBorder.Child = formulaPanel;
                contentPanel.Children.Add(formulaBorder);

                // Pasek postępu realizacji
                var progressBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(39, 55, 70)),
                    CornerRadius = new CornerRadius(15),
                    Padding = new Thickness(40, 30, 40, 30),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MinWidth = 800
                };
                var progressStack = new StackPanel();

                // Label
                var progressHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
                progressHeader.Children.Add(new TextBlock
                {
                    Text = "REALIZACJA: ",
                    FontSize = 24,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                });

                Color progressColor = przekroczono ? Color.FromRgb(155, 89, 182)
                    : procentRealizacji >= 90 ? Color.FromRgb(39, 174, 96)
                    : procentRealizacji >= 70 ? Color.FromRgb(241, 196, 15)
                    : Color.FromRgb(231, 76, 60);

                progressHeader.Children.Add(new TextBlock
                {
                    Text = $"{procentRealizacji:N1}%",
                    FontSize = 32,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(progressColor),
                    VerticalAlignment = VerticalAlignment.Center
                });
                progressStack.Children.Add(progressHeader);

                // Pasek
                var barContainer = new Grid { Height = 40 };
                barContainer.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                    CornerRadius = new CornerRadius(10)
                });

                double displayPct = Math.Min((double)procentRealizacji, 140) / 140 * 100;
                var progressBar = new Border
                {
                    Background = new SolidColorBrush(progressColor),
                    CornerRadius = new CornerRadius(10),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = displayPct * 7 // Scale to ~700px max
                };
                barContainer.Children.Add(progressBar);

                // Marker 100%
                var marker100 = new Border
                {
                    Width = 3,
                    Background = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(500, 0, 0, 0) // ~71% of 700
                };
                barContainer.Children.Add(marker100);
                progressStack.Children.Add(barContainer);

                // Legenda
                var legendPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 15, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                legendPanel.Children.Add(new TextBlock
                {
                    Text = $"CEL: {cel:N0} kg",
                    FontSize = 18,
                    Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                    Margin = new Thickness(0, 0, 40, 0)
                });
                legendPanel.Children.Add(new TextBlock
                {
                    Text = $"WYKONANO: {zamLubWyd:N0} kg",
                    FontSize = 18,
                    Foreground = new SolidColorBrush(progressColor)
                });
                progressStack.Children.Add(legendPanel);

                progressBorder.Child = progressStack;
                contentPanel.Children.Add(progressBorder);

                Grid.SetRow(contentPanel, 1);
                mainGrid.Children.Add(contentPanel);

                // === STOPKA - ESC info ===
                var footerText = new TextBlock
                {
                    Text = "Naciśnij ESC lub ✕ aby zamknąć",
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 30, 0, 0)
                };
                Grid.SetRow(footerText, 2);
                mainGrid.Children.Add(footerText);

                mainContainer.Children.Add(mainGrid);
            };

            // Początkowe renderowanie
            refreshContent();

            // Obsługa klawisza ESC
            dialog.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    dialog.Close();
            };

            dialog.Show();
        }

        /// <summary>
        /// Uproszczona wersja wyświetlania okna szczegółów produktu.
        /// </summary>
        /// <param name="nazwa">Nazwa produktu</param>
        /// <param name="towarId">ID produktu</param>
        /// <param name="plan">Planowana ilość</param>
        /// <param name="fakt">Faktyczna ilość</param>
        /// <param name="zamLubWyd">Zamówienia lub wydania</param>
        /// <param name="bilans">Bilans</param>
        /// <param name="stan">Stan magazynowy</param>
        /// <param name="data">Data</param>
        /// <param name="useReleases">Czy używać wydań</param>
        public static void ShowProductDetailWindow(
            string nazwa,
            int towarId,
            decimal plan,
            decimal fakt,
            decimal zamLubWyd,
            decimal bilans,
            decimal stan,
            DateTime data,
            bool useReleases = false)
        {
            var productData = new ProductDetailData
            {
                Id = towarId,
                Kod = nazwa,
                Nazwa = nazwa,
                Plan = plan,
                Fakt = fakt,
                Stan = stan,
                Zamowienia = useReleases ? 0 : zamLubWyd,
                Wydania = useReleases ? zamLubWyd : 0,
                Bilans = bilans,
                Data = data
            };

            ShowExpandedProductCard(productData, useReleases);
        }
    }
}
