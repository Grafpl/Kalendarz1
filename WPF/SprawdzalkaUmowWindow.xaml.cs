using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Kalendarz1.WPF
{
    public partial class SprawdzalkaUmowWindow : Window
    {
        private readonly HarmonogramDostawRepository _repo = new HarmonogramDostawRepository();
        private readonly ObservableCollection<DostawaItem> _items = new ObservableCollection<DostawaItem>();
        private CollectionViewSource _viewSource;

        private readonly Dictionary<int, string> _operatorNameCache = new Dictionary<int, string>();
        private readonly Dictionary<string, BitmapSource> _avatarCache = new Dictionary<string, BitmapSource>();

        private const int DOMYSLNY_ZAKRES_MIESIECY = 6;
        private const string UMOWY_ROOT = @"\\192.168.0.170\Install\UmowyZakupu";

        private QuickFilter _aktywnyChip = QuickFilter.Brak;
        private readonly System.Windows.Threading.DispatcherTimer _searchDebounce;

        // Undo support — last toggle (10 sec window)
        private (int Id, string Column, bool OldValue, DateTime At)? _lastToggle;

        public string UserID { get; set; } = "";

        // Static commands for keyboard bindings
        public static readonly RoutedCommand FocusSearchCmd = new RoutedCommand();
        public static readonly RoutedCommand NewContractCmd = new RoutedCommand();
        public static readonly RoutedCommand RefreshCmd = new RoutedCommand();
        public static readonly RoutedCommand ExportCsvCmd = new RoutedCommand();
        public static readonly RoutedCommand ClearFiltersCmd = new RoutedCommand();

        public SprawdzalkaUmowWindow() : this(App.UserID ?? "") { }

        public SprawdzalkaUmowWindow(string userId)
        {
            InitializeComponent();
            UserID = userId ?? "";

            _searchDebounce = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _searchDebounce.Tick += (s, e) => { _searchDebounce.Stop(); RefreshView(); };

            _viewSource = new CollectionViewSource { Source = _items };
            _viewSource.SortDescriptions.Add(new SortDescription(nameof(DostawaItem.DataOdbioru), ListSortDirection.Descending));
            _viewSource.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DostawaItem.DataOdbioru)));
            _viewSource.Filter += ViewSource_Filter;
            dgvContracts.ItemsSource = _viewSource.View;

            CommandBindings.Add(new CommandBinding(FocusSearchCmd, (s, e) => txtSearch.Focus()));
            CommandBindings.Add(new CommandBinding(NewContractCmd, (s, e) => OpenUmowaForm(null)));
            CommandBindings.Add(new CommandBinding(RefreshCmd, async (s, e) => await LoadDataAsync()));
            CommandBindings.Add(new CommandBinding(ExportCsvCmd, (s, e) => ExportCsv()));
            CommandBindings.Add(new CommandBinding(ClearFiltersCmd, (s, e) => ClearAllFilters()));

            Loaded += async (s, e) => await LoadDataAsync();
        }

        // ============ LOAD ============

        private async Task LoadDataAsync()
        {
            loadingOverlay.Visibility = Visibility.Visible;
            try
            {
                bool archiwalne = chkArchiwalne?.IsChecked == true;
                DataTable dt = await _repo.LoadDostawyAsync(archiwalne, DOMYSLNY_ZAKRES_MIESIECY);

                _items.Clear();
                foreach (DataRow r in dt.Rows)
                {
                    _items.Add(DostawaItem.FromRow(r));
                }
                RefreshView();
                UpdateStats();

                // Avatary doładujemy w tle (fire-and-forget) — UI nie blokuje na 500+ wierszach
                _ = LoadAvatarsBackgroundAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        // Progresywne ładowanie avatarów: Dispatcher Background = UI ma priorytet, avatary pojawiają się
        // wsuwając się w wolne chwile między renderingiem. Dla typowych 500-1500 wierszy zakończy się w 1-3s
        // bez najmniejszego "freezu" UI (input/scroll/klik checkboxa działa natychmiast).
        private async Task LoadAvatarsBackgroundAsync()
        {
            var snapshot = _items.ToList();
            const int batchSize = 25;
            for (int i = 0; i < snapshot.Count; i += batchSize)
            {
                int end = Math.Min(i + batchSize, snapshot.Count);
                await Dispatcher.InvokeAsync(() =>
                {
                    for (int j = i; j < end; j++)
                    {
                        var it = snapshot[j];
                        if (it.AvatarUtw == null && !string.IsNullOrWhiteSpace(it.KtoUtw))
                            it.AvatarUtw = GetOrCreateAvatar(it.KtoUtwID, it.KtoUtw);
                        if (it.AvatarWysl == null && !string.IsNullOrWhiteSpace(it.KtoWysl))
                            it.AvatarWysl = GetOrCreateAvatar(it.KtoWyslID, it.KtoWysl);
                        if (it.AvatarOtrzym == null && !string.IsNullOrWhiteSpace(it.KtoOtrzym))
                            it.AvatarOtrzym = GetOrCreateAvatar(it.KtoOtrzymID, it.KtoOtrzym);
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void RefreshView()
        {
            _viewSource.View?.Refresh();
            UpdateStats();
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            if (emptyState == null) return;
            bool isEmpty = _items.Count > 0 && (_viewSource.View?.Cast<object>().Any() == false);
            emptyState.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        // ============ FILTERING ============

        private void ViewSource_Filter(object sender, FilterEventArgs e)
        {
            var it = e.Item as DostawaItem;
            if (it == null) { e.Accepted = false; return; }

            // Search
            string q = (txtSearch?.Text ?? "").Trim();
            if (q.Length > 0)
            {
                bool hit =
                    (it.Dostawca?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (it.KtoUtw?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (it.KtoWysl?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (it.KtoOtrzym?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    it.DataOdbioru.ToString("yyyy-MM-dd").Contains(q);
                if (!hit) { e.Accepted = false; return; }
            }

            // Only incomplete
            if (chkOnlyIncomplete?.IsChecked == true)
            {
                if (it.IsPosrednik || it.IsKompletna) { e.Accepted = false; return; }
            }

            // Quick-chip
            switch (_aktywnyChip)
            {
                case QuickFilter.Dzis:
                    if (it.DataOdbioru.Date != DateTime.Today) { e.Accepted = false; return; }
                    break;
                case QuickFilter.Jutro:
                    if (it.DataOdbioru.Date != DateTime.Today.AddDays(1)) { e.Accepted = false; return; }
                    break;
                case QuickFilter.TenTydzien:
                    int days = ((int)DateTime.Today.DayOfWeek + 6) % 7;
                    DateTime mon = DateTime.Today.AddDays(-days);
                    DateTime sun = mon.AddDays(6);
                    if (it.DataOdbioru.Date < mon || it.DataOdbioru.Date > sun) { e.Accepted = false; return; }
                    break;
                case QuickFilter.Spoznione:
                    // strict: data < today, niekompletne, nie pośrednik
                    if (it.DataOdbioru.Date >= DateTime.Today || it.IsKompletna || it.IsPosrednik) { e.Accepted = false; return; }
                    break;
                case QuickFilter.TylkoMoje:
                    if (string.IsNullOrEmpty(UserID)) { e.Accepted = false; return; }
                    bool mine = it.KtoUtwID == UserID || it.KtoWyslID == UserID || it.KtoOtrzymID == UserID;
                    if (!mine) { e.Accepted = false; return; }
                    break;
            }

            e.Accepted = true;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e) => RefreshView();

        private async void Archiwalne_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            await LoadDataAsync();
        }

        private void Chip_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as ToggleButton;
            if (btn?.Tag is not string tag) return;

            var clicked = (QuickFilter)Enum.Parse(typeof(QuickFilter), tag);
            // Toggle: jeśli już aktywny → wyłącz
            _aktywnyChip = (_aktywnyChip == clicked && btn.IsChecked == false) ? QuickFilter.Brak
                         : btn.IsChecked == true ? clicked : QuickFilter.Brak;

            // Uncheck inne chipy
            foreach (var c in new[] { chipDzis, chipJutro, chipTydzien, chipSpoznione, chipMoje })
            {
                if (c != btn) c.IsChecked = false;
            }

            RefreshView();
        }

        private void ClearAllFilters()
        {
            txtSearch.Text = "";
            chkOnlyIncomplete.IsChecked = false;
            foreach (var c in new[] { chipDzis, chipJutro, chipTydzien, chipSpoznione, chipMoje })
                c.IsChecked = false;
            _aktywnyChip = QuickFilter.Brak;
            RefreshView();
        }

        // ============ STATS ============

        private void UpdateStats()
        {
            int total = _items.Count;
            int kompletne = 0, overdue = 0, today = 0, posrednicy = 0;
            DateTime t = DateTime.Today;

            foreach (var it in _items)
            {
                if (it.IsPosrednik) { posrednicy++; kompletne++; continue; }
                if (it.IsKompletna) { kompletne++; continue; }
                if (it.DataOdbioru.Date < t) overdue++;
                else if (it.DataOdbioru.Date == t) today++;
            }

            int visible = _viewSource.View?.Cast<object>().Count() ?? 0;
            lblTotal.Text = $" {visible} z {total} pozycji";
            lblOverdue.Text = $"⚠ Przeterminowane: {overdue}";
            lblToday.Text = $"⏰ Dziś do zamknięcia: {today}";
            lblKompletne.Text = $"✅ Kompletne: {kompletne}";
            lblPosrednicy.Text = $"🤝 Pośrednicy: {posrednicy}";
        }

        // ============ FLAG TOGGLE ============

        private void Flag_Click(object sender, RoutedEventArgs e)
        {
            var cb = sender as CheckBox;
            if (cb?.Tag is not string colName) return;
            if (cb.DataContext is not DostawaItem it) return;

            // CheckBox z IsChecked=OneWay - po kliknięciu IsChecked został zmieniony przez framework
            // ale binding nie odświeży OneWay automatycznie. Bierzemy logiczny stan przeciwny.
            bool currentValue = colName switch
            {
                "Utworzone" => it.Utworzone,
                "Wysłane" => it.Wyslane,
                "Otrzymane" => it.Otrzymane,
                "Posrednik" => it.IsPosrednik,
                _ => false
            };
            bool newValue = !currentValue;

            // Confirm tylko gdy COFAMY (true → false). Tickowanie idzie od razu.
            if (currentValue == true)
            {
                string msg = $"Cofnąć '{colName}' dla {it.Dostawca} ({it.DataOdbioru:yyyy-MM-dd})?";
                if (MessageBox.Show(msg, "Potwierdzenie cofnięcia",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    // Przywróć IsChecked w UI (bo framework już zmienił)
                    cb.IsChecked = currentValue;
                    return;
                }
            }

            try
            {
                int? uid = int.TryParse(UserID, out int parsed) ? parsed : (int?)null;
                _repo.UpdateFlag(it.Id, colName, newValue, uid, out bool? oldVal);
                _repo.InsertAuditLog(it.Id, colName, oldVal, newValue, uid);

                // Update local item
                ApplyFlagLocally(it, colName, newValue, uid);
                _lastToggle = (it.Id, colName, currentValue, DateTime.Now);

                // Refresh row appearance — model jest INPC, ale RowStyle triggers wymagają nowej oceny
                RefreshView();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd aktualizacji: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                cb.IsChecked = currentValue;
            }
        }

        private void ApplyFlagLocally(DostawaItem it, string colName, bool value, int? uid)
        {
            string operatorName = (value && uid.HasValue) ? GetOperatorNameCached(uid.Value) : null;
            BitmapSource avatar = (value && uid.HasValue) ? GetOrCreateAvatar(uid.Value.ToString(), operatorName) : null;

            switch (colName)
            {
                case "Utworzone":
                    it.Utworzone = value;
                    it.KtoUtw = operatorName;
                    it.KtoUtwID = uid?.ToString();
                    it.KiedyUtw = value ? DateTime.Now : (DateTime?)null;
                    it.AvatarUtw = avatar;
                    break;
                case "Wysłane":
                    it.Wyslane = value;
                    it.KtoWysl = operatorName;
                    it.KtoWyslID = uid?.ToString();
                    it.KiedyWysl = value ? DateTime.Now : (DateTime?)null;
                    it.AvatarWysl = avatar;
                    break;
                case "Otrzymane":
                    it.Otrzymane = value;
                    it.KtoOtrzym = operatorName;
                    it.KtoOtrzymID = uid?.ToString();
                    it.KiedyOtrzm = value ? DateTime.Now : (DateTime?)null;
                    it.AvatarOtrzym = avatar;
                    break;
                case "Posrednik":
                    it.IsPosrednik = value;
                    break;
            }
            it.RecomputeDerived();
        }

        // ============ DOUBLE CLICK / KEYBOARD / CONTEXT MENU ============

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Ignoruj dwuklik w nagłówku grupy (Expander)
            var dep = e.OriginalSource as System.Windows.DependencyObject;
            while (dep != null && dep is not DataGridRow && dep is not Expander)
                dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
            if (dep is Expander) return;

            // Ignoruj klik na checkbox (Flag_Click handle)
            if (e.OriginalSource is CheckBox) return;
            if (e.OriginalSource is FrameworkElement fe && fe.TemplatedParent is CheckBox) return;

            EditSelected();
        }

        private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                EditSelected();
                e.Handled = true;
            }
            else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                UndoLastToggle();
                e.Handled = true;
            }
        }

        private void EditSelected()
        {
            if (dgvContracts.SelectedItem is DostawaItem it)
                OpenUmowaForm(it.Id.ToString());
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e) => OpenUmowaForm(null);
        private void BtnEdit_Click(object sender, RoutedEventArgs e) => EditSelected();
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadDataAsync();
        private void BtnExport_Click(object sender, RoutedEventArgs e) => ExportCsv();
        private void MenuEdit_Click(object sender, RoutedEventArgs e) => EditSelected();

        private void MenuCopyLp_Click(object sender, RoutedEventArgs e)
        {
            if (dgvContracts.SelectedItem is DostawaItem it)
            {
                try { Clipboard.SetText(it.Id.ToString()); } catch { }
            }
        }

        private void MenuHistory_Click(object sender, RoutedEventArgs e)
        {
            if (dgvContracts.SelectedItem is not DostawaItem it) return;
            var history = _repo.GetAuditHistory(it.Id);
            using var dlg = new AuditHistoryDialog(it.Id, it.Dostawca ?? "?", history);
            dlg.ShowDialog();
        }

        private void MenuShowFile_Click(object sender, RoutedEventArgs e)
        {
            if (dgvContracts.SelectedItem is not DostawaItem it) return;

            if (string.IsNullOrEmpty(it.Dostawca))
            {
                MessageBox.Show("Brak nazwy dostawcy w wierszu.", "Pokaż plik",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!Directory.Exists(UMOWY_ROOT))
            {
                MessageBox.Show($"Nie można połączyć z folderem:\n{UMOWY_ROOT}\n\nSprawdź czy zasób sieciowy jest dostępny.",
                    "Folder niedostępny", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string expectedFile = $"Umowa Zakupu {it.Dostawca} {it.DataOdbioru.Day}-{it.DataOdbioru.Month}-{it.DataOdbioru.Year}.docx";
            string fullPath = Path.Combine(UMOWY_ROOT, expectedFile);

            if (File.Exists(fullPath))
            {
                OpenExplorerSelect(fullPath);
                return;
            }

            try
            {
                string pattern = $"*{SanitizeForGlob(it.Dostawca)}*.docx";
                var files = Directory.GetFiles(UMOWY_ROOT, pattern);
                if (files.Length == 0)
                {
                    Process.Start("explorer.exe", $"\"{UMOWY_ROOT}\"");
                    MessageBox.Show($"Nie znaleziono pliku dla:\n  {it.Dostawca}\n\nOtwarto folder UmowyZakupu.",
                        "Plik nie znaleziony", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (files.Length == 1) { OpenExplorerSelect(files[0]); return; }

                var best = files
                    .Select(f => new { Path = f, Diff = Math.Abs((File.GetLastWriteTime(f) - it.DataOdbioru).TotalDays) })
                    .OrderBy(x => x.Diff).First();
                OpenExplorerSelect(best.Path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd dostępu: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void OpenExplorerSelect(string fullPath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{fullPath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie można otworzyć Eksploratora: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string SanitizeForGlob(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("[", "").Replace("]", "").Replace("?", "").Replace("*", "");
        }

        // ============ UMOWA FORM (legacy WinForms) ============

        private void OpenUmowaForm(string lpOrNull)
        {
            var form = new UmowyForm(initialLp: lpOrNull, initialIdLibra: null) { UserID = App.UserID ?? UserID };
            form.FormClosed += async (s, args) => await LoadDataAsync();
            form.Show();
        }

        // ============ SORTING ============

        private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // Zachowaj grupowanie po dacie — sortuj tylko w obrębie grupy
            // Domyślnie WPF DataGrid przy sortowaniu czyści grupy. Workaround: zastąp sort descriptions.
            e.Handled = true;

            var direction = e.Column.SortDirection != ListSortDirection.Ascending
                ? ListSortDirection.Ascending : ListSortDirection.Descending;
            e.Column.SortDirection = direction;

            _viewSource.SortDescriptions.Clear();
            _viewSource.SortDescriptions.Add(new SortDescription(nameof(DostawaItem.DataOdbioru), ListSortDirection.Descending));
            if (!string.IsNullOrEmpty(e.Column.SortMemberPath))
                _viewSource.SortDescriptions.Add(new SortDescription(e.Column.SortMemberPath, direction));

            _viewSource.View?.Refresh();
        }

        // ============ EXPORT CSV ============

        private void ExportCsv()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"Umowy_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("LP;Data;Dostawca;Utworzone;Wyslane;Otrzymane;Posrednik;Auta;Sztuki;Waga;SztPoj;KtoUtw;KiedyUtw;KtoWysl;KiedyWysl;KtoOtrzym;KiedyOtrzm");

                foreach (var it in _viewSource.View.OfType<DostawaItem>())
                {
                    sb.Append(it.Id).Append(';')
                      .Append(it.DataOdbioru.ToString("yyyy-MM-dd")).Append(';')
                      .Append(Csv(it.Dostawca)).Append(';')
                      .Append(it.Utworzone ? 1 : 0).Append(';')
                      .Append(it.Wyslane ? 1 : 0).Append(';')
                      .Append(it.Otrzymane ? 1 : 0).Append(';')
                      .Append(it.IsPosrednik ? 1 : 0).Append(';')
                      .Append(it.Auta?.ToString() ?? "").Append(';')
                      .Append(it.SztukiDek?.ToString(CultureInfo.InvariantCulture) ?? "").Append(';')
                      .Append(it.WagaDek?.ToString(CultureInfo.InvariantCulture) ?? "").Append(';')
                      .Append(Csv(it.SztSzuflada)).Append(';')
                      .Append(Csv(it.KtoUtw)).Append(';')
                      .Append(it.KiedyUtw?.ToString("yyyy-MM-dd HH:mm") ?? "").Append(';')
                      .Append(Csv(it.KtoWysl)).Append(';')
                      .Append(it.KiedyWysl?.ToString("yyyy-MM-dd HH:mm") ?? "").Append(';')
                      .Append(Csv(it.KtoOtrzym)).Append(';')
                      .Append(it.KiedyOtrzm?.ToString("yyyy-MM-dd HH:mm") ?? "")
                      .AppendLine();
                }
                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                MessageBox.Show($"Wyeksportowano {_viewSource.View.OfType<DostawaItem>().Count()} wierszy.",
                    "Eksport CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd eksportu: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        // ============ UNDO ============

        private void UndoLastToggle()
        {
            if (!_lastToggle.HasValue)
            {
                SystemSounds.Beep();
                return;
            }
            var lt = _lastToggle.Value;
            if ((DateTime.Now - lt.At).TotalSeconds > 30)
            {
                MessageBox.Show("Ostatnia zmiana jest starsza niż 30 sekund — Ctrl+Z niedostępne.",
                    "Undo", MessageBoxButton.OK, MessageBoxImage.Information);
                _lastToggle = null;
                return;
            }
            var it = _items.FirstOrDefault(x => x.Id == lt.Id);
            if (it == null) return;

            try
            {
                int? uid = int.TryParse(UserID, out int parsed) ? parsed : (int?)null;
                _repo.UpdateFlag(it.Id, lt.Column, lt.OldValue, uid, out bool? _);
                _repo.InsertAuditLog(it.Id, lt.Column, !lt.OldValue, lt.OldValue, uid);
                ApplyFlagLocally(it, lt.Column, lt.OldValue, lt.OldValue ? uid : (int?)null);
                _lastToggle = null;
                RefreshView();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd undo: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static class SystemSounds
        {
            public static void Beep() { try { System.Media.SystemSounds.Beep.Play(); } catch { } }
        }

        // ============ AVATARS ============

        internal string GetOperatorNameCached(int userId)
        {
            if (_operatorNameCache.TryGetValue(userId, out var cached)) return cached;
            try
            {
                var name = _repo.GetOperatorName(userId);
                _operatorNameCache[userId] = name;
                return name;
            }
            catch { return userId.ToString(); }
        }

        internal BitmapSource GetOrCreateAvatar(string odbiorcaId, string name)
        {
            string key = odbiorcaId ?? name ?? "unknown";
            if (_avatarCache.TryGetValue(key, out var cached)) return cached;

            System.Drawing.Image avatar = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(odbiorcaId))
                    avatar = UserAvatarManager.GetAvatarRounded(odbiorcaId, 32);
                if (avatar == null && !string.IsNullOrWhiteSpace(name))
                    avatar = UserAvatarManager.GenerateDefaultAvatar(name, key, 32);
            }
            catch { }

            if (avatar == null) return null;
            var bmp = ConvertImageToBitmapSource(avatar);
            avatar.Dispose();
            if (bmp != null)
            {
                bmp.Freeze();
                _avatarCache[key] = bmp;
            }
            return bmp;
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private static BitmapSource ConvertImageToBitmapSource(System.Drawing.Image image)
        {
            if (image == null) return null;
            using var bitmap = new System.Drawing.Bitmap(image);
            var hBitmap = bitmap.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(hBitmap); }
        }

        // ============ ENUMS ============

        private enum QuickFilter { Brak, Dzis, Jutro, TenTydzien, Spoznione, TylkoMoje }
    }

    // ============ ITEM MODEL ============

    public class DostawaItem : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public DateTime DataOdbioru { get; set; }
        public string Dostawca { get; set; }

        private bool _utworzone;
        public bool Utworzone { get => _utworzone; set { _utworzone = value; OnChanged(); } }

        private bool _wyslane;
        public bool Wyslane { get => _wyslane; set { _wyslane = value; OnChanged(); } }

        private bool _otrzymane;
        public bool Otrzymane { get => _otrzymane; set { _otrzymane = value; OnChanged(); } }

        private bool _isPosrednik;
        public bool IsPosrednik { get => _isPosrednik; set { _isPosrednik = value; OnChanged(); } }

        public int? Auta { get; set; }
        public decimal? SztukiDek { get; set; }
        public decimal? WagaDek { get; set; }
        public string SztSzuflada { get; set; }

        private string _ktoUtw;
        public string KtoUtw { get => _ktoUtw; set { _ktoUtw = value; OnChanged(); } }
        public string KtoUtwID { get; set; }
        private DateTime? _kiedyUtw;
        public DateTime? KiedyUtw { get => _kiedyUtw; set { _kiedyUtw = value; OnChanged(); } }
        private BitmapSource _avatarUtw;
        public BitmapSource AvatarUtw { get => _avatarUtw; set { _avatarUtw = value; OnChanged(); } }

        private string _ktoWysl;
        public string KtoWysl { get => _ktoWysl; set { _ktoWysl = value; OnChanged(); } }
        public string KtoWyslID { get; set; }
        private DateTime? _kiedyWysl;
        public DateTime? KiedyWysl { get => _kiedyWysl; set { _kiedyWysl = value; OnChanged(); } }
        private BitmapSource _avatarWysl;
        public BitmapSource AvatarWysl { get => _avatarWysl; set { _avatarWysl = value; OnChanged(); } }

        private string _ktoOtrzym;
        public string KtoOtrzym { get => _ktoOtrzym; set { _ktoOtrzym = value; OnChanged(); } }
        public string KtoOtrzymID { get; set; }
        private DateTime? _kiedyOtrzm;
        public DateTime? KiedyOtrzm { get => _kiedyOtrzm; set { _kiedyOtrzm = value; OnChanged(); } }
        private BitmapSource _avatarOtrzym;
        public BitmapSource AvatarOtrzym { get => _avatarOtrzym; set { _avatarOtrzym = value; OnChanged(); } }

        // Derived
        public bool IsKompletna => Utworzone && Wyslane && Otrzymane;
        public bool IsToday => DataOdbioru.Date == DateTime.Today;
        public bool IsOverdue => DataOdbioru.Date < DateTime.Today && !IsKompletna && !IsPosrednik;

        // Status: priorytetyzowane od najbardziej istotnego (Overdue > Today > Posrednik > Kompletna > Pending)
        public string StatusKey
        {
            get
            {
                if (IsPosrednik) return "Posrednik";
                if (IsKompletna) return "Kompletna";
                if (IsOverdue) return "Overdue";
                if (IsToday) return "Today";
                return "Pending";
            }
        }

        public string StatusLabel => StatusKey switch
        {
            "Overdue" => "Przeterminowane",
            "Today" => "Dziś do zamknięcia",
            "Kompletna" => "Kompletne",
            "Posrednik" => "Pośrednik",
            _ => "W toku"
        };

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged([CallerMemberName] string n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public void RecomputeDerived()
        {
            OnChanged(nameof(IsKompletna));
            OnChanged(nameof(IsToday));
            OnChanged(nameof(IsOverdue));
            OnChanged(nameof(StatusKey));
            OnChanged(nameof(StatusLabel));
        }

        public static DostawaItem FromRow(DataRow r)
        {
            // Avatary NIE ładowane tutaj — patrz LoadAvatarsBackgroundAsync (progresywnie po renderze).
            return new DostawaItem
            {
                Id = r["ID"] != DBNull.Value ? Convert.ToInt32(r["ID"]) : 0,
                DataOdbioru = r["DataOdbioru"] != DBNull.Value ? Convert.ToDateTime(r["DataOdbioru"]) : DateTime.MinValue,
                Dostawca = r["Dostawca"] as string,
                Utworzone = r["Utworzone"] != DBNull.Value && Convert.ToBoolean(r["Utworzone"]),
                Wyslane = r["Wysłane"] != DBNull.Value && Convert.ToBoolean(r["Wysłane"]),
                Otrzymane = r["Otrzymane"] != DBNull.Value && Convert.ToBoolean(r["Otrzymane"]),
                IsPosrednik = r["Posrednik"] != DBNull.Value && Convert.ToBoolean(r["Posrednik"]),
                Auta = r["Auta"] != DBNull.Value ? Convert.ToInt32(r["Auta"]) : (int?)null,
                SztukiDek = r["SztukiDek"] != DBNull.Value ? Convert.ToDecimal(r["SztukiDek"]) : (decimal?)null,
                WagaDek = r["WagaDek"] != DBNull.Value ? Convert.ToDecimal(r["WagaDek"]) : (decimal?)null,
                SztSzuflada = r["SztSzuflada"]?.ToString(),
                KtoUtw = r["KtoUtw"] as string,
                KtoUtwID = r["KtoUtwID"] as string,
                KiedyUtw = r["KiedyUtw"] != DBNull.Value ? Convert.ToDateTime(r["KiedyUtw"]) : (DateTime?)null,
                KtoWysl = r["KtoWysl"] as string,
                KtoWyslID = r["KtoWyslID"] as string,
                KiedyWysl = r["KiedyWysl"] != DBNull.Value ? Convert.ToDateTime(r["KiedyWysl"]) : (DateTime?)null,
                KtoOtrzym = r["KtoOtrzym"] as string,
                KtoOtrzymID = r["KtoOtrzymID"] as string,
                KiedyOtrzm = r["KiedyOtrzm"] != DBNull.Value ? Convert.ToDateTime(r["KiedyOtrzm"]) : (DateTime?)null,
            };
        }
    }

    // ============ CONVERTERS ============

    public class BoolToVisibilityConv : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseBoolToVisibilityConv : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isPosrednik = value is bool b && b;
            if (parameter is string p && p == "Bool") return !isPosrednik;
            return isPosrednik ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class NullOrEmptyToVisibilityConv : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool empty = string.IsNullOrWhiteSpace(value as string);
            bool invert = parameter is string p && p == "Invert";
            return (empty ^ invert) ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StatusToBrushConv : IValueConverter
    {
        // Solid (left accent bar / dot)
        private static readonly System.Windows.Media.SolidColorBrush _overdue = Freeze("#D93B3B");
        private static readonly System.Windows.Media.SolidColorBrush _today = Freeze("#E89614");
        private static readonly System.Windows.Media.SolidColorBrush _kompletna = Freeze("#3A9D4A");
        private static readonly System.Windows.Media.SolidColorBrush _posrednik = Freeze("#3578D9");
        private static readonly System.Windows.Media.SolidColorBrush _pending = Freeze("#B6BFC8");

        // Tint (pigułka tło)
        private static readonly System.Windows.Media.SolidColorBrush _overdueTint = Freeze("#FCE3E3");
        private static readonly System.Windows.Media.SolidColorBrush _todayTint = Freeze("#FCEDD0");
        private static readonly System.Windows.Media.SolidColorBrush _kompletnaTint = Freeze("#DCEFDE");
        private static readonly System.Windows.Media.SolidColorBrush _posrednikTint = Freeze("#DCE7F7");
        private static readonly System.Windows.Media.SolidColorBrush _pendingTint = Freeze("#ECEFF2");

        // Dark text na tincie
        private static readonly System.Windows.Media.SolidColorBrush _overdueDark = Freeze("#9C2828");
        private static readonly System.Windows.Media.SolidColorBrush _todayDark = Freeze("#7C4A00");
        private static readonly System.Windows.Media.SolidColorBrush _kompletnaDark = Freeze("#1E6B2C")
;
        private static readonly System.Windows.Media.SolidColorBrush _posrednikDark = Freeze("#1F4D90");
        private static readonly System.Windows.Media.SolidColorBrush _pendingDark = Freeze("#4D5763");

        private static System.Windows.Media.SolidColorBrush Freeze(string hex)
        {
            var brush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string variant = parameter as string ?? "Solid";
            string key = value as string ?? "";
            return (variant, key) switch
            {
                ("Tint", "Overdue") => _overdueTint,
                ("Tint", "Today") => _todayTint,
                ("Tint", "Kompletna") => _kompletnaTint,
                ("Tint", "Posrednik") => _posrednikTint,
                ("Tint", _) => _pendingTint,
                ("Dark", "Overdue") => _overdueDark,
                ("Dark", "Today") => _todayDark,
                ("Dark", "Kompletna") => _kompletnaDark,
                ("Dark", "Posrednik") => _posrednikDark,
                ("Dark", _) => _pendingDark,
                (_, "Overdue") => _overdue,
                (_, "Today") => _today,
                (_, "Kompletna") => _kompletna,
                (_, "Posrednik") => _posrednik,
                _ => _pending
            };
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class RelativeDateConv : IValueConverter
    {
        private static readonly CultureInfo _pl = new CultureInfo("pl-PL");
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "";
            DateTime d;
            try { d = System.Convert.ToDateTime(value); } catch { return ""; }
            int diff = (d.Date - DateTime.Today).Days;
            string txt = diff switch
            {
                0 => "DZIŚ",
                1 => "JUTRO",
                -1 => "wczoraj",
                _ when diff > 0 && diff <= 7 => $"za {diff} dni",
                _ when diff < 0 && diff >= -7 => $"{-diff} dni temu",
                _ when diff < 0 => d.ToString("d MMM", _pl),
                _ => $"za {diff} dni"
            };
            return parameter is string p && p == "Short" ? txt : "  •  " + txt;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class DayOfWeekConv : IValueConverter
    {
        private static readonly CultureInfo _pl = new CultureInfo("pl-PL");
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "";
            try { return System.Convert.ToDateTime(value).ToString("dddd", _pl).ToUpper(); }
            catch { return ""; }
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class FullDateConv : IValueConverter
    {
        private static readonly CultureInfo _pl = new CultureInfo("pl-PL");
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "";
            try { return System.Convert.ToDateTime(value).ToString("d MMMM yyyy", _pl); }
            catch { return ""; }
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }
}
