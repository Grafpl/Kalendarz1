using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Admin.Models;
using Kalendarz1.Admin.Services;

namespace Kalendarz1.Admin
{
    public partial class TemplateManagerWindow : Window
    {
        private readonly PermissionTemplatesService _templatesService = new();
        private readonly AdminPermissionsService _permService = new();
        private readonly ObservableCollection<PermissionTemplate> _templates = new();
        private PermissionTemplate? _selectedTemplate;
        private readonly List<AdminModuleInfo> _modules;
        private readonly Dictionary<CheckBox, AdminModuleInfo> _checkboxToModule = new();
        private readonly Dictionary<string, List<CheckBox>> _categoryCheckboxes = new();
        private readonly Dictionary<string, CheckBox> _categoryHeaders = new();
        private bool _isPopulating;
        private bool _isDirty;
        private static readonly SolidColorBrush TextDarkBrush = CreateFrozen(31, 41, 55);
        private static readonly SolidColorBrush WhiteOverlayBrush = CreateFrozen(255, 255, 255, 200);
        private static readonly SolidColorBrush ModuleBorderBrush = CreateFrozen(229, 231, 235);

        private static SolidColorBrush CreateFrozen(byte r, byte g, byte b)
        { var br = new SolidColorBrush(Color.FromRgb(r, g, b)); br.Freeze(); return br; }
        private static SolidColorBrush CreateFrozen(byte r, byte g, byte b, byte a)
        { var br = new SolidColorBrush(Color.FromArgb(a, r, g, b)); br.Freeze(); return br; }

