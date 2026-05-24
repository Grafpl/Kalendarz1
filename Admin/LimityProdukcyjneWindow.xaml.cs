using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Kalendarz1.Zamowienia.Services;

namespace Kalendarz1.Admin
{
    /// <summary>
    /// Panel Admin → Limity produkcyjne. Zarządzanie konfiguracją limitów (Kurczak A / Filet A / Ćwiartka / …)
    /// dla okna nowego zamówienia (NoweZamowienieTestWindow).
    /// </summary>
    public partial class LimityProdukcyjneWindow : Window
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private readonly LimityProdukcyjneService _service;
        private List<LimitProdukcyjny> _items = new();

        public static IValueConverter BoolToCheckConv { get; } = new BoolToCheckConverter();

        public LimityProdukcyjneWindow()
        {
            InitializeComponent();
            _service = new LimityProdukcyjneService(ConnLibra);
            try { Kalendarz1.WindowIconHelper.SetIcon(this); } catch { }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private void ChkOnlyActive_Changed(object sender, RoutedEventArgs e) => _ = LoadAsync();

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                // Invaliduj cache definicji w NoweZamowienieTestWindow — wymuszone fresh fetch przy następnej walidacji
                Kalendarz1.Zamowienia.Views.NoweZamowienieTestWindow.InvalidateLimitDefinicjeCache();

                LblStatus.Text = "Ładowanie...";
                bool onlyActive = ChkOnlyActive?.IsChecked == true;
                _items = await _service.GetAllAsync(tylkoAktywne: onlyActive);
                GridLimity.ItemsSource = _items;
                int aktywne = _items.Count(i => i.Aktywny);
                LblStatus.Text = onlyActive
                    ? $"Aktywnych grup: {aktywne}"
                    : $"Zdefiniowanych grup: {_items.Count} · Aktywnych: {aktywne}";

                if (_items.Count > 0)
                {
                    EmptyState.Visibility = Visibility.Collapsed;
                    GridLimity.Visibility = Visibility.Visible;
                    PreviewEmpty.Visibility = Visibility.Collapsed;
                    PreviewScroll.Visibility = Visibility.Visible;
                    GridLimity.SelectedIndex = 0;
                }
                else
                {
                    EmptyState.Visibility = Visibility.Visible;
                    GridLimity.Visibility = Visibility.Collapsed;
                    PreviewScroll.Visibility = Visibility.Collapsed;
                    PreviewEmpty.Visibility = Visibility.Visible;
                    PreviewKpiBox.Visibility = Visibility.Collapsed;
                    PreviewArticlesBox.Visibility = Visibility.Collapsed;
                    PreviewArticlesTitle.Visibility = Visibility.Collapsed;
                    PreviewArticlesCount.Visibility = Visibility.Collapsed;
                    PreviewInactiveBadge.Visibility = Visibility.Collapsed;
                    PreviewTitle.Text = "🎯 Limity produkcyjne";
                    PreviewSubtitle.Text = "Dodaj pierwszą grupę aby zacząć.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd ładowania: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GridLimity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridLimity.SelectedItem is not LimitProdukcyjny item) return;
            await LoadPreviewAsync(item);
        }

        private async System.Threading.Tasks.Task LoadPreviewAsync(LimitProdukcyjny item)
        {
            PreviewEmpty.Visibility = Visibility.Collapsed;
            PreviewScroll.Visibility = Visibility.Visible;
            PreviewTitle.Text = $"{item.Ikona} {item.NazwaGrupy}";
            PreviewSubtitle.Text = $"Wzorzec: '{item.Wzorzec}' · {item.SposobDisplay}";
            PreviewInactiveBadge.Visibility = item.Aktywny ? Visibility.Collapsed : Visibility.Visible;

            // Preview limitu dla dziś
            try
            {
                var today = DateTime.Today;
                var koszyk = new List<LimityProdukcyjneService.TowarKoszyka>(); // pusty — chcemy realne SumaInnychKg
                var evals = await _service.EvaluateAllAsync(today, koszyk, excludeOrderId: null);
                var eval = evals.FirstOrDefault(x => x.Definicja.Id == item.Id);
                if (eval != null)
                {
                    PreviewKpiBox.Visibility = Visibility.Visible;
                    PreviewKpiDate.Text = today.ToString("dd.MM.yyyy");
                    PreviewKpiPlan.Text = $"{eval.PlanKg:N0} kg";
                    PreviewKpiStan.Text = $"{eval.StanKg:N0} kg";
                    PreviewKpiLimitTitle.Text = $"🎯 LIMIT {item.ProcentLimitu:N0}%";
                    PreviewKpiLimit.Text = $"{eval.LimitKg:N0} kg";
                    PreviewKpiZam.Text = $"{eval.SumaInnychKg:N0} kg";
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Preview KPI] {ex.Message}"); }

            // Lista towarów matchujących + zdjęcia z cache
            try
            {
                var articles = await _service.PreviewMatchingArticlesAsync(item.Wzorzec);
                await Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.LoadAsync(ConnLibra);
                foreach (var a in articles)
                    a.Image = Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.Get(a.Id);

                PreviewArticlesTitle.Visibility = Visibility.Visible;
                PreviewArticlesBox.Visibility = Visibility.Visible;
                PreviewArticlesList.ItemsSource = articles;
                PreviewArticlesCount.Visibility = Visibility.Visible;
                PreviewArticlesCount.Text = articles.Count == 0
                    ? "(brak dopasowań — sprawdź wzorzec)"
                    : $"Znaleziono {articles.Count} towar(ów)";
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Preview articles] {ex.Message}"); }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new LimitProdukcyjnyEditDialog(null) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                try
                {
                    await _service.AddAsync(dlg.Result, Kalendarz1.App.UserID ?? "");
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Błąd dodawania: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not LimitProdukcyjny item) return;
            var dlg = new LimitProdukcyjnyEditDialog(item) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                try
                {
                    await _service.UpdateAsync(dlg.Result, Kalendarz1.App.UserID ?? "");
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Błąd zapisu: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not LimitProdukcyjny item) return;
            var kopia = new LimitProdukcyjny
            {
                NazwaGrupy = $"Kopia — {item.NazwaGrupy}",
                Wzorzec = item.Wzorzec,
                SposobLiczeniaPlanu = item.SposobLiczeniaPlanu,
                ProcentZKurczakaA = item.ProcentZKurczakaA,
                PlanStalyKg = item.PlanStalyKg,
                ProcentLimitu = item.ProcentLimitu,
                Aktywny = false,  // domyślnie kopia jest wyłączona — user świadomie ją włączy
                Ikona = item.Ikona,
                Kolejnosc = item.Kolejnosc + 1
            };
            try
            {
                await _service.AddAsync(kopia, Kalendarz1.App.UserID ?? "");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd duplikowania: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnToggleActive_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not LimitProdukcyjny item) return;
            item.Aktywny = !item.Aktywny;
            try
            {
                await _service.UpdateAsync(item, Kalendarz1.App.UserID ?? "");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                item.Aktywny = !item.Aktywny; // rollback
                MessageBox.Show(this, "Błąd zmiany statusu: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not LimitProdukcyjny item) return;
            var r = MessageBox.Show(this,
                $"Czy na pewno usunąć grupę '{item.NazwaGrupy}' (wzorzec: '{item.Wzorzec}')?",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            try
            {
                await _service.DeleteAsync(item.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd usuwania: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class BoolToCheckConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? "✅" : "⛔";
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
