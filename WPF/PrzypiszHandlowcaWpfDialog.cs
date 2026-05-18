using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.WPF
{
    public sealed class PrzypiszHandlowcaWpfDialog : Window
    {
        private readonly ComboBox _cmb;
        public string? WybranyHandlowiec
        {
            get
            {
                // Preferuj SelectedItem (gdy klikał z dropdownu); fallback na Text (gdy wpisał ręcznie)
                if (_cmb.SelectedItem is string s && !string.IsNullOrWhiteSpace(s)) return s.Trim();
                return (_cmb.Text ?? "").Trim();
            }
        }

        public PrzypiszHandlowcaWpfDialog(string odbiorca, string aktualny, List<string> dostepni)
        {
            Title = "Przypisz handlowca";
            Width = 400; Height = 220;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            Background = Brushes.White;

            var grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.Children.Add(new TextBlock
            {
                Text = "Kontrahent:",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                FontSize = 11
            });

            var lblNazwa = new TextBlock
            {
                Text = string.IsNullOrEmpty(odbiorca) ? "(nieznany)" : odbiorca,
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x40, 0x9A)),
                Margin = new Thickness(0, 2, 0, 12),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(lblNazwa, 1);
            grid.Children.Add(lblNazwa);

            var lblHand = new TextBlock
            {
                Text = "Handlowiec (puste = brak):",
                Margin = new Thickness(0, 0, 0, 4),
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55))
            };
            Grid.SetRow(lblHand, 2);
            grid.Children.Add(lblHand);

            _cmb = new ComboBox
            {
                IsEditable = true,
                Padding = new Thickness(6, 4, 6, 4),
                MinHeight = 30,
                Text = aktualny ?? ""
            };
            foreach (var h in dostepni) _cmb.Items.Add(h);
            Grid.SetRow(_cmb, 3);
            grid.Children.Add(_cmb);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            var btnCancel = new Button
            {
                Content = "Anuluj",
                Width = 90, Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                IsCancel = true
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            var btnOK = new Button
            {
                Content = "Zapisz",
                Width = 90, Height = 32,
                IsDefault = true,
                Background = new SolidColorBrush(Color.FromRgb(0x21, 0x40, 0x9A)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold
            };
            btnOK.Click += (s, e) => { DialogResult = true; Close(); };
            buttons.Children.Add(btnCancel);
            buttons.Children.Add(btnOK);
            Grid.SetRow(buttons, 5);
            grid.Children.Add(buttons);

            Content = grid;
        }
    }
}
