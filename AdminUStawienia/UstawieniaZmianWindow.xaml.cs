using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Services;

namespace Kalendarz1
{
    public partial class UstawieniaZmianWindow : Window
    {
        private List<WylaczonyUzytkownik> _wylaczenia = new();
        private List<TileItem> _tileItems = new();

        public UstawieniaZmianWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            BuildTileList();
            Loaded += async (_, _) => await LoadAllAsync();
        }

        // ═══ TILE LIST ═══
        private void BuildTileList()
        {
            var accessMap = ZmianyZamowienSettingsService.GetAccessMap();
            _tileItems = accessMap
                .OrderBy(kv => kv.Value)
                .Select(kv => new TileItem { ModuleName = kv.Value, DisplayName = $"{kv.Value}  [{kv.Key}]" })
                .ToList();
            lstKafelki.ItemsSource = _tileItems;
        }

        // ═══ TITLE BAR ═══
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ═══ LOAD ═══
        private async System.Threading.Tasks.Task LoadAllAsync()
        {
            try
            {
                txtStatus.Text = "Ładowanie...";
                txtStatus.Foreground = FindResource("BrAccentBlue") as SolidColorBrush;

                ZmianyZamowienSettingsService.InvalidateCache();
                var settings = ZmianyZamowienSettingsService.GetSettingsCached();
                ApplySettingsToUI(settings);

                _wylaczenia = await ZmianyZamowienSettingsService.GetExemptionsAsync();
                RefreshGrid();
                UpdatePreview();

                var operators = await ZmianyZamowienSettingsService.GetOperatorsAsync();
                cbOperator.ItemsSource = operators.Select(o => new OperatorItem { Id = o.Id, Name = o.Name }).ToList();

                txtStatus.Text = "Gotowe";
                txtStatus.Foreground = FindResource("BrTextMuted") as SolidColorBrush;
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Błąd ładowania";
                txtStatus.Foreground = FindResource("BrAccentRed") as SolidColorBrush;
                MessageBox.Show(this, $"Błąd ładowania ustawień:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══ UI ↔ SETTINGS ═══
        private void ApplySettingsToUI(ZmianyZamowienSettings s)
        {
            txtGodzinaPowiadomien.Text = FormatTime(s.GodzinaOdKtorejPowiadamiac);
            txtGodzinaBlokady.Text = s.GodzinaBlokadyEdycji.HasValue ? FormatTime(s.GodzinaBlokadyEdycji.Value) : "";
            chkBlokadaEdycji.IsChecked = s.CzyBlokowacEdycjePoGodzinie;

            var days = ParseDays(s.DniTygodniaAktywne);
            tglPon.IsChecked = days.Contains(1);
            tglWt.IsChecked = days.Contains(2);
            tglSr.IsChecked = days.Contains(3);
            tglCzw.IsChecked = days.Contains(4);
            tglPt.IsChecked = days.Contains(5);
            tglSob.IsChecked = days.Contains(6);
            tglNdz.IsChecked = days.Contains(0);

            // Kafelki docelowe
            var selectedTiles = (s.KafelkiDocelowe ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var tile in _tileItems)
                tile.IsSelected = selectedTiles.Contains(tile.ModuleName);
            lstKafelki.ItemsSource = null;
            lstKafelki.ItemsSource = _tileItems;

            SelectComboItem(cbRodzajPowiadomienia, s.RodzajPowiadomienia);
            txtMinKg.Text = s.MinimalnaZmianaKgDoPowiadomienia.ToString("F0");

            chkLogowanie.IsChecked = s.CzyLogowacZmianyDoHistorii;
            chkKomentarz.IsChecked = s.CzyWymagacKomentarzaPrzyZmianie;

            if (s.ModifiedAt.HasValue)
                txtLastModified.Text = $"Zapisano {s.ModifiedAt.Value:yyyy-MM-dd HH:mm} przez {s.ModifiedBy ?? "?"}";
            else
                txtLastModified.Text = "Ustawienia domyślne";
        }

        private ZmianyZamowienSettings ReadSettingsFromUI()
        {
            var s = new ZmianyZamowienSettings();
            s.GodzinaOdKtorejPowiadamiac = ParseTime(txtGodzinaPowiadomien.Text) ?? new TimeSpan(11, 0, 0);
            s.GodzinaBlokadyEdycji = ParseTime(txtGodzinaBlokady.Text);
            s.CzyBlokowacEdycjePoGodzinie = chkBlokadaEdycji.IsChecked == true;

            var days = new List<int>();
            if (tglPon.IsChecked == true) days.Add(1);
            if (tglWt.IsChecked == true) days.Add(2);
            if (tglSr.IsChecked == true) days.Add(3);
            if (tglCzw.IsChecked == true) days.Add(4);
            if (tglPt.IsChecked == true) days.Add(5);
            if (tglSob.IsChecked == true) days.Add(6);
            if (tglNdz.IsChecked == true) days.Add(0);
            s.DniTygodniaAktywne = string.Join(",", days);

            // Kafelki docelowe — zbierz zaznaczone
            var selectedTiles = _tileItems.Where(t => t.IsSelected).Select(t => t.ModuleName);
            s.KafelkiDocelowe = string.Join(",", selectedTiles);

            var selectedItem = cbRodzajPowiadomienia.SelectedItem as System.Windows.Controls.ComboBoxItem;
            s.RodzajPowiadomienia = selectedItem?.Content?.ToString() ?? "MessageBox";

            decimal.TryParse(txtMinKg.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal minKg);
            s.MinimalnaZmianaKgDoPowiadomienia = minKg;

            s.CzyLogowacZmianyDoHistorii = chkLogowanie.IsChecked == true;
            s.CzyWymagacKomentarzaPrzyZmianie = chkKomentarz.IsChecked == true;

            return s;
        }

        // ═══ PREVIEW PANEL ═══
        private void UpdatePreview()
        {
            var s = ZmianyZamowienSettingsService.GetSettingsCached();

            txtPreviewGodzina.Text = FormatTime(s.GodzinaOdKtorejPowiadamiac);

            if (s.CzyBlokowacEdycjePoGodzinie && s.GodzinaBlokadyEdycji.HasValue)
            {
                txtPreviewBlokada.Text = FormatTime(s.GodzinaBlokadyEdycji.Value);
                txtPreviewBlokada.Foreground = FindResource("BrAccentAmber") as SolidColorBrush;
                dotBlokada.Fill = FindResource("BrAccentAmber") as SolidColorBrush;
            }
            else
            {
                txtPreviewBlokada.Text = "Wyłączona";
                txtPreviewBlokada.Foreground = FindResource("BrTextSecondary") as SolidColorBrush;
                dotBlokada.Fill = FindResource("BrTextMuted") as SolidColorBrush;
            }

            txtPreviewWylaczeni.Text = _wylaczenia.Count.ToString();
            txtExemptCount.Text = _wylaczenia.Count.ToString();
        }

        // ═══ BUTTONS ═══
        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnZapisz.IsEnabled = false;
                txtStatus.Text = "Zapisywanie...";
                txtStatus.Foreground = FindResource("BrAccentBlue") as SolidColorBrush;

                var settings = ReadSettingsFromUI();
                string user = App.UserFullName ?? App.UserID ?? "admin";
                await ZmianyZamowienSettingsService.SaveSettingsAsync(settings, user);

                txtStatus.Text = "Zapisano pomyślnie";
                txtStatus.Foreground = FindResource("BrAccentGreen") as SolidColorBrush;
                await LoadAllAsync();
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Błąd zapisu";
                txtStatus.Foreground = FindResource("BrAccentRed") as SolidColorBrush;
                MessageBox.Show(this, $"Błąd zapisu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnZapisz.IsEnabled = true;
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllAsync();
        }

        private async void BtnDodajWylaczenie_Click(object sender, RoutedEventArgs e)
        {
            var selected = cbOperator.SelectedItem as OperatorItem;
            if (selected == null)
            {
                MessageBox.Show(this, "Wybierz operatora z listy.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                TimeSpan? indGodzina = ParseTime(txtIndGodzina.Text);
                string? powod = string.IsNullOrWhiteSpace(txtPowod.Text) ? null : txtPowod.Text.Trim();
                string dodanoPrzez = App.UserFullName ?? App.UserID ?? "admin";

                await ZmianyZamowienSettingsService.AddExemptionAsync(
                    selected.Id, selected.Name,
                    chkZwolniony.IsChecked == true,
                    indGodzina, powod, dodanoPrzez);

                txtIndGodzina.Text = "";
                txtPowod.Text = "";

                _wylaczenia = await ZmianyZamowienSettingsService.GetExemptionsAsync();
                RefreshGrid();
                UpdatePreview();

                txtStatus.Text = $"Dodano wyłączenie: {selected.Name}";
                txtStatus.Foreground = FindResource("BrAccentGreen") as SolidColorBrush;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Błąd dodawania:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnUsunWylaczenie_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgWylaczenia.SelectedItem as WylaczonyRow;
            if (selected == null)
            {
                MessageBox.Show(this, "Zaznacz wiersz do usunięcia.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(this, $"Usunąć wyłączenie dla {selected.UserName}?", "Potwierdź", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await ZmianyZamowienSettingsService.RemoveExemptionAsync(selected.Id);
                _wylaczenia = await ZmianyZamowienSettingsService.GetExemptionsAsync();
                RefreshGrid();
                UpdatePreview();

                txtStatus.Text = $"Usunięto wyłączenie: {selected.UserName}";
                txtStatus.Foreground = FindResource("BrAccentGreen") as SolidColorBrush;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Błąd usuwania:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══ GRID ═══
        private void RefreshGrid()
        {
            dgWylaczenia.ItemsSource = _wylaczenia.Select(w => new WylaczonyRow
            {
                Id = w.Id,
                UserName = w.UserName,
                StatusText = w.CzyZwolnionyZPowiadomien ? "Zwolniony" : "Aktywny",
                IndywidualnaGodzinaText = w.IndywidualnaGodzina.HasValue ? FormatTime(w.IndywidualnaGodzina.Value) : "—",
                Powod = w.Powod ?? "",
                DodanoPrzez = w.DodanoPrzez ?? ""
            }).ToList();
        }

        // ═══ HELPERS ═══
        private static string FormatTime(TimeSpan ts) => $"{ts.Hours:D2}:{ts.Minutes:D2}";

        private static TimeSpan? ParseTime(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (TimeSpan.TryParseExact(text.Trim(), new[] { @"hh\:mm", @"h\:mm" }, CultureInfo.InvariantCulture, out var ts))
                return ts;
            if (TimeSpan.TryParse(text.Trim(), out ts))
                return ts;
            return null;
        }

        private static HashSet<int> ParseDays(string s)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrEmpty(s)) return set;
            foreach (var p in s.Split(','))
                if (int.TryParse(p.Trim(), out int d))
                    set.Add(d);
            return set;
        }

        private static void SelectComboItem(System.Windows.Controls.ComboBox cb, string value)
        {
            for (int i = 0; i < cb.Items.Count; i++)
            {
                if (cb.Items[i] is System.Windows.Controls.ComboBoxItem item && item.Content?.ToString() == value)
                {
                    cb.SelectedIndex = i;
                    return;
                }
            }
            cb.SelectedIndex = 0;
        }

        // ═══ VIEW MODELS ═══
        public class TileItem : INotifyPropertyChanged
        {
            public string ModuleName { get; set; } = "";
            public string DisplayName { get; set; } = "";

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        private class OperatorItem
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
        }

        private class WylaczonyRow
        {
            public int Id { get; set; }
            public string UserName { get; set; } = "";
            public string StatusText { get; set; } = "";
            public string IndywidualnaGodzinaText { get; set; } = "";
            public string Powod { get; set; } = "";
            public string DodanoPrzez { get; set; } = "";
        }
    }
}
