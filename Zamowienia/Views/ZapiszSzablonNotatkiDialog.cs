using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NS = Kalendarz1.Zamowienia.Services.NotatkiService;

namespace Kalendarz1.Zamowienia.Views
{
    /// <summary>
    /// Dialog zapisu notatki jako szablon: tekst + kategoria + zakres + pin.
    /// </summary>
    public sealed class ZapiszSzablonNotatkiDialog : Window
    {
        private readonly TextBox _tbTekst;
        private readonly ComboBox _cmbKategoria;
        private readonly RadioButton _rbGlobalny;
        private readonly RadioButton _rbPerKlient;
        private readonly RadioButton _rbPerHandlowiec;
        private readonly CheckBox _cbPin;

        public string Tekst => _tbTekst.Text?.Trim() ?? "";
        public string Kategoria => _cmbKategoria.SelectedItem as string ?? NS.KategoriaInne;
        public string Zakres => _rbPerKlient.IsChecked == true ? NS.ZakresPerKlient
                              : _rbPerHandlowiec.IsChecked == true ? NS.ZakresPerHandlowiec
                              : NS.ZakresGlobalny;
        public bool Pinowane => _cbPin.IsChecked == true;

        public ZapiszSzablonNotatkiDialog(string startText, int? klientId, string klientNazwa, string userId)
        {
            Title = "Zapisz jako szablon notatki";
            Width = 520; Height = 480;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 12;
            Background = Brushes.White;

            var root = new StackPanel { Margin = new Thickness(18) };

            // Tytuł
            root.Children.Add(new TextBlock
            {
                Text = "💾 Zapisz jako szablon",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x40, 0x9A)),
                Margin = new Thickness(0, 0, 0, 12)
            });

            // Tekst
            root.Children.Add(new TextBlock { Text = "Treść notatki:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            _tbTekst = new TextBox
            {
                Text = startText ?? "",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 80,
                MaxHeight = 140,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 12)
            };
            root.Children.Add(_tbTekst);

            // Kategoria
            root.Children.Add(new TextBlock { Text = "Kategoria:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            _cmbKategoria = new ComboBox { Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 0, 12) };
            foreach (var k in NS.Kategorie) _cmbKategoria.Items.Add(k);
            _cmbKategoria.SelectedIndex = 0;
            root.Children.Add(_cmbKategoria);

            // Zakres
            root.Children.Add(new TextBlock { Text = "Zakres widoczności:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });

            _rbGlobalny = new RadioButton
            {
                Content = "🌍 Globalny — widoczny dla wszystkich, przy każdym kliencie",
                IsChecked = true,
                Margin = new Thickness(0, 0, 0, 4)
            };
            root.Children.Add(_rbGlobalny);

            _rbPerKlient = new RadioButton
            {
                Content = klientId.HasValue
                    ? $"👤 Dla tego klienta: {klientNazwa} (id={klientId})"
                    : "👤 Dla wybranego klienta (najpierw wybierz klienta)",
                IsEnabled = klientId.HasValue,
                Margin = new Thickness(0, 0, 0, 4)
            };
            root.Children.Add(_rbPerKlient);

            _rbPerHandlowiec = new RadioButton
            {
                Content = $"🧑‍💼 Tylko dla mnie ({userId})",
                IsEnabled = !string.IsNullOrEmpty(userId),
                Margin = new Thickness(0, 0, 0, 12)
            };
            root.Children.Add(_rbPerHandlowiec);

            // Pin
            _cbPin = new CheckBox
            {
                Content = "📌 Przypnij — zawsze pokazuj na górze listy",
                Margin = new Thickness(0, 0, 0, 14)
            };
            root.Children.Add(_cbPin);

            // Buttons
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var btnCancel = new Button
            {
                Content = "Anuluj",
                Width = 90, Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                IsCancel = true
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            var btnOk = new Button
            {
                Content = "Zapisz",
                Width = 100, Height = 32,
                IsDefault = true,
                Background = new SolidColorBrush(Color.FromRgb(0x21, 0x40, 0x9A)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold
            };
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_tbTekst.Text))
                {
                    MessageBox.Show(this, "Treść szablonu nie może być pusta.", "Pusty tekst",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                DialogResult = true;
                Close();
            };
            buttons.Children.Add(btnCancel);
            buttons.Children.Add(btnOk);
            root.Children.Add(buttons);

            Content = root;
        }
    }
}
