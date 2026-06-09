using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.SkrzynkaZakupu.Models;

namespace Kalendarz1.SkrzynkaZakupu.Views
{
    /// <summary>
    /// Autouzupełnianie adresów e-mail dla pola tekstowego z wieloma adresami (po przecinku).
    /// Pokazuje popup z listą pasujących kontaktów; obsługa klawiatury (↑/↓/Enter/Tab/Esc).
    /// </summary>
    public class EmailAutoComplete
    {
        private readonly TextBox _tb;
        private readonly Popup _popup;
        private readonly ListBox _list;
        private List<MailContact> _source = new();
        private bool _tlumiTextChanged;

        public EmailAutoComplete(TextBox tb)
        {
            _tb = tb;

            _list = new ListBox
            {
                MaxHeight = 250,
                BorderThickness = new Thickness(0),
                Background = Brushes.White
            };
            _list.ItemTemplate = BudujSzablon();
            _list.PreviewMouseLeftButtonUp += (_, _) => Wybierz();

            var ramka = new Border
            {
                Background = Brushes.White,
                BorderBrush = (Brush)new BrushConverter().ConvertFromString("#E5E9EF")!,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(4),
                Child = _list,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, Opacity = 0.18, BlurRadius = 12, ShadowDepth = 2, Direction = 270
                }
            };

            _popup = new Popup
            {
                PlacementTarget = _tb,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                Child = ramka
            };

            _tb.TextChanged += (_, _) => { if (!_tlumiTextChanged) Filtruj(); };
            _tb.PreviewKeyDown += OnKey;
            _tb.LostFocus += (_, _) => { if (!_popup.IsKeyboardFocusWithin) _popup.IsOpen = false; };
        }

        public void SetSource(List<MailContact> contacts) => _source = contacts ?? new();

        private static DataTemplate BudujSzablon()
        {
            // <Border Padding="8,6"><StackPanel>
            //   <TextBlock Text="{DisplayName}" FontWeight="SemiBold"/>
            //   <TextBlock Text="{Email}" Foreground="gray" FontSize="11"/>
            // </StackPanel></Border>
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.PaddingProperty, new Thickness(9, 6, 9, 6));

            var sp = new FrameworkElementFactory(typeof(StackPanel));

            var name = new FrameworkElementFactory(typeof(TextBlock));
            name.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(MailContact.DisplayName)));
            name.SetValue(TextBlock.FontSizeProperty, 13.0);
            name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            name.SetValue(TextBlock.ForegroundProperty, (Brush)new BrushConverter().ConvertFromString("#0F172A")!);
            name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

            var email = new FrameworkElementFactory(typeof(TextBlock));
            email.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(MailContact.Email)));
            email.SetValue(TextBlock.FontSizeProperty, 11.0);
            email.SetValue(TextBlock.ForegroundProperty, (Brush)new BrushConverter().ConvertFromString("#94A3B8")!);
            email.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

            sp.AppendChild(name);
            sp.AppendChild(email);
            border.AppendChild(sp);
            return new DataTemplate { VisualTree = border };
        }

        private void Filtruj()
        {
            string token = BiezacyToken(out _, out _).Trim();
            if (token.Length < 1)
            {
                _popup.IsOpen = false;
                return;
            }

            const StringComparison OIC = StringComparison.OrdinalIgnoreCase;
            var wyniki = _source
                .Where(c => c.Email.IndexOf(token, OIC) >= 0
                         || (!string.IsNullOrEmpty(c.DisplayName) && c.DisplayName.IndexOf(token, OIC) >= 0))
                .Select(c => new { c, r = Ranking(c, token) })
                .OrderBy(x => x.r)
                .ThenByDescending(x => x.c.UseCount)
                .ThenBy(x => x.c.Email.Length)
                .Take(8)
                .Select(x => x.c)
                .ToList();

            if (wyniki.Count == 0)
            {
                _popup.IsOpen = false;
                return;
            }

            _list.ItemsSource = wyniki;
            _list.SelectedIndex = 0;
            _popup.Width = _tb.ActualWidth;
            _popup.IsOpen = true;
        }

        /// <summary>0 = najlepsze (początek adresu / imienia), wyżej = słabsze (zawiera gdziekolwiek).</summary>
        private static int Ranking(MailContact c, string token)
        {
            const StringComparison OIC = StringComparison.OrdinalIgnoreCase;
            var email = c.Email ?? "";
            var name = c.DisplayName ?? "";
            var local = email.Contains('@') ? email.Substring(0, email.IndexOf('@')) : email;

            if (local.StartsWith(token, OIC)) return 0;                 // "ser" -> sergiusz@...
            if (SlowoZaczynaSie(name, token)) return 1;                 // "Sergiusz Piórkowski"
            if (email.StartsWith(token, OIC) || name.StartsWith(token, OIC)) return 2;
            return 3;                                                    // zawiera w środku
        }

        private static bool SlowoZaczynaSie(string text, string token)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var w in text.Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries))
                if (w.StartsWith(token, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            if (!_popup.IsOpen) return;
            switch (e.Key)
            {
                case Key.Down:
                    _list.SelectedIndex = Math.Min(_list.SelectedIndex + 1, _list.Items.Count - 1);
                    _list.ScrollIntoView(_list.SelectedItem);
                    e.Handled = true;
                    break;
                case Key.Up:
                    _list.SelectedIndex = Math.Max(_list.SelectedIndex - 1, 0);
                    _list.ScrollIntoView(_list.SelectedItem);
                    e.Handled = true;
                    break;
                case Key.Enter:
                case Key.Tab:
                    if (_list.SelectedItem != null) { Wybierz(); e.Handled = true; }
                    break;
                case Key.Escape:
                    _popup.IsOpen = false;
                    e.Handled = true;
                    break;
            }
        }

        private void Wybierz()
        {
            if (_list.SelectedItem is not MailContact c) return;
            string text = _tb.Text ?? "";
            BiezacyToken(out int tokenStart, out int caret);

            string prefix = text.Substring(0, tokenStart);
            string suffix = caret <= text.Length ? text.Substring(caret) : "";
            // dołóż spację po przecinku jeśli trzeba
            if (prefix.Length > 0 && !prefix.EndsWith(" ")) prefix += " ";

            string wstaw = c.Email + ", ";
            _tlumiTextChanged = true;
            _tb.Text = prefix + wstaw + suffix.TrimStart();
            _tb.CaretIndex = (prefix + wstaw).Length;
            _tlumiTextChanged = false;

            _popup.IsOpen = false;
        }

        /// <summary>Zwraca tekst aktualnie wpisywanego adresu (po ostatnim przecinku/średniku).</summary>
        private string BiezacyToken(out int start, out int caret)
        {
            string text = _tb.Text ?? "";
            caret = Math.Min(_tb.CaretIndex, text.Length);
            start = PozycjaTokenu(text, caret);
            return text.Substring(start, caret - start);
        }

        private static int PozycjaTokenu(string text, int caret)
        {
            int sep = -1;
            for (int i = Math.Min(caret, text.Length) - 1; i >= 0; i--)
            {
                if (text[i] == ',' || text[i] == ';') { sep = i; break; }
            }
            return sep + 1;
        }
    }
}
