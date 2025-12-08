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

        private void BtnPodgladSzablon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is SzablonOdbiorcow szablon)
            {
                var podgladWindow = new PodgladSzablonuWindow(szablon, _operatorId, _szablonyManager);
                podgladWindow.Owner = this;
                if (podgladWindow.ShowDialog() == true)
                {
                    // Szablon mógł zostać zmodyfikowany
                    WczytajSzablony();
                }
            }
        }

        private void BtnEdytujSzablon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is SzablonOdbiorcow szablon)
            {
                // Dialog do edycji nazwy i opisu
                var editDialog = new EdytujSzablonDialog(szablon.Nazwa, szablon.Opis);
                editDialog.Owner = this;

                if (editDialog.ShowDialog() == true)
                {
                    szablon.Nazwa = editDialog.NowaNazwa;
                    szablon.Opis = editDialog.NowyOpis;
                    _szablonyManager.AktualizujSzablonOdbiorcow(_operatorId, szablon);
                    WczytajSzablony();
                }
            }
        }

        private void BtnDuplikujSzablon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is SzablonOdbiorcow szablon)
            {
                // Utwórz kopię szablonu
                var kopia = new SzablonOdbiorcow
                {
                    Nazwa = $"{szablon.Nazwa} (kopia)",
                    Opis = szablon.Opis,
                    Odbiorcy = szablon.Odbiorcy.Select(o => new OdbiorcaSzablonu
                    {
                        Id = o.Id,
                        Nazwa = o.Nazwa,
                        NIP = o.NIP,
                        Adres = o.Adres,
                        KodPocztowy = o.KodPocztowy,
                        Miejscowosc = o.Miejscowosc,
                        Telefon = o.Telefon,
                        Email = o.Email,
                        OsobaKontaktowa = o.OsobaKontaktowa,
                        Zrodlo = o.Zrodlo
                    }).ToList()
                };

                _szablonyManager.DodajSzablonOdbiorcow(_operatorId, kopia);
                WczytajSzablony();

                MessageBox.Show($"Utworzono kopię szablonu: \"{kopia.Nazwa}\"", "Szablon zduplikowany",
                    MessageBoxButton.OK, MessageBoxImage.Information);
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

    /// <summary>
    /// Dialog do edycji nazwy i opisu szablonu
    /// </summary>
    public partial class EdytujSzablonDialog : Window
    {
        public string NowaNazwa { get; private set; } = "";
        public string NowyOpis { get; private set; } = "";

        public EdytujSzablonDialog(string nazwa, string opis)
        {
            Title = "Edytuj szablon";
            Width = 550;
            Height = 320;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;

            var border = new System.Windows.Controls.Border
            {
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(12),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20)
            };

            // Dodaj cień
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 0,
                Opacity = 0.3
            };

            var mainGrid = new System.Windows.Controls.Grid();
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            // Tytuł
            var titleBlock = new System.Windows.Controls.TextBlock
            {
                Text = "✏️ Edytuj szablon",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 41, 55)),
                Margin = new Thickness(0, 0, 0, 20)
            };
            System.Windows.Controls.Grid.SetRow(titleBlock, 0);

            // Nazwa
            var nazwaLabel = new System.Windows.Controls.TextBlock
            {
                Text = "Nazwa szablonu:",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128)),
                Margin = new Thickness(0, 0, 0, 5)
            };
            var nazwaBox = new System.Windows.Controls.TextBox
            {
                Text = nazwa,
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var nazwaPanel = new System.Windows.Controls.StackPanel();
            nazwaPanel.Children.Add(nazwaLabel);
            nazwaPanel.Children.Add(nazwaBox);
            System.Windows.Controls.Grid.SetRow(nazwaPanel, 1);

            // Opis
            var opisLabel = new System.Windows.Controls.TextBlock
            {
                Text = "Opis (opcjonalnie):",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128)),
                Margin = new Thickness(0, 0, 0, 5)
            };
            var opisBox = new System.Windows.Controls.TextBox
            {
                Text = opis,
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 20),
                Height = 60,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
            };
            var opisPanel = new System.Windows.Controls.StackPanel();
            opisPanel.Children.Add(opisLabel);
            opisPanel.Children.Add(opisBox);
            System.Windows.Controls.Grid.SetRow(opisPanel, 2);

            // Przyciski
            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Anuluj",
                Padding = new Thickness(20, 10, 20, 10),
                Margin = new Thickness(0, 0, 10, 0),
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            cancelButton.Click += (s, e) => { DialogResult = false; Close(); };

            var saveButton = new System.Windows.Controls.Button
            {
                Content = "💾 Zapisz",
                Padding = new Thickness(20, 10, 20, 10),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(75, 131, 60)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            saveButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(nazwaBox.Text))
                {
                    MessageBox.Show("Nazwa szablonu nie może być pusta.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                NowaNazwa = nazwaBox.Text.Trim();
                NowyOpis = opisBox.Text?.Trim() ?? "";
                DialogResult = true;
                Close();
            };

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(saveButton);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 3);

            mainGrid.Children.Add(titleBlock);
            mainGrid.Children.Add(nazwaPanel);
            mainGrid.Children.Add(opisPanel);
            mainGrid.Children.Add(buttonPanel);

            border.Child = mainGrid;
            Content = border;

            Loaded += (s, e) => { nazwaBox.Focus(); nazwaBox.SelectAll(); };
        }
    }

    /// <summary>
    /// Okno podglądu i edycji odbiorców w szablonie
    /// </summary>
    public partial class PodgladSzablonuWindow : Window
    {
        private readonly SzablonOdbiorcow _szablon;
        private readonly string _operatorId;
        private readonly SzablonyManager _szablonyManager;
        private List<OdbiorcaSzablonu> _odbiorcy;
        private bool _zmodyfikowano = false;

        public PodgladSzablonuWindow(SzablonOdbiorcow szablon, string operatorId, SzablonyManager szablonyManager)
        {
            _szablon = szablon;
            _operatorId = operatorId;
            _szablonyManager = szablonyManager;
            _odbiorcy = szablon.Odbiorcy.ToList();

            Title = $"Odbiorcy w szablonie: {szablon.Nazwa}";
            Width = 900;
            Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;

            BuildUI();
        }

        private void BuildUI()
        {
            var mainBorder = new System.Windows.Controls.Border
            {
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(12),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(1)
            };

            mainBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 0,
                Opacity = 0.3
            };

            var mainGrid = new System.Windows.Controls.Grid();
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            // Nagłówek
            var headerBorder = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(75, 131, 60)),
                CornerRadius = new CornerRadius(12, 12, 0, 0),
                Padding = new Thickness(20, 15, 20, 15)
            };
            headerBorder.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };

            var headerGrid = new System.Windows.Controls.Grid();
            var headerText = new System.Windows.Controls.TextBlock
            {
                Text = $"👥 {_szablon.Nazwa}",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            var closeBtn = new System.Windows.Controls.Button
            {
                Content = "✕",
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 16,
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(8, 4, 8, 4)
            };
            closeBtn.Click += (s, e) => { DialogResult = _zmodyfikowano; Close(); };

            headerGrid.Children.Add(headerText);
            headerGrid.Children.Add(closeBtn);
            headerBorder.Child = headerGrid;
            System.Windows.Controls.Grid.SetRow(headerBorder, 0);

            // Lista odbiorców
            var contentGrid = new System.Windows.Controls.Grid { Margin = new Thickness(20) };
            contentGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            contentGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

            var infoText = new System.Windows.Controls.TextBlock
            {
                Text = $"Odbiorców w szablonie: {_odbiorcy.Count}",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            System.Windows.Controls.Grid.SetRow(infoText, 0);

            var listBorder = new System.Windows.Controls.Border
            {
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };

            var listBox = new System.Windows.Controls.ListBox
            {
                BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent
            };

            RefreshList(listBox, infoText);

            listBorder.Child = listBox;
            System.Windows.Controls.Grid.SetRow(listBorder, 1);

            contentGrid.Children.Add(infoText);
            contentGrid.Children.Add(listBorder);
            System.Windows.Controls.Grid.SetRow(contentGrid, 1);

            // Stopka
            var footerBorder = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 250, 251)),
                CornerRadius = new CornerRadius(0, 0, 12, 12),
                Padding = new Thickness(20, 15, 20, 15)
            };

            var footerGrid = new System.Windows.Controls.Grid();
            var leftPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var usunZaznaczoneBtn = new System.Windows.Controls.Button
            {
                Content = "🗑️ Usuń zaznaczone",
                Padding = new Thickness(15, 10, 15, 10),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            usunZaznaczoneBtn.Click += (s, e) =>
            {
                var zaznaczeni = listBox.SelectedItems
                    .Cast<System.Windows.Controls.ListBoxItem>()
                    .Select(item => item.Tag as OdbiorcaSzablonu)
                    .Where(o => o != null)
                    .ToList();

                if (zaznaczeni.Count == 0)
                {
                    MessageBox.Show("Zaznacz odbiorców do usunięcia.", "Brak zaznaczenia", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Czy na pewno chcesz usunąć {zaznaczeni.Count} odbiorców z szablonu?",
                    "Potwierdź usunięcie",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var o in zaznaczeni)
                        _odbiorcy.Remove(o!);

                    _szablon.Odbiorcy = _odbiorcy;
                    _szablonyManager.AktualizujSzablonOdbiorcow(_operatorId, _szablon);
                    _zmodyfikowano = true;
                    RefreshList(listBox, infoText);
                }
            };

            leftPanel.Children.Add(usunZaznaczoneBtn);

            var zamknijBtn = new System.Windows.Controls.Button
            {
                Content = "Zamknij",
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(20, 10, 20, 10),
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            zamknijBtn.Click += (s, e) => { DialogResult = _zmodyfikowano; Close(); };

            footerGrid.Children.Add(leftPanel);
            footerGrid.Children.Add(zamknijBtn);
            footerBorder.Child = footerGrid;
            System.Windows.Controls.Grid.SetRow(footerBorder, 2);

            mainGrid.Children.Add(headerBorder);
            mainGrid.Children.Add(contentGrid);
            mainGrid.Children.Add(footerBorder);

            mainBorder.Child = mainGrid;
            Content = mainBorder;
        }

        private void RefreshList(System.Windows.Controls.ListBox listBox, System.Windows.Controls.TextBlock infoText)
        {
            listBox.Items.Clear();
            infoText.Text = $"Odbiorców w szablonie: {_odbiorcy.Count}";

            foreach (var odbiorca in _odbiorcy)
            {
                var itemBorder = new System.Windows.Controls.Border
                {
                    Padding = new Thickness(12),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 231, 235)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Background = System.Windows.Media.Brushes.White
                };

                var itemGrid = new System.Windows.Controls.Grid();
                itemGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

                var infoPanel = new System.Windows.Controls.StackPanel();

                var nazwaText = new System.Windows.Controls.TextBlock
                {
                    Text = odbiorca.Nazwa,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 41, 55))
                };

                var detalePanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Margin = new Thickness(0, 3, 0, 0)
                };

                if (!string.IsNullOrEmpty(odbiorca.NIP))
                {
                    detalePanel.Children.Add(new System.Windows.Controls.TextBlock
                    {
                        Text = $"NIP: {odbiorca.NIP}",
                        FontSize = 11,
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128)),
                        Margin = new Thickness(0, 0, 10, 0)
                    });
                }

                if (!string.IsNullOrEmpty(odbiorca.Miejscowosc))
                {
                    detalePanel.Children.Add(new System.Windows.Controls.TextBlock
                    {
                        Text = $"📍 {odbiorca.Miejscowosc}",
                        FontSize = 11,
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175))
                    });
                }

                infoPanel.Children.Add(nazwaText);
                if (detalePanel.Children.Count > 0)
                    infoPanel.Children.Add(detalePanel);

                itemGrid.Children.Add(infoPanel);
                itemBorder.Child = itemGrid;

                var listItem = new System.Windows.Controls.ListBoxItem
                {
                    Content = itemBorder,
                    Tag = odbiorca,
                    Padding = new Thickness(0)
                };

                listBox.Items.Add(listItem);
            }

            // Ustaw SelectionMode na Extended dla wielokrotnego zaznaczania
            listBox.SelectionMode = System.Windows.Controls.SelectionMode.Extended;
        }
    }
}