        public TemplateManagerWindow()
        {
            // Tłumimy eventy MarkDirty podczas InitializeComponent() — WPF fires
            // TextChanged/SelectionChanged dla TxtName/ColorPicker zanim LblStatus istnieje.
            _isPopulating = true;
            InitializeComponent();
            _isPopulating = false;

            try { WindowIconHelper.SetIcon(this); } catch { }
            _modules = _permService.GetModulesList();
            TemplatesList.ItemsSource = _templates;
            BuildModulesUI();
            Loaded += async (_, _) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var (ok, error) = await _templatesService.EnsureTableExistsAsync();
            if (!ok)
            {
                MessageBox.Show(
                    $"Nie udało się utworzyć/sprawdzić tabeli PermissionTemplates w bazie LibraNet:\n\n{error}\n\n" +
                    "Sprawdź uprawnienia użytkownika 'pronova' (CREATE TABLE, INSERT, UPDATE, DELETE).",
                    "Błąd inicjalizacji szablonów", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            await ReloadTemplatesAsync();
            ClearForm();
            TxtName.Focus();
        }

        // ─────────────────────────────────────────────────────────────────
        // LISTA SZABLONÓW
        // ─────────────────────────────────────────────────────────────────

        private async Task ReloadTemplatesAsync()
        {
            var (list, error) = await _templatesService.LoadAllAsync();
            _templates.Clear();
            foreach (var t in list) _templates.Add(t);
            LblTemplatesCount.Text = _templates.Count.ToString();

            if (error != null)
            {
                LblStatus.Text = $"❌ Błąd ładowania: {error}";
            }
            else if (_templates.Count == 0)
            {
                LblStatus.Text = "Brak szablonów — wpisz nazwę poniżej i kliknij 💾 Zapisz, żeby utworzyć pierwszy.";
            }
            else
            {
                LblStatus.Text = $"Załadowano {_templates.Count} szablonów";
            }
            UpdateEmptyStateVisibility();
        }

        private void UpdateEmptyStateVisibility()
        {
            if (EmptyStateHint != null)
            {
                EmptyStateHint.Visibility = _templates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void TemplateCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is PermissionTemplate t)
            {
                if (_isDirty)
                {
                    string msg = _selectedTemplate != null
                        ? $"Zapisać zmiany w \"{_selectedTemplate.Name}\" przed przełączeniem na \"{t.Name}\"?"
                        : $"Porzucić bieżący niezapisany szablon i otworzyć \"{t.Name}\"?";
                    var res = MessageBox.Show(msg, "Niezapisane zmiany",
                        MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Cancel) return;
                    if (res == MessageBoxResult.Yes && _selectedTemplate != null)
                    {
                        _ = SaveCurrentAsync(switchToTemplate: t); return;
                    }
                    // No (lub Yes ale brak selected — nowy niezapisany) → przełącz bez zapisu
                }
                SelectTemplate(t);
            }
        }

        private void SelectTemplate(PermissionTemplate t)
        {
            if (_selectedTemplate != null) _selectedTemplate.IsSelected = false;
            _selectedTemplate = t;
            t.IsSelected = true;
            PopulateForm(t);
        }

        private void PopulateForm(PermissionTemplate t)
        {
            _isPopulating = true;
            try
            {
                TxtName.Text = t.Name;
                TxtDescription.Text = t.Description;
                TxtIcon.Text = t.Icon;
                // ColorPicker — znajdź pasującą opcję, inaczej pierwsza
                bool found = false;
                foreach (ComboBoxItem item in ColorPicker.Items)
                {
                    if ((item.Tag?.ToString() ?? "") == t.Color)
                    {
                        ColorPicker.SelectedItem = item;
                        found = true;
                        break;
                    }
                }
                if (!found) ColorPicker.SelectedIndex = 0;

                // Zaznacz checkboxy zgodnie z ModuleKeys
                var keySet = new HashSet<string>(t.ModuleKeys, StringComparer.OrdinalIgnoreCase);
                foreach (var kv in _checkboxToModule)
                {
                    kv.Key.IsChecked = keySet.Contains(kv.Value.Key);
                }
                foreach (var cat in _categoryCheckboxes.Keys.ToList()) UpdateCategoryHeaderState(cat);
                UpdateModuleCount();
            }
            finally { _isPopulating = false; _isDirty = false; }
        }

        private void ClearForm()
        {
            _selectedTemplate = null;
            _isPopulating = true;
            try
            {
                TxtName.Text = "";
                TxtDescription.Text = "";
                TxtIcon.Text = "📋";
                ColorPicker.SelectedIndex = 0;
                foreach (var cb in _checkboxToModule.Keys) cb.IsChecked = false;
                foreach (var cat in _categoryCheckboxes.Keys.ToList()) UpdateCategoryHeaderState(cat);
                UpdateModuleCount();
            }
            finally { _isPopulating = false; _isDirty = false; }
        }

        // ─────────────────────────────────────────────────────────────────
        // FORMULARZ EDYCJI
        // ─────────────────────────────────────────────────────────────────

        private void BuildModulesUI()
        {
            ModulesPanel.Children.Clear();
            _checkboxToModule.Clear();
            _categoryCheckboxes.Clear();
            _categoryHeaders.Clear();

            var grouped = _modules.GroupBy(m => m.Category).OrderBy(g => _permService.GetCategoryOrder(g.Key));
            foreach (var group in grouped)
            {
                BuildCategorySection(group.Key, group.ToList());
            }
        }

        private void BuildCategorySection(string category, List<AdminModuleInfo> modules)
        {
            var (r, g, b) = _permService.GetCategoryColor(category);
            var catBrush = new SolidColorBrush(Color.FromRgb(r, g, b)); catBrush.Freeze();

            var headerBorder = new Border
            {
                Background = catBrush,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 6, 0, 3)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerCheckbox = new CheckBox
            {
                Content = category,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            headerCheckbox.Checked += (_, _) => CategoryHeaderChanged(category, true);
            headerCheckbox.Unchecked += (_, _) => CategoryHeaderChanged(category, false);
            Grid.SetColumn(headerCheckbox, 0);
            headerGrid.Children.Add(headerCheckbox);

            var countChip = new Border
            {
                Background = WhiteOverlayBrush,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 1, 6, 1),
                VerticalAlignment = VerticalAlignment.Center
            };
            countChip.Child = new TextBlock
            {
                Text = $"{modules.Count}",
                Foreground = catBrush,
                FontWeight = FontWeights.Bold,
                FontSize = 10.5
            };
            Grid.SetColumn(countChip, 1);
            headerGrid.Children.Add(countChip);

            headerBorder.Child = headerGrid;
            ModulesPanel.Children.Add(headerBorder);
            _categoryHeaders[category] = headerCheckbox;

            var wrapPanel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            _categoryCheckboxes[category] = new List<CheckBox>();

            foreach (var module in modules)
            {
                var card = BuildModuleCard(module, catBrush, category);
                wrapPanel.Children.Add(card);
            }
            ModulesPanel.Children.Add(wrapPanel);
            UpdateCategoryHeaderState(category);
        }

        private Border BuildModuleCard(AdminModuleInfo module, Brush catBrush, string category)
        {
            var card = new Border
            {
                Width = 220, Height = 30,
                BorderBrush = ModuleBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(2),
                Background = Brushes.White,
                Cursor = Cursors.Hand,
                ToolTip = module.Description
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });

            var bar = new Border { Background = catBrush };
            Grid.SetColumn(bar, 0);
            grid.Children.Add(bar);

            var iconBlock = new TextBlock
            {
                Text = module.Icon,
                FontFamily = new FontFamily("Segoe UI Emoji"),
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = catBrush
            };
            Grid.SetColumn(iconBlock, 1);
            grid.Children.Add(iconBlock);

            var nameBlock = new TextBlock
            {
                Text = module.DisplayName,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(4, 0, 2, 0),
                Foreground = TextDarkBrush
            };
            Grid.SetColumn(nameBlock, 2);
            grid.Children.Add(nameBlock);

            var checkbox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand
            };
            checkbox.Checked += (_, _) => { MarkDirty(); UpdateCategoryHeaderState(category); UpdateModuleCount(); };
            checkbox.Unchecked += (_, _) => { MarkDirty(); UpdateCategoryHeaderState(category); UpdateModuleCount(); };
            Grid.SetColumn(checkbox, 3);
            grid.Children.Add(checkbox);

            _categoryCheckboxes[category].Add(checkbox);
            _checkboxToModule[checkbox] = module;

            card.MouseLeftButtonDown += (_, _) => checkbox.IsChecked = !(checkbox.IsChecked ?? false);
            card.Child = grid;
            return card;
        }

