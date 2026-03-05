using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Kalendarz1.KartotekaTowarow
{
    public partial class ArticleEditWindow : Window
    {
        private readonly ArticleModel? _original;
        private readonly bool _isCopy;
        private readonly bool _isNew;
        private string _guid;
        private List<PhotoViewModel> _photos = new();
        private List<(string ID, string Name)> _articleList = new();

        public ArticleEditWindow(ArticleModel? article, bool isCopy)
        {
            InitializeComponent();
            _original = article;
            _isCopy = isCopy;
            _isNew = article == null || isCopy;
            _guid = _isNew ? Guid.NewGuid().ToString() : article!.GUID;

            if (_isNew && !isCopy)
                Title = "Artykul — nowy";
            else if (isCopy)
                Title = $"Artykul — kopia z [{article?.ID}]";
            else
                Title = $"Artykul — edycja [{article?.ID}]";
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadLookupsAsync();
            PopulateFields();

            if (!_isNew || _isCopy)
            {
                await LoadPhotosAsync();
                await LoadPartitionsAsync();
                await LoadKonfiguracjaAsync();
            }

            if (!_isNew)
                TxtID.IsReadOnly = true;

            UpdateLabelPreview();
        }

        // ═══════════════════════════════════════════════════════════════
        // LOOKUPS
        // ═══════════════════════════════════════════════════════════════

        private async Task LoadLookupsAsync()
        {
            try
            {
                // Rodzaje
                var rodzaje = await ArticleService.GetDistinctRodzajeAsync();
                CmbRodzaj.Items.Clear();
                CmbRodzaj.Items.Add(new ComboBoxItem { Content = "(brak)", Tag = (int?)null });
                foreach (var r in rodzaje)
                {
                    string label = r switch
                    {
                        0 => "0 - Mieso",
                        1 => "1 - Podroby",
                        2 => "2 - Odpady",
                        _ => r.ToString()
                    };
                    CmbRodzaj.Items.Add(new ComboBoxItem { Content = label, Tag = (int?)r });
                }

                // Grupy
                var grupy = await ArticleService.GetDistinctGrupyAsync();
                CmbGrupa.Items.Clear();
                CmbGrupa.Items.Add(new ComboBoxItem { Content = "(brak)", Tag = (int?)null });
                foreach (var g in grupy)
                    CmbGrupa.Items.Add(new ComboBoxItem { Content = g.ToString(), Tag = (int?)g });

                // Grupa1 — same groups for now
                CmbGrupa1.Items.Clear();
                CmbGrupa1.Items.Add(new ComboBoxItem { Content = "(brak)", Tag = (int?)null });
                foreach (var g in grupy)
                    CmbGrupa1.Items.Add(new ComboBoxItem { Content = g.ToString(), Tag = (int?)g });

                // Related articles
                _articleList = await ArticleService.GetArticleListAsync();
                var emptyItem = ("", "(brak)");
                PopulateRelatedCombo(CmbRelated1, _articleList, emptyItem);
                PopulateRelatedCombo(CmbRelated2, _articleList, emptyItem);
                PopulateRelatedCombo(CmbRelated3, _articleList, emptyItem);

                // Grupy skalowania
                var grupySkal = await ArticleService.GetDistinctGrupyScalowaniaAsync();
                CmbGrupaScalowania.Items.Clear();
                CmbGrupaScalowania.Items.Add(new ComboBoxItem { Content = "(brak)" });
                foreach (var gs in grupySkal)
                    CmbGrupaScalowania.Items.Add(new ComboBoxItem { Content = gs });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych pomocniczych:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PopulateRelatedCombo(ComboBox cmb, List<(string ID, string Name)> articles,
            (string ID, string Name) emptyItem)
        {
            cmb.Items.Clear();
            cmb.Items.Add(new ComboBoxItem { Content = emptyItem.Name, Tag = emptyItem.ID });
            foreach (var (id, name) in articles)
                cmb.Items.Add(new ComboBoxItem { Content = $"{id} - {name}", Tag = id });
        }

        // ═══════════════════════════════════════════════════════════════
        // FIELD POPULATION
        // ═══════════════════════════════════════════════════════════════

        private void PopulateFields()
        {
            if (_original == null) return;
            var a = _original;

            TxtID.Text = _isCopy ? $"{a.ID}_KOPIA" : a.ID;
            TxtName.Text = a.Name;
            TxtShortName.Text = a.ShortName;
            TxtNameLine1.Text = a.NameLine1;
            TxtNameLine2.Text = a.NameLine2;

            // JM
            SelectComboByContent(CmbJM, a.JM ?? "kg");

            // Rodzaj
            SelectComboByTag(CmbRodzaj, a.Rodzaj);

            // Grupa
            SelectComboByTag(CmbGrupa, a.Grupa);
            SelectComboByTag(CmbGrupa1, a.Grupa1);

            // Prices
            SpinCena1.EditValue = a.Cena1 ?? 0.0;
            SpinCena2.EditValue = a.Cena2 ?? 0.0;
            SpinCena3.EditValue = a.Cena3 ?? 0.0;

            // WRC, Wydajnosc, Przelicznik
            SpinWRC.EditValue = a.WRC ?? 0.0;
            SpinWydajnosc.EditValue = (double)(a.Wydajnosc ?? 0);
            SpinPrzelicznik.EditValue = a.Przelicznik ?? 0.0;

            // Halt
            ChkHalt.IsChecked = a.Halt == 1;

            // Tab 2: Etykieta
            SpinDuration.EditValue = a.Duration ?? 0;
            TxtTemp.Text = a.TempOfStorage;
            TxtIng1.Text = a.Ingredients1;
            TxtIng2.Text = a.Ingredients2;
            TxtIng3.Text = a.Ingredients3;
            TxtIng4.Text = a.Ingredients4;
            TxtIng5.Text = a.Ingredients5;
            TxtIng6.Text = a.Ingredients6;
            TxtIng7.Text = a.Ingredients7;
            TxtIng8.Text = a.Ingredients8;

            // Show extra ingredients if 1-4 are filled
            if (!string.IsNullOrWhiteSpace(a.Ingredients4))
                PanelExtraIngredients.Visibility = Visibility.Visible;

            // Tab 3: Standard
            ChkIsStandard.IsChecked = a.isStandard == 1;
            SpinStdWeight.EditValue = (double)(a.StandardWeight ?? 0);
            SpinStdTol.EditValue = (double)(a.StandardTol ?? 0);
            SpinStdTolMinus.EditValue = (double)(a.StandardTolMinus ?? 0);

            // Tab 5: Related
            SelectRelatedCombo(CmbRelated1, a.RELATED_ID1);
            SelectRelatedCombo(CmbRelated2, a.RELATED_ID2);
            SelectRelatedCombo(CmbRelated3, a.RELATED_ID3);
        }

        // ═══════════════════════════════════════════════════════════════
        // PHOTOS
        // ═══════════════════════════════════════════════════════════════

        private async Task LoadPhotosAsync()
        {
            if (_original == null) return;
            try
            {
                var photos = await ArticleService.GetPhotosAsync(_original.ID);
                _photos = photos.Select(p => new PhotoViewModel
                {
                    Id = p.Id,
                    RawData = p.Zdjecie,
                    SizeText = $"{p.RozmiarKB ?? (p.Zdjecie.Length / 1024)} KB",
                    Thumbnail = BytesToImage(p.Zdjecie)
                }).ToList();

                if (_photos.Count == 0)
                {
                    // Try fallback from ArtPartitionD
                    var fallback = await ArticleService.GetPhotoAsync(_original.ID);
                    if (fallback != null && fallback.Length > 0)
                    {
                        _photos.Add(new PhotoViewModel
                        {
                            Id = -1, // indicates fallback, not deletable
                            RawData = fallback,
                            SizeText = $"{fallback.Length / 1024} KB",
                            Thumbnail = BytesToImage(fallback)
                        });
                    }
                }

                ListPhotos.ItemsSource = _photos;
                TxtNoPhotoEdit.Visibility = _photos.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
                if (_photos.Count > 0)
                    ListPhotos.SelectedIndex = 0;
            }
            catch { }
        }

        private void ListPhotos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListPhotos.SelectedItem is PhotoViewModel photo)
            {
                ImgPreview.Source = BytesToImage(photo.RawData);
                ImgPreview.Visibility = Visibility.Visible;
            }
            else
            {
                ImgPreview.Source = null;
                ImgPreview.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnAddPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (_original == null && !_isCopy)
            {
                MessageBox.Show("Najpierw zapisz artykul, potem dodaj zdjecia.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new OpenFileDialog
            {
                Filter = "Obrazy (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Wszystkie pliki (*.*)|*.*",
                Title = "Wybierz zdjecie produktu"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var bytes = File.ReadAllBytes(dlg.FileName);
                var ext = Path.GetExtension(dlg.FileName).ToLower();
                var mime = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".bmp" => "image/bmp",
                    _ => "image/jpeg"
                };

                await ArticleService.SavePhotoAsync(_original!.ID, bytes, Path.GetFileName(dlg.FileName), mime);
                await LoadPhotosAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad dodawania zdjecia:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDeletePhoto_Click(object sender, RoutedEventArgs e)
        {
            if (ListPhotos.SelectedItem is not PhotoViewModel photo)
            {
                MessageBox.Show("Zaznacz zdjecie do usuniecia.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (photo.Id == -1)
            {
                MessageBox.Show("To zdjecie pochodzi z zestawu rozbiorowego — nie mozna go usunac tutaj.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show("Czy na pewno usunac to zdjecie?", "Potwierdzenie",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await ArticleService.DeletePhotoAsync(photo.Id);
                await LoadPhotosAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad usuwania zdjecia:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PARTITIONS
        // ═══════════════════════════════════════════════════════════════

        private async Task LoadPartitionsAsync()
        {
            if (_original == null) return;
            try
            {
                var partitions = await ArticleService.GetPartitionsAsync(_original.ID);
                ListPartitions.ItemsSource = partitions;
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════════
        // KONFIGURACJA PRODUKCJI
        // ═══════════════════════════════════════════════════════════════

        private async Task LoadKonfiguracjaAsync()
        {
            if (_original == null) return;
            try
            {
                var konfig = await ArticleService.GetKonfiguracjaAsync(_original.ID);
                if (konfig != null)
                {
                    SpinProcentUdzialu.EditValue = (double)konfig.ProcentUdzialu;
                    SelectComboByContent(CmbGrupaScalowania, konfig.GrupaScalowania ?? "");
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════════
        // LABEL PREVIEW
        // ═══════════════════════════════════════════════════════════════

        private void UpdateLabelPreview()
        {
            TxtLabelName.Text = TxtName.Text;
            RunLabelJM.Text = (CmbJM.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "kg";

            var ings = new[] { TxtIng1.Text, TxtIng2.Text, TxtIng3.Text, TxtIng4.Text,
                               TxtIng5.Text, TxtIng6.Text, TxtIng7.Text, TxtIng8.Text }
                .Where(s => !string.IsNullOrWhiteSpace(s));
            TxtLabelSklad.Text = ings.Any() ? $"Sklad: {string.Join(", ", ings)}" : "Sklad: -";

            var temp = TxtTemp.Text;
            TxtLabelTemp.Text = !string.IsNullOrWhiteSpace(temp)
                ? $"Przechowywac w temperaturze {temp}" : "";
        }

        private void Ingredients_Changed(object sender, TextChangedEventArgs e)
        {
            // Show extra ingredients if 1-4 have content
            if (!string.IsNullOrWhiteSpace(TxtIng4?.Text))
                PanelExtraIngredients.Visibility = Visibility.Visible;

            UpdateLabelPreview();
        }

        // ═══════════════════════════════════════════════════════════════
        // STANDARD WEIGHT EVENTS
        // ═══════════════════════════════════════════════════════════════

        private void ChkIsStandard_Changed(object sender, RoutedEventArgs e)
        {
            PanelStandardFields.Visibility = ChkIsStandard.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
            UpdateWeightVisualization();
        }

        private void Standard_ValueChanged(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        {
            UpdateWeightVisualization();
        }

        private void UpdateWeightVisualization()
        {
            if (SpinStdWeight == null || TxtStdWeight == null) return;

            var weight = ToDouble(SpinStdWeight.EditValue);
            var tolPlus = ToDouble(SpinStdTol.EditValue);
            var tolMinus = ToDouble(SpinStdTolMinus.EditValue);

            TxtMinWeight.Text = $"{weight - tolMinus:N2} kg";
            TxtStdWeight.Text = $"{weight:N2} kg";
            TxtMaxWeight.Text = $"{weight + tolPlus:N2} kg";
        }

        // ═══════════════════════════════════════════════════════════════
        // SAVE
        // ═══════════════════════════════════════════════════════════════

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validation
            var id = TxtID.Text?.Trim();
            var name = TxtName.Text?.Trim();

            if (string.IsNullOrEmpty(id))
            {
                MessageBox.Show("Pole ID jest wymagane.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtID.Focus();
                return;
            }
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Pole Nazwa jest wymagane.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtName.Focus();
                return;
            }

            // Check ID uniqueness
            try
            {
                var excludeGuid = _isNew ? null : _guid;
                var isUnique = await ArticleService.CheckIdUniqueAsync(id, excludeGuid);
                if (!isUnique)
                {
                    MessageBox.Show($"Artykul o ID '{id}' juz istnieje.", "Walidacja",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtID.Focus();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad walidacji:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Build model
            var article = new ArticleModel
            {
                GUID = _guid,
                ID = id,
                ShortName = NullIfEmpty(TxtShortName.Text),
                Name = name,
                Grupa = GetComboTag<int?>(CmbGrupa),
                Grupa1 = GetComboTag<int?>(CmbGrupa1),
                Cena1 = ToDoubleNullable(SpinCena1.EditValue),
                Cena2 = ToDoubleNullable(SpinCena2.EditValue),
                Cena3 = ToDoubleNullable(SpinCena3.EditValue),
                Rodzaj = GetComboTag<int?>(CmbRodzaj),
                JM = (CmbJM.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "kg",
                WRC = ToDoubleNullable(SpinWRC.EditValue),
                Wydajnosc = ToDecimalNullable(SpinWydajnosc.EditValue),
                Przelicznik = ToDoubleNullable(SpinPrzelicznik.EditValue),
                Halt = (short)(ChkHalt.IsChecked == true ? 1 : 0),
                Duration = ToIntNullable(SpinDuration.EditValue),
                TempOfStorage = NullIfEmpty(TxtTemp.Text),
                Ingredients1 = NullIfEmpty(TxtIng1.Text),
                Ingredients2 = NullIfEmpty(TxtIng2.Text),
                Ingredients3 = NullIfEmpty(TxtIng3.Text),
                Ingredients4 = NullIfEmpty(TxtIng4.Text),
                Ingredients5 = NullIfEmpty(TxtIng5.Text),
                Ingredients6 = NullIfEmpty(TxtIng6.Text),
                Ingredients7 = NullIfEmpty(TxtIng7.Text),
                Ingredients8 = NullIfEmpty(TxtIng8.Text),
                isStandard = (short)(ChkIsStandard.IsChecked == true ? 1 : 0),
                StandardWeight = ToDecimalNullable(SpinStdWeight.EditValue),
                StandardTol = ToDecimalNullable(SpinStdTol.EditValue),
                StandardTolMinus = ToDecimalNullable(SpinStdTolMinus.EditValue),
                NameLine1 = NullIfEmpty(TxtNameLine1.Text),
                NameLine2 = NullIfEmpty(TxtNameLine2.Text),
                RELATED_ID1 = GetRelatedComboValue(CmbRelated1),
                RELATED_ID2 = GetRelatedComboValue(CmbRelated2),
                RELATED_ID3 = GetRelatedComboValue(CmbRelated3)
            };

            // Validate standard weight
            if (article.isStandard == 1)
            {
                if ((article.StandardWeight ?? 0) <= 0)
                {
                    MessageBox.Show("Dla artykulu standardowego waga musi byc > 0.", "Walidacja",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    TabMain.SelectedIndex = 2;
                    return;
                }
            }

            // Save
            try
            {
                BtnSave.IsEnabled = false;
                BtnSave.Content = "  Zapisywanie...  ";

                if (_isNew)
                    await ArticleService.InsertAsync(article);
                else
                    await ArticleService.UpdateAsync(article);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisu:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSave.IsEnabled = true;
                BtnSave.Content = "  ZAPISZ  ";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static string? NullIfEmpty(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static double ToDouble(object? value) =>
            value switch
            {
                double d => d,
                decimal dec => (double)dec,
                int i => i,
                _ => 0
            };

        private static double? ToDoubleNullable(object? value)
        {
            var d = ToDouble(value);
            return d == 0 ? null : d;
        }

        private static decimal? ToDecimalNullable(object? value) =>
            value switch
            {
                decimal d => d == 0 ? null : d,
                double dbl => dbl == 0 ? null : (decimal)dbl,
                int i => i == 0 ? null : i,
                _ => null
            };

        private static int? ToIntNullable(object? value) =>
            value switch
            {
                int i => i == 0 ? null : i,
                double d => d == 0 ? null : (int)d,
                decimal dec => dec == 0 ? null : (int)dec,
                _ => null
            };

        private static T? GetComboTag<T>(ComboBox cmb)
        {
            if (cmb.SelectedItem is ComboBoxItem item && item.Tag is T val)
                return val;
            return default;
        }

        private static string? GetRelatedComboValue(ComboBox cmb)
        {
            if (cmb.SelectedItem is ComboBoxItem item && item.Tag is string tag && !string.IsNullOrEmpty(tag))
                return tag;
            return null;
        }

        private static void SelectComboByContent(ComboBox cmb, string content)
        {
            foreach (var item in cmb.Items)
            {
                if (item is ComboBoxItem ci && ci.Content?.ToString() == content)
                {
                    cmb.SelectedItem = ci;
                    return;
                }
            }
        }

        private static void SelectComboByTag(ComboBox cmb, object? tagValue)
        {
            foreach (var item in cmb.Items)
            {
                if (item is ComboBoxItem ci)
                {
                    if (tagValue == null && ci.Tag == null)
                    {
                        cmb.SelectedItem = ci;
                        return;
                    }
                    if (ci.Tag != null && ci.Tag.Equals(tagValue))
                    {
                        cmb.SelectedItem = ci;
                        return;
                    }
                }
            }
            // Fallback: select first
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }

        private static void SelectRelatedCombo(ComboBox cmb, string? articleId)
        {
            if (string.IsNullOrEmpty(articleId))
            {
                if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
                return;
            }
            foreach (var item in cmb.Items)
            {
                if (item is ComboBoxItem ci && ci.Tag is string tag && tag == articleId)
                {
                    cmb.SelectedItem = ci;
                    return;
                }
            }
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
                image.DecodePixelWidth = 200; // thumbnail size
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch { return null; }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PHOTO VIEW MODEL
    // ═══════════════════════════════════════════════════════════════

    public class PhotoViewModel
    {
        public int Id { get; set; }
        public byte[] RawData { get; set; } = Array.Empty<byte>();
        public string SizeText { get; set; } = "";
        public BitmapImage? Thumbnail { get; set; }
    }
}
