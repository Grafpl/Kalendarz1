using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kalendarz1.DashboardPrzychodu.Views
{
    /// <summary>
    /// #11 - Custom logo i ikony KPI (context menu PPM na kafelkach + logo box).
    /// Wybor emoji albo pliku graficznego (PNG/JPG/ICO/BMP).
    /// Wybory persystowane przez DashboardSettings (#9).
    /// </summary>
    public partial class DashboardPrzychoduWindow
    {
        /// <summary>
        /// Prawy przycisk myszy na logo - pokazuje menu wyboru logo.
        /// </summary>
        private void LogoBox_RightClick(object sender, MouseButtonEventArgs e)
        {
            var menu = (ContextMenu)FindResource("LogoContextMenu");
            menu.PlacementTarget = sender as UIElement;
            menu.IsOpen = true;
            e.Handled = true;
        }

        /// <summary>
        /// Ustawia emoji jako logo (z LogoContextMenu).
        /// </summary>
        private void Logo_SetEmoji(object sender, RoutedEventArgs e)
        {
            var emoji = (sender as MenuItem)?.Tag?.ToString();
            if (!string.IsNullOrEmpty(emoji))
            {
                if (!(LogoBox.Child is TextBlock))
                    LogoBox.Child = LogoIcon;

                LogoIcon.Text = emoji;
                Debug.WriteLine($"[DashboardPrzychodu] Ustawiono emoji logo: {emoji}");

                // Persystencja (#9): zapisz emoji, wyczysc sciezke pliku
                _settings.LogoEmoji = emoji;
                _settings.LogoFilePath = null;
                SaveSettings();
            }
        }

        /// <summary>
        /// Wybiera logo z dostepnych plikow w projekcie.
        /// </summary>
        private void Logo_SelectFile(object sender, RoutedEventArgs e)
        {
            var logoFiles = FindLogoFiles();

            if (logoFiles.Count == 0)
            {
                MessageBox.Show("Nie znaleziono plikow logo w katalogu aplikacji.\n\nDostepne sa emoji - uzyj menu kontekstowego.",
                    "Brak logo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectWindow = new Window
            {
                Title = "Wybierz logo",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(28, 25, 23)),
                ResizeMode = ResizeMode.NoResize
            };

            var mainGrid = new Grid { Margin = new Thickness(15) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = "Wybierz logo firmy",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            var listBox = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(41, 37, 36)),
                Foreground = new SolidColorBrush(Color.FromRgb(231, 229, 228)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(68, 64, 60)),
                BorderThickness = new Thickness(1)
            };

            foreach (var logoPath in logoFiles)
            {
                var item = new ListBoxItem
                {
                    Tag = logoPath,
                    Padding = new Thickness(8, 6, 8, 6)
                };

                var panel = new StackPanel { Orientation = Orientation.Horizontal };

                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(logoPath);
                    bitmap.DecodePixelHeight = 32;
                    bitmap.EndInit();

                    var img = new Image
                    {
                        Source = bitmap,
                        Width = 32,
                        Height = 32,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    panel.Children.Add(img);
                }
                catch
                {
                    panel.Children.Add(new TextBlock { Text = "📁", FontSize = 20, Margin = new Thickness(0, 0, 10, 0) });
                }

                panel.Children.Add(new TextBlock
                {
                    Text = Path.GetFileName(logoPath),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(231, 229, 228))
                });

                item.Content = panel;
                listBox.Items.Add(item);
            }

            Grid.SetRow(listBox, 1);
            mainGrid.Children.Add(listBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var btnSelect = new Button
            {
                Content = "Wybierz",
                Padding = new Thickness(15, 6, 15, 6),
                Background = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                Foreground = new SolidColorBrush(Color.FromRgb(28, 25, 23)),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            btnSelect.Click += (s, args) =>
            {
                if (listBox.SelectedItem is ListBoxItem selected && selected.Tag is string path)
                {
                    SetLogoFromFile(path);
                    selectWindow.Close();
                }
                else
                {
                    MessageBox.Show("Wybierz logo z listy", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            buttonPanel.Children.Add(btnSelect);

            var btnCancel = new Button
            {
                Content = "Anuluj",
                Padding = new Thickness(15, 6, 15, 6),
                Background = new SolidColorBrush(Color.FromRgb(68, 64, 60)),
                Foreground = new SolidColorBrush(Color.FromRgb(168, 162, 158)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, args) => selectWindow.Close();
            buttonPanel.Children.Add(btnCancel);

            Grid.SetRow(buttonPanel, 2);
            mainGrid.Children.Add(buttonPanel);

            selectWindow.Content = mainGrid;
            selectWindow.ShowDialog();
        }

        /// <summary>
        /// Znajduje dostepne pliki logo w katalogu aplikacji (PNG/JPG/ICO/BMP zawierajace "logo" w nazwie).
        /// </summary>
        private List<string> FindLogoFiles()
        {
            var logoFiles = new List<string>();
            var extensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.ico", "*.bmp" };

            var searchDirs = new[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."),
                Environment.CurrentDirectory
            };

            foreach (var dir in searchDirs)
            {
                try
                {
                    string fullDir = Path.GetFullPath(dir);
                    if (Directory.Exists(fullDir))
                    {
                        foreach (var ext in extensions)
                        {
                            var files = Directory.GetFiles(fullDir, ext, SearchOption.TopDirectoryOnly)
                                .Where(f => f.ToLower().Contains("logo"))
                                .ToList();
                            logoFiles.AddRange(files);
                        }
                    }
                }
                catch { }
            }

            return logoFiles.Select(f => Path.GetFullPath(f)).Distinct().ToList();
        }

        /// <summary>
        /// Ustawia logo z pliku graficznego.
        /// </summary>
        private void SetLogoFromFile(string logoPath)
        {
            try
            {
                LogoBox.Child = null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(logoPath);
                bitmap.DecodePixelHeight = 20;
                bitmap.EndInit();

                var image = new Image
                {
                    Source = bitmap,
                    Width = 20,
                    Height = 20,
                    Stretch = Stretch.Uniform
                };

                LogoBox.Child = image;

                Debug.WriteLine($"[DashboardPrzychodu] Ustawiono logo z pliku: {logoPath}");

                // Persystencja (#9)
                _settings.LogoFilePath = logoPath;
                _settings.LogoEmoji = null;
                SaveSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardPrzychodu] Blad ladowania logo: {ex.Message}");
                LogoBox.Child = LogoIcon;
                MessageBox.Show($"Nie mozna zaladowac obrazka:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Prawy przycisk myszy na kafelku KPI - pokazuje menu wyboru ikony.
        /// </summary>
        private void KpiIcon_RightClick(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            _currentKpiTarget = border?.Tag?.ToString();

            var menu = (ContextMenu)FindResource("IconContextMenu");

            var names = new Dictionary<string, string>
            {
                {"plan", "PLAN"},
                {"zwazone", "ZWAZONE"},
                {"pozostalo", "POZOSTALO"},
                {"odchylenie", "ODCHYLENIE"},
                {"tuszki", "TUSZKI"},
                {"realizacja", "REALIZACJA"}
            };

            if (names.TryGetValue(_currentKpiTarget ?? "", out var name))
            {
                if (menu.Items[0] is MenuItem titleItem)
                {
                    titleItem.Header = $"Wybierz ikone dla: {name}";
                }
            }

            menu.PlacementTarget = border;
            menu.IsOpen = true;
            e.Handled = true;
        }

        /// <summary>
        /// Ustawia emoji jako ikone KPI (z IconContextMenu) + persystencja.
        /// </summary>
        private void Icon_SetEmoji(object sender, RoutedEventArgs e)
        {
            var emoji = (sender as MenuItem)?.Tag?.ToString();
            if (string.IsNullOrEmpty(emoji) || string.IsNullOrEmpty(_currentKpiTarget)) return;

            ApplyKpiIconEmoji(_currentKpiTarget, emoji);

            // Persystencja (#9)
            _settings.KpiIkony[_currentKpiTarget] = emoji;
            SaveSettings();
        }
    }
}
