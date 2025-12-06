using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Okno wyboru i zarządzania szablonami odbiorców
    /// </summary>
    public partial class SzablonOdbiorcowWindow : Window
    {
        private readonly SzablonyManager _szablonyManager;
        private readonly string _operatorId;
        private List<SzablonOdbiorcow> _szablony = new();

        /// <summary>
        /// Wybrany szablon do wczytania
        /// </summary>
        public SzablonOdbiorcow? WybranySzablon { get; private set; }

        public SzablonOdbiorcowWindow(string operatorId)
        {
            InitializeComponent();
            _szablonyManager = new SzablonyManager();
            _operatorId = operatorId;

            WczytajSzablony();
        }

        private void WczytajSzablony()
        {
            _szablony = _szablonyManager.WczytajSzablonyOdbiorcow(_operatorId);
            lstSzablony.ItemsSource = _szablony;

            // Pokaż/ukryj placeholder
            placeholderBrakSzablonow.Visibility = _szablony.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            lstSzablony.Visibility = _szablony.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void LstSzablony_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            btnWczytaj.IsEnabled = lstSzablony.SelectedItem != null;
        }

        private void BtnWczytaj_Click(object sender, RoutedEventArgs e)
        {
            if (lstSzablony.SelectedItem is SzablonOdbiorcow szablon)
            {
                WybranySzablon = szablon;
                DialogResult = true;
                Close();
            }
        }

        private void BtnEdytujSzablon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is SzablonOdbiorcow szablon)
            {
                // Prosty dialog do edycji nazwy
                var inputDialog = new InputDialog("Edytuj szablon", "Nazwa szablonu:", szablon.Nazwa);
                inputDialog.Owner = this;

                if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.InputText))
                {
                    szablon.Nazwa = inputDialog.InputText;
                    _szablonyManager.AktualizujSzablonOdbiorcow(_operatorId, szablon);
                    WczytajSzablony();
                }
            }
        }

        private void BtnUsunSzablon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is SzablonOdbiorcow szablon)
            {
                var result = MessageBox.Show(
                    $"Czy na pewno chcesz usunąć szablon \"{szablon.Nazwa}\"?\n\nTa operacja jest nieodwracalna.",
                    "Potwierdź usunięcie",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _szablonyManager.UsunSzablonOdbiorcow(_operatorId, szablon.Id);
                    WczytajSzablony();
                }
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// Prosty dialog do wprowadzania tekstu
    /// </summary>
    public partial class InputDialog : Window
    {
        public string InputText { get; private set; } = "";

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            Title = title;
            Width = 400;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;

            var border = new System.Windows.Controls.Border
            {
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(8),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20)
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            var titleBlock = new System.Windows.Controls.TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            System.Windows.Controls.Grid.SetRow(titleBlock, 0);

            var promptBlock = new System.Windows.Controls.TextBlock
            {
                Text = prompt,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var textBox = new System.Windows.Controls.TextBox
            {
                Text = defaultValue,
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 15)
            };
            textBox.SelectAll();

            var promptPanel = new System.Windows.Controls.StackPanel();
            promptPanel.Children.Add(promptBlock);
            promptPanel.Children.Add(textBox);
            System.Windows.Controls.Grid.SetRow(promptPanel, 1);

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Anuluj",
                Padding = new Thickness(20, 8, 20, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219))
            };
            cancelButton.Click += (s, e) => { DialogResult = false; Close(); };

            var okButton = new System.Windows.Controls.Button
            {
                Content = "OK",
                Padding = new Thickness(20, 8, 20, 8),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(75, 131, 60)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0)
            };
            okButton.Click += (s, e) => { InputText = textBox.Text; DialogResult = true; Close(); };

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(okButton);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 2);

            grid.Children.Add(titleBlock);
            grid.Children.Add(promptPanel);
            grid.Children.Add(buttonPanel);

            border.Child = grid;
            Content = border;

            // Focus na textbox po załadowaniu
            Loaded += (s, e) => { textBox.Focus(); textBox.SelectAll(); };
        }
    }
}
