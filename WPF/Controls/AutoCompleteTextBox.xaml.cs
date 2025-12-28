using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Kalendarz1.WPF.Controls
{
    public partial class AutoCompleteTextBox : UserControl
    {
        private CancellationTokenSource? _searchCts;
        private readonly int _debounceMs = 300;

        public AutoCompleteTextBox()
        {
            InitializeComponent();
            UpdatePlaceholderVisibility();
        }

        #region Dependency Properties

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(AutoCompleteTextBox),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnTextChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(AutoCompleteTextBox),
                new PropertyMetadata("Wyszukaj..."));

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public static readonly DependencyProperty MinSearchLengthProperty =
            DependencyProperty.Register(nameof(MinSearchLength), typeof(int), typeof(AutoCompleteTextBox),
                new PropertyMetadata(2));

        public int MinSearchLength
        {
            get => (int)GetValue(MinSearchLengthProperty);
            set => SetValue(MinSearchLengthProperty, value);
        }

        public static readonly DependencyProperty MaxSuggestionsProperty =
            DependencyProperty.Register(nameof(MaxSuggestions), typeof(int), typeof(AutoCompleteTextBox),
                new PropertyMetadata(10));

        public int MaxSuggestions
        {
            get => (int)GetValue(MaxSuggestionsProperty);
            set => SetValue(MaxSuggestionsProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler<AutoCompleteSearchEventArgs>? SearchRequested;
        public event EventHandler<AutoCompleteSuggestion>? SuggestionSelected;

        #endregion

        #region Properties

        public List<AutoCompleteSuggestion> Suggestions { get; private set; } = new();

        #endregion

        #region Methods

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AutoCompleteTextBox control)
            {
                control.UpdatePlaceholderVisibility();
            }
        }

        private void UpdatePlaceholderVisibility()
        {
            txtPlaceholder.Visibility = string.IsNullOrEmpty(Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public void SetSuggestions(IEnumerable<AutoCompleteSuggestion> suggestions)
        {
            Suggestions = suggestions.Take(MaxSuggestions).ToList();
            lstSuggestions.ItemsSource = Suggestions;
            popupSuggestions.IsOpen = Suggestions.Any();
        }

        public void ClearSuggestions()
        {
            Suggestions.Clear();
            lstSuggestions.ItemsSource = null;
            popupSuggestions.IsOpen = false;
        }

        #endregion

        #region Event Handlers

        private async void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePlaceholderVisibility();

            var searchText = txtInput.Text?.Trim();

            // Anuluj poprzednie wyszukiwanie
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            if (string.IsNullOrEmpty(searchText) || searchText.Length < MinSearchLength)
            {
                ClearSuggestions();
                return;
            }

            try
            {
                // Debounce - czekaj przed wyszukiwaniem
                await Task.Delay(_debounceMs, token);

                if (token.IsCancellationRequested) return;

                // Wywołaj event wyszukiwania
                var args = new AutoCompleteSearchEventArgs(searchText, MaxSuggestions);
                SearchRequested?.Invoke(this, args);

                // Jeśli handler ustawił wyniki synchronicznie
                if (args.Results != null)
                {
                    SetSuggestions(args.Results);
                }
            }
            catch (TaskCanceledException)
            {
                // Ignoruj anulowanie
            }
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (!popupSuggestions.IsOpen) return;

            switch (e.Key)
            {
                case Key.Down:
                    if (lstSuggestions.SelectedIndex < Suggestions.Count - 1)
                        lstSuggestions.SelectedIndex++;
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (lstSuggestions.SelectedIndex > 0)
                        lstSuggestions.SelectedIndex--;
                    e.Handled = true;
                    break;

                case Key.Enter:
                    if (lstSuggestions.SelectedItem is AutoCompleteSuggestion selected)
                    {
                        SelectSuggestion(selected);
                    }
                    e.Handled = true;
                    break;

                case Key.Escape:
                    ClearSuggestions();
                    e.Handled = true;
                    break;
            }
        }

        private void TxtInput_LostFocus(object sender, RoutedEventArgs e)
        {
            // Delay aby pozwolić na kliknięcie sugestii
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!lstSuggestions.IsMouseOver)
                {
                    ClearSuggestions();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void LstSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSuggestions.SelectedItem is AutoCompleteSuggestion selected && e.AddedItems.Count > 0)
            {
                // Wywołaj event tylko przy kliknięciu myszką
                if (Mouse.LeftButton == MouseButtonState.Pressed)
                {
                    SelectSuggestion(selected);
                }
            }
        }

        private void SelectSuggestion(AutoCompleteSuggestion suggestion)
        {
            Text = suggestion.DisplayText;
            txtInput.CaretIndex = Text.Length;
            ClearSuggestions();
            SuggestionSelected?.Invoke(this, suggestion);
        }

        #endregion
    }

    #region Models

    public class AutoCompleteSuggestion
    {
        public object? Value { get; set; }
        public string DisplayText { get; set; } = "";
        public string? SecondaryText { get; set; }
        public bool HasSecondaryText => !string.IsNullOrEmpty(SecondaryText);

        public AutoCompleteSuggestion() { }

        public AutoCompleteSuggestion(object value, string displayText, string? secondaryText = null)
        {
            Value = value;
            DisplayText = displayText;
            SecondaryText = secondaryText;
        }
    }

    public class AutoCompleteSearchEventArgs : EventArgs
    {
        public string SearchText { get; }
        public int MaxResults { get; }
        public List<AutoCompleteSuggestion>? Results { get; set; }

        public AutoCompleteSearchEventArgs(string searchText, int maxResults)
        {
            SearchText = searchText;
            MaxResults = maxResults;
        }
    }

    #endregion
}
