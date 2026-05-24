using Kalendarz1.Customer360.Models;
using Kalendarz1.Customer360.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Kalendarz1.Customer360
{
    public partial class KlientPickerDialog : Window
    {
        private readonly Customer360Service _service = new();
        private string _selectedHandlowiec = "";  // pusty = wszyscy
        private CancellationTokenSource? _cts;
        private List<KlientSearchItem> _ostatnieWyniki = new();   // bieżący widok
        private const int PAGE_LIMIT = 100;                       // ile wyników max na stronę

        public KlientSearchItem? Selected { get; private set; }

        public class KlientCardVm
        {
            public int Id { get; set; }
            public string Nazwa { get; set; } = "";
            public string Inicjaly { get; set; } = "";
            public string NIP { get; set; } = "";
            public string Miasto { get; set; } = "";
            public string Handlowiec { get; set; } = "";

            public static KlientCardVm From(KlientSearchItem k) => new()
            {
                Id = k.Id,
                Nazwa = k.Nazwa,
                Inicjaly = GetInicjaly(k.Nazwa),
                NIP = string.IsNullOrWhiteSpace(k.NIP) ? "—" : k.NIP,
                Miasto = string.IsNullOrWhiteSpace(k.Miasto) ? "—" : k.Miasto,
                Handlowiec = string.IsNullOrWhiteSpace(k.Handlowiec) ? "—" : k.Handlowiec
            };

            private static string GetInicjaly(string nazwa)
            {
                if (string.IsNullOrWhiteSpace(nazwa)) return "?";
                var parts = nazwa.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
                return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
            }
        }

        public KlientPickerDialog()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            KeyDown += (s, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Build chipy handlowców z bazy (distinct, niezależnie od PAGE_LIMIT)
            await BuildHandlowcyChipsAsync();
            // Initial query: TOP 100 aktywnych klientów (najczęściej używani na górze)
            await ExecuteSearchAsync("", null);
            TxtSearch.Focus();
        }

        private async Task BuildHandlowcyChipsAsync()
        {
            HandlowcyChips.Children.Clear();
            int aktywniCount = await _service.GetAktywniCountAsync();
            var handlowcy = await _service.GetHandlowcyDistinctAsync();

            var chipAll = new ToggleButton
            {
                Content = aktywniCount > 0 ? $"Wszyscy (~{aktywniCount} aktywnych)" : "Wszyscy",
                Style = (Style)FindResource("ChipFilter"),
                IsChecked = true,
                Margin = new Thickness(0, 0, 6, 6)
            };
            chipAll.Checked += async (s, e) =>
            {
                _selectedHandlowiec = "";
                UncheckOthers(chipAll);
                await ExecuteSearchAsync(TxtSearch.Text ?? "", null);
            };
            HandlowcyChips.Children.Add(chipAll);

            foreach (var h in handlowcy)
            {
                var chip = new ToggleButton
                {
                    Content = h,
                    Style = (Style)FindResource("ChipFilter"),
                    Margin = new Thickness(0, 0, 6, 6),
                    Tag = h
                };
                chip.Checked += async (s, e) =>
                {
                    _selectedHandlowiec = (chip.Tag as string) ?? "";
                    UncheckOthers(chip);
                    await ExecuteSearchAsync(TxtSearch.Text ?? "", _selectedHandlowiec);
                };
                HandlowcyChips.Children.Add(chip);
            }
        }

        private void UncheckOthers(ToggleButton current)
        {
            foreach (var child in HandlowcyChips.Children)
            {
                if (child is ToggleButton tb && tb != current) tb.IsChecked = false;
            }
        }

        private void TxtSearch_KeyUp(object sender, KeyEventArgs e)
        {
            _ = DebouncedSearchAsync(TxtSearch.Text ?? "");
        }

        private async Task DebouncedSearchAsync(string query)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            try
            {
                await Task.Delay(250, token);
                if (token.IsCancellationRequested) return;
                await ExecuteSearchAsync(query, string.IsNullOrEmpty(_selectedHandlowiec) ? null : _selectedHandlowiec);
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
        }

        private async Task ExecuteSearchAsync(string query, string? handlowiecFilter)
        {
            try
            {
                LblCount.Text = "Szukam…";
                var wyniki = await _service.SearchKlienciAsync(query?.Trim() ?? "", PAGE_LIMIT, handlowiecFilter);

                // Jeśli service zwrócił błąd SQL — pokaż go bezpośrednio użytkownikowi
                if (!string.IsNullOrEmpty(_service.LastError) && wyniki.Count == 0)
                {
                    MessageBox.Show(this,
                        "Nie udało się pobrać klientów z HANDEL.\n\n" + _service.LastError +
                        "\n\nSprawdź połączenie z serwerem 192.168.0.112.",
                        "Błąd SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                    LblCount.Text = "Błąd SQL";
                    return;
                }

                _ostatnieWyniki = wyniki;
                var cards = wyniki.Select(KlientCardVm.From).ToList();
                ListaKlientow.ItemsSource = cards;
                LblCount.Text = cards.Count == 0
                    ? "Brak wyników"
                    : cards.Count == PAGE_LIMIT
                        ? $"{PAGE_LIMIT}+ (zawęź wyszukiwanie)"
                        : cards.Count == 1 ? "1 klient" : $"{cards.Count} klientów";
                EmptyState.Visibility = cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LblCount.Text = "Błąd";
                MessageBox.Show(this, "Błąd wyszukiwania: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ListaKlientow_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListaKlientow.SelectedItem is KlientCardVm card)
            {
                Selected = _ostatnieWyniki.FirstOrDefault(k => k.Id == card.Id);
                if (Selected != null)
                {
                    DialogResult = true;
                    Close();
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
