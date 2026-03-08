using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DevExpress.Xpf.Grid;
using Microsoft.Win32;

namespace Kalendarz1.KartotekaTowarow
{
    public partial class WidokTowary : UserControl
    {
        private List<ArticleModel> _allArticles = new();
        private List<ArticleModel> _filteredArticles = new();
        private HashSet<string> _favoriteGuids = new();
        private bool _isLoading;
        private bool _initialized;
        private bool _suppressDetailPriceSave;
        private ArticleModel? _currentDetailArticle;

        public WidokTowary()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            _initialized = true;

            await LoadFiltersAsync();
            await LoadFavoritesAsync();
            await LoadDataAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        // DATA LOADING
        // ═══════════════════════════════════════════════════════════════

        private async Task LoadDataAsync()
        {
            if (_isLoading) return;
            _isLoading = true;
            TxtLoadingIndicator.Visibility = Visibility.Visible;

            try
            {
                _allArticles = await ArticleService.GetAllAsync();
                ApplyFilters();
                await UpdateStatusBarAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                TxtLoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadFiltersAsync()
        {
            try
            {
                var grupy = await ArticleService.GetDistinctGrupyAsync();
                CmbGrupa.Items.Clear();
                CmbGrupa.Items.Add(new ComboBoxItem { Content = "Wszystkie", IsSelected = true });
                foreach (var g in grupy)
                    CmbGrupa.Items.Add(new ComboBoxItem { Content = g.ToString(), Tag = g });
            }
            catch { }
        }

        private async Task LoadFavoritesAsync()
        {
            try
            {
                var userId = App.UserID ?? "default";
                _favoriteGuids = await ArticleService.GetFavoriteGuidsAsync(userId);
            }
            catch { }
        }

        private async Task UpdateStatusBarAsync()
        {
            try
            {
                var (total, active, halted) = await ArticleService.GetStatsAsync();
                TxtStatus.Text = $"Artykulow: {total}  |  aktywnych: {active}  |  wstrzymanych: {halted}  |  wyswietlanych: {_filteredArticles.Count}";
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════════
        // FILTERING
        // ═══════════════════════════════════════════════════════════════

        private void ApplyFilters()
        {
            if (!_initialized || gridArticles == null || _allArticles == null) return;

            var query = _allArticles.AsEnumerable();

            // Text search
            var searchText = TxtSzukaj?.Text?.Trim();
            if (!string.IsNullOrEmpty(searchText))
            {
                var lower = searchText.ToLower();
                query = query.Where(a =>
                    (a.ID?.ToLower().Contains(lower) == true) ||
                    (a.Name?.ToLower().Contains(lower) == true) ||
                    (a.ShortName?.ToLower().Contains(lower) == true));
            }

            // Placeholder
            if (PlaceholderText != null)
                PlaceholderText.Visibility = string.IsNullOrEmpty(TxtSzukaj?.Text)
                    ? Visibility.Visible : Visibility.Collapsed;

            // Grupa
            if (CmbGrupa?.SelectedItem is ComboBoxItem grupaItem && grupaItem.Tag is int grupaId)
                query = query.Where(a => a.Grupa == grupaId);

            // JM
            if (CmbJM?.SelectedItem is ComboBoxItem jmItem)
            {
                var jm = jmItem.Content?.ToString();
                if (jm == "kg" || jm == "szt")
                    query = query.Where(a => a.JM == jm);
            }

            // Status
            if (CmbStatus?.SelectedItem is ComboBoxItem statusItem)
            {
                switch (statusItem.Content?.ToString())
                {
                    case "Aktywne":      query = query.Where(a => a.Halt != 1); break;
                    case "Wstrzymane":   query = query.Where(a => a.Halt == 1); break;
                    case "Standardowe":  query = query.Where(a => a.isStandard == 1); break;
                    case "Bez ceny":     query = query.Where(a => a.Cena1 == null || a.Cena1 == 0); break;
                    case "Ulubione":     query = query.Where(a => _favoriteGuids.Contains(a.GUID)); break;
                }
            }

            _filteredArticles = query.ToList();
            gridArticles.ItemsSource = _filteredArticles;
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized) return;
            ApplyFilters();
        }

        // ═══════════════════════════════════════════════════════════════
        // GRID EVENTS
        // ═══════════════════════════════════════════════════════════════

        private async void TableView_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            if (gridArticles.SelectedItem is ArticleModel article)
            {
                TxtSelectHint.Visibility = Visibility.Collapsed;
                ScrollDetails.Visibility = Visibility.Visible;
                await ShowDetailsAsync(article);
            }
            else
            {
                TxtSelectHint.Visibility = Visibility.Visible;
                ScrollDetails.Visibility = Visibility.Collapsed;
                _currentDetailArticle = null;
            }
        }

        private async void TableView_CellValueChanged(object sender, CellValueChangedEventArgs e)
        {
            if (e.Row is not ArticleModel article) return;

            try
            {
                var fieldName = e.Column.FieldName;
                var value = e.Value;
                string? oldValStr = null;
                string? newValStr = value?.ToString();

                if (fieldName == "Halt")
                {
                    short haltValue = (value is bool b && b) ? (short)1 :
                                      (value is short s) ? s : (short)0;
                    oldValStr = (haltValue == 1 ? "0" : "1");
                    await ArticleService.UpdateFieldAsync(article.GUID, "Halt", haltValue);
                    newValStr = haltValue.ToString();
                }
                else if (fieldName is "Cena1" or "Cena2" or "Cena3")
                {
                    double? cenaValue = value switch
                    {
                        double d => d,
                        decimal dec => (double)dec,
                        int i => i,
                        _ => null
                    };
                    await ArticleService.UpdateFieldAsync(article.GUID, fieldName, cenaValue);
                    newValStr = cenaValue?.ToString("N2");
                }

                // Audit log
                await ArticleService.LogChangeAsync(article.GUID, article.ID, fieldName, oldValStr, newValStr);
                await UpdateStatusBarAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisu:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TableView_RowDoubleClick(object sender, RowDoubleClickEventArgs e)
        {
            if (gridArticles.SelectedItem is ArticleModel article)
            {
                e.Handled = true;
                await OpenEditWindowAsync(article, false);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DETAIL PANEL
        // ═══════════════════════════════════════════════════════════════

        private async Task ShowDetailsAsync(ArticleModel a)
        {
            _suppressDetailPriceSave = true;
            _currentDetailArticle = a;

            // Name + ID
            TxtDetailName.Text = a.Name ?? "(brak nazwy)";
            TxtDetailID.Text = $"ID: {a.ID}   Skrot: {a.ShortName ?? "-"}";

            // Favorite star
            var isFav = _favoriteGuids.Contains(a.GUID);
            BtnFavorite.Content = isFav ? "\u2605" : "\u2606"; // ★ or ☆
            BtnFavorite.Foreground = isFav
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD4, 0xAF, 0x37))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xBD, 0xC3, 0xC7));

            // Badges
            BadgeHalt.Visibility = a.Halt == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (a.isStandard == 1)
            {
                BadgeStandard.Visibility = Visibility.Visible;
                TxtBadgeStandard.Text = $"STANDARD  {a.StandardWeight} kg  (tol. -{a.StandardTolMinus}/+{a.StandardTol})";
            }
            else
            {
                BadgeStandard.Visibility = Visibility.Collapsed;
            }

            // Quick-edit prices
            SpinDetailCena1.EditValue = a.Cena1 ?? 0.0;
            SpinDetailCena2.EditValue = a.Cena2 ?? 0.0;
            SpinDetailCena3.EditValue = a.Cena3 ?? 0.0;

            // JM, WRC, Wydajnosc
            TxtDetailJM.Text = a.JM ?? "-";
            TxtDetailWRC.Text = a.WRC != null ? $"{a.WRC:P0}" : "-";
            TxtDetailWydajnosc.Text = a.Wydajnosc?.ToString("N1") ?? "-";

            // Duration + Temp
            TxtDetailDuration.Text = a.Duration != null ? $"{a.Duration} dni" : "-";
            TxtDetailTemp.Text = a.TempOfStorage ?? "-";

            // Modification
            TxtDetailModified.Text = a.ModificationData != null
                ? $"Zmodyfikowano: {a.ModificationData} {a.ModificationGodzina}"
                : "";

            // Ingredients
            var ingredients = new[] { a.Ingredients1, a.Ingredients2, a.Ingredients3, a.Ingredients4,
                                      a.Ingredients5, a.Ingredients6, a.Ingredients7, a.Ingredients8 }
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            PanelIngredients.Visibility = ingredients.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtDetailSklad.Text = string.Join(", ", ingredients);

            // Related
            var related = new[] { a.RELATED_ID1, a.RELATED_ID2, a.RELATED_ID3 }
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            PanelRelated.Visibility = related.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtRelated.Text = string.Join(", ", related);

            _suppressDetailPriceSave = false;

            // Async loads
            await LoadDetailPhotoAsync(a.ID);
            await LoadDetailPartitionsAsync(a.ID);
            await LoadDetailAuditAsync(a.GUID);
        }

        private async Task LoadDetailPhotoAsync(string articleId)
        {
            try
            {
                var photoBytes = await ArticleService.GetPhotoAsync(articleId);
                if (photoBytes != null && photoBytes.Length > 0)
                {
                    ImgArticle.Source = BytesToImage(photoBytes);
                    ImgArticle.Visibility = Visibility.Visible;
                    TxtNoPhoto.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ImgArticle.Source = null;
                    ImgArticle.Visibility = Visibility.Collapsed;
                    TxtNoPhoto.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                ImgArticle.Source = null;
                ImgArticle.Visibility = Visibility.Collapsed;
                TxtNoPhoto.Visibility = Visibility.Visible;
            }
        }

        private async Task LoadDetailPartitionsAsync(string articleId)
        {
            try
            {
                var partitions = await ArticleService.GetPartitionsAsync(articleId);
                PanelPartitions.Visibility = partitions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                ListPartitions.ItemsSource = partitions;
            }
            catch
            {
                PanelPartitions.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadDetailAuditAsync(string guid)
        {
            try
            {
                var entries = await ArticleService.GetAuditLogAsync(guid, 8);
                PanelAudit.Visibility = entries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                ListAudit.ItemsSource = entries;
            }
            catch
            {
                PanelAudit.Visibility = Visibility.Collapsed;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DETAIL PANEL - QUICK EDIT PRICES
        // ═══════════════════════════════════════════════════════════════

        private async void SpinDetailCena_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        {
            if (_suppressDetailPriceSave || _currentDetailArticle == null) return;

            var article = _currentDetailArticle;
            string fieldName;
            double? newVal;

            if (sender == SpinDetailCena1)
            {
                fieldName = "Cena1";
                newVal = ToDoubleOrNull(SpinDetailCena1.EditValue);
                var oldVal = article.Cena1;
                article.Cena1 = newVal;
                await SaveDetailFieldAsync(article, fieldName, newVal, oldVal?.ToString("N2"));
            }
            else if (sender == SpinDetailCena2)
            {
                fieldName = "Cena2";
                newVal = ToDoubleOrNull(SpinDetailCena2.EditValue);
                var oldVal = article.Cena2;
                article.Cena2 = newVal;
                await SaveDetailFieldAsync(article, fieldName, newVal, oldVal?.ToString("N2"));
            }
            else if (sender == SpinDetailCena3)
            {
                fieldName = "Cena3";
                newVal = ToDoubleOrNull(SpinDetailCena3.EditValue);
                var oldVal = article.Cena3;
                article.Cena3 = newVal;
                await SaveDetailFieldAsync(article, fieldName, newVal, oldVal?.ToString("N2"));
            }
        }

        private async Task SaveDetailFieldAsync(ArticleModel article, string field, double? value, string? oldValStr)
        {
            try
            {
                await ArticleService.UpdateFieldAsync(article.GUID, field, value);
                await ArticleService.LogChangeAsync(article.GUID, article.ID, field, oldValStr, value?.ToString("N2"));
                gridArticles.RefreshData();
                FlashSaveIndicator();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisu:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void FlashSaveIndicator()
        {
            TxtSaveIndicator.Visibility = Visibility.Visible;
            await Task.Delay(1500);
            TxtSaveIndicator.Visibility = Visibility.Collapsed;
        }

        // ═══════════════════════════════════════════════════════════════
        // FAVORITES
        // ═══════════════════════════════════════════════════════════════

        private async void BtnFavorite_Click(object sender, RoutedEventArgs e)
        {
            await ToggleFavoriteAsync();
        }

        private async Task ToggleFavoriteAsync()
        {
            if (_currentDetailArticle == null) return;
            var article = _currentDetailArticle;
            var userId = App.UserID ?? "default";

            var isNowFav = await ArticleService.ToggleFavoriteAsync(article.GUID, userId);

            if (isNowFav)
                _favoriteGuids.Add(article.GUID);
            else
                _favoriteGuids.Remove(article.GUID);

            // Update star
            BtnFavorite.Content = isNowFav ? "\u2605" : "\u2606";
            BtnFavorite.Foreground = isNowFav
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD4, 0xAF, 0x37))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xBD, 0xC3, 0xC7));

            // Re-filter if "Ulubione" filter is active
            if (CmbStatus?.SelectedItem is ComboBoxItem si && si.Content?.ToString() == "Ulubione")
                ApplyFilters();
        }

        // ═══════════════════════════════════════════════════════════════
        // DASHBOARD
        // ═══════════════════════════════════════════════════════════════

        private async void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (PanelDashboard.Visibility == Visibility.Visible)
            {
                PanelDashboard.Visibility = Visibility.Collapsed;
                return;
            }

            PanelDashboard.Visibility = Visibility.Visible;
            await LoadDashboardAsync();
        }

        private async Task LoadDashboardAsync()
        {
            try
            {
                var stats = await ArticleService.GetDashboardStatsAsync();
                DashTotal.Text = stats.Total.ToString();
                DashActive.Text = stats.Active.ToString();
                DashHalted.Text = stats.Halted.ToString();
                DashStandard.Text = stats.Standard.ToString();
                DashNoCena.Text = stats.WithoutPrice.ToString();
                DashAvgCena.Text = stats.AvgCena1.ToString("N2");
                DashGrupy.ItemsSource = stats.ByGrupa;
                DashRodzaje.ItemsSource = stats.ByRodzaj;
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════════
        // TOOLBAR ACTIONS
        // ═══════════════════════════════════════════════════════════════

        private async Task OpenEditWindowAsync(ArticleModel? article, bool isCopy)
        {
            var window = new ArticleEditWindow(article, isCopy);
            window.Owner = Window.GetWindow(this);
            if (window.ShowDialog() == true)
                await LoadDataAsync();
        }

        private async void BtnDodaj_Click(object sender, RoutedEventArgs e)
            => await OpenEditWindowAsync(null, false);

        private async void BtnEdytuj_Click(object sender, RoutedEventArgs e)
        {
            if (gridArticles.SelectedItem is ArticleModel article)
                await OpenEditWindowAsync(article, false);
        }

        private async void BtnKopiuj_Click(object sender, RoutedEventArgs e)
        {
            if (gridArticles.SelectedItem is ArticleModel article)
                await OpenEditWindowAsync(article, true);
        }

        private async void BtnWstrzymaj_Click(object sender, RoutedEventArgs e)
        {
            if (gridArticles.SelectedItem is not ArticleModel article) return;

            try
            {
                short newHalt = (article.Halt == 1) ? (short)0 : (short)1;
                string action = newHalt == 1 ? "wstrzymac" : "aktywowac";
                var confirm = MessageBox.Show(
                    $"Czy na pewno chcesz {action} artykul \"{article.Name}\" ({article.ID})?",
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;

                var oldVal = article.Halt?.ToString() ?? "0";
                await ArticleService.UpdateFieldAsync(article.GUID, "Halt", newHalt);
                await ArticleService.LogChangeAsync(article.GUID, article.ID, "Halt", oldVal, newHalt.ToString());
                article.Halt = newHalt;
                gridArticles.RefreshData();
                if (gridArticles.SelectedItem is ArticleModel sel)
                    await ShowDetailsAsync(sel);
                await UpdateStatusBarAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCompare_Click(object sender, RoutedEventArgs e)
        {
            var selected = gridArticles.SelectedItems?.Cast<ArticleModel>().ToList();
            if (selected == null || selected.Count < 2)
            {
                MessageBox.Show("Zaznacz co najmniej 2 artykuly do porownania (Ctrl+klik).",
                    "Porownywarka", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (selected.Count > 4)
            {
                MessageBox.Show("Mozna porownac maksymalnie 4 artykuly naraz.",
                    "Porownywarka", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var window = new ArticleCompareWindow(selected);
            window.Owner = Window.GetWindow(this);
            window.Show();
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (gridArticles.SelectedItem is not ArticleModel article)
            {
                MessageBox.Show("Zaznacz artykul do wydruku.", "Drukuj",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var window = new ProductCardWindow(article);
            window.Owner = Window.GetWindow(this);
            window.Show();
        }

        private void BtnExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "Pliki Excel (*.xlsx)|*.xlsx",
                    FileName = $"KartotekaTowarow_{DateTime.Now:yyyy-MM-dd}"
                };
                if (dlg.ShowDialog() == true)
                {
                    tableView.ExportToXlsx(dlg.FileName);
                    MessageBox.Show($"Wyeksportowano do:\n{dlg.FileName}", "Eksport",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad eksportu:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
            => await LoadDataAsync();

        // ═══════════════════════════════════════════════════════════════
        // KEYBOARD SHORTCUTS
        // ═══════════════════════════════════════════════════════════════

        private async void CmdDodaj_Executed(object sender, ExecutedRoutedEventArgs e)
            => await OpenEditWindowAsync(null, false);

        private async void CmdEdytuj_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (gridArticles.SelectedItem is ArticleModel article)
                await OpenEditWindowAsync(article, false);
        }

        private async void CmdOdswiez_Executed(object sender, ExecutedRoutedEventArgs e)
            => await LoadDataAsync();

        private async void CmdWstrzymaj_Executed(object sender, ExecutedRoutedEventArgs e)
            => BtnWstrzymaj_Click(sender, e);

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static double? ToDoubleOrNull(object? value)
        {
            var d = value switch
            {
                double v => v,
                decimal v => (double)v,
                int v => v,
                _ => 0.0
            };
            return d == 0 ? null : d;
        }

        private static BitmapImage? BytesToImage(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                var image = new BitmapImage();
                using var ms = new MemoryStream(bytes);
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch { return null; }
        }
    }
}
