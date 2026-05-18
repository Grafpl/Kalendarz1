using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Kalendarz1.Admin.Models;
using Kalendarz1.Admin.Services;

namespace Kalendarz1.Admin
{
    public partial class AdminPermissionsWindow : Window
    {
        private readonly AdminPermissionsService _service = new();
        private readonly PermissionTemplatesService _templatesService = new();
        private readonly ObservableCollection<AdminUserInfo> _allUsers = new();
        private readonly ObservableCollection<AdminUserInfo> _displayedUsers = new();
        private List<AdminModuleInfo> _modules = new();
        private List<PermissionTemplate> _customTemplates = new();
        private List<int> _selectedUserAssignedTemplateIds = new();
        private AdminUserInfo? _selectedUser;
        private string? _copiedPermissions;

        // Powiązanie: kategoria → lista checkboxów + nagłówek (do Update / Apply preset / Save)
        private readonly Dictionary<string, List<CheckBox>> _categoryCheckboxes = new();
        private readonly Dictionary<string, CheckBox> _categoryHeaders = new();
        // Mapowanie checkbox → moduł (do save)
        private readonly Dictionary<CheckBox, AdminModuleInfo> _checkboxToModule = new();
        // Cache karty Border per checkbox — używane przez search żeby nie szukać przez VisualTreeHelper
        private readonly Dictionary<CheckBox, Border> _checkboxToCard = new();

        // Suppress flagi — wyłączają update progress bar podczas bulk operations (SelectAll, preset, paste).
        // 1 wywołanie animacji zamiast 68 → ~50× szybsze.
        private bool _suppressUpdate;

        // Debounce timer dla wyszukiwarki modułów (200 ms)
        private DispatcherTimer? _moduleSearchDebounce;

