using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kalendarz1.Kartoteka.Models;

namespace Kalendarz1.Kartoteka.Features.Przypomnienia
{
    public partial class PrzypomnieniaCentrum : Window
    {
        private readonly PrzypomnienieService _service;
        private readonly string _userName;
        private readonly int? _klientId;
        private readonly string _nazwaKlienta;

        public PrzypomnieniaCentrum(string connLibra, string userName, int? klientId = null, string nazwaKlienta = null)
        {
            InitializeComponent();

            _service = new PrzypomnienieService(connLibra);
            _userName = userName;
            _klientId = klientId;
            _nazwaKlienta = nazwaKlienta;

            if (klientId.HasValue)
                Title = $"Przypomnienia — {nazwaKlienta}";

            Loaded += async (s, e) =>
            {
                await _service.EnsureTableExistsAsync();
                await LoadPrzypomnienia();
            };
        }

        private async System.Threading.Tasks.Task LoadPrzypomnienia()
        {
            try
            {
                var lista = await _service.PobierzAktywnePrzypomnienia(
                    przypisaneDo: _userName, klientId: _klientId);

                panelPrzypomnienia.Children.Clear();

                if (lista.Count == 0)
                {
                    panelPrzypomnienia.Children.Add(new TextBlock
                    {
                        Text = "Brak aktywnych przypomnień.",
                        FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                        Margin = new Thickness(12, 20, 0, 0)
                    });
                }

                int przeterminowane = 0;
                foreach (var p in lista)
                {
                    panelPrzypomnienia.Children.Add(CreatePrzypomnienieCard(p));
                    if (p.JestPrzeterminowane) przeterminowane++;
                }

                if (przeterminowane > 0)
                {
                    badgeLicznik.Visibility = Visibility.Visible;
                    txtBadge.Text = przeterminowane.ToString();
                }
                else
                {
                    badgeLicznik.Visibility = Visibility.Collapsed;
                }

                txtStatus.Text = $"Aktywne przypomnienia: {lista.Count} (przeterminowane: {przeterminowane})";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Błąd: {ex.Message}";
            }
        }

        private Border CreatePrzypomnienieCard(Przypomnienie p)
        {
            var bgColor = p.JestPrzeterminowane
                ? Color.FromRgb(254, 226, 226)  // red
                : p.Priorytet == 1
                    ? Color.FromRgb(254, 243, 199) // amber
                    : Color.FromRgb(255, 255, 255); // white

            var borderColor = p.JestPrzeterminowane
                ? Color.FromRgb(252, 165, 165) // red border
                : Color.FromRgb(229, 231, 235); // gray border

            var card = new Border
            {
                Background = new SolidColorBrush(bgColor),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Treść
            var content = new StackPanel();
            var headerLine = new StackPanel { Orientation = Orientation.Horizontal };
            headerLine.Children.Add(new TextBlock
            {
                Text = $"{p.TypIkona} {p.PriorytetIkona}",
                FontSize = 13, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center
            });
            headerLine.Children.Add(new TextBlock
            {
                Text = p.Tytul, FontWeight = FontWeights.SemiBold, FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                VerticalAlignment = VerticalAlignment.Center
            });
            content.Children.Add(headerLine);

            if (!string.IsNullOrEmpty(p.Opis))
            {
                content.Children.Add(new TextBlock
                {
                    Text = p.Opis, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0)
                });
            }

            var dateLine = new TextBlock
            {
                Text = $"📅 {p.DataPrzypomnienia:dd.MM.yyyy HH:mm}" +
                       (p.JestPrzeterminowane ? " (PRZETERMINOWANE)" : ""),
                FontSize = 10, Foreground = new SolidColorBrush(
                    p.JestPrzeterminowane ? Color.FromRgb(220, 38, 38) : Color.FromRgb(156, 163, 175)),
                Margin = new Thickness(0, 4, 0, 0)
            };
            content.Children.Add(dateLine);

            Grid.SetColumn(content, 0);
            grid.Children.Add(content);

            // Przyciski akcji
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var btnWykonane = new Button
            {
                Content = "✓", ToolTip = "Oznacz jako wykonane",
                Width = 28, Height = 28, FontSize = 14,
                Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)),
                Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52)),
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0)
            };
            btnWykonane.Click += async (s, ev) =>
            {
                await _service.ZmienStatusAsync(p.Id, "Wykonane", _userName);
                await LoadPrzypomnienia();
            };

            var btnOdloz = new Button
            {
                Content = "⏳", ToolTip = "Odłóż",
                Width = 28, Height = 28, FontSize = 14,
                Background = new SolidColorBrush(Color.FromRgb(254, 249, 195)),
                Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14)),
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0)
            };
            btnOdloz.Click += async (s, ev) =>
            {
                await _service.ZmienStatusAsync(p.Id, "Odlozone", _userName);
                await LoadPrzypomnienia();
            };

            var btnUsun = new Button
            {
                Content = "🗑", ToolTip = "Usuń",
                Width = 28, Height = 28, FontSize = 14,
                Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0)
            };
            btnUsun.Click += async (s, ev) =>
            {
                if (MessageBox.Show("Usunąć to przypomnienie?", "Potwierdzenie",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await _service.UsunPrzypomnienieAsync(p.Id);
                    await LoadPrzypomnienia();
                }
            };

            actions.Children.Add(btnWykonane);
            actions.Children.Add(btnOdloz);
            actions.Children.Add(btnUsun);
            Grid.SetColumn(actions, 1);
            grid.Children.Add(actions);

            card.Child = grid;
            return card;
        }

        private async void BtnDodaj_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DodajPrzypomnienieWindow(_klientId, _nazwaKlienta);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.NowePrzypomnienie != null)
            {
                try
                {
                    dialog.NowePrzypomnienie.UtworzonyPrzez = _userName;
                    await _service.DodajPrzypomnienieAsync(dialog.NowePrzypomnienie);
                    await LoadPrzypomnienia();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd dodawania przypomnienia:\n{ex.Message}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadPrzypomnienia();
        }
    }
}
