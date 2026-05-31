using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.WPF
{
    /// <summary>
    /// Okno admina — konfiguracja 2 kamer dla Panelu Pani Joli.
    /// Jola panel tylko OGLĄDA — admin (Sergiusz) wybiera tutaj który kanał NVR jest pokazany.
    /// Zapis do %LOCALAPPDATA%\Kalendarz1\panel_jola_cameras.json.
    /// </summary>
    public class PanelJolaKameryConfigWindow : Window
    {
        private TextBox _txtCh1 = null!;
        private TextBox _txtName1 = null!;
        private TextBox _txtCh2 = null!;
        private TextBox _txtName2 = null!;

        public PanelJolaKameryConfigWindow()
        {
            Title = "Kamery Panelu Joli — konfiguracja (admin)";
            Width = 560;
            Height = 420;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(245, 247, 250));
            ResizeMode = ResizeMode.NoResize;

            var cfg = PanelJolaKameryConfig.Load();

            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // tytuł
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // info
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // kam 1
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // kam 2
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // przyciski

            var title = new TextBlock
            {
                Text = "Kamery widoczne na Panelu Pani Joli",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4),
                Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33))
            };
            Grid.SetRow(title, 0);
            root.Children.Add(title);

            var info = new TextBlock
            {
                Text = "Wpisz numer kanału NVR (1-32) i czytelną nazwę. Jola nie ma możliwości zmiany — to ustawiasz tylko Ty.\nZmiana wymaga zamknięcia i ponownego otwarcia Panelu Pani Joli.",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(96, 96, 96)),
                Margin = new Thickness(0, 0, 0, 16)
            };
            Grid.SetRow(info, 1);
            root.Children.Add(info);

            var cam1 = BuildCameraRow("Kamera 1 (lewa)", cfg.Camera1, out _txtCh1, out _txtName1);
            Grid.SetRow(cam1, 2);
            root.Children.Add(cam1);

            var cam2 = BuildCameraRow("Kamera 2 (prawa)", cfg.Camera2, out _txtCh2, out _txtName2);
            Grid.SetRow(cam2, 3);
            root.Children.Add(cam2);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };

            var btnCancel = new Button
            {
                Content = "Anuluj",
                Width = 110,
                Height = 38,
                FontSize = 14,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCancel.Click += (s, e) => Close();
            buttons.Children.Add(btnCancel);

            var btnSave = new Button
            {
                Content = "✓ Zapisz",
                Width = 140,
                Height = 38,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnSave.Click += SaveClick;
            buttons.Children.Add(btnSave);

            Grid.SetRow(buttons, 5);
            root.Children.Add(buttons);

            Content = root;
        }

        private static Border BuildCameraRow(string label, PanelJolaKamera cam, out TextBox txtCh, out TextBox txtName)
        {
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 226, 230)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 12),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(lbl, 0);
            Grid.SetColumnSpan(lbl, 2);
            grid.Children.Add(lbl);

            var lblCh = new TextBlock { Text = "Kanał (nr):", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetRow(lblCh, 1);
            Grid.SetColumn(lblCh, 0);
            grid.Children.Add(lblCh);

            var chPanel = new Grid();
            chPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            chPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            chPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            txtCh = new TextBox
            {
                Text = cam.Channel.ToString(),
                FontSize = 14,
                Padding = new Thickness(6, 4, 6, 4),
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(txtCh, 0);
            chPanel.Children.Add(txtCh);

            var lblName = new TextBlock { Text = "Nazwa:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(lblName, 1);
            chPanel.Children.Add(lblName);

            txtName = new TextBox
            {
                Text = cam.Name,
                FontSize = 14,
                Padding = new Thickness(6, 4, 6, 4),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtName, 2);
            chPanel.Children.Add(txtName);

            Grid.SetRow(chPanel, 1);
            Grid.SetColumn(chPanel, 1);
            grid.Children.Add(chPanel);

            border.Child = grid;
            return border;
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(_txtCh1.Text.Trim(), out int ch1) || ch1 < 1 || ch1 > 64)
            {
                MessageBox.Show("Kamera 1: numer kanału musi być liczbą 1-64.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                _txtCh1.Focus();
                return;
            }
            if (!int.TryParse(_txtCh2.Text.Trim(), out int ch2) || ch2 < 1 || ch2 > 64)
            {
                MessageBox.Show("Kamera 2: numer kanału musi być liczbą 1-64.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                _txtCh2.Focus();
                return;
            }

            var name1 = string.IsNullOrWhiteSpace(_txtName1.Text) ? $"Kanał {ch1}" : _txtName1.Text.Trim();
            var name2 = string.IsNullOrWhiteSpace(_txtName2.Text) ? $"Kanał {ch2}" : _txtName2.Text.Trim();

            try
            {
                var cfg = new PanelJolaKameryConfig
                {
                    Camera1 = new PanelJolaKamera { Channel = ch1, Name = name1 },
                    Camera2 = new PanelJolaKamera { Channel = ch2, Name = name2 }
                };
                cfg.Save();
                MessageBox.Show(
                    "Zapisano. Jeśli Panel Pani Joli jest otwarty — zamknij go i uruchom ponownie, by zobaczyć nowe kanały.",
                    "OK", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