        private bool _suppressCategoryHeaderEvent;
        private void CategoryHeaderChanged(string category, bool isChecked)
        {
            if (_suppressCategoryHeaderEvent || _isPopulating) return;
            if (!_categoryCheckboxes.TryGetValue(category, out var list)) return;
            _suppressCategoryHeaderEvent = true;
            try { foreach (var cb in list) cb.IsChecked = isChecked; }
            finally { _suppressCategoryHeaderEvent = false; }
            UpdateModuleCount();
            MarkDirty();
        }

        private void UpdateCategoryHeaderState(string category)
        {
            if (!_categoryHeaders.TryGetValue(category, out var header)) return;
            if (!_categoryCheckboxes.TryGetValue(category, out var list)) return;
            int total = list.Count;
            int enabled = list.Count(c => c.IsChecked == true);
            _suppressCategoryHeaderEvent = true;
            try
            {
                if (enabled == 0) header.IsChecked = false;
                else if (enabled == total) header.IsChecked = true;
                else header.IsChecked = null;
            }
            finally { _suppressCategoryHeaderEvent = false; }
        }

        private void UpdateModuleCount()
        {
            int total = _checkboxToModule.Count;
            int enabled = _checkboxToModule.Keys.Count(c => c.IsChecked == true);
            if (LblModuleCount != null)
                LblModuleCount.Text = $"{enabled} / {total} modułów wybranych";
        }

        private void MarkDirty()
        {
            if (_isPopulating) return;
            _isDirty = true;
            if (LblStatus == null) return;
            LblStatus.Text = _selectedTemplate != null
                ? $"Niezapisane zmiany w: {_selectedTemplate.Name}"
                : "Niezapisany nowy szablon";
        }

        // ─────────────────────────────────────────────────────────────────
        // PRZYCISKI
        // ─────────────────────────────────────────────────────────────────

        private void TxtName_TextChanged(object sender, TextChangedEventArgs e) => MarkDirty();
        private void TxtDescription_TextChanged(object sender, TextChangedEventArgs e) => MarkDirty();
        private void TxtIcon_TextChanged(object sender, TextChangedEventArgs e) => MarkDirty();
        private void ColorPicker_SelectionChanged(object sender, SelectionChangedEventArgs e) => MarkDirty();

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            if (_isDirty)
            {
                var msg = _selectedTemplate != null
                    ? $"Zapisać zmiany w \"{_selectedTemplate.Name}\" przed utworzeniem nowego?"
                    : "Porzucić bieżący niezapisany szablon i utworzyć nowy?";
                var res = MessageBox.Show(msg, "Niezapisane zmiany",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (res == MessageBoxResult.Cancel) return;
                if (res == MessageBoxResult.Yes && _selectedTemplate != null)
                {
                    _ = SaveCurrentAsync(switchToTemplate: null);
                    return;
                }
            }
            if (_selectedTemplate != null) _selectedTemplate.IsSelected = false;
            ClearForm();
            TxtName.Focus();
            LblStatus.Text = "✏ Wpisz nazwę nowego szablonu i wybierz moduły, potem 💾 Zapisz.";
        }

