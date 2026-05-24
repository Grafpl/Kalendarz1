using Kalendarz1.HDI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Kalendarz1.HDI
{
    /// <summary>
    /// Picker towaru — fakturzystka klika ikonkę towaru w wierszu partii, otwiera się to okno,
    /// można wybrać świeży lub mrożony produkt z katalogu HM.TW (z miniaturą).
    /// Zwraca SelectedTowar gdy user kliknie kartę.
    /// </summary>
    public partial class TowarPickerDialog : Window
    {
        private readonly HdiService _service;
        private List<HdiService.TowarKatalog> _all = new();
        private readonly ObservableCollection<HdiService.TowarKatalog> _visible = new();

        public HdiService.TowarKatalog? SelectedTowar { get; private set; }

        public TowarPickerDialog(HdiService service)
        {
            InitializeComponent();
            try { Kalendarz1.WindowIconHelper.SetIcon(this); } catch { }
            _service = service;
            ListTowary.ItemsSource = _visible;
            Loaded += async (s, e) => await LoadAsync();
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            LblStatus.Text = "⏳ Ładowanie katalogu…";
            try
            {
                _all = await _service.GetKatalogTowarowAsync();
                ApplyFilter();
                LblStatus.Text = $"✓ Załadowano {_all.Count} towarów (świeże + mrożone)";
            }
            catch (Exception ex) { LblStatus.Text = $"⚠ Błąd: {ex.Message}"; }
        }

        private void ApplyFilter()
        {
            // Guard: RadioButton IsChecked="True" w XAML triggera Filter_Changed PODCZAS
            // InitializeComponent — gdy inne kontrolki (TxtSearch, LblCount) jeszcze nie istnieją.
            if (!IsLoaded || TxtSearch == null || LblCount == null || _all == null) return;

            string f = (TxtSearch.Text ?? "").Trim().ToLowerInvariant();
            bool tylkoSwieze = RbSwieze?.IsChecked == true;
            bool tylkoMrozone = RbMrozone?.IsChecked == true;

            _visible.Clear();
            foreach (var t in _all)
            {
                if (tylkoSwieze && t.IsMrozone) continue;
                if (tylkoMrozone && !t.IsMrozone) continue;
                if (!string.IsNullOrEmpty(f))
                {
                    if (!t.Nazwa.ToLowerInvariant().Contains(f) && !t.Kod.ToLowerInvariant().Contains(f))
                        continue;
                }
                _visible.Add(t);
            }
            LblCount.Text = $"{_visible.Count} / {_all.Count}";
        }

        private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();
        private void Search_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void Card_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is HdiService.TowarKatalog t)
            {
                SelectedTowar = t;
                DialogResult = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