        public AdminPermissionsWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch (Exception ex) { Debug.WriteLine($"[Admin] icon: {ex.Message}"); }
            _modules = _service.GetModulesList();
            UsersList.ItemsSource = _displayedUsers;
            Loaded += async (_, _) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                UserHandlowcyManager.CreateTableIfNotExists();
            }
            catch (Exception ex) { Debug.WriteLine($"[Admin.UserHandlowcy] {ex.Message}"); }
            await _service.EnsureAccessColumnSizeAsync();
            await _service.EnsureAuditTableAsync();
            var (tmplOk, tmplError) = await _templatesService.EnsureTableExistsAsync();
            if (!tmplOk) Debug.WriteLine($"[Admin.EnsureTemplatesTable] {tmplError}");
            var (assignOk, assignError) = await _templatesService.EnsureAssignmentTableExistsAsync();
            if (!assignOk) Debug.WriteLine($"[Admin.EnsureAssignTable] {assignError}");
            await Task.WhenAll(LoadUsersAsync(), LoadCustomTemplatesAsync());
        }

        private async Task LoadCustomTemplatesAsync()
        {
            var (list, error) = await _templatesService.LoadAllAsync();
            _customTemplates = list;
            if (error != null) Debug.WriteLine($"[Admin.LoadCustomTemplates] {error}");
            RebuildPresetsMenu();
        }

        // Dynamiczne menu szablonów: wbudowane (5) + custom z DB + opcje zarządzania.
        private void RebuildPresetsMenu()
        {
            if (PresetsMenu == null) return;
            PresetsMenu.Items.Clear();

            // Wbudowane szablony
            AddPresetMenuItem("👑 Administrator (wszystko)", "admin");
            AddPresetMenuItem("👔 Kierownik (bez administracji)", "manager");
            AddPresetMenuItem("💼 Handlowiec (sprzedaż + CRM)", "sales");
            AddPresetMenuItem("📦 Magazynier (produkcja + magazyn)", "warehouse");
            AddPresetMenuItem("👁 Podgląd (tylko odczyt analiz)", "viewer");

            if (_customTemplates.Count > 0)
            {
                PresetsMenu.Items.Add(new Separator());
                var customHeader = new MenuItem
                {
                    Header = "📋  STANOWISKA / SZABLONY",
                    IsEnabled = false,
                    FontWeight = FontWeights.Bold,
                    FontSize = 10
                };
                PresetsMenu.Items.Add(customHeader);

                foreach (var t in _customTemplates)
                {
                    // Główny item: zastępuje wszystkie zaznaczenia
                    var item = new MenuItem { Header = $"{t.Icon}  {t.Name}", Tag = ("custom_replace", t) };
                    item.Click += CustomTemplateMenu_Click;
                    item.ToolTip = $"{t.Description}\n\nKliknij: zastąp aktualne uprawnienia tym szablonem.\nMożesz też użyć opcji 'Dodaj' (Shift+Klik) żeby dołączyć do istniejących.";

                    // Sub-menu: opcje
                    var replaceSub = new MenuItem { Header = $"🔄 Zastąp uprawnienia ({t.ModuleCount} modułów)", Tag = ("custom_replace", t) };
                    replaceSub.Click += CustomTemplateMenu_Click;
                    var addSub = new MenuItem { Header = $"➕ Dodaj do bieżących (komponuj)", Tag = ("custom_add", t) };
                    addSub.Click += CustomTemplateMenu_Click;
                    var subSub = new MenuItem { Header = $"➖ Usuń te uprawnienia", Tag = ("custom_remove", t) };
                    subSub.Click += CustomTemplateMenu_Click;
                    item.Items.Add(replaceSub);
                    item.Items.Add(addSub);
                    item.Items.Add(subSub);

                    PresetsMenu.Items.Add(item);
                }
            }

            PresetsMenu.Items.Add(new Separator());
            var manageItem = new MenuItem { Header = "⚙ Zarządzaj szablonami…" };
            manageItem.Click += BtnManageTemplates_Click;
            PresetsMenu.Items.Add(manageItem);

            PresetsMenu.Items.Add(new Separator());
            AddPresetMenuItem("🚫 Wyczyść wszystko", "none");
        }

        private void AddPresetMenuItem(string header, string preset)
        {
            var mi = new MenuItem { Header = header, Tag = preset };
            mi.Click += PresetMenu_Click;
            PresetsMenu.Items.Add(mi);
        }

        // ─────────────────────────────────────────────────────────────────
        // USERS
        // ─────────────────────────────────────────────────────────────────

        private async Task LoadUsersAsync()
        {
            try
            {
                // 1. Lista users + równolegle: jedno query dla all last-logins (batch)
                var usersTask = _service.LoadUsersAsync();
                var loginsTask = _service.LoadLastLoginsAsync();
                await Task.WhenAll(usersTask, loginsTask);

                var users = await usersTask;
                var lastLogins = await loginsTask;

                // 2. Pobierz count'y uprawnień równolegle dla każdego usera.
                //    UWAGA: liczymy TYLKO pozycje odpowiadające widocznym modułom w GetModulesList()
                //    — pomijamy bity dla deprecated modułów (PodsumowanieSaldOpak, AnalizaPrzychodu itp.)
                //    żeby liczba była zgodna z tym co pokazuje BuildPermissionsUI po kliknięciu.
                var accessMap = _service.GetAccessMap();
                var visibleModuleKeys = new HashSet<string>(_modules.Select(m => m.Key), StringComparer.OrdinalIgnoreCase);
                var visiblePositions = accessMap.Where(kv => visibleModuleKeys.Contains(kv.Value)).Select(kv => kv.Key).ToHashSet();

                var accessTasks = users.Select(async u =>
                {
                    var access = await _service.GetAccessStringAsync(u.ID);
                    int count = 0;
                    foreach (var pos in visiblePositions)
                    {
                        if (pos >= 0 && pos < access.Length && access[pos] == '1') count++;
                    }
                    u.EnabledCount = count;
                    if (lastLogins.TryGetValue(u.ID, out var dt)) u.LastLogin = dt;
                }).ToList();
                await Task.WhenAll(accessTasks);

                // 3. Pokaż listę userów od razu (z fallback inicjałami)
                _allUsers.Clear();
                foreach (var u in users) _allUsers.Add(u);
                ApplyUserSearch();
                LblUsersCount.Text = $"{_allUsers.Count} użytk.";
                LblStatus.Text = $"Załadowano {_allUsers.Count} użytkowników — ładowanie avatarów…";

                // Pokaż placeholder w prawym panelu (nic nie wybrane)
                await BuildPermissionsUIAsync();

                // 4. Wczytaj avatary RÓWNOLEGLE w tle (nie blokujemy UI).
                //    Każdy avatar to plik sieciowy/lokalny ~5-50 KB. Wczytanie w paralelnym Task.Run.
                _ = LoadAvatarsInBackgroundAsync(users);
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd ładowania: {ex.Message}", ToastType.Error);
                Debug.WriteLine($"[Admin.LoadUsers] {ex}");
            }
        }

        private async Task LoadAvatarsInBackgroundAsync(List<AdminUserInfo> users)
        {
            try
            {
                // Etap 1: wczytaj wszystkie avatary równolegle bez UI marshall.
                // BitmapImage po Freeze() jest bezpieczne między wątkami.
                var loadTasks = users.Select(u => Task.Run(() =>
                {
                    var bmp = AdminPermissionsService.LoadAvatarFast(u.ID);
                    return (u, bmp);
                }));
                var results = await Task.WhenAll(loadTasks);

                // Etap 2: jeden marshall UI dla całej batch (zamiast N).
                // BeginInvoke z DispatcherPriority.Background — nie blokujemy interakcji usera.
                await Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                {
                    foreach (var (u, bmp) in results)
                    {
                        if (bmp != null) u.AvatarSource = bmp;
                    }
                    LblStatus.Text = $"Załadowano {_allUsers.Count} użytkowników";
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Admin.LoadAvatars] {ex.Message}");
            }
        }

        private void ApplyUserSearch()
        {
            string q = (TxtSearchUsers.Text ?? "").Trim().ToLowerInvariant();
            _displayedUsers.Clear();
            foreach (var u in _allUsers)
            {
                if (string.IsNullOrEmpty(q) ||
                    u.Name.ToLowerInvariant().Contains(q) ||
                    u.ID.ToLowerInvariant().Contains(q))
                {
                    _displayedUsers.Add(u);
                }
            }
        }

        private void TxtSearchUsers_TextChanged(object sender, TextChangedEventArgs e) => ApplyUserSearch();

        // Wyszukiwarka modułów — debounce 200 ms + cache kart (brak VisualTreeHelper traversal).
        // Przy 68 modułach każdy keystroke wcześniej iterował przez całe drzewo wizualne.
        private void TxtSearchModules_TextChanged(object sender, TextChangedEventArgs e)
        {
            _moduleSearchDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _moduleSearchDebounce.Tick -= ModuleSearchDebounce_Tick;
            _moduleSearchDebounce.Tick += ModuleSearchDebounce_Tick;
            _moduleSearchDebounce.Stop();
            _moduleSearchDebounce.Start();
        }

        private void ModuleSearchDebounce_Tick(object? sender, EventArgs e)
        {
            _moduleSearchDebounce?.Stop();
            ApplyModuleSearch();
        }

        private void ApplyModuleSearch()
        {
            string q = (TxtSearchModules.Text ?? "").Trim().ToLowerInvariant();
            // Direct lookup z cache _checkboxToCard — 0 traversali.
            foreach (var kv in _checkboxToModule)
            {
                var module = kv.Value;
                bool match = string.IsNullOrEmpty(q)
                    || module.DisplayName.ToLowerInvariant().Contains(q)
                    || (module.Description?.ToLowerInvariant().Contains(q) ?? false)
                    || (module.Icon?.Contains(q) ?? false)
                    || module.Key.ToLowerInvariant().Contains(q);

                if (_checkboxToCard.TryGetValue(kv.Key, out var card))
                    card.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private async void UserCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is AdminUserInfo user)
            {
                // Selected highlight: odznacz poprzedniego, zaznacz nowego
                if (_selectedUser != null) _selectedUser.IsSelected = false;
                _selectedUser = user;
                user.IsSelected = true;

                LblSelectedUser.Text = user.Name;
                LblSelectedUserId.Text = $"ID: {user.ID}";
                LblSelectedLastLogin.Text = user.LastLogin.HasValue
                    ? $"🕐 Ostatnie logowanie: {user.LastLoginText} ({user.LastLogin:yyyy-MM-dd HH:mm})"
                    : "🕐 Ostatnie logowanie: brak danych";

                // Loading state — krótki visual feedback aby user wiedział że klik się rejestruje
                Mouse.OverrideCursor = Cursors.Wait;
                LblStatus.Text = $"⏳ Ładowanie uprawnień: {user.Name}…";
                try
                {
                    await BuildPermissionsUIAsync();
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

        // Per-module: otwiera dialog z listą modułów i checked-list userów dla wybranego modułu.
        // Tymczasowo otwiera starą WinForms wersję (z AdminPermissionsForm.cs:1955).
        // Etap 2 — przeniesienie na WPF.
        private void BtnPerModule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Otwieramy starą formę tylko po to żeby pokazać dialog "per moduł"
                // (jest to method-level dialog wewnątrz starej AdminPermissionsForm)
                var oldForm = new AdminPermissionsForm();
                oldForm.OpenPerModuleDialogFromExternal();
                oldForm.Dispose();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd otwarcia dialogu per-moduł: {ex.Message}", ToastType.Error);
                Debug.WriteLine($"[Admin.PerModule] {ex}");
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // PERMISSIONS UI
        // ─────────────────────────────────────────────────────────────────

        private async Task BuildPermissionsUIAsync()
        {
            PermissionsPanel.Children.Clear();
            _categoryCheckboxes.Clear();
            _categoryHeaders.Clear();
            _checkboxToModule.Clear();
            _checkboxToCard.Clear();

            if (_selectedUser == null)
            {
                // Placeholder gdy nic nie wybrane (zamiast pustego scrollu)
                var placeholder = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 60, 0, 0)
                };
                placeholder.Children.Add(new TextBlock
                {
                    Text = "👈", FontSize = 36, HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.5
                });
                placeholder.Children.Add(new TextBlock
                {
                    Text = "Wybierz użytkownika z listy po lewej",
                    FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 12, 0, 4)
                });
                placeholder.Children.Add(new TextBlock
                {
                    Text = "aby zarządzać jego uprawnieniami",
                    FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                PermissionsPanel.Children.Add(placeholder);
                return;
            }

            // Wczytaj access string i ustaw HasAccess w modułach
            string access = await _service.GetAccessStringAsync(_selectedUser.ID);
            _service.ApplyAccessToModules(access, _modules);

            // Pobierz przypisane szablony dla tego usera
            var (assignedIds, assignError) = await _templatesService.GetUserTemplateIdsAsync(_selectedUser.ID);
            _selectedUserAssignedTemplateIds = assignedIds;
            if (assignError != null) Debug.WriteLine($"[Admin.GetAssign] {assignError}");

            // Grupuj po kategorii
            var grouped = _modules.GroupBy(m => m.Category).OrderBy(g => _service.GetCategoryOrder(g.Key));
            foreach (var group in grouped)
            {
                BuildCategorySection(group.Key, group.ToList());
            }

            RebuildAssignedTemplatesChips();
            UpdateProgressBar();
            LblStatus.Text = $"Wybrany: {_selectedUser.Name} ({_selectedUser.EnabledCount} uprawnień, {_selectedUserAssignedTemplateIds.Count} stanowisk)";
        }

        // ─────────────────────────────────────────────────────────────────────
        // CHIPSY przypisanych stanowisk/szablonów
        // ─────────────────────────────────────────────────────────────────────

        private void RebuildAssignedTemplatesChips()
        {
            if (AssignedTemplatesPanel == null) return;
            AssignedTemplatesPanel.Items.Clear();

            if (_selectedUser == null)
            {
                AssignedTemplatesBorder.Visibility = Visibility.Collapsed;
                return;
            }

            AssignedTemplatesBorder.Visibility = Visibility.Visible;

            if (_selectedUserAssignedTemplateIds.Count == 0)
            {
                AssignedTemplatesPanel.Items.Add(new TextBlock
                {
                    Text = "brak przypisań — kliknij ➕ Dodaj stanowisko aby przypisać szablon",
                    FontSize = 11,
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                return;
            }

            foreach (var tid in _selectedUserAssignedTemplateIds)
            {
                var t = _customTemplates.FirstOrDefault(x => x.Id == tid);
                if (t == null) continue;
                AssignedTemplatesPanel.Items.Add(BuildTemplateChip(t));
            }
        }

        private Border BuildTemplateChip(PermissionTemplate t)
        {
            var chip = new Border
            {
                Background = t.ColorBrush,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 3, 4, 3),
                Margin = new Thickness(0, 0, 6, 4),
                Cursor = Cursors.Hand,
                ToolTip = BuildChipTooltip(t)
            };
            // Hover: lekkie rozjaśnienie (opacity zachowuje aktualny kolor szablonu)
            chip.MouseEnter += (_, _) => chip.Opacity = 0.88;
            chip.MouseLeave += (_, _) => chip.Opacity = 1.0;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new TextBlock
            {
                Text = t.Icon, FontFamily = new FontFamily("Segoe UI Emoji"),
                FontSize = 12, Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0)
            };
            Grid.SetColumn(icon, 0); grid.Children.Add(icon);

            var name = new TextBlock
            {
                Text = t.Name, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(name, 1); grid.Children.Add(name);

            // Count badge — liczba modułów w okrągłej ramce
            var countBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(6, 0, 2, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            countBadge.Child = new TextBlock
            {
                Text = t.ModuleCount.ToString(),
                FontSize = 9.5, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            Grid.SetColumn(countBadge, 2); grid.Children.Add(countBadge);

            var closeBtn = new Button
            {
                Content = "×", FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White, Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Width = 18, Height = 18,
                Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(0, -3, 0, 0),
                Cursor = Cursors.Hand, Tag = t.Id,
                ToolTip = "Odepnij stanowisko (uprawnienia w checkboxach zostają)"
            };
            closeBtn.Click += async (_, _) => await UnassignTemplateAsync((int)closeBtn.Tag!);
            Grid.SetColumn(closeBtn, 3); grid.Children.Add(closeBtn);

            chip.Child = grid;
            return chip;
        }

        // Bogatszy tooltip — wymienia pierwsze N modułów żeby user widział co stanowisko daje
        private string BuildChipTooltip(PermissionTemplate t)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{t.Icon}  {t.Name}");
            if (!string.IsNullOrEmpty(t.Description))
            {
                sb.AppendLine(t.Description);
            }
            sb.AppendLine();
            sb.AppendLine($"Moduły ({t.ModuleCount}):");
            var keySet = new HashSet<string>(t.ModuleKeys, StringComparer.OrdinalIgnoreCase);
            var preview = _modules.Where(m => keySet.Contains(m.Key)).Take(8).ToList();
            foreach (var m in preview) sb.AppendLine($"  {m.Icon} {m.DisplayName}");
            if (t.ModuleCount > 8) sb.AppendLine($"  … i {t.ModuleCount - 8} więcej");
            sb.AppendLine();
            sb.Append("Kliknij × aby odpiąć (uprawnienia w checkboxach zostają).");
            return sb.ToString();
        }

        private void BtnAddTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null) { ShowToast("Wybierz użytkownika", ToastType.Warning); return; }
            if (_customTemplates.Count == 0)
            {
                var res = MessageBox.Show(
                    "Nie masz jeszcze żadnych szablonów. Otworzyć Zarządzanie szablonami?",
                    "Brak szablonów", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes) BtnManageTemplates_Click(sender, e);
                return;
            }

            // Wyfiltruj już przypisane
            AddTemplateMenu.Items.Clear();
            var notAssigned = _customTemplates.Where(t => !_selectedUserAssignedTemplateIds.Contains(t.Id)).ToList();

            if (notAssigned.Count == 0)
            {
                var mi = new MenuItem { Header = "(wszystkie szablony już przypisane)", IsEnabled = false };
                AddTemplateMenu.Items.Add(mi);
            }
            else
            {
                foreach (var t in notAssigned)
                {
                    var mi = new MenuItem { Header = $"{t.Icon}  {t.Name}  ({t.ModuleCount})", Tag = t.Id };
                    mi.Click += async (_, _) =>
                    {
                        if (mi.Tag is int tid) await AssignTemplateAsync(tid);
                    };
                    AddTemplateMenu.Items.Add(mi);
                }
            }

            AddTemplateMenu.Items.Add(new Separator());
            var manageMi = new MenuItem { Header = "⚙ Zarządzaj szablonami…" };
            manageMi.Click += BtnManageTemplates_Click;
            AddTemplateMenu.Items.Add(manageMi);

            if (sender is Button btn) { btn.ContextMenu.PlacementTarget = btn; btn.ContextMenu.IsOpen = true; }
        }

        private async Task AssignTemplateAsync(int templateId)
        {
            if (_selectedUser == null) return;
            var template = _customTemplates.FirstOrDefault(x => x.Id == templateId);
            if (template == null) return;

            var (ok, error) = await _templatesService.AssignTemplateAsync(_selectedUser.ID, templateId, App.UserID ?? "");
            if (!ok)
            {
                MessageBox.Show($"Błąd przypisania szablonu:\n\n{error}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Dodaj do listy + auto-zaznacz moduły z szablonu (OR z istniejącymi)
            if (!_selectedUserAssignedTemplateIds.Contains(templateId))
                _selectedUserAssignedTemplateIds.Add(templateId);

            var keySet = new HashSet<string>(template.ModuleKeys, StringComparer.OrdinalIgnoreCase);
            BulkOperation(() =>
            {
                foreach (var kv in _checkboxToModule)
                    if (keySet.Contains(kv.Value.Key)) kv.Key.IsChecked = true;
            });
            RebuildAssignedTemplatesChips();
            ShowToast($"➕ Dodano stanowisko: {template.Name}", ToastType.Success);
        }

        private async Task UnassignTemplateAsync(int templateId)
        {
            if (_selectedUser == null) return;
            var template = _customTemplates.FirstOrDefault(x => x.Id == templateId);
            string tName = template?.Name ?? $"#{templateId}";

            var (ok, error) = await _templatesService.UnassignTemplateAsync(_selectedUser.ID, templateId);
            if (!ok)
            {
                MessageBox.Show($"Błąd odpięcia szablonu:\n\n{error}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _selectedUserAssignedTemplateIds.RemoveAll(x => x == templateId);
            RebuildAssignedTemplatesChips();
            // Uprawnienia zostają — user nadal może je ręcznie odznaczyć jeśli chce
            ShowToast($"➖ Odpięto stanowisko: {tName}", ToastType.Info);
        }

        // ── Cache statycznych brushów dla kategorii (Freeze) — eliminuje duplikaty alokacji
        private static readonly Brush TextDarkBrush = CreateFrozen(31, 41, 55);
        private static readonly Brush WhiteOverlayBrush = CreateFrozen(255, 255, 255, 200);
        private static readonly Brush ModuleBorderBrush = CreateFrozen(229, 231, 235);
        private static readonly Dictionary<string, SolidColorBrush> _categoryBrushCache = new();
        private static SolidColorBrush CreateFrozen(byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromRgb(r, g, b)); br.Freeze(); return br;
        }
        private static SolidColorBrush CreateFrozen(byte r, byte g, byte b, byte a)
        {
            var br = new SolidColorBrush(Color.FromArgb(a, r, g, b)); br.Freeze(); return br;
        }
        private SolidColorBrush GetCategoryBrushCached(string category)
        {
            if (_categoryBrushCache.TryGetValue(category, out var cached)) return cached;
            var (r, g, b) = _service.GetCategoryColor(category);
            var br = new SolidColorBrush(Color.FromRgb(r, g, b)); br.Freeze();
            _categoryBrushCache[category] = br;
            return br;
        }

        private void BuildCategorySection(string category, List<AdminModuleInfo> modules)
        {
            var catBrush = GetCategoryBrushCached(category);

            // Header kategorii — kompaktowy
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

            // Count chip — biały okrąg z liczbą
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
            PermissionsPanel.Children.Add(headerBorder);
            _categoryHeaders[category] = headerCheckbox;

            // Grid modułów
            var modulesGrid = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };
            _categoryCheckboxes[category] = new List<CheckBox>();

            foreach (var module in modules)
            {
                var moduleControl = BuildModuleCard(module, catBrush, category);
                modulesGrid.Children.Add(moduleControl);
            }

            PermissionsPanel.Children.Add(modulesGrid);
            UpdateCategoryHeaderState(category);
        }

        private Border BuildModuleCard(AdminModuleInfo module, Brush categoryBrush, string category)
        {
            // Kompaktowa karta modułu: 220×30, pasek 3px po lewej + ikona + nazwa + checkbox
            var card = new Border
            {
                Width = 220,
                Height = 30,
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

            // Pasek koloru
            var bar = new Border { Background = categoryBrush };
            Grid.SetColumn(bar, 0);
            grid.Children.Add(bar);

            // Ikona
            var iconBlock = new TextBlock
            {
                Text = module.Icon,
                FontFamily = new FontFamily("Segoe UI Emoji"),
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = categoryBrush
            };
            Grid.SetColumn(iconBlock, 1);
            grid.Children.Add(iconBlock);

            // Nazwa
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

            // Checkbox
            var checkbox = new CheckBox
            {
                IsChecked = module.HasAccess,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand
            };
            checkbox.Checked += (_, _) =>
            {
                module.HasAccess = true;
                UpdateCategoryHeaderState(category);
                UpdateProgressBar();
            };
            checkbox.Unchecked += (_, _) =>
            {
                module.HasAccess = false;
                UpdateCategoryHeaderState(category);
                UpdateProgressBar();
            };
            Grid.SetColumn(checkbox, 3);
            grid.Children.Add(checkbox);
            _categoryCheckboxes[category].Add(checkbox);
            _checkboxToModule[checkbox] = module;
            _checkboxToCard[checkbox] = card;

            // Klik na kartę toggluje checkbox
            card.MouseLeftButtonDown += (_, _) => checkbox.IsChecked = !(checkbox.IsChecked ?? false);

            card.Child = grid;
            return card;
        }

        private bool _suppressCategoryHeaderEvent;
        private void CategoryHeaderChanged(string category, bool isChecked)
        {
            if (_suppressCategoryHeaderEvent) return;
            if (!_categoryCheckboxes.TryGetValue(category, out var list)) return;
            _suppressCategoryHeaderEvent = true;
            try
            {
                foreach (var cb in list) cb.IsChecked = isChecked;
            }
            finally { _suppressCategoryHeaderEvent = false; }
            UpdateProgressBar();
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
                else header.IsChecked = null; // partial state
            }
            finally { _suppressCategoryHeaderEvent = false; }
        }

        private void UpdateProgressBar()
        {
            if (_suppressUpdate) return; // bulk operation w toku — pominiemy update, finalny call odpali się po zakończeniu

            int total = _checkboxToModule.Count;
            int enabled = _checkboxToModule.Keys.Count(c => c.IsChecked == true);
            double pct = total > 0 ? (double)enabled / total : 0;
            LblProgress.Text = $"{enabled} / {total} uprawnień  ·  {pct * 100:N0}%";

            // Animacja paska — szerokość proporcjonalna do rodzica (Border parent)
            if (ProgressBarFill.Parent is Border parent && parent.ActualWidth > 0)
            {
                double targetW = parent.ActualWidth * pct;
                var anim = new DoubleAnimation
                {
                    To = targetW,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                ProgressBarFill.BeginAnimation(WidthProperty, anim);
            }

            // Aktualizuj count w karcie usera (do wyświetlenia w sidebarze)
            if (_selectedUser != null)
            {
                _selectedUser.EnabledCount = enabled;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // PRZYCISKI AKCJI
        // ─────────────────────────────────────────────────────────────────

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null) { ShowToast("Wybierz użytkownika", ToastType.Warning); return; }

            // Zsynchronizuj HasAccess w modułach (już zsynchronizowane przez bindingi, ale dla pewności)
            foreach (var kv in _checkboxToModule)
            {
                kv.Value.HasAccess = kv.Key.IsChecked == true;
            }

            bool ok = await _service.SaveAccessAsync(_selectedUser.ID, _modules, App.UserID ?? "", "manual");
            if (ok)
            {
                _selectedUser.EnabledCount = _modules.Count(m => m.HasAccess);
                ShowToast("Uprawnienia zapisane!", ToastType.Success);
            }
            else
            {
                ShowToast("Błąd zapisywania uprawnień", ToastType.Error);
            }
        }

        private async void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null) { ShowToast("Wybierz użytkownika", ToastType.Warning); return; }
            _copiedPermissions = await _service.GetAccessStringAsync(_selectedUser.ID);
            ShowToast($"Skopiowano uprawnienia: {_selectedUser.Name}", ToastType.Info);
        }

        private void BtnPaste_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null) { ShowToast("Wybierz użytkownika", ToastType.Warning); return; }
            if (string.IsNullOrEmpty(_copiedPermissions)) { ShowToast("Najpierw skopiuj uprawnienia", ToastType.Warning); return; }

            BulkOperation(() =>
            {
                _service.ApplyAccessToModules(_copiedPermissions, _modules);
                foreach (var kv in _checkboxToModule)
                    kv.Key.IsChecked = kv.Value.HasAccess;
            });
            ShowToast("Uprawnienia wklejone!", ToastType.Success);
        }

        // Bulk operations — suppress progress bar update aż do końca, potem 1× refresh.
        private void BulkOperation(Action mutate)
        {
            _suppressUpdate = true;
            try { mutate(); }
            finally
            {
                _suppressUpdate = false;
                foreach (var cat in _categoryCheckboxes.Keys.ToList()) UpdateCategoryHeaderState(cat);
                UpdateProgressBar();
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
            => BulkOperation(() => { foreach (var cb in _checkboxToModule.Keys) cb.IsChecked = true; });

        private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
            => BulkOperation(() => { foreach (var cb in _checkboxToModule.Keys) cb.IsChecked = false; });

        private void BtnInvert_Click(object sender, RoutedEventArgs e)
            => BulkOperation(() => { foreach (var cb in _checkboxToModule.Keys) cb.IsChecked = !(cb.IsChecked ?? false); });

        private void BtnPresets_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null) { ShowToast("Wybierz użytkownika", ToastType.Warning); return; }
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void PresetMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string preset)
            {
                if (_selectedUser == null) { ShowToast("Wybierz użytkownika", ToastType.Warning); return; }
                BulkOperation(() =>
                {
                    _service.ApplyPreset(_modules, preset);
                    foreach (var kv in _checkboxToModule)
                        kv.Key.IsChecked = kv.Value.HasAccess;
                });
                ShowToast($"Szablon: {AdminPermissionsService.GetPresetDisplayName(preset)}", ToastType.Info);
            }
        }

        // Custom template — 3 tryby: replace (zastąp), add (dodaj do istniejących), remove (odejmij)
        private void CustomTemplateMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null) { ShowToast("Wybierz użytkownika", ToastType.Warning); return; }
            if (sender is not MenuItem mi) return;
            if (mi.Tag is not ValueTuple<string, PermissionTemplate> tuple) return;

            var (mode, template) = tuple;
            var keySet = new HashSet<string>(template.ModuleKeys, StringComparer.OrdinalIgnoreCase);

            BulkOperation(() =>
            {
                foreach (var kv in _checkboxToModule)
                {
                    bool inTemplate = keySet.Contains(kv.Value.Key);
                    bool currentlyChecked = kv.Key.IsChecked == true;

                    bool newValue = mode switch
                    {
                        "custom_replace" => inTemplate,                       // zastąp: zaznacz dokładnie te z szablonu
                        "custom_add"     => currentlyChecked || inTemplate,   // dodaj: zachowaj + dodaj
                        "custom_remove"  => currentlyChecked && !inTemplate,  // usuń: zachowaj wszystkie OPRÓCZ tych z szablonu
                        _ => currentlyChecked
                    };
                    kv.Key.IsChecked = newValue;
                }
            });

            string actionText = mode switch
            {
                "custom_replace" => $"🔄 Zastąpiono: {template.Name}",
                "custom_add"     => $"➕ Dodano: {template.Name}",
                "custom_remove"  => $"➖ Usunięto: {template.Name}",
                _ => template.Name
            };
            ShowToast(actionText, ToastType.Info);
        }

        private async void BtnManageTemplates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new TemplateManagerWindow { Owner = this };
                win.ShowDialog();
                // Po zamknięciu — odśwież listę custom templates
                await LoadCustomTemplatesAsync();
            }
            catch (Exception ex) { ShowToast($"Błąd: {ex.Message}", ToastType.Error); }
        }

        // ─────────────────────────────────────────────────────────────────
        // SUB-DIALOGI (na razie wywołane jako WinForms / istniejące WPF)
        // ─────────────────────────────────────────────────────────────────

        private void BtnKonto_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null) { ShowToast("Wybierz użytkownika", ToastType.Warning); return; }
            try
            {
                var dlg = new AccountManagementDialog(_selectedUser.ID, _selectedUser.Name);
                dlg.Owner = this;
                dlg.ShowDialog();
            }
            catch (Exception ex) { ShowToast($"Błąd: {ex.Message}", ToastType.Error); }
        }

        private void BtnHistoria_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new LoginAuditWindow();
                win.Owner = this;
                win.Show();
            }
            catch (Exception ex) { ShowToast($"Błąd: {ex.Message}", ToastType.Error); }
        }

        private void BtnHandlowcy_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null) { ShowToast("Wybierz użytkownika", ToastType.Warning); return; }
            try
            {
                var libraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                var handel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
                var dialog = new Kalendarz1.UserHandlowcyDialog(libraNet, handel, _selectedUser.ID, _selectedUser.Name);
                dialog.HandlowcyZapisani += async (_, _) => await LoadUsersAsync();
                dialog.Show();
            }
            catch (Exception ex) { ShowToast($"Błąd: {ex.Message}", ToastType.Error); }
        }

        private void BtnEditContact_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null) { ShowToast("Wybierz użytkownika", ToastType.Warning); return; }
            try
            {
                var libraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                var dlg = new Kalendarz1.EditOperatorContactDialog(libraNet, _selectedUser.ID, _selectedUser.Name);
                dlg.ShowDialog();
            }
            catch (Exception ex) { ShowToast($"Błąd: {ex.Message}", ToastType.Error); }
        }

        private void BtnPrzydzielKlientow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var libraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                var dialog = new Kalendarz1.PrzydzielKlientowDialog(libraNet);
                dialog.Show();
            }
            catch (Exception ex) { ShowToast($"Błąd: {ex.Message}", ToastType.Error); }
        }

        private async void BtnAvatar_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null) { ShowToast("Wybierz użytkownika", ToastType.Warning); return; }
            var menu = new ContextMenu();
            var imp = new MenuItem { Header = "📷 Importuj zdjęcie..." };
            imp.Click += (_, _) => ImportAvatarFromFile();
            menu.Items.Add(imp);

            if (UserAvatarManager.HasAvatar(_selectedUser.ID))
            {
                var rem = new MenuItem { Header = "🗑 Usuń avatar" };
                rem.Click += async (_, _) =>
                {
                    if (UserAvatarManager.DeleteAvatar(_selectedUser.ID))
                    {
                        ShowToast("Avatar usunięty", ToastType.Info);
                        await LoadUsersAsync();
                    }
                };
                menu.Items.Add(rem);
            }
            if (sender is Button btn) { menu.PlacementTarget = btn; menu.IsOpen = true; }
            await Task.CompletedTask;
        }

        private async void ImportAvatarFromFile()
        {
            if (_selectedUser == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"Wybierz avatar dla {_selectedUser.Name}",
                Filter = "Pliki graficzne|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Wszystkie pliki|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    if (UserAvatarManager.SaveAvatar(_selectedUser.ID, dlg.FileName))
                    {
                        ShowToast($"Avatar zapisany dla {_selectedUser.Name}", ToastType.Success);
                        await LoadUsersAsync();
                    }
                    else
                    {
                        ShowToast("Błąd podczas zapisywania avatara", ToastType.Error);
                    }
                }
                catch (Exception ex)
                {
                    ShowToast($"Błąd: {ex.Message}", ToastType.Error);
                }
            }
        }

        private async void BtnDodajUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var libraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                using var dialog = new Kalendarz1.AddUserDialog(libraNet);
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    await LoadUsersAsync();
                    ShowToast("Użytkownik dodany", ToastType.Success);
                }
            }
            catch (Exception ex) { ShowToast($"Błąd: {ex.Message}", ToastType.Error); }
        }

        private async void BtnCloneUser_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null) { ShowToast("Wybierz najpierw użytkownika docelowego", ToastType.Warning); return; }

            // Prosty inline dialog z dropdown
            var dlg = new Window
            {
                Title = $"Klonuj uprawnienia → {_selectedUser.Name}",
                Width = 500,
                Height = 240,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(241, 244, 248)),
                FontFamily = new FontFamily("Segoe UI")
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock
            {
                Text = $"📑 Skopiuj uprawnienia od…",
                FontSize = 16, FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(title, 0); grid.Children.Add(title);

            var hint = new TextBlock
            {
                Text = $"Wybierz źródłowego użytkownika. Wszystkie checkboxy zostaną zastąpione kopią. " +
                       $"Przypisane stanowiska {_selectedUser.Name} nie zmienią się.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(hint, 1); grid.Children.Add(hint);

            var combo = new ComboBox { Padding = new Thickness(8, 5, 8, 5), FontSize = 13 };
            foreach (var u in _allUsers.Where(u => u.ID != _selectedUser.ID).OrderBy(u => u.Name))
            {
                combo.Items.Add(new ComboBoxItem
                {
                    Content = $"{u.Name}   (ID: {u.ID}, {u.EnabledCount} uprawnień)",
                    Tag = u
                });
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            Grid.SetRow(combo, 2); grid.Children.Add(combo);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            var btnCancel = new Button
            {
                Content = "Anuluj", MinWidth = 90, Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand
            };
            var btnOk = new Button
            {
                Content = "📑 Klonuj", MinWidth = 120, Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                Foreground = Brushes.White, FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            btnCancel.Click += (_, _) => dlg.DialogResult = false;
            btnOk.Click += (_, _) => dlg.DialogResult = true;
            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);
            Grid.SetRow(btnPanel, 4); grid.Children.Add(btnPanel);

            dlg.Content = grid;
            if (dlg.ShowDialog() != true) return;

            if (combo.SelectedItem is not ComboBoxItem item || item.Tag is not AdminUserInfo source) return;

            bool ok = await _service.CloneAccessAsync(_selectedUser.ID, source.ID, App.UserID ?? "");
            if (ok)
            {
                ShowToast($"📑 Skopiowano uprawnienia od: {source.Name}", ToastType.Success);
                // Reload current user's permissions UI
                await BuildPermissionsUIAsync();
                // Update count badges
                await LoadUsersAsync();
            }
            else
            {
                ShowToast("Błąd klonowania uprawnień", ToastType.Error);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null) { ShowToast("Wybierz użytkownika", ToastType.Warning); return; }

            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć użytkownika:\n\nID: {_selectedUser.ID}\nNazwa: {_selectedUser.Name}\n\nTa operacja jest nieodwracalna!",
                "Potwierdzenie usunięcia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                bool ok = await _service.DeleteUserAsync(_selectedUser.ID);
                if (ok)
                {
                    _selectedUser = null;
                    LblSelectedUser.Text = "Wybierz użytkownika z listy";
                    LblSelectedUserId.Text = "";
                    PermissionsPanel.Children.Clear();
                    _categoryCheckboxes.Clear();
                    _categoryHeaders.Clear();
                    _checkboxToModule.Clear();
                    UpdateProgressBar();
                    await LoadUsersAsync();
                    ShowToast("Użytkownik usunięty", ToastType.Success);
                }
                else
                {
                    ShowToast("Błąd usuwania", ToastType.Error);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // TOAST notifications
        // ─────────────────────────────────────────────────────────────────

        public enum ToastType { Success, Error, Warning, Info }

        private DispatcherTimer? _toastTimer;
        private void ShowToast(string message, ToastType type, int durationMs = 2500)
        {
            ToastText.Text = message;
            ToastBorder.Background = type switch
            {
                ToastType.Success => new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                ToastType.Error   => new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                ToastType.Warning => new SolidColorBrush(Color.FromRgb(243, 156, 18)),
                ToastType.Info    => new SolidColorBrush(Color.FromRgb(41, 128, 185)),
                _ => new SolidColorBrush(Color.FromRgb(45, 57, 69))
            };
            ToastBorder.Visibility = Visibility.Visible;
            ToastBorder.Opacity = 0;
            var fadeIn = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(180) };
            ToastBorder.BeginAnimation(OpacityProperty, fadeIn);

            _toastTimer?.Stop();
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
            _toastTimer.Tick += (_, _) =>
            {
                _toastTimer?.Stop();
                var fadeOut = new DoubleAnimation { From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(220) };
                fadeOut.Completed += (_, _) => ToastBorder.Visibility = Visibility.Collapsed;
                ToastBorder.BeginAnimation(OpacityProperty, fadeOut);
            };
            _toastTimer.Start();
        }
    }
}