        private async void BtnDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTemplate == null)
            {
                LblStatus.Text = "Wybierz szablon do zduplikowania.";
                return;
            }
            var copy = new PermissionTemplate
            {
                Name = _selectedTemplate.Name + " (kopia)",
                Description = _selectedTemplate.Description,
                ModuleKeys = new List<string>(_selectedTemplate.ModuleKeys),
                Icon = _selectedTemplate.Icon,
                Color = _selectedTemplate.Color
            };
            var (newId, error) = await _templatesService.InsertAsync(copy, App.UserID ?? "");
            if (newId > 0)
            {
                await ReloadTemplatesAsync();
                var inserted = _templates.FirstOrDefault(x => x.Id == newId);
                if (inserted != null) SelectTemplate(inserted);
                LblStatus.Text = $"✅ Zduplikowano: {copy.Name}";
            }
            else
            {
                MessageBox.Show(
                    $"Nie udało się zduplikować szablonu:\n\n{error}",
                    "Błąd duplikacji", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTemplate == null)
            {
                LblStatus.Text = "Wybierz szablon do usunięcia.";
                return;
            }
            var res = MessageBox.Show(
                $"Czy na pewno usunąć szablon \"{_selectedTemplate.Name}\"?\n\nTa operacja jest nieodwracalna.",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;

            var deletedName = _selectedTemplate.Name;
            var (ok, error) = await _templatesService.DeleteAsync(_selectedTemplate.Id);
            if (ok)
            {
                await ReloadTemplatesAsync();
                ClearForm();
                LblStatus.Text = $"🗑 Usunięto: {deletedName}";
            }
            else
            {
                MessageBox.Show(
                    $"Nie udało się usunąć szablonu:\n\n{error}",
                    "Błąd usuwania", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e) => await SaveCurrentAsync(switchToTemplate: null);

        private async Task SaveCurrentAsync(PermissionTemplate? switchToTemplate)
        {
            string name = TxtName.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(name))
            {
                LblStatus.Text = "⚠ Podaj nazwę szablonu — pole jest wymagane.";
                TxtName.Focus();
                return;
            }

            var selectedKeys = _checkboxToModule
                .Where(kv => kv.Key.IsChecked == true)
                .Select(kv => kv.Value.Key)
                .ToList();

            if (selectedKeys.Count == 0)
            {
                var confirm = MessageBox.Show(
                    "Nie zaznaczono żadnych modułów. Szablon będzie pusty (przyda się tylko do 'wyczyść uprawnienia').\n\nKontynuować?",
                    "Pusty szablon", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;
            }

            string color = (ColorPicker.SelectedItem is ComboBoxItem item ? item.Tag?.ToString() : null) ?? "#3B82F6";
            string icon = TxtIcon.Text?.Trim() ?? "📋";
            if (string.IsNullOrEmpty(icon)) icon = "📋";

            if (_selectedTemplate == null)
            {
                // Nowy szablon
                var nt = new PermissionTemplate
                {
                    Name = name,
                    Description = TxtDescription.Text ?? "",
                    ModuleKeys = selectedKeys,
                    Icon = icon,
                    Color = color
                };
                var (newId, error) = await _templatesService.InsertAsync(nt, App.UserID ?? "");
                if (newId > 0)
                {
                    await ReloadTemplatesAsync();
                    var inserted = _templates.FirstOrDefault(x => x.Id == newId);
                    if (inserted != null) SelectTemplate(inserted);
                    _isDirty = false;
                    LblStatus.Text = $"✅ Utworzono szablon: {nt.Name} ({selectedKeys.Count} modułów)";
                }
                else
                {
                    MessageBox.Show(
                        $"Nie udało się zapisać szablonu:\n\n{error}\n\n" +
                        "Możliwe przyczyny: brak uprawnień INSERT na tabelę PermissionTemplates, " +
                        "tabela nie istnieje, problem z połączeniem do bazy.",
                        "Błąd zapisu", MessageBoxButton.OK, MessageBoxImage.Error);
                    LblStatus.Text = $"❌ Błąd zapisu: {error}";
                }
            }
            else
            {
                _selectedTemplate.Name = name;
                _selectedTemplate.Description = TxtDescription.Text ?? "";
                _selectedTemplate.ModuleKeys = selectedKeys;
                _selectedTemplate.Icon = icon;
                _selectedTemplate.Color = color;
                var (ok, error) = await _templatesService.UpdateAsync(_selectedTemplate);
                if (ok)
                {
                    _isDirty = false;
                    LblStatus.Text = $"✅ Zapisano: {_selectedTemplate.Name} ({selectedKeys.Count} modułów)";
                }
                else
                {
                    MessageBox.Show(
                        $"Nie udało się zapisać zmian:\n\n{error}",
                        "Błąd aktualizacji", MessageBoxButton.OK, MessageBoxImage.Error);
                    LblStatus.Text = $"❌ Błąd: {error}";
                }
            }

            if (switchToTemplate != null) SelectTemplate(switchToTemplate);
        }

        private void BtnSelectAllModules_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _checkboxToModule.Keys) cb.IsChecked = true;
        }

        private void BtnSelectNoneModules_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _checkboxToModule.Keys) cb.IsChecked = false;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_isDirty)
            {
                var res = MessageBox.Show("Niezapisane zmiany zostaną utracone. Zamknąć?",
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;
            }
            Close();
        }
    }
}
